using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für den vierten Beruf (Alchimist), den Krötenschleim-Vorteil und KI-Häuser, die einen zweiten Beruf erlernen.</summary>
static class AlchimistTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation(int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: Daten.Value);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: Kräutergarten zieht Kräuter und fängt Kröten; die Tinkturei macht Farbe, Kräuterschnaps und Heilsalbe – verflochten mit drei Berufen", () =>
        {
            var d = Daten.Value;
            Pruefe.Wahr(d.Art(Betriebsart.Kraeutergarten).Rohstoffe.SequenceEqual(new[] { "Kräuter", "Kröte" }), "Kräutergarten");
            Pruefe.Wahr(d.Art(Betriebsart.Tinkturei).Produkte.SequenceEqual(new[] { "Farbe", "Kräuterschnaps", "Heilsalbe" }), "Tinkturei");
            Pruefe.Gleich(Betriebsart.Alchimistenstube, d.Art(Betriebsart.Kraeuterladen).Ausbau);
            Pruefe.Gleich(1, d.Waren["Kräuterschnaps"].Zutaten["Kräuter"]); // der Schnaps hat jetzt einen Hersteller
            Pruefe.Gleich(1, d.Waren["Schrank"].Zutaten["Farbe"]);           // der Tischler braucht Farbe …
            Pruefe.Gleich(1, d.Waren["Zinnober"].Zutaten["Schleifziegel"]);  // … der Alchimist Schleifziegel und Silber
            Pruefe.Gleich(1, d.Waren["Zinnober"].Zutaten["Silber"]);
            Pruefe.Wahr(d.Berufe.ContainsKey("Alchimist"), "Alchimist fehlt");
        }),

        ("Tinkturei: Kräuterschnaps aus einem Bund Kräuter – das Haus sammelt Erfahrung als Alchimist", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Fuchs", startvermoegen: 20_000);
            var t = w.BetriebEroeffnen("Tinkturei Fuchs", Betriebsart.Tinkturei, k, startkarren: "");
            Pruefe.Gleich("Heilsalbe", t.Produkt);
            Pruefe.Wahr(w.Ausfuehren(k, new ProduktWaehlen(t.Name, "Kräuterschnaps")).Erfolg, "Schnaps");
            t.Einlagern("Kräuter", 3);
            w.Einstellen(t, "Geselle", 2);
            for (var i = 0; i < 600; i++) sim.Tick(); // bis 16 Uhr
            Pruefe.Gleich(3, t.ImLager("Kräuterschnaps"));
            Pruefe.Gleich(0, t.ImLager("Kräuter"));
            Pruefe.Wahr(k.Beherrscht("Alchimist") && k.Erfahrung("Alchimist") > 0, "Erfahrung");
        }),

        ("Krötenschleim: Ein Alchimist mischt selbst – halbe Kosten, höhere Erfolgschance", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var u = w.Gauner;
            var fremd = w.KontoEroeffnen("Familie Fuchs", startvermoegen: 5_000);
            var alchimist = w.KontoEroeffnen("Familie Kraut", startvermoegen: 5_000);
            w.BetriebEroeffnen("Tinkturei Kraut", Betriebsart.Tinkturei, alchimist);
            Pruefe.Gleich(200.0, u.Kosten(Gaunerstueck.Kroetenschleim, fremd));
            Pruefe.Gleich(100.0, u.Kosten(Gaunerstueck.Kroetenschleim, alchimist));
            Pruefe.Gleich(150.0, u.Kosten(Gaunerstueck.Diebstahl, alchimist)); // gilt nur für den Schleim
            Pruefe.Ungefaehr(u.Erfolgschance(Gaunerstueck.Kroetenschleim, null, fremd) + 0.15,
                u.Erfolgschance(Gaunerstueck.Kroetenschleim, null, alchimist), 1e-9);

            var opfer = w.KontoEroeffnen("Haus Eisenbart");
            w.Familien.Gruenden(opfer, Volk.Zwerg, Geschlecht.Mann);
            var vorher = alchimist.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(alchimist, new Beauftragen(opfer.Inhaber, Gaunerstueck.Kroetenschleim)).Erfolg, "bestellen");
            Pruefe.Ungefaehr(vorher - 100, alchimist.Vermoegen, 1e-9);
        }),

        ("Partieaufbau: Startberuf Alchimist – Tinkturei am Weinmarkt", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Beruf: "Alchimist"));
            var eigene = sim.Wirtschaft!.Betriebe.Single(b => b.Konto == spieler);
            Pruefe.Gleich("Tinkturei am Weinmarkt", eigene.Name);
            Pruefe.Gleich(Betriebsart.Tinkturei, eigene.Art);
        }),

        ("KI: Ein reiches Haus (Patrizier) erlernt einen zweiten Beruf, um einen freien Betrieb zu übernehmen – ein Bürger nicht", () =>
        {
            foreach (var reich in new[] { false, true })
            {
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 9 },
                    Daten.Value, new NeuePartie(Rivalen: 1));
                var w = sim.Wirtschaft!;
                var eisenbart = w.Rivalen[0].Konto;
                // Alle freien Schmiedebetriebe sind schon vergeben – wachsen kann Eisenbart nur in einem fremden Handwerk.
                var nachbar = w.KontoEroeffnen("Familie Fuchs");
                foreach (var l in w.FreieLiegenschaften.Where(l => w.Daten.Art(l.Art).Beruf == "Schmied").ToList())
                    w.BetriebEroeffnen(l.Name, l.Art, nachbar);
                eisenbart.Umsatz(reich ? 60_000 : 15_000); // als Bürger unter der Patrizier-Schwelle (20.000), sonst stiege die KI selbst auf
                eisenbart.Stand = w.Daten.StandStufe(reich ? "Patrizier" : "Bürger");
                for (var i = 0; i < 4 * 60; i++) sim.Tick(); // vier Stunden: jede Stunde überlegt die KI
                var betriebe = w.Betriebe.Where(b => b.Konto == eisenbart).ToList();
                if (!reich)
                {
                    Pruefe.Wahr(eisenbart.Berufe.SequenceEqual(new[] { "Schmied" }), string.Join(",", eisenbart.Berufe));
                    Pruefe.Gleich(2, betriebe.Count);
                    continue;
                }
                Pruefe.Gleich(2, eisenbart.Berufe.Count);
                Pruefe.Wahr(betriebe.Count is 3 or 4, $"{betriebe.Count} Betriebe");
                var neu = eisenbart.Berufe.Single(b => b != "Schmied");
                Pruefe.Wahr(betriebe.Any(b => b.Artdaten.Beruf == neu), $"kein Betrieb als {neu}");
            }
        }),
    };
}
