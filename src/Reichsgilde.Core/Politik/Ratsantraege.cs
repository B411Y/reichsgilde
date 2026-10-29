using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Politik;

/// <summary>Die Anträge rund um die Stadtkasse (PR #200).</summary>
public enum Antragsart
{
    /// <summary>Neuer Steuersatz (<see cref="Ratsantrag.Prozent"/>).</summary>
    Steuersatz,

    /// <summary>Ein Bauvorhaben der Stadt (<see cref="Ratsantrag.Bauvorhaben"/> = Kennung).</summary>
    Bauvorhaben,

    /// <summary>Ein Fest im nächsten Jahr.</summary>
    Fest,
}

/// <summary>
/// Ein Antrag im Rat zu Steuer, Bau oder Fest. Für die Prognose vor dem Antrag baut die Oberfläche ihn selbst,
/// z. B. <c>new Ratsantrag(Antragsart.Steuersatz, spieler, Prozent: 4)</c>.
/// </summary>
/// <param name="Prozent">Nur beim Steuersatz: der neue Satz in ganzen Prozent.</param>
/// <param name="Bauvorhaben">Nur beim Bauvorhaben: die Kennung aus <c>politik.bauvorhaben</c>.</param>
public sealed record Ratsantrag(Antragsart Art, Konto Antragsteller, int Prozent = 0, string Bauvorhaben = "");

/// <summary>Eine Baustelle der Stadt: fertig ab Spielminute <see cref="FertigAb"/>; die Darstellung stellt sie an den passenden Ort.</summary>
/// <param name="Antragsteller">Wer den Bau beantragt hat (null nur in alten Ständen).</param>
public sealed record Stadtbaustelle(string Kennung, string Name, Konto? Antragsteller, long FertigAb, double Kosten);

/// <summary>Ein Punkt der Tagesordnung der nächsten Ratssitzung (alle Anträge, in der Reihenfolge der Abstimmung).</summary>
/// <param name="Thema">„Statut“, „Stadtglaube“, „Pflaster“, „Steuersatz“, „Bauvorhaben“ oder „Fest“.</param>
public sealed record Tagesordnungspunkt(string Thema, Konto Antragsteller, string Text);

/// <summary>
/// Die Anträge zu Steuer, Bau und Fest (PR #200): je Art ein Antrag im Jahr, abgestimmt in der Ratssitzung am Jahresende.
///
/// Tagesordnung der Sitzung (in dieser Reihenfolge): Statut, Stadtglaube, Pflaster (Wegekasse), Steuersatz, Bauvorhaben, Fest.
/// Bauvorhaben und Fest werden erst bei der Abstimmung bezahlt; reicht die Kasse dann nicht mehr (z. B. weil gerade gebaut wurde),
/// wird aus dem angenommenen Antrag nichts.
///
/// Stimmen wie bei Statut und Stadtglaube: Ratsherren nach der Kasse und ihrer Gunst (<see cref="KasseDaten"/>), KI-Häuser nach ihren
/// Interessen, der Antragsteller dafür, andere Häuser wie festgelegt (sonst Enthaltung). Gleichstand: Der Bürgermeister entscheidet.
/// </summary>
public sealed class Ratsantraege
{
    private readonly Stadtwirtschaft _w;
    private readonly PolitikDaten _d;
    private readonly Dictionary<Antragsart, Ratsantrag> _antraege = new();
    private readonly Dictionary<(Konto Konto, Antragsart Art), bool> _stimmen = new();
    private readonly List<Stadtbaustelle> _baustellen = new();
    private readonly List<string> _gebaut = new();
    private readonly List<(Konto Konto, double Wert, int Bis)> _ansehen = new();
    private int? _festJahr;

    internal Ratsantraege(Stadtwirtschaft w, PolitikDaten daten)
    {
        _w = w;
        _d = daten;
    }

    /// <summary>Gibt es diese Anträge? (Nur mit Stadtkasse.)</summary>
    public bool IstAktiv => _w.Kasse.IstAktiv;

    private Stadtkasse Kasse => _w.Kasse;

    // ═════════════════════════════════════════════════════════════════════════
    //  Auskunft
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die vorliegenden Anträge dieses Jahres (Steuersatz, Bauvorhaben, Fest – in dieser Reihenfolge).</summary>
    public IReadOnlyList<Ratsantrag> Antraege => _antraege.OrderBy(a => a.Key).Select(a => a.Value).ToList();

    /// <summary>Der vorliegende Antrag dieser Art (null = keiner).</summary>
    public Ratsantrag? Antrag(Antragsart art) => _antraege.GetValueOrDefault(art);

    /// <summary>Alle Bauvorhaben aus den Daten.</summary>
    public IReadOnlyList<BauvorhabenDaten> Bauvorhaben => _d.Bauvorhaben;

    public BauvorhabenDaten? BauvorhabenNamens(string kennung) => _d.Bauvorhaben.FirstOrDefault(b => b.Kennung == kennung);

    /// <summary>Die laufenden Baustellen der Stadt.</summary>
    public IReadOnlyList<Stadtbaustelle> Baustellen => _baustellen;

    /// <summary>Die fertigen Bauvorhaben (Kennungen, in der Reihenfolge der Fertigstellung).</summary>
    public IReadOnlyList<string> Gebaut => _gebaut;

    public bool IstGebaut(string kennung) => _gebaut.Contains(kennung);
    public bool ImBau(string kennung) => _baustellen.Any(b => b.Kennung == kennung);

    /// <summary>Gilt in diesem Jahr ein Fest?</summary>
    public bool FestLaeuft => _festJahr is { } j && j == _w.Jahr;

    /// <summary>Das Jahr, in dem das zuletzt beschlossene Fest gilt (null = noch keines).</summary>
    public int? FestJahr => _festJahr;

    /// <summary>Ansehen aus angenommenen Anträgen (Fest, Bauvorhaben), solange es gilt.</summary>
    public double AnsehenBonus(Konto konto) => _ansehen.Where(a => a.Konto == konto && a.Bis >= _w.Jahr).Sum(a => a.Wert);

    // ── Wirkungen (alle 1 bzw. 0 ohne Bau und Fest) ─────────────────────────

    private double Gebaute(Bauwirkung wirkung) =>
        _d.Bauvorhaben.Where(b => b.Wirkung == wirkung && IstGebaut(b.Kennung)).Aggregate(1.0, (f, b) => f * (1 - b.Wert));

    /// <summary>Faktor auf die Chance zu erkranken (Brunnen).</summary>
    public double Krankheitsfaktor => Gebaute(Bauwirkung.Gesundheit);

    /// <summary>Faktor auf die Erfolgschance nächtlicher Taten in der Stadt (Mauer).</summary>
    public double Nachtschutzfaktor => Gebaute(Bauwirkung.Nachtschutz);

    /// <summary>Faktor auf Lade- und Liegezeit der Schiffe (Hafen).</summary>
    public double Hafenfaktor => Gebaute(Bauwirkung.Hafen);

    /// <summary>Faktor auf die Nachfrage aller Waren: größerer Marktplatz, Fest.</summary>
    public double Nachfragefaktor =>
        _d.Bauvorhaben.Where(b => b.Wirkung == Bauwirkung.Nachfrage && IstGebaut(b.Kennung)).Aggregate(1.0, (f, b) => f * (1 + b.Wert))
        * (FestLaeuft ? 1 + _d.Fest.Nachfrage : 1);

    /// <summary>Faktor auf die Wirtshausgäste (Fest).</summary>
    public double Gaestefaktor => FestLaeuft ? 1 + _d.Fest.Gaeste : 1;

    /// <summary>Was ein Antrag kostet (Bau, Fest; 0 beim Steuersatz).</summary>
    public double Kosten(Ratsantrag a) => a.Art switch
    {
        Antragsart.Bauvorhaben => BauvorhabenNamens(a.Bauvorhaben)?.Kosten ?? 0,
        Antragsart.Fest => _d.Fest.Kosten,
        _ => 0,
    };

    /// <summary>Ein kurzer Satz zum Antrag, z. B. „Steuersatz 4 % (bisher 5 %)“.</summary>
    public string Beschreibung(Ratsantrag a) => a.Art switch
    {
        Antragsart.Steuersatz => $"Steuersatz {a.Prozent} % (bisher {Kasse.SteuersatzProzent} %)",
        Antragsart.Bauvorhaben => $"Bauvorhaben „{BauvorhabenNamens(a.Bauvorhaben)?.Name ?? a.Bauvorhaben}“ für {Zahlenformat.Ganz(Kosten(a))} {_w.Daten.Allgemein.Waehrung}",
        _ => $"Fest im Jahr {_w.Jahr + 1} für {Zahlenformat.Ganz(Kosten(a))} {_w.Daten.Allgemein.Waehrung}",
    };

    /// <summary>
    /// Die Tagesordnung der nächsten Ratssitzung: alle vorliegenden Anträge in der Reihenfolge der Abstimmung
    /// (Statut, Stadtglaube, Pflaster, Steuersatz, Bauvorhaben, Fest).
    /// </summary>
    public IReadOnlyList<Tagesordnungspunkt> Tagesordnung
    {
        get
        {
            var liste = new List<Tagesordnungspunkt>();
            if (_w.Rat.Antrag is { } s)
                liste.Add(new("Statut", s.Antragsteller, $"Statut {s.Statut.Kuerzel()} – {s.Statut.Name()}"));
            if (_w.Glaube.IstAktiv && _w.Glaube.Antrag is { } g)
                liste.Add(new("Stadtglaube", g.Antragsteller, $"Stadtglaube: {Glaube.KonfessionTexte.StadtglaubeText(g.Glaube)}"));
            if (_w.Wegebau.IstAktiv && _w.Wegebau.Antrag is { } p)
                liste.Add(new("Pflaster", p.Antragsteller, $"Weg pflastern ({Zahlenformat.Ganz(p.Bis - p.Von)} m, aus der Wegekasse)"));
            foreach (var a in Antraege)
                liste.Add(new(a.Art switch { Antragsart.Steuersatz => "Steuersatz", Antragsart.Bauvorhaben => "Bauvorhaben", _ => "Fest" },
                    a.Antragsteller, Beschreibung(a)));
            return liste;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Anträge stellen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Darf dieses Haus den Antrag stellen? (null = ja, sonst der Grund)</summary>
    public string? BeantragenVerboten(Ratsantrag a)
    {
        var konto = a.Antragsteller;
        if (!IstAktiv) return "In dieser Stadt gibt es keine Stadtkasse.";
        if (_w.Rat.AmtVon(konto) is null) return "Anträge stellen dürfen nur Mitglieder des Rates.";
        if (Antrag(a.Art) is { } vorliegend)
            return $"Für dieses Jahr liegt schon ein Antrag vor ({vorliegend.Antragsteller.Inhaber}: {Beschreibung(vorliegend)}).";
        var k = Kasse.Daten;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        switch (a.Art)
        {
            case Antragsart.Steuersatz:
                if (a.Prozent < k.SteuerMin || a.Prozent > k.SteuerMax)
                    return $"Der Steuersatz liegt zwischen {k.SteuerMin} und {k.SteuerMax} %.";
                if (a.Prozent == Kasse.SteuersatzProzent) return $"Der Steuersatz ist schon {a.Prozent} %.";
                return null;
            case Antragsart.Bauvorhaben:
                if (BauvorhabenNamens(a.Bauvorhaben) is not { } b) return $"Ein Bauvorhaben „{a.Bauvorhaben}“ gibt es nicht.";
                if (IstGebaut(b.Kennung)) return $"{b.Name}: Das ist schon gebaut.";
                if (ImBau(b.Kennung)) return $"{b.Name}: Daran wird schon gebaut.";
                if (b.Wirkung == Bauwirkung.Hafen && !_w.Schiffahrt.IstAktiv) return $"{b.Name}: Die Stadt hat keinen Hafen.";
                if (Kasse.Stand < b.Kosten)
                    return $"{b.Name} kostet {Zahlenformat.Ganz(b.Kosten)} {waehrung} – in der Stadtkasse liegen nur {Zahlenformat.Ganz(Kasse.Stand)}.";
                return null;
            default:
                if (Kasse.Stand < _d.Fest.Kosten)
                    return $"Ein Fest kostet {Zahlenformat.Ganz(_d.Fest.Kosten)} {waehrung} – in der Stadtkasse liegen nur {Zahlenformat.Ganz(Kasse.Stand)}.";
                return null;
        }
    }

    internal Befehlsergebnis Beantragen(Ratsantrag a)
    {
        if (BeantragenVerboten(a) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        _antraege[a.Art] = a;
        foreach (var s in _stimmen.Keys.Where(s => s.Art == a.Art).ToList()) _stimmen.Remove(s); // Stimmen zum alten gelten nicht mehr
        return Befehlsergebnis.Ok($"{a.Antragsteller.Inhaber} beantragt im Rat: {Beschreibung(a)}. Der Rat stimmt am Jahresende ab.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Stimmen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Würde dieser Ratsherr zustimmen? Mit Gunst ab <c>kasse.antragGunst</c> immer; sonst rechnet er wie ein Haushälter mit der Kasse und
    /// seiner Reserve: Steuer rauf, wenn die Kasse unter der Reserve liegt oder der Haushalt des Vorjahres im Minus war; runter nur, wenn
    /// die Kasse mehr als das Doppelte hat und der Haushalt auch mit dem neuen Satz im Plus bliebe (<see cref="Stadtkasse.HaushaltMit"/>);
    /// Bau und Fest, wenn danach noch die Reserve bleibt.
    /// </summary>
    public bool StimmtZu(Ratsherr r, Ratsantrag a)
    {
        var k = Kasse.Daten;
        if (_w.Rat.Meinung(r, a.Antragsteller) >= k.AntragGunst) return true; // Meinung über das Oberhaupt (PR #231)
        var reserve = k.ReserveVon(r.Daten.Volk);
        return a.Art switch
        {
            Antragsart.Steuersatz => a.Prozent > Kasse.SteuersatzProzent
                ? Kasse.Stand < reserve || Kasse.HaushaltMit(Kasse.SteuersatzProzent) < 0
                : Kasse.Stand > 2 * reserve && Kasse.HaushaltMit(a.Prozent) >= 0,
            _ => Kasse.Stand - Kosten(a) >= reserve,
        };
    }

    /// <summary>
    /// Wie ginge die Abstimmung heute aus? Häuser, die keine KI sind, stimmen wie festgelegt (nur beim vorliegenden Antrag)
    /// und enthalten sich sonst.
    /// </summary>
    public (int Ja, int Nein, int Enthaltung, bool? Buergermeister) Prognose(Ratsantrag a) =>
        _w.Rat.Zaehlen(sitz => StimmeVonSitz(sitz, a));

    /// <summary>Die Stimme eines Sitzes zum Antrag (true dafür, false dagegen, null Enthaltung) – in Prognose und Sitzung dieselbe.</summary>
    internal bool? StimmeVonSitz(Amt sitz, Ratsantrag a) =>
        sitz.Ratsherr is { } r ? StimmtZu(r, a)
        : sitz.Konto == a.Antragsteller ? true
        : _w.Rivalen.FirstOrDefault(k => k.Konto == sitz.Konto) is { } ki ? ki.StimmtFuerRatsantrag(a)
        : Antrag(a.Art) == a ? StimmeVon(sitz.Konto!, a.Art) // die festgelegte Stimme gilt nur dem vorliegenden Antrag
        : null;

    /// <summary>Darf dieses Haus zum vorliegenden Antrag dieser Art eine Stimme festlegen? (null = ja, sonst der Grund)</summary>
    public string? StimmeVerboten(Konto konto, Antragsart art)
    {
        if (!IstAktiv) return "In dieser Stadt gibt es keine Stadtkasse.";
        if (_w.Rat.StimmeVerboten(konto) is { } grund) return grund;
        if (Antrag(art) is not { } a) return "Dazu liegt dieses Jahr kein Antrag vor.";
        if (a.Antragsteller == konto) return $"{konto.Inhaber} hat den Antrag gestellt und stimmt ohnehin dafür.";
        return null;
    }

    /// <summary>Die festgelegte Stimme dieses Hauses zum vorliegenden Antrag (true = dafür, false = dagegen, null = Enthaltung).</summary>
    public bool? StimmeVon(Konto konto, Antragsart art) =>
        StimmeVerboten(konto, art) is null && _stimmen.TryGetValue((konto, art), out var ja) ? ja : null;

    /// <summary>Stimme zum Antrag festlegen (true/false) oder sich enthalten (null) – gilt bis zur Sitzung.</summary>
    internal Befehlsergebnis StimmeFestlegen(Konto konto, Antragsart art, bool? ja)
    {
        if (StimmeVerboten(konto, art) is { } grund) return Befehlsergebnis.Abgelehnt(grund);
        var a = Antrag(art)!;
        var worum = $"den Antrag von {a.Antragsteller.Inhaber} ({Beschreibung(a)})";
        if (ja is not { } stimme)
        {
            _stimmen.Remove((konto, art));
            return Befehlsergebnis.Ok($"{konto.Inhaber} enthält sich bei der Abstimmung über {worum}.");
        }
        _stimmen[(konto, art)] = stimme;
        return Befehlsergebnis.Ok($"{konto.Inhaber} stimmt {(stimme ? "für" : "gegen")} {worum}. Abgestimmt wird am Jahresende.");
    }

    /// <summary>Ein Haus scheidet aus dem Rat aus (Bankrott, Erbfall, Urteil): seine Anträge und Stimmen verfallen.</summary>
    internal void Ausscheiden(Konto konto)
    {
        foreach (var art in _antraege.Where(a => a.Value.Antragsteller == konto).Select(a => a.Key).ToList())
        {
            _antraege.Remove(art);
            foreach (var s in _stimmen.Keys.Where(s => s.Art == art).ToList()) _stimmen.Remove(s);
        }
        foreach (var s in _stimmen.Keys.Where(s => s.Konto == konto).ToList()) _stimmen.Remove(s);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Sitzung und Ablauf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>In der Ratssitzung, nach Statut, Stadtglaube und Pflaster: Abstimmung über den vorliegenden Antrag dieser Art.</summary>
    internal (int Ja, int Nein, int Enthaltung, bool? Buergermeister, bool Angenommen) Abstimmen(Antragsart art, int jahr, Action<string> protokoll)
    {
        var a = Antrag(art)!;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        var (ja, nein, enthaltung, bm) = Prognose(a); // mit den festgelegten Stimmen
        var angenommen = Stadtrat.IstAngenommen(ja, nein, bm);
        var ergebnis = (ja, nein, enthaltung, bm, angenommen);
        var was = $"Ratssitzung {jahr}: Antrag von {a.Antragsteller.Inhaber} – {Beschreibung(a)} –";
        var stimmen = $"({ja} dafür, {nein} dagegen{(ja == nein ? ", der Bürgermeister entscheidet" : "")})";
        if (!angenommen)
        {
            protokoll($"{was} abgelehnt {stimmen}.");
            return ergebnis;
        }
        var kosten = Kosten(a);
        if (kosten > 0 && kosten > Kasse.Stand + 1e-9)
        {
            protokoll($"{was} angenommen {stimmen}, aber die Stadtkasse reicht nicht mehr ({Zahlenformat.Ganz(Kasse.Stand)} {waehrung}).");
            return ergebnis;
        }
        switch (a.Art)
        {
            case Antragsart.Steuersatz:
                Kasse.SteuersatzSetzen(a.Prozent);
                protokoll($"{was} ANGENOMMEN {stimmen}. Ab sofort gilt eine Umsatzsteuer von {a.Prozent} %.");
                break;
            case Antragsart.Bauvorhaben:
                var b = BauvorhabenNamens(a.Bauvorhaben)!;
                Kasse.Buchen(Kassenposten.Bauvorhaben, kosten);
                _baustellen.Add(new Stadtbaustelle(b.Kennung, b.Name, a.Antragsteller, _w.Uhr.VergangeneMinuten + b.BauzeitStunden * 60L, kosten));
                if (b.Ansehen > 0 && b.AnsehenJahre > 0) _ansehen.Add((a.Antragsteller, b.Ansehen, _w.Jahr + b.AnsehenJahre - 1));
                protokoll($"{was} ANGENOMMEN {stimmen}. Die Stadt baut: {b.Name}. {a.Antragsteller.Inhaber} gewinnt Ansehen.");
                break;
            default:
                Kasse.Buchen(Kassenposten.Feste, kosten);
                _festJahr = _w.Jahr;
                if (_d.Fest.Ansehen > 0) _ansehen.Add((a.Antragsteller, _d.Fest.Ansehen, _w.Jahr));
                _w.Meinungen.FestAusgerichtet(a.Antragsteller); // die Stadt denkt besser über den Gastgeber (PR #231)
                protokoll($"{was} ANGENOMMEN {stimmen}. Im Jahr {_w.Jahr} feiert die Stadt – {a.Antragsteller.Inhaber} gewinnt Ansehen.");
                break;
        }
        return ergebnis;
    }

    /// <summary>
    /// Nach Steuersatz, Bauvorhaben und Fest (auch ohne Kasse): Die Anträge und Stimmen des Jahres sind erledigt, abgelaufenes Ansehen fällt weg.
    /// Bis dahin bleiben die Anträge stehen – die Stimmen zum nächsten Punkt gelten weiter.
    /// </summary>
    internal void SitzungAbschliessen()
    {
        var aktiv = IstAktiv;
        _antraege.Clear();
        _stimmen.Clear();
        if (aktiv) _ansehen.RemoveAll(x => x.Bis < _w.Jahr);
    }

    /// <summary>Jede Spielminute: fertige Baustellen der Stadt.</summary>
    internal void Minute(Spielzeit zeit)
    {
        if (_baustellen.Count == 0) return;
        foreach (var b in _baustellen.Where(b => zeit.VergangeneMinuten >= b.FertigAb).ToList())
        {
            _baustellen.Remove(b);
            _gebaut.Add(b.Kennung);
            _w.Melden($"Die Stadt hat gebaut: {b.Name} ist fertig.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal RatsantraegeStand Sichern(Func<Konto, int> nr) => new()
    {
        Antraege = Antraege.Select(a => new RatsantragStand { Art = a.Art, Konto = nr(a.Antragsteller), Prozent = a.Prozent, Bauvorhaben = a.Bauvorhaben }).ToList(),
        Stimmen = _stimmen.OrderBy(s => s.Key.Art).ThenBy(s => nr(s.Key.Konto))
            .Select(s => new RatsantragStimmeStand { Art = s.Key.Art, Konto = nr(s.Key.Konto), Ja = s.Value }).ToList(),
        Baustellen = _baustellen.Select(b => new StadtbaustelleStand
        {
            Kennung = b.Kennung, Konto = b.Antragsteller is null ? null : nr(b.Antragsteller), FertigAb = b.FertigAb, Kosten = b.Kosten,
        }).ToList(),
        Gebaut = new(_gebaut),
        Ansehen = _ansehen.Select(a => new AntragAnsehenStand { Konto = nr(a.Konto), Wert = a.Wert, Bis = a.Bis }).ToList(),
        FestJahr = _festJahr,
    };

    internal void Wiederherstellen(RatsantraegeStand? s, IReadOnlyList<Konto> konten)
    {
        if (s is null) return;
        foreach (var a in s.Antraege)
            _antraege[a.Art] = new Ratsantrag(a.Art, konten[a.Konto], a.Prozent, a.Bauvorhaben);
        foreach (var st in s.Stimmen)
            _stimmen[(konten[st.Konto], st.Art)] = st.Ja;
        foreach (var b in s.Baustellen)
            if (BauvorhabenNamens(b.Kennung) is { } d)
                _baustellen.Add(new Stadtbaustelle(b.Kennung, d.Name, b.Konto is int k ? konten[k] : null, b.FertigAb, b.Kosten));
        _gebaut.AddRange(s.Gebaut);
        foreach (var a in s.Ansehen) _ansehen.Add((konten[a.Konto], a.Wert, a.Bis));
        _festJahr = s.FestJahr;
    }
}
