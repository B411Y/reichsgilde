using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests: Der „Kriegskarren“ heißt seit 04.11.2026 „Pferdekarren“ (nur der Name). Ältere Spielstände tragen die alte Kennung in ihren
/// mitgespeicherten Daten, in den Karren und in Befehlen – beim Laden wird sie umgestellt, die Partie läuft genau so weiter.
/// </summary>
static class KarrenartUmbenennungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static readonly DateTime Zeitpunkt = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Ein Haus mit einer Grube (Ochsenkarren der Startausstattung) und einem gekauften Pferdekarren, eine Weile gelaufen.</summary>
    static (Simulation Sim, Konto K) GrubeMitPferdekarren()
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 5_000);
        var b = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k);
        Pruefe.Wahr(w.Ausfuehren(k, new KarrenKaufen(b.Name, "Pferdekarren")).Erfolg, "Pferdekarren gekauft");
        for (var i = 0; i < 300; i++) sim.Tick();
        return (sim, k);
    }

    /// <summary>Was von den Karren zählt: Name, Art, Zustand, Ankunft, Ladung – und das Geld des Hauses.</summary>
    static string Zustand(Simulation sim) =>
        string.Join(" | ", sim.Wirtschaft!.Konten.Select(k => k.Vermoegen.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Concat(sim.Wirtschaft.Betriebe.SelectMany(b => b.Karren).Select(c =>
                $"{c.Name}/{c.Typ}/{c.Zustand}/{c.Ankunft}/{string.Join(",", c.Ladung.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}={x.Value}"))}")));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Karrenart: Die Daten kennen den Pferdekarren (3 × 25, 900, Tempo 1,3), den Kriegskarren nicht mehr", () =>
        {
            var karren = Daten.Value.Logistik.Karren;
            Pruefe.Wahr(!karren.ContainsKey("Kriegskarren"), "keine alte Kennung in wirtschaft.json");
            var p = karren["Pferdekarren"];
            Pruefe.Gleich((3, 25, 900.0, 1.3), (p.Sorten, p.KapazitaetJeSorte, p.Preis, p.Tempo));
            Pruefe.Gleich("Pferdekarren", LogistikDaten.Karrenart("Kriegskarren"));
            Pruefe.Gleich("Ochsenkarren", LogistikDaten.Karrenart("Ochsenkarren"));
        }),

        ("Karrenart: Ein Spielstand mit „Kriegskarren“ lädt als Pferdekarren (Daten, Art, Name) und läuft genau wie der heutige weiter", () =>
        {
            var (sim, k) = GrubeMitPferdekarren();
            var json = Speicherung.Speichern(sim, k, Zeitpunkt);
            var alt = json.Replace("Pferdekarren", "Kriegskarren");
            Pruefe.Wahr(alt != json && alt.Contains("\"Kriegskarren\""), "der alte Spielstand trägt die alte Kennung");

            var (neuSim, _) = Speicherung.Laden(json);
            var (altSim, _) = Speicherung.Laden(alt);
            var karren = altSim.Wirtschaft!.Betriebe.Single().Karren;
            Pruefe.Gleich("Ochsenkarren 1, Pferdekarren 2", string.Join(", ", karren.Select(c => c.Name)));
            Pruefe.Gleich("Ochsenkarren, Pferdekarren", string.Join(", ", karren.Select(c => c.Typ)));
            Pruefe.Gleich("Handkarren, Ochsenkarren, Pferdekarren", string.Join(", ", altSim.Wirtschaft.Daten.Logistik.Karren.Keys));
            Pruefe.Gleich(Zustand(neuSim), Zustand(altSim));

            neuSim.Takt.IstPausiert = false;
            altSim.Takt.IstPausiert = false;
            for (var i = 0; i < 600; i++) { neuSim.Tick(); altSim.Tick(); }
            Pruefe.Gleich(Zustand(neuSim), Zustand(altSim));
        }),

        ("Karrenart: Der Befehl mit der alten Kennung (Protokoll, Startoption) kauft einen Pferdekarren", () =>
        {
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 5_000);
            var b = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k);
            var ergebnis = w.Ausfuehren(k, new KarrenKaufen(b.Name, "Kriegskarren"));
            Pruefe.Wahr(ergebnis.Erfolg, ergebnis.Meldung);
            Pruefe.Gleich("Pferdekarren 2", b.Karren[1].Name);
            Pruefe.Gleich(5_000 - 900.0, k.Vermoegen);
        }),
    };
}
