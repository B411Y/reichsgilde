using System.Collections.Concurrent;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Lebenswegmessung – welchen Pfad und welche Knoten der KI-geführte Spieler nimmt (Messung, kein Test; Lebensweise 3a, 8.11.2026)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen lebensweg [Jahre] [Startwerte]
//                [--berufe=Dieb,Parfumeur,Schmied] [--voelker=Mensch,Halbling] [--karte=greybox] [--parallel=n]
//
//  Je Beruf, Volk und Startwert eine Partie wie im Balancing-Bericht (Spieler KI-geführt, ein Betrieb). Am Ende jedes Jahres: der aktive
//  Pfad des Oberhaupts; am Ende: die Knoten aller Personen des Hauses mit Lebensweg und der Zuwachs (Reinvermögen − Startvermögen).
//  Ausgabe je Beruf und Volk: Anteil der Oberhaupt-Jahre je Pfad, Knoten am Ende (Mittel), die häufigsten Knoten, Zuwachs je Pfad am Ende.
// ═════════════════════════════════════════════════════════════════════════════

static class Lebenswegmessung
{
    sealed class Zaehler
    {
        public readonly List<Talent> PfadJeJahr = new();
        public readonly List<string> Knoten = new();
        public Talent? PfadAmEnde;
    }

    public static int Erstellen(string[] args)
    {
        string? Schalter(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1];
        var worte = args.Where(a => !a.StartsWith("--")).ToList();
        var jahre = worte.Count > 0 ? int.Parse(worte[0]) : 20;
        var startwerte = worte.Count > 1 ? int.Parse(worte[1]) : 10;
        var berufe = (Schalter("berufe") ?? "Dieb,Parfumeur,Schmied").Split(',');
        var voelker = (Schalter("voelker") ?? "Mensch,Halbling").Split(',').Select(Enum.Parse<Volk>).ToArray();
        var karte = Schalter("karte") ?? "greybox";
        var parallel = int.Parse(Schalter("parallel") ?? Environment.ProcessorCount.ToString());
        var q = new Messquellen();
        var daten = Wirtschaftsdaten.AusJson(q.Wirtschaft);

        var laeufe = (from b in berufe from v in voelker from s in Enumerable.Range(1, startwerte)
                      select new Messaufbau(karte, s * 101, jahre, Beruf: b, Volk: v)).ToList();
        Console.Error.WriteLine($"Lebenswegmessung: {laeufe.Count} Partien zu {jahre} Jahren, {parallel} parallel …");
        var ergebnisse = new ConcurrentDictionary<Messaufbau, (Partieergebnis P, Zaehler Z)>();
        var fertig = 0;
        Parallel.ForEach(laeufe, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel) }, a =>
        {
            var z = new Zaehler();
            Partieergebnis r;
            try { r = Messpartie.Spielen(a, q, jahresende: (w, jahr) => Jahresende(w, z, jahr == jahre)); }
            catch (Exception ex) { r = new Partieergebnis { Aufbau = a, Fehler = ex.ToString() }; }
            ergebnisse[a] = (r, z);
            var n = Interlocked.Increment(ref fertig);
            if (n % 10 == 0 || n == laeufe.Count) Console.Error.WriteLine($"  {n}/{laeufe.Count}");
        });
        foreach (var (a, (r, _)) in ergebnisse.Where(x => x.Value.P.Fehler is not null).Take(5)) Console.Error.WriteLine($"FEHLER in {a}: {r.Fehler}");

        Console.WriteLine($"Datenversion {daten.Version}, {jahre} Jahre, {startwerte} Startwerte, Karte {karte}, main {Balancingbericht.Git("rev-parse --short origin/main")}, " +
                          $"Stand {Balancingbericht.Git("rev-parse --short HEAD")}");
        var start = daten.Allgemein.Startvermoegen;
        foreach (var b in berufe)
            foreach (var v in voelker)
            {
                var liste = ergebnisse.Where(x => x.Key.Beruf == b && x.Key.Volk == v && x.Value.P.Fehler is null).Select(x => x.Value).ToList();
                if (liste.Count == 0) continue;
                var zuwachs = liste.Average(x => x.P.Spieler.Im(jahre).Rein - start);
                var jahreGesamt = liste.Sum(x => x.Z.PfadJeJahr.Count);
                var anteile = string.Join(", ", liste.SelectMany(x => x.Z.PfadJeJahr).GroupBy(t => t).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                    .Select(g => $"{g.Key} {100.0 * g.Count() / Math.Max(1, jahreGesamt):0} %"));
                var knoten = string.Join(", ", liste.SelectMany(x => x.Z.Knoten).GroupBy(k => k, StringComparer.Ordinal)
                    .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(12).Select(g => $"{g.Key} {g.Count()}"));
                var jePfad = string.Join(", ", liste.GroupBy(x => x.Z.PfadAmEnde).OrderBy(g => g.Key)
                    .Select(g => $"{g.Key?.ToString() ?? "–"} n={g.Count()} {g.Average(x => x.P.Spieler.Im(jahre).Rein - start):0}"));
                var umsatz = liste.Average(x => x.P.Spieler.Jahre.Average(j => j.Umsatz));
                var gewinn = liste.Average(x => x.P.Spieler.Jahre.Average(j => j.Gewinn));
                Console.WriteLine($"{b} {v}: Zuwachs {zuwachs:0} (n={liste.Count}), Umsatz {umsatz:0} und Betriebsgewinn {gewinn:0} je Jahr; Oberhaupt-Jahre je Pfad: {anteile}");
                Console.WriteLine($"    Knoten am Ende je Partie {liste.Average(x => x.Z.Knoten.Count):0.0}: {knoten}");
                Console.WriteLine($"    Zuwachs nach Pfad am Ende: {jePfad}");
                var sonder = liste.SelectMany(x => x.P.Spieler.Jahre.SelectMany(j => j.Sonder)).GroupBy(s => s.Key, StringComparer.Ordinal)
                    .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Sum(s => s.Value) / liste.Count / jahre:0}/Jahr");
                if (sonder.Any()) Console.WriteLine($"    ohne Ware: {string.Join(", ", sonder)}");
            }
        return ergebnisse.Values.Any(x => x.P.Fehler is not null) ? 1 : 0;
    }

    private static void Jahresende(Stadtwirtschaft w, Zaehler z, bool letztes)
    {
        var fb = w.Familien;
        if (!fb.HatLebensweise || fb.Von(w.Konten[0]) is not { } f) return;
        z.PfadJeJahr.Add(fb.AktiverPfad(f.Oberhaupt));
        if (!letztes) return;
        z.PfadAmEnde = fb.AktiverPfad(f.Oberhaupt);
        var personen = new List<Person> { f.Oberhaupt };
        if (fb.Erbe(f) is { } erbe && !personen.Contains(erbe)) personen.Add(erbe);
        if (f.Ehepartner is { } e && !personen.Contains(e)) personen.Add(e);
        foreach (var p in personen) z.Knoten.AddRange(p.Lebensweg.Knoten);
    }
}
