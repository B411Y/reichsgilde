using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>Kartenwahl im Hauptmenü: das Kennzeichen „spielbar“ und der Kurztext einer Karte (Lesen, Schreiben, Rückgängig).</summary>
static class KartenwahlTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenentwurf Oeffnen(string kennung) => Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Kartenwahl: ohne Feld ist eine Karte nicht spielbar, die Datei bleibt beim Speichern unverändert", () =>
        {
            var json = File.ReadAllText(Datenpfad.Karte("messstadt"));
            var e = Oeffnen("messstadt");
            Pruefe.Wahr(!e.Spielbar && !e.ZuStadtkarte().Spielbar, "messstadt ist nicht spielbar");
            Pruefe.Wahr(!json.Contains("\"spielbar\""), "und die Datei sagt nichts dazu");
            Pruefe.Gleich(json.TrimEnd(), e.ZuJson().TrimEnd());
        }),

        ("Kartenwahl: spielbar setzen schreibt das Feld hinter die Vorlage, Rückgängig nimmt es weg, Lesen findet es wieder", () =>
        {
            var e = Oeffnen("messstadt");
            e.SpielbarSetzen(true);
            var json = e.ZuJson();
            Pruefe.Wahr(json.Contains("\"spielbar\": true") || json.Contains("\"spielbar\":true"), "Feld steht in der Datei");
            Pruefe.Wahr(json.IndexOf("\"spielbar\"", StringComparison.Ordinal) < json.IndexOf("\"gebaeude\"", StringComparison.Ordinal), "vor den Gebäuden");
            var neu = Kartenentwurf.AusJson(json, Katalog.Value);
            Pruefe.Wahr(neu.Spielbar && neu.ZuStadtkarte().Spielbar, "gelesen");
            e.Rueckgaengig();
            Pruefe.Wahr(!e.Spielbar && !e.ZuJson().Contains("\"spielbar\""), "zurückgenommen");
        }),

        ("Kartenwahl: Kurztext wird geschrieben und gelesen, leer entfernt ihn", () =>
        {
            var e = Oeffnen("messstadt");
            e.BeschreibungSetzen("Eine kleine Stadt am Fluss.");
            var karte = Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value).ZuStadtkarte();
            Pruefe.Gleich("Eine kleine Stadt am Fluss.", karte.Beschreibung);
            e.BeschreibungSetzen("");
            Pruefe.Wahr(!e.ZuJson().Contains("\"beschreibung\""), "leer: kein Feld");
        }),
    };
}
