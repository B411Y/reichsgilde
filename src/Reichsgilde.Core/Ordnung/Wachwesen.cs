using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Ordnung;

// ─────────────────────────────────────────────────────────────────────────────
//  Daten (Abschnitt „wache" in wirtschaft.json, ab Datenversion 19)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class SoldDaten
{
    /// <summary>Gulden je Arbeitsminute eines Gesellen (vor Steuer) – die Stadt zahlt.</summary>
    public double JeMinute { get; init; } = 0.75;

    /// <summary>Faktor je Gebäudestufe (Wachhaus, Stadtwache, Garnison).</summary>
    public List<double> JeStufe { get; init; } = new() { 1, 1.1, 1.3 };
}

public sealed class PatrouilleDaten
{
    /// <summary>So viel sinkt die Erfolgschance nächtlicher Taten in der Stadt je Wachmann (Geselle) im Dienst.</summary>
    public double SchutzJeWachmann { get; init; } = 0.025;

    /// <summary>Mehr Schutz geben auch viele Wachen nicht.</summary>
    public double Hoechstens { get; init; } = 0.4;

    /// <summary>Anteil des Schutzes, um den die Zeugenchance steigt (die Streife sieht mehr).</summary>
    public double ZeugenAnteil { get; init; } = 0.5;
}

public sealed class WachturmDaten
{
    /// <summary>Ein voll besetzter Wachturm senkt die Überfallchance auf allen Umlandwegen um diesen Anteil.</summary>
    public double Schutz { get; init; } = 0.3;
    public double Hoechstens { get; init; } = 0.6;
}

public sealed class RazziaDaten
{
    /// <summary>Razzien je Jahr nach Stufe.</summary>
    public List<int> JeStufe { get; init; } = new() { 1, 2, 3 };

    /// <summary>Chance je unbekannter Tat, einen Beweis zu finden – plus <see cref="ChanceJeStufe"/> je Stufe über 1.</summary>
    public double Chance { get; init; } = 0.5;
    public double ChanceJeStufe { get; init; } = 0.1;
}

public sealed class KopfgeldDaten
{
    /// <summary>Gewinnt ein Haus der Wache eine Anklage, zahlt die Stadt diesen Anteil der Buße als Kopfgeld.</summary>
    public double AnteilBusse { get; init; } = 0.25;

    /// <summary>Für jeden Taschendieb, den die Wache am Markt fasst.</summary>
    public double Taschendieb { get; init; } = 50;
}

public sealed class GeleitDaten
{
    /// <summary>Name der Eskorte (unter logistik.eskorten, mit „wache": true), die nur die Wache stellt.</summary>
    public string Eskorte { get; init; } = "Stadtwache";

    /// <summary>Ab dieser Stufe stellt ein Haus der Wache Geleitschutz.</summary>
    public int AbStufe { get; init; } = 1;
}

/// <summary>Der Gardist: Sold, Patrouille, Wachturm, Razzia, Kopfgeld, Geleitschutz.</summary>
public sealed class WacheDaten
{
    public string Beruf { get; init; } = "Gardist";

    /// <summary>So viel Ansehen gewinnt ein Haus, das einen Betrieb der Wache führt.</summary>
    public double Ansehen { get; init; } = 3;

    public SoldDaten Sold { get; init; } = new();
    public PatrouilleDaten Patrouille { get; init; } = new();
    public WachturmDaten Wachturm { get; init; } = new();
    public RazziaDaten Razzia { get; init; } = new();
    public KopfgeldDaten Kopfgeld { get; init; } = new();
    public GeleitDaten Geleit { get; init; } = new();

    public void Pruefen(List<string> f, IReadOnlyDictionary<string, BerufDaten> berufe, IReadOnlyDictionary<string, EskorteDaten> eskorten)
    {
        if (berufe.Count > 0 && !berufe.ContainsKey(Beruf)) f.Add($"wache.beruf: Den Beruf „{Beruf}“ gibt es nicht.");
        if (!eskorten.TryGetValue(Geleit.Eskorte, out var e) || !e.Wache)
            f.Add($"wache.geleit.eskorte: „{Geleit.Eskorte}“ muss unter logistik.eskorten stehen und \"wache\": true haben.");
        if (Sold.JeStufe.Count < 3 || Razzia.JeStufe.Count < 3) f.Add("wache: sold.jeStufe und razzia.jeStufe brauchen je einen Wert für Stufe 1, 2 und 3.");
        if (Sold.JeMinute < 0 || Ansehen < 0 || Kopfgeld.Taschendieb < 0 || Sold.JeStufe.Any(x => x < 0) || Razzia.JeStufe.Any(x => x < 0))
            f.Add("wache: Sold, Ansehen, Kopfgeld und Razzien dürfen nicht negativ sein.");
        foreach (var (name, wert) in new[] { ("patrouille.schutzJeWachmann", Patrouille.SchutzJeWachmann), ("patrouille.hoechstens", Patrouille.Hoechstens),
                     ("patrouille.zeugenAnteil", Patrouille.ZeugenAnteil), ("wachturm.schutz", Wachturm.Schutz), ("wachturm.hoechstens", Wachturm.Hoechstens),
                     ("razzia.chance", Razzia.Chance), ("razzia.chanceJeStufe", Razzia.ChanceJeStufe), ("kopfgeld.anteilBusse", Kopfgeld.AnteilBusse) })
            if (wert is < 0 or > 1) f.Add($"wache.{name} muss zwischen 0 und 1 liegen.");
        if (Geleit.AbStufe is < 1 or > 3) f.Add("wache.geleit.abStufe muss zwischen 1 und 3 liegen.");
    }
}

/// <summary>Was die Wache eines Hauses in einem Jahr verdient hat.</summary>
public sealed record Wachbuch(double Sold, double Kopfgeld, double Geleitgeld, int Razzien);

// ─────────────────────────────────────────────────────────────────────────────
//  Teilsystem
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Die Wache der Stadt – ein Beruf ohne Ware, das ehrliche Gegenstück zum Dieb:
///   • Sold: Jede volle Stunde zahlt die Stadt für die Arbeitszeit der Wachleute (versteuert, wie ein Umsatz).
///   • Patrouille (passiv): Wachleute in der Stadt senken nachts die Erfolgschance von Diebstahl, Sabotage,
///     Einbruch, Brandstiftung und Entführung in der Stadt und machen Zeugen häufiger – zum Schutz aller.
///   • Wachturm (Umland): senkt die Überfallchance der gewöhnlichen Banden auf allen Umlandwegen.
///   • Razzia (Befehl): sofort – Beweise für Taten des Hauses, Diebesgut wird beschlagnahmt.
///   • Kopfgeld: Gewinnt ein Haus der Wache eine Anklage, zahlt die Stadt einen Anteil der Buße; gefasste Taschendiebe bringen Kopfgeld.
///   • Geleitschutz: Die Eskorte „Stadtwache" gibt es nur, wenn ein Haus der Wache sie stellt – das Geld geht an dieses Haus.
///   • Ein Haus der Wache ist überall gern gesehen: mehr Ansehen.
/// Alle Wirkungen sind Faktoren ohne zusätzliche Würfe: Ohne Wache verläuft eine Partie genau wie zuvor.
/// </summary>
public sealed class Wachwesen
{
    private readonly Stadtwirtschaft _w;
    private readonly WacheDaten? _d;
    private Zufallsquelle _zufall;
    private readonly Dictionary<Konto, Wachbuch> _diesesJahr = new();
    private readonly Dictionary<Konto, Wachbuch> _vorjahr = new();

    public Wachwesen(Stadtwirtschaft w, WacheDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 53 + 31));
    }

    public bool IstAktiv => _d is not null;
    public WacheDaten Daten => _d ?? throw new InvalidOperationException("Es gibt keine Wache (wache fehlt in den Daten).");

    public Wachbuch DiesesJahr(Konto k) => _diesesJahr.GetValueOrDefault(k, new Wachbuch(0, 0, 0, 0));
    public Wachbuch? Vorjahr(Konto k) => _vorjahr.GetValueOrDefault(k);

    private void Buchen(Konto k, Func<Wachbuch, Wachbuch> aendern) => _diesesJahr[k] = aendern(DiesesJahr(k));

    // ── Abfragen ────────────────────────────────────────────────────────────

    /// <summary>Ein Betrieb der Wache (Wachhaus, Stadtwache, Garnison, Wachturm).</summary>
    public bool IstWachbetrieb(Betrieb b) => IstAktiv && b.OhneProduktion && b.Artdaten.Beruf == Daten.Beruf;

    /// <summary>Wachhäuser in der Stadt mit Leuten, deren Haus noch spielt.</summary>
    private IEnumerable<Betrieb> Wachhaeuser => _w.Betriebe.Where(b => IstWachbetrieb(b) && b.InDerStadt && b.Belegt > 0 && b.Konto.IstAktiv);

    /// <summary>Die beste eigene Wachstube in der Stadt (höchste Stufe, mit Leuten) – oder null.</summary>
    public Betrieb? Wachstube(Konto k) => Wachhaeuser.Where(b => b.Konto == k)
        .OrderByDescending(b => b.Stufe).ThenBy(b => b.Name, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>Häuser, die eine Wachstube mit Leuten führen.</summary>
    public IEnumerable<Konto> Wachhaushaeuser => Wachhaeuser.Select(b => b.Konto).Distinct();

    /// <summary>Gulden je Arbeitsminute eines Gesellen nach Steuer (für Kalkulation, Personal und KI).</summary>
    public double WertJeMinute(int stufe) => IstAktiv
        ? Daten.Sold.JeMinute * Daten.Sold.JeStufe[Math.Clamp(stufe - 1, 0, Daten.Sold.JeStufe.Count - 1)] * (1 - _w.Daten.Allgemein.Umsatzsteuer)
        : 0;

    /// <summary>Wie stark die Streife schützt: Anteil, um den nächtliche Taten in der Stadt seltener gelingen.</summary>
    public double Patrouilleschutz => IstAktiv
        ? Math.Min(Daten.Patrouille.Hoechstens, Daten.Patrouille.SchutzJeWachmann * Wachhaeuser.Sum(b => b.LeistungProMinute))
        : 0;

    /// <summary>Faktor auf die Erfolgschance einer Tat gegen diesen Betrieb (1 = keine Streife; im Umland wirkt sie nicht).</summary>
    public double Patrouillefaktor(Betrieb? b) => b is null || b.InDerStadt ? 1 - Patrouilleschutz : 1;

    /// <summary>So viel wahrscheinlicher ist ein Zeuge in der Stadt.</summary>
    public double ZeugenBonus(Betrieb? b) => b is not null && b.InDerStadt ? ZeugenBonusInDerStadt : 0;

    /// <summary>Zeugenbonus der Streife für alles in der Stadt (Betriebe in der Stadt, Wohnhäuser).</summary>
    public double ZeugenBonusInDerStadt => IstAktiv ? Patrouilleschutz * Daten.Patrouille.ZeugenAnteil : 0;

    /// <summary>Faktor auf die Überfallchance der Banden im Umland (Wachtürme mit Leuten).</summary>
    public double Landstrassenfaktor => IstAktiv
        ? 1 - Math.Min(Daten.Wachturm.Hoechstens, _w.Betriebe.Where(b => IstWachbetrieb(b) && !b.InDerStadt && b.Konto.IstAktiv)
            .Sum(b => Daten.Wachturm.Schutz * b.Belegt / (double)b.Plaetze))
        : 1;

    /// <summary>Ansehensbonus für ein Haus mit Betrieb der Wache.</summary>
    public double AnsehenBonus(Konto k) => IstAktiv && _w.Betriebe.Any(b => b.Konto == k && IstWachbetrieb(b)) ? Daten.Ansehen : 0;

    // ── Sold (jede volle Stunde) ────────────────────────────────────────────

    internal void Stunde(Spielzeit zeit)
    {
        if (!IstAktiv) return;
        var steuer = _w.Daten.Allgemein.Umsatzsteuer;
        foreach (var b in _w.Betriebe.Where(IstWachbetrieb).ToList())
        {
            var minuten = b.ArbeitsminutenAbholen();
            if (minuten <= 0 || !b.Konto.IstAktiv) continue;
            var sold = minuten * Daten.Sold.JeMinute * Daten.Sold.JeStufe[Math.Clamp(b.Stufe - 1, 0, Daten.Sold.JeStufe.Count - 1)];
            b.UmsatzBuchen(sold);
            b.SteuerBuchen(sold * steuer);
            Buchen(b.Konto, x => x with { Sold = x.Sold + sold });
        }
    }

    // ── Kopfgeld ────────────────────────────────────────────────────────────

    /// <summary>
    /// Die Wache fasst einen Taschendieb: Wer bekommt den Beweis? Ein Haus der Wache (nicht der Dieb selbst), sonst null.
    /// Die Wache erhält Kopfgeld. Ein Wurf aus dem eigenen Zufall – nur, wenn es Wachen gibt.
    /// </summary>
    internal Konto? TaschendiebGefasst(Konto dieb)
    {
        if (!IstAktiv) return null;
        var wachen = Wachhaushaeuser.Where(k => k != dieb).OrderBy(k => k.Inhaber, StringComparer.Ordinal).ToList();
        if (wachen.Count == 0) return null;
        var wache = wachen[_zufall.Next(wachen.Count)];
        wache.Umsatz(Daten.Kopfgeld.Taschendieb);
        Buchen(wache, x => x with { Kopfgeld = x.Kopfgeld + Daten.Kopfgeld.Taschendieb });
        return wache;
    }

    /// <summary>Nach einem Schuldspruch: Ist der Ankläger ein Haus der Wache, zahlt die Stadt Kopfgeld.</summary>
    internal double KopfgeldNachUrteil(Konto anklaeger, double busse)
    {
        if (!IstAktiv || Wachstube(anklaeger) is null) return 0;
        var kopfgeld = busse * Daten.Kopfgeld.AnteilBusse;
        anklaeger.Umsatz(kopfgeld);
        Buchen(anklaeger, x => x with { Kopfgeld = x.Kopfgeld + kopfgeld });
        return kopfgeld;
    }

    // ── Geleitschutz ────────────────────────────────────────────────────────

    /// <summary>Wer gerade Geleitschutz stellt: das Haus mit der höchsten Wachstube (ab der nötigen Stufe) – oder null.</summary>
    public Konto? Geleitgeber => IstAktiv
        ? Wachhaeuser.Where(b => b.Stufe >= Daten.Geleit.AbStufe)
            .OrderByDescending(b => b.Stufe).ThenByDescending(b => b.Belegt).ThenBy(b => b.Name, StringComparer.Ordinal)
            .Select(b => b.Konto).FirstOrDefault()
        : null;

    /// <summary>Gibt es diese Eskorte gerade? Die der Wache nur, wenn ein Haus sie stellt.</summary>
    public bool EskorteVerfuegbar(string name) =>
        _w.Daten.Logistik.Eskorten.TryGetValue(name, out var e) && (!e.Wache || Geleitgeber is not null);

    /// <summary>Ein Karren fährt mit der Stadtwache: Der Lohn geht an das Haus, das sie stellt.</summary>
    internal void GeleitBezahlen(Betrieb kunde, double kosten)
    {
        kunde.TransportBuchen(kosten);
        if (Geleitgeber is not { } wache) return;
        wache.Umsatz(kosten);
        Buchen(wache, x => x with { Geleitgeld = x.Geleitgeld + kosten });
    }

    // ── Razzia ──────────────────────────────────────────────────────────────

    /// <summary>Wie viele Razzien dieses Haus dieses Jahr noch machen darf.</summary>
    public int FreieRazzien(Konto k) =>
        Wachstube(k) is { } s ? Math.Max(0, Daten.Razzia.JeStufe[Math.Clamp(s.Stufe - 1, 0, Daten.Razzia.JeStufe.Count - 1)] - DiesesJahr(k).Razzien) : 0;

    public double Razziachance(Konto k) =>
        Math.Min(0.95, Daten.Razzia.Chance + Daten.Razzia.ChanceJeStufe * Math.Max(0, (Wachstube(k)?.Stufe ?? 1) - 1));

    public string? RazziaVerboten(Konto wache, Konto ziel)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Wache.";
        if (!_w.Justiz.IstAktiv) return "Ohne Gericht nützt keine Razzia.";
        if (ziel == wache || !ziel.IstAktiv) return "Dieses Haus durchsucht ihr nicht.";
        if (Wachstube(wache) is null) return "Für eine Razzia braucht ihr ein Wachhaus mit eigenen Leuten.";
        if (FreieRazzien(wache) <= 0) return "Eure Wache hat dieses Jahr schon genug Razzien gemacht.";
        return null;
    }

    internal Befehlsergebnis Razzia(Konto wache, Konto ziel)
    {
        if (RazziaVerboten(wache, ziel) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        Buchen(wache, x => x with { Razzien = x.Razzien + 1 });
        var chance = Razziachance(wache);
        var beweise = 0;
        foreach (var spur in _w.Justiz.SpurenVon(ziel).OrderBy(s => s.Nummer).ToList())
        {
            if (_w.Justiz.KenntSpur(wache, spur)) continue;
            if (_zufall.NextDouble() < chance)
            {
                _w.Justiz.BeweisGeben(wache, spur);
                beweise++;
            }
        }
        // Diebesgut: Was in den Verstecken des Hauses liegt, nimmt die Wache mit.
        double beschlagnahmt = 0;
        foreach (var b in _w.Betriebe.Where(b => b.Konto == ziel && _w.Diebe.IstDiebesbetrieb(b)).ToList())
            foreach (var (ware, stueck) in b.Lager.ToList())
            {
                b.Auslagern(ware, stueck);
                beschlagnahmt += stueck * _w.Markt[ware].Preis;
            }
        if (beschlagnahmt > 0) ziel.Raub(beschlagnahmt);
        var text = $"Razzia der Wache von {wache.Inhaber} bei {ziel.Inhaber}: " +
                   (beweise == 0 && beschlagnahmt == 0 ? "nichts gefunden." :
                       string.Join(", ", new[]
                       {
                           beweise > 0 ? $"{beweise} {(beweise == 1 ? "Beweis" : "Beweise")}" : null,
                           beschlagnahmt > 0 ? $"Diebesgut für {Zahlenformat.Ganz(beschlagnahmt)} {_w.Daten.Allgemein.Waehrung} beschlagnahmt" : null,
                       }.Where(x => x is not null)) + ".");
        _w.Melden(text);
        return Befehlsergebnis.Ok(text);
    }

    // ── Jahresende ──────────────────────────────────────────────────────────

    internal void Jahresende()
    {
        if (!IstAktiv) return;
        _vorjahr.Clear();
        foreach (var (k, buch) in _diesesJahr) _vorjahr[k] = buch;
        _diesesJahr.Clear();
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    internal WacheStand Sichern(Func<Konto, int> nr) => new()
    {
        ZufallWuerfe = _zufall.Wuerfe,
        DiesesJahr = _diesesJahr.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
        Vorjahr = _vorjahr.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
    };

    internal void Wiederherstellen(WacheStand s, IReadOnlyList<Konto> k)
    {
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        foreach (var (nr, buch) in s.DiesesJahr) _diesesJahr[k[int.Parse(nr)]] = buch;
        foreach (var (nr, buch) in s.Vorjahr) _vorjahr[k[int.Parse(nr)]] = buch;
    }
}
