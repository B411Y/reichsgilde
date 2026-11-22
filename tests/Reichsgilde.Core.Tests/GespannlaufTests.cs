using Reichsgilde;

/// <summary>
/// Schleppkurve der Karren (Darstellung, 22.11.2026; <c>game/scripts/Gespannlauf.cs</c>, hier mitübersetzt): Abstand Zugpunkt–Deichsel konstant, der Wagen
/// schneidet die Kurve innen, Vierradwagen mit Drehschemel, Knickwinkel begrenzt, gleiches Ergebnis bei 30 und 144 Bildern, Wenden ohne Durchdringen.
/// </summary>
static class GespannlaufTests
{
    static void Fehler(string s) => throw new Exception(s);

    /// <summary>Fährt den Zugpunkt auf einem Streckenzug mit 1,3 m/s ab und ruft <paramref name="je"/> nach jedem Bild.</summary>
    static Gespannlauf Fahren(Gespannlauf g, (double X, double Z)[] weg, double fps, Action<Gespannlauf>? je = null, double tempo = 1.3)
    {
        var laengen = new List<double>();
        for (var i = 0; i + 1 < weg.Length; i++) laengen.Add(Math.Sqrt(Math.Pow(weg[i + 1].X - weg[i].X, 2) + Math.Pow(weg[i + 1].Z - weg[i].Z, 2)));
        var gesamt = laengen.Sum();
        ((double X, double Z) P, (double X, double Z) R) Bei(double s)
        {
            s = Math.Clamp(s, 0, gesamt);
            for (var i = 0; i < laengen.Count; i++)
            {
                if (s <= laengen[i] || i == laengen.Count - 1)
                {
                    var t = laengen[i] > 0 ? s / laengen[i] : 0;
                    return ((weg[i].X + (weg[i + 1].X - weg[i].X) * t, weg[i].Z + (weg[i + 1].Z - weg[i].Z) * t), ((weg[i + 1].X - weg[i].X) / laengen[i], (weg[i + 1].Z - weg[i].Z) / laengen[i]));
                }
                s -= laengen[i];
            }
            return (weg[0], (0, 1));
        }
        var (p0, r0) = Bei(0);
        g.Setzen(p0.X, p0.Z, r0.X, r0.Z);
        var dt = 1.0 / fps;
        for (var s = 0.0; s < gesamt; s += tempo * dt)
        {
            // Der Kurs des Weges wird über 1 m geglättet (wie die Darstellung: Blick ein Stück voraus)
            var (p, _) = Bei(s);
            var (v, _) = Bei(s + 1.0);
            var (rx, rz) = (v.X - p.X, v.Z - p.Z);
            g.Weiter(p.X, p.Z, rx, rz, tempo * dt * 2);
            je?.Invoke(g);
        }
        return g;
    }

    static readonly (double X, double Z)[] Ecke = { (0, 0), (0, 30), (30, 30), (30, 60) };
    static readonly (double X, double Z)[] Weite = Enumerable.Range(0, 91).Select(i => (20 * (1 - Math.Cos(i * Math.PI / 180)) , 20 * Math.Sin(i * Math.PI / 180))).ToArray();

    public static IEnumerable<(string, Action)> Alle()
    {
        yield return ("Gespannlauf: Abstand Zugpunkt-Deichsel bleibt in Ecke und Kurve konstant", () =>
        {
            foreach (var weg in new[] { Ecke, Weite })
            {
                var g = new Gespannlauf(4.7, 0, 50, 2.5);
                var streuung = 0.0;
                Fahren(g, weg, 60, x => streuung = Math.Max(streuung, Math.Abs(x.DeichselIst - 4.7)));
                if (streuung > 1e-6) Fehler($"Deichselabstand schwankt um {streuung}");
            }
        });
        yield return ("Gespannlauf: Einachser schneidet die Ecke innen", () =>
        {
            var g = new Gespannlauf(4.7, 0, 60, 2.5);
            var innen = double.MaxValue;
            Fahren(g, Ecke, 60, x => innen = Math.Min(innen, Math.Sqrt(Math.Pow(x.VorneX - 0, 2) + Math.Pow(x.VorneZ - 30, 2))));
            if (innen > 4.0 || innen < 0.2) Fehler($"Achse zieht in {innen:0.00} m Abstand an der Ecke vorbei (erwartet knapp innen, 0,2 bis 4 m)");
            if (Math.Abs(g.VorneX - g.ZugX) > 0.2 && g.ZugZ > 59) { } // gestreckt nach der Ecke: Achse hinter dem Zugpunkt
            if (Math.Abs(g.ZugX - 30) > 0.5 || Math.Abs(g.VorneX - 30) > 0.5) Fehler($"nach der Kurve nicht gestreckt: Zug {g.ZugX:0.00}, Achse {g.VorneX:0.00}");
        });
        yield return ("Gespannlauf: gerade Strecke folgt dem Weg ohne Abweichung", () =>
        {
            var g = new Gespannlauf(5.7, 1.9, 45, 3);
            Fahren(g, new[] { (0.0, 0.0), (0.0, 40.0) }, 30);
            if (Math.Abs(g.VorneX) > 1e-6 || Math.Abs(g.VorneZ - (g.ZugZ - 5.7)) > 1e-6 || Math.Abs(g.HintenZ - (g.VorneZ - 1.9)) > 1e-6) Fehler("gerade Fahrt verschiebt das Gespann");
        });
        yield return ("Gespannlauf: Vierradwagen - Hinterachse folgt der Vorderachse im Radstand, Hinterachse schneidet innen", () =>
        {
            var g = new Gespannlauf(5.7, 1.9, 45, 3);
            var streuung = 0.0; var maxKnick = 0.0;
            Fahren(g, Ecke, 60, x =>
            {
                streuung = Math.Max(streuung, Math.Abs(Math.Sqrt(Math.Pow(x.VorneX - x.HintenX, 2) + Math.Pow(x.VorneZ - x.HintenZ, 2)) - 1.9));
                maxKnick = Math.Max(maxKnick, Math.Abs(x.Knick));
            });
            if (streuung > 1e-6) Fehler($"Radstand schwankt um {streuung}");
            if (maxKnick > 45 * Math.PI / 180 + 1e-9) Fehler($"Knick {maxKnick * 180 / Math.PI:0.0} Grad über der Grenze");
        });
        yield return ("Gespannlauf: Knickwinkel bleibt in der engsten Ecke und beim Wenden begrenzt", () =>
        {
            var g = new Gespannlauf(4.7, 0, 50, 2.5);
            var maxKnick = 0.0;
            Fahren(g, new[] { (0.0, 0.0), (0.0, 12.0), (0.5, 12.0), (0.5, 0.0) }, 60, x => maxKnick = Math.Max(maxKnick, Math.Abs(x.Knick)));
            if (maxKnick > 50 * Math.PI / 180 + 1e-9) Fehler($"Knick {maxKnick * 180 / Math.PI:0.0} Grad");
        });
        yield return ("Gespannlauf: gleiches Ergebnis bei 30, 60 und 144 Bildern je Sekunde", () =>
        {
            foreach (var weg in new[] { Ecke, Weite })
            {
                var erg = new List<Gespannlauf>();
                foreach (var fps in new[] { 30.0, 60.0, 144.0 })
                    erg.Add(Fahren(new Gespannlauf(4.7, 0, 50, 2.5), weg, fps, tempo: 1.3));
                for (var i = 1; i < erg.Count; i++)
                {
                    var d = Math.Sqrt(Math.Pow(erg[i].VorneX - erg[0].VorneX, 2) + Math.Pow(erg[i].VorneZ - erg[0].VorneZ, 2));
                    if (d > 0.15) Fehler($"Achse weicht zwischen Bildraten um {d:0.000} m ab");
                }
            }
        });
        yield return ("Gespannlauf: Wenden - das Tier läuft einen Bogen, Zugpunkt springt nie", () =>
        {
            var g = new Gespannlauf(4.7, 0, 50, 2.5);
            g.Setzen(0, 4.7, 0, 1);
            // Ziel springt hinter das Gespann (Ankunft am Markt, sofortige Rückfahrt)
            var zuletzt = (X: g.ZugX, Z: g.ZugZ);
            var maxSprung = 0.0;
            for (var i = 0; i < 1200 && Math.Sqrt(Math.Pow(g.ZugX - 0, 2) + Math.Pow(g.ZugZ + 4.7, 2)) > 0.01; i++)
            {
                g.Weiter(0, -4.7 + i * 0 , 0, -1, 4.0 / 60);
                maxSprung = Math.Max(maxSprung, Math.Sqrt(Math.Pow(g.ZugX - zuletzt.X, 2) + Math.Pow(g.ZugZ - zuletzt.Z, 2)));
                zuletzt = (g.ZugX, g.ZugZ);
                if (Math.Abs(g.Knick) > 50 * Math.PI / 180 + 1e-9) Fehler("Knick über der Grenze beim Wenden");
            }
            if (maxSprung > Gespannlauf.Schnapp + 0.01) Fehler($"Zugpunkt springt um {maxSprung:0.00} m");
            if (Math.Abs(g.KursZ + 1) > 0.05) Fehler($"Gespann blickt nach dem Wenden nicht zurück (Kurs Z {g.KursZ:0.00})");
        });
        yield return ("Gespannlauf: Halt - bleibt das Ziel stehen, bewegt sich nichts", () =>
        {
            var g = new Gespannlauf(4.7, 0, 50, 2.5);
            Fahren(g, Ecke, 60);
            var (a, b) = (g.VorneX, g.VorneZ);
            for (var i = 0; i < 100; i++) g.Weiter(g.ZugX, g.ZugZ, g.KursX, g.KursZ, 0);
            if (Math.Abs(g.VorneX - a) + Math.Abs(g.VorneZ - b) > 1e-9) Fehler("Gespann wandert im Stand");
        });
    }
}
