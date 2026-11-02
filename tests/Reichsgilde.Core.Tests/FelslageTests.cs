using System.Security.Cryptography;
using System.Text;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Fels-Plan R2: volle Drehung (Kippen und Rollen −180 … 180°), Größe je Achse, Höhenversatz; der Befehl FelsTransformieren (absolut und
/// relativ, mehrere Stücke, ein Schritt), Einrasten am Boden, Neu würfeln, die Zufallsbereiche des Felspinsels und die Abfragen für den Gizmo
/// (Felsraum). Alte Karten und alte Striche bleiben Byte für Byte gleich.
/// </summary>
static class FelslageTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>Westlich von x = 0 eben, von x = 0 bis 100 ein Hang mit 30 % nach Osten hinauf (wie in den FelsenTests).</summary>
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

    static void Nahe(double erwartet, double ist, double toleranz, string text) =>
        Pruefe.Wahr(Math.Abs(erwartet - ist) <= toleranz, $"{text}: erwartet {erwartet}, ist {ist}");

    static void BasisNahe(Felsbasis a, Felsbasis b, double toleranz, string text)
    {
        var (x, y) = (new[] { a.M00, a.M01, a.M02, a.M10, a.M11, a.M12, a.M20, a.M21, a.M22 }, new[] { b.M00, b.M01, b.M02, b.M10, b.M11, b.M12, b.M20, b.M21, b.M22 });
        for (var i = 0; i < 9; i++) Nahe(x[i], y[i], toleranz, $"{text} (Feld {i})");
    }

    /// <summary>
    /// Ein Ablauf nur mit Pinseln, wie es sie vor R2 gab (Stempel, Streuen, Radieren, Rückgängig), mit oder ohne Boden. Seine Prüfsummen stammen
    /// aus dem Stand vor R2 (phase5/felsbild 711cfe9b): Ein alter Strich mit gleichem Startwert ergibt dieselben Stücke (und denselben Boden).
    /// </summary>
    static string AlterAblauf(bool boden = false)
    {
        var e = HangMitEbene();
        e.GrundgesteinSetzen("trachyt");
        e.FelsStempeln(new Felspinsel(Gruppe: "wand", Boden: boden), O(40, -60));
        e.FelsStempeln(new Felspinsel(Gruppe: "kuppe", Gestein: "buntsandstein", Groesse: 1.2, Boden: boden), O(-60, 40));
        e.FelsStempeln(new Felspinsel(Gruppe: "rippe", Neigung: 0.8, Boden: boden), O(70, 60), drehung: 33.3);
        e.FelsStreuen(new Felspinsel(Formen: new[] { "block", "platte", "schuttbrocken" }, Radius: 12, Dichte: 0.3, Filter: new Pinselfilter(SteigungAb: 10), Boden: boden),
            Linie(O(-30, 0), O(60, 10), 30));
        e.FelsStreuen(new Felspinsel(Formen: new[] { "felsnase", "ueberhang" }, Radius: 15, Dichte: 0.15, Gestein: "kalkstein", Boden: boden), Linie(O(20, 100), O(90, 120), 10), startwert: 77);
        e.FelsStreuen(Felspinsel.Radiergummi(6) with { Boden = boden }, Linie(O(30, 0), O(40, 5), 4));
        e.Rueckgaengig();
        e.Wiederholen();
        e.FelsStreuen(new Felspinsel(Formen: new[] { "wand", "kuppe", "rippe", "block" }, Radius: 30, Dichte: 0.4, Groesse: 1.5, Streuung: 0.5, Boden: boden),
            Linie(O(-150, -100), O(150, 100), 40), startwert: 5);
        return e.ZuJson();
    }

    /// <summary>
    /// Ein fester Ablauf mit allem aus R2 (Zufallsbereiche beim Streuen und Stempeln, Transformieren absolut und relativ um alle Achsen,
    /// Einrasten, Neu würfeln, Rückgängig/Wiederholen) – für den Gleichheitstest zwischen Linux und Windows.
    /// </summary>
    internal static string Ablauf()
    {
        var e = HangMitEbene();
        var bereiche = new Felspinsel(Formen: new[] { "wand", "rippe", "block" }, Radius: 20, Dichte: 0.25, AchseX: new(0.8, 1.4), AchseY: new(0.7, 1.8),
            AchseZ: new(0.9, 1.1), Kippbereich: new(-25, 25), Rollbereich: new(-40, 10), Versatzbereich: new(-0.5, 1.5), Drehbereich: new(-60, 60),
            NeigungBis: 12, Neigung: 1);
        e.FelsStreuen(bereiche.MitGroesse(0.6, 2.2), Linie(O(-80, -40), O(80, 40), 20), startwert: 4242);
        e.FelsStempeln(bereiche with { Formen = null, Gruppe = "wand" }, O(50, -100));
        var alle = e.Felsen.ToList();
        e.FelsTransformieren(alle.Take(3), new Felswerte(Kippen: 135.55, Rollen: -170, AchseY: 2.5, Versatz: 3.333));
        e.FelsTransformieren(alle.Skip(3).Take(5), new Felsaenderung(Drehachse: Felsachse.X, Winkel: 37.3));
        e.FelsTransformieren(alle.Skip(5).Take(5), new Felsaenderung(Drehachse: Felsachse.Z, Winkel: -71.9, Weltachsen: false));
        e.FelsTransformieren(alle.Skip(8).Take(4), new Felsaenderung(DX: 2.5, DY: 0.4, DZ: -1.25, Drehachse: Felsachse.Y, Winkel: 95, FaktorX: 1.3, FaktorY: 0.7, FaktorZ: 1.1));
        e.FelsEinrasten(alle.Skip(10).Take(6), ausrichten: true);
        e.FelsEinrasten(alle.Take(2));
        e.FelsNeuWuerfeln(alle.Skip(12), bereiche with { Versatzbereich = new(2, 4) }, Felswurf.Alle, startwert: 99);
        e.FelsNeuWuerfeln(alle.Take(4), bereiche, Felswurf.Kippen | Felswurf.Achsen);
        e.Rueckgaengig();
        e.Rueckgaengig();
        e.Wiederholen();
        return e.ZuJson();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fels R2: Alte Karten öffnen und schreiben ergibt Byte für Byte dieselbe Datei; ein alter Strich (Stand vor R2) ergibt dieselben Stücke", () =>
        {
            foreach (var datei in Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Datenordner)!, "karten"), "*.json").OrderBy(d => d, StringComparer.Ordinal))
            {
                var text = File.ReadAllText(datei);
                Pruefe.Gleich(text, Kartenentwurf.AusJson(text, Katalog.Value).ZuJson(), $"{Path.GetFileName(datei)}: Byte für Byte");
            }
            var alt = AlterAblauf();
            Pruefe.Wahr(!alt.Contains("\"achsen\": [") && !alt.Contains("\"versatz\""), "ohne Bereiche keine neuen Felder: " + Umgebung(alt));
            Pruefe.Gleich("89bbc16442a5f4918a757cc3803b023db6ae0a09b2aa88425389c6c5797cacf7", Sha(alt),
                "alter Ablauf: dieselben Bytes wie vor R2 (ändert sich das gewollt, hier die neue Prüfsumme eintragen und es im Bericht nennen)");
            Pruefe.Gleich(alt, Kartenentwurf.AusJson(alt, Katalog.Value).ZuJson(), "alte Felsen wieder geschrieben");
            // mit Boden: hängt auch am Felsboden – ändert der sich gewollt (etwa R1), hier die neue Prüfsumme eintragen
            Pruefe.Gleich("2c74dbcb26b03cdffce6098aee8d527d78858a90f6e901143bd0bca1affcc159", Sha(AlterAblauf(boden: true)), "alter Ablauf mit Boden wie vor R2");
        }),

        ("Fels R2: Achsen, Versatz und volles Kippen stehen in der Datei, lesen und schreiben ergibt dieselben Bytes; die Stadtkarte liest sie", () =>
        {
            var e = Leer();
            e.FelsStempeln(new Felspinsel(Formen: new[] { "wand" }, Boden: false), O(10, 20), drehung: 0, startwert: 1);
            var f = e.Felsen[0];
            e.FelsTransformieren(new[] { f }, new Felswerte(Kippen: 120, Rollen: -45.04, AchseX: 1.5, AchseY: 0.5, AchseZ: 2, Versatz: 1.27));
            Pruefe.Gleich(120.0, f.KippenX);
            Pruefe.Gleich(-45.0, f.KippenZ);
            Pruefe.Gleich(1.25, f.Versatz, "auf 5 cm");
            var text = e.ZuJson();
            Pruefe.Wahr(text.Contains("\"kippen\": [\n") || text.Contains("\"kippen\": [120"), "kippen in der Datei:\n" + text);
            Pruefe.Wahr(text.Contains("\"achsen\": [") && text.Contains("\"versatz\": 1.25"), "achsen und versatz in der Datei:\n" + text);
            var zwei = Kartenentwurf.AusJson(text, Katalog.Value);
            Pruefe.Gleich(text, zwei.ZuJson(), "wieder geschrieben");
            var g = zwei.Felsen[0];
            Pruefe.Gleich((1.5, 0.5, 2.0, 1.25, 120.0), (g.AchseX, g.AchseY, g.AchseZ, g.Versatz, g.KippenX));
            var s = Stadtkarte.AusJson(text, Katalog.Value).Felsen!.Stuecke[0];
            Pruefe.Gleich("1.5,0.5,2", string.Join(",", s.Achsen!.Select(a => a.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            Pruefe.Gleich(1.25, s.Versatz);
            Pruefe.Gleich(120.0, s.Kippen![0]);
            // zurück auf die Vorgabe: die Felder verschwinden wieder
            zwei.FelsTransformieren(zwei.Felsen, new Felswerte(AchseX: 1, AchseY: 1, AchseZ: 1, Versatz: 0));
            var drei = zwei.ZuJson();
            Pruefe.Wahr(!drei.Contains("\"achsen\": [") && !drei.Contains("\"versatz\""), "Vorgabe fehlt in der Datei: " + Umgebung(drei));
        }),

        ("Fels R2: Die Kartenprüfung meldet Kippen, Achsen und Versatz außerhalb der Grenzen als Hinweis", () =>
        {
            var e = Leer();
            e.FelsStempeln(new Felspinsel(Formen: new[] { "block" }, Boden: false), O(0, 0), startwert: 1);
            e.FelsTransformieren(e.Felsen, new Felswerte(Kippen: 0, Rollen: 0));
            var text = e.ZuJson().Replace("\"einsinken\"", "\"achsen\": [1, 5, 1], \"versatz\": 25, \"kippen\": [200, 0], \"einsinken\"");
            var befunde = Kartenpruefung.Pruefen(Kartenentwurf.AusJson(text, Katalog.Value)).Where(b => b.Regel == "fels").Select(b => b.Text).ToList();
            Pruefe.Wahr(befunde.Any(b => b.Contains("Kippen 200")), "Kippen: " + string.Join(" | ", befunde));
            Pruefe.Wahr(befunde.Any(b => b.Contains("Achsen")), "Achsen: " + string.Join(" | ", befunde));
            Pruefe.Wahr(befunde.Any(b => b.Contains("Versatz 25")), "Versatz: " + string.Join(" | ", befunde));
            Pruefe.Wahr(!Kartenpruefung.Pruefen(e).Any(b => b.Regel == "fels"), "ein gutes Stück ohne Befund");
        }),

        ("Fels R2: Euler-Winkel Y · X · Z – aus Winkeln die Basis und zurück, genau bis auf Rechenrauschen; auf der Seite Rollen 0", () =>
        {
            var z = new Reichsgilde.Core.Zufallsquelle(7);
            for (var n = 0; n < 200; n++)
            {
                var (a, b, c) = (z.NextDouble() * 360, (z.NextDouble() * 2 - 1) * 89, (z.NextDouble() * 2 - 1) * 179);
                var basis = Felsbasis.AusEuler(a, b, c);
                var (a2, b2, c2) = basis.Euler();
                BasisNahe(basis, Felsbasis.AusEuler(a2, b2, c2), 1e-9, $"Fall {n}");
                Nahe(b, b2, 1e-7, $"Kippen Fall {n}");
            }
            var seite = Felsbasis.AusEuler(30, 90, 20).Euler();
            Pruefe.Gleich(0.0, seite.Rollen);
            Nahe(90, seite.Kippen, 1e-6, "auf der Seite");
            BasisNahe(Felsbasis.AusEuler(30, 90, 20), Felsbasis.AusEuler(seite.Drehung, seite.Kippen, seite.Rollen), 1e-7, "auf der Seite");
            // die Achsen des Stücks: Drehung 90° um die Senkrechte – die Schauseite (+Z) zeigt nach Osten (+X), wie Kartengeometrie.Dreh
            var ost = Felsbasis.AusEuler(90, 0, 0).AchseZ;
            Nahe(1, ost.X, 1e-12, "Schauseite nach Osten");
            Nahe(0, ost.Z, 1e-12, "Schauseite nach Osten");
            Pruefe.Gleich((180.0, -179.9, 0.0, 180.0), (Felsraum.Winkel(-180), Felsraum.Winkel(180.1), Felsraum.Winkel(-0.01), Felsraum.Winkel(540)));
        }),

        ("Fels R2: FelsTransformieren absolut für mehrere Stücke ist ein Schritt; Rückgängig Byte für Byte; gerundet und begrenzt", () =>
        {
            var e = HangMitEbene();
            e.FelsStempeln(new Felspinsel(Gruppe: "wand", Boden: false), O(-50, 0), startwert: 3);
            var vorher = e.ZuJson();
            var schritte = e.Geschichte.Schritte;
            e.FelsTransformieren(e.Felsen.Take(3), new Felswerte(Kippen: 400, Rollen: -12.345, Massstab: 9, AchseX: 0.1, AchseZ: 1.234, Versatz: -30, Drehung: -10));
            Pruefe.Gleich(schritte + 1, e.Geschichte.Schritte, "ein Schritt");
            Pruefe.Gleich("3 Felsstücke verändert", e.Geschichte.RueckgaengigText);
            foreach (var f in e.Felsen.Take(3))
                Pruefe.Gleich((40.0, -12.3, 4.0, 0.25, 1.0, 1.23, -20.0, 350.0), (f.KippenX, f.KippenZ, f.Massstab, f.AchseX, f.AchseY, f.AchseZ, f.Versatz, f.Drehung));
            Pruefe.Gleich(0.0, e.Felsen[3].Versatz, "die anderen bleiben");
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson(), "Rückgängig");
            e.Wiederholen();
            Pruefe.Wahr(vorher != e.ZuJson(), "Wiederholen");
            // je Stück eigene Werte (der Gizmo), nichts geändert: kein Schritt
            var n = e.Geschichte.Schritte;
            e.FelsTransformieren(e.Felsen.Select(f => (f, new Felswerte(X: f.X, Kippen: f.KippenX))));
            Pruefe.Gleich(n, e.Geschichte.Schritte, "ohne Änderung kein Schritt");
            e.FelsTransformieren(new[] { (e.Felsen[0], new Felswerte(X: 1.01)), (e.Felsen[1], new Felswerte(X: 2)) }, "verschoben");
            Pruefe.Gleich((1.0, 2.0), (e.Felsen[0].X, e.Felsen[1].X));
            Pruefe.Gleich("2 Felsstücke verschoben", e.Geschichte.RueckgaengigText);
            // der Boden um den Fels bleibt liegen (entschieden 02.11.2026)
            var b = Leer();
            b.FelsStempeln(new Felspinsel(Formen: new[] { "kuppe" }), O(0, 0), startwert: 2);
            var boden = b.ZuJson();
            b.FelsTransformieren(b.Felsen, new Felsaenderung(DX: 10));
            Pruefe.Gleich(Teil(boden, "\"boden\""), Teil(b.ZuJson(), "\"boden\""), "Boden unverändert");
        }),

        ("Fels R2: FelsTransformieren relativ – verschieben, um die Senkrechte um die gemeinsame Mitte drehen, um x und z drehen, skalieren", () =>
        {
            var e = Leer();
            e.FelsStempeln(new Felspinsel(Formen: new[] { "block" }, Boden: false), O(10, 0), drehung: 20, startwert: 1);
            e.FelsStempeln(new Felspinsel(Formen: new[] { "block" }, Boden: false), O(-10, 0), drehung: 0, startwert: 1);
            var (a, b) = (e.Felsen[0], e.Felsen[1]);
            e.FelsTransformieren(new[] { a, b }, new Felswerte(Kippen: 0, Rollen: 0));
            e.FelsTransformieren(new[] { a, b }, new Felsaenderung(DX: 1, DY: 0.5, DZ: -2));
            Pruefe.Gleich((11.0, 0.5, -2.0), (a.X, a.Versatz, a.Z));
            Pruefe.Gleich("2 Felsstücke verschoben", e.Geschichte.RueckgaengigText);
            // um die Senkrechte um die Mitte (1, −2): a von (11, −2) nach (1, −12) – wie Kartengeometrie.Dreh mit 90°: (x, z) → (z, −x)
            e.FelsTransformieren(new[] { a, b }, new Felsaenderung(Drehachse: Felsachse.Y, Winkel: 90));
            Pruefe.Gleich((1.0, -12.0, 110.0, 0.0, 0.5), (a.X, a.Z, a.Drehung, a.KippenX, a.Versatz));
            Pruefe.Gleich((1.0, 8.0, 90.0), (b.X, b.Z, b.Drehung));
            var dreh = Kartengeometrie.Dreh(10, 0, 90);
            Pruefe.Gleich((Kartenraster.Punkt(1 + dreh.X), Kartenraster.Punkt(-2 + dreh.Z)), (a.X, a.Z), "wie Kartengeometrie.Dreh");
            // ein Stück allein dreht um sich selbst
            e.FelsTransformieren(new[] { a }, new Felsaenderung(Drehachse: Felsachse.X, Winkel: 30));
            Pruefe.Gleich((1.0, -12.0, 0.5), (a.X, a.Z, a.Versatz));
            BasisNahe(Felsbasis.Um(Felsachse.X, 30) * Felsbasis.AusEuler(110, 0, 0), Felsbasis.AusEuler(a.Drehung, a.KippenX, a.KippenZ), 2e-3, "um x in der Welt");
            // um die eigene z-Achse: nur Rollen
            var (d, k) = (a.Drehung, a.KippenX);
            e.FelsTransformieren(new[] { a }, new Felsaenderung(Drehachse: Felsachse.Z, Winkel: 15, Weltachsen: false));
            Pruefe.Gleich((d, k), (a.Drehung, a.KippenX));
            // um die eigene x-Achse eines geraden Stücks: Kippen
            e.FelsTransformieren(new[] { b }, new Felsaenderung(Drehachse: Felsachse.X, Winkel: 25, Weltachsen: false));
            Pruefe.Gleich((90.0, 25.0, 0.0), (b.Drehung, b.KippenX, b.KippenZ));
            // zwei Stücke um x in der Welt um ihre Mitte: die Höhe (Versatz) ändert sich, das untere geht hinauf
            var c = Leer();
            c.FelsStempeln(new Felspinsel(Formen: new[] { "platte" }, Boden: false), O(0, 10), drehung: 0, startwert: 1);
            c.FelsStempeln(new Felspinsel(Formen: new[] { "platte" }, Boden: false), O(0, -10), drehung: 0, startwert: 1);
            c.FelsTransformieren(c.Felsen, new Felswerte(Kippen: 0, Rollen: 0));
            c.FelsTransformieren(c.Felsen, new Felsaenderung(Drehachse: Felsachse.X, Winkel: 30));
            // um x: (y, z) → (y·cos − z·sin, y·sin + z·cos); Mitte (0, 0, 0)
            Pruefe.Gleich((-5.0, 8.65), (c.Felsen[0].Versatz, c.Felsen[0].Z));
            Pruefe.Gleich((5.0, -8.65), (c.Felsen[1].Versatz, c.Felsen[1].Z));
            Nahe(30, c.Felsen[0].KippenX, 0.051, "Kippen");
            // skalieren: gleich → Maßstab, je Achse → Achsen; die Mitten rücken auseinander
            var m = c.Felsen[0].Massstab;
            c.FelsTransformieren(c.Felsen, new Felsaenderung(FaktorX: 2, FaktorY: 2, FaktorZ: 2));
            Pruefe.Gleich(Math.Round(m * 2, 2), c.Felsen[0].Massstab);
            Pruefe.Gleich((-10.0, 17.3), (c.Felsen[0].Versatz, c.Felsen[0].Z));
            Pruefe.Gleich("2 Felsstücke skaliert", c.Geschichte.RueckgaengigText);
            c.FelsTransformieren(new[] { c.Felsen[0] }, new Felsaenderung(FaktorX: 1.5, FaktorY: 1, FaktorZ: 0.5));
            Pruefe.Gleich((1.5, 1.0, 0.5), (c.Felsen[0].AchseX, c.Felsen[0].AchseY, c.Felsen[0].AchseZ));
            Pruefe.Gleich((-10.0, 17.3), (c.Felsen[0].Versatz, c.Felsen[0].Z), "allein um sich selbst");
        }),

        ("Fels R2: Abfragen für den Gizmo – Ursprung, Basis, Größe je Achse, Hüllkasten; Höhe und Normale des Bodens; Einrasten", () =>
        {
            var e = HangMitEbene();
            e.FelsStempeln(new Felspinsel(Formen: new[] { "wand" }, Boden: false, Neigung: 0), O(-50, 0), drehung: 0, startwert: 1);
            var f = e.Felsen[0];
            e.FelsTransformieren(new[] { f }, new Felswerte(Kippen: 0, Rollen: 0, Einsinken: 0.5, Massstab: 1, AchseY: 2, Versatz: 1));
            var r = e.FelsRahmen(f);
            var w = Felsformen.Finde("wand")!;
            Pruefe.Gleich((w.Breite, w.Hoehe * 2, w.Tiefe), (r.Groesse.X, r.Groesse.Y, r.Groesse.Z));
            Nahe(-w.Hoehe + 1, r.Ursprung.Y, 1e-9, "Ursprung: eben, Einsinken die halbe Höhe, 1 m Versatz");
            Nahe(-w.Breite / 2 - 50, r.HuelleMin.X, 1e-9, "Hülle x");
            Nahe(w.Hoehe + 1, r.HuelleMax.Y, 1e-9, "Hülle oben");
            // Boden: eben bei x < 0, am Hang 30 % nach Osten
            var (h0, n0) = e.BodenBei(O(-50, 0));
            Pruefe.Gleich((0.0, 0.0, 1.0, 0.0), (h0, n0.X, n0.Y, n0.Z));
            var (h1, n1) = e.BodenBei(O(50, 0));
            Nahe(15, h1, 0.05, "Hang in der Mitte 15 m");
            Nahe(-0.3 / Math.Sqrt(1.09), n1.X, 1e-3, "Normale zeigt nach Westen (hangabwärts)");
            // Einrasten: gerade auf ebenem Boden → Versatz 0; gekippt hebt es sich, bis die untere Kante so tief steckt wie sonst die Unterkante
            e.FelsEinrasten(new[] { f });
            Pruefe.Gleich(0.0, f.Versatz);
            Pruefe.Gleich("Felswand aufgesetzt", e.Geschichte.RueckgaengigText);
            e.FelsTransformieren(new[] { f }, new Felswerte(Kippen: 20));
            e.FelsEinrasten(new[] { f });
            Nahe(Math.Sin(20 * Math.PI / 180) * Felsboden.Grundflaeche * w.Tiefe / 2, f.Versatz, 0.03, "gekippt gehoben");
            // am Hang mit Ausrichten: Neigung 1, gerade, Versatz klein
            e.FelsTransformieren(new[] { f }, new Felswerte(X: 50, AchseY: 1));
            e.FelsEinrasten(new[] { f }, ausrichten: true);
            Pruefe.Gleich((1.0, 0.0, 0.0), (f.Neigung, f.KippenX, f.KippenZ));
            Pruefe.Wahr(Math.Abs(f.Versatz) <= 0.15, $"am Hang ausgerichtet kaum Versatz ({f.Versatz})");
            // die Basis am Hang mit Neigung 1: die y-Achse ist die Hangnormale
            var nb = e.FelsRahmen(f).Basis.AchseY;
            Nahe(n1.X, nb.X, 1e-3, "Achse y = Normale");
            Nahe(n1.Y, nb.Y, 1e-3, "Achse y = Normale");
        }),

        ("Fels R2: Am Hang dreht eine Weltachse die ganze Lage samt Hangneigung; Einrasten auf dem Kopf; Winkel springen nicht über 90°", () =>
        {
            var e = HangMitEbene();
            e.FelsStempeln(new Felspinsel(Formen: new[] { "block" }, Neigung: 1, Boden: false), O(50, 0), drehung: 20, startwert: 1);
            var f = e.Felsen[0];
            foreach (var achse in new[] { Felsachse.X, Felsachse.Y, Felsachse.Z })
            {
                var vorher = e.FelsRahmen(f).Basis;
                e.FelsTransformieren(new[] { f }, new Felsaenderung(Drehachse: achse, Winkel: 30));
                BasisNahe(Felsbasis.Um(achse, 30) * vorher, e.FelsRahmen(f).Basis, 3e-3, $"um Welt-{achse} am Hang");
            }
            // auf dem Kopf (Kippen 180°) auf ebenem Boden: Einrasten hebt um die ganze Höhe
            var b = Leer();
            b.FelsStempeln(new Felspinsel(Formen: new[] { "block" }, Boden: false), O(0, 0), drehung: 0, startwert: 1);
            var k = b.Felsen[0];
            b.FelsTransformieren(new[] { k }, new Felswerte(Kippen: 180, Rollen: 0));
            b.FelsEinrasten(new[] { k });
            Nahe(Felsformen.Finde("block")!.Hoehe * k.Massstab, k.Versatz, 0.03, "auf dem Kopf um die Höhe gehoben");
            // über 90° gekippt: eine kleine Drehung um die eigene y-Achse lässt Kippen nahe 120
            b.FelsTransformieren(new[] { k }, new Felswerte(Drehung: 10, Kippen: 120, Rollen: 30));
            b.FelsTransformieren(new[] { k }, new Felsaenderung(Drehachse: Felsachse.Y, Winkel: 0.1, Weltachsen: false));
            Pruefe.Wahr(Math.Abs(k.KippenX - 120) < 1 && Math.Abs(k.Drehung - 10) < 1, $"Winkel nahe den alten ({k.Drehung}, {k.KippenX}, {k.KippenZ})");
            // ein umgekehrter Bereich bleibt „Von fest“
            Pruefe.Wahr(new Felspinsel(Versatzbereich: new(5, 2)).Begrenzt().Versatzbereich!.IstLeer, "Bis < Von bleibt leer");
        }),

        ("Fels R2: Zufallsbereiche – Werte im Bereich; leere Bereiche ziehen nicht (gleiche Stücke wie ohne); Größe min–max; Grenze in Grad", () =>
        {
            Kartenentwurf Streuen(Felspinsel p)
            {
                var e = HangMitEbene();
                e.FelsStreuen(p, Linie(O(-100, -50), O(100, 50), 20), startwert: 11);
                return e;
            }
            var grund = new Felspinsel(Formen: new[] { "block", "platte" }, Radius: 15, Dichte: 0.3, Boden: false);
            var ohne = Streuen(grund);
            // ein fester Wert (Von = Bis) zieht nicht: dieselben Stücke, nur die Höhe je Achse anders
            var fest = Streuen(grund with { AchseY = new(1.5, 1.5), Versatzbereich = new(0.5, 0.5) });
            Pruefe.Gleich(ohne.Felsen.Count, fest.Felsen.Count);
            for (var i = 0; i < ohne.Felsen.Count; i++)
            {
                var (a, b) = (ohne.Felsen[i], fest.Felsen[i]);
                Pruefe.Wahr(a.X == b.X && a.Z == b.Z && a.Drehung == b.Drehung && a.KippenX == b.KippenX && a.Massstab == b.Massstab, $"Stück {i} gleich");
                Pruefe.Gleich((1.5, 0.5), (b.AchseY, b.Versatz));
            }
            var mit = Streuen(grund with
            {
                AchseX = new(0.5, 0.8), AchseY = new(1.5, 3), Kippbereich = new(30, 60), Rollbereich = new(-10, -5), Versatzbereich = new(1, 2),
                Drehbereich = new(100, 110), ZumHang = false,
            });
            Pruefe.Wahr(mit.Felsen.Count > 10, $"genug Stücke ({mit.Felsen.Count})");
            foreach (var f in mit.Felsen)
            {
                Pruefe.Wahr(f.AchseX is >= 0.5 and <= 0.8 && f.AchseY is >= 1.5 and <= 3 && f.AchseZ == 1, $"Achsen {f.AchseX} {f.AchseY} {f.AchseZ}");
                Pruefe.Wahr(f.KippenX is >= 30 and <= 60 && f.KippenZ is >= -10 and <= -5, $"Kippen {f.KippenX}, Rollen {f.KippenZ}");
                Pruefe.Wahr(f.Versatz is >= 1 and <= 2 && f.Drehung is >= 100 and <= 110, $"Versatz {f.Versatz}, Drehung {f.Drehung}");
            }
            Pruefe.Wahr(mit.Felsen.Select(f => f.AchseY).Distinct().Count() > 5, "die Achsen streuen");
            // gleich bleibt gleich
            Pruefe.Gleich(mit.ZuJson(), Streuen(grund with
            {
                AchseX = new(0.5, 0.8), AchseY = new(1.5, 3), Kippbereich = new(30, 60), Rollbereich = new(-10, -5), Versatzbereich = new(1, 2),
                Drehbereich = new(100, 110), ZumHang = false,
            }).ZuJson(), "gleicher Strich, gleiche Bytes");
            // Größe min–max ist Größe ± Streuung
            var g = grund.MitGroesse(0.5, 2);
            Pruefe.Gleich((0.5, 2.0), (Math.Round(g.GroesseMin, 9), Math.Round(g.GroesseMax, 9)));
            Pruefe.Wahr(Streuen(g).Felsen.All(f => f.Massstab is >= 0.5 and <= 2), "Größe im Bereich");
            // am Hang (30 % ≈ 16,7°) höchstens 10° aus der Senkrechten; ohne Grenze der ganze Anteil
            var e = HangMitEbene();
            var p = new Felspinsel(Formen: new[] { "block" }, Neigung: 1, NeigungBis: 10, Boden: false);
            var am = e.FelsStempelVorschau(p, O(50, 0), startwert: 1)[0];
            Nahe(10 / (Math.Atan(0.3) * 180 / Math.PI), am.Neigung, 0.006, "Neigung begrenzt");
            Pruefe.Gleich(1.0, e.FelsStempelVorschau(p with { NeigungBis = null }, O(50, 0), startwert: 1)[0].Neigung);
            Pruefe.Gleich(1.0, e.FelsStempelVorschau(p, O(-50, 0), startwert: 1)[0].Neigung, "eben: keine Grenze nötig");
        }),

        ("Fels R2: Neu würfeln zieht die Bereiche des Pinsels für gewählte Stücke – nur, was gewählt ist; ein Schritt; gleich bleibt gleich", () =>
        {
            Kartenentwurf Lauf(Felswurf was)
            {
                var e = HangMitEbene();
                e.FelsStreuen(new Felspinsel(Formen: new[] { "block", "kuppe" }, Radius: 20, Dichte: 0.3, Boden: false), Linie(O(-80, 0), O(80, 0), 8), startwert: 3);
                e.FelsNeuWuerfeln(e.Felsen.Where((_, i) => i % 2 == 0), new Felspinsel(Kippbereich: new(60, 70), Versatzbereich: new(2, 3), AchseZ: new(2, 2.5)),
                    was, startwert: 5);
                return e;
            }
            var vorher = HangMitEbene();
            vorher.FelsStreuen(new Felspinsel(Formen: new[] { "block", "kuppe" }, Radius: 20, Dichte: 0.3, Boden: false), Linie(O(-80, 0), O(80, 0), 8), startwert: 3);
            var alle = Lauf(Felswurf.Alle);
            Pruefe.Gleich(alle.ZuJson(), Lauf(Felswurf.Alle).ZuJson(), "gleich bleibt gleich");
            Pruefe.Wahr(alle.Geschichte.RueckgaengigText!.EndsWith("neu gewürfelt"), alle.Geschichte.RueckgaengigText!);
            for (var i = 0; i < alle.Felsen.Count; i++)
            {
                var (a, v) = (alle.Felsen[i], vorher.Felsen[i]);
                Pruefe.Gleich((v.X, v.Z, v.Form), (a.X, a.Z, a.Form), "Ort und Form bleiben");
                if (i % 2 == 0) Pruefe.Wahr(a.KippenX is >= 60 and <= 70 && a.Versatz is >= 2 and <= 3 && a.AchseZ is >= 2 and <= 2.5, $"Stück {i} neu");
                else Pruefe.Wahr(a.GleicheWerteWie(v), $"Stück {i} nicht gewählt, unverändert");
            }
            var nur = Lauf(Felswurf.Versatz);
            for (var i = 0; i < nur.Felsen.Count; i += 2)
            {
                var (a, v) = (nur.Felsen[i], vorher.Felsen[i]);
                Pruefe.Gleich((v.KippenX, v.AchseZ, v.Drehung, v.Massstab), (a.KippenX, a.AchseZ, a.Drehung, a.Massstab), $"Stück {i}: nur der Versatz");
                Pruefe.Wahr(a.Versatz is >= 2 and <= 3, $"Stück {i}: Versatz {a.Versatz}");
            }
            nur.Rueckgaengig();
            Pruefe.Gleich(vorher.ZuJson(), nur.ZuJson(), "Rückgängig");
            // ohne Angabe: nur Größe, Drehung und was einen Bereich hat – gestreckte, gehobene Stücke bleiben es
            var g = HangMitEbene();
            g.FelsStempeln(new Felspinsel(Formen: new[] { "block" }, Boden: false), O(-50, 0), startwert: 1);
            g.FelsTransformieren(g.Felsen, new Felswerte(AchseY: 2, Versatz: 3, Einsinken: 0.5));
            g.FelsNeuWuerfeln(g.Felsen, new Felspinsel(Kippbereich: new(10, 20)), startwert: 2);
            Pruefe.Gleich((2.0, 3.0, 0.5), (g.Felsen[0].AchseY, g.Felsen[0].Versatz, g.Felsen[0].Einsinken), "ohne Bereich bleibt es");
            Pruefe.Wahr(g.Felsen[0].KippenX is >= 10 and <= 20, "Kippen aus dem Bereich");
        }),

        ("Fels R2: Der Ablauf mit allem aus R2 gibt zweimal dieselben Bytes; alle Werte gerundet wie in der Datei und in ihren Grenzen", () =>
        {
            var a = Ablauf();
            Pruefe.Gleich(a, Ablauf(), "zweiter Lauf");
            var e = Kartenentwurf.AusJson(a, Katalog.Value);
            Pruefe.Wahr(e.Felsen.Count > 10, $"genug Stücke ({e.Felsen.Count})");
            Pruefe.Wahr(e.Felsen.Any(f => Math.Abs(f.KippenX) > 90) && e.Felsen.Any(f => f.AchseY != 1) && e.Felsen.Any(f => f.Versatz != 0), "der Ablauf nutzt alles");
            foreach (var f in e.Felsen)
            {
                Pruefe.Wahr(f.KippenX == Math.Round(f.KippenX, 1) && f.KippenZ == Math.Round(f.KippenZ, 1) && Math.Abs(f.KippenX) <= 180 && Math.Abs(f.KippenZ) <= 180, $"Kippen {f.KippenX} {f.KippenZ}");
                Pruefe.Wahr(new[] { f.AchseX, f.AchseY, f.AchseZ }.All(x => x == Math.Round(x, 2) && x is >= 0.25 and <= 4), $"Achsen {f.AchseX} {f.AchseY} {f.AchseZ}");
                Pruefe.Wahr(f.Versatz == Kartenraster.Punkt(f.Versatz) && Math.Abs(f.Versatz) <= 20, $"Versatz {f.Versatz}");
                Pruefe.Wahr(f.X == Kartenraster.Punkt(f.X) && f.Drehung == Kartenraster.Drehung(f.Drehung), $"Ort und Drehung {f.X} {f.Drehung}");
            }
            Pruefe.Gleich(a, e.ZuJson(), "wieder geschrieben");
        }),
    };

    /// <summary>Die Stelle um das erste „achsen“ bzw. „versatz“ (für die Meldung).</summary>
    static string Umgebung(string text)
    {
        var i = Math.Max(text.IndexOf("\"achsen\": [", StringComparison.Ordinal), text.IndexOf("\"versatz\"", StringComparison.Ordinal));
        return i < 0 ? "" : text[Math.Max(0, i - 300)..Math.Min(text.Length, i + 100)];
    }

    /// <summary>Der Abschnitt einer Kartendatei ab einem Feld (für den Vergleich des Bodens).</summary>
    static string Teil(string text, string feld)
    {
        var i = text.IndexOf(feld, StringComparison.Ordinal);
        return i < 0 ? "" : text[i..];
    }

    /// <summary>Hat das Stück dieselben Werte wie ein anderes (alle Felder der Datei)?</summary>
    static bool GleicheWerteWie(this EntwurfFels a, EntwurfFels b) => a.Form == b.Form && a.Gestein == b.Gestein && a.X == b.X && a.Z == b.Z
        && a.Drehung == b.Drehung && a.Massstab == b.Massstab && a.Neigung == b.Neigung && a.Einsinken == b.Einsinken && a.KippenX == b.KippenX
        && a.KippenZ == b.KippenZ && a.AchseX == b.AchseX && a.AchseY == b.AchseY && a.AchseZ == b.AchseZ && a.Versatz == b.Versatz;
}
