using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Grafikschnittstelle unter Windows: <b>Vulkan</b> (Standard, <c>project.godot</c>) oder <b>DirectX 12</b>. Die Wahl steht im Fenster Einstellungen
/// (Grafik → Erweitert) und gilt erst nach einem Neustart, denn Godot wählt den Grafiktreiber beim Start, bevor ein Fenster oder eine Szene da ist.
///
/// <b>Wie es wirkt:</b> <c>project.godot</c> nennt in <c>application/config/project_settings_override</c> die Datei <see cref="Datei"/> (<c>user://</c>).
/// Godot liest sie gleich nach <c>project.godot</c> und lässt ihre Werte gelten (belegt mit Godot 4.7.2: <c>ProjectSettings::setup</c> lädt die Datei, bevor
/// <c>Main::setup</c> den Treiber aus <c>rendering/rendering_device/driver.windows</c> liest). Die Datei hat das Format von <c>project.godot</c>:
/// <c>[rendering]</c> und <c>rendering_device/driver.windows="d3d12"</c>. Fehlt sie, gilt Vulkan. Die Startoption <c>--rendering-driver</c> geht vor.
///
/// <b>Rückfall auf Vulkan</b> (zwei Stufen):
/// 1. Startet DirectX 12 nicht (Treiber fehlt oder lehnt ab), nimmt Godot selbst Vulkan (<c>rendering/rendering_device/fallback_to_vulkan</c>, Grundwert an,
///    Warnung „switching to Vulkan“). <see cref="Starten"/> merkt das (<see cref="Aktiv"/> ist „vulkan“, gewählt war „d3d12“) und stellt die Datei auf Vulkan zurück.
/// 2. Stürzt das Spiel mit DirectX 12 später ab, bleibt der <see cref="Vermerk"/> liegen, den <see cref="Starten"/> bei jedem DirectX-12-Start anlegt und nach
///    <see cref="Prueffrist"/> Sekunden oder beim sauberen Beenden löscht. Findet der nächste Start ihn, wird die Datei auf Vulkan zurückgestellt.
///
/// Auf anderen Systemen bleibt die Zeile in den Einstellungen unsichtbar (Ausnahme zum Ansehen: Startoption <c>--schnittstellezeigen=1</c>).
/// </summary>
public static class Grafikschnittstelle
{
    public const string Datei = "user://grafikschnittstelle.cfg";
    private const string Vermerk = "user://grafikschnittstelle.versuch";
    private const string Abschnitt = "rendering";
    private const string Schluessel = "rendering_device/driver.windows";
    /// <summary>So lange muss ein Start mit DirectX 12 laufen, bis er als geglückt gilt.</summary>
    private const double Prueffrist = 30;

    /// <summary>Kennung (wie in Godot) und Anzeigename; die erste Art ist der Standard.</summary>
    public static readonly (string Kennung, string Name)[] Arten =
    {
        ("vulkan", "Vulkan (empfohlen)"),
        ("d3d12", "DirectX 12 (wenig getestet)"),
    };

    /// <summary>Meldung, wenn beim Start auf Vulkan zurückgestellt wurde; leer, solange alles in Ordnung ist.</summary>
    public static string Hinweis { get; private set; } = "";

    /// <summary>Nur unter Windows gibt es die Wahl (Vulkan unter Linux ist die einzige Schnittstelle).</summary>
    public static bool Sichtbar => OS.GetName() == "Windows" || OS.GetCmdlineUserArgs().Contains("--schnittstellezeigen=1");

    /// <summary>Der Treiber, mit dem dieser Start tatsächlich läuft („vulkan“, „d3d12“, „opengl3“ …).</summary>
    public static string Aktiv => RenderingServer.GetCurrentRenderingDriverName();

    /// <summary>Die Wahl für den nächsten Start (aus der Datei; ohne Datei Vulkan).</summary>
    public static string Gewaehlt
    {
        get
        {
            var datei = new ConfigFile();
            if (datei.Load(Datei) != Error.Ok) return Arten[0].Kennung;
            var wert = datei.GetValue(Abschnitt, Schluessel, Arten[0].Kennung).AsString();
            return Array.Exists(Arten, a => a.Kennung == wert) ? wert : Arten[0].Kennung;
        }
    }

    /// <summary>Anzeigename zu einer Kennung; unbekannte Treiber (etwa „opengl3“ im Klicktest) erscheinen, wie Godot sie nennt.</summary>
    public static string Name(string kennung)
    {
        foreach (var (k, name) in Arten)
            if (k == kennung) return name.Replace(" (empfohlen)", "").Replace(" (wenig getestet)", "");
        return kennung;
    }

    /// <summary>Merkt die Wahl für den nächsten Start. Vulkan löscht die Datei nicht, sondern schreibt es ausdrücklich hinein (Spuren bleiben lesbar).</summary>
    public static void Setzen(string kennung)
    {
        if (!Array.Exists(Arten, a => a.Kennung == kennung)) return;
        var datei = new ConfigFile();
        datei.SetValue(Abschnitt, Schluessel, kennung);
        datei.Save(Datei);
    }

    /// <summary>Beim Start (Autoload <see cref="Fenster"/>): Rückfälle erkennen und bei DirectX 12 den Versuchsvermerk anlegen.</summary>
    public static void Starten(Node knoten)
    {
        Hinweis = "";
        // Nur unter Windows gibt es etwas zu prüfen; sonst bliebe „gewählt d3d12, aktiv vulkan“ beim Ansehen auf Linux ein falscher Alarm.
        if (OS.GetName() != "Windows") return;
        var gewaehlt = Gewaehlt;
        if (Godot.FileAccess.FileExists(Vermerk))
        {
            Loeschen(Vermerk);
            if (gewaehlt == "d3d12")
            {
                Setzen("vulkan");
                gewaehlt = "vulkan";
                Hinweis = "Der letzte Start mit DirectX 12 kam nicht bis zum Ende (Absturz oder zu früh beendet). Ab dem nächsten Start nimmt das Spiel wieder Vulkan.";
            }
        }
        else if (gewaehlt == "d3d12" && Aktiv != "d3d12")
        {
            Setzen("vulkan");
            gewaehlt = "vulkan";
            Hinweis = "DirectX 12 ließ sich auf diesem Rechner nicht starten; das Spiel läuft mit Vulkan und merkt sich das.";
        }
        if (gewaehlt == "d3d12" && Aktiv == "d3d12")
        {
            using var vermerk = Godot.FileAccess.Open(Vermerk, Godot.FileAccess.ModeFlags.Write);
            vermerk?.StoreString("d3d12");
            knoten.GetTree().CreateTimer(Prueffrist).Timeout += () => Loeschen(Vermerk);
        }
    }

    /// <summary>Beim sauberen Beenden: Der Start gilt als geglückt, der Vermerk fällt weg.</summary>
    public static void Beenden() => Loeschen(Vermerk);

    private static void Loeschen(string pfad)
    {
        if (Godot.FileAccess.FileExists(pfad)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(pfad));
    }
}
