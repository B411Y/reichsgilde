using Godot;

namespace Reichsgilde;

/// <summary>
/// Die gekauften GameDev.tv-Texturen (GameDev.tv General Asset License 2.0) liegen nicht im öffentlichen Repository, sondern in <c>game/assets/privat/</c>
/// (von Git ignoriert; <c>tools/assets/privat_holen.py</c> holt sie aus dem privaten Speicher, <c>docs/assets/privat_manifest.json</c> nennt sie).
/// Diese Klasse wählt zwischen der privaten Fassung und der bisherigen eigenen Textur: Fehlt die private Datei (öffentliches Repository, CI), gilt
/// stillschweigend die bisherige, im Protokoll nur ein Hinweis. Nichts im Projekt verweist hart auf <c>res://assets/privat/</c>; jede Stelle fragt hier.
/// </summary>
public static class PrivateTexturen
{
    public const string Wurzel = "res://assets/privat/";

    private static readonly HashSet<string> _gemeldet = new();

    /// <summary>Gibt es die private Datei (unter <c>assets/privat/</c>, relativ, z. B. <c>boden/boden_farbe_hoehe.webp</c>) als geladene Ressource?</summary>
    public static bool Da(string relativ) => ResourceLoader.Exists(Wurzel + relativ);

    /// <summary>Der Pfad der privaten Datei, wenn sie da ist (und alle <paramref name="zusaetzlich"/> auch), sonst <paramref name="bisher"/>.</summary>
    public static string Waehle(string relativ, string bisher, params string[] zusaetzlich)
    {
        var alle = zusaetzlich.Prepend(relativ);
        if (alle.All(Da)) return Wurzel + relativ;
        if (_gemeldet.Add(relativ)) GD.Print($"Hinweis: private Textur {relativ} fehlt, es gilt die eigene ({bisher})");
        return bisher;
    }

    /// <summary>Der Boden-Atlas (Farbe und Höhe, Normale und Rauheit): beide privat, wenn beide da sind, sonst beide eigen (sie gehören zusammen).</summary>
    public static (string Farbe, string Normale) BodenAtlas(string farbeEigen, string normaleEigen)
    {
        var farbe = Waehle("boden/boden_farbe_hoehe.webp", farbeEigen, "boden/boden_normal_rauheit.webp");
        return farbe == farbeEigen ? (farbeEigen, normaleEigen) : (farbe, Wurzel + "boden/boden_normal_rauheit.webp");
    }
}
