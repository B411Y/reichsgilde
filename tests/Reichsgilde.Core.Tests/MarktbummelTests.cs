using Reichsgilde;

/// <summary>
/// Wie die Leute ihre Zeit auf dem Markt verbringen (Darstellung, Markt v3 vom 1.11.2026; Modell in <c>game/scripts/Marktbummel.cs</c>, hier mitübersetzt):
/// verschiedene Ziele in den gewünschten Anteilen, verschieden lange Pausen ohne Gleichtakt, nur wenige gehen um den Brunnen und nicht alle in dieselbe
/// Richtung, geredet wird erst zu zweit.
/// </summary>
static class MarktbummelTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Marktbummel: Ziele in den Anteilen (Stand, Gespräch, Brunnen, Rand, Bank, Schauen)", () =>
        {
            Pruefe.Ungefaehr(1, Marktbummel.Anteile.Sum(), 1e-5);
            var zahl = new int[Marktbummel.Anteile.Length];
            const int n = 6000;
            for (var i = 0; i < n; i++) zahl[(int)Marktbummel.Waehlen(i, i % 5, 400 + i % 13)]++;
            for (var a = 0; a < zahl.Length; a++)
                Pruefe.Wahr(MathF.Abs(zahl[a] / (float)n - Marktbummel.Anteile[a]) < 0.025f, $"{(Marktbummel.Art)a}: {zahl[a] / (float)n:0.000} statt {Marktbummel.Anteile[a]}");
            // dieselbe Person wählt beim nächsten Ziel nicht immer dasselbe
            var folge = Enumerable.Range(0, 12).Select(z => Marktbummel.Waehlen(17, z, 600)).Distinct().Count();
            Pruefe.Wahr(folge >= 3, $"nur {folge} verschiedene Ziele in zwölf Schritten");
        }),
        ("Marktbummel: Ausweichliste beginnt mit der gewählten Art und enthält jede genau einmal", () =>
        {
            foreach (var art in Enum.GetValues<Marktbummel.Art>())
            {
                var liste = Marktbummel.Reihenfolge(art).ToList();
                Pruefe.Gleich(art, liste[0]);
                Pruefe.Gleich(Enum.GetValues<Marktbummel.Art>().Length, liste.Distinct().Count());
                Pruefe.Gleich(liste.Count, liste.Distinct().Count());
            }
        }),
        ("Marktbummel: Pausen in ihren Grenzen, verschieden lang, kein Gleichtakt", () =>
        {
            foreach (var art in Enum.GetValues<Marktbummel.Art>())
            {
                var (von, bis) = Marktbummel.Pause(art);
                var pausen = Enumerable.Range(0, 300).Select(i => Marktbummel.Verweilen(art, i, 1, 420)).ToList();
                Pruefe.Wahr(pausen.All(x => x >= von && x <= bis), $"{art}: außerhalb {von}–{bis}");
                // fast alle verschieden (auf eine Zehntelminute) und gut gestreut
                Pruefe.Wahr(pausen.Select(x => Math.Round(x, 3)).Distinct().Count() > 250, $"{art}: zu viele gleiche Pausen");
                var mittel = pausen.Average();
                var streuung = Math.Sqrt(pausen.Average(x => (x - mittel) * (x - mittel)));
                Pruefe.Wahr(streuung > (bis - von) * 0.2, $"{art}: Streuung {streuung:0.0} min");
                Pruefe.Wahr(mittel < (von + bis) / 2, $"{art}: kurze Pausen sollen häufiger sein (Mittel {mittel:0.0})");
            }
            // dieselbe Person, nächstes Ziel: andere Pause
            Pruefe.Wahr(Marktbummel.Verweilen(Marktbummel.Art.Stand, 5, 1, 400) != Marktbummel.Verweilen(Marktbummel.Art.Stand, 5, 2, 400), "Pause hängt vom Ziel ab");
        }),
        ("Marktbummel: nur wenige gehen um den Brunnen, in beide Richtungen, auf einem Bogen außerhalb des Beckens", () =>
        {
            var (kreisen, plus, wege) = (0, 0, 0);
            for (var i = 0; i < 4000; i++)
            {
                var art = Marktbummel.Waehlen(i, 2, 500);
                wege++;
                if (!Marktbummel.Kreist(art, i, 2, 500)) continue;
                kreisen++;
                var (richtung, spanne, abstand) = Marktbummel.Kreisbogen(i, 2, 500);
                if (richtung > 0) plus++;
                Pruefe.Wahr(spanne >= 100 * MathF.PI / 180 - 1e-4 && spanne <= 250 * MathF.PI / 180 + 1e-4, $"Spanne {spanne}");
                Pruefe.Wahr(abstand is >= 1.8f and <= 3.0f, $"Abstand {abstand}");
            }
            var anteil = kreisen / (float)wege;
            Pruefe.Wahr(anteil is > 0.04f and < 0.15f, $"Anteil der Wege mit Kreisen {anteil:0.000}");
            Pruefe.Wahr(MathF.Abs(plus / (float)kreisen - 0.5f) < 0.1f, $"Richtungen ungleich: {plus} von {kreisen} in eine Richtung");
            Pruefe.Wahr(!Marktbummel.Kreist(Marktbummel.Art.Stand, 1, 1, 1) && !Marktbummel.Kreist(Marktbummel.Art.Gruppe, 1, 1, 1), "zum Stand und ins Gespräch ohne Kreisen");
        }),
        ("Marktbummel: Bogen liegt außen am Kreis, die Sehnen schneiden ihn nicht, Richtung stimmt", () =>
        {
            foreach (var richtung in new[] { 1, -1 })
            {
                var bogen = Marktbummel.Bogen(5, -3, 3.5f, 0.3f, 2.5f, richtung);
                var r0 = MathF.Sqrt((bogen[0].X - 5) * (bogen[0].X - 5) + (bogen[0].Z + 3) * (bogen[0].Z + 3));
                Pruefe.Wahr(r0 >= 3.5f - 1e-4f && r0 < 3.6f, $"Anfang im Abstand {r0}");
                for (var i = 1; i < bogen.Count; i++)
                {
                    var (mx, mz) = ((bogen[i].X + bogen[i - 1].X) / 2 - 5, (bogen[i].Z + bogen[i - 1].Z) / 2 + 3);
                    Pruefe.Wahr(MathF.Sqrt(mx * mx + mz * mz) >= 3.5f - 1e-3f, "Sehne schneidet den Kreis");
                    var (ax, az) = (bogen[i - 1].X - 5, bogen[i - 1].Z + 3);
                    var (bx, bz) = (bogen[i].X - 5, bogen[i].Z + 3);
                    Pruefe.Wahr(MathF.Sign(ax * bz - az * bx) == richtung, "falsche Richtung");
                }
                var ende = MathF.Atan2(bogen[^1].Z + 3, bogen[^1].X - 5);
                Pruefe.Ungefaehr(0, MathF.Sin(ende - (0.3f + richtung * 2.5f)), 1e-3);
            }
        }),
        ("Marktbummel: geredet wird nur zu zweit; Gruppen füllen sich gegenüber zuerst", () =>
        {
            Pruefe.Wahr(!Marktbummel.Redet(0), "allein redet keiner");
            Pruefe.Wahr(Marktbummel.Redet(1) && Marktbummel.Redet(3), "zu zweit und mehr");
            Pruefe.Gleich(4, Marktbummel.Gruppenfolge.Length);
            Pruefe.Gleich(2, Math.Abs(Marktbummel.Gruppenfolge[0] - Marktbummel.Gruppenfolge[1])); // gegenüber (zwei Viertel)
            Pruefe.Gleich(4, Marktbummel.Gruppenfolge.Distinct().Count());
            var abstand = Marktbummel.GruppeRadius * 2;
            Pruefe.Wahr(abstand > 0.9f && abstand < 1.6f, "natürlicher Abstand im Gespräch");
        }),
        ("Marktbummel: Umschauen weich und begrenzt, je Person anders", () =>
        {
            var (groesste, sprung) = (0f, 0f);
            for (var t = 0.0; t < 60; t += 1 / 30.0)
            {
                var w = Marktbummel.Umschauen(3, t);
                groesste = MathF.Max(groesste, MathF.Abs(w));
                sprung = MathF.Max(sprung, MathF.Abs(w - Marktbummel.Umschauen(3, t - 1 / 30.0)));
            }
            Pruefe.Wahr(groesste <= 0.9f + 1e-4f && groesste > 0.4f, $"größter Winkel {groesste}");
            Pruefe.Wahr(sprung < 0.03f, $"ruckt um {sprung} je Bild");
            Pruefe.Wahr(MathF.Abs(Marktbummel.Umschauen(3, 10) - Marktbummel.Umschauen(4, 10)) > 1e-3f, "zwei Personen gleich");
        }),
        ("Marktbummel: Tempo auf dem Markt meist schlendernd, manchmal zielstrebig; Seite der Wege gestreut", () =>
        {
            var tempi = Enumerable.Range(0, 1000).Select(i => Marktbummel.Tempoanteil(i, 1, 300)).ToList();
            Pruefe.Wahr(tempi.All(x => x is >= 0.65f and <= 1f), "Tempo außerhalb");
            var zuegig = tempi.Count(x => x >= 0.9f) / 1000f;
            Pruefe.Wahr(zuegig is > 0.18f and < 0.32f, $"zielstrebig {zuegig:0.00}");
            var seiten = Enumerable.Range(0, 1000).Select(i => Marktbummel.Seite(i, 1, 300)).ToList();
            Pruefe.Wahr(seiten.Min() < -0.9f && seiten.Max() > 0.9f && MathF.Abs(seiten.Average()) < 0.08f, "Seiten nicht gestreut");
        }),
    };
}
