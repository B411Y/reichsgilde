using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Die Kartenprüfung in Häppchen bei Änderungen an Hofstellen und am Start (10.11.2026, Befund der Prüfung zu #409): Die Zahl der freien
/// Hofstellen zählt für die Werkstätten der Pflicht (Kapazität), die Werkstätten der Hofstellen sind Startplätze (Startwege). Setzen, Löschen,
/// Auflösen und „Start“ umschalten müssen deshalb die Zwischenspeicher von Kapazität und Startwegen verwerfen – auch mitten in einem halben Lauf.
/// Verglichen wird mit der vollen Prüfung einer frisch geöffneten Karte in einem Zug.
/// </summary>
static class HofstellenHaeppchenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenentwurf Lade(string kennung) => Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value);

    static string Text(IEnumerable<Befund> befunde) =>
        string.Join("\n", befunde.Select(b => $"{b.Schwere} {b.Regel}: {b.Text} @ {b.Ort} [{string.Join(",", b.Gebaeude)}] ({string.Join(",", b.Andere.Select(t => t.Bezeichnung))})"));

    /// <summary>Die volle Prüfung der Karte, wie sie jetzt ist, frisch geöffnet (ohne jeden Zwischenspeicher) und in einem Zug.</summary>
    static string Frisch(Kartenentwurf e) => Text(Kartenpruefung.Pruefen(Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value)));

    static string Kapazitaet(Kapazitaet k) =>
        string.Join("\n", k.Befunde.Select(b => $"{b.Schwere} {b.Regel}: {b.Text}")) + "\n--\n" + string.Join("\n", k.Bericht) + "\n--\n"
        + string.Join("\n", k.Plaetze.Select(p => p.Art + ": " + string.Join(";", p.Felder.Select(f => $"{f.I},{f.J}"))));

    static string Wege(IEnumerable<Startweg> wege) =>
        string.Join("\n", wege.Select(w => $"{w.Wer} {w.Platz} {w.Ort} {(w.Meter is { } m ? BitConverter.DoubleToInt64Bits(m) : -1)} {w.Markt}"));

    /// <summary>Geht einen halben Lauf (<paramref name="schritte"/> Häppchen) und lässt ihn liegen.</summary>
    static void Halb(IEnumerable<string> lauf, int schritte)
    {
        using var l = lauf.GetEnumerator();
        for (var i = 0; i < schritte && l.MoveNext(); i++) { }
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Hofstellen in Häppchen: nach je 12 zufälligen Änderungen an Hofstellen und Start (auch mitten im Lauf) auf Köln, Greybox und Messstadt dieselben Befunde wie frisch geöffnet", () =>
        {
            foreach (var (kennung, startwert) in new[] { ("koeln", 51), ("greybox", 52), ("messstadt", 53) })
            {
                var e = Lade(kennung);
                var r = new Random(startwert);
                Pruefe.Gleich(Frisch(e), Text(e.Befunde), $"{kennung}: geöffnet");
                for (var i = 0; i < 12; i++)
                {
                    var was = new List<string> { Werkstattaenderungen.Zufaellig(e, r, Werkstattaenderungen.Hofstellen) };
                    if (r.Next(2) == 0)
                    {
                        // mitten im Lauf: nach einer zufälligen Zahl von Häppchen noch eine Änderung an den Hofstellen
                        var nach = r.Next(300);
                        for (var n = 0; n < nach && !e.BefundeSchritt(); n++) { }
                        was.Add(Werkstattaenderungen.Zufaellig(e, r, Werkstattaenderungen.Hofstellen));
                    }
                    while (!e.BefundeSchritt()) { }
                    Pruefe.Gleich(Frisch(e), Text(e.Befunde), $"{kennung}, Änderung {i + 1} ({string.Join(", ", was)}): Befunde");
                }
            }
        }),

        ("Hofstellen in Häppchen: ein halber Lauf der Kapazität und der Startwege, dann Hofstelle auflösen, setzen, löschen, Start umschalten – Zahl für Zahl wie in einem Zug", () =>
        {
            var e = Lade("koeln");
            var schwere = Schwere.Hinweis;
            var kapazitaetAlle = e.KapazitaetSchritte(schwere).Count();
            var startwegeAlle = e.StartwegSchritte().Count();
            var vorher = Kapazitaet(e.Kapazitaetsrechnung(schwere));
            var r = new Random(61);
            Action Aufloesen = () => e.HofstelleAufloesen(e.Start.Hofstellen[^1].Kennung);
            Action Setzen = () => Werkstattaenderungen.Zufaellig(e, r, new[] { "hof-neu" });
            Action Start = () => e.HofstelleStartSetzen(e.Start.Hofstellen[0].Kennung, !e.Start.Hofstellen[0].Start);
            Action Loeschen = () => e.HofstelleLoeschen(e.Start.Hofstellen[^1].Kennung);
            Action Weg = () => e.WegVerschieben(e.Wege[0], 1, 0);
            Action Zurueck = () => e.Rueckgaengig();
            // je Paar: die erste Änderung verwirft, ein halber Lauf beginnt, die zweite fällt mitten hinein
            var paare = new (string Was, Action Erst, Action Dann)[]
            {
                ("Weg, dann auflösen", Weg, Aufloesen),
                ("auflösen, dann setzen", Aufloesen, Setzen),
                ("Weg, dann setzen", Weg, Setzen),
                ("setzen, dann Start umschalten", Setzen, Start),
                ("Weg, dann Start umschalten", Weg, Start),
                ("Start umschalten, dann löschen", Start, Loeschen),
                ("Weg, dann löschen", Weg, Loeschen),
                ("löschen, dann Rückgängig", Loeschen, Zurueck),
            };
            var veraendert = false;
            foreach (var (was, erst, dann) in paare)
            {
                erst();
                Halb(e.KapazitaetSchritte(schwere), kapazitaetAlle / 2);
                Halb(e.StartwegSchritte(), startwegeAlle / 2);
                dann();
                var k = Kapazitaet(e.Kapazitaetsrechnung(schwere));
                Pruefe.Gleich(Kapazitaet(Kapazitaetspruefung.Pruefen(e, schwere)), k, $"{was}: Kapazität");
                Pruefe.Gleich(Wege(Startwegpruefung.Rechnen(e.ZuStadtkarte())), Wege(e.Startwegrechnung()), $"{was}: Startwege");
                Pruefe.Gleich(Frisch(e), Text(e.Befunde), $"{was}: Befunde");
                veraendert |= k != vorher;
            }
            Pruefe.Wahr(veraendert, "die Hofstellen haben die Kapazität verändert (sonst prüft der Test nichts)");
        }),

        ("Hofstellen: wer alle löscht, speichert eine leere Liste – das Öffnen macht aus den Startpaaren keine neuen; Rückgängig bringt sie zurück", () =>
        {
            var e = Lade("greybox");
            var alle = e.Start.Hofstellen.Count;
            Pruefe.Wahr(alle > 0, "Greybox hat Hofstellen");
            foreach (var h in e.Start.Hofstellen.ToList()) e.HofstelleAufloesen(h.Kennung);
            var wieder = Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value);
            Pruefe.Gleich(0, wieder.Start.Hofstellen.Count, "nach dem Öffnen keine Hofstellen");
            Pruefe.Gleich(Frisch(e), Text(e.Befunde), "Befunde wie frisch geöffnet");
            Pruefe.Gleich(e.ZuJson(), wieder.ZuJson(), "dieselbe Datei");
            for (var i = 0; i < alle; i++) e.Rueckgaengig();
            Pruefe.Gleich(File.ReadAllText(Datenpfad.Karte("greybox")), e.ZuJson(), "Rückgängig: Greybox Byte für Byte");
        }),
    };
}
