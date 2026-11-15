using Reichsgilde;

/// <summary>Brandschaden am Dach und Strohkanten der Neubauten: dieselben Werte wie vor den Steinebenen (damals im Grundmaterial).</summary>
static class NeubauBrandTests
{
    static string Shaderordner() => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft()))!, "assets", "shader");

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Neubau-Brand: Rußanteil, Rußschicht und Rauheit wie bisher (Kante 0,6, Stroh 0,55, Dach 0,3, Rauheit mindestens 0,92)", () =>
        {
            // Werte von RussStoff vor dem Umbau (Stadtansicht.Feuer.cs bis Commit a81a01ea): kante ? 0.6f : stroh ? 0.55f : 0.3f; Max(Rauheit, 0.92f); Schicht nur ohne Kante
            Pruefe.Gleich(0.6f, NeubauBrand.RussAnteil(kante: true, stroh: true));
            Pruefe.Gleich(0.6f, NeubauBrand.RussAnteil(kante: true, stroh: false));
            Pruefe.Gleich(0.55f, NeubauBrand.RussAnteil(kante: false, stroh: true));
            Pruefe.Gleich(0.3f, NeubauBrand.RussAnteil(kante: false, stroh: false));
            Pruefe.Wahr(NeubauBrand.MitRussSchicht(false) && !NeubauBrand.MitRussSchicht(true), "Rußschicht nicht auf der Kante");
            Pruefe.Gleich(1f, NeubauBrand.Rauheit(1f));       // Grundmaterial der Neubauten: Rauheit 1, bleibt 1
            Pruefe.Gleich(0.92f, NeubauBrand.Rauheit(0.5f));
            // Gegenprobe: Stroh und Dach unterscheiden sich, die Tönung ist nicht Weiß
            Pruefe.Wahr(NeubauBrand.RussAnteil(false, true) != NeubauBrand.RussAnteil(false, false), "Gegenprobe Stroh/Dach");
            var russ = (0.16f, 0.14f, 0.13f);
            var t = NeubauBrand.RussTint(russ, 0.55f);
            Pruefe.Wahr(Math.Abs(t.R - (1f + (0.16f - 1f) * 0.55f)) < 1e-6 && t.G < 1f && t.B < 1f, "Tönung = Weiß.Lerp(Ruß, Anteil)");
            Pruefe.Wahr(NeubauBrand.RussTint(russ, 0f) == (1f, 1f, 1f), "ohne Brand Weiß");
        }),
        ("Neubau-Strohkanten: Alpha Scissor 0,5, im Pixel-Look 1,01; Kantenshader beidseitig mit demselben Rumpf", () =>
        {
            Pruefe.Gleich(0.5f, NeubauBrand.KantenSchwelle(false));
            Pruefe.Gleich(1.01f, NeubauBrand.KantenSchwelle(true));
            var kante = File.ReadAllText(Path.Combine(Shaderordner(), "building_trim_kante.gdshader"));
            var haupt = File.ReadAllText(Path.Combine(Shaderordner(), "building_trim.gdshader"));
            var rumpf = File.ReadAllText(Path.Combine(Shaderordner(), "building_trim.gdshaderinc"));
            Pruefe.Wahr(kante.Contains("render_mode cull_disabled") && kante.Contains("#define STROHKANTE")
                        && kante.Contains("building_trim.gdshaderinc"), "Kante: beidseitig, STROHKANTE, gleicher Rumpf");
            Pruefe.Wahr(haupt.Contains("building_trim.gdshaderinc") && !haupt.Contains("#define STROHKANTE") && !haupt.Contains("cull_disabled;"), "Hauptshader ohne Kante");
            Pruefe.Wahr(rumpf.Contains("ALPHA_SCISSOR_THRESHOLD = kante_schwelle") && rumpf.Contains("uniform vec3 russ_tint"), "Rumpf: Scissor und Rußtönung");
        }),
        ("Neubau-Grundmaterial ohne Texturen: kein Material lädt die Trim-Sheets mehr einzeln (Gegenprobe: Arrays werden geladen)", () =>
        {
            var code = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft()))!, "scripts", "Stadtansicht.Neubau.cs"));
            Pruefe.Wahr(!code.Contains("blatt + \"_albedo.png\"") &&!code.Contains("NeubauTextur<Texture2D>(blatt"), "keine einzelnen Blätter");
            Pruefe.Wahr(code.Contains("NeubauTextur<TextureLayered>(array + \"_ebenen_albedo.png\")"), "Gegenprobe: Arrays");
        }),
    };
}
