using System.Diagnostics;
using Reichsgilde;

/// <summary>
/// Das vorbereitete Wegenetz der Marktfläche (Darstellung, 5.11.2026; <c>Marktflaeche.NetzPfad</c>): Wege über den Platz werden aus fertigen Kanten
/// zusammengesetzt statt auf dem Raster gesucht – gleich frei, ähnlich lang, viel billiger; ein neuer Stand baut das Netz neu.
/// </summary>
static class MarktnetzTests
{
    const float Frei = 1.1f;

    static Marktflaeche Platz()
    {
        var f = new Marktflaeche(0, 0, 1, 0, 12);
        f.Kreis(0, 0, 1.98f);
        foreach (var (u, v) in new[] { (-8f, -8f), (0f, -9f), (8f, -8f), (-9f, 2f), (9f, 2f), (-4f, 8f), (4f, 8f) })
        {
            var l = MathF.Sqrt(u * u + v * v);
            f.Stand(new Marktflaeche.Rechteck(u, v, v / l, -u / l, 1.45f, 0.78f));
        }
        f.Rechteckig(new Marktflaeche.Rechteck(10.6f, 10f, 1, 0, 1.25f, 0.26f));
        return f;
    }

    static float Naechster(Marktflaeche f, List<(float X, float Z)> weg)
    {
        var naechster = float.MaxValue;
        for (var i = 1; i < weg.Count; i++)
        {
            var (dx, dz) = (weg[i].X - weg[i - 1].X, weg[i].Z - weg[i - 1].Z);
            var l = MathF.Sqrt(dx * dx + dz * dz);
            for (var t = 0f; t <= l; t += 0.05f)
                naechster = MathF.Min(naechster, f.Abstand(weg[i - 1].X + dx * t / MathF.Max(l, 1e-6f), weg[i - 1].Z + dz * t / MathF.Max(l, 1e-6f)));
        }
        return naechster;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Marktnetz: Wege zwischen vielen Rändern bleiben frei, nicht viel länger als nötig, fast nie Rückfall auf das Raster", () =>
        {
            var f = Platz();
            f.Vorbereiten(Frei);
            Pruefe.Wahr(f.Knotenzahl(Frei) > 50, $"zu wenige Knoten: {f.Knotenzahl(Frei)}");
            var rand = new[] { -11.4f, -6f, -2f, 3f, 7f };
            var paare = 0;
            foreach (var a in rand) foreach (var b in rand)
                foreach (var (von, nach) in new[] { ((a, -11.4f), (b, 11.4f)), ((-11.4f, a), (11.4f, b)), ((a, 11.4f), (11.4f, b)) })
                {
                    var weg = f.Pfad(von, nach, Frei);
                    Pruefe.Wahr(weg is not null, $"kein Weg {von} → {nach}");
                    paare++;
                    Pruefe.Gleich(von, weg![0]);
                    Pruefe.Gleich(nach, weg[^1]);
                    var gerade = MathF.Sqrt((von.Item1 - nach.Item1) * (von.Item1 - nach.Item1) + (von.Item2 - nach.Item2) * (von.Item2 - nach.Item2));
                    Pruefe.Wahr(Marktflaeche.Laenge(weg) <= gerade * 2.5f + 1f, $"Weg {von} → {nach} zu lang: {Marktflaeche.Laenge(weg):0.0} statt {gerade:0.0}");
                    Pruefe.Wahr(Naechster(f, weg) >= Frei - 0.12f || Naechster(f, weg) >= MathF.Min(f.Frei(von.Item1, von.Item2), f.Frei(nach.Item1, nach.Item2)) - 0.12f,
                        $"Weg {von} → {nach} kommt einem Hindernis auf {Naechster(f, weg):0.00} m nah");
                }
            Pruefe.Wahr(f.Netzwege > 0, "kein Weg lief über das Netz");
            Pruefe.Wahr(f.NetzRueckfaelle * 10 <= paare, $"zu oft Rückfall auf das Raster: {f.NetzRueckfaelle} von {paare}");
        }),
        ("Marktnetz: ein neuer Stand baut das Netz neu – kein Weg mehr durch ihn", () =>
        {
            var f = Platz();
            f.Vorbereiten(Frei);
            var vorher = f.Pfad((-11, 4), (11, 4), Frei)!;
            f.Stand(new Marktflaeche.Rechteck(0, 4, 1, 0, 2.5f, 0.8f));
            var nachher = f.Pfad((-11, 4), (11, 4), Frei)!;
            Pruefe.Wahr(Naechster(f, nachher) >= Frei - 0.12f, $"der neue Stand wird gestreift: {Naechster(f, nachher):0.00}");
            Pruefe.Wahr(Marktflaeche.Laenge(nachher) > Marktflaeche.Laenge(vorher) - 0.01f, "der Weg ging nicht um den Stand");
        }),
        ("Marktnetz: Stehende im Weg – der Weg führt um sie herum, nicht durch sie, und das Netz wird nicht verwirrt", () =>
        {
            var f = Platz();
            f.Vorbereiten(Frei);
            var leer = f.Pfad((-11.4f, 4), (11.4f, 4), Frei)!;
            // eine Gruppe mitten auf dem Weg, einer in der Gasse zwischen Stand und Brunnen
            var leute = new List<(float X, float Z)> { (-5, 4), (-4.3f, 4.3f), (-4.6f, 3.4f), (3, 4.5f), (0, 5.5f) };
            f.Personen(leute);
            var weg = f.Pfad((-11.4f, 4), (11.4f, 4), Frei);
            Pruefe.Wahr(weg is not null, "kein Weg mit Stehenden");
            for (var i = 1; i < weg!.Count; i++)
            {
                var (dx, dz) = (weg[i].X - weg[i - 1].X, weg[i].Z - weg[i - 1].Z);
                var l = MathF.Sqrt(dx * dx + dz * dz);
                foreach (var (px, pz) in leute)
                    for (var t = 0f; t <= l; t += 0.05f)
                    {
                        var (x, z) = (weg[i - 1].X + dx * t / l, weg[i - 1].Z + dz * t / l);
                        Pruefe.Wahr((x - px) * (x - px) + (z - pz) * (z - pz) >= 0.55f * 0.55f, $"der Weg geht durch jemanden bei ({px}|{pz})");
                    }
            }
            Pruefe.Wahr(Marktflaeche.Laenge(weg) > Marktflaeche.Laenge(leer) - 0.01f, "mit Stehenden nicht länger?");
            f.Personen(new List<(float X, float Z)>());
            var wieder = f.Pfad((-11.4f, 4), (11.4f, 4), Frei)!;
            Pruefe.Ungefaehr(Marktflaeche.Laenge(leer), Marktflaeche.Laenge(wieder), 0.01);
        }),
        ("Marktnetz: ein Weg kostet nur einen Bruchteil einer Rastersuche (Messung auf der Konsole)", () =>
        {
            var f = Platz();
            f.Vorbereiten(Frei);
            var alt = Platz(); // ohne Netz: Rastersuche mit Abstand 0,9 (unter NetzAb)
            var punkte = new[] { (-11.4f, -11.4f), (11.4f, 11.4f), (-11.4f, 11.4f), (11.4f, -11.4f), (0f, -11.4f), (0f, 11.4f), (-11.4f, 0f), (11.4f, 0f) };
            var (uhrNetz, uhrRaster) = (Stopwatch.StartNew(), new Stopwatch());
            var n = 0;
            for (var wdh = 0; wdh < 3; wdh++)
                foreach (var a in punkte) foreach (var b in punkte)
                {
                    if (a == b) continue;
                    uhrNetz.Start();
                    f.Pfad(a, (b.Item1 + wdh * 0.3f, b.Item2), Frei); // jedes Mal ein anderes Ende: nicht gemerkt
                    uhrNetz.Stop();
                    uhrRaster.Start();
                    alt.Pfad(a, (b.Item1 + wdh * 0.3f, b.Item2), 0.9f);
                    uhrRaster.Stop();
                    n++;
                }
            Console.WriteLine($"    Marktnetz: {n} Wege, Netz {uhrNetz.Elapsed.TotalMilliseconds / n:0.000} ms je Weg, Raster {uhrRaster.Elapsed.TotalMilliseconds / n:0.000} ms je Weg, Rückfälle {f.NetzRueckfaelle}");
            Pruefe.Wahr(uhrNetz.Elapsed < uhrRaster.Elapsed, "das Netz ist nicht schneller als das Raster");
        }),
    };
}
