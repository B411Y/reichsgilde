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

    /// <summary>Zusätzliche AP je Runde nach der besten Stufe (Wechselstube, Geldleihe, Bankhaus).</summary>
    public List<int> ApJeStufe { get; init; } = new() { 0, 1, 2 };

    public void Pruefen(List<string> f, IReadOnlyDictionary<string, BerufDaten> berufe)
    {
        if (berufe.Count > 0 && !berufe.ContainsKey(Beruf)) f.Add($"geldleiher.beruf: Den Beruf „{Beruf}“ gibt es nicht.");
        if (Rahmen.JeStufe.Count < 3 || Wechsel.JeStufe.Count < 3 || Kredit.HoechstJeStufe.Count < 3 || ApJeStufe.Count < 3)
            f.Add("geldleiher: rahmen.jeStufe, wechsel.jeStufe, kredit.hoechstJeStufe und apJeStufe brauchen je einen Wert für Stufe 1, 2 und 3.");
        if (Rahmen.JeSchreiber < 0 || Wechsel.BedarfSchreiber <= 0 || Rahmen.JeStufe.Any(x => x < 0) || Wechsel.JeStufe.Any(x => x < 0)
            || Kredit.HoechstJeStufe.Any(x => x < 0) || ApJeStufe.Any(x => x < 0) || Wucher.Ansehen < 0)
            f.Add("geldleiher: Rahmen, Bedarf, Höchstbeträge, AP und Ansehen dürfen nicht negativ sein (Bedarf größer als 0).");
        foreach (var (name, wert) in new[] { ("stadtzins", Stadtzins), ("ausfall.chance", Ausfall.Chance), ("ausfall.anteil", Ausfall.Anteil),
                     ("wechsel.gebuehr", Wechsel.Gebuehr), ("kredit.kreditwuerdigAnteil", Kredit.KreditwuerdigAnteil), ("kredit.pfandAnteil", Kredit.PfandAnteil),
                     ("kredit.zinsMindestens", Kredit.ZinsMindestens), ("kredit.zinsHoechstens", Kredit.ZinsHoechstens), ("wucher.ab", Wucher.Ab) })
            if (wert is < 0 or > 1) f.Add($"geldleiher.{name} muss zwischen 0 und 1 liegen.");
        if (Kredit.ZinsStandard < Kredit.ZinsMindestens || Kredit.ZinsStandard > Kredit.ZinsHoechstens)
            f.Add("geldleiher.kredit.zinsStandard muss zwischen zinsMindestens und zinsHoechstens liegen.");
        if (Kredit.LaufzeitHoechstens < 1) f.Add("geldleiher.kredit.laufzeitHoechstens muss mindestens 1 sein.");
    }
}

/// <summary>Was die Geldleihe eines Hauses in einem Jahr eingenommen und verloren hat.</summary>
public sealed record Bankbuch(double Stadtzinsen, double Wechsel, double Hauszinsen, double Ausfall);

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

    /// <summary>So viel Kapital können die Schreiber bei voller Besetzung (lauter Gesellen) betreuen.</summary>
    public double RahmenVoll(Betrieb b) => IstAktiv ? Daten.Rahmen.JeSchreiber * Stufenwert(Daten.Rahmen.JeStufe, b.Stufe) * b.Plaetze : 0;

    /// <summary>So viel Kapital betreuen die Schreiber mit der jetzigen Belegschaft.</summary>
    public double Rahmen(Betrieb b) => IstAktiv ? Daten.Rahmen.JeSchreiber * Stufenwert(Daten.Rahmen.JeStufe, b.Stufe) * b.LeistungProMinute : 0;

    /// <summary>Der Zins, den dieses Haus für Kredite an Häuser verlangt.</summary>
    public double Zinssatz(Konto k) => _zinssaetze.GetValueOrDefault(k, IstAktiv ? Daten.Kredit.ZinsStandard : 0);

    /// <summary>Umsatz des ganzen Marktes in einem Jahr (Angebot der letzten 24 Stunden × Preis, mit den auswärtigen Händlern).</summary>
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
        return (zins + WechselJeMinute * Stufenwert(Daten.Wechsel.JeStufe, stufe)) * (1 - _w.Daten.Allgemein.Umsatzsteuer) - ausfall;
    }

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

    /// <summary>So viel darf <paramref name="geber"/> noch an Häuser verleihen.</summary>
    public double FreierKreditrahmen(Konto geber)
    {
        if (Kontor(geber) is not { } b) return 0;
        var hoechst = Stufenwert(Daten.Kredit.HoechstJeStufe, b.Stufe) - KrediteVon(geber).Sum(c => c.Rest);
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

    internal Befehlsergebnis ZinsSetzen(Betrieb b, double satz)
    {
        if (!IstBankbetrieb(b)) return Befehlsergebnis.Abgelehnt($"{b.Name} ist keine Geldleihe.");
        if (satz < Daten.Kredit.ZinsMindestens - 1e-9 || satz > Daten.Kredit.ZinsHoechstens + 1e-9)
            return Befehlsergebnis.Abgelehnt($"Der Zins muss zwischen {Daten.Kredit.ZinsMindestens:P0} und {Daten.Kredit.ZinsHoechstens:P0} liegen.");
        _zinssaetze[b.Konto] = satz;
        return Befehlsergebnis.Ok($"{b.Konto.Inhaber} verleiht Geld an Häuser jetzt zu {satz:P0} im Jahr" +
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
        var text = $"{nehmer.Inhaber} leiht {Zahlenformat.Ganz(betrag)} {waehrung} bei {geber.Inhaber} – {satz:P0} Zins, {jahre} {(jahre == 1 ? "Jahr" : "Jahre")}.";
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
            k.Geber.Steuer(zins * _w.Daten.Allgemein.Umsatzsteuer);
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
        var steuer = _w.Daten.Allgemein.Umsatzsteuer;
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
            var rahmen = Daten.Rahmen.JeSchreiber * Stufenwert(Daten.Rahmen.JeStufe, b.Stufe) * minuten / 60;
            var zins = Math.Min(Kasse(b), rahmen) * Daten.Stadtzins * 60 / _w.Daten.Allgemein.Arbeitsminuten;
            var wechsel = minuten * Stufenwert(Daten.Wechsel.JeStufe, b.Stufe) * proMinute * deckel;
            if (zins + wechsel <= 0) continue;
            b.Konto.Umsatz(zins + wechsel);
            b.Konto.Steuer((zins + wechsel) * steuer);
            Buchen(b.Konto, x => x with { Stadtzinsen = x.Stadtzinsen + zins, Wechsel = x.Wechsel + wechsel });
        }
        if (zeit.MinuteImTag != 0) // die Stunde vor Mitternacht rechnet das Jahresende ab
            ZinsenAuflaufen();
    }

    /// <summary>Eine Stunde Zinsen auf alle Kredite – ein Spieljahr hat 24 Stunden.</summary>
    private void ZinsenAuflaufen()
    {
        foreach (var k in _kredite)
            k.Aufgelaufen += k.Rest * k.Satz / 24;
    }

    // ── Jahresende (vor dem Abschluss der Jahresrechnung) ────────────────────

    internal void Jahresende()
    {
        if (!IstAktiv) return;
        ZinsenAuflaufen(); // die letzte Stunde des Jahres
        var waehrung = _w.Daten.Allgemein.Waehrung;
        // Stadtkunden fallen aus.
        foreach (var b in _w.Betriebe.Where(b => IstBankbetrieb(b) && Kasse(b) > 0).OrderBy(b => b.Name, StringComparer.Ordinal).ToList())
        {
            if (_zufall.NextDouble() >= Daten.Ausfall.Chance) continue;
            var verlust = Kasse(b) * Daten.Ausfall.Anteil;
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
                k.Geber.ForderungAbschreiben(k.Rest);
                Buchen(k.Geber, x => x with { Ausfall = x.Ausfall + k.Rest });
                _w.Melden($"{k.Nehmer.Inhaber} ist aus dem Spiel – {k.Geber.Inhaber} verliert {Zahlenformat.Ganz(k.Rest)} {waehrung} Kredit.");
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
