namespace Reichsgilde;

/// <summary>
/// <b>Begehbarkeit der Stadt für Passanten</b> (Nutzer, 14.11.2026): Sperrflächen aus Gebäuden, Mauern und Wohnhäusern als gedrehte Rechtecke.
/// Nur Darstellung (der Kern kennt sie nicht), ohne Godot-Typen, damit ein Test sie mitübersetzen kann (<c>BegehbarkeitTests</c>). Die Rechtecke liegen
/// in einem groben Gitter (8 m); eine Anfrage prüft nur die Rechtecke der eigenen Zelle (im Mittel wenige), also Mikrosekunden.
/// <see cref="Abstand"/> ist der Abstand der Körpermitte zum nächsten Hindernis (negativ darin). Eine Figur darf bis <see cref="Luft"/> an die Wand
/// heran (Körper 0,22 m plus Spalt 0,1 m), nie hinein.
/// </summary>
public sealed class Begehbarkeit
{
    public const float Koerper = 0.22f, Spalt = 0.1f, Luft = Koerper + Spalt;
    private const float Zelle = 8f;

    private readonly record struct Kasten(float Mx, float Mz, float Ax, float Az, float Bx, float Bz, float Hx, float Hz, string Name, bool Rund = false, bool Mauer = false);

    private readonly List<Kasten> _kaesten = new();
    private readonly Dictionary<(int, int), List<int>> _gitter = new();
    public int Anzahl => _kaesten.Count;

    public void Leeren() { _kaesten.Clear(); _gitter.Clear(); }

    private readonly List<(float X, float Z, float Dx, float Dz, float Breit, float Lang)> _gaenge = new();

    /// <summary>
    /// Ein Durchgang durch die Mauer (Torbogen): In diesem Rechteck (Mitte, Richtung des Gangs, halbe Breite, halbe Länge) zählen Mauerteile nicht als
    /// Hindernis – die Türme neben dem Bogen reichen als Quader weiter hinein als das Tor breit ist.
    /// </summary>
    public void Durchgang(float x, float z, float dx, float dz, float breit, float lang) => _gaenge.Add((x, z, dx, dz, breit, lang));
    public void DurchgaengeLeeren() => _gaenge.Clear();

    private bool ImDurchgang(float x, float z)
    {
        foreach (var g in _gaenge)
        {
            var (ex, ez) = (x - g.X, z - g.Z);
            if (MathF.Abs(ex * g.Dx + ez * g.Dz) <= g.Lang && MathF.Abs(-ex * g.Dz + ez * g.Dx) <= g.Breit) return true;
        }
        return false;
    }

    /// <summary>Fügt ein gedrehtes Rechteck hinzu: Mitte, die beiden Einheitsachsen (a quer, b längs) und die halben Maße.</summary>
    public void Hinzu(float mx, float mz, float ax, float az, float bx, float bz, float hx, float hz, string name = "", bool rund = false, bool mauer = false)
    {
        var nummer = _kaesten.Count;
        _kaesten.Add(new Kasten(mx, mz, ax, az, bx, bz, hx, hz, name, rund, mauer));
        var ex = MathF.Abs(ax) * hx + MathF.Abs(bx) * hz + Luft + 0.5f;
        var ez = MathF.Abs(az) * hx + MathF.Abs(bz) * hz + Luft + 0.5f;
        var (x0, x1) = ((int)MathF.Floor((mx - ex) / Zelle), (int)MathF.Floor((mx + ex) / Zelle));
        var (z0, z1) = ((int)MathF.Floor((mz - ez) / Zelle), (int)MathF.Floor((mz + ez) / Zelle));
        for (var x = x0; x <= x1; x++)
            for (var z = z0; z <= z1; z++)
            {
                if (!_gitter.TryGetValue((x, z), out var liste)) _gitter[(x, z)] = liste = new List<int>();
                liste.Add(nummer);
            }
    }

    /// <summary>Abstand eines Punktes zum nächsten Hindernis (negativ darin; weiter als 8 m wird nicht gesucht und als 8 gemeldet).</summary>
    public float Abstand(float x, float z)
    {
        if (!_gitter.TryGetValue(((int)MathF.Floor(x / Zelle), (int)MathF.Floor(z / Zelle)), out var liste)) return Zelle;
        var best = Zelle;
        var tor = _gaenge.Count > 0 && ImDurchgang(x, z);
        foreach (var i in liste)
        {
            var k = _kaesten[i];
            if (tor && k.Mauer) continue;
            var abstand = Entfernung(k, x, z);
            if (abstand < best) best = abstand;
        }
        return best;
    }

    private static float Entfernung(Kasten k, float x, float z)
    {
        var (dx, dz) = (x - k.Mx, z - k.Mz);
        if (k.Rund) return MathF.Sqrt(dx * dx + dz * dz) - k.Hx;
        var (qx, qz) = (MathF.Abs(dx * k.Ax + dz * k.Az) - k.Hx, MathF.Abs(dx * k.Bx + dz * k.Bz) - k.Hz);
        return qx > 0 || qz > 0 ? MathF.Sqrt(MathF.Max(qx, 0) * MathF.Max(qx, 0) + MathF.Max(qz, 0) * MathF.Max(qz, 0)) : MathF.Max(qx, qz);
    }

    /// <summary>Fügt einen Kreis hinzu (Baum, Stein, Stand).</summary>
    public void HinzuRund(float mx, float mz, float radius, string name = "") => Hinzu(mx, mz, 1, 0, 0, 1, radius, radius, name, true);

    /// <summary>Name des nächsten Hindernisses (für Protokolle) und sein Abstand.</summary>
    public (string Name, float Abstand) Naechstes(float x, float z)
    {
        if (!_gitter.TryGetValue(((int)MathF.Floor(x / Zelle), (int)MathF.Floor(z / Zelle)), out var liste)) return ("", Zelle);
        var (name, best) = ("", Zelle);
        var tor = _gaenge.Count > 0 && ImDurchgang(x, z);
        foreach (var i in liste)
        {
            var k = _kaesten[i];
            if (tor && k.Mauer) continue;
            var abstand = Entfernung(k, x, z);
            if (abstand < best) (name, best) = (k.Name, abstand);
        }
        return (name, best);
    }

    /// <summary>Ist die Körpermitte so nah an einer Wand (oder darin), dass der Spalt unterschritten ist?</summary>
    public bool Zunah(float x, float z) => Abstand(x, z) < Luft;

    /// <summary>
    /// Wie viel der Strecke von der Mittellinie (<paramref name="mx"/>, <paramref name="mz"/>) zur Lage der Figur (<paramref name="hx"/>, <paramref name="hz"/>)
    /// begehbar bleibt (0…1): die Figur rückt zur Mittellinie zusammen. Liegt schon die Mittellinie an der Wand (Tür, Wegende), bleibt es bei 1.
    /// </summary>
    public float Anteil(float mx, float mz, float hx, float hz)
    {
        if (!Zunah(hx, hz) || Zunah(mx, mz)) return 1;
        float lo = 0, hi = 1;
        for (var i = 0; i < 6; i++)
        {
            var m = (lo + hi) / 2;
            if (Zunah(mx + (hx - mx) * m, mz + (hz - mz) * m)) hi = m; else lo = m;
        }
        return lo;
    }

    /// <summary>
    /// Wie viele Meter der Strecke a–b im Hindernis liegen (Körpermitte näher als der Körper an der Wand; Schritte zu 0,25 m), ohne 2,5 m um
    /// <paramref name="anfang"/> und <paramref name="ende"/> (Türen). <paramref name="ersterX"/>, <paramref name="ersterZ"/>: die erste verletzte Stelle.
    /// </summary>
    public float Verletzt(float ax, float az, float bx, float bz, (float X, float Z) anfang, (float X, float Z) ende, out float ersterX, out float ersterZ)
    {
        var laenge = MathF.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
        var n = Math.Max(1, (int)MathF.Ceiling(laenge / 0.25f));
        float summe = 0;
        (ersterX, ersterZ) = (ax, az);
        for (var i = 0; i <= n; i++)
        {
            var t = i / (float)n;
            var (qx, qz) = (ax + (bx - ax) * t, az + (bz - az) * t);
            if ((qx - anfang.X) * (qx - anfang.X) + (qz - anfang.Z) * (qz - anfang.Z) < 6.25f || (qx - ende.X) * (qx - ende.X) + (qz - ende.Z) * (qz - ende.Z) < 6.25f) continue;
            if (Abstand(qx, qz) >= Koerper - 0.01f) continue;
            if (summe == 0) (ersterX, ersterZ) = (qx, qz);
            summe += laenge / n;
        }
        return summe;
    }
}
