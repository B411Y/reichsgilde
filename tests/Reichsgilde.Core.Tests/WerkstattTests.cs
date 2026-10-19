using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für die Werkstatt wie in Gilde 1: Arbeiter mit eigener Ware, Beförderung, Lohn und Laune, Unfälle, Einrichtung.</summary>
static class WerkstattTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 10, 19, 12, 0, 0);

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? Daten.Value);

    /// <summary>Die Daten aus wirtschaft.json, im Abschnitt „werkstatt“ verändert.</summary>
    static Wirtschaftsdaten DatenMit(Action<JsonNode> aendern)
    {
        var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()),
            documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!;
        aendern(knoten["werkstatt"]!);
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    /// <summary>Unfälle in der Schmiede sehr häufig (Gefahr je Arbeiter und Jahr), beim Tischler gar nicht.</summary>
    static Wirtschaftsdaten VieleUnfaelle(double gefahr = 1.0) => DatenMit(w =>
    {
        w["unfall"]!["gefahr"]!["Schmied"] = gefahr;
        w["unfall"]!["gefahr"]!.AsObject().Remove("Tischler");
    });

    /// <summary>Ein Haus mit Gießerei ohne Karren (nichts wird verkauft oder gekauft), Lager mit Eisen und Silber gefüllt.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb G) Giesserei(Simulation sim, double vermoegen = 20_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: vermoegen);
        var g = w.BetriebEroeffnen("Gießerei am Weinmarkt", Betriebsart.Giesserei, k, startkarren: "");
        g.Einlagern("Eisen", 40);
        g.Einlagern("Silber", 40);
        return (w, k, g);
    }

    static void BisUhrzeit(Simulation sim, int stunde)
    {
        while (sim.Zeit.UhrzeitInMinuten < stunde * 60) sim.Tick();
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

    static IReadOnlyList<string> Fehlerliste(Action laden)
    {
        try { laden(); }
        catch (WirtschaftsdatenFehler f) { return f.Fehler; }
        return Array.Empty<string>();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Werkstatt: Daten – Abschnitt „werkstatt“ mit drei Lohnstufen und genau den sieben Stücken des Schmieds (Kennungen wie im Raummodell)", () =>
        {
            var d = Daten.Value.Werkstatt!;
            Pruefe.Gleich(36, Daten.Value.Version);
            Pruefe.Ungefaehr(1, d.Stufe(Lohnstufe.Ueblich).LohnFaktor, 1e-12);
            Pruefe.Gleich(0.0, d.Stufe(Lohnstufe.Ueblich).Laune);
            Pruefe.Wahr(d.Stufe(Lohnstufe.Knapp).LohnFaktor < 1 && d.Stufe(Lohnstufe.Gut).LohnFaktor > 1, "Lohnfaktoren");
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Blasebalg Regale Loeschfass Eisentuer Schuerzen",
                string.Join(" ", d.EinrichtungFuer("Schmied").Select(e => e.Kennung)));
            Pruefe.Gleich("Werkzeug1", d.Stueck("Schmied", "Werkzeug2")!.Voraussetzung);
            Pruefe.Gleich(2, d.Stueck("Schmied", "Werkzeug2")!.AbStufe);
            Pruefe.Ungefaehr(0.5, d.Stueck("Schmied", "Schuerzen")!.Wirkung.UnfallFaktor, 1e-12);
            Pruefe.Wahr(d.Unfall.Gefahr["Schmied"] >= d.Unfall.Gefahr.Values.Max(), "Die Schmiede ist am gefährlichsten");
        }),

        ("Werkstatt: Daten werden geprüft (Wirkung, Voraussetzung, Kennung ohne Umlaut, Beruf, Laune)", () =>
        {
            var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()),
                documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!;
            var schmied = knoten["werkstatt"]!["einrichtung"]!["Schmied"]!.AsArray();
            schmied[0]!["wirkung"]!["zeitFaktor"] = 1.5;
            schmied[1]!["voraussetzung"] = "Amboss";
            schmied[4]!["kennung"] = "Löschfass";
            knoten["werkstatt"]!["unfall"]!["gefahr"]!["Bäcker"] = 0.1;
            knoten["werkstatt"]!["laune"]!["tempoBis"] = 1.5;
            var fehler = Fehlerliste(() => Wirtschaftsdaten.AusJson(knoten.ToJsonString()));
            var alle = string.Join(" | ", fehler);
            foreach (var text in new[] { "zeitFaktor", "„Amboss“", "„Löschfass“ darf nur Buchstaben", "„Bäcker“", "tempoBis" })
                Pruefe.Wahr(alle.Contains(text), $"„{text}“ fehlt in: {alle}");
        }),

        ("Werkstatt: Jeder Arbeiter ist ein eigener Eintrag mit fester Nummer – PersonalSetzen, Einstellen und Entlassen", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 2));
            Pruefe.Gleich("1 2", string.Join(" ", g.Arbeiter.Select(a => a.Nummer)));
            Pruefe.Gleich(2, g.Belegung["Geselle"]);
            Ok(w, k, new ArbeiterEntlassen(g.Name, 1));
            Ok(w, k, new ArbeiterEinstellen(g.Name, "Lehrling", "Beschlag"));
            Pruefe.Gleich("2:Geselle:Silberring 3:Lehrling:Beschlag", string.Join(" ", g.Arbeiter.Select(a => $"{a.Nummer}:{a.Arbeitskraft}:{g.WareVon(a)}")));
            Pruefe.Gleich(1, g.Belegung["Lehrling"]);
            Abgelehnt(w, k, new ArbeiterEinstellen(g.Name, "Geselle"), "nur 2 Arbeitsplätze");
            Abgelehnt(w, k, new ArbeiterEntlassen(g.Name, 1), "keinen Arbeiter Nr. 1");
            Ok(w, k, new PersonalSetzen(g.Name, "Lehrling", 0));
            Pruefe.Gleich("2", string.Join(" ", g.Arbeiter.Select(a => a.Nummer)));
            Pruefe.Wahr(!g.Belegung.ContainsKey("Lehrling"), "Lehrling noch in der Belegung");
        }),

        ("Werkstatt: Zwei Waren zugleich – jeder Geselle an seiner Ware, jede mit eigenem Fortschritt; Zutaten beider zusammen", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 2));
            Ok(w, k, new ArbeiterZuteilen(g.Name, 2, "Beschlag"));
            Pruefe.Gleich("Beschlag Silberring", string.Join(" ", g.WarenInArbeit));
            Pruefe.Gleich(3, g.Zutaten["Eisen"]);  // Silberring 1 + Beschlag 2
            Pruefe.Gleich(1, g.Zutaten["Silber"]);
            BisUhrzeit(sim, 14); // 6 Arbeitsstunden
            Pruefe.Wahr(g.ImLager("Silberring") is >= 6 and <= 8, $"Silberringe: {g.ImLager("Silberring")} (360 / 50 ≈ 7)");
            Pruefe.Wahr(g.ImLager("Beschlag") is >= 9 and <= 11, $"Beschläge: {g.ImLager("Beschlag")} (360 / 35 ≈ 10)");
            Pruefe.Gleich(40 - g.ImLager("Silberring") - 2 * g.ImLager("Beschlag"), g.ImLager("Eisen"));
            var inArbeit = w.Werkstaetten.WarenInArbeit(g);
            Pruefe.Gleich("Beschlag:1 Silberring:1", string.Join(" ", inArbeit.Select(x => $"{x.Ware}:{x.Arbeiter}")));
            Pruefe.Wahr(inArbeit.All(x => x.Fortschritt is >= 0 and < 1 && x.Stillstand is null), "Fortschritt zwischen 0 und 1, kein Stillstand");
            Pruefe.Ungefaehr(g.FortschrittVon("Beschlag") / 35, inArbeit[0].Fortschritt, 1e-9);
        }),

        ("Werkstatt: ProduktWaehlen teilt alle Arbeiter dieser Ware zu (für KI und alte Abläufe); Fortschritt der anderen bleibt liegen", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 2));
            Ok(w, k, new ArbeiterZuteilen(g.Name, 1, "Beschlag"));
            BisUhrzeit(sim, 9);
            var beschlag = g.FortschrittVon("Beschlag");
            Pruefe.Wahr(beschlag > 0, "Fortschritt am Beschlag");
            Ok(w, k, new ProduktWaehlen(g.Name, "Rapier"));
            Pruefe.Wahr(g.Arbeiter.All(a => a.Ware is null && g.WareVon(a) == "Rapier"), "alle am Rapier");
            Pruefe.Gleich("Rapier", string.Join(" ", g.WarenInArbeit));
            Pruefe.Gleich("Beschlag:1 Eisen:1", string.Join(" ", g.Zutaten.OrderBy(z => z.Key, StringComparer.Ordinal).Select(z => $"{z.Key}:{z.Value}")));
            Pruefe.Ungefaehr(beschlag, g.FortschrittVon("Beschlag"), 1e-12);
            Abgelehnt(w, k, new ArbeiterZuteilen(g.Name, 1, "Goldkette"), "nicht herstellen");
            Abgelehnt(w, k, new ArbeiterZuteilen(g.Name, 7, "Beschlag"), "keinen Arbeiter Nr. 7");
            Ok(w, k, new ArbeiterZuteilen(g.Name, 1, null));
            Pruefe.Gleich("Rapier", g.WareVon(g.ArbeiterNr(1)!));
        }),

        ("Werkstatt: Lehrling wird nach zwei Jahren Dienst Geselle – vorher mit Grund abgelehnt, danach Gesellenlohn", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            Ok(w, k, new PersonalSetzen(g.Name, "Lehrling", 1));
            Abgelehnt(w, k, new ArbeiterBefoerdern(g.Name, 1), "noch 2 Jahre");
            Pruefe.Wahr(w.Werkstaetten.Arbeiterliste(g)[0].BefoerderungVerboten is not null, "Abfrage zeigt das Verbot");
            JahreSimulieren(sim, 2);
            Pruefe.Ungefaehr(2 * 120, g.Vorjahr!.Personal + 120, 1e-9); // Vorjahr: ein Lehrlingslohn
            Pruefe.Gleich(null, w.Werkstaetten.BefoerdernVerboten(g, 1));
            Ok(w, k, new ArbeiterBefoerdern(g.Name, 1));
            Pruefe.Gleich("Geselle", g.ArbeiterNr(1)!.Arbeitskraft);
            Pruefe.Gleich(1, g.Belegung["Geselle"]);
            Abgelehnt(w, k, new ArbeiterBefoerdern(g.Name, 1), "nur ein Lehrling");
            JahreSimulieren(sim, 1);
            Pruefe.Ungefaehr(220, g.Vorjahr!.Personal, 1e-9);
        }),

        ("Werkstatt: Laune – bei üblichem Lohn neutral (Tempo 1), guter Lohn hebt sie auf 75 (+10 % Tempo), knapper drückt sie auf 25 (−10 %)", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 2));
            var k2 = w.KontoEroeffnen("Haus Knapp", startvermoegen: 20_000);
            var g2 = w.BetriebEroeffnen("Gießerei Knapp", Betriebsart.Giesserei, k2, startkarren: "");
            Ok(w, k2, new PersonalSetzen(g2.Name, "Geselle", 1));
            var k3 = w.KontoEroeffnen("Haus Ueblich", startvermoegen: 20_000);
            var g3 = w.BetriebEroeffnen("Gießerei Ueblich", Betriebsart.Giesserei, k3, startkarren: "");
            Ok(w, k3, new PersonalSetzen(g3.Name, "Geselle", 1));
            Ok(w, k, new LohnstufeSetzen(g.Name, Lohnstufe.Gut));
            Ok(w, k2, new LohnstufeSetzen(g2.Name, Lohnstufe.Knapp));
            BisUhrzeit(sim, 18); // 10 Arbeitsstunden à 3 Punkte
            Pruefe.Wahr(g.Arbeiter.All(a => a.Laune == 75), string.Join(", ", g.Arbeiter.Select(a => a.Laune)));
            Pruefe.Ungefaehr(1.1, g.TempoFaktor(g.Arbeiter[0]), 1e-12);
            Pruefe.Ungefaehr(2.2, g.LeistungProMinute, 1e-12);
            Pruefe.Gleich(25.0, g2.Arbeiter[0].Laune);
            Pruefe.Ungefaehr(0.9, g2.TempoFaktor(g2.Arbeiter[0]), 1e-12);
            Pruefe.Gleich(50.0, g3.Arbeiter[0].Laune);
            Pruefe.Gleich(1.0, g3.TempoFaktor(g3.Arbeiter[0]));
            Pruefe.Ungefaehr(1.1, w.Werkstaetten.Arbeiterliste(g)[0].TempoFaktor, 1e-12);
        }),

        ("Werkstatt: Lohn am Jahresende nach der Lohnstufe im Mittel der Arbeitszeit – kurz vor der Abrechnung knausern spart nichts", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 2));
            Ok(w, k, new LohnstufeSetzen(g.Name, Lohnstufe.Knapp));
            Pruefe.Ungefaehr(2 * 220 * 0.8, g.Lohnkosten, 1e-12);
            JahreSimulieren(sim, 1);
            Pruefe.Ungefaehr(2 * 220 * 0.8, g.Vorjahr!.Personal, 1e-9);
            Ok(w, k, new LohnstufeSetzen(g.Name, Lohnstufe.Gut));
            BisUhrzeit(sim, 21); // nach der Arbeitszeit zurück auf knapp
            Ok(w, k, new LohnstufeSetzen(g.Name, Lohnstufe.Knapp));
            JahreSimulieren(sim, 1);
            Pruefe.Ungefaehr(2 * 220 * 1.25, g.Vorjahr!.Personal, 1e-9);
        }),

        ("Werkstatt: Prämie kostet je Arbeiter, zählt als Personal und hebt das Laune-Ziel eine Weile", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 2));
            Pruefe.Ungefaehr(60, w.Werkstaetten.PraemieKosten(g), 1e-12);
            Ok(w, k, new PraemieZahlen(g.Name, 2));
            Pruefe.Ungefaehr(30, g.Laufend.Personal, 1e-9);
            Pruefe.Ungefaehr(70, w.Werkstaetten.LauneZiel(g, g.ArbeiterNr(2)!), 1e-12);
            Pruefe.Ungefaehr(50, w.Werkstaetten.LauneZiel(g, g.ArbeiterNr(1)!), 1e-12);
            Ok(w, k, new PraemieZahlen(g.Name));
            Pruefe.Ungefaehr(90, g.Laufend.Personal, 1e-9);
            BisUhrzeit(sim, 15);
            Pruefe.Wahr(g.Arbeiter.All(a => a.Laune > 60), string.Join(", ", g.Arbeiter.Select(a => a.Laune)));
            JahreSimulieren(sim, 2); // nach einem Spieltag ist sie verflogen
            Pruefe.Ungefaehr(50, w.Werkstaetten.LauneZiel(g, g.ArbeiterNr(1)!), 1e-12);
            Pruefe.Wahr(g.Arbeiter.All(a => a.Laune == 50), string.Join(", ", g.Arbeiter.Select(a => a.Laune)));
            var arm = w.KontoEroeffnen("Haus Arm", startvermoegen: 10);
            var ga = w.BetriebEroeffnen("Gießerei Arm", Betriebsart.Giesserei, arm, startkarren: "");
            Abgelehnt(w, arm, new PraemieZahlen(ga.Name), "arbeitet niemand");
            Ok(w, arm, new PersonalSetzen(ga.Name, "Lehrling", 1));
            Abgelehnt(w, arm, new PraemieZahlen(ga.Name), "Die Prämie kostet 30");
        }),

        ("Werkstatt: Wer zu lange missmutig ist, kündigt – mit Nachricht", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 1));
            Ok(w, k, new LohnstufeSetzen(g.Name, Lohnstufe.Knapp));
            g.UnfallLauneBis = long.MaxValue; // knapp und nach einem Unfall: Ziel 10
            var meldungen = new List<string>();
            w.Meldung += meldungen.Add;
            JahreSimulieren(sim, 2);
            Pruefe.Gleich(0, g.Belegt);
            Pruefe.Wahr(meldungen.Any(m => m.Contains("Geselle Nr. 1 kündigt")), string.Join(" | ", meldungen));
        }),

        ("Werkstatt: Unfall – der Arbeiter fällt für den Rest des Tages aus, arbeitet nicht, bekommt aber Lohn; die Laune im Betrieb sinkt", () =>
        {
            var sim = NeueSimulation(VieleUnfaelle());
            var (w, k, g) = Giesserei(sim);
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 2));
            var meldungen = new List<string>();
            w.Meldung += meldungen.Add;
            while (!g.Arbeiter.Any(a => a.FaelltAus(sim.Zeit.VergangeneMinuten))) sim.Tick();
            var verletzt = g.Arbeiter.First(a => a.FaelltAus(sim.Zeit.VergangeneMinuten));
            Pruefe.Wahr(meldungen.Any(m => m.Contains($"Nr. {verletzt.Nummer} hat sich verletzt und fällt für den Rest des Tages aus")), string.Join(" | ", meldungen));
            Pruefe.Gleich((long)Spielzeit.MinutenProTag, verletzt.AusfallBis); // bis zum Jahreswechsel
            Pruefe.Wahr(g.LeistungProMinute <= 1.0 + 1e-9, $"Leistung {g.LeistungProMinute}");
            Pruefe.Wahr(g.UnfallLauneBis > sim.Zeit.VergangeneMinuten, "Laune im Betrieb gedrückt");
            Pruefe.Ungefaehr(35, w.Werkstaetten.LauneZiel(g, verletzt), 1e-12);
            Pruefe.Gleich(verletzt.AusfallBis, w.Werkstaetten.Arbeiterliste(g).Single(z => z.Nummer == verletzt.Nummer).FaelltAusBis);
            JahreSimulieren(sim, 1);
            Pruefe.Ungefaehr(2 * 220, g.Vorjahr!.Personal, 1e-9);
            Pruefe.Wahr(!verletzt.FaelltAus(sim.Zeit.VergangeneMinuten), "Im neuen Jahr arbeitet er wieder");
        }),

        ("Werkstatt: Schürzen halbieren die Unfälle (gezählt über viele Jahre); ohne Gefahr im Beruf keine Unfälle", () =>
        {
            int Zaehlen(bool schuerzen)
            {
                var sim = NeueSimulation(VieleUnfaelle(0.3), startwert: 77);
                var w = sim.Wirtschaft!;
                var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 100_000);
                for (var i = 0; i < 8; i++)
                {
                    var g = w.BetriebEroeffnen($"Gießerei {i}", Betriebsart.Giesserei, k, startkarren: "");
                    Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 2));
                    if (schuerzen) Ok(w, k, new EinrichtungKaufen(g.Name, "Schuerzen"));
                }
                var t = w.BetriebEroeffnen("Tischlerei", Betriebsart.Tischlerei, k, startkarren: "");
                Ok(w, k, new PersonalSetzen(t.Name, "Geselle", 2));
                var zahl = 0;
                w.Meldung += m => { if (m.StartsWith("Unfall in Gießerei")) zahl++; else if (m.StartsWith("Unfall in Tischlerei")) zahl += 1000; };
                JahreSimulieren(sim, 20);
                return zahl;
            }
            var ohne = Zaehlen(false);
            var mit = Zaehlen(true);
            Pruefe.Wahr(ohne < 1000 && mit < 1000, "Unfall in der Tischlerei, obwohl der Tischler hier keine Gefahr hat");
            // Je Arbeiter und Jahr höchstens ein Unfall (danach fehlt er): 1 − (1 − 0,3/12)^12 ≈ 26 %, mit Schürzen ≈ 14 %.
            Pruefe.Wahr(ohne is > 60 and < 110, $"ohne Schürzen {ohne} Unfälle (erwartet ≈ 84)");
            Pruefe.Wahr(mit / (double)ohne is > 0.35 and < 0.72, $"mit Schürzen {mit}, ohne {ohne}");
        }),

        ("Werkstatt: Einrichtung – Werkzeug und Blasebalg sparen Zeit je Stück, Regale geben Lager, Preis als Anschaffung", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim, vermoegen: 5_000);
            Ok(w, k, new EinrichtungKaufen(g.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(4_700, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(300, k.Laufend.Investitionen, 1e-9);
            Pruefe.Ungefaehr(45, g.MinutenJeStueck("Silberring"), 1e-9);
            Ok(w, k, new EinrichtungKaufen(g.Name, "Blasebalg"));
            Pruefe.Ungefaehr(35 * 0.81, g.MinutenJeStueck("Beschlag"), 1e-9);
            Pruefe.Ungefaehr(45, g.MinutenJeStueck("Silberring"), 1e-9); // der Blasebalg hilft beim Schmuck nicht
            Pruefe.Gleich(40, g.LagerKapazitaetJeWare);
            Ok(w, k, new EinrichtungKaufen(g.Name, "Regale"));
            Pruefe.Gleich(60, g.LagerKapazitaetJeWare);
            Abgelehnt(w, k, new EinrichtungKaufen(g.Name, "Regale"), "hat schon");
            Abgelehnt(w, k, new EinrichtungKaufen(g.Name, "Werkzeug2"), "erst ab Stufe 2");
            Abgelehnt(w, k, new EinrichtungKaufen(g.Name, "Amboss"), "kein Stück „Amboss“");
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 1));
            BisUhrzeit(sim, 14);
            Pruefe.Wahr(g.ImLager("Silberring") == 8, $"Silberringe mit Werkzeug: {g.ImLager("Silberring")} (360 / 45 = 8)");
            var liste = w.Werkstaetten.Einrichtungsliste(g);
            Pruefe.Gleich(7, liste.Count);
            Pruefe.Gleich("Werkzeug1 Blasebalg Regale", string.Join(" ", liste.Where(e => e.Gekauft).Select(e => e.Kennung)));
            Pruefe.Wahr(liste.Single(e => e.Kennung == "Werkzeug2").Verboten!.Contains("Stufe 2"), "Verbotsgrund in der Liste");
            Pruefe.Gleich("10 % weniger Zeit je Stück", liste.Single(e => e.Kennung == "Werkzeug1").Wirkung);
            Pruefe.Gleich("Unfälle halb so oft", liste.Single(e => e.Kennung == "Schuerzen").Wirkung);
        }),

        ("Werkstatt: Meisterwerkzeug braucht Stufe 2 und das gute Werkzeug; die Einrichtung bleibt beim Ausbau", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            Ok(w, k, new EinrichtungKaufen(g.Name, "Regale"));
            g.Ausbauen(Betriebsart.Schmiede, 0);
            Pruefe.Wahr(g.HatEinrichtung("Regale") && g.LagerKapazitaetJeWare == 60, "Regale nach dem Ausbau");
            Abgelehnt(w, k, new EinrichtungKaufen(g.Name, "Werkzeug2"), "Erst braucht es: Gutes Werkzeug");
            Ok(w, k, new EinrichtungKaufen(g.Name, "Werkzeug1"));
            Ok(w, k, new EinrichtungKaufen(g.Name, "Werkzeug2"));
            Pruefe.Ungefaehr(50 * 0.81, g.MinutenJeStueck("Silberring"), 1e-9);
        }),

        ("Werkstatt: Eisentür halbiert Diebstahl und Einbruch, Löschfass halbiert Sabotage und Brand", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            var dieb = w.KontoEroeffnen("Haus Dieb");
            var vorher = (w.Gauner.Erfolgschance(Gaunerstueck.Diebstahl, g), w.Gauner.Erfolgschance(Gaunerstueck.Sabotage, g),
                w.Diebe.Einbruchchance(g, dieb), w.Diebe.Brandchance(g, dieb));
            Ok(w, k, new EinrichtungKaufen(g.Name, "Eisentuer"));
            Ok(w, k, new EinrichtungKaufen(g.Name, "Loeschfass"));
            Pruefe.Ungefaehr(vorher.Item1 / 2, w.Gauner.Erfolgschance(Gaunerstueck.Diebstahl, g), 1e-12);
            Pruefe.Ungefaehr(vorher.Item2 / 2, w.Gauner.Erfolgschance(Gaunerstueck.Sabotage, g), 1e-12);
            Pruefe.Ungefaehr(vorher.Item3 / 2, w.Diebe.Einbruchchance(g, dieb), 1e-12);
            Pruefe.Ungefaehr(vorher.Item4 / 2, w.Diebe.Brandchance(g, dieb), 1e-12);
        }),

        ("Werkstatt: Beim Verkauf zählt die Einrichtung zum Wert und bleibt im Haus für den nächsten Käufer", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            w.BetriebEroeffnen("Grube Fuchs", Betriebsart.Mine, k);
            var wert = w.Betriebswert(g);
            Ok(w, k, new EinrichtungKaufen(g.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(wert + 300, w.Betriebswert(g), 1e-9);
            Pruefe.Ungefaehr((wert + 300) * 0.75, w.VerkaufVorschau(g).Gebaeudeerloes, 1e-9);
            Ok(w, k, new BetriebVerkaufen(g.Name));
            var frei = w.FreieLiegenschaften.Single(l => l.Name == g.Name);
            Pruefe.Ungefaehr(wert + 300, frei.Kaufpreis, 1e-9);

            var json = Speicherung.Speichern(sim, k, Zeitpunkt); // die Einrichtung im leeren Haus übersteht das Speichern
            var (geladen, _) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, geladen.Wirtschaft!.Konten.Single(x => x.Inhaber == k.Inhaber), Zeitpunkt));
            var gw = geladen.Wirtschaft!;
            var kaeufer = gw.KontoEroeffnen("Haus Neu", startvermoegen: 10_000);
            Pruefe.Wahr(gw.Ausfuehren(Wohnhaushilfe.MitWohnhaus(gw, kaeufer), new StandErwerben()).Erfolg, "Aufstieg zum Bürger");
            kaeufer.Umsatz(10_000);
            Ok(gw, kaeufer, new LiegenschaftErwerben(g.Name));
            Pruefe.Wahr(gw.Betriebe.Single(b => b.Name == g.Name).HatEinrichtung("Werkzeug1"), "Der Käufer hat das Werkzeug");
        }),

        ("Werkstatt: Speichern und Laden – Arbeiter mit Ware, Laune, Ausfall, Lohnstufe, Einrichtung, Fortschritt je Ware; gleicher Verlauf danach", () =>
        {
            var original = NeueSimulation(VieleUnfaelle(), startwert: 9);
            var (w, k, g) = Giesserei(original);
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 1));
            Ok(w, k, new ArbeiterEinstellen(g.Name, "Lehrling", "Beschlag"));
            Ok(w, k, new LohnstufeSetzen(g.Name, Lohnstufe.Gut));
            Ok(w, k, new EinrichtungKaufen(g.Name, "Werkzeug1"));
            Ok(w, k, new PraemieZahlen(g.Name, 1));
            BisUhrzeit(original, 13);
            original.Tick(); // mitten in einer Stunde
            var json = Speicherung.Speichern(original, k, Zeitpunkt);
            var (kopie, kopieSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(kopie, kopieSpieler, Zeitpunkt));
            var kg = kopie.Wirtschaft!.Betriebe.Single(b => b.Name == g.Name);
            Pruefe.Gleich(string.Join(" ", g.Arbeiter.Select(a => $"{a.Nummer}:{a.Ware}:{a.Laune}:{a.AusfallBis}:{a.PraemieBis}")),
                string.Join(" ", kg.Arbeiter.Select(a => $"{a.Nummer}:{a.Ware}:{a.Laune}:{a.AusfallBis}:{a.PraemieBis}")));
            Pruefe.Gleich(Lohnstufe.Gut, kg.Lohnstufe);
            Pruefe.Wahr(kg.HatEinrichtung("Werkzeug1"), "Werkzeug");
            Pruefe.Ungefaehr(g.FortschrittVon("Beschlag"), kg.FortschrittVon("Beschlag"), 1e-12);
            JahreSimulieren(original, 2);
            JahreSimulieren(kopie, 2);
            Pruefe.Gleich(Speicherung.Speichern(original, k, Zeitpunkt), Speicherung.Speichern(kopie, kopieSpieler, Zeitpunkt));
        }),

        ("Werkstatt: Alter Spielstand – alle Arbeiter an der bisherigen Ware, Laune neutral, keine Einrichtung, Fortschritt bleibt beim Produkt", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 2));
            Ok(w, k, new ProduktWaehlen(g.Name, "Rapier"));
            var knoten = JsonNode.Parse(Speicherung.Speichern(sim, k))!;
            knoten["Daten"]!.AsObject().Remove("Werkstatt");
            knoten["Wirtschaft"]!.AsObject().Remove("WerkstattWuerfe");
            foreach (var b in knoten["Wirtschaft"]!["Betriebe"]!.AsArray())
            {
                foreach (var feld in new[] { "Arbeiter", "NaechsteArbeiterNummer", "FortschrittJeWare", "Lohnstufe", "LohnFaktorSumme", "LohnMinuten", "Einrichtung", "UnfallLauneBis" })
                    b!.AsObject().Remove(feld);
                if (b!["Name"]!.GetValue<string>() == g.Name) b["Fortschritt"] = 20.0;
            }
            var (alt, altSpieler) = Speicherung.Laden(knoten.ToJsonString());
            var aw = alt.Wirtschaft!;
            var ag = aw.Betriebe.Single(b => b.Name == g.Name);
            Pruefe.Gleich("1:Rapier:50 2:Rapier:50", string.Join(" ", ag.Arbeiter.Select(a => $"{a.Nummer}:{ag.WareVon(a)}:{a.Laune}")));
            Pruefe.Ungefaehr(20, ag.FortschrittVon("Rapier"), 1e-12);
            Pruefe.Gleich(0.0, ag.Fortschritt);
            Pruefe.Gleich(0, ag.Einrichtung.Count);
            Pruefe.Gleich(Lohnstufe.Ueblich, ag.Lohnstufe);
            Abgelehnt(aw, altSpieler!, new LohnstufeSetzen(ag.Name, Lohnstufe.Gut), "keine Lohnstufen"); // Daten ohne Abschnitt „werkstatt“
            Pruefe.Gleich(0, aw.Werkstaetten.Einrichtungsliste(ag).Count);
            Ok(aw, altSpieler!, new ArbeiterZuteilen(ag.Name, 2, "Beschlag")); // Zuteilen geht auch ohne den Abschnitt
            JahreSimulieren(alt, 1);
            Pruefe.Gleich(1.0, ag.TempoFaktor(ag.Arbeiter[0]));
            Pruefe.Ungefaehr(2 * 220, ag.Vorjahr!.Personal, 1e-9);
        }),

        ("Werkstatt: Determinismus – gleicher Startwert, gleiche Unfälle", () =>
        {
            List<string> Unfaelle()
            {
                var sim = NeueSimulation(VieleUnfaelle(), startwert: 5);
                var (w, k, g) = Giesserei(sim);
                Ok(w, k, new PersonalSetzen(g.Name, "Geselle", 2));
                var liste = new List<string>();
                w.Meldung += m => { if (m.StartsWith("Unfall")) liste.Add($"{sim.Zeit.VergangeneMinuten} {m}"); };
                JahreSimulieren(sim, 3);
                return liste;
            }
            var a = Unfaelle();
            Pruefe.Wahr(a.Count > 0, "keine Unfälle");
            Pruefe.Gleich(string.Join("\n", a), string.Join("\n", Unfaelle()));
        }),
    };
}
