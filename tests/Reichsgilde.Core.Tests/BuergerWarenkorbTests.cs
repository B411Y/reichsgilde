using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests: Warenkorb je Volk und Rolle – wer einkauft, verschiebt die Nachfrage zwischen den Warengruppen.</summary>
static class BuergerWarenkorbTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Wirtschaftsdaten Abgewandelt(Action<JsonObject> aendern)
    {
        var json = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()), documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!.AsObject();
        aendern(json);
        return Wirtschaftsdaten.AusJson(json.ToJsonString());
    }

    static readonly Lazy<Wirtschaftsdaten> OhneWarenkorb = new(() => Abgewandelt(j => j["buerger"]!.AsObject().Remove("warenkorb")));

    /// <summary>Nur Zwerge in der Stadt (und als Ratsherren/Kandidaten zählen sie ohnehin nach ihren Häusern).</summary>
    static readonly Lazy<Wirtschaftsdaten> NurZwerge = new(() => Abgewandelt(j => j["buerger"]!["voelker"] = new JsonObject { ["Zwerg"] = 1 }));

    static (Simulation Sim, Konto Spieler) Partie(Wirtschaftsdaten? daten = null) =>
        Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
            daten ?? Daten.Value, new NeuePartie(Rivale: KiCharakter.Ausgewogen));

    static void Ticken(Simulation sim, long minuten)
    {
        for (var i = 0; i < minuten; i++) sim.Tick();
    }

    static Einwohnerschaft Ew(Simulation sim) => sim.Wirtschaft!.Einwohnerschaft;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Warenkorb: Ohne Abschnitt bleibt jeder Faktor 1 – die Nachfrage folgt nur dem Marktfaktor", () =>
        {
            var (sim, _) = Partie(OhneWarenkorb.Value);
            Ticken(sim, 2 * Spielzeit.MinutenProTag + 300);
            var ew = Ew(sim);
            foreach (var stand in sim.Wirtschaft!.Markt.Staende)
            {
                Pruefe.Gleich(1.0, ew.Warenkorbfaktor(stand.Ware), stand.Ware);
                if (ew.Daten.Kopplung.MarktTypen.Contains(stand.Typ)) Pruefe.Gleich(ew.Marktfaktor, stand.NachfrageFaktor, stand.Ware);
            }
            Pruefe.Wahr(ew.Warengruppe("Silberring") is null, "Ohne Warenkorb keine Gruppe");
        }),

        ("Warenkorb: Faktoren in 1 ± spanne, die Nachfrage der Stadt in Gulden bleibt gleich", () =>
        {
            var (sim, _) = Partie();
            Ticken(sim, 2 * Spielzeit.MinutenProTag + 300);
            var ew = Ew(sim);
            var wk = ew.Daten.Warenkorb!;
            var waren = sim.Wirtschaft!.Daten.Waren.Where(x => ew.Daten.Kopplung.MarktTypen.Contains(x.Value.Typ)).ToList();
            var faktoren = waren.Select(x => ew.Warenkorbfaktor(x.Key)).ToList();
            Pruefe.Wahr(faktoren.All(f => f >= 1 - wk.Spanne - 1e-9 && f <= 1 + wk.Spanne + 1e-9), "Faktor außerhalb der Spanne");
            Pruefe.Wahr(faktoren.Distinct().Count() > 3, "Der Warenkorb verschiebt etwas");
            var wert = waren.Sum(x => x.Value.Nachfrage * x.Value.Grundpreis);
            var gewichtet = waren.Sum(x => x.Value.Nachfrage * x.Value.Grundpreis * ew.Warenkorbfaktor(x.Key)) / wert;
            Pruefe.Ungefaehr(1.0, gewichtet, 1e-6);
            var stand = sim.Wirtschaft.Markt["Silberring"];
            Pruefe.Ungefaehr(ew.Marktfaktor * ew.Warenkorbfaktor("Silberring"), stand.NachfrageFaktor, 1e-12);
        }),

        ("Warenkorb: Die Rolle wirkt auf die Preisklasse – günstige Schmiedewaren vor teuren", () =>
        {
            var (sim, _) = Partie();
            Ticken(sim, 2 * Spielzeit.MinutenProTag + 300);
            var ew = Ew(sim);
            Pruefe.Wahr(ew.Warenkorbfaktor("Silberring") > ew.Warenkorbfaktor("Goldkette"),
                $"Silberring {ew.Warenkorbfaktor("Silberring"):0.000}, Goldkette {ew.Warenkorbfaktor("Goldkette"):0.000}");
        }),

        ("Warenkorb: Das Volk wirkt auf die Gruppe – in einer Zwergenstadt Schmiedewaren vor Holzwaren", () =>
        {
            var (sim, _) = Partie(NurZwerge.Value);
            Ticken(sim, 2 * Spielzeit.MinutenProTag + 300);
            var ew = Ew(sim);
            // beide günstig: Silberring (Schmied) und Schemel (Tischler)
            Pruefe.Wahr(ew.Warenkorbfaktor("Silberring") > ew.Warenkorbfaktor("Schemel") + 0.05,
                $"Silberring {ew.Warenkorbfaktor("Silberring"):0.000}, Schemel {ew.Warenkorbfaktor("Schemel"):0.000}");
        }),

        ("Warenkorb: Gruppen tragen den Namen aus den Daten, Rohstoffe liegen in keinem Korb", () =>
        {
            var (sim, _) = Partie();
            var ew = Ew(sim);
            Pruefe.Gleich("Schmiedewaren", ew.Warengruppe("Silberring"));
            Pruefe.Gleich("Arzneien", ew.Warengruppe("Kräuterschnaps"));
            Pruefe.Wahr(ew.Warengruppe("Eisen") is null, "Eisen ist ein Rohstoff");
        }),

        ("Warenkorb: Speichern und Laden – gleiche Faktoren, danach gleicher Verlauf", () =>
        {
            var (sim, spieler) = Partie();
            Ticken(sim, Spielzeit.MinutenProTag + 400);
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            string Faktoren(Simulation s) => string.Join("|", s.Wirtschaft!.Markt.Staende.Select(x => $"{x.Ware}={Ew(s).Warenkorbfaktor(x.Ware):R}/{x.NachfrageFaktor:R}"));
            Pruefe.Gleich(Faktoren(sim), Faktoren(geladen));
            Ticken(sim, 300);
            Ticken(geladen, 300);
            Pruefe.Gleich(Faktoren(sim), Faktoren(geladen));
        }),
    };
}
