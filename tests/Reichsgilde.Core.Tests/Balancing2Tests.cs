using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

// ─────────────────────────────────────────────────────────────────────────────
//  Balancing 2 (PR #208, Datenversion 57): Gaunerei je Nachtstunde, Fahrtzeiten der Schiffe mit dem Spieltag, Spitzel der KI,
//  Hinfracht und Heimatmarkt, Amtsgehälter kleiner Städte.
// ─────────────────────────────────────────────────────────────────────────────

static class Balancing2Tests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation(Wirtschaftsdaten daten) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten);

    /// <summary>Echte Daten (vier Versuche je Nacht), aber ohne Zeugen und mit fester Chance des Diebstahls je Versuch.</summary>
    static Wirtschaftsdaten Diebstahl(double chance, double versuchFaktor) => Wirtschaftsdaten.AusJson(Json.Value
        .Replace("\"diebstahl\": { \"kosten\": 150, \"chance\": 0.7", $"\"diebstahl\": {{ \"kosten\": 150, \"chance\": {chance.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
        .Replace("\"versuchFaktor\": 0.3333", $"\"versuchFaktor\": {versuchFaktor.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
        .Replace("\"zeugenChance\": 0.25", "\"zeugenChance\": 0"));

    static void BisUhrzeit(Simulation sim, int stunde)
    {
        while (sim.Zeit.UhrzeitInMinuten != stunde * 60) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Gaunerei je Nachtstunde: vier Versuche um 20, 21, 22 und 23 Uhr – jeder Erfolg holt Beute und hinterlässt eine Spur", () =>
        {
            var daten = Diebstahl(1, 1); // jeder Versuch gelingt
            Pruefe.Gleich((4, 1.0), (daten.Gauner!.NachtVersuche, daten.Gauner.VersuchFaktor));
            var sim = NeueSimulation(daten);
            var w = sim.Wirtschaft!;
            Pruefe.Gleich("0,1,2,3,-", string.Join(",", new[] { 960, 1020, 1080, 1140, 1000 }.Select(m => w.Gauner.VersuchUm(m)?.ToString() ?? "-")));
            Pruefe.Gleich(4, Unterwelt.NachtstundenMoeglich);
            var dieb = w.KontoEroeffnen("Familie Dieb", startvermoegen: 10_000);
            var opfer = w.KontoEroeffnen("Familie Opfer", startvermoegen: 10_000);
            var b = w.BetriebEroeffnen("Gießerei Opfer", Betriebsart.Giesserei, opfer, startkarren: "");
            b.Einlagern("Eisen", 100);
            Pruefe.Wahr(w.Ausfuehren(dieb, new Beauftragen(opfer.Inhaber, Gaunerstueck.Diebstahl, b.Name)).Erfolg, "Auftrag");
            var spuren = new List<int>();
            foreach (var stunde in new[] { 20, 21, 22 })
            {
                BisUhrzeit(sim, stunde);
                sim.Tick(); // die Bande schlägt zu
                spuren.Add(w.Justiz.SpurenVon(dieb).Count());
            }
            Pruefe.Wahr(w.Gauner.Auftraege.Count == 1, "der Auftrag bleibt bis 23 Uhr");
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick(); // 23 Uhr: der letzte Versuch vor der Jahresrechnung
            spuren.Add(w.Justiz.SpurenVon(dieb).Count());
            Pruefe.Gleich("1,2,3,4", string.Join(",", spuren));
            Pruefe.Gleich(100 - 4 * 12, b.ImLager("Eisen")); // je Erfolg höchstens 12 Stück
            Pruefe.Wahr(w.Gauner.Auftraege.Count == 0, "nach der Nacht ist der Auftrag erledigt");
        }),

        ("Gaunerei je Nachtstunde: Gelingt in der ganzen Nacht nichts, bleibt eine Spur – um 23 Uhr; Erwartung vier Drittel der alten Erfolge", () =>
        {
            var sim = NeueSimulation(Diebstahl(1, 0)); // kein Versuch gelingt
            var w = sim.Wirtschaft!;
            var dieb = w.KontoEroeffnen("Familie Dieb", startvermoegen: 10_000);
            var opfer = w.KontoEroeffnen("Familie Opfer", startvermoegen: 10_000);
            var b = w.BetriebEroeffnen("Gießerei Opfer", Betriebsart.Giesserei, opfer, startkarren: "");
            b.Einlagern("Eisen", 100);
            w.Ausfuehren(dieb, new Beauftragen(opfer.Inhaber, Gaunerstueck.Diebstahl, b.Name));
            BisUhrzeit(sim, 22);
            sim.Tick();
            Pruefe.Gleich(0, w.Justiz.SpurenVon(dieb).Count()); // misslungene Versuche fallen erst am Ende der Nacht auf
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick();
            Pruefe.Gleich(1, w.Justiz.SpurenVon(dieb).Count());
            Pruefe.Gleich(100, b.ImLager("Eisen"));

            // Mit den echten Werten (⅓ der Chance je Versuch, vier Versuche) gelingen im Mittel vier Drittel so viele Diebstähle wie mit einem
            // Versuch und der vollen Chance: 200 Nächte, gleiche Startwerte.
            int Erfolge(Wirtschaftsdaten daten)
            {
                var summe = 0;
                for (var n = 0; n < 200; n++)
                {
                    var s = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1000 + n },
                        new Spielzeit(19 * 60 - Spielzeit.TagesbeginnMinute), daten);
                    var ww = s.Wirtschaft!;
                    var d = ww.KontoEroeffnen("Familie Dieb", startvermoegen: 10_000);
                    var o = ww.KontoEroeffnen("Familie Opfer", startvermoegen: 10_000);
                    var g = ww.BetriebEroeffnen("Gießerei Opfer", Betriebsart.Giesserei, o, startkarren: "");
                    g.Einlagern("Eisen", 100);
                    ww.Ausfuehren(d, new Beauftragen(o.Inhaber, Gaunerstueck.Diebstahl, g.Name));
                    while (s.Zeit.Jahr == Spielzeit.StartJahr) s.Tick();
                    summe += (100 - g.ImLager("Eisen")) / 12;
                }
                return summe;
            }
            var neu = Erfolge(Diebstahl(0.7, 0.3333));
            var alt = Erfolge(Wirtschaftsdaten.AusJson(Datenpfad.EinVersuchJeNacht(Json.Value).Replace("\"zeugenChance\": 0.25", "\"zeugenChance\": 0")));
            Pruefe.Wahr(alt is > 110 and < 170, $"ein Versuch mit 70 %: {alt} von 200");
            Pruefe.Wahr(neu > alt * 1.15 && neu < alt * 1.55, $"vier Versuche mit je ⅓: {neu} gegen {alt} (erwartet etwa vier Drittel)");
        }),

        ("Schiffe: Fahrtzeiten wachsen mit dem Spieltag (× 1140/1020) – je Jahr gleich viele Fahrten wie mit 1020 Minuten", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(Json.Value);
            var typ = daten.Schiffshandel!.Schiffe["Flussschiff"];
            Pruefe.Gleich((67, 112, 22, 34), (typ.MinutenFlussab, typ.MinutenFlussauf, typ.LadeMinuten, typ.LiegeMinuten));
            var w = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten).Wirtschaft!;
            w.KarteSetzen(Reichsgilde.Core.Bauen.Stadtkarte.AusDatei(Datenpfad.Karte("probestadt")));
            foreach (var (stadt, alt) in new[] { ("Eisenfurt", 242), ("Weidenmünd", 290) })
            {
                var rund = Schiffahrt.Rundreise(typ, w.Schiffahrt.Stadt(stadt)!);
                Pruefe.Wahr(Math.Abs(rund - alt * (double)Spielzeit.MinutenProTag / 1020) < 1.5, $"{stadt}: {rund} Minuten statt {alt}");
            }
        }),

        ("Spitzel der KI: nur gegen ein Haus vor ihr (oder einen Feind), höchstens jedes zweite Jahr; vorn ohne Feind keiner", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte());
            var sim = NeueSimulation(daten);
            var w = sim.Wirtschaft!;
            Konto Haus(string name, double geld, int stand = 1)
            {
                var k = w.KontoEroeffnen(name, startvermoegen: 1_000_000);
                for (var i = 0; i < stand; i++)
                    Pruefe.Wahr(w.Ausfuehren(Wohnhaushilfe.MitWohnhaus(w, k), new StandErwerben()).Erfolg, "Aufstieg");
                k.Schaden(k.Vermoegen - geld);
                return k;
            }
            var reich = Haus("Haus Reich", 90_000, stand: 2);
            var ki = Haus("Haus Spitzel", 31_000);
            var arm = Haus("Haus Arm", 20_000);
            // Hohe Reserve: Die KI investiert nicht und bleibt hinter Haus Reich – nur der Spitzel (300) passt noch.
            var rivale = w.KiAnmelden(ki, KiProfil.Standard with { Reserve = 30_000 });
            var spitzel = new List<string>();
            var diesesJahr = "-";
            w.Meldung += m => { if (m.StartsWith("Haus Spitzel: setzt einen Spitzel auf ")) diesesJahr = m.Split("auf ")[1].Split(" an (")[0]; };
            for (var jahr = 0; jahr < 4; jahr++)
            {
                var j = sim.Zeit.Jahr;
                diesesJahr = "-";
                while (sim.Zeit.Jahr == j) sim.Tick();
                spitzel.Add(diesesJahr);
            }
            Pruefe.Gleich("Haus Reich,-,Haus Reich,-", string.Join(",", spitzel), string.Join(" | ", rivale.Entscheidungen));

            // Ein KI-Haus ganz vorn setzt ohne Feind keinen Spitzel an – das ärmere Haus liegt hinter ihm.
            var sim2 = NeueSimulation(daten);
            var w2 = sim2.Wirtschaft!;
            var vorn = w2.KontoEroeffnen("Haus Vorn", startvermoegen: 1_000_000);
            w2.Ausfuehren(Wohnhaushilfe.MitWohnhaus(w2, vorn), new StandErwerben());
            var hinten = w2.KontoEroeffnen("Haus Hinten", startvermoegen: 1_000_000);
            w2.Ausfuehren(Wohnhaushilfe.MitWohnhaus(w2, hinten), new StandErwerben());
            hinten.Schaden(hinten.Vermoegen - 20_000);
            var r2 = w2.KiAnmelden(vorn, KiProfil.Standard with { Reserve = 900_000 });
            var j2 = sim2.Zeit.Jahr;
            while (sim2.Zeit.Jahr == j2) sim2.Tick();
            Pruefe.Wahr(r2.Entscheidungen.All(e => !e.Contains("setzt einen Spitzel")), string.Join(" | ", r2.Entscheidungen));
        }),

        ("Hinfracht der Schiffe: Ein Stück fehlt danach nur zu 85 % im Angebot daheim – der Preis steigt langsamer", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(Json.Value);
            Pruefe.Gleich(0.85, daten.Schiffshandel!.HeimatmarktAnteil);
            var markt = new Markt(daten);
            var stand = markt["Silberring"];
            var voll = stand.HandKaufpreis(30, 1.1);
            var anteilig = stand.HandKaufpreis(30, 1.1, anteil: 0.85);
            Pruefe.Wahr(anteilig < voll && anteilig > stand.Preis * 1.1 * 30, $"anteilig {anteilig:0} zwischen {stand.Preis * 33:0} und {voll:0}");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"heimatmarktAnteil\": 0.85", "\"heimatmarktAnteil\": 1.5")),
                "heimatmarktAnteil");
        }),

        ("Stadtkasse: Kleine Städte zahlen kleinere Amtsgehälter – volle Gehälter ab sechs Häusern", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(Json.Value);
            Pruefe.Gleich(6, daten.Politik!.Kasse!.GehaltVolleHaeuser);
            var s = daten.Stadt!;
            Pruefe.Ungefaehr(0.7, s.Einwohner(3) / s.Einwohner(6), 1e-12);
            Pruefe.Ungefaehr(0.8, s.Einwohner(4) / s.Einwohner(6), 1e-12);
            var sim = NeueSimulation(daten);
            var w = sim.Wirtschaft!;
            for (var i = 0; i < 7; i++)
            {
                var k = w.KontoEroeffnen($"Haus {i}", startvermoegen: 10_000);
                var b = w.BetriebEroeffnen($"Gießerei {i}", Betriebsart.Giesserei, k, startkarren: "");
                w.Einstellen(b, "Geselle", 1);
                var erwartet = Math.Min(1, s.Einwohner(i + 1) / s.Einwohner(6));
                Pruefe.Ungefaehr(erwartet, w.Kasse.Gehaltsfaktor, 1e-12);
            }
        }),
    };
}
