using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Umbauten;

// ═════════════════════════════════════════════════════════════════════════════
//  Greybox: Bauzonen aus dem Raster (Variante A, Nutzer 4.11.2026)
//
//  Die elf von Hand gezeichneten Bauzonen (K1–K3, M1, V1–V8) weichen der Rechnung aus dem 2-m-Raster. Von Hand bleiben nur grobe Lage-Gebiete,
//  die wiedergeben, wo die alten Zonen lagen (in dieser Reihenfolge, das erste gilt):
//    • „Hof vor der Westmauer“ (Mauerring): wie die alte M1 knapp vor der Westmauer – innerhalb der Mauer ist kein Platz für einen Stadthof
//      (siehe „fiktion“).
//    • Kern: innerhalb der Ringgasse (± 38 m).
//    • Mauerring: zwischen Ringgasse und Mauer (± 50 m).
//    • Vorstadt: vier Rechtecke um die alten Zonen V1–V8 (an der Weststraße beiderseits, am Abzweig und am Weg zum Felsenhang) mit bis zu 2 m Rand – der
//      Mischwald im Westen bleibt Wald. Im Osten (Umlandbetriebe) und Süden (Hafen) bleibt es wie bisher ohne Zonen.
//  Der Probeplan bekommt die Namen der neuen Zonen (seine Plätze bleiben, wo sie sind).
//  Aufruf: dotnet run --project tools/Kartenbau -- umbauen greybox-raster   (auf die Greybox vor PR „Bauzonen aus dem Raster“; danach genügt
//  „bauzonen greybox“, wenn sich Wege oder Gebäude ändern)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class GreyboxRaster : Umbau
{
    public override string Kennung => "greybox-raster";
    public override string Karte => "greybox";
    public override string Beschreibung => "Bauzonen aus dem 2-m-Raster statt von Hand: vier grobe Lage-Gebiete, Restflächen, Probeplan auf die neuen Zonen";

    private static Kartenort P(double x, double z) => new(x, z);

    private static List<Kartenort> Rechteck(double x0, double z0, double x1, double z1) => new() { P(x0, z0), P(x1, z0), P(x1, z1), P(x0, z1) };

    /// <summary>Die Lage-Gebiete der Greybox (auch für den Test).</summary>
    public static readonly Lagegebiet[] Lagen =
    {
        new(Lage.Mauerring, Rechteck(-82, -58, -60, -24), "Hof vor der Westmauer"),
        new(Lage.Kern, Rechteck(-38, -38, 38, 38), "innerhalb der Ringgasse"),
        new(Lage.Mauerring, Rechteck(-50, -50, 50, 50), "zwischen Ringgasse und Mauer"),
        new(Lage.Vorstadt, Rechteck(-118, -14, -78, 6), "an der Weststraße, Nordseite"),
        new(Lage.Vorstadt, Rechteck(-120, 6, -78, 38), "an der Weststraße, Südseite"),
        new(Lage.Vorstadt, Rechteck(-78, -68, -64, -56), "am Abzweig zum Felsenhang"),
        new(Lage.Vorstadt, Rechteck(-58, -98, -14, -50), "am Weg zum Felsenhang"),
    };

    public override void Anwenden(Kartenentwurf e, TextWriter aus)
    {
        var alt = e.Bauzonen.Where(z => !z.Berechnet).ToList();
        foreach (var z in alt) e.FlaecheLoeschen(z);
        aus.WriteLine($"   {alt.Count} Zonen von Hand gelöscht");
        e.LagenSetzen(Lagen);
        Befehle.BauzonenRechnen(e, aus);
    }
}
