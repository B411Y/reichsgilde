using Reichsgilde.Core.Dynastie;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Wer den Karren zieht, schiebt oder lenkt – nur fürs Bild (Volk, Geschlecht, Aussehen und ein fester Startwert für Kleidung und Fell).
/// Der Fuhrmann ist <b>abgeleitet, nicht gespeichert</b>: Er kostet keinen Lohn, steht in keinem Spielstand und verändert keine Wirtschaftszahl.
/// Derselbe Karren hat immer denselben Fuhrmann (festes Würfeln aus Betriebsname (Heimat.Name) und Karrenname, siehe <see cref="Karren.FuhrmannIn"/>).
/// </summary>
/// <param name="Volk">Volk des Fuhrmanns (Ork oder Oger nur, wenn das Haus diesem Volk angehört).</param>
/// <param name="Geschlecht">Mann oder Frau.</param>
/// <param name="Aussehen">Gesicht, Haar und Augen wie bei einem Arbeiter (für die Figur).</param>
/// <param name="Startwert">Fester Wert (nicht negativ) für Kleidung, Fellvariante des Zugtiers und andere Kleinigkeiten der Darstellung.</param>
public readonly record struct Fuhrmann(Volk Volk, Geschlecht Geschlecht, Aussehen Aussehen, int Startwert)
{
    private static readonly Volk[] Buergervoelker = { Volk.Mensch, Volk.Zwerg, Volk.Elb, Volk.Halbling };

    /// <summary>Würfelt den Fuhrmann fest aus Betriebs- und Karrenname; <paramref name="hausVolk"/> ist das Volk des Hauses (null: unbekannt).</summary>
    internal static Fuhrmann Ableiten(string betriebsname, string karrenname, Volk? hausVolk, double eigenesVolk, double frauenAnteil)
    {
        var z = new Zufallsquelle(Personalwesen.Startwert($"Fuhrmann#{betriebsname}#{karrenname}"));
        var eigen = z.NextDouble();
        var irgendeins = z.Next(1000);
        var frau = z.NextDouble() < frauenAnteil;
        var start = z.Next(1_000_000);
        // Das Volk des Hauses mit der Wahrscheinlichkeit „eigenes Volk“ (wie bei den Arbeitern), sonst eines der vier Bürgervölker.
        var volk = hausVolk is { } h && eigen < eigenesVolk ? h : Buergervoelker[irgendeins % Buergervoelker.Length];
        return new Fuhrmann(volk, frau ? Geschlecht.Frau : Geschlecht.Mann, Aussehenspalette.Wuerfeln(z, volk), start);
    }
}
