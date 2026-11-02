using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  „Hang mitformen“ (Fels-Plan R5, 02.11.2026)
//
//  Die großen Stücke des Felswand-Satzes haben eine offene Rückseite: Sie sollen im Hang stecken, nicht auf der Wiese stehen. Ist im Felspinsel
//  das Häkchen „Hang mitformen“ gesetzt (Felspinsel.HangFormen, Vorgabe aus), hebt der Stempel das Gelände hinter und neben den Wänden an:
//    • Hinter der Fußlinie des Stücks (wo seine Vorderseite am Boden steht) bleibt das Gelände zunächst, wie es ist – erst 2,5 m dahinter
//      (Felshang.Vorlauf) beginnt es zu steigen, sonst stäche es durch die Vorderseite. Von dort steigt es in einer S-Kurve (ohne Knick) bis zur
//      Rückseite des Stücks auf die Höhe der Oberkante (knapp darunter, Felshang.Unterkante) – das sind bei einer 16 m hohen Wand knapp 70°, eine
//      Felswand eben –, und steigt dahinter sanft weiter (Felshang.Hinterland je Meter), so weit wie die Wand hoch ist.
//    • Zu den Seiten laufen die Wände in den Hang aus (ihre Enden werden niedriger, bis 55 %): Die Höhe geht an den Enden auf 60 % zurück,
//      und über eine Breite, die mit der Höhe wächst (3 bis 10 m), wird das Gelände weich auf Null zurückgenommen.
//    • Es wird nur angehoben, nie gesenkt; mehrere Stücke wirken als Höchstwert (die Reihenfolge ist gleichgültig).
//  Gerechnet wird mit der Drehung um die Senkrechte; Kippen, Rollen und Neigung bleiben außen vor. Alles aus ganzen Rasterpunkten, +, −, ·, / und
//  √ – gleich auf jedem Rechner. Der Befehl (Kartenentwurf.FelsHangBefehl) steht im selben Sammelbefehl wie Stücke und Boden: ein Schritt bei
//  Rückgängig.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Die Rechnung für „Hang mitformen“ (siehe Kopf der Datei).</summary>
public static class Felshang
{
    /// <summary>So weit hinter der Fußlinie (Meter) beginnt das Gelände zu steigen; R4: weniger, und es sticht durch die Vorderseite.</summary>
    public const double Vorlauf = 2.5;

    /// <summary>Um so viel (Meter) bleibt die Kuppe unter der Oberkante des Stücks.</summary>
    public const double Unterkante = 0.4;

    /// <summary>So steil steigt das Gelände hinter der Rückseite weiter (Meter je Meter), über höchstens die Höhe des Stücks.</summary>
    public const double Hinterland = 0.25;

    /// <summary>Höhe an den Enden der Wand im Verhältnis zur Mitte.</summary>
    public const double Enden = 0.6;

    /// <summary>Kleinste und größte Breite (Meter), über die das Gelände seitlich auf Null zurückgeht.</summary>
    public const double SeiteMin = 3, SeiteMax = 10;

    private static double Weich(double t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);

    /// <summary>Formt das Gelände hinter den Stücken (nur die, deren Form <see cref="Felsform.HangFormt"/> hat). Null, wenn sich nichts ändert.</summary>
    public static Gelaendeaenderung? Formen(Hoehenraster raster, Kartenrechteck karte, IEnumerable<Felslage> stuecke)
    {
        var aenderung = new Gelaendeaenderung(raster, karte);
        // erst alle Höhen aus dem Stand vor dem Eingriff, dann schreiben: das Ergebnis hängt nicht von der Reihenfolge der Stücke ab
        var neu = new Dictionary<(int I, int J), double>();
        var liste = stuecke.Where(l => Felsformen.Finde(l.Form) is { HangFormt: true }).ToList();
        foreach (var l in liste)
        {
            if (Vorgelagert(raster, l, liste)) continue;
            Stueck(raster, aenderung, l, Felsformen.Finde(l.Form)!, neu);
        }
        foreach (var ((i, j), cm) in neu.OrderBy(p => p.Key.J).ThenBy(p => p.Key.I)) aenderung.Setzen(i, j, cm);
        return aenderung.Bereich is null ? null : aenderung;
    }

    /// <summary>
    /// Steht ein höheres Stück der Auswahl hinter diesem (in dessen Breite)? Dann ist es vorgelagert – eine mittlere Wand vor einer großen –, und das
    /// Gelände steigt nur hinter dem großen, nicht als Wall dazwischen.
    /// </summary>
    private static bool Vorgelagert(Hoehenraster raster, Felslage p, List<Felslage> alle)
    {
        var (gp, hp) = (Felsraum.Groesse(p), Felsraum.UrsprungY(p, raster));
        var (c, s) = (DetMathe.Cos(p.Drehung * Math.PI / 180), DetMathe.Sin(p.Drehung * Math.PI / 180));
        foreach (var q in alle)
        {
            if (ReferenceEquals(q, p) || q == p) continue;
            var gq = Felsraum.Groesse(q);
            var (dx, dz) = (q.X - p.X, q.Z - p.Z);
            var (u, v) = (dx * c - dz * s, dx * s + dz * c); // Ort von q in den Achsen von p (v: + = Schauseite)
            if (v < -2 && Math.Abs(u) < gp.X / 2 + 0.4 * gq.X && Felsraum.UrsprungY(q, raster) + gq.Y > hp + gp.Y + 0.5) return true;
        }
        return false;
    }

    private static void Stueck(Hoehenraster raster, Gelaendeaenderung aenderung, Felslage l, Felsform form, Dictionary<(int I, int J), double> neu)
    {
        var g = Felsraum.Groesse(l); // Breite, Höhe, Tiefe in Metern
        var (sx, sz) = (l.Massstab * l.AchseX, l.Massstab * l.AchseZ);
        var basis = raster.Hoehe(l.X, l.Z);
        var oben = Felsraum.UrsprungY(l, raster) + g.Y - Unterkante;
        var hoehe = oben - basis;
        if (hoehe <= 0.5) return;
        var (start, rueck) = ((form.Fusslinie - Vorlauf) * sz, form.Rueckwand * sz); // v: Weg in der Tiefe des Stücks, + = Schauseite
        if (start <= rueck + 0.5) start = rueck + 0.5;
        var halb = g.X / 2;
        var seite = Math.Clamp(0.5 * hoehe, SeiteMin, SeiteMax);
        var hinten = Math.Min(hoehe, g.Y); // so weit hinter der Rückseite steigt es weiter
        var t = l.Drehung * Math.PI / 180;
        var (c, s) = (DetMathe.Cos(t), DetMathe.Sin(t));
        // Rechteck um das Stück samt Rand (Meter), als Grenzen der Rasterpunkte
        var (rv, ru) = (hinten + 1, halb + seite + 1);
        double minx = double.MaxValue, minz = double.MaxValue, maxx = double.MinValue, maxz = double.MinValue;
        foreach (var (u, v) in new[] { (-ru, rueck - rv), (ru, rueck - rv), (-ru, start), (ru, start) })
        {
            // Achsen des Stücks → Welt (Kartengeometrie.Dreh): u-Achse (c, −s), v-Achse (s, c)
            var (wx, wz) = (l.X + u * c + v * s, l.Z - u * s + v * c);
            (minx, maxx, minz, maxz) = (Math.Min(minx, wx), Math.Max(maxx, wx), Math.Min(minz, wz), Math.Max(maxz, wz));
        }
        var (i0, j0, i1, j1) = aenderung.PunkteIn(new Kartenrechteck(minx, minz, maxx, maxz));
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
            {
                var (dx, dz) = (i * Hoehenraster.Abstand - l.X, j * Hoehenraster.Abstand - l.Z);
                var (u, v) = (dx * c - dz * s, dx * s + dz * c);
                var d = start - v; // Weg hinter dem Beginn des Anstiegs
                if (d <= 0) continue;
                var au = Math.Abs(u);
                // Breite: voll bis zum Ende der Wand, dann weich auf Null; an den Enden der Wand nur 60 % der Höhe
                var seitlich = 1 - Weich((au - halb) / seite);
                if (seitlich <= 0) continue;
                var ende = au >= halb ? 1.0 : (au / halb) * (au / halb);
                var rampe = d >= start - rueck ? 1 : Weich(d / (start - rueck));
                var dahinter = Math.Min(Math.Max(0, d - (start - rueck)), hinten);
                var soll = basis + hoehe * (1 - (1 - Enden) * ende) * rampe + Hinterland * dahinter * (1 - (1 - Enden) * ende);
                var alt = raster.Wert(i, j) / 100.0;
                if (soll <= alt) continue;
                var cm = (alt + seitlich * (soll - alt)) * 100;
                if (!neu.TryGetValue((i, j), out var vorher) || cm > vorher) neu[(i, j)] = cm;
            }
    }
}
