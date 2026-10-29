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

    /// <summary>Die Daten ohne Abschnitt „personal“ – so laufen alte Spielstände mit ihren eigenen Daten (Beförderung nach Dienstzeit).</summary>
    static readonly Lazy<Wirtschaftsdaten> OhnePersonal = new(() =>
    {
        var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()),
            documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!;
        knoten.AsObject().Remove("personal");
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    });

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

    /// <summary>Ein Haus mit Tischlerei ohne Karren, Lager mit Holz und Beschlag für Truhen gefüllt.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb T) Tischlerei(Simulation sim, double vermoegen = 20_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: vermoegen);
        var t = w.BetriebEroeffnen("Tischlerei am Holzmarkt", Betriebsart.Tischlerei, k, startkarren: "");
        t.Einlagern("Holz", 40);
        t.Einlagern("Beschlag", 20);
        return (w, k, t);
    }

    /// <summary>Ein Haus mit Maurerei ohne Karren, Lager mit Bruchstein und Schleifziegeln für Mühlsteine gefüllt.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb M) Maurerei(Simulation sim, double vermoegen = 20_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: vermoegen);
        var m = w.BetriebEroeffnen("Maurerei an der Stadtmauer", Betriebsart.Maurerei, k, startkarren: "");
        m.Einlagern("Bruchstein", 40);
        m.Einlagern("Schleifziegel", 20);
        return (w, k, m);
    }

    /// <summary>Ein Haus mit Tinkturei ohne Karren, Lager mit Kräutern und Kröten gefüllt.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb T) Tinkturei(Simulation sim, double vermoegen = 20_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: vermoegen);
        var t = w.BetriebEroeffnen("Tinkturei am Domplatz", Betriebsart.Tinkturei, k, startkarren: "");
        t.Einlagern("Kräuter", 40);
        t.Einlagern("Kröte", 20);
        return (w, k, t);
    }

    /// <summary>Ein Haus mit Siederei ohne Karren, Lager mit Lavendel und Jasmin gefüllt.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb S) Siederei(Simulation sim, double vermoegen = 20_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Rosenhain", startvermoegen: vermoegen);
        var s = w.BetriebEroeffnen("Siederei an der Seifengasse", Betriebsart.Siederei, k, startkarren: "");
        s.Einlagern("Lavendel", 40);
        s.Einlagern("Jasmin", 40);
        return (w, k, s);
    }

    /// <summary>Ein Haus mit Schänke ohne Karren, Lager mit Gerste und Roggen gefüllt, Dünnbier für die Gäste (sie nehmen, wovon am meisten da ist).</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb S) Schaenke(Simulation sim, double vermoegen = 20_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Familie Krug", startvermoegen: vermoegen);
        var s = w.BetriebEroeffnen("Schänke am Kornmarkt", Betriebsart.Schaenke, k, startkarren: "");
        s.Einlagern("Gerste", 40);
        s.Einlagern("Roggen", 40);
        s.Einlagern("Dünnbier", 100);
        return (w, k, s);
    }

    /// <summary>Ein Haus mit Kirche ohne Karren, Lager mit Lumpen und Wachs gefüllt.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb B) Kirche(Simulation sim, double vermoegen = 20_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Familie Psalm", startvermoegen: vermoegen);
        var b = w.BetriebEroeffnen("Kirche am Weinmarkt", Betriebsart.Kirche, k, startkarren: "");
        b.Einlagern("Lumpen", 40);
        b.Einlagern("Wachs", 40);
        return (w, k, b);
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
            Pruefe.Gleich(64, Daten.Value.Version);
            Pruefe.Ungefaehr(1, d.Stufe(Lohnstufe.Ueblich).LohnFaktor, 1e-12);
            Pruefe.Gleich(0.0, d.Stufe(Lohnstufe.Ueblich).Laune);
            Pruefe.Wahr(d.Stufe(Lohnstufe.Knapp).LohnFaktor < 1 && d.Stufe(Lohnstufe.Gut).LohnFaktor > 1, "Lohnfaktoren");
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Blasebalg Regale Loeschfass Eisentuer Schuerzen",
                string.Join(" ", d.EinrichtungFuer("Schmied").Take(7).Select(e => e.Kennung))); // dazu ab PR #224 drei Stücke der Stufe 3
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
            w.Einstellen(g, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
            Pruefe.Gleich("1 2", string.Join(" ", g.Arbeiter.Select(a => a.Nummer)));
            Pruefe.Gleich(2, g.Belegung["Geselle"]);
            Ok(w, k, new ArbeiterEntlassen(g.Name, 1));
            Ok(w, k, new ArbeiterEinstellen(g.Name, "Lehrling", "Beschlag"));
            Pruefe.Gleich("2:Geselle:Silberring 3:Lehrling:Beschlag", string.Join(" ", g.Arbeiter.Select(a => $"{a.Nummer}:{a.Arbeitskraft}:{g.WareVon(a)}")));
            Pruefe.Gleich(1, g.Belegung["Lehrling"]);
            Abgelehnt(w, k, new ArbeiterEinstellen(g.Name, "Geselle"), "nur Lehrlinge"); // PR #224
            Abgelehnt(w, k, new ArbeiterEinstellen(g.Name, "Lehrling"), "nur 2 Arbeitsplätze");
            Abgelehnt(w, k, new ArbeiterEntlassen(g.Name, 1), "keinen Arbeiter Nr. 1");
            Ok(w, k, new PersonalSetzen(g.Name, "Lehrling", 0));
            Pruefe.Gleich("2", string.Join(" ", g.Arbeiter.Select(a => a.Nummer)));
            Pruefe.Wahr(!g.Belegung.ContainsKey("Lehrling"), "Lehrling noch in der Belegung");
        }),

        ("Werkstatt: Zwei Waren zugleich – jeder Geselle an seiner Ware, jede mit eigenem Fortschritt; Zutaten beider zusammen", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Giesserei(sim);
            w.Einstellen(g, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
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
            w.Einstellen(g, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
            Ok(w, k, new ArbeiterZuteilen(g.Name, 1, "Beschlag"));
            BisUhrzeit(sim, 9);
            var beschlag = g.FortschrittVon("Beschlag");
            Pruefe.Wahr(beschlag > 0, "Fortschritt am Beschlag");
            Ok(w, k, new ProduktWaehlen(g.Name, "Dolch"));
            Pruefe.Wahr(g.Arbeiter.All(a => a.Ware is null && g.WareVon(a) == "Dolch"), "alle am Dolch");
            Pruefe.Gleich("Dolch", string.Join(" ", g.WarenInArbeit));
            Pruefe.Gleich("Beschlag:1 Eisen:1", string.Join(" ", g.Zutaten.OrderBy(z => z.Key, StringComparer.Ordinal).Select(z => $"{z.Key}:{z.Value}")));
            Pruefe.Ungefaehr(beschlag, g.FortschrittVon("Beschlag"), 1e-12);
            Abgelehnt(w, k, new ArbeiterZuteilen(g.Name, 1, "Goldkette"), "nicht herstellen");
            Abgelehnt(w, k, new ArbeiterZuteilen(g.Name, 7, "Beschlag"), "keinen Arbeiter Nr. 7");
            Ok(w, k, new ArbeiterZuteilen(g.Name, 1, null));
            Pruefe.Gleich("Dolch", g.WareVon(g.ArbeiterNr(1)!));
        }),

        ("Werkstatt: ohne Abschnitt „personal“ (alte Spielstände) wird ein Lehrling nach zwei Jahren Dienst Geselle – vorher mit Grund abgelehnt, danach Gesellenlohn", () =>
        {
            var sim = NeueSimulation(OhnePersonal.Value);
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
            w.Einstellen(g, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
            var k2 = w.KontoEroeffnen("Haus Knapp", startvermoegen: 20_000);
            var g2 = w.BetriebEroeffnen("Gießerei Knapp", Betriebsart.Giesserei, k2, startkarren: "");
            w.Einstellen(g2, "Geselle", 1); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
            var k3 = w.KontoEroeffnen("Haus Ueblich", startvermoegen: 20_000);
            var g3 = w.BetriebEroeffnen("Gießerei Ueblich", Betriebsart.Giesserei, k3, startkarren: "");
            w.Einstellen(g3, "Geselle", 1); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
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
            w.Einstellen(g, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
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
            w.Einstellen(g, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
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
            w.Einstellen(g, "Geselle", 1); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
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
            w.Einstellen(g, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
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
                    w.Einstellen(g, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
                    if (schuerzen) Ok(w, k, new EinrichtungKaufen(g.Name, "Schuerzen"));
                }
                var t = w.BetriebEroeffnen("Tischlerei", Betriebsart.Tischlerei, k, startkarren: "");
                w.Einstellen(t, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
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
            w.Einstellen(g, "Geselle", 1); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
            BisUhrzeit(sim, 14);
            Pruefe.Wahr(g.ImLager("Silberring") == 8, $"Silberringe mit Werkzeug: {g.ImLager("Silberring")} (360 / 45 = 8)");
            var liste = w.Werkstaetten.Einrichtungsliste(g);
            Pruefe.Gleich(10, liste.Count); // sieben, ab PR #224 drei der Stufe 3 dazu
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

        ("Werkstatt: Daten – der Tischler hat sieben Stücke (Kennungen wie im Raummodell) zu den Preisen des Schmieds; jede Ware eines Stücks stellt der Beruf selbst her", () =>
        {
            var d = Daten.Value.Werkstatt!;
            var tischler = d.EinrichtungFuer("Tischler");
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Leimzwingen Regale Loeschfass Eisentuer Schuerzen", string.Join(" ", tischler.Take(7).Select(e => e.Kennung))); // dazu ab PR #224 drei Stücke der Stufe 3
            Pruefe.Gleich("Gutes Werkzeug, Meisterwerkzeug, Leimofen und Zwingen, Holzgestell, Löschfass, Eisentür, Lederschürzen",
                string.Join(", ", tischler.Take(7).Select(e => e.Name)));
            foreach (var e in tischler.Take(7).Where(e => e.Kennung != "Leimzwingen"))
                Pruefe.Gleich(d.Stueck("Schmied", e.Kennung)!.Preis, e.Preis, $"Preis {e.Kennung}");
            Pruefe.Gleich(350.0, d.Stueck("Tischler", "Leimzwingen")!.Preis);
            Pruefe.Gleich("Werkzeug1", d.Stueck("Tischler", "Werkzeug2")!.Voraussetzung);
            Pruefe.Gleich(2, d.Stueck("Tischler", "Werkzeug2")!.AbStufe);
            Pruefe.Gleich("Truhe Schrank Prunktruhe Armbrust", string.Join(" ", d.Stueck("Tischler", "Leimzwingen")!.Wirkung.Waren));
            // Eine Ware, die keine Werkstatt des Berufs herstellt, gibt es zwar, das Stück wirkte aber nie.
            var arten = Enum.GetValues<Betriebsart>().Where(Daten.Value.KenntArt).Select(Daten.Value.Art).ToList();
            foreach (var (beruf, liste) in d.Einrichtung)
                foreach (var ware in liste.SelectMany(e => e.Wirkung.Waren))
                    Pruefe.Wahr(arten.Any(a => a.Beruf == beruf && a.Produkte.Contains(ware)), $"{beruf}: „{ware}“ stellt keine Werkstatt des Berufs her");
        }),

        ("Werkstatt: Tischler – Werkzeug spart Zeit bei allen Waren, Leimofen und Zwingen nur bei Truhe, Schrank, Prunktruhe, Armbrust; Holzgestell gibt Lager", () =>
        {
            var sim = NeueSimulation();
            var (w, k, t) = Tischlerei(sim, vermoegen: 5_000);
            Pruefe.Wahr(w.Werkstaetten.Einrichtungsliste(t) is { Count: 10 } l && l.All(e => !e.Gekauft), "sieben Stücke und drei der Stufe 3 (PR #224), keins gekauft");
            Ok(w, k, new EinrichtungKaufen(t.Name, "Leimzwingen"));
            Pruefe.Ungefaehr(4_650, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(350, k.Laufend.Investitionen, 1e-9);
            foreach (var (ware, zeit) in new[] { ("Truhe", 75.0), ("Schrank", 100.0), ("Prunktruhe", 100.0), ("Armbrust", 70.0) })
                Pruefe.Ungefaehr(zeit * 0.9, t.MinutenJeStueck(ware), 1e-9);
            foreach (var (ware, zeit) in new[] { ("Holzzapfen", 30.0), ("Schemel", 45.0), ("Gehstock", 40.0) })
                Pruefe.Ungefaehr(zeit, t.MinutenJeStueck(ware), 1e-9); // Zwingen helfen beim Drechseln nicht
            Ok(w, k, new EinrichtungKaufen(t.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(75 * 0.81, t.MinutenJeStueck("Truhe"), 1e-9);
            Pruefe.Ungefaehr(45 * 0.9, t.MinutenJeStueck("Schemel"), 1e-9);
            Pruefe.Gleich(40, t.LagerKapazitaetJeWare);
            Ok(w, k, new EinrichtungKaufen(t.Name, "Regale"));
            Pruefe.Gleich(60, t.LagerKapazitaetJeWare);
            foreach (var kennung in new[] { "Loeschfass", "Eisentuer", "Schuerzen" }) Ok(w, k, new EinrichtungKaufen(t.Name, kennung));
            Pruefe.Gleich((0.5, 0.5, 0.5), (t.BrandFaktor, t.EinbruchFaktor, t.UnfallFaktor));
            Pruefe.Ungefaehr(5_000 - 350 - 300 - 200 - 100 - 150 - 80, k.Vermoegen, 1e-9); // Eisentür 150 seit PR #177
            Abgelehnt(w, k, new EinrichtungKaufen(t.Name, "Blasebalg"), "kein Stück „Blasebalg“");
            Abgelehnt(w, k, new EinrichtungKaufen(t.Name, "Werkzeug2"), "erst ab Stufe 2");
            Ok(w, k, new ProduktWaehlen(t.Name, "Truhe"));
            w.Einstellen(t, "Geselle", 1); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
            BisUhrzeit(sim, 14);
            Pruefe.Wahr(t.ImLager("Truhe") == 5, $"Truhen mit Werkzeug und Zwingen: {t.ImLager("Truhe")} (360 / 60,75 ≈ 5,9; ohne beides 360 / 75 = 4,8)");
            var liste = w.Werkstaetten.Einrichtungsliste(t);
            Pruefe.Gleich("10 % weniger Zeit je Stück (Truhe, Schrank, Prunktruhe, Armbrust)", liste.Single(e => e.Kennung == "Leimzwingen").Wirkung);
            Pruefe.Gleich("20 mehr Lager je Ware", liste.Single(e => e.Kennung == "Regale").Wirkung);
        }),

        ("Werkstatt: Tischler – Meisterwerkzeug erst ab der Drechslerei und nur mit gutem Werkzeug; die Einrichtung gilt in allen drei Stufen, nicht im Holzschlag", () =>
        {
            var sim = NeueSimulation();
            var (w, k, t) = Tischlerei(sim);
            Ok(w, k, new EinrichtungKaufen(t.Name, "Regale"));
            Ok(w, k, new EinrichtungKaufen(t.Name, "Leimzwingen"));
            Abgelehnt(w, k, new EinrichtungKaufen(t.Name, "Werkzeug2"), "erst ab Stufe 2");
            t.Ausbauen(Betriebsart.Drechslerei, 0);
            Pruefe.Gleich(10, w.Werkstaetten.Einrichtungsliste(t).Count); // sieben, ab PR #224 drei der Stufe 3 dazu
            Pruefe.Wahr(t.HatEinrichtung("Regale") && t.LagerKapazitaetJeWare == 60, "Holzgestell nach dem Ausbau");
            Abgelehnt(w, k, new EinrichtungKaufen(t.Name, "Werkzeug2"), "Erst braucht es: Gutes Werkzeug");
            Ok(w, k, new EinrichtungKaufen(t.Name, "Werkzeug1"));
            Ok(w, k, new EinrichtungKaufen(t.Name, "Werkzeug2"));
            Pruefe.Ungefaehr(40 * 0.81, t.MinutenJeStueck("Gehstock"), 1e-9);
            Pruefe.Ungefaehr(70 * 0.729, t.MinutenJeStueck("Armbrust"), 1e-9);
            t.Ausbauen(Betriebsart.Saegewerk, 0);
            Pruefe.Gleich(3, t.Stufe);
            var liste = w.Werkstaetten.Einrichtungsliste(t);
            Pruefe.Gleich(10, liste.Count); // sieben, ab PR #224 drei der Stufe 3 dazu
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Leimzwingen Regale", string.Join(" ", liste.Where(e => e.Gekauft).Select(e => e.Kennung)));
            Pruefe.Ungefaehr(100 * 0.729, t.MinutenJeStueck("Schrank"), 1e-9);
            Pruefe.Ungefaehr(100 * 0.729, t.MinutenJeStueck("Prunktruhe"), 1e-9);
            // Der Holzschlag gehört auch zum Tischler, ist aber keine Werkstatt.
            var h = w.BetriebEroeffnen("Holzschlag Fuchs", Betriebsart.Holzfaellerei, k, startkarren: "");
            Pruefe.Gleich(0, w.Werkstaetten.Einrichtungsliste(h).Count);
            Abgelehnt(w, k, new EinrichtungKaufen(h.Name, "Regale"), "kein Stück „Regale“");
        }),

        ("Werkstatt: Daten – der Steinmetz hat sieben Stücke (Kennungen wie im Raummodell) zu den Preisen des Schmieds, mit Flaschenzug statt Blasebalg", () =>
        {
            var d = Daten.Value.Werkstatt!;
            var steinmetz = d.EinrichtungFuer("Steinmetz");
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Flaschenzug Regale Loeschfass Eisentuer Schuerzen", string.Join(" ", steinmetz.Take(7).Select(e => e.Kennung))); // dazu ab PR #224 drei Stücke der Stufe 3
            Pruefe.Gleich("Gutes Werkzeug, Meisterwerkzeug, Flaschenzug, Steingestell, Löschfass, Eisentür, Lederschürzen",
                string.Join(", ", steinmetz.Take(7).Select(e => e.Name)));
            foreach (var e in steinmetz.Take(7).Where(e => e.Kennung != "Flaschenzug"))
            {
                var schmied = d.Stueck("Schmied", e.Kennung)!;
                Pruefe.Gleich(schmied.Preis, e.Preis, $"Preis {e.Kennung}");
                Pruefe.Gleich(schmied.Wirkung.Text(), e.Wirkung.Text(), $"Wirkung {e.Kennung}");
            }
            Pruefe.Gleich(350.0, d.Stueck("Steinmetz", "Flaschenzug")!.Preis);
            Pruefe.Gleich("Werkzeug1", d.Stueck("Steinmetz", "Werkzeug2")!.Voraussetzung);
            Pruefe.Gleich(2, d.Stueck("Steinmetz", "Werkzeug2")!.AbStufe);
            Pruefe.Gleich("Mühlstein Säule Taufbecken Grabmal", string.Join(" ", d.Stueck("Steinmetz", "Flaschenzug")!.Wirkung.Waren));
        }),

        ("Werkstatt: Steinmetz – Werkzeug spart Zeit bei allen Waren, der Flaschenzug nur bei Mühlstein, Säule, Taufbecken, Grabmal; Steingestell gibt Lager", () =>
        {
            var sim = NeueSimulation();
            var (w, k, m) = Maurerei(sim, vermoegen: 5_000);
            Pruefe.Wahr(w.Werkstaetten.Einrichtungsliste(m) is { Count: 10 } l && l.All(e => !e.Gekauft), "sieben Stücke und drei der Stufe 3 (PR #224), keins gekauft");
            Ok(w, k, new EinrichtungKaufen(m.Name, "Flaschenzug"));
            Pruefe.Ungefaehr(4_650, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(350, k.Laufend.Investitionen, 1e-9);
            foreach (var (ware, zeit) in new[] { ("Mühlstein", 60.0), ("Säule", 60.0), ("Taufbecken", 70.0), ("Grabmal", 100.0) })
                Pruefe.Ungefaehr(zeit * 0.9, m.MinutenJeStueck(ware), 1e-9);
            foreach (var (ware, zeit) in new[] { ("Schleifziegel", 35.0), ("Grabstein", 50.0), ("Steinfigur", 100.0) })
                Pruefe.Ungefaehr(zeit, m.MinutenJeStueck(ware), 1e-9); // gehoben wird nur, was schwer ist
            Ok(w, k, new EinrichtungKaufen(m.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(60 * 0.81, m.MinutenJeStueck("Mühlstein"), 1e-9);
            Pruefe.Ungefaehr(50 * 0.9, m.MinutenJeStueck("Grabstein"), 1e-9);
            Pruefe.Gleich(40, m.LagerKapazitaetJeWare);
            Ok(w, k, new EinrichtungKaufen(m.Name, "Regale"));
            Pruefe.Gleich(60, m.LagerKapazitaetJeWare);
            foreach (var kennung in new[] { "Loeschfass", "Eisentuer", "Schuerzen" }) Ok(w, k, new EinrichtungKaufen(m.Name, kennung));
            Pruefe.Gleich((0.5, 0.5, 0.5), (m.BrandFaktor, m.EinbruchFaktor, m.UnfallFaktor));
            Pruefe.Ungefaehr(5_000 - 350 - 300 - 200 - 100 - 150 - 80, k.Vermoegen, 1e-9); // Eisentür 150 seit PR #177
            Abgelehnt(w, k, new EinrichtungKaufen(m.Name, "Leimzwingen"), "kein Stück „Leimzwingen“");
            Abgelehnt(w, k, new EinrichtungKaufen(m.Name, "Werkzeug2"), "erst ab Stufe 2");
            Ok(w, k, new ProduktWaehlen(m.Name, "Mühlstein"));
            w.Einstellen(m, "Geselle", 1); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
            BisUhrzeit(sim, 14);
            Pruefe.Wahr(m.ImLager("Mühlstein") == 7, $"Mühlsteine mit Werkzeug und Flaschenzug: {m.ImLager("Mühlstein")} (360 / 48,6 ≈ 7,4; ohne beides 360 / 60 = 6)");
            var liste = w.Werkstaetten.Einrichtungsliste(m);
            Pruefe.Gleich("10 % weniger Zeit je Stück (Mühlstein, Säule, Taufbecken, Grabmal)", liste.Single(e => e.Kennung == "Flaschenzug").Wirkung);
            Pruefe.Gleich("Steingestell", liste.Single(e => e.Kennung == "Regale").Name);
            Pruefe.Gleich("20 mehr Lager je Ware", liste.Single(e => e.Kennung == "Regale").Wirkung);
        }),

        ("Werkstatt: Steinmetz – Meisterwerkzeug erst ab der Steinschneiderei und nur mit gutem Werkzeug; die Einrichtung gilt in allen drei Stufen, nicht im Steinbruch", () =>
        {
            var sim = NeueSimulation();
            var (w, k, m) = Maurerei(sim);
            Ok(w, k, new EinrichtungKaufen(m.Name, "Regale"));
            Ok(w, k, new EinrichtungKaufen(m.Name, "Flaschenzug"));
            Abgelehnt(w, k, new EinrichtungKaufen(m.Name, "Werkzeug2"), "erst ab Stufe 2");
            m.Ausbauen(Betriebsart.Steinschneiderei, 0);
            Pruefe.Gleich(10, w.Werkstaetten.Einrichtungsliste(m).Count); // sieben, ab PR #224 drei der Stufe 3 dazu
            Pruefe.Wahr(m.HatEinrichtung("Regale") && m.LagerKapazitaetJeWare == 60, "Steingestell nach dem Ausbau");
            Abgelehnt(w, k, new EinrichtungKaufen(m.Name, "Werkzeug2"), "Erst braucht es: Gutes Werkzeug");
            Ok(w, k, new EinrichtungKaufen(m.Name, "Werkzeug1"));
            Ok(w, k, new EinrichtungKaufen(m.Name, "Werkzeug2"));
            Pruefe.Ungefaehr(50 * 0.81, m.MinutenJeStueck("Grabstein"), 1e-9);
            Pruefe.Ungefaehr(70 * 0.729, m.MinutenJeStueck("Taufbecken"), 1e-9);
            m.Ausbauen(Betriebsart.Steinmetzhuette, 0);
            Pruefe.Gleich(3, m.Stufe);
            var liste = w.Werkstaetten.Einrichtungsliste(m);
            Pruefe.Gleich(10, liste.Count); // sieben, ab PR #224 drei der Stufe 3 dazu
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Flaschenzug Regale", string.Join(" ", liste.Where(e => e.Gekauft).Select(e => e.Kennung)));
            Pruefe.Ungefaehr(100 * 0.729, m.MinutenJeStueck("Grabmal"), 1e-9);
            Pruefe.Ungefaehr(100 * 0.81, m.MinutenJeStueck("Steinfigur"), 1e-9);
            // Der Steinbruch gehört auch zum Steinmetz, ist aber keine Werkstatt.
            var s = w.BetriebEroeffnen("Steinbruch Fuchs", Betriebsart.Steinbruch, k, startkarren: "");
            Pruefe.Gleich(0, w.Werkstaetten.Einrichtungsliste(s).Count);
            Abgelehnt(w, k, new EinrichtungKaufen(s.Name, "Regale"), "kein Stück „Regale“");
        }),

        ("Werkstatt: Daten – der Alchimist hat sieben Stücke (Kennungen wie im Raummodell) zu den Preisen des Schmieds, mit Destillierhelm statt Blasebalg und Handschuhen statt Schürzen", () =>
        {
            var d = Daten.Value.Werkstatt!;
            var alchimist = d.EinrichtungFuer("Alchimist");
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Destillierhelm Regale Loeschfass Eisentuer Handschuhe", string.Join(" ", alchimist.Take(7).Select(e => e.Kennung))); // dazu ab PR #224 drei Stücke der Stufe 3
            Pruefe.Gleich("Gutes Werkzeug, Meisterwerkzeug, Kupferner Destillierhelm, Kräuterregale, Löschfass, Eisentür, Lederhandschuhe",
                string.Join(", ", alchimist.Take(7).Select(e => e.Name)));
            foreach (var e in alchimist.Take(7).Where(e => e.Kennung != "Destillierhelm"))
            {
                var schmied = d.Stueck("Schmied", e.Kennung == "Handschuhe" ? "Schuerzen" : e.Kennung)!;
                Pruefe.Gleich(schmied.Preis, e.Preis, $"Preis {e.Kennung}");
                Pruefe.Gleich(schmied.Wirkung.Text(), e.Wirkung.Text(), $"Wirkung {e.Kennung}");
            }
            Pruefe.Gleich(350.0, d.Stueck("Alchimist", "Destillierhelm")!.Preis);
            Pruefe.Gleich("Werkzeug1", d.Stueck("Alchimist", "Werkzeug2")!.Voraussetzung);
            Pruefe.Gleich(2, d.Stueck("Alchimist", "Werkzeug2")!.AbStufe);
            Pruefe.Gleich("Kräuterschnaps Rosenwasser Gegengift Theriak", string.Join(" ", d.Stueck("Alchimist", "Destillierhelm")!.Wirkung.Waren));
            Pruefe.Ungefaehr(0.5, d.Stueck("Alchimist", "Handschuhe")!.Wirkung.UnfallFaktor, 1e-12);
            Pruefe.Wahr(d.Stueck("Alchimist", "Schuerzen") is null, "Der Alchimist hat Handschuhe, keine Schürzen");
        }),

        ("Werkstatt: Alchimist – Werkzeug spart Zeit bei allen Waren, der Destillierhelm nur bei Kräuterschnaps, Rosenwasser, Gegengift, Theriak; Kräuterregale geben Lager", () =>
        {
            var sim = NeueSimulation();
            var (w, k, t) = Tinkturei(sim, vermoegen: 5_000);
            Pruefe.Wahr(w.Werkstaetten.Einrichtungsliste(t) is { Count: 10 } l && l.All(e => !e.Gekauft), "sieben Stücke und drei der Stufe 3 (PR #224), keins gekauft");
            Ok(w, k, new EinrichtungKaufen(t.Name, "Destillierhelm"));
            Pruefe.Ungefaehr(4_650, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(350, k.Laufend.Investitionen, 1e-9);
            foreach (var (ware, zeit) in new[] { ("Kräuterschnaps", 15.0), ("Rosenwasser", 50.0), ("Gegengift", 70.0), ("Theriak", 100.0) })
                Pruefe.Ungefaehr(zeit * 0.9, t.MinutenJeStueck(ware), 1e-9);
            foreach (var (ware, zeit) in new[] { ("Farbe", 30.0), ("Heilsalbe", 50.0), ("Zinnober", 100.0) })
                Pruefe.Ungefaehr(zeit, t.MinutenJeStueck(ware), 1e-9); // gebrannt wird nur, was destilliert wird
            Ok(w, k, new EinrichtungKaufen(t.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(15 * 0.81, t.MinutenJeStueck("Kräuterschnaps"), 1e-9);
            Pruefe.Ungefaehr(50 * 0.9, t.MinutenJeStueck("Heilsalbe"), 1e-9);
            Pruefe.Gleich(40, t.LagerKapazitaetJeWare);
            Ok(w, k, new EinrichtungKaufen(t.Name, "Regale"));
            Pruefe.Gleich(60, t.LagerKapazitaetJeWare);
            foreach (var kennung in new[] { "Loeschfass", "Eisentuer", "Handschuhe" }) Ok(w, k, new EinrichtungKaufen(t.Name, kennung));
            Pruefe.Gleich((0.5, 0.5, 0.5), (t.BrandFaktor, t.EinbruchFaktor, t.UnfallFaktor));
            Pruefe.Ungefaehr(5_000 - 350 - 300 - 200 - 100 - 150 - 80, k.Vermoegen, 1e-9); // Eisentür 150 seit PR #177
            Abgelehnt(w, k, new EinrichtungKaufen(t.Name, "Schuerzen"), "kein Stück „Schuerzen“");
            Abgelehnt(w, k, new EinrichtungKaufen(t.Name, "Flaschenzug"), "kein Stück „Flaschenzug“");
            Abgelehnt(w, k, new EinrichtungKaufen(t.Name, "Werkzeug2"), "erst ab Stufe 2");
            Ok(w, k, new ProduktWaehlen(t.Name, "Kräuterschnaps"));
            w.Einstellen(t, "Geselle", 1); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
            BisUhrzeit(sim, 14);
            Pruefe.Wahr(t.ImLager("Kräuterschnaps") == 29, $"Kräuterschnaps mit Werkzeug und Destillierhelm: {t.ImLager("Kräuterschnaps")} (360 / 12,15 ≈ 29,6; ohne beides 360 / 15 = 24)");
            var liste = w.Werkstaetten.Einrichtungsliste(t);
            Pruefe.Gleich("10 % weniger Zeit je Stück (Kräuterschnaps, Rosenwasser, Gegengift, Theriak)", liste.Single(e => e.Kennung == "Destillierhelm").Wirkung);
            Pruefe.Gleich("Kräuterregale", liste.Single(e => e.Kennung == "Regale").Name);
            Pruefe.Gleich("20 mehr Lager je Ware", liste.Single(e => e.Kennung == "Regale").Wirkung);
            Pruefe.Gleich("Lederhandschuhe", liste.Single(e => e.Kennung == "Handschuhe").Name);
            Pruefe.Gleich("Unfälle halb so oft", liste.Single(e => e.Kennung == "Handschuhe").Wirkung);
            Pruefe.Ungefaehr(0.03 * 0.5, w.Werkstaetten.UnfallGefahr(t), 1e-12); // Gefahr des Alchimisten je Arbeiter und Jahr
        }),

        ("Werkstatt: Alchimist – Lederhandschuhe halbieren die Unfälle in der Tinkturei (Gefahr und gezählt über viele Jahre)", () =>
        {
            int Zaehlen(bool handschuhe)
            {
                var sim = NeueSimulation(DatenMit(x => x["unfall"]!["gefahr"]!["Alchimist"] = 0.3), startwert: 77);
                var w = sim.Wirtschaft!;
                var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 100_000);
                for (var i = 0; i < 8; i++)
                {
                    var t = w.BetriebEroeffnen($"Tinkturei {i}", Betriebsart.Tinkturei, k, startkarren: "");
                    w.Einstellen(t, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
                    if (handschuhe) Ok(w, k, new EinrichtungKaufen(t.Name, "Handschuhe"));
                    Pruefe.Ungefaehr(handschuhe ? 0.15 : 0.3, w.Werkstaetten.UnfallGefahr(t), 1e-12);
                }
                var zahl = 0;
                w.Meldung += m => { if (m.StartsWith("Unfall in Tinkturei")) zahl++; };
                JahreSimulieren(sim, 20);
                return zahl;
            }
            var ohne = Zaehlen(false);
            var mit = Zaehlen(true);
            // Je Arbeiter und Jahr höchstens ein Unfall (danach fehlt er): 1 − (1 − 0,3/12)^12 ≈ 26 %, mit Handschuhen ≈ 14 %.
            Pruefe.Wahr(ohne is > 60 and < 110, $"ohne Handschuhe {ohne} Unfälle (erwartet ≈ 84)");
            Pruefe.Wahr(mit / (double)ohne is > 0.35 and < 0.72, $"mit Handschuhen {mit}, ohne {ohne}");
        }),

        ("Werkstatt: Alchimist – Meisterwerkzeug erst ab dem Kräuterladen und nur mit gutem Werkzeug; die Einrichtung gilt in allen drei Stufen, nicht im Kräutergarten", () =>
        {
            var sim = NeueSimulation();
            var (w, k, t) = Tinkturei(sim);
            Ok(w, k, new EinrichtungKaufen(t.Name, "Regale"));
            Ok(w, k, new EinrichtungKaufen(t.Name, "Destillierhelm"));
            Abgelehnt(w, k, new EinrichtungKaufen(t.Name, "Werkzeug2"), "erst ab Stufe 2");
            t.Ausbauen(Betriebsart.Kraeuterladen, 0);
            Pruefe.Gleich(10, w.Werkstaetten.Einrichtungsliste(t).Count); // sieben, ab PR #224 drei der Stufe 3 dazu
            Pruefe.Wahr(t.HatEinrichtung("Regale") && t.LagerKapazitaetJeWare == 60, "Kräuterregale nach dem Ausbau");
            Abgelehnt(w, k, new EinrichtungKaufen(t.Name, "Werkzeug2"), "Erst braucht es: Gutes Werkzeug");
            Ok(w, k, new EinrichtungKaufen(t.Name, "Werkzeug1"));
            Ok(w, k, new EinrichtungKaufen(t.Name, "Werkzeug2"));
            Pruefe.Ungefaehr(50 * 0.81, t.MinutenJeStueck("Heilsalbe"), 1e-9);
            Pruefe.Ungefaehr(50 * 0.729, t.MinutenJeStueck("Rosenwasser"), 1e-9);
            Pruefe.Ungefaehr(70 * 0.729, t.MinutenJeStueck("Gegengift"), 1e-9);
            t.Ausbauen(Betriebsart.Alchimistenstube, 0);
            Pruefe.Gleich(3, t.Stufe);
            var liste = w.Werkstaetten.Einrichtungsliste(t);
            Pruefe.Gleich(10, liste.Count); // sieben, ab PR #224 drei der Stufe 3 dazu
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Destillierhelm Regale", string.Join(" ", liste.Where(e => e.Gekauft).Select(e => e.Kennung)));
            Pruefe.Ungefaehr(100 * 0.729, t.MinutenJeStueck("Theriak"), 1e-9);
            Pruefe.Ungefaehr(100 * 0.81, t.MinutenJeStueck("Zinnober"), 1e-9);
            Pruefe.Ungefaehr(30 * 0.81, t.MinutenJeStueck("Farbe"), 1e-9);
            // Der Kräutergarten gehört auch zum Alchimisten, ist aber keine Werkstatt.
            var g = w.BetriebEroeffnen("Kräutergarten Fuchs", Betriebsart.Kraeutergarten, k, startkarren: "");
            Pruefe.Gleich(0, w.Werkstaetten.Einrichtungsliste(g).Count);
            Abgelehnt(w, k, new EinrichtungKaufen(g.Name, "Regale"), "kein Stück „Regale“");
            Pruefe.Gleich(0.0, w.Werkstaetten.UnfallGefahr(g));
        }),

        ("Werkstatt: Daten – der Parfumeur hat sieben Stücke (Kennungen wie im Raummodell) zu den Preisen des Schmieds, mit Glasflakons statt Blasebalg und Handschuhen statt Schürzen", () =>
        {
            var d = Daten.Value.Werkstatt!;
            var parfumeur = d.EinrichtungFuer("Parfumeur");
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Glasflakons Regale Loeschfass Eisentuer Handschuhe", string.Join(" ", parfumeur.Take(7).Select(e => e.Kennung))); // dazu ab PR #224 drei Stücke der Stufe 3
            Pruefe.Gleich("Gutes Werkzeug, Meisterwerkzeug, Venezianische Glasflakons, Flakonregale, Löschfass, Eisentür, Lederhandschuhe",
                string.Join(", ", parfumeur.Take(7).Select(e => e.Name)));
            foreach (var e in parfumeur.Take(7).Where(e => e.Kennung != "Glasflakons"))
            {
                var schmied = d.Stueck("Schmied", e.Kennung == "Handschuhe" ? "Schuerzen" : e.Kennung)!;
                Pruefe.Gleich(schmied.Preis, e.Preis, $"Preis {e.Kennung}");
                Pruefe.Gleich(schmied.Wirkung.Text(), e.Wirkung.Text(), $"Wirkung {e.Kennung}");
            }
            Pruefe.Gleich(350.0, d.Stueck("Parfumeur", "Glasflakons")!.Preis);
            Pruefe.Gleich("Werkzeug1", d.Stueck("Parfumeur", "Werkzeug2")!.Voraussetzung);
            Pruefe.Gleich(2, d.Stueck("Parfumeur", "Werkzeug2")!.AbStufe);
            Pruefe.Gleich("Moschusduft Fechterduft Heiligenduft", string.Join(" ", d.Stueck("Parfumeur", "Glasflakons")!.Wirkung.Waren));
            foreach (var ware in d.Stueck("Parfumeur", "Glasflakons")!.Wirkung.Waren)
                Pruefe.Wahr(Daten.Value.Waren.ContainsKey(ware), $"Ware {ware} gibt es");
            Pruefe.Ungefaehr(0.5, d.Stueck("Parfumeur", "Handschuhe")!.Wirkung.UnfallFaktor, 1e-12);
            Pruefe.Wahr(d.Stueck("Parfumeur", "Schuerzen") is null, "Der Parfumeur hat Handschuhe, keine Schürzen");
        }),

        ("Werkstatt: Parfumeur – Werkzeug spart Zeit bei allen Waren, die Glasflakons nur bei Moschusduft, Fechterduft, Heiligenduft; Flakonregale geben Lager", () =>
        {
            var sim = NeueSimulation();
            var (w, k, s) = Siederei(sim, vermoegen: 5_000);
            Pruefe.Wahr(w.Werkstaetten.Einrichtungsliste(s) is { Count: 10 } l && l.All(e => !e.Gekauft), "sieben Stücke und drei der Stufe 3 (PR #224), keins gekauft");
            Ok(w, k, new EinrichtungKaufen(s.Name, "Glasflakons"));
            Pruefe.Ungefaehr(4_650, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(350, k.Laufend.Investitionen, 1e-9);
            foreach (var (ware, zeit) in new[] { ("Moschusduft", 70.0), ("Fechterduft", 60.0), ("Heiligenduft", 100.0) })
                Pruefe.Ungefaehr(zeit * 0.9, s.MinutenJeStueck(ware), 1e-9);
            foreach (var (ware, zeit) in new[] { ("Politur", 30.0), ("Blume der Zwietracht", 50.0), ("Giftdolch", 60.0), ("Schattendolch", 100.0) })
                Pruefe.Ungefaehr(zeit, s.MinutenJeStueck(ware), 1e-9); // abgefüllt wird nur, was in den Flakon kommt
            Ok(w, k, new EinrichtungKaufen(s.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(70 * 0.81, s.MinutenJeStueck("Moschusduft"), 1e-9);
            Pruefe.Ungefaehr(30 * 0.9, s.MinutenJeStueck("Politur"), 1e-9);
            Pruefe.Gleich(40, s.LagerKapazitaetJeWare);
            Ok(w, k, new EinrichtungKaufen(s.Name, "Regale"));
            Pruefe.Gleich(60, s.LagerKapazitaetJeWare);
            foreach (var kennung in new[] { "Loeschfass", "Eisentuer", "Handschuhe" }) Ok(w, k, new EinrichtungKaufen(s.Name, kennung));
            Pruefe.Gleich((0.5, 0.5, 0.5), (s.BrandFaktor, s.EinbruchFaktor, s.UnfallFaktor));
            Pruefe.Ungefaehr(5_000 - 350 - 300 - 200 - 100 - 150 - 80, k.Vermoegen, 1e-9); // Eisentür 150 seit PR #177
            Abgelehnt(w, k, new EinrichtungKaufen(s.Name, "Schuerzen"), "kein Stück „Schuerzen“");
            Abgelehnt(w, k, new EinrichtungKaufen(s.Name, "Destillierhelm"), "kein Stück „Destillierhelm“");
            Abgelehnt(w, k, new EinrichtungKaufen(s.Name, "Werkzeug2"), "erst ab Stufe 2");
            Ok(w, k, new ProduktWaehlen(s.Name, "Politur"));
            w.Einstellen(s, "Geselle", 1); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
            BisUhrzeit(sim, 14);
            Pruefe.Wahr(s.ImLager("Politur") == 13, $"Politur mit Werkzeug: {s.ImLager("Politur")} (360 / 27 ≈ 13,3; ohne Werkzeug 360 / 30 = 12)");
            var liste = w.Werkstaetten.Einrichtungsliste(s);
            Pruefe.Gleich("10 % weniger Zeit je Stück (Moschusduft, Fechterduft, Heiligenduft)", liste.Single(e => e.Kennung == "Glasflakons").Wirkung);
            Pruefe.Gleich("Venezianische Glasflakons", liste.Single(e => e.Kennung == "Glasflakons").Name);
            Pruefe.Gleich("Flakonregale", liste.Single(e => e.Kennung == "Regale").Name);
            Pruefe.Gleich("20 mehr Lager je Ware", liste.Single(e => e.Kennung == "Regale").Wirkung);
            Pruefe.Gleich("Lederhandschuhe", liste.Single(e => e.Kennung == "Handschuhe").Name);
            Pruefe.Gleich("Unfälle halb so oft", liste.Single(e => e.Kennung == "Handschuhe").Wirkung);
            Pruefe.Ungefaehr(0.03 * 0.5, w.Werkstaetten.UnfallGefahr(s), 1e-12); // Gefahr des Parfumeurs je Arbeiter und Jahr
        }),

        ("Werkstatt: Parfumeur – Lederhandschuhe halbieren die Unfälle in der Siederei (Gefahr und gezählt über viele Jahre)", () =>
        {
            int Zaehlen(bool handschuhe)
            {
                var sim = NeueSimulation(DatenMit(x => x["unfall"]!["gefahr"]!["Parfumeur"] = 0.3), startwert: 77);
                var w = sim.Wirtschaft!;
                var k = w.KontoEroeffnen("Haus Rosenhain", startvermoegen: 100_000);
                for (var i = 0; i < 8; i++)
                {
                    var s = w.BetriebEroeffnen($"Siederei {i}", Betriebsart.Siederei, k, startkarren: "");
                    w.Einstellen(s, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
                    if (handschuhe) Ok(w, k, new EinrichtungKaufen(s.Name, "Handschuhe"));
                    Pruefe.Ungefaehr(handschuhe ? 0.15 : 0.3, w.Werkstaetten.UnfallGefahr(s), 1e-12);
                }
                var zahl = 0;
                w.Meldung += m => { if (m.StartsWith("Unfall in Siederei")) zahl++; };
                JahreSimulieren(sim, 20);
                return zahl;
            }
            var ohne = Zaehlen(false);
            var mit = Zaehlen(true);
            // Je Arbeiter und Jahr höchstens ein Unfall (danach fehlt er): 1 − (1 − 0,3/12)^12 ≈ 26 %, mit Handschuhen ≈ 14 %.
            Pruefe.Wahr(ohne is > 60 and < 110, $"ohne Handschuhe {ohne} Unfälle (erwartet ≈ 84)");
            Pruefe.Wahr(mit / (double)ohne is > 0.35 and < 0.72, $"mit Handschuhen {mit}, ohne {ohne}");
        }),

        ("Werkstatt: Parfumeur – Meisterwerkzeug erst ab der Duftwerkstatt und nur mit gutem Werkzeug; die Einrichtung gilt in allen drei Stufen, nicht im Duftgarten", () =>
        {
            var sim = NeueSimulation();
            var (w, k, s) = Siederei(sim);
            Ok(w, k, new EinrichtungKaufen(s.Name, "Regale"));
            Ok(w, k, new EinrichtungKaufen(s.Name, "Glasflakons"));
            Abgelehnt(w, k, new EinrichtungKaufen(s.Name, "Werkzeug2"), "erst ab Stufe 2");
            s.Ausbauen(Betriebsart.Duftwerkstatt, 0);
            Pruefe.Gleich(10, w.Werkstaetten.Einrichtungsliste(s).Count); // sieben, ab PR #224 drei der Stufe 3 dazu
            Pruefe.Wahr(s.HatEinrichtung("Regale") && s.LagerKapazitaetJeWare == 60, "Flakonregale nach dem Ausbau");
            Abgelehnt(w, k, new EinrichtungKaufen(s.Name, "Werkzeug2"), "Erst braucht es: Gutes Werkzeug");
            Ok(w, k, new EinrichtungKaufen(s.Name, "Werkzeug1"));
            Ok(w, k, new EinrichtungKaufen(s.Name, "Werkzeug2"));
            Pruefe.Ungefaehr(60 * 0.81, s.MinutenJeStueck("Giftdolch"), 1e-9);
            Pruefe.Ungefaehr(70 * 0.729, s.MinutenJeStueck("Moschusduft"), 1e-9);
            Pruefe.Ungefaehr(60 * 0.729, s.MinutenJeStueck("Fechterduft"), 1e-9);
            s.Ausbauen(Betriebsart.Parfumerie, 0);
            Pruefe.Gleich(3, s.Stufe);
            var liste = w.Werkstaetten.Einrichtungsliste(s);
            Pruefe.Gleich(10, liste.Count); // sieben, ab PR #224 drei der Stufe 3 dazu
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Glasflakons Regale", string.Join(" ", liste.Where(e => e.Gekauft).Select(e => e.Kennung)));
            Pruefe.Ungefaehr(100 * 0.729, s.MinutenJeStueck("Heiligenduft"), 1e-9);
            Pruefe.Ungefaehr(100 * 0.81, s.MinutenJeStueck("Schattendolch"), 1e-9);
            Pruefe.Ungefaehr(50 * 0.81, s.MinutenJeStueck("Blume der Zwietracht"), 1e-9);
            // Der Duftgarten gehört auch zum Parfumeur, ist aber keine Werkstatt.
            var g = w.BetriebEroeffnen("Duftgarten Rosenhain", Betriebsart.Duftgarten, k, startkarren: "");
            Pruefe.Gleich(0, w.Werkstaetten.Einrichtungsliste(g).Count);
            Abgelehnt(w, k, new EinrichtungKaufen(g.Name, "Regale"), "kein Stück „Regale“");
            Pruefe.Gleich(0.0, w.Werkstaetten.UnfallGefahr(g));
        }),

        ("Werkstatt: Daten – der Wirt hat sieben Stücke (Kennungen wie im Raummodell) zu den Preisen des Schmieds, mit Braupfanne statt Blasebalg und Handschuhen statt Schürzen", () =>
        {
            var d = Daten.Value.Werkstatt!;
            var wirt = d.EinrichtungFuer("Wirt");
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Braupfanne Regale Loeschfass Eisentuer Handschuhe", string.Join(" ", wirt.Take(7).Select(e => e.Kennung))); // dazu ab PR #224 drei Stücke der Stufe 3
            Pruefe.Gleich("Gutes Werkzeug, Meisterwerkzeug, Kupferne Braupfanne, Fassregale, Löschfass, Eisentür, Lederhandschuhe",
                string.Join(", ", wirt.Take(7).Select(e => e.Name)));
            foreach (var e in wirt.Take(7).Where(e => e.Kennung != "Braupfanne"))
            {
                var schmied = d.Stueck("Schmied", e.Kennung == "Handschuhe" ? "Schuerzen" : e.Kennung)!;
                Pruefe.Gleich(schmied.Preis, e.Preis, $"Preis {e.Kennung}");
                Pruefe.Gleich(schmied.Wirkung.Text(), e.Wirkung.Text(), $"Wirkung {e.Kennung}");
            }
            Pruefe.Gleich(350.0, d.Stueck("Wirt", "Braupfanne")!.Preis);
            Pruefe.Gleich("Werkzeug1", d.Stueck("Wirt", "Werkzeug2")!.Voraussetzung);
            Pruefe.Gleich(2, d.Stueck("Wirt", "Werkzeug2")!.AbStufe);
            Pruefe.Gleich("Dünnbier Starkbier Festbier", string.Join(" ", d.Stueck("Wirt", "Braupfanne")!.Wirkung.Waren));
            foreach (var ware in d.Stueck("Wirt", "Braupfanne")!.Wirkung.Waren)
                Pruefe.Wahr(Daten.Value.Art(Betriebsart.Gasthaus).Produkte.Contains(ware), $"Ware {ware} braut der Wirt");
            Pruefe.Ungefaehr(0.5, d.Stueck("Wirt", "Handschuhe")!.Wirkung.UnfallFaktor, 1e-12);
        }),

        ("Werkstatt: Wirt – Werkzeug spart Zeit bei allen Waren, die Braupfanne nur bei Dünnbier, Starkbier, Festbier; Fassregale geben Lager", () =>
        {
            var sim = NeueSimulation();
            var (w, k, s) = Schaenke(sim, vermoegen: 5_000);
            Pruefe.Wahr(w.Werkstaetten.Einrichtungsliste(s) is { Count: 10 } l && l.All(e => !e.Gekauft), "sieben Stücke und drei der Stufe 3 (PR #224), keins gekauft");
            Ok(w, k, new EinrichtungKaufen(s.Name, "Braupfanne"));
            Pruefe.Ungefaehr(4_650, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(350, k.Laufend.Investitionen, 1e-9);
            Pruefe.Ungefaehr(35 * 0.9, s.MinutenJeStueck("Dünnbier"), 1e-9);
            Pruefe.Ungefaehr(30, s.MinutenJeStueck("Körnermahlzeit"), 1e-9); // gebraut wird nur Bier
            Pruefe.Ungefaehr(30, s.MinutenJeStueck("Weingeist"), 1e-9);
            Ok(w, k, new EinrichtungKaufen(s.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(35 * 0.81, s.MinutenJeStueck("Dünnbier"), 1e-9);
            Pruefe.Ungefaehr(30 * 0.9, s.MinutenJeStueck("Körnermahlzeit"), 1e-9);
            Pruefe.Gleich(40, s.LagerKapazitaetJeWare);
            Ok(w, k, new EinrichtungKaufen(s.Name, "Regale"));
            Pruefe.Gleich(60, s.LagerKapazitaetJeWare);
            foreach (var kennung in new[] { "Loeschfass", "Eisentuer", "Handschuhe" }) Ok(w, k, new EinrichtungKaufen(s.Name, kennung));
            Pruefe.Gleich((0.5, 0.5, 0.5), (s.BrandFaktor, s.EinbruchFaktor, s.UnfallFaktor));
            Pruefe.Ungefaehr(5_000 - 350 - 300 - 200 - 100 - 150 - 80, k.Vermoegen, 1e-9); // Eisentür 150 seit PR #177
            Abgelehnt(w, k, new EinrichtungKaufen(s.Name, "Schuerzen"), "kein Stück „Schuerzen“");
            Abgelehnt(w, k, new EinrichtungKaufen(s.Name, "Glasflakons"), "kein Stück „Glasflakons“");
            Abgelehnt(w, k, new EinrichtungKaufen(s.Name, "Werkzeug2"), "erst ab Stufe 2");
            Ok(w, k, new ProduktWaehlen(s.Name, "Körnermahlzeit"));
            w.Einstellen(s, "Geselle", 1); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
            BisUhrzeit(sim, 14); // die Gäste ab 12 Uhr trinken Dünnbier (davon ist am meisten da)
            Pruefe.Wahr(s.ImLager("Körnermahlzeit") == 13, $"Körnermahlzeit mit Werkzeug: {s.ImLager("Körnermahlzeit")} (360 / 27 ≈ 13,3; ohne Werkzeug 360 / 30 = 12)");
            var liste = w.Werkstaetten.Einrichtungsliste(s);
            Pruefe.Gleich("10 % weniger Zeit je Stück (Dünnbier, Starkbier, Festbier)", liste.Single(e => e.Kennung == "Braupfanne").Wirkung);
            Pruefe.Gleich("Kupferne Braupfanne", liste.Single(e => e.Kennung == "Braupfanne").Name);
            Pruefe.Gleich("Fassregale", liste.Single(e => e.Kennung == "Regale").Name);
            Pruefe.Gleich("20 mehr Lager je Ware", liste.Single(e => e.Kennung == "Regale").Wirkung);
            Pruefe.Gleich("Lederhandschuhe", liste.Single(e => e.Kennung == "Handschuhe").Name);
            Pruefe.Gleich("Unfälle halb so oft", liste.Single(e => e.Kennung == "Handschuhe").Wirkung);
            Pruefe.Ungefaehr(0.02 * 0.5 * 840 / 720, w.Werkstaetten.UnfallGefahr(s), 1e-12); // Gefahr des Wirts je Arbeiter und Jahr – je Stunde gleich, die Schicht geht bis 22 Uhr
        }),

        ("Werkstatt: Wirt – Meisterwerkzeug erst ab dem Wirtshaus und nur mit gutem Werkzeug; die Einrichtung gilt in allen drei Stufen, nicht auf dem Bauernhof", () =>
        {
            var sim = NeueSimulation();
            var (w, k, s) = Schaenke(sim);
            Ok(w, k, new EinrichtungKaufen(s.Name, "Regale"));
            Ok(w, k, new EinrichtungKaufen(s.Name, "Braupfanne"));
            Abgelehnt(w, k, new EinrichtungKaufen(s.Name, "Werkzeug2"), "erst ab Stufe 2");
            s.Ausbauen(Betriebsart.Wirtshaus, 0);
            Pruefe.Gleich(10, w.Werkstaetten.Einrichtungsliste(s).Count); // sieben, ab PR #224 drei der Stufe 3 dazu
            Pruefe.Wahr(s.HatEinrichtung("Regale") && s.LagerKapazitaetJeWare == 60, "Fassregale nach dem Ausbau");
            Abgelehnt(w, k, new EinrichtungKaufen(s.Name, "Werkzeug2"), "Erst braucht es: Gutes Werkzeug");
            Ok(w, k, new EinrichtungKaufen(s.Name, "Werkzeug1"));
            Ok(w, k, new EinrichtungKaufen(s.Name, "Werkzeug2"));
            Pruefe.Ungefaehr(50 * 0.729, s.MinutenJeStueck("Starkbier"), 1e-9);
            Pruefe.Ungefaehr(60 * 0.81, s.MinutenJeStueck("Kräuterlikör"), 1e-9);
            s.Ausbauen(Betriebsart.Gasthaus, 0);
            Pruefe.Gleich(3, s.Stufe);
            var liste = w.Werkstaetten.Einrichtungsliste(s);
            Pruefe.Gleich(10, liste.Count); // sieben, ab PR #224 drei der Stufe 3 dazu
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Braupfanne Regale", string.Join(" ", liste.Where(e => e.Gekauft).Select(e => e.Kennung)));
            Pruefe.Ungefaehr(80 * 0.729, s.MinutenJeStueck("Festbier"), 1e-9);
            Pruefe.Ungefaehr(35 * 0.729, s.MinutenJeStueck("Dünnbier"), 1e-9);
            Pruefe.Ungefaehr(100 * 0.81, s.MinutenJeStueck("Schädelbrand"), 1e-9);
            Pruefe.Ungefaehr(30 * 0.81, s.MinutenJeStueck("Körnermahlzeit"), 1e-9);
            // Der Bauernhof gehört auch zum Wirt, ist aber keine Werkstatt.
            var g = w.BetriebEroeffnen("Bauernhof Krug", Betriebsart.Bauernhof, k, startkarren: "");
            Pruefe.Gleich(0, w.Werkstaetten.Einrichtungsliste(g).Count);
            Abgelehnt(w, k, new EinrichtungKaufen(g.Name, "Regale"), "kein Stück „Regale“");
            Pruefe.Gleich(0.0, w.Werkstaetten.UnfallGefahr(g));
        }),

        ("Werkstatt: Daten – der Prediger hat sieben Stücke (Kennungen wie im Raummodell) zu den Preisen des Schmieds, mit Gänsekielen statt Blasebalg und Handschuhen statt Schürzen", () =>
        {
            var d = Daten.Value.Werkstatt!;
            var prediger = d.EinrichtungFuer("Prediger");
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Gaensekiele Regale Loeschfass Eisentuer Handschuhe", string.Join(" ", prediger.Take(7).Select(e => e.Kennung))); // dazu ab PR #224 drei Stücke der Stufe 3
            Pruefe.Gleich("Gutes Werkzeug, Meisterwerkzeug, Gänsekiele und Lapislazuli, Bücherregale, Löschfass, Eisentür, Lederhandschuhe",
                string.Join(", ", prediger.Take(7).Select(e => e.Name)));
            foreach (var e in prediger.Take(7).Where(e => e.Kennung != "Gaensekiele"))
            {
                var schmied = d.Stueck("Schmied", e.Kennung == "Handschuhe" ? "Schuerzen" : e.Kennung)!;
                Pruefe.Gleich(schmied.Preis, e.Preis, $"Preis {e.Kennung}");
                Pruefe.Gleich(schmied.Wirkung.Text(), e.Wirkung.Text(), $"Wirkung {e.Kennung}");
            }
            Pruefe.Gleich(350.0, d.Stueck("Prediger", "Gaensekiele")!.Preis);
            Pruefe.Gleich("Werkzeug1", d.Stueck("Prediger", "Werkzeug2")!.Voraussetzung);
            Pruefe.Gleich(2, d.Stueck("Prediger", "Werkzeug2")!.AbStufe);
            Pruefe.Gleich("Lehrschrift Psalter Stundenbuch", string.Join(" ", d.Stueck("Prediger", "Gaensekiele")!.Wirkung.Waren));
            foreach (var ware in d.Stueck("Prediger", "Gaensekiele")!.Wirkung.Waren)
                Pruefe.Wahr(Daten.Value.Art(Betriebsart.Kathedrale).Produkte.Contains(ware), $"Ware {ware} schreibt der Prediger");
            Pruefe.Ungefaehr(0.5, d.Stueck("Prediger", "Handschuhe")!.Wirkung.UnfallFaktor, 1e-12);
            Pruefe.Wahr(d.Stueck("Prediger", "Schuerzen") is null, "Der Prediger hat Handschuhe, keine Schürzen");
        }),

        ("Werkstatt: Prediger – Werkzeug spart Zeit bei allen Waren, die Gänsekiele nur bei Lehrschrift, Psalter, Stundenbuch; Bücherregale geben Lager", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Kirche(sim, vermoegen: 5_000);
            Pruefe.Wahr(w.Werkstaetten.Einrichtungsliste(b) is { Count: 10 } l && l.All(e => !e.Gekauft), "sieben Stücke und drei der Stufe 3 (PR #224), keins gekauft");
            Ok(w, k, new EinrichtungKaufen(b.Name, "Gaensekiele"));
            Pruefe.Ungefaehr(4_650, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(350, k.Laufend.Investitionen, 1e-9);
            Pruefe.Ungefaehr(60 * 0.9, b.MinutenJeStueck("Lehrschrift"), 1e-9);
            Pruefe.Ungefaehr(30, b.MinutenJeStueck("Papier"), 1e-9); // die Kiele schreiben, sie schöpfen kein Papier
            Pruefe.Ungefaehr(40, b.MinutenJeStueck("Kerze"), 1e-9);
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug1"));
            Pruefe.Ungefaehr(60 * 0.81, b.MinutenJeStueck("Lehrschrift"), 1e-9);
            Pruefe.Ungefaehr(30 * 0.9, b.MinutenJeStueck("Papier"), 1e-9);
            Pruefe.Gleich(40, b.LagerKapazitaetJeWare);
            Ok(w, k, new EinrichtungKaufen(b.Name, "Regale"));
            Pruefe.Gleich(60, b.LagerKapazitaetJeWare);
            foreach (var kennung in new[] { "Loeschfass", "Eisentuer", "Handschuhe" }) Ok(w, k, new EinrichtungKaufen(b.Name, kennung));
            Pruefe.Gleich((0.5, 0.5, 0.5), (b.BrandFaktor, b.EinbruchFaktor, b.UnfallFaktor));
            Pruefe.Ungefaehr(5_000 - 350 - 300 - 200 - 100 - 150 - 80, k.Vermoegen, 1e-9); // Eisentür 150 seit PR #177
            Abgelehnt(w, k, new EinrichtungKaufen(b.Name, "Schuerzen"), "kein Stück „Schuerzen“");
            Abgelehnt(w, k, new EinrichtungKaufen(b.Name, "Braupfanne"), "kein Stück „Braupfanne“");
            Abgelehnt(w, k, new EinrichtungKaufen(b.Name, "Werkzeug2"), "erst ab Stufe 2");
            Ok(w, k, new ProduktWaehlen(b.Name, "Papier"));
            w.Einstellen(b, "Geselle", 1); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
            BisUhrzeit(sim, 14);
            Pruefe.Wahr(b.ImLager("Papier") == 13, $"Papier mit Werkzeug: {b.ImLager("Papier")} (360 / 27 ≈ 13,3; ohne Werkzeug 360 / 30 = 12)");
            var liste = w.Werkstaetten.Einrichtungsliste(b);
            Pruefe.Gleich("10 % weniger Zeit je Stück (Lehrschrift, Psalter, Stundenbuch)", liste.Single(e => e.Kennung == "Gaensekiele").Wirkung);
            Pruefe.Gleich("Gänsekiele und Lapislazuli", liste.Single(e => e.Kennung == "Gaensekiele").Name);
            Pruefe.Gleich("Bücherregale", liste.Single(e => e.Kennung == "Regale").Name);
            Pruefe.Gleich("20 mehr Lager je Ware", liste.Single(e => e.Kennung == "Regale").Wirkung);
            Pruefe.Gleich("Lederhandschuhe", liste.Single(e => e.Kennung == "Handschuhe").Name);
            Pruefe.Gleich("Unfälle halb so oft", liste.Single(e => e.Kennung == "Handschuhe").Wirkung);
            Pruefe.Ungefaehr(0.02 * 0.5, w.Werkstaetten.UnfallGefahr(b), 1e-12); // Gefahr des Predigers je Arbeiter und Jahr
        }),

        ("Werkstatt: Prediger – Meisterwerkzeug erst ab dem Dom und nur mit gutem Werkzeug; die Einrichtung gilt in allen drei Stufen, nicht auf dem Klosterhof", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Kirche(sim);
            Ok(w, k, new EinrichtungKaufen(b.Name, "Regale"));
            Ok(w, k, new EinrichtungKaufen(b.Name, "Gaensekiele"));
            Abgelehnt(w, k, new EinrichtungKaufen(b.Name, "Werkzeug2"), "erst ab Stufe 2");
            b.Ausbauen(Betriebsart.Dom, 0);
            Pruefe.Gleich(10, w.Werkstaetten.Einrichtungsliste(b).Count); // sieben, ab PR #224 drei der Stufe 3 dazu
            Pruefe.Wahr(b.HatEinrichtung("Regale") && b.LagerKapazitaetJeWare == 60, "Bücherregale nach dem Ausbau");
            Abgelehnt(w, k, new EinrichtungKaufen(b.Name, "Werkzeug2"), "Erst braucht es: Gutes Werkzeug");
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug1"));
            Ok(w, k, new EinrichtungKaufen(b.Name, "Werkzeug2"));
            Pruefe.Ungefaehr(70 * 0.729, b.MinutenJeStueck("Psalter"), 1e-9);
            Pruefe.Ungefaehr(50 * 0.81, b.MinutenJeStueck("Hetzschrift"), 1e-9); // Hetze schreibt sich ohne Lapislazuli
            b.Ausbauen(Betriebsart.Kathedrale, 0);
            Pruefe.Gleich(3, b.Stufe);
            var liste = w.Werkstaetten.Einrichtungsliste(b);
            Pruefe.Gleich(10, liste.Count); // sieben, ab PR #224 drei der Stufe 3 dazu
            Pruefe.Gleich("Werkzeug1 Werkzeug2 Gaensekiele Regale", string.Join(" ", liste.Where(e => e.Gekauft).Select(e => e.Kennung)));
            Pruefe.Ungefaehr(100 * 0.729, b.MinutenJeStueck("Stundenbuch"), 1e-9);
            Pruefe.Ungefaehr(60 * 0.729, b.MinutenJeStueck("Lehrschrift"), 1e-9);
            Pruefe.Ungefaehr(80 * 0.81, b.MinutenJeStueck("Reliquiar"), 1e-9);
            Pruefe.Ungefaehr(40 * 0.81, b.MinutenJeStueck("Kerze"), 1e-9);
            // Der Klosterhof gehört auch zum Prediger, ist aber keine Werkstatt.
            var g = w.BetriebEroeffnen("Klosterhof Psalm", Betriebsart.Klosterhof, k, startkarren: "");
            Pruefe.Gleich(0, w.Werkstaetten.Einrichtungsliste(g).Count);
            Abgelehnt(w, k, new EinrichtungKaufen(g.Name, "Regale"), "kein Stück „Regale“");
            Pruefe.Gleich(0.0, w.Werkstaetten.UnfallGefahr(g));
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
            w.Einstellen(g, "Geselle", 1); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
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
            w.Einstellen(g, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
            Ok(w, k, new ProduktWaehlen(g.Name, "Dolch"));
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
            Pruefe.Gleich("1:Dolch:50 2:Dolch:50", string.Join(" ", ag.Arbeiter.Select(a => $"{a.Nummer}:{ag.WareVon(a)}:{a.Laune}")));
            Pruefe.Ungefaehr(20, ag.FortschrittVon("Dolch"), 1e-12);
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
                w.Einstellen(g, "Geselle", 2); // Gesellen gibt es nur durch Beförderung (PR #224) – hier gleich eingestellt
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
