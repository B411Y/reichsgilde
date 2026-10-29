namespace Reichsgilde.Core;

/// <summary>
/// Deterministische Mathematik: Pow, Exp, Log, Sin, Cos und Atan2, die auf jedem Rechner bitgenau dasselbe ergeben.
///
/// Das Problem: <c>Math.Pow</c>, <c>Math.Exp</c>, <c>Math.Sin</c> … rufen die C-Mathebibliothek des Betriebssystems auf. Windows und Linux
/// rechnen dort verschieden und liefern manchmal im letzten Bit ein anderes Ergebnis. Für eine Partie allein ist das egal. Im Mehrspieler
/// (Lockstep) rechnet aber jeder Rechner die Simulation selbst, nur die Befehle gehen übers Netz – und schon ein anderes Bit in einem Preis
/// kann eine Entscheidung kippen. Dann laufen die Partien auseinander.
///
/// Der Trick: Diese Klasse rechnet alles selbst, nur mit + − × ÷. Diese Rechenarten schreibt die Norm IEEE 754 exakt vor (korrekt gerundet),
/// deshalb ergeben sie auf jedem x64-Rechner dasselbe Bit. Dazu kommen <c>Math.Floor</c> und <c>Math.Abs</c> (ebenfalls exakt) sowie Ganzzahl-
/// und Bitoperationen (Exponent einer Kommazahl auslesen und setzen); eine Wurzel braucht keine der Formeln. Die Formeln (Bereich verkleinern, dann ein Polynom) stammen aus fdlibm, der freien
/// Mathebibliothek von Sun, die auch Java als <c>StrictMath</c> nutzt. Genauigkeit: Exp, Log, Sin, Cos und Atan2 etwa 1 Einheit der letzten
/// Stelle (rund 1e-16 relativ); Pow rechnet über Exp(y·Log x) und verliert dabei etwas, in den genutzten Bereichen bleibt es unter 1e-14.
///
/// Regel (CLAUDE.md, „Mehrspieler-Tauglichkeit“): Im Kern nie <c>Math.Pow/Exp/Log/Sin/Cos/Tan/Atan/Atan2</c>, immer <see cref="DetMathe"/>;
/// <c>Math.Pow(x, 2)</c> wird zu <c>x * x</c>. Floor, Round, Abs, Min, Max, Clamp und Sqrt sind exakt und dürfen bleiben.
/// Ein Test (DetMatheTests) sucht verbotene Aufrufe im Quelltext des Kerns.
/// </summary>
/// <remarks>
/// Die Konstanten und Polynome folgen fdlibm 5.3 (netlib.org/fdlibm). Die Hinweise der Vorlagen, wie sie dort stehen:
///
/// e_exp.c (1.6 04/04/22): Copyright (C) 2004 by Sun Microsystems, Inc. All rights reserved.
/// Permission to use, copy, modify, and distribute this software is freely granted, provided that this notice is preserved.
///
/// e_log.c, k_sin.c, k_cos.c, e_rem_pio2.c, s_atan.c, e_atan2.c (1.3 bzw. 1.4 95/01/18): Copyright (C) 1993 by Sun Microsystems, Inc.
/// All rights reserved. Developed at SunSoft, a Sun Microsystems, Inc. business. Permission to use, copy, modify, and distribute this
/// software is freely granted, provided that this notice is preserved.
/// </remarks>
public static class DetMathe
{
    // ── Hilfen: die Bits einer Kommazahl ──────────────────────────────────────
    // Eine double besteht aus 64 Bits: 1 Vorzeichen, 11 Exponent, 52 Mantisse. fdlibm arbeitet mit dem „hohen Wort“ (die oberen 32 Bits:
    // Vorzeichen, Exponent und der Anfang der Mantisse) – daran sieht man schnell, wie groß eine Zahl ungefähr ist.

    private static long Bits(double x) => BitConverter.DoubleToInt64Bits(x);
    private static double AusBits(long bits) => BitConverter.Int64BitsToDouble(bits);
    private static int HohesWort(double x) => (int)(Bits(x) >> 32);
    private static uint TiefesWort(double x) => (uint)Bits(x);
    private static double MitHohemWort(double x, int hoch) => AusBits(((long)hoch << 32) | TiefesWort(x));

    // ln 2 in zwei Teilen: Der hohe Teil hat hinten Nullen, damit k · Ln2Hoch für ganze k bis 2^11 exakt bleibt.
    private const double Ln2Hoch = 6.93147180369123816490e-01; // 0x3FE62E42 FEE00000
    private const double Ln2Tief = 1.90821492927058770002e-10; // 0x3DEA39EF 35793C76

    // ═════════════════════════════════════════════════════════════════════════
    //  Exp
    // ═════════════════════════════════════════════════════════════════════════

    private const double KehrwertLn2 = 1.44269504088896338700e+00;
    private const double ExpUeberlauf = 7.09782712893383973096e+02;  // darüber: unendlich
    private const double ExpUnterlauf = -7.45133219101941108420e+02; // darunter: 0
    private const double ZweiHochMinus1000 = 9.33263618503218878990e-302;
    private const double P1 = 1.66666666666666019037e-01, P2 = -2.77777777770155933842e-03, P3 = 6.61375632143793436117e-05,
        P4 = -1.65339022054652515390e-06, P5 = 4.13813679705723846039e-08;

    /// <summary>e hoch <paramref name="x"/> – wie <see cref="Math.Exp"/>, aber auf jedem Rechner bitgleich.</summary>
    public static double Exp(double x)
    {
        if (double.IsNaN(x)) return x;
        if (x > ExpUeberlauf) return double.PositiveInfinity;
        if (x < ExpUnterlauf) return 0;
        if (Math.Abs(x) < 3.7252902984619141e-09) return 1 + x; // |x| < 2^-28: e^x = 1 + x auf alle Stellen

        // 1. Bereich verkleinern: x = k·ln2 + r mit |r| ≤ ln2/2. Dann ist e^x = 2^k · e^r, und 2^k ist nur ein anderer Exponent.
        var k = (int)(KehrwertLn2 * x + (x < 0 ? -0.5 : 0.5)); // runden (zur Null hin abschneiden nach ±0,5)
        double t = k;
        var hoch = x - t * Ln2Hoch; // exakt
        var tief = t * Ln2Tief;
        var r = hoch - tief;

        // 2. e^r über ein Polynom (Remez-Näherung von fdlibm): e^r = 1 + r + r·c/(2 − c)
        var rr = r * r;
        var c = r - rr * (P1 + rr * (P2 + rr * (P3 + rr * (P4 + rr * P5))));
        var y = 1 - ((tief - r * c / (2 - c)) - hoch);

        // 3. Mit 2^k malnehmen: k auf den Exponenten addieren (exakt). Sehr kleine Ergebnisse in zwei Schritten (unter 2^-1022).
        if (k >= -1021) return AusBits(Bits(y) + ((long)k << 52));
        return AusBits(Bits(y) + ((long)(k + 1000) << 52)) * ZweiHochMinus1000;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Log
    // ═════════════════════════════════════════════════════════════════════════

    private const double Zwei54 = 1.80143985094819840000e+16; // 2^54
    private const double Lg1 = 6.666666666666735130e-01, Lg2 = 3.999999999940941908e-01, Lg3 = 2.857142874366239149e-01,
        Lg4 = 2.222219843214978396e-01, Lg5 = 1.818357216161805012e-01, Lg6 = 1.531383769920937332e-01, Lg7 = 1.479819860511658591e-01;

    /// <summary>Natürlicher Logarithmus – wie <see cref="Math.Log(double)"/>, aber auf jedem Rechner bitgleich.</summary>
    public static double Log(double x)
    {
        var hx = HohesWort(x);
        var lx = TiefesWort(x);
        var k = 0;
        if (hx < 0x00100000) // x < 2^-1022 – auch alle negativen Zahlen (dann ist das hohe Wort negativ)
        {
            if (((hx & 0x7fffffff) | (int)lx) == 0) return double.NegativeInfinity; // Log(±0)
            if (hx < 0) return double.NaN;                                         // Log(negativ)
            k -= 54; x *= Zwei54; hx = HohesWort(x);                               // sehr kleine Zahl: erst hochskalieren
        }
        if (hx >= 0x7ff00000) return x + x; // unendlich oder NaN

        // 1. x = 2^k · (1 + f) mit √2/2 < 1 + f < √2
        k += (hx >> 20) - 1023;
        hx &= 0x000fffff;
        var i = (hx + 0x95f64) & 0x100000;
        x = MitHohemWort(x, hx | (i ^ 0x3ff00000)); // auf 1 + f bzw. (1 + f)/2 setzen
        k += i >> 20;
        var f = x - 1.0;
        double dk = k;

        if ((0x000fffff & (2 + hx)) < 3) // |f| < 2^-20: kurze Reihe genügt
        {
            if (f == 0) return k == 0 ? 0 : dk * Ln2Hoch + dk * Ln2Tief;
            var r0 = f * f * (0.5 - 0.33333333333333333 * f);
            return k == 0 ? f - r0 : dk * Ln2Hoch - ((r0 - dk * Ln2Tief) - f);
        }

        // 2. log(1 + f) = 2s + 2s³/3 + … mit s = f/(2 + f), als Polynom (Remez-Näherung von fdlibm)
        var s = f / (2.0 + f);
        var z = s * s;
        i = hx - 0x6147a;
        var w = z * z;
        var j = 0x6b851 - hx;
        var t1 = w * (Lg2 + w * (Lg4 + w * Lg6));
        var t2 = z * (Lg1 + w * (Lg3 + w * (Lg5 + w * Lg7)));
        i |= j;
        var r = t2 + t1;
        if (i > 0)
        {
            var hfsq = 0.5 * f * f;
            return k == 0 ? f - (hfsq - s * (hfsq + r)) : dk * Ln2Hoch - ((hfsq - (s * (hfsq + r) + dk * Ln2Tief)) - f);
        }
        return k == 0 ? f - s * (f - r) : dk * Ln2Hoch - ((s * (f - r) - dk * Ln2Tief) - f);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Pow
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Bis zu dieser ganzen Hochzahl wird schrittweise multipliziert (exakt, wo es geht: 0,5³ = 0,125, 10³ = 1000).</summary>
    private const double GanzeHochzahlBis = 32;

    /// <summary>
    /// <paramref name="x"/> hoch <paramref name="y"/> – wie <see cref="Math.Pow"/> (auch bei 0, unendlich, NaN und negativen Zahlen), aber auf
    /// jedem Rechner bitgleich. Ganze Hochzahlen bis 32 werden multipliziert (<c>Pow(x, 2)</c> ist genau <c>x * x</c>), sonst
    /// <c>Exp(y · Log x)</c>.
    /// </summary>
    public static double Pow(double x, double y)
    {
        // Der häufige Fall zuerst (jeder Preis: Nachfrage/Angebot hoch Elastizität): x positiv und endlich, y keine ganze Zahl
        var ganz = y == Math.Floor(y); // true auch für ±unendlich, false für NaN
        if (!ganz && x > 0 && x < double.PositiveInfinity && !double.IsNaN(y))
            return Exp(y * Log(x));

        if (y == 0 || x == 1) return 1; // auch Pow(NaN, 0) und Pow(1, NaN) – wie Math.Pow
        if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
        if (ganz && Math.Abs(y) <= GanzeHochzahlBis)
            return GanzePotenz(x, (int)y);

        if (double.IsInfinity(y))
        {
            var betrag = Math.Abs(x);
            if (betrag == 1) return 1; // (−1)^±∞
            return (betrag < 1) == (y > 0) ? 0 : double.PositiveInfinity;
        }

        // Ab hier ist y endlich und keine kleine ganze Zahl.
        var ungerade = ganz && Math.Abs(y) < 9007199254740992.0 && Math.Floor(y / 2) * 2 != y; // nur für negatives x wichtig
        if (x == 0 || double.IsInfinity(x))
        {
            // 0^y und ∞^y: 0 oder unendlich; bei −0 und −∞ mit ungerader Hochzahl negativ
            var gross = (x == 0) == (y < 0);
            var negativ = ungerade && (x < 0 || (x == 0 && double.IsNegative(x)));
            return gross ? (negativ ? double.NegativeInfinity : double.PositiveInfinity) : (negativ ? -0.0 : 0.0);
        }
        if (x < 0)
        {
            if (!ganz) return double.NaN; // z. B. (−8)^(1/3) – wie Math.Pow
            var p = Exp(y * Log(-x));
            return ungerade ? -p : p;
        }
        return Exp(y * Log(x));
    }

    /// <summary>x^n für ganze n durch fortgesetztes Quadrieren (x^13 = x^8 · x^4 · x); negatives n: 1 / x^|n|.</summary>
    private static double GanzePotenz(double x, int n)
    {
        var rest = n < 0 ? -n : n;
        double ergebnis = 1, faktor = x;
        var erstes = true;
        while (rest > 0)
        {
            if ((rest & 1) != 0)
            {
                ergebnis = erstes ? faktor : ergebnis * faktor; // die erste Multiplikation mit 1 sparen (behält das Vorzeichen von −0)
                erstes = false;
            }
            rest >>= 1;
            if (rest > 0) faktor *= faktor;
        }
        return n < 0 ? 1 / ergebnis : ergebnis;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Sin und Cos
    // ═════════════════════════════════════════════════════════════════════════

    private const double ZweiDurchPi = 6.36619772367581382433e-01; // 2/π
    // π/2 in Teilen zu je 33 Bit: n · PiHalbe1 ist für ganze n bis 2^20 exakt.
    private const double PiHalbe1 = 1.57079632673412561417e+00, PiHalbe1t = 6.07710050650619224932e-11;
    private const double PiHalbe2 = 6.07710050630396597660e-11, PiHalbe2t = 2.02226624879595063154e-21;
    private const double PiHalbe3 = 2.02226624871116645580e-21, PiHalbe3t = 8.47842766036889956997e-32;

    private const double S1 = -1.66666666666666324348e-01, S2 = 8.33333333332248946124e-03, S3 = -1.98412698298579493134e-04,
        S4 = 2.75573137070700676789e-06, S5 = -2.50507602534068634195e-08, S6 = 1.58969099521155010221e-10;
    private const double C1 = 4.16666666666666019037e-02, C2 = -1.38888888888741095749e-03, C3 = 2.48015872894767294178e-05,
        C4 = -2.75573143513906633035e-07, C5 = 2.08757232129817482790e-09, C6 = -1.13596475577881948265e-11;

    /// <summary>Sinus (Bogenmaß) – wie <see cref="Math.Sin"/>, aber auf jedem Rechner bitgleich.</summary>
    public static double Sin(double x)
    {
        var ix = HohesWort(x) & 0x7fffffff;
        if (ix < 0x3e400000) return x;                      // |x| < 2^-27: sin(x) = x auf alle Stellen (auch −0)
        if (ix <= 0x3fe921fb) return KernSin(x, 0, false); // |x| ≤ π/4
        if (ix >= 0x7ff00000) return double.NaN;            // unendlich oder NaN
        var n = UmPiHalbeVerkleinern(x, out var r, out var rTief);
        return (n & 3) switch
        {
            0 => KernSin(r, rTief, true),
            1 => KernCos(r, rTief),
            2 => -KernSin(r, rTief, true),
            _ => -KernCos(r, rTief),
        };
    }

    /// <summary>Kosinus (Bogenmaß) – wie <see cref="Math.Cos"/>, aber auf jedem Rechner bitgleich.</summary>
    public static double Cos(double x)
    {
        var ix = HohesWort(x) & 0x7fffffff;
        if (ix < 0x3e400000) return 1;              // |x| < 2^-27: cos(x) = 1 auf alle Stellen
        if (ix <= 0x3fe921fb) return KernCos(x, 0); // |x| ≤ π/4
        if (ix >= 0x7ff00000) return double.NaN;
        var n = UmPiHalbeVerkleinern(x, out var r, out var rTief);
        return (n & 3) switch
        {
            0 => KernCos(r, rTief),
            1 => -KernSin(r, rTief, true),
            2 => -KernCos(r, rTief),
            _ => KernSin(r, rTief, true),
        };
    }

    /// <summary>Bis hier verkleinert <see cref="UmPiHalbeVerkleinern"/> genau: 2^19 · π/2 (n · PiHalbe1 bleibt exakt).</summary>
    private const double GenauBis = 823549.6653973387;

    /// <summary>
    /// x = n · π/2 + r mit |r| ≤ π/4; r steht als Summe <paramref name="r"/> + <paramref name="rTief"/> (mehr Stellen als eine double).
    /// Genau für |x| bis etwa 800.000 (in der Simulation kommen nur Winkel von wenigen Umdrehungen vor). Darüber bleibt das Ergebnis
    /// bitgleich und zwischen −1 und 1, aber nicht genau – dafür bräuchte es 2/π auf über tausend Stellen (Payne-Hanek, wie in fdlibm).
    /// </summary>
    /// <returns>n (nur die letzten zwei Bits zählen: der Viertelkreis).</returns>
    private static long UmPiHalbeVerkleinern(double x, out double r, out double rTief)
    {
        // Riesige Winkel grob um Vielfache von π/2 verkleinern (jede Runde um mindestens zehn Stellen), bis der genaue Weg greift.
        long viertel = 0;
        while (Math.Abs(x) >= GenauBis)
        {
            var f = Math.Floor(x * ZweiDurchPi + 0.5);
            viertel += (long)(f - Math.Floor(f / 4) * 4); // f mod 4, auch für negative und riesige f exakt
            x = (x - f * PiHalbe1) - f * PiHalbe1t;
        }

        var betrag = Math.Abs(x);
        var fn = Math.Floor(betrag * ZweiDurchPi + 0.5);
        var rest = betrag - fn * PiHalbe1; // exakt für n < 2^20
        var w = fn * PiHalbe1t;
        r = rest - w;
        // Liegt x sehr nahe an einem Vielfachen von π/2, löscht die Subtraktion Stellen aus: dann mit den nächsten Teilen von π/2 nachrechnen.
        var j = HohesWort(betrag) >> 20;
        if (j - ((HohesWort(r) >> 20) & 0x7ff) > 16)
        {
            var t = rest;
            w = fn * PiHalbe2;
            rest = t - w;
            w = fn * PiHalbe2t - ((t - rest) - w);
            r = rest - w;
            if (j - ((HohesWort(r) >> 20) & 0x7ff) > 49)
            {
                t = rest;
                w = fn * PiHalbe3;
                rest = t - w;
                w = fn * PiHalbe3t - ((t - rest) - w);
                r = rest - w;
            }
        }
        rTief = (rest - r) - w;
        var n = (long)fn;
        if (x < 0) { r = -r; rTief = -rTief; n = -n; }
        return n + viertel;
    }

    /// <summary>sin(x + y) für |x| ≤ π/4, |y| klein (der Rest aus dem Verkleinern; <paramref name="mitRest"/> = false: y ist 0).</summary>
    private static double KernSin(double x, double y, bool mitRest)
    {
        var z = x * x;
        var v = z * x;
        var r = S2 + z * (S3 + z * (S4 + z * (S5 + z * S6)));
        if (!mitRest) return x + v * (S1 + z * r);
        return x - ((z * (0.5 * y - v * r) - y) - v * S1);
    }

    /// <summary>cos(x + y) für |x| ≤ π/4, |y| klein.</summary>
    private static double KernCos(double x, double y)
    {
        var ix = HohesWort(x) & 0x7fffffff;
        var z = x * x;
        var r = z * (C1 + z * (C2 + z * (C3 + z * (C4 + z * (C5 + z * C6)))));
        if (ix < 0x3fd33333) return 1 - (0.5 * z - (z * r - x * y)); // |x| < 0,3
        // sonst 1 − x²/2 in zwei Teilen rechnen, damit keine Stelle verloren geht
        var qx = ix > 0x3fe90000 ? 0.28125 : AusBits((long)(ix - 0x00200000) << 32); // etwa x/4
        var hz = 0.5 * z - qx;
        var a = 1 - qx;
        return a - (hz - (z * r - x * y));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Atan und Atan2
    // ═════════════════════════════════════════════════════════════════════════

    private const double PiViertel = 7.8539816339744827900e-01, PiHalbe = 1.5707963267948965580e+00, Pi = 3.1415926535897931160e+00;
    private const double PiTief = 1.2246467991473531772e-16; // π − Pi

    // atan(0,5), atan(1), atan(1,5), atan(∞) – je in hohem und tiefem Teil
    private static readonly double[] AtanHoch = { 4.63647609000806093515e-01, 7.85398163397448278999e-01, 9.82793723247329054082e-01, 1.57079632679489655800e+00 };
    private static readonly double[] AtanTief = { 2.26987774529616870924e-17, 3.06161699786838301793e-17, 1.39033110312309984516e-17, 6.12323399573676603587e-17 };

    private static readonly double[] AT =
    {
        3.33333333333329318027e-01, -1.99999999998764832476e-01, 1.42857142725034663711e-01, -1.11111104054623557880e-01,
        9.09088713343650656196e-02, -7.69187620504482999495e-02, 6.66107313738753120669e-02, -5.83357013379057348645e-02,
        4.97687799461593236017e-02, -3.65315727442169155270e-02, 1.62858201153657823623e-02,
    };

    /// <summary>Arkustangens (Ergebnis im Bogenmaß, −π/2 bis π/2) – wie <see cref="Math.Atan"/>, aber auf jedem Rechner bitgleich.</summary>
    public static double Atan(double x)
    {
        var hx = HohesWort(x);
        var ix = hx & 0x7fffffff;
        if (ix >= 0x44100000) // |x| ≥ 2^66 (oder unendlich, NaN)
        {
            if (double.IsNaN(x)) return x;
            return hx > 0 ? AtanHoch[3] + AtanTief[3] : -AtanHoch[3] - AtanTief[3];
        }
        int id;
        if (ix < 0x3fdc0000) // |x| < 0,4375
        {
            if (ix < 0x3e200000) return x; // |x| < 2^-29: atan(x) = x auf alle Stellen
            id = -1;
        }
        else
        {
            // 1. Bereich verkleinern: atan(x) = atan(c) + atan((x − c)/(1 + c·x)) mit c = 0,5, 1, 1,5 oder ∞
            x = Math.Abs(x);
            if (ix < 0x3ff30000) // |x| < 1,1875
            {
                if (ix < 0x3fe60000) { id = 0; x = (2.0 * x - 1.0) / (2.0 + x); } // 7/16 ≤ |x| < 11/16
                else { id = 1; x = (x - 1.0) / (x + 1.0); }                        // 11/16 ≤ |x| < 19/16
            }
            else
            {
                if (ix < 0x40038000) { id = 2; x = (x - 1.5) / (1.0 + 1.5 * x); } // |x| < 2,4375
                else { id = 3; x = -1.0 / x; }                                      // 2,4375 ≤ |x| < 2^66
            }
        }
        // 2. Polynom, in gerade und ungerade Glieder geteilt
        var z = x * x;
        var w = z * z;
        var s1 = z * (AT[0] + w * (AT[2] + w * (AT[4] + w * (AT[6] + w * (AT[8] + w * AT[10])))));
        var s2 = w * (AT[1] + w * (AT[3] + w * (AT[5] + w * (AT[7] + w * AT[9]))));
        if (id < 0) return x - x * (s1 + s2);
        var ergebnis = AtanHoch[id] - ((x * (s1 + s2) - AtanTief[id]) - x);
        return hx < 0 ? -ergebnis : ergebnis;
    }

    /// <summary>
    /// Winkel des Punktes (<paramref name="x"/>, <paramref name="y"/>) gegen die x-Achse, −π bis π – wie <see cref="Math.Atan2"/>
    /// (Reihenfolge y, x; auch bei ±0 und unendlich), aber auf jedem Rechner bitgleich.
    /// </summary>
    public static double Atan2(double y, double x)
    {
        if (double.IsNaN(x) || double.IsNaN(y)) return x + y;
        if (x == 1.0) return Atan(y);
        var hx = HohesWort(x);
        var hy = HohesWort(y);
        var m = ((hy >> 31) & 1) | ((hx >> 30) & 2); // 2·Vorzeichen(x) + Vorzeichen(y)

        if (y == 0) // auch −0
            return m switch { 0 or 1 => y, 2 => Pi, _ => -Pi };
        if (x == 0)
            return hy < 0 ? -PiHalbe : PiHalbe;
        if (double.IsInfinity(x))
        {
            if (double.IsInfinity(y))
                return m switch { 0 => PiViertel, 1 => -PiViertel, 2 => 3.0 * PiViertel, _ => -3.0 * PiViertel };
            return m switch { 0 => 0.0, 1 => -0.0, 2 => Pi, _ => -Pi };
        }
        if (double.IsInfinity(y))
            return hy < 0 ? -PiHalbe : PiHalbe;

        // y/x berechnen – bei sehr verschiedenen Größen ohne Division
        var k = ((hy & 0x7fffffff) - (hx & 0x7fffffff)) >> 20;
        double z;
        if (k > 60) z = PiHalbe + 0.5 * PiTief; // |y/x| > 2^60
        else if (hx < 0 && k < -60) z = 0.0;    // |y|/x < −2^60
        else z = Atan(Math.Abs(y / x));
        return m switch
        {
            0 => z,                  // atan(+, +)
            1 => -z,                 // atan(−, +)
            2 => Pi - (z - PiTief),  // atan(+, −)
            _ => (z - PiTief) - Pi,  // atan(−, −)
        };
    }
}
