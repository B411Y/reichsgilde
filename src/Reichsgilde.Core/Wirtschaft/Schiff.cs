using Reichsgilde.Core.Speichern;

namespace Reichsgilde.Core.Wirtschaft;

public enum SchiffZustand
{
    /// <summary>Liegt leer am Kai und wartet auf einen Auftrag.</summary>
    AmKai,

    /// <summary>Wird am Kai beladen und legt danach ab.</summary>
    Beladen,

    /// <summary>Unterwegs zur fernen Stadt.</summary>
    Hinfahrt,

    /// <summary>Liegt in der fernen Stadt: verkauft die Hinfracht, kauft die Rückfracht.</summary>
    InFernerStadt,

    /// <summary>Unterwegs zurück zum Hafen.</summary>
    Rueckfahrt,
}

/// <summary>
/// Ein Auftrag für ein Schiff: welche ferne Stadt, was hin (Hinfracht) und was zurück (Rückfracht).
/// </summary>
/// <param name="Ziel">Name der fernen Stadt.</param>
/// <param name="Hinfracht">Ware → Stück, die das Schiff mitnimmt und dort verkauft.</param>
/// <param name="Rueckfracht">Ware → Stück, die es dort kauft und heimbringt (so viel das Geld reicht).</param>
/// <param name="Betrieb">
/// Eigener Betrieb (Name) oder null. Mit Betrieb kommt die Hinfracht aus seinem Lager, und die Rückfracht geht in sein Lager (was nicht
/// mehr hineinpasst, wird am Markt verkauft). Ohne Betrieb wird die Hinfracht am Markt der Stadt gekauft und die Rückfracht dort verkauft.
/// </param>
/// <param name="Wiederholen">Fährt das Schiff nach der Rückkehr mit demselben Auftrag wieder los?</param>
public sealed record Schiffsauftrag(
    string Ziel,
    IReadOnlyDictionary<string, int> Hinfracht,
    IReadOnlyDictionary<string, int> Rueckfracht,
    string? Betrieb = null,
    bool Wiederholen = false);

/// <summary>Wo ein Schiff gerade zu sehen ist (für die Darstellung).</summary>
/// <param name="Sichtbar">Auf der Karte zu sehen? Hinter dem Kartenrand (und in der fernen Stadt) nicht.</param>
/// <param name="Ort">Die Stelle auf dem Fluss (am Kai: der Liegeplatz).</param>
/// <param name="RichtungX">Fahrtrichtung (Einheitsvektor); am Kai die Richtung vom Kai aufs Wasser.</param>
/// <param name="Fortschritt">Anteil der laufenden Fahrt (0 … 1); am Kai und beim Beladen 0, in der fernen Stadt 1.</param>
public readonly record struct Schiffslage(bool Sichtbar, Kartenwerkstatt.Kartenort Ort, double RichtungX, double RichtungZ, double Fortschritt);

/// <summary>
/// Ein Schiff eines Hauses. Es liegt an einem Hafen der Karte und fährt mit Auftrag zu einer fernen Stadt – ein Zustandsautomat wie der Karren:
///
///   AmKai ──(Auftrag: Hinfracht laden)──▶ Beladen ──(Ladezeit vorbei: Zoll, Geleit, Piraten)──▶ Hinfahrt
///   Hinfahrt ──(Ankunft: verkaufen, Rückfracht kaufen)──▶ InFernerStadt ──(Liegezeit vorbei: Zoll, Geleit, Piraten)──▶ Rückfahrt
///   Rückfahrt ──(Ankunft: ins Lager oder auf den Markt)──▶ AmKai (oder gleich wieder Beladen, wenn der Auftrag sich wiederholt)
///
/// Die Regeln stehen in <see cref="Schiffahrt"/>; das Schiff trägt nur seinen Zustand.
/// </summary>
public sealed class Schiff
{
    private readonly Dictionary<string, int> _ladung = new();

    public string Name { get; }
    public string Typ { get; }
    public SchiffstypDaten Daten { get; }
    public Konto Besitzer { get; }

    /// <summary>Kennung des Hafens (Gebäude der Karte), an dem das Schiff liegt.</summary>
    public string Hafen { get; }

    public SchiffZustand Zustand { get; internal set; } = SchiffZustand.AmKai;

    /// <summary>Spielminute, zu der der laufende Zustand begann (Beladen, Fahrt, Liegezeit).</summary>
    public long Beginn { get; internal set; }

    /// <summary>Spielminute, zu der der laufende Zustand endet (nur unterwegs, beim Beladen und in der fernen Stadt von Bedeutung).</summary>
    public long Ende { get; internal set; }

    /// <summary>Was das Schiff gerade geladen hat: Ware → Stück.</summary>
    public IReadOnlyDictionary<string, int> Ladung => _ladung;

    /// <summary>Der laufende Auftrag (null am Kai ohne Auftrag).</summary>
    public Schiffsauftrag? Auftrag { get; internal set; }

    /// <summary>Geleitstufe wie bei den Karren (<c>logistik.eskorten</c>), z. B. „Keine“, „Normal“, „Schwer“.</summary>
    public string Geleit { get; internal set; } = "Keine";

    /// <summary>Was die letzte Fahrt gebracht hat (für die Anzeige; nicht gespeichert).</summary>
    public string? LetzteFahrt { get; internal set; }

    /// <summary>Was die Fahrten im laufenden Jahr eingebracht haben: Erlöse − Einkauf − Zoll − Geleit (ohne Unterhalt und Kaufpreis).</summary>
    public double ErtragLaufend { get; internal set; }

    /// <summary>Dasselbe für das Vorjahr, dazu der Unterhalt abgezogen (null im ersten Jahr).</summary>
    public double? ErtragVorjahr { get; internal set; }

    /// <summary>Abgeschlossene Fahrten (hin und zurück) seit dem Kauf.</summary>
    public int Fahrten { get; internal set; }

    internal Schiff(string name, string typ, SchiffstypDaten daten, Konto besitzer, string hafen)
    {
        Name = name;
        Typ = typ;
        Daten = daten;
        Besitzer = besitzer;
        Hafen = hafen;
    }

    /// <summary>Ist das Schiff unterwegs (nicht am Kai und nicht beim Beladen)?</summary>
    public bool IstUnterwegs => Zustand is SchiffZustand.Hinfahrt or SchiffZustand.InFernerStadt or SchiffZustand.Rueckfahrt;

    /// <summary>Stück an Bord.</summary>
    public int Geladen => _ladung.Values.Sum();

    internal void Laden(string ware, int stueck)
    {
        if (stueck > 0) _ladung[ware] = _ladung.GetValueOrDefault(ware) + stueck;
    }

    /// <summary>Nimmt alles von Bord.</summary>
    internal Dictionary<string, int> Entladen()
    {
        var alles = new Dictionary<string, int>(_ladung);
        _ladung.Clear();
        return alles;
    }

    /// <summary>Piraten: Von jeder Ware geht dieser Anteil verloren (aufgerundet). Gibt die Beute zurück.</summary>
    internal Dictionary<string, int> Ausrauben(double anteil)
    {
        var beute = new Dictionary<string, int>();
        foreach (var ware in _ladung.Keys.OrderBy(w => w, StringComparer.Ordinal).ToList())
        {
            var stueck = Math.Min(_ladung[ware], (int)Math.Ceiling(_ladung[ware] * anteil - 1e-9));
            if (stueck <= 0) continue;
            beute[ware] = stueck;
            _ladung[ware] -= stueck;
            if (_ladung[ware] == 0) _ladung.Remove(ware);
        }
        return beute;
    }

    // ── Speichern ───────────────────────────────────────────────────────────

    internal SchiffStand Sichern(int konto) => new()
    {
        Name = Name, Typ = Typ, Konto = konto, Hafen = Hafen, Zustand = Zustand, Beginn = Beginn, Ende = Ende,
        Ladung = new(_ladung), Geleit = Geleit,
        Ziel = Auftrag?.Ziel, Betrieb = Auftrag?.Betrieb, Wiederholen = Auftrag?.Wiederholen ?? false,
        Hinfracht = Auftrag is null ? null : new(Auftrag.Hinfracht),
        Rueckfracht = Auftrag is null ? null : new(Auftrag.Rueckfracht),
        ErtragLaufend = ErtragLaufend, ErtragVorjahr = ErtragVorjahr, Fahrten = Fahrten,
    };

    internal void Wiederherstellen(SchiffStand s)
    {
        Zustand = s.Zustand;
        Beginn = s.Beginn;
        Ende = s.Ende;
        _ladung.Clear();
        foreach (var (ware, stueck) in s.Ladung) _ladung[ware] = stueck;
        Geleit = s.Geleit;
        Auftrag = s.Ziel is null ? null
            : new Schiffsauftrag(s.Ziel, new Dictionary<string, int>(s.Hinfracht ?? new()), new Dictionary<string, int>(s.Rueckfracht ?? new()),
                s.Betrieb, s.Wiederholen);
        ErtragLaufend = s.ErtragLaufend;
        ErtragVorjahr = s.ErtragVorjahr;
        Fahrten = s.Fahrten;
    }
}
