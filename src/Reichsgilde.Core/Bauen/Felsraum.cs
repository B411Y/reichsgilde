using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Lage eines Felsstücks im Raum (Fels-Plan R2, 02.11.2026)
//
//  Die Karte speichert die Drehung eines Stücks als drei Euler-Winkel in Grad: „drehung“ (Gieren um die Senkrechte Y), „kippen“[0] (Kippen um
//  die eigene x-Achse) und „kippen“[1] (Rollen um die eigene z-Achse). Gedreht wird in der Reihenfolge Y · X · Z – erst um die Senkrechte,
//  dann um die schon gedrehte x-Achse, zuletzt um die z-Achse; die Darstellung rechnet genauso (FelsLage in Stadtansicht.Felsen.cs). Davor
//  (in Weltachsen) kommt die Neigung zum Hang: Das Stück neigt sich um den Anteil „neigung“ von der Senkrechten zur Hangnormalen.
//
//  Koordinaten wie in Godot: x nach Osten, y nach oben, z nach Süden; das Modell hat seinen Ursprung in der Mitte der Grundfläche (y = 0 ist die
//  Unterkante), die Schauseite zeigt nach +Z. Eine Basis ist eine 3×3-Matrix, ihre Spalten sind die Achsen des Stücks in der Welt.
//
//  Gleich auf jedem Rechner: Winkelfunktionen nur über DetMathe, sonst nur +, −, ·, / und √. Die Ergebnisse, die in die Datei gehen, werden
//  gerundet wie dort (0,1°, 5 cm, 0,01).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Punkt oder eine Richtung im Raum (Meter; x Osten, y oben, z Süden).</summary>
public readonly record struct Felsvektor(double X, double Y, double Z)
{
    public static Felsvektor operator +(Felsvektor a, Felsvektor b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Felsvektor operator -(Felsvektor a, Felsvektor b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Felsvektor operator *(Felsvektor a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    /// <summary>Die Länge.</summary>
    public double Laenge => Math.Sqrt(X * X + Y * Y + Z * Z);
}

/// <summary>Eine Drehachse in Weltachsen bzw. in den Achsen des Stücks.</summary>
public enum Felsachse { X, Y, Z }

/// <summary>Eine Drehung als 3×3-Matrix (Zeile, Spalte); ihre Spalten sind die Achsen des Stücks in der Welt.</summary>
public readonly record struct Felsbasis(double M00, double M01, double M02, double M10, double M11, double M12, double M20, double M21, double M22)
{
    /// <summary>Keine Drehung.</summary>
    public static readonly Felsbasis Einheit = new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    /// <summary>Die x-Achse des Stücks (Breite) in der Welt.</summary>
    public Felsvektor AchseX => new(M00, M10, M20);

    /// <summary>Die y-Achse des Stücks (Höhe) in der Welt.</summary>
    public Felsvektor AchseY => new(M01, M11, M21);

    /// <summary>Die z-Achse des Stücks (Tiefe, Schauseite) in der Welt.</summary>
    public Felsvektor AchseZ => new(M02, M12, M22);

    public static Felsbasis operator *(Felsbasis a, Felsbasis b) => new(
        a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20, a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21, a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22,
        a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20, a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21, a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22,
        a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20, a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21, a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22);

    /// <summary>Dreht einen Vektor.</summary>
    public Felsvektor Mal(Felsvektor v) => new(M00 * v.X + M01 * v.Y + M02 * v.Z, M10 * v.X + M11 * v.Y + M12 * v.Z, M20 * v.X + M21 * v.Y + M22 * v.Z);

    /// <summary>Die Drehung um eine Achse (Grad, rechte Hand wie in Godot).</summary>
    public static Felsbasis Um(Felsachse achse, double grad)
    {
        var w = grad * Math.PI / 180;
        var (c, s) = (DetMathe.Cos(w), DetMathe.Sin(w));
        return achse switch
        {
            Felsachse.X => new(1, 0, 0, 0, c, -s, 0, s, c),
            Felsachse.Y => new(c, 0, s, 0, 1, 0, -s, 0, c),
            _ => new(c, -s, 0, s, c, 0, 0, 0, 1),
        };
    }

    /// <summary>Die Drehung aus den drei Winkeln der Datei (Grad): Y (drehung) · X (kippen) · Z (rollen).</summary>
    public static Felsbasis AusEuler(double drehung, double kippen, double rollen) =>
        Um(Felsachse.Y, drehung) * Um(Felsachse.X, kippen) * Um(Felsachse.Z, rollen);

    /// <summary>
    /// Die drei Winkel (Grad, ungerundet) dieser Drehung in der Reihenfolge Y · X · Z: Drehung 0 … 360, Kippen −90 … 90, Rollen −180 … 180. Steht
    /// das Stück genau auf der Seite (Kippen ±90°), sind Drehung und Rollen nicht zu trennen: dann Rollen 0.
    /// </summary>
    public (double Drehung, double Kippen, double Rollen) Euler()
    {
        // Y·X·Z = [[ca·cc + sa·sb·sc, …, sa·cb], [cb·sc, cb·cc, −sb], [−sa·cc + ca·sb·sc, …, ca·cb]]
        var cb = Math.Sqrt(M02 * M02 + M22 * M22);
        var b = DetMathe.Atan2(-M12, cb);
        double a, c;
        if (cb < 1e-9)
        {
            a = DetMathe.Atan2(-M20, M00);
            c = 0;
        }
        else
        {
            a = DetMathe.Atan2(M02, M22);
            c = DetMathe.Atan2(M10, M11);
        }
        const double grad = 180 / Math.PI;
        return (Kartengeometrie.Mod(a * grad, 360), b * grad, c * grad);
    }

    /// <summary>Die umgekehrte Drehung (bei einer Drehung die Transponierte).</summary>
    public Felsbasis Transponiert => new(M00, M10, M20, M01, M11, M21, M02, M12, M22);

    /// <summary>
    /// Wie <see cref="Euler()"/>, aber von den beiden Winkelsätzen derselben Drehung – (a, b, c) und (a + 180, 180 − b, c + 180) – der, der näher
    /// an den bisherigen Winkeln liegt: So springen die Zahlenfelder nicht, wenn ein Stück über 90° gekippt ist (Kippen dann −180 … 180).
    /// </summary>
    public (double Drehung, double Kippen, double Rollen) EulerNahe(double drehung, double kippen, double rollen)
    {
        var (a, b, c) = Euler();
        static double Abstand(double x, double y) => Math.Abs(Kartengeometrie.Mod(x - y + 180, 360) - 180);
        static double Halb(double w) => Kartengeometrie.Mod(w + 180, 360) - 180;
        var (a2, b2, c2) = (Kartengeometrie.Mod(a + 180, 360), Halb(180 - b), Halb(c + 180));
        var eins = Abstand(a, drehung) + Abstand(b, kippen) + Abstand(c, rollen);
        var zwei = Abstand(a2, drehung) + Abstand(b2, kippen) + Abstand(c2, rollen);
        return zwei < eins ? (a2, b2, c2) : (a, b, c);
    }

    /// <summary>
    /// Die Drehung um eine Achse durch den Ursprung (Länge 1) um einen Winkel im Bogenmaß (Rodrigues) – für die Neigung zum Hang.
    /// </summary>
    internal static Felsbasis UmAchse(Felsvektor k, double bogen)
    {
        var (c, s) = (DetMathe.Cos(bogen), DetMathe.Sin(bogen));
        var t = 1 - c;
        return new(c + k.X * k.X * t, k.X * k.Y * t - k.Z * s, k.X * k.Z * t + k.Y * s,
            k.Y * k.X * t + k.Z * s, c + k.Y * k.Y * t, k.Y * k.Z * t - k.X * s,
            k.Z * k.X * t - k.Y * s, k.Z * k.Y * t + k.X * s, c + k.Z * k.Z * t);
    }
}

/// <summary>
/// Ein Felsstück im Raum (für die Werkstatt: Gizmo, Zahlenfelder, Einrasten): <paramref name="Ursprung"/> (Mitte der Unterkante in der Welt),
/// <paramref name="Basis"/> (ganze Drehung samt Neigung zum Hang, ohne Größe), <paramref name="Eigen"/> (nur die drei Winkel der Datei),
/// <paramref name="Groesse"/> (Breite, Höhe, Tiefe in Metern mit Maßstab und Achsen) und der Hüllkasten in Weltachsen (aus den Maßen der Form).
/// </summary>
public sealed record Felsrahmen(Felsvektor Ursprung, Felsbasis Basis, Felsbasis Eigen, Felsvektor Groesse, Felsvektor HuelleMin, Felsvektor HuelleMax)
{
    /// <summary>Ein Punkt in den Achsen des Stücks (Meter, unskaliert wie die Größe) in der Welt.</summary>
    public Felsvektor InWelt(Felsvektor lokal) => Ursprung + Basis.Mal(lokal);
}

/// <summary>Die Rechnungen zur Lage im Raum (siehe Kopf der Datei).</summary>
public static class Felsraum
{
    /// <summary>Ein Winkel für Kippen und Rollen in der Datei: −180 (ausschließlich) … 180 Grad, auf 0,1°.</summary>
    public static double Winkel(double grad)
    {
        var d = Math.Round(Kartengeometrie.Mod(grad + 180, 360) - 180, 1);
        return (d <= -180 ? d + 360 : d) + 0.0; // + 0.0: keine „-0“
    }

    /// <summary>Höhe (Meter) und Normale (Länge 1) des Bodens an einem Ort – dieselbe Steigung, mit der die Neigung zum Hang rechnet.</summary>
    public static (double Hoehe, Felsvektor Normale) Boden(Hoehenraster hoehen, double x, double z)
    {
        var (gx, gz) = Felsstreuung.Hang(hoehen, x, z);
        var n = new Felsvektor(-gx, 1, -gz);
        return (hoehen.Hoehe(x, z), n * (1 / n.Laenge));
    }

    /// <summary>
    /// Die Neigung zum Hang als Drehung (in Weltachsen, vor den Winkeln der Datei): um den Anteil <paramref name="neigung"/> von der Senkrechten zur
    /// Hangnormalen, wie die Darstellung (Slerp der Senkrechten).
    /// </summary>
    public static Felsbasis Hangneigung(Hoehenraster hoehen, double x, double z, double neigung)
    {
        if (neigung <= 0 || hoehen.IstLeer) return Felsbasis.Einheit;
        var (gx, gz) = Felsstreuung.Hang(hoehen, x, z);
        var quer = Math.Sqrt(gx * gx + gz * gz);
        if (quer < 1e-12) return Felsbasis.Einheit;
        // Senkrechte × Normale ∝ (−gz, 0, gx); Winkel zwischen beiden atan(|g|)
        return Felsbasis.UmAchse(new Felsvektor(-gz / quer, 0, gx / quer), neigung * DetMathe.Atan2(quer, 1));
    }

    /// <summary>Breite, Höhe und Tiefe eines Stücks in Metern (Maße der Form mal Maßstab mal Achse; unbekannte Form: 1 m).</summary>
    public static Felsvektor Groesse(Felslage l)
    {
        var f = Felsformen.Finde(l.Form);
        return new Felsvektor((f?.Breite ?? 1) * l.Massstab * l.AchseX, (f?.Hoehe ?? 1) * l.Massstab * l.AchseY, (f?.Tiefe ?? 1) * l.Massstab * l.AchseZ);
    }

    /// <summary>
    /// Das Stück im Raum: Ursprung = Boden unter der Mitte − Einsinken · Höhe + Versatz, Drehung = Hangneigung · Y · X · Z. <paramref name="boden"/>:
    /// die Bodenhöhe unter der Mitte, wenn der Aufrufer sie anders misst (die Darstellung auf ihren Dreiecken); sonst bilinear aus dem Raster.
    /// </summary>
    public static Felsrahmen Rahmen(Felslage l, Hoehenraster hoehen, double? boden = null)
    {
        var groesse = Groesse(l);
        var eigen = Felsbasis.AusEuler(l.Drehung, l.KippenX, l.KippenZ);
        var basis = Hangneigung(hoehen, l.X, l.Z, l.Neigung) * eigen;
        var ursprung = new Felsvektor(l.X, (boden ?? hoehen.Hoehe(l.X, l.Z)) - l.Einsinken * groesse.Y + l.Versatz, l.Z);
        var (min, max) = (new Felsvektor(double.MaxValue, double.MaxValue, double.MaxValue), new Felsvektor(double.MinValue, double.MinValue, double.MinValue));
        foreach (var sx in new[] { -0.5, 0.5 })
            foreach (var sy in new[] { 0.0, 1.0 })
                foreach (var sz in new[] { -0.5, 0.5 })
                {
                    var p = ursprung + basis.Mal(new Felsvektor(sx * groesse.X, sy * groesse.Y, sz * groesse.Z));
                    min = new Felsvektor(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                    max = new Felsvektor(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
                }
        return new Felsrahmen(ursprung, basis, eigen, groesse, min, max);
    }

    /// <summary>
    /// Der Versatz (Meter, gerundet wie in der Datei), mit dem das Stück auf dem Boden aufsetzt (wie die Taste End in Unreal): Von acht Punkten
    /// auf dem Rand der Grundfläche (Ellipse aus 0,8 × Breite × Tiefe wie beim Felsboden) und acht ebensolchen auf Höhe der Oberkante liegt der tiefste – gemessen am Boden unter ihm – genau
    /// Einsinken · Höhe unter dem Boden. Auf ebenem Boden und gerade stehend ist das 0; ein gekipptes Stück hebt sich, bis seine untere Kante
    /// so tief steckt wie sonst die ganze Unterkante.
    /// </summary>
    public static double Aufsetzen(Felslage l, Hoehenraster hoehen)
    {
        var r = Rahmen(l with { Versatz = 0 }, hoehen);
        var tiefe = l.Einsinken * r.Groesse.Y;
        var (hb, ht) = (Felsboden.Grundflaeche * r.Groesse.X / 2, Felsboden.Grundflaeche * r.Groesse.Z / 2);
        const double d = 0.70710678118654752; // cos 45°
        var versatz = double.MinValue;
        // Unter- und Oberkante: ein über 90° gekipptes Stück steht auf seiner Oberseite
        foreach (var y in new[] { 0.0, r.Groesse.Y })
            foreach (var (u, v) in new[] { (1.0, 0.0), (d, d), (0.0, 1.0), (-d, d), (-1.0, 0.0), (-d, -d), (0.0, -1.0), (d, -d) })
            {
                var p = r.InWelt(new Felsvektor(u * hb, y, v * ht));
                versatz = Math.Max(versatz, hoehen.Hoehe(p.X, p.Z) - tiefe - p.Y);
            }
        return Kartenraster.Punkt(Math.Clamp(versatz, -Felsformen.VersatzMax, Felsformen.VersatzMax));
    }
}
