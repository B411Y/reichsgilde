using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Liest <c>wirtschaft.json</c> schon im Hintergrund, solange das Hauptmenü steht (<see cref="Vorladen"/>): Das Auslesen braucht rund 0,3 s und
/// ist reines C# (System.Text.Json, kein Godot-Knoten), darf also in einem eigenen Thread laufen. Die Datei selbst liest Godot vorher im
/// Hauptthread. Hauptszene und Kartenwerkstatt holen die Daten mit <see cref="Holen"/> – jede Vorab-Lesung genau einmal, denn eine Partie
/// verändert ihre Daten; ohne Vorab-Lesung wird wie bisher sofort gelesen.
/// </summary>
public static class Datenvorrat
{
    private static Task<(Wirtschaftsdaten? Daten, WirtschaftsdatenFehler? Fehler, bool Leer)>? _vorab;

    /// <summary>Beginnt das Lesen im Hintergrund (Hauptmenü); läuft schon eines, bleibt es dabei.</summary>
    public static void Vorladen()
    {
        if (_vorab is not null) return;
        var json = Godot.FileAccess.GetFileAsString(Hauptszene.WirtschaftsdatenPfad);
        _vorab = Task.Run(() => Lesen(json));
    }

    /// <summary>Die Daten (aus dem Hintergrund, sonst jetzt gelesen); bei einem Fehler dessen Meldung, Leer: Die Datei fehlt.</summary>
    public static (Wirtschaftsdaten? Daten, WirtschaftsdatenFehler? Fehler, bool Leer) Holen()
    {
        var vorab = _vorab;
        _vorab = null;
        return vorab?.Result ?? Lesen(Godot.FileAccess.GetFileAsString(Hauptszene.WirtschaftsdatenPfad));
    }

    private static (Wirtschaftsdaten? Daten, WirtschaftsdatenFehler? Fehler, bool Leer) Lesen(string json)
    {
        if (string.IsNullOrEmpty(json)) return (null, null, true);
        try
        {
            return (Wirtschaftsdaten.AusJson(json), null, false);
        }
        catch (WirtschaftsdatenFehler fehler)
        {
            return (null, fehler, false);
        }
    }
}
