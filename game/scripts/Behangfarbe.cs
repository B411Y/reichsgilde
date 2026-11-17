using System.Globalization;

namespace Reichsgilde;

/// <summary>
/// Farbe des Wandbehangs in der Stube der Steinhäuser (Nutzer, 17.11.2026): an die Hausfarbe gebunden, aber als gedämpfter,
/// mittelalterlicher Farbton der Färberei (Krapp, Waid, Wau, Walnuss …) statt der kräftigen Farbe von Fahne und Wappen. Jede Farbe
/// der Spiel-Palette (<c>Partiebesetzung.Hausfarben</c>) hat ihren Ton; eine andere Farbe (Farbwähler der Kartenwerkstatt, Haus ohne
/// Besitzer) nimmt den Ton der nächstliegenden Palettenfarbe. Der Saum ist derselbe Ton, dunkler (<see cref="SaumFaktor"/>).
/// Die Tabelle steht auch in <c>docs/bereiche/darstellung.md</c> und im Spielbuch (<c>docs/spielbuch/gebaeude-betreten.md</c>).
/// Ohne Godot, damit <c>BehangfarbeTests</c> sie prüfen kann; im Spiel setzt <c>Stadtansicht.Neubau.cs</c> die Stoffe
/// „Behang“ und „Behangsaum“ der Innenmodelle.
/// </summary>
public static class Behangfarbe
{
    /// <summary>Eine Zeile der Tabelle: Hausfarbe der Palette, Name des Behangtons, Behangton (beide <c>#rrggbb</c>, sRGB).</summary>
    public readonly record struct Eintrag(string Hausfarbe, string Name, string Behang);

    /// <summary>Die Palette der Hausfarben (Reihenfolge wie <c>Partiebesetzung.Hausfarben</c>) und ihr Behangton.</summary>
    public static readonly Eintrag[] Tabelle =
    {
        new("#336bb8", "Waidblau", "#3b5876"),
        new("#b33329", "Krapprot", "#8e2a24"),
        new("#388c47", "Waugrün", "#4e6b3a"),
        new("#d9ad2e", "Resedagelb", "#b8953a"),
        new("#7a3b8f", "Orseillepurpur", "#5e3560"),
        new("#2a8c8a", "Grünspan", "#3d6e68"),
        new("#c4672a", "Krapporange", "#a85a2a"),
        new("#5c5c5c", "Eisengrau", "#484848"),
        new("#8c6239", "Walnussbraun", "#5e4128"),
        new("#b84d86", "Kermesrosa", "#8a3a5c"),
        new("#a88a3a", "Goldocker", "#8a7032"),
    };

    /// <summary>Der Saum ist der Behangton mal diesem Faktor (in sRGB).</summary>
    public const float SaumFaktor = 0.6f;

    /// <summary>Der Eintrag der Palettenfarbe, die <paramref name="r"/>, <paramref name="g"/>, <paramref name="b"/> (sRGB, 0–1) am nächsten liegt.</summary>
    public static Eintrag Zu(float r, float g, float b)
    {
        var bester = Tabelle[0];
        var abstand = float.MaxValue;
        foreach (var e in Tabelle)
        {
            var (er, eg, eb) = Lesen(e.Hausfarbe);
            var d = (er - r) * (er - r) + (eg - g) * (eg - g) + (eb - b) * (eb - b);
            if (d < abstand) { abstand = d; bester = e; }
        }
        return bester;
    }

    /// <summary>Behangton (sRGB, 0–1) zur Hausfarbe.</summary>
    public static (float R, float G, float B) Behang(float r, float g, float b) => Lesen(Zu(r, g, b).Behang);

    /// <summary>Saumton (sRGB, 0–1) zur Hausfarbe: der Behangton, dunkler.</summary>
    public static (float R, float G, float B) Saum(float r, float g, float b)
    {
        var (br, bg, bb) = Behang(r, g, b);
        return (br * SaumFaktor, bg * SaumFaktor, bb * SaumFaktor);
    }

    /// <summary><c>#rrggbb</c> als sRGB 0–1.</summary>
    public static (float R, float G, float B) Lesen(string hex)
    {
        float K(int i) => int.Parse(hex.AsSpan(1 + 2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f;
        return (K(0), K(1), K(2));
    }
}
