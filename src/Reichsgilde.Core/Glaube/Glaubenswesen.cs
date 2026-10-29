using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Glaube;

/// <summary>
/// Die Glaubensrichtungen der Stadt (Entscheidung vom 13.10.2026): zwei anerkannte Konfessionen für die Kulturvölker,
/// dazu der Naturglaube der Orks und Oger – für die Kirchen sind das Heiden.
/// </summary>
public enum Konfession
{
    Katholisch,
    Evangelisch,
    Naturglaube,
}

public static class KonfessionTexte
{
    public static string Name(this Konfession k) => k switch
    {
        Konfession.Katholisch => "katholisch",
        Konfession.Evangelisch => "evangelisch",
        _ => "Naturglaube",
    };

    /// <summary>„der katholische Glaube" – für Sätze wie „tritt zum … über".</summary>
    public static string Glaube(this Konfession k) => k switch
    {
        Konfession.Katholisch => "katholischen Glauben",
        Konfession.Evangelisch => "evangelischen Glauben",
        _ => "Naturglauben",
    };

    /// <summary>Die Gläubigen: „Katholiken", „Evangelische", „Heiden".</summary>
    public static string Gemeinde(this Konfession k) => k switch
    {
        Konfession.Katholisch => "Katholiken",
        Konfession.Evangelisch => "Evangelische",
        _ => "Heiden",
    };

    /// <summary>Eine der anerkannten Konfessionen (nur sie haben Kirchen).</summary>
    public static bool IstKirchlich(this Konfession k) => k != Konfession.Naturglaube;

    /// <summary>Der Stadtglaube als Text (null = Glaubensfreiheit).</summary>
    public static string StadtglaubeText(Konfession? k) => k is { } x ? x.Name() : "Glaubensfreiheit";
}

// ─────────────────────────────────────────────────────────────────────────────
//  Daten (Abschnitt „glaube" in wirtschaft.json, ab Datenversion 22)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Wahlpunkte im Rat: Ratsherren schätzen ihre Glaubensbrüder (neben dem eigenen Volk).</summary>
public sealed class GlaubensGunstDaten
{
    /// <summary>Oberhaupt hat den Glauben des Ratsherrn.</summary>
    public double Gleich { get; init; } = 5;

    /// <summary>Oberhaupt hat die andere Konfession (negativ).</summary>
    public double Anders { get; init; } = -3;

    /// <summary>Oberhaupt ist ein Heide (negativ).</summary>
    public double Heide { get; init; } = -8;

    /// <summary>Der Ehepartner ist zum Glauben des Hauses übergetreten: so viel bei Ratsherren seines alten Glaubens (negativ).</summary>
    public double Mischehe { get; init; } = -2;
}

public sealed class KonvertierenDaten
{
    /// <summary>Gebühr für Unterricht und Taufe bzw. Übertritt.</summary>
    public double Gebuehr { get; init; } = 300;

    /// <summary>So viel Ansehen kostet der Wechsel – bis zum Ende des nächsten Jahres („Wendehals").</summary>
    public double Ansehen { get; init; } = 3;
}

public sealed class BekehrungDaten
{
    /// <summary>So viel vom Anteil der Gläubigen wechselt zur Konfession des Predigers (0,04 = 4 Punkte).</summary>
    public double Anteil { get; init; } = 0.04;

    /// <summary>Je Stern Rhetorik wirkt die Predigt so viel stärker (0,1 = +10 %).</summary>
    public double JeRhetorik { get; init; } = 0.1;

    /// <summary>Gunst bei jedem Ratsherrn des eigenen Glaubens.</summary>
    public double Gunst { get; init; } = 3;
}

public sealed class StadtglaubeDaten
{
    /// <summary>Glaubenszins: So viel zahlt jedes andersgläubige Haus am Jahresende an die Stadt.</summary>
    public double Abgabe { get; init; } = 150;

    /// <summary>Ab dieser Gunst stimmt ein Ratsherr für Glaubensfreiheit, auch wenn sein Glaube der Stadtglaube ist.</summary>
    public double SchwelleFrei { get; init; } = 25;

    /// <summary>Ab dieser Gunst stimmt ein Ratsherr sogar für einen fremden Stadtglauben.</summary>
    public double SchwelleFremd { get; init; } = 60;

    /// <summary>Wert der Spur für eine Ketzerpredigt (Bekehrung gegen den Stadtglauben).</summary>
    public double Ketzerpredigt { get; init; } = 200;
}

/// <summary>Glaube und Konfessionen (Abschnitt „glaube"). Fehlt er, spielt der Glaube keine Rolle.</summary>
public sealed class GlaubeDaten
{
    /// <summary>
    /// Wie sich jedes Volk auf die Konfessionen verteilt (Gewichte für Heiratskandidaten).
    /// Mit dem höchsten Gewicht beginnen Häuser und Ratsherren ohne eigene Angabe.
    /// </summary>
    public Dictionary<Volk, Dictionary<Konfession, double>> Voelker { get; init; } = new();

    /// <summary>Anteil der Kirchgänger je Konfession zu Spielbeginn (nur kirchliche, Summe 1).</summary>
    public Dictionary<Konfession, double> Stadt { get; init; } = new();

    /// <summary>Jedes Jahr kehren die Anteile um diesen Teil zu den Startwerten zurück (Gewohnheit der Stadt).</summary>
    public double Rueckkehr { get; init; } = 0.1;

    /// <summary>Kleiner wird keine Gemeinde.</summary>
    public double MindestAnteil { get; init; } = 0.1;

    /// <summary>Gläubige ohne Kirche ihrer Konfession gehen in eine fremde – und geben nur diesen Teil.</summary>
    public double FremdeKirche { get; init; } = 0.5;

    public GlaubensGunstDaten Gunst { get; init; } = new();
    public KonvertierenDaten Konvertieren { get; init; } = new();
    public BekehrungDaten Bekehrung { get; init; } = new();
    public StadtglaubeDaten Stadtglaube { get; init; } = new();

    /// <summary>Glaube, mit dem ein Haus dieses Volkes beginnt (höchstes Gewicht; Orks und Oger: Naturglaube).</summary>
    public Konfession Startglaube(Volk volk) =>
        Voelker.TryGetValue(volk, out var g) && g.Count > 0
            ? g.OrderByDescending(x => x.Value).ThenBy(x => x).First().Key
            : volk is Volk.Ork or Volk.Oger ? Konfession.Naturglaube : Konfession.Katholisch;

    public void Pruefen(List<string> f, IEnumerable<RatsherrDaten> ratsherren)
    {
        foreach (var (volk, g) in Voelker)
        {
            if (g.Values.Any(x => x < 0) || g.Values.Sum() <= 0) f.Add($"glaube.voelker.{volk}: Gewichte ≥ 0, zusammen mehr als 0.");
            if (volk is not (Volk.Ork or Volk.Oger) && g.Any(x => x.Key == Konfession.Naturglaube && x.Value > 0))
                f.Add($"glaube.voelker.{volk}: Der Naturglaube gehört zu Orks und Ogern.");
        }
        if (Stadt.Count == 0 || Stadt.Keys.Any(k => !k.IstKirchlich())) f.Add("glaube.stadt: Anteile für Katholisch und Evangelisch.");
        if (Stadt.Values.Any(x => x < MindestAnteil) || Math.Abs(Stadt.Values.Sum() - 1) > 1e-6)
            f.Add("glaube.stadt: Jeder Anteil mindestens mindestAnteil, zusammen 1.");
        if (Rueckkehr is < 0 or > 1 || MindestAnteil is < 0 or >= 0.5 || FremdeKirche is < 0 or > 1)
            f.Add("glaube: rueckkehr und fremdeKirche zwischen 0 und 1, mindestAnteil unter 0,5.");
        if (Konvertieren.Gebuehr < 0 || Konvertieren.Ansehen < 0 || Bekehrung.Anteil < 0 || Stadtglaube.Abgabe < 0)
            f.Add("glaube: Gebühren, Ansehen, Anteile und Abgaben dürfen nicht negativ sein.");
        foreach (var r in ratsherren.Where(r => r.Glaube == Konfession.Naturglaube))
            f.Add($"Ratsherr {r.Name}: Im Rat sitzen nur Katholiken und Evangelische.");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Teilsystem
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Glaube und Konfessionen:
///   • Jede Person hat einen Glauben; ein Haus folgt dem Glauben seines Oberhaupts.
///   • Im Rat geben Ratsherren Glaubensbrüdern Punkte, Andersgläubigen und Heiden weniger.
///   • Die Gläubigen der Stadt gehen in die Kirchen ihrer Konfession (Anteile je Konfession, siehe Kirchenwesen).
///   • Konvertieren (Taufe für Heiden), Bekehrungspredigt der Prediger, Mischehe (der Partner tritt über).
///   • Stadtglaube: ein Ratsantrag. Gilt einer, zahlen andersgläubige Häuser Glaubenszins, und ihre Messen sind Ketzerei.
/// </summary>
public sealed class Glaubenswesen
{
    private readonly Stadtwirtschaft _w;
    private readonly GlaubeDaten? _d;
    private Zufallsquelle _zufall;
    private readonly Dictionary<Konfession, double> _anteile = new();
    private readonly Dictionary<Konto, int> _konvertiertBis = new(); // Ansehen gemindert bis einschließlich Jahr …
    private readonly Dictionary<Konto, int> _bekehrtIm = new();      // Bekehrungspredigt gehalten im Jahr …
    private readonly Dictionary<Konto, double> _abgabenVorjahr = new();
    private readonly Dictionary<Konto, bool> _stimmen = new();       // festgelegte Stimmen zum Antrag (fehlt = Enthaltung)
    private (Konto Antragsteller, Konfession? Glaube)? _antrag;

    public Glaubenswesen(Stadtwirtschaft w, GlaubeDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 67 + 47));
        if (daten is not null)
            foreach (var (k, a) in daten.Stadt) _anteile[k] = a;
    }

    public bool IstAktiv => _d is not null;
    public GlaubeDaten Daten => _d ?? throw new InvalidOperationException("Der Glaube spielt keine Rolle (glaube fehlt in den Daten).");

    /// <summary>Der Stadtglaube (null = Glaubensfreiheit).</summary>
    public Konfession? Stadtglaube { get; internal set; }

    /// <summary>Der Antrag auf einen Stadtglauben, über den am Jahresende abgestimmt wird.</summary>
    public (Konto Antragsteller, Konfession? Glaube)? Antrag
    {
        get => _antrag;
        private set
        {
            _antrag = value;
            _stimmen.Clear(); // ein neuer oder kein Antrag: die Stimmen zum alten gelten nicht mehr
        }
    }

    private int Jahr => _w.Jahr;

    // ── Auskunft ────────────────────────────────────────────────────────────

    public Konfession Startglaube(Volk volk) => _d?.Startglaube(volk) ?? Konfession.Katholisch;

    /// <summary>Darf ein Haus dieses Volkes mit diesem Glauben beginnen? (null = ja, sonst der Grund)</summary>
    public string? StartglaubeVerboten(Volk volk, Konfession glaube) =>
        volk is Volk.Ork or Volk.Oger
            ? glaube == Konfession.Naturglaube ? null : "Orks und Oger beginnen mit dem Naturglauben – taufen lassen können sie sich später."
            : glaube.IstKirchlich() ? null : "Der Naturglaube gehört zu Orks und Ogern.";

    /// <summary>Glaube eines Hauses = Glaube seines Oberhaupts (null ohne Familie).</summary>
    public Konfession? GlaubeVon(Konto konto) => _w.Familien.Von(konto)?.Oberhaupt.Glaube;

    public Konfession GlaubeVon(Ratsherr r) => r.Daten.Glaube ?? Startglaube(r.Daten.Volk);

    /// <summary>Anteil der Kirchgänger dieser Konfession.</summary>
    public double Anteil(Konfession k) => _anteile.GetValueOrDefault(k);

    public IReadOnlyDictionary<Konfession, double> Anteile => _anteile;

    /// <summary>Würfelt den Glauben eines neuen Heiratskandidaten nach den Gewichten seines Volkes.</summary>
    internal Konfession Wuerfeln(Volk volk)
    {
        if (_d is null || !_d.Voelker.TryGetValue(volk, out var g) || g.Count == 0) return Startglaube(volk);
        var liste = g.OrderBy(x => x.Key).ToList();
        var wurf = _zufall.NextDouble() * liste.Sum(x => x.Value);
        foreach (var (k, gewicht) in liste)
        {
            wurf -= gewicht;
            if (wurf < 0) return k;
        }
        return liste[^1].Key;
    }

    /// <summary>
    /// Glaubensnähe für die Wahl im Rat: Glaubensbruder +, andere Konfession −, Heide −−.
    /// Ist der Ehepartner übergetreten, nehmen es ihm die Ratsherren seines alten Glaubens übel.
    /// </summary>
    public double Naehe(Ratsherr r, Konto konto)
    {
        if (_d is null || _w.Familien.Von(konto) is not { } f) return 0;
        var g = _d.Gunst;
        var ratsglaube = GlaubeVon(r);
        var eigener = f.Oberhaupt.Glaube;
        var wert = eigener == Konfession.Naturglaube ? g.Heide : eigener == ratsglaube ? g.Gleich : g.Anders;
        if (f.Ehepartner is { } e && e.Herkunftsglaube != eigener && e.Herkunftsglaube == ratsglaube)
            wert += g.Mischehe;
        return wert;
    }

    /// <summary>Ansehensverlust nach einem Glaubenswechsel (bis Ende des nächsten Jahres).</summary>
    public double AnsehenAbzug(Konto konto) =>
        IstAktiv && _konvertiertBis.GetValueOrDefault(konto) >= Jahr ? Daten.Konvertieren.Ansehen : 0;

    public int? KonvertiertBis(Konto konto) => _konvertiertBis.TryGetValue(konto, out var bis) && bis >= Jahr ? bis : null;

    /// <summary>Zahlt dieses Haus Glaubenszins? (Ein Stadtglaube gilt, und das Haus hat einen anderen.)</summary>
    public bool IstAndersglaeubig(Konto konto) => Stadtglaube is { } s && GlaubeVon(konto) is { } g && g != s;

    /// <summary>Glaubenszins, den das Haus im letzten Jahr gezahlt hat.</summary>
    public double AbgabeVorjahr(Konto konto) => _abgabenVorjahr.GetValueOrDefault(konto);

    // ── Kirchen und Messe ───────────────────────────────────────────────────

    /// <summary>Die Konfession einer Kirche = Glaube des Hauses, dem sie gehört.</summary>
    public Konfession? KonfessionVon(Betrieb kirche) => GlaubeVon(kirche.Konto);

    /// <summary>Hält dieses Haus eine Messe gegen den Stadtglauben? (Ketzerei)</summary>
    public bool IstKetzerkirche(Betrieb kirche) => Stadtglaube is { } s && KonfessionVon(kirche) is { } k && k != s;

    // ── Befehle ─────────────────────────────────────────────────────────────

    public string? KonvertierenVerboten(Konto konto, Konfession ziel)
    {
        if (!IstAktiv) return "In dieser Stadt spielt der Glaube keine Rolle.";
        if (_w.Familien.Von(konto) is not { } f) return $"{konto.Inhaber} hat keine Familie.";
        if (!ziel.IstKirchlich()) return "Zum Naturglauben bekehrt keine Kirche.";
        if (f.Oberhaupt.Glaube == ziel) return $"Das Haus {f.Nachname} ist schon {ziel.Name()}.";
        if (konto.Vermoegen < Daten.Konvertieren.Gebuehr)
            return $"Unterricht und {(f.Oberhaupt.Glaube.IstKirchlich() ? "Übertritt" : "Taufe")} kosten {Zahlenformat.Ganz(Daten.Konvertieren.Gebuehr)} {_w.Daten.Allgemein.Waehrung}.";
        return null;
    }

    /// <summary>Das ganze Haus wechselt die Konfession (Heiden: Taufe).</summary>
    internal Befehlsergebnis Konvertieren(Konto konto, Konfession ziel)
    {
        if (KonvertierenVerboten(konto, ziel) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var f = _w.Familien.Von(konto)!;
        var vorher = f.Oberhaupt.Glaube;
        konto.Familienkosten(Daten.Konvertieren.Gebuehr);
        foreach (var p in f.Lebende) p.Glaube = ziel;
        _konvertiertBis[konto] = Jahr + 1;
        var wie = vorher.IstKirchlich() ? $"tritt zum {ziel.Glaube()} über" : $"lässt sich {ziel.Name()} taufen";
        _w.Melden($"Das Haus {f.Nachname} {wie}.");
        return Befehlsergebnis.Ok($"Das Haus {f.Nachname} {wie} ({Zahlenformat.Ganz(Daten.Konvertieren.Gebuehr)} {_w.Daten.Allgemein.Waehrung}). " +
                                  $"Die Stadt tuschelt: {Zahlenformat.Ganz(Daten.Konvertieren.Ansehen)} Ansehen weniger bis Ende {Jahr + 1}.");
    }

    /// <summary>Die Kirche, in der dieses Haus predigen kann (Personal da), oder null.</summary>
    public Betrieb? KanzelVon(Konto konto) =>
        _w.Kirchenwesen.IstAktiv ? _w.Kirchenwesen.Kirchen.Where(b => b.Konto == konto).OrderByDescending(b => b.Stufe).ThenBy(b => b.Name, StringComparer.Ordinal).FirstOrDefault() : null;

    public string? BekehrenVerboten(Konto konto)
    {
        if (!IstAktiv) return "In dieser Stadt spielt der Glaube keine Rolle.";
        if (KanzelVon(konto) is null) return "Bekehrungspredigten hält nur ein Prediger – in einer eigenen Kirche mit Personal.";
        if (GlaubeVon(konto) is not { } g || !g.IstKirchlich()) return "Wer selbst kein Christ ist, bekehrt niemanden.";
        if (_bekehrtIm.GetValueOrDefault(konto) == Jahr) return "Dieses Jahr hat das Haus schon eine Bekehrungspredigt gehalten.";
        if (Anteil(g) >= 1 - Daten.MindestAnteil * (_anteile.Count - 1) - 1e-9) return $"Mehr {g.Gemeinde()} gibt es in der Stadt nicht.";
        return null;
    }

    /// <summary>Wie viele Punkte Anteil eine Bekehrungspredigt dieses Hauses jetzt bringt.</summary>
    public double Bekehrungskraft(Konto konto) =>
        Daten.Bekehrung.Anteil * (1 + _w.Familien.Talent(konto, Talent.Rhetorik) * Daten.Bekehrung.JeRhetorik);

    internal Befehlsergebnis Bekehrungspredigt(Konto konto)
    {
        if (BekehrenVerboten(konto) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var g = GlaubeVon(konto)!.Value;
        var vorher = Anteil(g);
        Verschieben(g, Bekehrungskraft(konto));
        _bekehrtIm[konto] = Jahr;
        var ratsherren = _w.Rat.Ratsherren.Where(r => GlaubeVon(r) == g).ToList();
        foreach (var r in ratsherren) _w.Rat.GunstAendern(r, konto, "bekehrung", "Bekehrungspredigt", Daten.Bekehrung.Gunst);
        var ketzer = Stadtglaube is { } s && s != g;
        if (ketzer)
            _w.Justiz.TatMelden(konto, Vergehen.Ketzerei, "die Stadt", Daten.Stadtglaube.Ketzerpredigt);
        var kanzel = KanzelVon(konto)!;
        _w.Melden($"In {kanzel.Name} predigt {konto.Inhaber} für den {g.Glaube()}.");
        return Befehlsergebnis.Ok($"Bekehrungspredigt in {kanzel.Name}: {g.Gemeinde()} jetzt {Prozent(Anteil(g))} der Stadt (vorher {Prozent(vorher)}). " +
                                  $"{ratsherren.Count} Ratsherren eures Glaubens sind angetan (+{Zahlenformat.Ganz(Daten.Bekehrung.Gunst)} Gunst)." +
                                  (ketzer ? " Gegen den Stadtglauben ist das Ketzerei – ein Vergehen." : ""));
    }

    private static string Prozent(double anteil) => $"{Zahlenformat.Ganz(anteil * 100)} %";

    /// <summary>Verschiebt Anteil zu einer Konfession; die anderen geben gleichmäßig ab, keine unter den Mindestanteil.</summary>
    private void Verschieben(Konfession zu, double menge)
    {
        var andere = _anteile.Keys.Where(k => k != zu).OrderBy(k => k).ToList();
        var verfuegbar = andere.Sum(k => Math.Max(0, _anteile[k] - Daten.MindestAnteil));
        var echt = Math.Min(menge, verfuegbar);
        if (echt <= 0) return;
        foreach (var k in andere)
            _anteile[k] -= echt * Math.Max(0, _anteile[k] - Daten.MindestAnteil) / verfuegbar;
        _anteile[zu] = Anteil(zu) + echt;
    }

    // ── Stadtglaube im Rat ──────────────────────────────────────────────────

    /// <summary>Würde dieser Ratsherr dem Stadtglauben zustimmen, den dieses Haus beantragt?</summary>
    public bool StimmtZu(Ratsherr r, Konfession? glaube, Konto antragsteller)
    {
        var eigener = GlaubeVon(r);
        var gunst = _w.Rat.Meinung(r, antragsteller); // Meinung über das Oberhaupt (PR #231, früher Gunst)
        if (glaube == eigener) return true;
        if (glaube is null) return Stadtglaube != eigener || gunst >= Daten.Stadtglaube.SchwelleFrei;
        return gunst >= Daten.Stadtglaube.SchwelleFremd;
    }

    /// <summary>Stimme eines KI-Hauses im Rat: für den eigenen Glauben; für Freiheit, wenn ein fremder Stadtglaube gilt.</summary>
    internal bool KiStimmtZu(Konto ki, Konfession? glaube)
    {
        var eigener = GlaubeVon(ki);
        return glaube == eigener || glaube is null && Stadtglaube != eigener;
    }

    /// <summary>
    /// Wie ginge die Abstimmung über einen Stadtglauben heute aus? Häuser, die keine KI sind, stimmen wie festgelegt
    /// (nur beim vorliegenden Antrag) und enthalten sich sonst.
    /// </summary>
    public (int Ja, int Nein, int Enthaltung, bool? Buergermeister) AbstimmungsPrognose(Konto antragsteller, Konfession? glaube) =>
        _w.Rat.Zaehlen(sitz => StimmeVonSitz(sitz, antragsteller, glaube));

    /// <summary>Die Stimme eines Sitzes zum Stadtglauben (true dafür, false dagegen, null Enthaltung) – in Prognose und Sitzung dieselbe.</summary>
    internal bool? StimmeVonSitz(Amt sitz, Konto antragsteller, Konfession? glaube) =>
        sitz.Ratsherr is { } r ? StimmtZu(r, glaube, antragsteller)
        : sitz.Konto == antragsteller ? true
        : _w.Rivalen.Any(k => k.Konto == sitz.Konto) ? KiStimmtZu(sitz.Konto!, glaube)
        : Antrag == (antragsteller, glaube) ? StimmeVon(sitz.Konto!) // die festgelegte Stimme gilt nur dem vorliegenden Antrag
        : null;

    public string? BeantragenVerboten(Konto konto, Konfession? glaube)
    {
        if (!IstAktiv) return "In dieser Stadt spielt der Glaube keine Rolle.";
        if (!_w.Rat.IstAktiv || _w.Rat.AmtVon(konto) is null) return "Anträge stellen dürfen nur Mitglieder des Rates.";
        if (glaube == Konfession.Naturglaube) return "Ein heidnischer Stadtglaube? Das wagt kein Rat.";
        if (glaube == Stadtglaube) return $"{KonfessionTexte.StadtglaubeText(glaube)} gilt bereits.";
        if (Antrag is { } a)
            return $"Für dieses Jahr liegt schon ein Antrag zum Stadtglauben vor ({a.Antragsteller.Inhaber}: {KonfessionTexte.StadtglaubeText(a.Glaube)}).";
        return null;
    }

    internal Befehlsergebnis Beantragen(Konto konto, Konfession? glaube)
    {
        if (BeantragenVerboten(konto, glaube) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        Antrag = (konto, glaube);
        return Befehlsergebnis.Ok($"{konto.Inhaber} beantragt als Stadtglauben: {KonfessionTexte.StadtglaubeText(glaube)}. Der Rat stimmt am Jahresende ab.");
    }

    /// <summary>Darf dieses Haus zum Antrag auf einen Stadtglauben eine Stimme festlegen? (null = ja, sonst der Grund)</summary>
    public string? StimmeVerboten(Konto konto)
    {
        if (!IstAktiv) return "In dieser Stadt spielt der Glaube keine Rolle.";
        if (_w.Rat.StimmeVerboten(konto) is { } grund) return grund;
        if (Antrag is not { } antrag) return "Dieses Jahr liegt kein Antrag zum Stadtglauben vor.";
        if (antrag.Antragsteller == konto) return $"{konto.Inhaber} hat den Antrag gestellt und stimmt ohnehin dafür.";
        return null;
    }

    /// <summary>Die festgelegte Stimme dieses Hauses zum vorliegenden Antrag (true = dafür, false = dagegen, null = Enthaltung).</summary>
    public bool? StimmeVon(Konto konto) => StimmeVerboten(konto) is null && _stimmen.TryGetValue(konto, out var ja) ? ja : null;

    /// <summary>Stimme zum Antrag auf einen Stadtglauben festlegen (true/false) oder sich enthalten (null) – gilt bis zur Sitzung.</summary>
    internal Befehlsergebnis StimmeFestlegen(Konto konto, bool? ja)
    {
        if (StimmeVerboten(konto) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var antrag = Antrag!.Value;
        var worum = $"den Antrag von {antrag.Antragsteller.Inhaber} auf Stadtglauben „{KonfessionTexte.StadtglaubeText(antrag.Glaube)}“";
        if (ja is not { } stimme)
        {
            _stimmen.Remove(konto);
            return Befehlsergebnis.Ok($"{konto.Inhaber} enthält sich bei der Abstimmung über {worum}.");
        }
        _stimmen[konto] = stimme;
        return Befehlsergebnis.Ok($"{konto.Inhaber} stimmt {(stimme ? "für" : "gegen")} {worum}. Abgestimmt wird am Jahresende.");
    }

    /// <summary>Ein Haus scheidet aus dem Rat aus (Bankrott, Erbfall): sein Antrag und seine Stimme verfallen.</summary>
    internal void Ausscheiden(Konto konto)
    {
        if (Antrag?.Antragsteller == konto) Antrag = null;
        _stimmen.Remove(konto);
    }

    /// <summary>In der Ratssitzung, nach dem Statut: Abstimmung über den vorliegenden Antrag auf einen Stadtglauben.</summary>
    internal (int Ja, int Nein, int Enthaltung, bool? Buergermeister, bool Angenommen) Abstimmen(int jahr, Action<string> protokoll)
    {
        var antrag = Antrag!.Value;
        var (ja, nein, enthaltung, buergermeister) = AbstimmungsPrognose(antrag.Antragsteller, antrag.Glaube); // mit den festgelegten Stimmen
        Antrag = null; // leert die Stimmen
        var angenommen = Stadtrat.IstAngenommen(ja, nein, buergermeister);
        protokoll($"Ratssitzung {jahr}: Antrag von {antrag.Antragsteller.Inhaber} auf Stadtglauben „{KonfessionTexte.StadtglaubeText(antrag.Glaube)}“ " +
                  $"{(angenommen ? "ANGENOMMEN" : "abgelehnt")} ({ja} dafür, {nein} dagegen{(ja == nein ? ", der Bürgermeister entscheidet" : "")}).");
        if (angenommen) Stadtglaube = antrag.Glaube;
        return (ja, nein, enthaltung, buergermeister, angenommen);
    }

    // ── Jahresende ──────────────────────────────────────────────────────────

    /// <summary>Glaubenszins der Andersgläubigen – vor dem Jahresabschluss, damit er im alten Jahr steht.</summary>
    internal void AbgabenZahlen()
    {
        _abgabenVorjahr.Clear();
        if (!IstAktiv || Stadtglaube is null) return;
        foreach (var konto in _w.Konten.Where(k => k.IstAktiv && IstAndersglaeubig(k)))
        {
            konto.Steuer(Daten.Stadtglaube.Abgabe, Kassenposten.Glaubenszins);
            _abgabenVorjahr[konto] = Daten.Stadtglaube.Abgabe;
        }
    }

    /// <summary>
    /// Nach dem Kirchenbuch des Jahres: Ketzerkirchen hinterlassen eine Spur (die Gemeinde des Stadtglaubens hat es gesehen),
    /// die Anteile kehren ein Stück zu den Startwerten zurück.
    /// </summary>
    internal void Jahresende()
    {
        if (!IstAktiv) return;
        if (_w.Justiz.IstAktiv && Stadtglaube is { } s)
            foreach (var kirche in _w.Kirchenwesen.Kirchen.Where(IstKetzerkirche).ToList())
            {
                var kollekte = _w.Kirchenwesen.DiesesJahr(kirche).Kollekte;
                if (kollekte <= 0) continue;
                if (_w.Justiz.TatMelden(kirche.Konto, Vergehen.Ketzerei, "die Stadt", kollekte) is not { } spur) continue;
                foreach (var zeuge in _w.Konten.Where(k => k != kirche.Konto && k.IstAktiv && GlaubeVon(k) == s))
                    _w.Justiz.BeweisGeben(zeuge, spur);
            }
        foreach (var (k, start) in Daten.Stadt)
            _anteile[k] = Anteil(k) + (start - Anteil(k)) * Daten.Rueckkehr;
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    internal GlaubeStand Sichern(Func<Konto, int> nr) => new()
    {
        ZufallWuerfe = _zufall.Wuerfe,
        Anteile = _anteile.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value),
        Stadtglaube = Stadtglaube,
        AntragKonto = Antrag is { } a ? nr(a.Antragsteller) : null,
        AntragGlaube = Antrag?.Glaube,
        KonvertiertBis = _konvertiertBis.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
        BekehrtIm = _bekehrtIm.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
        AbgabenVorjahr = _abgabenVorjahr.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
        Stimmen = _stimmen.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
    };

    internal void Wiederherstellen(GlaubeStand s, IReadOnlyList<Konto> konten)
    {
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        _anteile.Clear();
        foreach (var (k, v) in s.Anteile) _anteile[k] = v;
        Stadtglaube = s.Stadtglaube;
        Antrag = s.AntragKonto is int ak ? (konten[ak], s.AntragGlaube) : null; // leert die Stimmen
        foreach (var (k, v) in s.Stimmen) _stimmen[konten[int.Parse(k)]] = v;
        foreach (var (k, v) in s.KonvertiertBis) _konvertiertBis[konten[int.Parse(k)]] = v;
        foreach (var (k, v) in s.BekehrtIm) _bekehrtIm[konten[int.Parse(k)]] = v;
        foreach (var (k, v) in s.AbgabenVorjahr) _abgabenVorjahr[konten[int.Parse(k)]] = v;
    }
}
