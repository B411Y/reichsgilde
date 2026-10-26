using Reichsgilde.Core.Zeit;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für den Wirt: Gäste und Ausschank, Vorrat, Barde, Würfeltisch, Stammtisch, dunkle Ecke.</summary>
static class WirtTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Daten, in denen die dunkle Ecke jedes Gerücht hört – damit der Test nicht vom Zufall abhängt.</summary>
    static readonly Lazy<Wirtschaftsdaten> HellhoerigeEcke = new(() => Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft())
        .Replace("\"dunkleEcke\": { \"chance\": 0.2 }", "\"dunkleEcke\": { \"chance\": 1.0 }")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Bis Mitternacht: Die Partie beginnt um 6 Uhr, die Schänken öffnen um 12.</summary>
    /// <summary>Den ganzen Spieltag bis 22 Uhr spielen – die letzte Stunde rechnet die Jahresrechnung ab, das Gästebuch steht dann im Vorjahr.</summary>
    static void TagVorbei(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static void BisUhrzeit(Simulation sim, int stunde)
    {
        while (sim.Zeit.UhrzeitInMinuten < stunde * 60) sim.Tick();
    }

    /// <summary>Eine Schänke der gewünschten Art mit Personal und Bier im Lager, ohne Karren.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb S) Wirt(Simulation sim, Betriebsart art = Betriebsart.Schaenke, int bier = 200, string name = "Familie Krug")
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: 10_000);
        var s = w.BetriebEroeffnen($"Schänke {name}", art, k, startkarren: "");
        w.Einstellen(s, "Geselle", 1);
        s.Einlagern("Dünnbier", bier);
        return (w, k, s);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: Bauernhof baut Gerste und Roggen an; Schänke → Wirtshaus → Gasthaus; Schädelbrand braucht Schmied und Tischler", () =>
        {
            var d = Daten.Value;
            Pruefe.Wahr(d.Art(Betriebsart.Bauernhof).Rohstoffe.SequenceEqual(new[] { "Gerste", "Roggen" }), "Bauernhof");
            Pruefe.Wahr(d.Art(Betriebsart.Schaenke).Produkte.SequenceEqual(new[] { "Weingeist", "Körnermahlzeit", "Dünnbier" }), "Schänke");
            Pruefe.Gleich(Betriebsart.Gasthaus, d.Art(Betriebsart.Wirtshaus).Ausbau);
            Pruefe.Gleich(1, d.Waren["Schädelbrand"].Zutaten["Beschlag"]);
            Pruefe.Gleich(1, d.Waren["Schädelbrand"].Zutaten["Holzzapfen"]);
            Pruefe.Gleich(1, d.Waren["Kräuterlikör"].Zutaten["Kräuter"]);
            Pruefe.Wahr(!d.Gastwirtschaft!.Ausschank.Contains("Weingeist"), "Weingeist ist Zwischenware, kein Ausschank");
            var json = File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"ausschank\": [ \"Körnermahlzeit\"", "\"ausschank\": [ \"Bratwurst\"");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json), "„Bratwurst“ gibt es nicht");
        }),

        ("Gäste: kommen ab 12 Uhr, abends doppelt so viele – eine Schänke allein bewirtet alle 120 Gäste des Jahres zum Ausschankpreis", () =>
        {
            var sim = NeueSimulation();
            var (w, k, s) = Wirt(sim);
            var g = w.Gastwirtschaft;
            for (var i = 0; i < 13 * 60 + 1 - Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // bis 13 Uhr: die erste Stunde ist vorbei
            Pruefe.Wahr(g.DiesesJahr(s).Bewirtet is >= 8 and <= 9, $"nach der ersten Stunde {g.DiesesJahr(s).Bewirtet}"); // 120 × 1/14 (12–18 Uhr je 1, 18–22 Uhr je 2)
            var vorher = k.Laufend.Umsatz;
            TagVorbei(sim); // bis 22 Uhr – die Gäste der letzten Stunde zählen noch zum Jahr
            var buch = g.Vorjahr(s)!;
            Pruefe.Wahr(buch.Bewirtet is >= 119 and <= 120, $"bewirtet {buch.Bewirtet}");
            Pruefe.Ungefaehr(buch.Bewirtet * 85 * 1.2, buch.Einnahmen, 1e-9); // Aufschlag 1,2 seit PR #177
            Pruefe.Wahr(k.Vorjahr!.Umsatz > vorher, "kein Umsatz");
            Pruefe.Gleich(0, buch.Abgewiesen);
        }),

        ("Gäste: Ist nichts mehr da, gehen sie wieder; der Karren lässt 15 Stück je Ausschankware für die Gäste im Lager", () =>
        {
            var sim = NeueSimulation();
            var (w, _, s) = Wirt(sim, bier: 10);
            TagVorbei(sim);
            var buch = w.Gastwirtschaft.Vorjahr(s)!;
            Pruefe.Gleich(10, buch.Bewirtet);
            Pruefe.Wahr(buch.Abgewiesen >= 109, $"abgewiesen {buch.Abgewiesen}");

            s.Einlagern("Dünnbier", 20);
            s.Einlagern("Weingeist", 20);
            Pruefe.Gleich(15, s.FuerGaeste("Dünnbier"));
            Pruefe.Gleich(0, s.FuerGaeste("Weingeist"));
        }),

        ("Konkurrenz: Gäste verteilen sich nach Anziehung – ein Wirtshaus (Stufe 2) zieht anderthalbmal so viele wie eine Schänke", () =>
        {
            var sim = NeueSimulation();
            var (w, _, schaenke) = Wirt(sim, name: "Familie Krug");
            var (_, _, wirtshaus) = Wirt(sim, Betriebsart.Wirtshaus, name: "Familie Humpen");
            TagVorbei(sim);
            var g = w.Gastwirtschaft;
            var a = g.Vorjahr(schaenke)!.Bewirtet;
            var b = g.Vorjahr(wirtshaus)!.Bewirtet;
            Pruefe.Wahr(a + b is >= 118 and <= 120, $"{a} + {b}");
            Pruefe.Ungefaehr(1.5, b / (double)a, 0.05);
        }),

        ("Barde: abends 50 % mehr Gäste, jeder zahlt 10 % mehr; einmal je Jahr, 120 Gulden Lohn", () =>
        {
            var sim = NeueSimulation();
            var (w, k, s) = Wirt(sim);
            var vorher = k.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(k, new BardeEinladen(s.Name)).Erfolg, "Barde");
            Pruefe.Ungefaehr(vorher - 120, k.Vermoegen, 1e-9);
            Pruefe.Wahr(!w.Ausfuehren(k, new BardeEinladen(s.Name)).Erfolg, "zweimal");
            TagVorbei(sim);
            var buch = w.Gastwirtschaft.Vorjahr(s)!;
            Pruefe.Wahr(buch.Bewirtet is >= 153 and <= 155, $"bewirtet {buch.Bewirtet}"); // 120 × 6/14 nachmittags + 120 × 8/14 × 1,5 abends
            Pruefe.Wahr(buch.Einnahmen > buch.Bewirtet * 85 * 1.2, "keine höhere Zeche");
            Pruefe.Wahr(!w.Gastwirtschaft.BardeGebucht(s), "Der Barde zieht zum Jahresende weiter");
        }),

        ("Würfeltisch: erst im Wirtshaus; bringt 4 Gulden je Gast, kostet 2 Ansehen", () =>
        {
            var sim = NeueSimulation();
            var (w, k, s) = Wirt(sim);
            var e = w.Ausfuehren(k, new WuerfeltischAufstellen(s.Name, true));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Stufe 2"), e.Meldung);
            var (_, k2, wirtshaus) = Wirt(sim, Betriebsart.Wirtshaus, name: "Familie Humpen");
            var ansehen = w.Rat.Ansehen(k2);
            Pruefe.Wahr(w.Ausfuehren(k2, new WuerfeltischAufstellen(wirtshaus.Name, true)).Erfolg, "Würfeltisch");
            Pruefe.Ungefaehr(ansehen - 2, w.Rat.Ansehen(k2), 1e-9);
            TagVorbei(sim);
            var buch = w.Gastwirtschaft.Vorjahr(wirtshaus)!;
            Pruefe.Ungefaehr(buch.Bewirtet * 4.0, buch.Wuerfelgeld, 1e-9);
            Pruefe.Gleich(0.0, w.Gastwirtschaft.Vorjahr(s)!.Wuerfelgeld);
        }),

        ("Stammtisch: Ein Gasthaus bringt am Jahresende Gunst bei jedem Ratsherrn – mit Barde doppelt, ohne Bestechungsspur", () =>
        {
            var sim = NeueSimulation();
            var (w, k, s) = Wirt(sim, Betriebsart.Gasthaus);
            var (_, k2, _) = Wirt(sim, Betriebsart.Wirtshaus, name: "Familie Humpen");
            Pruefe.Wahr(w.Ausfuehren(k, new BardeEinladen(s.Name)).Erfolg, "Barde");
            var ratsherr = w.Rat.Ratsherren[0];
            JahrBeenden(sim);
            // Gunst verfällt in der Ratssitzung um 30 % – der Stammtisch kommt vorher: 4 × 2 × 0,7
            Pruefe.Ungefaehr(4 * 2 * 0.7, w.Rat.Gunst(ratsherr, k), 1e-9);
            Pruefe.Gleich(0.0, w.Rat.Gunst(ratsherr, k2)); // ein Wirtshaus hat keinen Stammtisch
            Pruefe.Gleich(0, w.Justiz.SpurenVon(k).Count());
        }),

        ("Dunkle Ecke: Wer eine Schänke führt, hört am Gerichtstag Gerüchte über die Taten anderer Häuser", () =>
        {
            var sim = NeueSimulation(HellhoerigeEcke.Value);
            var (w, wirt, _) = Wirt(sim);
            var fremd = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
            var ohneSchaenke = w.KontoEroeffnen("Familie Fuchs", startvermoegen: 5_000);
            Pruefe.Wahr(w.Ausfuehren(fremd, new Schenken(w.Rat.Ratsherren[0].Name, 100)).Erfolg, "Bestechung");
            JahrBeenden(sim);
            Pruefe.Gleich(1, w.Justiz.Beweise(wirt, fremd).Count());
            Pruefe.Gleich(0, w.Justiz.Beweise(ohneSchaenke, fremd).Count());
        }),

        ("KI: holt für ihr Gasthaus einen Barden; ein wagemutiges Haus stellt im Wirtshaus einen Würfeltisch auf", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (_, k1, gasthaus) = Wirt(sim, Betriebsart.Gasthaus, name: "Haus Krug");
            var (_, k2, wirtshaus) = Wirt(sim, Betriebsart.Wirtshaus, name: "Haus Humpen");
            w.KiAnmelden(k1, KiProfil.Standard);
            w.KiAnmelden(k2, Partieaufbau.ProfilFuer(KiCharakter.Wagemutig));
            for (var i = 0; i < 12 * 60 - Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // über die Politikstunde (12 Uhr)
            Pruefe.Wahr(w.Gastwirtschaft.BardeGebucht(gasthaus), "kein Barde im Gasthaus");
            Pruefe.Wahr(w.Gastwirtschaft.HatWuerfeltisch(wirtshaus), "kein Würfeltisch beim Wagemutigen");
            Pruefe.Wahr(!w.Gastwirtschaft.HatWuerfeltisch(gasthaus), "Würfeltisch beim Ausgewogenen");
        }),

        ("Speichern: Gästebuch, Barde, Würfeltisch und angefangene Gäste überstehen Speichern und Laden; Startberuf Wirt", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Beruf: "Wirt", Startvermoegen: 10_000));
            var w = sim.Wirtschaft!;
            var s = w.Betriebe.Single(b => b.Konto == spieler);
            Pruefe.Gleich("Schänke am Weinmarkt", s.Name);
            s.Einlagern("Körnermahlzeit", 30);
            Pruefe.Wahr(w.Ausfuehren(spieler, new BardeEinladen(s.Name)).Erfolg, "Barde");
            for (var i = 0; i < 14 * 60 + 30 - Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // 14:30 – Gäste waren schon da
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var ks = kopie.Wirtschaft!.Betriebe.Single(b => b.Konto == kopieSpieler);
            var g = kopie.Wirtschaft.Gastwirtschaft;
            Pruefe.Gleich(w.Gastwirtschaft.DiesesJahr(s), g.DiesesJahr(ks));
            Pruefe.Wahr(g.BardeGebucht(ks), "Barde vergessen");
            for (var i = 0; i < 60; i++) { sim.Tick(); kopie.Tick(); }
            Pruefe.Gleich(w.Gastwirtschaft.DiesesJahr(s), g.DiesesJahr(ks)); // gleicher Verlauf
        }),

        ("Gäste im Raum: keine vor 12 Uhr, nachmittags die Gäste einer Stunde, abends doppelt so viele – höchstens 12 Plätze", () =>
        {
            var sim = NeueSimulation();
            var (w, _, s) = Wirt(sim);
            var g = w.Gastwirtschaft;
            Pruefe.Gleich(0, g.GaesteJetzt(s)); // 5 Uhr
            BisUhrzeit(sim, 11);
            Pruefe.Gleich(0, g.GaesteJetzt(s));
            BisUhrzeit(sim, 12);
            Pruefe.Gleich(9, g.GaesteJetzt(s)); // allein in der Stadt: 120 × 1/14 ≈ 8,6
            BisUhrzeit(sim, 18);
            Pruefe.Gleich(12, g.GaesteJetzt(s)); // 120 × 2/14 ≈ 17,1 – gedeckelt
            Pruefe.Gleich(17, g.GaesteJetzt(s, hoechstens: 40));
            Pruefe.Gleich(5, g.GaesteJetzt(s, hoechstens: 5));
        }),

        ("Gäste im Raum: Bei Konkurrenz nach Anziehung, abends mehr als mittags; das Wirtshaus hat mehr als die Schänke", () =>
        {
            var sim = NeueSimulation();
            var (w, _, schaenke) = Wirt(sim, name: "Familie Krug");
            var (_, _, wirtshaus) = Wirt(sim, Betriebsart.Wirtshaus, name: "Familie Humpen");
            var g = w.Gastwirtschaft;
            BisUhrzeit(sim, 12);
            var mittags = (g.GaesteJetzt(schaenke), g.GaesteJetzt(wirtshaus));
            Pruefe.Gleich((int)Math.Round(g.ErwarteteGaeste(schaenke) / 14, MidpointRounding.AwayFromZero), mittags.Item1);
            Pruefe.Wahr(mittags.Item1 >= 1 && mittags.Item2 > mittags.Item1, $"mittags {mittags}");
            BisUhrzeit(sim, 18);
            var abends = (g.GaesteJetzt(schaenke), g.GaesteJetzt(wirtshaus));
            Pruefe.Gleich((int)Math.Round(g.ErwarteteGaeste(wirtshaus) * 2 / 14, MidpointRounding.AwayFromZero), abends.Item2);
            Pruefe.Wahr(abends.Item1 > mittags.Item1 && abends.Item2 > mittags.Item2 && abends.Item2 <= 12, $"mittags {mittags}, abends {abends}");
        }),

        ("Gäste im Raum: Ohne Ausschankware im Lager, ohne Arbeiter oder ohne Gastwirtschaft sitzt niemand", () =>
        {
            var sim = NeueSimulation();
            var (w, k, s) = Wirt(sim, bier: 0);
            var g = w.Gastwirtschaft;
            BisUhrzeit(sim, 12);
            Pruefe.Gleich(0, g.GaesteJetzt(s));
            s.Einlagern("Weingeist", 10); // Zwischenware, kein Ausschank
            Pruefe.Gleich(0, g.GaesteJetzt(s));
            s.Einlagern("Körnermahlzeit", 10);
            Pruefe.Gleich(9, g.GaesteJetzt(s));
            Pruefe.Wahr(w.Ausfuehren(k, new PersonalSetzen(s.Name, "Geselle", 0)).Erfolg, "Geselle entlassen");
            Pruefe.Gleich(0, g.GaesteJetzt(s));
            Pruefe.Wahr(!g.BardeSpielt(s) && !g.WuerfeltischBesetzt(s) && !g.StammtischBesetzt(s), "leere Schänke");

            var ohne = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 },
                wirtschaftsdaten: Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"gastwirtschaft\":", "\"gastwirtschaftAlt\":")));
            var (w2, _, s2) = Wirt(ohne);
            BisUhrzeit(ohne, 18);
            Pruefe.Wahr(!w2.Gastwirtschaft.IstAktiv, "Gastwirtschaft aktiv");
            Pruefe.Gleich(0, w2.Gastwirtschaft.GaesteJetzt(s2));
            Pruefe.Wahr(!w2.Gastwirtschaft.BardeSpielt(s2) && !w2.Gastwirtschaft.StammtischBesetzt(s2), "ohne Gastwirtschaft");
        }),

        ("Gäste im Raum: Der Barde spielt nur, wenn er gebucht ist, und erst ab 18 Uhr – dann sitzen anderthalbmal so viele", () =>
        {
            var sim = NeueSimulation();
            var (w, k, s) = Wirt(sim);
            var g = w.Gastwirtschaft;
            BisUhrzeit(sim, 12);
            Pruefe.Wahr(!g.BardeSpielt(s), "ohne Buchung");
            Pruefe.Wahr(w.Ausfuehren(k, new BardeEinladen(s.Name)).Erfolg, "Barde");
            Pruefe.Wahr(!g.BardeSpielt(s), "mittags");
            Pruefe.Gleich(9, g.GaesteJetzt(s));
            BisUhrzeit(sim, 18);
            Pruefe.Wahr(g.BardeSpielt(s), "abends");
            Pruefe.Gleich(26, g.GaesteJetzt(s, hoechstens: 40)); // 120 × 2/14 × 1,5 ≈ 25,7
            Pruefe.Gleich(12, g.GaesteJetzt(s));
        }),

        ("Gäste im Raum: Würfeltisch besetzt, wenn er steht und Gäste da sind; Stammtisch erst im Gasthaus und nur abends", () =>
        {
            var sim = NeueSimulation();
            var (w, _, schaenke) = Wirt(sim, name: "Familie Krug");
            var (_, k2, wirtshaus) = Wirt(sim, Betriebsart.Wirtshaus, name: "Familie Humpen");
            var (_, _, gasthaus) = Wirt(sim, Betriebsart.Gasthaus, name: "Familie Kanne");
            var g = w.Gastwirtschaft;
            Pruefe.Wahr(w.Ausfuehren(k2, new WuerfeltischAufstellen(wirtshaus.Name, true)).Erfolg, "Würfeltisch");
            Pruefe.Wahr(!g.WuerfeltischBesetzt(wirtshaus), "vor 12 Uhr");
            BisUhrzeit(sim, 12);
            Pruefe.Wahr(g.GaesteJetzt(wirtshaus) >= 2 && g.WuerfeltischBesetzt(wirtshaus), $"Würfeltisch mit {g.GaesteJetzt(wirtshaus)} Gästen");
            Pruefe.Wahr(!g.WuerfeltischBesetzt(schaenke) && !g.WuerfeltischBesetzt(gasthaus), "ohne Würfeltisch");
            Pruefe.Wahr(!g.StammtischBesetzt(gasthaus), "Stammtisch mittags");
            BisUhrzeit(sim, 18);
            Pruefe.Wahr(g.StammtischBesetzt(gasthaus), "Stammtisch abends im Gasthaus");
            Pruefe.Wahr(!g.StammtischBesetzt(wirtshaus) && !g.StammtischBesetzt(schaenke), "Stammtisch erst ab Stufe 3");
        }),

        ("Gäste im Raum: Die Abfragen ändern nichts – ein Jahr mit ständigen Abfragen verläuft zeichengleich", () =>
        {
            string Spielen(bool abfragen)
            {
                var sim = NeueSimulation();
                var (w, k, s) = Wirt(sim, Betriebsart.Gasthaus);
                Pruefe.Wahr(w.Ausfuehren(k, new BardeEinladen(s.Name)).Erfolg, "Barde");
                var summe = 0;
                while (sim.Zeit.Jahr == Reichsgilde.Core.Zeit.Spielzeit.StartJahr)
                {
                    sim.Tick();
                    if (!abfragen) continue;
                    foreach (var b in w.Betriebe.Where(b => b.IstSchaenke))
                        summe += w.Gastwirtschaft.GaesteJetzt(b) + (w.Gastwirtschaft.BardeSpielt(b) ? 1 : 0)
                                 + (w.Gastwirtschaft.WuerfeltischBesetzt(b) ? 1 : 0) + (w.Gastwirtschaft.StammtischBesetzt(b) ? 1 : 0);
                }
                Pruefe.Wahr(!abfragen || summe > 0, "keine Gäste gesehen");
                return Speicherung.Speichern(sim, k, new DateTime(2026, 10, 21, 12, 0, 0));
            }
            Pruefe.Gleich(Spielen(false), Spielen(true));
        }),
    };
}
