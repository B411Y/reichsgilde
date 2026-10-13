using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für Stände, Rangliste, Schulden und Bankrott.</summary>
static class StandTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation(Siegbedingung sieg = Siegbedingung.Endlos) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = sieg }, wirtschaftsdaten: Daten.Value);

    /// <summary>Vermögen direkt setzen – über eine Buchung, wie sie im Spiel auch vorkommt.</summary>
    static void VermoegenSetzen(Konto k, double ziel)
    {
        if (ziel > k.Vermoegen) k.Umsatz(ziel - k.Vermoegen);
        else k.Schaden(k.Vermoegen - ziel);
    }

    static void JahreTicken(Simulation sim, int jahre)
    {
        for (var i = 0; i < jahre * Spielzeit.MinutenProTag; i++) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Stände: Jeder beginnt als Gemeiner, nächster Stand ist Bürger ab 10.000", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = w.KontoEroeffnen("Spieler");
            Pruefe.Gleich("Gemeiner", w.StandVon(k).Name);
            Pruefe.Gleich("Bürger", w.NaechsterStand(k)!.Name);
            Pruefe.Ungefaehr(10000, w.NaechsterStand(k)!.Schwelle, 1e-9);
        }),

        ("Stände: Aufstieg nur ab der Schwelle, Gebühr wird abgebucht", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = w.KontoEroeffnen("Spieler");
            var e = w.Ausfuehren(k, new StandErwerben());
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("fehlen noch 6.000"), e.Meldung);

            VermoegenSetzen(k, 10000);
            var aufgestiegen = 0;
            w.Aufgestiegen += (_, _) => aufgestiegen++;
            Pruefe.Wahr(w.Ausfuehren(k, new StandErwerben()).Erfolg, "Aufstieg abgelehnt");
            Pruefe.Gleich("Bürger", w.StandVon(k).Name);
            Pruefe.Ungefaehr(9000, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(1000, k.Laufend.Standesgebuehren, 1e-9);
            Pruefe.Gleich(1, aufgestiegen);
            // Die Gebühr ist keine laufende Ausgabe – der Betriebsgewinn bleibt unberührt.
            Pruefe.Ungefaehr(6000, k.Laufend.Gewinn, 1e-9);
        }),

        ("Stände: Immer nur eine Stufe auf einmal, und der Stand bleibt bei Verlusten", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = w.KontoEroeffnen("Spieler");
            VermoegenSetzen(k, 60000);
            w.Ausfuehren(k, new StandErwerben());
            Pruefe.Gleich("Bürger", w.StandVon(k).Name);
            w.Ausfuehren(k, new StandErwerben());
            Pruefe.Gleich("Patrizier", w.StandVon(k).Name);
            VermoegenSetzen(k, 100);
            Pruefe.Gleich("Patrizier", w.StandVon(k).Name);
        }),

        ("Rangliste: erst Stand, dann Vermögen, Bankrotte zuletzt", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var a = w.KontoEroeffnen("A");
            var b = w.KontoEroeffnen("B");
            var c = w.KontoEroeffnen("C");
            VermoegenSetzen(a, 12000);
            w.Ausfuehren(a, new StandErwerben()); // Bürger mit 11.000
            VermoegenSetzen(b, 15000);            // Gemeiner, aber reicher
            c.IstBankrott = true;
            VermoegenSetzen(c, 99999);
            Pruefe.Gleich("A,B,C", string.Join(",", w.Rangliste.Select(k => k.Inhaber)));
        }),

        ("Schulden: erst eine Mahnung, im zweiten Jahr der Bankrott", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Spieler");
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, k);
            w.Einstellen(g, "Geselle", 2);
            var meldungen = new List<string>();
            Konto? bankrott = null;
            w.Meldung += meldungen.Add;
            w.Bankrott += x => bankrott = x;

            VermoegenSetzen(k, -5000);
            JahreTicken(sim, 1);
            Pruefe.Wahr(k.Vermoegen < 0, "Vermögen sollte negativ bleiben");
            Pruefe.Wahr(meldungen.Any(m => m.Contains("Kämmerer mahnt Spieler")), "keine Mahnung");
            Pruefe.Gleich(false, k.IstBankrott);

            JahreTicken(sim, 1);
            Pruefe.Gleich(true, k.IstBankrott);
            Pruefe.Gleich(k, bankrott);
            Pruefe.Gleich(0, g.Belegt);
            Pruefe.Wahr(!w.Ausfuehren(k, new PersonalSetzen("G", "Geselle", 1)).Erfolg, "Bankrotte dürfen nichts mehr");
        }),

        ("Schulden: Wer rechtzeitig ins Plus kommt, wird nicht bankrott", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Spieler");
            VermoegenSetzen(k, -100);
            JahreTicken(sim, 1);
            Pruefe.Gleich(1, k.Schuldenjahre);
            VermoegenSetzen(k, 500);
            JahreTicken(sim, 1);
            Pruefe.Gleich(0, k.Schuldenjahre);
            Pruefe.Gleich(false, k.IstBankrott);
        }),

        ("Letzte Dynastie gewinnt: Partie endet, wenn der Rivale bankrott ist", () =>
        {
            var sim = NeueSimulation(Siegbedingung.LetzteDynastie);
            var w = sim.Wirtschaft!;
            w.KontoEroeffnen("Spieler");
            var rivale = w.KontoEroeffnen("Rivale");
            VermoegenSetzen(rivale, -50000);
            JahreTicken(sim, 1);
            Pruefe.Gleich(false, sim.IstBeendet);
            JahreTicken(sim, 1);
            Pruefe.Gleich(true, sim.IstBeendet);
            Pruefe.Wahr(sim.Endgrund!.Length > 0, "Endgrund fehlt");
        }),

        ("Partie beenden: einmalig, mit Grund, danach steht die Zeit", () =>
        {
            var sim = NeueSimulation();
            var beendet = 0;
            sim.PartieBeendet += () => beendet++;
            sim.Beenden("Bankrott");
            sim.Beenden("noch mal");
            Pruefe.Gleich(1, beendet);
            Pruefe.Gleich("Bankrott", sim.Endgrund);
            var vorher = sim.Zeit.VergangeneMinuten;
            sim.Tick();
            Pruefe.Gleich(vorher, sim.Zeit.VergangeneMinuten);
        }),

        ("KI steigt auf, sobald sie es sich leisten kann", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Rivale");
            VermoegenSetzen(k, 12000);
            w.KiAnmelden(k);
            Pruefe.Gleich("Bürger", w.StandVon(k).Name);
        }),

        ("Daten: Stände müssen aufsteigen und zur Bürgerschwelle passen", () =>
        {
            var json = File.ReadAllText(Datenpfad.Wirtschaft())
                .Replace("\"schwelle\": 20000", "\"schwelle\": 5000");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json), "schwelle muss höher sein");
        }),
    };
}
