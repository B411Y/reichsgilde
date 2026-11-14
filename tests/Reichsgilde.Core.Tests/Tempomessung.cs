using System.Diagnostics;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

// ═════════════════════════════════════════════════════════════════════════════
//  Tempomessung – Rechenzeit einer Partie (Messung, kein Test; Auftrag „Spiel und Tests schneller“, 14.11.2026)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen tempo [Jahre] [Startwerte] [--karte=koeln]
//
//  Spielt je Startwert eine Partie mit allen KI-Häusern der Karte (wie messen bauorte) Minute für Minute, ohne Befehle des Menschen, und misst
//  die Rechenzeit (Stopwatch, ein Faden) je Jahr und zusammen. Am Ende eine Prüfsumme über Vermögen und Betriebe aller Häuser – damit sieht man,
//  dass ein schnellerer Kern dieselbe Partie spielt. Für ein Profil der heißen Stellen: denselben Aufruf unter dotnet-trace laufen lassen.
// ═════════════════════════════════════════════════════════════════════════════

static class Tempomessung
{
    public static int Erstellen(string[] args)
    {
        string? Schalter(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1];
        var zahlen = args.Where(a => !a.StartsWith("--")).ToArray();
        var jahre = zahlen.Length > 0 ? int.Parse(zahlen[0]) : 3;
        var startwerte = zahlen.Length > 1 ? int.Parse(zahlen[1]) : 1;
        var kennung = Schalter("karte") ?? "koeln";
        var gesamt = Stopwatch.StartNew();
        for (var s = 1; s <= startwerte; s++)
        {
            var daten = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
            var haeuser = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte(kennung)))!["start"]?["haeuser"]?.AsArray().Count ?? 1;
            var aufbau = Stopwatch.StartNew();
            var (sim, _) = Partieaufbau.Erstellen(
                Partieeinstellungen.Standard with { Zufallsstartwert = s * 101, Siegbedingung = Siegbedingung.Endlos },
                daten, new NeuePartie(Rivalen: haeuser), karte: Stadtkarte.AusDatei(Datenpfad.Karte(kennung)));
            var w = sim.Wirtschaft!;
            Console.WriteLine($"{kennung}, Startwert {s * 101}: Aufbau {aufbau.ElapsedMilliseconds} ms");
            for (var j = 0; j < jahre; j++)
            {
                var uhr = Stopwatch.StartNew();
                for (var t = 0; t < Spielzeit.MinutenProTag; t++) sim.Tick();
                Console.WriteLine($"  Jahr {j + 1}: {uhr.ElapsedMilliseconds} ms");
            }
            var summe = string.Join(";", w.Konten.Select(k => $"{k.Inhaber}={k.Vermoegen.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}")) + "|" + string.Join(";", w.Betriebe.Select(b => b.Name));
            Console.WriteLine($"  Prüfsumme {Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(summe)))[..16]}");
        }
        Console.WriteLine($"Zusammen {gesamt.ElapsedMilliseconds} ms");
        return 0;
    }
}
