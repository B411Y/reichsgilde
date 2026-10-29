using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Dynastie;

/// <summary>Ein Familienereignis für Jahresbericht und Protokoll.</summary>
public sealed record FamilienEreignis(Konto Konto, string Text);

/// <summary>
/// Das Familienbuch der Stadt: alle Familien der Häuser und die ledigen Heiratskandidaten.
///
/// Über das Jahr:
///   • Häuser werben mit Geschenken um Kandidaten (Befehl Werben) und heiraten (Befehl Heiraten).
///   • Das Oberhaupt kann im Testament einen Erben bestimmen (Befehl ErbenBestimmen).
///
/// Am Jahresende (in dieser Reihenfolge):
///   1. Haushalt und Unterhalt – noch im alten Jahr verbucht
///   2. Todesfälle, dann der Erbfall, falls das Oberhaupt gestorben ist
///   3. Geburten
///   4. Kandidaten verlassen die Brautschau, neue kommen dazu; Zuneigung verblasst
///
/// Leben und Tod würfelt ein eigener Zufallsgenerator. So verändert die Familie nicht die Würfe
/// der Wirtschaft (Überfälle, Ausbrüche), und alte Messungen bleiben vergleichbar.
/// </summary>
public sealed partial class Familienbuch
{
    private readonly Stadtwirtschaft _w;
    private readonly DynastieDaten _d;
    private Zufallsquelle _zufall;
    private Zufallsquelle _talentZufall;
    private Zufallsquelle _aussehenZufall;
    private readonly Dictionary<Konto, Familie> _familien = new();
    private readonly List<Kandidat> _kandidaten = new();
    private readonly Dictionary<(Konto, int), double> _zuneigung = new();
    private readonly Dictionary<(Konto, int), double> _zuneigungDiesesJahr = new();
    private List<FamilienEreignis> _laufend = new();
    private List<FamilienEreignis> _letztesJahr = new();
    private int _naechsteNummer = 1;

    public DynastieDaten Daten => _d;
    public bool IstAktiv => _d.IstAktiv;

    /// <summary>Das laufende Jahr (für Alter und Fruchtbarkeit).</summary>
    public int Jahr { get; private set; } = Zeit.Spielzeit.StartJahr;

    public IReadOnlyList<Kandidat> Kandidaten => _kandidaten;

    /// <summary>Was in der Familie im letzten abgeschlossenen Jahr geschah (Heiraten, Geburten, Todesfälle).</summary>
    public IReadOnlyList<FamilienEreignis> LetztesJahr => _letztesJahr;

    /// <summary>Was in diesem Jahr bisher geschah.</summary>
    public IReadOnlyList<FamilienEreignis> DiesesJahr => _laufend;

    /// <summary>Das Oberhaupt eines Hauses ist gestorben: (Familie, Verstorbener, Nachfolger).</summary>
    public event Action<Familie, Person, Person>? Erbfall;

    /// <summary>Ein Haus ist ohne Erben ausgestorben.</summary>
    public event Action<Konto>? Erloschen;

    internal Familienbuch(Stadtwirtschaft w, DynastieDaten daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 17 + 3));
        // Eigener Generator für Talente: So bleiben Geburten, Todesfälle und Kandidaten wie vor den Talenten.
        _talentZufall = new Zufallsquelle(unchecked(zufallsstartwert * 37 + 13));
        // Und einer fürs Aussehen: Porträts ändern keinen Wurf der Familien oder der Wirtschaft.
        _aussehenZufall = new Zufallsquelle(unchecked(zufallsstartwert * 53 + 29));
        // Charakterzüge: eigener Generator für die erworbenen (Familienbuch.Zuege.cs) – die übrigen Würfe bleiben wie vorher.
        _zugZufall = new Zufallsquelle(unchecked(zufallsstartwert * 59 + 31));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Talente
    // ═════════════════════════════════════════════════════════════════════════

    public TalentDaten TalentDaten => _d.Talente;
    public bool HatTalente => _d.Talente.IstAktiv;

    /// <summary>Das Talent des Oberhaupts – es führt das Haus (0 ohne Familie oder ohne Talentdaten).</summary>
    public double Talent(Konto konto, Talent t) => HatTalente && Von(konto) is { } f ? f.Oberhaupt.Talent(t) : 0;

    private TalentWirkung W => _d.Talente.Wirkung;

    /// <summary>Faktor auf die Markterlöse (Verhandeln).</summary>
    public double VerkaufsFaktor(Konto k) => 1 + Talent(k, Dynastie.Talent.Verhandeln) * W.Verhandeln;

    /// <summary>Faktor auf den Einkaufspreis beim Handel von Hand (Verhandeln senkt ihn so, wie es den Erlös hebt).</summary>
    public double EinkaufsFaktor(Konto k) => Math.Max(0.5, 1 - Talent(k, Dynastie.Talent.Verhandeln) * W.Verhandeln);

    /// <summary>Faktor auf die Leistung aller Arbeitskräfte (Handwerkskunst).</summary>
    public double LeistungsFaktor(Konto k) => 1 + Talent(k, Dynastie.Talent.Handwerk) * W.Handwerk;

    /// <summary>Zusätzliche Punkte bei Ratswahlen (Rhetorik).</summary>
    public double RhetorikPunkte(Konto k) => Talent(k, Dynastie.Talent.Rhetorik) * W.RhetorikRat;

    /// <summary>Plädoyer vor Gericht: positiv hilft dem Ankläger, negativ dem Beklagten.</summary>
    public double Plaedoyer(Konto anklaeger, Konto beklagter) =>
        (Talent(anklaeger, Dynastie.Talent.Rhetorik) - Talent(beklagter, Dynastie.Talent.Rhetorik)) * W.RhetorikGericht;

    /// <summary>Zusätzliche Erfolgschance im Hinterzimmer (Bei Nacht und Nebel).</summary>
    public double NachtBonus(Konto k) => Talent(k, Dynastie.Talent.NachtUndNebel) * W.NachtErfolg;

    /// <summary>Faktor auf die Zeugenchance (Bei Nacht und Nebel – wer geschickt ist, wird seltener gesehen).</summary>
    public double ZeugenFaktor(Konto k) => Math.Max(0, 1 - Talent(k, Dynastie.Talent.NachtUndNebel) * W.NachtZeugen);

    /// <summary>Zusätzliche Stärke angeheuerter Wegelagerer (Kampf des Auftraggebers).</summary>
    public double KampfUeberfall(Konto k) => Talent(k, Dynastie.Talent.Kampf) * W.KampfUeberfall;

    /// <summary>Zusätzlicher Schutz der eigenen Karren (Kampf).</summary>
    public double KampfSchutz(Konto k) => Talent(k, Dynastie.Talent.Kampf) * W.KampfSchutz;

    private double Runden(double wert) => Math.Clamp(Math.Round(wert * 2) / 2, 0, _d.Talente.Hoechstwert);

    /// <summary>Würfelt ± streuung in halben Schritten.</summary>
    private double Streuen(double streuung)
    {
        var schritte = (int)Math.Round(streuung * 2);
        return schritte <= 0 ? 0 : (_talentZufall.Next(2 * schritte + 1) - schritte) / 2.0;
    }

    /// <summary>Talente für einen Erwachsenen, der neu ins Spiel kommt: Grundwert des Volkes ± Streuung.</summary>
    private void TalenteWuerfeln(Person p)
    {
        if (!HatTalente) return;
        foreach (var t in TalentTexte.Alle)
            p.TalentSetzen(t, Runden(_d.Talente.Grundwert(p.Volk, t) + Streuen(_d.Talente.Streuung)));
    }

    /// <summary>Talente eines Neugeborenen: halb von den Eltern, halb vom Volk, dazu etwas Zufall.</summary>
    private void TalenteVererben(Person kind, Person mutter, Person vater)
    {
        if (!HatTalente) return;
        var td = _d.Talente;
        foreach (var t in TalentTexte.Alle)
        {
            var eltern = (mutter.Anlage(t) + vater.Anlage(t)) / 2;
            kind.TalentSetzen(t, Runden(td.Vererbung * eltern + (1 - td.Vererbung) * td.Grundwert(kind.Volk, t) + Streuen(td.Streuung / 2)));
        }
    }

    /// <summary>Ab welchem Alter Kinder dieses Volkes ausgebildet werden.</summary>
    public int AusbildungAb(Volk volk) => (int)Math.Ceiling(_d.Volk(volk).Volljaehrig * _d.Talente.AusbildungAb);

    public bool IstInAusbildungsalter(Person p) =>
        p.Lebt && !IstVolljaehrig(p) && p.Alter(Jahr) >= AusbildungAb(p.Volk);

    /// <summary>Zuwachs je Ausbildungsjahr – umgerechnet, damit jedes Volk am Ende gleich viel lernt.</summary>
    public double Ausbildungsfaktor(Volk volk)
    {
        var jahre = _d.Volk(volk).Volljaehrig - AusbildungAb(volk);
        return jahre <= 0 ? 0 : _d.Talente.ReferenzJahre / jahre;
    }

    /// <summary>Welche Wege einem Kind dieses Hauses offenstehen (null = ja, sonst der Grund).</summary>
    public string? AusbildungVerboten(Konto konto, string weg)
    {
        if (!_d.Talente.Ausbildungen.TryGetValue(weg, out var a)) return $"Eine Ausbildung „{weg}“ gibt es nicht.";
        if (a.MindestStand is { } st && konto.Stand < _w.Daten.StandStufe(st)) return $"{weg} steht erst Häusern ab dem Stand {st} offen.";
        return null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Auskunft
    // ═════════════════════════════════════════════════════════════════════════

    public Familie? Von(Konto konto) => _familien.GetValueOrDefault(konto);

    public Kandidat? KandidatNummer(int nummer) => _kandidaten.FirstOrDefault(k => k.Person.Nummer == nummer);

    /// <summary>Das Haus, zu dem die Person gehört (auch Verstorbene und Angeheiratete), oder null für Heiratskandidaten.</summary>
    public Familie? FamilieVon(Person p) => _familien.Values.FirstOrDefault(f => f.MitgliederListe.Contains(p));

    /// <summary>Stufe in der Ständeordnung: der Stand des Hauses, bei Heiratskandidaten der ihrer Herkunft.</summary>
    public int StandVon(Person p) =>
        FamilieVon(p)?.Konto.Stand ?? _kandidaten.FirstOrDefault(k => k.Person == p)?.Stand ?? 0;

    /// <summary>Gemeiner, Bürger, Patrizier oder Adel (Edler, Freiherr, Reichsgraf) – für den Hintergrund des Porträts.</summary>
    public Standgruppe StandgruppeVon(Person p)
    {
        int Stufe(string name, int ersatz) => _w.Daten.StandStufe(name) is var s and >= 0 ? s : ersatz;
        var stand = StandVon(p);
        return stand >= Stufe("Edler", 3) ? Standgruppe.Adel
            : stand >= Stufe("Patrizier", 2) ? Standgruppe.Patrizier
            : stand >= Stufe("Bürger", 1) ? Standgruppe.Buerger
            : Standgruppe.Gemeiner;
    }

    /// <summary>Schlicht (Gemeiner, Bürger) oder vornehm (Patrizier, Adel).</summary>
    public Kleidung KleidungVon(Person p) => Aussehenspalette.KleidungAus(StandgruppeVon(p));

    /// <summary>
    /// Das Alter in Menschenjahren: Zwerge und Elben altern langsamer, darum zählt das Alter im Verhältnis der Sterbealter
    /// (ein Elb mit 55 Jahren sieht aus wie ein Mensch mit 21). Bei Verstorbenen das Alter beim Tod.
    /// </summary>
    public double Menschenjahre(Person p)
    {
        var mensch = _d.Voelker.TryGetValue(Reichsgilde.Core.Volk.Mensch, out var m) ? m.Sterbealter : 64;
        var eigen = _d.Voelker.TryGetValue(p.Volk, out var v) ? v.Sterbealter : mensch;
        return p.Alter(Jahr) * mensch / eigen;
    }

    /// <summary>Kind (unter 14 Menschenjahren), jung (14–29), mittel (30–54) oder alt (ab 55).</summary>
    public Altersstufe AltersstufeVon(Person p) => Aussehenspalette.AltersstufeAus(Menschenjahre(p));

    public VolkDaten VolkDaten(Volk volk) => _d.Volk(volk);

    public bool IstVolljaehrig(Person p) => p.Alter(Jahr) >= _d.Volk(p.Volk).Volljaehrig;

    /// <summary>Wie wahrscheinlich die Person dieses Jahr stirbt (Gompertz, siehe <see cref="VolkDaten"/>).</summary>
    public double Sterberisiko(Person p) => p.Lebt ? Sterberisiko(p.Volk, p.Alter(Jahr)) : 0;

    /// <summary>Sterberisiko für ein Jahr in diesem Alter.</summary>
    public double Sterberisiko(Volk volk, int alter)
    {
        var v = _d.Volk(volk);
        double H(double x) => Math.Exp((x - v.Sterbealter) / v.Streuung);
        var grund = alter < v.Volljaehrig ? _d.Kinderrisiko : _d.Grundrisiko;
        return 1 - Math.Exp(-(grund + H(alter + 1) - H(alter)));
    }

    /// <summary>Grobe Angabe statt genauer Zahl – niemand kennt sein Todesjahr.</summary>
    public string Verfassung(Person p)
    {
        if (!p.Lebt) return "verstorben";
        if (!IstVolljaehrig(p)) return "Kind";
        return Sterberisiko(p) switch
        {
            < 0.015 => "rüstig",
            < 0.05 => "bei Kräften",
            < 0.15 => "betagt",
            _ => "gebrechlich",
        };
    }

    /// <summary>Wer erben würde, wenn das Oberhaupt jetzt stürbe (null = niemand, das Haus erlischt).</summary>
    public Person? Erbe(Familie f)
    {
        // Wer als Geisel bei Dieben sitzt, kann das Haus nicht führen.
        if (f.Testament is { Lebt: true } t && t.IstKindVon(f.Oberhaupt) && IstVolljaehrig(t) && !_w.Diebe.IstGeisel(t))
            return t;
        if (f.Kinder.FirstOrDefault(k => k.Lebt && IstVolljaehrig(k) && !_w.Diebe.IstGeisel(k)) is { } aeltestes)
            return aeltestes;
        return _d.Witwenrecht && f.Ehepartner is { } witwe && !_w.Diebe.IstGeisel(witwe) ? witwe : null;
    }

    /// <summary>Chance auf ein Kind in diesem Jahr.</summary>
    public double Geburtschance(Familie f)
    {
        if (f.Ehepartner is not { } partner || _w.Diebe.IstGeisel(partner)) return 0;
        if (!Paar(f.Oberhaupt, partner, out var frau, out var mann)) return 0;
        var v = _d.Volk(frau.Volk);
        var alter = frau.Alter(Jahr);
        if (alter < v.FruchtbarVon || alter > v.FruchtbarBis) return 0;
        var kinder = f.KinderVon(frau).Count(k => k.Lebt);
        return Geburtenrate(frau, mann) * Math.Pow(_d.KinderAbnahme, kinder);
    }

    /// <summary>
    /// Wie viele Kinder ein Paar aus Oberhaupt und diesem Partner ab jetzt erwarten lässt (für Brautschau und KI).
    /// Gerechnet wird Jahr für Jahr: Geburtenrate × Chance, dass bis dahin noch beide leben.
    /// Eine junge Elbin nützt einem Menschen wenig – er erlebt ihre fruchtbaren Jahre nicht.
    /// </summary>
    public double ErwarteteKinder(Familie f, Person partner)
    {
        if (!Paar(f.Oberhaupt, partner, out var frau, out var mann)) return 0;
        var v = _d.Volk(frau.Volk);
        var rate = Geburtenrate(frau, mann);
        var kinder = (double)f.KinderVon(frau).Count(k => k.Lebt);
        var start = kinder;
        var beideLeben = 1.0;
        var alterMann = mann.Alter(Jahr);
        for (var alter = frau.Alter(Jahr); alter <= v.FruchtbarBis && beideLeben > 0.01; alter++, alterMann++)
        {
            if (alter >= v.FruchtbarVon)
                kinder += beideLeben * rate * Math.Pow(_d.KinderAbnahme, kinder);
            beideLeben *= (1 - Sterberisiko(frau.Volk, alter)) * (1 - Sterberisiko(mann.Volk, alterMann));
        }
        return kinder - start;
    }

    /// <summary>
    /// Was die Familie jedes Jahr kostet: Haushalt für Oberhaupt und Ehepartner, Unterhalt für alle anderen – mal dem Faktor aus der
    /// Meinung des Ehepartners über das Oberhaupt (PR #231: wer zufrieden ist, wirtschaftet sparsamer).
    /// </summary>
    public double Haushaltskosten(Familie f) => f.Lebende.Sum(p =>
        (p == f.Oberhaupt || p == f.Ehepartner ? _d.Volk(p.Volk).Haushalt : _d.Volk(p.Volk).Unterhalt) * HaushaltFaktor(p))
        * _w.Meinungen.HaushaltFaktor(f);

    public double Zuneigung(Konto konto, Kandidat k) => _zuneigung.GetValueOrDefault((konto, k.Person.Nummer));

    /// <summary>So viel Zuneigung lässt sich dieses Jahr noch gewinnen.</summary>
    public double ZuneigungRest(Konto konto, Kandidat k) =>
        Math.Max(0, _d.ZuneigungJeJahr - _zuneigungDiesesJahr.GetValueOrDefault((konto, k.Person.Nummer)));

    /// <summary>Werben noch andere Häuser um diesen Kandidaten? (In der Stadt spricht sich so etwas herum.)</summary>
    public IEnumerable<Konto> Mitbewerber(Konto konto, Kandidat k) =>
        _w.Konten.Where(x => x != konto && Zuneigung(x, k) > 0.5);

    /// <summary>Zuneigung + Ansehen (+ Meinung), ab der der Kandidat einwilligt.</summary>
    public double Heiratsschwelle(Kandidat k) => _d.Heiratsschwelle + k.Stand * _d.HeiratsschwelleJeStand;

    /// <summary>Zuneigung + Ansehen des Hauses beim Kandidaten + seine Meinung über das Oberhaupt (PR #231, halb gezählt).</summary>
    public double Bereitschaft(Konto konto, Kandidat k) => Zuneigung(konto, k) + _w.Rat.Ansehen(konto) + _w.Meinungen.HeiratBereitschaft(konto, k);

    public double Hochzeitskosten(Kandidat k) => _d.Volk(k.Person.Volk).Hochzeit * (1 + _d.HochzeitJeStand * k.Stand);

    /// <summary>Gulden für einen Punkt Zuneigung, wenn dieses Haus wirbt (das Volk des Oberhaupts wirbt verschieden gut).</summary>
    public double GuldenJeZuneigung(Konto konto) =>
        Von(konto) is { } f
            ? 1 / (_d.ZuneigungJeGulden * _d.Volk(f.Oberhaupt.Volk).Werbefaktor * (1 + Talent(konto, Dynastie.Talent.Rhetorik) * W.RhetorikWerben)
                   * _w.Wohnen.WerbeFaktor(konto)) // Stube im Wohnhaus (PR #224)
            : 1 / _d.ZuneigungJeGulden;

    /// <summary>Gulden für einen Punkt Zuneigung bei diesem Kandidaten: Wer ihm gefällt, wirbt billiger (Meinung, PR #231).</summary>
    public double GuldenJeZuneigung(Konto konto, Kandidat k) => GuldenJeZuneigung(konto) * _w.Meinungen.HeiratKostenFaktor(konto, k);

    /// <summary>Darf dieses Haus um den Kandidaten werben? (null = ja, sonst der Grund)</summary>
    public string? WerbenVerboten(Konto konto, Kandidat k)
    {
        if (Von(konto) is not { } f) return $"{konto.Inhaber} hat keine Familie.";
        if (f.Ehepartner is { } e) return $"{f.Oberhaupt.Name} ist verheiratet mit {e.Name}.";
        if (k.Person.Geschlecht == f.Oberhaupt.Geschlecht)
            return $"{k.Person.Name} sucht {(k.Person.Geschlecht == Geschlecht.Frau ? "einen Ehemann" : "eine Ehefrau")}.";
        if (k.Stand > konto.Stand)
            return $"{k.Person.Name} stammt aus dem Stand {_w.Daten.Staende[k.Stand].Name} – erst ab diesem Stand ist die Partie standesgemäß.";
        return null;
    }

    /// <summary>
    /// Volksnähe für die Wahl im Rat: Ratsherren schätzen ihr eigenes Volk. Eine Ehe zwischen Völkern
    /// kostet Ansehen beim Volk des Oberhaupts und bringt etwas bei dem des Ehepartners.
    /// </summary>
    public double Volksnaehe(Volk ratsherrVolk, Konto konto)
    {
        if (Von(konto) is not { } f) return 0;
        var wert = f.Oberhaupt.Volk == ratsherrVolk ? _w.Daten.Politik.Gunst.Volksnaehe : 0;
        if (f.Ehepartner is { } e && e.Volk != f.Oberhaupt.Volk)
        {
            if (ratsherrVolk == f.Oberhaupt.Volk) wert += _d.Mischehe.AnsehenEigenesVolk;
            if (ratsherrVolk == e.Volk) wert += _d.Mischehe.AnsehenPartnervolk;
        }
        return wert;
    }

    private static bool Paar(Person a, Person b, out Person frau, out Person mann)
    {
        frau = a.Geschlecht == Geschlecht.Frau ? a : b;
        mann = a.Geschlecht == Geschlecht.Frau ? b : a;
        return a.Geschlecht != b.Geschlecht;
    }

    private double Geburtenrate(Person frau, Person mann)
    {
        var rate = _d.Volk(frau.Volk).Geburtenrate;
        return mann.Volk == frau.Volk ? rate : Math.Min(rate, _d.Volk(mann.Volk).Geburtenrate) * _d.Mischehe.Fruchtbarkeit;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Gründung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gründet die Familie eines Hauses mit einem ledigen Oberhaupt im Startalter seines Volkes.
    /// </summary>
    /// <param name="vorname">Ohne Angabe ein zufälliger Name des Volkes.</param>
    /// <param name="glaube">Ohne Angabe der Startglaube des Volkes.</param>
    /// <param name="aussehen">Ohne Angabe gewürfelt (<see cref="Aussehenspalette.Verboten"/> sagt, was passt).</param>
    public Familie Gruenden(Konto konto, Volk volk, Geschlecht geschlecht, string? vorname = null, int? alter = null, Konfession? glaube = null,
        Aussehen? aussehen = null)
    {
        if (aussehen is { } a && Aussehenspalette.Verboten(volk, a) is { } unpassend)
            throw new ArgumentException(unpassend, nameof(aussehen));
        if (glaube is { } g && _w.Glaube.IstAktiv && _w.Glaube.StartglaubeVerboten(volk, g) is { } grund)
            throw new ArgumentException(grund, nameof(glaube));
        if (!IstAktiv) throw new InvalidOperationException("Die Wirtschaftsdaten enthalten keine Dynastie-Daten.");
        if (_familien.ContainsKey(konto)) throw new InvalidOperationException($"{konto.Inhaber} hat schon eine Familie.");
        if (!_w.Konten.Contains(konto)) throw new ArgumentException("Das Konto gehört nicht zu dieser Stadt.", nameof(konto));
        var v = _d.Volk(volk); // wirft bei Orks und Ogern, die keine Daten haben

        Jahr = konto.Laufend.Jahr;
        var nachname = Familie.NachnameAus(konto.Inhaber);
        vorname = string.IsNullOrWhiteSpace(vorname) ? Vorname(v, geschlecht) : vorname.Trim();
        var oberhaupt = new Person(_naechsteNummer++, vorname, nachname, volk, geschlecht, Jahr - (alter ?? v.Startalter));
        TalenteWuerfeln(oberhaupt);
        // Immer würfeln, auch wenn der Spieler wählt – so sehen die KI-Familien danach gleich aus.
        oberhaupt.Aussehen = Aussehenspalette.Wuerfeln(_aussehenZufall, volk);
        if (aussehen is { } gewaehlt) oberhaupt.Aussehen = gewaehlt;
        oberhaupt.GlaubeSetzen(glaube ?? _w.Glaube.Startglaube(volk));
        ZuegeFuerErwachsenen(oberhaupt); // eigener Würfel je Person – ändert keinen anderen Wurf
        var familie = new Familie(konto, volk, nachname, oberhaupt);
        _familien[konto] = familie;
        KandidatenAuffuellen();
        return familie;
    }

    private string Vorname(VolkDaten v, Geschlecht g)
    {
        var liste = g == Geschlecht.Frau ? v.VornamenFrau : v.VornamenMann;
        return liste[_zufall.Next(liste.Count)];
    }

    private void KandidatenAuffuellen()
    {
        while (_kandidaten.Count < _d.Kandidaten)
            _kandidaten.Add(NeuerKandidat());
    }

    private Kandidat NeuerKandidat()
    {
        var voelker = _d.Voelker.OrderBy(x => x.Key).ToList();
        var volk = Gewichtet(voelker.Select(x => (x.Key, x.Value.Kandidatenanteil)).ToList());
        var v = _d.Volk(volk);

        // Beide Geschlechter etwa gleich oft – sonst fände ein Oberhaupt womöglich niemanden.
        var frauen = _kandidaten.Count(k => k.Person.Geschlecht == Geschlecht.Frau);
        var maenner = _kandidaten.Count - frauen;
        var geschlecht = frauen == maenner
            ? (_zufall.Next(2) == 0 ? Geschlecht.Frau : Geschlecht.Mann)
            : frauen < maenner ? Geschlecht.Frau : Geschlecht.Mann;

        var staende = _d.KandidatenStaende.Select(x => (Stufe: _w.Daten.StandStufe(x.Key), Gewicht: x.Value))
            .Where(x => x.Stufe >= 0).OrderBy(x => x.Stufe).ToList();
        var stand = Gewichtet(staende);

        var spanne = Math.Max(1, (v.FruchtbarBis - v.Volljaehrig) * 2 / 3);
        var alter = v.Volljaehrig + _zufall.Next(spanne);
        // Zwei lebende Personen mit gleichem Namen verwirren nur – höchstens zehn Versuche, dann eben doch.
        string vorname, name;
        var versuche = 0;
        do
        {
            vorname = Vorname(v, geschlecht);
            name = v.Familiennamen[_zufall.Next(v.Familiennamen.Count)];
        } while (NameVergeben(vorname, name) && ++versuche < 10);
        var person = new Person(_naechsteNummer++, vorname, name, volk, geschlecht, Jahr - alter);
        TalenteWuerfeln(person);
        person.Aussehen = Aussehenspalette.Wuerfeln(_aussehenZufall, volk); // Ehepartner von außen: gewürfelt
        person.GlaubeSetzen(_w.Glaube.Wuerfeln(volk)); // eigener Würfel – Namen und Talente bleiben wie ohne Glauben
        ZuegeFuerErwachsenen(person);
        return new Kandidat(person, stand);
    }

    /// <summary>Unter den Kandidaten kein Vorname doppelt, in den Familien kein ganzer Name.</summary>
    private bool NameVergeben(string vorname, string nachname) =>
        _kandidaten.Any(k => k.Person.Vorname == vorname)
        || _familien.Values.Any(f => f.Lebende.Any(p => p.Vorname == vorname && p.Familienname == nachname));

    private T Gewichtet<T>(List<(T Wert, double Gewicht)> liste)
    {
        var summe = liste.Sum(x => Math.Max(0, x.Gewicht));
        var wurf = _zufall.NextDouble() * summe;
        foreach (var (wert, gewicht) in liste)
        {
            wurf -= Math.Max(0, gewicht);
            if (wurf < 0) return wert;
        }
        return liste[^1].Wert;
    }

    /// <summary>Entfernt Zuneigung aus beiden Büchern (gesamt und dieses Jahr) – sie müssen zusammenpassen.</summary>
    private void ZuneigungEntfernen(Func<(Konto, int), bool> bedingung)
    {
        foreach (var schluessel in _zuneigung.Keys.Concat(_zuneigungDiesesJahr.Keys).Where(bedingung).Distinct().ToList())
        {
            _zuneigung.Remove(schluessel);
            _zuneigungDiesesJahr.Remove(schluessel);
        }
    }

    internal void Ereignis(Konto konto, string text)
    {
        _laufend.Add(new FamilienEreignis(konto, text));
        _w.Melden(text);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Befehle (aufgerufen über Stadtwirtschaft.Ausfuehren)
    // ═════════════════════════════════════════════════════════════════════════

    internal Befehlsergebnis Werben(Konto konto, int nummer, double betrag)
    {
        if (KandidatNummer(nummer) is not { } k)
            return Befehlsergebnis.Abgelehnt("Diesen Heiratskandidaten gibt es nicht (mehr).");
        if (WerbenVerboten(konto, k) is { } grund)
            return Befehlsergebnis.Abgelehnt(grund);
        if (betrag <= 0 || double.IsNaN(betrag))
            return Befehlsergebnis.Abgelehnt("Ein Geschenk muss etwas wert sein.");
        var rest = ZuneigungRest(konto, k);
        if (rest <= 0.001)
            return Befehlsergebnis.Abgelehnt($"Mehr Geschenke helfen dieses Jahr nicht – {k.Person.Vorname} braucht Zeit. Im nächsten Jahr geht es weiter.");

        // Nur so viel bezahlen, wie dieses Jahr noch wirkt.
        var jeZuneigung = GuldenJeZuneigung(konto, k);
        var wirkung = Math.Min(betrag / jeZuneigung, rest);
        var kosten = wirkung * jeZuneigung;
        if (konto.Vermoegen < kosten)
            return Befehlsergebnis.Abgelehnt($"So viel Geld hat {konto.Inhaber} nicht.");

        konto.Familienkosten(kosten);
        var schluessel = (konto, nummer);
        _zuneigung[schluessel] = Zuneigung(konto, k) + wirkung;
        _zuneigungDiesesJahr[schluessel] = _zuneigungDiesesJahr.GetValueOrDefault(schluessel) + wirkung;
        var bereit = Bereitschaft(konto, k);
        var schwelle = Heiratsschwelle(k);
        return Befehlsergebnis.Ok($"{k.Person.Name} freut sich über das Geschenk ({Zahlenformat.Ganz(kosten)} {_w.Daten.Allgemein.Waehrung}). " +
                                  (bereit >= schwelle
                                      ? "Einer Heirat steht nichts mehr im Weg!"
                                      : $"Zuneigung und Ansehen: {Zahlenformat.Kommazahl(bereit)} von {Zahlenformat.Ganz(schwelle)}."));
    }

    /// <summary>
    /// Ein besonderes Geschenk (Moschusduft): Zuneigung, die nicht auf das Jahreslimit zählt.
    /// Prüft nur, ob geworben werden darf – woher das Geschenk kommt, regelt der Aufrufer.
    /// </summary>
    internal string? ZuneigungSchenken(Konto konto, int nummer, double wert)
    {
        if (KandidatNummer(nummer) is not { } k) return "Diesen Heiratskandidaten gibt es nicht (mehr).";
        if (WerbenVerboten(konto, k) is { } grund) return grund;
        var schluessel = (konto, nummer);
        _zuneigung[schluessel] = Zuneigung(konto, k) + wert;
        return null;
    }

    internal Befehlsergebnis Heiraten(Konto konto, int nummer)
    {
        if (KandidatNummer(nummer) is not { } k)
            return Befehlsergebnis.Abgelehnt("Diesen Heiratskandidaten gibt es nicht (mehr).");
        if (WerbenVerboten(konto, k) is { } grund)
            return Befehlsergebnis.Abgelehnt(grund);
        var bereit = Bereitschaft(konto, k);
        var schwelle = Heiratsschwelle(k);
        if (bereit < schwelle - 1e-9)
            return Befehlsergebnis.Abgelehnt($"{k.Person.Name} zögert noch: Zuneigung und Ansehen {Zahlenformat.Kommazahl(bereit)}, nötig sind {Zahlenformat.Ganz(schwelle)}.");
        var kosten = Hochzeitskosten(k);
        if (konto.Vermoegen < kosten)
            return Befehlsergebnis.Abgelehnt($"Die Hochzeit kostet {Zahlenformat.Ganz(kosten)} {_w.Daten.Allgemein.Waehrung} – so viel hat {konto.Inhaber} nicht.");

        var f = _familien[konto];
        konto.Familienkosten(kosten);
        _kandidaten.Remove(k);
        ZuneigungEntfernen(s => s.Item2 == nummer || s.Item1 == konto);

        var p = k.Person;
        p.Familienname = f.Nachname;
        var uebertritt = _w.Glaube.IstAktiv && p.Glaube != f.Oberhaupt.Glaube;
        if (uebertritt) p.Glaube = f.Oberhaupt.Glaube; // Mischehe: Der Partner tritt zum Glauben des Hauses über
        f.MitgliederListe.Add(p);
        f.Ehepartner = p;
        Ereignis(konto, $"Hochzeit im Haus {f.Nachname}: {f.Oberhaupt.Name} heiratet {p.Vorname}, geb. {p.Geburtsname} " +
                        $"({p.Volk.Einzahl()}, {p.Alter(Jahr)} Jahre). Das Fest kostet {Zahlenformat.Ganz(kosten)} {_w.Daten.Allgemein.Waehrung}." +
                        (uebertritt ? $" {p.Vorname} tritt zum {p.Glaube.Glaube()} über – die {p.Herkunftsglaube.Gemeinde()} im Rat sehen es ungern." : ""));
        return Befehlsergebnis.Ok($"{f.Oberhaupt.Vorname} und {p.Vorname} sind verheiratet.");
    }

    internal Befehlsergebnis ErbenBestimmen(Konto konto, int nummer)
    {
        if (Von(konto) is not { } f)
            return Befehlsergebnis.Abgelehnt($"{konto.Inhaber} hat keine Familie.");
        var erbe = f.Kinder.FirstOrDefault(p => p.Nummer == nummer);
        if (erbe is null)
            return Befehlsergebnis.Abgelehnt("Erben kann nur ein leibliches Kind des Oberhaupts.");
        if (!erbe.Lebt)
            return Befehlsergebnis.Abgelehnt($"{erbe.Vorname} ist verstorben.");
        f.Testament = erbe;
        return Befehlsergebnis.Ok(IstVolljaehrig(erbe)
            ? $"Testament: {erbe.Vorname} soll das Haus erben."
            : $"Testament: {erbe.Vorname} soll das Haus erben – sobald {(erbe.Geschlecht == Geschlecht.Frau ? "sie" : "er")} volljährig ist. Bis dahin erbt das älteste volljährige Kind.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jahresende
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Haushalt und Unterhalt – vor dem Jahresabschluss, damit sie im abgelaufenen Jahr stehen.</summary>
    internal void UnterhaltZahlen()
    {
        foreach (var konto in _w.Konten.Where(k => k.IstAktiv))
            if (Von(konto) is { } f)
                konto.Familienkosten(Haushaltskosten(f) * _w.Wohnen.HaushaltFaktor(konto) + Ausbildungskosten(f)); // Vorratskammer (PR #224)
    }

    /// <summary>Was die laufenden Ausbildungen dieses Jahr kosten (nur Kinder im Ausbildungsalter).</summary>
    public double Ausbildungskosten(Familie f) => !HatTalente ? 0 : f.Lebende
        .Where(p => p.Ausbildung is { } weg && IstInAusbildungsalter(p) && _d.Talente.Ausbildungen.ContainsKey(weg))
        .Sum(p => _d.Talente.Ausbildungen[p.Ausbildung!].Kosten);

    /// <summary>Ein Jahr Ausbildung: Zuwachs in den Talenten; wer volljährig wird, schließt ab.</summary>
    private void Ausbilden(Familie f)
    {
        if (!HatTalente) return;
        foreach (var p in f.Lebende.Where(p => p.Ausbildung is not null).ToList())
        {
            if (!_d.Talente.Ausbildungen.TryGetValue(p.Ausbildung!, out var a)) { p.Ausbildung = null; continue; }
            if (IstInAusbildungsalter(p))
            {
                var faktor = Ausbildungsfaktor(p.Volk) * _w.Wohnen.AusbildungFaktor(f.Konto); // Bibliothek im Wohnhaus (PR #224)
                foreach (var (t, zuwachs) in a.Talente)
                    p.TalentSetzen(t, Math.Min(_d.Talente.Hoechstwert, p.Anlage(t) + zuwachs * faktor));
            }
            // Wird das Kind im neuen Jahr volljährig, ist die Ausbildung abgeschlossen.
            if (p.Alter(Jahr + 1) >= _d.Volk(p.Volk).Volljaehrig)
            {
                p.Abschluss = p.Ausbildung;
                p.Ausbildung = null;
                Ereignis(f.Konto, $"{p.Vorname} {f.Nachname} hat die Ausbildung abgeschlossen ({p.Abschluss}) und ist nun volljährig. " +
                                  $"Stärken: {string.Join(", ", p.Staerken.Take(2).Select(x => $"{x.Talent.Name()} {TalentTexte.Sterne(x.Wert)}"))}.");
                ZugAnlass(f.Konto, p, $"abschluss:{p.Abschluss}"); // Universität → Gelehrt
            }
        }
    }

    internal Befehlsergebnis AusbildungWaehlen(Konto konto, int nummer, string? weg)
    {
        if (!HatTalente) return Befehlsergebnis.Abgelehnt("In dieser Stadt gibt es keine Ausbildungen.");
        if (Von(konto) is not { } f) return Befehlsergebnis.Abgelehnt($"{konto.Inhaber} hat keine Familie.");
        var kind = f.Lebende.FirstOrDefault(p => p.Nummer == nummer);
        if (kind is null) return Befehlsergebnis.Abgelehnt("Dieses Kind gibt es im Haus nicht.");
        if (IstVolljaehrig(kind)) return Befehlsergebnis.Abgelehnt($"{kind.Vorname} ist schon volljährig – die Ausbildung ist vorbei.");
        if (weg is null)
        {
            kind.Ausbildung = null;
            return Befehlsergebnis.Ok($"{kind.Vorname} wird nicht mehr ausgebildet.");
        }
        if (AusbildungVerboten(konto, weg) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        kind.Ausbildung = weg;
        var ab = AusbildungAb(kind.Volk);
        var a = _d.Talente.Ausbildungen[weg];
        return Befehlsergebnis.Ok($"{kind.Vorname}: {weg} ({Zahlenformat.Ganz(a.Kosten)} {_w.Daten.Allgemein.Waehrung} im Jahr)" +
                                  (kind.Alter(Jahr) < ab ? $" – beginnt mit {ab} Jahren." : "."));
    }

    /// <summary>Warum das Oberhaupt dieses Talent jetzt nicht üben kann – oder null.</summary>
    public string? TrainingVerboten(Konto konto, Talent talent)
    {
        if (!HatTalente) return "In dieser Stadt gibt es keine Talente.";
        if (Von(konto) is not { } f) return $"{konto.Inhaber} hat keine Familie.";
        var o = f.Oberhaupt;
        if (o.Anlage(talent) >= _d.Talente.Hoechstwert - 1e-9)
            return $"{o.Vorname} beherrscht {talent.Name()} schon meisterhaft ({TalentTexte.Sterne(o.Anlage(talent))} Sterne).";
        if (o.GeuebtIm(talent) == Jahr)
            return $"{o.Vorname} hat {talent.Name()} dieses Jahr schon geübt.";
        return null;
    }

    /// <summary>Das Oberhaupt übt ein Talent (die Aktionspunkte bucht die Stadtwirtschaft ab).</summary>
    internal Befehlsergebnis TalentTrainieren(Konto konto, Talent talent)
    {
        if (TrainingVerboten(konto, talent) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var o = Von(konto)!.Oberhaupt;
        var schritt = _w.Daten.Aktionspunkte?.TrainingSchritt ?? 0.5;
        o.TalentSetzen(talent, Math.Min(_d.Talente.Hoechstwert, o.Anlage(talent) + schritt));
        o.GeuebtSetzen(talent, Jahr);
        return Befehlsergebnis.Ok($"{o.Vorname} übt {talent.Name()}: jetzt {TalentTexte.Sterne(o.Anlage(talent))} Sterne.");
    }

    /// <summary>Todesfälle, Erbfälle, Geburten und die Brautschau des neuen Jahres.</summary>
    internal void Jahreswechsel()
    {
        if (!IstAktiv) return;
        foreach (var konto in _w.Konten.Where(k => k.IstAktiv))
        {
            if (Von(konto) is not { } f) continue;

            foreach (var p in f.Lebende.ToList())
                if (_zufall.NextDouble() < Sterberisiko(p))
                    Sterben(f, p);
            if (!f.Oberhaupt.Lebt)
                Nachfolge(f);
            if (!konto.IstAktiv) continue;

            if (_zufall.NextDouble() < Geburtschance(f))
                Geburt(f);
            Ausbilden(f);
            ZuegeImJahreswechsel(f); // Persönlichkeit der neu Volljährigen, erworbene Züge (Familienbuch.Zuege.cs)
        }

        // Die Brautschau: Manche heiraten anderweitig, Neue kommen dazu.
        foreach (var k in _kandidaten.ToList())
        {
            if (_zufall.NextDouble() >= _d.KandidatGehtJeJahr) continue;
            _kandidaten.Remove(k);
            foreach (var konto in _w.Konten.Where(x => Zuneigung(x, k) > 0.5))
                Ereignis(konto, $"{k.Person.Name}, um {(k.Person.Geschlecht == Geschlecht.Frau ? "die" : "den")} {konto.Inhaber} geworben hat, hat anderweitig geheiratet.");
            ZuneigungEntfernen(s => s.Item2 == k.Person.Nummer);
        }
        Jahr++;
        KandidatenAuffuellen();

        var bleibt = 1 - _d.ZuneigungVerfall;
        foreach (var schluessel in _zuneigung.Keys.ToList())
            _zuneigung[schluessel] *= bleibt;
        _zuneigungDiesesJahr.Clear();

        _letztesJahr = _laufend;
        _laufend = new List<FamilienEreignis>();
    }

    private void Sterben(Familie f, Person p)
    {
        p.Todesjahr = Jahr;
        p.Krankheit = null;
        var alter = p.Alter(Jahr);
        var woran = p.Todesursache is { } u ? $" {u}" : "";
        if (p == f.Ehepartner)
        {
            f.Ehepartner = null;
            Ereignis(f.Konto, $"Trauer im Haus {f.Nachname}: {p.Vorname}, {p.Ehepartnertitel} von {f.Oberhaupt.Vorname}, ist mit {alter} Jahren{woran} gestorben.");
        }
        else if (p != f.Oberhaupt)
            Ereignis(f.Konto, $"Trauer im Haus {f.Nachname}: {p.Vorname} ist mit {alter} Jahren{woran} gestorben.");
        if (f.Testament == p) f.Testament = null;
    }

    /// <summary>Eine Person stirbt an einer Krankheit (vom Gesundheitswesen) – mit Erbfolge, wenn es das Oberhaupt traf.</summary>
    internal void AnKrankheitSterben(Person p, string ursache)
    {
        if (!p.Lebt) return;
        p.Todesursache = ursache;
        var kandidat = _kandidaten.FirstOrDefault(k => k.Person == p);
        if (kandidat is not null)
        {
            p.Todesjahr = Jahr;
            p.Krankheit = null;
            _kandidaten.Remove(kandidat);
            foreach (var konto in _w.Konten.Where(x => Zuneigung(x, kandidat) > 0.5))
                Ereignis(konto, $"{p.Name}, um {(p.Geschlecht == Geschlecht.Frau ? "die" : "den")} {konto.Inhaber} geworben hat, ist {ursache} gestorben.");
            ZuneigungEntfernen(s => s.Item2 == p.Nummer);
            return;
        }
        if (_familien.Values.FirstOrDefault(f => f.Konto.IstAktiv && f.Lebende.Contains(p)) is not { } familie) return;
        Sterben(familie, p);
        if (!familie.Oberhaupt.Lebt)
            Nachfolge(familie);
    }

    /// <summary>
    /// Eine Geisel, für die kein Lösegeld kam, wird in die Fremde verkauft: Sie gehört nicht mehr zum Haus
    /// (zählt wie verstorben – kein Erbe, kein Unterhalt). Das Oberhaupt selbst wird nie entführt.
    /// </summary>
    internal void Verschwinden(Konto konto, Person p, string text)
    {
        if (Von(konto) is not { } f || p == f.Oberhaupt || !p.Lebt) return;
        p.Todesjahr = Jahr;
        p.Todesursache = "in die Fremde verschleppt";
        p.Krankheit = null;
        if (p == f.Ehepartner) f.Ehepartner = null;
        if (f.Testament == p) f.Testament = null;
        Ereignis(f.Konto, text);
    }

    /// <summary>Das Oberhaupt ist tot: Testament, ältestes volljähriges Kind, Witwenrecht – oder das Haus erlischt.</summary>
    private void Nachfolge(Familie f)
    {
        var tot = f.Oberhaupt;
        f.AhnenListe.Add(tot);
        var erbe = Erbe(f);
        var waisen = f.Kinder.Count(k => k.Lebt);

        if (erbe is null)
        {
            Ereignis(f.Konto, $"{tot.OberhauptTitel} {tot.Name} ist mit {tot.Alter(Jahr)} Jahren{(tot.Todesursache is { } u1 ? " " + u1 : "")} gestorben – ohne Erben. " +
                              $"Das Haus {f.Nachname} ist erloschen{(waisen > 0 ? $"; {waisen} unmündige Kinder bleiben zurück" : "")}.");
            f.Konto.IstErloschen = true;
            _w.Stilllegen(f.Konto);
            Erloschen?.Invoke(f.Konto);
            return;
        }

        var wieErbe = erbe == f.Ehepartner ? $"Nach dem Witwenrecht führt {erbe.Vorname} das Haus weiter"
            : erbe == f.Testament ? $"Wie im Testament bestimmt, erbt {erbe.SohnOderTochter} {erbe.Vorname}"
            : $"{erbe.SohnOderTochter} {erbe.Vorname} erbt";
        f.Oberhaupt = erbe;
        f.Ehepartner = null; // der Witwer/die Witwe bleibt im Haus, ist aber nicht Ehepartner des neuen Oberhaupts
        f.Testament = null;
        ZuneigungEntfernen(s => s.Item1 == f.Konto);
        _w.Rat.Neuanfang(f.Konto);
        Ereignis(f.Konto, $"{tot.OberhauptTitel} {tot.Name} ist mit {tot.Alter(Jahr)} Jahren{(tot.Todesursache is { } u2 ? " " + u2 : "")} gestorben. {wieErbe} ({erbe.Alter(Jahr)} Jahre). " +
                          "Ämter und Gunst im Rat beginnen von vorn.");
        Erbfall?.Invoke(f, tot, erbe);
    }

    /// <summary>Für Tests und Szenarien: ein Kind des Oberhaupts (etwa aus einer früheren Ehe) mit diesem Alter.</summary>
    internal Person KindAufnehmen(Konto konto, int alter, Geschlecht geschlecht = Geschlecht.Frau)
    {
        var f = _familien[konto];
        var o = f.Oberhaupt;
        var kind = new Person(_naechsteNummer++, Vorname(_d.Volk(f.Volk), geschlecht), f.Nachname, f.Volk, geschlecht, Jahr - alter,
            o.Geschlecht == Geschlecht.Mann ? o.Nummer : null, o.Geschlecht == Geschlecht.Frau ? o.Nummer : null);
        kind.Aussehen = Aussehenspalette.Vererben(_aussehenZufall, kind.Volk, geschlecht,
            o.Geschlecht == Geschlecht.Mann ? o : null, o.Geschlecht == Geschlecht.Frau ? o : null);
        kind.GlaubeSetzen(o.Glaube);
        ZuegeBeiGeburt(kind);
        if (IstVolljaehrig(kind)) ZuegeAlsErwachsener(kind);
        f.MitgliederListe.Add(kind);
        return kind;
    }

    private void Geburt(Familie f)
    {
        Paar(f.Oberhaupt, f.Ehepartner!, out var mutter, out var vater);
        var geschlecht = _zufall.Next(2) == 0 ? Geschlecht.Frau : Geschlecht.Mann;
        var v = _d.Volk(f.Volk);
        var kind = new Person(_naechsteNummer++, Vorname(v, geschlecht), f.Nachname, f.Volk, geschlecht, Jahr + 1, vater.Nummer, mutter.Nummer);
        TalenteVererben(kind, mutter, vater);
        kind.Aussehen = Aussehenspalette.Vererben(_aussehenZufall, kind.Volk, geschlecht, vater, mutter);
        kind.GlaubeSetzen(f.Oberhaupt.Glaube); // getauft im Glauben des Hauses
        ZuegeBeiGeburt(kind, vater, mutter);
        f.MitgliederListe.Add(kind);
        Ereignis(f.Konto, $"Freude im Haus {f.Nachname}: {mutter.Vorname} und {vater.Vorname} bekommen {(geschlecht == Geschlecht.Frau ? "eine Tochter" : "einen Sohn")}, {kind.Vorname}.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal FamilienbuchStand Sichern(Func<Konto, int> kontoNummer)
    {
        var personen = _familien.Values.SelectMany(f => f.Mitglieder).Concat(_kandidaten.Select(k => k.Person))
            .OrderBy(p => p.Nummer).Select(p => new PersonStand
            {
                Nummer = p.Nummer, Vorname = p.Vorname, Familienname = p.Familienname, Geburtsname = p.Geburtsname,
                Volk = p.Volk, Geschlecht = p.Geschlecht, Geburtsjahr = p.Geburtsjahr, Todesjahr = p.Todesjahr,
                Vater = p.Vater, Mutter = p.Mutter,
                Talente = HatTalente ? TalentTexte.Alle.Select(p.Anlage).ToList() : null,
                Krankheit = p.Krankheit is { } k ? new ErkrankungStand { Name = k.Name, Bis = k.Bis, Medicus = k.Medicus, Schnaps = k.Schnaps } : null,
                Todesursache = p.Todesursache,
                Glaube = p.Glaube, Herkunftsglaube = p.Herkunftsglaube,
                Ausbildung = p.Ausbildung, Abschluss = p.Abschluss,
                Geuebt = TalentTexte.Alle.Any(t => p.GeuebtIm(t) != 0) ? TalentTexte.Alle.Select(p.GeuebtIm).ToList() : null,
                Gesicht = p.Aussehen.Gesicht, Haar = p.Aussehen.Haar, Augen = p.Aussehen.Augen,
                Zuege = HatZuege ? p.Zuege.Select(z => z.Kennung).ToList() : null,
            }).ToList();
        return new FamilienbuchStand
        {
            Jahr = Jahr,
            ZufallWuerfe = _zufall.Wuerfe,
            TalentWuerfe = _talentZufall.Wuerfe,
            AussehenWuerfe = _aussehenZufall.Wuerfe,
            ZugWuerfe = _zugZufall.Wuerfe,
            NaechsteNummer = _naechsteNummer,
            Personen = personen,
            Familien = _w.Konten.Where(_familien.ContainsKey).Select(k => _familien[k]).Select(f => new FamilieStand
            {
                Konto = kontoNummer(f.Konto), Volk = f.Volk, Nachname = f.Nachname,
                Oberhaupt = f.Oberhaupt.Nummer, Ehepartner = f.Ehepartner?.Nummer, Testament = f.Testament?.Nummer,
                Mitglieder = f.Mitglieder.Select(p => p.Nummer).ToList(),
                Ahnen = f.Ahnen.Select(p => p.Nummer).ToList(),
            }).ToList(),
            Kandidaten = _kandidaten.Select(k => new KandidatStand { Person = k.Person.Nummer, Stand = k.Stand }).ToList(),
            // Feste Reihenfolge: Ein Dictionary zählt nach Löschungen nicht mehr in Einfügereihenfolge auf.
            Zuneigung = _zuneigung.OrderBy(z => kontoNummer(z.Key.Item1)).ThenBy(z => z.Key.Item2).Select(z => new ZuneigungStand
            {
                Konto = kontoNummer(z.Key.Item1), Kandidat = z.Key.Item2, Wert = z.Value,
                DiesesJahr = _zuneigungDiesesJahr.GetValueOrDefault(z.Key),
            }).ToList(),
            DiesesJahr = _laufend.Select(e => new EreignisStand { Konto = kontoNummer(e.Konto), Text = e.Text }).ToList(),
            LetztesJahr = _letztesJahr.Select(e => new EreignisStand { Konto = kontoNummer(e.Konto), Text = e.Text }).ToList(),
        };
    }

    internal void Wiederherstellen(FamilienbuchStand s, IReadOnlyList<Konto> konten)
    {
        Jahr = s.Jahr;
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        _naechsteNummer = s.NaechsteNummer;
        _talentZufall = new Zufallsquelle(_talentZufall.Startwert, s.TalentWuerfe);
        _aussehenZufall = new Zufallsquelle(_aussehenZufall.Startwert, s.AussehenWuerfe);
        _zugZufall = new Zufallsquelle(_zugZufall.Startwert, s.ZugWuerfe);
        var personen = s.Personen.ToDictionary(p => p.Nummer, p =>
        {
            var person = new Person(p.Nummer, p.Vorname, p.Familienname, p.Volk, p.Geschlecht, p.Geburtsjahr, p.Vater, p.Mutter, p.Geburtsname, p.Todesjahr)
            {
                Ausbildung = p.Ausbildung, Abschluss = p.Abschluss, Todesursache = p.Todesursache,
                Krankheit = p.Krankheit is { } k && _w.Daten.Gesundheit is { } g ? Gesundheit.Erkrankung.Aus(g, k.Name, k.Bis) with { Medicus = k.Medicus, Schnaps = k.Schnaps } : null,
            };
            person.Glaube = p.Glaube ?? _w.Glaube.Startglaube(p.Volk); // alte Spielstände ohne Glauben: Startglaube des Volkes
            person.Herkunftsglaube = p.Herkunftsglaube ?? person.Glaube;
            foreach (var t in TalentTexte.Alle) // alte Spielstände ohne Talente: Grundwerte des Volkes
                person.TalentSetzen(t, p.Talente is { } werte && (int)t < werte.Count ? werte[(int)t] : HatTalente ? _d.Talente.Grundwert(p.Volk, t) : 0);
            // Alte Spielstände ohne Aussehen: das feste Aussehen aus Nummer und Volk, das der Konstruktor schon gesetzt hat.
            if (p.Gesicht is { } gesicht && p.Haar is { } haar && p.Augen is { } augen)
                person.Aussehen = new Aussehen(gesicht, haar, augen);
            if (p.Geuebt is { } geuebt)
                foreach (var t in TalentTexte.Alle.Where(t => (int)t < geuebt.Count))
                    person.GeuebtSetzen(t, geuebt[(int)t]);
            return person;
        });

        _familien.Clear();
        foreach (var fs in s.Familien)
        {
            var f = new Familie(konten[fs.Konto], fs.Volk, fs.Nachname, personen[fs.Oberhaupt]);
            f.MitgliederListe.Clear();
            f.MitgliederListe.AddRange(fs.Mitglieder.Select(n => personen[n]));
            f.AhnenListe.AddRange(fs.Ahnen.Select(n => personen[n]));
            f.Ehepartner = fs.Ehepartner is int e ? personen[e] : null;
            f.Testament = fs.Testament is int t ? personen[t] : null;
            _familien[f.Konto] = f;
        }
        _kandidaten.Clear();
        _kandidaten.AddRange(s.Kandidaten.Select(k => new Kandidat(personen[k.Person], k.Stand)));
        _zuneigung.Clear();
        _zuneigungDiesesJahr.Clear();
        foreach (var z in s.Zuneigung)
        {
            _zuneigung[(konten[z.Konto], z.Kandidat)] = z.Wert;
            if (z.DiesesJahr > 0) _zuneigungDiesesJahr[(konten[z.Konto], z.Kandidat)] = z.DiesesJahr;
        }
        _laufend = s.DiesesJahr.Select(e => new FamilienEreignis(konten[e.Konto], e.Text)).ToList();
        _letztesJahr = s.LetztesJahr.Select(e => new FamilienEreignis(konten[e.Konto], e.Text)).ToList();
        ZuegeWiederherstellen(s, personen);
    }
}
