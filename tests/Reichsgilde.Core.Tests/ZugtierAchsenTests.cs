using System.Text.Json;

namespace Reichsgilde.Core.Tests;

/// <summary>Die Drehachsen der Kurvenhaltung in zugtiere.json sind keine Nullachsen (Zugtierdaten normiert sie beim Laden, Godot verlangt Länge 1).</summary>
public static class ZugtierAchsenTests
{
    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Zugtiere: Kurvenachsen sind keine Nullachsen", () =>
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "game", "daten", "zugtiere.json"))) dir = Path.GetDirectoryName(dir);
            if (dir == null) throw new Exception("zugtiere.json nicht gefunden");
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "game", "daten", "zugtiere.json")));
            foreach (var art in doc.RootElement.EnumerateObject())
            {
                if (art.Value.ValueKind != JsonValueKind.Object || !art.Value.TryGetProperty("kurve", out var ku)) continue;
                foreach (var e in ku.EnumerateArray())
                {
                    var a = e.GetProperty("achse").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                    if (a.Length != 3 || a.Sum(x => x * x) < 1e-6) throw new Exception($"Nullachse bei {e.GetProperty("knochen").GetString()}");
                }
            }
        });
    }
}
