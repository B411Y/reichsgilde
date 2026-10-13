using Reichsgilde.Core;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für das Erwerben von Werkstätten und Gruben (Expansion).</summary>
static class LiegenschaftTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);

    /// <summary>Ein Konto, das schon Bürger ist und das gewünschte Vermögen hat.</summary>
    static Konto Buerger(Stadtwirtschaft w, string name, double vermoegen)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 10000);
        Pruefe.Wahr(w.Ausfuehren(k, new StandErwerben()).Erfolg, "Aufstieg zum Bürger fehlgeschlagen");
        if (vermoegen > k.Vermoegen) k.Umsatz(vermoegen - k.Vermoegen); else k.Schaden(k.Vermoegen - vermoegen);
        return k;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Liegenschaften: drei sind frei, erst ab Bürger zu haben", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            Pruefe.Gleich(3, w.FreieLiegenschaften.Count());
            var gemeiner = w.KontoEroeffnen("Gemeiner", startvermoegen: 50000);
            var e = w.Ausfuehren(gemeiner, new LiegenschaftErwerben("Gießerei an der Südgasse"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("erst ab dem Stand Bürger"), e.Meldung);
        }),

        ("Werkstatt kaufen: Kaufpreis als Anschaffung, Betrieb mit Handkarren, nicht mehr frei", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = Buerger(w, "Spieler", 8000);
            Betrieb? erworben = null;
            w.BetriebErworben += b => erworben = b;
            var e = w.Ausfuehren(k, new LiegenschaftErwerben("Gießerei an der Südgasse"));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Ungefaehr(4500, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(3500, k.Laufend.Investitionen, 1e-9);
            Pruefe.Gleich("Gießerei an der Südgasse", erworben!.Name);
            Pruefe.Gleich(k, erworben.Konto);
            Pruefe.Gleich("Handkarren", erworben.Karren.Single().Typ);
            Pruefe.Gleich(0, erworben.Belegt);
            Pruefe.Gleich(2, w.FreieLiegenschaften.Count());
        }),

        ("Grube pachten: kein Kaufpreis, aber die erste Jahrespacht muss gedeckt sein", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var arm = Buerger(w, "Arm", 300);
            var e = w.Ausfuehren(arm, new LiegenschaftErwerben("Grube am Silberberg"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("erste Jahrespacht"), e.Meldung);

            var k = Buerger(w, "Spieler", 5000);
            Pruefe.Wahr(w.Ausfuehren(k, new LiegenschaftErwerben("Grube am Silberberg")).Erfolg, "Pacht abgelehnt");
            Pruefe.Ungefaehr(5000, k.Vermoegen, 1e-9);
            var grube = w.Betriebe.Single(b => b.Name == "Grube am Silberberg");
            Pruefe.Gleich(Betriebsart.Mine, grube.Art);
            Pruefe.Gleich("Ochsenkarren", grube.Karren.Single().Typ);
            for (var i = 0; i < Spielzeit.MinutenProTag; i++) sim.Tick();
            Pruefe.Ungefaehr(w.Daten.Allgemein.PachtMine, k.Vorjahr!.Fixkosten, 1e-9); // Pacht am Jahresende
        }),

        ("Jede Liegenschaft gibt es nur einmal", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var a = Buerger(w, "A", 20000);
            var b = Buerger(w, "B", 20000);
            Pruefe.Wahr(w.Ausfuehren(a, new LiegenschaftErwerben("Grube im Tannenwald")).Erfolg, "erster Erwerb abgelehnt");
            var e = w.Ausfuehren(b, new LiegenschaftErwerben("Grube im Tannenwald"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("gehört bereits A"), e.Meldung);
            Pruefe.Wahr(!w.Ausfuehren(b, new LiegenschaftErwerben("Burg Drachenfels")).Erfolg, "unbekannte Liegenschaft angenommen");
        }),

        ("Erwartung: Eine Grube unter Statut B bringt mehr als eine Werkstatt", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var grube = Kalkulation.ErwarteterJahresgewinn(w, Betriebsart.Mine);
            var werkstatt = Kalkulation.ErwarteterJahresgewinn(w, Betriebsart.Giesserei);
            Pruefe.Wahr(grube > werkstatt && werkstatt > 0, $"Grube {grube:0}, Werkstatt {werkstatt:0}");
        }),

        ("KI expandiert als Bürger – aber nur bis zu ihrer Höchstzahl an Betrieben", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Buerger(w, "Rivale", 30000);
            w.KiAnmelden(k, KiProfil.Standard with { MaxBetriebe = 2 });
            Pruefe.Gleich(1, w.Betriebe.Count(b => b.Konto == k)); // höchstens ein Erwerb pro Stunde
            for (var i = 0; i < 3 * 60; i++) sim.Tick();
            Pruefe.Gleich(2, w.Betriebe.Count(b => b.Konto == k));
            Pruefe.Wahr(w.Betriebe.Where(b => b.Konto == k).All(b => b.Belegt > 0), "neue Betriebe ohne Personal");
            Pruefe.Gleich(1, w.FreieLiegenschaften.Count());
        }),

        ("Daten: Mindeststand muss es geben", () =>
        {
            var json = File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"mindestStand\": \"Bürger\"", "\"mindestStand\": \"Kaiser\"");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json), "mindestStand „Kaiser“ gibt es nicht");
        }),
    };
}
