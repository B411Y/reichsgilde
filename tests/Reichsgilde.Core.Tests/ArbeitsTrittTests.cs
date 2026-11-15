using Reichsgilde;
using Reichsgilde.Core;

/// <summary>
/// Tritt und Standort der kleinen Völker am Arbeitsplatz (Darstellung, <c>game/scripts/ArbeitsTritt.cs</c>, hier mitübersetzt): Zwerge
/// und Halblinge bekommen je Volk, Geschlecht und Arbeitsclip ihren Versatz aus der Tabelle, Menschen, Elben, Orks und Oger nie.
/// </summary>
static class ArbeitsTrittTests
{
    const string Tabelle = """
    {
     "halblingin": { "hocker_m": { "Werkbank": 0.12, "Destille": 0.0 }, "clips": { "Haemmern": { "moebel": "Werkbank", "vor_m": 0.11, "seitlich_m": 0.02 },
                     "Hobeln": { "moebel": "Werkbank", "vor_m": 0.16, "seitlich_m": 0.0 }, "Destillieren": { "moebel": "Destille", "vor_m": 0.1, "seitlich_m": 0.0 } } },
     "zwerg": { "hocker_m": { "Werkbank": 0.0 }, "clips": { "Haemmern": { "moebel": "Werkbank", "vor_m": 0.06, "seitlich_m": 0.0 } } }
    }
    """;

    static string DatenPfad()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "game", "daten", "arbeitstritt.json"))) d = d.Parent;
        return d is null ? "" : Path.Combine(d.FullName, "game", "daten", "arbeitstritt.json");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Arbeitstritt: kleine Völker bekommen Tritt und Standort je Clip, Gegenprobe: Mensch, Elb, Ork, Oger nie, unbekannter Clip nie", () =>
        {
            var t = ArbeitsTritt.Laden(Tabelle);
            var l = t.Lage(Volk.Halbling, true, "Haemmern");
            Pruefe.Ungefaehr(0.11, l.Vor, 1e-6);
            Pruefe.Ungefaehr(0.02, l.Seitlich, 1e-6);
            Pruefe.Ungefaehr(0.12, l.Hoch, 1e-6);
            Pruefe.Wahr(l.MitTritt, "Halblingin hämmert auf dem Hocker");
            // der Hocker ist fest: bei jedem Arbeitsclip dieselbe Höhe, nur der Standort ändert sich
            Pruefe.Ungefaehr(0.12, t.Lage(Volk.Halbling, true, "Hobeln").Hoch, 1e-6);
            Pruefe.Ungefaehr(0.16, t.Lage(Volk.Halbling, true, "Hobeln").Vor, 1e-6);
            Pruefe.Gleich("Tritt_Werkbank", l.Netz);
            Pruefe.Wahr(!t.Lage(Volk.Halbling, true, "Destillieren").MitTritt && t.Lage(Volk.Halbling, true, "Destillieren").Netz is null, "an der Destille kein Hocker");
            var z = t.Lage(Volk.Zwerg, false, "Haemmern");
            Pruefe.Wahr(!z.MitTritt && z.Vor > 0f, "Zwerg: näher, kein Tritt");
            foreach (var v in new[] { Volk.Mensch, Volk.Elb, Volk.Ork, Volk.Oger })
                foreach (var frau in new[] { false, true })
                    Pruefe.Gleich(default(TrittLage), t.Lage(v, frau, "Haemmern"), $"{v} {frau}");
            Pruefe.Gleich(default(TrittLage), t.Lage(Volk.Halbling, true, "Gehen"), "kein Arbeitsclip");
            Pruefe.Gleich(default(TrittLage), t.Lage(Volk.Halbling, false, "Haemmern"), "Figur nicht in der Tabelle");
        }),
        ("Arbeitstritt: die Tabelle im Spiel (game/daten/arbeitstritt.json) hat die vier kleinen Figuren; je Figur und Möbel EINE Hockerhöhe, Werte in sinnvollen Grenzen", () =>
        {
            var pfad = DatenPfad();
            Pruefe.Wahr(pfad != "", "arbeitstritt.json gefunden");
            var t = ArbeitsTritt.Laden(File.ReadAllText(pfad));
            foreach (var f in new[] { "zwerg", "zwergin", "halbling", "halblingin" })
                Pruefe.Wahr(t.Hocker.ContainsKey((f, "Werkbank")), $"{f} in der Tabelle");
            var clips = new[] { "Haemmern", "Blasebalg", "Schleifen", "Hobeln", "Saegen", "Drechseln", "Gatter", "Behauen", "Bildhauen", "Moersern",
                                "Ruehren", "Destillieren", "Mischen", "Pressen", "Zapfen", "Kerzenziehen", "Schoepfen", "Knacken", "Wiegen" };
            foreach (var (volk, frau) in new[] { (Volk.Zwerg, false), (Volk.Zwerg, true), (Volk.Halbling, false), (Volk.Halbling, true) })
            {
                // der Hocker ist fest: je Möbel genau eine Höhe, gleich der Höhe des Hocker-Netzes
                foreach (var gruppe in clips.GroupBy(c => t.Lage(volk, frau, c).Moebel))
                {
                    var hoehen = gruppe.Select(c => t.Lage(volk, frau, c).Hoch).Distinct().ToList();
                    Pruefe.Gleich(1, hoehen.Count, $"{volk} {frau} {gruppe.Key}: eine feste Hockerhöhe");
                    Pruefe.Wahr(hoehen[0] is >= 0f and < 0.6f, $"{volk} {frau}: Höhe {hoehen[0]}");
                    Pruefe.Ungefaehr(t.Hocker[(ArbeitsTritt.Figur(volk, frau)!, gruppe.Key!)], hoehen[0], 1e-6);
                }
                foreach (var c in clips)
                    Pruefe.Wahr(t.Lage(volk, frau, c).Vor is > -0.1f and < 0.4f, $"{volk} {frau} {c}: {t.Lage(volk, frau, c)}");
            }
        }),
        ("Arbeitstritt: Ergonomie – auf ihrem Hocker arbeiten die kleinen Völker im Mittel aller Arbeitsclips unter der Schulter, wie der Mensch (Gegenprobe aus der Tabelle)", () =>
        {
            using var dok = System.Text.Json.JsonDocument.Parse(File.ReadAllText(DatenPfad()));
            foreach (var figur in dok.RootElement.EnumerateObject())
            {
                var klein = figur.Value.GetProperty("clips").EnumerateObject().Select(c => c.Value.GetProperty("faust_ueber_schulter_mittel_m").GetSingle()).ToList();
                var mensch = figur.Value.GetProperty("clips").EnumerateObject().Select(c => c.Value.GetProperty("mensch_faust_ueber_schulter_m").GetSingle()).ToList();
                Pruefe.Wahr(klein.Average() < 0f, $"{figur.Name}: Faust im Mittel {klein.Average() * 100:0} cm zur Schulter");
                Pruefe.Wahr(mensch.Average() < 0f, $"Mensch: Faust im Mittel {mensch.Average() * 100:0} cm zur Schulter");
                // die einzelnen Ausnahmen (Bildhauen, Blasebalg am Hebel …) stehen im Bericht; hier: kein Clip mehr als 20 cm über der Schulter im Mittel
                Pruefe.Wahr(klein.Max() < 0.2f, $"{figur.Name}: höchstes Mittel {klein.Max() * 100:0} cm über der Schulter");
            }
        }),
    };
}
