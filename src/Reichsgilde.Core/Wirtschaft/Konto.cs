using Reichsgilde.Core.Speichern;
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

    /// <summary>Einkommen aus einem Amt im Stadtrat (nicht Teil des Betriebsgewinns).</summary>
    public double Amtseinkommen { get; internal set; }

    /// <summary>Geschenke an Ratsherren (nicht Teil des Betriebsgewinns).</summary>
    public double Geschenke { get; internal set; }

    /// <summary>Haushalt, Unterhalt der Kinder, Brautgeschenke und Hochzeiten (nicht Teil des Betriebsgewinns).</summary>
    public double Familie { get; internal set; }

    /// <summary>Spitzel, Gerichtsgebühren, Bußen und Entschädigungen (negativ = Einnahme; nicht Teil des Betriebsgewinns).</summary>
    public double Gericht { get; internal set; }

    /// <summary>Hinterzimmer: Banden, Hehlererlöse, Schweigegeld (negativ = Einnahme; nicht Teil des Betriebsgewinns).</summary>
    public double Gaunerei { get; internal set; }

    /// <summary>Zinsen für Kredite bei einem Geldleiher (nicht Teil des Betriebsgewinns).</summary>
    public double Zinsen { get; internal set; }

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

    /// <summary>Das Wappen des Hauses. Ohne eigene Wahl eines, das aus dem Namen gewürfelt ist (immer dasselbe).</summary>
    public Dynastie.Wappen Wappen { get; internal set; }

    /// <summary>
    /// Bargeld in Gulden – damit wird bezahlt. Darf negativ werden (Schulden bei der Stadt).
    /// Was das Haus wirklich besitzt, zeigt <see cref="Reinvermoegen"/>.
    /// </summary>
    public double Vermoegen { get; private set; }

    /// <summary>Verliehenes Geld: die Kasse der Geldleiher und offene Kredite an andere Häuser.</summary>
    public double Forderungen { get; private set; }

    /// <summary>Offene Kredite bei einem Geldleiher.</summary>
    public double Schulden { get; private set; }

    /// <summary>
    /// Bargeld + Forderungen − Schulden. Danach richten sich Stand, Rangliste, Ansehen, Bußdeckel, Bankrott und Sieg –
    /// wer sich Geld leiht, wird dadurch nicht reicher. Ohne Kredite ist es genau das Bargeld.
    /// </summary>
    public double Reinvermoegen => Vermoegen + Forderungen - Schulden;

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

    /// <summary>Das Haus ist ausgestorben: Das Oberhaupt starb ohne Erben.</summary>
    public bool IstErloschen { get; internal set; }

    /// <summary>Noch im Spiel: weder bankrott noch erloschen.</summary>
    public bool IstAktiv => !IstBankrott && !IstErloschen;

    private readonly SortedSet<string> _berufe = new(StringComparer.Ordinal);

    /// <summary>Die Handwerke, die das Haus beherrscht (Startberuf und erlernte).</summary>
    public IReadOnlyCollection<string> Berufe => _berufe;
    public bool Beherrscht(string beruf) => _berufe.Contains(beruf);
    internal void BerufLernen(string beruf) { if (beruf != "") _berufe.Add(beruf); }

    private readonly SortedDictionary<string, int> _grade = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, double> _erfahrung = new(StringComparer.Ordinal);

    /// <summary>Meistergrad des Hauses in diesem Beruf: 0 = beherrscht ihn nicht, sonst 1–6.</summary>
    public int Meistergrad(string beruf) => Beherrscht(beruf) ? Math.Max(1, _grade.GetValueOrDefault(beruf)) : 0;

    /// <summary>Der höchste Meistergrad in irgendeinem Beruf (Grad 6 bringt einen AP mehr je Runde).</summary>
    public int HoechsterMeistergrad => _berufe.Select(Meistergrad).DefaultIfEmpty(0).Max();

    /// <summary>Gesammelte Erfahrung in diesem Beruf – Arbeitsstunden eines Gesellen für alles, was die Betriebe des Hauses hergestellt haben.</summary>
    public double Erfahrung(string beruf) => _erfahrung.GetValueOrDefault(beruf);

    internal void ErfahrungSammeln(string beruf, double stunden)
    {
        if (beruf != "") _erfahrung[beruf] = _erfahrung.GetValueOrDefault(beruf) + stunden;
    }

    internal void GradSetzen(string beruf, int grad) => _grade[beruf] = grad;

    /// <summary>Stufe des Wohnhauses (0 = Startwohnhaus, siehe <see cref="Wohnen.Wohnungswesen"/>).</summary>
    public int WohnhausStufe { get; internal set; }

    /// <summary>Läuft ein Ausbau des Wohnhauses: ab dieser Spielminute gilt die nächste Stufe (sonst null).</summary>
    public long? WohnhausFertigAb { get; internal set; }

    /// <summary>Aktionspunkte des Hauses für persönliche Handlungen (Werben, Rat, Gericht, Hinterzimmer, Training).</summary>
    public Partie.Aktionspunkte Aktionspunkte { get; } = new();

    public Konto(string inhaber, double startvermoegen, int startjahr)
    {
        Inhaber = inhaber;
        Wappen = Dynastie.Wappen.AusName(inhaber);
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
    internal void Amtseinkommen(double betrag) { Vermoegen += betrag; Laufend.Amtseinkommen += betrag; }
    internal void Geschenk(double betrag) { Vermoegen -= betrag; Laufend.Geschenke += betrag; }
    internal void Familienkosten(double betrag) { Vermoegen -= betrag; Laufend.Familie += betrag; }
    internal void Gerichtskosten(double betrag) { Vermoegen -= betrag; Laufend.Gericht += betrag; }
    internal void Gaunerkosten(double betrag) { Vermoegen -= betrag; Laufend.Gaunerei += betrag; }
    internal void Raub(double warenwert) => Laufend.Raubwert += warenwert;

    // ── Kredite (Geldleiher) ────────────────────────────────────────────────
    internal void Verleihen(double betrag) { Vermoegen -= betrag; Forderungen += betrag; }
    internal void Zurueckerhalten(double betrag) { Vermoegen += betrag; Forderungen -= betrag; }
    /// <summary>Ein Schuldner fällt aus: Die Forderung ist verloren (Schaden), das Bargeld bleibt.</summary>
    internal void ForderungAbschreiben(double betrag) { Forderungen -= betrag; Laufend.Schaden += betrag; }
    internal void KreditErhalten(double betrag) { Vermoegen += betrag; Schulden += betrag; }
    internal void KreditTilgen(double betrag) { Vermoegen -= betrag; Schulden -= betrag; }
    internal void Zinsen(double betrag) { Vermoegen -= betrag; Laufend.Zinsen += betrag; }

    internal void Hergestellt(string ware, int stueck = 1) =>
        Laufend.Hergestellt[ware] = Laufend.Hergestellt.GetValueOrDefault(ware) + stueck;

    // ── Speichern ───────────────────────────────────────────────────────────

    internal KontoStand Sichern() => new()
    {
        Inhaber = Inhaber, Wappen = Wappen, Vermoegen = Vermoegen, Forderungen = Forderungen, Schulden = Schulden, Stand = Stand, Schuldenjahre = Schuldenjahre, IstBankrott = IstBankrott, IstErloschen = IstErloschen,
        Aktionspunkte = Aktionspunkte.Vorrat,
        WohnhausStufe = WohnhausStufe, WohnhausFertigAb = WohnhausFertigAb,
        Berufe = _berufe.ToList(),
        Meistergrade = _grade.ToDictionary(x => x.Key, x => x.Value),
        Erfahrung = _erfahrung.ToDictionary(x => x.Key, x => x.Value),
        Laufend = BilanzSichern(Laufend), Vorjahr = Vorjahr is null ? null : BilanzSichern(Vorjahr),
    };

    internal static Konto Wiederherstellen(KontoStand s)
    {
        var konto = new Konto(s.Inhaber, s.Vermoegen, s.Laufend.Jahr)
        {
            Forderungen = s.Forderungen, Schulden = s.Schulden,
            Stand = s.Stand, Schuldenjahre = s.Schuldenjahre, IstBankrott = s.IstBankrott, IstErloschen = s.IstErloschen,
            Laufend = BilanzLaden(s.Laufend), Vorjahr = s.Vorjahr is null ? null : BilanzLaden(s.Vorjahr),
        };
        if (s.Wappen is { } wappen) konto.Wappen = wappen;
        konto.Aktionspunkte.Setzen(s.Aktionspunkte);
        konto.WohnhausStufe = s.WohnhausStufe ?? 0; // alte Spielstände: ergänzt die Stadtwirtschaft nach dem Laden
        konto.WohnhausFertigAb = s.WohnhausFertigAb;
        foreach (var beruf in s.Berufe ?? new List<string>())
            konto.BerufLernen(beruf);
        foreach (var (beruf, grad) in s.Meistergrade ?? new Dictionary<string, int>())
            konto._grade[beruf] = grad;
        foreach (var (beruf, stunden) in s.Erfahrung ?? new Dictionary<string, double>())
            konto._erfahrung[beruf] = stunden;
        return konto;
    }

    private static BilanzStand BilanzSichern(Jahresbilanz b) => new()
    {
        Jahr = b.Jahr, Umsatz = b.Umsatz, Zutaten = b.Zutaten, Personal = b.Personal, Fixkosten = b.Fixkosten,
        Steuer = b.Steuer, Schaden = b.Schaden, Transport = b.Transport, Raubwert = b.Raubwert,
        Investitionen = b.Investitionen, Standesgebuehren = b.Standesgebuehren, Hergestellt = new(b.Hergestellt),
        Amtseinkommen = b.Amtseinkommen, Geschenke = b.Geschenke, Familie = b.Familie, Gericht = b.Gericht, Gaunerei = b.Gaunerei,
        Zinsen = b.Zinsen,
    };

    private static Jahresbilanz BilanzLaden(BilanzStand s)
    {
        var b = new Jahresbilanz
        {
            Jahr = s.Jahr, Umsatz = s.Umsatz, Zutaten = s.Zutaten, Personal = s.Personal, Fixkosten = s.Fixkosten,
            Steuer = s.Steuer, Schaden = s.Schaden, Transport = s.Transport, Raubwert = s.Raubwert,
            Investitionen = s.Investitionen, Standesgebuehren = s.Standesgebuehren,
            Amtseinkommen = s.Amtseinkommen, Geschenke = s.Geschenke, Familie = s.Familie, Gericht = s.Gericht, Gaunerei = s.Gaunerei,
            Zinsen = s.Zinsen,
        };
        foreach (var (ware, stueck) in s.Hergestellt) b.Hergestellt[ware] = stueck;
        return b;
    }

    /// <summary>Schließt das Jahr ab: Die laufende Rechnung wird zum Vorjahr.</summary>
    internal void JahrAbschliessen(int neuesJahr)
    {
        Schuldenjahre = Reinvermoegen < 0 ? Schuldenjahre + 1 : 0;
        Vorjahr = Laufend;
        Laufend = new Jahresbilanz { Jahr = neuesJahr };
    }
}
