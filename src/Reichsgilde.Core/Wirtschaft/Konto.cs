namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Die Jahresrechnung eines Kontos – dieselben Posten wie im Balancing-Rechner.
/// </summary>
public sealed class Jahresbilanz
{
    public int Jahr { get; init; }
    public double Umsatz { get; internal set; }
    public double Zutaten { get; internal set; }
    public double Personal { get; internal set; }
    public double Fixkosten { get; internal set; }
    public double Steuer { get; internal set; }
    public double Schaden { get; internal set; }

    /// <summary>Hergestellte Stück je Ware.</summary>
    public Dictionary<string, int> Hergestellt { get; } = new();

    public double Gewinn => Umsatz - Zutaten - Personal - Fixkosten - Steuer - Schaden;
}

/// <summary>
/// Das Geld einer Dynastie (später) bzw. eines Spielers (jetzt).
/// Jede Bewegung wird einem Posten der Jahresbilanz zugeordnet.
/// </summary>
public sealed class Konto
{
    public string Inhaber { get; }

    /// <summary>Aktuelles Vermögen in Gulden. Darf negativ werden (Schulden).</summary>
    public double Vermoegen { get; private set; }

    /// <summary>Die Rechnung des laufenden Jahres.</summary>
    public Jahresbilanz Laufend { get; private set; }

    /// <summary>Die abgeschlossene Rechnung des Vorjahres (null im ersten Jahr).</summary>
    public Jahresbilanz? Vorjahr { get; private set; }

    public Konto(string inhaber, double startvermoegen, int startjahr)
    {
        Inhaber = inhaber;
        Vermoegen = startvermoegen;
        Laufend = new Jahresbilanz { Jahr = startjahr };
    }

    internal void Umsatz(double betrag) { Vermoegen += betrag; Laufend.Umsatz += betrag; }
    internal void Zutaten(double betrag) { Vermoegen -= betrag; Laufend.Zutaten += betrag; }
    internal void Personal(double betrag) { Vermoegen -= betrag; Laufend.Personal += betrag; }
    internal void Fixkosten(double betrag) { Vermoegen -= betrag; Laufend.Fixkosten += betrag; }
    internal void Steuer(double betrag) { Vermoegen -= betrag; Laufend.Steuer += betrag; }
    internal void Schaden(double betrag) { Vermoegen -= betrag; Laufend.Schaden += betrag; }

    internal void Hergestellt(string ware, int stueck = 1) =>
        Laufend.Hergestellt[ware] = Laufend.Hergestellt.GetValueOrDefault(ware) + stueck;

    /// <summary>Schließt das Jahr ab: Die laufende Rechnung wird zum Vorjahr.</summary>
    internal void JahrAbschliessen(int neuesJahr)
    {
        Vorjahr = Laufend;
        Laufend = new Jahresbilanz { Jahr = neuesJahr };
    }
}
