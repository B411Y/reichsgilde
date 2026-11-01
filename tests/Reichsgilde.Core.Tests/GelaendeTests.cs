using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Kartenbau;
using Reichsgilde.Kartenbau.Staedte;

/// <summary>
/// Kartenwerkstatt, Schritt 5b (PR #179): Gelände – Höhenraster (packen, Datei, Spielstand), Pinsel mit Rückgängig, Begrenzung, Größenänderung,
/// Fundament, Hangsperre, Bett und Profil der Wege, flache Karten unverändert.
/// </summary>
static class GelaendeTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static string Text(string kennung) => File.ReadAllText(Datenpfad.Karte(kennung));
    static Kartenort O(double x, double z) => new(x, z);
    static string Liste(IEnumerable<Befund> befunde) => string.Join("\n  ", befunde.Select(b => b.ToString()));

    /// <summary>Eine leere Karte 400 × 300 m mit 300 m Rand (die ganze Karte: x −500 … 500, z −450 … 450).</summary>
    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>Ein gleichmäßiger Hang nach Osten (x) von −100 bis 100 m: Höhe = Gefälle · x (zwei Punkte setzen, Rampe dazwischen).</summary>
    static void Hang(Kartenentwurf e, double gefaelle)
    {
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 1, Staerke: gefaelle * 100, Kante: 0), new[] { O(100, 0) });
        e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 1, Staerke: gefaelle * 100, Kante: 0), new[] { O(-100, 0) });
        e.Rampe(O(-100, 0), O(100, 0), breite: 400, kante: 0);
    }

    /// <summary>Die Höhe eines Rasterpunkts in cm am Ort (x, z) (ganze gerade Meter).</summary>
    static int Cm(Kartenentwurf e, double x, double z) => e.Hoehen.Wert((int)(x / 2), (int)(z / 2));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Höhenraster: packen und entpacken ergibt dieselben Werte (Ebene, Rauschen, −20 und +60 m); ein gleichmäßiger Hang packt klein", () =>
        {
            var ebene = new short[64 * 64];
            var rauh = new short[64 * 64];
            for (var j = 0; j < 64; j++)
                for (var i = 0; i < 64; i++)
                {
                    ebene[j * 64 + i] = (short)(i * 7 - j * 3 - 100);
                    rauh[j * 64 + i] = (short)Math.Round(Gelaendepinsel.Rauschwert(3, i * 2, j * 2, 10) * 1500 + 500);
                }
            rauh[0] = Hoehenraster.TiefsteCm;
            rauh[4095] = Hoehenraster.HoechsteCm;
            foreach (var w in new[] { ebene, rauh, new short[64 * 64] })
                Pruefe.Wahr(Hoehenraster.Entpacken(Hoehenraster.Packen(w))!.AsSpan().SequenceEqual(w), "gleich nach dem Entpacken");
            Pruefe.Wahr(Hoehenraster.Packen(ebene).Length < 400, $"eine Ebene packt klein (ungepackt 10.924 Zeichen): {Hoehenraster.Packen(ebene).Length} Zeichen");
            Pruefe.Gleich(null, Hoehenraster.Entpacken("kaputt!"));
            Pruefe.Gleich(null, Hoehenraster.Entpacken(Convert.ToBase64String(new byte[] { 1, 2, 3 })));
        }),

        ("Höhenraster: am Boden verankert (Punkt i, j bei 2·i, 2·j), Kacheln zu 64 Punkten, bilinear, Steigung, Ausschnitt über Kachelgrenzen", () =>
        {
            var r = new Hoehenraster();
            Pruefe.Wahr(r.IstLeer && r.Hoehe(5, 5) == 0, "leer = flach");
            r.WertSetzen(-1, 0, 100);
            r.WertSetzen(0, 0, 300);
            r.WertSetzen(0, 1, 500);
            Pruefe.Gleich(2, r.Kacheln.Count);  // (−1, 0) und (0, 0)
            Pruefe.Wahr(r.Kacheln.Contains((-1, 0)) && r.Kacheln.Contains((0, 0)), string.Join(", ", r.Kacheln));
            Pruefe.Ungefaehr(2.0, r.Hoehe(-1, 0), 1e-9);   // zwischen −2 (1 m) und 0 (3 m)
            Pruefe.Ungefaehr(4.0, r.Hoehe(0, 1), 1e-9);    // zwischen (0|0) 3 m und (0|2) 5 m
            Pruefe.Ungefaehr(1.0, r.Steigung(-1, 0).X, 1e-9); // 2 m auf 2 m
            var a = r.Ausschnitt(-2, 0, 4, 2);
            Pruefe.Gleich("0 1 3 0 0 0 5 0", string.Join(" ", a));
            r.WertSetzen(5, 5, 10000);
            Pruefe.Gleich((short)6000, r.Wert(5, 5), "auf +60 m begrenzt");
            r.WertSetzen(5, 5, -9999);
            Pruefe.Gleich((short)-2000, r.Wert(5, 5), "auf −20 m begrenzt");
            r.BereichLeeren(-100, -100, 100, 100);
            Pruefe.Wahr(r.IstLeer, "geleert, die Kacheln fallen weg");
        }),

        ("Höhenraster: Feld hoehen schreiben und lesen (Entwurf und Stadtkarte), unveränderte Kacheln Byte für Byte, kaputte Felder melden", () =>
        {
            var e = Leer();
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 20, Staerke: 3), new[] { O(0, 0) });
            e.GelaendePinseln(new Pinsel(Pinselart.Rauschen, Radius: 60, Staerke: 2, Kante: 0.3, Startwert: 5, Massstab: 15), new[] { O(200, 100) });
            var json = e.ZuJson();
            Pruefe.Wahr(json.Contains("\"hoehen\": {\n  \"abstand\": 2,\n  \"einheit\": \"cm\",\n  \"kachel\": 64,\n  \"kacheln\": [\n"), json[^400..]);
            Pruefe.Wahr(json.IndexOf("\"hoehen\"", StringComparison.Ordinal) > json.IndexOf("\"bauzonen\"", StringComparison.Ordinal), "hoehen hinten");
            var wieder = Kartenentwurf.AusJson(json, Katalog.Value);
            Pruefe.Gleich(json, wieder.ZuJson(), "geschrieben und wieder gelesen");
            foreach (var (i, j) in new[] { (0, 0), (5, 3), (100, 50), (95, 48) })
                Pruefe.Gleich(e.Hoehen.Wert(i, j), wieder.Hoehen.Wert(i, j), $"Punkt {i}, {j}");
            var karte = Stadtkarte.AusJson(json, Katalog.Value);
            Pruefe.Gleich((short)300, karte.Hoehen!.Wert(0, 0));
            Pruefe.Ungefaehr(3, karte.Gelaendeform.Hoehe(0, 0), 1e-9);
            // eine Kachel aus der Datei bleibt als Text, wie sie war – auch wenn eine andere sich ändert
            var vorher = JsonNode.Parse(json)!["hoehen"]!["kacheln"]!.AsArray().Select(k => k!.ToJsonString()).ToList();
            wieder.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 4, Staerke: 1), new[] { O(-400, -400) });
            var nachher = JsonNode.Parse(wieder.ZuJson())!["hoehen"]!["kacheln"]!.AsArray().Select(k => k!.ToJsonString()).ToList();
            Pruefe.Gleich(vorher.Count + 1, nachher.Count);
            Pruefe.Wahr(vorher.All(nachher.Contains), "die alten Kacheln unverändert");
            // kaputt
            foreach (var (kaputt, text) in new[]
            {
                ("{\"abstand\": 1, \"einheit\": \"cm\", \"kachel\": 64, \"kacheln\": []}", "abstand"),
                ("{\"abstand\": 2, \"einheit\": \"dm\", \"kachel\": 64, \"kacheln\": []}", "einheit"),
                ("{\"abstand\": 2, \"einheit\": \"cm\", \"kachel\": 64, \"kacheln\": [{\"i\": 0, \"j\": 0, \"daten\": \"xx\"}]}", "nicht lesbar"),
            })
            {
                var o = JsonNode.Parse(json)!.AsObject();
                o["hoehen"] = JsonNode.Parse(kaputt);
                Pruefe.WirftMitMeldung(() => Kartenentwurf.AusJson(o.ToJsonString(), Katalog.Value), text);
                Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson(o.ToJsonString(), Katalog.Value), text);
            }
        }),

        ("Flache Karte: Die Messstadt hat kein Gelände – gespeichert Byte für Byte dieselbe Datei, Boden 0", () =>
        {
            // Die Greybox hat seit PR #199 Gelände (Flussufer, Hafen) – sie zählt nicht mehr zu den flachen Karten.
            foreach (var kennung in new[] { "messstadt" })
            {
                var e = Kartenentwurf.AusJson(Text(kennung), Katalog.Value);
                var nass = false; // die Messstadt hat kein Wasser
                Pruefe.Wahr(!e.Gelaendeform.HatHoehen && e.Gelaendeform.IstFlach == !nass && !Text(kennung).Contains("\"hoehen\""), kennung);
                Pruefe.Gleich(Text(kennung), e.ZuJson(), kennung);
                Pruefe.Gleich(0.0, e.Gelaendeform.Hoehe(-60, -20), kennung);
                Pruefe.Wahr(e.Wege.All(w => e.Profil(w).All(h => h == 0)), "Profile 0");
                var fern = e.Gebaeude.First(g => e.Wasserform.Uferabstand(g.X, g.Z) > 40);
                Pruefe.Gleich(new Fundament(0, 0), e.Fundament(fern));
                Pruefe.Wahr(e.Befunde.All(b => b.Regel != "hang"), kennung);
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                Pruefe.Wahr(karte.Hoehen is null && karte.Gelaendeform.IstFlach == !nass, kennung);
            }
        }),

        ("Köln: Terrasse der Altstadt – 4 m über dem Rhein, Gefälle von x = −60 bis 30 (4,4 %), Hochufer ohne Hangfehler; der Rhein läuft nicht bergauf", () =>
        {
            var e = Kartenentwurf.AusJson(Text("koeln"), Katalog.Value);
            Pruefe.Wahr(e.Gelaendeform.HatHoehen && Text("koeln").Contains("\"hoehen\""), "Köln hat Höhen");
            Pruefe.Gleich(4.0, e.Gelaendeform.Roh.Hoehe(-60, 0), "Plateau");
            Pruefe.Gleich(0.0, e.Gelaendeform.Roh.Hoehe(30, 0), "am Fuß des Hochufers auf Höhe des Rheins");
            var mitte = e.Gelaendeform.Roh.Hoehe(-15, 0);
            Pruefe.Wahr(mitte > 1.8 && mitte < 2.2, $"halbe Höhe in der Mitte der Rampe: {mitte:0.00}");
            Pruefe.Wahr(e.Befunde.All(b => b.Regel is not ("hang" or "fluss-bergauf")), "kein Hang über 8 % unter Plätzen und Zonen, kein Fluss bergauf:\n  " + Liste(e.Befunde));
            Pruefe.Wahr(e.Wasserform.Laeufe.Single(l => l.Name == "Rhein").GroessterEinschnitt < Wasserform.Schluchtgrenze, "Rhein");
        }),

        ("Pinsel Heben: in der Mitte die Stärke, weiche Kante, ein Strich = ein Schritt; Rückgängig und Wiederholen; Meldungen mit Rechteck", () =>
        {
            var e = Leer();
            var meldungen = new List<Kartenaenderung>();
            e.Geaendert += meldungen.Add;
            Pruefe.Wahr(e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 20, Staerke: 3, Kante: 0.5), new[] { O(0, 0) }), "geändert");
            Pruefe.Gleich(300, Cm(e, 0, 0));
            Pruefe.Gleich(300, Cm(e, 10, 0), "innen (bis 10 m) voll");
            Pruefe.Gleich(106, Cm(e, 16, 0), "weich: (20 − 16) / 10 = 0,4 → 0,352");
            Pruefe.Gleich(0, Cm(e, 20, 0), "am Rand nichts");
            Pruefe.Gleich(1, e.Geschichte.Schritte);
            Pruefe.Gleich("Gelände gehoben", e.Geschichte.RueckgaengigText);
            Pruefe.Wahr(meldungen.Count >= 1 && meldungen.All(m => m.Teil == Kartenteil.Gelaende && m.Bereich is not null), "Meldungen mit Rechteck");
            Pruefe.Wahr(meldungen[^1].Bereich!.Value.Enthaelt(-18, 18) && !meldungen[^1].Bereich!.Value.Enthaelt(-30, 0), $"{meldungen[^1].Bereich}");
            e.Rueckgaengig();
            Pruefe.Wahr(e.Hoehen.IstLeer, "zurück: flach");
            e.Wiederholen();
            Pruefe.Gleich(300, Cm(e, 0, 0));
            // live beim Ziehen: das Raster ändert sich sofort, der Schritt kommt erst mit Beenden
            e.GelaendeStrichBeginnen(new Pinsel(Pinselart.Heben, Radius: 8, Staerke: 0.1, Kante: 0));
            e.GelaendeStrichWeiter(O(100, 0));
            Pruefe.Gleich(10, Cm(e, 100, 0));
            Pruefe.Wahr(e.StrichLaeuft && e.Geschichte.Schritte == 1, "noch kein Schritt");
            e.GelaendeStrichWeiter(O(103, 0));
            e.GelaendeStrichWeiter(O(110, 0));
            Pruefe.Wahr(e.GelaendeStrichBeenden() && !e.StrichLaeuft, "beendet");
            Pruefe.Gleich(2, e.Geschichte.Schritte);
            // Tupfer alle 2 m bei 100, 102 … 110: der Punkt 104 liegt in allen sechs (Abstand unter 8 m)
            Pruefe.Gleich(60, Cm(e, 104, 0));
            // derselbe Strich anders geteilt: dasselbe Gelände
            var f = Leer();
            f.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 8, Staerke: 0.1, Kante: 0), new[] { O(100, 0), O(110, 0) });
            for (var x = 90; x <= 120; x += 2) Pruefe.Gleich(Cm(e, x, 0), Cm(f, x, 0), $"x = {x}");
            e.Rueckgaengig();
            Pruefe.Gleich(0, Cm(e, 104, 0));
            Pruefe.Gleich(300, Cm(e, 0, 0), "der erste Strich bleibt");
        }),

        ("Pinsel Senken, Glätten, Ebnen (Höhe unter dem ersten Punkt) und Rauschen (aus dem Startwert, einmal je Strich)", () =>
        {
            var e = Leer();
            e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 5, Staerke: 2, Kante: 0), new[] { O(-50, 0) });
            Pruefe.Gleich(-200, Cm(e, -50, 0));
            // Glätten: eine Spitze wird zum Mittel ihrer 5 × 5 Nachbarn
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 1, Staerke: 5, Kante: 0), new[] { O(60, 60) });
            Pruefe.Gleich(500, Cm(e, 60, 60));
            e.GelaendePinseln(new Pinsel(Pinselart.Glaetten, Radius: 1, Staerke: 1, Kante: 0), new[] { O(60, 60) });
            Pruefe.Gleich(20, Cm(e, 60, 60));
            // Ebnen: ein Hügel, dann eben auf die Höhe unter dem ersten Punkt (3 m)
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 20, Staerke: 3, Kante: 0.5), new[] { O(0, 0) });
            e.GelaendePinseln(new Pinsel(Pinselart.Ebnen, Radius: 30, Staerke: 1, Kante: 0), new[] { O(0, 0), O(8, 0) });
            Pruefe.Gleich(300, Cm(e, 20, 0));
            Pruefe.Gleich(300, Cm(e, 36, 0), "Tupfer bei 0 und 7,5 (Radius / 4): 36 liegt im zweiten");
            Pruefe.Gleich(0, Cm(e, 38, 0), "draußen bleibt es");
            e.GelaendePinseln(new Pinsel(Pinselart.Ebnen, Radius: 10, Staerke: 0.5, Kante: 0), new[] { O(-50, 0) });
            Pruefe.Gleich(-200, Cm(e, -50, 0), "Ziel ist die Höhe unter dem ersten Punkt");
            Pruefe.Gleich(-100, Cm(e, -56, 0), "halb dorthin");
            // Rauschen: einmal je Punkt und Strich, aus dem Startwert
            var r = new Pinsel(Pinselart.Rauschen, Radius: 50, Staerke: 2, Kante: 0, Startwert: 7, Massstab: 20);
            var a = Leer();
            a.GelaendePinseln(r, new[] { O(0, 0), O(30, 0) });
            var erwartet = (int)Math.Round(Gelaendepinsel.Rauschwert(7, 10, 4, 20) * 200, MidpointRounding.AwayFromZero);
            Pruefe.Gleich(erwartet, Cm(a, 10, 4), "drei Tupfer, einmal gerechnet");
            var b = Leer();
            b.GelaendePinseln(r, new[] { O(0, 0), O(30, 0) });
            var c = Leer();
            c.GelaendePinseln(r with { Startwert = 8 }, new[] { O(0, 0), O(30, 0) });
            Pruefe.Gleich(a.ZuJson(), b.ZuJson(), "gleicher Startwert, gleiches Gelände");
            Pruefe.Wahr(a.ZuJson() != c.ZuJson(), "anderer Startwert, anderes Gelände");
            Pruefe.Wahr(Enumerable.Range(-20, 40).Select(i => Cm(a, i * 2, 0)).Distinct().Count() > 10, "es ist uneben");
        }),

        ("Begrenzung: −20 … +60 m; außerhalb der Karte (Spielfeld und Rand) ändert der Pinsel nichts", () =>
        {
            var e = Leer();
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 5, Staerke: 100, Kante: 0), new[] { O(0, 0) });
            Pruefe.Gleich(6000, Cm(e, 0, 0));
            e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 5, Staerke: 70, Kante: 0), new[] { O(0, 0) });
            e.GelaendePinseln(new Pinsel(Pinselart.Senken, Radius: 5, Staerke: 70, Kante: 0), new[] { O(0, 0) });
            Pruefe.Gleich(-2000, Cm(e, 0, 0));
            // die Karte reicht bis x = 500 (Rasterpunkt 250); der Pinsel bei 510 trifft 500, nicht 502
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 20, Staerke: 1, Kante: 0), new[] { O(510, 0) });
            Pruefe.Gleich(100, Cm(e, 500, 0));
            Pruefe.Gleich(0, Cm(e, 502, 0));
            Pruefe.Gleich(false, e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 5, Staerke: 1), new[] { O(900, 0) }), "ganz draußen: kein Schritt");
        }),

        ("Rampe: gleichmäßiges Gefälle zwischen den Höhen der beiden Punkte, weiche Seiten, davor und dahinter unverändert; Rückgängig", () =>
        {
            var e = Leer();
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 1, Staerke: 4, Kante: 0), new[] { O(40, 0) });
            Pruefe.Wahr(e.Rampe(O(0, 0), O(40, 0), breite: 10, kante: 4), "angelegt");
            Pruefe.Gleich(200, Cm(e, 20, 0));
            Pruefe.Gleich(100, Cm(e, 10, 4), "im Band (halbe Breite 5 m)");
            Pruefe.Gleich(169, Cm(e, 20, 6), "Seite: (9 − 6) / 4 = 0,75 → 0,84");
            Pruefe.Gleich(0, Cm(e, 20, 10), "daneben");
            Pruefe.Gleich(0, Cm(e, -2, 0), "davor");
            Pruefe.Gleich(400, Cm(e, 40, 0));
            Pruefe.Gleich("Rampe angelegt", e.Geschichte.RueckgaengigText);
            e.Rueckgaengig();
            Pruefe.Gleich(0, Cm(e, 20, 0));
        }),

        ("Größe und Rand ändern: Höhen bleiben an ihrem Ort; was außerhalb der Karte liegt, wird nicht geschrieben – Rückgängig bringt es wieder", () =>
        {
            var e = Leer();
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 10, Staerke: 2, Kante: 0), new[] { O(470, 0) });
            Pruefe.Wahr(e.ZuJson().Contains("\"hoehen\""), "im Rand");
            e.RandbreiteSetzen(100);   // die Karte reicht jetzt bis x = 300
            Pruefe.Wahr(!e.ZuJson().Contains("\"hoehen\""), "außerhalb: nicht geschrieben");
            Pruefe.Gleich(200, Cm(e, 470, 0), "aber noch da");
            e.Rueckgaengig();
            Pruefe.Wahr(e.ZuJson().Contains("\"hoehen\""), "wieder in der Karte");
            e.GroesseSetzen(800, 300, O(100, 0));
            Pruefe.Gleich(200, Cm(e, 470, 0), "am selben Ort");
            var json = e.ZuJson();
            Pruefe.Gleich(200, Kartenentwurf.AusJson(json, Katalog.Value).Hoehen.Wert(235, 0));
        }),

        ("Fundament: auf der höchsten Ecke, Sockel bis zur tiefsten; das Gelände bleibt; flach: 0", () =>
        {
            var e = Leer();
            var g = e.Setzen("werkstattplatz", 0, 0);   // 16 × 14 m, x −8 … 8
            Pruefe.Gleich(new Fundament(0, 0), e.Fundament(g));
            Hang(e, 0.1);
            Pruefe.Gleich(80, Cm(e, 8, 0));
            var f = e.Fundament(g);
            Pruefe.Ungefaehr(0.8, f.Hoehe, 1e-9);
            Pruefe.Ungefaehr(1.6, f.Tiefe, 1e-9);
            Pruefe.Gleich(80, Cm(e, 8, 0), "das Raster bleibt");
            var karte = Stadtkarte.AusJson(e.ZuJson(), Katalog.Value);
            Pruefe.Gleich(f, karte.Fundament(karte.GebaeudeMit(g.Kennung)!), "Stadtkarte: dasselbe Fundament");
            // gedreht: die Ecken liegen anders
            e.Drehen(g.Kennung, 90);   // 14 × 16 m quer: x −7 … 7
            Pruefe.Ungefaehr(1.4, e.Fundament(g).Tiefe, 1e-9);
        }),

        ("Hangsperre: über 15 % Fehler, 8–15 % Hinweis, darunter nichts – für Plätze und Bauzonen, nicht für Kulissen; beim Ziehen", () =>
        {
            foreach (var (gefaelle, schwere) in new[] { (0.2, Schwere.Fehler), (0.1, Schwere.Hinweis), (0.05, (Schwere?)null) }.Select(x => (x.Item1, (Schwere?)x.Item2)))
            {
                var e = Leer();
                Hang(e, gefaelle);
                var w = e.Setzen("werkstattplatz", 0, 0);
                var wh = e.Setzen("wohnhausplatz", 40, 40);
                var k = e.Setzen("leerstand", -40, -40);
                var zone = e.BauzoneAnlegen(Lage.Vorstadt, new[] { O(-60, 30), O(-20, 30), O(-20, 60), O(-60, 60) }, "V1");
                var hang = e.Befunde.Where(b => b.Regel == "hang").ToList();
                if (schwere is null)
                {
                    Pruefe.Gleich("", Liste(hang), $"{gefaelle}");
                    continue;
                }
                Pruefe.Wahr(hang.All(b => b.Schwere == schwere), Liste(hang));
                Pruefe.Wahr(hang.Any(b => b.Gebaeude.Contains(w.Kennung)) && hang.Any(b => b.Gebaeude.Contains(wh.Kennung)), Liste(hang));
                Pruefe.Wahr(hang.All(b => !b.Gebaeude.Contains(k.Kennung)), "Kulisse ohne Hangsperre");
                Pruefe.Wahr(hang.Any(b => b.Andere.Contains(zone)), "Bauzone");
                Pruefe.Wahr(hang.Any(b => b.Text.Contains($"{gefaelle * 100:0} % Gefälle")), Liste(hang));
                var stelle = Kartenpruefung.PruefeStelle(e, "werkstattplatz", 20, -40, 0);
                Pruefe.Wahr(stelle.Any(b => b.Regel == "hang" && b.Schwere == schwere), "beim Ziehen: " + Liste(stelle));
            }
            // ein kleiner Buckel ändert die Ebene im Fenster kaum: eine Spitze von 20 cm auf 5 % bleibt ohne Befund
            var f = Leer();
            Hang(f, 0.05);
            f.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 1, Staerke: 0.2, Kante: 0), new[] { O(2, 2) });
            f.Setzen("werkstattplatz", 0, 0);
            Pruefe.Wahr(f.Befunde.All(b => b.Regel != "hang"), Liste(f.Befunde));
        }),

        ("Wege und Mauern: quer eingeebnet (Bett und Bankett), Profil = Boden in der Mitte, das gespeicherte Raster bleibt; Bett folgt dem Weg", () =>
        {
            var e = Leer();
            Hang(e, 0.1);
            // quer zum Hang (Nord–Süd bei x = 0): im Bett (2 m) eben auf der Höhe der Mitte, im Bankett weich zurück
            var quer = e.WegAnlegen(Wegklasse.Land, new[] { O(0, -60), O(0, 60) }, glatt: false, name: "Quer");
            var g = e.Gelaendeform;
            Pruefe.Ungefaehr(0, g.Punkthoehe(1, 0), 1e-9);    // x = 2: raw 0,2 m, im Bett 0
            Pruefe.Ungefaehr(0, g.Punkthoehe(-1, 0), 1e-9);
            Pruefe.Ungefaehr(0.4, g.Punkthoehe(2, 0), 1e-9);  // x = 4: am Ende des Banketts das Gelände
            Pruefe.Gleich(20, Cm(e, 2, 0), "das Raster bleibt");
            Pruefe.Wahr(e.Profil(quer).All(h => Math.Abs(h) < 1e-9), string.Join(", ", e.Profil(quer)));
            // entlang des Hangs (Ost–West bei z = −30): das Profil steigt mit, quer eben
            var laengs = e.WegAnlegen(Wegklasse.Land, new[] { O(-60, -30), O(60, -30) }, glatt: false, name: "Längs");
            var p = e.Profil(laengs);
            Pruefe.Gleich(2, p.Length);
            Pruefe.Ungefaehr(-5.9, p[0], 0.01);   // am Ende über 4 m einseitig gemittelt
            Pruefe.Ungefaehr(5.9, p[1], 0.01);
            Pruefe.Ungefaehr(2.0, g.Hoehe(20, -30), 1e-9);
            Pruefe.Ungefaehr(2.0, g.Hoehe(20, -28), 1e-9);  // quer eben
            // die gerade Straße hat nur zwei Punkte – ihr Band folgt trotzdem dem Gelände: die Profillinie hat alle 1 m einen Punkt
            var linie = e.Profillinie(laengs);
            Pruefe.Gleich(121, linie.Count);
            Pruefe.Wahr(linie.All(p => Math.Abs(p.Hoehe - g.Hoehe(p.Ort.X, p.Ort.Z)) < 0.01), "das Band liegt auf dem Boden (auf 1 cm; zwischen zwei Rasterpunkten bilinear)");
            Pruefe.Ungefaehr(2.0, linie.Single(p => p.Ort.X == 20).Hoehe, 1e-9);
            // verschoben: das Bett geht mit
            e.WegVerschieben(quer, 20, 0);
            Pruefe.Ungefaehr(0.2, g.Punkthoehe(1, 0), 1e-9);
            Pruefe.Ungefaehr(2.0, g.Punkthoehe(11, 0), 1e-9);  // x = 22: im Bett auf der Höhe von x = 20
            // ein Pinselstrich neben dem Weg: das Bett rechnet um den Strich neu
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 6, Staerke: 1, Kante: 0), new[] { O(20, 40) });
            Pruefe.Ungefaehr(3.0, g.Punkthoehe(10, 20), 0.05);  // die Mitte hebt sich (über 4 m gemittelt etwas weniger)
            Pruefe.Ungefaehr(g.Punkthoehe(10, 20), g.Punkthoehe(11, 20), 1e-9);  // quer eben
            // Mauer: Bett 4 m
            var m = e.MauerAnlegen(new[] { O(-80, 80), O(80, 80) }, geschlossen: false);
            Pruefe.Ungefaehr(g.Punkthoehe(30, 40), g.Punkthoehe(30, 41), 1e-9);
            Pruefe.Gleich(2, e.Profil(m).Length);
            // Stadtkarte: dieselben Höhen
            var karte = Stadtkarte.AusJson(e.ZuJson(), Katalog.Value);
            foreach (var (i, j) in new[] { (1, 0), (11, 0), (10, 20), (30, 40), (-20, -15), (5, -15) })
                Pruefe.Ungefaehr(g.Punkthoehe(i, j), karte.Gelaendeform.Punkthoehe(i, j), 1e-9);
            var ausschnitt = karte.Gelaendeform.Ausschnitt(8, 18, 6, 4);
            Pruefe.Ungefaehr(karte.Gelaendeform.Punkthoehe(10, 20), ausschnitt[2 * 6 + 2], 1e-5);
        }),

        ("Kartenskript Probestadt: Hügel im Rand bis 25 m, sanfter Hang an der östlichen Vorstadt, die Stadt in der Mauer flach, unter Plätzen unter 8 %", () =>
        {
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            var g = e.Gelaendeform;
            var a = e.Kartengrenzen.Aussen;
            var (i0, j0, i1, j1) = Hoehenraster.Punkte(a);
            var hoechste = Enumerable.Range(i0, i1 - i0 + 1).Max(i => Enumerable.Range(j0, j1 - j0 + 1).Max(j => e.Hoehen.Wert(i, j)));
            Pruefe.Wahr(hoechste is >= 2000 and <= 3000, $"Hügel: {hoechste} cm");
            for (var x = -68; x <= 68; x += 2)
                for (var z = -54; z <= 54; z += 2)
                    Pruefe.Wahr(Math.Abs(g.Hoehe(x, z)) < 0.05, $"in der Mauer flach: {g.Hoehe(x, z)} bei ({x} | {z})");
            Pruefe.Wahr(g.Hoehe(180, 20) > 2, $"die östliche Vorstadt liegt höher: {g.Hoehe(180, 20):0.00} m");
            foreach (var platz in e.Gebaeude.Where(p => Kartenpruefung.AmHangGesperrt.Contains(p.Art)))
                Pruefe.Wahr(g.Hang(platz.Ecken()).Gefaelle < Kartenpruefung.HangHinweis, platz.Kennung);
            foreach (var zone in e.Bauzonen)
                Pruefe.Wahr(g.Hang(zone.Ecken).Gefaelle < Kartenpruefung.HangHinweis, zone.Kennung);
            Pruefe.Wahr(e.ZuJson() == Text("probestadt"), "die Datei im Spiel ist die Ausgabe des Skripts");
        }),

        ("Kartenskript: gelaende und rampe schreiben die Karte (Vorschau mit Höhenlinien); ein Strich außerhalb ändert nichts; flach ohne Linien", () =>
        {
            var ordner = Path.Combine(Path.GetTempPath(), "reichsgilde-gelaende-" + Guid.NewGuid().ToString("N"));
            try
            {
                var o = new Projektordner(ordner);
                Directory.CreateDirectory(o.Daten);
                Directory.CreateDirectory(o.Karten);
                foreach (var datei in new[] { "katalog.json", "wirtschaft.json" }) File.Copy(Path.Combine(Datenordner, datei), Path.Combine(o.Daten, datei));
                File.Copy(Datenpfad.Karte("messstadt"), o.Karte("messstadt"));
                Pruefe.Wahr(!Vorschau.Zeichnen(Kartenentwurf.AusJson(Text("messstadt"), Katalog.Value)).Contains("#8C6A43"), "flach: keine Höhenlinien");
                var aus = new StringWriter();
                Pruefe.Gleich(0, Befehle.Gelaende(o, "messstadt", new Pinsel(Pinselart.Heben, 30, 2, Kante: 0.5), new[] { O(-450, 300) }, aus), aus.ToString());
                var e = Kartenentwurf.AusJson(File.ReadAllText(o.Karte("messstadt")), Katalog.Value);
                Pruefe.Gleich((short)200, e.Hoehen.Wert(-225, 150));
                Pruefe.Wahr(File.ReadAllText(Path.Combine(o.Vorschauen, "messstadt.svg")).Contains("stroke=\"#8C6A43\""), "Höhenlinien");
                Pruefe.Gleich(0, Befehle.Rampe(o, "messstadt", O(-450, 300), O(-450, 400), 20, 4, aus), aus.ToString());
                e = Kartenentwurf.AusJson(File.ReadAllText(o.Karte("messstadt")), Katalog.Value);
                Pruefe.Wahr(e.Hoehen.Wert(-225, 175) is > 0 and < 200, $"Rampe: {e.Hoehen.Wert(-225, 175)}");
                var vorher = File.ReadAllText(o.Karte("messstadt"));
                Pruefe.Gleich(0, Befehle.Gelaende(o, "messstadt", new Pinsel(Pinselart.Heben), new[] { O(5000, 0) }, aus));
                Pruefe.Wahr(aus.ToString().Contains("ändert nichts"), aus.ToString());
                Pruefe.Gleich(vorher, File.ReadAllText(o.Karte("messstadt")), "unverändert");
            }
            finally
            {
                if (Directory.Exists(ordner)) Directory.Delete(ordner, recursive: true);
            }
        }),

        ("Karrennetz: Kreuzungen und Einmündungen verbunden, kürzester Weg nach Länge, Stücke mit Anstieg in Fahrtrichtung; getrennt: keine Fahrt", () =>
        {
            var e = Leer();
            Hang(e, 0.1);
            var g = e.Gelaendeform;
            // ein Kreuz aus zwei Straßen, die sich mitten auf ihren Strecken schneiden, und eine Einmündung, die 0,6 m vor der Straße endet
            var linien = new List<IReadOnlyList<Kartenort>>
            {
                new[] { O(-60, 0), O(60, 0) },
                new[] { O(0, -50), O(0, 50) },
                new[] { O(40, 40), O(40, 0.6) },
                new[] { O(200, 200), O(250, 200) },  // ohne Verbindung
            };
            var netz = new Karrennetz(linien, g);
            var fahrt = netz.Fahrt(O(-60, 0), O(0, 50));
            Pruefe.Ungefaehr(110, fahrt.Sum(x => x.Laenge), 1e-6);
            Pruefe.Ungefaehr(5.9, fahrt.Sum(x => x.Anstieg), 0.01);   // von x = −60 (Profil am Ende gemittelt: −5,9 m) hinauf auf x = 0
            var bergauf = netz.Fahrt(O(-50, 3), O(40, 40));
            Pruefe.Ungefaehr(90 + 40, bergauf.Sum(x => x.Laenge), 0.01);  // −50 → 40 an der Straße, dann 39,4 m die Einmündung hinauf
            Pruefe.Wahr(bergauf.Sum(x => x.Anstieg) > 8, $"bergauf {bergauf.Sum(x => x.Anstieg):0.00} m");
            var zurueck = netz.Fahrt(O(40, 40), O(-50, 3));
            Pruefe.Ungefaehr(-bergauf.Sum(x => x.Anstieg), zurueck.Sum(x => x.Anstieg), 1e-9);
            Pruefe.Gleich(0, netz.Fahrt(O(-60, 0), O(220, 200)).Count, "getrennt");
            Pruefe.Ungefaehr(10, netz.Fahrt(O(-20, 1), O(-10, -1)).Sum(x => x.Laenge), 1e-9);   // an der Straße entlang
            Pruefe.Ungefaehr(0.4, netz.Fahrt(O(-20.2, 1), O(-19.8, -1)).Sum(x => x.Laenge), 1e-9); // beide am selben 1-m-Stück
        }),

        ("Steigung bremst Karren: ab 6 % je Prozentpunkt 5 % länger, höchstens dreimal so lang; bergab und eben wie bisher", () =>
        {
            var s = Daten.Value.Logistik.Steigung!;
            Pruefe.Gleich(65, Daten.Value.Version);
            Pruefe.Gleich(1.0, s.Faktor(0.06));
            Pruefe.Ungefaehr(1.2, s.Faktor(0.10), 1e-12);
            Pruefe.Gleich(3.0, s.Faktor(0.5));
            Pruefe.Gleich(1.0, s.Faktor(-0.3));
            Pruefe.Ungefaehr((100 * 1.2 + 100 * 1) / 200, s.Faktor(new[] { new Fahrtstueck(100, 10), new Fahrtstueck(100, -10) }), 1e-12);
            Pruefe.Gleich(1.0, s.Faktor(Array.Empty<Fahrtstueck>()));
        }),

        ("Fahrzeit der Karren: bergauf zum Markt länger, bergab zurück wie in der Ebene; auf flachen Wegen Länge ÷ Tempo der Karrenart", () =>
        {
            // Probestadt: der Markt auf einem Hügel (6 m, Radius 60) – die Werkstatt im Westen fährt die Hauptstraße hinauf
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, 60, 6, Kante: 1), new[] { O(0, 0) });
            var (sim, spieler) = e.Ausprobieren(Daten.Value, "Schmied");
            var w = sim.Wirtschaft!;
            var west = w.Betriebe.Single(b => b.Konto == spieler);   // der Spieler beginnt auf dem Werkstattplatz bei (−30 | −10)
            var karren = west.Karren[0];
            var jeMinute = karren.Daten.MeterJeMinute;
            var (hin, zurueck) = w.Fahrten(west)!.Value;
            var faktor = Daten.Value.Logistik.Steigung!.Faktor(hin);
            Pruefe.Wahr(faktor > 1.05, $"Faktor bergauf {faktor:0.000}");
            int Eben(IReadOnlyList<Fahrtstueck> fahrt) => (int)Math.Round(fahrt.Sum(x => x.Laenge) / jeMinute, MidpointRounding.AwayFromZero);
            Pruefe.Gleich(FahrzeitWeglaengeTests.NachFormel(w, west, karren.Typ, true), w.Fahrzeit(karren, zumMarkt: true));
            Pruefe.Wahr(w.Fahrzeit(karren, zumMarkt: true) > Eben(hin), $"bergauf länger: {w.Fahrzeit(karren, true)} > {Eben(hin)}");
            Pruefe.Gleich(Eben(zurueck), w.Fahrzeit(karren, zumMarkt: false), "bergab wie in der Ebene");
            Pruefe.Ungefaehr(-hin.Sum(x => x.Anstieg), zurueck.Sum(x => x.Anstieg), 1e-9);
            Pruefe.Gleich(w.Uhr.UhrzeitInMinuten + w.Fahrzeit(karren, true), w.AnkunftAmMarkt(karren));
            // ohne Hügel: die Werkstätten der flachen Stadt fahren Länge ÷ Tempo, obwohl die Karte Gelände hat
            var f = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            var (simF, spielerF) = f.Ausprobieren(Daten.Value, "Schmied");
            var wf = simF.Wirtschaft!;
            Pruefe.Wahr(!wf.Karte!.Gelaendeform.IstFlach, "Probestadt hat Gelände");
            foreach (var b in wf.Betriebe.Where(b => b.InDerStadt && b.Karren.Count > 0))
            {
                var (h, z) = wf.Fahrten(b)!.Value;
                Pruefe.Wahr(h.Concat(z).All(x => wf.Zeitfaktor(x) == 1), $"{b.Name}: flach und Fahrweg");
                var v = b.Karren[0].Daten.MeterJeMinute;
                int Zeit(IReadOnlyList<Fahrtstueck> fahrt) => (int)Math.Round(fahrt.Sum(x => x.Laenge) / v, MidpointRounding.AwayFromZero);
                Pruefe.Gleich((Zeit(h), Zeit(z)), (wf.Fahrzeit(b.Karren[0], true), wf.Fahrzeit(b.Karren[0], false)), b.Name);
            }
            // Messstadt (flach): die feste Fahrzeit, ohne Netz zu bauen (die Greybox hat seit PR #199 Gelände)
            var flach = Stadtkarte.AusDatei(Datenpfad.Karte("messstadt"));
            Pruefe.Wahr(flach.Gelaendeform.IstFlach, "Messstadt flach");
            // nach dem Laden dieselben Zeiten
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var west2 = geladen.Wirtschaft!.Betriebe.Single(b => b.Name == west.Name);
            Pruefe.Gleich(w.Fahrzeit(karren, true), geladen.Wirtschaft.Fahrzeit(west2.Karren[0], true), "nach dem Laden");
        }),

        ("Spielstand: Das Gelände der Karte liegt im Spielstand und kommt beim Laden genauso wieder", () =>
        {
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            Pruefe.Wahr(!e.Hoehen.IstLeer, "die Probestadt hat Gelände");
            var (sim, spieler) = e.Ausprobieren(Daten.Value, "Schmied");
            var json = Speicherung.Speichern(sim, spieler);
            var (geladen, _) = Speicherung.Laden(json);
            var a = sim.Wirtschaft!.Karte!.Hoehen!;
            var b = geladen.Wirtschaft!.Karte!.Hoehen!;
            Pruefe.Gleich(a.Kacheln.Count, b.Kacheln.Count);
            Pruefe.Wahr(a.Kacheln.All(k => b.KachelKopie(k)!.AsSpan().SequenceEqual(a.KachelKopie(k))), "alle Kacheln gleich");
        }),
    };
}
