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
    /// <summary>Sucht game/data/wirtschaft.json, egal von wo aus die Tests gestartet werden.</summary>
    public static string Wirtschaft()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null)
        {
            var pfad = Path.Combine(ordner.FullName, "game", "data", "wirtschaft.json");
            if (File.Exists(pfad)) return pfad;
            ordner = ordner.Parent;
        }
        throw new FileNotFoundException("game/data/wirtschaft.json wurde nicht gefunden.");
    }

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
}

static class WirtschaftTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

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
        ("Wirtschaftsdaten: game/data/wirtschaft.json wird geladen und ist gültig", () =>
        {
            var d = Daten.Value;
            Pruefe.Gleich(45, d.Waren.Count); // 9 Schmied, 8 Tischler, 9 Steinmetz, 9 Alchimist (mit Kräuterschnaps), 9 Wirt, Gold (nur Handel)
            Pruefe.Gleich(5, d.Arbeitskraefte.Count);
            Pruefe.Gleich(90.0, d.Waren["Silberring"].Grundpreis);
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
            Pruefe.Ungefaehr(90 * Math.Sqrt(150.0 / 105), ring.Preis, 0.01); // nur NPC-Angebot: knapp
            ring.Verkaufen(45);                                               // 105 + 45 = 150 = Nachfrage
            Pruefe.Ungefaehr(90, ring.Preis, 0.01);
            ring.Verkaufen(150);                                              // Angebot 300 = doppelt
            Pruefe.Ungefaehr(90 * Math.Sqrt(0.5), ring.Preis, 0.01);
        }),

        ("Markt: Preis bleibt in den Grenzen (50 % bis 200 %)", () =>
        {
            var markt = new Markt(Daten.Value);
            markt["Rapier"].Verkaufen(5000);
            Pruefe.Ungefaehr(55, markt["Rapier"].Preis, 0.01);
        }),

        ("Markt: Verkäufe zählen genau 24 Spielstunden lang (= ein Jahr)", () =>
        {
            var markt = new Markt(Daten.Value);
            markt["Eisen"].Verkaufen(60);
            for (var h = 0; h < 24; h++) markt.NeueStunde();       // genau 24 Stunden später
            Pruefe.Gleich(60.0, markt["Eisen"].SpielerAngebotImJahr);
            for (var m = 0; m < 30; m++) markt.NeueMinute();        // eine halbe Stunde darüber hinaus
            Pruefe.Ungefaehr(30.0, markt["Eisen"].SpielerAngebotImJahr, 0.001);
            markt.NeueStunde();                                     // 25 Stunden später
            Pruefe.Gleich(0.0, markt["Eisen"].SpielerAngebotImJahr);
        }),

        ("Arbeitszeit: 08:00 bis 20:00 Uhr = 720 Minuten pro Spieljahr", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var minuten = 0;
            for (var m = 0; m < Spielzeit.MinutenProTag; m++)
                if (w.IstArbeitszeit(new Spielzeit(m))) minuten++;
            Pruefe.Gleich(720, minuten);
            Pruefe.Gleich(false, w.IstArbeitszeit(new Spielzeit(119)));  // 07:59
            Pruefe.Gleich(true, w.IstArbeitszeit(new Spielzeit(120)));   // 08:00
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
        ("Gießerei allein, 2 Gesellen, Silberring: Gewinn ≈ 848 Gulden/Jahr", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var konto = w.KontoEroeffnen("Spieler");
            var g = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, konto);
            w.Einstellen(g, "Geselle", 2);
            Pruefe.Ungefaehr(848, MittlererGewinn(sim, konto).Gewinn, 0.05);
        }),

        ("Gießerei, 1 Lehrling, Silberring: Gewinn ≈ 289 Gulden/Jahr", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var konto = w.KontoEroeffnen("Spieler");
            w.Einstellen(w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, konto), "Lehrling", 1);
            Pruefe.Ungefaehr(289, MittlererGewinn(sim, konto).Gewinn, 0.05);
        }),

        ("Konkurrenz: Zwei Gießereien teilen sich den Silberring-Markt, je ≈ 611 Gulden", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var spieler = w.KontoEroeffnen("Spieler");
            var rivale = w.KontoEroeffnen("Rivale");
            w.Einstellen(w.BetriebEroeffnen("Gießerei Spieler", Betriebsart.Giesserei, spieler), "Geselle", 2);
            w.Einstellen(w.BetriebEroeffnen("Gießerei Rivale", Betriebsart.Giesserei, rivale), "Geselle", 2);
            Pruefe.Ungefaehr(611, MittlererGewinn(sim, spieler).Gewinn, 0.05);
            Pruefe.Ungefaehr(spieler.Vermoegen, rivale.Vermoegen, 0.05);
        }),

        ("Mine, 3 Bergleute (Statut A), Ochsenkarren ohne Eskorte: Gewinn ≈ 947 Gulden/Jahr", () =>
        {
            var sim = NeueSimulation(Fronstatut.FreieStadt);
            var w = sim.Wirtschaft!;
            var konto = w.KontoEroeffnen("Spieler");
            w.Einstellen(w.BetriebEroeffnen("Mine", Betriebsart.Mine, konto), "Bergmann", 3);
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
            w.Einstellen(mine, "Bergmann", 1);
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
