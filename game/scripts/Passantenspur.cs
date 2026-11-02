namespace Reichsgilde;

/// <summary>
/// Wo auf der Gasse eine Figur geht (seitlich von der Mitte des Weges), nur Rechnung ohne Godot, damit ein Test die Datei mitübersetzen kann
/// (<c>PassantenspurTests</c>). Die Darstellung stellt die Figur dann senkrecht zur Gehrichtung um dieses Maß nach rechts (Stadtansicht.Passanten.cs).
///
/// Drei Schichten (Nutzer, 1.11.2026):
///   1. <see cref="Grundspur"/>: jede Person hat ihre eigene Spur, fest aus der Personennummer (kein Zufall, jeder Lauf gleich), und zwar auf der
///      rechten Hälfte der Gasse (Rechtsverkehr).
///   2. <see cref="Schwankung"/>: um diese Spur pendelt sie sanft, ±<see cref="Schwanken"/> Meter, mit Wellenlängen von 3 bis 5 Metern längs der
///      zurückgelegten Strecke und einer Phase aus der Nummer.
///   3. <see cref="Seite"/>: am Anfang und Ende des Weges und beim Anschluss an einen laufenden Weg gleicht sich die Spur über
///      <see cref="Uebergang"/> Meter an, statt zu springen.
///
/// Die Breite des Weges kommt aus der Karte (Nutzer, 1.11.2026): <see cref="IMittellinie.Halbbreite"/> sagt, wie breit der Weg an dieser Stelle ist
/// (die Darstellung liest sie aus den Wegen der Karte, geglättet, damit die Spur vor einer engen Gasse schon schmaler wird). Das Grundmaß ist eine
/// Gasse von <see cref="Gassenbreite"/> = 2 m: Dort reicht die Spur von <see cref="Spurnah"/> bis <see cref="Spurfern"/> Meter rechts der Mitte, mit dem
/// Schwanken von 0,20 bis 0,75 m – die Mitte der Figur bleibt auf der rechten Hälfte, der Körper (Schulterbreite etwa 0,5 m) reicht höchstens bis zum
/// rechten Rand. Auf breiteren Wegen wächst der fernste Rand der Spur mit (<see cref="Spurbereich"/>), so verteilen sich die Leute über die ganze rechte
/// Hälfte, und zur Hauswand bleibt ein Abstand; auf schmaleren (Türwege) wird alles im Verhältnis enger. Ausweichen (Schicht 4,
/// <c>Passantenausweichen.cs</c>) und der Karrenausweichschritt (Schritt 5d) kommen in der Darstellung dazu, beide innerhalb der Wegbreite.
///
/// Weil die Figur seitlich versetzt geht (Übergang, Schwanken), geht sie auf dem Boden etwas weiter als die Mittellinie: <see cref="Bewegung"/> gibt die
/// wirkliche Richtung und das Verhältnis der beiden Wege (für den Takt der Schritte, damit die Füße nicht rutschen) an.
/// </summary>
public static class Passantenspur
{
    /// <summary>Grundmaß einer Gasse (Meter): Ohne Angabe der Breite (<see cref="IMittellinie.Halbbreite"/>) gilt sie, und die Spuren sind darauf abgestimmt.</summary>
    public const float Gassenbreite = 2.0f;

    /// <summary>Auf breiteren Wegen rückt der fernste Rand der Spur um diesen Anteil jedes zusätzlichen halben Meters weiter nach außen (der Rest bleibt Abstand zur Wand).</summary>
    public const float Breitenanteil = 0.8f;

    /// <summary>Die Figur bleibt mit ihrer Mitte mindestens so weit (Meter) vom Rand des Weges entfernt (halbe Schulterbreite und etwas Luft), auch beim Ausweichen.</summary>
    public const float Wandabstand = 0.35f;

    /// <summary>
    /// Wie weit rechts der Mitte die Spuren auf einem Weg mit dieser halben Breite (Meter) liegen dürfen: bei 1 m (Grundmaß) <see cref="Spurnah"/> bis
    /// <see cref="Spurfern"/>; breiter wächst nur der ferne Rand (um <see cref="Breitenanteil"/> der Mehrbreite), schmaler wird beides im Verhältnis kleiner.
    /// </summary>
    public static (float Nah, float Fern) Spurbereich(float halb)
    {
        if (halb >= Gassenbreite / 2) return (Spurnah, Spurfern + (halb - Gassenbreite / 2) * Breitenanteil);
        var f = Schwankanteil(halb);
        return (Spurnah * f, Spurfern * f);
    }

    /// <summary>
    /// Auf schmaleren Wegen als dem Grundmaß werden Spur und Schwanken mit diesem Anteil kleiner: so, dass der Körper (0,25 m) mit der fernsten Spur und
    /// dem vollen Schwanken gerade bis zum Rand reicht (im Grundmaß 1, nie unter 0,1).
    /// </summary>
    public static float Schwankanteil(float halb) => Math.Clamp((halb - 0.25f) / (Spurfern + Schwanken), 0.1f, 1f);

    /// <summary>
    /// Losgehen gestreut (Nutzer, 1.11.2026): Jede Person geht um diese festen Minuten (0 bis <see cref="LosgehenStreuung"/>, aus der Nummer) später los als der
    /// Kern – damit nicht alle zur vollen Stunde zugleich aus den Häusern kommen. Nur Darstellung, der Kern zählt sie wie bisher.
    /// </summary>
    public static double Losgehversatz(int nummer) => LosgehenStreuung * Bruchteil(nummer, 7);

    /// <summary>Höchster Versatz beim Losgehen (Spielminuten).</summary>
    public const double LosgehenStreuung = 45;

    /// <summary>Die Grundspur liegt zwischen so viel und so viel Meter rechts der Mitte (bei zwei Metern Gassenbreite die rechte Hälfte).</summary>
    public const float Spurnah = 0.35f, Spurfern = 0.60f;

    /// <summary>Wie weit (Meter) die Figur beim Gehen um ihre Spur pendelt, höchstens.</summary>
    public const float Schwanken = 0.15f;

    /// <summary>Länge (Meter) von Anfang und Ende des Weges, über die sich die Spur angleicht.</summary>
    public const float Uebergang = 1.0f;

    /// <summary>Wellenlängen (Meter) der beiden Schwingungen: die erste 3 bis 4 m, die zweite 4 bis 5 m.</summary>
    public const float WelleKurzMin = 3f, WelleKurzMax = 4f, WelleLangMin = 4f, WelleLangMax = 5f;

    /// <summary>
    /// Ein fester „Zufall“ (0 bis unter 1) aus einer Nummer und einem Schlüssel (Mischfunktion, kein Zufallsgenerator): Wellenlängen und Phasen
    /// hängen so nicht voneinander und nicht von der Grundspur ab, zwei Personen mit ähnlicher Spur schwanken also nicht im Gleichschritt.
    /// </summary>
    public static float Bruchteil(int nummer, int schluessel)
    {
        unchecked
        {
            var h = (uint)nummer * 0x9E3779B1u ^ (uint)(schluessel + 1) * 0x85EBCA6Bu;
            h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
            return (h >> 8) / 16777216f;
        }
    }

    /// <summary>
    /// Die eigene Spur dieser Person (Meter rechts der Mitte), fest nach der Nummer, von <see cref="Spurnah"/> bis <see cref="Spurfern"/>. Der Goldene Schnitt
    /// verteilt aufeinanderfolgende Nummern gleichmäßig über die Breite (Nachbarn im Haus liegen nicht auf derselben Spur).
    /// </summary>
    public static float Grundspur(int nummer) => GrundspurBei(nummer, Gassenbreite / 2);

    /// <summary>Die eigene Spur auf einem Weg mit dieser halben Breite: derselbe Anteil der Breite (<see cref="Spurbereich"/>), auf breiten Straßen also weiter außen.</summary>
    public static float GrundspurBei(int nummer, float halb)
    {
        var x = (nummer * 2L + 1) * 0.6180339887498949;
        var (nah, fern) = Spurbereich(halb);
        return nah + (fern - nah) * (float)(x - Math.Floor(x));
    }

    /// <summary>Die erste und zweite Wellenlänge dieser Person (Meter).</summary>
    public static (float Kurz, float Lang) Wellenlaengen(int nummer) =>
        (WelleKurzMin + (WelleKurzMax - WelleKurzMin) * Bruchteil(nummer, 1), WelleLangMin + (WelleLangMax - WelleLangMin) * Bruchteil(nummer, 2));

    /// <summary>
    /// Wie weit sie nach <paramref name="strecke"/> Metern auf dem Weg von ihrer Spur abweicht (−<see cref="Schwanken"/> bis +<see cref="Schwanken"/>):
    /// zwei Sinuswellen (Gewichte 0,6 und 0,4, zusammen höchstens 1) mit Phasen aus der Nummer.
    /// </summary>
    public static float Schwankung(int nummer, float strecke)
    {
        var (kurz, lang) = Wellenlaengen(nummer);
        var (phase1, phase2) = (Bruchteil(nummer, 3) * MathF.Tau, Bruchteil(nummer, 4) * MathF.Tau);
        return Schwanken * (0.6f * MathF.Sin(MathF.Tau * strecke / kurz + phase1) + 0.4f * MathF.Sin(MathF.Tau * strecke / lang + phase2));
    }

    /// <summary>Weiche Rampe von 0 auf 1 über <see cref="Uebergang"/> Meter (S-Kurve: am Anfang und am Ende ohne Knick).</summary>
    public static float Rampe(float meter, float laenge = Uebergang)
    {
        var t = Math.Clamp(meter / laenge, 0f, 1f);
        return t * t * (3 - 2 * t);
    }

    /// <summary>
    /// Der seitliche Abstand zur Mitte des Weges (Meter, nach rechts positiv) nach <paramref name="strecke"/> Metern, mit noch <paramref name="rest"/>
    /// Metern bis zum Ende. Am Anfang kommt sie von <paramref name="startseite"/> (0, wenn die Figur an einem Ort losgeht; ihr bisheriges Maß, wenn sie
    /// von einem laufenden Weg auf den neuen übergeht) und gleicht sich über <see cref="Uebergang"/> Meter an die Spur an, am Ende geht sie ebenso zurück
    /// auf die Mitte des Weges.
    /// </summary>
    public static float Seite(int nummer, float strecke, float rest, float startseite = 0) =>
        Versatz(nummer, strecke, rest, (startseite, 0), (1, 0)).X;

    /// <summary>
    /// Der seitliche Versatz als Vektor (x, z in Metern) nach <paramref name="strecke"/> Metern, mit noch <paramref name="rest"/> Metern bis zum Ende:
    /// <paramref name="rechts"/> ist die Rechtsrichtung der Figur (Länge 1), <paramref name="start"/> der Versatz, mit dem sie auf den Weg kommt (null, wenn sie
    /// an einem Ort losgeht; sonst ihr bisheriger Versatz als Vektor, also auch seine Richtung – so springt sie beim Anschluss nicht). Der Versatz gleitet
    /// über <see cref="Uebergang"/> Meter von <paramref name="start"/> auf die Spur (<see cref="Grundspur"/> plus <see cref="Schwankung"/> nach rechts)
    /// und am Ende zurück auf null.
    /// </summary>
    public static (float X, float Z) Versatz(int nummer, float strecke, float rest, (float X, float Z) start, (float X, float Z) rechts) =>
        Versatz(nummer, strecke, rest, start, rechts, Gassenbreite / 2);

    /// <summary>Die Spur nach so vielen Metern auf einem Weg mit dieser halben Breite (Meter rechts der Mitte, mit Schwanken).</summary>
    public static float Spur(int nummer, float strecke, float halb) => GrundspurBei(nummer, halb) + Schwankanteil(halb) * Schwankung(nummer, strecke);

    /// <summary>Wie <see cref="Versatz(int, float, float, (float X, float Z), (float X, float Z))"/>, auf einem Weg mit der halben Breite <paramref name="halb"/>.</summary>
    public static (float X, float Z) Versatz(int nummer, float strecke, float rest, (float X, float Z) start, (float X, float Z) rechts, float halb)
    {
        var ziel = Spur(nummer, strecke, halb);
        // Je weiter der alte Versatz vom Ziel weg ist (anderer Weg, andere Richtung), desto länger dauert der Übergang: so bleibt er überall gleich flach
        var (dx, dz) = (rechts.X * ziel - start.X, rechts.Z * ziel - start.Z);
        var laenge = Uebergang * MathF.Max(1, MathF.Sqrt(dx * dx + dz * dz) / (Spurfern + Schwanken));
        var an = Rampe(strecke, laenge);
        var aus = 1 - (1 - Rampe(rest)) * an; // am Anfang bleibt der alte Versatz auch auf einem ganz kurzen Weg, wie er ist; das Ausblenden greift erst, wenn die Spur erreicht ist
        return ((start.X * (1 - an) + rechts.X * ziel * an) * aus, (start.Z * (1 - an) + rechts.Z * ziel * an) * aus);
    }

    /// <summary>Die Mittellinie eines Weges: der Punkt (x, z) nach so vielen Metern Weg (die Darstellung liefert ihn aus dem Gassennetz, ein Test aus einer Punktfolge).</summary>
    public interface IMittellinie
    {
        (float X, float Z) Bei(float strecke);

        /// <summary>
        /// Die halbe Breite des Weges (Meter) nach so vielen Metern, so geglättet, dass sie sich höchstens um einen halben Meter je Meter Weg ändert
        /// (<see cref="Huellbreite"/>). Ohne Angabe das Grundmaß.
        /// </summary>
        float Halbbreite(float strecke) => Gassenbreite / 2;

        /// <summary>
        /// Die gerundete Mittellinie: der gewichtete Mittelwert der Linie von <paramref name="strecke"/> − <paramref name="fenster"/> bis + <paramref name="fenster"/>
        /// (in der Mitte am stärksten, zum Rand linear weniger) – null, wenn die Linie das nicht kann; dann gilt die Linie selbst und die Ecken rundet nur die
        /// Rechtsrichtung (wie bis Schicht 3). <see cref="Polylinie"/> kann es genau.
        /// </summary>
        (float X, float Z)? Gerundet(float strecke, float fenster) => null;
    }

    /// <summary>
    /// Eine Mittellinie aus Punkten (x, z) mit der halben Breite jedes Stücks (oder ohne: das Grundmaß), mit genauer gerundeter Linie
    /// (<see cref="IMittellinie.Gerundet"/>). Die Rundung ist ein Dreiecksmittel: Anders als beim gleichmäßigen Mittel ändert sich so auch die Krümmung
    /// der gerundeten Ecke stetig, und die Figur innen und außen wird an den Rändern des Fensters nicht ruckartig schneller oder langsamer. Gerechnet über
    /// die zweite Stammfunktion der Linie (stückweise kubisch, in doppelter Genauigkeit), also ohne Abtasten.
    /// </summary>
    public class Polylinie : IMittellinie
    {
        private readonly double[] _meter, _x, _z, _rx, _rz, _ax, _az, _bx, _bz;
        private readonly float[] _anfaenge, _halbe;
        private readonly float _schmalste;

        /// <summary>Länge der Linie (Meter, auf dem Boden).</summary>
        public float Laenge { get; }

        public Polylinie(IReadOnlyList<(float X, float Z)> punkte, IReadOnlyList<float>? halbe = null)
        {
            var n = Math.Max(punkte.Count, 1);
            (_meter, _x, _z, _rx, _rz, _ax, _az, _bx, _bz) = (new double[n], new double[n], new double[n], new double[n], new double[n], new double[n], new double[n], new double[n], new double[n]);
            for (var i = 0; i < punkte.Count; i++) (_x[i], _z[i]) = (punkte[i].X, punkte[i].Z);
            for (var i = 0; i + 1 < punkte.Count; i++)
            {
                var (dx, dz) = (_x[i + 1] - _x[i], _z[i + 1] - _z[i]);
                var l = Math.Sqrt(dx * dx + dz * dz);
                _meter[i + 1] = _meter[i] + l;
                (_rx[i], _rz[i]) = l > 1e-9 ? (dx / l, dz / l) : (0, 0);
                // Stammfunktionen am Anfang des nächsten Stücks: A = ∫ Linie, B = ∫ A
                _ax[i + 1] = _ax[i] + _x[i] * l + _rx[i] * l * l / 2;
                _az[i + 1] = _az[i] + _z[i] * l + _rz[i] * l * l / 2;
                _bx[i + 1] = _bx[i] + _ax[i] * l + _x[i] * l * l / 2 + _rx[i] * l * l * l / 6;
                _bz[i + 1] = _bz[i] + _az[i] * l + _z[i] * l * l / 2 + _rz[i] * l * l * l / 6;
            }
            Laenge = (float)_meter[n - 1];
            var breiten = halbe ?? Enumerable.Repeat(Gassenbreite / 2, Math.Max(punkte.Count - 1, 1)).ToArray();
            _schmalste = float.MaxValue;
            (_anfaenge, _halbe) = Stuecke(punkte.Count >= 2 ? punkte : new[] { punkte.Count > 0 ? punkte[0] : (0f, 0f), punkte.Count > 0 ? punkte[0] : (0f, 0f) }, breiten);
            foreach (var h in _halbe) _schmalste = MathF.Min(_schmalste, h);
        }

        /// <summary>Das Stück, in dem der Meter liegt (das letzte, das bei oder vor ihm beginnt; nie das letzte Punkt-„Stück“).</summary>
        private int Stueck(double s)
        {
            var (lo, hi) = (0, Math.Max(_meter.Length - 2, 0));
            while (lo < hi)
            {
                var m = (lo + hi + 1) / 2;
                if (_meter[m] <= s) lo = m; else hi = m - 1;
            }
            return lo;
        }

        public (float X, float Z) Bei(float strecke)
        {
            var s = Math.Clamp(strecke, 0, Laenge);
            var i = Stueck(s);
            var t = s - _meter[i];
            return ((float)(_x[i] + _rx[i] * t), (float)(_z[i] + _rz[i] * t));
        }

        public float Halbbreite(float strecke) => Huellbreite(_anfaenge, _halbe, Laenge, strecke, _schmalste);

        /// <summary>Die zweite Stammfunktion bei <paramref name="s"/> Metern.</summary>
        private (double X, double Z) Zweite(double s)
        {
            var i = Stueck(s);
            var t = s - _meter[i];
            return (_bx[i] + _ax[i] * t + _x[i] * t * t / 2 + _rx[i] * t * t * t / 6, _bz[i] + _az[i] * t + _z[i] * t * t / 2 + _rz[i] * t * t * t / 6);
        }

        public virtual (float X, float Z)? Gerundet(float strecke, float fenster)
        {
            var f = Math.Min(fenster, Math.Min(strecke, Laenge - strecke));
            if (f < 1e-3) return Bei(strecke);
            // Dreiecksmittel = zweite Differenz der zweiten Stammfunktion: (B(s + f) − 2 B(s) + B(s − f)) / f²
            var (vor, hier, zurueck) = (Zweite(strecke + f), Zweite(strecke), Zweite(strecke - f));
            return ((float)((vor.X - 2 * hier.X + zurueck.X) / (f * f)), (float)((vor.Z - 2 * hier.Z + zurueck.Z) / (f * f)));
        }
    }

    /// <summary>
    /// Die Stücke eines Pfades für <see cref="Huellbreite"/>: wo jedes beginnt (Meter) und seine halbe Breite, dazu an jeder Ecke (die Richtung ändert sich
    /// um mehr als <see cref="EckeAb"/> Grad) ein Stück der Länge null mit höchstens <see cref="Eckenhalb"/>. <paramref name="halbe"/> hat eine Breite je
    /// Stück (eins weniger als Punkte).
    /// </summary>
    public static (float[] Anfaenge, float[] Halbe) Stuecke(IReadOnlyList<(float X, float Z)> punkte, IReadOnlyList<float> halbe)
    {
        var (anfaenge, breiten) = (new List<float>(), new List<float>());
        var meter = 0f;
        var grenze = MathF.Cos(EckeAb * MathF.PI / 180);
        for (var i = 0; i + 1 < punkte.Count; i++)
        {
            var (dx, dz) = (punkte[i + 1].X - punkte[i].X, punkte[i + 1].Z - punkte[i].Z);
            var laenge = MathF.Sqrt(dx * dx + dz * dz);
            if (i > 0 && laenge > 1e-4f)
            {
                var (vx, vz) = (punkte[i].X - punkte[i - 1].X, punkte[i].Z - punkte[i - 1].Z);
                var vl = MathF.Sqrt(vx * vx + vz * vz);
                if (vl > 1e-4f && (vx * dx + vz * dz) / (vl * laenge) < grenze)
                {
                    // die Ecke als Stück von einem Meter (halb davor, halb danach): so hat die Breite dort keine Spitze, und die Figur rückt nicht
                    // erst zur Mitte und gleich wieder hinaus
                    anfaenge.Add(MathF.Max(meter - 0.5f, anfaenge.Count > 0 ? anfaenge[^1] : 0));
                    breiten.Add(MathF.Min(Eckenhalb, MathF.Min(halbe[i], halbe[i - 1])));
                    anfaenge.Add(MathF.Min(meter + 0.5f, meter + laenge));
                    breiten.Add(halbe[i]);
                    meter += laenge;
                    continue;
                }
            }
            anfaenge.Add(meter);
            breiten.Add(halbe[i]);
            meter += laenge;
        }
        if (anfaenge.Count == 0) { anfaenge.Add(0); breiten.Add(halbe.Count > 0 ? halbe[0] : Gassenbreite / 2); }
        return (anfaenge.ToArray(), breiten.ToArray());
    }

    /// <summary>
    /// An einer Ecke (die Richtung ändert sich um mehr als so viel Grad) gilt höchstens die halbe Breite <see cref="Eckenhalb"/>: Weit außen auf einer
    /// breiten Straße ginge die Figur sonst in einem großen Bogen um die Ecke (außen weit, innen fast auf der Stelle), und die gerundete Linie schnitte die
    /// Ecke so stark, dass sie innen den Häusern nahe käme. So rücken alle vor der Ecke etwas zur Mitte.
    /// </summary>
    public const float EckeAb = 35f, Eckenhalb = 1.25f;

    /// <summary>So schnell (Meter je Meter Weg) darf sich die geglättete halbe Breite ändern: Vor einer engen Gasse wird die Spur rechtzeitig schmaler.</summary>
    public const float BreiteSteigung = 0.5f;

    /// <summary>
    /// Die geglättete halbe Breite nach <paramref name="strecke"/> Metern auf einem Weg aus Stücken: <paramref name="anfaenge"/> (aufsteigend, Meter ab
    /// Wegbeginn, das erste 0) und ihre halben Breiten <paramref name="halbe"/>. Ergebnis ist das Kleinste aus „Breite eines Stücks plus
    /// <see cref="BreiteSteigung"/> mal Abstand dorthin“: nie breiter als das Stück, auf dem die Figur ist, stetig, und eine enge Stelle macht die Spur
    /// schon davor schmal. Durchsucht nur die Stücke in Reichweite.
    /// </summary>
    public static float Huellbreite(IReadOnlyList<float> anfaenge, IReadOnlyList<float> halbe, float laenge, float strecke, float? schmalste = null)
    {
        var n = halbe.Count;
        if (n == 0) return Gassenbreite / 2;
        var s = Math.Clamp(strecke, 0, laenge);
        var (lo, hi) = (0, n - 1);
        while (lo < hi) // das letzte Stück, das bei oder vor s beginnt
        {
            var mitte = (lo + hi + 1) / 2;
            if (anfaenge[mitte] <= s) lo = mitte; else hi = mitte - 1;
        }
        var beste = halbe[lo];
        var kleinste = schmalste ?? beste; // die schmalste Stelle des ganzen Weges (ohne Angabe hier gesucht)
        if (schmalste is null) for (var j = 0; j < n; j++) kleinste = MathF.Min(kleinste, halbe[j]);
        for (var j = lo + 1; j < n; j++)
        {
            var abstand = anfaenge[j] - s;
            if (kleinste + BreiteSteigung * abstand >= beste) break;
            beste = MathF.Min(beste, halbe[j] + BreiteSteigung * abstand);
        }
        for (var j = lo - 1; j >= 0; j--)
        {
            var abstand = s - (j + 1 < n ? anfaenge[j + 1] : laenge);
            if (kleinste + BreiteSteigung * abstand >= beste) break;
            beste = MathF.Min(beste, halbe[j] + BreiteSteigung * abstand);
        }
        return beste;
    }

    /// <summary>Wo die Figur ist und wohin sie geht: <see cref="Ort"/> (Mittellinie plus Versatz), der <see cref="Versatz"/> und die <see cref="Rechts"/>richtung dazu, die <see cref="Bewegung"/> (Weg auf dem Boden je Meter Mittellinie als Vektor; null, wenn nicht verlangt) und die geglättete halbe Breite des Weges dort (<see cref="Halb"/>).</summary>
    public readonly record struct Gang((float X, float Z) Ort, (float X, float Z) Versatz, (float X, float Z) Rechts, (float X, float Z) Bewegung, float Halb = Gassenbreite / 2);

    /// <summary>Wie weit (Meter) vor und zurück der Weg auf dem Boden betrachtet wird, um die Bewegung zu messen.</summary>
    public const float Schrittweite = 0.1f;

    /// <summary>Wie weit (Meter) vor und zurück die Mittellinie betrachtet wird, um die Rechtsrichtung zu finden (Ecken werden über etwa einen Meter gerundet).</summary>
    public const float Fenster = 0.5f;

    /// <summary>
    /// Die Figur nach <paramref name="strecke"/> Metern auf einem Weg der Länge <paramref name="laenge"/>. Mit <paramref name="mitBewegung"/> gehört
    /// dazu, wohin sie auf dem Boden wirklich geht (aus ihrer Lage kurz davor und kurz danach (je 0,1 m), also auch an Ecken und im Übergang): Die Darstellung dreht die
    /// Figur in diese Richtung (kein seitliches Rutschen) und lässt die Schritte um die Länge dieser Bewegung schneller laufen. Auf gerader Strecke im Gehen
    /// ist sie höchstens 1,04, in den Übergängen bis etwa 1,5; beim Anschluss mit seitlichem Versatz oder an einer Ecke kann sie auch weit darüber oder
    /// darunter liegen (die Darstellung begrenzt sie).
    /// </summary>
    public static Gang Gehen(IMittellinie mitte, int nummer, float laenge, float strecke, (float X, float Z) start, bool mitBewegung)
    {
        var jetzt = Punkt(mitte, nummer, laenge, strecke, start);
        if (!mitBewegung) return jetzt;
        var (s0, s1) = (MathF.Max(0, strecke - Schrittweite), MathF.Min(laenge, strecke + Schrittweite));
        if (s1 - s0 < 1e-3f) return jetzt;
        var (davor, danach) = (Punkt(mitte, nummer, laenge, s0, start).Ort, Punkt(mitte, nummer, laenge, s1, start).Ort);
        return jetzt with { Bewegung = ((danach.X - davor.X) / (s1 - s0), (danach.Z - davor.Z) / (s1 - s0)) };
    }

    /// <summary>
    /// Abstand (Meter Mittellinie) der Stützpunkte in <see cref="Bodenmeter"/> für lange Wege (<see cref="BodenschrittFuer"/>): Die Tabelle eines Weges von 400 m kostete sonst zur vollen Stunde einige Millisekunden je Figur (Profil Köln, 1.11.2026).
    /// </summary>
    public const float BodenschrittLang = 0.25f, LangerWeg = 60f, BodenschrittSehrLang = 0.5f, SehrLangerWeg = 150f;

    /// <summary>Der Abstand der Stützpunkte für einen Weg dieser Länge (Meter): bis 60 m <see cref="Bodenschritt"/>, bis 150 m 0,25 m, darüber 0,5 m.</summary>
    public static float BodenschrittFuer(float laenge) => laenge > SehrLangerWeg ? BodenschrittSehrLang : laenge > LangerWeg ? BodenschrittLang : Bodenschritt;

    /// <summary>Abstand (Meter Mittellinie) der Stützpunkte in <see cref="Bodenmeter"/>.</summary>
    public const float Bodenschritt = 0.1f;

    /// <summary>
    /// Wie weit die Figur auf dem Boden gegangen ist, wenn sie auf der Mittellinie 0, <see cref="Bodenschritt"/>, 2 · <see cref="Bodenschritt"/> … und am
    /// Ende <paramref name="laenge"/> Meter weit ist (ihre Spur samt Übergängen und gerundeten Ecken, ohne Ausweichen). Die Darstellung plant den Gang nach
    /// diesem Weg und setzt die Figur über <see cref="StreckeZuBoden"/>: So geht jede in ihrem Tempo, auch an Ecken – innen läge sonst kaum Weg auf einem
    /// Meter Mittellinie (sie bliebe fast stehen), außen viel (sie hetzte).
    /// </summary>
    public static float[] Bodenmeter(IMittellinie mitte, int nummer, float laenge, (float X, float Z) start, float schritt = Bodenschritt)
    {
        var tabelle = new float[BodenmeterAnzahl(laenge, schritt) + 1];
        BodenmeterFuellen(tabelle, 1, tabelle.Length - 1, mitte, nummer, laenge, start, schritt);
        return tabelle;
    }

    /// <summary>Wie viele Stücke die Tabelle der <see cref="Bodenmeter"/> eines Weges dieser Länge hat (die Tabelle hat eins mehr).</summary>
    public static int BodenmeterAnzahl(float laenge, float schritt = Bodenschritt) => Math.Max(1, (int)MathF.Ceiling(laenge / schritt - 1e-4f));

    /// <summary>
    /// Füllt die Einträge <paramref name="von"/> bis einschließlich <paramref name="bis"/> einer Tabelle der <see cref="Bodenmeter"/> (die davor müssen schon
    /// stehen; Eintrag 0 ist 0). So lässt sich eine lange Tabelle über mehrere Bilder verteilen – mit genau demselben Ergebnis wie in einem Zug.
    /// </summary>
    public static void BodenmeterFuellen(float[] tabelle, int von, int bis, IMittellinie mitte, int nummer, float laenge, (float X, float Z) start, float schritt = Bodenschritt)
    {
        if (von < 1 || von > bis) return;
        var davor = Gehen(mitte, nummer, laenge, MathF.Min((von - 1) * schritt, laenge), start, false).Ort;
        for (var k = von; k <= bis; k++)
        {
            var ort = Gehen(mitte, nummer, laenge, MathF.Min(k * schritt, laenge), start, false).Ort;
            tabelle[k] = tabelle[k - 1] + MathF.Sqrt((ort.X - davor.X) * (ort.X - davor.X) + (ort.Z - davor.Z) * (ort.Z - davor.Z));
            davor = ort;
        }
    }

    /// <summary>Die Meter auf der Mittellinie, bei denen die Figur <paramref name="boden"/> Meter auf dem Boden gegangen ist (Umkehrung von <see cref="Bodenmeter"/>, dazwischen gerade).</summary>
    public static float StreckeZuBoden(float[] tabelle, float laenge, float boden, float schritt = Bodenschritt)
    {
        if (tabelle.Length < 2 || boden <= 0) return 0;
        if (boden >= tabelle[^1]) return laenge;
        var (lo, hi) = (0, tabelle.Length - 1);
        while (hi - lo > 1)
        {
            var mitte = (lo + hi) / 2;
            if (tabelle[mitte] <= boden) lo = mitte; else hi = mitte;
        }
        var (s0, s1) = (lo * schritt, MathF.Min(hi * schritt, laenge));
        var stueck = tabelle[hi] - tabelle[lo];
        return stueck > 1e-6f ? s0 + (s1 - s0) * (boden - tabelle[lo]) / stueck : s0;
    }

    /// <summary>
    /// Das Fenster für die Rundung der Ecken wächst mit dem Versatz (dieses Vielfache): Innen darf der Versatz nicht größer sein als der Radius der
    /// gerundeten Mittellinie, sonst bliebe die Figur in der Ecke stehen.
    /// </summary>
    public const float Eckenrundung = 3f;

    /// <summary>So weit (Meter) vor und zurück wird die gerundete Mittellinie betrachtet, um ihre Richtung zu finden.</summary>
    private const float Richtungsschritt = 0.25f;

    private static Gang Punkt(IMittellinie mitte, int nummer, float laenge, float strecke, (float X, float Z) start)
    {
        var halb = mitte.Halbbreite(strecke);
        // Weit außen (breite Straße) wird die Ecke über ein längeres Stück gerundet: innen bliebe die Figur sonst in der Ecke stehen oder ginge rückwärts
        // (nach der Spur ohne Schwanken: ein Fenster, das mit dem Schwanken wüchse, verschöbe die gerundete Linie an der Ecke vor und zurück)
        var fenster = MathF.Max(Fenster, Eckenrundung * (GrundspurBei(nummer, halb) + Schwanken * Schwankanteil(halb)));
        // Die Figur folgt der gerundeten Mittellinie (Mittel über das Fenster, an den Enden kürzer: dort genau der Anfangs- und Endpunkt), und die
        // Rechtsrichtung ist deren Richtung (aus zwei Punkten der gerundeten Linie kurz davor und danach) – so passt beides zusammen, und auch weit
        // innen gibt es einen Bogen statt eines Knicks
        (float X, float Z) hier, zurueck, vor;
        if (mitte.Gerundet(strecke, fenster) is { } gerundet)
        {
            var (s0, s1) = (MathF.Max(0, strecke - Richtungsschritt), MathF.Min(laenge, strecke + Richtungsschritt));
            (hier, zurueck, vor) = (gerundet, mitte.Gerundet(s0, fenster)!.Value, mitte.Gerundet(s1, fenster)!.Value);
        }
        else // eine Linie ohne Rundung (wie bis Schicht 3): die Linie selbst, die Rechtsrichtung über einen Meter um die Figur
            (hier, zurueck, vor) = (mitte.Bei(strecke), mitte.Bei(MathF.Max(0, strecke - Fenster)), mitte.Bei(MathF.Min(laenge, strecke + Fenster)));
        var weiter = mitte.Bei(MathF.Min(laenge, strecke + Fenster));
        var rechts = Rechts(zurueck, vor, (weiter.X - hier.X, weiter.Z - hier.Z));
        var versatz = Versatz(nummer, strecke, laenge - strecke, start, rechts, halb);
        return new Gang((hier.X + versatz.X, hier.Z + versatz.Z), versatz, rechts, (0, 0), halb);
    }

    /// <summary>
    /// Die Richtung, senkrecht zu der die Figur versetzt wird, aus den Punkten ein Stück vor und hinter ihr (also über Ecken geglättet) – ohne Godot als
    /// ebene Richtung (x, z). Die Rechtsrichtung ist (−z, x). Ist die Strecke zu kurz oder kehrt sie um (Summe null), gilt <paramref name="rueckfall"/>.
    /// </summary>
    public static (float X, float Z) Rechts((float X, float Z) zurueck, (float X, float Z) vor, (float X, float Z) rueckfall)
    {
        var (dx, dz) = (vor.X - zurueck.X, vor.Z - zurueck.Z);
        var laenge = MathF.Sqrt(dx * dx + dz * dz);
        if (laenge < 0.05f) (dx, dz, laenge) = (rueckfall.X, rueckfall.Z, MathF.Sqrt(rueckfall.X * rueckfall.X + rueckfall.Z * rueckfall.Z));
        if (laenge < 1e-6f) return (0, 0);
        return (-dz / laenge, dx / laenge);
    }
}
