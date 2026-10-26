using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Kartenbau;
using Reichsgilde.Kartenbau.Staedte;

/// <summary>
/// Kartenwerkstatt, Schritt 5c (PR #180): Wasser – Format, Bett, Spiegel (fällt mit dem Gelände, nie bergauf), Seen, Brücken und Furten,
/// Fähren, Karrennetz und Fahrzeit, Prüfung, Befehle mit Rückgängig, Natur, Kartenskript; Karten ohne Wasser unverändert.
/// </summary>
static class WasserTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static string Text(string kennung) => File.ReadAllText(Datenpfad.Karte(kennung));
    static Kartenort O(double x, double z) => new(x, z);
    static string Liste(IEnumerable<Befund> befunde) => string.Join("\n  ", befunde.Select(b => b.ToString()));
    static List<Befund> Fehler(Kartenentwurf e) => e.Befunde.Where(b => b.Schwere == Schwere.Fehler).ToList();

    /// <summary>Eine leere Karte 400 × 300 m mit 300 m Rand.</summary>
    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>Ein Fluss von West nach Ost entlang z = 0 (x −150 … 150).</summary>
    static EntwurfFluss Ostfluss(Kartenentwurf e, double breite, string art = "fluss") => e.FlussAnlegen(new[] { O(-150, 0), O(0, 0), O(150, 0) }, art, breite);

    /// <summary>Ein gleichmäßiger Hang nach Osten: Höhe = Gefälle · x (wie in den Geländetests).</summary>
    static void Hang(Kartenentwurf e, double gefaelle)
    {
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 1, Staerke: gefaelle * 100, Kante: 0), new[] { O(100, 0) });
        e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 1, Staerke: gefaelle * 100, Kante: 0), new[] { O(-100, 0) });
        e.Rampe(O(-100, 0), O(100, 0), breite: 400, kante: 0);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Wasser: Format – Stützpunkte [x, z], [x, z, breite], [x, z, breite, tiefe], Seen mit Wasserstand; geschrieben und vom Kern wieder gelesen", () =>
        {
            var e = Leer();
            var f = e.FlussAnlegen(new[] { O(-100, 20), O(0, 30), O(100, 20) }, "bach", 4, "Mühlbach");
            e.FlussBreiteSetzen(f, 1, 6);
            e.FlussTiefeSetzen(f, 2, 0.8);
            e.FlussStroemungSetzen(f, 0.7);
            e.SeeAnlegen(new[] { O(-60, -60), O(-20, -60), O(-20, -30), O(-60, -30) }, "teich", "Dorfteich", wasserstand: -0.5, tiefe: 1.5);
            var json = e.ZuJson();
            Pruefe.Wahr(json.Contains("\"linie\": [[-100, 20], [0, 30, 6], [100, 20, 4, 0.8]]"), json);
            Pruefe.Wahr(json.Contains("\"stroemung\": 0.7") && json.Contains("\"wasserstand\": -0.5") && json.Contains("\"tiefe\": 1.5"), "Felder");
            var karte = Stadtkarte.AusJson(json, Katalog.Value);
            Pruefe.Gleich(2, karte.Wasser.Count);
            var lauf = karte.Gelaendeform.Wasser.Laeufe.Single();
            Pruefe.Gleich("Mühlbach", lauf.Name);
            Pruefe.Ungefaehr(6, lauf.Punkte.First(p => p.Ort.Abstand(O(0, 30)) < 1e-6).Breite, 1e-9);
            Pruefe.Ungefaehr(0.8, lauf.Punkte[^1].Tiefe, 1e-9);
            Pruefe.Ungefaehr(Wasserform.Vorgabetiefe(4), lauf.Punkte[0].Tiefe, 1e-9);
            Pruefe.Ungefaehr(0.7, lauf.Stroemung, 1e-9);
            var see = karte.Gelaendeform.Wasser.Seen.Single();
            Pruefe.Wahr(see.Wasserstand == -0.5 && see.Tiefe == 1.5 && !see.StandAutomatisch, "See");
            // ein zweites Mal geöffnet und gespeichert: dieselbe Datei
            Pruefe.Gleich(json, Kartenentwurf.AusJson(json, Katalog.Value).ZuJson());
            // der Kern prüft beim Lesen
            Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson(json.Replace("\"art\": \"teich\"", "\"art\": \"tuempel\""), Katalog.Value), "Die Art „tuempel“");
        }),

        ("Wasser: Bett im Boden – Querprofil im Fluss, weiches Ufer, Strömung in Fließrichtung; das gespeicherte Raster bleibt leer", () =>
        {
            var e = Leer();
            Ostfluss(e, 6);
            var g = e.Gelaendeform;
            Pruefe.Wahr(!g.IstFlach && !g.HatHoehen && e.Hoehen.IstLeer, "Wasser ohne Höhen");
            var tiefe = Wasserform.Vorgabetiefe(6);
            var freibord = Wasserform.Freibord(6);                                                       // 0,6 m
            Pruefe.Ungefaehr(0.6, freibord, 1e-12);
            Pruefe.Ungefaehr(-freibord - tiefe, g.Hoehe(0, 0), 0.006);                                  // Mitte: Spiegel − Tiefe
            Pruefe.Ungefaehr(-freibord - tiefe * (1 - 4.0 / 9), g.Hoehe(0, 2), 0.006);                // 2 m neben der Mitte (Parabel)
            Pruefe.Wahr(g.Hoehe(0, 4) > -freibord && g.Hoehe(0, 4) < 0, $"Ufer {g.Hoehe(0, 4)}");    // Ufer bis 3 + 1,4 m
            Pruefe.Gleich(0.0, g.Hoehe(0, 6));                                                         // hinter der Uferkante
            Pruefe.Wahr(g.Wasser.ImUfer(0, 4.3) && !g.Wasser.ImUfer(0, 4.5), "Uferkante bei 4,4 m");
            Pruefe.Ungefaehr(1, Wasserform.Freibord(16), 1e-12);                                         // wie in den Modellen
            Pruefe.Ungefaehr(0.4, Wasserform.Freibord(1.6), 1e-12);
            Pruefe.Ungefaehr(tiefe, g.Wassertiefe(0, 0), 0.006);
            Pruefe.Wahr(g.Wasser.IstWasser(0, 2.9) && !g.Wasser.IstWasser(0, 3.1), "Rand bei 3 m");
            Pruefe.Ungefaehr(-0.6, g.Wasser.Spiegel(10, 1)!.Value, 1e-9);
            var (sx, sz, tempo) = g.Wasser.Stroemung(0, 0);
            Pruefe.Wahr(Math.Abs(sx - 1) < 1e-9 && Math.Abs(sz) < 1e-9 && tempo == 1, $"Strömung ({sx}, {sz})");
            Pruefe.Ungefaehr(1 - 0.5 * (2.9 / 3) * (2.9 / 3), g.Wasser.Stroemung(0, 2.9).Tempo, 1e-6);
            Pruefe.Gleich((0.0, 0.0, 0.0), g.Wasser.Stroemung(0, 20));
            Pruefe.Ungefaehr(7, g.Wasser.Uferabstand(0, 10), 1e-6);
            // eine Mauer am Ufer bleibt stehen, der Fluss gräbt sich unter ihr durch
            var mauer = e.MauerAnlegen(new[] { O(20, -20), O(20, 20) }, geschlossen: false);
            Pruefe.Gleich(0.0, g.Hoehe(20, 4));
            Pruefe.Wahr(g.Hoehe(20, 0) < -1, "Bett unter der Mauer");
            e.Rueckgaengig();
            Pruefe.Wahr(g.Hoehe(20, 4) < 0, "Ufer ohne Mauer");
            Pruefe.Wahr(!e.Enthaelt(mauer), "Mauer zurückgenommen");
        }),

        ("Wasser: Der Spiegel fällt mit dem Gelände und steigt nie – bergauf gezeichnet schneidet sich das Bett ein (Hinweis), umgekehrt nicht", () =>
        {
            var e = Leer();
            Hang(e, 0.03);   // nach Osten 3 % hinauf: bei x = −100 −3 m, bei x = 100 +3 m
            var f = e.FlussAnlegen(new[] { O(-90, 0), O(90, 0) }, "bach", 3);   // gezeichnet von West nach Ost = bergauf
            var lauf = e.Wasserform.Laeufe.Single();
            Pruefe.Wahr(lauf.Punkte.Zip(lauf.Punkte.Skip(1)).All(p => p.Second.Spiegel <= p.First.Spiegel), "nie bergauf");
            Pruefe.Ungefaehr(lauf.Punkte[0].Spiegel, lauf.Punkte[^1].Spiegel, 1e-9);   // bleibt auf der Höhe der Quelle
            Pruefe.Wahr(lauf.GroessterEinschnitt > 4, $"Einschnitt {lauf.GroessterEinschnitt}");
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "fluss-bergauf" && b.Andere.Contains(f) && b.Schwere == Schwere.Hinweis), Liste(e.Befunde));
            e.FlussUmkehren(f);
            lauf = e.Wasserform.Laeufe.Single();
            Pruefe.Wahr(lauf.Punkte[0].Ort == O(90, 0) && lauf.Punkte[^1].Spiegel < lauf.Punkte[0].Spiegel - 4, "fließt bergab");
            Pruefe.Wahr(lauf.Punkte.Zip(lauf.Punkte.Skip(1)).All(p => p.Second.Spiegel <= p.First.Spiegel), "nie bergauf");
            Pruefe.Ungefaehr(0, lauf.GroessterEinschnitt, 0.05);
            Pruefe.Wahr(e.Befunde.All(b => b.Regel != "fluss-bergauf"), Liste(e.Befunde));
            // ein Pinselstrich am Fluss ändert Spiegel und Bett auf ganzer Länge
            var vorher = e.Gelaendeform.Hoehe(80, 0);
            e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 6, Staerke: 2, Kante: 0), new[] { O(85, 0) });
            Pruefe.Wahr(e.Gelaendeform.Hoehe(-80, 0) < e.Wasserform.Laeufe.Single().Punkte[^1].Spiegel, "Bett unten");
            Pruefe.Wahr(e.Wasserform.Laeufe.Single().Punkte[0].Spiegel < lauf.Punkte[0].Spiegel - 1, "Quelle tiefer");
            Pruefe.Wahr(e.Gelaendeform.Hoehe(80, 0) < vorher, "Bett an der Quelle tiefer");
        }),

        ("Wasser: See – Wasserstand vom tiefsten Ufer oder fest, Boden fällt vom Ufer zur Tiefe; ein fester Stand über dem Ufer ist ein Hinweis", () =>
        {
            var e = Leer();
            Hang(e, 0.02);
            var s = e.SeeAnlegen(new[] { O(-40, -30), O(40, -30), O(40, 30), O(-40, 30) });
            var see = e.Wasserform.Seen.Single();
            Pruefe.Wahr(see.StandAutomatisch && see.Art == "see" && see.Tiefe == Wasserform.SeeTiefeVorgabe, "Vorgaben");
            Pruefe.Ungefaehr(-0.8 - Wasserform.SeeFreibord, see.Wasserstand, 0.02);   // das tiefste Ufer bei x = −40: −0,8 m
            Pruefe.Ungefaehr(see.Wasserstand - 2, e.Gelaendeform.Hoehe(0, 0), 0.01);
            Pruefe.Ungefaehr(see.Wasserstand - 0.3 * 2, e.Gelaendeform.Hoehe(0, -28), 0.01);  // 2 m vom Ufer: 0,6 m tief
            Pruefe.Wahr(e.Wasserform.ImSee(0, 0) && e.Wasserform.Stroemung(0, 0).Tempo == 0, "steht still");
            Pruefe.Wahr(e.Befunde.All(b => b.Regel != "see-ufer"), "automatisch nie über dem Ufer");
            e.SeeWasserstandSetzen(s, 0.5);
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "see-ufer" && b.Andere.Contains(s)), Liste(e.Befunde));
            Pruefe.Ungefaehr(0.5 - 2, e.Gelaendeform.Hoehe(0, 0), 0.01);
            e.SeeTiefeSetzen(s, 4);
            Pruefe.Ungefaehr(0.5 - 4, e.Gelaendeform.Hoehe(0, 0), 0.01);
            e.Rueckgaengig();
            e.Rueckgaengig();
            Pruefe.Wahr(e.Wasserform.Seen.Single().StandAutomatisch, "zurück");
        }),

        ("Wasser: Brücken und Furten entstehen, wo ein Weg einen Fluss quert – Furt unter 0,5 m Tiefe, Holz bis 20 m, Stein darüber; Deck und Pfeiler", () =>
        {
            Wasserquerung Querung(double breite, string art, double richtung = 0)
            {
                var e = Leer();
                Ostfluss(e, breite, art);
                var (dx, dz) = (Math.Sin(richtung * Math.PI / 180) * 40, Math.Cos(richtung * Math.PI / 180) * 40);
                e.WegAnlegen(Wegklasse.Land, new[] { O(-dx, -dz), O(dx, dz) }, glatt: false, name: "Landstraße");
                return e.Wasserplan.Querungen.Single();
            }
            var furt = Querung(3, "bach");
            Pruefe.Wahr(furt.Art == Uebergangsart.Furt && furt.Quert && furt.Karren && furt.Pfeiler.Count == 0, $"Furt {furt}");
            Pruefe.Ungefaehr(3, furt.Spannweite, 0.02);
            Pruefe.Ungefaehr(40 - 1.5 - Wasserform.Uferbreite(3), furt.VonStelle, 0.02);   // die Furt reicht von Uferkante zu Uferkante
            Pruefe.Ungefaehr(furt.UferAStelle, furt.VonStelle, 1e-9);
            Pruefe.Ungefaehr(furt.Spiegel - furt.Tiefe, furt.Deckhoehe, 1e-9);
            var holz = Querung(12, "fluss");
            Pruefe.Wahr(holz.Art == Uebergangsart.Holzbruecke && holz.IstBruecke, $"Holz {holz}");
            Pruefe.Ungefaehr(12, holz.Spannweite, 0.02);
            Pruefe.Ungefaehr(12 + 2 * 1.85, holz.Weite, 0.02);          // Uferkante zu Uferkante: Ufer 0,5 + 1,5 · 0,9 m
            Pruefe.Ungefaehr(holz.Weite + 2 * Wasserplan.HolzEnde, holz.Laenge, 0.02);
            Pruefe.Gleich(4, holz.Felder);                             // round(15,7 / 4) Joche
            Pruefe.Ungefaehr(holz.Weite / 4, holz.Feldlaenge, 1e-9);
            Pruefe.Gleich(4, holz.Pfeiler.Count);                      // ein Holm je Joch, in seiner Mitte
            Pruefe.Ungefaehr(holz.Feldlaenge / 2, holz.Pfeiler[0].Ort.Abstand(holz.UferA), 0.01);
            Pruefe.Ungefaehr(Wasserplan.HolzDeck, holz.Deckhoehe, 1e-9);  // Uferhöhe 0 + 0,6 m
            Pruefe.Ungefaehr(-Wasserform.Freibord(12), holz.Spiegel, 1e-9);
            Pruefe.Wahr(holz.Pfeiler.Skip(1).SkipLast(1).All(p => p.Fuss < holz.Spiegel) && holz.Pfeiler.All(p => p.Fuss < 0), "Holme im Flussbett, die äußeren am Ufer");
            Pruefe.Ungefaehr(0, holz.Richtung, 1e-6);                   // von Norden (z −40) nach Süden: +z ist 0° wie Rotation.Y
            var stein = Querung(30, "fluss");
            Pruefe.Wahr(stein.Art == Uebergangsart.Steinbruecke && stein.Felder == 4 && stein.Pfeiler.Count == 3, $"Stein {stein}");  // W 34: round(3,6)
            Pruefe.Ungefaehr(34, stein.Weite, 0.02);
            Pruefe.Ungefaehr((34 - 2 * 3) / 4.0, stein.Feldlaenge, 0.01);
            Pruefe.Ungefaehr(34 + 2 * Wasserplan.SteinRampe, stein.Laenge, 0.02);
            Pruefe.Ungefaehr(Wasserplan.SteinDeck, stein.Deckhoehe, 1e-9);
            Pruefe.Ungefaehr(stein.Feldlaenge + 1, stein.Pfeiler[0].Ort.Abstand(stein.UferA), 0.01);  // Mitte des ersten Pfeilers
            var schraeg = Querung(12, "fluss", 45);
            Pruefe.Ungefaehr(12 * Math.Sqrt(2), schraeg.Spannweite, 0.05);
            Pruefe.Wahr(schraeg.Art == Uebergangsart.Holzbruecke, "17 m schräg: Holz");
        }),

        ("Wasser: Prüfung der Übergänge – Brücke über 60 m (Hinweis), Weg durch einen See (Fehler), Weg im Fluss entlang (Hinweis)", () =>
        {
            var e = Leer();
            e.FlussAnlegen(new[] { O(-150, 0), O(150, 0) }, "fluss", 70);
            var weg = e.WegAnlegen(Wegklasse.Land, new[] { O(0, -60), O(0, 60) }, glatt: false);
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "bruecke-lang" && b.Schwere == Schwere.Hinweis && b.Andere.Contains(weg)), Liste(e.Befunde));
            var entlang = e.WegAnlegen(Wegklasse.Fussweg, new[] { O(-100, 30), O(-60, 30), O(-60, 33) }, glatt: false);
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "weg-im-wasser" && b.Andere.Contains(entlang)), Liste(e.Befunde));

            var s = Leer();
            s.SeeAnlegen(new[] { O(-20, -20), O(20, -20), O(20, 20), O(-20, 20) }, name: "Weiher");
            var durch = s.WegAnlegen(Wegklasse.Land, new[] { O(-60, 0), O(60, 0) }, glatt: false);
            var sperre = s.Wasserplan.Sperren.Single();
            Pruefe.Wahr(sperre.See == "Weiher" && Math.Abs(sperre.VonStelle - 40) < 0.02 && Math.Abs(sperre.BisStelle - 80) < 0.02, $"{sperre}");
            Pruefe.Wahr(Fehler(s).Any(b => b.Regel == "weg-see" && b.Andere.Contains(durch)), Liste(s.Befunde));
            Pruefe.Gleich(0, s.Wasserplan.Querungen.Count);
        }),

        ("Wasser: Prüfung – Gebäude und Bauzonen im Wasser sind Fehler (auch beim Ziehen), ein Hafen steht mit der Rückseite (Kai) am Ufer", () =>
        {
            var e = Leer();
            Ostfluss(e, 10);
            var platz = e.Setzen("werkstattplatz", 0, 10);   // 16 × 14 m: z 3 … 17, der Fluss reicht bis z = 5
            Pruefe.Wahr(Fehler(e).Any(b => b.Regel == "wasser" && b.Gebaeude.Contains(platz.Kennung)), Liste(e.Befunde));
            e.Verschieben(platz.Kennung, 0, 20);
            Pruefe.Wahr(!e.Befunde.Any(b => b.Regel == "wasser"), Liste(e.Befunde));
            Pruefe.Wahr(Kartenpruefung.PruefeStelle(e, "werkstattplatz", 50, 8, 0).Any(b => b.Regel == "wasser"), "Umriss beim Ziehen rot");
            var zone = e.BauzoneAnlegen(Lage.Vorstadt, new[] { O(-60, 2), O(-40, 2), O(-40, -20), O(-60, -20) });
            Pruefe.Wahr(Fehler(e).Any(b => b.Regel == "wasser" && b.Andere.Contains(zone)), Liste(e.Befunde));

            // Hafen 16 × 14 m: Drehung 0 = die Vorderseite (Weg) nach Süden, die Rückseite (Kai) nach Norden zum Fluss (Wasser bis z = 5)
            var hafen = e.Setzen(Kartenentwurf.HafenKatalog, 60, 13, 0);   // Rückseite bei z = 6
            Pruefe.Wahr(!e.Befunde.Any(b => b.Gebaeude.Contains(hafen.Kennung) && b.Regel is "wasser" or "ufer"), Liste(e.Befunde));
            e.Verschieben(hafen.Kennung, 60, 25);   // 13 m vom Wasser
            Pruefe.Wahr(Fehler(e).Any(b => b.Regel == "ufer" && b.Gebaeude.Contains(hafen.Kennung) && b.Text.Contains("13 m")), Liste(e.Befunde));
            e.Versetzen(hafen.Kennung, 60, 11, 180);  // falsch herum: der Hof stünde im Wasser, der Kai am Land
            Pruefe.Wahr(Fehler(e).Any(b => b.Regel == "wasser" && b.Gebaeude.Contains(hafen.Kennung)), Liste(e.Befunde));
            Pruefe.Wahr(Fehler(e).Any(b => b.Regel == "ufer" && b.Gebaeude.Contains(hafen.Kennung)), "Rückseite weg vom Wasser");
            var trocken = Leer();
            var h2 = trocken.Setzen(Kartenentwurf.HafenKatalog, 0, 0);
            Pruefe.Wahr(Fehler(trocken).Any(b => b.Regel == "ufer" && b.Gebaeude.Contains(h2.Kennung) && b.Text.Contains("kein Wasser")), Liste(trocken.Befunde));
        }),

        ("Wasser: Fähre – Anleger an beiden Ufern; im Wasser, ohne Wasser dazwischen oder weit vom Ufer ein Fehler, ohne Weg ein Hinweis", () =>
        {
            var e = Leer();
            Ostfluss(e, 20);
            var nord = e.WegAnlegen(Wegklasse.Land, new[] { O(0, -60), O(0, -12) }, glatt: false);
            var sued = e.WegAnlegen(Wegklasse.Land, new[] { O(0, 60), O(0, 12) }, glatt: false);
            var f = e.FaehreSetzen(O(0.8, -12.5), O(-0.5, 11.2));
            Pruefe.Wahr(f.Von == O(0, -12) && f.Bis == O(0, 12), $"eingerastet {f.Von} {f.Bis}");
            Pruefe.Wahr(!e.Befunde.Any(b => b.Regel.StartsWith("faehre")), Liste(e.Befunde));
            var info = e.Wasserplan.Faehren.Single();
            Pruefe.Wahr(info.Gewaesser == "Fluss" && Math.Abs(info.UeberWasser - 20) < 0.6 && info.Laenge == 24, $"{info}");
            // die Stege stehen an der Uferkante (10 m + 2 m Ufer von der Mitte), der Anleger selbst 2 m davor auf dem Land
            Pruefe.Wahr(info.VonUferkante.Abstand(O(0, -12)) < 0.01 && info.BisUferkante.Abstand(O(0, 12)) < 0.01, $"{info.VonUferkante} {info.BisUferkante}");
            e.FaehreAnlegerVerschieben(f, 1, 0, 5, einrasten: false);
            Pruefe.Wahr(Fehler(e).Any(b => b.Regel == "faehre" && b.Text.Contains("im Wasser") && b.Andere.Contains(f)), Liste(e.Befunde));
            e.FaehreAnlegerVerschieben(f, 1, 30, 40, einrasten: false);
            Pruefe.Wahr(Fehler(e).Any(b => b.Regel == "faehre" && b.Text.Contains("30 m vom Wasser")), Liste(e.Befunde));
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "faehre-weg"), "kein Weg am Anleger");
            e.FaehreAnlegerVerschieben(f, 1, 50, -20, einrasten: false);
            Pruefe.Wahr(Fehler(e).Any(b => b.Regel == "faehre" && b.Text.Contains("kein Wasser")), Liste(e.Befunde));
            // ein Wegende rastet am Anleger ein
            e.Rueckgaengig();
            e.Rueckgaengig();
            e.Rueckgaengig();
            Pruefe.Gleich(O(0, 12), f.Bis);
            var fang = e.Einrasten(1.2, 12.5, ausser: sued);
            Pruefe.Wahr(fang is { Ziel: Fangziel.Anleger } x && ReferenceEquals(x.Faehre, f) && x.Ort == O(0, 12), $"{fang}");
            Pruefe.Wahr(e.Enthaelt(nord), "Wege bleiben");
        }),

        ("Karrennetz mit Wasser: Brücke wie Straße, Furt und Fähre als eigene Fahrbahn (Minuten aus logistik.wasser), See gesperrt, Wegwahl meidet die Fähre", () =>
        {
            // Fluss 20 m entlang z = 0; Straßen nördlich und südlich, die Fähre dazwischen; ein Umweg über eine Brücke bei x = d
            Stadtkarte Karte(double d)
            {
                var e = Leer();
                Ostfluss(e, 20);
                e.WegAnlegen(Wegklasse.Land, new[] { O(0, -60), O(0, -12) }, glatt: false);
                e.WegAnlegen(Wegklasse.Land, new[] { O(0, 12), O(0, 60) }, glatt: false);
                e.FaehreSetzen(O(0, -12), O(0, 12));
                e.WegAnlegen(Wegklasse.Land, new[] { O(0, -60), O(d, -60), O(d, 60), O(0, 60) }, glatt: false, name: "Umweg");
                return e.ZuStadtkarte();
            }
            var wasser = Daten.Value.Logistik.Wasser!;
            Pruefe.Gleich((5, 30), (wasser.FurtMinuten, wasser.FaehreMinuten));
            var nah = Karrennetz.Von(Karte(100)).Fahrt(O(0, -60), O(0, 60));   // Umweg 320 m < Fähre 120 + 300
            Pruefe.Wahr(nah.All(s => s.Art != Fahrbahn.Faehre) && nah.Any(s => s.Art == Fahrbahn.Bruecke), "über die Brücke");
            Pruefe.Ungefaehr(320, nah.Sum(s => s.Laenge), 1e-6);
            Pruefe.Gleich(0, wasser.Aufschlag(nah));
            var weit = Karrennetz.Von(Karte(200)).Fahrt(O(0, -60), O(0, 60)); // Umweg 520 m > 420
            Pruefe.Wahr(weit.Count(s => s.Art == Fahrbahn.Faehre) == 1, string.Join(", ", weit));
            Pruefe.Ungefaehr(120, weit.Sum(s => s.Laenge), 1e-6);
            Pruefe.Gleich(30, wasser.Aufschlag(weit));
            Pruefe.Gleich(Karrennetz.Von(Karte(200)).Fahrt(O(0, 60), O(0, -60)).Sum(s => s.Laenge), weit.Sum(s => s.Laenge));
            // Furt: eine Straße durch einen Bach – mehrere Stücke in der Furt sind ein Aufschlag
            var b = Leer();
            Ostfluss(b, 3, "bach");
            b.WegAnlegen(Wegklasse.Feldweg, new[] { O(0, -40), O(0, 40) }, glatt: false);
            var furt = Karrennetz.Von(b.ZuStadtkarte()).Fahrt(O(0, -40), O(0, 40));
            Pruefe.Wahr(furt.Count(s => s.Art == Fahrbahn.Furt) >= 3, string.Join(", ", furt));
            Pruefe.Gleich(5, wasser.Aufschlag(furt));
            Pruefe.Ungefaehr(80, furt.Sum(s => s.Laenge), 1e-6);
            // See: die Straße hindurch ist gesperrt
            var s = Leer();
            s.SeeAnlegen(new[] { O(-20, -20), O(20, -20), O(20, 20), O(-20, 20) });
            s.WegAnlegen(Wegklasse.Land, new[] { O(-60, 0), O(60, 0) }, glatt: false);
            Pruefe.Gleich(0, Karrennetz.Von(s.ZuStadtkarte()).Fahrt(O(-60, 0), O(60, 0)).Count);
        }),

        ("Fahrzeit mit Wasser: Eine Furt auf dem Weg zum Markt kostet hin und zurück die Minuten aus logistik.wasser; Brücken und Karten ohne Wasser nichts", () =>
        {
            // Probestadt: ein Bach quert die Hauptstraße zwischen der Werkstatt des Spielers (−30 | −10) und dem Markt
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            e.FlussAnlegen(new[] { O(-20, -8), O(-20, 8) }, "bach", 3);
            var (sim, spieler) = e.Ausprobieren(Daten.Value, "Schmied");
            var w = sim.Wirtschaft!;
            var west = w.Betriebe.Single(b => b.Konto == spieler);
            var basis = Daten.Value.Logistik.FahrzeitAb(west.Ort);
            var (hin, zurueck) = w.Fahrten(west)!.Value;
            Pruefe.Wahr(hin.Any(x => x.Art == Fahrbahn.Furt) && zurueck.Any(x => x.Art == Fahrbahn.Furt), "Furt hin und zurück");
            Pruefe.Gleich((basis + 5, basis + 5), (w.Fahrzeit(west, true), w.Fahrzeit(west, false)));
            // tief genug für eine Brücke: kein Aufschlag
            var b = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            var fluss = b.FlussAnlegen(new[] { O(-20, -8), O(-20, 8) }, "bach", 3);
            b.FlussTiefeSetzen(fluss, -1, 1);
            var (simB, spielerB) = b.Ausprobieren(Daten.Value, "Schmied");
            var westB = simB.Wirtschaft!.Betriebe.Single(x => x.Konto == spielerB);
            Pruefe.Wahr(simB.Wirtschaft.Fahrten(westB)!.Value.Hin.Any(x => x.Art == Fahrbahn.Bruecke), "über die Brücke");
            Pruefe.Gleich((basis, basis), (simB.Wirtschaft.Fahrzeit(westB, true), simB.Wirtschaft.Fahrzeit(westB, false)));
            // nach dem Laden dieselbe Zeit
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var west2 = geladen.Wirtschaft!.Betriebe.Single(x => x.Name == west.Name);
            Pruefe.Gleich(basis + 5, geladen.Wirtschaft.Fahrzeit(west2, true));
            // Messstadt: kein Wasser, nichts hält auf
            var mess = Stadtkarte.AusDatei(Datenpfad.Karte("messstadt"));
            Pruefe.Wahr(mess.Gelaendeform.IstFlach && mess.Gelaendeform.Wasser.IstLeer && !mess.Wasserplan.HaeltAuf && mess.Wasserplan.Querungen.Count == 0, "messstadt");
            // Greybox (seit PR #199 mit dem Grenzfluss im Süden): Kein Weg quert ihn, nichts hält die Karren auf
            var grey = Stadtkarte.AusDatei(Datenpfad.Karte("greybox"));
            Pruefe.Wahr(!grey.Gelaendeform.Wasser.IstLeer && !grey.Wasserplan.HaeltAuf, "greybox");
        }),

        ("Wasser: Befehle mit Rückgängig – Fluss anlegen, Punkte setzen, verschieben, löschen, Breite und Tiefe je Punkt, umkehren; See; Fährstelle mit Einrasten", () =>
        {
            var e = Leer();
            var weg = e.WegAnlegen(Wegklasse.Land, new[] { O(-50, 40), O(50, 40) }, glatt: false);
            var vorher = e.ZuJson();
            var meldungen = new List<Kartenaenderung>();
            e.Geaendert += meldungen.Add;
            var f = e.FlussAnlegen(new[] { O(-100, 0), O(0, 5), O(100, 0) }, "bach");
            Pruefe.Wahr(f.Name == "Bach" && f.Breite == 3 && f.Punkte.Count == 3 && meldungen.Single().Teil == Kartenteil.Gewaesser && meldungen[0].Fluss == f, "angelegt");
            Pruefe.Gleich(f, e.Gewaesser.Single());
            e.FlussBreiteSetzen(f, 0, 5);
            e.FlussBreiteSetzen(f, 1, 7);
            e.FlussPunktEinfuegen(f, 1, -50, 3);
            Pruefe.Gleich((double?)6, f.Breiten[1], "Mittel der Nachbarn");
            Pruefe.Gleich((double?)null, f.Tiefen[1], "keine eigene Tiefe");
            Pruefe.Gleich(2, e.FlussPunktEinfuegenBei(f, -20, 30));   // hinter dem Punkt bei −50
            e.FlussPunktVerschieben(f, 2, -20, 8);
            Pruefe.Gleich(O(-20, 8), f.Punkte[2]);
            e.FlussTiefeSetzen(f, 4, 1.2);
            e.FlussStroemungSetzen(f, 0.33);
            Pruefe.Ungefaehr(0.35, f.Stroemung!.Value, 1e-9);
            e.FlussUmkehren(f);
            Pruefe.Wahr(f.Punkte[0] == O(100, 0) && f.Tiefen[0] == 1.2 && f.Breiten[^1] == 5, "umgekehrt mit Breite und Tiefe");
            Pruefe.Wahr(e.FlussPunktLoeschen(f, 1), "gelöscht");
            e.FlussVerschieben(f, 2, 0);
            e.GewaesserUmbenennen(f, "Mühlbach");
            e.GewaesserArtSetzen(f, "fluss");
            Pruefe.Wirft(() => e.GewaesserArtSetzen(f, "see"));
            var s = e.SeeAnlegen(new[] { O(-30, -60), O(30, -60), O(0, -30) }, "teich");
            e.EckeVerschieben(s, 2, 0, -25);
            e.SeeWasserstandSetzen(s, -0.333);
            Pruefe.Gleich((double?)-0.33, s.Wasserstand);
            var fahre = e.FaehreSetzen(O(-10, 41), O(30, 70), "Fähre am Bach");
            Pruefe.Wahr(fahre.Von == O(-10, 40) && weg.Punkte.Contains(O(-10, 40)), "Anleger am Weg, der Weg bekommt dort einen Stützpunkt");
            e.FaehreAnlegerVerschieben(fahre, 1, 20, 70);
            e.FaehreUmbenennen(fahre, "Fähre");
            var json = e.ZuJson();
            Pruefe.Wahr(json.Contains("\"wasser\"") && json.Contains("\"uebergaenge\"") && json.Contains("Mühlbach"), json);
            // alles zurück: dieselbe Datei wie vorher, alles wieder vor: dieselbe wie eben
            while (e.Geschichte.Schritte > 1) e.Rueckgaengig();   // der Weg bleibt
            Pruefe.Gleich(vorher, e.ZuJson());
            Pruefe.Wahr(e.Gewaesser.Count == 0 && e.Faehren.Count == 0 && weg.Punkte.Count == 2, "leer");
            while (e.Wiederholen()) { }
            Pruefe.Gleich(json, e.ZuJson());
            Pruefe.Wahr(meldungen.Any(m => m.Teil == Kartenteil.Faehre && m.Faehre == fahre) && meldungen.Any(m => m.See == s), "Meldungen");
            Pruefe.Gleich(json, Kartenentwurf.AusJson(json, Katalog.Value).ZuJson());
        }),

        ("Wasser: Natur und Felder bleiben vom Wasser fern (3 m), Bäume wachsen weiter am Ufer", () =>
        {
            var e = Leer();
            e.NaturflaecheAnlegen("wald", new[] { O(-100, -60), O(100, -60), O(100, 60), O(-100, 60) });
            var ohne = e.NaturAlle();
            Ostfluss(e, 10);
            e.SeeAnlegen(new[] { O(40, 20), O(80, 20), O(80, 50), O(40, 50) });
            var mit = e.NaturAlle();
            Pruefe.Wahr(mit.Count < ohne.Count && mit.Count > ohne.Count / 2, $"{ohne.Count} → {mit.Count}");
            Pruefe.Wahr(mit.All(p => e.Wasserform.Uferabstand(p.X, p.Z) >= Naturfreiraum.Abstand - 1e-6), "nichts im Wasser");
            Pruefe.Wahr(mit.Any(p => e.Wasserform.Uferabstand(p.X, p.Z) < 6), "Bäume am Ufer");
            e.FeldAnlegen(new[] { O(-100, -100), O(100, -100), O(100, 100), O(-100, 100) });
            Pruefe.Wahr(e.FelderAlle().All(a => a.Ecken().All(p => !e.Wasserform.IstWasser(p.X, p.Z))), "kein Acker im Wasser");
        }),

        ("Wasser: Kartenskript – fluss, see, faehre schreiben nur ohne Fehler; die Vorschau zeichnet Wasser, Brücken, Furten und Fähren", () =>
        {
            var ordner = Path.Combine(Path.GetTempPath(), "rg-wasser-" + Guid.NewGuid().ToString("N"));
            try
            {
                var projekt = Projektordner.Finden()!;
                Directory.CreateDirectory(ordner);
                var datei = Path.Combine(ordner, "probestadt.json");
                File.WriteAllText(datei, new Probestadt().Bauen(Katalog.Value, Daten.Value).ZuJson());
                var aus = new StringWriter();
                // ein Fluss quer durch den Norden (der Waldweg quert ihn), ein Teich im Randstreifen
                Pruefe.Gleich(0, Befehle.Fluss(projekt, datei, new[] { (O(-400, -120), (double?)null, (double?)null), (O(400, -120), (double?)24, (double?)2) },
                    "fluss", "Nordfluss", 20, null, null, aus), aus.ToString());
                Pruefe.Gleich(0, Befehle.See(projekt, datei, new[] { O(250, 180), O(300, 180), O(300, 220) }, "teich", null, null, null, aus), aus.ToString());
                // eine Fähre ohne Wasser dazwischen: Fehler – nichts geschrieben
                var stand = File.ReadAllText(datei);
                Pruefe.Gleich(1, Befehle.Faehre(projekt, datei, O(100, 0), O(150, 0), "Fähre", aus));
                Pruefe.Gleich(stand, File.ReadAllText(datei));
                var k = Stadtkarte.AusDatei(datei, projekt.Katalog);
                Pruefe.Wahr(k.Wasser.Single(w => w.Name == "Nordfluss").Linie![1].Length == 4 && k.Wasser.Any(w => w.Art == "teich" && w.Name == "Teich"),
                    "Fluss mit Breite und Tiefe am Ende, Teich");
                Pruefe.Wahr(k.Wasserplan.Querungen.Any(q => q.WegName == "Waldweg" && q.IstBruecke), "der Waldweg quert auf einer Brücke");
                var svg = Reichsgilde.Kartenbau.Vorschau.Zeichnen(Kartenentwurf.AusJson(File.ReadAllText(datei), Katalog.Value));
                Pruefe.Wahr(svg.Contains("#AFC7D5") && (svg.Contains("#7D7F78") || svg.Contains("#8A5A2B")), "Wasser und Brücke im Bild");
                Pruefe.Wahr(Umbau.Finde("greybox-hafen") is { Karte: "greybox" }, "Umbau für die Greybox");
            }
            finally
            {
                if (Directory.Exists(ordner)) Directory.Delete(ordner, recursive: true);
            }
        }),

        ("Katalog: Wasser-Bausteine aus game/assets/modelle/wasser – Hafen (Umlandstelle am Ufer, 16 × 14 m), Brücken, Fähre, Furt, Ufersteine", () =>
        {
            var k = Katalog.Value;
            Pruefe.Wahr(k.Finde(Kartenentwurf.HafenKatalog) is { Art: Bausteinart.Umlandstelle, AmUfer: true, Modell: "wasser/wasser_hafen", Knoten: "Hafen" } h
                        && h.Groesse!.SequenceEqual(new double[] { 16, 14 }), "hafenplatz");
            foreach (var (kennung, modell) in new[] { ("holzbruecke", "wasser_holzbruecke"), ("steinbruecke", "wasser_steinbruecke"), ("faehre", "wasser_faehre"), ("furt", "wasser_steine") })
                Pruefe.Wahr(k.Finde(kennung) is { Art: Bausteinart.Wasserbau } b && b.Modell == "wasser/" + modell, kennung);
            Pruefe.Wahr(k.Finde("furt")!.Varianten == 3 && k.Finde("ufersteine") is { Art: Bausteinart.Natur, Varianten: 3, Knoten: "Ufersteine" }, "Varianten");
            Pruefe.Wahr(!Bausteinart.ImGebaeude.Contains(Bausteinart.Wasserbau), "Wasserbau steht in keiner Karte");
            var befunde = Katalogpruefung.Pruefen(k, Projektordner.Finden()!.Modelle).Where(b => b.Gebaeude.Any(x => x is "hafenplatz" or "holzbruecke" or "steinbruecke" or "faehre" or "furt" or "ufersteine"));
            Pruefe.Wahr(!befunde.Any(), Liste(befunde));
        }),

        ("Probestadt (Stadtskript): Bach mit Furt am Waldweg und Holzbrücke vor dem Westtor, Fluss mit Fähre und Hafen, Teich – keine Fehler, keine Hinweise; die Datei ist das Skript", () =>
        {
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            Pruefe.Gleich(Text("probestadt"), e.ZuJson());
            Pruefe.Wahr(e.Befunde.Count == 0, Liste(e.Befunde));
            var plan = e.Wasserplan;
            Pruefe.Wahr(plan.Querungen.Single(q => q.WegName == "Waldweg") is { Art: Uebergangsart.Furt, GewaesserName: "Mühlbach" }, "Furt");
            Pruefe.Wahr(plan.Querungen.Single(q => q.WegName == "Landstraße nach Westen") is { Art: Uebergangsart.Holzbruecke, Karren: true }, "Holzbrücke");
            Pruefe.Wahr(plan.Faehren.Single() is { Gewaesser: "Sandfluss", VonImWasser: false, BisImWasser: false }, "Fähre");
            Pruefe.Wahr(e.Wasserform.Seen.Single().Name == "Mühlenteich" && e.Wasserform.Laeufe.All(l => l.GroessterEinschnitt < Wasserform.Schluchtgrenze), "Teich, nichts bergauf");
            var hafen = e.Gebaeude.Single(g => g.Katalog == Kartenentwurf.HafenKatalog);
            Pruefe.Wahr(hafen.Name == "Hafen am Sandfluss" && hafen.Drehung == 180, "Hafen am Nordufer, Kai nach Süden");
            Pruefe.Wahr(plan.Querungen.Single(q => q.WegName == "Weg zum Hafen") is { Art: Uebergangsart.Furt }, "Zufahrt durch die Furt");
            var netz = Karrennetz.Von(e.ZuStadtkarte());
            Pruefe.Wahr(netz.Fahrt(O(-130, 60), O(-130, 200)).Any(x => x.Art == Fahrbahn.Faehre), "über die Fähre nach Süden");
        }),

        ("Köln (Stadtskript): der Rhein fließt nach Norden bis zur Kante, die Rheinbrücke ist eine Steinbrücke, die Fähre Mülheim verbindet beide Ufer, der Duffesbach quert in Furt und Holzbrücke", () =>
        {
            var e = Kartenentwurf.AusJson(Text("koeln"), Katalog.Value);
            var rhein = e.Wasserform.Laeufe.Single(l => l.Name == "Rhein");
            Pruefe.Wahr(rhein.Punkte[0].Ort.Z > rhein.Punkte[^1].Ort.Z && rhein.Punkte[0].RichtungZ < 0, "nach Norden");
            Pruefe.Wahr(e.Wasserplan.Bruecken.Single(b => b.WegName == "Rheinbrücke") is { Art: Uebergangsart.Steinbruecke, Quert: true }, "Rheinbrücke");
            var faehre = e.Wasserplan.Faehren.Single();
            Pruefe.Wahr(faehre.Gewaesser == "Rhein" && !faehre.VonImWasser && !faehre.BisImWasser, $"{faehre}");
            Pruefe.Wahr(!e.HatFehler, Liste(Fehler(e)));
            Pruefe.Wahr(e.Wasserform.Seen.Count == 0 && e.Wasserform.Laeufe.Count == 2, "Rhein und Duffesbach");
            var g = e.Kartengrenzen.Aussen;
            Pruefe.Wahr(Math.Abs(rhein.Punkte[0].Ort.Z - g.Z1) < 1 && Math.Abs(rhein.Punkte[^1].Ort.Z - g.Z0) < 1, "der Rhein läuft von Außenkante zu Außenkante");
            Pruefe.Wahr(rhein.Punkte.All(p => p.Breite is >= 60 and <= 100), "60 bis 100 m breit");
            var bach = e.Wasserplan.Querungen.Where(q => q.GewaesserName == "Duffesbach").ToList();
            Pruefe.Wahr(bach.Any(q => q.Art == Uebergangsart.Furt) && bach.Any(q => q.Art == Uebergangsart.Holzbruecke && q.WegName == "Severinstraße"), $"Furten und eine Holzbrücke am Duffesbach: {string.Join(", ", bach.Select(q => q.WegName + " " + q.Art))}");
        }),
    };
}
