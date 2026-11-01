using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Sitzplätze der Schänken (Datenversion 66, Entscheidung des Nutzers 1.11.2026): Schänke 20, Wirtshaus 30, Gasthaus 40. Wer keinen Platz findet,
/// geht in die nächste freie Schänke; ist überall voll, fällt der Besuch aus. Ohne Sitzplätze in den Daten wie bis Datenversion 65.
/// </summary>
static class SchankplaetzeTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Die Daten ohne Sitzplätze – so rechnen alte Spielstände mit ihren eigenen Daten.</summary>
    static readonly Lazy<Wirtschaftsdaten> OhnePlaetze = new(() => Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft())
        .Replace("\"sitzplaetzeJeStufe\": [ 20, 30, 40 ],", "")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    static (Stadtwirtschaft W, Konto K, Betrieb S) Wirt(Simulation sim, Betriebsart art = Betriebsart.Schaenke, string name = "Familie Krug")
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: 10_000);
        var s = w.BetriebEroeffnen($"Schänke {name}", art, k, startkarren: "");
        w.Einstellen(s, "Geselle", 1);
        s.Einlagern("Dünnbier", 400);
        return (w, k, s);
    }

    static void BisUhrzeit(Simulation sim, int stunde)
    {
        while (sim.Zeit.UhrzeitInMinuten < stunde * 60) sim.Tick();
    }

    /// <summary>Bis kurz nach der vollen Stunde: Die Gäste der Stunde davor sind dann im Gästebuch.</summary>
    static void KurzNach(Simulation sim, int stunde)
    {
        BisUhrzeit(sim, stunde);
        sim.Tick();
    }

    static void TagVorbei(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Sitzplätze: Daten 20/30/40 je Stufe (Datenversion 66); ohne Angabe ohne Grenze; Werte unter 1 lehnt die Prüfung ab", () =>
        {
            Pruefe.Gleich(66, Daten.Value.Version);
            Pruefe.Wahr(Daten.Value.Gastwirtschaft!.SitzplaetzeJeStufe.SequenceEqual(new[] { 20, 30, 40 }), "sitzplaetzeJeStufe");
            var sim = NeueSimulation();
            var (w, _, s) = Wirt(sim);
            var (_, _, wh) = Wirt(sim, Betriebsart.Wirtshaus, "Familie Humpen");
            var (_, _, gh) = Wirt(sim, Betriebsart.Gasthaus, "Familie Fass");
            Pruefe.Gleich(20, w.Gastwirtschaft.Sitzplaetze(s));
            Pruefe.Gleich(30, w.Gastwirtschaft.Sitzplaetze(wh));
            Pruefe.Gleich(40, w.Gastwirtschaft.Sitzplaetze(gh));
            var schmiede = w.BetriebEroeffnen("Schmiede Probe", Betriebsart.Giesserei, w.KontoEroeffnen("Familie Amboss"), startkarren: "");
            Pruefe.Wahr(w.Gastwirtschaft.Sitzplaetze(schmiede) is null && w.Gastwirtschaft.Platzbelegung(schmiede) is null, "keine Schänke");

            var alt = NeueSimulation(OhnePlaetze.Value);
            var (w2, _, s2) = Wirt(alt);
            Pruefe.Wahr(w2.Gastwirtschaft.Sitzplaetze(s2) is null && w2.Gastwirtschaft.Platzbelegung(s2) is null, "ohne Daten keine Grenze");
            var falsch = File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"sitzplaetzeJeStufe\": [ 20, 30, 40 ]", "\"sitzplaetzeJeStufe\": [ 20, 0, 40 ]");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(falsch), "sitzplaetzeJeStufe");
        }),

        ("Sitzplätze: Allein in der Stadt mit Barde sitzen abends höchstens 20 – die übrigen finden keinen Platz, ihr Besuch fällt aus", () =>
        {
            var sim = NeueSimulation();
            var (w, k, s) = Wirt(sim);
            var g = w.Gastwirtschaft;
            Pruefe.Wahr(w.Ausfuehren(k, new BardeEinladen(s.Name)).Erfolg, "Barde");
            KurzNach(sim, 18);
            var vorher = g.DiesesJahr(s);
            KurzNach(sim, 19); // die Stunde 18–19 Uhr: 120 × 2/14 × 1,5 ≈ 25,7 Gäste wollen herein
            var buch = g.DiesesJahr(s);
            Pruefe.Gleich(20, buch.Bewirtet - vorher.Bewirtet + buch.Abgewiesen - vorher.Abgewiesen);
            Pruefe.Wahr(buch.Voll - vorher.Voll is >= 5 and <= 6, $"voll {buch.Voll - vorher.Voll}");
            Pruefe.Gleich(buch.Voll, buch.Ausgefallen); // keine andere Schänke
            var platz = g.Platzbelegung(s)!;
            Pruefe.Gleich(new Schankplaetze(20, 20, 0), platz);

            // Ohne Sitzplätze kommen alle (wie bis Datenversion 65).
            var alt = NeueSimulation(OhnePlaetze.Value);
            var (w2, k2, s2) = Wirt(alt);
            Pruefe.Wahr(w2.Ausfuehren(k2, new BardeEinladen(s2.Name)).Erfolg, "Barde");
            KurzNach(alt, 18);
            var a = w2.Gastwirtschaft.DiesesJahr(s2).Bewirtet;
            KurzNach(alt, 19);
            Pruefe.Wahr(w2.Gastwirtschaft.DiesesJahr(s2).Bewirtet - a is >= 25 and <= 26, "ohne Grenze");
            Pruefe.Gleich(0, w2.Gastwirtschaft.DiesesJahr(s2).Voll);
        }),

        ("Sitzplätze: Ist die eine Schänke voll, gehen die Gäste in die nächste freie und zahlen dort (Gäste ohne Kopplung an die Einwohner)", () =>
        {
            // Ohne Kopplung an die Einwohner: Die Gäste der Stunde hängen dann nur an der Anziehung, nicht daran, wie viele Einwohner Platz fanden.
            static string OhneKopplung(string json) => json.Replace("\"kopplung\": { \"schaenke\": 1.0", "\"kopplung\": { \"schaenke\": 0.0");
            var sim = NeueSimulation(Wirtschaftsdaten.AusJson(OhneKopplung(File.ReadAllText(Datenpfad.Wirtschaft()))));
            var (w, k, gasthaus) = Wirt(sim, Betriebsart.Gasthaus, "Familie Fass");   // Anziehung 2, 40 Plätze
            var (_, _, schaenke) = Wirt(sim, name: "Familie Krug");                  // Anziehung 1, 20 Plätze
            var g = w.Gastwirtschaft;
            TagVorbei(sim);
            var gh = g.Vorjahr(gasthaus)!;
            var sch = g.Vorjahr(schaenke)!;
            // Die Schänke ist nie voll (abends ≈ 5,7 eigene Gäste), das Gasthaus auch nicht (≈ 11,4) – hier läuft nichts über.
            Pruefe.Gleich(0, gh.Voll + sch.Voll);

            // Mit nur 2 Plätzen im Gasthaus: Was nicht hineinpasst, sitzt in der Schänke (abends ≈ 9,4 + 5,7 < 20) – nichts fällt aus, die Gäste der Stadt bleiben.
            var eng = Wirtschaftsdaten.AusJson(OhneKopplung(File.ReadAllText(Datenpfad.Wirtschaft())).Replace("\"sitzplaetzeJeStufe\": [ 20, 30, 40 ]", "\"sitzplaetzeJeStufe\": [ 20, 30, 2 ]"));
            var sim2 = NeueSimulation(eng);
            var (w2, k2, gasthaus2) = Wirt(sim2, Betriebsart.Gasthaus, "Familie Fass");
            var (_, _, schaenke2) = Wirt(sim2, name: "Familie Krug");
            TagVorbei(sim2);
            var gh2 = w2.Gastwirtschaft.Vorjahr(gasthaus2)!;
            var sch2 = w2.Gastwirtschaft.Vorjahr(schaenke2)!;
            Pruefe.Wahr(gh2.Voll > 0, "das Gasthaus war nie voll");
            Pruefe.Gleich(0, gh2.Ausgefallen);
            Pruefe.Gleich(10 * 2, gh2.Bewirtet); // 12–22 Uhr je 2
            Pruefe.Gleich(gh.Bewirtet + sch.Bewirtet, gh2.Bewirtet + sch2.Bewirtet); // alle Gäste der Stadt finden Platz
            Pruefe.Wahr(sch2.Einnahmen > sch.Einnahmen, "die Schänke nimmt die Gäste des Gasthauses nicht auf");
        }),

        ("Sitzplätze: Barde und erwartete Gäste rechnen mit den Plätzen – ist die Schänke schon voll, bringt der Barde nur die höhere Zeche", () =>
        {
            var sim = NeueSimulation();
            var (w, _, s) = Wirt(sim);
            var alt = NeueSimulation(OhnePlaetze.Value);
            var (w2, _, s2) = Wirt(alt);
            // 120 Gäste allein, abends ≈ 17,1 je Stunde – unter 20 Plätzen: ohne Barde gleich wie ohne Grenze
            Pruefe.Gleich(w2.Gastwirtschaft.ErwarteteGaeste(s2), w.Gastwirtschaft.ErwarteteGaeste(s));
            // mit Barde 25,7 je Abendstunde → 20: weniger Nutzen als ohne Grenze, aber noch etwas
            var mit = w.Gastwirtschaft.BardeNutzen(s);
            var ohne = w2.Gastwirtschaft.BardeNutzen(s2);
            Pruefe.Wahr(mit < ohne && mit > 0, $"mit Plätzen {mit:0}, ohne {ohne:0}");
            // Mit 10 Plätzen: abends höchstens 10, nachmittags ≈ 8,6
            var zehn = Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"sitzplaetzeJeStufe\": [ 20, 30, 40 ]", "\"sitzplaetzeJeStufe\": [ 10, 30, 40 ]"));
            var sim3 = NeueSimulation(zehn);
            var (w3, _, s3) = Wirt(sim3);
            Pruefe.Ungefaehr(120.0 * 6 / 14 + 4 * 10, w3.Gastwirtschaft.ErwarteteGaeste(s3), 1e-9);
            // Schon ohne Barden voll: kein Gast mehr, nur 10 % mehr Zeche von den 10 je Abendstunde (die Schänke schenkt Körnermahlzeit und Dünnbier aus)
            var zeche = new[] { "Körnermahlzeit", "Dünnbier" }.Select(w3.Gastwirtschaft.Ausschankpreis).Average();
            Pruefe.Ungefaehr(4 * 10 * zeche * w3.Gastwirtschaft.Daten.Barde.Zeche, w3.Gastwirtschaft.BardeNutzen(s3), 1e-9);
        }),

        ("Sitzplätze: Platzbelegung folgt dem Gastraum – vor 12 Uhr alles frei, nachmittags ≈ 9 belegt", () =>
        {
            var sim = NeueSimulation();
            var (w, _, s) = Wirt(sim, Betriebsart.Wirtshaus);
            var g = w.Gastwirtschaft;
            Pruefe.Gleich(new Schankplaetze(30, 0, 30), g.Platzbelegung(s));
            BisUhrzeit(sim, 12);
            Pruefe.Gleich(new Schankplaetze(30, 9, 21), g.Platzbelegung(s));
            BisUhrzeit(sim, 18);
            Pruefe.Gleich(new Schankplaetze(30, 17, 13), g.Platzbelegung(s));
        }),

        ("Sitzplätze: In einer Partie sitzen nie mehr Einwohner in einer Schänke, als sie Plätze hat; wer keinen findet, kommt später wieder", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 101 },
                Daten.Value, new NeuePartie(Beruf: "Wirt"));
            var w = sim.Wirtschaft!;
            var ew = w.Einwohnerschaft;
            Pruefe.Wahr(ew.IstAktiv, "ohne Einwohner");
            var schaenke = w.Betriebe.First(b => b.Konto == spieler && b.IstSchaenke);
            schaenke.Einlagern("Dünnbier", 300);
            int hoechstens = 0, ohnePlatz = 0;
            for (var tag = 0; tag < 2; tag++)
            {
                var jahr = sim.Zeit.Jahr;
                while (sim.Zeit.Jahr == jahr)
                {
                    sim.Tick();
                    foreach (var b in w.Gastwirtschaft.Schaenken)
                    {
                        var drin = ew.Einkehrende(b);
                        hoechstens = Math.Max(hoechstens, drin);
                        Pruefe.Wahr(drin <= w.Gastwirtschaft.Sitzplaetze(b)!.Value, $"{drin} Einwohner in {b.Name} ({sim.Zeit.UhrzeitInMinuten / 60} Uhr)");
                    }
                    ohnePlatz = Math.Max(ohnePlatz, ew.OhneSchankplatz);
                }
            }
            Pruefe.Gleich(20, hoechstens); // abends voll
            Pruefe.Wahr(ohnePlatz > 0, "niemand fand keinen Platz");
        }),

        ("Sitzplätze: Gästebuch mit „voll“ und „ausgefallen“ übersteht Speichern und Laden; ein altes Gästebuch ohne die Felder lädt mit 0", () =>
        {
            var sim = NeueSimulation();
            var (w, k, s) = Wirt(sim);
            Pruefe.Wahr(w.Ausfuehren(k, new BardeEinladen(s.Name)).Erfolg, "Barde");
            KurzNach(sim, 20);
            var buch = w.Gastwirtschaft.DiesesJahr(s);
            Pruefe.Wahr(buch.Voll > 0 && buch.Ausgefallen > 0, "nichts voll");
            var text = Speicherung.Speichern(sim, null, new DateTime(2026, 1, 1));
            var (geladen, _) = Speicherung.Laden(text);
            var s2 = geladen.Wirtschaft!.Betriebe.First(b => b.Name == s.Name);
            Pruefe.Gleich(buch, geladen.Wirtschaft.Gastwirtschaft.DiesesJahr(s2));

            var alt = System.Text.Json.JsonSerializer.Deserialize<Gaestebuch>("{\"Bewirtet\":3,\"Abgewiesen\":1,\"Einnahmen\":5.5,\"Wuerfelgeld\":0}")!;
            Pruefe.Gleich(new Gaestebuch(3, 1, 5.5, 0), alt);
            var ohneVoll = System.Text.Json.JsonSerializer.Serialize(new Gaestebuch(3, 1, 5.5, 0));
            Pruefe.Wahr(!ohneVoll.Contains("Voll") && !ohneVoll.Contains("Ausgefallen"), $"leere Felder im Spielstand: {ohneVoll}");
        }),
    };
}
