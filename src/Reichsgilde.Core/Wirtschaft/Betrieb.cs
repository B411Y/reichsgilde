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

    /// <summary>
    /// Was Diebe aus diesem Betrieb geholt haben: Marktwert der Lagerware, beim Geldleiher die Gulden aus der Kasse. Nur zur Information
    /// (schon aus Lager bzw. Kasse verschwunden, kein Posten des Gewinns) – Grundlage für den Wert der Eisentür.
    /// </summary>
    public double Raub { get; internal set; }

    /// <summary>
    /// Was gelungene Anschläge (Feuer) diesen Betrieb gekostet haben: Reparatur und Stillstand, geschätzt beim Anschlag
    /// (<see cref="Gauner.Unterwelt.Sabotageschaden"/>). Nur zur Information – Grundlage für den Wert des Löschfasses.
    /// </summary>
    public double Brandschaden { get; internal set; }

    /// <summary>Gewinn des Betriebs: Umsatz minus Zutaten, Personal, Unterhalt bzw. Pacht, Steuer, Schaden und Transport.</summary>
    public double Gewinn => Umsatz - Zutaten - Personal - Fixkosten - Steuer - Schaden - Transport;

    internal BetriebsrechnungStand Sichern() => new()
    {
        Jahr = Jahr, Ganzjaehrig = Ganzjaehrig, Umsatz = Umsatz, Zutaten = Zutaten, Personal = Personal, Fixkosten = Fixkosten,
        Steuer = Steuer, Schaden = Schaden, Transport = Transport, Raub = Raub, Brandschaden = Brandschaden,
    };

    internal static Betriebsrechnung Laden(BetriebsrechnungStand s) => new()
    {
        Jahr = s.Jahr, Ganzjaehrig = s.Ganzjaehrig, Umsatz = s.Umsatz, Zutaten = s.Zutaten, Personal = s.Personal, Fixkosten = s.Fixkosten,
        Steuer = s.Steuer, Schaden = s.Schaden, Transport = s.Transport, Raub = s.Raub, Brandschaden = s.Brandschaden,
    };
}

/// <summary>
/// Ein Betrieb mit Arbeitskräften, Lager und Karren.
///
/// Ablauf:
///   • Während der Arbeitszeit produzieren die Arbeitskräfte. Zutaten kommen aus dem Lager,
///     fertige Waren gehen ins Lager.
///   • Jeder Arbeiter ist ein eigener Eintrag (<see cref="Arbeiter"/>) mit eigener Ware: Eine Werkstatt stellt
///     mehrere Waren zugleich her, jede mit ihrem eigenen Fortschritt. Wer keine Ware zugeteilt hat, arbeitet am <see cref="Produkt"/>.
///   • Fehlen Zutaten oder ist das Lager voll, steht die Produktion dieser Ware still.
///   • Die Karren bringen Waren zum Markt und Zutaten zurück (siehe <see cref="Karren"/>).
/// </summary>
public sealed class Betrieb
{
    private readonly Wirtschaftsdaten _daten;
    private BetriebsartDaten _art;
    private readonly List<Arbeiter> _arbeiter = new();
    private int _naechsteNummer = 1;
    private readonly Dictionary<string, int> _belegung = new(); // aus _arbeiter abgeleitet (Art → Anzahl)
    private readonly SortedDictionary<string, int> _lager = new(StringComparer.Ordinal); // sortiert: gleiche Reihenfolge nach Entnehmen und nach dem Laden
    private readonly List<Karren> _karren = new();
    private readonly SortedDictionary<string, double> _fortschrittJeWare = new(StringComparer.Ordinal); // Werkstatt: Arbeitsminuten am Stück je Ware
    private readonly SortedDictionary<string, double> _leistungJeWare = new(StringComparer.Ordinal);    // nur innerhalb einer Arbeitsminute
    private readonly SortedSet<string> _einrichtung = new(StringComparer.Ordinal);
    private List<string> _warenInArbeit = new();
    private IReadOnlyDictionary<string, int> _zutaten = KeineZutaten;
    private readonly Func<long> _jetzt;  // die Uhr der Stadt (Spielminuten seit Spielbeginn)
    private double _lohnFaktorSumme;     // Lohnstufe über die Arbeitsminuten des Jahres – wer kurz vor der Abrechnung knausert, spart nichts
    private int _lohnMinuten;
    private int _lagerPlus;              // Regale: so viel mehr Lager je Ware
    private double _fortschritt;        // ohne Produktion: gesammelte Arbeitsminuten
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

    /// <summary>Arbeitsminuten je Spieljahr (Tag) in diesem Betrieb: 720 = 8–20 Uhr, im Wirtshaus 840 = 8–22 Uhr.</summary>
    public int Arbeitsminuten => _daten.Arbeitsminuten(Art);

    /// <summary>Feierabend in Minuten nach Mitternacht (20:00 = 1200, im Wirtshaus 22:00 = 1320).</summary>
    public int Arbeitsende => Stadtwirtschaft.Arbeitsbeginn + Arbeitsminuten;

    /// <summary>Ist zu dieser Uhrzeit (Minuten nach Mitternacht) Arbeitszeit in diesem Betrieb?</summary>
    public bool IstArbeitszeit(int uhrzeitInMinuten) => uhrzeitInMinuten >= Stadtwirtschaft.Arbeitsbeginn && uhrzeitInMinuten < Arbeitsende;

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

    /// <summary>Wie viele Arbeitskräfte jeder Art hier arbeiten (aus <see cref="Arbeiter"/> gezählt, auch wer nach einem Unfall fehlt).</summary>
    public IReadOnlyDictionary<string, int> Belegung => _belegung;
    public int Belegt => _arbeiter.Count;

    /// <summary>Die einzelnen Arbeiter in der Reihenfolge, in der sie eingestellt wurden.</summary>
    public IReadOnlyList<Arbeiter> Arbeiter => _arbeiter;

    /// <summary>Der Arbeiter mit dieser Nummer (null, wenn es ihn hier nicht gibt).</summary>
    public Arbeiter? ArbeiterNr(int nummer) => _arbeiter.FirstOrDefault(a => a.Nummer == nummer);

    /// <summary>Woran der Arbeiter arbeitet: seine zugeteilte Ware oder die Ware des Betriebs.</summary>
    public string WareVon(Arbeiter a) => a.Ware ?? _produkt;

    public IReadOnlyDictionary<string, int> Lager => _lager;

    /// <summary>Wie viel von jeder Ware ins Lager passt – 40 laut Daten, mit Regalen mehr.</summary>
    public int LagerKapazitaetJeWare => _daten.Logistik.LagerJeWare + _lagerPlus;
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

    /// <summary>
    /// Die Ware des Betriebs – eines seiner <see cref="BetriebsartDaten.Produkte"/>: Daran arbeitet, wem keine eigene Ware zugeteilt ist.
    /// Setzen (Befehl <see cref="ProduktWaehlen"/>) teilt alle Arbeiter dieser Ware zu. Der Fortschritt anderer Waren bleibt liegen.
    /// </summary>
    public string Produkt
    {
        get => _produkt;
        set
        {
            if (!IstWerkstatt)
                throw new InvalidOperationException($"Eine {_art.Name} stellt nichts her, sie fördert.");
            if (!_art.Produkte.Contains(value) || !_daten.Waren.ContainsKey(value))
                throw new ArgumentException($"„{value}“ kann eine {_art.Name} nicht herstellen.");
            _produkt = value;
            foreach (var a in _arbeiter) a.Ware = null;
            PersonalGeaendert();
        }
    }

    /// <summary>Die Waren, an denen gerade jemand arbeitet (sortiert; ohne Personal nur die Ware des Betriebs).</summary>
    public IReadOnlyList<string> WarenInArbeit => _warenInArbeit;

    /// <summary>Werkstatt: Anteil jeder Ware an der Arbeit (nach dem Tempo der zugeteilten Arbeitskräfte; ohne Personal die Ware des Betriebs ganz).</summary>
    public IReadOnlyDictionary<string, double> WarenAnteile
    {
        get
        {
            var anteile = new SortedDictionary<string, double>(StringComparer.Ordinal);
            if (!IstWerkstatt) return anteile;
            foreach (var a in _arbeiter)
                anteile[WareVon(a)] = anteile.GetValueOrDefault(WareVon(a)) + _daten.Arbeitskraefte[a.Arbeitskraft].TempoAm(Ort);
            var summe = anteile.Values.Sum();
            if (summe <= 0) return new SortedDictionary<string, double>(StringComparer.Ordinal) { [_produkt] = 1 };
            foreach (var ware in anteile.Keys.ToList()) anteile[ware] /= summe;
            return anteile;
        }
    }

    /// <summary>Werkstatt: Arbeitsminuten, die am angefangenen Stück dieser Ware schon getan sind.</summary>
    public double FortschrittVon(string ware) => _fortschrittJeWare.GetValueOrDefault(ware);

    /// <summary>Arbeitsminuten (eines Gesellen) je Stück in dieser Werkstatt – mit der Wirkung der Einrichtung.</summary>
    public double MinutenJeStueck(string ware) => _daten.Waren[ware].Zeit * ZeitFaktor(ware);

    // ── Einrichtung ─────────────────────────────────────────────────────────

    /// <summary>Die gekauften Stücke (Kennungen, sortiert).</summary>
    public IReadOnlyCollection<string> Einrichtung => _einrichtung;

    public bool HatEinrichtung(string kennung) => _einrichtung.Contains(kennung);

    /// <summary>Die gekauften Stücke mit ihren Daten (Stücke, die es in den Daten nicht mehr gibt, wirken nicht).</summary>
    public IEnumerable<EinrichtungDaten> Einrichtungsstuecke =>
        _einrichtung.Count == 0 || _daten.Werkstatt is not { } w ? Enumerable.Empty<EinrichtungDaten>()
            : w.EinrichtungFuer(_art.Beruf).Where(e => _einrichtung.Contains(e.Kennung));

    /// <summary>Faktor der Einrichtung auf die Zeit je Stück dieser Ware (1 = ohne Wirkung).</summary>
    public double ZeitFaktor(string ware)
    {
        var faktor = 1.0;
        foreach (var e in Einrichtungsstuecke)
            if (e.Wirkung.GiltFuer(ware)) faktor *= e.Wirkung.ZeitFaktor;
        return faktor;
    }

    /// <summary>Faktor auf die Unfallgefahr (Schürzen: 0,5).</summary>
    public double UnfallFaktor => Einrichtungsstuecke.Aggregate(1.0, (f, e) => f * e.Wirkung.UnfallFaktor);

    /// <summary>Faktor auf die Erfolgschance von Feuer – Sabotage und Brand (Löschfass).</summary>
    public double BrandFaktor => Einrichtungsstuecke.Aggregate(1.0, (f, e) => f * e.Wirkung.BrandFaktor);

    /// <summary>Faktor auf die Erfolgschance von Diebstahl und Einbruch ins Lager (Eisentür).</summary>
    public double EinbruchFaktor => Einrichtungsstuecke.Aggregate(1.0, (f, e) => f * e.Wirkung.EinbruchFaktor);

    /// <summary>Dieb: so viel höher die Erfolgschance der eigenen Einbrüche (Dietriche: +0,1, mit Meisterdietrichen +0,2).</summary>
    public double EinbruchBonus => Einrichtungsstuecke.Sum(e => e.Wirkung.EinbruchBonus);

    /// <summary>Dieb: Faktor auf die Fangchance der Taschendiebe (Verkleidungstruhe: 0,75).</summary>
    public double FangFaktor => Einrichtungsstuecke.Aggregate(1.0, (f, e) => f * e.Wirkung.FangFaktor);

    /// <summary>Dieb: Faktor auf Zeugen und Beweise gegen das Haus (Lederhandschuhe: 0,75).</summary>
    public double SpurenFaktor => Einrichtungsstuecke.Aggregate(1.0, (f, e) => f * e.Wirkung.SpurenFaktor);

    /// <summary>Wache: so viel höher die Chance je Tat bei einer Razzia (Hellebarden: +0,1, mit Armbrüsten +0,2).</summary>
    public double RazziaBonus => Einrichtungsstuecke.Sum(e => e.Wirkung.RazziaBonus);

    /// <summary>Wache: Faktor auf den Schutz der Streife dieses Hauses (Wachhunde: 1,25).</summary>
    public double StreifenFaktor => Einrichtungsstuecke.Aggregate(1.0, (f, e) => f * e.Wirkung.StreifenFaktor);

    /// <summary>Wache: Faktor auf die Erfolgschance von Überfällen auf Karren, die dieses Haus geleitet (Kettenhemden: 0,8).</summary>
    public double GeleitFaktor => Einrichtungsstuecke.Aggregate(1.0, (f, e) => f * e.Wirkung.GeleitFaktor);

    /// <summary>Geldleiher: Faktor auf den Rahmen je Schreiber (Rechenbretter: 1,1, mit Hauptbüchern 1,21).</summary>
    public double RahmenFaktor => Einrichtungsstuecke.Aggregate(1.0, (f, e) => f * e.Wirkung.RahmenFaktor);

    /// <summary>Geldleiher: Faktor auf die Wechselgebühr (Goldwaage: 1,1).</summary>
    public double WechselFaktor => Einrichtungsstuecke.Aggregate(1.0, (f, e) => f * e.Wirkung.WechselFaktor);

    /// <summary>Geldleiher: Faktor auf den Verlust bei Ausfall eines Kunden (Pfandkammer: 0,75).</summary>
    public double AusfallFaktor => Einrichtungsstuecke.Aggregate(1.0, (f, e) => f * e.Wirkung.AusfallFaktor);

    /// <summary>Geldleiher: Faktor auf den Höchstbetrag der Kredite an Häuser (Kassenschrank: 1,25).</summary>
    public double KreditFaktor => Einrichtungsstuecke.Aggregate(1.0, (f, e) => f * e.Wirkung.KreditFaktor);

    internal void EinrichtungHinzufuegen(string kennung)
    {
        _einrichtung.Add(kennung);
        _lagerPlus = Einrichtungsstuecke.Sum(e => e.Wirkung.LagerPlus);
    }

    // ── Lohn und Laune ──────────────────────────────────────────────────────

    /// <summary>Wie gut der Betrieb zahlt (nur Werkstätten; sonst immer üblich).</summary>
    public Lohnstufe Lohnstufe { get; internal set; } = Lohnstufe.Ueblich;

    /// <summary>Faktor auf den Lohn bei der jetzigen Lohnstufe – mal der Einrichtung (Waffenkammer der Wache: 0,9).</summary>
    public double LohnFaktor => (HatWerkstattregeln ? _daten.Werkstatt!.Stufe(Lohnstufe).LohnFaktor : 1) * EinrichtungLohnFaktor;

    /// <summary>Faktor der Einrichtung auf die Lohnkosten (Waffenkammer der Wache: 0,9; sonst 1).</summary>
    public double EinrichtungLohnFaktor => Einrichtungsstuecke.Aggregate(1.0, (f, e) => f * e.Wirkung.LohnFaktor);

    /// <summary>Löhne für ein ganzes Jahr bei der jetzigen Belegung und Lohnstufe (ohne Wächter; mit der Länge der Schicht).</summary>
    public double Lohnkosten => _belegung.Sum(b => b.Value * _daten.Lohn(b.Key, Art)) * LohnFaktor;

    /// <summary>Nach einem Unfall ist die Laune aller hier bis zu dieser Spielminute gedrückt (null = kein Unfall).</summary>
    public long? UnfallLauneBis { get; internal set; }

    /// <summary>Gilt die Werkstatt wie in Gilde 1 (Laune, Lohnstufe, Unfälle, Einrichtung) für diesen Betrieb? Nur Werkstätten mit Ware.</summary>
    public bool HatWerkstattregeln => IstWerkstatt && _daten.Werkstatt is not null;

    /// <summary>Faktor der Laune auf das Tempo dieses Arbeiters (1 = neutral; ±20 % laut Daten). Nur in Werkstätten mit Ware.</summary>
    public double TempoFaktor(Arbeiter a)
    {
        if (!HatWerkstattregeln) return 1;
        var l = _daten.Werkstatt!.Laune;
        var abstand = a.Laune >= l.Neutral ? (a.Laune - l.Neutral) / (100 - l.Neutral) : (a.Laune - l.Neutral) / l.Neutral;
        return 1 + l.TempoBis * Math.Clamp(abstand, -1, 1);
    }

    private double NeutraleLaune => _daten.Werkstatt?.Laune.Neutral ?? 50;

    /// <summary>
    /// Umland mit zwei Rohstoffen: Anteil der Arbeitszeit für den zweiten (Grube: 0 = nur Eisen, 1 = nur Silber).
    /// Bei nur einem Rohstoff ohne Bedeutung.
    /// </summary>
    public double Silberanteil
    {
        get => _silberanteil;
        set => _silberanteil = Math.Clamp(value, 0, 1);
    }

    /// <summary>
    /// Waren, die dieser Betrieb als Zutaten braucht (nur Werkstätten): Menge je Stück, über alle <see cref="WarenInArbeit"/> zusammengezählt
    /// (eine Runde, in der von jeder Ware in Arbeit ein Stück entsteht).
    /// </summary>
    public IReadOnlyDictionary<string, int> Zutaten => _zutaten;

    private static readonly Dictionary<string, int> KeineZutaten = new();

    /// <summary>Für wie viele Stück die Zutaten im Lager noch reichen (Mine: unendlich).</summary>
    public int ZutatenReichweite =>
        Zutaten.Count == 0 ? int.MaxValue : Zutaten.Min(z => ImLager(z.Key) / z.Value);

    /// <param name="ganzjaehrig">Eröffnet genau zum Jahresbeginn? Dann zählt die erste Betriebsrechnung als ganzes Jahr.</param>
    /// <param name="jetzt">Die Uhr der Stadt in Spielminuten (für Dienstzeit und Unfälle; ohne Angabe steht sie auf 0).</param>
    internal Betrieb(string name, Betriebsart art, Konto konto, Wirtschaftsdaten daten, bool ganzjaehrig = false, Func<long>? jetzt = null)
    {
        Name = name;
        Art = art;
        Konto = konto;
        _daten = daten;
        _art = daten.Art(art);
        _jetzt = jetzt ?? (() => 0);
        _produkt = _art.Standardprodukt ?? _art.Produkte.LastOrDefault() ?? "";
        Laufend = new Betriebsrechnung { Jahr = konto.Laufend.Jahr, Ganzjaehrig = ganzjaehrig };
        PersonalGeaendert();
    }

    /// <summary>Nach jeder Änderung am Personal oder an der Ware: Belegung, Waren in Arbeit und Zutaten neu zählen.</summary>
    private void PersonalGeaendert()
    {
        _belegung.Clear();
        foreach (var a in _arbeiter)
            _belegung[a.Arbeitskraft] = _belegung.GetValueOrDefault(a.Arbeitskraft) + 1;
        if (!IstWerkstatt)
        {
            _warenInArbeit = new List<string>();
            _zutaten = KeineZutaten;
            return;
        }
        _warenInArbeit = _arbeiter.Count == 0 ? new List<string> { _produkt }
            : _arbeiter.Select(WareVon).Distinct().OrderBy(w => w, StringComparer.Ordinal).ToList();
        if (_warenInArbeit.Count == 1)
        {
            _zutaten = _daten.Waren[_warenInArbeit[0]].Zutaten;
            return;
        }
        var summe = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var ware in _warenInArbeit)
            foreach (var (zutat, menge) in _daten.Waren[ware].Zutaten)
                summe[zutat] = summe.GetValueOrDefault(zutat) + menge;
        _zutaten = summe;
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
    internal void RaubBuchen(double wert) { Konto.Raub(wert); Laufend.Raub += wert; }
    internal void BrandBuchen(double schaden) => Laufend.Brandschaden += schaden;

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

        // Mehr: neue Arbeiter an der Ware des Betriebs. Weniger: die zuletzt eingestellten dieser Art gehen.
        var jetzt = _belegung.GetValueOrDefault(arbeitskraft);
        for (var i = jetzt; i < anzahl; i++)
            _arbeiter.Add(new Arbeiter(_naechsteNummer++, arbeitskraft, _jetzt(), NeutraleLaune));
        for (var i = jetzt; i > anzahl; i--)
            _arbeiter.RemoveAt(_arbeiter.FindLastIndex(a => a.Arbeitskraft == arbeitskraft));
        PersonalGeaendert();
    }

    /// <summary>Stellt einen Arbeiter ein (Statut und Plätze wie bei <see cref="Einstellen"/>) und teilt ihm gleich eine Ware zu.</summary>
    internal Arbeiter EinenEinstellen(string arbeitskraft, string? ware, Fronstatut statut)
    {
        Einstellen(arbeitskraft, _belegung.GetValueOrDefault(arbeitskraft) + 1, statut);
        var neu = _arbeiter[^1];
        if (ware is not null) Zuteilen(neu, ware);
        return neu;
    }

    /// <summary>Entlässt diesen einen Arbeiter (Kündigung, Entlassung, gefasster Taschendieb).</summary>
    internal void Entlassen(Arbeiter a)
    {
        _arbeiter.Remove(a);
        PersonalGeaendert();
    }

    /// <summary>Teilt dem Arbeiter eine Ware zu (null: wieder die Ware des Betriebs). Die Ware muss die Werkstatt herstellen können.</summary>
    internal void Zuteilen(Arbeiter a, string? ware)
    {
        if (ware is not null && (!IstWerkstatt || !_art.Produkte.Contains(ware)))
            throw new ArgumentException($"„{ware}“ kann eine {_art.Name} nicht herstellen.");
        a.Ware = ware;
        PersonalGeaendert();
    }

    /// <summary>Beförderung: Der Arbeiter wird zu dieser Arbeitskraft (Ware, Laune und Nummer bleiben).</summary>
    internal void Befoerdern(Arbeiter a, string zu)
    {
        a.Arbeitskraft = zu;
        PersonalGeaendert();
    }

    /// <summary>Entlässt alle Arbeitskräfte, die unter dem Statut nicht erlaubt sind.</summary>
    internal List<(string Kraft, int Anzahl)> UnerlaubteEntlassen(Fronstatut statut)
    {
        var entlassen = _belegung
            .Where(b => !_daten.Arbeitskraefte[b.Key].IstErlaubt(statut, Ort))
            .Select(b => (b.Key, b.Value))
            .ToList();
        if (entlassen.Count == 0) return entlassen;
        _arbeiter.RemoveAll(a => entlassen.Any(e => e.Item1 == a.Arbeitskraft));
        PersonalGeaendert();
        return entlassen;
    }

    // ── Karren ──────────────────────────────────────────────────────────────

    /// <summary>Stellt einen Karren in Dienst, ohne ihn zu bezahlen (Startausstattung).</summary>
    internal Karren KarrenHinzufuegen(string typ)
    {
        if (!_daten.Logistik.Karren.TryGetValue(typ, out var daten))
            throw new ArgumentException($"Den Karrentyp „{typ}“ gibt es nicht.");
        // Nummer wie bisher die Zahl der Karren + 1 – nach einem Verkauf die nächste freie, damit kein Name doppelt vorkommt.
        var nummer = _karren.Count + 1;
        while (_karren.Any(k => k.Name == $"{typ} {nummer}")) nummer++;
        var karren = new Karren($"{typ} {nummer}", typ, daten, this);
        _karren.Add(karren);
        return karren;
    }

    /// <summary>Gibt einen verkauften Karren ab (geprüft und gebucht hat die Stadtwirtschaft).</summary>
    internal void KarrenEntfernen(Karren karren) => _karren.Remove(karren);

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

    /// <summary>Arbeitsminuten, die die Belegschaft in einer Spielminute schafft (wer nach einem Unfall fehlt, zählt nicht; mit der Laune).</summary>
    public double LeistungProMinute
    {
        get
        {
            var jetzt = _jetzt();
            var summe = 0.0;
            foreach (var a in _arbeiter)
                if (!a.FaelltAus(jetzt))
                    summe += _daten.Arbeitskraefte[a.Arbeitskraft].TempoAm(Ort) * TempoFaktor(a);
            return summe;
        }
    }

    /// <summary>Eine Spielminute Arbeit. Wird nur während der Arbeitszeit aufgerufen.</summary>
    /// <param name="leistungsFaktor">Handwerkskunst des Oberhaupts, Krankenstand, Völkerbonus – gilt für Betriebe mit Ware.</param>
    /// <param name="volksFaktor">Nur der Völkerbonus – gilt auch für Betriebe ohne Ware (Diebe, Wache, Schreiber).</param>
    internal void Arbeitsminute(Action<string> melden, double leistungsFaktor = 1, double volksFaktor = 1)
    {
        _lohnFaktorSumme += LohnFaktor; // für den Lohn am Jahresende: die Lohnstufe im Mittel der Arbeitszeit
        _lohnMinuten++;

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

        var leistungProMinute = LeistungProMinute;
        var leistung = leistungProMinute * leistungsFaktor;
        if (leistung <= 0) return;

        if (OhneProduktion)
        {
            // Diebe verdienen nicht nach Handwerkskunst – gezählt wird die Arbeitszeit der Leute, mit dem Geschick ihres Volkes.
            _fortschritt += leistungProMinute * volksFaktor;
            Konto.ErfahrungSammeln(_art.Beruf, leistungProMinute * volksFaktor / 60 * _art.ErfahrungFaktor);
            Stillstand = null;
            _letzterStillstand = null;
            return;
        }

        string? stillstand = null;
        if (IstWerkstatt)
        {
            // Jede Ware für sich: Die Leistung der Arbeiter, die an ihr arbeiten, geht in ihren eigenen Fortschritt.
            var jetzt = _jetzt();
            _leistungJeWare.Clear();
            foreach (var a in _arbeiter)
            {
                if (a.FaelltAus(jetzt)) continue;
                var ware = WareVon(a);
                _leistungJeWare[ware] = _leistungJeWare.GetValueOrDefault(ware) + _daten.Arbeitskraefte[a.Arbeitskraft].TempoAm(Ort) * TempoFaktor(a);
            }
            foreach (var (ware, anteil) in _leistungJeWare)
            {
                var grund = Herstellen(ware, anteil * leistungsFaktor);
                stillstand ??= grund;
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

    /// <summary>Werkstatt: so viel Arbeit an dieser Ware – fertige Stücke ins Lager. Gibt den Grund zurück, wenn sie stillsteht.</summary>
    private string? Herstellen(string ware, double leistung)
    {
        var zeit = MinutenJeStueck(ware);
        var fortschritt = _fortschrittJeWare.GetValueOrDefault(ware) + leistung;
        string? stillstand = null;
        while (fortschritt >= zeit)
        {
            stillstand = KannHerstellen(ware);
            if (stillstand is not null)
            {
                fortschritt = zeit; // wartet, statt Arbeit „anzusparen"
                break;
            }
            foreach (var (zutat, menge) in _daten.Waren[ware].Zutaten)
                Auslagern(zutat, menge);
            Einlagern(ware, 1);
            Konto.Hergestellt(ware);
            Konto.ErfahrungSammeln(_art.Beruf, zeit / 60 * _art.ErfahrungFaktor);
            fortschritt -= zeit;
        }
        _fortschrittJeWare[ware] = fortschritt;
        return stillstand;
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
            Konto.ErfahrungSammeln(_art.Beruf, zeit / 60 * _art.ErfahrungFaktor);
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
        PersonalGeaendert();
    }

    /// <summary>Sabotage: So viele Arbeitsminuten muss repariert werden (zwei Anschläge addieren sich).</summary>
    internal void Sabotieren(int arbeitsminuten) => _sabotageMinuten += arbeitsminuten;

    /// <summary>Verkauft ein Stück auf dem Markt und verbucht Umsatz und Steuer.</summary>
    /// <param name="verkaufsFaktor">Verhandlungsgeschick des Oberhaupts: 1,03 = 3 % mehr Erlös.</param>
    /// <param name="steuersatz">Steuersatz für dieses Haus (<see cref="Stadtwirtschaft.SteuersatzMarkt"/>); null = <c>allgemein.umsatzsteuer</c>.</param>
    internal void Verkaufen(string ware, int stueck, Markt markt, double verkaufsFaktor = 1, double? steuersatz = null)
    {
        if (stueck <= 0) return;
        var erloes = markt[ware].Verkaufen(stueck) * verkaufsFaktor;
        UmsatzBuchen(erloes);
        SteuerBuchen(erloes * (steuersatz ?? _daten.Allgemein.Umsatzsteuer));
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
        Arbeiter = _arbeiter.Select(a => a.Sichern()).ToList(), NaechsteArbeiterNummer = _naechsteNummer,
        FortschrittJeWare = new(_fortschrittJeWare), Lohnstufe = Lohnstufe,
        LohnFaktorSumme = _lohnFaktorSumme, LohnMinuten = _lohnMinuten,
        Einrichtung = _einrichtung.Count > 0 ? _einrichtung.ToList() : null, UnfallLauneBis = UnfallLauneBis,
    };

    internal void Wiederherstellen(BetriebStand s)
    {
        if (s.Arbeiter is { } arbeiter)
        {
            _arbeiter.AddRange(arbeiter.Select(Wirtschaft.Arbeiter.Laden));
            _naechsteNummer = Math.Max(s.NaechsteArbeiterNummer ?? 1, _arbeiter.Select(a => a.Nummer + 1).DefaultIfEmpty(1).Max());
        }
        else // alter Spielstand: nur die Zahl je Art – alle an der Ware des Betriebs, Laune neutral, Dienstzeit ab Spielbeginn
            foreach (var (kraft, anzahl) in s.Belegung)
                for (var i = 0; i < anzahl; i++)
                    _arbeiter.Add(new Arbeiter(_naechsteNummer++, kraft, 0, NeutraleLaune));
        foreach (var (ware, stueck) in s.Lager) _lager[ware] = stueck;
        _produkt = s.Produkt;
        _silberanteil = s.Silberanteil;
        _fortschritt = s.Fortschritt;
        if (s.FortschrittJeWare is { } jeWare)
            foreach (var (ware, minuten) in jeWare) _fortschrittJeWare[ware] = minuten;
        else if (IstWerkstatt) // alter Spielstand: Der Fortschritt gehörte der einen Ware
        {
            if (s.Fortschritt > 0) _fortschrittJeWare[_produkt] = s.Fortschritt;
            _fortschritt = 0;
        }
        _fortschrittEisen = s.FortschrittEisen;
        _fortschrittSilber = s.FortschrittSilber;
        Stillstand = s.Stillstand;
        _letzterStillstand = s.LetzterStillstand;
        StillstandMinuten = s.StillstandMinuten;
        StillstandMinutenVorjahr = s.StillstandMinutenVorjahr;
        Waechter = s.Waechter;
        _sabotageMinuten = s.SabotageMinuten;
        _umbauMinuten = s.UmbauMinuten;
        Lohnstufe = s.Lohnstufe ?? Lohnstufe.Ueblich;
        _lohnFaktorSumme = s.LohnFaktorSumme;
        _lohnMinuten = s.LohnMinuten;
        UnfallLauneBis = s.UnfallLauneBis;
        foreach (var kennung in s.Einrichtung ?? new()) EinrichtungHinzufuegen(kennung);
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
        PersonalGeaendert();
    }

    // ── Jahresende ──────────────────────────────────────────────────────────

    /// <summary>Löhne, Unterhalt bzw. Pacht und zufällige Ausbrüche oder Aufstände.</summary>
    internal void Jahresabrechnung(Zufallsquelle zufall, Action<string> melden)
    {
        // Lohnstufe: im Mittel über die Arbeitsminuten des Jahres (ohne Arbeitszeit: die jetzige).
        var lohnFaktor = _lohnMinuten > 0 ? _lohnFaktorSumme / _lohnMinuten : LohnFaktor;
        _lohnFaktorSumme = 0;
        _lohnMinuten = 0;
        foreach (var (name, anzahl) in _belegung)
        {
            var kraft = _daten.Arbeitskraefte[name];
            PersonalBuchen(anzahl * _daten.Lohn(name, Art) * lohnFaktor); // je Stunde gleich: die längere Schicht im Wirtshaus kostet mehr

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
        foreach (var karren in _karren) karren.JahrAbschliessen();
    }
}
