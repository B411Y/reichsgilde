using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;

/// <summary>
/// Rasen in den Restflächen (Darstellung, 08.11.2026): welche Themen Gras als Grund haben (restflaechen.json, „rasen“), Einzug und Saum des Bodenstempels,
/// der ausgetretene Pfad bleibt in der Fläche.
/// </summary>
static class RasenTests
{
    static JsonObject Datei() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "restflaechen.json")))!.AsObject();

    static IReadOnlyList<IReadOnlyList<(double X, double Z)>> Quadrat(double x0, double z0, double x1, double z1) =>
        new IReadOnlyList<(double, double)>[] { new[] { (x0, z0), (x1, z0), (x1, z1), (x0, z1) } };

    static IReadOnlyList<(int I, int J)> Rechteck(int i0, int j0, int b, int t) =>
        Enumerable.Range(0, t).SelectMany(dj => Enumerable.Range(0, b).Select(di => (i0 + di, j0 + dj))).ToList();

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Rasen: Garten, Gestrüpp, Kirchhof, Bildstock und Heuschober haben Gras als Grund; Hof, Pferch, Werkhof und Steinmetzplatz bleiben Erde", () =>
        {
            var wurzel = Datei();
            var rasen = wurzel["rasen"]!.AsObject();
            var themen = rasen["themen"]!.AsObject();
            foreach (var t in new[] { "garten", "randgestruepp", "kirchhof", "bildstock", "heuschober" })
                Pruefe.Wahr(themen.ContainsKey(t), $"{t} hat Rasen");
            foreach (var t in new[] { "hof", "pferch", "werkhof", "steinmetzplatz", "holzplatz", "wasserstelle" })
            {
                Pruefe.Wahr(!themen.ContainsKey(t), $"{t} hat keinen Rasen");
                // Erde bleibt: jede Variante des Untergrunds stempelt kräftig (Erde, Kies, Schlamm, Mist oder Pflaster ab 0,75)
                foreach (var v in wurzel["untergruende"]![t]!.AsArray())
                    Pruefe.Wahr(v!.AsArray().Any(e => e!["staerke"]!.GetValue<double>() >= 0.75), $"{t}: Untergrund kräftig");
            }
            var bekannt = new HashSet<string> { "saum", "pfad", "beet", "baum", "busch" };
            foreach (var (thema, teile) in themen)
            {
                Pruefe.Wahr(wurzel["themen"]!.AsObject().ContainsKey(thema), $"{thema} ist ein Thema der Füllung");
                foreach (var teil in teile!.AsArray().Select(t => t!.GetValue<string>()))
                {
                    Pruefe.Wahr(bekannt.Contains(teil), $"{thema}: {teil} ist bekannt");
                    Pruefe.Wahr(rasen[teil] is JsonObject o && Bodenraster.TexturVon(o["textur"]?.GetValue<string>()) is not null, $"{teil} hat eine Bodenart");
                }
                // kein Teppich aus Erde: Ein Untergrund auf Rasen ist schwach oder fleckig
                if (wurzel["untergruende"]![thema] is JsonArray varianten)
                    foreach (var e in varianten.SelectMany(v => v!.AsArray()))
                        Pruefe.Wahr(e!["staerke"]!.GetValue<double>() <= 0.6 || (e["fleck"]?.GetValue<double>() ?? 0) >= 0.9, $"{thema}: Untergrund schwach oder fleckig");
            }
            // Beete gibt es nur, wo die Rezepte Beete (art raster) haben
            foreach (var (thema, teile) in themen)
                if (teile!.AsArray().Any(t => t!.GetValue<string>() == "beet"))
                    Pruefe.Wahr(wurzel["themen"]![thema]!["formate"]!.AsObject().Any(f => f.Value!.AsArray().Any(t => t!["art"]?.GetValue<string>() == "raster")), $"{thema} hat Beete");
            var maske = rasen["maske"]!;
            Pruefe.Wahr(maske["einzug"]!.GetValue<double>() >= maske["rand"]!.GetValue<double>() / 2 + maske["rauschen"]!.GetValue<double>() - 1e-9,
                "der weiche Rand der Maske bleibt in der Fläche (Gassen daneben behalten ihr Pflaster)");
        }),

        ("Rasen: Einzug rückt den Stempel samt weichem Rand in die Fläche, Saum stempelt nur innen am Rand", () =>
        {
            var ohne = new Bodenraster();
            ohne.Stempeln(Quadrat(0, 0, 20, 20), new Bodenstempel(Bodentextur.Erde, 1, 1, 0, 0, 1));
            Pruefe.Wahr(ohne.Gewicht(0, 10, Bodentextur.Erde) is > 60 and < 200, "ohne Einzug: auf dem Umriss halb");
            var ein = new Bodenraster();
            ein.Stempeln(Quadrat(0, 0, 20, 20), new Bodenstempel(Bodentextur.Erde, 1, 1, 0.2, 0, 1, Einzug: 0.8));
            for (var j = -2; j <= 22; j++)
            {
                Pruefe.Gleich(0, (int)ein.Gewicht(0, j, Bodentextur.Erde));
                Pruefe.Gleich(0, (int)ein.Gewicht(20, j, Bodentextur.Erde));
            }
            Pruefe.Gleich(255, (int)ein.Gewicht(10, 10, Bodentextur.Erde));
            var saum = new Bodenraster();
            saum.Stempeln(Quadrat(0, 0, 20, 20), new Bodenstempel(Bodentextur.Erde, 1, 0.6, 0, 0, 1, Saum: 1));
            Pruefe.Gleich(0, (int)saum.Gewicht(10, 10, Bodentextur.Erde));
            Pruefe.Gleich(0, (int)saum.Gewicht(5, 10, Bodentextur.Erde));
            Pruefe.Wahr(saum.Gewicht(0, 10, Bodentextur.Erde) > 60, "am Rand Erde");
        }),

        ("Rasen: Ein großer Einzug oder Saum (tiefer als 4 m) stempelt überall gleich – keine Stufe an der Suchzelle", () =>
        {
            // Quadrat 20,1 … 40,1 (nicht auf dem Raster): Mitte 30 m, 10 m tief
            var ein = new Bodenraster();
            ein.Stempeln(Quadrat(3.9, 3.9, 33.9, 33.9), new Bodenstempel(Bodentextur.Erde, 1, 1, 0, 0, 1, Einzug: 6));
            for (var i = 5; i <= 33; i++) // tiefer als 6 m innen: voll, flacher: nichts
            {
                var tiefe = Math.Min(Math.Min(i - 3.9, 33.9 - i), 15);
                Pruefe.Wahr(tiefe < 5.4 ? ein.Gewicht(i, 18, Bodentextur.Erde) == 0 : tiefe > 6.6 ? ein.Gewicht(i, 18, Bodentextur.Erde) == 255 : true, $"Einzug 6: Punkt {i} (Tiefe {tiefe:F1})");
            }
            var saum = new Bodenraster();
            saum.Stempeln(Quadrat(3.9, 3.9, 33.9, 33.9), new Bodenstempel(Bodentextur.Erde, 1, 1, 0, 0, 1, Saum: 4));
            var vorher = 999;
            for (var i = 5; i <= 19; i++) // (ab 1 m Tiefe, am Umriss selbst hält der weiche Rand die Deckung halb) nach innen nie wieder stärker: keine Stufe (vorher sprang es bei 4 m Tiefe von 224 auf 0)
            {
                var g = (int)saum.Gewicht(i, 18, Bodentextur.Erde);
                Pruefe.Wahr(g <= vorher, $"Saum 4: Punkt {i} nicht stärker als der davor ({g} > {vorher})");
                vorher = g;
            }
        }),

        ("Rasen: Der Pfad liegt in der Fläche, kommt von der Seite des Weges und ist fest", () =>
        {
            // 8 × 4 m, der Weg südlich (z = −5): Der Pfad läuft von Süd nach Nord quer durch
            var felder = Rechteck(0, 0, 4, 2);
            var weg = new IReadOnlyList<(double, double)>[] { new[] { (-10.0, -5.0), (20.0, -5.0) } };
            var pfad = Rasenflaeche.Pfad(felder, weg, 7, 0.8, 0.3, 24, 3);
            Pruefe.Wahr(pfad.Count > 0, "ein Pfad");
            var ecken = pfad.SelectMany(p => p).ToList();
            Pruefe.Wahr(ecken.Max(e => e.Z) - ecken.Min(e => e.Z) > 2, "von Süd nach Nord");
            Pruefe.Wahr(ecken.Max(e => e.X) - ecken.Min(e => e.X) < 3, "nicht entlang der langen Seite");
            Pruefe.Gleich(string.Join(";", ecken), string.Join(";", Rasenflaeche.Pfad(felder, weg, 7, 0.8, 0.3, 24, 3).SelectMany(p => p)));
            // ohne Weg: entlang der langen Seite; zu klein: kein Pfad
            var lang = Rasenflaeche.Pfad(felder, Array.Empty<IReadOnlyList<(double, double)>>(), 7, 0.8, 0.3, 24, 3).SelectMany(p => p).ToList();
            Pruefe.Wahr(lang.Max(e => e.X) - lang.Min(e => e.X) > 4, "ohne Weg entlang x");
            Pruefe.Gleich(0, Rasenflaeche.Pfad(Rechteck(0, 0, 2, 1), weg, 7, 0.8, 0.3, 24, 3).Count);
            // alle Restflächen von Köln und Greybox: jede Ecke mindestens den Einzug in der Fläche
            var n = 0;
            foreach (var k in new[] { "koeln", "greybox" })
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(k));
                var wege = karte.Wege.Select(w => (IReadOnlyList<(double X, double Z)>)w.Verlauf.Where(p => p is { Length: 2 }).Select(p => (p[0], p[1])).ToList()).ToList();
                foreach (var r in karte.Restflaechen ?? new List<Restflaeche>())
                {
                    var f = Restflaechenfuellung.Felder(r);
                    var menge = new HashSet<(int I, int J)>(f);
                    foreach (var e in Rasenflaeche.Pfad(f, wege, Restflaechenfuellung.Wuerfel(r.Kennung), 0.8, 0.3, 24, 3).SelectMany(p => p))
                    {
                        Pruefe.Wahr(Rasenflaeche.Innen(menge, e.X, e.Z, 0.3 - 1e-6), $"{k} {r.Kennung}: Ecke {e} in der Fläche");
                        n++;
                    }
                }
            }
            Pruefe.Wahr(n > 20, "Köln und Greybox haben Pfade");
        }),
    };
}
