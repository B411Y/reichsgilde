using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Licht und Schatten der Stadt – ein Teil der <c>partial class Stadtansicht</c>.
///
/// Tag und Nacht: Jede Uhrzeit hat eine <see cref="Lichtstimmung"/> (Richtung, Farbe und Stärke des Lichts, Himmel,
/// Umgebungslicht, Fensterlicht). Alle Werte sind stetige Verläufe der Uhrzeit, darum gleitet der Tag ohne Sprung in die Nacht.
/// Springt die Uhr selbst (Jahreswechsel 22 → 05 Uhr, Laden, Spulen), blendet <see cref="LichtNachUhrzeit"/> über
/// <see cref="UeberblendSekunden"/> Sekunden Echtzeit vom alten zum neuen Bild über.
///
/// Schatten: <see cref="SchattenNachfuehren"/> stellt die Schattenkarte auf die orthografische Kamera ein (eine Karte für
/// das ganze Bild, Tiefenbereich und Bias passend zum Zoom). Die Einstellung „Schattenqualität“ wählt die Stufe
/// (<see cref="Fenster.AktuelleSchattenqualitaet"/>).
/// </summary>
public partial class Stadtansicht
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Tag und Nacht
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Alles, was die Uhrzeit am Licht der Stadt ändert. <see cref="Richtung"/> ist die Drehung des einen gerichteten Lichts
    /// (<see cref="_sonne"/>), das am Tag die Sonne und in der Nacht der Mond ist.
    /// </summary>
    private readonly record struct Lichtstimmung(
        Quaternion Richtung, Color Farbe, float Staerke, float Schattendeckkraft,
        Color Himmel, Color Umgebung, float Umgebungsstaerke, float Hell, float Fenster)
    {
        /// <summary>Weicher Übergang von <paramref name="a"/> nach <paramref name="b"/> (<paramref name="t"/> 0 … 1).</summary>
        public static Lichtstimmung Mischen(Lichtstimmung a, Lichtstimmung b, float t) => new(
            a.Richtung.Slerp(b.Richtung, t), a.Farbe.Lerp(b.Farbe, t), Mathf.Lerp(a.Staerke, b.Staerke, t),
            Mathf.Lerp(a.Schattendeckkraft, b.Schattendeckkraft, t), a.Himmel.Lerp(b.Himmel, t), a.Umgebung.Lerp(b.Umgebung, t),
            Mathf.Lerp(a.Umgebungsstaerke, b.Umgebungsstaerke, t), Mathf.Lerp(a.Hell, b.Hell, t), Mathf.Lerp(a.Fenster, b.Fenster, t));
    }

    /// <summary>So lange (Sekunden Echtzeit) dauert die Überblendung, wenn die Uhr springt.</summary>
    private const float UeberblendSekunden = 1.5f;

    /// <summary>
    /// Ab diesem Sprung der Uhr (Spielminuten in einem Bild) wird übergeblendet. Selbst bei Tempo 32 und nur 2 Bildern
    /// in der Sekunde rückt die Uhr je Bild nur etwa 16 Minuten vor – 45 Minuten erreicht nur ein echter Sprung.
    /// </summary>
    private const float SprungMinuten = 45f;

    /// <summary>Der gemeinsame Dämmerungswert der gezeigten Stimmung (0 = Tag, 1 = Nacht), aus dem alle Lichter der Stadt ihren Beginn nehmen.</summary>
    private float _daemmerung;

    private Lichtstimmung? _gezeigteStimmung;
    private Lichtstimmung _ueberblendenVon;
    private float _ueberblendung = 1f;
    private float _letzteLichtUhrzeit = -1f;

    /// <summary>
    /// Sonne bei Tag, Mond bei Nacht – beide werfen Schatten.
    ///
    /// Wann ist ein Schatten sichtbar? Ein Schatten ist die Stelle, an der nur noch das
    /// Umgebungslicht ankommt. Sichtbar wird er erst, wenn das gerichtete Licht (Sonne oder Mond)
    /// deutlich heller ist als das Umgebungslicht. Deshalb ist die Nacht hier nicht einfach „alles
    /// dunkler", sondern: wenig blaues Umgebungslicht + ein klarer, kühler Mond.
    ///
    /// <paramref name="echtzeit"/> ist die vergangene Echtzeit seit dem letzten Bild (für die Überblendung bei Sprüngen).
    /// </summary>
    private void LichtNachUhrzeit(float uhrzeitInMinuten, double echtzeit, Jahreszeit jahreszeit = Jahreszeit.Fruehling)
    {
        var ziel = StimmungUm(uhrzeitInMinuten / 60f, jahreszeit);
        if (_letzteLichtUhrzeit >= 0 && Mathf.Abs(uhrzeitInMinuten - _letzteLichtUhrzeit) > SprungMinuten && _gezeigteStimmung is { } alt)
        {
            _ueberblendenVon = alt;
            _ueberblendung = 0;
        }
        _letzteLichtUhrzeit = uhrzeitInMinuten;
        if (_ueberblendung < 1)
        {
            _ueberblendung = Mathf.Min(1, _ueberblendung + (float)echtzeit / UeberblendSekunden);
            ziel = Lichtstimmung.Mischen(_ueberblendenVon, ziel, Mathf.SmoothStep(0, 1, _ueberblendung));
        }
        _gezeigteStimmung = ziel;
        ziel = MitWetter(ziel); // Wolken, Nebel und Blitz (Stadtansicht.Wetter.cs) – nach dem Merken: Das Wetter blendet selbst über

        _sonne.Quaternion = ziel.Richtung;
        _sonne.LightColor = ziel.Farbe;
        _sonne.LightEnergy = ziel.Staerke;
        _sonne.ShadowOpacity = ziel.Schattendeckkraft;
        var umgebung = _umgebung.Environment;
        umgebung.BackgroundColor = ziel.Himmel;
        umgebung.AmbientLightColor = ziel.Umgebung;
        umgebung.AmbientLightEnergy = ziel.Umgebungsstaerke;
        _daemmerung = ziel.Fenster;
        FensterlichtSetzen(ziel.Fenster, uhrzeitInMinuten / 60f); // Stadtansicht.Modelle.cs – fasst die Materialien nur bei merklicher Änderung an
        RaumlichtSetzen(ziel.Himmel, ziel.Hell); // Stadtansicht.Raeume.cs
    }

    /// <summary>
    /// Die Lichtstimmung zur Uhrzeit (<paramref name="stunde"/> mit Bruchteil). Der Spieltag läuft von 05 bis 22 Uhr:
    /// <list type="bullet">
    /// <item>05:00 Morgengrauen im Mondlicht – genauso wie 22:00, denn um 22 Uhr springt die Uhr auf 05 Uhr des nächsten Jahres.</item>
    /// <item>05:00–05:27 der Mond blendet aus, 05:30–06:30 die Sonne ein (Morgenrot), ab etwa 07:30 voller Tag.</item>
    /// <item>ab 18:00 Abendrot, die Sonne sinkt zum Horizont und blendet von 19:00 bis 19:54 aus.</item>
    /// <item>ab 19:57 blendet der Mond ein (voll um 20:45), ab 21:00 ist richtig Nacht.</item>
    /// </list>
    /// Sonne und Mond sind <b>ein</b> Licht mit einer Schattenkarte (das spart die Hälfte der Schattenarbeit). Es wechselt die
    /// Richtung nur, wenn es gerade ganz ausgeblendet ist – darum kippen die Schatten nie sichtbar.
    /// </summary>
    private static Lichtstimmung StimmungUm(float uhrStunde, Jahreszeit jahreszeit)
    {
        var stunde = BezugsStunde(uhrStunde, jahreszeit); // alle Kurven unten gelten für den Bezugstag (siehe BezugsStunde)
        // Sonne: Aufgang 05:30 im Osten (+X), Untergang 20:00 im Westen. Sie blendet nach dem Aufgang ein und kurz vor dem
        // Untergang aus – so steht sie nie mit voller Kraft auf dem Horizont (dort wären alle Schatten endlos lang).
        var bogen = Mathf.Clamp((stunde - 5.5f) / 14.5f, 0f, 1f);
        var sonnenhoehe = Mathf.Sin(bogen * Mathf.Pi);
        var sonnenSicht = Mathf.SmoothStep(5.5f, 6.5f, stunde) * (1 - Mathf.SmoothStep(19f, 19.9f, stunde));
        var sonnenStaerke = sonnenSicht * (0.25f + 0.95f * Mathf.Sqrt(sonnenhoehe));

        // Mond: läuft von 20 Uhr bis 05:45 von Osten nach Westen. u zählt die Nachtstunden: 20 Uhr = 0, 22 Uhr = 2 –
        // und 05 Uhr ebenfalls 2, damit der Mond nach dem Sprung über den Jahreswechsel genau dort steht, wo er um 22 Uhr stand.
        // Er blendet erst ein, wenn die Sonne ganz aus ist (19:57), und morgens aus, bevor sie kommt (05:27).
        var u = stunde >= 12 ? stunde - 20f : stunde - 3f;
        var mondSicht = Mathf.SmoothStep(-0.05f, 0.75f, u) * (1 - Mathf.SmoothStep(2f, 2.45f, u));
        var mondStaerke = 0.35f * mondSicht;

        Quaternion richtung;
        Color farbe;
        float staerke, schatten;
        if (sonnenStaerke >= mondStaerke)
        {
            // Tief stehende Sonne: warm und rot (Morgen- und Abendrot), hoch stehende: fast weiß
            richtung = Drehung(4 + 56 * sonnenhoehe, 90 - bogen * 180);
            farbe = new Color(1f, 0.55f, 0.32f).Lerp(new Color(1f, 0.97f, 0.9f), Mathf.SmoothStep(0.05f, 0.6f, sonnenhoehe));
            staerke = sonnenStaerke;
            schatten = 1f;
        }
        else
        {
            var mondbogen = Mathf.Clamp(u / 2.75f, 0f, 1f);
            richtung = Drehung(18 + 30 * Mathf.Sin(mondbogen * Mathf.Pi), 90 - mondbogen * 180);
            farbe = new Color(0.62f, 0.72f, 1f);
            staerke = mondStaerke;
            schatten = 0.85f; // Mondschatten sind etwas weicher und weniger tief
        }

        // Umgebungslicht: steigt ab 05:00, sinkt ab 18:15 und ist um 21:00 bei Nacht angekommen. Nachts wenig, dafür bläulich –
        // so heben sich Mondschatten und die warme Glut der Essen ab. Morgen- und Abendrot tönen es warm.
        var hell = Mathf.SmoothStep(5f, 7.5f, stunde) * (1 - Mathf.SmoothStep(18.25f, 21f, stunde));
        var rot = Mathf.Max(Mathf.SmoothStep(5.25f, 6f, stunde) * (1 - Mathf.SmoothStep(6.5f, 7.5f, stunde)),
                            Mathf.SmoothStep(17.5f, 18.75f, stunde) * (1 - Mathf.SmoothStep(19.75f, 20.75f, stunde)));
        var umgebung = new Color(0.30f, 0.36f, 0.58f).Lerp(new Color(0.85f, 0.83f, 0.78f), hell)
                                                     .Lerp(new Color(0.95f, 0.66f, 0.52f), 0.35f * rot);
        // Zwischen Sonne und Mond (und morgens umgekehrt) fehlt das gerichtete Licht ganz. Damit die Stadt dort nicht dunkler
        // wird als in der Nacht danach, hellt das Himmelslicht diese Lücke etwas auf (die „blaue Stunde“).
        var luecke = (1 - Mathf.Max(sonnenSicht, mondSicht)) * (1 - hell);

        // Dämmerungswert (0 = Tag, 1 = Nacht), der gemeinsame für alle Lichter der Stadt (Fensterlicht, Nachtlichter, Licht in der Hand,
        // Kirchenfenster zur Messe): geht im Abendrot nach und nach an und morgens wieder aus. Er hängt am Bezugstag, folgt also der Jahreszeit.
        var fenster = stunde >= 12 ? Mathf.SmoothStep(18.5f, 20.5f, stunde) : 1 - Mathf.SmoothStep(5.5f, 7f, stunde);

        return new Lichtstimmung(richtung, farbe, staerke, schatten, Himmelsfarbe(stunde * 60), umgebung,
                                 0.18f + 0.52f * hell + 0.2f * luecke, hell, fenster);
    }

    /// <summary>
    /// Wann es hell und wann es dunkel wird, je Jahreszeit (Uhrzeit mit Bruchteil): Der Himmel ist ab <c>Hell</c> voller Tag und ab <c>Dunkel</c> richtig
    /// Nacht. Winter hell gegen 7:30 und dunkel gegen 16:30, Sommer 4:30 und 21:30, Frühling und Herbst dazwischen (6:00 und 19:00).
    /// </summary>
    private static (float Hell, float Dunkel) Sonnenzeiten(Jahreszeit jahreszeit) => jahreszeit switch
    {
        Jahreszeit.Winter => (7.5f, 16.5f),
        Jahreszeit.Sommer => (4.5f, 21.5f),
        _ => (6f, 19f),
    };

    /// <summary>
    /// Rechnet die Uhrzeit auf den <b>Bezugstag</b> um, für den <see cref="StimmungUm"/> seine Kurven kennt (Sonne 5:30–6:30 auf, 19:00–19:54 unter,
    /// richtig Nacht ab 21:00, um 5 und um 22 Uhr dieselbe Nacht). Aus der Jahreszeit folgen zwei Zeiten (<see cref="Sonnenzeiten"/>): Morgens ist der
    /// Bezugstag um 7:30 voll hell und 2,5 Stunden früher Nacht, abends ist er bei 21:00 ganz dunkel; die Dämmerung läuft in 2 echten Stunden durch
    /// (18 bis 21 Uhr des Bezugstags). Dazwischen vergeht der Bezugstag gleichmäßig. Vor dem Morgengrauen und nach dem Dunkelwerden bleibt er in der
    /// Nacht (5 bzw. bis 22 Uhr). Der Spieltag selbst (Beginn und Ende, <see cref="Spielzeit.TagesbeginnMinute"/>) kommt hier nicht vor: Er darf
    /// beliebig früh beginnen und spät enden, die Nacht davor und danach ist immer dieselbe.
    /// </summary>
    private static float BezugsStunde(float stunde, Jahreszeit jahreszeit)
    {
        var (hell, dunkel) = Sonnenzeiten(jahreszeit);
        var abendBeginn = dunkel - 2f;
        if (stunde <= hell) return Mathf.Max(5f, 7.5f - 1.5f * (hell - stunde));
        if (stunde < abendBeginn) return 7.5f + (stunde - hell) * (18f - 7.5f) / (abendBeginn - hell);
        if (stunde < dunkel) return 18f + 1.5f * (stunde - abendBeginn);
        return Mathf.Min(22f, 21f + (stunde - dunkel));
    }

    /// <summary>Drehung des gerichteten Lichts: <paramref name="hoehe"/> Grad über dem Horizont, aus der Himmelsrichtung <paramref name="richtung"/> (90 = Osten).</summary>
    private static Quaternion Drehung(float hoehe, float richtung) =>
        Quaternion.FromEuler(new Vector3(Mathf.DegToRad(-hoehe), Mathf.DegToRad(richtung), 0));

    // Stützpunkte für den Himmel: (Uhrzeit in Minuten, Farbe). 05:00 und 22:00 sind dasselbe Nachtblau (Sprung über den Jahreswechsel).
    private static readonly (int Minute, Color Farbe)[] HimmelsVerlauf =
    {
        (5 * 60,       new Color(0.10f, 0.10f, 0.18f)), // Morgengrauen = Nacht von 22 Uhr
        (5 * 60 + 45,  new Color(0.36f, 0.28f, 0.34f)),
        (6 * 60 + 30,  new Color(0.68f, 0.50f, 0.40f)), // Morgenrot
        (8 * 60,       new Color(0.52f, 0.66f, 0.78f)),
        (14 * 60,      new Color(0.55f, 0.69f, 0.82f)),
        (17 * 60,      new Color(0.57f, 0.65f, 0.75f)),
        (18 * 60 + 30, new Color(0.72f, 0.50f, 0.34f)), // Abendrot
        (19 * 60 + 30, new Color(0.56f, 0.32f, 0.26f)),
        (20 * 60 + 15, new Color(0.24f, 0.17f, 0.26f)), // Dämmerung
        (21 * 60,      new Color(0.10f, 0.10f, 0.18f)), // Nacht
    };

    /// <summary>Himmelsfarbe zur Uhrzeit; vor dem ersten und nach dem letzten Stützpunkt bleibt sie bei Nachtblau.</summary>
    public static Color Himmelsfarbe(float uhrzeitInMinuten)
    {
        if (uhrzeitInMinuten <= HimmelsVerlauf[0].Minute) return HimmelsVerlauf[0].Farbe;
        for (var i = 0; i < HimmelsVerlauf.Length - 1; i++)
        {
            var (von, farbeVon) = HimmelsVerlauf[i];
            var (bis, farbeBis) = HimmelsVerlauf[i + 1];
            if (uhrzeitInMinuten < bis)
                return farbeVon.Lerp(farbeBis, (uhrzeitInMinuten - von) / (bis - von));
        }
        return HimmelsVerlauf[^1].Farbe;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Schatten
    // ═════════════════════════════════════════════════════════════════════════
    //
    // Wie Godot die Schattenkarte für ein gerichtetes Licht baut (Quelle: Godot-Quelltext, renderer_scene_cull.cpp und
    // light_storage.cpp): Es nimmt den Ausschnitt der Kamera zwischen Near und Far, legt eine Kugel darum und rendert alles
    // darin aus Sicht des Lichts in die Schattenkarte. Bei einer orthografischen Kamera gilt „Max Distance“ nicht, der
    // Ausschnitt reicht immer von Near bis Far. Daraus folgt:
    //  • Near und Far eng um die Stadt legen – sonst verteilt sich die Karte auf viele Meter leere Luft.
    //  • Eine Karte statt vier „Splits“: Die Splits teilen nach Abstand zur Kamera, bei einer orthografischen Kamera
    //    ist aber jeder Split gleich breit – vier Splits bringen nur viermal Arbeit und je ein Viertel der Auflösung.
    //  • Der Bias wächst in Godot mit der Tiefe des Ausschnitts (mal dem Weichzeichner). Bei 400 m Far und Weichzeichner 1,5
    //    rutschte der Schatten so um gut einen Meter vom Fuß weg („Peter Panning“: Figuren und Fahnen schwebten).
    //    Hier wird er darum in Metern vorgegeben und für jeden Zoom zurückgerechnet.

    /// <summary>Abstand der Kamera vom Drehpunkt am Boden (Meter) und ihre Neigung (Grad) – siehe <see cref="KameraAufbauen"/>.</summary>
    private const float KameraAbstand = 150f, KameraNeigung = 40f;

    /// <summary>Höchster Punkt der Stadt: Domturm 42,6 m mit Kreuz, dazu Luft. Er muss vor Near liegen, sonst wird er abgeschnitten.</summary>
    private const float HoechsterPunkt = 48f;

    /// <summary>
    /// Tiefen-Bias in Metern: So weit muss ein Schattenwerfer näher an der Sonne liegen als die beschattete Stelle.
    /// 2 cm reichen gegen die Rechenungenauigkeit der 16-Bit-Schattenkarte (ein Schritt ≈ 5 mm bei 300 m Tiefe), und eine Lücke
    /// von höchstens 2 cm am Fuß sieht man nicht. Gegen Schattenakne auf schräg beschienenen Flächen hilft der Normal-Bias.
    /// </summary>
    private const float SchattenBiasMeter = 0.02f;

    /// <summary>Die Werte einer Stufe der Einstellung „Schattenqualität“.</summary>
    /// <param name="Karte">Kantenlänge der Schattenkarte in Pixeln.</param>
    /// <param name="Filter">Godots weicher Filter: wie viele Proben je Bildpunkt und wie weit (Radius in Texeln).</param>
    /// <param name="FilterRadius">Radius dieses Filters in Texeln (aus Godots Quelltext), für die Bias-Rechnung.</param>
    /// <param name="Weich">Weichzeichner („Blur“) – der Filterradius wird damit malgenommen.</param>
    internal readonly record struct SchattenStufe(int Karte, RenderingServer.ShadowQuality Filter, float FilterRadius, float Weich);

    /// <summary>
    /// Niedrig, Mittel, Hoch. Bei Zoom 95 entspricht ein Texel der Karte etwa 13 / 7 / 3,3 cm. Der Filter macht die Kante
    /// etwa 1,5–2 Texel weich: genug gegen Treppen, scharf genug, dass der Schatten am Fuß klar ansetzt.
    /// Die Filterradien stehen in Godots renderer_scene_render_rd.cpp (Soft Low/Medium 2, Soft High 3).
    /// </summary>
    internal static SchattenStufe SchattenStufeFuer(Schattenqualitaet q) => q switch
    {
        Schattenqualitaet.Niedrig => new(2048, RenderingServer.ShadowQuality.SoftLow, 2f, 0.75f),   // 4 Proben
        Schattenqualitaet.Mittel => new(4096, RenderingServer.ShadowQuality.SoftMedium, 2f, 0.75f), // 8 Proben
        _ => new(8192, RenderingServer.ShadowQuality.SoftHigh, 3f, 0.67f),                          // 16 Proben
    };

    private (float Zoom, float Seiten, Schattenqualitaet Stufe, bool Gelaende)? _schattenFuer;

    /// <summary>Auf Gelände liegen Hügel und Täler vor und hinter dem Blickpunkt: So viele Meter mehr Tiefe haben Near und Far dann (die Kamera sitzt auf der Höhe des Blickpunkts).</summary>
    private const float GelaendeTiefe = 45f;

    // ═════════════════════════════════════════════════════════════════════════
    //  Bloom
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Godots Glow trägt nur Bildpunkte weiter, deren hellster Farbkanal über dem Schwellwert liegt. Das Bild wird in HDR
    /// gerechnet (Werte über 1 sind möglich): Sonnenbeschienene Dächer und Wände liegen darunter und schimmern nie (gemessen:
    /// Markt um 13 Uhr bei 100 % gegen Aus, Unterschiede nur an leuchtenden Fenstern). Lichtquellen liegen darüber: Fensterlicht
    /// mit Emission bis 2,6 (<see cref="FensterLichtStaerke"/>), Glut, Feuerkorb und Domfenster – nur sie leuchten nach.
    /// 1,0 ist Godots Grundwert; <see cref="BloomUebergang"/> blendet den Einsatz weich ein, statt eine harte Kante zu ziehen.
    /// </summary>
    private const float BloomSchwelle = 1f, BloomUebergang = 1f;

    /// <summary>
    /// Stärke des Scheins (<c>GlowIntensity</c>) bei 100 %: kräftig, aber nicht grell. Die Stärke in Prozent geht mit
    /// Exponent <see cref="BloomKurve"/> ein, so liegt der Regler unten fein abgestuft: 50 % ergibt etwa 0,75 (dezent, das
    /// empfohlene Bild), 25 % etwa 0,2. 0 % schaltet den Glow ganz ab, das Bild ist dann wie mit „Aus“.
    /// </summary>
    private const float BloomMaximum = 2.6f, BloomKurve = 1.8f;

    /// <summary>
    /// Wie weit der Schein reicht: Die Stufen des Glow sind immer breitere Weichzeichnungen des hellen Bildes
    /// (Stufe 1 klein, Stufe 5 sehr breit). Wir nehmen die mittleren: ein Hof um Fenster und Glut, aber kein Nebel über der Stadt.
    /// </summary>
    private static readonly float[] BloomStufen = { 0f, 0.5f, 1f, 1f, 0.6f, 0f, 0f };

    /// <summary>
    /// Stellt den Bloom einer Umgebung (Stadt oder Raumansicht) nach der Einstellung ein (<see cref="Fenster.BloomAn"/>,
    /// <see cref="Fenster.BloomStaerke"/>). „Screen“ als Mischung hellt nur auf und überstrahlt nie über Weiß.
    /// </summary>
    internal static void BloomAnwenden(Godot.Environment umgebung)
    {
        var anteil = Fenster.BloomStaerke / 100f;
        umgebung.GlowEnabled = Fenster.BloomAn && Fenster.BloomStaerke > 0;
        umgebung.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen;
        umgebung.GlowHdrThreshold = BloomSchwelle;
        umgebung.GlowHdrScale = BloomUebergang;
        umgebung.GlowHdrLuminanceCap = 12f; // sehr helle Quellen (Domfenster) strahlen nicht unbegrenzt weiter
        umgebung.GlowBloom = 0f;            // sonst würden auch dunkle Flächen leuchten
        umgebung.GlowStrength = 1f;
        umgebung.GlowIntensity = BloomMaximum * Mathf.Pow(anteil, BloomKurve);
        for (var i = 0; i < BloomStufen.Length; i++) umgebung.SetGlowLevel(i, BloomStufen[i]);
    }

    /// <summary>Stadt und Raumansicht übernehmen die Einstellung sofort, wenn sie sich ändert.</summary>
    private void BloomNachfuehren()
    {
        BloomAnwenden(_umgebung.Environment);
        BloomAnwenden(_raumUmgebung);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Umgebungsverdeckung (SSAO) und indirektes Licht (SSIL)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wie genau Godot SSAO und SSIL rechnet (<see cref="Fenster.UmgebungsverdeckungSetzen"/> reicht es an den RenderingServer).</summary>
    internal readonly record struct VerdeckungGuete(RenderingServer.EnvironmentSsaoQuality Qualitaet, bool HalbeGroesse, float Anpassung, int Weichzeichner);

    /// <summary>
    /// Güte je Stufe: Niedrig = wenige Proben auf halber Auflösung, Mittel = Godots Grundwerte (mittlere Proben, halbe
    /// Auflösung), Hoch = viele Proben in voller Auflösung (feinere Fugen, kostet deutlich mehr). Bei „Aus“ zählt die Güte nur
    /// für das indirekte Licht, wenn es allein läuft.
    /// </summary>
    internal static VerdeckungGuete VerdeckungGueteFuer(Umgebungsverdeckung stufe) => stufe switch
    {
        Umgebungsverdeckung.Hoch => new(RenderingServer.EnvironmentSsaoQuality.High, false, 0.5f, 2),
        Umgebungsverdeckung.Mittel => new(RenderingServer.EnvironmentSsaoQuality.Medium, true, 0.5f, 2),
        _ => new(RenderingServer.EnvironmentSsaoQuality.Low, true, 0.5f, 1),
    };

    /// <summary>
    /// Reichweite (Meter), Stärke, Kontrast und Einfluss aufs direkte Licht der Umgebungsverdeckung. Dezent: dunkler werden
    /// nur Ecken, Fußpunkte und Fugen, nicht ganze Flächen. Die Kamera ist orthografisch; Godot rechnet die Reichweite in Metern
    /// der Welt, sie gilt also bei jedem Zoom gleich (gemessen bei Zoom 25, 45 und 95). Der Einfluss aufs direkte Licht
    /// (<c>SsaoLightAffect</c>) ist nötig: Mit Godots Grundwert 0 dunkelt SSAO nur das Umgebungslicht, und das ist in der
    /// Sonne ein kleiner Teil des Bildes – der Unterschied war dann kaum zu sehen (im Mittel unter 1 von 255).
    /// </summary>
    private const float VerdeckungReichweite = 2f, VerdeckungStaerke = 6f, VerdeckungKontrast = 1.2f, VerdeckungDirektesLicht = 0.7f;

    /// <summary>
    /// Dieselben Werte für die Raumansicht: Dort steht eine perspektivische Kamera nah an kleinen Räumen, 2 m Reichweite
    /// würden ganze Wände und Böden abdunkeln (geprüft in der Schmiede). Darum kleiner und schwächer: Nur Ecken, Möbelfüße
    /// und die Kante zwischen Wand und Boden werden dunkler.
    /// </summary>
    private const float RaumVerdeckungReichweite = 0.7f, RaumVerdeckungStaerke = 2.5f, RaumVerdeckungDirektesLicht = 0.5f;

    /// <summary>Reichweite (Meter) und Stärke des indirekten Lichts; es soll nur ein wenig Farbe von nahen Flächen zurückgeben, nichts verschmieren.</summary>
    private const float IndirektReichweite = 4f, IndirektStaerke = 2f;

    /// <summary>Stellt SSAO und SSIL einer Umgebung (Stadt oder Raumansicht) nach der Einstellung ein.</summary>
    internal static void VerdeckungAnwenden(Godot.Environment umgebung, bool raum = false)
    {
        umgebung.SsaoEnabled = Fenster.AktuelleUmgebungsverdeckung != Umgebungsverdeckung.Aus;
        umgebung.SsaoRadius = raum ? RaumVerdeckungReichweite : VerdeckungReichweite;
        umgebung.SsaoIntensity = raum ? RaumVerdeckungStaerke : VerdeckungStaerke;
        umgebung.SsaoPower = VerdeckungKontrast;
        umgebung.SsaoLightAffect = raum ? RaumVerdeckungDirektesLicht : VerdeckungDirektesLicht;
        umgebung.SsilEnabled = Fenster.IndirektesLichtAn;
        umgebung.SsilRadius = IndirektReichweite;
        umgebung.SsilIntensity = IndirektStaerke;
    }

    /// <summary>Stadt und Raumansicht übernehmen die Einstellung sofort, wenn sie sich ändert.</summary>
    private void VerdeckungNachfuehren()
    {
        VerdeckungAnwenden(_umgebung.Environment);
        VerdeckungAnwenden(_raumUmgebung, raum: true);
    }

    /// <summary>Einmal beim Aufbau: Grundeinstellung des Schattens der Sonne.</summary>
    private void SchattenEinrichten()
    {
        _sonne.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal;
        // Ohne Splits blendet Godot den Schatten zwischen FadeStart · Far und Far aus. Far liegt knapp hinter dem Boden am
        // oberen Bildrand – mit dem Grundwert 0,8 fehlte dort oben der Schatten. 1 heißt: nicht ausblenden.
        _sonne.DirectionalShadowFadeStart = 1f;
        // Godot legt beim Import vereinfachte Fassungen jedes Modells an (LOD) und wählt sie auch für den Schatten – dort aus
        // Sicht der Sonne, deren Karte die ganze Stadt fasst, also sehr grob. Dann warfen Spitzdächer, Zinnen und Balken keinen
        // Schatten. Die Modelle sind schlicht genug, um immer ganz gezeichnet zu werden (kostet unter lavapipe 7–9 % Bildzeit).
        GetViewport().MeshLodThreshold = 0;
        SchattenNachfuehren();
    }

    /// <summary>
    /// Jedes Bild: Haben sich Zoom, Seitenverhältnis des Fensters oder die Schattenqualität geändert, werden Near/Far der
    /// Kamera und Bias/Weichzeichner der Sonne neu berechnet (sonst nichts – der Vergleich kostet fast nichts).
    /// </summary>
    private void SchattenNachfuehren()
    {
        var bild = GetViewport().GetVisibleRect().Size;
        var seiten = bild.Y > 0 ? bild.X / bild.Y : 16f / 9f;
        var stand = (_kamera.Size, seiten, Fenster.AktuelleSchattenqualitaet, BodenBewegt);
        if (_schattenFuer == stand) return;
        _schattenFuer = stand;

        // Tiefe einer sichtbaren Stelle (Abstand vor der Kamera): Abstand + v · cot(Neigung) − Höhe / sin(Neigung),
        // v = senkrechte Lage im Bild (±Zoom/2). Der Boden reicht also ±Zoom/2 · cot(Neigung) um den Abstand, die Turmspitze
        // am unteren Bildrand liegt noch HoechsterPunkt / sin(Neigung) näher. Hinten 10 m Luft für Gruben unter dem Boden.
        var neigung = Mathf.DegToRad(KameraNeigung);
        var boden = _kamera.Size / 2 / Mathf.Tan(neigung);
        var gelaende = BodenBewegt ? GelaendeTiefe : 0f;
        _kamera.Near = Mathf.Max(0.05f, KameraAbstand - boden - (HoechsterPunkt + gelaende) / Mathf.Sin(neigung) - 2);
        _kamera.Far = KameraAbstand + boden + 10 + gelaende / Mathf.Sin(neigung);

        // Den Bias rechnet Godot so: Bias / 100 · (Tiefe der Schattenkarte) · max(Weichzeichner · Filterradius, 1).
        // Die Tiefe der Karte ist 2 · Radius der Kugel um den Kameraausschnitt + Pancake (20 m). Umgekehrt ergibt das den
        // Wert, der genau SchattenBiasMeter Meter verschiebt – bei jedem Zoom gleich.
        var stufe = SchattenStufeFuer(Fenster.AktuelleSchattenqualitaet);
        var breite = _kamera.Size * seiten;
        var radius = 0.5f * new Vector3(breite, _kamera.Size, _kamera.Far - _kamera.Near).Length() * stufe.Karte / (stufe.Karte - 2f);
        var kartentiefe = 2 * radius + _sonne.DirectionalShadowPancakeSize;
        var filter = stufe.FilterRadius * stufe.Weich; // Radius des weichen Filters in Texeln
        _sonne.ShadowBlur = stufe.Weich;
        _sonne.ShadowBias = SchattenBiasMeter * 100f / (kartentiefe * Mathf.Max(filter, 1f));
        // Normal-Bias in Texeln (Godot rechnet ihn selbst in Meter um): etwas größer als der Filterradius. Er schiebt die
        // Abfrage seitlich vom Boden weg – das verhindert Akne auch bei flach stehender Sonne, ohne den Schatten vom Fuß zu lösen.
        _sonne.ShadowNormalBias = filter + 1f;
    }
}
