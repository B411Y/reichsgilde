using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Mehrspieler;

/// <summary>
/// Ein Befehl in der Warteschlange: Er wirkt <b>am Anfang</b> des Ticks <paramref name="ZielTick"/> (<see cref="Zeit.Spielzeit.VergangeneMinuten"/>
/// vor dieser Minute), vor allem anderen, was in dieser Minute geschieht.
/// </summary>
/// <param name="Konto">Nummer des Absenders in <see cref="Stadtwirtschaft.Konten"/> (eine Nummer statt eines Objekts – so geht er übers Netz und in den Spielstand).</param>
/// <param name="Nummer">Laufende Nummer beim Einreihen: Reihenfolge unter den Befehlen desselben Hauses im selben Tick.</param>
public sealed record EingereihterBefehl(long ZielTick, int Konto, long Nummer, Befehl Befehl);

/// <summary>
/// Der Befehlsbote (Mehrspieler M2): der Weg der Befehle von Spielern zur <see cref="Stadtwirtschaft"/>.
///
/// Heute führt die Oberfläche einen Befehl mitten im Bild aus (<see cref="Stadtwirtschaft.Ausfuehren"/>). Im Mehrspieler im Gleichschritt
/// („Lockstep“) geht das nicht: Jeder Rechner rechnet selbst, und ein Befehl muss überall im selben Tick wirken. Darum reiht der Bote ihn ein
/// – mit einem <b>Ziel-Tick</b> – und führt alle fälligen Befehle am Anfang dieses Ticks aus, in fester Reihenfolge: nach Ziel-Tick, dann
/// Haus (Nummer des Kontos), dann laufender Nummer. Das Ergebnis kommt danach zurück, als Rückruf und als Ereignis <see cref="Ausgefuehrt"/>.
///
/// Im Einzelspiel ist die <see cref="Verzoegerung"/> 0: Der Befehl wirkt sofort beim Einreihen. Das ist genau der Zeitpunkt „Anfang des
/// nächsten Ticks“ – zwischen zwei Ticks geschieht in der Simulation nichts – und darum dasselbe wie früher. Auch bei Verzögerung wirkt ein
/// Befehl sofort, solange die Zeit steht (Pause, angehaltene Ratssitzung, Partie vorbei; <see cref="SofortWennZeitSteht"/>), sonst bräche die
/// Bedienung in der Pause.
///
/// KI-Häuser rechnet jeder Rechner selbst; ihre Befehle gehen weiter direkt an <see cref="Stadtwirtschaft.Ausfuehren"/>, mitten im Tick.
/// </summary>
public sealed class Befehlsbote
{
    private readonly Simulation _simulation;
    private readonly List<EingereihterBefehl> _warteschlange = new(); // stets in Ausführungsreihenfolge
    private readonly Dictionary<(int Konto, long Nummer), Action<Befehlsergebnis>> _rueckrufe = new(); // nur auf diesem Rechner, nie im Spielstand
    private int _verzoegerung;
    private long _ausgefuehrtBis = -1; // bis zu welchem Ziel-Tick zuletzt ausgeführt wurde (für das Protokoll)

    // Aufzeichnung (Befehlsprotokoll)
    private List<EingereihterBefehl>? _aufgezeichnet;
    private string _anfangszustand = "";
    private string? _luecke;

    internal Befehlsbote(Simulation simulation) => _simulation = simulation;

    /// <summary>
    /// Wie viele Ticks laufen, bevor ein eingereihter Befehl wirkt: 0 = sofort (Einzelspiel), im Mehrspieler etwa 2 – so viel Zeit hat der
    /// Befehl, um bei allen Rechnern anzukommen. Ziel-Tick = <see cref="Zeit.Spielzeit.VergangeneMinuten"/> + Verzögerung.
    /// </summary>
    public int Verzoegerung
    {
        get => _verzoegerung;
        set => _verzoegerung = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Die Verzögerung kann nicht negativ sein.");
    }

    /// <summary>
    /// Wirkt ein Befehl sofort, solange die Zeit steht (Standard: ja – Einzelspiel)? Im Gleichschritt wird das abgeschaltet; dort laufen
    /// Befehle in der Pause über einen Pausen-Takt des Gastgebers (Vorschlag in <c>docs/bereiche/kern.md</c>, M3).
    /// </summary>
    public bool SofortWennZeitSteht { get; set; } = true;

    /// <summary>Die eingereihten, noch nicht ausgeführten Befehle in der Reihenfolge, in der sie wirken werden.</summary>
    public IReadOnlyList<EingereihterBefehl> Warteschlange => _warteschlange;

    /// <summary>Die laufende Nummer des nächsten Befehls.</summary>
    public long NaechsteNummer { get; private set; }

    /// <summary>Steht die Zeit (Pause, angehaltene Ratssitzung, Partie vorbei)? Dann kommt kein Tick, der einen Befehl ausführen könnte.</summary>
    public bool ZeitSteht => _simulation.Takt.IstPausiert || _simulation.Ratssitzung is not null || _simulation.IstBeendet;

    /// <summary>Würde ein Befehl, der jetzt eingereiht wird, sofort wirken?</summary>
    public bool WirktSofort => Verzoegerung == 0 || (SofortWennZeitSteht && ZeitSteht);

    /// <summary>Nach jeder Ausführung: der Befehl und sein Ergebnis – auch für Befehle anderer Spieler (Mehrspieler) und ohne Rückruf.</summary>
    public event Action<EingereihterBefehl, Befehlsergebnis>? Ausgefuehrt;

    /// <summary>
    /// Reiht einen Befehl ein. Er wirkt am Anfang des Ticks „jetzt + <see cref="Verzoegerung"/>“ – oder sofort (<see cref="WirktSofort"/>);
    /// dann ist der Rückruf schon vor der Rückkehr gelaufen. Der Rückruf bekommt das Ergebnis genau einmal.
    /// </summary>
    /// <returns>Die laufende Nummer des Befehls.</returns>
    /// <exception cref="InvalidOperationException">Ohne Wirtschaft.</exception>
    /// <exception cref="ArgumentException">Wenn der Absender kein Haus dieser Stadt ist.</exception>
    public long Einreihen(Konto absender, Befehl befehl, Action<Befehlsergebnis>? rueckruf = null)
    {
        var konto = KontoNummer(absender);
        var sofort = WirktSofort;
        var jetzt = _simulation.Zeit.VergangeneMinuten;
        var eintrag = new EingereihterBefehl(sofort ? jetzt : jetzt + Verzoegerung, konto, NaechsteNummer, befehl);
        Aufnehmen(eintrag);
        if (rueckruf is not null) _rueckrufe[(eintrag.Konto, eintrag.Nummer)] = rueckruf;
        // Sofort heißt: alles, was bis jetzt fällig ist, in der festen Reihenfolge – ältere Befehle desselben Ticks zuerst.
        if (sofort) FaelligeAusfuehren(jetzt);
        return eintrag.Nummer;
    }

    /// <summary>
    /// Führt einen Befehl sofort aus und gibt das Ergebnis zurück – für Stellen, die es gleich für die weitere Logik brauchen (Aufbau für den
    /// Klicktest, Ketten aus Befehlen und Ticks). Geht nur, wenn ein Befehl ohnehin sofort wirkt (<see cref="WirktSofort"/>); im Gleichschritt
    /// müssen solche Stellen auf <see cref="Einreihen"/> mit Rückruf umgebaut werden.
    /// </summary>
    /// <exception cref="InvalidOperationException">Wenn der Befehl erst später wirken würde.</exception>
    public Befehlsergebnis SofortAusfuehren(Konto absender, Befehl befehl)
    {
        if (!WirktSofort)
            throw new InvalidOperationException($"Der Befehl wirkt erst in {Verzoegerung} Ticks – hier geht nur Einreihen mit Rückruf.");
        Befehlsergebnis? ergebnis = null;
        Einreihen(absender, befehl, e => ergebnis = e);
        return ergebnis ?? throw new InvalidOperationException("Der Befehl wurde nicht ausgeführt.");
    }

    /// <summary>
    /// Übernimmt einen Befehl, der woanders eingereiht wurde, mit seinem Ziel-Tick und seiner Nummer (später: vom Netz; heute: beim Abspielen
    /// eines <see cref="Befehlsprotokoll"/>s). Ein Ziel-Tick, der schon vorbei ist, wirkt am Anfang des nächsten Ticks.
    /// </summary>
    /// <exception cref="ArgumentException">Wenn es das Haus nicht gibt.</exception>
    public void Empfangen(EingereihterBefehl eintrag)
    {
        if (_simulation.Wirtschaft is not { } w) throw new InvalidOperationException("Ohne Wirtschaft gibt es keine Befehle.");
        if (eintrag.Konto < 0 || eintrag.Konto >= w.Konten.Count)
            throw new ArgumentException($"Ein Haus Nr. {eintrag.Konto} gibt es nicht.", nameof(eintrag));
        Aufnehmen(eintrag);
    }

    /// <summary>
    /// Am Anfang eines Ticks (aus <see cref="Simulation.Tick"/>, vor der Wirtschaft): alle Befehle mit Ziel-Tick bis <paramref name="tick"/>
    /// ausführen. Ein Rückruf darf neue Befehle einreihen; die laufen in ihrer Reihenfolge mit.
    /// </summary>
    internal void FaelligeAusfuehren(long tick)
    {
        if (_simulation.Wirtschaft is not { } w) return;
        _ausgefuehrtBis = Math.Max(_ausgefuehrtBis, tick);
        while (_warteschlange.Count > 0 && _warteschlange[0].ZielTick <= tick)
        {
            var eintrag = _warteschlange[0];
            _warteschlange.RemoveAt(0);
            var ergebnis = w.Ausfuehren(w.Konten[eintrag.Konto], eintrag.Befehl);
            if (_rueckrufe.Remove((eintrag.Konto, eintrag.Nummer), out var rueckruf)) rueckruf(ergebnis);
            Ausgefuehrt?.Invoke(eintrag, ergebnis);
        }
    }

    private void Aufnehmen(EingereihterBefehl eintrag)
    {
        // Einsortieren hinter alle, die vorher dran sind (meist ganz hinten – die Schlange ist kurz)
        var stelle = _warteschlange.Count;
        while (stelle > 0 && Vergleichen(_warteschlange[stelle - 1], eintrag) > 0) stelle--;
        _warteschlange.Insert(stelle, eintrag);
        NaechsteNummer = Math.Max(NaechsteNummer, eintrag.Nummer + 1);
        _aufgezeichnet?.Add(eintrag);
    }

    /// <summary>Die feste Reihenfolge: Ziel-Tick, dann Haus, dann laufende Nummer.</summary>
    private static int Vergleichen(EingereihterBefehl a, EingereihterBefehl b) =>
        a.ZielTick != b.ZielTick ? a.ZielTick.CompareTo(b.ZielTick)
        : a.Konto != b.Konto ? a.Konto.CompareTo(b.Konto)
        : a.Nummer.CompareTo(b.Nummer);

    private int KontoNummer(Konto konto)
    {
        if (_simulation.Wirtschaft is not { } w) throw new InvalidOperationException("Ohne Wirtschaft gibt es keine Befehle.");
        for (var i = 0; i < w.Konten.Count; i++)
            if (w.Konten[i] == konto) return i;
        throw new ArgumentException($"{konto.Inhaber} ist kein Haus dieser Stadt.", nameof(konto));
    }

    // ── Spielstand ──────────────────────────────────────────────────────────

    /// <summary>Beim Laden: die gespeicherte Warteschlange übernehmen (null: leer). Rückrufe gibt es danach keine.</summary>
    internal void Wiederherstellen(IEnumerable<EingereihterBefehl>? eintraege)
    {
        _warteschlange.Clear();
        _rueckrufe.Clear();
        NaechsteNummer = 0;
        foreach (var e in eintraege ?? Enumerable.Empty<EingereihterBefehl>())
        {
            if (_simulation.Wirtschaft is not { } w || e.Konto < 0 || e.Konto >= w.Konten.Count || e.Befehl is null)
                throw new InvalidDataException($"Ein eingereihter Befehl gehört zu keinem Haus (Nr. {e.Konto}).");
            Aufnehmen(e);
        }
    }

    // ── Aufzeichnen ─────────────────────────────────────────────────────────

    /// <summary>Wird gerade ein <see cref="Befehlsprotokoll"/> aufgezeichnet?</summary>
    public bool ZeichnetAuf => _aufgezeichnet is not null;

    /// <summary>
    /// Beginnt ein Befehlsprotokoll: Der Anfangszustand ist der Spielstand von jetzt (mit der Warteschlange), danach zählt jeder Befehl,
    /// der eingereiht oder empfangen wird. Eine laufende Aufzeichnung beginnt neu.
    /// </summary>
    /// <exception cref="InvalidOperationException">Während einer angehaltenen Ratssitzung (dann lässt sich nicht speichern).</exception>
    public void AufzeichnungBeginnen()
    {
        _anfangszustand = Speichern.Speicherung.Speichern(_simulation, zeitpunkt: DateTime.UnixEpoch);
        _aufgezeichnet = new List<EingereihterBefehl>();
        _luecke = null;
    }

    /// <summary>Beendet die Aufzeichnung und liefert das Protokoll bis jetzt.</summary>
    /// <exception cref="InvalidOperationException">Wenn keine Aufzeichnung läuft.</exception>
    public Befehlsprotokoll AufzeichnungBeenden()
    {
        if (_aufgezeichnet is null) throw new InvalidOperationException("Es läuft keine Aufzeichnung.");
        var protokoll = new Befehlsprotokoll(_anfangszustand, _aufgezeichnet, _simulation.Zeit.VergangeneMinuten,
            _ausgefuehrtBis >= _simulation.Zeit.VergangeneMinuten, _luecke);
        _aufgezeichnet = null;
        _anfangszustand = "";
        _luecke = null;
        return protokoll;
    }

    /// <summary>
    /// Etwas hat den Zustand geändert, das kein Befehl ist (heute: eine angehaltene Ratssitzung – ihre Schritte steuert die Oberfläche, M3).
    /// Das Protokoll gilt dann als unvollständig; der erste Grund bleibt.
    /// </summary>
    internal void LueckeMelden(string grund)
    {
        if (_aufgezeichnet is not null) _luecke ??= grund;
    }
}
