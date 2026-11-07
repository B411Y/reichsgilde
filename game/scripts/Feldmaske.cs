using System.Threading.Tasks;
using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Wo auf einer Karte wirklich Acker liegt und wie weit ein Punkt vom Rand davon entfernt ist (Meter, innen positiv, außen negativ, nur bis <see cref="Weit"/> genau).
/// Der Rand des Ackers ist nicht der Umriss der Feldfläche: Die Stücke des Kerns (<see cref="Feldparzelle"/>) füllen die Fläche nicht ganz, und ihre Ränder
/// sind es, die man sieht. Darum wird je Feldfläche ein Raster (<see cref="Zelle"/> m) angelegt, markiert, was in der Fläche und in einem Stück liegt, und der
/// Abstand mit einer Distanztransformation gerechnet. Nur Rechnung (keine Godot-Objekte), die Abfrage <see cref="Abstand"/> ist in allen Fäden erlaubt.
/// Für den Feldrain: Äcker und Pflanzen laufen darin aus (acker.gdshader, feldpflanzen.gdshader, <see cref="FeldPflanzenBauer.Feldrain"/>).
/// </summary>
public sealed class Feldmaske
{
    /// <summary>Kantenlänge einer Rasterzelle in Metern und Abstand, ab dem nichts mehr zählt (Meter).</summary>
    public const float Zelle = 0.5f, Weit = 4f;

    private sealed class Gitter
    {
        public Vector2 Ursprung;
        public int B, H;
        public float[] Wert = Array.Empty<float>();
    }

    private readonly Gitter?[] _gitter;

    public Feldmaske(IReadOnlyList<Feldparzelle> parzellen, IReadOnlyList<Vector2[]> polygone, ParallelOptions? po = null)
    {
        var stuecke = parzellen.Select(p =>
        {
            var basis = new Basis(Vector3.Up, Mathf.DegToRad((float)p.Drehung));
            var (b, l) = ((float)p.Breite / 2, (float)p.Laenge / 2);
            var mitte = new Vector2((float)p.X, (float)p.Z);
            var r = new Rect2(mitte, Vector2.Zero);
            foreach (var e in new[] { new Vector3(-b, 0, -l), new Vector3(b, 0, -l), new Vector3(b, 0, l), new Vector3(-b, 0, l) })
            {
                var w = basis * e;
                r = r.Expand(mitte + new Vector2(w.X, w.Z));
            }
            return (Mitte: mitte, Zurueck: basis.Inverse(), B: b, L: l, Rahmen: r);
        }).ToArray();
        _gitter = new Gitter?[polygone.Count];
        Parallel.For(0, polygone.Count, po ?? new ParallelOptions(), n => _gitter[n] = Bauen(polygone[n], stuecke));
    }

    private static Gitter? Bauen(Vector2[] pg, (Vector2 Mitte, Basis Zurueck, float B, float L, Rect2 Rahmen)[] stuecke)
    {
        if (pg.Length < 3) return null;
        var r = new Rect2(pg[0], Vector2.Zero);
        foreach (var q in pg) r = r.Expand(q);
        r = r.Grow(Weit + 1f);
        var g = new Gitter { Ursprung = r.Position, B = Mathf.CeilToInt(r.Size.X / Zelle), H = Mathf.CeilToInt(r.Size.Y / Zelle) };
        var bedeckt = new bool[g.B * g.H];
        foreach (var s in stuecke)
        {
            if (!s.Rahmen.Intersects(r)) continue;
            var (i0, i1) = (Mathf.Max(0, Mathf.FloorToInt((s.Rahmen.Position.X - g.Ursprung.X) / Zelle)), Mathf.Min(g.B - 1, Mathf.CeilToInt((s.Rahmen.End.X - g.Ursprung.X) / Zelle)));
            var (j0, j1) = (Mathf.Max(0, Mathf.FloorToInt((s.Rahmen.Position.Y - g.Ursprung.Y) / Zelle)), Mathf.Min(g.H - 1, Mathf.CeilToInt((s.Rahmen.End.Y - g.Ursprung.Y) / Zelle)));
            for (var j = j0; j <= j1; j++)
                for (var i = i0; i <= i1; i++)
                {
                    if (bedeckt[j * g.B + i]) continue;
                    var p = g.Ursprung + new Vector2((i + 0.5f) * Zelle, (j + 0.5f) * Zelle);
                    var lokal = s.Zurueck * new Vector3(p.X - s.Mitte.X, 0, p.Y - s.Mitte.Y);
                    if (Mathf.Abs(lokal.X) > s.B || Mathf.Abs(lokal.Z) > s.L) continue;
                    if (Innen(p, pg)) bedeckt[j * g.B + i] = true;
                }
        }
        var innen = Transformation(bedeckt, g.B, g.H, false);
        var aussen = Transformation(bedeckt, g.B, g.H, true);
        g.Wert = new float[bedeckt.Length];
        for (var k = 0; k < bedeckt.Length; k++)
            g.Wert[k] = bedeckt[k] ? Mathf.Min(Weit, (innen[k] / 3f - 0.5f) * Zelle) : -Mathf.Min(Weit, (aussen[k] / 3f - 0.5f) * Zelle);
        return g;
    }

    private static bool Innen(Vector2 q, Vector2[] pg)
    {
        var innen = false;
        for (int i = 0, j = pg.Length - 1; i < pg.Length; j = i++)
        {
            var (a, c) = (pg[j], pg[i]);
            if ((c.Y > q.Y) != (a.Y > q.Y) && q.X < (a.X - c.X) * (q.Y - c.Y) / (a.Y - c.Y) + c.X) innen = !innen;
        }
        return innen;
    }

    /// <summary>Chamfer-Abstand (3 je Schritt, 4 diagonal) jeder Zelle zur nächsten Zelle des anderen Zustands (<paramref name="vonAussen"/>: zur nächsten bedeckten).</summary>
    private static int[] Transformation(bool[] bedeckt, int b, int h, bool vonAussen)
    {
        const int Weg = 1_000_000;
        var d = new int[bedeckt.Length];
        for (var k = 0; k < d.Length; k++) d[k] = bedeckt[k] == vonAussen ? 0 : Weg;
        for (var j = 0; j < h; j++)
            for (var i = 0; i < b; i++)
            {
                var k = j * b + i;
                var v = d[k];
                if (i > 0) v = Math.Min(v, d[k - 1] + 3);
                if (j > 0)
                {
                    v = Math.Min(v, d[k - b] + 3);
                    if (i > 0) v = Math.Min(v, d[k - b - 1] + 4);
                    if (i < b - 1) v = Math.Min(v, d[k - b + 1] + 4);
                }
                d[k] = v;
            }
        for (var j = h - 1; j >= 0; j--)
            for (var i = b - 1; i >= 0; i--)
            {
                var k = j * b + i;
                var v = d[k];
                if (i < b - 1) v = Math.Min(v, d[k + 1] + 3);
                if (j < h - 1)
                {
                    v = Math.Min(v, d[k + b] + 3);
                    if (i < b - 1) v = Math.Min(v, d[k + b + 1] + 4);
                    if (i > 0) v = Math.Min(v, d[k + b - 1] + 4);
                }
                d[k] = v;
            }
        return d;
    }

    /// <summary>
    /// Der Abstand zum Rand des Ackers in Metern: innen positiv (höchstens <see cref="Weit"/>), außen negativ; <see cref="float.NegativeInfinity"/>, wo kein
    /// Raster liegt (weit von jeder Feldfläche).
    /// </summary>
    public float Abstand(float x, float z)
    {
        var beste = float.NegativeInfinity;
        foreach (var g in _gitter)
        {
            if (g is null) continue;
            var (fx, fz) = ((x - g.Ursprung.X) / Zelle - 0.5f, (z - g.Ursprung.Y) / Zelle - 0.5f);
            if (fx < 0 || fz < 0 || fx >= g.B - 1 || fz >= g.H - 1) continue;
            var (i, j) = ((int)fx, (int)fz);
            var (tx, tz) = (fx - i, fz - j);
            var k = j * g.B + i;
            var w = Mathf.Lerp(Mathf.Lerp(g.Wert[k], g.Wert[k + 1], tx), Mathf.Lerp(g.Wert[k + g.B], g.Wert[k + g.B + 1], tx), tz);
            if (w > beste) beste = w;
        }
        return beste;
    }
}
