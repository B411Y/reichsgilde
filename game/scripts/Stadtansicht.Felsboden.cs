using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Boden um den Fels (Felspinsel F6) – ein Teil der <c>partial class Stadtansicht</c>. Den Boden selbst (Fels, Schuttkegel aus Kies, Moossaum) malt
/// der Kern ins Bodenraster (<see cref="Felsboden"/>); hier nur:
/// <list type="bullet">
/// <item><b>Farbe der Gesteinsart</b>: Fels im Boden (am Hang und gemalt) nimmt Hell und Dunkel des Grundgesteins der Karte an, Kies etwas dessen
/// Schuttfarbe – über die globalen Uniforms <c>boden_gestein_*</c> (boden.gdshaderinc). Karten ohne Felsen behalten den Boden wie bisher.</item>
/// <item><b>Schuttsteine</b>: kleine Steine der Form „schuttbrocken“ im Kegel vor jedem Stück (<see cref="Felsboden.Schuttsteine"/>), mit dem
/// Fels-Material des Stücks, ohne Schatten und nur nah (Stufe 0 und 1), nur wo Kies liegt; sie werden mit der Zelle ihres Stücks gebaut, in der
/// Werkstatt auch nach jeder Änderung des Bodens darunter.</item>
/// <item><b>Felsnähe</b> (Fels R1): Der Kern malt den Boden je Stück; nebeneinander standen so graue Inseln in der Wiese. In den freien Kanal A des
/// dritten Gewichtsbilds jeder Kachel kommt darum die geglättete Menge von Fels und Kies ringsum (<see cref="FelsnaeheEintragen"/>, etwa 6 m weit).
/// Der Boden-Shader macht daraus auf Karten mit Felsen eine steinige, magere Wiese mit Kies und Erde, die benachbarte Flecken zu einer Fläche
/// verbindet und ihre Ränder ausfranst. Nur Darstellung: Das Bodenraster des Kerns bleibt, wie es ist.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ohne Bodenraster: überall Wiese – dann liegen keine Schuttsteine.</summary>
    private static readonly Bodenraster LeererBoden = new();

    private bool? _felsbodenAn;
    private string? _felsbodenGestein;

    /// <summary>Setzt die Farbe der Gesteinsart für den Boden (<paramref name="an"/> false: Karte ohne Felsen, Boden wie bisher).</summary>
    private void FelsbodenFarbe(string gestein, bool an)
    {
        if (_felsbodenAn == an && _felsbodenGestein == gestein) return;
        (_felsbodenAn, _felsbodenGestein) = (an, gestein);
        var g = Gesteinsarten.Nach(gestein);
        static Vector3 Linear(Color c)
        {
            var l = c.SrgbToLinear();
            return new Vector3(l.R, l.G, l.B);
        }
        RenderingServer.GlobalShaderParameterSet("boden_gestein_hell", Linear(g.Hell));
        RenderingServer.GlobalShaderParameterSet("boden_gestein_dunkel", Linear(g.Dunkel));
        RenderingServer.GlobalShaderParameterSet("boden_gestein_schutt", Linear(g.SchuttFarbe));
        RenderingServer.GlobalShaderParameterSet("boden_gestein_an", an ? 1f : 0f);
    }

    /// <summary>Werkstatt: die Farbe nach dem Entwurf (an, sobald er ein Stück oder ein Grundgestein hat).</summary>
    private void FelsbodenFarbeNachEntwurf()
    {
        if (_entwurf is null) return;
        FelsbodenFarbe(_entwurf.Grundgestein.Wirksam, _entwurf.Felsen.Count > 0 || _entwurf.Grundgestein.Gestein is not null);
    }

    /// <summary>Radius (Rasterpunkte = Meter) der beiden Kastenfilter der Felsnähe; zusammen reichen sie doppelt so weit.</summary>
    private const int FelsnaeheRadius = 3;

    /// <summary>So weit (Meter) wirkt Fels oder Kies in die Felsnähe hinein.</summary>
    private const int FelsnaeheReichweite = 2 * FelsnaeheRadius;

    /// <summary>
    /// Schreibt die Felsnähe in Kanal A von <paramref name="splat2"/> (Bild der Gruppe 2 ab Punkt i0, j0, <see cref="BodenBild"/> Punkte je Seite):
    /// Fels und Kies (Kies zu 80 %) ringsum, zweimal mit einem Kastenfilter geglättet (fast eine Glocke) und verstärkt – mitten in einem Fleck 1,
    /// zwischen zwei Flecken wenige Meter auseinander noch deutlich, weit weg 0.
    /// </summary>
    private static void FelsnaeheEintragen(Bodenraster raster, int i0, int j0, byte[] splat2)
    {
        if (FelsnaeheRechnen(raster, i0, j0, BodenBild) is not { } naehe) return;
        for (var k = 0; k < BodenBild * BodenBild; k++) splat2[k * 4 + 3] = (byte)Math.Clamp((int)(naehe[k] * 255f + 0.5f), 0, 255);
    }

    /// <summary>
    /// Die Felsnähe (0 … 1) für <paramref name="n"/> × <paramref name="n"/> Bodenpunkte ab Punkt i0, j0 – null, wenn ringsum weder Fels noch Kies liegt.
    /// Der Boden-Shader liest sie als Kanal A von Bild 2 (<see cref="FelsnaeheEintragen"/>), der Grasteppich und die Büschel rechnen mit ihr, damit auf
    /// steinigem Boden keine vollen Halme stehen (Fels R5).
    /// </summary>
    internal static float[]? FelsnaeheRechnen(Bodenraster raster, int i0, int j0, int n)
    {
        const int r = FelsnaeheRadius, rand = FelsnaeheReichweite;
        var m = n + 2 * rand;
        var g0 = raster.Splat(i0 - rand, j0 - rand, m, m, 0); // Gruppe 0: Wiese, Erde, Fels, Kies
        var a = new float[m * m];
        var irgendwo = false;
        for (var k = 0; k < a.Length; k++)
        {
            a[k] = Math.Max(g0[k * 4 + 2], 0.8f * g0[k * 4 + 3]) / 255f;
            irgendwo |= a[k] > 0;
        }
        if (!irgendwo) return null;
        var b = new float[m * m];
        for (var durchgang = 0; durchgang < 2; durchgang++)
        {
            Kasten(a, b, m, r, 1, m);   // waagerecht
            Kasten(b, a, m, r, m, 1);   // senkrecht
        }
        var aus = new float[n * n];
        for (var j = 0; j < n; j++)
            for (var i = 0; i < n; i++)
                aus[j * n + i] = Math.Min(1f, a[(j + rand) * m + i + rand] * 2.2f);
        return aus;

        // gleitender Mittelwert über 2r + 1 Punkte entlang einer Richtung (schritt 1: Zeile, n: Spalte); am Rand wird mit 0 gerechnet
        static void Kasten(float[] von, float[] nach, int n, int r, int schritt, int quer)
        {
            var f = 1f / (2 * r + 1);
            for (var q = 0; q < n; q++)
            {
                var start = q * quer;
                var summe = 0f;
                for (var k = 0; k <= r && k < n; k++) summe += von[start + k * schritt];
                for (var k = 0; k < n; k++)
                {
                    nach[start + k * schritt] = summe * f;
                    if (k + r + 1 < n) summe += von[start + (k + r + 1) * schritt];
                    if (k - r >= 0) summe -= von[start + (k - r) * schritt];
                }
            }
        }
    }

    /// <summary>Die Schuttsteine eines Stücks als Bilder (Form „schuttbrocken“, Gestein des Stücks); leer ohne Höhenraster.</summary>
    private List<Felsbild> FelsSchutt(in Felsbild b)
    {
        var aus = new List<Felsbild>();
        if ((_entwurf?.Hoehen ?? _gelaende?.Roh) is not { } raster) return aus;
        foreach (var s in Felsboden.Schuttsteine(new Felsfuss(b.Form, b.X, b.Z, b.Drehung, b.Massstab, b.AchseX, b.AchseY, b.AchseZ), raster, BodenRaster ?? LeererBoden))
            aus.Add(new Felsbild(Felsboden.Schuttform, b.Gestein, s.X, s.Z, s.Drehung, s.Massstab, 0.6, 0.3, 0, 0));
        return aus;
    }
}
