using Reichsgilde.Core;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Ungenutzte Karren der KI (PR #219): Die Reparatur nach einem Anschlag zählt nicht als Stillstand – sie sagt nichts über zu knappe Karren.
/// Mit vier Anschlägen je Nacht (PR #208) stand eine Werkstatt sonst bis zum ganzen Jahr „still“, die KI kaufte einen Karren,
/// der dann ein Jahr nicht fuhr, und verkaufte ihn wieder (Messung 6: 45 statt 3).
/// </summary>
static class KiKarrenStillstandTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);

    /// <summary>Eine voll besetzte Gießerei mit Zutaten für das ganze Jahr – nur ein Anschlag hält sie auf.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K, Betrieb B) Giesserei()
    {
        var sim = NeueSimulation();
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 20_000);
        var b = w.BetriebEroeffnen("Gießerei Lind", Betriebsart.Giesserei, k);
        w.Einstellen(b, "Geselle", b.Plaetze);
        foreach (var (zutat, _) in w.Daten.Waren[b.Produkt].Zutaten) b.Einlagern(zutat, 200);
        return (sim, w, k, b);
    }

    static void Jahr(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Karren: Die Reparatur nach einem Anschlag ruht die Arbeit, zählt aber nicht als Stillstand (wie ein Umbau)", () =>
        {
            var (sim, w, _, b) = Giesserei();
            b.Sabotieren(w.Daten.Allgemein.Arbeitsminuten); // ein ganzes Jahr Reparatur
            Jahr(sim);
            Pruefe.Gleich(0, b.StillstandMinutenVorjahr);
            Pruefe.Wahr(b.SabotageMinuten == 0 || b.Stillstand?.Contains("repariert") == true, b.Stillstand ?? "läuft");
        }),

        ("KI-Karren: Nach einem Jahr Reparatur kauft die KI keinen Karren – und verkauft im Jahr darauf keinen ungenutzten", () =>
        {
            var (sim, w, k, b) = Giesserei();
            var ki = w.KiAnmelden(k);
            b.Sabotieren(w.Daten.Allgemein.Arbeitsminuten);
            var karren = b.Karren.Count;
            Jahr(sim);
            Pruefe.Gleich(karren, b.Karren.Count);
            Pruefe.Wahr(ki.Entscheidungen.All(e => !e.Contains("der Arbeitszeit still")), string.Join(" | ", ki.Entscheidungen));
            Jahr(sim);
            Pruefe.Wahr(ki.Entscheidungen.All(e => !e.Contains("nicht gefahren")), string.Join(" | ", ki.Entscheidungen));
        }),
    };
}
