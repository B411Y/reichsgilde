using Reichsgilde;
using Reichsgilde.Core;

/// <summary>Efeu-Blattkarten (Stoff „Efeublatt“): Die Blattbilder liegen nur im Blatt der Halblinge Stufe 3; Karten anderer Häuser
/// (Elben, 21.11.2026) lesen deshalb diese Ebene über <c>efeu_ebene</c> im Shader.</summary>
static class EfeuEbeneTests
{
    static string Spielordner() => Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft()))!;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Efeublatt: Die Ebene der Blattbilder ist das Blatt der Halblinge Stufe 3 und nicht das der Elben", () =>
        {
            Pruefe.Gleich(Steinebenen.Ebene(Volk.Halbling, 3), Steinebenen.EfeuEbene);
            Pruefe.Gleich("halbling_s3", Steinebenen.Haeuser[Steinebenen.EfeuEbene].Name);
            for (var stufe = 1; stufe <= 3; stufe++)
                Pruefe.Wahr(Steinebenen.Ebene(Volk.Elb, stufe) != Steinebenen.EfeuEbene, $"Elb Stufe {stufe} hat eine eigene Ebene ohne Blattbilder");
        }),
        ("Efeublatt: Der Shader liest für Blattkarten efeu_ebene (ohne Angabe die Ebene des Hauses), das Spiel setzt sie", () =>
        {
            var shader = File.ReadAllText(Path.Combine(Spielordner(), "assets", "shader", "building_trim.gdshaderinc"));
            Pruefe.Wahr(shader.Contains("uniform int efeu_ebene = -1;"), "efeu_ebene im Shader, ohne Angabe -1");
            Pruefe.Wahr(shader.Contains("if (efeu_blatt && efeu_ebene >= 0) eb = efeu_ebene;"), "nur Blattkarten wechseln die Ebene");
            Pruefe.Wahr(!shader.Contains("clamp(ebene, 0, 11)"), "Gegenprobe: keine Tabelle liest mehr die Ebene des Hauses an efeu_ebene vorbei");
            var spiel = File.ReadAllText(Path.Combine(Spielordner(), "scripts", "Stadtansicht.Neubau.cs"));
            Pruefe.Wahr(spiel.Contains("SetShaderParameter(\"efeu_ebene\", Steinebenen.EfeuEbene)"), "Stadtansicht.Neubau setzt efeu_ebene");
        }),
    };
}
