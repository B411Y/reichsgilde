using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für Lager, Karren, Eskorten und Banditenüberfälle.</summary>
static class LogistikTests
{
    static Wirtschaftsdaten Daten() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());

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

        ("Verwalter: kauft knappe Zutaten nach und bringt sie ins Lager", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, w.KontoEroeffnen("Spieler"));
            var karren = g.Karren[0];
            TickenBis(sim, () => karren.Zustand == KarrenZustand.ZumBetrieb);
            Pruefe.Gleich(15, karren.Ladung.Values.Sum()); // Handkarren: 1 Ladefläche à 15
            TickenBis(sim, () => karren.Zustand == KarrenZustand.AmBetrieb);
            Pruefe.Gleich(15, g.ImLager("Eisen") + g.ImLager("Silber"));
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
