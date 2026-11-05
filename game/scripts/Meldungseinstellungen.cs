using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Wahl des Spielers für die Benachrichtigungen (Stufe 1): je Meldungsart <see cref="Meldestufe"/> (aus, Verlauf, Siegel, Brief) und ob
/// die Siegel sich bewegen. Gespeichert im Abschnitt <c>[meldungen]</c> von <c>user://einstellungen.cfg</c>; ohne Wahl gilt die Vorgabe aus
/// <c>game/daten/meldungen.json</c> (<see cref="Ordnung"/>). Die Wahl gilt für das eigene Haus und die Stadt – fremde Häuser bleiben im Verlauf.
/// </summary>
public static class Meldungseinstellungen
{
    private const string Datei = "user://einstellungen.cfg", Abschnitt = "meldungen", DatenPfad = "res://daten/meldungen.json";

    private static Meldungsordnung? _ordnung;
    private static Dictionary<string, Meldestufe>? _wahl;
    private static bool _bewegung = true;

    /// <summary>Die Zuordnung der Textmeldungen (einmal aus <c>meldungen.json</c> geladen).</summary>
    public static Meldungsordnung Ordnung => _ordnung ??= Meldungsordnung.AusJson(Godot.FileAccess.GetFileAsString(DatenPfad));

    /// <summary>Bewegen sich die Siegel (Einblenden, leises Pulsieren) und blenden Briefe ein? Aus: alles steht still (wie „Fahnen bewegen“).</summary>
    public static bool Bewegung
    {
        get { Laden(); return _bewegung; }
        set
        {
            Laden();
            if (_bewegung == value) return;
            _bewegung = value;
            Speichern();
        }
    }

    /// <summary>Die Wahl des Spielers für die Art; null, wenn er nichts gewählt hat (dann gilt die Vorgabe).</summary>
    public static Meldestufe? Wahl(string artId)
    {
        Laden();
        return _wahl!.TryGetValue(artId, out var s) ? s : null;
    }

    /// <summary>Die Stufe, die für eigene Meldungen dieser Art gilt (Wahl oder Vorgabe).</summary>
    public static Meldestufe Wirksam(Meldeart art) => Wahl(art.Id) ?? art.Eigen;

    /// <summary>Wählt die Stufe für die Art; null nimmt die Wahl zurück.</summary>
    public static void Setzen(string artId, Meldestufe? stufe)
    {
        Laden();
        if (stufe is { } s) _wahl![artId] = s; else _wahl!.Remove(artId);
        Speichern();
    }

    /// <summary>Alle Wahlen zurück auf die Vorgabe (die Bewegung bleibt).</summary>
    public static void Zuruecksetzen()
    {
        Laden();
        _wahl!.Clear();
        Speichern();
    }

    private static void Laden()
    {
        if (_wahl is not null) return;
        _wahl = new Dictionary<string, Meldestufe>();
        var datei = new ConfigFile();
        if (datei.Load(Datei) != Error.Ok) return;
        _bewegung = datei.GetValue(Abschnitt, "bewegung", true).AsBool();
        foreach (var art in Ordnung.Arten)
            if (datei.HasSectionKey(Abschnitt, art.Id) && datei.GetValue(Abschnitt, art.Id).AsInt32() is var n && Enum.IsDefined(typeof(Meldestufe), n))
                _wahl[art.Id] = (Meldestufe)n;
    }

    private static void Speichern()
    {
        var datei = new ConfigFile();
        datei.Load(Datei); // andere Einstellungen behalten
        datei.EraseSection(Abschnitt);
        datei.SetValue(Abschnitt, "bewegung", _bewegung);
        foreach (var (id, stufe) in _wahl!) datei.SetValue(Abschnitt, id, (int)stufe);
        datei.Save(Datei);
    }
}
