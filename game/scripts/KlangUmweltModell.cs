using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Die <b>Umgebung um den Blickpunkt</b> für die Umgebungsklänge (Ton Schritt 4, Nachbesserung 31.10.2026) ohne Godot: wie viel Stadt (Häuser,
/// Gassen, Mauern) und wie viel Natur (Bäume, Felder, Wasser) in der Nähe der Kamera liegt. Draußen auf der Wiese oder im Wald soll man kein
/// Stadtgemurmel hören, am Fluss das Wasser, im Wald das Laub.
///
/// <para><b>Raster.</b> Aus der Karte der Partie (<see cref="Stadtkarte"/>, nur gelesen) entsteht einmal ein grobes Raster (Zellen von
/// <see cref="Zelle"/> m) mit vier Werten je Zelle (0–1): Stadt (nahe bei Häusern der Stadt, in der Mauer), Baum (Wald und Streu der Naturflächen
/// nach ihrer Dichte), Feld (Äcker) und Wasser (Fluss und Bach mit Uferstreifen, Seen). Gebaut wird es erst bei der ersten Abfrage, im Spiel also
/// hinter dem Ladebildschirm.</para>
///
/// <para><b>Abfrage.</b> <see cref="Raster.Probe"/> mittelt die Zellen im Umkreis des Blickpunkts (voll bis 30 % des Radius, danach Smoothstep auf 0 wie bei den
/// Menschen). Der <b>Stadtanteil</b> ist Stadt gegen Natur: offene Wiese zählt als Natur (mindestens <see cref="WieseNatur"/> je Zelle), so
/// dass er auf freiem Feld gegen 0 geht und in der Stadtmitte gegen 1.</para>
/// </summary>
public static class KlangUmweltModell
{
    /// <summary>Kantenlänge einer Rasterzelle in Metern.</summary>
    public const float Zelle = 8f;

    /// <summary>Ohne Wald und Feld zählt eine Zelle als Wiese: so viel „Natur“ (0–1), damit der Stadtanteil auf offenem Land gegen 0 geht.</summary>
    public const float WieseNatur = 0.35f;

    /// <summary>Die Umgebung in einem Bild: alle Werte 0–1.</summary>
    /// <param name="Stadt">Anteil Stadt gegen Natur (0 = freies Land, 1 = mitten in der Stadt).</param>
    /// <param name="Baum">Wie viel Wald und Gebüsch (Laubrauschen, Waldvögel).</param>
    /// <param name="Feld">Wie viel Acker und Wiese (Insekten, Krähen).</param>
    /// <param name="Wasser">Wie nah und wie viel Fluss, Bach oder See (Plätschern, Frösche).</param>
    public readonly record struct Lage(float Stadt, float Baum, float Feld, float Wasser)
    {
        /// <summary>Wie viel Natur um den Blickpunkt: das Größere von Baum, Feld und Wasser, mindestens der Rest der Stadt.</summary>
        public float Natur => Math.Max(Math.Max(Baum, Feld), Wasser) * (1f - 0.6f * Stadt);

        /// <summary>Ohne Karte (Selbsttests, Hauptmenü): Stadtmitte mit etwas Grün – alles wie vor der Nachbesserung.</summary>
        public static readonly Lage Stadtmitte = new(1f, 0f, 0f, 0f);
    }

    /// <summary>Das Raster einer Karte.</summary>
    public sealed class Raster
    {
        private readonly float _x0, _z0;
        private readonly int _nx, _nz;
        private readonly float[] _stadt, _baum, _feld, _wasser;

        private Raster(float x0, float z0, int nx, int nz)
        {
            (_x0, _z0, _nx, _nz) = (x0, z0, nx, nz);
            _stadt = new float[nx * nz];
            _baum = new float[nx * nz];
            _feld = new float[nx * nz];
            _wasser = new float[nx * nz];
        }

        /// <summary>Wie die Zellen belegt sind (für Tests und das Log).</summary>
        public (float Stadt, float Baum, float Feld, float Wasser) Zellwerte(float x, float z)
        {
            var i = (int)MathF.Floor((x - _x0) / Zelle);
            var j = (int)MathF.Floor((z - _z0) / Zelle);
            if (i < 0 || j < 0 || i >= _nx || j >= _nz) return default;
            var k = j * _nx + i;
            return (_stadt[k], _baum[k], _feld[k], _wasser[k]);
        }

        // ── Aufbau aus der Karte ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>Baut das Raster aus der Karte. Gebäude der Stadt, Mauerflächen, Wald, Streu, Felder und Gewässer werden eingestempelt.</summary>
        public static Raster Von(Stadtkarte karte)
        {
            var g = karte.Kartengrenzen;
            // Spielfeld plus ein Streifen des Rands (die Kamera sieht ein Stück darüber hinaus)
            var halbB = (float)(g.Breite / 2 + Math.Min(g.Randbreite, 200));
            var halbT = (float)(g.Tiefe / 2 + Math.Min(g.Randbreite, 200));
            float mx = (float)g.Mitte.X, mz = (float)g.Mitte.Z;
            var nx = Math.Clamp((int)MathF.Ceiling(2 * halbB / Zelle), 8, 400);
            var nz = Math.Clamp((int)MathF.Ceiling(2 * halbT / Zelle), 8, 400);
            var r = new Raster(mx - halbB, mz - halbT, nx, nz);

            // Gebäude der Stadt (nicht Umlandbetriebe, Berge, Umlandstellen, Wasserbau): Stadt rund um den Fußabdruck
            foreach (var b in karte.Gebaeude)
            {
                if (b.Baustein is not { } bs || !IstStadtbau(bs.Art)) continue;
                var gr = bs.Groesse is { Length: 2 } s ? Math.Max(s[0], s[1]) * b.Massstab : 10;
                r.StempelKreis((float)b.Punkt[0], (float)b.Punkt[1], (float)gr / 2f + 4f, 10f, r._stadt, 1f);
            }
            // In der Mauer: die Stadt, auch zwischen den Häusern (Gassen, Höfe)
            foreach (var m in karte.Mauern)
            {
                if (m.Linie.Count < 3) continue;
                r.FuelleVieleck(m.Linie, null, r._stadt, 0.6f);
            }
            // Wald und Streu nach Dichte je 1000 m²: 10 = dichter Wald (1), Streu sehr viel lichter
            foreach (var f in karte.Natur.Flaechen)
            {
                if (f.Flaeche.Count < 3) continue;
                var wert = f.Art == "wald" ? Math.Clamp((float)f.Dichte / 8f, 0.25f, 1f) : Math.Clamp((float)f.Dichte / 30f, 0.1f, 0.3f);
                r.FuelleVieleck(f.Flaeche, f.Aussparungen, r._baum, wert);
            }
            foreach (var f in karte.Natur.Felder)
                if (f.Flaeche.Count >= 3) r.FuelleVieleck(f.Flaeche, f.Aussparungen, r._feld, 1f);
            foreach (var ge in karte.Gelaende)
            {
                if (ge.Flaeche.Count < 3) continue;
                if (ge.Art == "feld") r.FuelleVieleck(ge.Flaeche, null, r._feld, 1f);
                else if (ge.Art == "wald") r.FuelleVieleck(ge.Flaeche, null, r._baum, 0.8f);
            }
            // Wasser: Linie mit halber Breite plus Uferstreifen, Fläche samt Rand
            foreach (var w in karte.Wasser)
            {
                if (w.IstLinie && w.Linie is { Count: >= 2 } l)
                {
                    var halb = (float)(w.Breite > 0 ? w.Breite : 8) / 2f;
                    for (var i = 0; i + 1 < l.Count; i++)
                    {
                        var breite = l[i].Length >= 3 ? (float)l[i][2] / 2f : halb;
                        r.StempelStrecke((float)l[i][0], (float)l[i][1], (float)l[i + 1][0], (float)l[i + 1][1], breite, 14f, r._wasser);
                    }
                }
                else if (w.Flaeche is { Count: >= 3 } fl)
                {
                    r.FuelleVieleck(fl, null, r._wasser, 1f);
                    r.StempelUmriss(fl, 12f, r._wasser);
                }
            }
            return r;
        }

        private static bool IstStadtbau(string art) => art is Bausteinart.Werkstattplatz or Bausteinart.Wohnhausplatz or Bausteinart.Einwohnerplatz
            or Bausteinart.Kulisse or Bausteinart.Sonderbau or Bausteinart.Werkstatt or Bausteinart.Wohnhaus or Bausteinart.Einwohnerhaus;

        private void StempelKreis(float cx, float cz, float innen, float fall, float[] ziel, float wert)
        {
            var aussen = innen + fall;
            int i0 = Spalte(cx - aussen), i1 = Spalte(cx + aussen), j0 = Reihe(cz - aussen), j1 = Reihe(cz + aussen);
            for (var j = j0; j <= j1; j++)
                for (var i = i0; i <= i1; i++)
                {
                    float px = _x0 + (i + 0.5f) * Zelle - cx, pz = _z0 + (j + 0.5f) * Zelle - cz;
                    var d = MathF.Sqrt(px * px + pz * pz);
                    var w = wert * (1f - KlangUmgebungModell.Glatt(innen, aussen, d));
                    ref var z = ref ziel[j * _nx + i];
                    if (w > z) z = w;
                }
        }

        private void StempelStrecke(float ax, float az, float bx, float bz, float halbBreite, float ufer, float[] ziel)
        {
            var aussen = halbBreite + ufer;
            int i0 = Spalte(Math.Min(ax, bx) - aussen), i1 = Spalte(Math.Max(ax, bx) + aussen);
            int j0 = Reihe(Math.Min(az, bz) - aussen), j1 = Reihe(Math.Max(az, bz) + aussen);
            float dx = bx - ax, dz = bz - az, l2 = dx * dx + dz * dz;
            for (var j = j0; j <= j1; j++)
                for (var i = i0; i <= i1; i++)
                {
                    float px = _x0 + (i + 0.5f) * Zelle, pz = _z0 + (j + 0.5f) * Zelle;
                    var t = l2 <= 0 ? 0 : Math.Clamp(((px - ax) * dx + (pz - az) * dz) / l2, 0f, 1f);
                    float qx = ax + t * dx - px, qz = az + t * dz - pz;
                    var d = MathF.Sqrt(qx * qx + qz * qz);
                    var w = 1f - KlangUmgebungModell.Glatt(halbBreite, aussen, d);
                    ref var z = ref ziel[j * _nx + i];
                    if (w > z) z = w;
                }
        }

        private void StempelUmriss(IReadOnlyList<double[]> vieleck, float ufer, float[] ziel)
        {
            for (var i = 0; i < vieleck.Count; i++)
            {
                var a = vieleck[i];
                var b = vieleck[(i + 1) % vieleck.Count];
                StempelStrecke((float)a[0], (float)a[1], (float)b[0], (float)b[1], 0f, ufer, ziel);
            }
        }

        private void FuelleVieleck(IReadOnlyList<double[]> vieleck, List<List<double[]>>? loecher, float[] ziel, float wert)
        {
            double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
            foreach (var p in vieleck)
            {
                minX = Math.Min(minX, p[0]); maxX = Math.Max(maxX, p[0]);
                minZ = Math.Min(minZ, p[1]); maxZ = Math.Max(maxZ, p[1]);
            }
            int i0 = Spalte((float)minX), i1 = Spalte((float)maxX), j0 = Reihe((float)minZ), j1 = Reihe((float)maxZ);
            for (var j = j0; j <= j1; j++)
                for (var i = i0; i <= i1; i++)
                {
                    double px = _x0 + (i + 0.5) * Zelle, pz = _z0 + (j + 0.5) * Zelle;
                    if (!Drin(px, pz, vieleck)) continue;
                    if (loecher is not null && loecher.Any(l => l.Count >= 3 && Drin(px, pz, l))) continue;
                    ref var z = ref ziel[j * _nx + i];
                    if (wert > z) z = wert;
                }
        }

        private static bool Drin(double x, double z, IReadOnlyList<double[]> v)
        {
            var drin = false;
            for (int i = 0, j = v.Count - 1; i < v.Count; j = i++)
            {
                double x1 = v[i][0], z1 = v[i][1], x2 = v[j][0], z2 = v[j][1];
                if ((z1 > z) != (z2 > z) && x < (x2 - x1) * (z - z1) / (z2 - z1) + x1) drin = !drin;
            }
            return drin;
        }

        private int Spalte(float x) => Math.Clamp((int)MathF.Floor((x - _x0) / Zelle), 0, _nx - 1);
        private int Reihe(float z) => Math.Clamp((int)MathF.Floor((z - _z0) / Zelle), 0, _nz - 1);

        // ── Abfrage ──────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Die Umgebung im Umkreis <paramref name="radius"/> um den Blickpunkt. Zellen außerhalb des Rasters zählen als Wiese. Stadtanteil =
        /// Stadt ÷ (Stadt + Natur), wobei jede Zelle außerhalb der Stadt mindestens <see cref="WieseNatur"/> Natur hat.
        /// </summary>
        public Lage Probe(float x, float z, float radius)
        {
            radius = Math.Max(radius, Zelle);
            double ws = 0, sS = 0, sB = 0, sF = 0, sW = 0, sN = 0;
            int i0 = (int)MathF.Floor((x - radius - _x0) / Zelle), i1 = (int)MathF.Floor((x + radius - _x0) / Zelle);
            int j0 = (int)MathF.Floor((z - radius - _z0) / Zelle), j1 = (int)MathF.Floor((z + radius - _z0) / Zelle);
            for (var j = j0; j <= j1; j++)
                for (var i = i0; i <= i1; i++)
                {
                    float px = _x0 + (i + 0.5f) * Zelle - x, pz = _z0 + (j + 0.5f) * Zelle - z;
                    var d = MathF.Sqrt(px * px + pz * pz);
                    var w = KlangStadtlebenModell.Nahe(d, radius);
                    if (w <= 0) continue;
                    float s = 0, b = 0, f = 0, wa = 0;
                    f = WieseNatur; // außerhalb des Rasters: Wiese
                    if (i >= 0 && j >= 0 && i < _nx && j < _nz)
                    {
                        var k = j * _nx + i;
                        (s, b, f, wa) = (_stadt[k], _baum[k], _feld[k], _wasser[k]);
                        b *= 1f - s; // die Waldflächen der Karte reichen in die Stadt hinein, gewachsen ist dort nichts (Auswahl beim Bau)
                        f = Math.Max(f, WieseNatur) * (1f - s); // offener Boden ist Wiese: Grillen, Krähen
                    }
                    ws += w;
                    sS += w * s;
                    sB += w * b;
                    sF += w * f;
                    sW += w * wa;
                    sN += w * (1f - s) * Math.Max(WieseNatur, Math.Max(b, f));
                }
            if (ws <= 0) return default;
            double stadt = sS / ws, natur = sN / ws;
            var anteil = stadt + natur <= 1e-6 ? 0.0 : stadt / (stadt + natur);
            return new Lage((float)anteil, (float)Math.Min(1.0, 3.0 * sB / ws), (float)Math.Min(1.0, 2.0 * sF / ws), (float)Math.Min(1.0, 4.0 * sW / ws));
        }
    }
}
