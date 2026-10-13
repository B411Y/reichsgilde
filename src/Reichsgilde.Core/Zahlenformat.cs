using System.Globalization;

namespace Reichsgilde.Core;

/// <summary>
/// Deutsches Zahlenformat für alle Anzeigen: 4.000 Gulden, 12,6 Gulden.
///
/// Warum nicht einfach die Systemeinstellung? Die hängt vom Rechner ab (ein englisches
/// Windows zeigt „4,000"), und exportierte Godot-Spiele laufen teils ohne Länderdaten.
/// Ein fest eingebautes Format sieht auf Windows und Linux immer gleich aus.
/// </summary>
public static class Zahlenformat
{
    public static readonly NumberFormatInfo Deutsch = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        PercentDecimalSeparator = ",",
        PercentGroupSeparator = ".",
        NegativeSign = "−",
    };

    /// <summary>Ganze Zahl mit Tausenderpunkt: 4000 → „4.000".</summary>
    public static string Ganz(double wert) => wert.ToString("#,0", Deutsch);

    /// <summary>Eine Nachkommastelle: 12.63 → „12,6".</summary>
    public static string Kommazahl(double wert) => wert.ToString("#,0.0", Deutsch);

    /// <summary>Mit Vorzeichen: 627 → „+627", −120 → „−120".</summary>
    public static string MitVorzeichen(double wert) => wert.ToString("+#,0;−#,0;0", Deutsch);
}
