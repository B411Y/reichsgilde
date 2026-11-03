using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Testszene „Pixel-Look“ (Versuch, Nutzer 03.11.2026): derselbe Ausschnitt (Häuser, Figuren, Gras, Fels, Teich, Feuer) in voller Auflösung, aber mit
/// dem Stil einer Pixelart-Vorlage: wenige Lichtstufen mit harter Schattengrenze und violettem Schatten, warme entsättigte Farben, helle/dunkle Kanten
/// statt schwarzem Umriss, wenige Texelflecken, Pixel-Wasser. Alles live einstellbar über die Reglertafel (Tab klappt sie ein und aus);
/// „Werte speichern“ schreibt die Einstellung nach <c>user://pixellook.json</c> (Pfad steht auf der Tafel), „Werte kopieren“ legt sie in die Zwischenablage.
/// Startet ohne Optionen (auch mit F6 im Godot-Editor) in der Spielkamera (orthografisch, 40 Grad schräg) mit der Voreinstellung „Referenz“.
/// Tasten: C Spielkamera, F Flugkamera, V Spielkamera weit, Mausrad Zoom (Spielkamera), R Referenz, 0 aus, Tab Tafel, Esc Ende.
/// Startoptionen (alle freiwillig): <c>--pixelmodus=aus|referenz</c>, <c>--pixelsicht=spiel|spielnah|spielweit|flug|nah</c>, <c>--pixeltexel=&lt;Bildpunkte&gt;</c>, <c>--pixelpalette=aus|oklab|rgb</c>, <c>--pixelkreis=1</c>,
/// <c>--pixelmessung=1</c> (aus und an je 6 s messen, Ergebnis im Log, dann beenden), <c>--pixelende=&lt;s&gt;</c>, <c>--pixeltafel=0</c> (Tafel zu).
/// Kein Teil des Spiels; die Shader liegen in <c>assets/shader/pixel_*.gdshader</c>.
/// </summary>
public partial class Pixeltest : Node3D
{
    public const string Szene = "res://scenes/Pixeltest.tscn";

    public static bool Angefordert =>
        OS.GetCmdlineUserArgs().Any(a => a == "--pixeltest" || (a.StartsWith("--pixeltest=") && a != "--pixeltest=0"));

    /// <summary>Alle einstellbaren Werte (werden gespeichert).</summary>
    private sealed class Werte
    {
        public bool An = true, Stufen = true, Kanten = true, Flecken = true, Halme = true;
        public int Lichtstufen = 3, Palette = 0, PaletteStufen = 12, Dither = 0;
        public float TexelPx = 3f, SchattenTon = -60f, SchattenStaerke = 0.05f, KanteHell = 0.13f, KanteDunkel = 0.12f, Saettigung = 0.8f, Waerme = 0.03f, Ferne = 1.4f;
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
    private ShaderMaterial _farbStoff = null!, _wasserPixel = null!;
    private Material? _wasserVorher;
    private Label _anzeige = null!, _pfad = null!;
    private PanelContainer _tafel = null!;
    private readonly Dictionary<string, Action<Werte>> _abgleich = new();
    private readonly List<Action> _reglerNeu = new();
    private readonly List<MeshInstance3D> _netze = new();
    private readonly Dictionary<BaseMaterial3D, BaseMaterial3D.TextureFilterEnum> _filterVorher = new();
    private double _ende, _zeit;
    private double _messZeit; private int _messSchritt = -1; private readonly List<double> _gpu = new(), _cpu = new();
    private readonly string[] _messNamen = { "aus", "Referenz", "Referenz mit Palette Oklab" };

    public override void _Ready()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var t = arg.TrimStart('-').Split('=', 2);
            if (t.Length == 2) _optionen[t[0]] = t[1];
        }
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        double.TryParse(_optionen.GetValueOrDefault("pixelende", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out _ende);
        _kreis = _optionen.GetValueOrDefault("pixelkreis") == "1";

        UmgebungBauen();
        WeltBauen();
        _kamera = FreieKamera.Einhaengen(this, new Vector3(30, 14, 34), new Vector3(0, 2, 0));
        _kamera.Bodenhoehe = Hoehe;
        _kamera.MindestHoehe = 0.4f;
        _spielKamera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = _spielZoom, Near = 1f, Far = 600f, Name = "Spielkamera" };
        AddChild(_spielKamera);
        NachbearbeitungBauen();
        if (_optionen.GetValueOrDefault("pixelmodus", "referenz") == "aus") _w = Aus();
        if (float.TryParse(_optionen.GetValueOrDefault("pixeltexel", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var tpx)) _w.TexelPx = tpx;
        _w.Palette = _optionen.GetValueOrDefault("pixelpalette", "aus") switch { "oklab" => 1, "rgb" => 2, _ => _w.Palette };
        OberflaecheBauen();
        switch (_optionen.GetValueOrDefault("pixelsicht", "spiel"))
        {
            case "flug": Flug(false); break;
            case "nah": Flug(true); break;
            case "spielweit": SpielSicht(140f); break;
            case "spielnah": SpielSicht(25f); break;
            default: SpielSicht(60f); break;
        }
        if (_optionen.GetValueOrDefault("pixeltafel") == "0") _tafel.Visible = false;
        if (_optionen.GetValueOrDefault("pixelmessung") == "1") _messSchritt = 0;
        GD.Print($"[pixeltest] renderer={RenderingServer.GetCurrentRenderingDriverName()} netze={_netze.Count}");
    }

    private static Werte Aus() => new() { An = false };
    private static Werte Referenz() => new();

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
        _wasser = wasser; _wasserVorher = wasser.MaterialOverride;
        _wasserPixel = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/pixel_wasser.gdshader") };

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
        // Senkrechte schmale Halme (zwei gekreuzte Dreiecke je Büschel), Normalen nach oben wie beim Boden: kein Knick, also keine Kante im Gras;
        // hell und warm wie Heu, vorn dunkler wirkt durch das Stufenlicht
        for (var k = 0; k < 2; k++)
        {
            var w = k * Mathf.Pi / 2f; var d = new Vector3(Mathf.Cos(w), 0, Mathf.Sin(w)) * 0.035f;
            st.SetNormal(Vector3.Up); st.SetColor(new Color(0.56f, 0.45f, 0.2f)); st.AddVertex(-d);
            st.SetNormal(Vector3.Up); st.SetColor(new Color(0.56f, 0.45f, 0.2f)); st.AddVertex(d);
            st.SetNormal(Vector3.Up); st.SetColor(new Color(0.90f, 0.78f, 0.36f)); st.AddVertex(new Vector3(0, 0.6f, 0));
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
        _kamera.AddChild(_flaeche);
    }

    private void Anwenden()
    {
        var w = _w;
        _flaeche.Visible = w.An;
        _farbStoff.SetShaderParameter("stufen_an", w.Stufen);
        _farbStoff.SetShaderParameter("stufen", w.Lichtstufen);
        _farbStoff.SetShaderParameter("palette", w.Palette);
        _farbStoff.SetShaderParameter("pal_l", w.PaletteStufen);
        _farbStoff.SetShaderParameter("dither", w.Dither);
        _farbStoff.SetShaderParameter("dither_staerke", 0.8f);
        _farbStoff.SetShaderParameter("koernung_an", w.Flecken);
        _farbStoff.SetShaderParameter("koernung", 0.18f);
        _farbStoff.SetShaderParameter("texel_je_meter", 16f);
        _farbStoff.SetShaderParameter("texel_auto", _spiel);
        KameraWerte();
        _farbStoff.SetShaderParameter("texel_ziel_px", w.TexelPx);
        _farbStoff.SetShaderParameter("blende_von", w.Ferne);
        _farbStoff.SetShaderParameter("umriss_an", w.Kanten);
        _farbStoff.SetShaderParameter("kante_hell", w.KanteHell);
        _farbStoff.SetShaderParameter("kante_dunkel", w.KanteDunkel);
        _farbStoff.SetShaderParameter("sattel", w.Saettigung);
        _farbStoff.SetShaderParameter("waerme", new Vector2(w.Waerme / 3f, w.Waerme));
        var ton = Mathf.DegToRad(w.SchattenTon);
        _farbStoff.SetShaderParameter("schatten_ton", new Vector2(Mathf.Cos(ton), Mathf.Sin(ton)) * w.SchattenStaerke);
        _farbStoff.SetShaderParameter("sonne_richtung", -_sonne.GlobalBasis.Z);
        _wasserPixel.SetShaderParameter("texel", 0.16f);
        _wasser.MaterialOverride = w.An && w.Flecken ? _wasserPixel : _wasserVorher;
        _gras.Visible = w.Halme;
        foreach (var m in _netze)
        {
            if (!IsInstanceValid(m)) continue;
            for (var i = 0; i < m.GetSurfaceOverrideMaterialCount(); i++)
            {
                var mat = m.GetActiveMaterial(i) as BaseMaterial3D;
                if (mat is null) continue;
                if (!_filterVorher.ContainsKey(mat)) _filterVorher[mat] = mat.TextureFilter;
                mat.TextureFilter = w.An && w.Flecken ? BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps : _filterVorher[mat];
            }
        }
        _sonne.ShadowBlur = w.An && w.Stufen ? 0f : 1f;
        _umgebung.Environment.FogEnabled = _nebel;
        AnzeigeAktualisieren();
    }

    private void KameraWerte()
    {
        _farbStoff.SetShaderParameter("ortho_groesse", _spiel ? _spielZoom : 0f);
        _farbStoff.SetShaderParameter("fov_faktor", 2f * Mathf.Tan(Mathf.DegToRad(_kamera.Fov) / 2f));
    }

    private void Aktiv(Camera3D k)
    {
        k.MakeCurrent();
        _flaeche.Reparent(k, false);
    }

    /// <summary>Spielkamera: orthografisch, 40 Grad schräg von oben, 150 m Abstand – wie im Spiel (Stadtansicht.KameraAufbauen). Zoom = sichtbare Höhe in Metern (Spiel: 25–140, Start 95).</summary>
    private void SpielSicht(float zoom)
    {
        _spiel = true; _spielZoom = Mathf.Clamp(zoom, 25f, 140f);
        var basis = Basis.FromEuler(new Vector3(Mathf.DegToRad(-40f), Mathf.DegToRad(30f), 0));
        _spielKamera.GlobalTransform = new Transform3D(basis, new Vector3(2, 0, 2) + basis * new Vector3(0, 0, 150f));
        _spielKamera.Size = _spielZoom;
        Aktiv(_spielKamera);
        Anwenden();
    }

    private void Flug(bool nah)
    {
        _spiel = false; Aktiv(_kamera);
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
        Schalter(spalte, "Stufenlicht", w => w.Stufen, (w, v) => w.Stufen = v);
        Regler(spalte, "Lichtstufen", 2, 5, 1, w => w.Lichtstufen, (w, v) => w.Lichtstufen = (int)v);
        Regler(spalte, "Schattenton (Grad, -60 = violett)", -180, 180, 5, w => w.SchattenTon, (w, v) => w.SchattenTon = v);
        Regler(spalte, "Schattenstärke", 0, 0.15f, 0.005f, w => w.SchattenStaerke, (w, v) => w.SchattenStaerke = v);
        Schalter(spalte, "Kanten hell/dunkel", w => w.Kanten, (w, v) => w.Kanten = v);
        Regler(spalte, "Kante hell", 0, 0.3f, 0.01f, w => w.KanteHell, (w, v) => w.KanteHell = v);
        Regler(spalte, "Kante dunkel", 0, 0.3f, 0.01f, w => w.KanteDunkel, (w, v) => w.KanteDunkel = v);
        Regler(spalte, "Sättigung", 0.2f, 1.4f, 0.05f, w => w.Saettigung, (w, v) => w.Saettigung = v);
        Regler(spalte, "Wärme", 0, 0.08f, 0.005f, w => w.Waerme, (w, v) => w.Waerme = v);
        Schalter(spalte, "Texelflecken und Pixel-Wasser", w => w.Flecken, (w, v) => w.Flecken = v);
        Regler(spalte, "Texelgröße (Bildpunkte, Spielkamera)", 1, 8, 0.5f, w => w.TexelPx, (w, v) => w.TexelPx = v);
        Regler(spalte, "Ferne ausblenden (Texel unter x Punkten)", 0.5f, 4f, 0.1f, w => w.Ferne, (w, v) => w.Ferne = v);
        Schalter(spalte, "Dithering (Welt)", w => w.Dither == 2, (w, v) => w.Dither = v ? 2 : 0);
        Wahl(spalte, "Palette", new[] { "aus", "Oklab", "RGB" }, w => w.Palette, (w, v) => w.Palette = v);
        Regler(spalte, "Palettenstufen", 4, 24, 1, w => w.PaletteStufen, (w, v) => w.PaletteStufen = (int)v);
        Schalter(spalte, "Gras-Halme", w => w.Halme, (w, v) => w.Halme = v);

        var reihe = new HBoxContainer(); spalte.AddChild(reihe);
        Knopf(reihe, "Referenz", () => { _w = Referenz(); Abgleichen(); });
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
            ["pixel_look_an"] = _w.An, ["stufenlicht"] = _w.Stufen, ["lichtstufen"] = _w.Lichtstufen,
            ["schattenton_grad"] = _w.SchattenTon, ["schattenstaerke"] = _w.SchattenStaerke,
            ["kanten"] = _w.Kanten, ["kante_hell"] = _w.KanteHell, ["kante_dunkel"] = _w.KanteDunkel,
            ["saettigung"] = _w.Saettigung, ["waerme"] = _w.Waerme,
            ["texelflecken_und_wasser"] = _w.Flecken, ["texelgroesse_bildpunkte"] = _w.TexelPx, ["ferne_ausblenden_bildpunkte"] = _w.Ferne,
            ["dithering_welt"] = _w.Dither == 2, ["palette"] = new[] { "aus", "oklab", "rgb" }[_w.Palette], ["palettenstufen"] = _w.PaletteStufen,
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
                        "C Spielkamera  V weit  F Flugkamera  Mausrad Zoom  R Referenz  0 aus  Tab Tafel";
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } t) return;
        switch (t.Keycode)
        {
            case Key.Key1: Flug(false); break;
            case Key.Key2: Flug(true); break;
            case Key.F: Flug(false); break;
            case Key.C: SpielSicht(60f); break;
            case Key.V: SpielSicht(140f); break;
            case Key.R: _w = Referenz(); Abgleichen(); break;
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
            _spielZoom = Mathf.Clamp(_spielZoom * (mb.ButtonIndex == MouseButton.WheelUp ? 0.9f : 1.1f), 25f, 140f);
            _spielKamera.Size = _spielZoom;
            KameraWerte();
        }
    }

    public override void _Process(double delta)
    {
        _zeit += delta;
        {
            var hoehe = GetViewport().GetVisibleRect().Size.Y;
            var k = GetViewport().GetCamera3D();
            var mpp = _spiel ? _spielZoom / hoehe : 2f * Mathf.Tan(Mathf.DegToRad(k.Fov) / 2f) * k.GlobalPosition.DistanceTo(_wasser.GlobalPosition) / hoehe;
            _wasserPixel.SetShaderParameter("meter_je_pixel", mpp);
        }
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
            _w = _messSchritt == 0 ? Aus() : Referenz();
            if (_messSchritt == 2) _w.Palette = 1;
            Abgleichen(); _gpu.Clear(); _cpu.Clear();
        }
        _messZeit += delta;
        if (_messZeit > 2.0)
        {
            var rid = GetViewport().GetViewportRid();
            _gpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(rid));
            _cpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(rid));
        }
        if (_messZeit > 8.0)
        {
            GD.Print($"[messung] {_messNamen[_messSchritt]}: Grafikkarte {_gpu.Average():F2} ms, CPU {_cpu.Average():F2} ms, {Engine.GetFramesPerSecond():F0} Bilder/s");
            _messZeit = 0; _messSchritt++;
            if (_messSchritt >= _messNamen.Length) GetTree().Quit();
        }
    }
}
