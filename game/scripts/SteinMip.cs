namespace Reichsgilde;

/// <summary>
/// Stein-Mip im Pixel-Look (Nutzer, 15.11.2026): Auf den Steinbändern eines Trim-Sheets nimmt <c>building_trim.gdshader</c> einen kleineren
/// Mip-Zuschlag (<c>stein_mip</c>, Standard 1) als auf Putz, Holz, Stroh und Schiefer (<c>pixel_mip</c> 2,5), damit Lagen und Quader lesbar
/// bleiben. Hier stehen die Steinbänder je Blatt (v-Bereiche wie in <c>tools/modelle/neubau/trimsheet.py</c> bzw. <c>rathaus_trimsheet.py</c>)
/// und dieselbe Regel wie im Shader, ohne Godot – für <c>SteinMipTests</c>; das Spiel gibt die Bänder als Uniform <c>stein_baender</c> weiter.
/// </summary>
public static class SteinMip
{
    /// <summary>Zwei v-Bereiche [A0, A1) und [B0, B1); −1 = kein Bereich.</summary>
    public readonly record struct Baender(float A0, float A1, float B0, float B1)
    {
        public bool Enthaelt(float v) => (v >= A0 && v < A1) || (v >= B0 && v < B1);
    }

    /// <summary>Standard des Shaders: Band „bruchstein“ der Häuser.</summary>
    public static readonly Baender Haus = new(0.85f, 0.95f, -1f, -1f);

    /// <summary>Die Steinbänder eines Blatts (Dateiname ohne Endung, wie <c>NeubauBlatt</c> ihn liefert).</summary>
    public static Baender Von(string blatt) => blatt switch
    {
        "rathaus_trim" => new(0.60f, 0.70f, 0.85f, 0.95f),            // Quader, Bruchstein
        "rathaus_innen_trim" => new(0.48f, 0.68f, 0.68f, 0.83f),      // Quader + Bruchstein, Platten + Kerkermauer
        "innen_trim" => new(0.70f, 0.80f, -1f, -1f),                  // Bruchstein innen
        _ => Haus,                                                     // trim und die Stufenblätter der Häuser: Bruchstein 0,85–0,95
    };

    /// <summary>Mip-Zuschlag an der Stelle <paramref name="v"/> wie im Shader (ohne Pixel-Look 0).</summary>
    public static float Zuschlag(float v, Baender baender, bool pixelLook, float steinMip = 1f, float pixelMip = 2.5f) =>
        !pixelLook ? 0f : baender.Enthaelt(v) ? steinMip : pixelMip;
}
