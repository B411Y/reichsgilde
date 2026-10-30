using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die Grafikeinstellungen in der Stadt – ein Teil der <c>partial class Stadtansicht</c> (die Werte stehen in <see cref="Fenster"/>,
/// Fenster.Grafik.cs; hier wird nur gezeigt):
/// <list type="bullet">
/// <item><b>Miniatur-Unschärfe</b> (Tilt-Shift): Tiefenunschärfe der Stadtkamera. Scharf ist ein Band um den Drehpunkt in der Bildmitte,
/// oben (weiter weg) und unten (näher) wird es weich – die Stadt wirkt wie ein Modell auf dem Tisch. Die Raumansicht bleibt scharf.</item>
/// <item><b>Wolkenschatten</b>: ein großes Decal mit Wolkenflecken, das mit dem Wind über Boden und Dächer zieht.</item>
/// <item><b>Morgendunst</b>: Nebel am Boden, morgens dicht, abends leicht; in der hohen Stufe volumetrisch mit Lichtbahnen der Sonne,
/// in der Raumansicht als feiner Staub im Licht der Fenster.</item>
/// <item><b>Farbstimmung</b>: Tonemapping, Helligkeit, Kontrast, Sättigung beider Umgebungen (Stadt und Raumansicht).</item>
/// <item><b>Detailstufe</b>: Natur-Dichte, Menge der Passanten und Sichtweite der Feinheiten – nur Darstellung, der Kern rechnet weiter mit allen.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    private CameraAttributesPractical _tiltShift = null!;
    private Decal _wolken = null!;
    private FogVolume? _dunstVolumen;
    private FogMaterial? _dunstStoff;
    private Vector2 _wolkenVersatz;
    private (float Zoom, Miniaturunschaerfe Stufe)? _tiltShiftFuer;

    /// <summary>Einmal beim Aufbau (nach Umgebung, Licht und Kamera).</summary>
    private void GrafikEinrichten()
    {
        _tiltShift = new CameraAttributesPractical();
        _kamera.Attributes = _tiltShift;
        WolkenEinrichten();
        DunstEinrichten();
        WetterEinrichten(); // Regen, Schnee und Blitz (Stadtansicht.Wetter.cs)
        Fenster.GrafikGeaendert += GrafikNachfuehren;
        GrafikNachfuehren();
    }

    private void GrafikAufgeben() => Fenster.GrafikGeaendert -= GrafikNachfuehren;

    /// <summary>Bei jeder Änderung der Einstellungen: was nicht jedes Bild nachgezogen wird.</summary>
    private void GrafikNachfuehren()
    {
        FarbstimmungAnwenden(_umgebung.Environment);
        FarbstimmungAnwenden(_raumUmgebung);
        DunstAnwenden();
        _tiltShiftFuer = null;
        NaturDetailAnwenden();
        BodenqualitaetNachfuehren(); // Shader des Bodens und Grasbüschel (Stadtansicht.Boden.cs)
        OberflaecheStufeNachfuehren(); // Material-Bibliothek der Modelle (Stadtansicht.Oberflaechendetails.cs)
        // Mesh-LOD: Godot darf Modelle vereinfachen, die im Bild klein sind. Nur in der Stufe „Nah“ – auch die Schatten nehmen dann
        // die vereinfachten Modelle, und Spitzdächer, Zinnen und Balken werfen keinen Schatten mehr (siehe SchattenEinrichten).
        GetViewport().MeshLodThreshold = Fenster.AktuelleFeinheiten == Feinheiten.Nah ? 1f : 0f;
    }

    /// <summary>Jedes Bild (aus _Process): Unschärfe nach Zoom, Wolken im Wind, Dunst nach Uhrzeit, Feinheiten nach Zoom.</summary>
    private void GrafikBild(double delta)
    {
        TiltShiftNachfuehren();
        WolkenBewegen(delta);
        DunstNachUhrzeit();
        WetterNebel(); // Nebel des Wetters (Stadtansicht.Wetter.cs)
        BodennebelNachfuehren(); // Schwaden am Boden (Stadtansicht.Bodennebel.cs)
        FeinheitenNachZoom();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Miniatur-Unschärfe (Tilt-Shift)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Je Stufe: halbe Breite des scharfen Bandes oben und unten (Anteil der halben Bildtiefe), Länge des Übergangs bis zur vollen
    /// Unschärfe (ebenso) und die größte Unschärfe (Godots <c>DofBlurAmount</c>). Unten (näher an der Kamera) ist das Band breiter, denn
    /// dort stehen auch alle hohen Dinge – Turmspitzen und Dächer liegen näher an der Kamera als der Boden darunter.
    /// </summary>
    private static (float Fern, float Nah, float Uebergang, float Menge) TiltShiftWerte(Miniaturunschaerfe stufe) => stufe switch
    {
        Miniaturunschaerfe.Stark => (0.18f, 0.5f, 0.55f, 0.16f),
        _ => (0.35f, 0.8f, 0.7f, 0.08f),
    };

    /// <summary>
    /// Die orthografische Kamera sieht den Boden zwischen den Tiefen Abstand ± Zoom/2 · cot(Neigung) (siehe SchattenNachfuehren).
    /// Das Band wird in Anteilen dieser halben Tiefe gelegt, es bleibt also bei jedem Zoom gleich groß im Bild. Nah (kleiner Zoom)
    /// wird es stärker unscharf als weit – so wirkt eine Gasse aus der Nähe wie ein Modell, die ganze Stadt bleibt lesbar.
    /// </summary>
    private void TiltShiftNachfuehren()
    {
        var stand = (_kamera.Size, Fenster.AktuelleUnschaerfe);
        if (_tiltShiftFuer == stand) return;
        _tiltShiftFuer = stand;
        var an = Fenster.AktuelleUnschaerfe != Miniaturunschaerfe.Aus;
        _tiltShift.DofBlurFarEnabled = an;
        _tiltShift.DofBlurNearEnabled = an;
        if (!an) return;
        var (fern, nah, uebergang, menge) = TiltShiftWerte(Fenster.AktuelleUnschaerfe);
        var halbeTiefe = _kamera.Size / 2 / Mathf.Tan(Mathf.DegToRad(KameraNeigung));
        _tiltShift.DofBlurFarDistance = KameraAbstand + fern * halbeTiefe;
        _tiltShift.DofBlurFarTransition = uebergang * halbeTiefe;
        _tiltShift.DofBlurNearDistance = KameraAbstand - nah * halbeTiefe;
        _tiltShift.DofBlurNearTransition = uebergang * halbeTiefe;
        var nahFaktor = Mathf.Lerp(1.5f, 0.7f, Mathf.Clamp((_kamera.Size - KameraMinZoom) / (KameraMaxZoom - KameraMinZoom), 0, 1));
        _tiltShift.DofBlurAmount = menge * nahFaktor;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wolkenschatten
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wiederholung des Wolkenmusters (Meter); das Decal ist doppelt so groß und zeigt es 2 × 2-mal.</summary>
    private const float WolkenPeriode = 512f;

    /// <summary>Wie weit die Wolken je echter Sekunde ziehen (Meter) und wie dunkel ihr Schatten höchstens macht (Anteil).</summary>
    private const float WolkenTempo = 2.5f, WolkenDunkel = 0.42f;

    /// <summary>
    /// Ein Decal über der ganzen Stadt: Es färbt alles darunter (Boden, Dächer, Wände, Figuren) zum Schwarz hin, so weit die Wolke dicht
    /// ist. Die Flecken kommen aus nahtlosem Rauschen (wiederholt sich ohne Kante) und sind in die Textur 2 × 2-mal gelegt; das Decal
    /// folgt der Kamera in Schritten einer Periode – so sieht man nie seinen Rand und das Muster springt nie.
    /// </summary>
    private void WolkenEinrichten()
    {
        var rauschen = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Seed = 1480, Frequency = 0.012f,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 4, FractalGain = 0.5f,
        };
        const int seite = 256;
        var kachel = rauschen.GetSeamlessImage(seite, seite);
        var bild = Image.CreateEmpty(2 * seite, 2 * seite, false, Image.Format.Rgba8);
        for (var y = 0; y < seite; y++)
            for (var x = 0; x < seite; x++)
            {
                // Nur die dichten Stellen werden Wolke, weich auslaufend; dazwischen bleibt freier Himmel
                var dichte = Mathf.SmoothStep(0.48f, 0.72f, kachel.GetPixel(x, y).R);
                var farbe = new Color(0, 0, 0, dichte);
                bild.SetPixel(x, y, farbe);
                bild.SetPixel(x + seite, y, farbe);
                bild.SetPixel(x, y + seite, farbe);
                bild.SetPixel(x + seite, y + seite, farbe);
            }
        bild.GenerateMipmaps();
        _wolken = new Decal
        {
            Name = "Wolkenschatten",
            Size = new Vector3(2 * WolkenPeriode, 160, 2 * WolkenPeriode),
            TextureAlbedo = ImageTexture.CreateFromImage(bild),
            AlbedoMix = 1,
            UpperFade = 0, LowerFade = 0, NormalFade = 0,
            Visible = false,
        };
        AddChild(_wolken);
        // Die Wolken ziehen mit dem Wind der Fahnen (globale Shader-Uniform in project.godot; RenderingServer gibt sie nur im Editor heraus).
        if (ProjectSettings.GetSetting("shader_globals/wind_richtung").AsGodotDictionary() is { } wind && wind.TryGetValue("value", out var richtung))
            _windRichtung = richtung.AsVector2().Normalized();
    }

    private Vector2 _windRichtung = new(1, 0);

    private void WolkenBewegen(double delta)
    {
        var zeigen = Fenster.WolkenschattenAn && _raum is null && _sonne.Visible && _sonne.LightEnergy > 0.05f;
        if (_wolken.Visible != zeigen) _wolken.Visible = zeigen;
        if (!zeigen) return;
        if (_simulation is not { Takt.IstPausiert: true })
        {
            var tempo = WolkenTempo * (0.4f + 2.4f * _wetter.Wind); // bei leichtem Wind (0,25) wie bisher, im Sturm dreimal so schnell (Stadtansicht.Wetter.cs)
            _wolkenVersatz = (_wolkenVersatz + _windRichtung * tempo * (float)delta).PosMod(WolkenPeriode);
        }
        // In ganzen Perioden zur Kamera: Das Muster liegt fest in der Welt (es zieht nur mit dem Wind), das Decal deckt immer das Bild.
        var ziel = _kameraDrehung.Position;
        var x = _wolkenVersatz.X + Mathf.Round((ziel.X - _wolkenVersatz.X) / WolkenPeriode) * WolkenPeriode;
        var z = _wolkenVersatz.Y + Mathf.Round((ziel.Z - _wolkenVersatz.Y) / WolkenPeriode) * WolkenPeriode;
        _wolken.Position = new Vector3(x, 40, z);
        // Nur die Sonne wirft Wolkenschatten: morgens und abends schwächer, nachts keine (der Mond ist zu schwach dafür)
        var sonne = _sonne.LightColor.R > _sonne.LightColor.B ? Mathf.Clamp(_sonne.LightEnergy / 1.2f, 0, 1) : 0; // der Mond ist bläulich
        // Wechselnd bewölkt: mehr Wolken; ganz bedeckt: keine einzelnen Schatten mehr (die Sonne ist dann ohnehin gedämpft, Stadtansicht.Wetter.cs)
        var wolken = _wetter.Wolken;
        sonne *= (1 + 0.6f * Mathf.SmoothStep(0.1f, 0.6f, wolken)) * (1 - 0.8f * Mathf.SmoothStep(0.6f, 0.95f, wolken));
        _wolken.Modulate = new Color(1, 1, 1, Mathf.Min(WolkenDunkel * sonne, 0.6f));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Morgendunst und Lichtstrahlen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Dichte des volumetrischen Dunstes am Boden (voller Anteil) und wie schnell er nach oben dünner wird: Abfall 0,7 je Meter heißt
    /// in 3 m Höhe noch 12 %, in 5 m 3 % – der Dunst liegt flach über Feldern und Gassen, Dächer und Mauerkronen stehen darüber.
    /// Halb so dicht wie bis Oktober 2026 (0,16): Die sichtbaren Nebelbänke macht der Bodennebel (Stadtansicht.Bodennebel.cs), der Dunst ist
    /// der Schleier darüber, in dem die Sonne ihre Lichtbahnen zieht. Seine Höhe misst Godot ab y = 0, nicht ab dem Boden – zu dicht gab er
    /// über tief liegendem Wasser eine gleichmäßig graue Fläche.
    /// </summary>
    private const float DunstDichte = 0.08f, DunstAbfall = 0.7f;

    /// <summary>Innerhalb der Stadtmauer nimmt ein zweites Volumen mit negativer Dichte so viel weg, dass nur ein leichter Schleier bleibt.</summary>
    private const float DunstStadtAnteil = 0.35f;

    /// <summary>Farbe der Dunsttröpfchen: kühl-grau. Im Gegenlicht der Morgensonne wird der Dunst von selbst zartgolden.</summary>
    private static readonly Color DunstFarbe = new(0.7f, 0.8f, 0.95f);

    /// <summary>Höhennebel der niedrigen Stufe: bis zu dieser Höhe (Meter) und wie dicht darunter.</summary>
    private const float DunstHoehe = 3f, DunstHoehenDichte = 0.055f;

    /// <summary>Feiner Staub in der Luft der Raumansicht (nur Stufe Hoch): macht das Licht der Fenster als Bahn sichtbar.</summary>
    private const float RaumDunstDichte = 0.025f;

    /// <summary>So viel heller als ihr Licht im Raum leuchten die Fensterlichter im Staub (Godots <c>LightVolumetricFogEnergy</c>, Grundwert 1):
    /// Die Lichtbahn soll zu sehen sein, ohne dass der ganze Raum neblig wird.</summary>
    private const float RaumLichtbahn = 8f;

    private float _dunstAnteil = -1;

    /// <summary>
    /// Kann dieser Renderer Volumennebel? Unter OpenGL (Kompatibilitäts-Renderer, etwa im Klicktest mit <c>--rendering-driver opengl3</c>) gibt es weder
    /// <see cref="FogVolume"/> noch volumetrischen Nebel; schon ein <see cref="FogMaterial"/> meldet „shader type fog not supported in OpenGL renderer“.
    /// Dann wird kein Volumen angelegt, und der Morgendunst Hoch wirkt wie Niedrig (Höhennebel und Bodennebel, <see cref="DunstWirksam"/>).
    /// </summary>
    private static bool VolumennebelMoeglich => _volumennebelMoeglich ??= !RenderingServer.GetCurrentRenderingDriverName().StartsWith("opengl");

    private static bool? _volumennebelMoeglich;

    /// <summary>Die Stufe des Morgendunsts, wie sie wirkt: Hoch ohne Volumennebel (OpenGL) wie Niedrig.</summary>
    private static Morgendunst DunstWirksam =>
        Fenster.AktuellerDunst == Morgendunst.Hoch && !VolumennebelMoeglich ? Morgendunst.Niedrig : Fenster.AktuellerDunst;

    private void DunstEinrichten()
    {
        if (!VolumennebelMoeglich) return; // OpenGL: kein Volumen, kein FogMaterial (siehe VolumennebelMoeglich)
        _dunstStoff = new FogMaterial { Density = 0, HeightFalloff = DunstAbfall, EdgeFade = 0.1f, Albedo = DunstFarbe };
        _stadtDunstStoff = new FogMaterial { Density = 0, HeightFalloff = DunstAbfall, EdgeFade = 0.15f, Albedo = DunstFarbe };
        _dunstVolumen = new FogVolume
        {
            Name = "Morgendunst",
            Shape = RenderingServer.FogVolumeShape.Box,
            Size = new Vector3(1400, 60, 1400),
            Material = _dunstStoff,
            Visible = false,
        };
        AddChild(_dunstVolumen);
    }

    private FogMaterial? _stadtDunstStoff;
    private readonly List<FogVolume> _stadtDunst = new();

    /// <summary>
    /// Je Stadtmauer ein Kasten über ihrem umschließenden Rechteck (etwas eingerückt, weich auslaufend) mit negativer Dichte: In der Stadt
    /// bleibt ein Schleier, vor der Mauer liegt der volle Dunst über Feldern und Wiesen. Angelegt, sobald die Karte ihre Mauern kennt.
    /// </summary>
    private void StadtDunstAnlegen()
    {
        if (_stadtDunstStoff is null || _stadtDunst.Count == _mauerFlaechen.Count) return;
        foreach (var alt in _stadtDunst) alt.QueueFree();
        _stadtDunst.Clear();
        foreach (var flaeche in _mauerFlaechen)
        {
            var rahmen = new Rect2(flaeche[0], Vector2.Zero);
            foreach (var p in flaeche) rahmen = rahmen.Expand(p);
            var mitte = rahmen.GetCenter();
            var volumen = new FogVolume
            {
                Name = "Stadtdunst",
                Shape = RenderingServer.FogVolumeShape.Box,
                Size = new Vector3(rahmen.Size.X + 6, 60, rahmen.Size.Y + 6),
                Position = new Vector3(mitte.X, 0, mitte.Y),
                Material = _stadtDunstStoff,
                Visible = _dunstVolumen?.Visible ?? false,
            };
            AddChild(volumen);
            _stadtDunst.Add(volumen);
        }
    }

    /// <summary>
    /// Wie dicht der Dunst gerade ist (1 = voller Anteil). Er folgt dem Bodennebel (Stadtansicht.Bodennebel.cs): an einem Nebelmorgen bis 0,7, an
    /// einem ruhigen Morgen ohne Nebel ein leichter Dunst (bis 0,25 nach dem Verlauf des Nebels: vor und um Sonnenaufgang, danach aufgelöst), bei
    /// Tag ein Hauch (damit die Sonne Lichtbahnen hat), bei Wetter „Nebel“ 0,45. Nachts nur mit <see cref="Nachtnebel"/>; seine Farbe ist dann dunkel
    /// (<see cref="DunstNachUhrzeit"/>). Früher lag jeden Morgen voller Dunst, unabhängig vom Wetter.
    /// </summary>
    private float DunstAnteilJetzt()
    {
        var jahreszeit = _letzteLichtJahreszeit ?? Jahreszeit.Fruehling;
        var stunde = _letzteLichtUhrzeit >= 0 ? _letzteLichtUhrzeit / 60f : 12f; // im Hauptmenü (ohne Uhr): Mittag
        var (verlauf, _) = NebelVerlauf(stunde, jahreszeit, Nachtnebel);
        var (nebelLand, _) = NebelStaerkeJetzt();
        var hauch = 0.05f * (1f - NebelDunkel());
        return Mathf.Max(Mathf.Max(hauch, 0.25f * verlauf * NebelWetterRuhe()), Mathf.Max(0.7f * nebelLand, 0.45f * _wetter.Nebel));
    }

    private void DunstAnwenden()
    {
        var stufe = DunstWirksam;
        var umgebung = _umgebung.Environment;
        umgebung.VolumetricFogEnabled = stufe == Morgendunst.Hoch;
        umgebung.VolumetricFogDensity = 0;   // keine Luft zwischen Kamera und Stadt – nur das Volumen am Boden
        umgebung.VolumetricFogLength = 280;  // die Kamera steht 150 m vor dem Drehpunkt, der Boden reicht bis etwa 250 m
        umgebung.VolumetricFogDetailSpread = 1; // Schichten gleich tief: Die Stadt liegt weit vor der Kamera, nicht direkt davor
        umgebung.VolumetricFogAnisotropy = 0.5f; // Licht streut nach vorn: zur Sonne hin heller
        umgebung.VolumetricFogTemporalReprojectionEnabled = true;
        umgebung.VolumetricFogAmbientInject = 0.5f; // auch das Himmelslicht hellt den Dunst auf, nicht nur die Sonne
        umgebung.FogEnabled = stufe == Morgendunst.Niedrig;
        umgebung.FogMode = Godot.Environment.FogModeEnum.Exponential;
        umgebung.FogDensity = 0;             // nur nach Höhe, nicht nach Abstand (die Kamera ist orthografisch)
        umgebung.FogHeight = DunstHoehe;
        umgebung.FogSkyAffect = 0;
        if (_dunstVolumen is not null) _dunstVolumen.Visible = stufe == Morgendunst.Hoch;
        foreach (var v in _stadtDunst) v.Visible = stufe == Morgendunst.Hoch;
        _raumUmgebung.VolumetricFogEnabled = stufe == Morgendunst.Hoch;
        _raumUmgebung.VolumetricFogDensity = RaumDunstDichte;
        _raumUmgebung.VolumetricFogLength = 40;
        _raumUmgebung.VolumetricFogAlbedo = new Color(0.9f, 0.85f, 0.75f);
        _raumUmgebung.VolumetricFogTemporalReprojectionEnabled = true;
        _raumUmgebung.VolumetricFogAmbientInject = 0.3f; // der Staub hellt den Raum etwas auf, statt ihn zu verdunkeln
        _dunstAnteil = -1;
        _lichtbahnenFuer = null;
    }

    private Raum? _lichtbahnenFuer;

    /// <summary>
    /// In der Raumansicht: Die Fensterlichter (<see cref="SpotLight3D"/> im Modell) bekommen eine kräftige Lichtbahn im Staub. Einmal je
    /// Raum, denn das Durchsuchen der Einrichtung kostet etwas.
    /// </summary>
    private void LichtbahnenImRaum()
    {
        if (_raum is null || _raum == _lichtbahnenFuer || _innen?.Innen is not { } innen) return;
        _lichtbahnenFuer = _raum;
        foreach (var licht in innen.FindChildren("*", nameof(SpotLight3D), true, false).OfType<SpotLight3D>())
            licht.LightVolumetricFogEnergy = Fenster.AktuellerDunst == Morgendunst.Hoch ? RaumLichtbahn : 1f;
    }

    private void DunstNachUhrzeit()
    {
        LichtbahnenImRaum();
        if (Fenster.AktuellerDunst == Morgendunst.Aus) return;
        StadtDunstAnlegen();
        var anteil = DunstAnteilJetzt(); // folgt dem Bodennebel und dem Wetter (dazu Entfernungsnebel bei Wetter „Nebel“)
        var dunkel = NebelDunkel();
        if (Mathf.Abs(anteil - _dunstAnteil) < 0.005f && Mathf.Abs(dunkel - _dunstDunkel) < 0.01f) return;
        _dunstAnteil = anteil;
        _dunstDunkel = dunkel;
        var umgebung = _umgebung.Environment;
        if (_dunstVolumen is not null) _dunstVolumen.Position = new Vector3(_kameraDrehung.Position.X, 0, _kameraDrehung.Position.Z);
        if (_dunstStoff is not null) _dunstStoff.Density = DunstDichte * anteil;
        if (_stadtDunstStoff is not null) _stadtDunstStoff.Density = -DunstDichte * anteil * (1 - DunstStadtAnteil);
        // Der einfache Nebel leuchtet nicht selbst und wird nicht beleuchtet: Er bekommt die Farbe, die ein weißes Tuch in diesem
        // Licht hätte – Umgebungslicht und etwas Sonne (morgens rosig und gedämpft, mittags hell). Nachts so dunkel wie eine unbeleuchtete
        // Wand (NebelNachtfarbe, Stadtansicht.Bodennebel.cs): Die alte Farbe war fünf- bis zehnmal so hell, der helle Saum am Fuß der Mauern.
        var licht = umgebung.AmbientLightColor * umgebung.AmbientLightEnergy + _sonne.LightColor * _sonne.LightEnergy * 0.35f;
        umgebung.FogLightColor = new Color(licht.R, licht.G, licht.B).Lerp(NebelNachtfarbe(), dunkel);
        umgebung.FogHeightDensity = DunstHoehenDichte * anteil;
        umgebung.FogSunScatter = 0.15f * (1f - dunkel); // nachts wäre die „Sonne“ der Mond
        // Volumetrisch (Hoch): Godot beleuchtet den Nebel selbst – nachts weniger Umgebungslicht und Mond im Nebel, damit er nicht heller wird
        // als die Wände dahinter; Laternen und Feuer behalten ihren Lichthof.
        umgebung.VolumetricFogAmbientInject = Mathf.Lerp(0.5f, 0.25f, dunkel);
        _sonne.LightVolumetricFogEnergy = Mathf.Lerp(1f, 0.25f, dunkel);
    }

    private float _dunstDunkel = -1;

    // ═════════════════════════════════════════════════════════════════════════
    //  Farbstimmung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tonemapping und die drei Regler einer Umgebung. „Bisher“ ist Godots lineares Abbilden (Werte über 1 werden abgeschnitten),
    /// AgX rollt helle Stellen weich ab und entsättigt sie leicht, wie ein Film. Helligkeit, Kontrast und Sättigung wirken danach
    /// auf das fertige Bild (Godots <c>Adjustment</c>), 100 % lässt es, wie es ist.
    /// </summary>
    private static void FarbstimmungAnwenden(Godot.Environment umgebung)
    {
        umgebung.TonemapMode = Fenster.AktuellesTonemapping == Tonemapping.Agx ? Godot.Environment.ToneMapper.Agx : Godot.Environment.ToneMapper.Linear;
        umgebung.AdjustmentEnabled = Fenster.Helligkeit != 100 || Fenster.Kontrast != 100 || Fenster.Saettigung != 100;
        umgebung.AdjustmentBrightness = Fenster.Helligkeit / 100f;
        umgebung.AdjustmentContrast = Fenster.Kontrast / 100f;
        umgebung.AdjustmentSaturation = Fenster.Saettigung / 100f;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Detailstufe
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein MultiMesh der Natur (Stadtansicht.Natur.cs), wie viele Kopien es hat und ob es Bäume bzw. Feinheiten sind.</summary>
    private readonly record struct NaturDetail(MultiMeshInstance3D Teil, int Gesamt, bool Baum, bool Feinheit);

    private readonly List<NaturDetail> _naturDetails = new();

    /// <summary>Feinheiten (Bodenpflanzen, Steine, Holz) bis zu welchem Zoom: Nah, Mittel, Weit (darüber ausgeblendet).</summary>
    private static float FeinheitenBisZoom(Feinheiten f) => f switch
    {
        Feinheiten.Nah => 55f,
        Feinheiten.Mittel => 100f,
        _ => float.MaxValue,
    };

    private bool? _feinheitenSichtbar;

    private void NaturDetailMelden(MultiMeshInstance3D teil, string datei)
    {
        var detail = new NaturDetail(teil, teil.Multimesh.InstanceCount, IstBaumDatei(datei),
                                     IstBodenDatei(datei) || datei is "natur/natur_steine" or "natur/natur_holz" or "natur/natur_kit_steine");
        _naturDetails.Add(detail);
        NaturDichteSetzen(detail);
    }

    /// <summary>Natur-Dichte: Das MultiMesh zeigt nur die ersten Kopien (sie sind gemischt, siehe NaturAufbauen). Bäume halb so stark –
    /// ein Wald soll ein Wald bleiben.</summary>
    private static void NaturDichteSetzen(NaturDetail d)
    {
        var anteil = Fenster.NaturDichte / 100f;
        if (d.Baum) anteil = 0.5f + 0.5f * anteil;
        var zahl = anteil >= 1 ? -1 : Mathf.RoundToInt(d.Gesamt * anteil); // −1 = alle
        if (d.Teil.Multimesh.VisibleInstanceCount != zahl) d.Teil.Multimesh.VisibleInstanceCount = zahl;
    }

    private void NaturDetailAnwenden()
    {
        _naturDetails.RemoveAll(d => !IsInstanceValid(d.Teil));
        foreach (var d in _naturDetails) NaturDichteSetzen(d);
        _feinheitenSichtbar = null;
    }

    /// <summary>Feinheiten nach Zoom: Weit herausgezoomt sind Halme und Kiesel nur noch ein Flimmern, kosten aber Zeichenarbeit.</summary>
    private void FeinheitenNachZoom()
    {
        var sichtbar = _raum is not null || _kamera.Size <= FeinheitenBisZoom(Fenster.AktuelleFeinheiten);
        if (_feinheitenSichtbar == sichtbar) return;
        _feinheitenSichtbar = sichtbar;
        foreach (var d in _naturDetails)
            if (d.Feinheit && IsInstanceValid(d.Teil)) d.Teil.Visible = sichtbar;
    }

    /// <summary>Menge der Passanten: Ob dieser Einwohner zu sehen ist. Fest je Nummer gestreut – wer gezeigt wird, bleibt es.</summary>
    private static bool PassantGezeigt(int nummer) =>
        Fenster.PassantenMenge >= 100 || (uint)(nummer * 2654435761u) % 100 < Fenster.PassantenMenge;

    /// <summary>Einen nicht gezeigten Passanten ausblenden: Er gibt seine Stelle auf dem Markt frei und plant beim Wiederzeigen neu.</summary>
    private void PassantVerbergen(Passant p, int nummer)
    {
        if (!p.Knoten.Visible && p.Stand is null && p.FuerWeg is null) return;
        p.Knoten.Visible = false;
        MarktstelleRaeumen(p.Marktstelle, nummer);
        MarktstelleRaeumen(p.AlteMarktstelle, nummer);
        p.Marktstelle = p.AlteMarktstelle = -1;
        p.Stand = null;
        p.FuerWeg = null;
        _imDom.Remove(nummer);
    }
}
