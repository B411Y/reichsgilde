using Godot;

namespace Reichsgilde;

/// <summary>
/// Globale Beleuchtung mit SDFGI (Einstellung „Globale Beleuchtung (SDFGI)“ mit den Unterschaltern „Innenräume“ und „Außenansicht – experimentell“,
/// Entscheidung des Nutzers 30.10.2026: Standard Aus, zum Ausprobieren; Innenräume an, Außenansicht aus) – ein Teil der <c>partial class Stadtansicht</c>.
///
/// <para><b>Was SDFGI tut:</b> Godot legt um die Kamera Würfel aus Zellen („Kaskaden“, je 128 Zellen breit, jede doppelt so grob wie die vorige),
/// trägt die Geometrie darin als Abstandsfeld ein und schickt von Sonden Strahlen hindurch. So kommt Licht von Wänden, Boden und Himmel zurück,
/// auch von Dingen außerhalb des Bildes, und enge Gassen bekommen weniger Himmel ab als offene Plätze. Das Ergebnis <b>ersetzt</b> das
/// Umgebungslicht der Umgebung (im Godot-Quelltext: <c>ambient_light = mix(ambient_light, gi, gi.a)</c>).</para>
///
/// <para><b>Warum die Stadt grobe Zellen bekommt:</b> Die Kaskaden folgen der Kameraposition (<c>cam_transform.origin</c>, renderer_scene_cull.cpp),
/// eine Verschiebung gibt es nicht. Unsere Kamera steht orthografisch 150 m schräg über dem Blickpunkt (96 m hoch, 115 m zurück). Was im Bild ist,
/// liegt also 95 bis 225 m von ihr entfernt, die feinen Kaskaden um die Kamera enthielten nur Luft. Die Stadt bekommt darum eine grobe Kaskade,
/// die bis über den Bildrand reicht: Zellen von 5 m, Sonden alle 40 m. Feiner geht es mit dieser Kamera kaum: Eine Kaskade reicht 64 Zellen weit,
/// schon für Zoom 25 (140 m) braucht es mindestens 2,2 m je Zelle. Licht in einzelnen Gassen kann SDFGI so nicht zeigen, nur großflächig.</para>
///
/// <para><b>Raumansicht:</b> Die Raumkamera steht nah im Raum (perspektivisch), dort passen feine Kaskaden ab 0,1 m. Der Raum hat seine eigene
/// Umgebung mit eigenen Kaskaden; beim Wechsel zwischen Stadt und Raum legt Godot das Feld neu an (ein paar Bilder, bis es sich gesetzt hat).
/// Die Räume sind für ein gleichmäßiges Grundlicht gebaut, das SDFGI wegnimmt; wie es zurückkommt, steht unten unter „Räume mit SDFGI“.</para>
///
/// <para><b>Was nicht nachkommt:</b> Godot trägt die Geometrie nur ein, wenn eine Kaskade neu angelegt wird oder beim Verschieben der Kamera ein
/// neuer Streifen dazukommt. Was danach gebaut, abgerissen oder bewegt wird, und Leuchten (Fensterlicht, Glut), die sich ändern, kommen erst
/// beim nächsten Verschieben an diese Stelle oder beim nächsten Umschalten nach (Godot-Doku: „dynamic occluders and emissive surfaces are not
/// supported“). Darum tragen Partikel und Figuren mit Skelett gar nicht bei (<see cref="GeometryInstance3D.GIModeEnum.Disabled"/>): Sie
/// empfangen das Licht, stehen aber nicht als Geister an der Stelle, an der sie beim Eintragen waren.</para>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Kaskaden einer Umgebung: Anzahl, Kantenlänge der feinsten Zelle (Meter), Höhe der Zellen und Godots Verfahren gegen Lichtlecks.</summary>
    internal readonly record struct SdfgiKaskaden(int Anzahl, float Zelle, Godot.Environment.SdfgiyScale Hoehe, bool GegenLecks);

    /// <summary>
    /// Kaskaden der Stadt, für alle Stufen gleich: eine Kaskade zu 5 m (128 Zellen, bis ±320 m um die Kamera, volle Zellhöhe, weil das Bild 96 m unter
    /// der Kamera liegt). Ihr Mittelpunkt rückt in Schritten von 8 Zellen nach, sicher reichen also etwa 56 Zellen = 280 m: Das deckt das ganze Bild bis
    /// Zoom 140 bei jeder Drehung. Zwei Kaskaden (2,5 m und 5 m) waren im Versuch schlechter: An der Grenze zwischen ihnen lag ein dunkles Band quer über
    /// dem Boden, die feinere zeigte Flecken. Godots Verfahren gegen Lichtlecks bleibt in der Stadt aus (es macht im Freien dunkle Flecken, Godot-Doku).
    /// Die Stufen unterscheiden sich in der Stadt nur in Strahlen, Auflösung und Nachführen (<see cref="GlobaleBeleuchtungGueteSetzen"/>).
    /// </summary>
    internal static SdfgiKaskaden StadtKaskaden(GlobaleBeleuchtung stufe) => new(1, 5f, Godot.Environment.SdfgiyScale.Scale100Percent, false);

    /// <summary>
    /// Kaskaden der Raumansicht je Stufe: Die Kamera steht nah, die Wände sind 0,2–0,5 m dick. Niedrig 0,4 m (zwei Kaskaden, bis ±51 m),
    /// Mittel 0,2 m (drei, bis ±51 m), Hoch 0,1 m (vier, bis ±51 m) mit Verdeckung gegen Lichtlecks.
    /// <para><b>Volle Zellhöhe (100 %), nicht Godots Grundwert 75 %:</b> Godot prüft für jede Kaskade, ob ein Punkt- oder Spotlicht hineinreicht, und vergleicht dafür
    /// den Würfel der Kaskade in gestauchten Höhen (mal 1,5 bei 75 %) mit dem Umriss des Lichts in Metern (gi.cpp, <c>cascade_aabb.intersects(light_aabb)</c>
    /// ohne Rückrechnung). Nahe der Höhe 0 fällt das kaum auf; unsere Räume liegen aber 200 m unter der Stadt (<see cref="RaumTiefe"/>): Der Würfel stand dort
    /// rund 100 m tiefer als das Licht, und kein einziges Licht des Raums kam in SDFGI an (Glut, Laterne, Fenster – im Versuch vom 30.10.2026 änderte darum
    /// ein stärkeres Fensterlicht nichts). Mit 100 % rechnet Godot nicht um, und die Lichter wirken.</para>
    /// </summary>
    internal static SdfgiKaskaden RaumKaskaden(GlobaleBeleuchtung stufe) => stufe switch
    {
        GlobaleBeleuchtung.Niedrig => new(2, 0.4f, Godot.Environment.SdfgiyScale.Scale100Percent, false),
        GlobaleBeleuchtung.Mittel => new(3, 0.2f, Godot.Environment.SdfgiyScale.Scale100Percent, false),
        _ => new(4, 0.1f, Godot.Environment.SdfgiyScale.Scale100Percent, true),
    };

    /// <summary>
    /// Was für alle Umgebungen gilt (RenderingServer): Strahlen je Sonde, über wie viele Bilder gemittelt wird, auf wie viele Bilder das Licht
    /// verteilt wird und ob das Ergebnis auf halber Auflösung gerechnet wird. Godots Grundwerte: 8 Strahlen, 30 Bilder, Licht in 4 Bildern, volle Auflösung.
    /// <para><b>Halbe Auflösung nur bei Niedrig:</b> Godot rechnet das indirekte Licht dann in einem halb so großen Puffer und liest ihn beim Zeichnen nur
    /// bilinear aus (<c>scene_forward_clustered.glsl</c>, ohne Rücksicht auf Kanten). Wo das indirekte Licht springt (Fensterlaibung gegen Wand, Wandecke,
    /// Fachwerkpfosten), entstehen Treppen aus 2 Pixeln; ohne SDFGI sind dieselben Kanten glatt. Mittel hatte bis zur Prüfung am 30.10.2026 auch halbe Auflösung.</para>
    /// </summary>
    internal static void GlobaleBeleuchtungGueteSetzen(GlobaleBeleuchtung stufe)
    {
        if (stufe == GlobaleBeleuchtung.Aus || !Fenster.GlobaleBeleuchtungMoeglich) return; // die Werte zählen nur, solange SDFGI an ist
        var (strahlen, licht, halb) = stufe switch
        {
            GlobaleBeleuchtung.Niedrig => (RenderingServer.EnvironmentSdfgiRayCount.Count8, RenderingServer.EnvironmentSdfgiFramesToUpdateLight.In8Frames, true),
            GlobaleBeleuchtung.Mittel => (RenderingServer.EnvironmentSdfgiRayCount.Count16, RenderingServer.EnvironmentSdfgiFramesToUpdateLight.In4Frames, false),
            _ => (RenderingServer.EnvironmentSdfgiRayCount.Count32, RenderingServer.EnvironmentSdfgiFramesToUpdateLight.In2Frames, false),
        };
        RenderingServer.EnvironmentSetSdfgiRayCount(strahlen);
        RenderingServer.EnvironmentSetSdfgiFramesToConverge(RenderingServer.EnvironmentSdfgiFramesToConverge.In30Frames);
        RenderingServer.EnvironmentSetSdfgiFramesToUpdateLight(licht);
        RenderingServer.GISetUseHalfResolution(halb);
    }

    /// <summary>
    /// Wie hell das Licht der Raumansicht zurückkommt und wie viel davon bei jedem weiteren Abprallen bleibt: Godots Grundwerte (1 und 0,5). Die Helligkeit
    /// der Räume stellen die Lichter ein (unten), nicht diese Zahlen. Der Raum liest keinen Himmel (sein Hintergrund ist fast schwarz, Godot rät bei Innenräumen ab).
    /// Gesetzt wird die Energie geteilt durch (1 − <see cref="RaumGrundlichtAnteil"/>): Die Sonde des Grundlichts mischt SDFGI auf diesen Rest herunter
    /// (<see cref="_giGrundlicht"/>), so bleibt sein Beitrag so groß wie mit Energie 1 ohne Sonde.
    /// </summary>
    private const float RaumGiEnergie = 1f, RaumGiRueckwurf = 0.5f;

    /// <summary>
    /// Anteil des Himmels im Licht der Stadt, gemessen am Umgebungslicht ohne SDFGI (1 = gleich hell auf einer offenen Fläche). Godot liest die
    /// Hintergrundfarbe als Himmel ohne Umrechnung (sRGB als linear), das wäre rund doppelt so hell; <see cref="GlobaleBeleuchtungHelligkeit"/> gleicht es aus.
    /// Mit 1 wurden die Schatten am Boden um ein Fünftel heller (Köln 14 Uhr, Zoom 95: 67 → 80 von 255), weil besonnte Wände und Boden zurückstrahlen,
    /// und das Bild flacher. 0,8 hält die Schatten etwa so dunkel wie ohne SDFGI; was bleibt, ist der Unterschied: dunkler, wo wenig Himmel hinkommt,
    /// heller neben besonnten Flächen.
    /// </summary>
    private const float StadtGiAnteil = 0.8f;

    /// <summary>Stellt SDFGI einer Umgebung (Stadt oder Raumansicht) nach der Einstellung ein (aus <see cref="VerdeckungAnwenden"/>).</summary>
    internal static void GlobaleBeleuchtungAnwenden(Godot.Environment umgebung, bool raum)
    {
        var stufe = Fenster.AktuelleGlobaleBeleuchtung;
        var an = Fenster.GlobaleBeleuchtungWirkt(raum); // Stufe, Forward+ und der Unterschalter „Innenräume“ bzw. „Außenansicht“
        umgebung.SdfgiEnabled = an;
        if (!an) return;
        var k = raum ? RaumKaskaden(stufe) : StadtKaskaden(stufe);
        umgebung.SdfgiCascades = k.Anzahl;
        // Eine Spur anders als beim letzten Mal (<see cref="GlobaleBeleuchtungNeuAnlegen"/>): Godot legt das Feld dann neu an
        var folge = umgebung.HasMeta(GiFolgeSchluessel) ? umgebung.GetMeta(GiFolgeSchluessel).AsInt32() : 0;
        umgebung.SdfgiMinCellSize = k.Zelle + folge % 997 * 0.00001f;
        umgebung.SdfgiYScale = k.Hoehe;
        umgebung.SdfgiUseOcclusion = k.GegenLecks;
        umgebung.SdfgiReadSkyLight = !raum;
        if (raum)
        {
            umgebung.SdfgiEnergy = RaumGiEnergie / (1f - RaumGrundlichtAnteil); // die Sonde des Grundlichts nimmt davon wieder (1 − Anteil)
            umgebung.SdfgiBounceFeedback = RaumGiRueckwurf;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Räume mit SDFGI: Grundlicht anteilig, Fülllicht, Zwillinge der Raumlichter
    // ═════════════════════════════════════════════════════════════════════════
    //
    // SDFGI ersetzt das Grundlicht der Räume (Umgebungslicht, warm, nach der Tageszeit) ganz. Ohne Ersatz wurde die Schmiede von 82 auf 17 von 255 dunkel
    // (mittlere Bildhelligkeit, Mittel, Tag). Ziel (Auftrag 30.10.2026): je Raum −15 % bis +10 % der Helligkeit ohne SDFGI, das Licht sichtbar weicher.
    // Die Räume bekommen darum, nur solange SDFGI in der Raumansicht wirkt, vier Dinge:
    //  1. Grundlicht anteilig (RaumGrundlichtAnteil, _giGrundlicht): Eine Reflexionssonde ohne Spiegelbild mischt nach SDFGI einen festen Anteil des Grundlichts
    //     zurück – in jeder Richtung gleich, wie das Umgebungslicht ohne SDFGI, und wie dieses von der Umgebungsverdeckung (SSAO) gedämpft. Er hält auch Räume
    //     aus dunklem Holz hell genug (Dachboden: das zurückgeworfene Licht allein ist dort schwach) und geht nicht in SDFGI ein. Bis zur Prüfung am 30.10.2026
    //     kam er aus sechs gerichteten Lichtern entlang der Achsen: Eine Fläche bekam davon |nx| + |ny| + |nz|, Schrägen der Normal-Maps und Facetten bis zu
    //     73 % mehr als eine glatte Wand – helle Säume an beiden Seiten jeder Fuge, Kringel im Putz, facettierte Figuren; dazu kosteten sie Rechenzeit.
    //  2. Fülllicht (RaumFuellAnteil): ein unsichtbares Punktlicht vor der Raumkamera, das nichts direkt beleuchtet (LightCullMask 0) und nur in SDFGI eingeht:
    //     Es hellt Wände, Boden und Decke auf, und was sie zurückwerfen, ist das weiche Licht mit dunkleren Ecken und Nischen.
    //  3. Zwillinge der Raumlichter (GiZwillingAnlegen): Die Fensterlichter der Modelle sitzen in der Wand, Glut und Laterne in ihrem Körper – SDFGI fände
    //     den Weg zu ihnen verdeckt. Ein Zwilling davor (Fenster, 0,6 m, weiter und breiter), darüber (Glut) oder darunter (Laterne) bringt ihr Licht hinein;
    //     das Tageslicht der Fenster stark (FensterGi, bei mehr als vier Fenstern im Raum geteilt), die Glut gedämpft (GlutGi), damit die Esse den Raum
    //     nicht rot färbt, aber warm bleibt.
    //  4. Leuchtende Flächen (Glut, Fensterscheiben) trägt Godot nur beim Anlegen des Feldes ein: Der Raum legt es neu an, wenn die Glut an- oder ausgeht und
    //     wenn sich das Tageslicht merklich geändert hat (GiRaumNeuAnlegen).
    // Messung und Bilder: docs/bereiche/darstellung.md („Globale Beleuchtung SDFGI: Innenräume und Außenansicht“), Startoption --givergleich.

    /// <summary>
    /// Anteil des Grundlichts (Umgebungslicht ohne SDFGI), der mit SDFGI fest bleibt (0 bis unter 1). Die Sonde mischt: Umgebungslicht = (1 − Anteil) · SDFGI
    /// + Anteil · Grundlicht; SDFGI bekommt dafür die Energie / (1 − Anteil). Abgestimmt mit <c>--givergleich</c> in allen Räumen bei Tag und Nacht (31.10.2026: alle zwischen −11 % und +10 %; mit 0,7 lag der Kerker
    /// bei −10 %, die Räume 1–2 Punkte dunkler als zuvor). Zuvor trugen
    /// sechs gerichtete Lichter je 0,65 (eine glatte Wand bekam 0,65, Schrägen bis 1,1); Versuche damals: 0,45 ließ den Dachboden der Schmiede nachts 27 %
    /// dunkler, 0,6 noch 16 %.
    /// </summary>
    private const float RaumGrundlichtAnteil = 0.75f;

    /// <summary>
    /// Stärke des Fülllichts nach dem Grundlicht (Energie = Anteil × <see cref="RaumGrundlicht"/>): Es leuchtet ohne Abfall bis <see cref="FuellWeite"/> Meter.
    /// Mehr macht helle Räume (Putz) heller, kaum dunkle (Holz) – darum trägt das Grundlicht den festen Teil und das Fülllicht nur den Rest.
    /// </summary>
    private const float RaumFuellAnteil = 1.5f, FuellWeite = 20f;

    /// <summary>So weit vor der Raumkamera steht das Fülllicht: Die Kamera steht in Modellräumen dicht an der Vorderwand, das Licht muss im freien Raum stehen.</summary>
    private const float FuellVersatz = 1f;

    /// <summary>Anteil von Fenster, Glut und Laterne an SDFGI (<c>LightIndirectEnergy</c> ihrer Zwillinge; 1 = so stark wie ihr Licht im Raum).</summary>
    internal const float FensterGi = 5f, GlutGi = 0.6f, LampeGi = 1f;

    /// <summary>Zwilling eines Fensters: so weit vor der Scheibe, so weit reichend und so breit (Grad) wie ein Lichtkegel durchs Fenster; Glut und Laterne: so weit darüber bzw. darunter.</summary>
    private const float FensterGiVersatz = 0.6f, FensterGiWeite = 9f, FensterGiWinkel = 75f, GlutGiVersatz = 0.35f;

    /// <summary>Unsichtbares Fülllicht an der Raumkamera: Es beleuchtet nichts direkt (<c>LightCullMask</c> 0), geht aber in SDFGI ein.</summary>
    private OmniLight3D? _giFuelllicht;

    /// <summary>
    /// Die Sonde des festen Grundlichts (nur mit SDFGI in der Raumansicht sichtbar). Godot mischt das Umgebungslicht einer Reflexionssonde <b>nach</b> SDFGI ein
    /// (<c>scene_forward_clustered.glsl</c>, 4.7.2: Umgebungslicht · (1 − a) + Farbe der Sonde · a), mit dem Gewicht a = (Überblendung)², das zum Rand des Kastens
    /// hin auf 0 fällt. Damit a im ganzen Raum gleich ist, ist der Kasten riesig und die Räume liegen in seiner Überblendzone, wo 1 − |y| / <see cref="GrundlichtKasten"/>
    /// = √<see cref="RaumGrundlichtAnteil"/> ist; in X und Z liegt alles tiefer im Kasten als die Überblendung reicht (dort zählt 1). 50 m Höhenunterschied
    /// ändern a um gut 1 %. Die Farbe der Sonde ist genau das Grundlicht (Farbe und Stärke wie das Umgebungslicht der Raumumgebung).
    /// <para>Ein Spiegelbild zeigt die Sonde nicht: Sie sieht nichts (<c>CullMask</c> 0), und ihre Stärke ist fast 0 – aber nicht 0, damit Godot auch das Glanzlicht
    /// von SDFGI mit demselben Gewicht herunternimmt; sonst glänzte Metall mit der höheren SDFGI-Energie stärker. Was ohne SDFGI-Puffer gezeichnet wird
    /// (Durchsichtiges), bekommt (1 − a) · Grundlicht + a · Grundlicht, also dasselbe wie ohne SDFGI. Die Räume liegen bis Ebene 22: <c>ReflectionMask</c> mit allen Bits
    /// (Godots Grundwert reicht nur bis Ebene 20). Im Projekt ist der Sondenatlas klein (64 Pixel, 1 Platz; <c>project.godot</c>), sonst legte Godot beim ersten
    /// Einschalten Platz für 64 Sonden zu 256 Pixeln an (rund 400 MB Grafikspeicher) – das Spiel hat keine andere Reflexionssonde.</para>
    /// </summary>
    private ReflectionProbe? _giGrundlicht;

    /// <summary>
    /// Kasten der Sonde des Grundlichts: halbe Höhe, zugleich Weite der Überblendung (m); Rand in X und Z, in dem sie noch voll wirkt; Höhe der Raummitte über
    /// <see cref="RaumTiefe"/>; Stärke des (leeren) Spiegelbilds.
    /// </summary>
    private const float GrundlichtKasten = 10000f, GrundlichtRand = 3000f, GrundlichtRaumhoehe = 2f, GrundlichtSpiegel = 0.0001f;

    /// <summary>Einmal mit der Raumkamera (<see cref="RaumKameraAufbauen"/>): das Fülllicht an ihr, die Sonde des Grundlichts fest in der Welt; beide nur mit SDFGI im Raum an.</summary>
    private void GiRaumlichtAufbauen()
    {
        _giFuelllicht = new OmniLight3D
        {
            Name = "GiFuelllicht", LightCullMask = 0, ShadowEnabled = false, LightSpecular = 0, Visible = false,
            OmniRange = FuellWeite, OmniAttenuation = 0, LightColor = RaumGrundlichtFarbe,
            Position = new Vector3(0, 0, -FuellVersatz), // vor der Kamera im Raum, nicht in der Wand, an der sie steht
        };
        _raumKamera.AddChild(_giFuelllicht);
        _giGrundlicht = new ReflectionProbe
        {
            Name = "GiGrundlicht", Visible = false, UpdateMode = ReflectionProbe.UpdateModeEnum.Once,
            Size = new Vector3(2 * (GrundlichtKasten + GrundlichtRand), 2 * GrundlichtKasten, 2 * (GrundlichtKasten + GrundlichtRand)),
            BlendDistance = GrundlichtKasten, CullMask = 0, ReflectionMask = uint.MaxValue, Interior = true, Intensity = GrundlichtSpiegel,
            AmbientMode = ReflectionProbe.AmbientModeEnum.Color, AmbientColor = RaumGrundlichtFarbe, AmbientColorEnergy = 0,
            Position = new Vector3(0, RaumTiefe + GrundlichtRaumhoehe + (1 - Mathf.Sqrt(RaumGrundlichtAnteil)) * GrundlichtKasten, 0),
        };
        AddChild(_giGrundlicht); // fest in der Welt (die Stadtansicht liegt im Ursprung)
    }

    /// <summary>
    /// Jedes Bild aus <see cref="RaumlichtSetzen"/>: Fülllicht und Sonde des Grundlichts nur in der Raumansicht mit SDFGI, Stärke nach der Tageszeit wie das Grundlicht;
    /// die Zwillinge der Raumlichter gehen mit der Einstellung an und aus. Hat sich das Tageslicht seit dem Anlegen merklich geändert, wird das Feld neu angelegt.
    /// </summary>
    private void GiRaumlichtNachfuehren(float hell)
    {
        if (_giFuelllicht is null || _giGrundlicht is null) return;
        var raumKennung = _raumUmgebung.SdfgiEnabled ? _raum?.Kennung : null;
        if (raumKennung != _giZwillingeRaum) // nur die Zwillinge des gezeigten Raums: jedes Licht kostet SDFGI Rechenzeit in jeder Kaskade, die es erreicht
        {
            _giZwillingeRaum = raumKennung;
            _giZwillinge.RemoveAll(zwilling => !IsInstanceValid(zwilling)); // Schutz: freigegeben ohne GiZwillingeVergessen (sonst ObjectDisposedException)
            foreach (var zwilling in _giZwillinge)
                zwilling.Visible = raumKennung is not null && zwilling.GetMeta(GiRaumMeta, "").AsString() is var r && (r == "" || r == raumKennung);
        }
        var an = _raum is not null && _raumUmgebung.SdfgiEnabled;
        if (_giFuelllicht.Visible != an)
        {
            _giFuelllicht.Visible = an;
            _giGrundlicht.Visible = an; // die Sonde wirkt auf alles in ihrem Kasten, auch auf die Stadt: nur in der Raumansicht
        }
        if (!an) return;
        if (Mathf.Abs(hell - _giRaumHellBeimAnlegen) > GiRaumHellSchritt) GiRaumNeuAnlegen(); // die Scheiben leuchten anders als beim Anlegen
        var grund = RaumGrundlicht(hell);
        if (Mathf.Abs(grund - _giGrundlicht.AmbientColorEnergy) < 0.002f) return;
        _giFuelllicht.LightEnergy = RaumFuellAnteil * grund;
        _giGrundlicht.AmbientColorEnergy = grund; // wie das Umgebungslicht der Raumumgebung; den Anteil gibt die Lage im Kasten (Farbe oder Stärke neu: keine neue Aufnahme)
    }

    /// <summary>Meta-Schlüssel der Lichter, die nur in SDFGI eingehen: <see cref="EbeneSetzen"/> lässt ihre <c>LightCullMask</c> auf 0.</summary>
    private const string NurGiMeta = "nur_gi";

    /// <summary>Glut und Laterne: Versatz des Zwillings in Weltrichtung (senkrecht, egal wie der Anker im Modell gedreht ist).</summary>
    private const string GiVersatzMeta = "gi_versatz";

    /// <summary>Raum eines Zwillings (Kennung wie <c>raum:werk</c>, leer: unter keinem Raumknoten): Er leuchtet nur, solange dieser Raum gezeigt wird.</summary>
    private const string GiRaumMeta = "gi_raum";

    /// <summary>
    /// Alle Zwillinge der Raumlichter (nur für SDFGI), damit sie mit der Einstellung und dem gezeigten Raum an- und ausgehen – auch die der Betriebe anderer
    /// Häuser. Geht ein Betrieb weg (Verkauf, Bankrott), vergisst <see cref="GiZwillingeVergessen"/> seine Zwillinge.
    /// </summary>
    private readonly List<Light3D> _giZwillinge = new();

    /// <summary>
    /// Aus <see cref="KnotenEntfernen"/>: Die Zwillinge unter <paramref name="knoten"/> (der Innenraum eines verkauften Betriebs) gehen mit ihm. Blieben sie in der
    /// Liste, griffe der nächste Raumwechsel auf freigegebene Lichter zu (ObjectDisposedException), und die Zwillinge danach behielten ihren alten Zustand.
    /// </summary>
    private void GiZwillingeVergessen(Node knoten) => _giZwillinge.RemoveAll(zwilling => !IsInstanceValid(zwilling) || knoten.IsAncestorOf(zwilling));
    /// <summary>Der Raum, dessen Zwillinge gerade leuchten (null: keiner, SDFGI aus oder Stadt).</summary>
    private string? _giZwillingeRaum;

    /// <summary>
    /// Die Lichter der Raummodelle (<c>Licht_&lt;n&gt;</c>) sitzen in der Wand (Fenster), in der Glut oder in der Laterne. SDFGI prüft für jede Zelle, ob
    /// der Weg zum Licht frei ist, und fände ihn dort verdeckt. Ein Zwilling ein Stück daneben im freien Raum bringt ihr Licht in die globale Beleuchtung:
    /// vor dem Fenster (gleich ausgerichtet, aber weiter und breiter), über der Glut, unter der Laterne. Er beleuchtet nichts direkt (<c>LightCullMask</c> 0);
    /// das Licht selbst geht dafür nicht in SDFGI ein (<c>LightIndirectEnergy</c> 0, Stadtansicht.Innenmodell.cs).
    /// </summary>
    private void GiZwillingAnlegen(Light3D licht, LichtArt art)
    {
        Light3D zwilling = art == LichtArt.Fenster
            ? new SpotLight3D { SpotRange = FensterGiWeite, SpotAngle = FensterGiWinkel, Position = new Vector3(0, 0, -FensterGiVersatz) }
            : new OmniLight3D { OmniRange = ((OmniLight3D)licht).OmniRange, OmniAttenuation = ((OmniLight3D)licht).OmniAttenuation };
        zwilling.Name = "GiZwilling";
        zwilling.LightCullMask = 0;
        zwilling.ShadowEnabled = false;
        zwilling.LightSpecular = 0;
        zwilling.LightColor = licht.LightColor;
        zwilling.LightIndirectEnergy = art switch { LichtArt.Glut => GlutGi, LichtArt.Lampe => LampeGi, _ => FensterGi };
        zwilling.Visible = false; // an, sobald sein Raum mit SDFGI gezeigt wird (GiRaumlichtNachfuehren)
        zwilling.SetMeta(NurGiMeta, true);
        zwilling.SetMeta(GiRaumMeta, RaumSchluesselVon(licht) is { Length: > 0 } schluessel ? RaumKennungVon(schluessel) : "");
        if (art != LichtArt.Fenster) zwilling.SetMeta(GiVersatzMeta, new Vector3(0, art == LichtArt.Glut ? GlutGiVersatz : -GlutGiVersatz, 0));
        licht.AddChild(zwilling);
        _giZwillinge.Add(zwilling);
    }

    /// <summary>
    /// So viele Fenster eines Raums tragen voll bei; hat er mehr, teilen sie sich diesen Anteil. Das Kirchenschiff des Predigers hat neun Fensterlichter
    /// und wurde mit vollem Anteil am Tag 16 % heller als ohne SDFGI (Ziel höchstens 10 %); die Werkstätten haben zwei bis vier.
    /// </summary>
    private const int FensterGiHoechstens = 4;

    /// <summary>Nach dem Laden eines Raummodells: Je Raum (<c>Raum_&lt;Schlüssel&gt;</c>) tragen die Fenster zusammen höchstens so viel bei wie <see cref="FensterGiHoechstens"/> einzelne.</summary>
    private static void GiFensterGewichten(IEnumerable<Light3D> fensterlichter)
    {
        foreach (var raum in fensterlichter.GroupBy(RaumSchluesselVon))
        {
            var anteil = FensterGi * Mathf.Min(1f, FensterGiHoechstens / (float)raum.Count());
            foreach (var licht in raum)
                foreach (var kind in licht.GetChildren())
                    if (kind is Light3D zwilling && zwilling.HasMeta(NurGiMeta)) zwilling.LightIndirectEnergy = anteil;
        }
    }

    /// <summary>Der Zwilling leuchtet so stark wie sein Licht (Tageszeit, Arbeit); sein Anteil an SDFGI steht in <c>LightIndirectEnergy</c>.</summary>
    private static void GiZwillingNachfuehren(Light3D licht)
    {
        foreach (var kind in licht.GetChildren())
        {
            if (kind is not Light3D zwilling || !zwilling.HasMeta(NurGiMeta)) continue;
            zwilling.LightEnergy = licht.LightEnergy;
            if (zwilling.HasMeta(GiVersatzMeta) && zwilling.IsInsideTree())
                zwilling.GlobalPosition = licht.GlobalPosition + zwilling.GetMeta(GiVersatzMeta).AsVector3();
        }
    }

    /// <summary>
    /// Jedes Bild nach dem Licht der Stunde (<see cref="LichtNachUhrzeit"/>): Die Stärke von SDFGI folgt dem Umgebungslicht, das es ersetzt. Godot
    /// nimmt als Himmel die Hintergrundfarbe, so wie sie eingestellt ist (sRGB, ohne Umrechnung); das Umgebungslicht rechnet es dagegen linear. Ohne
    /// Ausgleich wäre die Stadt mit SDFGI am Tag rund doppelt so hell in den Schatten und nachts zu hell. So bleibt eine offene Fläche etwa gleich hell,
    /// und SDFGI ändert nur, was es soll: weniger Himmel in engen Gassen, Licht von besonnten Wänden und vom Boden in den Schatten.
    /// </summary>
    private void GlobaleBeleuchtungHelligkeit()
    {
        var umgebung = _umgebung.Environment;
        if (!umgebung.SdfgiEnabled) return;
        static float Leuchtdichte(Color c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
        var umgebungslicht = Leuchtdichte(umgebung.AmbientLightColor.SrgbToLinear()) * umgebung.AmbientLightEnergy;
        var himmel = Mathf.Max(Leuchtdichte(umgebung.BackgroundColor) * umgebung.BackgroundEnergyMultiplier, 0.01f);
        var energie = Mathf.Clamp(StadtGiAnteil * umgebungslicht / himmel, 0.05f, 4f);
        if (Mathf.Abs(energie - umgebung.SdfgiEnergy) > 0.005f) umgebung.SdfgiEnergy = energie;
    }

    // ── Neu anlegen ─────────────────────────────────────────────────────────

    /// <summary>Zähler über alle Stadtansichten: Jede neue Fassung des Feldes bekommt eine eigene Nummer.</summary>
    private static int _giFolge;

    /// <summary>Unter diesem Schlüssel steht die Nummer der Fassung an der Umgebung (Metadaten, nur für <see cref="GlobaleBeleuchtungAnwenden"/>).</summary>
    private const string GiFolgeSchluessel = "gi_folge";

    /// <summary>
    /// Lässt Godot das Feld neu anlegen. Es tut das nur, wenn sich Kaskaden, Zellgröße, Zellhöhe, Verdeckung oder die Zahl der gemittelten Bilder
    /// ändern – nicht bei einer neuen Umgebung. Das SDFGI hängt am Fenster (Render-Puffer), nicht an der Szene: Ohne das sähe eine neue Partie das
    /// Feld der Greybox aus dem Hauptmenü, bis die Kamera weit genug wandert. Darum ändert sich die kleinste Zelle um ein Hunderttausendstel Meter
    /// (unsichtbar). Gerufen beim Aufbau jeder Stadtansicht und wenn ein Betrieb dazukommt oder geht. Danach flackert das Licht etwa 30 Bilder lang,
    /// bis es sich wieder gesetzt hat.
    /// </summary>
    private void GlobaleBeleuchtungNeuAnlegen()
    {
        _giFolge++;
        _umgebung.Environment.SetMeta(GiFolgeSchluessel, _giFolge);
        _raumUmgebung.SetMeta(GiFolgeSchluessel, _giFolge);
        if (Fenster.AktuelleGlobaleBeleuchtung == GlobaleBeleuchtung.Aus) return;
        GlobaleBeleuchtungAnwenden(_umgebung.Environment, raum: false);
        GlobaleBeleuchtungAnwenden(_raumUmgebung, raum: true);
    }

    /// <summary>Die Tageshelligkeit, mit der das Feld des Raums zuletzt angelegt wurde (−1: noch keins).</summary>
    private float _giRaumHellBeimAnlegen = -1;

    /// <summary>Ab diesem Unterschied der Tageshelligkeit (0 Nacht … 1 Tag) legt der Raum sein Feld neu an: Das Leuchten der Fensterscheiben hat sich dann sichtbar geändert.</summary>
    private const float GiRaumHellSchritt = 0.25f;

    /// <summary>
    /// Nur das Feld der Raumansicht neu anlegen (wie <see cref="GlobaleBeleuchtungNeuAnlegen"/>): Godot trägt leuchtende Flächen nur beim Anlegen ein. Gerufen,
    /// wenn die Glut des gezeigten Hauses an- oder ausgeht und wenn sich das Tageslicht seit dem letzten Anlegen merklich geändert hat (Fensterscheiben);
    /// sonst stünde nachts noch das Tageslicht der Scheiben im Raum oder die Glut glühte im Licht nach, wenn die Arbeit ruht. Kurzes Flackern, wie beim Betreten.
    /// </summary>
    private void GiRaumNeuAnlegen()
    {
        _giRaumHellBeimAnlegen = _raumHell;
        if (!_raumUmgebung.SdfgiEnabled) return;
        _giFolge++;
        _raumUmgebung.SetMeta(GiFolgeSchluessel, _giFolge);
        GlobaleBeleuchtungAnwenden(_raumUmgebung, raum: true);
    }

    // ── Was nicht beiträgt ──────────────────────────────────────────────────

    /// <summary>Einmal beim Aufbau: Partikel und Figuren, die schon da sind, und alle, die später kommen, tragen nicht zum Feld bei.</summary>
    private void GlobaleBeleuchtungEinrichten()
    {
        GlobaleBeleuchtungNeuAnlegen(); // nicht das Feld der vorigen Stadtansicht weiterverwenden
        GetTree().NodeAdded += GiModusSetzen;
        foreach (var teil in FindChildren("*", nameof(GeometryInstance3D), true, false)) GiModusSetzen(teil);
    }

    private void GlobaleBeleuchtungAufgeben() => GetTree().NodeAdded -= GiModusSetzen;

    /// <summary>
    /// Regen, Schnee, Rauch, Funken (Partikel) und Figuren mit Skelett (Passanten, Arbeiter, Vögel) bewegen sich; Godot trüge sie an der Stelle ein,
    /// an der sie gerade sind, wenn eine Kaskade angelegt wird, und dort blieben sie als dunkle Flecken stehen. Mit <c>Disabled</c> empfangen sie das
    /// Licht weiter, tragen aber nichts bei. Ohne SDFGI ändert das nichts (das Spiel nutzt weder VoxelGI noch Lightmaps).
    /// </summary>
    private void GiModusSetzen(Node knoten)
    {
        if (knoten is not GeometryInstance3D teil || !IsAncestorOf(teil)) return;
        var bewegt = teil is GpuParticles3D or CpuParticles3D || teil is MeshInstance3D { Skin: not null } || teil.GetParent() is Skeleton3D;
        if (bewegt && teil.GIMode != GeometryInstance3D.GIModeEnum.Disabled) teil.GIMode = GeometryInstance3D.GIModeEnum.Disabled;
    }
}
