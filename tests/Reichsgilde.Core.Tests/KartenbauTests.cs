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

        ("Kapazität Köln (Stadtskript, PR #213; Märkte an den Enden der Mauer und in Deutz seit 31.10.2026): 7.746 Felder, Pflicht und Reserve passen – 80 % frei nach der Pflicht", () =>
        {
            var k = Kapazitaetspruefung.Pruefen(Oeffnen("koeln"));
            Pruefe.Gleich(string.Join("\n", new[]
            {
                "Stadt-Bauzonen: 7746 Felder, davon fest belegt 0",
                "Pflicht Werkstatt: 16 von 16 passen", "Pflicht Ratsherrenhaus: 13 von 13 passen", "Pflicht Einwohner-Bürgerhaus: 3 von 3 passen",
                "Pflicht Einwohner-Wohnhaus: 40 von 40 passen", "Reserve Stadthof: 4 von 4 passen", "Reserve Stadtpalais: 8 von 8 passen",
                "Frei nach der Pflicht: 6234 Felder (80%), nach der Reserve: 5770 Felder",
            }), string.Join("\n", k.Bericht));
            Pruefe.Gleich(0, k.Befunde.Count);
            // Die gesetzten Gebäude: alle 84 aus Pflicht und Reserve; feste keine (die Plätze der Karte liegen neben den Zonen; bis 31.10.2026 4 Felder)
            Pruefe.Gleich(84, k.Plaetze.Count(p => p.Art != "fest"));
            Pruefe.Gleich(16 * 56, k.Plaetze.Where(p => p.Art == "Werkstatt").Sum(p => p.Felder.Count));
            Pruefe.Gleich(0, k.Plaetze.Where(p => p.Art == "fest").Sum(p => p.Felder.Count));
        }),

        ("Kapazität aller Karten: dieselben Plätze wie vor der Beschleunigung (27.10.2026) – Anzahl, Felder und eine Prüfsumme ihrer Lage", () =>
        {
            foreach (var (karte, befunde, plaetze, felder, summe) in new[]
            {
                // Köln seit den drei Marktplätzen (31.10.2026) neu, vorher 86 / 1.980 / −1.223.276; seit der gemeinsamen Strecke (Startgrundstücke 36 m vom Platz) die Prüfsumme neu, vorher −11.184.073;
                // seit den Märkten an den Enden der Mauer und in Deutz die Prüfsumme neu, vorher −12.931.232; Greybox mit dem Weinmarktgässchen ein Platz mehr, vorher 22 / 698 / −2.115.343
                ("koeln", 0, 84, 1976, -4666812L), ("greybox", 4, 23, 714, -2170301L), ("probestadt", 0, 80, 1752, 7598432L), ("messstadt", 0, 80, 1752, 6597050L),
            })
            {
                var k = Kapazitaetspruefung.Pruefen(Oeffnen(karte));
                Pruefe.Gleich(befunde, k.Befunde.Count, karte);
                Pruefe.Gleich(plaetze, k.Plaetze.Count, karte);
                Pruefe.Gleich(felder, k.Plaetze.Sum(p => p.Felder.Count), karte);
                // Reihenfolge und Lage: jeder Platz gewichtet mit seiner Stelle
                Pruefe.Gleich(summe, k.Plaetze.Select((p, n) => (long)(n + 1) * p.Felder.Sum(f => f.I * 7 + f.J * 13)).Sum(), karte);
            }
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
            Pruefe.Wahr(Fehler(koeln).Count(b => b.Regel == "kapazitaet") == 4, Liste(Fehler(koeln)));
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

        ("Greybox: dieselben 7 Hinweise wie tools/karten/greybox.py (4 Wege über Umlandbetrieben, 3 Felsberge), keine Fehler", () =>
        {
            var e = Oeffnen("greybox");
            Pruefe.Gleich(0, Fehler(e).Count, Liste(Fehler(e)));
            var hinweise = e.Befunde.Where(b => b.Schwere == Schwere.Hinweis).ToList();
            Pruefe.Gleich(4, hinweise.Count(b => b.Regel == "weg-ueber-umland"), Liste(hinweise));
            Pruefe.Gleich(3, hinweise.Count(b => b.Regel == "ueberlappung"), Liste(hinweise));
            Pruefe.Gleich(7, hinweise.Count, Liste(hinweise));
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

        ("Katalog gegen die Modelle: 0 Fehler, 13 Hinweise – dieselben Bausteine wie tools/karten/katalog_pruefen.py; Kran und Zoll ragen übers Wasser", () =>
        {
            var befunde = Katalogpruefung.Pruefen(Katalog.Value, Modellordner);
            Pruefe.Gleich(0, befunde.Count(b => b.Schwere == Schwere.Fehler), Liste(befunde));
            Pruefe.Gleich("hafenkran, zollstelle, grube, holzschlag, steinbruch, kraeutergarten, duftgarten, bauernhof, klosterhof, wachturm, weide, einwohner_wohnhaus, einwohner_ratsherrenhaus",
                string.Join(", ", befunde.Select(b => b.Gebaeude[0])));
            Pruefe.Wahr(befunde[2].Text.Contains("x -11 … 11, z -8,9 … 9 (22 × 17,9 m) – ragt bis 3 m über die Fläche (Umland: bekannt"), befunde[2].Text);
            Pruefe.Wahr(befunde[^1].Text.Contains("ragt bis 0,5 m"), befunde[^1].Text);
        }),

        ("Katalog gegen die Modelle: fehlende Datei, fehlender Knoten und fehlende Variante sind Fehler", () =>
        {
            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(Datenordner, "katalog.json")))!;
            var bausteine = json["bausteine"]!.AsArray();
            bausteine.Single(b => (string?)b!["kennung"] == "rathaus")!["modell"] = "gebaeude/gibt_es_nicht";
            bausteine.Single(b => (string?)b!["kennung"] == "weide")!["knoten"] = "Kuhstall";
            bausteine.Single(b => (string?)b!["kennung"] == "tanne")!["varianten"] = 9;
            var befunde = Katalogpruefung.Pruefen(Gebaeudekatalog.AusJson(json.ToJsonString()), Modellordner);
            var fehler = befunde.Where(b => b.Schwere == Schwere.Fehler).Select(b => b.Text).ToList();
            Pruefe.Wahr(fehler.Any(f => f.StartsWith("rathaus: Die Modelldatei gebaeude/gibt_es_nicht.glb fehlt")), string.Join("\n", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("„Kuhstall“ fehlt")), string.Join("\n", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("„Tanne9“ fehlt")), string.Join("\n", fehler));
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
            Pruefe.Gleich(4, e.Gebaeude.Count(g => g.Art == Bausteinart.Werkstattplatz));
            Pruefe.Gleich(6, e.Gebaeude.Count(g => g.Art == Bausteinart.Wohnhausplatz));
            Pruefe.Wahr(e.Gebaeude.Count(g => g.Art == Bausteinart.Einwohnerplatz) >= 10, "Einwohnerplätze");
            Pruefe.Gleich(2, e.Mauern[0].Tore.Count);
            Pruefe.Gleich(2, e.Start.Haeuser.Count);
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

        ("Kartenskript Probestadt: eine Partie aus ihr läuft ein Spieljahr (1140 Ticks) ohne Fehler, mit beiden KI-Häusern", () =>
        {
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            var (sim, spieler) = e.Ausprobieren(Daten.Value, "Schmied");
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(3, w.Konten.Count);
            Pruefe.Wahr(w.Betriebe.Any(b => b.Konto == spieler), "der Spieler hat seine Werkstatt");
            Pruefe.Wahr(w.Betriebe.Any(b => b.Art == Betriebsart.Tischlerei) && w.Betriebe.Any(b => b.Art == Betriebsart.Giesserei), "Tischlerei und Gießerei");
            var jahr = sim.Zeit.Jahr;
            for (var t = 0; t < Spielzeit.MinutenProTag; t++) sim.Tick();
            Pruefe.Gleich(jahr + 1, sim.Zeit.Jahr, "ein Jahr vergangen");
        }),

        ("Kartenskript: schreibt nicht über eine vorhandene Datei (außer mit --ueberschreiben), mit Fehlern gar nicht", () =>
        {
            var ordner = Path.Combine(Path.GetTempPath(), "reichsgilde-kartenbau-" + Guid.NewGuid().ToString("N"));
            try
            {
                var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
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
                var kaputt = new Probestadt().Bauen(Katalog.Value, Daten.Value);
                kaputt.Setzen("werkstattplatz", -30, -10);  // genau auf einen anderen Werkstattplatz
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
            // Die Wohnhäuser der Nordreihe: Vorderseite nach Süden zur Ringstraße, der Eingang 0,5 m neben dem Straßenrand
            var nord = e.Gebaeude.Where(g => g.Name.StartsWith("Wohnhaus Nord")).ToList();
            Pruefe.Gleich(3, nord.Count);
            Pruefe.Wahr(nord.All(g => g.Drehung == 180 && g.Eingang!.Value.Z == -40), string.Join(", ", nord.Select(g => $"{g.Punkt} {g.Drehung}")));
            Pruefe.Gleich("-10, 0, 10", string.Join(", ", nord.Select(g => g.X)));
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
            Pruefe.Wahr(svg.Contains(">Haus Ahlbeck</text>") && svg.Contains("fill=\"#336bb8\""), "Legende und Spielerfarbe");
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
