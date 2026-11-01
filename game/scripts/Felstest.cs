using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Testszene „Felsen“ (Felspinsel F2, <c>--felstest=1</c>): ein Hang, der nach Norden (−z) ansteigt, darauf nebeneinander die vier Gesteinsarten aus
/// <c>game/daten/gestein.json</c> (Grauwacke, Trachyt, Buntsandstein, Kalkstein) und rechts ein Steinbruch-Stück (Kalkstein, „frisch gebrochen“
/// mit bewachsener alter Oberkante). Die Felsen sind Platzhalter-Netze, die hier aus Quadern und Kugeln erzeugt werden (gekappt und verbeult,
/// feste Startwerte); die echten Formen kommen mit F1 vom Modell-Agenten. Kein Teil des Spiels: nur das Material ansehen und messen.
/// Startoptionen: <c>--felssicht=uebersicht|grauwacke|trachyt|buntsandstein|kalkstein|steinbruch</c>, <c>--felsstufe=hoch|niedrig</c>,
/// <c>--tageszeit=tag|abend|nacht</c>, <c>--felsviele=&lt;n&gt;</c> (n weitere Brocken zum Messen), <c>--felsmessung=1</c> (Hoch, Niedrig, einfaches Material und ohne
/// Felsen reihum messen, Ergebnis ins Log, dann beenden), <c>--felsende=&lt;Sekunden&gt;</c>. Tasten: 1–5 Gesteinsart, V Übersicht, N Tag/Abend/
/// Nacht, G Grafikstufe Hoch/Niedrig, F Felsen aus/an, B Beschriftung, Esc Ende. Kamera: <see cref="FreieKamera"/> ab Start (Nutzer 30.10.2026).
/// </summary>
public partial class Felstest : Node3D
{
    public const string Szene = "res://scenes/Felstest.tscn";

    /// <summary>Soll das Spiel gleich in die Testszene gehen? <c>--felstest=1</c> oder nur <c>--felstest</c>.</summary>
    public static bool Angefordert =>
        OS.GetCmdlineUserArgs().Any(a => a == "--felstest" || (a.StartsWith("--felstest=") && a != "--felstest=0"));

    private enum Tageszeit { Tag, Abend, Nacht }

    /// <summary>Ein Abschnitt des Hangs: Name, Gesteinsart, Mitte in x, Steinbruch?</summary>
    private sealed record Abschnitt(string Name, string Gestein, float X, bool Steinbruch);

    private static readonly Abschnitt[] Abschnitte =
    {
        new("Grauwacke", "grauwacke", -52f, false),
        new("Trachyt", "trachyt", -26f, false),
        new("Buntsandstein", "buntsandstein", 0f, false),
        new("Kalkstein", "kalkstein", 26f, false),
        new("Steinbruch (Kalkstein, frisch gebrochen)", "kalkstein", 52f, true),
    };

    // Hang: Talboden y = 0 südlich von z = TalKante, dann steigt er mit HangNeigung nach Norden bis z = −Halbtiefe
    private const float Halbbreite = 70f, Halbtiefe = 40f, TalKante = 12f, HangNeigung = 0.42f; // ≈ 23°

    private readonly Dictionary<string, string> _optionen = new();
    private readonly List<ShaderMaterial> _stoffe = new();
    private readonly List<Label3D> _beschriftungen = new();
    private Node3D _felsen = null!;
    private bool _hoch = true, _felsenAn = true, _beschriftung = true;
    private Tageszeit _zeit = Tageszeit.Tag;
    private int _abschnitt = -1;

    private WorldEnvironment _umgebung = null!;
    private ProceduralSkyMaterial _himmel = null!;
    private DirectionalLight3D _sonne = null!;
    private FreieKamera _kamera = null!;
    private Label _anzeige = null!;

    // Messung
    private double _messZeit, _messCpu, _messGpu, _ende, _laufzeit;
    private int _messBilder;
    private readonly List<(string Name, Action Setzen)> _messfolge = new();
    private readonly Dictionary<string, List<double>> _messErgebnis = new();
    private int _messNr = -1;
    private double _messSchritt;
    private int _messSekunde;
    private double _gpuLetzte, _fpsLetzte;

    public override void _Ready()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var t = arg.TrimStart('-').Split('=', 2);
            if (t.Length == 2) _optionen[t[0]] = t[1];
        }
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled); // Bildrate messen, nicht die Bildschirmfrequenz
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        _hoch = _optionen.GetValueOrDefault("felsstufe", "hoch") != "niedrig";
        _zeit = _optionen.GetValueOrDefault("tageszeit", "tag") switch { "abend" => Tageszeit.Abend, "nacht" => Tageszeit.Nacht, _ => Tageszeit.Tag };
        double.TryParse(_optionen.GetValueOrDefault("felsende", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out _ende);

        UmgebungBauen();
        AddChild(new MeshInstance3D { Mesh = HangNetz(), MaterialOverride = HangStoff(), Name = "Hang" });
        _felsen = new Node3D { Name = "Felsen" };
        AddChild(_felsen);
        for (var i = 0; i < Abschnitte.Length; i++) AbschnittBauen(i);
        if (int.TryParse(_optionen.GetValueOrDefault("felsviele", "0"), out var viele) && viele > 0) VieleBrocken(viele);

        _kamera = FreieKamera.Einhaengen(this, new Vector3(0, 40, 70), new Vector3(0, 6, -6));
        _kamera.Bodenhoehe = Hoehe;
        _kamera.MindestHoehe = 0.4f;
        var sicht = _optionen.GetValueOrDefault("felssicht", "uebersicht");
        var nr = Array.FindIndex(Abschnitte, a => a.Gestein == sicht && !a.Steinbruch);
        if (sicht == "steinbruch") nr = 4;
        SichtSetzen(nr);

        OberflaecheBauen();
        TageszeitSetzen();
        if (_optionen.GetValueOrDefault("felsmessung") == "1") MessungVorbereiten();
        GD.Print($"[felstest] sicht={sicht} stufe={(_hoch ? "hoch" : "niedrig")} zeit={_zeit} stoffe={_stoffe.Count} stuecke={_felsen.GetChildCount()} " +
                 $"renderer={RenderingServer.GetCurrentRenderingDriverName()}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Hang und Umgebung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Höhe des Hangs (m) an (x, z): Talboden im Süden, nach Norden ansteigend, mit sanften Wellen.</summary>
    private static float Hoehe(float x, float z)
    {
        var y = HangHoehe(x, z);
        // Steinbruch: ein ebener Boden in den Hang geschnitten (x 44…60, z −14…6), weicher Rand von 1,5 m; die Abbaustufen füllen die Rückwand
        var innen = Mathf.Min(Mathf.Min(x - BruchX0, BruchX1 - x), Mathf.Min(z - BruchZ0, BruchZ1 - z));
        var anteil = Mathf.Clamp(innen / 1.5f + 0.5f, 0f, 1f);
        return Mathf.Lerp(y, Mathf.Min(y, BruchBoden), anteil);
    }

    private const float BruchX0 = 43.5f, BruchX1 = 60.5f, BruchZ0 = -15f, BruchZ1 = 6f;
    private static readonly float BruchBoden = HangHoehe(52f, BruchZ1);

    private static float HangHoehe(float x, float z)
    {
        var anstieg = Mathf.Max(0f, TalKante - z);
        var weich = anstieg < 4f ? anstieg * anstieg / 8f : anstieg - 2f;   // weicher Knick am Hangfuß
        var wellen = 0.6f * Mathf.Sin(x * 0.11f) * Mathf.Sin(z * 0.09f + 1f) + 0.3f * Mathf.Sin(x * 0.31f + z * 0.17f);
        return weich * HangNeigung + wellen * Mathf.Clamp(anstieg / 6f, 0.2f, 1f);
    }

    private static Mesh HangNetz()
    {
        const float schritt = 1f;
        var nx = (int)(Halbbreite * 2 / schritt);
        var nz = (int)(Halbtiefe * 2 / schritt);
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var gras = new Color(0.33f, 0.40f, 0.20f);
        var trocken = new Color(0.46f, 0.44f, 0.27f);
        var erde = new Color(0.36f, 0.30f, 0.22f);
        for (var j = 0; j <= nz; j++)
            for (var i = 0; i <= nx; i++)
            {
                float x = -Halbbreite + i * schritt, z = -Halbtiefe + j * schritt;
                var y = Hoehe(x, z);
                var flecken = 0.5f + 0.5f * Mathf.Sin(x * 0.37f + Mathf.Sin(z * 0.23f) * 2f) * Mathf.Cos(z * 0.29f - x * 0.07f);
                var farbe = gras.Lerp(trocken, flecken * 0.6f);
                // vor dem Steinbruch festgefahrener Erdboden
                var bruch = Mathf.Clamp(Mathf.Min(Mathf.Min(x - BruchX0, BruchX1 - x), Mathf.Min(z - BruchZ0, BruchZ1 + 3f - z)) / 2f + 0.5f, 0f, 1f);
                farbe = farbe.Lerp(erde, bruch);
                st.SetColor(farbe);
                st.SetUV(new Vector2(x, z) / 4f);
                st.AddVertex(new Vector3(x, y, z));
            }
        for (var j = 0; j < nz; j++)
            for (var i = 0; i < nx; i++)
            {
                var a = j * (nx + 1) + i;
                var b = a + 1;
                var c = a + nx + 1;
                var d = c + 1;
                st.AddIndex(a); st.AddIndex(b); st.AddIndex(c);
                st.AddIndex(b); st.AddIndex(d); st.AddIndex(c);
            }
        st.GenerateNormals();
        return st.Commit();
    }

    private static Material HangStoff()
    {
        var rauschen = new NoiseTexture2D
        {
            Width = 256, Height = 256, Seamless = true,
            Noise = new FastNoiseLite { Frequency = 0.04f, FractalOctaves = 4, Seed = 3 },
            ColorRamp = new Gradient { Colors = new[] { new Color(0.72f, 0.72f, 0.72f), new Color(1.12f, 1.12f, 1.12f) } },
        };
        return new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, AlbedoTexture = rauschen, Roughness = 1f,
        };
    }

    private void UmgebungBauen()
    {
        _himmel = new ProceduralSkyMaterial();
        _umgebung = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky { SkyMaterial = _himmel },
                // wie die Stadtansicht: Grundlicht als Farbe (nicht vom Himmel, der färbt die Schattenseiten blau), AgX
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
                TonemapMode = Godot.Environment.ToneMapper.Agx,
                SsaoEnabled = true, SsaoRadius = 1.2f, SsaoIntensity = 1.5f,
            },
        };
        AddChild(_umgebung);
        _sonne = new DirectionalLight3D { ShadowEnabled = true, DirectionalShadowMaxDistance = 160f };
        AddChild(_sonne);
    }

    private void TageszeitSetzen()
    {
        switch (_zeit)
        {
            case Tageszeit.Tag:
                _sonne.RotationDegrees = new Vector3(-50, 150, 0);   // Sonne aus Südsüdost, hoch
                _sonne.LightColor = new Color(1f, 0.97f, 0.9f);
                _sonne.LightEnergy = 1.25f;
                _himmel.SkyTopColor = new Color(0.36f, 0.52f, 0.78f);
                _himmel.SkyHorizonColor = new Color(0.66f, 0.73f, 0.82f);
                _himmel.GroundHorizonColor = new Color(0.55f, 0.58f, 0.52f);
                _himmel.GroundBottomColor = new Color(0.22f, 0.24f, 0.2f);
                _umgebung.Environment.AmbientLightColor = new Color(0.75f, 0.75f, 0.80f);
                _umgebung.Environment.AmbientLightEnergy = 0.5f;
                break;
            case Tageszeit.Abend:
                _sonne.RotationDegrees = new Vector3(-12, 245, 0);   // tiefe Sonne aus Westen
                _sonne.LightColor = new Color(1f, 0.68f, 0.42f);
                _sonne.LightEnergy = 1.0f;
                _himmel.SkyTopColor = new Color(0.30f, 0.36f, 0.56f);
                _himmel.SkyHorizonColor = new Color(0.86f, 0.58f, 0.40f);
                _himmel.GroundHorizonColor = new Color(0.52f, 0.40f, 0.32f);
                _himmel.GroundBottomColor = new Color(0.16f, 0.14f, 0.12f);
                _umgebung.Environment.AmbientLightColor = new Color(0.72f, 0.58f, 0.52f);
                _umgebung.Environment.AmbientLightEnergy = 0.35f;
                break;
            default:
                _sonne.RotationDegrees = new Vector3(-35, 200, 0);   // Mond
                _sonne.LightColor = new Color(0.62f, 0.72f, 1f);
                _sonne.LightEnergy = 0.16f;
                _himmel.SkyTopColor = new Color(0.02f, 0.03f, 0.07f);
                _himmel.SkyHorizonColor = new Color(0.06f, 0.08f, 0.13f);
                _himmel.GroundHorizonColor = new Color(0.04f, 0.05f, 0.07f);
                _himmel.GroundBottomColor = new Color(0.01f, 0.01f, 0.02f);
                _umgebung.Environment.AmbientLightColor = new Color(0.36f, 0.42f, 0.60f);
                _umgebung.Environment.AmbientLightEnergy = 0.22f;
                break;
        }
        AnzeigeAktualisieren();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Felsen (Platzhalter-Netze)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ein Platzhalter-Fels: ein unterteilter Quader oder eine Kugel, mit <paramref name="schnitte"/> zufälligen Ebenen gekappt (Bruchflächen) und mit
    /// Rauschen verbeult. <paramref name="rund"/> 0 kantig (nur die Schnittflächen) bis 1 rund (zur Kugel hin gezogen); die Normalen sind immer weich.
    /// Fester Startwert: auf jedem Rechner dasselbe Netz.
    /// </summary>
    private static Mesh FelsNetz(Vector3 groesse, int saat, float rund, int schnitte, float beulen = 0.08f)
    {
        var grund = new BoxMesh { Size = Vector3.One, SubdivideWidth = 7, SubdivideHeight = 7, SubdivideDepth = 7 };
        var arrays = grund.SurfaceGetArrays(0);
        var ecken = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var indizes = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        var zufall = new RandomNumberGenerator { Seed = (ulong)saat };
        var ebenen = new List<(Vector3 N, float D)>();
        for (var i = 0; i < schnitte; i++)
        {
            var n = new Vector3(zufall.RandfRange(-1, 1), zufall.RandfRange(-0.6f, 1), zufall.RandfRange(-1, 1)).Normalized();
            ebenen.Add((n, zufall.RandfRange(0.30f, 0.45f)));
        }
        var rauschen = new FastNoiseLite { Seed = saat, Frequency = 1.6f, FractalOctaves = 3 };
        var klein = Mathf.Min(groesse.X, Mathf.Min(groesse.Y, groesse.Z));
        for (var i = 0; i < ecken.Length; i++)
        {
            var v = ecken[i];
            // zur Kugel hin runden (gleiche Lage = gleiche Verschiebung, die Kanten des Quaders reißen nicht auf)
            v = v.Lerp(v.Normalized() * 0.55f, rund * 0.8f);
            foreach (var (n, d) in ebenen)
            {
                var ueber = v.Dot(n) - d;
                if (ueber > 0) v -= n * ueber;
            }
            v *= groesse;
            v += v.Normalized() * rauschen.GetNoise3Dv(v * 0.8f) * klein * beulen;
            ecken[i] = v;
        }
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var idx in indizes) st.AddVertex(ecken[idx]);
        // gleiche Ecken zusammenlegen → weiche Normalen; die Kanten der Schnitte bleiben als schmale Fase sichtbar (eine Reihe der Unterteilung)
        st.Index();
        st.GenerateNormals();
        return st.Commit();
    }

    private ShaderMaterial StoffFuer(Gestein g, bool frisch = false, float frischBis = 1000f)
    {
        var m = Gesteinsarten.Material(g, _hoch, frisch, frischBis);
        _stoffe.Add(m);
        return m;
    }

    /// <summary>Setzt ein Stück so in den Hang, dass es um <paramref name="einsinken"/> seiner Höhe im Boden steckt.</summary>
    private MeshInstance3D Stueck(Mesh netz, Material stoff, float x, float z, float hoehe, float einsinken, Vector3 drehungGrad)
    {
        // die tiefste Stelle unter dem Stück (Hang), damit keine Unterkante schwebt; Radius nach Größe
        var r = Mathf.Clamp(hoehe * 0.5f, 0.1f, 1f);
        var y = Mathf.Min(Mathf.Min(Hoehe(x - r, z), Hoehe(x + r, z)), Mathf.Min(Hoehe(x, z - r), Hoehe(x, z + r)));
        var mi = new MeshInstance3D
        {
            Mesh = netz, MaterialOverride = stoff,
            Position = new Vector3(x, y + hoehe * (0.5f - einsinken), z),
            RotationDegrees = drehungGrad,
        };
        _felsen.AddChild(mi);
        return mi;
    }

    private void Schutt(Material stoff, float x, float z, float breite, float tiefe, int anzahl, int saat, float rund, float groesse = 0.45f)
    {
        var r = new RandomNumberGenerator { Seed = (ulong)saat };
        for (var i = 0; i < anzahl; i++)
        {
            var s = groesse * r.RandfRange(0.35f, 1f);
            var g = new Vector3(s * r.RandfRange(0.8f, 1.4f), s * r.RandfRange(0.4f, 0.8f), s * r.RandfRange(0.8f, 1.3f));
            var netz = FelsNetz(g, saat * 31 + i, rund, rund < 0.5f ? 5 : 2, 0.05f);
            Stueck(netz, stoff, x + r.RandfRange(-breite, breite), z + r.RandfRange(0, tiefe), g.Y, 0.35f,
                new Vector3(r.RandfRange(-8, 8), r.RandfRange(0, 360), r.RandfRange(-8, 8)));
        }
    }

    private void AbschnittBauen(int nr)
    {
        var a = Abschnitte[nr];
        var g = Gesteinsarten.Nach(a.Gestein);
        var x0 = a.X;
        var z0 = -6f;   // mitten am Hang
        float labelHoehe;
        switch (a.Gestein)
        {
            case "grauwacke" when !a.Steinbruch:
            {
                // schräg gestellte Rippen, hangabwärts gestreckt, darunter Schutt aus flachen Platten
                var stoff = StoffFuer(g);
                for (var i = 0; i < 5; i++)
                {
                    var gr = new Vector3(1.4f + 0.3f * (i % 2), 2.4f + 0.5f * ((i * 7) % 3), 5.5f + i % 3);
                    Stueck(FelsNetz(gr, 100 + i, 0.05f, 10), stoff, x0 - 8f + i * 4f, z0 - 2f + (i % 2) * 2.5f, gr.Y, 0.3f, new Vector3(-12, 8 - i * 4, 28));
                }
                Stueck(FelsNetz(new Vector3(3f, 2.2f, 2.6f), 110, 0.05f, 10), stoff, x0 + 2f, z0 + 6f, 2.2f, 0.4f, new Vector3(0, 30, 20));
                Schutt(stoff, x0, z0 + 3f, 9f, 7f, 34, 120, 0.0f, 0.5f);
                labelHoehe = 5f;
                break;
            }
            case "trachyt":
            {
                // Pfeiler und Quader nebeneinander, eine Wand mit grobem Blockwerk am Fuß
                var stoff = StoffFuer(g);
                for (var i = 0; i < 6; i++)
                {
                    var h = 4.5f + 2.2f * ((i * 5) % 3) / 2f;
                    var gr = new Vector3(2.0f + 0.4f * (i % 2), h, 2.2f);
                    Stueck(FelsNetz(gr, 200 + i, 0.2f, 7, 0.06f), stoff, x0 - 7.5f + i * 2.6f, z0 - 3f + (i % 3) * 0.8f, h, 0.2f, new Vector3(0, i * 9, 0));
                }
                Stueck(FelsNetz(new Vector3(3.2f, 2.6f, 2.8f), 210, 0.25f, 8), stoff, x0 + 4f, z0 + 4f, 2.6f, 0.35f, new Vector3(4, 25, -6));
                Schutt(stoff, x0, z0 + 1.5f, 9f, 7f, 22, 220, 0.2f, 0.8f);
                labelHoehe = 8f;
                break;
            }
            case "buntsandstein":
            {
                // dicke, gerundete Bänke: zwei Lagen Blöcke übereinander, eine Kuppe, roter Schutt
                var stoff = StoffFuer(g);
                for (var i = 0; i < 4; i++)
                {
                    var gr = new Vector3(3.6f, 2.0f, 3.0f);
                    Stueck(FelsNetz(gr, 300 + i, 0.6f, 4, 0.1f), stoff, x0 - 7f + i * 4.2f, z0 - 2f, 2.0f, 0.25f, new Vector3(0, i * 13, 0));
                    if (i % 2 == 0)
                        Stueck(FelsNetz(new Vector3(3.2f, 1.7f, 2.6f), 310 + i, 0.6f, 4, 0.1f), stoff, x0 - 6.5f + i * 4.2f, z0 - 2.6f, 1.7f, 0f, new Vector3(0, i * 21 + 5, 0))
                            .Position += new Vector3(0, 1.0f, 0);   // zweite Bank auf der ersten (steckt etwas in ihr)
                }
                Stueck(FelsNetz(new Vector3(4.5f, 2.6f, 3.8f), 320, 0.9f, 2, 0.1f), stoff, x0 + 7f, z0 + 3f, 2.6f, 0.45f, new Vector3(0, 40, 0));
                Schutt(stoff, x0, z0 + 2f, 9f, 6f, 20, 330, 0.7f, 0.6f);
                labelHoehe = 7f;
                break;
            }
            case "kalkstein" when !a.Steinbruch:
            {
                // gebankte Blöcke und ein Wandstück
                var stoff = StoffFuer(g);
                Stueck(FelsNetz(new Vector3(9f, 3.2f, 2.4f), 400, 0.15f, 8, 0.06f), stoff, x0 - 2f, z0 - 3f, 3.2f, 0.25f, new Vector3(0, 6, 0));
                for (var i = 0; i < 3; i++)
                {
                    var gr = new Vector3(2.6f, 1.8f + 0.4f * i, 2.2f);
                    Stueck(FelsNetz(gr, 410 + i, 0.25f, 8), stoff, x0 - 6f + i * 5.5f, z0 + 2.5f + (i % 2), gr.Y, 0.35f, new Vector3(0, 20 + i * 35, 0));
                }
                Schutt(stoff, x0, z0 + 2f, 9f, 6f, 24, 420, 0.25f, 0.55f);
                labelHoehe = 6f;
                break;
            }
            default:
                labelHoehe = SteinbruchBauen(g, x0);
                break;
        }
        var beschriftung = new Label3D
        {
            Text = a.Name, FontSize = 64, PixelSize = 0.02f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Position = new Vector3(x0, Hoehe(x0, z0) + labelHoehe, z0), OutlineSize = 12, NoDepthTest = true,
        };
        _beschriftungen.Add(beschriftung);
        AddChild(beschriftung);
    }

    /// <summary>
    /// Steinbruch-Stück: drei Abbaustufen in den Hang geschnitten (frische, glatte Wände ohne Bewuchs), darüber die alte Oberkante mit Moos
    /// (frisch_bis), davor Abraum und gebrochene Quader. Gibt die Höhe der Beschriftung zurück.
    /// </summary>
    private float SteinbruchBauen(Gestein g, float x0)
    {
        const float z0 = -2f;
        var boden = BruchBoden;
        // Die alte Oberkante liegt 0,5 m unter der Krone der obersten Stufe: darüber Bewuchs, darunter frisch
        var stufen = new[] { (Hoehe: 3.2f, Tiefe: 0f), (Hoehe: 6.2f, Tiefe: -4f), (Hoehe: 9.0f, Tiefe: -8f) };
        var krone = boden + stufen[^1].Hoehe;
        var stoff = StoffFuer(g, frisch: true, frischBis: krone - 0.5f);
        for (var i = 0; i < stufen.Length; i++)
        {
            var (h, t) = stufen[i];
            var tiefe = i == stufen.Length - 1 ? 8f : 4.4f;   // die oberste reicht bis in den Hang hinter dem Bruch
            var netz = FelsNetz(new Vector3(16f - i * 1.5f, h, tiefe), 500 + i, 0.0f, 0, 0.025f);   // gerade Abbauwände
            var mi = new MeshInstance3D
            {
                Mesh = netz, MaterialOverride = stoff,
                Position = new Vector3(x0, boden + h * 0.5f - 0.02f, z0 + t - tiefe * 0.5f),
            };
            _felsen.AddChild(mi);
        }
        // gebrochene Quader und Abraum vor der Wand
        var quader = Gesteinsarten.Material(g, _hoch, frisch: true);
        _stoffe.Add(quader);
        for (var i = 0; i < 6; i++)
        {
            var gr = new Vector3(1.6f, 0.7f, 0.8f);
            var mi = new MeshInstance3D
            {
                Mesh = FelsNetz(gr, 520 + i, 0f, 0, 0.01f), MaterialOverride = quader,
                Position = new Vector3(x0 - 5f + (i % 3) * 1.8f, boden + 0.35f + (i / 3) * 0.7f, z0 + 4.5f), RotationDegrees = new Vector3(0, (i % 2) * 4, 0),
            };
            _felsen.AddChild(mi);
        }
        Schutt(quader, x0 + 4f, z0 + 2f, 3.5f, 4f, 40, 530, 0f, 0.4f);
        return 11f;
    }

    /// <summary>Messung: <paramref name="anzahl"/> weitere Brocken über den ganzen Hang (für F5: so viele Stücke kosten so viel).</summary>
    private void VieleBrocken(int anzahl)
    {
        var r = new RandomNumberGenerator { Seed = 909 };
        var netze = Enumerable.Range(0, 8).Select(i => FelsNetz(new Vector3(1.6f, 1.1f, 1.4f), 900 + i, i % 2 == 0 ? 0.1f : 0.7f, 7)).ToArray();
        var stoffe = Gesteinsarten.Alle.Select(g => StoffFuer(g)).ToArray();
        for (var i = 0; i < anzahl; i++)
        {
            var x = r.RandfRange(-Halbbreite + 4, Halbbreite - 4);
            var z = r.RandfRange(-Halbtiefe + 4, -16f);
            var s = r.RandfRange(0.6f, 1.6f);
            var mi = Stueck(netze[i % netze.Length], stoffe[i % stoffe.Length], x, z, 1.1f * s, 0.3f, new Vector3(r.RandfRange(-8, 8), r.RandfRange(0, 360), 0));
            mi.Scale = Vector3.One * s;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kamera, Oberfläche, Eingabe
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Setzt die freie Kamera auf einen Abschnitt (0–4) oder mit −1 auf die Übersicht; danach fliegt man frei weiter.</summary>
    private void SichtSetzen(int nr)
    {
        _abschnitt = nr;
        if (nr < 0)
        {
            // wie die Stadtkamera: etwa 40° von oben, ganzer Hang im Bild
            _kamera.Blicken(new Vector3(0, 46, 62), new Vector3(0, 4, -6));
        }
        else
        {
            var a = Abschnitte[nr];
            var ziel = new Vector3(a.X, Hoehe(a.X, -6f) + 2f, -6f);
            _kamera.Blicken(ziel + new Vector3(-2f, 5f, 13f), ziel);
        }
        AnzeigeAktualisieren();
    }

    private void OberflaecheBauen()
    {
        var ebene = new CanvasLayer { Layer = 5 };
        AddChild(ebene);
        _anzeige = new Label { Position = new Vector2(14, 10) };
        var hilfe = new Label { Position = new Vector2(14, 0), AnchorTop = 1f, AnchorBottom = 1f, OffsetTop = -40f, OffsetBottom = -10f };
        foreach (var l in new[] { _anzeige, hilfe })
        {
            l.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.85f));
            l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
            l.AddThemeConstantOverride("outline_size", 6);
            l.AddThemeFontSizeOverride("font_size", 16);
            ebene.AddChild(l);
        }
        hilfe.Text = "1–5: Gesteinsart   V: Übersicht   N: Tag/Abend/Nacht   G: Grafikstufe Hoch/Niedrig   F: Felsen aus/an   B: Beschriftung   Esc: Ende";
        AnzeigeAktualisieren();
    }

    private void AnzeigeAktualisieren()
    {
        if (_anzeige is null) return;
        var wo = _abschnitt < 0 ? "Übersicht" : $"{_abschnitt + 1}: {Abschnitte[_abschnitt].Name}";
        var zeit = _zeit switch { Tageszeit.Tag => "Tag", Tageszeit.Abend => "Abend", _ => "Nacht" };
        _anzeige.Text = $"Felstest – Fels-Material mit Gesteinsvorlagen (Felspinsel F2), Felsen sind Platzhalter\n" +
                        $"{wo}   {zeit}   Grafikstufe: {(_hoch ? "Hoch (dreiseitig)" : "Niedrig (nur von oben)")}   Felsen: {(_felsenAn ? "an" : "aus")}   " +
                        $"Bilder/s: {_fpsLetzte:F0}   Grafikkarte: {_gpuLetzte:F2} ms";
    }

    private void StufeSetzen(bool hoch)
    {
        _hoch = hoch;
        foreach (var m in _stoffe) Gesteinsarten.StufeSetzen(m, hoch);
        AnzeigeAktualisieren();
    }

    private void FelsenSetzen(bool an)
    {
        _felsenAn = an;
        _felsen.Visible = an;
        AnzeigeAktualisieren();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        switch (k.Keycode)
        {
            case >= Key.Key1 and <= Key.Key5: SichtSetzen((int)k.Keycode - (int)Key.Key1); break;
            case Key.V: SichtSetzen(-1); break;
            case Key.N: _zeit = (Tageszeit)(((int)_zeit + 1) % 3); TageszeitSetzen(); break;
            case Key.G: StufeSetzen(!_hoch); break;
            case Key.F: FelsenSetzen(!_felsenAn); break;
            case Key.B:
                _beschriftung = !_beschriftung;
                foreach (var l in _beschriftungen) l.Visible = _beschriftung;
                break;
            case Key.Escape: GetTree().Quit(); break;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Messung
    // ═════════════════════════════════════════════════════════════════════════

    private void MessungVorbereiten()
    {
        foreach (var l in _beschriftungen) l.Visible = false;
        // „einfach“: dieselben Felsen mit einem StandardMaterial3D – der Unterschied zu „hoch“ ist der Preis des Fels-Shaders allein
        var einfach = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.55f, 0.52f), Roughness = 0.9f };
        var eigene = _felsen.GetChildren().OfType<MeshInstance3D>().Select(m => (m, m.MaterialOverride)).ToList();
        void Stoffe(bool fels) { foreach (var (m, stoff) in eigene) m.MaterialOverride = fels ? stoff : einfach; }
        _messfolge.Add(("hoch", () => { Stoffe(true); FelsenSetzen(true); StufeSetzen(true); }));
        _messfolge.Add(("niedrig", () => { Stoffe(true); FelsenSetzen(true); StufeSetzen(false); }));
        _messfolge.Add(("einfach", () => { Stoffe(false); FelsenSetzen(true); }));
        _messfolge.Add(("ohne", () => { Stoffe(true); FelsenSetzen(false); }));
        _messNr = 0;
        _messfolge[0].Setzen();
    }

    public override void _Process(double delta)
    {
        _laufzeit += delta;
        var kasten = GetViewport().GetViewportRid();
        _messCpu += RenderingServer.ViewportGetMeasuredRenderTimeCpu(kasten);
        _messGpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(kasten);
        _messZeit += delta;
        _messBilder++;
        if (_messZeit < 1.0) return;
        var fps = _messBilder / _messZeit;
        var gpu = _messGpu / _messBilder;
        var cpu = _messCpu / _messBilder;
        var r = RenderingServer.GetRenderingInfo;
        var zustand = _messNr >= 0 ? _messfolge[_messNr].Name : (_hoch ? "hoch" : "niedrig");
        GD.Print($"[felstest] fps={fps:F1} ms={_messZeit / _messBilder * 1000:F2} gpu={gpu:F2} cpu={cpu:F2} zustand={zustand} felsen={_felsenAn} " +
                 $"aufrufe={r(RenderingServer.RenderingInfo.TotalDrawCallsInFrame)} dreiecke={r(RenderingServer.RenderingInfo.TotalPrimitivesInFrame)}");
        _fpsLetzte = fps;
        _gpuLetzte = gpu;
        AnzeigeAktualisieren();
        _messZeit = _messCpu = _messGpu = 0;
        _messBilder = 0;

        if (_messNr >= 0)
        {
            // je Zustand 8 Sekunden, die ersten 2 nicht zählen (Shader übersetzen, Schatten einschwingen); zwei Runden
            _messSekunde++;
            if (_messSekunde > 2)
            {
                if (!_messErgebnis.TryGetValue(zustand, out var liste)) _messErgebnis[zustand] = liste = new List<double>();
                liste.Add(gpu);
            }
            if (_messSekunde >= 8)
            {
                _messSekunde = 0;
                _messSchritt++;
                if (_messSchritt >= _messfolge.Count * 2)
                {
                    foreach (var (name, werte) in _messErgebnis)
                        GD.Print($"[messung] {name}: gpu {werte.Average():F2} ms (min {werte.Min():F2}, max {werte.Max():F2}, n {werte.Count})");
                    GetTree().Quit();
                    return;
                }
                _messNr = (int)(_messSchritt % _messfolge.Count);
                _messfolge[_messNr].Setzen();
            }
        }
        if (_ende > 0 && _laufzeit >= _ende) GetTree().Quit();
    }
}
