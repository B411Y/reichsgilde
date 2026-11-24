using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Karren selbst fahren, Einkauf ohne Händlerspanne (K4, Nutzer 24.11.2026, F12): Ein selbst gefahrener Karren kauft am Markt und am Hafen zum
/// reinen Marktpreis wie der Verwalter (<see cref="Stadtwirtschaft.KarrenKaufpreis"/> = <see cref="Marktstand.Kaufen"/>), nicht zum Handkaufpreis
/// mit Händlerspanne. Der Handel von Hand (Handelsauftrag) behält die Spanne.
/// </summary>
static class KarrenEinkaufOhneSpanneTests
{
    static readonly string Json = Datenpfad.OhneJahreszeiten();

    static long Um(int stunde) => stunde * 60 - Spielzeit.TagesbeginnMinute;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Karren selbst, Einkauf: zum Marktpreis ohne Händlerspanne wie der Verwalter; der Handkaufpreis liegt um die Spanne darüber", () =>
        {
            var sim = new Simulation(Partieeinstellungen.Standard with
                {
                    StartStatut = Fronstatut.FreieStadt, Siegbedingung = Siegbedingung.Endlos, KarrenSelbstFahren = true,
                },
                new Spielzeit(Um(10)), Wirtschaftsdaten.AusJson(Json));
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Spieler", startvermoegen: 5_000);
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, k);
            var karren = g.Karren[0];
            Pruefe.Wahr(w.Ausfuehren(k, new KarrenLosschicken("G", new[] { 0 }, null)).Erfolg, "los");
            while (sim.Zeit.VergangeneMinuten <= karren.Ankunft) sim.Tick();
            Pruefe.Gleich(KarrenZustand.AmMarkt, karren.Zustand);

            var preis = w.Markt["Eisen"].Preis;
            Pruefe.Ungefaehr(preis * 10, w.KarrenKaufpreis("Eisen", 10), 1e-9);
            var mitSpanne = w.HandKaufpreis(k, "Eisen", 10);
            Pruefe.Wahr(mitSpanne > w.KarrenKaufpreis("Eisen", 10) * (1 + w.Daten.Handel!.Aufschlag) * 0.99, $"Handkaufpreis {mitSpanne} mit Spanne");
            var vorher = k.Vermoegen;
            var e = w.Ausfuehren(k, new KarrenHandeln("G", 0, new Dictionary<string, int>(), new Dictionary<string, int> { ["Eisen"] = 10 }));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Ungefaehr(preis * 10, vorher - k.Vermoegen, 1e-6);
            Pruefe.Ungefaehr(preis * 10, g.Laufend.Zutaten, 1e-6);
            Pruefe.Ungefaehr(preis, w.Markt["Eisen"].Preis, 1e-9); // wie beim Verwalter hebt der Einkauf den Preis nicht
            Pruefe.Gleich(10, karren.Ladung["Eisen"]);
        }),
    };
}
