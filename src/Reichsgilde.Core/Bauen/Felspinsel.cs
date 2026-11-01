using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Der Felspinsel der Kartenwerkstatt (Felspinsel F3): Streuen, Stempeln, Radieren
//
//  • Streuen: Ein Strich setzt alle Radius/2 Meter (mindestens 1 m) einen Tupfer – nicht je Bild der Darstellung, damit derselbe Strich immer
//    dieselben Stücke ergibt. Jeder Tupfer füllt seinen Kreis, bis die Grundflächen der Stücke darin „Dichte“ (Anteil 0 … 1) des Kreises
//    decken; höchstens <see cref="Felspinsel.Versuche"/> Versuche je Tupfer. Ein neues Stück hält Abstand zu allen anderen (die Kreise seiner
//    Grundfläche dürfen sich nur zu <see cref="Felspinsel.Ueberlappung"/> überdecken). Vorlage ist eine Form, eine Auswahl von Formen (zufällig
//    gleich verteilt) oder eine feste Gruppe (Wand, Kuppe, Rippe, Felsformen.Gruppen) – dann setzt jeder Treffer die ganze Gruppe.
//  • Stempeln: ein Stück oder eine Gruppe genau an einen Punkt, ohne Abstand und Dichte.
//  • Radieren: nimmt alle Stücke weg, deren Mitte im Kreis liegt.
//  Der Filter (Pinselfilter: Steigung, Bodenart) gilt für alles: Beim Streuen wird ein Stück mit der Wahrscheinlichkeit des Filterfaktors
//  gesetzt (weicher Rand); Stempel und Radierer wirken, wo der Faktor mindestens ½ ist. Gemessen an der Mitte des Stücks (bei einer Gruppe an
//  der Mitte der Gruppe).
//  Je Stück: Größe = Größe des Pinsels ± Streuung (in einer Gruppe je Stück noch ± 8 %, Drehung ± 6°, außer
//  dem ersten, der Mitte), Drehung um die Senkrechte voll zufällig – bei Formen mit Schauseite (und Gruppen) hangabwärts
//  ± 20°, wenn „zum Hang“ an ist und der Hang mindestens 3 % steigt –, Kippen ±8°, Einsinken ± 0,05, Neigung zum Hang wie eingestellt.
//  Zufall nur aus der Zufallsquelle des Strichs (Startwert), Winkel über DetMathe; alles gerundet wie in der Datei.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Felspinsel (siehe Kopf der Datei): Vorlage (<paramref name="Formen"/> oder <paramref name="Gruppe"/>), <paramref name="Gestein"/> (null:
/// Grundgestein der Karte), Radius (Meter), Dichte (Anteil der gedeckten Fläche 0 … 1), Größe (Maßstab) mit Streuung (± Anteil), Einsinken
/// (Anteil der Höhe), Neigung zum Hang (0 … 1), „zum Hang drehen“, Filter und Radierer.
/// </summary>
public sealed record Felspinsel(string[]? Formen = null, string? Gruppe = null, string? Gestein = null, double Radius = 8, double Dichte = 0.2,
    double Groesse = 1, double Streuung = 0.25, double Einsinken = Felsformen.EinsinkenVorgabe, double Neigung = 0.5, bool ZumHang = true,
    Pinselfilter? Filter = null, bool Radierer = false)
{
    /// <summary>Kleinster und größter Radius (Meter).</summary>
    public const double MinRadius = 1, MaxRadius = 100;

    /// <summary>
    /// Versuche je Tupfer: dreimal so viele, wie Plätze (Stück oder Gruppe, kleinste Form bei kleinster Größe) in die noch freie Zieldichte passen,
    /// mindestens <see cref="VersucheMin"/>, höchstens <see cref="VersucheMax"/>; nach <see cref="FehlversucheMax"/> Fehlversuchen in Folge ist der
    /// Kreis voll. So erreicht ein großer Radius dieselbe Deckung wie ein kleiner.
    /// </summary>
    public const int VersucheMin = 24, VersucheMax = 2000, FehlversucheMax = 60;

    /// <summary>So weit dürfen sich die Kreise der Grundflächen zweier Stücke überdecken (Anteil der Summe der Radien).</summary>
    public const double Ueberlappung = 0.35;

    /// <summary>Ab dieser Steigung (Prozent) dreht „zum Hang“ ein Stück mit Schauseite hangabwärts.</summary>
    public const double HangAb = 3;

    /// <summary>Die Form eines neuen Stücks, wenn der Pinsel keine nennt.</summary>
    public const string FormVorgabe = "block";

    /// <summary>Ein Radierer mit diesem Radius (und Filter).</summary>
    public static Felspinsel Radiergummi(double radius = 8, Pinselfilter? filter = null) => new(Radius: radius, Filter: filter, Radierer: true);

    /// <summary>Abstand der Tupfer entlang des Strichs (Meter).</summary>
    public double Tupfabstand => Math.Max(1, Radius / 2);

    /// <summary>Die Formen, aus denen er zieht (nur bekannte; ohne Angabe <see cref="FormVorgabe"/>).</summary>
    public IReadOnlyList<Felsform> FormenWirksam
    {
        get
        {
            var liste = (Formen ?? Array.Empty<string>()).Select(Felsformen.Finde).OfType<Felsform>().ToList();
            return liste.Count > 0 ? liste : new[] { Felsformen.Finde(FormVorgabe)! };
        }
    }

    /// <summary>Die Grundfläche (Kreise, m²) des kleinsten Platzes, den der Pinsel setzt: eine Gruppe ganz, sonst die kleinste Form – bei kleinster Größe.</summary>
    public double KleinsterPlatz
    {
        get
        {
            var s = Math.Max(Felsformen.MassstabMin, Groesse * (1 - Streuung));
            double Flaeche(string form, double m) => Felsformen.Finde(form) is { } f ? Math.PI * f.Radius * m * f.Radius * m : 1;
            return Felsformen.Gruppe(Gruppe) is { } g
                ? g.Stuecke.Sum(x => Flaeche(x.Form, s * x.Massstab * 0.92))
                : FormenWirksam.Min(f => Flaeche(f.Kennung, s));
        }
    }

    /// <summary>Der Pinsel mit Werten in ihren Grenzen; eine unbekannte Gruppe oder ein unbekanntes Gestein fallen weg.</summary>
    public Felspinsel Begrenzt() => this with
    {
        Gruppe = Felsformen.Gruppe(Gruppe) is null ? null : Gruppe,
        Gestein = Felsformen.IstGestein(Gestein) ? Gestein : null,
        Radius = Math.Clamp(Radius, MinRadius, MaxRadius),
        Dichte = Math.Clamp(Dichte, 0, 1),
        Groesse = Math.Clamp(Groesse, Felsformen.MassstabMin, Felsformen.MassstabMax),
        Streuung = Math.Clamp(Streuung, 0, 0.75),
        Einsinken = Math.Clamp(Einsinken, 0, Felsformen.EinsinkenMax),
        Neigung = Math.Clamp(Neigung, 0, 1),
        Filter = Filter is { } f && !f.Begrenzt().IstLeer ? f.Begrenzt() : null,
    };
}

/// <summary>Ein Stück, wie es der Pinsel setzen will (gerundet wie in der Datei) – daraus wird ein Stück des Entwurfs.</summary>
public sealed record Felslage(string Form, string? Gestein, double X, double Z, double Drehung, double Massstab, double Neigung, double Einsinken,
    double KippenX, double KippenZ)
{
    /// <summary>Der Radius der Grundfläche (Meter, mit Maßstab).</summary>
    public double Radius => (Felsformen.Finde(Form)?.Radius ?? 1) * Massstab;
}

/// <summary>Die Rechnungen des Felspinsels, die nicht am Entwurf hängen (siehe Kopf der Datei).</summary>
public static class Felsstreuung
{
    /// <summary>
    /// Die Steigung des Rasters an einem Ort als Vektor (Anteil je Meter nach x und z, bilinear zwischen den Höhenpunkten wie
    /// <see cref="Pinselfilter.Steigung(Hoehenraster, double, double)"/>) – für „zum Hang drehen“ und die Darstellung der Neigung.
    /// </summary>
    public static (double X, double Z) Hang(Hoehenraster raster, double x, double z)
    {
        if (raster.IstLeer) return (0, 0);
        (double X, double Z) G(int i, int j) => Pinselfilter.Gradient((a, b) => raster.Wert(a, b), i, j);
        var (fx, fz) = (x / Hoehenraster.Abstand, z / Hoehenraster.Abstand);
        var (i, j) = ((int)Math.Floor(fx), (int)Math.Floor(fz));
        var (tx, tz) = (fx - i, fz - j);
        var (a, b, c, d) = (G(i, j), G(i + 1, j), G(i, j + 1), G(i + 1, j + 1));
        return ((a.X + (b.X - a.X) * tx) * (1 - tz) + (c.X + (d.X - c.X) * tx) * tz, (a.Z + (b.Z - a.Z) * tx) * (1 - tz) + (c.Z + (d.Z - c.Z) * tx) * tz);
    }

    /// <summary>Die Drehung (Grad), bei der die Schauseite (+Z) hangabwärts zeigt – null, wenn der Hang flacher als <see cref="Felspinsel.HangAb"/> ist.</summary>
    public static double? Hangabwaerts(Hoehenraster raster, double x, double z)
    {
        var (gx, gz) = Hang(raster, x, z);
        if (Pinselfilter.Betrag(gx, gz) < Felspinsel.HangAb) return null;
        // Kartengeometrie.Dreh(0, 1, w) = (sin w, cos w): die gedrehte +Z-Achse soll in Richtung (−gx, −gz) zeigen
        return DetMathe.Atan2(-gx, -gz) * 180 / Math.PI;
    }

    /// <summary>
    /// Wie stark der Filter an einem Ort wirkt (0 … 1): Steigung am Raster, Bodenart am nächsten Bodenpunkt (gerundet, Punkt i liegt bei x = i). Ohne Filter 1.
    /// </summary>
    public static double Filterfaktor(Pinselfilter? filter, Hoehenraster hoehen, Bodenraster boden, double x, double z)
    {
        if (filter is null) return 1;
        if (filter.Boden is { } t && !filter.BodenPasst(boden.Gewicht(Bodenraster.Punktindex(x + Bodenraster.Abstand / 2), Bodenraster.Punktindex(z + Bodenraster.Abstand / 2), t))) return 0;
        return filter.MitSteigung ? filter.SteigungFaktor(Pinselfilter.Steigung(hoehen, x, z)) : 1;
    }

    /// <summary>
    /// Die Stücke eines Treffers an (x, z): ein Stück aus den Formen des Pinsels oder die ganze Gruppe. Verbraucht die Würfe immer in derselben
    /// Reihenfolge (Form, Größe, Drehung, je Stück Drehung, Größe, Einsinken, Kippen). <paramref name="drehung"/>: feste Drehung (Stempel mit
    /// Q/E) statt der zufälligen bzw. hangabwärts.
    /// </summary>
    public static List<Felslage> Treffer(Felspinsel pinsel, Zufallsquelle zufall, Hoehenraster hoehen, double x, double z, double? drehung = null)
    {
        double Plusminus(double weite) => (2 * zufall.NextDouble() - 1) * weite;
        var p = pinsel;
        var gruppe = Felsformen.Gruppe(p.Gruppe);
        var formen = p.FormenWirksam;
        var form = formen[zufall.Next(formen.Count)];
        var groesse = Math.Clamp(p.Groesse * (1 + Plusminus(p.Streuung)), Felsformen.MassstabMin, Felsformen.MassstabMax);
        var wurf = zufall.NextDouble();
        var gerichtet = gruppe is not null || form.Gerichtet;
        var hang = p.ZumHang && gerichtet ? Hangabwaerts(hoehen, x, z) : null;
        var grund = drehung ?? (hang is { } h ? h + (2 * wurf - 1) * 20 : wurf * 360);

        var stuecke = gruppe?.Stuecke ?? new[] { new Gruppenstueck(form.Kennung, 0, 0, 0, 1) };
        var aus = new List<Felslage>(stuecke.Count);
        for (var n = 0; n < stuecke.Count; n++)
        {
            var s = stuecke[n];
            var o = Kartengeometrie.Dreh(s.X * groesse, s.Z * groesse, grund);
            var eigen = n == 0 ? 0 : Plusminus(6); // das erste Stück (die Mitte der Gruppe) steht genau in der Drehung der Gruppe
            var massstab = n == 0 ? groesse * s.Massstab : groesse * s.Massstab * (1 + Plusminus(0.08));
            var einsinken = p.Einsinken + s.Einsinken + Plusminus(0.05);
            var (kx, kz) = (Plusminus(Felsformen.KippenMax), Plusminus(Felsformen.KippenMax));
            aus.Add(new Felslage(s.Form, p.Gestein, Kartenraster.Punkt(x + o.X), Kartenraster.Punkt(z + o.Z), Kartenraster.Drehung(grund + s.Drehung + eigen),
                Math.Round(Math.Clamp(massstab, Felsformen.MassstabMin, Felsformen.MassstabMax), 2), Math.Round(p.Neigung, 2),
                Math.Round(Math.Clamp(einsinken, 0, Felsformen.EinsinkenMax), 2), Math.Round(kx, 1) + 0.0, Math.Round(kz, 1) + 0.0));
        }
        return aus;
    }
}
