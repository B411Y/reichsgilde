using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Der Fußabdruck der Umlandmodelle für das Gras (Teil der <c>partial class Stadtansicht</c>): Bis hierher sperrte die Gras-Aussparung das ganze Rechteck des
/// Modells (<see cref="Modellmass"/>), weil darin eine Grasplatte lag. Die Platten sind aus den Modellen weg; der Teppich soll jetzt dort wachsen, wo das
/// Modell nichts hat (der Rest des Platzes, die Weide hinter dem Zaun). Darum wird aus den Dreiecken des Modells (von oben gesehen) ein Raster von
/// <see cref="FussZelle"/> m gefüllt – Wände, Dächer, Beete, Höfe, Zäune und Mauern, nicht die Gras- und Moosbüschel – und zu wenigen Rechtecken
/// zusammengefasst. Die Rechtecke gehen als <see cref="Naturhindernis"/> in den Freiraum des Grasteppichs, dessen Rand weich ausläuft (<c>GrasRandFaktor</c>).
/// </summary>
public sealed partial class Stadtansicht
{
    /// <summary>Kantenlänge einer Rasterzelle des Fußabdrucks (Meter, im Modell).</summary>
    private const float FussZelle = 0.5f;

    private readonly Dictionary<string, (float X, float Z, float HalbX, float HalbZ)[]?> _grasFuss = new();

    /// <summary>
    /// Die Rechtecke (Mitte und halbe Maße im Modell, ohne Maßstab) des Fußabdrucks von <paramref name="b"/>; null, wenn das Modell nicht zu lesen ist
    /// (dann gilt das ganze Rechteck von <see cref="Modellmass"/>). Je Modell und Knoten einmal gerechnet.
    /// </summary>
    private (float X, float Z, float HalbX, float HalbZ)[]? GrasFussabdruck(Baustein b)
    {
        if (b.Modell is not { } datei) return null;
        var schluessel = datei + "#" + b.Knoten;
        if (_grasFuss.TryGetValue(schluessel, out var bekannt)) return bekannt;
        (float, float, float, float)[]? ergebnis = null;
        if (Modell(datei)?.Instantiate<Node3D>() is { } wurzel)
        {
            try
            {
                var start = b.Knoten is { } k && wurzel.FindChild(k, true, false) is Node3D teilWurzel ? teilWurzel : wurzel;
                ergebnis = FussRechtecke(wurzel, start);
            }
            finally { wurzel.Free(); }
        }
        return _grasFuss[schluessel] = ergebnis;
    }

    private static (float X, float Z, float HalbX, float HalbZ)[]? FussRechtecke(Node3D wurzel, Node3D start)
    {
        var dreiecke = new List<(Vector2 A, Vector2 B, Vector2 C)>();
        var (min, max) = (new Vector2(float.MaxValue, float.MaxValue), new Vector2(float.MinValue, float.MinValue));
        var teile = start.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToList();
        if (start is MeshInstance3D selbst) teile.Add(selbst);
        foreach (var teil in teile)
        {
            if (teil.Mesh is not { } netz) continue;
            var lage = LageUnter(wurzel, teil);
            for (var s = 0; s < netz.GetSurfaceCount(); s++)
            {
                var name = netz.SurfaceGetMaterial(s)?.ResourceName ?? "";
                if (name.StartsWith("Gras", StringComparison.Ordinal) || name.StartsWith("Moos", StringComparison.Ordinal)) continue; // Büschel sind selbst Gras
                var arrays = netz.SurfaceGetArrays(s);
                var ecken = (Vector3[])arrays[(int)Mesh.ArrayType.Vertex];
                var indizes = (int[])arrays[(int)Mesh.ArrayType.Index];
                if (indizes.Length == 0) indizes = Enumerable.Range(0, ecken.Length).ToArray();
                for (var i = 0; i + 2 < indizes.Length; i += 3)
                {
                    var (p, q, r) = (lage * ecken[indizes[i]], lage * ecken[indizes[i + 1]], lage * ecken[indizes[i + 2]]);
                    var d = (new Vector2(p.X, p.Z), new Vector2(q.X, q.Z), new Vector2(r.X, r.Z));
                    dreiecke.Add(d);
                    foreach (var e in new[] { d.Item1, d.Item2, d.Item3 }) { min = min.Min(e); max = max.Max(e); }
                }
            }
        }
        if (dreiecke.Count == 0) return null;
        var (b, h) = (Mathf.CeilToInt((max.X - min.X) / FussZelle) + 1, Mathf.CeilToInt((max.Y - min.Y) / FussZelle) + 1);
        if (b > 600 || h > 600) return null;
        var belegt = new bool[b * h];
        void Setzen(Vector2 p)
        {
            var (i, j) = (Mathf.Clamp(Mathf.FloorToInt((p.X - min.X) / FussZelle), 0, b - 1), Mathf.Clamp(Mathf.FloorToInt((p.Y - min.Y) / FussZelle), 0, h - 1));
            belegt[j * b + i] = true;
        }
        foreach (var (p, q, r) in dreiecke)
        {
            Setzen(p); Setzen(q); Setzen(r); // dünne Teile (Pfosten) füllen wenigstens die Zellen ihrer Ecken
            var (lo, hi) = (p.Min(q).Min(r), p.Max(q).Max(r));
            var (i0, i1) = (Mathf.FloorToInt((lo.X - min.X) / FussZelle), Mathf.FloorToInt((hi.X - min.X) / FussZelle));
            var (j0, j1) = (Mathf.FloorToInt((lo.Y - min.Y) / FussZelle), Mathf.FloorToInt((hi.Y - min.Y) / FussZelle));
            if (i1 == i0 && j1 == j0) continue;
            for (var j = j0; j <= j1; j++)
                for (var i = i0; i <= i1; i++)
                    if (ImDreieck(new Vector2(min.X + (i + 0.5f) * FussZelle, min.Y + (j + 0.5f) * FussZelle), p, q, r)) belegt[Mathf.Clamp(j, 0, h - 1) * b + Mathf.Clamp(i, 0, b - 1)] = true;
        }
        // Zeilenstücke, dann gleiche Stücke in aufeinanderfolgenden Zeilen zu einem Rechteck
        var liste = new List<(float, float, float, float)>();
        var offen = new Dictionary<(int, int), int>(); // (von, bis) -> Zeile, in der das Rechteck beginnt
        for (var j = 0; j <= h; j++)
        {
            var jetzt = new HashSet<(int, int)>();
            for (var i = 0; j < h && i < b; i++)
            {
                if (!belegt[j * b + i]) continue;
                var von = i;
                while (i + 1 < b && belegt[j * b + i + 1]) i++;
                jetzt.Add((von, i + 1));
            }
            foreach (var (stueck, anfang) in offen.ToList())
            {
                if (jetzt.Contains(stueck)) continue;
                offen.Remove(stueck);
                liste.Add((min.X + stueck.Item1 * FussZelle, min.Y + anfang * FussZelle, min.X + stueck.Item2 * FussZelle, min.Y + j * FussZelle));
            }
            foreach (var stueck in jetzt) offen.TryAdd(stueck, j);
        }
        return liste.Select(r => ((r.Item1 + r.Item3) / 2, (r.Item2 + r.Item4) / 2, (r.Item3 - r.Item1) / 2, (r.Item4 - r.Item2) / 2)).ToArray();
    }

    private static bool ImDreieck(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        static float V(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        float d1 = V(p, a, b), d2 = V(p, b, c), d3 = V(p, c, a);
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
    }

    /// <summary>
    /// Die Gebäude des Grasteppichs als Hindernisse: Umlandbetriebe und -stellen mit den Rechtecken ihres Fußabdrucks (<see cref="GrasFussabdruck"/>), alle anderen
    /// mit ihrer Fläche aus dem Katalog (<see cref="Naturhindernis.Von"/>).
    /// </summary>
    private IEnumerable<Naturhindernis> GrasHindernisse(IEnumerable<Kartengebaeude> gebaeude)
    {
        foreach (var g in gebaeude)
        {
            var (x, z, w, m) = (g.Punkt[0], g.Punkt[1], g.Drehung, g.Massstab);
            if (g.Baustein is { Art: Bausteinart.Umlandbetrieb or Bausteinart.Umlandstelle } b && m > 0 && GrasFussabdruck(b) is { } rechtecke)
            {
                var (c, s) = (Math.Cos(w * Math.PI / 180), Math.Sin(w * Math.PI / 180));
                foreach (var (rx, rz, hx, hz) in rechtecke) // Modell (x, z) nach Welt wie Rotation.Y
                {
                    var (lx, lz) = (rx * m, rz * m);
                    yield return new Naturhindernis(x + lx * c + lz * s, z - lx * s + lz * c, w, hx * m, hz * m, false);
                }
            }
            else if (Naturhindernis.Von(g.Baustein, x, z, w, m, Modellmass) is { } h) yield return h;
        }
    }
}
