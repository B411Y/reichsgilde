using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core;

/// <summary>
/// Das Herz des Spiels: die gesamte Spielwelt und ihr Ablauf.
///
/// Heute kann sie nur die Zeit weiterdrehen und das Spielende erkennen.
/// Nach und nach kommen hier Markt, Betriebe, Bürger, Dynastien, Rat und Gericht dazu.
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

    /// <summary>Ist die Partie laut Siegbedingung vorbei? Dann läuft keine Zeit mehr.</summary>
    public bool IstBeendet { get; private set; }

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

    public Simulation(Partieeinstellungen? einstellungen = null, Spielzeit? zeit = null)
    {
        Einstellungen = einstellungen ?? Partieeinstellungen.Standard;

        var fehler = Einstellungen.Pruefen();
        if (fehler.Count > 0)
            throw new ArgumentException("Ungültige Partie-Einstellungen: " + string.Join(" ", fehler), nameof(einstellungen));

        Zeit = zeit ?? new Spielzeit();
        Takt.RundenDauerSekunden = Einstellungen.RundenDauerMinuten * 60;
    }

    /// <summary>Echtzeit verarbeiten: so viele Ticks ausführen, wie der Taktgeber erlaubt.</summary>
    /// <returns>Anzahl der ausgeführten Ticks.</returns>
    public int Aktualisieren(double echtzeitSekunden)
    {
        if (IstBeendet)
            return 0;

        var ticks = Takt.Voranschreiten(echtzeitSekunden);
        for (var i = 0; i < ticks && !IstBeendet; i++)
            Tick();
        return ticks;
    }

    /// <summary>Genau eine Spielminute simulieren.</summary>
    public void Tick()
    {
        if (IstBeendet)
            return;

        var altesJahr = Zeit.Jahr;
        var altePhase = Zeit.Tagesphase;

        Zeit.Weiter();

        // Hier kommt später die eigentliche Simulation hin (Produktion, Bürger-KI ...).

        if (Zeit.Jahr != altesJahr)
            Jahresabrechnung(altesJahr);

        if (!IstBeendet && Zeit.Tagesphase != altePhase)
            PhaseBegonnen?.Invoke(Zeit.Tagesphase);
    }

    private void Jahresabrechnung(int beendetesJahr)
    {
        JahrBeendet?.Invoke(beendetesJahr);

        var stand = new Spielstand(Zeit.Jahr, PlatzhalterLebendeDynastien, HoechstesVermoegen: 0);
        if (Spielende.IstErreicht(Einstellungen, stand))
        {
            IstBeendet = true;
            PartieBeendet?.Invoke();
        }
    }
}
