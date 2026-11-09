using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Staedte;

// Köln: Hofstellen (Entscheidung des Nutzers vom 9.11.2026). Eine Hofstelle fasst einen Werkstattplatz (16 × 14 m) und einen Wohnhausplatz
// (8 × 10 m) der Karte zusammen – den Platz eines Hauses in der Stadt. Sie löst die festen Startgrundstücke und die Werkstatt-Bauplätze ab
// (Bauzonen mit `nur: werkstatt`, bis 9.11.2026 hier in Koeln.Bauplaetze.cs; Befund H2 der Prüfung von phase5/gehtempo, Entscheidung des
// Nutzers vom 1.11.2026: Seit der Kern nach der Weglänge rechnet, entscheidet der Karrenweg über eine neue Werkstatt – jeder Bauplatz unter
// 45 Minuten Handkarrenfahrt, etwa 35 m Karrenweg zum nächsten Platz):
//
//    • Start-Hofstellen S1–S8 (Koeln.Platzierung.cs): die acht Startgrundstücke. Im Spiel wählt der Spieler eine, jedes KI-Haus bekommt eine
//      geloste; die Vorgabe (Spieler S1, KI-Häuser S2–S8) gilt ohne Los.
//    • Freie Hofstellen (hier, `start` fehlt): N1–N5 am Neumarkt in Deutz und A1 hinter dem Dom am Alten Markt auf den Stellen der sechs
//      Werkstatt-Bauplätze (WB_Neumarkt1–5, WB_AlterMarkt2), dazu R1 und R2 am Rheinhafen. Sie bleiben Bauland, bis ein Haus sie baut: der
//      Werkstattteil nimmt nur eine Werkstatt auf, der Wohnhausteil nur ein Wohnhaus, jeweils genau eingepasst.
//
// Die Werkstattplätze der freien Hofstellen liegen dort, wo die Bauplätze lagen (gleiche Tür); vier rücken wenige Meter, weil die
// Kartenprüfung 2 m zwischen zwei Werkstattplätzen verlangt (die Bauplätze stießen aneinander): N2 1,9 m nach Osten (bis an die Kirchstraße),
// N1 0,15 m nach Westen (bis an die Urbanusgasse), N3 1,6 m nach Süden, N4 0,5 m nach Norden. Der Wohnhausteil steht daneben (vorn bündig oder
// gegenüber), so nah, wie die Stadt es zulässt. Die Plätze stehen vor den Einwohnerplätzen und Kulissen (Einwohnerplaetze()), die ihnen weichen.

public sealed partial class Koeln
{
    /// <summary>
    /// Eine freie Hofstelle: Kennung, Name (Lage), Werkstattplatz und Wohnhausplatz (Mitte und Drehung), Abstand zu anderen Gebäuden (Meter;
    /// kleiner als die üblichen 1,5, wo es sonst nicht passt) und Abstand zur Mauer.
    /// </summary>
    private sealed record Hofplan(string Kennung, string Name, double Wx, double Wz, double Wd, double Hx, double Hz, double Hd, double Abstand = 1.5, double Mauer = 4);

    private static readonly Hofplan[] FreieHofplaene =
    {
        // Neumarkt (Deutz, 170 | 6): an Deutzer Freiheit und Kirchstraße; die Wohnhausteile an der Urbanusgasse, an der Abteigasse und hinter N5
        new("N1", "an der Deutzer Freiheit (Brückenseite)", 143.85, 19, 180, 140, 34.9, 180, Abstand: 0.1, Mauer: 1.5),
        new("N2", "an der Deutzer Kirchstraße (West)", 160.9, 28, 90, 148.3, 34.9, 180, Abstand: 0.1, Mauer: 1.5),
        new("N3", "an der Deutzer Kirchstraße (Ost)", 183, 29.7, 270, 199, 33.5, 90, Abstand: 0.5, Mauer: 1.5),
        new("N4", "an der Deutzer Freiheit (Osttor)", 194, 12.6, 180, 199, 24.6, 90, Abstand: 0.5, Mauer: 1.5),
        new("N5", "an der Deutzer Freiheit (Nordseite)", 200, -13, 0, 196, -26.2, 0, Mauer: 1.5),
        // Alter Markt (37 | −110): ein Hof am Ende der Straße Unter Fettenhennen hinter dem Dom, der Wohnhausteil an der Hohen Straße
        new("A1", "Unter Fettenhennen", -13, -94, 90, -26.2, -94, 270),
        // Rheinhafen (51 | −54): im Streifen zwischen Rheingasse und Ufer, die Tür zur Rheingasse; R1 zwischen Hafen und Zollstelle (der Wohnhausteil
        // gegenüber an der Rheingasse), R2 zwischen Zollstelle und Rheinbrücke (der Wohnhausteil davor, an der Zollstelle)
        new("R1", "am Rheinhafen", 46.81, -37, 270, 29.1, -31, 90, Abstand: 0.9),
        new("R2", "an der Rheinbrücke", 46.81, -2.81, 270, 44.81, -15, 270, Abstand: 0.9),
    };

    /// <summary>
    /// Die freien Hofstellen anlegen (nach den Start-Hofstellen, vor den Einwohnerplätzen und Kulissen, die ihnen weichen): Werkstattplatz und
    /// Wohnhausplatz je Hofstelle, ohne `start`.
    /// </summary>
    private void FreieHofstellenAnlegen()
    {
        foreach (var p in FreieHofplaene)
        {
            var (w, h) = (Rechteck(P(p.Wx, p.Wz), 16, 14, p.Wd), Rechteck(P(p.Hx, p.Hz), 8, 10, p.Hd));
            if (!Frei(w, p.Abstand, 3, p.Mauer) || !Frei(h, p.Abstand, 3, p.Mauer))
                throw new InvalidOperationException($"Hofstelle {p.Kennung} ({p.Name}): Werkstatt frei {Frei(w, p.Abstand, 3, p.Mauer)}, Wohnhaus frei {Frei(h, p.Abstand, 3, p.Mauer)}. Werkstatt: {Warum(w, p.Abstand, p.Mauer)}; Wohnhaus: {Warum(h, p.Abstand, p.Mauer)}");
            if (Kartengeometrie.AbstandVielecke(w, h) < 0.1)
                throw new InvalidOperationException($"Hofstelle {p.Kennung}: Werkstatt und Wohnhaus überlappen.");
            foreach (var g in E.Gebaeude.Where(g => g.Art == Bausteinart.Werkstattplatz))   // Kartenprüfung: 2 m zwischen zwei Werkstattplätzen
                if (Kartengeometrie.AbstandVielecke(w, g.Ecken()) < 2.01)
                    throw new InvalidOperationException($"Hofstelle {p.Kennung}: weniger als 2 m zur Werkstatt {g.Kennung}.");
            var werkstatt = E.Setzen("werkstattplatz", p.Wx, p.Wz, p.Wd);
            var wohnhaus = E.Setzen("wohnhausplatz", p.Hx, p.Hz, p.Hd);
            E.KennungSetzen(werkstatt.Kennung, $"{p.Kennung}_Werkstatt");
            E.KennungSetzen(wohnhaus.Kennung, $"{p.Kennung}_Wohnhaus");
            E.Umbenennen($"{p.Kennung}_Werkstatt", $"Werkstatt Hofstelle {p.Kennung}");
            E.Umbenennen($"{p.Kennung}_Wohnhaus", $"Wohnhaus Hofstelle {p.Kennung}");
            E.LageSetzen($"{p.Kennung}_Wohnhaus", Lage.Mauerring);
            E.HofstelleBilden($"{p.Kennung}_Werkstatt", $"{p.Kennung}_Wohnhaus", start: false, name: p.Name, kennung: p.Kennung);
        }
        Console.WriteLine($"   Freie Hofstellen: {FreieHofplaene.Length}");
    }

    /// <summary>Was an einer Fläche im Weg steht (für die Fehlermeldung, wenn eine Hofstelle nicht passt): Gebäude, Wege, Mauern, Wasser.</summary>
    private string Warum(IReadOnlyList<Kartenort> u, double abstand, double mauer)
    {
        var l = new List<string>();
        foreach (var g in E.Gebaeude) { var e = g.Ecken(); if (e.Count > 0 && Kartengeometrie.AbstandVielecke(u, e) < abstand) l.Add($"Gebäude {g.Kennung}"); }
        foreach (var w in E.Wege) if (Kartengeometrie.AbstandVielecke(u, w.Verlauf, false) < w.BreiteWirksam / 2 + 0.3) l.Add($"Weg {w.Name}");
        foreach (var m in E.Mauern) if (Kartengeometrie.AbstandVielecke(u, m.Ecken, m.Geschlossen) < mauer) l.Add($"Mauer {m.Name}");
        if (E.Wasserform.Ueberlappt(u) is not null) l.Add("Wasser");
        foreach (var q in u.Append(Kartengeometrie.Mitte(u))) if (E.Wasserform.Uferabstand(q.X, q.Z, 8) < 3) l.Add($"Ufer {q}");
        return string.Join(", ", l);
    }

    /// <summary>
    /// Die Ruine „Haus zum schwarzen Raben“ (L3) stand bis 1.11.2026 im Südosten von Deutz (196 | 27), wo jetzt Hofstellen liegen; sie rückt vor das
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
}
