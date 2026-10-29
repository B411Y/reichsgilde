using Godot;

namespace Reichsgilde;

/// <summary>Wie die Flächen der Shader-Flamme (Technik A) stehen: immer zur Kamera, nur um die Hochachse zur Kamera, oder zwei feste, gekreuzte Flächen.</summary>
public enum FeuerAusrichtung { Voll, Hochachse, Gekreuzt }

/// <summary>
/// Ein Feuer (Phase 1 „Feuer und Flammen“): Flamme, Funken, Rauch und ein flackerndes <see cref="OmniLight3D"/> zu einer <see cref="FeuerArt"/>, in
/// einer von zwei Techniken (<see cref="FeuerTechnik"/>). Der Knoten sitzt am Fuß der Flamme.
/// <list type="bullet">
/// <item><b>A, Shader</b> (<c>flamme.gdshader</c>, <c>rauch.gdshader</c>): je Zunge eine Fläche mit Flammen-Shader (wanderndes Rauschen, weißgelber Kern, Funken
/// im selben Shader), der Rauch eine Fläche mit Rauch-Shader. Ein Material je Art, der Startwert je Flamme als Instanz-Uniform. Kein Partikelsystem.</item>
/// <item><b>B, Partikel</b>: <see cref="GpuParticles3D"/> mit der Flipbook-Flamme (<c>texturen/feuer/flamme_flipbook.png</c>), Funken und Rauch (der Rauch
/// ist beleuchtet: das Licht des Feuers färbt ihn nachts orange). Flamme (gemischt) und Funken (additiv) ohne Beleuchtung, HDR für den Bloom.</item>
/// <item><b>Licht</b> (beide): ein <see cref="OmniLight3D"/> ohne Schatten, Energie und Farbe flackern nach derselben Rechnung wie die Flamme im Shader
/// (<c>f_rausch</c> aus <c>licht_flackern.gdshaderinc</c>, hier in <see cref="Flackerwert"/> nachgebaut, dieselbe Uhr <see cref="Zeit"/>). Mit Fernausblendung
/// (DistanceFade, <see cref="FeuerArt.LichtAusblendenAb"/>).</item>
/// </list>
/// Wer viele Feuer hat, ruft nichts selbst auf: Jeder Knoten stellt in <c>_Process</c> die gemeinsame Uhr und die geteilten Materialien (einmal je Bild).
/// </summary>
public partial class Feuer : Node3D
{
    /// <summary>Die fertige Größe, wenn keine eigene <see cref="Art"/> gesetzt ist.</summary>
    [Export] public FeuerVorgabe Vorgabe { get; set; } = FeuerVorgabe.Fackel;
    /// <summary>Eigene Werte (überschreibt die Vorgabe).</summary>
    [Export] public FeuerArt? Art { get; set; }
    [Export] public FeuerTechnik Technik { get; set; } = FeuerTechnik.Shader;
    [Export] public FeuerAusrichtung Ausrichtung { get; set; } = FeuerAusrichtung.Voll;
    [Export] public bool MitLicht { get; set; } = true;
    /// <summary>Startwert (0–1) für Flackern und Zufall; kleiner als 0 = aus der Lage berechnen (jedes Feuer anders).</summary>
    [Export] public float Phase { get; set; } = -1f;

    // ═════════════════════════════════════════════════════════════════════════
    //  Gemeinsame Uhr und Umgebung (gilt für alle Feuer)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Uhr der Feuer in Sekunden: Flamme (Shader) und Licht rechnen mit demselben Wert und flackern deshalb im selben Takt.</summary>
    public static double Zeit { get; private set; }

    /// <summary>0 = Nacht … 1 = Tag: Der Rauch der Technik A (unbeleuchtet) wird nachts dunkel; der Rauch der Technik B folgt dem echten Licht.</summary>
    public static float Umgebung { get; set; } = 1f;

    /// <summary>
    /// Zuschlag zur Fernausblendung der Lichter (Meter). Die Stadtkamera ist eine Orthokamera in 150 m Abstand: Die Entfernung zur Kamera ist dort nie
    /// klein, sodass die Fernausblendung (gemessen von der Kamera, nicht vom Blickpunkt) um diesen Wert später einsetzen muss.
    /// </summary>
    public static float AusblendenZuschlag { get; set; }

    private static ulong _letzterRahmen = ulong.MaxValue;
    private static readonly List<ShaderMaterial> _zeitMaterialien = new();
    private static readonly List<ShaderMaterial> _umgebungMaterialien = new();

    // Seed und Phasen der Zungen (für das Licht)
    private readonly List<uint> _saaten = new();
    private OmniLight3D? _licht;
    private float _lichtFaktor = 1f;

    /// <summary>Legt ein Feuer an (noch nicht im Baum): Vorgabe, Technik, ob es leuchtet.</summary>
    public static Feuer Neu(FeuerVorgabe vorgabe, FeuerTechnik technik, bool mitLicht = true, float phase = -1f) =>
        new() { Vorgabe = vorgabe, Technik = technik, MitLicht = mitLicht, Phase = phase };

    public override void _Ready()
    {
        Art ??= FeuerArt.Neu(Vorgabe);
        if (Phase < 0f)
        {
            // Aus der Lage in der Welt: nahe beieinander stehende Feuer bekommen verschiedene Startwerte
            var p = GlobalPosition;
            Phase = Mathf.PosMod((float)(Math.Sin(p.X * 12.9898 + p.Y * 78.233 + p.Z * 37.719) * 43758.5453), 1f);
        }
        var zufall = new RandomNumberGenerator { Seed = (ulong)(Phase * 1000000f) + 17UL };
        if (Technik == FeuerTechnik.Shader) ShaderBauen(Art, zufall);
        else PartikelBauen(Art, zufall);
        if (Art.Form == FeuerForm.Glut) GlutBauen(Art);
        if (MitLicht && Art.LichtEnergie > 0f) LichtBauen(Art);
    }

    public override void _Process(double delta)
    {
        var rahmen = Engine.GetProcessFrames();
        if (rahmen != _letzterRahmen)
        {
            // Einmal je Bild, vom ersten Feuer, das drankommt
            _letzterRahmen = rahmen;
            Zeit += delta;
            var z = (float)Zeit;
            foreach (var m in _zeitMaterialien) m.SetShaderParameter("zeit", z);
            foreach (var m in _umgebungMaterialien) m.SetShaderParameter("umgebung", Umgebung);
        }
        LichtAktualisieren();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Flackern (gleiche Rechnung wie flamme.gdshader / nachtlicht.gdshader)
    // ═════════════════════════════════════════════════════════════════════════

    private static uint HashU(uint x, uint y, uint z, uint w)
    {
        unchecked
        {
            var n = x * 1597334673u ^ y * 3812015801u ^ z * 2798796415u ^ w * 1979697957u;
            n = (n ^ (n >> 15)) * 2246822519u;
            n = (n ^ (n >> 13)) * 3266489917u;
            return n ^ (n >> 16);
        }
    }

    private static float Hash(uint x, uint y, uint z, uint w) => (HashU(x, y, z, w) >> 8) * (1f / 16777216f);

    private static float Rausch(float t, uint s)
    {
        var i = MathF.Floor(t);
        var f = t - i;
        f = f * f * (3f - 2f * f);
        var k = (uint)((int)i & 0xFFFFFF);
        return Mathf.Lerp(Hash(k, s, 7u, 0u), Hash(k + 1u, s, 7u, 0u), f);
    }

    /// <summary>Das Flackern 0–1 zur Zeit <paramref name="t"/> (Sekunden · Tempo) mit dem Startwert <paramref name="saat"/> (wie <c>flacker_wert</c> im Shader).</summary>
    public static float Flackerwert(float t, uint saat) =>
        0.55f * Rausch(t * 1.7f, saat) + 0.3f * Rausch(t * 4.3f, saat ^ 0x5bd1e995u) + 0.15f * Rausch(t * 11f, saat ^ 0x27d4eb2du);

    private static uint Saat(float phase) => (uint)(phase * 65535f) + 7u;

    // ═════════════════════════════════════════════════════════════════════════
    //  Licht
    // ═════════════════════════════════════════════════════════════════════════

    private void LichtBauen(FeuerArt art)
    {
        _licht = new OmniLight3D
        {
            Name = "Licht",
            OmniRange = art.LichtReichweite,
            OmniAttenuation = 1.3f,
            LightEnergy = art.LichtEnergie,
            LightColor = art.LichtFarbeKalt.Lerp(art.LichtFarbeHeiss, 0.5f),
            LightSpecular = 0.25f,
            ShadowEnabled = false,
            Position = new Vector3(0, Mathf.Max(art.Hoehe * 0.7f, 0.05f), 0),
            // Fernausblendung: ab dieser Entfernung zur Kamera wird das Licht schwächer, danach ganz aus (spart die Berechnung)
            DistanceFadeEnabled = true,
            DistanceFadeBegin = art.LichtAusblendenAb + AusblendenZuschlag,
            DistanceFadeLength = art.LichtAusblendenLaenge,
            DistanceFadeShadow = 0f,
        };
        AddChild(_licht);
    }

    private void LichtAktualisieren()
    {
        if (_licht is null || Art is null) return;
        var t = (float)Zeit * Art.Tempo;
        var summe = 0f;
        foreach (var s in _saaten) summe += Flackerwert(t, s);
        var f = _saaten.Count > 0 ? summe / _saaten.Count : 0.5f;
        _licht.LightEnergy = Art.LichtEnergie * _lichtFaktor * (1f + (f - 0.5f) * 2.4f * Art.LichtFlackern);
        _licht.LightColor = Art.LichtFarbeKalt.Lerp(Art.LichtFarbeHeiss, Mathf.Clamp(f * 1.3f - 0.1f, 0f, 1f));
    }

    /// <summary>Stellt die Fernausblendung des Lichts neu (nach einem Wechsel der Kamera, siehe <see cref="AusblendenZuschlag"/>).</summary>
    public void AusblendenNeu()
    {
        if (_licht is not null && Art is not null) _licht.DistanceFadeBegin = Art.LichtAusblendenAb + AusblendenZuschlag;
    }

    /// <summary>Schaltet das Licht ein oder aus (nur der Schein, nicht die Flamme); für Messungen.</summary>
    public void LichtSetzen(bool an)
    {
        if (_licht is not null) _licht.Visible = an;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Technik A: Shader
    // ═════════════════════════════════════════════════════════════════════════

    private static readonly Vector2 FlammenRahmen = new(2.2f, 1.9f);

    private static QuadMesh? _flaeche;

    /// <summary>Eine Fläche 1 × 1 m, der Fuß im Ursprung (steht aufrecht, mit der Vorderseite nach +Z).</summary>
    private static QuadMesh Flaeche => _flaeche ??= new QuadMesh { Size = Vector2.One, CenterOffset = new Vector3(0, 0.5f, 0) };

    private static Texture2D? _rauschen;
    private static Texture2D Rauschen => _rauschen ??= GD.Load<Texture2D>("res://assets/texturen/feuer/rauschen.png");

    private static readonly Dictionary<(FeuerArt, FeuerAusrichtung), ShaderMaterial> _flammenMaterialien = new();
    private static readonly Dictionary<(FeuerArt, FeuerAusrichtung), ShaderMaterial> _rauchMaterialien = new();
    private static readonly Dictionary<FeuerArt, ShaderMaterial> _glutMaterialien = new();

    private static int AusrichtungsWert(FeuerAusrichtung a) => a switch { FeuerAusrichtung.Voll => 2, FeuerAusrichtung.Hochachse => 1, _ => 0 };

    private static ShaderMaterial FlammenMaterial(FeuerArt art, FeuerAusrichtung ausrichtung)
    {
        if (_flammenMaterialien.TryGetValue((art, ausrichtung), out var vorhanden)) return vorhanden;
        var m = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/flamme.gdshader") };
        m.SetShaderParameter("rausch", Rauschen);
        m.SetShaderParameter("billboard", AusrichtungsWert(ausrichtung));
        m.SetShaderParameter("rahmen", FlammenRahmen);
        m.SetShaderParameter("groesse_m", new Vector2(art.Breite, art.Hoehe));
        m.SetShaderParameter("tempo", art.Tempo);
        m.SetShaderParameter("flackern", art.Flackern);
        m.SetShaderParameter("wiegen", 0.4f + 0.7f * art.Flackern);
        m.SetShaderParameter("ausfransen", 0.7f + 0.6f * art.Flackern);
        m.SetShaderParameter("helligkeit", art.Helligkeit);
        m.SetShaderParameter("farbe_kern", art.FarbeKern);
        m.SetShaderParameter("farbe_mitte", art.FarbeMitte);
        m.SetShaderParameter("farbe_rand", art.FarbeRand);
        m.SetShaderParameter("funken", Math.Clamp(art.Funken, 0, 12));
        m.SetShaderParameter("funken_hoehe", art.FunkenHoehe);
        m.SetShaderParameter("funken_groesse", art.FunkenGroesse);
        _zeitMaterialien.Add(m);
        return _flammenMaterialien[(art, ausrichtung)] = m;
    }

    private static ShaderMaterial RauchMaterial(FeuerArt art, FeuerAusrichtung ausrichtung)
    {
        if (_rauchMaterialien.TryGetValue((art, ausrichtung), out var vorhanden)) return vorhanden;
        var m = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/rauch.gdshader") };
        m.SetShaderParameter("rausch", Rauschen);
        m.SetShaderParameter("billboard", AusrichtungsWert(ausrichtung));
        m.SetShaderParameter("dichte", 0.25f + 0.6f * art.RauchMenge);
        m.SetShaderParameter("fuss_breite", Mathf.Clamp(0.55f * art.Breite / Mathf.Max(art.RauchBreite, 0.01f) + 0.12f, 0.12f, 0.5f));
        m.SetShaderParameter("russ", art.RauchRuss);
        m.SetShaderParameter("glut", art.RauchGlut);
        m.SetShaderParameter("glut_zone", Mathf.Clamp(1.6f / Mathf.Max(art.RauchHoehe, 0.1f), 0.05f, 0.35f));
        _zeitMaterialien.Add(m);
        _umgebungMaterialien.Add(m);
        return _rauchMaterialien[(art, ausrichtung)] = m;
    }

    private static Vector3 ZungenLage(int i, int n, float radius)
    {
        if (i == 0 || radius <= 0f) return Vector3.Zero;
        var winkel = i * 2.39996f;   // goldener Winkel: gleichmäßig verstreut
        var r = radius * Mathf.Sqrt((i + 0.5f) / n);
        return new Vector3(Mathf.Cos(winkel) * r, 0, Mathf.Sin(winkel) * r);
    }

    private void ShaderBauen(FeuerArt art, RandomNumberGenerator zufall)
    {
        if (art.Form != FeuerForm.NurRauch)
        {
            var material = FlammenMaterial(art, Ausrichtung);
            for (var i = 0; i < Math.Max(art.Zungen, 1); i++)
            {
                var lage = ZungenLage(i, art.Zungen, art.Radius);
                var rand = art.Radius > 0f ? lage.Length() / art.Radius : 0f;
                // Außen kleiner, innen größer; jede Zunge ein wenig anders
                var groesse = (1f - 0.35f * rand) * (i == 0 ? 1f : zufall.RandfRange(0.72f, 1.05f));
                var phase = i == 0 ? Phase : zufall.Randf();
                _saaten.Add(Saat(phase));
                FlaechenSetzen(material, lage, new Vector3(art.Breite * groesse * FlammenRahmen.X, art.Hoehe * groesse * FlammenRahmen.Y, 1f), phase);
            }
        }
        if (art.RauchMenge > 0f)
        {
            var material = RauchMaterial(art, Ausrichtung);
            var fuss = art.Form switch { FeuerForm.Flamme => art.Hoehe * 0.6f, FeuerForm.Glut => 0.2f, _ => 0f };
            for (var k = 0; k < Math.Max(art.RauchSchichten, 1); k++)
            {
                var seite = k == 0 ? 0f : (k % 2 == 0 ? 1f : -1f) * 0.12f * art.RauchBreite * k;
                var schicht = Mathf.Lerp(1f, 0.72f, k / (float)Math.Max(art.RauchSchichten, 1));
                var lage = new Vector3(seite, fuss, 0f);
                FlaechenSetzen(material, lage, new Vector3(art.RauchBreite * schicht, art.RauchHoehe * (0.8f + 0.2f * schicht), 1f), k == 0 ? Phase : zufall.Randf());
            }
        }
    }

    /// <summary>Eine Fläche (oder bei „gekreuzt“ zwei) mit dem Material und dem Startwert dieser Zunge.</summary>
    private void FlaechenSetzen(ShaderMaterial material, Vector3 lage, Vector3 groesse, float phase)
    {
        var flaechen = Ausrichtung == FeuerAusrichtung.Gekreuzt ? 2 : 1;
        for (var f = 0; f < flaechen; f++)
        {
            var m = new MeshInstance3D
            {
                Mesh = Flaeche,
                MaterialOverride = material,
                Position = lage,
                Scale = groesse,
                RotationDegrees = new Vector3(0, f * 90f + phase * 40f, 0),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                // Die Flamme darf nie weggeschnitten werden, solange ihr Fuß sichtbar ist
                ExtraCullMargin = 2f,
            };
            m.SetInstanceShaderParameter("phase", phase);
            AddChild(m);
        }
    }

    private void GlutBauen(FeuerArt art)
    {
        if (!_glutMaterialien.TryGetValue(art, out var material))
        {
            material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/glut.gdshader") };
            material.SetShaderParameter("rausch", Rauschen);
            material.SetShaderParameter("flackern", art.Flackern + 0.1f);
            material.SetShaderParameter("helligkeit", art.Helligkeit);
            _zeitMaterialien.Add(material);
            _glutMaterialien[art] = material;
        }
        var scheibe = new MeshInstance3D
        {
            Name = "Glutbett",
            Mesh = new QuadMesh { Size = new Vector2(art.Radius * 2.2f, art.Radius * 2.2f), Orientation = PlaneMesh.OrientationEnum.Y },
            MaterialOverride = material,
            Position = new Vector3(0, 0.012f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        scheibe.SetInstanceShaderParameter("phase", Phase);
        AddChild(scheibe);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Technik B: Partikel
    // ═════════════════════════════════════════════════════════════════════════

    private static readonly Dictionary<FeuerArt, StandardMaterial3D> _flammenTeilchen = new();
    private static readonly Dictionary<FeuerArt, StandardMaterial3D> _rauchTeilchen = new();
    private static StandardMaterial3D? _funkenTeilchen;

    private static GradientTexture1D Verlauf(params (float Stelle, Color Farbe)[] punkte)
    {
        var g = new Gradient { Offsets = punkte.Select(p => p.Stelle).ToArray(), Colors = punkte.Select(p => p.Farbe).ToArray() };
        return new GradientTexture1D { Gradient = g, UseHdr = true };
    }

    private static CurveTexture Kurve(float max, params (float X, float Y)[] punkte)
    {
        var c = new Curve { MinValue = 0f, MaxValue = max };
        foreach (var (x, y) in punkte) c.AddPoint(new Vector2(x, y));
        return new CurveTexture { Curve = c };
    }

    /// <summary>
    /// Der Wind für die Partikel (Richtung in der Ebene, Länge = Stärke). Die Shader der Technik A lesen die globalen Uniforms <c>wind_richtung</c> und
    /// <c>wetter_wind</c> selbst; Partikel brauchen den Wert beim Bau (<c>RenderingServer.GlobalShaderParameterGet</c> gibt es nur im Editor), darum setzt ihn
    /// das Spiel hier (Standard wie in project.godot: Richtung (1; 0,4), Stärke 0,25).
    /// </summary>
    public static Vector3 Wind { get; set; } = new Vector3(1f, 0f, 0.4f).Normalized() * 0.35f;

    private static StandardMaterial3D FlammenTeilchenMaterial(FeuerArt art)
    {
        if (_flammenTeilchen.TryGetValue(art, out var vorhanden)) return vorhanden;
        var m = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            // Gemischt, nicht additiv: Überlappende Zungen blenden nicht zu einem weißen Klumpen auf, jede behält ihre Gestalt; der Kern (HDR über 1) leuchtet trotzdem im Bloom
            BlendMode = BaseMaterial3D.BlendModeEnum.Mix,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = GD.Load<Texture2D>("res://assets/texturen/feuer/flamme_flipbook.png"),
            AlbedoColor = new Color(art.Helligkeit * 0.75f, art.Helligkeit * 0.75f, art.Helligkeit * 0.75f),
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            BillboardKeepScale = true,   // sonst gilt die Größe der Teilchen (Skalierung, Verlauf) nicht
            ParticlesAnimHFrames = 8,
            ParticlesAnimVFrames = 4,
            ParticlesAnimLoop = false,
            DisableFog = true,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
        };
        return _flammenTeilchen[art] = m;
    }

    private static StandardMaterial3D FunkenTeilchenMaterial() => _funkenTeilchen ??= new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        VertexColorUseAsAlbedo = true,
        AlbedoTexture = GD.Load<Texture2D>("res://assets/texturen/feuer/funke.png"),
        AlbedoColor = new Color(2.2f, 2.2f, 2.2f),
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            BillboardKeepScale = true,   // sonst gilt die Größe der Teilchen (Skalierung, Verlauf) nicht
        DisableFog = true,
    };

    private static StandardMaterial3D RauchTeilchenMaterial(FeuerArt art)
    {
        if (_rauchTeilchen.TryGetValue(art, out var vorhanden)) return vorhanden;
        var m = new StandardMaterial3D
        {
            // Beleuchtet: Das Licht des Feuers färbt den Rauch nachts orange, der Mond und die Sonne den Rest
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = GD.Load<Texture2D>("res://assets/texturen/feuer/rauch_flipbook.png"),
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            BillboardKeepScale = true,   // sonst gilt die Größe der Teilchen (Skalierung, Verlauf) nicht
            ParticlesAnimHFrames = 4,
            ParticlesAnimVFrames = 4,
            ParticlesAnimLoop = false,
            Roughness = 1f,
            DisableReceiveShadows = true,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
        };
        return _rauchTeilchen[art] = m;
    }

    private void PartikelBauen(FeuerArt art, RandomNumberGenerator zufall)
    {
        for (var i = 0; i < Math.Max(art.Zungen, 1); i++) _saaten.Add(Saat(i == 0 ? Phase : zufall.Randf()));
        var wind = Wind;

        if (art.Form != FeuerForm.NurRauch)
        {
            // Flamme: Teilchen mit dem Flipbook, steigen ein Stück und verjüngen sich
            var leben = art.PartikelLebensdauer;
            var steig = art.Hoehe * 0.32f / leben;
            var flaeche = Mathf.Max(art.Radius, 0.004f);
            var flammen = new GpuParticles3D
            {
                Name = "Flammen",
                Amount = Math.Max(2, (art.Zungen * art.PartikelProZunge + 1) / 2),
                Lifetime = leben,
                Preprocess = leben,
                Randomness = 1f,
                LocalCoords = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityAabb = new Aabb(new Vector3(-flaeche - art.Breite * 2f, -0.3f, -flaeche - art.Breite * 2f), new Vector3(2f * (flaeche + art.Breite * 2f), art.Hoehe * 3f + 0.6f, 2f * (flaeche + art.Breite * 2f))),
                DrawPass1 = new QuadMesh { Size = new Vector2(art.Breite * 1.5f, art.Hoehe * 0.95f), CenterOffset = new Vector3(0, art.Hoehe * 0.475f, 0), Material = FlammenTeilchenMaterial(art) },
                ProcessMaterial = new ParticleProcessMaterial
                {
                    EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
                    EmissionBoxExtents = new Vector3(flaeche, 0.01f, flaeche),
                    Direction = Vector3.Up,
                    Spread = 7f,
                    InitialVelocityMin = steig * 0.7f,
                    InitialVelocityMax = steig * 1.15f,
                    Gravity = Vector3.Zero,
                    ScaleMin = 0.8f,
                    ScaleMax = 1.15f,
                    ScaleCurve = Kurve(1f, (0f, 0.85f), (0.25f, 1f), (1f, 0.4f)),
                    ColorRamp = Verlauf((0f, new Color(1, 1, 1, 0)), (0.12f, new Color(1, 1, 1, 0.95f)), (0.6f, new Color(1, 0.9f, 0.8f, 0.85f)), (1f, new Color(1, 0.6f, 0.4f, 0))),
                    AnimSpeedMin = 1f,
                    AnimSpeedMax = 1f,
                    AnimOffsetMin = 0f,
                    AnimOffsetMax = 0.4f,
                    TurbulenceEnabled = true,
                    TurbulenceNoiseStrength = 0.35f,
                    TurbulenceNoiseScale = 3f,
                    TurbulenceInfluenceMin = 0.03f,
                    TurbulenceInfluenceMax = 0.08f,
                },
            };
            AddChild(flammen);
        }

        if (art.PartikelFunken > 0)
        {
            var reich = art.Hoehe * art.FunkenHoehe;
            var funken = new GpuParticles3D
            {
                Name = "Funken",
                Amount = art.PartikelFunken,
                Lifetime = 1.6f,
                Preprocess = 1.6f,
                Randomness = 1f,
                LocalCoords = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityAabb = new Aabb(new Vector3(-reich, -0.3f, -reich), new Vector3(2f * reich, reich * 1.6f + 0.6f, 2f * reich)),
                DrawPass1 = new QuadMesh { Size = new Vector2(1f, 1.7f), Material = FunkenTeilchenMaterial() },
                ProcessMaterial = new ParticleProcessMaterial
                {
                    EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
                    EmissionSphereRadius = Mathf.Max(art.Radius * 0.8f, art.Breite * 0.25f),
                    Direction = Vector3.Up,
                    Spread = 32f,
                    InitialVelocityMin = reich / 1.6f * 0.6f,
                    InitialVelocityMax = reich / 1.6f * 1.3f,
                    Gravity = new Vector3(wind.X * 0.5f, -reich / 1.6f * 0.35f, wind.Z * 0.5f),
                    ScaleMin = art.FunkenGroesse * 0.7f,
                    ScaleMax = art.FunkenGroesse * 1.5f,
                    ScaleCurve = Kurve(1f, (0f, 1f), (1f, 0.25f)),
                    ColorRamp = Verlauf((0f, new Color(1f, 0.95f, 0.7f, 1f)), (0.35f, new Color(1f, 0.6f, 0.2f, 0.9f)), (1f, new Color(0.9f, 0.2f, 0.05f, 0f))),
                    TurbulenceEnabled = true,
                    TurbulenceNoiseStrength = 1.2f,
                    TurbulenceNoiseScale = 2.5f,
                    TurbulenceInfluenceMin = 0.05f,
                    TurbulenceInfluenceMax = 0.2f,
                },
            };
            AddChild(funken);
        }

        if (art.RauchMenge > 0f)
        {
            var steigen = 0.9f;
            var leben = Mathf.Clamp(art.RauchHoehe / steigen, 2.2f, 14f);
            var fuss = art.Form switch { FeuerForm.Flamme => art.Hoehe * 0.6f, FeuerForm.Glut => 0.2f, _ => 0f };
            var breit = art.RauchBreite * 1.4f + wind.Length() * leben * leben * 0.18f;
            var rauch = new GpuParticles3D
            {
                Name = "Rauch",
                Amount = Math.Clamp(Mathf.RoundToInt(art.RauchHoehe * 4.5f * art.RauchMenge), 5, 60),
                Lifetime = leben,
                Preprocess = leben,
                Randomness = 1f,
                LocalCoords = false,
                DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0, fuss, 0),
                VisibilityAabb = new Aabb(new Vector3(-breit, -0.5f, -breit), new Vector3(2f * breit, art.RauchHoehe * 1.5f + 1f, 2f * breit)),
                DrawPass1 = new QuadMesh { Size = Vector2.One, Material = RauchTeilchenMaterial(art) },
                ProcessMaterial = new ParticleProcessMaterial
                {
                    EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
                    EmissionSphereRadius = Mathf.Max(art.Radius * 0.6f, art.Breite * 0.25f),
                    Direction = Vector3.Up,
                    Spread = 12f,
                    InitialVelocityMin = steigen * 0.75f,
                    InitialVelocityMax = steigen * 1.25f,
                    Gravity = wind * 0.18f,
                    ScaleMin = art.RauchBreite * 0.34f,
                    ScaleMax = art.RauchBreite * 0.5f,
                    ScaleCurve = Kurve(4f, (0f, 0.45f), (0.35f, 1.1f), (1f, 2.2f)),
                    AngleMin = -180f,
                    AngleMax = 180f,
                    AngularVelocityMin = -12f,
                    AngularVelocityMax = 12f,
                    ColorRamp = Verlauf(
                        (0f, new Color(0.8f, 0.8f, 0.8f, 0f)),
                        (0.10f, RauchFarbe(art, 0.85f)),
                        (0.7f, RauchFarbe(art, 0.5f)),
                        (1f, RauchFarbe(art, 0f))),
                    AnimSpeedMin = 0.6f,
                    AnimSpeedMax = 1f,
                    TurbulenceEnabled = true,
                    TurbulenceNoiseStrength = 0.5f,
                    TurbulenceNoiseScale = 2f,
                    TurbulenceInfluenceMin = 0.05f,
                    TurbulenceInfluenceMax = 0.12f,
                },
            };
            AddChild(rauch);
        }
    }

    /// <summary>Farbe und Deckkraft des Rauchs im Verlauf: Ruß macht ihn dunkler und dichter.</summary>
    private static Color RauchFarbe(FeuerArt art, float deckung)
    {
        var grau = Mathf.Lerp(0.72f, 0.16f, art.RauchRuss);
        return new Color(grau, grau, grau * 1.02f, deckung * Mathf.Clamp(0.45f + 0.5f * art.RauchMenge, 0f, 1f));
    }
}
