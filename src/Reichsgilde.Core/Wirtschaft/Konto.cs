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

    /// <summary>Eskorten für beladene Fahrten.</summary>
    public double Transport { get; internal set; }

    /// <summary>Marktwert der Ware, die Banditen erbeutet haben (nur zur Information – das Geld floss nie).</summary>
    public double Raubwert { get; internal set; }

    /// <summary>Anschaffungen wie Karren. Mindern das Vermögen, zählen aber nicht als laufende Kosten.</summary>
    public double Investitionen { get; internal set; }

    /// <summary>Gebühren für den Aufstieg in einen höheren Stand. Zählen ebenfalls nicht als laufende Kosten.</summary>
    public double Standesgebuehren { get; internal set; }

    /// <summary>Hergestellte Stück je Ware.</summary>
    public Dictionary<string, int> Hergestellt { get; } = new();

    /// <summary>Betriebsgewinn: Umsatz minus alle laufenden Kosten (ohne Investitionen).</summary>
    public double Gewinn => Umsatz - Zutaten - Personal - Fixkosten - Steuer - Schaden - Transport;
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

    /// <summary>Stufe in der Ständeordnung: 0 = unterster Stand der Daten (Gemeiner).</summary>
    public int Stand { get; internal set; }

    /// <summary>Wie viele Jahresabschlüsse in Folge das Vermögen negativ war.</summary>
    public int Schuldenjahre { get; private set; }

    /// <summary>Bankrott: Das Konto ist aus dem Spiel, seine Betriebe stehen still.</summary>
    public bool IstBankrott { get; internal set; }

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
    internal void Transport(double betrag) { Vermoegen -= betrag; Laufend.Transport += betrag; }
    internal void Investition(double betrag) { Vermoegen -= betrag; Laufend.Investitionen += betrag; }
    internal void Standesgebuehr(double betrag) { Vermoegen -= betrag; Laufend.Standesgebuehren += betrag; }
    internal void Raub(double warenwert) => Laufend.Raubwert += warenwert;

    internal void Hergestellt(string ware, int stueck = 1) =>
        Laufend.Hergestellt[ware] = Laufend.Hergestellt.GetValueOrDefault(ware) + stueck;

    /// <summary>Schließt das Jahr ab: Die laufende Rechnung wird zum Vorjahr.</summary>
    internal void JahrAbschliessen(int neuesJahr)
    {
        Schuldenjahre = Vermoegen < 0 ? Schuldenjahre + 1 : 0;
        Vorjahr = Laufend;
        Laufend = new Jahresbilanz { Jahr = neuesJahr };
    }
}
