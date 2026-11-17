using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Zeit;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Kartenbau;
using Reichsgilde.Kartenbau.Staedte;

// ─────────────────────────────────────────────────────────────────────────────
//  Kartenwerkstatt, Schritt 4 (PR #174): die Prüfungen aus dem alten Python-Werkzeug in C# (Kapazität, Katalog gegen die Modelle), das
//  Kartenskript tools/Kartenbau (Stadtskript Probestadt, Schreibschutz, Vorschau) und Wege, die einem Tor folgen, wenn die Mauer sich ändert.
//  Die erwarteten Zahlen für Köln und den Katalog lieferte tools/karten/kapazitaet.py bzw. katalog_pruefen.py am 22.10.2026 (vor dem Löschen).
// ─────────────────────────────────────────────────────────────────────────────

static class KartenbauTests
{
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static string Modellordner => Path.Combine(Path.GetDirectoryName(Datenordner)!, "assets", "modelle");

    static Kartenentwurf Oeffnen(string kennung)
    {
        var e = Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value);
        e.Daten = Daten.Value;
        return e;
    }

    static Kartenort O(double x, double z) => new(x, z);
    static string Liste(IEnumerable<Befund> befunde) => string.Join("\n  ", befunde.Select(b => b.ToString()));
    static List<Befund> Fehler(Kartenentwurf e) => e.Befunde.Where(b => b.Schwere == Schwere.Fehler).ToList();

    /// <summary>Eine kleine Stadt mit Mauer, Tor im Osten und einer Straße, die mit einem Stützpunkt in der Tormitte hindurchläuft.</summary>
    static (Kartenentwurf E, EntwurfMauer Mauer, EntwurfWeg Weg) MitTorweg()
    {
        var e = Kartenentwurf.Neu("probe", "Probe", 300, 300, Katalog.Value);
        var mauer = e.MauerAnlegen(new[] { O(-60, -60), O(60, -60), O(60, 60), O(-60, 60) }, geschlossen: true);
        e.TorSetzen(mauer, 60, 10, "Osttor");
        var weg = e.WegAnlegen(Wegklasse.Land, new[] { O(20, 10), O(60.5, 10.5), O(110, 10) }, glatt: false, name: "Landstraße");
        Pruefe.Gleich(O(60, 10), weg.Punkte[1], "der Punkt dazwischen rastet in die Tormitte");
        return (e, mauer, weg);
    }

    static bool Torfehler(Kartenentwurf e) => e.Befunde.Any(b => b.Regel == "tor");

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        // ── Kapazität ────────────────────────────────────────────────────────

        ("Kapazität Köln (Stadtskript, PR #213; Märkte an den Enden der Mauer und in Deutz seit 31.10.2026, eine Hausreihe vor dem Tor und Werkstatt-Bauplätze nahe den Märkten und Häuser in der Deutzer Südhälfte seit 1.11.2026, die romanischen Kirchen als Modelle seit 9.11.2026, Rathaus, Stapelhaus und Rheinkran als Modelle und das Rathaus am Alter Markt seit 9.11.2026, die Hofstellen seit 9.11.2026, Köln mit nur drei Kirchen seit 11.11.2026, die Startgebäude in der Pflicht seit 16.11.2026): 8.495 Felder, Pflicht und Reserve passen – 75 % frei nach der Pflicht", () =>
        {
            var k = Kapazitaetspruefung.Pruefen(Oeffnen("koeln"));
            Pruefe.Gleich(string.Join("\n", new[]
            {
                "Stadt-Bauzonen: 8495 Felder, davon fest belegt 0",   // seit 16.11.2026 ohne die Werkstatt- und Wohnhausplätze (vorher 1.133 Felder unter den 32 alten Plätzen)
                "Pflicht Werkstatt am Start (Kern und Mauerring): 8 von 8 passen", "Pflicht Wohnhaus am Start (Kern und Mauerring): 8 von 8 passen", // seit 16.11.2026
                "Pflicht Werkstatt: 16 von 16 passen", // bis 16.11.2026 acht auf freien Hofstellen, acht in den Zonen
                "Pflicht Ratsherrenhaus: 13 von 13 passen", "Pflicht Einwohner-Bürgerhaus: 3 von 3 passen",
                "Pflicht Einwohner-Wohnhaus: 40 von 40 passen", "Reserve Stadthof: 4 von 4 passen", "Reserve Stadtpalais: 8 von 8 passen",
                "Frei nach der Pflicht: 6375 Felder (75%), nach der Reserve: 5911 Felder", // ohne die Startgebäude 6.983 (82 %) und 6.519, mit den alten Plätzen 6.298 (74 %) und 5.834
            }), string.Join("\n", k.Bericht));
            Pruefe.Gleich(0, k.Befunde.Count);
            // Die gesetzten Gebäude: alle 100 aus Pflicht und Reserve (8 + 16 Werkstätten, 8 Wohnhäuser am Start, 56 Häuser der Pflicht, 12 der Reserve); feste keine mehr
            Pruefe.Gleich(100, k.Plaetze.Count(p => p.Art != "fest"));
            Pruefe.Gleich(24 * 56, k.Plaetze.Where(p => p.Art == "Werkstatt").Sum(p => p.Felder.Count));
            Pruefe.Gleich(8 * 20, k.Plaetze.Where(p => p.Art == "Wohnhaus").Sum(p => p.Felder.Count));
            Pruefe.Gleich(0, k.Plaetze.Count(p => p.Art == "fest"));
        }),

        ("Kapazität aller Karten: dieselben Plätze wie vor der Beschleunigung (27.10.2026) – Anzahl, Felder und eine Prüfsumme ihrer Lage", () =>
        {
            var erwartet = new[]
            {
                // Köln seit den drei Marktplätzen (31.10.2026) neu, vorher 86 / 1.980 / −1.223.276; seit der gemeinsamen Strecke (Startgrundstücke 36 m vom Platz) die Prüfsumme neu, vorher −11.184.073;
                // seit den Märkten an den Enden der Mauer und in Deutz die Prüfsumme neu, vorher −12.931.232; seit der Hausreihe vor dem Tor (1.11.2026) die
                // Prüfsumme neu, vorher −4.666.812; seit den Werkstatt-Bauplätzen nahe den Märkten (1.11.2026) die Prüfsumme neu, vorher −8.247.264, und mit „nur werkstatt“ (keine Wohnhäuser dort) noch einmal, vorher −6.835.905; seit den Häusern in der Deutzer Südhälfte (1.11.2026) noch einmal, vorher −6.192.870; Greybox mit dem Weinmarktgässchen ein Platz mehr, vorher 22 / 698 / −2.115.343;
                // seit den Bauzonen aus dem Raster (4.11.2026; Köln unverändert) Greybox vorher 4 / 23 / 714 / −2.170.301, Probestadt vorher 0 / 80 / 1.752 / 7.598.432;
                // seit den Kirchenmodellen in Köln (9.11.2026) die Prüfsumme neu, vorher −5.342.922; seit Rathaus, Stapelhaus und Rheinkran (Paket 3) noch einmal, vorher −7.195.386, und seit dem Rathaus am Alter Markt, vorher −8.093.550
                // seit Köln ohne St. Severin und St. Kunibert (11.11.2026) Köln vorher 0 / 76 / 1.528 / −5.176.768, die Plätze bleiben gleich; seit den Hofstellen (9.11.2026): Köln vorher 0 / 84 / 1.976 / −7.853.157 (die sechs Werkstatt-Bauplätze und zwei Hafenzonen sind Hofstellen, 8 Pflicht-Werkstätten
                // stehen auf freien Hofstellen), Messstadt vorher 0 / 80 / 1.752 / 6.597.050 (9 Hofstellen: eine nimmt eine Pflicht-Werkstatt auf)
                // seit „nur noch Bauzonen“ (16.11.2026; die Flächen der alten Plätze gehören zur Zone und zählen als fest belegt): Köln vorher 0 / 76 / 1.528 / −7.275.443,
                // Greybox vorher 4 / 26 / 901 / −2.835.284, Probestadt vorher 0 / 84 / 1.976 / 12.679.538; Messstadt unverändert
                // seit dem Aufräumen (16.11.2026; keine Plätze mehr, also nichts fest belegt; Greybox ohne die zwei Gassen in K4 und mit dem Hof als Vorstadt,
                // Messstadt mit Zonen im Stadtinneren): Köln vorher 0 / 108 / 2.671 / −9.672.404, Greybox 4 / 41 / 1.327 / −5.675.165,
                // Probestadt 0 / 94 / 2.312 / 6.589.340, Messstadt 0 / 79 / 1.696 / 5.021.759
                // seit den Startgebäuden in der Pflicht (16.11.2026): Köln vorher 0 / 84 / 1.976 / 12.452.268, Greybox 2 / 52 / 1.285 / −3.610.854,
                // Probestadt 0 / 84 / 1.976 / 6.241.256, Messstadt 0 / 84 / 1.976 / −13.917.464
                ("koeln", 0, 100, 2584, 2856704L), ("greybox", 3, 46, 1319, -6144997L), ("probestadt", 0, 100, 2584, -5967037L), ("messstadt", 0, 100, 2584, -28830371L),
            };
            var gemessen = erwartet.Select(x =>
            {
                var k = Kapazitaetspruefung.Pruefen(Oeffnen(x.Item1));
                // Reihenfolge und Lage: jeder Platz gewichtet mit seiner Stelle
                return (x.Item1, k.Befunde.Count, k.Plaetze.Count, k.Plaetze.Sum(p => p.Felder.Count), k.Plaetze.Select((p, n) => (long)(n + 1) * p.Felder.Sum(f => f.I * 7 + f.J * 13)).Sum());
            }).ToList();
            Pruefe.Gleich(string.Join("\n", erwartet), string.Join("\n", gemessen));
        }),

        ("Kapazität: Köln prüft sie als Fehler, die Greybox gar nicht (dort der Probeplan), neue Karten als Hinweis", () =>
        {
            Pruefe.Gleich(Schwere.Fehler, Pruefregeln.Koeln.Kapazitaet);
            Pruefe.Gleich(null, Pruefregeln.Greybox.Kapazitaet);
            Pruefe.Gleich(Schwere.Hinweis, Pruefregeln.Standard.Kapazitaet);
            var e = Kartenentwurf.Neu("neu", "Neu", 300, 300, Katalog.Value);
            e.WegAnlegen(Wegklasse.Land, new[] { O(-100, 0), O(100, 0) }, glatt: false);
            e.BauzoneAnlegen(Lage.Vorstadt, new[] { O(-40, 4), O(40, 4), O(40, 20), O(-40, 20) });
            var kap = e.Befunde.Where(b => b.Regel == "kapazitaet").ToList();
            Pruefe.Wahr(kap.Count > 0 && kap.All(b => b.Schwere == Schwere.Hinweis), Liste(e.Befunde));
            Pruefe.Wahr(kap.Any(b => b.Text.Contains("Pflicht Werkstatt: 5 von 16 passen")), Liste(kap));
            // Köln mit der Kapazität als Fehler: ohne Bauzonen passt nichts
            var koeln = Oeffnen("koeln");
            Pruefe.Wahr(!koeln.HatFehler, Liste(Fehler(koeln)));
            foreach (var z in koeln.Bauzonen.ToList()) koeln.FlaecheLoeschen(z);
            Pruefe.Wahr(Fehler(koeln).Count(b => b.Regel == "kapazitaet") == 6, Liste(Fehler(koeln))); // seit 16.11.2026 mit den zwei Startgebäuden
        }),

        ("Kapazität Köln: unter 150 ms (Release, Median), und die Kartenwerkstatt rechnet sie nur neu, wenn sich Gebäude, Wege oder Bauzonen ändern", () =>
        {
            var e = Oeffnen("koeln");
            static double Median(Action a)
            {
                for (var i = 0; i < 3; i++) a();
                var zeiten = new List<double>();
                for (var i = 0; i < 9; i++)
                {
                    var uhr = System.Diagnostics.Stopwatch.StartNew();
                    a();
                    zeiten.Add(uhr.Elapsed.TotalMilliseconds);
                }
                return zeiten.Order().ElementAt(4);
            }
            var kapazitaet = Median(() => Kapazitaetspruefung.Pruefen(e));
            var voll = Median(() => Kartenpruefung.Pruefen(e));
            Console.WriteLine($"          (Köln: Kapazität {kapazitaet:0.0} ms, volle Prüfung mit gespeicherter Kapazität {voll:0.0} ms)");
            Pruefe.Wahr(kapazitaet < 150, $"Kapazität {kapazitaet:0.0} ms");
            var vorher = e.Kapazitaetsrechnung();
            Pruefe.Wahr(ReferenceEquals(vorher, e.Kapazitaetsrechnung()), "gespeichert");
            e.MauerUmbenennen(e.Mauern[0], "Alte Mauer");
            Pruefe.Wahr(ReferenceEquals(vorher, e.Kapazitaetsrechnung()), "eine Mauer ändert sie nicht");
            e.Verschieben(e.Gebaeude[0].Kennung, e.Gebaeude[0].X + 1, e.Gebaeude[0].Z);
            Pruefe.Wahr(!ReferenceEquals(vorher, e.Kapazitaetsrechnung()), "ein Gebäude schon");
        }),

        ("Greybox: dieselben 7 Hinweise wie tools/karten/greybox.py (4 Wege über Umlandbetrieben, 3 Felsberge), dazu 2 Startwege des Umlands, keine Fehler", () =>
        {
            var e = Oeffnen("greybox");
            Pruefe.Gleich(0, Fehler(e).Count, Liste(Fehler(e)));
            var hinweise = e.Befunde.Where(b => b.Schwere == Schwere.Hinweis).ToList();
            Pruefe.Gleich(4, hinweise.Count(b => b.Regel == "weg-ueber-umland"), Liste(hinweise));
            Pruefe.Gleich(3, hinweise.Count(b => b.Regel == "ueberlappung"), Liste(hinweise));
            // Die Regel „startweg“ misst nur noch die Umlandbetriebe der KI-Häuser (16.11.2026; bis dahin auch die vier Start-Hofstellen, die im Band
            // lagen) – außerhalb ±10 % liegen zwei: Grube Eisenbart und Steinbruch Kieselbach
            Pruefe.Gleich("Grube_Eisenbart, Steinbruch_Kieselbach",
                string.Join(", ", hinweise.Where(b => b.Regel == "startweg").SelectMany(b => b.Gebaeude).Order(StringComparer.Ordinal)), Liste(hinweise));
            Pruefe.Gleich(9, hinweise.Count, Liste(hinweise));
        }),

        // ── Einwohnerplätze ──────────────────────────────────────────────────

        ("Prüfung: Eine Karte ohne Einwohnerplätze bekommt einen Hinweis, mit einem Platz nicht mehr (Köln hat 70 seit PR #213)", () =>
        {
            var e = Kartenentwurf.Neu("neu", "Neu", 300, 300, Katalog.Value);
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "einwohnerplaetze" && b.Schwere == Schwere.Hinweis), Liste(e.Befunde));
            Pruefe.Wahr(Oeffnen("koeln").Befunde.All(b => b.Regel != "einwohnerplaetze"), "Köln hat Einwohnerplätze");
            Pruefe.Wahr(Oeffnen("greybox").Befunde.All(b => b.Regel != "einwohnerplaetze"), "die Greybox hat 36");
            e.Setzen("einwohnerplatz_ring", 0, 0);
            Pruefe.Wahr(e.Befunde.All(b => b.Regel != "einwohnerplaetze"), Liste(e.Befunde));
        }),

        // ── Katalog gegen die Modelle ────────────────────────────────────────

        ("Katalog gegen die Modelle: 0 Fehler, 14 Hinweise – dieselben Bausteine wie tools/karten/katalog_pruefen.py; Kran, Rheinkran (Ponton) und Zoll ragen übers Wasser", () =>
        {
            var befunde = Katalogpruefung.Pruefen(Katalog.Value, Modellordner);
            Pruefe.Gleich(0, befunde.Count(b => b.Schwere == Schwere.Fehler), Liste(befunde));
            Pruefe.Gleich("hafenkran, rheinkran, zollstelle, grube, holzschlag, steinbruch, kraeutergarten, duftgarten, bauernhof, klosterhof, wachturm, weide, einwohner_wohnhaus, einwohner_ratsherrenhaus",
                string.Join(", ", befunde.Select(b => b.Gebaeude[0])));
            Pruefe.Wahr(befunde[3].Text.Contains("x -10,6 … 10,4, z -8,9 … 7,5 (21 × 16,4 m) – ragt bis 2,6 m über die Fläche (Umland: bekannt"), befunde[3].Text); // die Grube (seit dem Rheinkran an vierter Stelle)
            Pruefe.Wahr(befunde[^1].Text.Contains("ragt bis 0,5 m"), befunde[^1].Text);
        }),

        ("Katalog gegen die Modelle: fehlende Datei, fehlender Knoten und fehlende Variante sind Fehler", () =>
        {
            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Datenordner, "katalog.json")))!;
            var bausteine = json["bausteine"]!.AsArray();
            bausteine.Single(b => (string?)b!["kennung"] == "rathaus")!["modell"] = "gebaeude/gibt_es_nicht";
            bausteine.Single(b => (string?)b!["kennung"] == "weide")!["knoten"] = "Kuhstall";
            bausteine.Single(b => (string?)b!["kennung"] == "tanne")!["varianten"] = 19;
            var befunde = Katalogpruefung.Pruefen(Gebaeudekatalog.AusJson(json.ToJsonString()), Modellordner);
            var fehler = befunde.Where(b => b.Schwere == Schwere.Fehler).Select(b => b.Text).ToList();
            Pruefe.Wahr(fehler.Any(f => f.StartsWith("rathaus: Die Modelldatei gebaeude/gibt_es_nicht.glb fehlt")), string.Join("\n", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("„Kuhstall“ fehlt")), string.Join("\n", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("„Kiefer19“ fehlt")), string.Join("\n", fehler));
        }),

        ("Modelldatei: Anker wie die Stadtansicht (Halteplatz der Grube, Gatter der Weide) und Ausdehnung eines Knotens", () =>
        {
            var grube = Modelldatei.Lesen(Path.Combine(Modellordner, "umland", "grube.glb"));
            Pruefe.Wahr(grube.Anker("Anker_Karren") is { } k && k.Z > 5, $"Halteplatz vorn: {grube.Anker("Anker_Karren")}");
            Pruefe.Gleich(null, grube.Anker("GibtEsNicht"));
            var stellen = Modelldatei.Lesen(Path.Combine(Modellordner, "umland", "umland_stellen.glb"));
            Pruefe.Wahr(stellen.Anker("Anker_Tuer") is not null, "Gatter");
            Pruefe.Wahr(stellen.Ausdehnung("Weide") is var (x0, _, x1, _) && Math.Abs(x1 - x0 - 14.1) < 0.1, $"{stellen.Ausdehnung("Weide")}");
            Pruefe.Gleich(null, stellen.Ausdehnung("Kuhstall"));
        }),

        // ── Wege folgen dem Tor ──────────────────────────────────────────────

        ("Mauerecke ziehen: der Stützpunkt im Tor folgt dem Tor (gleicher Rückgängig-Schritt), die Straße kreuzt die Mauer weiter im Tor", () =>
        {
            var (e, mauer, weg) = MitTorweg();
            Pruefe.Wahr(!Torfehler(e), Liste(e.Befunde));
            var schritte = e.Geschichte.Schritte;
            e.MauerEckeVerschieben(mauer, 2, 72, 60);     // die Ecke Südost nach Osten: die Ostmauer wird schräg
            Pruefe.Gleich(schritte + 1, e.Geschichte.Schritte, "ein Schritt");
            Pruefe.Gleich(mauer.Tore[0].Punkt, weg.Punkte[1], "der Weg folgt dem Tor");
            Pruefe.Wahr(mauer.Tore[0].X > 60, $"das Tor ist gewandert: {mauer.Tore[0].Punkt}");
            Pruefe.Gleich(O(20, 10), weg.Punkte[0], "die übrigen Punkte bleiben");
            Pruefe.Gleich(O(110, 10), weg.Punkte[2]);
            Pruefe.Wahr(!Torfehler(e), Liste(e.Befunde));
            Pruefe.Wahr(e.Rueckgaengig(), "Rückgängig");
            Pruefe.Gleich(O(60, 10), weg.Punkte[1], "beides zurück");
            Pruefe.Gleich(O(60, 10), mauer.Tore[0].Punkt);
            Pruefe.Wahr(e.Wiederholen(), "Wiederholen");
            Pruefe.Gleich(mauer.Tore[0].Punkt, weg.Punkte[1]);
        }),

        ("Mauer verschieben, Tor verschieben, Ecke einfügen: der Weg im Tor folgt; ein Weg, der nur in der Nähe liegt, nicht", () =>
        {
            var (e, mauer, weg) = MitTorweg();
            var daneben = e.WegAnlegen(Wegklasse.Gasse, new[] { O(40, 30), O(55, 30) }, glatt: false, name: "Gasse daneben");
            e.MauerVerschieben(mauer, 5, -2);
            Pruefe.Gleich(O(65, 8), mauer.Tore[0].Punkt);
            Pruefe.Gleich(O(65, 8), weg.Punkte[1], "verschoben mit der Mauer");
            Pruefe.Gleich(O(55, 30), daneben.Punkte[1], "die Gasse bleibt");
            e.TorVerschieben(mauer, 0, 64, 20);
            Pruefe.Gleich(O(65, 20), mauer.Tore[0].Punkt, "auf der Mauerlinie");
            Pruefe.Gleich(O(65, 20), weg.Punkte[1], "folgt dem Tor");
            e.TorUmbenennen(mauer, 0, "Rheintor");
            Pruefe.Gleich(O(65, 20), weg.Punkte[1]);
            e.MauerEckeEinfuegen(mauer, 2, 80, 0);         // ein Knick nach außen zwischen Nordost und Südost
            Pruefe.Gleich(mauer.Tore[0].Punkt, weg.Punkte[1], "folgt beim Einfügen einer Ecke");
            Pruefe.Wahr(!Torfehler(e), Liste(e.Befunde));
            while (e.Rueckgaengig()) { }
            Pruefe.Gleich(0, e.Wege.Count + e.Mauern.Count, "alles zurück");
        }),

        // ── Kartenskript ─────────────────────────────────────────────────────

        ("Kartenskript Probestadt: ohne Fehler und Hinweise, Kapazität passt, schreibt und liest dieselbe Datei", () =>
        {
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            Pruefe.Gleich("", Liste(e.Befunde));
            Pruefe.Gleich(0, e.Gebaeude.Count(g => g.Art is Bausteinart.Werkstattplatz or Bausteinart.Wohnhausplatz)); // seit 16.11.2026 (vorher vier und sechs)
            Pruefe.Gleich(8, Startortlos.Haeuser(e.ZuStadtkarte(), Daten.Value)); // dafür Platz für acht Häuser in den Bauzonen im Stadtinneren
            Pruefe.Wahr(e.Gebaeude.Count(g => g.Art == Bausteinart.Einwohnerplatz) >= 10, "Einwohnerplätze");
            Pruefe.Gleich(2, e.Mauern[0].Tore.Count);
            Pruefe.Gleich(0, e.Start.Haeuser.Count); // keine festen Häuser mehr (17.11.2026): die zwei früheren als Namen der Stadt
            Pruefe.Gleich("Ahlbeck, Brendel", string.Join(", ", e.Start.Hausnamen));
            Pruefe.Wahr(e.Wege.Any(w => w.IstGlatt), "ein Weg als Kurve");
            Pruefe.Wahr(e.Naturflaechen.Count >= 1 && e.Bauzonen.Count >= 1, "Wald und Bauzonen");
            Pruefe.Gleich(0, Kapazitaetspruefung.Pruefen(e).Befunde.Count);
            var json = e.ZuJson();
            Pruefe.Gleich(json, Kartenentwurf.AusJson(json, Katalog.Value).ZuJson(), "geschrieben und wieder gelesen");
            var karte = Stadtkarte.AusJson(json, Katalog.Value);
            Pruefe.Gleich("probestadt", karte.Kennung);
            Pruefe.Wahr(json.Contains("\"vorlage\": ") && json.Contains("\"kamera\": {\"punkt\": [0, 0], \"zoom\": 110, \"drehung\": 0}") && json.Contains("\"fiktion\": ["), json[..300]);
            Pruefe.Wahr(json.IndexOf("\"vorlage\"", StringComparison.Ordinal) < json.IndexOf("\"einheit\"", StringComparison.Ordinal), "vorlage an ihrer Stelle");
            Pruefe.Wahr(json.IndexOf("\"start\"", StringComparison.Ordinal) < json.IndexOf("\"kamera\"", StringComparison.Ordinal)
                        && json.IndexOf("\"kamera\"", StringComparison.Ordinal) < json.IndexOf("\"fiktion\"", StringComparison.Ordinal), "kamera und fiktion hinten");
        }),

        ("Kartenskript Probestadt: eine Partie aus ihr läuft ein Spieljahr (1140 Ticks) ohne Fehler, mit sieben gewürfelten KI-Häusern (so viele passen hinein)", () =>
        {
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            var (sim, spieler) = e.Ausprobieren(Daten.Value, "Schmied");
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(8, w.Konten.Count); // seit 17.11.2026 würfelt auch die Probe ihre KI-Häuser
            Pruefe.Wahr(w.Betriebe.Any(b => b.Konto == spieler), "der Spieler hat seine Werkstatt");
            Pruefe.Wahr(w.Konten.All(k => w.Betriebe.Count(b => b.Konto == k && b.InDerStadt) == 1), "jedes Haus mit einer Werkstatt in der Stadt");
            Pruefe.Gleich(8, w.Betriebe.Where(b => b.InDerStadt).Select(b => b.Art).Distinct().Count(), "jeder Beruf einmal");
            var jahr = sim.Zeit.Jahr;
            for (var t = 0; t < Spielzeit.MinutenProTag; t++) sim.Tick();
            Pruefe.Gleich(jahr + 1, sim.Zeit.Jahr, "ein Jahr vergangen");
        }),

        ("Kartenskript: schreibt nicht über eine vorhandene Datei (außer mit --ueberschreiben), mit Fehlern gar nicht", () =>
        {
            var ordner = Path.Combine(Path.GetTempPath(), "reichsgilde-kartenbau-" + Guid.NewGuid().ToString("N"));
            try
            {
                var e = Probestadtvorlage.Entwurf(Katalog.Value, Daten.Value);
                var (karte, vorschau) = (Path.Combine(ordner, "karten", "probestadt.json"), Path.Combine(ordner, "vorschau"));
                var aus = new StringWriter();
                Pruefe.Gleich(0, Befehle.Schreiben(e, karte, vorschau, false, aus), aus.ToString());
                Pruefe.Gleich(e.ZuJson(), File.ReadAllText(karte));
                Pruefe.Wahr(File.Exists(Path.Combine(vorschau, "probestadt.svg")) && File.Exists(Path.Combine(vorschau, "probestadt-belegung.svg")), "Vorschaubilder");
                File.WriteAllText(karte, "verfeinert in der Kartenwerkstatt");
                Pruefe.Gleich(2, Befehle.Schreiben(e, karte, vorschau, false, aus));
                Pruefe.Gleich("verfeinert in der Kartenwerkstatt", File.ReadAllText(karte), "nicht überschrieben");
                Pruefe.Wahr(aus.ToString().Contains("--ueberschreiben"), aus.ToString());
                Pruefe.Gleich(0, Befehle.Schreiben(e, karte, vorschau, true, aus));
                Pruefe.Gleich(e.ZuJson(), File.ReadAllText(karte), "mit --ueberschreiben ersetzt");
                // mit einem Fehler: nichts
                var kaputt = Probestadtvorlage.Entwurf(Katalog.Value, Daten.Value);
                kaputt.Setzen("rathaus", 0, -19);  // genau auf das Rathaus
                var andere = Path.Combine(ordner, "karten", "kaputt.json");
                Pruefe.Gleich(1, Befehle.Schreiben(kaputt, andere, vorschau, true, aus));
                Pruefe.Wahr(!File.Exists(andere), "nichts geschrieben");
            }
            finally
            {
                if (Directory.Exists(ordner)) Directory.Delete(ordner, recursive: true);
            }
        }),

        ("Kartenskript: Stadtskripte finden (Kennung oder Klassenname), Hilfen Häuserreihe und Ringgasse", () =>
        {
            Pruefe.Wahr(Stadtskript.Finde("probestadt") is Probestadt && Stadtskript.Finde("Probestadt") is Probestadt, "gefunden");
            Pruefe.Gleich(null, Stadtskript.Finde("atlantis"));
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            var ring = e.Wege.Single(w => w.Name == "Ringstraße");
            Pruefe.Gleich("(-57 | -42), (57 | -42), (57 | 42), (-57 | 42), (-57 | -42)", string.Join(", ", ring.Punkte), "13 m innerhalb der Mauer");
            // Die Einwohnerplätze der Nordreihe (zwischen Ringstraße und Mauer): Vorderseite nach Süden zur Ringstraße, der Eingang 0,5 m neben dem
            // Straßenrand, in gleichen Abständen (bis 16.11.2026 prüfte das die Reihe der Wohnhausplätze)
            var nord = e.Gebaeude.Where(g => g.Katalog == "einwohnerplatz_ring" && g.Z < 0).OrderBy(g => g.X).ToList();
            Pruefe.Wahr(nord.Count >= 5, $"{nord.Count} Einwohnerplätze in der Nordreihe");
            Pruefe.Wahr(nord.All(g => g.Drehung == 0 && Math.Abs(g.Eingang!.Value.Z - (-42 - ring.BreiteWirksam / 2 - 0.5)) < 1e-9), string.Join(", ", nord.Select(g => $"{g.Punkt} {g.Drehung} {g.Eingang}")));
            var schritte = nord.Zip(nord.Skip(1), (a, b) => b.X - a.X).ToList();
            Pruefe.Wahr(schritte.All(x => Math.Abs(x - schritte[0]) < 1e-9 && x > 0), "gleiche Abstände: " + string.Join(", ", schritte));
        }),

        ("Vorschau: Mauer aus dem Mauerplan (Türme, Torburgen), Wege als Kurve, Startbelegung farbig mit Legende; Belegung mit Kapazität bzw. Probeplan", () =>
        {
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            var svg = Vorschau.Zeichnen(e);
            var plan = e.Mauern[0].Plan;
            Pruefe.Gleich(plan.Ecktuerme.Count, CountOf(svg, "r=\"3.2\""), "Ecktürme");
            Pruefe.Gleich(plan.Tuerme.Count, CountOf(svg, "r=\"2.4\""), "Mauertürme");
            Pruefe.Wahr(svg.Contains(">Westtor</text>") && svg.Contains(">Osttor</text>"), "Tore");
            var waldweg = e.Wege.Single(w => w.Name == "Waldweg");
            Pruefe.Wahr(waldweg.Verlauf.Count > waldweg.Punkte.Count * 5, "die Kurve ist abgetastet");
            Pruefe.Wahr(svg.Contains(">Spieler</text>") && svg.Contains("fill=\"#336bb8\"") && !svg.Contains(">Haus Ahlbeck</text>"), "Legende und Spielerfarbe, keine festen Häuser");
            // mit Häusern zur Probe (wie bis zum 17.11.2026 in der Karte) stehen sie in der Legende
            Pruefe.Wahr(Vorschau.Zeichnen(Probestadtvorlage.Entwurf(Katalog.Value, Daten.Value)).Contains(">Haus Ahlbeck</text>"), "Legende mit Probehäusern");
            Pruefe.Wahr(!svg.Contains("opacity=\"0.75\""), "ohne Belegung");
            Pruefe.Wahr(Vorschau.Zeichnen(e, belegung: true).Contains("fill=\"#8C5A2B\" opacity=\"0.75\""), "Kapazität: Werkstätten");
            Pruefe.Wahr(Vorschau.Zeichnen(Oeffnen("greybox"), belegung: true).Contains("stroke=\"#1F2326\" stroke-width=\"0.3\"/>\n<circle"), "Greybox: Probeplan");
        }),
    };

    static int CountOf(string text, string teil)
    {
        var n = 0;
        for (var i = text.IndexOf(teil, StringComparison.Ordinal); i >= 0; i = text.IndexOf(teil, i + 1, StringComparison.Ordinal)) n++;
        return n;
    }
}
