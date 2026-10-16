using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für die Stadttore: Torschluss um 20 Uhr, Warten bis zum Morgen, Torgeld mit Nachtfahrt-Erlaubnis.</summary>
static class ToreTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static Wirtschaftsdaten Daten() => Wirtschaftsdaten.AusJson(Json);

    /// <summary>Spielminute dieser Uhrzeit am ersten Tag (05:00 = 0).</summary>
    static long Um(int stunde, int minute = 0) => (stunde * 60 + minute) - Spielzeit.TagesbeginnMinute;

    /// <summary>Eine Stadt, in der um <paramref name="stunde"/> Uhr eine Grube mit vollem Lager ihren Ochsenkarren losschickt.</summary>
    static (Simulation Sim, Konto K, Betrieb Mine) Aufbau(int stunde, int minute = 0, Wirtschaftsdaten? daten = null)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { StartStatut = Fronstatut.FreieStadt, Siegbedingung = Siegbedingung.Endlos },
            new Spielzeit(Um(stunde, minute)), daten ?? Daten());
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
        ("Tore: Um 19 Uhr losgefahren, um 21 Uhr am Tor – der Karren wartet bis 5 Uhr und verkauft dann", () =>
        {
            var (sim, k, mine) = Aufbau(19);
            sim.Tick();
            var karren = mine.Karren[0];
            Pruefe.Gleich(KarrenZustand.ZumMarkt, karren.Zustand);
            Ticken(sim, Um(21, 30));
            Pruefe.Wahr(karren.WartetAmTor(sim.Zeit), "Karren sollte am Tor warten");
            Pruefe.Gleich(KarrenZustand.ZumMarkt, karren.Zustand);
            Pruefe.Gleich(0.0, k.Laufend.Umsatz);
            Pruefe.Wahr(karren.ZustandText(sim.Zeit).Contains("Stadttor"), karren.ZustandText(sim.Zeit));
            Pruefe.Gleich((long)Spielzeit.MinutenProTag, karren.TorWartenBis!.Value);

            // Nächster Morgen (= nächstes Spieljahr): Tor offen, der Karren kommt am Markt an.
            Ticken(sim, Spielzeit.MinutenProTag + 2);
            Pruefe.Wahr(!karren.WartetAmTor(sim.Zeit), "Am Morgen wartet niemand mehr");
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, karren.Zustand);
            Pruefe.Wahr(k.Laufend.Umsatz > 0, "Am Morgen verkauft");
        }),

        ("Tore: Mit Nachtfahrt-Erlaubnis zahlt der Karren 15 Gulden Torgeld je Durchfahrt und fährt durch", () =>
        {
            var (sim, k, mine) = Aufbau(19);
            mine.Karren[0].NachtsFahren = true;
            sim.Tick();
            Ticken(sim, Um(21, 5));
            var karren = mine.Karren[0];
            Pruefe.Wahr(!karren.WartetAmTor(sim.Zeit), "Mit Erlaubnis wartet der Karren nicht");
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, karren.Zustand);
            // Hinein und gleich wieder hinaus: zweimal Torgeld (Eskorte „Keine“ kostet nichts).
            Pruefe.Gleich(30.0, k.Laufend.Transport);
        }),

        ("Tore: Vor 20 Uhr am Tor – offen, kein Torgeld, keine Wartezeit", () =>
        {
            var (sim, k, mine) = Aufbau(17);
            sim.Tick();
            Ticken(sim, Um(19, 5));
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, mine.Karren[0].Zustand);
            Pruefe.Gleich(0.0, k.Laufend.Transport);
            Pruefe.Wahr(mine.Karren[0].TorWartenBis is null, "kein Warten");
        }),

        ("Tore: Werkstätten in der Stadt fahren nicht durchs Tor", () =>
        {
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, new Spielzeit(Um(20, 30)), Daten());
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Spieler");
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, k);
            sim.Tick(); // braucht Zutaten → fährt zum Markt
            Ticken(sim, Um(21, 10));
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, g.Karren[0].Zustand);
            Pruefe.Wahr(g.Karren[0].TorWartenBis is null, "Stadtkarren warten nie am Tor");
        }),

        ("Tore: Ohne Abschnitt „tore“ gibt es keinen Torschluss (alte Daten)", () =>
        {
            var ohne = Wirtschaftsdaten.AusJson(System.Text.RegularExpressions.Regex.Replace(Json, @"""tore"": \{[^}]*\},", ""));
            Pruefe.Wahr(ohne.Logistik.Tore is null, "Abschnitt nicht entfernt");
            var (sim, k, mine) = Aufbau(19, daten: ohne);
            sim.Tick();
            Ticken(sim, Um(21, 5));
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, mine.Karren[0].Zustand);
            Pruefe.Wahr(k.Laufend.Umsatz > 0, "verkauft wie früher");
        }),

        ("Tore: Wer am Tor wartet, wartet auch nach Speichern und Laden weiter", () =>
        {
            var (sim, _, mine) = Aufbau(19);
            sim.Tick();
            Ticken(sim, Um(21, 30));
            Pruefe.Wahr(mine.Karren[0].WartetAmTor(sim.Zeit), "wartet vor dem Speichern");
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim));
            var karren = geladen.Wirtschaft!.Betriebe.Single(b => b.Name == "Grube").Karren[0];
            Pruefe.Wahr(karren.WartetAmTor(geladen.Zeit), "wartet nach dem Laden");
            Pruefe.Gleich(mine.Karren[0].TorWartenBis, karren.TorWartenBis);
        }),

        ("Tore: Daten werden geprüft (Torschluss innerhalb des Spieltags, Torgeld nicht negativ)", () =>
        {
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace("\"schliessenUm\": 20", "\"schliessenUm\": 23")), "schliessenUm");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace("\"torgeld\": 15", "\"torgeld\": -1")), "torgeld");
        }),
    };
}
