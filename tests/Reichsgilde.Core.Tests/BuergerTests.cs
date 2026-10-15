using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für die Bürger-KI: Einwohner, Tagesablauf, Abgleich mit Rat und Brautschau, Speichern – und keine Wirkung auf die Wirtschaft.</summary>
static class BuergerTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Dieselben Daten, aber verändert (z. B. ohne Abschnitt „buerger").</summary>
    static Wirtschaftsdaten Abgewandelt(Action<JsonObject> aendern)
    {
        var json = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()), documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!.AsObject();
        aendern(json);
        return Wirtschaftsdaten.AusJson(json.ToJsonString());
    }

    static readonly Lazy<Wirtschaftsdaten> OhneBuerger = new(() => Abgewandelt(j => j.Remove("buerger")));

    static (Simulation Sim, Konto Spieler) NeuePartie(Wirtschaftsdaten? daten = null, int startwert = 1400) =>
        Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            daten ?? Daten.Value, new NeuePartie(Rivale: KiCharakter.Wagemutig));

    static void Ticken(Simulation sim, long minuten)
    {
        for (long i = 0; i < minuten; i++) sim.Tick();
    }

    /// <summary>Bis kurz nach der angegebenen Uhrzeit im laufenden Jahr ticken.</summary>
    static void Bis(Simulation sim, int stunde, int minute = 30)
    {
        var ziel = stunde * 60 + minute;
        while (sim.Zeit.UhrzeitInMinuten != ziel) sim.Tick();
    }

    static Einwohnerschaft Einwohner(Simulation sim) => sim.Wirtschaft!.Einwohnerschaft;

    /// <summary>Spielstand ohne die Daten und ohne die Einwohner – also nur das, was die Wirtschaft ausmacht.</summary>
    static string OhneEinwohner(Simulation sim, Konto spieler)
    {
        var json = JsonNode.Parse(Speicherung.Speichern(sim, spieler, new DateTime(2026, 10, 15)))!.AsObject();
        json.Remove("Daten");
        json["Wirtschaft"]!.AsObject().Remove("Einwohner");
        return json.ToJsonString();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Bürger-KI: 25 Einwohner je Haus, dazu Ratsherren und Kandidaten; nur Kulturvölker", () =>
        {
            var (sim, _) = NeuePartie();
            var ew = Einwohner(sim);
            Pruefe.Gleich(0, ew.Einwohner.Count); // vor dem ersten Takt ist noch niemand eingezogen
            sim.Tick();
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(25 * w.Konten.Count, ew.Einwohner.Count(e => e.Herkunft == Herkunft.Stadt));
            Pruefe.Gleich(w.Rat.Ratsherren.Count, ew.Einwohner.Count(e => e.Herkunft == Herkunft.Ratsherr));
            Pruefe.Gleich(w.Familien.Kandidaten.Count, ew.Einwohner.Count(e => e.Herkunft == Herkunft.Kandidat));
            Pruefe.Wahr(ew.Einwohner.All(e => e.Volk is not (Volk.Ork or Volk.Oger)), "Orks und Oger haben kein Bürgerrecht");
            Pruefe.Wahr(ew.Einwohner.Select(e => e.Nummer).Distinct().Count() == ew.Einwohner.Count, "Nummern eindeutig");
            Pruefe.Wahr(ew.Einwohner.Where(e => e.Herkunft == Herkunft.Stadt).Select(e => e.Volk).Distinct().Count() >= 3,
                        "Bei 100 Einwohnern sollten mindestens drei Völker vertreten sein");
            var ratsherr = ew.Einwohner.First(e => e.Herkunft == Herkunft.Ratsherr);
            Pruefe.Gleich(ratsherr.Quelle, ratsherr.Name);
            Pruefe.Gleich("Ratsherr", ratsherr.Rolle);
        }),

        ("Bürger-KI: Ohne Abschnitt „buerger“ gibt es keine Einwohner, und der Spielstand hat kein Feld dafür", () =>
        {
            var (sim, spieler) = NeuePartie(OhneBuerger.Value);
            Ticken(sim, 300);
            Pruefe.Gleich(false, Einwohner(sim).IstAktiv);
            Pruefe.Gleich(0, Einwohner(sim).Einwohner.Count);
            var json = JsonNode.Parse(Speicherung.Speichern(sim, spieler))!;
            Pruefe.Wahr(json["Wirtschaft"]!["Einwohner"] is null, "Einwohner im Spielstand ohne Bürger-KI");
            var (geladen, _) = Speicherung.Laden(json.ToJsonString());
            Ticken(geladen, 300);
            Pruefe.Gleich(0, Einwohner(geladen).Einwohner.Count);
        }),

        ("Bürger-KI: Keine Wirkung auf die Wirtschaft – mit und ohne Einwohner läuft die Partie 2 Jahre gleich", () =>
        {
            var (mit, spielerMit) = NeuePartie(startwert: 4711);
            var (ohne, spielerOhne) = NeuePartie(OhneBuerger.Value, startwert: 4711);
            Ticken(mit, 2 * Spielzeit.MinutenProTag + 400);
            Ticken(ohne, 2 * Spielzeit.MinutenProTag + 400);
            Pruefe.Wahr(Einwohner(mit).Einwohner.Count > 0, "Mit Abschnitt gibt es Einwohner");
            Pruefe.Gleich(OhneEinwohner(ohne, spielerOhne), OhneEinwohner(mit, spielerMit));
        }),

        ("Bürger-KI: Tagesablauf – morgens zu Hause, tagsüber Arbeit, nach 20 Uhr niemand bei der Arbeit oder in der Gasse", () =>
        {
            var (sim, _) = NeuePartie();
            sim.Tick();
            var ew = Einwohner(sim);
            Pruefe.Wahr(ew.Einwohner.All(e => e.Ort == Stadtort.Heim), "Um 05:00 schlafen alle noch");
            Bis(sim, 10);
            var arbeiten = ew.Einwohner.Count(e => e.Handlung == Handlung.Arbeiten);
            Pruefe.Wahr(arbeiten >= ew.Einwohner.Count * 0.4, $"Um 10:30 sollte die Mehrheit arbeiten (waren {arbeiten} von {ew.Einwohner.Count})");
            Pruefe.Wahr(ew.Einwohner.Select(e => e.Handlung).Distinct().Count() >= 3, "Tagsüber tun nicht alle dasselbe");
            Bis(sim, 20);
            Pruefe.Wahr(ew.Einwohner.All(e => e.Handlung is Handlung.Ruhen or Handlung.Einkehren),
                        "Nach 20 Uhr ziehen die Gauner los – die Einwohner sind zu Hause oder in der Schänke");
        }),

        ("Bürger-KI: Arbeitsort nach Rolle; Ratsherren arbeiten im Rathaus", () =>
        {
            var (sim, _) = NeuePartie();
            sim.Tick();
            Bis(sim, 11);
            var ew = Einwohner(sim);
            foreach (var e in ew.Einwohner.Where(e => e.Handlung == Handlung.Arbeiten && e.Ort != Stadtort.Kirche))
                Pruefe.Gleich(ew.Daten.Rollen[e.Rolle].Arbeitsort, e.Ort, $"Arbeitsort von {e.Name} ({e.Rolle})");
            Pruefe.Wahr(ew.Am(Stadtort.Rathaus).Any(e => e.Herkunft == Herkunft.Ratsherr), "Um 11 Uhr sitzt ein Ratsherr im Rathaus");
        }),

        ("Bürger-KI: Abends kehren Einwohner in die Schänke ein – jeder in seine Stammschänke", () =>
        {
            var (sim, _) = NeuePartie();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Krug", startvermoegen: 10_000);
            var s = w.BetriebEroeffnen("Schänke Familie Krug", Betriebsart.Schaenke, k, startkarren: "");
            w.Einstellen(s, "Geselle", 1);
            sim.Tick();
            var ew = Einwohner(sim);
            var gaeste = new HashSet<int>();
            for (var stunde = 18; stunde < 22; stunde++)
            {
                Bis(sim, stunde);
                foreach (var e in ew.Am(Stadtort.Schaenke))
                {
                    Pruefe.Gleich(s.Name, e.Betrieb);
                    gaeste.Add(e.Nummer);
                }
            }
            Pruefe.Wahr(gaeste.Count >= 10, $"Am Abend sollten einige einkehren (waren {gaeste.Count})");
            Pruefe.Wahr(ew.Im(s.Name).All(e => e.Ort == Stadtort.Schaenke), "Im() passt zum Ort");
        }),

        ("Bürger-KI: Kandidaten der Brautschau und Einwohner bleiben über die Jahre deckungsgleich; Stadt hält ihre Größe", () =>
        {
            var (sim, _) = NeuePartie(startwert: 77);
            var w = sim.Wirtschaft!;
            var ew = Einwohner(sim);
            sim.Tick();
            var stadt = ew.Einwohner.Count(e => e.Herkunft == Herkunft.Stadt);
            var verstorben = 0;
            for (var jahr = 0; jahr < 12; jahr++)
            {
                Ticken(sim, Spielzeit.MinutenProTag);
                var kandidaten = w.Familien.Kandidaten.Select(x => x.Person.Nummer.ToString()).OrderBy(x => x).ToList();
                var alsEinwohner = ew.Einwohner.Where(e => e.Herkunft == Herkunft.Kandidat).Select(e => e.Quelle).OrderBy(x => x).ToList();
                Pruefe.Wahr(kandidaten.SequenceEqual(alsEinwohner), $"Jahr {sim.Zeit.Jahr}: Kandidaten und Einwohner weichen ab");
                Pruefe.Gleich(stadt, ew.Einwohner.Count(e => e.Herkunft == Herkunft.Stadt));
                verstorben += ew.VerstorbenImVorjahr;
            }
            Pruefe.Wahr(verstorben > 0, "In 12 Jahren sollte in einer Stadt mit 100 Einwohnern jemand sterben");
            Pruefe.Wahr(ew.Einwohner.Select(e => e.Nummer).Distinct().Count() == ew.Einwohner.Count, "Nummern bleiben eindeutig");
            Pruefe.Wahr(ew.Einwohner.Where(e => e.Herkunft == Herkunft.Stadt)
                          .All(e => e.Alter(sim.Zeit.Jahr) >= w.Familien.VolkDaten(e.Volk).Volljaehrig), "Gewöhnliche Einwohner sind volljährig");
        }),

        ("Bürger-KI: Gleicher Startwert = gleiche Einwohner und gleicher Tagesablauf", () =>
        {
            var (a, sa) = NeuePartie(startwert: 99);
            var (b, sb) = NeuePartie(startwert: 99);
            Ticken(a, Spielzeit.MinutenProTag + 600);
            Ticken(b, Spielzeit.MinutenProTag + 600);
            var ja = JsonNode.Parse(Speicherung.Speichern(a, sa))!["Wirtschaft"]!["Einwohner"]!.ToJsonString();
            var jb = JsonNode.Parse(Speicherung.Speichern(b, sb))!["Wirtschaft"]!["Einwohner"]!.ToJsonString();
            Pruefe.Gleich(ja, jb);
            var (c, sc) = NeuePartie(startwert: 100);
            Ticken(c, Spielzeit.MinutenProTag + 600);
            Pruefe.Wahr(ja != JsonNode.Parse(Speicherung.Speichern(c, sc))!["Wirtschaft"]!["Einwohner"]!.ToJsonString(),
                        "Anderer Startwert, andere Stadt");
        }),

        ("Bürger-KI: Speichern mitten in der Stunde und Laden – Einwohner laufen 2 Jahre gleich weiter", () =>
        {
            var (original, spieler) = NeuePartie(startwert: 4711);
            Ticken(original, Spielzeit.MinutenProTag + 437);
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(original, spieler));
            Pruefe.Gleich(Einwohner(original).Einwohner.Count, Einwohner(kopie).Einwohner.Count);
            Ticken(original, 2 * Spielzeit.MinutenProTag);
            Ticken(kopie, 2 * Spielzeit.MinutenProTag);
            Pruefe.Gleich(Speicherung.Speichern(original, spieler, new DateTime(2026, 10, 15)),
                          Speicherung.Speichern(kopie, kopieSpieler, new DateTime(2026, 10, 15)));
        }),

        ("Bürger-KI: Datenprüfung lehnt Orks als Einwohner und eine fehlende Handlung ab", () =>
        {
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["voelker"]!["Ork"] = 1), "kein Bürgerrecht");
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["handlungen"]!.AsObject().Remove("Beten")), "Beten fehlt");
        }),
    };
}
