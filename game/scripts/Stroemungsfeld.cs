// Reichsgilde – Strömungsfeld eines Flusses oder Baches (Darstellung, ohne Godot-Typen, damit es sich einzeln prüfen lässt).
// Code: MIT.

using System;
using System.Collections.Generic;

namespace Reichsgilde;

/// <summary>
/// Das Strömungsfeld eines Laufs: ein Raster in Weltachsen über dem Band, das die Strömung je Punkt kennt – einmal beim Kartenaufbau gerechnet
/// (<c>Stadtansicht.Stroemungsfeld.cs</c>), vom Wasser-Shader als Flowmap gelesen. Die Strömung des Bandes ist überall gleich (Richtung der
/// Mittellinie mal Tempo); das Feld liefert den <b>Unterschied</b> dazu:
/// <list type="bullet">
/// <item><b>Ufer</b>: Reibung, das Wasser wird zum Rand hin langsamer (nach der Profilkurve des Kerns, <c>Wasserform.An</c>, und einer Grenzschicht von 3,5 m).</item>
/// <item><b>Hindernisse</b> (Pfeiler, Joche, Kähne, Boden über dem Spiegel mitten im Band, also Inseln und Ufervorsprünge): vor ihnen staut sich das Wasser
/// (die Strömung auf die Fläche zu wird abgebaut, so weicht es seitlich aus), an den Flanken beschleunigt es, hinter ihnen liegt ein Kehrwasser: langsamer,
/// im Kern gegenläufig, mit Zufluss von der Seite (Wirbel).</item>
/// <item><b>Kai</b>: vor einer Kaimauer ist es still (Eingabe <see cref="Eingabe.Kai"/>).</item>
/// </list>
/// Gerechnet wird eine einfache Umströmung (Abstandsfeld, abgebautes Normalstück, Schattenwurf gegen die Strömung, geglättet), keine Navier-Stokes-Lösung.
/// </summary>
public sealed class Stroemungsfeld
{
    /// <summary>Ein Hindernis: Mitte, halbe Ausdehnung quer zur Strömung und längs (wie bei den Eisschollen).</summary>
    public readonly record struct Hindernis(float X, float Z, float HalbQuer, float HalbLaengs);

    /// <summary>Ein Punkt der Mittellinie: Ort, Fließrichtung (Einheitsvektor), Breite, Höhe des Spiegels.</summary>
    public readonly record struct Punkt(float X, float Z, float RichtungX, float RichtungZ, float Breite, float Spiegel);

    /// <summary>Was das Feld braucht (alles aus der Darstellung, der Kern wird nur gelesen).</summary>
    public sealed class Eingabe
    {
        public IReadOnlyList<Punkt> Mittellinie = Array.Empty<Punkt>();
        /// <summary>Tempo der Strömung des Bandes in m/s (Mitte).</summary>
        public float Tempo;
        /// <summary>Das Band reicht über die halbe Breite hinaus bis halbe Breite · Faktor (Rand des Netzes).</summary>
        public float RandFaktor = 1.15f;
        public IReadOnlyList<Hindernis> Hindernisse = Array.Empty<Hindernis>();
        /// <summary>Höhe des Bodens an (x, z); liegt er über dem Spiegel, steht dort Land im Band. Null = nicht prüfen.</summary>
        public Func<float, float, float>? Boden;
        /// <summary>Nähe einer Kaimauer an (x, z), 0 bis 1. Null = keine.</summary>
        public Func<float, float, float>? Kai;
        /// <summary>Kantenlänge einer Zelle in Metern (Untergrenze; bei sehr langen Läufen größer, damit die Karte höchstens <see cref="HoechstSeite"/> Zellen breit bleibt).</summary>
        public float Zelle = 0.5f;
    }

    public const int HoechstSeite = 4096;

    /// <summary>Breite und Höhe in Zellen, Ursprung (x, z) der Ecke in Metern, Zelle in Metern.</summary>
    public int Breite { get; private set; }
    public int Hoehe { get; private set; }
    public float X0 { get; private set; }
    public float Z0 { get; private set; }
    public float Zelle { get; private set; }

    /// <summary>Spanne der Verschlüsselung: ±<see cref="Skala"/> m/s.</summary>
    public float Skala { get; private set; }

    /// <summary>RGBA8: R, G = Unterschied zur Strömung des Bandes in Weltachsen (0,5 = keiner), B = Kehrwasser (0–1), A = 255.</summary>
    public byte[] Bilder { get; private set; } = Array.Empty<byte>();

    /// <summary>Für Prüfung und Protokoll: Zellen im Band, Hindernis-Zellen, Zellen im Kehrwasser, kleinste und größte Geschwindigkeit entlang der Richtung (m/s).</summary>
    public (int Wasser, int Hindernis, int Kehrwasser, float Kleinst, float Groesst) Zahlen { get; private set; }

    /// <summary>Die Strömung (m/s, Weltachsen) und das Kehrwasser in Zelle (i, j), für Prüfungen.</summary>
    private float[] _vx = Array.Empty<float>(), _vz = Array.Empty<float>();

    public (float X, float Z) StromBei(int i, int j) => (_vx[j * Breite + i], _vz[j * Breite + i]);
    public float KehrBei(int i, int j) => Bilder[(j * Breite + i) * 4 + 2] / 255f;
    public bool IstWasser(int i, int j) => _wasser[j * Breite + i];
    private bool[] _wasser = Array.Empty<bool>();

    public static Stroemungsfeld? Rechnen(Eingabe e)
    {
        var m = e.Mittellinie;
        if (m.Count < 2) return null;
        var f = new Stroemungsfeld();
        f.Berechnen(e);
        return f;
    }

    private void Berechnen(Eingabe e)
    {
        var m = e.Mittellinie;
        // ── Rahmen: Mittellinie samt halber Breite, dazu ein Rand ──
        float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue, groessteBreite = 0;
        foreach (var p in m)
        {
            var r = p.Breite / 2 * e.RandFaktor + 1f;
            x0 = Math.Min(x0, p.X - r); x1 = Math.Max(x1, p.X + r);
            z0 = Math.Min(z0, p.Z - r); z1 = Math.Max(z1, p.Z + r);
            groessteBreite = Math.Max(groessteBreite, p.Breite);
        }
        const float rand = 3f;
        x0 -= rand; z0 -= rand; x1 += rand; z1 += rand;
        var zelle = Math.Max(e.Zelle, Math.Max(x1 - x0, z1 - z0) / HoechstSeite);
        var b = Math.Max(4, (int)Math.Ceiling((x1 - x0) / zelle));
        var h = Math.Max(4, (int)Math.Ceiling((z1 - z0) / zelle));
        (Breite, Hoehe, X0, Z0, Zelle) = (b, h, x0, z0, zelle);
        var n = b * h;

        var wasser = new bool[n];
        var ufer = new float[n];           // Abstand zum Rand des Bandes (halbe Breite minus Abstand zur Mitte), < 0 außerhalb
        var dx = new float[n];
        var dz = new float[n];             // Richtung der Mittellinie
        var spiegel = new float[n];
        var profil = new float[n];         // 1 − 0,5 · (quer / halbe Breite)², wie Wasserform.An
        Array.Fill(ufer, float.MinValue);

        // ── Das Band in Zellen schreiben (Schritt halbe Zelle entlang und quer; der nähere Rand gewinnt) ──
        var schritt = zelle * 0.5f;
        for (var i = 0; i + 1 < m.Count; i++)
        {
            var (p, q) = (m[i], m[i + 1]);
            var laenge = MathF.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Z - p.Z) * (q.Z - p.Z));
            var teile = Math.Max(1, (int)Math.Ceiling(laenge / schritt));
            for (var t = 0; t < teile; t++)
            {
                var a = (float)t / teile;
                var (px, pz) = (p.X + (q.X - p.X) * a, p.Z + (q.Z - p.Z) * a);
                var (rx, rz) = (p.RichtungX + (q.RichtungX - p.RichtungX) * a, p.RichtungZ + (q.RichtungZ - p.RichtungZ) * a);
                var rl = MathF.Sqrt(rx * rx + rz * rz);
                if (rl < 1e-4f) continue;
                (rx, rz) = (rx / rl, rz / rl);
                var halb = (p.Breite + (q.Breite - p.Breite) * a) / 2;
                var reich = halb * e.RandFaktor + zelle;
                var nq = (int)Math.Ceiling(reich / schritt);
                for (var k = -nq; k <= nq; k++)
                {
                    var quer = k * schritt;
                    var (wx, wz) = (px - rz * quer, pz + rx * quer);
                    var (ci, cj) = ((int)((wx - x0) / zelle), (int)((wz - z0) / zelle));
                    if (ci < 0 || cj < 0 || ci >= b || cj >= h) continue;
                    var idx = cj * b + ci;
                    var u = halb - MathF.Abs(quer);
                    if (u <= ufer[idx]) continue;
                    ufer[idx] = u;
                    dx[idx] = rx; dz[idx] = rz;
                    spiegel[idx] = p.Spiegel + (q.Spiegel - p.Spiegel) * a;
                    var qa = Math.Min(1f, MathF.Abs(quer) / Math.Max(halb, 1e-3f));
                    profil[idx] = 1f - 0.5f * qa * qa;
                    wasser[idx] = u >= 0;
                }
            }
        }

        // ── Boden über dem Spiegel mitten im Band: Insel oder Ufervorsprung; Pfeiler, Joche, Kähne ──
        var hind = new bool[n];
        if (e.Boden is not null)
        {
            for (var j = 0; j < h; j++)
                for (var i = 0; i < b; i++)
                {
                    var idx = j * b + i;
                    if (!wasser[idx] || ufer[idx] < 1.0f) continue; // am Rand ist das Ufer selbst da
                    var (wx, wz) = (x0 + (i + 0.5f) * zelle, z0 + (j + 0.5f) * zelle);
                    if (e.Boden(wx, wz) > spiegel[idx] + 0.08f) hind[idx] = true;
                }
        }
        foreach (var o in e.Hindernisse)
        {
            var r = MathF.Sqrt(o.HalbQuer * o.HalbQuer + o.HalbLaengs * o.HalbLaengs) + zelle;
            var (ia, ib) = ((int)((o.X - r - x0) / zelle), (int)((o.X + r - x0) / zelle));
            var (ja, jb) = ((int)((o.Z - r - z0) / zelle), (int)((o.Z + r - z0) / zelle));
            for (var j = Math.Max(0, ja); j <= Math.Min(h - 1, jb); j++)
                for (var i = Math.Max(0, ia); i <= Math.Min(b - 1, ib); i++)
                {
                    var idx = j * b + i;
                    if (!wasser[idx]) continue;
                    var (rx, rz) = (x0 + (i + 0.5f) * zelle - o.X, z0 + (j + 0.5f) * zelle - o.Z);
                    // längs und quer zur örtlichen Strömungsrichtung (wie die Hindernisse der Schollen in Laufachsen)
                    var laengs = rx * dx[idx] + rz * dz[idx];
                    var quer = -rx * dz[idx] + rz * dx[idx];
                    if (MathF.Abs(laengs) <= o.HalbLaengs && MathF.Abs(quer) <= o.HalbQuer) hind[idx] = true;
                }
        }

        // ── Abstandsfelder (Meter): zum Ufer (außerhalb des Bandes) und zu den Hindernissen ──
        var dHind = Abstand(hind, b, h, zelle);
        var dUfer = Abstand(Array.ConvertAll(wasser, w => !w), b, h, zelle);

        // ── Kehrwasser: gegen die Strömung zurück bis zum nächsten Hindernis; je näher, desto stärker ──
        var windschatten = new float[n];
        var laengeSchatten = Math.Max(10f, groessteBreite * 1.2f);
        var maxSchritte = (int)(laengeSchatten / zelle);
        for (var j = 0; j < h; j++)
            for (var i = 0; i < b; i++)
            {
                var idx = j * b + i;
                if (!wasser[idx] || hind[idx] || dHind[idx] > laengeSchatten) continue;
                var (wx, wz) = (i + 0.5f, j + 0.5f);
                var (sx, sz) = (-dx[idx], -dz[idx]);
                for (var s = 1; s <= maxSchritte; s++)
                {
                    var (ci, cj) = ((int)(wx + sx * s), (int)(wz + sz * s));
                    if (ci < 0 || cj < 0 || ci >= b || cj >= h) break;
                    if (!hind[cj * b + ci]) continue;
                    var w = 1f - (s * zelle) / laengeSchatten;
                    windschatten[idx] = w * MathF.Sqrt(w);
                    break;
                }
            }
        // Weich machen: sonst steht das Kehrwasser als scharfer Streifen, und der Zufluss von der Seite hätte nichts zu greifen
        var weich = Weichzeichnen(Weichzeichnen(windschatten, b, h, 2), b, h, 2);
        var dweich = Weichzeichnen(Weichzeichnen(weich, b, h, 1), b, h, 1);

        // ── Geschwindigkeit je Zelle ──
        var vx = new float[n];
        var vz = new float[n];
        var u0 = e.Tempo;
        int zahlWasser = 0, zahlHind = 0, zahlKehr = 0;
        float kleinst = float.MaxValue, groesst = float.MinValue;
        for (var j = 0; j < h; j++)
            for (var i = 0; i < b; i++)
            {
                var idx = j * b + i;
                if (hind[idx]) { zahlHind++; continue; } // unter dem Hindernis: Strömung 0
                if (ufer[idx] == float.MinValue) continue;
                var (rx, rz) = (dx[idx], dz[idx]);
                var (wx, wz) = (x0 + (i + 0.5f) * zelle, z0 + (j + 0.5f) * zelle);
                var (vxj, vzj) = (rx * u0 * profil[idx], rz * u0 * profil[idx]);

                if (wasser[idx])
                {
                    // Vor einem Hindernis: der Teil der Strömung auf die Fläche zu wird abgebaut (das Wasser staut und weicht seitlich aus)
                    var dh = dHind[idx];
                    if (dh < 6f)
                    {
                        var (nx, nz) = Gefaelle(dHind, i, j, b, h, zelle);
                        var gegen = vxj * nx + vzj * nz; // < 0: auf das Hindernis zu
                        var rl = MathF.Sqrt(nx * nx + nz * nz);
                        if (rl > 1e-3f)
                        {
                            (nx, nz) = (nx / rl, nz / rl);
                            gegen = vxj * nx + vzj * nz;
                            var staerke = 1f - SmoothStep(0f, 5f, dh);
                            if (gegen < 0) { vxj -= nx * gegen * staerke; vzj -= nz * gegen * staerke; }
                            // An den Flanken schneller (das Wasser muss durch die Enge): ohne Anteil quer auf das Hindernis zu
                            var flanke = 1f - MathF.Abs(gegen) / Math.Max(u0 * profil[idx], 1e-3f);
                            var beschleunigung = 1f + 0.55f * MathF.Exp(-dh / 1.6f) * Math.Clamp(flanke, 0f, 1f);
                            vxj *= beschleunigung; vzj *= beschleunigung;
                        }
                    }
                    // Reibung am Ufer (Grenzschicht von 3,5 m) und, schwächer, an Hindernissen (dort soll es an den Flanken ja schneller werden)
                    var reibung = (0.45f + 0.55f * SmoothStep(0f, 3.5f, dUfer[idx])) * (0.85f + 0.15f * SmoothStep(0f, 1f, dHind[idx]));
                    vxj *= reibung; vzj *= reibung;

                    // Kehrwasser: langsamer bis gegenläufig, dazu Zufluss von der Seite (zum Windschatten hin, der Gradient zeigt dorthin)
                    var w = Math.Clamp(weich[idx], 0f, 1f);
                    if (w > 0.004f)
                    {
                        var (gx, gz) = Gefaelle(dweich, i, j, b, h, zelle);
                        var faktor = 1f - 1.5f * w;
                        vxj = vxj * faktor + gx * u0 * 2.5f;
                        vzj = vzj * faktor + gz * u0 * 2.5f;
                        if (w > 0.12f) zahlKehr++;
                    }
                    if (e.Kai is not null)
                    {
                        var kai = Math.Clamp(e.Kai(wx, wz), 0f, 1f);
                        vxj *= 1f - 0.85f * kai; vzj *= 1f - 0.85f * kai;
                    }
                    zahlWasser++;
                    var laengsGeschw = vxj * rx + vzj * rz;
                    kleinst = Math.Min(kleinst, laengsGeschw);
                    groesst = Math.Max(groesst, laengsGeschw);
                }
                else
                {
                    // Über dem Rand des Netzes hinaus (unter dem Gelände): wie am Ufer
                    (vxj, vzj) = (vxj * 0.45f, vzj * 0.45f);
                }
                vx[idx] = vxj; vz[idx] = vzj;
            }
        (_vx, _vz, _wasser) = (vx, vz, wasser);

        // ── Verschlüsseln: Unterschied zur Strömung des Bandes, leicht geglättet (gegen Treppen der Zellen) ──
        var skala = Math.Max(1.5f, u0 * 2.5f);
        Skala = skala;
        var ex = new float[n];
        var ez = new float[n];
        for (var idx = 0; idx < n; idx++)
        {
            if (ufer[idx] == float.MinValue) continue;
            ex[idx] = vx[idx] - dx[idx] * u0;
            ez[idx] = vz[idx] - dz[idx] * u0;
        }
        ex = Weichzeichnen(ex, b, h, 1);
        ez = Weichzeichnen(ez, b, h, 1);
        var bytes = new byte[n * 4];
        for (var idx = 0; idx < n; idx++)
        {
            bytes[idx * 4 + 0] = Kodieren(ex[idx] / (2 * skala) + 0.5f);
            bytes[idx * 4 + 1] = Kodieren(ez[idx] / (2 * skala) + 0.5f);
            bytes[idx * 4 + 2] = Kodieren(Math.Clamp(weich[idx] * 1.4f, 0f, 1f));
            bytes[idx * 4 + 3] = 255;
        }
        Bilder = bytes;
        Zahlen = (zahlWasser, zahlHind, zahlKehr, kleinst == float.MaxValue ? 0 : kleinst, groesst == float.MinValue ? 0 : groesst);
    }

    private static byte Kodieren(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

    private static float SmoothStep(float a, float b, float x)
    {
        var t = Math.Clamp((x - a) / (b - a), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Zeigt von der Zelle weg vom Hindernis (Gefälle des Abstandsfeldes je Meter), Mittelwert aus den Nachbarn.</summary>
    private static (float X, float Z) Gefaelle(float[] d, int i, int j, int b, int h, float zelle)
    {
        var (l, r) = (d[j * b + Math.Max(0, i - 1)], d[j * b + Math.Min(b - 1, i + 1)]);
        var (o, u) = (d[Math.Max(0, j - 1) * b + i], d[Math.Min(h - 1, j + 1) * b + i]);
        return ((r - l) / (2 * zelle), (u - o) / (2 * zelle));
    }

    /// <summary>Abstand jeder Zelle zur nächsten gesetzten (Meter, Zwei-Durchgänge-Näherung mit Diagonalen), 0 in der gesetzten Zelle selbst.</summary>
    private static float[] Abstand(bool[] gesetzt, int b, int h, float zelle)
    {
        const float gross = 1e6f;
        var d = new float[b * h];
        var any = false;
        for (var i = 0; i < d.Length; i++) { d[i] = gesetzt[i] ? 0 : gross; any |= gesetzt[i]; }
        if (!any) { Array.Fill(d, 1000f); return d; }
        var dia = 1.41421356f;
        for (var j = 0; j < h; j++)
            for (var i = 0; i < b; i++)
            {
                var v = d[j * b + i];
                if (i > 0) v = Math.Min(v, d[j * b + i - 1] + 1);
                if (j > 0)
                {
                    v = Math.Min(v, d[(j - 1) * b + i] + 1);
                    if (i > 0) v = Math.Min(v, d[(j - 1) * b + i - 1] + dia);
                    if (i + 1 < b) v = Math.Min(v, d[(j - 1) * b + i + 1] + dia);
                }
                d[j * b + i] = v;
            }
        for (var j = h - 1; j >= 0; j--)
            for (var i = b - 1; i >= 0; i--)
            {
                var v = d[j * b + i];
                if (i + 1 < b) v = Math.Min(v, d[j * b + i + 1] + 1);
                if (j + 1 < h)
                {
                    v = Math.Min(v, d[(j + 1) * b + i] + 1);
                    if (i + 1 < b) v = Math.Min(v, d[(j + 1) * b + i + 1] + dia);
                    if (i > 0) v = Math.Min(v, d[(j + 1) * b + i - 1] + dia);
                }
                d[j * b + i] = v;
            }
        for (var i = 0; i < d.Length; i++) d[i] = Math.Min(d[i] * zelle, 1000f);
        return d;
    }

    /// <summary>Kastenfilter mit dem Halbmesser <paramref name="r"/> Zellen (getrennt in x und y).</summary>
    private static float[] Weichzeichnen(float[] q, int b, int h, int r)
    {
        var t = new float[q.Length];
        var z = new float[q.Length];
        var nenner = 2 * r + 1f;
        for (var j = 0; j < h; j++)
            for (var i = 0; i < b; i++)
            {
                float s = 0;
                for (var k = -r; k <= r; k++) s += q[j * b + Math.Clamp(i + k, 0, b - 1)];
                t[j * b + i] = s / nenner;
            }
        for (var j = 0; j < h; j++)
            for (var i = 0; i < b; i++)
            {
                float s = 0;
                for (var k = -r; k <= r; k++) s += t[Math.Clamp(j + k, 0, h - 1) * b + i];
                z[j * b + i] = s / nenner;
            }
        return z;
    }
}
