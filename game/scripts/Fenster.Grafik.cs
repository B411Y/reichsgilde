using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Grafikeinstellungen ab PR #169 (ein Teil der <c>partial class Fenster</c>): Voreinstellungen, Bildrate und VSync,
/// dynamische Auflösung, Detailstufe, Stimmung (Miniatur-Unschärfe, Wolkenschatten, Farbstimmung, Nachschärfen, Morgendunst,
/// Fahnen) und Barrierefreiheit (Größe der Oberfläche, Farbfilter). Gespeichert wie die übrigen in <c>user://einstellungen.cfg</c>.
///
/// Was das Fenster selbst einstellen kann, stellt es hier ein (Bildrate, VSync, Auflösung, Oberfläche, Fahnen, Farbfilter,
/// Nachschärfen). Was die Stadt betrifft (Unschärfe, Wolken, Dunst, Farbstimmung, Natur, Passanten), liest die Stadtansicht
/// bei <see cref="GrafikGeaendert"/> neu (Stadtansicht.Grafik.cs).
/// </summary>
public partial class Fenster
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Voreinstellungen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Alles, was eine Voreinstellung festlegt: die Regler, die Leistung kosten. Farbstimmung, Bildrate, VSync,
    /// dynamische Auflösung, Fahnen und Barrierefreiheit sind Geschmack oder Bildschirm und bleiben, wie der Spieler sie gestellt hat.</summary>
    public readonly record struct Grafikstufe(
        Kantenglaettung Kanten, Schattenqualitaet Schatten, Umgebungsverdeckung Verdeckung, bool Indirekt, bool Bloom,
        bool Fsr, int Renderaufloesung, bool Oberflaeche, int Natur, int Passanten, Feinheiten Feinheiten,
        Miniaturunschaerfe Unschaerfe, bool Wolken, Morgendunst Dunst);

    /// <summary>
    /// Die vier Stufen. Hoch entspricht den Grundwerten vor PR #169 (SMAA, Schatten Hoch, Verdeckung Mittel, Bloom), dazu der
    /// Morgendunst in der einfachen Fassung. Messung je Stufe: <c>docs/bereiche/darstellung.md</c>, Grafikeinstellungen (PR #169).
    /// </summary>
    public static Grafikstufe StufeVon(Voreinstellung v) => v switch
    {
        Voreinstellung.Niedrig => new(Kantenglaettung.Fxaa, Schattenqualitaet.Niedrig, Umgebungsverdeckung.Aus, false, false,
                                      true, 67, false, 50, 50, Feinheiten.Nah, Miniaturunschaerfe.Aus, false, Morgendunst.Aus),
        Voreinstellung.Mittel => new(Kantenglaettung.Smaa, Schattenqualitaet.Mittel, Umgebungsverdeckung.Niedrig, false, true,
                                     false, StandardRenderaufloesung, true, 75, 75, Feinheiten.Mittel, Miniaturunschaerfe.Aus, true, Morgendunst.Aus),
        Voreinstellung.Ultra => new(Kantenglaettung.Msaa4, Schattenqualitaet.Hoch, Umgebungsverdeckung.Hoch, true, true,
                                    false, StandardRenderaufloesung, true, 100, 100, Feinheiten.Weit, Miniaturunschaerfe.Leicht, true, Morgendunst.Hoch),
        _ => new(Kantenglaettung.Smaa, Schattenqualitaet.Hoch, Umgebungsverdeckung.Mittel, false, true,
                 false, StandardRenderaufloesung, true, 100, 100, Feinheiten.Weit, Miniaturunschaerfe.Leicht, true, Morgendunst.Niedrig),
    };

    /// <summary>Ohne gespeicherte Einstellungen (und bis die automatische Wahl fertig ist): Hoch.</summary>
    public const Voreinstellung StandardVoreinstellung = Voreinstellung.Hoch;

    /// <summary>Die Regler, wie sie gerade stehen.</summary>
    public static Grafikstufe AktuelleStufe => new(
        AktuelleKantenglaettung, AktuelleSchattenqualitaet, AktuelleUmgebungsverdeckung, IndirektesLichtAn, BloomAn,
        FsrAn, Renderaufloesung, OberflaechendetailsAn, NaturDichte, PassantenMenge, AktuelleFeinheiten,
        AktuelleUnschaerfe, WolkenschattenAn, AktuellerDunst);

    /// <summary>
    /// Welche Voreinstellung die Regler gerade zeigen – „Eigene“, sobald einer abweicht. Gerechnet statt gespeichert: Stellt der
    /// Spieler einen Regler zurück, steht wieder der Name der Stufe da. Die Renderauflösung zählt nur, wenn FSR an ist.
    /// </summary>
    public static Voreinstellung AktuelleVoreinstellung
    {
        get
        {
            var jetzt = AktuelleStufe;
            foreach (var v in new[] { Voreinstellung.Niedrig, Voreinstellung.Mittel, Voreinstellung.Hoch, Voreinstellung.Ultra })
            {
                var stufe = StufeVon(v);
                if (!jetzt.Fsr) stufe = stufe with { Renderaufloesung = jetzt.Renderaufloesung };
                if (stufe == jetzt) return v;
            }
            return Voreinstellung.Eigene;
        }
    }

    /// <summary>Stellt alle Regler der Stufe ein (<paramref name="speichern"/> false: nur für diesen Start, z. B. Startoption oder Messung).</summary>
    public static void VoreinstellungSetzen(Voreinstellung v, bool speichern = true)
    {
        if (v == Voreinstellung.Eigene) return;
        var s = StufeVon(v);
        _sammeln = true; // die Ereignisse einmal am Ende, nicht je Regler
        KantenglaettungSetzen(s.Kanten, speichern);
        SchattenqualitaetSetzen(s.Schatten, speichern);
        UmgebungsverdeckungSetzen(s.Verdeckung, speichern);
        IndirektesLichtSetzen(s.Indirekt, speichern);
        BloomSetzen(s.Bloom, BloomStaerke, speichern);
        FsrSetzen(s.Fsr, s.Fsr ? s.Renderaufloesung : Renderaufloesung, speichern);
        OberflaechendetailsSetzen(s.Oberflaeche, speichern);
        NaturDichteSetzen(s.Natur, speichern);
        PassantenMengeSetzen(s.Passanten, speichern);
        FeinheitenSetzen(s.Feinheiten, speichern);
        UnschaerfeSetzen(s.Unschaerfe, speichern);
        WolkenschattenSetzen(s.Wolken, speichern);
        DunstSetzen(s.Dunst, speichern);
        _sammeln = false;
        GrafikGeaendert?.Invoke();
    }

    /// <summary>Wird ausgelöst, wenn sich eine der neuen Einstellungen ändert: Stadt und Raumansicht stellen sich um (Stadtansicht.Grafik.cs).</summary>
    public static event Action? GrafikGeaendert;

    private static bool _sammeln;

    private static void Melden()
    {
        if (!_sammeln) GrafikGeaendert?.Invoke();
    }

    /// <summary>Liest die neuen Einstellungen beim Start (aus <see cref="_Ready"/>). Fehlt ein Schlüssel, gilt der Wert der Stufe Hoch bzw. der Grundwert.</summary>
    private static void GrafikLaden(ConfigFile datei, bool geladen)
    {
        Variant Wert(string schluessel, Variant standard) => geladen ? datei.GetValue("anzeige", schluessel, standard) : standard;
        T Art<T>(string schluessel, T standard) where T : struct, Enum =>
            Enum.TryParse<T>(Wert(schluessel, standard.ToString()).AsString(), out var w) ? w : standard;
        var hoch = StufeVon(StandardVoreinstellung);
        _sammeln = true;
        BildrateSetzen(Wert("bildrate", 0).AsInt32(), false);
        VsyncSetzen(Art("vsync", VSync.An), false);
        DynamischeAufloesungSetzen(Wert("dynamisch", 0).AsInt32(), false);
        NaturDichteSetzen(Wert("natur", hoch.Natur).AsInt32(), false);
        PassantenMengeSetzen(Wert("passanten", hoch.Passanten).AsInt32(), false);
        FeinheitenSetzen(Art("feinheiten", hoch.Feinheiten), false);
        UnschaerfeSetzen(Art("unschaerfe", hoch.Unschaerfe), false);
        WolkenschattenSetzen(Wert("wolken", hoch.Wolken).AsBool(), false);
        DunstSetzen(Art("dunst", hoch.Dunst), false);
        FarbstimmungSetzen(Art("tonemapping", Tonemapping.Bisher), Wert("helligkeit", 100).AsInt32(),
                           Wert("kontrast", 100).AsInt32(), Wert("saettigung", 100).AsInt32(), false);
        SchaerfeSetzen(Wert("schaerfe", StandardSchaerfe).AsInt32(), false);
        FahnenSetzen(Wert("fahnen", true).AsBool(), false);
        OberflaechengroesseSetzen(Wert("uigroesse", 100).AsInt32(), false);
        FarbfilterSetzen(Art("farbfilter", Farbfilter.Aus), false);
        _sammeln = false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bildrate, VSync und dynamische Auflösung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Höchste Bildrate (Bilder je Sekunde), 0 = unbegrenzt.</summary>
    public static int Bildrate { get; private set; }

    public static VSync AktuellesVsync { get; private set; } = VSync.An;

    /// <summary>Ziel-Bildrate der dynamischen Auflösung, 0 = aus.</summary>
    public static int DynamischeAufloesung { get; private set; }

    /// <summary>Die Renderauflösung, die die dynamische Auflösung gerade gewählt hat (0,5–1,0).</summary>
    public static float DynamischerAnteil { get; private set; } = 1f;

    /// <summary>Grenzen der dynamischen Auflösung: darunter wird das Bild zu weich, darüber gibt es nichts zu gewinnen.</summary>
    public const float DynamischMin = 0.5f, DynamischMax = 1f;

    /// <summary>Begrenzt die Bildrate (<see cref="Engine.MaxFps"/>, 0 = unbegrenzt). Spart Strom und hält Lüfter leise.</summary>
    public static void BildrateSetzen(int bilder, bool speichern = true)
    {
        Bildrate = Math.Max(0, bilder);
        Engine.MaxFps = Bildrate;
        if (speichern) Speichern("bildrate", Bildrate);
        Melden();
    }

    /// <summary>VSync: Aus (kein Warten, Risse möglich), An (wartet auf den Bildschirm), Adaptiv (wartet nur, solange das Spiel schnell genug ist).</summary>
    public static void VsyncSetzen(VSync art, bool speichern = true)
    {
        AktuellesVsync = art;
        if (DisplayServer.GetName() != "headless")
            DisplayServer.WindowSetVsyncMode(art switch
            {
                VSync.Aus => DisplayServer.VSyncMode.Disabled,
                VSync.Adaptiv => DisplayServer.VSyncMode.Adaptive,
                _ => DisplayServer.VSyncMode.Enabled,
            });
        if (speichern) Speichern("vsync", art.ToString());
        Melden();
    }

    /// <summary>
    /// Schaltet die dynamische Auflösung ein (<paramref name="zielBildrate"/> &gt; 0) oder aus. Eingeschaltet regelt
    /// <see cref="DynamischRegeln"/> die Renderauflösung zwischen <see cref="DynamischMin"/> und <see cref="DynamischMax"/>, mit FSR 2.2
    /// (wenn an) oder bilinear hochgerechnet; die feste Renderauflösung von FSR ruht so lange.
    /// </summary>
    public static void DynamischeAufloesungSetzen(int zielBildrate, bool speichern = true)
    {
        DynamischeAufloesung = Math.Max(0, zielBildrate);
        DynamischerAnteil = DynamischMax;
        _dynamischGlatt = 0;
        _dynamischWarten = DynamischRuhe;
        SkalierungAnwenden();
        Messen(DynamischeAufloesung > 0 || _messungLaeuft || _auto is not null);
        if (speichern) Speichern("dynamisch", DynamischeAufloesung);
        Melden();
    }

    /// <summary>
    /// Stellt die 3D-Auflösung des Hauptfensters ein: FSR 2.2 oder bilinear, fest oder dynamisch, dazu die Schärfe von FSR.
    /// Ohne FSR und ohne dynamische Auflösung rechnet Godot in voller Größe (Faktor 1,0).
    /// </summary>
    private static void SkalierungAnwenden()
    {
        if (_instanz?.GetTree().Root is not { } fenster) return;
        var anteil = DynamischeAufloesung > 0 ? DynamischerAnteil : FsrAn ? Renderaufloesung / 100f : 1f;
        fenster.Scaling3DMode = FsrAn ? Viewport.Scaling3DModeEnum.Fsr2 : Viewport.Scaling3DModeEnum.Bilinear;
        if (!Mathf.IsEqualApprox(fenster.Scaling3DScale, anteil)) fenster.Scaling3DScale = anteil;
        // Schärfe von FSR: 0 = am schärfsten, 2 = kaum nachgeschärft. Der Regler 0–100 läuft umgekehrt.
        fenster.FsrSharpness = 2f * (1 - Schaerfe / 100f);
    }

    // Regelung der dynamischen Auflösung (jedes Bild in _Process)
    private static float _dynamischGlatt;
    private static double _dynamischWarten;

    /// <summary>So lange (Sekunden) wartet die Regelung nach einer Änderung: Die Bildzeit muss sich erst setzen, FSR braucht ein paar Bilder Verlauf.</summary>
    private const double DynamischRuhe = 2.0;

    /// <summary>
    /// Ruhig geregelt statt jedes Bild: Die GPU-Zeit je Bild wird über etwa eine Sekunde geglättet. Liegt sie über 90 % der Zeit, die
    /// die Ziel-Bildrate erlaubt, geht die Auflösung einen Schritt herunter; liegt sie so tief, dass auch der nächste Schritt hinauf
    /// noch unter 75 % bliebe, einen Schritt hinauf. Dazwischen bleibt sie stehen – das verhindert ein Pumpen. Die GPU-Zeit
    /// (nicht die Bildrate) ist das Maß, denn VSync und Bildratenbegrenzung halten die Bildrate fest, auch wenn Luft wäre.
    /// </summary>
    private void DynamischRegeln(double delta)
    {
        if (DynamischeAufloesung <= 0) return;
        var gpu = GpuZeit();
        if (gpu <= 0) return;
        _dynamischGlatt = _dynamischGlatt <= 0 ? gpu : Mathf.Lerp(_dynamischGlatt, gpu, 0.05f);
        _dynamischWarten -= delta;
        if (_dynamischWarten > 0) return;
        var budget = 1000f / DynamischeAufloesung;
        const float schritt = 0.05f;
        var neu = DynamischerAnteil;
        // Die Arbeit wächst etwa mit der Zahl der Bildpunkte, also mit dem Quadrat des Anteils.
        if (_dynamischGlatt > 0.9f * budget) neu = DynamischerAnteil - schritt;
        else if (_dynamischGlatt * Mathf.Pow((DynamischerAnteil + schritt) / DynamischerAnteil, 2) < 0.75f * budget) neu = DynamischerAnteil + schritt;
        neu = Mathf.Clamp(Mathf.Snapped(neu, schritt), DynamischMin, DynamischMax);
        if (Mathf.IsEqualApprox(neu, DynamischerAnteil)) return;
        GD.Print($"Dynamische Auflösung: {DynamischerAnteil * 100:0} % → {neu * 100:0} % (GPU {_dynamischGlatt:0.0} ms je Bild, Ziel {budget:0.0} ms bei {DynamischeAufloesung} Bildern/s)");
        DynamischerAnteil = neu;
        SkalierungAnwenden();
        _dynamischWarten = DynamischRuhe;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Detailstufe (nur Darstellung, die Simulation bleibt gleich)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Anteil der Streupflanzen, Büsche, Steine und Hölzer in Prozent (25–100); Bäume dünnen nur halb so stark aus.</summary>
    public static int NaturDichte { get; private set; } = 100;

    /// <summary>Anteil der Einwohner, die als Figur auf den Straßen zu sehen sind, in Prozent (25–100).</summary>
    public static int PassantenMenge { get; private set; } = 100;

    public static Feinheiten AktuelleFeinheiten { get; private set; } = Feinheiten.Weit;

    public static void NaturDichteSetzen(int prozent, bool speichern = true)
    {
        NaturDichte = Math.Clamp(prozent, 25, 100);
        if (speichern) Speichern("natur", NaturDichte);
        Melden();
    }

    public static void PassantenMengeSetzen(int prozent, bool speichern = true)
    {
        PassantenMenge = Math.Clamp(prozent, 25, 100);
        if (speichern) Speichern("passanten", PassantenMenge);
        Melden();
    }

    /// <summary>Sichtweite der Feinheiten: bis zu welchem Zoom Bodenpflanzen, Steine und Holz gezeichnet werden, und ob Godot weit
    /// entfernte Modelle vereinfachen darf (nur „Nah“, siehe Stadtansicht.Grafik.cs).</summary>
    public static void FeinheitenSetzen(Feinheiten f, bool speichern = true)
    {
        AktuelleFeinheiten = f;
        if (speichern) Speichern("feinheiten", f.ToString());
        Melden();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Stimmung
    // ═════════════════════════════════════════════════════════════════════════

    public static Miniaturunschaerfe AktuelleUnschaerfe { get; private set; } = Miniaturunschaerfe.Leicht;
    public static bool WolkenschattenAn { get; private set; } = true;
    public static Morgendunst AktuellerDunst { get; private set; } = Morgendunst.Niedrig;
    public static Tonemapping AktuellesTonemapping { get; private set; } = Tonemapping.Bisher;

    /// <summary>Helligkeit, Kontrast und Sättigung in Prozent (50–150, 100 = unverändert).</summary>
    public static int Helligkeit { get; private set; } = 100;
    public static int Kontrast { get; private set; } = 100;
    public static int Saettigung { get; private set; } = 100;

    /// <summary>Nachschärfen 0–100.</summary>
    public static int Schaerfe { get; private set; } = StandardSchaerfe;

    /// <summary>Ohne gespeicherte Wahl: leicht nachgeschärft – bei FSR entspricht das Godots Grundwert 0,2 nicht ganz, sondern 1,2 (FSR 2.2 schärft schon selbst).</summary>
    public const int StandardSchaerfe = 40;

    public static bool FahnenBewegen { get; private set; } = true;

    /// <summary>Stärke des Windes für die Fahnen (globale Shader-Uniform <c>wind_staerke</c>, Grundwert in <c>project.godot</c>).</summary>
    private const float Windstaerke = 0.5f;

    public static void UnschaerfeSetzen(Miniaturunschaerfe stufe, bool speichern = true)
    {
        AktuelleUnschaerfe = stufe;
        if (speichern) Speichern("unschaerfe", stufe.ToString());
        Melden();
    }

    public static void WolkenschattenSetzen(bool an, bool speichern = true)
    {
        WolkenschattenAn = an;
        if (speichern) Speichern("wolken", an);
        Melden();
    }

    public static void DunstSetzen(Morgendunst stufe, bool speichern = true)
    {
        AktuellerDunst = stufe;
        if (speichern) Speichern("dunst", stufe.ToString());
        Melden();
    }

    public static void FarbstimmungSetzen(Tonemapping art, int helligkeit, int kontrast, int saettigung, bool speichern = true)
    {
        AktuellesTonemapping = art;
        Helligkeit = Math.Clamp(helligkeit, 50, 150);
        Kontrast = Math.Clamp(kontrast, 50, 150);
        Saettigung = Math.Clamp(saettigung, 0, 150);
        if (speichern)
        {
            Speichern("tonemapping", art.ToString());
            Speichern("helligkeit", Helligkeit);
            Speichern("kontrast", Kontrast);
            Speichern("saettigung", Saettigung);
        }
        Melden();
    }

    /// <summary>
    /// Nachschärfen: Mit FSR 2.2 stellt es dessen eigene Schärfe ein, sonst legt das Fenster einen leichten Schärfeschritt über das
    /// fertige 3D-Bild (<see cref="Bildfilter"/>, unter allen Tafeln – die Schrift bleibt, wie sie ist).
    /// </summary>
    public static void SchaerfeSetzen(int wert, bool speichern = true)
    {
        Schaerfe = Math.Clamp(wert, 0, 100);
        SkalierungAnwenden();
        _instanz?._filter?.Auffrischen();
        if (speichern) Speichern("schaerfe", Schaerfe);
        Melden();
    }

    /// <summary>Fahnen bewegen an/aus: Bei Wind 0 hängen alle Fahnen still (fahne.gdshader).</summary>
    public static void FahnenSetzen(bool an, bool speichern = true)
    {
        FahnenBewegen = an;
        RenderingServer.GlobalShaderParameterSet("wind_staerke", an ? Windstaerke : 0f);
        if (speichern) Speichern("fahnen", an);
        Melden();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Barrierefreiheit
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Größe der Oberfläche in Prozent (75–200): alle Tafeln, Schriften und Knöpfe.</summary>
    public static int Oberflaechengroesse { get; private set; } = 100;

    public static Farbfilter AktuellerFarbfilter { get; private set; } = Farbfilter.Aus;

    /// <summary>
    /// Vergrößert oder verkleinert die ganze Oberfläche über den Faktor des Hauptfensters (<see cref="Window.ContentScaleFactor"/>):
    /// Godot zeichnet alle Tafeln und Schriften größer, die 3D-Ansicht bleibt in voller Auflösung. Tafeln, die dann nicht mehr ins
    /// Fenster passen, rollen wie bisher (<see cref="Rollbereich"/>).
    /// </summary>
    public static void OberflaechengroesseSetzen(int prozent, bool speichern = true)
    {
        Oberflaechengroesse = Math.Clamp(prozent, 75, 200);
        if (_instanz?.GetTree().Root is { } fenster) fenster.ContentScaleFactor = Oberflaechengroesse / 100f;
        if (speichern) Speichern("uigroesse", Oberflaechengroesse);
        Melden();
    }

    /// <summary>Farbfilter für Farbsehschwäche: verschiebt Farbunterschiede, die schwer zu sehen sind, in Farben, die gut zu sehen sind (<see cref="Bildfilter"/>).</summary>
    public static void FarbfilterSetzen(Farbfilter filter, bool speichern = true)
    {
        AktuellerFarbfilter = filter;
        _instanz?._filter?.Auffrischen();
        if (speichern) Speichern("farbfilter", filter.ToString());
        Melden();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  GPU-Zeit, automatische Wahl und Messung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Schaltet die Zeitmessung der Grafikkarte am Hauptfenster ein oder aus (kostet fast nichts, aber nur, wenn jemand sie braucht).</summary>
    private static void Messen(bool an)
    {
        if (_instanz?.GetTree().Root is not { } fenster || DisplayServer.GetName() == "headless") return;
        RenderingServer.ViewportSetMeasureRenderTime(fenster.GetViewportRid(), an);
    }

    /// <summary>GPU-Zeit des letzten Bildes in Millisekunden (0, wenn nicht gemessen wird oder der Treiber es nicht kann).</summary>
    private static float GpuZeit() =>
        _instanz?.GetTree().Root is { } fenster ? (float)RenderingServer.ViewportGetMeasuredRenderTimeGpu(fenster.GetViewportRid()) : 0f;

    /// <summary>War beim Start keine Datei mit Einstellungen da? Dann wählt das Spiel die Voreinstellung selbst (<see cref="AutomatischWaehlen"/>).</summary>
    public static bool ErsterStart { get; private set; }

    /// <summary>Ergebnis der letzten automatischen Wahl (für die Tafel), leer, solange keine lief.</summary>
    public static string AutomatischErgebnis { get; private set; } = "";

    /// <summary>Läuft gerade eine automatische Wahl?</summary>
    public static bool WaehltAutomatisch => _auto is not null;

    /// <summary>Stand der automatischen Wahl: welche Stufe gerade gemessen wird, wie lange schon, und die Zeiten.</summary>
    private sealed class Autowahl
    {
        public int Stufe = 3; // Ultra, Hoch, Mittel, Niedrig – von oben nach unten
        public double Zeit = -AutoVorlauf;
        public readonly List<float> Zeiten = new();
        public readonly List<string> Protokoll = new();
        public Grafikstufe Vorher;
    }

    private static Autowahl? _auto;

    /// <summary>Vor der ersten Messung warten (Sekunden): Die Stadt baut sich auf, Shader werden übersetzt.</summary>
    private const double AutoVorlauf = 3;
    /// <summary>Je Stufe: kurz einschwingen (neue Shader, Schattenkarte), dann messen.</summary>
    private const double AutoEinschwingen = 0.6, AutoMessdauer = 1.2;

    /// <summary>
    /// GPU-Zeit je Bild, die eine Stufe höchstens brauchen darf: 60 Bilder je Sekunde mit Luft (16,7 ms · 0,7). Gemessen wird die
    /// Stadt im Hintergrund – im Spiel kommen Passanten, Karren und Tafeln dazu, darum die Reserve.
    /// </summary>
    private const float AutoBudget = 1000f / 60f * 0.7f;

    /// <summary>
    /// Misst die Stufen von Ultra abwärts je etwa zwei Sekunden und behält die höchste, deren GPU-Zeit (Median) ins Budget passt; passt
    /// keine, wird es Niedrig. Läuft beim ersten Start im Hintergrund des Hauptmenüs und auf Knopfdruck in der Tafel.
    /// </summary>
    public static void AutomatischWaehlen()
    {
        if (_instanz is null || _auto is not null) return;
        if (DisplayServer.GetName() == "headless") return;
        _auto = new Autowahl { Vorher = AktuelleStufe, Zeit = ErsterStart ? -AutoVorlauf : -0.5 };
        Messen(true);
        _instanz.HinweisZeigen("Die Grafik wird für diesen Rechner eingestellt …");
        VoreinstellungSetzen(Voreinstellung.Ultra, speichern: false);
    }

    private void AutomatischBild(double delta)
    {
        if (_auto is not { } a) return;
        a.Zeit += delta;
        if (a.Zeit < AutoEinschwingen) return;
        var gpu = GpuZeit();
        if (gpu <= 0) gpu = (float)(delta * 1000); // ohne GPU-Zeit (manche Treiber): die Bildzeit
        a.Zeiten.Add(gpu);
        if (a.Zeit < AutoEinschwingen + AutoMessdauer) return;

        a.Zeiten.Sort();
        var median = a.Zeiten[a.Zeiten.Count / 2];
        var stufe = (Voreinstellung)a.Stufe;
        a.Protokoll.Add($"{stufe} {median:0.0} ms");
        if (median <= AutoBudget || a.Stufe == 0)
        {
            VoreinstellungSetzen(stufe); // gespeichert
            AutomatischErgebnis = $"Gewählt: {Name(stufe)} ({string.Join(", ", a.Protokoll)} GPU-Zeit je Bild, Grenze {AutoBudget:0.0} ms)";
            GD.Print("Automatische Wahl der Grafik: " + AutomatischErgebnis);
            _auto = null;
            Messen(DynamischeAufloesung > 0 || _messungLaeuft);
            HinweisZeigen($"Grafik: Voreinstellung „{Name(stufe)}“ gewählt. Ändern unter Einstellungen.");
            return;
        }
        a.Stufe--;
        a.Zeit = 0;
        a.Zeiten.Clear();
        VoreinstellungSetzen((Voreinstellung)a.Stufe, speichern: false);
    }

    public static string Name(Voreinstellung v) => v switch
    {
        Voreinstellung.Niedrig => "Niedrig",
        Voreinstellung.Mittel => "Mittel",
        Voreinstellung.Hoch => "Hoch",
        Voreinstellung.Ultra => "Ultra",
        _ => "Eigene",
    };

    // ── Messung (Startoption --messung=<Sekunden>) ──────────────────────────

    private static bool _messungLaeuft;
    private double _messungDauer, _messungZeit, _messungSekunde;
    private readonly List<double> _messBildzeiten = new();
    private readonly List<float> _messGpu = new();
    private int _messBilderSekunde;
    private double _messGpuSekunde;

    /// <summary>Nach dem Laden so lange warten (Sekunden), bevor gemessen wird: Stadt, Figuren und Shader sind dann fertig.</summary>
    private const double MessVorlauf = 8;

    /// <summary>
    /// Startoption <c>--messung=30</c>: Nach dem Vorlauf misst das Spiel so viele Sekunden lang jede Bildzeit und die GPU-Zeit, schreibt je
    /// Sekunde eine Zeile ins Protokoll und am Ende Mittel, 1-%-Tief (Bilder/s im langsamsten Hundertstel der Bilder), Bildzeit und GPU-Zeit
    /// – dann beendet es sich. Für Vergleiche feste Kamera (<c>--blick</c>), feste Uhrzeit (<c>--startstunde</c>) und VSync aus.
    /// </summary>
    public static void MessungStarten(double sekunden)
    {
        if (_instanz is not { } f) return;
        _messungLaeuft = true;
        f._messungDauer = sekunden;
        f._messungZeit = -MessVorlauf;
        Messen(true);
    }

    private void MessungBild(double delta)
    {
        if (!_messungLaeuft) return;
        _messungZeit += delta;
        if (_messungZeit < 0) return;
        _messBildzeiten.Add(delta * 1000);
        var gpu = GpuZeit();
        _messGpu.Add(gpu);
        _messBilderSekunde++;
        _messGpuSekunde += gpu;
        _messungSekunde += delta;
        if (_messungSekunde >= 1)
        {
            GD.Print($"Messung {(int)_messungZeit,3} s: {_messBilderSekunde / _messungSekunde:0.0} Bilder/s, Bildzeit {_messungSekunde * 1000 / _messBilderSekunde:0.0} ms, " +
                     $"GPU {_messGpuSekunde / _messBilderSekunde:0.0} ms, {RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame)} Zeichenaufrufe");
            _messungSekunde = 0;
            _messBilderSekunde = 0;
            _messGpuSekunde = 0;
        }
        if (_messungZeit < _messungDauer) return;

        var zeiten = _messBildzeiten.OrderByDescending(z => z).ToList();
        var schlimmste = zeiten.Take(Math.Max(1, zeiten.Count / 100)).Average();
        var mittel = zeiten.Average();
        GD.Print($"Messung Ergebnis: {zeiten.Count / zeiten.Sum() * 1000:0.0} Bilder/s im Mittel, 1-%-Tief {1000 / schlimmste:0.0} Bilder/s, " +
                 $"Bildzeit {mittel:0.00} ms, GPU {_messGpu.Average():0.00} ms, Voreinstellung {Name(AktuelleVoreinstellung)}, " +
                 $"Renderauflösung {GetTree().Root.Scaling3DScale * 100:0} %, {RenderingServer.GetVideoAdapterName()}");
        _messungLaeuft = false;
        GetTree().Quit();
    }

    public override void _Process(double delta)
    {
        AutomatischBild(delta);
        DynamischRegeln(delta);
        MessungBild(delta);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Startoptionen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Grafik-Startoptionen (<c>--kanten</c>, <c>--voreinstellung</c>, <c>--dunst</c> …, Liste in Hauptszene.cs): gelten nur für diesen
    /// Start und ändern die gespeicherte Wahl nicht – für Vergleichsbilder und Messungen. Zuerst die Voreinstellung, danach die einzelnen
    /// Regler, die sie überschreiben.
    /// </summary>
    public static void StartoptionenAnwenden(Dictionary<string, string> optionen)
    {
        static bool An(string wert) => !wert.Equals("Aus", StringComparison.OrdinalIgnoreCase) && wert != "0";
        bool Zahl(string schluessel, out int zahl)
        {
            zahl = 0;
            return optionen.TryGetValue(schluessel, out var text) && int.TryParse(text, out zahl);
        }
        bool Art<T>(string schluessel, out T wert) where T : struct, Enum
        {
            wert = default;
            return optionen.TryGetValue(schluessel, out var text) && Enum.TryParse(text, true, out wert);
        }

        if (Art<Voreinstellung>("voreinstellung", out var voreinstellung)) VoreinstellungSetzen(voreinstellung, speichern: false);
        if (Art<Kantenglaettung>("kanten", out var kanten)) KantenglaettungSetzen(kanten, speichern: false);
        if (Art<Schattenqualitaet>("schatten", out var schatten)) SchattenqualitaetSetzen(schatten, speichern: false);
        if (optionen.TryGetValue("oberflaeche", out var oberflaeche)) OberflaechendetailsSetzen(An(oberflaeche), speichern: false);
        if (optionen.ContainsKey("bloom") || optionen.ContainsKey("bloomstaerke")) // was fehlt, bleibt wie eingestellt
            BloomSetzen(optionen.TryGetValue("bloom", out var bloom) ? An(bloom) : BloomAn,
                        Zahl("bloomstaerke", out var bloomStaerke) ? bloomStaerke : BloomStaerke, speichern: false);
        if (Art<Umgebungsverdeckung>("ssao", out var ssao)) UmgebungsverdeckungSetzen(ssao, speichern: false);
        if (optionen.TryGetValue("ssil", out var ssil)) IndirektesLichtSetzen(An(ssil), speichern: false);
        if (optionen.ContainsKey("fsr") || optionen.ContainsKey("renderaufloesung")) // ebenso
            FsrSetzen(optionen.TryGetValue("fsr", out var fsr) ? An(fsr) : FsrAn,
                      Zahl("renderaufloesung", out var aufloesung) ? aufloesung : Renderaufloesung, speichern: false);
        if (Zahl("bildrate", out var bildrate)) BildrateSetzen(bildrate, speichern: false);
        if (Art<VSync>("vsync", out var vsync)) VsyncSetzen(vsync, speichern: false);
        if (optionen.TryGetValue("dynamisch", out var dynamisch)) DynamischeAufloesungSetzen(int.TryParse(dynamisch, out var ziel) ? ziel : 0, speichern: false);
        if (Zahl("natur", out var natur)) NaturDichteSetzen(natur, speichern: false);
        if (Zahl("passanten", out var passanten)) PassantenMengeSetzen(passanten, speichern: false);
        if (Art<Feinheiten>("feinheiten", out var feinheiten)) FeinheitenSetzen(feinheiten, speichern: false);
        if (Art<Miniaturunschaerfe>("tiltshift", out var tiltshift)) UnschaerfeSetzen(tiltshift, speichern: false);
        if (optionen.TryGetValue("wolken", out var wolken)) WolkenschattenSetzen(An(wolken), speichern: false);
        if (Art<Morgendunst>("dunst", out var dunst)) DunstSetzen(dunst, speichern: false);
        if (optionen.ContainsKey("tonemapping") || optionen.ContainsKey("helligkeit") || optionen.ContainsKey("kontrast") || optionen.ContainsKey("saettigung"))
            FarbstimmungSetzen(Art<Tonemapping>("tonemapping", out var tonemapping) ? tonemapping : AktuellesTonemapping,
                               Zahl("helligkeit", out var hell) ? hell : Helligkeit, Zahl("kontrast", out var kontrast) ? kontrast : Kontrast,
                               Zahl("saettigung", out var saettigung) ? saettigung : Saettigung, speichern: false);
        if (Zahl("schaerfe", out var schaerfe)) SchaerfeSetzen(schaerfe, speichern: false);
        if (optionen.TryGetValue("fahnen", out var fahnen)) FahnenSetzen(An(fahnen), speichern: false);
        if (Zahl("uigroesse", out var uigroesse)) OberflaechengroesseSetzen(uigroesse, speichern: false);
        if (Art<Farbfilter>("farbfilter", out var farbfilter)) FarbfilterSetzen(farbfilter, speichern: false);
        if (optionen.GetValueOrDefault("automatisch") == "1") AutomatischWaehlen();
        if (Zahl("messung", out var messung) && messung > 0) MessungStarten(messung);
    }
}

/// <summary>Die Voreinstellungen der Grafik (Werte: <see cref="Fenster.StufeVon"/>); „Eigene“, sobald ein Regler abweicht.</summary>
public enum Voreinstellung { Niedrig, Mittel, Hoch, Ultra, Eigene }

/// <summary>VSync-Arten (Beschreibung: <see cref="EinstellungenAnzeige"/>).</summary>
public enum VSync { Aus, An, Adaptiv }

/// <summary>Sichtweite der Feinheiten (Bodenpflanzen, Steine, Holz; Werte: Stadtansicht.Grafik.cs).</summary>
public enum Feinheiten { Nah, Mittel, Weit }

/// <summary>Stufen der Miniatur-Unschärfe (Tilt-Shift; Werte: Stadtansicht.Grafik.cs).</summary>
public enum Miniaturunschaerfe { Aus, Leicht, Stark }

/// <summary>Morgendunst und Lichtstrahlen: Niedrig = einfacher Höhennebel, Hoch = volumetrischer Nebel mit Lichtbahnen (Stadtansicht.Grafik.cs).</summary>
public enum Morgendunst { Aus, Niedrig, Hoch }

/// <summary>Wie das helle Bild auf den Bildschirm gebracht wird: bisher (linear) oder AgX (weichere Lichter, natürlichere Farben).</summary>
public enum Tonemapping { Bisher, Agx }

/// <summary>Farbfilter für Farbsehschwäche (Daltonisierung; Shader in <see cref="Bildfilter"/>).</summary>
public enum Farbfilter { Aus, Protanopie, Deuteranopie, Tritanopie }
