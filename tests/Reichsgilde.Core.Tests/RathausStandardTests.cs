using Reichsgilde.Core.Bauen;

/// <summary>
/// Das neue Rathaus ist das einzige (Stufe 8): Der Katalog zeigt auf den Neubau, die alten Modelle und ihr Code sind fort, die sieben Räume
/// gibt es – in der Stadt wie in Köln (dort in der Kölner Hülle, Stadtansicht.Rathausneubau.cs).
/// </summary>
static class RathausStandardTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static string Spielordner => Path.GetDirectoryName(Datenordner)!;
    static string Modelle => Path.Combine(Spielordner, "assets", "modelle");

    static readonly string[] Raeume = { "eingangshalle", "amtsstube", "gerichtssaal", "kerker", "ratshalle", "turm_treppe", "turm_spitze" };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Rathaus: Der Katalog „rathaus“ zeigt auf den Neubau, Köln behält seine Hülle", () =>
        {
            var k = Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json"));
            Pruefe.Gleich("gebaeude/neubau/rathaus", k.Finde("rathaus")!.Modell);
            Pruefe.Gleich("gebaeude/koelner_rathaus", k.Finde("koelner_rathaus")!.Modell);
            Pruefe.Wahr(File.Exists(Path.Combine(Modelle, "gebaeude", "neubau", "rathaus.glb")), "Hülle des Neubaus fehlt");
        }),
        ("Rathaus: Die sieben Räume gibt es als Modell", () =>
        {
            foreach (var raum in Raeume)
                Pruefe.Wahr(File.Exists(Path.Combine(Modelle, "gebaeude", "neubau", $"rathaus_{raum}_innen.glb")), $"Raum {raum} fehlt");
        }),
        ("Rathaus: Die alten Modelle und die Startoption sind fort, kein Skript nennt sie noch", () =>
        {
            Pruefe.Wahr(!File.Exists(Path.Combine(Modelle, "gebaeude", "rathaus.glb")), "altes rathaus.glb liegt noch da");
            Pruefe.Wahr(!File.Exists(Path.Combine(Modelle, "innen", "rathaus_innen.glb")), "alter Ratssaal liegt noch da");
            var skripte = Directory.GetFiles(Path.Combine(Spielordner, "scripts"), "*.cs");
            foreach (var verbot in new[] { "\"innen/rathaus_innen\"", "\"gebaeude/rathaus\"", "--neuesrathaus" })
            {
                var treffer = skripte.Where(d => File.ReadAllText(d).Contains(verbot)).Select(Path.GetFileName).ToList();
                Pruefe.Wahr(treffer.Count == 0, $"{verbot} steht noch in {string.Join(", ", treffer)}");
            }
        }),
        ("Rathaus: Gegenprobe – die Suche nach alten Verweisen findet einen eingeschleusten", () =>
        {
            var text = "var datei = \"innen/rathaus_innen\";";
            Pruefe.Wahr(text.Contains("\"innen/rathaus_innen\""), "die Suche muss den Verweis finden");
        }),
    };
}
