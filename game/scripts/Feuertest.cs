using Godot;

namespace Reichsgilde;

/// <summary>
/// Testszene „Feuer und Flammen“ (<c>--feuertest=1</c>): ein Steinboden mit den sieben Feuerarten nebeneinander, von links nach rechts Kerze,
/// Fackel, Laterne, Feuerkorb, Esse, Kamin und Gebäudebrand, jede vor einer Mauer, auf die ihr Licht fällt. Seit Phase 2 (Nutzer 30.10.2026) zeigt
/// sie nur noch die endgültige Fassung, so wie das Spiel sie auf jeder Grafikstufe zeichnet: Flamme als Flipbook der Blender-Simulation, Rauch und
/// Funken im Shader (<see cref="Feuer"/>). Die Blickrichtung geht nach Norden.
///
/// Startoptionen (siehe <c>docs/STARTOPTIONEN.md</c>): <c>--feuersicht=nah|stadt|fern</c>, <c>--feuerpaar=1–7</c>, <c>--feuertag=1</c>,
/// <c>--feuerzeigen=0|1</c>, <c>--feuerlicht=0|1</c>, <c>--feuerfackeln=&lt;n&gt;</c> (Messung: n Fackeln im Raster, mit <c>--feuerlichter=&lt;m&gt;</c>
/// echten Lichtern), <c>--feuerzyklus=&lt;s&gt;</c>.
/// Tasten: 1–7 oder Pfeile = Feuerart, V = Sicht, N = Tag/Nacht, T = Feuer an/aus, L = Lichter, B = Beschriftung, Esc = Ende. Im Protokoll steht jede
/// Sekunde die Bildrate. Die Kamera ist eine <see cref="FreieKamera"/> (Nutzer 30.10.2026: jede Testszene startet mit ihr): rechte Maustaste halten =
/// umsehen, W/A/S/D/Q/E = fahren, Shift = schneller, Mausrad = Tempo, H = ihre Hilfe. 1–7, Pfeile und V setzen sie auf die Ansicht der Feuerart bzw. der Sicht.
/// </summary>
public partial class Feuertest : Node
{
    public const string Szene = "res://scenes/Feuertest.tscn";

    /// <summary>
    /// Die Startargumente der Testszene, auch wenn das einzelne „--“ fehlt. Godot gibt nur, was hinter „--“ steht, als Spielargument weiter
    /// (<c>OS.GetCmdlineUserArgs()</c>). Steht <c>--feuertest=1</c> ohne „--“ in den Laufzeitargumenten des Editors, landet es ohne Fehlermeldung
    /// bei Godots eigenen Argumenten (<c>OS.GetCmdlineArgs()</c>), und es kam bisher einfach das Hauptmenü (belegt mit Godot 4.7.2, Start aus dem
    /// Editor, 30.10.2026). Darum zählen hier auch die Argumente vor „--“, aber nur die mit <c>--feuer</c> am Anfang: Vor „--“ stehen beim Start
    /// aus dem Editor immer Godots eigene Argumente (<c>--scene</c>, <c>--position</c> …), und schon eine einzige Spieloption ließe das Hauptmenü aus
    /// (<see cref="KlicktestTreiber.SpielOptionen"/>). Alle übrigen Startoptionen bleiben deshalb hinter „--“.
    /// </summary>
    public static IEnumerable<string> Startargumente() =>
        OS.GetCmdlineArgs().Where(a => a.StartsWith("--feuer")).Concat(OS.GetCmdlineUserArgs());

    /// <summary>Soll das Spiel gleich in die Testszene gehen? <c>--feuertest=1</c> oder nur <c>--feuertest</c>, mit oder ohne „--“ davor.</summary>
    public static bool Angefordert =>
        Startargumente().Any(a => a == "--feuertest" || (a.StartsWith("--feuertest=") && a != "--feuertest=0"));

    private enum Sicht { Nah, Stadt, Fern }

    /// <summary>Eine Feuerart mit ihrem Aufbau: halbe Breite des Aufbaus, Abstand zur vorigen, Blickhöhe und -abstand für „nah“, wie weit der Aufbau nach vorn reicht. Maße in Metern.</summary>
    private sealed record Paar(string Name, FeuerVorgabe Vorgabe, float Halb, float Abstand, float NahHoehe, float NahAbstand, float Vorne);

    private static readonly Paar[] Paare =
    {
        new("Kerze", FeuerVorgabe.Kerze, 1.5f, 0f, 0.9f, 0.85f, 0.4f),
        new("Fackel", FeuerVorgabe.Fackel, 2f, 4f, 1.7f, 3.6f, 0.5f),
        new("Laterne", FeuerVorgabe.Laterne, 2f, 4f, 1.6f, 3.4f, 0.5f),
        new("Feuerkorb", FeuerVorgabe.Feuerkorb, 2.25f, 4f, 1.15f, 4.2f, 0.9f),
        new("Esse", FeuerVorgabe.Esse, 2f, 4f, 1.0f, 3.8f, 1.1f),
        new("Kamin", FeuerVorgabe.Kamin, 3.2f, 5f, 3.6f, 11f, 1.6f),
        new("Gebäudebrand", FeuerVorgabe.Gebaeudebrand, 7.5f, 9f, 3.8f, 24f, 3.6f),
    };

    private readonly Dictionary<string, string> _optionen = new();
    private Sicht _sicht = Sicht.Nah;
    private bool _zeigen = true;
    private int _paar = 1;
    private bool _tag, _licht = true, _beschriftung = true;
    private int _fackeln, _fackelLichter = int.MaxValue;
    private sealed record Zustand(string Name, bool Zeigen, int Lichter);
    private readonly List<Zustand> _zyklus = new();
    private int _zyklusNr;
    private double _zyklusSekunden, _zyklusZeit;
    private string _zustand = "-";
    /// <summary>Neigung (Grad) des Blicks in der Sicht „nah“ und in den Sichten „Stadt“/„fern“ (wie die Stadtkamera des Spiels).</summary>
    private const float NahNeigung = 10f, StadtNeigung = 40f;

    private readonly List<Feuer> _feuer = new();
    private readonly List<Node3D> _wurzeln = new();   // je Feuerart ein Aufbau
    private readonly List<Label3D> _beschriftungen = new();
    private float[] _xPaar = Array.Empty<float>();

    private WorldEnvironment _umgebung = null!;
    private DirectionalLight3D _sonne = null!;
    private FreieKamera _kamera = null!;
    private Label _anzeige = null!, _hilfe = null!;

    private double _messZeit, _messCpu, _messGpu;
    private int _messBilder;

    // Gemeinsame Stoffe
    private StandardMaterial3D _stein = null!, _holz = null!, _eisen = null!, _wachs = null!, _glas = null!, _dach = null!, _putz = null!, _kohle = null!, _dunkel = null!;

    public override void _Ready()
    {
        foreach (var arg in Startargumente())
        {
            var teile = arg.TrimStart('-').Split('=', 2);
            if (teile.Length == 2) _optionen[teile[0]] = teile[1];
        }
        _tag = Option("feuertag", "0") == "1";
        _licht = Option("feuerlicht", "1") != "0";
        _zeigen = Option("feuerzeigen", "1") != "0";
        _fackeln = int.TryParse(Option("feuerfackeln", "0"), out var n) ? Math.Max(0, n) : 0;
        if (int.TryParse(Option("feuerlichter", ""), out var m)) _fackelLichter = Math.Max(0, m);
        _sicht = Option("feuersicht", _fackeln > 0 ? "stadt" : "nah").ToLowerInvariant() switch { "stadt" => Sicht.Stadt, "fern" => Sicht.Fern, _ => Sicht.Nah };
        double.TryParse(Option("feuerzyklus", "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _zyklusSekunden);
        _paar = int.TryParse(Option("feuerpaar", "2"), out var p) ? Math.Clamp(p, 1, Paare.Length) - 1 : 1;

        StoffeBauen();
        UmgebungBauen();
        BodenBauen();
        if (_fackeln > 0) FackelRasterBauen(); else PaareBauen();
        KameraBauen();
        OberflaecheBauen();
        TagNachtSetzen();
        SichtSetzen();
        if (_zyklusSekunden > 0) ZyklusBauen();
        ZeigenSetzen();
        GD.Print($"[feuer] start sicht={_sicht} zeigen={_zeigen} tag={_tag} licht={_licht} paar={Paare[_paar].Name} fackeln={_fackeln} lichter={(_fackelLichter == int.MaxValue ? "alle" : _fackelLichter)} " +
                 $"renderer={RenderingServer.GetCurrentRenderingMethod()} fenster={GetViewport().GetVisibleRect().Size}");
    }

    /// <summary>
    /// Messzyklus (<c>--feuerzyklus=&lt;Sekunden&gt;</c>): Die Szene wechselt von selbst durch die Zustände (ohne Feuer, mit Feuer, im Raster mit 0, 12 und
    /// allen Lichtern) und schreibt den Namen des Zustands in jede Protokollzeile. Weil alle Zustände im selben Lauf folgen, trifft die Last des Rechners
    /// sie nahezu gleich; getrennte Läufe schwanken auf einem geteilten Rechner stärker als der Unterschied selbst.
    /// </summary>
    private void ZyklusBauen()
    {
        _zyklus.Add(new Zustand("ohne_Feuer", false, 0));
        if (_fackeln > 0)
            foreach (var lichter in new[] { 0, 12, _fackeln })
                _zyklus.Add(new Zustand($"Feuer_{lichter}_Lichter", true, lichter));
        else
            _zyklus.Add(new Zustand("mit_Feuer", true, int.MaxValue));
        ZyklusSchritt(0);
    }

    private void ZyklusSchritt(int nr)
    {
        _zyklusNr = nr % _zyklus.Count;
        var z = _zyklus[_zyklusNr];
        _zustand = z.Name;
        _zeigen = z.Zeigen;
        foreach (var f in _feuer)
            f.LichtSetzen(_licht && (!f.HasMeta("fackel") || (int)f.GetMeta("fackel") < z.Lichter));
        ZeigenSetzen();
        _zyklusZeit = 0;
    }

    private string Option(string name, string vorgabe) => _optionen.TryGetValue(name, out var w) ? w : vorgabe;

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    private static StandardMaterial3D Stoff(Color farbe, float rauheit = 0.9f, float metall = 0f) =>
        new() { AlbedoColor = farbe, Roughness = rauheit, Metallic = metall };

    private void StoffeBauen()
    {
        _stein = Stoff(new Color(0.46f, 0.44f, 0.41f), 0.95f);
        _holz = Stoff(new Color(0.32f, 0.21f, 0.12f));
        _eisen = Stoff(new Color(0.13f, 0.13f, 0.14f), 0.5f, 0.7f);
        _wachs = Stoff(new Color(0.92f, 0.87f, 0.72f), 0.6f);
        _glas = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.9f, 1f, 0.16f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        _dach = Stoff(new Color(0.40f, 0.14f, 0.11f));
        _putz = Stoff(new Color(0.68f, 0.62f, 0.52f));
        _kohle = Stoff(new Color(0.07f, 0.055f, 0.05f), 1f);
        _dunkel = Stoff(new Color(0.03f, 0.025f, 0.025f), 1f);
    }

    private void UmgebungBauen()
    {
        var umgebung = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            TonemapMode = Fenster.AktuellesTonemapping == Tonemapping.Agx ? Godot.Environment.ToneMapper.Agx : Godot.Environment.ToneMapper.Linear,
        };
        Stadtansicht.BloomAnwenden(umgebung);   // derselbe Bloom wie im Spiel (Stärke nach den Einstellungen)
        _umgebung = new WorldEnvironment { Environment = umgebung };
        AddChild(_umgebung);
        _sonne = new DirectionalLight3D { ShadowEnabled = true, DirectionalShadowMaxDistance = 140f };
        AddChild(_sonne);
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
            // Nacht wie im Spiel: kühles, schwaches Mondlicht; die Feuer sind die Lichtquellen
            u.BackgroundColor = new Color(0.015f, 0.02f, 0.04f);
            u.AmbientLightColor = new Color(0.16f, 0.2f, 0.32f);
            u.AmbientLightEnergy = 0.35f;
            _sonne.LightEnergy = 0.12f;
            _sonne.LightColor = new Color(0.55f, 0.65f, 1f);
            _sonne.RotationDegrees = new Vector3(-45, 150, 0);
        }
        Feuer.Umgebung = _tag ? 1f : 0f;
    }

    private void BodenBauen()
    {
        // Pflaster aus Zellrauschen: Fugen dunkel, Steine hell mit etwas Wechsel (nur für diese Szene)
        var rauschen = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = 0.045f, Seed = 7, CellularDistanceFunction = FastNoiseLite.CellularDistanceFunctionEnum.Euclidean, CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance2Sub };
        var verlauf = new Gradient { Offsets = new[] { 0f, 0.12f, 0.5f, 1f }, Colors = new[] { new Color(0.05f, 0.045f, 0.04f), new Color(0.22f, 0.21f, 0.19f), new Color(0.40f, 0.38f, 0.34f), new Color(0.50f, 0.47f, 0.42f) } };
        var farbe = new NoiseTexture2D { Noise = rauschen, Width = 512, Height = 512, Seamless = true, ColorRamp = verlauf, Normalize = true };
        var normal = new NoiseTexture2D { Noise = rauschen, Width = 512, Height = 512, Seamless = true, AsNormalMap = true, BumpStrength = 5f, Normalize = true };
        var boden = new MeshInstance3D
        {
            Name = "Boden",
            Mesh = new PlaneMesh { Size = new Vector2(400, 400) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = farbe,
                NormalEnabled = true,
                NormalTexture = normal,
                Roughness = 0.92f,
                Uv1Scale = new Vector3(40, 40, 1),
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            },
        };
        AddChild(boden);
    }

    private MeshInstance3D Kasten(Node3D eltern, Vector3 groesse, Vector3 lage, Material stoff, Vector3? drehung = null)
    {
        var m = new MeshInstance3D { Mesh = new BoxMesh { Size = groesse }, MaterialOverride = stoff, Position = lage + new Vector3(0, groesse.Y * 0.5f, 0) };
        if (drehung is { } d) m.RotationDegrees = d;
        eltern.AddChild(m);
        return m;
    }

    private MeshInstance3D Zylinder(Node3D eltern, float radiusOben, float radiusUnten, float hoehe, Vector3 lage, Material stoff, Vector3? drehung = null)
    {
        var m = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = radiusOben, BottomRadius = radiusUnten, Height = hoehe, RadialSegments = 14, Rings = 1 }, MaterialOverride = stoff, Position = lage + new Vector3(0, hoehe * 0.5f, 0) };
        if (drehung is { } d) m.RotationDegrees = d;
        eltern.AddChild(m);
        return m;
    }

    private Feuer FeuerSetzen(Node3D eltern, FeuerVorgabe vorgabe, Vector3 lage, bool mitLicht = true)
    {
        var f = Feuer.Neu(vorgabe, mitLicht);
        f.Position = lage;
        eltern.AddChild(f);
        f.LichtSetzen(_licht);
        _feuer.Add(f);
        return f;
    }

    private void Wand(Node3D eltern, float breite, float hoehe, float z)
    {
        // Eine niedrige Steinmauer hinter dem Feuer: Man sieht, wie das Licht auf sie fällt
        Kasten(eltern, new Vector3(breite, hoehe, 0.35f), new Vector3(0, 0, z), _stein);
    }

    /// <summary>Die sieben Aufbauten nebeneinander (von links nach rechts), jeder mit seinem Feuer und seinem Namen davor.</summary>
    private void PaareBauen()
    {
        _xPaar = new float[Paare.Length];
        var x = 0f;
        for (var i = 0; i < Paare.Length; i++)
        {
            var paar = Paare[i];
            x += i == 0 ? 0f : Paare[i - 1].Halb + paar.Abstand + paar.Halb;
            _xPaar[i] = x;
            var wurzel = new Node3D { Name = paar.Name, Position = new Vector3(x, 0, 0) };
            wurzel.SetMeta("paar", i);
            AddChild(wurzel);
            _wurzeln.Add(wurzel);
            PropBauen(wurzel, paar);
            var text = new Label3D
            {
                Text = paar.Name,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FixedSize = true,
                NoDepthTest = true,
                PixelSize = 0.0011f,
                FontSize = 34,
                OutlineSize = 8,
                Modulate = new Color(1f, 0.85f, 0.5f),
                OutlineModulate = new Color(0, 0, 0, 0.9f),
                Position = new Vector3(0, 0.04f, paar.Vorne + 0.25f),
                RenderPriority = 10,
            };
            text.SetMeta("paar", i);
            wurzel.AddChild(text);
            _beschriftungen.Add(text);
        }
        // Die Mitte der Reihe liegt bei x = 0
        var mitte = (_xPaar[0] + _xPaar[^1]) * 0.5f;
        for (var i = 0; i < Paare.Length; i++)
        {
            _xPaar[i] -= mitte;
            _wurzeln[i].Position = new Vector3(_xPaar[i], 0, 0);
        }
    }

    private void PropBauen(Node3D w, Paar paar)
    {
        switch (paar.Vorgabe)
        {
            case FeuerVorgabe.Kerze:
                Kasten(w, new Vector3(0.5f, 0.74f, 0.5f), Vector3.Zero, _stein);
                Zylinder(w, 0.03f, 0.04f, 0.03f, new Vector3(0, 0.74f, 0), _eisen);
                Zylinder(w, 0.011f, 0.012f, 0.11f, new Vector3(0, 0.77f, 0), _wachs);
                Wand(w, 3f, 2.4f, -0.55f);
                FeuerSetzen(w, FeuerVorgabe.Kerze, new Vector3(0, 0.88f, 0));
                break;
            case FeuerVorgabe.Fackel:
                Zylinder(w, 0.03f, 0.035f, 1.55f, Vector3.Zero, _holz);
                Zylinder(w, 0.085f, 0.04f, 0.17f, new Vector3(0, 1.55f, 0), _eisen);
                Zylinder(w, 0.07f, 0.07f, 0.04f, new Vector3(0, 1.7f, 0), _kohle);
                Wand(w, 4f, 2.6f, -0.85f);
                FeuerSetzen(w, FeuerVorgabe.Fackel, new Vector3(0, 1.72f, 0));
                break;
            case FeuerVorgabe.Laterne:
                Zylinder(w, 0.025f, 0.03f, 1.48f, Vector3.Zero, _eisen);
                Kasten(w, new Vector3(0.2f, 0.03f, 0.2f), new Vector3(0, 1.48f, 0), _eisen);
                Kasten(w, new Vector3(0.17f, 0.22f, 0.17f), new Vector3(0, 1.51f, 0), _glas);
                Kasten(w, new Vector3(0.22f, 0.04f, 0.22f), new Vector3(0, 1.73f, 0), _eisen);
                Zylinder(w, 0.003f, 0.012f, 0.05f, new Vector3(0, 1.51f, 0), _wachs);
                Wand(w, 4f, 2.6f, -0.85f);
                FeuerSetzen(w, FeuerVorgabe.Laterne, new Vector3(0, 1.555f, 0));
                break;
            case FeuerVorgabe.Feuerkorb:
                for (var k = 0; k < 3; k++)
                {
                    var winkel = k * 120f + 20f;
                    var dx = Mathf.Cos(Mathf.DegToRad(winkel));
                    var dz = Mathf.Sin(Mathf.DegToRad(winkel));
                    Zylinder(w, 0.02f, 0.025f, 1.0f, new Vector3(dx * 0.2f, 0, dz * 0.2f), _eisen, new Vector3(-dz * 9f, 0, dx * 9f));
                }
                Zylinder(w, 0.36f, 0.22f, 0.22f, new Vector3(0, 0.92f, 0), _eisen);
                Zylinder(w, 0.31f, 0.31f, 0.02f, new Vector3(0, 1.13f, 0), _kohle);
                for (var k = 0; k < 3; k++)
                    Zylinder(w, 0.045f, 0.045f, 0.5f, new Vector3(0, 0.92f + k * 0.03f, 0), _kohle, new Vector3(90f, k * 60f, 0));
                Wand(w, 4.5f, 2.6f, -1.1f);
                FeuerSetzen(w, FeuerVorgabe.Feuerkorb, new Vector3(0, 1.17f, 0));
                break;
            case FeuerVorgabe.Esse:
                Kasten(w, new Vector3(1.3f, 0.85f, 1.0f), Vector3.Zero, _stein);
                Zylinder(w, 0.42f, 0.42f, 0.02f, new Vector3(0, 0.85f, 0), _dunkel);
                Kasten(w, new Vector3(1.3f, 1.7f, 0.3f), new Vector3(0, 0.85f, -0.48f), _stein);
                Kasten(w, new Vector3(0.5f, 0.12f, 0.25f), new Vector3(0.9f, 0.85f, 0.1f), _eisen);   // Amboss-Block daneben
                FeuerSetzen(w, FeuerVorgabe.Esse, new Vector3(0, 0.88f, 0));
                break;
            case FeuerVorgabe.Kamin:
                HausBauen(w, 2.4f, 1.6f, 2.4f, false);
                Kasten(w, new Vector3(0.45f, 1.3f, 0.45f), new Vector3(0.7f, 1.6f, -0.3f), _stein);
                FeuerSetzen(w, FeuerVorgabe.Kamin, new Vector3(0.7f, 2.92f, -0.3f), false);
                break;
            case FeuerVorgabe.Gebaeudebrand:
                HausBauen(w, 6.5f, 3.6f, 5.5f, true);
                Kasten(w, new Vector3(0.6f, 1.6f, 0.6f), new Vector3(2.0f, 3.6f, -1.2f), _stein);
                FeuerSetzen(w, FeuerVorgabe.Gebaeudebrand, new Vector3(0, 4.6f, 0));
                FeuerSetzen(w, FeuerVorgabe.Fensterbrand, new Vector3(-1.8f, 1.3f, 2.85f), false);
                FeuerSetzen(w, FeuerVorgabe.Fensterbrand, new Vector3(1.8f, 1.3f, 2.85f), false);
                break;
        }
    }

    /// <summary>Ein einfaches Haus: Wände, Satteldach, dunkle Fenster und Tür vorn (nach +Z).</summary>
    private void HausBauen(Node3D w, float breite, float hoehe, float tiefe, bool brennt)
    {
        Kasten(w, new Vector3(breite, hoehe, tiefe), Vector3.Zero, brennt ? Stoff(new Color(0.45f, 0.38f, 0.3f)) : _putz);
        var dachHoehe = breite * 0.28f;
        var dach = new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = new Vector3(breite + 0.5f, dachHoehe, tiefe + 0.5f) },
            MaterialOverride = _dach,
            Position = new Vector3(0, hoehe + dachHoehe * 0.5f, 0),
        };
        w.AddChild(dach);
        var fensterStoff = brennt ? _dunkel : Stoff(new Color(0.12f, 0.14f, 0.18f), 0.2f);
        var fy = hoehe * 0.38f;
        var vorn = tiefe * 0.5f + 0.02f;
        foreach (var x in new[] { -breite * 0.28f, breite * 0.28f })
            Kasten(w, new Vector3(0.9f, 1.1f, 0.06f), new Vector3(x, fy, vorn - 0.02f), fensterStoff);
        Kasten(w, new Vector3(0.9f, 1.8f, 0.06f), new Vector3(0, 0, vorn - 0.02f), _holz);
    }

    private void FackelRasterBauen()
    {
        // Messung: n Fackeln im Raster (10 je Reihe, 3,5 m Abstand), mit Pfahl und Schale
        var spalten = 10;
        var reihen = (_fackeln + spalten - 1) / spalten;
        var wurzel = new Node3D { Name = "Fackeln" };
        AddChild(wurzel);
        _xPaar = new[] { 0f };
        for (var i = 0; i < _fackeln; i++)
        {
            var x = (i % spalten - (spalten - 1) * 0.5f) * 3.5f;
            var z = -(i / spalten - (reihen - 1) * 0.5f) * 3.5f;
            var stelle = new Node3D { Position = new Vector3(x, 0, z) };
            wurzel.AddChild(stelle);
            Zylinder(stelle, 0.03f, 0.035f, 1.55f, Vector3.Zero, _holz);
            Zylinder(stelle, 0.085f, 0.04f, 0.17f, new Vector3(0, 1.55f, 0), _eisen);
            var f = FeuerSetzen(stelle, FeuerVorgabe.Fackel, new Vector3(0, 1.72f, 0), i < _fackelLichter);
            f.SetMeta("fackel", i);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kamera
    // ═════════════════════════════════════════════════════════════════════════

    private void KameraBauen()
    {
        // Ort und Ziel setzt gleich SichtSetzen; der Boden liegt bei y = 0, die Kamera bleibt darüber
        _kamera = FreieKamera.Einhaengen(this, new Vector3(0, 2, 10), Vector3.Zero);
        _kamera.Bodenhoehe = (_, _) => 0f;
        _kamera.MindestHoehe = 0.15f;
    }

    /// <summary>
    /// Setzt die freie Kamera auf die Ansicht der gewählten Sicht (Sprung, danach fliegt man frei weiter). Die Kamera bleibt perspektivisch (Fov 60):
    /// „nah“ blickt wie die Raumansicht des Spiels (dort Fov 50) leicht von oben auf die Feuerart, der Abstand ist so umgerechnet, dass sie gleich groß
    /// erscheint; „Stadt“ und „fern“ blicken 40° von oben wie die Stadtkamera, aus dem Abstand, in dem das Bild so hoch ist wie deren Orthobild
    /// (Größe 25 bzw. 95 m).
    /// </summary>
    private void SichtSetzen()
    {
        var paar = _fackeln > 0 ? null : Paare[_paar];
        var xMitte = _fackeln > 0 ? 0f : _xPaar[_paar];
        var halbFov = Mathf.DegToRad(_kamera.Fov * 0.5f);
        Vector3 ziel;
        float abstand, neigung;
        switch (_sicht)
        {
            case Sicht.Nah:
                ziel = new Vector3(xMitte, paar?.NahHoehe ?? 1.5f, 0);
                abstand = (paar?.NahAbstand ?? 12f) * Mathf.Tan(Mathf.DegToRad(25f)) / Mathf.Tan(halbFov);
                neigung = NahNeigung;
                break;
            default:
                ziel = new Vector3(_sicht == Sicht.Fern ? 0f : xMitte, 0, 0);
                abstand = (_sicht == Sicht.Stadt ? 25f : 95f) * 0.5f / Mathf.Tan(halbFov);
                neigung = StadtNeigung;
                break;
        }
        // Der Blick geht nach Norden (−z): die Kamera steht südlich (+z) des Ziels und um die Neigung erhöht
        var n = Mathf.DegToRad(neigung);
        _kamera.Blicken(ziel + new Vector3(0, Mathf.Sin(n), Mathf.Cos(n)) * abstand, ziel);
        // Die Lichter blenden nach dem echten Abstand zur Kamera aus (der Zuschlag ist für die Orthokamera des Spiels in 150 m Abstand)
        Feuer.AusblendenZuschlag = 0f;
        foreach (var f in _feuer) f.AusblendenNeu();
        BeschriftungSetzen();
    }

    private void ZeigenSetzen()
    {
        foreach (var f in _feuer) f.Visible = _zeigen;
        BeschriftungSetzen();
    }

    /// <summary>Nah: nur der Name der gewählten Feuerart; Stadt alle; fern nur die großen (Kamin, Gebäudebrand), die kleinen stehen zu dicht.</summary>
    private void BeschriftungSetzen()
    {
        foreach (var l in _beschriftungen)
        {
            var paar = (int)l.GetMeta("paar");
            l.Visible = _beschriftung && _sicht switch { Sicht.Nah => paar == _paar, Sicht.Fern => paar >= 5, _ => true };
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Oberfläche, Eingabe, Messung
    // ═════════════════════════════════════════════════════════════════════════

    private void OberflaecheBauen()
    {
        var ebene = new CanvasLayer { Layer = 5 };
        AddChild(ebene);
        _anzeige = new Label { Position = new Vector2(14, 10) };
        _hilfe = new Label { Position = new Vector2(14, 0), AnchorTop = 1f, AnchorBottom = 1f, OffsetTop = -68f, OffsetBottom = -10f };
        foreach (var l in new[] { _anzeige, _hilfe })
        {
            l.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.85f));
            l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
            l.AddThemeConstantOverride("outline_size", 6);
            l.AddThemeFontSizeOverride("font_size", 16);
            ebene.AddChild(l);
        }
        _hilfe.Text = "1–7 / Pfeile: Feuerart   V: Sicht (nah, Stadt, fern)   N: Nacht/Tag   T: Feuer an/aus   L: Lichter   B: Beschriftung   Esc: Ende\n" +
                      "(1–7, Pfeile und V setzen die freie Kamera auf die Ansicht)";
        AnzeigeAktualisieren(0);
    }

    private void AnzeigeAktualisieren(double fps)
    {
        var kopf = _fackeln > 0
            ? $"Feuertest – Messung: {_fackeln} Fackeln{(_zeigen ? "" : " (ohne Feuer, nur Pfähle)")}"
            : "Feuertest – so brennt es im Spiel (alle Grafikstufen): Flamme aus der Simulation, Rauch und Funken im Shader";
        var paar = _fackeln > 0 ? "" : $"   {_paar + 1}: {Paare[_paar].Name}";
        _anzeige.Text = $"{kopf}\nSicht: {_sicht}{paar}   {(_tag ? "Tag" : "Nacht")}   Lichter: {(_licht ? "an" : "aus")}   Feuer: {(_zeigen ? "an" : "aus")}   Bilder/s: {fps:F0}";
    }

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey { Pressed: true, Echo: false } k:
                switch (k.Keycode)
                {
                    case >= Key.Key1 and <= Key.Key7: PaarWaehlen((int)k.Keycode - (int)Key.Key1); break;
                    case Key.Right: PaarWaehlen((_paar + 1) % Paare.Length); break;
                    case Key.Left: PaarWaehlen((_paar + Paare.Length - 1) % Paare.Length); break;
                    case Key.V: _sicht = (Sicht)(((int)_sicht + 1) % 3); SichtSetzen(); AnzeigeAktualisieren(0); break;
                    case Key.N: _tag = !_tag; TagNachtSetzen(); AnzeigeAktualisieren(0); break;
                    case Key.T: _zeigen = !_zeigen; ZeigenSetzen(); AnzeigeAktualisieren(0); break;
                    case Key.L:
                        _licht = !_licht;
                        foreach (var f in _feuer) f.LichtSetzen(_licht);
                        AnzeigeAktualisieren(0);
                        break;
                    case Key.B: _beschriftung = !_beschriftung; BeschriftungSetzen(); break;   // H gehört der freien Kamera (ihre Hilfe)
                    case Key.Escape: GetTree().Quit(); break;
                }
                break;
            // Rechte Maustaste und Mausrad gehören der freien Kamera (umsehen, Tempo)
        }
    }

    private void PaarWaehlen(int i)
    {
        if (_fackeln > 0) return;
        _paar = Math.Clamp(i, 0, Paare.Length - 1);
        SichtSetzen();
        AnzeigeAktualisieren(0);
    }

    public override void _Process(double delta)
    {
        Feuer.UhrStellen(delta);   // auch die Flammen ohne eigenes Licht laufen im Takt
        if (_zyklus.Count > 0)
        {
            _zyklusZeit += delta;
            if (_zyklusZeit >= _zyklusSekunden) ZyklusSchritt(_zyklusNr + 1);
        }
        // Messung: jede Sekunde die mittlere Bildrate und Bildzeit ins Protokoll
        // Die Zeit, die der Zeichner für das Bild braucht (Hauptprozessor und Grafikkarte), unabhängig vom Anzeigen im Fenster: Unter Xvfb ist das Anzeigen
        // bei viel Last der Engpass und verfälscht die Bildrate. Die Messung läuft ab dem zweiten Bild (ViewportSetMeasureRenderTime).
        var kasten = GetViewport().GetViewportRid();
        if (_messBilder == 0 && _messZeit == 0) RenderingServer.ViewportSetMeasureRenderTime(kasten, true);
        _messCpu += RenderingServer.ViewportGetMeasuredRenderTimeCpu(kasten);
        _messGpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(kasten);
        _messZeit += delta;
        _messBilder++;
        if (_messZeit < 1.0) return;
        var fps = _messBilder / _messZeit;
        var ms = _messZeit / _messBilder * 1000.0;
        var r = RenderingServer.GetRenderingInfo;
        var cpu = _messCpu / _messBilder;
        var gpu = _messGpu / _messBilder;
        GD.Print($"[feuer] fps={fps:F1} ms={ms:F2} gpu={gpu:F2} cpu={cpu:F2} zustand={_zustand} sicht={_sicht} zeigen={_zeigen} tag={_tag} licht={_licht} paar={(_fackeln > 0 ? "-" : Paare[_paar].Name)} " +
                 $"feuer={_feuer.Count(f => f.Visible)} aufrufe={r(RenderingServer.RenderingInfo.TotalDrawCallsInFrame)} objekte={r(RenderingServer.RenderingInfo.TotalObjectsInFrame)} " +
                 $"dreiecke={r(RenderingServer.RenderingInfo.TotalPrimitivesInFrame)}");
        AnzeigeAktualisieren(fps);
        _messZeit = _messCpu = _messGpu = 0;
        _messBilder = 0;
    }
}
