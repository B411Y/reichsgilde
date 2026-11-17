using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Nur noch Bauzonen, Aufräumen (16.11.2026): Die Karten haben keine Werkstatt- und Wohnhausplätze und keine Hofstellen mehr. Eine Kartendatei
/// von vorher lädt weiter – Kern und Kartenwerkstatt vergessen die alten Felder still, und die Häuser stehen in den Bauzonen. (Alte Spielstände:
/// StartortlosTests.) Die alte Karte entsteht hier aus der heutigen Probestadt, der die alten Felder wieder eingesetzt werden.
/// </summary>
static class AlteKartenfelderTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static string Heute => File.ReadAllText(Altkarte.Pfad("probestadt"));

    /// <summary>Die Probestadt, wie sie bis zum 16.11.2026 war: vier Werkstatt- und zwei Wohnhausplätze, Start auf Plätzen, Hofstellen.</summary>
    static string Alt(Action<JsonObject>? aendern = null)
    {
        var k = JsonNode.Parse(Heute)!.AsObject();
        var gebaeude = k["gebaeude"]!.AsArray();
        JsonObject Platz(string kennung, string katalog, double x, double z, double drehung, string? betrieb = null)
        {
            var g = new JsonObject { ["kennung"] = kennung, ["katalog"] = katalog, ["name"] = kennung, ["punkt"] = new JsonArray(x, z), ["drehung"] = drehung };
            if (betrieb is not null) g["betrieb"] = betrieb;
            return g;
        }
        gebaeude.Add(Platz("Werkstattplatz1", "werkstattplatz", -30, -10, 0));
        gebaeude.Add(Platz("Werkstattplatz2", "werkstattplatz", 32, -10, 0, "Tischlerei Ahlbeck"));
        gebaeude.Add(Platz("Werkstattplatz4", "werkstattplatz", 32, 10, 180, "Gießerei Brendel"));
        gebaeude.Add(Platz("Wohnhausplatz1", "wohnhausplatz", -40, -44, 0));
        gebaeude.Add(Platz("Wohnhausplatz2", "wohnhausplatz", -28, -44, 0));
        gebaeude.Add(Platz("Wohnhausplatz3", "wohnhausplatz", -16, -44, 0));
        var start = k["start"]!.AsObject();
        var spieler = new JsonObject { ["farbe"] = "#336bb8" }; // bis zum 17.11.2026 nannte die Karte die Farbe des Spielers
        start["spieler"] = spieler;
        spieler["wohnhaus"] = "Wohnhausplatz2";
        spieler["werkstatt"] = new JsonObject { ["platz"] = "Werkstattplatz1", ["name"] = "{art} am Markt" };
        spieler["jeBeruf"] = new JsonObject { ["Tischler"] = new JsonObject { ["platz"] = "Werkstattplatz1", ["name"] = "{art} an der Hauptstraße" } };
        var haeuser = start["haeuser"]!.AsArray();
        for (var i = 0; i < haeuser.Count; i++)
        {
            haeuser[i]!["wohnhaus"] = i == 0 ? "Wohnhausplatz1" : "Wohnhausplatz3";
            haeuser[i]!["betriebe"]![0]!["platz"] = i == 0 ? "Werkstattplatz2" : "Werkstattplatz4";
        }
        start["hofstellen"] = new JsonArray(
            new JsonObject { ["kennung"] = "S1", ["name"] = "an der Hauptstraße", ["werkstatt"] = "Werkstattplatz1", ["wohnhaus"] = "Wohnhausplatz2", ["start"] = true, ["nummer"] = 1 },
            new JsonObject { ["kennung"] = "S2", ["werkstatt"] = "Werkstattplatz2", ["wohnhaus"] = "Wohnhausplatz1", ["start"] = true, ["nummer"] = 2 });
        aendern?.Invoke(k);
        return k.ToJsonString();
    }

    static string Grundstuecke(string karte)
    {
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Rivalen: 2, Aufstellung: Aufstellungsart.Vorgabe),
            karte: Stadtkarte.AusJson(karte, Katalog.Value));
        return string.Join("\n", sim.Wirtschaft!.Bauen.Grundstuecke.Select(g => $"{g.Art} {g.Grundriss} {g.Besitzer?.Inhaber} {g.Betrieb}"));
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Alte Kartenfelder: Der Kern liest eine Karte mit Werkstatt- und Wohnhausplätzen und Hofstellen wie die heutige – die Plätze sind weg, die Werkstatt in der Stadt hat keinen Platz, der Umlandbetrieb behält seinen", () =>
        {
            var (alt, heute) = (Stadtkarte.AusJson(Alt(), Katalog.Value), Stadtkarte.AusJson(Heute, Katalog.Value));
            Pruefe.Wahr(Alt().Contains("\"werkstattplatz\"") && Alt().Contains("\"hofstellen\""), "die alte Karte hat die alten Felder");
            Pruefe.Wahr(alt.Gebaeude.All(g => g.Art is not (Bausteinart.Werkstattplatz or Bausteinart.Wohnhausplatz)), "keine Plätze mehr");
            Pruefe.Gleich(string.Join(",", heute.Gebaeude.Select(g => g.Kennung)), string.Join(",", alt.Gebaeude.Select(g => g.Kennung)));
            Pruefe.Gleich(2, alt.Startbelegung.Haeuser.Count);
            Pruefe.Wahr(alt.Startbelegung.Haeuser.All(h => h.Wohnhaus is null && h.Betriebe[0].Platz == ""), "kein Wohnhaus, kein Platz der Werkstatt");
            Pruefe.Gleich(Betriebsart.Tischlerei, alt.Startbelegung.Haeuser[0].Betriebe[0].Art);
            Pruefe.Wahr(alt.Startbelegung.Spieler.Wohnhaus is null && alt.Startbelegung.Spieler.JeBeruf.Count == 0, "der Spieler hat keinen Platz der Karte");
            Pruefe.Gleich("#336bb8", alt.Startbelegung.Spieler.Farbe);
            // die Karten des Spiels haben die alten Felder nicht mehr, und ihre Umlandbetriebe stehen weiter im Start
            foreach (var kennung in new[] { "greybox", "koeln", "probestadt", "messstadt" })
            {
                var text = File.ReadAllText(Altkarte.Pfad(kennung));
                foreach (var feld in new[] { "\"werkstattplatz\"", "\"wohnhausplatz", "\"hofstellen\"", "\"wohnhaus\"" })
                    Pruefe.Wahr(!text.Contains(feld), $"{kennung}: kein {feld}");
            }
            var koeln = Stadtkarte.AusDatei(Altkarte.Pfad("koeln"));
            Pruefe.Wahr(koeln.Startbelegung.Haeuser.All(h => h.Betriebe.Count(b => b.Platz == "") == 1 && h.Betriebe.Any(b => koeln.GebaeudeMit(b.Platz) is { Art: Bausteinart.Umlandbetrieb })),
                "Köln: je Haus eine Werkstatt ohne Platz und ein Umlandbetrieb mit Platz");
        }),

        ("Alte Kartenfelder: Die Kartenwerkstatt öffnet die alte Karte ohne Fehler und schreibt sie ohne die alten Felder – Zeichen für Zeichen wie die heutige", () =>
        {
            // ohne die feste Farbe des Spielers: Die bleibt einer alten Karte erhalten (HausfarbeWappenTests), die heutige hat keine mehr
            var e = Kartenentwurf.AusJson(Alt(k => k["start"]!["spieler"]!.AsObject().Remove("farbe")), Katalog.Value);
            e.Daten = Daten.Value;
            Pruefe.Wahr(e.Gebaeude.All(g => g.Art is not (Bausteinart.Werkstattplatz or Bausteinart.Wohnhausplatz)), "keine Plätze im Entwurf");
            Pruefe.Wahr(e.Start.Haeuser.All(h => h.Betriebe[0].Platz == ""), "die Werkstatt in der Stadt ohne Platz");
            Pruefe.Gleich(0, e.Befunde.Count(b => b.Schwere == Schwere.Fehler));
            var geschrieben = e.ZuJson();
            foreach (var feld in new[] { "werkstattplatz", "wohnhausplatz", "hofstellen", "\"wohnhaus\"", "jeBeruf", "Werkstattplatz1" })
                Pruefe.Wahr(!geschrieben.Contains(feld), $"geschrieben ohne {feld}");
            Pruefe.Gleich(Kartenentwurf.AusJson(Heute, Katalog.Value).ZuJson(), geschrieben);
        }),

        ("Alte Kartenfelder: Eine Partie auf der alten Karte stellt jedes Haus auf dieselben gelosten Grundstücke in den Bauzonen wie auf der heutigen", () =>
        {
            var alt = Grundstuecke(Alt());
            Pruefe.Gleich(Grundstuecke(Heute), alt);
            Pruefe.Gleich(3, alt.Split('\n').Count(z => z.StartsWith("Wohnhaus ")));
            Pruefe.Gleich(3, alt.Split('\n').Count(z => z.StartsWith("Werkstatt ")));
        }),

        ("Alte Kartenfelder: Ein Startbetrieb mit Platz muss ein Umlandbetrieb sein – ein anderes Gebäude lehnt der Kern ab, die Kartenprüfung meldet es als Fehler", () =>
        {
            var rathaus = Stadtkarte.AusJson(Heute, Katalog.Value).Gebaeude.First(g => g.Katalog == "rathaus").Kennung;
            string Mit(string platz) => Alt(k => k["start"]!["haeuser"]![0]!["betriebe"]![0]!["platz"] = platz);
            Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson(Mit(rathaus), Katalog.Value), "ist kein Umlandbetrieb der Karte");
            var e = Kartenentwurf.AusJson(Mit(rathaus), Katalog.Value);
            e.Daten = Daten.Value;
            Pruefe.Wahr(e.Befunde.Any(b => b.Schwere == Schwere.Fehler && b.Regel == "start" && b.Text.Contains("ist kein Umlandbetrieb der Karte")), "Fehler der Kartenprüfung");
        }),

        ("Alte Kartenfelder: Die Startwege (Regel „startweg“) messen nur noch die Umlandbetriebe der KI-Häuser – Köln sieben, die Probestadt keinen", () =>
        {
            var koeln = Startwegpruefung.Rechnen(Stadtkarte.AusDatei(Altkarte.Pfad("koeln")));
            Pruefe.Gleich(7, koeln.Count);
            Pruefe.Wahr(koeln.All(w => w.Ort == Arbeitsort.Umland && w.Meter is > 0), "alle im Umland, alle mit Weg");
            Pruefe.Gleich(0, Startwegpruefung.Rechnen(Stadtkarte.AusJson(Alt(), Katalog.Value)).Count);
        }),

        ("Alte Kartenfelder: Ladeprobe – Karte kaputt (Umlandbetrieb ohne Namen), ein Weg stößt das Netz an, mitten im Lauf repariert: danach Startwege und Befunde wie frisch geöffnet", () =>
        {
            static string Text(IEnumerable<Befund> befunde) =>
                string.Join("\n", befunde.Select(b => $"{b.Schwere} {b.Regel}: {b.Text} @ {b.Ort} [{string.Join(",", b.Gebaeude)}] ({string.Join(",", b.Andere.Select(t => t.Bezeichnung))})"));
            static string Wege(IEnumerable<Startweg> wege) =>
                string.Join("\n", wege.Select(w => $"{w.Wer} {w.Platz} {w.Ort} {(w.Meter is { } m ? BitConverter.DoubleToInt64Bits(m) : -1)} {w.Markt}"));
            var e = Kartenentwurf.AusJson(File.ReadAllText(Altkarte.Pfad("koeln")), Katalog.Value);
            _ = e.Befunde.Count;
            var betrieb = e.GebaeudeMit("Grube4")!.Betrieb;
            e.BetriebSetzen("Grube4", null);
            Pruefe.Gleich(0, e.Befunde.Count(b => b.Regel == "startweg"), "kaputt – keine Startwege");
            e.WegVerschieben(e.Wege[0], 0.5, 0); // das Netz muss neu
            using (var lauf = e.StartwegSchritte().GetEnumerator())
                Pruefe.Wahr(lauf.MoveNext() && lauf.Current == "Startwege: Karte schreiben", "die kaputte Karte geschrieben");
            e.BetriebSetzen("Grube4", betrieb); // repariert, mitten im Netz
            Pruefe.Gleich(Wege(Startwegpruefung.Rechnen(e.ZuStadtkarte())), Wege(e.Startwegrechnung()), "Startwege");
            Pruefe.Wahr(e.Startwegrechnung().Count > 0, "repariert – Startwege");
            Pruefe.Gleich(Text(Kartenpruefung.Pruefen(Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value))), Text(e.Befunde), "Befunde");
        }),
    };
}
