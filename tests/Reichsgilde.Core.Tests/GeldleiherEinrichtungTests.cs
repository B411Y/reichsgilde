using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bank;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für den Innenraum des Geldleihers: Einrichtung (Rechenbretter, Hauptbücher, Goldwaage, Pfandkammer, Kassenschrank …), Kunden und Schreiber.</summary>
static class GeldleiherEinrichtungTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Jedes Jahr fallen Stadtkunden aus.</summary>
    static readonly Lazy<Wirtschaftsdaten> Pleitejahr = new(() => Wirtschaftsdaten.AusJson(Json.Replace("\"ausfall\": { \"chance\": 0.2", "\"ausfall\": { \"chance\": 1")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? Daten.Value);

    /// <summary>Die Daten aus wirtschaft.json, in der Einrichtung des Geldleihers verändert.</summary>
    static Wirtschaftsdaten DatenMit(Action<JsonArray> aendern)
    {
        var knoten = JsonNode.Parse(Json, documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!;
        aendern(knoten["werkstatt"]!["einrichtung"]!["Geldleiher"]!.AsArray());
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    static IReadOnlyList<string> Fehlerliste(Action laden)
    {
        try { laden(); }
        catch (WirtschaftsdatenFehler f) { return f.Fehler; }
        return Array.Empty<string>();
    }

    /// <summary>Ein Haus mit einer Geldleihe der gewünschten Stufe, voll mit Gesellen (Schreibern), ohne Karren.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb B) Geldleihe(Simulation sim, Betriebsart art = Betriebsart.Wechselstube, string name = "Familie Fugger",
        double geld = 20_000, int? schreiber = null)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: geld);
        var b = w.BetriebEroeffnen($"{w.Daten.Art(art).Name} {name}", art, k, startkarren: "");
        if ((schreiber ?? b.Plaetze) > 0) w.Einstellen(b, "Geselle", schreiber ?? b.Plaetze);
        return (w, k, b);
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

    static void BisUhrzeit(Simulation sim, int stunde, int minute = 0)
    {
        while (sim.Zeit.UhrzeitInMinuten != stunde * 60 + minute) sim.Tick();
    }

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static int Anzahl(IEnumerable<Bankkunde> kunden, Kundenart art) => kunden.Count(k => k.Art == art);

    /// <summary>Die Kunden kurz als Text: „K0 K1 B1:Leihen V0:Kredit“.</summary>
    static string Kurz(IEnumerable<Bankkunde> kunden) =>
        string.Join(" ", kunden.Select(k => $"{k.Art.ToString()[0]}{k.Platz}" + (k.Art == Kundenart.Kaufmann ? "" : $":{k.Anliegen}")));

    /// <summary>Was die Abfrage für die Kaufleute rechnen soll – zum Nachrechnen im Test.</summary>
    static int ErwarteteKaufleute(Stadtwirtschaft w, Betrieb b, double schreiberGewicht, int stunde)
    {
        var gewicht = stunde is >= 9 and < 13 ? 1.5 : stunde >= 17 ? 0.5 : 1;
        var anteil = schreiberGewicht / Math.Max(schreiberGewicht, w.Daten.Geldleiher!.Wechsel.BedarfSchreiber);
        var wert = w.Bank.Marktvolumen / 12 * anteil / Bankwesen.UmsatzJeKaufmann * gewicht;
        return Math.Clamp((int)Math.Round(wert, MidpointRounding.AwayFromZero), 1, Bankwesen.HoechstensKaufleute);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Geldleiher-Einrichtung: Daten – sieben Stücke mit den Kennungen des Raummodells, Preise wie bei den Werkstätten, Datenversion 47", () =>
        {
            var d = Daten.Value.Werkstatt!;
            Pruefe.Gleich(47, Daten.Value.Version);
            var bank = d.EinrichtungFuer("Geldleiher");
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Goldwaage Pfandkammer Kassenschrank Loeschfass Eisentuer", string.Join(" ", bank.Select(e => e.Kennung)));
            Pruefe.Gleich("Rechenbretter, Hauptbücher, Goldwaage, Pfandkammer, Kassenschrank, Löschfass, Eisentür", string.Join(", ", bank.Select(e => e.Name)));
            Pruefe.Gleich("300 800 350 350 200 100 300", string.Join(" ", bank.Select(e => e.Preis)));
            Pruefe.Gleich("1 2 1 1 1 1 1", string.Join(" ", bank.Select(e => e.AbStufe)));
            Pruefe.Gleich("Werkzeug1", d.Stueck("Geldleiher", "Werkzeug2")!.Voraussetzung);
            Pruefe.Ungefaehr(1.1, d.Stueck("Geldleiher", "Werkzeug1")!.Wirkung.RahmenFaktor, 1e-12);
            Pruefe.Ungefaehr(1.1, d.Stueck("Geldleiher", "Werkzeug2")!.Wirkung.RahmenFaktor, 1e-12);
            Pruefe.Ungefaehr(1.1, d.Stueck("Geldleiher", "Goldwaage")!.Wirkung.WechselFaktor, 1e-12);
            Pruefe.Ungefaehr(0.75, d.Stueck("Geldleiher", "Pfandkammer")!.Wirkung.AusfallFaktor, 1e-12);
            Pruefe.Ungefaehr(1.25, d.Stueck("Geldleiher", "Kassenschrank")!.Wirkung.KreditFaktor, 1e-12);
            Pruefe.Ungefaehr(1, d.Stueck("Geldleiher", "Kassenschrank")!.Wirkung.EinbruchFaktor, 1e-12);
            Pruefe.Ungefaehr(0.5, d.Stueck("Geldleiher", "Loeschfass")!.Wirkung.BrandFaktor, 1e-12);
            Pruefe.Ungefaehr(0.5, d.Stueck("Geldleiher", "Eisentuer")!.Wirkung.EinbruchFaktor, 1e-12);
            Pruefe.Gleich("Rahmen je Schreiber 10 % größer", d.Stueck("Geldleiher", "Werkzeug1")!.Wirkung.Text());
            Pruefe.Gleich("Wechselgebühren 10 % höher", d.Stueck("Geldleiher", "Goldwaage")!.Wirkung.Text());
            Pruefe.Gleich("Verlust bei Ausfall eines Kunden 25 % kleiner", d.Stueck("Geldleiher", "Pfandkammer")!.Wirkung.Text());
            Pruefe.Gleich("Kredite an Häuser bis 25 % höher", d.Stueck("Geldleiher", "Kassenschrank")!.Wirkung.Text());
            // Die anderen Berufe kennen die neuen Wirkungen nicht: Ihre Stücke bleiben ohne.
            foreach (var (beruf, liste) in d.Einrichtung.Where(x => x.Key != "Geldleiher"))
                Pruefe.Wahr(liste.All(e => e.Wirkung is { RahmenFaktor: 1, WechselFaktor: 1, AusfallFaktor: 1, KreditFaktor: 1 }), $"{beruf}: keine Wirkung des Geldleihers");
        }),

        ("Geldleiher-Einrichtung: Daten werden geprüft (Rahmen-, Wechsel- und Kreditfaktor zwischen 1 und 2, Ausfallfaktor zwischen 0 und 1)", () =>
        {
            var fehler = Fehlerliste(() => DatenMit(a =>
            {
                a[0]!["wirkung"]!["rahmenFaktor"] = 0.9;
                a[2]!["wirkung"]!["wechselFaktor"] = 2.5;
                a[3]!["wirkung"]!["ausfallFaktor"] = 1.2;
                a[4]!["wirkung"]!["rahmenFaktor"] = 3;
                a[5]!["wirkung"]!["kreditFaktor"] = 0.5;
            }));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Geldleiher.Werkzeug1.wirkung.rahmenFaktor muss zwischen 1 und 2")), string.Join(" | ", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Geldleiher.Goldwaage.wirkung.wechselFaktor muss zwischen 1 und 2")), string.Join(" | ", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Geldleiher.Pfandkammer.wirkung.ausfallFaktor muss zwischen 0 und 1")), string.Join(" | ", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Geldleiher.Kassenschrank.wirkung.rahmenFaktor")), string.Join(" | ", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Geldleiher.Loeschfass.wirkung.kreditFaktor muss zwischen 1 und 2")), string.Join(" | ", fehler));
            Pruefe.Gleich(5, fehler.Count);
        }),

        ("Geldleiher-Einrichtung: Wechselstube, Geldleihe und Bankhaus können Einrichtung kaufen (Anschaffung); Hauptbücher erst ab Stufe 2 und nach den Rechenbrettern; die Einrichtung bleibt beim Ausbau", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Geldleihe(sim, geld: 5_000);
            foreach (var art in new[] { Betriebsart.Wechselstube, Betriebsart.Geldleihe, Betriebsart.Bankhaus })
                Pruefe.Wahr(w.Werkstaetten.KannEinrichtung(w.BetriebEroeffnen($"Probe {art}", art, k, startkarren: "")), $"{art} hat Einrichtung");
            var liste = w.Werkstaetten.Einrichtungsliste(b);
            Pruefe.Wahr(liste.Count == 7 && liste.All(e => !e.Gekauft), "sieben Stücke, keins gekauft");
            Abgelehnt(w, k, new EinrichtungKaufen(b.Name, "Werkzeug2"), "erst ab Stufe 2");
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(4_700, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(300, k.Laufend.Investitionen, 1e-9);
            Abgelehnt(w, k, new EinrichtungKaufen(b.Name, "Werkzeug1"), "hat schon");
            b.Ausbauen(Betriebsart.Geldleihe, 0);
            Pruefe.Wahr(b.HatEinrichtung("Werkzeug1"), "Rechenbretter nach dem Ausbau");
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug2"));
            var (_, k2, b2) = Geldleihe(sim, Betriebsart.Geldleihe, "Familie Welser");
            Abgelehnt(w, k2, new EinrichtungKaufen(b2.Name, "Werkzeug2"), "Erst braucht es: Rechenbretter");
            Abgelehnt(w, k2, new EinrichtungKaufen(b2.Name, "Regale"), "kein Stück");
        }),

        ("Geldleiher-Einrichtung: Rechenbretter und Hauptbücher – Rahmen je Schreiber ×1,1 und nochmals ×1,1 (1,21); die Stadtzinsen eines Tages steigen mit", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Geldleihe(sim);
            Pruefe.Ungefaehr(2_000, w.Bank.RahmenJeSchreiber(b), 1e-12);
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(2_200, w.Bank.RahmenJeSchreiber(b), 1e-12);
            Pruefe.Ungefaehr(4_400, w.Bank.RahmenVoll(b), 1e-12);
            Pruefe.Ungefaehr(4_400, w.Bank.Rahmen(b), 1e-12);
            Ok(w, k, new KasseEinlegen(b.Name, 10_000));
            BisUhrzeit(sim, 21);
            Pruefe.Ungefaehr(0.12 * 4_400, w.Bank.DiesesJahr(k).Stadtzinsen, 1e-6);

            var (w2, k2, b2) = Geldleihe(NeueSimulation(), Betriebsart.Geldleihe);
            Ok(w2, k2, new EinrichtungKaufen(b2.Name, "Werkzeug1"));
            Ok(w2, k2, new EinrichtungKaufen(b2.Name, "Werkzeug2"));
            Pruefe.Ungefaehr(2_000 * 1.25 * 1.21, w2.Bank.RahmenJeSchreiber(b2), 1e-12);
            Pruefe.Ungefaehr(1.21, b2.RahmenFaktor, 1e-12);
        }),

        ("Geldleiher-Einrichtung: Goldwaage – Wechselgebühr ×1,1 (gleicher Markt, gleiche Stadtzinsen); Wert je Minute mit und ohne Einrichtung", () =>
        {
            Bankbuch Tag(bool goldwaage, out double wert, out double wertStufe)
            {
                var sim = NeueSimulation();
                var (w, k, b) = Geldleihe(sim);
                Ok(w, k, new KasseEinlegen(b.Name, 4_000));
                if (goldwaage) Ok(w, k, new EinrichtungKaufen(b.Name, "Goldwaage"));
                wert = w.Bank.WertJeMinute(b);
                wertStufe = w.Bank.WertJeMinute(b.Stufe);
                BisUhrzeit(sim, 21);
                return w.Bank.DiesesJahr(k);
            }
            var ohne = Tag(false, out var wertOhne, out var stufeOhne);
            var mit = Tag(true, out var wertMit, out var stufeMit);
            Pruefe.Wahr(ohne.Wechsel > 0, "Wechsel ohne Goldwaage");
            Pruefe.Ungefaehr(ohne.Wechsel * 1.1, mit.Wechsel, 1e-9);
            Pruefe.Ungefaehr(ohne.Stadtzinsen, mit.Stadtzinsen, 1e-12);
            Pruefe.Ungefaehr(stufeOhne, wertOhne, 1e-12); // ohne Einrichtung derselbe Wert wie für die Stufe
            Pruefe.Ungefaehr(stufeOhne, stufeMit, 1e-12);
            Pruefe.Wahr(wertMit > wertOhne, $"Wert je Minute mit Goldwaage {wertMit} > {wertOhne}");
        }),

        ("Geldleiher-Einrichtung: Pfandkammer – fallen Stadtkunden aus, sind 7,5 % statt 10 % der Kasse verloren (gleicher Zufall)", () =>
        {
            foreach (var pfand in new[] { false, true })
            {
                var sim = NeueSimulation(Pleitejahr.Value);
                var (w, k, b) = Geldleihe(sim);
                if (pfand) Ok(w, k, new EinrichtungKaufen(b.Name, "Pfandkammer"));
                Ok(w, k, new KasseEinlegen(b.Name, 3_000));
                JahrBeenden(sim);
                var verlust = pfand ? 225 : 300;
                Pruefe.Ungefaehr(3_000 - verlust, w.Bank.Kasse(b), 1e-9);
                Pruefe.Ungefaehr(verlust, w.Bank.Vorjahr(k)!.Ausfall, 1e-9);
            }
        }),

        ("Geldleiher-Einrichtung: Pfandkammer – geht ein Schuldner aus dem Spiel, bringt das Pfand 25 % des Rests zurück; es zählt die beste Pfandkammer des Hauses", () =>
        {
            (double Vermoegen, double Ausfall, double Forderungen) Lauf(bool pfand)
            {
                var sim = NeueSimulation();
                var (w, geber, _) = Geldleihe(sim);
                // Die Pfandkammer steht in einer zweiten Wechselstube ohne Schreiber – das Pfand liegt trotzdem im Haus.
                var zweite = w.BetriebEroeffnen("Wechselstube am Hafen", Betriebsart.Wechselstube, geber, startkarren: "");
                if (pfand) Ok(w, geber, new EinrichtungKaufen(zweite.Name, "Pfandkammer"));
                Pruefe.Ungefaehr(pfand ? 0.75 : 1, w.Bank.AusfallFaktor(geber), 1e-12);
                var nehmer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 4_000);
                Ok(w, nehmer, new KreditAufnehmen(geber.Inhaber, 2_000, 3));
                nehmer.IstBankrott = true;
                JahrBeenden(sim);
                Pruefe.Gleich<object?>(null, w.Bank.KreditVon(nehmer));
                return (geber.Vermoegen + (pfand ? 350 : 0), w.Bank.Vorjahr(geber)!.Ausfall, geber.Forderungen);
            }
            var ohne = Lauf(false);
            var mit = Lauf(true);
            Pruefe.Ungefaehr(2_000, ohne.Ausfall, 1e-9);
            Pruefe.Ungefaehr(1_500, mit.Ausfall, 1e-9);
            Pruefe.Wahr(Math.Abs(mit.Forderungen) < 1e-9 && Math.Abs(ohne.Forderungen) < 1e-9, "keine Forderung bleibt stehen");
            Pruefe.Ungefaehr(ohne.Vermoegen + 500, mit.Vermoegen, 1e-9); // das Pfand: 25 % von 2.000 zurück ins Bargeld
        }),

        ("Geldleiher-Einrichtung: Kassenschrank – Kredite an Häuser bis ×1,25 (Wechselstube 3.000 → 3.750, Bankhaus 12.000 → 15.000); es zählt der beste Kassenschrank des Hauses; ohne bleibt alles", () =>
        {
            var sim = NeueSimulation();
            // Ohne Kassenschrank: der Höchstbetrag nach Stufe, genau wie bisher (so auch bei den KI-Häusern, die nichts kaufen).
            foreach (var (art, hoechst) in new[] { (Betriebsart.Wechselstube, 3_000.0), (Betriebsart.Geldleihe, 6_000.0), (Betriebsart.Bankhaus, 12_000.0) })
            {
                var (w0, k0, _) = Geldleihe(sim, art, $"Probe {art}", geld: 50_000);
                Pruefe.Gleich(1.0, w0.Bank.KreditFaktor(k0));
                Pruefe.Gleich(hoechst, w0.Bank.Kredithoechstbetrag(k0));
                Pruefe.Gleich(hoechst, w0.Bank.FreierKreditrahmen(k0));
            }

            var (w, k, b) = Geldleihe(sim, geld: 20_000);
            var nehmer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 40_000);
            Abgelehnt(w, nehmer, new KreditAufnehmen(k.Inhaber, 3_500, 2), "höchstens 3.000");
            // Der Kassenschrank steht in einer zweiten Wechselstube ohne Schreiber – er zählt trotzdem (der beste des Hauses).
            var zweite = w.BetriebEroeffnen("Wechselstube am Hafen", Betriebsart.Wechselstube, k, startkarren: "");
            Ok(w, k, new EinrichtungKaufen(zweite.Name, "Kassenschrank"));
            Pruefe.Ungefaehr(1.25, w.Bank.KreditFaktor(k), 1e-12);
            Pruefe.Ungefaehr(3_750, w.Bank.Kredithoechstbetrag(k), 1e-12);
            Pruefe.Ungefaehr(3_750, w.Bank.Hoechstkredit(k, nehmer), 1e-12);
            Abgelehnt(w, nehmer, new KreditAufnehmen(k.Inhaber, 3_800, 2), "höchstens 3.750");
            Ok(w, nehmer, new KreditAufnehmen(k.Inhaber, 3_750, 2));
            Pruefe.Ungefaehr(0, w.Bank.FreierKreditrahmen(k), 1e-12);
            // Ausgebaut zum Bankhaus: 12.000 × 1,25.
            b.Ausbauen(Betriebsart.Geldleihe, 0);
            b.Ausbauen(Betriebsart.Bankhaus, 0);
            Pruefe.Ungefaehr(15_000, w.Bank.Kredithoechstbetrag(k), 1e-12);
            Pruefe.Ungefaehr(15_000 - 3_750, w.Bank.FreierKreditrahmen(k), 1e-12);
        }),

        ("Geldleiher-Einrichtung: Löschfass und Eisentür halbieren Feuer und Einbruch in der Geldleihe", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Geldleihe(sim);
            var diebstahl = w.Gauner.Erfolgschance(Gaunerstueck.Diebstahl, b);
            var feuer = w.Gauner.Erfolgschance(Gaunerstueck.Sabotage, b);
            Ok(w, k, new EinrichtungKaufen(b.Name, "Kassenschrank"));
            Pruefe.Ungefaehr(diebstahl, w.Gauner.Erfolgschance(Gaunerstueck.Diebstahl, b), 1e-15); // der Kassenschrank schützt nicht
            Ok(w, k, new EinrichtungKaufen(b.Name, "Eisentuer"));
            Ok(w, k, new EinrichtungKaufen(b.Name, "Loeschfass"));
            Pruefe.Ungefaehr(diebstahl * 0.5, w.Gauner.Erfolgschance(Gaunerstueck.Diebstahl, b), 1e-15);
            Pruefe.Ungefaehr(feuer * 0.5, w.Gauner.Erfolgschance(Gaunerstueck.Sabotage, b), 1e-15);
        }),

        ("Geldleiher-Einrichtung: Einrichtung der Geldleihe übersteht Speichern und Laden (Rahmen gleich)", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Beruf: "Geldleiher", Startvermoegen: 10_000));
            var w = sim.Wirtschaft!;
            var stube = w.Betriebe.Single(b => b.Konto == spieler);
            Ok(w, spieler, new EinrichtungKaufen(stube.Name, "Werkzeug1"));
            Ok(w, spieler, new EinrichtungKaufen(stube.Name, "Pfandkammer"));
            var (kopie, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var kb = kopie.Wirtschaft!.Betriebe.Single(b => b.Name == stube.Name);
            Pruefe.Gleich("Pfandkammer Werkzeug1", string.Join(" ", kb.Einrichtung.OrderBy(x => x, StringComparer.Ordinal)));
            Pruefe.Ungefaehr(w.Bank.RahmenJeSchreiber(stube), kopie.Wirtschaft!.Bank.RahmenJeSchreiber(kb), 1e-12);
        }),

        ("Geldleiher-Kunden: Wechselstube – Kaufleute nach Marktumsatz und Tageszeit (vormittags Markt, abends weniger), Bürger nur mit Kasse, keine Vornehmen; außerhalb der Arbeitszeit niemand", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Geldleihe(sim);
            Pruefe.Gleich(0, w.Bank.KundenJetzt(b).Count); // 05:00
            BisUhrzeit(sim, 7, 59);
            Pruefe.Gleich(0, w.Bank.KundenJetzt(b).Count);
            var kaufleute = new Dictionary<int, int>();
            var buerger = 0;
            for (var stunde = 8; stunde < 20; stunde++)
                for (var minute = 0; minute < 60; minute += 15)
                {
                    BisUhrzeit(sim, stunde, minute);
                    var kunden = w.Bank.KundenJetzt(b);
                    if (minute == 0) kaufleute[stunde] = Anzahl(kunden, Kundenart.Kaufmann);
                    Pruefe.Gleich(ErwarteteKaufleute(w, b, b.LeistungProMinute, stunde), Anzahl(kunden, Kundenart.Kaufmann), $"Kaufleute um {stunde}:{minute:00}");
                    buerger += Anzahl(kunden, Kundenart.Buerger);
                    Pruefe.Gleich(0, Anzahl(kunden, Kundenart.Vornehm), "keine Vornehmen in der Wechselstube");
                    Pruefe.Wahr(kunden.Where(x => x.Art == Kundenart.Kaufmann).Select(x => x.Platz).SequenceEqual(Enumerable.Range(0, Anzahl(kunden, Kundenart.Kaufmann))),
                        "Plätze am Wechseltisch der Reihe nach");
                }
            Pruefe.Wahr(kaufleute[10] > kaufleute[14] && kaufleute[14] > kaufleute[18], $"vormittags mehr als nachmittags, abends weniger: {string.Join(" ", kaufleute)}");
            Pruefe.Gleich(0, buerger); // leere Kasse: niemand leiht oder zahlt zurück
            BisUhrzeit(sim, 20);
            Pruefe.Gleich(0, w.Bank.KundenJetzt(b).Count);
            Pruefe.Gleich(0, w.Bank.SchreiberJetzt(b).Count);
        }),

        ("Geldleiher-Kunden: Bürger am Schalter – mit voller Kasse an etwa der Hälfte der Viertelstunden je Platz (goldener Schnitt), leihen und zurückzahlen; halbe Kasse halb so oft", () =>
        {
            int[] Zaehlen(double kasse)
            {
                var sim = NeueSimulation();
                var (w, k, b) = Geldleihe(sim);
                if (kasse > 0) Ok(w, k, new KasseEinlegen(b.Name, kasse));
                var z = new int[4]; // Platz 0, Platz 1, Leihen, Zurückzahlen
                for (var q = 8 * 4; q < 20 * 4; q++)
                {
                    BisUhrzeit(sim, q / 4, q % 4 * 15);
                    var kunden = w.Bank.KundenJetzt(b).Where(x => x.Art == Kundenart.Buerger).ToList();
                    Pruefe.Wahr(kunden.Count <= Bankwesen.HoechstensBuerger, "höchstens zwei Bürger");
                    foreach (var x in kunden)
                    {
                        z[x.Platz]++;
                        z[x.Anliegen == Kundenanliegen.Leihen ? 2 : 3]++;
                        Pruefe.Wahr(x.Anliegen is Kundenanliegen.Leihen or Kundenanliegen.Zurueckzahlen && x.Haus is null, $"Bürger: {x}");
                    }
                    // Nachrechnen: Platz 0 nach q × 0,618…, Platz 1 nach q × 0,414…
                    var c = 0.5 * Math.Min(1, kasse / w.Bank.RahmenVoll(b));
                    var p0 = q * 0.6180339887498949 - Math.Floor(q * 0.6180339887498949) < c;
                    var p1 = q * 0.4142135623730951 - Math.Floor(q * 0.4142135623730951) < c;
                    Pruefe.Gleich($"{p0} {p1}", $"{kunden.Any(x => x.Platz == 0)} {kunden.Any(x => x.Platz == 1)}", $"Viertelstunde {q}");
                }
                return z;
            }
            var voll = Zaehlen(10_000);
            Pruefe.Wahr(voll[0] is >= 20 and <= 28 && voll[1] is >= 20 and <= 28, $"je Platz etwa 24 von 48 Viertelstunden: {string.Join(" ", voll)}");
            Pruefe.Wahr(voll[2] > 5 && voll[3] > 5, $"leihen und zurückzahlen: {string.Join(" ", voll)}");
            var halb = Zaehlen(2_000);
            Pruefe.Wahr(halb[0] + halb[1] is >= 16 and <= 32, $"halbe Kasse: etwa 24 Besuche: {string.Join(" ", halb)}");
        }),

        ("Geldleiher-Kunden: Bankhaus – Vornehme im Empfang: zuerst Häuser mit Kredit, dann Patrizier, höchstens drei; nur im Kontor und ab Stufe 3; höchstens 4 Kaufleute", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Geldleihe(sim, Betriebsart.Bankhaus, geld: 40_000);
            Ok(w, k, new KasseEinlegen(b.Name, 20_000));
            var nehmer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 10_000);
            Ok(w, nehmer, new KreditAufnehmen(k.Inhaber, 1_000, 2));
            var patrizier = Enumerable.Range(1, 6).Select(i => w.KontoEroeffnen($"Haus Patrizier {i}", startvermoegen: 1_000)).ToList();
            foreach (var p in patrizier) p.Stand = w.Daten.StandStufe("Patrizier");
            // Eine Geldleihe (Stufe 2) desselben Hauses: kein Empfang.
            var stufe2 = w.BetriebEroeffnen("Geldleihe Fugger", Betriebsart.Geldleihe, k, startkarren: "");
            w.Einstellen(stufe2, "Geselle", 3);
            var eisenbart = 0;
            var gesehen = new HashSet<Konto>();
            for (var stunde = 8; stunde < 20; stunde++)
            {
                BisUhrzeit(sim, stunde, 30);
                var kunden = w.Bank.KundenJetzt(b);
                Pruefe.Wahr(Anzahl(kunden, Kundenart.Kaufmann) is >= 1 and <= Bankwesen.HoechstensKaufleute, Kurz(kunden));
                var vornehm = kunden.Where(x => x.Art == Kundenart.Vornehm).ToList();
                Pruefe.Wahr(vornehm.Count <= Bankwesen.HoechstensVornehme, Kurz(kunden));
                Pruefe.Wahr(vornehm.Select(x => x.Platz).SequenceEqual(Enumerable.Range(0, vornehm.Count)), "Plätze im Empfang der Reihe nach");
                // Nachrechnen: der i-te Kandidat (Eisenbart, dann die Patrizier) nach h × 0,618… + i × 0,414…
                var erwartet = new List<Konto>();
                var kandidaten = new[] { nehmer }.Concat(patrizier).ToList();
                for (var i = 0; i < kandidaten.Count && erwartet.Count < 3; i++)
                {
                    var v = stunde * 0.6180339887498949 + i * 0.4142135623730951;
                    if (v - Math.Floor(v) < (i == 0 ? 0.5 : 0.25)) erwartet.Add(kandidaten[i]);
                }
                Pruefe.Gleich(string.Join(",", erwartet.Select(x => x.Inhaber)), string.Join(",", vornehm.Select(x => x.Haus!.Inhaber)), $"{stunde}:30");
                foreach (var x in vornehm)
                {
                    gesehen.Add(x.Haus!);
                    Pruefe.Gleich(x.Haus == nehmer ? Kundenanliegen.Kredit : Kundenanliegen.Beraten, x.Anliegen);
                }
                if (vornehm.Any(x => x.Haus == nehmer)) eisenbart++;
                Pruefe.Gleich(0, Anzahl(w.Bank.KundenJetzt(stufe2), Kundenart.Vornehm), "Geldleihe: kein Empfang");
            }
            Pruefe.Wahr(eisenbart is >= 3 and <= 9, $"Eisenbart kommt etwa jede zweite Stunde: {eisenbart}");
            Pruefe.Wahr(gesehen.Count >= 4, $"auch Patrizier kommen: {gesehen.Count}");
        }),

        ("Geldleiher-Schreiber: je Kundengruppe bedient einer (Empfang, Wechseltisch, Schalter; Gesellen vor Lehrlingen), die anderen rechnen, wiegen und zählen – ab Stufe 2 auch das Hauptbuch", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Geldleihe(sim, schreiber: 0);
            w.Einstellen(b, "Lehrling", 1);
            w.Einstellen(b, "Geselle", 1);
            Ok(w, k, new KasseEinlegen(b.Name, 10_000));
            Pruefe.Gleich(0, w.Bank.SchreiberJetzt(b).Count); // 05:00 – noch daheim
            var taetigkeiten = new HashSet<Schreibertaetigkeit>();
            for (var q = 8 * 4; q < 20 * 4; q++)
            {
                BisUhrzeit(sim, q / 4, q % 4 * 15);
                var kunden = w.Bank.KundenJetzt(b);
                var schreiber = w.Bank.SchreiberJetzt(b);
                Pruefe.Gleich(2, schreiber.Count);
                Pruefe.Wahr(schreiber.Select(s => s.Arbeiter.Nummer).SequenceEqual(schreiber.Select(s => s.Arbeiter.Nummer).OrderBy(n => n)), "nach Nummer");
                // Erst der Wechseltisch, dann der Schalter; der Geselle zuerst, dann der Lehrling.
                var gruppen = new[] { Kundenart.Kaufmann, Kundenart.Buerger }.Where(a => kunden.Any(x => x.Art == a)).ToList();
                var geselle = schreiber.Single(s => s.Arbeiter.Arbeitskraft == "Geselle");
                var lehrling = schreiber.Single(s => s.Arbeiter.Arbeitskraft == "Lehrling");
                Pruefe.Gleich($"{(gruppen.Count > 0 ? gruppen[0] : null)} {(gruppen.Count > 1 ? gruppen[1] : null)}", $"{geselle.Bedient} {lehrling.Bedient}",
                    $"{q / 4}:{q % 4 * 15:00} {Kurz(kunden)}");
                foreach (var s in schreiber)
                {
                    Pruefe.Gleich(s.Taetigkeit == Schreibertaetigkeit.Bedienen, s.Bedient is not null, "Bedienen hat Kunden, sonst keine");
                    if (s.Taetigkeit != Schreibertaetigkeit.Bedienen) taetigkeiten.Add(s.Taetigkeit);
                }
            }
            Pruefe.Gleich("Rechnen Wiegen Zaehlen", string.Join(" ", taetigkeiten.OrderBy(t => t)));
            BisUhrzeit(sim, 20);
            Pruefe.Gleich(0, w.Bank.SchreiberJetzt(b).Count);

            // Stufe 2: Wer nicht bedient, rechnet, wiegt, schreibt ins Hauptbuch oder zählt – reihum, jede Stunde weiter.
            var sim2 = NeueSimulation();
            var (w2, _, b2) = Geldleihe(sim2, Betriebsart.Geldleihe);
            var reihe = new HashSet<Schreibertaetigkeit>();
            for (var stunde = 8; stunde < 20; stunde++)
            {
                BisUhrzeit(sim2, stunde);
                foreach (var s in w2.Bank.SchreiberJetzt(b2).Where(s => s.Taetigkeit != Schreibertaetigkeit.Bedienen)) reihe.Add(s.Taetigkeit);
                var stand = w2.Bank.SchreiberJetzt(b2).Where(s => s.Taetigkeit != Schreibertaetigkeit.Bedienen).Select(s => s.Taetigkeit).ToList();
                Pruefe.Gleich(stand.Count, stand.Distinct().Count(), "zwei tun nicht dasselbe");
            }
            Pruefe.Gleich("Rechnen Wiegen Hauptbuch Zaehlen", string.Join(" ", reihe.OrderBy(t => t)));
        }),

        ("Geldleiher-Kunden: Abfragen ändern nichts (gleicher Verlauf mit und ohne Abfrage); kein Bankbetrieb, ohne Schreiber oder im Umbau: keine Kunden", () =>
        {
            string Lauf(bool abfragen)
            {
                var sim = NeueSimulation(startwert: 1401);
                var (w, k, b) = Geldleihe(sim, Betriebsart.Bankhaus, geld: 40_000);
                Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug1"));
                Ok(w, k, new EinrichtungKaufen(b.Name, "Goldwaage"));
                Ok(w, k, new EinrichtungKaufen(b.Name, "Kassenschrank"));
                Ok(w, k, new KasseEinlegen(b.Name, 15_000));
                var nehmer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 10_000);
                Ok(w, nehmer, new KreditAufnehmen(k.Inhaber, 2_000, 2));
                for (var i = 0; i < 2 * 1020; i++)
                {
                    sim.Tick();
                    if (!abfragen) continue;
                    _ = w.Bank.KundenJetzt(b);
                    _ = w.Bank.SchreiberJetzt(b);
                    _ = w.Bank.WertJeMinute(b);
                }
                return string.Join(";", w.Konten.Select(x => $"{x.Inhaber}:{x.Vermoegen:R}")) +
                       $"|{w.Bank.Vorjahr(k)}|{w.Bank.Kasse(b):R}|{w.Bank.KreditVon(nehmer)?.Rest:R}";
            }
            Pruefe.Gleich(Lauf(false), Lauf(true));

            var sim3 = NeueSimulation();
            var (w3, k3, b3) = Geldleihe(sim3, schreiber: 0);
            var schmiede = w3.BetriebEroeffnen("Schmiede Fugger", Betriebsart.Schmiede, k3, startkarren: "");
            w3.Einstellen(schmiede, "Geselle", 2);
            BisUhrzeit(sim3, 10);
            Pruefe.Gleich(0, w3.Bank.KundenJetzt(b3).Count);
            Pruefe.Gleich(0, w3.Bank.SchreiberJetzt(b3).Count);
            Pruefe.Gleich(0, w3.Bank.KundenJetzt(schmiede).Count);
            Pruefe.Gleich(0, w3.Bank.SchreiberJetzt(schmiede).Count);
            w3.Einstellen(b3, "Geselle", 2);
            Pruefe.Wahr(w3.Bank.KundenJetzt(b3).Count > 0, "mit Schreibern kommen Kunden");
            b3.Ausbauen(Betriebsart.Geldleihe, 600);
            Pruefe.Gleich(0, w3.Bank.KundenJetzt(b3).Count);
            Pruefe.Wahr(w3.Bank.SchreiberJetzt(b3).All(s => s.Taetigkeit != Schreibertaetigkeit.Bedienen), "beim Umbau bedient niemand");
        }),
    };
}
