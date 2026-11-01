using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Fairness der Startgrundstücke nach dem Karrenweg (<see cref="Startwegpruefung"/>, Befund M4 der Prüfung von phase5/gehtempo, 1.11.2026):
/// Die Kartenprüfung misst „gleich weit vom Markt“ wie der Kern die Fahrzeit – den Weg vom Halteplatz bis zum Halt am nächsten Marktplatz –,
/// nicht mehr die Luftlinie. Abweichungen über ±10 % sind Hinweise.
/// </summary>
static class StartwegpruefungTests
{
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "katalog.json")));

    static Kartenentwurf Oeffnen(string kennung) => Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Startwege: Köln – je Haus Werkstatt und Umland gemessen wie der Kern (Halteplatz bis Halt), drei Hinweise: der Spieler +10 %, zwei Umlandbetriebe", () =>
        {
            var e = Oeffnen("koeln");
            var wege = e.Startwegrechnung();
            Pruefe.Gleich(8, wege.Count(w => w.Ort == Arbeitsort.Werkstatt));
            Pruefe.Gleich(7, wege.Count(w => w.Ort == Arbeitsort.Umland));
            Pruefe.Wahr(wege.All(w => w.Meter is > 0 && w.Markt is not null), "jeder Startbetrieb hat einen Weg");
            // mit demselben Modell (Schmied) liegen die sieben KI-Werkstätten gleich weit, der Spieler 2,5 m weiter (sein Weg ist 24 m, die alte feste Zeit)
            foreach (var w in wege.Where(w => w.Ort == Arbeitsort.Werkstatt && w.Wer != "Spieler"))
                Pruefe.Ungefaehr(21.1, w.Meter!.Value, 0.01);
            Pruefe.Ungefaehr(23.6, wege.Single(w => w.Wer == "Spieler").Meter!.Value, 0.01);
            var hinweise = e.Befunde.Where(b => b.Regel == "startweg").ToList();
            Pruefe.Wahr(hinweise.All(b => b.Schwere == Schwere.Hinweis), "nur Hinweise");
            Pruefe.Gleich("Bauernhof1, Grube4, S1_Werkstatt", string.Join(", ", hinweise.SelectMany(b => b.Gebaeude).Order(StringComparer.Ordinal)));
            Pruefe.Wahr(!e.HatFehler, "Köln ohne Fehler");
        }),

        ("Startwege: gleich der Strecke des Kerns – Umland je Betrieb, Werkstätten der Schmiede (das Bezugsmodell) auf Köln und Greybox", () =>
        {
            foreach (var kennung in new[] { "koeln", "greybox" })
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                var wege = Startwegpruefung.Rechnen(karte).ToDictionary(w => w.Platz);
                var haeuser = karte.Start!.Haeuser.Count;
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
                    Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()), new NeuePartie(Rivalen: haeuser, Beruf: "Schmied"), karte: karte);
                var w = sim.Wirtschaft!;
                var verglichen = 0;
                foreach (var b in w.Betriebe)
                {
                    if (w.Bauen.PlatzVon(b.Name)?.Gebaeude?.Kennung is not { } platz || !wege.TryGetValue(platz, out var weg)) continue;
                    if (b.InDerStadt && b.Artdaten.Beruf != Startwegpruefung.Bezugsberuf) continue;
                    var kern = w.Fahrten(b)!.Value.Hin.Sum(s => s.Laenge);
                    Pruefe.Ungefaehr(kern, weg.Meter!.Value, 1e-9);
                    Pruefe.Gleich(w.MarktplatzVon(b)!.Name, weg.Markt, $"{kennung} {platz}");
                    verglichen++;
                }
                Pruefe.Wahr(verglichen >= (kennung == "koeln" ? 10 : 5), $"{kennung}: nur {verglichen} Betriebe verglichen");
            }
        }),

        ("Startwege: aus dem Zwischenspeicher – ein Gebäude verschoben rechnet nur die Wege neu (nicht das Netz), ein Marktplatz auch das Netz; die Greybox prüft sie nicht", () =>
        {
            var e = Oeffnen("koeln");
            var vorher = e.Startwegrechnung();
            var netz = e.Startnetz()!.Value.Netz;
            Pruefe.Wahr(ReferenceEquals(vorher, e.Startwegrechnung()), "ohne Änderung derselbe Stand");
            var s2 = e.GebaeudeMit("S2_Werkstatt")!;
            var alt = vorher.Single(w => w.Platz == "S2_Werkstatt").Meter!.Value;
            e.Verschieben(s2.Kennung, s2.X + 8, s2.Z); // an derselben Straße 8 m weiter vom Platz weg
            var neu = e.Startwegrechnung().Single(w => w.Platz == "S2_Werkstatt").Meter!.Value;
            Pruefe.Ungefaehr(alt + 8, neu, 0.01);
            Pruefe.Wahr(ReferenceEquals(netz, e.Startnetz()!.Value.Netz), "das Netz bleibt");
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "startweg" && b.Gebaeude.Contains("S2_Werkstatt")), "S2 jetzt mit Hinweis");
            var markt = e.Gebaeude.First(g => g.Katalog == Stadtkarte.MarktKatalog);
            e.Verschieben(markt.Kennung, markt.X, markt.Z + 2);
            Pruefe.Wahr(!ReferenceEquals(netz, e.Startnetz()!.Value.Netz), "ein Marktplatz baut das Netz neu");

            var greybox = Oeffnen("greybox");
            Pruefe.Wahr(greybox.Befunde.All(b => b.Regel != "startweg"), "Greybox ohne Regel der Startgrundstücke");
        }),
    };
}
