using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Staedte;

// Köln: Die Bauzonen entstehen aus dem 2-m-Raster. Jedes Feld, das frei ist (Abstand zu Wegen, Gebäuden, Mauer und Wasser wie in den
// Kartenregeln, kein steiler Hang), gehört zu einer Zone seiner Lage (Kern, Mauerring, Vorstadt, Umland); zusammenhängende Felder
// einer Lage werden eine Zone, deren Umriss die Ränder der Felder nachzeichnet.

public sealed partial class Koeln
{
    private const double Raster = 2;

    /// <summary>Umland-Zonen: Rechtecke (x0, z0, x1, z1) neben den Landstraßen, in denen freie Felder Umland-Bauzone werden.</summary>
    private static readonly (double X0, double Z0, double X1, double Z1)[] UmlandGebiete =
    {
        (-330, -40, -200, 16), (-330, 24, -200, 70), (-300, -130, -190, -80), (230, -60, 290, -14), (150, 110, 215, 170), (-70, -190, -20, -160),
    };

    private List<Kartenort> _stadtFlaeche = null!;

    private Lage? LageVon(Kartenort p)
    {
        // Deutz: innerhalb der kleinen Mauer
        if (p.X is > 124 and < 213 && p.Z is > -47 and < 41) return Lage.Vorstadt;
        // Altstadt: innerhalb der Mauer bis zum Rheinufer
        if (p.X < 57 && Kartengeometrie.ImVieleck(p, _stadtFlaeche))
            return p.Abstand(P(0, 0)) < 64 || p.Abstand(P(0, -44)) < 54 ? Lage.Kern : Lage.Mauerring;
        // Vorstädte vor den Toren: ein Streifen längs der Landstraßen
        if (p.X < 45 && p.Abstand(P(Cx, 0)) is > R + 6 and < R + 80 && _vorstadtWege.Any(w => Kartengeometrie.AbstandLinie(p, w.Verlauf) < 34)) return Lage.Vorstadt;
        foreach (var (x0, z0, x1, z1) in UmlandGebiete)
            if (p.X >= x0 && p.X <= x1 && p.Z >= z0 && p.Z <= z1) return Lage.Umland;
        return null;
    }

    private List<EntwurfWeg> _vorstadtWege = new();

    private double AbstandZuUmriss(Kartenort p, IReadOnlyList<Kartenort> ecken) =>
        Kartengeometrie.ImVieleck(p, ecken) ? 0 : Kartengeometrie.AbstandLinie(p, ecken.Append(ecken[0]).ToList());

    private void Bauzonen()
    {
        _stadtFlaeche = _stadtmauer.Ecken.ToList();
        _vorstadtWege = new[] { "Aachener Straße", "Venloer Straße", "Neusser Straße", "Luxemburger Straße", "Bonner Straße" }.Select(n => _strassen[n]).ToList();

        // Was Felder sperrt: Wege (halbe Breite + 0,5 m), Gebäude (2 m), Mauern (3 m), Wasser (3 m), steiles Gelände
        var wege = E.Wege.Select(w => (Linie: w.Verlauf, Abstand: w.BreiteWirksam / 2 + 0.5, Rahmen: Rahmen(w.Verlauf, w.BreiteWirksam / 2 + 0.5 + 1))).ToList();
        var gebaeude = E.Gebaeude.Where(g => g.Ecken().Count > 0)
            .Select(g => (Ecken: g.Ecken(), Rahmen: Rahmen(g.Ecken(), 2 + 1))).ToList();
        var mauern = E.Mauern.Select(m => (Linie: m.Ecken, Rahmen: Rahmen(m.Ecken, 3 + 1))).ToList();
        var hoehen = E.Gelaendeform.Roh;

        bool Gesperrt(Kartenort p)
        {
            foreach (var w in wege)
                if (p.X >= w.Rahmen.X0 && p.X <= w.Rahmen.X1 && p.Z >= w.Rahmen.Z0 && p.Z <= w.Rahmen.Z1 && Kartengeometrie.AbstandLinie(p, w.Linie) < w.Abstand) return true;
            foreach (var g in gebaeude)
                if (p.X >= g.Rahmen.X0 && p.X <= g.Rahmen.X1 && p.Z >= g.Rahmen.Z0 && p.Z <= g.Rahmen.Z1 && AbstandZuUmriss(p, g.Ecken) < 2) return true;
            foreach (var m in mauern)
                if (p.X >= m.Rahmen.X0 && p.X <= m.Rahmen.X1 && p.Z >= m.Rahmen.Z0 && p.Z <= m.Rahmen.Z1 && Kartengeometrie.AbstandLinie(p, m.Linie) < 3) return true;
            return E.Wasserform.Uferabstand(p.X, p.Z, 8) < 3;
        }

        var karren = E.Wege.Where(w => w.IstKarrenstrasse).Select(w => w.Verlauf).ToList();
        var zellen = new Dictionary<(int I, int J), Lage>();
        for (var i = -170; i < 125; i++)
            for (var j = -98; j < 123; j++)   // z von −196 bis 246: das Spielfeld reicht von −200 bis 250
            {
                var mitte = P(i * Raster + Raster / 2, j * Raster + Raster / 2);
                if (LageVon(mitte) is not { } lage) continue;
                if (karren.Min(l => Kartengeometrie.AbstandLinie(mitte, l)) > 30) continue;   // dahinter käme kein Haus mehr an die Straße
                var frei = true;
                for (var dx = 0; dx <= 2 && frei; dx++)
                    for (var dz = 0; dz <= 2 && frei; dz++)
                        if (Gesperrt(P((i + dx / 2.0) * Raster, (j + dz / 2.0) * Raster))) frei = false;
                if (!frei) continue;
                // steiles Gelände lassen wir aus: die Hangsperre der Prüfung liegt bei 8 % (Hinweis) und 15 % (Fehler), gemessen wie dort
                // im steilsten 8-×-8-m-Fenster
                if (!hoehen.IstLeer && E.Gelaendeform.Hang(Rechteck(mitte, 8, 8, 0)).Gefaelle > 0.07) continue;
                zellen[(i, j)] = lage;
            }

        // Einzelne Felder, die sich nur an der Ecke berühren, würden einen Umriss mit Berührungspunkt geben: eines davon weglassen
        bool Entfernt()
        {
            foreach (var ((i, j), lage) in zellen.ToList())
            {
                if (!zellen.ContainsKey((i, j))) continue;
                foreach (var (di, dj) in new[] { (1, 1), (1, -1) })
                    if (zellen.TryGetValue((i + di, j + dj), out var l2) && l2 == lage
                        && !zellen.ContainsKey((i + di, j)) && !zellen.ContainsKey((i, j + dj)))
                    {
                        zellen.Remove((i, j));
                        return true;
                    }
            }
            return false;
        }
        while (Entfernt()) { }

        var anzahl = new Dictionary<Lage, (int Zonen, int Felder)>();
        foreach (var lage in Enum.GetValues<Lage>())
            foreach (var komponente in Komponenten(zellen.Where(z => z.Value == lage).Select(z => z.Key).ToHashSet()))
                foreach (var teil in OhneLoecher(komponente))
                {
                    if (teil.Count < 24) continue;
                    var umriss = Umriss(teil);
                    if (umriss.Min(p => karren.Min(l => Kartengeometrie.AbstandLinie(p, l))) > 7.5) continue;   // Köln: Zone höchstens 8 m von einer Karrenstraße
                    E.BauzoneAnlegen(lage, umriss);
                    anzahl[lage] = anzahl.TryGetValue(lage, out var a) ? (a.Zonen + 1, a.Felder + teil.Count) : (1, teil.Count);
                }
        foreach (var (lage, a) in anzahl) Console.WriteLine($"   Bauzonen {lage}: {a.Zonen} Zonen, {a.Felder} Felder");
    }

    private static (double X0, double Z0, double X1, double Z1) Rahmen(IReadOnlyList<Kartenort> punkte, double rand) =>
        (punkte.Min(p => p.X) - rand, punkte.Min(p => p.Z) - rand, punkte.Max(p => p.X) + rand, punkte.Max(p => p.Z) + rand);

    /// <summary>Zusammenhängende Felder (über die vier Seiten).</summary>
    private static List<HashSet<(int I, int J)>> Komponenten(HashSet<(int I, int J)> felder)
    {
        var rest = new HashSet<(int I, int J)>(felder);
        var liste = new List<HashSet<(int I, int J)>>();
        while (rest.Count > 0)
        {
            var start = rest.First();
            var komponente = new HashSet<(int I, int J)>();
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
    /// Teilt eine Komponente mit Löchern (ein Haus mitten im Block) mit einem senkrechten Schnitt vom Loch nach oben, bis jede Teilfläche genau
    /// einen Umriss hat (eine Zone kennt keine Löcher).
    /// </summary>
    private static IEnumerable<HashSet<(int I, int J)>> OhneLoecher(HashSet<(int I, int J)> komponente)
    {
        var umrisse = Schleifen(komponente);
        if (umrisse.Count <= 1) { yield return komponente; yield break; }
        var loch = umrisse.First(s => Flaeche(s) < 0);
        var ic = (int)Math.Floor((loch.Min(p => p.I) + loch.Max(p => p.I)) / 2.0);
        var jo = loch.Min(p => p.J) - 1;
        var kopie = new HashSet<(int I, int J)>(komponente);
        while (kopie.Remove((ic, jo))) jo--;
        foreach (var teil in Komponenten(kopie).SelectMany(OhneLoecher)) yield return teil;
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

    /// <summary>Alle Randschleifen einer Feldmenge, im Uhrzeigersinn (Löcher entgegengesetzt), als Gitterpunkte.</summary>
    private static List<List<(int I, int J)>> Schleifen(HashSet<(int I, int J)> felder)
    {
        var kanten = new Dictionary<(int I, int J), (int I, int J)>();
        foreach (var (i, j) in felder)
        {
            if (!felder.Contains((i, j - 1))) kanten[(i, j)] = (i + 1, j);
            if (!felder.Contains((i + 1, j))) kanten[(i + 1, j)] = (i + 1, j + 1);
            if (!felder.Contains((i, j + 1))) kanten[(i + 1, j + 1)] = (i, j + 1);
            if (!felder.Contains((i - 1, j))) kanten[(i, j + 1)] = (i, j);
        }
        var liste = new List<List<(int I, int J)>>();
        while (kanten.Count > 0)
        {
            var start = kanten.Keys.First();
            var schleife = new List<(int I, int J)>();
            var p = start;
            do
            {
                schleife.Add(p);
                var n = kanten[p];
                kanten.Remove(p);
                p = n;
            } while (p != start && kanten.ContainsKey(p));
            liste.Add(schleife);
        }
        return liste;
    }

    /// <summary>Der äußere Umriss einer Feldmenge in Metern, ohne Punkte auf geraden Strecken.</summary>
    private static List<Kartenort> Umriss(HashSet<(int I, int J)> felder)
    {
        var schleife = Schleifen(felder).OrderByDescending(s => Math.Abs(Flaeche(s))).First();
        var punkte = new List<(int I, int J)>();
        for (var k = 0; k < schleife.Count; k++)
        {
            var (a, b, c) = (schleife[(k + schleife.Count - 1) % schleife.Count], schleife[k], schleife[(k + 1) % schleife.Count]);
            if ((b.I - a.I) * (c.J - b.J) - (b.J - a.J) * (c.I - b.I) != 0) punkte.Add(b);
        }
        return punkte.Select(p => P(p.I * Raster, p.J * Raster)).ToList();
    }
}
