using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Feuerglocke beim Blitzschlag: Die nächste Kirche läutet Sturm, solange das Feuer brennt (<c>glocke_sturm.ogg</c>, Geläut des Kölner Doms,
/// etwas schneller und pochend, als Schleife; <c>tools/ton/glocke_sturm_bauen.py</c>). Ist das Feuer aus, klingt es in
/// <see cref="Ausklang"/> Sekunden weich aus (<see cref="Beenden"/>). Der Klang steht an der Kirche in der Welt (ein
/// <see cref="AudioStreamPlayer3D"/> an der Stadt, Bus Umgebung): nah gut zu hören, mit der Entfernung leiser. Der Spieler hängt an der Stadt –
/// verlässt man die Partie oder lädt, verstummt die Glocke mit ihr. Läutet sie schon, bleibt es bei dem einen Läuten.
/// </summary>
public static class KlangSturmlaeuten
{
    public const string Pfad = "res://assets/audio/umgebung/glocken/glocke_sturm.ogg";

    /// <summary>Bis zu diesem Abstand (Meter) voll, darüber leiser (umgekehrt zur Entfernung); ab <see cref="Reichweite"/> nichts mehr.</summary>
    private const float VollBis = 45f, Reichweite = 1000f;

    /// <summary>So lange (echte Sekunden) klingt das Läuten nach dem Feuer aus.</summary>
    public const float Ausklang = 4f;

    private const float Pegel = -4f;

    private static AudioStream? _ton;
    private static AudioStreamPlayer3D? _spieler;

    /// <summary>Läutet gerade eine Kirche?</summary>
    public static bool Laeutet => _spieler is not null && GodotObject.IsInstanceValid(_spieler) && _spieler.Playing;

    /// <summary>
    /// Läutet an der Kirche <paramref name="ort"/> (Koordinaten von <paramref name="eltern"/>, der Stadt) Sturm, für das Feuer in <paramref name="wo"/> (nur
    /// fürs Protokoll), bis <see cref="Beenden"/> kommt.
    /// </summary>
    public static void Laeuten(Node3D eltern, Vector3 ort, string wo)
    {
        if (Laeutet) return;
        _ton ??= ResourceLoader.Exists(Pfad) ? GD.Load<AudioStream>(Pfad) : null;
        if (_ton is null)
        {
            GD.PushError($"Klang fehlt: {Pfad}");
            return;
        }
        if (_ton is AudioStreamOggVorbis ogg) ogg.Loop = true; // läutet, solange es brennt
        _spieler = new AudioStreamPlayer3D
        {
            Name = "Sturmlaeuten", Stream = _ton, Bus = KlangDienst.BusUmgebung, Position = ort, MaxDistance = Reichweite,
            UnitSize = VollBis, AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance, VolumeDb = Pegel,
        };
        eltern.AddChild(_spieler);
        _spieler.Play();
        GD.Print($"[blitz] Sturmläuten an {ort} (Feuer in {wo})");
    }

    /// <summary>Das Feuer ist aus: Die Glocke klingt in <see cref="Ausklang"/> Sekunden aus und verstummt.</summary>
    public static void Beenden()
    {
        if (!Laeutet) return;
        var spieler = _spieler!;
        _spieler = null;
        var blende = spieler.CreateTween();
        blende.TweenProperty(spieler, "volume_db", -60f, Ausklang).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        blende.TweenCallback(Callable.From(spieler.QueueFree));
        GD.Print("[blitz] Sturmläuten klingt aus");
    }
}
