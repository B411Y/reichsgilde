using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests für das Personal wie in Gilde 1 (PR #224): Laufbahnen mit drei Stufen, nur Anfänger einstellbar, Erfahrung aus Arbeitsstunden,
/// Beförderung von Hand mit Brief, höchstens ein Vorarbeiter mit einer Eigenschaft aus drei gezogenen, Name und Aussehen jedes Arbeiters.
/// </summary>
static class PersonalTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null, Fronstatut statut = Fronstatut.FreieStadt, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert, StartStatut = statut },
            wirtschaftsdaten: daten ?? Daten.Value);

    /// <summary>Die Daten aus wirtschaft.json, im Abschnitt „personal“ verändert.</summary>
    static Wirtschaftsdaten PersonalMit(Action<JsonNode> aendern)
    {
        var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()),
            documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!;
        aendern(knoten["personal"]!);
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    static (Stadtwirtschaft W, Konto K, Betrieb G) Giesserei(Simulation sim, double vermoegen = 20_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: vermoegen);
        var g = w.BetriebEroeffnen("Gießerei am Weinmarkt", Betriebsart.Giesserei, k, startkarren: "");
        g.Einlagern("Eisen", 40);
        g.Einlagern("Silber", 40);
        return (w, k, g);
    }

    static void JahreSimulieren(Simulation sim, int jahre)
    {
        var ziel = sim.Zeit.Jahr + jahre;
        while (sim.Zeit.Jahr < ziel) sim.Tick();
    }

    static Befehlsergebnis Ok(Stadtwirtschaft w, Konto k, Befehl befehl)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(e.Erfolg, $"{befehl.GetType().Name} abgelehnt: {e.Meldung}");
        return e;
    }

    static void Abgelehnt(Stadtwirtschaft w, Konto k, Befehl befehl, string textInMeldung)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains(textInMeldung), $"{befehl.GetType().Name}: erwartet abgelehnt mit „{textInMeldung}“, war {e.Erfolg}: {e.Meldung}");
    }

    static List<string> Fehlerliste(Func<Wirtschaftsdaten> laden)
    {
        try { laden(); return new(); }
        catch (WirtschaftsdatenFehler f) { return f.Fehler.ToList(); }
    }

    /// <summary>Ein Arbeiter mit genug Erfahrung für die nächste Stufe (wie nach drei bzw. acht Jahren).</summary>
    static void ErfahrungFuer(Stadtwirtschaft w, Arbeiter a, int stufe) => a.Erfahrung = w.Personal.Daten!.Schwelle(stufe);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Personal: Daten – vier Laufbahnen mit drei Stufen und eine Schreibstube; Lehrling 120, Geselle 220, Vorarbeiter 300; Erfahrung 24 und 84, Briefe 150 und 400", () =>
        {
            var d = Daten.Value;
            var p = d.Personal!;
            Pruefe.Gleich("Diebe Wache Schreibstube Umland Werkstatt", string.Join(" ", p.Laufbahnen.Select(l => l.Name)));
            Pruefe.Gleich("Lehrling Geselle Vorarbeiter", string.Join(" ", d.Laufbahn(Betriebsart.Giesserei)!.Stufen));
            Pruefe.Gleich("Knecht Altknecht Großknecht", string.Join(" ", d.Laufbahn(Betriebsart.Mine)!.Stufen));
            Pruefe.Gleich("Taschendieb Dieb Meisterdieb", string.Join(" ", d.Laufbahn(Betriebsart.Schmugglerloch)!.Stufen));
            Pruefe.Gleich("Rekrut Gefreiter Feldwebel", string.Join(" ", d.Laufbahn(Betriebsart.Wachhaus)!.Stufen));
            Pruefe.Gleich("Rekrut", d.Laufbahn(Betriebsart.Wachturm)!.Anfaenger); // der Wachturm im Umland gehört zur Wache
            Pruefe.Gleich("Schreibstube", d.Laufbahn(Betriebsart.Wechselstube)!.Name);
            Pruefe.Gleich("Knecht", d.Laufbahn(Betriebsart.Klosterhof)!.Anfaenger);
            Pruefe.Gleich("120 220 300", string.Join(" ", new[] { "Lehrling", "Geselle", "Vorarbeiter" }.Select(k => d.Arbeitskraefte[k].Kosten)));
            Pruefe.Gleich("80 150 205", string.Join(" ", new[] { "Knecht", "Altknecht", "Großknecht" }.Select(k => d.Arbeitskraefte[k].Kosten)));
            Pruefe.Gleich("24 84", string.Join(" ", p.Erfahrung));
            Pruefe.Gleich("150 400", string.Join(" ", p.Briefe));
            Pruefe.Gleich(1, p.StufeVon("Lehrling"));
            Pruefe.Gleich(3, p.StufeVon("Feldwebel"));
            Pruefe.Gleich(0, p.StufeVon("Ork"));
            Pruefe.Wahr(!d.Arbeitskraefte.ContainsKey("Bergmann"), "Bergmann heißt jetzt Altknecht");
            // Je Laufbahn drei Gruppen, die Werkstatt mit den Vorschlägen des Nutzers – Werte seit Balancing 3 so, dass keine Gruppe immer gewinnt.
            var werkstatt = d.Laufbahn(Betriebsart.Giesserei)!;
            Pruefe.Gleich("Ware Führung Betrieb", string.Join(" ", werkstatt.Eigenschaften.Select(g => g.Gruppe)));
            Pruefe.Ungefaehr(0.9, werkstatt.Eigenschaft("Flink")!.Wirkung.ZeitFaktor, 1e-12);
            Pruefe.Ungefaehr(1.06, werkstatt.Eigenschaft("GuterRuf")!.Wirkung.PreisFaktor, 1e-12);
            Pruefe.Ungefaehr(0.92, werkstatt.Eigenschaft("Sparsam")!.Wirkung.RohstoffFaktor, 1e-12);
            Pruefe.Gleich(90, werkstatt.Eigenschaft("Fruehaufsteher")!.Wirkung.ArbeitsminutenPlus);
            Pruefe.Ungefaehr(0.85, werkstatt.Eigenschaft("Genuegsam")!.Wirkung.LohnFaktor, 1e-12);
            foreach (var l in p.Laufbahnen) Pruefe.Gleich(3, l.Eigenschaften.Count, l.Name);
        }),

        ("Personal: Daten werden geprüft – drei Stufen, steigende Erfahrung, bekannte Arbeitskräfte, Wirkungen im Bereich, letzte Laufbahn ohne Bedingung", () =>
        {
            var fehler = Fehlerliste(() => PersonalMit(p =>
            {
                p["erfahrung"] = new JsonArray(50, 20);
                p["laufbahnen"]![4]!["stufen"] = new JsonArray("Lehrling", "Geselle");
                p["laufbahnen"]![0]!["stufen"]![1] = "Spitzbube";
                p["laufbahnen"]![4]!["eigenschaften"]![0]!["stuecke"]![0]!["wirkung"]!["zeitFaktor"] = 1.5;
                p["laufbahnen"]![4]!["eigenschaften"]![0]!["stuecke"]![1]!["wirkung"]!["preisFaktor"] = 3;
                p["laufbahnen"]![4]!["ort"] = "Werkstatt";
            }));
            var alle = string.Join(" | ", fehler);
            foreach (var text in new[] { "personal.erfahrung", "genau drei", "„Spitzbube“", "zeitFaktor", "preisFaktor", "letzte Laufbahn" })
                Pruefe.Wahr(alle.Contains(text), $"„{text}“ fehlt in: {alle}");
        }),

        ("Personal: Eingestellt werden nur Anfänger – Lehrling in der Werkstatt, Knecht im Umland, Taschendieb, Rekrut; Orks nach Statut; entlassen geht immer", () =>
        {
            var sim = NeueSimulation(statut: Fronstatut.Bestienfron);
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 50_000);
            var g = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k, startkarren: "");
            var m = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k, startkarren: "");
            var d = w.BetriebEroeffnen("Loch", Betriebsart.Schmugglerloch, k, startkarren: "");
            var h = w.BetriebEroeffnen("Wache", Betriebsart.Wachhaus, k, startkarren: "");
            Abgelehnt(w, k, new PersonalSetzen(g.Name, "Geselle", 1), "nur Lehrlinge");
            Abgelehnt(w, k, new ArbeiterEinstellen(g.Name, "Vorarbeiter"), "nur Lehrlinge");
            Abgelehnt(w, k, new PersonalSetzen(g.Name, "Knecht", 1), "arbeitet nicht in einer Gießerei");
            Ok(w, k, new PersonalSetzen(g.Name, "Lehrling", 2));
            Abgelehnt(w, k, new PersonalSetzen(m.Name, "Altknecht", 1), "nur Knechte");
            Ok(w, k, new PersonalSetzen(m.Name, "Knecht", 1));
            Ok(w, k, new PersonalSetzen(m.Name, "Ork", 2)); // Unfreie und Orks haben keine Stufen – Statut B erlaubt sie im Umland
            Abgelehnt(w, k, new PersonalSetzen(d.Name, "Lehrling", 1), "hier lernen Taschendiebe");
            Ok(w, k, new ArbeiterEinstellen(d.Name, "Taschendieb"));
            Abgelehnt(w, k, new ArbeiterEinstellen(h.Name, "Gefreiter"), "nur Rekruten");
            Ok(w, k, new ArbeiterEinstellen(h.Name, "Rekrut"));
            w.Einstellen(g, "Geselle", 0);
            w.Einstellen(g, "Lehrling", 1);
            w.Einstellen(g, "Geselle", 1); // die Stadt selbst (Partieaufbau, Tests) darf jede Kraft setzen
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 0)); // entlassen geht immer
            Pruefe.Gleich(1, g.Belegt);
        }),

        ("Personal: Die Partie beginnt mit Anfängern – Spieler und KI-Häuser mit zwei Lehrlingen, die Grube („Schwer“) mit drei Knechten", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { StartStatut = Fronstatut.FreieStadt, Schwierigkeit = Schwierigkeit.Schwer },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var eigene = w.Betriebe.Single(b => b.Konto == spieler);
            Pruefe.Gleich(2, eigene.Belegung.GetValueOrDefault("Lehrling"));
            foreach (var b in w.Betriebe.Where(b => b.Konto != spieler && b.Belegt > 0))
                Pruefe.Wahr(b.Arbeiter.All(a => w.Personal.IstAnfaenger(a)), $"{b.Name}: {string.Join(", ", b.Belegung.Keys)}");
            Pruefe.Gleich(3, w.Betriebe.Single(b => b.Name == "Grube Eisenbart").Belegung.GetValueOrDefault("Knecht"));
        }),

        ("Personal: Erfahrung aus Arbeitsstunden – ein Lehrling sammelt im Jahr 0,67 × 12 ≈ 8 Stunden, ein Geselle 12; bei Stillstand nichts", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            w.Einstellen(g, "Lehrling", 1);
            w.Einstellen(g, "Geselle", 1);
            g.Einlagern("Eisen", 400); // genug Zutaten: kein Stillstand
            g.Einlagern("Silber", 400);
            JahreSimulieren(sim, 1);
            var lehrling = g.Arbeiter.Single(a => a.Arbeitskraft == "Lehrling");
            var geselle = g.Arbeiter.Single(a => a.Arbeitskraft == "Geselle");
            Pruefe.Ungefaehr(0.67 * 12, lehrling.Erfahrung, 0.15);
            Pruefe.Ungefaehr(12, geselle.Erfahrung, 0.15);
            Pruefe.Ungefaehr(0.67 * 12, w.Personal.ErfahrungJeJahr(g, lehrling), 1e-9);
            Pruefe.Ungefaehr((24 - lehrling.Erfahrung) / (0.67 * 12), w.Personal.JahreBisNaechste(g, lehrling)!.Value, 1e-6);

            // Ohne Zutaten steht die Werkstatt still – dann lernt niemand mehr etwas (nur das angefangene erste Stück zählt).
            var sim2 = NeueSimulation();
            var w2 = sim2.Wirtschaft!;
            var k2 = w2.KontoEroeffnen("Haus Leer", startvermoegen: 20_000);
            var leer = w2.BetriebEroeffnen("Gießerei ohne Eisen", Betriebsart.Giesserei, k2, startkarren: "");
            w2.Einstellen(leer, "Lehrling", 1);
            JahreSimulieren(sim2, 1);
            var einStueck = w2.Daten.Waren[leer.Produkt].Zeit / 60.0;
            Pruefe.Wahr(leer.Arbeiter[0].Erfahrung <= einStueck + 1e-9, $"{leer.Arbeiter[0].Erfahrung} Stunden ohne Zutaten (ein Stück: {einStueck})");
        }),

        ("Personal: Befördern – erst mit 24 Arbeitsstunden Erfahrung, gegen 150 Gulden Gesellenbrief und einen Aktionspunkt; danach Gesellenlohn", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            Ok(w, k, new PersonalSetzen(g.Name, "Lehrling", 2));
            var a = g.ArbeiterNr(1)!;
            Abgelehnt(w, k, new ArbeiterBefoerdern(g.Name, 1), "braucht 24 Arbeitsstunden Erfahrung");
            Pruefe.Wahr(w.Werkstaetten.Arbeiterliste(g)[0].BefoerderungVerboten!.Contains("noch etwa 3 Jahre"), w.Werkstaetten.Arbeiterliste(g)[0].BefoerderungVerboten!);
            ErfahrungFuer(w, a, 2);
            var vorher = k.Vermoegen;
            var ap = k.Aktionspunkte.Vorrat;
            Pruefe.Gleich(1, w.ApKosten(new ArbeiterBefoerdern(g.Name, 1)));
            Ok(w, k, new ArbeiterBefoerdern(g.Name, 1));
            Pruefe.Gleich("Geselle", a.Arbeitskraft);
            Pruefe.Ungefaehr(150, vorher - k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(150, g.Laufend.Personal, 1e-9); // der Brief ist Personalaufwand des Betriebs
            Pruefe.Gleich(ap - 1, k.Aktionspunkte.Vorrat);
            Pruefe.Ungefaehr(220, g.LohnJeArbeiter(a.Arbeitskraft), 1e-9);
            var zeile = w.Werkstaetten.Arbeiterliste(g)[0];
            Pruefe.Gleich(2, zeile.Stufe);
            Pruefe.Gleich("Vorarbeiter", zeile.NaechsteStufe);
            Pruefe.Gleich(400.0, zeile.BriefKosten);
            Pruefe.Gleich(84.0, zeile.ErfahrungNaechste);
            Abgelehnt(w, k, new ArbeiterBefoerdern(g.Name, 1), "braucht 84 Arbeitsstunden Erfahrung");
        }),

        ("Personal: Vorarbeiter – höchstens einer je Betrieb; drei Eigenschaften (je eine aus Ware, Führung, Betrieb), fest für die Person; Wahl ohne Kosten", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            w.Einstellen(g, "Geselle", 2);
            foreach (var a in g.Arbeiter) ErfahrungFuer(w, a, 3);
            Ok(w, k, new ArbeiterBefoerdern(g.Name, 1));
            var v = g.ArbeiterNr(1)!;
            Pruefe.Gleich("Vorarbeiter", v.Arbeitskraft);
            Pruefe.Ungefaehr(300, g.LohnJeArbeiter("Vorarbeiter"), 1e-9);
            Abgelehnt(w, k, new ArbeiterBefoerdern(g.Name, 2), "hat schon einen Vorarbeiter");
            var wahl = v.EigenschaftWahl!;
            Pruefe.Gleich(3, wahl.Count);
            var l = w.Personal.Laufbahn(g)!;
            Pruefe.Gleich("Ware Führung Betrieb", string.Join(" ", wahl.Select(l.GruppeVon)));
            Pruefe.Gleich(string.Join(" ", wahl), string.Join(" ", w.Personal.EigenschaftenZiehen(g, v))); // dieselbe Person zieht dieselben
            Pruefe.Gleich(null, v.Eigenschaft);
            Pruefe.Gleich(3, w.Werkstaetten.Arbeiterliste(g)[0].EigenschaftWahl!.Count);
            Abgelehnt(w, k, new EigenschaftWaehlen(g.Name, 1, "Unsinn"), "steht für");
            Abgelehnt(w, k, new EigenschaftWaehlen(g.Name, 2, wahl[0]), "nichts zu wählen");
            var geld = k.Vermoegen;
            Ok(w, k, new EigenschaftWaehlen(g.Name, 1, wahl[1]));
            Pruefe.Gleich(wahl[1], v.Eigenschaft);
            Pruefe.Gleich(null, v.EigenschaftWahl);
            Pruefe.Gleich(geld, k.Vermoegen);
            Pruefe.Gleich(wahl[1], w.Werkstaetten.Arbeiterliste(g)[0].Eigenschaft!.Kennung);
            Pruefe.Gleich(0, w.ApKosten(new EigenschaftWaehlen(g.Name, 1, wahl[1])));
            // Ein anderer Name zieht (meist) anders – die Ziehung hängt an der Person.
            var verschieden = Enumerable.Range(0, 20).Select(i => { v.Name = $"Probe {i}"; return string.Join(" ", w.Personal.EigenschaftenZiehen(g, v)); }).Distinct().Count();
            Pruefe.Wahr(verschieden > 3, $"nur {verschieden} verschiedene Ziehungen");
        }),

        ("Personal: Die Eigenschaft wirkt auf den ganzen Betrieb, solange ihr Vorarbeiter da ist – Flinke Hand −10 % Zeit, Guter Ruf +6 % Erlös, Genügsam −15 % Lohn, Frühaufsteher +1½ Std.", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            w.Einstellen(g, "Geselle", 2);
            var v = g.ArbeiterNr(1)!;
            void Mit(string eigenschaft) => g.EigenschaftSetzen(v, eigenschaft);
            Mit("Flink");
            Pruefe.Ungefaehr(0.9, g.ZeitFaktor("Silberring"), 1e-12);
            Pruefe.Ungefaehr(w.Daten.Waren["Silberring"].Zeit * 0.9, g.MinutenJeStueck("Silberring"), 1e-9);
            Mit("GuterRuf");
            Pruefe.Ungefaehr(1, g.ZeitFaktor("Silberring"), 1e-12);
            Pruefe.Ungefaehr(1.06, g.PreisFaktor, 1e-12);
            Mit("Genuegsam");
            Pruefe.Ungefaehr(0.85, g.LohnFaktor, 1e-12);
            Mit("Fruehaufsteher");
            Pruefe.Gleich(810, g.Arbeitsminuten);
            Pruefe.Ungefaehr(440, g.Lohnkosten, 1e-9); // Lohn nach der Art: keine Stunde mehr bezahlt
            Pruefe.Wahr(g.IstArbeitszeit(21 * 60 + 15), "bis 21:30 Uhr");
            Mit("Ordnungsliebend");
            Pruefe.Gleich(w.Daten.Logistik.LagerJeWare + 20, g.LagerKapazitaetJeWare);
            Mit("Antreiber");
            Pruefe.Ungefaehr(1.15, g.TempoBonus, 1e-12);
            Pruefe.Ungefaehr(-10, g.LauneBonus, 1e-12);
            Pruefe.Ungefaehr(40, w.Werkstaetten.LauneZiel(g, v), 1e-9);
            // Geht er, geht die Eigenschaft mit.
            Ok(w, k, new ArbeiterEntlassen(g.Name, 1));
            Pruefe.Ungefaehr(1, g.TempoBonus, 1e-12);
            Pruefe.Gleich(null, g.Vorarbeiter);
        }),

        ("Personal: Sparsam – 8 % weniger Zutaten; was je Stück übrig bleibt, sammelt sich zu ganzen Stücken", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            w.Einstellen(g, "Geselle", 2);
            g.Einlagern("Eisen", 400);
            g.Einlagern("Silber", 400);
            g.EigenschaftSetzen(g.ArbeiterNr(1)!, "Sparsam");
            var silberVorher = g.ImLager("Silber");
            JahreSimulieren(sim, 1);
            var stueck = k.Vorjahr!.Hergestellt.GetValueOrDefault("Silberring");
            var jeStueck = w.Daten.Waren["Silberring"].Zutaten["Silber"];
            var verbraucht = silberVorher - g.ImLager("Silber");
            Pruefe.Wahr(stueck > 5, $"{stueck} Silberringe");
            Pruefe.Wahr(Math.Abs(verbraucht - stueck * jeStueck * 0.92) <= jeStueck, $"{verbraucht} Silber für {stueck} Ringe zu je {jeStueck}");
        }),

        ("Personal: Lehrlingsstube (Stufe 3) – Anfänger sammeln 50 % mehr Erfahrung, mit dem Lehrmeister dreimal so viel; die KI rechnet sie nach dem schnelleren Gesellen", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim, vermoegen: 50_000);
            w.Einstellen(g, "Lehrling", 1);
            Pruefe.Gleich("Lehrlingsstube gibt es erst ab Stufe 3 – Gießerei am Weinmarkt ist Stufe 1.", w.Werkstaetten.EinrichtungVerboten(g, "Lehrlingsstube"));
            g.EinrichtungHinzufuegen("Lehrlingsstube");
            Pruefe.Ungefaehr(1.5, g.AnfaengerErfahrungFaktor, 1e-12);
            Pruefe.Ungefaehr(0.67 * 12 * 1.5, w.Personal.ErfahrungJeJahr(g, g.Arbeiter[0]), 1e-9);
            w.Einstellen(g, "Geselle", 1);
            g.EigenschaftSetzen(g.Arbeiter.Single(a => a.Arbeitskraft == "Geselle"), "Lehrmeister");
            Pruefe.Ungefaehr(3, g.AnfaengerErfahrungFaktor, 1e-12);
            var geselle = g.Arbeiter.Single(a => a.Arbeitskraft == "Geselle");
            Pruefe.Ungefaehr(12, w.Personal.ErfahrungJeJahr(g, geselle), 1e-9); // nur Anfänger lernen schneller
        }),

        ("Personal: Name und Aussehen – jeder Arbeiter hat einen Namen und ein Gesicht seines Volkes, fest aus Betrieb und Nummer; Orks sind Orks", () =>
        {
            var sim = NeueSimulation(statut: Fronstatut.Bestienfron);
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 50_000);
            var g = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k, startkarren: "");
            var m = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k, startkarren: "");
            w.Einstellen(g, "Lehrling", 2);
            w.Einstellen(m, "Ork", 3);
            foreach (var a in g.Arbeiter.Concat(m.Arbeiter))
            {
                Pruefe.Wahr(a.Name.Contains(' '), $"Name „{a.Name}“");
                Pruefe.Gleich(null, Aussehenspalette.Verboten(a.Volk, a.Aussehen));
            }
            Pruefe.Wahr(m.Arbeiter.All(a => a.Volk == Volk.Ork), "Orks");
            Pruefe.Wahr(g.Arbeiter.All(a => a.AlterBeimEinstellen is >= 14 and < 19), "Lehrlinge sind 14 bis 18");
            // Dieselbe Stadt noch einmal: dieselben Namen (kein Zufall der Partie).
            var w2 = NeueSimulation(statut: Fronstatut.Bestienfron, startwert: 99).Wirtschaft!;
            var k2 = w2.KontoEroeffnen("Haus Fuchs", startvermoegen: 50_000);
            var g2 = w2.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k2, startkarren: "");
            w2.Einstellen(g2, "Lehrling", 2);
            Pruefe.Gleich(string.Join(", ", g.Arbeiter.Select(a => a.Name)), string.Join(", ", g2.Arbeiter.Select(a => a.Name)));
            var zeile = w.Werkstaetten.Arbeiterliste(g)[0];
            Pruefe.Gleich(g.Arbeiter[0].Name, zeile.Name);
            Pruefe.Gleich(g.Arbeiter[0].Aussehen, zeile.Aussehen);
            // Viele Arbeiter: mehrere Völker, einige Frauen.
            var viele = Enumerable.Range(0, 60).Select(i => w.BetriebEroeffnen($"Probe {i}", Betriebsart.Giesserei, k, startkarren: "")).ToList();
            foreach (var b in viele) w.Einstellen(b, "Lehrling", 2);
            var alle = viele.SelectMany(b => b.Arbeiter).ToList();
            Pruefe.Wahr(alle.Select(a => a.Volk).Distinct().Count() >= 3, "mehrere Völker");
            Pruefe.Wahr(alle.Count(a => a.Geschlecht == Geschlecht.Frau) is > 5 and < 40, $"{alle.Count(a => a.Geschlecht == Geschlecht.Frau)} Frauen unter 120");
        }),

        ("Personal: Speichern und Laden – Name, Aussehen, Erfahrung, Eigenschaft und Wahl bleiben; alte Spielstände: Gesellen mit voller Erfahrung, Namen fest", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 20_000);
            var g = w.BetriebEroeffnen("Schmiede am Weinmarkt", Betriebsart.Schmiede, k, startkarren: "");
            w.Einstellen(g, "Lehrling", 1);
            w.Einstellen(g, "Geselle", 2);
            g.Arbeiter[0].Erfahrung = 7.5;
            g.EigenschaftSetzen(g.Arbeiter[1], "Sparsam");
            g.EigenschaftSetzen(g.Arbeiter[2], null, new[] { "Flink", "Lehrmeister", "Wachsam" });
            var json = Speicherung.Speichern(sim);
            var g2 = Speicherung.Laden(json).Simulation.Wirtschaft!.Betriebe.Single(b => b.Name == g.Name);
            string Bild(Betrieb b) => string.Join(" | ", b.Arbeiter.Select(a =>
                $"{a.Nummer} {a.Name} {a.Volk} {a.Geschlecht} {a.Aussehen} {a.AlterBeimEinstellen} {a.Erfahrung} {a.Eigenschaft} {string.Join(",", a.EigenschaftWahl ?? Array.Empty<string>())}"));
            Pruefe.Gleich(Bild(g), Bild(g2));
            Pruefe.Ungefaehr(0.92, g2.RohstoffFaktor, 1e-12);

            // Ein Spielstand von vor PR #224: ohne Name und Erfahrung.
            var knoten = JsonNode.Parse(json)!;
            foreach (var b in knoten["Wirtschaft"]!["Betriebe"]!.AsArray())
                foreach (var a in b!["Arbeiter"]!.AsArray())
                    foreach (var feld in new[] { "Name", "Volk", "Geschlecht", "Gesicht", "Haar", "Augen", "AlterBeimEinstellen", "Erfahrung", "Eigenschaft", "EigenschaftWahl" })
                        a!.AsObject().Remove(feld);
            var g3 = Speicherung.Laden(knoten.ToJsonString()).Simulation.Wirtschaft!.Betriebe.Single(b => b.Name == g.Name);
            Pruefe.Gleich("0 24 24", string.Join(" ", g3.Arbeiter.Select(a => a.Erfahrung)));
            Pruefe.Gleich(string.Join(", ", g.Arbeiter.Select(a => a.Name)), string.Join(", ", g3.Arbeiter.Select(a => a.Name)));
            Pruefe.Gleich(null, g3.Vorarbeiter);
        }),

        ("Personal: ohne Abschnitt „personal“ (alte Spielstände bringen ihre Daten mit) – jede Kraft einstellbar, Beförderung nach Dienstzeit wie früher", () =>
        {
            var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()),
                documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!;
            knoten.AsObject().Remove("personal");
            var sim = NeueSimulation(Wirtschaftsdaten.AusJson(knoten.ToJsonString()));
            var (w, k, g) = Giesserei(sim);
            Pruefe.Wahr(!w.Personal.IstAktiv, "ohne personal");
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 2));
            Pruefe.Wahr(w.Personal.BefoerdernVerboten(g, 1)!.Contains("Befördert wird nur ein Lehrling"), w.Personal.BefoerdernVerboten(g, 1)!);
            Pruefe.Gleich(0, w.Werkstaetten.Arbeiterliste(g)[0].Stufe);
        }),

        ("Personal: Anfänger zuerst – der gefasste Taschendieb ist ein Anfänger, der Meisterdieb und der Hauptmann sind keine", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 50_000);
            var d = w.BetriebEroeffnen("Gilde", Betriebsart.Diebesgilde, k, startkarren: "");
            w.Einstellen(d, "Taschendieb", 1);
            w.Einstellen(d, "Dieb", 1);
            Pruefe.Gleich(2, d.Arbeiter.Single(a => a.Arbeitskraft == "Dieb").Nummer);
            Pruefe.Gleich(2, w.Diebe.Meisterdieb(d)!.Nummer);
            var h = w.BetriebEroeffnen("Garnison", Betriebsart.Garnison, k, startkarren: "");
            w.Einstellen(h, "Rekrut", 1);
            w.Einstellen(h, "Feldwebel", 1);
            Pruefe.Gleich("Feldwebel", w.Wache.Hauptmann(h)!.Arbeitskraft);
        }),
    };
}
