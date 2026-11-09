using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Ein Lehrling zum Start (Datenversion 72, Entscheidung des Nutzers 4.11.2026): Jeder Startbetrieb beginnt mit einem Anfänger – beim Spieler
/// und bei den KI-Häusern gleich (Mehrspieler-Fairness), bei „Schwer“ auch im Umland. Weitere stellt jeder selbst ein. Ohne
/// <c>personal.startJeBetrieb</c> in den Daten wie bis Datenversion 71: zwei in der Stadt, drei im Umland.
/// </summary>
static class StartlehrlingTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static Wirtschaftsdaten Daten() => Wirtschaftsdaten.AusJson(Json.Value);
    static Wirtschaftsdaten OhneAngabe() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"startJeBetrieb\": 1,", ""));

    static (Simulation Sim, Konto Spieler) Partie(Wirtschaftsdaten daten, Schwierigkeit schwierigkeit, string beruf = "Schmied") =>
        Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = schwierigkeit },
            daten, new NeuePartie(Beruf: beruf), karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Startlehrling: Daten personal.startJeBetrieb = 1 (Datenversion 72); 0 lehnt die Prüfung ab", () =>
        {
            var d = Daten();
            Pruefe.Gleich(76, d.Version);
            Pruefe.Gleich(1, d.Personal!.StartJeBetrieb ?? 0);
            Pruefe.Wahr(OhneAngabe().Personal!.StartJeBetrieb is null, "ohne Angabe null");
            var falsch = false;
            try { Wirtschaftsdaten.AusJson(Json.Value.Replace("\"startJeBetrieb\": 1,", "\"startJeBetrieb\": 0,")); }
            catch (Exception ex) { falsch = ex.Message.Contains("startJeBetrieb"); }
            Pruefe.Wahr(falsch, "startJeBetrieb 0 wird abgelehnt");
        }),

        ("Startlehrling: Normal – Spieler und KI-Häuser beginnen mit einem Lehrling; die KI stellt den zweiten schon beim Anmelden ein (Schmied, Wirt, Steinmetz, Geldleiher)", () =>
        {
            foreach (var beruf in new[] { "Schmied", "Wirt", "Steinmetz", "Geldleiher" })
            {
                var (sim, spieler) = Partie(Daten(), Schwierigkeit.Normal, beruf);
                var w = sim.Wirtschaft!;
                Pruefe.Gleich(w.Konten.Count, w.Betriebe.Count, $"{beruf}: ein Betrieb je Haus");
                var eigene = w.Betriebe.Single(b => b.Konto == spieler);
                Pruefe.Gleich(1, eigene.Belegt, $"{beruf}: der Spieler beginnt mit einem");
                Pruefe.Gleich("Lehrling", eigene.Arbeiter[0].Arbeitskraft, $"{beruf}: der Spieler hat einen Lehrling");
                foreach (var b in w.Betriebe.Where(b => b.Konto != spieler))
                {
                    // Aufgestellt mit einem; die KI füllt in ihrer ersten Stunde (beim Anmelden) den freien Platz mit einem Anfänger.
                    var ki = w.Rivalen.Single(r => r.Konto == b.Konto);
                    Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains($": {b.Name} stellt {b.Plaetze - 1}× {w.Personal.Anfaenger(b)} ein")),
                        $"{beruf}: {b.Name} – {string.Join(" | ", ki.Entscheidungen)}");
                    Pruefe.Wahr(b.Arbeiter.All(a => w.Personal.IstAnfaenger(a)), $"{beruf}: {b.Name} nur Anfänger");
                }
            }
        }),

        ("Startlehrling: Schwer – auch die Umlandbetriebe der KI beginnen mit einer Kraft (die KI füllt die übrigen Plätze selbst)", () =>
        {
            var (sim, spieler) = Partie(Daten(), Schwierigkeit.Schwer);
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(w.Betriebe.Any(b => !b.Artdaten.InDerStadt), "es gibt Umlandbetriebe");
            Pruefe.Gleich(1, w.Betriebe.Single(b => b.Konto == spieler).Belegt, "der Spieler");
            foreach (var b in w.Betriebe.Where(b => b.Konto != spieler))
            {
                var ki = w.Rivalen.Single(r => r.Konto == b.Konto);
                Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains($": {b.Name} stellt {b.Plaetze - 1}× ")), $"{b.Name} – {string.Join(" | ", ki.Entscheidungen)}");
            }
        }),

        ("Startlehrling: Ohne personal.startJeBetrieb wie bis Datenversion 71 – zwei in der Stadt, drei im Umland, die KI muss niemanden einstellen", () =>
        {
            var (sim, spieler) = Partie(OhneAngabe(), Schwierigkeit.Schwer);
            var w = sim.Wirtschaft!;
            foreach (var b in w.Betriebe)
                Pruefe.Gleich(b.Artdaten.InDerStadt ? 2 : 3, b.Belegt, b.Name);
            foreach (var ki in w.Rivalen)
                Pruefe.Wahr(!ki.Entscheidungen.Any(e => e.Contains(" ein (erwarteter Ertrag")), string.Join(" | ", ki.Entscheidungen));
        }),

        ("Startlehrling: Der Spieler behält seinen einen Lehrling, bis er selbst einstellt; Einstellen kostet nur das Handgeld (Datenversion 73)", () =>
        {
            var (sim, spieler) = Partie(Daten(), Schwierigkeit.Normal);
            var w = sim.Wirtschaft!;
            var eigene = w.Betriebe.Single(b => b.Konto == spieler);
            for (var i = 0; i < 120; i++) sim.Tick();
            Pruefe.Gleich(1, eigene.Belegt, "nach zwei Stunden");
            var vorher = spieler.Vermoegen;
            var e = w.Ausfuehren(spieler, new ArbeiterEinstellen(eigene.Name, "Lehrling"));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(2, eigene.Belegt, "nach dem Einstellen");
            Pruefe.Gleich(vorher - w.Personal.Handgeld("Lehrling"), spieler.Vermoegen, "Einstellen kostet nur das Handgeld (der Lohn kommt am Jahresende)");
        }),
    };
}
