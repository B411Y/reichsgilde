namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Felspinsel P3b: Gebäude und Wege beim Streuen aussparen
//
//  Eine reine, rasterbasierte Abfrage ohne Physik: Wo liegen Grundflächen von Gebäuden (mit Vorplatz), Wege (Mittellinie ± halbe Breite) und,
//  wenn gewollt, Bauzonen – und berührt die Grundfläche eines neuen Felsstücks (Kreis um seine Mitte) eine davon? Der Streuen-Strich fragt sie je
//  Versuch; ein Stück, das etwas berührt, wird verworfen wie eines, das zu nah an einem anderen Stück liegt. Stempel, Gizmo und Radierer fragen sie
//  nicht (Bauen auf Fels bleibt erlaubt, Entscheidung 01.11.2026). Nur Zahlen und Listen in fester Reihenfolge: deterministisch.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Die Hindernisse eines Strichs (Gebäude, Wege, Bauzonen) in einem groben Suchraster; <see cref="Belegt"/> prüft einen Kreis.</summary>
public sealed class Felsaussparung
{
    /// <summary>Kantenlänge einer Suchzelle (Meter).</summary>
    public const double Zelle = 16;

    private readonly List<Hindernis> _liste = new();
    private readonly Dictionary<(int, int), List<int>> _zellen = new();

    private sealed record Hindernis(IReadOnlyList<Kartenort> Punkte, bool Flaeche, double Halbbreite);

    /// <summary>
    /// Baut die Abfrage: <paramref name="flaechen"/> sind Vielecke (Gebäude, Bauzonen), <paramref name="wege"/> Linien mit ihrer Breite (Meter).
    /// Leere Listen, Vielecke unter drei Ecken und Linien unter einem Punkt zählen nicht.
    /// </summary>
    public Felsaussparung(IEnumerable<IReadOnlyList<Kartenort>> flaechen, IEnumerable<(IReadOnlyList<Kartenort> Linie, double Breite)> wege)
    {
        foreach (var f in flaechen) if (f.Count >= 3) Dazu(new Hindernis(f, true, 0));
        foreach (var (linie, breite) in wege) if (linie.Count >= 1) Dazu(new Hindernis(linie, false, Math.Max(0, breite) / 2));
    }

    /// <summary>Die Gebäude (Grundfläche mit Vorplatz), Wege und – mit <paramref name="bauzonen"/> – Bauzonen eines Entwurfs.</summary>
    public static Felsaussparung Von(Kartenentwurf e, bool bauzonen)
    {
        var flaechen = e.Gebaeude.Select(g => g.Ecken()).ToList();
        if (bauzonen) flaechen.AddRange(e.Bauzonen.Select(z => z.Ecken));
        return new Felsaussparung(flaechen, e.Wege.Select(w => (w.Verlauf, w.BreiteWirksam)));
    }

    /// <summary>Wie viele Hindernisse gezählt werden.</summary>
    public int Anzahl => _liste.Count;

    private static (int, int) ZelleBei(double x, double z) => ((int)Math.Floor(x / Zelle), (int)Math.Floor(z / Zelle));

    private void Dazu(Hindernis h)
    {
        var nr = _liste.Count;
        _liste.Add(h);
        var (x0, z0, x1, z1) = (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        foreach (var p in h.Punkte) (x0, z0, x1, z1) = (Math.Min(x0, p.X), Math.Min(z0, p.Z), Math.Max(x1, p.X), Math.Max(z1, p.Z));
        var (i0, j0) = ZelleBei(x0 - h.Halbbreite, z0 - h.Halbbreite);
        var (i1, j1) = ZelleBei(x1 + h.Halbbreite, z1 + h.Halbbreite);
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
            {
                if (!_zellen.TryGetValue((i, j), out var l)) _zellen[(i, j)] = l = new();
                l.Add(nr);
            }
    }

    /// <summary>Berührt der Kreis um (x, z) mit diesem Radius (Meter) eine Fläche oder einen Weg?</summary>
    public bool Belegt(double x, double z, double radius)
    {
        if (_liste.Count == 0) return false;
        // ein Kreis kann in mehreren Zellen liegen; die Zelle seiner Mitte genügt nicht, darum alle Zellen im Quadrat des Kreises
        var (i0, j0) = ZelleBei(x - radius, z - radius);
        var (i1, j1) = ZelleBei(x + radius, z + radius);
        var p = new Kartenort(x, z);
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
                if (_zellen.TryGetValue((i, j), out var l))
                    foreach (var nr in l)
                        if (Beruehrt(_liste[nr], p, radius)) return true;
        return false;
    }

    private static bool Beruehrt(Hindernis h, Kartenort p, double radius)
    {
        if (h.Flaeche)
        {
            if (Kartengeometrie.ImVieleck(p, h.Punkte)) return true;
            foreach (var (a, b) in Kartengeometrie.Strecken(h.Punkte))
                if (Kartengeometrie.AbstandStrecke(p, a, b) <= radius) return true;
            return false;
        }
        return Kartengeometrie.AbstandLinie(p, h.Punkte) <= radius + h.Halbbreite;
    }
}
