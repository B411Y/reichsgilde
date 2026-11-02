using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für Lager, Karren, Eskorten und Banditenüberfälle.</summary>
static class LogistikTests
{
    static Wirtschaftsdaten Daten() => Wirtschaftsdaten.AusJson(Datenpfad.OhneJahreszeiten()); // feste Fahrzeiten ohne Schlamm (Frühling 1400)

    static Simulation NeueSimulation(Fronstatut statut = Fronstatut.FreieStadt, int startwert = 1400, Spielzeit? zeit = null) =>
        new(Partieeinstellungen.Standard with { StartStatut = statut, Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            zeit, Daten());

    /// <summary>Lässt die Simulation laufen, bis die Bedingung erfüllt ist (höchstens ein Jahr).</summary>
    static void TickenBis(Simulation sim, Func<bool> bedingung)
    {
        for (var i = 0; i < Spielzeit.MinutenProTag; i++)
        {
            if (bedingung()) return;
            sim.Tick();
        }
        throw new Exception("Bedingung wurde innerhalb eines Spieljahres nicht erreicht.");
    }

    static void JahreSimulieren(Simulation sim, int jahre)
    {
        for (var i = 0; i < jahre * Spielzeit.MinutenProTag; i++)
            sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Startausstattung: Gießerei mit Handkarren, Mine mit Ochsenkarren", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = w.KontoEroeffnen("Spieler");
            Pruefe.Gleich("Handkarren", w.BetriebEroeffnen("G", Betriebsart.Giesserei, k).Karren.Single().Typ);
            Pruefe.Gleich("Ochsenkarren", w.BetriebEroeffnen("M", Betriebsart.Mine, k).Karren.Single().Typ);
            Pruefe.Gleich(w.Daten.Allgemein.Startvermoegen, k.Vermoegen); // Startausstattung kostet nichts
        }),

        ("Fahrzeiten: 30 Minuten in der Stadt, 120 Minuten ins Umland", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Spieler");
            var giesserei = w.BetriebEroeffnen("G", Betriebsart.Giesserei, k);
            var mine = w.BetriebEroeffnen("M", Betriebsart.Mine, k);
            mine.Einlagern("Eisen", 40);

            sim.Tick(); // 06:00 – Gießerei braucht Zutaten, Mine hat volles Lager
            var start = sim.Zeit.VergangeneMinuten - 1;
            Pruefe.Gleich(KarrenZustand.ZumMarkt, giesserei.Karren[0].Zustand);
            Pruefe.Gleich(KarrenZustand.ZumMarkt, mine.Karren[0].Zustand);
            Pruefe.Gleich(start + 30, giesserei.Karren[0].Ankunft);
            Pruefe.Gleich(start + 120, mine.Karren[0].Ankunft);
        }),

        ("Ladeflächen: Ein Ochsenkarren lädt 2 × 20 Eisen, wenn nur Eisen da ist", () =>
        {
            var sim = NeueSimulation(startwert: 5);
            var w = sim.Wirtschaft!;
            var mine = w.BetriebEroeffnen("M", Betriebsart.Mine, w.KontoEroeffnen("Spieler"));
            mine.Einlagern("Eisen", 40);
            sim.Tick();
            var karren = mine.Karren[0];
            // Falls Banditen zugeschlagen haben, ist die Ladung weg – dann zählt, was das Lager verlassen hat.
            Pruefe.Gleich(0, mine.ImLager("Eisen"));
            Pruefe.Gleich(true, karren.Ladung.GetValueOrDefault("Eisen") is 40 or 0);
        }),

        ("Nachts fährt kein Karren ins Umland – außer man erlaubt es", () =>
        {
            var nacht = new Spielzeit(21 * 60 - Spielzeit.TagesbeginnMinute); // 21:00 Uhr
            var sim = NeueSimulation(zeit: nacht);
            var w = sim.Wirtschaft!;
            var mine = w.BetriebEroeffnen("M", Betriebsart.Mine, w.KontoEroeffnen("Spieler"));
            mine.Einlagern("Eisen", 40);
            sim.Tick();
            Pruefe.Gleich(KarrenZustand.AmBetrieb, mine.Karren[0].Zustand);
            mine.Karren[0].NachtsFahren = true;
            sim.Tick();
            Pruefe.Gleich(KarrenZustand.ZumMarkt, mine.Karren[0].Zustand);
        }),

        // Seit PR #232 nur bis zum Vorrat weniger Arbeitsstunden, mindestens für 2 × 5 Stück (vorher: eine volle Ladefläche à 15, bis zur Lagergrenze).
        ("Verwalter: kauft knappe Zutaten nach und bringt sie ins Lager – ohne Personal den Vorrat für 10 Stück", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, w.KontoEroeffnen("Spieler"));
            var karren = g.Karren[0];
            TickenBis(sim, () => karren.Zustand == KarrenZustand.ZumBetrieb);
            Pruefe.Gleich(10, karren.Ladung.Values.Sum()); // Handkarren: 1 Ladefläche à 15, gekauft wird aber nur bis zum Vorrat
            TickenBis(sim, () => karren.Zustand == KarrenZustand.AmBetrieb);
            Pruefe.Gleich(10, g.ImLager("Eisen") + g.ImLager("Silber"));
        }),

        ("Verwalter: Vorrat nach Bedarf – mit mehr Leistung mehr Zutaten, höchstens bis zur Lagergrenze", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, w.KontoEroeffnen("Spieler", startvermoegen: 50_000));
            w.Einstellen(g, "Geselle", 2);
            g.Produkt = "Beschlag"; // 2 Eisen je Stück, 35 Minuten: 2 Gesellen schaffen ≈ 3,4 Stück je Stunde → 4 Stunden ≈ 14 Stück = 28 Eisen
            var karren = g.Karren[0];
            var bisher = 0;
            for (var fahrt = 0; fahrt < 6; fahrt++)
            {
                TickenBis(sim, () => karren.Zustand == KarrenZustand.ZumBetrieb);
                TickenBis(sim, () => karren.Zustand == KarrenZustand.AmBetrieb);
                bisher = Math.Max(bisher, g.ImLager("Eisen"));
            }
            var stunden = w.Daten.Logistik.ZutatenVorratStunden;
            var ziel = (int)Math.Ceiling(Kalkulation.Leistung(w, g) * 60 / g.MinutenJeStueck("Beschlag") * stunden * 2);
            Pruefe.Wahr(ziel > 20 && ziel < 40, $"Vorrat {ziel} Eisen");
            Pruefe.Wahr(bisher > 10 && bisher <= ziel, $"höchstens {bisher} Eisen im Lager (Ziel {ziel})");
        }),

        ("Verwalter: kauft nur von freiem Geld – die Kosten des Jahresabschlusses bleiben in der Kasse, ausgegeben wird höchstens die Hälfte des Rests", () =>
        {
            var sim = NeueSimulation(); // 04:00 Uhr: Bis 8 Uhr wird nichts verarbeitet
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Spieler");
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, k);
            w.Einstellen(g, "Geselle", 2);
            g.Produkt = "Beschlag"; // 2 Eisen je Stück
            var ruecklage = w.OffeneJahreskosten(k);
            Pruefe.Ungefaehr(g.Lohnkosten + g.Artdaten.Fixkosten, ruecklage, 1e-9); // kein Wohnhaus, keine Familie, kein Schiff
            g.Einlagern("Eisen", 10);     // reicht für 5 Stück: die Werkstatt stockt nicht
            g.Einlagern("Beschlag", 15);  // volle Ladefläche: Der Karren fährt zum Verkauf
            k.Zutaten(k.Vermoegen - ruecklage + 500); // 500 Gulden fehlen bis zur Rücklage – der Verkauf bringt etwas mehr
            var karren = g.Karren[0];
            var zutatenVorher = g.Laufend.Zutaten;
            TickenBis(sim, () => karren.Zustand == KarrenZustand.ZumBetrieb);
            var ausgegeben = g.Laufend.Zutaten - zutatenVorher;
            var vorDemKauf = k.Vermoegen + ausgegeben;
            Pruefe.Wahr(vorDemKauf > ruecklage, $"nach dem Verkauf {vorDemKauf:F0} Gulden, Rücklage {ruecklage:F0}");
            Pruefe.Wahr(ausgegeben > 0 && ausgegeben <= (vorDemKauf - ruecklage) * 0.5 + 1e-9,
                $"für {ausgegeben:F0} Gulden gekauft, frei waren {vorDemKauf - ruecklage:F0}");
            // Platz wäre für 15 Eisen (eine Ladefläche; Vorrat für 4 Stunden ≈ 28 Eisen, 10 liegen schon da) – das Geld begrenzt.
            Pruefe.Wahr(karren.Ladung.GetValueOrDefault("Eisen") is > 0 and < 15, $"{karren.Ladung.GetValueOrDefault("Eisen")} Eisen – das Geld begrenzt, nicht der Platz");
        }),

        // Nachbesserung #232: Vorher kaufte der Karren bei knapper Kasse gar nichts – die Werkstatt stand still bis zum Bankrott.
        ("Verwalter: stockt die Werkstatt, kauft er wenigstens das Nötigste (Zutaten für 5 Stück), auch über das freie Geld hinaus – und meldet es einmal", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var meldungen = new List<string>();
            w.Meldung += meldungen.Add;
            var k = w.KontoEroeffnen("Spieler");
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, k);
            w.Einstellen(g, "Geselle", 2);
            var ruecklage = w.OffeneJahreskosten(k);
            k.Zutaten(k.Vermoegen - ruecklage - 10); // nur 10 Gulden frei: nach der alten Regel kein Stück Eisen (≈ 12,6), kein Silber (≈ 30)
            var karren = g.Karren[0];
            TickenBis(sim, () => karren.Zustand == KarrenZustand.ZumBetrieb);
            Pruefe.Gleich(5, karren.Ladung.GetValueOrDefault("Eisen"), "Silberring: 1 Eisen + 1 Silber je Stück, die eine Ladefläche nimmt Eisen für 5 Stück");
            Pruefe.Gleich(1, karren.Ladung.Count);
            Pruefe.Wahr(k.Vermoegen > 0 && k.Vermoegen < ruecklage, $"Vermögen {k.Vermoegen:F0}");
            TickenBis(sim, () => karren.Zustand == KarrenZustand.AmBetrieb);
            TickenBis(sim, () => karren.Zustand == KarrenZustand.ZumBetrieb);
            Pruefe.Gleich(5, karren.Ladung.GetValueOrDefault("Silber"), "zweite Fahrt: Silber für 5 Stück");
            Pruefe.Gleich(1, meldungen.Count(m => m.Contains("nur das Nötigste")), string.Join(" | ", meldungen.Where(m => m.Contains("Nötigste"))));
        }),

        ("Verwalter: Werkstatt mit knapper Kasse arbeitet sich heraus – Kasse knapp unter oder über der Rücklage, kein Bankrott in 4 Jahren", () =>
        {
            foreach (var abstand in new[] { -50.0, 40.0 })
            {
                var sim = NeueSimulation();
                var w = sim.Wirtschaft!;
                var k = w.KontoEroeffnen("Spieler");
                var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, k);
                w.Einstellen(g, "Geselle", 2);
                g.Produkt = "Silberring";
                k.Zutaten(k.Vermoegen - w.OffeneJahreskosten(k) - abstand);
                for (var jahr = 1; jahr <= 4; jahr++)
                {
                    JahreSimulieren(sim, 1);
                    Pruefe.Wahr(!k.IstBankrott, $"Abstand {abstand}: bankrott im {jahr}. Jahr");
                    Pruefe.Wahr(g.Vorjahr?.Umsatz > 0, $"Abstand {abstand}: kein Umsatz im {jahr}. Jahr");
                }
            }
        }),

        ("Verwalter: zutatenVorratStunden muss größer als 0 sein, zutatenAnteilKasse größer als 0 und höchstens 1 – sonst fällt es beim Laden auf", () =>
        {
            var json = File.ReadAllText(Datenpfad.Wirtschaft());
            Pruefe.Wahr(json.Contains("\"zutatenVorratStunden\": 4,") && json.Contains("\"zutatenAnteilKasse\": 0.5,"), "Werte in wirtschaft.json");
            foreach (var (alt, neu, feld) in new[]
            {
                ("\"zutatenVorratStunden\": 4,", "\"zutatenVorratStunden\": 0,", "zutatenVorratStunden"),
                ("\"zutatenVorratStunden\": 4,", "\"zutatenVorratStunden\": -2,", "zutatenVorratStunden"),
                ("\"zutatenAnteilKasse\": 0.5,", "\"zutatenAnteilKasse\": 0,", "zutatenAnteilKasse"),
                ("\"zutatenAnteilKasse\": 0.5,", "\"zutatenAnteilKasse\": 1.5,", "zutatenAnteilKasse"),
            })
                Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json.Replace(alt, neu)), feld);
            Pruefe.Ungefaehr(1.0, Wirtschaftsdaten.AusJson(json.Replace("\"zutatenAnteilKasse\": 0.5,", "\"zutatenAnteilKasse\": 1,")).Logistik.ZutatenAnteilKasse, 1e-12);
        }),

        ("Eskorte kostet nur bei beladener Fahrt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Spieler");
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, k);
            g.Karren[0].Eskorte = "Normal";
            TickenBis(sim, () => g.Karren[0].Zustand == KarrenZustand.ZumBetrieb);
            // Hinweg leer (nur zum Einkaufen) = 0, Rückweg mit Zutaten = 10
            Pruefe.Gleich(10.0, k.Laufend.Transport);
        }),

        ("Produktion ruht ohne Zutaten – und meldet es einmal", () =>
        {
            var sim = NeueSimulation(zeit: new Spielzeit(8 * 60 - Spielzeit.TagesbeginnMinute)); // 08:00
            var w = sim.Wirtschaft!;
            var meldungen = new List<string>();
            w.Meldung += meldungen.Add;
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, w.KontoEroeffnen("Spieler"), startkarren: "");
            w.Einstellen(g, "Geselle", 2);
            for (var i = 0; i < 120; i++) sim.Tick();
            Pruefe.Gleich(true, g.Stillstand?.Contains("Es fehlt") == true, g.Stillstand ?? "kein Stillstand");
            Pruefe.Gleich(1, meldungen.Count(m => m.Contains("Es fehlt")));
            Pruefe.Gleich(0, g.ImLager("Silberring"));
        }),

        ("Volles Lager stoppt die Förderung", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var mine = w.BetriebEroeffnen("M", Betriebsart.Mine, w.KontoEroeffnen("Spieler"), startkarren: "");
            w.Einstellen(mine, "Altknecht", 3);
            JahreSimulieren(sim, 1);
            Pruefe.Gleich(mine.LagerKapazitaetJeWare, mine.ImLager("Eisen"));
            Pruefe.Gleich(true, mine.Stillstand?.Contains("voll") == true, mine.Stillstand ?? "kein Stillstand");
        }),

        ("Karren kaufen: Investition mindert das Vermögen, nicht den Gewinn", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = w.KontoEroeffnen("Spieler");
            var mine = w.BetriebEroeffnen("M", Betriebsart.Mine, k);
            mine.KarrenKaufen("Kriegskarren");
            Pruefe.Gleich(2, mine.Karren.Count);
            Pruefe.Gleich(w.Daten.Allgemein.Startvermoegen - 900, k.Vermoegen);
            Pruefe.Gleich(900.0, k.Laufend.Investitionen);
            Pruefe.Gleich(0.0, k.Laufend.Gewinn);
        }),

        // ── Banditen ────────────────────────────────────────────────────────
        ("Banden: alle sechs Völker kommen vor, auch gemischte Banden und wilde Bestien", () =>
        {
            var banden = Daten().Logistik.Banden;
            var voelker = banden.SelectMany(b => b.Voelker.Keys).Distinct().ToHashSet();
            foreach (var volk in Enum.GetNames<Volk>())
                Pruefe.Gleich(true, voelker.Contains(volk), $"Volk {volk} fehlt unter den Banden");
            Pruefe.Gleich(true, banden.Any(b => b.Voelker.Count >= 3 && !b.Wild), "gemischte Bande mit mind. 3 Völkern");
            Pruefe.Gleich(true, banden.Any(b => b.Wild), "wilde Bestien");
            Pruefe.Gleich(true, banden.Count(b => !b.Wild) > banden.Count(b => b.Wild), "mehr Banditen als wilde Bestien");
        }),

        ("Überfälle: Häufigkeit passt zu 8 % je beladener Umland-Fahrt (100 Jahre)", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var ueberfaelle = 0;
            w.Meldung += m => { if (m.StartsWith("Überfall")) ueberfaelle++; };
            w.Einstellen(w.BetriebEroeffnen("M", Betriebsart.Mine, w.KontoEroeffnen("Spieler")), "Altknecht", 3);
            JahreSimulieren(sim, 100);
            // 126 Stück/Jahr ÷ 40 je Fahrt ≈ 3,2 beladene Fahrten × 100 Jahre × 8 % ≈ 25
            Pruefe.Gleich(true, ueberfaelle is >= 12 and <= 45, $"{ueberfaelle} Überfälle");
        }),

        ("Eskorte wirkt: Normale Eskorte halbiert den Raub, schwere viertelt ihn ungefähr", () =>
        {
            double Raub(string eskorte)
            {
                var sim = NeueSimulation(startwert: 99);
                var w = sim.Wirtschaft!;
                var k = w.KontoEroeffnen("Spieler");
                var mine = w.BetriebEroeffnen("M", Betriebsart.Mine, k);
                w.Einstellen(mine, "Altknecht", 3);
                mine.Karren[0].Eskorte = eskorte;
                double summe = 0;
                for (var j = 0; j < 100; j++) { JahreSimulieren(sim, 1); summe += k.Vorjahr!.Raubwert; }
                return summe;
            }
            var ohne = Raub("Keine");
            var normal = Raub("Normal");
            var schwer = Raub("Schwer");
            Pruefe.Gleich(true, normal < 0.7 * ohne, $"ohne {ohne:F0}, normal {normal:F0}");
            Pruefe.Gleich(true, schwer < 0.45 * ohne, $"ohne {ohne:F0}, schwer {schwer:F0}");
        }),

        ("Überfall-Meldung nennt Bande, Völker und Beute", () =>
        {
            var sim = NeueSimulation(startwert: 3);
            var w = sim.Wirtschaft!;
            string? meldung = null;
            w.Meldung += m => { if (m.Contains("erbeutet")) meldung ??= m; };
            w.Einstellen(w.BetriebEroeffnen("Grube", Betriebsart.Mine, w.KontoEroeffnen("Spieler")), "Altknecht", 3);
            for (var j = 0; j < 30 && meldung is null; j++) JahreSimulieren(sim, 1);
            Pruefe.Gleich(true, meldung is not null, "in 30 Jahren kein gelungener Überfall");
            Pruefe.Gleich(true, meldung!.Contains("Grube") && meldung.Contains("Wert") && meldung.Contains("("), meldung);
        }),
    };
}
