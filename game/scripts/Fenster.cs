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
    private static Fenster? _instanz;
    private Label? _hinweis;
    private int _hinweisNummer;

    public static bool IstVollbild =>
        DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;

    public override void _Ready()
    {
        _instanz = this;
        Schriften.Einrichten(GetTree());
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
        // Im Godot-Editor läuft das Spiel standardmäßig eingebettet im Reiter „Game". Dort bestimmt der Editor
        // die Fenstergröße, Vollbild geht nicht (Godot meldet sonst „Embedded window only supports Windowed mode").
        if (Engine.IsEmbeddedInEditor())
        {
            if (speichern)
                _instanz?.HinweisZeigen("Vollbild geht nicht im eingebetteten Spielfenster des Editors.\n" +
                                        "Zum Testen im eigenen Fenster starten: im Reiter „Game“ das Einbetten ausschalten.");
            return;
        }
        DisplayServer.WindowSetMode(vollbild ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        if (speichern)
        {
            var einstellungen = new ConfigFile();
            einstellungen.Load(Datei); // andere Einstellungen behalten (fehlt die Datei, bleibt sie leer)
            einstellungen.SetValue("anzeige", "vollbild", vollbild);
            einstellungen.Save(Datei);
        }
    }

    /// <summary>Kurzer Hinweis oben in der Mitte, verschwindet nach fünf Sekunden – in Menü und Spiel.</summary>
    private void HinweisZeigen(string text)
    {
        if (_hinweis is null)
        {
            var ebene = new CanvasLayer { Layer = 100 };
            AddChild(ebene);
            _hinweis = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _hinweis.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.11f, 0.10f, 0.95f),
                ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 10, ContentMarginBottom = 10,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            });
            _hinweis.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.Minsize, 16);
            _hinweis.GrowHorizontal = Control.GrowDirection.Both;
            ebene.AddChild(_hinweis);
        }
        _hinweis.Text = text;
        _hinweis.Show();
        var nummer = ++_hinweisNummer; // nur der jüngste Hinweis blendet aus
        GetTree().CreateTimer(5).Timeout += () => { if (nummer == _hinweisNummer) _hinweis?.Hide(); };
    }
}
