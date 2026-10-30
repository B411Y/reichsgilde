using Godot;

namespace Reichsgilde;

/// <summary>Wie die Flächen der Flamme stehen: immer zur Kamera, nur um die Hochachse zur Kamera, oder zwei feste, gekreuzte Flächen.</summary>
public enum FeuerAusrichtung { Voll, Hochachse, Gekreuzt }

/// <summary>
/// Ein Feuer: Flamme, Funken, Rauch und auf Wunsch ein flackerndes <see cref="OmniLight3D"/> zu einer <see cref="FeuerArt"/>. Der Knoten sitzt am
/// Fuß der Flamme. Seit Phase 2 (Nutzer 30.10.2026) gibt es nur noch eine Fassung, für alle Grafikstufen gleich:
/// <list type="bullet">
/// <item><b>Flamme</b>: je Zunge eine Fläche, die der Kamera folgt, mit dem Flipbook der Blender-Simulation (<c>flamme_sim.gdshader</c>,
/// <c>texturen/feuer/flamme_ruhig.png</c> bzw. <c>flamme_wild.png</c>); das Bild wählt der Shader aus der gemeinsamen Uhr <see cref="Zeit"/>, die
/// Funken rechnet er mit. Ein Material je Art, der Startwert je Zunge als Instanz-Uniform: Alle Fackeln teilen sich ein Material. Kein Partikelsystem.</item>
/// <item><b>Rauch</b>: eine Säule aus Flächen mit <c>rauch.gdshader</c> (unbeleuchtet; nachts dunkler, aber nie ganz schwarz, <see cref="Umgebung"/>).
/// Wie stark sie qualmt, stellt <see cref="RauchStaerke"/> (Kamin: Feuer im Haus an oder aus).</item>
/// <item><b>Glut</b> (Esse): ein flaches Glutbett mit <c>glut.gdshader</c> unter den Flammen.</item>
/// <item><b>Licht</b> (nur mit <see cref="MitLicht"/>): ein <see cref="OmniLight3D"/> ohne Schatten; Energie und Farbe folgen der Helligkeit der Bilder,
/// die die Zungen gerade zeigen (<c>helligkeit</c> in der <c>.json</c> des Flipbooks), mit Fernausblendung (DistanceFade). Wer das Licht selbst
/// stellt (der Lichter-Pool der Stadt), liest denselben Wert mit <see cref="Lichtwert"/>.</item>
/// </list>
/// Die Uhr stellt <see cref="UhrStellen"/> einmal je Bild: Feuer mit eigenem Licht rufen es selbst auf, die Stadtansicht und die Testszene ebenfalls,
/// so laufen auch Flammen ohne Licht (ohne eigenes <c>_Process</c>) im Takt.
/// </summary>
public partial class Feuer : Node3D
{
    /// <summary>Die fertige Größe, wenn keine eigene <see cref="Art"/> gesetzt ist.</summary>
    [Export] public FeuerVorgabe Vorgabe { get; set; } = FeuerVorgabe.Fackel;
    /// <summary>Eigene Werte (überschreibt die Vorgabe).</summary>
    [Export] public FeuerArt? Art { get; set; }
    [Export] public FeuerAusrichtung Ausrichtung { get; set; } = FeuerAusrichtung.Voll;
    [Export] public bool MitLicht { get; set; } = true;
    /// <summary>Startwert (0–1) für Flackern und Zufall; kleiner als 0 = aus der Lage berechnen (jedes Feuer anders).</summary>
    [Export] public float Phase { get; set; } = -1f;
    /// <summary>Größe der Flamme gegenüber der Art (1 = wie in <see cref="FeuerArt.Hoehe"/>); nur beim Bau gelesen.</summary>
    [Export] public float Groesse { get; set; } = 1f;

    // ═════════════════════════════════════════════════════════════════════════
    //  Gemeinsame Uhr und Umgebung (gilt für alle Feuer)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Uhr der Feuer in Sekunden: Flamme (Shader) und Licht rechnen mit demselben Wert und flackern deshalb im selben Takt.</summary>
    public static double Zeit { get; private set; }

    /// <summary>0 = Nacht … 1 = Tag: Der Rauch ist unbeleuchtet und wird nachts dunkler (ein Rest bleibt, damit man die Säule im Mondlicht sieht).</summary>
    public static float Umgebung { get; set; } = 1f;

    /// <summary>
    /// Zuschlag zur Fernausblendung der Lichter (Meter). Die Stadtkamera ist eine Orthokamera in 150 m Abstand: Die Entfernung zur Kamera ist dort nie
    /// klein, sodass die Fernausblendung (gemessen von der Kamera, nicht vom Blickpunkt) um diesen Wert später einsetzen muss.
    /// </summary>
    public static float AusblendenZuschlag { get; set; }

    private static ulong _letzterRahmen = ulong.MaxValue;
    private static readonly List<ShaderMaterial> _zeitMaterialien = new();
    private static readonly List<ShaderMaterial> _umgebungMaterialien = new();

    /// <summary>
    /// Stellt die Uhr und gibt sie (und <see cref="Umgebung"/>) den geteilten Materialien – höchstens einmal je Bild, egal wie oft es gerufen wird.
    /// Die Stadtansicht und die Testszene rufen es jedes Bild auf, Feuer mit eigenem Licht ebenfalls.
    /// </summary>
    public static void UhrStellen(double delta)
    {
        var rahmen = Engine.GetProcessFrames();
        if (rahmen == _letzterRahmen) return;
        _letzterRahmen = rahmen;
        Zeit += delta;
        var z = (float)Zeit;
        foreach (var m in _zeitMaterialien) m.SetShaderParameter("zeit", z);
        foreach (var m in _umgebungMaterialien) m.SetShaderParameter("umgebung", Umgebung);
    }

    // Startwert je Zunge (für das Licht)
    private readonly List<uint> _saaten = new();
    private SimKennwerte? _sim;
    private OmniLight3D? _licht;
    private readonly List<MeshInstance3D> _rauch = new();
    private readonly List<MeshInstance3D> _flammen = new();   // Zungen und Glutbett
    private float _rauchStaerke = 1f;
    private bool _flammeAn = true;

    /// <summary>Legt ein Feuer an (noch nicht im Baum).</summary>
    public static Feuer Neu(FeuerVorgabe vorgabe, bool mitLicht = true, float phase = -1f, float groesse = 1f) =>
        new() { Vorgabe = vorgabe, MitLicht = mitLicht, Phase = phase, Groesse = groesse };

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
        if (Art.Form != FeuerForm.NurRauch) FlammeBauen(Art, zufall);
        if (Art.RauchMenge > 0f) RauchBauen(Art, zufall);
        if (Art.Form == FeuerForm.Glut) GlutBauen(Art);
        if (MitLicht && Art.LichtEnergie > 0f) LichtBauen(Art);
        if (_rauchStaerke < 1f) RauchStaerke = _rauchStaerke;
        if (!_flammeAn) FlammeAn = false;
        // Nur Feuer mit eigenem Licht brauchen jedes Bild Rechenzeit; die Flammen laufen über die gemeinsame Uhr
        SetProcess(_licht is not null);
    }

    public override void _Process(double delta)
    {
        UhrStellen(delta);
        LichtAktualisieren();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Flackern (gleiche Rechnung wie licht_flackern.gdshaderinc)
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

    /// <summary>
    /// Ein Flackern 0–1 zur Zeit <paramref name="t"/> (Sekunden · Tempo) mit dem Startwert <paramref name="saat"/>, wie <c>flacker_wert</c> der Shader
    /// (<c>nachtlicht.gdshader</c>, Fensterlicht): drei Rauschfrequenzen übereinander. Für Lichter ohne eigenen <see cref="Feuer"/>-Knoten.
    /// </summary>
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
            Position = new Vector3(0, Mathf.Max(art.Hoehe * Groesse * 0.7f, 0.05f), 0),
            // Fernausblendung: ab dieser Entfernung zur Kamera wird das Licht schwächer, danach ganz aus (spart die Berechnung)
            DistanceFadeEnabled = true,
            DistanceFadeBegin = art.LichtAusblendenAb + AusblendenZuschlag,
            DistanceFadeLength = art.LichtAusblendenLaenge,
            DistanceFadeShadow = 0f,
        };
        AddChild(_licht);
    }

    /// <summary>
    /// Wie hell das Feuer gerade ist (0 = dunkelstes Bild … 1 = hellstes, 0,5 = Mittel): die Helligkeit der Bilder des Flipbooks, die die Zungen
    /// in diesem Augenblick zeigen (dieselbe Rechnung wie im Shader). Ein Kamin ohne Flamme flackert nicht (0,5).
    /// </summary>
    public float Lichtwert()
    {
        if (_sim is null || _saaten.Count == 0 || Art is null) return 0.5f;
        var lauf = (float)Zeit * _sim.Bildrate * Art.Tempo;
        var summe = 0f;
        foreach (var s in _saaten)
        {
            var bild = lauf + Hash(s, 23u, 0u, 0u) * _sim.Bilder;
            var b0 = (int)MathF.Floor(bild);
            var w = bild - b0;
            var hell = Mathf.Lerp(_sim.Helligkeit[b0 % _sim.Bilder], _sim.Helligkeit[(b0 + 1) % _sim.Bilder], w);
            summe += Mathf.Clamp(0.5f + (hell - 1f) * 2f, 0f, 1f);
        }
        return summe / _saaten.Count;
    }

    /// <summary>Energie und Farbe eines Lichts zu diesem Feuer nach <see cref="Lichtwert"/> (Grundwerte aus der Art, mal <paramref name="faktor"/>).</summary>
    public (float Energie, Color Farbe) LichtJetzt(float faktor = 1f)
    {
        var art = Art ?? FeuerArt.Neu(Vorgabe);
        var f = Lichtwert();
        return (art.LichtEnergie * faktor * (1f + (f - 0.5f) * 2.4f * art.LichtFlackern), art.LichtFarbeKalt.Lerp(art.LichtFarbeHeiss, Mathf.Clamp(f * 1.3f - 0.1f, 0f, 1f)));
    }

    private void LichtAktualisieren()
    {
        if (_licht is null) return;
        var (energie, farbe) = LichtJetzt();
        _licht.LightEnergy = energie;
        _licht.LightColor = farbe;
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

    /// <summary>Wie stark der Rauch qualmt (0 = gar nicht … 1 = wie in der Art); weich ein- und ausblenden, indem man es jedes Bild ein Stück verstellt.</summary>
    public float RauchStaerke
    {
        get => _rauchStaerke;
        set
        {
            _rauchStaerke = Mathf.Clamp(value, 0f, 1f);
            foreach (var m in _rauch)
            {
                m.SetInstanceShaderParameter("staerke", _rauchStaerke);
                m.Visible = _rauchStaerke > 0.005f;
            }
        }
    }

    /// <summary>Brennt die Flamme (mit Glutbett und Licht)? Aus: nur noch der Rauch, etwa ein Brand, der nur noch schwelt.</summary>
    public bool FlammeAn
    {
        get => _flammeAn;
        set
        {
            _flammeAn = value;
            foreach (var m in _flammen) m.Visible = value;
            if (_licht is not null) _licht.Visible = value;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Flamme, Rauch, Glut
    // ═════════════════════════════════════════════════════════════════════════

    private static QuadMesh? _flaeche;

    /// <summary>Eine Fläche 1 × 1 m, der Fuß im Ursprung (steht aufrecht, mit der Vorderseite nach +Z).</summary>
    private static QuadMesh Flaeche => _flaeche ??= new QuadMesh { Size = Vector2.One, CenterOffset = new Vector3(0, 0.5f, 0) };

    private static Texture2D? _rauschen;
    private static Texture2D Rauschen => _rauschen ??= GD.Load<Texture2D>("res://assets/texturen/feuer/rauschen.png");

    private static readonly Dictionary<(FeuerArt, FeuerAusrichtung), ShaderMaterial> _flammenMaterialien = new();
    private static readonly Dictionary<(FeuerArt, FeuerAusrichtung), ShaderMaterial> _rauchMaterialien = new();
    private static readonly Dictionary<FeuerArt, ShaderMaterial> _glutMaterialien = new();

    private static int AusrichtungsWert(FeuerAusrichtung a) => a switch { FeuerAusrichtung.Voll => 2, FeuerAusrichtung.Hochachse => 1, _ => 0 };

    /// <summary>Das Material der Flamme (<c>flamme_sim.gdshader</c>), je Art und Ausrichtung einmal.</summary>
    private static ShaderMaterial FlammenMaterial(FeuerArt art, FeuerAusrichtung ausrichtung)
    {
        if (_flammenMaterialien.TryGetValue((art, ausrichtung), out var vorhanden)) return vorhanden;
        var kenn = SimKennwerte.Laden(art.Flipbook);
        var m = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/flamme_sim.gdshader") };
        m.SetShaderParameter("atlas", kenn.Atlas);
        m.SetShaderParameter("rausch", Rauschen);
        m.SetShaderParameter("raster", new Vector2(kenn.Spalten, kenn.Zeilen));
        m.SetShaderParameter("bilder", (float)kenn.Bilder);
        m.SetShaderParameter("bildrate", kenn.Bildrate * art.Tempo);
        m.SetShaderParameter("bild_pixel", kenn.BildPixel);
        m.SetShaderParameter("billboard", AusrichtungsWert(ausrichtung));
        m.SetShaderParameter("helligkeit", art.Helligkeit);
        m.SetShaderParameter("toenung", art.Toenung);
        m.SetShaderParameter("flamme_hoehe", kenn.FlammeHoehe);
        m.SetShaderParameter("flamme_breite", kenn.FlammeBreite);
        m.SetShaderParameter("fuss", kenn.Fuss);
        m.SetShaderParameter("funken", Math.Clamp(art.Funken, 0, 12));
        m.SetShaderParameter("funken_hoehe", art.FunkenHoehe);
        m.SetShaderParameter("funken_groesse", art.FunkenGroesse);
        m.SetShaderParameter("groesse_m", new Vector2(art.Breite, art.Hoehe));
        _zeitMaterialien.Add(m);
        return _flammenMaterialien[(art, ausrichtung)] = m;
    }

    /// <summary>
    /// Kennwerte eines Flipbooks der Simulation (<c>texturen/feuer/flamme_&lt;variante&gt;.json</c>, geschrieben von <c>tools/feuer/flammen_sim.py</c>):
    /// Raster, Bildrate, wo die Flamme im Bild steht und die Helligkeit je Bild (für das Licht). Einmal je Variante geladen.
    /// </summary>
    private sealed class SimKennwerte
    {
        public int Spalten = 16, Zeilen = 8, Bilder = 128;
        public Vector2 BildPixel = new(128, 256);
        public float Bildrate = 30f, FlammeHoehe = 0.6f, FlammeBreite = 0.5f, Fuss = 0.06f;
        public float[] Helligkeit = { 1f };
        public Texture2D Atlas = null!;

        private static readonly Dictionary<FeuerFlipbook, SimKennwerte> _geladen = new();

        public static SimKennwerte Laden(FeuerFlipbook flipbook)
        {
            if (_geladen.TryGetValue(flipbook, out var k)) return k;
            var name = flipbook == FeuerFlipbook.Wild ? "wild" : "ruhig";
            k = new SimKennwerte { Atlas = GD.Load<Texture2D>($"res://assets/texturen/feuer/flamme_{name}.png") };
            var pfad = $"res://assets/texturen/feuer/flamme_{name}.json";
            if (Json.ParseString(Godot.FileAccess.GetFileAsString(pfad)).AsGodotDictionary() is { Count: > 0 } d)
            {
                k.Spalten = (int)d["spalten"];
                k.Zeilen = (int)d["zeilen"];
                k.Bilder = (int)d["bilder"];
                var px = d["bild_pixel"].AsGodotArray();
                k.BildPixel = new Vector2((float)px[0], (float)px[1]);
                k.Bildrate = (float)d["bildrate"];
                k.FlammeHoehe = (float)d["flamme_hoehe"];
                k.FlammeBreite = (float)d["flamme_breite"];
                k.Fuss = (float)d["fuss"];
                k.Helligkeit = d["helligkeit"].AsGodotArray().Select(x => (float)x).ToArray();
            }
            else GD.PushWarning($"[feuer] Kennwerte fehlen: {pfad}");
            if (k.Helligkeit.Length < k.Bilder) k.Helligkeit = Enumerable.Repeat(1f, k.Bilder).ToArray();
            return _geladen[flipbook] = k;
        }

        /// <summary>
        /// Breite und Höhe der Fläche (= ein Bild) für eine Flamme der Art: Die Flamme im Bild soll <see cref="FeuerArt.Hoehe"/> hoch sein; die Breite
        /// folgt dem Seitenverhältnis des Bildes und wird nur mäßig (0,8- bis 1,4-fach) auf die Breite der Art gestreckt, sonst wirkt die Flamme verzerrt.
        /// </summary>
        public (float Breite, float Hoehe) Flaeche(FeuerArt art, float groesse)
        {
            var hoehe = art.Hoehe * groesse / Mathf.Max(FlammeHoehe, 0.05f);
            var seiten = BildPixel.X / BildPixel.Y;
            var natuerlich = FlammeBreite * seiten / Mathf.Max(FlammeHoehe, 0.05f);   // Breite / Höhe der Flamme im Bild
            var strecken = Mathf.Clamp(art.Breite / art.Hoehe / Mathf.Max(natuerlich, 0.05f), 0.8f, 1.4f);
            return (hoehe * seiten * strecken, hoehe);
        }
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
        m.SetShaderParameter("umgebung", Umgebung);
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

    /// <summary>Lage, Größe und Startwert der Zunge <paramref name="i"/>: außen kleiner, innen größer, jede ein wenig anders.</summary>
    private (Vector3 Lage, float Groesse, float Phase) Zunge(FeuerArt art, int i, RandomNumberGenerator zufall)
    {
        var lage = ZungenLage(i, art.Zungen, art.Radius * Groesse);
        var rand = art.Radius > 0f ? lage.Length() / (art.Radius * Groesse) : 0f;
        var groesse = Groesse * (1f - 0.35f * rand) * (i == 0 ? 1f : zufall.RandfRange(0.72f, 1.05f));
        var phase = i == 0 ? Phase : zufall.Randf();
        return (lage, groesse, phase);
    }

    /// <summary>Je Zunge eine Fläche mit dem Flipbook, so groß wie ein Bild des Flipbooks.</summary>
    private void FlammeBauen(FeuerArt art, RandomNumberGenerator zufall)
    {
        var kenn = SimKennwerte.Laden(art.Flipbook);
        var material = FlammenMaterial(art, Ausrichtung);
        _sim = kenn;
        for (var i = 0; i < Math.Max(art.Zungen, 1); i++)
        {
            var (lage, groesse, phase) = Zunge(art, i, zufall);
            _saaten.Add(Saat(phase));
            var (breite, hoehe) = kenn.Flaeche(art, groesse);
            // Der Fuß der Flamme liegt im Bild etwas über dem unteren Rand: die Fläche um so viel tiefer setzen
            _flammen.AddRange(FlaechenSetzen(material, lage - new Vector3(0, kenn.Fuss * hoehe, 0), new Vector3(breite, hoehe, 1f), phase));
        }
    }

    /// <summary>Der Rauch als Säule aus Flächen mit <c>rauch.gdshader</c>.</summary>
    private void RauchBauen(FeuerArt art, RandomNumberGenerator zufall)
    {
        var material = RauchMaterial(art, Ausrichtung);
        var fuss = art.Form switch { FeuerForm.Flamme => art.Hoehe * Groesse * 0.6f, FeuerForm.Glut => 0.2f, _ => 0f };
        for (var k = 0; k < Math.Max(art.RauchSchichten, 1); k++)
        {
            var seite = k == 0 ? 0f : (k % 2 == 0 ? 1f : -1f) * 0.12f * art.RauchBreite * k;
            var schicht = Mathf.Lerp(1f, 0.72f, k / (float)Math.Max(art.RauchSchichten, 1));
            var lage = new Vector3(seite, fuss, 0f);
            _rauch.AddRange(FlaechenSetzen(material, lage, new Vector3(art.RauchBreite * schicht, art.RauchHoehe * (0.8f + 0.2f * schicht), 1f), k == 0 ? Phase : zufall.Randf()));
        }
    }

    /// <summary>Eine Fläche (oder bei „gekreuzt“ zwei) mit dem Material und dem Startwert dieser Zunge.</summary>
    private List<MeshInstance3D> FlaechenSetzen(ShaderMaterial material, Vector3 lage, Vector3 groesse, float phase)
    {
        var flaechen = Ausrichtung == FeuerAusrichtung.Gekreuzt ? 2 : 1;
        var neu = new List<MeshInstance3D>(flaechen);
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
            neu.Add(m);
        }
        return neu;
    }

    private void GlutBauen(FeuerArt art)
    {
        if (!_glutMaterialien.TryGetValue(art, out var material))
        {
            material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/glut.gdshader") };
            material.SetShaderParameter("rausch", Rauschen);
            material.SetShaderParameter("flackern", art.Flackern + 0.1f);
            material.SetShaderParameter("helligkeit", 1.8f);
            _zeitMaterialien.Add(material);
            _glutMaterialien[art] = material;
        }
        var scheibe = new MeshInstance3D
        {
            Name = "Glutbett",
            Mesh = new QuadMesh { Size = Vector2.One * art.Radius * Groesse * 2.2f, Orientation = PlaneMesh.OrientationEnum.Y },
            MaterialOverride = material,
            Position = new Vector3(0, 0.012f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        scheibe.SetInstanceShaderParameter("phase", Phase);
        AddChild(scheibe);
        _flammen.Add(scheibe);
    }
}
