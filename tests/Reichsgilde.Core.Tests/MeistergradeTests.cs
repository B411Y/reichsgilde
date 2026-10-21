using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für Meistergrade (Erfahrung + Prüfung) und Gebäudestufen (Ausbau am Platz).</summary>
static class MeistergradeTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation(int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Ein Haus mit Gießerei, das schon Meister im angegebenen Grad ist (Erfahrung und Grad direkt gesetzt).</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb G) Meister(Simulation sim, int grad)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Familie Fuchs", startvermoegen: 30_000);
        var g = w.BetriebEroeffnen("Gießerei Fuchs", Betriebsart.Giesserei, k);
        k.GradSetzen("Schmied", grad);
        return (w, k, g);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: drei Stufen je Beruf mit 2/3/4 Plätzen – jede höhere Stufe kann alles der niedrigeren und mehr", () =>
        {
            var d = Daten.Value;
            foreach (var (s1, s2, s3) in new[] { (Betriebsart.Giesserei, Betriebsart.Schmiede, Betriebsart.Goldschmiede),
                                                 (Betriebsart.Tischlerei, Betriebsart.Drechslerei, Betriebsart.Saegewerk) })
            {
                var (a, b, c) = (d.Art(s1), d.Art(s2), d.Art(s3));
                Pruefe.Wahr(a.Stufe == 1 && b.Stufe == 2 && c.Stufe == 3, $"{a.Name}: Stufen");
                Pruefe.Wahr(a.Plaetze == 2 && b.Plaetze == 3 && c.Plaetze == 4, $"{a.Name}: Plätze");
                Pruefe.Wahr(a.Ausbau == s2 && b.Ausbau == s3 && c.Ausbau is null, $"{a.Name}: Ausbau");
                Pruefe.Wahr(a.Produkte.All(b.Produkte.Contains) && b.Produkte.All(c.Produkte.Contains), $"{a.Name}: Produkte");
                Pruefe.Wahr(b.Produkte.Count > a.Produkte.Count && c.Produkte.Count > b.Produkte.Count, $"{a.Name}: neue Waren");
            }
            Pruefe.Gleich(6, d.Meistergrade!.HoechsterGrad);
            Pruefe.Gleich(5, d.Meistergrade.GradFuerStufe(3));

            var json = File.ReadAllText(Datenpfad.Wirtschaft());
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json.Replace("\"produkte\": [ \"Beschlag\", \"Rapier\", \"Silberring\", \"Werkzeug\", \"Silberkette\" ]",
                "\"produkte\": [ \"Beschlag\", \"Silberring\", \"Werkzeug\", \"Silberkette\" ]")), "ginge „Rapier“ verloren");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json.Replace("\"art\": \"Mine\"", "\"art\": \"Schmiede\"")),
                "nur Werkstätten der Stufe 1");
        }),

        ("Erfahrung: Jedes Stück bringt die Gesellenstunden dafür – im Beruf des Betriebs, für das ganze Haus", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Fuchs", startvermoegen: 20_000);
            var g = w.BetriebEroeffnen("Gießerei Fuchs", Betriebsart.Giesserei, k, startkarren: "");
            Pruefe.Gleich(1, k.Meistergrad("Schmied"));
            Pruefe.Gleich(0, k.Meistergrad("Tischler"));
            w.Ausfuehren(k, new ProduktWaehlen(g.Name, "Beschlag"));
            g.Einlagern("Eisen", 20);
            w.Einstellen(g, "Geselle", 2);
            JahrBeenden(sim);
            var stueck = k.Vorjahr!.Hergestellt["Beschlag"];
            Pruefe.Wahr(stueck > 0, "nichts hergestellt");
            Pruefe.Ungefaehr(stueck * 35 / 60.0, k.Erfahrung("Schmied"), 1e-9); // Beschlag: 35 Minuten
            Pruefe.Gleich(0.0, k.Erfahrung("Tischler"));
        }),

        ("Meisterprüfung: erst mit genug Erfahrung – dann Gebühr und AP; im höchsten Grad ist Schluss", () =>
        {
            var sim = NeueSimulation();
            var (w, k, _) = Meister(sim, 1);
            var e = w.Ausfuehren(k, new MeisterpruefungAblegen("Schmied"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Stunden Erfahrung"), e.Meldung);
            Pruefe.Gleich(4, k.Aktionspunkte.Vorrat); // abgelehnt – kostet nichts

            k.ErfahrungSammeln("Schmied", 60);
            Pruefe.Gleich(2, w.ApKosten(k, new MeisterpruefungAblegen("Schmied")));
            var vorher = k.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(k, new MeisterpruefungAblegen("Schmied")).Erfolg, "Grad 2");
            Pruefe.Gleich(2, k.Meistergrad("Schmied"));
            Pruefe.Ungefaehr(vorher - 200, k.Vermoegen, 1e-9);
            Pruefe.Gleich(2, k.Aktionspunkte.Vorrat);

            Pruefe.Wahr(w.Ausfuehren(k, new MeisterpruefungAblegen("Tischler")).Meldung.Contains("beherrscht"), "fremder Beruf");

            k.GradSetzen("Schmied", 6);
            k.ErfahrungSammeln("Schmied", 10_000);
            var e2 = w.Ausfuehren(k, new MeisterpruefungAblegen("Schmied"));
            Pruefe.Wahr(!e2.Erfolg && e2.Meldung.Contains("höchsten Meistergrad"), e2.Meldung);
        }),

        ("Ausbau ohne Ware: Schmugglerloch, Wachhaus und Wechselstube lassen sich ausbauen, ohne nach einem Produkt zu suchen", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            foreach (var (art, beruf, neu) in new[] { (Betriebsart.Schmugglerloch, "Dieb", Betriebsart.Unterschlupf),
                         (Betriebsart.Wachhaus, "Gardist", Betriebsart.Stadtwache), (Betriebsart.Wechselstube, "Geldleiher", Betriebsart.Geldleihe) })
            {
                var k = w.KontoEroeffnen($"Familie {beruf}", startvermoegen: 30_000);
                var b = w.BetriebEroeffnen($"{beruf} Probe", art, k, startkarren: "");
                k.GradSetzen(beruf, 3);
                var e = w.Ausfuehren(k, new BetriebAusbauen(b.Name));
                Pruefe.Wahr(e.Erfolg, e.Meldung);
                Pruefe.Gleich(neu, b.Art);
                Pruefe.Gleich(2, b.Stufe);
                Pruefe.Gleich("", b.Produkt);
            }
        }),

        ("Ausbau: ab Grad 3 zur Schmiede – 3 Plätze, neue Waren, ein halber Tag Umbau; Stufe 3 erst ab Grad 5", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g) = Meister(sim, 2);
            var e = w.Ausfuehren(k, new BetriebAusbauen(g.Name));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("3. Meistergrad"), e.Meldung);

            k.GradSetzen("Schmied", 3);
            w.Einstellen(g, "Geselle", 2);
            var vorher = k.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(k, new BetriebAusbauen(g.Name)).Erfolg, "Ausbau");
            Pruefe.Gleich(Betriebsart.Schmiede, g.Art);
            Pruefe.Gleich(2, g.Stufe);
            Pruefe.Gleich(3, g.Plaetze);
            Pruefe.Ungefaehr(vorher - 2500, k.Vermoegen, 1e-9);
            Pruefe.Gleich("Silberring", g.Produkt); // das Produkt bleibt
            Pruefe.Wahr(w.Ausfuehren(k, new ProduktWaehlen(g.Name, "Silberkette")).Erfolg, "neue Ware");
            w.Einstellen(g, "Geselle", 3);
            Pruefe.Gleich(360, g.UmbauMinuten);

            var e2 = w.Ausfuehren(k, new BetriebAusbauen(g.Name));
            Pruefe.Wahr(!e2.Erfolg && e2.Meldung.Contains("umgebaut"), e2.Meldung);

            // Die Partie beginnt um 5 Uhr, gearbeitet wird ab 8: 360 Arbeitsminuten Umbau bis 14 Uhr, danach wird gearbeitet
            g.Einlagern("Silber", 20); g.Einlagern("Eisen", 20); g.Einlagern("Beschlag", 10);
            for (var i = 0; i < 180 + 300; i++) sim.Tick();
            Pruefe.Wahr(g.Stillstand?.Contains("Umbau") == true, g.Stillstand ?? "kein Stillstand");
            for (var i = 0; i < 120; i++) sim.Tick();
            Pruefe.Gleich(0, g.UmbauMinuten);
            Pruefe.Wahr(g.ImLager("Silberkette") > 0, "nach dem Umbau keine Silberkette");
            Pruefe.Gleich(0, g.StillstandMinuten); // ein Umbau zählt nicht als Stillstand

            var e3 = w.Ausfuehren(k, new BetriebAusbauen(g.Name));
            Pruefe.Wahr(!e3.Erfolg && e3.Meldung.Contains("5. Meistergrad"), e3.Meldung);
            k.GradSetzen("Schmied", 5);
            Pruefe.Wahr(w.Ausfuehren(k, new BetriebAusbauen(g.Name)).Erfolg, "Goldschmiede");
            Pruefe.Gleich(4, g.Plaetze);
            Pruefe.Wahr(!w.Ausfuehren(k, new BetriebAusbauen(g.Name)).Erfolg, "über Stufe 3");
            Pruefe.Wahr(Kalkulation.AusbauMehrgewinn(w, g) == 0, "keine Stufe 4");
        }),

        ("Grad 6: ein Aktionspunkt mehr je Runde", () =>
        {
            var sim = NeueSimulation();
            var (w, k, _) = Meister(sim, 5);
            JahrBeenden(sim);
            Pruefe.Gleich(8, k.Aktionspunkte.Vorrat);
            k.GradSetzen("Schmied", 6);
            Pruefe.Gleich(1, w.ApBonus(k));
            JahrBeenden(sim);
            Pruefe.Gleich(12, k.Aktionspunkte.Vorrat); // 8 + 4 + 1, gedeckelt bei 12
            k.Aktionspunkte.Setzen(0);
            JahrBeenden(sim);
            Pruefe.Gleich(5, k.Aktionspunkte.Vorrat);
        }),

        ("KI: legt Prüfungen ab und baut ihre Werkstatt aus, sobald der Grad reicht", () =>
        {
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 7 },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var eisenbart = w.Rivalen[0].Konto;
            eisenbart.ErfahrungSammeln("Schmied", 300); // genug für Grad 2 und 3
            for (var j = 0; j < 3; j++) JahrBeenden(sim);
            Pruefe.Wahr(eisenbart.Meistergrad("Schmied") >= 3, $"Grad {eisenbart.Meistergrad("Schmied")}");
            Pruefe.Wahr(w.Betriebe.Any(b => b.Konto == eisenbart && b.Stufe >= 2), "nicht ausgebaut");
            Pruefe.Wahr(eisenbart.Aktionspunkte.Vorrat is >= 0 and <= 12, $"AP {eisenbart.Aktionspunkte.Vorrat}");
        }),

        ("Speichern: Grade, Erfahrung, Ausbau und laufender Umbau überstehen Speichern und Laden; alte Stände beginnen mit Grad 1", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Startvermoegen: 20_000));
            var w = sim.Wirtschaft!;
            spieler.GradSetzen("Schmied", 3);
            spieler.ErfahrungSammeln("Schmied", 123.5);
            var g = w.Betriebe.First(b => b.Konto == spieler && b.IstWerkstatt);
            Pruefe.Wahr(w.Ausfuehren(spieler, new BetriebAusbauen(g.Name)).Erfolg, "Ausbau");
            var json = Speicherung.Speichern(sim, spieler);
            var (kopie, kopieSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(3, kopieSpieler!.Meistergrad("Schmied"));
            Pruefe.Gleich(123.5, kopieSpieler.Erfahrung("Schmied"));
            var kg = kopie.Wirtschaft!.Betriebe.First(b => b.Name == g.Name);
            Pruefe.Gleich(Betriebsart.Schmiede, kg.Art);
            Pruefe.Gleich(360, kg.UmbauMinuten);
            static string OhneZeit(string j) => System.Text.RegularExpressions.Regex.Replace(j, "\"Gespeichert\": \"[^\"]*\"", "");
            Pruefe.Wahr(OhneZeit(json) == OhneZeit(Speicherung.Speichern(kopie, kopieSpieler)), "Speichern – Laden – Speichern ist nicht gleich");

            var alt = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            foreach (var konto in alt["Wirtschaft"]!["Konten"]!.AsArray())
            {
                konto!.AsObject().Remove("Meistergrade");
                konto.AsObject().Remove("Erfahrung");
            }
            var (_, altSpieler) = Speicherung.Laden(alt.ToJsonString());
            Pruefe.Gleich(1, altSpieler!.Meistergrad("Schmied"));
            Pruefe.Gleich(0.0, altSpieler.Erfahrung("Schmied"));
        }),
    };
}
