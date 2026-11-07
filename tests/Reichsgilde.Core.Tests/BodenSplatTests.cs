using Reichsgilde.Core.Bauen;

/// <summary>Bodenraster.Splat (schnelle Fassung für den Ladevorgang): liefert genau, was Gewicht Punkt für Punkt liefert, auch über Kachelränder und Lücken hinweg.</summary>
static class BodenSplatTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Splat: gleich Gewicht je Punkt und Textur, über Kachelränder, leere Kacheln und negative Punkte", () =>
        {
            var r = new Bodenraster();
            var zufall = new Random(7); // nur im Test, nicht im Kern
            var werte = new byte[11];
            // zwei benachbarte Kacheln gemalt (um den Ursprung), eine weit weg, dazwischen Lücken
            foreach (var (x0, z0, x1, z1) in new[] { (-70, -70, 70, 70), (300, 300, 330, 330) })
                for (var j = z0; j < z1; j += 3)
                    for (var i = x0; i < x1; i += 3)
                    {
                        Array.Clear(werte);
                        var rest = 255;
                        for (var c = 0; c < 11; c++)
                        {
                            if (zufall.Next(3) != 0) continue;
                            var w = c == 10 ? zufall.Next(256) : zufall.Next(rest + 1);
                            if (c < 10) rest -= w;
                            werte[c] = (byte)w;
                        }
                        r.WerteSetzen(i, j, werte);
                    }
            foreach (var (i0, j0, b, t) in new[] { (-80, -80, 130, 130), (50, 50, 40, 30), (60, -5, 20, 3), (200, 200, 50, 50), (280, 290, 70, 50), (-130, -2, 10, 10) })
                for (var gruppe = 0; gruppe < 3; gruppe++)
                {
                    var splat = r.Splat(i0, j0, b, t, gruppe);
                    Pruefe.Gleich(b * t * 4, splat.Length);
                    for (var j = 0; j < t; j++)
                        for (var i = 0; i < b; i++)
                            for (var c = 0; c < 4; c++)
                            {
                                var textur = gruppe * 4 + c;
                                var soll = textur < Bodenraster.Texturen ? r.Gewicht(i0 + i, j0 + j, (Bodentextur)textur) : (byte)0;
                                if (splat[(j * b + i) * 4 + c] != soll) Pruefe.Wahr(false, $"Splat ({i0},{j0}) Gruppe {gruppe} Punkt {i},{j} Kanal {c}: {splat[(j * b + i) * 4 + c]} statt {soll}");
                            }
                }
        }),
    };
}
