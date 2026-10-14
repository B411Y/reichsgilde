namespace Reichsgilde.Core.Justiz;

/// <summary>
/// Alle Zahlen zum Stadtgericht (Abschnitt "justiz" in wirtschaft.json).
///
/// Wie ein Richter urteilt:
///   Überzeugung = Beweise × BeweiskraftJeBeweis + Gunstwirkung
///   Gunstwirkung = GunstFaktor × (Gunst für den Ankläger − Gunst für den Beklagten), höchstens ± GunstHoechstwirkung
///   ab SchuldSchwelle: „schuldig". Drei Richter (Vorsitz + zwei Schöffen), die Mehrheit entscheidet.
/// Bestechung kann also einen knappen Fall kippen, aber keinen klaren: Ab SchuldSchwelle + GunstHoechstwirkung
/// (Standard 25, also 3 Beweise) hilft dem Beklagten kein Geschenk mehr.
/// </summary>
public sealed class JustizDaten
{
    /// <summary>Nach so vielen Jahren ist eine Tat verjährt – Spuren und Beweise verfallen.</summary>
    public int VerjaehrungJahre { get; init; } = 5;

    /// <summary>Was ein Spitzel für ein Jahr kostet (je beobachtetem Haus).</summary>
    public double SpitzelKosten { get; init; } = 300;

    /// <summary>Chance, dass der Spitzel eine bestimmte Spur im Jahr aufdeckt.</summary>
    public double SpitzelChance { get; init; } = 0.5;

    /// <summary>Gebühr für eine Anklage.</summary>
    public double Gerichtsgebuehr { get; init; } = 100;

    public double BeweiskraftJeBeweis { get; init; } = 10;
    public double SchuldSchwelle { get; init; } = 15;

    /// <summary>Wie stark die Gunst eines Richters (aus dem Rat) sein Urteil färbt.</summary>
    public double GunstFaktor { get; init; } = 0.5;

    /// <summary>Mehr als so viele Punkte kann Gunst die Überzeugung eines Richters nicht verschieben.</summary>
    public double GunstHoechstwirkung { get; init; } = 10;

    /// <summary>Ab dieser Beweiskraft ist ein Schuldspruch sicher – auch gegen bestochene Richter.</summary>
    public double SichereBeweiskraft => SchuldSchwelle + GunstHoechstwirkung;

    /// <summary>Buße = Faktor × nachgewiesenes Bestechungsgeld, mindestens <see cref="Mindestbusse"/>.</summary>
    public double BusseFaktor { get; init; } = 3;
    public double Mindestbusse { get; init; } = 200;

    /// <summary>So viele Jahre steht ein Verurteilter am Pranger – mit so viel weniger Ansehen.</summary>
    public int PrangerJahre { get; init; } = 3;
    public double PrangerAnsehen { get; init; } = 10;

    /// <summary>Bei Freispruch zahlt der Ankläger dem Beklagten diese Entschädigung.</summary>
    public double Entschaedigung { get; init; } = 200;

    /// <summary>Verurteilte Amtsträger verlieren ihr Amt.</summary>
    public bool Amtsverlust { get; init; } = true;

    /// <summary>
    /// So viele Jahre sitzt ein Oberhaupt im Kerker, wenn ein schweres Vergehen bewiesen ist.
    /// Im Kerker: kein Rat, keine Brautschau, kein Gericht, kein Hinterzimmer – die Betriebe laufen weiter.
    /// </summary>
    public int KerkerJahre { get; init; } = 1;

    /// <summary>Welche Vergehen in den Kerker führen.</summary>
    public List<Vergehen> KerkerFuer { get; init; } = new() { Vergehen.Raub, Vergehen.Erpressung };

    internal void Pruefen(List<string> f)
    {
        if (VerjaehrungJahre < 1) f.Add("justiz.verjaehrungJahre muss mindestens 1 sein.");
        if (SpitzelChance is < 0 or > 1) f.Add("justiz.spitzelChance muss zwischen 0 und 1 liegen.");
        if (SpitzelKosten < 0 || Gerichtsgebuehr < 0 || Mindestbusse < 0 || Entschaedigung < 0) f.Add("justiz: Kosten dürfen nicht negativ sein.");
        if (BeweiskraftJeBeweis <= 0 || SchuldSchwelle <= 0) f.Add("justiz: beweiskraftJeBeweis und schuldSchwelle müssen größer als 0 sein.");
        if (PrangerJahre < 0 || PrangerAnsehen < 0) f.Add("justiz: prangerJahre und prangerAnsehen dürfen nicht negativ sein.");
        if (KerkerJahre < 0) f.Add("justiz.kerkerJahre darf nicht negativ sein.");
    }
}
