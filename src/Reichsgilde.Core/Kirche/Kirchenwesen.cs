using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Kirche;

// ─────────────────────────────────────────────────────────────────────────────
//  Daten (Abschnitt „kirche" in wirtschaft.json, ab Datenversion 17)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class AblassDaten
{
    /// <summary>Was ein Ablass mindestens kostet.</summary>
    public double Grundpreis { get; init; } = 250;

    /// <summary>Dazu dieser Anteil vom Wert der Tat (Bestechungsgeld, Beute, Schaden …).</summary>
    public double AnteilTat { get; init; } = 0.5;
}

public sealed class BeichteDaten
{
    /// <summary>Chance je Tat eines anderen Hauses, dass der Prediger sie im Jahr in der Beichte hört.</summary>
    public double Chance { get; init; } = 0.25;

    /// <summary>Wert der Spur, wenn er das Beichtgeheimnis bricht (bemisst die Buße).</summary>
    public double Betrag { get; init; } = 200;
}

public sealed class HetzschriftDaten
{
    public string Ware { get; init; } = "Hetzschrift";

    /// <summary>So viel Ansehen verliert das Opfer – bis zum Ende des nächsten Jahres.</summary>
    public double Ansehen { get; init; } = 5;
}

/// <summary>
/// Kirche und Prediger: Messe mit Kollekte, Ablass, Beichte und zwei Schriften mit Wirkung.
/// Mit Glauben (Abschnitt „glaube") gehen die Gläubigen in Kirchen ihrer Konfession, siehe <see cref="Kirchenwesen.Verteilen"/>.
/// </summary>
public sealed class KirchenDaten
{
    public string Beruf { get; init; } = "Prediger";

    /// <summary>Zu dieser Stunde ist Messe – dann wird gespendet.</summary>
    public int MesseUm { get; init; } = 10;

    /// <summary>Gläubige in der ganzen Stadt je Jahr; sie verteilen sich auf die Kirchen.</summary>
    public double GlaeubigeJeJahr { get; init; } = 200;

    public double SpendeJeGlaeubigem { get; init; } = 2.5;

    public List<double> AnziehungJeStufe { get; init; } = new() { 1, 1.5, 2 };
    public double AnziehungJeAnsehen { get; init; } = 0.0125;

    /// <summary>Je Stern Rhetorik des Oberhaupts predigt es so viel besser (0,1 = +10 %).</summary>
    public double AnziehungJeRhetorik { get; init; } = 0.1;

    public AblassDaten Ablass { get; init; } = new();
    public BeichteDaten Beichte { get; init; } = new();

    /// <summary>Wer sie liest, gewinnt ein halbes Talent (einmal je Jahr, keine AP).</summary>
    public string Lehrschrift { get; init; } = "Lehrschrift";

    /// <summary>Eine Lehrschrift bringt ein Talent höchstens bis zu so vielen Sternen – darüber hilft nur Üben.</summary>
    public double LehrschriftBis { get; init; } = 3;

    public HetzschriftDaten Hetzschrift { get; init; } = new();

    public void Pruefen(List<string> f, IReadOnlyDictionary<string, WareDaten> waren, IReadOnlyDictionary<string, BerufDaten> berufe)
    {
        if (berufe.Count > 0 && !berufe.ContainsKey(Beruf)) f.Add($"kirche.beruf: Den Beruf „{Beruf}“ gibt es nicht.");
        if (!waren.ContainsKey(Lehrschrift)) f.Add($"kirche.lehrschrift: Die Ware „{Lehrschrift}“ gibt es nicht.");
        if (!waren.ContainsKey(Hetzschrift.Ware)) f.Add($"kirche.hetzschrift.ware: Die Ware „{Hetzschrift.Ware}“ gibt es nicht.");
        if (MesseUm is < 6 or > 23) f.Add("kirche.messeUm muss zwischen 6 und 23 liegen.");
        if (GlaeubigeJeJahr < 0 || SpendeJeGlaeubigem < 0) f.Add("kirche: glaeubigeJeJahr und spendeJeGlaeubigem dürfen nicht negativ sein.");
        if (AnziehungJeStufe.Count == 0 || AnziehungJeStufe.Any(a => a <= 0)) f.Add("kirche.anziehungJeStufe: Werte größer als 0.");
        if (Beichte.Chance is < 0 or > 1) f.Add("kirche.beichte.chance muss zwischen 0 und 1 liegen.");
        if (LehrschriftBis <= 0) f.Add("kirche.lehrschriftBis muss größer als 0 sein.");
        if (Ablass.Grundpreis < 0 || Ablass.AnteilTat < 0 || Hetzschrift.Ansehen < 0) f.Add("kirche: Preise und Ansehen dürfen nicht negativ sein.");
    }
}

/// <summary>Was eine Kirche in einem Jahr eingenommen hat.</summary>
public sealed record Kirchenbuch(double Glaeubige, double Kollekte, int Ablaesse, double Ablassgeld);

// ─────────────────────────────────────────────────────────────────────────────
//  Teilsystem
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Das Kirchenwesen der Stadt:
///   • Messe (einmal am Tag, zur Stunde <see cref="KirchenDaten.MesseUm"/>): Die Gläubigen verteilen sich nach Stufe,
///     Ansehen und Rhetorik auf die Kirchen und spenden – die Kollekte geht an den Prediger.
///   • Ablass: Ein anderes Haus zahlt, eine seiner Taten verfällt sofort (mit allen Beweisen dafür).
///   • Beichte: Am Jahresende hört der Prediger von den Taten anderer Häuser. Bricht er das Beichtgeheimnis,
///     werden daraus Beweise – und er begeht selbst ein Vergehen.
///   • Lehrschrift (½ Talent bis 3 Sterne, einmal im Jahr) und Hetzschrift (fremdes Ansehen −5, Vergehen Verleumdung) –
///     aus dem eigenen Lager oder vom Markt.
/// </summary>
public sealed class Kirchenwesen
{
    private readonly Stadtwirtschaft _w;
    private readonly KirchenDaten? _d;
    private Zufallsquelle _zufall;
    private readonly Dictionary<string, Kirchenbuch> _diesesJahr = new();
    private readonly Dictionary<string, Kirchenbuch> _vorjahr = new();
    private readonly HashSet<(Konto Prediger, int Spur)> _beichtwissen = new();
    private readonly Dictionary<Konto, int> _gelesen = new();     // Lehrschrift gelesen im Jahr …
    private readonly Dictionary<Konto, int> _gehetztBis = new();  // Ansehen gemindert bis einschließlich Jahr …

    public Kirchenwesen(Stadtwirtschaft w, KirchenDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 43 + 23));
    }

    public bool IstAktiv => _d is not null;
    public KirchenDaten Daten => _d ?? throw new InvalidOperationException("Es gibt keine Kirchen (kirche fehlt in den Daten).");

    /// <summary>Eine Werkstatt des Predigers (Kirche, Dom, Kathedrale).</summary>
    public bool IstKirche(Betrieb b) => IstAktiv && b.IstWerkstatt && b.Artdaten.Beruf == Daten.Beruf;

    /// <summary>Kirchen, in denen gepredigt wird: Haus im Spiel, Personal da.</summary>
    public IEnumerable<Betrieb> Kirchen => _w.Betriebe.Where(b => IstKirche(b) && b.Konto.IstAktiv && b.Belegt > 0);

    public Kirchenbuch DiesesJahr(Betrieb b) => _diesesJahr.GetValueOrDefault(b.Name, new Kirchenbuch(0, 0, 0, 0));
    public Kirchenbuch? Vorjahr(Betrieb b) => _vorjahr.GetValueOrDefault(b.Name);

    private int Jahr => _w.Jahr;

    // ── Messe ───────────────────────────────────────────────────────────────

    /// <summary>Wie gern die Gläubigen hierher kommen: Stufe × Ansehen × Rhetorik des Predigers.</summary>
    public double Anziehung(Betrieb b)
    {
        var d = Daten;
        var stufe = d.AnziehungJeStufe[Math.Clamp(b.Stufe - 1, 0, d.AnziehungJeStufe.Count - 1)];
        var rhetorik = _w.Familien.Von(b.Konto)?.Oberhaupt.Talent(Talent.Rhetorik) ?? 0;
        return stufe * (1 + Math.Max(0, _w.Rat.Ansehen(b.Konto)) * d.AnziehungJeAnsehen) * (1 + rhetorik * d.AnziehungJeRhetorik);
    }

    /// <summary>
    /// Wie sich die Gläubigen eines Jahres auf diese Kirchen verteilen: (Gläubige, Kollekte) je Kirche.
    /// Ohne Glauben nach Anziehung. Mit Glauben bekommt jede Konfession ihren Anteil an der Stadt und verteilt ihn
    /// auf die Kirchen ihrer Konfession; gibt es keine, gehen ihre Gläubigen in fremde Kirchen und spenden weniger.
    /// </summary>
    public Dictionary<Betrieb, (double Glaeubige, double Kollekte)> Verteilen(IEnumerable<Betrieb> kirchen)
    {
        var liste = kirchen.ToList();
        var ergebnis = liste.ToDictionary(b => b, _ => (Glaeubige: 0.0, Kollekte: 0.0));
        var gw = _w.Glaube;
        var gruppen = gw.IstAktiv
            ? gw.Anteile.OrderBy(x => x.Key).Select(x => (Konfession: (Glaube.Konfession?)x.Key, x.Value)).ToList()
            : new() { (null, 1.0) };
        foreach (var (konfession, anteil) in gruppen)
        {
            // Eine Kirche ohne Familie (keine Konfession) nimmt jeden auf.
            var eigene = konfession is null ? liste : liste.Where(b => gw.KonfessionVon(b) is null || gw.KonfessionVon(b) == konfession).ToList();
            var faktor = 1.0;
            if (eigene.Count == 0)
            {
                eigene = liste;
                faktor = gw.Daten.FremdeKirche;
            }
            var summe = eigene.Sum(Anziehung);
            if (summe <= 0) continue;
            foreach (var b in eigene)
            {
                var glaeubige = Daten.GlaeubigeJeJahr * anteil * Anziehung(b) / summe;
                var (g, k) = ergebnis[b];
                ergebnis[b] = (g + glaeubige, k + glaeubige * Daten.SpendeJeGlaeubigem * faktor);
            }
        }
        return ergebnis;
    }

    /// <summary>Gläubige im Jahr für diese Kirche bei der heutigen Konkurrenz.</summary>
    public double ErwarteteGlaeubige(Betrieb b) => Verteilen(Kirchen.Append(b).Distinct())[b].Glaeubige;

    /// <summary>Kollekte im Jahr für diese Kirche bei der heutigen Konkurrenz.</summary>
    public double ErwarteteKollekte(Betrieb b) => Verteilen(Kirchen.Append(b).Distinct())[b].Kollekte;

    /// <summary>Jede volle Stunde – zur Messe spenden die Gläubigen.</summary>
    internal void Stunde(Spielzeit zeit)
    {
        if (!IstAktiv || zeit.UhrzeitInMinuten / 60 != Daten.MesseUm) return;
        // Bürger-KI: Wie viele Einwohner zur Messe in den Kirchen waren, verschiebt Gläubige und Kollekte (Faktor um 1).
        // Ohne Kopplung (Abschnitt „buerger" oder „kopplung.kirche" fehlt) ist der Faktor genau 1.
        var faktor = _w.Einwohnerschaft.Messefaktor();
        foreach (var (b, (g0, k0)) in Verteilen(Kirchen))
        {
            var (glaeubige, kollekte) = faktor == 1 ? (g0, k0) : (g0 * faktor, k0 * faktor);
            if (glaeubige <= 0) continue;
            b.Konto.Umsatz(kollekte); // Spenden sind steuerfrei
            var buch = DiesesJahr(b);
            _diesesJahr[b.Name] = buch with { Glaeubige = buch.Glaeubige + glaeubige, Kollekte = buch.Kollekte + kollekte };
        }
    }

    // ── Ablass ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Welche Tat ein Ablass tilgen würde: zuerst eine, für die andere schon Beweise haben (am gefährlichsten),
    /// sonst die jüngste.
    /// </summary>
    public Spur? AblassFuer(Konto konto) =>
        _w.Justiz.IstAktiv
            ? _w.Justiz.SpurenVon(konto).OrderByDescending(s => _w.Justiz.BeweiseFuer(s)).ThenByDescending(s => s.Jahr).ThenByDescending(s => s.Nummer).FirstOrDefault()
            : null;

    public double Ablasspreis(Spur spur) => Daten.Ablass.Grundpreis + Daten.Ablass.AnteilTat * spur.Betrag;

    internal Befehlsergebnis AblassKaufen(Konto kaeufer, Betrieb kirche)
    {
        if (!IstKirche(kirche)) return Befehlsergebnis.Abgelehnt($"{kirche.Name} ist keine Kirche.");
        if (kirche.Konto == kaeufer) return Befehlsergebnis.Abgelehnt("Sich selbst spricht kein Prediger los – dafür braucht es eine fremde Kirche.");
        if (!kirche.Konto.IstAktiv) return Befehlsergebnis.Abgelehnt($"In {kirche.Name} predigt niemand mehr.");
        if (AblassFuer(kaeufer) is not { } spur) return Befehlsergebnis.Abgelehnt($"{kaeufer.Inhaber} hat nichts zu bereuen.");
        var preis = Ablasspreis(spur);
        var waehrung = _w.Daten.Allgemein.Waehrung;
        if (kaeufer.Vermoegen < preis) return Befehlsergebnis.Abgelehnt($"Der Ablass kostet {Zahlenformat.Ganz(preis)} {waehrung}.");
        kaeufer.Gerichtskosten(preis);
        kirche.Konto.Umsatz(preis);
        var buch = DiesesJahr(kirche);
        _diesesJahr[kirche.Name] = buch with { Ablaesse = buch.Ablaesse + 1, Ablassgeld = buch.Ablassgeld + preis };
        var beschreibung = spur.Beschreibung;
        _w.Justiz.Lossprechen(spur);
        _beichtwissen.RemoveWhere(x => x.Spur == spur.Nummer);
        _w.Melden($"{kaeufer.Inhaber} kauft in {kirche.Name} einen Ablass.");
        return Befehlsergebnis.Ok($"Ablass für {Zahlenformat.Ganz(preis)} {waehrung}: „{beschreibung}“ ist vergeben – " +
                                  "die Tat verfällt, Beweise dafür sind wertlos.");
    }

    // ── Beichte ─────────────────────────────────────────────────────────────

    /// <summary>Taten, die dieser Prediger aus der Beichte kennt (und die noch nicht verjährt oder vergeben sind).</summary>
    public IEnumerable<Spur> Beichtwissen(Konto prediger, Konto gegen) =>
        _w.Justiz.SpurenVon(gegen).Where(s => _beichtwissen.Contains((prediger, s.Nummer)) && !_w.Justiz.KenntSpur(prediger, s));

    internal Befehlsergebnis BeichtgeheimnisBrechen(Konto prediger, Konto gegen)
    {
        if (!_w.Justiz.IstAktiv) return Befehlsergebnis.Abgelehnt("Ohne Gericht nützt kein Geheimnis.");
        var wissen = Beichtwissen(prediger, gegen).ToList();
        if (wissen.Count == 0) return Befehlsergebnis.Abgelehnt($"Aus der Beichte wisst ihr nichts gegen {gegen.Inhaber}.");
        foreach (var spur in wissen)
        {
            _w.Justiz.BeweisGeben(prediger, spur);
            _beichtwissen.Remove((prediger, spur.Nummer));
        }
        _w.Justiz.TatMelden(prediger, Vergehen.Beichtbruch, gegen.Inhaber, Daten.Beichte.Betrag);
        return Befehlsergebnis.Ok($"Ihr brecht das Beichtgeheimnis: {wissen.Count} {(wissen.Count == 1 ? "Beweis" : "Beweise")} gegen {gegen.Inhaber}. " +
                                  "Das ist selbst ein Vergehen – hoffentlich erfährt es niemand.");
    }

    // ── Schriften ───────────────────────────────────────────────────────────

    /// <summary>Eine Schrift aus dem eigenen Lager nehmen – oder am Markt kaufen. Liefert den Preis (0 aus dem Lager) oder null.</summary>
    private double? Beschaffen(Konto konto, string ware, Action<double> bezahlen)
    {
        if (_w.Betriebe.FirstOrDefault(b => b.Konto == konto && b.ImLager(ware) > 0) is { } lager)
        {
            lager.Auslagern(ware, 1);
            return 0;
        }
        var preis = _w.Markt[ware].Preis;
        if (konto.Vermoegen < preis) return null;
        bezahlen(_w.Markt[ware].Kaufen(1));
        return preis;
    }

    /// <summary>Was die Schrift kostet: 0 aus dem eigenen Lager, sonst der Marktpreis.</summary>
    public double Schriftpreis(Konto konto, string ware) =>
        _w.Betriebe.Any(b => b.Konto == konto && b.ImLager(ware) > 0) ? 0 : _w.Markt[ware].Preis;

    public string? LesenVerboten(Konto konto, Talent talent)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Schriften.";
        if (!_w.Familien.HatTalente || _w.Familien.Von(konto) is not { } f) return $"{konto.Inhaber} hat keine Familie, die lesen könnte.";
        if (_gelesen.GetValueOrDefault(konto) == Jahr) return $"{f.Oberhaupt.Vorname} hat dieses Jahr schon eine Lehrschrift gelesen.";
        if (f.Oberhaupt.Anlage(talent) >= Math.Min(Daten.LehrschriftBis, _w.Familien.Daten.Talente.Hoechstwert) - 1e-9)
            return $"Über {talent.Name()} kann {f.Oberhaupt.Vorname} aus Lehrschriften nichts mehr lernen (bis {TalentTexte.Sterne(Daten.LehrschriftBis)} Sterne).";
        if (Schriftpreis(konto, Daten.Lehrschrift) > konto.Vermoegen) return "Für eine Lehrschrift fehlt das Geld.";
        return null;
    }

    internal Befehlsergebnis LehrschriftLesen(Konto konto, Talent talent)
    {
        if (LesenVerboten(konto, talent) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var preis = Beschaffen(konto, Daten.Lehrschrift, konto.Familienkosten)!.Value;
        var o = _w.Familien.Von(konto)!.Oberhaupt;
        var schritt = _w.Daten.Aktionspunkte?.TrainingSchritt ?? 0.5;
        o.TalentSetzen(talent, Math.Min(Math.Min(Daten.LehrschriftBis, _w.Familien.Daten.Talente.Hoechstwert), o.Anlage(talent) + schritt));
        _gelesen[konto] = Jahr;
        return Befehlsergebnis.Ok($"{o.Vorname} liest eine Lehrschrift ({(preis > 0 ? $"{Zahlenformat.Ganz(preis)} {_w.Daten.Allgemein.Waehrung} am Markt" : "aus dem eigenen Lager")}): " +
                                  $"{talent.Name()} jetzt {TalentTexte.Sterne(o.Anlage(talent))} Sterne.");
    }

    /// <summary>Ansehensverlust durch eine Hetzschrift, die noch wirkt.</summary>
    public double AnsehenAbzug(Konto konto) =>
        IstAktiv && _gehetztBis.GetValueOrDefault(konto) >= Jahr ? Daten.Hetzschrift.Ansehen : 0;

    public int? GehetztBis(Konto konto) => _gehetztBis.TryGetValue(konto, out var bis) && bis >= Jahr ? bis : null;

    internal Befehlsergebnis HetzschriftVerbreiten(Konto autor, Konto ziel)
    {
        if (!IstAktiv) return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Schriften.");
        if (ziel == autor || !ziel.IstAktiv) return Befehlsergebnis.Abgelehnt("Gegen dieses Haus lohnt keine Hetzschrift.");
        var ware = Daten.Hetzschrift.Ware;
        if (Schriftpreis(autor, ware) > autor.Vermoegen) return Befehlsergebnis.Abgelehnt("Für eine Hetzschrift fehlt das Geld.");
        var preis = Beschaffen(autor, ware, autor.Gaunerkosten)!.Value;
        _gehetztBis[ziel] = Jahr + 1;
        _w.Justiz.TatMelden(autor, Vergehen.Verleumdung, ziel.Inhaber, preis > 0 ? preis : _w.Markt[ware].Preis);
        _w.Melden($"In der Stadt geht eine Hetzschrift gegen {ziel.Inhaber} um.");
        return Befehlsergebnis.Ok($"Die Hetzschrift gegen {ziel.Inhaber} geht von Hand zu Hand: {Zahlenformat.Ganz(Daten.Hetzschrift.Ansehen)} Ansehen weniger " +
                                  $"bis Ende {Jahr + 1}. Eine Verleumdung – wer sie euch nachweist, kann euch anklagen.");
    }

    // ── Jahresende ──────────────────────────────────────────────────────────

    /// <summary>Beichte (vor dem Gerichtstag nicht nötig – das Wissen wirkt erst, wenn der Prediger es preisgibt), dann neues Kirchenbuch.</summary>
    internal void Jahresende()
    {
        if (!IstAktiv) return;
        if (_w.Justiz.IstAktiv)
            foreach (var prediger in Kirchen.Select(b => b.Konto).Distinct().ToList())
                foreach (var spur in _w.Konten.Where(k => k != prediger).SelectMany(_w.Justiz.SpurenVon).ToList())
                    if (!_beichtwissen.Contains((prediger, spur.Nummer)) && !_w.Justiz.KenntSpur(prediger, spur)
                        && _zufall.NextDouble() < Daten.Beichte.Chance)
                        _beichtwissen.Add((prediger, spur.Nummer));
        _vorjahr.Clear();
        foreach (var (name, buch) in _diesesJahr) _vorjahr[name] = buch;
        _diesesJahr.Clear();
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    internal KirchenStand Sichern(Func<Konto, int> nr) => new()
    {
        ZufallWuerfe = _zufall.Wuerfe,
        DiesesJahr = _diesesJahr.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value),
        Vorjahr = _vorjahr.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value),
        Beichtwissen = _beichtwissen.OrderBy(x => nr(x.Prediger)).ThenBy(x => x.Spur).Select(x => new[] { nr(x.Prediger), x.Spur }).ToList(),
        Gelesen = _gelesen.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
        GehetztBis = _gehetztBis.OrderBy(x => nr(x.Key)).ToDictionary(x => nr(x.Key).ToString(), x => x.Value),
    };

    internal void Wiederherstellen(KirchenStand s, IReadOnlyList<Konto> konten)
    {
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        foreach (var (k, v) in s.DiesesJahr) _diesesJahr[k] = v;
        foreach (var (k, v) in s.Vorjahr) _vorjahr[k] = v;
        foreach (var x in s.Beichtwissen) _beichtwissen.Add((konten[x[0]], x[1]));
        foreach (var (k, v) in s.Gelesen) _gelesen[konten[int.Parse(k)]] = v;
        foreach (var (k, v) in s.GehetztBis) _gehetztBis[konten[int.Parse(k)]] = v;
    }
}
