using System.Text.Json;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Regeln der Zugtiere aus <c>res://daten/zugtiere.json</c> (erzeugt von <c>tools/modelle/zugtiere.py</c>): je Tierart (<c>pferd</c>, <c>ochse</c>) und Clip
/// Dauer, Schleife, Rumpfweg je Bild und Anlauf, dazu Tempo, Anker im Ziehen und Blendzeit zwischen Gehen und Ziehen. Nur lesen, nichts rechnen.
/// </summary>
public static class Zugtierdaten
{
    /// <summary>Ein Clip: Dauer in Sekunden (Godot nennt Schleifen um 1/24 s zu lang), ob er schleift, und der Rumpfweg in Metern je Bild (24 je Sekunde).</summary>
    public sealed record Klip(double Dauer, bool Schleife, float[] Weg);

    /// <summary>Eine Tierart.</summary>
    public sealed record Tierart(Vector3 AnkerZiehen, float Tempo, double BlendGehenZiehen, Dictionary<string, Klip> Klips);

    private static Dictionary<string, Tierart>? _arten;

    /// <summary>Die Tierart („pferd“, „ochse“); null, wenn die Datei fehlt.</summary>
    public static Tierart? Von(string tier)
    {
        _arten ??= Laden();
        return _arten.GetValueOrDefault(tier);
    }

    private static Dictionary<string, Tierart> Laden()
    {
        var aus = new Dictionary<string, Tierart>();
        const string Pfad = "res://daten/zugtiere.json";
        if (!Godot.FileAccess.FileExists(Pfad)) return aus;
        using var dok = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(Pfad));
        var wurzel = dok.RootElement;
        var blend = wurzel.GetProperty("regeln").GetProperty("blendzeit_gehen_ziehen");
        foreach (var art in wurzel.GetProperty("tiere").EnumerateObject())
        {
            var a = art.Value.GetProperty("anker_ziehen_godot");
            var klips = new Dictionary<string, Klip>();
            foreach (var k in art.Value.GetProperty("klips").EnumerateObject())
            {
                var weg = k.Value.TryGetProperty("weg", out var w) ? w.EnumerateArray().Select(x => x.GetSingle()).ToArray() : Array.Empty<float>();
                klips[k.Name] = new Klip(k.Value.GetProperty("dauer").GetDouble(), k.Value.GetProperty("schleife").GetBoolean(), weg);
            }
            aus[art.Name] = new Tierart(new Vector3(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle()), art.Value.GetProperty("tempo").GetSingle(),
                blend.GetProperty(art.Name).GetDouble(), klips);
        }
        return aus;
    }

    /// <summary>
    /// Stellt die Clips eines Tiermodells so ein, wie die Regeln es verlangen: <c>Animation.Length</c> gleich der Dauer der Datei, Schleifen mit
    /// <see cref="Animation.LoopModeEnum.Linear"/>, Anfahren_ und Anhalten_ laufen einmal. Gilt für die Clips, die das Modell mitbringt (Idempotent).
    /// </summary>
    public static void KlipsEinrichten(AnimationPlayer spieler, string tier)
    {
        if (Von(tier) is not { } art) return;
        foreach (var name in spieler.GetAnimationList())
        {
            var anim = spieler.GetAnimation(name);
            if (!art.Klips.TryGetValue(name, out var klip)) { anim.LoopMode = Animation.LoopModeEnum.Linear; continue; }
            anim.Length = (float)klip.Dauer;
            anim.LoopMode = klip.Schleife ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        }
    }

    /// <summary>Die Abspielzeit (Sekunden), zu der ein Clip (Rumpfweg je Bild in Metern, wachsend) die Strecke <paramref name="meter"/> erreicht; am Ende die Dauer.</summary>
    public static double ZeitFuerWeg(Klip klip, double meter)
    {
        var weg = klip.Weg;
        if (weg.Length < 2) return klip.Dauer;
        if (meter <= 0) return 0;
        for (var i = 1; i < weg.Length; i++)
        {
            if (weg[i] < meter) continue;
            var span = weg[i] - weg[i - 1];
            var t = span > 1e-9f ? (meter - weg[i - 1]) / span : 0;
            return (i - 1 + t) / 24.0;
        }
        return klip.Dauer;
    }
}
