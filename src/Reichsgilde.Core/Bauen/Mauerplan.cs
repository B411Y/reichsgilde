namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Wo das Spiel die Teile einer Mauer setzt: Mauerstücke, Mauertürme, Ecktürme und Torburgen
//
//  Die Karte nennt nur die Linie der Mauer (Punkte, offen oder geschlossen) und ihre Tore. Wie daraus eine Mauer wird, ist eine feste
//  Regel – bis zur Kartenwerkstatt, Schritt 3, stand sie nur in der Darstellung (Stadtansicht.Mauer.cs). Jetzt rechnet sie der Kern,
//  damit Spiel und Kartenwerkstatt dieselben Türme zeigen:
//    • Jedes Tor sitzt auf der Strecke, der es am nächsten liegt; die Mauerstücke enden 7,6 m links und rechts davon (Torburg 16 m breit).
//    • Die Abschnitte zwischen den Toren füllen Mauerstücke von etwa 4 m (gestreckt auf 3 bis 5 m, damit der Abschnitt aufgeht);
//      dazwischen steht etwa alle 35 m ein Mauerturm (gleichmäßig verteilt, round(Länge / 35) − 1 je Abschnitt).
//    • Ein Eckturm steht an jedem Knick ab 12° und an den beiden Enden einer offenen Mauer.
//  Die Feldseite (außen) ist die Seite, die vom Mittelpunkt der Linienpunkte wegzeigt.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Lage eines Mauerteils: Ort (Meter), Richtung entlang der Mauer als Einheitsvektor (so gedreht, dass die Feldseite – rechts davon,
/// <see cref="FeldX"/>/<see cref="FeldZ"/> – nach außen zeigt) und Länge (nur Mauerstücke, sonst 0).
/// </summary>
public readonly record struct Mauerlage(double X, double Z, double EntlangX, double EntlangZ, double Laenge = 0)
{
    /// <summary>Die Feldseite (außen) als Einheitsvektor: Entlang × oben, wie lokal +Z im Baukasten.</summary>
    public double FeldX => -EntlangZ;

    public double FeldZ => EntlangX;

    /// <summary>Die Drehung um die Hochachse in Grad wie Godot <c>Rotation.Y</c> (lokal X des Baukastens zeigt entlang der Mauer).</summary>
    public double Drehung => DetMathe.Atan2(-EntlangZ, EntlangX) * 180 / Math.PI;
}

/// <summary>Eine Torburg: das Tor der Karte, seine Lage auf der Mauer (Ort = auf die Strecke gelegt) und die Nummer der Strecke.</summary>
public readonly record struct Mauertor(string Name, Mauerlage Lage, int Strecke);

/// <summary>Alle Teile einer Mauer (siehe Kopf der Datei). Entsteht mit <see cref="Von"/>.</summary>
public sealed class Mauerplan
{
    /// <summary>Länge eines Mauerstücks im Baukasten; es wird auf 3 bis 5 m gestreckt, damit ein Abschnitt aufgeht.</summary>
    public const double MauerstueckLaenge = 4;

    /// <summary>So weit links und rechts der Tormitte enden die Mauerstücke (die Torburg ist 16 m breit).</summary>
    public const double TorAbstand = 7.6;

    /// <summary>Etwa so weit stehen Mauertürme auseinander.</summary>
    public const double TurmAbstand = 35;

    /// <summary>Ab diesem Knick (Grad) bekommt ein Punkt der Linie einen Eckturm; flachere Knicke laufen durch.</summary>
    public const double EckturmAbWinkel = 12;

    /// <summary>Abschnitte zwischen Toren, die kürzer sind, bekommen kein Mauerstück.</summary>
    public const double KleinsterAbschnitt = 0.5;

    public IReadOnlyList<Mauerlage> Stuecke { get; }
    public IReadOnlyList<Mauerlage> Tuerme { get; }
    public IReadOnlyList<Mauerlage> Ecktuerme { get; }
    public IReadOnlyList<Mauertor> Tore { get; }

    /// <summary>Alle Türme (Ecktürme, dann Mauertürme) – für die Kartenwerkstatt, die sie nur als Punkte zeigt.</summary>
    public IEnumerable<Mauerlage> AlleTuerme => Ecktuerme.Concat(Tuerme);

    private Mauerplan(List<Mauerlage> stuecke, List<Mauerlage> tuerme, List<Mauerlage> ecken, List<Mauertor> tore) =>
        (Stuecke, Tuerme, Ecktuerme, Tore) = (stuecke, tuerme, ecken, tore);

    /// <summary>Der Plan einer Mauer der Karte.</summary>
    public static Mauerplan Von(Kartenmauer m) =>
        Von(m.Linie, m.Geschlossen, m.Tore.Where(t => t.Punkt is { Length: 2 }).Select(t => (t.Name, t.Punkt[0], t.Punkt[1])).ToList());

    /// <summary>
    /// Der Plan einer Mauerlinie (Punkte [x, z]; jeder ein Knick oder ein Ende) mit ihren Toren. Weniger als zwei Punkte: ein leerer Plan.
    /// </summary>
    public static Mauerplan Von(IReadOnlyList<double[]> linie, bool geschlossen, IReadOnlyList<(string Name, double X, double Z)> tore)
    {
        var (stuecke, tuerme, ecken, torlagen) = (new List<Mauerlage>(), new List<Mauerlage>(), new List<Mauerlage>(), new List<Mauertor>());
        if (linie.Count < 2) return new Mauerplan(stuecke, tuerme, ecken, torlagen);
        var (mx, mz) = (linie.Average(p => p[0]), linie.Average(p => p[1]));
        var strecken = geschlossen ? linie.Count : linie.Count - 1;
        var torZuStrecke = tore.Select(t => (t, Strecke: NaechsteStrecke(linie, geschlossen, t.X, t.Z))).ToList();

        for (var i = 0; i < strecken; i++)
        {
            var (von, bis) = (linie[i], linie[(i + 1) % linie.Count]);
            var (strX, strZ) = (bis[0] - von[0], bis[1] - von[1]);
            var laenge = Math.Sqrt(strX * strX + strZ * strZ);
            if (laenge <= 0) continue;
            var (ex, ez) = ((bis[0] - von[0]) / laenge, (bis[1] - von[1]) / laenge);
            var (lx, lz) = Ausrichtung(ex, ez, (von[0] + bis[0]) / 2 - mx, (von[1] + bis[1]) / 2 - mz);
            (double X, double Z) Bei(double t) => (von[0] + ex * t, von[1] + ez * t);

            // Die Strecke zwischen den Toren in Abschnitte teilen: Mauerstücke enden ±TorAbstand vom Tor.
            var schnitte = new List<(double Anfang, double Ende)>();
            var anfang = 0.0;
            foreach (var (tor, _) in torZuStrecke.Where(x => x.Strecke == i).OrderBy(x => (x.t.X - von[0]) * ex + (x.t.Z - von[1]) * ez))
            {
                var t = Math.Clamp((tor.X - von[0]) * ex + (tor.Z - von[1]) * ez, 0, laenge);
                schnitte.Add((anfang, t - TorAbstand));
                anfang = t + TorAbstand;
                var (tx, tz) = Bei(t);
                torlagen.Add(new Mauertor(tor.Name, new Mauerlage(tx, tz, lx, lz), i));
            }
            schnitte.Add((anfang, laenge));

            foreach (var (s0, s1) in schnitte)
            {
                var abschnitt = s1 - s0;
                if (abschnitt < KleinsterAbschnitt) continue;
                var anzahl = Math.Max(1, (int)Math.Round(abschnitt / MauerstueckLaenge));
                var stueck = abschnitt / anzahl;
                for (var k = 0; k < anzahl; k++)
                {
                    var (sx, sz) = Bei(s0 + (k + 0.5) * stueck);
                    stuecke.Add(new Mauerlage(sx, sz, lx, lz, stueck));
                }
                // dazwischen Mauertürme, etwa alle 35 m
                var innere = (int)Math.Round(abschnitt / TurmAbstand) - 1;
                for (var k = 1; k <= innere; k++)
                {
                    var (tx, tz) = Bei(s0 + abschnitt * k / (innere + 1));
                    tuerme.Add(new Mauerlage(tx, tz, lx, lz));
                }
            }
        }

        // Ecktürme an Knicken und an den Enden einer offenen Mauer
        for (var i = 0; i < linie.Count; i++)
        {
            var offenesEnde = !geschlossen && (i == 0 || i == linie.Count - 1);
            if (!offenesEnde && Knick(linie[(i + linie.Count - 1) % linie.Count], linie[i], linie[(i + 1) % linie.Count]) < EckturmAbWinkel) continue;
            var (ax, az) = (linie[i][0] - mx, linie[i][1] - mz);
            var l = Math.Sqrt(ax * ax + az * az);
            // entlang = oben × außen (so zeigt die Feldseite nach außen); im Mittelpunkt selbst gibt es kein Außen
            var (ex, ez) = l > 0 ? (az / l, -ax / l) : (1.0, 0.0);
            ecken.Add(new Mauerlage(linie[i][0], linie[i][1], ex, ez));
        }
        return new Mauerplan(stuecke, tuerme, ecken, torlagen);
    }

    /// <summary>
    /// Richtung entlang der Strecke, so gedreht, dass die Feldseite (entlang × oben) zur Außenseite zeigt – die Strecke selbst hat keine
    /// feste Richtung.
    /// </summary>
    private static (double X, double Z) Ausrichtung(double ex, double ez, double aussenX, double aussenZ) =>
        -ez * aussenX + ex * aussenZ < 0 ? (-ex, -ez) : (ex, ez);

    /// <summary>Der Knick an Punkt b in Grad (0 = geradeaus).</summary>
    private static double Knick(double[] a, double[] b, double[] c)
    {
        var (ux, uz, vx, vz) = (b[0] - a[0], b[1] - a[1], c[0] - b[0], c[1] - b[1]);
        return DetMathe.Atan2(Math.Abs(ux * vz - uz * vx), ux * vx + uz * vz) * 180 / Math.PI;
    }

    /// <summary>Die Strecke der Linie, der der Punkt am nächsten liegt (bei Gleichstand die erste).</summary>
    public static int NaechsteStrecke(IReadOnlyList<double[]> linie, bool geschlossen, double x, double z)
    {
        var (beste, kleinster) = (0, double.MaxValue);
        for (var i = 0; i < (geschlossen ? linie.Count : linie.Count - 1); i++)
        {
            var (a, b) = (linie[i], linie[(i + 1) % linie.Count]);
            var d = Geometrie.AbstandZurStrecke(x, z, a[0], a[1], b[0], b[1]);
            if (d < kleinster) (beste, kleinster) = (i, d);
        }
        return beste;
    }
}
