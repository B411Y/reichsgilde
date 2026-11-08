using System.Text.Json.Nodes;

namespace Reichsgilde.Core.Tests;

/// <summary>Der Marktbrunnen im Modell des Marktplatzes (<c>marktplatz.glb</c>, gebaut von <c>tools/modelle/brunnen.py</c>): Netze, Stoffe, Platz für die Anker.</summary>
public static class MarktbrunnenTests
{
    private static JsonNode Modell()
    {
        var pfad = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft()))!, "assets", "modelle", "gebaeude", "marktplatz.glb");
        var bytes = File.ReadAllBytes(pfad);
        return JsonNode.Parse(System.Text.Encoding.UTF8.GetString(bytes, 20, BitConverter.ToInt32(bytes, 12)))!;
    }

    private static (double X, double Z) Ort(JsonNode knoten) =>
        knoten["translation"] is JsonArray t ? (t[0]!.GetValue<double>(), t[2]!.GetValue<double>()) : (0, 0);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Marktbrunnen: das Modell hat die Netze Brunnen, Trog, Wasser, Strahl und Eis; Wasser und Strahl tragen die Stoffe der Shader, höchstens vier Stoffe Stein", () =>
        {
            var wurzel = Modell();
            var knoten = wurzel["nodes"]!.AsArray().ToDictionary(n => n!["name"]!.GetValue<string>(), n => n!);
            var netze = wurzel["meshes"]!.AsArray();
            var stoffe = wurzel["materials"]!.AsArray().Select(m => m!["name"]!.GetValue<string>()).ToList();
            foreach (var name in new[] { "Brunnen", "Brunnen_Trog", "Brunnen_Wasser", "Brunnen_Strahl", "Brunnen_Eis", "Anker_Trog" })
                Pruefe.Wahr(knoten.ContainsKey(name), $"Knoten {name}");
            List<string> StoffeVon(string name) =>
                netze[knoten[name]["mesh"]!.GetValue<int>()]!["primitives"]!.AsArray().Select(p => stoffe[p!["material"]!.GetValue<int>()]).ToList();
            Pruefe.Wahr(StoffeVon("Brunnen_Wasser").SequenceEqual(new[] { "Brunnenwasser" }), "Wasser: ein Stoff Brunnenwasser");
            Pruefe.Wahr(StoffeVon("Brunnen_Strahl").SequenceEqual(new[] { "Brunnenstrahl" }), "Strahl: ein Stoff Brunnenstrahl");
            Pruefe.Wahr(StoffeVon("Brunnen_Trog").Count == 1, "Trog: ein Stoff");
            Pruefe.Wahr(StoffeVon("Brunnen").Count <= 4, "Brunnen: höchstens vier Stoffe – " + string.Join(", ", StoffeVon("Brunnen")));
            Pruefe.Wahr(StoffeVon("Brunnen_Eis").Count == 2, "Eis: zwei Stoffe (Eis, Schnee)");
            var shaderOrdner = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft()))!, "assets", "shader");
            foreach (var shader in new[] { "brunnenwasser.gdshader", "brunnenstrahl.gdshader" })
                Pruefe.Wahr(File.ReadAllText(Path.Combine(shaderOrdner, shader)).Contains("uniform int stufe"), $"{shader} folgt der Grafikstufe");
        }),
        ("Marktbrunnen: Anker, Stände, Bänke und Plauderrunden stehen nicht auf der Stufe (Umkreis 2,77 m) und nicht im Trog; die Brunnenanker haben Platz", () =>
        {
            var knoten = Modell()["nodes"]!.AsArray().ToDictionary(n => n!["name"]!.GetValue<string>(), n => n!);
            var trog = Ort(knoten["Anker_Trog"]);
            double Mitte((double X, double Z) o) => Math.Sqrt(o.X * o.X + o.Z * o.Z);
            double Trog((double X, double Z) o) => Math.Sqrt((o.X - trog.X) * (o.X - trog.X) + (o.Z - trog.Z) * (o.Z - trog.Z));
            Pruefe.Wahr(Math.Abs(Mitte(trog) - 3.35) < 0.01, $"Trog 3,35 m von der Mitte: {Mitte(trog):0.00}");
            foreach (var (name, n) in knoten.Where(k => k.Key.StartsWith("Anker_") && k.Key != "Anker_Trog"))
            {
                var o = Ort(n);
                var brunnen = name.StartsWith("Anker_Brunnen");
                Pruefe.Wahr(Mitte(o) >= (brunnen ? 3.2 : 3.4), $"{name}: {Mitte(o):0.00} m von der Mitte (Stufe 2,77 m)");
                Pruefe.Wahr(Trog(o) >= (brunnen ? 1.6 : 1.8), $"{name}: {Trog(o):0.00} m vom Trog");
            }
        }),
    };
}
