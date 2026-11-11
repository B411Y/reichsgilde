using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  „Hang mitformen“ (Fels-Plan R5, 02.11.2026)
//
//  Die großen Stücke des Felswand-Satzes haben hinten nur eine grobe Bruchfläche (seit R7 geschlossen): Sie sollen im Hang stecken, nicht auf der Wiese stehen. Ist im Felspinsel
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
//  Seit P4a (10.11.2026) rechnet dieselbe Hebung auch die Hang-Schicht der Stücke mit „hang“ (Felsschicht.cs): dort nie fest ins Raster.
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

    /// <summary>So weit (Meter) hinter dem Hinterland läuft das Gelände weich auf Null zurück (keine senkrechte Kante hinten).</summary>
    public const double Auslauf = 8;

    private static double Weich(double t) => t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t);

    /// <summary>Formt das Gelände hinter den Stücken (nur die, deren Form <see cref="Felsform.HangFormt"/> hat). Null, wenn sich nichts ändert.</summary>
    public static Gelaendeaenderung? Formen(Hoehenraster raster, Kartenrechteck karte, IEnumerable<Felslage> stuecke)
    {
        var aenderung = new Gelaendeaenderung(raster, karte);
        // erst alle Höhen aus dem Stand vor dem Eingriff, dann schreiben: das Ergebnis hängt nicht von der Reihenfolge der Stücke ab
        foreach (var ((i, j), cm) in Hebung(raster, karte, stuecke).OrderBy(p => p.Key.J).ThenBy(p => p.Key.I)) aenderung.Setzen(i, j, cm);
        return aenderung.Bereich is null ? null : aenderung;
    }

    /// <summary>
    /// Die Höhen (cm, ungerundet), auf die der Hang der Stücke die Rasterpunkte hebt – nur Punkte, die höher kämen als im Raster; mehrere Stücke
    /// als Höchstwert, aus dem Stand des Rasters (die Reihenfolge der Stücke ist gleichgültig). Für <see cref="Formen"/> und die Hang-Schicht (P4a).
    /// </summary>
    internal static Dictionary<(int I, int J), double> Hebung(Hoehenraster raster, Kartenrechteck karte, IEnumerable<Felslage> stuecke)
    {
        var liste = stuecke.ToList();
        return Hebung(raster, karte, liste, Urspruenge(raster, liste));
    }

    /// <summary>
    /// Wie <see cref="Hebung(Hoehenraster, Kartenrechteck, IEnumerable{Felslage})"/>, aber mit vorgegebener Höhe des Ursprungs je Stück (Meter, wie
    /// <see cref="Felsraum.UrsprungY"/>): Die Hang-Schicht (P4a) rechnet ihn auf der wirksamen Höhe, das Gelände (alt, Fuß) bleibt das Raster.
    /// <paramref name="nur"/> begrenzt auf diese Rasterpunkte (für die Höhe unter der Mitte, Felsschicht.Urspruenge).
    /// </summary>
    internal static Dictionary<(int I, int J), double> Hebung(Hoehenraster raster, Kartenrechteck karte, IReadOnlyList<Felslage> stuecke,
        IReadOnlyList<double> ursprung, (int I0, int J0, int I1, int J1)? nur = null, IReadOnlyList<bool>? aktiv = null)
    {
        var neu = new Dictionary<(int I, int J), double>();
        Durchlaufen(raster, karte, stuecke, ursprung, nur, aktiv, (i, j, soll, seitlich) =>
        {
            var alt = raster.Wert(i, j) / 100.0;
            if (soll <= alt) return;
            var cm = (alt + seitlich * (soll - alt)) * 100;
            if (!neu.TryGetValue((i, j), out var vorher) || cm > vorher) neu[(i, j)] = cm;
        });
        return neu;
    }

    /// <summary>Die Ursprünge der Stücke auf diesem Raster (<see cref="Felsraum.UrsprungY"/>) – so rechnet der feste Hang.</summary>
    internal static double[] Urspruenge(Hoehenraster raster, IReadOnlyList<Felslage> stuecke) => stuecke.Select(l => Felsraum.UrsprungY(l, raster)).ToArray();

    /// <summary>
    /// Welche Stücke einen Hang formen: Form mit <see cref="Felsform.HangFormt"/> und nicht vorgelagert (<see cref="Vorgelagert"/>) – mit diesen Ursprüngen.
    /// </summary>
    internal static bool[] Aktiv(IReadOnlyList<Felslage> stuecke, IReadOnlyList<double> ursprung)
    {
        var hang = stuecke.Select(l => Felsformen.Finde(l.Form) is { HangFormt: true }).ToArray();
        var aus = new bool[stuecke.Count];
        for (var k = 0; k < stuecke.Count; k++) aus[k] = hang[k] && !Vorgelagert(k, stuecke, ursprung, hang);
        return aus;
    }

    /// <summary>
    /// Die Rasterpunkte, auf die der Hang der Stücke wirken kann (unabhängig davon, wie hoch das Gelände dort schon liegt) – für „Hebung ablösen“
    /// (P4a, <see cref="Felsschicht.Abloesen"/>). Dieselbe Auswahl wie <see cref="Formen"/>: vorgelagerte Stücke zählen nicht.
    /// </summary>
    internal static void Flaeche(Hoehenraster raster, Kartenrechteck karte, IEnumerable<Felslage> stuecke, Action<int, int> punkt)
    {
        var liste = stuecke.ToList();
        Durchlaufen(raster, karte, liste, Urspruenge(raster, liste), null, null, (i, j, _, _) => punkt(i, j));
    }

    /// <summary>
    /// Ein Rechteck (Meter), außerhalb dessen der Hang eines Stücks nichts ändert, unabhängig vom Gelände – dazu die Mitte des Stücks (ihre Höhe geht in die Rechnung
    /// ein) samt einem Rasterabstand Rand. Null für Formen ohne <see cref="Felsform.HangFormt"/>: ihr Hang ist leer.
    /// </summary>
    internal static Kartenrechteck? Reichweite(Felslage l)
    {
        if (Felsformen.Finde(l.Form) is not { HangFormt: true } form) return null;
        var g = Felsraum.Groesse(l);
        var sz = l.Massstab * l.AchseZ;
        var (start, rueck) = ((form.Fusslinie - Vorlauf) * sz, form.Rueckwand * sz);
        if (start <= rueck + 0.5) start = rueck + 0.5;
        // die Höhe des Hangs ist höchstens die Höhe des Stücks, die Seiten höchstens SeiteMax breit (wie in Stueck, nach oben abgeschätzt)
        var (rv, ru) = (g.Y + Auslauf + 1, g.X / 2 + SeiteMax + 1);
        var t = l.Drehung * Math.PI / 180;
        var (c, s) = (DetMathe.Cos(t), DetMathe.Sin(t));
        double minx = l.X, minz = l.Z, maxx = l.X, maxz = l.Z;
        foreach (var (u, v) in new[] { (-ru, rueck - rv), (ru, rueck - rv), (-ru, Math.Max(start, 0)), (ru, Math.Max(start, 0)) })
        {
            var (wx, wz) = (l.X + u * c + v * s, l.Z - u * s + v * c);
            (minx, maxx, minz, maxz) = (Math.Min(minx, wx), Math.Max(maxx, wx), Math.Min(minz, wz), Math.Max(maxz, wz));
        }
        return new Kartenrechteck(minx, minz, maxx, maxz).Vergroessert(Hoehenraster.Abstand);
    }

    /// <summary>Ruft für jeden Rasterpunkt im Hang jedes nicht vorgelagerten Stücks (Form mit <see cref="Felsform.HangFormt"/>) Sollhöhe (Meter) und Anteil auf.</summary>
    private static void Durchlaufen(Hoehenraster raster, Kartenrechteck karte, IReadOnlyList<Felslage> stuecke, IReadOnlyList<double> ursprung,
        (int I0, int J0, int I1, int J1)? nur, IReadOnlyList<bool>? aktiv, Action<int, int, double, double> punkt)
    {
        var erlaubt = Hoehenraster.Punkte(karte);
        if (nur is { } n) erlaubt = (Math.Max(erlaubt.I0, n.I0), Math.Max(erlaubt.J0, n.J0), Math.Min(erlaubt.I1, n.I1), Math.Min(erlaubt.J1, n.J1));
        if (erlaubt.I0 > erlaubt.I1 || erlaubt.J0 > erlaubt.J1) return;
        aktiv ??= Aktiv(stuecke, ursprung);
        for (var k = 0; k < stuecke.Count; k++)
            if (aktiv[k]) Stueck(raster, erlaubt, stuecke[k], ursprung[k], Felsformen.Finde(stuecke[k].Form)!, punkt);
    }

    /// <summary>
    /// Steht ein höheres Stück der Auswahl hinter diesem (in dessen Breite)? Dann ist es vorgelagert – eine mittlere Wand vor einer großen –, und das
    /// Gelände steigt nur hinter dem großen, nicht als Wall dazwischen. Nur Stücke mit <paramref name="hang"/> zählen.
    /// </summary>
    private static bool Vorgelagert(int k, IReadOnlyList<Felslage> alle, IReadOnlyList<double> ursprung, bool[] hang)
    {
        var p = alle[k];
        var (gp, hp) = (Felsraum.Groesse(p), ursprung[k]);
        var (c, s) = (DetMathe.Cos(p.Drehung * Math.PI / 180), DetMathe.Sin(p.Drehung * Math.PI / 180));
        for (var m = 0; m < alle.Count; m++)
        {
            var q = alle[m];
            if (!hang[m] || m == k || q == p) continue;
            var gq = Felsraum.Groesse(q);
            var (dx, dz) = (q.X - p.X, q.Z - p.Z);
            var (u, v) = (dx * c - dz * s, dx * s + dz * c); // Ort von q in den Achsen von p (v: + = Schauseite)
            if (v < -2 && Math.Abs(u) < gp.X / 2 + 0.4 * gq.X && ursprung[m] + gq.Y > hp + gp.Y + 0.5) return true;
        }
        return false;
    }

    private static void Stueck(Hoehenraster raster, (int I0, int J0, int I1, int J1) erlaubt, Felslage l, double ursprung, Felsform form, Action<int, int, double, double> punkt)
    {
        var g = Felsraum.Groesse(l); // Breite, Höhe, Tiefe in Metern
        var (sx, sz) = (l.Massstab * l.AchseX, l.Massstab * l.AchseZ);
        var basis = raster.Hoehe(l.X, l.Z);
        var oben = ursprung + g.Y - Unterkante;
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
        var (rv, ru) = (hinten + Auslauf + 1, halb + seite + 1);
        double minx = double.MaxValue, minz = double.MaxValue, maxx = double.MinValue, maxz = double.MinValue;
        foreach (var (u, v) in new[] { (-ru, rueck - rv), (ru, rueck - rv), (-ru, start), (ru, start) })
        {
            // Achsen des Stücks → Welt (Kartengeometrie.Dreh): u-Achse (c, −s), v-Achse (s, c)
            var (wx, wz) = (l.X + u * c + v * s, l.Z - u * s + v * c);
            (minx, maxx, minz, maxz) = (Math.Min(minx, wx), Math.Max(maxx, wx), Math.Min(minz, wz), Math.Max(maxz, wz));
        }
        // die Rasterpunkte im Rechteck, die in der Karte liegen (wie Gelaendeaenderung.PunkteIn)
        var (ri0, rj0, ri1, rj1) = Hoehenraster.Punkte(new Kartenrechteck(minx, minz, maxx, maxz));
        var (i0, j0, i1, j1) = (Math.Max(ri0, erlaubt.I0), Math.Max(rj0, erlaubt.J0), Math.Min(ri1, erlaubt.I1), Math.Min(rj1, erlaubt.J1));
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
                var roh = Math.Max(0, d - (start - rueck));
                var dahinter = Math.Min(roh, hinten);
                seitlich *= 1 - Weich((roh - hinten) / Auslauf);
                if (seitlich <= 0) continue;
                var soll = basis + hoehe * (1 - (1 - Enden) * ende) * rampe + Hinterland * dahinter * (1 - (1 - Enden) * ende);
                punkt(i, j, soll, seitlich);
            }
    }
}
