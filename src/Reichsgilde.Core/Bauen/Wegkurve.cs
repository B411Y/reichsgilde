namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Kurve eines Weges (Kartenformat 2, Feld „kurve“)
//
//  Ein Weg speichert nur seine Stützpunkte („linie“). Ohne „kurve“ verbinden gerade Strecken die Punkte – so wie bisher, die Greybox
//  und Köln bleiben unverändert. Mit „kurve“: „glatt“ legt die Kartenwerkstatt eine weiche Kurve durch ALLE Stützpunkte: eine
//  zentripetale Catmull-Rom-Kurve (sie schlägt keine Schleifen und schießt in engen Biegungen nicht über), abgetastet etwa alle
//  Meter. Spiel, Prüfung und Bauwesen rechnen alle mit dieser abgetasteten Linie (dem „Verlauf“), damit sie denselben Weg sehen.
//
//  Zum Nachlesen: Catmull-Rom baut jedes Stück zwischen zwei Stützpunkten aus den Punkten davor und danach; „zentripetal“ heißt,
//  die Abstände der Punkte gehen mit ihrer Wurzel in die Zeitachse der Kurve ein (Barry-Goldman-Pyramide, α = 0,5).
//  An den Enden fehlt ein Nachbar – dort wird er gespiegelt (P₋₁ = 2·P₀ − P₁), sodass die Kurve gerade ausläuft.
//  Ein Ring (erster = letzter Punkt) läuft rundherum glatt.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Die abgetastete Kurve eines Weges: Punkte und für jeden Punkt das Stück zwischen Stützpunkt i und i + 1, auf dem er liegt.</summary>
public sealed record Wegverlauf(IReadOnlyList<double[]> Punkte, IReadOnlyList<int> Abschnitte)
{
    /// <summary>Die Länge der Linie in Metern.</summary>
    public double Laenge => Wegkurve.Laenge(Punkte);
}

/// <summary>Rechnet die Kurve eines Weges (siehe Kopf der Datei). Ohne Godot, ohne Zufall: gleiche Stützpunkte, gleiche Kurve.</summary>
public static class Wegkurve
{
    /// <summary>Der Wert von <c>kurve</c> für eine weiche Kurve durch die Stützpunkte.</summary>
    public const string Glatt = "glatt";

    /// <summary>Abstand der Punkte einer glatten Kurve (Meter, höchstens).</summary>
    public const double Abtastung = 1;

    /// <summary>Feinheit der Längenmessung je Stück (Teilschritte).</summary>
    private const int Messschritte = 32;

    /// <summary>
    /// Die Linie eines Weges: gerade = die Stützpunkte selbst; glatt = die Kurve, je Stück zwischen zwei Stützpunkten in gleich lange
    /// Teile von höchstens <paramref name="schritt"/> Metern geteilt. Jeder Stützpunkt ist ein Punkt des Ergebnisses.
    /// </summary>
    public static Wegverlauf Verlauf(IReadOnlyList<double[]> stuetzpunkte, bool glatt, double schritt = Abtastung)
    {
        var p = stuetzpunkte.Where(x => x is { Length: 2 }).ToList();
        if (!glatt || p.Count < 3)
            return new Wegverlauf(p.Select(x => new[] { x[0], x[1] }).ToList(), Enumerable.Range(0, p.Count).Select(i => Math.Min(i, Math.Max(0, p.Count - 2))).ToList());

        var ring = p.Count >= 4 && Gleich(p[0], p[^1]);
        var n = p.Count;
        double[] Nachbar(int i)
        {
            if (i >= 0 && i < n) return p[i];
            if (ring) return i < 0 ? p[n - 2] : p[1]; // der erste und der letzte Punkt sind derselbe
            var (a, b) = i < 0 ? (p[0], p[1]) : (p[n - 1], p[n - 2]);
            return new[] { 2 * a[0] - b[0], 2 * a[1] - b[1] };
        }

        var punkte = new List<double[]>();
        var abschnitte = new List<int>();
        for (var i = 0; i + 1 < n; i++)
        {
            var (p0, p1, p2, p3) = (Nachbar(i - 1), p[i], p[i + 1], Nachbar(i + 2));
            var stueck = new Stueck(p0, p1, p2, p3);
            // Länge messen: Teilschritte im Parameter, kumuliert
            var laengen = new double[Messschritte + 1];
            var vorher = p1;
            for (var k = 1; k <= Messschritte; k++)
            {
                var q = stueck.Bei(k / (double)Messschritte);
                laengen[k] = laengen[k - 1] + Abstand(vorher, q);
                vorher = q;
            }
            var laenge = laengen[Messschritte];
            var teile = Math.Max(1, (int)Math.Ceiling(laenge / schritt - 1e-9));
            punkte.Add(new[] { p1[0], p1[1] });
            abschnitte.Add(i);
            for (var j = 1; j < teile; j++)
            {
                // Der Parameter, an dem j/teile der Länge erreicht ist (zwischen zwei Teilschritten linear)
                var ziel = laenge * j / teile;
                var k = Array.BinarySearch(laengen, ziel);
                if (k < 0) k = ~k;
                k = Math.Clamp(k, 1, Messschritte);
                var anteil = laengen[k] > laengen[k - 1] ? (ziel - laengen[k - 1]) / (laengen[k] - laengen[k - 1]) : 0;
                punkte.Add(stueck.Bei((k - 1 + anteil) / Messschritte));
                abschnitte.Add(i);
            }
        }
        punkte.Add(new[] { p[^1][0], p[^1][1] });
        abschnitte.Add(n - 2);
        return new Wegverlauf(punkte, abschnitte);
    }

    /// <summary>Die Länge einer Linie (Summe der Strecken) in Metern.</summary>
    public static double Laenge(IReadOnlyList<double[]> linie)
    {
        var summe = 0.0;
        for (var i = 0; i + 1 < linie.Count; i++) summe += Abstand(linie[i], linie[i + 1]);
        return summe;
    }

    private static double Abstand(double[] a, double[] b) => Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));
    private static bool Gleich(double[] a, double[] b) => Abstand(a, b) < 1e-6;

    /// <summary>Ein Stück der zentripetalen Catmull-Rom-Kurve von p1 nach p2 (Parameter 0 … 1).</summary>
    private readonly struct Stueck
    {
        private readonly double[] _p0, _p1, _p2, _p3;
        private readonly double _t0, _t1, _t2, _t3;

        public Stueck(double[] p0, double[] p1, double[] p2, double[] p3)
        {
            (_p0, _p1, _p2, _p3) = (p0, p1, p2, p3);
            // Knotenabstände: Wurzel des Abstands (α = 0,5); ein winziger Mindestwert, falls zwei Punkte zusammenfallen
            static double Schritt(double[] a, double[] b) => Math.Max(Math.Sqrt(Abstand(a, b)), 1e-4);
            _t0 = 0;
            _t1 = _t0 + Schritt(p0, p1);
            _t2 = _t1 + Schritt(p1, p2);
            _t3 = _t2 + Schritt(p2, p3);
        }

        /// <summary>Der Punkt bei u (0 = p1, 1 = p2).</summary>
        public double[] Bei(double u)
        {
            if (u <= 0) return new[] { _p1[0], _p1[1] };
            if (u >= 1) return new[] { _p2[0], _p2[1] };
            var t = _t1 + u * (_t2 - _t1);
            var x = Pyramide(t, 0);
            var z = Pyramide(t, 1);
            return new[] { x, z };
        }

        private double Pyramide(double t, int k)
        {
            var a1 = Mischen(_p0[k], _p1[k], _t0, _t1, t);
            var a2 = Mischen(_p1[k], _p2[k], _t1, _t2, t);
            var a3 = Mischen(_p2[k], _p3[k], _t2, _t3, t);
            var b1 = Mischen(a1, a2, _t0, _t2, t);
            var b2 = Mischen(a2, a3, _t1, _t3, t);
            return Mischen(b1, b2, _t1, _t2, t);
        }

        private static double Mischen(double a, double b, double ta, double tb, double t) => ((tb - t) * a + (t - ta) * b) / (tb - ta);
    }
}
