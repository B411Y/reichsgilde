namespace Reichsgilde.Core.Kartenwerkstatt;

/// <summary>Ein Punkt auf dem Boden der Karte in Metern (x = Osten, z = Süden).</summary>
public readonly record struct Kartenort(double X, double Z)
{
    public static Kartenort Aus(double[] p) => new(p[0], p[1]);

    public double Abstand(Kartenort o) => Math.Sqrt((X - o.X) * (X - o.X) + (Z - o.Z) * (Z - o.Z));

    public override string ToString() => $"({Kartenschreiber.Zahl(Math.Round(X, 1))} | {Kartenschreiber.Zahl(Math.Round(Z, 1))})";
}

/// <summary>
/// Rechenhilfen für Punkte, Strecken und Vielecke – genau wie im alten Kartenwerkzeug (bis Schritt 4 <c>karte.py</c>, Abschnitt „Geometrie“),
/// damit die Befunde beim Umzug nach C# dieselben blieben.
/// </summary>
public static class Kartengeometrie
{
    /// <summary>Ein lokaler Punkt (x nach rechts, z zur Vorderseite) in Weltrichtung gedreht – wie Godot <c>Rotation.Y</c> in Grad.</summary>
    public static Kartenort Dreh(double lx, double lz, double grad)
    {
        var t = grad * Math.PI / 180;
        var (c, s) = (DetMathe.Cos(t), DetMathe.Sin(t));
        return new Kartenort(lx * c + lz * s, -lx * s + lz * c);
    }

    /// <summary>Kürzester Abstand von Punkt p zur Strecke a–b (gleich voll optimiert: die Kapazitätsprüfung ruft sie hunderttausendfach auf).</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    public static double AbstandStrecke(Kartenort p, Kartenort a, Kartenort b)
    {
        var (dx, dz) = (b.X - a.X, b.Z - a.Z);
        var l2 = dx * dx + dz * dz;
        var t = l2 == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / l2));
        return p.Abstand(new Kartenort(a.X + t * dx, a.Z + t * dz));
    }

    /// <summary>Kürzester Abstand eines Punktes zu einer Linie (Punktfolge).</summary>
    public static double AbstandLinie(Kartenort p, IReadOnlyList<Kartenort> linie)
    {
        if (linie.Count == 1) return p.Abstand(linie[0]);
        var best = double.MaxValue;
        for (var i = 0; i + 1 < linie.Count; i++)
            best = Math.Min(best, AbstandStrecke(p, linie[i], linie[i + 1]));
        return best;
    }

    /// <summary>Schnittpunkt der Strecken a–b und c–d oder null.</summary>
    public static Kartenort? Schnitt(Kartenort a, Kartenort b, Kartenort c, Kartenort d)
    {
        var (rx, rz) = (b.X - a.X, b.Z - a.Z);
        var (sx, sz) = (d.X - c.X, d.Z - c.Z);
        var n = rx * sz - rz * sx;
        if (Math.Abs(n) < 1e-9) return null;
        var t = ((c.X - a.X) * sz - (c.Z - a.Z) * sx) / n;
        var u = ((c.X - a.X) * rz - (c.Z - a.Z) * rx) / n;
        return t is >= 0 and <= 1 && u is >= 0 and <= 1 ? new Kartenort(a.X + t * rx, a.Z + t * rz) : null;
    }

    /// <summary>Liegt p im Vieleck? (Strahlverfahren, der Rand zählt mal so, mal so – wie im Werkzeug.)</summary>
    public static bool ImVieleck(Kartenort p, IReadOnlyList<Kartenort> poly)
    {
        var drin = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var (x1, z1, x2, z2) = (poly[i].X, poly[i].Z, poly[j].X, poly[j].Z);
            if ((z1 > p.Z) != (z2 > p.Z) && p.X < (x2 - x1) * (p.Z - z1) / (z2 - z1) + x1)
                drin = !drin;
        }
        return drin;
    }

    /// <summary>Die Strecken eines Vielecks (geschlossen) oder einer Linie (offen).</summary>
    public static IEnumerable<(Kartenort A, Kartenort B)> Strecken(IReadOnlyList<Kartenort> poly, bool geschlossen = true)
    {
        var n = poly.Count;
        for (var i = 0; i < (geschlossen ? n : n - 1); i++)
            yield return (poly[i], poly[(i + 1) % n]);
    }

    /// <summary>Kürzester Abstand zweier Strecken (0, wenn sie sich schneiden).</summary>
    public static double AbstandStrecken(Kartenort a, Kartenort b, Kartenort c, Kartenort d)
    {
        if (Schnitt(a, b, c, d) is not null) return 0;
        return Math.Min(Math.Min(AbstandStrecke(a, c, d), AbstandStrecke(b, c, d)), Math.Min(AbstandStrecke(c, a, b), AbstandStrecke(d, a, b)));
    }

    /// <summary>Kürzester Abstand zwischen dem Vieleck p und dem Vieleck (oder der offenen Linie) q – 0, wenn sie sich überdecken.</summary>
    public static double AbstandVielecke(IReadOnlyList<Kartenort> p, IReadOnlyList<Kartenort> q, bool qGeschlossen = true)
    {
        if (q.Any(e => ImVieleck(e, p)) || (qGeschlossen && p.Any(e => ImVieleck(e, q)))) return 0;
        if (q.Count == 1) return Strecken(p).Min(s => AbstandStrecke(q[0], s.A, s.B));
        var best = double.MaxValue;
        foreach (var (a, b) in Strecken(p))
            foreach (var (c, d) in Strecken(q, qGeschlossen))
                best = Math.Min(best, AbstandStrecken(a, b, c, d));
        return best;
    }

    /// <summary>Überdecken sich zwei konvexe Vielecke um mehr als <paramref name="rand"/> Meter? (Trennachsen; Berühren an einer Kante zählt nicht.)</summary>
    public static bool Ueberlappen(IReadOnlyList<Kartenort> p, IReadOnlyList<Kartenort> q, double rand = 0.01)
    {
        foreach (var poly in new[] { p, q })
            foreach (var (a, b) in Strecken(poly))
            {
                var (nx, nz) = (b.Z - a.Z, a.X - b.X);
                var laenge = Math.Sqrt(nx * nx + nz * nz);
                if (laenge == 0) laenge = 1;
                (nx, nz) = (nx / laenge, nz / laenge);
                double pMin = double.MaxValue, pMax = double.MinValue, qMin = double.MaxValue, qMax = double.MinValue;
                foreach (var e in p) { var s = e.X * nx + e.Z * nz; pMin = Math.Min(pMin, s); pMax = Math.Max(pMax, s); }
                foreach (var e in q) { var s = e.X * nx + e.Z * nz; qMin = Math.Min(qMin, s); qMax = Math.Max(qMax, s); }
                if (Math.Min(pMax, qMax) - Math.Max(pMin, qMin) <= rand) return false;
            }
        return true;
    }

    /// <summary>Die Mitte (Durchschnitt der Ecken) eines Vielecks – Ort zum Hinspringen.</summary>
    public static Kartenort Mitte(IReadOnlyList<Kartenort> poly) => new(poly.Average(p => p.X), poly.Average(p => p.Z));

    /// <summary>Python-Modulo für Gleitkommazahlen (Ergebnis hat das Vorzeichen des Teilers).</summary>
    internal static double Mod(double a, double b) => a - b * Math.Floor(a / b);
}

/// <summary>Raster-Hilfen für das Setzen: Die Darstellung entscheidet, ob sie rundet (mit gehaltener Alt-Taste frei).</summary>
public static class Kartenraster
{
    /// <summary>Raster beim Setzen und Ziehen (Meter).</summary>
    public const double Schritt = 1;

    /// <summary>Drehschritt mit Q/E (Grad).</summary>
    public const double Drehschritt = 15;

    /// <summary>Genauigkeit der Punkte in der Datei (5 cm, wie die Greybox) – auch „frei“ gesetzte Punkte landen darauf.</summary>
    public const double Fein = 0.05;

    /// <summary>Ein Wert auf das Raster gerundet (Vorgabe 1 m).</summary>
    public static double Runden(double wert, double schritt = Schritt) => Math.Round(wert / schritt) * schritt;

    /// <summary>Eine Drehung auf den Drehschritt gerundet (Vorgabe 15°), zwischen 0 und 360.</summary>
    public static double DrehungRunden(double grad, double schritt = Drehschritt) => Drehung(Math.Round(grad / schritt) * schritt);

    /// <summary>Eine Drehung zwischen 0 (einschließlich) und 360 Grad, auf 0,1° wie in der Datei.</summary>
    public static double Drehung(double grad)
    {
        var d = Math.Round(Kartengeometrie.Mod(grad, 360), 1);
        return d >= 360 ? 0 : d;
    }

    /// <summary>Ein Punkt auf 5 cm wie in der Datei (genauso gerundet wie im Werkzeug: <c>round(round(p / 0.05) * 0.05, 4)</c>).</summary>
    public static double Punkt(double wert) => Math.Round(Math.Round(wert / Fein) * Fein, 4) + 0.0; // + 0.0: keine „-0“
}
