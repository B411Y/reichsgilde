using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests: Vorrat auf Verdacht (Nutzer 16.11.2026: die KI sieht nicht in die Zukunft, <c>ki.vorrat</c>, KiRivale.Vorrat.cs) – ein Wurf je Haus und
/// Jahr mit 10 %, bei Anzeichen 30 %; je Ware 15–35 % des Lagers; nie mehr, als das Haus bezahlen kann; Freigabe beim nächsten Wurf ohne Treffer;
/// keine Ansage-Abfrage im Ordner Ki/.
/// </summary>
static class KiVorratTests
{
    const string VorratZeile = "\"vorrat\": { \"chance\": 0.1, \"chanceMitAnzeichen\": 0.3,";

    static readonly Lazy<string> Json = new(() =>
    {
        var json = File.ReadAllText(Datenpfad.Wirtschaft());
        Pruefe.Wahr(json.Contains(VorratZeile), "ki.vorrat steht nicht wie erwartet in wirtschaft.json");
        return json;
    });

    /// <summary>Die echten Daten – mit <paramref name="chance"/> statt 10 % (ohne Anzeichen).</summary>
    static Wirtschaftsdaten Daten(double chance = 0.1) => Wirtschaftsdaten.AusJson(Json.Value.Replace(VorratZeile,
        VorratZeile.Replace("\"chance\": 0.1", $"\"chance\": {chance.ToString(System.Globalization.CultureInfo.InvariantCulture)}")));

    /// <summary>Die Daten ohne Abschnitt ki.vorrat (für Tests, die bitgleich mit dem Kern davor rechnen).</summary>
    public static string OhneVorrat(string json)
    {
        var a = json.IndexOf(",\n    \"vorrat\": {", StringComparison.Ordinal);
        Pruefe.Wahr(a >= 0, "ki.vorrat steht nicht wie erwartet in wirtschaft.json");
        var e = json.IndexOf('}', a);
        return json[..a] + json[(e + 1)..];
    }

    static readonly Lazy<Wirtschaftsdaten> Immer = new(() => Daten(1));
    static readonly Lazy<Wirtschaftsdaten> Echt = new(() => Daten());

    static long Minute(int jahr, int stunde) => jahr * (long)Spielzeit.MinutenProTag + stunde * 60 - Spielzeit.TagesbeginnMinute;

    /// <summary>Ein KI-Haus in der Probestadt zu dieser Minute.</summary>
    /// <summary>Die KI würfelt schon beim Anmelden (ihre erste Stunde) – eine Tischlerei mit Großem Pferdekarren gibt es dann schon, wenn gewünscht.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K, KiRivale Ki) KiHaus(Wirtschaftsdaten daten, long minute, double geld = 30_000,
        string? tischlerei = null)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 },
            new Spielzeit(minute), daten);
        var w = sim.Wirtschaft!;
        w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte("probestadt")));
        var k = w.KontoEroeffnen("Haus Vorrat", startvermoegen: geld);
        if (tischlerei is not null) w.BetriebEroeffnen(tischlerei, Betriebsart.Tischlerei, k, startkarren: "Großer Pferdekarren");
        return (sim, w, k, w.KiAnmelden(k));
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Vorrat: Der Wurf trifft in etwa 10 % der Jahre, mit Anzeichen in etwa 30 %; der Anteil liegt zwischen 15 und 35 %", () =>
        {
            var (_, w, _, ki) = KiHaus(Echt.Value, Minute(0, 5));
            var d = w.Daten.Ki!.Vorrat!;
            int ohne = 0, mit = 0;
            double min = 1, max = 0;
            const int jahre = 4000;
            for (var j = 0; j < jahre; j++)
            {
                if (ki.Vorratswurf(d, 1400 + j, anzeichen: false).Treffer) ohne++;
                var (treffer, anteil) = ki.Vorratswurf(d, 1400 + j, anzeichen: true);
                if (treffer) mit++;
                min = Math.Min(min, anteil);
                max = Math.Max(max, anteil);
            }
            Pruefe.Ungefaehr(0.10, ohne / (double)jahre, 0.15);
            Pruefe.Ungefaehr(0.30, mit / (double)jahre, 0.08);
            Pruefe.Wahr(min >= 0.15 && max <= 0.35, $"Anteil {min} … {max}");
            Pruefe.Wahr(min < 0.16 && max > 0.34, $"der Anteil streut über die ganze Spanne: {min} … {max}");
            Pruefe.Gleich(ki.Vorratswurf(d, 1407, false), ki.Vorratswurf(d, 1407, false)); // derselbe Wurf, kein geteilter Zustand
        }),

        ("KI-Vorrat: Anzeichen sind nur geltende schlechte oder schwere Lagen und ein strenger Winter – ohne Jahreszeiten und Ernte keine", () =>
        {
            var (_, w, _, ki) = KiHaus(Echt.Value, Minute(0, 5));
            int ja = 0, nein = 0;
            for (var j = 0; j < 60; j++)
            {
                w.Uhr = new Spielzeit(Minute(j, 5));
                var erwartet = w.Jahreszeiten.IstStrengerWinter || w.Lagen.Jetzt.Any(l => l.Guete is Lagegute.Schlecht or Lagegute.Schwer);
                Pruefe.Gleich(erwartet, ki.VorratsAnzeichen);
                if (erwartet) ja++; else nein++;
            }
            Pruefe.Wahr(ja > 0 && nein > 0, $"in 60 Jahren beides: {ja} mit, {nein} ohne Anzeichen");

            var ohne = Wirtschaftsdaten.AusJson(Datenpfad.OhneErnte(Datenpfad.OhneJahreszeiten(Json.Value)));
            var (_, w2, _, ki2) = KiHaus(ohne, Minute(0, 5));
            for (var j = 0; j < 60; j++)
            {
                w2.Uhr = new Spielzeit(Minute(j, 5));
                Pruefe.Wahr(!ki2.VorratsAnzeichen, $"Jahr {j}: ohne Jahreszeiten und Ernte keine Anzeichen");
            }
        }),

        ("KI-Vorrat: Bei einem Treffer hält jede Werkstatt von Grundnahrung und Zutaten 15–35 % des Lagers je Ware vor; Grundnahrung kauft ein Karren", () =>
        {
            var (sim, w, _, ki) = KiHaus(Immer.Value, Minute(1, 4), tischlerei: "Tischlerei Vorrat"); // erste Stunde: würfeln
            var t = w.Betriebe.Single();
            var lager = t.LagerKapazitaetJeWare;
            var vorrat = ki.Vorrat["Tischlerei Vorrat"];
            foreach (var ware in new[] { "Gerste", "Roggen", "Körnermahlzeit" }.Concat(t.Zutaten.Keys))
            {
                Pruefe.Wahr(vorrat.ContainsKey(ware), $"{ware} fehlt im Vorrat: {string.Join(", ", vorrat.Keys)}");
                Pruefe.Wahr(vorrat[ware] >= (int)(0.15 * lager) && vorrat[ware] <= (int)(0.35 * lager + 0.5), $"{ware}: {vorrat[ware]} von {lager}");
            }
            Pruefe.Gleich(1, vorrat.Values.Distinct().Count()); // ein Anteil für alle Waren (leeres Lager)
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("glaubt, dass es schlecht wird")), string.Join(" | ", ki.Entscheidungen));
            // Der Karren holt die Grundnahrung zur Marktzeit und hält sie zurück; die Zutaten füllt der Verwalter bis zum Vorrat auf.
            for (var i = 0; i < 10 * 60; i++) sim.Tick();
            var gerste = t.ImLager("Gerste") + t.Karren.Sum(c => c.Ladung.GetValueOrDefault("Gerste"));
            Pruefe.Wahr(gerste > 0, "der Karren hat Gerste geholt");
            Pruefe.Wahr(t.HaeltZurueck("Gerste"), "die Gerste wird zurückgehalten");
            Pruefe.Wahr(ki.Vorhalten(t, t.Zutaten.Keys.First()) >= vorrat[t.Zutaten.Keys.First()], "der Verwalter hält die Zutat vor");
        }),

        ("KI-Vorrat: Nie mehr, als das Haus bezahlen kann – mit 60 Gulden nur ein paar Stück, ohne Geld nichts", () =>
        {
            foreach (var geld in new[] { 60.0, 0.0 })
            {
                var (_, w, k, ki) = KiHaus(Immer.Value, Minute(1, 4), geld, tischlerei: "Tischlerei Arm");
                var t = w.Betriebe.Single();
                var vorher = geld;
                var plan = ki.Vorrat.GetValueOrDefault("Tischlerei Arm") ?? new SortedDictionary<string, int>();
                var kosten = plan.Sum(x => t.Zutaten.ContainsKey(x.Key) ? w.Markt[x.Key].Kaufen(x.Value) : w.HandKaufpreis(k, x.Key, x.Value));
                Pruefe.Wahr(kosten <= Math.Max(0, vorher) + 1e-9, $"Geld {vorher}: Vorrat kostet {kosten}");
                if (vorher <= 0) Pruefe.Gleich(0, plan.Count);
                else Pruefe.Wahr(plan.Count > 0, "mit etwas Geld ein wenig Vorrat");
            }
        }),

        ("KI-Vorrat: Beim nächsten Wurf ohne Treffer gibt das Haus den Vorrat frei (die Gerste ist nicht mehr zurückgehalten); der Spielstand behält den Vorrat", () =>
        {
            var daten = Echt.Value;
            var (_, w0, _, ki0) = KiHaus(daten, Minute(0, 5));
            var d = daten.Ki!.Vorrat!;
            // ein Jahr mit Treffer, danach eines ohne (der Wurf hängt nur von Startwert, Haus und Jahr ab; Anzeichen erhöhen nur die Chance)
            var jahr = Enumerable.Range(1, 400).First(j =>
            {
                w0.Uhr = new Spielzeit(Minute(j, 5));
                var a = ki0.VorratsAnzeichen;
                w0.Uhr = new Spielzeit(Minute(j + 1, 5));
                var b = ki0.VorratsAnzeichen;
                return ki0.Vorratswurf(d, Spielzeit.StartJahr + j, a).Treffer && !ki0.Vorratswurf(d, Spielzeit.StartJahr + j + 1, b).Treffer;
            });
            var (_, w, k, ki) = KiHaus(daten, Minute(jahr, 4), tischlerei: "Tischlerei Vorrat");
            var t = w.Betriebe.Single();
            Pruefe.Wahr(ki.Vorrat.ContainsKey("Tischlerei Vorrat"), $"Treffer im Jahr {jahr}");
            Pruefe.Wahr(w.Ausfuehren(k, new WareZurueckhalten("Tischlerei Vorrat", "Gerste", true)).Erfolg, "zurückhalten");

            // Spielstand: Vorrat und offener Einkauf kommen zurück
            var stand = ki.Sichern(0);
            var k2 = w.KontoEroeffnen("Haus Geladen", startvermoegen: 100);
            var ki2 = w.KiAnmelden(k2);
            ki2.Wiederherstellen(stand);
            Pruefe.Gleich(string.Join(";", ki.Vorrat.Select(x => x.Key + ":" + string.Join(",", x.Value))),
                string.Join(";", ki2.Vorrat.Select(x => x.Key + ":" + string.Join(",", x.Value))));

            ki.Jahresende();
            w.Uhr = new Spielzeit(Minute(jahr + 1, 4));
            ki.Stunde();
            Pruefe.Gleich(0, ki.Vorrat.Count);
            Pruefe.Wahr(!t.HaeltZurueck("Gerste"), "die Gerste ist freigegeben");
            Pruefe.Gleich((int?)null, ki.Vorhalten(t, t.Zutaten.Keys.First())); // früh am Morgen auch kein Wintervorrat
        }),

        ("KI-Vorrat: Ohne Abschnitt ki.vorrat würfelt die KI nicht und hält nichts vor", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(OhneVorrat(Json.Value));
            Pruefe.Gleich(null, daten.Ki!.Vorrat);
            var (_, _, _, ki) = KiHaus(daten, Minute(1, 4), tischlerei: "Tischlerei Vorrat");
            Pruefe.Gleich(0, ki.Vorrat.Count);
        }),

        ("KI-Vorrat: Kein Code im Ordner Ki/ fragt eine Ansage ab (Ernte.Angesagt, Lagen.Angesagt, AngesagtIm)", () =>
        {
            var ki = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!, "src", "Reichsgilde.Core", "Ki");
            var funde = new List<string>();
            foreach (var datei in Directory.EnumerateFiles(ki, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var zeilen = File.ReadAllLines(datei);
                for (var i = 0; i < zeilen.Length; i++)
                {
                    var code = zeilen[i].Split("//")[0]; // Kommentare zählen nicht
                    if (code.Contains("Angesagt", StringComparison.Ordinal)) funde.Add($"{Path.GetFileName(datei)}:{i + 1}");
                }
            }
            Pruefe.Wahr(Directory.EnumerateFiles(ki, "*.cs").Count() > 10, "Ordner Ki/ gefunden");
            Pruefe.Gleich("", string.Join(", ", funde));
        }),
    };
}
