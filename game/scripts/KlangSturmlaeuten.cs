using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Feuerglocke beim Blitzschlag: Die nächste Kirche läutet Sturm – schnelle, unregelmäßige Schläge statt des ruhigen Läutens, gut anderthalb
/// Minuten, dann ist Ruhe (<c>glocke_sturm.ogg</c>, 95 s, gesetzt aus einem Schlag der großen Glocke der Feuerglocke, <c>tools/ton/glocke_sturm_bauen.py</c>).
/// Der Klang steht an der Kirche in der Welt (ein <see cref="AudioStreamPlayer3D"/> an der Stadt, Bus Umgebung): nah laut, am anderen Ende der Stadt leise.
/// Läutet sie schon, bleibt es bei dem einen Läuten (zwei Einschläge kurz hintereinander läuten nicht doppelt).
/// </summary>
public static class KlangSturmlaeuten
{
    public const string Pfad = "res://assets/audio/umgebung/glocken/glocke_sturm.ogg";

    /// <summary>So weit (Meter) ist die Sturmglocke zu hören.</summary>
    private const float Reichweite = 1600f;

    private static AudioStream? _ton;
    private static AudioStreamPlayer3D? _spieler;

    /// <summary>
    /// Läutet an der Kirche <paramref name="ort"/> (Koordinaten von <paramref name="eltern"/>, der Stadt) Sturm, für das Feuer in <paramref name="wo"/> (nur
    /// fürs Protokoll). Der Spieler hängt an der Stadt: Verlässt man die Partie, verstummt die Glocke mit ihr.
    /// </summary>
    public static void Laeuten(Node3D eltern, Vector3 ort, string wo)
    {
        if (_spieler is not null && GodotObject.IsInstanceValid(_spieler) && _spieler.Playing) return;
        _ton ??= ResourceLoader.Exists(Pfad) ? GD.Load<AudioStream>(Pfad) : null;
        if (_ton is null)
        {
            GD.PushError($"Klang fehlt: {Pfad}");
            return;
        }
        _spieler = new AudioStreamPlayer3D
        {
            Name = "Sturmlaeuten", Stream = _ton, Bus = KlangDienst.BusUmgebung, Position = ort, MaxDistance = Reichweite,
            UnitSize = Reichweite / 8f, VolumeDb = -1f,
        };
        eltern.AddChild(_spieler);
        _spieler.Finished += _spieler.QueueFree;
        _spieler.Play();
        GD.Print($"[blitz] Sturmläuten an {ort} (Feuer in {wo}), {_ton.GetLength():0} s");
    }
}
