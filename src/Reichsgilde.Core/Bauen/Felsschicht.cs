using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Der Hang als eigene Schicht (Felspinsel P4a, 10.11.2026; wie „Edit Layers“ in Unreal)
//
//  „Hang mitformen“ (Felshang.cs, R5) schreibt den Hang hinter einer Wand fest ins Höhenraster: Wer die Wand danach verschiebt, dreht oder
//  löscht, behält einen Wall. Darum gibt es seit P4a das Feld „hang“ am Stück (fehlt bei false):
//    • Die Datei speichert nur die Grundhöhe (Feld „hoehen“, wie bisher). Der Hang aller Stücke mit „hang“ wird beim Laden und nach jeder
//      Änderung aus den Stücken neu gerechnet und nie gespeichert.
//    • Wirksame Höhe = Grundhöhe, gehoben durch den größten Hang aller Stücke mit „hang“ (dieselbe Rechnung wie Felshang.Formen, aus der
//      Grundhöhe; mehrere Stücke als Höchstwert, die Reihenfolge der Stücke ist gleichgültig). Löschen oder Abschalten stellt die Grundhöhe
//      exakt wieder her.
//    • Ein Stück steht auf der wirksamen Höhe (Boden unter der Mitte samt Schicht + Versatz) – von dort rechnet auch sein Hang: Hebt der Hang
//      einer anderen Wand die Mitte, steigt der eigene Hang mit. Die Zelle um die Mitte heben nur Stücke, die in der Datei davor stehen (Sperre,
//      wie die Reihenfolge des Stempelns beim festen Hang): kein Stück hebt sich selbst, keine zwei einander – ein Durchgang (Urspruenge). Welche
//      Stücke vorgelagert sind, entscheidet die Höhe über der Grundhöhe (sonst nähme eine Wand auf dem Hang einer anderen dieser den Hang).
//    • Die Geländewerkzeuge der Werkstatt (Pinsel, Rampe, Höhenkarte) formen die Grundhöhe; die Schicht liegt darüber.
//    • Ohne Stück mit „hang“ ist die wirksame Höhe die Grundhöhe selbst (dasselbe Raster): alte Karten und Spielstände bleiben Byte für Byte gleich.
//  „Hebung ablösen“ (Abloesen): Ein altes Stück, dessen Hang schon fest im Raster steht, bekäme mit „hang“ einen zweiten Hang darüber (an den
//  weichen Rändern ein Buckel). Ablösen nimmt die alte Hebung aus der Grundhöhe: Im Gebiet, auf das der Hang der Stücke wirken kann (einen
//  Rasterpunkt breiter), wird die Grundhöhe aus dem Rand des Gebiets geschätzt – je Zeile und je Spalte linear zwischen den nächsten Punkten
//  außerhalb, beide gewichtet nach der Länge (die kürzere zählt mehr). Ein ebener oder gleichmäßig geneigter Hang kommt so genau zurück. Gesenkt
//  wird nur, nie gehoben.
//  Alles aus ganzen Rasterpunkten mit +, −, ·, / (die Winkel der Stücke über DetMathe in Felshang) – gleich auf jedem Rechner.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Die Rechnungen der Hang-Schicht (siehe Kopf der Datei).</summary>
public static class Felsschicht
{
    /// <summary>
    /// Die wirksamen Höhen (cm) der Rasterpunkte, die der Hang der Stücke mit <see cref="Felslage.Hang"/> über die Grundhöhe hebt – nur Punkte, die
    /// höher kommen (gerundet wie ein Pinsel). Stücke ohne „hang“ zählen nicht.
    /// </summary>
    public static Dictionary<(int I, int J), short> Rechnen(Hoehenraster grund, Kartenrechteck karte, IEnumerable<Felslage> stuecke) =>
        Rechnen(grund, karte, stuecke.Where(l => l.Hang).Select(l => (l, (double?)null)).ToList());

    /// <summary>
    /// Wie <see cref="Rechnen(Hoehenraster, Kartenrechteck, IEnumerable{Felslage})"/> (nur Stücke mit „hang“ übergeben, in der Reihenfolge der Datei).
    /// Ein Stück steht auf der wirksamen Höhe (Boden unter der Mitte samt Schicht + Versatz), und von dort rechnet sein Hang (wie der feste Hang vom
    /// Gelände unter dem Stück). Die Zelle um seine Mitte heben nur Stücke, die vor ihm stehen (<see cref="Sperre"/>, wie die Reihenfolge des
    /// Stempelns beim festen Hang): So hängt kein Ursprung von sich selbst ab, und alle folgen in einem Durchgang (<see cref="Urspruenge"/>). Ein
    /// vorgegebener Ursprung (Meter) gilt statt dessen – für Befehle, die ein Stück dort lassen, wo es steht, und dann seinen Versatz ausgleichen.
    /// </summary>
    public static Dictionary<(int I, int J), short> Rechnen(Hoehenraster grund, Kartenrechteck karte, IReadOnlyList<(Felslage Lage, double? Ursprung)> stuecke) =>
        RechnenMitStand(grund, karte, stuecke).Schicht;

    /// <summary>
    /// Welche Stücke einen Hang formen (Form mit Hang, nicht vorgelagert): verglichen wird, wie hoch die Stücke über der Grundhöhe stünden (Ursprung
    /// auf der Grundhöhe), nicht auf der gehobenen – sonst machte eine Wand, die auf dem Hang einer anderen steht, diese vorgelagert und nähme ihr den
    /// Hang, auf dem sie steht. So hängt die Auswahl nicht von der Schicht ab.
    /// </summary>
    internal static bool[] Aktiv(Hoehenraster grund, IReadOnlyList<Felslage> lagen) => Felshang.Aktiv(lagen, Felshang.Urspruenge(grund, lagen));

    /// <summary>
    /// Je Rasterpunkt in der Zelle um die Mitte eines Stücks die kleinste Nummer (Stelle in der Liste) der Stücke, deren Mitte dort liegt: Dort heben
    /// nur Stücke mit kleinerer Nummer. Die Höhe, auf der ein Stück steht, hängt so nur von Stücken vor ihm ab – nie von seinem eigenen Hang und nie
    /// im Kreis (zwei Wände Rücken an Rücken höben sich sonst gegenseitig bis an die Grenze des Rasters).
    /// </summary>
    internal static Dictionary<(int I, int J), int> Sperre(IReadOnlyList<Felslage> lagen)
    {
        var aus = new Dictionary<(int I, int J), int>();
        for (var k = 0; k < lagen.Count; k++)
        {
            var (i, j) = Mittelpunkt(lagen[k]);
            foreach (var p in new[] { (i, j), (i + 1, j), (i, j + 1), (i + 1, j + 1) }) aus.TryAdd(p, k);
        }
        return aus;
    }

    /// <summary>Der Rasterpunkt links oben in der Zelle um die Mitte (die vier Punkte, aus denen die Höhe unter der Mitte kommt).</summary>
    private static (int I, int J) Mittelpunkt(Felslage l) => ((int)Math.Floor(l.X / Hoehenraster.Abstand), (int)Math.Floor(l.Z / Hoehenraster.Abstand));

    /// <summary>
    /// Wie <see cref="Rechnen(Hoehenraster, Kartenrechteck, IReadOnlyList{ValueTuple{Felslage, double?}})"/>, dazu Ursprung und Auswahl je Stück –
    /// der Stand, von dem aus der Entwurf später nur betroffene Stücke neu rechnet (<see cref="Teilweise"/>).
    /// </summary>
    internal static (Dictionary<(int I, int J), short> Schicht, double[] Ursprung, double[] Basis, bool[] Aktiv) RechnenMitStand(Hoehenraster grund, Kartenrechteck karte,
        IReadOnlyList<(Felslage Lage, double? Ursprung)> stuecke)
    {
        var lagen = stuecke.Select(s => s.Lage).ToList();
        var aktiv = Aktiv(grund, lagen);
        var sperre = Sperre(lagen);
        var (ursprung, basis) = Urspruenge(grund, karte, stuecke.Select(s => (s.Lage, s.Ursprung, (double?)null)).ToList(), aktiv, null, sperre);
        return (Runden(grund, Felshang.Hebung(grund, karte, lagen, ursprung, aktiv: aktiv, sperre: sperre, basis: basis)), ursprung, basis, aktiv);
    }

    /// <summary>
    /// Die Punkte der Schicht nur in diesen Rechtecken (Meter), aus den Stücken mit Ursprung und Auswahl (alle Stücke, deren Reichweite eins der
    /// Rechtecke trifft, müssen dabei sein; <paramref name="rang"/>: ihre Nummern in der ganzen Liste, aufsteigend; <paramref name="sperre"/> aus
    /// der ganzen Liste) – Zahl für Zahl dieselben wie in der ganzen Rechnung.
    /// </summary>
    internal static Dictionary<(int I, int J), short> Teilweise(Hoehenraster grund, Kartenrechteck karte, IReadOnlyList<Felslage> lagen,
        IReadOnlyList<double> ursprung, IReadOnlyList<double> basis, bool[] aktiv, IReadOnlyList<Kartenrechteck> bereiche, IReadOnlyList<int> rang,
        IReadOnlyDictionary<(int I, int J), int> sperre)
    {
        var aus = new Dictionary<(int I, int J), short>();
        foreach (var b in bereiche)
            foreach (var (p, w) in Runden(grund, Felshang.Hebung(grund, karte, lagen, ursprung, Hoehenraster.Punkte(b), aktiv, rang, sperre, basis)))
                aus[p] = w;
        return aus;
    }

    /// <summary>Die Hebung (cm, ungerundet) als Punkte der Schicht: gerundet wie ein Pinsel, nur Punkte, die höher kommen.</summary>
    private static Dictionary<(int I, int J), short> Runden(Hoehenraster grund, Dictionary<(int I, int J), double> hebung)
    {
        var aus = new Dictionary<(int I, int J), short>();
        foreach (var ((i, j), cm) in hebung)
        {
            var w = Gerundet(cm);
            if (w > grund.Wert(i, j)) aus[(i, j)] = w;
        }
        return aus;
    }

    private static short Gerundet(double cm) => (short)Math.Clamp((int)Math.Round(cm, MidpointRounding.AwayFromZero), Hoehenraster.TiefsteCm, Hoehenraster.HoechsteCm);

    /// <summary>
    /// Der Ursprung jedes Stücks (Meter, wie <see cref="Felsraum.UrsprungY"/>) auf der wirksamen Höhe – in einem Durchgang in der Reihenfolge der
    /// Liste: Die Zelle um die Mitte eines Stücks heben nur Stücke davor (<see cref="Sperre"/>), deren Ursprung schon feststeht. Gerechnet werden nur
    /// die vier Rasterpunkte um jede Mitte, aus den Stücken, deren Reichweite sie trifft – gerundet wie die Schicht, also Zahl für Zahl die Höhe, auf
    /// der das Stück dargestellt wird. <paramref name="rang"/>: die Nummern der Stücke in der ganzen Liste (aufsteigend; null: ihre Stelle hier).
    /// </summary>
    internal static (double[] Ursprung, double[] Basis) Urspruenge(Hoehenraster grund, Kartenrechteck karte,
        IReadOnlyList<(Felslage Lage, double? Ursprung, double? Basis)> stuecke, bool[] aktiv, IReadOnlyList<int>? rang, IReadOnlyDictionary<(int I, int J), int> sperre)
    {
        var n = stuecke.Count;
        var lagen = stuecke.Select(s => s.Lage).ToList();
        var (u, basis) = (new double[n], new double[n]);
        var reich = lagen.Select(Reichweite).ToArray();
        for (var k = 0; k < n; k++)
        {
            // der Boden unter der Mitte: Grundhöhe, gehoben nur von Stücken davor (die stehen schon fest)
            var boden = stuecke[k].Basis;
            if (boden is null)
            {
                var (i, j) = Mittelpunkt(lagen[k]);
                var zelle = new Kartenrechteck(i * Hoehenraster.Abstand, j * Hoehenraster.Abstand, (i + 1) * Hoehenraster.Abstand, (j + 1) * Hoehenraster.Abstand);
                var maske = new bool[n];
                var quellen = false;
                for (var m = 0; m < k; m++)
                    if (aktiv[m] && reich[m] is { } r && r.X0 <= zelle.X1 && zelle.X0 <= r.X1 && r.Z0 <= zelle.Z1 && zelle.Z0 <= r.Z1) maske[m] = quellen = true;
                var schicht = quellen ? Runden(grund, Felshang.Hebung(grund, karte, lagen, u, (i, j, i + 1, j + 1), maske, rang, sperre, basis)) : new();
                boden = Hoehe(grund, schicht, lagen[k].X, lagen[k].Z);
            }
            basis[k] = boden.Value;
            u[k] = stuecke[k].Ursprung ?? boden.Value - lagen[k].Einsinken * Felsraum.Groesse(lagen[k]).Y + lagen[k].Versatz;
        }
        return (u, basis);
    }
    /// <summary>
    /// Nur für die Gegenprobe der Tests: die Schicht ohne die Reihenfolge (jedes Stück hebt jede Zelle, auch die eigene), Ursprünge nachgerechnet,
    /// bis sich keiner mehr ändert (höchstens <paramref name="runden"/> Runden) – so rechnete P4a vor der Nachprüfung.
    /// </summary>
    internal static (Dictionary<(int I, int J), short> Schicht, double[] Ursprung) RechnenOhneReihenfolge(Hoehenraster grund, Kartenrechteck karte,
        IReadOnlyList<Felslage> lagen, int runden = 32)
    {
        var aktiv = Aktiv(grund, lagen);
        var u = lagen.Select(l => Felsraum.UrsprungY(l, grund)).ToArray();
        for (var runde = 0; runde < runden; runde++)
        {
            var schicht = Runden(grund, Felshang.Hebung(grund, karte, lagen, u, aktiv: aktiv));
            var neu = lagen.Select(l => Hoehe(grund, schicht, l.X, l.Z) - l.Einsinken * Felsraum.Groesse(l).Y + l.Versatz).ToArray();
            if (neu.AsSpan().SequenceEqual(u)) break;
            u = neu;
        }
        return (Runden(grund, Felshang.Hebung(grund, karte, lagen, u, aktiv: aktiv)), u);
    }

    /// <summary>
    /// Das wirksame Raster einer Karte (Stadtkarte): die Grundhöhe mit der Schicht der Stücke mit „hang“ – ohne ein solches Stück (oder wenn es
    /// nichts hebt) das Grundraster selbst.
    /// </summary>
    public static Hoehenraster? Wirksam(Hoehenraster? grund, Kartenfelsen? felsen, Kartenrechteck karte)
    {
        if (felsen?.Stuecke is not { } stuecke || !stuecke.Any(s => s is { Hang: true })) return grund;
        var lagen = stuecke.Where(s => s is { Hang: true }).Select(LageVon).ToList();
        var basis = grund ?? new Hoehenraster();
        var schicht = Rechnen(basis, karte, lagen);
        if (schicht.Count == 0) return grund;
        var aus = basis.Kopie();
        foreach (var ((i, j), cm) in schicht.OrderBy(p => p.Key.J).ThenBy(p => p.Key.I)) aus.WertSetzen(i, j, cm);
        return aus;
    }

    /// <summary>Ein Felsstück der Karte als Lage – genau so gelesen wie in der Kartenwerkstatt (<see cref="EntwurfFels"/>).</summary>
    public static Felslage LageVon(Felsstueck s) => EntwurfFels.Aus(s, null).AlsLage();

    /// <summary>
    /// Das Rechteck (Meter), in dem der Hang eines Stücks etwas ändern kann (unabhängig vom Gelände, mit einem Rasterabstand Rand) – null, wenn seine
    /// Form keinen Hang hat. Für die Darstellung (nur diese Kacheln neu bauen) und für „nur betroffene Zellen neu rechnen“.
    /// </summary>
    public static Kartenrechteck? Reichweite(Felslage l) => Felshang.Reichweite(l);

    /// <summary>Hat diese Form einen Hang (nur dann setzt „hang“ etwas)?</summary>
    public static bool HatHang(string form) => Felsformen.Finde(form) is { HangFormt: true };

    /// <summary>
    /// Die Höhe in Metern an einem Ort (bilinear wie <see cref="Hoehenraster.Hoehe"/>), wenn die Punkte der Schicht die Grundhöhe ersetzten – Zahl
    /// für Zahl dieselbe wie im wirksamen Raster.
    /// </summary>
    internal static double Hoehe(Hoehenraster grund, IReadOnlyDictionary<(int I, int J), short> schicht, double x, double z)
    {
        if (grund.IstLeer && schicht.Count == 0) return 0;
        var (fx, fz) = (x / Hoehenraster.Abstand, z / Hoehenraster.Abstand);
        var (i, j) = ((int)Math.Floor(fx), (int)Math.Floor(fz));
        var (tx, tz) = (fx - i, fz - j);
        double W(int a, int b) => schicht.TryGetValue((a, b), out var v) ? v : grund.Wert(a, b);
        double a = W(i, j), b = W(i + 1, j), c = W(i, j + 1), d = W(i + 1, j + 1);
        return ((a + (b - a) * tx) * (1 - tz) + (c + (d - c) * tx) * tz) / 100;
    }

    /// <summary>So weit (Rasterpunkte) sucht „Hebung ablösen“ je Richtung nach dem Rand des Gebiets.</summary>
    public const int AbloesenWeite = 1024;

    /// <summary>
    /// „Hebung ablösen“ (siehe Kopf der Datei): senkt die Grundhöhe im Gebiet des Hangs der Stücke (nur Formen mit Hang; vorgelagerte zählen nicht)
    /// auf die aus dem Rand geschätzte Höhe. Das Raster ändert sich sofort (wie <see cref="Felshang.Formen"/>); null, wenn sich nichts ändert.
    /// </summary>
    public static Gelaendeaenderung? Abloesen(Hoehenraster grund, Kartenrechteck karte, IEnumerable<Felslage> stuecke)
    {
        var erlaubt = Hoehenraster.Punkte(karte);
        bool Erlaubt(int i, int j) => i >= erlaubt.I0 && i <= erlaubt.I1 && j >= erlaubt.J0 && j <= erlaubt.J1;
        var kern = new HashSet<(int I, int J)>();
        Felshang.Flaeche(grund, karte, stuecke, (i, j) => kern.Add((i, j)));
        if (kern.Count == 0) return null;
        // einen Rasterpunkt breiter (auch schräg): ein Rand, der nur wenig gehoben war, gehört dazu
        var gebiet = new HashSet<(int I, int J)>(kern);
        foreach (var (i, j) in kern)
            for (var dj = -1; dj <= 1; dj++)
                for (var di = -1; di <= 1; di++)
                    if (Erlaubt(i + di, j + dj)) gebiet.Add((i + di, j + dj));
        // erst alle Schätzungen aus dem Stand vorher, dann schreiben: die Reihenfolge ist gleichgültig
        var neu = new List<(int I, int J, double Cm)>();
        foreach (var (i, j) in gebiet.OrderBy(p => p.J).ThenBy(p => p.I))
        {
            if (Schaetzen(grund, gebiet, Erlaubt, i, j) is not { } cm) continue;
            if (Math.Round(cm, MidpointRounding.AwayFromZero) < grund.Wert(i, j)) neu.Add((i, j, cm));
        }
        if (neu.Count == 0) return null;
        var aenderung = new Gelaendeaenderung(grund, karte);
        foreach (var (i, j, cm) in neu) aenderung.Setzen(i, j, cm);
        return aenderung.Bereich is null ? null : aenderung;
    }

    /// <summary>
    /// Die Höhe (cm) eines Punkts im Gebiet aus dem Rand: je Zeile und Spalte linear zwischen den nächsten Punkten außerhalb des Gebiets, beide
    /// Richtungen gewichtet mit 1 / Länge². Null, wenn keine Richtung beide Seiten findet.
    /// </summary>
    private static double? Schaetzen(Hoehenraster grund, HashSet<(int I, int J)> gebiet, Func<int, int, bool> erlaubt, int i, int j)
    {
        double summe = 0, gewichte = 0;
        foreach (var (di, dj) in new[] { (1, 0), (0, 1) })
        {
            if (Rand(i, j, -di, -dj) is not { } a || Rand(i, j, di, dj) is not { } b) continue;
            var laenge = a.Weg + b.Weg;
            var w = 1.0 / (laenge * (double)laenge);
            summe += w * (a.Cm * (double)b.Weg + b.Cm * (double)a.Weg) / laenge;
            gewichte += w;
        }
        return gewichte > 0 ? summe / gewichte : null;

        (int Weg, short Cm)? Rand(int i0, int j0, int di, int dj)
        {
            for (var n = 1; n <= AbloesenWeite; n++)
            {
                var (x, y) = (i0 + n * di, j0 + n * dj);
                if (!erlaubt(x, y)) return null;
                if (!gebiet.Contains((x, y))) return (n, grund.Wert(x, y));
            }
            return null;
        }
    }
}
