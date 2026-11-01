using Reichsgilde;

/// <summary>
/// Die freie Fläche eines Marktplatzes (Darstellung, Markt v3 vom 1.11.2026; Modell in <c>game/scripts/Marktflaeche.cs</c>, hier mitübersetzt): Wege über
/// das Pflaster gehen um Stände, Brunnen und Bänke herum, bleiben auf dem Platz, fächern sich je Person auf, und der Raum zum Ausweichen endet vor dem
/// nächsten Hindernis. Der Testplatz ist wie der Greybox-Markt gebaut: 24 × 24 m, Brunnen in der Mitte, sieben Stände.
/// </summary>
static class MarktflaecheTests
{
    const float Frei = 1.1f;

    /// <summary>Ein Platz wie im Modell <c>marktplatz.glb</c>, um <paramref name="winkel"/> (Bogenmaß) gedreht und nach (<paramref name="ox"/>, <paramref name="oz"/>) verschoben.</summary>
    static Marktflaeche Testplatz(float winkel = 0, float ox = 0, float oz = 0)
    {
        var (ax, az) = (MathF.Cos(winkel), MathF.Sin(winkel));
        var f = new Marktflaeche(ox, oz, ax, az, 12);
        (float X, float Z) Welt(float u, float v) => (ox + u * ax - v * az, oz + u * az + v * ax);
        f.Kreis(ox, oz, 1.98f);
        foreach (var (u, v) in new[] { (-8f, -8f), (0f, -9f), (8f, -8f), (-9f, 2f), (9f, 2f), (-4f, 8f), (4f, 8f) })
        {
            // Front zur Mitte: die erste Achse quer zur Richtung zur Mitte
            var (cx, cz) = Welt(u, v);
            var (dx, dz) = (ox - cx, oz - cz);
            var l = MathF.Sqrt(dx * dx + dz * dz);
            f.Stand(new Marktflaeche.Rechteck(cx, cz, -dz / l, dx / l, 1.45f, 0.78f));
        }
        var (bx, bz) = Welt(10.6f, 10f);
        f.Rechteckig(new Marktflaeche.Rechteck(bx, bz, ax, az, 1.25f, 0.26f));
        return f;
    }

    /// <summary>Wie nah (Meter) ein Weg einem Hindernis kommt (abgetastet alle 5 cm, ohne die ersten und letzten <paramref name="enden"/> Meter).</summary>
    static float Naechster(Marktflaeche f, List<(float X, float Z)> weg, float enden = 0)
    {
        var (gesamt, s, naechster) = (Marktflaeche.Laenge(weg), 0f, float.MaxValue);
        for (var i = 1; i < weg.Count; i++)
        {
            var (dx, dz) = (weg[i].X - weg[i - 1].X, weg[i].Z - weg[i - 1].Z);
            var l = MathF.Sqrt(dx * dx + dz * dz);
            for (var t = 0f; t <= l; t += 0.05f)
            {
                var gegangen = s + t;
                if (gegangen < enden || gesamt - gegangen < enden) continue;
                var (x, z) = (weg[i - 1].X + dx * t / Math.Max(l, 1e-6f), weg[i - 1].Z + dz * t / Math.Max(l, 1e-6f));
                naechster = MathF.Min(naechster, f.Abstand(x, z));
                Pruefe.Wahr(f.Auf(x, z, -0.06f), $"Punkt ({x:0.00}|{z:0.00}) liegt nicht auf dem Pflaster");
            }
            s += l;
        }
        return naechster;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Marktfläche: Abstand zum gedrehten Rechteck – außen wie zum nächsten Punkt, innen negativ", () =>
        {
            var r = new Marktflaeche.Rechteck(0, 0, MathF.Cos(0.5f), MathF.Sin(0.5f), 2, 1);
            Pruefe.Ungefaehr(-1, r.Abstand(0, 0), 1e-4);
            var (ux, uz) = (MathF.Cos(0.5f), MathF.Sin(0.5f));
            Pruefe.Ungefaehr(1, r.Abstand(ux * 3, uz * 3), 1e-4);                  // längs: 1 m vor der Kante
            Pruefe.Ungefaehr(0.5, r.Abstand(-uz * 1.5f, ux * 1.5f), 1e-4);        // quer: 0,5 m
            Pruefe.Ungefaehr(MathF.Sqrt(2), r.Abstand(ux * 3 - uz * 2, uz * 3 + ux * 2), 1e-4); // schräg vor der Ecke
        }),
        ("Marktfläche: freie Gerade bleibt eine Gerade", () =>
        {
            var f = Testplatz();
            var weg = f.Pfad((-11, -4), (-5, -4), Frei);
            Pruefe.Wahr(weg is { Count: 2 }, $"zwei Punkte erwartet, {weg?.Count}");
        }),
        ("Marktfläche: Weg quer über den Platz geht um den Brunnen und hält überall den Abstand", () =>
        {
            foreach (var winkel in new[] { 0f, 0.7f, -2.2f })
            {
                var f = Testplatz(winkel, 30, -12);
                var von = (30 + -11.5f * MathF.Cos(winkel), -12 + -11.5f * MathF.Sin(winkel));
                var nach = (30 + 11.5f * MathF.Cos(winkel), -12 + 11.5f * MathF.Sin(winkel));
                var weg = f.Pfad(von, nach, Frei);
                Pruefe.Wahr(weg is not null, "kein Weg gefunden");
                Pruefe.Gleich(von, weg![0]);
                Pruefe.Gleich(nach, weg[^1]);
                Pruefe.Wahr(weg.Count >= 3, "der Brunnen steht im Weg: mindestens eine Ecke");
                var naechster = Naechster(f, weg);
                Pruefe.Wahr(naechster >= Frei - 0.13f, $"Drehung {winkel}: nur {naechster:0.00} m zum nächsten Hindernis");
                // straff: kaum länger als nötig um den Brunnen (Umweg um einen Kreis von 1,98 + 1,1 m Halbmesser)
                var noetig = 2 * MathF.Sqrt(11.5f * 11.5f - 3.08f * 3.08f) + 3.08f * 2 * MathF.Asin(3.08f / 11.5f);
                Pruefe.Wahr(Marktflaeche.Laenge(weg) < noetig * 1.06f, $"Weg {Marktflaeche.Laenge(weg):0.0} m, nötig {noetig:0.0} m");
            }
        }),
        ("Marktfläche: an einen Stand heran (Platz vor der Auslage) und wieder weg, nie durch einen Stand", () =>
        {
            var f = Testplatz();
            // Kundenplatz 1,45 m vor der Mitte des Standes (0|−9), also zur Mitte hin
            var kunde = (0.15f, -9f + 1.45f);
            foreach (var von in new[] { (-11f, 0f), (11f, -11f), (6f, 10f), (-6f, -11.5f) })
            {
                var weg = f.Pfad(von, kunde, Frei) ?? throw new Exception($"kein Weg von {von}");
                Pruefe.Wahr(Naechster(f, weg, enden: 1.4f) >= Frei - 0.13f, $"von {von}: zu nah an einem Hindernis");
                foreach (var stand in f.Staende) Pruefe.Wahr(Naechster(new[] { stand }, weg) >= 0.4f, $"von {von}: geht durch einen Stand");
            }
        }),
        ("Marktfläche: gefächert – zwei Seiten, zwei Wege, beide frei; ohne Seite der gerade", () =>
        {
            var f = Testplatz();
            var (von, nach) = ((-6f, -5f), (-6f, 5f));
            var links = f.Gefaechert(von, nach, Frei, -1)!;
            var rechts = f.Gefaechert(von, nach, Frei, 1)!;
            var gerade = f.Gefaechert(von, nach, Frei, 0)!;
            Pruefe.Wahr(gerade.Count == 2, "ohne Seite gerade");
            float Quer(List<(float X, float Z)> w)
            {
                float Strecke((float X, float Z) a, (float X, float Z) b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
                var m = w.OrderBy(p => MathF.Abs(Strecke(von, p) - Strecke(p, nach))).First();
                var (dx, dz) = (nach.Item1 - von.Item1, nach.Item2 - von.Item2);
                return ((m.X - von.Item1) * -dz + (m.Z - von.Item2) * dx) / MathF.Sqrt(dx * dx + dz * dz);
            }
            Pruefe.Wahr(Quer(links) < -0.5f && Quer(rechts) > 0.5f, $"seitlich {Quer(links):0.0} / {Quer(rechts):0.0} m");
            Pruefe.Wahr(Naechster(f, links) >= Frei - 0.13f && Naechster(f, rechts) >= Frei - 0.13f, "gefächert zu nah an einem Hindernis");
            Pruefe.Gleich(von, links[0]);
            Pruefe.Gleich(nach, rechts[^1]);
        }),
        ("Marktfläche: Raum zum Ausweichen endet vor dem Hindernis und am Rand", () =>
        {
            var f = Testplatz();
            Pruefe.Ungefaehr(1.5, f.Raum(-6, 0, 0, 1, 0.32f, 1.5f), 1e-4);         // frei: so weit wie erlaubt
            var richtung = f.Raum(4, 0, -1, 0, 0.32f, 3f);                           // zum Brunnen (Rand bei 1,98)
            Pruefe.Ungefaehr(4 - 1.98 - 0.32, richtung, 0.06);
            Pruefe.Wahr(f.Raum(2.2f, 0, -1, 0, 0.32f, 1.5f) == 0, "schon zu nah: kein Raum");
            Pruefe.Ungefaehr(1 + Marktflaeche.Kantenzugabe - 0.32, f.Raum(5, 11, 0, 1, 0.32f, 1.5f), 0.06); // zur Kante des Pflasters (mit Zugabe)
        }),
        ("Marktfläche: mit Spur, Schwanken und gerundeten Ecken bleibt jeder Körper aus Ständen und Brunnen (seitliche Lage begrenzt wie in der Darstellung)", () =>
        {
            var f = Testplatz();
            const float radius = 0.32f; // der breiteste Körper (Oger)
            var wege = new[] { ((-11.5f, 0f), (11.5f, 0.5f)), ((-11f, -11f), (0.15f, -7.55f)), ((11f, -11.5f), (-9f, 3.5f)), ((6f, 11f), (-4f, -6.6f)) };
            var (geprueft, schlechteste) = (0, float.MaxValue);
            foreach (var (von, nach) in wege)
                for (var nummer = 0; nummer < 30; nummer++)
                {
                    var weg = f.Gefaechert(von, nach, Frei, (nummer % 7 - 3) / 3f) ?? throw new Exception($"kein Weg {von} → {nach}");
                    var linie = new Passantenspur.Polylinie(weg, Enumerable.Repeat(1.0f, weg.Count - 1).ToArray()); // Pflaster: halbe Breite 1 m wie in der Darstellung
                    for (var strecke = 0f; strecke <= linie.Laenge; strecke += 0.05f)
                    {
                        var gang = Passantenspur.Gehen(linie, nummer, linie.Laenge, strecke, (0, 0), false);
                        var (cx, cz) = (gang.Ort.X - gang.Versatz.X, gang.Ort.Z - gang.Versatz.Z);
                        var quer = gang.Versatz.X * gang.Rechts.X + gang.Versatz.Z * gang.Rechts.Z;
                        var (links, rechts) = f.Seitlich(cx, cz, gang.Rechts.X, gang.Rechts.Z, radius + 0.1f, 2.5f);
                        quer = Math.Clamp(quer, MathF.Min(links, 0), MathF.Max(rechts, 0));
                        var (x, z) = (cx + gang.Rechts.X * quer, cz + gang.Rechts.Z * quer);
                        // ein Ende, das selbst näher dran ist (Kundenplatz), darf so nah sein wie es selbst
                        var noetig = MathF.Min(radius, f.Abstand(cx, cz) - 0.01f);
                        var abstand = f.Abstand(x, z);
                        schlechteste = MathF.Min(schlechteste, abstand - noetig);
                        Pruefe.Wahr(abstand >= noetig - 0.02f, $"Nummer {nummer}, {von} → {nach}, {strecke:0.00} m: Körper {abstand - radius:0.00} m im Hindernis");
                        geprueft++;
                    }
                }
            Pruefe.Wahr(geprueft > 5000, $"nur {geprueft} Lagen geprüft");
        }),
        ("Marktfläche: gemerkte Wege – gleich wie neu gesucht, genau die verlangten Enden, Seiten auf fünf Stufen", () =>
        {
            var f = Testplatz();
            var (von, nach) = ((-6f, -5f), (-6f, 5f));
            var neu = f.Gefaechert(von, nach, Frei, 0.5f)!;
            var gemerkt = f.GefaechertGemerkt(von, nach, Frei, 0.45f)!;
            Pruefe.Gleich(neu.Count, gemerkt.Count);
            for (var i = 0; i < neu.Count; i++) Pruefe.Wahr(MathF.Abs(neu[i].X - gemerkt[i].X) + MathF.Abs(neu[i].Z - gemerkt[i].Z) < 1e-5f, $"Punkt {i} anders");
            Pruefe.Gleich(1, f.Gemerkt);
            var nochmal = f.GefaechertGemerkt((-6.01f, -5.01f), nach, Frei, 0.55f)!;
            Pruefe.Gleich(1, f.Gemerkt);                       // derselbe Eintrag (5 cm, gleiche Stufe)
            Pruefe.Gleich((-6.01f, -5.01f), nochmal[0]);       // aber mit dem verlangten Anfang
            nochmal[1] = (0, 0);                                // eine Kopie: der Speicher bleibt unberührt
            Pruefe.Wahr(f.GefaechertGemerkt(von, nach, Frei, 0.5f)![1] != (0, 0), "Speicher verändert");
        }),
        ("Marktfläche: Kosten – eine Suche fasst höchstens einen Teil des Rasters an", () =>
        {
            var f = Testplatz();
            var weg = f.Pfad((-11.5f, -11.5f), (11.5f, 11.5f), Frei);
            Pruefe.Wahr(weg is not null, "Weg diagonal");
            Pruefe.Wahr(f.Besucht < 96 * 96 / 2, $"{f.Besucht} Zellen besucht");
        }),
        ("Marktfläche: kein Weg ins Eingeschlossene, aber ein Ende im Hindernis (Bank) geht", () =>
        {
            var f = Testplatz();
            var bank = (10.6f, 10f);
            var weg = f.Pfad((0, 5), bank, Frei);
            Pruefe.Wahr(weg is not null && weg[^1] == bank, "zur Bank");
            var zu = new Marktflaeche(0, 0, 1, 0, 5);
            zu.Kreis(0, 0, 3.5f);
            foreach (var (x, z) in new[] { (4.7f, 0f), (-4.7f, 0f), (0f, 4.7f), (0f, -4.7f) }) zu.Kreis(x, z, 2.5f);
            Pruefe.Wahr(zu.Pfad((-4.9f, 4.9f), (4.9f, -4.9f), 1.1f) is null, "alles zugestellt: kein Weg");
        }),
    };

    static float Naechster(IEnumerable<Marktflaeche.Rechteck> rechtecke, List<(float X, float Z)> weg)
    {
        var naechster = float.MaxValue;
        for (var i = 1; i < weg.Count; i++)
            for (var t = 0f; t <= 1; t += 0.01f)
            {
                var (x, z) = (weg[i - 1].X + (weg[i].X - weg[i - 1].X) * t, weg[i - 1].Z + (weg[i].Z - weg[i - 1].Z) * t);
                foreach (var r in rechtecke) naechster = MathF.Min(naechster, r.Abstand(x, z));
            }
        return naechster;
    }
}
