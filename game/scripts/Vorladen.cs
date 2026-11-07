using Godot;

namespace Reichsgilde;

/// <summary>
/// Lädt die großen Texturen und zwei große Modelle schon beim Programmstart im Hintergrund (<see cref="ResourceLoader.LoadThreadedRequest"/>): Die vier Material-Arrays
/// (Modelle und Boden, Farbe und Normalen, zusammen gut 100 MB verlustfreies WebP) brauchten beim ersten Gebrauch auf dem Hauptfaden je ein bis zwei
/// Sekunden zum Entpacken. Jetzt entpacken sie sich in vier Fäden neben dem Start der Szene, der Aufrufer holt sie mit <see cref="Laden{T}"/> ab
/// (wartet nur, falls sie noch nicht fertig sind). Wer sie nie braucht (Test ohne Bild), schließt beim Beenden ab (<see cref="AllesAbholen"/>), damit
/// keine Anforderung als Leck stehen bleibt. Ohne Fenster (headless, CI) und mit <c>--vorladen=0</c> tut die Klasse nichts.
/// </summary>
public static class Vorladen
{
    /// <summary>Die großen Dateien, die jede Partie braucht.</summary>
    private static readonly string[] Dateien =
    {
        "res://assets/texturen/modelle/modelle_farbe_hoehe.webp",
        "res://assets/texturen/modelle/modelle_normal_rauheit.webp",
        "res://assets/texturen/boden/boden_farbe_hoehe.webp",
        "res://assets/texturen/boden/boden_normal_rauheit.webp",
        // zwei große Modelle, die beim Laden jeder Stadt und mit den ersten Passanten gebraucht werden (je 0,1 bis 0,2 s auf dem Hauptfaden)
        "res://assets/modelle/natur/natur_kit_baeume.glb",
        "res://assets/modelle/figuren/ki/schmied_ki.glb",
    };

    private static readonly HashSet<string> _offen = new();

    /// <summary>Stößt die Ladevorgänge an (einmal, ganz am Anfang des Programms).</summary>
    public static void Starten(SceneTree baum)
    {
        if (_offen.Count > 0 || DisplayServer.GetName() == "headless" || OS.GetCmdlineUserArgs().Contains("--vorladen=0")) return;
        foreach (var pfad in Dateien)
        {
            if (!ResourceLoader.Exists(pfad) || ResourceLoader.LoadThreadedRequest(pfad) != Error.Ok) continue;
            _offen.Add(pfad);
        }
        baum.Root.TreeExiting += AllesAbholen;
    }

    /// <summary>Die Ressource: die vorgeladene (wartet, bis sie fertig ist) oder, wenn sie nicht angefordert wurde oder schon abgeholt ist, wie <c>GD.Load</c>.</summary>
    public static T? Laden<T>(string pfad) where T : Resource
    {
        if (_offen.Remove(pfad)) return ResourceLoader.LoadThreadedGet(pfad) as T;
        return GD.Load<T>(pfad);
    }

    /// <summary>Holt alles Angeforderte ab (beim Beenden des Programms).</summary>
    public static void AllesAbholen()
    {
        foreach (var pfad in _offen.ToList()) ResourceLoader.LoadThreadedGet(pfad);
        _offen.Clear();
    }
}
