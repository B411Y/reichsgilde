using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Staedte;

// Köln: Die Bauzonen entstehen aus dem 2-m-Raster (die Rechnung steht seit 4.11.2026 im Kern: Kartenwerkstatt/Rasterbauzonen.cs). Jedes Feld,
// das frei ist (Abstand zu Wegen, Gebäuden, Mauer und Wasser wie in den Kartenregeln, kein steiler Hang), gehört zu einer Zone seiner Lage
// (Kern, Mauerring, Vorstadt, Umland); zusammenhängende Felder einer Lage werden eine Zone. Köln-eigen ist nur die Lage-Regel hier.

public sealed partial class Koeln
{
    /// <summary>Umland-Zonen: Rechtecke (x0, z0, x1, z1) neben den Landstraßen, in denen freie Felder Umland-Bauzone werden.</summary>
    private static readonly (double X0, double Z0, double X1, double Z1)[] UmlandGebiete =
    {
        (-330, -40, -200, 16), (-330, 24, -200, 70), (-300, -130, -190, -80), (230, -60, 290, -14), (150, 110, 215, 170), (-70, -190, -20, -160),
    };

    private List<Kartenort> _stadtFlaeche = null!;

    private Lage? LageVon(Kartenort p)
    {
        // Deutz: innerhalb der kleinen Mauer
        if (p.X is > 124 and < 213 && p.Z is > -47 and < 41) return Lage.Vorstadt;
        // Altstadt: innerhalb der Mauer bis zum Rheinufer
        if (p.X < 57 && Kartengeometrie.ImVieleck(p, _stadtFlaeche))
            return p.Abstand(P(0, 0)) < 64 || p.Abstand(P(0, -44)) < 54 ? Lage.Kern : Lage.Mauerring;
        // Vorstädte vor den Toren: ein Streifen längs der Landstraßen
        if (p.X < 45 && p.Abstand(P(Cx, 0)) is > R + 6 and < R + 80 && _vorstadtWege.Any(w => Kartengeometrie.AbstandLinie(p, w.Verlauf) < 34)) return Lage.Vorstadt;
        foreach (var (x0, z0, x1, z1) in UmlandGebiete)
            if (p.X >= x0 && p.X <= x1 && p.Z >= z0 && p.Z <= z1) return Lage.Umland;
        return null;
    }

    private List<EntwurfWeg> _vorstadtWege = new();

    private double AbstandZuUmriss(Kartenort p, IReadOnlyList<Kartenort> ecken) =>
        Kartengeometrie.ImVieleck(p, ecken) ? 0 : Kartengeometrie.AbstandLinie(p, ecken.Append(ecken[0]).ToList());

    /// <summary>Was <see cref="LageVon"/> braucht: die Fläche innerhalb der Mauer und die Landstraßen der Vorstädte.</summary>
    private void LagenVorbereiten()
    {
        _stadtFlaeche = _stadtmauer.Ecken.ToList();
        _vorstadtWege = new[] { "Aachener Straße", "Venloer Straße", "Neusser Straße", "Luxemburger Straße", "Bonner Straße" }.Select(n => _strassen[n]).ToList();
    }

    /// <summary>
    /// Die Bauzonen aus dem Raster mit der allgemeinen Rechnung des Kerns (Kartenentwurf.BauzonenAusRaster, seit 4.11.2026 für jede Karte): Köln
    /// gibt seine Lage-Regel (<see cref="LageVon"/>) und den bisherigen Bereich (x −340 … 250, z −196 … 246) und 3 m Abstand zu Mauer und Ufer vor, alles andere sind die
    /// Vorgaben (Feld höchstens 30 m, Zone höchstens 7,5 m von einer Karrenstraße, mindestens 24 Felder). Die Werkstatt-Bauplätze sind von Hand
    /// gezeichnet und stehen schon (WerkstattbauplaetzeAnlegen); die Rechnung lässt sie aus und stellt die Rasterzonen davor.
    /// </summary>
    private void Bauzonen()
    {
        var ergebnis = E.BauzonenAusRaster(new Rastervorgaben { LageVon = LageVon, Bereich = new Kartenrechteck(-340, -196, 250, 246), Mauerabstand = 3, Uferabstand = 3 });
        foreach (var (lage, zonen, felder) in ergebnis.JeLage) Console.WriteLine($"   Bauzonen {lage}: {zonen} Zonen, {felder} Felder");
        Console.WriteLine($"   Restflächen: {ergebnis.Restflaechen.Count} ({ergebnis.Restflaechen.Sum(r => r.Felder)} Felder)");
    }
}
