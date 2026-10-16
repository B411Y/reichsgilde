using System.Text.RegularExpressions;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für das Wohnhaus: Ausbau mit Bauzeit, Pflicht für den Stand, Ansehen, Unterhalt, Speichern, alte Daten.</summary>
static class WohnhausTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten ?? Daten.Value);

    static void Ticken(Simulation sim, int minuten)
    {
        for (var i = 0; i < minuten; i++) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Wohnhaus: Jedes Haus beginnt im Fachwerkhaus; ohne Bürgerhaus kein Aufstieg zum Bürger", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Krug", startvermoegen: 12_000);
            Pruefe.Gleich("Fachwerkhaus", w.Wohnen.Stufe(k)!.Name);
            Pruefe.Gleich(0.0, w.Wohnen.AnsehenBonus(k));
            var e = w.Ausfuehren(k, new StandErwerben());
            Pruefe.Wahr(!e.Erfolg, "Aufstieg ohne Bürgerhaus");
            Pruefe.Wahr(e.Meldung.Contains("Bürgerhaus"), e.Meldung);
            Pruefe.Gleich(0, k.Stand);
        }),

        ("Wohnhaus: Ausbau kostet 1.500 Gulden und 4 Stunden – danach Bürgerhaus, +2 Ansehen, Aufstieg möglich", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Krug", startvermoegen: 12_000);
            Pruefe.Wahr(w.Ausfuehren(k, new WohnhausAusbauen()).Erfolg, "Ausbau");
            Pruefe.Gleich(10_500.0, k.Vermoegen);
            Pruefe.Gleich(1500.0, k.Laufend.Investitionen);
            Pruefe.Wahr(!w.Ausfuehren(k, new WohnhausAusbauen()).Erfolg, "zweiter Ausbau während des Baus");
            Ticken(sim, 239);
            Pruefe.Gleich("Fachwerkhaus", w.Wohnen.Stufe(k)!.Name);
            Pruefe.Wahr(!w.Ausfuehren(k, new StandErwerben()).Erfolg, "Aufstieg vor dem Einzug");
            var ansehenVorher = w.Rat.Ansehen(k); // nach dem Bezahlen (das Vermögen zählt mit), vor dem Einzug
            Ticken(sim, 2);
            Pruefe.Gleich("Bürgerhaus", w.Wohnen.Stufe(k)!.Name);
            Pruefe.Ungefaehr(ansehenVorher + 2, w.Rat.Ansehen(k), 1e-9);
            Pruefe.Wahr(w.Ausfuehren(k, new StandErwerben()).Erfolg, "Aufstieg mit Bürgerhaus");
            Pruefe.Gleich("Bürger", w.StandVon(k).Name);
        }),

        ("Wohnhaus: höchstens eine Stufe über dem Stand, und nur mit genug Bargeld", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var arm = w.KontoEroeffnen("Haus Arm", startvermoegen: 1000);
            var e = w.Ausfuehren(arm, new WohnhausAusbauen());
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("fehlen"), e.Meldung);
            var reich = w.KontoEroeffnen("Haus Reich", startvermoegen: 100_000);
            Pruefe.Wahr(w.Ausfuehren(reich, new WohnhausAusbauen()).Erfolg, "Bürgerhaus");
            Ticken(sim, 241);
            var steinhaus = w.Ausfuehren(reich, new WohnhausAusbauen());
            Pruefe.Wahr(!steinhaus.Erfolg && steinhaus.Meldung.Contains("Bürger"), steinhaus.Meldung); // ein Steinhaus erst als Bürger
            Pruefe.Wahr(w.Ausfuehren(reich, new StandErwerben()).Erfolg, "Bürger");
            Pruefe.Wahr(w.Ausfuehren(reich, new WohnhausAusbauen()).Erfolg, "Steinhaus als Bürger");
        }),

        ("Wohnhaus: Unterhalt am Jahresende mit den Haushaltskosten (Bürgerhaus 30 Gulden)", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Krug", startvermoegen: 12_000);
            w.Ausfuehren(k, new WohnhausAusbauen());
            while (sim.Zeit.Jahr == Spielzeit.StartJahr) sim.Tick();
            Pruefe.Ungefaehr(30, k.Vorjahr!.Familie, 1e-9); // ohne Familie: nur der Unterhalt des Hauses
            Pruefe.Gleich(30.0, w.Wohnen.Unterhalt(k));
        }),

        ("Wohnhaus: Ausbau und Stufe überstehen Speichern und Laden; alte Spielstände bekommen das Haus ihres Standes", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Krug", startvermoegen: 12_000);
            w.Ausfuehren(k, new WohnhausAusbauen());
            Ticken(sim, 60);
            var json = Speicherung.Speichern(sim);
            var geladen = Speicherung.Laden(json).Simulation;
            var kg = geladen.Wirtschaft!.Konten.Single(x => x.Inhaber == "Haus Krug");
            Pruefe.Gleich(k.WohnhausFertigAb, kg.WohnhausFertigAb);
            Ticken(geladen, 200);
            Pruefe.Gleich("Bürgerhaus", geladen.Wirtschaft.Wohnen.Stufe(kg)!.Name);

            // Ein alter Spielstand: ein Patrizier ohne Wohnhaus-Felder wohnt nach dem Laden im Steinhaus.
            var alt = Regex.Replace(json, @"\s*""WohnhausStufe"": \d+,", "");
            alt = Regex.Replace(alt, @"""Stand"": 0,", @"""Stand"": 2,");
            var ka = Speicherung.Laden(alt).Simulation.Wirtschaft!;
            Pruefe.Gleich("Steinhaus", ka.Wohnen.Stufe(ka.Konten.Single(x => x.Inhaber == "Haus Krug"))!.Name);
        }),

        ("Wohnhaus: Wer bankrott ist, hinterlässt Leerstand; ohne Abschnitt „wohnen“ gibt es keine Wohnhäuser", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Pleite");
            Pruefe.Wahr(!w.Wohnen.Leerstand.Any(), "noch kein Leerstand");
            k.IstBankrott = true;
            Pruefe.Wahr(w.Wohnen.Leerstand.Contains(k), "Leerstand nach Bankrott");

            var ohne = Wirtschaftsdaten.AusJson(Regex.Replace(Json, @"""wohnen"": \{.*?\]\s*\},", "", RegexOptions.Singleline));
            Pruefe.Wahr(ohne.Wohnen is null, "Abschnitt nicht entfernt");
            var w2 = NeueSimulation(ohne).Wirtschaft!;
            var k2 = w2.KontoEroeffnen("Haus Alt", startvermoegen: 12_000);
            Pruefe.Wahr(w2.Ausfuehren(k2, new StandErwerben()).Erfolg, "ohne Wohnhäuser wie früher");
            Pruefe.Wahr(!w2.Ausfuehren(k2, new WohnhausAusbauen()).Erfolg, "kein Ausbau ohne Wohnhäuser");
        }),

        ("Wohnhaus: Der Hauswert zählt ganz zum Reinvermögen – wer an der Schwelle baut, kann trotzdem aufsteigen", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Knapp", startvermoegen: 11_000); // Bürger ab 10.000
            Pruefe.Gleich(0.0, w.Wohnen.Hauswert(k));
            Pruefe.Wahr(w.Ausfuehren(k, new WohnhausAusbauen()).Erfolg, "Bürgerhaus");
            Pruefe.Gleich(9_500.0, k.Vermoegen);                 // Bargeld sinkt …
            Pruefe.Gleich(1_500.0, k.Hauswert);                  // … das Geld steckt im Haus (schon während des Baus)
            Pruefe.Gleich(11_000.0, k.Reinvermoegen);            // … das Reinvermögen bleibt
            Ticken(sim, 241);
            Pruefe.Wahr(w.Ausfuehren(k, new StandErwerben()).Erfolg, "Aufstieg trotz Bargeld unter der Schwelle");
            Pruefe.Gleich(1_500.0, k.Hauswert);

            // Speichern und Laden: Der Hauswert wird aus der Stufe neu berechnet.
            var geladen = Speicherung.Laden(Speicherung.Speichern(sim)).Simulation.Wirtschaft!;
            var kg = geladen.Konten.Single(x => x.Inhaber == "Haus Knapp");
            Pruefe.Gleich(1_500.0, kg.Hauswert);
            Pruefe.Gleich(k.Reinvermoegen, kg.Reinvermoegen);
        }),

        ("Wohnhaus: Hauswert = alle Ausbauten bis zur Stufe; ein Brand ändert ihn nicht", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Gross");
            k.WohnhausStufe = 3; // Stadtpalais
            Pruefe.Gleich(1_500.0 + 4_000 + 10_000, w.Wohnen.Hauswert(k));
            k.WohnhausFertigAb = 1; // Hof im Bau: schon bezahlt, zählt mit
            Pruefe.Gleich(1_500.0 + 4_000 + 10_000 + 25_000, w.Wohnen.Hauswert(k));
        }),

        ("Wohnhaus: Daten werden geprüft (Startwohnhaus kostenlos, Stände aufsteigend)", () =>
        {
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace(
                "{ \"name\": \"Fachwerkhaus\", \"ausbau\": 0,", "{ \"name\": \"Fachwerkhaus\", \"ausbau\": 100,")), "Startwohnhaus");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace(
                "\"ansehen\": 4, \"abStand\": 2", "\"ansehen\": 4, \"abStand\": 1")), "abStand");
        }),
    };
}
