using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Buergermessung – mehr Bürger in großen Städten (Messung, kein Test; Spiel-KI, 5.11.2026)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen buerger [Jahre] [Startwerte]
//                [--karten=greybox,koeln] [--aufbauten=Schmied,Wirt,Ausbau] [--varianten=alt,neu,alt26] [--einzeln] [--parallel=n]
//
//  Je Karte, Aufbau (Schmied, Wirt: ein Betrieb; Ausbau: Schmied, geführt wie ein KI-Haus) und Startwert zwei Partien mit allen KI-Häusern:
//  „alt“ (die Bürger-KI wie bis Datenversion 72: ohne „jeHausplatz“ und „tagesplan“) und „neu“ (die Daten, wie sie sind).
//  Je Uhrzeit dazu die Orte im Einzelnen (Heim, Markt, …, unterwegs), gezählt zur halben Stunde.
//  Gemessen: Reinvermögen des Spielers und der KI am Ende, Jahr bis zum Reinvermögen des Bürgers, dazu die Bürger-KI: Einwohner, wer zur halben
//  Stunde in der Stadt zu sehen ist (Markt, Gasse, Tor, unterwegs in der Stadt) bzw. im Umland, Marktfaktor, ohne Schankplatz, Rechenzeit.
// ═════════════════════════════════════════════════════════════════════════════

static class Buergermessung
{
    /// <summary>Die Felder, um die die neue Bürger-KI die Daten ergänzt – ohne sie rechnet sie wie vorher.</summary>
    public static readonly string[] NeueFelder = { "jeHausplatz", "tagesplan" };

    /// <summary>Die Uhrzeiten, zu denen gezählt wird (jeweils zur halben Stunde).</summary>
    static readonly int[] Stunden = { 8, 10, 12, 15, 18, 20, 22 };

    sealed class Zaehler
    {
        public int Einwohner, Tage;
        public readonly double[] Stadt = new double[24], Umland = new double[24];
        public readonly double[,] Orte = new double[24, 9];
        public double Marktfaktor;
        public readonly Dictionary<string, (double Summe, int Anzahl)> Warenkorb = new(StringComparer.Ordinal);
        public int Marktstunden, OhnePlatz;
        public double Sekunden;
    }

    public static int Erstellen(string[] args)
    {
        string? Schalter(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1];
        var worte = args.Where(a => !a.StartsWith("--")).ToList();
        var jahre = worte.Count > 0 ? int.Parse(worte[0]) : 20;
        var startwerte = worte.Count > 1 ? int.Parse(worte[1]) : 8;
        var karten = (Schalter("karten") ?? "greybox,koeln").Split(',');
        var parallel = int.Parse(Schalter("parallel") ?? "2");
        var alle = new Dictionary<string, Func<Messquellen>>(StringComparer.Ordinal)
        {
            ["alt"] = () => new Messquellen(anpassen: Alt),
            ["neu"] = () => new Messquellen(),
            // Gegenprobe für das Rauschen: wie „alt“, aber 26 statt 25 Einwohner je Haus – ein anderer Lauf der Bürger-KI bei fast gleicher Stadt
            ["alt26"] = () => new Messquellen(anpassen: json => Alt(json).Replace("\"jeHaus\":25", "\"jeHaus\":26")),
            // Diagnose: doppelt so viele Einwohner nach der alten Regel (ohne Tagesplan) bzw. nur die Haushalte je Hausplatz (ohne Tagesplan)
            ["alt50"] = () => new Messquellen(anpassen: json => Alt(json).Replace("\"jeHaus\":25", "\"jeHaus\":50")),
            ["ohneplan"] = () => new Messquellen(anpassen: json => { var k = JsonNode.Parse(json)!.AsObject(); k["buerger"]!.AsObject().Remove("tagesplan"); return k.ToJsonString(); }),
        };
        var quellen = (Schalter("varianten") ?? "alt,neu").Split(',').ToDictionary(v => v, v => alle[v](), StringComparer.Ordinal);
        var einzeln = args.Contains("--einzeln");
        var aufbauten = new (string Name, string Beruf, Bezugsart Bezug)[] { ("Schmied", "Schmied", Bezugsart.EinBetrieb), ("Wirt", "Wirt", Bezugsart.EinBetrieb), ("Ausbau", "Schmied", Bezugsart.WieKi) }
            .Where(a => Schalter("aufbauten") is not { } nur || nur.Split(',').Contains(a.Name)).ToArray();

        var laeufe = new List<(string Variante, string Name, Messaufbau A)>();
        foreach (var karte in karten)
            foreach (var (name, beruf, bezug) in aufbauten)
                for (var s = 1; s <= startwerte; s++)
                    foreach (var v in quellen.Keys)
                        laeufe.Add((v, name, new Messaufbau(karte, s * 101, jahre, Beruf: beruf, Bezug: bezug)));
        Console.Error.WriteLine($"Buergermessung: {laeufe.Count} Partien zu {jahre} Jahren, {parallel} parallel …");
        var ergebnisse = new ConcurrentDictionary<(string, string, Messaufbau), (Partieergebnis P, Zaehler Z)>();
        var fertig = 0;
        Parallel.ForEach(laeufe, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel) }, l =>
        {
            var z = new Zaehler();
            Partieergebnis r;
            var uhr = Stopwatch.StartNew();
            try { r = Messpartie.Spielen(l.A, quellen[l.Variante], jeStunde: w => Stunde(w, z), jeMinute: w => Minute(w, z)); }
            catch (Exception ex) { r = new Partieergebnis { Aufbau = l.A, Fehler = ex.ToString() }; }
            z.Sekunden = uhr.Elapsed.TotalSeconds;
            ergebnisse[l] = (r, z);
            var n = Interlocked.Increment(ref fertig);
            if (n % 4 == 0 || n == laeufe.Count) Console.Error.WriteLine($"  {n}/{laeufe.Count}");
        });
        foreach (var (a, (r, _)) in ergebnisse.Where(x => x.Value.P.Fehler is not null).Take(5)) Console.Error.WriteLine($"FEHLER in {a}: {r.Fehler}");

        Console.WriteLine($"Datenversion {Wirtschaftsdaten.AusJson(quellen.Values.First().Wirtschaft).Version}, {jahre} Jahre, {startwerte} Startwerte, main {Balancingbericht.Git("rev-parse --short origin/main")}, Stand {Balancingbericht.Git("rev-parse --short HEAD")}");
        Console.WriteLine();
        Console.WriteLine($"Karte | Aufbau | Variante | Spieler Rein J{jahre} (± Fehler) | KI Rein J{jahre} | Bürger (Rein 10.000) Ø Jahr | im Ziel 8–12 | Marktfaktor Ø | ohne Schankplatz/Tag | Sekunden/Partie");
        var gruppen = ergebnisse.Where(x => x.Value.P.Fehler is null)
            .GroupBy(x => (x.Key.Item3.Karte, Aufbau: x.Key.Item2, Variante: x.Key.Item1))
            .OrderBy(x => x.Key.Karte, StringComparer.Ordinal).ThenBy(x => x.Key.Aufbau, StringComparer.Ordinal).ThenBy(x => x.Key.Variante, StringComparer.Ordinal).ToList();
        foreach (var g in gruppen)
        {
            var liste = g.Select(x => x.Value).ToList();
            var rein = liste.Select(x => x.P.Spieler.Im(jahre).Rein).ToList();
            var fehler = rein.Count > 1 ? Math.Sqrt(rein.Sum(x => (x - rein.Average()) * (x - rein.Average())) / (rein.Count - 1) / rein.Count) : 0;
            var ki = liste.SelectMany(x => x.P.Ki.Select(h => h.Im(jahre).Rein)).DefaultIfEmpty(0).Average();
            var buerger = liste.Select(x => x.P.Spieler.BuergerReinJahr).ToList();
            var ziel = buerger.Count(b => b is >= 8 and <= 12);
            var bj = buerger.Where(b => b is not null).Select(b => (double)b!.Value).DefaultIfEmpty(double.NaN).Average();
            var mf = liste.Sum(x => x.Z.Marktfaktor) / Math.Max(1, liste.Sum(x => x.Z.Marktstunden));
            var op = liste.Sum(x => (double)x.Z.OhnePlatz) / Math.Max(1, liste.Sum(x => x.Z.Tage));
            Console.WriteLine($"{g.Key.Karte} | {g.Key.Aufbau} | {g.Key.Variante} | {rein.Average():0} (± {fehler:0}) | {ki:0} | {bj:0.0} ({buerger.Count(b => b is not null)}/{buerger.Count}) | {ziel}/{buerger.Count} | " +
                              $"{mf:0.000} | {op:0.0} | {liste.Average(x => x.Z.Sekunden):0.0}");
        }
        Console.WriteLine();
        Console.WriteLine("Karte | Variante | Warenkorbfaktor Ø je Warengruppe (9–20 Uhr)");
        foreach (var g in ergebnisse.Where(x => x.Value.P.Fehler is null).GroupBy(x => (x.Key.Item3.Karte, Variante: x.Key.Item1))
                     .OrderBy(x => x.Key.Karte, StringComparer.Ordinal).ThenBy(x => x.Key.Variante, StringComparer.Ordinal))
        {
            var korbGruppen = g.SelectMany(x => x.Value.Z.Warenkorb.Keys).Distinct().Order(StringComparer.Ordinal);
            Console.WriteLine($"{g.Key.Karte} | {g.Key.Variante} | " + string.Join(", ", korbGruppen.Select(gr =>
                $"{gr} {g.Sum(x => x.Value.Z.Warenkorb.GetValueOrDefault(gr).Summe) / Math.Max(1, g.Sum(x => x.Value.Z.Warenkorb.GetValueOrDefault(gr).Anzahl)):0.000}")));
        }
        if (einzeln)
        {
            Console.WriteLine();
            Console.WriteLine("Karte | Aufbau | Startwert | " + string.Join(" | ", quellen.Keys.Select(v => $"Spieler Rein {v}")));
            foreach (var g in ergebnisse.Where(x => x.Value.P.Fehler is null).GroupBy(x => (x.Key.Item3.Karte, Aufbau: x.Key.Item2, x.Key.Item3.Startwert))
                         .OrderBy(x => x.Key.Karte, StringComparer.Ordinal).ThenBy(x => x.Key.Aufbau, StringComparer.Ordinal).ThenBy(x => x.Key.Startwert))
                Console.WriteLine($"{g.Key.Karte} | {g.Key.Aufbau} | {g.Key.Startwert} | " + string.Join(" | ", quellen.Keys.Select(v =>
                    g.FirstOrDefault(x => x.Key.Item1 == v).Value.P is { } p ? p.Spieler.Im(jahre).Rein.ToString("0") : "–")));
        }
        Console.WriteLine();
        Console.WriteLine("Karte | Variante | Einwohner | in der Stadt zu sehen (Markt, Gasse, Tor, unterwegs) je Uhrzeit " + string.Join(" / ", Stunden.Select(s => s + " Uhr")) + " | im Umland 12 Uhr");
        foreach (var g in ergebnisse.Where(x => x.Value.P.Fehler is null).GroupBy(x => (x.Key.Item3.Karte, Variante: x.Key.Item1))
                     .OrderBy(x => x.Key.Karte, StringComparer.Ordinal).ThenBy(x => x.Key.Variante, StringComparer.Ordinal))
        {
            var z = g.Select(x => x.Value.Z).ToList();
            var tage = Math.Max(1, z.Sum(x => x.Tage));
            Console.WriteLine($"{g.Key.Karte} | {g.Key.Variante} | {z.Average(x => x.Einwohner):0} | " +
                              string.Join(" / ", Stunden.Select(s => (z.Sum(x => x.Stadt[s]) / tage).ToString("0"))) + $" | {z.Sum(x => x.Umland[12]) / tage:0}");
            foreach (var s in Stunden)
                Console.WriteLine($"    {s} Uhr: " + string.Join(", ", Enumerable.Range(0, 9).Select(o => $"{(o == 8 ? "unterwegs" : ((Stadtort)o).ToString())} {z.Sum(x => x.Orte[s, o]) / tage:0}")));
        }
        return ergebnisse.Values.Any(x => x.P.Fehler is not null) ? 1 : 0;
    }

    /// <summary>Die Bürger-KI wie vor diesem PR: ohne die neuen Felder im Abschnitt „buerger“.</summary>
    public static string Alt(string json)
    {
        var knoten = JsonNode.Parse(json)!.AsObject();
        var buerger = knoten["buerger"]!.AsObject();
        foreach (var feld in NeueFelder) buerger.Remove(feld);
        return knoten.ToJsonString();
    }

    static void Stunde(Stadtwirtschaft w, Zaehler z)
    {
        var e = w.Einwohnerschaft;
        if (!e.IstAktiv || e.Einwohner.Count == 0) return;
        var stunde = w.Uhr.UhrzeitInMinuten / 60;
        if (stunde == 5) { z.Tage++; z.Einwohner = e.Einwohner.Count; }
        if (stunde is >= 9 and <= 20)
        {
            z.Marktfaktor += e.Marktfaktor;
            z.Marktstunden++;
            foreach (var s in w.Markt.Staende)
                if (e.Warengruppe(s.Ware) is { } gruppe)
                {
                    var (summe, anzahl) = z.Warenkorb.GetValueOrDefault(gruppe);
                    z.Warenkorb[gruppe] = (summe + e.Warenkorbfaktor(s.Ware), anzahl + 1);
                }
        }
        if (stunde is >= 13 and <= 22) z.OhnePlatz += e.OhneSchankplatz;
    }

    static void Minute(Stadtwirtschaft w, Zaehler z)
    {
        if (w.Uhr.UhrzeitInMinuten % 60 != 30 || !w.Einwohnerschaft.IstAktiv) return;
        var stunde = w.Uhr.UhrzeitInMinuten / 60;
        var jetzt = w.Uhr.VergangeneMinuten;
        foreach (var e in w.Einwohnerschaft.Einwohner)
        {
            if (e.Wo(jetzt) is { } a)
            {
                z.Orte[stunde, (int)a.Ort]++;
                if (a.Ort is Stadtort.Markt or Stadtort.Gasse or Stadtort.Tor) z.Stadt[stunde]++;
                else if (a.Ort == Stadtort.Umland) z.Umland[stunde]++;
            }
            else if (e.Weg is { } weg)
            {
                z.Orte[stunde, 8]++;
                if (weg.Von.Ort != Stadtort.Umland && weg.Nach.Ort != Stadtort.Umland) z.Stadt[stunde]++;
                else z.Umland[stunde]++;
            }
        }
    }
}
