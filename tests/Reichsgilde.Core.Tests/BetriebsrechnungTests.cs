using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests für die Rechnung je Betrieb (Gewinn im Vorjahr – damit die KI einen Betrieb mit Verlust erkennt)
/// und für den Zeitpunkt des Bankrotts, vor dem ein Verkauf noch hilft.
/// </summary>
static class BetriebsrechnungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);

    static void Ticken(Simulation sim, int minuten)
    {
        for (var i = 0; i < minuten; i++) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Betriebsrechnung: Gießerei und Grube – zusammen genau der Betriebsgewinn des Kontos, Posten für Posten", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 5_000);
            var giesserei = w.BetriebEroeffnen("Gießerei am Weinmarkt", Betriebsart.Giesserei, k);
            var grube = w.BetriebEroeffnen("Grube Fuchs", Betriebsart.Mine, k);
            w.Einstellen(giesserei, "Geselle", 2);
            w.Einstellen(grube, "Ork", 3); // unfrei: kann sich losreißen (Schaden)
            Pruefe.Wahr(giesserei.Laufend.Ganzjaehrig && giesserei.Vorjahr is null, "zum Jahresbeginn eröffnet: ganzes Jahr, noch kein Vorjahr");

            Ticken(sim, Spielzeit.MinutenProTag);
            var b = new[] { giesserei.Vorjahr!, grube.Vorjahr! };
            var kv = k.Vorjahr!;
            Pruefe.Gleich(Spielzeit.StartJahr, b[0].Jahr);
            Pruefe.Wahr(b[0].Umsatz > 0 && b[1].Umsatz > 0, "beide verkaufen");
            Pruefe.Wahr(b[0].Zutaten > 0 && b[0].Personal > 0 && b[0].Fixkosten > 0, "die Gießerei kauft Erz, zahlt Löhne und Unterhalt");
            Pruefe.Ungefaehr(kv.Umsatz, b.Sum(x => x.Umsatz), 1e-9);
            Pruefe.Ungefaehr(kv.Zutaten, b.Sum(x => x.Zutaten), 1e-9);
            Pruefe.Ungefaehr(kv.Personal, b.Sum(x => x.Personal), 1e-9);
            Pruefe.Ungefaehr(kv.Fixkosten, b.Sum(x => x.Fixkosten), 1e-9);
            Pruefe.Ungefaehr(kv.Steuer, b.Sum(x => x.Steuer), 1e-9);
            Pruefe.Ungefaehr(kv.Schaden, b.Sum(x => x.Schaden), 1e-9);
            Pruefe.Ungefaehr(kv.Transport, b.Sum(x => x.Transport), 1e-9);
            Pruefe.Ungefaehr(kv.Gewinn, b.Sum(x => x.Gewinn), 1e-9);
            Pruefe.Wahr(giesserei.Laufend is { Ganzjaehrig: true, Umsatz: 0 } && giesserei.Laufend.Jahr == Spielzeit.StartJahr + 1, "neues Jahr, neue Rechnung");
        }),

        ("Betriebsrechnung: Verlust ohne Absatz; ein ganzes Jahr nur ab Jahresbeginn; Speichern und Laden; alte Spielstände", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 20_000);
            w.BetriebEroeffnen("Gießerei am Weinmarkt", Betriebsart.Giesserei, k);
            Ticken(sim, 300);
            // Mitten im Jahr eröffnet, mit Leuten, aber ohne Karren: kein Erz, kein Verkauf – nur Löhne und Unterhalt.
            var spaet = w.BetriebEroeffnen("Gießerei Spät", Betriebsart.Giesserei, k, startkarren: "");
            w.Einstellen(spaet, "Geselle", 1);
            Pruefe.Wahr(!spaet.Laufend.Ganzjaehrig, "mitten im Jahr eröffnet");
            var loehne = Daten.Value.Arbeitskraefte["Geselle"].Kosten;
            var unterhalt = spaet.Artdaten.Fixkosten;

            Ticken(sim, Spielzeit.MinutenProTag - 300);
            Pruefe.Wahr(spaet.Vorjahr is { Ganzjaehrig: false, Umsatz: 0 }, "erstes Jahr nicht ganz, kein Umsatz");
            Pruefe.Ungefaehr(-(loehne + unterhalt), spaet.Vorjahr!.Gewinn, 1e-9);
            Pruefe.Wahr(spaet.Laufend.Ganzjaehrig, "ab dem zweiten Jahr ganz");
            Ticken(sim, 50);

            // Speichern und Laden: beide Rechnungen bleiben, die Partie läuft gleich weiter.
            var json = Speicherung.Speichern(sim);
            var (sim2, _) = Speicherung.Laden(json);
            var spaet2 = sim2.Wirtschaft!.Betriebe.Single(b => b.Name == "Gießerei Spät");
            Pruefe.Gleich(spaet.Vorjahr.Gewinn, spaet2.Vorjahr!.Gewinn);
            Pruefe.Gleich((spaet.Laufend.Jahr, spaet.Laufend.Ganzjaehrig, spaet.Laufend.Personal), (spaet2.Laufend.Jahr, spaet2.Laufend.Ganzjaehrig, spaet2.Laufend.Personal));
            Ticken(sim, 1_000);
            Ticken(sim2, 1_000);
            Pruefe.Gleich(Speicherung.Speichern(sim, zeitpunkt: DateTime.UnixEpoch), Speicherung.Speichern(sim2, zeitpunkt: DateTime.UnixEpoch));

            // Alter Spielstand ohne Betriebsrechnung: kein Vorjahr, das laufende Jahr zählt ab dem Laden (nicht ganz).
            var alt = JsonNode.Parse(json)!.AsObject();
            foreach (var b in alt["Wirtschaft"]!["Betriebe"]!.AsArray())
            {
                b!.AsObject().Remove("Rechnung");
                b.AsObject().Remove("RechnungVorjahr");
            }
            var w3 = Speicherung.Laden(alt.ToJsonString()).Simulation.Wirtschaft!;
            var spaet3 = w3.Betriebe.Single(b => b.Name == "Gießerei Spät");
            Pruefe.Wahr(spaet3.Vorjahr is null && spaet3.Laufend is { Ganzjaehrig: false, Umsatz: 0 } && spaet3.Laufend.Jahr == Spielzeit.StartJahr + 1, "alter Spielstand");
        }),

        ("Bankrott: am dritten Jahresabschluss in Folge mit negativem Reinvermögen – ein Verkauf davor hebt das Reinvermögen und rettet das Haus", () =>
        {
            foreach (var verkaufen in new[] { false, true })
            {
                var sim = NeueSimulation();
                var w = sim.Wirtschaft!;
                var k = w.KontoEroeffnen("Haus Klamm", startvermoegen: -2_000);
                w.BetriebEroeffnen("Gießerei am Weinmarkt", Betriebsart.Giesserei, k, startkarren: "");
                var schmiede = w.BetriebEroeffnen("Gießerei Klamm", Betriebsart.Giesserei, k, startkarren: "");
                schmiede.Ausbauen(Betriebsart.Schmiede, 0);
                var grenze = Daten.Value.Schulden.JahreBisBankrott;
                Pruefe.Gleich(3, grenze);

                Ticken(sim, (grenze - 1) * Spielzeit.MinutenProTag);
                Pruefe.Gleich(grenze - 1, k.Schuldenjahre); // noch ein Abschluss im Minus, dann ist es aus
                Pruefe.Wahr(k.IstAktiv, "noch im Spiel");
                if (verkaufen)
                {
                    Pruefe.Wahr(w.VerkaufVorschau(schmiede).Summe > -k.Reinvermoegen, "der Erlös deckt die Schulden");
                    Pruefe.Wahr(w.Ausfuehren(k, new BetriebVerkaufen(schmiede.Name)).Erfolg, "verkaufen");
                }
                Ticken(sim, Spielzeit.MinutenProTag);
                Pruefe.Gleich(!verkaufen, k.IstBankrott);
                Pruefe.Gleich(verkaufen ? 0 : grenze, k.Schuldenjahre);
            }
        }),
    };
}
