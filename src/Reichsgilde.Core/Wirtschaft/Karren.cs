using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

public enum KarrenZustand
{
    /// <summary>Steht am Betrieb und wartet auf Ladung oder Bedarf.</summary>
    AmBetrieb,

    /// <summary>Unterwegs zum Marktplatz.</summary>
    ZumMarkt,

    /// <summary>Unterwegs zurück zum Betrieb.</summary>
    ZumBetrieb,
}

/// <summary>
/// Ein Karren, der für seinen Betrieb automatisch zwischen Betrieb und Markt pendelt
/// (wie ein Verwalter oder Meister im Original).
///
/// Ein Karren ist ein „Zustandsautomat": Er ist immer in genau einem Zustand und
/// wechselt nach festen Regeln:
///
///   AmBetrieb ──(genug Ware oder Zutaten knapp)──▶ ZumMarkt
///   ZumMarkt  ──(Ankunft: verkaufen, Zutaten kaufen)──▶ ZumBetrieb
///   ZumBetrieb ──(Ankunft: abladen)──▶ AmBetrieb
///
/// Beladene Fahrten ins und aus dem Umland können überfallen werden.
/// </summary>
public sealed class Karren
{
    private readonly Dictionary<string, int> _ladung = new();
    private long? _wareWartetSeit;

    public string Name { get; }
    public string Typ { get; }
    public KarrenDaten Daten { get; }
    public Betrieb Heimat { get; }

    public KarrenZustand Zustand { get; private set; } = KarrenZustand.AmBetrieb;

    /// <summary>Spielminute der Ankunft am Ziel (nur unterwegs von Bedeutung).</summary>
    public long Ankunft { get; private set; }

    public IReadOnlyDictionary<string, int> Ladung => _ladung;

    /// <summary>Ein Handelsauftrag von Hand: Was der Karren am Markt kaufen soll (null: er fährt nach Verwalterregel).</summary>
    public Handelsplan? Auftrag { get; private set; }

    /// <summary>Ein Handelsauftrag, der startet, sobald der Karren zurück ist (null: keiner).</summary>
    public Vormerkung? Vormerkung { get; internal set; }

    /// <summary>Was der letzte Handelsauftrag am Markt gebracht hat (für die Anzeige; nicht gespeichert).</summary>
    public string? LetzterHandel { get; private set; }

    /// <summary>Eskortenstufe, z. B. "Keine", "Normal", "Schwer".</summary>
    public string Eskorte { get; set; } = "Keine";

    /// <summary>
    /// Darf der Karren ins Umland auch in der Dunklen Nacht fahren?
    /// Standard: nein – nachts sind Überfälle doppelt so häufig.
    /// </summary>
    public bool NachtsFahren { get; set; }

    /// <summary>
    /// Wartet der Karren am geschlossenen Stadttor, steht hier die Spielminute, zu der es wieder aufgeht (null: wartet nicht).
    /// Auf dem Hinweg wartet er draußen und kommt dann am Markt an, auf dem Rückweg wartet er drinnen und fährt dann los.
    /// </summary>
    public long? TorWartenBis { get; private set; }

    /// <summary>Wartet der Karren gerade am geschlossenen Tor? (für Anzeige und Darstellung)</summary>
    public bool WartetAmTor(Spielzeit zeit) => TorWartenBis is { } bis && zeit.VergangeneMinuten < bis;

    /// <summary>Der Rückweg ins Umland beginnt erst, wenn das Tor aufgeht (Überfall und Eskorte erst dann).</summary>
    private bool _rueckfahrtWartet;

    internal Karren(string name, string typ, KarrenDaten daten, Betrieb heimat)
    {
        Name = name;
        Typ = typ;
        Daten = daten;
        Heimat = heimat;
    }

    /// <summary>Kurzer Text für die Anzeige.</summary>
    public string ZustandText(Spielzeit zeit) => WartetAmTor(zeit)
        ? (Zustand == KarrenZustand.ZumMarkt ? "wartet vor dem geschlossenen Stadttor bis zum Morgen" : "wartet in der Stadt, bis das Tor aufgeht")
        : Zustand switch
    {
        KarrenZustand.AmBetrieb => "wartet am Betrieb",
        KarrenZustand.ZumMarkt => $"{(Auftrag is null ? "unterwegs zum Markt" : "mit Handelsauftrag zum Markt")} (noch {Math.Max(0, Ankunft - zeit.VergangeneMinuten)} Min.)",
        KarrenZustand.ZumBetrieb => $"auf dem Rückweg (noch {Math.Max(0, Ankunft - zeit.VergangeneMinuten)} Min.)",
        _ => Zustand.ToString(),
    };

    /// <summary>Eine Spielminute für diesen Karren.</summary>
    internal void Tick(Spielzeit zeit, Stadtwirtschaft w)
    {
        if (Zustand != KarrenZustand.AmBetrieb)
        {
            if (_rueckfahrtWartet)
            {
                if (zeit.VergangeneMinuten >= TorWartenBis)
                {
                    _rueckfahrtWartet = false;
                    TorWartenBis = null;
                    Losfahren(KarrenZustand.ZumBetrieb, zeit, w);
                }
                return;
            }
            if (zeit.VergangeneMinuten >= Ankunft)
            {
                // Vom Umland in die Stadt führt der Weg durchs Tor – kurz vor dem Markt.
                if (Zustand == KarrenZustand.ZumMarkt && !DurchsTor(zeit, w))
                {
                    Ankunft = TorWartenBis!.Value;
                    return;
                }
                TorWartenBis = null;
                Ankommen(zeit, w);
            }
            return;
        }

        // Ein vorgemerkter Handelsauftrag geht dem Verwalter vor.
        if (Vormerkung is { } v)
        {
            Vormerkung = null;
            if (w.VorgemerktenAuftragStarten(this, v)) return;
        }

        if (SollLosfahren(zeit, w.Daten.Logistik))
        {
            Beladen(w.Daten.Logistik);
            Losfahren(KarrenZustand.ZumMarkt, zeit, w);
        }
    }

    // ── Entscheidungen des Verwalters ───────────────────────────────────────

    private bool SollLosfahren(Spielzeit zeit, LogistikDaten l)
    {
        // Ins Umland nachts nur, wenn ausdrücklich erlaubt.
        if (Heimat.Ort == Arbeitsort.Umland && zeit.Tagesphase == Tagesphase.DunkleNacht && !NachtsFahren)
            return false;

        if (Heimat.ZutatenReichweite < l.NachkaufUnterStueck)
            return true;

        var ware = Heimat.Verkaufsware().ToList();
        if (ware.Count == 0)
        {
            _wareWartetSeit = null;
            return false;
        }
        _wareWartetSeit ??= zeit.VergangeneMinuten;

        var ladbar = LadePlan(ware).Values.Sum();
        var genugFuerFahrt = ladbar >= Math.Ceiling(Daten.Gesamtkapazitaet * l.AbfahrtAbFuellgrad);
        // Am ganzen Lagerplatz gemessen – eine Schänke behält einen Vorrat für ihre Gäste zurück, der zählt mit.
        var lagerFastVoll = ware.Max(x => Heimat.ImLager(x.Ware)) >= Heimat.LagerKapazitaetJeWare * l.AbfahrtAbFuellgrad;
        var wartetZuLange = zeit.VergangeneMinuten - _wareWartetSeit >= l.MaxWartezeitMinuten;
        return genugFuerFahrt || lagerFastVoll || wartetZuLange;
    }

    /// <summary>
    /// Verteilt Waren auf die Ladeflächen: Jede Fläche bekommt die Ware, von der gerade
    /// am meisten übrig ist. Eine Ware darf mehrere Flächen belegen.
    /// </summary>
    private Dictionary<string, int> LadePlan(IEnumerable<(string Ware, int Stueck)> ware)
    {
        var rest = ware.ToDictionary(x => x.Ware, x => x.Stueck);
        var plan = new Dictionary<string, int>();
        for (var flaeche = 0; flaeche < Daten.Sorten; flaeche++)
        {
            var naechste = rest.Where(r => r.Value > 0)
                .OrderByDescending(r => r.Value)
                .ThenBy(r => r.Key, StringComparer.Ordinal) // feste Reihenfolge = gleicher Verlauf
                .Select(r => r.Key)
                .FirstOrDefault();
            if (naechste is null) break;
            var menge = Math.Min(rest[naechste], Daten.KapazitaetJeSorte);
            plan[naechste] = plan.GetValueOrDefault(naechste) + menge;
            rest[naechste] -= menge;
        }
        return plan;
    }

    private void Beladen(LogistikDaten l)
    {
        foreach (var (ware, stueck) in LadePlan(Heimat.Verkaufsware()))
            _ladung[ware] = Heimat.Auslagern(ware, stueck);
        _wareWartetSeit = null;
    }

    // ── Fahrten ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Kommt der Karren jetzt durchs Stadttor? Nur Umlandkarren müssen hindurch. Ist es zu, zahlt ein Karren mit
    /// Nachtfahrt-Erlaubnis das Torgeld; sonst wartet er (<see cref="TorWartenBis"/> = nächster Morgen).
    /// </summary>
    private bool DurchsTor(Spielzeit zeit, Stadtwirtschaft w)
    {
        if (Heimat.Ort != Arbeitsort.Umland || w.Daten.Logistik.Tore is not { } tore || !tore.IstZu(zeit))
            return true;
        if (NachtsFahren)
        {
            Heimat.TransportBuchen(tore.Torgeld);
            return true;
        }
        TorWartenBis = (zeit.VergangeneMinuten / Spielzeit.MinutenProTag + 1) * Spielzeit.MinutenProTag;
        return false;
    }

    /// <summary>Vom Markt zurück zum Betrieb – ins Umland erst, wenn das Tor offen ist.</summary>
    private void Zurueckfahren(Spielzeit zeit, Stadtwirtschaft w)
    {
        if (DurchsTor(zeit, w))
        {
            Losfahren(KarrenZustand.ZumBetrieb, zeit, w);
            return;
        }
        Zustand = KarrenZustand.ZumBetrieb;
        _rueckfahrtWartet = true;
        Ankunft = TorWartenBis!.Value + w.Fahrzeit(Heimat, zumMarkt: false);
    }

    private void Losfahren(KarrenZustand ziel, Spielzeit zeit, Stadtwirtschaft w)
    {
        Zustand = ziel;
        Ankunft = zeit.VergangeneMinuten + w.Fahrzeit(Heimat, zumMarkt: ziel == KarrenZustand.ZumMarkt);
        w.Wegebau.FahrtZaehlen(Heimat); // Verkehr auf den Wegen (Austritt)

        if (_ladung.Count == 0)
            return; // Leere Karren kosten nichts und lohnen keinen Überfall.

        // Die Eskorte der Wache gibt es nur, solange ein Haus sie stellt – sonst fährt der Karren ohne.
        var eskorte = w.Wache.EskorteVerfuegbar(Eskorte) ? w.Daten.Logistik.Eskorten[Eskorte] : new EskorteDaten();
        if (eskorte.KostenJeFahrt > 0)
        {
            if (eskorte.Wache) w.Wache.GeleitBezahlen(Heimat, eskorte.KostenJeFahrt);
            else Heimat.TransportBuchen(eskorte.KostenJeFahrt);
        }

        // Ein bestellter Hinterhalt geht vor; sonst würfeln die gewöhnlichen Banden.
        if (Heimat.Ort == Arbeitsort.Umland && !w.Gauner.HinterhaltAusloesen(this, eskorte, zeit))
            Ueberfall.Wuerfeln(this, eskorte, zeit, w);
    }

    /// <summary>
    /// Handel von Hand: Verkaufsware laden und mit dem Einkaufszettel zum Markt fahren.
    /// Geprüft hat die Stadtwirtschaft (Zustand, Ladeflächen, Lager, Marktzeit).
    /// </summary>
    internal void AuftragAnnehmen(IReadOnlyDictionary<string, int> verkauf, IReadOnlyDictionary<string, int> kauf, Spielzeit zeit, Stadtwirtschaft w)
    {
        foreach (var (ware, stueck) in verkauf.Where(x => x.Value > 0))
            _ladung[ware] = _ladung.GetValueOrDefault(ware) + Heimat.Auslagern(ware, stueck);
        Auftrag = new Handelsplan(kauf.Where(x => x.Value > 0).ToDictionary(x => x.Key, x => x.Value));
        _wareWartetSeit = null;
        Losfahren(KarrenZustand.ZumMarkt, zeit, w);
    }

    /// <summary>Am Markt: den Einkaufszettel abarbeiten – so viel, wie das Geld reicht.</summary>
    private void AuftragEinkaufen(Stadtwirtschaft w, double erloes)
    {
        var plan = Auftrag!;
        Auftrag = null;
        var faktor = (1 + w.Daten.Handel!.Aufschlag) * w.Familien.EinkaufsFaktor(Heimat.Konto);
        var gekauft = new List<string>();
        foreach (var (ware, wunsch) in plan.Kauf.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var stand = w.Markt[ware];
            var stueck = wunsch;
            while (stueck > 0 && stand.HandKaufpreis(stueck, faktor) > Heimat.Konto.Vermoegen) stueck--;
            if (stueck <= 0) { gekauft.Add($"{ware}: kein Geld"); continue; }
            var kosten = stand.HandKaufen(stueck, faktor);
            Heimat.ZutatenBuchen(kosten);
            _ladung[ware] = _ladung.GetValueOrDefault(ware) + stueck;
            gekauft.Add($"{stueck} {ware} für {Zahlenformat.Ganz(kosten)}");
        }
        var waehrung = w.Daten.Allgemein.Waehrung;
        LetzterHandel = $"{Name} ({Heimat.Name}) am Markt: " +
                        (erloes > 0 ? $"verkauft für {Zahlenformat.Ganz(erloes)} {waehrung}" : "nichts verkauft") +
                        (gekauft.Count > 0 ? $"; gekauft {string.Join(", ", gekauft)} {waehrung}" : "") + ".";
        w.Melden(LetzterHandel);
    }

    private void Ankommen(Spielzeit zeit, Stadtwirtschaft w)
    {
        if (Zustand == KarrenZustand.ZumMarkt)
        {
            if (Auftrag is not null)
            {
                var vorher = Heimat.Konto.Laufend.Umsatz;
                foreach (var (ware, stueck) in _ladung)
                    Heimat.Verkaufen(ware, stueck, w.Markt, w.Familien.VerkaufsFaktor(Heimat.Konto));
                _ladung.Clear();
                AuftragEinkaufen(w, Heimat.Konto.Laufend.Umsatz - vorher);
                Zurueckfahren(zeit, w);
                return;
            }

            // Alles verkaufen ...
            foreach (var (ware, stueck) in _ladung)
                Heimat.Verkaufen(ware, stueck, w.Markt, w.Familien.VerkaufsFaktor(Heimat.Konto));
            _ladung.Clear();

            // ... und knappe Zutaten einkaufen.
            ZutatenEinkaufen(w);
            Zurueckfahren(zeit, w);
        }
        else
        {
            foreach (var (ware, stueck) in _ladung)
                Heimat.Einlagern(ware, stueck);
            _ladung.Clear();
            Zustand = KarrenZustand.AmBetrieb;
        }
    }

    /// <summary>
    /// Füllt jede Ladefläche mit der Zutat, die (einschließlich bereits geladener Menge)
    /// gerade am knappsten ist – gemessen daran, für wie viele Stück sie reicht.
    /// </summary>
    private void ZutatenEinkaufen(Stadtwirtschaft w)
    {
        if (Heimat.Zutaten.Count == 0) return;

        for (var flaeche = 0; flaeche < Daten.Sorten; flaeche++)
        {
            var knappste = Heimat.Zutaten
                .Select(z => (Zutat: z.Key, Menge: z.Value, Bestand: Heimat.ImLager(z.Key) + _ladung.GetValueOrDefault(z.Key)))
                .Where(z => z.Bestand < Heimat.LagerKapazitaetJeWare)
                .OrderBy(z => z.Bestand / z.Menge)
                .ThenBy(z => z.Zutat, StringComparer.Ordinal)
                .FirstOrDefault();
            if (knappste.Zutat is null) break;

            var menge = Math.Min(Daten.KapazitaetJeSorte, Heimat.LagerKapazitaetJeWare - knappste.Bestand);
            Heimat.ZutatenBuchen(w.Markt[knappste.Zutat].Kaufen(menge));
            _ladung[knappste.Zutat] = _ladung.GetValueOrDefault(knappste.Zutat) + menge;
        }
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    internal KarrenStand Sichern() => new()
    {
        Name = Name, Typ = Typ, Zustand = Zustand, Ankunft = Ankunft, Ladung = new(_ladung),
        Eskorte = Eskorte, NachtsFahren = NachtsFahren, WareWartetSeit = _wareWartetSeit,
        TorWartenBis = TorWartenBis, RueckfahrtWartet = _rueckfahrtWartet,
        Auftrag = Auftrag is null ? null : new(Auftrag.Kauf),
        VormerkungVerkauf = Vormerkung is null ? null : new(Vormerkung.Verkauf),
        VormerkungKauf = Vormerkung is null ? null : new(Vormerkung.Kauf),
    };

    internal void Wiederherstellen(KarrenStand s)
    {
        Zustand = s.Zustand;
        Ankunft = s.Ankunft;
        _ladung.Clear();
        foreach (var (ware, stueck) in s.Ladung) _ladung[ware] = stueck;
        Eskorte = s.Eskorte;
        NachtsFahren = s.NachtsFahren;
        _wareWartetSeit = s.WareWartetSeit;
        TorWartenBis = s.TorWartenBis;
        _rueckfahrtWartet = s.RueckfahrtWartet;
        Auftrag = s.Auftrag is null ? null : new Handelsplan(new Dictionary<string, int>(s.Auftrag));
        Vormerkung = s.VormerkungVerkauf is null || s.VormerkungKauf is null ? null
            : new Vormerkung(new Dictionary<string, int>(s.VormerkungVerkauf), new Dictionary<string, int>(s.VormerkungKauf));
    }

    /// <summary>Die Ladung geht verloren (Überfall).</summary>
    internal Dictionary<string, int> LadungVerlieren()
    {
        var verloren = new Dictionary<string, int>(_ladung);
        _ladung.Clear();
        return verloren;
    }
}
