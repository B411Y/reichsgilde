using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Zeit = Reichsgilde.Baumjahr.Zeit;

namespace Reichsgilde;

/// <summary>
/// Testszene „Bäume“ (Nutzer 21.11.2026: Originalbäume statt vereinfachter, mit Wind und Jahreszeiten): die Originalmodelle des Quaternius-Pakets
/// (<c>natur_original_baeume.glb</c>, CC0) in allen vier Jahreszeiten nebeneinander – Eiche, Obstbaum, Tanne, Busch ohne und mit Blüten –, dazu
/// Wind (ruhig bis Sturm), Schnee auf den Tannen, fallendes Laub im Herbst (nur Stufe hoch und ultra) und ein Wald zum Messen von Dreiecken und
/// Zeichenaufrufen (Original gegen die vereinfachten Bäume von heute gegen Original mit Fernfassung).
/// Startoptionen (hinter „--“): <c>--baumtest=1</c>, <c>--baumwind=0–1</c> (0,2), <c>--baumzeit=alle|fruehling|sommer|herbst|winter</c> (alle),
/// <c>--baumsicht=reihe|nah|wald</c>, <c>--baumwald=original|heute|lod</c> (original), <c>--baumlod=auto|nah|fern</c> (auto: Fernfassung ab
/// <see cref="LodEntfernung"/> m mit weichem Überblenden), <c>--baumstufe=niedrig|mittel|hoch|ultra</c> (hoch), <c>--baumkamera=x,y,z:zx,zy,zz</c>,
/// <c>--baumende=Sekunden</c>. Tasten: 1–4 Wind (0,05 / 0,25 / 0,6 / 1), L Fernfassung ein/aus, Esc beendet. Freie Kamera wie in jeder Testszene
/// (rechte Maustaste halten, W A S D Q E, Shift, Mausrad, H Hilfe).
/// </summary>
public partial class Baumtest : Node
{
    public const string Szene = "res://scenes/Baumtest.tscn";
    private const string Modell = "res://assets/modelle/natur/natur_original_baeume.glb";
    private const string KitModell = "res://assets/modelle/natur/natur_kit_baeume.glb";
    public const float LodEntfernung = 60f;

    public static bool Angefordert
    {
        get
        {
            foreach (var a in OS.GetCmdlineUserArgs())
                if (a == "--baumtest" || (a.StartsWith("--baumtest=") && a != "--baumtest=0")) return true;
            return false;
        }
    }

    
    private readonly Dictionary<string, string> _optionen = new();
    private float _wind = 0.2f;
    private string _lod = "auto";
    private int _stufe = 2;
    private Node _quelle = null!;
    private Node _kit = null!;
    private FreieKamera _kamera = null!;
    private Label _anzeige = null!;
    private int _bilder;
    private double _zeit, _ende;
    private readonly Dictionary<string, ShaderMaterial> _stoffe = new();
    private readonly List<MeshInstance3D> _fern = new();
    private readonly List<MeshInstance3D> _nah = new();
    private bool _fernErzwungen;

    private string Option(string name, string vorgabe) => _optionen.TryGetValue(name, out var w) ? w : vorgabe;

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (!a.StartsWith("--baum")) continue;
            var i = a.IndexOf('=');
            _optionen[i < 0 ? a[2..] : a[2..i]] = i < 0 ? "1" : a[(i + 1)..];
        }
        _wind = Mathf.Clamp(float.TryParse(Option("baumwind", "0.2"), NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 0.2f, 0f, 1f);
        _lod = Option("baumlod", "auto");
        _stufe = Option("baumstufe", "hoch") switch { "niedrig" => 0, "mittel" => 1, "ultra" => 3, _ => 2 };
        _ende = double.TryParse(Option("baumende", "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out var e) ? e : 0;

        _quelle = GD.Load<PackedScene>(Modell).Instantiate();
        _kit = GD.Load<PackedScene>(KitModell).Instantiate();
        UmgebungBauen();
        var sicht = Option("baumsicht", "reihe");
        if (sicht == "wald") WaldBauen(Option("baumwald", "original"));
        else ReiheBauen(Option("baumzeit", "alle"));
        KameraBauen(sicht);
        OberflaecheBauen();
        WindSetzen();
        GD.Print($"[baum] start wind={_wind} sicht={sicht} lod={_lod} stufe={_stufe} renderer={RenderingServer.GetCurrentRenderingMethod()}");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Umgebung, Kamera, Anzeige
    // ═════════════════════════════════════════════════════════════════════════

    private void UmgebungBauen()
    {
        var umgebung = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.55f, 0.66f, 0.78f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.72f, 0.74f, 0.80f),
            AmbientLightEnergy = 0.55f,
            TonemapMode = Fenster.AktuellesTonemapping == Tonemapping.Agx ? Godot.Environment.ToneMapper.Agx : Godot.Environment.ToneMapper.Linear,
        };
        Stadtansicht.BloomAnwenden(umgebung);
        AddChild(new WorldEnvironment { Environment = umgebung });
        AddChild(new DirectionalLight3D
        {
            ShadowEnabled = true, DirectionalShadowMaxDistance = 140f, LightEnergy = 1.1f,
            LightColor = new Color(1f, 0.97f, 0.9f), RotationDegrees = new Vector3(-50, 30, 0),
        });
    }

    private void KameraBauen(string sicht)
    {
        var (von, nach) = sicht switch
        {
            "wald" => (new Vector3(0, 40, 60), new Vector3(0, 0, -10)),
            "nah" => (new Vector3(0, 6, 22), new Vector3(0, 5, 0)),
            _ => (new Vector3(0, 38, 36), new Vector3(0, 0, -16)),
        };
        var frei = Option("baumkamera", "").Split(':');
        if (frei.Length == 2 && Vektor(frei[0]) is { } v1 && Vektor(frei[1]) is { } v2) (von, nach) = (v1, v2);
        _kamera = FreieKamera.Einhaengen(this, von, nach);
        _kamera.Bodenhoehe = (_, _) => 0f;
        _kamera.Far = 2000f;
    }

    private static Vector3? Vektor(string s)
    {
        var t = s.Split(',');
        if (t.Length != 3) return null;
        float F(string x) => float.Parse(x, CultureInfo.InvariantCulture);
        try { return new Vector3(F(t[0]), F(t[1]), F(t[2])); } catch (FormatException) { return null; }
    }

    private void OberflaecheBauen()
    {
        var ebene = new CanvasLayer();
        _anzeige = new Label { Position = new Vector2(12, 10) };
        _anzeige.AddThemeColorOverride("font_color", Colors.White);
        _anzeige.AddThemeColorOverride("font_outline_color", Colors.Black);
        _anzeige.AddThemeConstantOverride("outline_size", 6);
        ebene.AddChild(_anzeige);
        AddChild(ebene);
    }

    private void WindSetzen()
    {
        RenderingServer.GlobalShaderParameterSet("wetter_wind", _wind);
        RenderingServer.GlobalShaderParameterSet("wind_richtung", new Vector2(1f, 0.15f));
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        switch (k.Keycode)
        {
            case Key.Key1: _wind = 0.05f; WindSetzen(); break;
            case Key.Key2: _wind = 0.25f; WindSetzen(); break;
            case Key.Key3: _wind = 0.6f; WindSetzen(); break;
            case Key.Key4: _wind = 1f; WindSetzen(); break;
            case Key.L: FernErzwingen(!_fernErzwungen); break;
            case Key.Escape: GetTree().Quit(); break;
        }
    }

    private void FernErzwingen(bool an)
    {
        _fernErzwungen = an;
        foreach (var f in _fern) { f.VisibilityRangeBegin = an ? 0f : LodEntfernung - 8f; f.VisibilityRangeFadeMode = an ? GeometryInstance3D.VisibilityRangeFadeModeEnum.Disabled : GeometryInstance3D.VisibilityRangeFadeModeEnum.Self; }
        foreach (var n in _nah) { n.VisibilityRangeEnd = an ? 0.001f : LodEntfernung; n.Visible = !an; }
    }

    public override void _Process(double dt)
    {
        _zeit += dt;
        _bilder++;
        var info = $"Wind {_wind:0.00}  Fernfassung {(_fernErzwungen ? "an" : _lod)}  Stufe {_stufe}\n"
            + $"Dreiecke {RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame):N0}  Zeichenaufrufe {RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame):N0}";
        _anzeige.Text = info;
        if (_bilder == 40) GD.Print("[baum] " + info.Replace("\n", "; "));
        if (_ende > 0 && _zeit >= _ende) GetTree().Quit();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Materialien nach Jahreszeit
    // ═════════════════════════════════════════════════════════════════════════

    private MeshInstance3D? Netz(Node wurzel, string name) => wurzel.FindChild(name, true, false) as MeshInstance3D;

    /// <summary>Winter: Laubbäume kahl; bei <paramref name="trocken"/> (Büsche, Jungeiche) die ausgedünnte Fassung mit dürrem braunem Laub.</summary>
    private MeshInstance3D? Teil(Node wurzel, string knoten, Zeit z, string art, Node3D eltern, bool lodSteuern, bool fern)
    {
        var quelle = Netz(wurzel, knoten);
        if (quelle?.Mesh is null) return null;
        var netz = (Mesh)quelle.Mesh.Duplicate();   // eigene Flächen: das Netz der Quelle bleibt unberührt
        var hoehe = netz.GetAabb().End.Y;
        for (var i = 0; i < netz.GetSurfaceCount(); i++)
            if (quelle.Mesh.SurfaceGetMaterial(i) is BaseMaterial3D q) netz.SurfaceSetMaterial(i, Baumjahr.Stoff(_stoffe, q, z, art, hoehe));
        var mi = new MeshInstance3D { Mesh = netz, Name = knoten };
        eltern.AddChild(mi);
        if (lodSteuern)
        {
            if (fern) { mi.VisibilityRangeBegin = LodEntfernung - 8f; mi.VisibilityRangeBeginMargin = 8f; mi.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self; _fern.Add(mi); }
            else { mi.VisibilityRangeEnd = LodEntfernung; mi.VisibilityRangeEndMargin = 8f; mi.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self; _nah.Add(mi); }
        }
        return mi;
    }

    
    /// <summary>Ein Baum der Quelle (Knoten „Eiche1“ …) an <paramref name="ort"/>: nah, dazu fern mit Überblenden.</summary>
    private Node3D BaumSetzen(string knoten, string art, Vector3 ort, Zeit z, float drehung = 0f, float groesse = 1f)
    {
        var wurzel = new Node3D { Name = knoten + "_" + art + "_" + z, Position = ort, RotationDegrees = new Vector3(0, drehung, 0), Scale = Vector3.One * groesse };
        AddChild(wurzel);
        var trocken = Baumjahr.Trocken(art, z);
        var kahl = z == Zeit.Winter && !trocken && Netz(_quelle, knoten + "_Winter") is not null;
        var nah = trocken ? knoten + "_Fern" : knoten + (kahl ? "_Winter" : "");
        var fern = trocken ? knoten + "_Fern" : knoten + (kahl ? "_FernWinter" : "_Fern");
        var lodAn = _lod == "auto" && !trocken;
        if (_lod != "fern" || trocken) Teil(_quelle, nah, z, art, wurzel, lodAn, false);
        if (!trocken && _lod is "fern" or "auto") Teil(_quelle, fern, z, art, wurzel, lodAn, true);
        return wurzel;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Reihe der Jahreszeiten
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Knoten des Modells, Art (bestimmt Farben, Blüte, Winterbild), Name in der Anzeige, Ort, Größe.</summary>
    private static readonly (string Knoten, string Art, string Anzeige, float X, float Groesse)[] Arten =
    {
        ("Eiche1", "Eiche", "Eiche", -30f, 1f), ("Eiche3", "Jungeiche", "Jungeiche", -23f, 0.7f), ("Buche1", "Buche", "Buche", -16f, 1f),
        ("Apfel1", "Apfel", "Apfel", -8.5f, 1f), ("Kirsche1", "Kirsche", "Kirsche", -1f, 1f),
        ("Fichte2", "Fichte", "Fichte", 6f, 1f), ("Kiefer1", "Kiefer", "Kiefer", 13f, 1f),
        ("Hasel1", "Hasel", "Hasel", 20f, 1f), ("Weissdorn1", "Weissdorn", "Weissdorn", 26f, 1f), ("Holunder1", "Holunder", "Holunder", 32f, 1f),
    };

    private static readonly Color[] Boden =
    {
        new(0.36f, 0.50f, 0.22f), new(0.30f, 0.44f, 0.17f), new(0.48f, 0.40f, 0.20f), new(0.90f, 0.92f, 0.95f),
    };

    private void ReiheBauen(string welche)
    {
        var zeiten = welche switch
        {
            "fruehling" => new[] { Zeit.Fruehling }, "sommer" => new[] { Zeit.Sommer },
            "herbst" => new[] { Zeit.Herbst }, "winter" => new[] { Zeit.Winter },
            _ => new[] { Zeit.Fruehling, Zeit.Sommer, Zeit.Herbst, Zeit.Winter },
        };
        for (var r = 0; r < zeiten.Length; r++)
        {
            var z = zeiten[r];
            var tiefe = -r * 14f;
            AddChild(new MeshInstance3D
            {
                Name = "Boden_" + z, Mesh = new BoxMesh { Size = new Vector3(80, 0.1f, 13.5f) },
                Position = new Vector3(0, -0.05f, tiefe),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = Boden[(int)z], Roughness = 1f },
            });
            AddChild(new Label3D
            {
                Text = z switch { Zeit.Fruehling => "Fruehling", Zeit.Sommer => "Sommer", Zeit.Herbst => "Herbst", _ => "Winter" },
                Position = new Vector3(-37, 1.2f, tiefe + 5), FontSize = 120, PixelSize = 0.012f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Modulate = Colors.White, OutlineSize = 24, NoDepthTest = true,
            });
            foreach (var (knoten, art, anzeige, x, groesse) in Arten)
            {
                BaumSetzen(knoten, art, new Vector3(x, 0, tiefe), z, 15f * (x < 0 ? 1 : -1), groesse);
                if (r == 0)
                    AddChild(new Label3D
                    {
                        Text = anzeige, Position = new Vector3(x, 0.15f, tiefe + 5.2f), FontSize = 64, PixelSize = 0.01f, RotationDegrees = new Vector3(-70, 0, 0),
                        Modulate = Colors.White, OutlineSize = 12, NoDepthTest = true,
                    });
            }
            if (z == Zeit.Herbst && _stufe >= 2)
                foreach (var (knoten, art, _, x, _) in Arten)
                    if (art is "Eiche" or "Buche" or "Kirsche") LaubFallen(new Vector3(x, art == "Kirsche" ? 4.5f : 7.5f, tiefe), art switch { "Eiche" => new Color(0.66f, 0.46f, 0.15f), "Buche" => new Color(0.80f, 0.42f, 0.12f), _ => new Color(0.86f, 0.28f, 0.12f) });
        }
        GD.Print($"[baum] reihe zeiten={zeiten.Length}");
    }

    /// <summary>Fallendes Laub unter der Krone (Grafikstufe hoch und ultra): kleine orange Blattkarten, die im Wind abdriften und kippen.</summary>
    private void LaubFallen(Vector3 ort, Color farbe)
    {
        var stoff = new StandardMaterial3D
        {
            AlbedoColor = farbe, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 1f,
            VertexColorUseAsAlbedo = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
        };
        var verlauf = new Gradient { Colors = new[] { Colors.White, new Color(0.85f, 0.8f, 0.75f), new Color(1f, 0.95f, 0.85f) }, Offsets = new[] { 0f, 0.5f, 1f } };
        var teilchen = new GpuParticles3D
        {
            Name = "FallendesLaub", Amount = _stufe >= 3 ? 90 : 45, Lifetime = 7.0, Preprocess = 7.0,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.18f, 0.14f), Material = stoff },
            Position = ort, VisibilityAabb = new Aabb(new Vector3(-12, -10, -12), new Vector3(24, 14, 24)),
            ProcessMaterial = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(3.2f, 0.3f, 3.2f),
                Direction = new Vector3(1, -0.4f, 0.15f), Spread = 25f, InitialVelocityMin = 0.2f, InitialVelocityMax = 0.8f,
                Gravity = new Vector3(0.5f + _wind * 3f, -0.9f, 0.1f), DampingMin = 0.2f, DampingMax = 0.6f,
                AngularVelocityMin = -180f, AngularVelocityMax = 180f,
                TurbulenceEnabled = true, TurbulenceNoiseStrength = 0.6f, TurbulenceNoiseScale = 1.5f,
                ScaleMin = 0.7f, ScaleMax = 1.3f,
                ColorRamp = new GradientTexture1D { Gradient = verlauf },
            },
        };
        AddChild(teilchen);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wald zum Messen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ein Waldstück (Quadrat von 200 m, rund 600 Bäume: Eichen, Tannen, Büsche) in vier mal vier Zellen, je Zelle und Sorte ein MultiMesh – wie das
    /// Spiel es mit den Natur-Bäumen macht. <paramref name="art"/>: <c>original</c> (alle Originale), <c>heute</c> (die vereinfachten Kit-Bäume),
    /// <c>lod</c> (Original nah, Fernfassung ab <see cref="LodEntfernung"/> m).
    /// </summary>
    private void WaldBauen(string art)
    {
        AddChild(new MeshInstance3D
        {
            Name = "Boden", Mesh = new BoxMesh { Size = new Vector3(260, 0.1f, 260) }, Position = new Vector3(0, -0.05f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Boden[1], Roughness = 1f },
        });
        var sorten = art == "heute"
            ? new[] { ("Laubbaum1", 0.2f), ("Laubbaum2", 0.2f), ("Laubbaum3", 0.15f), ("Kiefer1", 0.12f), ("Kiefer2", 0.12f), ("Kiefer4", 0.11f), ("Kiefer5", 0.1f) }
            : new[] { ("Eiche1", 0.15f), ("Eiche2", 0.15f), ("Eiche3", 0.12f), ("Buche1", 0.1f), ("Buche2", 0.08f), ("Fichte1", 0.08f), ("Fichte2", 0.1f), ("Fichte3", 0.08f), ("Fichte4", 0.06f), ("Kiefer1", 0.06f), ("Hasel1", 0.04f), ("Holunder1", 0.03f) };
        var zufall = new Random(4711);
        const int gesamt = 600, zellen = 4;
        var lagen = new Dictionary<(string, int, int), List<Transform3D>>();
        for (var i = 0; i < gesamt; i++)
        {
            var x = (float)(zufall.NextDouble() * 200 - 100);
            var z = (float)(zufall.NextDouble() * 200 - 100);
            var wahl = zufall.NextDouble();
            var summe = 0f;
            var sorte = sorten[^1].Item1;
            foreach (var (n, anteil) in sorten) { summe += anteil; if (wahl * sorten.Sum(t => t.Item2) <= summe) { sorte = n; break; } }
            var groesse = 0.85f + (float)zufall.NextDouble() * 0.35f;
            var basis = Basis.FromEuler(new Vector3(0, (float)(zufall.NextDouble() * Mathf.Tau), 0)).Scaled(Vector3.One * groesse);
            var zx = Mathf.Clamp((int)((x + 100) / 50f), 0, zellen - 1);
            var zz = Mathf.Clamp((int)((z + 100) / 50f), 0, zellen - 1);
            if (!lagen.TryGetValue((sorte, zx, zz), out var liste)) lagen[(sorte, zx, zz)] = liste = new();
            liste.Add(new Transform3D(basis, new Vector3(x, 0, z)));
        }
        foreach (var ((sorte, zx, zz), liste) in lagen)
        {
            if (art == "heute") WaldZelle(_kit, sorte, liste, false, false);
            else
            {
                if (art == "lod") { WaldZelle(_quelle, sorte, liste, true, false); WaldZelle(_quelle, sorte + "_Fern", liste, true, true); }
                else WaldZelle(_quelle, sorte, liste, false, false);
            }
        }
        GD.Print($"[baum] wald={art} baeume={gesamt}");
    }

    private void WaldZelle(Node quelle, string knoten, List<Transform3D> lagen, bool lod, bool fern)
    {
        var q = Netz(quelle, knoten);
        if (q?.Mesh is null) return;
        var netz = (Mesh)q.Mesh.Duplicate();
        var art = Baumjahr.Art(knoten) is { Length: > 0 } a ? a : knoten.StartsWith("Laub") ? "Eiche" : "Fichte";
        var hoehe = netz.GetAabb().End.Y;
        if (quelle == _quelle)
            for (var i = 0; i < netz.GetSurfaceCount(); i++)
                if (q.Mesh.SurfaceGetMaterial(i) is BaseMaterial3D m) netz.SurfaceSetMaterial(i, Baumjahr.Stoff(_stoffe, m, Zeit.Sommer, art, hoehe));
        var vielfach = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = netz, InstanceCount = lagen.Count };
        for (var i = 0; i < lagen.Count; i++) vielfach.SetInstanceTransform(i, lagen[i]);
        var mmi = new MultiMeshInstance3D { Multimesh = vielfach, Name = knoten };
        if (lod)
        {
            if (fern) { mmi.VisibilityRangeBegin = LodEntfernung - 8f; mmi.VisibilityRangeBeginMargin = 8f; }
            else { mmi.VisibilityRangeEnd = LodEntfernung; mmi.VisibilityRangeEndMargin = 8f; }
            mmi.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;
        }
        AddChild(mmi);
    }
}
