using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Duft;

// ─────────────────────────────────────────────────────────────────────────────
//  Daten (Abschnitt „parfum" in wirtschaft.json, ab Datenversion 21)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class MoschusDaten
{
    public string Ware { get; init; } = "Moschusduft";

    /// <summary>So viel Zuneigung gewinnt man bei einem Heiratskandidaten – zusätzlich zum Jahreslimit der Geschenke.</summary>
    public double Zuneigung { get; init; } = 10;

    /// <summary>So viel Gunst gewinnt man bei einem Ratsherrn – ohne Bestechungsspur.</summary>
    public double Gunst { get; init; } = 12;
}

public sealed class ZwietrachtDaten
{
    public string Ware { get; init; } = "Blume der Zwietracht";

    /// <summary>So viel Gunst verliert das Opfer beim Ratsherrn.</summary>
    public double Gunst { get; init; } = 15;
}

public sealed class HeiligenduftDaten
{
    public string Ware { get; init; } = "Heiligenduft";

    /// <summary>So viel Ansehen gewinnt das stiftende Haus – bis zum Ende des nächsten Jahres.</summary>
    public double Ansehen { get; init; } = 4;
}

public sealed class DolchDaten
{
    /// <summary>Chance, dass das Gift ins Blut gelangt (vor Talent und Streife).</summary>
    public double Chance { get; init; } = 0.6;

    /// <summary>Welche Vergiftung es auslöst (unter gesundheit.krankheiten, mit „gegengift": true).</summary>
    public string Krankheit { get; init; } = "Vergiftung";
}

public sealed class GiftDaten
{
    /// <summary>Wert der Spur fürs Gericht (bemisst die Buße).</summary>
    public double Betrag { get; init; } = 400;

    /// <summary>Waren, mit denen man vergiften kann.</summary>
    public Dictionary<string, DolchDaten> Dolche { get; init; } = new();
}

/// <summary>Der Parfumeur: Düfte mit Wirkung und Giftdolche.</summary>
public sealed class ParfumDaten
{
    public string Beruf { get; init; } = "Parfumeur";
    public MoschusDaten Moschus { get; init; } = new();
    public ZwietrachtDaten Zwietracht { get; init; } = new();
    public HeiligenduftDaten Heiligenduft { get; init; } = new();
    public GiftDaten Gift { get; init; } = new();

    public void Pruefen(List<string> f, IReadOnlyDictionary<string, WareDaten> waren, IReadOnlyDictionary<string, BerufDaten> berufe,
        Gesundheit.GesundheitDaten? gesundheit)
    {
        if (berufe.Count > 0 && !berufe.ContainsKey(Beruf)) f.Add($"parfum.beruf: Den Beruf „{Beruf}“ gibt es nicht.");
        foreach (var (wo, ware) in new[] { ("moschus", Moschus.Ware), ("zwietracht", Zwietracht.Ware), ("heiligenduft", Heiligenduft.Ware) })
            if (!waren.ContainsKey(ware)) f.Add($"parfum.{wo}.ware: Die Ware „{ware}“ gibt es nicht.");
        if (Moschus.Zuneigung < 0 || Moschus.Gunst < 0 || Zwietracht.Gunst < 0 || Heiligenduft.Ansehen < 0 || Gift.Betrag < 0)
            f.Add("parfum: Zuneigung, Gunst, Ansehen und Betrag dürfen nicht negativ sein.");
        foreach (var (dolch, d) in Gift.Dolche)
        {
            if (!waren.ContainsKey(dolch)) f.Add($"parfum.gift.dolche: Die Ware „{dolch}“ gibt es nicht.");
            if (d.Chance is < 0 or > 1) f.Add($"parfum.gift.dolche.{dolch}.chance muss zwischen 0 und 1 liegen.");
            if (gesundheit is null || !gesundheit.Krankheiten.TryGetValue(d.Krankheit, out var k) || !k.Gegengift)
                f.Add($"parfum.gift.dolche.{dolch}.krankheit: „{d.Krankheit}“ muss unter gesundheit.krankheiten stehen (mit „gegengift“: true).");
        }
    }
}

/// <summary>Ein Giftanschlag – ausgeführt in der nächsten Nacht.</summary>
public sealed record Giftauftrag(Konto Auftraggeber, Konto Ziel, string Dolch);

// ─────────────────────────────────────────────────────────────────────────────
//  Teilsystem
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Düfte und Dolche des Parfumeurs – nutzbar für jedes Haus, aus dem eigenen Lager oder vom Markt:
///   • Moschusduft: bei einem Heiratskandidaten Zuneigung über das Jahreslimit hinaus, bei einem Ratsherrn Gunst ohne
///     Bestechungsspur – je Kandidat bzw. Ratsherr einmal im Jahr.
///   • Blume der Zwietracht: Ein Ratsherr verliert Gunst für ein anderes Haus – eine Intrige (Vergehen).
///   • Heiligenduft: in der Kirche gestiftet, mehr Ansehen bis Ende des nächsten Jahres.
///   • Giftdolch / Schattendolch: Bei Nacht wird das Oberhaupt eines anderen Hauses vergiftet; nur Gegengift heilt.
///     Ein Vergehen mit Spur und vielleicht einem Zeugen – vor Gericht führt es in den Kerker.
/// Eigener Zufall – und gewürfelt wird nur bei einem Anschlag: Ohne Gift verläuft eine Partie wie zuvor.
/// </summary>
public sealed class Duftwesen
{
    private readonly Stadtwirtschaft _w;
    private readonly ParfumDaten? _d;
    private Zufallsquelle _zufall;
    private readonly List<Giftauftrag> _auftraege = new();
    private readonly SortedSet<(int Konto, string Ziel)> _geschenkt = new(); // Moschus dieses Jahr: „K:Nummer" oder „R:Name"
    private readonly Dictionary<Konto, int> _heiligBis = new();
    private readonly Dictionary<Konto, int> _anschlaege = new();

    public Duftwesen(Stadtwirtschaft w, ParfumDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 61 + 43));
    }

    public bool IstAktiv => _d is not null;
    public ParfumDaten Daten => _d ?? throw new InvalidOperationException("Es gibt keine Düfte (parfum fehlt in den Daten).");

    private int Jahr => _w.Jahr;
    private string Waehrung => _w.Daten.Allgemein.Waehrung;
    private int Nr(Konto k) => _w.Konten.ToList().IndexOf(k); // wie beim Speichern: die Reihenfolge der Konten

    public IReadOnlyList<Giftauftrag> Auftraege => _auftraege;

    // ── Beschaffen ──────────────────────────────────────────────────────────

    /// <summary>Was ein Stück dieses Haus kostet: 0 aus dem eigenen Lager, sonst der Marktpreis.</summary>
    public double Preis(Konto konto, string ware) =>
        _w.Betriebe.Any(b => b.Konto == konto && b.ImLager(ware) > 0) ? 0 : _w.Markt[ware].Preis;

    /// <summary>Ein Stück aus dem eigenen Lager nehmen oder am Markt kaufen. Liefert den Preis (0 aus dem Lager).</summary>
    private double Beschaffen(Konto konto, string ware, Action<double> bezahlen)
    {
        if (_w.Betriebe.FirstOrDefault(b => b.Konto == konto && b.ImLager(ware) > 0) is { } lager)
        {
            lager.Auslagern(ware, 1);
            return 0;
        }
        var preis = _w.Markt[ware].Kaufen(1);
        bezahlen(preis);
        return preis;
    }

    private string Herkunft(double preis) => preis > 0 ? $"{Zahlenformat.Ganz(preis)} {Waehrung} am Markt" : "aus dem eigenen Lager";

    private string? Geldfehlt(Konto konto, string ware) =>
        Preis(konto, ware) > konto.Vermoegen ? $"{ware} kostet am Markt {Zahlenformat.Ganz(Preis(konto, ware))} {Waehrung} – so viel habt ihr nicht." : null;

    // ── Abfragen ────────────────────────────────────────────────────────────

    /// <summary>Ansehen durch Heiligenduft, solange er wirkt.</summary>
    public double AnsehenBonus(Konto konto) => IstAktiv && _heiligBis.GetValueOrDefault(konto) >= Jahr ? Daten.Heiligenduft.Ansehen : 0;

    public int? HeiligBis(Konto konto) => _heiligBis.TryGetValue(konto, out var bis) && bis >= Jahr ? bis : null;

    /// <summary>Wie viele Giftanschläge dieses Haus dieses Jahr in Auftrag gab.</summary>
    public int AnschlaegeDiesesJahr(Konto konto) => _anschlaege.GetValueOrDefault(konto);

    public bool MoschusSchonKandidat(Konto konto, int nummer) => _geschenkt.Contains((Nr(konto), $"K:{nummer}"));
    public bool MoschusSchonRatsherr(Konto konto, string ratsherr) => _geschenkt.Contains((Nr(konto), $"R:{ratsherr}"));

    /// <summary>Chance, dass das Gift wirkt: Dolch + „Bei Nacht und Nebel", die Streife der Wache macht es schwerer.</summary>
    public double Giftchance(Konto auftraggeber, string dolch) =>
        IstAktiv && Daten.Gift.Dolche.TryGetValue(dolch, out var d)
            ? Math.Min(0.95, d.Chance + _w.Familien.NachtBonus(auftraggeber)) * _w.Wache.Patrouillefaktor(null)
            : 0;

    public string? MoschusVerboten(Konto konto, int kandidat)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Düfte.";
        if (!_w.Familien.IstAktiv || _w.Familien.KandidatNummer(kandidat) is not { } k) return "Diesen Heiratskandidaten gibt es nicht (mehr).";
        if (_w.Familien.WerbenVerboten(konto, k) is { } grund) return grund;
        if (MoschusSchonKandidat(konto, kandidat)) return $"{k.Person.Vorname} hat dieses Jahr schon Moschus von euch bekommen.";
        return Geldfehlt(konto, Daten.Moschus.Ware);
    }

    public string? MoschusRatsherrVerboten(Konto konto, string ratsherr)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Düfte.";
        if (!_w.Rat.IstAktiv || _w.Rat.RatsherrNamens(ratsherr) is null) return $"Einen Ratsherrn „{ratsherr}“ gibt es nicht.";
        if (MoschusSchonRatsherr(konto, ratsherr)) return $"{ratsherr} hat dieses Jahr schon Moschus von euch bekommen.";
        return Geldfehlt(konto, Daten.Moschus.Ware);
    }

    public string? ZwietrachtVerboten(Konto konto, string ratsherr, Konto ziel)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Düfte.";
        if (!_w.Rat.IstAktiv || _w.Rat.RatsherrNamens(ratsherr) is null) return $"Einen Ratsherrn „{ratsherr}“ gibt es nicht.";
        if (ziel == konto || !ziel.IstAktiv) return "Gegen dieses Haus lohnt keine Zwietracht.";
        return Geldfehlt(konto, Daten.Zwietracht.Ware);
    }

    public string? HeiligenduftVerboten(Konto konto)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Düfte.";
        if (_heiligBis.GetValueOrDefault(konto) > Jahr) return $"Euer Heiligenduft wirkt noch bis Ende {_heiligBis[konto]}.";
        return Geldfehlt(konto, Daten.Heiligenduft.Ware);
    }

    public string? GiftVerboten(Konto konto, Konto ziel, string dolch)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Düfte.";
        if (!Daten.Gift.Dolche.ContainsKey(dolch)) return $"Mit „{dolch}“ vergiftet man niemanden.";
        if (!_w.Gesundheit.IstAktiv || _w.Familien.Von(ziel) is null) return "Ein Giftanschlag braucht ein Oberhaupt, das krank werden kann.";
        if (ziel == konto || !ziel.IstAktiv) return "Gegen dieses Haus richtet sich kein Dolch.";
        if (_auftraege.Any(a => a.Auftraggeber == konto && a.Ziel == ziel)) return $"Für heute Nacht ist gegen {ziel.Inhaber} schon ein Anschlag geplant.";
        return Geldfehlt(konto, dolch);
    }

    // ── Befehle ─────────────────────────────────────────────────────────────

    internal Befehlsergebnis MoschusSchenken(Konto konto, int kandidat)
    {
        if (MoschusVerboten(konto, kandidat) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var preis = Beschaffen(konto, Daten.Moschus.Ware, konto.Familienkosten);
        _w.Familien.ZuneigungSchenken(konto, kandidat, Daten.Moschus.Zuneigung);
        _geschenkt.Add((Nr(konto), $"K:{kandidat}"));
        var k = _w.Familien.KandidatNummer(kandidat)!;
        var bereit = _w.Familien.Bereitschaft(konto, k);
        var schwelle = _w.Familien.Heiratsschwelle(k);
        return Befehlsergebnis.Ok($"{k.Person.Name} ist hingerissen vom Moschusduft ({Herkunft(preis)}): +{Zahlenformat.Ganz(Daten.Moschus.Zuneigung)} Zuneigung. " +
                                  (bereit >= schwelle ? "Einer Heirat steht nichts mehr im Weg!" : $"Zuneigung und Ansehen: {Zahlenformat.Kommazahl(bereit)} von {Zahlenformat.Ganz(schwelle)}."));
    }

    internal Befehlsergebnis MoschusRatsherr(Konto konto, string ratsherr)
    {
        if (MoschusRatsherrVerboten(konto, ratsherr) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var r = _w.Rat.RatsherrNamens(ratsherr)!;
        var preis = Beschaffen(konto, Daten.Moschus.Ware, konto.Geschenk);
        _w.Rat.GunstHinzufuegen(r, konto, Daten.Moschus.Gunst);
        _geschenkt.Add((Nr(konto), $"R:{ratsherr}"));
        return Befehlsergebnis.Ok($"{ratsherr} nimmt den Moschusduft gern ({Herkunft(preis)}) – ein Duft ist kein Bestechungsgeld. " +
                                  $"Gunst für {konto.Inhaber}: {Zahlenformat.Ganz(_w.Rat.Gunst(r, konto))}.");
    }

    internal Befehlsergebnis ZwietrachtSaeen(Konto konto, string ratsherr, Konto ziel)
    {
        if (ZwietrachtVerboten(konto, ratsherr, ziel) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var r = _w.Rat.RatsherrNamens(ratsherr)!;
        var ware = Daten.Zwietracht.Ware;
        var preis = Beschaffen(konto, ware, konto.Gaunerkosten);
        _w.Rat.GunstMindern(r, ziel, Daten.Zwietracht.Gunst);
        _w.Justiz.TatMelden(konto, Vergehen.Intrige, $"{ratsherr} gegen {ziel.Inhaber}", preis > 0 ? preis : _w.Markt[ware].Preis);
        _w.Melden($"{ratsherr} ist neuerdings schlecht auf {ziel.Inhaber} zu sprechen.");
        return Befehlsergebnis.Ok($"Die {ware} wirkt ({Herkunft(preis)}): {ratsherr} verliert {Zahlenformat.Ganz(Daten.Zwietracht.Gunst)} Gunst für {ziel.Inhaber}. " +
                                  "Eine Intrige – wer sie euch nachweist, kann euch anklagen.");
    }

    internal Befehlsergebnis HeiligenduftStiften(Konto konto)
    {
        if (HeiligenduftVerboten(konto) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var preis = Beschaffen(konto, Daten.Heiligenduft.Ware, konto.Familienkosten);
        _heiligBis[konto] = Jahr + 1;
        _w.Melden($"{konto.Inhaber} stiftet der Kirche Heiligenduft – die ganze Gemeinde riecht es.");
        return Befehlsergebnis.Ok($"Heiligenduft gestiftet ({Herkunft(preis)}): {Zahlenformat.Ganz(Daten.Heiligenduft.Ansehen)} Ansehen mehr bis Ende {Jahr + 1}.");
    }

    internal Befehlsergebnis Giftanschlag(Konto konto, Konto ziel, string dolch)
    {
        if (GiftVerboten(konto, ziel, dolch) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var preis = Beschaffen(konto, dolch, konto.Gaunerkosten);
        _auftraege.Add(new Giftauftrag(konto, ziel, dolch));
        _anschlaege[konto] = AnschlaegeDiesesJahr(konto) + 1;
        return Befehlsergebnis.Ok($"Der {dolch} liegt bereit ({Herkunft(preis)}). Heute Nacht um 21 Uhr trifft er {ziel.Inhaber} – " +
                                  $"Chance {Zahlenformat.Ganz(Giftchance(konto, dolch) * 100)} %.");
    }

    // ── Nacht und Jahresende ────────────────────────────────────────────────

    /// <summary>Um 21:00 (nach Banden und Dieben): die Giftanschläge.</summary>
    internal void Nacht()
    {
        if (!IstAktiv || _auftraege.Count == 0) return;
        foreach (var a in _auftraege.ToList())
        {
            if (!a.Auftraggeber.IstAktiv || !a.Ziel.IstAktiv || _w.Familien.Von(a.Ziel) is not { } f) continue;
            var o = f.Oberhaupt;
            var erfolg = _zufall.NextDouble() < Giftchance(a.Auftraggeber, a.Dolch);
            var krankheit = Daten.Gift.Dolche[a.Dolch].Krankheit;
            string oeffentlich;
            if (erfolg && _w.Gesundheit.Erkranken(a.Ziel, o, krankheit))
            {
                oeffentlich = $"Giftanschlag im Haus {f.Nachname}: {o.Name} ist vergiftet – {o.Krankheit!.Beschreibung}. Nur Gegengift kann helfen.";
                Ereignis(a.Auftraggeber, $"Der {a.Dolch} hat {o.Name} getroffen.");
            }
            else if (erfolg)
                oeffentlich = $"Ein Dolch hat {o.Name} gestreift – doch {o.Vorname} ist ohnehin krank, das Gift wirkt nicht.";
            else
                oeffentlich = $"Im Haus {f.Nachname} wurde ein Fremder mit einem Dolch verscheucht – {o.Vorname} blieb unverletzt.";
            var spur = _w.Justiz.TatMelden(a.Auftraggeber, Vergehen.Giftanschlag, a.Ziel.Inhaber, Daten.Gift.Betrag);
            var zeugenchance = _w.Gauner.IstAktiv ? _w.Gauner.Zeugenchance(null, erfolg, a.Auftraggeber) : 0.25;
            if (spur is not null && _zufall.NextDouble() < zeugenchance)
            {
                _w.Justiz.BeweisGeben(a.Ziel, spur);
                oeffentlich += $" Ein Zeuge hat Leute von {a.Auftraggeber.Inhaber} erkannt!";
            }
            Ereignis(null, oeffentlich);
        }
        _auftraege.Clear();
    }

    /// <summary>Nachricht aus der Nacht – im Hinterzimmer und im Protokoll.</summary>
    private void Ereignis(Konto? fuer, string text)
    {
        if (_w.Gauner.IstAktiv) _w.Gauner.Melden(fuer, text);
        else _w.Melden(text);
    }

    internal void Jahresende()
    {
        if (!IstAktiv) return;
        _geschenkt.Clear();
        _anschlaege.Clear();
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    internal ParfumStand Sichern(Func<Konto, int> nr) => new()
    {
        ZufallWuerfe = _zufall.Wuerfe,
        Auftraege = _auftraege.Select(a => new GiftauftragStand { Auftraggeber = nr(a.Auftraggeber), Ziel = nr(a.Ziel), Dolch = a.Dolch }).ToList(),
        Geschenkt = _geschenkt.Select(g => $"{g.Konto}|{g.Ziel}").ToList(),
        HeiligBis = _heiligBis.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
        Anschlaege = _anschlaege.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
    };

    internal void Wiederherstellen(ParfumStand s, IReadOnlyList<Konto> k)
    {
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        _auftraege.Clear();
        _auftraege.AddRange(s.Auftraege.Select(a => new Giftauftrag(k[a.Auftraggeber], k[a.Ziel], a.Dolch)));
        foreach (var g in s.Geschenkt)
        {
            var teile = g.Split('|', 2);
            _geschenkt.Add((int.Parse(teile[0]), teile[1]));
        }
        foreach (var (nr, bis) in s.HeiligBis) _heiligBis[k[int.Parse(nr)]] = bis;
        foreach (var (nr, n) in s.Anschlaege) _anschlaege[k[int.Parse(nr)]] = n;
    }
}
