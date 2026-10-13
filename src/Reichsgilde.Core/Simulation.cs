using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core;

/// <summary>
/// Das Herz des Spiels: die gesamte Spielwelt und ihr Ablauf.
///
/// Heute kann sie nur die Zeit weiterdrehen. Nach und nach kommen hier
/// Markt, Betriebe, Bürger, Dynastien, Rat und Gericht dazu.
///
/// Godot (die Darstellung) ruft nur <see cref="Aktualisieren"/> auf und liest danach
/// den Zustand aus. Im Multiplayer läuft genau diese Klasse später auf dem Server.
/// </summary>
public sealed class Simulation
{
    public Spielzeit Zeit { get; }
    public Taktgeber Takt { get; } = new();

    /// <summary>Wird ausgelöst, wenn eine neue Tagesphase beginnt (z. B. Vesper).</summary>
    public event Action<Tagesphase>? PhaseBegonnen;

    /// <summary>
    /// Wird am Ende jeder Runde ausgelöst – das ist die Jahresabrechnung.
    /// Übergeben wird das Jahr, das gerade zu Ende gegangen ist.
    /// Später: Steuern, Zinsen, Geburten, Alterung, Erbschaften ...
    /// </summary>
    public event Action<int>? JahrBeendet;

    public Simulation(Spielzeit? zeit = null)
    {
        Zeit = zeit ?? new Spielzeit();
    }

    /// <summary>Echtzeit verarbeiten: so viele Ticks ausführen, wie der Taktgeber erlaubt.</summary>
    /// <returns>Anzahl der ausgeführten Ticks.</returns>
    public int Aktualisieren(double echtzeitSekunden)
    {
        var ticks = Takt.Voranschreiten(echtzeitSekunden);
        for (var i = 0; i < ticks; i++)
            Tick();
        return ticks;
    }

    /// <summary>Genau eine Spielminute simulieren.</summary>
    public void Tick()
    {
        var altesJahr = Zeit.Jahr;
        var altePhase = Zeit.Tagesphase;

        Zeit.Weiter();

        // Hier kommt später die eigentliche Simulation hin (Produktion, Bürger-KI ...).

        if (Zeit.Jahr != altesJahr)
            JahrBeendet?.Invoke(altesJahr);

        if (Zeit.Tagesphase != altePhase)
            PhaseBegonnen?.Invoke(Zeit.Tagesphase);
    }
}
