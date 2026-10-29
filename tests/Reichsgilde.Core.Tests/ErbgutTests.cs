using System.IO.Compression;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

// ─────────────────────────────────────────────────────────────────────────────
//  Erbgut der Personen (Charakter-Editor, Phase 1): Gen-Vorlage, Würfeln und Vererben, Volksgrenzen, Verteilung,
//  Dominanz und Großeltern, Speichern, alte Spielstände, unbekannte und fehlende Gene, Entwicklerbefehl.
// ─────────────────────────────────────────────────────────────────────────────

static class ErbgutTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 10, 29, 12, 0, 0);
    static readonly Volk[] AlleVoelker = Enum.GetValues<Volk>();

    static string Datei() => Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "erbgut.json");

    static (Simulation Sim, Konto Spieler) Partie(int startwert) =>
        Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert },
            Daten.Value, new NeuePartie());

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static IEnumerable<Person> AllePersonen(Stadtwirtschaft w) =>
        w.Konten.Select(w.Familien.Von).Where(f => f is not null).SelectMany(f => f!.Mitglieder)
            .Concat(w.Familien.Kandidaten.Select(k => k.Person)).OrderBy(p => p.Nummer);

    static string Bild(Stadtwirtschaft w) => string.Join(";", AllePersonen(w).Select(p => $"{p.Nummer}:{p.Erbgut.AlsText()}"));

    static string AlterStand()
    {
        var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
            "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
        using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
        return new StreamReader(gz).ReadToEnd();
    }

    /// <summary>Alle Allele liegen in den Grenzen des Volkes.</summary>
    static void InGrenzen(Erbgut e, Volk volk, string wer)
    {
        foreach (var gen in e.Vorlage.Gene)
        {
            var g = gen.Grenzen(volk);
            foreach (var wert in new[] { e.SichtbarerWert(gen), e.VerdeckterWert(gen.Kennung) })
                Pruefe.Wahr(wert >= g.Min && wert <= g.Max, $"{wer}: {gen.Kennung} = {wert} außerhalb {g.Min}–{g.Max} ({volk.Mehrzahl()})");
        }
    }

    /// <summary>Eine kleine Vorlage mit bekannten Zahlen – die Statistik hängt so nicht an der Abstimmung von erbgut.json.</summary>
    static readonly Lazy<Erbgutvorlage> Testvorlage = new(() => Erbgutvorlage.AusJson(TestvorlageJson()));

    static string TestvorlageJson()
    {
        string Voelker(string mensch, string sonst) =>
            string.Join(", ", AlleVoelker.Select(v => $"\"{v}\": {(v == Volk.Mensch ? mensch : sonst)}"));
        const string breit = "{ \"min\": 0, \"mittel\": 128, \"max\": 255, \"streuung\": 30 }";
        return $$"""
        {
          "version": 3,
          "gene": [
            { "kennung": "glocke", "name": "Glocke", "art": "Merkmal", "dominanz": 50, "mutation": 0,
              "voelker": { {{Voelker("{ \"min\": 40, \"mittel\": 100, \"max\": 160, \"streuung\": 20 }", breit)}} } },
            { "kennung": "auswahl", "name": "Auswahl", "art": "Zubehoer", "verteilung": "Gleich", "mutation": 0,
              "voelker": { {{Voelker("{ \"min\": 0, \"mittel\": 128, \"max\": 255, \"streuung\": 0 }", breit)}} } },
            { "kennung": "haar", "name": "Haar", "art": "Farbe", "dominanz": 10, "mutation": 2,
              "voelker": { {{Voelker("{ \"min\": 0, \"mittel\": 120, \"max\": 255, \"streuung\": 50 }", breit)}} } },
            { "kennung": "folge", "name": "Folge", "art": "Merkmal", "dominanz": 50, "mutation": 0,
              "voelker": { {{Voelker("{ \"min\": 0, \"mittel\": 128, \"max\": 255, \"streuung\": 20 }", breit)}} } }
          ],
          "korrelationen": [ { "von": "glocke", "zu": "folge", "staerke": 80 } ]
        }
        """;
    }

    static double Mittel(IEnumerable<int> werte) => werte.Average();

    static double Streuung(IReadOnlyCollection<int> werte)
    {
        var m = werte.Average();
        return Math.Sqrt(werte.Sum(x => (x - m) * (x - m)) / werte.Count);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Erbgut: game/daten/erbgut.json ist gültig – 30 bis 40 Gene aller vier Arten mit Grenzen für alle sechs Völker; der Kern trägt dieselbe Datei", () =>
        {
            var v = Erbgutvorlage.Standard;
            var ausDatei = Erbgutvorlage.AusDatei(Datei());
            Pruefe.Gleich(ausDatei.Version, v.Version);
            Pruefe.Gleich(string.Join(",", ausDatei.Gene.Select(g => g.Kennung)), string.Join(",", v.Gene.Select(g => g.Kennung)));
            Pruefe.Wahr(v.Gene.Count is >= 30 and <= 40, $"{v.Gene.Count} Gene");
            foreach (var art in Enum.GetValues<Genart>())
                Pruefe.Wahr(v.GeneDerArt(art).Any(), $"Kein Gen der Art {art}");
            Pruefe.Gleich(v.Gene.Count, v.Gene.Select(g => g.Kennung).Distinct().Count());
            for (var i = 0; i < v.Gene.Count; i++) Pruefe.Gleich(i, v.Gene[i].Index);
            foreach (var gen in v.Gene)
                foreach (var volk in AlleVoelker)
                    Pruefe.Wahr(gen.Grenzen(volk).Max >= gen.Grenzen(volk).Min, $"{gen.Kennung}: Grenzen {volk}");
            // Ein paar Völker-Merkmale, die die Darstellung voraussetzen wird
            Pruefe.Gleich(0, v.Gen("hauer")!.Grenzen(Volk.Mensch).Max);
            Pruefe.Wahr(v.Gen("hauer")!.Grenzen(Volk.Ork).Mittel > 100, "Orks ohne Hauer");
            Pruefe.Wahr(v.Gen("ohren_spitze")!.Grenzen(Volk.Elb).Min > v.Gen("ohren_spitze")!.Grenzen(Volk.Mensch).Max, "Elbenohren nicht spitzer");
            Pruefe.Wahr(v.Gen("groesse")!.Grenzen(Volk.Zwerg).Max < v.Gen("groesse")!.Grenzen(Volk.Mensch).Min, "Zwerge nicht kleiner");
            Pruefe.Wahr(v.Korrelationen.Any(k => k.Von == "kiefer_breite" && k.Zu == "kinn" && k.Staerke > 0), "Kiefer → Kinn fehlt");
            // Die Alterskurve: vor dem ersten Punkt wie dort, dazwischen gerade, danach wie am letzten
            var nase = v.Gen("nase_laenge")!;
            Pruefe.Gleich("20:0,80:18", string.Join(",", nase.Alterskurve.Select(p => $"{p.Jahre}:{p.Verschiebung}")));
            Pruefe.Gleich(0, nase.Altersverschiebung(5));
            Pruefe.Gleich(9, nase.Altersverschiebung(50));
            Pruefe.Gleich(18, nase.Altersverschiebung(120));
            Pruefe.Gleich(0, v.Gen("kinn")!.Altersverschiebung(80));
            var e = Erbgut.Wuerfeln(v, 3, Volk.Mensch);
            Pruefe.Gleich(Math.Min(255, e.SichtbarerWert("nase_laenge") + 18), e.WertImAlter("nase_laenge", 90));
        }),

        ("Erbgut: Die Gen-Vorlage nennt Fehler verständlich (Grenzen, fehlendes Volk, doppelte Kennung, unbekanntes Gen in einer Korrelation, unbekanntes Feld, Gen für Frauen)", () =>
        {
            var gut = TestvorlageJson();
            Pruefe.Gleich(4, Erbgutvorlage.AusJson(gut).Gene.Count);
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(gut.Replace("\"min\": 40, \"mittel\": 100", "\"min\": 140, \"mittel\": 100")), "min ≤ mittel");
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(gut.Replace("\"Oger\": ", "\"Ork2\": ")), "Kein gültiges JSON");
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(gut.Replace("\"kennung\": \"folge\"", "\"kennung\": \"glocke\"")), "zweimal");
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(gut.Replace("\"kennung\": \"folge\"", "\"kennung\": \"Folge!\"")), "a–z");
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(gut.Replace("\"zu\": \"folge\"", "\"zu\": \"fluegel\"")), "„fluegel“ gibt es nicht");
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(gut.Replace("\"dominanz\": 10", "\"dominanz\": 140")), "Dominanz");
            var ohneOger = gut.Replace(", \"Oger\": { \"min\": 0, \"mittel\": 128, \"max\": 255, \"streuung\": 30 }", "");
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(ohneOger), "Grenzen für Oger fehlen");
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(gut.Replace("\"version\": 3", "\"version\": 0")), "version");
            // Unbekannte Felder sind Tippfehler und werden abgelehnt – im Gen, in den Grenzen, ganz oben
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(gut.Replace("\"dominanz\": 10", "\"domianz\": 10")), "'domianz'");
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(gut.Replace("\"dominanz\": 10", "\"domianz\": 10")), "unbekanntes Feld");
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(gut.Replace("\"streuung\": 20 }", "\"streuung\": 20, \"farbe\": 1 }")), "'farbe'");
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(gut.Replace("\"version\": 3,", "\"version\": 3, \"gen\": [],")), "'gen'");
            // Das Gen für Frauen muss es geben, und es darf nicht selbst auf ein weiteres zeigen
            var mitFrauen = gut.Replace("\"kennung\": \"glocke\",", "\"kennung\": \"glocke\", \"beiFrauen\": \"folge\",");
            Pruefe.Gleich("folge", Erbgutvorlage.AusJson(mitFrauen).Gen("glocke")!.BeiFrauen);
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(mitFrauen.Replace("\"beiFrauen\": \"folge\"", "\"beiFrauen\": \"bart\"")), "„bart“ für Frauen gibt es nicht");
            Pruefe.WirftMitMeldung(() => Erbgutvorlage.AusJson(mitFrauen.Replace("\"kennung\": \"folge\",", "\"kennung\": \"folge\", \"beiFrauen\": \"haar\",")),
                "darf selbst kein");
        }),

        ("Erbgut: Gleiche Eltern und gleicher Startwert ergeben dasselbe Kind, Byte für Byte – ein anderer Startwert ein anderes; Partien mit gleichem Startwert sind gleich", () =>
        {
            var v = Erbgutvorlage.Standard;
            var vater = Erbgut.Wuerfeln(v, 11, Volk.Mensch);
            var mutter = Erbgut.Wuerfeln(v, 12, Volk.Mensch);
            Pruefe.Gleich(vater.AlsText(), Erbgut.Wuerfeln(v, 11, Volk.Mensch).AlsText());
            Pruefe.Wahr(vater.AlsText() != mutter.AlsText(), "zwei Startwerte, dasselbe Erbgut");
            Pruefe.Gleich(4 * v.Gene.Count, vater.AlsText().Length);
            var kind = Erbgut.Vererben(v, 99, Volk.Mensch, vater, mutter);
            Pruefe.Gleich(kind.AlsText(), Erbgut.Vererben(v, 99, Volk.Mensch, vater, mutter).AlsText());
            Pruefe.Wahr(kind.AlsText() != Erbgut.Vererben(v, 100, Volk.Mensch, vater, mutter).AlsText(), "Geschwister gleich");
            Pruefe.Gleich(Erbgut.Wuerfeln(v, 5, Volk.Zwerg).AlsText(), Erbgut.Vererben(v, 5, Volk.Zwerg, null, null).AlsText());

            // Jedes Allel des Kindes stammt von einem Elternteil (bis auf die Mutation): eines vom Vater, eines von der Mutter
            foreach (var gen in v.Gene)
            {
                var a = new[] { kind.SichtbarerWert(gen), kind.VerdeckterWert(gen.Kennung) };
                bool Von(Erbgut e, int wert) => new[] { e.SichtbarerWert(gen), e.VerdeckterWert(gen.Kennung) }.Any(x => Math.Abs(x - wert) <= gen.Mutation);
                Pruefe.Wahr((Von(vater, a[0]) && Von(mutter, a[1])) || (Von(vater, a[1]) && Von(mutter, a[0])), $"{gen.Kennung}: {a[0]}/{a[1]} nicht von den Eltern");
            }

            // In der Partie: dieselben Personen mit demselben Erbgut, jede mit ihrem eigenen
            var (a1, _) = Partie(77);
            var (a2, _) = Partie(77);
            var (b, _) = Partie(78);
            Pruefe.Gleich(Bild(a1.Wirtschaft!), Bild(a2.Wirtschaft!));
            Pruefe.Wahr(Bild(a1.Wirtschaft!) != Bild(b.Wirtschaft!), "Startwert 78 wie 77");
            var personen = AllePersonen(a1.Wirtschaft!).ToList();
            Pruefe.Wahr(personen.Count >= 10, $"nur {personen.Count} Personen");
            Pruefe.Gleich(personen.Count, personen.Select(p => p.Erbgut.AlsText()).Distinct().Count());
            foreach (var p in personen.Where(p => p.Vater is null && p.Mutter is null)) // zu Spielbeginn kommen alle von außen
                Pruefe.Gleich(Erbgut.Wuerfeln(v, a1.Wirtschaft!.Familien.ErbgutStartwert(p.Nummer), p.Volk).AlsText(), p.Erbgut.AlsText(), p.Name);
        }),

        ("Erbgut: Kind mit nur einem Elternteil – ein Allel von ihm, das andere aus der Glockenkurve des Volkes; in den Grenzen, gleiche Bytes, im Spiel wie beim Aufnehmen", () =>
        {
            var v = Erbgutvorlage.Standard;
            foreach (var volk in AlleVoelker)
                foreach (var elternVolk in AlleVoelker)
                {
                    var elternteil = Erbgut.Wuerfeln(v, 600 + (int)elternVolk, elternVolk);
                    for (var i = 0; i < 30; i++)
                    {
                        var nurVater = Erbgut.Vererben(v, 1000 + i, volk, elternteil, null);
                        var nurMutter = Erbgut.Vererben(v, 1000 + i, volk, null, elternteil);
                        Pruefe.Gleich(nurVater.AlsText(), Erbgut.Vererben(v, 1000 + i, volk, elternteil, null).AlsText());
                        Pruefe.Gleich(nurMutter.AlsText(), Erbgut.Vererben(v, 1000 + i, volk, null, elternteil).AlsText());
                        InGrenzen(nurVater, volk, $"Kind ({volk}) eines {elternVolk.Einzahl()}s ohne Mutter");
                        InGrenzen(nurMutter, volk, $"Kind ({volk}) einer {elternVolk.Einzahl()} ohne Vater");
                        if (volk != elternVolk) continue;
                        // Gleiches Volk: Je Gen stammt mindestens ein Allel (bis auf die Mutation) vom bekannten Elternteil
                        foreach (var gen in v.Gene)
                            foreach (var kind in new[] { nurVater, nurMutter })
                            {
                                var eltern = new[] { elternteil.SichtbarerWert(gen), elternteil.VerdeckterWert(gen.Kennung) };
                                Pruefe.Wahr(new[] { kind.SichtbarerWert(gen), kind.VerdeckterWert(gen.Kennung) }
                                    .Any(a => eltern.Any(e => Math.Abs(a - e) <= gen.Mutation)), $"{gen.Kennung}: kein Allel vom Elternteil");
                            }
                    }
                }
            // Das zweite Allel kommt aus dem Volk, nicht vom Elternteil: Über viele Kinder streut es wie die Glockenkurve
            var mensch = Erbgut.Wuerfeln(v, 77, Volk.Mensch);
            var kinder = Enumerable.Range(0, 400).Select(i => Erbgut.Vererben(v, 5000 + i, Volk.Mensch, mensch, null)).ToList();
            var kinn = v.Gen("kinn")!;
            var fremd = kinder.Select(k => new[] { k.SichtbarerWert(kinn), k.VerdeckterWert("kinn") })
                .Select(a => Math.Abs(a[0] - mensch.SichtbarerWert(kinn)) <= 3 || Math.Abs(a[0] - mensch.VerdeckterWert("kinn")) <= 3 ? a[1] : a[0]).ToList();
            Pruefe.Wahr(fremd.Distinct().Count() > 30, $"Das zweite Allel streut nicht ({fremd.Distinct().Count()} Werte)");

            // Im Spiel: ein Kind, das das Oberhaupt allein ins Haus bringt (KindAufnehmen), erbt genau so
            var (sim, spieler) = Partie(91);
            var b = sim.Wirtschaft!.Familien;
            var o = b.Von(spieler)!.Oberhaupt;
            var aufgenommen = b.KindAufnehmen(spieler, 3);
            var vater = o.Geschlecht == Geschlecht.Mann ? o.Erbgut : null;
            var mutter = o.Geschlecht == Geschlecht.Frau ? o.Erbgut : null;
            Pruefe.Gleich(Erbgut.Vererben(v, b.ErbgutStartwert(aufgenommen.Nummer), aufgenommen.Volk, vater, mutter).AlsText(), aufgenommen.Erbgut.AlsText());
            InGrenzen(aufgenommen.Erbgut, aufgenommen.Volk, aufgenommen.Name);
            var (kopie, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler, Zeitpunkt));
            Pruefe.Gleich(Bild(sim.Wirtschaft!), Bild(kopie.Wirtschaft!));
        }),

        ("Erbgut: Zwerginnen tragen Bart – meist leicht (kurz oder geflochten), selten voll; Frauen anderer Völker keinen, Männer wie bisher", () =>
        {
            var v = Erbgutvorlage.Standard;
            Pruefe.Gleich("bartwuchs_frau", v.Gen("bartwuchs")!.BeiFrauen);
            var zwerge = Enumerable.Range(0, 2000).Select(i => Erbgut.Wuerfeln(v, 40_000 + i, Volk.Zwerg)).ToList();
            var frauen = zwerge.Select(e => e.SichtbarerWert("bartwuchs", Geschlecht.Frau)).ToList();
            var leicht = frauen.Count(x => x is >= 40 and < 160) / 2000.0;
            var voll = frauen.Count(x => x >= 160) / 2000.0;
            Pruefe.Wahr(leicht >= 0.8, $"Nur {leicht:P0} der Zwerginnen mit leichtem Bart");
            Pruefe.Wahr(voll is >= 0.003 and <= 0.05, $"Vollbart bei {voll:P1} der Zwerginnen (selten heißt 0,3–5 %)");
            Pruefe.Wahr(Mittel(frauen) < Mittel(zwerge.Select(e => e.SichtbarerWert("bartwuchs", Geschlecht.Mann))) - 80, "Zwerginnen so bärtig wie Zwerge");
            foreach (var e in zwerge.Take(50))
            {
                Pruefe.Gleich(e.SichtbarerWert("bartwuchs"), e.SichtbarerWert("bartwuchs", Geschlecht.Mann));
                Pruefe.Gleich(e.SichtbarerWert("bartwuchs_frau"), e.SichtbarerWert("bartwuchs", Geschlecht.Frau));
                Pruefe.Gleich(e.SichtbarerWert("kinn"), e.SichtbarerWert("kinn", Geschlecht.Frau)); // ohne „beiFrauen“: dasselbe Gen
                Pruefe.Gleich(e.SichtbarerWert("bartwuchs_frau"), e.WertImAlter("bartwuchs", 60, Geschlecht.Frau));
            }
            foreach (var volk in AlleVoelker.Where(x => x != Volk.Zwerg))
                Pruefe.Wahr(Enumerable.Range(0, 200).All(i => Erbgut.Wuerfeln(v, i, volk).SichtbarerWert("bartwuchs", Geschlecht.Frau) == 0), $"Bärtige Frau ({volk})");
            // Vererbt wird der Frauenbart wie jedes Gen – auch über den Vater; die Menschentochter einer Zwergin hat keinen
            var zwergin = zwerge.First(e => e.SichtbarerWert("bartwuchs_frau") >= 120 && e.VerdeckterWert("bartwuchs_frau") >= 120);
            var toechter = Enumerable.Range(0, 200).Select(i => Erbgut.Vererben(v, i, Volk.Zwerg, zwerge[i + 100], zwergin)).ToList();
            Pruefe.Wahr(toechter.Average(t => t.SichtbarerWert("bartwuchs", Geschlecht.Frau)) > 100, "Töchter einer bärtigen Zwergin ohne mehr Bart");
            Pruefe.Wahr(Enumerable.Range(0, 50).All(i => Erbgut.Vererben(v, i, Volk.Mensch, Erbgut.Wuerfeln(v, 300 + i, Volk.Mensch), zwergin)
                .SichtbarerWert("bartwuchs", Geschlecht.Frau) == 0), "Menschentochter mit Bart");
        }),

        ("Erbgut: Volksgrenzen halten – je Volk 1000 gewürfelte Personen und ihre Kinder, auch aus Ehen zwischen Völkern", () =>
        {
            var v = Erbgutvorlage.Standard;
            foreach (var volk in AlleVoelker)
            {
                var leute = Enumerable.Range(0, 1000).Select(i => Erbgut.Wuerfeln(v, 5000 + i, volk)).ToList();
                foreach (var (e, i) in leute.Select((e, i) => (e, i))) InGrenzen(e, volk, $"Person {i}");
                foreach (var kindVolk in AlleVoelker)
                    for (var i = 0; i < 100; i++)
                        InGrenzen(Erbgut.Vererben(v, 9000 + i, kindVolk, leute[i], Erbgut.Wuerfeln(v, 7000 + i, kindVolk)), kindVolk,
                            $"Kind ({kindVolk}) eines {volk.Einzahl()}s");
            }
            // Ein Menschenkind einer Elbin hat Ohren am spitzen Rand der Menschen, aber keine Elbenohren
            var elbin = Erbgut.Wuerfeln(v, 31, Volk.Elb);
            var kinder = Enumerable.Range(0, 200).Select(i => Erbgut.Vererben(v, i, Volk.Mensch, Erbgut.Wuerfeln(v, 400 + i, Volk.Mensch), elbin)).ToList();
            var spitze = v.Gen("ohren_spitze")!.Grenzen(Volk.Mensch);
            Pruefe.Wahr(kinder.All(k => k.SichtbarerWert("ohren_spitze") <= spitze.Max), "Elbenohren beim Menschenkind");
            Pruefe.Wahr(kinder.Count(k => k.SichtbarerWert("ohren_spitze") >= spitze.Max - 4) > 40, "Kein Kind mit spitzeren Ohren");
        }),

        ("Erbgut: Die Verteilung ist plausibel – 1000 Personen: Mittelwert, Streuung, nichts außerhalb ±2 Streuungen, Korrelation wirkt", () =>
        {
            var t = Testvorlage.Value;
            var leute = Enumerable.Range(0, 1000).Select(i => Erbgut.Wuerfeln(t, 300 + i, Volk.Mensch)).ToList();
            var glocke = leute.SelectMany(e => new[] { e.SichtbarerWert("glocke"), e.VerdeckterWert("glocke") }).ToList();
            Pruefe.Ungefaehr(100, Mittel(glocke), 0.02);
            // Abgeschnitten bei ±2 Streuungen: Die Streuung ist dann das 0,88-Fache (statt 1 bei der ganzen Glockenkurve)
            Pruefe.Ungefaehr(0.88 * 20, Streuung(glocke), 0.08);
            Pruefe.Wahr(glocke.All(x => x is >= 60 and <= 140), $"Wert außerhalb 60–140: {glocke.Min()}–{glocke.Max()}");
            Pruefe.Wahr(glocke.Min() <= 63 && glocke.Max() >= 137, $"Die Ränder fehlen: {glocke.Min()}–{glocke.Max()}");
            Pruefe.Wahr(glocke.Count(x => Math.Abs(x - 100) <= 20) is > 1300 and < 1550, "nicht etwa 72 % innerhalb einer Streuung");
            var auswahl = leute.Select(e => e.SichtbarerWert("auswahl")).ToList();
            Pruefe.Ungefaehr(127.5, Mittel(auswahl), 0.05);
            Pruefe.Wahr(auswahl.Min() < 8 && auswahl.Max() > 247, "Auswahl nicht über den ganzen Bereich");
            // folge = eigene Glocke + 0,8 × (glocke − 100): Mittel bleibt, Zusammenhang deutlich
            var folge = leute.Select(e => e.SichtbarerWert("folge")).ToList();
            var sicht = leute.Select(e => e.SichtbarerWert("glocke")).ToList();
            Pruefe.Ungefaehr(128, Mittel(folge), 0.02);
            var kov = sicht.Zip(folge).Sum(p => (p.First - Mittel(sicht)) * (p.Second - Mittel(folge))) / sicht.Count;
            Pruefe.Wahr(kov / (Streuung(sicht) * Streuung(folge)) is > 0.5 and < 0.8, "Korrelation glocke → folge");

            // Die echte Vorlage: je Volk und Gen (mit Streuung ab 8) Mittel und Streuung der Allele nahe an den Daten
            var v = Erbgutvorlage.Standard;
            foreach (var volk in AlleVoelker)
            {
                var echte = Enumerable.Range(0, 1000).Select(i => Erbgut.Wuerfeln(v, 20_000 + i, volk)).ToList();
                foreach (var gen in v.Gene.Where(g => g.Verteilung == Genverteilung.Normal && g.Grenzen(volk).Streuung >= 8))
                {
                    var g = gen.Grenzen(volk);
                    var werte = echte.SelectMany(e => new[] { e.SichtbarerWert(gen), e.VerdeckterWert(gen.Kennung) }).ToList();
                    Pruefe.Wahr(Math.Abs(Mittel(werte) - g.Mittel) <= 0.2 * g.Streuung, $"{volk} {gen.Kennung}: Mittel {Mittel(werte):F1} statt {g.Mittel}");
                    var s = Streuung(werte) / g.Streuung;
                    Pruefe.Wahr(s is >= 0.7 and <= 1.2, $"{volk} {gen.Kennung}: Streuung {s:F2} × {g.Streuung}");
                }
            }
        }),

        ("Erbgut: Dominanz – bei Dominanz 10 ist von zwei verschiedenen Allelen zu etwa 90 % das niedrigere sichtbar, bei 50 halb und halb", () =>
        {
            var t = Testvorlage.Value;
            var leute = Enumerable.Range(0, 4000).Select(i => Erbgut.Wuerfeln(t, i, Volk.Mensch)).ToList();
            var verschieden = leute.Where(e => e.SichtbarerWert("haar") != e.VerdeckterWert("haar")).ToList();
            Pruefe.Ungefaehr(0.9, verschieden.Count(e => e.SichtbarerWert("haar") < e.VerdeckterWert("haar")) / (double)verschieden.Count, 0.03);
            var glocke = leute.Where(e => e.SichtbarerWert("glocke") != e.VerdeckterWert("glocke")).ToList();
            Pruefe.Ungefaehr(0.5, glocke.Count(e => e.SichtbarerWert("glocke") < e.VerdeckterWert("glocke")) / (double)glocke.Count, 0.06);
        }),

        ("Erbgut: Merkmale der Großeltern tauchen wieder auf – blonde Enkel zweier dunkelhaariger Eltern (rezessiv), statistisch wie erwartet", () =>
        {
            var t = Testvorlage.Value;
            // Zuerst eine Großmutter ganz hell und einen Großvater ganz dunkel suchen
            var hell = Enumerable.Range(0, 20_000).Select(i => Erbgut.Wuerfeln(t, i, Volk.Mensch))
                .First(e => e.SichtbarerWert("haar") >= 200 && e.VerdeckterWert("haar") >= 200);
            var dunkel = Enumerable.Range(0, 20_000).Select(i => Erbgut.Wuerfeln(t, i, Volk.Mensch))
                .First(e => e.SichtbarerWert("haar") <= 40 && e.VerdeckterWert("haar") <= 40);
            // Ihre Kinder tragen ein helles und ein dunkles Allel; das dunkle setzt sich meist durch
            var kinder = Enumerable.Range(0, 400).Select(i => Erbgut.Vererben(t, 50_000 + i, Volk.Mensch, dunkel, hell)).ToList();
            Pruefe.Wahr(kinder.All(k => Math.Min(k.SichtbarerWert("haar"), k.VerdeckterWert("haar")) <= 42
                                       && Math.Max(k.SichtbarerWert("haar"), k.VerdeckterWert("haar")) >= 198), "Kinder ohne je ein Allel der Großeltern");
            var dunkleKinder = kinder.Where(k => k.SichtbarerWert("haar") <= 42).ToList();
            Pruefe.Ungefaehr(0.9, dunkleKinder.Count / 400.0, 0.06);
            // Zwei dunkelhaarige Kinder bekommen Enkel: 1/4 tragen zwei helle Allele (immer hell), 1/2 eines (zu 10 % hell) → 30 % blond
            var enkel = Enumerable.Range(0, 4000)
                .Select(i => Erbgut.Vererben(t, 90_000 + i, Volk.Mensch, dunkleKinder[i % 50], dunkleKinder[50 + i % 50])).ToList();
            var blond = enkel.Count(e => e.SichtbarerWert("haar") >= 190) / 4000.0;
            Pruefe.Ungefaehr(0.30, blond, 0.1);
            Pruefe.Wahr(dunkleKinder.All(k => k.SichtbarerWert("haar") < 190), "Ein Elternteil ist schon blond");
        }),

        ("Erbgut: Kinder im Spiel erben von ihren Eltern; Speichern und Laden behält jedes Byte, die Prüfsumme sieht das Erbgut", () =>
        {
            var (sim, spieler) = Partie(202);
            var w = sim.Wirtschaft!;
            for (var i = 0; i < 15; i++) JahrBeenden(sim);
            var personen = AllePersonen(w).ToDictionary(p => p.Nummer);
            var kinder = personen.Values.Where(p => p.Vater is { } v && p.Mutter is { } m && personen.ContainsKey(v) && personen.ContainsKey(m)).ToList();
            Pruefe.Wahr(kinder.Count >= 3, $"nur {kinder.Count} Kinder in 15 Jahren");
            var vorlage = w.Familien.Erbgutvorlage;
            foreach (var k in kinder)
                Pruefe.Gleich(Erbgut.Vererben(vorlage, w.Familien.ErbgutStartwert(k.Nummer), k.Volk, personen[k.Vater!.Value].Erbgut,
                    personen[k.Mutter!.Value].Erbgut).AlsText(), k.Erbgut.AlsText(), k.Name);
            foreach (var p in personen.Values) InGrenzen(p.Erbgut, p.Volk, p.Name);

            var json = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            var knoten = JsonNode.Parse(json)!["Wirtschaft"]!["Familien"]!;
            Pruefe.Gleich(vorlage.Version, (int)knoten["ErbgutVersion"]!);
            Pruefe.Gleich(vorlage.Gene.Count, knoten["ErbgutGene"]!.AsArray().Count);
            var (kopie, kopieSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(Bild(w), Bild(kopie.Wirtschaft!));
            Pruefe.Gleich(json, Speicherung.Speichern(kopie, kopieSpieler, Zeitpunkt));
            for (var i = 0; i < 5; i++) { JahrBeenden(sim); JahrBeenden(kopie); }
            Pruefe.Gleich(Bild(w), Bild(kopie.Wirtschaft!));

            // Die Prüfsumme des Zustands ändert sich, wenn sich nur ein Erbgut ändert
            var vorher = Zustandspruefsumme.Berechnen(sim);
            Pruefe.Wahr(vorher is not null, "Keine Prüfsumme (Ratssitzung?)");
            var oberhaupt = w.Familien.Von(spieler)!.Oberhaupt;
            oberhaupt.Erbgut = Erbgut.Wuerfeln(vorlage, 4242, oberhaupt.Volk);
            Pruefe.Wahr(vorher != Zustandspruefsumme.Berechnen(sim), "Die Prüfsumme sieht das Erbgut nicht");
        }),

        ("Erbgut: Ein alter Spielstand ohne Erbgut lädt – fest aus dem Startwert jeder Person, Kinder erben von ihren Eltern, alle anderen Würfe bleiben", () =>
        {
            var json = AlterStand();
            var alt = JsonNode.Parse(json)!.AsObject();
            var altFamilien = alt["Wirtschaft"]!["Familien"]!;
            Pruefe.Wahr(altFamilien["ErbgutGene"] is null && altFamilien["Personen"]!.AsArray().All(p => p!["Erbgut"] is null), "Der alte Spielstand hat schon Erbgut");
            var (sim, spieler) = Speicherung.Laden(json);
            var w = sim.Wirtschaft!;
            var b = w.Familien;
            var personen = AllePersonen(w).ToDictionary(p => p.Nummer);
            Pruefe.Wahr(personen.Count >= 10, $"nur {personen.Count} Personen");
            foreach (var p in personen.Values)
            {
                InGrenzen(p.Erbgut, p.Volk, p.Name);
                var vater = p.Vater is int v ? personen.GetValueOrDefault(v) : null;
                var mutter = p.Mutter is int m ? personen.GetValueOrDefault(m) : null;
                Pruefe.Gleich(Erbgut.Vererben(b.Erbgutvorlage, b.ErbgutStartwert(p.Nummer), p.Volk, vater?.Erbgut, mutter?.Erbgut).AlsText(),
                    p.Erbgut.AlsText(), p.Name);
            }
            Pruefe.Gleich(personen.Count, personen.Values.Select(p => p.Erbgut.AlsText()).Distinct().Count());
            var (wieder, _) = Speicherung.Laden(json);
            Pruefe.Gleich(Bild(w), Bild(wieder.Wirtschaft!));

            var neu = JsonNode.Parse(Speicherung.Speichern(sim, spieler))!["Wirtschaft"]!["Familien"]!;
            foreach (var feld in new[] { "ZufallWuerfe", "TalentWuerfe", "AussehenWuerfe" })
                Pruefe.Gleich((long?)altFamilien[feld] ?? 0, (long)neu[feld]!, feld);
            Pruefe.Gleich(personen.Count, neu["Personen"]!.AsArray().Count(p => p!["Erbgut"] is not null));
        }),

        ("Erbgut: Laden mit unbekannten und fehlenden Genen – Unbekanntes fällt weg, Fehlendes entsteht fest aus dem Startwert, das Übrige bleibt", () =>
        {
            var (sim, spieler) = Partie(55);
            for (var i = 0; i < 12; i++) JahrBeenden(sim);
            var w = sim.Wirtschaft!;
            var vorlage = w.Familien.Erbgutvorlage;
            var json = Speicherung.Speichern(sim, spieler, Zeitpunkt);

            // Im Spielstand: „nase_laenge“ heißt „fluegel“ (das gibt es nicht, nase_laenge fehlt also), „kinn“ fehlt ganz
            var stand = JsonNode.Parse(json)!.AsObject();
            var familien = stand["Wirtschaft"]!["Familien"]!;
            var gene = familien["ErbgutGene"]!.AsArray().Select(g => (string)g!).ToList();
            var kinn = gene.IndexOf("kinn");
            var nase = gene.IndexOf("nase_laenge");
            Pruefe.Wahr(kinn >= 0 && nase >= 0, "Gene nicht im Spielstand");
            var neueGene = gene.Select(g => g == "nase_laenge" ? "fluegel" : g).Where(g => g != "kinn").ToList();
            familien["ErbgutGene"] = new JsonArray(neueGene.Select(g => (JsonNode)JsonValue.Create(g)!).ToArray());
            foreach (var p in familien["Personen"]!.AsArray())
            {
                var text = (string)p!["Erbgut"]!;
                p["Erbgut"] = text.Remove(4 * kinn, 4);
            }
            var veraendert = stand.ToJsonString(Speicherung.Optionen);

            var (geladen, _) = Speicherung.Laden(veraendert);
            var (nochmal, _) = Speicherung.Laden(veraendert);
            Pruefe.Gleich(Bild(geladen.Wirtschaft!), Bild(nochmal.Wirtschaft!));
            var b = geladen.Wirtschaft!.Familien;
            var personen = AllePersonen(geladen.Wirtschaft!).ToDictionary(p => p.Nummer);
            Pruefe.Wahr(personen.Count >= 10, $"nur {personen.Count} Personen");
            Pruefe.Wahr(personen.Values.Any(p => p.Vater is not null && p.Mutter is not null), "Kein Kind im Spielstand");
            foreach (var p in personen.Values)
            {
                InGrenzen(p.Erbgut, p.Volk, p.Name);
                // Fehlendes entsteht wie bei der Geburt: ohne Eltern gewürfelt (mit der Korrelation zum bekannten Kiefer), Kinder erben
                var vater = p.Vater is int v ? personen.GetValueOrDefault(v) : null;
                var mutter = p.Mutter is int m ? personen.GetValueOrDefault(m) : null;
                Pruefe.Gleich(Erbgut.Vererben(vorlage, b.ErbgutStartwert(p.Nummer), p.Volk, vater?.Erbgut, mutter?.Erbgut).AlsText(), p.Erbgut.AlsText(), p.Name);
            }
            // Deshalb kommt sogar genau das alte Erbgut heraus: Die übrigen Gene standen im Spielstand, Kinn und Nase entstehen aus demselben Startwert neu
            Pruefe.Gleich(Bild(w), Bild(geladen.Wirtschaft!));
            // Beim nächsten Speichern steht wieder die volle Genliste drin
            Pruefe.Gleich(vorlage.Gene.Count, JsonNode.Parse(Speicherung.Speichern(geladen, null, Zeitpunkt))!["Wirtschaft"]!["Familien"]!["ErbgutGene"]!.AsArray().Count);

            // Kaputtes Erbgut (falsche Länge, keine Hexziffern) ist ein Fehler im Spielstand
            var ersteZeile = ((string)familien["Personen"]!.AsArray()[0]!["Erbgut"]!);
            familien["Personen"]!.AsArray()[0]!["Erbgut"] = ersteZeile[..^4];
            Pruefe.WirftMitMeldung(() => Speicherung.Laden(stand.ToJsonString(Speicherung.Optionen)), "Allele");
            familien["Personen"]!.AsArray()[0]!["Erbgut"] = "XY" + ersteZeile[2..];
            Pruefe.WirftMitMeldung(() => Speicherung.Laden(stand.ToJsonString(Speicherung.Optionen)), "Hexadezimal");
        }),

        ("Erbgut: Entwicklerbefehl „Erbgut neu würfeln“ – ein anderes Erbgut in den Volksgrenzen, auf jedem Rechner gleich, gesperrt ohne Entwicklerhilfe", () =>
        {
            (Simulation, Konto) Neu(bool erlaubt)
            {
                var (sim, spieler) = Partie(1400);
                for (var i = 0; i < 300; i++) sim.Tick();
                sim.EntwicklerErlaubt = erlaubt;
                return (sim, spieler);
            }
            var (a, spielerA) = Neu(true);
            var (b, spielerB) = Neu(true);
            var o = a.Wirtschaft!.Familien.Von(spielerA)!.Oberhaupt;
            var vorher = o.Erbgut.AlsText();
            var e = a.Bote.SofortAusfuehren(spielerA, new EntwicklerErbgut(o.Nummer));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Wahr(o.Erbgut.AlsText() != vorher, "Erbgut gleich geblieben");
            InGrenzen(o.Erbgut, o.Volk, o.Name);
            Pruefe.Wahr(b.Bote.SofortAusfuehren(spielerB, new EntwicklerErbgut(o.Nummer)).Erfolg, "Lauf B");
            Pruefe.Gleich(o.Erbgut.AlsText(), b.Wirtschaft!.Familien.Von(spielerB)!.Oberhaupt.Erbgut.AlsText());
            var zweites = o.Erbgut.AlsText();
            Pruefe.Wahr(a.Bote.SofortAusfuehren(spielerA, new EntwicklerErbgut(o.Nummer)).Erfolg, "zweiter Wurf");
            Pruefe.Wahr(o.Erbgut.AlsText() != zweites && o.Erbgut.AlsText() != vorher, "Der zweite Wurf wiederholt einen früheren");
            var kandidat = a.Wirtschaft!.Familien.Kandidaten[0].Person;
            Pruefe.Wahr(a.Bote.SofortAusfuehren(spielerA, new EntwicklerErbgut(kandidat.Nummer)).Erfolg, "Kandidat");
            Pruefe.Wahr(!a.Bote.SofortAusfuehren(spielerA, new EntwicklerErbgut(999_999)).Erfolg, "Person 999999");

            var (gesperrt, spielerG) = Neu(false);
            var og = gesperrt.Wirtschaft!.Familien.Von(spielerG)!.Oberhaupt;
            var alt = og.Erbgut.AlsText();
            var abgelehnt = gesperrt.Bote.SofortAusfuehren(spielerG, new EntwicklerErbgut(og.Nummer));
            Pruefe.Wahr(!abgelehnt.Erfolg && abgelehnt.Meldung.Contains("gesperrt"), abgelehnt.Meldung);
            Pruefe.Gleich(alt, og.Erbgut.AlsText());
        }),
    };
}
