using Reichsgilde;

/// <summary>
/// Passanten bleiben mit ihrem Körper aus Ständen, Bänken und Brunnen heraus (Darstellung, 22.11.2026): <c>Marktflaeche.HerausSchieben</c> schiebt einen
/// Punkt aus Kreisen und gedrehten Rechtecken, bis der Abstand mindestens der Körperradius ist; wer schon frei steht, bleibt unverändert.
/// </summary>
static class MarktabstandTests
{
    const float Koerper = 0.24f;

    static Marktflaeche Platz()
    {
        var f = new Marktflaeche(0, 0, 1, 0, 12);
        f.Kreis(0, 0, 2.8f);
        f.Stand(new Marktflaeche.Rechteck(8, 8, 0.6f, 0.8f, 1.45f, 0.9f)); // gedrehter Stand
        f.Rechteckig(new Marktflaeche.Rechteck(10.6f, -3, 1, 0, 1.25f, 0.26f)); // Bank
        return f;
    }

    static void Gleich(float a, float b, float eps, string was)
    {
        if (MathF.Abs(a - b) > eps) throw new Exception($"{was}: {a} statt {b}");
    }

    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Marktabstand: Freier Punkt bleibt unverändert", () =>
        {
            var (x, z) = Platz().HerausSchieben(-6, 6, Koerper);
            Gleich(x, -6, 1e-5f, "x"); Gleich(z, 6, 1e-5f, "z");
        });
        yield return ("Marktabstand: Aus dem Brunnenbecken heraus, radial", () =>
        {
            var f = Platz();
            var (x, z) = f.HerausSchieben(1, 0.5f, Koerper);
            if (f.Abstand(x, z) < Koerper - 1e-3f) throw new Exception("noch im Becken");
            Gleich(MathF.Sqrt(x * x + z * z), 2.8f + Koerper, 1e-3f, "Radius");
        });
        yield return ("Marktabstand: Mitte des Brunnens ist ein gültiger Punkt", () =>
        {
            var f = Platz();
            var (x, z) = f.HerausSchieben(0, 0, Koerper);
            if (f.Abstand(x, z) < Koerper - 1e-3f) throw new Exception("noch im Becken");
        });
        yield return ("Marktabstand: Aus dem gedrehten Stand heraus, innen und am Rand", () =>
        {
            var f = Platz();
            foreach (var (px, pz) in new[] { (8f, 8f), (8.3f, 8.2f), (9.5f, 8.9f), (7.2f, 7.6f) })
            {
                var (x, z) = f.HerausSchieben(px, pz, Koerper);
                if (f.Abstand(x, z) < Koerper - 1e-3f) throw new Exception($"({px}|{pz}) bleibt im Stand, Abstand {f.Abstand(x, z)}");
            }
        });
        yield return ("Marktabstand: Von der Bank weg, nie weiter als nötig", () =>
        {
            var f = Platz();
            var (x, z) = f.HerausSchieben(10.6f, -2.9f, Koerper);
            Gleich(f.Abstand(x, z), Koerper, 1e-3f, "Abstand zur Bank");
            if (MathF.Abs(x - 10.6f) > 1e-3f) throw new Exception("seitlich verschoben");
        });
        yield return ("Marktabstand: Engstelle zwischen zwei Ständen – der Punkt bleibt in der Lücke und wird nie schlechter", () =>
        {
            var f = new Marktflaeche(0, 0, 1, 0, 12);
            f.Stand(new Marktflaeche.Rechteck(-1, 0, 1, 0, 1.0f, 1f)); // Kante bei x = 0
            f.Stand(new Marktflaeche.Rechteck(1.3f, 0, 1, 0, 1.0f, 1f)); // Kante bei x = 0,3: Lücke 0,3 m, enger als ein Körper
            var vorher = f.Abstand(0.2f, 0);
            var (x, z) = f.HerausSchieben(0.2f, 0, Koerper);
            if (f.Abstand(x, z) < vorher - 1e-4f) throw new Exception("schlechter als vorher");
            if (f.Abstand(x, z) > 0.15f + 1e-4f || f.Abstand(x, z) < 0.09f) throw new Exception($"Abstand {f.Abstand(x, z)} liegt nicht in der Lücke (höchstens 0,15 m möglich)");
        });
    }
}
