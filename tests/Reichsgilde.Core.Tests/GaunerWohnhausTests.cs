using System.Globalization;
using System.Text.RegularExpressions;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für Gaunerstücke gegen das Wohnhaus: Einbruch (Bargeld), Brand (Reparatur, Ansehen fehlt), Hauswächter.</summary>
static class GaunerWohnhausTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Einbruch und Brand gelingen immer, Zeugen nach Wunsch.</summary>
    static Wirtschaftsdaten Sicher(double zeugen = 0)
    {
        var z = zeugen.ToString(CultureInfo.InvariantCulture);
        return Wirtschaftsdaten.AusJson(Json
            .Replace("\"kosten\": 200, \"chance\": 0.6, \"anteil\"", "\"kosten\": 200, \"chance\": 1.0, \"anteil\"")
            .Replace("\"kosten\": 300, \"chance\": 0.5, \"reparaturAnteil\"", "\"kosten\": 300, \"chance\": 1.0, \"reparaturAnteil\"")
            .Replace("\"zeugenChance\": 0.25", $"\"zeugenChance\": {z}")
            .Replace("\"zeugenChanceWaechter\": 0.6", $"\"zeugenChanceWaechter\": {z}"));
    }

    static readonly Lazy<Wirtschaftsdaten> OhneZeugen = new(() => Sicher(0));

    static Simulation NeueSimulation(Wirtschaftsdaten daten) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten);

    static void BisZurNacht(Simulation sim)
    {
        while (sim.Zeit.MinuteImTag != Unterwelt.Nachtstunde) sim.Tick();
        sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Wohnhaus-Gauner: Einbruch holt 5 % vom Bargeld, höchstens 300 je Hausstufe – Bargeld ohne Hehler", () =>
        {
            var sim = NeueSimulation(OhneZeugen.Value);
            var w = sim.Wirtschaft!;
            var taeter = w.KontoEroeffnen("Haus Täter", startvermoegen: 5_000);
            var opfer = w.KontoEroeffnen("Haus Opfer", startvermoegen: 20_000);
            Pruefe.Gleich(300.0, w.Gauner.Hausbeute(opfer));   // 5 % = 1.000, gedeckelt: Fachwerkhaus 1 × 300
            Wohnhaushilfe.MitWohnhaus(w, opfer);                // Bürgerhaus
            Pruefe.Gleich(600.0, w.Gauner.Hausbeute(opfer));   // 2 × 300
            var klein = w.KontoEroeffnen("Haus Klein", startvermoegen: 2_000);
            Pruefe.Gleich(100.0, w.Gauner.Hausbeute(klein));   // 5 % von 2.000 liegt unter der Grenze

            Pruefe.Wahr(w.Ausfuehren(taeter, new Beauftragen(opfer.Inhaber, Gaunerstueck.Hauseinbruch)).Erfolg, "Auftrag");
            Pruefe.Gleich(4_800.0, taeter.Vermoegen);
            BisZurNacht(sim);
            Pruefe.Ungefaehr(20_000 - 600, opfer.Vermoegen, 1e-6);
            Pruefe.Ungefaehr(4_800 + 600, taeter.Vermoegen, 1e-6);
            Pruefe.Ungefaehr(600, opfer.Laufend.Schaden, 1e-6);
            Pruefe.Wahr(w.Gauner.NachrichtenFuer(klein, false).Any(t => t.Contains("Einbruch ins Bürgerhaus")), "öffentliche Meldung");
            Pruefe.Gleich(w.Jahr, w.Gauner.LetzterAngriffWohnhaus(opfer));
            Pruefe.Wahr(w.Justiz.SpurenVon(taeter).Any(s => s.Betroffener == Unterwelt.WohnhausVon(opfer)), "Spur fürs Gericht");
        }),

        ("Wohnhaus-Gauner: Brand kostet 20 % des Ausbaus, das Ansehen fehlt einen Tag lang – die Stufe bleibt", () =>
        {
            var sim = NeueSimulation(OhneZeugen.Value);
            var w = sim.Wirtschaft!;
            var taeter = w.KontoEroeffnen("Haus Täter", startvermoegen: 5_000);
            var opfer = Wohnhaushilfe.MitWohnhaus(w, w.KontoEroeffnen("Haus Opfer", startvermoegen: 20_000));
            Pruefe.Gleich(2.0, w.Wohnen.AnsehenBonus(opfer));
            Pruefe.Gleich(300.0, w.Gauner.Brandschaden(opfer));                   // 20 % von 1.500
            Pruefe.Gleich(100.0, w.Gauner.Brandschaden(taeter));                  // Fachwerkhaus: Mindestreparatur

            w.Ausfuehren(taeter, new Beauftragen(opfer.Inhaber, Gaunerstueck.Brandstiftung));
            BisZurNacht(sim);
            Pruefe.Wahr(w.Wohnen.IstBeschaedigt(opfer), "beschädigt");
            Pruefe.Gleich(0.0, w.Wohnen.AnsehenBonus(opfer));
            Pruefe.Gleich("Bürgerhaus", w.Wohnen.Stufe(opfer)!.Name);
            Pruefe.Ungefaehr(300, opfer.Laufend.Schaden, 1e-6);
            Pruefe.Wahr(w.Wohnen.AusbauVerboten(opfer)!.Contains("instand"), "kein Ausbau während der Instandsetzung");
            Pruefe.Wahr(w.Ausfuehren(opfer, new StandErwerben()).Erfolg, "Aufstieg bleibt möglich – die Stufe zählt");
            var e = w.Ausfuehren(taeter, new Beauftragen(opfer.Inhaber, Gaunerstueck.Brandstiftung));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("instand"), e.Meldung);

            var bis = opfer.WohnhausInstandBis!.Value;
            while (sim.Zeit.VergangeneMinuten <= bis) sim.Tick();
            Pruefe.Wahr(!w.Wohnen.IstBeschaedigt(opfer), "instand gesetzt");
            Pruefe.Gleich(2.0, w.Wohnen.AnsehenBonus(opfer));
        }),

        ("Wohnhaus-Gauner: Hauswächter halbiert Einbruch und Brand, macht Zeugen wahrscheinlicher und kostet 150 im Jahr", () =>
        {
            var sim = NeueSimulation(Daten.Value);
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Vorsicht", startvermoegen: 20_000);
            Pruefe.Ungefaehr(0.6, w.Gauner.ErfolgschanceWohnhaus(Gaunerstueck.Hauseinbruch, k), 1e-9);
            Pruefe.Ungefaehr(0.5, w.Gauner.ErfolgschanceWohnhaus(Gaunerstueck.Brandstiftung, k), 1e-9);
            Pruefe.Ungefaehr(0.25, w.Gauner.ZeugenchanceWohnhaus(k, true), 1e-9);
            Pruefe.Wahr(w.Ausfuehren(k, new HauswaechterSetzen(true)).Erfolg, "einstellen");
            Pruefe.Ungefaehr(0.3, w.Gauner.ErfolgschanceWohnhaus(Gaunerstueck.Hauseinbruch, k), 1e-9);
            Pruefe.Ungefaehr(0.25, w.Gauner.ErfolgschanceWohnhaus(Gaunerstueck.Brandstiftung, k), 1e-9);
            Pruefe.Ungefaehr(0.6, w.Gauner.ZeugenchanceWohnhaus(k, true), 1e-9);
            while (sim.Zeit.Jahr == Spielzeit.StartJahr) sim.Tick();
            Pruefe.Ungefaehr(150, k.Vorjahr!.Personal, 1e-9);
            Pruefe.Wahr(w.Ausfuehren(k, new HauswaechterSetzen(false)).Erfolg, "entlassen");
            Pruefe.Wahr(!k.Hauswaechter, "entlassen");
        }),

        ("Wohnhaus-Gauner: Brand und Hauswächter überstehen Speichern und Laden; alte Spielstände ohne die Felder laden", () =>
        {
            var sim = NeueSimulation(OhneZeugen.Value);
            var w = sim.Wirtschaft!;
            var taeter = w.KontoEroeffnen("Haus Täter", startvermoegen: 5_000);
            var opfer = Wohnhaushilfe.MitWohnhaus(w, w.KontoEroeffnen("Haus Opfer", startvermoegen: 20_000));
            w.Ausfuehren(taeter, new HauswaechterSetzen(true));
            w.Ausfuehren(opfer, new Beauftragen(taeter.Inhaber, Gaunerstueck.Brandstiftung)); // mit Hauswächter 50 % – egal wie, verglichen wird gespeichert gegen geladen
            w.Ausfuehren(taeter, new Beauftragen(opfer.Inhaber, Gaunerstueck.Brandstiftung));
            BisZurNacht(sim);
            var json = Speicherung.Speichern(sim);
            var g = Speicherung.Laden(json).Simulation.Wirtschaft!;
            var og = g.Konten.Single(x => x.Inhaber == "Haus Opfer");
            var tg = g.Konten.Single(x => x.Inhaber == "Haus Täter");
            Pruefe.Gleich(opfer.WohnhausInstandBis, og.WohnhausInstandBis);
            Pruefe.Wahr(og.WohnhausInstandBis is not null, "Brand gespeichert");
            Pruefe.Wahr(tg.Hauswaechter, "Hauswächter gespeichert");
            Pruefe.Gleich(taeter.WohnhausInstandBis, tg.WohnhausInstandBis);

            var alt = Regex.Replace(json, @"\s*""(WohnhausInstandBis|Hauswaechter)"": [^,\r\n]+,", "");
            var ka = Speicherung.Laden(alt).Simulation.Wirtschaft!;
            Pruefe.Wahr(ka.Konten.All(x => !x.Hauswaechter && x.WohnhausInstandBis is null), "alte Spielstände: kein Wächter, kein Brand");
        }),

        ("Wohnhaus-Gauner: Ohne Abschnitt gauner.wohnhaus nimmt keine Bande Aufträge gegen Wohnhäuser an", () =>
        {
            var ohne = Wirtschaftsdaten.AusJson(Regex.Replace(Json, @",\s*""wohnhaus"": \{\s*""einbruch"".*?\}\s*\}", "", RegexOptions.Singleline));
            Pruefe.Wahr(ohne.Gauner!.Wohnhaus is null, "Abschnitt nicht entfernt");
            var w = NeueSimulation(ohne).Wirtschaft!;
            var a = w.KontoEroeffnen("Haus A", startvermoegen: 5_000);
            var b = w.KontoEroeffnen("Haus B", startvermoegen: 5_000);
            Pruefe.Wahr(!w.Gauner.GegenWohnhaus, "keine Wohnhaus-Gauner");
            Pruefe.Wahr(!w.Ausfuehren(a, new Beauftragen(b.Inhaber, Gaunerstueck.Hauseinbruch)).Erfolg, "Einbruch abgelehnt");
            Pruefe.Wahr(!w.Ausfuehren(a, new HauswaechterSetzen(true)).Erfolg, "Hauswächter abgelehnt");
            Pruefe.Gleich(5_000.0, a.Vermoegen);
        }),

        ("Wohnhaus-Gauner: Daten werden geprüft (Anteile zwischen 0 und 1)", () =>
        {
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace("\"anteil\": 0.05", "\"anteil\": 1.5")), "einbruch.anteil");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace("\"reparaturAnteil\": 0.2", "\"reparaturAnteil\": -0.1")), "brand.reparaturAnteil");
        }),
    };
}
