using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Testszene „Pixel-Look“ (<c>--pixeltest=1</c>): derselbe Ausschnitt (Häuser, Figuren, Gras, Fels, Wasser, Feuer, Nebel) mit Bausteinen, die ihn
/// wie sehr feine Pixelart aussehen lassen – in VOLLER Auflösung, ohne kleinere Renderauflösung und ohne Hochskalieren (Nutzer, 03.11.2026;
/// „Bucket 2“: Texel-Pixelart, Objekte bewegen sich frei). Die Bausteine sind einzeln schaltbar:
/// 3 Stufenlicht (Toon-Stufen, harte Schattenkante), 4 Umriss (1 Bildpunkt, aus Tiefe und Normalen), 5 Palette (aus/frei/Holzschnitt),
/// 6 Dithering (aus/Bildschirm/Welt), 7 Texel-Raster (feste Texel je Meter für alle Netze, Nearest-Filter), 8 Punktgröße 1/2/3, 9 Nebel.
/// Dithering „Welt“ ist an die Texel der Weltpositionen gebunden (bleibt beim Fahren und Drehen am Objekt), „Bildschirm“ an das Bildschirmraster.
/// Weitere Tasten: 1 Übersicht, 2 Nah, 0 alles aus, Y alles an mit freier Palette, X alles an mit Holzschnitt-Palette, K Kamera kreist (Dreh-Beobachtung),
/// Esc Ende. Kamera: <see cref="FreieKamera"/> ab Start. Startoptionen: <c>--pixelmodus=aus|stufen|frei|holz</c>, <c>--pixelsicht=uebersicht|nah</c>,
/// <c>--pixelkreis=1</c>, <c>--pixelmessung=1</c> (aus und alles an je 6 s messen, Ergebnis im Log, dann beenden), <c>--pixelende=&lt;s&gt;</c>.
/// Kein Teil des Spiels; die Shader liegen in <c>assets/shader/pixel_*.gdshader</c>.
/// </summary>
public partial class Pixeltest : Node3D
{
    public const string Szene = "res://scenes/Pixeltest.tscn";

    public static bool Angefordert =>
        OS.GetCmdlineUserArgs().Any(a => a == "--pixeltest" || (a.StartsWith("--pixeltest=") && a != "--pixeltest=0"));

    private readonly Dictionary<string, string> _optionen = new();
    private bool _stufen, _umriss, _texel, _nebel;
    private int _palette, _dither, _punkt = 1;     // Palette 0 aus/1 frei/2 Holzschnitt; Dither 0 aus/1 Bildschirm/2 Welt
    private bool _kreis;
    private float _kreisWinkel;
    private FreieKamera _kamera = null!;
    private WorldEnvironment _umgebung = null!;
    private DirectionalLight3D _sonne = null!;
    private MeshInstance3D _umrissFlaeche = null!;
    private ColorRect _farbe = null!;
    private ShaderMaterial _farbStoff = null!, _umrissStoff = null!, _texelStoff = null!;
    private Label _anzeige = null!;
    private readonly List<MeshInstance3D> _netze = new();
    private readonly Dictionary<BaseMaterial3D, BaseMaterial3D.TextureFilterEnum> _filterVorher = new();
    private double _ende, _zeit, _fps;
    private double _messZeit; private int _messSchritt = -1; private readonly List<double> _gpu = new(), _cpu = new();
    private readonly string[] _messNamen = { "aus", "alles an (frei)", "alles an (Holzschnitt)" };

    private static float Hoehe(float x, float z)
    {
        var y = 0.5f * Mathf.Sin(x * 0.09f) * Mathf.Cos(z * 0.07f) + 0.15f * Mathf.Sin(x * 0.4f + z * 0.3f);
        var d = new Vector2(x + 16f, z - 14f).Length();      // Teich
        if (d < 10f) y -= (1f - d / 10f) * 1.6f + 0.2f;
        return y;
    }

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
        NachbearbeitungBauen();
        OberflaecheBauen();
        Sicht(_optionen.GetValueOrDefault("pixelsicht", "uebersicht") == "nah");
        switch (_optionen.GetValueOrDefault("pixelmodus", "aus"))
        {
            case "stufen": _stufen = true; _umriss = true; break;
            case "frei": Alles(1); break;
            case "holz": Alles(2); break;
        }
        Anwenden();
        if (_optionen.GetValueOrDefault("pixelmessung") == "1") _messSchritt = 0;
        GD.Print($"[pixeltest] renderer={RenderingServer.GetCurrentRenderingDriverName()} netze={_netze.Count}");
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
        AddChild(wasser);   // kein Texel-Overlay auf Wasser (Transparenz)

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
        for (var k = 0; k < 3; k++)
        {
            var w = k * Mathf.Pi / 3f; var d = new Vector3(Mathf.Cos(w), 0, Mathf.Sin(w)) * 0.07f;
            st.SetColor(new Color(0.18f, 0.28f, 0.1f)); st.AddVertex(-d);
            st.SetColor(new Color(0.18f, 0.28f, 0.1f)); st.AddVertex(d);
            st.SetColor(new Color(0.55f, 0.65f, 0.28f)); st.AddVertex(new Vector3(d.Z * 2, 0.5f, -d.X * 2));
        }
        st.GenerateNormals();
        var netz = st.Commit();
        var zufall = new RandomNumberGenerator { Seed = 11 };
        const int anzahl = 9000;
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
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = mm, Name = "Gras", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 1f },
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Nachbearbeitung
    // ═════════════════════════════════════════════════════════════════════════

    private void NachbearbeitungBauen()
    {
        // Umriss: Vollbild-Viereck vor der Kamera (Spatial-Shader liest Tiefe und Normalen)
        _umrissStoff = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/pixel_umriss.gdshader") };
        _umrissFlaeche = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2, 2), FlipFaces = true }, MaterialOverride = _umrissStoff,
            ExtraCullMargin = 16384f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _kamera.AddChild(_umrissFlaeche);

        // Farbe: Vollbild-Rechteck auf dem Bild (Canvas-Shader liest das fertige Bild)
        var ebene = new CanvasLayer { Layer = 1 };
        AddChild(ebene);
        _farbStoff = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/pixel_farbe.gdshader") };
        _farbe = new ColorRect { Material = _farbStoff, MouseFilter = Control.MouseFilterEnum.Ignore };
        _farbe.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        ebene.AddChild(_farbe);

        // Texel-Raster: Overlay auf allen Netzen
        _texelStoff = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/pixel_texel.gdshader") };
    }

    private void Alles(int palette)
    {
        _stufen = true; _umriss = true; _palette = palette; _dither = 2; _texel = true; _punkt = 1;
    }

    private void Anwenden()
    {
        _umrissFlaeche.Visible = _umriss;
        _farbe.Visible = _stufen || _palette != 0 || _dither == 1;
        _farbStoff.SetShaderParameter("stufen_an", _stufen);
        _farbStoff.SetShaderParameter("stufen", 4);
        _farbStoff.SetShaderParameter("palette", _palette);
        _farbStoff.SetShaderParameter("dither_an", _dither == 1);
        _farbStoff.SetShaderParameter("dither_punkt", _punkt);
        var raster = _texel || _dither == 2;
        _texelStoff.SetShaderParameter("geordnet", _dither == 2);
        _texelStoff.SetShaderParameter("koernung", _dither == 2 ? 0.07f : 0.16f);
        _texelStoff.SetShaderParameter("texel_je_meter", 16f / _punkt);
        foreach (var m in _netze)
        {
            if (!IsInstanceValid(m)) continue;
            m.MaterialOverlay = raster ? _texelStoff : null;
            // Nearest-Filter für alle Standard-Materialien (Texturen der Modelle)
            for (var i = 0; i < m.GetSurfaceOverrideMaterialCount(); i++)
            {
                var mat = m.GetActiveMaterial(i) as BaseMaterial3D;
                if (mat is null) continue;
                if (!_filterVorher.ContainsKey(mat)) _filterVorher[mat] = mat.TextureFilter;
                mat.TextureFilter = _texel ? BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps : _filterVorher[mat];
            }
        }
        _sonne.ShadowBlur = _stufen ? 0f : 1f;
        _umgebung.Environment.FogEnabled = _nebel;
        AnzeigeAktualisieren();
    }

    private void Sicht(bool nah)
    {
        if (nah) _kamera.Blicken(new Vector3(14, 2.6f, 11), new Vector3(7, 1.6f, 4));
        else _kamera.Blicken(new Vector3(30, 14, 34), new Vector3(0, 2, 0));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Oberfläche, Tasten, Messung
    // ═════════════════════════════════════════════════════════════════════════

    private void OberflaecheBauen()
    {
        var ebene = new CanvasLayer { Layer = 5 };
        AddChild(ebene);
        _anzeige = new Label { Position = new Vector2(14, 10) };
        var hilfe = new Label { Position = new Vector2(14, 0), AnchorTop = 1f, AnchorBottom = 1f, OffsetTop = -40f, OffsetBottom = -10f,
            Text = "1 Übersicht  2 Nah  3 Stufenlicht  4 Umriss  5 Palette  6 Dithering  7 Texel-Raster  8 Punktgröße  9 Nebel  0 aus  Y alles frei  X alles Holzschnitt  K kreisen  H Kamerahilfe  Esc Ende" };
        foreach (var l in new[] { _anzeige, hilfe })
        {
            l.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.85f));
            l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
            l.AddThemeConstantOverride("outline_size", 6);
            l.AddThemeFontSizeOverride("font_size", 16);
            ebene.AddChild(l);
        }
    }

    private void AnzeigeAktualisieren()
    {
        var pal = new[] { "aus", "frei (6 Stufen je Kanal)", "Holzschnitt (21 Farben)" }[_palette];
        var dit = new[] { "aus", "Bildschirm", "Welt (je Texel)" }[_dither];
        _anzeige.Text = $"Pixeltest – Pixel-Look in voller Auflösung\nStufenlicht: {(_stufen ? "an" : "aus")}   Umriss: {(_umriss ? "an" : "aus")}   Palette: {pal}   Dithering: {dit}   " +
                        $"Texel-Raster: {(_texel ? "an" : "aus")}   Punktgröße: {_punkt}   Nebel: {(_nebel ? "an" : "aus")}   Bilder/s: {_fps:F0}";
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } t) return;
        switch (t.Keycode)
        {
            case Key.Key1: Sicht(false); break;
            case Key.Key2: Sicht(true); break;
            case Key.Key3: _stufen = !_stufen; break;
            case Key.Key4: _umriss = !_umriss; break;
            case Key.Key5: _palette = (_palette + 1) % 3; break;
            case Key.Key6: _dither = (_dither + 1) % 3; break;
            case Key.Key7: _texel = !_texel; break;
            case Key.Key8: _punkt = _punkt % 3 + 1; break;
            case Key.Key9: _nebel = !_nebel; break;
            case Key.Key0: _stufen = _umriss = _texel = _nebel = false; _palette = _dither = 0; _punkt = 1; break;
            case Key.Y: Alles(1); break;
            case Key.X: Alles(2); break;
            case Key.K: _kreis = !_kreis; break;
            case Key.Escape: GetTree().Quit(); return;
            default: return;
        }
        Anwenden();
    }

    public override void _Process(double delta)
    {
        _zeit += delta;
        _fps = Engine.GetFramesPerSecond();
        if (_kreis)
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
            _stufen = _umriss = _texel = _nebel = false; _palette = _dither = 0;
            if (_messSchritt == 1) Alles(1);
            if (_messSchritt == 2) Alles(2);
            Anwenden(); _gpu.Clear(); _cpu.Clear();
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
