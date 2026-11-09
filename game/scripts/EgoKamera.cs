using System;
using Godot;

namespace Reichsgilde;

/// <summary>
/// <b>Ego-Perspektive</b> (Entwicklerfenster, Nutzer 09.11.2026): Man steht in der Stadt und läuft wie in einem Ego-Shooter herum. Eine reine Ansicht, nichts
/// davon geht an den Kern. Aufbau wie die <see cref="FreieKamera"/> (gleiche Perspektive, Nahebene 5 cm, weite Ferne; die Stadtansicht behandelt beide gleich:
/// Spielkamera, Schatten und Dunst richten sich nach der aktiven perspektivischen Kamera), nur steuert man hier eine Figur statt einer Kamera:
/// <list type="bullet">
/// <item><b>Maus</b> schaut (die Maus ist gefangen), <b>W/A/S/D</b> läuft, <b>Umschalt</b> rennt, <b>Leertaste</b> springt, <b>Esc</b> beendet (<see cref="Beendet"/>).</item>
/// <item>Augenhöhe <see cref="Augenhoehe"/> (1,65 m), Gehtempo <see cref="Gehtempo"/> (1,4 m/s), Rennen <see cref="Renntempo"/> (4 m/s).</item>
/// <item><b>Boden</b>: Die Füße stehen auf <see cref="Fusshoehe"/> (Gelände, Fundamente, Brückendecks – die Stadtansicht liefert sie). Stufen bis
/// <see cref="Stufenhoehe"/> steigt man, höher geht es nicht; fällt der Boden tiefer ab, fällt man (Schwerkraft), die Leertaste springt.</item>
/// <item><b>Hindernisse</b>: <see cref="Herausschieben"/> schiebt den Kreis der Füße (Halbmesser <see cref="Radius"/>) aus Gebäuden und Mauern (gleitet an der Wand
/// entlang), <see cref="Gesperrt"/> hält vom Wasser fern; beides liefert die Stadtansicht (Stadtansicht.Ego.cs).</item>
/// <item>Die Oberfläche ist während des Modus ausgeblendet; zu sehen ist nur der kleine Hinweis „Esc: zurück“. Alle Tasten außer den Ziffern (Tempo) nimmt die Kamera
/// selbst, damit kein Kürzel des Spiels (Leertaste = Pause, F = Familie …) hindurchgeht.</item>
/// </list>
/// </summary>
public partial class EgoKamera : FreieKamera
{
    public const float Augenhoehe = 1.65f, Gehtempo = 1.4f, Renntempo = 4f, Radius = 0.45f, Stufenhoehe = 0.5f;
    public const float Sprunggeschwindigkeit = 4.2f, Schwerkraft = 9.81f;

    /// <summary>Höhe des Bodens, auf dem man bei (x, z) steht (Gelände, Fundament, Deck).</summary>
    public Func<float, float, float> Fusshoehe { get; set; } = (_, _) => 0f;

    /// <summary>Ist der Ort (x, z) gesperrt (Wasser ohne Deck, Rand der Karte)?</summary>
    public Func<Vector2, bool> Gesperrt { get; set; } = _ => false;

    /// <summary>Schiebt einen Ort (x, z) mit Fußhöhe aus Gebäuden und Mauern heraus und gibt den neuen Ort zurück.</summary>
    public Func<Vector2, float, Vector2> Herausschieben { get; set; } = (p, _) => p;

    /// <summary>Esc gedrückt: Der Modus soll enden (die Stadtansicht räumt auf und stellt die Spielkamera wieder her).</summary>
    public event Action? Beendet;

    /// <summary>Ort der Füße (x, z in Metern) und ihre Höhe.</summary>
    public Vector2 Ort => _ort;

    /// <summary>Höhe der Füße über dem Meeresspiegel der Karte.</summary>
    public float FussY => _fussY;

    private Vector2 _ort;
    private float _fussY, _vy, _augeY;
    private bool _amBoden = true;
    private CanvasLayer? _hinweisEbene;

    public EgoKamera()
    {
        Name = "EgoKamera";
        Empfindlichkeit = 0.12f;
    }

    /// <summary>Stellt die Figur auf den Ort (x, z) mit Blick in Richtung <paramref name="gier"/> (Grad, 0 = nach −z) und Neigung <paramref name="nick"/>; die Füße stehen auf dem Boden.</summary>
    public void Aufstellen(Vector2 ort, float gier, float nick = 0f)
    {
        _ort = ort;
        _fussY = Fusshoehe(ort.X, ort.Y);
        _vy = 0;
        _amBoden = true;
        _augeY = _fussY + Augenhoehe;
        _gier = gier;
        _nick = Mathf.Clamp(nick, -85f, 85f);
        GlobalPosition = new Vector3(ort.X, _augeY, ort.Y);
        DrehungSetzen();
    }

    public override void _Ready()
    {
        _hinweisEbene = new CanvasLayer { Layer = 90, Name = "EgoHinweis" };
        AddChild(_hinweisEbene);
        var hinweis = new Label
        {
            Text = "Esc: zurück", MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0, AnchorRight = 0, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 12, OffsetTop = -36, OffsetRight = 160, OffsetBottom = -12,
            GrowVertical = Control.GrowDirection.Begin,
        };
        hinweis.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.06f, 0.04f, 0.6f), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 3, ContentMarginBottom = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
        });
        hinweis.AddThemeFontSizeOverride("font_size", 13);
        hinweis.AddThemeColorOverride("font_color", new Color(1f, 0.97f, 0.88f));
        _hinweisEbene.AddChild(hinweis);
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _ExitTree() => Input.MouseMode = Input.MouseModeEnum.Visible;

    public override void _Input(InputEvent eingabe)
    {
        if (!Current) return;
        switch (eingabe)
        {
            case InputEventMouseMotion bewegung:
                _gier -= bewegung.Relative.X * Empfindlichkeit;
                _nick = Mathf.Clamp(_nick - bewegung.Relative.Y * Empfindlichkeit, -85f, 85f);
                DrehungSetzen();
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }:
                GetViewport().SetInputAsHandled(); // vor dem Beenden: danach hängt die Kamera nicht mehr im Baum, und Esc soll nicht das Spielmenü öffnen
                Beendet?.Invoke();
                return;
            case InputEventKey { Keycode: >= Key.Key1 and <= Key.Key9 }:
                return; // Tempo der Spielzeit bleibt bedienbar (Hauptszene)
            case InputEventKey:
            case InputEventMouseButton:
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledInput(InputEvent eingabe) { } // alles Nötige nimmt _Input

    public override void _Process(double delta)
    {
        if (!Current) return;
        var dt = (float)Math.Min(delta, 0.1);
        if (Input.MouseMode != Input.MouseModeEnum.Captured) Input.MouseMode = Input.MouseModeEnum.Captured; // etwa nach einem Fenster davor

        // Wunschrichtung auf dem Boden: nur die Drehung um die Hochachse zählt, nicht die Neigung
        var gier = Mathf.DegToRad(_gier);
        var vor = new Vector2(-Mathf.Sin(gier), -Mathf.Cos(gier));
        var rechts = new Vector2(Mathf.Cos(gier), -Mathf.Sin(gier));
        var richtung = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W)) richtung += vor;
        if (Input.IsKeyPressed(Key.S)) richtung -= vor;
        if (Input.IsKeyPressed(Key.D)) richtung += rechts;
        if (Input.IsKeyPressed(Key.A)) richtung -= rechts;
        if (richtung != Vector2.Zero)
        {
            var tempo = Input.IsKeyPressed(Key.Shift) ? Renntempo : Gehtempo;
            Gehen(richtung.Normalized() * tempo * dt);
        }
        else Gehen(Vector2.Zero); // auch im Stehen aus einer Wand schieben, wenn dort ein Gebäude gebaut wird

        Senkrecht(dt);

        // Die Augen folgen dem Boden weich (Stufen ruckeln sonst), in der Luft genau
        var soll = _fussY + Augenhoehe;
        _augeY = _amBoden ? Mathf.Lerp(_augeY, soll, 1f - Mathf.Exp(-25f * dt)) : soll;
        GlobalPosition = new Vector3(_ort.X, _augeY, _ort.Y);
    }

    /// <summary>Kann man mit den Füßen auf Höhe <see cref="_fussY"/> dorthin treten (kein Wasser, keine zu hohe Stufe)?</summary>
    private bool Begehbar(Vector2 p) => !Gesperrt(p) && Fusshoehe(p.X, p.Y) - _fussY <= Stufenhoehe;

    private void Gehen(Vector2 schritt)
    {
        var ziel = Herausschieben(_ort + schritt, _fussY);
        if (Begehbar(ziel)) { _ort = ziel; return; }
        // an einer Stufe oder am Wasser: an der Kante entlang gleiten (erst x, dann z)
        var nurX = Herausschieben(new Vector2(_ort.X + schritt.X, _ort.Y), _fussY);
        if (Begehbar(nurX)) { _ort = nurX; return; }
        var nurZ = Herausschieben(new Vector2(_ort.X, _ort.Y + schritt.Y), _fussY);
        if (Begehbar(nurZ)) _ort = nurZ;
    }

    private void Senkrecht(float dt)
    {
        var boden = Fusshoehe(_ort.X, _ort.Y);
        if (_amBoden)
        {
            if (Input.IsKeyPressed(Key.Space)) { _vy = Sprunggeschwindigkeit; _amBoden = false; }
            else if (Mathf.Abs(boden - _fussY) <= Stufenhoehe) { _fussY = boden; return; }
            else _amBoden = false; // der Boden fällt tief ab
        }
        _vy -= Schwerkraft * dt;
        _fussY += _vy * dt;
        if (_fussY <= boden && _vy <= 0) { _fussY = boden; _vy = 0; _amBoden = true; }
    }
}
