namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Was ein Karren beim Verkauf erlöst – EINE Rechnung für die Abrechnung am Handelsort (<see cref="Karren"/>) und für alle Vorschauen
/// (Handelsauftrag im Marktfenster, <see cref="Kalkulation.GewinnJeStueck"/>, Einrichtung in <see cref="Werkstattwesen"/>). So können
/// Vorschau und Abrechnung nicht auseinanderlaufen. Der Aufschlag am Hafen (<see cref="Schiffahrt.HafenVerkaufsFaktor"/>, Datenversion 80)
/// gilt nur für Ausfuhrware, nur am offenen Hafen und nur, wenn der Karren dort verkauft – am Marktplatz nie.
/// </summary>
public sealed partial class Stadtwirtschaft
{
    /// <summary>
    /// Faktor auf den Marktpreis, mit dem ein Karren dieses Hauses eine Ware verkauft: Verhandeln und Lebensweise des Hauses
    /// (<see cref="Dynastie.Familienbuch.VerkaufsFaktor"/>) × Aufschlag am Hafen, wenn er am Hafen verkauft (<paramref name="amHafen"/>).
    /// Die Abrechnung ruft sie mit <see cref="Karren.ZielHafen"/>, die Vorschau mit <see cref="HandeltAmHafen"/>.
    /// </summary>
    public double KarrenVerkaufsFaktor(Konto konto, string ware, bool amHafen) =>
        Familien.VerkaufsFaktor(konto) * HandelsortFaktor(ware, amHafen);

    /// <summary>Aufschlag des Handelsorts auf eine Ware: am Hafen <see cref="Schiffahrt.HafenVerkaufsFaktor"/>, am Marktplatz 1.</summary>
    public double HandelsortFaktor(string ware, bool amHafen) => amHafen ? Schiffahrt.HafenVerkaufsFaktor(ware) : 1;

    /// <summary>
    /// Verkauft dieser Karren auf seiner Fahrt am Hafen? Unterwegs zum Handeln: das beim Aufbruch gewählte Ziel (<see cref="Karren.ZielHafen"/>);
    /// sonst, ob er jetzt zum Hafen aufbräche (<see cref="FaehrtZumHafen"/>; ein vorgemerkter Auftrag startet später, dann nach dem Stand von jetzt).
    /// </summary>
    public bool HandeltAmHafen(Karren karren) =>
        karren.Zustand == KarrenZustand.ZumMarkt ? karren.ZielHafen : FaehrtZumHafen(karren.Heimat, karren.Typ);

    /// <summary>Faktor auf den Marktpreis für diese Ware, wenn dieser Karren sie jetzt zum Verkauf mitnähme (Vorschau, siehe <see cref="KarrenVerkaufsFaktor"/>).</summary>
    public double KarrenVerkaufsFaktor(Karren karren, string ware) => KarrenVerkaufsFaktor(karren.Heimat.Konto, ware, HandeltAmHafen(karren));

    /// <summary>
    /// Was dieser Karren für so viele Stück jetzt erlöste (vor Steuer, Vorschau des Handelsauftrags): Marktpreis mit dem Preisdruck jedes Stücks
    /// (<see cref="Markt.Verkaufswert"/>) × <see cref="KarrenVerkaufsFaktor(Karren, string)"/> × Preisfaktor des Betriebs (Vorarbeiter, Ladentheke) –
    /// genau wie die Abrechnung (<see cref="Betrieb.Verkaufen"/>) zu den Preisen von jetzt.
    /// </summary>
    public double KarrenErloes(Karren karren, string ware, int stueck) =>
        stueck <= 0 ? 0 : Markt[ware].Verkaufswert(stueck) * KarrenVerkaufsFaktor(karren, ware) * karren.Heimat.PreisFaktor;

    /// <summary>
    /// Aufschlag am Hafen auf die Ware dieses Betriebs im Mittel seiner Karren (je Karren <see cref="HandelsortFaktor"/> nach
    /// <see cref="HandeltAmHafen"/>) – für Vorschauen, die für den ganzen Betrieb rechnen (<see cref="Kalkulation.GewinnJeStueck"/>, Einrichtung).
    /// Ohne Karren, ohne Karren zum Hafen oder ohne Aufschlag genau 1.
    /// </summary>
    public double HafenFaktor(Betrieb betrieb, string ware)
    {
        if (betrieb.Karren.Count == 0 || !Schiffahrt.KarrenZumHafen || Schiffahrt.Daten is not { HafenAufschlag: > 0 }) return 1;
        var summe = 0.0;
        foreach (var k in betrieb.Karren) summe += HandelsortFaktor(ware, HandeltAmHafen(k));
        return summe / betrieb.Karren.Count;
    }
}
