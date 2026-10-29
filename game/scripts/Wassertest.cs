using System;
using System.Linq;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Testszene für den fremden „Realistic Water Shader“ (UnionBytes/AiYori, MIT; Phase 1 der Wasser-Übernahme).
/// Eigene, kleine Welt: Becken mit Sandbank und Felsen, Sonne, Himmel und eine Wasserfläche – nichts davon hängt am Spiel.
/// Start: <c>--wassertest=1</c>; dazu <c>--wassersicht=nah|fern|schraeg|oben|flach|ufer</c>, <c>--wasserart=keins</c>
/// (ohne Wasser, für den Vergleich der Bildrate).
/// Im Log steht jede Sekunde die Bildrate.
/// </summary>
public partial class Wassertest : Node3D
{
    public const string Szene = "res://scenes/Wassertest.tscn";
    private const string Ordner = "res://assets/texturen/realistisches_wasser/";
    private const string ShaderOrdner = "res://assets/shader/realistisches_wasser/";

    private Camera3D _kamera = null!;
    private double _sek;
    private int _bilder;
    private double _summe;
    private int _messungen;

    public override void _Ready()
    {
        var optionen = OS.GetCmdlineUserArgs()
            .Select(a => a.TrimStart('-').Split('=', 2)).Where(t => t.Length == 2)
            .ToDictionary(t => t[0], t => t[1]);
        var art = optionen.GetValueOrDefault("wasserart", "original");
        var sicht = optionen.GetValueOrDefault("wassersicht", "schraeg");

        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled); // Bildrate messen, nicht die Bildschirmfrequenz
        Umgebung();
        Boden();
        if (art != "keins") Wasser();
        Kamera(sicht);
        GD.Print($"[wassertest] art={art} sicht={sicht} renderer={RenderingServer.GetCurrentRenderingDriverName()}");
    }

    private void Umgebung()
    {
        var himmel = new ProceduralSkyMaterial();
        var umgebung = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = himmel },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        AddChild(new WorldEnvironment { Environment = umgebung });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-48, -35, 0), ShadowEnabled = true, LightEnergy = 1.2f });
    }

    private void Boden()
    {
        var rauschen = new FastNoiseLite { Frequency = 0.04f };
        var grund = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.62f, 0.55f, 0.42f),
            AlbedoTexture = new NoiseTexture2D { Noise = rauschen, Width = 512, Height = 512, Seamless = true },
            Uv1Scale = new Vector3(12, 12, 1),
            Roughness = 0.95f,
        };
        // Becken: Boden, der vom Rand (Land) zur Mitte (tief) absinkt
        var netz = new ArrayMesh();
        const int n = 80; const float halb = 40f;
        var ecken = new Vector3[(n + 1) * (n + 1)];
        var uv = new Vector2[ecken.Length];
        for (var z = 0; z <= n; z++)
            for (var x = 0; x <= n; x++)
            {
                float px = -halb + 2 * halb * x / n, pz = -halb + 2 * halb * z / n;
                var r = new Vector2(px, pz).Length();
                // Mulde bis -3 m in der Mitte, steigt zum Ufer (r≈26) auf +1 m; dazu eine flache Sandbank bei (+8,-6)
                var tiefe = Mathf.Lerp(-3.2f, 1.2f, Mathf.SmoothStep(4f, 27f, r));
                tiefe += 2.6f * Mathf.Exp(-(new Vector2(px - 8, pz + 6).LengthSquared()) / 28f);
                ecken[z * (n + 1) + x] = new Vector3(px, tiefe, pz);
                uv[z * (n + 1) + x] = new Vector2(x, z) / n;
            }
        var idx = new int[n * n * 6];
        var k = 0;
        for (var z = 0; z < n; z++)
            for (var x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x, b = a + 1, c = a + n + 1, d = c + 1;
                idx[k++] = a; idx[k++] = b; idx[k++] = c; idx[k++] = b; idx[k++] = d; idx[k++] = c;
            }
        var feld = new Godot.Collections.Array();
        feld.Resize((int)Mesh.ArrayType.Max);
        feld[(int)Mesh.ArrayType.Vertex] = ecken;
        feld[(int)Mesh.ArrayType.TexUV] = uv;
        feld[(int)Mesh.ArrayType.Index] = idx;
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, feld);
        var st = new SurfaceTool();
        st.CreateFrom(netz, 0);
        st.GenerateNormals();
        var boden = new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = grund };
        AddChild(boden);

        // Felsen, die aus dem Wasser ragen (Schaum am Rand, Tiefe)
        var fels = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.4f, 0.4f), Roughness = 0.9f };
        foreach (var (p, g) in new[] { (new Vector3(-6, -1.5f, 3), 2.2f), (new Vector3(-3, -1.8f, -9), 1.6f), (new Vector3(3, -1.2f, 9), 1.2f) })
            AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = g, Height = g * 2.2f, RadialSegments = 12, Rings = 8 }, Position = p, MaterialOverride = fels });
        // Pfahl und Kiste als Dinge im Wasser (Spiegelung, Brechung)
        var holz = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.3f, 0.18f), Roughness = 0.8f };
        AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 4f }, Position = new Vector3(5, 0.2f, 2), MaterialOverride = holz });
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(2, 1, 1.3f) }, Position = new Vector3(-9, -0.1f, -4), RotationDegrees = new Vector3(0, 25, 4), MaterialOverride = holz });
    }

    private void Wasser()
    {
        var shader = GD.Load<Shader>(ShaderOrdner + "Water_Original.gdshader");
        var m = new ShaderMaterial { Shader = shader };
        m.SetShaderParameter("normalmap_a_sampler", GD.Load<Texture2D>(Ordner + "wasser_normal_a.png"));
        m.SetShaderParameter("normalmap_b_sampler", GD.Load<Texture2D>(Ordner + "wasser_normal_b.png"));
        m.SetShaderParameter("uv_sampler", GD.Load<Texture2D>(Ordner + "wasser_stroemung.png"));
        m.SetShaderParameter("foam_sampler", GD.Load<Texture2D>(Ordner + "wasser_schaum.png"));
        m.SetShaderParameter("caustic_sampler", Kaustik());
        var ebene = new PlaneMesh { Size = new Vector2(80, 80), SubdivideWidth = 200, SubdivideDepth = 200 };
        AddChild(new MeshInstance3D { Mesh = ebene, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Die Kaustik-Datei ist ein 4×4-Atlas aus 256-Pixel-Bildern; der Shader will sie als Textur-Feld mit 16 Lagen.</summary>
    private static Texture2DArray Kaustik()
    {
        var atlas = GD.Load<Texture2D>(Ordner + "wasser_kaustik.png").GetImage();
        atlas.Decompress();
        var lagen = new Godot.Collections.Array<Image>();
        for (var i = 0; i < 16; i++)
            lagen.Add(atlas.GetRegion(new Rect2I(i % 4 * 256, i / 4 * 256, 256, 256)));
        var feld = new Texture2DArray();
        feld.CreateFromImages(lagen);
        return feld;
    }

    private void Kamera(string sicht)
    {
        _kamera = new Camera3D { Fov = 55, Far = 400 };
        AddChild(_kamera);
        var (pos, ziel) = sicht switch
        {
            "nah" => (new Vector3(2, 1.2f, 6), new Vector3(0, 0, -2)),
            "fern" => (new Vector3(0, 28, 70), new Vector3(0, 0, 0)),
            "oben" => (new Vector3(0.01f, 45, 0), new Vector3(0, 0, 0)),
            "flach" => (new Vector3(0, 0.9f, 20), new Vector3(0, 0.2f, 0)),
            "ufer" => (new Vector3(14, 3, -10), new Vector3(8, 0, -6)),
            _ => (new Vector3(12, 9, 22), new Vector3(0, -0.5f, 0)),
        };
        _kamera.Position = pos;
        _kamera.LookAt(ziel, sicht == "oben" ? Vector3.Forward : Vector3.Up);
    }

    public override void _Process(double delta)
    {
        _sek += delta;
        _bilder++;
        if (_sek < 1) return;
        var fps = _bilder / _sek;
        _messungen++;
        if (_messungen > 2) _summe += fps; // die ersten Sekunden (Shader übersetzen) zählen nicht
        GD.Print($"[wassertest] {fps:F1} Bilder/s, {1000 / fps:F2} ms (Mittel ab Sekunde 3: {(_messungen > 2 ? _summe / (_messungen - 2) : 0):F1})");
        _sek = 0; _bilder = 0;
    }
}
