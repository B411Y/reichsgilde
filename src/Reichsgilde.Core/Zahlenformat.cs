using System.Globalization;

namespace Reichsgilde.Core;

/// <summary>
/// Deutsches Zahlenformat für alle Anzeigen: 4.000 Gulden, 12,6 Gulden.
///
/// Warum nicht einfach die Systemeinstellung? Die hängt vom Rechner ab (ein englisches
/// Windows zeigt „4,000"), und exportierte Godot-Spiele laufen teils ohne Länderdaten.
/// Ein fest eingebautes Format sieht auf Windows und Linux immer gleich aus.
///
/// Im Kern gilt das für jede Zahl, die zu Text wird (Meldungen, Protokolle): Texte stehen teils im Spielstand, und im Mehrspieler muss
/// der Spielstand auf jedem Rechner gleich sein – auch mit englischer oder deutscher Ländereinstellung (der Gleichheitstest prüft das).
/// Darum nie <c>{x:0.#}</c> oder <c>{x:P0}</c>, sondern <see cref="Knapp"/>, <see cref="Prozent"/> usw.
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

    /// <summary>Höchstens eine Nachkommastelle, ohne „,0“: 2.3 → „2,3“, 2.0 → „2“ (Dauer, Meter).</summary>
    public static string Knapp(double wert) => wert.ToString("#,0.#", Deutsch);

    /// <summary>Ein Anteil als ganze Prozent: 0.05 → „5 %“.</summary>
    public static string Prozent(double anteil) => anteil.ToString("P0", Deutsch);
}
