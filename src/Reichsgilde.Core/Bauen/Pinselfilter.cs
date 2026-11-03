namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Der Filter der Pinsel (Kartenwerkstatt, Pinsel-Paket 1): wo ein Gelände- oder Texturpinsel wirkt
//
//  Ein Pinsel wirkt sonst überall in seinem Kreis. Mit dem Filter nur dort, wo
//    • die Steigung im Bereich „ab … bis …“ Prozent liegt (100 % = 45°): Fels nur an Böschungen, Wiese nur in der Ebene. Innen an jeder Grenze
//      läuft die Wirkung über 2 Prozentpunkte weich aus – so entsteht keine Treppenkante im 1-m-Raster, und eine Grenze von 8 % (Hinweis der
//      Hangsperre) wird nie überschritten;
//    • eine bestimmte Bodentextur liegt: Mit Rand 0 % muss sie mindestens die Hälfte des Bodenpunkts decken, mit 100 % genügt ein Hauch davon.
//  Gemessen wird am Stand vor dem Strich (der Strich ändert nicht, wo er wirken darf) und am gespeicherten Raster ohne das Bett der Wege – wie die
//  Hangsperre. Die Steigung eines Höhenpunkts kommt aus seinen vier Nachbarn (Mittendifferenz über 4 m), zwischen den Höhenpunkten bilinear.
//  Die Idee folgt dem Steigungsfilter von Terrain3D (MIT, src/terrain_3d_data.cpp:829-851 „is_in_slope“, Commit cdcd382c); gerechnet wird hier eigenständig
//  in Prozent statt Grad, ohne Winkelfunktionen – nur + − × ÷ und Wurzel, auf jedem Rechner gleich.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Der Filter eines Pinsels (siehe Kopf der Datei): Steigung ab/bis (Prozent; <c>null</c> bei „bis“ = ohne Grenze) und eine Bodentextur mit
/// Rand (0 … 1). Ohne Angaben wirkt der Pinsel überall.
/// </summary>
public sealed record Pinselfilter(double SteigungAb = 0, double? SteigungBis = null, Bodentextur? Boden = null, double BodenRand = 0)
{
    /// <summary>Über so viele Prozentpunkte läuft die Wirkung innen an einer Grenze der Steigung weich aus.</summary>
    public const double Uebergang = 2;

    /// <summary>Die größte Steigung, die sich einstellen lässt (Prozent; 1000 % ≈ 84°).</summary>
    public const double HoechsteSteigung = 1000;

    /// <summary>Ein Filter, der überall wirkt.</summary>
    public static readonly Pinselfilter Keiner = new();

    /// <summary>Filtert er nach der Steigung?</summary>
    public bool MitSteigung => SteigungAb > 0 || SteigungBis is not null;

    /// <summary>Wirkt er überall (keine Grenze)?</summary>
    public bool IstLeer => !MitSteigung && Boden is null;

    /// <summary>Der Filter mit Werten in ihren Grenzen (bis nie unter ab).</summary>
    public Pinselfilter Begrenzt()
    {
        var ab = Math.Clamp(SteigungAb, 0, HoechsteSteigung);
        return this with
        {
            SteigungAb = ab,
            SteigungBis = SteigungBis is { } bis ? Math.Clamp(bis, ab, HoechsteSteigung) : null,
            BodenRand = Math.Clamp(BodenRand, 0, 1),
        };
    }

    /// <summary>Wie stark der Pinsel bei dieser Steigung (Prozent) wirkt: 1 im Bereich, 0 außerhalb, innen an den Grenzen weich.</summary>
    public double SteigungFaktor(double prozent)
    {
        var f = 1.0;
        if (SteigungAb > 0) f = Math.Min(f, Gelaendeform.Weich((prozent - SteigungAb) / Uebergang));
        if (SteigungBis is { } bis) f = Math.Min(f, Gelaendeform.Weich((bis - prozent) / Uebergang));
        return f;
    }

    /// <summary>So viel (0 … 255) muss die Textur des Bodenfilters an einem Bodenpunkt mindestens decken: 128 bei Rand 0, 1 bei Rand 1.</summary>
    public int BodenSchwelle => Math.Max(1, (int)Math.Round(128 * (1 - Math.Clamp(BodenRand, 0, 1)), MidpointRounding.AwayFromZero));

    /// <summary>Passt ein Bodenpunkt mit diesem Anteil der Filtertextur (0 … 255)? Ohne Bodenfilter immer.</summary>
    public bool BodenPasst(int anteil) => Boden is null || anteil >= BodenSchwelle;

    // ── Steigung messen ──────────────────────────────────────────────────────

    /// <summary>
    /// Die Steigung des gespeicherten Rasters an einem Ort in Prozent (100 % = 1 m auf 1 m): an jedem Höhenpunkt aus den vier Nachbarn, dazwischen
    /// bilinear – dieselbe Zahl, mit der der Filter rechnet (für die Anzeige unter der Maus).
    /// </summary>
    public static double Steigung(Hoehenraster raster, double x, double z) =>
        raster.IstLeer ? 0 : Steigung((i, j) => Gradient((a, b) => raster.Wert(a, b), i, j), x, z);

    /// <summary>Die Steigung in Prozent aus der Steigung je Höhenpunkt (<paramref name="gradient"/>), bilinear zwischen den vier Punkten der Zelle.</summary>
    internal static double Steigung(Func<int, int, (double X, double Z)> gradient, double x, double z)
    {
        var (fx, fz) = (x / Hoehenraster.Abstand, z / Hoehenraster.Abstand);
        var (i, j) = ((int)Math.Floor(fx), (int)Math.Floor(fz));
        var (tx, tz) = (fx - i, fz - j);
        var a = gradient(i, j);
        if (tx == 0 && tz == 0) return Betrag(a.X, a.Z);
        var (b, c, d) = (gradient(i + 1, j), gradient(i, j + 1), gradient(i + 1, j + 1));
        var gx = (a.X + (b.X - a.X) * tx) * (1 - tz) + (c.X + (d.X - c.X) * tx) * tz;
        var gz = (a.Z + (b.Z - a.Z) * tx) * (1 - tz) + (c.Z + (d.Z - c.Z) * tx) * tz;
        return Betrag(gx, gz);
    }

    /// <summary>Die Steigung (Anteil je Meter nach x und z) an einem Höhenpunkt aus seinen vier Nachbarn (Höhen in cm).</summary>
    internal static (double X, double Z) Gradient(Func<int, int, double> hoeheCm, int i, int j) =>
        ((hoeheCm(i + 1, j) - hoeheCm(i - 1, j)) / (200 * Hoehenraster.Abstand), (hoeheCm(i, j + 1) - hoeheCm(i, j - 1)) / (200 * Hoehenraster.Abstand));

    internal static double Betrag(double x, double z) => Math.Sqrt(x * x + z * z) * 100;
}

/// <summary>
/// Misst für einen Strich, wie stark der Filter nach der Steigung an einem Punkt wirkt – am Stand vor dem Strich, mit einem Zwischenspeicher je
/// Höhenpunkt (ein großer Pinsel trifft dieselben Punkte viele Male).
/// </summary>
internal sealed class Filtermessung
{
    private readonly Pinselfilter _filter;
    private readonly Func<int, int, double> _hoeheVorher;
    private readonly Dictionary<(int, int), (double X, double Z)> _gradient = new();

    /// <param name="filter">Der Filter (begrenzt).</param>
    /// <param name="hoeheVorher">Die Höhe eines Rasterpunkts in cm vor dem Strich.</param>
    internal Filtermessung(Pinselfilter filter, Func<int, int, double> hoeheVorher) => (_filter, _hoeheVorher) = (filter, hoeheVorher);

    internal Pinselfilter Filter => _filter;

    /// <summary>Der Faktor der Steigung an einem Ort in Metern (1, wenn der Filter keine Steigung kennt) – für die 1-m-Punkte des Bodens.</summary>
    internal double SteigungFaktor(double x, double z) => _filter.MitSteigung ? _filter.SteigungFaktor(Pinselfilter.Steigung(Gradient, x, z)) : 1;

    /// <summary>Der Faktor der Steigung an einem Höhenpunkt (1, wenn der Filter keine Steigung kennt) – für die Geländepinsel.</summary>
    internal double SteigungFaktorAmPunkt(int i, int j)
    {
        if (!_filter.MitSteigung) return 1;
        var g = Gradient(i, j);
        return _filter.SteigungFaktor(Pinselfilter.Betrag(g.X, g.Z));
    }

    private (double X, double Z) Gradient(int i, int j)
    {
        if (!_gradient.TryGetValue((i, j), out var g)) _gradient[(i, j)] = g = Pinselfilter.Gradient(_hoeheVorher, i, j);
        return g;
    }
}
