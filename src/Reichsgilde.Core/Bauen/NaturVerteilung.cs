namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Wo die Natur wächst: Bäume, Büsche und Steine aus den Naturflächen der Karte
//
//  Bis zur Kartenwerkstatt (Schritt 2) streute nur die Darstellung (Stadtansicht.Natur.cs). Jetzt rechnet der Kern die Lagen, damit
//  Kartenwerkstatt und Spiel dieselben Bäume zeigen und „unter Gebäuden und Wegen bleibt es frei“ überall gleich gilt:
//
//    • Flaeche(…)  – eine Wald- oder Streufläche: so viele Streupunkte, wie die Dichte sagt (je 1.000 m² des Teils außerhalb der
//                    Aussparungen), gleich verteilt aus dem Startwert. An jedem Punkt eine Gruppe nach Gewicht; jeder ihrer Teile setzt
//                    seine Anzahl Kopien einer Art in eine Scheibe mit dem Radius der Gruppe. Dieselbe Reihenfolge der Würfe wie bisher in
//                    der Darstellung – die Greybox sieht aus wie vorher.
//    • Einzeln(…)  – eine einzeln gesetzte Pflanze (sie steht, wo sie gesetzt ist).
//    • Strich(…)   – der Pinsel der Kartenwerkstatt: locker verteilte Pflanzen entlang eines gezogenen Strichs.
//
//  Frei bleibt (Naturfreiraum): die Stadt innerhalb der Mauern und 6 m vor der Mauer, jeder Weg mit 3 m Rand, jedes Gebäude mit 3 m Rand
//  (ein Felsberg nur auf seiner Ellipse), jede Bauzone mit 3 m Rand, seit Schritt 5c jedes Gewässer mit 3 m Rand (Wasserform). Ein verworfener Punkt würfelt trotzdem weiter: Eine neue Straße
//  verschiebt keine anderen Bäume.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Eine Pflanze (oder ein Stein …), wie das Spiel sie zeichnet: Art, Ort, Drehung in Grad (wie <c>Rotation.Y</c>), Größe, Variante (1 …).</summary>
public readonly record struct Pflanzenlage(Baustein Art, double X, double Z, double Drehung, double Massstab, int Variante);

/// <summary>Ein Gebäude als Hindernis für die Natur: Mitte, Drehung (Grad), halbe Breite und Tiefe (ohne Rand); ein Felsberg als Ellipse.</summary>
public readonly record struct Naturhindernis(double X, double Z, double Drehung, double HalbX, double HalbZ, bool Ellipse)
{
    /// <summary>
    /// Das Hindernis eines Gebäudes: seine Fläche aus dem Katalog mal Maßstab. Umlandbetriebe und -stellen sind größer als ihre Fläche
    /// (Gelände, Zaun, Beete): <paramref name="modellmass"/> liefert die halben Maße des Modells um seinen Ursprung (ohne Maßstab), die
    /// Darstellung liest sie aus der Modelldatei. Null ohne Größe.
    /// </summary>
    public static Naturhindernis? Von(Baustein? b, double x, double z, double drehung, double massstab, Func<Baustein, (double X, double Z)?>? modellmass = null)
    {
        if (b?.Groesse is not { Length: 2 } g || massstab <= 0) return null;
        var (hx, hz) = (g[0] * massstab / 2, g[1] * massstab / 2);
        if (b.Art is Bausteinart.Umlandbetrieb or Bausteinart.Umlandstelle && modellmass?.Invoke(b) is var (mx, mz))
            (hx, hz) = (Math.Max(hx, mx * massstab), Math.Max(hz, mz * massstab));
        return new Naturhindernis(x, z, drehung, hx, hz, b.Art == Bausteinart.Berg);
    }

    /// <summary>Liegt der Punkt auf dem Hindernis, mit <paramref name="rand"/> Metern Rand (die Ellipse eines Felsbergs ohne Rand)?</summary>
    public bool Enthaelt(double px, double pz, double rand)
    {
        var w = Drehung * Math.PI / 180;
        var (c, s) = (Math.Cos(w), Math.Sin(w));
        var (dx, dz) = (px - X, pz - Z);
        var (lx, lz) = (dx * c - dz * s, dx * s + dz * c); // zurückgedreht: lokal x nach rechts, z zur Vorderseite
        if (Ellipse) return Math.Pow(lx / HalbX, 2) + Math.Pow(lz / HalbZ, 2) < 1;
        return Math.Abs(lx) < HalbX + rand && Math.Abs(lz) < HalbZ + rand;
    }

    /// <summary>Radius des Kreises um die Mitte, der das Hindernis mit Rand umschließt.</summary>
    internal double Umkreis(double rand) => Math.Sqrt((HalbX + rand) * (HalbX + rand) + (HalbZ + rand) * (HalbZ + rand));
}

/// <summary>
/// Wo nichts wächst: Stadt und Mauer, Wege, Gebäude, Bauzonen (siehe Kopf der Datei). Einmal bauen, dann viele Punkte fragen – die
/// Hindernisse liegen in einem Raster von 16-m-Feldern, damit eine Frage nur die Nachbarschaft ansieht.
/// </summary>
public sealed class Naturfreiraum
{
    /// <summary>So viel Abstand (Meter) hält die Natur zu Gebäuden, Bauzonen und zum Rand der Wege.</summary>
    public const double Abstand = 3;

    /// <summary>So viel Abstand (Meter) hält die Natur zur Mauer.</summary>
    public const double AbstandMauer = 6;

    private const double Feld = 16;

    private readonly List<(double Ax, double Az, double Bx, double Bz, double Halb)> _strecken = new();
    private readonly List<Naturhindernis> _gebaeude = new();
    private readonly List<(IReadOnlyList<double[]> Ecken, double X0, double Z0, double X1, double Z1)> _zonen = new();
    private readonly List<IReadOnlyList<double[]>> _mauern = new();
    private readonly Dictionary<(int, int), List<int>> _streckenRaster = new();
    private readonly Dictionary<(int, int), List<int>> _gebaeudeRaster = new();

    /// <param name="wege">Je Weg die Linie (Verlauf) und die gezeichnete Breite.</param>
    /// <param name="gebaeude">Die Gebäude als Hindernisse (<see cref="Naturhindernis.Von"/>).</param>
    /// <param name="bauzonen">Die Flächen der Bauzonen.</param>
    /// <param name="mauern">Die Mauerlinien (eine offene Linie gilt wie mit einer Geraden geschlossen, wie in der Stadtansicht).</param>
    public Naturfreiraum(IEnumerable<(IReadOnlyList<double[]> Linie, double Breite)> wege, IEnumerable<Naturhindernis> gebaeude,
        IEnumerable<IReadOnlyList<double[]>> bauzonen, IEnumerable<IReadOnlyList<double[]>> mauern)
    {
        foreach (var (linie, breite) in wege)
            for (var i = 0; i + 1 < linie.Count; i++)
            {
                var (a, b) = (linie[i], linie[i + 1]);
                var halb = breite / 2 + Abstand;
                _strecken.Add((a[0], a[1], b[0], b[1], halb));
                Eintragen(_streckenRaster, _strecken.Count - 1, Math.Min(a[0], b[0]) - halb, Math.Min(a[1], b[1]) - halb,
                    Math.Max(a[0], b[0]) + halb, Math.Max(a[1], b[1]) + halb);
            }
        foreach (var g in gebaeude)
        {
            _gebaeude.Add(g);
            var r = g.Umkreis(g.Ellipse ? 0 : Abstand);
            Eintragen(_gebaeudeRaster, _gebaeude.Count - 1, g.X - r, g.Z - r, g.X + r, g.Z + r);
        }
        foreach (var z in bauzonen.Where(z => z.Count >= 3))
            _zonen.Add((z, z.Min(p => p[0]), z.Min(p => p[1]), z.Max(p => p[0]), z.Max(p => p[1])));
        _mauern.AddRange(mauern.Where(m => m.Count >= 2));
    }

    /// <summary>Der Freiraum einer Stadtkarte (Wege mit ihrem Verlauf, Gebäude mit ihrer Fläche, Bauzonen, Mauern, Gewässer).</summary>
    public static Naturfreiraum Aus(Stadtkarte karte, Func<Baustein, (double X, double Z)?>? modellmass = null) => new(
        karte.Wege.Where(w => w.Linie.Count >= 2).Select(w => (w.Verlauf, w.BreiteWirksam)).Concat(Flussstuecke(karte.Gelaendeform.Wasser)),
        karte.Gebaeude.Select(g => Naturhindernis.Von(g.Baustein, g.Punkt[0], g.Punkt[1], g.Drehung, g.Massstab, modellmass)).OfType<Naturhindernis>(),
        karte.Bauzonen.Select(z => (IReadOnlyList<double[]>)z.Flaeche).Concat(Seeflaechen(karte.Gelaendeform.Wasser)),
        karte.Mauern.Select(m => (IReadOnlyList<double[]>)m.Linie));

    /// <summary>Die Flüsse als Stücke mit ihrer Breite (wie Wege: frei mit 3 m Rand).</summary>
    public static IEnumerable<(IReadOnlyList<double[]> Linie, double Breite)> Flussstuecke(Wasserform wasser) =>
        wasser.Laeufe.SelectMany(l => Enumerable.Range(0, Math.Max(0, l.Punkte.Count - 1)).Select(k => ((IReadOnlyList<double[]>)new[]
        {
            new[] { l.Punkte[k].Ort.X, l.Punkte[k].Ort.Z }, new[] { l.Punkte[k + 1].Ort.X, l.Punkte[k + 1].Ort.Z },
        }, Math.Max(l.Punkte[k].Breite, l.Punkte[k + 1].Breite))));

    /// <summary>Die Seen als Flächen (wie Bauzonen: frei mit 3 m Rand).</summary>
    public static IEnumerable<IReadOnlyList<double[]>> Seeflaechen(Wasserform wasser) =>
        wasser.Seen.Select(s => (IReadOnlyList<double[]>)s.Umriss.Select(p => new[] { p.X, p.Z }).ToList());

    private static void Eintragen(Dictionary<(int, int), List<int>> raster, int nummer, double x0, double z0, double x1, double z1)
    {
        for (var i = (int)Math.Floor(x0 / Feld); i <= (int)Math.Floor(x1 / Feld); i++)
            for (var j = (int)Math.Floor(z0 / Feld); j <= (int)Math.Floor(z1 / Feld); j++)
            {
                if (!raster.TryGetValue((i, j), out var liste)) raster[(i, j)] = liste = new List<int>();
                liste.Add(nummer);
            }
    }

    private static List<int>? Bei(Dictionary<(int, int), List<int>> raster, double x, double z) =>
        raster.GetValueOrDefault(((int)Math.Floor(x / Feld), (int)Math.Floor(z / Feld)));

    /// <summary>Darf hier etwas wachsen?</summary>
    public bool Frei(double x, double z) => !InDerStadt(x, z) && MauerAbstand(x, z) >= AbstandMauer && !AufWeg(x, z) && !AufGebaeude(x, z) && !AufBauzone(x, z);

    /// <summary>Liegt der Punkt innerhalb einer Mauer?</summary>
    public bool InDerStadt(double x, double z) => _mauern.Any(m => Geometrie.ImVieleck(x, z, m));

    /// <summary>Liegt der Punkt auf einem Weg, mit <paramref name="rand"/> Metern Rand (Vorgabe <see cref="Abstand"/>)?</summary>
    public bool AufWeg(double x, double z, double rand = Abstand)
    {
        if (Bei(_streckenRaster, x, z) is not { } liste) return false;
        foreach (var i in liste)
        {
            var s = _strecken[i];
            if (Geometrie.AbstandZurStrecke(x, z, s.Ax, s.Az, s.Bx, s.Bz) < s.Halb - Abstand + rand) return true;
        }
        return false;
    }

    /// <summary>Liegt der Punkt auf einem Gebäude, mit <paramref name="rand"/> Metern Rand (Vorgabe <see cref="Abstand"/>)?</summary>
    public bool AufGebaeude(double x, double z, double rand = Abstand)
    {
        if (Bei(_gebaeudeRaster, x, z) is not { } liste) return false;
        foreach (var i in liste)
            if (_gebaeude[i].Enthaelt(x, z, rand)) return true;
        return false;
    }

    private bool AufBauzone(double x, double z)
    {
        foreach (var (ecken, x0, z0, x1, z1) in _zonen)
        {
            if (x < x0 - Abstand || x > x1 + Abstand || z < z0 - Abstand || z > z1 + Abstand) continue;
            if (Geometrie.ImVieleck(x, z, ecken)) return true;
            for (var i = 0; i < ecken.Count; i++)
            {
                var (a, b) = (ecken[i], ecken[(i + 1) % ecken.Count]);
                if (Geometrie.AbstandZurStrecke(x, z, a[0], a[1], b[0], b[1]) < Abstand) return true;
            }
        }
        return false;
    }

    /// <summary>Der kleinste Abstand zu einer Mauerlinie (geschlossen gedacht, wie die Stadtansicht).</summary>
    private double MauerAbstand(double x, double z)
    {
        var best = double.MaxValue;
        foreach (var m in _mauern)
            for (var i = 0; i < m.Count; i++)
            {
                var (a, b) = (m[i], m[(i + 1) % m.Count]);
                best = Math.Min(best, Geometrie.AbstandZurStrecke(x, z, a[0], a[1], b[0], b[1]));
            }
        return best;
    }
}

/// <summary>Verteilt die Natur einer Karte deterministisch (siehe Kopf der Datei).</summary>
public static class NaturVerteilung
{
    /// <summary>
    /// Alle Pflanzen einer Stadtkarte: die Naturflächen in der Reihenfolge der Datei, dann die einzeln gesetzten. Für das Spiel und die
    /// Kartenwerkstatt; <paramref name="modellmass"/> wie bei <see cref="Naturhindernis.Von"/>.
    /// </summary>
    public static List<Pflanzenlage> Alle(Stadtkarte karte, Func<Baustein, (double X, double Z)?>? modellmass = null)
    {
        var liste = new List<Pflanzenlage>();
        if (karte.Katalog is not { } katalog) return liste;
        var frei = Naturfreiraum.Aus(karte, modellmass);
        foreach (var f in karte.Natur.Flaechen) liste.AddRange(Flaeche(f, katalog, frei));
        foreach (var p in karte.Natur.Pflanzen)
            if (Einzeln(p, katalog) is { } lage) liste.Add(lage);
        return liste;
    }

    /// <summary>
    /// Die Pflanzen einer Wald- oder Streufläche (siehe Kopf der Datei): gleiche Fläche, gleicher Startwert, gleiche Hindernisse – gleiche
    /// Pflanzen. Arten, die der Katalog nicht als Natur kennt, fallen weg (ohne den Zufall zu verschieben).
    /// </summary>
    public static List<Pflanzenlage> Flaeche(Naturflaeche flaeche, Gebaeudekatalog katalog, Naturfreiraum frei)
    {
        var liste = new List<Pflanzenlage>();
        var aussen = flaeche.Flaeche.Where(p => p is { Length: 2 }).ToList();
        if (aussen.Count < 3 || flaeche.Gruppen.Count == 0) return liste;
        var loecher = flaeche.Aussparungen.Select(l => l.Where(p => p is { Length: 2 }).ToList()).Where(l => l.Count >= 3).ToList();
        var (x0, z0) = (aussen.Min(p => p[0]), aussen.Min(p => p[1]));
        var (breite, tiefe) = (aussen.Max(p => p[0]) - x0, aussen.Max(p => p[1]) - z0);
        bool InFlaeche(double x, double z) => Geometrie.ImVieleck(x, z, aussen) && !loecher.Any(l => Geometrie.ImVieleck(x, z, l));
        var anzahl = (int)Math.Round(flaeche.Dichte * Inhalt(x0, z0, breite, tiefe, aussen, loecher) / 1000);

        var zufall = new Random(flaeche.Startwert);
        for (var n = 0; n < anzahl; n++)
        {
            // Streupunkt: gleich verteilt in der Fläche (Würfeln im umschließenden Rechteck, bis er drin liegt)
            var (px, pz) = (x0 + breite / 2, z0 + tiefe / 2);
            for (var versuch = 0; versuch < 200; versuch++)
            {
                (px, pz) = (x0 + zufall.NextDouble() * breite, z0 + zufall.NextDouble() * tiefe);
                if (InFlaeche(px, pz)) break;
            }
            var gruppe = NachGewicht(flaeche.Gruppen, g => g.Gewicht, zufall);
            var mitteFrei = frei.Frei(px, pz); // der Zufall läuft auch für verworfene Punkte gleich weiter
            foreach (var teil in gruppe.Teile)
            {
                var (von, bis) = teil.Anzahl is { Length: 2 } a ? (a[0], Math.Max(a[0], a[1])) : (1, 1);
                var kopien = von + zufall.Next(bis - von + 1);
                for (var i = 0; i < kopien; i++)
                {
                    var winkel = zufall.NextDouble() * Math.Tau;
                    var abstand = Math.Sqrt(zufall.NextDouble()) * gruppe.Radius;
                    var (x, z) = (px + Math.Cos(winkel) * abstand, pz + Math.Sin(winkel) * abstand);
                    var art = teil.Arten.Count == 0 ? null : katalog.Finde(NachGewicht(teil.Arten.ToList(), e => e.Value, zufall).Key);
                    var variante = zufall.Next(Math.Max(1, art?.Varianten ?? 1)) + 1;
                    var drehung = zufall.NextDouble() * 360;
                    var groesse = 0.85 + zufall.NextDouble() * 0.3;
                    if (!mitteFrei || art is not { Art: Bausteinart.Natur } || !InFlaeche(x, z) || !frei.Frei(x, z)) continue;
                    liste.Add(new Pflanzenlage(art, x, z, drehung, groesse, variante));
                }
            }
        }
        return liste;
    }

    /// <summary>Eine einzeln gesetzte Pflanze: Variante aus der Datei, bei 0 fest aus dem Ort gewürfelt. Null, wenn die Art keine Natur des Katalogs ist.</summary>
    public static Pflanzenlage? Einzeln(Pflanze p, Gebaeudekatalog katalog)
    {
        if (katalog.Finde(p.Katalog) is not { Art: Bausteinart.Natur } art || p.Punkt is not { Length: 2 }) return null;
        var varianten = Math.Max(1, art.Varianten);
        var variante = p.Variante > 0 ? Math.Min(p.Variante, varianten)
            : (int)(Streuung((int)Math.Round(p.Punkt[0] * 100), (long)Math.Round(p.Punkt[1] * 100)) % (uint)varianten) + 1;
        return new Pflanzenlage(art, p.Punkt[0], p.Punkt[1], p.Drehung, p.Massstab, variante);
    }

    /// <summary>
    /// Der Pinsel: Pflanzen locker entlang eines Strichs (Punktfolge; ein Punkt = ein Tupfer) in einem Band mit <paramref name="radius"/>.
    /// So viele, wie <paramref name="dichte"/> (je 1.000 m²) für die Fläche des Bandes sagt, mindestens eine; keine näher als
    /// <paramref name="mindestabstand"/> (Vorgabe aus der Dichte) an einer anderen oder an <paramref name="vorhanden"/>. Mit
    /// <paramref name="frei"/> bleiben Wege, Gebäude, Bauzonen und die Stadt frei. Arten nach Gewicht aus <paramref name="arten"/>
    /// (nur Natur des Katalogs). Gleiche Eingaben, gleiche Pflanzen.
    /// </summary>
    public static List<Pflanzenlage> Strich(IReadOnlyList<double[]> strich, double radius, double dichte, IReadOnlyDictionary<string, double> arten,
        int startwert, Gebaeudekatalog katalog, Naturfreiraum? frei = null, IEnumerable<double[]>? vorhanden = null, double? mindestabstand = null)
    {
        var liste = new List<Pflanzenlage>();
        var punkte = strich.Where(p => p is { Length: 2 }).ToList();
        var wahl = arten.Where(a => a.Value > 0 && katalog.Finde(a.Key) is { Art: Bausteinart.Natur }).ToList();
        if (punkte.Count == 0 || wahl.Count == 0 || dichte <= 0 || radius < 0) return liste;

        var laenge = Wegkurve.Laenge(punkte);
        var flaeche = 2 * radius * laenge + Math.PI * radius * radius;
        var anzahl = Math.Max(1, (int)Math.Round(dichte * flaeche / 1000));
        var abstand = mindestabstand ?? Math.Clamp(0.6 * Math.Sqrt(1000 / dichte), 0.5, 8);
        var belegt = (vorhanden ?? Enumerable.Empty<double[]>()).Where(p => p is { Length: 2 }).Select(p => (X: p[0], Z: p[1])).ToList();
        var zufall = new Random(startwert);

        for (var n = 0; n < anzahl; n++)
            for (var versuch = 0; versuch < 30; versuch++)
            {
                var (mx, mz) = AufDerLinie(punkte, zufall.NextDouble() * laenge);
                var winkel = zufall.NextDouble() * Math.Tau;
                var weite = Math.Sqrt(zufall.NextDouble()) * radius;
                var (x, z) = (mx + Math.Cos(winkel) * weite, mz + Math.Sin(winkel) * weite);
                if (belegt.Any(b => (b.X - x) * (b.X - x) + (b.Z - z) * (b.Z - z) < abstand * abstand)) continue;
                if (frei is not null && !frei.Frei(x, z)) continue;
                var art = katalog.Finde(NachGewicht(wahl, e => e.Value, zufall).Key)!;
                var variante = zufall.Next(Math.Max(1, art.Varianten)) + 1;
                var drehung = zufall.NextDouble() * 360;
                var groesse = 0.85 + zufall.NextDouble() * 0.3;
                liste.Add(new Pflanzenlage(art, x, z, drehung, groesse, variante));
                belegt.Add((x, z));
                break;
            }
        return liste;
    }

    /// <summary>Der Punkt nach <paramref name="strecke"/> Metern auf einer Linie (ein einzelner Punkt: er selbst).</summary>
    private static (double X, double Z) AufDerLinie(IReadOnlyList<double[]> linie, double strecke)
    {
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var (a, b) = (linie[i], linie[i + 1]);
            var l = Math.Sqrt((b[0] - a[0]) * (b[0] - a[0]) + (b[1] - a[1]) * (b[1] - a[1]));
            if (strecke <= l && l > 0) return (a[0] + (b[0] - a[0]) * strecke / l, a[1] + (b[1] - a[1]) * strecke / l);
            strecke -= l;
        }
        return (linie[^1][0], linie[^1][1]);
    }

    /// <summary>
    /// Wie groß der Teil des Rechtecks ist, der in der Fläche und in keiner Aussparung liegt (m²) – gezählt auf einem Raster von etwa 20.000
    /// Punkten. So zählen Aussparungen, die sich überdecken oder über den Rand der Fläche hinausragen, richtig. Gezählt wird Zeile für Zeile:
    /// Je Vieleck die Stellen, an denen seine Kanten die Zeile kreuzen (dieselbe Rechnung wie das Strahlverfahren, nur einmal je Zeile) –
    /// ein Punkt liegt drin, wenn rechts von ihm ungerade viele davon liegen.
    /// </summary>
    private static double Inhalt(double x0, double z0, double breite, double tiefe, List<double[]> aussen, List<List<double[]>> loecher)
    {
        var schritt = Math.Max(0.5, Math.Sqrt(breite * tiefe / 20000));
        var vielecke = loecher.Prepend(aussen).ToList();
        var kreuzungen = vielecke.Select(_ => new List<double>()).ToList();
        var zeiger = new int[vielecke.Count];
        var zaehler = 0;
        for (var z = z0 + schritt / 2; z < z0 + tiefe; z += schritt)
        {
            for (var v = 0; v < vielecke.Count; v++)
            {
                Kreuzungen(vielecke[v], z, kreuzungen[v]);
                zeiger[v] = 0;
            }
            for (var x = x0 + schritt / 2; x < x0 + breite; x += schritt)
            {
                // je Vieleck: wie viele Kreuzungen liegen links von x oder auf x (x wächst, der Zeiger läuft nur vorwärts)
                var drin = true;
                for (var v = 0; v < vielecke.Count && drin; v++)
                {
                    var k = kreuzungen[v];
                    while (zeiger[v] < k.Count && !(x < k[zeiger[v]])) zeiger[v]++;
                    var rechts = k.Count - zeiger[v];
                    var imVieleck = rechts % 2 == 1;
                    drin = v == 0 ? imVieleck : !imVieleck;
                }
                if (drin) zaehler++;
            }
        }
        return zaehler * schritt * schritt;
    }

    /// <summary>Die x-Stellen, an denen die Kanten des Vielecks die Zeile z kreuzen – gerechnet wie in <see cref="Geometrie.ImVieleck"/>, sortiert.</summary>
    private static void Kreuzungen(List<double[]> poly, double z, List<double> aus)
    {
        aus.Clear();
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var (x1, z1, x2, z2) = (poly[i][0], poly[i][1], poly[j][0], poly[j][1]);
            if ((z1 > z) != (z2 > z)) aus.Add((x2 - x1) * (z - z1) / (z2 - z1) + x1);
        }
        aus.Sort();
    }

    /// <summary>Wählt einen Eintrag nach seinem Gewicht (Gewichte ≤ 0 zählen nicht; sind alle 0, der erste).</summary>
    private static T NachGewicht<T>(IReadOnlyList<T> liste, Func<T, double> gewicht, Random zufall)
    {
        var summe = liste.Sum(x => Math.Max(0, gewicht(x)));
        var wurf = zufall.NextDouble() * summe;
        foreach (var x in liste)
        {
            wurf -= Math.Max(0, gewicht(x));
            if (wurf < 0) return x;
        }
        return liste[0];
    }

    /// <summary>Feste „Zufallszahl“ aus zwei Zahlen (Hash-Mischung, bei jedem Start gleich) – wie die Stadtansicht für Passanten.</summary>
    internal static uint Streuung(int nummer, long zeitpunkt)
    {
        var x = (uint)nummer * 2654435761u ^ (uint)zeitpunkt * 40503u;
        x ^= x >> 15;
        x *= 2246822519u;
        x ^= x >> 13;
        return x;
    }
}
