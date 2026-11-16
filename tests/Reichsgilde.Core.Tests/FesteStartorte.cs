using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;

/// <summary>
/// Feste Startorte für Tests, die wissen müssen, wo ein Haus steht (Fahrzeit, Karrenweg, Marktwahl, Brücke): je Karte der Spieler und die
/// KI-Häuser in ihrer Reihenfolge, Wohnhaus und Werkstatt – die Flächen, auf denen bis zum 16.11.2026 die Wohnhaus- und Werkstattplätze der
/// Karten lagen (heute Bauzone). Eine Partie nimmt sie mit <see cref="NeuePartie.Startorte"/> statt des Loses (<see cref="Startortlos"/>).
/// </summary>
static class FesteStartorte
{
    static readonly Dictionary<string, Startort[]> Orte = new()
    {
        ["greybox"] = new Startort[]
        {
            new(new(0, -44.3, 17, 8, 0), new(-23, 16, 16, 14, 0)),      // am Nordring, Weinmarkt
            new(new(0, 44.3, 17, 8, 2), new(22, 13, 16, 14, 0)),        // am Südring, an der Ostgasse (Süd)
            new(new(-44.3, -9, 17, 8, 1), new(24, -13, 16, 14, 0)),     // am Westring, an der Ostgasse (Nord)
            new(new(44.3, -16.25, 17, 8, 3), new(24, -29, 16, 14, 0)),  // am Ostring, an der Ringgasse
        },
        ["probestadt"] = new Startort[]
        {
            new(new(0, -35, 8, 10, 2), new(-30, -10, 16, 14, 0)),
            new(new(-10, -35, 8, 10, 2), new(32, -10, 16, 14, 0)),
            new(new(10, -35, 8, 10, 2), new(32, 10, 16, 14, 2)),
        },
        ["messstadt"] = new Startort[]
        {
            new(new(-20, -40, 8, 10, 2), new(-24, -10, 16, 14, 0)),
            new(new(-10, -40, 8, 10, 2), new(24, -10, 16, 14, 0)),
            new(new(0, -40, 8, 10, 2), new(-42, -10, 16, 14, 0)),
            new(new(10, -40, 8, 10, 2), new(42, -10, 16, 14, 0)),
            new(new(20, -40, 8, 10, 2), new(-60, -10, 16, 14, 0)),
            new(new(15, 40, 8, 10, 0), new(60, -10, 16, 14, 0)),
            new(new(5, 40, 8, 10, 0), new(-24, 10, 16, 14, 2)),
            new(new(-5, 40, 8, 10, 0), new(24, 10, 16, 14, 2)),
        },
        ["koeln"] = new Startort[]
        {
            new(new(28.5, 66.9, 8, 10, 1), new(26.5, 79, 16, 14, 1)),       // S1 Heumarkt
            new(new(45.5, 69.4, 8, 10, 3), new(47.5, 81.5, 16, 14, 3)),     // S2 an der Rheingasse (Heumarkt)
            new(new(-3.6, 118.5, 8, 10, 2), new(8.5, 120.5, 16, 14, 2)),    // S3 am Malzbüchel
            new(new(3.6, -68, 8, 10, 0), new(8.5, -84.5, 16, 14, 2)),       // S4 Unter Fettenhennen
            new(new(45.5, -69.4, 8, 10, 3), new(47.5, -81.5, 16, 14, 3)),   // S5 an der Rheingasse (Alter Markt)
            new(new(163, -35, 8, 10, 1), new(160, -23, 16, 14, 1)),         // S6 an der Deutzer Kirchstraße (West; der Platz lag um 2,2° schräg)
            new(new(180, -34, 8, 10, 3), new(182, -22, 16, 14, 3)),         // S7 an der Deutzer Kirchstraße (Ost)
            new(new(129, -1, 8, 10, 0), new(141, -3, 16, 14, 0)),           // S8 an der Deutzer Freiheit
        },
    };

    /// <summary>
    /// Acht Stellen für Wohnhäuser am Ring der Greybox (17 × 8 m, wo bis zum 16.11.2026 die Wohnhausplätze „Wohnhaus1“ bis „Wohnhaus8“ lagen) –
    /// für Tests, die einem Konto ohne Partie ein Wohnhaus geben (<c>Bauwesen.StartwohnhausSetzen</c>).
    /// </summary>
    public static readonly IReadOnlyList<Grundriss> GreyboxWohnhaeuser = new Grundriss[]
    {
        new(0, -44.3, 17, 8, 0), new(0, 44.3, 17, 8, 2), new(-44.3, -9, 17, 8, 1), new(44.3, -16.25, 17, 8, 3),
        new(44.3, 16.25, 17, 8, 3), new(-17, -44.3, 17, 8, 0), new(-17, 44.3, 17, 8, 2), new(17, -44.3, 17, 8, 0),
    };

    /// <summary>Die ersten <paramref name="haeuser"/> Startorte der Karte (der Spieler und seine KI-Häuser).</summary>
    public static IReadOnlyList<Startort> Fuer(string karte, int haeuser) => Orte[karte].Take(haeuser).ToList();

    /// <summary>Eine neue Partie mit festen Startorten: der Spieler und <paramref name="rivalen"/> KI-Häuser.</summary>
    public static NeuePartie Partie(string karte, NeuePartie neu) => neu with { Startorte = Fuer(karte, 1 + neu.Rivalen) };
}
