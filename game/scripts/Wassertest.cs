using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Testszene für das realistische Wasser (Phase 2, <c>docs/wasser/phase2.md</c>): kleine Welt ohne Verbindung zum Spiel, je Gewässerart ein Feld
/// von 60 × 60 m mit Ufer und Hindernissen, Sonne, Himmel und <c>wasser_realistisch.gdshader</c>. Start: <c>--wassertest=1</c>, dazu
/// <c>--wasserart=alle|fluss|bach|teich|see|meer|hafen|furt|keins</c>, <c>--wassersicht=schraeg|nah|fern|oben|flach|ufer|uebersicht</c>,
/// <c>--wetter=klar|wolkig|regen|sturm|gewitter|schnee|nebel</c>, <c>--jahreszeit=fruehling|sommer|herbst|winter</c>, <c>--winter=0..1</c>
/// (Eisfortschritt; sonst im Winter 1, sonst 0), <c>--wasserteile=-brechung,-tiefe,-kaustik,-schaum,-eis,-wind,-ringe</c> (Teile abschalten, zur
/// Messung), <c>--wasserstufe=niedrig|mittel|hoch|ultra</c> (Shader der Grafikstufe), <c>--wasservorwaermen=1</c> (<see cref="Wasserarten.Vorwaermen"/>
/// prüfen, Zeit im Log) und <c>--wasserende=&lt;Sekunden&gt;</c> (nach der Zeit Mittelwert ins Log und beenden). Im Log steht jede Sekunde die Bildrate.
/// </summary>
public partial class Wassertest : Node3D
{
    public const string Szene = "res://scenes/Wassertest.tscn";
    private const string ShaderPfad = "res://assets/shader/wasser_realistisch.gdshader";
    private const float Zelle = 60f;

    private readonly Dictionary<string, string> _optionen = new();
    private double _sek, _gesamt;
    private int _bilder, _messungen;
    private double _summe;
    private double _ende;
    private Shader _shader = null!;
    private readonly List<(Wasserart Art, MeshInstance3D Netz, ShaderMaterial Stoff)> _wasser = new();
    private List<(string Name, Action Setzen)>? _messfolge;
    private int _messNr, _messRunde;
    private double _messZeit;
    private readonly List<double> _messRahmen = new();
    private readonly Dictionary<string, List<double>> _messErgebnis = new();

    public override void _Ready()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var t = arg.TrimStart('-').Split('=', 2);
            if (t.Length == 2) _optionen[t[0]] = t[1];
        }
        var artName = _optionen.GetValueOrDefault("wasserart", "alle");
        var sicht = _optionen.GetValueOrDefault("wassersicht", artName == "alle" ? "uebersicht" : "schraeg");
        double.TryParse(_optionen.GetValueOrDefault("wasserende", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out _ende);

        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled); // Bildrate messen, nicht die Bildschirmfrequenz

        var lage = Enum.TryParse<Wetterlage>(_optionen.GetValueOrDefault("wetter", "klar"), true, out var l) ? l : Wetterlage.Klar;
        var jahreszeit = Enum.TryParse<Jahreszeit>(_optionen.GetValueOrDefault("jahreszeit", "sommer"), true, out var j) ? j : Jahreszeit.Sommer;
        var winter = float.TryParse(_optionen.GetValueOrDefault("winter", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
            ? Mathf.Clamp(w, 0f, 1f) : (jahreszeit == Jahreszeit.Winter ? 1f : 0f);
        var schneeDecke = jahreszeit == Jahreszeit.Winter ? Mathf.Max(0.6f, Wetterwerte.Von(lage).Schnee) : Wetterwerte.Von(lage).Schnee * 0.4f;
        Wetter(lage, schneeDecke);
        Umgebung(lage);

        if (_optionen.GetValueOrDefault("wasservorwaermen") == "1") Wasserarten.Vorwaermen(this, alleStufen: true); // Zeit steht im Log
        _shader = ShaderLaden();
        var arten = artName == "alle" ? Wasserarten.Alle.ToList()
            : artName == "keins" ? new List<Wasserart>()
            : Wasserarten.Nach(artName) is { } a ? new List<Wasserart> { a } : new List<Wasserart> { Wasserarten.Fluss };
        for (var i = 0; i < arten.Count; i++)
        {
            var mitte = artName == "alle" ? new Vector3((i % 4 - 1.5f) * (Zelle + 4), 0, (i / 4 - 0.5f) * (Zelle + 4)) : Vector3.Zero;
            Feld(arten[i], mitte, winter, schneeDecke);
        }
        var massstab = artName == "alle" ? 1f : arten.Count == 1 ? Massstab(arten[0]) : 1f;
        Kamera(sicht, artName == "alle", massstab, arten.Count == 1 && arten[0] == Wasserarten.Meer);
        if (_optionen.TryGetValue("wassermessung", out var messung)) MessungVorbereiten(messung);
        GD.Print($"[wassertest] art={artName} sicht={sicht} wetter={lage} jahreszeit={jahreszeit} winter={winter:F2} renderer={RenderingServer.GetCurrentRenderingDriverName()}");
    }

    private static float Massstab(Wasserart a) => a.Name switch
    {
        "Bach" => 0.3f, "Furt" => 0.6f, "Teich" => 0.8f, "Hafenbecken" => 0.9f, "Fluss" => 1.1f, "See" => 1.5f, "Meer" => 1.3f, _ => 1f,
    };

    /// <summary>
    /// Lädt den Shader (mit <c>--wasserstufe=niedrig|mittel|hoch|ultra</c> den der Stufe, sonst den vollen); <c>--wasserteile=-kaustik,…</c> schaltet
    /// dessen #define-Zeilen ab (nur zum Messen).
    /// </summary>
    private Shader ShaderLaden()
    {
        var shader = Wasserarten.StufeVorgabe is { } stufe ? Wasserarten.ShaderFuer(stufe) : GD.Load<Shader>(ShaderPfad);
        var teile = _optionen.GetValueOrDefault("wasserteile", "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (teile.Length == 0) return shader;
        var code = shader.Code;
        foreach (var teil in teile.Where(t => t.StartsWith('-')))
            code = code.Replace("#define MIT_" + teil[1..].ToUpperInvariant(), "// aus: MIT_" + teil[1..].ToUpperInvariant());
        return new Shader { Code = code };
    }

    // ── Wetter und Himmel ──

    private static void Wetter(Wetterlage lage, float schnee)
    {
        var w = Wetterwerte.Von(lage);
        var wind = new Vector2(0.8f, 0.6f).Normalized();
        RenderingServer.GlobalShaderParameterSet("wetter_wind", w.Wind);
        RenderingServer.GlobalShaderParameterSet("wind_richtung", wind);
        RenderingServer.GlobalShaderParameterSet("wetter_regen", w.Regen);
        RenderingServer.GlobalShaderParameterSet("wetter_schnee", schnee);
        var himmel = new Color(0.47f, 0.60f, 0.72f).Lerp(new Color(0.34f, 0.36f, 0.39f), w.Wolken);
        RenderingServer.GlobalShaderParameterSet("wetter_himmel", new Vector3(himmel.R, himmel.G, himmel.B));
    }

    private void Umgebung(Wetterlage lage)
    {
        var w = Wetterwerte.Von(lage);
        var grau = new Color(0.55f, 0.58f, 0.62f);
        var himmel = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.38f, 0.55f, 0.82f).Lerp(grau * 0.8f, w.Wolken),
            SkyHorizonColor = new Color(0.66f, 0.74f, 0.84f).Lerp(grau, w.Wolken),
            GroundHorizonColor = new Color(0.66f, 0.74f, 0.84f).Lerp(grau, w.Wolken),
        };
        var umgebung = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = himmel },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        AddChild(new WorldEnvironment { Environment = umgebung });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-48, -35, 0), ShadowEnabled = true, LightEnergy = 1.2f * (1f - 0.6f * w.Wolken) });
    }

    // ── Felder: Boden, Hindernisse und Wasser je Art ──

    /// <summary>Höhe des Bodens (m) und Abstand zum Ufer (m) an einem Punkt des Feldes (lokal −30…30); Wasserspiegel y = 0.</summary>
    private static (float Hoehe, float Ufer) Gelaende(Wasserart art, float x, float z)
    {
        static float Rampe(float d, float steil) => Mathf.Min(1.2f, d * steil); // Ufer steigt aus dem Wasser
        switch (art.Name)
        {
            case "Fluss":
            {
                const float halb = 18f, tiefe = 2.5f;
                var a = Mathf.Abs(x);
                return a < halb ? (-tiefe * (1f - a * a / (halb * halb)), halb - a) : (Rampe(a - halb, 0.8f), 0f);
            }
            case "Bach":
            {
                const float halb = 2f, tiefe = 0.5f;
                var a = Mathf.Abs(x);
                return a < halb ? (-tiefe * (1f - a * a / (halb * halb)), halb - a) : (Rampe(a - halb, 0.6f), 0f);
            }
            case "Furt":
            {
                const float halb = 7f, tiefe = 0.3f;
                var a = Mathf.Abs(x);
                return a < halb - 1f ? (-tiefe, halb - a) : a < halb ? (-tiefe * (halb - a), halb - a) : (Rampe(a - halb, 0.5f), 0f);
            }
            case "Teich":
            case "See":
            {
                var r = Mathf.Sqrt(x * x + z * z);
                var halb = art.Name == "Teich" ? 12f : 28f;
                var tiefe = art.Name == "Teich" ? 1.5f : 4f;
                return r < halb ? (-tiefe * (1f - r * r / (halb * halb)), halb - r) : (Rampe(r - halb, 0.7f), 0f);
            }
            case "Meer":
            {
                const float kueste = -22f;
                return z < kueste ? (Rampe(kueste - z, 0.15f), 0f) : (Mathf.Max(-6f, -(z - kueste) * 0.12f), z - kueste);
            }
            default: // Hafenbecken: Becken 40 × 28 m, 3 m tief, eine Kaimauer ringsum
            {
                var dx = 18f - Mathf.Abs(x);
                var dz = 14f - Mathf.Abs(z);
                var d = Mathf.Min(dx, dz);
                return d > 0f ? (-3f, d) : (Rampe(-d, 1.2f), 0f);
            }
        }
    }

    private void Feld(Wasserart art, Vector3 mitte, float winter, float schnee)
    {
        var knoten = new Node3D { Position = mitte };
        AddChild(knoten);
        knoten.AddChild(new MeshInstance3D { Mesh = BodenNetz(art, schnee), MaterialOverride = BodenStoff() });
        Hindernisse(art, knoten);

        var m = new ShaderMaterial { Shader = _shader };
        Wasserarten.BilderSetzen(m);
        var richtung = art.Stroemung > 0 ? new Vector2(0, 1) : new Vector2(1, 0.4f);
        var gewaesser = art switch
        {
            _ when art == Wasserarten.Fluss => Wasserarten.Fuer(art, 36f, 0),
            _ when art == Wasserarten.Bach => Wasserarten.Fuer(art, 4f, 0),
            _ when art == Wasserarten.Teich => Wasserarten.Fuer(art, 0, MathF.PI * 144f),
            _ when art == Wasserarten.See => Wasserarten.Fuer(art, 0, MathF.PI * 784f),
            _ => art,
        };
        Wasserarten.Anwenden(m, gewaesser, richtung, winter, uferImNetz: true);
        m.SetShaderParameter("netz_halb", new Vector2(30f, 30f)); // Wellen klingen zum Rand des Testfeldes ab
        var netz = new MeshInstance3D { Mesh = WasserNetz(art), MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        knoten.AddChild(netz);
        _wasser.Add((art, netz, m));
        if (art == Wasserarten.Hafenbecken) Kai(knoten);
    }

    private static Mesh BodenNetz(Wasserart art, float schnee)
    {
        const int n = 60;
        var ecken = new Vector3[(n + 1) * (n + 1)];
        var farben = new Color[ecken.Length];
        var uv = new Vector2[ecken.Length];
        var sand = new Color(0.62f, 0.54f, 0.40f);
        var gras = new Color(0.36f, 0.46f, 0.24f);
        for (var z = 0; z <= n; z++)
            for (var x = 0; x <= n; x++)
            {
                float px = x - n / 2f, pz = z - n / 2f;
                var (h, _) = Gelaende(art, px, pz);
                var i = z * (n + 1) + x;
                ecken[i] = new Vector3(px, h, pz);
                uv[i] = new Vector2(x, z) / n;
                // unter Wasser Sand, am Land Gras, bei Schnee weiß (Winter)
                var land = Mathf.SmoothStep(-0.1f, 0.5f, h);
                farben[i] = sand.Lerp(gras, land).Lerp(new Color(0.93f, 0.95f, 0.97f), schnee * land);
            }
        var idx = new List<int>(n * n * 6);
        for (var z = 0; z < n; z++)
            for (var x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x, b = a + 1, c = a + n + 1, d = c + 1;
                idx.AddRange(new[] { a, b, c, b, d, c });
            }
        return NetzBauen(ecken, uv, idx.ToArray(), farben, mitNormalen: true);
    }

    private static Mesh WasserNetz(Wasserart art)
    {
        const int n = 120; // 0,5 m
        var ecken = new Vector3[(n + 1) * (n + 1)];
        var farben = new Color[ecken.Length];
        for (var z = 0; z <= n; z++)
            for (var x = 0; x <= n; x++)
            {
                float px = x * 0.5f - 30f, pz = z * 0.5f - 30f;
                var i = z * (n + 1) + x;
                ecken[i] = new Vector3(px, 0, pz);
                farben[i] = new Color(Mathf.Clamp(Gelaende(art, px, pz).Ufer / 32f, 0f, 1f), 0, 1, 1); // Uferabstand, offen, Schaumanteil, Deckkraft
            }
        var idx = new List<int>(n * n * 6);
        for (var z = 0; z < n; z++)
            for (var x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x, b = a + 1, c = a + n + 1, d = c + 1;
                idx.AddRange(new[] { a, b, c, b, d, c });
            }
        return NetzBauen(ecken, null, idx.ToArray(), farben, mitNormalen: false);
    }

    private static Mesh NetzBauen(Vector3[] ecken, Vector2[]? uv, int[] idx, Color[] farben, bool mitNormalen)
    {
        var feld = new Godot.Collections.Array();
        feld.Resize((int)Mesh.ArrayType.Max);
        feld[(int)Mesh.ArrayType.Vertex] = ecken;
        if (uv != null) feld[(int)Mesh.ArrayType.TexUV] = uv;
        feld[(int)Mesh.ArrayType.Color] = farben;
        feld[(int)Mesh.ArrayType.Index] = idx;
        if (!mitNormalen)
        {
            var normalen = new Vector3[ecken.Length];
            Array.Fill(normalen, Vector3.Up);
            feld[(int)Mesh.ArrayType.Normal] = normalen;
            var netz = new ArrayMesh();
            netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, feld);
            netz.CustomAabb = new Aabb(new Vector3(-31, -2, -31), new Vector3(62, 4, 62)); // Wellen heben die Fläche über das Netz
            return netz;
        }
        var roh = new ArrayMesh();
        roh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, feld);
        var st = new SurfaceTool();
        st.CreateFrom(roh, 0);
        st.GenerateNormals();
        return st.Commit();
    }

    private static StandardMaterial3D BodenStoff() => new()
    {
        VertexColorUseAsAlbedo = true,
        AlbedoTexture = new NoiseTexture2D { Noise = new FastNoiseLite { Frequency = 0.04f }, Width = 512, Height = 512, Seamless = true },
        Uv1Scale = new Vector3(12, 12, 1),
        Roughness = 0.95f,
    };

    private static void Hindernisse(Wasserart art, Node3D eltern)
    {
        var fels = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.41f, 0.40f), Roughness = 0.9f };
        var holz = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.3f, 0.18f), Roughness = 0.8f };
        void Stein(Vector3 p, float r) => eltern.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = r, Height = r * 2.2f, RadialSegments = 12, Rings = 8 }, Position = p, MaterialOverride = fels });
        void Pfahl(Vector3 p) => eltern.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 4f }, Position = p + new Vector3(0, 1.5f, 0), MaterialOverride = holz });
        switch (art.Name)
        {
            case "Fluss": Pfahl(new(5, 0, -5)); Stein(new(-8, -1.3f, 8), 2.0f); Stein(new(9, -1.0f, 12), 1.3f); break;
            case "Bach": Stein(new(0.2f, -0.2f, 5), 0.5f); Stein(new(-0.8f, -0.2f, -4), 0.4f); break;
            case "Furt": Pfahl(new(-3, -0.3f, 4)); Stein(new(2.5f, -0.5f, -6), 0.8f); break;
            case "Teich": Stein(new(-4, -1f, 3), 1.0f); Pfahl(new(3, -1f, -4)); break;
            case "See": Stein(new(-8, -3f, 4), 2.2f); Pfahl(new(10, -2f, -6)); Stein(new(5, -2f, 12), 1.4f); break;
            case "Meer": Stein(new(-8, -2f, 2), 2.6f); Stein(new(10, -3f, 12), 3.0f); Pfahl(new(0, -1f, -14)); break;
        }
    }

    private static void Kai(Node3D eltern)
    {
        var stein = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.53f, 0.50f), Roughness = 0.9f };
        var holz = new StandardMaterial3D { AlbedoColor = new Color(0.40f, 0.27f, 0.16f), Roughness = 0.8f };
        void Mauer(Vector3 mitte, Vector3 groesse) => eltern.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = groesse }, Position = mitte, MaterialOverride = stein });
        Mauer(new(0, 0.5f, -14.5f), new(37f, 4f, 1f));
        Mauer(new(0, 0.5f, 14.5f), new(37f, 4f, 1f));
        Mauer(new(-18.5f, 0.5f, 0), new(1f, 4f, 30f));
        Mauer(new(18.5f, 0.5f, 0), new(1f, 4f, 30f));
        eltern.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(8, 0.8f, 2.6f) }, Position = new(-4, 0.1f, 3), RotationDegrees = new(0, 12, 0), MaterialOverride = holz });
    }


    // ── Messung in einem Lauf: Die Konfigurationen wechseln im selben Prozess reihum (mehrere Runden), damit andere Last auf der Grafikkarte alle gleich trifft ──

    private Shader Variante(params string[] aus)
    {
        var code = GD.Load<Shader>(ShaderPfad).Code;
        foreach (var teil in aus) code = code.Replace("#define MIT_" + teil.ToUpperInvariant(), "// aus");
        return new Shader { Code = code };
    }

    private void MessungVorbereiten(string art)
    {
        var voll = Variante();
        var folge = new List<(string, Action)>();
        void Alle(string name, Shader shader) => folge.Add((name, () => { foreach (var w in _wasser) { w.Netz.Visible = true; w.Stoff.Shader = shader; } }));
        folge.Add(("ohne Wasser", () => { foreach (var w in _wasser) w.Netz.Visible = false; }));
        Alle(art == "stufen" ? "Stufe Hoch" : "alle Arten, volle Stufe", voll);
        switch (art)
        {
            case "winter": Alle("ohne Eis", Variante("eis")); break;
            case "regen": Alle("ohne Ringe", Variante("ringe")); break;
            case "sturm": Alle("ohne Wind", Variante("wind")); break;
            case "teile1": foreach (var teil in new[] { "brechung", "tiefe" }) Alle("ohne " + teil, Variante(teil)); break;
            case "teile2": foreach (var teil in new[] { "kaustik", "schaum" }) Alle("ohne " + teil, Variante(teil)); break;
            case "stufen": // die Shader der Grafikstufen (Ultra ist der Shader von Hoch mit feinerem Netz; die Testfelder sind schon fein)
                Alle("Stufe Niedrig", Wasserarten.ShaderFuer(Wasserstufe.Niedrig));
                Alle("Stufe Mittel", Wasserarten.ShaderFuer(Wasserstufe.Mittel));
                break;
            case "teile3":
                foreach (var teil in new[] { "wind", "ringe" }) Alle("ohne " + teil, Variante(teil));
                Alle("billig (ohne Brechung, Tiefe, Kaustik, Schaum, Eis, Wind, Ringe)", Variante("brechung", "tiefe", "kaustik", "schaum", "eis", "wind", "ringe"));
                break;
            default: // arten
                foreach (var a in Wasserarten.Alle)
                {
                    var name = "nur " + a.Name;
                    folge.Add((name, () => { foreach (var w in _wasser) { w.Netz.Visible = w.Art == a; w.Stoff.Shader = voll; } }));
                }
                break;
        }
        _messfolge = folge;
        _messfolge[0].Setzen();
    }

    private void MessungSchritt(double delta)
    {
        if (_messfolge is null) return;
        _messZeit += delta;
        if (_messZeit > 2.0) _messRahmen.Add(delta); // die ersten zwei Sekunden je Konfiguration (Shader übersetzen) zählen nicht
        if (_messZeit < 5.0) return;
        var name = _messfolge[_messNr].Name;
        _messRahmen.Sort();
        var median = _messRahmen.Count > 0 ? _messRahmen[_messRahmen.Count / 2] : 0.0;
        if (_messRunde > 0) // Runde 0 übersetzt alle Shader
        {
            if (!_messErgebnis.TryGetValue(name, out var liste)) _messErgebnis[name] = liste = new();
            liste.Add(median * 1000);
        }
        _messRahmen.Clear(); _messZeit = 0;
        if (++_messNr >= _messfolge.Count)
        {
            _messNr = 0;
            if (++_messRunde > 3)
            {
                foreach (var (n, werte) in _messErgebnis)
                {
                    werte.Sort();
                    GD.Print($"[messung] {n}: bestes {werte[0]:F1} ms ({1000 / werte[0]:F0} Bilder/s), Median {werte[werte.Count / 2]:F1} ms, schlechtestes {werte[^1]:F1} ms (Runden: {werte.Count})");
                }
                GetTree().Quit();
                return;
            }
        }
        _messfolge[_messNr].Setzen();
    }

    private void Kamera(string sicht, bool alle, float s, bool kueste)
    {
        var kamera = new Camera3D { Fov = 55, Far = 600 };
        AddChild(kamera);
        var (pos, ziel) = alle
            ? sicht switch
            {
                "oben" => (new Vector3(0.01f, 230, 0), Vector3.Zero),
                "fern" => (new Vector3(0, 90, 260), Vector3.Zero),
                _ => (new Vector3(0, 150, 165), new Vector3(0, 0, 8)),
            }
            : sicht switch
            {
                "nah" => (new Vector3(2, 1.4f, 7) * s, new Vector3(0, 0, -2) * s),
                "fern" => (new Vector3(0, 28, 70) * s, Vector3.Zero),
                "oben" => (new Vector3(0.01f, 45, 0) * s, Vector3.Zero),
                "flach" => (new Vector3(0, 0.9f, 20) * s, new Vector3(0, 0.2f, 0)),
                "ufer" => (new Vector3(14, 3, -10) * s, new Vector3(6, 0, -6) * s),
                _ => (new Vector3(12, 9, 22) * s, new Vector3(0, -0.5f, 0)),
            };
        if (kueste && !alle) { pos += new Vector3(0, 0, -12 * s); ziel += new Vector3(0, 0, -12); }
        kamera.Position = pos;
        kamera.LookAt(ziel, sicht == "oben" ? Vector3.Forward : Vector3.Up);
    }

    public override void _Process(double delta)
    {
        MessungSchritt(delta);
        _sek += delta;
        _gesamt += delta;
        _bilder++;
        if (_sek < 1) return;
        var fps = _bilder / _sek;
        _messungen++;
        if (_messungen > 3) _summe += fps; // die ersten Sekunden (Shader übersetzen) zählen nicht
        var mittel = _messungen > 3 ? _summe / (_messungen - 3) : 0;
        GD.Print($"[wassertest] {fps:F1} Bilder/s, {1000 / fps:F2} ms (Mittel ab Sekunde 4: {mittel:F1})");
        _sek = 0; _bilder = 0;
        if (_ende > 0 && _gesamt >= _ende)
        {
            GD.Print($"[wassertest] ENDE Mittel {mittel:F1} Bilder/s = {1000 / Math.Max(mittel, 0.01):F2} ms");
            GetTree().Quit();
        }
    }
}
