using Reichsgilde.Core.Zeit;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für den Innenraum des Diebs: Einrichtung (Dietriche, Verkleidung, Hehlerkammer, Handschuhe …) und die Anwesenheit der Leute.</summary>
static class DiebEinrichtungTests
{
    static readonly string Json = Datenpfad.EinVersuchJeNacht(File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Niemand wird am Markt erwischt – die Belegschaft bleibt, wie sie ist.</summary>
    static readonly Lazy<Wirtschaftsdaten> Unerwischt = new(() => Wirtschaftsdaten.AusJson(Json.Replace("\"fangJeStunde\": 0.012", "\"fangJeStunde\": 0")));

    /// <summary>Auskundschafter finden jede Spur (gedeckelt 95 %), niemand wird am Markt erwischt.</summary>
    static readonly Lazy<Wirtschaftsdaten> Spuersinn = new(() => Wirtschaftsdaten.AusJson(Json
        .Replace("\"fangJeStunde\": 0.012", "\"fangJeStunde\": 0")
        .Replace("\"auskundschaften\": { \"abStufe\": 1, \"chance\": 0.4 }", "\"auskundschaften\": { \"abStufe\": 1, \"chance\": 1 }")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? Daten.Value);

    /// <summary>Die Daten aus wirtschaft.json, in der Einrichtung des Diebs verändert.</summary>
    static Wirtschaftsdaten DatenMit(Action<JsonArray> aendern)
    {
        var knoten = JsonNode.Parse(Json, documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!;
        aendern(knoten["werkstatt"]!["einrichtung"]!["Dieb"]!.AsArray());
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    static IReadOnlyList<string> Fehlerliste(Action laden)
    {
        try { laden(); }
        catch (WirtschaftsdatenFehler f) { return f.Fehler; }
        return Array.Empty<string>();
    }

    /// <summary>Ein Haus mit einem Diebesbetrieb, Gesellen und Geld, ohne Karren.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb B) Dieb(Simulation sim, Betriebsart art = Betriebsart.Schmugglerloch, int leute = 4,
        string name = "Familie Elster", double vermoegen = 10_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: vermoegen);
        var b = w.BetriebEroeffnen($"{w.Daten.Art(art).Name} {name}", art, k, startkarren: "");
        if (leute > 0) w.Einstellen(b, "Geselle", leute);
        return (w, k, b);
    }

    static (Konto K, Betrieb G) Opfer(Stadtwirtschaft w, string name = "Haus Eisenbart")
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 10_000);
        var g = w.BetriebEroeffnen($"Gießerei {name}", Betriebsart.Giesserei, k, startkarren: "");
        g.Einlagern("Dolch", 20);
        return (k, g);
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

    /// <summary>Die Anwesenheit kurz als Text: „Nummer:Ort:Tätigkeit“, nach Nummer.</summary>
    static string Kurz(IEnumerable<Diebesstand> liste) =>
        string.Join(" ", liste.OrderBy(s => s.Arbeiter.Nummer).Select(s => $"{s.Arbeiter.Nummer}:{s.Ort}:{s.Taetigkeit}"));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Dieb-Einrichtung: Daten – sieben Stücke mit den Kennungen des Raummodells, Preise wie bei den Werkstätten, Datenversion 52", () =>
        {
            var d = Daten.Value.Werkstatt!;
            Pruefe.Gleich(66, Daten.Value.Version);
            var dieb = d.EinrichtungFuer("Dieb");
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Verkleidung Regale Loeschfass Eisentuer Handschuhe", string.Join(" ", dieb.Take(7).Select(e => e.Kennung))); // dazu ab PR #224 die Lehrlingsstube
            Pruefe.Gleich("Dietriche, Meisterdietriche, Verkleidungstruhe, Hehlerkammer, Löschfass, Eisentür, Lederhandschuhe",
                string.Join(", ", dieb.Take(7).Select(e => e.Name)));
            Pruefe.Gleich("300 800 350 200 100 150 80", string.Join(" ", dieb.Take(7).Select(e => e.Preis))); // Eisentür 150 seit PR #177
            Pruefe.Gleich("Werkzeug1", d.Stueck("Dieb", "Werkzeug2")!.Voraussetzung);
            Pruefe.Gleich(2, d.Stueck("Dieb", "Werkzeug2")!.AbStufe);
            Pruefe.Ungefaehr(0.1, d.Stueck("Dieb", "Werkzeug1")!.Wirkung.EinbruchBonus, 1e-12);
            Pruefe.Ungefaehr(0.1, d.Stueck("Dieb", "Werkzeug2")!.Wirkung.EinbruchBonus, 1e-12);
            Pruefe.Ungefaehr(0.75, d.Stueck("Dieb", "Verkleidung")!.Wirkung.FangFaktor, 1e-12);
            Pruefe.Gleich(20, d.Stueck("Dieb", "Regale")!.Wirkung.LagerPlus);
            Pruefe.Ungefaehr(0.75, d.Stueck("Dieb", "Handschuhe")!.Wirkung.SpurenFaktor, 1e-12);
            Pruefe.Gleich("+10 % Erfolg beim Einbruch", d.Stueck("Dieb", "Werkzeug1")!.Wirkung.Text());
            Pruefe.Gleich("Taschendiebe 25 % seltener gefasst", d.Stueck("Dieb", "Verkleidung")!.Wirkung.Text());
            Pruefe.Gleich("Zeugen und Beweise 25 % seltener", d.Stueck("Dieb", "Handschuhe")!.Wirkung.Text());
            // Die anderen Berufe kennen die neuen Wirkungen nicht: Ihre Stücke bleiben ohne.
            foreach (var (beruf, liste) in d.Einrichtung.Where(x => x.Key != "Dieb"))
                Pruefe.Wahr(liste.All(e => e.Wirkung is { EinbruchBonus: 0, FangFaktor: 1, SpurenFaktor: 1 }), $"{beruf}: keine Wirkung des Diebs");
        }),

        ("Dieb-Einrichtung: Daten werden geprüft (Einbruch-Bonus, Fang- und Spurenfaktor zwischen 0 und 1)", () =>
        {
            var fehler = Fehlerliste(() => DatenMit(a =>
            {
                a[0]!["wirkung"]!["einbruchBonus"] = 1.5;
                a[2]!["wirkung"]!["fangFaktor"] = -0.1;
                a[6]!["wirkung"]!["spurenFaktor"] = 2;
            }));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Dieb.Werkzeug1.wirkung.einbruchBonus")), string.Join(" | ", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Dieb.Verkleidung.wirkung.fangFaktor")), string.Join(" | ", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Dieb.Handschuhe.wirkung.spurenFaktor")), string.Join(" | ", fehler));
            Pruefe.Gleich(3, fehler.Count);
        }),

        ("Dieb-Einrichtung: Schmugglerloch kann Einrichtung kaufen (Anschaffung), Wachturm und Klosterhof nicht; Meisterdietriche erst ab Stufe 2", () =>
        {
            var sim = NeueSimulation(Unerwischt.Value);
            var (w, k, b) = Dieb(sim, vermoegen: 5_000);
            Pruefe.Wahr(w.Werkstaetten.KannEinrichtung(b), "Schmugglerloch");
            var liste = w.Werkstaetten.Einrichtungsliste(b);
            Pruefe.Wahr(liste.Count == 8 && liste.All(e => !e.Gekauft), "sieben Stücke und die Lehrlingsstube (PR #224), keins gekauft");
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(4_700, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(300, k.Laufend.Investitionen, 1e-9);
            Abgelehnt(w, k, new EinrichtungKaufen(b.Name, "Werkzeug2"), "erst ab Stufe 2");
            Abgelehnt(w, k, new EinrichtungKaufen(b.Name, "Blasebalg"), "kein Stück „Blasebalg“");
            Pruefe.Wahr(w.Werkstaetten.Einrichtungsliste(b).Single(e => e.Kennung == "Werkzeug2").Verboten!.Contains("Stufe 2"), "Verbotsgrund in der Liste");
            Pruefe.Ungefaehr(300, w.Werkstaetten.Einrichtungswert(b), 1e-9);

            var wk = w.KontoEroeffnen("Haus Wacht", startvermoegen: 5_000);
            var wache = w.BetriebEroeffnen("Wachturm Wacht", Betriebsart.Wachturm, wk, startkarren: ""); // das Wachhaus in der Stadt hat Einrichtung (GardistEinrichtungTests)
            var hof = w.BetriebEroeffnen("Klosterhof Wacht", Betriebsart.Klosterhof, wk, startkarren: "");
            foreach (var x in new[] { wache, hof })
            {
                Pruefe.Wahr(!w.Werkstaetten.KannEinrichtung(x), $"{x.Name} hat keine Einrichtung");
                Pruefe.Gleich(0, w.Werkstaetten.Einrichtungsliste(x).Count);
                Abgelehnt(w, wk, new EinrichtungKaufen(x.Name, "Loeschfass"), "kein Stück");
            }
        }),

        ("Dieb-Einrichtung: Dietriche +0,1 auf die Einbruchchance (absolut, vor dem Wächter), Meisterdietriche nochmals; die Einrichtung bleibt beim Ausbau", () =>
        {
            var sim = NeueSimulation(Unerwischt.Value);
            var (w, k, b) = Dieb(sim);
            var (_, g) = Opfer(w);
            var gauner = w.Gauner.Daten;
            var ohne = w.Diebe.Einbruchchance(g, k);
            Pruefe.Ungefaehr(gauner.Diebstahl.Chance + w.Diebe.Daten.Einbruch.ChanceBonus, ohne, 1e-12);
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug1"));
            Ok(w, k, new EinrichtungKaufen(b.Name, "Regale"));
            Pruefe.Ungefaehr(0.1, b.EinbruchBonus, 1e-12);
            Pruefe.Ungefaehr(ohne + 0.1, w.Diebe.Einbruchchance(g, k), 1e-12);
            g.Waechter = true; // der Wächter halbiert auch den Bonus
            Pruefe.Ungefaehr((ohne + 0.1) * gauner.WaechterSchutz, w.Diebe.Einbruchchance(g, k), 1e-12);
            Pruefe.Gleich(60, b.LagerKapazitaetJeWare);

            b.Ausbauen(Betriebsart.Unterschlupf, 0);
            Pruefe.Wahr(b.HatEinrichtung("Werkzeug1") && b.HatEinrichtung("Regale") && b.LagerKapazitaetJeWare == 60, "Einrichtung nach dem Ausbau");
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug2"));
            Pruefe.Ungefaehr(0.2, b.EinbruchBonus, 1e-12);
            Pruefe.Ungefaehr((ohne + 0.2) * gauner.WaechterSchutz, w.Diebe.Einbruchchance(g, k), 1e-12);
            g.Waechter = false;
            Pruefe.Ungefaehr(Math.Min(0.95, ohne + 0.2), w.Diebe.Einbruchchance(g, k), 1e-12); // gedeckelt bei 95 %

            // Ohne Dietriche geht es nicht: erst die einfachen.
            var sim2 = NeueSimulation(Unerwischt.Value);
            var (w2, k2, b2) = Dieb(sim2, Betriebsart.Unterschlupf);
            Abgelehnt(w2, k2, new EinrichtungKaufen(b2.Name, "Werkzeug2"), "Erst braucht es: Dietriche");
        }),

        ("Dieb-Einrichtung: Die Hehlerkammer fasst 20 Stück mehr Beute je Ware – ein Einbruch bringt mehr ins eigene Lager", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(Json
                .Replace("\"fangJeStunde\": 0.012", "\"fangJeStunde\": 0")
                .Replace("\"diebstahl\": { \"kosten\": 150, \"chance\": 0.7", "\"diebstahl\": { \"kosten\": 150, \"chance\": 1")
                .Replace("\"zeugenChance\": 0.25", "\"zeugenChance\": 0"));
            foreach (var kammer in new[] { false, true })
            {
                var sim = NeueSimulation(daten, startwert: 7);
                var (w, k, b) = Dieb(sim, Betriebsart.Diebesgilde);
                var (opfer, g) = Opfer(w);
                g.Einlagern("Dolch", 30); // 50 Dolche im Lager, die Diebesgilde trägt 24
                b.Einlagern("Dolch", 30); // schon 30 im eigenen Lager: Platz für 10 (ohne) oder 30 (mit Kammer)
                if (kammer) Ok(w, k, new EinrichtungKaufen(b.Name, "Regale"));
                Ok(w, k, new Einbrechen(opfer.Inhaber, g.Name));
                while (sim.Zeit.UhrzeitInMinuten <= 20 * 60) sim.Tick();
                var gestohlen = 50 - g.ImLager("Dolch");
                Pruefe.Wahr(gestohlen == 24, $"Einbruch gelungen ({gestohlen} Stück)");
                Pruefe.Gleich(kammer ? 54 : 40, b.ImLager("Dolch"), kammer ? "mit Hehlerkammer" : "ohne Hehlerkammer");
            }
        }),

        ("Dieb-Einrichtung: Verkleidungstruhe – Taschendiebe 25 % seltener gefasst", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Dieb(sim);
            var vorher = w.Diebe.Fangchance(b);
            Pruefe.Ungefaehr(w.Diebe.Fangchance(k, b.Stufe), vorher, 1e-15);
            Ok(w, k, new EinrichtungKaufen(b.Name, "Verkleidung"));
            Pruefe.Ungefaehr(0.75, b.FangFaktor, 1e-12);
            Pruefe.Ungefaehr(vorher * 0.75, w.Diebe.Fangchance(b), 1e-15);
            Pruefe.Ungefaehr(vorher, w.Diebe.Fangchance(k, b.Stufe), 1e-15); // die Chance ohne Betrieb bleibt
        }),

        ("Dieb-Einrichtung: Lederhandschuhe – Zeugen der eigenen Aufträge, Auskundschafter und Razzien finden 25 % seltener etwas", () =>
        {
            var sim = NeueSimulation(Unerwischt.Value);
            var (w, k, b) = Dieb(sim);
            var (opfer, g) = Opfer(w);
            var wache = w.KontoEroeffnen("Haus Wacht", startvermoegen: 5_000);
            var vorher = (Einbruch: w.Diebe.Zeugenchance(Diebesstueck.Einbruch, g, true, k), Brand: w.Diebe.Zeugenchance(Diebesstueck.Brandstiftung, g, false, k),
                Ausspaehen: w.Diebe.Auskundschaftchance(opfer, k), Razzia: w.Wache.Razziachance(wache, k));
            Pruefe.Ungefaehr(w.Gauner.Zeugenchance(g, true, k) * w.Diebe.Daten.Einbruch.ZeugenFaktor, vorher.Einbruch, 1e-15);
            Pruefe.Ungefaehr(w.Wache.Razziachance(wache), vorher.Razzia, 1e-15);
            Ok(w, k, new EinrichtungKaufen(b.Name, "Handschuhe"));
            Pruefe.Ungefaehr(0.75, w.Diebe.SpurenFaktor(k), 1e-12);
            Pruefe.Ungefaehr(vorher.Einbruch * 0.75, w.Diebe.Zeugenchance(Diebesstueck.Einbruch, g, true, k), 1e-15);
            Pruefe.Ungefaehr(vorher.Brand * 0.75, w.Diebe.Zeugenchance(Diebesstueck.Brandstiftung, g, false, k), 1e-15);
            Pruefe.Ungefaehr(vorher.Ausspaehen * 0.75, w.Diebe.Auskundschaftchance(opfer, k), 1e-15);
            Pruefe.Ungefaehr(vorher.Razzia * 0.75, w.Wache.Razziachance(wache, k), 1e-15);
            // Die Handschuhe schützen nur das eigene Haus: Wer selbst ausspäht, findet so viel wie vorher.
            Pruefe.Ungefaehr(Math.Min(0.95, w.Diebe.Daten.Auskundschaften.Chance), w.Diebe.Auskundschaftchance(k, opfer), 1e-15);
            // Ohne Leute im Versteck wirken sie nicht.
            w.Einstellen(b, "Geselle", 0);
            Pruefe.Ungefaehr(1, w.Diebe.SpurenFaktor(k), 1e-15);
        }),

        ("Dieb-Einrichtung: Auskundschaften beim Haus mit Lederhandschuhen – von 400 Taten werden etwa 71 % statt 95 % gefunden", () =>
        {
            foreach (var handschuhe in new[] { false, true })
            {
                var sim = NeueSimulation(Spuersinn.Value, startwert: 11);
                var w = sim.Wirtschaft!;
                var (_, spaeher, _) = Dieb(sim, name: "Familie Elster");
                var (_, ziel, zb) = Dieb(sim, name: "Familie Krähe");
                if (handschuhe) Ok(w, ziel, new EinrichtungKaufen(zb.Name, "Handschuhe"));
                for (var i = 0; i < 400; i++) w.Justiz.TatMelden(ziel, Vergehen.Diebstahl, $"Opfer {i}", 10);
                Ok(w, spaeher, new Auskundschaften(ziel.Inhaber));
                w.Diebe.Nacht();
                var gefunden = w.Justiz.SpurenVon(ziel).Count(s => w.Justiz.KenntSpur(spaeher, s));
                var erwartet = 400 * 0.95 * (handschuhe ? 0.75 : 1);
                Pruefe.Wahr(Math.Abs(gefunden - erwartet) < 25, $"{(handschuhe ? "mit" : "ohne")} Handschuhe: {gefunden} gefunden, erwartet ≈ {erwartet}");
            }
        }),

        ("Dieb-Anwesenheit: Schmugglerloch mit 4 Leuten – tagsüber 2 am Markt und 2 daheim (reihum), abends alle daheim beim Zählen und Planen, kein Schlafboden", () =>
        {
            var sim = NeueSimulation(Unerwischt.Value);
            var (w, k, b) = Dieb(sim);
            Pruefe.Gleich("1:Daheim:Schleifen 2:Daheim:Ueben 3:Daheim:Schleifen 4:Daheim:Ueben", Kurz(w.Diebe.AnwesenheitJetzt(b))); // 4 Uhr: Nummer + Stunde gerade = Üben
            var daheimGewesen = new HashSet<int>();
            for (var h = 8; h < 20; h++)
            {
                BisUhrzeit(sim, h, 30);
                var jetzt = w.Diebe.AnwesenheitJetzt(b);
                Pruefe.Gleich(2, jetzt.Count(s => s.Ort == Diebesort.Markt), $"{h} Uhr am Markt");
                Pruefe.Wahr(jetzt.Where(s => s.Ort == Diebesort.Markt).All(s => s.Taetigkeit == Diebestaetigkeit.Taschendiebstahl), "am Markt: Taschendiebstahl");
                Pruefe.Wahr(jetzt.Where(s => s.Ort == Diebesort.Daheim).All(s => s.Taetigkeit is Diebestaetigkeit.Ueben or Diebestaetigkeit.Schleifen), $"{h} Uhr daheim: üben oder schleifen");
                foreach (var s in jetzt.Where(s => s.Ort == Diebesort.Daheim)) daheimGewesen.Add(s.Arbeiter.Nummer);
            }
            Pruefe.Gleich(4, daheimGewesen.Count, "jeder war einmal daheim");
            BisUhrzeit(sim, 10);
            Pruefe.Gleich("1:Markt:Taschendiebstahl 2:Markt:Taschendiebstahl 3:Daheim:Schleifen 4:Daheim:Ueben", Kurz(w.Diebe.AnwesenheitJetzt(b)));
            BisUhrzeit(sim, 20, 15);
            Pruefe.Gleich("1:Daheim:Planen 2:Daheim:Zaehlen 3:Daheim:Planen 4:Daheim:Zaehlen", Kurz(w.Diebe.AnwesenheitJetzt(b)));
            BisUhrzeit(sim, 21, 30);
            Pruefe.Wahr(w.Diebe.AnwesenheitJetzt(b).All(s => s.Ort == Diebesort.Daheim && s.Taetigkeit is Diebestaetigkeit.Zaehlen or Diebestaetigkeit.Planen),
                "im Schmugglerloch schläft niemand");
            Pruefe.Wahr(w.Diebe.AnwesenheitJetzt(b).All(s => !s.Meisterdieb), "kein Meisterdieb unter Stufe 3");
        }),

        ("Dieb-Anwesenheit: Unterschlupf – nachts sind zwei mit Einbruch und Auskundschaften unterwegs (Gesellen vor Lehrlingen), die anderen schlafen ab 21 Uhr; am Morgen sind alle zurück", () =>
        {
            var sim = NeueSimulation(Unerwischt.Value);
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Elster", startvermoegen: 10_000);
            var b = w.BetriebEroeffnen("Unterschlupf Elster", Betriebsart.Unterschlupf, k, startkarren: "");
            w.Einstellen(b, "Lehrling", 1);
            w.Einstellen(b, "Geselle", 3);
            var (opfer, g) = Opfer(w);
            Ok(w, k, new Einbrechen(opfer.Inhaber, g.Name));
            Ok(w, k, new Auskundschaften(opfer.Inhaber));
            BisUhrzeit(sim, 19, 59);
            Pruefe.Wahr(w.Diebe.AnwesenheitJetzt(b).All(s => s.Ort != Diebesort.Auftrag), "vor 20 Uhr ist noch niemand unterwegs");
            BisUhrzeit(sim, 20, 1);
            var nacht = w.Diebe.AnwesenheitJetzt(b);
            Pruefe.Gleich("1:Daheim:Planen 2:Auftrag:Auftrag 3:Auftrag:Auftrag 4:Daheim:Zaehlen", Kurz(nacht));
            Pruefe.Gleich(Diebesstueck.Einbruch, nacht.Single(s => s.Arbeiter.Nummer == 2).Auftrag);
            Pruefe.Gleich(Diebesstueck.Auskundschaften, nacht.Single(s => s.Arbeiter.Nummer == 3).Auftrag);
            Pruefe.Gleich("Lehrling", nacht.Single(s => s.Arbeiter.Nummer == 1).Arbeiter.Arbeitskraft);
            BisUhrzeit(sim, 21, 0);
            Pruefe.Gleich("1:Daheim:Schlafen 2:Auftrag:Auftrag 3:Auftrag:Auftrag 4:Daheim:Schlafen", Kurz(w.Diebe.AnwesenheitJetzt(b)));
            BisUhrzeit(sim, 5, 0); // neues Jahr
            Pruefe.Gleich("1:Daheim:Schlafen 2:Daheim:Schlafen 3:Daheim:Schlafen 4:Daheim:Schlafen", Kurz(w.Diebe.AnwesenheitJetzt(b)));
            BisUhrzeit(sim, 7, 0);
            Pruefe.Gleich("1:Daheim:Ueben 2:Daheim:Schleifen 3:Daheim:Ueben 4:Daheim:Schleifen", Kurz(w.Diebe.AnwesenheitJetzt(b)));
            BisUhrzeit(sim, 20, 30); // diese Nacht ohne Auftrag: alle daheim
            Pruefe.Wahr(w.Diebe.AnwesenheitJetzt(b).All(s => s.Ort == Diebesort.Daheim), "ohne Auftrag bleiben alle daheim");
        }),

        ("Dieb-Anwesenheit: Diebesgilde – der dienstälteste Geselle ist Meisterdieb und sitzt von 8 bis 22 Uhr in der Gildenhalle; allein ist er keiner", () =>
        {
            var sim = NeueSimulation(Unerwischt.Value);
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Elster", startvermoegen: 10_000);
            var b = w.BetriebEroeffnen("Diebesgilde Elster", Betriebsart.Diebesgilde, k, startkarren: "");
            w.Einstellen(b, "Lehrling", 1);
            w.Einstellen(b, "Geselle", 3);
            Pruefe.Gleich(2, w.Diebe.Meisterdieb(b)!.Nummer);
            Pruefe.Wahr(!w.Diebe.MeisterdiebInGildenhalle(b), "um 5 Uhr schläft er");
            Pruefe.Gleich(Diebestaetigkeit.Schlafen, w.Diebe.AnwesenheitJetzt(b).Single(s => s.Meisterdieb).Taetigkeit);
            for (var h = 8; h < 22; h++)
            {
                BisUhrzeit(sim, h);
                var jetzt = w.Diebe.AnwesenheitJetzt(b);
                Pruefe.Wahr(w.Diebe.MeisterdiebInGildenhalle(b), $"{h} Uhr in der Gildenhalle");
                var meister = jetzt.Single(s => s.Meisterdieb);
                Pruefe.Wahr(meister.Arbeiter.Nummer == 2 && meister.Ort == Diebesort.Daheim && meister.Taetigkeit == Diebestaetigkeit.Vorsitzen, $"{h} Uhr: Vorsitzen");
                if (h < 20) Pruefe.Gleich(2, jetzt.Count(s => s.Ort == Diebesort.Markt), $"{h} Uhr: von den drei anderen sind zwei am Markt");
            }
            // Nur ein Arbeiter: Er geht selbst auf den Markt – ein Meisterdieb ohne Leute ist keiner.
            var sim2 = NeueSimulation(Unerwischt.Value);
            var (w2, _, b2) = Dieb(sim2, Betriebsart.Diebesgilde, leute: 1);
            Pruefe.Gleich<Arbeiter?>(null, w2.Diebe.Meisterdieb(b2));
            BisUhrzeit(sim2, 10);
            Pruefe.Gleich("1:Markt:Taschendiebstahl", Kurz(w2.Diebe.AnwesenheitJetzt(b2)));
        }),

        ("Dieb-Anwesenheit: Abfragen ändern nichts (gleicher Verlauf mit und ohne Abfrage); kein Diebesbetrieb oder ohne Leute: leer; Umbau: alle daheim", () =>
        {
            string Lauf(bool abfragen)
            {
                var sim = NeueSimulation(startwert: 1401);
                var (w, k, b) = Dieb(sim, Betriebsart.Unterschlupf, leute: 6);
                var (opfer, g) = Opfer(w);
                Ok(w, k, new Einbrechen(opfer.Inhaber, g.Name));
                for (var i = 0; i < 2 * Spielzeit.MinutenProTag; i++)
                {
                    sim.Tick();
                    if (!abfragen) continue;
                    _ = w.Diebe.AnwesenheitJetzt(b);
                    _ = w.Diebe.MeisterdiebInGildenhalle(b);
                    _ = w.Diebe.AnwesenheitJetzt(g);
                }
                return string.Join(";", w.Konten.Select(x => $"{x.Inhaber}:{x.Vermoegen:R}")) +
                       $"|{w.Diebe.DiesesJahr(k)}|{b.Belegt}|{b.ImLager("Dolch")}|{w.Justiz.SpurenVon(k).Count()}";
            }
            Pruefe.Gleich(Lauf(false), Lauf(true));

            var sim3 = NeueSimulation(Unerwischt.Value);
            var (w3, k3, b3) = Dieb(sim3, leute: 0);
            var (_, g3) = Opfer(w3);
            Pruefe.Gleich(0, w3.Diebe.AnwesenheitJetzt(b3).Count);
            Pruefe.Gleich(0, w3.Diebe.AnwesenheitJetzt(g3).Count);
            w3.Einstellen(b3, "Geselle", 2);
            b3.Ausbauen(Betriebsart.Unterschlupf, 600);
            BisUhrzeit(sim3, 10);
            Pruefe.Wahr(w3.Diebe.AnwesenheitJetzt(b3).All(s => s.Ort == Diebesort.Daheim), "beim Umbau geht niemand auf den Markt");
        }),
    };
}
