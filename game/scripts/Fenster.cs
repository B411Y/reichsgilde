using Godot;

namespace Reichsgilde;

/// <summary>
/// Anzeige-Einstellungen: Fenster oder Vollbild (auch F11, überall) und die Kantenglättung.
/// Geändert werden sie im Fenster „Einstellungen“ (<see cref="EinstellungenAnzeige"/>), gespeichert in
/// <c>user://einstellungen.cfg</c> und beim Start wieder angewendet.
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
        var geladen = einstellungen.Load(Datei) == Error.Ok;
        if (geladen && einstellungen.GetValue("anzeige", "vollbild", false).AsBool())
            Setzen(true, speichern: false);
        var kanten = geladen ? einstellungen.GetValue("anzeige", "kantenglaettung", "").AsString() : "";
        KantenglaettungSetzen(Enum.TryParse<Kantenglaettung>(kanten, out var k) ? k : StandardKantenglaettung, speichern: false);
    }

    // ── Kantenglättung ──────────────────────────────────────────────────────

    /// <summary>Ohne gespeicherte Wahl: SMAA – glättet unsere klaren Kanten gut und kostet kaum Leistung.</summary>
    public const Kantenglaettung StandardKantenglaettung = Kantenglaettung.Smaa;

    public static Kantenglaettung AktuelleKantenglaettung { get; private set; } = StandardKantenglaettung;

    /// <summary>
    /// Stellt die Kantenglättung am Hauptfenster um. Alle 3D-Ansichten zeichnen in dieses Fenster,
    /// darum gilt die Wahl sofort für Stadt, Innenräume und Hauptmenü.
    /// Es ist immer genau ein Verfahren aktiv: Mehrere zugleich kosten nur Leistung und machen das Bild weicher.
    /// </summary>
    public static void KantenglaettungSetzen(Kantenglaettung art, bool speichern = true)
    {
        AktuelleKantenglaettung = art;
        if (_instanz?.GetTree().Root is { } fenster)
        {
            fenster.Msaa3D = art switch
            {
                Kantenglaettung.Msaa2 => Viewport.Msaa.Msaa2X,
                Kantenglaettung.Msaa4 => Viewport.Msaa.Msaa4X,
                Kantenglaettung.Msaa8 => Viewport.Msaa.Msaa8X,
                _ => Viewport.Msaa.Disabled,
            };
            fenster.ScreenSpaceAA = art switch
            {
                Kantenglaettung.Fxaa => Viewport.ScreenSpaceAAEnum.Fxaa,
                Kantenglaettung.Smaa => Viewport.ScreenSpaceAAEnum.Smaa,
                _ => Viewport.ScreenSpaceAAEnum.Disabled,
            };
            fenster.UseTaa = art == Kantenglaettung.Taa;
        }
        if (speichern) Speichern("kantenglaettung", art.ToString());
    }

    private static void Speichern(string schluessel, Variant wert)
    {
        var einstellungen = new ConfigFile();
        einstellungen.Load(Datei); // andere Einstellungen behalten (fehlt die Datei, bleibt sie leer)
        einstellungen.SetValue("anzeige", schluessel, wert);
        einstellungen.Save(Datei);
    }

    // ── Vollbild ────────────────────────────────────────────────────────────

    public override void _Input(InputEvent eingabe)
    {
        if (eingabe is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            Umschalten();
            GetViewport().SetInputAsHandled();
        }
    }

    public static void Umschalten() => Setzen(!IstVollbild);

    public static void VollbildSetzen(bool vollbild) => Setzen(vollbild);

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
        if (speichern) Speichern("vollbild", vollbild);
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

/// <summary>Die Verfahren zur Kantenglättung, die Godot 4.7 mitbringt (Beschreibung: <see cref="EinstellungenAnzeige"/>).</summary>
public enum Kantenglaettung { Aus, Fxaa, Smaa, Taa, Msaa2, Msaa4, Msaa8 }
