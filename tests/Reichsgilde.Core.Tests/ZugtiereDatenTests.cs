using System.Text.Json;

/// <summary>
/// Daten der Zugtiere (<c>game/daten/zugtiere.json</c>, gelesen von <c>Zugtierdaten.cs</c> im Spiel): Bilder, Dauer, Rumpfweg und Anlauf der Clips stimmen miteinander
/// und mit dem Tempo der Art überein. Gilt für Pferd (Modell „Horse“ von henrysteve973, Gehen 26 Bilder zu 24/s, 1,3 m/s) und Ochse.
/// </summary>
static class ZugtiereDatenTests
{
    static JsonElement Tiere()
    {
        var pfad = Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "zugtiere.json");
        return JsonDocument.Parse(File.ReadAllText(pfad)).RootElement.GetProperty("tiere");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Zugtiere: Bilder = Dauer · 24 + 1 in jedem Clip, Rumpfweg und Anlauf haben je Bild einen Eintrag", () =>
        {
            foreach (var art in Tiere().EnumerateObject())
                foreach (var k in art.Value.GetProperty("klips").EnumerateObject())
                {
                    var bilder = k.Value.GetProperty("bilder").GetInt32();
                    var dauer = k.Value.GetProperty("dauer").GetDouble();
                    Pruefe.Gleich((int)Math.Round(dauer * 24) + 1, bilder);
                    foreach (var feld in new[] { "weg", "anlauf" })
                        if (k.Value.TryGetProperty(feld, out var liste)) Pruefe.Gleich(bilder, liste.GetArrayLength());
                }
        }),
        ("Zugtiere: Gehen und Ziehen legen in der Schleife genau Tempo · Dauer zurück, in gleichen Schritten", () =>
        {
            foreach (var art in Tiere().EnumerateObject())
            {
                var tempo = art.Value.GetProperty("tempo").GetDouble();
                foreach (var name in new[] { "Gehen", "Ziehen" })
                {
                    var k = art.Value.GetProperty("klips").GetProperty(name);
                    var weg = k.GetProperty("weg").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                    var dauer = k.GetProperty("dauer").GetDouble();
                    Pruefe.Wahr(Math.Abs(weg[^1] - tempo * dauer) < 2e-3, $"{art.Name} {name}: Weg {weg[^1]} gegen {tempo * dauer}");
                    for (var i = 1; i < weg.Length; i++)
                        Pruefe.Wahr(Math.Abs(weg[i] - weg[i - 1] - tempo / 24) < 2e-4, $"{art.Name} {name}: Schritt {i}");
                }
            }
        }),
        ("Zugtiere: der Zugpunkt im Ziehen steht in beiden Feldern gleich (anker_lokal_godot = anker_ziehen_godot), das Pferd mit Geschirr 1,279 m hoch", () =>
        {
            foreach (var art in Tiere().EnumerateObject())
            {
                var a = art.Value.GetProperty("anker_lokal_godot").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                var b = art.Value.GetProperty("anker_ziehen_godot").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                Pruefe.Gleich(3, a.Length);
                for (var i = 0; i < 3; i++) Pruefe.Wahr(a[i] == b[i], $"{art.Name} Anker {i}");
            }
            var pferd = Tiere().GetProperty("pferd").GetProperty("anker_ziehen_godot").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            Pruefe.Wahr(Math.Abs(pferd[1] - 1.279) < 1e-6, "Zugpunkt des Pferds 1,279 m (karren.py: tip_z)");
        }),
    };
}
