using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Staedte;

// Köln: Werkstatt-Bauplätze nahe den Märkten (Befund H2 der Prüfung von phase5/gehtempo, Entscheidung des Nutzers vom 1.11.2026: Weg A,
// „Kern = Bild“ bleibt). Seit der Kern die Fahrzeit nach der Weglänge rechnet, entscheidet der Karrenweg über eine neue Werkstatt: Von 602
// Bauorten lag nur einer unter 60 Minuten Handkarrenfahrt (Median 179). Hier bekommen die Märkte eigene Bauplätze, jeder unter 45 Minuten zum
// nächsten Platz (etwa 35 m Karrenweg; gerechnet mit Stadtwirtschaft.FahrzeitAmBauort, Tabelle in docs/LEVELDESIGN.md, Abschnitt Köln).
//
// Ein Bauplatz ist eine eigene kleine Bauzone genau in der Größe einer Werkstatt (16 × 14 m auf dem 2-m-Raster). Häuser, Einwohnerplätze und die
// Zonen aus dem Raster halten sich davon frei (Frei, Bauzonen); sonst bliebe nach den Reihen der Einwohnerplätze nichts übrig. Die Werkstatt darf
// jede Drehung haben, die in die Fläche passt und ihre Vorderseite an eine Karrenstraße legt – der Kern nimmt für den Bauort die Drehung des Bauherrn.
//
// Platz gab es fast nur in Deutz: Um Alter Markt und Heumarkt stehen Startgrundstücke, Dom, St. Severin, Rheingasse und Rheinufer so dicht, dass
// selbst ohne alle Einwohnerplätze und Kulissen je Platz nur zwei Stellen unter 45 Minuten lägen – genau auf der Hausreihe vor dem Tor am Rhein
// (Wunsch des Nutzers vom 1.11.2026). Deshalb (Entscheidung des Nutzers, 1.11.2026): Deutz so viele Plätze wie passen – fünf (seine
// Einwohnerplätze und die Ruine rücken nach außen, die Zahl bleibt) –, der Alte Markt zwei an der neuen Gasse Unter Fettenhennen, der Heumarkt
// behält seine eine Stelle am Malzbüchel, die Hausreihe bleibt.

public sealed partial class Koeln
{
    /// <summary>Ein Werkstatt-Bauplatz: Kennung der Zone, Markt (für Kommentar und Bericht), Lage im Raster (x0, z0, x1, z1 in Metern).</summary>
    private sealed record Werkstattbauplatz(string Kennung, string Markt, double X0, double Z0, double X1, double Z1, string Wo);

    private static readonly Werkstattbauplatz[] FesteBauplaetze =
    {
        // Neumarkt (Deutz, 170 | 6): an Deutzer Freiheit und Kirchstraße, an den drei Ecken des Platzes, die nicht die Startgrundstücke S6–S8 belegen
        new("WB_Neumarkt1", "Neumarkt", 136, 12, 152, 26, "an der Deutzer Freiheit, westlich des Platzes (Südseite)"),
        new("WB_Neumarkt2", "Neumarkt", 152, 20, 166, 36, "an der Deutzer Kirchstraße, südwestlich des Platzes"),
        new("WB_Neumarkt3", "Neumarkt", 176, 20, 190, 36, "an der Deutzer Kirchstraße, südöstlich des Platzes"),
        new("WB_Neumarkt4", "Neumarkt", 186, 6, 202, 20, "an der Deutzer Freiheit Richtung Osttor (Südseite)"),
        new("WB_Neumarkt5", "Neumarkt", 192, -20, 208, -6, "an der Deutzer Freiheit Richtung Osttor (Nordseite)"),
        // Alter Markt (37 | −110): zwei Höfe zwischen Dom und S4 an der neuen Gasse Unter Fettenhennen
        new("WB_AlterMarkt1", "Alter Markt", 0, -92, 16, -78, "an Unter Fettenhennen, südwestlich des Platzes"),
        new("WB_AlterMarkt2", "Alter Markt", -20, -102, -6, -86, "am Ende von Unter Fettenhennen, hinter dem Dom"),
    };

    private static IEnumerable<Werkstattbauplatz> Werkstattbauplaetze => FesteBauplaetze;

    private static List<Kartenort> Umriss(Werkstattbauplatz b) => new() { P(b.X0, b.Z0), P(b.X1, b.Z0), P(b.X1, b.Z1), P(b.X0, b.Z1) };

    /// <summary>Liegt der Umriss näher als 2 m (Abstand einer Bauzone zu Gebäuden) an einem Werkstatt-Bauplatz?</summary>
    private static bool AmBauplatz(IReadOnlyList<Kartenort> umriss) =>
        Werkstattbauplaetze.Any(b => Kartengeometrie.AbstandVielecke(umriss, Umriss(b)) < 2
                                     || Kartengeometrie.ImVieleck(Kartengeometrie.Mitte(umriss), Umriss(b)));

    /// <summary>Liegt der Punkt auf einem Werkstatt-Bauplatz? (Die Rasterzonen lassen ihn aus.)</summary>
    private static bool AufBauplatz(Kartenort p) =>
        Werkstattbauplaetze.Any(b => p.X > b.X0 - 1e-6 && p.X < b.X1 + 1e-6 && p.Z > b.Z0 - 1e-6 && p.Z < b.Z1 + 1e-6);

    /// <summary>
    /// Die Ruine „Haus zum schwarzen Raben“ (L3) stand bis 1.11.2026 im Südosten von Deutz (196 | 27), wo jetzt Bauplätze liegen; sie rückt vor das
    /// Deutzer Südtor an die Siegburger Straße: die erste freie Stelle ab 10 m, die Vorderseite zur Straße.
    /// </summary>
    private void RuineVorDemSuedtor()
    {
        var weg = _strassen["Siegburger Straße"];
        var b = E.Katalog.Finde("leerstand")!.Groesse!;
        for (var s = 10.0; s < 80; s += 1)
        {
            if (Auf(weg.Verlauf, s) is not var (p, tx, tz)) break;
            var (nx, nz) = (tz, -tx);   // links in Laufrichtung (nach Süden): die Ostseite
            var abstand = weg.BreiteWirksam / 2 + 0.8 + b[1] / 2;
            var mitte = P(Math.Round((p.X + nx * abstand) * 2) / 2, Math.Round((p.Z + nz * abstand) * 2) / 2);
            var d = Math.Round(Math.Atan2(-nx, -nz) * 180 / Math.PI * 10) / 10;
            if (!Frei(Rechteck(mitte, b[0], b[1], d), 2)) continue;
            Haus("leerstand", "Haus zum schwarzen Raben (Ruine)", mitte.X, mitte.Z, d, "L3");
            Console.WriteLine($"   Ruine L3 vor dem Deutzer Südtor bei {mitte}");
            return;
        }
        throw new InvalidOperationException("Ruine L3: keine freie Stelle an der Siegburger Straße.");
    }

    /// <summary>Die Werkstatt-Bauplätze als eigene Bauzonen (nach den Zonen aus dem Raster, deren Kennungen so gleich bleiben).</summary>
    private void WerkstattbauplaetzeAnlegen()
    {
        foreach (var b in Werkstattbauplaetze)
        {
            var mitte = P((b.X0 + b.X1) / 2, (b.Z0 + b.Z1) / 2);
            var lage = LageVon(mitte) ?? throw new InvalidOperationException($"{b.Kennung}: keine Lage bei {mitte}.");
            E.BauzoneAnlegen(lage, Umriss(b), b.Kennung);
        }
        Console.WriteLine($"   Werkstatt-Bauplätze: {Werkstattbauplaetze.Count()}");
    }
}
