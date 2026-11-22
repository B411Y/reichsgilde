using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// <b>Farbabstimmung nach Tages- und Jahreszeit</b> (Grafik-Paket Teil 3, Bildstimmung): eine dezente Farb-LUT, die Godots Tonemapping-Schritt
/// zuletzt auf das fertige Bild der <b>Stadt</b> legt (<c>Environment.AdjustmentColorCorrection</c>; Räume und Tafeln bleiben ohne).
/// Sie ist kein Farbfilter im Sinne des Pixel-Looks, sondern eine Stimmung: Morgen und Abend einen Hauch wärmer, die Nacht einen Hauch kühler,
/// im Winter etwas kälter, im Sommer und Herbst etwas wärmer. Mittags im Frühling ist sie die Identität – dort bleibt das Bild, wie es war.
/// <para>
/// Es gibt nicht <i>eine</i> feste LUT, sondern einen stetigen Verlauf: Aus Bezugsstunde (siehe <see cref="BezugsStunde"/>, folgt den Sonnenzeiten
/// der Jahreszeit) und Jahreszeit entsteht ein <see cref="Farbton"/> (Verstärkung je Farbkanal, Tönung der Schatten, Sättigung); der Farbton, der gerade
/// gilt, zieht dem Ziel mit einer kurzen Überblendung nach (Sprünge der Uhr, Wechsel der Jahreszeit). Daraus wird eine kleine 3D-LUT (32³, 32768 Punkte)
/// im Speicher gerechnet und nur dann neu hochgeladen, wenn sich der Farbton merklich geändert hat. Es gibt keine Bilddateien dazu.
/// </para>
/// Abschaltbar: <see cref="Fenster.FarbabstimmungAn"/> (Einstellungen, Grafik, Erweitert), <c>--farbabstimmung=0|1</c>.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Kantenlänge der LUT: 32 Stützpunkte je Farbkanal; die Verläufe sind glatt, Godot mischt dazwischen.</summary>
    private const int LutGroesse = 32;

    /// <summary>Die Stimmung als Zahlen: Verstärkung je Kanal (1 = unverändert), Schattentönung (zusätzlich, nur in dunklen Stellen) und Sättigung.</summary>
    private readonly record struct Farbton(Vector3 Gain, Vector3 Schatten, float Saettigung)
    {
        public static readonly Farbton Identitaet = new(Vector3.One, Vector3.Zero, 1f);

        public static Farbton Mischen(Farbton a, Farbton b, float t) =>
            new(a.Gain.Lerp(b.Gain, t), a.Schatten.Lerp(b.Schatten, t), Mathf.Lerp(a.Saettigung, b.Saettigung, t));

        /// <summary>Beide Stimmungen übereinander (Tageszeit mal Jahreszeit).</summary>
        public static Farbton Zusammen(Farbton a, Farbton b) =>
            new(a.Gain * b.Gain, a.Schatten + b.Schatten, a.Saettigung * b.Saettigung);

        public float Abstand(Farbton o) => Mathf.Max(Mathf.Abs(Gain.X - o.Gain.X), Mathf.Max(Mathf.Abs(Gain.Y - o.Gain.Y), Mathf.Max(Mathf.Abs(Gain.Z - o.Gain.Z),
            Mathf.Max(Mathf.Abs(Schatten.X - o.Schatten.X), Mathf.Max(Mathf.Abs(Schatten.Y - o.Schatten.Y), Mathf.Max(Mathf.Abs(Schatten.Z - o.Schatten.Z), Mathf.Abs(Saettigung - o.Saettigung)))))));
    }

    // Tageszeiten auf dem Bezugstag (5 bis 22 Uhr, wie Lichtstimmung): Nacht, Morgenrot, Tag, Abendrot, Nacht. Alles höchstens wenige Prozent.
    private static readonly Farbton TonNacht = new(new Vector3(0.95f, 0.98f, 1.05f), new Vector3(-0.004f, 0f, 0.010f), 0.96f);
    private static readonly Farbton TonMorgen = new(new Vector3(1.04f, 1.00f, 0.95f), new Vector3(0f, 0f, 0.004f), 1.03f);
    private static readonly Farbton TonAbend = new(new Vector3(1.06f, 0.98f, 0.91f), new Vector3(0.006f, 0f, 0.008f), 1.04f);

    /// <summary>Der Farbton der Tageszeit zur Bezugsstunde: stetig Nacht (5) – Morgen (6,5) – Tag (8…17) – Abend (18,75) – Nacht (20,5…22).</summary>
    private static Farbton TonTageszeit(float stunde)
    {
        static float Glatt(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp((x - a) / (b - a), 0f, 1f));
        if (stunde < 6.5f) return Farbton.Mischen(TonNacht, TonMorgen, Glatt(5f, 6.5f, stunde));
        if (stunde < 8.5f) return Farbton.Mischen(TonMorgen, Farbton.Identitaet, Glatt(6.5f, 8.5f, stunde));
        if (stunde < 17f) return Farbton.Identitaet;
        if (stunde < 18.75f) return Farbton.Mischen(Farbton.Identitaet, TonAbend, Glatt(17f, 18.75f, stunde));
        return Farbton.Mischen(TonAbend, TonNacht, Glatt(18.75f, 20.75f, stunde));
    }

    /// <summary>Der Farbton der Jahreszeit: Frühling ist die Grundlage (Identität), Sommer und Herbst wärmer, der Winter kälter und blasser.</summary>
    private static Farbton TonJahreszeit(Jahreszeit jahreszeit) => jahreszeit switch
    {
        Jahreszeit.Sommer => new Farbton(new Vector3(1.02f, 1.00f, 0.97f), Vector3.Zero, 1.03f),
        Jahreszeit.Herbst => new Farbton(new Vector3(1.03f, 0.99f, 0.93f), Vector3.Zero, 1.02f),
        Jahreszeit.Winter => new Farbton(new Vector3(0.96f, 0.99f, 1.05f), new Vector3(-0.002f, 0f, 0.006f), 0.90f),
        _ => Farbton.Identitaet,
    };

    private Farbton _lutIst = Farbton.Identitaet;      // was gerade gilt (zieht dem Ziel nach)
    private Farbton _lutHochgeladen = Farbton.Identitaet;
    private ImageTexture3D? _lutTextur;
    private bool _lutAktiv;
    private bool _lutErster = true;

    /// <summary>Nach <see cref="LichtNachUhrzeit"/>: Ziel bestimmen, dem Ziel nachziehen, bei merklicher Änderung die LUT neu hochladen.</summary>
    private void FarbabstimmungBild(float uhrStunde, Jahreszeit jahreszeit, double echtzeit)
    {
        var an = Fenster.FarbabstimmungAn;
        if (an != _lutAktiv)
        {
            _lutAktiv = an;
            _lutErster = true;
            FarbstimmungAnwenden(_umgebung.Environment, true);
        }
        if (!an) return;
        var ziel = Farbton.Zusammen(TonTageszeit(BezugsStunde(uhrStunde, jahreszeit)), TonJahreszeit(jahreszeit));
        if (_lutErster) _lutIst = ziel;
        else _lutIst = Farbton.Mischen(_lutIst, ziel, 1f - Mathf.Exp(-(float)echtzeit / 0.8f));
        if (!_lutErster && _lutIst.Abstand(_lutHochgeladen) < 0.0015f) return;
        _lutErster = false;
        LutHochladen(_lutIst);
    }

    private void LutHochladen(Farbton ton)
    {
        _lutHochgeladen = ton;
        var scheiben = new Godot.Collections.Array<Image>();
        for (var z = 0; z < LutGroesse; z++)
        {
            var daten = new byte[LutGroesse * LutGroesse * 3];
            for (var y = 0; y < LutGroesse; y++)
            for (var x = 0; x < LutGroesse; x++)
            {
                var wert = Abstimmen(new Vector3(LutEingang(x), LutEingang(y), LutEingang(z)), ton);
                var o = (y * LutGroesse + x) * 3;
                daten[o] = (byte)Mathf.RoundToInt(Mathf.Clamp(wert.X, 0f, 1f) * 255f);
                daten[o + 1] = (byte)Mathf.RoundToInt(Mathf.Clamp(wert.Y, 0f, 1f) * 255f);
                daten[o + 2] = (byte)Mathf.RoundToInt(Mathf.Clamp(wert.Z, 0f, 1f) * 255f);
            }
            scheiben.Add(Image.CreateFromData(LutGroesse, LutGroesse, false, Image.Format.Rgb8, daten));
        }
        if (_lutTextur is null)
        {
            _lutTextur = new ImageTexture3D();
            _lutTextur.Create(Image.Format.Rgb8, LutGroesse, LutGroesse, LutGroesse, false, scheiben);
            FarbstimmungAnwenden(_umgebung.Environment, true);
        }
        else _lutTextur.Update(scheiben);
    }

    /// <summary>
    /// Der Farbwert, für den Stützpunkt <paramref name="i"/> die Antwort speichert. Godot fragt die LUT mit der Bildfarbe selbst als Koordinate ab (ohne Halbpunkt-Ausgleich):
    /// Der Punkt i liegt bei (i + 0,5) / n, nicht bei i / (n − 1) – sonst wäre selbst die Identität nicht die Identität (gemessen: Mittags 1,5 % dunkler). Die äußersten
    /// Punkte antworten für 0 und 1, damit Schwarz und Weiß genau bleiben.
    /// </summary>
    private static float LutEingang(int i) => i == 0 ? 0f : i == LutGroesse - 1 ? 1f : (i + 0.5f) / LutGroesse;

    /// <summary>Ein Farbwert (Anzeigefarbe nach dem Tonemapping, 0 bis 1) durch die Stimmung: Sättigung um die Helligkeit, Verstärkung je Kanal, Schatten getönt.</summary>
    private static Vector3 Abstimmen(Vector3 c, Farbton ton)
    {
        var luma = c.X * 0.2126f + c.Y * 0.7152f + c.Z * 0.0722f;
        var grau = new Vector3(luma, luma, luma);
        var v = grau + (c - grau) * ton.Saettigung;
        v *= ton.Gain;
        var dunkel = 1f - Mathf.SmoothStep(0f, 0.6f, luma);
        return v + ton.Schatten * dunkel;
    }
}
