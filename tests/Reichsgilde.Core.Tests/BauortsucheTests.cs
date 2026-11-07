using Reichsgilde.Core.Bauen;

/// <summary>
/// Die schnelle Platzsuche (<see cref="Bauwesen.Platzsuche"/>, 07.11.2026): <see cref="Bauwesen.Bauorte"/> prüft in Köln je Abfrage etwa 6.000 Stellen;
/// die Rechtecke um Bauzonen und Wege ersparen das ganze Vieleck bzw. jede Strecke, wenn ein Punkt weit draußen liegt. Hier die Gegenprobe ohne
/// Rechtecke auf allen Karten des Spiels: dieselbe Zone, dieselbe Antwort „an der Straße“ – auf dem Raster, neben dem Raster und genau an den
/// Ecken und Kanten der Rechtecke.
/// </summary>
static class BauortsucheTests
{
    static readonly string[] Karten = { "greybox", "probestadt", "messstadt", "koeln" };
    static readonly double[] Fein = { -1e-9, 0, 1e-9 };

    /// <summary>Punkte im Umkreis der Bauzonen (30 m Rand, wie Bauorte sie prüft) auf einem Raster, das Feldmitten und Feldränder trifft.</summary>
    static IEnumerable<(double X, double Z)> Raster(Stadtkarte karte, double schritt)
    {
        var ecken = karte.Bauzonen.SelectMany(z => z.Flaeche).ToList();
        for (var x = Math.Floor(ecken.Min(p => p[0])) - 31; x <= ecken.Max(p => p[0]) + 31; x += schritt)
            for (var z = Math.Floor(ecken.Min(p => p[1])) - 31; z <= ecken.Max(p => p[1]) + 31; z += schritt)
                yield return (x, z);
    }

    /// <summary>Die Kanten und Ecken eines Rechtecks samt Rand, je genau darauf, knapp davor und knapp dahinter.</summary>
    static IEnumerable<(double X, double Z)> AmRahmen((double X0, double Z0, double X1, double Z1) r, params double[] raender)
    {
        var (mx, mz) = ((r.X0 + r.X1) / 2, (r.Z0 + r.Z1) / 2);
        foreach (var rand in raender)
            foreach (var d in Fein)
            {
                foreach (var x in new[] { r.X0 - rand + d, r.X1 + rand + d })
                    foreach (var z in new[] { r.Z0 - rand + d, mz, r.Z1 + rand + d })
                        yield return (x, z);
                foreach (var z in new[] { r.Z0 - rand + d, r.Z1 + rand + d })
                    yield return (mx, z);
            }
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Bauortsuche: Rechtecke um Bauzonen und Wege ändern nichts – dieselbe Zone und dieselbe Antwort „an der Straße“ wie ohne, auf allen Karten", () =>
        {
            const double abstand = 7; // bauen.strassenAbstand in wirtschaft.json
            foreach (var kennung in Karten)
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                var suche = new Bauwesen.Platzsuche(karte);

                // Zonen: jede fünfte Feldmitte, jede Ecke einer Zone samt knapp darüber und darunter (in z liegt die Grenze der Strahlprüfung genau
                // auf den Ecken) und die Ränder ihrer Rechtecke
                var punkte = Raster(karte, 10).Concat(karte.Bauzonen.SelectMany(z => z.Flaeche).SelectMany(p => Fein.Select(dz => (p[0], p[1] + dz))))
                    .Concat(karte.Bauzonen.SelectMany(z => AmRahmen(Bauwesen.Platzsuche.Rahmen(z.Flaeche), 0, 1)));
                var inZone = 0;
                foreach (var (x, z) in punkte)
                {
                    var ohne = karte.Bauzonen.FirstOrDefault(b => b.Enthaelt(x, z));
                    var mit = suche.ZoneBei(x, z);
                    Pruefe.Wahr(ReferenceEquals(ohne, mit), $"{kennung} ({x} | {z}): Zone {ohne?.Kennung ?? "keine"} ohne Rechtecke, {mit?.Kennung ?? "keine"} mit");
                    if (ohne is not null) inZone++;
                }

                // Straßen: ein gröberes Raster (Köln hat 5.000 Wegpunkte) und die Ränder der Rechtecke um jeden Weg
                var anStrasse = 0;
                foreach (var (x, z) in Raster(karte, 14).Concat(karte.Wege.SelectMany(w => AmRahmen(Bauwesen.Platzsuche.Rahmen(w.Verlauf), 0, abstand, abstand + 1))))
                {
                    var nah = karte.Wege.Where(w => w.Abstand(x, z) <= abstand).ToList();
                    foreach (var werkstatt in new[] { true, false })
                    {
                        var ohne = nah.Any(w => w.IstKarrenstrasse || !werkstatt && w.Klasse == Wegklasse.Gasse);
                        Pruefe.Gleich(ohne, suche.AnDerStrasse(x, z, abstand, werkstatt), $"{kennung} ({x} | {z}), Werkstatt {werkstatt}: an der Straße");
                        if (ohne) anStrasse++;
                    }
                }
                Pruefe.Wahr(inZone > 100 && anStrasse > 100, $"{kennung}: zu wenige Punkte in Zonen ({inZone}) oder an Straßen ({anStrasse})");
            }
        }),
    };
}
