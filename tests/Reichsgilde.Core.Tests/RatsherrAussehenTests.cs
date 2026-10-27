using System.Text.RegularExpressions;
using Reichsgilde.Core;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Geschlecht und Aussehen der Ratsherren in den Daten (PR #219, Datenversion 58): <see cref="Ratsherr.Geschlecht"/> und
/// <see cref="Ratsherr.Aussehen"/> fürs Porträt und die Bürger-KI. Ohne Angabe (alte Daten) gilt die frühere Ableitung aus dem Namen.
/// </summary>
static class RatsherrAussehenTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));

    static Stadtwirtschaft Stadt(string json) =>
        new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Wirtschaftsdaten.AusJson(json)).Wirtschaft!;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Ratsherren: Geschlecht und Aussehen kommen aus den Daten – jeder Ratsherr hat beides, passend zu seinem Volk", () =>
        {
            var d = Wirtschaftsdaten.AusJson(Json.Value);
            Pruefe.Wahr(d.Politik!.Ratsherren.All(r => r.Geschlecht is not null && r.Aussehen is not null), "alle mit Geschlecht und Aussehen");
            var w = Stadt(Json.Value);
            var aelindra = w.Rat.Ratsherren.Single(r => r.Name == "Aelindra vom Silberhain");
            Pruefe.Gleich(Geschlecht.Frau, aelindra.Geschlecht);
            Pruefe.Gleich(new Aussehen(2, Haarfarbe.Hellblond, Augenfarbe.Gruen), aelindra.Aussehen);
            Pruefe.Gleich(Geschlecht.Mann, w.Rat.Ratsherren.Single(r => r.Name == "Ulrich Wendelstein").Geschlecht);
            Pruefe.Gleich("Aelindra vom Silberhain, Dorla Kupferbart, Margarethe Lindwurm, Rosalind Apfelgrund",
                string.Join(", ", w.Rat.Ratsherren.Where(r => r.Geschlecht == Geschlecht.Frau).Select(r => r.Name)));
            foreach (var r in w.Rat.Ratsherren)
                Pruefe.Gleich<string?>(null, Aussehenspalette.Verboten(r.Daten.Volk, r.Aussehen), r.Name);
        }),

        ("Ratsherren: Ohne Angabe (alte Daten) leitet der Kern Geschlecht und Aussehen wie bisher das Porträt aus dem Namen ab – dasselbe Bild", () =>
        {
            var ohne = Regex.Replace(Json.Value, ",\\s*\"geschlecht\": \"(Mann|Frau)\",\\s*\"aussehen\": \\{[^}]*\\}", "");
            var alt = Wirtschaftsdaten.AusJson(ohne);
            Pruefe.Wahr(alt.Politik!.Ratsherren.All(r => r.Geschlecht is null && r.Aussehen is null), "die Angaben sind entfernt");
            var neu = Stadt(Json.Value).Rat.Ratsherren;
            var abgeleitet = Stadt(ohne).Rat.Ratsherren;
            Pruefe.Gleich(neu.Count, abgeleitet.Count);
            for (var i = 0; i < neu.Count; i++)
            {
                Pruefe.Gleich(neu[i].Geschlecht, abgeleitet[i].Geschlecht, neu[i].Name);
                Pruefe.Gleich(neu[i].Aussehen, abgeleitet[i].Aussehen, neu[i].Name); // die Daten halten das bisherige Bild fest
            }
        }),

        ("Ratsherren: Ein Aussehen, das nicht zum Volk passt, fällt beim Laden der Daten auf", () =>
        {
            var falsch = Json.Value.Replace("\"aussehen\": { \"gesicht\": 2, \"haar\": \"Hellblond\", \"augen\": \"Gruen\" }",
                                            "\"aussehen\": { \"gesicht\": 2, \"haar\": \"Rot\", \"augen\": \"Gruen\" }");
            Pruefe.Wahr(falsch != Json.Value, "Aelindras Aussehen ersetzt");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(falsch), "Aelindra vom Silberhain");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"gesicht\": 4,", "\"gesicht\": 9,")), "Gesicht 9");
        }),

        ("Ratsherren: Die Bürger-KI nimmt das Geschlecht aus den Daten – Ratsherrinnen gehen als Frauen durch die Stadt", () =>
        {
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                Wirtschaftsdaten.AusJson(Json.Value), new NeuePartie());
            sim.Tick(); // mit dem ersten Takt ziehen die Einwohner ein
            var w = sim.Wirtschaft!;
            var ratsherren = w.Einwohnerschaft.Einwohner.Where(x => x.Herkunft == Herkunft.Ratsherr).ToList();
            Pruefe.Gleich(w.Rat.Ratsherren.Count, ratsherren.Count);
            foreach (var r in w.Rat.Ratsherren)
                Pruefe.Gleich(r.Geschlecht, ratsherren.Single(x => x.Quelle == r.Name).Geschlecht, r.Name);
        }),
    };
}
