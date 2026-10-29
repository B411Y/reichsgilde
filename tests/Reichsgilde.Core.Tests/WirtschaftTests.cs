using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests für Markt, Betriebe und Fronstatut.
/// Die Sollwerte stammen aus dem freigegebenen Wirtschaftsmodell (Dokument 03, Abschnitt 7).
/// </summary>
static class Datenpfad
{
    /// <summary>Sucht game/daten/wirtschaft.json, egal von wo aus die Tests gestartet werden.</summary>
    public static string Wirtschaft()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null)
        {
            var pfad = Path.Combine(ordner.FullName, "game", "daten", "wirtschaft.json");
            if (File.Exists(pfad)) return pfad;
            ordner = ordner.Parent;
        }
        throw new FileNotFoundException("game/daten/wirtschaft.json wurde nicht gefunden.");
    }

    /// <summary>
    /// Die Daten mit einem Versuch je Nacht um 20 Uhr und voller Chance (wie bis Datenversion 56) – für Tests einer einzelnen Tat.
    /// Die Gaunerei je Nachtstunde (gauner.nachtVersuche) prüft GaunerNachtTests.
    /// </summary>
    public static string EinVersuchJeNacht(string json) => json.Replace("\"nachtVersuche\": 4", "\"nachtVersuche\": 1").Replace("\"versuchFaktor\": 0.3333", "\"versuchFaktor\": 1");

    /// <summary>Die Karte game/karten/&lt;kennung&gt;.json (neben game/daten/).</summary>
    public static string Karte(string kennung) =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Wirtschaft()))!, "karten", kennung + ".json");

    /// <summary>
    /// Die echten Daten ohne Aktionspunkte – für Tests, die eine Mechanik (Rat, Gericht …) mit vielen Zügen
    /// im selben Jahr prüfen. Die AP-Regeln selbst prüfen die AktionspunkteTests.
    /// </summary>
    public static string OhneAktionspunkte(string? json = null)
    {
        var knoten = System.Text.Json.Nodes.JsonNode.Parse(json ?? File.ReadAllText(Wirtschaft()),
            documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true })!;
        knoten.AsObject().Remove("aktionspunkte");
        return knoten.ToJsonString();
    }

    /// <summary>
    /// Die echten Daten ohne Abschnitt „stadt“: Die Nachfrage ist fest wie in „waren“ (Einwohnerfaktor 1, die Bezugsstadt mit vier Häusern) –
    /// für Proben mit nur einem oder zwei Häusern, die die Referenzwerte des Wirtschaftsmodells nachrechnen.
    /// </summary>
    public static string OhneStadt()
    {
        var knoten = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Wirtschaft()),
            documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true })!;
        knoten.AsObject().Remove("stadt");
        return knoten.ToJsonString();
    }
}

static class WirtschaftTests
{
    // Die Referenzwerte (Abschnitt 7 und 9 des Wirtschaftsmodells) gelten in der Bezugsstadt: Nachfrage wie in den Daten.
    // Die Proben hier haben nur ein oder zwei Häuser – mit dem Abschnitt „stadt“ wäre ihre Stadt kleiner (PR #177).
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneStadt()));

    static Simulation NeueSimulation(Fronstatut statut = Fronstatut.Bestienfron, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { StartStatut = statut, Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            wirtschaftsdaten: Daten.Value);

    static void JahreSimulieren(Simulation sim, int jahre)
    {
        for (var i = 0; i < jahre * Spielzeit.MinutenProTag; i++)
            sim.Tick();
    }

    /// <summary>
    /// Durchschnittlicher Jahresgewinn der Jahre 2 bis 21.
    /// • Das erste Jahr wird übersprungen, weil der Markt erst „einschwingt" (das Verkaufsfenster ist leer).
    /// • Über 20 Jahre wird gemittelt, weil Waren in Fuhren verkauft werden und Fuhren über den
    ///   Jahreswechsel laufen. Einzelne Jahre schwanken deshalb stark, der Durchschnitt nicht.
    /// </summary>
    static (double Gewinn, double Schaden) MittlererGewinn(Simulation sim, Konto konto)
    {
        const int jahre = 20;
        JahreSimulieren(sim, 1);
        double gewinn = 0, schaden = 0;
        for (var j = 0; j < jahre; j++)
        {
            JahreSimulieren(sim, 1);
            gewinn += konto.Vorjahr!.Gewinn;
            schaden += konto.Vorjahr!.Schaden;
        }
        return (gewinn / jahre, schaden / jahre);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        // ── Daten ───────────────────────────────────────────────────────────
        ("Wirtschaftsdaten: game/daten/wirtschaft.json wird geladen und ist gültig", () =>
        {
            var d = Daten.Value;
            Pruefe.Gleich(63, d.Waren.Count); // 9 Schmied, 8 Tischler, 9 Steinmetz, 9 Alchimist (mit Kräuterschnaps), 9 Wirt, 9 Prediger, 9 Parfumeur, Gold (nur Handel)
            Pruefe.Gleich(14, d.Arbeitskraefte.Count); // ab PR #224 drei Stufen je Laufbahn (Werkstatt, Umland, Diebe, Wache), dazu Ork und Leibeigener
            Pruefe.Gleich(112.0, d.Waren["Silberring"].Grundpreis); // 90 × 1,28 seit PR #177, −2,5 % seit Balancing 3
            Pruefe.Gleich(42.0, d.ZutatenZumGrundpreis("Silberring"));
            Pruefe.Gleich(true, d.Arbeitskraefte["Ork"].IstErlaubt(Fronstatut.Bestienfron, Arbeitsort.Umland));
            Pruefe.Gleich(false, d.Arbeitskraefte["Ork"].IstErlaubt(Fronstatut.Bestienfron, Arbeitsort.Werkstatt));
        }),

        ("Wirtschaftsdaten: Fehler werden gesammelt und verständlich gemeldet", () =>
        {
            const string json = """
                { "waren": { "Ring": { "grundpreis": 10, "zeit": 5, "nachfrage": 10, "npcAnteil": 1.2, "zutaten": { "Gold": 1 } } } }
                """;
            try
            {
                Wirtschaftsdaten.AusJson(json);
                throw new Exception("Es wurde ein Fehler erwartet.");
            }
            catch (WirtschaftsdatenFehler f)
            {
                Pruefe.Gleich(true, f.Fehler.Count >= 2, $"{f.Fehler.Count} Fehler"); // dazu fehlt der Abschnitt "logistik"
                Pruefe.Gleich(true, f.Message.Contains("Gold"));
                Pruefe.Gleich(true, f.Message.Contains("npcAnteil"));
            }
        }),

        ("Wirtschaftsdaten: kaputtes JSON nennt die Zeile", () =>
        {
            try
            {
                Wirtschaftsdaten.AusJson("{\n  \"waren\": {\n    \"Eisen\": { \"grundpreis\": 12,, }\n  }\n}");
                throw new Exception("Es wurde ein Fehler erwartet.");
            }
            catch (WirtschaftsdatenFehler f)
            {
                Pruefe.Gleich(true, f.Message.Contains("Zeile 3"), "Meldung: " + f.Message);
            }
        }),

        // ── Markt ───────────────────────────────────────────────────────────
        ("Markt: ausgeglichenes Angebot ergibt den Grundpreis, doppeltes etwa 71 %", () =>
        {
            var markt = new Markt(Daten.Value);
            var ring = markt["Silberring"];
            var gp = ring.Grundpreis;
            Pruefe.Ungefaehr(gp * Math.Sqrt(150.0 / 105), ring.Preis, 0.01); // nur NPC-Angebot: knapp
            ring.Verkaufen(45);                                               // 105 + 45 = 150 = Nachfrage
            Pruefe.Ungefaehr(gp, ring.Preis, 0.01);
            ring.Verkaufen(150);                                              // Angebot 300 = doppelt
            Pruefe.Ungefaehr(gp * Math.Sqrt(0.5), ring.Preis, 0.01);
        }),

        ("Markt: Preis bleibt in den Grenzen (50 % bis 200 %)", () =>
        {
            var markt = new Markt(Daten.Value);
            markt["Dolch"].Verkaufen(5000);
            Pruefe.Ungefaehr(markt["Dolch"].Grundpreis * 0.5, markt["Dolch"].Preis, 0.01);
        }),

        ("Markt: Verkäufe zählen genau 19 Spielstunden lang (04–23 Uhr = ein Jahr)", () =>
        {
            var markt = new Markt(Daten.Value);
            markt["Eisen"].Verkaufen(60);
            for (var h = 0; h < Spielzeit.StundenProTag; h++) markt.NeueStunde(); // genau 19 Stunden später
            Pruefe.Gleich(60.0, markt["Eisen"].SpielerAngebotImJahr);
            for (var m = 0; m < 30; m++) markt.NeueMinute();        // eine halbe Stunde darüber hinaus
            Pruefe.Ungefaehr(30.0, markt["Eisen"].SpielerAngebotImJahr, 0.001);
            markt.NeueStunde();                                     // 20 Stunden später
            Pruefe.Gleich(0.0, markt["Eisen"].SpielerAngebotImJahr);
        }),

        ("Arbeitszeit: 08:00 bis 20:00 Uhr = 720 Minuten pro Spieljahr", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var minuten = 0;
            for (var m = 0; m < Spielzeit.MinutenProTag; m++)
                if (w.IstArbeitszeit(new Spielzeit(m))) minuten++;
            Pruefe.Gleich(720, minuten);
            static Spielzeit Um(int h, int m = 0) => new(h * 60 + m - Spielzeit.TagesbeginnMinute);
            Pruefe.Gleich(false, w.IstArbeitszeit(Um(7, 59)));
            Pruefe.Gleich(true, w.IstArbeitszeit(Um(8)));
            Pruefe.Gleich(true, w.IstArbeitszeit(Um(19, 59)));
            Pruefe.Gleich(false, w.IstArbeitszeit(Um(20)));
        }),

        ("Markt: Ohne Spieler-Mine ist Eisen knapp (NPC deckt 90 %), eine Mine senkt den Preis", () =>
        {
            var markt = new Markt(Daten.Value);
            Pruefe.Ungefaehr(12 * Math.Sqrt(600.0 / 540), markt["Eisen"].Preis, 0.001); // ≈ 12,65
            markt["Eisen"].Verkaufen(60);                                               // eine Jahresförderung
            Pruefe.Ungefaehr(12, markt["Eisen"].Preis, 0.001);
        }),

        // ── Betriebe: Referenzwerte ─────────────────────────────────────────
        // Herkunft der Zahlen (siehe Dokument „Wirtschaftsmodell"):
        //   • Rechner (Grundpreis-Zutaten, stückweiser Verkauf):    Gießerei 878, Rivale 627, Mine A 1.139
        //   • Simulation Phase 2a (Marktpreis-Zutaten, Sofortverkauf): 812 / 570 / 1.139
        //   • Simulation mit Lager & Karren (diese Tests):           848 / 611 / 947 (inkl. Banditen)
        // Fuhrenverkauf erzielt etwas bessere Preise (das 24-h-Fenster zählt vor einer Fuhre weniger
        // Angebot); die Mine verliert Ware an Banditen. Die Tests sichern diese Werte gegen ungewollte
        // Änderungen ab (±5 %).
        //   • Balancing 1 (PR #177): Fertigwaren des Schmieds × 1,28 (Silberring 90 → 115), damit ein Betrieb in etwa
        //     10 Jahren Bürger wird (Z2) – Gießerei 848 → 1.551, Lehrling 289 → 543, Konkurrenz 649 → 1.317; Z1 ist dafür
        //     auf 1.300–1.700 angehoben (Entscheidung des Nutzers vom 22.10.2026). Die Grube bleibt bei 947 / 1.365.
        //   • Balancing 3: Schmied-Waren −2,5 % (Silberring 115 → 112), der Schmied lag 16 % über dem Mittel – Gießerei 1.551 → 1.455,
        //     mit zwei Lehrlingen 1.094 → 1.029, Konkurrenz 1.317 → 1.207 (alle Ziele Z1 weiter erfüllt).
        ("Gießerei allein, 2 Gesellen, Silberring: Gewinn ≈ 1.455 Gulden/Jahr", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var konto = w.KontoEroeffnen("Spieler");
            var g = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, konto);
            w.Einstellen(g, "Geselle", 2);
            Pruefe.Ungefaehr(1455, MittlererGewinn(sim, konto).Gewinn, 0.05);
        }),

        // Personal wie in Gilde 1 (PR #224): Die Partie beginnt mit zwei Lehrlingen – das ist das neue Z1 (Entscheidung des Nutzers vom
        // 28.10.2026: das Ziel fürs erste Jahr senken statt mehr Startgeld).
        ("Z1 (PR #224): Gießerei allein, 2 Lehrlinge wie zum Partiestart, Silberring: Gewinn ≈ 1.029 Gulden/Jahr (Ziel 900–1.300)", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var konto = w.KontoEroeffnen("Spieler");
            w.Einstellen(w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, konto), "Lehrling", 2);
            Pruefe.Ungefaehr(1029, MittlererGewinn(sim, konto).Gewinn, 0.05);
        }),

        // Seit PR #232 kauft der Karren Zutaten nur für wenige Arbeitsstunden (vorher bis zur Lagergrenze von 40). Der Gewinn zählt Zutaten
        // beim Kauf: Ein Lehrling braucht nur ≈ 10 Stück Eisen und Silber im Jahr, bisher füllte er in den ersten Jahren 40 + 40 auf Vorrat
        // (≈ 1.450 Gulden, die im Lager lagen) – 543 → 584. Mit zwei Gesellen bleibt es bei ≈ 1.551 (Vorrat verbraucht sich schnell).
        ("Gießerei, 1 Lehrling, Silberring: Gewinn ≈ 584 Gulden/Jahr", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var konto = w.KontoEroeffnen("Spieler");
            w.Einstellen(w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, konto), "Lehrling", 1);
            Pruefe.Ungefaehr(584, MittlererGewinn(sim, konto).Gewinn, 0.05);
        }),

        // Seit dem Warenkorb der Einwohner (PR #91) fragen sie günstige Schmiedewaren wie den Silberring rund 7 % mehr nach: 611 → 649.
        // Seit Balancing 1 (PR #177, Silberring 90 → 115): 649 → 1.317; seit Balancing 3 (Silberring 112): 1.207.
        ("Konkurrenz: Zwei Gießereien teilen sich den Silberring-Markt, je ≈ 1.207 Gulden", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var spieler = w.KontoEroeffnen("Spieler");
            var rivale = w.KontoEroeffnen("Rivale");
            w.Einstellen(w.BetriebEroeffnen("Gießerei Spieler", Betriebsart.Giesserei, spieler), "Geselle", 2);
            w.Einstellen(w.BetriebEroeffnen("Gießerei Rivale", Betriebsart.Giesserei, rivale), "Geselle", 2);
            Pruefe.Ungefaehr(1207, MittlererGewinn(sim, spieler).Gewinn, 0.05);
            Pruefe.Ungefaehr(spieler.Vermoegen, rivale.Vermoegen, 0.08); // ±8 %: Seit PR #152 trifft ein Unfall die beiden zufällig verschieden oft
        }),

        ("Mine, 3 Bergleute (Statut A), Ochsenkarren ohne Eskorte: Gewinn ≈ 947 Gulden/Jahr", () =>
        {
            var sim = NeueSimulation(Fronstatut.FreieStadt);
            var w = sim.Wirtschaft!;
            var konto = w.KontoEroeffnen("Spieler");
            w.Einstellen(w.BetriebEroeffnen("Mine", Betriebsart.Mine, konto), "Altknecht", 3);
            Pruefe.Ungefaehr(947, MittlererGewinn(sim, konto).Gewinn, 0.05);
        }),

        ("Mine, 3 Orks (Statut B), Ochsenkarren: Gewinn ≈ 1.365 Gulden/Jahr – deutlich mehr als mit Bergleuten", () =>
        {
            var sim = NeueSimulation(Fronstatut.Bestienfron);
            var w = sim.Wirtschaft!;
            var konto = w.KontoEroeffnen("Spieler");
            w.Einstellen(w.BetriebEroeffnen("Mine", Betriebsart.Mine, konto), "Ork", 3);
            Pruefe.Ungefaehr(1365, MittlererGewinn(sim, konto).Gewinn, 0.05);
        }),

        // ── Fronstatut ──────────────────────────────────────────────────────
        ("Statut B: Orks nur im Umland, Leibeigene gar nicht", () =>
        {
            var w = NeueSimulation(Fronstatut.Bestienfron).Wirtschaft!;
            var konto = w.KontoEroeffnen("Spieler");
            var giesserei = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, konto);
            var mine = w.BetriebEroeffnen("Mine", Betriebsart.Mine, konto);
            Pruefe.WirftMitMeldung(() => w.Einstellen(giesserei, "Ork", 1), "Bestienfron");
            Pruefe.WirftMitMeldung(() => w.Einstellen(mine, "Leibeigener", 1), "Bestienfron");
            w.Einstellen(mine, "Ork", 2); // erlaubt
            Pruefe.Gleich(2, mine.Belegt);
        }),

        ("Arbeitsplätze: Mehr Arbeiter als Plätze werden abgelehnt", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var g = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, w.KontoEroeffnen("Spieler"));
            w.Einstellen(g, "Geselle", 1);
            w.Einstellen(g, "Lehrling", 1);
            Pruefe.WirftMitMeldung(() => w.Einstellen(g, "Geselle", 2), "nur 2 Arbeitsplätze");
        }),

        ("Statutwechsel zu A: Fron-Orks werden entlassen und gemeldet", () =>
        {
            var w = NeueSimulation(Fronstatut.Bestienfron).Wirtschaft!;
            var meldungen = new List<string>();
            w.Meldung += meldungen.Add;
            var mine = w.BetriebEroeffnen("Mine", Betriebsart.Mine, w.KontoEroeffnen("Spieler"));
            w.Einstellen(mine, "Ork", 2);
            w.Einstellen(mine, "Altknecht", 1);
            w.StatutAendern(Fronstatut.FreieStadt);
            Pruefe.Gleich(1, mine.Belegt);
            Pruefe.Gleich(true, meldungen.Any(m => m.Contains("2× Ork")), string.Join(" | ", meldungen));
        }),

        // ── Zufall und Determinismus ────────────────────────────────────────
        ("Ausbrüche treten etwa so oft auf wie geplant (3 Orks, 4 %, 100 Jahre)", () =>
        {
            var sim = NeueSimulation(Fronstatut.Bestienfron);
            var w = sim.Wirtschaft!;
            var ausbrueche = 0;
            w.Meldung += m => { if (m.StartsWith("Ausbruch")) ausbrueche++; };
            w.Einstellen(w.BetriebEroeffnen("Mine", Betriebsart.Mine, w.KontoEroeffnen("Spieler")), "Ork", 3);
            JahreSimulieren(sim, 100);
            // Erwartet: 3 × 0,04 × 100 = 12
            Pruefe.Gleich(true, ausbrueche is >= 4 and <= 22, $"{ausbrueche} Ausbrüche");
        }),

        ("Determinismus: gleicher Startwert = exakt gleicher Verlauf", () =>
        {
            double Lauf(int startwert)
            {
                var sim = NeueSimulation(Fronstatut.Bestienfron, startwert);
                var w = sim.Wirtschaft!;
                var konto = w.KontoEroeffnen("Spieler");
                w.Einstellen(w.BetriebEroeffnen("Mine", Betriebsart.Mine, konto), "Ork", 3);
                JahreSimulieren(sim, 30);
                return konto.Vermoegen;
            }
            Pruefe.Gleich(Lauf(7), Lauf(7));
            Pruefe.Gleich(true, Lauf(7) != Lauf(8), "verschiedene Startwerte sollten sich unterscheiden");
        }),

        // ── Zusammenspiel mit der Partie ────────────────────────────────────
        ("Siegbedingung Zielvermögen beendet die Partie, sobald das Vermögen erreicht ist", () =>
        {
            var sim = new Simulation(
                Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Zielvermoegen, Zielvermoegen = 5_000 },
                wirtschaftsdaten: Daten.Value);
            var w = sim.Wirtschaft!;
            var konto = w.KontoEroeffnen("Spieler");
            w.Einstellen(w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, konto), "Geselle", 2);
            JahreSimulieren(sim, 5);
            Pruefe.Gleich(true, sim.IstBeendet);
            Pruefe.Gleich(true, konto.Vermoegen >= 5_000, $"Vermögen {konto.Vermoegen:N0}");
            Pruefe.Gleich(true, sim.Zeit.Jahr <= 1403, $"beendet im Jahr {sim.Zeit.Jahr}");
        }),
    };
}
