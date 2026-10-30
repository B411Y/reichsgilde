using Godot;

namespace Reichsgilde;

/// <summary>
/// Globale Beleuchtung mit SDFGI (Versuch vom 30.10.2026, Einstellung „Globale Beleuchtung (SDFGI)“) – ein Teil der <c>partial class Stadtansicht</c>.
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
/// Umgebung mit eigenen Kaskaden; beim Wechsel zwischen Stadt und Raum legt Godot das Feld neu an (ein paar Bilder, bis es sich gesetzt hat).</para>
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
    /// Mittel 0,2 m (drei, bis ±51 m), Hoch 0,1 m (vier, bis ±51 m) mit Verdeckung gegen Lichtlecks. Zellhöhe 75 % (Godots Grundwert).
    /// </summary>
    internal static SdfgiKaskaden RaumKaskaden(GlobaleBeleuchtung stufe) => stufe switch
    {
        GlobaleBeleuchtung.Niedrig => new(2, 0.4f, Godot.Environment.SdfgiyScale.Scale75Percent, false),
        GlobaleBeleuchtung.Mittel => new(3, 0.2f, Godot.Environment.SdfgiyScale.Scale75Percent, false),
        _ => new(4, 0.1f, Godot.Environment.SdfgiyScale.Scale75Percent, true),
    };

    /// <summary>
    /// Was für alle Umgebungen gilt (RenderingServer): Strahlen je Sonde, über wie viele Bilder gemittelt wird, auf wie viele Bilder das Licht
    /// verteilt wird und ob das Ergebnis auf halber Auflösung gerechnet wird. Godots Grundwerte: 8 Strahlen, 30 Bilder, Licht in 4 Bildern, volle Auflösung.
    /// </summary>
    internal static void GlobaleBeleuchtungGueteSetzen(GlobaleBeleuchtung stufe)
    {
        if (stufe == GlobaleBeleuchtung.Aus || !Fenster.GlobaleBeleuchtungMoeglich) return; // die Werte zählen nur, solange SDFGI an ist
        var (strahlen, licht, halb) = stufe switch
        {
            GlobaleBeleuchtung.Niedrig => (RenderingServer.EnvironmentSdfgiRayCount.Count8, RenderingServer.EnvironmentSdfgiFramesToUpdateLight.In8Frames, true),
            GlobaleBeleuchtung.Mittel => (RenderingServer.EnvironmentSdfgiRayCount.Count16, RenderingServer.EnvironmentSdfgiFramesToUpdateLight.In4Frames, true),
            _ => (RenderingServer.EnvironmentSdfgiRayCount.Count32, RenderingServer.EnvironmentSdfgiFramesToUpdateLight.In2Frames, false),
        };
        RenderingServer.EnvironmentSetSdfgiRayCount(strahlen);
        RenderingServer.EnvironmentSetSdfgiFramesToConverge(RenderingServer.EnvironmentSdfgiFramesToConverge.In30Frames);
        RenderingServer.EnvironmentSetSdfgiFramesToUpdateLight(licht);
        RenderingServer.GISetUseHalfResolution(halb);
    }

    /// <summary>
    /// Wie hell das Licht der Raumansicht zurückkommt. Der Raum liest keinen Himmel (sein Hintergrund ist fast schwarz, Godot rät bei Innenräumen ab):
    /// Ohne Umgebungslicht bliebe nur, was Fenster, Glut und Lampen zurückwerfen – dafür etwas kräftiger als Godots Grundwert 1.
    /// </summary>
    private const float RaumGiEnergie = 3f;

    /// <summary>
    /// Wie stark das Tageslicht der Fenster (<c>SpotLight3D</c> der Modellräume) in SDFGI zurückkommt (<see cref="Light3D.LightIndirectEnergy"/>, wirkt nur
    /// auf SDFGI). Ohne SDFGI füllt das Umgebungslicht den Raum am Tag; mit SDFGI muss das Fenster es tun.
    /// </summary>
    internal const float FensterGiVerstaerkung = 6f;

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
        var an = stufe != GlobaleBeleuchtung.Aus && Fenster.GlobaleBeleuchtungMoeglich;
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
        if (raum) umgebung.SdfgiEnergy = RaumGiEnergie;
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
