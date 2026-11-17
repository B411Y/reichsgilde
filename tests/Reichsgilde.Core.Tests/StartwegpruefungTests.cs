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

    static Kartenentwurf Oeffnen(string kennung) => Kartenentwurf.AusJson(File.ReadAllText(Altkarte.Pfad(kennung)), Katalog.Value);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Startwege: Köln – je KI-Haus der Umlandbetrieb gemessen wie der Kern (Halteplatz bis Halt), zwei Hinweise; die Werkstätten in der Stadt haben keinen Platz mehr", () =>
        {
            var e = Oeffnen("koeln");
            var wege = e.Startwegrechnung();
            Pruefe.Gleich(0, wege.Count(w => w.Ort == Arbeitsort.Werkstatt)); // bis 16.11.2026 acht Start-Hofstellen (21,1 m, der Spieler 23,6 m)
            Pruefe.Gleich(7, wege.Count(w => w.Ort == Arbeitsort.Umland));
            Pruefe.Wahr(wege.All(w => w.Meter is > 0 && w.Markt is not null), "jeder Startbetrieb hat einen Weg");
            Pruefe.Gleich(string.Join(", ", e.Start.Haeuser.Select(h => h.Name)), string.Join(", ", wege.Select(w => w.Wer)));
            var hinweise = e.Befunde.Where(b => b.Regel == "startweg").ToList();
            Pruefe.Wahr(hinweise.All(b => b.Schwere == Schwere.Hinweis), "nur Hinweise");
            Pruefe.Gleich("Bauernhof1, Grube4", string.Join(", ", hinweise.SelectMany(b => b.Gebaeude).Order(StringComparer.Ordinal)));
            Pruefe.Wahr(!e.HatFehler, "Köln ohne Fehler");
        }),

        ("Startwege: gleich der Strecke des Kerns – je Umlandbetrieb der KI-Häuser auf Köln und Greybox", () =>
        {
            foreach (var kennung in new[] { "koeln", "greybox" })
            {
                var karte = Stadtkarte.AusDatei(Altkarte.Pfad(kennung));
                var wege = Startwegpruefung.Rechnen(karte).ToDictionary(w => w.Platz);
                var haeuser = karte.Start!.Haeuser.Count;
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
                    Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()), FesteStartorte.Partie(kennung, new NeuePartie(Rivalen: haeuser, Beruf: "Schmied", Aufstellung: Aufstellungsart.Vorgabe)), karte: karte);
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
                Pruefe.Gleich(kennung == "koeln" ? 7 : 3, verglichen, $"{kennung}: Betriebe verglichen");
            }
        }),

        ("Startwege: aus dem Zwischenspeicher – ein Umlandbetrieb verschoben rechnet nur die Wege neu (nicht das Netz), ein Marktplatz auch das Netz; die Greybox prüft sie auch", () =>
        {
            var e = Oeffnen("koeln");
            var vorher = e.Startwegrechnung();
            var netz = e.Startnetz()!.Value.Netz;
            Pruefe.Wahr(ReferenceEquals(vorher, e.Startwegrechnung()), "ohne Änderung derselbe Stand");
            var grube = e.GebaeudeMit("Grube2")!;
            var alt = vorher.Single(w => w.Platz == "Grube2").Meter!.Value;
            e.Verschieben(grube.Kennung, grube.X + 6, grube.Z + 6);
            var neu = e.Startwegrechnung().Single(w => w.Platz == "Grube2").Meter!.Value;
            Pruefe.Wahr(Math.Abs(neu - alt) > 1, $"der Weg der Grube ändert sich ({alt:0.0} → {neu:0.0} m)");
            Pruefe.Ungefaehr(Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value).Startwegrechnung().Single(w => w.Platz == "Grube2").Meter!.Value, neu, 1e-9);
            Pruefe.Wahr(ReferenceEquals(netz, e.Startnetz()!.Value.Netz), "das Netz bleibt");
            var markt = e.Gebaeude.First(g => g.Katalog == Stadtkarte.MarktKatalog);
            e.Verschieben(markt.Kennung, markt.X, markt.Z + 2);
            Pruefe.Wahr(!ReferenceEquals(netz, e.Startnetz()!.Value.Netz), "ein Marktplatz baut das Netz neu");

            var greybox = Oeffnen("greybox");
            // die Greybox misst die drei Umlandbetriebe ihrer KI-Häuser; zwei liegen außerhalb des Bandes
            Pruefe.Gleich(3, greybox.Startwegrechnung().Count);
            Pruefe.Gleich("Grube_Eisenbart, Steinbruch_Kieselbach",
                string.Join(", ", greybox.Befunde.Where(b => b.Regel == "startweg").SelectMany(b => b.Gebaeude).Order(StringComparer.Ordinal)));
        }),

        ("Startwege: ein Gebäude ohne Startplatz rechnet nichts neu; nach einem Ladefehler (Umlandbetrieb ohne Betrieb) springt die Regel nach der Behebung wieder an", () =>
        {
            var e = Oeffnen("koeln");
            var vorher = e.Startwegrechnung();
            var kulisse = e.Gebaeude.First(g => g.Katalog == "leerstand");
            e.Verschieben(kulisse.Kennung, kulisse.X + 2, kulisse.Z);
            Pruefe.Wahr(ReferenceEquals(vorher, e.Startwegrechnung()), "ein fremdes Gebäude: dieselben Wege");
            Pruefe.Gleich(2, e.Befunde.Count(b => b.Regel == "startweg"));

            // Befund der zweiten Prüfung: Musste das Netz neu gebaut werden, während sich die Karte nicht laden ließ (Umlandbetrieb ohne Betrieb), blieb die
            // Regel danach stumm, bis sich wieder ein Weg änderte
            var grube = e.GebaeudeMit("Grube4")!;
            var betrieb = grube.Betrieb;
            var weg = e.Wege[0];
            e.BetriebSetzen("Grube4", null);
            e.WegVerschieben(weg, 0.5, 0); // das Netz wird neu gebaut – die Karte lädt jetzt nicht
            Pruefe.Gleich(0, e.Befunde.Count(b => b.Regel == "startweg"), "ohne Betrieb lädt die Karte nicht");
            Pruefe.Wahr(e.Startnetz() is null, "kein Netz");
            e.BetriebSetzen("Grube4", betrieb);
            Pruefe.Gleich(2, e.Befunde.Count(b => b.Regel == "startweg"), "behoben: wieder zwei Hinweise");
            e.BetriebSetzen("Grube4", null);
            e.WegVerschieben(weg, -0.5, 0);
            Pruefe.Gleich(0, e.Befunde.Count(b => b.Regel == "startweg"), "wieder kaputt");
            e.Rueckgaengig(); // der Weg zurück, die Karte noch kaputt
            e.Rueckgaengig(); // der Betrieb zurück
            Pruefe.Gleich(2, e.Befunde.Count(b => b.Regel == "startweg"), "Rückgängig: wieder zwei Hinweise");
            var s3 = e.GebaeudeMit("Grube2")!;
            e.Verschieben(s3.Kennung, s3.X + 15, s3.Z);
            Pruefe.Gleich(Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value).Befunde.Count(b => b.Regel == "startweg"), e.Befunde.Count(b => b.Regel == "startweg"),
                "gleich wie frisch geöffnet");
        }),
    };
}
