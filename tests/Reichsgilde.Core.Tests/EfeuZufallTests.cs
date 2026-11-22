using Reichsgilde;

/// <summary>Efeu-Zufall der Elbenhäuser: fest aus der Platz-Saat, in Grenzen, je Haus verschieden.</summary>
static class EfeuZufallTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("EfeuZufall: dieselbe Saat gibt dieselben Werte", () =>
        {
            for (uint s = 0; s < 50; s++) Pruefe.Gleich(EfeuZufall.Ableiten(s * 7919u + 3), EfeuZufall.Ableiten(s * 7919u + 3), "deterministisch");
        }),
        ("EfeuZufall: Werte liegen in den Grenzen", () =>
        {
            for (uint s = 0; s < 2000; s++)
            {
                var (v, d, f) = EfeuZufall.Ableiten(s * 2654435761u);
                Pruefe.Wahr(v >= 0f && v < 1f, "Versatz");
                Pruefe.Wahr(d >= EfeuZufall.DichteMin && d <= EfeuZufall.DichteMax, "Dichte");
                Pruefe.Wahr(f >= -1f && f <= 1f, "Farbton");
            }
        }),
        ("EfeuZufall: verschiedene Häuser sehen verschieden aus", () =>
        {
            var menge = new HashSet<(float, float, float)>();
            for (uint s = 0; s < 200; s++) menge.Add(EfeuZufall.Ableiten(s * 2654435761u + 11));
            Pruefe.Wahr(menge.Count > 190, "fast alle verschieden: " + menge.Count);
            var dichten = Enumerable.Range(0, 200).Select(s => EfeuZufall.Ableiten((uint)s * 2654435761u + 11).Dichte).ToList();
            Pruefe.Wahr(dichten.Max() - dichten.Min() > 0.4f, "Dichte streut breit");
        }),
    };
}
