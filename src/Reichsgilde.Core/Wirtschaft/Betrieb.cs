using Reichsgilde.Core.Speichern;
namespace Reichsgilde.Core.Wirtschaft;

public enum Betriebsart
{
    /// <summary>Werkstatt des Schmieds, Stufe 1. Stellt Beschlag, Rapier und Silberring her.</summary>
    Giesserei,

    /// <summary>Umlandbetrieb (gepachtet). Fördert Eisen und Silber.</summary>
    Mine,

    /// <summary>Werkstatt des Tischlers, Stufe 1. Stellt Holzzapfen, Schemel und Truhen her.</summary>
    Tischlerei,

    /// <summary>Umlandbetrieb des Tischlers (gepachtet). Schlägt Holz.</summary>
    Holzfaellerei,

    /// <summary>Werkstatt des Schmieds, Stufe 2 (Ausbau der Gießerei ab Meistergrad 3).</summary>
    Schmiede,

    /// <summary>Werkstatt des Schmieds, Stufe 3 (Ausbau der Schmiede ab Meistergrad 5).</summary>
    Goldschmiede,

    /// <summary>Werkstatt des Tischlers, Stufe 2.</summary>
    Drechslerei,

    /// <summary>Werkstatt des Tischlers, Stufe 3.</summary>
    Saegewerk,

    /// <summary>Werkstatt des Steinmetzes, Stufe 1. Stellt Schleifziegel, Grabsteine und Mühlsteine her.</summary>
    Maurerei,

    /// <summary>Werkstatt des Steinmetzes, Stufe 2.</summary>
    Steinschneiderei,

    /// <summary>Werkstatt des Steinmetzes, Stufe 3.</summary>
    Steinmetzhuette,

    /// <summary>Umlandbetrieb des Steinmetzes (gepachtet). Bricht Bruchstein und Granit.</summary>
    Steinbruch,

    /// <summary>Werkstatt des Alchimisten, Stufe 1. Stellt Farbe, Kräuterschnaps und Heilsalbe her.</summary>
    Tinkturei,

    /// <summary>Werkstatt des Alchimisten, Stufe 2.</summary>
    Kraeuterladen,

    /// <summary>Werkstatt des Alchimisten, Stufe 3.</summary>
    Alchimistenstube,

    /// <summary>Umlandbetrieb des Alchimisten (gepachtet). Zieht Kräuter und fängt Kröten.</summary>
    Kraeutergarten,

    /// <summary>Werkstatt des Wirts, Stufe 1: Schänke – bewirtet Gäste, dunkle Ecke.</summary>
    Schaenke,

    /// <summary>Werkstatt des Wirts, Stufe 2: Wirtshaus – mit Würfeltisch.</summary>
    Wirtshaus,

    /// <summary>Werkstatt des Wirts, Stufe 3: Gasthaus – mit Stammtisch der Ratsherren.</summary>
    Gasthaus,

    /// <summary>Umlandbetrieb des Wirts (gepachtet). Baut Gerste und Roggen an.</summary>
    Bauernhof,

    /// <summary>Werkstatt des Predigers, Stufe 1: Kirche – Messe mit Kollekte, Ablass, Beichte.</summary>
    Kirche,

    /// <summary>Werkstatt des Predigers, Stufe 2.</summary>
    Dom,

    /// <summary>Werkstatt des Predigers, Stufe 3.</summary>
    Kathedrale,

    /// <summary>Umlandbetrieb des Predigers (gepachtet). Sammelt Lumpen und Bienenwachs.</summary>
    Klosterhof,
    Schmugglerloch,
    Unterschlupf,
    Diebesgilde,
    Wachhaus,
    Stadtwache,
    Garnison,
    Wachturm,
    Wechselstube,
    Geldleihe,
    Bankhaus,
    Siederei,
    Duftwerkstatt,
    Parfumerie,
    Duftgarten,
}

/// <summary>
/// Die Jahresrechnung eines einzelnen Betriebs – dieselben Posten wie der Betriebsgewinn des Kontos (<see cref="Jahresbilanz"/>),
/// aber nur, was diesem Betrieb gehört: Markt, Gäste, Kollekte, Sold, Beute, Zinsen der Stadtkunden; Zutaten, Fahrten, Löhne,
/// Unterhalt bzw. Pacht, Schäden durch Aufstände und Anschläge. Was dem ganzen Haus gehört (Kredite an andere Häuser, Kopfgeld,
/// Geleitgeld, Glaubenszins, Einbruch ins Wohnhaus), fehlt – die Summe aller Betriebe ist daher nicht genau der Gewinn des Kontos.
/// Für die KI: Welcher Betrieb macht Verlust?
/// </summary>
public sealed class Betriebsrechnung
{
    public int Jahr { get; init; }

    /// <summary>
    /// Lief der Betrieb das ganze Jahr? Nein im Jahr, in dem er gekauft, gepachtet oder fertig gebaut wurde (außer genau zum Jahresbeginn),
    /// und im Jahr, in dem ein alter Spielstand ohne Betriebsrechnung geladen wurde. Dann ist der Gewinn kein gutes Urteil.
    /// </summary>
    public bool Ganzjaehrig { get; init; }

    public double Umsatz { get; internal set; }
    public double Zutaten { get; internal set; }
    public double Personal { get; internal set; }
    public double Fixkosten { get; internal set; }
    public double Steuer { get; internal set; }
    public double Schaden { get; internal set; }
    public double Transport { get; internal set; }

    /// <summary>Gewinn des Betriebs: Umsatz minus Zutaten, Personal, Unterhalt bzw. Pacht, Steuer, Schaden und Transport.</summary>
    public double Gewinn => Umsatz - Zutaten - Personal - Fixkosten - Steuer - Schaden - Transport;

    internal BetriebsrechnungStand Sichern() => new()
    {
        Jahr = Jahr, Ganzjaehrig = Ganzjaehrig, Umsatz = Umsatz, Zutaten = Zutaten, Personal = Personal, Fixkosten = Fixkosten,
        Steuer = Steuer, Schaden = Schaden, Transport = Transport,
    };

    internal static Betriebsrechnung Laden(BetriebsrechnungStand s) => new()
    {
        Jahr = s.Jahr, Ganzjaehrig = s.Ganzjaehrig, Umsatz = s.Umsatz, Zutaten = s.Zutaten, Personal = s.Personal, Fixkosten = s.Fixkosten,
        Steuer = s.Steuer, Schaden = s.Schaden, Transport = s.Transport,
    };
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
    private BetriebsartDaten _art;
    private readonly Dictionary<string, int> _belegung = new();
    private readonly SortedDictionary<string, int> _lager = new(StringComparer.Ordinal); // sortiert: gleiche Reihenfolge nach Entnehmen und nach dem Laden
    private readonly List<Karren> _karren = new();
    private double _fortschritt;        // Werkstatt: Arbeitsminuten am aktuellen Stück
    private double _fortschrittEisen;   // Umland: erster Rohstoff (Name aus alten Spielständen)
    private double _fortschrittSilber;  // Umland: zweiter Rohstoff
    private string _produkt;
    private double _silberanteil = 0.5;
    private string? _letzterStillstand;
    private int _sabotageMinuten;
    private int _umbauMinuten;

    public string Name { get; }

    /// <summary>Die Art – ändert sich nur durch Ausbau (Gießerei → Schmiede → Goldschmiede).</summary>
    public Betriebsart Art { get; private set; }
    public Konto Konto { get; }

    /// <summary>Was diese Betriebsart ist und kann (aus den Daten).</summary>
    public BetriebsartDaten Artdaten => _art;

    /// <summary>Gebäudestufe 1–3.</summary>
    public int Stufe => _art.Stufe;

    /// <summary>Arbeitsminuten, die der Umbau zur neuen Stufe noch dauert (0 = fertig).</summary>
    public int UmbauMinuten => _umbauMinuten;

    public Arbeitsort Ort => _art.Ort;

    public int Plaetze => _art.Plaetze;

    /// <summary>Werkstatt (stellt Produkte her) – sonst Umlandbetrieb (fördert Rohstoffe) oder ein Betrieb ohne Ware.</summary>
    public bool IstWerkstatt => _art.IstWerkstatt;

    /// <summary>Ein Gebäude in der Stadt (Werkstatt oder ein Betrieb ohne Ware wie das Schmugglerloch) – mit Gebäudestufen.</summary>
    public bool InDerStadt => _art.InDerStadt;

    /// <summary>Stellt nichts her und fördert nichts (Dieb): Die Arbeitsminuten sammeln sich in <see cref="Fortschritt"/>.</summary>
    public bool OhneProduktion => _art.OhneProduktion;

    /// <summary>Ohne Produktion: gesammelte Arbeitsminuten (Leistung) seit der letzten vollen Stunde.</summary>
    public double Fortschritt => _fortschritt;

    /// <summary>Holt die gesammelten Arbeitsminuten ab (für das Teilsystem, das sie verwertet).</summary>
    internal double ArbeitsminutenAbholen()
    {
        var minuten = _fortschritt;
        _fortschritt = 0;
        return minuten;
    }

    /// <summary>Umland: die geförderten Rohstoffe (eine oder zwei Waren).</summary>
    public IReadOnlyList<string> Rohstoffe => _art.Rohstoffe;

    public IReadOnlyDictionary<string, int> Belegung => _belegung;
    public int Belegt => _belegung.Values.Sum();

    public IReadOnlyDictionary<string, int> Lager => _lager;
    public int LagerKapazitaetJeWare => _daten.Logistik.LagerJeWare;
    public int ImLager(string ware) => _lager.GetValueOrDefault(ware);

    public IReadOnlyList<Karren> Karren => _karren;

    /// <summary>Ein Wächter halbiert die Erfolgschance von Diebstahl und Sabotage und sieht öfter, wer es war.</summary>
    public bool Waechter { get; internal set; }

    /// <summary>Arbeitsminuten, die nach einer Sabotage noch repariert werden muss (0 = alles heil).</summary>
    public int SabotageMinuten => _sabotageMinuten;

    /// <summary>Warum die Produktion gerade stillsteht (null = sie läuft).</summary>
    public string? Stillstand { get; private set; }

    /// <summary>Arbeitsminuten mit Stillstand im laufenden Jahr.</summary>
    public int StillstandMinuten { get; private set; }

    /// <summary>Arbeitsminuten mit Stillstand im Vorjahr – zeigt, ob Karren oder Lager zu knapp sind.</summary>
    public int StillstandMinutenVorjahr { get; private set; }

    /// <summary>Was die Werkstatt herstellt – eines ihrer <see cref="BetriebsartDaten.Produkte"/>.</summary>
    public string Produkt
    {
        get => _produkt;
        set
        {
            if (!IstWerkstatt)
                throw new InvalidOperationException($"Eine {_art.Name} stellt nichts her, sie fördert.");
            if (!_art.Produkte.Contains(value) || !_daten.Waren.ContainsKey(value))
                throw new ArgumentException($"„{value}“ kann eine {_art.Name} nicht herstellen.");
            if (value != _produkt) _fortschritt = 0;
            _produkt = value;
        }
    }

    /// <summary>
    /// Umland mit zwei Rohstoffen: Anteil der Arbeitszeit für den zweiten (Grube: 0 = nur Eisen, 1 = nur Silber).
    /// Bei nur einem Rohstoff ohne Bedeutung.
    /// </summary>
    public double Silberanteil
    {
        get => _silberanteil;
        set => _silberanteil = Math.Clamp(value, 0, 1);
    }

    /// <summary>Waren, die dieser Betrieb als Zutaten braucht (nur Werkstätten).</summary>
    public IReadOnlyDictionary<string, int> Zutaten =>
        IstWerkstatt ? _daten.Waren[_produkt].Zutaten : KeineZutaten;

    private static readonly Dictionary<string, int> KeineZutaten = new();

    /// <summary>Für wie viele Stück die Zutaten im Lager noch reichen (Mine: unendlich).</summary>
    public int ZutatenReichweite =>
        Zutaten.Count == 0 ? int.MaxValue : Zutaten.Min(z => ImLager(z.Key) / z.Value);

    /// <param name="ganzjaehrig">Eröffnet genau zum Jahresbeginn? Dann zählt die erste Betriebsrechnung als ganzes Jahr.</param>
    internal Betrieb(string name, Betriebsart art, Konto konto, Wirtschaftsdaten daten, bool ganzjaehrig = false)
    {
        Name = name;
        Art = art;
        Konto = konto;
        _daten = daten;
        _art = daten.Art(art);
        _produkt = _art.Standardprodukt ?? _art.Produkte.LastOrDefault() ?? "";
        Laufend = new Betriebsrechnung { Jahr = konto.Laufend.Jahr, Ganzjaehrig = ganzjaehrig };
    }

    // ── Rechnung des Betriebs ───────────────────────────────────────────────

    /// <summary>Die Rechnung des laufenden Jahres.</summary>
    public Betriebsrechnung Laufend { get; private set; }

    /// <summary>Die abgeschlossene Rechnung des Vorjahres (null im ersten Jahr des Betriebs). Gewinn: <c>Vorjahr.Gewinn</c>.</summary>
    public Betriebsrechnung? Vorjahr { get; private set; }

    // Buchungen: aufs Konto des Hauses und zugleich in die Rechnung dieses Betriebs.
    internal void UmsatzBuchen(double betrag) { Konto.Umsatz(betrag); Laufend.Umsatz += betrag; }
    internal void ZutatenBuchen(double betrag) { Konto.Zutaten(betrag); Laufend.Zutaten += betrag; }
    internal void PersonalBuchen(double betrag) { Konto.Personal(betrag); Laufend.Personal += betrag; }
    internal void FixkostenBuchen(double betrag) { Konto.Fixkosten(betrag); Laufend.Fixkosten += betrag; }
    internal void SteuerBuchen(double betrag) { Konto.Steuer(betrag); Laufend.Steuer += betrag; }
    internal void SchadenBuchen(double betrag) { Konto.Schaden(betrag); Laufend.Schaden += betrag; }
    internal void TransportBuchen(double betrag) { Konto.Transport(betrag); Laufend.Transport += betrag; }

    /// <summary>Jahreswechsel (mit dem Konto): Die laufende Rechnung wird zum Vorjahr.</summary>
    internal void JahrAbschliessen(int neuesJahr)
    {
        Vorjahr = Laufend;
        Laufend = new Betriebsrechnung { Jahr = neuesJahr, Ganzjaehrig = true };
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

    private readonly SortedSet<string> _zurueckgehalten = new(StringComparer.Ordinal);

    /// <summary>Waren, die der Karren nicht von selbst verkauft (Handel von Hand: horten, später verkaufen).</summary>
    public IReadOnlyCollection<string> Zurueckgehalten => _zurueckgehalten;

    public bool HaeltZurueck(string ware) => _zurueckgehalten.Contains(ware);

    internal void Zurueckhalten(string ware, bool an)
    {
        if (an) _zurueckgehalten.Add(ware);
        else _zurueckgehalten.Remove(ware);
    }

    internal int Auslagern(string ware, int hoechstens)
    {
        var menge = Math.Min(hoechstens, ImLager(ware));
        if (menge <= 0) return 0;
        _lager[ware] = ImLager(ware) - menge;
        if (_lager[ware] == 0) _lager.Remove(ware);
        return menge;
    }

    /// <summary>
    /// Waren im Lager, die zum Markt dürfen: alles außer den eigenen Zutaten – und in einer Schänke
    /// ohne den Vorrat, den der Wirt für seine Gäste zurückbehält.
    /// </summary>
    internal IEnumerable<(string Ware, int Stueck)> Verkaufsware() =>
        _lager.Where(l => !Zutaten.ContainsKey(l.Key) && !_zurueckgehalten.Contains(l.Key))
            .Select(l => (l.Key, l.Value - FuerGaeste(l.Key)))
            .Where(x => x.Item2 > 0);

    /// <summary>Wie viel von dieser Ware die Schänke für ihre Gäste im Lager behält (0 bei allen anderen Betrieben).</summary>
    public int FuerGaeste(string ware) =>
        IstSchaenke && _daten.Gastwirtschaft!.Ausschank.Contains(ware) ? _daten.Gastwirtschaft.Vorrat : 0;

    /// <summary>Eine Werkstatt des Wirts (Schänke, Wirtshaus, Gasthaus): Hier kehren Gäste ein.</summary>
    public bool IstSchaenke => IstWerkstatt && _daten.Gastwirtschaft is { } g && _art.Beruf == g.Beruf;

    // ── Produktion ──────────────────────────────────────────────────────────

    /// <summary>Arbeitsminuten, die die Belegschaft in einer Spielminute schafft.</summary>
    public double LeistungProMinute =>
        _belegung.Sum(b => b.Value * _daten.Arbeitskraefte[b.Key].TempoAm(Ort));

    /// <summary>Eine Spielminute Arbeit. Wird nur während der Arbeitszeit aufgerufen.</summary>
    /// <param name="leistungsFaktor">Handwerkskunst des Oberhaupts: 1,09 = die Arbeitskräfte schaffen 9 % mehr.</param>
    /// <param name="leistungsFaktor">Handwerkskunst des Oberhaupts, Krankenstand, Völkerbonus – gilt für Betriebe mit Ware.</param>
    /// <param name="volksFaktor">Nur der Völkerbonus – gilt auch für Betriebe ohne Ware (Diebe, Wache, Schreiber).</param>
    internal void Arbeitsminute(Action<string> melden, double leistungsFaktor = 1, double volksFaktor = 1)
    {
        if (_umbauMinuten > 0 || _sabotageMinuten > 0)
        {
            // Während des Umbaus bzw. nach einem Anschlag wird gebaut statt gearbeitet.
            string grund;
            var umbau = _umbauMinuten > 0;
            if (umbau)
            {
                _umbauMinuten--;
                grund = $"Umbau (Stufe {_art.Stufe}: {_art.Name}) – die Arbeit ruht.";
            }
            else
            {
                _sabotageMinuten--;
                grund = "Nach dem Anschlag wird repariert – die Arbeit ruht.";
            }
            if (LeistungProMinute <= 0) return;
            if (_letzterStillstand != grund) melden($"{Name}: {grund}");
            _letzterStillstand = grund;
            Stillstand = grund;
            if (!umbau) StillstandMinuten++; // ein gewollter Umbau sagt nichts über zu knappe Karren oder Lager
            return;
        }

        var leistung = LeistungProMinute * leistungsFaktor;
        if (leistung <= 0) return;

        if (OhneProduktion)
        {
            // Diebe verdienen nicht nach Handwerkskunst – gezählt wird die Arbeitszeit der Leute, mit dem Geschick ihres Volkes.
            _fortschritt += LeistungProMinute * volksFaktor;
            Konto.ErfahrungSammeln(_art.Beruf, LeistungProMinute * volksFaktor / 60);
            Stillstand = null;
            _letzterStillstand = null;
            return;
        }

        string? stillstand = null;
        if (IstWerkstatt)
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
                Konto.ErfahrungSammeln(_art.Beruf, zeit / 60);
                _fortschritt -= zeit;
            }
        }
        else if (Rohstoffe.Count == 1)
        {
            _fortschrittEisen += leistung;
            stillstand = Foerdern(Rohstoffe[0], ref _fortschrittEisen);
        }
        else if (Rohstoffe.Count >= 2)
        {
            _fortschrittEisen += leistung * (1 - _silberanteil);
            _fortschrittSilber += leistung * _silberanteil;
            // Beide Waren getrennt fördern: Ist das eine Lager voll, geht die Förderung des anderen weiter.
            var erster = Foerdern(Rohstoffe[0], ref _fortschrittEisen);
            var zweiter = Foerdern(Rohstoffe[1], ref _fortschrittSilber);
            stillstand = erster ?? zweiter;
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
            Konto.ErfahrungSammeln(_art.Beruf, zeit / 60);
            fortschritt -= zeit;
        }
        return null;
    }

    /// <summary>
    /// Ausbau am Platz zur nächsten Stufe: Ab sofort gelten Plätze, Produkte und Unterhalt der neuen Art;
    /// das Produkt bleibt (die höhere Stufe kann alles, was die niedrigere konnte). Bis der Umbau fertig ist, ruht die Arbeit.
    /// </summary>
    internal void Ausbauen(Betriebsart neu, int umbauMinuten)
    {
        Art = neu;
        _art = _daten.Art(neu);
        _umbauMinuten += umbauMinuten;
        if (_art.IstWerkstatt && !_art.Produkte.Contains(_produkt)) // Betriebe ohne Ware (Dieb, Wache, Geldleiher) haben kein Produkt
            _produkt = _art.Standardprodukt ?? _art.Produkte.Last();
    }

    /// <summary>Sabotage: So viele Arbeitsminuten muss repariert werden (zwei Anschläge addieren sich).</summary>
    internal void Sabotieren(int arbeitsminuten) => _sabotageMinuten += arbeitsminuten;

    /// <summary>Verkauft ein Stück auf dem Markt und verbucht Umsatz und Steuer.</summary>
    /// <param name="verkaufsFaktor">Verhandlungsgeschick des Oberhaupts: 1,03 = 3 % mehr Erlös.</param>
    internal void Verkaufen(string ware, int stueck, Markt markt, double verkaufsFaktor = 1)
    {
        if (stueck <= 0) return;
        var erloes = markt[ware].Verkaufen(stueck) * verkaufsFaktor;
        UmsatzBuchen(erloes);
        SteuerBuchen(erloes * _daten.Allgemein.Umsatzsteuer);
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    internal BetriebStand Sichern(int kontoNummer) => new()
    {
        Name = Name, Art = Art, Konto = kontoNummer,
        Belegung = new(_belegung), Lager = new(_lager), Produkt = _produkt, Silberanteil = _silberanteil,
        Fortschritt = _fortschritt, FortschrittEisen = _fortschrittEisen, FortschrittSilber = _fortschrittSilber,
        Stillstand = Stillstand, LetzterStillstand = _letzterStillstand,
        StillstandMinuten = StillstandMinuten, StillstandMinutenVorjahr = StillstandMinutenVorjahr,
        Karren = _karren.Select(k => k.Sichern()).ToList(),
        Zurueckgehalten = _zurueckgehalten.Count > 0 ? _zurueckgehalten.ToList() : null,
        Waechter = Waechter, SabotageMinuten = _sabotageMinuten, UmbauMinuten = _umbauMinuten,
        Rechnung = Laufend.Sichern(), RechnungVorjahr = Vorjahr?.Sichern(),
    };

    internal void Wiederherstellen(BetriebStand s)
    {
        foreach (var (kraft, anzahl) in s.Belegung) _belegung[kraft] = anzahl;
        foreach (var (ware, stueck) in s.Lager) _lager[ware] = stueck;
        _produkt = s.Produkt;
        _silberanteil = s.Silberanteil;
        _fortschritt = s.Fortschritt;
        _fortschrittEisen = s.FortschrittEisen;
        _fortschrittSilber = s.FortschrittSilber;
        Stillstand = s.Stillstand;
        _letzterStillstand = s.LetzterStillstand;
        StillstandMinuten = s.StillstandMinuten;
        StillstandMinutenVorjahr = s.StillstandMinutenVorjahr;
        Waechter = s.Waechter;
        _sabotageMinuten = s.SabotageMinuten;
        _umbauMinuten = s.UmbauMinuten;
        if (s.Rechnung is { } r) Laufend = Betriebsrechnung.Laden(r); // alte Spielstände: ab dem Laden, kein ganzes Jahr
        Vorjahr = s.RechnungVorjahr is { } v ? Betriebsrechnung.Laden(v) : null;
        foreach (var ware in s.Zurueckgehalten ?? new()) _zurueckgehalten.Add(ware);
        foreach (var karrenStand in s.Karren)
        {
            if (!_daten.Logistik.Karren.TryGetValue(karrenStand.Typ, out var daten))
                throw new InvalidDataException($"Den Karrentyp „{karrenStand.Typ}“ gibt es in den Daten nicht.");
            var karren = new Karren(karrenStand.Name, karrenStand.Typ, daten, this);
            karren.Wiederherstellen(karrenStand);
            _karren.Add(karren);
        }
    }

    // ── Jahresende ──────────────────────────────────────────────────────────

    /// <summary>Löhne, Unterhalt bzw. Pacht und zufällige Ausbrüche oder Aufstände.</summary>
    internal void Jahresabrechnung(Zufallsquelle zufall, Action<string> melden)
    {
        foreach (var (name, anzahl) in _belegung)
        {
            var kraft = _daten.Arbeitskraefte[name];
            PersonalBuchen(anzahl * kraft.Kosten);

            // Jede unfreie Arbeitskraft hat ihr eigenes Risiko.
            for (var i = 0; i < anzahl; i++)
            {
                if (kraft.Risiko > 0 && zufall.NextDouble() < kraft.Risiko)
                {
                    SchadenBuchen(kraft.Schaden);
                    melden(Ort == Arbeitsort.Umland
                        ? $"Ausbruch in {Name}: Ein {name} hat sich losgerissen. Schaden: {Zahlenformat.Ganz(kraft.Schaden)} {_daten.Allgemein.Waehrung}."
                        : $"Aufstand in {Name}: Ein {name} hat sich erhoben. Schaden: {Zahlenformat.Ganz(kraft.Schaden)} {_daten.Allgemein.Waehrung}.");
                }
            }
        }

        FixkostenBuchen(_art.Fixkosten);
        if (Waechter && _daten.Gauner is { } gauner)
            PersonalBuchen(gauner.WaechterKosten);

        StillstandMinutenVorjahr = StillstandMinuten;
        StillstandMinuten = 0;
    }
}
