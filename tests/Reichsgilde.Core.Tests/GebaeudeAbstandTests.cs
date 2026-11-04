using Reichsgilde.Core.Bauen;

/// <summary>Der Abstand zum nächsten Gebäude (Naturfreiraum.GebaeudeAbstand): Grundlage des weichen Grasrands am Haus.</summary>
public static class GebaeudeAbstandTests
{
    static Naturfreiraum Mit(params Naturhindernis[] h) =>
        new(Array.Empty<(IReadOnlyList<double[]>, double)>(), h, Array.Empty<IReadOnlyList<double[]>>(), Array.Empty<IReadOnlyList<double[]>>());

    static void Nah(double soll, double ist, string was) => Pruefe.Wahr(Math.Abs(soll - ist) < 1e-6, $"{was}: {ist} statt {soll}");

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("GebaeudeAbstand: Rechteck innen negativ, an der Kante 0, draußen wachsend, an der Ecke der echte Abstand", () =>
        {
            var f = Mit(new Naturhindernis(10, 20, 0, 4, 3, false));
            Nah(-1, f.GebaeudeAbstand(13, 20), "1 m innen an der Seitenwand");
            Nah(0, f.GebaeudeAbstand(14, 20), "auf der Kante");
            Nah(1.5, f.GebaeudeAbstand(15.5, 20), "1,5 m daneben");
            Nah(Math.Sqrt(2), f.GebaeudeAbstand(15, 24), "Ecke");
            Nah(3, f.GebaeudeAbstand(10, 40), "gekappt auf Naturfreiraum.Abstand");
            Nah(1, f.GebaeudeAbstand(10, 40, 1), "gekappt auf den gewünschten Wert");
        }),

        ("GebaeudeAbstand: gedrehtes Rechteck wie AufGebaeude, kein Gebäude in der Nähe = Höchstwert", () =>
        {
            var h = new Naturhindernis(0, 0, 90, 5, 2, false);
            var f = Mit(h);
            for (var x = -8.0; x <= 8; x += 0.7)
                for (var z = -8.0; z <= 8; z += 0.7)
                    Pruefe.Wahr((f.GebaeudeAbstand(x, z) < 0.3) == f.AufGebaeude(x, z, 0.3) || Math.Abs(f.GebaeudeAbstand(x, z) - 0.3) < 0.35, $"({x} | {z})");
            Nah(3, Mit().GebaeudeAbstand(1, 2), "ohne Gebäude");
        }),
    };
}
