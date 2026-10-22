using Reichsgilde.Core.Justiz;
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

/// <summary>Wo ein Wachmann gerade ist (für die Darstellung der Wache).</summary>
public enum Wachort
{
    /// <summary>Im eigenen Wachhaus (Wachhaus, Stadtwache, Garnison).</summary>
    Daheim,
    /// <summary>Auf Streife in der Stadt.</summary>
    Patrouille,
    /// <summary>Begleitet einen Karren mit dem Geleit der Wache (welchen: <see cref="Wachstand.Karren"/>).</summary>
    Geleit,
}

/// <summary>Was ein Wachmann gerade tut.</summary>
public enum Wachtaetigkeit
{
    /// <summary>Auf Streife in der Stadt.</summary>
    Streife,
    /// <summary>Unterwegs als Geleit eines Karrens.</summary>
    Geleit,
    /// <summary>Daheim: Rüstung und Waffen putzen.</summary>
    Putzen,
    /// <summary>Daheim: Fechten üben (ab Stufe 2, auf dem Fechtboden).</summary>
    Fechten,
    /// <summary>Daheim: würfeln (abends).</summary>
    Wuerfeln,
    /// <summary>Daheim: Klingen und Hellebarden schärfen.</summary>
    Schaerfen,
    /// <summary>Daheim: das Wachbuch führen.</summary>
    Wachbuch,
    /// <summary>Daheim: schlafen (nachts).</summary>
    Schlafen,
    /// <summary>Der Hauptmann sitzt in der Hauptmannsstube (Stufe 3).</summary>
    Befehligen,
}

/// <summary>Ein Arbeiter eines Wachhauses: wo er gerade ist, was er tut und ob er der Hauptmann ist.</summary>
/// <param name="Karren">Nur bei <see cref="Wachort.Geleit"/>: der Karren, den er begleitet.</param>
public sealed record Wachstand(Arbeiter Arbeiter, Wachort Ort, Wachtaetigkeit Taetigkeit, bool Hauptmann, Karren? Karren);

/// <summary>
/// Ein Gauner in der Arrestzelle der Wache: ein Taschendieb, den die Leute dieses Hauses dieses Jahr am Markt gefasst haben.
/// </summary>
/// <param name="Herkunft">Das Haus, dem er dient.</param>
/// <param name="Betroffener">Wem er in die Tasche gegriffen hat (ein Einwohner oder „Bürger am Markt“).</param>
/// <param name="Betrag">Die Beute dieser Stunde, für die er angeklagt werden kann.</param>
/// <param name="Spur">Nummer der Spur beim Stadtgericht (fest, auch nach dem Laden).</param>
public sealed record Gefangener(Konto Herkunft, Vergehen Vergehen, string Betroffener, double Betrag, int Spur);

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

    /// <summary>
    /// Wie stark die Streife schützt: Anteil, um den nächtliche Taten in der Stadt seltener gelingen. Wachhunde (Einrichtung)
    /// zählen die Leute ihres Hauses × 1,25; der Deckel <c>wache.patrouille.hoechstens</c> gilt danach.
    /// </summary>
    public double Patrouilleschutz => IstAktiv
        ? Math.Min(Daten.Patrouille.Hoechstens, Daten.Patrouille.SchutzJeWachmann * Wachhaeuser.Sum(b => b.LeistungProMinute * b.StreifenFaktor))
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

    /// <summary>Das Wachhaus, das gerade Geleitschutz stellt: die höchste Wachstube (ab der nötigen Stufe, dann die vollste) – oder null.</summary>
    public Betrieb? Geleitstube => IstAktiv
        ? Wachhaeuser.Where(b => b.Stufe >= Daten.Geleit.AbStufe)
            .OrderByDescending(b => b.Stufe).ThenByDescending(b => b.Belegt).ThenBy(b => b.Name, StringComparer.Ordinal)
            .FirstOrDefault()
        : null;

    /// <summary>Wer gerade Geleitschutz stellt: das Haus der <see cref="Geleitstube"/> – oder null.</summary>
    public Konto? Geleitgeber => Geleitstube?.Konto;

    /// <summary>Faktor auf die Erfolgschance von Überfällen auf Karren mit dem Geleit der Wache (Kettenhemden in der Geleitstube: 0,8).</summary>
    public double GeleitFaktor => Geleitstube?.GeleitFaktor ?? 1;

    /// <summary>Faktor auf die Erfolgschance eines Überfalls auf einen Karren mit dieser Eskorte (1, wenn es nicht die der Wache ist).</summary>
    public double UeberfallFaktor(EskorteDaten eskorte) => eskorte.Wache ? GeleitFaktor : 1;

    /// <summary>
    /// Die Karren, die gerade mit dem Geleit der Wache fahren: Eskorte <c>wache.geleit.eskorte</c>, beladen (nur dann wird das
    /// Geleit bezahlt) und unterwegs – auf dem Hinweg auch vor dem geschlossenen Tor, auf dem Rückweg erst, wenn er losgefahren ist.
    /// Leer, wenn niemand Geleit stellt. Nach Heimat und Name geordnet.
    /// </summary>
    public IReadOnlyList<Karren> GeleitKarren
    {
        get
        {
            if (Geleitgeber is null) return Array.Empty<Karren>();
            var zeit = _w.Uhr;
            return _w.Betriebe.SelectMany(b => b.Karren)
                .Where(k => k.Eskorte == Daten.Geleit.Eskorte && k.Ladung.Count > 0
                            && (k.Zustand == KarrenZustand.ZumMarkt || (k.Zustand == KarrenZustand.ZumBetrieb && !k.WartetAmTor(zeit))))
                .OrderBy(k => k.Heimat.Name, StringComparer.Ordinal).ThenBy(k => k.Name, StringComparer.Ordinal)
                .ToList();
        }
    }

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

    /// <summary>
    /// Chance je unbekannter Tat, bei einer Razzia einen Beweis zu finden: Grundchance + je Stufe über 1, dazu Hellebarden und
    /// Armbrüste der Wachstube (absolut, je +0,1); höchstens 95 %.
    /// </summary>
    public double Razziachance(Konto k) =>
        Math.Min(0.95, Daten.Razzia.Chance + Daten.Razzia.ChanceJeStufe * Math.Max(0, (Wachstube(k)?.Stufe ?? 1) - 1) + (Wachstube(k)?.RazziaBonus ?? 0));

    /// <summary>Chance je unbekannter Tat bei diesem Haus – haben seine Diebe Lederhandschuhe, finden sich weniger Spuren.</summary>
    public double Razziachance(Konto wache, Konto ziel) => Razziachance(wache) * _w.Diebe.SpurenFaktor(ziel);

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
        var chance = Razziachance(wache, ziel);
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

    // ── Anwesenheit (für die Darstellung, nur lesend) ───────────────────────

    /// <summary>So viele Gefangene zeigt die Arrestzelle höchstens (die zuletzt gefassten).</summary>
    public const int HoechstensGefangene = 6;

    /// <summary>
    /// Der Hauptmann einer Garnison (Stufe 3): der dienstälteste Arbeiter, der kein Lehrling ist – nur, wenn er noch
    /// jemanden zu befehligen hat. Sonst null.
    /// </summary>
    public Arbeiter? Hauptmann(Betrieb b) =>
        IstWachbetrieb(b) && b.InDerStadt && b.Stufe >= 3 && b.Belegt >= 2
            ? b.Arbeiter.Where(a => a.Arbeitskraft != "Lehrling").OrderBy(a => a.EingestelltUm).ThenBy(a => a.Nummer).FirstOrDefault()
            : null;

    /// <summary>Sitzt der Hauptmann gerade in der Hauptmannsstube (8–22 Uhr)?</summary>
    public bool HauptmannInStube(Betrieb b) => AnwesenheitJetzt(b).Any(s => s.Taetigkeit == Wachtaetigkeit.Befehligen);

    /// <summary>
    /// Wo jeder Arbeiter dieses Wachhauses gerade ist und was er tut – zur aktuellen Uhrzeit, ohne Zufall, ändert nichts
    /// (leer, wenn es kein Wachhaus in der Stadt ist oder niemand dort arbeitet). Regeln (Stunde h der Uhr, Stufe s):
    ///   • Geleit: Stellt dieses Haus das Geleit (<see cref="Geleitstube"/>), begleitet je Karren aus <see cref="GeleitKarren"/>
    ///     einer ihn (Gesellen vor Lehrlingen, dann nach Nummer; nie der Hauptmann) – zu jeder Uhrzeit.
    ///   • Streife: Von den übrigen (ohne Hauptmann) sind in der Arbeitszeit (8–20 Uhr) die Hälfte (aufgerundet) auf
    ///     Patrouille, sonst (5–8 und 20–22 Uhr) ein Viertel (gerundet: 1 → 0, 2–5 → 1, 6–8 → 2). Wer, wechselt alle zwei
    ///     Stunden (Wachwechsel zur geraden Stunde): Die Reihe nach Nummer rückt eins weiter. Ruht der Dienst (Umbau, Brand,
    ///     Haus aufgelöst), geht niemand auf Streife.
    ///   • Daheim (j = Platz unter denen, die gerade daheim sind, nach Nummer): in der Arbeitszeit reihum Putzen, Schärfen,
    ///     Wachbuch (ab Stufe 2 Fechten, Putzen, Schärfen, Wachbuch) – Eintrag (j + h) der Reihe; um 20 Uhr Würfeln (j + h
    ///     gerade) oder Putzen, ab 21 Uhr Schlafen; vor 7 Uhr Schlafen, um 7 Uhr Putzen (j + h gerade) oder Schärfen.
    ///   • Stufe 3: Der <see cref="Hauptmann"/> sitzt von 8 bis 22 Uhr in der Hauptmannsstube (Befehligen).
    /// Die Simulation rechnet den Schutz der Streife weiter mit allen Arbeitern – die Abfrage verteilt sie nur fürs Bild.
    /// </summary>
    public IReadOnlyList<Wachstand> AnwesenheitJetzt(Betrieb b)
    {
        if (!IstWachbetrieb(b) || !b.InDerStadt || b.Belegt == 0) return Array.Empty<Wachstand>();
        var uhr = _w.Uhr.UhrzeitInMinuten;
        var h = uhr / 60;
        var hauptmann = Hauptmann(b);
        var leute = b.Arbeiter.OrderBy(a => a.Nummer).ToList();
        var uebrige = leute.Where(a => a != hauptmann).ToList();

        // Geleit: je Karren einer, solange dieses Haus das Geleit stellt.
        var geleit = new Dictionary<Arbeiter, Karren>();
        if (Geleitstube == b)
        {
            var karren = GeleitKarren;
            var reihe = uebrige.OrderBy(a => a.Arbeitskraft == "Lehrling" ? 1 : 0).ThenBy(a => a.Nummer).ToList();
            for (var i = 0; i < Math.Min(karren.Count, reihe.Count); i++) geleit[reihe[i]] = karren[i];
        }

        // Streife: tags die Hälfte, nachts ein Viertel der übrigen; alle zwei Stunden rückt die Reihe weiter.
        var wache = uebrige.Where(a => !geleit.ContainsKey(a)).ToList();
        var n = wache.Count;
        var tag = b.IstArbeitszeit(uhr);
        var dienst = b.Konto.IstAktiv && b.UmbauMinuten == 0 && b.SabotageMinuten == 0;
        var streife = !dienst || n == 0 ? 0 : tag ? (n + 1) / 2 : (n + 2) / 4;
        var wechsel = h / 2;

        var liste = new List<Wachstand>(leute.Count);
        var daheim = 0; // Platz unter denen, die gerade daheim sind – so tun zwei Leute daheim nicht dasselbe
        foreach (var a in leute)
        {
            var istHauptmann = a == hauptmann;
            if (geleit.TryGetValue(a, out var k))
            {
                liste.Add(new Wachstand(a, Wachort.Geleit, Wachtaetigkeit.Geleit, false, k));
                continue;
            }
            var platz = wache.IndexOf(a);
            if (platz >= 0 && ((platz - wechsel) % n + n) % n < streife)
            {
                liste.Add(new Wachstand(a, Wachort.Patrouille, Wachtaetigkeit.Streife, false, null));
                continue;
            }
            if (istHauptmann && uhr >= Stadtwirtschaft.Arbeitsbeginn)
            {
                liste.Add(new Wachstand(a, Wachort.Daheim, Wachtaetigkeit.Befehligen, true, null));
                continue;
            }
            var j = daheim++;
            var gerade = (j + h) % 2 == 0;
            Wachtaetigkeit taetigkeit;
            if (tag)
            {
                var reihum = b.Stufe >= 2 ? TagMitFechtboden : TagOhneFechtboden;
                taetigkeit = reihum[(j + h) % reihum.Length];
            }
            else if (uhr >= b.Arbeitsende)
                taetigkeit = h >= 21 ? Wachtaetigkeit.Schlafen : gerade ? Wachtaetigkeit.Wuerfeln : Wachtaetigkeit.Putzen;
            else
                taetigkeit = h < 7 ? Wachtaetigkeit.Schlafen : gerade ? Wachtaetigkeit.Putzen : Wachtaetigkeit.Schaerfen;
            liste.Add(new Wachstand(a, Wachort.Daheim, taetigkeit, istHauptmann, null));
        }
        return liste;
    }

    private static readonly Wachtaetigkeit[] TagOhneFechtboden = { Wachtaetigkeit.Putzen, Wachtaetigkeit.Schaerfen, Wachtaetigkeit.Wachbuch };
    private static readonly Wachtaetigkeit[] TagMitFechtboden = { Wachtaetigkeit.Fechten, Wachtaetigkeit.Putzen, Wachtaetigkeit.Schaerfen, Wachtaetigkeit.Wachbuch };

    /// <summary>
    /// Wer in der Arrestzelle dieses Wachhauses sitzt: die Taschendiebe, für die das Haus dieses Jahr einen Beweis hat – so
    /// kommt der Beweis zur Wache, die einen Taschendieb am Markt fasst. Die zuletzt gefassten zuerst, höchstens
    /// <see cref="HoechstensGefangene"/>. Nur in der <see cref="Wachstube"/> des Hauses; am Gerichtstag (Jahreswechsel) wird
    /// die Zelle leer. Abgeleitet aus den Spuren des Stadtgerichts, die im Spielstand stehen – nach dem Laden dieselbe Liste.
    /// Ohne Gericht leer. Selten zählt mit, was das Haus anders erfahren hat (z. B. bei einer eigenen Razzia).
    /// Bei einer Razzia fasst die Wache niemanden: Sie findet Beweise und beschlagnahmt Diebesgut.
    /// </summary>
    public IReadOnlyList<Gefangener> Gefangene(Betrieb b)
    {
        if (!IstAktiv || !_w.Justiz.IstAktiv || Wachstube(b.Konto) != b) return Array.Empty<Gefangener>();
        var jahr = _w.Jahr;
        return _w.Konten.Where(k => k != b.Konto)
            .SelectMany(k => _w.Justiz.SpurenVon(k))
            .Where(s => s.Art == Vergehen.Taschendiebstahl && s.Jahr == jahr && _w.Justiz.KenntSpur(b.Konto, s))
            .OrderByDescending(s => s.Nummer)
            .Take(HoechstensGefangene)
            .Select(s => new Gefangener(s.Taeter, s.Art, s.Betroffener, s.Betrag, s.Nummer))
            .ToList();
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
