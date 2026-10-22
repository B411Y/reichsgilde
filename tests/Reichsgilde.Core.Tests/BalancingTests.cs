using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Balancing 1 (PR #177): Schwierigkeit beim Start (Normal: alle Häuser wie der Spieler, Schwer: der bisherige Vorsprung der KI)
/// und was ein Stück Einrichtung im Jahr mehr einbringt (Abfrage für die KI).
/// </summary>
static class BalancingTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Partieeinstellungen Endlos(Schwierigkeit s = Schwierigkeit.Normal) =>
        Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = s };

    static Simulation NeueSimulation() => new(Endlos(), wirtschaftsdaten: Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Eine Gießerei mit zwei Gesellen an Rapieren, ohne Karren.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb B) Giesserei(Simulation sim)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Familie Probe", startvermoegen: 10_000);
        var b = w.BetriebEroeffnen("Gießerei Probe", Betriebsart.Giesserei, k, startkarren: "");
        b.Produkt = "Rapier";
        w.Einstellen(b, "Geselle", 2);
        return (w, k, b);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Schwierigkeit: Normal (Vorgabe) – jedes KI-Haus beginnt wie der Spieler mit einer Werkstatt und zwei Gesellen; der Umlandbetrieb ist frei zur Pacht", () =>
        {
            Pruefe.Gleich(Schwierigkeit.Normal, Partieeinstellungen.Standard.Schwierigkeit);
            foreach (var karte in new[] { null, Stadtkarte.AusDatei(Datenpfad.Karte("greybox")) })
            {
                var (sim, spieler) = Partieaufbau.Erstellen(Endlos(), Daten.Value, new NeuePartie(), karte: karte);
                var w = sim.Wirtschaft!;
                Pruefe.Gleich(3, w.Rivalen.Count);
                foreach (var ki in w.Rivalen.Select(r => r.Konto))
                {
                    var betriebe = w.Betriebe.Where(b => b.Konto == ki).ToList();
                    Pruefe.Gleich(1, betriebe.Count, ki.Inhaber);
                    Pruefe.Wahr(betriebe[0].IstWerkstatt && betriebe[0].Belegung.GetValueOrDefault("Geselle") == 2, $"{ki.Inhaber}: {betriebe[0].Name}");
                    Pruefe.Ungefaehr(spieler.Vermoegen, ki.Vermoegen, 1e-9); // gleiches Startgeld
                }
                var frei = w.FreieLiegenschaften.Select(l => l.Name).ToHashSet();
                foreach (var name in new[] { "Grube Eisenbart", "Holzschlag Eichwald", "Steinbruch Kieselbach" })
                {
                    Pruefe.Wahr(frei.Contains(name), $"{name} frei ({(karte is null ? "ohne Karte" : "Greybox")})");
                    Pruefe.Wahr(w.FreieLiegenschaften.First(l => l.Name == name).IstPacht, $"{name} ist eine Pacht");
                }
                if (karte is not null)
                    Pruefe.Wahr(w.Bauen.PlatzVon("Grube Eisenbart")?.Gebaeude is not null, "die freie Grube steht auf ihrem Platz der Karte");
            }
        }),

        ("Schwierigkeit: Schwer – die KI-Häuser beginnen mit Werkstatt und Umlandbetrieb (der Start vor PR #177)", () =>
        {
            var (sim, _) = Partieaufbau.Erstellen(Endlos(Schwierigkeit.Schwer), Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            foreach (var ki in w.Rivalen.Select(r => r.Konto))
            {
                var betriebe = w.Betriebe.Where(b => b.Konto == ki).ToList();
                Pruefe.Gleich(2, betriebe.Count, ki.Inhaber);
                Pruefe.Gleich(5, betriebe.Sum(b => b.Belegt), ki.Inhaber);
            }
            Pruefe.Wahr(w.FreieLiegenschaften.All(l => l.Name != "Grube Eisenbart"), "die Grube gehört Eisenbart");
        }),

        ("Schwierigkeit: übersteht Speichern und Laden; alte Spielstände ohne Feld laden als Normal; die freie Grube bleibt frei", () =>
        {
            foreach (var s in new[] { Schwierigkeit.Normal, Schwierigkeit.Schwer })
            {
                var (sim, spieler) = Partieaufbau.Erstellen(Endlos(s), Daten.Value, new NeuePartie());
                var json = Speicherung.Speichern(sim, spieler);
                var (kopie, _) = Speicherung.Laden(json);
                Pruefe.Gleich(s, kopie.Einstellungen.Schwierigkeit);
                Pruefe.Gleich(s == Schwierigkeit.Normal, kopie.Wirtschaft!.FreieLiegenschaften.Any(l => l.Name == "Grube Eisenbart"), s.ToString());
            }
            var (sim2, spieler2) = Partieaufbau.Erstellen(Endlos(Schwierigkeit.Schwer), Daten.Value, new NeuePartie());
            var alt = JsonNode.Parse(Speicherung.Speichern(sim2, spieler2))!;
            Pruefe.Wahr(alt["Einstellungen"]!.AsObject().Remove("Schwierigkeit"), "Feld im Spielstand");
            Pruefe.Gleich(Schwierigkeit.Normal, Speicherung.Laden(alt.ToJsonString()).Simulation.Einstellungen.Schwierigkeit);
        }),

        ("Einrichtung: Mehrgewinn je Jahr – Werkzeug = Leistung × Arbeitsminuten × Gewinn je Minute × (1 ÷ 0,9 − 1); gekauft oder ohne Gulden-Wirkung 0", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Giesserei(sim);
            var ws = w.Werkstaetten;
            var gesellen = 2 * w.Daten.Arbeitskraefte["Geselle"].TempoAm(b.Ort) * w.VolksFaktor(k, "Schmied");
            var erwartet = gesellen * b.Arbeitsminuten * Kalkulation.GewinnJeArbeitsminute(w, "Rapier") * (1 / 0.9 - 1);
            Pruefe.Wahr(erwartet > 0, $"Rapier mit Gewinn ({erwartet:0})");
            Pruefe.Ungefaehr(erwartet, ws.ErwarteterMehrgewinnJeJahr(b, "Werkzeug1"), 1e-6);
            Pruefe.Ungefaehr(erwartet, ws.ErwarteterMehrgewinnJeJahr(b, "Blasebalg"), 1e-6); // der Blasebalg gilt für Rapiere
            Pruefe.Ungefaehr(300 / erwartet, ws.Amortisationsjahre(b, "Werkzeug1")!.Value, 1e-9);
            Pruefe.Gleich(0.0, ws.ErwarteterMehrgewinnJeJahr(b, "Regale"));
            Pruefe.Gleich<double?>(null, ws.Amortisationsjahre(b, "Regale"));
            Pruefe.Gleich(0.0, ws.ErwarteterMehrgewinnJeJahr(b, "Gibtsnicht"));
            Pruefe.Gleich(0.0, ws.ErwarteterMehrgewinnJeJahr(b, "Eisentuer"), "nie bestohlen");
            Pruefe.Gleich(0.0, ws.ErwarteterMehrgewinnJeJahr(b, "Loeschfass"), "nie gebrannt");

            // Mit dem Werkzeug bringt der Blasebalg weniger (er macht die schon schnellere Arbeit nochmals 10 % schneller).
            Pruefe.Wahr(w.Ausfuehren(k, new EinrichtungKaufen(b.Name, "Werkzeug1")).Erfolg, "Werkzeug kaufen");
            Pruefe.Gleich(0.0, ws.ErwarteterMehrgewinnJeJahr(b, "Werkzeug1"), "schon gekauft");
            Pruefe.Ungefaehr(erwartet / 0.9, ws.ErwarteterMehrgewinnJeJahr(b, "Blasebalg"), 1e-6);

            // Silberringe sind keine Ware des Blasebalgs.
            b.Produkt = "Silberring";
            Pruefe.Gleich(0.0, ws.ErwarteterMehrgewinnJeJahr(b, "Blasebalg"));

            // Schürzen: Wert der Arbeit × Unfallgefahr × verlorener Teil des Tages × (1 − 0,5).
            var stunden = b.Arbeitsminuten / 60.0;
            var arbeit = gesellen * b.Arbeitsminuten * Kalkulation.GewinnJeArbeitsminute(w, "Silberring") / 0.9;
            Pruefe.Ungefaehr(arbeit * ws.UnfallGefahr(b) * (stunden - 1) / 2 / stunden * 0.5, ws.ErwarteterMehrgewinnJeJahr(b, "Schuerzen"), 1e-6);
        }),

        ("Einrichtung: Eisentür und Löschfass rechnen mit dem Raub bzw. Brandschaden des Vorjahres; beides übersteht Speichern und Laden", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Giesserei(sim);
            b.RaubBuchen(240);
            b.BrandBuchen(90);
            Pruefe.Ungefaehr(240, k.Laufend.Raubwert, 1e-9); // der Raub zählt auch beim Haus
            JahrBeenden(sim);
            Pruefe.Ungefaehr(240, b.Vorjahr!.Raub, 1e-9);
            Pruefe.Ungefaehr(240 * 0.5, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Eisentuer"), 1e-9);
            Pruefe.Ungefaehr(90 * 0.5, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Loeschfass"), 1e-9);
            var (kopie, _) = Speicherung.Laden(Speicherung.Speichern(sim, k));
            var kb = kopie.Wirtschaft!.Betriebe.Single(x => x.Name == b.Name);
            Pruefe.Ungefaehr(240, kb.Vorjahr!.Raub, 1e-9);
            Pruefe.Ungefaehr(90, kb.Vorjahr!.Brandschaden, 1e-9);
        }),

        ("Einrichtung: Geldleiher – Rechenbretter bringen Zins nur, so weit die Kasse den Rahmen füllt; Pfandkammer mindert den Ausfall der Kasse", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Fugger", startvermoegen: 50_000);
            var b = w.BetriebEroeffnen("Wechselstube Fugger", Betriebsart.Wechselstube, k, startkarren: "");
            w.Einstellen(b, "Geselle", b.Plaetze);
            var bank = w.Bank;
            var d = bank.Daten;
            var nachSteuer = 1 - w.Daten.Allgemein.Umsatzsteuer;
            var jahresanteil = b.Arbeitsminuten / (double)w.Daten.Allgemein.Arbeitsminuten;
            Pruefe.Gleich(0.0, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Werkzeug1"), "leere Kasse: mehr Rahmen bringt nichts");
            Pruefe.Gleich(0.0, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Pfandkammer"), "leere Kasse: kein Ausfall");

            var rahmen = bank.Rahmen(b) * w.VolksFaktor(k, "Geldleiher");
            Pruefe.Wahr(w.Ausfuehren(k, new KasseEinlegen(b.Name, 2 * rahmen)).Erfolg, "Kasse füllen");
            var kasse = bank.Kasse(b);
            Pruefe.Ungefaehr(rahmen * 0.1 * d.Stadtzins * jahresanteil * nachSteuer, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Werkzeug1"), 1e-6);
            Pruefe.Ungefaehr(kasse * d.Ausfall.Chance * d.Ausfall.Anteil * 0.25, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Pfandkammer"), 1e-6);
            Pruefe.Gleich(0.0, w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Kassenschrank"), "Kreditrahmen ohne Gulden-Wirkung");
            Pruefe.Wahr(w.Werkstaetten.ErwarteterMehrgewinnJeJahr(b, "Goldwaage") > 0, "Goldwaage: mehr Wechselgebühr");
        }),
    };
}
