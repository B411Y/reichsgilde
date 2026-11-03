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
/// Runde 4 (Feinschliff auf Weg A, alle Bausteine einzeln schaltbar): Umrisse, Glühen (volle Auflösung), Tiefendunst, Kontrast und Vignette, weiche Schattenkante, ruhiges Gras, feste Zoomstufen.
/// Optionen dazu: <c>--pixelvorgabe=alt|fein|aus</c> (Standard fein = „Pixel 2 fein“), <c>--pixelnur=umriss|gluehen|dunst|farbe|schatten|gras|zoom</c> (alt plus ein Baustein), <c>--pixelabend=1</c> (Abendbild mit Fensterlicht und Laternen), <c>--pixelssao=0</c>, <c>--pixelschatten=0</c>. Taste T = Pixel 2 alt, R = Pixel 2 fein.
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
        // Runde 4 (Feinschliff Weg A); die alte Voreinstellung hat alles aus
        public bool Umriss, Gluehen, Dunst, GrasRuhig;
        public float UmrissDunkel = 0.4f, UmrissHell = 0.2f, GluehStaerke = 0.85f, GluehSchwelle = 0.3f, GluehRadius = 1f;
        public float DunstDichte = 0.0045f, DunstMax = 0.42f, Kontrast = 1f, Vignette = 0f;
        public int SchattenWeich, ZoomModus = 2;   // Schatten: 0 hart, 1 weich, 2 weicher; Zoom: 0 stufenlos (schwimmt), 1 gleiten und nach dem Stopp neu ausrichten, 2 feste Stufen
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
    private bool _weltA, _abend;
    private ColorRect _gluehen = null!, _vignette = null!;
    private ShaderMaterial _gluehStoff = null!, _vignetteStoff = null!;
    private MultiMeshInstance3D _grasRuhig = null!;
    private float _zoomZiel = 40f, _zoomAnim = 40f, _zoomRuhe, _lichtskala = 1f;
    private static readonly float[] ZoomStufen = { 15f, 20f, 28f, 40f, 56f, 80f, 112f, 140f };
    private Basis _spielBasis; private Vector3 _spielOrt; private float _scroll, _scrollTempo;
    private Label _anzeige = null!, _pfad = null!;
    private PanelContainer _tafel = null!;
    private readonly Dictionary<string, Action<Werte>> _abgleich = new();
    private readonly List<Action> _reglerNeu = new();
    private readonly List<MeshInstance3D> _netze = new();
    private double _ende, _zeit;
    private double _messZeit; private int _messSchritt = -1; private readonly List<double> _gpu = new(), _cpu = new();
    private readonly string[] _messNamen = { "aus", "Pixel 2 alt (Runde 3)", "Pixel 2 fein (Runde 4)", "fein ohne ruhiges Gras (alt: Gras aus)", "fein ohne Glühen", "fein ohne Umrisse", "fein ohne Dunst, Kontrast, Vignette, weiche Schatten", "Pixel 3 fein", "Pixel 2 fein, Zoom 140 m" };

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
        _w = _optionen.GetValueOrDefault("pixelvorgabe", "fein") switch { "alt" => Pixel2Alt(), "aus" => Aus(), _ => Pixel2Fein() };
        if (_optionen.TryGetValue("pixelnur", out var nur)) _w = NurBaustein(nur);
        if (_optionen.GetValueOrDefault("pixelmodus", "pixel") == "aus") _w = Aus();
        if (float.TryParse(_optionen.GetValueOrDefault("pixeltexel", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var tpx)) _w.TexelPx = tpx;
        _abend = _optionen.GetValueOrDefault("pixelabend") == "1";
        if (_optionen.GetValueOrDefault("pixelssao") == "0") _umgebung.Environment.SsaoEnabled = false;
        if (_optionen.GetValueOrDefault("pixelschatten") == "0") _sonne.ShadowEnabled = false;
        if (_abend) AbendBauen();
        OberflaecheBauen();
        if (int.TryParse(_optionen.GetValueOrDefault("pixelgroesse", ""), out var pg)) { _w.PixelGroesse = Mathf.Clamp(pg, 1, 8); }
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
    /// <summary>Die Voreinstellung der Runde 3 mit Pixelgröße 2 (Vergleich).</summary>
    private static Werte Pixel2Alt() => new() { PixelGroesse = 2, ZoomModus = 0 };
    /// <summary>„Pixel 2 fein“: Runde 3 plus Umrisse, Glühen, Tiefendunst, Kontrast, Vignette, weiche Schattenkante, ruhiges Gras, feste Zoomstufen.</summary>
    private static Werte Pixel2Fein() => new()
    {
        PixelGroesse = 2, Umriss = true, Gluehen = true, Dunst = true, GrasRuhig = true, Kontrast = 1.12f, Vignette = 0.18f, SchattenWeich = 1, ZoomModus = 2, KanteHell = 0f,
    };
    /// <summary>Alt plus genau ein Baustein (Prüfbilder): umriss, gluehen, dunst, farbe, schatten, gras, zoom.</summary>
    private static Werte NurBaustein(string name)
    {
        var w = Pixel2Alt();
        switch (name)
        {
            case "umriss": w.Umriss = true; w.KanteHell = 0f; break;
            case "gluehen": w.Gluehen = true; break;
            case "dunst": w.Dunst = true; break;
            case "farbe": w.Kontrast = 1.12f; w.Vignette = 0.18f; break;
            case "schatten": w.SchattenWeich = 1; break;
            case "gras": w.GrasRuhig = true; break;
            case "zoom": w.ZoomModus = 2; break;
        }
        return w;
    }

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

        _gras = Gras(false);
        _grasRuhig = Gras(true);

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

    /// <summary>Abendbild (<c>--pixelabend=1</c>): tiefe warme Sonne, Dämmerhimmel, Fensterlicht, Laternen und Feuer – um das Glühen zu zeigen.</summary>
    private void AbendBauen()
    {
        _lichtskala = 0.62f;
        _sonne.RotationDegrees = new Vector3(-9, 140, 0);
        _sonne.LightColor = new Color(1f, 0.58f, 0.32f); _sonne.LightEnergy = 0.75f;
        var env = _umgebung.Environment;
        var himmel = (ProceduralSkyMaterial)env.Sky.SkyMaterial;
        himmel.SkyTopColor = new Color(0.09f, 0.12f, 0.24f); himmel.SkyHorizonColor = new Color(0.55f, 0.36f, 0.30f);
        himmel.GroundHorizonColor = new Color(0.22f, 0.18f, 0.20f); himmel.GroundBottomColor = new Color(0.08f, 0.08f, 0.12f);
        env.AmbientLightColor = new Color(0.40f, 0.46f, 0.70f); env.AmbientLightEnergy = 0.55f;
        // Fenster der Häuser leuchten (fenster.gdshader wie im Spiel)
        var fenster = GD.Load<Shader>("res://assets/shader/fenster.gdshader");
        var stoff = new ShaderMaterial { Shader = fenster };
        stoff.SetShaderParameter("helligkeit", 1f); stoff.SetShaderParameter("anteil", 0.85f);
        foreach (var m in _netze)
        {
            if (m.Mesh is null) continue;
            for (var i = 0; i < m.Mesh.GetSurfaceCount(); i++)
                if (m.Mesh.SurfaceGetMaterial(i)?.ResourceName == "Fenster") m.SetSurfaceOverrideMaterial(i, stoff);
        }
        // Laternen: Pfahl, glühende Kugel, Licht
        foreach (var p in new[] { new Vector3(-1.5f, 0, 10), new Vector3(15, 0, 9), new Vector3(-8, 0, 1), new Vector3(3, 0, -1) })
        {
            var y = Hoehe(p.X, p.Z);
            var pfahl = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.07f, Height = 2.4f }, Position = new Vector3(p.X, y + 1.2f, p.Z) };
            pfahl.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.12f, 0.09f) };
            AddChild(pfahl); _netze.Add(pfahl);
            var kugel = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.17f, Height = 0.34f }, Position = new Vector3(p.X, y + 2.5f, p.Z) };
            kugel.MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1f, 0.72f, 0.38f) };
            AddChild(kugel); _netze.Add(kugel);
            AddChild(new OmniLight3D { Position = new Vector3(p.X, y + 2.5f, p.Z), LightColor = new Color(1f, 0.62f, 0.3f), LightEnergy = 1.5f, OmniRange = 8f });
        }
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
    private MultiMeshInstance3D Gras(bool ruhig)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        // Senkrechte schmale Halme (zwei gekreuzte Dreiecke je Büschel), Normalen nach oben wie beim Boden; Farben wie die Wiese des Bodens
        for (var k = 0; k < 2; k++)
        {
            // ruhig: breiter (mehr als ein Weltpixel), kürzer, Spitze und Fuß näher an der Wiesenfarbe, dafür dichter – weniger Strichrauschen im Low-Res-Bild
            var w = k * Mathf.Pi / 2f; var d = new Vector3(Mathf.Cos(w), 0, Mathf.Sin(w)) * (ruhig ? 0.07f : 0.035f);
            var fuss = ruhig ? new Color(0.25f, 0.325f, 0.15f) : new Color(0.24f, 0.32f, 0.14f);
            var spitze = ruhig ? new Color(0.39f, 0.49f, 0.23f) : new Color(0.40f, 0.50f, 0.24f);
            st.SetNormal(Vector3.Up); st.SetColor(fuss); st.AddVertex(-d);
            st.SetNormal(Vector3.Up); st.SetColor(fuss); st.AddVertex(d);
            st.SetNormal(Vector3.Up); st.SetColor(spitze); st.AddVertex(new Vector3(0, ruhig ? 0.38f : 0.6f, 0));
        }
        var netz = st.Commit();
        var zufall = new RandomNumberGenerator { Seed = 11 };
        var anzahl = ruhig ? 42000 : 16000;
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
        var knoten = new MultiMeshInstance3D
        {
            Multimesh = mm, Name = "Gras", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = ruhig ? new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/pixel_gras.gdshader") }
                : new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 1f },
        };
        AddChild(knoten);
        return knoten;
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
        _gluehStoff.SetShaderParameter("licht", _lichtBild.GetTexture());
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
        // Glühen (additiv, weich, volle Auflösung) als Kind des Weltbildes: folgt Versatz und Größe; Vignette (mal) über dem ganzen Fenster
        _gluehStoff = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/pixel_gluehen.gdshader") };
        _gluehStoff.SetShaderParameter("welt", _welt.GetTexture());
        _gluehen = new ColorRect { Material = _gluehStoff, MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White, Visible = false };
        _gluehen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _weltAnzeige.AddChild(_gluehen);
        _vignetteStoff = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/pixel_vignette.gdshader") };
        _vignette = new ColorRect { Material = _vignetteStoff, MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White, Visible = false };
        _vignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _weltEbene.AddChild(_vignette);
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
        var anzeigeGroesse = new Vector2(gross.X, gross.Y) * n;
        var anzeigeOrt = (fenster - anzeigeGroesse) / 2f + new Vector2(-rest.X, rest.Y) / ps * n;
        // Zoom-Gleiten (Modus 1): das fertige Bild wird bis zum Stopp um die Fenstermitte skaliert, erst danach neu gerastert
        var s = _w.ZoomModus == 1 && _zoomAnim > 0 ? _spielZoom / _zoomAnim : 1f;
        if (Mathf.Abs(s - 1f) > 0.0005f)
        {
            var mitte = fenster / 2f;
            anzeigeOrt = mitte + (anzeigeOrt + anzeigeGroesse / 2f - mitte) * s - anzeigeGroesse * s / 2f;
            anzeigeGroesse *= s;
        }
        _weltAnzeige.Size = anzeigeGroesse;
        _weltAnzeige.Position = anzeigeOrt;
        _gluehStoff.SetShaderParameter("welt_groesse", new Vector2(gross.X, gross.Y));
        _farbStoff.SetShaderParameter("ortho_groesse", _spielKamera.Size);
        _farbStoff.SetShaderParameter("dunst_bezug", 150f);   // Entfernung Kamera – Blickpunkt (SpielSicht)
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
        _farbStoff.SetShaderParameter("umriss_an", w.Umriss);
        _farbStoff.SetShaderParameter("umriss_dunkel", w.UmrissDunkel);
        _farbStoff.SetShaderParameter("umriss_hell", w.UmrissHell);
        _farbStoff.SetShaderParameter("dunst_an", w.Dunst);
        _farbStoff.SetShaderParameter("dunst_dichte", w.DunstDichte);
        _farbStoff.SetShaderParameter("dunst_max", w.DunstMax);
        _farbStoff.SetShaderParameter("dunst_farbe", _abend ? new Vector3(0.10f, 0.11f, 0.16f) : new Vector3(0.55f, 0.6f, 0.68f));
        _farbStoff.SetShaderParameter("kontrast", w.Kontrast);
        _farbStoff.SetShaderParameter("schatten_weich", w.SchattenWeich);
        _farbStoff.SetShaderParameter("lichtskala", _lichtskala);
        _gluehen.Visible = w.Gluehen && _weltA;
        _gluehStoff.SetShaderParameter("staerke", w.GluehStaerke);
        _gluehStoff.SetShaderParameter("schwelle", w.GluehSchwelle);
        _gluehStoff.SetShaderParameter("radius", w.GluehRadius);
        _vignette.Visible = w.Vignette > 0.001f && _weltA;
        _vignetteStoff.SetShaderParameter("staerke", w.Vignette);
        _gras.Visible = w.Halme && !w.GrasRuhig;
        _grasRuhig.Visible = w.Halme && w.GrasRuhig;
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
        _zoomZiel = _zoomAnim = _spielZoom;
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
        spalte.AddChild(new Label { Text = "— Runde 4: Feinschliff —" });
        Schalter(spalte, "1 Umrisse (Außenkontur, Innenkanten)", w => w.Umriss, (w, v) => w.Umriss = v);
        Regler(spalte, "Umriss-Faktor (kleiner = dunkler)", 0.2f, 1f, 0.02f, w => w.UmrissDunkel, (w, v) => w.UmrissDunkel = v);
        Regler(spalte, "Innenkante heller um", 0, 0.4f, 0.01f, w => w.UmrissHell, (w, v) => w.UmrissHell = v);
        Schalter(spalte, "2 Glühen (volle Auflösung)", w => w.Gluehen, (w, v) => w.Gluehen = v);
        Regler(spalte, "Glühen Stärke", 0, 1.5f, 0.05f, w => w.GluehStaerke, (w, v) => w.GluehStaerke = v);
        Regler(spalte, "Glühen ab Helligkeit", 0.1f, 1f, 0.01f, w => w.GluehSchwelle, (w, v) => w.GluehSchwelle = v);
        Regler(spalte, "Glühen Radius (Vielfaches)", 0.4f, 2.5f, 0.1f, w => w.GluehRadius, (w, v) => w.GluehRadius = v);
        Schalter(spalte, "3 Tiefendunst", w => w.Dunst, (w, v) => w.Dunst = v);
        Regler(spalte, "Dunst Dichte (je m)", 0, 0.02f, 0.0005f, w => w.DunstDichte, (w, v) => w.DunstDichte = v);
        Regler(spalte, "Dunst höchstens", 0, 0.8f, 0.01f, w => w.DunstMax, (w, v) => w.DunstMax = v);
        Regler(spalte, "4 Kontrast (1 = aus)", 0.8f, 1.4f, 0.01f, w => w.Kontrast, (w, v) => w.Kontrast = v);
        Regler(spalte, "4 Vignette (0 = aus)", 0, 0.5f, 0.01f, w => w.Vignette, (w, v) => w.Vignette = v);
        Regler(spalte, "5 Schattenkante (0 hart, 1 weich, 2 weicher)", 0, 2, 1, w => w.SchattenWeich, (w, v) => w.SchattenWeich = (int)v);
        Regler(spalte, "6 Zoom (0 stufenlos, 1 gleiten, 2 Stufen)", 0, 2, 1, w => w.ZoomModus, (w, v) => w.ZoomModus = (int)v);
        Schalter(spalte, "7 Gras ruhig (dichter, kürzer)", w => w.GrasRuhig, (w, v) => w.GrasRuhig = v);

        var reihe = new HBoxContainer(); spalte.AddChild(reihe);
        Knopf(reihe, "Pixel 2 alt", () => { _w = Pixel2Alt(); Abgleichen(); });
        Knopf(reihe, "Pixel 2 fein", () => { _w = Pixel2Fein(); Abgleichen(); });
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
            ["gras_halme"] = _w.Halme,
            ["umriss"] = _w.Umriss, ["umriss_faktor"] = _w.UmrissDunkel, ["innenkante_heller"] = _w.UmrissHell,
            ["gluehen"] = _w.Gluehen, ["gluehen_staerke"] = _w.GluehStaerke, ["gluehen_schwelle"] = _w.GluehSchwelle, ["gluehen_radius"] = _w.GluehRadius,
            ["tiefendunst"] = _w.Dunst, ["dunst_dichte"] = _w.DunstDichte, ["dunst_max"] = _w.DunstMax, ["kontrast"] = _w.Kontrast, ["vignette"] = _w.Vignette,
            ["schattenkante"] = _w.SchattenWeich, ["zoom_modus"] = _w.ZoomModus, ["gras_ruhig"] = _w.GrasRuhig, ["kamera"] = _spiel ? "spielkamera" : "flugkamera", ["spielkamera_zoom_m"] = _spielZoom,
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
            case Key.R: _w = Pixel2Fein(); Abgleichen(); break;
            case Key.T: _w = Pixel2Alt(); Abgleichen(); break;
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
            var hoch = mb.ButtonIndex == MouseButton.WheelUp;
            if (_w.ZoomModus == 0 || !_weltA)
            {
                _spielZoom = _zoomZiel = _zoomAnim = Mathf.Clamp(_spielZoom * (hoch ? 0.9f : 1.1f), 15f, 140f);
            }
            else if (_w.ZoomModus == 2)
            {
                // feste Stufen: eine Stufe je Rasttick, sofort neu gerastert (das Raster ist je Stufe immer dasselbe, nichts schwimmt)
                var i = Array.FindIndex(ZoomStufen, z => z >= _spielZoom - 0.01f);
                if (i < 0) i = ZoomStufen.Length - 1;
                i = Mathf.Clamp(i + (hoch ? -1 : 1), 0, ZoomStufen.Length - 1);
                _spielZoom = _zoomZiel = _zoomAnim = ZoomStufen[i];
            }
            else
            {
                _zoomZiel = Mathf.Clamp(_zoomZiel * (hoch ? 0.9f : 1.1f), 15f, 140f);
                _zoomRuhe = 0f;
            }
            KameraWerte();
        }
    }

    public override void _Process(double delta)
    {
        _zeit += delta;
        if (_scrollTempo != 0 && _spiel) _scroll += _scrollTempo * (float)delta;
        if (_w.ZoomModus == 1 && _weltA && Mathf.Abs(_zoomZiel - _spielZoom) > 0.001f)
        {
            _zoomAnim = Mathf.Lerp(_zoomAnim, _zoomZiel, 1f - Mathf.Exp(-14f * (float)delta));
            _zoomRuhe += (float)delta;
            if (_zoomRuhe > 0.3f) { _spielZoom = _zoomAnim = _zoomZiel; KameraWerte(); }   // nach dem Stopp: neu ausrichten
        }
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
            _w = _messSchritt == 0 ? Aus() : _messSchritt == 1 ? Pixel2Alt() : Pixel2Fein();
            if (_messSchritt == 3) _w.Halme = false;
            if (_messSchritt == 4) _w.Gluehen = false;
            if (_messSchritt == 5) _w.Umriss = false;
            if (_messSchritt == 6) { _w.Dunst = false; _w.Kontrast = 1f; _w.Vignette = 0f; _w.SchattenWeich = 0; }
            if (_messSchritt == 7) _w.PixelGroesse = 3;
            Abgleichen();
            if (_messSchritt == 8) SpielSicht(140f); _gpu.Clear(); _cpu.Clear();
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
