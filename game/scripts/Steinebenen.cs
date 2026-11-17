using System.Text.RegularExpressions;
using Reichsgilde.Core;

namespace Reichsgilde;

/// <summary>
/// Steinebenen je Volk (Nutzer, 15.11.2026): Die Trim-Sheets aller Außenhäuser liegen in einem Texture2DArray
/// (<c>haeuser_ebenen_*.png</c>, gebaut von <c>tools/modelle/neubau/trimsheet_ebenen.py</c>), eine Ebene je Volk und Stufe mit
/// gleichem Bandlayout; die AO-Atlanten der Gruppen in einem zweiten (<c>haeuser_ao_ebenen.png</c>). <c>building_trim.gdshader</c>
/// wählt die Ebene über die Instanz-Uniformen <c>ebene</c> und <c>ao_ebene</c>; so teilen sich alle Häuser aller Völker ein Material.
/// Hier steht die Zuordnung ohne Godot (für <c>SteinebenenTests</c>); die Reihenfolge ist dieselbe wie im Bauskript und in der
/// Tabelle <c>STEIN_EBENE</c> des Shaders.
/// </summary>
public static class Steinebenen
{
    /// <summary>Zwei v-Bereiche [A0, A1) und [B0, B1) der Steinbänder (Stein-Mip im Pixel-Look); −1 = kein Bereich.</summary>
    public readonly record struct Baender(float A0, float A1, float B0, float B1)
    {
        public bool Enthaelt(float v) => (v >= A0 && v < A1) || (v >= B0 && v < B1);
    }

    static readonly Baender Bruchstein = new(0.85f, 0.95f, -1f, -1f);
    static readonly Baender PutzUndBruchstein = new(0.50f, 0.70f, 0.85f, 0.95f);

    /// <summary>Die Ebenen des Häuser-Arrays: Name, Steinbänder. Index = Wert der Instanz-Uniform <c>ebene</c>.</summary>
    public static readonly (string Name, Baender Stein)[] Haeuser =
    {
        ("mensch_s1", Bruchstein),          // Sockel aus K4 (PavingStones142)
        ("mensch_s2", Bruchstein),          // Mauer aus K1 (castle_wall_varriation)
        ("mensch_s3", PutzUndBruchstein),   // Werkstein im Putzband, Mauer aus K1
        ("zwerg_s1", PutzUndBruchstein),    // alter dunkler Stein: dunkler Quader (Referenzstein) im Putzband, dunkler Bruchstein (K3 zurückgenommen)
        ("zwerg_s2", PutzUndBruchstein),
        ("zwerg_s3", PutzUndBruchstein),    // dunkler Quader, im Bruchsteinband heller Haustein K2 (large_sandstone_blocks_01) für Gewände, Giebel, Eckquader
        ("elb_s1", Bruchstein),             // Stufe 1 (trim_elb): Putz bleibt Putz (Kunstwerk), Haustein K2 (large_sandstone_blocks_01) im Bruchsteinband
        ("halbling", PutzUndBruchstein),
    };

    /// <summary>Die Gruppen des AO-Arrays (Index = <c>ao_ebene</c>); Bürgerhaus und Steinhaus haben je Volk einen eigenen Atlas (größere Zellen),
    /// die Elben (Stufe 1) einen mit Zellen von 1024 Bildpunkten.</summary>
    public static readonly string[] AoGruppen = { "wohnhaus_mensch", "wohnhaus_zwerg", "wohnhaus_mensch_s2", "wohnhaus_mensch_s3", "wohnhaus_zwerg_s2", "wohnhaus_zwerg_s3", "wohnhaus_elb" };

    /// <summary>Die Ebenen des Innen-Arrays <c>innen_ebenen_*.png</c>: die Innenblätter (Stufe 1, Stufen 2/3, Räume) der Menschen und der
    /// Zwerge (Steinwand und Pflaster statt Lehm und Dielen) und der Elben (Stufe 1: Kalkputz und Dielen), gleiches Layout.</summary>
    public static readonly string[] InnenBlaetter =
        { "innen_trim", "innen_trim_s23", "raeume_innen_trim", "innen_trim_zwerg", "innen_trim_s23_zwerg", "raeume_innen_trim_zwerg", "innen_trim_elb" };

    /// <summary>Die AO-Atlanten der Innen-Gruppen (Array <c>innen_ao_ebenen.png</c>, alle auf 2048 gebracht).</summary>
    public static readonly string[] InnenAoGruppen =
    {
        "wohnhaus_mensch_innen", "wohnhaus_mensch_dachraeume_innen", "wohnhaus_mensch_raeume_innen",
        "wohnhaus_zwerg_innen", "wohnhaus_zwerg_dachraeume_innen", "wohnhaus_zwerg_raeume_innen", "wohnhaus_elb_innen",
    };

    /// <summary>Ebene und AO-Ebene eines Innenmodells aus Innenblatt und Innengruppe; unbekannte Namen fallen auf Ebene 0.</summary>
    public static (int Ebene, int AoEbene) InnenAusBlatt(string blatt, string gruppe)
        => (System.Math.Max(0, System.Array.IndexOf(InnenBlaetter, blatt)), System.Math.Max(0, System.Array.IndexOf(InnenAoGruppen, gruppe)));

    /// <summary>Ebene eines Volkes in einer Stufe (1–3). Orks und Oger haben keine eigenen Häuser und wohnen wie Menschen;
    /// Elben haben die Ebene ihrer Stufe 1 (Stufen 2 und 3 sind noch nicht gebaut, dort stehen Menschenhäuser), Halblinge eine Platzhalter-Ebene.</summary>
    public static int Ebene(Volk volk, int stufe)
    {
        var s = System.Math.Clamp(stufe, 1, 3) - 1;
        return volk switch
        {
            Volk.Zwerg => 3 + s,
            Volk.Elb => 6,
            Volk.Halbling => 7,
            _ => s,
        };
    }

    /// <summary>Ebene und AO-Ebene aus dem Dateinamen eines Außenhauses (<c>wohnhaus_&lt;volk&gt;_s&lt;stufe&gt;_g&lt;form&gt;</c>);
    /// null für Dateien, die nicht ins Häuser-Array gehören (Innenräume, andere Modelle).</summary>
    public static (int Ebene, int AoEbene)? AusDatei(string datei)
    {
        var name = datei[(datei.LastIndexOf('/') + 1)..];
        var m = Regex.Match(name, @"^(wohnhaus_(mensch|zwerg|elb|halbling))_s(\d+)_g\d+$");
        if (!m.Success) return null;
        var volk = m.Groups[2].Value switch { "zwerg" => Volk.Zwerg, "elb" => Volk.Elb, "halbling" => Volk.Halbling, _ => Volk.Mensch };
        var stufe = int.Parse(m.Groups[3].Value);
        // Bürgerhaus und Steinhaus haben je einen eigenen AO-Atlas (größere Zellen); ohne eigenen Atlas gilt der der Gruppe
        var gruppe = m.Groups[1].Value + (stufe >= 2 ? $"_s{System.Math.Min(stufe, 3)}" : "");
        var ao = System.Array.IndexOf(AoGruppen, gruppe);
        if (ao < 0) ao = System.Array.IndexOf(AoGruppen, m.Groups[1].Value);
        return (Ebene(volk, stufe), ao < 0 ? 0 : ao);
    }
}
