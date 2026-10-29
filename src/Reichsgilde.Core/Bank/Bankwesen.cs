using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Bank;

// ─────────────────────────────────────────────────────────────────────────────
//  Daten (Abschnitt „geldleiher" in wirtschaft.json, ab Datenversion 20)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class RahmenDaten
{
    /// <summary>So viel Kapital betreut ein Schreiber (Geselle) während der Arbeitszeit.</summary>
    public double JeSchreiber { get; init; } = 2000;

    /// <summary>Faktor je Gebäudestufe (Wechselstube, Geldleihe, Bankhaus).</summary>
    public List<double> JeStufe { get; init; } = new() { 1, 1.25, 1.5 };
}

public sealed class AusfallDaten
{
    /// <summary>Chance je Jahr und Kasse, dass Stadtkunden ausfallen.</summary>
    public double Chance { get; init; } = 0.2;

    /// <summary>Dann ist dieser Anteil der Kasse verloren.</summary>
    public double Anteil { get; init; } = 0.1;
}

public sealed class WechselDaten
{
    /// <summary>Gebühr auf den Umsatz des ganzen Marktes – auswärtige Kaufleute wechseln ihre Münzen.</summary>
    public double Gebuehr { get; init; } = 0.007;

    /// <summary>So viele Schreiber braucht die Stadt, um alles Geld zu wechseln; mehr verdienen nichts dazu.</summary>
    public double BedarfSchreiber { get; init; } = 6;

    /// <summary>Faktor je Gebäudestufe.</summary>
    public List<double> JeStufe { get; init; } = new() { 1, 1.2, 1.4 };
}

public sealed class KreditDaten
{
    public double ZinsStandard { get; init; } = 0.12;
    public double ZinsMindestens { get; init; } = 0.04;
    public double ZinsHoechstens { get; init; } = 0.5;

    /// <summary>So viel darf ein Geldleiher insgesamt an Häuser verleihen, nach seiner besten Stufe.</summary>
    public List<double> HoechstJeStufe { get; init; } = new() { 3000, 6000, 12000 };

    /// <summary>Ein Haus bekommt höchstens diesen Anteil seines Reinvermögens geliehen.</summary>
    public double KreditwuerdigAnteil { get; init; } = 0.5;

    public int LaufzeitHoechstens { get; init; } = 5;

    /// <summary>Reicht das Bargeld nicht für die Rate, wird Lagerware zu diesem Anteil des Marktpreises gepfändet.</summary>
    public double PfandAnteil { get; init; } = 0.75;
}

public sealed class WucherDaten
{
    /// <summary>Ein Zins über dieser Grenze ist Wucher: eine Spur fürs Gericht, der Schuldner hat den Beweis.</summary>
    public double Ab { get; init; } = 0.2;

    /// <summary>So viel Ansehen verliert ein Haus, solange ein Wucherkredit läuft.</summary>
    public double Ansehen { get; init; } = 5;
}

/// <summary>
/// Einbruch in einen Bankbetrieb (Wechselstube, Geldleihe, Bankhaus; PR #177): Ein gelungener Diebstahl oder Einbruch holt einen Teil
/// der Kasse. Fehlt der Abschnitt (alte Spielstände), bleibt die Kasse unberührt.
/// </summary>
public sealed class KasseneinbruchDaten
{
    /// <summary>Anteil der Kasse, den die Diebe mitnehmen (0,05 = 5 %).</summary>
    public double Anteil { get; init; }

    /// <summary>Höchstens so viele Gulden je Einbruch.</summary>
    public double Hoechstens { get; init; }
}

/// <summary>Der Geldleiher: Kasse für Stadtkunden, Wechsel, Kredite an Häuser, zusätzliche AP.</summary>
public sealed class GeldleiherDaten
{
    public string Beruf { get; init; } = "Geldleiher";

    /// <summary>Zins der Stadtkunden je Jahr auf das betreute Kapital.</summary>
    public double Stadtzins { get; init; } = 0.12;

    public RahmenDaten Rahmen { get; init; } = new();
    public AusfallDaten Ausfall { get; init; } = new();
    public WechselDaten Wechsel { get; init; } = new();
    public KreditDaten Kredit { get; init; } = new();
    public WucherDaten Wucher { get; init; } = new();
    public KasseneinbruchDaten Einbruch { get; init; } = new();

    /// <summary>Zusätzliche AP je Runde nach der besten Stufe (Wechselstube, Geldleihe, Bankhaus).</summary>
    public List<int> ApJeStufe { get; init; } = new() { 0, 1, 2 };

    public void Pruefen(List<string> f, IReadOnlyDictionary<string, BerufDaten> berufe)
    {
        if (berufe.Count > 0 && !berufe.ContainsKey(Beruf)) f.Add($"geldleiher.beruf: Den Beruf „{Beruf}“ gibt es nicht.");
        if (Rahmen.JeStufe.Count < 3 || Wechsel.JeStufe.Count < 3 || Kredit.HoechstJeStufe.Count < 3 || ApJeStufe.Count < 3)
            f.Add("geldleiher: rahmen.jeStufe, wechsel.jeStufe, kredit.hoechstJeStufe und apJeStufe brauchen je einen Wert für Stufe 1, 2 und 3.");
        if (Rahmen.JeSchreiber < 0 || Wechsel.BedarfSchreiber <= 0 || Rahmen.JeStufe.Any(x => x < 0) || Wechsel.JeStufe.Any(x => x < 0)
            || Kredit.HoechstJeStufe.Any(x => x < 0) || ApJeStufe.Any(x => x < 0) || Wucher.Ansehen < 0 || Einbruch.Hoechstens < 0)
            f.Add("geldleiher: Rahmen, Bedarf, Höchstbeträge, AP und Ansehen dürfen nicht negativ sein (Bedarf größer als 0).");
        foreach (var (name, wert) in new[] { ("stadtzins", Stadtzins), ("ausfall.chance", Ausfall.Chance), ("ausfall.anteil", Ausfall.Anteil),
                     ("wechsel.gebuehr", Wechsel.Gebuehr), ("kredit.kreditwuerdigAnteil", Kredit.KreditwuerdigAnteil), ("kredit.pfandAnteil", Kredit.PfandAnteil),
                     ("kredit.zinsMindestens", Kredit.ZinsMindestens), ("kredit.zinsHoechstens", Kredit.ZinsHoechstens), ("wucher.ab", Wucher.Ab),
                     ("einbruch.anteil", Einbruch.Anteil) })
            if (wert is < 0 or > 1) f.Add($"geldleiher.{name} muss zwischen 0 und 1 liegen.");
        if (Kredit.ZinsStandard < Kredit.ZinsMindestens || Kredit.ZinsStandard > Kredit.ZinsHoechstens)
            f.Add("geldleiher.kredit.zinsStandard muss zwischen zinsMindestens und zinsHoechstens liegen.");
        if (Kredit.LaufzeitHoechstens < 1) f.Add("geldleiher.kredit.laufzeitHoechstens muss mindestens 1 sein.");
    }
}

/// <summary>Was die Geldleihe eines Hauses in einem Jahr eingenommen und verloren hat (Gestohlen: von Dieben aus der Kasse; 0 in alten Spielständen).</summary>
public sealed record Bankbuch(double Stadtzinsen, double Wechsel, double Hauszinsen, double Ausfall, double Gestohlen = 0);

/// <summary>Ein Kredit eines Geldleihers an ein anderes Haus.</summary>
public sealed class Kredit
{
    public int Nummer { get; internal init; }
    public Konto Geber { get; internal init; } = null!;
    public Konto Nehmer { get; internal init; } = null!;

    /// <summary>Geliehener Betrag.</summary>
    public double Betrag { get; internal init; }

    /// <summary>Noch nicht getilgt.</summary>
    public double Rest { get; internal set; }

    /// <summary>Zins je Jahr.</summary>
    public double Satz { get; internal init; }

    /// <summary>Noch zu zahlende Raten (Jahresenden); bei der letzten wird der ganze Rest fällig.</summary>
    public int JahreOffen { get; internal set; }

    /// <summary>Seit dem letzten Jahresende aufgelaufene Zinsen (stündlich, fällig am Jahresende).</summary>
    public double Aufgelaufen { get; internal set; }

    /// <summary>Jahr der Vergabe.</summary>
    public int Jahr { get; internal init; }

    public bool IstWucher { get; internal init; }

    /// <summary>Die nächste Tilgungsrate (ohne Zinsen).</summary>
    public double Tilgungsrate => JahreOffen <= 1 ? Rest : Rest / JahreOffen;
}

/// <summary>Wer in der Geldleihe Kunde ist.</summary>
public enum Kundenart
{
    /// <summary>Ein auswärtiger Kaufmann am Wechseltisch.</summary>
    Kaufmann,
    /// <summary>Ein Bürger am Schalter, der Geld leiht oder zurückzahlt.</summary>
    Buerger,
    /// <summary>Ein vornehmer Kunde im Empfang (ab Stufe 3): ein Haus mit Kredit oder ein Patrizier.</summary>
    Vornehm,
}

/// <summary>Weshalb ein Kunde gekommen ist.</summary>
public enum Kundenanliegen
{
    /// <summary>Kaufmann: Münzen wechseln.</summary>
    Wechseln,
    /// <summary>Bürger: Geld aus der Kasse leihen.</summary>
    Leihen,
    /// <summary>Bürger: Geld zurückzahlen.</summary>
    Zurueckzahlen,
    /// <summary>Vornehm: ein Haus, das bei diesem Geldleiher einen Kredit hat.</summary>
    Kredit,
    /// <summary>Vornehm: ein Patrizier lässt sich beraten.</summary>
    Beraten,
}

/// <summary>Ein Kunde in der Geldleihe, gerade jetzt.</summary>
/// <param name="Platz">Platz innerhalb seiner Art, ab 0 (Wechseltisch, Schalter, Empfang) – höchstens die Plätze im Raum.</param>
/// <param name="Haus">Bei vornehmen Kunden das Haus, sonst null.</param>
public sealed record Bankkunde(Kundenart Art, int Platz, Kundenanliegen Anliegen, Konto? Haus);

/// <summary>Was ein Schreiber gerade tut.</summary>
public enum Schreibertaetigkeit
{
    /// <summary>Bedient Kunden (welche: <see cref="Schreiberstand.Bedient"/>).</summary>
    Bedienen,
    /// <summary>Rechnet Zinsen auf dem Rechenbrett.</summary>
    Rechnen,
    /// <summary>Wiegt Münzen.</summary>
    Wiegen,
    /// <summary>Schreibt ins Hauptbuch (ab Stufe 2).</summary>
    Hauptbuch,
    /// <summary>Zählt die Kasse.</summary>
    Zaehlen,
}

/// <summary>Was ein Schreiber der Geldleihe gerade tut; beim Bedienen, welche Kunden (Wechseltisch, Schalter, Empfang).</summary>
public sealed record Schreiberstand(Arbeiter Arbeiter, Schreibertaetigkeit Taetigkeit, Kundenart? Bedient);

// ─────────────────────────────────────────────────────────────────────────────
//  Teilsystem
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Die Geldleiher der Stadt – ein Beruf ohne Ware, dessen Schreiber Geld arbeiten lassen:
///   • Kasse: Der Geldleiher legt Bargeld in die Kasse. Während der Arbeitszeit verleihen die Schreiber es an Stadtkunden
///     (Zins je Jahr), jeder Schreiber betreut einen festen Rahmen. Am Jahresende fallen manchmal Kunden aus.
///   • Wechsel: Auswärtige Kaufleute wechseln ihre Münzen – eine Gebühr auf den Umsatz des Marktes, verteilt nach Schreibern.
///   • Kredite an Häuser: Jedes Haus kann bei einem Geldleiher leihen (Zins setzt der Geldleiher). Zinsen laufen stündlich auf,
///     Zins und Rate sind am Jahresende fällig; fehlt das Bargeld, wird Lagerware gepfändet. Fällt ein Schuldner aus, ist der Rest verloren.
///   • Wucher: Ein Zins über der Grenze ist eine Spur fürs Gericht und kostet Ansehen, solange der Kredit läuft.
///   • Geldleihe und Bankhaus bringen zusätzliche AP.
/// Ohne Geldleiher bleibt alles wie zuvor: eigener Zufall, keine Wirkung auf andere Häuser.
/// </summary>
public sealed class Bankwesen
{
    private readonly Stadtwirtschaft _w;
    private readonly GeldleiherDaten? _d;
    private Zufallsquelle _zufall;
    private readonly SortedDictionary<string, double> _kassen = new(StringComparer.Ordinal);
    private readonly Dictionary<Konto, double> _zinssaetze = new();
    private readonly List<Kredit> _kredite = new();
    private int _naechsterKredit = 1;
    private readonly Dictionary<Konto, Bankbuch> _diesesJahr = new();
    private readonly Dictionary<Konto, Bankbuch> _vorjahr = new();

    public Bankwesen(Stadtwirtschaft w, GeldleiherDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 59 + 37));
    }

    public bool IstAktiv => _d is not null;
    public GeldleiherDaten Daten => _d ?? throw new InvalidOperationException("Es gibt keine Geldleiher (geldleiher fehlt in den Daten).");

    public Bankbuch DiesesJahr(Konto k) => _diesesJahr.GetValueOrDefault(k, new Bankbuch(0, 0, 0, 0));
    public Bankbuch? Vorjahr(Konto k) => _vorjahr.GetValueOrDefault(k);
    private void Buchen(Konto k, Func<Bankbuch, Bankbuch> aendern) => _diesesJahr[k] = aendern(DiesesJahr(k));

    private static T Stufenwert<T>(List<T> liste, int stufe) => liste[Math.Clamp(stufe - 1, 0, liste.Count - 1)];

    // ── Abfragen ────────────────────────────────────────────────────────────

    /// <summary>Ein Betrieb des Geldleihers (Wechselstube, Geldleihe, Bankhaus).</summary>
    public bool IstBankbetrieb(Betrieb b) => IstAktiv && b.OhneProduktion && b.Artdaten.Beruf == Daten.Beruf;

    /// <summary>Die beste eigene Geldleihe mit Schreibern – oder null.</summary>
    public Betrieb? Kontor(Konto k) => _w.Betriebe.Where(b => b.Konto == k && IstBankbetrieb(b) && b.Belegt > 0)
        .OrderByDescending(b => b.Stufe).ThenBy(b => b.Name, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>Häuser, bei denen man Geld leihen kann (aktiv, mit Schreibern).</summary>
    public IEnumerable<Konto> Geldleiher => _w.Konten.Where(k => k.IstAktiv && Kontor(k) is not null);

    public double Kasse(Betrieb b) => _kassen.GetValueOrDefault(b.Name);

    /// <summary>So viel Kapital betreut ein Schreiber (Geselle) in diesem Betrieb – nach Stufe, mit der Einrichtung (Rechenbretter, Hauptbücher).</summary>
    public double RahmenJeSchreiber(Betrieb b) => IstAktiv ? Daten.Rahmen.JeSchreiber * Stufenwert(Daten.Rahmen.JeStufe, b.Stufe) * b.RahmenFaktor : 0;

    /// <summary>So viel Kapital können die Schreiber bei voller Besetzung (lauter Gesellen) betreuen.</summary>
    public double RahmenVoll(Betrieb b) => RahmenJeSchreiber(b) * b.Plaetze;

    /// <summary>So viel Kapital betreuen die Schreiber mit der jetzigen Belegschaft.</summary>
    public double Rahmen(Betrieb b) => RahmenJeSchreiber(b) * b.LeistungProMinute;

    /// <summary>Der Zins, den dieses Haus für Kredite an Häuser verlangt.</summary>
    public double Zinssatz(Konto k) => _zinssaetze.GetValueOrDefault(k, IstAktiv ? Daten.Kredit.ZinsStandard : 0);

    /// <summary>Umsatz des ganzen Marktes in einem Jahr (Angebot des letzten Spieljahres × Preis, mit den auswärtigen Händlern).</summary>
    public double Marktvolumen => _w.Markt.Staende.Sum(s => s.Gesamtangebot * s.Preis);

    /// <summary>Wechselgebühr je Arbeitsminute eines Schreibers (vor Steuer, Stufe 1) – solange nicht zu viele Schreiber wechseln.</summary>
    public double WechselJeMinute => IstAktiv
        ? Daten.Wechsel.Gebuehr * Marktvolumen / _w.Daten.Allgemein.Arbeitsminuten / Daten.Wechsel.BedarfSchreiber
        : 0;

    /// <summary>
    /// Was eine Arbeitsminute eines Schreibers erwartungsgemäß einbringt (nach Steuer und Ausfall, mit voller Kasse) –
    /// für Kalkulation, Personal und KI.
    /// </summary>
    public double WertJeMinute(int stufe)
    {
        if (!IstAktiv) return 0;
        var zins = Daten.Rahmen.JeSchreiber * Stufenwert(Daten.Rahmen.JeStufe, stufe) * Daten.Stadtzins / _w.Daten.Allgemein.Arbeitsminuten;
        var ausfall = Daten.Rahmen.JeSchreiber * Stufenwert(Daten.Rahmen.JeStufe, stufe) * Daten.Ausfall.Chance * Daten.Ausfall.Anteil / _w.Daten.Allgemein.Arbeitsminuten;
        return (zins + WechselJeMinute * Stufenwert(Daten.Wechsel.JeStufe, stufe)) * (1 - _w.Umsatzsteuer) - ausfall;
    }

    /// <summary>Wie <see cref="WertJeMinute(int)"/>, mit der Einrichtung dieses Betriebs (Rahmen, Wechselgebühr, Ausfall).</summary>
    public double WertJeMinute(Betrieb b)
    {
        if (!IstAktiv) return 0;
        var rahmen = RahmenJeSchreiber(b) / _w.Daten.Allgemein.Arbeitsminuten;
        var zins = rahmen * Daten.Stadtzins;
        var ausfall = rahmen * Daten.Ausfall.Chance * Daten.Ausfall.Anteil * b.AusfallFaktor;
        var wechsel = WechselJeMinute * Stufenwert(Daten.Wechsel.JeStufe, b.Stufe) * b.WechselFaktor;
        return (zins + wechsel) * (1 - _w.Umsatzsteuer) - ausfall;
    }

    /// <summary>
    /// Was ein gelungener Diebstahl oder Einbruch aus der Kasse dieses Bankbetriebs holt: Kasse × <c>einbruch.anteil</c>, höchstens
    /// <c>einbruch.hoechstens</c> (0 bei anderen Betrieben).
    /// </summary>
    public double Kassenbeute(Betrieb b) => IstBankbetrieb(b) ? Math.Min(Kasse(b) * Daten.Einbruch.Anteil, Daten.Einbruch.Hoechstens) : 0;

    /// <summary>Was der <paramref name="nter"/> gelungene Griff einer Nacht (1 = der erste) aus der Kasse holt – jeder nimmt seinen Anteil vom Rest.</summary>
    public double Kassenbeute(Betrieb b, int nter)
    {
        if (!IstBankbetrieb(b)) return 0;
        double kasse = Kasse(b), betrag = 0;
        for (var i = 0; i < nter; i++)
        {
            betrag = Math.Min(kasse * Daten.Einbruch.Anteil, Daten.Einbruch.Hoechstens);
            kasse -= betrag;
        }
        return betrag;
    }

    /// <summary>Diebe holen <see cref="Kassenbeute"/> aus der Kasse – das Geld ist fort (Forderungen des Hauses sinken). Gibt den Betrag zurück.</summary>
    internal double KasseBestehlen(Betrieb b)
    {
        var betrag = Kassenbeute(b);
        if (betrag <= 0) return 0;
        _kassen[b.Name] = Kasse(b) - betrag;
        b.Konto.ForderungGestohlen(betrag);
        Buchen(b.Konto, x => x with { Gestohlen = x.Gestohlen + betrag });
        return betrag;
    }

    /// <summary>
    /// Was ein Stück Einrichtung mit dieser Wirkung dem Bankbetrieb im Jahr mehr bringt, mit den Schreibern und der Kasse von jetzt
    /// (für <see cref="Werkstattwesen.ErwarteterMehrgewinnJeJahr"/>): mehr Rahmen bringt Zins nur, so weit die Kasse ihn füllt;
    /// die Wechselgebühr nach dem jetzigen Deckel der Stadt; die Pfandkammer mindert den erwarteten Ausfall der Kasse
    /// (Kasse × Chance × Anteil). Zins und Wechsel nach Steuer.
    /// </summary>
    internal double MehrgewinnJeJahr(Betrieb b, EinrichtungWirkung wi)
    {
        if (!IstBankbetrieb(b)) return 0;
        double Leistung(Betrieb x) => x.LeistungProMinute * _w.VolksFaktor(x.Konto, x.Artdaten.Beruf);
        var leistung = Leistung(b);
        var kasse = Kasse(b);
        var rahmen = RahmenJeSchreiber(b) * leistung;
        var zins = (Math.Min(kasse, rahmen * wi.RahmenFaktor) - Math.Min(kasse, rahmen)) * Daten.Stadtzins
                   * b.Arbeitsminuten / _w.Daten.Allgemein.Arbeitsminuten;
        var gewicht = _w.Betriebe.Where(x => IstBankbetrieb(x) && x.Konto.IstAktiv).Sum(x => Leistung(x) * Stufenwert(Daten.Wechsel.JeStufe, x.Stufe));
        var deckel = gewicht > 0 ? Math.Min(1, Daten.Wechsel.BedarfSchreiber / gewicht) : 0;
        var wechsel = b.Arbeitsminuten * leistung * Stufenwert(Daten.Wechsel.JeStufe, b.Stufe) * WechselJeMinute * deckel * b.WechselFaktor
                      * (wi.WechselFaktor - 1);
        var ausfall = kasse * Daten.Ausfall.Chance * Daten.Ausfall.Anteil * b.AusfallFaktor * (1 - wi.AusfallFaktor);
        return (zins + wechsel) * (1 - _w.Umsatzsteuer) + ausfall;
    }

    /// <summary>
    /// Faktor auf den Verlust, wenn ein Haus mit einem Kredit dieses Geldleihers aus dem Spiel geht: die beste Pfandkammer
    /// seiner Geldleihen (auch ohne Schreiber – das Pfand liegt im Haus); ohne 1.
    /// </summary>
    public double AusfallFaktor(Konto geber) =>
        _w.Betriebe.Where(b => b.Konto == geber && IstBankbetrieb(b)).Select(b => b.AusfallFaktor).DefaultIfEmpty(1).Min();

    /// <summary>Zusätzliche AP je Runde für die beste eigene Geldleihe.</summary>
    public int ApBonus(Konto k) => IstAktiv && k.IstAktiv && _w.Betriebe.Where(b => b.Konto == k && IstBankbetrieb(b)).Select(b => b.Stufe).DefaultIfEmpty(0).Max() is var s and > 0
        ? Stufenwert(Daten.ApJeStufe, s)
        : 0;

    public IReadOnlyList<Kredit> Kredite => _kredite;
    public IEnumerable<Kredit> KrediteVon(Konto geber) => _kredite.Where(c => c.Geber == geber);
    public Kredit? KreditVon(Konto nehmer) => _kredite.FirstOrDefault(c => c.Nehmer == nehmer);

    /// <summary>Ansehensabzug, solange ein Wucherkredit des Hauses läuft.</summary>
    public double AnsehenAbzug(Konto k) => IstAktiv && _kredite.Any(c => c.Geber == k && c.IstWucher) ? Daten.Wucher.Ansehen : 0;

    public bool IstWucher(double satz) => IstAktiv && satz > Daten.Wucher.Ab + 1e-9;

    /// <summary>
    /// Faktor auf den Höchstbetrag der Kredite an Häuser: der beste Kassenschrank der Geldleihen von <paramref name="geber"/>
    /// (auch ohne Schreiber, wie die Pfandkammer); ohne 1.
    /// </summary>
    public double KreditFaktor(Konto geber) =>
        _w.Betriebe.Where(b => b.Konto == geber && IstBankbetrieb(b)).Select(b => b.KreditFaktor).DefaultIfEmpty(1).Max();

    /// <summary>
    /// So viel darf <paramref name="geber"/> insgesamt an Häuser verleihen: <c>kredit.hoechstJeStufe</c> nach der Stufe seines
    /// Kontors × <see cref="KreditFaktor(Konto)"/> (0 ohne Kontor).
    /// </summary>
    public double Kredithoechstbetrag(Konto geber) =>
        Kontor(geber) is { } b ? Stufenwert(Daten.Kredit.HoechstJeStufe, b.Stufe) * KreditFaktor(geber) : 0;

    /// <summary>So viel darf <paramref name="geber"/> noch an Häuser verleihen.</summary>
    public double FreierKreditrahmen(Konto geber)
    {
        if (Kontor(geber) is null) return 0;
        var hoechst = Kredithoechstbetrag(geber) - KrediteVon(geber).Sum(c => c.Rest);
        return Math.Max(0, Math.Min(hoechst, geber.Vermoegen));
    }

    /// <summary>So viel bekäme <paramref name="nehmer"/> bei <paramref name="geber"/> höchstens.</summary>
    public double Hoechstkredit(Konto geber, Konto nehmer) =>
        Math.Max(0, Math.Min(FreierKreditrahmen(geber), Daten.Kredit.KreditwuerdigAnteil * nehmer.Reinvermoegen));

    /// <summary>Warum <paramref name="nehmer"/> bei <paramref name="geber"/> nicht leihen kann (null = er kann).</summary>
    public string? KreditVerboten(Konto geber, Konto nehmer, double betrag, int jahre)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Geldleiher.";
        if (geber == nehmer) return "Bei sich selbst leiht man kein Geld.";
        if (!geber.IstAktiv || Kontor(geber) is null) return $"{geber.Inhaber} verleiht kein Geld – es braucht eine Geldleihe mit Schreibern.";
        if (KreditVon(nehmer) is { } alt) return $"{nehmer.Inhaber} hat schon einen Kredit bei {alt.Geber.Inhaber}.";
        if (jahre < 1 || jahre > Daten.Kredit.LaufzeitHoechstens) return $"Die Laufzeit muss zwischen 1 und {Daten.Kredit.LaufzeitHoechstens} Jahren liegen.";
        if (betrag < 1) return "Der Betrag muss mindestens 1 Gulden sein.";
        var hoechst = Hoechstkredit(geber, nehmer);
        if (betrag > hoechst + 1e-6)
            return $"{geber.Inhaber} leiht {nehmer.Inhaber} höchstens {Zahlenformat.Ganz(hoechst)} {_w.Daten.Allgemein.Waehrung}.";
        return null;
    }

    // ── Kasse und Zinssatz (Befehle des Geldleihers) ─────────────────────────

    internal Befehlsergebnis KasseEinlegen(Betrieb b, double betrag)
    {
        if (!IstBankbetrieb(b)) return Befehlsergebnis.Abgelehnt($"{b.Name} ist keine Geldleihe.");
        if (betrag <= 0) return Befehlsergebnis.Abgelehnt("Der Betrag muss größer als 0 sein.");
        if (b.Konto.Vermoegen < betrag) return Befehlsergebnis.Abgelehnt($"So viel Bargeld hat {b.Konto.Inhaber} nicht.");
        b.Konto.Verleihen(betrag);
        _kassen[b.Name] = Kasse(b) + betrag;
        return Befehlsergebnis.Ok($"{b.Name}: {Zahlenformat.Ganz(betrag)} {_w.Daten.Allgemein.Waehrung} in die Kasse gelegt – jetzt {Zahlenformat.Ganz(Kasse(b))}.");
    }

    internal Befehlsergebnis KasseAbheben(Betrieb b, double betrag)
    {
        if (!IstBankbetrieb(b)) return Befehlsergebnis.Abgelehnt($"{b.Name} ist keine Geldleihe.");
        if (betrag <= 0) return Befehlsergebnis.Abgelehnt("Der Betrag muss größer als 0 sein.");
        betrag = Math.Min(betrag, Kasse(b));
        if (betrag <= 0) return Befehlsergebnis.Abgelehnt($"Die Kasse von {b.Name} ist leer.");
        b.Konto.Zurueckerhalten(betrag);
        _kassen[b.Name] = Kasse(b) - betrag;
        if (_kassen[b.Name] < 0.005) _kassen.Remove(b.Name);
        return Befehlsergebnis.Ok($"{b.Name}: {Zahlenformat.Ganz(betrag)} {_w.Daten.Allgemein.Waehrung} aus der Kasse genommen.");
    }

    /// <summary>Die Geldleihe wird verkauft: Die Kasse geht ganz zurück ins Bargeld (sie ist Geld des Hauses, kein Erlös).</summary>
    internal double KasseAufloesen(Betrieb b)
    {
        var kasse = Kasse(b);
        if (kasse > 0) b.Konto.Zurueckerhalten(kasse);
        _kassen.Remove(b.Name);
        return kasse;
    }

    internal Befehlsergebnis ZinsSetzen(Betrieb b, double satz)
    {
        if (!IstBankbetrieb(b)) return Befehlsergebnis.Abgelehnt($"{b.Name} ist keine Geldleihe.");
        if (satz < Daten.Kredit.ZinsMindestens - 1e-9 || satz > Daten.Kredit.ZinsHoechstens + 1e-9)
            return Befehlsergebnis.Abgelehnt($"Der Zins muss zwischen {Zahlenformat.Prozent(Daten.Kredit.ZinsMindestens)} und {Zahlenformat.Prozent(Daten.Kredit.ZinsHoechstens)} liegen.");
        _zinssaetze[b.Konto] = satz;
        return Befehlsergebnis.Ok($"{b.Konto.Inhaber} verleiht Geld an Häuser jetzt zu {Zahlenformat.Prozent(satz)} im Jahr" +
                                  (IstWucher(satz) ? " – das ist Wucher." : "."));
    }

    // ── Kredite an Häuser ───────────────────────────────────────────────────

    internal Befehlsergebnis KreditAufnehmen(Konto nehmer, Konto geber, double betrag, int jahre)
    {
        if (KreditVerboten(geber, nehmer, betrag, jahre) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var satz = Zinssatz(geber);
        var kredit = new Kredit
        {
            Nummer = _naechsterKredit++, Geber = geber, Nehmer = nehmer, Betrag = betrag, Rest = betrag,
            Satz = satz, JahreOffen = jahre, Jahr = _w.Jahr, IstWucher = IstWucher(satz),
        };
        _kredite.Add(kredit);
        geber.Verleihen(betrag);
        nehmer.KreditErhalten(betrag);
        var waehrung = _w.Daten.Allgemein.Waehrung;
        if (kredit.IstWucher && _w.Justiz.IstAktiv
            && _w.Justiz.TatMelden(geber, Vergehen.Wucher, nehmer.Inhaber, (satz - Daten.Wucher.Ab) * betrag * jahre) is { } spur)
            _w.Justiz.BeweisGeben(nehmer, spur); // der Schuldschein ist der Beweis
        var text = $"{nehmer.Inhaber} leiht {Zahlenformat.Ganz(betrag)} {waehrung} bei {geber.Inhaber} – {Zahlenformat.Prozent(satz)} Zins, {jahre} {(jahre == 1 ? "Jahr" : "Jahre")}.";
        _w.Melden(text);
        return Befehlsergebnis.Ok(text + (kredit.IstWucher ? " Ein Wucherzins – der Schuldschein ist ein Beweis." : ""));
    }

    /// <summary>Was es kostet, den Kredit jetzt ganz zurückzuzahlen.</summary>
    public double Abloesung(Kredit k) => k.Rest + k.Aufgelaufen;

    internal Befehlsergebnis KreditTilgen(Konto nehmer)
    {
        if (KreditVon(nehmer) is not { } k) return Befehlsergebnis.Abgelehnt($"{nehmer.Inhaber} hat keinen Kredit.");
        var summe = Abloesung(k);
        if (nehmer.Vermoegen < summe)
            return Befehlsergebnis.Abgelehnt($"Zum Ablösen braucht {nehmer.Inhaber} {Zahlenformat.Ganz(summe)} {_w.Daten.Allgemein.Waehrung} Bargeld.");
        Zahlen(k, k.Rest);
        _kredite.Remove(k);
        var text = $"{nehmer.Inhaber} hat den Kredit bei {k.Geber.Inhaber} abgelöst ({Zahlenformat.Ganz(summe)} {_w.Daten.Allgemein.Waehrung}).";
        _w.Melden(text);
        return Befehlsergebnis.Ok(text);
    }

    /// <summary>Zahlt die aufgelaufenen Zinsen und <paramref name="tilgung"/>.</summary>
    private void Zahlen(Kredit k, double tilgung)
    {
        var zins = k.Aufgelaufen;
        k.Aufgelaufen = 0;
        if (zins > 0)
        {
            k.Nehmer.Zinsen(zins);
            k.Geber.Umsatz(zins);
            k.Geber.Steuer(zins * _w.Umsatzsteuer);
            Buchen(k.Geber, x => x with { Hauszinsen = x.Hauszinsen + zins });
        }
        tilgung = Math.Min(tilgung, k.Rest);
        k.Nehmer.KreditTilgen(tilgung);
        k.Geber.Zurueckerhalten(tilgung);
        k.Rest -= tilgung;
    }

    /// <summary>Reicht das Bargeld nicht, holt der Büttel Lagerware des Schuldners – zum Pfandpreis an den Schuldner verkauft.</summary>
    private double Pfaenden(Konto nehmer, double fehlt)
    {
        double erloes = 0;
        foreach (var b in _w.Betriebe.Where(b => b.Konto == nehmer).OrderBy(b => b.Name, StringComparer.Ordinal).ToList())
            foreach (var (ware, stueck) in b.Lager.OrderByDescending(x => _w.Markt[x.Key].Preis).ThenBy(x => x.Key, StringComparer.Ordinal).ToList())
            {
                if (erloes >= fehlt) break;
                var preis = _w.Markt[ware].Preis * Daten.Kredit.PfandAnteil;
                if (preis <= 0) continue;
                var noetig = Math.Min(stueck, (int)Math.Ceiling((fehlt - erloes) / preis));
                b.Auslagern(ware, noetig);
                erloes += noetig * preis;
            }
        if (erloes > 0) nehmer.Umsatz(erloes); // Pfandverkauf: wie ein Verkauf weit unter Preis
        return erloes;
    }

    // ── Jede volle Stunde ───────────────────────────────────────────────────

    internal void Stunde(Spielzeit zeit)
    {
        if (!IstAktiv) return;
        var steuer = _w.Umsatzsteuer;
        var proMinute = WechselJeMinute;
        var arbeit = new List<(Betrieb B, double Minuten)>();
        foreach (var b in _w.Betriebe.Where(IstBankbetrieb).ToList())
        {
            var minuten = b.ArbeitsminutenAbholen();
            if (minuten > 0 && b.Konto.IstAktiv) arbeit.Add((b, minuten));
        }
        // Wechsel: Mehr als der Bedarf der Stadt bringt nichts – dann teilen sich alle Schreiber die Gebühr.
        var gewicht = arbeit.Sum(a => a.Minuten * Stufenwert(Daten.Wechsel.JeStufe, a.B.Stufe));
        var deckel = gewicht > 0 ? Math.Min(1, Daten.Wechsel.BedarfSchreiber * 60 / gewicht) : 0;
        foreach (var (b, minuten) in arbeit)
        {
            // Die Schreiber betreuen in dieser Stunde den Rahmen ihrer Arbeitszeit; Zins je Jahr auf die Arbeitsminuten verteilt.
            // Die Einrichtung wirkt nur auf diesen Betrieb: Rechenbretter und Hauptbücher auf den Rahmen, die Goldwaage auf die
            // Gebühr (nach dem Deckel – sie nimmt den anderen Geldleihern keinen Wechsel weg).
            var rahmen = RahmenJeSchreiber(b) * minuten / 60;
            var zins = Math.Min(Kasse(b), rahmen) * Daten.Stadtzins * 60 / _w.Daten.Allgemein.Arbeitsminuten;
            var wechsel = minuten * Stufenwert(Daten.Wechsel.JeStufe, b.Stufe) * proMinute * deckel * b.WechselFaktor;
            if (zins + wechsel <= 0) continue;
            b.UmsatzBuchen(zins + wechsel);
            b.SteuerBuchen((zins + wechsel) * steuer);
            Buchen(b.Konto, x => x with { Stadtzinsen = x.Stadtzinsen + zins, Wechsel = x.Wechsel + wechsel });
        }
        if (zeit.MinuteImTag != 0) // die letzte Stunde des Jahres rechnet das Jahresende ab
            ZinsenAuflaufen();
    }

    /// <summary>Eine Stunde Zinsen auf alle Kredite – ein Spieljahr hat <see cref="Spielzeit.StundenProTag"/> Stunden.</summary>
    private void ZinsenAuflaufen()
    {
        foreach (var k in _kredite)
            k.Aufgelaufen += k.Rest * k.Satz / Spielzeit.StundenProTag;
    }

    // ── Jahresende (vor dem Abschluss der Jahresrechnung) ────────────────────

    internal void Jahresende()
    {
        if (!IstAktiv) return;
        ZinsenAuflaufen(); // die letzte Stunde des Jahres
        var waehrung = _w.Daten.Allgemein.Waehrung;
        // Stadtkunden fallen aus. Die Pfandkammer mindert den Verlust, nicht die Chance (gleicher Zufall mit und ohne).
        foreach (var b in _w.Betriebe.Where(b => IstBankbetrieb(b) && Kasse(b) > 0).OrderBy(b => b.Name, StringComparer.Ordinal).ToList())
        {
            if (_zufall.NextDouble() >= Daten.Ausfall.Chance) continue;
            var verlust = Kasse(b) * Daten.Ausfall.Anteil * b.AusfallFaktor;
            _kassen[b.Name] = Kasse(b) - verlust;
            b.Konto.ForderungAbschreiben(verlust);
            Buchen(b.Konto, x => x with { Ausfall = x.Ausfall + verlust });
            _w.Melden($"{b.Name}: Ein Schuldner aus der Stadt ist auf und davon – {Zahlenformat.Ganz(verlust)} {waehrung} sind verloren.");
        }
        // Kredite an Häuser: Zins und Rate.
        foreach (var k in _kredite.OrderBy(c => c.Nummer).ToList())
        {
            if (!k.Nehmer.IstAktiv)
            {
                // Mit einer Pfandkammer bleibt dem Geldleiher das Pfand: Dieser Teil des Rests kommt zurück ins Bargeld.
                var verlust = k.Rest * AusfallFaktor(k.Geber);
                var pfand = k.Rest - verlust;
                if (pfand > 0) k.Geber.Zurueckerhalten(pfand);
                k.Geber.ForderungAbschreiben(verlust);
                Buchen(k.Geber, x => x with { Ausfall = x.Ausfall + verlust });
                _w.Melden($"{k.Nehmer.Inhaber} ist aus dem Spiel – {k.Geber.Inhaber} verliert {Zahlenformat.Ganz(verlust)} {waehrung} Kredit" +
                          (pfand > 0 ? $" (das Pfand bringt {Zahlenformat.Ganz(pfand)})." : "."));
                _kredite.Remove(k);
                continue;
            }
            var tilgung = k.Tilgungsrate;
            var faellig = k.Aufgelaufen + tilgung;
            if (k.Nehmer.Vermoegen < faellig)
            {
                var erloes = Pfaenden(k.Nehmer, faellig - Math.Max(0, k.Nehmer.Vermoegen));
                if (erloes > 0)
                    _w.Melden($"Der Büttel pfändet bei {k.Nehmer.Inhaber} Ware für {Zahlenformat.Ganz(erloes)} {waehrung} – die Rate an {k.Geber.Inhaber} war fällig.");
            }
            Zahlen(k, tilgung); // was dann noch fehlt, macht das Bargeld negativ – Schulden bei der Stadt
            k.JahreOffen--;
            if (k.Rest <= 0.005)
            {
                _kredite.Remove(k);
                _w.Melden($"{k.Nehmer.Inhaber} hat den Kredit bei {k.Geber.Inhaber} getilgt.");
            }
        }
        _vorjahr.Clear();
        foreach (var (konto, buch) in _diesesJahr) _vorjahr[konto] = buch;
        _diesesJahr.Clear();
    }

    // ── Kunden und Schreiber (für die Darstellung, nur lesend) ──────────────

    /// <summary>So viele Kaufleute haben am Wechseltisch Platz.</summary>
    public const int HoechstensKaufleute = 4;

    /// <summary>So viele Bürger haben am Schalter Platz.</summary>
    public const int HoechstensBuerger = 2;

    /// <summary>So viele vornehme Kunden haben im Empfang Platz (nur Stufe 3).</summary>
    public const int HoechstensVornehme = 3;

    /// <summary>Wechselumsatz je Stunde, für den ein Kaufmann am Tisch steht (Gulden).</summary>
    public const double UmsatzJeKaufmann = 10_000;

    /// <summary>Chance je Viertelstunde und Platz am Schalter, dass ein Bürger da ist – bei voll genutzter Kasse.</summary>
    public const double BuergerChance = 0.5;

    /// <summary>Chance je Stunde, dass ein Haus mit Kredit bei diesem Geldleiher im Empfang sitzt.</summary>
    public const double KreditnehmerChance = 0.5;

    /// <summary>Chance je Stunde, dass ein Patrizier im Empfang sitzt.</summary>
    public const double PatrizierChance = 0.25;

    private const double GoldenerSchnitt = 0.6180339887498949;
    private const double Wurzel2 = 0.4142135623730951; // Nachkommateil von √2: eine zweite, unabhängige Folge

    private static double Nachkomma(double x) => x - Math.Floor(x);

    /// <summary>Ruht die Arbeit (Umbau, nach einem Anschlag, Haus aus dem Spiel), gibt es keine Kunden.</summary>
    private bool ImDienst(Betrieb b) => IstBankbetrieb(b) && b.Belegt > 0 && b.Konto.IstAktiv && b.UmbauMinuten == 0 && b.SabotageMinuten == 0;

    /// <summary>Andrang am Wechseltisch je Stunde: Kaufleute nach Tageszeit – vormittags ist Markt.</summary>
    private static double Marktgewicht(int stunde) => stunde switch
    {
        >= 9 and < 13 => 1.5,
        >= 17 => 0.5,
        _ => 1,
    };

    /// <summary>
    /// Wie viele Kaufleute im Mittel einer Arbeitsstunde an diesem Wechseltisch stehen: der Umsatz des Marktes je Stunde,
    /// davon der Anteil dieser Schreiber am Wechsel der Stadt (wie in der Simulation: Schreiber × Stufenfaktor, geteilt
    /// durch die übrigen Geldleiher oder mindestens den Bedarf), geteilt durch <see cref="UmsatzJeKaufmann"/>.
    /// </summary>
    private double Kaufleute(Betrieb b)
    {
        double Gewicht(Betrieb x) => x.LeistungProMinute * Stufenwert(Daten.Wechsel.JeStufe, x.Stufe);
        var alle = _w.Betriebe.Where(ImDienst).Sum(Gewicht);
        var anteil = Gewicht(b) / Math.Max(alle, Daten.Wechsel.BedarfSchreiber);
        return Marktvolumen / (_w.Daten.Allgemein.Arbeitsminuten / 60.0) * anteil / UmsatzJeKaufmann;
    }

    /// <summary>
    /// Wer gerade Kunde in dieser Geldleihe ist – zur aktuellen Uhrzeit, ohne Zufall, ändert nichts. Leer außerhalb der
    /// Arbeitszeit (8–20 Uhr), ohne Schreiber, wenn das Haus nicht mehr spielt und solange die Arbeit ruht (Umbau, Anschlag).
    /// Regeln (Stunde h, Viertelstunde q seit Mitternacht):
    ///   • Kaufleute am Wechseltisch: <see cref="Kaufleute"/> × Tageszeit (9–13 Uhr Markt × 1,5, ab 17 Uhr × 0,5, sonst × 1),
    ///     gerundet, mindestens 1, höchstens <see cref="HoechstensKaufleute"/> – keine, wenn der Markt nichts umsetzt.
    ///   • Bürger am Schalter: Platz 0 ist besetzt, wenn der Nachkommateil von q × 0,618… unter c liegt, Platz 1 mit
    ///     q × 0,414…; c = <see cref="BuergerChance"/> × genutzte Kasse (Kasse bis zum Rahmen ÷ Rahmen bei voller Besetzung).
    ///     Unter c ÷ 2 leiht er, sonst zahlt er zurück. Leere Kasse: keine Bürger.
    ///   • Vornehme im Empfang (nur Stufe 3, nur im Kontor des Hauses): zuerst die Häuser mit Kredit bei diesem Geldleiher
    ///     (nach Kreditnummer), dann die Patrizier der Stadt; der i-te ist da, wenn der Nachkommateil von
    ///     h × 0,618… + i × 0,414… unter <see cref="KreditnehmerChance"/> bzw. <see cref="PatrizierChance"/> liegt;
    ///     höchstens <see cref="HoechstensVornehme"/>.
    /// Die Simulation rechnet Zins und Wechsel unverändert – die Kunden sind nur fürs Bild.
    /// </summary>
    public IReadOnlyList<Bankkunde> KundenJetzt(Betrieb b)
    {
        var uhr = _w.Uhr.UhrzeitInMinuten;
        if (!ImDienst(b) || !b.IstArbeitszeit(uhr)) return Array.Empty<Bankkunde>();
        var h = uhr / 60;
        var q = uhr / 15;
        var liste = new List<Bankkunde>();

        var kaufleute = Kaufleute(b) * Marktgewicht(h);
        var n = kaufleute <= 0 ? 0 : Math.Clamp((int)Math.Round(kaufleute, MidpointRounding.AwayFromZero), 1, HoechstensKaufleute);
        for (var i = 0; i < n; i++) liste.Add(new Bankkunde(Kundenart.Kaufmann, i, Kundenanliegen.Wechseln, null));

        var voll = RahmenVoll(b);
        var genutzt = voll > 0 ? Math.Min(1, Math.Min(Kasse(b), Rahmen(b)) / voll) : 0;
        var c = BuergerChance * genutzt;
        for (var platz = 0; platz < HoechstensBuerger; platz++)
        {
            var v = Nachkomma(q * (platz == 0 ? GoldenerSchnitt : Wurzel2));
            if (v < c) liste.Add(new Bankkunde(Kundenart.Buerger, platz, v < c / 2 ? Kundenanliegen.Leihen : Kundenanliegen.Zurueckzahlen, null));
        }

        if (b.Stufe >= 3 && Kontor(b.Konto) == b)
        {
            var reich = _w.Daten.StandStufe("Patrizier");
            var kandidaten = KrediteVon(b.Konto).Where(k => k.Nehmer.IstAktiv).OrderBy(k => k.Nummer)
                .Select(k => (Haus: k.Nehmer, Anliegen: Kundenanliegen.Kredit, Chance: KreditnehmerChance)).ToList();
            if (reich >= 0)
                kandidaten.AddRange(_w.Konten.Where(k => k.IstAktiv && k != b.Konto && k.Stand >= reich && kandidaten.All(x => x.Haus != k))
                    .Select(k => (Haus: k, Anliegen: Kundenanliegen.Beraten, Chance: PatrizierChance)));
            var platz = 0;
            for (var i = 0; i < kandidaten.Count && platz < HoechstensVornehme; i++)
                if (Nachkomma(h * GoldenerSchnitt + i * Wurzel2) < kandidaten[i].Chance)
                    liste.Add(new Bankkunde(Kundenart.Vornehm, platz++, kandidaten[i].Anliegen, kandidaten[i].Haus));
        }
        return liste;
    }

    /// <summary>
    /// Was jeder Schreiber dieser Geldleihe gerade tut – zur aktuellen Uhrzeit, ohne Zufall, ändert nichts. Leer außerhalb der
    /// Arbeitszeit (dann sind sie daheim) und ohne Schreiber. Regeln (Stunde h):
    ///   • Bedienen: Je Art Kunden, die gerade da sind (<see cref="KundenJetzt"/>), bedient sie einer – erst der Empfang, dann
    ///     der Wechseltisch, dann der Schalter; Gesellen vor Lehrlingen, dann nach Nummer.
    ///   • Die übrigen (j = Platz unter ihnen, nach Nummer) reihum Rechnen, Wiegen, Zählen (ab Stufe 2 Rechnen, Wiegen,
    ///     Hauptbuch, Zählen) – Eintrag (j + h) der Reihe, jede Stunde rückt sie weiter.
    /// </summary>
    public IReadOnlyList<Schreiberstand> SchreiberJetzt(Betrieb b)
    {
        var uhr = _w.Uhr.UhrzeitInMinuten;
        if (!IstBankbetrieb(b) || b.Belegt == 0 || !b.IstArbeitszeit(uhr)) return Array.Empty<Schreiberstand>();
        var h = uhr / 60;
        var kunden = KundenJetzt(b);
        var gruppen = new[] { Kundenart.Vornehm, Kundenart.Kaufmann, Kundenart.Buerger }.Where(a => kunden.Any(k => k.Art == a)).ToList();
        var bediener = new Dictionary<Arbeiter, Kundenart>();
        var reihe = b.Arbeiter.OrderBy(a => _w.Personal.IstAnfaenger(a) ? 1 : 0).ThenBy(a => a.Nummer).ToList();
        for (var i = 0; i < Math.Min(gruppen.Count, reihe.Count); i++) bediener[reihe[i]] = gruppen[i];

        var reihum = b.Stufe >= 2 ? MitHauptbuch : OhneHauptbuch;
        var liste = new List<Schreiberstand>(b.Belegt);
        var j = 0;
        foreach (var a in b.Arbeiter.OrderBy(a => a.Nummer))
            liste.Add(bediener.TryGetValue(a, out var art)
                ? new Schreiberstand(a, Schreibertaetigkeit.Bedienen, art)
                : new Schreiberstand(a, reihum[(j++ + h) % reihum.Length], null));
        return liste;
    }

    private static readonly Schreibertaetigkeit[] OhneHauptbuch = { Schreibertaetigkeit.Rechnen, Schreibertaetigkeit.Wiegen, Schreibertaetigkeit.Zaehlen };
    private static readonly Schreibertaetigkeit[] MitHauptbuch =
        { Schreibertaetigkeit.Rechnen, Schreibertaetigkeit.Wiegen, Schreibertaetigkeit.Hauptbuch, Schreibertaetigkeit.Zaehlen };

    // ── Speichern ───────────────────────────────────────────────────────────

    internal BankStand Sichern(Func<Konto, int> nr) => new()
    {
        ZufallWuerfe = _zufall.Wuerfe,
        NaechsterKredit = _naechsterKredit,
        Kassen = _kassen.ToDictionary(x => x.Key, x => x.Value),
        Zinssaetze = _zinssaetze.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
        Kredite = _kredite.Select(k => new KreditStand
        {
            Nummer = k.Nummer, Geber = nr(k.Geber), Nehmer = nr(k.Nehmer), Betrag = k.Betrag, Rest = k.Rest, Satz = k.Satz,
            JahreOffen = k.JahreOffen, Aufgelaufen = k.Aufgelaufen, Jahr = k.Jahr,
        }).ToList(),
        DiesesJahr = _diesesJahr.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
        Vorjahr = _vorjahr.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
    };

    internal void Wiederherstellen(BankStand s, IReadOnlyList<Konto> k)
    {
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        _naechsterKredit = s.NaechsterKredit;
        foreach (var (name, betrag) in s.Kassen) _kassen[name] = betrag;
        foreach (var (nr, satz) in s.Zinssaetze) _zinssaetze[k[int.Parse(nr)]] = satz;
        foreach (var c in s.Kredite)
            _kredite.Add(new Kredit
            {
                Nummer = c.Nummer, Geber = k[c.Geber], Nehmer = k[c.Nehmer], Betrag = c.Betrag, Rest = c.Rest, Satz = c.Satz,
                JahreOffen = c.JahreOffen, Aufgelaufen = c.Aufgelaufen, Jahr = c.Jahr, IstWucher = IstWucher(c.Satz),
            });
        foreach (var (nr, buch) in s.DiesesJahr) _diesesJahr[k[int.Parse(nr)]] = buch;
        foreach (var (nr, buch) in s.Vorjahr) _vorjahr[k[int.Parse(nr)]] = buch;
    }
}
