using System.Text.RegularExpressions;
using Reichsgilde.Core;

/// <summary>
/// Tests für die deterministische Mathematik (<see cref="DetMathe"/>, Mehrspieler M1): so genau wie Math, dieselben Sonderfälle,
/// feste Bitmuster (die CI prüft sie auf Windows und Linux) und kein verbotener Math-Aufruf mehr im Kern.
/// </summary>
static class DetMatheTests
{
    /// <summary>Abstand zweier Kommazahlen in Einheiten der letzten Stelle (0 = bitgleich; ±0 und NaN gelten als gleich).</summary>
    static long Ulp(double a, double b)
    {
        if (a == b || double.IsNaN(a) && double.IsNaN(b)) return 0;
        long ia = BitConverter.DoubleToInt64Bits(a), ib = BitConverter.DoubleToInt64Bits(b);
        if (ia < 0) ia = long.MinValue - ia; // negative Zahlen spiegeln, damit der Abstand über 0 hinweg stimmt
        if (ib < 0) ib = long.MinValue - ib;
        return Math.Abs(ia - ib);
    }

    /// <summary>Gleich bis aufs Bit – auch Vorzeichen der Null; NaN gleich NaN.</summary>
    static bool Bitgleich(double a, double b) =>
        double.IsNaN(a) ? double.IsNaN(b) : BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

    /// <summary>Größte relative Abweichung gegen Math auf <paramref name="n"/> Stichproben (fester Startwert); <paramref name="kleinst"/>
    /// begrenzt den Nenner nach unten (1 = absolute Abweichung, für Sinus und Kosinus nahe ihrer Nullstellen).</summary>
    static (double Abweichung, double Bei) Vergleich(int n, Func<Random, double> stichprobe, Func<double, double> det, Func<double, double> sys,
        double kleinst = 0)
    {
        var zufall = new Random(29_10_2026);
        var (schlimmst, bei) = (0.0, 0.0);
        for (var i = 0; i < n; i++)
        {
            var x = stichprobe(zufall);
            var (a, b) = (det(x), sys(x));
            var abweichung = Math.Abs(a - b) / Math.Max(Math.Abs(b), kleinst);
            if (abweichung > schlimmst) (schlimmst, bei) = (abweichung, x);
        }
        return (schlimmst, bei);
    }

    static void Genau(string name, (double Abweichung, double Bei) ergebnis, double grenze) =>
        Pruefe.Wahr(ergebnis.Abweichung <= grenze, $"{name}: Abweichung {ergebnis.Abweichung:E2} bei {ergebnis.Bei:R} (erlaubt {grenze:E0})");

    /// <summary>Sonderwerte: Nullen mit Vorzeichen, ±1, ganze und halbe Zahlen, riesig, winzig, unendlich, NaN.</summary>
    static readonly double[] Sonderwerte =
    {
        0.0, -0.0, 1, -1, 2, -2, 0.5, -0.5, 3, -3, 2.5, -2.5, 1.5, -1.5, 0.1, -0.1, 33, -33,
        1e300, -1e300, 1e-300, 4.9e-324, double.MaxValue, double.PositiveInfinity, double.NegativeInfinity, double.NaN,
    };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("DetMathe: Exp, Log, Sin, Cos und Atan2 so genau wie Math (höchstens 1e-15 relativ, Sinus und Kosinus absolut)", () =>
        {
            const int n = 20_000;
            Genau("Exp [−1, 1]", Vergleich(n, z => z.NextDouble() * 2 - 1, DetMathe.Exp, Math.Exp), 1e-15);
            Genau("Exp [−700, 700]", Vergleich(n, z => z.NextDouble() * 1400 - 700, DetMathe.Exp, Math.Exp), 1e-15);
            Genau("Log [0,5, 2]", Vergleich(n, z => 0.5 + z.NextDouble() * 1.5, DetMathe.Log, Math.Log), 1e-15);
            Genau("Log [1e−300, 1e300]", Vergleich(n, z => Math.Pow(10, z.NextDouble() * 600 - 300), DetMathe.Log, Math.Log), 1e-15);
            Genau("Log nahe 1", Vergleich(n, z => 1 + (z.NextDouble() * 2 - 1) * 1e-6, DetMathe.Log, Math.Log), 1e-15);
            Genau("Sin [−10, 10]", Vergleich(n, z => z.NextDouble() * 20 - 10, DetMathe.Sin, Math.Sin, kleinst: 1), 1e-15);
            Genau("Cos [−10, 10]", Vergleich(n, z => z.NextDouble() * 20 - 10, DetMathe.Cos, Math.Cos, kleinst: 1), 1e-15);
            Genau("Sin [−1e5, 1e5]", Vergleich(n, z => z.NextDouble() * 2e5 - 1e5, DetMathe.Sin, Math.Sin, kleinst: 1), 1e-15);
            Genau("Cos ganze Grad", Vergleich(n, z => z.Next(-720, 721) * Math.PI / 180, DetMathe.Cos, Math.Cos, kleinst: 1), 1e-15);
            Genau("Atan [−1e6, 1e6]", Vergleich(n, z => z.NextDouble() * 2e6 - 1e6, DetMathe.Atan, Math.Atan), 1e-15);
            var y = 0.0;
            Genau("Atan2 [−100, 100]²", Vergleich(n, z => { y = z.NextDouble() * 200 - 100; return z.NextDouble() * 200 - 100; },
                x => DetMathe.Atan2(y, x), x => Math.Atan2(y, x)), 1e-15);
        }),

        ("DetMathe: Pow so genau wie verlangt (Preise 1e-15, ganze Hochzahlen 1e-14, sonst bis |y·ln x| = 350 unter 1e-13)", () =>
        {
            const int n = 20_000;
            var y = 0.0;
            // Wie der Markt: Nachfrage/Angebot hoch Elastizität
            Genau("Pow Preis", Vergleich(n, z => { y = 0.3 + z.NextDouble() * 1.2; return 0.2 + z.NextDouble() * 5; },
                x => DetMathe.Pow(x, y), x => Math.Pow(x, y)), 1e-15);
            Genau("Pow ganz −32 … 32", Vergleich(n, z => { y = z.Next(-32, 33); return 0.1 + z.NextDouble() * 9.9; },
                x => DetMathe.Pow(x, y), x => Math.Pow(x, y)), 1e-14);
            Genau("Pow [1e−3, 1e3] hoch [−50, 50]", Vergleich(n, z => { y = z.NextDouble() * 100 - 50; return Math.Pow(10, z.NextDouble() * 6 - 3); },
                x => DetMathe.Pow(x, y), x => Math.Pow(x, y)), 1e-13);
            Genau("Pow [0, 1] hoch [0, 60]", Vergleich(n, z => { y = z.NextDouble() * 60; return z.NextDouble(); },
                x => DetMathe.Pow(x, y), x => Math.Pow(x, y)), 1e-13);
        }),

        ("DetMathe: Sonderfälle wie Math – ±0, ±unendlich, NaN, negative Basis, x² ist genau x · x", () =>
        {
            foreach (var a in Sonderwerte)
            {
                foreach (var (name, det, sys) in new (string, Func<double, double>, Func<double, double>)[]
                         { ("Exp", DetMathe.Exp, Math.Exp), ("Log", DetMathe.Log, Math.Log), ("Atan", DetMathe.Atan, Math.Atan) })
                    Pruefe.Wahr(Ulp(det(a), sys(a)) <= 1 && double.IsNegative(det(a)) == double.IsNegative(sys(a)) || double.IsNaN(sys(a)) && double.IsNaN(det(a)),
                        $"{name}({a:R}): {det(a):R} statt {sys(a):R}");
                if (Math.Abs(a) < 1e5 || double.IsNaN(a) || double.IsInfinity(a)) // riesige Winkel: siehe unten
                    foreach (var (name, det, sys) in new (string, Func<double, double>, Func<double, double>)[]
                             { ("Sin", DetMathe.Sin, Math.Sin), ("Cos", DetMathe.Cos, Math.Cos) })
                        Pruefe.Wahr(Ulp(det(a), sys(a)) <= 1 && double.IsNegative(det(a)) == double.IsNegative(sys(a)) || double.IsNaN(sys(a)) && double.IsNaN(det(a)),
                            $"{name}({a:R}): {det(a):R} statt {sys(a):R}");
                foreach (var b in Sonderwerte)
                {
                    var (pd, ps) = (DetMathe.Pow(a, b), Math.Pow(a, b));
                    // exakte Ergebnisse (0, ±unendlich, ±1, NaN …) müssen bitgleich sein, gerundete höchstens 1e-13 abweichen
                    var exakt = ps == 0 || double.IsInfinity(ps) || double.IsNaN(ps) || Math.Abs(ps) == 1;
                    Pruefe.Wahr(exakt ? Bitgleich(pd, ps) : Math.Abs(pd - ps) <= 1e-13 * Math.Abs(ps) && Math.Sign(pd) == Math.Sign(ps),
                        $"Pow({a:R}, {b:R}): {pd:R} statt {ps:R}");
                    var (ad, @as) = (DetMathe.Atan2(a, b), Math.Atan2(a, b));
                    Pruefe.Wahr(Ulp(ad, @as) <= 1 && double.IsNegative(ad) == double.IsNegative(@as), $"Atan2({a:R}, {b:R}): {ad:R} statt {@as:R}");
                }
            }
            // ganze Hochzahlen: exakt, wo das Ergebnis darstellbar ist
            Pruefe.Gleich(0.125, DetMathe.Pow(0.5, 3));
            Pruefe.Gleich(1024.0, DetMathe.Pow(2, 10));
            Pruefe.Gleich(-8.0, DetMathe.Pow(-2, 3));
            Pruefe.Gleich(0.0625, DetMathe.Pow(-2, -4));
            Pruefe.Gleich(1000.0, DetMathe.Pow(10, 3));
            var zufall = new Random(7);
            for (var i = 0; i < 1000; i++)
            {
                var x = (zufall.NextDouble() - 0.5) * 1e3;
                Pruefe.Wahr(Bitgleich(x * x, DetMathe.Pow(x, 2)), $"Pow({x:R}, 2) ist nicht x · x");
            }
        }),

        ("DetMathe: riesige Winkel bleiben bitgleich, endlich und zwischen −1 und 1 (sin² + cos² = 1)", () =>
        {
            var zufall = new Random(11);
            foreach (var groesse in new[] { 1e6, 1e10, 1e20, 1e100, 1e300 })
                for (var i = 0; i < 200; i++)
                {
                    var x = groesse * (1 + zufall.NextDouble()) * (i % 2 == 0 ? 1 : -1);
                    var (s, c) = (DetMathe.Sin(x), DetMathe.Cos(x));
                    Pruefe.Wahr(Math.Abs(s) <= 1 && Math.Abs(c) <= 1, $"Sin/Cos({x:R}) = {s:R}, {c:R}");
                    Pruefe.Wahr(Math.Abs(s * s + c * c - 1) < 1e-15, $"sin² + cos² bei {x:R}");
                }
        }),

        ("DetMathe: feste Bitmuster – auf jedem Rechner dieselben (sonst laufen Mehrspieler-Partien auseinander)", () =>
        {
            // Diese Zahlen sind das Versprechen der Klasse: Windows und Linux müssen genau sie liefern (die CI läuft auf beiden).
            // Ändern nur, wenn DetMathe absichtlich geändert wurde – dann rechnet jede Partie ein wenig anders.
            (string Was, double Wert, ulong Bits)[] soll =
            {
                ("Exp(1)", DetMathe.Exp(1), 0x4005BF0A8B14576AUL),
                ("Exp(−3,5)", DetMathe.Exp(-3.5), 0x3F9EEC1018E4FF66UL),
                ("Exp(−730)", DetMathe.Exp(-730), 0x00000000001C7EA3UL),
                ("Log(10)", DetMathe.Log(10), 0x40026BB1BBB55516UL),
                ("Log(0,3)", DetMathe.Log(0.3), 0xBFF34378FCBDA721UL),
                ("Sin(1)", DetMathe.Sin(1), 0x3FEAED548F090CEEUL),
                ("Cos(2,5)", DetMathe.Cos(2.5), 0xBFE9A2F7EF858B7DUL),
                ("Sin(100000)", DetMathe.Sin(1e5), 0x3FA24DAA9C527E96UL),
                ("Sin(1e300)", DetMathe.Sin(1e300), 0x3FC6F1A50C9BDE3FUL),
                ("Cos(π/2)", DetMathe.Cos(Math.PI / 2), 0x3C91A62633145C07UL),
                ("Atan2(1,7, −2)", DetMathe.Atan2(1.7, -2), 0x40037F2D8B93012BUL),
                ("Atan2(−3, 0,5)", DetMathe.Atan2(-3, 0.5), 0xBFF67D8863BC99BDUL),
                ("Pow(1,7, 0,6)", DetMathe.Pow(1.7, 0.6), 0x3FF5FF914D82151CUL),
                ("Pow(0,85, 13,25)", DetMathe.Pow(0.85, 13.25), 0x3FBDB82DB5ACED14UL),
                ("Pow(10, 13,25)", DetMathe.Pow(10, 13.25), 0x42B02C61201EA540UL),
                ("Pow(0,9, 7)", DetMathe.Pow(0.9, 7), 0x3FDE9C6A99D1CDC0UL),
            };
            foreach (var (was, wert, bits) in soll)
                Pruefe.Gleich($"{bits:X16}", $"{BitConverter.DoubleToInt64Bits(wert):X16}", $"{was} = {wert:R}");
        }),

        ("DetMathe: Im Kern steht kein Math.Pow/Exp/Log/Sin/Cos/Tan/Atan mehr (Mehrspieler-Regel der CLAUDE.md)", () =>
        {
            var kern = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!, "src", "Reichsgilde.Core");
            var verboten = new Regex(@"\b(Math|MathF|double|float|Double|Single)\s*\.\s*(Pow|Exp|Exp2|Exp10|ExpM1|Log|Log2|Log10|LogP1|ILogB|Sin|Cos|Tan|SinCos|Asin|Acos|Atan|Atan2|Sinh|Cosh|Tanh|Asinh|Acosh|Atanh|Cbrt|Hypot|RootN)\s*\(");
            var funde = new List<string>();
            foreach (var datei in Directory.EnumerateFiles(kern, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                if (datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
                var zeilen = File.ReadAllLines(datei);
                for (var i = 0; i < zeilen.Length; i++)
                {
                    var code = zeilen[i].Split("//")[0]; // Kommentare zählen nicht
                    if (verboten.IsMatch(code)) funde.Add($"{Path.GetRelativePath(kern, datei)}:{i + 1}: {zeilen[i].Trim()}");
                }
            }
            Pruefe.Wahr(funde.Count == 0, "Statt Math.* bitte DetMathe verwenden (Math.Pow(x, 2) → x * x):\n  " + string.Join("\n  ", funde));
        }),
    };
}
