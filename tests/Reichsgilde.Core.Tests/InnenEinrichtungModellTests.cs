using System.Text;
using System.Text.Json;
using Reichsgilde.Core.Partie;

/// <summary>
/// Jedes Stück der Einrichtung, das der Kern verkauft (<c>werkstatt.einrichtung</c> je Beruf, <c>wohnen.einrichtung</c> des Wohnhauses), hat einen Knoten
/// <c>Einrichtung_&lt;Kennung&gt;</c> im Raummodell (<c>game/assets/modelle/innen/&lt;beruf&gt;_innen.glb</c>, Wohnhaus: <c>wohnstube_innen.glb</c>) – sonst kauft der Spieler etwas,
/// das im Raum nicht erscheint (PR „Innenräume – Personalstufen und neue Einrichtung“, Platzhalter). Liest nur die Knotennamen aus der Datei.
/// </summary>
static class InnenEinrichtungModellTests
{
    static string InnenOrdner() => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "..", "assets", "modelle", "innen"));

    /// <summary>Die Knotennamen einer .glb (JSON-Abschnitt am Anfang der Datei).</summary>
    static HashSet<string> Knoten(string datei)
    {
        var b = File.ReadAllBytes(datei);
        var laenge = BitConverter.ToInt32(b, 12);
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(b, 20, laenge));
        return json.RootElement.GetProperty("nodes").EnumerateArray()
            .Select(n => n.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "").ToHashSet();
    }

    static JsonElement Daten()
    {
        var text = File.ReadAllText(Datenpfad.Wirtschaft());
        return JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip }).RootElement;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Innenräume: Jedes Stück der Werkstatt-Einrichtung hat einen Knoten Einrichtung_<Kennung> im Raummodell seines Berufs", () =>
        {
            var einrichtung = Daten().GetProperty("werkstatt").GetProperty("einrichtung");
            var fehlt = new List<string>();
            var gezaehlt = 0;
            foreach (var beruf in einrichtung.EnumerateObject())
            {
                var datei = Path.Combine(InnenOrdner(), beruf.Name.ToLowerInvariant() + "_innen.glb");
                Pruefe.Wahr(File.Exists(datei), $"Raummodell fehlt: {datei}");
                var knoten = Knoten(datei);
                foreach (var stueck in beruf.Value.EnumerateArray())
                {
                    gezaehlt++;
                    var kennung = stueck.GetProperty("kennung").GetString()!;
                    if (!knoten.Contains("Einrichtung_" + kennung)) fehlt.Add($"{beruf.Name}: Einrichtung_{kennung}");
                }
            }
            Pruefe.Wahr(gezaehlt > 60, $"zu wenige Stücke gelesen: {gezaehlt}");
            Pruefe.Gleich("", string.Join(", ", fehlt));
        }),

        ("Innenräume: Jedes kaufbare Stück des Wohnhauses hat einen Knoten Einrichtung_<Kennung> in wohnstube_innen.glb", () =>
        {
            var knoten = Knoten(Path.Combine(InnenOrdner(), "wohnstube_innen.glb"));
            var fehlt = new List<string>();
            foreach (var stueck in Daten().GetProperty("wohnen").GetProperty("einrichtung").EnumerateArray())
            {
                if (stueck.TryGetProperty("vorhanden", out _)) continue; // Hausapotheke und Hauswächter sind Sache des Spiels, kein Stück im Raum
                var kennung = stueck.GetProperty("kennung").GetString()!;
                if (!knoten.Contains("Einrichtung_" + kennung)) fehlt.Add(kennung);
            }
            Pruefe.Gleich("", string.Join(", ", fehlt));
        }),
    };
}
