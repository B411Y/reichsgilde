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
//
//  Zufallsbereiche (R2, wie die Foliage-Typen in Unreal): Größe min–max (= Größe ± Streuung), je Achse ein eigener Faktor (Breite, Höhe,
//  Tiefe), Drehung um die Senkrechte, Kippen, Rollen und Höhenversatz als Bereich, Ausrichten am Hang mit einer Grenze in Grad. Fehlt ein
//  Bereich (null), gilt das Verhalten von vorher. Drehung, Kippen und Rollen nutzen die Würfe, die es schon gab; neue Würfe (Achsen,
//  Versatz) gibt es nur für einen Bereich, der nicht leer ist (Bis > Von), und sie kommen je Stück hinten an. So ergibt ein alter Strich mit
//  gleichem Startwert dieselben Stücke. „Neu würfeln“ (Kartenentwurf.FelsNeuWuerfeln) zieht dieselben Würfe für schon gesetzte Stücke.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Zufallsbereich von <paramref name="Von"/> bis <paramref name="Bis"/> (gleich verteilt). Leer, wenn Bis nicht größer als Von ist: Dann gilt
/// Von fest, ohne Wurf.
/// </summary>
public sealed record Felsbereich(double Von, double Bis)
{
    /// <summary>Braucht der Bereich keinen Wurf (Bis ≤ Von)?</summary>
    public bool IstLeer => !(Bis > Von);

    /// <summary>Der Wert zum Wurf <paramref name="u"/> (0 … 1).</summary>
    public double Wert(double u) => IstLeer ? Von : Von + (Bis - Von) * u;

    /// <summary>Der Wert mit einem neuen Wurf aus der Quelle – nur, wenn der Bereich nicht leer ist.</summary>
    internal double Ziehen(Zufallsquelle zufall) => IstLeer ? Von : Wert(zufall.NextDouble());

    /// <summary>Der Bereich in Grenzen (Von und Bis je zwischen <paramref name="min"/> und <paramref name="max"/>; nicht getauscht: Bis ≤ Von bleibt „Von fest“).</summary>
    internal Felsbereich Begrenzt(double min, double max) => new(Math.Clamp(Von, min, max), Math.Clamp(Bis, min, max));
}

/// <summary>
/// Ein Felspinsel (siehe Kopf der Datei): Vorlage (<paramref name="Formen"/> oder <paramref name="Gruppe"/>), <paramref name="Gestein"/> (null:
/// Grundgestein der Karte), Radius (Meter), Dichte (Anteil der gedeckten Fläche 0 … 1), Größe (Maßstab) mit Streuung (± Anteil), Einsinken
/// (Anteil der Höhe), Neigung zum Hang (0 … 1), „zum Hang drehen“, Filter, Radierer und „Boden mitmalen“ (F6: Fels, Schuttkegel und Moossaum,
/// Felsboden.cs; nicht beim Radierer).
/// Zufallsbereiche (R2; null: wie vorher): <paramref name="AchseX"/>, <paramref name="AchseY"/>, <paramref name="AchseZ"/> – Faktor je Achse
/// (Breite, Höhe, Tiefe; 0,25 … 4); <paramref name="Drehbereich"/> – Drehung um die Senkrechte in Grad (bei „zum Hang“ am Hang um hangabwärts
/// herum, sonst ab Norden; vorher ±20° bzw. 0 … 360°); <paramref name="Kippbereich"/>, <paramref name="Rollbereich"/> – Grad (−180 … 180; vorher
/// ±8°); <paramref name="Versatzbereich"/> – Höhenversatz in Metern (±20); <paramref name="NeigungBis"/> – am Hang höchstens so viele Grad aus
/// der Senkrechten neigen (wie „Align Max Angle“ in Unreal). <paramref name="HangFormen"/>: das Gelände mitformen (folgt in R5, wirkt noch nicht).
/// </summary>
public sealed record Felspinsel(string[]? Formen = null, string? Gruppe = null, string? Gestein = null, double Radius = 8, double Dichte = 0.2,
    double Groesse = 1, double Streuung = 0.25, double Einsinken = Felsformen.EinsinkenVorgabe, double Neigung = 0.5, bool ZumHang = true,
    Pinselfilter? Filter = null, bool Radierer = false, bool Boden = true,
    Felsbereich? AchseX = null, Felsbereich? AchseY = null, Felsbereich? AchseZ = null, Felsbereich? Drehbereich = null,
    Felsbereich? Kippbereich = null, Felsbereich? Rollbereich = null, Felsbereich? Versatzbereich = null, double? NeigungBis = null,
    bool HangFormen = false)
{
    /// <summary>Größte Streuung der Größe (± Anteil): so reicht min–max bis zum 19-Fachen.</summary>
    public const double StreuungMax = 0.9;

    /// <summary>Kleinste Größe eines Stücks aus diesem Pinsel (vor der Grenze des Maßstabs).</summary>
    public double GroesseMin => Groesse * (1 - Streuung);

    /// <summary>Größte Größe eines Stücks aus diesem Pinsel (vor der Grenze des Maßstabs).</summary>
    public double GroesseMax => Groesse * (1 + Streuung);

    /// <summary>Der Pinsel mit Größe min–max statt Größe ± Streuung (dasselbe, anders geschrieben).</summary>
    public Felspinsel MitGroesse(double min, double max)
    {
        var (a, b) = (Math.Max(Felsformen.MassstabMin, Math.Min(min, max)), Math.Max(Felsformen.MassstabMin, Math.Max(min, max)));
        return this with { Groesse = (a + b) / 2, Streuung = (b - a) / (a + b) };
    }

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
            // mit Achsen-Bereichen die kleinste Grundfläche (R2; ohne Bereich ändert sich die Zahl nicht: alte Striche bleiben gleich)
            if (AchseX is not null || AchseZ is not null) s *= Math.Sqrt(Math.Clamp(AchseX?.Von ?? 1, Felsformen.AchseMin, Felsformen.AchseMax)
                                                                       * Math.Clamp(AchseZ?.Von ?? 1, Felsformen.AchseMin, Felsformen.AchseMax));
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
        Streuung = Math.Clamp(Streuung, 0, StreuungMax),
        Einsinken = Math.Clamp(Einsinken, 0, Felsformen.EinsinkenMax),
        Neigung = Math.Clamp(Neigung, 0, 1),
        Filter = Filter is { } f && !f.Begrenzt().IstLeer ? f.Begrenzt() : null,
        AchseX = AchseX?.Begrenzt(Felsformen.AchseMin, Felsformen.AchseMax),
        AchseY = AchseY?.Begrenzt(Felsformen.AchseMin, Felsformen.AchseMax),
        AchseZ = AchseZ?.Begrenzt(Felsformen.AchseMin, Felsformen.AchseMax),
        Drehbereich = Drehbereich?.Begrenzt(-360, 360),
        Kippbereich = Kippbereich?.Begrenzt(-Felsformen.KippenGrenze, Felsformen.KippenGrenze),
        Rollbereich = Rollbereich?.Begrenzt(-Felsformen.KippenGrenze, Felsformen.KippenGrenze),
        Versatzbereich = Versatzbereich?.Begrenzt(-Felsformen.VersatzMax, Felsformen.VersatzMax),
        NeigungBis = NeigungBis is { } nb ? Math.Clamp(nb, 0, 90) : null,
    };
}

/// <summary>
/// Ein Stück, wie es der Pinsel setzen will (gerundet wie in der Datei) – daraus wird ein Stück des Entwurfs. Achsen und Versatz seit R2
/// (Vorgabe 1 bzw. 0, wie ohne die Felder in der Datei).
/// </summary>
public sealed record Felslage(string Form, string? Gestein, double X, double Z, double Drehung, double Massstab, double Neigung, double Einsinken,
    double KippenX, double KippenZ, double AchseX = 1, double AchseY = 1, double AchseZ = 1, double Versatz = 0)
{
    /// <summary>Der Radius der Grundfläche (Meter, mit Maßstab und den Achsen der Grundfläche).</summary>
    public double Radius => (Felsformen.Finde(Form)?.Radius ?? 1) * Massstab * Math.Sqrt(AchseX * AchseZ);
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
    public static double Filterfaktor(Pinselfilter? filter, Hoehenraster hoehen, Bodenraster boden, double x, double z) =>
        Filterfaktor(filter, hoehen, boden.Gewicht, x, z);

    /// <summary>
    /// Wie oben, der Boden aber aus <paramref name="gewicht"/> (Punkt i, j, Textur → 0 … 255) – der Felsstrich mit „Boden“ liest so den Boden vor
    /// dem Strich, sonst sähe der Filter, was der Strich selbst gerade malt (F6, wie beim Texturpinsel).
    /// </summary>
    internal static double Filterfaktor(Pinselfilter? filter, Hoehenraster hoehen, Func<int, int, Bodentextur, byte> gewicht, double x, double z)
    {
        if (filter is null) return 1;
        if (filter.Boden is { } t && !filter.BodenPasst(gewicht(Bodenraster.Punktindex(x + Bodenraster.Abstand / 2), Bodenraster.Punktindex(z + Bodenraster.Abstand / 2), t))) return 0;
        return filter.MitSteigung ? filter.SteigungFaktor(Pinselfilter.Steigung(hoehen, x, z)) : 1;
    }

    /// <summary>
    /// Die Stücke eines Treffers an (x, z): ein Stück aus den Formen des Pinsels oder die ganze Gruppe. Verbraucht die Würfe immer in derselben
    /// Reihenfolge (Form, Größe, Drehung, je Stück Drehung, Größe, Einsinken, Kippen, Rollen – dann nur bei nicht leeren Bereichen Achse x, y, z
    /// und Versatz). <paramref name="drehung"/>: feste Drehung (Stempel mit Q/E) statt der zufälligen bzw. hangabwärts.
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
        var grund = drehung ?? (p.Drehbereich is { } db ? (hang ?? 0) + db.Wert(wurf) : hang is { } h ? h + (2 * wurf - 1) * 20 : wurf * 360);
        var neigung = Math.Round(NeigungBegrenzt(p, hoehen, x, z), 2);

        var stuecke = gruppe?.Stuecke ?? new[] { new Gruppenstueck(form.Kennung, 0, 0, 0, 1) };
        var aus = new List<Felslage>(stuecke.Count);
        for (var n = 0; n < stuecke.Count; n++)
        {
            var s = stuecke[n];
            var o = Kartengeometrie.Dreh(s.X * groesse, s.Z * groesse, grund);
            var eigen = n == 0 ? 0 : Plusminus(6); // das erste Stück (die Mitte der Gruppe) steht genau in der Drehung der Gruppe
            var massstab = n == 0 ? groesse * s.Massstab : groesse * s.Massstab * (1 + Plusminus(0.08));
            var einsinken = p.Einsinken + s.Einsinken + Plusminus(0.05);
            var (uk, ur) = (zufall.NextDouble(), zufall.NextDouble());
            var kx = p.Kippbereich is { } kb ? Felsraum.Winkel(kb.Wert(uk)) : Math.Round((2 * uk - 1) * Felsformen.KippenMax, 1) + 0.0;
            var kz = p.Rollbereich is { } rb ? Felsraum.Winkel(rb.Wert(ur)) : Math.Round((2 * ur - 1) * Felsformen.KippenMax, 1) + 0.0;
            // neue Würfe (R2) nur bei nicht leeren Bereichen, hinten an: ein alter Strich zieht dieselben Zahlen wie vorher
            var ax = Achse(p.AchseX, zufall);
            var ay = Achse(p.AchseY, zufall);
            var az = Achse(p.AchseZ, zufall);
            var versatz = p.Versatzbereich is { } vb ? Kartenraster.Punkt(Math.Clamp(vb.Ziehen(zufall), -Felsformen.VersatzMax, Felsformen.VersatzMax)) : 0;
            aus.Add(new Felslage(s.Form, p.Gestein, Kartenraster.Punkt(x + o.X), Kartenraster.Punkt(z + o.Z), Kartenraster.Drehung(grund + s.Drehung + eigen),
                Math.Round(Math.Clamp(massstab, Felsformen.MassstabMin, Felsformen.MassstabMax), 2), neigung,
                Math.Round(Math.Clamp(einsinken, 0, Felsformen.EinsinkenMax), 2), kx, kz, ax, ay, az, versatz));
        }
        return aus;
    }

    /// <summary>Ein Faktor je Achse aus seinem Bereich (gerundet wie in der Datei); ohne Bereich 1, ohne Wurf.</summary>
    private static double Achse(Felsbereich? bereich, Zufallsquelle zufall) =>
        bereich is null ? 1 : Math.Round(Math.Clamp(bereich.Ziehen(zufall), Felsformen.AchseMin, Felsformen.AchseMax), 2);

    /// <summary>
    /// Die Neigung zum Hang (Anteil 0 … 1) mit der Grenze <see cref="Felspinsel.NeigungBis"/>: Ist der Hang steiler, neigt sich das Stück nur so
    /// weit, dass es höchstens so viele Grad aus der Senkrechten steht. Ohne Grenze der Anteil des Pinsels.
    /// </summary>
    internal static double NeigungBegrenzt(Felspinsel p, Hoehenraster hoehen, double x, double z)
    {
        if (p.NeigungBis is not { } bis || p.Neigung <= 0) return p.Neigung;
        var (gx, gz) = Hang(hoehen, x, z);
        var hangGrad = DetMathe.Atan2(Math.Sqrt(gx * gx + gz * gz), 1) * 180 / Math.PI;
        return hangGrad * p.Neigung <= bis ? p.Neigung : bis / hangGrad;
    }
}
