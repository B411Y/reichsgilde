namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Bodenstempel (Darstellung, Boden Stufe 2)
//
//  Ein Stempel drückt eine Bodentextur (Erde, Kies, Pflaster …) in das Bodenraster, und zwar nur dort, wo noch Wiese ist: Was das Leveldesign
//  gemalt hat, bleibt. Der Rand ist weich (Anteil fällt über „Rand“ Meter ab) und mit einem Rauschen ausgefranst; „Fleck“ lässt auch das
//  Innere stellenweise Wiese durchblicken (ein Garten mit etwas Gras). Das Ergebnis hängt nur von den Angaben ab (feste Hash-Zahlen, keine
//  Uhr, kein Zufall): auf jedem Rechner dasselbe Bild.
//
//  Gedacht für eine Kopie des Rasters (<see cref="Bodenraster.Kopie"/>), die nur die Darstellung zeichnet – die Karte und der Spielstand bleiben
//  unberührt, es gehört nicht zum Zustand der Simulation.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Was ein Stempel ins Bodenraster schreibt (siehe Kopf der Datei). Die Fläche besteht aus beliebig vielen Vielecken (ihre Vereinigung).</summary>
/// <param name="Textur">Die Bodentextur.</param>
/// <param name="Staerke">0 … 1: wie viel der Wiese die Textur im Innern höchstens ersetzt.</param>
/// <param name="Rand">Breite (Meter) des weichen Übergangs; er liegt je zur Hälfte innen und außen vom Umriss.</param>
/// <param name="Rauschen">Wie weit (Meter) das Rauschen den Rand verschiebt.</param>
/// <param name="Fleck">0 … 1: wie stark die Stärke im Innern nach einem langsamen Rauschen schwankt (0 = gleichmäßig).</param>
/// <param name="Saat">Verschiebt das Rauschen (jede Fläche ein eigenes Muster).</param>
public sealed record Bodenstempel(Bodentextur Textur, double Staerke, double Rand, double Rauschen, double Fleck, int Saat);

public sealed partial class Bodenraster
{
    /// <summary>
    /// Drückt einen Stempel in dieses Raster (Eigenschaften siehe <see cref="Bodenstempel"/>). <paramref name="vielecke"/> sind Umrisse in Metern
    /// (x, z); gestempelt wird innerhalb ihrer Vereinigung und bis <c>Rand / 2 + Rauschen</c> Meter darum.
    /// </summary>
    public void Stempeln(IReadOnlyList<IReadOnlyList<(double X, double Z)>> vielecke, Bodenstempel s)
    {
        if (s.Textur == Bodentextur.Wiese || s.Staerke <= 0 || vielecke.Count == 0) return;
        var rand = Math.Max(0.05, s.Rand);
        var reichweite = rand / 2 + Math.Abs(s.Rauschen) * 1.2 + 1;
        // Umriss der Vereinigung: Stücke der Kanten (je höchstens 0,5 m), die nicht im Innern einer Fläche liegen – so entsteht an der Naht zweier
        // aneinanderstoßender Vielecke keine Kante (der kleinste Abstand zu allen Kanten wäre dort 0)
        var rahmen = vielecke.Where(v => v.Count >= 3).Select(v => (V: v, B: Rahmen(v))).ToList();
        if (rahmen.Count == 0) return;
        bool Drin(double x, double z)
        {
            foreach (var (v, r) in rahmen)
                if (x >= r.X0 && x <= r.X1 && z >= r.Z0 && z <= r.Z1 && ImVieleck(v, x, z)) return true;
            return false;
        }
        var stuecke = new List<(double Ax, double Az, double Bx, double Bz)>();
        foreach (var (v, _) in rahmen)
        {
            double flaeche2 = 0;
            for (int i = 0, j = v.Count - 1; i < v.Count; j = i++) flaeche2 += v[j].X * v[i].Z - v[i].X * v[j].Z;
            var seite = flaeche2 >= 0 ? 1.0 : -1.0; // Umlaufsinn: nach außen zeigt die Normale (dz, -dx), bei Gegenuhrzeigersinn
            for (int i = 0, j = v.Count - 1; i < v.Count; j = i++)
            {
                var (ax, az, bx, bz) = (v[j].X, v[j].Z, v[i].X, v[i].Z);
                var laenge = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                if (laenge < 1e-9) continue;
                var (nx, nz) = ((bz - az) / laenge * seite, -(bx - ax) / laenge * seite);
                var n = Math.Max(1, (int)Math.Ceiling(laenge / 0.5));
                for (var k = 0; k < n; k++)
                {
                    var (t0, t1) = ((double)k / n, (double)(k + 1) / n);
                    var (mx, mz) = (ax + (bx - ax) * (t0 + t1) / 2 + nx * 0.02, az + (bz - az) * (t0 + t1) / 2 + nz * 0.02);
                    if (!Drin(mx, mz)) stuecke.Add((ax + (bx - ax) * t0, az + (bz - az) * t0, ax + (bx - ax) * t1, az + (bz - az) * t1));
                }
            }
        }
        // Stücke in Zellen sortieren (Seite mindestens die Reichweite): gefragt wird nur die Nachbarschaft
        var zelle = Math.Max(4, Math.Ceiling(reichweite));
        var zellen = new Dictionary<(int, int), List<int>>();
        for (var n = 0; n < stuecke.Count; n++)
        {
            var q = stuecke[n];
            var (c0, d0, c1, d1) = ((int)Math.Floor(Math.Min(q.Ax, q.Bx) / zelle), (int)Math.Floor(Math.Min(q.Az, q.Bz) / zelle), (int)Math.Floor(Math.Max(q.Ax, q.Bx) / zelle), (int)Math.Floor(Math.Max(q.Az, q.Bz) / zelle));
            for (var d = d0; d <= d1; d++)
                for (var c = c0; c <= c1; c++)
                {
                    if (!zellen.TryGetValue((c, d), out var l)) zellen[(c, d)] = l = new List<int>();
                    l.Add(n);
                }
        }
        var (x0, z0, x1, z1) = (rahmen.Min(r => r.B.X0), rahmen.Min(r => r.B.Z0), rahmen.Max(r => r.B.X1), rahmen.Max(r => r.B.Z1));
        var (i0, j0) = ((int)Math.Floor((x0 - reichweite) / Abstand), (int)Math.Floor((z0 - reichweite) / Abstand));
        var (breite, tiefe) = ((int)Math.Ceiling((x1 + reichweite) / Abstand) - i0 + 1, (int)Math.Ceiling((z1 + reichweite) / Abstand) - j0 + 1);
        var abstand = new double[breite * tiefe];
        for (var b = 0; b < tiefe; b++)
            for (var a = 0; a < breite; a++)
            {
                var (px, pz) = ((i0 + a) * Abstand, (j0 + b) * Abstand);
                var drin = Drin(px, pz);
                var kleinste = double.MaxValue;
                var (cx, cz) = ((int)Math.Floor(px / zelle), (int)Math.Floor(pz / zelle));
                for (var dz = -1; dz <= 1; dz++)
                    for (var dx = -1; dx <= 1; dx++)
                        if (zellen.TryGetValue((cx + dx, cz + dz), out var liste))
                            foreach (var n in liste)
                                kleinste = Math.Min(kleinste, AbstandZurStrecke(stuecke[n], px, pz));
                abstand[b * breite + a] = kleinste == double.MaxValue ? (drin ? -1e9 : 1e9) : drin ? -kleinste : kleinste;
            }
        var werte = new byte[Kanaele];
        var kanal = (int)s.Textur - 1;
        for (var b = 0; b < tiefe; b++)
            for (var a = 0; a < breite; a++)
            {
                var d = abstand[b * breite + a];
                if (d > reichweite) continue;
                var (px, pz) = ((i0 + a) * Abstand, (j0 + b) * Abstand);
                var verschoben = d + (Rausch(px * 0.45, pz * 0.45, s.Saat) - 0.5) * 2 * s.Rauschen;
                var deckung = 1 - GlatterSchritt(-rand / 2, rand / 2, verschoben);
                if (deckung <= 0) continue;
                var staerke = s.Staerke;
                if (s.Fleck > 0) staerke *= 1 - s.Fleck * GlatterSchritt(0.3, 0.7, Rausch(px * 0.28, pz * 0.28, s.Saat + 17));
                var soll = (int)Math.Round(staerke * deckung * 255);
                if (soll <= 0) continue;
                WerteLesen(i0 + a, j0 + b, werte);
                int wiese = 255;
                for (var c = 0; c < DekoKanal; c++) wiese -= werte[c];
                var neu = Math.Min(Math.Max(soll, werte[kanal]), werte[kanal] + wiese); // nur auf Kosten der Wiese, nie weniger als schon da
                if (neu == werte[kanal]) continue;
                werte[kanal] = (byte)neu;
                WerteSetzen(i0 + a, j0 + b, werte);
            }
    }

    private static double GlatterSchritt(double von, double bis, double x)
    {
        var t = Math.Clamp((x - von) / (bis - von), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static (double X0, double Z0, double X1, double Z1) Rahmen(IReadOnlyList<(double X, double Z)> v)
    {
        var r = (X0: double.MaxValue, Z0: double.MaxValue, X1: double.MinValue, Z1: double.MinValue);
        foreach (var (x, z) in v) r = (Math.Min(r.X0, x), Math.Min(r.Z0, z), Math.Max(r.X1, x), Math.Max(r.Z1, z));
        return r;
    }

    private static bool ImVieleck(IReadOnlyList<(double X, double Z)> v, double x, double z)
    {
        var drin = false;
        for (int i = 0, j = v.Count - 1; i < v.Count; j = i++)
            if ((v[i].Z > z) != (v[j].Z > z) && x < (v[j].X - v[i].X) * (z - v[i].Z) / (v[j].Z - v[i].Z) + v[i].X) drin = !drin;
        return drin;
    }

    private static double AbstandZurStrecke((double Ax, double Az, double Bx, double Bz) q, double x, double z)
    {
        var (ex, ez) = (q.Bx - q.Ax, q.Bz - q.Az);
        var laenge = ex * ex + ez * ez;
        var t = laenge < 1e-12 ? 0 : Math.Clamp(((x - q.Ax) * ex + (z - q.Az) * ez) / laenge, 0, 1);
        var (dx, dz) = (x - (q.Ax + ex * t), z - (q.Az + ez * t));
        return Math.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Ganzzahl-Hash → 0 … 1 (fest, plattformunabhängig).</summary>
    private static double Hash(int i, int j, int saat)
    {
        unchecked
        {
            var h = (uint)(i * 374761393 + j * 668265263 + saat * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (double)0x1000000;
        }
    }

    /// <summary>Weiches Wertrauschen 0 … 1 (zwei Stufen, bilinear zwischen Gitterwerten).</summary>
    private static double Rausch(double x, double z, int saat)
    {
        static double Stufe(double x, double z, int saat)
        {
            var (i, j) = ((int)Math.Floor(x), (int)Math.Floor(z));
            var (tx, tz) = (x - i, z - j);
            (tx, tz) = (tx * tx * (3 - 2 * tx), tz * tz * (3 - 2 * tz));
            var oben = Hash(i, j, saat) + (Hash(i + 1, j, saat) - Hash(i, j, saat)) * tx;
            var unten = Hash(i, j + 1, saat) + (Hash(i + 1, j + 1, saat) - Hash(i, j + 1, saat)) * tx;
            return oben + (unten - oben) * tz;
        }
        return (Stufe(x, z, saat) * 2 + Stufe(x * 2.3 + 7.1, z * 2.3 + 3.7, saat + 1)) / 3;
    }
}
