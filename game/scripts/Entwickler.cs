using Godot;

namespace Reichsgilde;

/// <summary>
/// Der Entwickler-Modus: eine zentrale Stelle für alles, was nur für die Arbeit am Spiel gedacht ist und Spielern nicht angeboten wird.
/// Heute zeigt er in der Kartenwahl auch die Test- und Messkarten (Greybox, Messstadt); weitere Entwickler-Einstellungen (Debugging …)
/// gehören hierher. Ein Haken unten rechts im Hauptmenü schaltet ihn um; gemerkt in <c>user://einstellungen.cfg</c> (Abschnitt „entwickler“,
/// Schlüssel „aktiv“). Die Startoption <c>--entwickler=1|0</c> gilt nur für diesen Start.
/// </summary>
public static class Entwickler
{
    private const string Datei = "user://einstellungen.cfg";

    private static bool? _aktiv;

    /// <summary>Ist der Entwickler-Modus an? Ohne Angabe (und ohne Startoption) aus.</summary>
    public static bool Aktiv
    {
        get
        {
            if (_aktiv is { } wert) return wert;
            var option = OS.GetCmdlineUserArgs().LastOrDefault(a => a.StartsWith("--entwickler="));
            if (option is not null) return (_aktiv = option["--entwickler=".Length..] != "0").Value;
            var einstellungen = new ConfigFile();
            return (_aktiv = einstellungen.Load(Datei) == Error.Ok && einstellungen.GetValue("entwickler", "aktiv", false).AsBool()).Value;
        }
    }

    /// <summary>Schaltet den Modus um und merkt ihn.</summary>
    public static void Setzen(bool aktiv)
    {
        _aktiv = aktiv;
        var einstellungen = new ConfigFile();
        einstellungen.Load(Datei);
        einstellungen.SetValue("entwickler", "aktiv", aktiv);
        einstellungen.Save(Datei);
    }
}
