using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für das Erwerben von Werkstätten und Gruben (Expansion).</summary>
static class LiegenschaftTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);

    /// <summary>Ein Konto, das schon Bürger ist und das gewünschte Vermögen hat.</summary>
    static Konto Buerger(Stadtwirtschaft w, string name, double vermoegen)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 10000);
        Pruefe.Wahr(w.Ausfuehren(Wohnhaushilfe.MitWohnhaus(w, k), new StandErwerben()).Erfolg, "Aufstieg zum Bürger fehlgeschlagen");
        if (vermoegen > k.Vermoegen) k.Umsatz(vermoegen - k.Vermoegen); else k.Schaden(k.Vermoegen - vermoegen);
        return k;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Liegenschaften: neun sind frei – nur Umland (je Beruf außer Dieb und Geldleiher, dazu eine Grube), erst ab Bürger zu haben", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            Pruefe.Gleich(9, w.FreieLiegenschaften.Count());
            Pruefe.Wahr(w.FreieLiegenschaften.All(l => l.IstPacht && !w.Daten.Art(l.Art).InDerStadt), "nur Umland");
            var gemeiner = w.KontoEroeffnen("Gemeiner", startvermoegen: 50000);
            var e = w.Ausfuehren(gemeiner, new LiegenschaftErwerben("Grube am Silberberg"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("erst ab dem Stand Bürger"), e.Meldung);
        }),

        ("Liegenschaften: Werkstätten in der Stadt gibt es nicht mehr zu kaufen – neue entstehen durch Bauen, freie durch Verkauf", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = Buerger(w, "Spieler", 20000);
            var e = w.Ausfuehren(k, new LiegenschaftErwerben("Gießerei an der Südgasse"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("gibt es nicht"), e.Meldung);
            Pruefe.Wahr(w.Daten.Liegenschaften.All(l => !w.Daten.Art(l.Art).InDerStadt), "keine Werkstatt unter „liegenschaften“");
            // Der Preis der Stufe 1 steht nun bei der Betriebsart: Neubau und Wert der Startwerkstatt wie früher.
            Pruefe.Ungefaehr(3500, w.Bauen.WerkstattPreis(Betriebsart.Giesserei), 1e-9);
            Pruefe.Ungefaehr(3000, w.Bauen.WerkstattPreis(Betriebsart.Schmugglerloch), 1e-9);
            var start = w.BetriebEroeffnen("Gießerei am Weinmarkt", Betriebsart.Giesserei, k, "");
            Pruefe.Ungefaehr(3500, w.Betriebswert(start), 1e-9);
            Pruefe.Ungefaehr(0, w.Betriebswert(w.BetriebEroeffnen("Grube Spieler", Betriebsart.Mine, k, "")), 1e-9);
        }),

        ("Daten: Jede Werkstatt der Stufe 1 hat einen kaufpreis – höhere Stufen und Umland keinen, negativ geht nicht", () =>
        {
            foreach (var (art, d) in Daten.Value.Betriebsarten)
                Pruefe.Wahr(d.InDerStadt && d.Stufe == 1 ? d.Kaufpreis > 0 : d.Kaufpreis == 0, $"{art}: kaufpreis {d.Kaufpreis}");
            JsonNode Knoten() => JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()),
                documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!;
            var schmiede = Knoten();
            schmiede["betriebsarten"]!["Schmiede"]!["kaufpreis"] = 6000;
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(schmiede.ToJsonString()), "Einen kaufpreis haben nur Werkstätten der Stufe 1");
            var negativ = Knoten();
            negativ["betriebsarten"]!["Giesserei"]!["kaufpreis"] = -1;
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(negativ.ToJsonString()), "betriebsarten.Giesserei.kaufpreis darf nicht negativ sein");
        }),

        ("Alter Spielstand (Datenversion 39): Die gekaufte Gießerei an der Südgasse bleibt dem Haus und behält ihren Wert", () =>
        {
            // Ein Spielstand bringt seine Daten mit – so sahen sie vor Datenversion 40 aus: die Werkstatt unter „liegenschaften“, kein kaufpreis an der Art.
            var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()),
                documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!;
            knoten["version"] = 39;
            knoten["betriebsarten"]!["Giesserei"]!.AsObject().Remove("kaufpreis");
            knoten["liegenschaften"]!.AsArray().Add(new JsonObject
            {
                ["name"] = "Gießerei an der Südgasse", ["art"] = "Giesserei", ["kaufpreis"] = 3500, ["mindestStand"] = "Bürger",
            });
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                wirtschaftsdaten: Wirtschaftsdaten.AusJson(knoten.ToJsonString()));
            var w = sim.Wirtschaft!;
            var k = Buerger(w, "Spieler", 8000);
            var e = w.Ausfuehren(k, new LiegenschaftErwerben("Gießerei an der Südgasse"));
            Pruefe.Wahr(e.Erfolg, e.Meldung);

            var (geladen, spieler) = Speicherung.Laden(Speicherung.Speichern(sim, k));
            var gw = geladen.Wirtschaft!;
            var g = gw.Betriebe.Single(b => b.Name == "Gießerei an der Südgasse");
            Pruefe.Gleich(spieler, g.Konto);
            Pruefe.Ungefaehr(3500, gw.Betriebswert(g), 1e-9);
            Pruefe.Ungefaehr(3500, gw.Bauen.WerkstattPreis(Betriebsart.Giesserei), 1e-9); // ohne kaufpreis: der der Liegenschaft
            Pruefe.Wahr(gw.FreieLiegenschaften.All(l => l.Name != g.Name), "gehört dem Haus, nicht frei");
        }),

        ("Werkstatt kaufen: Kaufpreis als Anschaffung, Betrieb mit Handkarren, nicht mehr frei", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = Buerger(w, "Spieler", 8000);
            Werkstatthilfe.FreieWerkstatt(w, "Gießerei an der Südgasse", Betriebsart.Giesserei);
            Betrieb? erworben = null;
            w.BetriebErworben += b => erworben = b;
            var e = w.Ausfuehren(k, new LiegenschaftErwerben("Gießerei an der Südgasse"));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Ungefaehr(4500, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(3500, k.Laufend.Investitionen, 1e-9);
            Pruefe.Gleich("Gießerei an der Südgasse", erworben!.Name);
            Pruefe.Gleich(k, erworben.Konto);
            Pruefe.Gleich("Handkarren", erworben.Karren.Single().Typ);
            Pruefe.Gleich(0, erworben.Belegt);
            Pruefe.Gleich(w.Daten.Liegenschaften.Count, w.FreieLiegenschaften.Count()); // die freie Werkstatt ist fort, das Umland bleibt
        }),

        ("Grube pachten: kein Kaufpreis, aber die erste Jahrespacht muss gedeckt sein", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var arm = Buerger(w, "Arm", 300);
            var e = w.Ausfuehren(arm, new LiegenschaftErwerben("Grube am Silberberg"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("erste Jahrespacht"), e.Meldung);

            var k = Buerger(w, "Spieler", 5000);
            Pruefe.Wahr(w.Ausfuehren(k, new LiegenschaftErwerben("Grube am Silberberg")).Erfolg, "Pacht abgelehnt");
            Pruefe.Ungefaehr(5000, k.Vermoegen, 1e-9);
            var grube = w.Betriebe.Single(b => b.Name == "Grube am Silberberg");
            Pruefe.Gleich(Betriebsart.Mine, grube.Art);
            Pruefe.Gleich("Ochsenkarren", grube.Karren.Single().Typ);
            for (var i = 0; i < Spielzeit.MinutenProTag; i++) sim.Tick();
            Pruefe.Ungefaehr(w.Daten.Allgemein.PachtMine, k.Vorjahr!.Fixkosten, 1e-9); // Pacht am Jahresende
        }),

        ("Jede Liegenschaft gibt es nur einmal", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var a = Buerger(w, "A", 20000);
            var b = Buerger(w, "B", 20000);
            Pruefe.Wahr(w.Ausfuehren(a, new LiegenschaftErwerben("Grube im Tannenwald")).Erfolg, "erster Erwerb abgelehnt");
            var e = w.Ausfuehren(b, new LiegenschaftErwerben("Grube im Tannenwald"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("gehört bereits A"), e.Meldung);
            Pruefe.Wahr(!w.Ausfuehren(b, new LiegenschaftErwerben("Burg Drachenfels")).Erfolg, "unbekannte Liegenschaft angenommen");
        }),

        // Bis PR #177 hieß die Erwartung „Grube unter Statut B bringt mehr als eine Werkstatt“. Seit die Fertigwaren teurer sind (Z2) gilt das
        // nicht mehr (1.878 gegen 1.966 zu Startpreisen) und ist kein Ziel mehr (Entscheidung des Nutzers vom 22.10.2026); es bleibt Z5.
        ("Erwartung: Eine Grube unter Statut B bringt deutlich mehr als unter A (Z5)", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var grube = Kalkulation.ErwarteterJahresgewinn(w, Betriebsart.Mine, Fronstatut.Bestienfron);
            var grubeA = Kalkulation.ErwarteterJahresgewinn(w, Betriebsart.Mine, Fronstatut.FreieStadt);
            Pruefe.Wahr(grube > grubeA * 1.3 && grubeA > 0, $"Grube unter B {grube:0}, unter A {grubeA:0}");
        }),

        ("KI expandiert als Bürger – aber nur bis zu ihrer Höchstzahl an Betrieben", () =>
        {
            // Ohne den Wachturm: Seit Datenversion 57 (Sold 0,84) pachtete die KI ihn zuerst – zum Gardisten gibt es keine zweite freie Liegenschaft.
            var knoten = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()))!;
            var liegenschaften = knoten["liegenschaften"]!.AsArray();
            liegenschaften.Remove(liegenschaften.Single(l => (string?)l!["art"] == "Wachturm"));
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                wirtschaftsdaten: Wirtschaftsdaten.AusJson(knoten.ToJsonString()));
            var w = sim.Wirtschaft!;
            var k = Buerger(w, "Rivale", 19000); // unter der Patrizier-Schwelle – sonst dürfte die KI einen Betrieb mehr führen
            Werkstatthilfe.FreieWerkstatt(w, "Schänke am Fischmarkt", Betriebsart.Schaenke); // ohne Karte wird nicht gebaut
            w.KiAnmelden(k, KiProfil.Standard with { MaxBetriebe = 2 });
            Pruefe.Gleich(1, w.Betriebe.Count(b => b.Konto == k)); // höchstens ein Erwerb pro Stunde
            for (var i = 0; i < 3 * 60; i++) sim.Tick();
            Pruefe.Gleich(2, w.Betriebe.Count(b => b.Konto == k));
            Pruefe.Wahr(w.Betriebe.Where(b => b.Konto == k).All(b => b.Belegt > 0), "neue Betriebe ohne Personal");
            Pruefe.Gleich(w.Daten.Liegenschaften.Count - 1, w.FreieLiegenschaften.Count()); // ein Umlandbetrieb und die freie Schänke
            // Mit dem ersten Erwerb hat die KI ihr Handwerk – der zweite gehört zum selben Beruf.
            Pruefe.Gleich(1, w.Betriebe.Where(b => b.Konto == k).Select(b => b.Artdaten.Beruf).Distinct().Count());
        }),

        ("Daten: Mindeststand muss es geben", () =>
        {
            var json = File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"mindestStand\": \"Bürger\"", "\"mindestStand\": \"Kaiser\"");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json), "mindestStand „Kaiser“ gibt es nicht");
        }),
    };
}
