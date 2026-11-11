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
//      einer anderen Wand (oder der eigene) die Mitte, steigt der eigene Hang mit (Urspruenge, nachgerechnet bis nichts mehr steigt). Welche
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
    /// Wie <see cref="Rechnen(Hoehenraster, Kartenrechteck, IEnumerable{Felslage})"/> (nur Stücke mit „hang“ übergeben). Ein Stück steht auf der
    /// wirksamen Höhe (Boden unter der Mitte samt Schicht + Versatz), und von dort rechnet sein Hang (wie der feste Hang vom Gelände unter dem Stück):
    /// Hebt die Schicht die Mitte eines Stücks, steigt auch sein Hang (<see cref="Urspruenge"/>). Ein vorgegebener Ursprung (Meter) gilt statt dessen –
    /// für Befehle, die ein Stück dort lassen, wo es steht, und dann seinen Versatz ausgleichen.
    /// </summary>
    public static Dictionary<(int I, int J), short> Rechnen(Hoehenraster grund, Kartenrechteck karte, IReadOnlyList<(Felslage Lage, double? Ursprung)> stuecke)
    {
        var lagen = stuecke.Select(s => s.Lage).ToList();
        var aktiv = Aktiv(grund, lagen);
        var ursprung = Urspruenge(grund, karte, stuecke, aktiv);
        return Runden(grund, Felshang.Hebung(grund, karte, lagen, ursprung, aktiv: aktiv));
    }

    /// <summary>
    /// Welche Stücke einen Hang formen (Form mit Hang, nicht vorgelagert): verglichen wird, wie hoch die Stücke über der Grundhöhe stünden (Ursprung
    /// auf der Grundhöhe), nicht auf der gehobenen – sonst machte eine Wand, die auf dem Hang einer anderen steht, diese vorgelagert und nähme ihr den
    /// Hang, auf dem sie steht. So hängt die Auswahl nicht von der Schicht ab.
    /// </summary>
    private static bool[] Aktiv(Hoehenraster grund, IReadOnlyList<Felslage> lagen) => Felshang.Aktiv(lagen, Felshang.Urspruenge(grund, lagen));

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

    /// <summary>So oft rechnet <see cref="Urspruenge"/> höchstens nach (danach gilt der letzte Stand).</summary>
    public const int UrsprungRunden = 32;

    /// <summary>
    /// Der Ursprung jedes Stücks (Meter, wie <see cref="Felsraum.UrsprungY"/>) auf der wirksamen Höhe, die die Schicht selbst ergibt: Die Schicht
    /// hängt von den Ursprüngen ab und hebt den Boden unter den Mitten – darum von der Grundhöhe aus so lange nachgerechnet, bis sich kein Ursprung
    /// mehr ändert (die Auswahl <paramref name="aktiv"/> steht vorher fest, gehoben wird nur, also steigt jeder Ursprung nur; höchstens
    /// <see cref="UrsprungRunden"/> Runden). Gerechnet werden dabei nur die
    /// vier Rasterpunkte um jede Mitte, aus den Stücken, deren Reichweite sie trifft – gerundet wie die Schicht, also Zahl für Zahl die Höhe,
    /// auf der das Stück dargestellt wird.
    /// </summary>
    internal static double[] Urspruenge(Hoehenraster grund, Kartenrechteck karte, IReadOnlyList<(Felslage Lage, double? Ursprung)> stuecke, bool[] aktiv)
    {
        var n = stuecke.Count;
        var lagen = stuecke.Select(s => s.Lage).ToList();
        var u = new double[n];
        for (var k = 0; k < n; k++) u[k] = stuecke[k].Ursprung ?? Felsraum.UrsprungY(lagen[k], grund);
        // je Stück ohne festen Ursprung: die vier Punkte um die Mitte und die Stücke, deren Hang sie erreichen kann
        var reich = lagen.Select(Reichweite).ToArray();
        var offen = new List<(int K, (int I0, int J0, int I1, int J1) Punkte, int[] Quellen)>();
        for (var k = 0; k < n; k++)
        {
            if (stuecke[k].Ursprung is not null) continue;
            var (i, j) = ((int)Math.Floor(lagen[k].X / Hoehenraster.Abstand), (int)Math.Floor(lagen[k].Z / Hoehenraster.Abstand));
            var (x0, z0, x1, z1) = (i * Hoehenraster.Abstand, j * Hoehenraster.Abstand, (i + 1) * Hoehenraster.Abstand, (j + 1) * Hoehenraster.Abstand);
            var quellen = Enumerable.Range(0, n).Where(m => aktiv[m] && reich[m] is { } r && r.X0 <= x1 && x0 <= r.X1 && r.Z0 <= z1 && z0 <= r.Z1).ToArray();
            if (quellen.Length > 0) offen.Add((k, (i, j, i + 1, j + 1), quellen));
        }
        for (var runde = 0; runde < UrsprungRunden && offen.Count > 0; runde++)
        {
            var neu = (double[])u.Clone();
            foreach (var (k, punkte, quellen) in offen)
            {
                var maske = new bool[n];
                foreach (var m in quellen) maske[m] = true;
                var hebung = Felshang.Hebung(grund, karte, lagen, u, punkte, maske);
                var schicht = Runden(grund, hebung);
                neu[k] = Hoehe(grund, schicht, lagen[k].X, lagen[k].Z) - lagen[k].Einsinken * Felsraum.Groesse(lagen[k]).Y + lagen[k].Versatz;
            }
            if (neu.AsSpan().SequenceEqual(u)) break;
            u = neu;
        }
        return u;
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
