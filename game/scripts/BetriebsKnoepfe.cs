namespace Reichsgilde;

/// <summary>
/// Welche der beiden Knöpfe „Ausbau“ und „Verkaufen bzw. Pacht kündigen“ die Kurzübersicht eines eigenen Betriebs zeigt (ohne Godot, getestet).
/// Es sind dieselben Regeln, nach denen sie vorher im Untermenü „…“ standen: Ausbau nur für Betriebe in der Stadt, wenn die Daten Meistergrade
/// kennen und die Betriebsart einen Ausbau hat; Verkaufen (bei Pacht: Pacht kündigen) immer. Gesperrt wird weiter mit Grund aus dem Kern.
/// </summary>
public static class BetriebsKnoepfe
{
    public static bool Ausbau(bool inDerStadt, bool datenKennenMeistergrade, bool artHatAusbau) => inDerStadt && datenKennenMeistergrade && artHatAusbau;

    public static bool Verkauf() => true;

    /// <summary>Beschriftung des Ausbau-Knopfs: kurz, nur der Betrag (aus den Daten); der Name der Stufe steht im Tooltip (<see cref="AusbauTipp"/>).</summary>
    public static string AusbauText(string kosten, string waehrung) => $"Ausbau ({kosten} {waehrung})";

    /// <summary>Erste Zeile des Tooltips: wohin der Ausbau führt; danach folgen die bisherigen Angaben.</summary>
    public static string AusbauTipp(string stufenname) => "Ausbau zu: " + stufenname + "\n";
}
