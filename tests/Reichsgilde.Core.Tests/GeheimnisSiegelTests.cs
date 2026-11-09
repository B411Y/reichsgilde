using Reichsgilde.Core.Geheimnisse;

namespace Reichsgilde.Core.Tests;

/// <summary>Für jede Geheimnisart gibt es ein Siegel (Medaille, 128 px) unter game/assets/oberflaeche/geheimnisse/ – sonst zeigt das Charakterfenster nur Text.</summary>
static class GeheimnisSiegelTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Geheimnisse: für jede Art gibt es ein Siegel als PNG mit Importdatei", () =>
        {
            var ordner = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(Datenpfad.Wirtschaft()))!, "..", "assets", "oberflaeche", "geheimnisse");
            foreach (var art in Enum.GetValues<Geheimnisart>())
            {
                var datei = Path.Combine(ordner, art.ToString().ToLowerInvariant() + ".png");
                Pruefe.Wahr(File.Exists(datei), $"Siegel für {art}: {datei}");
                Pruefe.Wahr(File.Exists(datei + ".import"), $"Importdatei für {art}");
                var kopf = File.ReadAllBytes(datei).Take(8).ToArray();
                Pruefe.Wahr(kopf.Length == 8 && kopf[1] == (byte)'P' && kopf[2] == (byte)'N' && kopf[3] == (byte)'G', $"{art}: gültiges PNG");
            }
        }),
    };
}
