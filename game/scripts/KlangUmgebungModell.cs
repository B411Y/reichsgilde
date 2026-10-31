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
    public enum Bus { Stadt, Draussen, Schenke }

    /// <summary>Was die Umgebung je Bild wissen muss. <see cref="UhrMinute"/> mit Bruchteil (gezeigte Uhr, auch <c>--lichtuhr</c>).</summary>
    public readonly record struct Eingaben(
        float UhrMinute, float Bezug, Jahreszeit Jahreszeit, float Zoom, float ZoomGroesst, Innenart Innen, bool Pausiert,
        double TicksProSekunde, bool Hintergrund = false, KlangStadtlebenModell.Lage? Leben = null)
    {
        /// <summary>Die Menschen in der Stadt; ohne Messung (null) gilt „alles voll“ wie vor der Nachbesserung (Selbsttests).</summary>
        public KlangStadtlebenModell.Lage Lage => Leben ?? KlangStadtlebenModell.Lage.Voll;
    }

    /// <summary>Eine Schleife: Name = Datei <c>schichten/&lt;Name&gt;.ogg</c>, Grundpegel in dB (Dateien auf −26 LUFS), Bus, Zweck für den Entwicklerreiter.</summary>
    public sealed record Schicht(string Name, string Zweck, float GrundDb, Bus Bus);

    public static readonly Schicht[] Schichten =
    {
        new("stadt_tag", "Stadtleben am Tag: Arbeit, Wasser, ferne Stimmen (Gerberei Fes, autofreie Altstadt); 8–20 Uhr, so voll wie die Straßen im Umkreis", -6f, Bus.Stadt),
        new("stadt_gemurmel", "Gemurmel vieler Leute: Menschen im Umkreis der Kamera und Markttreiben, nur nah (Halle von oben, gedämpft)", -16f, Bus.Stadt),
        new("stadt_abend", "Gedämpftes Gemurmel wie aus Schenken: nur aus geöffneten Schänken (8–22 Uhr) mit Gästen, am Ort des Wirtshauses", -13f, Bus.Schenke),
        new("morgen_voegel", "Vogelchor im Morgengrauen (Dämmerung bis 1,5 h nach Aufgang)", -13f, Bus.Draussen),
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

    /// <summary>
    /// Das Zielgewicht (Leistung 0–1, ohne Jahreszeit und Wetter) jeder Schicht. <paramref name="leben"/> sind die Menschen der Stadt
    /// (<see cref="KlangStadtlebenModell"/>): Mit Messung hängen Stadtleben, Gemurmel und Schenkengemurmel an ihnen und nicht an der Uhr –
    /// leere Straßen sind still (Stadtleben nur noch <see cref="StadtGrund"/>), ohne Messung gelten die alten Uhrkurven.
    /// </summary>
    public static float Gewicht(int i, float stunde, float b, float n, float h, KlangStadtlebenModell.Lage? leben = null)
    {
        var l = leben ?? KlangStadtlebenModell.Lage.Voll;
        return Schichten[i].Name switch
        {
            "stadt_tag" => StadtTag(stunde, b) * (l.Gemessen ? StadtGrund + (1f - StadtGrund) * l.Menge : 1f),
            "stadt_gemurmel" => l.Gemessen ? n * (1f - (1f - l.Menge) * (1f - l.Markt)) : Trapez(stunde, 7.0f, 8.5f, 19.0f, 20.5f) * n,
            "stadt_abend" => l.Gemessen ? l.Schenke : StadtAbend(stunde) * (0.25f + 0.75f * Glatt(0.1f, 0.45f, n)),
            "morgen_voegel" => Trapez(b, 5.0f, 5.6f, 7.0f, 8.5f) * (1f - 0.6f * h),
            "tag_spatzen" => Trapez(b, 6.5f, 7.5f, 18.0f, 19.0f) * (0.3f + 0.7f * n) * (1f - 0.6f * h),
            "abend_voegel" => Trapez(b, 17.5f, 18.3f, 19.8f, 21.0f) * (1f - 0.6f * h),
            "nacht" => Nacht(b),
            "wind_leicht" => (0.05f + 0.95f * h) * (1f - 0.2f * Nacht(b)),
            "wind_mittel" => Glatt(0.55f, 1f, h),
            _ => 0f,
        };
    }

    /// <summary>Was vom Stadtleben (Wasser, Arbeit, ferne Stimmen) bleibt, wenn im Umkreis niemand zu sehen ist: Leistung 0,06 (−12 dB).</summary>
    public const float StadtGrund = 0.06f;

    // ── Zustand und Schritt ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Was ein Bild ergibt: Pegel je Schicht und die beiden Unterbusse.</summary>
    public sealed class Ausgabe
    {
        public readonly float[] Ziel = new float[Schichten.Length];     // Zielamplitude (0–1, nach Deckel)
        public readonly float[] Amplitude = new float[Schichten.Length]; // gezeigte Amplitude (geglättet)
        public readonly float[] Db = new float[Schichten.Length];
        public float StadtDb, StadtTiefpass = Offen, DraussenDb, DraussenTiefpass = Offen;
        /// <summary>Schenkenbus: Seite (−1…+1) und Pegelausgleich des Panners in dB (−20·log10(1 + |Seite|), wie bei den Rufen).</summary>
        public float SchenkeSeite, SchenkeDb;
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

            // Die Schänke klingt aus ihrer Richtung: Seite des Wirtshauses im Bild, mit Ausgleich für Godots Panner
            var leben = e.Lage;
            aus.SchenkeSeite = leben.Gemessen ? Math.Clamp(leben.SchenkeSeite, -1f, 1f) * 0.8f : 0f;
            aus.SchenkeDb = -20f * MathF.Log10(1f + MathF.Abs(aus.SchenkeSeite));

            // Ziele mit Jahreszeit und Wetter, dann der Deckel über die Summe (Leistung relativ zur Datei, Stadtbus mitgerechnet)
            var summe = 0.0;
            for (var i = 0; i < Schichten.Length; i++)
            {
                var w = Gewicht(i, stunde, b, _n, _h, leben) * JahreszeitFaktor(Schichten[i].Name, e.Jahreszeit) * WetterFaktor[i];
                aus.Ziel[i] = MathF.Sqrt(Math.Clamp(w, 0f, 1f));
                var busDb = Schichten[i].Bus switch { Bus.Stadt => aus.StadtDb, Bus.Schenke => aus.StadtDb + aus.SchenkeDb, _ => 0f };
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
                                float FernAb, float Bis, bool NachtsFern, Func<float, float, float, float> Fenster, string? FernDatei = null, int FernVarianten = 0,
                                RufBedarf Bedarf = RufBedarf.Keiner);

    /// <summary>Was in der Stadt da sein muss, damit eine Art ruft: nichts (Tiere, Vögel), ein gehender Mensch im Umkreis oder eine arbeitende Werkstatt in Hörweite.</summary>
    public enum RufBedarf { Keiner, Geher, Werkstatt }

    /// <summary>Fenster: (Stunde, Bezugsstunde, Gewicht Stadtleben) → 0–1.</summary>
    public static readonly Rufart[] Rufarten =
    {
        new("Hahn", "Hahnenschrei im Morgengrauen, tagsüber selten", "hahn", 3, 20, 45, -12, -18, 70, 120, false,
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
        new("Passant", "Schritte eines Menschen, der im Umkreis wirklich geht (am Ort dieser Figur); öfter, je mehr gehen", "passant", 5, 6, 15, -15, -15, 105, 105, false,
            (h, b, tag) => Math.Clamp(tag + 0.3f * StadtAbend(h), 0f, 1f), Bedarf: RufBedarf.Geher),
        new("Handwerk", "Fernes Hämmern aus einer Schmiede oder Steinmetzhütte, die gerade arbeitet (am Ort des Betriebs), 8–20 Uhr", "handwerk", 4, 20, 60, -12, -12, 0, 130, false,
            (h, b, tag) => Trapez(h, 7.75f, 8.25f, 19.75f, 20.25f), Bedarf: RufBedarf.Werkstatt),
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
    /// Lautstärke des Rufspielers (dB) für diesen Ruf. Godots AudioEffectPanner hebt die eine Seite ohne Ausgleich an: Ein Mono-Ruf
    /// (beide Kanäle gleich, Pegel s) kommt bei der Seite p &gt; 0 als L = (1 − p)·s und R = (1 + p)·s heraus, bei p = 0,8 also 5 dB
    /// lauter als in der Mitte. Der Ausgleich −20·log10(1 + |p|) hält die lautere Seite auf dem Pegel der Mitte; so gilt die Pegeltabelle
    /// (NahDb/FernDb) für jede Seite, und keine Seite geht über die Spitze der Datei. Ein Ruf am Rand klingt dadurch insgesamt etwa
    /// 3 dB leiser als einer in der Mitte (wie ein lineares Panorama am Mischpult).
    /// </summary>
    public static float AbspielDb(in Ruf ruf) => Math.Min(ruf.Db, 0f) - 20f * MathF.Log10(1f + Math.Min(Math.Abs(ruf.Seite), 1f));

    /// <summary>Pegel der lauteren Seite nach Godots Panner (dB) für einen Mono-Ruf mit Spielerlautstärke <paramref name="db"/> – für die Selbstprüfung nachgerechnet.</summary>
    public static float PannerSpitzeDb(float seite, float db)
    {
        var p = Math.Clamp(seite, -1f, 1f);
        float lv = Math.Clamp(1f - p, 0f, 1f), rv = Math.Clamp(1f + p, 0f, 1f);
        float l = lv + (1f - rv), r = rv + (1f - lv); // L = l·lvol + r·(1 − rvol), R = r·rvol + l·(1 − lvol) mit l = r = 1
        return db + 20f * MathF.Log10(Math.Max(l, r));
    }

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
            var fenster = Math.Clamp(r.Fenster(e.UhrMinute / 60f, Math.Clamp(e.Bezug, 5f, 22f), aus.StadtTagGewicht), 0f, 1f);
            var lage = e.Lage;
            if (!lage.Gemessen) return fenster;
            return fenster * r.Bedarf switch { RufBedarf.Geher => lage.Geher, RufBedarf.Werkstatt => lage.Werkstatt, _ => 1f };
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
                // Ruf an einer echten Figur oder einem echten Betrieb: gibt es gerade keine, wartet die Art kurz (das Fenster schließt sich ohnehin)
                var ort = Ort(i, e.Lage);
                if (!frei || _seitLetztem < GlobalAbstand || (e.Lage.Gemessen && r.Bedarf != RufBedarf.Keiner && ort is null))
                {
                    _rest[i] = 2 + 6 * _zufall.NextDouble();
                    continue;
                }
                neu.Add(Erzeugen(i, fern, zoom, ort));
                _rest[i] = r.Mindest + Exponentiell(r.Mittel - r.Mindest);
            }
        }

        /// <summary>Einen Ruf dieser Art sofort (Entwicklerreiter, Hörprobe); hält die Abstände nicht ein.</summary>
        public Ruf Erzwingen(int art, bool fern, float zoom = 45f) => Erzeugen(art, fern, zoom, null);

        /// <summary>
        /// Der Ort eines Rufs: bei Schritten ein zufälliger Gänger im Umkreis, bei Hämmern eine arbeitende Werkstatt (zufällig, nähere öfter),
        /// sonst oder ohne Messung null (dann wie früher eine Zufallsseite). Gibt Seite (−1…+1) und Abstand zum Blickpunkt in Metern.
        /// </summary>
        private (float Seite, float Abstand, float Umkreis)? Ort(int art, KlangStadtlebenModell.Lage lage)
        {
            if (!lage.Gemessen) return null;
            var liste = Rufarten[art].Bedarf switch { RufBedarf.Geher => lage.GeherOrte, RufBedarf.Werkstatt => lage.WerkOrte, _ => null };
            if (liste is null || liste.Count == 0) return null;
            var umkreis = Rufarten[art].Bedarf == RufBedarf.Geher ? lage.GeherUmkreis : lage.WerkUmkreis;
            var summe = 0.0;
            foreach (var (_, d) in liste) summe += 0.2 + KlangStadtlebenModell.Nahe(d, umkreis);
            var wurf = _zufall.NextDouble() * summe;
            foreach (var (q, d) in liste)
            {
                wurf -= 0.2 + KlangStadtlebenModell.Nahe(d, umkreis);
                if (wurf <= 0) return (q.Seite, d, umkreis);
            }
            var letzte = liste[^1];
            return (letzte.Q.Seite, letzte.Abstand, umkreis);
        }

        private Ruf Erzeugen(int art, bool fern, float zoom, (float Seite, float Abstand, float Umkreis)? ort)
        {
            var r = Rufarten[art];
            var datei = fern && r.FernDatei is not null ? r.FernDatei : r.Datei;
            var anzahl = fern && r.FernDatei is not null ? r.FernVarianten : r.Varianten;
            var v = _zufall.Next(anzahl);
            if (anzahl > 1 && v == _letzteVariante[art]) v = (v + 1 + _zufall.Next(anzahl - 1)) % anzahl; // nie zweimal dieselbe hintereinander
            _letzteVariante[art] = v;
            var name = $"{datei}_{v + 1}";
            var grenze = fern ? 0.5f : 0.8f;
            var zufallSeite = (float)(_zufall.NextDouble() * 2 - 1) * grenze;
            // mit Ort: dort, wo die Figur im Bild ist (sanft zur Mitte gezogen, der Rand des Bildes wäre sonst ganz auf einer Seite)
            var seite = ort is { } o ? Math.Clamp(o.Seite, -1f, 1f) * grenze : zufallSeite;
            // nahe Rufe werden zum Startzoom hin leiser (bis −6 dB bei S = 100): man ist weiter weg, aber die Gasse lebt noch
            var db = (fern ? r.FernDb : r.NahDb - 6f * Glatt(40f, 100f, zoom)) + (float)(_zufall.NextDouble() * 6 - 3);
            // und mit dem Abstand der Figur zur Kamera: bis −8 dB am Rand des Umkreises
            if (ort is { } p) db -= 8f * Glatt(0.3f * p.Umkreis, p.Umkreis, p.Abstand);
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

        // Panner: die lautere Seite bleibt für jede Seite auf dem Pegel der Mitte (Godot hebt sie sonst um bis zu 6 dB an)
        foreach (var seite in new[] { -1f, -0.8f, -0.5f, -0.2f, 0f, 0.3f, 0.5f, 0.8f, 1f })
        {
            var spitze = PannerSpitzeDb(seite, AbspielDb(new Ruf(0, "", false, seite, -6f, 1f)));
            Pruefe(Math.Abs(spitze + 6f) <= 0.01f, $"Panner Seite {seite:0.0}: lautere Seite {spitze:0.00} dB statt −6 dB");
        }
        Pruefe(PannerSpitzeDb(0.8f, -6f) > -1.0f, "Nachrechnung des Panners stimmt nicht (ohne Ausgleich müsste Seite 0,8 etwa −0,9 dB ergeben)");
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
                if (a.Db[i] > AusDb) summe += Math.Pow(10, (a.Db[i] + Schichten[i].Bus switch { Bus.Stadt => a.StadtDb, Bus.Schenke => a.StadtDb + a.SchenkeDb, _ => 0f }) / 10.0);
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
                    var spitze = PannerSpitzeDb(r.Seite, AbspielDb(r));
                    Pruefe(spitze <= Math.Min(r.Db, 0f) + 0.01f, $"{lage}: {art.Name} auf Seite {r.Seite:0.00} nach dem Panner {spitze:0.0} dB statt höchstens {Math.Min(r.Db, 0f):0.0} dB");
                    letzte[r.Art] = t;
                    letzterIrgendein = t;
                }
            }
            log($"UMGEBUNGTEST Rufe {lage}: {zahl} in 30 min");
            if (lage == "Pause") Pruefe(zahl < 40, $"Pause: {zahl} Rufe in 30 min (zu viele)");
            if (lage == "Tempo 1") Pruefe(zahl > 30, $"Tempo 1: nur {zahl} Rufe in 30 min");
        }
        log("UMGEBUNGTEST Rufe je Art: " + string.Join(", ", Rufarten.Select((r, i) => $"{r.Name} {anzahl[i]}")));

        // 7. Die Menschen der Stadt (KlangStadtlebenModell): leere Straßen still, volle laut, weich, im Umkreis, Orte der Rufe
        Lebenstest(log, Pruefe);

        foreach (var f in fehler.Distinct().Take(30)) log("UMGEBUNGTEST FEHLER: " + f);
        log(fehler.Count == 0 ? "UMGEBUNGTEST OK" : $"UMGEBUNGTEST FEHLER: {fehler.Count} Abweichungen");
        return fehler.Count == 0;
    }

    // ── Selbstprüfung der Menschen (Teil von --umgebungtest=1) ──────────────────────────────────────────────────

    private static void Lebenstest(Action<string> log, Action<bool, string> pruefe)
    {
        int Index(string name) => Array.FindIndex(Schichten, s => s.Name == name);
        var tag = new Eingaben(12 * 60, 12, Jahreszeit.Fruehling, 40, 140, Innenart.Draussen, false, 1.27);
        KlangStadtlebenModell.Lage Lage(KlangStadtlebenModell.Momentbild m, float zoom, double sek = 20)
        {
            var z = new KlangStadtlebenModell.Zustand();
            for (var t = 0.0; t < sek; t += 1.0 / 30) z.Schritt(m, zoom, 1.0 / 30);
            return z.Lage;
        }
        KlangStadtlebenModell.Momentbild Szene(float stunde, float zoom)
        {
            var m = new KlangStadtlebenModell.Momentbild();
            KlangStadtlebenModell.Szenario.Fuellen(m, stunde, zoom);
            return m;
        }
        // Pegel (LUFS vor dem Regler) einer Lage zu einer Uhrzeit: Mischung aus Zoom 40
        double Lufs(float stunde, KlangStadtlebenModell.Lage l, float zoom = 40)
        {
            var e = tag with { UhrMinute = stunde * 60, Bezug = BezugFruehling(stunde), Zoom = zoom, Leben = l };
            var z = new Zustand();
            Ausgabe a = z.Aus;
            for (var t = 0.0; t < 8; t += 1.0 / 30) a = z.Schritt(e, 1.0 / 30);
            var summe = 0.0;
            for (var i = 0; i < Schichten.Length; i++)
                if (a.Db[i] > AusDb) summe += Math.Pow(10, (a.Db[i] + Schichten[i].Bus switch { Bus.Stadt => a.StadtDb, Bus.Schenke => a.StadtDb + a.SchenkeDb, _ => 0f }) / 10.0);
            return -26 + 10 * Math.Log10(Math.Max(summe, 1e-12));
        }
        float G(string name, float stunde, KlangStadtlebenModell.Lage l, float zoom = 40) =>
            Gewicht(Index(name), stunde, BezugFruehling(stunde), Naehe(zoom), Hoehe(zoom, 140), l);

        // a. Leere Straße: kein Gemurmel, kein Schenkengemurmel, Stadtleben nur noch der Grund
        var leer = Lage(new KlangStadtlebenModell.Momentbild { MarktOffen = true }, 40);
        pruefe(leer.Menge == 0f && leer.Markt == 0f && leer.Schenke == 0f && leer.Werkstatt == 0f && leer.Geher == 0f, "Leere Stadt: ein Pegel über 0");
        pruefe(G("stadt_gemurmel", 12f, leer) == 0f && G("stadt_abend", 12f, leer) == 0f, "Leere Stadt: Gemurmel oder Schenke");
        pruefe(G("stadt_tag", 12f, leer) <= StadtGrund + 1e-6f, "Leere Stadt: Stadtleben über dem Grund");
        // b. Volle Straße am Mittag: Stadtleben und Gemurmel da, Sättigung
        var voll = Lage(Szene(12f, 40), 40);
        pruefe(voll.Menge >= 0.8f && voll.Markt >= 0.6f, $"Mittag voll: Menge {voll.Menge:0.00}, Markt {voll.Markt:0.00} zu niedrig");
        pruefe(G("stadt_gemurmel", 12f, voll) >= 0.6f && G("stadt_tag", 12f, voll) >= 0.8f, "Mittag voll: Gemurmel oder Stadtleben zu leise");
        {
            var m = new KlangStadtlebenModell.Momentbild();
            for (var i = 0; i < 300; i++) m.Leute.Add(new KlangStadtlebenModell.Quelle(i % 30, i / 30, 0, 1, true));
            var viele = Lage(m, 40);
            pruefe(viele.Menge is > 0.99f and <= 1f && viele.Geher is > 0.99f and <= 1f, $"Sättigung: 300 Leute Menge {viele.Menge:0.000}");
            m.Leeren();
            m.Leute.Add(new KlangStadtlebenModell.Quelle(5, 5));
            pruefe(Lage(m, 40).Menge == 0f, "Ein einzelner Mensch murmelt");
        }
        log($"UMGEBUNGTEST Leben: leer Menge {leer.Menge:0.00}, Mittag voll Menge {voll.Menge:0.00} Markt {voll.Markt:0.00} (Leute {voll.LeuteImUmkreis}, Markt {voll.MarktleuteImUmkreis})");
        // c. Lautstärke: leere gegen volle Straße zur selben Zeit (Mittag, Zoom 40 und Startzoom) mindestens 8 dB
        foreach (var zoom in new[] { 40f, 95f })
        {
            var vollZ = Lage(Szene(12f, zoom), zoom);
            var leerZ = Lage(new KlangStadtlebenModell.Momentbild { MarktOffen = true }, zoom);
            double lv = Lufs(12f, vollZ, zoom), ll = Lufs(12f, leerZ, zoom);
            log($"UMGEBUNGTEST Leben Pegel Mittag Zoom {zoom:0}: voll {lv:0.0} LUFS, leer {ll:0.0} LUFS (Unterschied {lv - ll:0.0} dB)");
            pruefe(lv - ll >= 8.0, $"Mittag Zoom {zoom}: leer nur {lv - ll:0.0} dB leiser als voll (erwartet ≥ 8)");
        }
        // d. Umkreis hängt am Zoom: ein Mensch-Haufen in 80 m Abstand zählt bei Zoom 140 (Umkreis 123 m), nicht bei Zoom 25 (42 m)
        {
            var m = new KlangStadtlebenModell.Momentbild();
            for (var i = 0; i < 20; i++) m.Leute.Add(new KlangStadtlebenModell.Quelle(80 + i % 5, i / 5, 0, 1, true));
            float nah = Lage(m, 25).Menge, fern = Lage(m, 140).Menge;
            pruefe(nah == 0f && fern > 0.3f, $"Umkreis: bei Zoom 25 {nah:0.00}, bei Zoom 140 {fern:0.00} (erwartet 0 und > 0,3)");
            // hinter Häusern: die Rechnung kennt keine Sichtbarkeit, nur den Abstand (Seite außerhalb des Bildes ändert nichts)
            m.Leeren();
            for (var i = 0; i < 20; i++) m.Leute.Add(new KlangStadtlebenModell.Quelle(10 + i % 5, i / 5, 5f, 1, true));
            pruefe(Lage(m, 40).Menge > 0.5f, "Leute außerhalb des Bildes (Seite 5) zählen nicht");
        }
        // e. Markt nur bei offenem Markt
        {
            var m = new KlangStadtlebenModell.Momentbild { MarktOffen = false };
            for (var i = 0; i < 10; i++) m.Marktleute.Add(new KlangStadtlebenModell.Quelle(5 + i, 5));
            var zu = Lage(m, 40);
            m.MarktOffen = true;
            var offen = Lage(m, 40);
            pruefe(zu.Markt == 0f && offen.Markt > 0.7f, $"Markt zu {zu.Markt:0.00} / offen {offen.Markt:0.00}");
        }
        // f. Schänke: nur mit Gästen und in der Nähe, aus ihrer Richtung
        {
            var m = new KlangStadtlebenModell.Momentbild();
            m.Schenken.Add(new KlangStadtlebenModell.Quelle(-8, 3, -0.7f, 6));
            var da = Lage(m, 40);
            pruefe(da.Schenke >= 0.7f && da.SchenkeSeite < -0.6f, $"Schänke mit 6 Gästen: Pegel {da.Schenke:0.00}, Seite {da.SchenkeSeite:0.00}");
            m.Schenken.Clear();
            m.Schenken.Add(new KlangStadtlebenModell.Quelle(-20, 5, -0.7f, 0));
            pruefe(Lage(m, 40).Schenke == 0f, "Schänke ohne Gäste klingt");
            m.Schenken.Clear();
            m.Schenken.Add(new KlangStadtlebenModell.Quelle(200, 5, 1f, 8));
            pruefe(Lage(m, 40).Schenke == 0f, "Schänke in 200 m Entfernung klingt bei Zoom 40");
            var e = tag with { UhrMinute = 22 * 60 + 30, Bezug = 22, Leben = Lage(Szene(22.5f, 40), 40) };
            var a = new Zustand().Schritt(e, 1.0 / 30);
            pruefe(a.Db[Index("stadt_abend")] <= AusDb, "Nach Schankschluss (22 Uhr, keine Gäste) klingt die Schänke weiter");
            // Seite und Bus: Ausgleich des Panners
            var l2 = Lage(new KlangStadtlebenModell.Momentbild { Schenken = { new KlangStadtlebenModell.Quelle(-8, 3, -0.9f, 6) } }, 40);
            var a2 = new Zustand().Schritt(tag with { UhrMinute = 20 * 60, Bezug = 19.5f, Leben = l2 }, 1.0 / 30);
            pruefe(a2.SchenkeSeite < -0.6f && a2.SchenkeDb < -4f, $"Schenkenbus Seite {a2.SchenkeSeite:0.00}, Ausgleich {a2.SchenkeDb:0.0} dB");
        }
        // g. Weicher Anstieg und Abfall: kein Sprung im Pegel von einem Bild zum nächsten, 12 Minuten Szenario durchgehend
        {
            var z = new KlangStadtlebenModell.Zustand();
            var m = new KlangStadtlebenModell.Momentbild();
            float vorher = 0, groesst = 0;
            var dt = 1.0 / 30;
            var erstes = true;
            for (var t = 0.0; t < 200; t += dt)
            {
                var stunde = 5f + (float)t / 200f * 18f; // 5 bis 23 Uhr in 200 s (Hörprobe: 18 Stunden in gut 3 Minuten)
                KlangStadtlebenModell.Szenario.Fuellen(m, stunde, 40);
                var l = z.Schritt(m, 40, dt);
                if (!erstes) groesst = Math.Max(groesst, Math.Abs(l.Menge - vorher));
                vorher = l.Menge;
                erstes = false;
            }
            log($"UMGEBUNGTEST Leben: größte Änderung der Menge je Bild {groesst:0.0000} (erlaubt {1f - MathF.Exp(-(float)dt / KlangStadtlebenModell.AbfallSekunden):0.0000} bis {1f - MathF.Exp(-(float)dt / KlangStadtlebenModell.AnstiegSekunden):0.0000})");
            pruefe(groesst <= 1f - MathF.Exp(-(float)dt / KlangStadtlebenModell.AnstiegSekunden) + 1e-4f, $"Menge springt: {groesst:0.0000} je Bild");
        }
        // h. Rufe: ohne Gänger keine Schritte, ohne arbeitende Werkstatt kein Hämmern; mit ihnen am Ort der Figur, Seite stimmt
        {
            var passant = Array.FindIndex(Rufarten, r => r.Name == "Passant");
            var handwerk = Array.FindIndex(Rufarten, r => r.Name == "Handwerk");
            (int Passanten, int Handwerk, List<Ruf> Rufe) Lauf30(KlangStadtlebenModell.Momentbild m, float stunde = 12f)
            {
                var z = new Zustand();
                var lz = new KlangStadtlebenModell.Zustand();
                var p = new RufPlaner(11);
                var rufe = new List<Ruf>();
                var alle = new List<Ruf>();
                var dt = 1.0 / 30;
                var e0 = tag with { UhrMinute = stunde * 60, Bezug = BezugFruehling(stunde), Zoom = 40, TicksProSekunde = 0, Pausiert = true };
                for (var t = 0.0; t < 1800; t += dt)
                {
                    var lage = lz.Schritt(m, 40, dt);
                    var e = e0 with { Leben = lage };
                    var a = z.Schritt(e, dt);
                    rufe.Clear();
                    p.Schritt(e with { Pausiert = false }, a, dt, rufe);
                    alle.AddRange(rufe);
                }
                return (alle.Count(r => r.Art == passant), alle.Count(r => r.Art == handwerk), alle);
            }
            var leerLauf = Lauf30(new KlangStadtlebenModell.Momentbild { MarktOffen = true });
            pruefe(leerLauf.Passanten == 0 && leerLauf.Handwerk == 0, $"Leere Stadt: {leerLauf.Passanten} Schritte, {leerLauf.Handwerk} Hämmern in 30 min");
            var einer = new KlangStadtlebenModell.Momentbild();
            einer.Leute.Add(new KlangStadtlebenModell.Quelle(10, 5, -0.9f, 1, true));
            einer.Leute.Add(new KlangStadtlebenModell.Quelle(12, 6, -0.9f, 1, true));
            einer.Leute.Add(new KlangStadtlebenModell.Quelle(11, 7, -0.9f, 1, true));
            einer.Werkstaetten.Add(new KlangStadtlebenModell.Quelle(60, -20, 0.8f, 2));
            var mit = Lauf30(einer);
            log($"UMGEBUNGTEST Leben Rufe in 30 min: leer {leerLauf.Passanten} Schritte / {leerLauf.Handwerk} Hämmern, mit drei Gängern und einer Werkstatt {mit.Passanten} / {mit.Handwerk}");
            pruefe(mit.Passanten >= 20 && mit.Handwerk >= 8, $"Mit Gängern und Werkstatt: {mit.Passanten} Schritte, {mit.Handwerk} Hämmern (erwartet ≥ 20 und ≥ 8)");
            foreach (var r in mit.Rufe)
            {
                if (r.Art == passant) pruefe(Math.Abs(r.Seite - (-0.72f)) < 1e-4f, $"Schritte nicht am Ort der Figur: Seite {r.Seite:0.00} statt −0,72");
                if (r.Art == handwerk) pruefe(Math.Abs(r.Seite - 0.4f) < 1e-4f && r.Fern, $"Hämmern nicht am Ort der Werkstatt: Seite {r.Seite:0.00} statt 0,40, fern {r.Fern}");
            }
            // Gänger, die nicht gehen (Stehende), machen keine Schritte
            var steher = new KlangStadtlebenModell.Momentbild();
            for (var i = 0; i < 6; i++) steher.Leute.Add(new KlangStadtlebenModell.Quelle(10 + i, 5, 0, 1, false));
            var stehLauf = Lauf30(steher);
            pruefe(stehLauf.Passanten == 0, $"Stehende machen {stehLauf.Passanten} Schritte");
            // Hämmern nur 8–20 Uhr (Fenster) auch mit Werkstatt in den Daten
            var nachts = Lauf30(einer, 22f);
            pruefe(nachts.Handwerk == 0, $"Hämmern um 22 Uhr: {nachts.Handwerk}");
        }
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
