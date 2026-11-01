using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Fahrzeit an einem Bauort (<see cref="Stadtwirtschaft.FahrzeitAmBauort"/>, 1.11.2026, für die Messung des Ausbaus in Köln – Befund H2 der
/// Prüfung von phase5/gehtempo): Die Abfrage sagt vor dem Bau, wie lange die Karren einer Werkstatt an diesem Ort zum Markt führen – genau so
/// lange, wie es der Kern nach dem Bau rechnet.
/// </summary>
static class FahrzeitAmBauortTests
{
    static readonly string[] Arten = { "Handkarren", "Ochsenkarren", "Kriegskarren" };

    /// <summary>Die Wirtschaftsdaten mit Bauzeit 0 für Werkstätten: Die Werkstatt steht sofort, ohne ein Jahr zu spielen.</summary>
    static readonly Lazy<string> DatenSofort = new(() =>
    {
        var text = File.ReadAllText(Datenpfad.Wirtschaft());
        const string werkstatt = "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 1,";
        if (!text.Contains(werkstatt)) throw new InvalidOperationException("Werkstatt-Maße in wirtschaft.json haben sich geändert – Test anpassen.");
        return text.Replace(werkstatt, "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 0,");
    });

    /// <summary>
    /// Baut auf der Karte nacheinander Gießereien an verschiedenen Bauorten (der dem Markt nächste nach Luftlinie wie die KI, der fernste und
    /// einige dazwischen) und vergleicht vorher die Abfrage mit der Fahrzeit des gebauten Betriebs, für jede Karrenart hin und zurück.
    /// </summary>
    static void VorherWieNachher(string kennung, int bauten)
    {
        var daten = Wirtschaftsdaten.AusJson(DatenSofort.Value);
        var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, daten, new NeuePartie(Rivalen: 1), karte: karte);
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Probe", startvermoegen: 1_000_000);
        k.Stand = 1;
        k.WohnhausStufe = 1;
        k.BerufLernen("Schmied");
        w.Wohnen.HauswertAktualisieren(k);
        var gebaut = 0;
        for (var runde = 0; gebaut < bauten && runde < 3 * bauten; runde++)
        {
            var orte = w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0))
                .OrderBy(o => karte.MarktAbstandQuadrat(o.X, o.Z)).ToList();
            Pruefe.Wahr(orte.Count > 0, $"{kennung}: kein Bauort mehr nach {gebaut} Bauten");
            // der nächste nach Luftlinie, der fernste, dann über die Liste verteilt
            var ort = (WerkstattBauen)orte[gebaut switch { 0 => 0, 1 => orte.Count - 1, _ => orte.Count * gebaut / (bauten + 1) }];
            var vorher = Arten.SelectMany(a => new[] { true, false }.Select(hin => w.FahrzeitAmBauort(ort, a, hin))).ToList();
            Pruefe.Wahr(vorher.All(x => x is > 0), $"{kennung} {ort}: Abfrage ohne Zeit");
            var e = w.Ausfuehren(k, ort);
            Pruefe.Wahr(e.Erfolg, $"{kennung} {ort}: {e.Meldung}");
            var name = w.Bauen.Grundstuecke.Last(g => g.Art == Grundstuecksart.Werkstatt).Betrieb!;
            var betrieb = w.Betriebe.SingleOrDefault(b => b.Name == name);
            Pruefe.Wahr(betrieb is not null, $"{kennung}: {name} steht nicht sofort (Bauzeit 0)");
            var nachher = Arten.SelectMany(a => new[] { true, false }.Select(hin => (int?)w.Fahrzeit(betrieb!, a, hin))).ToList();
            Pruefe.Gleich(string.Join(", ", nachher), string.Join(", ", vorher), $"{kennung} {name} bei {ort.X} | {ort.Z}, Drehung {ort.Drehung}");
            gebaut++;
        }
        Pruefe.Gleich(bauten, gebaut);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fahrzeit am Bauort: Köln – vor dem Bau dieselbe Fahrzeit wie die gebaute Werkstatt (je Karrenart hin und zurück, fünf Bauorte von nah bis fern)",
            () => VorherWieNachher("koeln", 5)),

        ("Fahrzeit am Bauort: Greybox – vor dem Bau dieselbe Fahrzeit wie die gebaute Werkstatt (vier Bauorte)", () => VorherWieNachher("greybox", 4)),

        ("Fahrzeit am Bauort: ohne Karte und für einen Umlandbetrieb keine Antwort; mit alten Daten (ohne logistik.weglaenge) die feste Zeit", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(DatenSofort.Value);
            var ohne = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten).Wirtschaft!;
            Pruefe.Gleich(null, ohne.FahrzeitAmBauort(new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0), "Handkarren"));

            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("greybox"));
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, daten, new NeuePartie(Rivalen: 1), karte: karte);
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(null, w.FahrzeitAmBauort(new WerkstattBauen(Betriebsart.Mine, -24, 11, 2), "Ochsenkarren"));

            // Alte Daten: ohne Abschnitt „weglaenge“ rechnet der Kern auch die gebaute Werkstatt mit fester Zeit (mal dem Belag, gemittelt über die
            // Strecke der Karrenstraßen) – die Abfrage ebenso.
            var alt = Wirtschaftsdaten.AusJson(OhneWeglaenge(DatenSofort.Value));
            Pruefe.Gleich(null, alt.Logistik.Weglaenge);
            var (simAlt, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, alt, new NeuePartie(Rivalen: 1),
                karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
            var wa = simAlt.Wirtschaft!;
            var k = wa.KontoEroeffnen("Haus Alt", startvermoegen: 1_000_000);
            k.Stand = 1;
            k.WohnhausStufe = 1;
            k.BerufLernen("Schmied");
            wa.Wohnen.HauswertAktualisieren(k);
            var ort = (WerkstattBauen)wa.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).First();
            var vorher = (wa.FahrzeitAmBauort(ort, "Handkarren"), wa.FahrzeitAmBauort(ort, "Ochsenkarren", zumMarkt: false));
            Pruefe.Wahr(wa.Ausfuehren(k, ort).Erfolg, "Bau mit alten Daten");
            var betrieb = wa.Betriebe.Single(b => b.Name == wa.Bauen.Grundstuecke.Last(g => g.Art == Grundstuecksart.Werkstatt).Betrieb);
            Pruefe.Gleich(((int?)wa.Fahrzeit(betrieb, "Handkarren", true), (int?)wa.Fahrzeit(betrieb, "Ochsenkarren", false)), vorher);
            // ohne Weglänge hängt die Zeit nicht an der Karrenart
            Pruefe.Gleich(wa.FahrzeitAmBauort(ort, "Handkarren"), wa.FahrzeitAmBauort(ort, "Kriegskarren"));
            // Die Zeit allein sagt in der Greybox wenig (Belag am Start überall 1: jede Strecke 30 Minuten, Befund der Prüfung) – darum die Strecke
            // selbst: vor dem Bau dieselben Stücke wie nach dem Bau, an drei Bauorten von nah bis fern
            var orte = wa.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Cast<WerkstattBauen>()
                .OrderBy(o => wa.Karte!.MarktAbstandQuadrat(o.X, o.Z)).ToList();
            foreach (var o in new[] { orte[0], orte[orte.Count / 2], orte[^1] })
            {
                if (wa.Bauen.PlatzVerboten(o) is not null) continue; // überdeckt den ersten Bau
                var vorherStrecke = wa.StreckeAmBauort(o, alt.Art(Betriebsart.Giesserei), wa.Karte!, wa.Karrennetz!);
                Pruefe.Wahr(wa.Ausfuehren(k, o).Erfolg, $"Bau bei {o.X} | {o.Z}");
                var neu = wa.Betriebe.Single(b => b.Name == wa.Bauen.Grundstuecke.Last(g => g.Art == Grundstuecksart.Werkstatt).Betrieb);
                var nachher = wa.Fahrten(neu)!.Value;
                Pruefe.Wahr(vorherStrecke.Hin.Count > 0, $"Strecke bei {o.X} | {o.Z}");
                Pruefe.Gleich(string.Join(";", nachher.Hin), string.Join(";", vorherStrecke.Hin), $"hin bei {o.X} | {o.Z}");
                Pruefe.Gleich(string.Join(";", nachher.Zurueck), string.Join(";", vorherStrecke.Zurueck), $"zurück bei {o.X} | {o.Z}");
            }
        }),
    };

    /// <summary>Die Daten ohne <c>logistik.weglaenge</c> (wie ein Spielstand bis Datenversion 64).</summary>
    static string OhneWeglaenge(string json)
    {
        var knoten = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        knoten["logistik"]!.AsObject().Remove("weglaenge");
        return knoten.ToJsonString();
    }
}
