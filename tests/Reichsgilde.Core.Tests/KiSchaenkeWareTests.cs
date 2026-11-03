using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests: Die KI wählt die Ware einer Schänke nach dem Ausschank (Abschnitt <c>ki.schaenke</c>). Was die Gäste bestellen, bringt den festen
/// Ausschankpreis; nur der Rest geht zum Marktpreis. Ohne den Abschnitt rechnet die KI wie jede Werkstatt zum Marktpreis (bitgleich wie vorher).
/// </summary>
static class KiSchaenkeWareTests
{
    static readonly Lazy<string> Json = new(() => Datenpfad.OhneJahreszeiten()); // ohne Schlamm und Festmarkt: die Preise bleiben, wie der Test sie setzt

    /// <summary>Die echten Daten, ohne <c>ki.schaenke</c> (null) oder mit diesem Gästeanteil.</summary>
    static Wirtschaftsdaten Daten(double? gaesteanteil)
    {
        var knoten = JsonNode.Parse(Json.Value)!.AsObject();
        var ki = knoten["ki"]!.AsObject();
        Pruefe.Wahr(ki["schaenke"] is null, "ki.schaenke steht schon in wirtschaft.json – Tests anpassen");
        if (gaesteanteil is not { } a) return Wirtschaftsdaten.AusJson(Json.Value);
        ki["schaenke"] = new JsonObject { ["gaesteanteil"] = a };
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    /// <summary>Ein KI-Haus mit einer Schänke (ein Geselle, kein Karren), als einzige Schänke der Stadt.</summary>
    static (Stadtwirtschaft W, KiRivale Ki, Betrieb S) KiSchaenke(double? gaesteanteil)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 },
            wirtschaftsdaten: Daten(gaesteanteil));
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Zapfhahn", startvermoegen: 10_000);
        var s = w.BetriebEroeffnen("Schänke Zapfhahn", Betriebsart.Schaenke, k, startkarren: "");
        w.Einstellen(s, "Geselle", 1);
        return (w, w.KiAnmelden(k), s);
    }

    /// <summary>Den Marktpreis einer Ware drücken, bis er unter diesem Anteil des Grundpreises liegt (andere haben viel davon verkauft).</summary>
    static void PreisDruecken(Stadtwirtschaft w, string ware, double anteil)
    {
        while (w.Markt[ware].Preis > anteil * w.Markt[ware].Grundpreis) w.Markt[ware].Verkaufen(5);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Schänke: ohne ki.schaenke rechnet die Schänke jede Ware zum Marktpreis – genau wie GewinnJeArbeitsminute", () =>
        {
            var (w, ki, s) = KiSchaenke(null);
            PreisDruecken(w, "Dünnbier", 0.8);
            foreach (var ware in s.Artdaten.Produkte)
                Pruefe.Gleich(ki.GewinnJeArbeitsminute(ware), ki.SchankwertJeArbeitsminute(s, ware));
        }),

        ("KI-Schänke: mit ki.schaenke und mehr Gästen als Stück – jede Ausschankware zum Ausschankpreis, Weingeist zum Marktpreis", () =>
        {
            var (w, ki, s) = KiSchaenke(1);
            var g = w.Gastwirtschaft;
            var stueck = Kalkulation.Leistung(w, s) * s.Arbeitsminuten / s.MinutenJeStueck("Dünnbier");
            Pruefe.Wahr(g.ErwarteteGaeste(s) > stueck, $"die einzige Schänke hat mehr Gäste ({g.ErwarteteGaeste(s):0}) als ein Geselle Dünnbier braut ({stueck:0})");
            foreach (var ware in new[] { "Dünnbier", "Körnermahlzeit" })
            {
                var d = w.Daten.Waren[ware];
                var zutaten = d.Zutaten.Sum(z => w.Markt[z.Key].Preis * z.Value);
                var erwartet = (g.Ausschankpreis(ware) * (1 - w.Umsatzsteuer) - zutaten) / d.Zeit;
                Pruefe.Ungefaehr(erwartet, ki.SchankwertJeArbeitsminute(s, ware), 1e-9);
            }
            Pruefe.Gleich(ki.GewinnJeArbeitsminute("Weingeist"), ki.SchankwertJeArbeitsminute(s, "Weingeist"));
        }),

        ("KI-Schänke: mit weniger Gästen als Stück geht nur ihr Anteil zum Ausschankpreis, der Rest zum Marktpreis; Gästeanteil 0 = Marktpreis", () =>
        {
            var (w, ki, s) = KiSchaenke(0.25);
            var g = w.Gastwirtschaft;
            PreisDruecken(w, "Dünnbier", 0.8);
            var d = w.Daten.Waren["Dünnbier"];
            var stueck = Kalkulation.Leistung(w, s) * s.Arbeitsminuten / s.MinutenJeStueck("Dünnbier");
            var anteil = g.ErwarteteGaeste(s) * 0.25 / stueck;
            Pruefe.Wahr(anteil is > 0 and < 1, $"Anteil der Gäste {anteil:0.00}");
            var zutaten = d.Zutaten.Sum(z => w.Markt[z.Key].Preis * z.Value);
            var preis = anteil * g.Ausschankpreis("Dünnbier") + (1 - anteil) * w.Markt["Dünnbier"].Preis;
            Pruefe.Ungefaehr((preis * (1 - w.Umsatzsteuer) - zutaten) / d.Zeit, ki.SchankwertJeArbeitsminute(s, "Dünnbier"), 1e-9);

            var (w0, ki0, s0) = KiSchaenke(0);
            PreisDruecken(w0, "Dünnbier", 0.8);
            Pruefe.Gleich(ki0.GewinnJeArbeitsminute("Dünnbier"), ki0.SchankwertJeArbeitsminute(s0, "Dünnbier"));
        }),

        ("KI-Schänke: wird die Körnermahlzeit am Markt teuer, stellt die KI ohne ki.schaenke vom Dünnbier um – mit bleibt sie dabei, die Gäste zahlen fest", () =>
        {
            foreach (var mit in new[] { false, true })
            {
                var (w, ki, s) = KiSchaenke(mit ? 1 : null);
                Pruefe.Wahr(w.Ausfuehren(s.Konto, new ProduktWaehlen(s.Name, "Dünnbier")).Erfolg, "Dünnbier gewählt");
                w.Markt["Körnermahlzeit"].Jahreszeitfaktor = 2; // mehr Nachfrage (wie zu einem Festmarkt): Preis × √2
                Pruefe.Wahr(ki.GewinnJeArbeitsminute("Körnermahlzeit") > 1.5 * ki.GewinnJeArbeitsminute("Dünnbier"), "zum Marktpreis lohnt die Körnermahlzeit");
                if (mit)
                    Pruefe.Wahr(ki.SchankwertJeArbeitsminute(s, "Körnermahlzeit") < 1.15 * ki.SchankwertJeArbeitsminute(s, "Dünnbier"),
                        "zum Ausschankpreis nicht 15 % mehr");
                ki.Stunde();
                Pruefe.Gleich(mit ? "Dünnbier" : "Körnermahlzeit", s.Produkt);
            }
        }),

        ("KI-Schänke: ki.schaenke.gaesteanteil muss zwischen 0 und 1 liegen", () =>
        {
            Pruefe.WirftMitMeldung(() => Daten(1.5), "ki.schaenke.gaesteanteil");
            Pruefe.WirftMitMeldung(() => Daten(-0.1), "ki.schaenke.gaesteanteil");
        }),
    };
}
