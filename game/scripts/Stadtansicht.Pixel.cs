using System.Globalization;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Der <b>Pixel-Look</b> der Stadtansicht (Grafikoption, Nutzer 03.11.2026; Vorlage: Versuch Runde 4 „Pixel 2 fein“) – ein Teil der <c>partial class Stadtansicht</c>.
/// Seit dem PR „drei Zoomstufen“ ist er Standard (<see cref="Fenster.PixelLookAn"/>); aus bleibt alles wie vor dem Pixel-Look. Die Pixelgröße N (2 oder 3) wählt <see cref="PixelGroesseWaehlen"/> je Zoomstufe. Eingeschaltet gilt:
/// <list type="bullet">
/// <item><b>Die Welt rechnet in einem SubViewport mit 1/N der Fensterauflösung</b> (<c>PixelWelt</c>, teilt sich die 3D-Welt des Fensters) und wird mit Nearest
/// hochgezogen (ein <see cref="TextureRect"/> auf einer <see cref="CanvasLayer"/> unter der Oberfläche). Das Fenster selbst zeichnet dann keine 3D-Welt
/// (<c>Viewport.Disable3D</c>); Oberfläche, Tafeln und Schrift bleiben in voller Auflösung.</item>
/// <item><b>Die Spielkamera bleibt die Kamera der Logik.</b> Auswahl, Bauen, Kartenwerkstatt-Pinsel, Namen unter der Maus und Schatten der Figuren rechnen mit
/// ihr, in Fensterkoordinaten, ganz wie sonst – es ist nichts umzurechnen. Eine zweite Kamera im SubViewport (<c>_pxKamera</c>) folgt ihr jedes Bild
/// (<see cref="PixelBild"/>) und <b>rastet auf das Raster der Weltpixel</b> (Kamera-Snapping); den Rest des Rastens gleicht der Versatz des hochgezogenen Bildes aus –
/// dann wandert nichts im Pixelraster, wenn die Kamera schiebt. Gesehen wird so viel wie mit der Logik-Kamera (auf ein Weltpixel genau). Die Raumkamera und die freie
/// Kamera (<c>--freiekamera=1</c>) werden ohne Rasten übernommen.</item>
/// <item><b>Beleuchtungsbild als zweiter Durchgang</b> (<c>PixelLicht</c>, Debug-Zeichnung „Beleuchtung“ mit weißen Flächen, gleiche Welt, gleiche Kamera, ohne das Nachbearbeitungs-Viereck):
/// <c>pixel_farbe.gdshader</c> setzt daraus Stufenlicht, weiche Schattenkanten, Umrisse, Tiefendunst und Kontrast auf das Bild (ein Vollbild-Viereck vor der Kamera).
/// Wasser, Feuer, Rauch, Regen und Nebel werden danach darüber gezeichnet. Das Glühen (<c>pixel_gluehen.gdshader</c>) rechnet im Maßstab des Weltbildes in einem dritten SubViewport
/// (<c>PixelGlueh</c>) und wird weich auf Fenstergröße gezogen: nur ein Viertel (N = 2) bzw. ein Neuntel (N = 3) der Bildpunkte, 24 statt 72 Abtastungen je Punkt.</item>
/// <item><b>Reihenfolge der Ansichten:</b> Godot zeichnet verschachtelte Ansichten innen vor außen. Darum liegt <c>PixelLicht</c> in <c>PixelWelt</c> und diese in <c>PixelGlueh</c>:
/// Beleuchtung, dann Welt, dann Schein, dann das Fenster.</item>
/// <item><b>Drei feste Zoomstufen</b> der Spielkamera (<see cref="PixelZoomStufen"/>: 100/50/25 m, seit „Zoom A“ näher als die früheren 140/70/35 m), nur im Pixel-Look und nicht in der Kartenwerkstatt; sonst bleibt der Zoom stufenlos. Ruhiges Gras (<c>gras.gdshader</c>, Uniform <c>ruhig</c>) und
/// weniger Halme (<see cref="GrasWerte"/>); die Vignette liegt in voller Auflösung auf der Welt-Ebene.</item>
/// <item><b>Räume</b> (Raumkamera) und Innenansichten: Pixel 3, ebenfalls mit Beleuchtungsbild (Stufenlicht, Umrisse) und Glühen.</item>
/// </list>
/// Das Fenster schaltet das 3D-Bild beim Laden ebenfalls ab: Darüber läuft <see cref="Welt3DSperren"/>, damit der Pixel-Look dabei nicht gegen sich selbst arbeitet.
/// Startoptionen: <c>--pixellook=0|1</c>, <c>--pixelgluehen=0|1</c> (Fenster.Pixel.cs), <c>--pixelmessung=&lt;s&gt;</c> (Zeiten der Ansichten im Log, dann beenden).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ebene (Bit 30), auf der nur das Nachbearbeitungs-Viereck liegt: Die Beleuchtungskamera sieht sie nicht.</summary>
    private const uint PixelFlaechenEbene = 1u << 30;

    /// <summary>Ebene (Bit 29) für Regen und Schnee im Pixel-Look: Das Beleuchtungsbild zeichnet jedes Teilchen als undurchsichtiges Rechteck (Debug-Zeichnung „Beleuchtung“ kennt keine Durchsicht) – es sieht diese Ebene nicht.</summary>
    private const uint PixelTeilchenEbene = 1u << 29;

    /// <summary>Ebene (Bit 28) der Lichtzwillinge: Nur das Beleuchtungsbild sieht sie (siehe <see cref="PixelHilfeZwilling"/>).</summary>
    private const uint PixelHilfeEbene = 1u << 28;

    private ShaderMaterial? _pxHilfeStoff;
    private DirectionalLight3D? _pxHilfeLicht;
    private readonly List<(MeshInstance3D Netz, MeshInstance3D Zwilling)> _pxHilfeNetze = new();
    private SceneTree? _pxBaum;
    private readonly System.Collections.Concurrent.ConcurrentQueue<ulong> _pxNeueNetze = new();

    private SubViewport? _pxWelt, _pxLicht, _pxGlueh;
    private Camera3D _pxKamera = null!, _pxLichtKamera = null!;
    private MeshInstance3D _pxFlaeche = null!;
    private ShaderMaterial _pxFarbe = null!, _pxGluehStoff = null!;
    private CanvasLayer _pxEbene = null!;
    private TextureRect _pxAnzeige = null!, _pxGluehAnzeige = null!;
    private ColorRect _pxVignette = null!;
    private CanvasLayer? _pxNamenEbene;
    private readonly List<Label> _pxNamen = new();
    private bool _weltGesperrt;
    private Func<float>? _pxZeitFunktion;
    private double _pxMessSekunden, _pxMessLauf; // _pxMessSekunden: 0 = noch nicht geprüft, −1 = keine Messung
    private readonly List<(double Fenster, double Welt, double Licht, double Glueh, double Cpu)> _pxMessung = new();

    /// <summary>Feste Zoomstufen der Spielkamera im Pixel-Look (sichtbare Höhe in Metern): nah, mittel, weit (Nutzer 07.11.2026: näher als vorher 35/70/140); die letzte Stufe ist der größte erlaubte Zoom.</summary>
    private static readonly float[] PixelZoomStufen = { 25f, 50f, 100f };

    /// <summary>
    /// Wie viel größer die Zoomzahlen vor „Zoom A“ waren (140 : 100): Die Umgebungsklänge sind auf das alte Maß abgestimmt (Nähe, Höhe, Umkreis der Menschen, Hörweite der Karren),
    /// <see cref="KlangZoom"/> rechnet darum im Pixel-Look auf das alte Maß um – jede Stufe klingt wie vorher (nah wie 35, mittel wie 70, weit wie 140).
    /// </summary>
    private const float PixelKlangMass = 1.4f;

    /// <summary>Ab diesem Zoom (Meter sichtbare Höhe) und darunter sind die Weltpixel 3 × 3 groß (nah, nur die Stufe 25 m), darüber 2 × 2. Zwischen nah (25 m) und mittel (50 m); Rückschaltung mit Abstand, damit eine Kamerafahrt nicht flackert.</summary>
    private const float PixelNahBis = 36f, PixelNahZurueck = 40f;

    /// <summary>
    /// Welche Pixelgröße gerade gilt: Räume und Innenansichten 3, die Kartenwerkstatt 2 (freier Zoom), draußen nah (bis <see cref="PixelNahBis"/>) 3, sonst 2.
    /// Der Wechsel passiert zwischen zwei Bildern; <see cref="PixelBild"/> stellt dabei die Größe der Ansichten um.
    /// </summary>
    private void PixelGroesseWaehlen()
    {
        int n;
        if (_raum is not null || _innen is not null) n = 3;
        else if (_entwurf is not null) n = 2;
        else n = Fenster.PixelGroesseGewaehlt == 3 ? (_kamera.Size <= PixelNahZurueck ? 3 : 2) : (_kamera.Size <= PixelNahBis ? 3 : 2);
        Fenster.PixelGroesseGewaehlt = n;
    }

    /// <summary>Sperrt oder gibt das 3D-Bild frei (Ladebildschirm der Hauptszene und der Kartenwerkstatt). Ersetzt dort <c>GetViewport().Disable3D</c>.</summary>
    public void Welt3DSperren(bool sperre)
    {
        _weltGesperrt = sperre;
        GetViewport().Disable3D = sperre || Fenster.PixelGroesse > 1;
        if (_pxWelt is not null) PixelBild();
    }

    /// <summary>
    /// Die Zoomstufen, die zwischen dem kleinsten und dem größten erlaubten Zoom liegen (bei einer kleinen Karte ist der größte Zoom die letzte Stufe, falls er nicht
    /// gerade neben der vorigen liegt; nie über der letzten festen Stufe, auch wenn die Karte mehr erlaubt).
    /// </summary>
    private static float[] PixelStufenBis(float min, float max)
    {
        var obergrenze = Mathf.Min(max, PixelZoomStufen[^1]);
        var stufen = PixelZoomStufen.Where(s => s >= min - 0.01f && s < obergrenze - 0.01f).ToList();
        if (stufen.Count == 0 || obergrenze - stufen[^1] > 5f) stufen.Add(obergrenze);
        else stufen[^1] = obergrenze; // zwei Stufen dicht beieinander: die höhere gilt
        return stufen.ToArray();
    }

    /// <summary>Rastet einen Zoom auf die nächste Zoomstufe ein (Start, Zoom der Karte); ohne Pixel-Look und in der Kartenwerkstatt bleibt er, wie er ist.</summary>
    private float PixelZoomEinrasten(float zoom)
    {
        if (!Fenster.PixelLookAn || _entwurf is not null || _innen is not null) return zoom;
        var stufen = PixelStufenBis(KameraMinZoom, KameraGroessterZoom());
        return stufen.OrderBy(s => Mathf.Abs(s - zoom)).First();
    }

    /// <summary>Die nächste Zoomstufe: <paramref name="hinein"/> zur nächsten kleineren, sonst zur nächsten größeren (von einem Zoom zwischen den Stufen aus die benachbarte).</summary>
    private static float PixelZoomStufe(float jetzt, bool hinein, float min, float max)
    {
        var stufen = PixelStufenBis(min, max);
        if (hinein) return stufen.Where(s => s < jetzt - 0.5f).DefaultIfEmpty(Mathf.Min(stufen[0], jetzt)).Max(); // unter der kleinsten Stufe (Mindestzoom 25 m) bleibt der Zoom, wie er ist
        return stufen.Where(s => s > jetzt + 0.5f).DefaultIfEmpty(stufen[^1]).Min();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═══════════════════════════════════════════════════════════════════════

    private void PixelBauen()
    {
        if (_pxWelt is not null) return;
        SubViewport Ansicht(string name, bool dreid) => new()
        {
            Name = name, Size = new Vector2I(64, 64), RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Msaa3D = Viewport.Msaa.Disabled,
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled, UseTaa = false, TransparentBg = false, GuiDisableInput = true, Disable3D = !dreid,
        };
        // innen vor außen: PixelLicht in PixelWelt in PixelGlueh (siehe Klassenkopf)
        _pxGlueh = Ansicht("PixelGlueh", false);
        _pxWelt = Ansicht("PixelWelt", true);
        _pxLicht = Ansicht("PixelLicht", true);
        _pxLicht.DebugDraw = Viewport.DebugDrawEnum.Lighting;
        AddChild(_pxGlueh);
        _pxGlueh.AddChild(_pxWelt);
        _pxWelt.AddChild(_pxLicht);

        _pxKamera = new Camera3D { Name = "PixelKamera", Near = 1f, Far = 600f };
        _pxWelt.AddChild(_pxKamera);
        _pxLichtKamera = new Camera3D { Name = "PixelLichtKamera", Near = 1f, Far = 600f };
        _pxLicht.AddChild(_pxLichtKamera);
        _pxKamera.MakeCurrent();
        _pxLichtKamera.MakeCurrent();

        // Nachbearbeitung: ein Vollbild-Viereck vor der Kamera; kleinste Zeichenreihenfolge, damit Wasser, Feuer und Nebel danach darüber liegen
        _pxFarbe = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/pixel_farbe.gdshader"), RenderPriority = -128 };
        _pxFarbe.SetShaderParameter("licht", _pxLicht.GetTexture());
        _pxFlaeche = new MeshInstance3D
        {
            Name = "PixelFlaeche", Mesh = new QuadMesh { Size = new Vector2(2, 2), FlipFaces = true }, MaterialOverride = _pxFarbe,
            ExtraCullMargin = 16384f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Layers = PixelFlaechenEbene,
        };
        _pxKamera.AddChild(_pxFlaeche);

        // Schein: im Maßstab des Weltbildes gerechnet (nächster SubViewport um die Welt), später weich hochgezogen
        _pxGluehStoff = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/pixel_gluehen.gdshader") };
        _pxGluehStoff.SetShaderParameter("welt", _pxWelt.GetTexture());
        _pxGluehStoff.SetShaderParameter("licht", _pxLicht.GetTexture());
        var gluehRahmen = new ColorRect { Name = "Schein", Material = _pxGluehStoff, Color = Colors.White, MouseFilter = Control.MouseFilterEnum.Ignore };
        gluehRahmen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _pxGlueh.AddChild(gluehRahmen);
        _pxGlueh.MoveChild(gluehRahmen, 0);

        // Anzeige: unter der Oberfläche (CanvasLayer 1) und über nichts anderem
        _pxEbene = new CanvasLayer { Name = "PixelWelt", Layer = -1, Visible = false };
        AddChild(_pxEbene);
        _pxAnzeige = new TextureRect
        {
            Texture = _pxWelt.GetTexture(), TextureFilter = CanvasItem.TextureFilterEnum.Nearest, StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _pxAnzeige.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _pxEbene.AddChild(_pxAnzeige);
        _pxGluehAnzeige = new TextureRect
        {
            Texture = _pxGlueh.GetTexture(), TextureFilter = CanvasItem.TextureFilterEnum.Linear, StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
        _pxGluehAnzeige.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _pxAnzeige.AddChild(_pxGluehAnzeige);
        _pxVignette = new ColorRect
        {
            Name = "Vignette", Color = Colors.White, MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/pixel_vignette.gdshader") },
        };
        _pxVignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _pxEbene.AddChild(_pxVignette);

        foreach (var a in new[] { _pxWelt, _pxLicht, _pxGlueh }) RenderingServer.ViewportSetMeasureRenderTime(a.GetViewportRid(), true);
        _pxZeitFunktion = () => (float)(RenderingServer.ViewportGetMeasuredRenderTimeGpu(_pxWelt.GetViewportRid())
                                            + RenderingServer.ViewportGetMeasuredRenderTimeGpu(_pxLicht.GetViewportRid())
                                            + RenderingServer.ViewportGetMeasuredRenderTimeGpu(_pxGlueh.GetViewportRid()));
        Fenster.PixelGpuZeit = _pxZeitFunktion;

        // Hilfs-Overlays (Bauzonen, Raster, Umrisse): jedes durchsichtige, unbeleuchtete Netz bekommt einen Lichtzwilling, auch die schon vorhandenen
        // Zwilling: beleuchtet (unbeleuchtete und leuchtende Flächen zeigt das Beleuchtungsbild nur als dunklen Boden), die Normale zeigt zur Kamera, dazu ein starkes Licht entlang der Blickrichtung,
        // das nur die Zwillingsebene trifft
        _pxHilfeStoff = new ShaderMaterial { Shader = new Shader { Code = "shader_type spatial; render_mode cull_disabled; void fragment() { ALBEDO = vec3(1.0); NORMAL = vec3(0.0, 0.0, 1.0); ROUGHNESS = 1.0; SPECULAR = 0.0; }" } };
        _pxHilfeLicht = new DirectionalLight3D { Name = "Hilfslicht", LightEnergy = 8f, LightCullMask = PixelHilfeEbene, ShadowEnabled = false, LightSpecular = 0f };
        _pxLichtKamera.AddChild(_pxHilfeLicht);
        _pxBaum = GetTree();
        _pxBaum.NodeAdded += PixelKnotenHinzu;
        foreach (var m in GetTree().Root.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()) PixelHilfeZwilling(m);
    }

    /// <summary>Jeder neue Netzknoten kommt in die Warteschlange (die Stadt lädt in Fäden, dieser Aufruf kann aus jedem Faden kommen); <see cref="PixelBild"/> arbeitet sie im Hauptfaden ab.</summary>
    private void PixelKnotenHinzu(Node knoten)
    {
        if (knoten is MeshInstance3D m) _pxNeueNetze.Enqueue(m.GetInstanceId());
    }

    private void PixelNeueNetze()
    {
        var n = 0;
        while (n++ < 400 && _pxNeueNetze.TryDequeue(out var id))
            if (InstanceFromId(id) is MeshInstance3D m && m.IsInsideTree()) PixelHilfeZwilling(m);
    }

    /// <summary>
    /// Bauzonen, Raster, Umrisse und andere Hilfsflächen sind durchsichtig und unbeleuchtet. Das Glühen hält alles für Licht, was im Bild heller ist als im Beleuchtungsbild
    /// (<c>pixel_gluehen.gdshader</c>) – und das Beleuchtungsbild zeigt unbeleuchtete Flächen nur wie dunklen Boden. Darum bekommt jedes solche Netz einen <b>Zwilling</b> (gleiches Netz, undurchsichtig, weiß, von einem eigenen starken Licht beleuchtet, das nur diese Ebene trifft), den nur das Beleuchtungsbild sieht (Ebene <see cref="PixelHilfeEbene"/>): dort sind die Flächen hell, das Verhältnis liegt unter 1, nichts glüht.
    /// Die Flächen selbst bleiben, wie sie sind (nachts so hell wie ihre Farbe).
    /// </summary>
    private void PixelHilfeZwilling(MeshInstance3D m)
    {
        if (_pxHilfeStoff is null || !IsInstanceValid(m) || m.Layers != 1 || m.HasMeta("pxzwilling") || m.HasMeta("pxistzwilling")) return;
        if (m.MaterialOverride is not StandardMaterial3D { ShadingMode: BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency: not BaseMaterial3D.TransparencyEnum.Disabled, BlendMode: BaseMaterial3D.BlendModeEnum.Mix }) return;
        m.SetMeta("pxzwilling", true);
        var zwilling = new MeshInstance3D
        {
            Name = "Lichtzwilling", Mesh = m.Mesh, MaterialOverride = _pxHilfeStoff, Layers = PixelHilfeEbene,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, GIMode = GeometryInstance3D.GIModeEnum.Disabled,
        };
        zwilling.SetMeta("pxistzwilling", true);
        m.AddChild(zwilling);
        _pxHilfeNetze.Add((m, zwilling));
    }

    /// <summary>Bei jeder Änderung der Grafikeinstellungen (aus <see cref="GrafikNachfuehren"/>): bauen, wenn der Pixel-Look neu an ist; Werte des Fensters in die Ansichten übernehmen.</summary>
    private void PixelNachfuehren()
    {
        if (Fenster.PixelGroesse > 1) PixelBauen();
        if (_pxWelt is null) return;
        var wurzel = GetViewport();
        foreach (var a in new[] { _pxWelt, _pxLicht! })
        {
            // Schatten und Detailstufe wie im Fenster; Kantenglättung bleibt aus (harte Pixel), das Raster ist das Bild
            a.MeshLodThreshold = wurzel.MeshLodThreshold;
            a.PositionalShadowAtlasSize = wurzel.PositionalShadowAtlasSize;
            a.PositionalShadowAtlas16Bits = wurzel.PositionalShadowAtlas16Bits;
            a.PositionalShadowAtlasQuad0 = wurzel.PositionalShadowAtlasQuad0;
            a.PositionalShadowAtlasQuad1 = wurzel.PositionalShadowAtlasQuad1;
            a.PositionalShadowAtlasQuad2 = wurzel.PositionalShadowAtlasQuad2;
            a.PositionalShadowAtlasQuad3 = wurzel.PositionalShadowAtlasQuad3;
            a.UseOcclusionCulling = wurzel.UseOcclusionCulling;
        }
        BloomNachfuehren(); // das Glühen ersetzt den Bloom der Stadt (Stadtansicht.Licht.cs)
        PixelBild();
    }

    /// <summary>Beim Verlassen der Stadt: das Fenster zeichnet wieder 3D, die Zeit der Ansichten fällt aus der Messung.</summary>
    private void PixelAufgeben()
    {
        if (_pxWelt is null) return;
        if (_pxBaum is not null && IsInstanceValid(_pxBaum)) _pxBaum.NodeAdded -= PixelKnotenHinzu;
        if (Fenster.PixelGpuZeit == _pxZeitFunktion) Fenster.PixelGpuZeit = null; // nur die eigene Funktion (bei Szenenwechsel kann die neue Stadt schon eine gesetzt haben)
        GetViewport().Disable3D = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Jedes Bild
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Nach allem anderen im Bild (aus <see cref="_Process"/>): Ansichten ein- oder ausschalten, die Kamera übernehmen und rasten, Größe und Versatz des Bildes setzen,
    /// Lichtskala und Dunstfarbe nach Tageszeit.
    /// </summary>
    private void PixelBild()
    {
        PixelGroesseWaehlen();
        var gewuenscht = Fenster.PixelGroesse > 1;
        if (_pxMessSekunden >= 0) PixelMessen(GetViewport()); // --pixelmessung=<s>, auch ohne Pixel-Look (Vergleichswert)
        if (_pxWelt is null)
        {
            if (!gewuenscht) return;
            PixelNachfuehren();
            return;
        }
        var wurzel = GetViewport();
        var kamera = wurzel.GetCamera3D();
        wurzel.Disable3D = _weltGesperrt || gewuenscht; // das Fenster zeichnet im Pixel-Look keine 3D-Welt (die Ladebildschirme sperren sie zusätzlich)
        var laeuft = gewuenscht && !_weltGesperrt && kamera is not null && kamera != _pxKamera;
        var raum = _raum is not null;
        var licht = laeuft;
        var schein = licht && Fenster.PixelGluehenAn;
        _pxEbene.Visible = gewuenscht;
        _pxWelt!.RenderTargetUpdateMode = laeuft ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        _pxLicht!.RenderTargetUpdateMode = licht ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        _pxGlueh!.RenderTargetUpdateMode = schein ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        _pxFlaeche.Visible = licht;
        _pxGluehAnzeige.Visible = schein;
        _pxVignette.Visible = gewuenscht;
        PixelNeueNetze();
        PixelNamen(gewuenscht && laeuft && !raum ? kamera : null);
        var teilchen = licht ? PixelTeilchenEbene : 1u; // nur wenn das Beleuchtungsbild läuft, sonst sehen Fenster- und Raumkamera die Teilchen wie sonst
        if (_regen is not null && _regen.Layers != teilchen) _regen.Layers = teilchen;
        if (_schnee is not null && _schnee.Layers != teilchen) _schnee.Layers = teilchen;
        // ebenso alle anderen durchsichtigen Flächen: Traufentropfen und -spritzer, Lichtflecken und -schein, Flammen und Rauch (sonst dunkle Rechtecke im Beleuchtungsbild)
        foreach (VisualInstance3D? knoten in new VisualInstance3D?[] { _traufTropfen, _traufRinge, _fleckBoden, _fleckSchein })
            if (knoten is not null && knoten.Layers != teilchen) knoten.Layers = teilchen;
        // Hilfsflächen mit Lichtzwilling: das Beleuchtungsbild zeichnet durchsichtige Flächen undurchsichtig und verdeckte den Zwilling, es sieht sie darum nicht (wie die Teilchen)
        _pxHilfeNetze.RemoveAll(h => !IsInstanceValid(h.Netz) || !IsInstanceValid(h.Zwilling));
        foreach (var (netz, zwilling) in _pxHilfeNetze)
        {
            if (netz.Layers != teilchen) netz.Layers = teilchen;
            if (zwilling.Mesh != netz.Mesh) zwilling.Mesh = netz.Mesh; // das Raster tauscht sein Netz aus
        }
        // ebenso die Nebelschichten des Bodennebels (Stadtansicht.Bodennebel.cs): Das Beleuchtungsbild zeichnete sie undurchsichtig und beleuchtet, mit dem Schatten der Sonne in ihrer
        // Höhe, nicht auf dem Boden – wo ein Fetzen lag, stand im Stufenlicht ein zweiter, versetzter Schatten neben dem echten (besonders sichtbar bei Schnee am Mittag)
        if (_nebelSchichten.Count > 0)
            foreach (var (netz, _) in _nebelSchichten)
                if (netz.Layers != teilchen) netz.Layers = teilchen;
        // und die Wasserverdränger der Boote (Stadtansicht.Schiffe.cs): durchsichtig mit Tiefe, das Beleuchtungsbild machte daraus einen dunklen Deckel über Ladung, Schiffer und Boden
        _verdraengerNetze.RemoveAll(v => !IsInstanceValid(v));
        foreach (var v in _verdraengerNetze)
            if (v.Layers != teilchen) v.Layers = teilchen;
        Feuer.EbeneSetzen(GetTree(), teilchen);
        if (!laeuft || kamera is null) return;

        // Größen: Weltpixel = n × n Fensterpixel. Die Oberfläche kann skaliert sein (ContentScaleFactor): Bildschirmpixel und Zeichenfläche sind zu trennen.
        var n = Fenster.PixelGroesse;
        var fenster = GetWindow().Size;
        var zeichenflaeche = wurzel.GetVisibleRect().Size;
        var faktor = fenster.Y / Mathf.Max(zeichenflaeche.Y, 1f); // Bildschirmpixel je Einheit der Zeichenfläche
        var ortho = kamera.Projection == Camera3D.ProjectionType.Orthogonal;
        var gross = new Vector2I(Mathf.CeilToInt(fenster.X / (float)n) + (ortho ? 2 : 0), Mathf.CeilToInt(fenster.Y / (float)n) + (ortho ? 2 : 0));
        foreach (var a in new[] { _pxWelt, _pxLicht, _pxGlueh })
            if (a.Size != gross) a.Size = gross;

        var ziel = kamera.GlobalTransform;
        var anzeigeGroesse = new Vector2(gross.X, gross.Y) * n;
        var anzeigeOrt = (new Vector2(fenster.X, fenster.Y) - anzeigeGroesse) / 2f;
        var groesse = kamera.Size;
        if (ortho)
        {
            // Kamera-Snapping: in Blickrichtung auf das Raster der Weltpixel rasten (Meter je Weltpixel = sichtbare Höhe · n / Fensterhöhe)
            var ps = kamera.Size * n / fenster.Y;
            var basis = ziel.Basis.Orthonormalized();
            var lokal = basis.Transposed() * ziel.Origin;
            var einrast = new Vector2(Mathf.Round(lokal.X / ps), Mathf.Round(lokal.Y / ps)) * ps;
            var rest = new Vector2(lokal.X, lokal.Y) - einrast; // Meter
            ziel = new Transform3D(basis, basis * new Vector3(einrast.X, einrast.Y, lokal.Z));
            groesse = ps * gross.Y;
            anzeigeOrt += new Vector2(-rest.X, rest.Y) / ps * n; // den Rest gleicht der Versatz des hochgezogenen Bildes aus
        }
        PixelKameraSetzen(_pxKamera, kamera, ziel, groesse, true);
        PixelKameraSetzen(_pxLichtKamera, kamera, ziel, groesse, false);
        _pxAnzeige.Position = anzeigeOrt / faktor;
        _pxAnzeige.Size = anzeigeGroesse / faktor;

        // Schalter des Shaders, die von Kamera und Tageszeit abhängen
        _pxFarbe.SetShaderParameter("ortho_groesse", ortho ? groesse : 0f);
        _pxFarbe.SetShaderParameter("fov_faktor", 2f * Mathf.Tan(Mathf.DegToRad(kamera.Fov) / 2f));
        _pxFarbe.SetShaderParameter("dunst_bezug", KameraAbstand);
        var skala = PixelLichtskala();
        _pxFarbe.SetShaderParameter("lichtskala", skala);
        _pxFarbe.SetShaderParameter("tag", Mathf.SmoothStep(PixelLichtBoden, 0.85f, skala));
        _pxFarbe.SetShaderParameter("dunst_farbe", PixelDunstFarbe(skala));
        _pxGluehStoff.SetShaderParameter("welt_groesse", new Vector2(gross.X, gross.Y));
    }

    /// <summary>
    /// Alle Beschriftungen (Label3D aus <c>Beschriftung(...)</c>: Gebäudenamen, Namen der Bauzonen, Bauplatz-Hinweise, Fehler am Ort wie „Hier ist keine Bauzone“, Beschriftungen der Kartenwerkstatt)
    /// bleiben im Pixel-Look gestochen scharf: Die 3D-Schilder werden nicht gezeichnet (Sichtebene 0), stattdessen stehen
    /// Control-Beschriftungen in voller Auflösung über dem Pixelbild, an der Stelle, auf die die Kamera der Logik das Schild projiziert (das Pixelbild
    /// zeigt dieselbe Stelle auf ein Weltpixel genau). Größe wie das Schild: Schrift · Pixelgröße des Schildes (0,04 m) · Bildhöhe / Bezugszoom.
    /// <paramref name="kamera"/> null: kein Pixel-Look bzw. Raum – die Schilder kehren in die Welt zurück.
    /// </summary>
    private void PixelNamen(Camera3D? kamera)
    {
        var anzeigen = kamera is not null;
        var zahl = 0;
        foreach (var label in _beschriftungen)
        {
            if (!IsInstanceValid(label)) continue;
            var ziel = anzeigen ? 0u : 1u;
            if (label.Layers != ziel) label.Layers = ziel;
            if (!anzeigen || !label.IsVisibleInTree() || label.Text.Length == 0 || kamera!.IsPositionBehind(label.GlobalPosition)) continue;
            _pxNamenEbene ??= new CanvasLayer { Name = "PixelNamen", Layer = 0 };
            if (_pxNamenEbene.GetParent() is null) AddChild(_pxNamenEbene);
            if (zahl == _pxNamen.Count)
            {
                var neu = new Label { MouseFilter = Control.MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center };
                _pxNamenEbene.AddChild(neu);
                _pxNamen.Add(neu);
            }
            var l = _pxNamen[zahl++];
            var hoehe = GetViewport().GetVisibleRect().Size.Y;
            var massstab = label.PixelSize * hoehe / BezugsZoom; // Bildpunkte je Schrifteinheit des Schildes
            var groesse = Mathf.Max(6, Mathf.RoundToInt(label.FontSize * massstab));
            l.Text = label.Text;
            l.Visible = true;
            if (label.Font is not null) l.AddThemeFontOverride("font", label.Font);
            else l.RemoveThemeFontOverride("font");
            l.AddThemeFontSizeOverride("font_size", groesse);
            l.AddThemeColorOverride("font_color", label.Modulate);
            l.AddThemeColorOverride("font_outline_color", label.OutlineModulate);
            l.AddThemeConstantOverride("outline_size", Mathf.RoundToInt(label.OutlineSize * massstab));
            l.AddThemeConstantOverride("line_spacing", Mathf.RoundToInt(label.LineSpacing * massstab));
            var umbruch = label.AutowrapMode != TextServer.AutowrapMode.Off; // Hinweise mit fester Breite (Label3D.Width in Schrifteinheiten) brechen um
            l.AutowrapMode = label.AutowrapMode;
            l.CustomMinimumSize = umbruch ? new Vector2(Mathf.Round(label.Width * massstab), 0) : Vector2.Zero;
            l.Size = new Vector2(umbruch ? l.CustomMinimumSize.X : 0, 0);
            l.Position = (kamera!.UnprojectPosition(label.GlobalPosition) - l.GetCombinedMinimumSize() / 2f).Round();
        }
        for (var i = zahl; i < _pxNamen.Count; i++) _pxNamen[i].Visible = false;
    }

    /// <summary>Überträgt die Kamera der Logik auf eine Kamera im SubViewport (Projektion, Entfernungen, Umgebung, Sichtebenen).</summary>
    private void PixelKameraSetzen(Camera3D ziel, Camera3D quelle, Transform3D lage, float groesse, bool hauptbild)
    {
        ziel.GlobalTransform = lage;
        ziel.Projection = quelle.Projection;
        ziel.Size = groesse;
        ziel.Fov = quelle.Fov;
        ziel.Near = quelle.Near;
        ziel.Far = quelle.Far;
        ziel.KeepAspect = quelle.KeepAspect;
        ziel.Environment = quelle.Environment;
        ziel.Attributes = null; // keine Miniatur-Unschärfe im Pixel-Look: weiche Ränder passen nicht zu harten Pixeln
        ziel.CullMask = hauptbild ? quelle.CullMask | PixelFlaechenEbene | PixelTeilchenEbene : (quelle.CullMask & ~(PixelFlaechenEbene | PixelTeilchenEbene)) | PixelHilfeEbene;
    }

    /// <summary>
    /// Wie hell das volle Licht gerade ist (1 = Mittag): Sonne (oder Mond) plus Umgebungslicht im Verhältnis zum Tag. Die Lichtstufen des Shaders sind auf den Tag
    /// abgestimmt; ohne diesen Faktor fiele abends alles in die dunkelste Stufe.
    /// </summary>
    /// <summary>Kleinste Lichtskala: Nachts bleiben die Flächen sichtbar (Boden und Wände etwa so hell wie ohne Pixel-Look), Fenster und Feuer leuchten darüber.</summary>
    private const float PixelLichtBoden = 0.34f;

    /// <summary>
    /// Farbe des Tiefendunsts: ein helles Blaugrau am Tag, abends und nachts <b>so hell wie das Licht</b> (Lichtskala) und zur Hälfte im Farbton des Lichts
    /// (Umgebungslicht plus etwas Sonne). Eine feste helle Farbe legte abends einen grauen Schleier über die dunkle Stadt und färbte sie um.
    /// </summary>
    private Vector3 PixelDunstFarbe(float skala)
    {
        var umgebung = _umgebung.Environment;
        var licht = umgebung.AmbientLightColor * umgebung.AmbientLightEnergy + (_sonne.Visible ? _sonne.LightColor * _sonne.LightEnergy * 0.35f : Colors.Black);
        var hell = Mathf.Max(0.2126f * licht.R + 0.7152f * licht.G + 0.0722f * licht.B, 0.001f);
        var ton = new Vector3(licht.R, licht.G, licht.B) / hell; // Farbton bei Helligkeit 1
        var tag = new Vector3(0.55f, 0.6f, 0.68f);
        var farbe = tag.Lerp(tag * ton, 0.5f) * Mathf.Clamp(skala, 0.1f, 1f) * Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(PixelLichtBoden, 0.85f, skala)); // abends noch dunkler als das Licht: die Stadt ist dunkler als ihr Licht allein
        return farbe.Lerp(new Vector3(0.04f, 0.045f, 0.065f), NebelDunkel());
    }

    private float PixelLichtskala()
    {
        static float Hell(Color c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
        var umgebung = _umgebung.Environment;
        var sonne = _sonne.Visible ? _sonne.LightEnergy * Hell(_sonne.LightColor) : 0f;
        var licht = sonne + umgebung.AmbientLightEnergy * Hell(umgebung.AmbientLightColor);
        return Mathf.Clamp(licht / 1.55f, PixelLichtBoden, 1f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Messung (--pixelmessung=<Sekunden>)
    // ═══════════════════════════════════════════════════════════════════════

    private void PixelMessen(Viewport wurzel)
    {
        if (_pxMessSekunden == 0)
        {
            if (!OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--pixelmessung="))) { _pxMessSekunden = -1; return; }
            var wert = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--pixelmessung=")).Split('=')[1];
            _pxMessSekunden = double.TryParse(wert, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && s > 0 ? s : 10;
            RenderingServer.ViewportSetMeasureRenderTime(wurzel.GetViewportRid(), true);
        }
        if (_pxMessSekunden < 0) return;
        _pxMessLauf += GetProcessDeltaTime();
        if (_pxMessLauf < 3) return; // erst einlaufen lassen (Shader, Gras)
        double Gpu(Viewport? v) => v is null ? 0 : RenderingServer.ViewportGetMeasuredRenderTimeGpu(v.GetViewportRid());
        double Cpu(Viewport? v) => v is null ? 0 : RenderingServer.ViewportGetMeasuredRenderTimeCpu(v.GetViewportRid());
        _pxMessung.Add((Gpu(wurzel), Gpu(_pxWelt), Gpu(_pxLicht), Gpu(_pxGlueh), Cpu(wurzel) + Cpu(_pxWelt) + Cpu(_pxLicht)));
        if (_pxMessLauf < 3 + _pxMessSekunden) return;
        var m = _pxMessung;
        GD.Print(string.Create(CultureInfo.InvariantCulture,
            $"[pixelmessung] pixellook={Fenster.PixelLookAn} n={Fenster.PixelGroesse} gluehen={Fenster.PixelGluehenAn} bilder={m.Count} fenster={m.Average(x => x.Fenster):F2} ms, welt={m.Average(x => x.Welt):F2} ms, licht={m.Average(x => x.Licht):F2} ms, " +
            $"glueh={m.Average(x => x.Glueh):F2} ms, summe={m.Average(x => x.Fenster + x.Welt + x.Licht + x.Glueh):F2} ms, cpu={m.Average(x => x.Cpu):F2} ms, fps={Engine.GetFramesPerSecond():F0}"));
        GetTree().Quit();
    }
}
