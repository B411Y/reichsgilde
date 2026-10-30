using Godot;

namespace Reichsgilde;

/// <summary>
/// Anzeige-Einstellungen: Fenster oder Vollbild (auch F11, überall), die Kantenglättung, die Schattenqualität, der Bloom,
/// die Umgebungsverdeckung (SSAO), das indirekte Licht (SSIL), die globale Beleuchtung (SDFGI) und die Hochskalierung mit FSR 2.2;
/// dazu (Fenster.Grafik.cs) Voreinstellungen, Bildrate, VSync, dynamische Auflösung, Detailstufe, Stimmung und Barrierefreiheit.
/// Geändert werden sie im Fenster „Einstellungen“ (<see cref="EinstellungenAnzeige"/>), gespeichert in
/// <c>user://einstellungen.cfg</c> und beim Start wieder angewendet.
///
/// Läuft als Autoload (project.godot → [autoload]): Godot legt den Knoten beim Start einmal an und behält
/// ihn über alle Szenenwechsel. So gilt F11 im Hauptmenü wie im Spiel, und die Einstellung wird nur einmal geladen.
///
/// Die Oberfläche hat eine feste Pixelgröße (mal der Einstellung „Größe der Oberfläche“): Ein größeres Fenster zeigt mehr Stadt,
/// Tafeln, die nicht passen, rollen (<see cref="Rollbereich"/>). Eine Mindestgröße gibt es bewusst nicht.
/// </summary>
public partial class Fenster : Node
{
    private const string Datei = "user://einstellungen.cfg";
    private static Fenster? _instanz;
    private Label? _hinweis;
    private int _hinweisNummer;

    public static bool IstVollbild =>
        DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;

    /// <summary>
    /// Der Autoload verlässt den Baum erst beim Beenden des Spiels, als Letzter nach der Szene. Dann die Schriften loslassen
    /// und die Müllabfuhr von C# sofort laufen lassen, solange Godot noch ganz steht: Godot-Objekte, deren C#-Hülle schon
    /// Müll ist, werden sonst erst während des Herunterfahrens auf einem eigenen Faden freigegeben – je nach Zeitpunkt
    /// ein Leck oder ein Absturz im Rauchtest.
    /// </summary>
    public override void _ExitTree()
    {
        _sigterm?.Dispose();
        _sigterm = null;
        Grafikschnittstelle.Beenden();
        Oberflaechenstil.Freigeben();
        Schriften.Freigeben();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>
    /// SIGTERM (Linux, macOS: <c>kill</c>, der Klicktest, das Abmelden des Systems) beendet das Spiel über die Hauptschleife. Ohne das
    /// beendet die .NET-Laufzeit den Prozess aus ihrem Signal-Faden mit <c>exit()</c>, während Godot noch zeichnet und Shader speichert:
    /// Die statischen Objekte werden unter den laufenden Fäden abgebaut, und das Spiel stürzt beim Beenden ab (Signal 11).
    /// </summary>
    private static System.Runtime.InteropServices.PosixSignalRegistration? _sigterm;

    private void SigtermAbfangen()
    {
        if (OperatingSystem.IsWindows() || _sigterm is not null) return;
        _sigterm = System.Runtime.InteropServices.PosixSignalRegistration.Create(System.Runtime.InteropServices.PosixSignal.SIGTERM, zusammenhang =>
        {
            zusammenhang.Cancel = true; // nicht sofort beenden; Godot tut es im nächsten Bild selbst
            Callable.From(() => GetTree().Quit()).CallDeferred();
        });
    }

    public override void _Ready()
    {
        _instanz = this;
        SigtermAbfangen();
        Schriften.Einrichten(GetTree());
        Oberflaechenstil.Einrichten();
        Grafikschnittstelle.Starten(this);
        KlangOberflaeche.Einrichten(GetTree());
        var einstellungen = new ConfigFile();
        var geladen = einstellungen.Load(Datei) == Error.Ok;
        if (geladen && einstellungen.GetValue("anzeige", "vollbild", false).AsBool())
            Setzen(true, speichern: false);
        var kanten = geladen ? einstellungen.GetValue("anzeige", "kantenglaettung", "").AsString() : "";
        KantenglaettungSetzen(Enum.TryParse<Kantenglaettung>(kanten, out var k) ? k : StandardKantenglaettung, speichern: false);
        var schatten = geladen ? einstellungen.GetValue("anzeige", "schattenqualitaet", "").AsString() : "";
        SchattenqualitaetSetzen(Enum.TryParse<Schattenqualitaet>(schatten, out var q) ? q : Schattenqualitaet.Hoch, speichern: false);
        // Alte Dateien ohne die Schlüssel bekommen den Standard (An, 50 %).
        BloomSetzen(geladen ? einstellungen.GetValue("anzeige", "bloom", true).AsBool() : true,
                    geladen ? einstellungen.GetValue("anzeige", "bloomstaerke", StandardBloomStaerke).AsInt32() : StandardBloomStaerke,
                    speichern: false);
        // Alte Dateien ohne die Schlüssel: Umgebungsverdeckung Mittel, indirektes Licht aus, FSR aus mit 67 %.
        var verdeckung = geladen ? einstellungen.GetValue("anzeige", "ssao", "").AsString() : "";
        UmgebungsverdeckungSetzen(Enum.TryParse<Umgebungsverdeckung>(verdeckung, out var v) ? v : StandardUmgebungsverdeckung, speichern: false);
        IndirektesLichtSetzen(geladen && einstellungen.GetValue("anzeige", "ssil", false).AsBool(), speichern: false);
        // Ohne den Schlüssel (Dateien vor dem Versuch SDFGI, 30.10.2026): aus.
        var gi = geladen ? einstellungen.GetValue("anzeige", "sdfgi", "").AsString() : "";
        GlobaleBeleuchtungSetzen(Enum.TryParse<GlobaleBeleuchtung>(gi, out var g) ? g : GlobaleBeleuchtung.Aus, speichern: false);
        FsrSetzen(geladen && einstellungen.GetValue("anzeige", "fsr", false).AsBool(),
                  geladen ? einstellungen.GetValue("anzeige", "renderaufloesung", StandardRenderaufloesung).AsInt32() : StandardRenderaufloesung,
                  speichern: false);
        // Die Oberflächendetails sind immer an (Stadtansicht.Oberflaechendetails.cs): Der Schlüssel „oberflaechendetails“ älterer Dateien
        // bleibt ungelesen, ebenso „holzschnitt“ und „umriss“.
        _filter = new Bildfilter();
        AddChild(_filter);
        GrafikLaden(einstellungen, geladen); // die Einstellungen ab PR #169 (Fenster.Grafik.cs)

        // Erster Start: Das Spiel misst kurz im Hintergrund des Hauptmenüs und wählt die Voreinstellung selbst. Nicht bei
        // Startoptionen (Klick- und Prüfläufe sollen gleiche Bilder liefern); dort startet --automatisch=1 die Wahl.
        ErsterStart = !geladen;
        if (ErsterStart && OS.GetCmdlineUserArgs().Length == 0) AutomatischWaehlen();
    }

    /// <summary>Schärfe- und Farbfilter über dem Bild (Fenster.Grafik.cs).</summary>
    private Bildfilter? _filter;

    // ── Kantenglättung ──────────────────────────────────────────────────────

    /// <summary>Ohne gespeicherte Wahl: SMAA – glättet unsere klaren Kanten gut und kostet kaum Leistung.</summary>
    public const Kantenglaettung StandardKantenglaettung = Kantenglaettung.Smaa;

    public static Kantenglaettung AktuelleKantenglaettung { get; private set; } = StandardKantenglaettung;

    /// <summary>
    /// Stellt die Kantenglättung am Hauptfenster um. Alle 3D-Ansichten zeichnen in dieses Fenster,
    /// darum gilt die Wahl sofort für Stadt, Innenräume und Hauptmenü.
    /// Es ist immer genau ein Verfahren aktiv: Mehrere zugleich kosten nur Leistung und machen das Bild weicher.
    /// </summary>
    public static void KantenglaettungSetzen(Kantenglaettung art, bool speichern = true)
    {
        AktuelleKantenglaettung = art;
        KantenglaettungAnwenden();
        if (speichern) Speichern("kantenglaettung", art.ToString());
    }

    /// <summary>
    /// Überträgt die gewählte Kantenglättung aufs Hauptfenster. Ist FSR 2.2 an, glättet es selbst (es ist ein zeitliches
    /// Verfahren wie TAA, Godot schaltet TAA dabei ohnehin ab): MSAA, FXAA/SMAA und TAA bleiben dann aus – MSAA kostete Leistung
    /// auf dem kleinen Bild, FXAA/SMAA verwischten das hochgerechnete. Die gewählte Art bleibt gespeichert und gilt wieder,
    /// sobald FSR aus ist.
    /// </summary>
    private static void KantenglaettungAnwenden()
    {
        if (_instanz?.GetTree().Root is not { } fenster) return;
        var art = FsrAn ? Kantenglaettung.Aus : AktuelleKantenglaettung;
        fenster.Msaa3D = art switch
        {
            Kantenglaettung.Msaa2 => Viewport.Msaa.Msaa2X,
            Kantenglaettung.Msaa4 => Viewport.Msaa.Msaa4X,
            Kantenglaettung.Msaa8 => Viewport.Msaa.Msaa8X,
            _ => Viewport.Msaa.Disabled,
        };
        fenster.ScreenSpaceAA = art switch
        {
            Kantenglaettung.Fxaa => Viewport.ScreenSpaceAAEnum.Fxaa,
            Kantenglaettung.Smaa => Viewport.ScreenSpaceAAEnum.Smaa,
            _ => Viewport.ScreenSpaceAAEnum.Disabled,
        };
        fenster.UseTaa = art == Kantenglaettung.Taa;
    }

    // ── Schattenqualität ────────────────────────────────────────────────────

    public static Schattenqualitaet AktuelleSchattenqualitaet { get; private set; } = Schattenqualitaet.Hoch;

    /// <summary>
    /// Stellt die Schattenqualität um. Größe der Schattenkarte und Filter gelten für alle gerichteten Lichter und werden hier
    /// am RenderingServer gesetzt; Bias und Weichzeichner der Sonne passt die Stadtansicht im nächsten Bild selbst an
    /// (Stadtansicht.Licht.cs, dort stehen auch die Werte je Stufe und warum).
    /// </summary>
    public static void SchattenqualitaetSetzen(Schattenqualitaet q, bool speichern = true)
    {
        AktuelleSchattenqualitaet = q;
        var stufe = Stadtansicht.SchattenStufeFuer(q);
        // 16 Bit Tiefe wie Godots Grundeinstellung: reicht bei unserem engen Tiefenbereich (Schritt ≈ 5 mm) und ist schneller.
        RenderingServer.DirectionalShadowAtlasSetSize(stufe.Karte, true);
        RenderingServer.DirectionalSoftShadowFilterSetQuality(stufe.Filter);
        if (speichern) Speichern("schattenqualitaet", q.ToString());
    }

    // ── Bloom ───────────────────────────────────────────────────────────────

    /// <summary>Ohne gespeicherte Wahl: 50 % – ein dezenter Schein um Fensterlicht und Glut (Werte: Stadtansicht.Licht.cs).</summary>
    public const int StandardBloomStaerke = 50;

    public static bool BloomAn { get; private set; } = true;

    /// <summary>Stärke des Bloom in Prozent (0–100). 0 ist wie „Aus“.</summary>
    public static int BloomStaerke { get; private set; } = StandardBloomStaerke;

    /// <summary>Wird ausgelöst, wenn Schalter oder Stärke wechseln: Die Stadtansicht stellt ihre Umgebungen sofort um.</summary>
    public static event Action? BloomGeaendert;

    /// <summary>
    /// Schaltet den Bloom (weiches Nachleuchten heller Lichtquellen) an oder aus und stellt seine Stärke ein. Die Ansichten
    /// hören auf <see cref="BloomGeaendert"/>, die Änderung wirkt darum ohne Neustart (Werte und Wirkung: Stadtansicht.Licht.cs).
    /// </summary>
    public static void BloomSetzen(bool an, int staerke, bool speichern = true)
    {
        BloomAn = an;
        BloomStaerke = Math.Clamp(staerke, 0, 100);
        BloomGeaendert?.Invoke();
        if (speichern)
        {
            Speichern("bloom", BloomAn);
            Speichern("bloomstaerke", BloomStaerke);
        }
    }

    // ── Umgebungsverdeckung und indirektes Licht ────────────────────────────

    /// <summary>Ohne gespeicherte Wahl: Mittel – Ecken und Fußpunkte etwas dunkler, die Häuser stehen fester (Werte: Stadtansicht.Licht.cs).</summary>
    public const Umgebungsverdeckung StandardUmgebungsverdeckung = Umgebungsverdeckung.Mittel;

    public static Umgebungsverdeckung AktuelleUmgebungsverdeckung { get; private set; } = StandardUmgebungsverdeckung;

    public static bool IndirektesLichtAn { get; private set; }

    /// <summary>Wird ausgelöst, wenn Umgebungsverdeckung, indirektes Licht oder globale Beleuchtung wechseln: Die Stadtansicht stellt ihre Umgebungen sofort um.</summary>
    public static event Action? VerdeckungGeaendert;

    /// <summary>
    /// Stellt die Umgebungsverdeckung (SSAO) um. Die Güte der Rechnung gilt für alle Umgebungen und wird hier am RenderingServer
    /// gesetzt; Stärke und Reichweite stellt die Stadtansicht je Umgebung ein (<see cref="VerdeckungGeaendert"/>, Stadtansicht.Licht.cs).
    /// </summary>
    public static void UmgebungsverdeckungSetzen(Umgebungsverdeckung stufe, bool speichern = true)
    {
        AktuelleUmgebungsverdeckung = stufe;
        VerdeckungGueteSetzen();
        VerdeckungGeaendert?.Invoke();
        if (speichern) Speichern("ssao", stufe.ToString());
    }

    /// <summary>Schaltet das indirekte Licht (SSIL: Licht, das von Flächen in der Nähe zurückstrahlt) an oder aus.</summary>
    public static void IndirektesLichtSetzen(bool an, bool speichern = true)
    {
        IndirektesLichtAn = an;
        VerdeckungGueteSetzen();
        VerdeckungGeaendert?.Invoke();
        if (speichern) Speichern("ssil", an);
    }

    /// <summary>Stufe der globalen Beleuchtung (SDFGI); ohne gespeicherte Wahl aus (Werte und Gründe: Stadtansicht.GlobaleBeleuchtung.cs).</summary>
    public static GlobaleBeleuchtung AktuelleGlobaleBeleuchtung { get; private set; } = GlobaleBeleuchtung.Aus;

    /// <summary>SDFGI gibt es nur im Renderer Forward+ (Vulkan, DirectX 12), nicht unter <c>opengl3</c> (Kompatibilität) und nicht im Renderer Mobile.</summary>
    public static bool GlobaleBeleuchtungMoeglich => RenderingServer.GetCurrentRenderingMethod() == "forward_plus";

    /// <summary>
    /// Stellt die globale Beleuchtung (SDFGI: Licht, das von Wänden, Boden und Himmel zurückstrahlt, auch außerhalb des Bildes) ein. Strahlenzahl,
    /// halbe Auflösung und wie oft das Licht nachgeführt wird, gelten für alle Umgebungen und werden hier am RenderingServer gesetzt; Kaskaden und
    /// Zellgröße stellt die Stadtansicht je Umgebung ein (<see cref="VerdeckungGeaendert"/>). Wirkt ohne Neustart; unter <c>opengl3</c> bleibt es aus.
    /// </summary>
    public static void GlobaleBeleuchtungSetzen(GlobaleBeleuchtung stufe, bool speichern = true)
    {
        AktuelleGlobaleBeleuchtung = stufe;
        Stadtansicht.GlobaleBeleuchtungGueteSetzen(stufe);
        VerdeckungGeaendert?.Invoke();
        if (speichern) Speichern("sdfgi", stufe.ToString());
    }

    private static void VerdeckungGueteSetzen()
    {
        var gute = Stadtansicht.VerdeckungGueteFuer(AktuelleUmgebungsverdeckung);
        RenderingServer.EnvironmentSetSsaoQuality(gute.Qualitaet, gute.HalbeGroesse, gute.Anpassung, gute.Weichzeichner, VerdeckungAusblendenAb, VerdeckungAusblendenBis);
        // Das indirekte Licht rechnet in derselben Güte (die Stufen heißen gleich und stehen in derselben Reihenfolge).
        RenderingServer.EnvironmentSetSsilQuality((RenderingServer.EnvironmentSsilQuality)(int)gute.Qualitaet, gute.HalbeGroesse, gute.Anpassung, gute.Weichzeichner, VerdeckungAusblendenAb, VerdeckungAusblendenBis);
    }

    /// <summary>
    /// Godot blendet SSAO und SSIL nach Abstand zur Kamera aus (Grundwerte 50 m und 300 m). Die Stadtkamera steht aber rund
    /// 150 m entfernt (orthografisch, nur der Abstand zählt für die Tiefe) – mit den Grundwerten fehlte die Wirkung fast ganz.
    /// </summary>
    private const float VerdeckungAusblendenAb = 1000f, VerdeckungAusblendenBis = 2000f;

    // ── FSR 2.2 ─────────────────────────────────────────────────────────────

    /// <summary>Ohne gespeicherte Wahl: 67 % – die Stufe „Qualität“ von AMD (empfohlene Werte laut Godot-Doku: 77, 67, 59, 50 %).</summary>
    public const int StandardRenderaufloesung = 67;

    public static bool FsrAn { get; private set; }

    /// <summary>Renderauflösung in Prozent (25–100), gilt nur, solange FSR an ist.</summary>
    public static int Renderaufloesung { get; private set; } = StandardRenderaufloesung;

    /// <summary>
    /// Schaltet FSR 2.2 (AMD FidelityFX Super Resolution, in Godot eingebaut) an oder aus und stellt die Renderauflösung ein:
    /// Das 3D-Bild wird kleiner gerechnet und aus den letzten Bildern wieder auf Fenstergröße hochgerechnet, das spart Leistung.
    /// Aus: Godots normaler Weg bei Faktor 1,0. Die Kantenglättung ruht, solange FSR an ist (<see cref="KantenglaettungAnwenden"/>).
    /// </summary>
    public static void FsrSetzen(bool an, int prozent, bool speichern = true)
    {
        FsrAn = an;
        Renderaufloesung = Math.Clamp(prozent, 25, 100);
        SkalierungAnwenden(); // mit der dynamischen Auflösung und der Schärfe (Fenster.Grafik.cs)
        KantenglaettungAnwenden();
        _instanz?._filter?.Auffrischen(); // mit FSR schärft FSR selbst, ohne der Bildfilter
        if (speichern)
        {
            Speichern("fsr", FsrAn);
            Speichern("renderaufloesung", Renderaufloesung);
        }
    }

    /// <summary>
    /// Hat der Spieler die Hilfe zur Steuerung schon einmal gesehen? Beim allerersten Spiel blendet die Hauptszene sie einmal ein (PR #197);
    /// gemerkt in <c>user://einstellungen.cfg</c> (Abschnitt „spiel“).
    /// </summary>
    public static bool HilfeGesehen
    {
        get
        {
            var einstellungen = new ConfigFile();
            return einstellungen.Load(Datei) == Error.Ok && einstellungen.GetValue("spiel", "hilfe_gesehen", false).AsBool();
        }
        set
        {
            var einstellungen = new ConfigFile();
            einstellungen.Load(Datei);
            einstellungen.SetValue("spiel", "hilfe_gesehen", value);
            einstellungen.Save(Datei);
        }
    }

    /// <summary>Gelesene Einstellung „Ratssitzung zeigen“ (null: noch nicht aus der Datei gelesen).</summary>
    private static RatssitzungZeigen? _ratssitzung;

    /// <summary>
    /// Einstellung „Ratssitzung zeigen“ (PR #202): ob die Partie am Jahresende im Ratssaal anhält. Gemerkt in <c>user://einstellungen.cfg</c>
    /// (Abschnitt „spiel“, Schlüssel „ratssitzung“); fehlt er, gilt <see cref="RatssitzungZeigen.Betroffen"/>.
    /// </summary>
    public static RatssitzungZeigen Ratssitzung
    {
        get
        {
            if (_ratssitzung is { } wert) return wert;
            var einstellungen = new ConfigFile();
            var text = einstellungen.Load(Datei) == Error.Ok ? einstellungen.GetValue("spiel", "ratssitzung", "").AsString() : "";
            _ratssitzung = Enum.TryParse<RatssitzungZeigen>(text, out var gelesen) ? gelesen : RatssitzungZeigen.Betroffen;
            return _ratssitzung.Value;
        }
    }

    /// <summary>Setzt „Ratssitzung zeigen“; <paramref name="speichern"/> = false gilt nur für diesen Start (Startoption <c>--sitzungzeigen</c>).</summary>
    public static void RatssitzungSetzen(RatssitzungZeigen wert, bool speichern = true)
    {
        _ratssitzung = wert;
        if (!speichern) return;
        var einstellungen = new ConfigFile();
        einstellungen.Load(Datei);
        einstellungen.SetValue("spiel", "ratssitzung", wert.ToString());
        einstellungen.Save(Datei);
    }

    private static void Speichern(string schluessel, Variant wert)
    {
        var einstellungen = new ConfigFile();
        einstellungen.Load(Datei); // andere Einstellungen behalten (fehlt die Datei, bleibt sie leer)
        einstellungen.SetValue("anzeige", schluessel, wert);
        einstellungen.Save(Datei);
    }

    // ── Vollbild ────────────────────────────────────────────────────────────

    public override void _Input(InputEvent eingabe)
    {
        if (eingabe is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            Umschalten();
            GetViewport().SetInputAsHandled();
        }
    }

    public static void Umschalten() => Setzen(!IstVollbild);

    public static void VollbildSetzen(bool vollbild) => Setzen(vollbild);

    private static void Setzen(bool vollbild, bool speichern = true)
    {
        if (DisplayServer.GetName() == "headless") return; // Tests und CI haben kein Fenster
        // Im Godot-Editor läuft das Spiel standardmäßig eingebettet im Reiter „Game". Dort bestimmt der Editor
        // die Fenstergröße, Vollbild geht nicht (Godot meldet sonst „Embedded window only supports Windowed mode").
        if (Engine.IsEmbeddedInEditor())
        {
            if (speichern)
                _instanz?.HinweisZeigen("Vollbild geht nicht im eingebetteten Spielfenster des Editors.\n" +
                                        "Zum Testen im eigenen Fenster starten: im Reiter „Game“ das Einbetten ausschalten.");
            return;
        }
        DisplayServer.WindowSetMode(vollbild ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        if (speichern) Speichern("vollbild", vollbild);
    }

    /// <summary>Kurzer Hinweis oben in der Mitte, verschwindet nach <paramref name="sekunden"/> Sekunden – in Menü und Spiel.</summary>
    private void HinweisZeigen(string text, double sekunden = 5)
    {
        if (_hinweis is null)
        {
            var ebene = new CanvasLayer { Layer = 100 };
            AddChild(ebene);
            _hinweis = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _hinweis.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.11f, 0.10f, 0.95f),
                ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 10, ContentMarginBottom = 10,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            });
            _hinweis.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.Minsize, 16);
            _hinweis.GrowHorizontal = Control.GrowDirection.Both;
            ebene.AddChild(_hinweis);
        }
        _hinweis.Text = text;
        _hinweis.Show();
        var nummer = ++_hinweisNummer; // nur der jüngste Hinweis blendet aus
        GetTree().CreateTimer(sekunden).Timeout += () => { if (nummer == _hinweisNummer) _hinweis?.Hide(); };
    }
}

/// <summary>Die Verfahren zur Kantenglättung, die Godot 4.7 mitbringt (Beschreibung: <see cref="EinstellungenAnzeige"/>).</summary>
public enum Kantenglaettung { Aus, Fxaa, Smaa, Taa, Msaa2, Msaa4, Msaa8 }

/// <summary>Stufen der Umgebungsverdeckung (SSAO; Beschreibung: <see cref="EinstellungenAnzeige"/>, Werte: Stadtansicht.Licht.cs).</summary>
public enum Umgebungsverdeckung { Aus, Niedrig, Mittel, Hoch }

/// <summary>Stufen der globalen Beleuchtung (SDFGI; Beschreibung: <see cref="EinstellungenAnzeige"/>, Werte: Stadtansicht.GlobaleBeleuchtung.cs).</summary>
public enum GlobaleBeleuchtung { Aus, Niedrig, Mittel, Hoch }

/// <summary>Stufen der Schattenqualität (Beschreibung: <see cref="EinstellungenAnzeige"/>, Werte: Stadtansicht.Licht.cs).</summary>
public enum Schattenqualitaet { Niedrig, Mittel, Hoch }

/// <summary>Wann die Ratssitzung am Jahresende im Ratssaal gezeigt wird (Einstellung „Ratssitzung zeigen“, PR #202).</summary>
public enum RatssitzungZeigen { Immer, Betroffen, Nie }
