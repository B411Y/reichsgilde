using Reichsgilde.Core.Zeit;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests: KI-Häuser heuern Banden gegen Wohnhäuser an – Einbruch wie ein Diebstahl (für alle, die Gaunerstücke nutzen),
/// Brandstiftung wie eine Sabotage (nur die Wagemutige).
/// </summary>
static class KiHausgaunerTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Der Brand gelingt immer.</summary>
    static readonly Lazy<Wirtschaftsdaten> BrandSicher = new(() => Wirtschaftsdaten.AusJson(
        Json.Replace("\"kosten\": 300, \"chance\": 0.5, \"reparaturAnteil\"", "\"kosten\": 300, \"chance\": 1.0, \"reparaturAnteil\"")));

    /// <summary>
    /// Zwei Häuser ohne Betriebe: die KI (5.000 Gulden) und vor ihr ein reiches Haus mit diesem Wohnhaus.
    /// Ohne Betriebe beim Ziel bleiben nur die Gaunerstücke gegen das Wohnhaus.
    /// </summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto Ziel, Konto Ki) Partie(int zielStufe, int gaunerei, Wirtschaftsdaten? daten = null)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten ?? Daten.Value);
        var w = sim.Wirtschaft!;
        var ziel = w.KontoEroeffnen("Haus Reich", startvermoegen: 30_000);
        ziel.WohnhausStufe = zielStufe;
        var ki = w.KontoEroeffnen("Haus Lind", startvermoegen: 5_000);
        w.KiAnmelden(ki, new KiProfil(Gaunerei: gaunerei));
        return (sim, w, ziel, ki);
    }

    /// <summary>Lässt einen Spieltag laufen und sammelt die Aufträge der KI im Hinterzimmer.</summary>
    static List<string> Auftraege(Simulation sim, Stadtwirtschaft w, Konto ki, int tage = 1)
    {
        var auftraege = new List<string>();
        w.Meldung += m => { if (m.StartsWith(ki.Inhaber + ": heuert im Hinterzimmer")) auftraege.Add(m); };
        for (var i = 0; i < Spielzeit.MinutenProTag * tage; i++) sim.Tick();
        return auftraege;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Hausgauner: Die Ausgewogene bricht ins Steinhaus des reicheren Hauses ein – Bargeld ohne Hehler", () =>
        {
            var (sim, w, ziel, ki) = Partie(zielStufe: 2, gaunerei: 1);
            Pruefe.Wahr(w.Gauner.ErwarteterHausgewinn(ziel, ki) > 0, "Der Einbruch lohnt sich");
            var auftraege = Auftraege(sim, w, ki);
            Pruefe.Gleich(1, auftraege.Count);
            Pruefe.Wahr(auftraege[0].Contains("Einbruch ins Wohnhaus gegen Haus Reich"), auftraege[0]);
        }),

        ("KI-Hausgauner: Beim Fachwerkhaus lohnt kein Einbruch – höchstens 300 Beute, die Bande kostet 200", () =>
        {
            var (sim, w, ziel, ki) = Partie(zielStufe: 0, gaunerei: 1);
            Pruefe.Wahr(w.Gauner.ErwarteterHausgewinn(ziel, ki) <= 0, "Der Einbruch lohnt sich nicht");
            Pruefe.Gleich(0, Auftraege(sim, w, ki).Count);
        }),

        ("KI-Hausgauner: Die Ausgewogene legt nie Feuer – auch nicht am Stadthof", () =>
        {
            var (sim, w, _, ki) = Partie(zielStufe: 4, gaunerei: 1);
            var auftraege = Auftraege(sim, w, ki);
            Pruefe.Wahr(auftraege.All(a => !a.Contains("Brandstiftung")), string.Join(" | ", auftraege));
            Pruefe.Wahr(auftraege.Any(a => a.Contains("Einbruch ins Wohnhaus")), "Statt Feuer ein Einbruch");
        }),

        ("KI-Hausgauner: Die Wagemutige zündet den Stadthof an – die halbe Reparatur wiegt mehr als der Einbruch", () =>
        {
            var (sim, w, ziel, ki) = Partie(zielStufe: 4, gaunerei: 2);
            var u = w.Gauner;
            var brand = u.ErfolgschanceWohnhaus(Gaunerstueck.Brandstiftung, ziel, ki) * u.Brandschaden(ziel) * 0.5 - u.Kosten(Gaunerstueck.Brandstiftung, ki);
            Pruefe.Wahr(brand > u.ErwarteterHausgewinn(ziel, ki), $"Brand {brand:0} sollte mehr bringen als der Einbruch {u.ErwarteterHausgewinn(ziel, ki):0}");
            var auftraege = Auftraege(sim, w, ki);
            Pruefe.Wahr(auftraege.Count >= 1, "Die Wagemutige heuert eine Bande an");
            Pruefe.Wahr(auftraege[0].Contains("Brandstiftung gegen Haus Reich"), auftraege[0]);
        }),

        ("KI-Hausgauner: Steht der Stadthof noch vom Brand beschädigt, bricht die Wagemutige im Jahr darauf ein", () =>
        {
            var (sim, w, ziel, ki) = Partie(zielStufe: 4, gaunerei: 2, BrandSicher.Value);
            var auftraege = Auftraege(sim, w, ki, tage: 2);
            Pruefe.Gleich(2, auftraege.Count);
            Pruefe.Wahr(auftraege[0].Contains("Brandstiftung"), auftraege[0]);
            Pruefe.Wahr(auftraege[1].Contains("Einbruch ins Wohnhaus"), auftraege[1]);
        }),
    };
}
