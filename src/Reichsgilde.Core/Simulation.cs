using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core;

/// <summary>
/// Das Herz des Spiels: die gesamte Spielwelt und ihr Ablauf.
///
/// Heute: Zeit, Spielende und – wenn Wirtschaftsdaten übergeben werden – Markt und Betriebe.
/// Nach und nach kommen hier Bürger, Dynastien, Rat und Gericht dazu.
///
/// Godot (die Darstellung) ruft nur <see cref="Aktualisieren"/> auf und liest danach
/// den Zustand aus. Im Multiplayer läuft genau diese Klasse später auf dem Server.
/// </summary>
public sealed class Simulation
{
    /// <summary>
    /// Platzhalter, solange es noch keine Dynastien gibt (kommt in Phase 2/5).
    /// Zwei „lebende" Dynastien verhindern, dass „Letzte Dynastie gewinnt" sofort auslöst.
    /// </summary>
    private const int PlatzhalterLebendeDynastien = 2;

    public Partieeinstellungen Einstellungen { get; }
    public Spielzeit Zeit { get; }
    public Taktgeber Takt { get; } = new();

    /// <summary>
    /// Der Weg der Befehle von Spielern (Mehrspieler M2): Warteschlange mit Ziel-Tick, Ergebnis als Rückruf, Aufzeichnung als Protokoll.
    /// Im Einzelspiel wirkt ein Befehl sofort (Verzögerung 0) – wie <see cref="Stadtwirtschaft.Ausfuehren"/>.
    /// </summary>
    public Mehrspieler.Befehlsbote Bote { get; }

    /// <summary>Markt, Betriebe und Konten. Null, wenn die Simulation ohne Wirtschaftsdaten läuft.</summary>
    public Stadtwirtschaft? Wirtschaft { get; }

    /// <summary>Ist die Partie vorbei (Siegbedingung oder <see cref="Beenden"/>)? Dann läuft keine Zeit mehr.</summary>
    public bool IstBeendet { get; private set; }

    /// <summary>Warum die Partie endete (null, solange sie läuft).</summary>
    public string? Endgrund { get; private set; }

    /// <summary>Wird ausgelöst, wenn eine neue Tagesphase beginnt (z. B. Vesper).</summary>
    public event Action<Tagesphase>? PhaseBegonnen;

    /// <summary>
    /// Wird am Ende jeder Runde ausgelöst – das ist die Jahresabrechnung.
    /// Übergeben wird das Jahr, das gerade zu Ende gegangen ist.
    /// Später: Steuern, Zinsen, Geburten, Alterung, Erbschaften ...
    /// </summary>
    public event Action<int>? JahrBeendet;

    /// <summary>Wird genau einmal ausgelöst, wenn die Siegbedingung erfüllt ist.</summary>
    public event Action? PartieBeendet;

    /// <summary>
    /// Die Ratssitzung, vor der die Partie am Jahresende anhält (null = keine): Die Zeit ruht, <see cref="Aktualisieren"/> und
    /// <see cref="Tick"/> tun nichts, bis <see cref="RatssitzungWeiter"/> den letzten Punkt abgehalten hat. Ob angehalten wird,
    /// entscheidet <see cref="Stadtwirtschaft.RatssitzungAnhalten"/>.
    /// </summary>
    public Politik.Ratssitzung? Ratssitzung => Wirtschaft?.Rat.LaufendeSitzung;

    /// <summary>Was nach der angehaltenen Sitzung noch aussteht: das beendete Jahr und die Tagesphase vor dem Jahreswechsel.</summary>
    private (int Jahr, Tagesphase AltePhase)? _nachDerSitzung;

    /// <summary>
    /// Hält den aktuellen Punkt der laufenden Ratssitzung ab und gibt ihn mit Ergebnis zurück (null: keine Sitzung).
    /// Nach dem letzten Punkt läuft die Jahresabrechnung zu Ende (<see cref="JahrBeendet"/>, Spielende), danach läuft die Zeit wieder.
    /// </summary>
    public Politik.Sitzungspunkt? RatssitzungWeiter()
    {
        if (Ratssitzung is not { } sitzung) return null;
        var punkt = sitzung.PunktAbhalten();
        if (sitzung.IstBeendet && _nachDerSitzung is { } rest)
        {
            _nachDerSitzung = null;
            Wirtschaft!.JahresabrechnungAbschliessen(Zeit.Jahr);
            JahrAbschliessen(rest.Jahr);
            if (!IstBeendet && Zeit.Tagesphase != rest.AltePhase)
                PhaseBegonnen?.Invoke(Zeit.Tagesphase);
        }
        return punkt;
    }

    /// <summary>„Überspringen“: alle übrigen Punkte der laufenden Ratssitzung abhalten und die Jahresabrechnung beenden.</summary>
    public void RatssitzungAbschliessen()
    {
        while (Ratssitzung is not null)
            RatssitzungWeiter();
    }

    /// <param name="einstellungen">Partie-Einstellungen; ohne Angabe die Standardwerte.</param>
    /// <param name="zeit">Startzeitpunkt; ohne Angabe Frühling 1400, 04:00.</param>
    /// <param name="wirtschaftsdaten">Daten aus game/daten/wirtschaft.json; ohne Angabe läuft nur die Zeit.</param>
    public Simulation(Partieeinstellungen? einstellungen = null, Spielzeit? zeit = null, Wirtschaftsdaten? wirtschaftsdaten = null)
    {
        Einstellungen = einstellungen ?? Partieeinstellungen.Standard;

        var fehler = Einstellungen.Pruefen();
        if (fehler.Count > 0)
            throw new ArgumentException("Ungültige Partie-Einstellungen: " + string.Join(" ", fehler), nameof(einstellungen));

        Zeit = zeit ?? new Spielzeit();
        Bote = new Mehrspieler.Befehlsbote(this);
        Takt.RundenDauerSekunden = Einstellungen.RundenDauerMinuten * 60;

        if (wirtschaftsdaten is not null)
            Wirtschaft = new Stadtwirtschaft(wirtschaftsdaten, Einstellungen.StartStatut, Einstellungen.Zufallsstartwert)
            {
                AktionspunkteJeRunde = Einstellungen.AktionspunkteProRunde,
                Uhr = Zeit, // dieselbe Uhr – die Wirtschaft sieht immer die aktuelle Zeit (Marktzeiten)
            };
    }

    /// <summary>Echtzeit verarbeiten: so viele Ticks ausführen, wie der Taktgeber erlaubt.</summary>
    /// <returns>Anzahl der ausgeführten Ticks.</returns>
    public int Aktualisieren(double echtzeitSekunden)
    {
        if (IstBeendet || Ratssitzung is not null)
            return 0;

        var ticks = Takt.Voranschreiten(echtzeitSekunden);
        for (var i = 0; i < ticks && !IstBeendet && Ratssitzung is null; i++)
            Tick();
        return ticks;
    }

    /// <summary>Genau eine Spielminute simulieren.</summary>
    public void Tick()
    {
        if (IstBeendet || Ratssitzung is not null)
            return;

        var altesJahr = Zeit.Jahr;
        var altePhase = Zeit.Tagesphase;

        // Zuerst wirken die Befehle für diese Minute (in fester Reihenfolge), dann wird gearbeitet, dann rückt die Uhr weiter.
        // Bis zum Ende des Ticks (samt seinen Ereignissen) wirkt kein neuer Befehl sofort – er wartet auf den nächsten Tick.
        try
        {
            Bote.TickBeginnt(Zeit.VergangeneMinuten);
            Wirtschaft?.Tick(Zeit);
            // Später hier: Bürger-KI, Karren, Ereignisse ...

            Zeit.Weiter();

            if (Zeit.Jahr != altesJahr && !Jahresabrechnung(altesJahr))
            {
                _nachDerSitzung = (altesJahr, altePhase); // die Partie hält vor der Ratssitzung an
                Bote.LueckeMelden($"Ratssitzung {altesJahr} angehalten – ihre Schritte sind noch keine Befehle.");
                return;
            }

            if (!IstBeendet && Zeit.Tagesphase != altePhase)
                PhaseBegonnen?.Invoke(Zeit.Tagesphase);
        }
        finally
        {
            Bote.TickEndet();
        }
    }

    /// <returns>false, wenn die Partie vor der Ratssitzung anhält – den Rest erledigt <see cref="RatssitzungWeiter"/>.</returns>
    private bool Jahresabrechnung(int beendetesJahr)
    {
        if (Wirtschaft?.Jahresabrechnung(Zeit.Jahr, Zeit) == false)
            return false;
        JahrAbschliessen(beendetesJahr);
        return true;
    }

    private void JahrAbschliessen(int beendetesJahr)
    {
        JahrBeendet?.Invoke(beendetesJahr);

        var vermoegen = (long)Math.Floor(Wirtschaft?.HoechstesVermoegen ?? 0);
        var stand = new Spielstand(Zeit.Jahr, LebendeDynastien, vermoegen);
        if (!IstBeendet && Spielende.IstErreicht(Einstellungen, stand))
            Abschliessen(Einstellungen.SiegbedingungText); // die Siegbedingung: das rechnet jede Partie selbst nach, keine Lücke im Protokoll
    }

    /// <summary>
    /// Häuser, die noch im Spiel sind (weder bankrott noch erloschen). Solange es weniger als zwei Konten gibt (Tests, Sandbox),
    /// zählt der Platzhalter – sonst wäre „Letzte Dynastie gewinnt" sofort erfüllt.
    /// </summary>
    private int LebendeDynastien => Wirtschaft is { Konten.Count: >= 2 } w
        ? w.Konten.Count(k => k.IstAktiv)
        : PlatzhalterLebendeDynastien;

    // ── Entwicklerfenster ───────────────────────────────────────────────────

    /// <summary>
    /// Dürfen Entwicklerbefehle wirken und darf vorgespult werden? Standard: nein. Im Einzelspiel setzt die Oberfläche es nach dem Haken
    /// „Entwickler-Optionen“, im Mehrspieler bleibt es aus. Steht im Spielstand (<see cref="Entwickler.Entwicklerhilfe.Erlaubt"/>);
    /// ohne Wirtschaft immer aus.
    /// </summary>
    public bool EntwicklerErlaubt
    {
        get => Wirtschaft?.Entwicklerhilfe.Erlaubt ?? false;
        set { if (Wirtschaft is { } w) w.Entwicklerhilfe.Erlaubt = value; }
    }

    /// <summary>Hat in dieser Partie je ein Entwicklerbefehl gewirkt? Für Laden-Liste und Jahresbericht; bleibt für immer.</summary>
    public bool MitEntwicklerhilfe => Wirtschaft?.Entwicklerhilfe.Benutzt ?? false;

    /// <summary>Höchstens so viele Spielminuten auf einmal vorspulen (20 Spieljahre).</summary>
    public const long VorspulenHoechstens = 20L * Spielzeit.MinutenProTag;

    /// <summary>
    /// Vorspulen: <paramref name="minuten"/> Ticks sofort rechnen, wie sonst über viele Bilder – mit allen Ereignissen. Ein Befehl ist das
    /// nicht: Spulen ändert keine Regel, es rechnet nur schneller; ein <see cref="Mehrspieler.Befehlsprotokoll"/> kennt den Endtick und spielt
    /// es von selbst gleich ab. Im Mehrspieler wäre es Sache des Zugtakts des Gastgebers. Hält an, wenn eine Ratssitzung anhält oder die Partie
    /// endet. Setzt <see cref="MitEntwicklerhilfe"/> nicht, braucht aber <see cref="EntwicklerErlaubt"/>.
    /// </summary>
    public Befehlsergebnis Vorspulen(long minuten)
    {
        if (!EntwicklerErlaubt) return Befehlsergebnis.Abgelehnt("Vorspulen geht nur mit den Entwickler-Optionen.");
        if (minuten < 1 || minuten > VorspulenHoechstens)
            return Befehlsergebnis.Abgelehnt($"Vorspulen geht um 1 bis {VorspulenHoechstens} Spielminuten.");
        if (IstBeendet) return Befehlsergebnis.Abgelehnt("Die Partie ist vorbei.");
        if (Ratssitzung is not null) return Befehlsergebnis.Abgelehnt("Erst die Ratssitzung zu Ende führen.");
        var gerechnet = 0L;
        while (gerechnet < minuten && !IstBeendet && Ratssitzung is null)
        {
            Tick();
            gerechnet++;
        }
        var wo = $"jetzt {Zeit.DatumText}, {Zeit.UhrzeitText}";
        return Befehlsergebnis.Ok(gerechnet < minuten
            ? $"{gerechnet} von {minuten} Minuten vorgespult – {(IstBeendet ? "die Partie ist vorbei" : "die Ratssitzung beginnt")}, {wo}."
            : $"{gerechnet} Minuten vorgespult, {wo}.");
    }

    /// <summary>Bis zu dieser Uhrzeit vorspulen (04:00 bis 22:59); ist sie heute vorbei, bis zu ihr im nächsten Jahr.</summary>
    public Befehlsergebnis VorspulenBisUhrzeit(int stunde, int minute = 0)
    {
        var ziel = stunde * 60 + minute;
        if (minute is < 0 or > 59 || ziel < Spielzeit.TagesbeginnMinute || ziel >= Spielzeit.TagesendeMinute)
            return Befehlsergebnis.Abgelehnt($"Der Spieltag geht von {Spielzeit.TagesbeginnMinute / 60:00}:00 bis {Spielzeit.TagesendeMinute / 60 - 1}:59.");
        var abstand = ((ziel - Zeit.UhrzeitInMinuten) % Spielzeit.MinutenProTag + Spielzeit.MinutenProTag) % Spielzeit.MinutenProTag;
        return abstand == 0 ? Befehlsergebnis.Abgelehnt($"Es ist schon {Zeit.UhrzeitText}.") : Vorspulen(abstand);
    }

    /// <summary>Bis zur letzten Minute des Jahres vorspulen (22:59) – der nächste Tick bringt die Jahresabrechnung.</summary>
    public Befehlsergebnis VorspulenBisJahresende()
    {
        var abstand = Spielzeit.MinutenProTag - 1 - Zeit.MinuteImTag;
        return abstand == 0 ? Befehlsergebnis.Abgelehnt("Es ist schon die letzte Minute des Jahres.") : Vorspulen(abstand);
    }

    /// <summary>
    /// Über den Jahreswechsel vorspulen: Jahresabrechnung, Gerichtstag und Ratssitzung mit Wahlen (hält die Oberfläche sie an, steht sie danach
    /// in <see cref="Ratssitzung"/>). Ratssitzung und Wahl gibt es nur am Jahresende – „jetzt“ heißt darum: bis dorthin spulen.
    /// </summary>
    public Befehlsergebnis VorspulenBisRatssitzung() => Vorspulen(Spielzeit.MinutenProTag - Zeit.MinuteImTag);

    /// <summary>Für das Laden eines Spielstands: Ende der Partie übernehmen, ohne das Ereignis auszulösen.</summary>
    internal void EndeWiederherstellen(bool istBeendet, string? endgrund)
    {
        IstBeendet = istBeendet;
        Endgrund = endgrund;
    }

    /// <summary>
    /// Beendet die Partie sofort, z. B. wenn der Spieler bankrott ist. Löst <see cref="PartieBeendet"/> einmal aus. Wartende Befehle wirken
    /// nicht mehr (ihre Rückrufe bekommen eine Ablehnung). Weil das von außen kommt und kein Befehl ist, gilt ein laufendes Befehlsprotokoll
    /// danach als unvollständig.
    /// </summary>
    public void Beenden(string grund)
    {
        if (IstBeendet) return;
        Bote.LueckeMelden($"Partie von außen beendet: {grund}");
        Abschliessen(grund);
    }

    private void Abschliessen(string grund)
    {
        if (IstBeendet) return;
        IstBeendet = true;
        Endgrund = grund;
        try { PartieBeendet?.Invoke(); }
        finally { Bote.Verwerfen("Die Partie ist vorbei – der Befehl wirkte nicht mehr."); }
    }
}
