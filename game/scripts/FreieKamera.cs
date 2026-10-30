using System;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Freie Kamera zum Ansehen (Nutzer 30.10.2026: „Jede Testszene startet mit FreieKamera“): eine perspektivische Kamera, mit der man frei durch die
/// Szene fliegt, ohne erst Tasten für feste Ansichten zu drücken. Steuerung:
/// <list type="bullet">
/// <item><b>Rechte Maustaste halten</b>: umsehen (die Maus ist dabei gefangen). Ohne sie bleibt die Maus frei für die Oberfläche.</item>
/// <item><b>W/A/S/D</b>: vor, links, zurück, rechts (in Blickrichtung, also auch hinauf oder hinab), <b>Q/E</b>: senkrecht runter/hoch.</item>
/// <item><b>Shift</b>: viermal so schnell, <b>Mausrad</b>: Grundtempo (je Raste × 1,25, 0,5–400 m/s).</item>
/// <item><see cref="HilfeTaste"/> (Vorgabe H): blendet die Hilfe oben rechts (drei kurze Zeilen) aus und ein.</item>
/// </list>
/// Einhängen in eine Testszene: <c>FreieKamera.Einhaengen(this, ort, ziel)</c> – sie wird sofort die aktive Kamera. Die Spielkarte nimmt sie mit der
/// Startoption <c>--freiekamera=1</c> (Stadtansicht.FreieKamera.cs). Eingaben: Maustasten, Mausrad und Hilfetaste über <see cref="_UnhandledInput"/>
/// (was eine Tafel schon genommen hat, kommt nicht an), die Bewegung der Maus beim Umsehen über <see cref="_Input"/>, das Fahren jedes Bild über
/// <see cref="Input.IsKeyPressed"/> (nicht, solange ein Textfeld den Fokus hat oder Strg gedrückt ist).
/// </summary>
public partial class FreieKamera : Camera3D
{
    public const float TempoMin = 0.5f, TempoMax = 400f;

    /// <summary>Grundtempo in m/s (Mausrad ändert es).</summary>
    public float Tempo { get; set; } = 12f;

    /// <summary>Faktor auf das Tempo, solange Shift gedrückt ist.</summary>
    public float ShiftFaktor { get; set; } = 4f;

    /// <summary>Grad Drehung je Pixel Mausbewegung beim Umsehen.</summary>
    public float Empfindlichkeit { get; set; } = 0.15f;

    /// <summary>Taste, die die Hilfezeile aus- und einblendet (Vorgabe H; auf der Spielkarte F2, weil H dort das Hinterzimmer öffnet).</summary>
    public Key HilfeTaste { get; set; } = Key.H;

    /// <summary>Optional: Höhe des Bodens an (x, z) – die Kamera bleibt dann mindestens <see cref="MindestHoehe"/> darüber.</summary>
    public Func<float, float, float>? Bodenhoehe { get; set; }

    /// <summary>So weit (m) bleibt die Kamera über dem Boden, wenn <see cref="Bodenhoehe"/> gesetzt ist.</summary>
    public float MindestHoehe { get; set; } = 0.4f;

    /// <summary>Wird die rechte Maustaste gerade gehalten (umsehen)?</summary>
    public bool Umsehen { get; private set; }

    /// <summary>True, sobald der Spieler die Kamera einmal bewegt oder gedreht hat (die Spielkarte übernimmt bis dahin ihre Startansicht).</summary>
    public bool Gesteuert { get; private set; }

    /// <summary>Drehung um die Hochachse und Neigung (Grad); Neigung −89 (senkrecht hinab) bis 89.</summary>
    private float _gier, _nick;

    private CanvasLayer? _ebene;
    private Label? _hilfe;
    private bool _hilfeSichtbar = true;
    private float _hilfeTempo = -1;

    public FreieKamera()
    {
        Name = "FreieKamera";
        Fov = 60;
        Near = 0.05f;
        Far = 6000; // weit genug für die ganze Karte samt Rand von hoch oben (Godot rechnet die Tiefe umgekehrt, die Ferne bleibt genau)
    }

    /// <summary>Hängt eine freie Kamera unter <paramref name="eltern"/> ein, stellt sie nach <paramref name="ort"/> mit Blick auf <paramref name="ziel"/> und macht sie zur aktiven Kamera.</summary>
    public static FreieKamera Einhaengen(Node eltern, Vector3 ort, Vector3 ziel, Key hilfeTaste = Key.H)
    {
        var kamera = new FreieKamera { HilfeTaste = hilfeTaste };
        eltern.AddChild(kamera);
        kamera.Blicken(ort, ziel);
        kamera.MakeCurrent();
        return kamera;
    }

    /// <summary>Stellt die Kamera nach <paramref name="ort"/> und lässt sie auf <paramref name="ziel"/> blicken (zählt nicht als Steuern).</summary>
    public void Blicken(Vector3 ort, Vector3 ziel)
    {
        var richtung = ziel - ort;
        if (richtung.LengthSquared() < 1e-6f) richtung = Vector3.Forward;
        richtung = richtung.Normalized();
        _gier = Mathf.RadToDeg(Mathf.Atan2(-richtung.X, -richtung.Z));
        _nick = Mathf.Clamp(Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(richtung.Y, -1f, 1f))), -89f, 89f);
        GlobalPosition = ort;
        DrehungSetzen();
    }

    /// <summary>Drehung um die Hochachse in Grad (wie <c>RotationDegrees.Y</c> eines Knotens, der nach −z blickt).</summary>
    public float Gier => _gier;

    public override void _Ready()
    {
        _ebene = new CanvasLayer { Layer = 90, Name = "FreieKameraHilfe" };
        AddChild(_ebene);
        // Oben rechts, drei kurze Zeilen auf dunklem Grund: Dort liegt im Spiel keine Tafel (oben in der Mitte der Zeitstrahl)
        _hilfe = new Label
        {
            AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -372, OffsetRight = -12, OffsetTop = 12,
            HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = Control.MouseFilterEnum.Ignore,
            GrowHorizontal = Control.GrowDirection.Begin,
        };
        _hilfe.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.06f, 0.04f, 0.6f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
        });
        _hilfe.AddThemeFontSizeOverride("font_size", 13);
        _hilfe.AddThemeColorOverride("font_color", new Color(1f, 0.97f, 0.88f));
        _hilfe.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.85f));
        _hilfe.AddThemeConstantOverride("shadow_offset_x", 1);
        _hilfe.AddThemeConstantOverride("shadow_offset_y", 1);
        _ebene.AddChild(_hilfe);
        HilfeNachfuehren();
    }

    public override void _ExitTree()
    {
        if (Umsehen) Input.MouseMode = Input.MouseModeEnum.Visible;
        Umsehen = false;
    }

    /// <summary>Zeigt oder verbirgt die Hilfezeile.</summary>
    public bool HilfeSichtbar
    {
        get => _hilfeSichtbar;
        set
        {
            _hilfeSichtbar = value;
            if (_hilfe is not null) _hilfe.Visible = value;
        }
    }

    public override void _Input(InputEvent eingabe)
    {
        if (!Current) return;
        switch (eingabe)
        {
            case InputEventMouseMotion bewegung when Umsehen:
                _gier -= bewegung.Relative.X * Empfindlichkeit;
                _nick = Mathf.Clamp(_nick - bewegung.Relative.Y * Empfindlichkeit, -89f, 89f);
                DrehungSetzen();
                Gesteuert = true;
                GetViewport().SetInputAsHandled(); // keine Tafel unter der gefangenen Maus
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false } when Umsehen:
                UmsehenBeenden();
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    public override void _UnhandledInput(InputEvent eingabe)
    {
        if (!Current) return;
        switch (eingabe)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
                Umsehen = true;
                Input.MouseMode = Input.MouseModeEnum.Captured;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                Tempo = Mathf.Clamp(Tempo * 1.25f, TempoMin, TempoMax);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                Tempo = Mathf.Clamp(Tempo / 1.25f, TempoMin, TempoMax);
                break;
            case InputEventKey { Pressed: true, Echo: false, CtrlPressed: false, AltPressed: false, ShiftPressed: false, MetaPressed: false } taste
                when taste.Keycode == HilfeTaste:
                HilfeSichtbar = !HilfeSichtbar;
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (!Current)
        {
            if (Umsehen) UmsehenBeenden();
            if (_hilfe is not null && _hilfe.Visible) _hilfe.Visible = false;
            return;
        }
        if (_hilfe is not null && _hilfe.Visible != _hilfeSichtbar) _hilfe.Visible = _hilfeSichtbar;
        if (Umsehen && !Input.IsMouseButtonPressed(MouseButton.Right)) UmsehenBeenden(); // losgelassen über einer Tafel o. Ä.
        HilfeNachfuehren();

        // Fahren – nicht beim Schreiben in ein Textfeld und nicht mit Strg (Strg+S speichert in der Kartenwerkstatt)
        if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit || Input.IsKeyPressed(Key.Ctrl)) return;
        var basis = GlobalTransform.Basis;
        var richtung = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) richtung -= basis.Z;
        if (Input.IsKeyPressed(Key.S)) richtung += basis.Z;
        if (Input.IsKeyPressed(Key.A)) richtung -= basis.X;
        if (Input.IsKeyPressed(Key.D)) richtung += basis.X;
        if (Input.IsKeyPressed(Key.E)) richtung += Vector3.Up;
        if (Input.IsKeyPressed(Key.Q)) richtung -= Vector3.Up;
        if (richtung == Vector3.Zero) return;
        var tempo = Tempo * (Input.IsKeyPressed(Key.Shift) ? ShiftFaktor : 1f);
        var neu = GlobalPosition + richtung.Normalized() * tempo * (float)delta;
        if (Bodenhoehe is { } boden) neu.Y = Mathf.Max(neu.Y, boden(neu.X, neu.Z) + MindestHoehe);
        GlobalPosition = neu;
        Gesteuert = true;
    }

    private void UmsehenBeenden()
    {
        Umsehen = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void DrehungSetzen() => GlobalRotation = new Vector3(Mathf.DegToRad(_nick), Mathf.DegToRad(_gier), 0);

    private void HilfeNachfuehren()
    {
        if (_hilfe is null || Mathf.IsEqualApprox(_hilfeTempo, Tempo)) return;
        _hilfeTempo = Tempo;
        var taste = OS.GetKeycodeString(HilfeTaste);
        _hilfe.Text = $"Freie Kamera · Tempo {Tempo:0.#} m/s (Mausrad)\n" +
                      "rechte Maustaste halten: umsehen · W A S D: fahren\n" +
                      $"Q/E: runter/hoch · Shift: schneller · {taste}: Hilfe aus";
    }
}
