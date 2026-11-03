using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Bauortmessung – Fahrzeit an jedem Werkstatt-Bauort (Messung, kein Test; Befund H2 der Prüfung von phase5/gehtempo, 1.11.2026)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen bauorte [Karte …]   (ohne Karte: greybox koeln)
//
//  Stellt die Startpartie mit allen KI-Häusern auf und fragt für jeden Ort, an dem ein Bürger mit Geld jetzt eine Gießerei bauen dürfte
//  (Bauwesen.Bauorte), wie lange ein Handkarren von dort zum nächsten Marktplatz fährt (Stadtwirtschaft.FahrzeitAmBauort – dieselbe Zahl wie
//  nach dem Bau). Je Stelle (Lage ohne Drehung) zählt die schnellste Drehung. Schreibt die Verteilung (Quantile, Anteile bis 30/45/60/120
//  Minuten, an der Höchstdauer) und zwei Folgen von zehn Neubauten, die sich nicht überlappen: in der Reihenfolge der KI (Luftlinie zum
//  nächsten Platz, KiRivale.WerkstattBauen) und jeweils der schnellste freie Ort (ein Spieler, der die Fahrzeit kennt). Ändert nichts.
// ═════════════════════════════════════════════════════════════════════════════

static class Bauortmessung
{
    public static int Erstellen(string[] args)
    {
        var karten = args.Length > 0 ? args : new[] { "greybox", "koeln" };
        var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
        var hoechst = daten.Logistik.Weglaenge?.HoechstMinuten ?? int.MaxValue;
        Console.WriteLine($"Datenversion {daten.Version}; Handkarren {daten.Logistik.Karren["Handkarren"].Tempo:0.0#} m/s; Fahrzeit hin in Spielminuten (Kern), Höchstdauer {hoechst}");
        foreach (var kennung in karten)
        {
            var text = File.ReadAllText(Datenpfad.Karte(kennung));
            var haeuser = JsonNode.Parse(text)!["start"]?["haeuser"]?.AsArray().Count ?? 1;
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, daten,
                new NeuePartie(Rivalen: haeuser), karte: karte);
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Probe", startvermoegen: 1_000_000);
            k.Stand = 1;
            k.WohnhausStufe = 1;
            k.BerufLernen("Schmied");
            w.Wohnen.HauswertAktualisieren(k);
            var orte = w.Bauen.Bauorte(k, new WerkstattBauen(Betriebsart.Giesserei, 0, 0, 0)).Cast<WerkstattBauen>()
                .Select(o => (Ort: o, Minuten: w.FahrzeitAmBauort(o, "Handkarren") ?? 0)).ToList();
            var stellen = orte.GroupBy(x => (x.Ort.X, x.Ort.Z)).Select(g => g.OrderBy(x => x.Minuten).First()).ToList();
            var m = stellen.Select(x => (double)x.Minuten).OrderBy(x => x).ToList();
            Console.WriteLine();
            Console.WriteLine($"== {kennung}: {orte.Count} Bauorte (mit Drehung), {stellen.Count} Stellen; Startwerkstätten: " +
                              string.Join(", ", w.Betriebe.Where(b => b.InDerStadt).Select(b => w.Fahrzeit(b, "Handkarren", true)).OrderBy(x => x)) + " min");
            if (m.Count == 0) continue;
            string Q(double p) => Balancingbericht.Quantil(m, p).ToString("0");
            string Anteil(Func<double, bool> f) => $"{m.Count(f)} ({100.0 * m.Count(f) / m.Count:0} %)";
            Console.WriteLine($"Handkarren hin: kleinste {m[0]:0}, 10 % {Q(0.1)}, 25 % {Q(0.25)}, Median {Q(0.5)}, 75 % {Q(0.75)}, größte {m[^1]:0} min");
            Console.WriteLine($"Stellen bis 30 min {Anteil(x => x <= 30)}, bis 45 {Anteil(x => x <= 45)}, bis 60 {Anteil(x => x <= 60)}, bis 120 {Anteil(x => x <= 120)}, " +
                              $"an der Höchstdauer {Anteil(x => x >= hoechst)}");
            Folge("KI (Luftlinie zum nächsten Platz)", orte.OrderBy(x => karte.MarktAbstandQuadrat(x.Ort.X, x.Ort.Z)).ToList());
            Folge("schnellster freier Ort", orte.OrderBy(x => x.Minuten).ThenBy(x => karte.MarktAbstandQuadrat(x.Ort.X, x.Ort.Z)).ToList());

            void Folge(string wer, List<(WerkstattBauen Ort, int Minuten)> reihe)
            {
                var gewaehlt = new List<Grundriss>();
                var minuten = new List<int>();
                foreach (var (ort, min) in reihe)
                {
                    var g = w.Bauen.Grundriss(ort);
                    if (gewaehlt.Any(a => Ueberlappen(a, g))) continue;
                    gewaehlt.Add(g);
                    minuten.Add(min);
                    if (minuten.Count == 10) break;
                }
                Console.WriteLine($"Zehn Neubauten nacheinander, {wer}: {string.Join(", ", minuten)} min");
            }
        }
        return 0;
    }

    /// <summary>Überlappen zwei Grundrisse (Drehung in Vierteln: 1 und 3 tauschen Breite und Tiefe)?</summary>
    static bool Ueberlappen(Grundriss a, Grundriss b)
    {
        static (double X0, double X1, double Z0, double Z1) Kasten(Grundriss g)
        {
            var (bx, bz) = g.Drehung % 2 == 0 ? (g.Breite, g.Tiefe) : (g.Tiefe, g.Breite);
            return (g.X - bx / 2, g.X + bx / 2, g.Z - bz / 2, g.Z + bz / 2);
        }
        var (p, q) = (Kasten(a), Kasten(b));
        return p.X0 < q.X1 - 1e-6 && q.X0 < p.X1 - 1e-6 && p.Z0 < q.Z1 - 1e-6 && q.Z0 < p.Z1 - 1e-6;
    }
}
