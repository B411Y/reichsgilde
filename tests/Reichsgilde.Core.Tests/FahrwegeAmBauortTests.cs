using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Die Wege von einem Bauort zu allen Marktplätzen (<see cref="Stadtwirtschaft.FahrwegeAmBauort"/>, Anzeige des Fahrwegs im Baumodus, 01.11.2026):
/// Der beste Weg ist dieselbe Strecke und Zeit wie <see cref="Stadtwirtschaft.FahrzeitAmBauort"/> und nach dem Bau wie der Verlauf der Karren;
/// die Zwischenzeiten wachsen bis zur ungerundeten Fahrzeit.
/// </summary>
static class FahrwegeAmBauortTests
{
    static readonly string[] Arten = { "Handkarren", "Ochsenkarren", "Kriegskarren" };

    static (Stadtwirtschaft W, Konto K, Stadtkarte Karte) Partie(string kennung, string? json = null)
    {
        var daten = json is null ? Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()) : Wirtschaftsdaten.AusJson(json);
        var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, daten, new NeuePartie(Rivalen: 1), karte: karte);
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Probe", startvermoegen: 1_000_000);
        k.Stand = 1;
        k.WohnhausStufe = 1;
        k.BerufLernen("Schmied");
        w.Wohnen.HauswertAktualisieren(k);
        return (w, k, karte);
    }

    /// <summary>An Bauorten von nah bis fern: bester Weg = FahrzeitAmBauort (je Art hin und zurück), Verlauf passt zu den Stücken, Zwischenzeiten wachsen.</summary>
    static void WieDieFahrzeit(string kennung, int mindestensMaerkte)
    {
        var (w, k, karte) = Partie(kennung);
        var orte = w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Cast<WerkstattBauen>()
            .OrderBy(o => karte.MarktAbstandQuadrat(o.X, o.Z)).ToList();
        Pruefe.Wahr(orte.Count > 10, $"{kennung}: zu wenige Bauorte");
        var meisteMaerkte = 0;
        foreach (var ort in new[] { orte[0], orte[orte.Count / 4], orte[orte.Count / 2], orte[3 * orte.Count / 4], orte[^1] })
        {
            var wege = w.FahrwegeAmBauort(ort);
            var wo = $"{kennung} bei {ort.X} | {ort.Z}, Drehung {ort.Drehung}";
            Pruefe.Wahr(wege.Count > 0 && wege[0].Bester && wege.Skip(1).All(x => !x.Bester), $"{wo}: der beste Weg zuerst, nur einer");
            Pruefe.Gleich(wege.Count, wege.Select(x => x.Markt.Kennung).Distinct().Count(), $"{wo}: jeder Platz einmal");
            meisteMaerkte = Math.Max(meisteMaerkte, wege.Count);
            foreach (var art in Arten)
            {
                var zeiten = w.FahrzeitenAuf(wege[0], Betriebsart.Giesserei, art);
                Pruefe.Gleich((w.FahrzeitAmBauort(ort, art, true), w.FahrzeitAmBauort(ort, art, false)), ((int?)zeiten.Hin, (int?)zeiten.Zurueck), $"{wo}, {art}");
            }
            foreach (var weg in wege)
            {
                Pruefe.Gleich(weg.Hin.Count + 1, weg.Punkte.Count, $"{wo} → {weg.Markt.Name}: ein Punkt mehr als Stücke");
                var zwischen = w.Zwischenzeiten(weg, "Handkarren")!;
                Pruefe.Gleich(0.0, zwischen[0]);
                for (var i = 1; i < zwischen.Length; i++) Pruefe.Wahr(zwischen[i] >= zwischen[i - 1], $"{wo} → {weg.Markt.Name}: Zwischenzeit fällt bei Punkt {i}");
                var tempo = w.Daten.Logistik.Karren["Handkarren"].MeterJeMinute;
                Pruefe.Ungefaehr(w.Fahrminuten(weg.Hin, tempo), zwischen[^1], 1e-6);
                // ein anderer Platz ist nach den Kosten der Wegwahl nicht näher als der beste
                if (!weg.Bester) Pruefe.Wahr(Karrennetz.Kosten(weg.Hin) >= Karrennetz.Kosten(wege[0].Hin) - 1e-6, $"{wo}: {weg.Markt.Name} näher als der beste");
            }
        }
        Pruefe.Wahr(meisteMaerkte >= mindestensMaerkte, $"{kennung}: höchstens {meisteMaerkte} Plätze erreicht");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fahrwege am Bauort: Köln – der beste Weg wie FahrzeitAmBauort, Wege zu mehreren Plätzen, Zwischenzeiten bis zur Fahrzeit",
            () => WieDieFahrzeit("koeln", 2)),

        ("Fahrwege am Bauort: Greybox – ein Platz, der beste Weg wie FahrzeitAmBauort", () => WieDieFahrzeit("greybox", 1)),

        ("Fahrwege am Bauort: nach dem Bau fahren die Karren genau den besten Weg (Verlauf wie Stadtwirtschaft.Karrenverlauf, Platz wie MarktplatzVon)", () =>
        {
            var text = File.ReadAllText(Datenpfad.Wirtschaft());
            const string werkstatt = "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 1,";
            Pruefe.Wahr(text.Contains(werkstatt), "Werkstatt-Maße in wirtschaft.json haben sich geändert – Test anpassen.");
            var (w, k, karte) = Partie("koeln", text.Replace(werkstatt, "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 0,"));
            var orte = w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Cast<WerkstattBauen>()
                .OrderBy(o => karte.MarktAbstandQuadrat(o.X, o.Z)).ToList();
            var ort = orte[orte.Count / 2];
            var bester = w.FahrwegeAmBauort(ort)[0];
            Pruefe.Wahr(w.Ausfuehren(k, ort).Erfolg, "Bau");
            var betrieb = w.Betriebe.Single(b => b.Name == w.Bauen.Grundstuecke.Last(g => g.Art == Grundstuecksart.Werkstatt).Betrieb);
            Pruefe.Gleich(string.Join(";", bester.Punkte), string.Join(";", w.Karrenverlauf(betrieb)!));
            Pruefe.Gleich(bester.Markt.Kennung, w.MarktplatzVon(betrieb)!.Kennung);
        }),

        ("Fahrwege am Bauort: ohne Karte, für Umland und mit alten Daten (ohne Verlauf) keine Wege", () =>
        {
            var ohne = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                wirtschaftsdaten: Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft())).Wirtschaft!;
            Pruefe.Gleich(0, ohne.FahrwegeAmBauort(new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Count);
            var (w, _, _) = Partie("greybox");
            Pruefe.Gleich(0, w.FahrwegeAmBauort(new WerkstattBauen(Betriebsart.Mine, -24, 11, 2)).Count);
            var knoten = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()))!.AsObject();
            knoten["logistik"]!.AsObject().Remove("weglaenge");
            var (alt, ka, _) = Partie("greybox", knoten.ToJsonString());
            var ort = (WerkstattBauen)alt.Bauen.Bauorte(ka, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).First();
            Pruefe.Gleich(0, alt.FahrwegeAmBauort(ort).Count);
        }),
    };
}
