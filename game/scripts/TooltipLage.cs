namespace Reichsgilde;

/// <summary>Ein Rechteck in Bildschirmpixeln (ohne Godot, damit die Lagerechnung der Tooltips testbar ist).</summary>
public readonly record struct Rechteck(double X, double Y, double Breite, double Hoehe)
{
    public double Rechts => X + Breite;
    public double Unten => Y + Hoehe;
    public bool Schneidet(Rechteck r) => X < r.Rechts && r.X < Rechts && Y < r.Unten && r.Y < Unten;
}

/// <summary>
/// Wo ein Tooltip steht: neben der Maus, aber nie auf dem Mauszeiger. Der 3D-Handschuh liegt mit seiner Fingerspitze auf der Maus und ragt
/// mit der Hand ringsum über sie hinaus; sein Bild (<c>HandschuhZeiger</c>, Zeigergröße × 2) ist das Rechteck <paramref name="hand"/>. Der Tooltip
/// bleibt außerhalb davon: rechts daneben, sonst darunter, links, darüber; die Seite wechselt also am Bildschirmrand von selbst.
/// </summary>
public static class TooltipLage
{
    /// <summary>Abstand zur Hand und zum Bildschirmrand in Pixeln.</summary>
    public const double Abstand = 6;
    private const double Rand = 4;

    /// <summary>Wie bisher: unten rechts von der Maus, am Rand nach links bzw. über die Maus. Ohne Zeiger-Rechteck gilt nur das.</summary>
    public static (double X, double Y) NebenMaus(double mausX, double mausY, double breite, double hoehe, double sichtbarBreite, double sichtbarHoehe)
    {
        var x = mausX + 14;
        var y = mausY + 22;
        if (x + breite > sichtbarBreite - Rand) x = Math.Max(Rand, mausX - breite - 14);
        if (y + hoehe > sichtbarHoehe - Rand) y = Math.Max(Rand, mausY - hoehe - 12);
        return (x, y);
    }

    /// <summary>Lage des Tooltips (linke obere Ecke). <paramref name="hand"/>: das Rechteck des Zeigers oder null (dann <see cref="NebenMaus"/>).</summary>
    public static (double X, double Y) Berechnen(double mausX, double mausY, double breite, double hoehe, double sichtbarBreite, double sichtbarHoehe, Rechteck? hand)
    {
        var alt = NebenMaus(mausX, mausY, breite, hoehe, sichtbarBreite, sichtbarHoehe);
        if (hand is not { } h) return alt;
        double KlemmenX(double x) => Math.Clamp(x, Rand, Math.Max(Rand, sichtbarBreite - breite - Rand));
        double KlemmenY(double y) => Math.Clamp(y, Rand, Math.Max(Rand, sichtbarHoehe - hoehe - Rand));
        // Reihenfolge: rechts neben der Hand, darunter, links, darüber – die erste Stelle, die im Bild liegt und die Hand frei lässt
        var kandidaten = new (double X, double Y)[]
        {
            (h.Rechts + Abstand, KlemmenY(mausY)),
            (KlemmenX(mausX), h.Unten + Abstand),
            (h.X - Abstand - breite, KlemmenY(mausY)),
            (KlemmenX(mausX), h.Y - Abstand - hoehe),
        };
        foreach (var k in kandidaten)
        {
            var r = new Rechteck(k.X, k.Y, breite, hoehe);
            var imBild = k.X >= Rand - 1e-9 && k.Y >= Rand - 1e-9 && r.Rechts <= sichtbarBreite - Rand + 1e-9 && r.Unten <= sichtbarHoehe - Rand + 1e-9;
            if (imBild && !r.Schneidet(h)) return k;
        }
        // Nichts passt ganz (winziges Fenster, riesiger Tooltip): die Stelle mit dem kleinsten Überlapp mit der Hand, im Bild gehalten
        return kandidaten.Select(k => (X: KlemmenX(k.X), Y: KlemmenY(k.Y)))
            .OrderBy(k => Ueberlapp(new Rechteck(k.X, k.Y, breite, hoehe), h)).First();
    }

    private static double Ueberlapp(Rechteck a, Rechteck b)
    {
        var w = Math.Min(a.Rechts, b.Rechts) - Math.Max(a.X, b.X);
        var v = Math.Min(a.Unten, b.Unten) - Math.Max(a.Y, b.Y);
        return w > 0 && v > 0 ? w * v : 0;
    }
}
