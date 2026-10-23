using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Das Netz der Karrenstraßen einer Karte – für die Fahrzeit der Karren am Hang (Kartenwerkstatt, Schritt 5b)
//
//  Die Simulation kennt keine Strecken: Ein Karren braucht vom Betrieb zum Markt eine feste Zeit je Arbeitsort (logistik.fahrzeiten).
//  Mit Gelände kommt die Steigung dazu: Der Kern sucht einmal je Betrieb den kürzesten Weg über die Karrenstraßen (Land-, Haupt-, Ring-,
//  Wallstraße, Feldweg) vom Betrieb zum Markt – wie das Gassennetz der Darstellung nach der Länge – und merkt sich je Stück Länge und
//  Anstieg (aus dem Höhenprofil der Straße, Gelaendeform.Profillinie: höchstens alle 1 m ein Punkt). Daraus rechnet die Logistik einen
//  Zeitfaktor für hin und zurück.
//
//  So entsteht das Netz: Jeder Punkt des Profils einer Karrenstraße ist ein Knoten; Punkte verschiedener Straßen, die höchstens 1 m
//  auseinander liegen, sind derselbe Knoten (eingerastete Kreuzungen teilen ohnehin einen Punkt). Kreuzen sich zwei Strecken mitten
//  drin oder endet eine Straße höchstens 1 m neben einer anderen, wird dort ein Knoten eingefügt. Ein Ort (Betrieb, Markt) hängt am
//  nächsten Punkt einer Strecke. Ohne Zufall: gleiche Karte, gleiche Wege.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Stück einer Fahrt: Länge in Metern und Anstieg in Metern (positiv = bergauf in Fahrtrichtung).</summary>
public readonly record struct Fahrtstueck(double Laenge, double Anstieg)
{
    /// <summary>Die Steigung als Anteil (0,1 = 10 %); 0 bei Länge 0.</summary>
    public double Steigung => Laenge > 0 ? Anstieg / Laenge : 0;
}

/// <summary>Das Netz der Karrenstraßen (siehe Kopf der Datei).</summary>
public sealed class Karrennetz
{
    /// <summary>Punkte höchstens so weit auseinander sind ein Knoten; so nah an einer Strecke hängt ein Straßenende an ihr (Meter).</summary>
    public const double Fang = 1;

    private readonly List<Kartenort> _knoten = new();

    /// <summary>Die Linie, die den Knoten angelegt hat – Punkte derselben Linie verschmelzen nur, wenn sie gleich sind.</summary>
    private readonly List<int> _linieVon = new();
    private readonly List<List<(int Ziel, double Laenge, double Anstieg)>> _nachbarn = new();

    /// <summary>Die Kanten einmal (von, nach, Anstieg von → nach) – zum Anhängen eines Ortes.</summary>
    private readonly List<(int A, int B, double Anstieg)> _kanten = new();

    private readonly Dictionary<(long, long), List<int>> _raster = new();

    /// <summary>Das Netz der Karrenstraßen einer Karte, mit den Höhenprofilen ihres Geländes.</summary>
    public static Karrennetz Von(Stadtkarte karte) =>
        new(karte.Wege.Where(w => w.IstKarrenstrasse && w.Linie.Count >= 2).Select(w => (IReadOnlyList<Kartenort>)w.Verlauf.Select(Kartenort.Aus).ToList()).ToList(),
            karte.Gelaendeform);

    /// <param name="linien">Die Mittellinien der Karrenstraßen.</param>
    /// <param name="gelaende">Für die Höhen (Profil jeder Linie); flach: alle Anstiege 0.</param>
    public Karrennetz(IReadOnlyList<IReadOnlyList<Kartenort>> linien, Gelaendeform gelaende)
    {
        // alle Strecken mit ihren Höhen an beiden Enden
        var strecken = new List<(Kartenort A, Kartenort B, double HA, double HB, int Linie)>();
        for (var l = 0; l < linien.Count; l++)
        {
            // das Profil höchstens alle 1 m (eine gerade Straße hat sonst nur ihre Endpunkte und läge flach über jedem Hügel)
            var p = gelaende.Profillinie(linien[l]);
            for (var k = 0; k + 1 < p.Count; k++)
                if (p[k].Ort.Abstand(p[k + 1].Ort) > 1e-9) strecken.Add((p[k].Ort, p[k + 1].Ort, p[k].Hoehe, p[k + 1].Hoehe, l));
        }
        // je Strecke die Stellen (0 … 1), an denen sie geteilt wird: Kreuzungen und Straßenenden anderer Linien
        var teilung = strecken.Select(_ => new List<double> { 0, 1 }).ToList();
        var faecher = new Dictionary<(int, int), List<int>>();
        const double zelle = 25;
        (int, int) Zelle(double x, double z) => ((int)Math.Floor(x / zelle), (int)Math.Floor(z / zelle));
        for (var s = 0; s < strecken.Count; s++)
        {
            var (a, b, _, _, _) = strecken[s];
            var (z0, z1) = (Zelle(Math.Min(a.X, b.X) - Fang, Math.Min(a.Z, b.Z) - Fang), Zelle(Math.Max(a.X, b.X) + Fang, Math.Max(a.Z, b.Z) + Fang));
            for (var i = z0.Item1; i <= z1.Item1; i++)
                for (var j = z0.Item2; j <= z1.Item2; j++)
                {
                    if (!faecher.TryGetValue((i, j), out var liste)) faecher[(i, j)] = liste = new List<int>();
                    liste.Add(s);
                }
        }
        var ende = linien.Select(p => (p[0], p[^1])).ToList();
        foreach (var liste in faecher.Values)
            foreach (var s in liste)
                foreach (var t in liste)
                {
                    if (s >= t || strecken[s].Linie == strecken[t].Linie) continue;
                    var (a, b, _, _, ls) = strecken[s];
                    var (c, d, _, _, lt) = strecken[t];
                    if (Kartengeometrie.Schnitt(a, b, c, d) is { } x)
                    {
                        teilung[s].Add(Stelle(a, b, x));
                        teilung[t].Add(Stelle(c, d, x));
                    }
                    // ein Straßenende knapp neben der anderen Strecke
                    foreach (var e in new[] { ende[lt].Item1, ende[lt].Item2 })
                        if (Kartengeometrie.AbstandStrecke(e, a, b) <= Fang) teilung[s].Add(Stelle(a, b, e));
                    foreach (var e in new[] { ende[ls].Item1, ende[ls].Item2 })
                        if (Kartengeometrie.AbstandStrecke(e, c, d) <= Fang) teilung[t].Add(Stelle(c, d, e));
                }
        // Knoten und Kanten
        for (var s = 0; s < strecken.Count; s++)
        {
            var (a, b, ha, hb, linie) = strecken[s];
            var stellen = teilung[s].Distinct().OrderBy(t => t).ToList();
            var vorher = -1;
            var hVorher = 0.0;
            foreach (var t in stellen)
            {
                var p = new Kartenort(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
                var h = ha + (hb - ha) * t;
                var k = Knoten(p, linie);
                if (vorher >= 0 && vorher != k)
                {
                    var laenge = _knoten[vorher].Abstand(_knoten[k]);
                    _nachbarn[vorher].Add((k, laenge, h - hVorher));
                    _nachbarn[k].Add((vorher, laenge, hVorher - h));
                    _kanten.Add((vorher, k, h - hVorher));
                }
                (vorher, hVorher) = (k, h);
            }
        }

        static double Stelle(Kartenort a, Kartenort b, Kartenort p)
        {
            var (dx, dz) = (b.X - a.X, b.Z - a.Z);
            return Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / (dx * dx + dz * dz), 0, 1);
        }
    }

    /// <summary>Wie viele Knoten und Kanten das Netz hat (für Tests).</summary>
    public (int Knoten, int Kanten) Groesse => (_knoten.Count, _kanten.Count);

    /// <summary>
    /// Der Knoten an diesem Ort: ein vorhandener am selben Punkt (1 cm) oder einer einer anderen Linie höchstens 1 m entfernt (der nächste,
    /// bei gleichem Abstand der zuerst angelegte), sonst ein neuer.
    /// </summary>
    private int Knoten(Kartenort p, int linie)
    {
        var (ci, cj) = ((long)Math.Floor(p.X / Fang), (long)Math.Floor(p.Z / Fang));
        var best = (Index: -1, Abstand: double.MaxValue);
        for (var i = ci - 1; i <= ci + 1; i++)
            for (var j = cj - 1; j <= cj + 1; j++)
                if (_raster.TryGetValue((i, j), out var liste))
                    foreach (var k in liste)
                    {
                        var d = _knoten[k].Abstand(p);
                        if ((d <= 0.01 || (d <= Fang && _linieVon[k] != linie)) && (d < best.Abstand || (d == best.Abstand && k < best.Index))) best = (k, d);
                    }
        if (best.Index >= 0) return best.Index;
        _knoten.Add(p);
        _linieVon.Add(linie);
        _nachbarn.Add(new List<(int, double, double)>());
        if (!_raster.TryGetValue((ci, cj), out var neu)) _raster[(ci, cj)] = neu = new List<int>();
        neu.Add(_knoten.Count - 1);
        return _knoten.Count - 1;
    }

    /// <summary>Wo ein Ort am Netz hängt: die nächste Kante, die Stelle darauf (0 … 1) und der Abstand. Null bei einem leeren Netz.</summary>
    private (int Kante, double T)? Anhaengen(Kartenort p)
    {
        var best = (Kante: -1, T: 0.0, Abstand: double.MaxValue);
        for (var k = 0; k < _kanten.Count; k++)
        {
            var (a, b) = (_knoten[_kanten[k].A], _knoten[_kanten[k].B]);
            var (dx, dz) = (b.X - a.X, b.Z - a.Z);
            var t = Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / (dx * dx + dz * dz), 0, 1);
            var d = p.Abstand(new Kartenort(a.X + dx * t, a.Z + dz * t));
            if (d < best.Abstand) best = (k, t, d);
        }
        return best.Kante < 0 ? null : (best.Kante, best.T);
    }

    /// <summary>
    /// Die Fahrt über das Netz von <paramref name="von"/> nach <paramref name="nach"/>: der kürzeste Weg zwischen den Stellen, an denen
    /// die beiden Orte am Netz hängen, als Stücke mit Länge und Anstieg in Fahrtrichtung. Leer, wenn das Netz leer ist oder die beiden
    /// Stellen nicht verbunden sind.
    /// </summary>
    public IReadOnlyList<Fahrtstueck> Fahrt(Kartenort von, Kartenort nach)
    {
        if (Anhaengen(von) is not { } start || Anhaengen(nach) is not { } ziel) return Array.Empty<Fahrtstueck>();
        var (s, z) = (_kanten[start.Kante], _kanten[ziel.Kante]);
        var sLaenge = _knoten[s.A].Abstand(_knoten[s.B]);
        var zLaenge = _knoten[z.A].Abstand(_knoten[z.B]);
        // beide auf derselben Kante: das Stück dazwischen
        if (start.Kante == ziel.Kante)
            return new[] { new Fahrtstueck(Math.Abs(ziel.T - start.T) * sLaenge, (ziel.T - start.T) * s.Anstieg) };

        // Dijkstra nach der Länge, begonnen an beiden Enden der Startkante
        var abstand = new double[_knoten.Count];
        var vor = new int[_knoten.Count];
        Array.Fill(abstand, double.MaxValue);
        Array.Fill(vor, -1);
        var offen = new PriorityQueue<int, double>();
        (abstand[s.A], abstand[s.B]) = (start.T * sLaenge, (1 - start.T) * sLaenge);
        offen.Enqueue(s.A, abstand[s.A]);
        offen.Enqueue(s.B, abstand[s.B]);
        while (offen.TryDequeue(out var k, out var d))
        {
            if (d > abstand[k]) continue;
            foreach (var (n, laenge, _) in _nachbarn[k])
                if (d + laenge < abstand[n])
                {
                    (abstand[n], vor[n]) = (d + laenge, k);
                    offen.Enqueue(n, abstand[n]);
                }
        }
        // ins Ziel über das nähere Ende der Zielkante
        var ueberA = abstand[z.A] + ziel.T * zLaenge;
        var ueberB = abstand[z.B] + (1 - ziel.T) * zLaenge;
        if (Math.Min(ueberA, ueberB) >= double.MaxValue / 2) return Array.Empty<Fahrtstueck>();
        var letzter = ueberA <= ueberB ? z.A : z.B;

        var stuecke = new List<Fahrtstueck>();
        // das letzte Stück: vom Knoten auf der Zielkante bis zur Stelle
        stuecke.Add(letzter == z.A ? new Fahrtstueck(ziel.T * zLaenge, ziel.T * z.Anstieg) : new Fahrtstueck((1 - ziel.T) * zLaenge, -(1 - ziel.T) * z.Anstieg));
        var knoten = letzter;
        while (vor[knoten] >= 0)
        {
            var v = vor[knoten];
            var kante = _nachbarn[v].Where(x => x.Ziel == knoten).OrderBy(x => x.Laenge).First();
            stuecke.Add(new Fahrtstueck(kante.Laenge, kante.Anstieg));
            knoten = v;
        }
        // das erste Stück: von der Stelle auf der Startkante bis zu ihrem Knoten
        stuecke.Add(knoten == s.A ? new Fahrtstueck(start.T * sLaenge, -start.T * s.Anstieg) : new Fahrtstueck((1 - start.T) * sLaenge, (1 - start.T) * s.Anstieg));
        stuecke.Reverse();
        return stuecke.Where(x => x.Laenge > 1e-9).ToList();
    }
}
