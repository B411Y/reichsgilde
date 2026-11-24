using System.Text.Json;

namespace Reichsgilde.Core.Tests;

/// <summary>Die Drehachsen der Kurvenhaltung in zugtiere.json (unter tiere.&lt;art&gt;.kurve) sind keine Nullachsen
/// (Zugtierdaten normiert sie beim Laden, Godot verlangt Länge 1).</summary>
public static class ZugtierAchsenTests
{
    /// <summary>Prüft alle Kurvenachsen unter „tiere“ und gibt zurück, wie viele es sind; wirft bei einer Nullachse.</summary>
    static int AchsenPruefen(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var anzahl = 0;
        foreach (var art in doc.RootElement.GetProperty("tiere").EnumerateObject())
        {
            if (art.Value.ValueKind != JsonValueKind.Object || !art.Value.TryGetProperty("kurve", out var ku)) continue;
            foreach (var e in ku.EnumerateArray())
            {
                var a = e.GetProperty("achse").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                if (a.Length != 3 || a.Sum(x => x * x) < 1e-6) throw new Exception($"Nullachse bei {art.Name}/{e.GetProperty("knochen").GetString()}");
                anzahl++;
            }
        }
        return anzahl;
    }

    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Zugtiere: Kurvenachsen sind keine Nullachsen", () =>
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "game", "daten", "zugtiere.json"))) dir = Path.GetDirectoryName(dir);
            if (dir == null) throw new Exception("zugtiere.json nicht gefunden");
            var anzahl = AchsenPruefen(File.ReadAllText(Path.Combine(dir, "game", "daten", "zugtiere.json")));
            Pruefe.Wahr(anzahl > 0, "zugtiere.json hat Kurvenachsen (sonst prüft der Test nichts)");
        });

        yield return ("Zugtiere: Gegenprobe – eine Nullachse fällt auf", () =>
        {
            const string gut = """{ "tiere": { "pferd": { "kurve": [ { "knochen": "Hals", "achse": [0, 1, 0], "grad": 6 } ] } } }""";
            const string null_ = """{ "tiere": { "pferd": { "kurve": [ { "knochen": "Hals", "achse": [0, 0, 0], "grad": 6 } ] } } }""";
            Pruefe.Gleich(1, AchsenPruefen(gut));
            Pruefe.WirftMitMeldung(() => AchsenPruefen(null_), "Nullachse bei pferd/Hals");
        });
    }
}
