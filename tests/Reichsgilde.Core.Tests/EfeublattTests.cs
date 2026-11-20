using Reichsgilde;

/// <summary>Efeu der Halblinge (Stufe 3, Nutzer 19.11.2026): Blattkarten mit Alpha-Schnitt, die auch im Pixel-Look sichtbar bleiben.</summary>
static class EfeublattTests
{
    static string Spielordner() => Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft()))!;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Efeublatt: Alpha-Schnitt fest 0,5 (auch im Pixel-Look, anders als die Strohkanten), Alpha aus dem Efeu-Band ohne Pixel-Zuschlag, eigener Stoff im Spiel", () =>
        {
            var rumpf = File.ReadAllText(Path.Combine(Spielordner(), "assets", "shader", "building_trim.gdshaderinc"));
            var neubau = File.ReadAllText(Path.Combine(Spielordner(), "scripts", "Stadtansicht.Neubau.cs"));
            Pruefe.Wahr(rumpf.Contains("uniform bool efeu_blatt = false;"), "Uniform efeu_blatt (Standard aus)");
            Pruefe.Wahr(rumpf.Contains("if (efeu_blatt) {") && rumpf.Contains("ALPHA_SCISSOR_THRESHOLD = 0.5;"), "Efeublatt: Schwelle 0,5");
            Pruefe.Wahr(rumpf.Contains("band_lesen(albedo_tex, uvw, 0.0, teil_e, lod).a"), "Alpha aus dem Efeu-Band in der Grundstufe (lod, ohne grob)");
            Pruefe.Wahr(rumpf.Contains("&& !efeu) {"), "keine Halmstriche auf dem Efeu");
            Pruefe.Wahr(rumpf.Contains("if (efeu_blatt) house_ao = 1.0;"), "keine gebackene Verdeckung auf den Blattkarten");
            Pruefe.Wahr(neubau.Contains("case \"Efeublatt\":") && neubau.Contains("NeubauStoff(datei, kante: true, efeu: true)")
                        && neubau.Contains("SetShaderParameter(\"efeu_blatt\", true)"), "Stoff Efeublatt im Spiel: Kantenshader mit efeu_blatt");
            // Gegenprobe: die Strohkanten bleiben im Pixel-Look aus
            Pruefe.Gleich(1.01f, NeubauBrand.KantenSchwelle(true));
        }),
    };
}
