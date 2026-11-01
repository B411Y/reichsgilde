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
    static readonly Lazy<Wirtschaftsdaten> OhneKopplung = new(() => Abgewandelt(j => j["buerger"]!.AsObject().Remove("kopplung")));

    /// <summary>Partie mit dem Spieler als Wirt und einem zweiten Haus mit Wirtshaus (Stufe 2) – beide mit vollem Lager.</summary>
    static (Simulation Sim, Konto Spieler, Betrieb Schaenke, Betrieb Wirtshaus) Wirte(Wirtschaftsdaten? daten = null, int startwert = 1400)
    {
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            daten ?? Daten.Value, new NeuePartie(Beruf: "Wirt", Rivale: KiCharakter.Wagemutig));
        var w = sim.Wirtschaft!;
        var krug = w.KontoEroeffnen("Haus Krug", startvermoegen: 20_000);
        var wirtshaus = w.BetriebEroeffnen("Wirtshaus Krug", Betriebsart.Wirtshaus, krug, startkarren: "");
        w.Einstellen(wirtshaus, "Geselle", 1);
        var schaenke = w.Betriebe.First(b => b.Konto == spieler && b.IstSchaenke);
        return (sim, spieler, schaenke, wirtshaus);
    }

    static void Auffuellen(Simulation sim)
    {
        foreach (var b in sim.Wirtschaft!.Betriebe.Where(b => b.IstSchaenke))
            b.Einlagern("Dünnbier", Math.Max(0, 300 - b.ImLager("Dünnbier")));
    }

    static void JahrMitVollemLager(Simulation sim)
    {
        Auffuellen(sim);
        Ticken(sim, Spielzeit.MinutenProTag);
    }

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

        ("Bürger-KI: Ohne Kopplung keine Wirkung auf die Wirtschaft – mit und ohne Einwohner läuft die Partie 2 Jahre gleich", () =>
        {
            var (mit, spielerMit) = NeuePartie(OhneKopplung.Value, startwert: 4711);
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

        ("Schänke: Ohne Kopplung bleibt alles wie vorher – auch mit Schänken (Gäste wie ohne Einwohner)", () =>
        {
            var (mit, sm, _, _) = Wirte(OhneKopplung.Value, startwert: 4711);
            var (ohne, so, _, _) = Wirte(OhneBuerger.Value, startwert: 4711);
            for (var j = 0; j < 2; j++) { JahrMitVollemLager(mit); JahrMitVollemLager(ohne); }
            Pruefe.Wahr(Einwohner(mit).Einwohner.Count > 0, "Einwohner da");
            Pruefe.Gleich(OhneEinwohner(ohne, so), OhneEinwohner(mit, sm));
        }),

        ("Schänke: Die Gäste einer Stunde folgen den Einwohnern, die in der Schänke sitzen", () =>
        {
            var (sim, _, schaenke, wirtshaus) = Wirte();
            var w = sim.Wirtschaft!;
            var ew = Einwohner(sim);
            JahrMitVollemLager(sim); // erstes Jahr: Einschwingen
            JahrMitVollemLager(sim); // zweites Jahr: das „Übliche“ je Uhrzeit lernen
            Auffuellen(sim);
            Bis(sim, 20, 59);
            var drinS = ew.Einkehrende(schaenke);
            var drinW = ew.Einkehrende(wirtshaus);
            var erwartet = ew.EinkehrErwartet(20);
            var vorherS = w.Gastwirtschaft.DiesesJahr(schaenke).Bewirtet;
            var vorherW = w.Gastwirtschaft.DiesesJahr(wirtshaus).Bewirtet;
            var gaeste = w.Gastwirtschaft.Daten.GaesteJeJahr * w.Gastwirtschaft.Daten.AbendGewicht /
                         (6 + 4 * w.Gastwirtschaft.Daten.AbendGewicht); // 12–17 Uhr einfach, 18–21 Uhr doppelt
            var kd = ew.Daten.Kopplung;
            var faktor = Math.Clamp((drinS + drinW) / erwartet, 1 - kd.Spanne, 1 + kd.Spanne);
            Ticken(sim, 2); // 21:00 – die Gäste der Stunde 20–21 kehren ein
            var neuS = w.Gastwirtschaft.DiesesJahr(schaenke).Bewirtet - vorherS;
            var neuW = w.Gastwirtschaft.DiesesJahr(wirtshaus).Bewirtet - vorherW;
            Pruefe.Wahr(drinS + drinW > 0, "Abends sitzen Einwohner in den Schänken");
            // Ganze Gäste: Bruchteile werden gesammelt – je Schänke höchstens einer mehr oder weniger als der Anteil.
            Pruefe.Wahr(Math.Abs(neuS - gaeste * faktor * drinS / (drinS + drinW)) < 1.01, $"Schänke: {neuS} Gäste bei {drinS} von {drinS + drinW} Einwohnern");
            Pruefe.Wahr(Math.Abs(neuW - gaeste * faktor * drinW / (drinS + drinW)) < 1.01, $"Wirtshaus: {neuW} Gäste bei {drinW} Einwohnern");
        }),

        ("Schänke: Einwohner verteilen sich als Quote nach der Anziehung – das Wirtshaus (Stufe 2) bekommt anderthalbmal so viele", () =>
        {
            var (sim, _, schaenke, wirtshaus) = Wirte();
            var ew = Einwohner(sim);
            int s = 0, w = 0;
            for (var j = 0; j < 3; j++)
            {
                Auffuellen(sim);
                for (var m = 0; m < Spielzeit.MinutenProTag; m++)
                {
                    sim.Tick();
                    if (sim.Zeit.MinuteImTag % 60 != 59) continue;
                    // Seit Datenversion 66 mit Sitzplätzen (20 und 30): Ist eine voll, sitzen die Übrigen in der anderen – das zählt hier nicht.
                    var g = sim.Wirtschaft!.Gastwirtschaft;
                    if (ew.Einkehrende(schaenke) >= g.Sitzplaetze(schaenke) || ew.Einkehrende(wirtshaus) >= g.Sitzplaetze(wirtshaus)) continue;
                    s += ew.Einkehrende(schaenke);
                    w += ew.Einkehrende(wirtshaus);
                }
            }
            var a = sim.Wirtschaft!.Gastwirtschaft;
            var soll = a.Anziehung(wirtshaus) / (a.Anziehung(schaenke) + a.Anziehung(wirtshaus));
            var ist = w / (double)(s + w);
            Pruefe.Wahr(Math.Abs(ist - soll) < 0.02, $"Anteil Wirtshaus {ist:P1}, nach Anziehung {soll:P1}");
            Pruefe.Wahr(ew.Einwohner.Where(e => e.Ort == Stadtort.Schaenke).All(e => e.Betrieb == e.Stammschaenke), "Jeder bleibt bei seiner Schänke des Tages");
        }),

        ("Schänke: Über 10 Jahre kommen im Mittel so viele Gäste wie ohne Kopplung – mit Schwankung, höchstens ± Spanne", () =>
        {
            var (sim, _, schaenke, wirtshaus) = Wirte(startwert: 77);
            var g = sim.Wirtschaft!.Gastwirtschaft;
            var jahre = new List<int>();
            for (var j = 0; j < 11; j++)
            {
                JahrMitVollemLager(sim);
                // Alle Schänken der Stadt – seit PR #60 kann auch ein KI-Haus eine führen.
                if (j > 0) jahre.Add(sim.Wirtschaft!.Betriebe.Where(b => b.IstSchaenke).Sum(b => g.Vorjahr(b) is { } v ? v.Bewirtet + v.Abgewiesen : 0));
            }
            var soll = g.Daten.GaesteJeJahr;
            var spanne = Einwohner(sim).Daten.Kopplung.Spanne;
            Pruefe.Wahr(Math.Abs(jahre.Average() - soll) < soll * 0.06, $"Ø {jahre.Average():0.0} Gäste statt {soll}");
            Pruefe.Wahr(jahre.All(x => x >= soll * (1 - spanne) - 2 && x <= soll * (1 + spanne) + 2), $"Jahre: {string.Join(", ", jahre)}");
            Pruefe.Wahr(jahre.Distinct().Count() > 1, "Die Gäste schwanken von Jahr zu Jahr");
        }),

        ("Schänke: Speichern und Laden mit Kopplung – Gäste, Stammschänke und das Übliche laufen gleich weiter", () =>
        {
            var (original, spieler, _, _) = Wirte(startwert: 4711);
            JahrMitVollemLager(original);
            Auffuellen(original);
            Ticken(original, 560); // 14:20 – mitten am Nachmittag
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(original, spieler));
            Ticken(original, 2 * Spielzeit.MinutenProTag);
            Ticken(kopie, 2 * Spielzeit.MinutenProTag);
            Pruefe.Gleich(Speicherung.Speichern(original, spieler, new DateTime(2026, 10, 16)),
                          Speicherung.Speichern(kopie, kopieSpieler, new DateTime(2026, 10, 16)));
        }),

        ("Kirche: Gläubige und Kollekte der Messe × (Gebetsstunden bis zur Messe ÷ üblich), begrenzt auf 1 ± Spanne", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Beruf: "Prediger", Rivale: KiCharakter.Wagemutig));
            var w = sim.Wirtschaft!;
            var ew = Einwohner(sim);
            var kirche = w.Betriebe.Single(b => b.Konto == spieler && w.Kirchenwesen.IstKirche(b));
            Ticken(sim, 2 * Spielzeit.MinutenProTag); // erste Tage: Einschwingen, dann das Übliche lernen (Faktor 1)
            Pruefe.Wahr(ew.MesseErwartet > 0, "Nach dem zweiten Tag ist das Übliche bekannt");
            var kd = ew.Daten.Kopplung;
            for (var tag = 0; tag < 4; tag++)
            {
                Bis(sim, w.Kirchenwesen.Daten.MesseUm - 1, 59);
                var erwartet = ew.MesseErwartet;
                var soll = w.Kirchenwesen.Verteilen(w.Kirchenwesen.Kirchen)[kirche];
                var vorher = w.Kirchenwesen.DiesesJahr(kirche);
                Ticken(sim, 2); // 10:00 – die Messe wird abgerechnet
                var nachher = w.Kirchenwesen.DiesesJahr(kirche);
                var faktor = (nachher.Glaeubige - vorher.Glaeubige) / soll.Glaeubige;
                Pruefe.Wahr(faktor >= 1 - kd.Spanne - 1e-9 && faktor <= 1 + kd.Spanne + 1e-9, $"Faktor {faktor:0.00}");
                Pruefe.Ungefaehr(soll.Kollekte * faktor, nachher.Kollekte - vorher.Kollekte, 1e-6);
                Pruefe.Wahr(Math.Abs(ew.MesseErwartet - erwartet) > 0 || faktor == 1, "Das Übliche passt sich an");
                Ticken(sim, Spielzeit.MinutenProTag - 300);
            }
        }),

        ("Kirche: Ohne Kopplung bleibt die Messe genau wie vorher", () =>
        {
            var ohne = Abgewandelt(j => j["buerger"]!["kopplung"]!["kirche"] = 0);
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
                ohne, new NeuePartie(Beruf: "Prediger"));
            var w = sim.Wirtschaft!;
            var kirche = w.Betriebe.Single(b => b.Konto == spieler && w.Kirchenwesen.IstKirche(b));
            for (var j = 0; j < 3; j++)
            {
                var soll = w.Kirchenwesen.Verteilen(w.Kirchenwesen.Kirchen)[kirche];
                Ticken(sim, Spielzeit.MinutenProTag);
                Pruefe.Ungefaehr(soll.Glaeubige, w.Kirchenwesen.Vorjahr(kirche)!.Glaeubige, 1e-6);
            }
        }),

        ("Kirche: Betende gehen in eine Kirche ihrer Konfession (ohne eigene in eine fremde), Kirchen als Quote nach Anziehung", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Beruf: "Prediger"));
            var w = sim.Wirtschaft!;
            var ew = Einwohner(sim);
            var gezaehlt = 0;
            for (var stunde = 6; stunde < 20; stunde++)
            {
                Bis(sim, stunde, 59);
                var kirchen = w.Kirchenwesen.Kirchen.ToList();
                foreach (var e in ew.Einwohner.Where(e => e.Handlung == Handlung.Beten))
                {
                    var b = kirchen.Single(k => k.Name == e.Betrieb);
                    var eigene = kirchen.Where(k => w.Glaube.KonfessionVon(k) is not { } kk || kk == e.Glaube).ToList();
                    Pruefe.Wahr(eigene.Count == 0 || eigene.Contains(b), $"{e.Name} ({e.Glaube}) betet in {b.Name}");
                    gezaehlt++;
                }
            }
            Pruefe.Wahr(gezaehlt > 20, $"Über den Tag beten einige ({gezaehlt})");
        }),

        ("Kirche: Speichern und Laden mit Kopplung – Gebetsstunden des Morgens und das Übliche laufen gleich weiter", () =>
        {
            var (original, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 4711, Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Beruf: "Prediger", Rivale: KiCharakter.Wagemutig));
            Ticken(original, Spielzeit.MinutenProTag + 250); // 09:10 – mitten in den Gebetsstunden vor der Messe
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(original, spieler));
            Ticken(original, 2 * Spielzeit.MinutenProTag);
            Ticken(kopie, 2 * Spielzeit.MinutenProTag);
            Pruefe.Gleich(Speicherung.Speichern(original, spieler, new DateTime(2026, 10, 16)),
                          Speicherung.Speichern(kopie, kopieSpieler, new DateTime(2026, 10, 16)));
        }),

        ("Markt: Die Einkäufe der Einwohner verschieben die Nachfrage der Fertigwaren – Rohstoffe und Zwischenwaren bleiben fest", () =>
        {
            var (sim, _) = NeuePartie();
            var w = sim.Wirtschaft!;
            var ew = Einwohner(sim);
            var kd = ew.Daten.Kopplung;
            Ticken(sim, Spielzeit.MinutenProTag);
            Pruefe.Gleich(1.0, ew.Marktfaktor, "Erster Tag: Einschwingen, Faktor 1");
            var faktoren = new HashSet<double>();
            for (var m = 0; m < 3 * Spielzeit.MinutenProTag; m++)
            {
                sim.Tick();
                if (sim.Zeit.MinuteImTag % 60 != 30) continue;
                var f = ew.Marktfaktor;
                faktoren.Add(Math.Round(f, 6));
                Pruefe.Wahr(f >= 1 - kd.MarktSpanne - 1e-9 && f <= 1 + kd.MarktSpanne + 1e-9, $"Faktor {f}");
                foreach (var stand in w.Markt.Staende)
                    Pruefe.Ungefaehr(kd.MarktTypen.Contains(stand.Typ) ? f * (1 - kd.Markt + kd.Markt * ew.Warenkorbfaktor(stand.Ware)) : 1.0, // dazu der Warenkorb
                                     stand.NachfrageFaktor, 1e-12);
            }
            Pruefe.Wahr(faktoren.Count > 3, "Der Faktor bewegt sich über die Tage");
            var ring = w.Markt["Silberring"];
            var a = w.Daten.Allgemein;
            var nachfrage = w.Daten.Waren["Silberring"].Nachfrage;
            var soll = Math.Clamp(Math.Pow(nachfrage * ring.NachfrageFaktor / ring.Gesamtangebot, a.Elastizitaet), a.PreisMin, a.PreisMax);
            Pruefe.Ungefaehr(soll * ring.Grundpreis, ring.Preis, 1e-9);
            Pruefe.Ungefaehr(nachfrage * w.Daten.Waren["Silberring"].NpcAnteil + ring.SpielerAngebotImJahr, ring.Gesamtangebot, 1e-9);
        }),

        ("Markt: Ohne Kopplung bleibt die Nachfrage aller Waren genau wie in den Daten", () =>
        {
            var (sim, _) = NeuePartie(Abgewandelt(j => j["buerger"]!["kopplung"]!["markt"] = 0));
            Ticken(sim, 3 * Spielzeit.MinutenProTag);
            Pruefe.Wahr(sim.Wirtschaft!.Markt.Staende.All(s => s.NachfrageFaktor == 1.0), "Faktor 1 überall");
        }),

        ("Markt: Nach dem Laden gilt sofort derselbe Faktor – auch mitten in der Stunde", () =>
        {
            var (original, spieler) = NeuePartie(startwert: 4711);
            Ticken(original, 3 * Spielzeit.MinutenProTag + 431);
            var (kopie, _) = Speicherung.Laden(Speicherung.Speichern(original, spieler));
            foreach (var stand in original.Wirtschaft!.Markt.Staende)
                Pruefe.Gleich(stand.NachfrageFaktor, kopie.Wirtschaft!.Markt[stand.Ware].NachfrageFaktor, stand.Ware);
            Pruefe.Wahr(original.Wirtschaft!.Markt["Silberring"].NachfrageFaktor != 1.0, "Faktor sollte nach drei Tagen von 1 abweichen");
        }),

        ("Taschendiebe: Beute × (Passanten dieser Stunde ÷ üblich zu dieser Uhrzeit), begrenzt auf 1 ± Spanne", () =>
        {
            foreach (var mitKopplung in new[] { true, false })
            {
                var daten = mitKopplung ? Daten.Value : Abgewandelt(j => j["buerger"]!["kopplung"]!["diebe"] = 0);
                var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
                    daten, new NeuePartie(Beruf: "Dieb"));
                var w = sim.Wirtschaft!;
                var ew = Einwohner(sim);
                var nest = w.Betriebe.Single(b => b.Konto == spieler && w.Diebe.IstDiebesbetrieb(b));
                Ticken(sim, 2 * Spielzeit.MinutenProTag); // Einschwingen und das Übliche lernen
                var kd = ew.Daten.Kopplung;
                var abweichend = 0;
                for (var stunde = 9; stunde < 19; stunde++)
                {
                    Bis(sim, stunde, 59);
                    Ticken(sim, 1);                       // die letzte Minute der Stunde wird gearbeitet
                    var minuten = nest.Fortschritt;
                    var erwartet = ew.PassantenErwartet(stunde);
                    var faktor = mitKopplung && erwartet > 0 ? Math.Clamp(ew.Passanten.Count() / erwartet, 1 - kd.Spanne, 1 + kd.Spanne) : 1;
                    var vorher = w.Diebe.DiesesJahr(spieler).Beute;
                    Ticken(sim, 1);                       // volle Stunde: die Taschendiebe liefern ab
                    var beute = w.Diebe.DiesesJahr(spieler).Beute - vorher;
                    Pruefe.Ungefaehr(minuten * w.Diebe.WertJeMinute(nest.Stufe) * faktor, beute, 1e-6);
                    if (Math.Abs(faktor - 1) > 0.01) abweichend++;
                }
                Pruefe.Wahr(mitKopplung ? abweichend > 0 : abweichend == 0, $"{(mitKopplung ? "Mit" : "Ohne")} Kopplung: {abweichend} Stunden mit Faktor ≠ 1");
            }
        }),

        ("Taschendiebe: Der Bestohlene ist ein Passant – ohne Kopplung bleibt es beim namenlosen Bürger", () =>
        {
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Beruf: "Dieb"));
            var ew = Einwohner(sim);
            Ticken(sim, Spielzeit.MinutenProTag);
            Bis(sim, 9, 0);
            Ticken(sim, 1);
            Bis(sim, 10, 0);                            // volle Stunde: Passanten der Stunde 9–10
            var passanten = ew.Passanten.ToList();
            Pruefe.Wahr(passanten.Count > 0, "Um 9 Uhr ist jemand draußen");
            Pruefe.Wahr(passanten.All(e => e.Ort is Stadtort.Markt or Stadtort.Gasse || e.Weg is not null), "Passanten sind am Markt, in der Gasse oder unterwegs");
            var opfer = ew.Bestohlen();
            Pruefe.Wahr(opfer is not null && passanten.Contains(opfer), "Ein Passant wird bestohlen");
            var vorher = opfer!.Wert(Beduerfnis.Versorgung);
            Pruefe.Wahr(vorher >= ew.Daten.Kopplung.Bestohlen || vorher == 100, "Die Versorgung ist dringender geworden");

            var (ohne, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
                Abgewandelt(j => j["buerger"]!["kopplung"]!["diebe"] = 0), new NeuePartie(Beruf: "Dieb"));
            Ticken(ohne, Spielzeit.MinutenProTag + 300);
            Pruefe.Wahr(Einwohner(ohne).Bestohlen() is null, "Ohne Kopplung kein namentliches Opfer");
        }),

        ("Taschendiebe: Speichern und Laden mit Kopplung – Beute und das Übliche laufen gleich weiter", () =>
        {
            var (original, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 4711, Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Beruf: "Dieb", Rivale: KiCharakter.Wagemutig));
            Ticken(original, 2 * Spielzeit.MinutenProTag + 407);
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(original, spieler));
            Ticken(original, 2 * Spielzeit.MinutenProTag);
            Ticken(kopie, 2 * Spielzeit.MinutenProTag);
            Pruefe.Gleich(Speicherung.Speichern(original, spieler, new DateTime(2026, 10, 16)),
                          Speicherung.Speichern(kopie, kopieSpieler, new DateTime(2026, 10, 16)));
        }),

        ("Bürger-KI: Datenprüfung lehnt Orks als Einwohner und eine fehlende Handlung ab", () =>
        {
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["voelker"]!["Ork"] = 1), "kein Bürgerrecht");
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["handlungen"]!.AsObject().Remove("Beten")), "Beten fehlt");
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["rollen"]!["Hirte"]!.AsObject().Remove("stelle")), "braucht eine stelle");
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["wege"]!["umland"] = 55), "unter einer Stunde");
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["kopplung"]!["schaenke"] = 1.5), "kopplung.schaenke");
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["kopplung"]!["kirche"] = -0.1), "kopplung.kirche");
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["kopplung"]!["marktSpanne"] = 1.0), "marktSpanne");
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["kopplung"]!["diebe"] = 2), "kopplung.diebe");
            Pruefe.WirftMitMeldung(() => Abgewandelt(j => j["buerger"]!["handlungen"]!["Einkehren"]!["von"] = 13), "oeffnetUm");
        }),
    };
}
