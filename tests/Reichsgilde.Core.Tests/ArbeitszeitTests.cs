using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für die Arbeitszeit je Betriebsart: Im Wirtshaus (Schänke, Wirtshaus, Gasthaus) wird bis 22 Uhr gearbeitet, sonst bis 20 Uhr.</summary>
static class ArbeitszeitTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Die echten Daten, als hätte die Betriebsart keine eigene Arbeitszeit (wie vor Datenversion 43).</summary>
    static string OhneEigeneArbeitszeit() => File.ReadAllText(Datenpfad.Wirtschaft()).Replace(", \"arbeitsminuten\": 840", "");

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    static void BisUhrzeit(Simulation sim, int stunde)
    {
        while (sim.Zeit.UhrzeitInMinuten < stunde * 60) sim.Tick();
    }

    static void TagVorbei(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Ein Betrieb mit einem Gesellen, ohne Karren.</summary>
    static Betrieb Betrieb(Stadtwirtschaft w, Betriebsart art, string name)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 10_000);
        var b = w.BetriebEroeffnen($"{art} {name}", art, k, startkarren: "");
        w.Einstellen(b, "Geselle", 1);
        return b;
    }

    /// <summary>Wie viel Weingeist ein Geselle in einer Schänke an einem Tag brennt (Weingeist ist kein Ausschank – die Gäste trinken ihn nicht weg).</summary>
    static int WeingeistAmTag(Wirtschaftsdaten daten)
    {
        var sim = NeueSimulation(daten);
        var w = sim.Wirtschaft!;
        var s = Betrieb(w, Betriebsart.Schaenke, "Familie Krug");
        Pruefe.Wahr(w.Ausfuehren(s.Konto, new ProduktWaehlen(s.Name, "Weingeist")).Erfolg, "Weingeist wählen");
        s.Einlagern("Gerste", 40);
        s.Einlagern("Roggen", 40);
        TagVorbei(sim);
        return s.ImLager("Weingeist");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Arbeitszeit: Daten – Schänke, Wirtshaus und Gasthaus arbeiten 840 Minuten (8–22 Uhr), alle anderen 720 (8–20 Uhr); keine endet nach 22 Uhr", () =>
        {
            var d = Daten.Value;
            Pruefe.Gleich(54, d.Version);
            foreach (var art in new[] { Betriebsart.Schaenke, Betriebsart.Wirtshaus, Betriebsart.Gasthaus })
            {
                Pruefe.Gleich(840, d.Arbeitsminuten(art));
                Pruefe.Ungefaehr(14.0 / 12, d.Schichtfaktor(art), 1e-12);
            }
            foreach (var art in new[] { Betriebsart.Giesserei, Betriebsart.Bauernhof, Betriebsart.Tischlerei, Betriebsart.Schmugglerloch, Betriebsart.Wechselstube })
                Pruefe.Gleich(720, d.Arbeitsminuten(art));
            foreach (var art in d.Betriebsarten.Keys)
                Pruefe.Wahr(Stadtwirtschaft.Arbeitsbeginn + d.Arbeitsminuten(art) <= Spielzeit.TagesendeMinute, $"{art} arbeitet über 22 Uhr hinaus");
        }),

        ("Arbeitszeit: Der Wirt arbeitet bis 22 Uhr, der Schmied bis 20 Uhr – Abfragen je Betrieb", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var schaenke = Betrieb(w, Betriebsart.Schaenke, "Familie Krug");
            var giesserei = Betrieb(w, Betriebsart.Giesserei, "Haus Eisenbart");
            Pruefe.Gleich((1320, 1200), (schaenke.Arbeitsende, giesserei.Arbeitsende));
            Pruefe.Gleich((840, 720), (schaenke.Arbeitsminuten, giesserei.Arbeitsminuten));
            foreach (var (minute, wirt, schmied) in new[] { (179, false, false), (180, true, true), (899, true, true), (900, true, false), (1019, true, false) }) // 07:59, 08:00, 19:59, 20:00, 21:59
            {
                var zeit = new Spielzeit(minute);
                Pruefe.Gleich((wirt, schmied), (w.IstArbeitszeit(schaenke, zeit), w.IstArbeitszeit(giesserei, zeit)));
                Pruefe.Gleich(schmied, w.IstArbeitszeit(zeit)); // die übliche Arbeitszeit (Markt, Kontor) bleibt 8–20 Uhr
            }
        }),

        ("Arbeitszeit: Im Wirtshaus entsteht an einem Tag 14/12 so viel wie mit der üblichen Arbeitszeit", () =>
        {
            var mit = WeingeistAmTag(Daten.Value);
            var ohne = WeingeistAmTag(Wirtschaftsdaten.AusJson(OhneEigeneArbeitszeit()));
            Pruefe.Wahr(ohne > 0 && Math.Abs(mit - ohne * 14.0 / 12) <= 1, $"bis 22 Uhr {mit} Weingeist, bis 20 Uhr {ohne} (erwartet etwa 840 / 30 = 28 und 720 / 30 = 24)");
        }),

        ("Arbeitszeit: Lohn je Stunde gleich – im Wirtshaus kostet ein Geselle 14/12 des üblichen Jahreslohns", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var schaenke = Betrieb(w, Betriebsart.Schaenke, "Familie Krug");
            var giesserei = Betrieb(w, Betriebsart.Giesserei, "Haus Eisenbart");
            var kosten = w.Daten.Arbeitskraefte["Geselle"].Kosten;
            Pruefe.Ungefaehr(kosten * 14 / 12, w.Daten.Lohn("Geselle", Betriebsart.Schaenke), 1e-9);
            Pruefe.Ungefaehr(kosten * 14 / 12, schaenke.Lohnkosten, 1e-9);
            Pruefe.Ungefaehr(kosten, giesserei.Lohnkosten, 1e-9);
            TagVorbei(sim);
            Pruefe.Ungefaehr(kosten * 14 / 12, schaenke.Vorjahr!.Personal, 1e-9);
            Pruefe.Ungefaehr(kosten, giesserei.Vorjahr!.Personal, 1e-9);
            // Die Kalkulation rechnet mit derselben Schicht: 840 Minuten Arbeit gegen den längeren Lohn.
            var wert = Kalkulation.WertJeArbeitsminute(w, schaenke);
            Pruefe.Ungefaehr(840 * wert * w.VolksFaktor(schaenke.Konto, "Wirt") - kosten * 14 / 12, Kalkulation.Jahresertrag(w, schaenke, "Geselle"), 1e-6);
        }),

        ("Arbeitszeit: Um 21 Uhr sitzen Gäste in der Schänke und die Leute arbeiten noch – in der Gießerei ist Feierabend", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var schaenke = Betrieb(w, Betriebsart.Schaenke, "Familie Krug");
            schaenke.Einlagern("Dünnbier", 200);
            var giesserei = Betrieb(w, Betriebsart.Giesserei, "Haus Eisenbart");
            BisUhrzeit(sim, 21);
            Pruefe.Wahr(w.Gastwirtschaft.GaesteJetzt(schaenke) > 0, "keine Gäste um 21 Uhr");
            Pruefe.Wahr(w.IstArbeitszeit(schaenke, sim.Zeit), "Schänke um 21 Uhr geschlossen");
            Pruefe.Wahr(!w.IstArbeitszeit(giesserei, sim.Zeit), "Gießerei um 21 Uhr offen");
        }),

        ("Arbeitszeit: Ein alter Spielstand ohne eigene Arbeitszeit lädt mit 720 Minuten; ein neuer behält 840", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var s = Betrieb(w, Betriebsart.Schaenke, "Familie Krug");
            var json = Speicherung.Speichern(sim, s.Konto);

            var neu = Speicherung.Laden(json).Simulation.Wirtschaft!;
            Pruefe.Gleich(840, neu.Betriebe.Single(b => b.Name == s.Name).Arbeitsminuten);

            var alt = JsonNode.Parse(json)!.AsObject();
            var arten = alt["Daten"]!["Betriebsarten"]!.AsObject();
            foreach (var (_, art) in arten) art!.AsObject().Remove("Arbeitsminuten");
            var geladen = Speicherung.Laden(alt.ToJsonString()).Simulation.Wirtschaft!;
            var b = geladen.Betriebe.Single(x => x.Name == s.Name);
            Pruefe.Gleich((720, 1200), (b.Arbeitsminuten, b.Arbeitsende));
            Pruefe.Ungefaehr(geladen.Daten.Arbeitskraefte["Geselle"].Kosten, b.Lohnkosten, 1e-9);
        }),

        ("Arbeitszeit: Datenprüfung – höchstens bis 22 Uhr, mehr als 0, nur für Betriebe mit Ware, gleich über alle Ausbaustufen", () =>
        {
            var json = File.ReadAllText(Datenpfad.Wirtschaft());
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json.Replace("\"ausbauKosten\": 2500, \"arbeitsminuten\": 840", "\"ausbauKosten\": 2500, \"arbeitsminuten\": 841")),
                "betriebsarten.Schaenke.arbeitsminuten muss zwischen 1 und 840");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(OhneEigeneArbeitszeit().Replace("\"ausbauKosten\": 2500", "\"ausbauKosten\": 2500, \"arbeitsminuten\": 0")),
                "betriebsarten.Schaenke.arbeitsminuten muss zwischen 1 und 840");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json.Replace("\"ausbauKosten\": 5000, \"arbeitsminuten\": 840", "\"ausbauKosten\": 5000, \"arbeitsminuten\": 720")),
                "Nach dem Ausbau zu Gasthaus muss die Arbeitszeit");
            var dieb = JsonNode.Parse(json)!;
            dieb["betriebsarten"]!["Schmugglerloch"]!["arbeitsminuten"] = 600;
            dieb["betriebsarten"]!["Unterschlupf"]!["arbeitsminuten"] = 600;
            dieb["betriebsarten"]!["Diebesgilde"]!["arbeitsminuten"] = 600;
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(dieb.ToJsonString()), "nur für Betriebe mit Ware");
        }),
    };
}
