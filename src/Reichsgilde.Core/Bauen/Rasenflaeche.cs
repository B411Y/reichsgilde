namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Rasen der Restflächen (Darstellung)
//
//  Gärten, Gestrüpp, Kirchhof-Ecken, Bildstöcke, Heuschober und Taubenschläge haben Gras als Grund statt des Stadtlehms. Damit es kein
//  gleichmäßiger Teppich wird, führt ein ausgetretener Pfad hindurch: vom nächsten Weg her durch die Mitte bis an die andere Seite. Hier
//  steht nur die Geometrie dieses Pfades (feste Rechnung aus Feldern, Wegen und Würfel – auf jedem Rechner dasselbe); das Stempeln
//  macht die Darstellung (Stadtansicht.Rasen.cs). Gehört nicht zum Zustand der Simulation.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Geometrie des Rasens der Restflächen (siehe Kopf der Datei).</summary>
public static class Rasenflaeche
{
    /// <summary>Schrittweite (Meter), in der der Pfad von der Mitte nach außen geht.</summary>
    private const double Schritt = 0.25;

    /// <summary>
    /// Liegt der Punkt in der Fläche und mindestens <paramref name="abstand"/> Meter von jedem Feld, das nicht dazugehört?
    /// </summary>
    public static bool Innen(IReadOnlySet<(int I, int J)> menge, double x, double z, double abstand)
    {
        const double r = Restflaechenfuellung.Raster;
        var (i, j) = ((int)Math.Floor(x / r), (int)Math.Floor(z / r));
        if (!menge.Contains((i, j))) return false;
        var reich = (int)Math.Ceiling(abstand / r);
        for (var dj = -reich; dj <= reich; dj++)
            for (var di = -reich; di <= reich; di++)
            {
                if (menge.Contains((i + di, j + dj))) continue;
                // Abstand zum Rechteck des fremden Feldes
                var (x0, z0) = ((i + di) * r, (j + dj) * r);
                var ax = Math.Max(Math.Max(x0 - x, 0), x - (x0 + r));
                var az = Math.Max(Math.Max(z0 - z, 0), z - (z0 + r));
                if (ax * ax + az * az < abstand * abstand) return false;
            }
        return true;
    }

    /// <summary>
    /// Der ausgetretene Pfad durch eine Restfläche als Vielecke (je Strecke ein Rechteck der Breite <paramref name="breite"/>; ihre Vereinigung
    /// stempelt die Darstellung). Er kommt von der Seite des nächsten Weges (höchstens <paramref name="wegHoechstens"/> Meter entfernt, sonst
    /// entlang der langen Seite), geht durch die Mitte (um bis zu 0,6 m seitlich verschoben, nach dem Würfel) und endet an der anderen Seite.
    /// Alle Ecken liegen mindestens <paramref name="einzug"/> Meter innerhalb der Fläche. Leer bei weniger als <paramref name="abFelder"/>
    /// Feldern oder wenn der Pfad kürzer als 1,5 m wäre.
    /// </summary>
    public static List<IReadOnlyList<(double X, double Z)>> Pfad(IReadOnlyList<(int I, int J)> felder, IReadOnlyList<IReadOnlyList<(double X, double Z)>> wege,
        uint wuerfel, double breite, double einzug, double wegHoechstens, int abFelder)
    {
        var aus = new List<IReadOnlyList<(double X, double Z)>>();
        if (felder.Count < Math.Max(1, abFelder)) return aus;
        const double r = Restflaechenfuellung.Raster;
        var menge = new HashSet<(int I, int J)>(felder);
        var abstand = einzug + breite / 2;
        var (cx, cz) = (felder.Average(f => (f.I + 0.5) * r), felder.Average(f => (f.J + 0.5) * r));
        if (!Innen(menge, cx, cz, abstand))
        {
            // Die Mitte liegt nicht in der Fläche (L-Form): das Feld, das ihr am nächsten liegt und noch Platz hat
            var bestes = felder.Select(f => (X: (f.I + 0.5) * r, Z: (f.J + 0.5) * r)).Where(p => Innen(menge, p.X, p.Z, abstand))
                .OrderBy(p => (p.X - cx) * (p.X - cx) + (p.Z - cz) * (p.Z - cz)).ThenBy(p => p.X).ThenBy(p => p.Z).Select(p => ((double, double)?)p).FirstOrDefault();
            if (bestes is not { } b) return aus;
            (cx, cz) = b;
        }
        // Richtung: vom nächsten Weg zur Mitte, sonst die lange Seite
        var (rx, rz) = felder.Max(f => f.I) - felder.Min(f => f.I) >= felder.Max(f => f.J) - felder.Min(f => f.J) ? (1.0, 0.0) : (0.0, 1.0);
        var naechster = double.MaxValue;
        foreach (var weg in wege)
            for (var k = 0; k + 1 < weg.Count; k++)
            {
                var (px, pz) = NaechsterPunkt(weg[k], weg[k + 1], cx, cz);
                var d = Math.Sqrt((px - cx) * (px - cx) + (pz - cz) * (pz - cz));
                if (d < naechster && d <= wegHoechstens && d > 0.01) (naechster, rx, rz) = (d, (cx - px) / d, (cz - pz) / d);
            }
        // vom Weg her bis zur Mitte (rückwärts gelaufen), dann weiter bis zur anderen Seite
        (double X, double Z) Laufen(double sx, double sz)
        {
            var (x, z) = (cx, cz);
            for (var n = 1; n < 400; n++)
            {
                var (nx, nz) = (cx + sx * n * Schritt, cz + sz * n * Schritt);
                if (!Innen(menge, nx, nz, abstand)) break;
                (x, z) = (nx, nz);
            }
            return (x, z);
        }
        var anfang = Laufen(-rx, -rz);
        var ende = Laufen(rx, rz);
        var laenge = Math.Sqrt((ende.X - anfang.X) * (ende.X - anfang.X) + (ende.Z - anfang.Z) * (ende.Z - anfang.Z));
        if (laenge < 1.5) return aus;
        // die Mitte seitlich verschoben: ein Pfad ist nie ganz gerade
        var seitlich = (Restflaechenfuellung.Zufall(wuerfel, 1) - 0.5) * 1.2;
        var (mx, mz) = (cx - rz * seitlich, cz + rx * seitlich);
        if (!Innen(menge, mx, mz, abstand)) (mx, mz) = (cx, cz);
        foreach (var (a, b) in new[] { (anfang, (mx, mz)), ((mx, mz), ende) })
            if (Streifen(a, b, breite) is { } s) aus.Add(s);
        return aus;
    }

    /// <summary>Ein Rechteck der Breite <paramref name="breite"/> um die Strecke; null, wenn sie keine Länge hat.</summary>
    private static IReadOnlyList<(double X, double Z)>? Streifen((double X, double Z) a, (double X, double Z) b, double breite)
    {
        var (dx, dz) = (b.X - a.X, b.Z - a.Z);
        var l = Math.Sqrt(dx * dx + dz * dz);
        if (l < 0.05) return null;
        var (nx, nz) = (-dz / l * breite / 2, dx / l * breite / 2);
        return new[] { (a.X + nx, a.Z + nz), (b.X + nx, b.Z + nz), (b.X - nx, b.Z - nz), (a.X - nx, a.Z - nz) };
    }

    private static (double X, double Z) NaechsterPunkt((double X, double Z) a, (double X, double Z) b, double x, double z)
    {
        var (ex, ez) = (b.X - a.X, b.Z - a.Z);
        var l = ex * ex + ez * ez;
        var t = l < 1e-12 ? 0 : Math.Clamp(((x - a.X) * ex + (z - a.Z) * ez) / l, 0, 1);
        return (a.X + ex * t, a.Z + ez * t);
    }
}
