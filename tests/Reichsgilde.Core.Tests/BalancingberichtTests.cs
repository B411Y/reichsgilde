using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Wirtschaft;

// ─────────────────────────────────────────────────────────────────────────────
//  Balancing-Bericht (PR #176): die Bausteine der Messung – Kennzahlen, Stichjahre, die Rotation der Wohnhausplätze, die Messpartie
//  (Spieler von der KI mit genau einem Betrieb geführt, Geist, Einrichtung am Start, gleich bei gleichem Aufbau) und die Messstadt.
//  Die Messung selbst ist kein Test: dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen bericht
// ─────────────────────────────────────────────────────────────────────────────

static class BalancingberichtTests
{
    static readonly Lazy<Messquellen> Quellen = new(() => new Messquellen());

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Bericht: Kennzahl mit Mittel, Streuung und Quantilen (linear wie R type 7)", () =>
        {
            var k = Balancingbericht.Kennzahl(new double[] { 5, 1, 4, 2, 3 }, 2);
            Pruefe.Gleich(5, (int)k["n"]!);
            Pruefe.Ungefaehr(3, (double)k["mittel"]!, 1e-9);
            Pruefe.Ungefaehr(1.58, (double)k["streuung"]!, 1e-9);
            Pruefe.Ungefaehr(1.4, (double)k["q10"]!, 1e-9);
            Pruefe.Ungefaehr(2, (double)k["q25"]!, 1e-9);
            Pruefe.Ungefaehr(3, (double)k["median"]!, 1e-9);
            Pruefe.Ungefaehr(4.6, (double)k["q90"]!, 1e-9);
            Pruefe.Ungefaehr(1, (double)k["min"]!, 1e-9);
            Pruefe.Ungefaehr(5, (double)k["max"]!, 1e-9);
            Pruefe.Gleich(0, (int)Balancingbericht.Kennzahl(Array.Empty<double>())["n"]!);
            Pruefe.Gleich(1, (int)Balancingbericht.Kennzahl(new[] { double.NaN, 7.0 })["n"]!, "NaN zählt nicht");
        }),

        ("Bericht: Stichjahre 5, 10, 20 … bis zur Länge der Partie, dazu das letzte Jahr", () =>
        {
            Pruefe.Gleich("5,10,20", string.Join(",", Balancingbericht.Stichjahre(20)));
            Pruefe.Gleich("5,10,20,30", string.Join(",", Balancingbericht.Stichjahre(30)));
            Pruefe.Gleich("5,10,12", string.Join(",", Balancingbericht.Stichjahre(12)));
            Pruefe.Gleich("3", string.Join(",", Balancingbericht.Stichjahre(3)));
        }),

        ("Bericht: Vorzeichen und Abstand in Prozent", () =>
        {
            Pruefe.Gleich("+12", Balancingbericht.Vz(12.4));
            Pruefe.Gleich("−3,5", Balancingbericht.Vz(-3.46, 1));
            Pruefe.Gleich("0", Balancingbericht.Vz(-0.2));
            Pruefe.Ungefaehr(-25, Balancingbericht.Abstand(75, 100), 1e-9);
            Pruefe.Ungefaehr(50, Balancingbericht.Abstand(-50, -100), 1e-9);
        }),

        ("Bericht: Rotation reicht die Wohnhausplätze weiter, die Karte bleibt gültig", () =>
        {
            var text = Quellen.Value.Karte("greybox");
            var rotiert = JsonNode.Parse(Messpartie.KarteRotiert(text, 1, 3))!["start"]!;
            Pruefe.Gleich("Wohnhaus2", (string)rotiert["spieler"]!["wohnhaus"]!);
            Pruefe.Gleich("Wohnhaus3", (string)rotiert["haeuser"]![0]!["wohnhaus"]!);
            Pruefe.Gleich("Wohnhaus1", (string)rotiert["haeuser"]![2]!["wohnhaus"]!);
            Pruefe.Gleich("Giesserei_Eisenbart", (string)rotiert["haeuser"]![0]!["betriebe"]![0]!["platz"]!, "Werkstattplätze bleiben");
            var karte = Stadtkarte.AusJson(Messpartie.KarteRotiert(text, 2, 3), Gebaeudekatalog.AusJson(Quellen.Value.Katalog));
            Pruefe.Gleich("Wohnhaus3", karte.Wohnhausplaetze[0].Kennung);
            Pruefe.Gleich(text, Messpartie.KarteRotiert(text, 0, 3), "ohne Rotation unverändert");
        }),

        ("Bericht: Der Spieler mit einem Betrieb wird von der KI geführt und expandiert nicht", () =>
        {
            var r = Messpartie.Spielen(new Messaufbau("greybox", 101, 2, Beruf: "Tischler"), Quellen.Value);
            Pruefe.Wahr(r.Fehler is null, r.Fehler ?? "");
            Pruefe.Gleich(4, r.Haeuser.Count);
            Pruefe.Wahr(r.Spieler.IstSpieler && r.Spieler.Startberuf == "Tischler", r.Spieler.Startberuf);
            Pruefe.Gleich(2, r.Spieler.Jahre.Count);
            Pruefe.Wahr(r.Spieler.Jahre.All(j => j.Betriebe == 1), "genau ein Betrieb");
            Pruefe.Gleich(2, r.Spieler.StartArbeiter);
            Pruefe.Gleich(1, r.Spieler.Jahre[^1].Stufe);
            Pruefe.Wahr(r.Spieler.Jahre[^1].Gewinn > 0, $"die KI führt die Tischlerei mit Gewinn ({r.Spieler.Jahre[^1].Gewinn:0})");
            Pruefe.Wahr(r.Ki.All(h => h.StartBetriebe == 2 && h.StartArbeiter == 5), "KI-Häuser: Werkstatt und Umland");
            Pruefe.Wahr(r.Markt.Count == 0, "ohne MarktMessen kein Markt");
        }),

        ("Bericht: Gleicher Aufbau, gleiche Partie – auch parallel", () =>
        {
            var a = new Messaufbau("greybox", 202, 1, Beruf: "Geldleiher");
            var nacheinander = Messpartie.Spielen(a, Quellen.Value);
            var parallel = new Partieergebnis[2];
            Parallel.For(0, 2, i => parallel[i] = Messpartie.Spielen(a, Quellen.Value));
            foreach (var p in parallel)
                for (var h = 0; h < nacheinander.Haeuser.Count; h++)
                    Pruefe.Gleich(nacheinander.Haeuser[h].Jahre[0].Rein, p.Haeuser[h].Jahre[0].Rein, nacheinander.Haeuser[h].Name);
            Pruefe.Wahr(nacheinander.Spieler.Jahre[0].Sonder.ContainsKey("Wechsel"), "Bankbuch des Geldleihers");
        }),

        ("Bericht: Einrichtung am Start und Ertragsstücke (Werkzeug und Spezialstück, Waffenkammer, Bank)", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(Quellen.Value.Wirtschaft);
            Pruefe.Gleich("Werkzeug1,Blasebalg", string.Join(",", Balancingbericht.Ertragsstuecke(daten, "Schmied").Select(x => x.Kennung)));
            Pruefe.Gleich("Regale", string.Join(",", Balancingbericht.Ertragsstuecke(daten, "Gardist").Select(x => x.Kennung)));
            Pruefe.Wahr(Balancingbericht.Ertragsstuecke(daten, "Dieb").Count == 0, "der Dieb hat kein Ertragsstück");
            Pruefe.Gleich("Giesserei,Schmiede,Goldschmiede", string.Join(",", Balancingbericht.Stufen(daten, "Schmied")));
            var r = Messpartie.Spielen(new Messaufbau("greybox", 101, 1, Einrichtung: "Werkzeug1,Blasebalg"), Quellen.Value);
            Pruefe.Wahr(r.Fehler is null, r.Fehler ?? "");
            Pruefe.Ungefaehr(4000 - 650, r.Spieler.StartBar, 1e-6);
        }),

        ("Messstadt: 9 Werkstatt- und 9 Wohnhausplätze, 8 KI-Häuser, 17 Umlandbetriebe; eine Partie mit 8 Häusern und Geist läuft", () =>
        {
            var karte = Stadtkarte.AusJson(Quellen.Value.Karte("messstadt"), Gebaeudekatalog.AusJson(Quellen.Value.Katalog));
            Pruefe.Gleich(9, karte.GebaeudeDerArt(Bausteinart.Werkstattplatz).Count());
            Pruefe.Gleich(9, karte.GebaeudeDerArt(Bausteinart.Wohnhausplatz).Count());
            Pruefe.Gleich(17, karte.GebaeudeDerArt(Bausteinart.Umlandbetrieb).Count());
            Pruefe.Gleich(8, karte.Startbelegung.Haeuser.Count);
            Pruefe.Gleich("Zwerg,Elb,Halbling", string.Join(",", karte.Startbelegung.Haeuser.Take(3).Select(h => h.Volk)), "die ersten drei wie in der Greybox");
            var daten = Wirtschaftsdaten.AusJson(Quellen.Value.Wirtschaft);
            Pruefe.Wahr(daten.Liegenschaften.All(l => karte.Gebaeude.Any(g => g.Betrieb == l.Name)), "jede Liegenschaft hat ihren Platz");
            var r = Messpartie.Spielen(new Messaufbau("messstadt", 101, 1, Bezug: Bezugsart.Geist, Rivalen: 8, MarktMessen: true), Quellen.Value);
            Pruefe.Wahr(r.Fehler is null, r.Fehler ?? "");
            Pruefe.Gleich(9, r.Haeuser.Count);
            Pruefe.Gleich(0, r.Spieler.StartArbeiter);
            Pruefe.Wahr(r.Markt.ContainsKey(1) && r.Markt[1].Any(x => x.VerkauftHaeuser > 0), "Markt im Stichjahr gemessen");
            Pruefe.Wahr(r.Ki.Select(h => h.Wohnhausplatz).Distinct().Count() == 8, "jedes Haus hat sein Wohnhaus");
        }),
    };
}
