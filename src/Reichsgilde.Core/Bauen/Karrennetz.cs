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
//
//  Wasser (Schritt 5c): Brücken sind Straße wie jede andere. Ein Stück in einer Furt ist eine eigene Fahrbahn (Karren warten dort, die
//  Logistik rechnet die Minuten dazu); für die Wahl des Weges zählt ein Meter Furt wie 10 m Straße. Eine Fähre ist eine Kante zwischen
//  ihren beiden Anlegern (sie verbindet die Straßen, die dort enden, und wird von keiner Straße geteilt); für die Wahl des Weges zählt sie
//  wie 300 m Umweg. Ein Stück Straße in einem See fehlt im Netz (gesperrt) – anderes Wasser ohne Übergang liegt ohnehin nicht auf Straßen.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Worauf ein Stück einer Fahrt liegt.</summary>
public enum Fahrbahn
{
    Strasse,
    Bruecke,
    Furt,
    Faehre,
}

/// <summary>
/// Eine Linie des Netzes: die Mittellinie einer Karrenstraße mit ihren Stellen am Wasser (Meter entlang der Linie: Brücke, Furt; null =
/// gesperrt) – oder eine Fähre (zwei Punkte, die beiden Anleger).
/// </summary>
public sealed record Netzlinie(IReadOnlyList<Kartenort> Linie, IReadOnlyList<(double Von, double Bis, Fahrbahn? Art)>? Stellen = null, bool IstFaehre = false);

/// <summary>
/// Ein Stück einer Fahrt: Länge in Metern und Anstieg in Metern (positiv = bergauf in Fahrtrichtung), dazu die Fahrbahn (Straße, Brücke,
/// Furt, Fähre).
/// </summary>
public readonly record struct Fahrtstueck(double Laenge, double Anstieg, Fahrbahn Art = Fahrbahn.Strasse)
{
    /// <summary>Die Steigung als Anteil (0,1 = 10 %); 0 bei Länge 0.</summary>
    public double Steigung => Laenge > 0 ? Anstieg / Laenge : 0;
}

/// <summary>Das Netz der Karrenstraßen (siehe Kopf der Datei).</summary>
public sealed class Karrennetz
{
    /// <summary>Punkte höchstens so weit auseinander sind ein Knoten; so nah an einer Strecke hängt ein Straßenende an ihr (Meter).</summary>
    public const double Fang = 1;

    /// <summary>Für die Wahl des Weges zählt ein Meter Furt so viel wie so viele Meter Straße.</summary>
    public const double FurtKosten = 10;

    /// <summary>Für die Wahl des Weges zählt eine Fähre wie so viele Meter Umweg (dazu ihre Länge).</summary>
    public const double FaehreUmweg = 300;

    private readonly List<Kartenort> _knoten = new();

    /// <summary>Die Linie, die den Knoten angelegt hat – Punkte derselben Linie verschmelzen nur, wenn sie gleich sind.</summary>
    private readonly List<int> _linieVon = new();
    private readonly List<List<(int Ziel, double Laenge, double Anstieg, Fahrbahn Art, double Kosten)>> _nachbarn = new();

    /// <summary>Die Kanten einmal (von, nach, Anstieg von → nach, Fahrbahn, Kosten für die Wegwahl) – zum Anhängen eines Ortes.</summary>
    private readonly List<(int A, int B, double Anstieg, Fahrbahn Art, double Kosten)> _kanten = new();

    private readonly Dictionary<(long, long), List<int>> _raster = new();

    /// <summary>
    /// Das Netz der Karrenstraßen einer Karte, mit den Höhenprofilen ihres Geländes und dem Wasser: Brücken und Furten aus dem
    /// <see cref="Stadtkarte.Wasserplan"/>, Stücke im See gesperrt, die Fähren als Kanten.
    /// </summary>
    public static Karrennetz Von(Stadtkarte karte)
    {
        var plan = karte.Wasserplan;
        var linien = new List<Netzlinie>();
        for (var i = 0; i < karte.Wege.Count; i++)
        {
            var w = karte.Wege[i];
            if (!w.IstKarrenstrasse || w.Linie.Count < 2) continue;
            var stellen = plan.Querungen.Where(q => q.Weg == i).Select(q => (q.VonStelle, q.BisStelle, (Fahrbahn?)(q.IstBruecke ? Fahrbahn.Bruecke : Fahrbahn.Furt)))
                .Concat(plan.Sperren.Where(x => x.Weg == i).Select(x => (x.VonStelle, x.BisStelle, (Fahrbahn?)null))).ToList();
            linien.Add(new Netzlinie(w.Verlauf.Select(Kartenort.Aus).ToList(), stellen.Count == 0 ? null : stellen));
        }
        linien.AddRange(plan.Faehren.Select(f => new Netzlinie(new[] { f.Von, f.Bis }, IstFaehre: true)));
        return new Karrennetz(linien, karte.Gelaendeform);
    }

    /// <param name="linien">Die Mittellinien der Karrenstraßen.</param>
    /// <param name="gelaende">Für die Höhen (Profil jeder Linie); flach: alle Anstiege 0.</param>
    public Karrennetz(IReadOnlyList<IReadOnlyList<Kartenort>> linien, Gelaendeform gelaende)
        : this(linien.Select(l => new Netzlinie(l)).ToList(), gelaende)
    {
    }

    /// <param name="netzlinien">Die Karrenstraßen mit ihren Stellen am Wasser und die Fähren.</param>
    /// <param name="gelaende">Für die Höhen (Profil jeder Linie); flach: alle Anstiege 0.</param>
    public Karrennetz(IReadOnlyList<Netzlinie> netzlinien, Gelaendeform gelaende)
    {
        var linien = netzlinien.Select(n => n.Linie).ToList();
        // alle Strecken mit ihren Höhen an beiden Enden, ihrer Fahrbahn und ob andere Linien sie teilen dürfen (eine Fähre nicht)
        var strecken = new List<(Kartenort A, Kartenort B, double HA, double HB, int Linie, Fahrbahn Art, bool Teilbar)>();
        for (var l = 0; l < linien.Count; l++)
        {
            var n = netzlinien[l];
            if (n.IstFaehre)
            {
                if (n.Linie.Count >= 2 && n.Linie[0].Abstand(n.Linie[^1]) > 1e-9) strecken.Add((n.Linie[0], n.Linie[^1], 0, 0, l, Fahrbahn.Faehre, false));
                continue;
            }
            // das Profil höchstens alle 1 m (eine gerade Straße hat sonst nur ihre Endpunkte und läge flach über jedem Hügel)
            var p = gelaende.Profillinie(linien[l]);
            var stelle = 0.0;
            for (var k = 0; k + 1 < p.Count; k++)
            {
                var laenge = p[k].Ort.Abstand(p[k + 1].Ort);
                var mitte = stelle + laenge / 2;
                stelle += laenge;
                if (laenge <= 1e-9) continue;
                var art = Fahrbahn.Strasse;
                var gesperrt = false;
                foreach (var (von, bis, a) in n.Stellen ?? Array.Empty<(double, double, Fahrbahn?)>())
                    if (mitte >= von && mitte <= bis)
                    {
                        if (a is { } x) art = x;
                        else gesperrt = true;
                    }
                if (!gesperrt) strecken.Add((p[k].Ort, p[k + 1].Ort, p[k].Hoehe, p[k + 1].Hoehe, l, art, true));
            }
        }
        // je Strecke die Stellen (0 … 1), an denen sie geteilt wird: Kreuzungen und Straßenenden anderer Linien
        var teilung = strecken.Select(_ => new List<double> { 0, 1 }).ToList();
        var faecher = new Dictionary<(int, int), List<int>>();
        const double zelle = 25;
        (int, int) Zelle(double x, double z) => ((int)Math.Floor(x / zelle), (int)Math.Floor(z / zelle));
        for (var s = 0; s < strecken.Count; s++)
        {
            var (a, b, _, _, _, _, _) = strecken[s];
            var (z0, z1) = (Zelle(Math.Min(a.X, b.X) - Fang, Math.Min(a.Z, b.Z) - Fang), Zelle(Math.Max(a.X, b.X) + Fang, Math.Max(a.Z, b.Z) + Fang));
            for (var i = z0.Item1; i <= z1.Item1; i++)
                for (var j = z0.Item2; j <= z1.Item2; j++)
                {
                    if (!faecher.TryGetValue((i, j), out var liste)) faecher[(i, j)] = liste = new List<int>();
                    liste.Add(s);
                }
        }
        var ende = linien.Select(p => p.Count == 0 ? (default(Kartenort), default(Kartenort)) : (p[0], p[^1])).ToList();
        foreach (var liste in faecher.Values)
            foreach (var s in liste)
                foreach (var t in liste)
                {
                    if (s >= t || strecken[s].Linie == strecken[t].Linie) continue;
                    var (a, b, _, _, ls, _, sTeilbar) = strecken[s];
                    var (c, d, _, _, lt, _, tTeilbar) = strecken[t];
                    if (sTeilbar && tTeilbar && Kartengeometrie.Schnitt(a, b, c, d) is { } x)
                    {
                        teilung[s].Add(Stelle(a, b, x));
                        teilung[t].Add(Stelle(c, d, x));
                    }
                    // ein Straßenende (oder Anleger) knapp neben der anderen Strecke – eine Fähre selbst wird nie geteilt
                    if (sTeilbar)
                        foreach (var e in new[] { ende[lt].Item1, ende[lt].Item2 })
                            if (Kartengeometrie.AbstandStrecke(e, a, b) <= Fang) teilung[s].Add(Stelle(a, b, e));
                    if (tTeilbar)
                        foreach (var e in new[] { ende[ls].Item1, ende[ls].Item2 })
                            if (Kartengeometrie.AbstandStrecke(e, c, d) <= Fang) teilung[t].Add(Stelle(c, d, e));
                }
        // Knoten und Kanten
        for (var s = 0; s < strecken.Count; s++)
        {
            var (a, b, ha, hb, linie, art, _) = strecken[s];
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
                    var kosten = art switch
                    {
                        Fahrbahn.Furt => laenge * FurtKosten,
                        Fahrbahn.Faehre => laenge + FaehreUmweg,
                        _ => laenge,
                    };
                    _nachbarn[vorher].Add((k, laenge, h - hVorher, art, kosten));
                    _nachbarn[k].Add((vorher, laenge, hVorher - h, art, kosten));
                    _kanten.Add((vorher, k, h - hVorher, art, kosten));
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
        _nachbarn.Add(new List<(int, double, double, Fahrbahn, double)>());
        if (!_raster.TryGetValue((ci, cj), out var neu)) _raster[(ci, cj)] = neu = new List<int>();
        neu.Add(_knoten.Count - 1);
        return _knoten.Count - 1;
    }

    /// <summary>
    /// Wo ein Ort am Netz hängt: die nächste Kante (keine Fähre), die Stelle darauf (0 … 1) und der Abstand. Null bei einem leeren Netz.
    /// </summary>
    private (int Kante, double T)? Anhaengen(Kartenort p)
    {
        var best = (Kante: -1, T: 0.0, Abstand: double.MaxValue);
        for (var k = 0; k < _kanten.Count; k++)
        {
            if (_kanten[k].Art == Fahrbahn.Faehre) continue;
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
            return new[] { new Fahrtstueck(Math.Abs(ziel.T - start.T) * sLaenge, (ziel.T - start.T) * s.Anstieg, s.Art) };

        // Dijkstra nach den Kosten (Straße und Brücke: die Länge; Furt und Fähre teurer), begonnen an beiden Enden der Startkante
        var abstand = new double[_knoten.Count];
        var vor = new int[_knoten.Count];
        Array.Fill(abstand, double.MaxValue);
        Array.Fill(vor, -1);
        var offen = new PriorityQueue<int, double>();
        (abstand[s.A], abstand[s.B]) = (start.T * s.Kosten, (1 - start.T) * s.Kosten);
        offen.Enqueue(s.A, abstand[s.A]);
        offen.Enqueue(s.B, abstand[s.B]);
        while (offen.TryDequeue(out var k, out var d))
        {
            if (d > abstand[k]) continue;
            foreach (var (n, _, _, _, kosten) in _nachbarn[k])
                if (d + kosten < abstand[n])
                {
                    (abstand[n], vor[n]) = (d + kosten, k);
                    offen.Enqueue(n, abstand[n]);
                }
        }
        // ins Ziel über das nähere Ende der Zielkante
        var ueberA = abstand[z.A] + ziel.T * z.Kosten;
        var ueberB = abstand[z.B] + (1 - ziel.T) * z.Kosten;
        if (Math.Min(ueberA, ueberB) >= double.MaxValue / 2) return Array.Empty<Fahrtstueck>();
        var letzter = ueberA <= ueberB ? z.A : z.B;

        var stuecke = new List<Fahrtstueck>();
        // das letzte Stück: vom Knoten auf der Zielkante bis zur Stelle
        stuecke.Add(letzter == z.A ? new Fahrtstueck(ziel.T * zLaenge, ziel.T * z.Anstieg, z.Art) : new Fahrtstueck((1 - ziel.T) * zLaenge, -(1 - ziel.T) * z.Anstieg, z.Art));
        var knoten = letzter;
        while (vor[knoten] >= 0)
        {
            var v = vor[knoten];
            var kante = _nachbarn[v].Where(x => x.Ziel == knoten).OrderBy(x => x.Kosten).ThenBy(x => x.Laenge).First();
            stuecke.Add(new Fahrtstueck(kante.Laenge, kante.Anstieg, kante.Art));
            knoten = v;
        }
        // das erste Stück: von der Stelle auf der Startkante bis zu ihrem Knoten
        stuecke.Add(knoten == s.A ? new Fahrtstueck(start.T * sLaenge, -start.T * s.Anstieg, s.Art) : new Fahrtstueck((1 - start.T) * sLaenge, (1 - start.T) * s.Anstieg, s.Art));
        stuecke.Reverse();
        return stuecke.Where(x => x.Laenge > 1e-9).ToList();
    }
}
