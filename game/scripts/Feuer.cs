using Godot;

namespace Reichsgilde;

/// <summary>Wie die Flächen der Shader-Flamme (Technik A) stehen: immer zur Kamera, nur um die Hochachse zur Kamera, oder zwei feste, gekreuzte Flächen.</summary>
public enum FeuerAusrichtung { Voll, Hochachse, Gekreuzt }

/// <summary>
/// Ein Feuer (Phase 1 „Feuer und Flammen“): Flamme, Funken, Rauch und ein flackerndes <see cref="OmniLight3D"/> zu einer <see cref="FeuerArt"/>, in
/// einer von zwei Techniken (<see cref="FeuerTechnik"/>). Der Knoten sitzt am Fuß der Flamme.
/// <list type="bullet">
/// <item><b>A, Shader</b>: je Zunge eine Fläche, der Rauch eine Fläche mit Rauch-Shader (<c>rauch.gdshader</c>). Die Flamme in zwei Fassungen
/// (<see cref="ShaderFassung"/>): „Rauschen“ (<c>flamme.gdshader</c>: wanderndes Rauschen, weißgelber Kern, für große Feuer mit Stichflammen) oder
/// „Simulation“ (<c>flamme_sim.gdshader</c>: das Flipbook aus der Blender-Simulation, das Bild wählt der Shader aus der Uhr). Funken im selben Shader.
/// Ein Material je Art, der Startwert je Flamme als Instanz-Uniform. Kein Partikelsystem.</item>
/// <item><b>B, Partikel</b>: <see cref="GpuParticles3D"/>, jedes Teilchen ein Flipbook der Simulation (<c>flamme_sim.gdshader</c> im Teilchen-Betrieb,
/// <c>texturen/feuer/flamme_ruhig.png</c> bzw. <c>flamme_wild.png</c>), dazu Funken und Rauch (der Rauch ist beleuchtet: das Licht des Feuers färbt ihn
/// nachts orange). Flamme und Funken ohne Beleuchtung, HDR für den Bloom.</item>
/// <item><b>Rauch</b>: eigene Technik (<see cref="RauchTechnik"/>), denn ab Mittel ist der Rauch immer B, auch bei einer Flamme in A
/// (<see cref="NachStufe"/>, Zuordnung in <see cref="FeuerArt"/>).</item>
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
    /// <summary>Technik der Flamme (und der Funken).</summary>
    [Export] public FeuerTechnik Technik { get; set; } = FeuerTechnik.Shader;
    /// <summary>Technik des Rauchs (Nutzer 29.10.2026: ab Mittel immer B).</summary>
    [Export] public FeuerTechnik RauchTechnik { get; set; } = FeuerTechnik.Partikel;
    /// <summary>Nur Technik A: Rauschen-Flamme oder Flipbook der Simulation (Feuerarten ohne Flipbook, Kerze und Laterne, bleiben beim Rauschen).</summary>
    [Export] public ShaderFassung Fassung { get; set; } = ShaderFassung.Simulation;
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
    private SimKennwerte? _sim;          // gesetzt, wenn die Flamme das Flipbook als Fläche zeigt (Licht folgt der Helligkeit der Bilder)
    private float _stichflammen;         // Rauschen-Flamme mit Stichflammen (Licht folgt den Stößen)
    private OmniLight3D? _licht;
    private float _lichtFaktor = 1f;

    /// <summary>Welche Fassung der Technik A <see cref="Neu"/> nimmt, wenn keine angegeben ist.</summary>
    public static ShaderFassung StandardFassung { get; set; } = ShaderFassung.Simulation;

    /// <summary>Legt ein Feuer an (noch nicht im Baum): Vorgabe, Technik der Flamme, ob es leuchtet; der Rauch in derselben Technik, wenn nichts anderes gesagt ist.</summary>
    public static Feuer Neu(FeuerVorgabe vorgabe, FeuerTechnik technik, bool mitLicht = true, float phase = -1f, FeuerTechnik? rauch = null, ShaderFassung? fassung = null) =>
        new() { Vorgabe = vorgabe, Technik = technik, RauchTechnik = rauch ?? technik, Fassung = fassung ?? StandardFassung, MitLicht = mitLicht, Phase = phase };

    /// <summary>Ein Feuer, wie es auf einer Grafikstufe gezeichnet wird: Flamme und Rauch nach der Zuordnung in <see cref="FeuerArt"/> (Niedrig alles A).</summary>
    public static Feuer NachStufe(FeuerVorgabe vorgabe, Feuerqualitaet stufe, bool mitLicht = true, float phase = -1f, ShaderFassung? fassung = null)
    {
        var art = FeuerArt.Neu(vorgabe);
        return Neu(vorgabe, art.FlammeFuer(stufe), mitLicht, phase, FeuerArt.RauchFuer(stufe), fassung);
    }

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
        if (Art.Form != FeuerForm.NurRauch)
        {
            if (Technik == FeuerTechnik.Partikel) PartikelFlammeBauen(Art, zufall);
            else if (Fassung == ShaderFassung.Simulation && Art.Flipbook != FeuerFlipbook.Keins) SimFlammeBauen(Art, zufall);
            else ShaderFlammeBauen(Art, zufall);
        }
        if (Technik == FeuerTechnik.Partikel && Art.PartikelFunken > 0) PartikelFunkenBauen(Art);
        if (Art.RauchMenge > 0f)
        {
            if (RauchTechnik == FeuerTechnik.Shader) ShaderRauchBauen(Art, zufall);
            else PartikelRauchBauen(Art);
        }
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

    /// <summary>
    /// Stichflamme der Rauschen-Flamme (0 = keine … 1 = auf der Spitze), wie <c>stich_wert</c> in <c>flamme.gdshader</c>: Die Zeit ist in Abschnitte von
    /// 1,6 bis 3,2 s geteilt; in jedem kommt mit der Wahrscheinlichkeit <paramref name="haeufigkeit"/> · 0,75 ein Stoß zu einer zufälligen Stelle,
    /// der in 0,12 s ansteigt und danach abklingt.
    /// </summary>
    public static float Stichwert(float t, uint saat, float haeufigkeit)
    {
        if (haeufigkeit <= 0f) return 0f;
        var dauer = 1.6f + 1.6f * Hash(saat, 41u, 0u, 0u);
        var lauf = t / dauer + Hash(saat, 43u, 0u, 0u);
        var abschnitt = (uint)((int)MathF.Floor(lauf) & 0xFFFFFF);
        var imAbschnitt = (lauf - MathF.Floor(lauf)) * dauer;
        var start = Hash(saat, abschnitt, 47u, 0u) * MathF.Max(dauer - 0.9f, 0.1f);
        if (Hash(saat, abschnitt, 53u, 0u) >= haeufigkeit * 0.75f) return 0f;
        var s = imAbschnitt - start;
        if (s < 0f) return 0f;
        var anstieg = Mathf.SmoothStep(0f, 0.12f, s);
        return anstieg * MathF.Exp(-MathF.Max(s - 0.12f, 0f) * 4f);
    }

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
        if (_sim is not null)
        {
            // Flipbook der Simulation (Fläche): die Helligkeit des Bildes, das die Zunge gerade zeigt (dieselbe Rechnung wie im Shader)
            var lauf = (float)Zeit * _sim.Bildrate * Art.Tempo;
            foreach (var s in _saaten)
            {
                var bild = lauf + Hash(s, 23u, 0u, 0u) * _sim.Bilder;
                var b0 = (int)MathF.Floor(bild);
                var w = bild - b0;
                var hell = Mathf.Lerp(_sim.Helligkeit[b0 % _sim.Bilder], _sim.Helligkeit[(b0 + 1) % _sim.Bilder], w);
                summe += Mathf.Clamp(0.5f + (hell - 1f) * 2f, 0f, 1f);
            }
        }
        else
        {
            foreach (var s in _saaten)
            {
                summe += Flackerwert(t, s);
                // Rauschen-Flamme mit Stichflammen: ein Stoß macht auch das Licht kurz heller
                if (_stichflammen > 0f) summe += 0.6f * Stichwert(t, s, _stichflammen);
            }
        }
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

    /// <summary>Größe der Fläche der Rauschen-Flamme in Flammenbreiten und -höhen; mit Stichflammen höher, damit der Stoß Platz hat.</summary>
    private static Vector2 FlammenRahmen(FeuerArt art) => new(2.2f + 0.4f * art.Stichflammen, 1.9f + 0.7f * art.Stichflammen);

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
        m.SetShaderParameter("rahmen", FlammenRahmen(art));
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
        m.SetShaderParameter("stichflammen", art.Stichflammen);
        m.SetShaderParameter("bewegung", art.Stichflammen);
        _zeitMaterialien.Add(m);
        return _flammenMaterialien[(art, ausrichtung)] = m;
    }

    private static readonly Dictionary<(FeuerArt, FeuerAusrichtung, bool), ShaderMaterial> _simMaterialien = new();

    /// <summary>Das Material des Flipbooks der Simulation (<c>flamme_sim.gdshader</c>): als Fläche (Technik A) oder für Teilchen (Technik B).</summary>
    private static ShaderMaterial SimMaterial(FeuerArt art, FeuerAusrichtung ausrichtung, bool partikel)
    {
        if (_simMaterialien.TryGetValue((art, ausrichtung, partikel), out var vorhanden)) return vorhanden;
        var kenn = SimKennwerte.Laden(art.Flipbook == FeuerFlipbook.Keins ? FeuerFlipbook.Ruhig : art.Flipbook);
        var m = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/flamme_sim.gdshader") };
        m.SetShaderParameter("atlas", kenn.Atlas);
        m.SetShaderParameter("rausch", Rauschen);
        m.SetShaderParameter("raster", new Vector2(kenn.Spalten, kenn.Zeilen));
        m.SetShaderParameter("bilder", (float)kenn.Bilder);
        m.SetShaderParameter("bildrate", kenn.Bildrate * art.Tempo);
        m.SetShaderParameter("bild_pixel", kenn.BildPixel);
        m.SetShaderParameter("partikel", partikel);
        m.SetShaderParameter("billboard", AusrichtungsWert(ausrichtung));
        // Teilchen überlagern sich (meist zwei bis drei zugleich): darum jedes etwas dunkler als die einzelne Fläche
        m.SetShaderParameter("helligkeit", art.Helligkeit * SimHelligkeit * (partikel ? 0.6f : 1f));
        // Farbe der Art gegenüber der Grundfarbe (Esse etwas röter); die gebackene Flamme ist schon farbig
        var grund = new Color(1.0f, 0.55f, 0.08f);
        m.SetShaderParameter("toenung", new Color(1f, Mathf.Clamp(art.FarbeMitte.G / grund.G, 0.6f, 1.2f), Mathf.Clamp(art.FarbeMitte.B / grund.B, 0.6f, 1.2f)));
        m.SetShaderParameter("flamme_hoehe", kenn.FlammeHoehe);
        m.SetShaderParameter("flamme_breite", kenn.FlammeBreite);
        m.SetShaderParameter("fuss", kenn.Fuss);
        m.SetShaderParameter("funken", partikel ? 0 : Math.Clamp(art.Funken, 0, 12));
        m.SetShaderParameter("funken_hoehe", art.FunkenHoehe);
        m.SetShaderParameter("funken_groesse", art.FunkenGroesse);
        m.SetShaderParameter("groesse_m", new Vector2(art.Breite, art.Hoehe));
        _zeitMaterialien.Add(m);
        return _simMaterialien[(art, ausrichtung, partikel)] = m;
    }

    /// <summary>Wie hell das Flipbook gegenüber <see cref="FeuerArt.Helligkeit"/> leuchtet (die gebackenen Bilder sind schon gesättigt).</summary>
    private const float SimHelligkeit = 1.0f;

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

    /// <summary>Technik A, Fassung „Rauschen“: je Zunge eine Fläche mit <c>flamme.gdshader</c>.</summary>
    private void ShaderFlammeBauen(FeuerArt art, RandomNumberGenerator zufall)
    {
        var material = FlammenMaterial(art, Ausrichtung);
        var rahmen = FlammenRahmen(art);
        _stichflammen = art.Stichflammen;
        // Große Feuer: die Zungen etwas breiter, damit sie ineinander übergehen und nicht wie eine Reihe Kerzen wirken
        var breiter = 1f + 0.35f * art.Stichflammen;
        for (var i = 0; i < Math.Max(art.Zungen, 1); i++)
        {
            var (lage, groesse, phase) = Zunge(art, i, zufall);
            _saaten.Add(Saat(phase));
            FlaechenSetzen(material, lage, new Vector3(art.Breite * groesse * rahmen.X * breiter, art.Hoehe * groesse * rahmen.Y, 1f), phase);
        }
        // … und darunter ein Feuerbett aus wenigen breiten, niedrigen Flammen, das die Zungen verbindet (nur bei vielen Zungen)
        if (art.Stichflammen >= 0.5f && art.Zungen >= 6)
        {
            var bett = (art.Zungen + 2) / 3;
            for (var k = 0; k < bett; k++)
            {
                var winkel = k * Mathf.Tau / bett + 0.4f;
                var lage = new Vector3(Mathf.Cos(winkel), 0, Mathf.Sin(winkel)) * art.Radius * 0.55f;
                var phase = zufall.Randf();
                FlaechenSetzen(material, lage, new Vector3(art.Breite * 2.4f * rahmen.X, art.Hoehe * 0.5f * rahmen.Y, 1f), phase);
            }
        }
    }

    /// <summary>Lage, Größe und Startwert der Zunge <paramref name="i"/>: außen kleiner, innen größer, jede ein wenig anders.</summary>
    private (Vector3 Lage, float Groesse, float Phase) Zunge(FeuerArt art, int i, RandomNumberGenerator zufall)
    {
        var lage = ZungenLage(i, art.Zungen, art.Radius);
        var rand = art.Radius > 0f ? lage.Length() / art.Radius : 0f;
        var groesse = (1f - 0.35f * rand) * (i == 0 ? 1f : zufall.RandfRange(0.72f, 1.05f));
        var phase = i == 0 ? Phase : zufall.Randf();
        return (lage, groesse, phase);
    }

    /// <summary>Technik A, Fassung „Simulation“: je Zunge eine Fläche mit dem Flipbook (<c>flamme_sim.gdshader</c>), so groß wie ein Bild des Flipbooks.</summary>
    private void SimFlammeBauen(FeuerArt art, RandomNumberGenerator zufall)
    {
        var kenn = SimKennwerte.Laden(art.Flipbook);
        var material = SimMaterial(art, Ausrichtung, false);
        _sim = kenn;
        for (var i = 0; i < Math.Max(art.Zungen, 1); i++)
        {
            var (lage, groesse, phase) = Zunge(art, i, zufall);
            _saaten.Add(Saat(phase));
            var (breite, hoehe) = kenn.Flaeche(art, groesse);
            // Der Fuß der Flamme liegt im Bild etwas über dem unteren Rand: die Fläche um so viel tiefer setzen
            FlaechenSetzen(material, lage - new Vector3(0, kenn.Fuss * hoehe, 0), new Vector3(breite, hoehe, 1f), phase);
        }
    }

    /// <summary>Technik A: der Rauch als Säule aus Flächen mit <c>rauch.gdshader</c>.</summary>
    private void ShaderRauchBauen(FeuerArt art, RandomNumberGenerator zufall)
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

    /// <summary>
    /// Technik B: die Flamme aus Teilchen, jedes ein ganzes Flipbook der Simulation (<c>flamme_sim.gdshader</c> im Teilchen-Betrieb) mit eigenem
    /// Startbild und zufällig gespiegelt. Die Teilchen stehen fast still (die Bewegung steckt im Flipbook), leben gut eine Sekunde und blenden
    /// weich ein und aus: So mischen sich immer zwei bis drei Flammen, und die Schleife des Flipbooks fällt nicht auf.
    /// </summary>
    private void PartikelFlammeBauen(FeuerArt art, RandomNumberGenerator zufall)
    {
        for (var i = 0; i < Math.Max(art.Zungen, 1); i++) _saaten.Add(Saat(i == 0 ? Phase : zufall.Randf()));
        var kenn = SimKennwerte.Laden(art.Flipbook == FeuerFlipbook.Keins ? FeuerFlipbook.Ruhig : art.Flipbook);
        var (breite, hoehe) = kenn.Flaeche(art, 1f);
        var flaeche = Mathf.Max(art.Radius, 0.004f);
        var leben = Mathf.Max(art.PartikelLebensdauer, 1.1f);
        var rand = flaeche + breite;
        var flammen = new GpuParticles3D
        {
            Name = "Flammen",
            Amount = Math.Max(2, art.Zungen * 3 / 2 + 1),
            Lifetime = leben,
            Preprocess = leben,
            Randomness = 1f,
            LocalCoords = false,
            DrawOrder = GpuParticles3D.DrawOrderEnum.ViewDepth,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-rand, -hoehe * 0.3f, -rand), new Vector3(2f * rand, hoehe * 1.6f, 2f * rand)),
            // Der Fuß der Flamme liegt im Bild etwas über dem unteren Rand: die Fläche um so viel tiefer
            DrawPass1 = new QuadMesh { Size = new Vector2(breite, hoehe), CenterOffset = new Vector3(0, hoehe * (0.5f - kenn.Fuss), 0), Material = SimMaterial(art, Ausrichtung, true) },
            ProcessMaterial = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(flaeche, 0.005f, flaeche),
                Direction = Vector3.Up,
                Spread = 3f,
                InitialVelocityMin = art.Hoehe * 0.03f,
                InitialVelocityMax = art.Hoehe * 0.08f,
                Gravity = Vector3.Zero,
                ScaleMin = 0.85f,
                ScaleMax = 1.08f,
                ScaleCurve = Kurve(1.2f, (0f, 0.92f), (1f, 1.04f)),
                // Ein- und Ausblenden: vormultipliziert, darum Farbe und Deckung zugleich (der Shader rechnet COLOR.rgb · COLOR.a)
                ColorRamp = Verlauf((0f, new Color(1, 1, 1, 0)), (0.22f, new Color(1, 1, 1, 1)), (0.7f, new Color(1, 1, 1, 1)), (1f, new Color(1, 1, 1, 0))),
                // Startbild und Spiegelung: INSTANCE_CUSTOM.z im Shader (Versatz 0–1, kein Weiterzählen, das Bild wählt der Shader aus der Uhr)
                AnimSpeedMin = 0f,
                AnimSpeedMax = 0f,
                AnimOffsetMin = 0f,
                AnimOffsetMax = 1f,
            },
        };
        AddChild(flammen);
    }

    /// <summary>Technik B: Funken als eigenes Partikelsystem (additiv).</summary>
    private void PartikelFunkenBauen(FeuerArt art)
    {
        var wind = Wind;
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

    /// <summary>Technik B: der Rauch aus beleuchteten Teilchen (Nutzer 29.10.2026: ab Mittel bei allen Feuern).</summary>
    private void PartikelRauchBauen(FeuerArt art)
    {
        var wind = Wind;
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

    /// <summary>Farbe und Deckkraft des Rauchs im Verlauf: Ruß macht ihn dunkler und dichter.</summary>
    private static Color RauchFarbe(FeuerArt art, float deckung)
    {
        var grau = Mathf.Lerp(0.72f, 0.16f, art.RauchRuss);
        return new Color(grau, grau, grau * 1.02f, deckung * Mathf.Clamp(0.45f + 0.5f * art.RauchMenge, 0f, 1f));
    }
}
