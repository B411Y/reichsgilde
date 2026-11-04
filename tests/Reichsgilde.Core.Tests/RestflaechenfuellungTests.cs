using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;

/// <summary>
/// Füllung der Restflächen (Restflaechenfuellung.cs, 4.11.2026): Felder aus dem Umriss, lückenlose und feste Zerlegung in Formate, Nachbarart und fester Würfel.
/// </summary>
static class RestflaechenfuellungTests
{
    static IReadOnlyList<(int I, int J)> Rechteck(int i0, int j0, int b, int t) =>
        Enumerable.Range(0, t).SelectMany(dj => Enumerable.Range(0, b).Select(di => (i0 + di, j0 + dj))).ToList();

    /// <summary>Prüft: Die Kästen decken die Menge genau ab (kein Feld doppelt, keines fehlt, keines zu viel) und haben ein erlaubtes Format.</summary>
    static void Luckenlos(IReadOnlyList<(int I, int J)> felder, IReadOnlyList<Fuellkasten> kaesten, string was)
    {
        var belegt = new List<(int, int)>();
        foreach (var k in kaesten)
        {
            Pruefe.Wahr(Restflaechenfuellung.Formate.Contains((k.Breite, k.Tiefe)), $"{was}: Format {k.Breite}x{k.Tiefe} ist erlaubt");
            belegt.AddRange(Rechteck(k.I, k.J, k.Breite, k.Tiefe));
        }
        Pruefe.Gleich(felder.Count, belegt.Count);
        Pruefe.Gleich(belegt.Count, belegt.Distinct().Count());
        Pruefe.Wahr(belegt.All(b => felder.Contains(b)), $"{was}: nur Felder der Menge");
    }

    static IEnumerable<Restflaeche> AlleRestflaechen() =>
        new[] { "koeln", "greybox" }.SelectMany(k => Stadtkarte.AusDatei(Datenpfad.Karte(k)).Restflaechen ?? new List<Restflaeche>());

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Restflächenfüllung: Felder aus dem Umriss ergeben die Feldzahl der Karte (Köln und Greybox)", () =>
        {
            var n = 0;
            foreach (var r in AlleRestflaechen())
            {
                Pruefe.Gleich(r.Felder, Restflaechenfuellung.Felder(r).Count);
                n++;
            }
            Pruefe.Wahr(n >= 41, "Köln 30 und Greybox 11 Restflächen");
        }),

        ("Restflächenfüllung: jedes Rechteck bis 3×5 (auch gedreht) zerfällt lückenlos", () =>
        {
            for (var b = 1; b <= 5; b++)
                for (var t = 1; t <= 5; t++)
                {
                    var felder = Rechteck(-3, 7, b, t);
                    Luckenlos(felder, Restflaechenfuellung.Zerlegen(felder), $"{b}x{t}");
                }
        }),

        ("Restflächenfüllung: ein Rechteck, das ein Format ist, bleibt ein Kasten", () =>
        {
            foreach (var (b, t) in Restflaechenfuellung.Formate)
            {
                var k = Restflaechenfuellung.Zerlegen(Rechteck(4, 4, b, t));
                Pruefe.Gleich(1, k.Count);
                Pruefe.Gleich(new Fuellkasten(4, 4, b, t), k[0]);
            }
        }),

        ("Restflächenfüllung: beliebige Teilmengen von 3×5 zerfallen lückenlos und unabhängig von der Reihenfolge", () =>
        {
            var zustand = 12345u;
            uint Naechste() { zustand = unchecked(zustand * 1664525u + 1013904223u); return zustand >> 8; }
            var alle = Rechteck(0, 0, 5, 3);
            for (var lauf = 0; lauf < 300; lauf++)
            {
                var maske = Naechste();
                var teil = alle.Where((_, i) => (maske >> i & 1) == 1).ToList();
                var kaesten = Restflaechenfuellung.Zerlegen(teil);
                Luckenlos(teil, kaesten, $"Lauf {lauf}");
                var umgekehrt = Restflaechenfuellung.Zerlegen(Enumerable.Reverse(teil).ToList());
                Pruefe.Gleich(string.Join(";", kaesten), string.Join(";", umgekehrt));
            }
        }),

        ("Restflächenfüllung: alle Restflächen von Köln und Greybox zerfallen lückenlos", () =>
        {
            foreach (var r in AlleRestflaechen())
            {
                var felder = Restflaechenfuellung.Felder(r);
                var kaesten = Restflaechenfuellung.Zerlegen(felder);
                Luckenlos(felder, kaesten, r.Kennung);
                Pruefe.Gleich(string.Join(";", kaesten), string.Join(";", Restflaechenfuellung.Zerlegen(felder)));
            }
        }),

        ("Restflächenfüllung: ein L aus 5 Feldern wird 2×2 plus Einzelfeld (feste Reihenfolge)", () =>
        {
            var l = new (int, int)[] { (0, 0), (1, 0), (0, 1), (1, 1), (0, 2) };
            var k = Restflaechenfuellung.Zerlegen(l);
            Pruefe.Gleich(2, k.Count);
            Pruefe.Gleich(new Fuellkasten(0, 0, 2, 2), k[0]);
            Pruefe.Gleich(new Fuellkasten(0, 2, 1, 1), k[1]);
        }),

        ("Restflächenfüllung: der Würfel ist fest und die Wahl nach Gewicht folgt den Gewichten", () =>
        {
            Pruefe.Gleich(Restflaechenfuellung.Wuerfel("koeln", 1, 2), Restflaechenfuellung.Wuerfel("koeln", 1, 2));
            Pruefe.Wahr(Restflaechenfuellung.Wuerfel("koeln", 1, 2) != Restflaechenfuellung.Wuerfel("koeln", 2, 1), "andere Reihenfolge, anderer Wert");
            Pruefe.Gleich(-1, Restflaechenfuellung.Waehlen(new[] { 0, 0 }, 7));
            Pruefe.Gleich(1, Restflaechenfuellung.Waehlen(new[] { 0, 5 }, 123456));
            var zaehler = new int[3];
            for (var i = 0; i < 3000; i++) zaehler[Restflaechenfuellung.Waehlen(new[] { 70, 20, 10 }, Restflaechenfuellung.Wuerfel("t", i))]++;
            Pruefe.Wahr(zaehler[0] > 1900 && zaehler[0] < 2300 && zaehler[2] > 200 && zaehler[2] < 420, $"Verteilung {string.Join("/", zaehler)}");
            Pruefe.Wahr(Enumerable.Range(0, 100).All(n => Restflaechenfuellung.Zufall(77, n) is >= 0 and < 1), "Zufall in [0,1)");
        }),

        ("Restflächenfüllung: auf Gebäuden fällt die Füllung weg, auf den Restflächen von Köln und Greybox nicht", () =>
        {
            foreach (var kennung in new[] { "koeln", "greybox" })
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                var frei = Naturfreiraum.Aus(karte);
                foreach (var r in karte.Restflaechen ?? new())
                    foreach (var k in Restflaechenfuellung.Zerlegen(Restflaechenfuellung.Felder(r)))
                        Pruefe.Wahr(!Restflaechenfuellung.Bebaut(k, frei), $"{kennung} {r.Kennung}: keine Füllung unter Gebäuden");
                var haus = karte.Gebaeude.First(g => g.Art == Bausteinart.Wohnhausplatz || g.Art == Bausteinart.Einwohnerplatz);
                var (i, j) = ((int)Math.Floor(haus.Punkt[0] / Restflaechenfuellung.Raster), (int)Math.Floor(haus.Punkt[1] / Restflaechenfuellung.Raster));
                Pruefe.Wahr(Restflaechenfuellung.Bebaut(new Fuellkasten(i, j, 1, 1), frei), kennung + ": ein Kasten mitten im Haus ist bebaut");
            }
        }),

        ("Restflächenfüllung: die Rezepte (restflaechen.json) haben je Thema alle Formate, und die Nachbarn nennen nur bekannte Themen", () =>
        {
            var pfad = Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "restflaechen.json");
            var wurzel = JsonNode.Parse(File.ReadAllText(pfad))!;
            var themen = wurzel["themen"]!.AsObject().Select(t => t.Key).ToList();
            Pruefe.Wahr(themen.Count >= 3, "mindestens drei Themen");
            var formate = Restflaechenfuellung.Formate.Select(f => $"{Math.Min(f.Breite, f.Tiefe)}x{Math.Max(f.Breite, f.Tiefe)}").Distinct().OrderBy(f => f, StringComparer.Ordinal).ToList();
            foreach (var thema in themen)
            {
                var vorhanden = wurzel["themen"]![thema]!["formate"]!.AsObject().Select(f => f.Key).OrderBy(f => f, StringComparer.Ordinal).ToList();
                // die Rezepte decken die acht Grundformate ab (gedreht gilt gleich: 5x1 = 1x5)
                foreach (var f in formate) Pruefe.Wahr(vorhanden.Contains(f), $"{thema}: Rezept für {f}");
                foreach (var teil in wurzel["themen"]![thema]!["formate"]!.AsObject().SelectMany(f => f.Value!.AsArray()))
                    Pruefe.Wahr(wurzel["modelle"]![teil!["m"]!.GetValue<string>()] is not null, $"{thema}: Baukasten „{teil["m"]}“ ist in „modelle“ benannt");
            }
            foreach (var (art, gewichte) in wurzel["nachbarn"]!.AsObject())
                foreach (var (thema, _) in gewichte!.AsObject())
                    Pruefe.Wahr(themen.Contains(thema), $"Nachbarart {art}: Thema {thema} gibt es");
            foreach (var art in new[] { Restflaechenfuellung.Wohnen, Restflaechenfuellung.Werkstatt, Restflaechenfuellung.Kirche, Restflaechenfuellung.Rand, Restflaechenfuellung.Sonst })
                Pruefe.Wahr(wurzel["nachbarn"]![art] is not null, $"Gewichte für die Nachbarart {art}");
        }),

        ("Restflächenfüllung: die Nachbarart ist fest und kommt aus der erlaubten Menge (Köln und Greybox)", () =>
        {
            var erlaubt = new[] { Restflaechenfuellung.Wohnen, Restflaechenfuellung.Werkstatt, Restflaechenfuellung.Kirche, Restflaechenfuellung.Rand, Restflaechenfuellung.Sonst };
            foreach (var kennung in new[] { "koeln", "greybox" })
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                var arten = (karte.Restflaechen ?? new()).Select(r => Restflaechenfuellung.Nachbarart(r, karte)).ToList();
                Pruefe.Wahr(arten.All(a => erlaubt.Contains(a)), "nur bekannte Arten");
                Pruefe.Gleich(string.Join(",", arten), string.Join(",", (karte.Restflaechen ?? new()).Select(r => Restflaechenfuellung.Nachbarart(r, karte))));
                if (kennung == "koeln") Pruefe.Wahr(arten.Distinct().Count() >= 2, "Köln hat verschiedene Nachbarschaften: " + string.Join(",", arten));
            }
        }),
    };
}
