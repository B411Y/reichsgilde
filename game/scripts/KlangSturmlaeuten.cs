using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Feuerglocke beim Blitzschlag: Die nächste Kirche läutet Sturm – schnelle, unregelmäßige Schläge statt des ruhigen Läutens, gut anderthalb
/// Minuten, dann ist Ruhe (<c>glocke_sturm.ogg</c>, 95 s, gesetzt aus einem Schlag der großen Glocke der Feuerglocke, <c>tools/ton/glocke_sturm_bauen.py</c>).
/// Der Klang steht an der Kirche in der Welt (<see cref="KlangDienst.WeltSpielen"/>, Bus Umgebung): nah laut, am anderen Ende der Stadt leise.
/// Läutet sie schon, bleibt es bei dem einen Läuten (zwei Einschläge kurz hintereinander läuten nicht doppelt).
/// </summary>
public static class KlangSturmlaeuten
{
    public const string Pfad = "res://assets/audio/umgebung/glocken/glocke_sturm.ogg";

    /// <summary>So weit (Meter) ist die Sturmglocke zu hören.</summary>
    private const float Reichweite = 1600f;

    private static AudioStream? _ton;
    private static double _laeutetBis = -1;

    /// <summary>Läutet an der Kirche <paramref name="ort"/> Sturm (für das Feuer in <paramref name="wo"/>, nur fürs Protokoll).</summary>
    public static void Laeuten(Vector3 ort, string wo)
    {
        var jetzt = Time.GetTicksMsec() / 1000.0;
        if (jetzt < _laeutetBis) return;
        _ton ??= ResourceLoader.Exists(Pfad) ? GD.Load<AudioStream>(Pfad) : null;
        if (_ton is null)
        {
            GD.PushError($"Klang fehlt: {Pfad}");
            return;
        }
        _laeutetBis = jetzt + _ton.GetLength();
        KlangDienst.WeltSpielen(_ton, ort, KlangDienst.BusUmgebung, Reichweite, 1, db: -1f);
        GD.Print($"[blitz] Sturmläuten an {ort} (Feuer in {wo}), {_ton.GetLength():0} s");
    }
}
