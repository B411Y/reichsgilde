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

        ("Wohnhäuser: Haushalte zu 4–6 Personen; jeder Ratsherr im eigenen Haus; Bürgerkinder im Bürgerhaus", () =>
        {
            var (sim, _) = NeuePartie();
            sim.Tick();
            var ew = Einwohner(sim);
            var haeuser = ew.Wohnhaeuser.ToDictionary(h => h.Nummer);
            Pruefe.Wahr(ew.Einwohner.All(e => haeuser.ContainsKey(e.Wohnhaus)), "Jeder wohnt in einem Haus, das es gibt");
            var einfache = ew.Wohnhaeuser.Where(h => h.Art == Hausart.Wohnhaus).ToList();
            foreach (var h in einfache)
            {
                var stadt = ew.Bewohner(h.Nummer).Count(e => e.Herkunft == Herkunft.Stadt);
                Pruefe.Wahr(stadt is >= 1 and <= 6, $"Haus {h.Nummer}: {stadt} Bewohner aus der Stadt");
            }
            Pruefe.Wahr(einfache.Count(h => ew.Bewohner(h.Nummer).Count(e => e.Herkunft == Herkunft.Stadt) < 4) <= 1, "Nur das letzte Haus darf kleiner sein");
            foreach (var r in ew.Einwohner.Where(e => e.Herkunft == Herkunft.Ratsherr))
            {
                Pruefe.Gleich(Hausart.Ratsherrenhaus, haeuser[r.Wohnhaus].Art);
                Pruefe.Gleich(1, ew.Bewohner(r.Wohnhaus).Count());
            }
            foreach (var k in ew.Einwohner.Where(e => e.Herkunft == Herkunft.Kandidat))
                Pruefe.Gleich(k.Rolle == ew.Daten.RolleKandidatAbBuerger ? Hausart.Buergerhaus : Hausart.Wohnhaus, haeuser[k.Wohnhaus].Art);
            Pruefe.Wahr(ew.Einwohner.All(e => ew.ImHaus(e.Wohnhaus).Contains(e)), "Um 05:00 ist jeder in seinem eigenen Haus");
        }),

        ("Wege: Wer den Ort wechselt, läuft los – unterwegs zählt er nirgends, vor der nächsten Stunde ist er da", () =>
        {
            var (sim, _) = NeuePartie();
            sim.Tick();
            var ew = Einwohner(sim);
            var d = ew.Daten.Wege;
            Bis(sim, 8, 0);
            sim.Tick(); // 08:00 – alle haben geplant
            var start = sim.Zeit.VergangeneMinuten - 1; // Minute der Planung
            var gewechselt = ew.Einwohner.Where(e => e.Weg is { } w && w.Abmarsch >= start).ToList();
            Pruefe.Wahr(gewechselt.Count >= 20, $"Um 8 Uhr sollten viele losgehen (waren {gewechselt.Count})");
            foreach (var e in gewechselt)
            {
                var w = e.Weg!;
                Pruefe.Wahr(w.Abmarsch - start <= d.Verzoegerung, "Abmarsch kurz nach der vollen Stunde");
                Pruefe.Wahr(w.Ankunft - start < 60, "Ankunft vor der nächsten Entscheidung");
                Pruefe.Gleich(e.Aufenthalt, w.Nach);
                Pruefe.Wahr(w.Von != w.Nach, "Ein Weg führt woanders hin");
                var umland = w.Von.Ort == Stadtort.Umland || w.Nach.Ort == Stadtort.Umland;
                if (umland) Pruefe.Gleich(d.Umland, w.Dauer);
                else if (w.Von.Ort != Stadtort.Tor && w.Nach.Ort != Stadtort.Tor)
                    Pruefe.Wahr(Math.Abs(w.Dauer - d.Stadt) <= d.Streuung, $"Weg in der Stadt {w.Dauer} Minuten");
            }
            // Ein paar Minuten später: Unterwegs ist, wer zwischen Abmarsch und Ankunft liegt – und der zählt an keinem Ort.
            for (var i = 0; i < 9; i++) sim.Tick();
            var jetzt = sim.Zeit.VergangeneMinuten;
            var unterwegs = ew.Unterwegs.ToList();
            Pruefe.Wahr(unterwegs.Count > 0, "Um 08:10 ist jemand unterwegs");
            Pruefe.Wahr(unterwegs.All(e => e.Wo(jetzt) is null), "Unterwegs = kein Aufenthalt");
            var gezaehlt = Enum.GetValues<Stadtort>().Sum(o => ew.Am(o).Count());
            Pruefe.Gleich(ew.Einwohner.Count - unterwegs.Count, gezaehlt);
            Pruefe.Gleich(0.5, gewechselt[0].Weg!.Fortschritt((gewechselt[0].Weg!.Abmarsch + gewechselt[0].Weg!.Ankunft) / 2.0), "Halber Weg");
        }),

        ("Umland und Dienst: Hirten auf der Weide, Feldarbeiter auf dem Feld; Knechte und Mägde dienen in Bürger- und Ratsherrenhäusern", () =>
        {
            var (sim, _) = NeuePartie();
            sim.Tick();
            var ew = Einwohner(sim);
            var stellen = new HashSet<string?>();
            var dienst = 0;
            for (var stunde = 9; stunde < 19; stunde++)
            {
                Bis(sim, stunde, 50);
                foreach (var e in ew.Einwohner.Where(e => e.Handlung == Handlung.Arbeiten))
                {
                    var rolle = ew.Daten.Rollen[e.Rolle];
                    if (rolle.Arbeitsort == Stadtort.Umland)
                    {
                        Pruefe.Gleich(rolle.Stelle, e.Stelle);
                        stellen.Add(e.Stelle);
                    }
                    if (rolle.Dienst)
                    {
                        Pruefe.Wahr(e.Haus != e.Wohnhaus && ew.Wohnhaeuser.First(h => h.Nummer == e.Haus).Art != Hausart.Wohnhaus,
                                    $"{e.Name} dient in einem fremden, besseren Haus");
                        dienst++;
                    }
                }
                Pruefe.Wahr(ew.Einwohner.Where(e => e.Ort != Stadtort.Umland).All(e => e.Stelle is null), "Stelle nur im Umland");
            }
            Pruefe.Wahr(stellen.Count >= 2, $"Mehrere Stellen im Umland belegt ({string.Join(", ", stellen)})");
            Pruefe.Wahr(dienst > 0, "Jemand ist im Dienst");
        }),

        ("Alter Spielstand ohne Wohnhäuser: Häuser werden beim Laden verteilt, die Partie läuft weiter", () =>
        {
            var (sim, spieler) = NeuePartie();
            Ticken(sim, 200);
            var json = JsonNode.Parse(Speicherung.Speichern(sim, spieler))!;
            var stand = json["Wirtschaft"]!["Einwohner"]!.AsObject();
            stand.Remove("Haeuser");
            foreach (var e in stand["Einwohner"]!.AsArray())
            {
                var o = e!.AsObject();
                o.Remove("Haus"); o.Remove("Wohnhaus"); o.Remove("Weg"); o.Remove("Stelle");
            }
            var (geladen, _) = Speicherung.Laden(json.ToJsonString());
            var ew = Einwohner(geladen);
            Pruefe.Wahr(ew.Wohnhaeuser.Count > 0, "Häuser nachgerüstet");
            Pruefe.Wahr(ew.Einwohner.All(e => e.Wohnhaus > 0), "Jeder hat ein Haus");
            Ticken(geladen, Spielzeit.MinutenProTag);
            Pruefe.Wahr(ew.Einwohner.Any(e => e.Weg is not null), "Danach gehen sie wieder Wege");
        }),

        ("Bürger-KI: Datenprüfung lehnt Orks als Einwohner und eine fehlende Handlung ab", () =>
        {
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["voelker"]!["Ork"] = 1), "kein Bürgerrecht");
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["handlungen"]!.AsObject().Remove("Beten")), "Beten fehlt");
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["rollen"]!["Hirte"]!.AsObject().Remove("stelle")), "braucht eine stelle");
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["wege"]!["umland"] = 55), "unter einer Stunde");
        }),
    };
}
