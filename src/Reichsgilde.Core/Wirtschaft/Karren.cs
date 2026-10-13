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

    /// <summary>Eskortenstufe, z. B. "Keine", "Normal", "Schwer".</summary>
    public string Eskorte { get; set; } = "Keine";

    /// <summary>
    /// Darf der Karren ins Umland auch in der Dunklen Nacht fahren?
    /// Standard: nein – nachts sind Überfälle doppelt so häufig.
    /// </summary>
    public bool NachtsFahren { get; set; }

    internal Karren(string name, string typ, KarrenDaten daten, Betrieb heimat)
    {
        Name = name;
        Typ = typ;
        Daten = daten;
        Heimat = heimat;
    }

    /// <summary>Kurzer Text für die Anzeige.</summary>
    public string ZustandText(Spielzeit zeit) => Zustand switch
    {
        KarrenZustand.AmBetrieb => "wartet am Betrieb",
        KarrenZustand.ZumMarkt => $"unterwegs zum Markt (noch {Math.Max(0, Ankunft - zeit.VergangeneMinuten)} Min.)",
        KarrenZustand.ZumBetrieb => $"auf dem Rückweg (noch {Math.Max(0, Ankunft - zeit.VergangeneMinuten)} Min.)",
        _ => Zustand.ToString(),
    };

    /// <summary>Eine Spielminute für diesen Karren.</summary>
    internal void Tick(Spielzeit zeit, Stadtwirtschaft w)
    {
        if (Zustand != KarrenZustand.AmBetrieb)
        {
            if (zeit.VergangeneMinuten >= Ankunft)
                Ankommen(zeit, w);
            return;
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
        var lagerFastVoll = ware.Max(x => x.Stueck) >= Heimat.LagerKapazitaetJeWare * l.AbfahrtAbFuellgrad;
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

    private void Losfahren(KarrenZustand ziel, Spielzeit zeit, Stadtwirtschaft w)
    {
        Zustand = ziel;
        Ankunft = zeit.VergangeneMinuten + w.Daten.Logistik.FahrzeitAb(Heimat.Ort);

        if (_ladung.Count == 0)
            return; // Leere Karren kosten nichts und lohnen keinen Überfall.

        var eskorte = w.Daten.Logistik.Eskorten.GetValueOrDefault(Eskorte) ?? new EskorteDaten();
        if (eskorte.KostenJeFahrt > 0)
            Heimat.Konto.Transport(eskorte.KostenJeFahrt);

        if (Heimat.Ort == Arbeitsort.Umland)
            Ueberfall.Wuerfeln(this, eskorte, zeit, w);
    }

    private void Ankommen(Spielzeit zeit, Stadtwirtschaft w)
    {
        if (Zustand == KarrenZustand.ZumMarkt)
        {
            // Alles verkaufen ...
            foreach (var (ware, stueck) in _ladung)
                Heimat.Verkaufen(ware, stueck, w.Markt);
            _ladung.Clear();

            // ... und knappe Zutaten einkaufen.
            ZutatenEinkaufen(w);
            Losfahren(KarrenZustand.ZumBetrieb, zeit, w);
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
            Heimat.Konto.Zutaten(w.Markt[knappste.Zutat].Kaufen(menge));
            _ladung[knappste.Zutat] = _ladung.GetValueOrDefault(knappste.Zutat) + menge;
        }
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    internal KarrenStand Sichern() => new()
    {
        Name = Name, Typ = Typ, Zustand = Zustand, Ankunft = Ankunft, Ladung = new(_ladung),
        Eskorte = Eskorte, NachtsFahren = NachtsFahren, WareWartetSeit = _wareWartetSeit,
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
    }

    /// <summary>Die Ladung geht verloren (Überfall).</summary>
    internal Dictionary<string, int> LadungVerlieren()
    {
        var verloren = new Dictionary<string, int>(_ladung);
        _ladung.Clear();
        return verloren;
    }
}
