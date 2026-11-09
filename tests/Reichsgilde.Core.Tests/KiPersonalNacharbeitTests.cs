using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// KI nach der Personal-Nacharbeit (Datenversion 78, <c>werkstatt.jedeWirkungRechnen</c>): Mit der Gulden-Rechnung für Fänge und Spuren kauft ein
/// KI-geführter Dieb Einrichtung (vorher keine) und wählt Vorarbeiter-Eigenschaften nach dem Nutzen; ohne den Schalter wie bisher.
/// </summary>
static class KiPersonalNacharbeitTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    static readonly Lazy<Wirtschaftsdaten> OhneSchalter = new(() =>
    {
        var knoten = JsonNode.Parse(Json.Value)!;
        knoten["werkstatt"]!.AsObject().Remove("jedeWirkungRechnen");
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    });

    /// <summary>Ein KI-Haus mit einem Schmugglerloch und vier Taschendieben, ohne Karren.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb B, Reichsgilde.Core.Ki.KiRivale Ki) Dieb(Wirtschaftsdaten daten)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten);
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Elster", startvermoegen: 10_000);
        var b = w.BetriebEroeffnen("Schmugglerloch Elster", Betriebsart.Schmugglerloch, k, startkarren: "");
        w.Einstellen(b, "Taschendieb", 4);
        return (w, k, b, w.KiAnmelden(k));
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Personal-Nacharbeit: Ein Dieb kauft zum Jahresende die Lederhandschuhe (weniger Buße für die Fänge) – ohne den Schalter kein Stück", () =>
        {
            var (w, k, b, ki) = Dieb(Daten.Value);
            var jahre = w.Werkstaetten.Amortisationsjahre(b, "Handschuhe");
            Pruefe.Wahr(jahre is < 4, $"Handschuhe in {jahre} Jahren bezahlt");
            var vorher = k.Vermoegen;
            ki.Jahresende();
            Pruefe.Wahr(b.HatEinrichtung("Handschuhe"), "Die KI hat keine Handschuhe gekauft: " + string.Join(" | ", ki.Entscheidungen.Take(3)));
            Pruefe.Gleich(1, b.Einrichtungsstuecke.Count(), "ein Stück je Betrieb und Jahr");
            Pruefe.Ungefaehr(vorher - 80, k.Vermoegen, 1e-6);

            var (_, _, b2, ki2) = Dieb(OhneSchalter.Value);
            ki2.Jahresende();
            Pruefe.Gleich(0, b2.Einrichtungsstuecke.Count(), "ohne den Schalter bringt keins Gulden");
        }),

        ("KI-Personal-Nacharbeit: Der Vorarbeiter eines Diebes ohne Einbrüche ist eher vorsichtig als Hehlerfreund oder Einbrecher; ohne den Schalter alle 0", () =>
        {
            var (w, _, b, ki) = Dieb(Daten.Value);
            Pruefe.Wahr(ki.EigenschaftWert(b, "Vorsichtig") > 0, "Vorsichtig bringt Gulden");
            Pruefe.Wahr(ki.EigenschaftWert(b, "Verschwiegen") > 0, "Verschwiegen bringt Gulden");
            Pruefe.Gleich(0.0, ki.EigenschaftWert(b, "Hehlerfreund"), "kein Diebesgut im Vorjahr");
            Pruefe.Gleich(0.0, ki.EigenschaftWert(b, "Einbrecher"), "keine Einbrüche im Vorjahr");
            Pruefe.Gleich(w.Personal.MehrgewinnJeJahr(b, "Vorsichtig"), ki.EigenschaftWert(b, "Vorsichtig"), "die Rechnung des Kerns");

            var (_, _, b2, ki2) = Dieb(OhneSchalter.Value);
            foreach (var kennung in new[] { "Vorsichtig", "Verschwiegen", "Hehlerfreund", "Einbrecher" })
                Pruefe.Gleich(0.0, ki2.EigenschaftWert(b2, kennung), kennung);
        }),
    };
}
