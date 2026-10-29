using Godot;

namespace Reichsgilde;

/// <summary>
/// Testszene „Feuer und Flammen“, Phase 1 und 1b (<c>--feuertest=1</c>): ein Steinboden mit sieben Feuerarten. In der Ansicht „Vergleich“ steht
/// links jede in Technik A (Shader-Flamme, Fassung „Simulation“ oder „Rauschen“), rechts in Technik B (Partikel-Flamme), Seite an Seite; der Rauch
/// ist auf beiden Seiten B (Nutzer 29.10.2026). In den Ansichten „Niedrig“, „Mittel“ und „Hoch“ steht jede Feuerart einmal in der Mitte, so wie
/// die Grafikstufe sie zeichnet (Zuordnung in <see cref="FeuerArt"/>). Die Paare stehen hintereinander (Kerze, Fackel, Laterne, Feuerkorb, Esse,
/// Kamin, Gebäudebrand), die Blickrichtung geht nach Norden.
///
/// Startoptionen (siehe <c>docs/STARTOPTIONEN.md</c>): <c>--feuersicht=nah|stadt|fern</c>, <c>--feuerpaar=1–7</c>, <c>--feuertag=1</c>,
/// <c>--feuerart=A|B|keins|beide</c>, <c>--feuerstufe=vergleich|niedrig|mittel|hoch</c>, <c>--feuerfassung=sim|rauschen</c>,
/// <c>--feuerlicht=0|1</c>, <c>--feuerfackeln=&lt;n&gt;</c> (Messung: n Fackeln im Raster, mit <c>--feuerlichter=&lt;m&gt;</c> echten Lichtern).
/// Tasten: 1–7 oder Pfeile = Paar, V = Sicht, N = Tag/Nacht, G = Ansicht (Vergleich, Niedrig, Mittel, Hoch), F = Fassung von A, T = A/B/beide/keins,
/// L = Lichter, H = Beschriftung, rechte Maustaste ziehen = drehen, Rad = nähern, Esc = Ende. Im Protokoll steht jede Sekunde die Bildrate.
/// </summary>
public partial class Feuertest : Node
{
    public const string Szene = "res://scenes/Feuertest.tscn";

    private enum Sicht { Nah, Stadt, Fern }
    private enum Zeigen { Beide, NurA, NurB, Keins }
    /// <summary>Vergleich: A links, B rechts; sonst jede Feuerart einmal, wie die Grafikstufe sie zeichnet.</summary>
    private enum Ansicht { Vergleich, Niedrig, Mittel, Hoch }
    /// <summary>Im Fackelraster: welcher Satz Fackeln zu sehen ist (−1 = keiner).</summary>
    private const int SatzARauschen = 0, SatzASim = 1, SatzB = 2;

    /// <summary>Eine Stelle, an der ein Feuer brennt (der Aufbau bleibt, das Feuer wird beim Wechsel der Ansicht neu gebaut).</summary>
    private sealed record Stelle(int Paar, bool Links, Node3D Wurzel, FeuerVorgabe Vorgabe, Vector3 Lage, bool MitLicht);

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
    private Ansicht _ansicht = Ansicht.Vergleich;
    private ShaderFassung _fassung = ShaderFassung.Simulation;
    private int _rasterSatz = SatzARauschen;
    private readonly List<Stelle> _stellen = new();
    private int _paar = 1;
    private bool _tag, _licht = true, _beschriftung = true;
    private int _fackeln, _fackelLichter = int.MaxValue;
    private sealed record Zustand(string Name, Zeigen Zeigen, int Lichter, int Satz = -1);
    private readonly List<Zustand> _zyklus = new();
    private int _zyklusNr;
    private double _zyklusSekunden, _zyklusZeit;
    private string _zustand = "-";
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

    private double _messZeit, _messCpu, _messGpu;
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
        _ansicht = Option("feuerstufe", "vergleich").ToLowerInvariant() switch { "niedrig" => Ansicht.Niedrig, "mittel" => Ansicht.Mittel, "hoch" => Ansicht.Hoch, _ => Ansicht.Vergleich };
        _fassung = Option("feuerfassung", "sim").ToLowerInvariant() is "rauschen" or "alt" ? ShaderFassung.Rauschen : ShaderFassung.Simulation;
        _sicht = Option("feuersicht", _fackeln > 0 ? "stadt" : "nah").ToLowerInvariant() switch { "stadt" => Sicht.Stadt, "fern" => Sicht.Fern, _ => Sicht.Nah };
        double.TryParse(Option("feuerzyklus", "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _zyklusSekunden);
        if (_fackeln > 0 && _zyklusSekunden <= 0 && _zeigen == Zeigen.Beide) _zeigen = Zeigen.NurA;   // ohne Zyklus gibt es im Raster nur eine Technik
        if (int.TryParse(Option("feuerpaar", "2"), out var p)) _paar = Math.Clamp(p, 1, Paare.Length);
        else _paar = 2;
        _paar--;

        StoffeBauen();
        UmgebungBauen();
        BodenBauen();
        if (_fackeln > 0) FackelRasterBauen(); else { PaareBauen(); FeuerNeuBauen(); }
        KameraBauen();
        OberflaecheBauen();
        TagNachtSetzen();
        SichtSetzen();
        if (_zyklusSekunden > 0) ZyklusBauen();
        ZeigenSetzen();
        GD.Print($"[feuer] start sicht={_sicht} art={_zeigen} ansicht={_ansicht} fassung={_fassung} tag={_tag} licht={_licht} paar={Paare[_paar].Name} fackeln={_fackeln} lichter={(_fackelLichter == int.MaxValue ? "alle" : _fackelLichter)} " +
                 $"renderer={RenderingServer.GetCurrentRenderingMethod()} fenster={GetViewport().GetVisibleRect().Size}");
    }

    /// <summary>
    /// Messzyklus (<c>--feuerzyklus=&lt;Sekunden&gt;</c>): Die Szene wechselt von selbst durch die Zustände (ohne Feuer, A, B, im Raster auch mit 0, 12 und
    /// allen Lichtern) und schreibt den Namen des Zustands in jede Protokollzeile. Weil alle Zustände im selben Lauf folgen, trifft die Last des Rechners
    /// sie nahezu gleich; getrennte Läufe schwanken auf einem geteilten Rechner stärker als der Unterschied selbst.
    /// </summary>
    private void ZyklusBauen()
    {
        _zyklus.Add(new Zustand("ohne_Feuer", Zeigen.Keins, 0));
        if (_fackeln > 0)
        {
            foreach (var (name, satz) in new[] { ("A_Rauschen", SatzARauschen), ("A_Simulation", SatzASim), ("B", SatzB) })
                foreach (var lichter in new[] { 0, 12, _fackeln })
                    _zyklus.Add(new Zustand($"{name}_{lichter}_Lichter", satz == SatzB ? Zeigen.NurB : Zeigen.NurA, lichter, satz));
        }
        else
        {
            _zyklus.Add(new Zustand("A", Zeigen.NurA, int.MaxValue));
            _zyklus.Add(new Zustand("B", Zeigen.NurB, int.MaxValue));
            _zyklus.Add(new Zustand("A_und_B", Zeigen.Beide, int.MaxValue));
        }
        ZyklusSchritt(0);
    }

    private void ZyklusSchritt(int nr)
    {
        _zyklusNr = nr % _zyklus.Count;
        var z = _zyklus[_zyklusNr];
        _zustand = z.Name;
        _zeigen = z.Zeigen;
        if (_fackeln > 0) _rasterSatz = z.Satz;
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

    private Feuer FeuerSetzen(Node3D eltern, FeuerVorgabe vorgabe, FeuerTechnik technik, Vector3 lage, bool mitLicht = true, ShaderFassung fassung = ShaderFassung.Rauschen)
    {
        var f = Feuer.Neu(vorgabe, technik, mitLicht && _licht, fassung: fassung);
        f.Position = lage;
        eltern.AddChild(f);
        _feuer.Add(f);
        return f;
    }

    /// <summary>Merkt eine Stelle für ein Feuer; <see cref="FeuerNeuBauen"/> setzt dort das Feuer der gewählten Ansicht.</summary>
    private void StelleMerken(Node3D wurzel, FeuerVorgabe vorgabe, Vector3 lage, bool mitLicht = true) =>
        _stellen.Add(new Stelle((int)wurzel.GetMeta("paar"), (bool)wurzel.GetMeta("links"), wurzel, vorgabe, lage, mitLicht));

    private static Feuerqualitaet Stufe(Ansicht a) => a switch { Ansicht.Niedrig => Feuerqualitaet.Niedrig, Ansicht.Mittel => Feuerqualitaet.Mittel, _ => Feuerqualitaet.Hoch };

    /// <summary>
    /// Baut alle Feuer der Paare neu (nach einem Wechsel der Ansicht oder der Fassung von A). Vergleich: links A in der gewählten Fassung, rechts B,
    /// der Rauch beidseits B. Grafikstufe: nur die linke Seite, in die Mitte gerückt, Flamme und Rauch nach <see cref="Feuer.NachStufe"/>.
    /// </summary>
    private void FeuerNeuBauen()
    {
        foreach (var f in _feuer)
        {
            f.GetParent()?.RemoveChild(f);
            f.QueueFree();
        }
        _feuer.Clear();
        for (var i = 0; i < _wurzelA.Count; i++)
            _wurzelA[i].Position = new Vector3(_ansicht == Ansicht.Vergleich ? -Paare[i].Halb : 0f, 0, _zPaar[i]);
        foreach (var st in _stellen)
        {
            if (_ansicht != Ansicht.Vergleich && !st.Links) continue;
            var f = _ansicht == Ansicht.Vergleich
                ? Feuer.Neu(st.Vorgabe, st.Links ? FeuerTechnik.Shader : FeuerTechnik.Partikel, st.MitLicht, rauch: FeuerTechnik.Partikel, fassung: _fassung)
                : Feuer.NachStufe(st.Vorgabe, Stufe(_ansicht), st.MitLicht, fassung: _fassung);
            f.Position = st.Lage;
            f.SetMeta("links", st.Links);
            st.Wurzel.AddChild(f);
            f.LichtSetzen(_licht);
            _feuer.Add(f);
        }
        foreach (var l in _beschriftungen) l.Text = BeschriftungText((int)l.GetMeta("paar"), (bool)l.GetMeta("links"));
        PaareSichtbar();
        ZeigenSetzen();
    }

    private string FassungName(FeuerVorgabe v) =>
        FeuerArt.Neu(v).Flipbook == FeuerFlipbook.Keins || _fassung == ShaderFassung.Rauschen ? "Rauschen" : "Simulation";

    private string BeschriftungText(int paar, bool links)
    {
        var p = Paare[paar];
        if (_ansicht == Ansicht.Vergleich)
        {
            if (FeuerArt.Neu(p.Vorgabe).Form == FeuerForm.NurRauch) return $"{(links ? "A" : "B")}  {p.Name} (nur Rauch, beidseits B)";
            return links ? $"A ({FassungName(p.Vorgabe)})  {p.Name}" : $"B  {p.Name}";
        }
        var art = FeuerArt.Neu(p.Vorgabe);
        var stufe = Stufe(_ansicht);
        var flamme = art.Form == FeuerForm.NurRauch ? "keine Flamme" : art.FlammeFuer(stufe) == FeuerTechnik.Shader ? $"Flamme A ({FassungName(p.Vorgabe)})" : "Flamme B";
        return $"{_ansicht}: {p.Name} – {flamme}, Rauch {(FeuerArt.RauchFuer(stufe) == FeuerTechnik.Shader ? "A" : "B")}";
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
                var links = technik == FeuerTechnik.Shader;
                var wurzel = new Node3D { Name = $"{paar.Name}_{(links ? "A" : "B")}", Position = new Vector3(seite * paar.Halb, 0, z) };
                wurzel.SetMeta("paar", i);
                wurzel.SetMeta("links", links);
                AddChild(wurzel);
                (links ? _wurzelA : _wurzelB).Add(wurzel);
                PropBauen(wurzel, paar);
                var text = new Label3D
                {
                    Text = $"{(links ? "A" : "B")}  {paar.Name}",
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
                text.SetMeta("links", links);
                wurzel.AddChild(text);
                _beschriftungen.Add(text);
            }
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
                StelleMerken(w, FeuerVorgabe.Kerze, new Vector3(0, 0.88f, 0));
                break;
            case FeuerVorgabe.Fackel:
                Zylinder(w, 0.03f, 0.035f, 1.55f, Vector3.Zero, _holz);
                Zylinder(w, 0.085f, 0.04f, 0.17f, new Vector3(0, 1.55f, 0), _eisen);
                Zylinder(w, 0.07f, 0.07f, 0.04f, new Vector3(0, 1.7f, 0), _kohle);
                Wand(w, 4f, 2.6f, -0.85f);
                StelleMerken(w, FeuerVorgabe.Fackel, new Vector3(0, 1.72f, 0));
                break;
            case FeuerVorgabe.Laterne:
                Zylinder(w, 0.025f, 0.03f, 1.48f, Vector3.Zero, _eisen);
                Kasten(w, new Vector3(0.2f, 0.03f, 0.2f), new Vector3(0, 1.48f, 0), _eisen);
                Kasten(w, new Vector3(0.17f, 0.22f, 0.17f), new Vector3(0, 1.51f, 0), _glas);
                Kasten(w, new Vector3(0.22f, 0.04f, 0.22f), new Vector3(0, 1.73f, 0), _eisen);
                Zylinder(w, 0.003f, 0.012f, 0.05f, new Vector3(0, 1.51f, 0), _wachs);
                Wand(w, 4f, 2.6f, -0.85f);
                StelleMerken(w, FeuerVorgabe.Laterne, new Vector3(0, 1.545f, 0));
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
                StelleMerken(w, FeuerVorgabe.Feuerkorb, new Vector3(0, 1.17f, 0));
                break;
            case FeuerVorgabe.Esse:
                Kasten(w, new Vector3(1.3f, 0.85f, 1.0f), Vector3.Zero, _stein);
                Zylinder(w, 0.42f, 0.42f, 0.02f, new Vector3(0, 0.85f, 0), _dunkel);
                Kasten(w, new Vector3(1.3f, 1.7f, 0.3f), new Vector3(0, 0.85f, -0.48f), _stein);
                Kasten(w, new Vector3(0.5f, 0.12f, 0.25f), new Vector3(0.9f, 0.85f, 0.1f), _eisen);   // Amboss-Block daneben
                StelleMerken(w, FeuerVorgabe.Esse, new Vector3(0, 0.88f, 0));
                break;
            case FeuerVorgabe.Kamin:
                HausBauen(w, 2.4f, 1.6f, 2.4f, false);
                Kasten(w, new Vector3(0.45f, 1.3f, 0.45f), new Vector3(0.7f, 1.6f, -0.3f), _stein);
                StelleMerken(w, FeuerVorgabe.Kamin, new Vector3(0.7f, 3.0f, -0.3f), false);
                break;
            case FeuerVorgabe.Gebaeudebrand:
                HausBauen(w, 6.5f, 3.6f, 5.5f, true);
                Kasten(w, new Vector3(0.6f, 1.6f, 0.6f), new Vector3(2.0f, 3.6f, -1.2f), _stein);
                StelleMerken(w, FeuerVorgabe.Gebaeudebrand, new Vector3(0, 4.6f, 0));
                StelleMerken(w, FeuerVorgabe.Fensterbrand, new Vector3(-1.8f, 1.3f, 2.85f), false);
                StelleMerken(w, FeuerVorgabe.Fensterbrand, new Vector3(1.8f, 1.3f, 2.85f), false);
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
        // Messung: n Fackeln im Raster (10 je Reihe, 3,5 m Abstand), mit Pfahl und Schale; im Zyklus stehen A (Rauschen), A (Simulation) und B
        // übereinander, je ein Satz zu sehen. Flamme und Rauch in derselben Technik (wie Niedrig bei A, wie Hoch bei B).
        _rasterSatz = _zeigen == Zeigen.Keins ? -1 : _zeigen == Zeigen.NurB ? SatzB : _fassung == ShaderFassung.Simulation ? SatzASim : SatzARauschen;
        var saetze = _zyklusSekunden > 0 ? new[] { SatzARauschen, SatzASim, SatzB } : new[] { _rasterSatz };
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
            if (_zeigen == Zeigen.Keins && _zyklusSekunden <= 0) continue;
            foreach (var satz in saetze)
            {
                var f = FeuerSetzen(stelle, FeuerVorgabe.Fackel, satz == SatzB ? FeuerTechnik.Partikel : FeuerTechnik.Shader, new Vector3(0, 1.72f, 0), i < _fackelLichter,
                    satz == SatzASim ? ShaderFassung.Simulation : ShaderFassung.Rauschen);
                f.SetMeta("fackel", i);
                f.SetMeta("satz", satz);
            }
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
            if (f.HasMeta("satz")) f.Visible = (int)f.GetMeta("satz") == _rasterSatz;          // Fackelraster: ein Satz
            else if (_ansicht == Ansicht.Vergleich) f.Visible = (bool)f.GetMeta("links") ? a : b;  // links A, rechts B
            else f.Visible = _zeigen != Zeigen.Keins;                                             // Grafikstufe: alle (außer „keins“)
            f.ProcessMode = f.Visible ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        }
        BeschriftungSetzen();
    }

    /// <summary>In der Nähe nur das gewählte Paar (die übrigen stehen hinter der Mauer, sind aber sonst zu sehen und kosten Zeit).</summary>
    private void PaareSichtbar()
    {
        for (var i = 0; i < _wurzelA.Count; i++)
        {
            _wurzelA[i].Visible = _sicht != Sicht.Nah || i == _paar;
            // In den Ansichten der Grafikstufe steht jede Feuerart nur einmal (die linke Seite, in die Mitte gerückt)
            _wurzelB[i].Visible = _wurzelA[i].Visible && _ansicht == Ansicht.Vergleich;
        }
    }

    /// <summary>Nah und Stadt: nur die Namen des gewählten Paars (sonst scheinen die der anderen durch die Mauern); fern nur die der großen Paare (Kamin, Gebäudebrand), die kleinen liegen zu dicht.</summary>
    private void BeschriftungSetzen()
    {
        foreach (var l in _beschriftungen)
            l.Visible = _beschriftung && (_ansicht == Ansicht.Vergleich || (bool)l.GetMeta("links"))
                        && (_sicht == Sicht.Fern ? (int)l.GetMeta("paar") >= 5 : (int)l.GetMeta("paar") == _paar);
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
        _hilfe.Text = "1–7 / Pfeile: Paar   V: Sicht (nah, Stadt, fern)   N: Nacht/Tag   G: Ansicht (Vergleich, Niedrig, Mittel, Hoch)   F: Fassung von A (Simulation/Rauschen)\n" +
                      "T: A / B / beide / keins   L: Lichter   H: Beschriftung   Rechte Maustaste ziehen: drehen   Mausrad: nähern   Esc: Ende";
        AnzeigeAktualisieren(0);
    }

    private void AnzeigeAktualisieren(double fps)
    {
        var kopf = _fackeln > 0
            ? $"Feuertest – Messung: {_fackeln} Fackeln, Technik {_rasterSatz switch { SatzB => "B (Partikel)", SatzASim => "A (Simulation)", SatzARauschen => "A (Rauschen)", _ => "keine (nur Pfähle)" }}"
            : _ansicht == Ansicht.Vergleich
                ? $"Feuertest – Vergleich: links A (Shader-Flamme, Fassung {(_fassung == ShaderFassung.Simulation ? "Simulation" : "Rauschen")})   rechts B (Partikel-Flamme)   Rauch beidseits B"
                : $"Feuertest – Grafikstufe {_ansicht}: so zeichnet das Spiel die Feuer (Fassung von A: {(_fassung == ShaderFassung.Simulation ? "Simulation" : "Rauschen")})";
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
                    case Key.G when _fackeln == 0:
                        _ansicht = (Ansicht)(((int)_ansicht + 1) % 4);
                        FeuerNeuBauen();
                        SichtSetzen();
                        AnzeigeAktualisieren(0);
                        break;
                    case Key.F when _fackeln == 0:
                        _fassung = _fassung == ShaderFassung.Simulation ? ShaderFassung.Rauschen : ShaderFassung.Simulation;
                        FeuerNeuBauen();
                        AnzeigeAktualisieren(0);
                        break;
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
        GD.Print($"[feuer] fps={fps:F1} ms={ms:F2} gpu={gpu:F2} cpu={cpu:F2} zustand={_zustand} sicht={_sicht} art={_zeigen} ansicht={_ansicht} fassung={_fassung} tag={_tag} licht={_licht} paar={(_fackeln > 0 ? "-" : Paare[_paar].Name)} " +
                 $"feuer={_feuer.Count(f => f.Visible)} aufrufe={r(RenderingServer.RenderingInfo.TotalDrawCallsInFrame)} objekte={r(RenderingServer.RenderingInfo.TotalObjectsInFrame)} " +
                 $"dreiecke={r(RenderingServer.RenderingInfo.TotalPrimitivesInFrame)}");
        AnzeigeAktualisieren(fps);
        _messZeit = _messCpu = _messGpu = 0;
        _messBilder = 0;
    }
}
