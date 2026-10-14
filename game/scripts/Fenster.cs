using Godot;

namespace Reichsgilde;

/// <summary>
/// Fenster oder Vollbild – umschaltbar mit F11 (überall) und im Spielmenü; die Wahl wird gespeichert.
///
/// Läuft als Autoload (project.godot → [autoload]): Godot legt den Knoten beim Start einmal an und behält
/// ihn über alle Szenenwechsel. So gilt F11 im Hauptmenü wie im Spiel, und die Einstellung wird nur einmal geladen.
///
/// Die Oberfläche hat eine feste Pixelgröße: Ein größeres Fenster zeigt mehr Stadt, Tafeln, die nicht
/// passen, rollen (<see cref="Rollbereich"/>). Eine Mindestgröße gibt es bewusst nicht.
/// </summary>
public partial class Fenster : Node
{
    private const string Datei = "user://einstellungen.cfg";

    public static bool IstVollbild =>
        DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;

    public override void _Ready()
    {
        var einstellungen = new ConfigFile();
        if (einstellungen.Load(Datei) == Error.Ok && einstellungen.GetValue("anzeige", "vollbild", false).AsBool())
            Setzen(true, speichern: false);
    }

    public override void _Input(InputEvent eingabe)
    {
        if (eingabe is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            Umschalten();
            GetViewport().SetInputAsHandled();
        }
    }

    public static void Umschalten() => Setzen(!IstVollbild);

    private static void Setzen(bool vollbild, bool speichern = true)
    {
        if (DisplayServer.GetName() == "headless") return; // Tests und CI haben kein Fenster
        DisplayServer.WindowSetMode(vollbild ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        if (speichern)
        {
            var einstellungen = new ConfigFile();
            einstellungen.Load(Datei); // andere Einstellungen behalten (fehlt die Datei, bleibt sie leer)
            einstellungen.SetValue("anzeige", "vollbild", vollbild);
            einstellungen.Save(Datei);
        }
    }
}
