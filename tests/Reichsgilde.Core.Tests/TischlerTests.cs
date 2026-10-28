using Reichsgilde.Core;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für den zweiten Beruf (Tischler), datengetriebene Betriebsarten und das Erlernen von Berufen.</summary>
static class TischlerTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation(int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: Daten.Value);

    static void Arbeitstag(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: achtunddreißig Betriebsarten, zehn Berufe – der Tischler macht aus Holz Holzzapfen, Schemel und Truhen", () =>
        {
            var d = Daten.Value;
            Pruefe.Gleich(38, d.Betriebsarten.Count);
            Pruefe.Gleich(10, d.Berufe.Count);
            var t = d.Art(Betriebsart.Tischlerei);
            Pruefe.Gleich("Tischler", t.Beruf);
            Pruefe.Wahr(t.Produkte.SequenceEqual(new[] { "Holzzapfen", "Schemel", "Truhe" }), string.Join(",", t.Produkte));
            Pruefe.Wahr(d.Art(Betriebsart.Holzfaellerei).Rohstoffe.SequenceEqual(new[] { "Holz" }), "Holzschlag fördert Holz");
            Pruefe.Gleich(1, d.Waren["Truhe"].Zutaten["Beschlag"]); // die Truhe braucht den Schmied
            var json = File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"produkte\": [ \"Holzzapfen\", \"Schemel\", \"Truhe\" ]", "\"produkte\": [ \"Holz\", \"Schemel\", \"Truhe\" ]");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json), "„Holz“ ist keine Ware mit Zutaten");
        }),

        ("Holzschlag: fördert nur Holz – aufteilen gibt es nicht; die Tischlerei stellt nur Tischlerwaren her", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Fuchs", startvermoegen: 20_000);
            var h = w.BetriebEroeffnen("Holzschlag Fuchs", Betriebsart.Holzfaellerei, k, startkarren: "");
            w.Einstellen(h, "Altknecht", 3);
            Pruefe.Wahr(!w.Ausfuehren(k, new FoerderungAufteilen(h.Name, 0.5)).Erfolg, "Aufteilen bei einem Rohstoff");
            var t = w.BetriebEroeffnen("Tischlerei Fuchs", Betriebsart.Tischlerei, k);
            Pruefe.Gleich("Schemel", t.Produkt);
            var e = w.Ausfuehren(k, new ProduktWaehlen(t.Name, "Rapier"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("nicht herstellen"), e.Meldung);
            for (var i = 0; i < 600; i++) sim.Tick(); // bis in den Nachmittag
            Pruefe.Wahr(h.ImLager("Holz") > 0, "kein Holz gefördert");
            Pruefe.Gleich(0, h.ImLager("Eisen"));
        }),

        ("Tischlerei: Eine Truhe braucht 2 Holz und 1 Beschlag aus dem Lager", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Fuchs", startvermoegen: 20_000);
            var t = w.BetriebEroeffnen("Tischlerei Fuchs", Betriebsart.Tischlerei, k, startkarren: "");
            Pruefe.Wahr(w.Ausfuehren(k, new ProduktWaehlen(t.Name, "Truhe")).Erfolg, "Truhe");
            t.Einlagern("Holz", 4);
            t.Einlagern("Beschlag", 1);
            w.Einstellen(t, "Geselle", 2);
            for (var i = 0; i < 600; i++) sim.Tick();
            Pruefe.Gleich(1, t.ImLager("Truhe"));
            Pruefe.Gleich(2, t.ImLager("Holz"));
            Pruefe.Gleich(0, t.ImLager("Beschlag"));
            Pruefe.Wahr(t.Stillstand?.Contains("Beschlag") == true, t.Stillstand ?? "kein Stillstand");
        }),

        ("Berufe: Ein Schmied darf keine Tischlerei kaufen – erst erlernen (4 AP, 500 Gulden Lehrgeld)", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Startvermoegen: 20_000));
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(spieler.Berufe.SequenceEqual(new[] { "Schmied" }), string.Join(",", spieler.Berufe));
            w.Ausfuehren(Wohnhaushilfe.MitWohnhaus(w, spieler), new StandErwerben());
            Werkstatthilfe.FreieWerkstatt(w, "Tischlerei am Holzmarkt", Betriebsart.Tischlerei);
            var e = w.Ausfuehren(spieler, new LiegenschaftErwerben("Tischlerei am Holzmarkt"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("erst erlernen"), e.Meldung);
            spieler.Aktionspunkte.Setzen(4);
            var vorher = spieler.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(spieler, new BerufErlernen("Tischler")).Erfolg, "erlernen");
            Pruefe.Gleich(0, spieler.Aktionspunkte.Vorrat);
            Pruefe.Ungefaehr(vorher - 500, spieler.Vermoegen, 1e-9);
            Pruefe.Wahr(spieler.Beherrscht("Tischler"), "Tischler");
            Pruefe.Wahr(!w.Ausfuehren(spieler, new BerufErlernen("Tischler")).Erfolg, "schon beherrscht");
            Pruefe.Wahr(w.Ausfuehren(spieler, new LiegenschaftErwerben("Tischlerei am Holzmarkt")).Erfolg, "kaufen");
        }),

        ("Partieaufbau: Startberuf Tischler – Tischlerei am Weinmarkt; drei Rivalen, Haus Eichwald sind Elben und Tischler", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Beruf: "Tischler"));
            var w = sim.Wirtschaft!;
            var eigene = w.Betriebe.Single(b => b.Konto == spieler);
            Pruefe.Gleich("Tischlerei am Weinmarkt", eigene.Name);
            Pruefe.Gleich(Betriebsart.Tischlerei, eigene.Art);
            Pruefe.Gleich(3, w.Rivalen.Count);
            var eichwald = w.Konten.Single(k => k.Inhaber == Partieaufbau.ZweiterRivale);
            Pruefe.Gleich(Partieaufbau.ZweiterRivaleVolk, w.Familien.Von(eichwald)!.Volk);
            // Normal (Vorgabe): Eichwald beginnt wie der Spieler mit einer Werkstatt, sein Holzschlag ist frei zur Pacht.
            Pruefe.Wahr(w.Betriebe.Where(b => b.Konto == eichwald).Select(b => b.Art).SequenceEqual(new[] { Betriebsart.Tischlerei }), "Betriebe von Eichwald");
            Pruefe.Wahr(w.FreieLiegenschaften.Any(l => l.Name == "Holzschlag Eichwald" && l.IstPacht), "Holzschlag Eichwald frei");

            var (sim1, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Rivalen: 1));
            Pruefe.Gleich(1, sim1.Wirtschaft!.Rivalen.Count);
            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Beruf: "Bäcker")), "„Bäcker“ gibt es nicht");
        }),

        ("KI Eichwald: Holzschlag liefert Holz, die Tischlerei verkauft Tischlerwaren – mit Gewinn", () =>
        {
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var eichwald = w.Konten.Single(k => k.Inhaber == Partieaufbau.ZweiterRivale);
            for (var j = 0; j < 3; j++) Arbeitstag(sim);
            var bilanz = eichwald.Vorjahr!;
            Pruefe.Wahr(bilanz.Hergestellt.GetValueOrDefault("Holz") > 0, "kein Holz");
            Pruefe.Wahr(bilanz.Hergestellt.Keys.Any(x => x is "Holzzapfen" or "Schemel" or "Truhe"), string.Join(",", bilanz.Hergestellt.Keys));
            Pruefe.Wahr(!bilanz.Hergestellt.Keys.Any(x => x is "Rapier" or "Silberring" or "Beschlag"), "Tischler stellt Schmiedewaren her");
            Pruefe.Wahr(bilanz.Gewinn > 0, $"Gewinn {bilanz.Gewinn}");
            Pruefe.Wahr(Kalkulation.ErwarteterJahresgewinn(w, Betriebsart.Tischlerei) > 0, "Tischlerei lohnt sich nicht");
        }),

        ("Speichern: Berufe überstehen Speichern und Laden; alte Stände leiten sie aus den Betrieben ab", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Startvermoegen: 20_000));
            var w = sim.Wirtschaft!;
            spieler.Aktionspunkte.Setzen(4);
            w.Ausfuehren(spieler, new BerufErlernen("Tischler"));
            var json = Speicherung.Speichern(sim, spieler);
            var (_, kopie) = Speicherung.Laden(json);
            Pruefe.Wahr(kopie!.Berufe.SequenceEqual(new[] { "Schmied", "Tischler" }), string.Join(",", kopie.Berufe));

            var alt = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            foreach (var konto in alt["Wirtschaft"]!["Konten"]!.AsArray())
                konto!.AsObject().Remove("Berufe");
            var (geladen, altSpieler) = Speicherung.Laden(alt.ToJsonString());
            Pruefe.Wahr(altSpieler!.Berufe.SequenceEqual(new[] { "Schmied" }), string.Join(",", altSpieler.Berufe));
            var eichwald = geladen.Wirtschaft!.Konten.Single(k => k.Inhaber == Partieaufbau.ZweiterRivale);
            Pruefe.Wahr(eichwald.Berufe.SequenceEqual(new[] { "Tischler" }), "Eichwald");
        }),
    };
}
