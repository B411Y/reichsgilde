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
/// Fels-Material des Stücks, ohne Schatten und nur nah (Stufe 0 und 1); sie werden mit der Zelle ihres Stücks gebaut.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
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

    /// <summary>Die Schuttsteine eines Stücks als Bilder (Form „schuttbrocken“, Gestein des Stücks); leer ohne Höhenraster.</summary>
    private List<Felsbild> FelsSchutt(in Felsbild b)
    {
        var aus = new List<Felsbild>();
        if ((_entwurf?.Hoehen ?? _gelaende?.Roh) is not { } raster) return aus;
        foreach (var s in Felsboden.Schuttsteine(new Felsfuss(b.Form, b.X, b.Z, b.Drehung, b.Massstab), raster))
            aus.Add(new Felsbild(Felsboden.Schuttform, b.Gestein, s.X, s.Z, s.Drehung, s.Massstab, 0.6, 0.3, 0, 0));
        return aus;
    }
}
