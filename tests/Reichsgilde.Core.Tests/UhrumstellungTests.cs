using System.IO.Compression;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Zeit;

// ─────────────────────────────────────────────────────────────────────────────
//  Spieltag 04–23 Uhr (PR #208): Alte Spielstände (Format 2, 05–22 Uhr) laden weiter – gleiche Uhrzeit im gleichen Jahr.
//  Der Spielstand in Spielstaende/ wurde mit dem Stand vor der Umstellung gespeichert (main 90916dc + Balancing 2, Greybox,
//  Startwert 707, Herbst 1406 um 14:37, zwei Schiffe der KI).
// ─────────────────────────────────────────────────────────────────────────────

static class UhrumstellungTests
{
    static readonly Lazy<string> AlterStand = new(() =>
    {
        var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
            "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
        using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
        return new StreamReader(gz).ReadToEnd();
    });

    static void Ticken(Simulation sim, int minuten)
    {
        for (var i = 0; i < minuten; i++) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Spieltag: 04:00 bis 23:00 Uhr, 1140 Minuten und 19 Stunden je Jahr; 15 Minuten Echtzeit bleiben ein Jahr", () =>
        {
            Pruefe.Gleich((240, 1380, 1140, 19), (Spielzeit.TagesbeginnMinute, Spielzeit.TagesendeMinute, Spielzeit.MinutenProTag, Spielzeit.StundenProTag));
            Pruefe.Gleich("04:00", new Spielzeit().UhrzeitText);
            Pruefe.Gleich("22:59", new Spielzeit(1139).UhrzeitText);
            Pruefe.Gleich((1401, "04:00"), (new Spielzeit(1140).Jahr, new Spielzeit(1140).UhrzeitText));
            Pruefe.Gleich(22, new Spielzeit(1140).StundeDieEndete); // die letzte Stunde des Jahres (22–23 Uhr) rechnet das Jahresende ab
            Pruefe.Ungefaehr(1140 / 900.0, new Taktgeber().TicksProSekunde, 1e-12);
        }),

        ("Uhrumstellung: Zeitpunkt aus Format 2 – gleiche Uhrzeit im gleichen Jahr", () =>
        {
            Pruefe.Gleich(60L, Uhrumstellung.Zeitpunkt(0));                    // Frühling 1400, 05:00
            Pruefe.Gleich(60L + 1019, Uhrumstellung.Zeitpunkt(1019));          // 21:59
            Pruefe.Gleich(1140L + 60, Uhrumstellung.Zeitpunkt(1020));          // Sommer 1401, 05:00
            foreach (var alt in new long[] { 0, 1, 577, 1019, 1020, 6697, 30_599 })
            {
                var neu = new Spielzeit(Uhrumstellung.Zeitpunkt(alt));
                var jahr = (int)(alt / 1020);
                var uhr = 5 * 60 + (int)(alt % 1020);
                Pruefe.Gleich((Spielzeit.StartJahr + jahr, $"{uhr / 60:00}:{uhr % 60:00}"), (neu.Jahr, neu.UhrzeitText), $"Minute {alt}");
            }
        }),

        ("Uhrumstellung: Ein alter Spielstand (Greybox, Herbst 1406, 14:37) lädt – Uhr, Karren, Schiffe, Markt und Bürger umgerechnet", () =>
        {
            var json = AlterStand.Value;
            var alt = JsonNode.Parse(json)!.AsObject();
            Pruefe.Gleich(2, (int)alt["Format"]!);
            var (sim, spieler) = Speicherung.Laden(json);
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(("Herbst 1406", "14:37"), (sim.Zeit.DatumText, sim.Zeit.UhrzeitText));
            Pruefe.Gleich("Haus Hammerschmid", spieler?.Inhaber);

            // Karren und Schiffe kommen zur selben Uhrzeit an wie vorher – die Fahrt dauert noch genauso lange.
            var altBetriebe = alt["Wirtschaft"]!["Betriebe"]!.AsArray();
            var karren = 0;
            foreach (var b in altBetriebe)
                foreach (var k in b!["Karren"]!.AsArray())
                {
                    var name = (string)b["Name"]!;
                    var nummer = k!.Parent!.AsArray().IndexOf(k);
                    var geladen = w.Betriebe.Single(x => x.Name == name).Karren[nummer];
                    Pruefe.Gleich(Uhrumstellung.Zeitpunkt((long)k["Ankunft"]!), geladen.Ankunft, $"{name}, Karren {nummer}");
                    karren++;
                }
            Pruefe.Wahr(karren >= 4, $"Karren im Spielstand: {karren}");
            var altSchiffe = alt["Wirtschaft"]!["Schiffahrt"]!["Schiffe"]!.AsArray();
            Pruefe.Gleich(2, w.Schiffahrt.Schiffe.Count);
            foreach (var (s, i) in w.Schiffahrt.Schiffe.Select((s, i) => (s, i)))
            {
                Pruefe.Gleich(Uhrumstellung.Zeitpunkt((long)altSchiffe[i]!["Ende"]!), s.Ende, s.Name);
                Pruefe.Gleich((long)altSchiffe[i]!["Ende"]! - (long)alt["VergangeneMinuten"]!, s.Ende - sim.Zeit.VergangeneMinuten, $"{s.Name}: Restzeit");
            }

            // Vermögen bleiben; der Markt zählt alle Verkäufe der letzten 18 alten Stundenfächer (sie liegen in den neuen 19 Stunden).
            foreach (var k in alt["Wirtschaft"]!["Konten"]!.AsArray())
                Pruefe.Gleich((double)k!["Vermoegen"]!, w.Konten.Single(x => x.Inhaber == (string)k["Inhaber"]!).Vermoegen);
            var staende = alt["Wirtschaft"]!["Markt"]!.AsObject();
            foreach (var (ware, stand) in staende)
            {
                var summe = stand!["VerkaufteStueckProStunde"]!.AsArray().Sum(x => (double)x!);
                Pruefe.Wahr(Math.Abs(summe - w.Markt[ware].SpielerAngebotImJahr) < 1e-9, $"{ware}: {summe} gegen {w.Markt[ware].SpielerAngebotImJahr}");
            }
            var einkaeufe = alt["Wirtschaft"]!["Einwohner"]!["Einkaeufe"]!.AsArray().Sum(x => (int)x!);
            Pruefe.Wahr(einkaeufe > 0, "Einkäufe der Bürger im alten Spielstand");
            Pruefe.Gleich(einkaeufe, w.Einwohnerschaft.EinkaeufeImFenster);

            // Die Daten im Spielstand rechnen mit dem längeren Jahr; Ruhen geht über den ganzen Tag (sonst schlüge die Prüfung fehl).
            Pruefe.Gleich((1140, 1140, 2280), (w.Daten.Werkstatt!.Praemie.Minuten, w.Daten.Werkstatt.Unfall.LauneMinuten, (int)w.Daten.Werkstatt.Befoerderung.DienstMinuten));
            Pruefe.Gleich((4, 23), (w.Daten.Buerger!.Handlungen[Reichsgilde.Core.Buerger.Handlung.Ruhen].Von, w.Daten.Buerger.Handlungen[Reichsgilde.Core.Buerger.Handlung.Ruhen].Bis));
            Pruefe.Ungefaehr(0.0719, w.Daten.Schiffshandel!.ErholungJeStunde, 1e-9);

            // Weiterspielen über den Jahreswechsel; der neue Spielstand ist Format 3 und lädt Wurf für Wurf gleich.
            Ticken(sim, 23 * 60 - sim.Zeit.UhrzeitInMinuten + 30);
            Pruefe.Gleich(("Winter 1407", "04:30"), (sim.Zeit.DatumText, sim.Zeit.UhrzeitText));
            var neu = Speicherung.Speichern(sim, spieler, DateTime.UnixEpoch);
            Pruefe.Gleich(3, (int)JsonNode.Parse(neu)!["Format"]!);
            var (kopie, kopieSpieler) = Speicherung.Laden(neu);
            Ticken(sim, 300);
            Ticken(kopie, 300);
            Pruefe.Gleich(Speicherung.Speichern(sim, spieler, DateTime.UnixEpoch), Speicherung.Speichern(kopie, kopieSpieler, DateTime.UnixEpoch));
        }),

        ("Uhrumstellung: Ringe je Stunde – 18 Fächer des Marktes werden 20, 17 Einkaufsstunden der Bürger 19, die Reihenfolge bleibt", () =>
        {
            var stand = new JsonObject
            {
                ["Format"] = 2,
                ["VergangeneMinuten"] = 100,
                ["Wirtschaft"] = new JsonObject
                {
                    ["Markt"] = new JsonObject
                    {
                        ["Eisen"] = new JsonObject
                        {
                            ["VerkaufteStueckProStunde"] = new JsonArray(Enumerable.Range(0, 18).Select(i => (JsonNode)(double)i).ToArray()),
                            ["AktuellesFach"] = 4, ["MinuteInStunde"] = 40,
                        },
                    },
                    ["Einwohner"] = new JsonObject
                    {
                        ["Einkaeufe"] = new JsonArray(Enumerable.Range(1, 17).Select(i => (JsonNode)i).ToArray()),
                        ["EinkaufFach"] = 5,
                    },
                },
            };
            Uhrumstellung.Umrechnen(stand);
            Pruefe.Gleich(3, (int)stand["Format"]!);
            Pruefe.Gleich(160L, (long)stand["VergangeneMinuten"]!);
            var markt = stand["Wirtschaft"]!["Markt"]!["Eisen"]!;
            // ältestes altes Fach war 5, das laufende 4: neu 0, 0, 5, 6 … 17, 0, 1 … 4 – das laufende Fach ist das letzte
            Pruefe.Gleich("0,0,5,6,7,8,9,10,11,12,13,14,15,16,17,0,1,2,3,4", string.Join(",", markt["VerkaufteStueckProStunde"]!.AsArray().Select(x => (double)x!)));
            Pruefe.Gleich(19, (int)markt["AktuellesFach"]!);
            var buerger = stand["Wirtschaft"]!["Einwohner"]!;
            // nächstes (ältestes) Fach war 5 (Wert 6): neu 0, 0, 6 … 17, 1 … 5; als Nächstes wird das älteste (0) beschrieben
            Pruefe.Gleich("0,0,6,7,8,9,10,11,12,13,14,15,16,17,1,2,3,4,5", string.Join(",", buerger["Einkaeufe"]!.AsArray().Select(x => (int)x!)));
            Pruefe.Gleich(0, (int)buerger["EinkaufFach"]!);
            // Ein Spielstand in Format 3 bleibt, wie er ist.
            var neu = new JsonObject { ["Format"] = 3, ["VergangeneMinuten"] = 100L };
            Uhrumstellung.Umrechnen(neu);
            Pruefe.Gleich(100L, (long)neu["VergangeneMinuten"]!);
        }),
    };
}
