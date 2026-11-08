using Godot;

namespace Reichsgilde;

/// <summary>
/// Testszene „Kaminrauch“ (<c>--rauchtest=1</c>, nur mit Vulkan sinnvoll): vier Häuser mit Schornstein nebeneinander (von links Wohnhaus, Werkstatt, Gießerei,
/// erloschenes Feuer), alle mit dem Rauch der Stadt (<see cref="Kaminrauch"/>, ein Material, ein Zeichenaufruf). Wind, Tageszeit, Nässe und Frost lassen sich
/// stellen. Die Kamera ist eine <see cref="FreieKamera"/>; <c>--rauchsicht</c> wählt den Start.
///
/// Startoptionen (siehe <c>docs/STARTOPTIONEN.md</c>), alle hinter „--“: <c>--rauchwind=&lt;0–1&gt;</c> (Windstärke wie im Spiel: 0,25 leicht, 0,5 Regen, 1 Sturm),
/// <c>--rauchtag=0|1</c>, <c>--rauchnass=&lt;0–1&gt;</c>, <c>--rauchwinter=&lt;0–1&gt;</c>, <c>--rauchsicht=seite|nah|spiel25|spiel50|spiel100</c> (die drei letzten wie
/// die Spielkamera: schräg von oben, orthografisch, 25/50/100 m hoch), <c>--rauchstufe=niedrig|mittel|hoch</c>, <c>--rauchballen=&lt;n&gt;</c>, <c>--rauchende=&lt;Sekunden&gt;</c>.
/// Tasten: 1–4 Wind (0, 0,25, 0,5, 1), N Tag/Nacht, R Nässe, F Frost, B Stufe, V Sicht, Esc Ende; freie Kamera: rechte Maustaste halten, W A S D Q E, Shift, Mausrad, H.
/// </summary>
public partial class Rauchtest : Node
{
    public const string Szene = "res://scenes/Rauchtest.tscn";

    public static bool Angefordert => OS.GetCmdlineUserArgs().Any(a => a == "--rauchtest" || (a.StartsWith("--rauchtest=") && a != "--rauchtest=0"));

    private readonly Dictionary<string, string> _optionen = new();
    private float _wind = 0.25f, _nass, _winter;
    private bool _tag = true;
    private int _sicht;
    private static readonly string[] Sichten = { "seite", "nah", "spiel25", "spiel50", "spiel100" };
    private WorldEnvironment _umgebung = null!;
    private DirectionalLight3D _sonne = null!;
    private FreieKamera _kamera = null!;
    private Label _anzeige = null!;
    private readonly List<Feuer> _kamine = new();
    private double _mess, _ende;
    private int _bilder;

    private string Option(string name, string vorgabe) => _optionen.TryGetValue(name, out var w) ? w : vorgabe;

    public override void _Ready()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var teile = arg.TrimStart('-').Split('=', 2);
            if (teile.Length == 2) _optionen[teile[0]] = teile[1];
        }
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        float Zahl(string n, float v) => float.TryParse(Option(n, ""), System.Globalization.NumberStyles.Float, ci, out var z) ? z : v;
        _wind = Math.Clamp(Zahl("rauchwind", 0.25f), 0f, 1f);
        _nass = Math.Clamp(Zahl("rauchnass", 0f), 0f, 1f);
        _winter = Math.Clamp(Zahl("rauchwinter", 0f), 0f, 1f);
        _tag = Option("rauchtag", "1") != "0";
        _sicht = Math.Max(0, Array.IndexOf(Sichten, Option("rauchsicht", "seite")));
        Kaminrauch.StufeFest = Option("rauchstufe", "").ToLowerInvariant() switch { "niedrig" => 0, "mittel" => 1, "hoch" => 2, _ => -1 };
        if (int.TryParse(Option("rauchballen", "0"), out var b)) Kaminrauch.BallenFest = Math.Max(0, b);
        double.TryParse(Option("rauchende", "0"), System.Globalization.NumberStyles.Float, ci, out _ende);

        Feuer.EbeneSetzen(GetTree(), 1u);   // die Testszene hat keinen Pixel-Look: die Flächen der Feuer auf die Standardebene (Feuer.Ebene steht sonst auf der Teilchenebene)
        UmgebungBauen();
        AddChild(new Kaminrauch());
        BodenBauen();
        HaeuserBauen();
        KameraBauen();
        OberflaecheBauen();
        WetterSetzen();
        TagNachtSetzen();
        SichtSetzen();
        GD.Print($"[rauch] start wind={_wind} tag={_tag} nass={_nass} winter={_winter} sicht={Sichten[_sicht]} stufe={Kaminrauch.StufeFest} renderer={RenderingServer.GetCurrentRenderingMethod()}");
    }

    private void UmgebungBauen()
    {
        var umgebung = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            TonemapMode = Fenster.AktuellesTonemapping == Tonemapping.Agx ? Godot.Environment.ToneMapper.Agx : Godot.Environment.ToneMapper.Linear,
        };
        Stadtansicht.BloomAnwenden(umgebung);
        _umgebung = new WorldEnvironment { Environment = umgebung };
        AddChild(_umgebung);
        _sonne = new DirectionalLight3D { ShadowEnabled = true, DirectionalShadowMaxDistance = 140f };
        AddChild(_sonne);
    }

    private void BodenBauen()
    {
        AddChild(new MeshInstance3D
        {
            Name = "Boden",
            Mesh = new PlaneMesh { Size = new Vector2(600, 600) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.33f, 0.31f, 0.28f), Roughness = 0.95f },
        });
    }

    private static StandardMaterial3D Stoff(Color farbe) => new() { AlbedoColor = farbe, Roughness = 0.9f };

    private void HaeuserBauen()
    {
        var sorten = new[] { RauchSorte.Wohnhaus, RauchSorte.Werkstatt, RauchSorte.Schwer, RauchSorte.Schwer };
        var namen = new[] { "Wohnhaus", "Werkstatt", "Giesserei", "erloschen" };
        var putz = Stoff(new Color(0.68f, 0.62f, 0.52f));
        var dach = Stoff(new Color(0.40f, 0.14f, 0.11f));
        var stein = Stoff(new Color(0.42f, 0.40f, 0.37f));
        for (var i = 0; i < 4; i++)
        {
            var x = (i - 1.5f) * 12f;
            var haus = new Node3D { Name = namen[i], Position = new Vector3(x, 0, 0) };
            AddChild(haus);
            Kasten(haus, new Vector3(7f, 5f, 6f), new Vector3(0, 0, 0), putz);
            Kasten(haus, new Vector3(7.6f, 0.5f, 6.6f), new Vector3(0, 5f, 0), dach);
            Kasten(haus, new Vector3(0.9f, 2.0f, 0.9f), new Vector3(1.8f, 5f, 0), stein);
            var rauch = Feuer.Neu(FeuerVorgabe.Kamin, false, 0.13f + i * 0.21f);
            rauch.Position = new Vector3(1.8f, 7.0f, 0);
            rauch.Sorte = sorten[i];
            rauch.RauchStaerke = i == 3 ? 0f : 1f;
            haus.AddChild(rauch);
            _kamine.Add(rauch);
            var text = new Label3D
            {
                Text = namen[i], Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FixedSize = true, NoDepthTest = true, PixelSize = 0.0011f, FontSize = 34, OutlineSize = 8,
                Modulate = new Color(1f, 0.85f, 0.5f), OutlineModulate = new Color(0, 0, 0, 0.9f), Position = new Vector3(0, 0.4f, 3.4f),
            };
            haus.AddChild(text);
        }
    }

    private static void Kasten(Node3D eltern, Vector3 groesse, Vector3 fuss, Material stoff) =>
        eltern.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = groesse }, MaterialOverride = stoff, Position = fuss + new Vector3(0, groesse.Y * 0.5f, 0) });

    private void KameraBauen()
    {
        _kamera = FreieKamera.Einhaengen(this, new Vector3(0, 8, 40), new Vector3(0, 8, 0));
        _kamera.Bodenhoehe = (_, _) => 0f;
        _kamera.Far = 2000f;
    }

    private void SichtSetzen()
    {
        var s = Sichten[_sicht];
        _kamera.Projection = Camera3D.ProjectionType.Perspective;
        // --rauchkamera=x,y,z:zx,zy,zz setzt Ort und Blickziel selbst (für Bilder)
        var frei = Option("rauchkamera", "").Split(':');
        if (frei.Length == 2 && s == "seite" && Vektor(frei[0]) is { } von && Vektor(frei[1]) is { } nach) { _kamera.Blicken(von, nach); return; }
        if (s == "seite") _kamera.Blicken(new Vector3(0, 11, 34), new Vector3(0, 11, 0));
        else if (s == "nah") _kamera.Blicken(new Vector3(8, 11, 16), new Vector3(8, 11, 0));
        else
        {
            // Wie die Spielkamera: orthografisch, 40° geneigt; die Zahl ist die sichtbare Höhe in Metern
            var hoehe = s switch { "spiel25" => 25f, "spiel50" => 50f, _ => 100f };
            var ziel = new Vector3(s == "spiel25" ? 8f : 0f, 5f, 0f);
            _kamera.Projection = Camera3D.ProjectionType.Orthogonal;
            _kamera.Size = hoehe;
            _kamera.Blicken(ziel + new Vector3(0, Mathf.Sin(Mathf.DegToRad(40f)), Mathf.Cos(Mathf.DegToRad(40f))) * 150f, ziel);
        }
    }

    private static Vector3? Vektor(string text)
    {
        var t = text.Split(',');
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return t.Length == 3 && float.TryParse(t[0], System.Globalization.NumberStyles.Float, ci, out var x) && float.TryParse(t[1], System.Globalization.NumberStyles.Float, ci, out var y)
            && float.TryParse(t[2], System.Globalization.NumberStyles.Float, ci, out var z) ? new Vector3(x, y, z) : null;
    }

    private void WetterSetzen()
    {
        RenderingServer.GlobalShaderParameterSet("wetter_wind", _wind);
        RenderingServer.GlobalShaderParameterSet("wind_richtung", new Vector2(1f, 0.15f));
        RenderingServer.GlobalShaderParameterSet("wetter_naesse", _nass);
        RenderingServer.GlobalShaderParameterSet("jahreszeit_anteile", new Vector4(0, 1f - _winter, 0, _winter));
    }

    private void TagNachtSetzen()
    {
        var u = _umgebung.Environment;
        if (_tag)
        {
            u.BackgroundColor = new Color(0.47f, 0.60f, 0.72f);
            u.AmbientLightColor = new Color(0.75f, 0.75f, 0.80f);
            u.AmbientLightEnergy = 0.5f;
            _sonne.LightEnergy = 1.1f;
            _sonne.LightColor = new Color(1f, 0.97f, 0.9f);
            _sonne.RotationDegrees = new Vector3(-50, 30, 0);
        }
        else
        {
            u.BackgroundColor = new Color(0.015f, 0.02f, 0.04f);
            u.AmbientLightColor = new Color(0.16f, 0.2f, 0.32f);
            u.AmbientLightEnergy = 0.35f;
            _sonne.LightEnergy = 0.12f;
            _sonne.LightColor = new Color(0.55f, 0.65f, 1f);
            _sonne.RotationDegrees = new Vector3(-45, 150, 0);
        }
        Feuer.Umgebung = _tag ? 1f : 0f;
    }

    private void OberflaecheBauen()
    {
        var ebene = new CanvasLayer { Layer = 5 };
        AddChild(ebene);
        _anzeige = new Label { Position = new Vector2(14, 10) };
        _anzeige.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.85f));
        _anzeige.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
        _anzeige.AddThemeConstantOverride("outline_size", 6);
        _anzeige.AddThemeFontSizeOverride("font_size", 16);
        ebene.AddChild(_anzeige);
        if (OS.GetCmdlineUserArgs().Contains("--rauchui=0")) ebene.Visible = false;
        AnzeigeAktualisieren(0);
    }

    private void AnzeigeAktualisieren(double fps) =>
        _anzeige.Text = $"Rauchtest – Wind {_wind:0.00}   {(_tag ? "Tag" : "Nacht")}   Nässe {_nass:0.0}   Frost {_winter:0.0}   Sicht {Sichten[_sicht]}   Bilder/s {fps:F0}\n" +
                        "1–4 Wind (0, 0,25, 0,5, 1)   N Tag/Nacht   R Nässe   F Frost   B Stufe   V Sicht   Esc Ende";

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        switch (k.Keycode)
        {
            case Key.Key1: _wind = 0f; break;
            case Key.Key2: _wind = 0.25f; break;
            case Key.Key3: _wind = 0.5f; break;
            case Key.Key4: _wind = 1f; break;
            case Key.N: _tag = !_tag; TagNachtSetzen(); break;
            case Key.R: _nass = _nass > 0f ? 0f : 1f; break;
            case Key.F: _winter = _winter > 0f ? 0f : 1f; break;
            case Key.B: Kaminrauch.StufeFest = (Math.Max(Kaminrauch.StufeFest, -1) + 1) % 3; break;
            case Key.V: _sicht = (_sicht + 1) % Sichten.Length; SichtSetzen(); break;
            case Key.Escape: GetTree().Quit(); return;
            default: return;
        }
        WetterSetzen();
        AnzeigeAktualisieren(0);
    }

    public override void _Process(double delta)
    {
        Feuer.UhrStellen(delta);
        _mess += delta;
        _bilder++;
        if (_ende > 0 && Time.GetTicksMsec() / 1000.0 > _ende) { GetTree().Quit(); return; }
        if (_mess < 1.0) return;
        var r = RenderingServer.GetRenderingInfo;
        GD.Print($"[rauch] {Kaminrauch.Aktuell?.Zustand()} fps={_bilder / _mess:F1} aufrufe={r(RenderingServer.RenderingInfo.TotalDrawCallsInFrame)} objekte={r(RenderingServer.RenderingInfo.TotalObjectsInFrame)} dreiecke={r(RenderingServer.RenderingInfo.TotalPrimitivesInFrame)}");
        AnzeigeAktualisieren(_bilder / _mess);
        _mess = 0;
        _bilder = 0;
    }
}
