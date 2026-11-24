using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

public enum KarrenZustand
{
    /// <summary>Steht am Betrieb und wartet auf Ladung oder Bedarf.</summary>
    AmBetrieb,

    /// <summary>Unterwegs zum Marktplatz.</summary>
    ZumMarkt,

    /// <summary>Unterwegs zu einem Betrieb: zurück zum Heimatbetrieb oder (selbst gefahren) zu einem anderen eigenen (<see cref="Karren.ZielBetrieb"/>).</summary>
    ZumBetrieb,

    /// <summary>Steht am Markt und wartet auf Befehle (nur selbst gefahrene Karren; automatisch fahrende kehren nach dem Handel um).</summary>
    AmMarkt,
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
/// Fährt der Spieler seine Karren selbst (<see cref="Stadtwirtschaft.FaehrtAutomatisch"/> falsch, Nutzer 23.11.2026), entscheidet der Verwalter
/// nichts: Der Karren fährt nur auf Befehl (<see cref="KarrenLosschicken"/>) zum Markt oder zu einem eigenen Betrieb, steht dort
/// (<see cref="KarrenZustand.AmMarkt"/> bzw. <see cref="KarrenZustand.AmBetrieb"/> mit <see cref="StandortBetrieb"/>) mit seiner Ladung und
/// wartet auf den nächsten Befehl (Handeln, Beladen, Abladen, Losschicken).
///
/// Beladene Fahrten ins und aus dem Umland können überfallen werden.
/// </summary>
public sealed partial class Karren
{
    private readonly Dictionary<string, int> _ladung = new();
    private long? _wareWartetSeit;

    public string Name { get; }
    public string Typ { get; }
    public KarrenDaten Daten { get; }
    public Betrieb Heimat { get; }

    public KarrenZustand Zustand { get; private set; } = KarrenZustand.AmBetrieb;

    /// <summary>Ist der Karren unterwegs (zum Markt oder zu einem Betrieb, auch wartend an Tor, Brücke oder beim Verweilen)?</summary>
    public bool Unterwegs => Zustand is KarrenZustand.ZumMarkt or KarrenZustand.ZumBetrieb;

    /// <summary>
    /// Steht der Karren an einem anderen eigenen Betrieb als seinem Heimatbetrieb, dessen Name (null: am Heimatbetrieb, unterwegs oder am Markt).
    /// Nur selbst gefahrene Karren stehen woanders.
    /// </summary>
    public string? StandortBetrieb { get; private set; }

    /// <summary>Unterwegs zu einem Betrieb (<see cref="KarrenZustand.ZumBetrieb"/>): dessen Name, wenn es nicht der Heimatbetrieb ist (sonst null).</summary>
    public string? ZielBetrieb { get; private set; }

    /// <summary>
    /// Woher die Fahrt kommt, wenn nicht wie gewohnt: zum Markt von einem anderen Betrieb als dem Heimatbetrieb; zu einem Betrieb von einem Betrieb
    /// (auch dem Heimatbetrieb) statt vom Markt. Null: der gewohnte Weg (zum Markt vom Heimatbetrieb, zum Betrieb vom Markt).
    /// </summary>
    public string? VonBetrieb { get; private set; }

    /// <summary>
    /// Selbst gefahren: Fährt der Karren von einem Handelsort zum anderen (vom Markt zum Hafen oder umgekehrt; <see cref="ZielHafen"/> ist das Ziel)?
    /// Dann ist die Herkunft kein Betrieb (<see cref="Herkunft"/> null), sondern der andere Handelsort.
    /// </summary>
    public bool VonHandelsort { get; private set; }

    private Betrieb? BetriebNamens(Stadtwirtschaft w, string name) => w.Betriebe.FirstOrDefault(b => b.Name == name);

    /// <summary>Der Betrieb, an dem der Karren steht (null: am Markt oder unterwegs).</summary>
    public Betrieb? StandortVon(Stadtwirtschaft w) => Zustand != KarrenZustand.AmBetrieb ? null
        : StandortBetrieb is { } name ? BetriebNamens(w, name) ?? Heimat : Heimat;

    /// <summary>Woher die laufende Fahrt kommt: ein Betrieb oder der Markt (null). Am Betrieb bzw. Markt: der Standort.</summary>
    public Betrieb? Herkunft(Stadtwirtschaft w) => Zustand switch
    {
        KarrenZustand.ZumMarkt => VonHandelsort ? null : VonBetrieb is { } von ? BetriebNamens(w, von) ?? Heimat : Heimat,
        KarrenZustand.ZumBetrieb => VonBetrieb is { } von ? BetriebNamens(w, von) ?? Heimat : null,
        _ => StandortVon(w),
    };

    /// <summary>Wohin die laufende Fahrt geht: ein Betrieb oder der Markt (null). Am Betrieb bzw. Markt: der Standort.</summary>
    public Betrieb? Ziel(Stadtwirtschaft w) => Zustand switch
    {
        KarrenZustand.ZumMarkt => null,
        KarrenZustand.ZumBetrieb => ZielBetrieb is { } ziel ? BetriebNamens(w, ziel) ?? Heimat : Heimat,
        _ => StandortVon(w),
    };

    /// <summary>
    /// Selbst gefahren: Steht der Karren an einem Betrieb, der nicht mehr seinem Haus gehört (verkauft, abgerissen), steht er wieder am
    /// Heimatbetrieb – ein Fuhrmann bringt ihn heim, die Ladung bleibt auf dem Karren.
    /// </summary>
    private void StandortPruefen(Stadtwirtschaft w)
    {
        if (StandortBetrieb is not { } name) return;
        if (BetriebNamens(w, name) is { } b && b.Konto == Heimat.Konto) return;
        StandortBetrieb = null;
    }

    /// <summary>Spielminute der Ankunft am Ziel (nur unterwegs von Bedeutung).</summary>
    public long Ankunft { get; private set; }

    /// <summary>
    /// Fährt der Karren zum Handeln an den Hafen statt zum Marktplatz (Datenversion 73)? Gewählt beim Aufbruch (<see cref="Stadtwirtschaft.FaehrtZumHafen"/>)
    /// und für die ganze Fahrt behalten, auch auf dem Rückweg und danach am Betrieb (bis zum nächsten Aufbruch) – so fährt das Bild denselben Weg zu Ende.
    /// </summary>
    public bool ZielHafen { get; private set; }

    /// <summary>„Hafen“ oder „Markt“ – wohin der Karren zum Handeln fährt bzw. zuletzt fuhr (für Texte).</summary>
    public string Handelsort => ZielHafen ? "Hafen" : "Markt";

    public IReadOnlyDictionary<string, int> Ladung => _ladung;

    /// <summary>Ein Handelsauftrag von Hand: Was der Karren am Markt kaufen soll (null: er fährt nach Verwalterregel).</summary>
    public Handelsplan? Auftrag { get; private set; }

    /// <summary>Ein Handelsauftrag, der startet, sobald der Karren zurück ist (null: keiner).</summary>
    public Vormerkung? Vormerkung { get; internal set; }

    /// <summary>Was der letzte Handelsauftrag am Markt gebracht hat (für die Anzeige; nicht gespeichert).</summary>
    public string? LetzterHandel { get; private set; }

    /// <summary>Fahrten zum Markt in diesem Jahr (mit und ohne Ladung, auch Handelsaufträge).</summary>
    public int FahrtenImJahr { get; private set; }

    /// <summary>
    /// Fahrten zum Markt im Vorjahr – null, wenn der Karren das Vorjahr nicht ganz gezählt hat (erst im Lauf des Jahres gekauft,
    /// Startausstattung, alter Spielstand). Die KI verkauft nur einen Karren, der ein ganzes Jahr nicht gefahren ist.
    /// </summary>
    public int? FahrtenVorjahr { get; private set; }

    /// <summary>Zählt <see cref="FahrtenImJahr"/> seit Jahresbeginn? (erst nach dem ersten Jahresabschluss)</summary>
    private bool _ganzesJahr;

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

    /// <summary>
    /// Wartet der Karren auf dieser Fahrt an der offenen Klappbrücke (<see cref="Stadtwirtschaft.Klappbruecke"/>, Datenversion 90), steht hier die
    /// Spielminute, zu der er sie erreicht (null: wartet auf dieser Fahrt nicht). Gesetzt beim Aufbruch; die Wartezeit steckt in <see cref="Ankunft"/>.
    /// </summary>
    public long? BrueckeWartenAb { get; private set; }

    /// <summary>Bis zu dieser Spielminute wartet er an der Klappbrücke (dann geht sie zu und er fährt weiter; null: wartet nicht).</summary>
    public long? BrueckeWartenBis { get; private set; }

    /// <summary>
    /// Verweilt der Karren nach dem Handel am Markt bzw. Hafen, steht hier die Spielminute, zu der er zurückfährt (null: verweilt nicht).
    /// Nur automatisch fahrende Karren verweilen (<see cref="Stadtwirtschaft.FaehrtAutomatisch"/>, <see cref="LogistikDaten.VerweilenMinuten"/>,
    /// Datenversion 98): Die Zeit steht für das Ab- und Aufladen. Der Zustand bleibt solange <see cref="KarrenZustand.ZumMarkt"/>.
    /// </summary>
    public long? VerweiltBis { get; private set; }

    /// <summary>Verweilt der Karren gerade am Markt bzw. Hafen? (für Anzeige und Darstellung)</summary>
    public bool Verweilt(Spielzeit zeit) => VerweiltBis is { } bis && zeit.VergangeneMinuten < bis;

    /// <summary>Wartet der Karren gerade an der offenen Klappbrücke? (für Anzeige und Darstellung)</summary>
    public bool WartetAnBruecke(Spielzeit zeit) => Unterwegs && BrueckeWartenAb is { } ab && BrueckeWartenBis is { } bis
                                                   && zeit.VergangeneMinuten >= ab && zeit.VergangeneMinuten < bis;

    /// <summary>
    /// Fährt der Karren gerade mit seinem Fuhrmann (unterwegs und nicht am geschlossenen Tor)? Für die Darstellung: am Betrieb und am Tor
    /// ist der Fuhrmann nicht zu sehen und das Zugtier steht angebunden; ebenso, solange er am Markt verweilt (<see cref="Verweilt"/>).
    /// Nur eine Abfrage, ändert nichts an den Fahrzeiten.
    /// </summary>
    public bool Faehrt(Spielzeit zeit) => Unterwegs && !WartetAmTor(zeit) && !Verweilt(zeit);

    /// <summary>
    /// Der Fuhrmann dieses Karrens, nur fürs Bild (abgeleitet, nicht gespeichert, ohne Lohn): fest aus Betriebsname (Heimat.Name) und Karrenname gewürfelt,
    /// das Volk mit der Wahrscheinlichkeit „personal.eigenesVolk“ das des Hauses, sonst eines der vier Bürgervölker.
    /// </summary>
    public Fuhrmann FuhrmannIn(Stadtwirtschaft w) =>
        Fuhrmann.Ableiten(Heimat.Name, Name, w.Familien.Von(Heimat.Konto)?.Oberhaupt.Volk, w.Personal.Daten?.EigenesVolk ?? 0.6, w.Personal.Daten?.Frauen ?? 0.15);

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
        ? (_rueckfahrtWartet ? "wartet in der Stadt, bis das Tor aufgeht" : "wartet vor dem geschlossenen Stadttor bis zum Morgen") // drinnen nur vor der Fahrt ins Umland (Abfahren)
        : WartetAnBruecke(zeit) ? $"wartet an der offenen Klappbrücke (noch {BrueckeWartenBis!.Value - zeit.VergangeneMinuten} Min.)"
        : Verweilt(zeit) ? $"lädt am {Handelsort} (noch {VerweiltBis!.Value - zeit.VergangeneMinuten} Min.)"
        : Zustand switch
    {
        KarrenZustand.AmBetrieb => StandortBetrieb is { } dort ? $"steht an {dort}" : "wartet am Betrieb",
        KarrenZustand.AmMarkt => $"steht am {Handelsort}",
        KarrenZustand.ZumBetrieb when ZielBetrieb is { } ziel => $"unterwegs nach {ziel} (noch {Math.Max(0, Ankunft - zeit.VergangeneMinuten)} Min.)",
        KarrenZustand.ZumMarkt => $"{(Auftrag is null ? "unterwegs zum " : "mit Handelsauftrag zum ")}{Handelsort} (noch {Math.Max(0, Ankunft - zeit.VergangeneMinuten)} Min.)",
        KarrenZustand.ZumBetrieb => $"auf dem Rückweg (noch {Math.Max(0, Ankunft - zeit.VergangeneMinuten)} Min.)",
        _ => Zustand.ToString(),
    };

    /// <summary>Eine Spielminute für diesen Karren.</summary>
    internal void Tick(Spielzeit zeit, Stadtwirtschaft w)
    {
        if (Unterwegs)
        {
            // Nach dem Handel am Markt verweilen (nur automatisch fahrende Karren), dann zurück.
            if (VerweiltBis is { } bis)
            {
                if (zeit.VergangeneMinuten < bis) return;
                VerweiltBis = null;
                Zurueckfahren(zeit, w);
                return;
            }
            if (_rueckfahrtWartet)
            {
                if (zeit.VergangeneMinuten >= TorWartenBis)
                {
                    _rueckfahrtWartet = false;
                    TorWartenBis = null;
                    Fahren(Zustand, Herkunft(w), Ziel(w), zeit, w);
                }
                return;
            }
            if (zeit.VergangeneMinuten >= Ankunft)
            {
                // Vom Umland in die Stadt führt der Weg durchs Tor – kurz vor dem Ziel.
                if (Herkunft(w)?.Ort == Arbeitsort.Umland && Ziel(w)?.Ort != Arbeitsort.Umland && !DurchsTor(zeit, w))
                {
                    Ankunft = TorWartenBis!.Value;
                    return;
                }
                TorWartenBis = null;
                Ankommen(zeit, w);
            }
            return;
        }

        // Selbst gefahren: Der Karren steht, bis der Spieler ihn losschickt.
        if (!w.FaehrtAutomatisch(this))
        {
            StandortPruefen(w);
            return;
        }
        // Fährt er (wieder) automatisch, kommt er zuerst nach Hause: vom Markt zurück, von einem anderen Betrieb heim.
        if (Zustand == KarrenZustand.AmMarkt)
        {
            Zurueckfahren(zeit, w);
            return;
        }
        if (StandortBetrieb is not null)
        {
            if (StandortVon(w) is { } dort && dort != Heimat) Abfahren(KarrenZustand.ZumBetrieb, dort, Heimat, zeit, w);
            else StandortBetrieb = null;
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
            Fahren(KarrenZustand.ZumMarkt, Heimat, null, zeit, w);
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
    /// Kommt der Karren jetzt durchs Stadttor? Gefragt wird nur bei Fahrten zwischen Umland und Stadt (der Markt liegt in der Stadt). Ist es zu,
    /// zahlt ein Karren mit Nachtfahrt-Erlaubnis das Torgeld; sonst wartet er (<see cref="TorWartenBis"/> = nächster Morgen).
    /// </summary>
    private bool DurchsTor(Spielzeit zeit, Stadtwirtschaft w)
    {
        if (w.Daten.Logistik.Tore is not { } tore || !tore.IstZu(zeit))
            return true;
        if (NachtsFahren)
        {
            Heimat.TransportBuchen(tore.Torgeld);
            return true;
        }
        TorWartenBis = (zeit.VergangeneMinuten / Spielzeit.MinutenProTag + 1) * Spielzeit.MinutenProTag;
        return false;
    }

    /// <summary>
    /// Nach dem Handel am Markt: Ein automatisch fahrender Karren verweilt <see cref="LogistikDaten.VerweilenMinuten"/> (Ab- und Aufladen,
    /// Datenversion 98), dann fährt er zurück (<see cref="Tick"/>); sonst fährt er gleich.
    /// </summary>
    private void NachDemHandel(Spielzeit zeit, Stadtwirtschaft w)
    {
        if (w.Daten.Logistik.VerweilenMinuten > 0 && w.FaehrtAutomatisch(this))
        {
            VerweiltBis = zeit.VergangeneMinuten + w.Daten.Logistik.VerweilenMinuten;
            return;
        }
        Zurueckfahren(zeit, w);
    }

    /// <summary>
    /// Wann ist der Karren etwa wieder am Betrieb (Spielminute)? Unterwegs zum Markt: Ankunft, Verweilen und Rückweg; auf dem Rückweg: die Ankunft.
    /// Ein geschlossenes Tor auf dem Rückweg ist nicht eingerechnet. Am Betrieb: jetzt.
    /// </summary>
    internal long ZurueckEtwa(Stadtwirtschaft w) => Zustand switch
    {
        KarrenZustand.ZumBetrieb => Ankunft,
        KarrenZustand.ZumMarkt => (VerweiltBis ?? Ankunft + (w.Daten.Logistik.VerweilenMinuten > 0 && w.FaehrtAutomatisch(this) ? w.Daten.Logistik.VerweilenMinuten : 0))
                                  + w.Fahrzeit(this, zumMarkt: false),
        _ => w.Uhr.VergangeneMinuten,
    };

    /// <summary>Vom Markt zurück zum Heimatbetrieb – ins Umland erst, wenn das Tor offen ist.</summary>
    private void Zurueckfahren(Spielzeit zeit, Stadtwirtschaft w) => Abfahren(KarrenZustand.ZumBetrieb, null, Heimat, zeit, w);

    /// <summary>
    /// Aufbrechen von <paramref name="von"/> nach <paramref name="nach"/> (null: der Markt). Führt die Fahrt aus der Stadt ins Umland und ist
    /// das Tor zu, wartet der Karren drinnen, bis es aufgeht (ohne Nachtfahrt-Erlaubnis), und fährt dann (<see cref="Tick"/>).
    /// </summary>
    /// <param name="zumHafen">Selbst gefahren zu einem Handelsort: zum Hafen (true) oder zum Marktplatz (false); null: wie der Verwalter wählt.</param>
    private void Abfahren(KarrenZustand zustand, Betrieb? von, Betrieb? nach, Spielzeit zeit, Stadtwirtschaft w, bool? zumHafen = null)
    {
        if (von?.Ort != Arbeitsort.Umland && nach?.Ort == Arbeitsort.Umland && !DurchsTor(zeit, w))
        {
            Zustand = zustand;
            OrteMerken(von, nach);
            _rueckfahrtWartet = true;
            Ankunft = TorWartenBis!.Value + w.KarrenFahrt(this, von, nach).Minuten;
            return;
        }
        Fahren(zustand, von, nach, zeit, w, zumHafen);
    }

    /// <summary>Merkt Herkunft und Ziel einer Fahrt so, wie der Spielstand sie hält (der gewohnte Weg Heimat ↔ Markt ohne Namen).</summary>
    private void OrteMerken(Betrieb? von, Betrieb? nach)
    {
        StandortBetrieb = null;
        VonHandelsort = false;
        ZielBetrieb = nach is null || nach == Heimat ? null : nach.Name;
        VonBetrieb = Zustand == KarrenZustand.ZumMarkt
            ? (von is null || von == Heimat ? null : von.Name)
            : von?.Name;
    }

    /// <summary>
    /// Losfahren von <paramref name="von"/> nach <paramref name="nach"/> (null: der Markt): Fahrzeit des Kerns (<see cref="Stadtwirtschaft.KarrenFahrt"/>),
    /// Klappbrücke, Eskorte und – wenn die Fahrt das Umland berührt – Überfall. Automatisch fahrende Karren wählen den näheren Handelsort
    /// (Markt oder Hafen, Datenversion 73); selbst gefahrene fahren dorthin, wohin der Spieler sie schickt (<paramref name="zumHafen"/>).
    /// </summary>
    private void Fahren(KarrenZustand zustand, Betrieb? von, Betrieb? nach, Spielzeit zeit, Stadtwirtschaft w, bool? zumHafen = null)
    {
        var vonHafen = ZielHafen; // steht er an einem Handelsort, an welchem
        Zustand = zustand;
        OrteMerken(von, nach);
        var gewohnt = (zustand == KarrenZustand.ZumMarkt && von == Heimat) || (zustand == KarrenZustand.ZumBetrieb && von is null && nach == Heimat);
        if (zustand == KarrenZustand.ZumMarkt)
        {
            VonHandelsort = von is null;
            ZielHafen = zumHafen ?? (von == Heimat && w.FaehrtAutomatisch(this) && w.FaehrtZumHafen(Heimat, Typ)); // der nähere Handelsort (Datenversion 73)
        }
        var (fahrzeit, strecke) = VonHandelsort ? w.KarrenFahrt(this, null, null, vonHafen, ZielHafen) : w.KarrenFahrt(this, von, nach);
        Ankunft = zeit.VergangeneMinuten + fahrzeit;
        // Führt die Fahrt über die Klappbrücke und ist sie offen, wenn er sie erreicht: warten, bis sie zugeht (Datenversion 90).
        (BrueckeWartenAb, BrueckeWartenBis) = ((long?)null, (long?)null);
        if (strecke is not null && w.Klappbruecke.Halt(strecke, zeit.VergangeneMinuten, fahrzeit) is { } halt)
        {
            (BrueckeWartenAb, BrueckeWartenBis) = (halt.Von, halt.Bis);
            Ankunft += halt.Bis - halt.Von;
        }
        if (zustand == KarrenZustand.ZumMarkt) FahrtenImJahr++;
        if (gewohnt) w.Wegebau.FahrtZaehlen(Heimat, ZielHafen); // Verkehr auf den Wegen (Austritt)

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
        if ((von?.Ort == Arbeitsort.Umland || nach?.Ort == Arbeitsort.Umland) && !w.Gauner.HinterhaltAusloesen(this, eskorte, zeit))
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
        Fahren(KarrenZustand.ZumMarkt, Heimat, null, zeit, w);
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
        LetzterHandel = $"{Name} ({Heimat.Name}) am {Handelsort}: " +
                        (erloes > 0 ? $"verkauft für {Zahlenformat.Ganz(erloes)} {waehrung}" : "nichts verkauft") +
                        (gekauft.Count > 0 ? $"; gekauft {string.Join(", ", gekauft)} {waehrung}" : "") + ".";
        w.Melden(LetzterHandel);
    }

    private void Ankommen(Spielzeit zeit, Stadtwirtschaft w)
    {
        // Verhandeln × Aufschlag am Hafen (nur Ausfuhrware, Hafen offen; Datenversion 80) – dieselbe Rechnung wie die Vorschauen.
        double Faktor(string ware) => w.KarrenVerkaufsFaktor(Heimat.Konto, ware, ZielHafen);

        if (Zustand == KarrenZustand.ZumMarkt)
        {
            if (Auftrag is not null)
            {
                var vorher = Heimat.Konto.Laufend.Umsatz;
                foreach (var (ware, stueck) in _ladung)
                    Heimat.Verkaufen(ware, stueck, w.Markt, Faktor(ware), w.SteuersatzMarkt(Heimat.Konto));
                _ladung.Clear();
                AuftragEinkaufen(w, Heimat.Konto.Laufend.Umsatz - vorher);
                NachDemHandel(zeit, w);
                return;
            }

            // Selbst gefahren: Er steht am Markt, bis der Spieler handelt oder ihn weiterschickt.
            if (!w.FaehrtAutomatisch(this))
            {
                Zustand = KarrenZustand.AmMarkt;
                OrteVergessen();
                w.Melden(Meldungsart.KarrenAngekommen, $"{Name} ({Heimat.Name}) ist am {Handelsort} angekommen{LadungsText()}.", betrieb: Heimat);
                return;
            }

            // Alles verkaufen ...
            foreach (var (ware, stueck) in _ladung)
                Heimat.Verkaufen(ware, stueck, w.Markt, Faktor(ware), w.SteuersatzMarkt(Heimat.Konto));
            _ladung.Clear();

            // ... und knappe Zutaten einkaufen.
            ZutatenEinkaufen(zeit, w);
            NachDemHandel(zeit, w);
        }
        else
        {
            var ziel = Ziel(w) ?? Heimat;
            Zustand = KarrenZustand.AmBetrieb;
            OrteVergessen();
            StandortBetrieb = ziel == Heimat ? null : ziel.Name;
            if (!w.FaehrtAutomatisch(this))
            {
                // Selbst gefahren: kein Abladen von allein – die Ladung bleibt auf dem Karren.
                w.Melden(Meldungsart.KarrenAngekommen, $"{Name} ({Heimat.Name}) ist an {ziel.Name} angekommen{LadungsText()}.", betrieb: ziel);
                return;
            }
            foreach (var (ware, stueck) in _ladung)
                ziel.Einlagern(ware, stueck);
            _ladung.Clear();
        }
    }

    /// <summary>„ mit 10 Eisen, 5 Holz“ – oder leer, wenn der Karren nichts geladen hat (für Meldungen).</summary>
    private string LadungsText() => _ladung.Count == 0 ? " (leer)"
        : " mit " + string.Join(", ", _ladung.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{Zahlenformat.Ganz(x.Value)} {x.Key}"));

    /// <summary>Am Ziel: Herkunft und Ziel der Fahrt gelten nicht mehr.</summary>
    private void OrteVergessen() => (VonBetrieb, ZielBetrieb, VonHandelsort) = (null, null, false);

    /// <summary>
    /// Füllt jede Ladefläche mit der Zutat, die (einschließlich bereits geladener Menge)
    /// gerade am knappsten ist – gemessen daran, für wie viele Stück sie reicht.
    /// Wie ein vorsichtiger Kaufmann (PR #232): jede Zutat nur bis zum Bedarf weniger Arbeitsstunden (<see cref="Vorratsziel"/>),
    /// und auf der ganzen Fahrt höchstens einen Anteil des Geldes, das nach den Kosten des Jahresabschlusses frei bleibt.
    /// Sonst legte sich ein Parfumeur mit dem Giftdolch 40 Dolche (≈ 7.000 Gulden) ins Lager und konnte die Löhne nicht mehr zahlen.
    /// Stockt die Werkstatt (Zutaten für weniger als <see cref="LogistikDaten.NachkaufUnterStueck"/> Stück), kauft er trotzdem wenigstens
    /// die Zutaten für so viele Stück, solange Geld in der Kasse ist – sonst käme eine Werkstatt mit knapper Kasse nie wieder in Gang.
    /// </summary>
    private void ZutatenEinkaufen(Spielzeit zeit, Stadtwirtschaft w)
    {
        if (Heimat.Zutaten.Count == 0) return;

        var l = w.Daten.Logistik;
        var ziel = Vorratsziel(w);
        var budget = Math.Max(0, (Heimat.Konto.Vermoegen - w.OffeneJahreskosten(Heimat.Konto)) * l.ZutatenAnteilKasse);
        var stockt = Heimat.ZutatenReichweite < l.NachkaufUnterStueck;
        var nurNoetigstes = false;
        for (var flaeche = 0; flaeche < Daten.Sorten; flaeche++)
        {
            var knappste = Heimat.Zutaten
                .Select(z => (Zutat: z.Key, Menge: z.Value, Bestand: Heimat.ImLager(z.Key) + _ladung.GetValueOrDefault(z.Key)))
                .Where(z => z.Bestand < ziel[z.Zutat])
                .OrderBy(z => z.Bestand / z.Menge)
                .ThenBy(z => z.Zutat, StringComparer.Ordinal)
                .FirstOrDefault();
            if (knappste.Zutat is null) break;

            var preis = w.Markt[knappste.Zutat].Preis;
            var platz = Math.Min(Daten.KapazitaetJeSorte, ziel[knappste.Zutat] - knappste.Bestand);
            var menge = (int)Math.Min(platz, Math.Floor(budget / preis));
            // Das Nötigste: Zutaten für nachkaufUnterStueck Stück, bezahlt aus der Kasse, auch über das freie Geld hinaus.
            var noetig = stockt ? Math.Min(platz, l.NachkaufUnterStueck * knappste.Menge - knappste.Bestand) : 0;
            if (menge < noetig)
            {
                menge = (int)Math.Min(noetig, Math.Floor(Math.Max(0, Heimat.Konto.Vermoegen) / preis));
                nurNoetigstes = true;
            }
            if (menge <= 0) break;
            var kosten = w.KarrenKaufpreis(knappste.Zutat, menge); // derselbe Preis wie beim Selbstfahren (ohne Händlerspanne)
            budget -= kosten;
            Heimat.ZutatenBuchen(kosten);
            _ladung[knappste.Zutat] = _ladung.GetValueOrDefault(knappste.Zutat) + menge;
        }

        // Höchstens einmal am Spieltag (= Spieljahr) je Betrieb melden, nicht bei jeder Fahrt.
        var tag = zeit.VergangeneMinuten / Spielzeit.MinutenProTag;
        if (nurNoetigstes && Heimat.KasseKnappGemeldetAm != tag)
        {
            Heimat.KasseKnappGemeldetAm = tag;
            w.Melden(Meldungsart.KasseKnapp, $"{Heimat.Name}: Verwalter kauft nur das Nötigste – die Kasse ist knapp.", betrieb: Heimat);
        }
    }

    /// <summary>
    /// Bis zu welchem Bestand der Verwalter jede Zutat auffüllt: den Bedarf von <see cref="LogistikDaten.ZutatenVorratStunden"/> Arbeitsstunden
    /// bei der jetzigen Leistung (eine Runde = von jeder Ware in Arbeit ein Stück), mindestens für 2 × <see cref="LogistikDaten.NachkaufUnterStueck"/>
    /// Stück (damit er nicht gleich wieder losfährt), höchstens bis zur Lagergrenze. Der Verwalter eines KI-Hauses legt im späten Herbst Vorrat für
    /// den Winter an (<see cref="Ki.KiRivale.WinterVorrat"/>, Datenversion 69) und, wenn es zu Jahresbeginn glaubt, dass es schlecht
    /// wird, einen Vorrat auf Verdacht (<see cref="Ki.KiRivale.Vorhalten"/>).
    /// </summary>
    private Dictionary<string, int> Vorratsziel(Stadtwirtschaft w)
    {
        var l = w.Daten.Logistik;
        var minutenJeRunde = Heimat.WarenInArbeit.Sum(Heimat.MinutenJeStueck);
        var rundenJeStunde = minutenJeRunde > 0 ? Kalkulation.Leistung(w, Heimat) * 60 / minutenJeRunde : 0;
        var runden = Math.Max(2.0 * l.NachkaufUnterStueck, rundenJeStunde * l.ZutatenVorratStunden);
        // Vorrat vor dem Winter und Vorrat auf Verdacht eines KI-Hauses (KiRivale.Jahreszeiten.cs, KiRivale.Vorrat.cs)
        var ki = w.Jahreszeiten.IstAktiv || w.Daten.Ki?.Vorrat is not null ? w.Rivalen.FirstOrDefault(r => r.Konto == Heimat.Konto) : null;
        return Heimat.Zutaten.ToDictionary(z => z.Key, z => Math.Max((int)Math.Min(Heimat.LagerKapazitaetJeWare, Math.Ceiling(runden * z.Value)),
            ki?.Vorhalten(Heimat, z.Key) ?? 0));
    }

    /// <summary>Jahresabschluss des Betriebs: Die Fahrten dieses Jahres werden zu denen des Vorjahrs.</summary>
    internal void JahrAbschliessen()
    {
        FahrtenVorjahr = _ganzesJahr ? FahrtenImJahr : null;
        FahrtenImJahr = 0;
        _ganzesJahr = true;
    }

    /// <summary>Vor dem Verkauf: Die Ladung kommt ins Lager des Betriebs, an dem er steht (wie bei jeder Heimkehr), ein vorgemerkter Auftrag verfällt.</summary>
    internal Dictionary<string, int> Abladen(Betrieb ort)
    {
        var abgeladen = new Dictionary<string, int>(_ladung);
        foreach (var (ware, stueck) in _ladung)
            ort.Einlagern(ware, stueck);
        _ladung.Clear();
        Vormerkung = null;
        return abgeladen;
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    internal KarrenStand Sichern() => new()
    {
        Name = Name, Typ = Typ, Zustand = Zustand, Ankunft = Ankunft, Ladung = new(_ladung),
        Eskorte = Eskorte, NachtsFahren = NachtsFahren, WareWartetSeit = _wareWartetSeit,
        TorWartenBis = TorWartenBis, RueckfahrtWartet = _rueckfahrtWartet, BrueckeWartenAb = BrueckeWartenAb, BrueckeWartenBis = BrueckeWartenBis,
        VerweiltBis = VerweiltBis, StandortBetrieb = StandortBetrieb, ZielBetrieb = ZielBetrieb, VonBetrieb = VonBetrieb,
        VonHandelsort = VonHandelsort ? true : null,
        Auftrag = Auftrag is null ? null : new(Auftrag.Kauf),
        VormerkungVerkauf = Vormerkung is null ? null : new(Vormerkung.Verkauf),
        VormerkungKauf = Vormerkung is null ? null : new(Vormerkung.Kauf),
        FahrtenImJahr = FahrtenImJahr, FahrtenVorjahr = FahrtenVorjahr, FahrtenGanzesJahr = _ganzesJahr, ZielHafen = ZielHafen,
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
        BrueckeWartenAb = s.BrueckeWartenAb;
        BrueckeWartenBis = s.BrueckeWartenBis;
        VerweiltBis = s.VerweiltBis;
        StandortBetrieb = s.StandortBetrieb;
        ZielBetrieb = s.ZielBetrieb;
        VonBetrieb = s.VonBetrieb;
        VonHandelsort = s.VonHandelsort ?? false;
        _rueckfahrtWartet = s.RueckfahrtWartet;
        Auftrag = s.Auftrag is null ? null : new Handelsplan(new Dictionary<string, int>(s.Auftrag));
        Vormerkung = s.VormerkungVerkauf is null || s.VormerkungKauf is null ? null
            : new Vormerkung(new Dictionary<string, int>(s.VormerkungVerkauf), new Dictionary<string, int>(s.VormerkungKauf));
        FahrtenImJahr = s.FahrtenImJahr;
        FahrtenVorjahr = s.FahrtenVorjahr;
        _ganzesJahr = s.FahrtenGanzesJahr;
        ZielHafen = s.ZielHafen;
    }

    /// <summary>Die Ladung geht verloren (Überfall).</summary>
    internal Dictionary<string, int> LadungVerlieren()
    {
        var verloren = new Dictionary<string, int>(_ladung);
        _ladung.Clear();
        return verloren;
    }
}
