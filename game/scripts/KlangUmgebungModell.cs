using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die <b>Rechnung der Umgebungsklänge</b> (Ton Schritt 4) ohne Godot: aus Uhrzeit, Bezugsstunde, Zoom, Innenansicht und Pause die Pegel der
/// Schleifen und der beiden Unterbusse, dazu der Planer der Einzelrufe. <see cref="KlangUmgebung"/> spielt nur ab, was hier herauskommt; die
/// Selbstprüfung <see cref="Selbsttest"/> (Startoption <c>--umgebungtest=1</c>) fährt alle Fälle ohne Spiel ab.
///
/// <para><b>Zwei Uhren.</b> Vögel, Nacht und Abendlicht folgen der <b>Bezugsstunde</b> des Lichts (Stadtansicht.Licht.cs: 5 = Dämmerung
/// beginnt, 6 = Aufgang, 7,5 = voller Tag, 18 = Abendrot, 19,5 = Untergang, 21 = dunkel, 22 = Nacht; je Jahreszeit auf die Sonnenzeiten gestreckt).
/// Das Stadtleben folgt der <b>echten Uhr</b>: Arbeit 8–20 Uhr, Wirtshaus bis 22 Uhr (CLAUDE.md, feste Regeln). Im Winter ist es um 8 Uhr also
/// noch dunkel, aber die Stadt ist schon wach; im Sommer ist um 04:00 schon voller Vogelchor (beides gewollt).</para>
///
/// <para><b>Mischen.</b> Jede Schicht hat ein Gewicht w (Leistung, 0–1) aus stetigen Kurven (Trapeze mit Smoothstep-Flanken, keine harte
/// Schwelle). Der Pegel ist <c>Grund + 10·log10(w · Jahreszeit · Wetter)</c>, unter −60 dB stumm; die Summe aller Schichten wird gedeckelt
/// (<see cref="DeckelDb"/>). Die Tageszeit wird <b>nicht</b> geglättet (sonst hinkt der Klang bei 32x dem Licht eine Spielstunde nach): Nur die
/// Amplitude jeder Schicht darf sich höchstens um <see cref="AmplitudeJeSekunde"/> je Sekunde Echtzeit ändern (kein Flackern bei wenigen
/// Bildern). Springt die Uhr (Jahreswechsel 23 → 04, Laden, Spulen, Jahreszeitwechsel), blendet alles über <see cref="SprungSekunden"/> über.
/// Zoom (0,3 s), Innenansicht (0,6 s) und Pause (1 s) werden in Echtzeit geglättet.</para>
///
/// <para><b>Zoom.</b> Ausgewertet wird die Kameragröße S (sichtbare Bildhöhe in Metern, Startzoom 95): Nähe <c>N = 1 − smoothstep(45, 135, S)</c>
/// (bei 95 etwa 0,42) blendet die Nahschichten, Höhe <c>H = smoothstep(min(95, Smax − 20), Smax, S)</c> mit dem wirklichen Höchstzoom der Karte
/// dämpft die Stadt (Bus UmgebungStadt: bis −8 dB, Tiefpass bis 3,5 kHz) und holt den Wind herein. In Räumen und im Dom gilt N = 0,5, H = 0.</para>
///
/// <para><b>Anschluss Schritt 5.</b> <see cref="WetterFaktor"/> je Schicht (Standard 1) und die Jahreszeit-Tabelle <see cref="JahreszeitFaktor"/>
/// (Vögel im Winter fast still) – Schritt 5 verfeinert beide, statt die Kurven anzufassen.</para>
/// </summary>
public static class KlangUmgebungModell
{
    public enum Innenart { Draussen, InnenLeicht, Raum }
    public enum Bus { Stadt, Draussen }

    /// <summary>Was die Umgebung je Bild wissen muss. <see cref="UhrMinute"/> mit Bruchteil (gezeigte Uhr, auch <c>--lichtuhr</c>).</summary>
    public readonly record struct Eingaben(
        float UhrMinute, float Bezug, Jahreszeit Jahreszeit, float Zoom, float ZoomGroesst, Innenart Innen, bool Pausiert,
        double TicksProSekunde, bool Hintergrund = false);

    /// <summary>Eine Schleife: Name = Datei <c>schichten/&lt;Name&gt;.ogg</c>, Grundpegel in dB (Dateien auf −26 LUFS), Bus, Zweck für den Entwicklerreiter.</summary>
    public sealed record Schicht(string Name, string Zweck, float GrundDb, Bus Bus);

    public static readonly Schicht[] Schichten =
    {
        new("stadt_tag", "Stadtleben am Tag: Arbeit, Wasser, ferne Stimmen (Gerberei Fes, autofreie Altstadt); ab der Dämmerung schwach, 8–20 Uhr voll", -6f, Bus.Stadt),
        new("stadt_gemurmel", "Gemurmel vieler Leute, nur nah (Halle von oben, gedämpft)", -16f, Bus.Stadt),
        new("stadt_abend", "Gedämpftes Gemurmel am Abend wie aus Schenken, 20–22 Uhr (nah voll)", -13f, Bus.Stadt),
        new("morgen_voegel", "Vogelchor im Morgengrauen (Dämmerung bis 1,5 h nach Aufgang)", -12f, Bus.Draussen),
        new("tag_spatzen", "Spatzen am Tag, leise, nah deutlicher", -20f, Bus.Draussen),
        new("abend_voegel", "Abendgesang (Abendrot bis dunkel)", -15f, Bus.Draussen),
        new("nacht", "Stiller Nachtgrund: sehr ferner, dunkler Wind (Synthese)", -17f, Bus.Draussen),
        new("wind_leicht", "Leichter Wind in der Höhe (Synthese), nah kaum, weit voll", -8f, Bus.Draussen),
        new("wind_mittel", "Kräftigerer Wind, nur ganz weit herausgezoomt (Kitt Peak)", -10f, Bus.Draussen),
    };

    /// <summary>Anschluss für Schritt 5 (Wetter): Faktor (Leistung) je Schicht in der Reihenfolge von <see cref="Schichten"/>, Standard 1.</summary>
    public static readonly float[] WetterFaktor = Enumerable.Repeat(1f, Schichten.Length).ToArray();

    /// <summary>Die Summe aller Schichten (Leistung, relativ zum Dateipegel −26 LUFS) bleibt darunter: −4 dB = −30 LUFS vor dem Regler.</summary>
    public const float DeckelDb = -4f;
    /// <summary>Höchste Änderung der Amplitude (0–1) einer Schicht je Sekunde Echtzeit (ganzer Hub in 0,5 s).</summary>
    public const float AmplitudeJeSekunde = 2f;
    /// <summary>Überblendung bei einem Sprung der Uhr (Sekunden Echtzeit); auch beim ersten Bild von der Stille her.</summary>
    public const float SprungSekunden = 3f;
    /// <summary>Unter diesem Pegel (dB) gilt eine Schicht als stumm.</summary>
    public const float StummDb = -60f;
    public const float AusDb = -80f;

    public const float StartZoom = 95f;
    public const float PauseDb = -8f;
    public const float RaumDb = -14f, RaumTiefpass = 500f;
    public const float DomDb = -6f, DomTiefpass = 1500f;
    public const float HoeheDb = -8f, HoeheTiefpass = 3500f;
    public const float Offen = 20500f;
    public const float FernTiefpass = 2500f;

    // ── Kurven ────────────────────────────────────────────────────────────────────────────────────────────────────

    public static float Glatt(float a, float b, float x)
    {
        if (b <= a) return x >= b ? 1f : 0f;
        var t = Math.Clamp((x - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Trapez: steigt von a bis b, voll bis c, fällt bis d.</summary>
    public static float Trapez(float x, float a, float b, float c, float d) => Glatt(a, b, x) * (1f - Glatt(c, d, x));

    /// <summary>Stadtleben am Tag nach der Uhr (Stunden): schwach (0,3) ab der Dämmerung – die Stadt erwacht –, voll 08:30–19:00, aus bis 20:30.</summary>
    public static float StadtTag(float stunde, float bezug) =>
        Math.Max(0.3f * Glatt(5.0f, 5.8f, bezug) * (1f - Glatt(20.5f, 21.5f, bezug)) * (1f - Glatt(19.0f, 20.5f, stunde)),
                 Trapez(stunde, 7.0f, 8.5f, 19.0f, 20.5f));

    /// <summary>Abend nach der Uhr: steigt 19:00–20:30, voll bis 22:00, fällt bis 23:00 auf 0,15 (Wirtshaus bis 22 Uhr).</summary>
    public static float StadtAbend(float stunde) => Glatt(19.0f, 20.5f, stunde) * (1f - 0.85f * Glatt(22.0f, 23.0f, stunde));

    /// <summary>Nacht nach der Bezugsstunde: voll bei 5 (vor der Dämmerung) und ab 21,5; dazwischen aus.</summary>
    public static float Nacht(float bezug) => Math.Max(1f - Glatt(5.0f, 5.8f, bezug), Glatt(20.5f, 21.5f, bezug));

    public static float Naehe(float zoom) => 1f - Glatt(45f, 135f, zoom);

    public static float Hoehe(float zoom, float groesst)
    {
        var oben = Math.Max(groesst, 45f);
        return Glatt(Math.Min(StartZoom, oben - 20f), oben, zoom);
    }

    /// <summary>Jahreszeit (Leistung) je Schicht: Vögel singen im Frühling voll, im Winter kaum; Stadt und Wind unverändert.</summary>
    public static float JahreszeitFaktor(string schicht, Jahreszeit j) => schicht switch
    {
        "morgen_voegel" or "abend_voegel" => j switch { Jahreszeit.Sommer => 0.8f, Jahreszeit.Herbst => 0.3f, Jahreszeit.Winter => 0.1f, _ => 1f },
        "tag_spatzen" => j switch { Jahreszeit.Herbst => 0.8f, Jahreszeit.Winter => 0.6f, _ => 1f },
        _ => 1f,
    };

    /// <summary>Das Zielgewicht (Leistung 0–1, ohne Jahreszeit und Wetter) jeder Schicht.</summary>
    public static float Gewicht(int i, float stunde, float b, float n, float h) => Schichten[i].Name switch
    {
        "stadt_tag" => StadtTag(stunde, b),
        "stadt_gemurmel" => Trapez(stunde, 7.0f, 8.5f, 19.0f, 20.5f) * n,
        "stadt_abend" => StadtAbend(stunde) * (0.25f + 0.75f * Glatt(0.1f, 0.45f, n)),
        "morgen_voegel" => Trapez(b, 5.0f, 5.6f, 7.0f, 8.5f) * (1f - 0.6f * h),
        "tag_spatzen" => Trapez(b, 6.5f, 7.5f, 18.0f, 19.0f) * (0.3f + 0.7f * n) * (1f - 0.6f * h),
        "abend_voegel" => Trapez(b, 17.5f, 18.3f, 19.8f, 21.0f) * (1f - 0.6f * h),
        "nacht" => Nacht(b),
        "wind_leicht" => (0.05f + 0.95f * h) * (1f - 0.2f * Nacht(b)),
        "wind_mittel" => Glatt(0.55f, 1f, h),
        _ => 0f,
    };

    // ── Zustand und Schritt ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Was ein Bild ergibt: Pegel je Schicht und die beiden Unterbusse.</summary>
    public sealed class Ausgabe
    {
        public readonly float[] Ziel = new float[Schichten.Length];     // Zielamplitude (0–1, nach Deckel)
        public readonly float[] Amplitude = new float[Schichten.Length]; // gezeigte Amplitude (geglättet)
        public readonly float[] Db = new float[Schichten.Length];
        public float StadtDb, StadtTiefpass = Offen, DraussenDb, DraussenTiefpass = Offen;
        public float Naehe, Hoehe, StadtTagGewicht, NachtGewicht;
        public bool Sprungblende;
        public bool ErsterSprung;
    }

    public sealed class Zustand
    {
        public readonly Ausgabe Aus = new();
        private bool _gestartet;
        private float _letzteUhr;
        private Jahreszeit _letzteJahreszeit;
        private float _n = 0.5f, _h;
        private float _innenDb, _innenTp = Offen, _pauseDb;
        private float _blende = 1f;
        private readonly float[] _blendeVon = new float[Schichten.Length];
        public int Sprungzahl { get; private set; }

        public Ausgabe Schritt(in Eingaben e, double delta)
        {
            var dt = (float)Math.Max(delta, 0);
            var aus = Aus;
            // Sprung: die Uhr läuft rückwärts (Jahreswechsel, Laden) oder weiter, als das Tempo in diesem Bild erlaubt, oder die Jahreszeit wechselt.
            // Die Schwelle wächst mit dem Tempo (bei kurzer Rundendauer und 32x rückt die Uhr je Bild weit vor – das ist kein Sprung).
            var erwartet = e.Pausiert ? 0 : e.TicksProSekunde * dt;
            var sprung = false;
            if (!_gestartet)
            {
                _gestartet = true;
                _n = e.Innen == Innenart.Draussen ? Naehe(e.Zoom) : 0.5f;
                _h = e.Innen == Innenart.Draussen ? Hoehe(e.Zoom, e.ZoomGroesst) : 0f;
                (_innenDb, _innenTp) = InnenZiel(e.Innen);
                _pauseDb = e.Pausiert ? PauseDb : 0;
                sprung = true;
                aus.ErsterSprung = true;
            }
            else
            {
                var d = e.UhrMinute - _letzteUhr;
                sprung = d < -0.5f || d > Math.Max(45.0, 3.0 * erwartet + 5.0) || e.Jahreszeit != _letzteJahreszeit;
                if (sprung) Sprungzahl++;
                aus.ErsterSprung = false;
            }
            _letzteUhr = e.UhrMinute;
            _letzteJahreszeit = e.Jahreszeit;

            var draussen = e.Innen == Innenart.Draussen;
            var zielN = draussen ? Naehe(e.Zoom) : 0.5f;
            var zielH = draussen ? Hoehe(e.Zoom, e.ZoomGroesst) : 0f;
            _n += (zielN - _n) * Folge(dt, 0.3f);
            _h += (zielH - _h) * Folge(dt, 0.3f);
            var (innenDb, innenTp) = InnenZiel(e.Innen);
            _innenDb += (innenDb - _innenDb) * Folge(dt, 0.6f);
            _innenTp = MathF.Exp(MathF.Log(_innenTp) + (MathF.Log(innenTp) - MathF.Log(_innenTp)) * Folge(dt, 0.6f));
            _pauseDb += ((e.Pausiert ? PauseDb : 0) - _pauseDb) * Folge(dt, 1f);

            var stunde = e.UhrMinute / 60f;
            var b = Math.Clamp(e.Bezug, 5f, 22f);
            aus.Naehe = _n;
            aus.Hoehe = _h;
            aus.StadtTagGewicht = StadtTag(stunde, b);
            aus.NachtGewicht = Nacht(b);
            aus.StadtDb = HoeheDb * _h;
            aus.StadtTiefpass = Offen * MathF.Pow(HoeheTiefpass / Offen, _h);
            aus.DraussenDb = _innenDb + _pauseDb;
            aus.DraussenTiefpass = _innenTp;

            // Ziele mit Jahreszeit und Wetter, dann der Deckel über die Summe (Leistung relativ zur Datei, Stadtbus mitgerechnet)
            var summe = 0.0;
            for (var i = 0; i < Schichten.Length; i++)
            {
                var w = Gewicht(i, stunde, b, _n, _h) * JahreszeitFaktor(Schichten[i].Name, e.Jahreszeit) * WetterFaktor[i];
                aus.Ziel[i] = MathF.Sqrt(Math.Clamp(w, 0f, 1f));
                var busDb = Schichten[i].Bus == Bus.Stadt ? aus.StadtDb : 0f;
                summe += aus.Ziel[i] * aus.Ziel[i] * Math.Pow(10, (Schichten[i].GrundDb + busDb) / 10.0);
            }
            var deckel = Math.Pow(10, DeckelDb / 10.0);
            if (summe > deckel)
            {
                var f = (float)Math.Sqrt(deckel / summe);
                for (var i = 0; i < Schichten.Length; i++) aus.Ziel[i] *= f;
            }

            if (sprung)
            {
                Array.Copy(aus.Amplitude, _blendeVon, Schichten.Length);
                _blende = 0f;
            }
            if (_blende < 1f)
            {
                _blende = Math.Min(1f, _blende + dt / SprungSekunden);
                var t = Glatt(0f, 1f, _blende);
                for (var i = 0; i < Schichten.Length; i++) aus.Amplitude[i] = _blendeVon[i] + (aus.Ziel[i] - _blendeVon[i]) * t;
            }
            else
            {
                var schritt = AmplitudeJeSekunde * dt;
                for (var i = 0; i < Schichten.Length; i++)
                    aus.Amplitude[i] += Math.Clamp(aus.Ziel[i] - aus.Amplitude[i], -schritt, schritt);
            }
            aus.Sprungblende = _blende < 1f;
            for (var i = 0; i < Schichten.Length; i++)
            {
                var a = aus.Amplitude[i];
                var db = a > 0 ? Schichten[i].GrundDb + 20f * MathF.Log10(a) : AusDb;
                aus.Db[i] = db < StummDb ? AusDb : db;
            }
            return aus;
        }

        private static (float Db, float Tiefpass) InnenZiel(Innenart innen) => innen switch
        {
            Innenart.Raum => (RaumDb, RaumTiefpass),
            Innenart.InnenLeicht => (DomDb, DomTiefpass),
            _ => (0f, Offen),
        };

        /// <summary>Anteil einer exponentiellen Glättung mit Zeitkonstante <paramref name="tau"/> in diesem Bild.</summary>
        private static float Folge(float dt, float tau) => 1f - MathF.Exp(-dt / tau);
    }

    // ── Einzelrufe ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Eine Art Einzelruf: Varianten <c>rufe/&lt;Datei&gt;_1..n.ogg</c>, Abstände in Echtzeit (Mindestabstand und Mittel der Wartezeit, geteilt
    /// durch das Fenstergewicht), Pegel nah und fern (dB, Dateien auf −20 LUFS, Spitze begrenzt), Zoomgrenzen (S): ab <see cref="FernAb"/> nur
    /// noch fern (leiser, Tiefpass 2,5 kHz), ab <see cref="Bis"/> gar nicht. <see cref="Fenster"/> gibt das Gewicht (0–1) nach Uhr und Bezugsstunde.
    /// </summary>
    public sealed record Rufart(string Name, string Zweck, string Datei, int Varianten, float Mindest, float Mittel, float NahDb, float FernDb,
                                float FernAb, float Bis, bool NachtsFern, Func<float, float, float, float> Fenster, string? FernDatei = null, int FernVarianten = 0);

    /// <summary>Fenster: (Stunde, Bezugsstunde, Gewicht Stadtleben) → 0–1.</summary>
    public static readonly Rufart[] Rufarten =
    {
        new("Hahn", "Hahnenschrei im Morgengrauen, tagsüber selten", "hahn", 3, 20, 45, -10, -18, 70, 120, false,
            (h, b, tag) => Math.Max(Trapez(b, 5.0f, 5.3f, 7.0f, 8.5f), 0.08f * Trapez(b, 7.0f, 8.5f, 17.5f, 18.5f))),
        new("Hund", "Bellen, am Tag selten und nah, abends und nachts nur fern", "hund", 5, 60, 150, -13, -20, 70, 120, true,
            (h, b, tag) => 0.6f + 0.4f * (1f - tag), "hund_fern", 4),
        new("Kauz", "Waldkauz in der Nacht (nicht, solange die Stadt noch lebt)", "kauz", 5, 30, 90, -16, -20, 130, 130, false,
            (h, b, tag) => Math.Max(1f - Glatt(5.0f, 5.4f, b), Glatt(20.5f, 21.0f, b)) * (1f - tag)),
        new("Amsel", "Amselstrophe am Abend", "amsel", 5, 8, 25, -14, -20, 90, 110, false,
            (h, b, tag) => Trapez(b, 17.5f, 18.2f, 20.0f, 20.8f)),
        new("Pferd", "Pferd in der Gasse (Hufe auf Erde, Atmen), 8–20 Uhr", "pferd", 3, 40, 120, -4, -10, 70, 100, false,
            (h, b, tag) => Trapez(h, 7.5f, 8.0f, 19.5f, 20.0f)),
        new("Gefluegel", "Gänse und Hühner, 7–19 Uhr", "gefluegel", 4, 45, 150, -14, -20, 70, 100, false,
            (h, b, tag) => Trapez(h, 6.5f, 7.0f, 18.5f, 19.0f)),
        new("Passant", "Ein paar Leute gehen vorbei (Schritte), nur nah", "passant", 5, 6, 15, -15, -15, 105, 105, false,
            (h, b, tag) => Math.Clamp(tag + 0.3f * StadtAbend(h), 0f, 1f)),
        new("Handwerk", "Fernes Hämmern aus einer Schmiede, Arbeitszeit 8–20 Uhr", "handwerk", 4, 20, 60, -12, -12, 0, 130, false,
            (h, b, tag) => Trapez(h, 7.75f, 8.25f, 19.75f, 20.25f)),
    };

    /// <summary>Unter diesem Fenstergewicht ruft eine Art nicht.</summary>
    public const float RufSchwelle = 0.02f;
    /// <summary>Mindestabstand zwischen zwei beliebigen Rufen (Sekunden Echtzeit).</summary>
    public const float GlobalAbstand = 5f;
    /// <summary>In der Pause rufen die Arten so viel seltener (und 8 dB leiser über den Bus).</summary>
    public const float PauseSeltener = 3f;

    /// <summary>Ein Ruf, den der Planer abspielen lässt.</summary>
    public readonly record struct Ruf(int Art, string Datei, bool Fern, float Seite, float Db, float Tonhoehe);

    /// <summary>
    /// Plant die Einzelrufe: je Art ein Rückwärtszähler (Sekunden), der je Sekunde Echtzeit um Fenstergewicht × (Pause ? 1/3 : 1) sinkt – so
    /// gelten Mindestabstand und Mittel in Echtzeit, auch bei 32x (kein Dauerkrähen). Höchstens ein Ruf je Platz (nah, fern) gleichzeitig,
    /// zwischen zwei Rufen mindestens <see cref="GlobalAbstand"/> s. Im Hintergrund und während einer Sprungblende steht alles; ein fälliger,
    /// aber blockierter Ruf wartet 2–8 s neu (so kommen nach einer Pause nicht mehrere auf einmal). Eigener Zufall, nie der der Simulation.
    /// </summary>
    public sealed class RufPlaner
    {
        private readonly Random _zufall;
        private readonly double[] _rest = new double[Rufarten.Length];
        private readonly bool[] _imFenster = new bool[Rufarten.Length];
        private double _seitLetztem = GlobalAbstand;
        private double _nahFrei, _fernFrei, _jetzt;
        private readonly int[] _letzteVariante = Enumerable.Repeat(-1, Rufarten.Length).ToArray();
        private readonly double[] _letzterRuf = Enumerable.Repeat(-1e9, Rufarten.Length).ToArray();

        /// <summary>Länge einer Datei in Sekunden (der Knoten trägt die echten Längen ein; ohne Eintrag 3 s).</summary>
        public Func<string, double> Laenge = _ => 3.0;

        public RufPlaner(int saat)
        {
            _zufall = new Random(saat);
            for (var i = 0; i < _rest.Length; i++) _rest[i] = Rufarten[i].Mindest + _zufall.NextDouble() * Rufarten[i].Mittel;
        }

        /// <summary>Sekunden (bei vollem Fenster), bis die Art wieder dran ist (für den Entwicklerreiter).</summary>
        public double Rest(int art) => _rest[art];

        public double Jetzt => _jetzt;

        /// <summary>Fenstergewicht der Art bei diesen Eingaben, 0 außerhalb der Zoomgrenze.</summary>
        public static float FensterGewicht(int art, in Eingaben e, Ausgabe aus)
        {
            var r = Rufarten[art];
            var zoom = e.Innen == Innenart.Draussen ? e.Zoom : 60f;
            if (zoom >= r.Bis) return 0f;
            return Math.Clamp(r.Fenster(e.UhrMinute / 60f, Math.Clamp(e.Bezug, 5f, 22f), aus.StadtTagGewicht), 0f, 1f);
        }

        public void Schritt(in Eingaben e, Ausgabe aus, double delta, List<Ruf> neu)
        {
            _jetzt += delta;
            _seitLetztem += delta;
            if (e.Hintergrund || aus.Sprungblende) return;
            var faktor = e.Pausiert ? 1.0 / PauseSeltener : 1.0;
            for (var i = 0; i < Rufarten.Length; i++)
            {
                var g = FensterGewicht(i, e, aus);
                var drin = g > RufSchwelle;
                if (drin && !_imFenster[i] && _rest[i] > Rufarten[i].Mittel) _rest[i] = _zufall.NextDouble() * Rufarten[i].Mittel; // eben ins Fenster gekommen
                _imFenster[i] = drin;
                if (!drin) continue;
                _rest[i] -= delta * faktor * g;
                if (_rest[i] > 0) continue;
                var r = Rufarten[i];
                if (_jetzt - _letzterRuf[i] < r.Mindest) // nach dem Wiedereintritt ins Fenster: der Mindestabstand gilt trotzdem
                {
                    _rest[i] = r.Mindest - (_jetzt - _letzterRuf[i]);
                    continue;
                }
                var zoom = e.Innen == Innenart.Draussen ? e.Zoom : 60f;
                var fern = r.FernAb <= 0 || zoom >= r.FernAb || (r.NachtsFern && aus.NachtGewicht > 0.5f) || (r.NachtsFern && aus.StadtTagGewicht < 0.5f);
                var frei = fern ? _jetzt >= _fernFrei : _jetzt >= _nahFrei;
                if (!frei || _seitLetztem < GlobalAbstand)
                {
                    _rest[i] = 2 + 6 * _zufall.NextDouble();
                    continue;
                }
                neu.Add(Erzeugen(i, fern, zoom));
                _rest[i] = r.Mindest + Exponentiell(r.Mittel - r.Mindest);
            }
        }

        /// <summary>Einen Ruf dieser Art sofort (Entwicklerreiter, Hörprobe); hält die Abstände nicht ein.</summary>
        public Ruf Erzwingen(int art, bool fern, float zoom = 45f) => Erzeugen(art, fern, zoom);

        private Ruf Erzeugen(int art, bool fern, float zoom)
        {
            var r = Rufarten[art];
            var datei = fern && r.FernDatei is not null ? r.FernDatei : r.Datei;
            var anzahl = fern && r.FernDatei is not null ? r.FernVarianten : r.Varianten;
            var v = _zufall.Next(anzahl);
            if (anzahl > 1 && v == _letzteVariante[art]) v = (v + 1 + _zufall.Next(anzahl - 1)) % anzahl; // nie zweimal dieselbe hintereinander
            _letzteVariante[art] = v;
            var name = $"{datei}_{v + 1}";
            var seite = (float)(_zufall.NextDouble() * 2 - 1) * (fern ? 0.5f : 0.8f);
            // nahe Rufe werden zum Startzoom hin leiser (bis −6 dB bei S = 100): man ist weiter weg, aber die Gasse lebt noch
            var db = (fern ? r.FernDb : r.NahDb - 6f * Glatt(40f, 100f, zoom)) + (float)(_zufall.NextDouble() * 6 - 3);
            var hoehe = 1f + (float)(_zufall.NextDouble() * 0.08 - 0.04);
            var ende = _jetzt + Laenge(name) / hoehe + 0.2;
            if (fern) _fernFrei = ende;
            else _nahFrei = ende;
            _seitLetztem = 0;
            _letzterRuf[art] = _jetzt;
            return new Ruf(art, name, fern, seite, db, hoehe);
        }

        private double Exponentiell(double mittel) => -Math.Log(1 - _zufall.NextDouble()) * Math.Max(mittel, 0.1);
    }

    // ── Selbstprüfung (--umgebungtest=1) ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fährt alle Kurven und typische Abläufe ohne Spiel ab und meldet jede Abweichung. Gibt zurück, ob alles stimmt. Mit
    /// <paramref name="kaputt"/> wird absichtlich eine falsche Kurve geprüft (Nachweis, dass der Test FEHLER meldet).
    /// </summary>
    public static bool Selbsttest(Action<string> log, bool kaputt = false)
    {
        var fehler = new List<string>();
        void Pruefe(bool gut, string was) { if (!gut) fehler.Add(was); }
        int Index(string name) => Array.FindIndex(Schichten, s => s.Name == name);

        // 1. Wertebereich und Stetigkeit über alle Stunden, Bezugsstunden und Zoomstufen: benachbarte Schritte (0,1 h) höchstens 0,26 –
        //    die steilste Flanke (Morgenchor 5,0–5,6) ist 0,6 Bezugsstunden breit, im Frühling etwa 24 Spielminuten
        var zooms = new[] { 25f, 45f, 75f, 95f, 100f, 130f, 140f };
        var groessteSpruenge = 0f;
        for (var i = 0; i < Schichten.Length; i++)
            foreach (var s in zooms)
                foreach (var groesst in new[] { 140f, 110f })
                {
                    float n = Naehe(s), h = Hoehe(s, groesst);
                    for (var stunde = 4f; stunde <= 23.001f; stunde += 0.1f)
                        for (var b = 5f; b <= 22.001f; b += 0.1f)
                        {
                            var w = Gewicht(i, stunde, b, n, h);
                            if (kaputt && i == 0 && stunde > 12f) w = 1.5f;
                            Pruefe(w is >= 0f and <= 1f && float.IsFinite(w), $"{Schichten[i].Name}: Gewicht {w} außerhalb 0–1 (Uhr {stunde:0.0}, b {b:0.0}, S {s})");
                            var db = Math.Abs(Gewicht(i, stunde + 0.1f, b, n, h) - w);
                            var dbz = Math.Abs(Gewicht(i, stunde, b + 0.1f, n, h) - w);
                            groessteSpruenge = Math.Max(groessteSpruenge, Math.Max(db, dbz));
                        }
                }
        Pruefe(groessteSpruenge <= 0.26f, $"Sprung zwischen benachbarten Schritten {groessteSpruenge:0.00} > 0,26");
        log($"UMGEBUNGTEST Kurven: größter Schritt je 0,1 h {groessteSpruenge:0.000}");

        // 2. Feste Punkte
        Pruefe(Nacht(22f) >= 0.95f && Nacht(5f) >= 0.95f, "Nacht bei b = 22 und b = 5 nicht voll");
        Pruefe(Gewicht(Index("morgen_voegel"), 5.9f, 6f, 1, 0) >= 0.8f, "Morgenchor bei b = 6 unter 0,8");
        Pruefe(Gewicht(Index("stadt_tag"), 12f, 12f, 1, 0) >= 0.9f, "Stadtleben um 12 Uhr unter 0,9");
        Pruefe(Gewicht(Index("stadt_gemurmel"), 12f, 12f, Naehe(95), Hoehe(95, 140)) >= 0.35f, "Gemurmel am Startzoom 95 mittags unter 0,35");
        Pruefe(Gewicht(Index("wind_leicht"), 12f, 12f, Naehe(140), Hoehe(140, 140)) >= 0.9f, "Wind leicht am Höchstzoom unter 0,9");
        Pruefe(Gewicht(Index("wind_leicht"), 12f, 12f, Naehe(110), Hoehe(110, 110)) >= 0.9f, "Wind leicht am Höchstzoom einer kleinen Karte (110) unter 0,9");
        foreach (var s in new[] { 25f, 45f, 75f, 95f, 100f })
            Pruefe(Gewicht(Index("wind_mittel"), 12f, 12f, Naehe(s), Hoehe(s, 140)) <= 0.05f, $"Wind mittel bei S = {s} über 0,05");
        Pruefe(Gewicht(Index("stadt_tag"), 8f, 5f, 1, 0) >= 0.6f, "Winter 08:00 (noch dunkel): Stadtleben fehlt");
        // Winter 17 Uhr: dunkel (b ≥ 21), Stadt noch voll → der Kauz ruft nicht
        var kauz = Array.FindIndex(Rufarten, r => r.Name == "Kauz");
        Pruefe(Rufarten[kauz].Fenster(17f, 21.4f, StadtTag(17f, 21.4f)) <= RufSchwelle, "Kauz ruft im Winter um 17 Uhr über dem Stadtleben");
        Pruefe(Rufarten[kauz].Fenster(23f, 22f, StadtTag(23f, 22f)) >= 0.9f, "Kauz ruft um 23 Uhr nicht");
        var hahn = Array.FindIndex(Rufarten, r => r.Name == "Hahn");
        {
            // Startzoom 95 mittags: mindestens eine Art ruft nah (die Gasse lebt auch in der Ansicht, in der man am meisten spielt)
            var e95 = new Eingaben(12 * 60, 12, Jahreszeit.Fruehling, 95, 140, Innenart.Draussen, false, 1.27);
            var a95 = new Zustand().Schritt(e95, 0.1);
            var nahe = Enumerable.Range(0, Rufarten.Length).Where(i => RufPlaner.FensterGewicht(i, e95, a95) > RufSchwelle && Rufarten[i].FernAb > 95).Select(i => Rufarten[i].Name).ToList();
            Pruefe(nahe.Count > 0, "Startzoom 95 mittags: keine Rufart nah");
            log("UMGEBUNGTEST Startzoom 95 mittags nah: " + string.Join(", ", nahe));
        }
        Pruefe(Rufarten[hahn].Fenster(4f, 5f, 0) <= RufSchwelle, "Hahn kräht vor der Dämmerung");

        // 3. Ruhezustand: Raum, Dom, Pause; Startzoom-Pegel und Deckel
        Ausgabe Lauf(Eingaben e, double sek, double dt = 1.0 / 60)
        {
            var z = new Zustand();
            Ausgabe a = z.Aus;
            for (var t = 0.0; t < sek; t += dt) a = z.Schritt(e, dt);
            return a;
        }
        var tag = new Eingaben(12 * 60, 12, Jahreszeit.Fruehling, 95, 140, Innenart.Draussen, false, 1.27);
        var raum = Lauf(tag with { Innen = Innenart.Raum }, 6);
        Pruefe(raum.DraussenDb <= -12f && raum.DraussenTiefpass <= 600f, $"Raum: {raum.DraussenDb:0.0} dB, {raum.DraussenTiefpass:0} Hz (erwartet ≤ −12 dB, ≤ 600 Hz)");
        var dom = Lauf(tag with { Innen = Innenart.InnenLeicht }, 6);
        Pruefe(Math.Abs(dom.DraussenDb - DomDb) < 0.5f && dom.DraussenTiefpass <= 1600f, $"Dom: {dom.DraussenDb:0.0} dB, {dom.DraussenTiefpass:0} Hz");
        var pause = Lauf(tag with { Pausiert = true }, 8);
        Pruefe(Math.Abs(pause.DraussenDb - PauseDb) <= 0.5f, $"Pause senkt um {pause.DraussenDb:0.0} dB statt {PauseDb}");
        foreach (var (name, e) in new[]
                 {
                     ("Tag nah", tag with { Zoom = 25 }), ("Tag Startzoom", tag), ("Tag oben", tag with { Zoom = 140 }),
                     ("Morgen", tag with { UhrMinute = 5 * 60 + 45, Bezug = 5.75f }), ("Abend", tag with { UhrMinute = 21 * 60, Bezug = 22 }),
                     ("Nacht", tag with { UhrMinute = 23 * 60, Bezug = 22 }),
                 })
        {
            var a = Lauf(e, 5);
            var summe = 0.0;
            for (var i = 0; i < Schichten.Length; i++)
                if (a.Db[i] > AusDb) summe += Math.Pow(10, (a.Db[i] + (Schichten[i].Bus == Bus.Stadt ? a.StadtDb : 0)) / 10.0);
            var lufs = -26 + 10 * Math.Log10(Math.Max(summe, 1e-12)) + a.DraussenDb;
            log($"UMGEBUNGTEST Pegel {name}: etwa {lufs:0.0} LUFS vor dem Regler (bei 80 % {lufs - 1.9:0.0}) · " +
                string.Join(" ", Schichten.Select((s, i) => a.Db[i] > AusDb ? $"{s.Name} {a.Db[i]:0.0}" : "").Where(t => t != "")));
            Pruefe(lufs <= -26 + DeckelDb + 0.3, $"{name}: Summe {lufs:0.0} LUFS über dem Deckel");
        }

        // 4. Abläufe bei hohem Tempo: kein Dauer-Sprung, kein Flackern, Klang folgt dem Licht
        foreach (var runde in new[] { 900.0, 60.0 })
            foreach (var fps in new[] { 2.0, 60.0 })
            {
                var z = new Zustand();
                var tps = 1140.0 / runde * 32;
                var dt = 1.0 / fps;
                var groessterSchritt = 0f;
                var verzug = 0f;
                var uhr = 4f * 60;
                var vorher = new float[Schichten.Length];
                for (var t = 0.0; uhr < 23 * 60; t += dt)
                {
                    uhr += (float)(tps * dt);
                    var b = BezugFruehling(uhr / 60f);
                    var a = z.Schritt(tag with { UhrMinute = Math.Min(uhr, 23 * 60), Bezug = b, TicksProSekunde = tps }, dt);
                    if (t > SprungSekunden + dt)
                        for (var i = 0; i < Schichten.Length; i++)
                        {
                            groessterSchritt = Math.Max(groessterSchritt, Math.Abs(a.Amplitude[i] - vorher[i]));
                            verzug = Math.Max(verzug, Math.Abs(a.Amplitude[i] - a.Ziel[i]));
                        }
                    Array.Copy(a.Amplitude, vorher, vorher.Length);
                }
                // Verzug in Spielminuten: höchstens so lange, wie die Amplitude für den Rest braucht
                var verzugMinuten = verzug / AmplitudeJeSekunde * tps;
                log($"UMGEBUNGTEST Tempo 32, Rundendauer {runde:0} s, {fps:0} Bilder/s: Sprünge {z.Sprungzahl}, größter Schritt {groessterSchritt:0.00} je Bild, Verzug höchstens {verzugMinuten:0} Spielminuten");
                Pruefe(z.Sprungzahl == 0, $"Tempo 32, Runde {runde} s, {fps} Bilder/s: {z.Sprungzahl} Sprungblenden ohne Sprung");
                Pruefe(groessterSchritt <= AmplitudeJeSekunde * dt + 1e-3, $"Tempo 32, {fps} Bilder/s: Schritt {groessterSchritt:0.00} größer als erlaubt");
                if (runde >= 900) Pruefe(verzugMinuten <= 25, $"Tempo 32, {fps} Bilder/s: Klang hinkt {verzugMinuten:0} Spielminuten hinter dem Licht");
            }

        // 5. Jahreswechsel 23 → 04 Uhr (mit neuer Jahreszeit): Überblendung über mindestens 2,5 s
        {
            var z = new Zustand();
            var e = tag with { UhrMinute = 22 * 60 + 59, Bezug = 22 };
            for (var i = 0; i < 300; i++) z.Schritt(e, 1.0 / 60);
            var dauer = 0.0;
            var e2 = e with { UhrMinute = 4 * 60, Bezug = 5, Jahreszeit = Jahreszeit.Sommer };
            z.Schritt(e2, 1.0 / 60);
            while (z.Aus.Sprungblende && dauer < 10) { z.Schritt(e2, 1.0 / 60); dauer += 1.0 / 60; }
            Pruefe(z.Sprungzahl == 1 && dauer >= 2.5, $"Jahreswechsel: {z.Sprungzahl} Sprünge, Blende {dauer:0.00} s (erwartet ≥ 2,5 s)");
        }

        // 6. Rufe: 30 Minuten Echtzeit je Lage, Abstände, Fenster, Zoom, Pause, Hintergrund
        var anzahl = new int[Rufarten.Length];
        foreach (var (lage, tempo, zoom, pausiert, hintergrund) in new[]
                 { ("Tempo 1", 1.0, 60f, false, false), ("Tempo 32", 32.0, 95f, false, false), ("Pause", 1.0, 60f, true, false), ("Hintergrund", 1.0, 60f, false, true), ("oben", 4.0, 140f, false, false) })
        {
            var z = new Zustand();
            var p = new RufPlaner(7);
            var rufe = new List<Ruf>();
            var letzte = Enumerable.Repeat(-1e9, Rufarten.Length).ToArray();
            var letzterIrgendein = -1e9;
            var dt = 1.0 / 30;
            var uhr = 4f * 60;
            var tps = 1140.0 / 900 * tempo;
            var zahl = 0;
            for (var t = 0.0; t < 1800; t += dt)
            {
                if (!pausiert) uhr += (float)(tps * dt);
                if (uhr >= 23 * 60) uhr = 4 * 60; // Jahreswechsel
                var e = tag with { UhrMinute = uhr, Bezug = BezugFruehling(uhr / 60f), Zoom = zoom, Pausiert = pausiert, TicksProSekunde = tps, Hintergrund = hintergrund };
                var a = z.Schritt(e, dt);
                rufe.Clear();
                p.Schritt(e, a, dt, rufe);
                foreach (var r in rufe)
                {
                    zahl++;
                    anzahl[r.Art]++;
                    var art = Rufarten[r.Art];
                    Pruefe(t - letzte[r.Art] >= art.Mindest - 1e-6, $"{lage}: {art.Name} nach {t - letzte[r.Art]:0.0} s (Mindestabstand {art.Mindest})");
                    Pruefe(t - letzterIrgendein >= GlobalAbstand - 1e-6, $"{lage}: zwei Rufe nach {t - letzterIrgendein:0.0} s");
                    Pruefe(RufPlaner.FensterGewicht(r.Art, e, a) > RufSchwelle, $"{lage}: {art.Name} außerhalb seines Fensters (Uhr {uhr / 60:0.0})");
                    Pruefe(zoom < art.Bis, $"{lage}: {art.Name} über seiner Zoomgrenze");
                    Pruefe(!hintergrund, $"{lage}: Ruf im Hintergrund");
                    letzte[r.Art] = t;
                    letzterIrgendein = t;
                }
            }
            log($"UMGEBUNGTEST Rufe {lage}: {zahl} in 30 min");
            if (lage == "Pause") Pruefe(zahl < 40, $"Pause: {zahl} Rufe in 30 min (zu viele)");
            if (lage == "Tempo 1") Pruefe(zahl > 30, $"Tempo 1: nur {zahl} Rufe in 30 min");
        }
        log("UMGEBUNGTEST Rufe je Art: " + string.Join(", ", Rufarten.Select((r, i) => $"{r.Name} {anzahl[i]}")));

        foreach (var f in fehler.Distinct().Take(30)) log("UMGEBUNGTEST FEHLER: " + f);
        log(fehler.Count == 0 ? "UMGEBUNGTEST OK" : $"UMGEBUNGTEST FEHLER: {fehler.Count} Abweichungen");
        return fehler.Count == 0;
    }

    /// <summary>Bezugsstunde im Frühling wie Stadtansicht.Licht.cs (für den Test; dort ist sie privat).</summary>
    public static float BezugFruehling(float stunde) => BezugsStunde(stunde, Sonnenzeiten.Fuer(Jahreszeit.Fruehling));

    private static float BezugsStunde(float stunde, Sonnenlauf lauf)
    {
        float daemmerung = lauf.Daemmerung / 60f, aufgang = lauf.Aufgang / 60f, untergang = lauf.Untergang / 60f, dunkel = lauf.Dunkel / 60f;
        var vollerTag = Math.Min(aufgang + 1.5f, (aufgang + untergang) / 2);
        var abendrot = Math.Max(untergang - 1.5f, (aufgang + untergang) / 2);
        static float L(float a, float b, float t) => a + (b - a) * t;
        if (stunde <= daemmerung) return 5f;
        if (stunde < aufgang) return L(5f, 6f, (stunde - daemmerung) / (aufgang - daemmerung));
        if (stunde < vollerTag) return L(6f, 7.5f, (stunde - aufgang) / (vollerTag - aufgang));
        if (stunde < abendrot) return L(7.5f, 18f, (stunde - vollerTag) / (abendrot - vollerTag));
        if (stunde < untergang) return L(18f, 19.5f, (stunde - abendrot) / (untergang - abendrot));
        if (stunde < dunkel) return L(19.5f, 21f, (stunde - untergang) / (dunkel - untergang));
        return Math.Min(22f, 21f + (stunde - dunkel));
    }
}
