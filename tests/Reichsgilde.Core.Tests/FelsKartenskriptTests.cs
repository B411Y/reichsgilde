using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Kartenbau;

/// <summary>
/// Felspinsel K: Das Kartenskript setzt Felsen (<c>felsen --stempel</c> und <c>felsen --streuen</c>, tools/Kartenbau). Eine kleine Drachenfels-Probe
/// (Hügel, Große Wand mit Hang, Kuppe, gestreuter Schutt) auf einer Kopie der Messstadt: Die Karte lädt mit den Felsen, zweimal ausgeführt ergibt sie
/// dieselben Bytes. Gegenproben: ein Ort außerhalb ändert nichts, unlesbare Angaben gelten nicht.
/// </summary>
static class FelsKartenskriptTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;

    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Ein Projektordner im Temp-Verzeichnis mit Katalog, Wirtschaftsdaten und einer Kopie der Messstadt.</summary>
    static Projektordner Ordner()
    {
        var o = new Projektordner(Path.Combine(Path.GetTempPath(), "reichsgilde-felsen-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(o.Daten);
        Directory.CreateDirectory(o.Karten);
        foreach (var datei in new[] { "katalog.json", "wirtschaft.json" }) File.Copy(Path.Combine(Datenordner, datei), Path.Combine(o.Daten, datei));
        File.Copy(Datenpfad.Karte("messstadt"), o.Karte("messstadt"));
        return o;
    }

    static string[] A(params string[] schalter) => schalter;

    /// <summary>Die Drachenfels-Probe mit dem Kartenskript: Hügel, Große Wand mit Hang, Kuppe, Schutt gestreut. Gibt die Kartendatei und die Ausgabe zurück.</summary>
    static (string Datei, string Ausgabe) Probe()
    {
        var o = Ordner();
        try
        {
            var aus = new StringWriter();
            Pruefe.Gleich(0, Befehle.Gelaende(o, "messstadt", new Pinsel(Pinselart.Heben, 40, 12, Kante: 0.5), new[] { O(-450, 300) }, aus), aus.ToString());
            var wand = Reichsgilde.Kartenbau.Program.FelspinselVon("wandgross@trachyt", A("--hang=an"))!;
            Pruefe.Gleich(0, Befehle.Felsen(o, "messstadt", true, wand, new[] { O(-450, 330) }, 180, 4, aus), aus.ToString());
            var kuppe = Reichsgilde.Kartenbau.Program.FelspinselVon("kuppe", A("--gestein=trachyt", "--groesse=1.2"))!;
            Pruefe.Gleich(0, Befehle.Felsen(o, "messstadt", true, kuppe, new[] { O(-430, 290), O(-470, 285) }, null, 7, aus), aus.ToString());
            var schutt = Reichsgilde.Kartenbau.Program.FelspinselVon("block+schuttbrocken@trachyt", A("--radius=12", "--dichte=0.25", "--abstand=0.5", "--steigung=5-"))!;
            Pruefe.Gleich(0, Befehle.Felsen(o, "messstadt", false, schutt, new[] { O(-485, 300), O(-472, 328), O(-428, 328), O(-415, 300) }, null, 9, aus), aus.ToString());
            return (File.ReadAllText(o.Karte("messstadt")), aus.ToString());
        }
        finally
        {
            if (Directory.Exists(o.Wurzel)) Directory.Delete(o.Wurzel, recursive: true);
        }
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fels K: Kartenskript setzt eine Drachenfels-Probe (Stempel und Streuen); die Karte lädt mit allen Stücken, zweimal ausgeführt dieselben Bytes", () =>
        {
            var (datei, ausgabe) = Probe();
            var katalog = Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json"));
            var e = Kartenentwurf.AusJson(datei, katalog);
            Pruefe.Wahr(e.Felsen.Count(f => f.Form.StartsWith("wand_gross")) == 3, $"drei große Wände: {string.Join(", ", e.Felsen.Select(f => f.Form))}");
            Pruefe.Wahr(e.Felsen.Count(f => f.Form == "kuppe") == 2, "zwei Kuppen");
            Pruefe.Wahr(e.Felsen.Count(f => f.Form is "block" or "schuttbrocken") > 8, $"Schutt gestreut: {e.Felsen.Count}");
            Pruefe.Wahr(e.Felsen.All(f => e.GesteinVon(f) == "trachyt"), "alles Trachyt");
            Pruefe.Wahr(ausgabe.Contains("gestempelt") && ausgabe.Contains("gestreut"), ausgabe);
            var karte = Stadtkarte.AusJson(datei, katalog);
            Pruefe.Gleich(e.Felsen.Count, karte.Felsen?.Stuecke?.Count ?? 0, "die Spielkarte lädt alle Stücke");
            Pruefe.Gleich(datei, Probe().Datei, "zweimal ausgeführt: dieselben Bytes");
        }),

        ("Fels K: Ein Ort außerhalb der Karte setzt nichts und lässt die Datei, wie sie ist; der Filter kann alles verhindern", () =>
        {
            var o = Ordner();
            try
            {
                var vorher = File.ReadAllText(o.Karte("messstadt"));
                var aus = new StringWriter();
                Pruefe.Gleich(0, Befehle.Felsen(o, "messstadt", true, Reichsgilde.Kartenbau.Program.FelspinselVon("kuppe", A())!, new[] { O(9000, 9000) }, null, 1, aus));
                Pruefe.Wahr(aus.ToString().Contains("nichts geschrieben"), aus.ToString());
                // die Messstadt ist flach: „nur ab 20 % Steigung“ setzt nirgends etwas
                Pruefe.Gleich(0, Befehle.Felsen(o, "messstadt", false, Reichsgilde.Kartenbau.Program.FelspinselVon("block", A("--steigung=20-"))!, new[] { O(-450, 300), O(-400, 300) }, null, 1, aus));
                Pruefe.Gleich(vorher, File.ReadAllText(o.Karte("messstadt")), "unverändert");
                // Gegenprobe: ohne Filter streut derselbe Strich
                Pruefe.Gleich(0, Befehle.Felsen(o, "messstadt", false, Reichsgilde.Kartenbau.Program.FelspinselVon("block", A())!, new[] { O(-450, 300), O(-400, 300) }, null, 1, aus));
                Pruefe.Wahr(File.ReadAllText(o.Karte("messstadt")) != vorher, "ohne Filter geschrieben");
            }
            finally
            {
                if (Directory.Exists(o.Wurzel)) Directory.Delete(o.Wurzel, recursive: true);
            }
        }),

        ("Fels K: Die Angaben des Kartenskripts – Gruppe oder Formen, Gestein, Zahlen, Filter, Höhe; Unlesbares gilt nicht", () =>
        {
            var p = Reichsgilde.Kartenbau.Program.FelspinselVon("block+platte@kalkstein", A("--radius=12.5", "--dichte=0.3", "--abstand=1", "--groesse=0.8", "--streuung=0.1",
                "--einsinken=0.3", "--neigung=0.2", "--boden=aus", "--zumhang=aus", "--hoehe=-5,12", "--bodenfilter=wiese"))!;
            Pruefe.Gleich("block,platte", string.Join(",", p.Formen!));
            Pruefe.Gleich(null, p.Gruppe);
            Pruefe.Gleich("kalkstein", p.Gestein);
            Pruefe.Gleich((12.5, 0.3, 1.0, 0.8, 0.1, 0.3, 0.2), (p.Radius, p.Dichte, p.Abstand, p.Groesse, p.Streuung, p.Einsinken, p.Neigung));
            Pruefe.Gleich((false, false, false), (p.Boden, p.ZumHang, p.HangFormen));
            Pruefe.Gleich(new Felsbereich(-5, 12), p.Hoehenbereich);
            Pruefe.Gleich(Bodentextur.Wiese, p.Filter?.Boden);
            var g = Reichsgilde.Kartenbau.Program.FelspinselVon("wandgross", A("--hang=an"))!;
            Pruefe.Gleich(("wandgross", true, true), (g.Gruppe, g.HangFormen, g.Boden));
            foreach (var (vorlage, schalter) in new[] { ("granit", ""), ("block@marmor", ""), ("block", "--gestein=marmor"), ("block", "--dichte=viel"),
                         ("block", "--boden=ja"), ("block", "--hoehe=5"), ("block", "--steigung=steil"), ("block+", "--radius=1,5"), ("", "") })
                Pruefe.Gleich(null, Reichsgilde.Kartenbau.Program.FelspinselVon(vorlage, schalter.Length > 0 ? A(schalter) : A()), $"{vorlage} {schalter}");
            Pruefe.Wahr(Reichsgilde.Kartenbau.Program.FelspinselVon("block+", A()) is not null, "Gegenprobe: ein + am Ende schadet nicht");
        }),
    };
}
