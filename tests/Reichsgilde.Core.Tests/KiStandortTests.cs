using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Bauort einer neuen Werkstatt nach dem Karrenweg (phase5/gehtempo-ki, offener Punkt aus „Mehrere Marktplätze“): Mit der Fahrzeit nach Weglänge
/// (Datenversion 65) nimmt die KI unter den <see cref="KiRivale.StandortKandidaten"/> nächsten Bauorten (Luftlinie zum nächsten Marktplatz) den mit
/// der kürzesten Fahrt des Startkarrens hin und zurück. In der Greybox liegt der Luftlinien-erste Bauort (0 | 25, Hof nach Süden) 25 m vom Platz,
/// sein Karren braucht aber 131 Minuten; gedreht wenige Meter daneben 24. Die Fahrzeit fragt die KI beim Kern ab
/// (<see cref="Stadtwirtschaft.FahrzeitAmBauort"/>) – der erste Test prüft, dass sie nach dem Bau die des Betriebs ist.
/// </summary>
static class KiStandortTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Wirtschaftsdaten DatenMit(Action<JsonObject> aendern)
    {
        var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()))!.AsObject();
        aendern(knoten);
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    /// <summary>Eine Partie auf dieser Karte mit einem KI-Haus und einem reichen Haus, das Gießereien bauen darf.</summary>
    static (Simulation Sim, Stadtwirtschaft W, KiRivale Ki, Konto Bauherr) Partie(string karte, Wirtschaftsdaten? daten = null)
    {
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, daten ?? Daten.Value,
            new NeuePartie(Rivalen: 1), karte: Stadtkarte.AusDatei(Datenpfad.Karte(karte)));
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 100_000);
        k.Stand = 3;
        k.BerufLernen("Schmied");
        return (sim, w, w.Rivalen[0], k);
    }

    /// <summary>Die freien Bauorte einer Gießerei, nach Luftlinie zum nächsten Marktplatz (wie die KI sie zuerst ordnet).</summary>
    static List<BauBefehl> NachLuftlinie(Stadtwirtschaft w, Konto k) =>
        w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).OrderBy(o => w.Karte!.MarktAbstandQuadrat(o.X, o.Z)).ToList();

    static void Jahre(Simulation sim, int n)
    {
        for (var i = 0; i < n * Spielzeit.MinutenProTag; i++) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Standort: Die Fahrzeit eines geplanten Bauorts ist genau die, die der Kern nach dem Bau rechnet (Greybox Hof zur Gasse und zum Weg, Köln über das Pflaster; alle Karrenarten)", () =>
        {
            foreach (var (karte, nummern) in new[] { ("greybox", new[] { 0, 2, 7 }), ("koeln", new[] { 0, 2 }) })
                foreach (var nr in nummern)
                {
                    var (sim, w, ki, k) = Partie(karte);
                    var ort = (WerkstattBauen)NachLuftlinie(w, k)[nr];
                    var ergebnis = w.Ausfuehren(k, ort);
                    Pruefe.Wahr(ergebnis.Erfolg, ergebnis.Meldung);
                    for (var i = 0; i < w.Bauen.Daten!.Werkstatt.BauzeitJahre * Spielzeit.MinutenProTag + 60; i++) sim.Tick(); // bis die Werkstatt steht
                    var b = w.Betriebe.Single(x => x.Konto == k);
                    // zum selben Zeitpunkt: Der Belag der Wege ändert sich zum Jahreswechsel (Austritt), beide rechnen mit dem heutigen
                    foreach (var typ in w.Daten.Logistik.Karren.Keys)
                        Pruefe.Gleich((w.Fahrzeit(b, typ, true), w.Fahrzeit(b, typ, false)), ki.FahrzeitAmBauort(ort, typ), $"{karte} #{nr} {typ}");
                }
        }),

        ("KI-Standort: In der Greybox baut die KI ihre Gießerei nicht am Luftlinien-ersten Bauort (131 Minuten), sondern am schnellsten der nächsten", () =>
        {
            var (sim, w, _, _) = Partie("greybox");
            var stadt = w.KontoEroeffnen("Haus Stadt");
            foreach (var l in w.FreieLiegenschaften.ToList()) w.BetriebEroeffnen(l.Name, l.Art, stadt, "");
            var k = w.KontoEroeffnen("Haus Gold", startvermoegen: 15_000);
            k.Stand = 1;
            k.WohnhausStufe = 1;
            w.Wohnen.HauswertAktualisieren(k);
            k.BerufLernen("Schmied");
            var vorher = NachLuftlinie(w, k);
            var ki = w.KiAnmelden(k, new KiProfil(MaxBetriebe: 1));
            var zeiten = vorher.Take(KiRivale.StandortKandidaten).Select(o => ki.FahrzeitAmBauort((WerkstattBauen)o, "Handkarren")!.Value).Select(z => z.Hin + z.Zurueck).ToList();
            Pruefe.Wahr(zeiten[0] > 200, $"Luftlinien-erster: {zeiten[0]} Minuten hin und zurück");
            Jahre(sim, 1);
            var baustelle = w.Bauen.Grundstuecke.Single(g => g.Besitzer == k && g.Art == Grundstuecksart.Werkstatt);
            var i = vorher.FindIndex(o => Math.Abs(o.X - baustelle.Grundriss.X) < 1e-9 && Math.Abs(o.Z - baustelle.Grundriss.Z) < 1e-9 && o.Drehung == baustelle.Grundriss.Drehung);
            Pruefe.Wahr(i >= 0 && i < KiRivale.StandortKandidaten, $"unter den {KiRivale.StandortKandidaten} nächsten: #{i}");
            Pruefe.Gleich(zeiten.Min(), zeiten[i], $"#{i} ist der schnellste");
            Pruefe.Wahr(zeiten.Take(i).All(z => z > zeiten[i]), "bei Gleichstand der nähere");
            Pruefe.Wahr(zeiten[i] < zeiten[0] / 2, $"{zeiten[i]} statt {zeiten[0]} Minuten");
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains($"baut {baustelle.Betrieb} neu") && e.Contains("Minuten zum Markt")), string.Join(" | ", ki.Entscheidungen));
        }),

        ("KI-Standort: Ohne Fahrzeit nach Weglänge (alte Daten) baut die KI wie bisher am nächsten Bauort nach Luftlinie", () =>
        {
            var (sim, w, _, _) = Partie("greybox", DatenMit(d => d["logistik"]!.AsObject().Remove("weglaenge")));
            var stadt = w.KontoEroeffnen("Haus Stadt");
            foreach (var l in w.FreieLiegenschaften.ToList()) w.BetriebEroeffnen(l.Name, l.Art, stadt, "");
            var k = w.KontoEroeffnen("Haus Gold", startvermoegen: 15_000);
            k.Stand = 1;
            k.WohnhausStufe = 1;
            w.Wohnen.HauswertAktualisieren(k);
            k.BerufLernen("Schmied");
            var erster = NachLuftlinie(w, k)[0];
            var ki = w.KiAnmelden(k, new KiProfil(MaxBetriebe: 1));
            Pruefe.Gleich(null, ki.FahrzeitAmBauort((WerkstattBauen)erster, "Handkarren"));
            Jahre(sim, 1);
            var baustelle = w.Bauen.Grundstuecke.Single(g => g.Besitzer == k && g.Art == Grundstuecksart.Werkstatt);
            Pruefe.Gleich((erster.X, erster.Z, erster.Drehung), (baustelle.Grundriss.X, baustelle.Grundriss.Z, baustelle.Grundriss.Drehung));
        }),
    };
}
