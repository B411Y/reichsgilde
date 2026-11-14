using Reichsgilde;

/// <summary>Der Tooltip liegt nie auf dem Mauszeiger (Fehlerpaket 1, Punkt 2): Lagerechnung mit Gegenprobe, auch an allen Rändern und Ecken.</summary>
static class TooltipLageTests
{
    const double B = 1920, H = 1080;

    /// <summary>Bild der 3D-Hand bei Zeigergröße 64: Seite 128, die Fingerspitze liegt hier ein Stück vom linken oberen Rand entfernt.</summary>
    static Rechteck Hand(double mx, double my) => new(mx - 40, my - 30, 128, 128);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Tooltip-Lage: nie auf dem Zeiger – Mitte, alle vier Ränder und alle Ecken, kleine und große Tooltips", () =>
        {
            var xs = new double[] { 0, 3, 60, 500, 960, 1500, 1860, 1917, 1919 };
            var ys = new double[] { 0, 3, 60, 300, 540, 900, 1020, 1077, 1079 };
            foreach (var (tb, th) in new[] { (120.0, 40.0), (340.0, 120.0), (340.0, 400.0) })
                foreach (var mx in xs)
                    foreach (var my in ys)
                    {
                        var hand = Hand(mx, my);
                        var (x, y) = TooltipLage.Berechnen(mx, my, tb, th, B, H, hand);
                        var r = new Rechteck(x, y, tb, th);
                        Pruefe.Wahr(!r.Schneidet(hand), $"Maus {mx},{my}, Tooltip {tb}×{th}: liegt auf der Hand ({x:0},{y:0})");
                        Pruefe.Wahr(x >= 0 && y >= 0 && r.Rechts <= B && r.Unten <= H, $"Maus {mx},{my}, Tooltip {tb}×{th}: nicht im Bild ({x:0},{y:0})");
                    }
        }),

        ("Tooltip-Lage: Gegenprobe – die alte Lage (14, 22 unter der Maus) lag auf der Hand", () =>
        {
            var (x, y) = TooltipLage.NebenMaus(800, 500, 300, 100, B, H);
            Pruefe.Wahr(new Rechteck(x, y, 300, 100).Schneidet(Hand(800, 500)), "alte Lage schneidet die Hand");
            var (nx, ny) = TooltipLage.Berechnen(800, 500, 300, 100, B, H, Hand(800, 500));
            Pruefe.Wahr(nx >= 800 - 40 + 128, "neu: rechts neben der Hand");
        }),

        ("Tooltip-Lage: ohne Zeiger-Rechteck gilt die alte Lage; am rechten Rand wechselt die Seite", () =>
        {
            Pruefe.Gleich((814.0, 522.0), TooltipLage.Berechnen(800, 500, 300, 100, B, H, null));
            var (x, y) = TooltipLage.Berechnen(1800, 500, 300, 100, B, H, Hand(1800, 500));
            Pruefe.Wahr(x + 300 <= B - 4 && !new Rechteck(x, y, 300, 100).Schneidet(Hand(1800, 500)), "rechts passt nicht: unter der Hand, im Bild");
        }),
    };
}
