using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Tests;

/// <summary>
/// Raumwahl im Wohnhaus ohne Festsaal (Nutzerentscheidung 14.11.2026, Datenversion 88): Bürgerhaus wählt Hinterzimmer oder Bibliothek, das
/// Steinhaus bekommt den anderen. Den Festsaal gibt es erst mit der Stadtvilla („erstMit“) – nicht anbietbar, Befehl abgelehnt, die KI wählt ihn
/// nie, und das Gastmahl (an den Festsaal gebunden) geht bis dahin nicht. Alte Spielstände (Daten 87) mit Festsaal: beim Laden zurückgebaut,
/// Platz frei, Preis aus den Daten zurück, einmalig, mit Meldung; ohne Festsaal ändert sich an den Häusern nichts.
/// </summary>
static class WohnhausOhneFestsaalTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));
    static readonly DateTime Zeitpunkt = new(2026, 11, 14, 12, 0, 0);

    /// <summary>Die Daten wie in Datenversion 87: Festsaal zur Wahl im Wohnhaus, ohne „erstMit“.</summary>
    static string AlteDatenJson()
    {
        var json = Json.Value;
        foreach (var (neu, alt) in new[]
                 {
                     ("\"version\": 99,", "\"version\": 87,"),
                     (", \"erstMit\": \"Stadtvilla\"", ""),
                     ("\"waehlbar\": [\"Hinterzimmer\", \"Bibliothek\"]", "\"waehlbar\": [\"Hinterzimmer\", \"Bibliothek\", \"Festsaal\"]"),
                 })
        {
            Pruefe.Wahr(json.Contains(neu), $"In wirtschaft.json fehlt: {neu}");
            json = json.Replace(neu, alt);
        }
        return json;
    }

    static Simulation NeueSimulation(Wirtschaftsdaten daten) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten);

    static Konto Haus(Stadtwirtschaft w, string name, int stufe, double geld = 50_000)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: geld);
        k.WohnhausStufe = stufe;
        k.Aktionspunkte.Setzen(12);
        return k;
    }

    static string Liste(IEnumerable<string> x) => string.Join(",", x);

    static int Migrationsmeldungen(Stadtwirtschaft w, Konto k) =>
        w.Meldungsverlauf.Count(m => m.Text.StartsWith("Den Festsaal gibt es im Wohnhaus nicht mehr") && m.Text.Contains(k.Inhaber));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Raumwahl ohne Festsaal: Bürgerhaus Hinterzimmer → Steinhaus Bibliothek, und umgekehrt; der Festsaal steht in keiner Stufe zur Wahl", () =>
        {
            var w = NeueSimulation(Daten.Value).Wirtschaft!;
            foreach (var (erst, dann) in new[] { ("Hinterzimmer", "Bibliothek"), ("Bibliothek", "Hinterzimmer") })
            {
                var k = Haus(w, $"Haus {erst}", 1);
                Pruefe.Gleich("Hinterzimmer,Bibliothek", Liste(w.Wohnen.WaehlbareRaeume(k)), erst);
                Pruefe.Wahr(w.Ausfuehren(k, new RaumAnbauen(erst)).Erfolg, erst);
                Pruefe.Gleich(0, w.Wohnen.WaehlbareRaeume(k).Count, $"{erst}: Bürgerhaus voll");
                k.WohnhausStufe = 2; // Steinhaus: der andere Raum
                Pruefe.Gleich(dann, Liste(w.Wohnen.WaehlbareRaeume(k)), erst);
                Pruefe.Wahr(w.Ausfuehren(k, new RaumAnbauen(dann)).Erfolg, dann);
                Pruefe.Gleich($"Wohnstube,{erst},{dann}", Liste(w.Wohnen.Raeume(k)));
                Pruefe.Gleich(0, w.Wohnen.WaehlbareRaeume(k).Count, $"{erst}: Steinhaus voll");
            }
            for (var stufe = 0; stufe <= 4; stufe++)
                Pruefe.Wahr(!w.Wohnen.WaehlbareRaeume(Haus(w, $"Haus Stufe {stufe}", stufe)).Contains("Festsaal"), $"Stufe {stufe}");
        }),

        ("Raumwahl ohne Festsaal: Der Befehl Festsaal wird in jeder Stufe abgelehnt (erst mit der Stadtvilla) und kostet nichts", () =>
        {
            var w = NeueSimulation(Daten.Value).Wirtschaft!;
            for (var stufe = 0; stufe <= 4; stufe++)
            {
                var k = Haus(w, $"Haus {stufe}", stufe);
                var vorher = k.Vermoegen;
                var nein = w.Ausfuehren(k, new RaumAnbauen("Festsaal"));
                Pruefe.Wahr(!nein.Erfolg && nein.Meldung == "Einen Festsaal gibt es im Wohnhaus nicht – erst mit der Stadtvilla.", nein.Meldung);
                Pruefe.Gleich(vorher, k.Vermoegen);
                Pruefe.Wahr(!w.Wohnen.HatRaum(k, "Festsaal"), $"Stufe {stufe}");
            }
            // Gegenprobe: Die Wahl im Bürgerhaus geht weiter.
            Pruefe.Wahr(w.Ausfuehren(Haus(w, "Haus Probe", 1), new RaumAnbauen("Bibliothek")).Erfolg, "Bibliothek");
            // Daten: Ein Raum mit „erstMit“ darf weder zur Wahl noch fest im Wohnhaus stehen.
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace(
                "\"waehlbar\": [\"Hinterzimmer\", \"Bibliothek\"]", "\"waehlbar\": [\"Hinterzimmer\", \"Bibliothek\", \"Festsaal\"]")), "erst mit Stadtvilla");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace(
                "{ \"feste\": [\"Wohnstube\"], \"frei\": 2 }", "{ \"feste\": [\"Wohnstube\", \"Festsaal\"], \"frei\": 2 }")), "erst mit Stadtvilla");
        }),

        ("Raumwahl ohne Festsaal: Die KI wählt nie den Festsaal – auch nicht, wenn sie Ratsstimmen wirbt; Greybox-Partie drei Jahre ohne Festsaal und Gastmahl", () =>
        {
            foreach (var profil in new[] { new KiProfil(), new KiProfil(Gaunerei: 0), new KiProfil(Gaunerei: 1), new KiProfil(Gaunerei: 0, PolitikAnteil: 1) })
                for (var stufe = 1; stufe <= 4; stufe++)
                {
                    var w = NeueSimulation(Daten.Value).Wirtschaft!;
                    var k = Haus(w, "Haus Wahl", stufe, 100_000);
                    var ki = w.KiAnmelden(k, profil);
                    ki.Jahresende();
                    ki.Jahresende();
                    Pruefe.Wahr(!k.WohnhausRaeume.Contains("Festsaal"), $"{profil} Stufe {stufe}");
                    Pruefe.Gleich(Math.Min(stufe, 2), k.WohnhausRaeume.Count, $"{profil} Stufe {stufe}: freie Plätze belegt");
                }

            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value,
                new NeuePartie(), karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
            var gw = sim.Wirtschaft!;
            foreach (var r in gw.Rivalen) { r.Konto.WohnhausStufe = 2; r.Konto.Verkauf(50_000); }
            var gastmahl = 0;
            gw.Meldung += m => { if (m.Contains("Gastmahl")) gastmahl++; };
            for (var i = 0; i < 3 * Spielzeit.MinutenProTag; i++) sim.Tick();
            foreach (var r in gw.Rivalen)
            {
                Pruefe.Wahr(!r.Konto.WohnhausRaeume.Contains("Festsaal"), r.Konto.Inhaber);
                Pruefe.Wahr(!r.Entscheidungen.Any(e => e.Contains("Gastmahl") || e.Contains("Festsaal")), r.Konto.Inhaber);
            }
            Pruefe.Gleich(0, gastmahl);
            // Gegenprobe: Die KI hat in der Zeit Räume gewählt – nur eben nie den Festsaal.
            Pruefe.Wahr(gw.Rivalen.Any(r => r.Konto.WohnhausRaeume.Count > 0), "KI wählt Räume");
        }),

        ("Raumwahl ohne Festsaal: Alter Spielstand (Daten 87) mit Festsaal – Raum weg, Platz frei, 700 Gulden zurück, Meldung; nur einmal", () =>
        {
            var alteDaten = Wirtschaftsdaten.AusJson(AlteDatenJson());
            Pruefe.Wahr(alteDaten.Wohnen!.Raeume!.Waehlbar.Contains("Festsaal"), "alte Daten mit Festsaal zur Wahl");
            var sim = NeueSimulation(alteDaten);
            var w = sim.Wirtschaft!;
            var buerger = Haus(w, "Haus Saal", 1);
            var stein = Haus(w, "Haus Stein", 2);
            var ohne = Haus(w, "Haus Ohne", 2);
            Pruefe.Wahr(w.Ausfuehren(buerger, new RaumAnbauen("Festsaal")).Erfolg, "alter Anbau Bürgerhaus");
            Pruefe.Wahr(w.Ausfuehren(stein, new RaumAnbauen("Festsaal")).Erfolg, "alter Anbau Steinhaus");
            Pruefe.Wahr(w.Ausfuehren(stein, new RaumAnbauen("Hinterzimmer")).Erfolg, "Hinterzimmer");
            Pruefe.Wahr(w.Ausfuehren(ohne, new RaumAnbauen("Bibliothek")).Erfolg, "Bibliothek");
            var json = Speicherung.Speichern(sim, null, Zeitpunkt);
            Pruefe.Wahr(json.Contains("\"Festsaal\""), "Festsaal im Spielstand");

            var g = Speicherung.Laden(json).Simulation.Wirtschaft!;
            Konto K(Stadtwirtschaft x, string name) => x.Konten.Single(k => k.Inhaber == name);
            // Bürgerhaus: Festsaal weg, Platz frei, Hinterzimmer oder Bibliothek wieder zur Wahl, 700 Gulden zurück.
            Pruefe.Gleich("Wohnstube", Liste(g.Wohnen.Raeume(K(g, "Haus Saal"))));
            Pruefe.Gleich(1, g.Wohnen.FreiePlaetze(K(g, "Haus Saal")));
            Pruefe.Gleich("Hinterzimmer,Bibliothek", Liste(g.Wohnen.WaehlbareRaeume(K(g, "Haus Saal"))));
            Pruefe.Ungefaehr(buerger.Vermoegen + 700, K(g, "Haus Saal").Vermoegen, 1e-9);
            // Steinhaus: das Hinterzimmer bleibt, der Platz des Festsaals ist frei – jetzt für die Bibliothek.
            Pruefe.Gleich("Wohnstube,Hinterzimmer", Liste(g.Wohnen.Raeume(K(g, "Haus Stein"))));
            Pruefe.Gleich("Bibliothek", Liste(g.Wohnen.WaehlbareRaeume(K(g, "Haus Stein"))));
            Pruefe.Ungefaehr(stein.Vermoegen + 700, K(g, "Haus Stein").Vermoegen, 1e-9);
            // Gegenprobe: das Haus ohne Festsaal bleibt, wie es war.
            Pruefe.Gleich("Wohnstube,Bibliothek", Liste(g.Wohnen.Raeume(K(g, "Haus Ohne"))));
            Pruefe.Gleich(ohne.Vermoegen, K(g, "Haus Ohne").Vermoegen);
            Pruefe.Gleich(0, Migrationsmeldungen(g, K(g, "Haus Ohne")));
            Pruefe.Gleich(1, Migrationsmeldungen(g, K(g, "Haus Saal")));
            Pruefe.Gleich(1, Migrationsmeldungen(g, K(g, "Haus Stein")));
            Pruefe.Wahr(g.Meldungsverlauf.Any(m => m.Text == "Den Festsaal gibt es im Wohnhaus nicht mehr – erst mit der Stadtvilla. Haus Saal bekommt 700 Gulden zurück, der Platz ist wieder frei."),
                "Wortlaut der Meldung");
            // Die Daten des Spielstands sind umgestellt: kein Festsaal mehr zur Wahl, der Befehl wird abgelehnt.
            Pruefe.Gleich("Hinterzimmer,Bibliothek", Liste(g.Wohnen.Daten!.Raeume!.Waehlbar));
            Pruefe.Wahr(!g.Ausfuehren(K(g, "Haus Saal"), new RaumAnbauen("Festsaal")).Erfolg, "nach dem Laden kein Festsaal");

            // Nur einmal: Speichern und wieder laden ändert nichts mehr – Byte für Byte.
            var json2 = Speicherung.Speichern(Speicherung.Laden(json).Simulation, null, Zeitpunkt);
            var (sim3, _) = Speicherung.Laden(json2);
            var g3 = sim3.Wirtschaft!;
            Pruefe.Gleich(K(Speicherung.Laden(json).Simulation.Wirtschaft!, "Haus Saal").Vermoegen, K(g3, "Haus Saal").Vermoegen);
            Pruefe.Gleich(1, Migrationsmeldungen(g3, K(g3, "Haus Saal")));
            Pruefe.Gleich(json2, Speicherung.Speichern(sim3, null, Zeitpunkt));
        }),

        ("Raumwahl ohne Festsaal: Gegenprobe – Spielstände ohne Festsaal bleiben bitgleich (Daten 88; alte Daten 87 ohne Festsaal: Häuser unverändert)", () =>
        {
            var sim = NeueSimulation(Daten.Value);
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(w.Ausfuehren(Haus(w, "Haus Eins", 1), new RaumAnbauen("Hinterzimmer")).Erfolg, "Hinterzimmer");
            var zwei = Haus(w, "Haus Zwei", 2);
            Pruefe.Wahr(w.Ausfuehren(zwei, new RaumAnbauen("Bibliothek")).Erfolg, "Bibliothek");
            Pruefe.Wahr(w.Ausfuehren(zwei, new RaumAnbauen("Hinterzimmer")).Erfolg, "Hinterzimmer");
            for (var i = 0; i < 300; i++) sim.Tick();
            var json = Speicherung.Speichern(sim, null, Zeitpunkt);
            Pruefe.Gleich(json, Speicherung.Speichern(Speicherung.Laden(json).Simulation, null, Zeitpunkt));

            // Alte Daten 87, aber niemand hat einen Festsaal: nur die Wahl in den Daten ändert sich, an den Häusern nichts.
            var alt = NeueSimulation(Wirtschaftsdaten.AusJson(AlteDatenJson()));
            var a = alt.Wirtschaft!;
            var k = Haus(a, "Haus Alt", 1);
            Pruefe.Wahr(a.Ausfuehren(k, new RaumAnbauen("Bibliothek")).Erfolg, "Bibliothek");
            var altJson = Speicherung.Speichern(alt, null, Zeitpunkt);
            var ga = Speicherung.Laden(altJson).Simulation.Wirtschaft!;
            var gk = ga.Konten.Single(x => x.Inhaber == "Haus Alt");
            Pruefe.Gleich(k.Vermoegen, gk.Vermoegen);
            Pruefe.Gleich("Wohnstube,Bibliothek", Liste(ga.Wohnen.Raeume(gk)));
            Pruefe.Gleich(a.Meldungsverlauf.Count, ga.Meldungsverlauf.Count);
            Pruefe.Gleich("Hinterzimmer,Bibliothek", Liste(ga.Wohnen.Daten!.Raeume!.Waehlbar));
        }),
    };
}
