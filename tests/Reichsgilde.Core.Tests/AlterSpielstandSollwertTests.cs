using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Speichern;

/// <summary>
/// Alte Spielstände rechnen wie auf main (niedriger Befund der Prüfung von phase5/gehtempo, 1.11.2026): Ein Spielstand von vor Datenversion 65
/// (Greybox, Herbst 1406, Datenversion 55) bringt seine Daten mit und muss Jahr für Jahr genau so weiterlaufen wie mit dem Kern auf main – die
/// Fahrzeit nach Weglänge, die gemeinsame Strecke und die Marktplätze gelten nur mit <c>logistik.weglaenge</c>. Verglichen wird der Spielstand
/// ohne die Abschnitte „Daten“ und „Karte“ (die schreibt der neue Kern mit seinen neuen, leeren Feldern; die Simulation selbst steht in
/// „Wirtschaft“). Die Sollwerte sind auf main 880e3387 gerechnet (Probe mit demselben Ablauf, 1.11.2026). Ebenso ohne „Meldungen“ (Benachrichtigungen
/// Stufe 2, 6.11.2026): der Verlauf der Meldungen ist neu, die Simulation liest ihn nie – „Wirtschaft“ bleibt bitgleich. Seit dem 11.11.2026
/// (Bart der Zwerginnen) auch ohne das Erbgut der Personen (<c>Spielstandfelder.Erbgut</c>, die Simulation liest es nie): Die Sollwerte dafür
/// sind auf main 770c374a mit demselben Ablauf gerechnet, wo die Sollwerte von main 880e3387 mit Erbgut noch galten.
/// </summary>
static class AlterSpielstandSollwertTests
{
    static readonly string[] Sollwerte =
    {
        // Ohne das Erbgut der Personen (11.11.2026): gerechnet auf main 770c374a mit demselben Ablauf – dort mit Erbgut noch gleich den Sollwerten
        // von main 880e3387 (32479e9e…, 258c8b4f…, b579efb5…).
        "12b73856ad598920a90451b89c66e8d2ab0da36858ec1ca756f224bcb2940e99",
        "b08c5d4af791f5f211949d8bd405e4ea750375be37daad9dac7fba09905a4920",
        "9c9ad7e38a3883d47e1f67060ba1985eb674ed3ebc4947ac170e65054984d218",
    };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Alter Spielstand (Greybox, Herbst 1406, Datenversion 55): drei Jahre weiter bitgleich mit dem Kern auf main 880e3387 (SHA-256 ohne Daten, Karte, Meldungen und Erbgut; Sollwerte ohne Erbgut von main 770c374a)", () =>
        {
            var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
                "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
            using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
            var (sim, _) = Speicherung.Laden(new StreamReader(gz).ReadToEnd());
            Pruefe.Gleich(55, sim.Wirtschaft!.Daten.Version);
            for (var jahr = 0; jahr < Sollwerte.Length; jahr++)
            {
                var j = sim.Zeit.Jahr;
                while (sim.Zeit.Jahr == j) sim.Tick();
                var n = JsonNode.Parse(Speicherung.Speichern(sim, null, new DateTime(2026, 1, 1)))!.AsObject();
                foreach (var schluessel in n.Select(p => p.Key).ToList())
                    if (schluessel.Equals("daten", StringComparison.OrdinalIgnoreCase) || schluessel.Equals("karte", StringComparison.OrdinalIgnoreCase)
                        || schluessel.Equals("meldungen", StringComparison.OrdinalIgnoreCase))
                        n.Remove(schluessel);
                Spielstandfelder.OhneLeere(n, Spielstandfelder.Hafenziel); // neue Felder (Datenversion 73), solange sie nichts tragen
                Spielstandfelder.Ohne(n, Spielstandfelder.NurLaden); // nur fürs Laden (Datenversion 75), durchgehend ohne Wirkung
                Spielstandfelder.Ohne(n, Spielstandfelder.Erbgut); // Allele für den 3D-Kopf: die Simulation liest sie nie (Bart der Zwerginnen, 11.11.2026)
                var ist = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(n.ToJsonString()))).ToLowerInvariant();
                Pruefe.Gleich(Sollwerte[jahr], ist, $"Jahr {jahr + 1} nach dem Laden");
            }
        }),
    };
}
