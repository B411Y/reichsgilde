using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Testszene „Pixel-Look“ (Versuch, Nutzer 03.11.2026), Runde 3: derselbe Ausschnitt (Häuser, Figuren, Gras, Fels, Teich, Feuer) mit Originalfarben,
/// Originaltexturen und Original-Wasser, aber (1) gleich großen Texeln, an die Welt gebunden (Nearest), (2) Licht in Stufen mit harter Schattenkante
/// (aus der Beleuchtung, nicht aus der Endhelligkeit), (3) schwachem Kantenlicht und (4) einem Regler „Düsternis“ (Standard 12 %).
/// Alles live einstellbar über die Reglertafel (Tab klappt sie ein und aus); „Werte speichern“ schreibt nach <c>user://pixellook.json</c>.
/// Startet ohne Optionen (auch mit F6 im Godot-Editor) in der Spielkamera (orthografisch, 40 Grad schräg) mit der Voreinstellung „Pixel“.
/// Tasten: C Spielkamera, F Flugkamera, V Spielkamera weit, Mausrad Zoom (Spielkamera), R Pixel, 0 aus, Tab Tafel, Esc Ende.
/// Startoptionen (alle freiwillig): <c>--pixelmodus=aus|pixel</c>, <c>--pixelsicht=spiel|spielnah|spielweit|flug|nah</c>, <c>--pixeltexel=&lt;Bildpunkte&gt;</c>, <c>--pixelzoom=&lt;m&gt;</c> (Standard 40), <c>--pixelkreis=1</c>,
/// <c>--pixelmessung=1</c> (aus und an je 6 s messen, Ergebnis im Log, dann beenden), <c>--pixelende=&lt;s&gt;</c>, <c>--pixeltafel=0</c> (Tafel zu).
/// Weg A (Regler „Pixelgröße“ 2–6, Option <c>--pixelgroesse=N</c>, <c>--pixelscroll=&lt;m/s&gt;</c>): die Spielkamera-Welt wird in einem SubViewport mit 1/N der Auflösung gerendert und hart hochgezogen, Kamera-Snapping mit Versatz; Tafel und Schrift bleiben scharf.
/// Kein Teil des Spiels; der Shader liegt in <c>assets/shader/pixel_farbe.gdshader</c>.
/// </summary>
public partial class Pixeltest : Node3D
{
    public const string Szene = "res://scenes/Pixeltest.tscn";

    public static bool Angefordert =>
        OS.GetCmdlineUserArgs().Any(a => a == "--pixeltest" || (a.StartsWith("--pixeltest=") && a != "--pixeltest=0"));

    /// <summary>Alle einstellbaren Werte (werden gespeichert).</summary>
    private sealed class Werte
    {
        public int PixelGroesse = 1;   // Weg A: Welt in 1/N der Auflösung rendern (1 = aus)
        public bool An = true, Texel = true, Stufen = true, Kanten = true, Halme = true, Zweier;
        public int Lichtstufen = 3;
        public float TexelPx = 3f, Duester = 0.12f, KanteHell = 0.08f, StufenStaerke = 1f, LichtDunkel = 0.25f, LichtHell = 0.95f, Ferne = 1.25f;
    }

    private Werte _w = new();
    private readonly Dictionary<string, string> _optionen = new();
    private bool _nebel, _kreis, _spiel;
    private float _kreisWinkel, _spielZoom = 60f;
    private FreieKamera _kamera = null!;
    private Camera3D _spielKamera = null!;
    private WorldEnvironment _umgebung = null!;
    private DirectionalLight3D _sonne = null!;
    private MeshInstance3D _flaeche = null!, _wasser = null!;
    private MultiMeshInstance3D _gras = null!;
    private ShaderMaterial _farbStoff = null!;
    private SubViewport _lichtBild = null!;
    private Camera3D _lichtKamera = null!;
    // Weg A: Welt in einem SubViewport mit 1/N der Auflösung, hart (Nearest) hochgezogen; die Oberfläche liegt darüber in voller Auflösung
    private SubViewport _welt = null!;
    private TextureRect _weltAnzeige = null!;
    private CanvasLayer _weltEbene = null!;
    private Camera3D _aktiv = null!;
    private bool _weltA;
    private Basis _spielBasis; private Vector3 _spielOrt; private float _scroll, _scrollTempo;
    private Label _anzeige = null!, _pfad = null!;
    private PanelContainer _tafel = null!;
    private readonly Dictionary<string, Action<Werte>> _abgleich = new();
    private readonly List<Action> _reglerNeu = new();
    private readonly List<MeshInstance3D> _netze = new();
    private double _ende, _zeit;
    private double _messZeit; private int _messSchritt = -1; private readonly List<double> _gpu = new(), _cpu = new();
    private readonly string[] _messNamen = { "aus", "Runde 3 (volle Auflösung)", "Weg A Pixelgröße 2", "Weg A Pixelgröße 3", "Weg A Pixelgröße 4" };

    public override void _Ready()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var t = arg.TrimStart('-').Split('=', 2);
            if (t.Length == 2) _optionen[t[0]] = t[1];
        }
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        float.TryParse(_optionen.GetValueOrDefault("pixelscroll", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out _scrollTempo);
        double.TryParse(_optionen.GetValueOrDefault("pixelende", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out _ende);
        _kreis = _optionen.GetValueOrDefault("pixelkreis") == "1";

        UmgebungBauen();
        WeltBauen();
        _kamera = FreieKamera.Einhaengen(this, new Vector3(30, 14, 34), new Vector3(0, 2, 0));
        _kamera.Bodenhoehe = Hoehe;
        _kamera.MindestHoehe = 0.4f;
        _spielKamera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = _spielZoom, Near = 1f, Far = 600f, Name = "Spielkamera" };
        AddChild(_spielKamera);
        WeltBauen_A();
        NachbearbeitungBauen();
        if (int.TryParse(_optionen.GetValueOrDefault("pixelgroesse", ""), out var pg)) _w.PixelGroesse = Mathf.Clamp(pg, 1, 8);
        if (_optionen.GetValueOrDefault("pixelmodus", "pixel") == "aus") _w = Aus();
        if (float.TryParse(_optionen.GetValueOrDefault("pixeltexel", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var tpx)) _w.TexelPx = tpx;
        OberflaecheBauen();
        switch (_optionen.GetValueOrDefault("pixelsicht", "spiel"))
        {
            case "flug": Flug(false); break;
            case "nah": Flug(true); break;
            case "spielweit": SpielSicht(140f); break;
            case "spielnah": SpielSicht(20f); break;
            default: SpielSicht(float.TryParse(_optionen.GetValueOrDefault("pixelzoom", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var zm) ? zm : 40f); break;
        }
        if (_optionen.GetValueOrDefault("pixeltafel") == "0") _tafel.Visible = false;
        if (_optionen.GetValueOrDefault("pixelmessung") == "1") _messSchritt = 0;
        GD.Print($"[pixeltest] renderer={RenderingServer.GetCurrentRenderingDriverName()} netze={_netze.Count}");
    }

    private static Werte Aus() => new() { An = false };
    private static Werte Pixel() => new();

    // ═════════════════════════════════════════════════════════════════════════
    //  Welt
    // ═════════════════════════════════════════════════════════════════════════

    private static float Hoehe(float x, float z)
    {
        var y = 0.5f * Mathf.Sin(x * 0.09f) * Mathf.Cos(z * 0.07f) + 0.15f * Mathf.Sin(x * 0.4f + z * 0.3f);
        var d = new Vector2(x + 16f, z - 14f).Length();      // Teich
        if (d < 10f) y -= (1f - d / 10f) * 1.6f + 0.2f;
        return y;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Welt
    // ═════════════════════════════════════════════════════════════════════════

    private void UmgebungBauen()
    {
        var himmel = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.36f, 0.52f, 0.78f), SkyHorizonColor = new Color(0.68f, 0.74f, 0.82f),
            GroundHorizonColor = new Color(0.50f, 0.56f, 0.42f), GroundBottomColor = new Color(0.40f, 0.46f, 0.30f),
        };
        _umgebung = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky, Sky = new Sky { SkyMaterial = himmel },
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.75f, 0.75f, 0.8f), AmbientLightEnergy = 0.5f,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
                TonemapMode = Godot.Environment.ToneMapper.Agx,
                SsaoEnabled = true, SsaoRadius = 1.2f, SsaoIntensity = 1.5f,
                FogEnabled = false, FogLightColor = new Color(0.72f, 0.76f, 0.82f), FogDensity = 0.012f, FogSkyAffect = 0.6f,
            },
        };
        AddChild(_umgebung);
        _sonne = new DirectionalLight3D
        {
            ShadowEnabled = true, DirectionalShadowMaxDistance = 140f, RotationDegrees = new Vector3(-42, 140, 0),
            LightColor = new Color(1f, 0.96f, 0.88f), LightEnergy = 1.25f,
        };
        AddChild(_sonne);
    }

    private void WeltBauen()
    {
        var boden = new MeshInstance3D { Mesh = BodenNetz(), MaterialOverride = BodenStoff(), Name = "Boden" };
        AddChild(boden); _netze.Add(boden);

        // Teich
        var wasser = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(22, 22), SubdivideWidth = 8, SubdivideDepth = 8 },
            Position = new Vector3(-16, -0.45f, 14), Name = "Wasser",
        };
        if (ResourceLoader.Exists("res://assets/shader/wasser_mittel.gdshader"))
            wasser.MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/wasser_mittel.gdshader") };
        else
            wasser.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.35f, 0.45f, 0.8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        AddChild(wasser);
        _wasser = wasser;

        // Häuser (Maße der Modelle werden ins Log geschrieben)
        Haus("gebaeude/fachwerkhaus", new Vector3(-4, 0, -6), 20);
        Haus("gebaeude/steinhaus", new Vector3(8, 0, -8), -10);
        Haus("gebaeude/buergerhaus", new Vector3(20, 0, -2), -80);
        Haus("gebaeude/rathaus", new Vector3(5, 0, 6), 180);
        Haus("gebaeude/tischler", new Vector3(-12, 0, -4), 40);

        // Figuren
        var farben = new[] { (new Color(0.62f, 0.17f, 0.15f), new Color(0.27f, 0.2f, 0.14f)), (new Color(0.24f, 0.36f, 0.52f), new Color(0.3f, 0.25f, 0.2f)),
                             (new Color(0.72f, 0.55f, 0.25f), new Color(0.2f, 0.17f, 0.14f)), (new Color(0.36f, 0.48f, 0.24f), new Color(0.35f, 0.28f, 0.2f)) };
        var volk = new[] { "mensch", "zwerg", "halbling", "elb" };
        for (var i = 0; i < 4; i++)
        {
            var f = Modell($"figuren/figur_{volk[i]}");
            if (f is null) continue;
            var stoff = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/figur.gdshader") };
            stoff.SetShaderParameter("gewand", farben[i].Item1);
            stoff.SetShaderParameter("beinkleid", farben[i].Item2);
            foreach (var m in Netze(f)) m.MaterialOverride = stoff;
            var x = 12f + i * 1.6f; var z = 4f + (i % 2) * 1.4f;
            f.Position = new Vector3(x, Hoehe(x, z), z);
            f.RotationDegrees = new Vector3(0, 200 + i * 25, 0);
            AddChild(f);
        }

        // Felsen
        var fels = Gesteinsarten.Material(Gesteinsarten.Nach("grauwacke"), true);
        for (var i = 0; i < 4; i++)
        {
            var saat = 7 + i * 5;
            var x = -26f + i * 3.2f; var z = -4f + (i % 2) * 3f;
            var groesse = new Vector3(2.4f, 1.7f, 2.0f) * (1.2f - i * 0.15f);
            var fm = new MeshInstance3D { Mesh = FelsNetz(groesse, saat), MaterialOverride = fels, Name = "Fels" };
            fm.Position = new Vector3(x, Hoehe(x, z) + groesse.Y * 0.3f, z);
            fm.RotationDegrees = new Vector3(0, saat * 17, 0);
            AddChild(fm); _netze.Add(fm);
        }

        Gras();

        // Feuer: Korb vor dem Rathaus, Fackel am Haus
        foreach (var (v, p) in new[] { (FeuerVorgabe.Feuerkorb, new Vector3(9, 0, 12)), (FeuerVorgabe.Fackel, new Vector3(2, 1.6f, 10)) })
        {
            var f = Feuer.Neu(v, false);
            f.Position = new Vector3(p.X, p.Y + Hoehe(p.X, p.Z), p.Z);
            AddChild(f);
        }
        var korb = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.3f, Height = 0.6f }, Position = new Vector3(9, Hoehe(9, 12) + 0.3f, 12) };
        korb.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.16f, 0.14f), Roughness = 0.8f };
        AddChild(korb); _netze.Add(korb);
        var licht = new OmniLight3D { Position = new Vector3(9, Hoehe(9, 12) + 1.4f, 12), LightColor = new Color(1f, 0.55f, 0.25f), LightEnergy = 1.6f, OmniRange = 9f };
        AddChild(licht);
    }

    private Node3D? Modell(string datei)
    {
        var pfad = $"res://assets/modelle/{datei}.glb";
        if (!ResourceLoader.Exists(pfad)) { GD.PushError($"Modell fehlt: {pfad}"); return null; }
        var n = GD.Load<PackedScene>(pfad).Instantiate<Node3D>();
        foreach (var m in Netze(n)) _netze.Add(m);
        return n;
    }

    private static IEnumerable<MeshInstance3D> Netze(Node wurzel)
    {
        if (wurzel is MeshInstance3D mi) yield return mi;
        foreach (var k in wurzel.GetChildren()) foreach (var m in Netze(k)) yield return m;
    }

    private void Haus(string datei, Vector3 ort, float drehung)
    {
        var h = Modell(datei);
        if (h is null) return;
        h.Position = new Vector3(ort.X, Hoehe(ort.X, ort.Z), ort.Z);
        h.RotationDegrees = new Vector3(0, drehung, 0);
        AddChild(h);
        var box = Netze(h).Select(m => m.GetAabb()).Aggregate((a, b) => a.Merge(b));
        GD.Print($"[pixeltest] {datei} Aabb {box.Size}");
    }

    private static Mesh BodenNetz()
    {
        const float h = 1f; const int nx = 100, nz = 80;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var gras = new Color(0.33f, 0.40f, 0.20f); var trocken = new Color(0.46f, 0.44f, 0.27f); var erde = new Color(0.40f, 0.33f, 0.24f);
        for (var j = 0; j <= nz; j++)
            for (var i = 0; i <= nx; i++)
            {
                float x = -50 + i * h, z = -40 + j * h;
                var y = Hoehe(x, z);
                var fl = 0.5f + 0.5f * Mathf.Sin(x * 0.37f + Mathf.Sin(z * 0.23f) * 2f) * Mathf.Cos(z * 0.29f - x * 0.07f);
                var c = gras.Lerp(trocken, fl * 0.6f);
                if (y < -0.1f) c = c.Lerp(erde, Mathf.Clamp(-y, 0, 1));
                // Weg
                var weg = Mathf.Abs(z - (6f + 0.12f * x)) < 1.6f ? 1f : 0f;
                c = c.Lerp(erde, weg * 0.9f);
                st.SetColor(c); st.SetUV(new Vector2(x, z) / 4f); st.AddVertex(new Vector3(x, y, z));
            }
        for (var j = 0; j < nz; j++)
            for (var i = 0; i < nx; i++)
            {
                var a = j * (nx + 1) + i; var b = a + 1; var c2 = a + nx + 1; var d = c2 + 1;
                st.AddIndex(a); st.AddIndex(b); st.AddIndex(c2); st.AddIndex(b); st.AddIndex(d); st.AddIndex(c2);
            }
        st.GenerateNormals();
        return st.Commit();
    }

    private static Material BodenStoff() => new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 1f };

    private static Mesh FelsNetz(Vector3 groesse, int saat)
    {
        var grund = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 24, Rings = 12 };
        var arrays = grund.SurfaceGetArrays(0);
        var ecken = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var indizes = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        var rauschen = new FastNoiseLite { Seed = saat, Frequency = 1.8f, FractalOctaves = 3 };
        for (var i = 0; i < ecken.Length; i++)
        {
            var v = ecken[i];
            v += v.Normalized() * rauschen.GetNoise3Dv(v * 2f) * 0.22f;
            v.Y = Mathf.Max(v.Y, -0.25f);   // unten abgeflacht
            ecken[i] = v * groesse;
        }
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var idx in indizes) st.AddVertex(ecken[idx]);
        st.Index(); st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>Einfaches Gras: Büschel aus drei Dreiecken, ein MultiMesh. Platzhalter nur für diesen Test (das Spiel hat sein eigenes Gras).</summary>
    private void Gras()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        // Senkrechte schmale Halme (zwei gekreuzte Dreiecke je Büschel), Normalen nach oben wie beim Boden; Farben wie die Wiese des Bodens
        for (var k = 0; k < 2; k++)
        {
            var w = k * Mathf.Pi / 2f; var d = new Vector3(Mathf.Cos(w), 0, Mathf.Sin(w)) * 0.035f;
            st.SetNormal(Vector3.Up); st.SetColor(new Color(0.24f, 0.32f, 0.14f)); st.AddVertex(-d);
            st.SetNormal(Vector3.Up); st.SetColor(new Color(0.24f, 0.32f, 0.14f)); st.AddVertex(d);
            st.SetNormal(Vector3.Up); st.SetColor(new Color(0.40f, 0.50f, 0.24f)); st.AddVertex(new Vector3(0, 0.6f, 0));
        }
        var netz = st.Commit();
        var zufall = new RandomNumberGenerator { Seed = 11 };
        const int anzahl = 16000;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = false, Mesh = netz, InstanceCount = anzahl };
        for (var i = 0; i < anzahl; i++)
        {
            var x = zufall.RandfRange(-30, 38); var z = zufall.RandfRange(-14, 30);
            var y = Hoehe(x, z);
            if (y < -0.15f) { mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.Zero), Vector3.Zero)); continue; }
            var s = zufall.RandfRange(0.7f, 1.5f);
            var b = new Basis(Vector3.Up, zufall.RandfRange(0, Mathf.Tau)).Scaled(new Vector3(s, s, s));
            mm.SetInstanceTransform(i, new Transform3D(b, new Vector3(x, y, z)));
        }
        _gras = new MultiMeshInstance3D
        {
            Multimesh = mm, Name = "Gras", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 1f },
        };
        AddChild(_gras);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Nachbearbeitung
    // ═════════════════════════════════════════════════════════════════════════

    private void NachbearbeitungBauen()
    {
        // Ein Vollbild-Viereck vor der Kamera; die kleinste Zeichenreihenfolge, damit Wasser, Feuer und Nebel danach darüber liegen
        _farbStoff = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/pixel_farbe.gdshader"), RenderPriority = -128 };
        _flaeche = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2, 2), FlipFaces = true }, MaterialOverride = _farbStoff,
            ExtraCullMargin = 16384f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _flaeche.Layers = 1u << 1;   // Ebene 2: nur die Hauptkamera sieht das Viereck, nicht die Beleuchtungskamera
        _kamera.AddChild(_flaeche);

        // Zweiter Durchgang: nur die Beleuchtung (weiße Oberflächen), gleiche Welt, gleiche Kamera
        _lichtBild = new SubViewport
        {
            Size = new Vector2I(1600, 900), RenderTargetUpdateMode = SubViewport.UpdateMode.Always, DebugDraw = Viewport.DebugDrawEnum.Lighting,
            Msaa3D = Viewport.Msaa.Disabled, TransparentBg = false, Name = "Beleuchtung",
        };
        _lichtKamera = new Camera3D { CullMask = 1u, Near = 1f, Far = 600f };
        _lichtBild.AddChild(_lichtKamera);
        AddChild(_lichtBild);
        _farbStoff.SetShaderParameter("licht", _lichtBild.GetTexture());
        RenderingServer.ViewportSetMeasureRenderTime(_lichtBild.GetViewportRid(), true);
    }

    private void WeltBauen_A()
    {
        _welt = new SubViewport
        {
            Size = new Vector2I(800, 450), RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Msaa3D = Viewport.Msaa.Disabled,
            Name = "WeltNiedrig", TransparentBg = false,
        };
        AddChild(_welt);
        _weltEbene = new CanvasLayer { Layer = 0, Visible = false };
        AddChild(_weltEbene);
        _weltAnzeige = new TextureRect
        {
            Texture = _welt.GetTexture(), TextureFilter = CanvasItem.TextureFilterEnum.Nearest, StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _weltAnzeige.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _weltEbene.AddChild(_weltAnzeige);
        RenderingServer.ViewportSetMeasureRenderTime(_welt.GetViewportRid(), true);
    }

    /// <summary>Schaltet Weg A ein oder aus: die Spielkamera, das Nachbearbeitungs-Viereck und das Beleuchtungsbild ziehen in den Welt-SubViewport um (oder zurück).</summary>
    private void WeltUmschalten(bool a)
    {
        if (a == _weltA) return;
        _weltA = a;
        _lichtBild.Reparent(a ? _welt : this, false);
        _spielKamera.Reparent(a ? _welt : this, true);
        _welt.RenderTargetUpdateMode = a ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        _weltEbene.Visible = a;
        if (a) _kamera.HilfeSichtbar = false;
        GetViewport().Disable3D = a;
        _aktiv.MakeCurrent();
        _flaeche.Reparent(_aktiv, false);
        if (!a) { _spielKamera.Size = _spielZoom; _spielKamera.Position = _spielOrt; _flaeche.Visible = _w.An; }
    }

    /// <summary>Kamera-Snapping (Weg A): die Spielkamera rastet in Kamerarichtung auf das Raster der Weltpixel; der Rest wird beim Hochziehen als Versatz des Bildes ausgeglichen.</summary>
    private void SpielKameraSetzen()
    {
        if (!_spiel) return;
        var fenster = GetViewport().GetVisibleRect().Size;
        var ort = _spielOrt + _spielBasis * new Vector3(_scroll, 0, 0);
        if (!_weltA) { _spielKamera.Size = _spielZoom; _spielKamera.GlobalPosition = ort; return; }
        var n = _w.PixelGroesse;
        var ps = _spielZoom * n / fenster.Y;                         // Meter je Weltpixel
        var gross = new Vector2I(Mathf.CeilToInt(fenster.X / n) + 2, Mathf.CeilToInt(fenster.Y / n) + 2);
        if (_welt.Size != gross) _welt.Size = gross;
        var lokal = _spielBasis.Transposed() * ort;
        var einrast = new Vector2(Mathf.Round(lokal.X / ps), Mathf.Round(lokal.Y / ps)) * ps;
        var rest = new Vector2(lokal.X, lokal.Y) - einrast;           // Meter
        _spielKamera.GlobalPosition = _spielBasis * new Vector3(einrast.X, einrast.Y, lokal.Z);
        _spielKamera.Size = ps * gross.Y;
        _weltAnzeige.Size = new Vector2(gross.X, gross.Y) * n;
        _weltAnzeige.Position = (fenster - _weltAnzeige.Size) / 2f + new Vector2(-rest.X, rest.Y) / ps * n;
        _farbStoff.SetShaderParameter("ortho_groesse", _spielKamera.Size);
    }

    private void LichtKameraAbgleichen()
    {
        var k = _aktiv;
        if (k is null || _lichtBild is null) return;
        var flaeche = _weltA ? new Vector2(_welt.Size.X, _welt.Size.Y) : GetViewport().GetVisibleRect().Size;
        var groesse = new Vector2I(Mathf.Max((int)flaeche.X, 16), Mathf.Max((int)flaeche.Y, 16));
        if (_lichtBild.Size != groesse) _lichtBild.Size = groesse;
        _lichtKamera.GlobalTransform = k.GlobalTransform;
        _lichtKamera.Projection = k.Projection;
        _lichtKamera.Size = k.Size; _lichtKamera.Fov = k.Fov; _lichtKamera.Near = k.Near; _lichtKamera.Far = k.Far;
        _lichtKamera.KeepAspect = k.KeepAspect;
    }

    private void Anwenden()
    {
        var w = _w;
        _flaeche.Visible = w.An;
        WeltUmschalten(_spiel && w.An && w.PixelGroesse >= 2);
        _farbStoff.SetShaderParameter("texel_an", w.Texel && !_weltA);
        _farbStoff.SetShaderParameter("stufen_an", w.Stufen);
        _farbStoff.SetShaderParameter("stufen", w.Lichtstufen);
        _farbStoff.SetShaderParameter("stufen_staerke", w.StufenStaerke);
        _farbStoff.SetShaderParameter("licht_dunkel", w.LichtDunkel);
        _farbStoff.SetShaderParameter("licht_hell", w.LichtHell);
        _farbStoff.SetShaderParameter("duester", w.Duester);
        _farbStoff.SetShaderParameter("texel_je_meter", 16f);
        _farbStoff.SetShaderParameter("texel_auto", _spiel);
        _farbStoff.SetShaderParameter("zweierstufen", w.Zweier);
        KameraWerte();
        _farbStoff.SetShaderParameter("texel_ziel_px", w.TexelPx);
        _farbStoff.SetShaderParameter("blende_von", w.Ferne);
        _farbStoff.SetShaderParameter("zeige_licht", _optionen.GetValueOrDefault("pixelpruefbild") == "licht");
        _farbStoff.SetShaderParameter("kante_an", w.Kanten);
        _farbStoff.SetShaderParameter("kante_hell", w.KanteHell);
        _farbStoff.SetShaderParameter("sonne_richtung", -_sonne.GlobalBasis.Z);
        _gras.Visible = w.Halme;
        _sonne.ShadowBlur = w.An && w.Stufen ? 0f : 1f;
        _umgebung.Environment.FogEnabled = _nebel;
        AnzeigeAktualisieren();
    }

    private void KameraWerte()
    {
        _farbStoff.SetShaderParameter("ortho_groesse", _spiel ? (_weltA ? _spielKamera.Size : _spielZoom) : 0f);
        _farbStoff.SetShaderParameter("fov_faktor", 2f * Mathf.Tan(Mathf.DegToRad(_kamera.Fov) / 2f));
    }

    private void Aktiv(Camera3D k)
    {
        _aktiv = k;
        k.MakeCurrent();
        _flaeche.Reparent(k, false);
    }

    /// <summary>Spielkamera: orthografisch, 40 Grad schräg von oben, 150 m Abstand – wie im Spiel (Stadtansicht.KameraAufbauen). Zoom = sichtbare Höhe in Metern (Spiel: 25–140, Start 95).</summary>
    private void SpielSicht(float zoom)
    {
        _spiel = true; _spielZoom = Mathf.Clamp(zoom, 15f, 140f);
        var basis = Basis.FromEuler(new Vector3(Mathf.DegToRad(-40f), Mathf.DegToRad(30f), 0));
        _spielBasis = basis; _spielOrt = new Vector3(2, 0, 2) + basis * new Vector3(0, 0, 150f); _scroll = 0;
        _spielKamera.GlobalTransform = new Transform3D(basis, _spielOrt);
        _spielKamera.Size = _spielZoom;
        Aktiv(_spielKamera);
        Anwenden();
    }

    private void Flug(bool nah)
    {
        _spiel = false; Aktiv(_kamera);
        WeltUmschalten(false);
        if (nah) _kamera.Blicken(new Vector3(14, 2.6f, 11), new Vector3(7, 1.6f, 4));
        else _kamera.Blicken(new Vector3(30, 14, 34), new Vector3(0, 2, 0));
        Anwenden();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Reglertafel, Tasten, Messung
    // ═════════════════════════════════════════════════════════════════════════

    private void OberflaecheBauen()
    {
        var ebene = new CanvasLayer { Layer = 5 };
        AddChild(ebene);
        _anzeige = new Label { Position = new Vector2(14, 10) };
        _anzeige.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.85f));
        _anzeige.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
        _anzeige.AddThemeConstantOverride("outline_size", 6);
        ebene.AddChild(_anzeige);

        _tafel = new PanelContainer { Position = new Vector2(0, 0) };
        _tafel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _tafel.OffsetLeft = -330; _tafel.OffsetRight = -8; _tafel.OffsetTop = 8;
        _tafel.GrowHorizontal = Control.GrowDirection.Begin;
        ebene.AddChild(_tafel);
        var spalte = new VBoxContainer();
        _tafel.AddChild(spalte);
        spalte.AddChild(new Label { Text = "Pixel-Look einstellen (Tab: ein/aus)" });

        Schalter(spalte, "Pixel-Look an", w => w.An, (w, v) => w.An = v);
        Regler(spalte, "Pixelgröße (Weg A: Welt niedrig rendern, 1 = aus)", 1, 6, 1, w => w.PixelGroesse, (w, v) => w.PixelGroesse = (int)v);
        Schalter(spalte, "Texel (Welt-Raster, Nearest)", w => w.Texel, (w, v) => w.Texel = v);
        Regler(spalte, "Texelgröße (Bildpunkte, Spielkamera)", 1, 8, 0.5f, w => w.TexelPx, (w, v) => w.TexelPx = v);
        Schalter(spalte, "Texelgröße in Zweierstufen (kein Wandern beim Zoomen)", w => w.Zweier, (w, v) => w.Zweier = v);
        Regler(spalte, "Rasterung ab Texelgröße (Bildpunkte)", 0.5f, 4f, 0.05f, w => w.Ferne, (w, v) => w.Ferne = v);
        Schalter(spalte, "Stufenlicht (aus der Beleuchtung)", w => w.Stufen, (w, v) => w.Stufen = v);
        Regler(spalte, "Lichtstufen", 2, 5, 1, w => w.Lichtstufen, (w, v) => w.Lichtstufen = (int)v);
        Regler(spalte, "Stufen-Stärke", 0, 1, 0.05f, w => w.StufenStaerke, (w, v) => w.StufenStaerke = v);
        Regler(spalte, "Licht unterste Stufe", 0.05f, 0.8f, 0.01f, w => w.LichtDunkel, (w, v) => w.LichtDunkel = v);
        Regler(spalte, "Licht oberste Stufe", 0.3f, 1.2f, 0.01f, w => w.LichtHell, (w, v) => w.LichtHell = v);
        Schalter(spalte, "Kantenlicht", w => w.Kanten, (w, v) => w.Kanten = v);
        Regler(spalte, "Kantenlicht-Stärke", 0, 0.3f, 0.01f, w => w.KanteHell, (w, v) => w.KanteHell = v);
        Regler(spalte, "Düsternis", 0, 0.4f, 0.01f, w => w.Duester, (w, v) => w.Duester = v);
        Schalter(spalte, "Gras-Halme", w => w.Halme, (w, v) => w.Halme = v);

        var reihe = new HBoxContainer(); spalte.AddChild(reihe);
        Knopf(reihe, "Pixel", () => { _w = Pixel(); Abgleichen(); });
        Knopf(reihe, "Aus", () => { _w = Aus(); Abgleichen(); });
        Knopf(reihe, "Spielkamera", () => SpielSicht(_spielZoom));
        Knopf(reihe, "Flugkamera", () => Flug(false));
        var reihe2 = new HBoxContainer(); spalte.AddChild(reihe2);
        Knopf(reihe2, "Werte speichern", Speichern);
        Knopf(reihe2, "Werte kopieren", () => { DisplayServer.ClipboardSet(Json()); _pfad.Text = "In die Zwischenablage kopiert."; });
        _pfad = new Label { Text = "Speicherort: " + ProjectSettings.GlobalizePath("user://pixellook.json"), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0) };
        spalte.AddChild(_pfad);
    }

    private void Abgleichen()
    {
        foreach (var a in _reglerNeu) a();
        Anwenden();
    }

    private void Schalter(Container eltern, string text, Func<Werte, bool> lesen, Action<Werte, bool> setzen)
    {
        var k = new CheckBox { Text = text, ButtonPressed = lesen(_w) };
        k.Toggled += an => { setzen(_w, an); Anwenden(); };
        _reglerNeu.Add(() => k.SetPressedNoSignal(lesen(_w)));
        eltern.AddChild(k);
    }

    private void Regler(Container eltern, string text, float min, float max, float schritt, Func<Werte, float> lesen, Action<Werte, float> setzen)
    {
        var name = new Label { Text = text };
        var h = new HSlider { MinValue = min, MaxValue = max, Step = schritt, Value = lesen(_w), CustomMinimumSize = new Vector2(210, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var wert = new Label { Text = lesen(_w).ToString("0.###", CultureInfo.InvariantCulture), CustomMinimumSize = new Vector2(46, 0) };
        h.ValueChanged += v => { setzen(_w, (float)v); wert.Text = ((float)v).ToString("0.###", CultureInfo.InvariantCulture); Anwenden(); };
        _reglerNeu.Add(() => { h.SetValueNoSignal(lesen(_w)); wert.Text = lesen(_w).ToString("0.###", CultureInfo.InvariantCulture); });
        var zeile = new HBoxContainer();
        zeile.AddChild(h); zeile.AddChild(wert);
        eltern.AddChild(name); eltern.AddChild(zeile);
    }

    private void Wahl(Container eltern, string text, string[] eintraege, Func<Werte, int> lesen, Action<Werte, int> setzen)
    {
        var o = new OptionButton();
        foreach (var e in eintraege) o.AddItem(e);
        o.Selected = lesen(_w);
        o.ItemSelected += i => { setzen(_w, (int)i); Anwenden(); };
        _reglerNeu.Add(() => o.Selected = lesen(_w));
        var zeile = new HBoxContainer();
        zeile.AddChild(new Label { Text = text + ": " }); zeile.AddChild(o);
        eltern.AddChild(zeile);
    }

    private static void Knopf(Container eltern, string text, Action tat)
    {
        var k = new Button { Text = text };
        k.Pressed += tat;
        eltern.AddChild(k);
    }

    private string Json()
    {
        var d = new Godot.Collections.Dictionary
        {
            ["pixel_look_an"] = _w.An, ["pixelgroesse_weg_a"] = _w.PixelGroesse, ["texel"] = _w.Texel, ["texelgroesse_bildpunkte"] = _w.TexelPx, ["zweierstufen"] = _w.Zweier, ["rasterung_ab_bildpunkte"] = _w.Ferne,
            ["stufenlicht"] = _w.Stufen, ["lichtstufen"] = _w.Lichtstufen, ["stufen_staerke"] = _w.StufenStaerke,
            ["licht_unterste_stufe"] = _w.LichtDunkel, ["licht_oberste_stufe"] = _w.LichtHell,
            ["kantenlicht"] = _w.Kanten, ["kantenlicht_staerke"] = _w.KanteHell, ["duesternis"] = _w.Duester,
            ["gras_halme"] = _w.Halme, ["kamera"] = _spiel ? "spielkamera" : "flugkamera", ["spielkamera_zoom_m"] = _spielZoom,
        };
        return Json_(d);
    }

    private static string Json_(Godot.Collections.Dictionary d) => Godot.Json.Stringify(d, "  ");

    private void Speichern()
    {
        using var f = Godot.FileAccess.Open("user://pixellook.json", Godot.FileAccess.ModeFlags.Write);
        if (f is null) { _pfad.Text = "Speichern fehlgeschlagen."; return; }
        f.StoreString(Json());
        _pfad.Text = "Gespeichert: " + ProjectSettings.GlobalizePath("user://pixellook.json");
    }

    private void AnzeigeAktualisieren()
    {
        _anzeige.Text = $"Pixeltest – Kamera: {(_spiel ? $"Spielkamera (Zoom {_spielZoom:F0} m)" : "Flugkamera")}   Texel {_w.TexelPx:0.#} Bildpunkte   Bilder/s: {Engine.GetFramesPerSecond():F0}\n" +
                        "C Spielkamera  V weit  F Flugkamera  Mausrad Zoom  R Pixel  0 aus  Tab Tafel";
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } t) return;
        switch (t.Keycode)
        {
            case Key.Key1: Flug(false); break;
            case Key.Key2: Flug(true); break;
            case Key.F: Flug(false); break;
            case Key.C: SpielSicht(40f); break;
            case Key.V: SpielSicht(140f); break;
            case Key.R: _w = Pixel(); Abgleichen(); break;
            case Key.Key0: _w = Aus(); Abgleichen(); break;
            case Key.Key9: _nebel = !_nebel; Anwenden(); break;
            case Key.K: _kreis = !_kreis; break;
            case Key.Tab: _tafel.Visible = !_tafel.Visible; break;
            case Key.Escape: GetTree().Quit(); break;
        }
    }

    public override void _Input(InputEvent e)
    {
        if (_spiel && e is InputEventMouseButton { Pressed: true } mb && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown)
            && !_tafel.GetGlobalRect().HasPoint(mb.Position))
        {
            _spielZoom = Mathf.Clamp(_spielZoom * (mb.ButtonIndex == MouseButton.WheelUp ? 0.9f : 1.1f), 15f, 140f);
            KameraWerte();
        }
    }

    public override void _Process(double delta)
    {
        _zeit += delta;
        if (_scrollTempo != 0 && _spiel) _scroll += _scrollTempo * (float)delta;
        SpielKameraSetzen();
        LichtKameraAbgleichen();
        if (_kreis && !_spiel)
        {
            _kreisWinkel += (float)delta * 0.25f;
            var m = new Vector3(6, 2, 2);
            _kamera.Blicken(m + new Vector3(Mathf.Sin(_kreisWinkel), 0.35f, Mathf.Cos(_kreisWinkel)) * 22f, m);
        }
        if (_ende > 0 && _zeit > _ende) GetTree().Quit();
        if (_messSchritt >= 0) Messen(delta);
        else if (Mathf.PosMod((float)_zeit, 1f) < delta) AnzeigeAktualisieren();
    }

    private void Messen(double delta)
    {
        if (_messZeit == 0)
        {
            _w = _messSchritt == 0 ? Aus() : Pixel();
            if (_messSchritt >= 2) _w.PixelGroesse = _messSchritt;
            Abgleichen(); _gpu.Clear(); _cpu.Clear();
        }
        _messZeit += delta;
        if (_messZeit > 2.0)
        {
            var rids = new[] { GetViewport().GetViewportRid(), _welt.GetViewportRid(), _lichtBild.GetViewportRid() };
            _gpu.Add(rids.Sum(r => (double)RenderingServer.ViewportGetMeasuredRenderTimeGpu(r)));
            _cpu.Add(rids.Sum(r => (double)RenderingServer.ViewportGetMeasuredRenderTimeCpu(r)));
        }
        if (_messZeit > 8.0)
        {
            GD.Print($"[messung] {_messNamen[_messSchritt]}: Grafikkarte {_gpu.Average():F2} ms, CPU {_cpu.Average():F2} ms, {Engine.GetFramesPerSecond():F0} Bilder/s");
            _messZeit = 0; _messSchritt++;
            if (_messSchritt >= _messNamen.Length) GetTree().Quit();
        }
    }
}
