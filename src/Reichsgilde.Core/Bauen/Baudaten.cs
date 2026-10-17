namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Daten (Abschnitt „bauen" in wirtschaft.json, ab Datenversion 34)
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Größe, Bauzeit und erlaubte Lagen eines Gebäudes, das man neu bauen kann.</summary>
public sealed class Gebaeudemass
{
    /// <summary>Breite entlang der Straße in Metern, inkl. Vorplatz (Vielfaches des Rasters).</summary>
    public double Breite { get; init; }

    /// <summary>Tiefe von der Straße nach hinten in Metern, inkl. 2 m Vorplatz (Vielfaches des Rasters).</summary>
    public double Tiefe { get; init; }

    /// <summary>So viele Spieljahre (je 1.020 Spielminuten) dauert der Neubau.</summary>
    public double BauzeitJahre { get; init; } = 1;

    /// <summary>In welchen Bauzonen das Gebäude stehen darf.</summary>
    public List<Lage> Lagen { get; init; } = new() { Lage.Kern, Lage.Mauerring, Lage.Vorstadt };

    /// <summary>
    /// Nur für Wohnhäuser: Diese Stufe entsteht nur als Neubau oder durch Kauf eines Leerstands, nicht als Ausbau am Platz
    /// (Stadtpalais und Stadthof, ab Datenversion 35 – sie sind größer als ein Wohnhausplatz). Gilt nur, wo gebaut wird.
    /// </summary>
    public bool NurNeubau { get; init; }

    internal void Pruefen(List<string> f, string wo, double raster)
    {
        if (Breite <= 0 || Tiefe <= 0) f.Add($"{wo}: breite und tiefe müssen größer als 0 sein.");
        else if (!Vielfaches(Breite, raster) || !Vielfaches(Tiefe, raster)) f.Add($"{wo}: breite und tiefe müssen Vielfache des Rasters ({raster} m) sein.");
        if (BauzeitJahre < 0) f.Add($"{wo}.bauzeitJahre darf nicht negativ sein.");
        if (Lagen.Count == 0) f.Add($"{wo}.lagen darf nicht leer sein.");
    }

    private static bool Vielfaches(double wert, double raster) => Math.Abs(wert / raster - Math.Round(wert / raster)) < 1e-9;
}

/// <summary>Preise für leerstehende Wohnhäuser – als Anteil ihres Hauswerts (Summe der Ausbaupreise bis zu ihrer Stufe).</summary>
public sealed class LeerstandDaten
{
    /// <summary>Kaufpreis: 0,5 = 50 % des Hauswerts.</summary>
    public double Kauf { get; init; } = 0.5;

    /// <summary>Sanierung nach dem Kauf: 0,2 = 20 % des Hauswerts. Sie dauert so lange wie ein Ausbau (wohnen.bauzeitMinuten).</summary>
    public double Sanierung { get; init; } = 0.2;

    /// <summary>Abriss statt Sanierung: 0,1 = 10 % des Hauswerts, mindestens <see cref="AbrissMindestens"/>.</summary>
    public double Abriss { get; init; } = 0.1;

    /// <summary>Was ein Abriss mindestens kostet – auch ein Fachwerkhaus (Hauswert 0) reißt niemand umsonst ab.</summary>
    public double AbrissMindestens { get; init; } = 100;

    /// <summary>So lange dauert der Abriss (Spielminuten).</summary>
    public int AbrissMinuten { get; init; } = 240;

    internal void Pruefen(List<string> f)
    {
        if (Kauf < 0 || Sanierung < 0 || Abriss < 0 || AbrissMindestens < 0) f.Add("bauen.leerstand: Anteile und Preise dürfen nicht negativ sein.");
        if (AbrissMinuten < 0) f.Add("bauen.leerstand.abrissMinuten darf nicht negativ sein.");
    }
}

/// <summary>
/// Bauen in der Stadt (Entscheidung des Nutzers vom 17.10.2026, LEVELDESIGN §3): Raster, Abstand zur Straße,
/// Größe und Bauzeit von Werkstatt und Wohnhäusern, Preise für Leerstand. Fehlt der Abschnitt (oder die Karte),
/// wird nicht gebaut – alles wie vorher.
/// </summary>
public sealed class BauDaten
{
    /// <summary>Rasterfeld in Metern.</summary>
    public double Raster { get; init; } = 2;

    /// <summary>
    /// So weit (Meter) darf die Mitte eines Vorplatz-Feldes höchstens von der Mitte einer passenden Straße liegen;
    /// mindestens zwei Felder des Vorplatzes müssen es schaffen (wie STRASSENNAH im Kapazitätswerkzeug).
    /// </summary>
    public double StrassenAbstand { get; init; } = 7;

    /// <summary>Eine neue Werkstatt (Stufe 1). Ausgebaut wird sie später am Platz – die Fläche reicht für alle Stufen.</summary>
    public Gebaeudemass Werkstatt { get; init; } = new() { Breite = 16, Tiefe = 14 };

    /// <summary>Je Stufe des Wohnhauses (wie wohnen.stufen) Größe, Bauzeit und erlaubte Lagen.</summary>
    public List<Gebaeudemass> Wohnhaus { get; init; } = new();

    public LeerstandDaten Leerstand { get; init; } = new();

    internal void Pruefen(List<string> f, Wohnen.WohnenDaten? wohnen)
    {
        if (Raster <= 0) f.Add("bauen.raster muss größer als 0 sein.");
        if (StrassenAbstand <= 0) f.Add("bauen.strassenAbstand muss größer als 0 sein.");
        if (Raster <= 0) return;
        Werkstatt.Pruefen(f, "bauen.werkstatt", Raster);
        for (var i = 0; i < Wohnhaus.Count; i++)
            Wohnhaus[i].Pruefen(f, $"bauen.wohnhaus[{i}]", Raster);
        if (Werkstatt.NurNeubau) f.Add("bauen.werkstatt: nurNeubau gibt es nur für Wohnhäuser.");
        if (Wohnhaus.Count > 0 && Wohnhaus[0].NurNeubau) f.Add("bauen.wohnhaus[0]: Das Startwohnhaus kann nicht nurNeubau sein.");
        // Ohne Abschnitt „wohnen“ wird nicht gebaut (siehe Bauwesen.IstAktiv) – dann gibt es nichts abzugleichen.
        if (wohnen is not null && Wohnhaus.Count != wohnen.Stufen.Count) f.Add($"bauen.wohnhaus braucht je Stufe des Wohnhauses einen Eintrag ({wohnen.Stufen.Count}, sind {Wohnhaus.Count}).");
        Leerstand.Pruefen(f);
    }
}
