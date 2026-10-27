using System.Diagnostics;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Kartenbau.Staedte;

/// <summary>
/// Kartenwerkstatt, Schritt 5e (PR #183): Bodentexturen – Bodenraster (packen, Datei, Spielstand), Texturpinsel mit Rückgängig und Radierer,
/// Deko (deterministisch, weicht aus, nur wo gestreut), alte Karten unverändert, Katalog.
/// </summary>
static class BodenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static string Text(string kennung) => File.ReadAllText(Datenpfad.Karte(kennung));
    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Eine leere Karte 400 × 300 m mit 300 m Rand (die ganze Karte: x −500 … 500, z −450 … 450).</summary>
    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    static int G(Kartenentwurf e, int x, int z, Bodentextur t) => e.Boden.Gewicht(x, z, t);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Bodenraster: Kanal packen und entpacken ergibt dieselben Werte; Werte über 255 sind nicht lesbar", () =>
        {
            var w = new byte[64 * 64];
            for (var n = 0; n < w.Length; n++) w[n] = (byte)(n * 37 % 256);
            Pruefe.Wahr(Bodenraster.Entpacken(Bodenraster.Packen(w))!.AsSpan().SequenceEqual(w), "gleich");
            var gross = new short[64 * 64];
            gross[5] = 300;
            Pruefe.Gleich(null, Bodenraster.Entpacken(Hoehenraster.Packen(gross)));
            Pruefe.Wahr(Bodenraster.Packen(new byte[64 * 64]).Length < 40, "ein leerer Kanal packt klein");
        }),

        ("Texturpinsel: Stärke 1 deckt ganz, weiche Kante, ein Strich = ein Schritt, ein Punkt zählt je Strich nur einmal; Rückgängig, Meldungen", () =>
        {
            var e = Leer();
            var meldungen = new List<Kartenaenderung>();
            e.Geaendert += meldungen.Add;
            Pruefe.Wahr(e.BodenMalen(new Bodenpinsel(Bodentextur.Erde, Radius: 10, Kante: 0), new[] { O(0, 0) }), "geändert");
            Pruefe.Gleich(255, G(e, 0, 0, Bodentextur.Erde));
            Pruefe.Gleich(0, G(e, 0, 0, Bodentextur.Wiese));
            Pruefe.Gleich(255, G(e, 9, 0, Bodentextur.Erde), "innen voll");
            Pruefe.Gleich(255, G(e, 11, 0, Bodentextur.Wiese), "außen Wiese");
            Pruefe.Gleich(Bodentextur.Erde, e.Boden.Textur(3.2, -2.7));
            Pruefe.Gleich(1, e.Geschichte.Schritte);
            Pruefe.Gleich("Erde gemalt", e.Geschichte.RueckgaengigText);
            Pruefe.Wahr(meldungen.Count >= 1 && meldungen.All(m => m.Teil == Kartenteil.Boden && m.Bereich is not null), "Meldungen mit Rechteck");
            Pruefe.Wahr(meldungen[^1].Bereich!.Value.Enthaelt(-9, 9) && !meldungen[^1].Bereich!.Value.Enthaelt(-20, 0), $"{meldungen[^1].Bereich}");
            // halbe Stärke, dreimal über dieselbe Stelle in einem Strich: zählt einmal
            e.BodenMalen(new Bodenpinsel(Bodentextur.Fels, Radius: 4, Staerke: 0.5, Kante: 0), new[] { O(0, 0), O(1, 0), O(0, 0), O(1, 0) });
            Pruefe.Gleich(127, G(e, 0, 0, Bodentextur.Fels), "127,5 – die Rundung nimmt der gemalten Textur");
            Pruefe.Gleich(128, G(e, 0, 0, Bodentextur.Erde));
            Pruefe.Gleich(0, G(e, 0, 0, Bodentextur.Wiese));
            Pruefe.Gleich("Fels gemalt", e.Geschichte.RueckgaengigText);
            // weiche Kante: zum Rand hin weniger
            e.BodenMalen(new Bodenpinsel(Bodentextur.Sand, Radius: 20, Kante: 1), new[] { O(100, 0) });
            Pruefe.Wahr(G(e, 100, 0, Bodentextur.Sand) == 255 && G(e, 110, 0, Bodentextur.Sand) is > 0 and < 255 && G(e, 120, 0, Bodentextur.Sand) == 0, "weich");
            // Rückgängig und Wiederholen
            e.Rueckgaengig();
            e.Rueckgaengig();
            Pruefe.Gleich(255, G(e, 0, 0, Bodentextur.Erde));
            Pruefe.Gleich(0, G(e, 100, 0, Bodentextur.Sand));
            e.Rueckgaengig();
            Pruefe.Wahr(e.Boden.IstLeer, "zurück: alles Wiese");
            e.Wiederholen();
            Pruefe.Gleich(255, G(e, 0, 0, Bodentextur.Erde));
            // live beim Ziehen
            e.BodenStrichBeginnen(new Bodenpinsel(Bodentextur.Kies, Radius: 3));
            e.BodenStrichWeiter(O(-100, 0));
            Pruefe.Wahr(G(e, -100, 0, Bodentextur.Kies) == 255 && e.BodenStrichLaeuft && e.Geschichte.Schritte == 1, "sofort im Raster, noch kein Schritt");
            Pruefe.Wahr(e.BodenStrichBeenden() && e.Geschichte.Schritte == 2, "nach Beenden ein Schritt");
            // außerhalb der Karte: nichts
            Pruefe.Wahr(!e.BodenMalen(new Bodenpinsel(Bodentextur.Erde), new[] { O(900, 0) }), "außerhalb");
        }),

        ("Radierer: zurück zur Wiese ohne Deko – die Kacheln fallen weg, die Karte schreibt kein boden mehr", () =>
        {
            var e = Leer();
            var vorher = e.ZuJson();
            e.BodenMalen(new Bodenpinsel(Bodentextur.Moos, Radius: 8), new[] { O(0, 0), O(30, 0) });
            Pruefe.Wahr(e.ZuJson().Contains("\"boden\""), "boden steht drin");
            e.BodenMalen(Bodenpinsel.Radierer(radius: 30, kante: 0), new[] { O(0, 0), O(30, 0) });
            Pruefe.Gleich("Boden radiert", e.Geschichte.RueckgaengigText);
            Pruefe.Wahr(e.Boden.IstLeer, "leer");
            Pruefe.Gleich(vorher, e.ZuJson());
        }),

        ("Bodenraster: Feld boden schreiben und lesen (Entwurf, Stadtkarte), unveränderte Kacheln Byte für Byte, kaputte Felder melden", () =>
        {
            var e = Leer();
            e.BodenMalen(new Bodenpinsel(Bodentextur.Waldboden, Radius: 15, Staerke: 0.8), new[] { O(-50, -50), O(50, 20) });
            e.BodenMalen(new Bodenpinsel(Bodentextur.Pflaster, Radius: 5, Deko: false), new[] { O(200, 100) });
            var json = e.ZuJson();
            Pruefe.Wahr(json.Contains("\"boden\": {\n  \"abstand\": 1,\n  \"kachel\": 64,\n  \"startwert\": 1,\n  \"kacheln\": [\n"), json[^300..]);
            Pruefe.Wahr(json.IndexOf("\"boden\"", StringComparison.Ordinal) > json.IndexOf("\"bauzonen\"", StringComparison.Ordinal), "boden hinten");
            var wieder = Kartenentwurf.AusJson(json, Katalog.Value);
            Pruefe.Gleich(json, wieder.ZuJson(), "geschrieben und wieder gelesen");
            foreach (var (i, j) in new[] { (0, 0), (-50, -50), (200, 100), (199, 97), (-64, 0) })
                foreach (var t in Enum.GetValues<Bodentextur>())
                    Pruefe.Gleich(e.Boden.Gewicht(i, j, t), wieder.Boden.Gewicht(i, j, t), $"Punkt {i}, {j}, {t}");
            var karte = Stadtkarte.AusJson(json, Katalog.Value);
            Pruefe.Gleich(e.Boden.Gewicht(0, 0, Bodentextur.Waldboden), karte.Boden!.Gewicht(0, 0, Bodentextur.Waldboden));
            Pruefe.Gleich((byte)0, karte.Boden.Dekodichte(200, 100), "Pflaster ohne Deko");
            Pruefe.Wahr(karte.Boden.Dekodichte(0, -15) == 128, "Waldboden mit Deko (auf der Linie des Strichs)");
            // eine Kachel aus der Datei bleibt als Text, wie sie war
            var alt = JsonNode.Parse(json)!["boden"]!["kacheln"]!.AsArray().Select(k => k!.ToJsonString()).ToList();
            wieder.BodenMalen(new Bodenpinsel(Bodentextur.Lehm, Radius: 3), new[] { O(-400, -400) });
            var neu = JsonNode.Parse(wieder.ZuJson())!["boden"]!["kacheln"]!.AsArray().Select(k => k!.ToJsonString()).ToList();
            Pruefe.Gleich(alt.Count + 1, neu.Count);
            Pruefe.Wahr(alt.All(neu.Contains), "die alten Kacheln unverändert");
            // Splatmap: drei Gruppen, zusammen 255 je Punkt
            var splat = Enumerable.Range(0, 3).Select(g => e.Boden.Splat(-60, -60, 40, 30, g)).ToList();
            for (var n = 0; n < 40 * 30; n++)
                Pruefe.Gleich(255, splat.Sum(s => s[n * 4] + s[n * 4 + 1] + s[n * 4 + 2] + s[n * 4 + 3]), $"Punkt {n}");
            // kaputt
            var leer = Bodenraster.Packen(new byte[64 * 64]);
            var voll = Bodenraster.Packen(Enumerable.Repeat((byte)200, 64 * 64).ToArray());
            foreach (var (kaputt, text) in new[]
            {
                ("{\"abstand\": 2, \"kachel\": 64, \"kacheln\": []}", "abstand"),
                ("{\"abstand\": 1, \"kachel\": 32, \"kacheln\": []}", "kachel"),
                ($"{{\"abstand\": 1, \"kachel\": 64, \"kacheln\": [{{\"i\": 0, \"j\": 0, \"lava\": \"{leer}\"}}]}}", "keine Bodentextur"),
                ("{\"abstand\": 1, \"kachel\": 64, \"kacheln\": [{\"i\": 0, \"j\": 0, \"erde\": \"xx\"}]}", "nicht lesbar"),
                ($"{{\"abstand\": 1, \"kachel\": 64, \"kacheln\": [{{\"i\": 0, \"j\": 0, \"erde\": \"{voll}\", \"fels\": \"{voll}\"}}]}}", "mehr als 255"),
            })
            {
                var o = JsonNode.Parse(json)!.AsObject();
                o["boden"] = JsonNode.Parse(kaputt);
                Pruefe.WirftMitMeldung(() => Kartenentwurf.AusJson(o.ToJsonString(), Katalog.Value), text);
                Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson(o.ToJsonString(), Katalog.Value), text);
            }
        }),

        ("Deko: deterministisch, nur wo gestreut, Teile aus der Liste der Textur, Dichte wie im Katalog, weicht dem Weg aus; ein Strich daneben verschiebt nichts", () =>
        {
            var e = Leer();
            e.WegAnlegen(Wegklasse.Feldweg, new[] { O(-100, 32), O(200, 32) }, glatt: false, name: "Probeweg", einrasten: false);
            e.BodenMalen(new Bodenpinsel(Bodentextur.Waldboden, Radius: 70, Kante: 0), new[] { O(32, 32) });
            var frei = e.Freiraum();
            var deko = e.BodendekoVon(frei);
            var kachel = (0, 0);
            var teile = deko.Kachel(kachel);
            Pruefe.Wahr(teile.SequenceEqual(e.BodendekoVon(e.Freiraum()).Kachel(kachel)), "zweimal gerechnet: gleich");
            var erlaubt = Katalog.Value.TexturDaten(Bodentextur.Waldboden)!.Deko.Keys.ToHashSet();
            Pruefe.Wahr(teile.All(t => erlaubt.Contains(t.Art.Kennung)), string.Join(", ", teile.Select(t => t.Art.Kennung).Distinct()));
            Pruefe.Wahr(teile.All(t => t.X >= 0 && t.X < 64 && t.Z >= 0 && t.Z < 64), "in der Kachel");
            Pruefe.Wahr(teile.All(t => Math.Abs(t.Z - 32) > 1.5), "nicht auf dem Weg");
            Pruefe.Wahr(teile.All(t => t.Variante >= 1 && t.Variante <= Math.Max(1, t.Art.Varianten) && t.Massstab is >= 0.8 and <= 1.2), "Variante, Größe");
            // Dichte: 3 je 100 m² (Katalog) auf der Kachel ohne den Streifen des Weges
            var flaeche = 64 * 64 - 64 * (e.Wege[0].BreiteWirksam + 2);
            var erwartet = flaeche * Katalog.Value.TexturDaten(Bodentextur.Waldboden)!.Dichte / 100;
            Pruefe.Wahr(Math.Abs(teile.Count - erwartet) < erwartet * 0.25, $"{teile.Count} Teile, erwartet etwa {erwartet:0}");
            // ein Strich in einer anderen Kachel ändert diese Kachel nicht
            e.BodenMalen(new Bodenpinsel(Bodentextur.Fels, Radius: 10), new[] { O(300, 300) });
            Pruefe.Wahr(teile.SequenceEqual(e.BodendekoVon(e.Freiraum()).Kachel(kachel)), "unverändert");
            // ohne Deko gemalt: dort streut nichts (auch die Wiese nicht)
            e.BodenMalen(new Bodenpinsel(Bodentextur.Waldboden, Radius: 100, Kante: 0, Deko: false), new[] { O(32, 32) });
            Pruefe.Gleich(0, e.BodendekoVon(e.Freiraum()).Kachel(kachel).Count);
            // eine Textur ohne Deko im Katalog (Pflaster) streut nichts, auch mit „Deko streuen“
            var p = Leer();
            p.BodenMalen(new Bodenpinsel(Bodentextur.Pflaster, Radius: 70, Kante: 0), new[] { O(32, 32) });
            Pruefe.Gleich(0, p.BodendekoVon(p.Freiraum()).Kachel(kachel).Count);
            // doppelte Dichte: etwa doppelt so viele
            var d = Leer();
            d.BodenMalen(new Bodenpinsel(Bodentextur.Waldboden, Radius: 70, Kante: 0, Dichte: 2), new[] { O(32, 32) });
            var doppelt = d.BodendekoVon(d.Freiraum()).Kachel(kachel).Count;
            Pruefe.Wahr(doppelt > 1.6 * 64 * 64 * 3 / 100, $"doppelt: {doppelt}");
        }),

        ("Alte Karten: Die Messstadt hat als Boden nur das Marktpflaster – sonst Wiese, keine Deko, Byte für Byte dieselbe Datei", () =>
        {
            // Die Greybox hat seit PR #199 Boden (Sand, Schlamm und Kies am Fluss und am Hafen).
            Pruefe.Wahr(Stadtkarte.AusDatei(Datenpfad.Karte("greybox")).Boden is { IstLeer: false }, "Greybox mit Boden");
            foreach (var kennung in new[] { "messstadt" })
            {
                var e = Kartenentwurf.AusJson(Text(kennung), Katalog.Value);
                Pruefe.Wahr(!e.Boden.IstLeer, kennung + " mit Marktpflaster");
                Pruefe.Gleich(Text(kennung), e.ZuJson(), kennung);
                Pruefe.Gleich(Bodentextur.Pflaster, e.Boden.Textur(0, 0));
                Pruefe.Gleich(Bodentextur.Wiese, e.Boden.Textur(30, 30));
                Pruefe.Gleich(0, e.BodendekoVon(e.Freiraum()).Alle().Count);
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                Pruefe.Wahr(karte.Boden is not null && Bodendeko.Von(karte).Alle().Count == 0, kennung);
            }
        }),

        ("Marktpflaster: Unter der ganzen Grundfläche jedes Marktplatzes liegt Pflaster, daneben nicht (alle Karten)", () =>
        {
            foreach (var kennung in new[] { "greybox", "koeln", "probestadt", "messstadt" })
            {
                var e = Kartenentwurf.AusJson(Text(kennung), Katalog.Value);
                // jede Karte hat genau einen Markt; die Schleife gilt trotzdem für jeden
                foreach (var markt in e.Gebaeude.Where(g => g.Katalog == "markt"))
                {
                    var (breite, tiefe) = markt.Groesse!.Value;
                    foreach (var (dx, dz) in new[] { (0.0, 0.0), (-1.0, -1.0), (1.0, 1.0), (-1.0, 1.0), (1.0, -1.0) })
                        Pruefe.Gleich(Bodentextur.Pflaster, e.Boden.Textur(markt.X + dx * (breite / 2 - 0.5), markt.Z + dz * (tiefe / 2 - 0.5)), $"{kennung} {markt.Name} {dx} | {dz}");
                    Pruefe.Wahr(e.Boden.Textur(markt.X + breite / 2 + 2, markt.Z) != Bodentextur.Pflaster, $"{kennung}: {markt.Name}, daneben");
                }
            }
        }),

        ("Probestadt: Waldboden im Wald, Schlamm am Sandfluss, Fels am Hügel; Deko mit Schilf und Farn, nicht im Wasser und nicht auf Wegen", () =>
        {
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("probestadt"));
            var b = karte.Boden!;
            Pruefe.Gleich(Bodentextur.Waldboden, b.Textur(-120, -100));
            Pruefe.Gleich(Bodentextur.Schlamm, b.Textur(0, 121));
            Pruefe.Gleich(Bodentextur.Fels, b.Textur(180, -370));
            Pruefe.Gleich(Bodentextur.Pflaster, b.Textur(0, 0)); // Marktpflaster
            Pruefe.Gleich(Bodentextur.Wiese, b.Textur(30, 30));
            var uhr = Stopwatch.StartNew();
            var deko = Bodendeko.Von(karte).Alle();
            uhr.Stop();
            var arten = deko.Select(d => d.Art.Kennung).ToHashSet();
            Pruefe.Wahr(arten.Contains("schilf") && arten.Contains("farn") && arten.Contains("felsbrocken"), string.Join(", ", arten));
            Pruefe.Wahr(deko.All(d => !karte.Gelaendeform.Wasser.IstWasser(d.X, d.Z)), "nichts im Wasser");
            var frei = Naturfreiraum.Aus(karte);
            Pruefe.Wahr(deko.All(d => !frei.AufWeg(d.X, d.Z, 0.9)), "nichts auf Wegen");
            Pruefe.Wahr(uhr.ElapsedMilliseconds < 2000, $"{deko.Count} Teile in {uhr.ElapsedMilliseconds} ms");
        }),

        ("Leistung: 2 × 2 km ganz bemalt (Wiese und Waldboden im Wechsel, mit Deko) – Deko aller Kacheln in wenigen Sekunden, gepackt klein", () =>
        {
            var r = new Bodenraster();
            Span<byte> werte = stackalloc byte[11];
            for (var j = -1000; j < 1000; j++)
                for (var i = -1000; i < 1000; i++)
                {
                    werte.Clear();
                    werte[(int)Bodentextur.Waldboden - 1] = (byte)((i / 50 + j / 50) % 2 == 0 ? 255 : 0);
                    werte[10] = 128;
                    r.WerteSetzen(i, j, werte);
                }
            var uhr = Stopwatch.StartNew();
            var deko = new Bodendeko(r, Katalog.Value, null).Alle();
            uhr.Stop();
            Pruefe.Wahr(uhr.ElapsedMilliseconds < 10000, $"{deko.Count} Teile in {uhr.ElapsedMilliseconds} ms");
            Pruefe.Ungefaehr(4_000_000 * 3 / 100.0, deko.Count, 4_000_000 * 3 / 100.0 * 0.05);
            var groesse = r.AlsJson()!.ToJsonString().Length;
            Pruefe.Wahr(groesse < 2_000_000, $"{groesse} Zeichen");
        }),

        ("Spielstand: Der Boden der Karte liegt im Spielstand und kommt beim Laden genauso wieder", () =>
        {
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            Pruefe.Wahr(!e.Boden.IstLeer, "die Probestadt hat Bodentexturen");
            var (sim, spieler) = e.Ausprobieren(Daten.Value, "Schmied");
            var json = Speicherung.Speichern(sim, spieler);
            var (geladen, _) = Speicherung.Laden(json);
            var a = sim.Wirtschaft!.Karte!.Boden!;
            var b = geladen.Wirtschaft!.Karte!.Boden!;
            Pruefe.Gleich(a.Kacheln.Count, b.Kacheln.Count);
            Pruefe.Wahr(a.Kacheln.All(k => a.KachelGepackt(k)!.SequenceEqual(b.KachelGepackt(k)!)), "alle Kacheln gleich");
        }),

        ("Katalog: alle elf Bodentexturen mit Name und Farbe, ihre Deko sind Natur-Bausteine; eine unbekannte Textur ist ein Fehler", () =>
        {
            var k = Katalog.Value;
            foreach (var t in Enum.GetValues<Bodentextur>())
                Pruefe.Wahr(k.TexturDaten(t) is { Name.Length: > 0, Farbe.Length: 7 }, t.ToString());
            Pruefe.Gleich(11, k.Bodentexturen.Count);
            var text = File.ReadAllText(Path.Combine(Datenordner, "katalog.json")).Replace("\"kennung\": \"mist\"", "\"kennung\": \"lava\"");
            Pruefe.WirftMitMeldung(() => Gebaeudekatalog.AusJson(text), "lava");
            var text2 = File.ReadAllText(Path.Combine(Datenordner, "katalog.json")).Replace("{\"kiesel\": 1}", "{\"kieselstein\": 1}");
            Pruefe.WirftMitMeldung(() => Gebaeudekatalog.AusJson(text2), "kieselstein");
        }),
    };
}
