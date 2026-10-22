using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für den dritten Beruf (Steinmetz) und Haus Kieselbach.</summary>
static class SteinmetzTests
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
        ("Daten: Steinbruch bricht Bruchstein und Granit, drei Stufen bis zur Steinmetzhütte – verflochten mit dem Schmied", () =>
        {
            var d = Daten.Value;
            Pruefe.Wahr(d.Art(Betriebsart.Steinbruch).Rohstoffe.SequenceEqual(new[] { "Bruchstein", "Granit" }), "Steinbruch");
            Pruefe.Wahr(d.Art(Betriebsart.Maurerei).Produkte.SequenceEqual(new[] { "Schleifziegel", "Grabstein", "Mühlstein" }), "Maurerei");
            Pruefe.Gleich(Betriebsart.Steinschneiderei, d.Art(Betriebsart.Maurerei).Ausbau);
            Pruefe.Gleich(Betriebsart.Steinmetzhuette, d.Art(Betriebsart.Steinschneiderei).Ausbau);
            Pruefe.Gleich(4, d.Art(Betriebsart.Steinmetzhuette).Plaetze);
            Pruefe.Gleich(1, d.Waren["Steinfigur"].Zutaten["Werkzeug"]);    // die Steinmetzhütte braucht den Schmied …
            Pruefe.Gleich(1, d.Waren["Prunkschwert"].Zutaten["Schleifziegel"]); // … und die Goldschmiede den Steinmetz
            Pruefe.Gleich(Betriebsart.Maurerei, d.Berufe["Steinmetz"].Werkstatt);
        }),

        ("Steinbruch: Arbeitszeit lässt sich zwischen Bruchstein und Granit aufteilen; die Maurerei macht Mühlsteine aus dem Lager", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Fuchs", startvermoegen: 20_000);
            var bruch = w.BetriebEroeffnen("Steinbruch Fuchs", Betriebsart.Steinbruch, k, startkarren: "");
            w.Einstellen(bruch, "Bergmann", 3);
            Pruefe.Wahr(w.Ausfuehren(k, new FoerderungAufteilen(bruch.Name, 0.5)).Erfolg, "aufteilen");
            var m = w.BetriebEroeffnen("Maurerei Fuchs", Betriebsart.Maurerei, k, startkarren: "");
            Pruefe.Gleich("Grabstein", m.Produkt);
            Pruefe.Wahr(w.Ausfuehren(k, new ProduktWaehlen(m.Name, "Mühlstein")).Erfolg, "Mühlstein");
            Pruefe.Wahr(!w.Ausfuehren(k, new ProduktWaehlen(m.Name, "Taufbecken")).Erfolg, "Taufbecken erst ab Stufe 2");
            m.Einlagern("Bruchstein", 4);
            m.Einlagern("Schleifziegel", 1);
            w.Einstellen(m, "Geselle", 2);
            for (var i = 0; i < 600; i++) sim.Tick();
            Pruefe.Gleich(1, m.ImLager("Mühlstein"));
            Pruefe.Gleich(2, m.ImLager("Bruchstein"));
            Pruefe.Wahr(bruch.ImLager("Bruchstein") > 0 && bruch.ImLager("Granit") > 0, "Steinbruch fördert beides");
            Pruefe.Wahr(k.Erfahrung("Steinmetz") > 0, "keine Erfahrung als Steinmetz");
        }),

        ("Partieaufbau: Startberuf Steinmetz – Maurerei am Weinmarkt; drei Rivalen, Haus Kieselbach sind Halblinge und Steinmetze", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Schwierigkeit = Schwierigkeit.Schwer }, Daten.Value,
                new NeuePartie(Beruf: "Steinmetz"));
            var w = sim.Wirtschaft!;
            var eigene = w.Betriebe.Single(b => b.Konto == spieler);
            Pruefe.Gleich("Maurerei am Weinmarkt", eigene.Name);
            Pruefe.Wahr(spieler.Berufe.SequenceEqual(new[] { "Steinmetz" }), string.Join(",", spieler.Berufe));
            Pruefe.Gleich(3, w.Rivalen.Count);
            var kieselbach = w.Konten.Single(k => k.Inhaber == Partieaufbau.DritterRivale);
            Pruefe.Gleich(Partieaufbau.DritterRivaleVolk, w.Familien.Von(kieselbach)!.Volk);
            Pruefe.Wahr(w.Betriebe.Where(b => b.Konto == kieselbach).Select(b => b.Name).OrderBy(n => n)
                .SequenceEqual(new[] { "Maurerei Kieselbach", "Steinbruch Kieselbach" }), "Betriebe von Kieselbach");
            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Familienname: "Haus Kieselbach")),
                "schon der Name eines Rivalen");
            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Rivalen: 4)), "ein bis drei");
            var (sim2, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Rivalen: 2));
            Pruefe.Wahr(sim2.Wirtschaft!.Konten.All(k => k.Inhaber != Partieaufbau.DritterRivale), "mit zwei Rivalen ohne Kieselbach");
        }),

        ("KI Kieselbach: Steinbruch liefert Stein, die Maurerei verkauft Steinmetzwaren – mit Gewinn", () =>
        {
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var kieselbach = w.Konten.Single(k => k.Inhaber == Partieaufbau.DritterRivale);
            for (var j = 0; j < 3; j++) Arbeitstag(sim);
            var bilanz = kieselbach.Vorjahr!;
            Pruefe.Wahr(bilanz.Hergestellt.GetValueOrDefault("Bruchstein") + bilanz.Hergestellt.GetValueOrDefault("Granit") > 0, "kein Stein");
            Pruefe.Wahr(bilanz.Hergestellt.Keys.Any(x => x is "Schleifziegel" or "Grabstein" or "Mühlstein" or "Säule" or "Taufbecken"),
                string.Join(",", bilanz.Hergestellt.Keys));
            Pruefe.Wahr(bilanz.Gewinn > 0, $"Gewinn {bilanz.Gewinn}");
            Pruefe.Wahr(Kalkulation.ErwarteterJahresgewinn(w, Betriebsart.Maurerei) > 0, "Maurerei lohnt sich nicht");
        }),
    };
}
