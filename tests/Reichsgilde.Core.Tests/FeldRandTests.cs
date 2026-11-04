using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Tests;

/// <summary>Felder „bis zum Rand“ (nur Darstellung): Stücke, die den Rand der Fläche überragen, damit er gerade zugeschnitten werden kann.</summary>
public static class FeldRandTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Felder bis zum Rand: mehr Stücke als ohne, nie weniger, Rand schräg wird abgedeckt", () =>
        {
            var frei = new Naturfreiraum(Array.Empty<(IReadOnlyList<double[]>, double)>(), Enumerable.Empty<Naturhindernis>(), Array.Empty<IReadOnlyList<double[]>>(), Array.Empty<IReadOnlyList<double[]>>());
            // ein schräges Viereck (Raute), Streifen längs der x-Achse
            var feld = new Feldflaeche
            {
                Name = "Raute", Flaeche = new List<double[]> { new[] { 0.0, 0 }, new[] { 60.0, 20 }, new[] { 0.0, 80 }, new[] { -60.0, 20 } },
                Richtung = 0, Streifen = 8, Laenge = 40, Startwert = 5, Fruechte = new Dictionary<string, double> { ["weizen"] = 1 },
            };
            var streng = FeldVerteilung.Flaeche(feld, frei);
            var genau = FeldVerteilung.Flaeche(feld, frei, bisZumRand: true);
            Pruefe.Wahr(streng.Count > 0 && genau.Count >= streng.Count, $"{streng.Count} gegen {genau.Count}");
            Pruefe.Wahr(genau.Any(p => p.Ecken().Any(e => !Geometrie.ImVieleck(e.X, e.Z, feld.Flaeche))), "mindestens ein Stück ragt über den Rand");
            // jeder Punkt der Fläche liegt in einem Stück (kein Loch am Rand); an den Spitzen kann ein Rest unter 2 m wegfallen
            bool Spitze(double x, double z) => feld.Flaeche.Any(e => Math.Abs(e[0] - x) < 6 && Math.Abs(e[1] - z) < 6);
            var luecken = new List<string>();
            for (var x = -55.0; x <= 55; x += 2.5)
                for (var z = 2.0; z <= 78; z += 2.5)
                    if (!Spitze(x, z) && Geometrie.ImVieleck(x, z, feld.Flaeche) && !genau.Any(p => Geometrie.ImVieleck(x, z, p.Ecken().Select(e => new[] { e.X, e.Z }).ToList())))
                        luecken.Add($"({x} | {z})");
            Pruefe.Wahr(luecken.Count == 0, "Lücken bei " + string.Join(", ", luecken) + " | Stücke bei x=-36: " + string.Join("; ", genau.Where(p => Math.Abs(p.X + 36) < 1).Select(p => $"z {p.Z} l {p.Laenge} d {p.Drehung} b {p.Breite}")));
            Pruefe.Wahr(genau.SequenceEqual(FeldVerteilung.Flaeche(feld, frei, bisZumRand: true)), "deterministisch");
        }),
    };
}
