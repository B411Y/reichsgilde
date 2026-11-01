using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Felspinsel F3: Felsstücke in der Karte (Format, Grundgestein), Streu-Pinsel, Stempel und Radierer als Befehle der Kartenwerkstatt –
/// gleicher Strich gleiche Bytes, Rückgängig Byte für Byte zurück, Filter nach Steigung und Boden, Drehen zum Hang. Alte Karten und alte
/// Spielstände bleiben Byte für Byte gleich; ein Spielstand mit Felsen lädt sie wieder.
/// </summary>
static class FelsenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Eine leere Karte 400 × 300 m mit 300 m Rand.</summary>
    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>Westlich von x = 0 eben, von x = 0 bis 100 ein Hang mit 30 % nach Osten hinauf (wie in den PinselpaketTests), für |z| &lt; 200.</summary>
    static Kartenentwurf HangMitEbene()
    {
        var e = Leer();
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 1, Staerke: 30, Kante: 0), new[] { O(100, 0) });
        e.Rampe(O(0, 0), O(100, 0), breite: 400, kante: 0);
        return e;
    }

    static List<Kartenort> Linie(Kartenort von, Kartenort bis, int schritte) =>
        Enumerable.Range(0, schritte + 1).Select(i => O(von.X + (bis.X - von.X) * i / schritte, von.Z + (bis.Z - von.Z) * i / schritte)).ToList();

    static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>
    /// Ein fester Ablauf mit allen Befehlen (Stempel der drei Gruppen, Streuen mit Filter, Radieren, Rückgängig/Wiederholen, Grundgestein) –
    /// sein Ergebnis als Kartendatei. Für die Gleichheit zwischen Linux und Windows (GleichheitTests) und die Ländereinstellung.
    /// </summary>
    internal static string Ablauf()
    {
        var e = HangMitEbene();
        e.GrundgesteinSetzen("trachyt");
        e.FelsStempeln(new Felspinsel(Gruppe: "wand"), O(40, -60));
        e.FelsStempeln(new Felspinsel(Gruppe: "kuppe", Gestein: "buntsandstein", Groesse: 1.2), O(-60, 40));
        e.FelsStempeln(new Felspinsel(Gruppe: "rippe", Neigung: 0.8), O(70, 60), drehung: 33.3);
        e.FelsStreuen(new Felspinsel(Formen: new[] { "block", "platte", "schuttbrocken" }, Radius: 12, Dichte: 0.3, Filter: new Pinselfilter(SteigungAb: 10)),
            Linie(O(-30, 0), O(60, 10), 30));
        e.FelsStreuen(new Felspinsel(Formen: new[] { "felsnase", "ueberhang" }, Radius: 15, Dichte: 0.15, Gestein: "kalkstein"), Linie(O(20, 100), O(90, 120), 10), startwert: 77);
        if (!e.FelsStreuen(Felspinsel.Radiergummi(6), Linie(O(30, 0), O(40, 5), 4))) throw new Exception("Ablauf: der Radierer nimmt nichts weg – Ablauf anpassen");
        e.Rueckgaengig();
        e.Wiederholen();
        return e.ZuJson();
    }

    static T MitKultur<T>(string kultur, Func<T> lauf)
    {
        var (vorher, vorherUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(kultur);
            return lauf();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (vorher, vorherUi);
        }
    }

    static string AlterSpielstand()
    {
        var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Datenordner))!, "tests", "Reichsgilde.Core.Tests", "Spielstaende",
            "format2-greybox-herbst1406.json.gz");
        using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
        return new StreamReader(gz).ReadToEnd();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Felsen: Formen und Gruppen wie aus F1 (8 Formen, 3 Gruppen mit 3–8 Stücken), Gesteinsarten wie in gestein.json", () =>
        {
            Pruefe.Gleich(8, Felsformen.Alle.Count);
            Pruefe.Gleich(8, Felsformen.Alle.Select(f => f.Kennung).Distinct().Count());
            Pruefe.Gleich("Wand,Kuppe,Rippe,Block,Platte,Ueberhang,Schuttbrocken,Felsnase", string.Join(",", Felsformen.Alle.Select(f => f.Knoten)));
            foreach (var g in Felsformen.Gruppen)
            {
                Pruefe.Wahr(g.Stuecke.Count is >= 3 and <= 8, $"Gruppe {g.Kennung} hat {g.Stuecke.Count} Stücke");
                Pruefe.Wahr(g.Stuecke.All(s => Felsformen.Finde(s.Form) is not null), $"Gruppe {g.Kennung}: unbekannte Form");
            }
            Pruefe.Gleich("wand,kuppe,rippe", string.Join(",", Felsformen.Gruppen.Select(g => g.Kennung)));
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Datenordner, "gestein.json")));
            var kennungen = doc.RootElement.GetProperty("gesteine").EnumerateArray().Select(g => g.GetProperty("kennung").GetString()).ToList();
            Pruefe.Gleich(string.Join(",", kennungen), string.Join(",", Felsformen.Gesteine));
            Pruefe.Wahr(Felsformen.IstGestein(Felsformen.GrundgesteinVorgabe), "Die Vorgabe ist eine Gesteinsart");
        }),

        ("Felsen: Alte Karten ohne Felsen öffnen und schreiben ergibt Byte für Byte dieselbe Datei, auch als Stadtkarte ohne Feld Felsen", () =>
        {
            foreach (var kennung in new[] { "greybox", "koeln", "messstadt", "probestadt" })
            {
                var text = File.ReadAllText(Datenpfad.Karte(kennung));
                var e = Kartenentwurf.AusJson(text, Katalog.Value);
                Pruefe.Gleich(0, e.Felsen.Count, kennung);
                Pruefe.Gleich(null, e.Grundgestein.Gestein, kennung);
                Pruefe.Gleich(text, e.ZuJson(), $"{kennung}: Byte für Byte");
                Pruefe.Wahr(!text.Contains("\"felsen\""), $"{kennung} hat schon Felsen – Test anpassen");
                Pruefe.Gleich(null, Stadtkarte.AusJson(text, Katalog.Value).Felsen, kennung);
            }
        }),

        ("Felsen: Stücke und Grundgestein in der Datei – lesen, schreiben, wieder lesen ergibt dieselben Bytes; die Stadtkarte liest sie", () =>
        {
            var e = Leer();
            var neue = e.FelsStempeln(new Felspinsel(Gruppe: "wand", Gestein: "kalkstein"), O(10, 20));
            e.GrundgesteinSetzen("buntsandstein");
            var text = e.ZuJson();
            Pruefe.Wahr(text.Contains("\"felsen\": {") && text.Contains("\"grundgestein\": \"buntsandstein\"") && text.Contains("\"form\": \"wand\""),
                "felsen mit Grundgestein und Stücken in der Datei:\n" + text);
            var zwei = Kartenentwurf.AusJson(text, Katalog.Value);
            Pruefe.Gleich(neue.Count, zwei.Felsen.Count);
            Pruefe.Gleich(text, zwei.ZuJson(), "wieder geschrieben");
            var karte = Stadtkarte.AusJson(text, Katalog.Value);
            Pruefe.Gleich("buntsandstein", karte.Felsen!.Grundgestein);
            Pruefe.Gleich(neue.Count, karte.Felsen.Stuecke.Count);
            var (a, b) = (neue[1], karte.Felsen.Stuecke[1]);
            Pruefe.Gleich(a.Form, b.Form);
            Pruefe.Gleich("kalkstein", b.Gestein);
            Pruefe.Gleich(a.X, b.Punkt[0]);
            Pruefe.Gleich(a.Drehung, b.Drehung);
            Pruefe.Gleich(a.Massstab, b.Massstab);
            Pruefe.Gleich(a.Einsinken, b.Einsinken);
            Pruefe.Gleich(a.KippenZ, b.Kippen![1]);
            Pruefe.Gleich("kalkstein", zwei.GesteinVon(zwei.Felsen[0]));

            // ein Stück ohne gestein und ohne kippen: beides fehlt in der Datei, das Grundgestein gilt
            var ohne = System.Text.RegularExpressions.Regex.Replace(text.Replace("\"gestein\": \"kalkstein\", ", ""), @", ""kippen"": \[[^\]]*\]", "");
            var f = Kartenentwurf.AusJson(ohne, Katalog.Value);
            Pruefe.Gleich("buntsandstein", f.GesteinVon(f.Felsen[0]));
            Pruefe.Wahr(f.Felsen.All(x => x.KippenX == 0 && x.KippenZ == 0), "ohne kippen gerade");
            Pruefe.Wahr(!f.ZuJson().Contains("\"gestein\"") && !f.ZuJson().Contains("\"kippen\""), "ohne Abweichung kein gestein, ohne Kippen kein kippen");
            Pruefe.Gleich(ohne, f.ZuJson(), "so wieder geschrieben");

            // ein Pinsel mit dem Grundgestein schreibt kein gestein (es folgt dem Grundgestein)
            var gleich = Leer();
            gleich.GrundgesteinSetzen("trachyt");
            gleich.FelsStempeln(new Felspinsel(Gruppe: "kuppe", Gestein: "trachyt"), O(0, 0));
            Pruefe.Wahr(gleich.Felsen.All(x => x.Gestein is null), "Gestein gleich Grundgestein fällt weg");
            gleich.GrundgesteinSetzen("kalkstein");
            Pruefe.Gleich("kalkstein", gleich.GesteinVon(gleich.Felsen[0]));

            // unbekannte Felder bleiben, unbekannte Form und Gesteinsart sind nur Hinweise (Felsen sperren nichts)
            var fremd = text.Replace("\"grundgestein\": \"buntsandstein\"", "\"grundgestein\": \"marmor\", \"notiz\": 1").Replace("\"form\": \"wand\"", "\"form\": \"zinne\"");
            var g = Kartenentwurf.AusJson(fremd, Katalog.Value);
            var einmal = g.ZuJson();
            Pruefe.Wahr(einmal.Contains("\"notiz\": 1") && einmal.Contains("\"form\": \"zinne\"") && einmal.Contains("\"marmor\""), "unbekannte Felder bleiben");
            Pruefe.Gleich(einmal, Kartenentwurf.AusJson(einmal, Katalog.Value).ZuJson(), "und bleiben beim nächsten Schreiben gleich");
            var fels = g.Befunde.Where(x => x.Regel == "fels").ToList();
            Pruefe.Wahr(fels.Count >= 2 && fels.All(x => x.Schwere == Schwere.Hinweis), "nur Hinweise: " + string.Join(" | ", fels));

            // Werte außerhalb der Grenzen: ein Hinweis; ein leerer Eintrag oder ein Punkt ohne zwei Zahlen: Fehler der Stadtkarte (nicht zu zeichnen)
            var grenzen = Kartenentwurf.AusJson(text.Replace("\"massstab\": " + Kartenschreiber.Zahl(neue[0].Massstab), "\"massstab\": -3"), Katalog.Value);
            Pruefe.Wahr(grenzen.Befunde.Any(x => x.Regel == "fels" && x.Text.Contains("außerhalb der Grenzen") && x.Schwere == Schwere.Hinweis), "Hinweis bei Maßstab −3");
            Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson(text.Replace("\"stuecke\": [", "\"stuecke\": [null, "), Katalog.Value), "felsen.stuecke");
        }),

        ("Felsen: Stempel einer Wand setzt die ganze Gruppe genau wie die Vorschau, ein Schritt; Rückgängig und Wiederholen Byte für Byte", () =>
        {
            var e = Leer();
            var leer = e.ZuJson();
            var pinsel = new Felspinsel(Gruppe: "wand", Groesse: 1.1);
            var vorschau = e.FelsStempelVorschau(pinsel, O(5, 5));
            Pruefe.Gleich(e.Felsen.Count, 0);
            var neue = e.FelsStempeln(pinsel, O(5, 5));
            Pruefe.Gleich(Felsformen.Gruppe("wand")!.Stuecke.Count, neue.Count);
            Pruefe.Gleich(vorschau.Count, neue.Count);
            for (var i = 0; i < neue.Count; i++)
            {
                var (v, n) = (vorschau[i], neue[i]);
                Pruefe.Wahr(v.Form == n.Form && v.X == n.X && v.Z == n.Z && v.Drehung == n.Drehung && v.Massstab == n.Massstab && v.Einsinken == n.Einsinken
                            && v.KippenX == n.KippenX && v.KippenZ == n.KippenZ, $"Stück {i}: Vorschau und Stempel gleich");
                Pruefe.Wahr(Math.Abs(n.KippenX) <= Felsformen.KippenMax && Math.Abs(n.KippenZ) <= Felsformen.KippenMax, "Kippen höchstens ±8°");
                Pruefe.Wahr(n.Einsinken is >= 0 and <= 0.9, "Einsinken in den Grenzen");
            }
            Pruefe.Gleich(1, e.Geschichte.Schritte);
            Pruefe.Gleich("Gruppe Felswand gestempelt", e.Geschichte.RueckgaengigText);
            var mit = e.ZuJson();
            e.Rueckgaengig();
            Pruefe.Gleich(leer, e.ZuJson(), "zurückgenommen");
            e.Wiederholen();
            Pruefe.Gleich(mit, e.ZuJson(), "wiederholt");
            // Die Hauptwand steht in der Mitte, die Seitenwände links und rechts davon (Abstand 6,6 · 1,1 ± Streuung)
            Pruefe.Gleich(5.0, neue[0].X);
            Pruefe.Wahr(neue[1].Punkt.Abstand(neue[0].Punkt) is > 4 and < 11, $"Seitenwand {neue[1].Punkt} weit genug weg");
        }),

        ("Felsen: Gleicher Strich und Startwert – gleiche Bytes; anderer Startwert – andere Stücke; die Dichte begrenzt, der Abstand hält", () =>
        {
            Kartenentwurf Streuen(int? startwert)
            {
                var e = Leer();
                e.FelsStreuen(new Felspinsel(Formen: new[] { "block", "kuppe" }, Radius: 10, Dichte: 0.25), Linie(O(-50, 0), O(50, 0), 20), startwert);
                return e;
            }
            var (a, b, c) = (Streuen(5), Streuen(5), Streuen(6));
            Pruefe.Gleich(a.ZuJson(), b.ZuJson(), "gleicher Strich, gleiche Bytes");
            Pruefe.Wahr(a.ZuJson() != c.ZuJson(), "anderer Startwert, andere Stücke");
            Pruefe.Gleich(Streuen(null).ZuJson(), Streuen(null).ZuJson(), "ohne Startwert: einer aus dem ersten Punkt");
            Pruefe.Wahr(a.Felsen.Count >= 10, $"genug Stücke gestreut: {a.Felsen.Count}");
            Pruefe.Gleich(1, a.Geschichte.Schritte);
            Pruefe.Wahr(a.Felsen.All(f => Math.Abs(f.Z) <= 10 + 1e-9 && f.X >= -60 && f.X <= 60), "alle im Band des Strichs");
            for (var i = 0; i < a.Felsen.Count; i++)
                for (var j = i + 1; j < a.Felsen.Count; j++)
                {
                    var (f, g) = (a.Felsen[i], a.Felsen[j]);
                    Pruefe.Wahr(f.Punkt.Abstand(g.Punkt) >= (1 - Felspinsel.Ueberlappung) * (f.Radius + g.Radius) - 1e-6, $"Abstand {f.Punkt} – {g.Punkt}");
                }
            // gedeckte Fläche um die Mitte: höchstens etwa die Dichte (ein Stück darf die Grenze überschreiten)
            var mitte = a.Felsen.Where(f => f.Punkt.Abstand(O(0, 0)) <= 10).Sum(f => Math.PI * f.Radius * f.Radius) / (Math.PI * 100);
            Pruefe.Wahr(mitte <= 0.25 + 0.25, $"Dichte um die Mitte {mitte:0.00}");
            // ein zweiter Strich über dieselbe Stelle setzt kaum noch etwas (der Kreis ist voll)
            var vorher = a.Felsen.Count;
            a.FelsStreuen(new Felspinsel(Formen: new[] { "block", "kuppe" }, Radius: 10, Dichte: 0.25), Linie(O(-50, 0), O(50, 0), 20), 9);
            Pruefe.Wahr(a.Felsen.Count - vorher <= vorher / 3, $"zweiter Strich: {a.Felsen.Count - vorher} neue zu {vorher}");
        }),

        ("Felsen: Die Dichte gilt bei kleinem wie bei großem Radius (Deckung um die Mitte des Strichs zwischen 0,5- und 1,6-mal der Dichte)", () =>
        {
            foreach (var radius in new[] { 5.0, 20, 50 })
            {
                var e = Kartenentwurf.Neu("probe", "Probe", 1000, 1000, Katalog.Value);
                e.FelsStreuen(new Felspinsel(Formen: new[] { "block" }, Radius: radius, Dichte: 0.3), Linie(O(-4 * radius, 0), O(4 * radius, 0), 40), 11);
                var deckung = e.Felsen.Where(f => f.Punkt.Abstand(O(0, 0)) <= radius).Sum(f => Math.PI * f.Radius * f.Radius) / (Math.PI * radius * radius);
                Pruefe.Wahr(deckung >= 0.15 && deckung <= 0.48, $"Radius {radius}: Deckung {deckung:0.00} bei Dichte 0,3");
            }
        }),

        ("Felsen: Radierer nimmt die Stücke im Kreis weg (ein Schritt), Rückgängig setzt jedes an seine Stelle zurück", () =>
        {
            var e = Leer();
            e.FelsStreuen(new Felspinsel(Radius: 20, Dichte: 0.4), Linie(O(-40, 0), O(40, 0), 10), 3);
            var voll = e.ZuJson();
            var vorher = e.Felsen.Count;
            Pruefe.Wahr(e.FelsStreuen(Felspinsel.Radiergummi(8), new[] { O(0, 0) }), "radiert");
            Pruefe.Wahr(e.Felsen.All(f => f.Punkt.Abstand(O(0, 0)) > 8), "keins mehr im Kreis");
            Pruefe.Wahr(e.Felsen.Count < vorher, "es fehlen Stücke");
            Pruefe.Wahr(e.Geschichte.RueckgaengigText!.EndsWith("Felsstücke radiert"), e.Geschichte.RueckgaengigText);
            e.Rueckgaengig();
            Pruefe.Gleich(voll, e.ZuJson(), "Rückgängig: dieselbe Datei");
            Pruefe.Wahr(!e.FelsStreuen(Felspinsel.Radiergummi(3), new[] { O(300, 0) }), "wo nichts liegt, kein Schritt");

            // ein langer Radierstrich mit vielen Tupfern: Rückgängig und Wiederholen zweimal Byte für Byte
            Pruefe.Wahr(e.FelsStreuen(Felspinsel.Radiergummi(5), Linie(O(-45, -3), O(45, 3), 30)), "langer Radierstrich");
            var radiert = e.ZuJson();
            Pruefe.Wahr(e.Felsen.Count < vorher - 5, "viele weg");
            for (var i = 0; i < 2; i++)
            {
                e.Rueckgaengig();
                Pruefe.Gleich(voll, e.ZuJson(), $"Rückgängig {i + 1}");
                e.Wiederholen();
                Pruefe.Gleich(radiert, e.ZuJson(), $"Wiederholen {i + 1}");
            }
        }),

        ("Felsen: Filter nach Steigung und Boden gilt für Streuen, Stempel und Radierer", () =>
        {
            var e = HangMitEbene();
            var pinsel = new Felspinsel(Radius: 30, Dichte: 0.3, Filter: new Pinselfilter(SteigungAb: 20));
            e.FelsStreuen(pinsel, Linie(O(-20, 0), O(60, 0), 16), 4);
            Pruefe.Wahr(e.Felsen.Count > 5, $"am Hang gestreut: {e.Felsen.Count}");
            Pruefe.Wahr(e.Felsen.All(f => Pinselfilter.Steigung(e.Hoehen, f.X, f.Z) >= 18), "nur am Hang (ab 18 %, weicher Rand)");
            Pruefe.Gleich(0, e.FelsStempeln(pinsel, O(-20, 0)).Count, "Stempel in der Ebene: nichts");
            Pruefe.Gleich(1, e.FelsStempeln(pinsel with { Filter = new Pinselfilter(SteigungBis: 5) }, O(-20, 0)).Count, "Stempel in der Ebene mit Filter bis 5 %");
            var amHang = e.Felsen.Count - 1;
            e.FelsStreuen(Felspinsel.Radiergummi(100, new Pinselfilter(SteigungBis: 5)), new[] { O(0, 0) });
            Pruefe.Gleich(amHang, e.Felsen.Count, "der Radierer mit Filter nimmt nur das Stück in der Ebene");

            var b = Leer();
            b.BodenMalen(new Bodenpinsel(Bodentextur.Sand, 10, Staerke: 1, Kante: 0, Deko: false), new[] { O(0, 0) });
            b.FelsStreuen(new Felspinsel(Formen: new[] { "schuttbrocken" }, Radius: 25, Dichte: 0.3, Filter: new Pinselfilter(Boden: Bodentextur.Sand)),
                Linie(O(-15, -5), O(15, 5), 15), 8);
            Pruefe.Wahr(b.Felsen.Count > 3, $"auf Sand gestreut: {b.Felsen.Count}");
            Pruefe.Wahr(b.Felsen.All(f => f.Punkt.Abstand(O(0, 0)) <= 10.5), "nur auf dem Sand");
        }),

        ("Felsen: Zum Hang zeigt die Schauseite einer Wand hangabwärts (±20°), ohne Hang oder bei einem Block zufällig", () =>
        {
            var e = HangMitEbene();
            // Hang steigt nach Osten (+x): hangabwärts ist −x, die Drehung mit Schauseite (+Z) nach −x ist 270°
            Pruefe.Ungefaehr(270, Kartenraster.Drehung(Felsstreuung.Hangabwaerts(e.Hoehen, 50, 0)!.Value), 1e-9);
            Pruefe.Gleich(null, Felsstreuung.Hangabwaerts(e.Hoehen, -50, 0));
            var dreh = new List<double>();
            for (var i = 0; i < 12; i++)
            {
                var w = e.FelsStempeln(new Felspinsel(Gruppe: "wand"), O(30 + 3 * i, -150 + 25 * i))[0].Drehung;
                Pruefe.Wahr(Math.Abs(w - 270) <= 20 + 1e-9, $"Wand {i}: Drehung {w}");
                dreh.Add(w);
            }
            Pruefe.Wahr(dreh.Distinct().Count() > 3, "die Wände streuen um die Richtung");
            Pruefe.Gleich(12.5, e.FelsStempeln(new Felspinsel(Gruppe: "wand"), O(50, 150), drehung: 12.5)[0].Drehung, "feste Drehung");
            var bloecke = Enumerable.Range(0, 20).Select(i => e.FelsStempeln(new Felspinsel(Formen: new[] { "block" }), O(-100 + 4 * i, 100))[0].Drehung).ToList();
            Pruefe.Wahr(bloecke.Any(w => w < 180) && bloecke.Any(w => w >= 180), "Blöcke voll zufällig gedreht");
        }),

        ("Felsen: Ein laufender Strich zeigt seine Stücke sofort, wird erst mit Beenden ein Schritt; Rückgängig beendet ihn zuerst", () =>
        {
            var e = Leer();
            var meldungen = new List<Kartenaenderung>();
            e.Geaendert += meldungen.Add;
            e.FelsStrichBeginnen(new Felspinsel(Radius: 10, Dichte: 0.3), 2);
            var n = e.FelsStrichWeiter(O(0, 0));
            Pruefe.Wahr(n > 0 && e.Felsen.Count == n, $"sofort da: {n}");
            Pruefe.Gleich(n, meldungen.Count(m => m.Teil == Kartenteil.Fels && m.Art == Aenderungsart.Gesetzt));
            Pruefe.Gleich(0, e.Geschichte.Schritte, "noch kein Schritt");
            e.FelsStrichWeiter(O(30, 0));
            Pruefe.Wahr(e.Rueckgaengig(), "Rückgängig beendet den Strich und nimmt ihn zurück");
            Pruefe.Gleich(0, e.Felsen.Count);
            Pruefe.Wahr(!e.FelsStrichLaeuft, "kein Strich mehr");
            Pruefe.Gleich(0, e.FelsStrichWeiter(O(40, 0)), "Weiter ohne Strich tut nichts");
            Pruefe.Gleich(0, e.Felsen.Count);
            Pruefe.Wahr(e.Wiederholen() && e.Felsen.Count > n, "wiederholt");
            // Rückgängig direkt über die Geschichte mitten in einem Radierstrich: der Strich wird zuerst ein Schritt
            var stand = e.ZuJson();
            e.FelsStrichBeginnen(Felspinsel.Radiergummi(50), 1);
            e.FelsStrichWeiter(O(0, 0));
            Pruefe.Wahr(e.Geschichte.Rueckgaengig() && !e.FelsStrichLaeuft, "Geschichte.Rueckgaengig beendet den Strich");
            Pruefe.Gleich(stand, e.ZuJson(), "nur der Radierstrich ist zurückgenommen");
            e.GrundgesteinSetzen("kalkstein");
            Pruefe.Gleich("Grundgestein geändert", e.Geschichte.RueckgaengigText);
            Pruefe.Wahr(meldungen.Any(m => m.Teil == Kartenteil.Grundgestein), "Grundgestein gemeldet");
            e.Rueckgaengig();
            Pruefe.Gleich(null, e.Grundgestein.Gestein);
            Pruefe.WirftMitMeldung(() => e.GrundgesteinSetzen("marmor"), "marmor");
            e.FelsenLoeschen(e.Felsen.Take(2).ToList());
            Pruefe.Gleich("2 Felsstücke gelöscht", e.Geschichte.RueckgaengigText);
        }),

        ("Felsen: Der feste Ablauf gibt mit deutscher Ländereinstellung dieselben Bytes; Werte gerundet wie in der Datei", () =>
        {
            var a = Ablauf();
            Pruefe.Gleich(Sha(a), Sha(MitKultur("de-DE", Ablauf)), "de-DE");
            var e = Kartenentwurf.AusJson(a, Katalog.Value);
            Pruefe.Wahr(e.Felsen.Count > 20, $"Stücke im Ablauf: {e.Felsen.Count}");
            Pruefe.Wahr(e.Felsen.All(f => f.X == Kartenraster.Punkt(f.X) && f.Z == Kartenraster.Punkt(f.Z) && f.Drehung == Kartenraster.Drehung(f.Drehung)
                                          && f.Massstab == Math.Round(f.Massstab, 2) && f.KippenX == Math.Round(f.KippenX, 1)), "gerundet");
            Pruefe.Wahr(e.Felsen.Any(f => f.Gestein == "kalkstein") && e.Felsen.Any(f => f.Gestein is null), "abweichende und Grund-Gesteine");
            Pruefe.Gleich("trachyt", e.Grundgestein.Gestein);
        }),

        ("Felsen: Alter Spielstand lädt und schreibt ohne Felsen; ein Spielstand mit Felsen lädt sie wieder, Byte für Byte", () =>
        {
            var (alt, altSpieler) = Speicherung.Laden(AlterSpielstand());
            var zeit = new DateTime(2026, 11, 1, 12, 0, 0);
            var json = Speicherung.Speichern(alt, altSpieler, zeit);
            Pruefe.Wahr(!json.Contains("\"Felsen\""), "kein Feld Felsen im alten Stand");
            Pruefe.Gleich(null, alt.Wirtschaft!.Karte!.Felsen);

            var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
            var ohne = Stadtkarte.AusDatei(Datenpfad.Karte("greybox"));
            var (sim0, sp0) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 3 }, daten, new NeuePartie(), karte: ohne);
            Pruefe.Wahr(!Speicherung.Speichern(sim0, sp0, zeit).Contains("\"Felsen\""), "neue Partie ohne Felsen: kein Feld");

            var e = Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte("greybox")), Katalog.Value);
            e.FelsStempeln(new Felspinsel(Gruppe: "kuppe", Gestein: "trachyt"), O(150, 150));
            e.GrundgesteinSetzen("buntsandstein");
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 3 },
                Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()), new NeuePartie(), karte: e.ZuStadtkarte());
            var stand = Speicherung.Speichern(sim, spieler, zeit);
            Pruefe.Wahr(stand.Contains("\"Felsen\""), "Felsen im Spielstand");
            var (geladen, gs) = Speicherung.Laden(stand);
            Pruefe.Gleich(5, geladen.Wirtschaft!.Karte!.Felsen!.Stuecke.Count);
            Pruefe.Gleich("buntsandstein", geladen.Wirtschaft.Karte.Felsen.Grundgestein);
            Pruefe.Gleich("trachyt", geladen.Wirtschaft.Karte.Felsen.Stuecke[0].Gestein);
            Pruefe.Gleich(stand, Speicherung.Speichern(geladen, gs, zeit), "wieder gespeichert");
        }),
    };
}
