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
/// „Wirtschaft“). Die Sollwerte sind auf main 880e3387 gerechnet (Probe mit demselben Ablauf, 1.11.2026).
/// </summary>
static class AlterSpielstandSollwertTests
{
    static readonly string[] Sollwerte =
    {
        "32479e9e70fcda4cf05abb40311ac46dd330c714efe2169b0242c782276dbdf2",
        "258c8b4f5f6da13159dc3f1b34cb38cb0422e17d0fcb5ef5df38aae68a59f611",
        "b579efb56425e48507072f35594c6fd51b585403b04b6663cb8d61cc396618ab",
    };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Alter Spielstand (Greybox, Herbst 1406, Datenversion 55): drei Jahre weiter bitgleich mit dem Kern auf main 880e3387 (SHA-256 ohne Daten und Karte)", () =>
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
                    if (schluessel.Equals("daten", StringComparison.OrdinalIgnoreCase) || schluessel.Equals("karte", StringComparison.OrdinalIgnoreCase))
                        n.Remove(schluessel);
                var ist = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(n.ToJsonString()))).ToLowerInvariant();
                Pruefe.Gleich(Sollwerte[jahr], ist, $"Jahr {jahr + 1} nach dem Laden");
            }
        }),
    };
}
