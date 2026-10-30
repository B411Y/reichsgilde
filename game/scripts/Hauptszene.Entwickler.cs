using Godot;

namespace Reichsgilde;

/// <summary>
/// Das Entwicklerfenster in der Hauptszene (<see cref="EntwicklerAnzeige"/>) – ein Teil der <c>partial class Hauptszene</c>: Es gibt nur mit dem Haken
/// „Entwickler-Optionen“ (<see cref="Entwickler.Aktiv"/>) ein Fenster, die Taste F12 und den Knopf in der Betriebsleiste. Dieselbe Stelle schaltet die
/// Sperre des Kerns (<c>Simulation.EntwicklerErlaubt</c>): nach dem Haken, nach jedem Laden und nach jedem Neubeginn, denn die Hauptszene entsteht
/// für jede Partie neu und baut ihre Simulation selbst auf.
/// </summary>
public partial class Hauptszene
{
    private EntwicklerAnzeige? _entwickler;

    /// <summary>Sperre des Kerns nach dem Haken setzen (gleich nach dem Aufbau der Simulation, ob neu oder geladen).</summary>
    private void EntwicklerSperreSetzen() => _simulation.EntwicklerErlaubt = Entwickler.Aktiv && _simulation.Wirtschaft is not null;

    /// <summary>Baut das Fenster, wenn der Haken gesetzt ist. Startoptionen: <c>--entwicklerfenster=1|&lt;Reiter 1–6&gt;</c> öffnet es gleich.</summary>
    private void EntwicklerBauen(Dictionary<string, string> optionen)
    {
        if (!Entwickler.Aktiv || _spieler is null) return;
        _entwickler = new EntwicklerAnzeige();
        _entwickler.Verbinden(_simulation, _spieler, _stadt, TempoSetzen); // vor dem Einhängen: AddChild ruft schon _Ready (Aufbau der Reiter)
        GetNode<CanvasLayer>("Oberflaeche").AddChild(_entwickler);
        if (!optionen.TryGetValue("entwicklerfenster", out var wahl) || wahl == "0") return;
        _entwickler.Oeffnen(int.TryParse(wahl, out var reiter) && reiter > 1 ? reiter - 1 : -1);
    }
}
