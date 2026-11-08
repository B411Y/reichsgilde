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
            foreach (var (art, gewichte) in wurzel["erweiterung"]!["nachbarn"]!.AsObject())
                foreach (var (thema, _) in gewichte!.AsObject())
                    Pruefe.Wahr(themen.Contains(thema), $"Erweiterung {art}: Thema {thema} gibt es");
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

        ("Restflächenfüllung: Köln bekommt einige Steinmetzplätze und wenige Taubenschläge, und die alten Themen behalten ihre Flächen (Paket 3: 3 Höfe, 2 Pferche, 2 Wasserstellen, 5 Heuschober; die neuen Themen ersetzen nur Randgestrüpp)", () =>
        {
            var wurzel = JsonNode.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "restflaechen.json")))!;
            var grenzen = wurzel["hoechstens_felder"]!.AsObject().ToDictionary(e => e.Key, e => e.Value!.GetValue<int>());
            var ersetzt = wurzel["erweiterung"]!["ersetzt"]!.GetValue<string>();
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
            var mit = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var ohne = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in karte.Restflaechen ?? new List<Restflaeche>())
            {
                var art = Restflaechenfuellung.Nachbarart(r, karte);
                var wahl = wurzel["nachbarn"]![art]!.AsObject().OrderBy(e => e.Key, StringComparer.Ordinal).ToList(); // feste Reihenfolge wie im Spiel
                var erw = wurzel["erweiterung"]!["nachbarn"]![art]!.AsObject().OrderBy(e => e.Key, StringComparer.Ordinal).ToList();
                var felder = Restflaechenfuellung.Felder(r).Count;
                var kennung = karte.Kennung + "/" + r.Kennung;
                var namen = wahl.Select(e => e.Key).ToList();
                var gewichte = wahl.Select(e => e.Value!.GetValue<int>()).ToList();
                var alt = Restflaechenfuellung.ThemaWaehlen(kennung, felder, namen, gewichte, grenzen);
                var neu = Restflaechenfuellung.ThemaWaehlen(kennung, felder, namen, gewichte, grenzen, ersetzt, erw.Select(e => e.Key).ToList(), erw.Select(e => e.Value!.GetValue<int>()).ToList());
                if (alt is not null) ohne[alt] = ohne.GetValueOrDefault(alt) + 1;
                if (neu is not null) mit[neu] = mit.GetValueOrDefault(neu) + 1;
                Pruefe.Wahr(neu == alt || alt == ersetzt, $"{kennung}: nur Randgestrüpp wird ersetzt ({alt} → {neu})");
            }
            var uebersicht = string.Join(", ", mit.Select(e => $"{e.Key} {e.Value}"));
            Pruefe.Wahr(mit.GetValueOrDefault("steinmetzplatz") is >= 2 and <= 6, "Köln: einige Steinmetzplätze – " + uebersicht);
            Pruefe.Wahr(mit.GetValueOrDefault("taubenschlag") is >= 1 and <= 3, "Köln: wenige Taubenschläge – " + uebersicht);
            foreach (var (thema, zahl) in new[] { ("hof", 3), ("pferch", 2), ("wasserstelle", 2), ("heuschober", 5) })
                Pruefe.Gleich(zahl, mit.GetValueOrDefault(thema));
        }),

        ("Restflächenfüllung: jedes Teil der Rezepte nennt einen Knoten, den es im Baukasten (.glb) gibt; die Themen Kirchhof, Holzplatz, Heuschober, Bildstock, Hof, Pferch, Wasserstelle, Steinmetzplatz und Taubenschlag sind da, ihr Winterknoten auch (die Tiere haben keinen: Schwein und Ziege stehen im Winter wie im Sommer)", () =>
        {
            var pfad = Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "restflaechen.json");
            var wurzel = JsonNode.Parse(File.ReadAllText(pfad))!;
            var modelle = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(pfad))!, "assets", "modelle");
            var namen = new Dictionary<string, HashSet<string>>();
            HashSet<string> Knoten(string datei)
            {
                if (namen.TryGetValue(datei, out var vorhanden)) return vorhanden;
                var bytes = File.ReadAllBytes(Path.Combine(modelle, datei + ".glb"));
                var laenge = BitConverter.ToInt32(bytes, 12); // erster Block: JSON
                var json = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(bytes, 20, laenge))!;
                return namen[datei] = json["nodes"]!.AsArray().Select(n => n!["name"]?.GetValue<string>() ?? "").ToHashSet();
            }
            foreach (var thema in new[] { "kirchhof", "holzplatz", "heuschober", "bildstock", "hof", "pferch", "wasserstelle", "steinmetzplatz", "taubenschlag" })
                Pruefe.Wahr(wurzel["themen"]![thema] is not null, $"Thema {thema} gibt es");
            foreach (var (thema, inhalt) in wurzel["themen"]!.AsObject())
                foreach (var (format, teile) in inhalt!["formate"]!.AsObject())
                    foreach (var teil in teile!.AsArray())
                    {
                        var datei = wurzel["modelle"]![teil!["m"]!.GetValue<string>()]!.GetValue<string>();
                        var gesucht = teil["k"] is JsonArray a ? a.Select(k => k!.GetValue<string>()).ToList() : new List<string> { teil["k"]!.GetValue<string>() };
                        foreach (var k in gesucht)
                        {
                            Pruefe.Wahr(Knoten(datei).Contains(k), $"{thema} {format}: Knoten {k} in {datei}.glb");
                            if (datei.StartsWith("props/fuellung_")) Pruefe.Wahr(Knoten(datei).Contains(k + "_Winter") || k == "Kohlenmeiler" || datei == "props/fuellung_tiere", $"{datei}: {k} hat eine Winterfassung");
                        }
                    }
            Pruefe.Wahr(wurzel["hoechstens_felder"]!["bildstock"]!.GetValue<int>() > 0, "der Bildstock gilt nur für kleine Flächen");
        }),
    };
}
