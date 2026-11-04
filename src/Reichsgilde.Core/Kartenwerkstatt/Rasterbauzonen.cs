using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Bauzonen aus dem 2-m-Raster („frei heißt bebaubar“, Variante A, Nutzer 4.11.2026)
//
//  Jedes 2-m-Feld des Spielfelds, das frei ist, gehört zu einer Bauzone seiner Lage. Frei heißt: Abstand zu Wegen (halbe Breite + 0,5 m),
//  Gebäuden (2 m), Mauern und Wasser (2 m, Köln 3 m) wie in den Kartenregeln, nicht auf einer von Hand gezeichneten Zone, kein steiler Hang (über 7 %
//  im steilsten 8-×-8-m-Fenster, wie die Hangsperre der Prüfung misst). Geprüft werden Ecken, Kantenmitten und Mitte des Feldes.
//  Die Lage eines Feldes kommt aus den groben Lage-Gebieten der Karte („lagen“, von Hand: Kern, Mauerring, Vorstadt, Umland oder „keine“ =
//  hier nicht bauen; das erste Gebiet, das die Feldmitte enthält, gilt) oder aus einer Regel des Stadtskripts (Köln).
//  Zusammenhängende Felder einer Lage werden eine Zone, deren Umriss die Ränder der Felder nachzeichnet (Löcher – ein Haus mitten im Block –
//  teilt ein senkrechter Schnitt). Zu kleine Stücke (unter 24 Feldern) und Felder ohne Karrenstraße in der Nähe werden keine Zone, sondern
//  Restflächen (in Kern, Mauerring und Vorstadt): Platz für Gärten und Höfe der Darstellung.
//  Gerechnet wird beim Kartenbau (Stadtskript, Umbau, Befehl „bauzonen“), nie in der Partie: Das Ergebnis steht als „bauzonen“ (mit
//  "berechnet": true) und „restflaechen“ in der Datei. Herkunft: die Köln-Rechnung von tools/Kartenbau/Staedte/Koeln.Zonen.cs (PR #163 ff.).
//  Alle Mengen werden in fester Reihenfolge (nach Feldnummer) durchlaufen, damit dieselbe Karte immer dieselben Zonen ergibt.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein grobes Lage-Gebiet (Feld <c>lagen</c>): Lage (null = „keine“, hier nicht bauen), Umriss und ein Name zum Wiederfinden.</summary>
public sealed record Lagegebiet(Lage? Art, IReadOnlyList<Kartenort> Ecken, string Name = "")
{
    /// <summary>Der Text für „art“: die Lage klein oder „keine“.</summary>
    public string ArtText => Art is { } a ? a.ToString().ToLowerInvariant() : Keine;

    /// <summary>„art“ eines Gebiets, in dem nicht gebaut wird.</summary>
    public const string Keine = "keine";
}

/// <summary>Die Stellschrauben der Rechnung; die Vorgaben sind die von Köln (PR #163, #285).</summary>
public sealed record Rastervorgaben
{
    /// <summary>Die Lage eines Feldes (an seiner Mitte) – null: nach den Lage-Gebieten der Karte (<see cref="Kartenentwurf.Lagen"/>).</summary>
    public Func<Kartenort, Lage?>? LageVon { get; init; }

    /// <summary>Wo gerechnet wird – null: das Spielfeld. Nur Felder, die ganz darin liegen.</summary>
    public Kartenrechteck? Bereich { get; init; }

    /// <summary>Ein Feld (Mitte) höchstens so weit (m) von einer Karrenstraße, sonst kein Haus mehr an der Straße – null: ohne Grenze.</summary>
    public double? KarrenabstandFeld { get; init; } = 30;

    /// <summary>Eine Zone (eine Ecke ihres Umrisses) höchstens so weit (m) von einer Karrenstraße – null: ohne Grenze.</summary>
    public double? KarrenabstandZone { get; init; } = 7.5;

    /// <summary>So viele Felder braucht eine Zone mindestens (24 = 96 m²); kleinere Stücke werden Restflächen.</summary>
    public int MindestFelder { get; init; } = 24;

    /// <summary>Abstand (m) zu Mauern: wie die Prüfung (<see cref="Kartenpruefung.AbstandMauer"/>, 2 m); Köln rechnet seit PR #163 mit 3 m.</summary>
    public double Mauerabstand { get; init; } = Kartenpruefung.AbstandMauer;

    /// <summary>Abstand (m) zum Ufer: 2 m wie zu Gebäuden und Mauern (die Prüfung verbietet nur Zonen im Wasser); Köln rechnet mit 3 m.</summary>
    public double Uferabstand { get; init; } = 2;

    /// <summary>Steiler (Gefälle im steilsten 8-×-8-m-Fenster um die Feldmitte) wird nicht gebaut; die Prüfung meldet ab 8 %.</summary>
    public double Hanggrenze { get; init; } = 0.07;

    public static Rastervorgaben Standard { get; } = new();
}

/// <summary>Eine berechnete Zone: Lage, Umriss in Metern (auf dem Raster), Anzahl Felder.</summary>
public sealed record Rasterzone(Lage Art, IReadOnlyList<Kartenort> Umriss, int Felder);

/// <summary>Das Ergebnis: die Zonen (in der Reihenfolge, in der sie angelegt werden) und die Restflächen.</summary>
public sealed record Rasterergebnis(IReadOnlyList<Rasterzone> Zonen, IReadOnlyList<Restflaeche> Restflaechen)
{
    /// <summary>Zonen und Felder je Lage (für Berichte).</summary>
    public IReadOnlyList<(Lage Art, int Zonen, int Felder)> JeLage =>
        Enum.GetValues<Lage>().Select(l => (l, Zonen.Count(z => z.Art == l), Zonen.Where(z => z.Art == l).Sum(z => z.Felder)))
            .Where(x => x.Item2 > 0).ToList();
}

/// <summary>Die Rechnung selbst (ändert den Entwurf nicht; das tut <see cref="Kartenentwurf.BauzonenAusRaster"/>).</summary>
public static class Rasterbauzonen
{
    /// <summary>Rastergröße (m) – das Raster der Bauzonen und Spielerbauten.</summary>
    public const double Raster = 2;

    /// <summary>Abstände (m), in denen ein Feld gesperrt ist: zum Wegrand und zu Gebäuden wie die Prüfung (Mauer und Ufer: <see cref="Rastervorgaben"/>).</summary>
    public const double Wegrand = Kartenpruefung.AbstandStrasse, Gebaeudeabstand = Kartenpruefung.AbstandFeste;

    /// <summary>Rundungsspielraum (m) beim Abstand zum Ufer.</summary>
    private const double Toleranz = 1e-6;

    /// <summary>Restflächen gibt es in diesen Lagen (die Stadt); im Umland bleibt freies Land Wiese.</summary>
    public static bool InDerStadt(Lage l) => l != Lage.Umland;

    public static Rasterergebnis Rechnen(Kartenentwurf e, Rastervorgaben v)
    {
        var lagen = e.Lagen;
        var lageVon = v.LageVon ?? (p =>
        {
            foreach (var g in lagen)
                if (Kartengeometrie.ImVieleck(p, g.Ecken)) return g.Art;
            return null;
        });
        var bereich = v.Bereich ?? e.Kartengrenzen.Spielfeld;
        var (i0, i1) = ((int)Math.Ceiling(bereich.X0 / Raster - 1e-9), (int)Math.Floor(bereich.X1 / Raster + 1e-9) - 1);
        var (j0, j1) = ((int)Math.Ceiling(bereich.Z0 / Raster - 1e-9), (int)Math.Floor(bereich.Z1 / Raster + 1e-9) - 1);

        // Was Felder sperrt, mit umschließendem Rechteck zum schnellen Aussortieren
        var wege = e.Wege.Where(w => w.Verlauf.Count >= 2)
            .Select(w => (Linie: w.Verlauf, Abstand: w.BreiteWirksam / 2 + Wegrand, Rahmen: Rahmen(w.Verlauf, w.BreiteWirksam / 2 + Wegrand + 1))).ToList();
        var gebaeude = e.Gebaeude.Select(g => g.Ecken()).Where(ecken => ecken.Count > 0)
            .Select(ecken => (Ecken: ecken, Rahmen: Rahmen(ecken, Gebaeudeabstand + 1))).ToList();
        var mauern = e.Mauern.Where(m => m.Linie.Count >= 2).Select(m => (Linie: m.Linie, Rahmen: Rahmen(m.Linie, v.Mauerabstand + 1))).ToList();
        var vonHand = e.Bauzonen.Where(z => !z.Berechnet && z.Ecken.Count >= 3)
            .Select(z => (Ecken: z.Ecken, Rand: z.Ecken.Append(z.Ecken[0]).ToList(), Rahmen: Rahmen(z.Ecken, 1))).ToList();
        var hang = e.Gelaendeform;
        var wasser = e.Wasserform;

        static bool Im((double X0, double Z0, double X1, double Z1) r, Kartenort p) => p.X >= r.X0 && p.X <= r.X1 && p.Z >= r.Z0 && p.Z <= r.Z1;

        bool Gesperrt(Kartenort p)
        {
            foreach (var w in wege)
                if (Im(w.Rahmen, p) && Kartengeometrie.AbstandLinie(p, w.Linie) < w.Abstand) return true;
            foreach (var g in gebaeude)
                if (Im(g.Rahmen, p) && (Kartengeometrie.ImVieleck(p, g.Ecken) ? 0 : Kartengeometrie.AbstandLinie(p, g.Ecken.Append(g.Ecken[0]).ToList())) < Gebaeudeabstand)
                    return true;
            foreach (var m in mauern)
                if (Im(m.Rahmen, p) && Kartengeometrie.AbstandLinie(p, m.Linie) < v.Mauerabstand) return true;
            // von Hand gezeichnete Zonen gehen vor: Felder darauf und daran (auch auf dem Rand) bleiben frei für sie
            foreach (var z in vonHand)
                if (Im(z.Rahmen, p) && (Kartengeometrie.ImVieleck(p, z.Ecken) || Kartengeometrie.AbstandLinie(p, z.Rand) < 1e-6)) return true;
            return wasser.Uferabstand(p.X, p.Z, 8) < v.Uferabstand - Toleranz;   // genau 2 m ist frei (die Rechnung liefert 1,9999…)
        }

        var karren = e.Wege.Where(w => w.IstKarrenstrasse && w.Verlauf.Count >= 2).Select(w => w.Verlauf).ToList();
        double Karrenabstand(Kartenort p) => karren.Count == 0 ? double.PositiveInfinity : karren.Min(l => Kartengeometrie.AbstandLinie(p, l));

        bool Frei(int i, int j, Kartenort mitte)
        {
            for (var dx = 0; dx <= 2; dx++)
                for (var dz = 0; dz <= 2; dz++)
                    if (Gesperrt(new Kartenort((i + dx / 2.0) * Raster, (j + dz / 2.0) * Raster))) return false;
            // steiles Gelände lassen wir aus (gemessen wie die Hangsperre der Prüfung)
            return !hang.HatHoehen || hang.Hang(Quadrat(mitte, 8)).Gefaelle <= v.Hanggrenze;
        }

        // Felder (sortiert nach i, dann j): die für Zonen und die abseits der Karrenstraßen (nur für Restflächen)
        var zellen = new SortedDictionary<(int I, int J), Lage>();
        var rest = new SortedDictionary<(int I, int J), Lage>();
        for (var i = i0; i <= i1; i++)
            for (var j = j0; j <= j1; j++)
            {
                var mitte = new Kartenort(i * Raster + Raster / 2, j * Raster + Raster / 2);
                if (lageVon(mitte) is not { } lage) continue;
                var abseits = v.KarrenabstandFeld is { } kf && Karrenabstand(mitte) > kf;   // dahinter käme kein Haus mehr an die Straße
                if (abseits && !InDerStadt(lage)) continue;
                if (!Frei(i, j, mitte)) continue;
                (abseits ? rest : zellen)[(i, j)] = lage;
            }

        // Einzelne Felder, die sich nur an der Ecke berühren, gäben einen Umriss mit Berührungspunkt: das erste davon weglassen (wird Restfläche)
        bool Entfernt()
        {
            foreach (var ((i, j), lage) in zellen)
                foreach (var (di, dj) in new[] { (1, 1), (1, -1) })
                    if (zellen.TryGetValue((i + di, j + dj), out var l2) && l2 == lage
                        && !zellen.ContainsKey((i + di, j)) && !zellen.ContainsKey((i, j + dj)))
                    {
                        zellen.Remove((i, j));
                        if (InDerStadt(lage)) rest[(i, j)] = lage;
                        return true;
                    }
            return false;
        }
        while (Entfernt()) { }

        var zonen = new List<Rasterzone>();
        foreach (var lage in Enum.GetValues<Lage>())
            foreach (var komponente in Komponenten(zellen.Where(z => z.Value == lage).Select(z => z.Key)))
                foreach (var (teil, schnitt) in OhneLoecher(komponente))
                {
                    if (InDerStadt(lage))
                        foreach (var f in schnitt) rest[f] = lage;   // die Felder der Schnitte
                    var umriss = teil.Count < v.MindestFelder ? null : Umriss(teil);
                    if (umriss is null || v.KarrenabstandZone is { } kz && umriss.Min(Karrenabstand) > kz)
                    {
                        if (InDerStadt(lage)) foreach (var f in teil) rest[f] = lage;
                        continue;
                    }
                    zonen.Add(new Rasterzone(lage, umriss, teil.Count));
                }

        // Restflächen: zusammenhängende Reste je Lage; „klein“ unter der Mindestgröße einer Zone, sonst „abseits“
        var restflaechen = new List<Restflaeche>();
        foreach (var lage in Enum.GetValues<Lage>().Where(InDerStadt))
            foreach (var komponente in Komponenten(rest.Where(z => z.Value == lage).Select(z => z.Key)))
                foreach (var (teil, _) in OhneLoecher(komponente))
                    restflaechen.Add(new Restflaeche
                    {
                        Kennung = $"R{restflaechen.Count + 1}", Art = lage, Grund = teil.Count < v.MindestFelder ? Restflaeche.Klein : Restflaeche.Abseits,
                        Felder = teil.Count, Flaeche = Umriss(teil).Select(p => new[] { p.X, p.Z }).ToList(),
                    });
        return new Rasterergebnis(zonen, restflaechen);
    }

    // ── Hilfen (Felder als (i, j): Feld i, j reicht von i · 2 bis i · 2 + 2 m) ──

    private static List<Kartenort> Quadrat(Kartenort mitte, double seite) => new()
    {
        new(mitte.X - seite / 2, mitte.Z - seite / 2), new(mitte.X + seite / 2, mitte.Z - seite / 2),
        new(mitte.X + seite / 2, mitte.Z + seite / 2), new(mitte.X - seite / 2, mitte.Z + seite / 2),
    };

    private static (double X0, double Z0, double X1, double Z1) Rahmen(IReadOnlyList<Kartenort> punkte, double rand) =>
        (punkte.Min(p => p.X) - rand, punkte.Min(p => p.Z) - rand, punkte.Max(p => p.X) + rand, punkte.Max(p => p.Z) + rand);

    /// <summary>Zusammenhängende Felder (über die vier Seiten), geordnet nach ihrem kleinsten Feld.</summary>
    internal static List<SortedSet<(int I, int J)>> Komponenten(IEnumerable<(int I, int J)> felder)
    {
        var rest = new SortedSet<(int I, int J)>(felder);
        var liste = new List<SortedSet<(int I, int J)>>();
        while (rest.Count > 0)
        {
            var start = rest.Min;
            var komponente = new SortedSet<(int I, int J)>();
            var stapel = new Stack<(int I, int J)>();
            stapel.Push(start);
            rest.Remove(start);
            while (stapel.Count > 0)
            {
                var f = stapel.Pop();
                komponente.Add(f);
                foreach (var n in new[] { (f.I + 1, f.J), (f.I - 1, f.J), (f.I, f.J + 1), (f.I, f.J - 1) })
                    if (rest.Remove(n)) stapel.Push(n);
            }
            liste.Add(komponente);
        }
        return liste;
    }

    /// <summary>
    /// Teilt eine Komponente mit Löchern (ein Haus mitten im Block) mit einem senkrechten Schnitt vom Loch nach oben (−z), bis jede Teilfläche
    /// genau einen Umriss hat (eine Zone kennt keine Löcher). Gibt die Teile und je Teil die Felder seines Schnitts zurück (beim letzten Teil).
    /// </summary>
    internal static IEnumerable<(SortedSet<(int I, int J)> Teil, IReadOnlyList<(int I, int J)> Schnitt)> OhneLoecher(SortedSet<(int I, int J)> komponente)
    {
        var umrisse = Schleifen(komponente);
        if (umrisse.Count <= 1)
        {
            yield return (komponente, Array.Empty<(int, int)>());
            yield break;
        }
        var loch = umrisse.First(s => Flaeche(s) < 0);
        var minJ = loch.Min(p => p.J);
        var ic = (int)Math.Floor((loch.Min(p => p.I) + loch.Max(p => p.I)) / 2.0);
        // Liegt über der Mitte des Lochs kein Feld (das Loch ist dort nicht am höchsten), schneiden wir an seiner obersten Kante links
        if (!komponente.Contains((ic, minJ - 1))) ic = loch.Where(p => p.J == minJ).Min(p => p.I);
        var kopie = new SortedSet<(int I, int J)>(komponente);
        var schnitt = new List<(int I, int J)>();
        for (var jo = minJ - 1; kopie.Remove((ic, jo)); jo--) schnitt.Add((ic, jo));
        var teile = Komponenten(kopie).SelectMany(OhneLoecher).ToList();
        for (var k = 0; k < teile.Count; k++)
            yield return k == teile.Count - 1 ? (teile[k].Teil, teile[k].Schnitt.Concat(schnitt).ToList()) : teile[k];
    }

    private static double Flaeche(List<(int I, int J)> schleife)
    {
        var a = 0.0;
        for (var k = 0; k < schleife.Count; k++)
        {
            var (p, q) = (schleife[k], schleife[(k + 1) % schleife.Count]);
            a += (double)p.I * q.J - (double)q.I * p.J;
        }
        return a / 2;
    }

    /// <summary>
    /// Alle Randschleifen einer Feldmenge als Gitterpunkte, außen im Uhrzeigersinn (Fläche positiv), Löcher entgegengesetzt. Wo sich zwei Felder
    /// nur an der Ecke berühren, biegt die Schleife zum eigenen Feld ab (rechts) – so bleibt jede Schleife einfach.
    /// </summary>
    internal static List<List<(int I, int J)>> Schleifen(SortedSet<(int I, int J)> felder)
    {
        var kanten = new SortedDictionary<(int I, int J), List<(int I, int J)>>();
        void Kante((int I, int J) von, (int I, int J) nach)
        {
            if (!kanten.TryGetValue(von, out var l)) kanten[von] = l = new List<(int I, int J)>();
            l.Add(nach);
        }
        foreach (var (i, j) in felder)
        {
            if (!felder.Contains((i, j - 1))) Kante((i, j), (i + 1, j));
            if (!felder.Contains((i + 1, j))) Kante((i + 1, j), (i + 1, j + 1));
            if (!felder.Contains((i, j + 1))) Kante((i + 1, j + 1), (i, j + 1));
            if (!felder.Contains((i - 1, j))) Kante((i, j + 1), (i, j));
        }
        var liste = new List<List<(int I, int J)>>();
        while (kanten.Count > 0)
        {
            var start = kanten.Keys.First();
            var schleife = new List<(int I, int J)>();
            var p = start;
            (int I, int J)? richtung = null;
            while (kanten.TryGetValue(p, out var aus))
            {
                var k = 0;
                if (aus.Count > 1 && richtung is { } r)
                    for (var m = 0; m < aus.Count; m++)
                        if (r.I * (aus[m].J - p.J) - r.J * (aus[m].I - p.I) > 0) k = m;   // rechts abbiegen
                var n = aus[k];
                aus.RemoveAt(k);
                if (aus.Count == 0) kanten.Remove(p);
                schleife.Add(p);
                richtung = (n.I - p.I, n.J - p.J);
                p = n;
                if (p == start) break;
            }
            liste.Add(schleife);
        }
        return liste;
    }

    /// <summary>Der äußere Umriss einer Feldmenge in Metern, ohne Punkte auf geraden Strecken.</summary>
    internal static List<Kartenort> Umriss(SortedSet<(int I, int J)> felder)
    {
        var schleife = Schleifen(felder).OrderByDescending(s => Math.Abs(Flaeche(s))).First();
        var punkte = new List<(int I, int J)>();
        for (var k = 0; k < schleife.Count; k++)
        {
            var (a, b, c) = (schleife[(k + schleife.Count - 1) % schleife.Count], schleife[k], schleife[(k + 1) % schleife.Count]);
            if ((b.I - a.I) * (c.J - b.J) - (b.J - a.J) * (c.I - b.I) != 0) punkte.Add(b);
        }
        return punkte.Select(p => new Kartenort(p.I * Raster, p.J * Raster)).ToList();
    }
}
