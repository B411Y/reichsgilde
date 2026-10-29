using Godot;

namespace Reichsgilde;

/// <summary>
/// Testszene „Feuer und Flammen“, Phase 1 (<c>--feuertest=1</c>): ein Steinboden mit sieben Feuerarten, links jede in Technik A (Shader-Flamme),
/// rechts jede in Technik B (Partikel-Flamme), Seite an Seite, damit man beide vergleichen kann. Die Paare stehen hintereinander (Kerze,
/// Fackel, Laterne, Feuerkorb, Esse, Kamin, Gebäudebrand), die Blickrichtung geht nach Norden.
///
/// Startoptionen (siehe <c>docs/STARTOPTIONEN.md</c>): <c>--feuersicht=nah|stadt|fern</c>, <c>--feuerpaar=1–7</c>, <c>--feuertag=1</c>,
/// <c>--feuerart=A|B|keins|beide</c>, <c>--feuerlicht=0|1</c>, <c>--feuerfackeln=&lt;n&gt;</c> (Messung: n Fackeln im Raster, mit
/// <c>--feuerlichter=&lt;m&gt;</c> echten Lichtern). Tasten: 1–7 oder Pfeile = Paar, V = Sicht, N = Tag/Nacht, T = A/B/beide/keins,
/// L = Lichter, H = Beschriftung, rechte Maustaste ziehen = drehen, Rad = nähern, Esc = Ende. Im Protokoll steht jede Sekunde die Bildrate.
/// </summary>
public partial class Feuertest : Node
{
    public const string Szene = "res://scenes/Feuertest.tscn";

    private enum Sicht { Nah, Stadt, Fern }
    private enum Zeigen { Beide, NurA, NurB, Keins }

    /// <summary>Ein Paar: dieselbe Feuerart links in A und rechts in B. Maße in Metern.</summary>
    private sealed record Paar(string Name, FeuerVorgabe Vorgabe, float Halb, float Abstand, float NahHoehe, float NahAbstand, float Vorne);

    private static readonly Paar[] Paare =
    {
        new("Kerze", FeuerVorgabe.Kerze, 0.45f, 0f, 0.9f, 0.85f, 0.4f),
        new("Fackel", FeuerVorgabe.Fackel, 1.1f, 6f, 1.7f, 3.6f, 0.5f),
        new("Laterne", FeuerVorgabe.Laterne, 1.1f, 6f, 1.6f, 3.4f, 0.5f),
        new("Feuerkorb", FeuerVorgabe.Feuerkorb, 1.5f, 6f, 1.15f, 4.2f, 0.9f),
        new("Esse", FeuerVorgabe.Esse, 1.5f, 6f, 1.0f, 3.8f, 1.1f),
        new("Kamin", FeuerVorgabe.Kamin, 3.2f, 10f, 3.6f, 11f, 1.6f),
        new("Gebäudebrand", FeuerVorgabe.Gebaeudebrand, 7.5f, 16f, 3.8f, 24f, 3.6f),
    };

    private readonly Dictionary<string, string> _optionen = new();
    private Sicht _sicht = Sicht.Nah;
    private Zeigen _zeigen = Zeigen.Beide;
    private int _paar = 1;
    private bool _tag, _licht = true, _beschriftung = true;
    private int _fackeln, _fackelLichter = int.MaxValue;
    private float _gierung, _neigung = 10f, _nahFaktor = 1f, _zoomFaktor = 1f;

    private readonly List<Feuer> _feuer = new();
    private readonly List<Node3D> _wurzelA = new(), _wurzelB = new();   // je Paar eine Wurzel links (A) und rechts (B)
    private readonly List<Label3D> _beschriftungen = new();
    private float[] _zPaar = Array.Empty<float>();

    private WorldEnvironment _umgebung = null!;
    private DirectionalLight3D _sonne = null!;
    private Camera3D _kamera = null!;
    private Node3D _kameraDrehung = null!, _kameraNeigung = null!;
    private Label _anzeige = null!, _hilfe = null!;

    private double _messZeit;
    private int _messBilder;

    // Gemeinsame Stoffe
    private StandardMaterial3D _stein = null!, _holz = null!, _eisen = null!, _wachs = null!, _glas = null!, _dach = null!, _putz = null!, _kohle = null!, _dunkel = null!;

    public override void _Ready()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var teile = arg.TrimStart('-').Split('=', 2);
            if (teile.Length == 2) _optionen[teile[0]] = teile[1];
        }
        _tag = Option("feuertag", "0") == "1";
        _licht = Option("feuerlicht", "1") != "0";
        _fackeln = int.TryParse(Option("feuerfackeln", "0"), out var n) ? Math.Max(0, n) : 0;
        if (int.TryParse(Option("feuerlichter", ""), out var m)) _fackelLichter = Math.Max(0, m);
        _zeigen = Option("feuerart", "beide").ToUpperInvariant() switch { "A" => Zeigen.NurA, "B" => Zeigen.NurB, "KEINS" => Zeigen.Keins, _ => Zeigen.Beide };
        _sicht = Option("feuersicht", _fackeln > 0 ? "stadt" : "nah").ToLowerInvariant() switch { "stadt" => Sicht.Stadt, "fern" => Sicht.Fern, _ => Sicht.Nah };
        if (int.TryParse(Option("feuerpaar", "2"), out var p)) _paar = Math.Clamp(p, 1, Paare.Length);
        else _paar = 2;
        _paar--;

        StoffeBauen();
        UmgebungBauen();
        BodenBauen();
        if (_fackeln > 0) FackelRasterBauen(); else PaareBauen();
        KameraBauen();
        OberflaecheBauen();
        TagNachtSetzen();
        SichtSetzen();
        ZeigenSetzen();
        GD.Print($"[feuer] start sicht={_sicht} art={_zeigen} tag={_tag} licht={_licht} paar={Paare[_paar].Name} fackeln={_fackeln} lichter={(_fackelLichter == int.MaxValue ? "alle" : _fackelLichter)} " +
                 $"renderer={RenderingServer.GetCurrentRenderingMethod()} fenster={GetViewport().GetVisibleRect().Size}");
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
        Feuer.Umgebung = _tag ? 1f : 0.06f;
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

    private Feuer FeuerSetzen(Node3D eltern, FeuerVorgabe vorgabe, FeuerTechnik technik, Vector3 lage, bool mitLicht = true)
    {
        var f = Feuer.Neu(vorgabe, technik, mitLicht && _licht);
        f.Position = lage;
        eltern.AddChild(f);
        _feuer.Add(f);
        return f;
    }

    private void Wand(Node3D eltern, float breite, float hoehe, float z)
    {
        // Eine niedrige Steinmauer hinter dem Feuer: Man sieht, wie das Licht auf sie fällt
        Kasten(eltern, new Vector3(breite, hoehe, 0.35f), new Vector3(0, 0, z), _stein);
    }

    private void PaareBauen()
    {
        _zPaar = new float[Paare.Length];
        var z = 0f;
        for (var i = 0; i < Paare.Length; i++)
        {
            z -= Paare[i].Abstand;
            _zPaar[i] = z;
            var paar = Paare[i];
            foreach (var technik in new[] { FeuerTechnik.Shader, FeuerTechnik.Partikel })
            {
                var seite = technik == FeuerTechnik.Shader ? -1f : 1f;
                var wurzel = new Node3D { Name = $"{paar.Name}_{(technik == FeuerTechnik.Shader ? "A" : "B")}", Position = new Vector3(seite * paar.Halb, 0, z) };
                AddChild(wurzel);
                (technik == FeuerTechnik.Shader ? _wurzelA : _wurzelB).Add(wurzel);
                PropBauen(wurzel, paar, technik);
                var text = new Label3D
                {
                    Text = $"{(technik == FeuerTechnik.Shader ? "A" : "B")}  {paar.Name}",
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    FixedSize = true,
                    NoDepthTest = true,
                    PixelSize = 0.0011f,
                    FontSize = 34,
                    OutlineSize = 8,
                    Modulate = technik == FeuerTechnik.Shader ? new Color(0.65f, 0.9f, 1f) : new Color(1f, 0.85f, 0.5f),
                    OutlineModulate = new Color(0, 0, 0, 0.9f),
                    Position = new Vector3(0, 0.04f, paar.Vorne + 0.25f),
                    RenderPriority = 10,
                };
                text.SetMeta("paar", i);
                wurzel.AddChild(text);
                _beschriftungen.Add(text);
            }
        }
    }

    private void PropBauen(Node3D w, Paar paar, FeuerTechnik technik)
    {
        switch (paar.Vorgabe)
        {
            case FeuerVorgabe.Kerze:
                Kasten(w, new Vector3(0.5f, 0.74f, 0.5f), Vector3.Zero, _stein);
                Zylinder(w, 0.03f, 0.04f, 0.03f, new Vector3(0, 0.74f, 0), _eisen);
                Zylinder(w, 0.011f, 0.012f, 0.11f, new Vector3(0, 0.77f, 0), _wachs);
                Wand(w, 3f, 2.4f, -0.55f);
                FeuerSetzen(w, FeuerVorgabe.Kerze, technik, new Vector3(0, 0.88f, 0));
                break;
            case FeuerVorgabe.Fackel:
                Zylinder(w, 0.03f, 0.035f, 1.55f, Vector3.Zero, _holz);
                Zylinder(w, 0.085f, 0.04f, 0.17f, new Vector3(0, 1.55f, 0), _eisen);
                Zylinder(w, 0.07f, 0.07f, 0.04f, new Vector3(0, 1.7f, 0), _kohle);
                Wand(w, 4f, 2.6f, -0.85f);
                FeuerSetzen(w, FeuerVorgabe.Fackel, technik, new Vector3(0, 1.72f, 0));
                break;
            case FeuerVorgabe.Laterne:
                Zylinder(w, 0.025f, 0.03f, 1.48f, Vector3.Zero, _eisen);
                Kasten(w, new Vector3(0.2f, 0.03f, 0.2f), new Vector3(0, 1.48f, 0), _eisen);
                Kasten(w, new Vector3(0.17f, 0.22f, 0.17f), new Vector3(0, 1.51f, 0), _glas);
                Kasten(w, new Vector3(0.22f, 0.04f, 0.22f), new Vector3(0, 1.73f, 0), _eisen);
                Zylinder(w, 0.003f, 0.012f, 0.05f, new Vector3(0, 1.51f, 0), _wachs);
                Wand(w, 4f, 2.6f, -0.85f);
                FeuerSetzen(w, FeuerVorgabe.Laterne, technik, new Vector3(0, 1.545f, 0));
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
                FeuerSetzen(w, FeuerVorgabe.Feuerkorb, technik, new Vector3(0, 1.17f, 0));
                break;
            case FeuerVorgabe.Esse:
                Kasten(w, new Vector3(1.3f, 0.85f, 1.0f), Vector3.Zero, _stein);
                Zylinder(w, 0.42f, 0.42f, 0.02f, new Vector3(0, 0.85f, 0), _dunkel);
                Kasten(w, new Vector3(1.3f, 1.7f, 0.3f), new Vector3(0, 0.85f, -0.48f), _stein);
                Kasten(w, new Vector3(0.5f, 0.12f, 0.25f), new Vector3(0.9f, 0.85f, 0.1f), _eisen);   // Amboss-Block daneben
                FeuerSetzen(w, FeuerVorgabe.Esse, technik, new Vector3(0, 0.88f, 0));
                break;
            case FeuerVorgabe.Kamin:
                HausBauen(w, 2.4f, 1.6f, 2.4f, false);
                Kasten(w, new Vector3(0.45f, 1.3f, 0.45f), new Vector3(0.7f, 1.6f, -0.3f), _stein);
                FeuerSetzen(w, FeuerVorgabe.Kamin, technik, new Vector3(0.7f, 3.0f, -0.3f), false);
                break;
            case FeuerVorgabe.Gebaeudebrand:
                HausBauen(w, 6.5f, 3.6f, 5.5f, true);
                Kasten(w, new Vector3(0.6f, 1.6f, 0.6f), new Vector3(2.0f, 3.6f, -1.2f), _stein);
                FeuerSetzen(w, FeuerVorgabe.Gebaeudebrand, technik, new Vector3(0, 4.6f, 0));
                FeuerSetzen(w, FeuerVorgabe.Fensterbrand, technik, new Vector3(-1.8f, 1.3f, 2.85f), false);
                FeuerSetzen(w, FeuerVorgabe.Fensterbrand, technik, new Vector3(1.8f, 1.3f, 2.85f), false);
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
        var technik = _zeigen == Zeigen.NurB ? FeuerTechnik.Partikel : FeuerTechnik.Shader;
        var spalten = 10;
        var reihen = (_fackeln + spalten - 1) / spalten;
        var wurzel = new Node3D { Name = "Fackeln" };
        AddChild(wurzel);
        _zPaar = new[] { 0f };
        for (var i = 0; i < _fackeln; i++)
        {
            var x = (i % spalten - (spalten - 1) * 0.5f) * 3.5f;
            var z = -(i / spalten - (reihen - 1) * 0.5f) * 3.5f;
            var stelle = new Node3D { Position = new Vector3(x, 0, z) };
            wurzel.AddChild(stelle);
            Zylinder(stelle, 0.03f, 0.035f, 1.55f, Vector3.Zero, _holz);
            Zylinder(stelle, 0.085f, 0.04f, 0.17f, new Vector3(0, 1.55f, 0), _eisen);
            if (_zeigen != Zeigen.Keins) FeuerSetzen(stelle, FeuerVorgabe.Fackel, technik, new Vector3(0, 1.72f, 0), i < _fackelLichter);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kamera
    // ═════════════════════════════════════════════════════════════════════════

    private void KameraBauen()
    {
        _kameraDrehung = new Node3D { Name = "Kameradrehung" };
        AddChild(_kameraDrehung);
        _kameraNeigung = new Node3D { Name = "Kameraneigung" };
        _kameraDrehung.AddChild(_kameraNeigung);
        _kamera = new Camera3D { Name = "Kamera", Current = true };
        _kameraNeigung.AddChild(_kamera);
    }

    private void SichtSetzen()
    {
        var paar = _fackeln > 0 ? null : Paare[_paar];
        var zMitte = _zPaar.Length == 0 ? 0f : (_fackeln > 0 ? 0f : _zPaar[_paar]);
        switch (_sicht)
        {
            case Sicht.Nah:
                // Wie die Raumansicht des Spiels: Perspektive, Fov 50, ganz nah am Feuer
                _kamera.Projection = Camera3D.ProjectionType.Perspective;
                _kamera.Fov = 50;
                _kamera.Near = 0.03f;
                _kamera.Far = 400f;
                _kameraDrehung.Position = new Vector3(0, paar?.NahHoehe ?? 1.5f, zMitte);
                _kamera.Position = new Vector3(0, 0, (paar?.NahAbstand ?? 12f) * _nahFaktor);
                _kameraNeigung.RotationDegrees = new Vector3(-_neigung, 0, 0);
                Feuer.AusblendenZuschlag = 0f;
                break;
            case Sicht.Stadt:
            case Sicht.Fern:
                // Wie die Stadtkamera: Orthokamera 40° von oben, 150 m Abstand, Größe 25 (nächster Zoom) bzw. 95 (weitester)
                _kamera.Projection = Camera3D.ProjectionType.Orthogonal;
                _kamera.Size = (_sicht == Sicht.Stadt ? 25f : 95f) * _zoomFaktor;
                _kamera.Near = 1f;
                _kamera.Far = 400f;
                var mitte = _sicht == Sicht.Fern && _fackeln == 0 ? (_zPaar.Length > 0 ? _zPaar[^1] * 0.5f : 0f) : zMitte;
                _kameraDrehung.Position = new Vector3(0, 0, mitte);
                _kamera.Position = new Vector3(0, 0, 150f);
                _kameraNeigung.RotationDegrees = new Vector3(-40f, 0, 0);
                Feuer.AusblendenZuschlag = 150f;
                break;
        }
        _kameraDrehung.RotationDegrees = new Vector3(0, _gierung, 0);
        foreach (var f in _feuer) f.AusblendenNeu();
        PaareSichtbar();
        BeschriftungSetzen();
    }

    private void ZeigenSetzen()
    {
        var a = _zeigen is Zeigen.Beide or Zeigen.NurA;
        var b = _zeigen is Zeigen.Beide or Zeigen.NurB;
        foreach (var f in _feuer)
        {
            var istA = f.Technik == FeuerTechnik.Shader;
            f.Visible = _fackeln > 0 ? _zeigen != Zeigen.Keins : (istA ? a : b);
            f.ProcessMode = f.Visible ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        }
        BeschriftungSetzen();
    }

    /// <summary>In der Nähe nur das gewählte Paar (die übrigen stehen hinter der Mauer, sind aber sonst zu sehen und kosten Zeit).</summary>
    private void PaareSichtbar()
    {
        for (var i = 0; i < _wurzelA.Count; i++) _wurzelA[i].Visible = _wurzelB[i].Visible = _sicht != Sicht.Nah || i == _paar;
    }

    /// <summary>Nah und Stadt: nur die Namen des gewählten Paars (sonst scheinen die der anderen durch die Mauern); fern nur die der großen Paare (Kamin, Gebäudebrand), die kleinen liegen zu dicht.</summary>
    private void BeschriftungSetzen()
    {
        foreach (var l in _beschriftungen) l.Visible = _beschriftung && (_sicht == Sicht.Fern ? (int)l.GetMeta("paar") >= 5 : (int)l.GetMeta("paar") == _paar);
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
        _hilfe.Text = "1–7 / Pfeile: Paar   V: Sicht (nah, Stadt, fern)   N: Nacht/Tag   T: A / B / beide / keins   L: Lichter   H: Beschriftung\n" +
                      "Rechte Maustaste ziehen: drehen   Mausrad: nähern   Esc: Ende";
        AnzeigeAktualisieren(0);
    }

    private void AnzeigeAktualisieren(double fps)
    {
        var kopf = _fackeln > 0
            ? $"Feuertest – Messung: {_fackeln} Fackeln, Technik {(_zeigen == Zeigen.NurB ? "B (Partikel)" : _zeigen == Zeigen.Keins ? "keine (nur Pfähle)" : "A (Shader)")}"
            : "Feuertest – links A: Shader-Flamme   rechts B: Partikel-Flamme";
        var paar = _fackeln > 0 ? "" : $"   Paar {_paar + 1}: {Paare[_paar].Name}";
        _anzeige.Text = $"{kopf}\nSicht: {_sicht}{paar}   {(_tag ? "Tag" : "Nacht")}   Lichter: {(_licht ? "an" : "aus")}   Zeigen: {_zeigen}   Bilder/s: {fps:F0}";
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
                    case Key.V: _sicht = (Sicht)(((int)_sicht + 1) % 3); _zoomFaktor = 1f; SichtSetzen(); break;
                    case Key.N: _tag = !_tag; TagNachtSetzen(); break;
                    case Key.T: _zeigen = (Zeigen)(((int)_zeigen + 1) % 4); ZeigenSetzen(); break;
                    case Key.L:
                        _licht = !_licht;
                        foreach (var f in _feuer) f.LichtSetzen(_licht);
                        break;
                    case Key.H: _beschriftung = !_beschriftung; ZeigenSetzen(); break;
                    case Key.Escape: GetTree().Quit(); break;
                }
                break;
            case InputEventMouseMotion { ButtonMask: MouseButtonMask.Right } bewegung:
                _gierung -= bewegung.Relative.X * 0.3f;
                _neigung = Mathf.Clamp(_neigung + bewegung.Relative.Y * 0.2f, -5f, 80f);
                SichtSetzen();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                Naehern(0.88f);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                Naehern(1.14f);
                break;
        }
    }

    private void Naehern(float faktor)
    {
        if (_sicht == Sicht.Nah) _nahFaktor = Mathf.Clamp(_nahFaktor * faktor, 0.2f, 5f);
        else _zoomFaktor = Mathf.Clamp(_zoomFaktor * faktor, 0.2f, 3f);
        SichtSetzen();
    }

    private void PaarWaehlen(int i)
    {
        if (_fackeln > 0) return;
        _paar = Math.Clamp(i, 0, Paare.Length - 1);
        _nahFaktor = 1f;
        SichtSetzen();
        AnzeigeAktualisieren(0);
    }

    public override void _Process(double delta)
    {
        // Messung: jede Sekunde die mittlere Bildrate und Bildzeit ins Protokoll
        _messZeit += delta;
        _messBilder++;
        if (_messZeit < 1.0) return;
        var fps = _messBilder / _messZeit;
        var ms = _messZeit / _messBilder * 1000.0;
        var r = RenderingServer.GetRenderingInfo;
        GD.Print($"[feuer] fps={fps:F1} ms={ms:F2} sicht={_sicht} art={_zeigen} tag={_tag} licht={_licht} paar={(_fackeln > 0 ? "-" : Paare[_paar].Name)} " +
                 $"feuer={_feuer.Count(f => f.Visible)} aufrufe={r(RenderingServer.RenderingInfo.TotalDrawCallsInFrame)} objekte={r(RenderingServer.RenderingInfo.TotalObjectsInFrame)} " +
                 $"dreiecke={r(RenderingServer.RenderingInfo.TotalPrimitivesInFrame)}");
        AnzeigeAktualisieren(fps);
        _messZeit = 0;
        _messBilder = 0;
    }
}
