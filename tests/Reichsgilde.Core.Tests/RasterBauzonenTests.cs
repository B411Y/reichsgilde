using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Bauzonen aus dem 2-m-Raster für jede Karte (Kartenwerkstatt/Rasterbauzonen.cs, Variante A, Nutzer 4.11.2026): frei heißt bebaubar, die
/// Lage aus groben Lage-Gebieten, von Hand gezeichnete Zonen gehen vor, Restflächen für die Darstellung; ein Schritt mit Rückgängig.
/// </summary>
static class RasterBauzonenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    static Kartenort[] Rechteck(double x0, double z0, double x1, double z1) => new[] { O(x0, z0), O(x1, z0), O(x1, z1), O(x0, z1) };

    static Kartenentwurf Oeffnen(string karte) => Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(karte)), Katalog.Value);

    /// <summary>
    /// Eine kleine Karte: eine Landstraße von West nach Ost (z = 0), ein Werkstattplatz nördlich daran, eine Ecke Vorstadt (südlich der Straße)
    /// und ein schmaler Kern-Streifen. Lage-Gebiete: Kern (x 0 … 60), Vorstadt (x −60 … 0).
    /// </summary>
    static Kartenentwurf Probekarte()
    {
        var e = Kartenentwurf.Neu("rasterprobe", "Rasterprobe", 400, 300, Katalog.Value);
        e.WegAnlegen(Wegklasse.Land, new[] { O(-150, 0), O(150, 0) }, glatt: false, name: "Landstraße");
        e.Setzen("werkstattplatz", 20, -12);
        e.LagenSetzen(new[]
        {
            new Lagegebiet(Lage.Kern, Rechteck(0, -40, 60, 40), "Kern"),
            new Lagegebiet(Lage.Vorstadt, Rechteck(-60, -40, 0, 40), "Vorstadt"),
        });
        return e;
    }

    /// <summary>Die Rasterfelder (i, j) mit ihrer Mitte in den Zonen.</summary>
    static HashSet<(int I, int J)> Felder(IEnumerable<IReadOnlyList<Kartenort>> flaechen)
    {
        var felder = new HashSet<(int, int)>();
        foreach (var f in flaechen)
        {
            var (x0, x1, z0, z1) = ((int)Math.Floor(f.Min(p => p.X) / 2), (int)Math.Ceiling(f.Max(p => p.X) / 2), (int)Math.Floor(f.Min(p => p.Z) / 2), (int)Math.Ceiling(f.Max(p => p.Z) / 2));
            for (var i = x0; i < x1; i++)
                for (var j = z0; j < z1; j++)
                    if (Kartengeometrie.ImVieleck(O(i * 2 + 1, j * 2 + 1), f)) felder.Add((i, j));
        }
        return felder;
    }

    static IReadOnlyList<Kartenort> Umriss(Restflaeche r) => r.Flaeche.Select(Kartenort.Aus).ToList();

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Raster-Bauzonen: freie Felder werden Zonen ihrer Lage, mit Abstand zu Straße und Gebäude, Ecken auf dem 2-m-Raster", () =>
        {
            var e = Probekarte();
            var r = e.BauzonenAusRaster();
            Pruefe.Wahr(e.Bauzonen.Count > 0 && e.Bauzonen.All(z => z.Berechnet), "berechnete Zonen");
            Pruefe.Wahr(e.Bauzonen.Any(z => z.Art == Lage.Kern) && e.Bauzonen.Any(z => z.Art == Lage.Vorstadt), "Kern und Vorstadt");
            Pruefe.Gleich(r.Zonen.Count, e.Bauzonen.Count);
            var strasse = e.Wege[0];
            var platz = e.Gebaeude[0].Ecken();
            foreach (var z in e.Bauzonen)
            {
                Pruefe.Wahr(z.Ecken.All(p => p.X % 2 == 0 && p.Z % 2 == 0), $"{z.Kennung} auf dem Raster");
                Pruefe.Wahr(z.Ecken.All(p => Kartengeometrie.AbstandLinie(p, strasse.Verlauf) >= strasse.BreiteWirksam / 2 + 0.5 - 1e-9), $"{z.Kennung} hält Abstand zur Straße");
                Pruefe.Wahr(Kartengeometrie.AbstandVielecke(z.Ecken, platz) >= 2 - 1e-9 && !Kartengeometrie.ImVieleck(Kartengeometrie.Mitte(platz), z.Ecken),
                    $"{z.Kennung} hält 2 m Abstand zum Werkstattplatz");
                var lage = z.Art == Lage.Kern ? (0.0, 60.0) : (-60.0, 0.0);
                Pruefe.Wahr(z.Ecken.All(p => p.X >= lage.Item1 && p.X <= lage.Item2), $"{z.Kennung} bleibt in seinem Lage-Gebiet");
            }
            // Nordseite: zwischen Straße (2,5 m Rand) und Feldgrenze 30 m – die Felder der Kernlage nördlich der Straße reichen bis z = −30
            Pruefe.Wahr(e.Bauzonen.Where(z => z.Art == Lage.Kern).SelectMany(z => z.Ecken).Min(p => p.Z) >= -32, "nicht weiter als 30 m von der Karrenstraße");
            Pruefe.Gleich("", string.Join("\n", e.Befunde.Where(b => b.Schwere == Schwere.Fehler && b.Regel.StartsWith("zone"))));
            var kennungen = e.Bauzonen.Select(z => z.Kennung).ToList();
            Pruefe.Gleich(kennungen.Count, kennungen.Distinct().Count());
            Pruefe.Wahr(kennungen.All(k => k.StartsWith("K") || k.StartsWith("V")), string.Join(", ", kennungen));
        }),

        ("Raster-Bauzonen: eine neue Rechnung ersetzt nur berechnete Zonen; die von Hand gezeichnete bleibt, geht vor und steht hinten", () =>
        {
            var e = Probekarte();
            var hand = e.BauzoneAnlegen(Lage.Vorstadt, Rechteck(-40, 4, -24, 18), "WB_Probe", Bauzone.NurWerkstatt);
            e.BauzonenAusRaster();
            var erst = e.ZuJson();
            Pruefe.Wahr(e.Bauzonen.Contains(hand) && !hand.Berechnet, "die Hand-Zone bleibt");
            Pruefe.Gleich(hand, e.Bauzonen[^1], "berechnete Zonen stehen vor den von Hand gezeichneten");
            var handfelder = Felder(new[] { hand.Ecken });
            foreach (var z in e.Bauzonen.Where(z => z.Berechnet))
                Pruefe.Wahr(!Felder(new[] { z.Ecken }).Overlaps(handfelder) && Kartengeometrie.AbstandVielecke(z.Ecken, hand.Ecken) >= 2 - 1e-9,
                    $"{z.Kennung} hält sich von der Hand-Zone frei (ein Feld Abstand): {string.Join(" ", z.Ecken)}");
            e.BauzonenAusRaster();
            Pruefe.Gleich(erst, e.ZuJson(), "zweimal gerechnet: dieselbe Datei");
            Pruefe.Gleich(1, e.Bauzonen.Count(z => !z.Berechnet));
        }),

        ("Raster-Bauzonen: ein Schritt mit Rückgängig und Wiederholen, Byte für Byte", () =>
        {
            var e = Probekarte();
            var vorher = e.ZuJson();
            e.BauzonenAusRaster();
            var nachher = e.ZuJson();
            Pruefe.Wahr(vorher != nachher, "es hat sich etwas geändert");
            Pruefe.Wahr(e.Geschichte.Rueckgaengig(), "Rückgängig");
            Pruefe.Gleich(vorher, e.ZuJson());
            Pruefe.Wahr(e.Geschichte.Wiederholen(), "Wiederholen");
            Pruefe.Gleich(nachher, e.ZuJson());
            // ohne Lage-Gebiete keine Zone; von Hand gezeichnete bleiben
            var leer = Kartenentwurf.Neu("ohne", "Ohne", 400, 300, Katalog.Value);
            leer.WegAnlegen(Wegklasse.Land, new[] { O(-150, 0), O(150, 0) }, glatt: false, name: "Landstraße");
            var z = leer.BauzoneAnlegen(Lage.Vorstadt, Rechteck(-40, 4, -24, 18));
            var r = leer.BauzonenAusRaster();
            Pruefe.Gleich(0, r.Zonen.Count);
            Pruefe.Wahr(leer.Bauzonen.Single() == z, "die Hand-Zone bleibt allein");
        }),

        ("Raster-Bauzonen: Restflächen – zu kleine Stücke „klein“, ohne Karrenstraße „abseits“, im Umland keine, nie auf einer Zone", () =>
        {
            var e = Kartenentwurf.Neu("rest", "Rest", 400, 300, Katalog.Value);
            e.WegAnlegen(Wegklasse.Land, new[] { O(-150, 0), O(150, 0) }, glatt: false, name: "Landstraße");
            // eine Gasse (keine Karrenstraße) und zwei Werkstattplätze schließen nördlich der Straße ein kleines Stück ein
            e.Setzen("werkstattplatz", 0, -12);
            e.Setzen("werkstattplatz", 24, -12);
            e.WegAnlegen(Wegklasse.Gasse, new[] { O(-10, -24), O(34, -24) }, glatt: false, name: "Hintergasse");
            e.LagenSetzen(new[]
            {
                new Lagegebiet(Lage.Mauerring, Rechteck(-10, -60, 34, 0), "Mauerring"),
                new Lagegebiet(Lage.Umland, Rechteck(60, -80, 120, 0), "Umland"),
            });
            var r = e.BauzonenAusRaster();
            Pruefe.Wahr(r.Restflaechen.Count > 0, "es gibt Restflächen");
            Pruefe.Wahr(r.Restflaechen.All(x => x.Art == Lage.Mauerring), "nur in der Stadt (das Umland bleibt Wiese)");
            Pruefe.Wahr(r.Restflaechen.Any(x => x.Grund == Restflaeche.Klein && x.Felder < 24), "ein kleines Stück zwischen den Plätzen");
            Pruefe.Wahr(r.Restflaechen.Any(x => x.Grund == Restflaeche.Abseits && x.Felder >= 24), "hinter den Plätzen, über 30 m von der Straße");
            Pruefe.Wahr(r.Restflaechen.Select(x => x.Kennung).SequenceEqual(Enumerable.Range(1, r.Restflaechen.Count).Select(n => $"R{n}")), "R1, R2 …");
            var zonen = Felder(e.Bauzonen.Select(z => z.Ecken));
            foreach (var x in r.Restflaechen)
            {
                var f = Felder(new[] { Umriss(x) });
                Pruefe.Gleich(x.Felder, f.Count, $"{x.Kennung}: Felder passen zum Umriss");
                Pruefe.Wahr(!f.Overlaps(zonen), $"{x.Kennung} liegt auf keiner Zone");
            }
            // In der Datei und im Spiel lesbar
            var json = JsonNode.Parse(e.ZuJson())!;
            Pruefe.Gleich(r.Restflaechen.Count, json["restflaechen"]!.AsArray().Count);
            Pruefe.Gleich(2, json["lagen"]!.AsArray().Count);
            var karte = Stadtkarte.AusJson(e.ZuJson(), Katalog.Value);
            Pruefe.Gleich(r.Restflaechen.Count, karte.Restflaechen!.Count);
            Pruefe.Gleich(r.Restflaechen[0].Grund, karte.Restflaechen[0].Grund);
            Pruefe.Wahr(karte.Bauzonen.All(z => z.Berechnet), "das Spiel liest „berechnet“");
        }),

        ("Raster-Bauzonen: Lage-Gebiete, „berechnet“ und Restflächen überstehen Schreiben und Lesen Byte für Byte; „keine“ sperrt", () =>
        {
            var e = Probekarte();
            e.LagenSetzen(e.Lagen.Prepend(new Lagegebiet(null, Rechteck(-60, 4, -30, 40), "Bleiche")));
            e.BauzonenAusRaster();
            var text = e.ZuJson();
            var wieder = Kartenentwurf.AusJson(text, Katalog.Value);
            Pruefe.Gleich(text, wieder.ZuJson());
            Pruefe.Gleich(3, wieder.Lagen.Count);
            Pruefe.Gleich(null, wieder.Lagen[0].Art);
            Pruefe.Gleich("keine", (string)JsonNode.Parse(text)!["lagen"]![0]!["art"]!);
            Pruefe.Wahr(wieder.Bauzonen.All(z => z.Berechnet), "berechnet gelesen");
            Pruefe.Wahr(e.Bauzonen.All(z => z.Ecken.All(p => !(p.X > -60 && p.X < -30 && p.Z > 4 && p.Z < 40))), "im Gebiet „keine“ keine Zone");
            Pruefe.WirftMitMeldung(() => Kartenentwurf.AusJson(text.Replace("\"keine\"", "\"irgendwo\""), Katalog.Value), "unbekannte Art");
        }),

        ("Raster-Bauzonen: gleiche Karte, gleiche Zonen – zwei Entwürfe rechnen dieselbe Datei", () =>
        {
            var a = Probekarte();
            var b = Probekarte();
            a.BauzonenAusRaster();
            b.BauzonenAusRaster();
            Pruefe.Gleich(a.ZuJson(), b.ZuJson());
        }),

        ("Raster-Bauzonen: Umriss mit Ecken-Berührung und Loch wird in einfache Teile zerlegt", () =>
        {
            // ein Ring aus 5 × 5 Feldern mit Loch in der Mitte, dazu ein Feld, das nur an der Ecke hängt
            var felder = new SortedSet<(int I, int J)>();
            for (var i = 0; i < 5; i++)
                for (var j = 0; j < 5; j++)
                    if (!(i == 2 && j == 2)) felder.Add((i, j));
            felder.Add((5, 5));
            var schleifen = Rasterbauzonen.Schleifen(felder);
            Pruefe.Gleich(3, schleifen.Count, "außen, Loch und das Eckfeld je eine eigene Schleife");
            var teile = Rasterbauzonen.Komponenten(felder).SelectMany(Rasterbauzonen.OhneLoecher).ToList();
            Pruefe.Wahr(teile.All(t => Rasterbauzonen.Schleifen(t.Teil).Count == 1), "jedes Teil hat einen Umriss");
            Pruefe.Gleich(felder.Count, teile.Sum(t => t.Teil.Count + t.Schnitt.Count), "kein Feld geht verloren");
        }),

        ("Raster-Bauzonen: Greybox und Probestadt sind aktuell – neu gerechnet ergibt dieselbe Datei; Köln hat nur berechnete Zonen (die Werkstatt-Bauplätze sind seit 9.11.2026 Hofstellen)", () =>
        {
            foreach (var karte in new[] { "greybox", "probestadt" })
            {
                var e = Oeffnen(karte);
                var text = e.ZuJson();
                Pruefe.Wahr(e.Lagen.Count > 0 && e.Bauzonen.All(z => z.Berechnet), $"{karte}: Lage-Gebiete, alle Zonen berechnet");
                e.BauzonenAusRaster();
                e.ProbeplanZonenZuordnen();
                Pruefe.Gleich(text, e.ZuJson(), $"{karte}: die Datei ist auf dem Stand der Rechnung (sonst: bauzonen {karte})");
            }
            var koeln = Oeffnen("koeln");
            Pruefe.Gleich(0, koeln.Bauzonen.Count(z => !z.Berechnet), "keine Zone von Hand (bis zu den Hofstellen 6 Werkstatt-Bauplätze, bis zum Rathaus am Alter Markt 7)");
            Pruefe.Gleich(46, koeln.Bauzonen.Count(z => z.Berechnet)); // seit den Kirchenmodellen (9.11.2026) 49, vorher 50; seit Rathaus, Stapelhaus und Rheinkran (Paket 3) 48; seit den Hofstellen 46
            Pruefe.Gleich(0, koeln.Start.Hofstellen.Count(h => h.Kennung.StartsWith("WB_")), "die Werkstatt-Bauplätze heißen als Hofstellen N1–N5 und A1");
            Pruefe.Wahr(koeln.Restflaechen.Count > 0, "Köln hat Restflächen");
        }),

        ("Raster-Bauzonen: kein Verlust – jedes Feld der früheren Hand-Zonen der Greybox ist weiter Zone derselben Lage", () =>
        {
            // die elf Zonen der Greybox bis 4.11.2026 (K1–K3, M1, V1–V8)
            var alt = new (Lage Art, Kartenort[] Ecken)[]
            {
                (Lage.Kern, Rechteck(4, 22, 36, 36)),
                (Lage.Kern, new[] { O(-12, 14), O(8, 14), O(8, 22), O(4, 22), O(4, 36), O(-12, 36) }),
                (Lage.Kern, Rechteck(-36, 26, -12, 36)),
                (Lage.Vorstadt, Rechteck(-118, -12, -92, 2)),
                (Lage.Vorstadt, Rechteck(-120, 10, -104, 24)),
                (Lage.Vorstadt, Rechteck(-96, 10, -80, 24)),
                (Lage.Vorstadt, Rechteck(-112, 28, -102, 36)),
                (Lage.Mauerring, new[] { O(-80, -58), O(-66, -58), O(-66, -44), O(-62, -44), O(-62, -26), O(-80, -26) }),
                (Lage.Vorstadt, Rechteck(-76, -66, -66, -58)),
                (Lage.Vorstadt, new[] { O(-42, -62), O(-34, -62), O(-34, -66), O(-16, -66), O(-16, -52), O(-42, -52) }),
                (Lage.Vorstadt, new[]
                {
                    O(-56, -78), O(-48, -78), O(-48, -82), O(-40, -82), O(-40, -84), O(-32, -84), O(-32, -96), O(-16, -96), O(-16, -82), O(-32, -82),
                    O(-32, -74), O(-40, -74), O(-40, -72), O(-48, -72), O(-48, -68), O(-56, -68),
                }),
            };
            var e = Oeffnen("greybox");
            foreach (var lage in Enum.GetValues<Lage>())
            {
                var vorher = Felder(alt.Where(z => z.Art == lage).Select(z => (IReadOnlyList<Kartenort>)z.Ecken));
                var nachher = Felder(e.Bauzonen.Where(z => z.Art == lage).Select(z => z.Ecken));
                Pruefe.Wahr(vorher.IsSubsetOf(nachher), $"{lage}: {vorher.Count(f => !nachher.Contains(f))} Felder verloren");
                Pruefe.Wahr(nachher.Count >= vorher.Count, $"{lage}: {nachher.Count} ≥ {vorher.Count}");
            }
            Pruefe.Gleich(21, e.Probeplan.Count);
            Pruefe.Wahr(e.Probeplan.All(p => e.Bauzonen.Any(z => z.Kennung == p.Zone)), "der Probeplan nennt die neuen Zonen");
        }),
    };
}
