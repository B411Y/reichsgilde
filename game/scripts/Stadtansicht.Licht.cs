using Godot;

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
    private void LichtNachUhrzeit(float uhrzeitInMinuten, double echtzeit)
    {
        var ziel = StimmungUm(uhrzeitInMinuten / 60f);
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

        _sonne.Quaternion = ziel.Richtung;
        _sonne.LightColor = ziel.Farbe;
        _sonne.LightEnergy = ziel.Staerke;
        _sonne.ShadowOpacity = ziel.Schattendeckkraft;
        var umgebung = _umgebung.Environment;
        umgebung.BackgroundColor = ziel.Himmel;
        umgebung.AmbientLightColor = ziel.Umgebung;
        umgebung.AmbientLightEnergy = ziel.Umgebungsstaerke;
        FensterlichtSetzen(ziel.Fenster);      // Stadtansicht.Modelle.cs – fasst die Materialien nur bei merklicher Änderung an
        RaumlichtSetzen(ziel.Himmel, ziel.Hell); // Stadtansicht.Raeume.cs
    }

    /// <summary>
    /// Die Lichtstimmung zur Uhrzeit (<paramref name="stunde"/> mit Bruchteil). Der Spieltag läuft von 05 bis 22 Uhr:
    /// <list type="bullet">
    /// <item>05:00 Morgengrauen im Mondlicht – genauso wie 22:00, denn um 22 Uhr springt die Uhr auf 05 Uhr des nächsten Jahres.</item>
    /// <item>05:00–05:45 der Mond blendet aus, 05:45–06:45 die Sonne ein (Morgenrot), ab etwa 07:30 voller Tag.</item>
    /// <item>ab 18:00 Abendrot, die Sonne sinkt bis 20:00 zum Horizont und blendet dabei aus.</item>
    /// <item>ab 20:05 blendet der Mond ein, ab 21:00 ist richtig Nacht.</item>
    /// </list>
    /// Sonne und Mond sind <b>ein</b> Licht mit einer Schattenkarte (das spart die Hälfte der Schattenarbeit). Es wechselt die
    /// Richtung nur, wenn es gerade ganz ausgeblendet ist – darum kippen die Schatten nie sichtbar.
    /// </summary>
    private static Lichtstimmung StimmungUm(float stunde)
    {
        // Sonne: Aufgang 05:30 im Osten (+X), Untergang 20:00 im Westen. Sichtbar erst ab 05:45, ab 19:00 blendet sie aus.
        var bogen = Mathf.Clamp((stunde - 5.5f) / 14.5f, 0f, 1f);
        var sonnenhoehe = Mathf.Sin(bogen * Mathf.Pi);
        var sonnenSicht = Mathf.SmoothStep(5.75f, 6.75f, stunde) * (1 - Mathf.SmoothStep(19f, 20f, stunde));
        var sonnenStaerke = sonnenSicht * (0.25f + 0.95f * Mathf.Sqrt(sonnenhoehe));

        // Mond: läuft von 20 Uhr bis 05:45 von Osten nach Westen. u zählt die Nachtstunden: 20 Uhr = 0, 22 Uhr = 2 –
        // und 05 Uhr ebenfalls 2, damit der Mond nach dem Sprung über den Jahreswechsel genau dort steht, wo er um 22 Uhr stand.
        var u = stunde >= 12 ? stunde - 20f : stunde - 3f;
        var mondSicht = Mathf.SmoothStep(0.1f, 1f, u) * (1 - Mathf.SmoothStep(2f, 2.75f, u));
        var mondStaerke = 0.5f * mondSicht;

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

        // Fensterlicht: geht ab 18 Uhr nach und nach an und ab 05:30 wieder aus (um 22 und um 05 Uhr also gleich hell)
        var fenster = stunde >= 12 ? Mathf.SmoothStep(18f, 20f, stunde) : 1 - Mathf.SmoothStep(5.5f, 7f, stunde);

        return new Lichtstimmung(richtung, farbe, staerke, schatten, Himmelsfarbe(stunde * 60), umgebung,
                                 0.18f + 0.52f * hell, hell, fenster);
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

    private (float Zoom, float Seiten, Schattenqualitaet Stufe)? _schattenFuer;

    /// <summary>Einmal beim Aufbau: Grundeinstellung des Schattens der Sonne.</summary>
    private void SchattenEinrichten()
    {
        _sonne.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal;
        // Ohne Splits blendet Godot den Schatten zwischen FadeStart · Far und Far aus. Far liegt knapp hinter dem Boden am
        // oberen Bildrand – mit dem Grundwert 0,8 fehlte dort oben der Schatten. 1 heißt: nicht ausblenden.
        _sonne.DirectionalShadowFadeStart = 1f;
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
        var stand = (_kamera.Size, seiten, Fenster.AktuelleSchattenqualitaet);
        if (_schattenFuer == stand) return;
        _schattenFuer = stand;

        // Tiefe einer sichtbaren Stelle (Abstand vor der Kamera): Abstand + v · cot(Neigung) − Höhe / sin(Neigung),
        // v = senkrechte Lage im Bild (±Zoom/2). Der Boden reicht also ±Zoom/2 · cot(Neigung) um den Abstand, die Turmspitze
        // am unteren Bildrand liegt noch HoechsterPunkt / sin(Neigung) näher. Hinten 10 m Luft für Gruben unter dem Boden.
        var neigung = Mathf.DegToRad(KameraNeigung);
        var boden = _kamera.Size / 2 / Mathf.Tan(neigung);
        _kamera.Near = Mathf.Max(0.05f, KameraAbstand - boden - HoechsterPunkt / Mathf.Sin(neigung) - 2);
        _kamera.Far = KameraAbstand + boden + 10;

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
