using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests für das Verweilen am Markt (Datenversion 98, Nutzer 23.11.2026): Ein automatisch fahrender Karren bleibt nach dem Handel
/// „logistik.verweilenMinuten“ (20) am Markt stehen – die Zeit steht für das Ab- und Aufladen – und fährt erst dann zurück.
/// </summary>
static class KarrenVerweilenTests
{
    static readonly string Json = Datenpfad.OhneJahreszeiten(); // ohne Schlamm im Frühling 1400

    /// <summary>Spielminute dieser Uhrzeit am ersten Tag.</summary>
    static long Um(int stunde, int minute = 0) => (stunde * 60 + minute) - Spielzeit.TagesbeginnMinute;

    /// <summary>Eine Stadt, in der um 10 Uhr eine Grube mit vollem Lager ihren Karren losschickt.</summary>
    static (Simulation Sim, Konto K, Betrieb Mine) Aufbau(string? json = null)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { StartStatut = Fronstatut.FreieStadt, Siegbedingung = Siegbedingung.Endlos },
            new Spielzeit(Um(10)), Wirtschaftsdaten.AusJson(json ?? Json));
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Spieler");
        var mine = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k);
        mine.Einlagern("Eisen", 40);
        return (sim, k, mine);
    }

    static void Ticken(Simulation sim, long bisMinute)
    {
        while (sim.Zeit.VergangeneMinuten < bisMinute) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Karren verweilen: Daten 98 mit verweilenMinuten 20", () =>
        {
            var d = Wirtschaftsdaten.AusJson(Json);
            Pruefe.Gleich(98, d.Version);
            Pruefe.Gleich(20, d.Logistik.VerweilenMinuten);
        }),

        ("Karren verweilen: Nach dem Verkauf bleibt der Karren 20 Minuten am Markt stehen, dann fährt er zurück", () =>
        {
            var (sim, k, mine) = Aufbau();
            var w = sim.Wirtschaft!;
            sim.Tick();
            var karren = mine.Karren[0];
            Pruefe.Gleich(KarrenZustand.ZumMarkt, karren.Zustand);
            Pruefe.Wahr(w.FaehrtAutomatisch(karren), "bis zum Selbstfahren fahren alle Karren automatisch");
            var ankunft = karren.Ankunft;

            Ticken(sim, ankunft + 1);
            Pruefe.Wahr(k.Laufend.Umsatz > 0, "bei der Ankunft verkauft");
            Pruefe.Gleich(KarrenZustand.ZumMarkt, karren.Zustand);
            Pruefe.Gleich(ankunft + 20, karren.VerweiltBis!.Value);
            Pruefe.Wahr(karren.Verweilt(sim.Zeit), "verweilt am Markt");
            Pruefe.Wahr(!karren.Faehrt(sim.Zeit), "beim Verweilen fährt er nicht");
            Pruefe.Wahr(karren.ZustandText(sim.Zeit).Contains("lädt am Markt"), karren.ZustandText(sim.Zeit));

            Ticken(sim, ankunft + 19);
            Pruefe.Gleich(KarrenZustand.ZumMarkt, karren.Zustand);
            Ticken(sim, ankunft + 21);
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, karren.Zustand);
            Pruefe.Wahr(karren.VerweiltBis is null, "nach dem Verweilen zurückgesetzt");
            Pruefe.Wahr(karren.Faehrt(sim.Zeit), "fährt zurück");
            Pruefe.Gleich(ankunft + 20 + w.Fahrzeit(karren, zumMarkt: false), karren.Ankunft);
        }),

        ("Karren verweilen: Ohne verweilenMinuten (ältere Daten) kehrt der Karren sofort um", () =>
        {
            const string wert = "\"verweilenMinuten\": 20,";
            if (!Json.Contains(wert, StringComparison.Ordinal)) throw new InvalidDataException("verweilenMinuten nicht gefunden");
            var (sim, _, mine) = Aufbau(Json.Replace(wert, "", StringComparison.Ordinal));
            Pruefe.Gleich(0, sim.Wirtschaft!.Daten.Logistik.VerweilenMinuten);
            sim.Tick();
            var karren = mine.Karren[0];
            var ankunft = karren.Ankunft;
            Ticken(sim, ankunft + 1);
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, karren.Zustand);
            Pruefe.Wahr(karren.VerweiltBis is null, "verweilt nicht");
        }),

        ("Karren verweilen: Ein Handelsauftrag, der unterwegs vorgemerkt wird, rechnet das Verweilen in die Rückkehr ein", () =>
        {
            var (sim, _, mine) = Aufbau();
            var w = sim.Wirtschaft!;
            sim.Tick();
            var karren = mine.Karren[0];
            var zurueck = karren.Ankunft + 20 + w.Fahrzeit(karren, zumMarkt: false);
            var um = sim.Zeit.UhrzeitInMinuten + (int)(zurueck - sim.Zeit.VergangeneMinuten);
            var ergebnis = w.Ausfuehren(mine.Konto, new Handelsauftrag(mine.Name, 0, new Dictionary<string, int>(), new Dictionary<string, int> { ["Holz"] = 1 }));
            Pruefe.Wahr(ergebnis.Erfolg, ergebnis.Meldung);
            Pruefe.Wahr(ergebnis.Meldung.Contains($"{um / 60:00}:{um % 60:00}"), ergebnis.Meldung);
        }),

        ("Karren verweilen: Der Spielstand behält das Verweilen; ohne Verweilen steht kein Feld im Spielstand", () =>
        {
            var (sim, k, mine) = Aufbau();
            var leer = Speicherung.Speichern(sim, k, new DateTime(2026, 1, 1));
            Pruefe.Wahr(!leer.Contains("VerweiltBis", StringComparison.Ordinal), "ohne Verweilen kein Feld");

            sim.Tick();
            var karren = mine.Karren[0];
            Ticken(sim, karren.Ankunft + 5);
            var bis = karren.VerweiltBis!.Value;
            var text = Speicherung.Speichern(sim, k, new DateTime(2026, 1, 1));
            var (geladen, spieler) = Speicherung.Laden(text);
            var gk = geladen.Wirtschaft!.Betriebe.Single(b => b.Name == "Grube").Karren[0];
            Pruefe.Gleich(bis, gk.VerweiltBis!.Value);
            Pruefe.Gleich(text, Speicherung.Speichern(geladen, spieler, new DateTime(2026, 1, 1)));
            Ticken(geladen, bis + 1);
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, gk.Zustand);
        }),
    };
}
