using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Umland nach dem Karrenweg (phase5/deutz-ki, KiRivale.Umland.cs): Beim Pachten einer Umland-Liegenschaft zieht die KI vom erwarteten Gewinn ab,
/// was ihre Karren auf dem echten Weg nicht wegbringen, und rechnet die Karren, die der Weg verlangt, zu den Kosten. In Köln fährt der Holzschlag
/// im Eichenforst an der Höchstdauer (360 Minuten) – die KI pachtete ihn, er stand trotz drei Karren 53 % still.
/// </summary>
static class KiUmlandTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Wirtschaftsdaten DatenMit(Action<JsonObject> aendern)
    {
        var knoten = JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()))!.AsObject();
        aendern(knoten);
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    static (Stadtwirtschaft W, KiRivale Ki) Partie(string karte, Wirtschaftsdaten? daten = null)
    {
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, daten ?? Daten.Value,
            new NeuePartie(Rivalen: 3), karte: Stadtkarte.AusDatei(Datenpfad.Karte(karte))); // mit drei Häusern sind Grube Overstolz, Holzschlag Hardevust und Steinbruch Lyskirchen frei
        var w = sim.Wirtschaft!;
        return (w, w.Rivalen[0]);
    }

    /// <summary>Die KI ist reich und darf jedes Handwerk lernen – damit jede Liegenschaft ein Kandidat ist.</summary>
    static void Reich(Stadtwirtschaft w, KiRivale ki)
    {
        ki.Konto.Umsatz(200_000);
        ki.Konto.Stand = 3;
        foreach (var beruf in new[] { "Tischler", "Schmied", "Steinmetz", "Alchimist", "Wirt" }.Where(b => w.Daten.Berufe.ContainsKey(b)))
            if (!ki.Konto.Beherrscht(beruf)) ki.Konto.BerufLernen(beruf);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Umland: Im gebremsten Holzschlag (Köln, „Schwer“, ein Jahr) wählt der Vorarbeiter „Ordnungsliebend“ (mehr Lager: jeder Kriegskarren lädt 60 statt 40) – gerechnet wie beim Karrenkauf", () =>
        {
            var pfad = Datenpfad.Karte("koeln");
            var haeuser = JsonNode.Parse(File.ReadAllText(pfad))!["start"]!["haeuser"]!.AsArray().Count;
            var (sim, _) = Partieaufbau.Erstellen(
                Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
                Daten.Value, new NeuePartie(Rivalen: haeuser), karte: Stadtkarte.AusDatei(pfad));
            var w = sim.Wirtschaft!;
            var holz = w.Betriebe.Single(b => b.Name == "Holzschlag Hardevust");
            var ki = w.Rivalen.Single(r => r.Konto == holz.Konto);
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick();
            var vorher = holz.StillstandMinutenVorjahr / (double)holz.Arbeitsminuten;
            Pruefe.Wahr(vorher >= ki.Profil.StillstandFuerKarrenkauf, $"Stillstand {vorher:P0}");
            // „Ordnungsliebend“ (+20 Lager) bewertet der Kern mit 0; die KI nach dem Durchfluss mit dem größeren Lager
            Pruefe.Gleich(0.0, w.Personal.MehrgewinnJeJahr(holz, "Ordnungsliebend"));
            var jetzt = holz.Karren.Sum(k => ki.Durchfluss(holz, k.Typ));
            var mehr = holz.Karren.Sum(k => ki.Durchfluss(holz, k.Typ, holz.LagerKapazitaetJeWare + 20));
            Pruefe.Wahr(mehr > jetzt * 1.2, $"Durchfluss {jetzt} → {mehr}");
            var jahreswert = Kalkulation.Leistung(w, holz) * holz.Arbeitsminuten * ki.WertJeArbeitsminute(holz);
            var lager = ki.EigenschaftWert(holz, "Ordnungsliebend");
            Pruefe.Wahr(Math.Abs(lager - (vorher - DetMathe.Pow(vorher, mehr / jetzt)) * jahreswert) < 1e-9, $"Lager {lager}");
            // Mehr Förderung bringt nur für den Teil, der auch weggefahren wird
            foreach (var k in new[] { "Kraeftig", "GutesAuge" })
                Pruefe.Wahr(Math.Abs(ki.EigenschaftWert(holz, k) - w.Personal.MehrgewinnJeJahr(holz, k) * (1 - vorher)) < 1e-9, k);
            Pruefe.Wahr(lager > ki.EigenschaftWert(holz, "Kraeftig") && lager > ki.EigenschaftWert(holz, "GutesAuge"), "Lager gewinnt");
            // In einer Werkstatt bleibt es bei der Rechnung des Kerns
            var werkstatt = w.Betriebe.First(b => b.IstWerkstatt && b.Konto == ki.Konto);
            Pruefe.Gleich(w.Personal.MehrgewinnJeJahr(werkstatt, "Flink"), ki.EigenschaftWert(werkstatt, "Flink"));
        }),

        ("KI-Umland: Die Fahrzeit einer freien Liegenschaft ist genau die, die der Kern nach der Pacht rechnet (Köln: nah, weit, an der Höchstdauer; alle Karrenarten)", () =>
        {
            foreach (var name in new[] { "Grube am Silberberg", "Kräutergarten am Bach", "Holzschlag im Eichenforst", "Steinbruch Lyskirchen" })
            {
                var (w, ki) = Partie("koeln");
                var l = w.FreieLiegenschaften.Single(x => x.Name == name);
                var vorher = w.Daten.Logistik.Karren.Keys.ToDictionary(t => t, t => ki.FahrzeitDerLiegenschaft(name, l.Art, t));
                var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 10_000);
                var b = w.BetriebEroeffnen(name, l.Art, k, "");
                foreach (var typ in w.Daten.Logistik.Karren.Keys)
                    Pruefe.Gleich((w.Fahrzeit(b, typ, true), w.Fahrzeit(b, typ, false)), vorher[typ], $"{name} {typ}");
            }
        }),

        ("KI-Umland: Schätzung – nahe Liegenschaften voll weggebracht, der Holzschlag im Eichenforst (Höchstdauer) nur gut ein Drittel; Werkstatt und Wachturm ohne Schätzung", () =>
        {
            var (w, ki) = Partie("koeln");
            var silber = ki.UmlandSchaetzen("Grube am Silberberg", Betriebsart.Mine)!.Value;
            var eiche = ki.UmlandSchaetzen("Holzschlag im Eichenforst", Betriebsart.Holzfaellerei)!.Value;
            var bach = ki.UmlandSchaetzen("Kräutergarten am Bach", Betriebsart.Kraeutergarten)!.Value;
            Pruefe.Gleich(1.0, silber.Anteil);
            Pruefe.Wahr(eiche.Anteil is > 0.3 and < 0.45, $"Eichenforst {eiche.Anteil:0.00}");
            Pruefe.Wahr(bach.Anteil is > 0.6 and < 0.9, $"Kräutergarten am Bach {bach.Anteil:0.00}");
            Pruefe.Gleich(ki.Profil.MaxKarrenJeBetrieb, eiche.Karren);
            Pruefe.Wahr(silber.Karren < eiche.Karren, "die nahe Grube braucht weniger Karren");
            // Gewinn nach dem Weg: unverändert, wo alles wegkommt; sonst um den Rest der Förderung kleiner, Karren als Kosten
            var g = Kalkulation.ErwarteterJahresgewinn(w, Betriebsart.Mine);
            Pruefe.Gleich(g, ki.NachDemWeg("Grube am Silberberg", Betriebsart.Mine, g).Gewinn);
            var (gEiche, karren) = ki.NachDemWeg("Holzschlag im Eichenforst", Betriebsart.Holzfaellerei, g);
            Pruefe.Wahr(Math.Abs(gEiche - (g - (1 - eiche.Anteil) * eiche.Wert)) < 1e-9, $"{gEiche} statt {g}");
            Pruefe.Gleich((eiche.Karren - 1) * w.Daten.Logistik.Karren[eiche.Typ].Preis, karren);
            Pruefe.Gleich(null, ki.UmlandSchaetzen("Wachturm an der Landstraße", Betriebsart.Wachturm));
            Pruefe.Gleich(null, ki.UmlandSchaetzen("Gießerei", Betriebsart.Giesserei));
        }),

        ("KI-Umland: In Köln pachtet die KI den Holzschlag im Eichenforst nicht mehr (rechnet sich mit drei Karren nicht), die nahe Grube weiter zum vollen Gewinn", () =>
        {
            var (w, ki) = Partie("koeln");
            Reich(w, ki);
            var kandidaten = ki.Expansionskandidaten();
            Pruefe.Wahr(kandidaten.All(x => x.Angebot.Name != "Holzschlag im Eichenforst"), string.Join(", ", kandidaten.Select(x => x.Angebot.Name)));
            var grube = kandidaten.Single(x => x.Angebot.Name == "Grube am Silberberg");
            Pruefe.Wahr(kandidaten.Any(x => x.Angebot.Name == "Holzschlag Hardevust"), "der nähere Holzschlag bleibt ein Kandidat");
            var hardevust = kandidaten.Single(x => x.Angebot.Name == "Holzschlag Hardevust");
            Pruefe.Wahr(hardevust.Gewinn < grube.Gewinn && hardevust.Kosten > grube.Kosten, "der weitere Holzschlag gilt weniger und braucht mehr Karren");
        }),

        ("KI-Umland: Ohne Fahrzeit nach Weglänge (alte Daten) schätzt die KI wie bisher – der Holzschlag im Eichenforst gilt so viel wie der nähere", () =>
        {
            var (w, ki) = Partie("koeln", DatenMit(d => d["logistik"]!.AsObject().Remove("weglaenge")));
            Reich(w, ki);
            Pruefe.Gleich(null, ki.UmlandSchaetzen("Holzschlag im Eichenforst", Betriebsart.Holzfaellerei));
            var kandidaten = ki.Expansionskandidaten();
            var eiche = kandidaten.Single(x => x.Angebot.Name == "Holzschlag im Eichenforst");
            var hardevust = kandidaten.Single(x => x.Angebot.Name == "Holzschlag Hardevust");
            Pruefe.Gleich(hardevust.Gewinn, eiche.Gewinn);
            Pruefe.Gleich(hardevust.Kosten, eiche.Kosten);
        }),
    };
}
