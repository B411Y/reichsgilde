namespace Reichsgilde;

/// <summary>
/// Ein Stück Zaun als gedrehte Kiste auf dem Boden: Mitte, Längsrichtung (Einheitsvektor), halbe Länge und halbe Dicke (Meter).
/// Reine Rechnung ohne Godot, damit sie getestet werden kann (<c>EgoZaunTests</c>).
/// </summary>
public readonly record struct EgoZaunKiste(float MitteX, float MitteZ, float AchseX, float AchseZ, float HalbLaenge, float HalbDicke)
{
    /// <summary>Liegt der Punkt in der Kiste (ohne Rand für die Figur)?</summary>
    public bool Enthaelt(float x, float z)
    {
        var (dx, dz) = (x - MitteX, z - MitteZ);
        var laengs = dx * AchseX + dz * AchseZ;
        var quer = -dx * AchseZ + dz * AchseX;
        return Math.Abs(laengs) <= HalbLaenge && Math.Abs(quer) <= HalbDicke;
    }
}

/// <summary>
/// <b>Zäune der Umlandstellen als Hindernisse der Ego-Perspektive</b>: ein Linienzug (die Pfostenreihe des Modells) wird zu einer Kette von Kisten, ein Tor
/// oder eine Lücke schneidet ein Stück heraus – so läuft man durch das Tor in die Koppel, der Zaun daneben bleibt fest. Die Enden sind um die halbe Dicke
/// verlängert, damit an den Ecken kein Spalt bleibt.
/// </summary>
public static class EgoZaun
{
    /// <summary>
    /// Die Kisten eines Zauns. <paramref name="punkte"/> sind die Pfostenorte (x, z), <paramref name="geschlossen"/> verbindet den letzten mit dem ersten;
    /// jede Lücke (Mitte x, z und Breite) schneidet aus dem Segment, auf dem sie liegt, ein Stück dieser Breite heraus (nur Segmente, die höchstens
    /// <paramref name="halbDicke"/> + 0,5 m an der Mitte vorbeilaufen).
    /// </summary>
    public static List<EgoZaunKiste> Kisten(IReadOnlyList<(float X, float Z)> punkte, float halbDicke, bool geschlossen = false, IEnumerable<(float X, float Z, float Breite)>? luecken = null)
    {
        var kisten = new List<EgoZaunKiste>();
        var lueckenListe = luecken?.ToList() ?? new();
        var segmente = punkte.Count - (geschlossen ? 0 : 1);
        for (var i = 0; i < segmente; i++)
        {
            var (a, b) = (punkte[i], punkte[(i + 1) % punkte.Count]);
            var (dx, dz) = (b.X - a.X, b.Z - a.Z);
            var laenge = MathF.Sqrt(dx * dx + dz * dz);
            if (laenge < 1e-4f) continue;
            var (ax, az) = (dx / laenge, dz / laenge);
            // Abschnitte [von, bis] entlang des Segments, ohne die Lücken
            var abschnitte = new List<(float Von, float Bis)> { (0f, laenge) };
            foreach (var (lx, lz, breite) in lueckenListe)
            {
                var (rx, rz) = (lx - a.X, lz - a.Z);
                var t = rx * ax + rz * az;
                var quer = MathF.Abs(-rx * az + rz * ax);
                if (quer > halbDicke + 0.5f || t < 0 || t > laenge) continue;
                var (von, bis) = (t - breite / 2f, t + breite / 2f);
                var neu = new List<(float, float)>();
                foreach (var (v, bi) in abschnitte)
                {
                    if (bis <= v || von >= bi) { neu.Add((v, bi)); continue; }
                    if (von > v) neu.Add((v, von));
                    if (bis < bi) neu.Add((bis, bi));
                }
                abschnitte = neu;
            }
            foreach (var (von, bis) in abschnitte)
            {
                if (bis - von < 0.05f) continue;
                // die Enden an den Lücken bleiben bündig, nur Zaunenden und Ecken wachsen um die halbe Dicke
                var ende0 = von <= 1e-4f ? von - halbDicke : von;
                var ende1 = bis >= laenge - 1e-4f ? bis + halbDicke : bis;
                var mitte = (ende0 + ende1) / 2f;
                kisten.Add(new EgoZaunKiste(a.X + ax * mitte, a.Z + az * mitte, ax, az, (ende1 - ende0) / 2f, halbDicke));
            }
        }
        return kisten;
    }
}
