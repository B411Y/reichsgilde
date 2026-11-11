using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// KI nach der Personal-Nacharbeit (Datenversion 79, <c>werkstatt.jedeWirkungRechnen</c>): Mit der Gulden-Rechnung für Fänge und Spuren kauft ein
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

    /// <summary>Ein KI-Haus mit einem Schmugglerloch und vier Taschendieben, ohne Karren; auf Wunsch ein fremdes Wachhaus in der Stadt (Razzien).</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb B, Reichsgilde.Core.Ki.KiRivale Ki) Dieb(Wirtschaftsdaten daten, bool wache = true)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten);
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Elster", startvermoegen: 10_000);
        var b = w.BetriebEroeffnen("Schmugglerloch Elster", Betriebsart.Schmugglerloch, k, startkarren: "");
        w.Einstellen(b, "Taschendieb", 4);
        if (wache)
            w.Einstellen(w.BetriebEroeffnen("Wachhaus Wacker", Betriebsart.Wachhaus, w.KontoEroeffnen("Haus Wacker", startvermoegen: 10_000), startkarren: ""), "Rekrut", 4);
        return (w, k, b, w.KiAnmelden(k));
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Personal-Nacharbeit: Ein Dieb kauft zum Jahresende die Lederhandschuhe, wenn eine fremde Wache Razzien macht – allein in der Stadt oder ohne den Schalter kein Stück", () =>
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

            var (_, _, b3, ki3) = Dieb(Daten.Value, wache: false);
            ki3.Jahresende();
            Pruefe.Wahr(!b3.HatEinrichtung("Handschuhe"), "Ohne fremde Wache sucht niemand nach Spuren – den Beweis eines Fangs hat der Fänger");
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

        ("KI-Personal-Nacharbeit: Gebremstes Umland – „Ordnungsliebend“ gilt der KI im Kölner Holzschlag mit und ohne den Schalter gleich viel (Lager nicht doppelt); der Kern rechnet es nur mit Schalter", () =>
        {
            (Stadtwirtschaft W, Reichsgilde.Core.Ki.KiRivale Ki, Betrieb B) Aufbau(Wirtschaftsdaten daten)
            {
                var pfad = Datenpfad.Karte("koeln");
                var haeuser = JsonNode.Parse(File.ReadAllText(pfad))!["start"]!["haeuser"]!.AsArray().Count;
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400, Schwierigkeit = Schwierigkeit.Schwer },
                    daten, new NeuePartie(Rivalen: haeuser, Aufstellung: Aufstellungsart.Vorgabe), karte: Reichsgilde.Core.Bauen.Stadtkarte.AusDatei(pfad));
                var w = sim.Wirtschaft!;
                var b = w.Betriebe.Single(x => x.Name == "Holzschlag Hardevust"); // nur Holz, Lager 40
                b.KarrenHinzufuegen("Pferdekarren"); // lädt 75, das Lager gibt 40 her – mehr Lager lädt mehr
                typeof(Betrieb).GetProperty(nameof(Betrieb.StillstandMinutenVorjahr))!.SetValue(b, b.Arbeitsminuten * 3 / 10);
                return (w, w.Rivalen.Single(r => r.Konto == b.Konto), b);
            }
            var (w1, ki1, b1) = Aufbau(Daten.Value);
            var (w2, ki2, b2) = Aufbau(OhneSchalter.Value);
            Pruefe.Gleich(b1.Name, b2.Name);
            Pruefe.Wahr(ki1.KarrenwahlNachDurchfluss(b1), "Fahrzeit nach Weglänge");
            Pruefe.Wahr(w1.Personal.MehrgewinnJeJahr(b1, "Ordnungsliebend") > 0, "Kern mit Schalter");
            Pruefe.Gleich(0.0, w2.Personal.MehrgewinnJeJahr(b2, "Ordnungsliebend"), "Kern ohne Schalter");
            var mit = ki1.EigenschaftWert(b1, "Ordnungsliebend");
            Pruefe.Wahr(mit > 0, $"KI {mit}");
            Pruefe.Wahr(Math.Abs(mit - ki2.EigenschaftWert(b2, "Ordnungsliebend")) < 1e-9, $"mit {mit}, ohne {ki2.EigenschaftWert(b2, "Ordnungsliebend")}");
            foreach (var k in new[] { "Kraeftig", "GutesAuge", "Antreiber" })
                Pruefe.Wahr(Math.Abs(ki1.EigenschaftWert(b1, k) - ki2.EigenschaftWert(b2, k)) < 1e-9, k);
        }),
    };
}
