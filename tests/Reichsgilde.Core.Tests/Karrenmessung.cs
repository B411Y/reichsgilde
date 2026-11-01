using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Karrenmessung – Fahrzeiten je Betrieb (Messung, kein Test; Datenversion 65, Fahrzeit nach Weglänge)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen karren [Karte …] [--jahre=n]   (ohne Karte: greybox koeln)
//
//  Stellt auf jeder Karte die Startpartie mit allen KI-Häusern auf („Schwer“: die KI-Häuser beginnen mit Werkstatt und Umlandbetrieb, so sind
//  auch die Umlandwege dabei) und schreibt je Betrieb mit Platz: Arbeitsort, Länge des Weges zum Markt,
//  die feste Fahrzeit (logistik.fahrzeiten) und die Fahrzeit nach Weglänge je Karrenart hin und zurück – in Klammern ohne Höchstdauer, wenn
//  sie greift. Dazu je Arbeitsort Mittel, kleinste und größte Länge und wie viele Fahrten die Höchstdauer kappt. Mit --jahre=n spielt die
//  Partie danach n Jahre und schreibt je Jahr und Betrieb Karren, Fahrten zum Markt, Stillstand (Anteil der Arbeitszeit) und Umsatz. Ändert nichts.
// ═════════════════════════════════════════════════════════════════════════════

static class Karrenmessung
{
    static readonly string[] Arten = { "Handkarren", "Ochsenkarren", "Kriegskarren" };

    public static int Erstellen(string[] args)
    {
        var jahre = args.FirstOrDefault(a => a.StartsWith("--jahre=")) is { } j ? int.Parse(j.Split('=')[1]) : 0;
        args = args.Where(a => !a.StartsWith("--")).ToArray();
        var karten = args.Length > 0 ? args : new[] { "greybox", "koeln" };
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var grenzen = daten.Logistik.Weglaenge;
        Console.WriteLine($"Datenversion {daten.Version}; Tempo {string.Join(", ", Arten.Select(a => $"{a} {daten.Logistik.Karren[a].Tempo:0.0#} m/s"))}; " +
                          (grenzen is null ? "feste Fahrzeiten" : $"mindestens {grenzen.MindestMinuten}, höchstens {grenzen.HoechstMinuten} Minuten"));
        foreach (var kennung in karten)
        {
            var text = File.ReadAllText(Datenpfad.Karte(kennung));
            var haeuser = JsonNode.Parse(text)!["start"]?["haeuser"]?.AsArray().Count ?? 1;
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer }, daten,
                new NeuePartie(Rivalen: haeuser), karte: karte);
            var w = sim.Wirtschaft!;
            Console.WriteLine();
            Console.WriteLine($"== {kennung} ({haeuser} KI-Häuser) ==");
            Console.WriteLine("Betrieb | Ort | Länge (m) | fest | Hand hin/zurück | Ochse hin/zurück | Pferd hin/zurück | Startkarren");
            var jeOrt = new SortedDictionary<string, List<(double Laenge, int Gekappt)>>(StringComparer.Ordinal);
            foreach (var b in w.Betriebe.OrderBy(b => b.Ort).ThenBy(b => b.Name, StringComparer.Ordinal))
            {
                if (w.Fahrten(b) is not var (hin, zurueck) || hin.Count == 0)
                {
                    Console.WriteLine($"{b.Name} | {b.Ort} | kein Weg | {daten.Logistik.FahrzeitAb(b.Ort)} |");
                    continue;
                }
                var laenge = hin.Sum(s => s.Laenge);
                var gekappt = 0;
                string Zeiten(string typ)
                {
                    string Eine(bool zumMarkt)
                    {
                        var kern = w.Fahrzeit(b, typ, zumMarkt);
                        var frei = w.Fahrminuten(zumMarkt ? hin : zurueck, daten.Logistik.Karren[typ].MeterJeMinute);
                        if (grenzen is not null && frei > grenzen.HoechstMinuten + 0.5) { gekappt++; return $"{kern} ({frei:0})"; }
                        return kern.ToString();
                    }
                    return $"{Eine(true)}/{Eine(false)}";
                }
                var zeilen = string.Join(" | ", Arten.Select(Zeiten));
                Console.WriteLine($"{b.Name} | {b.Ort} | {laenge:0} | {daten.Logistik.FahrzeitAb(b.Ort)} | {zeilen} | {string.Join(", ", b.Karren.Select(k => k.Typ))}");
                if (!jeOrt.TryGetValue(b.Ort.ToString(), out var liste)) jeOrt[b.Ort.ToString()] = liste = new();
                liste.Add((laenge, gekappt));
            }
            foreach (var (ort, liste) in jeOrt)
            {
                var mittel = liste.Average(x => x.Laenge);
                string Bei(string typ) => $"{typ} {mittel / daten.Logistik.Karren[typ].MeterJeMinute:0}";
                Console.WriteLine($"{ort}: {liste.Count} Betriebe, Länge Ø {mittel:0} m ({liste.Min(x => x.Laenge):0}–{liste.Max(x => x.Laenge):0}); " +
                                  $"eben Ø {string.Join(", ", Arten.Select(Bei))} min; von der Höchstdauer gekappt {liste.Sum(x => x.Gekappt)} von {liste.Count * Arten.Length * 2} Fahrten");
            }
            for (var jahr = 1; jahr <= jahre; jahr++)
            {
                var start = sim.Zeit.Jahr;
                while (sim.Zeit.Jahr == start && !sim.IstBeendet) sim.Tick();
                Console.WriteLine($"-- Jahr {jahr}: Betrieb | Karren | Fahrten zum Markt | Stillstand | Umsatz | Gewinn");
                foreach (var b in w.Betriebe.OrderBy(b => b.Ort).ThenBy(b => b.Name, StringComparer.Ordinal))
                {
                    var fahrten = b.Karren.Sum(k => k.FahrtenVorjahr ?? 0);
                    var still = b.Arbeitsminuten > 0 ? 100.0 * b.StillstandMinutenVorjahr / b.Arbeitsminuten : 0;
                    Console.WriteLine($"   {b.Name} | {string.Join("+", b.Karren.Select(k => k.Typ.Replace("karren", "")))} | {fahrten} | {still:0} % | " +
                                      $"{b.Vorjahr?.Umsatz ?? 0:0} | {b.Vorjahr?.Gewinn ?? 0:0}");
                }
            }
        }
        return 0;
    }
}
