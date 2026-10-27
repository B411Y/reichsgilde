using Reichsgilde.Core.Zeit;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Ordnung;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für den Innenraum des Gardisten: Einrichtung (Hellebarden, Armbrüste, Wachhunde, Kettenhemden …), Anwesenheit und Gefangene.</summary>
static class GardistEinrichtungTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Jeder Taschendieb wird in der ersten Stunde erwischt.</summary>
    static readonly Lazy<Wirtschaftsdaten> Tolpatschig = new(() => Wirtschaftsdaten.AusJson(Json.Replace("\"fangJeStunde\": 0.012", "\"fangJeStunde\": 1")));

    /// <summary>Niemand wird am Markt erwischt.</summary>
    static readonly Lazy<Wirtschaftsdaten> Unerwischt = new(() => Wirtschaftsdaten.AusJson(Json.Replace("\"fangJeStunde\": 0.012", "\"fangJeStunde\": 0")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? Daten.Value);

    /// <summary>Die Daten aus wirtschaft.json, in der Einrichtung des Gardisten verändert.</summary>
    static Wirtschaftsdaten DatenMit(Action<JsonArray> aendern)
    {
        var knoten = JsonNode.Parse(Json, documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!;
        aendern(knoten["werkstatt"]!["einrichtung"]!["Gardist"]!.AsArray());
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    static IReadOnlyList<string> Fehlerliste(Action laden)
    {
        try { laden(); }
        catch (WirtschaftsdatenFehler f) { return f.Fehler; }
        return Array.Empty<string>();
    }

    /// <summary>Ein Haus mit einem Wachbetrieb, Gesellen und Geld, ohne Karren.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb B) Wache(Simulation sim, Betriebsart art = Betriebsart.Wachhaus, int leute = 4,
        string name = "Familie Wacker", double vermoegen = 10_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: vermoegen);
        var b = w.BetriebEroeffnen($"{w.Daten.Art(art).Name} {name}", art, k, startkarren: "");
        if (leute > 0) w.Einstellen(b, "Geselle", leute);
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

    /// <summary>Die Anwesenheit kurz als Text: „Nummer:Ort:Tätigkeit“, nach Nummer.</summary>
    static string Kurz(IEnumerable<Wachstand> liste) =>
        string.Join(" ", liste.OrderBy(s => s.Arbeiter.Nummer).Select(s => $"{s.Arbeiter.Nummer}:{s.Ort}:{s.Taetigkeit}"));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Gardist-Einrichtung: Daten – sieben Stücke mit den Kennungen des Raummodells, Preise wie bei den Werkstätten, Datenversion 52", () =>
        {
            var d = Daten.Value.Werkstatt!;
            Pruefe.Gleich(58, Daten.Value.Version);
            var wache = d.EinrichtungFuer("Gardist");
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Hunde Kettenhemd Regale Loeschfass Eisentuer", string.Join(" ", wache.Select(e => e.Kennung)));
            Pruefe.Gleich("Hellebarden, Armbrüste, Wachhunde, Kettenhemden, Waffenkammer, Löschfass, Eisentür", string.Join(", ", wache.Select(e => e.Name)));
            Pruefe.Gleich("300 800 350 350 200 100 150", string.Join(" ", wache.Select(e => e.Preis))); // Eisentür 150 seit PR #177
            Pruefe.Gleich("1 2 1 1 1 1 1", string.Join(" ", wache.Select(e => e.AbStufe)));
            Pruefe.Gleich("Werkzeug1", d.Stueck("Gardist", "Werkzeug2")!.Voraussetzung);
            Pruefe.Ungefaehr(0.1, d.Stueck("Gardist", "Werkzeug1")!.Wirkung.RazziaBonus, 1e-12);
            Pruefe.Ungefaehr(0.1, d.Stueck("Gardist", "Werkzeug2")!.Wirkung.RazziaBonus, 1e-12);
            Pruefe.Ungefaehr(1.25, d.Stueck("Gardist", "Hunde")!.Wirkung.StreifenFaktor, 1e-12);
            Pruefe.Ungefaehr(0.8, d.Stueck("Gardist", "Kettenhemd")!.Wirkung.GeleitFaktor, 1e-12);
            Pruefe.Ungefaehr(0.9, d.Stueck("Gardist", "Regale")!.Wirkung.LohnFaktor, 1e-12);
            Pruefe.Gleich(0, d.Stueck("Gardist", "Regale")!.Wirkung.LagerPlus);
            Pruefe.Gleich("Lohn der Leute 10 % niedriger", d.Stueck("Gardist", "Regale")!.Wirkung.Text());
            Pruefe.Ungefaehr(0.5, d.Stueck("Gardist", "Loeschfass")!.Wirkung.BrandFaktor, 1e-12);
            Pruefe.Ungefaehr(0.5, d.Stueck("Gardist", "Eisentuer")!.Wirkung.EinbruchFaktor, 1e-12);
            Pruefe.Gleich("+10 % Beweise bei Razzien", d.Stueck("Gardist", "Werkzeug1")!.Wirkung.Text());
            Pruefe.Gleich("Streife schützt 25 % mehr", d.Stueck("Gardist", "Hunde")!.Wirkung.Text());
            Pruefe.Gleich("Überfälle auf das Geleit 20 % seltener erfolgreich", d.Stueck("Gardist", "Kettenhemd")!.Wirkung.Text());
            // Die anderen Berufe kennen die neuen Wirkungen nicht: Ihre Stücke bleiben ohne.
            foreach (var (beruf, liste) in d.Einrichtung.Where(x => x.Key != "Gardist"))
                Pruefe.Wahr(liste.All(e => e.Wirkung is { RazziaBonus: 0, StreifenFaktor: 1, GeleitFaktor: 1, LohnFaktor: 1 }), $"{beruf}: keine Wirkung der Wache");
        }),

        ("Gardist-Einrichtung: Daten werden geprüft (Razzia-Bonus, Geleit- und Lohnfaktor zwischen 0 und 1, Streifenfaktor zwischen 1 und 2)", () =>
        {
            var fehler = Fehlerliste(() => DatenMit(a =>
            {
                a[0]!["wirkung"]!["razziaBonus"] = 1.5;
                a[2]!["wirkung"]!["streifenFaktor"] = 0.9;
                a[3]!["wirkung"]!["geleitFaktor"] = -0.1;
                a[4]!["wirkung"]!["streifenFaktor"] = 2.5;
                a[5]!["wirkung"]!["lohnFaktor"] = 1.2;
            }));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Gardist.Werkzeug1.wirkung.razziaBonus")), string.Join(" | ", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Gardist.Hunde.wirkung.streifenFaktor muss zwischen 1 und 2")), string.Join(" | ", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Gardist.Kettenhemd.wirkung.geleitFaktor")), string.Join(" | ", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Gardist.Regale.wirkung.streifenFaktor")), string.Join(" | ", fehler));
            Pruefe.Wahr(fehler.Any(f => f.Contains("Gardist.Loeschfass.wirkung.lohnFaktor")), string.Join(" | ", fehler));
            Pruefe.Gleich(5, fehler.Count);
        }),

        ("Gardist-Einrichtung: Wachhaus, Stadtwache und Garnison können Einrichtung kaufen (Anschaffung), der Wachturm nicht; Armbrüste erst ab Stufe 2 und nach den Hellebarden; die Einrichtung bleibt beim Ausbau", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Wache(sim, vermoegen: 5_000);
            foreach (var art in new[] { Betriebsart.Wachhaus, Betriebsart.Stadtwache, Betriebsart.Garnison })
                Pruefe.Wahr(w.Werkstaetten.KannEinrichtung(w.BetriebEroeffnen($"Probe {art}", art, k, startkarren: "")), $"{art} hat Einrichtung");
            var turm = w.BetriebEroeffnen("Wachturm Wacker", Betriebsart.Wachturm, k, startkarren: "");
            Pruefe.Wahr(!w.Werkstaetten.KannEinrichtung(turm), "Wachturm hat keine Einrichtung");
            Pruefe.Gleich(0, w.Werkstaetten.Einrichtungsliste(turm).Count);
            Abgelehnt(w, k, new EinrichtungKaufen(turm.Name, "Hunde"), "kein Stück");

            var liste = w.Werkstaetten.Einrichtungsliste(b);
            Pruefe.Wahr(liste.Count == 7 && liste.All(e => !e.Gekauft), "sieben Stücke, keins gekauft");
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(4_700, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(300, k.Laufend.Investitionen, 1e-9);
            Abgelehnt(w, k, new EinrichtungKaufen(b.Name, "Werkzeug2"), "erst ab Stufe 2");
            Abgelehnt(w, k, new EinrichtungKaufen(b.Name, "Blasebalg"), "kein Stück „Blasebalg“");
            Pruefe.Wahr(w.Werkstaetten.Einrichtungsliste(b).Single(e => e.Kennung == "Werkzeug2").Verboten!.Contains("Stufe 2"), "Verbotsgrund in der Liste");
            Ok(w, k, new EinrichtungKaufen(b.Name, "Hunde"));
            Ok(w, k, new EinrichtungKaufen(b.Name, "Regale"));
            Pruefe.Ungefaehr(850, w.Werkstaetten.Einrichtungswert(b), 1e-9);

            b.Ausbauen(Betriebsart.Stadtwache, 0);
            Pruefe.Wahr(b.HatEinrichtung("Werkzeug1") && b.HatEinrichtung("Hunde") && b.HatEinrichtung("Regale") && Math.Abs(b.LohnFaktor - 0.9) < 1e-12,
                "Einrichtung nach dem Ausbau");
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug2"));
            Pruefe.Ungefaehr(0.2, b.RazziaBonus, 1e-12);

            // Ohne Hellebarden keine Armbrüste.
            var sim2 = NeueSimulation();
            var (w2, k2, b2) = Wache(sim2, Betriebsart.Stadtwache);
            Abgelehnt(w2, k2, new EinrichtungKaufen(b2.Name, "Werkzeug2"), "Erst braucht es: Hellebarden");
        }),

        ("Gardist-Einrichtung: Hellebarden und Armbrüste – Razzia je +0,1 (absolut wie je Stufe), höchstens 95 %; es zählt die Wachstube", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Wache(sim);
            Pruefe.Ungefaehr(0.5, w.Wache.Razziachance(k), 1e-12);
            // Hellebarden in einem zweiten, kleineren Wachhaus desselben Hauses zählen nicht – nur die Wachstube.
            var klein = w.BetriebEroeffnen("Wachhaus Zweit", Betriebsart.Wachhaus, k, startkarren: "");
            w.Einstellen(klein, "Geselle", 1);
            Ok(w, k, new EinrichtungKaufen(klein.Name, "Werkzeug1"));
            Pruefe.Wahr(w.Wache.Wachstube(k) == b, "Wachstube ist das erste Wachhaus");
            Pruefe.Ungefaehr(0.5, w.Wache.Razziachance(k), 1e-12);
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(0.6, w.Wache.Razziachance(k), 1e-12);
            b.Ausbauen(Betriebsart.Stadtwache, 0);
            Pruefe.Ungefaehr(0.7, w.Wache.Razziachance(k), 1e-12);
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug2"));
            Pruefe.Ungefaehr(0.8, w.Wache.Razziachance(k), 1e-12);
            b.Ausbauen(Betriebsart.Garnison, 0);
            Pruefe.Ungefaehr(0.9, w.Wache.Razziachance(k), 1e-12);
            Pruefe.Ungefaehr(0.9, w.Wache.Razziachance(k, w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 1_000)), 1e-12);

            // Deckel: Mit 0,8 Grundchance wäre es in der Garnison 1,2.
            var hoch = Wirtschaftsdaten.AusJson(Json.Replace("\"chance\": 0.5, \"chanceJeStufe\": 0.1", "\"chance\": 0.8, \"chanceJeStufe\": 0.1"));
            var sim2 = NeueSimulation(hoch);
            var (w2, k2, b2) = Wache(sim2, Betriebsart.Garnison);
            Pruefe.Ungefaehr(0.95, w2.Wache.Razziachance(k2), 1e-12);
            Ok(w2, k2, new EinrichtungKaufen(b2.Name, "Werkzeug1"));
            Ok(w2, k2, new EinrichtungKaufen(b2.Name, "Werkzeug2"));
            Pruefe.Ungefaehr(0.95, w2.Wache.Razziachance(k2), 1e-12);
        }),

        ("Gardist-Einrichtung: Razzia mit Hellebarden – von 400 Taten werden etwa 60 % statt 50 % gefunden", () =>
        {
            foreach (var hellebarden in new[] { false, true })
            {
                var sim = NeueSimulation(Unerwischt.Value, startwert: 13);
                var (w, k, b) = Wache(sim);
                if (hellebarden) Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug1"));
                var ziel = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
                for (var i = 0; i < 400; i++) w.Justiz.TatMelden(ziel, Vergehen.Diebstahl, $"Opfer {i}", 10);
                k.Aktionspunkte.Setzen(6);
                Ok(w, k, new Razzia(ziel.Inhaber));
                var gefunden = w.Justiz.Beweise(k, ziel).Count();
                var erwartet = 400 * (hellebarden ? 0.6 : 0.5);
                Pruefe.Wahr(Math.Abs(gefunden - erwartet) < 30, $"{(hellebarden ? "mit" : "ohne")} Hellebarden: {gefunden} gefunden, erwartet ≈ {erwartet}");
            }
        }),

        ("Gardist-Einrichtung: Wachhunde – die Streife ihres Hauses schützt 25 % mehr, der Deckel von 40 % bleibt", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Wache(sim);
            var je = w.Wache.Daten.Patrouille.SchutzJeWachmann;
            var ohne = w.Wache.Patrouilleschutz;
            Pruefe.Ungefaehr(je * b.LeistungProMinute, ohne, 1e-12);
            var zeugenVorher = w.Wache.ZeugenBonusInDerStadt;
            Ok(w, k, new EinrichtungKaufen(b.Name, "Hunde"));
            Pruefe.Ungefaehr(1.25, b.StreifenFaktor, 1e-12);
            Pruefe.Ungefaehr(ohne * 1.25, w.Wache.Patrouilleschutz, 1e-12);
            Pruefe.Ungefaehr(1 - ohne * 1.25, w.Wache.Patrouillefaktor(null), 1e-12);
            Pruefe.Ungefaehr(zeugenVorher * 1.25, w.Wache.ZeugenBonusInDerStadt, 1e-12); // die Streife sieht auch mehr

            // Garnison (8) und Stadtwache (6): 14 Wachleute = 35 %; mit Hunden in beiden wären es 43,75 % – gedeckelt bei 40 %.
            var sim2 = NeueSimulation();
            var (w2, k2, garnison) = Wache(sim2, Betriebsart.Garnison, leute: 8);
            var (_, k3, stadtwache) = Wache(sim2, Betriebsart.Stadtwache, leute: 6, name: "Familie Hüter");
            var summe = garnison.LeistungProMinute + stadtwache.LeistungProMinute;
            Pruefe.Ungefaehr(je * summe, w2.Wache.Patrouilleschutz, 1e-12);
            Ok(w2, k2, new EinrichtungKaufen(garnison.Name, "Hunde"));
            Pruefe.Ungefaehr(je * (garnison.LeistungProMinute * 1.25 + stadtwache.LeistungProMinute), w2.Wache.Patrouilleschutz, 1e-12);
            Ok(w2, k3, new EinrichtungKaufen(stadtwache.Name, "Hunde"));
            Pruefe.Wahr(je * summe * 1.25 > w2.Wache.Daten.Patrouille.Hoechstens, "ohne Deckel mehr als 40 %");
            Pruefe.Ungefaehr(w2.Wache.Daten.Patrouille.Hoechstens, w2.Wache.Patrouilleschutz, 1e-12);
        }),

        ("Gardist-Einrichtung: Kettenhemden – Überfälle auf Karren mit dem Geleit der Wache gelingen 20 % seltener; es zählt die Geleitstube", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(Json.Replace("\"chanceTag\": 0.08", "\"chanceTag\": 1"));
            var erbeutet = new int[2];
            foreach (var kettenhemden in new[] { false, true })
            {
                var sim = NeueSimulation(daten, startwert: 17);
                var (w, k, b) = Wache(sim);
                var eskorten = w.Daten.Logistik.Eskorten;
                var kunde = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
                var grube = w.BetriebEroeffnen("Grube Eisenbart", Betriebsart.Mine, kunde);
                var vorher = Kalkulation.ErwarteteFahrtkosten(w, "Stadtwache", 1_000);
                var (_, k2, klein) = Wache(sim, leute: 2, name: "Familie Zweit");
                if (kettenhemden)
                {
                    // Im zweiten, kleineren Wachhaus wirken sie nicht – das Geleit stellt die Geleitstube.
                    Ok(w, k2, new EinrichtungKaufen(klein.Name, "Kettenhemd"));
                    Pruefe.Wahr(w.Wache.Geleitstube == b, "Geleitstube ist das volle Wachhaus");
                    Pruefe.Ungefaehr(1, w.Wache.GeleitFaktor, 1e-15);
                    Ok(w, k, new EinrichtungKaufen(b.Name, "Kettenhemd"));
                    Pruefe.Ungefaehr(0.8, w.Wache.GeleitFaktor, 1e-12);
                    Pruefe.Ungefaehr(0.8, w.Wache.UeberfallFaktor(eskorten["Stadtwache"]), 1e-12);
                    Pruefe.Ungefaehr(1, w.Wache.UeberfallFaktor(eskorten["Schwer"]), 1e-15); // gewöhnliche Eskorten bleiben
                    // Die KI rechnet mit: Das Risiko der Fahrt mit der Stadtwache sinkt um 20 %.
                    var kosten = eskorten["Stadtwache"].KostenJeFahrt;
                    Pruefe.Ungefaehr((vorher - kosten) * 0.8, Kalkulation.ErwarteteFahrtkosten(w, "Stadtwache", 1_000) - kosten, 1e-9);
                }
                else
                    Pruefe.Ungefaehr(1, w.Wache.UeberfallFaktor(eskorten["Stadtwache"]), 1e-15);
                w.Meldung += m => { if (m.StartsWith("Überfall") && m.Contains("erbeutet")) erbeutet[kettenhemden ? 1 : 0]++; };
                for (var i = 0; i < 4_000; i++) Ueberfall.Wuerfeln(grube.Karren[0], eskorten["Stadtwache"], sim.Zeit, w);
            }
            var verhaeltnis = erbeutet[1] / (double)erbeutet[0];
            Pruefe.Wahr(erbeutet[0] > 300 && Math.Abs(verhaeltnis - 0.8) < 0.08, $"Überfälle gelungen: ohne {erbeutet[0]}, mit Kettenhemden {erbeutet[1]} (× {verhaeltnis:0.00})");
        }),

        ("Gardist-Einrichtung: Waffenkammer – Lohn der Wachleute ×0,9 (am Jahresende gebucht), der Sold der Stadt bleibt", () =>
        {
            foreach (var kammer in new[] { false, true })
            {
                var sim = NeueSimulation(startwert: 19);
                var (w, k, b) = Wache(sim);
                var lohn = b.Lohnkosten;
                Pruefe.Ungefaehr(4 * w.Daten.Lohn("Geselle", b.Art), lohn, 1e-9);
                if (kammer)
                {
                    Ok(w, k, new EinrichtungKaufen(b.Name, "Regale"));
                    Pruefe.Ungefaehr(0.9, b.LohnFaktor, 1e-12);
                    Pruefe.Ungefaehr(lohn * 0.9, b.Lohnkosten, 1e-9);
                }
                JahrBeenden(sim);
                Pruefe.Ungefaehr(lohn * (kammer ? 0.9 : 1), k.Vorjahr!.Personal, 1e-6);
                Pruefe.Wahr(w.Wache.Vorjahr(k)!.Sold > 0, "Sold der Stadt");
            }
            // Mit oder ohne Kammer: Der Sold ist derselbe.
            string Sold(bool kammer)
            {
                var sim = NeueSimulation(startwert: 19);
                var (w, k, b) = Wache(sim);
                if (kammer) Ok(w, k, new EinrichtungKaufen(b.Name, "Regale"));
                JahrBeenden(sim);
                return $"{w.Wache.Vorjahr(k)!.Sold:R}";
            }
            Pruefe.Gleich(Sold(false), Sold(true));
        }),

        ("Gardist-Einrichtung: Löschfass und Eisentür halbieren Feuer und Einbruch im Wachhaus", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Wache(sim);
            var feuer = w.Gauner.Erfolgschance(Gaunerstueck.Sabotage, b);
            var einbruch = w.Gauner.Erfolgschance(Gaunerstueck.Diebstahl, b);
            var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 5_000);
            var brand = w.Diebe.Brandchance(b, dieb);
            Ok(w, k, new EinrichtungKaufen(b.Name, "Loeschfass"));
            Ok(w, k, new EinrichtungKaufen(b.Name, "Eisentuer"));
            Pruefe.Ungefaehr(feuer * 0.5, w.Gauner.Erfolgschance(Gaunerstueck.Sabotage, b), 1e-15);
            Pruefe.Ungefaehr(einbruch * 0.5, w.Gauner.Erfolgschance(Gaunerstueck.Diebstahl, b), 1e-15);
            Pruefe.Ungefaehr(brand * 0.5, w.Diebe.Brandchance(b, dieb), 1e-15);
        }),

        ("Gardist-Anwesenheit: Wachhaus mit 4 Leuten – tags 2 auf Streife, abends und morgens 1, Wachwechsel alle zwei Stunden; daheim putzen, schärfen, Wachbuch, würfeln, schlafen", () =>
        {
            var sim = NeueSimulation();
            var (w, _, b) = Wache(sim);
            Pruefe.Gleich("1:Daheim:Schlafen 2:Daheim:Schlafen 3:Patrouille:Streife 4:Daheim:Schlafen", Kurz(w.Wache.AnwesenheitJetzt(b))); // 5 Uhr
            BisUhrzeit(sim, 7);
            Pruefe.Gleich("1:Daheim:Schaerfen 2:Daheim:Putzen 3:Daheim:Schaerfen 4:Patrouille:Streife", Kurz(w.Wache.AnwesenheitJetzt(b)));
            var streifeGewesen = new HashSet<int>();
            var daheimGewesen = new HashSet<int>();
            for (var h = 8; h < 20; h++)
            {
                BisUhrzeit(sim, h, 30);
                var jetzt = w.Wache.AnwesenheitJetzt(b);
                Pruefe.Gleich(2, jetzt.Count(s => s.Ort == Wachort.Patrouille), $"{h} Uhr auf Streife");
                Pruefe.Wahr(jetzt.Where(s => s.Ort == Wachort.Patrouille).All(s => s.Taetigkeit == Wachtaetigkeit.Streife), "auf Patrouille: Streife");
                var daheim = jetzt.Where(s => s.Ort == Wachort.Daheim).ToList();
                Pruefe.Wahr(daheim.All(s => s.Taetigkeit is Wachtaetigkeit.Putzen or Wachtaetigkeit.Schaerfen or Wachtaetigkeit.Wachbuch),
                    $"{h} Uhr daheim: putzen, schärfen oder Wachbuch (ohne Fechtboden)");
                Pruefe.Wahr(daheim[0].Taetigkeit != daheim[1].Taetigkeit, $"{h} Uhr: die beiden daheim tun Verschiedenes");
                foreach (var s in jetzt) (s.Ort == Wachort.Patrouille ? streifeGewesen : daheimGewesen).Add(s.Arbeiter.Nummer);
            }
            Pruefe.Gleich(4, streifeGewesen.Count, "jeder war auf Streife");
            Pruefe.Gleich(4, daheimGewesen.Count, "jeder war daheim");
            BisUhrzeit(sim, 10);
            Pruefe.Gleich("1:Daheim:Schaerfen 2:Patrouille:Streife 3:Patrouille:Streife 4:Daheim:Wachbuch", Kurz(w.Wache.AnwesenheitJetzt(b)));
            BisUhrzeit(sim, 11, 59);
            Pruefe.Gleich("1:Daheim:Wachbuch 2:Patrouille:Streife 3:Patrouille:Streife 4:Daheim:Putzen", Kurz(w.Wache.AnwesenheitJetzt(b)));
            BisUhrzeit(sim, 12);
            Pruefe.Gleich("1:Daheim:Putzen 2:Daheim:Schaerfen 3:Patrouille:Streife 4:Patrouille:Streife", Kurz(w.Wache.AnwesenheitJetzt(b))); // Wachwechsel
            BisUhrzeit(sim, 20, 15);
            Pruefe.Gleich("1:Daheim:Wuerfeln 2:Daheim:Putzen 3:Patrouille:Streife 4:Daheim:Wuerfeln", Kurz(w.Wache.AnwesenheitJetzt(b)));
            BisUhrzeit(sim, 21, 30);
            Pruefe.Gleich("1:Daheim:Schlafen 2:Daheim:Schlafen 3:Patrouille:Streife 4:Daheim:Schlafen", Kurz(w.Wache.AnwesenheitJetzt(b)));
            Pruefe.Wahr(w.Wache.AnwesenheitJetzt(b).All(s => !s.Hauptmann && s.Karren is null), "kein Hauptmann, kein Geleit");
            Pruefe.Gleich<Arbeiter?>(null, w.Wache.Hauptmann(b));
        }),

        ("Gardist-Anwesenheit: Stadtwache – ab Stufe 2 fechten sie daheim; mit 6 Leuten tags 3 und nachts 2 auf Streife; allein nachts daheim", () =>
        {
            var sim = NeueSimulation();
            var (w, _, b) = Wache(sim, Betriebsart.Stadtwache, leute: 6);
            Pruefe.Gleich(2, w.Wache.AnwesenheitJetzt(b).Count(s => s.Ort == Wachort.Patrouille), "5 Uhr");
            var gefochten = false;
            for (var h = 8; h < 20; h++)
            {
                BisUhrzeit(sim, h);
                var jetzt = w.Wache.AnwesenheitJetzt(b);
                Pruefe.Gleich(3, jetzt.Count(s => s.Ort == Wachort.Patrouille), $"{h} Uhr");
                gefochten |= jetzt.Any(s => s.Taetigkeit == Wachtaetigkeit.Fechten);
            }
            Pruefe.Wahr(gefochten, "in der Stadtwache wird gefochten");
            BisUhrzeit(sim, 10);
            Pruefe.Gleich("1:Patrouille:Streife 2:Patrouille:Streife 3:Daheim:Schaerfen 4:Daheim:Wachbuch 5:Daheim:Fechten 6:Patrouille:Streife",
                Kurz(w.Wache.AnwesenheitJetzt(b)));
            BisUhrzeit(sim, 20, 30);
            Pruefe.Gleich(2, w.Wache.AnwesenheitJetzt(b).Count(s => s.Ort == Wachort.Patrouille), "20 Uhr");

            var sim2 = NeueSimulation();
            var (w2, _, b2) = Wache(sim2, leute: 1);
            Pruefe.Gleich("1:Daheim:Schlafen", Kurz(w2.Wache.AnwesenheitJetzt(b2)));
            BisUhrzeit(sim2, 10);
            Pruefe.Gleich("1:Patrouille:Streife", Kurz(w2.Wache.AnwesenheitJetzt(b2)));
            BisUhrzeit(sim2, 20);
            Pruefe.Gleich("1:Daheim:Wuerfeln", Kurz(w2.Wache.AnwesenheitJetzt(b2)));
        }),

        ("Gardist-Anwesenheit: Garnison – der dienstälteste Geselle ist Hauptmann und sitzt von 8 bis 22 Uhr in der Hauptmannsstube; allein ist er keiner", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Wacker", startvermoegen: 10_000);
            var b = w.BetriebEroeffnen("Garnison Wacker", Betriebsart.Garnison, k, startkarren: "");
            w.Einstellen(b, "Lehrling", 1);
            w.Einstellen(b, "Geselle", 3);
            Pruefe.Gleich(2, w.Wache.Hauptmann(b)!.Nummer);
            Pruefe.Wahr(!w.Wache.HauptmannInStube(b), "um 5 Uhr schläft er");
            Pruefe.Gleich("1:Daheim:Schlafen 2:Daheim:Schlafen 3:Daheim:Schlafen 4:Patrouille:Streife", Kurz(w.Wache.AnwesenheitJetzt(b)));
            Pruefe.Wahr(w.Wache.AnwesenheitJetzt(b).Single(s => s.Hauptmann).Arbeiter.Nummer == 2, "Hauptmann auch nachts gekennzeichnet");
            for (var h = 8; h < 22; h++)
            {
                BisUhrzeit(sim, h);
                var jetzt = w.Wache.AnwesenheitJetzt(b);
                Pruefe.Wahr(w.Wache.HauptmannInStube(b), $"{h} Uhr in der Hauptmannsstube");
                var hauptmann = jetzt.Single(s => s.Hauptmann);
                Pruefe.Wahr(hauptmann.Arbeiter.Nummer == 2 && hauptmann.Ort == Wachort.Daheim && hauptmann.Taetigkeit == Wachtaetigkeit.Befehligen, $"{h} Uhr: Befehligen");
                Pruefe.Gleich(h < 20 ? 2 : 1, jetzt.Count(s => s.Ort == Wachort.Patrouille), $"{h} Uhr: von den drei anderen auf Streife");
            }
            var sim2 = NeueSimulation();
            var (w2, _, b2) = Wache(sim2, Betriebsart.Garnison, leute: 1);
            Pruefe.Gleich<Arbeiter?>(null, w2.Wache.Hauptmann(b2));
            BisUhrzeit(sim2, 10);
            Pruefe.Gleich("1:Patrouille:Streife", Kurz(w2.Wache.AnwesenheitJetzt(b2)));
        }),

        ("Gardist-Anwesenheit: Geleit – fährt ein beladener Karren mit der Stadtwache, begleitet ihn ein Geselle der Geleitstube; zurück daheim ist er wieder in der Wache", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Wacker", startvermoegen: 10_000);
            var b = w.BetriebEroeffnen("Wachhaus Wacker", Betriebsart.Wachhaus, k, startkarren: "");
            w.Einstellen(b, "Lehrling", 1);
            w.Einstellen(b, "Geselle", 3);
            var (_, _, klein) = Wache(sim, leute: 2, name: "Familie Zweit");
            var kunde = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
            var grube = w.BetriebEroeffnen("Grube Eisenbart", Betriebsart.Mine, kunde);
            Ok(w, kunde, new EskorteWaehlen(grube.Name, 0, "Stadtwache"));
            var karren = grube.Karren[0];
            Pruefe.Gleich(0, w.Wache.GeleitKarren.Count);
            grube.Einlagern("Eisen", 30);
            for (var i = 0; i < 600 && karren.Zustand == KarrenZustand.AmBetrieb; i++) sim.Tick();
            Pruefe.Gleich(KarrenZustand.ZumMarkt, karren.Zustand);
            Pruefe.Wahr(w.Wache.GeleitKarren.Single() == karren, "der Karren fährt mit Geleit");
            var jetzt = w.Wache.AnwesenheitJetzt(b);
            var geleit = jetzt.Single(s => s.Ort == Wachort.Geleit);
            Pruefe.Wahr(geleit.Arbeiter.Nummer == 2 && geleit.Taetigkeit == Wachtaetigkeit.Geleit && geleit.Karren == karren, "Geselle 2 (vor dem Lehrling) geleitet");
            Pruefe.Wahr(w.Wache.AnwesenheitJetzt(klein).All(s => s.Ort != Wachort.Geleit), "das andere Wachhaus stellt kein Geleit");
            for (var i = 0; i < 1_000 && (karren.Zustand != KarrenZustand.AmBetrieb); i++) sim.Tick();
            Pruefe.Gleich(KarrenZustand.AmBetrieb, karren.Zustand);
            Pruefe.Wahr(w.Wache.AnwesenheitJetzt(b).All(s => s.Ort != Wachort.Geleit), "zurück: niemand mehr im Geleit");
            Pruefe.Gleich(0, w.Wache.GeleitKarren.Count);
        }),

        ("Gardist-Gefangene: gefasste Taschendiebe sitzen in der Arrestzelle der Wachstube (die letzten zuerst, höchstens 6), auch nach dem Laden; am Gerichtstag ist sie leer", () =>
        {
            var sim = NeueSimulation(Tolpatschig.Value);
            var (w, wache, b) = Wache(sim);
            var zweit = w.BetriebEroeffnen("Wachhaus Zweit", Betriebsart.Wachhaus, wache, startkarren: "");
            w.Einstellen(zweit, "Geselle", 1);
            var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 5_000);
            var loch = w.BetriebEroeffnen("Schmugglerloch Elster", Betriebsart.Schmugglerloch, dieb, startkarren: "");
            w.Einstellen(loch, "Geselle", 4);
            w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
            Pruefe.Gleich(0, w.Wache.Gefangene(b).Count);
            BisUhrzeit(sim, 9, 1); // die erste Arbeitsstunde ist abgerechnet: einer gefasst
            var erster = w.Wache.Gefangene(b).Single();
            Pruefe.Wahr(erster.Herkunft == dieb && erster.Vergehen == Vergehen.Taschendiebstahl, "der Dieb der Elsters");
            Pruefe.Gleich(w.Justiz.SpurenVon(dieb).Single().Nummer, erster.Spur);
            Pruefe.Gleich(0, w.Wache.Gefangene(zweit).Count); // nur die Wachstube hat die Zelle
            BisUhrzeit(sim, 12, 1);
            Pruefe.Gleich(0, loch.Belegt);
            var vier = w.Wache.Gefangene(b);
            Pruefe.Gleich(4, vier.Count);
            Pruefe.Wahr(vier.Select(g => g.Spur).SequenceEqual(vier.Select(g => g.Spur).OrderByDescending(x => x)), "die zuletzt gefassten zuerst");

            // Nach dem Laden dieselbe Zelle – sie folgt aus den Spuren des Gerichts.
            var (kopie, _) = Speicherung.Laden(Speicherung.Speichern(sim, wache));
            var kw = kopie.Wirtschaft!;
            var kb = kw.Betriebe.Single(x => x.Name == b.Name);
            Pruefe.Gleich(string.Join(" ", vier.Select(g => $"{g.Spur}:{g.Herkunft.Inhaber}")),
                string.Join(" ", kw.Wache.Gefangene(kb).Select(g => $"{g.Spur}:{g.Herkunft.Inhaber}")));

            // Höchstens 6; andere Taten (Diebstahl) sitzen nicht in der Zelle.
            for (var i = 0; i < 4; i++)
                w.Justiz.BeweisGeben(wache, w.Justiz.TatMelden(dieb, Vergehen.Taschendiebstahl, $"Bürger {i}", 5)!);
            w.Justiz.BeweisGeben(wache, w.Justiz.TatMelden(dieb, Vergehen.Diebstahl, "Gießerei", 100)!);
            var sechs = w.Wache.Gefangene(b);
            Pruefe.Gleich(Wachwesen.HoechstensGefangene, sechs.Count);
            Pruefe.Gleich("Bürger 3", sechs[0].Betroffener);
            Pruefe.Wahr(sechs.All(g => g.Vergehen == Vergehen.Taschendiebstahl), "nur Taschendiebe");

            JahrBeenden(sim);
            Pruefe.Gleich(0, w.Wache.Gefangene(b).Count);
        }),

        ("Gardist-Anwesenheit: Abfragen ändern nichts (gleicher Verlauf mit und ohne Abfrage); kein Wachhaus in der Stadt oder ohne Leute: leer; Umbau: niemand auf Streife", () =>
        {
            string Lauf(bool abfragen)
            {
                var sim = NeueSimulation(startwert: 1401);
                var (w, k, b) = Wache(sim, Betriebsart.Garnison, leute: 8);
                Ok(w, k, new EinrichtungKaufen(b.Name, "Kettenhemd"));
                Ok(w, k, new EinrichtungKaufen(b.Name, "Hunde"));
                var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 5_000);
                var loch = w.BetriebEroeffnen("Schmugglerloch Elster", Betriebsart.Schmugglerloch, dieb, startkarren: "");
                w.Einstellen(loch, "Geselle", 4);
                var kunde = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
                var grube = w.BetriebEroeffnen("Grube Eisenbart", Betriebsart.Mine, kunde);
                Ok(w, kunde, new EskorteWaehlen(grube.Name, 0, "Stadtwache"));
                for (var i = 0; i < 2 * Spielzeit.MinutenProTag; i++)
                {
                    sim.Tick();
                    if (!abfragen) continue;
                    _ = w.Wache.AnwesenheitJetzt(b);
                    _ = w.Wache.HauptmannInStube(b);
                    _ = w.Wache.Gefangene(b);
                    _ = w.Wache.GeleitKarren;
                    _ = w.Wache.AnwesenheitJetzt(grube);
                }
                return string.Join(";", w.Konten.Select(x => $"{x.Inhaber}:{x.Vermoegen:R}")) +
                       $"|{w.Wache.DiesesJahr(k)}|{loch.Belegt}|{grube.ImLager("Eisen")}|{w.Justiz.SpurenVon(dieb).Count()}";
            }
            Pruefe.Gleich(Lauf(false), Lauf(true));

            var sim3 = NeueSimulation();
            var (w3, k3, b3) = Wache(sim3, leute: 0);
            var turm = w3.BetriebEroeffnen("Wachturm Wacker", Betriebsart.Wachturm, k3, startkarren: "");
            w3.Einstellen(turm, "Bergmann", 3);
            Pruefe.Gleich(0, w3.Wache.AnwesenheitJetzt(b3).Count);
            Pruefe.Gleich(0, w3.Wache.AnwesenheitJetzt(turm).Count);
            Pruefe.Gleich(0, w3.Wache.Gefangene(turm).Count);
            w3.Einstellen(b3, "Geselle", 2);
            b3.Ausbauen(Betriebsart.Stadtwache, 600);
            BisUhrzeit(sim3, 10);
            Pruefe.Wahr(w3.Wache.AnwesenheitJetzt(b3).All(s => s.Ort == Wachort.Daheim), "beim Umbau geht niemand auf Streife");
        }),
    };
}
