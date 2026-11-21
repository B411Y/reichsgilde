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
    private static string[] Dateien()
    {
        // der Boden-Atlas: mit den privaten Texturen deren Fassung (PrivateTexturen), sonst die eigene
        var boden = PrivateTexturen.BodenAtlas("res://assets/texturen/boden/boden_farbe_hoehe.webp", "res://assets/texturen/boden/boden_normal_rauheit.webp");
        return new[]
        {
            "res://assets/texturen/modelle/modelle_farbe_hoehe.webp",
            "res://assets/texturen/modelle/modelle_normal_rauheit.webp",
            "res://assets/texturen/modelle/relief_farbe_hoehe.webp",   // Relief-Texturen der Modelle (fehlen sie, wird nichts angefordert)
            "res://assets/texturen/modelle/relief_normal_rauheit_ao.webp",
            boden.Farbe,
            boden.Normale,
            // zwei große Modelle, die beim Laden jeder Stadt und mit den ersten Passanten gebraucht werden (je 0,1 bis 0,2 s auf dem Hauptfaden)
            "res://assets/modelle/natur/natur_kit_baeume.glb",
        }
        .Concat(RathausArrays)
        .Concat(Modelle)
        .Concat(NeubauSchalter.Wohnhaeuser ? NeubauArrays : Array.Empty<string>())
        .ToArray();
    }

    private const string Neubau = "res://assets/texturen/neubau/";

    /// <summary>
    /// Die größten Modelle, die jede Stadt (Köln und Greybox) beim Aufbau braucht, samt den acht Probefiguren der Passanten und den sieben Räumen des Rathauses:
    /// Sie ladeten auf dem Hauptfaden je 10 bis 700 ms (Ladezeit 21.11.2026). Wer ein Modell nicht braucht, trägt nur die Anforderung (ein Faden, kurz);
    /// <c>Stadtansicht.Modell</c> holt sie über <see cref="Laden{T}"/> ab. Dateien, die es nicht gibt, überspringt <see cref="Starten"/>.
    /// </summary>
    private static readonly string[] Modelle = new[]
    {
        "figuren/probe/passant_probe", "figuren/probe/passant_probe_frau", "figuren/probe/passant_probe_elb", "figuren/probe/passant_probe_elbin",
        "figuren/probe/passant_probe_halbling", "figuren/probe/passant_probe_halblingin", "figuren/probe/passant_probe_zwerg", "figuren/probe/passant_probe_zwergin",
        "gebaeude/neubau/rathaus", "gebaeude/koelner_rathaus", "gebaeude/neubau/rathaus_eingangshalle_innen", "gebaeude/neubau/rathaus_amtsstube_innen",
        "gebaeude/neubau/rathaus_gerichtssaal_innen", "gebaeude/neubau/rathaus_kerker_innen", "gebaeude/neubau/rathaus_ratshalle_innen",
        "gebaeude/neubau/rathaus_turm_treppe_innen", "gebaeude/neubau/rathaus_turm_spitze_innen",
        "gebaeude/koelner_dom", "gebaeude/koeln_gross_st_martin", "gebaeude/koeln_st_gereon", "gebaeude/koeln_st_aposteln", "gebaeude/stapelhaus",
        "gebaeude/marktplatz", "gebaeude/marktstaende", "gebaeude/stadtmauer", "gebaeude/stadtmauer_koeln", "gebaeude/einwohner_buergerhaus",
        "umland/grube", "umland/bauernhof", "umland/holzschlag", "umland/steinbruch", "umland/kraeutergarten", "bruecke/klappbruecke",
    }.Select(m => $"res://assets/modelle/{m}.glb").ToArray();

    /// <summary>Die Trim-Arrays und AO-Atlanten des Rathauses (außen und Innenräume): Das Rathaus steht in jeder Stadt, sie liefen sonst auf dem Hauptfaden (Ladezeit 21.11.2026).</summary>
    private static readonly string[] RathausArrays =
    {
        Neubau + "rathaus_ebenen_albedo.png", Neubau + "rathaus_ebenen_normal.png", Neubau + "rathaus_ebenen_orm.png", Neubau + "rathaus_ao_atlas.png",
        Neubau + "rathaus_innen_trim_albedo.png", Neubau + "rathaus_innen_trim_normal.png", Neubau + "rathaus_innen_trim_orm.png", Neubau + "rathaus_innen_ao_atlas.png",
    };

    /// <summary>
    /// Die Arrays der Neubau-Wohnhäuser (Außenwände, Innenräume, Häuserzeilen). Sie werden nur angefordert, wenn die Neubau-Wohnhäuser gebaut werden
    /// (<see cref="NeubauSchalter.Wohnhaeuser"/>, heute nur mit <c>--neubauprobe=1</c>): zusammen rund 400 MB, die ein Menü ohne sie nicht laden soll.
    /// Wer sie im Normalspiel einschaltet, legt nur diesen Schalter um.
    /// </summary>
    private static readonly string[] NeubauArrays =
    {
        Neubau + "haeuser_ebenen_albedo.png", Neubau + "haeuser_ebenen_normal.png", Neubau + "haeuser_ebenen_orm.png", Neubau + "haeuser_ao_ebenen.png",
        Neubau + "innen_ebenen_albedo.png", Neubau + "innen_ebenen_normal.png", Neubau + "innen_ebenen_orm.png", Neubau + "innen_ao_ebenen.png",
        Neubau + "zeilen_ao_ebenen.png",
    };

    private static readonly HashSet<string> _offen = new();

    /// <summary>Stößt die Ladevorgänge an (einmal, ganz am Anfang des Programms).</summary>
    public static void Starten(SceneTree baum)
    {
        if (_offen.Count > 0 || DisplayServer.GetName() == "headless" || OS.GetCmdlineUserArgs().Contains("--vorladen=0")) return;
        foreach (var pfad in Dateien())
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

/// <summary>
/// Der Schalter, ob die Neubau-Wohnhäuser im Spiel gebaut werden (<c>Stadtansicht.Neubau.cs</c>). Heute nur mit <c>--neubauprobe=1</c>; wird sie
/// Standard, legt man hier den Schalter um: Dann laden <see cref="Vorladen"/> ihre Arrays beim Programmstart im Hintergrund und die Stadt baut sie.
/// Eigene Klasse, damit <see cref="Vorladen"/> beim Start nicht die ganze Stadtansicht anfassen muss.
/// </summary>
public static class NeubauSchalter
{
    public static bool Wohnhaeuser { get; } = OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--neubauprobe=") && a[("--neubauprobe=".Length)..].Trim() is "1" or "an");
}
