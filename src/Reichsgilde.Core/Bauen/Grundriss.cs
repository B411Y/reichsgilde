namespace Reichsgilde.Core.Bauen;

/// <summary>
/// Die Fläche, die ein Gebäude auf dem Boden belegt – ein Rechteck inkl. 2 m Vorplatz zur Straße (LEVELDESIGN „Gebäudegrößen“).
///
///   • <see cref="X"/>, <see cref="Z"/>: Mitte der Fläche in Metern (x = Osten, z = Süden).
///   • <see cref="Breite"/> liegt entlang der Straße, <see cref="Tiefe"/> von der Straße nach hinten.
///   • <see cref="Drehung"/> in 90°-Schritten: 0 = Eingang nach Süden (+z), 1 = Osten (+x), 2 = Norden (−z), 3 = Westen (−x).
///     In Godot ist das <c>Rotation.Y = Drehung · 90°</c>, wenn die Vorderseite des Modells lokal nach +Z zeigt.
/// </summary>
public readonly record struct Grundriss(double X, double Z, double Breite, double Tiefe, int Drehung)
{
    private const double Genauigkeit = 1e-6;

    /// <summary>Liegt die Breite entlang der z-Achse (Eingang nach Osten oder Westen)?</summary>
    public bool Quer => Drehung % 2 == 1;

    /// <summary>Halbe Ausdehnung in x-Richtung.</summary>
    public double HalbX => (Quer ? Tiefe : Breite) / 2;

    /// <summary>Halbe Ausdehnung in z-Richtung.</summary>
    public double HalbZ => (Quer ? Breite : Tiefe) / 2;

    /// <summary>Richtung zur Straße (Einheitsvektor).</summary>
    public (double X, double Z) Vorne => Drehung switch { 0 => (0, 1), 1 => (1, 0), 2 => (0, -1), _ => (-1, 0) };

    /// <summary>Mitte der Vorderkante – dort ist der Eingang (vor ihm liegt der Vorplatz).</summary>
    public (double X, double Z) Eingang => (X + Vorne.X * Tiefe / 2, Z + Vorne.Z * Tiefe / 2);

    /// <summary>Überdecken sich zwei Flächen? Aneinanderstoßen ist erlaubt.</summary>
    public bool Ueberlappt(Grundriss o) =>
        Math.Abs(X - o.X) < HalbX + o.HalbX - Genauigkeit && Math.Abs(Z - o.Z) < HalbZ + o.HalbZ - Genauigkeit;

    /// <summary>Liegt der Punkt auf der Fläche (Rand eingeschlossen)?</summary>
    public bool Enthaelt(double x, double z) => Math.Abs(x - X) <= HalbX + Genauigkeit && Math.Abs(z - Z) <= HalbZ + Genauigkeit;

    /// <summary>Liegen die Ecken auf dem Raster (Vielfache der Rastergröße)?</summary>
    public bool AufDemRaster(double raster) => Vielfaches(X - HalbX, raster) && Vielfaches(Z - HalbZ, raster);

    private static bool Vielfaches(double wert, double raster) => Math.Abs(wert / raster - Math.Round(wert / raster)) < Genauigkeit;

    /// <summary>
    /// Die Mittelpunkte der Rasterfelder unter der Fläche (in der Breite <c>Breite/raster</c>, in der Tiefe <c>Tiefe/raster</c> Felder).
    /// <paramref name="nurVorplatz"/>: nur die vorderste Reihe – der Vorplatz an der Straße.
    /// </summary>
    public IEnumerable<(double X, double Z)> Felder(double raster, bool nurVorplatz = false)
    {
        var quer = Math.Max(1, (int)Math.Round(Breite / raster));
        var tief = Math.Max(1, (int)Math.Round(Tiefe / raster));
        for (var j = nurVorplatz ? tief - 1 : 0; j < tief; j++)
            for (var i = 0; i < quer; i++)
            {
                // lokal: x quer zur Straße, z zur Straße hin (vorne = +z)
                var lx = -Breite / 2 + (i + 0.5) * Breite / quer;
                var lz = -Tiefe / 2 + (j + 0.5) * Tiefe / tief;
                var (wx, wz) = Drehung switch { 0 => (lx, lz), 1 => (lz, -lx), 2 => (-lx, -lz), _ => (-lz, lx) };
                yield return (X + wx, Z + wz);
            }
    }

    /// <summary>Die Drehung, deren Vorderseite am ehesten in diese Richtung zeigt (0 = +z, 1 = +x, 2 = −z, 3 = −x).</summary>
    public static int DrehungZu(double dx, double dz) =>
        Math.Abs(dx) > Math.Abs(dz) ? (dx > 0 ? 1 : 3) : (dz >= 0 ? 0 : 2);
}
