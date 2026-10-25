using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Messung des Schiffshandels (PR #199, kein Test): Partien auf einer Karte mit Hafen, einmal mit und einmal ohne Abschnitt „schiffshandel“
/// (sonst gleich). Der Spieler bleibt passiv; die KI-Häuser kaufen Schiffe und schicken sie los.
/// Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen schiffe [Jahre] [Startwerte] [Karte]
///          (Vorgabe: 30 Jahre, 20 Startwerte, probestadt)
/// </summary>
static class Schiffsmessung
{
    public static int Erstellen(string[] args)
    {
        var jahre = args.Length > 0 ? int.Parse(args[0]) : 30;
        var startwerte = args.Length > 1 ? int.Parse(args[1]) : 20;
        var kartenname = args.Length > 2 ? args[2] : "probestadt";
        var json = File.ReadAllText(Datenpfad.Wirtschaft());
        var mit = Wirtschaftsdaten.AusJson(json);
        var knoten = JsonNode.Parse(json)!.AsObject();
        knoten.Remove("schiffshandel");
        var ohne = Wirtschaftsdaten.AusJson(knoten.ToJsonString());
        Stadtkarte Karte() => Stadtkarte.AusDatei(Datenpfad.Karte(kartenname));

        // Die Rechnung am Start: der beste Auftrag je Stadt für ein Haus mit 10.000 Gulden
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 101, Siegbedingung = Siegbedingung.Endlos },
                mit, new NeuePartie(Rivalen: Karte().Startbelegung.Haeuser.Count), karte: Karte());
            var w = sim.Wirtschaft!;
            Console.WriteLine($"Karte {kartenname}: Häfen {string.Join(", ", w.Schiffahrt.Haefen.Select(h => $"{h.Kennung} am {h.Fluss} {h.Liegeplatz}"))}");
            if (!w.Schiffahrt.IstAktiv) { Console.WriteLine("Kein Schiffshandel auf dieser Karte."); return 1; }
            var typ = mit.Schiffshandel!.Schiffe.Values.First();
            foreach (var stadt in w.Schiffahrt.Staedte)
            {
                Console.WriteLine($"  {stadt.Name} ({stadt.Daten.Richtung}): Rundreise {Schiffahrt.Rundreise(typ, stadt)} Min., " +
                                  $"Route {w.Schiffahrt.RoutenLaenge(w.Schiffahrt.Haefen[0].Kennung, stadt.Daten.IstFlussauf):0} m");
                foreach (var ware in stadt.Daten.Kauft.Keys)
                    Console.WriteLine($"    kauft {ware}: dort {stadt.Ankaufpreis(ware):0.0}, hier Markt {w.Markt[ware].Preis:0.0}, von Hand {w.HandKaufpreis(spieler, ware, 1):0.0}");
                foreach (var ware in stadt.Daten.Verkauft.Keys)
                    Console.WriteLine($"    verkauft {ware}: dort {stadt.Verkaufspreis(ware):0.0}, hier Markt {w.Markt[ware].Preis:0.0}");
            }
            var v = Kalkulation.BesteSchiffsfahrt(w, spieler, typ, 10_000);
            if (v is null) Console.WriteLine("  Kein lohnender Auftrag.");
            else
            {
                var r = v.Rechnung;
                Console.WriteLine($"  Bester Auftrag: {v.Ziel}, hin {string.Join(", ", v.Hinfracht.Select(x => $"{x.Value} {x.Key}"))}, " +
                                  $"zurück {string.Join(", ", v.Rueckfracht.Select(x => $"{x.Value} {x.Key}"))}");
                Console.WriteLine($"    Einkauf {r.Einkauf:0}, Erlös dort {r.ErloesFern:0}, Einkauf dort {r.EinkaufFern:0}, Erlös hier {r.ErloesHeim:0}, " +
                                  $"Zoll {r.Zoll:0}, Geleit {r.Geleit:0}, Risiko {r.Risiko:0} → Gewinn {r.Gewinn:0} in {r.Minuten} Min. ({r.GewinnJeJahr:0} je Jahr ohne Pause)");
            }
        }

        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var ergebnisse = Enumerable.Range(1, startwerte).AsParallel().AsOrdered()
            .Select(s => (Mit: Partie(mit, Karte(), s, jahre), Ohne: Partie(ohne, Karte(), s, jahre)))
            .ToList();

        Console.WriteLine();
        Console.WriteLine($"{startwerte} Startwerte × {jahre} Jahre auf {kartenname} ({uhr.Elapsed.TotalSeconds:0} s):");
        var m = ergebnisse.Select(e => e.Mit).ToList();
        var o = ergebnisse.Select(e => e.Ohne).ToList();
        foreach (var haus in m[0].Vermoegen.Keys)
        {
            var a = m.Average(x => x.Vermoegen[haus]);
            var b = o.Average(x => x.Vermoegen[haus]);
            Console.WriteLine($"  {haus,-22} Vermögen Ø mit {a,10:N0}  ohne {b,10:N0}  ({(a / b - 1) * 100:+0.0;-0.0} %)");
        }
        var schiffsjahre = m.Sum(x => x.Schiffsjahre);
        Console.WriteLine($"  Schiffe gekauft: {m.Sum(x => x.Gekauft)} in {startwerte} Partien (Ø im Jahr {m.Where(x => x.ErstesSchiff > 0).Select(x => (double)x.ErstesSchiff).DefaultIfEmpty(0).Average():0.0} das erste), " +
                          $"Schiffsjahre {schiffsjahre}");
        if (schiffsjahre > 0)
        {
            Console.WriteLine($"  je Schiff und Jahr: Fahrten {m.Sum(x => x.Fahrten) / (double)schiffsjahre:0.00}, Ertrag nach Unterhalt {m.Sum(x => x.Ertrag) / schiffsjahre:N0}");
            Console.WriteLine($"  Piraten: {m.Sum(x => x.Angriffe)} Angriffe, {m.Sum(x => x.Raube)} gelungen ({m.Sum(x => x.Angriffe) / (double)m.Sum(x => x.Fahrten) / 2 * 100:0.0} % je Fahrtstrecke)");
            foreach (var ziel in m.SelectMany(x => x.Ziele).GroupBy(x => x.Key))
                Console.WriteLine($"  Fahrten nach {ziel.Key}: {ziel.Sum(x => x.Value)}");
        }
        var umland = m.Where(x => x.UmlandJahre > 0).ToList();
        if (umland.Count > 0)
            Console.WriteLine($"  Vergleich: Gewinn je Umlandbetrieb der KI und Jahr {umland.Sum(x => x.UmlandGewinn) / umland.Sum(x => x.UmlandJahre):N0}, " +
                              $"je Werkstatt {m.Sum(x => x.WerkstattGewinn) / Math.Max(1, m.Sum(x => x.WerkstattJahre)):N0}");
        return 0;
    }

    sealed record Ergebnis(Dictionary<string, double> Vermoegen, int Gekauft, int ErstesSchiff, int Schiffsjahre, int Fahrten, double Ertrag,
        int Angriffe, int Raube, Dictionary<string, int> Ziele, double UmlandGewinn, int UmlandJahre, double WerkstattGewinn, int WerkstattJahre);

    static Ergebnis Partie(Wirtschaftsdaten daten, Stadtkarte karte, int startwert, int jahre)
    {
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert * 101, Siegbedingung = Siegbedingung.Endlos },
            daten, new NeuePartie(Rivalen: karte.Startbelegung.Haeuser.Count), karte: karte);
        var w = sim.Wirtschaft!;
        int angriffe = 0, raube = 0, gekauft = 0, erstes = 0, schiffsjahre = 0, umlandJahre = 0, werkstattJahre = 0;
        double ertrag = 0, umland = 0, werkstatt = 0;
        var ziele = new Dictionary<string, int>();
        w.Meldung += m =>
        {
            if (m.Contains("Flusspiraten überfallen")) { angriffe++; raube++; }
            else if (m.Contains("Flusspiraten greifen")) angriffe++;
            if (m.Contains(": kauft ein Flussschiff")) gekauft++;
            if (m.Split(": schickt ") is [_, var rest] && rest.Split(" nach ") is [_, var z, ..])
            {
                var ziel = z.Split(':')[0];
                ziele[ziel] = ziele.GetValueOrDefault(ziel) + 1;
            }
        };
        for (var j = 1; j <= jahre; j++)
        {
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick();
            if (erstes == 0 && w.Schiffahrt.Schiffe.Count > 0) erstes = j;
            foreach (var s in w.Schiffahrt.Schiffe.Where(s => s.ErtragVorjahr is not null && s.Besitzer.IstAktiv))
            {
                schiffsjahre++;
                ertrag += s.ErtragVorjahr!.Value;
            }
            foreach (var b in w.Betriebe.Where(b => w.Rivalen.Any(r => r.Konto == b.Konto) && b.Vorjahr is { Ganzjaehrig: true } && b.Belegt > 0))
            {
                if (b.Ort == Arbeitsort.Umland) { umland += b.Vorjahr!.Gewinn; umlandJahre++; }
                else { werkstatt += b.Vorjahr!.Gewinn; werkstattJahre++; }
            }
        }
        return new Ergebnis(w.Konten.ToDictionary(k => k.Inhaber, k => k.Reinvermoegen), gekauft, erstes, schiffsjahre,
            w.Schiffahrt.Schiffe.Sum(s => s.Fahrten), ertrag, angriffe, raube, ziele, umland, umlandJahre, werkstatt, werkstattJahre);
    }
}
