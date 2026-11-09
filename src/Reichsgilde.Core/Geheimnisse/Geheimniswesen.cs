using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Geheimnisse;

/// <summary>Ein Geheimnis einer Person der Häuser (Oberhaupt, Ehepartner, Erbe) oder eines Ratsherrn.</summary>
public sealed class Geheimnis
{
    public int Nummer { get; }
    public Geheimnisart Art { get; }

    /// <summary>Wer es hütet: <see cref="ICharakter.Kennung"/> („P12“, „R:Ulrich Wendelstein“).</summary>
    public string Inhaber { get; }

    /// <summary>Seit diesem Jahr.</summary>
    public int Jahr { get; }

    /// <summary>Nur bei Mord: die Nummer des Opfers.</summary>
    public int? Opfer { get; }

    /// <summary>Die ganze Stadt weiß es (nach Anzeige oder Bloßstellen) – dann taugt es zu nichts mehr.</summary>
    public bool Oeffentlich { get; internal set; }

    /// <summary>Nur bei Ehebruch: Der Ehepartner hat es selbst entdeckt (privat, nur einmal).</summary>
    public bool EhepartnerWeiss { get; internal set; }

    internal Geheimnis(int nummer, Geheimnisart art, string inhaber, int jahr, int? opfer = null)
    {
        Nummer = nummer;
        Art = art;
        Inhaber = inhaber;
        Jahr = jahr;
        Opfer = opfer;
    }
}

/// <summary>Was ein Haus von einem Geheimnis weiß – woher und seit wann.</summary>
public sealed class GeheimnisWissen
{
    public Konto Haus { get; }
    public int Geheimnis { get; }
    public int Jahr { get; }

    /// <summary>„Spitzel“, „Gerede“, „Beichte“, „Entwicklerhilfe“.</summary>
    public string Quelle { get; }

    /// <summary>Aus der Beichte: Nutzen kann es der Prediger erst, wenn er das Beichtgeheimnis bricht.</summary>
    public bool AusBeichte { get; internal set; }

    /// <summary>In diesem Jahr wurde damit schon eine Ratsstimme erpresst (einmal je Geheimnis und Jahr).</summary>
    public int? GenutztIm { get; internal set; }

    /// <summary>In diesem Jahr ging ein Erpresserbrief um Gulden hinaus (ist er beantwortet und das Wissen noch da, hat das Opfer nicht gezahlt).</summary>
    public int? BriefIm { get; internal set; }

    internal GeheimnisWissen(Konto haus, int geheimnis, int jahr, string quelle, bool ausBeichte = false, int? genutztIm = null, int? briefIm = null)
    {
        Haus = haus;
        Geheimnis = geheimnis;
        Jahr = jahr;
        Quelle = quelle;
        AusBeichte = ausBeichte;
        GenutztIm = genutztIm;
        BriefIm = briefIm;
    }
}

/// <summary>Eine erpresste Ratsstimme: In der nächsten Sitzung stimmt der Sitz des Opfers (Ratsherr oder Haus) wie der Erpresser.</summary>
public sealed record ErpressteStimme(Konto Erpresser, Ratsherr? Ratsherr, Konto? Haus, int Geheimnis, int Jahr);

/// <summary>Eine Anzeige gegen einen Ratsherrn – verhandelt am Gerichtstag. (Gegen ein Haus wird ein gewöhnliches Verfahren daraus.)</summary>
public sealed record RatsherrVerfahren(Konto Anklaeger, Ratsherr Beklagter, int Geheimnis, Geheimnisart Art, int Jahr, IReadOnlyList<Richter> Richter);

/// <summary>
/// Geheimnisse (Charaktersystem Teil 3c, Abschnitt "geheimnisse"): Ehebruch, Ketzerei, Schulden und Mord – bei Oberhaupt, Ehepartner und Erbe
/// der Häuser und bei den Ratsherren.
///
/// Entstehen: am Jahresende (Ehebruch, Ketzerei der Zyniker, Schulden, bei Ratsherren), beim Glaubenswechsel (Ketzerei) und beim Tod eines
/// Opfers von Krötenschleim oder Giftdolch (Mord). Entdecken: Spitzel (auf ein Haus oder einen Ratsherrn, am Gerichtstag), Gerede, Beichte,
/// der Ehepartner (privat). Nutzen (Befehl <see cref="Wirtschaft.GeheimnisNutzen"/>): Erpressen um Gulden oder die Ratsstimme, Anzeige vor
/// Gericht, Bloßstellen – oder schweigen. Folgen für den Inhaber: Meinung, Ansehen und Stress.
///
/// Gewürfelt wird nur mit dem Abschnitt und mit einem eigenen Generator – alle übrigen Würfe bleiben wie vorher.
/// </summary>
public sealed partial class Geheimniswesen
{
    private readonly Stadtwirtschaft _w;
    private readonly GeheimnisDaten? _d;
    private Zufallsquelle _zufall;
    private readonly List<Geheimnis> _geheimnisse = new();
    private readonly List<GeheimnisWissen> _wissen = new();
    private readonly List<ErpressteStimme> _stimmen = new();
    private readonly List<(Konto Auftraggeber, Ratsherr Ziel)> _ratsspitzel = new();
    private readonly List<RatsherrVerfahren> _verfahren = new();
    private readonly List<Schande> _schande = new();
    private readonly Dictionary<string, int> _gesperrt = new(); // Ratsherr → bis einschließlich Jahr kein Nachrücken
    private int _naechste = 1;

    /// <summary>Ansehen (und bei Schulden der Kreditrahmen) nach dem Bloßstellen – für das Haus oder den Ratsherrn, bis einschließlich <c>Bis</c>.</summary>
    private sealed record Schande(Konto? Haus, Ratsherr? Ratsherr, Geheimnisart Art, double Ansehen, int Bis);

    internal Geheimniswesen(Stadtwirtschaft w, GeheimnisDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 79 + 59));
        if (IstAktiv) RatsherrenBeiPartiebeginn();
    }

    /// <summary>Gibt es Geheimnisse? (Abschnitt "geheimnisse" und Familien.)</summary>
    public bool IstAktiv => _d is { IstAktiv: true } && _w.Familien.IstAktiv;

    public GeheimnisDaten Daten => _d ?? throw new InvalidOperationException("Es gibt keine Geheimnisse (geheimnisse fehlt in den Daten).");

    private int Jahr => _w.Jahr;

    // ═════════════════════════════════════════════════════════════════════════
    //  Auskunft
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Alle Geheimnisse der Stadt (für Tests und Messung; das Fenster zeigt nur, was das Haus weiß: <see cref="Sichtbar"/>).</summary>
    public IReadOnlyList<Geheimnis> Alle => _geheimnisse;

    /// <summary>Alles Wissen aller Häuser (für Tests und Messung).</summary>
    public IReadOnlyList<GeheimnisWissen> AllesWissen => _wissen;

    /// <summary>Die erpressten Ratsstimmen für die nächste Sitzung.</summary>
    public IReadOnlyList<ErpressteStimme> ErpressteStimmen => _stimmen;

    /// <summary>Anzeigen gegen Ratsherren, die am Gerichtstag verhandelt werden.</summary>
    public IReadOnlyList<RatsherrVerfahren> RatsherrVerfahren => _verfahren;

    public Geheimnis? Nummer(int nummer) => _geheimnisse.FirstOrDefault(g => g.Nummer == nummer);

    /// <summary>Die Geheimnisse dieser Person bzw. dieses Ratsherrn (alle – öffentliche wie geheime).</summary>
    public IEnumerable<Geheimnis> Von(ICharakter c) => _geheimnisse.Where(g => g.Inhaber == c.Kennung);

    /// <summary>Hütet die Person (noch) ein Geheimnis dieser Art?</summary>
    public bool Hat(ICharakter c, Geheimnisart art) => _geheimnisse.Any(g => g.Inhaber == c.Kennung && g.Art == art);

    /// <summary>Wer das Geheimnis hütet (null: die Person gibt es nicht mehr).</summary>
    public ICharakter? Inhaber(Geheimnis g) => _w.Meinungen.Charakter(g.Inhaber);

    /// <summary>Das Haus des Inhabers (null bei Ratsherren).</summary>
    public Konto? HausVon(Geheimnis g) => Inhaber(g) is Person p ? _w.Familien.FamilieVon(p)?.Konto : null;

    /// <summary>Ein Geheimnis des eigenen Hauses?</summary>
    public bool IstEigen(Konto haus, Geheimnis g) => HausVon(g) == haus;

    /// <summary>Was dieses Haus über ein Geheimnis weiß (null: nichts).</summary>
    public GeheimnisWissen? Wissen(Konto haus, Geheimnis g) => _wissen.FirstOrDefault(w => w.Haus == haus && w.Geheimnis == g.Nummer);

    /// <summary>Kennt das Haus das Geheimnis so, dass es es nutzen kann (nicht nur aus der Beichte)?</summary>
    public bool KenntNutzbar(Konto haus, Geheimnis g) => Wissen(haus, g) is { AusBeichte: false };

    /// <summary>
    /// Was das Fenster bei dieser Person zeigt: beim eigenen Haus alles („Ihr hütet“), sonst, was das Haus weiß (auch aus der Beichte),
    /// und was öffentlich ist.
    /// </summary>
    public IEnumerable<Geheimnis> Sichtbar(Konto haus, ICharakter c) =>
        Von(c).Where(g => g.Oeffentlich || IstEigen(haus, g) || Wissen(haus, g) is not null);

    /// <summary>Alle fremden Geheimnisse, die dieses Haus nutzen kann (für die KI).</summary>
    public IEnumerable<Geheimnis> NutzbarFuer(Konto haus) =>
        _geheimnisse.Where(g => !g.Oeffentlich && !IstEigen(haus, g) && KenntNutzbar(haus, g));

    /// <summary>„Ehebruch“, „Mord an Agnes Weber“.</summary>
    public string Beschreibung(Geheimnis g)
    {
        var name = Daten.Art(g.Art).Name;
        return g.Opfer is int opfer && _w.Familien.PersonNummer(opfer) is { } p ? $"{name} an {p.Name}" : name;
    }

    /// <summary>Der Sitz im Rat, um den es bei der Ratsstimme geht: das Amt des Ratsherrn bzw. des Hauses (null: keiner).</summary>
    public Amt? SitzVon(Geheimnis g) => Inhaber(g) switch
    {
        Ratsherr r => _w.Rat.Aemter.FirstOrDefault(a => a.Ratsherr == r),
        Person => HausVon(g) is { } k ? _w.Rat.AmtVon(k) : null,
        _ => null,
    };

    /// <summary>Wer die Stimme dieses Sitzes für die nächste Sitzung erpresst hat (null: niemand).</summary>
    public Konto? ErpresserVon(Amt sitz) =>
        _stimmen.FirstOrDefault(s => sitz.Ratsherr is { } r ? s.Ratsherr == r : sitz.Konto is { } k && s.Haus == k) is { Erpresser.IstAktiv: true } e ? e.Erpresser : null;

    /// <summary>Wer die Ratsstimme dieses Hauses für die nächste Sitzung erpresst hat (null: niemand).</summary>
    public Konto? ErpresserVon(Konto haus) => _stimmen.FirstOrDefault(s => s.Haus == haus) is { Erpresser.IstAktiv: true } e ? e.Erpresser : null;

    /// <summary>Was das Haus an Ansehen verliert, weil ein Geheimnis bloßgestellt wurde.</summary>
    public double AnsehenAbzug(Konto konto) => IstAktiv ? _schande.Where(s => s.Haus == konto && s.Bis >= Jahr).Sum(s => s.Ansehen) : 0;

    /// <summary>So viele Wahlpunkte verliert der Ratsherr, weil ein Geheimnis von ihm bloßgestellt wurde.</summary>
    public double AnsehenAbzug(Ratsherr r) => IstAktiv ? _schande.Where(s => s.Ratsherr == r && s.Bis >= Jahr).Sum(s => s.Ansehen) : 0;

    /// <summary>Faktor auf den Kreditrahmen bei den Geldleihern (Schulden bloßgestellt: 0,75), sonst 1.</summary>
    public double KreditFaktor(Konto konto) =>
        IstAktiv && _schande.Any(s => s.Haus == konto && s.Bis >= Jahr && s.Art == Geheimnisart.Schulden) ? Daten.Art(Geheimnisart.Schulden).KreditFaktor : 1;

    /// <summary>Bis zu welchem Jahr das Haus bzw. der Ratsherr bloßgestellt ist (null: nicht).</summary>
    public int? SchandeBis(Konto konto) => IstAktiv && _schande.Where(s => s.Haus == konto && s.Bis >= Jahr).Select(s => (int?)s.Bis).Max() is { } bis ? bis : null;

    /// <summary>Darf dieser Ratsherr (verurteilt) gerade in kein Amt nachrücken?</summary>
    public bool IstGesperrt(Ratsherr r) => _gesperrt.TryGetValue(r.Name, out var bis) && bis >= Jahr;

    /// <summary>Hat dieses Haus dieses Jahr schon einen Spitzel auf den Ratsherrn angesetzt?</summary>
    public bool HatRatsspitzel(Konto auftraggeber, Ratsherr r) => _ratsspitzel.Contains((auftraggeber, r));

    /// <summary>So viel Stress im Jahr bringt es der Person, ihre Geheimnisse zu hüten (nur die geheimen; 0 ohne).</summary>
    internal double StressJeJahr(Person p)
    {
        if (!IstAktiv) return 0;
        var anzahl = _geheimnisse.Count(g => g.Inhaber == p.Kennung && !g.Oeffentlich);
        if (anzahl == 0) return 0;
        var s = Daten.Stress;
        var je = p.Zuege.Select(z => s.JahrZuege.GetValueOrDefault(z.Kennung)).Append(s.Jahr).Max();
        return anzahl * je;
    }

    /// <summary>Wie viele Geheimnisse die Person hütet (geheim) – für den Tooltip des Stresses.</summary>
    public int GeheimeAnzahl(ICharakter c) => _geheimnisse.Count(g => g.Inhaber == c.Kennung && !g.Oeffentlich);

    // ═════════════════════════════════════════════════════════════════════════
    //  Hilfen
    // ═════════════════════════════════════════════════════════════════════════

    private static double ZugFaktor(IEnumerable<Charakterzug> zuege, IReadOnlyDictionary<string, double> faktoren) =>
        zuege.Aggregate(1.0, (f, z) => f * (faktoren.TryGetValue(z.Kennung, out var x) ? x : 1));

    /// <summary>Schutz der Familie des Inhabers (Knoten Verschwiegen: 0,6) – gilt für jede Entdeckung durch andere.</summary>
    private double Schutz(Geheimnis g) => HausVon(g) is { } k ? _w.Familien.Lebenswirkung(k).GeheimnisSchutz : 1;

    /// <summary>Ein neues Geheimnis – die eigene Familie erfährt es vertraulich (Ratsherren: niemand).</summary>
    private Geheimnis Neu(Geheimnisart art, ICharakter inhaber, int? opfer = null)
    {
        var g = new Geheimnis(_naechste++, art, inhaber.Kennung, Jahr, opfer);
        _geheimnisse.Add(g);
        if (inhaber is Person p && _w.Familien.FamilieVon(p) is { } f)
            _w.Melden(Meldungsart.Sonstiges, $"Ein Geheimnis im Haus {f.Nachname}: {p.Vorname} hütet nun „{Beschreibung(g)}“. " +
                                              "Erfährt es ein anderes Haus, kann es euch erpressen, anzeigen oder bloßstellen.", haus: f.Konto, person: p, vertraulich: true);
        return g;
    }

    /// <summary>Ein Geheimnis ist vorbei (Inhaber tot, Schulden beglichen): mit allem Wissen darüber.</summary>
    private void Entfernen(Geheimnis g)
    {
        _geheimnisse.Remove(g);
        _wissen.RemoveAll(w => w.Geheimnis == g.Nummer);
    }

    /// <summary>Das Haus erfährt das Geheimnis (oder kann es nun nutzen, wenn es es bisher nur aus der Beichte kannte). true: etwas Neues.</summary>
    private bool WissenGeben(Konto haus, Geheimnis g, string quelle, bool ausBeichte = false)
    {
        if (IstEigen(haus, g)) return false;
        if (Wissen(haus, g) is { } w)
        {
            if (ausBeichte || !w.AusBeichte) return false;
            w.AusBeichte = false;
            return true;
        }
        _wissen.Add(new GeheimnisWissen(haus, g.Nummer, Jahr, quelle, ausBeichte));
        return true;
    }

    /// <summary>Stress beim Inhaber (nur Oberhaupt, Erbe, Ehepartner – Ratsherren haben keinen).</summary>
    private void InhaberStress(Geheimnis g, double wert, string text)
    {
        if (wert > 0 && Inhaber(g) is Person p) _w.Familien.StressAendern(p, wert, text);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal GeheimnisseStand Sichern(Func<Konto, int> nr) => new()
    {
        ZufallWuerfe = _zufall.Wuerfe,
        NaechsteNummer = _naechste,
        Geheimnisse = _geheimnisse.Select(g => new GeheimnisStand
        {
            Nummer = g.Nummer, Art = g.Art, Inhaber = g.Inhaber, Jahr = g.Jahr, Opfer = g.Opfer, Oeffentlich = g.Oeffentlich, EhepartnerWeiss = g.EhepartnerWeiss,
        }).ToList(),
        Wissen = _wissen.Select(w => new GeheimnisWissenStand
        {
            Haus = nr(w.Haus), Geheimnis = w.Geheimnis, Jahr = w.Jahr, Quelle = w.Quelle, AusBeichte = w.AusBeichte, GenutztIm = w.GenutztIm, BriefIm = w.BriefIm,
        }).ToList(),
        Stimmen = _stimmen.Select(s => new ErpressteStimmeStand
        {
            Erpresser = nr(s.Erpresser), Ratsherr = s.Ratsherr?.Name, Haus = s.Haus is null ? null : nr(s.Haus), Geheimnis = s.Geheimnis, Jahr = s.Jahr,
        }).ToList(),
        Ratsspitzel = _ratsspitzel.Select(s => new RatsspitzelStand { Auftraggeber = nr(s.Auftraggeber), Ratsherr = s.Ziel.Name }).ToList(),
        Verfahren = _verfahren.Select(v => new RatsherrVerfahrenStand
        {
            Anklaeger = nr(v.Anklaeger), Ratsherr = v.Beklagter.Name, Geheimnis = v.Geheimnis, Art = v.Art, Jahr = v.Jahr,
            Richter = v.Richter.Select(r => new RichterStand { Rolle = r.Rolle, Ratsherr = r.Ratsherr?.Name, Konto = r.Konto is null ? null : nr(r.Konto) }).ToList(),
        }).ToList(),
        Schande = _schande.Select(s => new SchandeStand
        {
            Haus = s.Haus is null ? null : nr(s.Haus), Ratsherr = s.Ratsherr?.Name, Art = s.Art, Ansehen = s.Ansehen, Bis = s.Bis,
        }).ToList(),
        Gesperrt = _gesperrt.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value),
    };

    /// <summary>Nach Rat, Familien und Meinungen. Ohne Stand (dürfte mit Abschnitt nicht vorkommen) bleibt es beim Partiebeginn.</summary>
    internal void Wiederherstellen(GeheimnisseStand? s, IReadOnlyList<Konto> konten)
    {
        if (s is null) return;
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        _naechste = s.NaechsteNummer;
        _geheimnisse.Clear();
        foreach (var g in s.Geheimnisse)
            _geheimnisse.Add(new Geheimnis(g.Nummer, g.Art, g.Inhaber, g.Jahr, g.Opfer) { Oeffentlich = g.Oeffentlich, EhepartnerWeiss = g.EhepartnerWeiss });
        _wissen.Clear();
        _wissen.AddRange(s.Wissen.Select(w => new GeheimnisWissen(konten[w.Haus], w.Geheimnis, w.Jahr, w.Quelle, w.AusBeichte, w.GenutztIm, w.BriefIm)));
        _stimmen.Clear();
        foreach (var x in s.Stimmen)
            _stimmen.Add(new ErpressteStimme(konten[x.Erpresser], x.Ratsherr is { } n ? _w.Rat.RatsherrNamens(n) : null, x.Haus is int h ? konten[h] : null, x.Geheimnis, x.Jahr));
        _ratsspitzel.Clear();
        foreach (var x in s.Ratsspitzel)
            if (_w.Rat.RatsherrNamens(x.Ratsherr) is { } r) _ratsspitzel.Add((konten[x.Auftraggeber], r));
        _verfahren.Clear();
        foreach (var v in s.Verfahren)
            if (_w.Rat.RatsherrNamens(v.Ratsherr) is { } r)
                _verfahren.Add(new RatsherrVerfahren(konten[v.Anklaeger], r, v.Geheimnis, v.Art, v.Jahr,
                    v.Richter.Select(x => new Richter(x.Rolle, x.Ratsherr is { } n ? _w.Rat.RatsherrNamens(n) : null, x.Konto is int k ? konten[k] : null)).ToList()));
        _schande.Clear();
        foreach (var x in s.Schande)
            _schande.Add(new Schande(x.Haus is int h ? konten[h] : null, x.Ratsherr is { } n ? _w.Rat.RatsherrNamens(n) : null, x.Art, x.Ansehen, x.Bis));
        _gesperrt.Clear();
        foreach (var (name, bis) in s.Gesperrt) _gesperrt[name] = bis;
    }
}
