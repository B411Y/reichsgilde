namespace Reichsgilde;

/// <summary>
/// Werte des Brandschadens am Dach und der Strohkanten ohne Godot (für <c>NeubauBrandTests</c>). Bis zu den Steinebenen kopierte der
/// Brand das benannte Grundmaterial (StandardMaterial3D mit den Trim-Texturen) und tönte dessen Albedofarbe; die neuen Häuser nehmen
/// stattdessen eine Kopie ihres Trim-Materials (<c>building_trim.gdshader</c>, Uniform <c>russ_tint</c>), damit kein Material die
/// Blätter als einzelne Texturen lädt. Die Zahlen sind dieselben wie in <c>Stadtansicht.RussStoff</c>, das sie von hier liest.
/// </summary>
public static class NeubauBrand
{
    /// <summary>Wie weit die Farbe zum Ruß gezogen wird: Kanten und First 0,6, Stroh 0,55, übrige Dächer 0,3.</summary>
    public static float RussAnteil(bool kante, bool stroh) => kante ? 0.6f : stroh ? 0.55f : 0.3f;

    /// <summary>Die Rußschicht (zweiter Durchgang) liegt nicht auf Kanten und First.</summary>
    public static bool MitRussSchicht(bool kante) => !kante;

    /// <summary>Ruß glänzt nicht: Rauheit des Grundmaterials mindestens 0,92 (das Grundmaterial der Neubauten hat 1, bleibt also 1).</summary>
    public static float Rauheit(float grund) => System.Math.Max(grund, 0.92f);

    /// <summary>Tönung der Farbe: Weiß (die Albedofarbe des Grundmaterials) zum Ruß hin, je Kanal (sRGB wie <c>Color.Lerp</c>).</summary>
    public static (float R, float G, float B) RussTint((float R, float G, float B) russ, float anteil) =>
        (1f + (russ.R - 1f) * anteil, 1f + (russ.G - 1f) * anteil, 1f + (russ.B - 1f) * anteil);

    /// <summary>Alpha Scissor der Strohkanten: 0,5; im Pixel-Look 1,01 (die Fransen zerfielen in groben Bildpunkten zu schwarzen Punkten).</summary>
    public static float KantenSchwelle(bool pixelLook) => pixelLook ? 1.01f : 0.5f;
}
