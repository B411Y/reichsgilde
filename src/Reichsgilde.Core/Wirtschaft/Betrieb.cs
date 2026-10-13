namespace Reichsgilde.Core.Wirtschaft;

public enum Betriebsart
{
    /// <summary>Werkstatt des Schmieds, Stufe 1. Stellt Beschlag, Rapier und Silberring her.</summary>
    Giesserei,

    /// <summary>Umlandbetrieb (gepachtet). Fördert Eisen und Silber.</summary>
    Mine,
}

/// <summary>
/// Ein Betrieb mit Arbeitskräften, Lager und Karren.
///
/// Ablauf:
///   • Während der Arbeitszeit produzieren die Arbeitskräfte. Zutaten kommen aus dem Lager,
///     fertige Waren gehen ins Lager.
///   • Fehlen Zutaten oder ist das Lager voll, steht die Produktion still.
///   • Die Karren bringen Waren zum Markt und Zutaten zurück (siehe <see cref="Karren"/>).
/// </summary>
public sealed class Betrieb
{
    private readonly Wirtschaftsdaten _daten;
    private readonly Dictionary<string, int> _belegung = new();
    private readonly Dictionary<string, int> _lager = new();
    private readonly List<Karren> _karren = new();
    private double _fortschritt;        // Gießerei: Arbeitsminuten am aktuellen Stück
    private double _fortschrittEisen;   // Mine
    private double _fortschrittSilber;  // Mine
    private string _produkt = "Silberring";
    private double _silberanteil = 0.5;
    private string? _letzterStillstand;

    public string Name { get; }
    public Betriebsart Art { get; }
    public Konto Konto { get; }

    public Arbeitsort Ort => Art == Betriebsart.Mine ? Arbeitsort.Umland : Arbeitsort.Werkstatt;

    public int Plaetze => Art == Betriebsart.Mine ? _daten.Allgemein.PlaetzeMine : _daten.Allgemein.PlaetzeGiesserei;

    public IReadOnlyDictionary<string, int> Belegung => _belegung;
    public int Belegt => _belegung.Values.Sum();

    public IReadOnlyDictionary<string, int> Lager => _lager;
    public int LagerKapazitaetJeWare => _daten.Logistik.LagerJeWare;
    public int ImLager(string ware) => _lager.GetValueOrDefault(ware);

    public IReadOnlyList<Karren> Karren => _karren;

    /// <summary>Warum die Produktion gerade stillsteht (null = sie läuft).</summary>
    public string? Stillstand { get; private set; }

    /// <summary>Arbeitsminuten mit Stillstand im laufenden Jahr.</summary>
    public int StillstandMinuten { get; private set; }

    /// <summary>Arbeitsminuten mit Stillstand im Vorjahr – zeigt, ob Karren oder Lager zu knapp sind.</summary>
    public int StillstandMinutenVorjahr { get; private set; }

    /// <summary>Was die Gießerei herstellt. Nur Waren mit Zutaten (Beschlag, Rapier, Silberring).</summary>
    public string Produkt
    {
        get => _produkt;
        set
        {
            if (Art != Betriebsart.Giesserei)
                throw new InvalidOperationException("Nur eine Gießerei hat ein Produkt.");
            if (!_daten.Waren.TryGetValue(value, out var ware) || ware.Zutaten.Count == 0)
                throw new ArgumentException($"„{value}“ kann eine Gießerei nicht herstellen.");
            if (value != _produkt) _fortschritt = 0;
            _produkt = value;
        }
    }

    /// <summary>Mine: Anteil der Arbeitszeit für Silber (0 = nur Eisen, 1 = nur Silber).</summary>
    public double Silberanteil
    {
        get => _silberanteil;
        set => _silberanteil = Math.Clamp(value, 0, 1);
    }

    /// <summary>Waren, die dieser Betrieb als Zutaten braucht (nur Gießerei).</summary>
    public IReadOnlyDictionary<string, int> Zutaten =>
        Art == Betriebsart.Giesserei ? _daten.Waren[_produkt].Zutaten : new Dictionary<string, int>();

    /// <summary>Für wie viele Stück die Zutaten im Lager noch reichen (Mine: unendlich).</summary>
    public int ZutatenReichweite =>
        Zutaten.Count == 0 ? int.MaxValue : Zutaten.Min(z => ImLager(z.Key) / z.Value);

    internal Betrieb(string name, Betriebsart art, Konto konto, Wirtschaftsdaten daten)
    {
        Name = name;
        Art = art;
        Konto = konto;
        _daten = daten;
    }

    // ── Personal ────────────────────────────────────────────────────────────

    /// <summary>
    /// Legt fest, wie viele Arbeitskräfte einer Art hier arbeiten.
    /// Prüft dabei das geltende Fronstatut und die Zahl der Arbeitsplätze.
    /// </summary>
    /// <exception cref="InvalidOperationException">Wenn die Belegung nicht erlaubt ist – mit verständlicher Begründung.</exception>
    public void Einstellen(string arbeitskraft, int anzahl, Fronstatut statut)
    {
        if (!_daten.Arbeitskraefte.TryGetValue(arbeitskraft, out var kraft))
            throw new ArgumentException($"Die Arbeitskraft „{arbeitskraft}“ gibt es nicht.");
        if (anzahl < 0)
            throw new ArgumentOutOfRangeException(nameof(anzahl));
        if (anzahl > 0 && !kraft.IstErlaubt(statut, Ort))
            throw new InvalidOperationException(
                $"{arbeitskraft} darf unter dem Statut „{statut.Name()}“ nicht im {(Ort == Arbeitsort.Umland ? "Umland" : "Werkstattbetrieb")} arbeiten.");

        var andere = Belegt - _belegung.GetValueOrDefault(arbeitskraft);
        if (andere + anzahl > Plaetze)
            throw new InvalidOperationException($"{Name} hat nur {Plaetze} Arbeitsplätze ({andere} sind schon belegt).");

        if (anzahl == 0) _belegung.Remove(arbeitskraft);
        else _belegung[arbeitskraft] = anzahl;
    }

    /// <summary>Entlässt alle Arbeitskräfte, die unter dem Statut nicht erlaubt sind.</summary>
    internal List<(string Kraft, int Anzahl)> UnerlaubteEntlassen(Fronstatut statut)
    {
        var entlassen = _belegung
            .Where(b => !_daten.Arbeitskraefte[b.Key].IstErlaubt(statut, Ort))
            .Select(b => (b.Key, b.Value))
            .ToList();
        foreach (var (kraft, _) in entlassen)
            _belegung.Remove(kraft);
        return entlassen;
    }

    // ── Karren ──────────────────────────────────────────────────────────────

    /// <summary>Stellt einen Karren in Dienst, ohne ihn zu bezahlen (Startausstattung).</summary>
    internal Karren KarrenHinzufuegen(string typ)
    {
        if (!_daten.Logistik.Karren.TryGetValue(typ, out var daten))
            throw new ArgumentException($"Den Karrentyp „{typ}“ gibt es nicht.");
        var karren = new Karren($"{typ} {_karren.Count + 1}", typ, daten, this);
        _karren.Add(karren);
        return karren;
    }

    /// <summary>Kauft einen neuen Karren. Der Preis wird als Investition verbucht.</summary>
    public Karren KarrenKaufen(string typ)
    {
        var karren = KarrenHinzufuegen(typ);
        Konto.Investition(karren.Daten.Preis);
        return karren;
    }

    // ── Lager ───────────────────────────────────────────────────────────────

    internal void Einlagern(string ware, int stueck) => _lager[ware] = ImLager(ware) + stueck;

    internal int Auslagern(string ware, int hoechstens)
    {
        var menge = Math.Min(hoechstens, ImLager(ware));
        if (menge <= 0) return 0;
        _lager[ware] = ImLager(ware) - menge;
        if (_lager[ware] == 0) _lager.Remove(ware);
        return menge;
    }

    /// <summary>Waren im Lager, die zum Verkauf bestimmt sind (alles außer den eigenen Zutaten).</summary>
    internal IEnumerable<(string Ware, int Stueck)> Verkaufsware() =>
        _lager.Where(l => !Zutaten.ContainsKey(l.Key) && l.Value > 0).Select(l => (l.Key, l.Value));

    // ── Produktion ──────────────────────────────────────────────────────────

    /// <summary>Arbeitsminuten, die die Belegschaft in einer Spielminute schafft.</summary>
    public double LeistungProMinute =>
        _belegung.Sum(b => b.Value * _daten.Arbeitskraefte[b.Key].TempoAm(Ort));

    /// <summary>Eine Spielminute Arbeit. Wird nur während der Arbeitszeit aufgerufen.</summary>
    internal void Arbeitsminute(Action<string> melden)
    {
        var leistung = LeistungProMinute;
        if (leistung <= 0) return;

        string? stillstand = null;
        if (Art == Betriebsart.Giesserei)
        {
            var zeit = _daten.Waren[_produkt].Zeit;
            _fortschritt += leistung;
            while (_fortschritt >= zeit)
            {
                stillstand = KannHerstellen(_produkt);
                if (stillstand is not null)
                {
                    _fortschritt = zeit; // wartet, statt Arbeit „anzusparen"
                    break;
                }
                foreach (var (zutat, menge) in Zutaten)
                    Auslagern(zutat, menge);
                Einlagern(_produkt, 1);
                Konto.Hergestellt(_produkt);
                _fortschritt -= zeit;
            }
        }
        else
        {
            _fortschrittEisen += leistung * (1 - _silberanteil);
            _fortschrittSilber += leistung * _silberanteil;
            // Beide Waren getrennt fördern: Ist das Eisenlager voll, geht die Silberförderung weiter.
            var eisen = Foerdern("Eisen", ref _fortschrittEisen);
            var silber = Foerdern("Silber", ref _fortschrittSilber);
            stillstand = eisen ?? silber;
        }

        // Stillstand nur einmal melden, nicht jede Minute.
        if (stillstand is not null && stillstand != _letzterStillstand)
            melden($"{Name}: {stillstand}");
        _letzterStillstand = stillstand;
        Stillstand = stillstand;
        if (stillstand is not null) StillstandMinuten++;
    }

    private string? KannHerstellen(string ware)
    {
        if (ImLager(ware) >= LagerKapazitaetJeWare)
            return $"Das Lager für {ware} ist voll – die Arbeit ruht.";
        foreach (var (zutat, menge) in _daten.Waren[ware].Zutaten)
            if (ImLager(zutat) < menge)
                return $"Es fehlt {zutat} – die Arbeit ruht.";
        return null;
    }

    private string? Foerdern(string ware, ref double fortschritt)
    {
        var zeit = _daten.Waren[ware].Zeit;
        while (fortschritt >= zeit)
        {
            if (ImLager(ware) >= LagerKapazitaetJeWare)
            {
                fortschritt = zeit;
                return $"Das Lager für {ware} ist voll – die Arbeit ruht.";
            }
            Einlagern(ware, 1);
            Konto.Hergestellt(ware);
            fortschritt -= zeit;
        }
        return null;
    }

    /// <summary>Verkauft ein Stück auf dem Markt und verbucht Umsatz und Steuer.</summary>
    internal void Verkaufen(string ware, int stueck, Markt markt)
    {
        if (stueck <= 0) return;
        var erloes = markt[ware].Verkaufen(stueck);
        Konto.Umsatz(erloes);
        Konto.Steuer(erloes * _daten.Allgemein.Umsatzsteuer);
    }

    // ── Jahresende ──────────────────────────────────────────────────────────

    /// <summary>Löhne, Unterhalt bzw. Pacht und zufällige Ausbrüche oder Aufstände.</summary>
    internal void Jahresabrechnung(Random zufall, Action<string> melden)
    {
        foreach (var (name, anzahl) in _belegung)
        {
            var kraft = _daten.Arbeitskraefte[name];
            Konto.Personal(anzahl * kraft.Kosten);

            // Jede unfreie Arbeitskraft hat ihr eigenes Risiko.
            for (var i = 0; i < anzahl; i++)
            {
                if (kraft.Risiko > 0 && zufall.NextDouble() < kraft.Risiko)
                {
                    Konto.Schaden(kraft.Schaden);
                    melden(Art == Betriebsart.Mine
                        ? $"Ausbruch in {Name}: Ein {name} hat sich losgerissen. Schaden: {Zahlenformat.Ganz(kraft.Schaden)} {_daten.Allgemein.Waehrung}."
                        : $"Aufstand in {Name}: Ein {name} hat sich erhoben. Schaden: {Zahlenformat.Ganz(kraft.Schaden)} {_daten.Allgemein.Waehrung}.");
                }
            }
        }

        Konto.Fixkosten(Art == Betriebsart.Mine ? _daten.Allgemein.PachtMine : _daten.Allgemein.UnterhaltGiesserei);

        StillstandMinutenVorjahr = StillstandMinuten;
        StillstandMinuten = 0;
    }
}
