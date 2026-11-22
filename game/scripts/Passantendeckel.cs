namespace Reichsgilde;

/// <summary>
/// Wie viele Passanten die Stadtansicht zugleich zeigt (Nutzer 22.11.2026): nur die im Bild (mit Rand), höchstens <see cref="Deckel"/>, davon höchstens
/// <see cref="SkelettMax"/> mit Skelett (darüber zeichnet das Spiel alle als gebackene Vertex-Animation), und je weiter herausgezoomt, desto kleiner die Teilmenge
/// (<see cref="ZoomProzent"/>). Die Teilmenge ist stabil: Jede Figur hat über ihre Nummer einen festen Rang (<see cref="Rang"/>, 0 bis 999), gezeigt wird, wer unter
/// der Schwelle liegt – beim Zoomen oder Verschieben kommen und gehen nur die Figuren am Rand der Schwelle, nichts flackert. Reine Rechnung ohne Godot,
/// getestet in <c>PassantendeckelTests.cs</c>; die Stadtansicht (Stadtansicht.Passanten.cs) wendet sie an. Der Grafikregler „Menge der Passanten“ wirkt zusätzlich.
/// </summary>
public static class Passantendeckel
{
    /// <summary>Höchstens so viele Passanten zugleich im Bild gezeigt.</summary>
    public const int Deckel = 400;

    /// <summary>Höchstens so viele davon mit Skelett; wären es mehr, schaltet das Spiel alle auf die gebackene Stufe (VAT).</summary>
    public const int SkelettMax = 60;

    /// <summary>Erst wenn es wieder höchstens so viele im Bild sind, geht es zurück zum Skelett (Spielraum gegen Hin- und Herschalten).</summary>
    public const int SkelettZurueck = 45;

    /// <summary>Rand ums Bild in Metern: Wer weiter draußen ist, wird nicht gebaut und nur selten weitergerechnet.</summary>
    public const float Rand = 15f;

    /// <summary>Wer außerhalb von Bild und <see cref="Rand"/> ist, wird nur in jedem so vielten Bild weitergerechnet (nach der Nummer gestaffelt).</summary>
    public const int FernJedes = 8;

    /// <summary>Wächst die Schwelle wieder (es ist Platz unter dem Deckel), um so viele Promille je Bild – die Figuren kommen nach und nach, nicht alle im selben Bild.</summary>
    public const int Wachstum = 30;

    /// <summary>Unter diesem Anteil des Deckels darf die Schwelle wieder wachsen.</summary>
    public const double WachsenUnter = 0.85;

    /// <summary>Stützstellen der Dichte: Zoom (Meter Bildhöhe: 25 nah, 50 mittel, 100 weit) und Anteil der Figuren in Prozent; dazwischen linear, außerhalb gleich dem Rand.</summary>
    public static readonly (float Zoom, int Prozent)[] Dichte = { (25f, 100), (50f, 60), (100f, 30) };

    /// <summary>Der Anteil der Figuren in Prozent bei diesem Zoom (Meter Bildhöhe).</summary>
    public static int ZoomProzent(float zoom)
    {
        if (zoom <= Dichte[0].Zoom) return Dichte[0].Prozent;
        for (var i = 1; i < Dichte.Length; i++)
        {
            if (zoom > Dichte[i].Zoom) continue;
            var (z0, p0) = Dichte[i - 1];
            var (z1, p1) = Dichte[i];
            return (int)MathF.Round(p0 + (p1 - p0) * (zoom - z0) / (z1 - z0));
        }
        return Dichte[^1].Prozent;
    }

    /// <summary>Der feste Rang einer Figur, 0 bis 999 (über die Nummer gestreut, wie beim Regler „Menge der Passanten“).</summary>
    public static int Rang(int nummer) => (int)(unchecked((uint)((long)nummer * 2654435761L)) % 1000u);

    /// <summary>Wie viele Promille der Figuren Zoom und Grafikregler zusammen zulassen (der kleinere Anteil gilt).</summary>
    public static int Promille(float zoom, int mengeProzent) => Math.Clamp(Math.Min(ZoomProzent(zoom), mengeProzent), 0, 100) * 10;

    /// <summary>Darf die Figur mit dieser Nummer bei dieser Schwelle (Promille) gezeigt werden?</summary>
    public static bool Gezeigt(int nummer, int schwelle) => Rang(nummer) < schwelle;

    /// <summary>
    /// Die neue Schwelle (Promille) für dieses Bild. <paramref name="alt"/> ist die vom letzten Bild, <paramref name="gewollt"/> die aus Zoom und Regler,
    /// <paramref name="imBild"/> zählt die jetzt im Bild gezeigten Figuren je Rang (1000 Plätze). Liegen mehr unter der Schwelle als <paramref name="deckel"/>,
    /// sinkt sie sofort auf die höchste, bei der es noch reicht; ist Platz (unter <see cref="WachsenUnter"/> des Deckels), wächst sie um <see cref="Wachstum"/>
    /// Promille bis höchstens <paramref name="gewollt"/>. Sinkt <paramref name="gewollt"/>, folgt sie sofort.
    /// </summary>
    public static int Schwelle(int alt, int gewollt, int[] imBild, int deckel = Deckel)
    {
        var t = Math.Min(Math.Clamp(alt, 0, 1000), gewollt);
        var summe = 0;
        for (var r = 0; r < t; r++) summe += imBild[r];
        if (summe > deckel)
        {
            // höchste Schwelle, bei der höchstens „deckel“ Figuren darunter liegen
            summe = 0;
            for (var r = 0; r < t; r++)
            {
                if (summe + imBild[r] > deckel) return r;
                summe += imBild[r];
            }
            return t;
        }
        if (summe < deckel * WachsenUnter && t < gewollt) t = Math.Min(gewollt, t + Wachstum);
        return t;
    }

    /// <summary>
    /// Skelett oder gebackene Stufe: Ab der mittleren Zoomstufe (<paramref name="weit"/>) immer gebacken; sonst Skelett, solange höchstens
    /// <see cref="SkelettMax"/> Figuren im Bild sind – darüber gebacken, zurück zum Skelett erst bei höchstens <see cref="SkelettZurueck"/> (Spielraum).
    /// </summary>
    public static bool Gebacken(bool weit, bool bisher, int imBild)
    {
        if (weit) return true;
        return bisher ? imBild > SkelettZurueck : imBild > SkelettMax;
    }
}
