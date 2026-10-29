using System.Runtime.ExceptionServices;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Mehrspieler;

/// <summary>
/// Ein Befehl in der Warteschlange: Er wirkt <b>am Anfang</b> des Ticks <paramref name="ZielTick"/> (<see cref="Zeit.Spielzeit.VergangeneMinuten"/>
/// vor dieser Minute), vor allem anderen, was in dieser Minute geschieht.
/// </summary>
/// <param name="Konto">Nummer des Absenders in <see cref="Stadtwirtschaft.Konten"/> (eine Nummer statt eines Objekts – so geht er übers Netz und in den Spielstand).</param>
/// <param name="Nummer">Laufende Nummer beim Einreihen: Reihenfolge unter den Befehlen desselben Hauses im selben Tick.</param>
/// <param name="Sofort">Wirkte er sofort beim Einreihen (Einzelspiel, Pause)? Solche Befehle stehen nur im Befehlsprotokoll und kommen in
/// ihrem Tick vor den wartenden – genau so geschah es auch: Sie wirkten vor dem Anfang dieses Ticks.</param>
public sealed record EingereihterBefehl(long ZielTick, int Konto, long Nummer, Befehl Befehl, bool Sofort = false);

/// <summary>
/// Der Befehlsbote (Mehrspieler M2): der Weg der Befehle von Spielern zur <see cref="Stadtwirtschaft"/>.
///
/// Heute führt die Oberfläche einen Befehl mitten im Bild aus (<see cref="Stadtwirtschaft.Ausfuehren"/>). Im Mehrspieler im Gleichschritt
/// („Lockstep“) geht das nicht: Jeder Rechner rechnet selbst, und ein Befehl muss überall im selben Tick wirken. Darum reiht der Bote ihn ein
/// – mit einem <b>Ziel-Tick</b> – und führt alle fälligen Befehle am Anfang dieses Ticks aus, in fester Reihenfolge: nach Ziel-Tick, dann die
/// sofort ausgeführten vor den wartenden, dann Haus (Nummer des Kontos), dann laufende Nummer. Das Ergebnis kommt danach zurück, als Rückruf
/// und als Ereignis <see cref="Ausgefuehrt"/>.
///
/// Im Einzelspiel ist die <see cref="Verzoegerung"/> 0: Der Befehl wirkt sofort beim Einreihen. Das ist genau der Zeitpunkt „Anfang des
/// nächsten Ticks“ – zwischen zwei Ticks geschieht in der Simulation nichts – und darum dasselbe wie früher. Auch bei Verzögerung wirkt ein
/// Befehl sofort, solange die Zeit steht (Pause, angehaltene Ratssitzung, Partie vorbei; <see cref="SofortWennZeitSteht"/>), sonst bräche die
/// Bedienung in der Pause. Sofort wirkt immer nur der neue Befehl; wartende bleiben bei ihrem Tick.
///
/// Zwei Ausnahmen, damit das Befehlsprotokoll stimmt:
///   • <b>Während eines Ticks</b> (z. B. ein Rückruf oder eine Meldung der Wirtschaft, die einen Befehl auslöst) wirkt nichts sofort – der
///     Befehl wartet auf den Anfang des nächsten Ticks (+ Verzögerung).
///   • <b>Während ein Befehl wirkt</b>, wartet ein sofortiger Befehl, bis dieser fertig ist, und kommt gleich danach.
///
/// KI-Häuser rechnet jeder Rechner selbst; ihre Befehle gehen weiter direkt an <see cref="Stadtwirtschaft.Ausfuehren"/>, mitten im Tick.
/// </summary>
public sealed class Befehlsbote
{
    private readonly Simulation _simulation;
    private readonly List<EingereihterBefehl> _warteschlange = new(); // stets in Ausführungsreihenfolge
    private readonly Dictionary<(int Konto, long Nummer), Action<Befehlsergebnis>> _rueckrufe = new(); // nur auf diesem Rechner, nie im Spielstand
    private readonly Queue<EingereihterBefehl> _nachgereicht = new(); // sofortige Befehle, die während einer Ausführung kamen
    private int _verzoegerung;
    private long? _tickMinute; // läuft gerade ein Tick? Dann seine Minute
    private bool _fuehrtAus;   // wirkt gerade ein Befehl?

    // Aufzeichnung (Befehlsprotokoll)
    private List<EingereihterBefehl>? _aufgezeichnet;
    private string _anfangszustand = "";
    private string? _luecke;

    internal Befehlsbote(Simulation simulation) => _simulation = simulation;

    /// <summary>
    /// Wie viele Ticks laufen, bevor ein eingereihter Befehl wirkt: 0 = sofort (Einzelspiel), im Mehrspieler etwa 2 – so viel Zeit hat der
    /// Befehl, um bei allen Rechnern anzukommen. Ziel-Tick = <see cref="FruehesterTick"/> + Verzögerung.
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

    /// <summary>Die laufende Nummer des nächsten Befehls (nach dem Laden: hinter dem höchsten Befehl der Schlange, sonst 0).</summary>
    public long NaechsteNummer { get; private set; }

    /// <summary>Steht die Zeit (Pause, angehaltene Ratssitzung, Partie vorbei)? Dann kommt kein Tick, der einen Befehl ausführen könnte.</summary>
    public bool ZeitSteht => _simulation.Takt.IstPausiert || _simulation.Ratssitzung is not null || _simulation.IstBeendet;

    /// <summary>Läuft gerade ein Tick (auch seine Ereignisse und die Rückrufe an seinem Anfang)?</summary>
    public bool ImTick => _tickMinute is not null;

    /// <summary>Würde ein Befehl, der jetzt eingereiht wird, sofort wirken? Nie während eines Ticks.</summary>
    public bool WirktSofort => !ImTick && (Verzoegerung == 0 || (SofortWennZeitSteht && ZeitSteht));

    /// <summary>
    /// Der früheste Tick, an dessen Anfang ein Befehl noch wirken kann: der nächste, der beginnt (<see cref="Zeit.Spielzeit.VergangeneMinuten"/>)
    /// – während eines Ticks der danach.
    /// </summary>
    public long FruehesterTick => _tickMinute + 1 ?? _simulation.Zeit.VergangeneMinuten;

    /// <summary>Nach jeder Ausführung: der Befehl und sein Ergebnis – auch für Befehle anderer Spieler (Mehrspieler) und ohne Rückruf.</summary>
    public event Action<EingereihterBefehl, Befehlsergebnis>? Ausgefuehrt;

    /// <summary>
    /// Reiht einen Befehl ein. Er wirkt am Anfang des Ticks <see cref="FruehesterTick"/> + <see cref="Verzoegerung"/> – oder sofort
    /// (<see cref="WirktSofort"/>); dann ist der Rückruf schon vor der Rückkehr gelaufen (kam der Befehl, während ein anderer wirkt: gleich
    /// nach diesem). Der Rückruf bekommt das Ergebnis genau einmal – bei Spielende ein abgelehntes, wenn der Befehl nicht mehr wirkte.
    /// </summary>
    /// <returns>Die laufende Nummer des Befehls.</returns>
    /// <exception cref="InvalidOperationException">Ohne Wirtschaft.</exception>
    /// <exception cref="ArgumentException">Wenn der Absender kein Haus dieser Stadt ist.</exception>
    public long Einreihen(Konto absender, Befehl befehl, Action<Befehlsergebnis>? rueckruf = null)
    {
        var konto = KontoNummer(absender);
        var sofort = WirktSofort;
        var eintrag = new EingereihterBefehl(FruehesterTick + (sofort ? 0 : Verzoegerung), konto, NaechsteNummer, befehl, sofort);
        NaechsteNummer++;
        _aufgezeichnet?.Add(eintrag);
        if (rueckruf is not null) _rueckrufe[(eintrag.Konto, eintrag.Nummer)] = rueckruf;
        if (!sofort)
            Einsortieren(eintrag);
        else if (_fuehrtAus)
            _nachgereicht.Enqueue(eintrag); // nach dem Befehl, der gerade wirkt
        else
            Abarbeiten(new[] { eintrag });
        return eintrag.Nummer;
    }

    /// <summary>
    /// Führt einen Befehl sofort aus und gibt das Ergebnis zurück – für Stellen, die es gleich für die weitere Logik brauchen (Aufbau für den
    /// Klicktest, Ketten aus Befehlen und Ticks). Geht nur, wenn ein Befehl ohnehin sofort wirkt (<see cref="WirktSofort"/>) und nicht gerade
    /// ein anderer wirkt; im Gleichschritt müssen solche Stellen auf <see cref="Einreihen"/> mit Rückruf umgebaut werden.
    /// </summary>
    /// <exception cref="InvalidOperationException">Wenn der Befehl erst später wirken würde.</exception>
    public Befehlsergebnis SofortAusfuehren(Konto absender, Befehl befehl)
    {
        if (!WirktSofort)
            throw new InvalidOperationException(ImTick
                ? "Während eines Ticks wirkt kein Befehl sofort – hier geht nur Einreihen mit Rückruf."
                : $"Der Befehl wirkt erst in {Verzoegerung} Ticks – hier geht nur Einreihen mit Rückruf.");
        if (_fuehrtAus)
            throw new InvalidOperationException("Während ein Befehl wirkt, kommt ein neuer erst danach – hier geht nur Einreihen mit Rückruf.");
        Befehlsergebnis? ergebnis = null;
        Einreihen(absender, befehl, e => ergebnis = e);
        return ergebnis ?? throw new InvalidOperationException("Der Befehl wurde nicht ausgeführt.");
    }

    /// <summary>
    /// Übernimmt einen Befehl, der woanders eingereiht wurde, mit seinem Ziel-Tick und seiner Nummer (später: vom Netz; heute: beim Abspielen
    /// eines <see cref="Befehlsprotokoll"/>s). Er wartet immer auf seinen Tick, auch wenn er dort sofort wirkte.
    /// </summary>
    /// <exception cref="ArgumentException">Wenn es das Haus nicht gibt oder der Ziel-Tick vor <see cref="FruehesterTick"/> liegt (zu spät
    /// angekommen – im Gleichschritt ein Fehler, der Rechner liefe auseinander).</exception>
    public void Empfangen(EingereihterBefehl eintrag)
    {
        if (_simulation.Wirtschaft is not { } w) throw new InvalidOperationException("Ohne Wirtschaft gibt es keine Befehle.");
        if (eintrag.Konto < 0 || eintrag.Konto >= w.Konten.Count)
            throw new ArgumentException($"Ein Haus Nr. {eintrag.Konto} gibt es nicht.", nameof(eintrag));
        if (eintrag.ZielTick < FruehesterTick)
            throw new ArgumentException($"Der Befehl kam zu spät: Ziel-Tick {eintrag.ZielTick}, frühestens geht {FruehesterTick}.", nameof(eintrag));
        _aufgezeichnet?.Add(eintrag);
        NaechsteNummer = Math.Max(NaechsteNummer, eintrag.Nummer + 1);
        Einsortieren(eintrag);
    }

    // ── Ablauf eines Ticks (aus Simulation.Tick) ────────────────────────────

    /// <summary>Am Anfang eines Ticks, vor der Wirtschaft: Ab jetzt wirkt nichts mehr sofort; alle Befehle mit Ziel-Tick bis zu dieser Minute wirken.</summary>
    internal void TickBeginnt(long tick)
    {
        _tickMinute = tick;
        FaelligeAusfuehren(tick);
    }

    /// <summary>Am Ende eines Ticks (auch wenn er abbricht).</summary>
    internal void TickEndet() => _tickMinute = null;

    /// <summary>Alle Befehle mit Ziel-Tick bis <paramref name="tick"/> ausführen (nur die sofortigen: am Ende des Abspielens).</summary>
    internal void FaelligeAusfuehren(long tick, bool nurSofortige = false)
    {
        var faellig = new List<EingereihterBefehl>();
        for (var i = 0; i < _warteschlange.Count && _warteschlange[i].ZielTick <= tick; i++)
            if (!nurSofortige || _warteschlange[i].Sofort)
                faellig.Add(_warteschlange[i]);
        if (faellig.Count == 0) return;
        _warteschlange.RemoveAll(faellig.Contains);
        Abarbeiten(faellig);
    }

    /// <summary>
    /// Führt die Befehle der Reihe nach aus – und danach, was währenddessen sofort dazukam (aus der Wirtschaft, einem Rückruf oder einem
    /// Empfänger von <see cref="Ausgefuehrt"/>), in der Reihenfolge der Nummern. Wirft ein Befehl (ein Fehler im Kern), gilt er als abgelehnt;
    /// wirft ein Rückruf oder ein Empfänger (ein Fehler der Oberfläche), laufen die übrigen Befehle trotzdem, und der erste Fehler kommt danach
    /// an den Aufrufer.
    /// </summary>
    private void Abarbeiten(IEnumerable<EingereihterBefehl> befehle)
    {
        if (_simulation.Wirtschaft is not { } w) return;
        ExceptionDispatchInfo? fehler = null;
        _fuehrtAus = true;
        try
        {
            foreach (var eintrag in befehle.Concat(Nachgereichte()))
            {
                Befehlsergebnis ergebnis;
                try { ergebnis = w.Ausfuehren(w.Konten[eintrag.Konto], eintrag.Befehl); }
                catch (Exception ex) { ergebnis = Befehlsergebnis.Abgelehnt($"Fehler im Kern beim Befehl {eintrag.Befehl.GetType().Name}: {ex.Message}"); }
                Melden(eintrag, ergebnis, ref fehler);
            }
        }
        finally { _fuehrtAus = false; }
        fehler?.Throw();
    }

    /// <summary>Die nachgereichten sofortigen Befehle – auch solche, die erst während der Schleife dazukommen.</summary>
    private IEnumerable<EingereihterBefehl> Nachgereichte()
    {
        while (_nachgereicht.Count > 0) yield return _nachgereicht.Dequeue();
    }

    /// <summary>Rückruf (aus dem Wörterbuch genommen, also höchstens einmal) und Ereignis; ein Fehler darin hält die anderen nicht auf.</summary>
    private void Melden(EingereihterBefehl eintrag, Befehlsergebnis ergebnis, ref ExceptionDispatchInfo? fehler)
    {
        if (_rueckrufe.Remove((eintrag.Konto, eintrag.Nummer), out var rueckruf))
            try { rueckruf(ergebnis); }
            catch (Exception ex) { fehler ??= ExceptionDispatchInfo.Capture(ex); }
        try { Ausgefuehrt?.Invoke(eintrag, ergebnis); }
        catch (Exception ex) { fehler ??= ExceptionDispatchInfo.Capture(ex); }
    }

    /// <summary>
    /// Bei Spielende: Wartende Befehle wirken nicht mehr (es kommt kein Tick). Ihre Rückrufe bekommen ein abgelehntes Ergebnis, die Schlange
    /// ist danach leer. Kein <see cref="Ausgefuehrt"/> – ausgeführt wurde nichts.
    /// </summary>
    internal void Verwerfen(string grund)
    {
        // Sofortige stehen nur beim Abspielen in der Schlange – sie wirkten auch nach dem Ende noch und bleiben
        var wartend = _warteschlange.Where(e => !e.Sofort).ToList();
        _warteschlange.RemoveAll(e => !e.Sofort);
        ExceptionDispatchInfo? fehler = null;
        foreach (var eintrag in wartend)
            if (_rueckrufe.Remove((eintrag.Konto, eintrag.Nummer), out var rueckruf))
                try { rueckruf(Befehlsergebnis.Abgelehnt(grund)); }
                catch (Exception ex) { fehler ??= ExceptionDispatchInfo.Capture(ex); }
        fehler?.Throw();
    }

    private void Einsortieren(EingereihterBefehl eintrag)
    {
        // Hinter alle, die vorher dran sind (meist ganz hinten – die Schlange ist kurz)
        var stelle = _warteschlange.Count;
        while (stelle > 0 && Vergleichen(_warteschlange[stelle - 1], eintrag) > 0) stelle--;
        _warteschlange.Insert(stelle, eintrag);
    }

    /// <summary>Die feste Reihenfolge: Ziel-Tick, dann die sofortigen vor den wartenden, dann Haus, dann laufende Nummer.</summary>
    private static int Vergleichen(EingereihterBefehl a, EingereihterBefehl b) =>
        a.ZielTick != b.ZielTick ? a.ZielTick.CompareTo(b.ZielTick)
        : a.Sofort != b.Sofort ? (a.Sofort ? -1 : 1)
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

    /// <summary>
    /// Beim Laden: die gespeicherte Warteschlange übernehmen (null: leer). Rückrufe gibt es danach keine; die laufende Nummer zählt hinter
    /// dem höchsten Befehl weiter (sie ordnet nur Befehle desselben Hauses im selben Tick).
    /// </summary>
    internal void Wiederherstellen(IEnumerable<EingereihterBefehl>? eintraege)
    {
        _warteschlange.Clear();
        _rueckrufe.Clear();
        _nachgereicht.Clear();
        NaechsteNummer = 0;
        foreach (var e in eintraege ?? Enumerable.Empty<EingereihterBefehl>())
        {
            if (_simulation.Wirtschaft is not { } w || e.Konto < 0 || e.Konto >= w.Konten.Count || e.Befehl is null)
                throw new InvalidDataException($"Ein eingereihter Befehl gehört zu keinem Haus (Nr. {e.Konto}).");
            NaechsteNummer = Math.Max(NaechsteNummer, e.Nummer + 1);
            Einsortieren(e);
        }
    }

    // ── Aufzeichnen ─────────────────────────────────────────────────────────

    /// <summary>Wird gerade ein <see cref="Befehlsprotokoll"/> aufgezeichnet?</summary>
    public bool ZeichnetAuf => _aufgezeichnet is not null;

    /// <summary>
    /// Beginnt ein Befehlsprotokoll: Der Anfangszustand ist der Spielstand von jetzt (mit der Warteschlange), danach zählt jeder Befehl,
    /// der eingereiht oder empfangen wird. Eine laufende Aufzeichnung beginnt neu.
    /// </summary>
    /// <exception cref="InvalidOperationException">Während einer angehaltenen Ratssitzung (dann lässt sich nicht speichern) oder eines Ticks.</exception>
    public void AufzeichnungBeginnen()
    {
        if (ImTick || _fuehrtAus) throw new InvalidOperationException("Eine Aufzeichnung beginnt zwischen zwei Ticks.");
        _anfangszustand = Speichern.Speicherung.Speichern(_simulation, zeitpunkt: DateTime.UnixEpoch);
        _aufgezeichnet = new List<EingereihterBefehl>();
        _luecke = null;
    }

    /// <summary>Beendet die Aufzeichnung und liefert das Protokoll bis jetzt.</summary>
    /// <exception cref="InvalidOperationException">Wenn keine Aufzeichnung läuft.</exception>
    public Befehlsprotokoll AufzeichnungBeenden()
    {
        if (_aufgezeichnet is null) throw new InvalidOperationException("Es läuft keine Aufzeichnung.");
        var protokoll = new Befehlsprotokoll(_anfangszustand, _aufgezeichnet, _simulation.Zeit.VergangeneMinuten, _luecke);
        _aufgezeichnet = null;
        _anfangszustand = "";
        _luecke = null;
        return protokoll;
    }

    /// <summary>
    /// Etwas hat den Zustand geändert, das kein Befehl ist (heute: eine angehaltene Ratssitzung – ihre Schritte steuert die Oberfläche – und
    /// <see cref="Simulation.Beenden"/> von außen; M3). Das Protokoll gilt dann als unvollständig; der erste Grund bleibt.
    /// </summary>
    internal void LueckeMelden(string grund)
    {
        if (_aufgezeichnet is not null) _luecke ??= grund;
    }
}
