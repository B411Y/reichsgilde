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
public sealed class Familienbuch
{
    private readonly Stadtwirtschaft _w;
    private readonly DynastieDaten _d;
    private Zufallsquelle _zufall;
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
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Auskunft
    // ═════════════════════════════════════════════════════════════════════════

    public Familie? Von(Konto konto) => _familien.GetValueOrDefault(konto);

    public Kandidat? KandidatNummer(int nummer) => _kandidaten.FirstOrDefault(k => k.Person.Nummer == nummer);

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
        if (f.Testament is { Lebt: true } t && t.IstKindVon(f.Oberhaupt) && IstVolljaehrig(t))
            return t;
        if (f.Kinder.FirstOrDefault(k => k.Lebt && IstVolljaehrig(k)) is { } aeltestes)
            return aeltestes;
        return _d.Witwenrecht ? f.Ehepartner : null;
    }

    /// <summary>Chance auf ein Kind in diesem Jahr.</summary>
    public double Geburtschance(Familie f)
    {
        if (f.Ehepartner is not { } partner) return 0;
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

    /// <summary>Was die Familie jedes Jahr kostet: Haushalt für Oberhaupt und Ehepartner, Unterhalt für alle anderen.</summary>
    public double Haushaltskosten(Familie f) => f.Lebende.Sum(p =>
        p == f.Oberhaupt || p == f.Ehepartner ? _d.Volk(p.Volk).Haushalt : _d.Volk(p.Volk).Unterhalt);

    public double Zuneigung(Konto konto, Kandidat k) => _zuneigung.GetValueOrDefault((konto, k.Person.Nummer));

    /// <summary>So viel Zuneigung lässt sich dieses Jahr noch gewinnen.</summary>
    public double ZuneigungRest(Konto konto, Kandidat k) =>
        Math.Max(0, _d.ZuneigungJeJahr - _zuneigungDiesesJahr.GetValueOrDefault((konto, k.Person.Nummer)));

    /// <summary>Werben noch andere Häuser um diesen Kandidaten? (In der Stadt spricht sich so etwas herum.)</summary>
    public IEnumerable<Konto> Mitbewerber(Konto konto, Kandidat k) =>
        _w.Konten.Where(x => x != konto && Zuneigung(x, k) > 0.5);

    /// <summary>Zuneigung + Ansehen, ab der der Kandidat einwilligt.</summary>
    public double Heiratsschwelle(Kandidat k) => _d.Heiratsschwelle + k.Stand * _d.HeiratsschwelleJeStand;

    /// <summary>Zuneigung + Ansehen des Hauses beim Kandidaten.</summary>
    public double Bereitschaft(Konto konto, Kandidat k) => Zuneigung(konto, k) + _w.Rat.Ansehen(konto);

    public double Hochzeitskosten(Kandidat k) => _d.Volk(k.Person.Volk).Hochzeit * (1 + _d.HochzeitJeStand * k.Stand);

    /// <summary>Gulden für einen Punkt Zuneigung, wenn dieses Haus wirbt (das Volk des Oberhaupts wirbt verschieden gut).</summary>
    public double GuldenJeZuneigung(Konto konto) =>
        Von(konto) is { } f ? 1 / (_d.ZuneigungJeGulden * _d.Volk(f.Oberhaupt.Volk).Werbefaktor) : 1 / _d.ZuneigungJeGulden;

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
    public Familie Gruenden(Konto konto, Volk volk, Geschlecht geschlecht, string? vorname = null, int? alter = null)
    {
        if (!IstAktiv) throw new InvalidOperationException("Die Wirtschaftsdaten enthalten keine Dynastie-Daten.");
        if (_familien.ContainsKey(konto)) throw new InvalidOperationException($"{konto.Inhaber} hat schon eine Familie.");
        if (!_w.Konten.Contains(konto)) throw new ArgumentException("Das Konto gehört nicht zu dieser Stadt.", nameof(konto));
        var v = _d.Volk(volk); // wirft bei Orks und Ogern, die keine Daten haben

        Jahr = konto.Laufend.Jahr;
        var nachname = Familie.NachnameAus(konto.Inhaber);
        vorname = string.IsNullOrWhiteSpace(vorname) ? Vorname(v, geschlecht) : vorname.Trim();
        var oberhaupt = new Person(_naechsteNummer++, vorname, nachname, volk, geschlecht, Jahr - (alter ?? v.Startalter));
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

    private void Ereignis(Konto konto, string text)
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
        var jeZuneigung = GuldenJeZuneigung(konto);
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
        f.MitgliederListe.Add(p);
        f.Ehepartner = p;
        Ereignis(konto, $"Hochzeit im Haus {f.Nachname}: {f.Oberhaupt.Name} heiratet {p.Vorname}, geb. {p.Geburtsname} " +
                        $"({p.Volk.Einzahl()}, {p.Alter(Jahr)} Jahre). Das Fest kostet {Zahlenformat.Ganz(kosten)} {_w.Daten.Allgemein.Waehrung}.");
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
                konto.Familienkosten(Haushaltskosten(f));
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
        var alter = p.Alter(Jahr);
        if (p == f.Ehepartner)
        {
            f.Ehepartner = null;
            Ereignis(f.Konto, $"Trauer im Haus {f.Nachname}: {p.Vorname}, {p.Ehepartnertitel} von {f.Oberhaupt.Vorname}, ist mit {alter} Jahren gestorben.");
        }
        else if (p != f.Oberhaupt)
            Ereignis(f.Konto, $"Trauer im Haus {f.Nachname}: {p.Vorname} ist mit {alter} Jahren gestorben.");
        if (f.Testament == p) f.Testament = null;
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
            Ereignis(f.Konto, $"{tot.OberhauptTitel} {tot.Name} ist mit {tot.Alter(Jahr)} Jahren gestorben – ohne Erben. " +
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
        Ereignis(f.Konto, $"{tot.OberhauptTitel} {tot.Name} ist mit {tot.Alter(Jahr)} Jahren gestorben. {wieErbe} ({erbe.Alter(Jahr)} Jahre). " +
                          "Ämter und Gunst im Rat beginnen von vorn.");
        Erbfall?.Invoke(f, tot, erbe);
    }

    private void Geburt(Familie f)
    {
        Paar(f.Oberhaupt, f.Ehepartner!, out var mutter, out var vater);
        var geschlecht = _zufall.Next(2) == 0 ? Geschlecht.Frau : Geschlecht.Mann;
        var v = _d.Volk(f.Volk);
        var kind = new Person(_naechsteNummer++, Vorname(v, geschlecht), f.Nachname, f.Volk, geschlecht, Jahr + 1, vater.Nummer, mutter.Nummer);
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
            }).ToList();
        return new FamilienbuchStand
        {
            Jahr = Jahr,
            ZufallWuerfe = _zufall.Wuerfe,
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
        var personen = s.Personen.ToDictionary(p => p.Nummer, p => new Person(p.Nummer, p.Vorname, p.Familienname, p.Volk,
            p.Geschlecht, p.Geburtsjahr, p.Vater, p.Mutter, p.Geburtsname, p.Todesjahr));

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
    }
}
