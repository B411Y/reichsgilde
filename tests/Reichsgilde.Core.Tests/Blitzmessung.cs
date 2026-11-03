using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Blitzmessung – Schaden nach Blitzschlag und Übersprung (Messung, kein Test; Blitzschaden 3.11.2026)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen blitz [Jahre] [Startwerte]
//                [--karten=greybox,koeln] [--berufe=Schmied,Wirt] [--parallel=n]
//
//  Je Karte, Beruf und Startwert zwei Partien mit allen KI-Häusern der Karte (Spieler KI-geführt, ein Betrieb): „mit“ (die Daten, wie sie sind)
//  und „ohne“ (Blitzschaden wie bis Datenversion 68: nur Stillstand und fehlendes Ansehen, kein Sprung). Das Wetter und die Einschläge sind
//  in beiden gleich (eigener Zufall des Gewitters); der Unterschied im Reinvermögen ist die Wirkung des Schadens.
//  Gemessen je Partie: Einschläge, Sprünge, davon in Häuser des Spielers bzw. der KI, Reparatur und verbrannte Stück; Reinvermögen am Ende.
// ═════════════════════════════════════════════════════════════════════════════

static class Blitzmessung
{
    sealed class Zaehler
    {
        public int Einschlaege, Spruenge, Spieler, Ki;
        public double Reparatur;
        public int Verbrannt;
        public bool Angemeldet;
    }

    public static int Erstellen(string[] args)
    {
        string? Schalter(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1];
        var worte = args.Where(a => !a.StartsWith("--")).ToList();
        var jahre = worte.Count > 0 ? int.Parse(worte[0]) : 20;
        var startwerte = worte.Count > 1 ? int.Parse(worte[1]) : 8;
        var karten = (Schalter("karten") ?? "greybox,koeln").Split(',');
        var berufe = (Schalter("berufe") ?? "Schmied,Wirt").Split(',');
        var parallel = int.Parse(Schalter("parallel") ?? "2");
        var quellen = new Dictionary<string, Messquellen>(StringComparer.Ordinal) { ["mit"] = new Messquellen(), ["ohne"] = new Messquellen(anpassen: OhneSchaden) };

        var laeufe = new List<(string Variante, Messaufbau A)>();
        foreach (var karte in karten)
            foreach (var beruf in berufe)
                for (var s = 1; s <= startwerte; s++)
                    foreach (var v in quellen.Keys)
                        laeufe.Add((v, new Messaufbau(karte, s * 101, jahre, Beruf: beruf)));
        Console.Error.WriteLine($"Blitzmessung: {laeufe.Count} Partien zu {jahre} Jahren, {parallel} parallel …");
        var ergebnisse = new ConcurrentDictionary<(string, Messaufbau), (Partieergebnis P, Zaehler Z)>();
        var fertig = 0;
        Parallel.ForEach(laeufe, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel) }, l =>
        {
            var z = new Zaehler();
            Partieergebnis r;
            try { r = Messpartie.Spielen(l.A, quellen[l.Variante], w => Anmelden(w, z)); }
            catch (Exception ex) { r = new Partieergebnis { Aufbau = l.A, Fehler = ex.ToString() }; }
            ergebnisse[l] = (r, z);
            var n = Interlocked.Increment(ref fertig);
            if (n % 4 == 0 || n == laeufe.Count) Console.Error.WriteLine($"  {n}/{laeufe.Count}");
        });
        foreach (var (a, (r, _)) in ergebnisse.Where(x => x.Value.P.Fehler is not null).Take(5)) Console.Error.WriteLine($"FEHLER in {a}: {r.Fehler}");

        Console.WriteLine($"Datenversion {Wirtschaftsdaten.AusJson(quellen["mit"].Wirtschaft).Version}, {jahre} Jahre, {startwerte} Startwerte, main {Balancingbericht.Git("rev-parse --short origin/main")}, Stand {Balancingbericht.Git("rev-parse --short HEAD")}");
        Console.WriteLine("Karte | Beruf | Variante | Einschläge/Partie | Sprünge/Partie | Treffer Spieler/Partie | Treffer KI/Partie | Reparatur/Partie | verbrannt Stück/Partie | Spieler Rein J" + jahre + " | KI Rein J" + jahre);
        foreach (var g in ergebnisse.Where(x => x.Value.P.Fehler is null)
                     .GroupBy(x => (x.Key.Item2.Karte, x.Key.Item2.Beruf, Variante: x.Key.Item1))
                     .OrderBy(x => x.Key.Karte, StringComparer.Ordinal).ThenBy(x => x.Key.Beruf, StringComparer.Ordinal).ThenBy(x => x.Key.Variante, StringComparer.Ordinal))
        {
            var liste = g.Select(x => x.Value).ToList();
            double Je(Func<Zaehler, double> f) => liste.Average(x => f(x.Z));
            var spieler = liste.Average(x => x.P.Spieler.Im(jahre).Rein);
            var ki = liste.SelectMany(x => x.P.Ki.Select(h => h.Im(jahre).Rein)).DefaultIfEmpty(0).Average();
            Console.WriteLine($"{g.Key.Karte} | {g.Key.Beruf} | {g.Key.Variante} | {Je(z => z.Einschlaege):0.00} | {Je(z => z.Spruenge):0.00} | {Je(z => z.Spieler):0.00} | " +
                              $"{Je(z => z.Ki):0.00} | {Je(z => z.Reparatur):0} | {Je(z => z.Verbrannt):0.0} | {spieler:0} | {ki:0}");
        }
        return ergebnisse.Values.Any(x => x.P.Fehler is not null) ? 1 : 0;
    }

    /// <summary>Blitzschaden wie bis Datenversion 68: ohne Anteile, Wächterfaktor und Sprung, Reparatur 0.</summary>
    static string OhneSchaden(string json)
    {
        var knoten = JsonNode.Parse(json)!.AsObject();
        var blitz = knoten["blitz"]!.AsObject();
        foreach (var feld in new[] { "reparaturAnteil", "lagerAnteil", "waechterFaktor", "wohnhausAnteil", "sprung" }) blitz.Remove(feld);
        blitz["reparatur"] = 0;
        blitz["wohnhausReparatur"] = 0;
        return knoten.ToJsonString();
    }

    static readonly Regex Reparatur = new(@"Reparatur ([\d\.]+)", RegexOptions.CultureInvariant);
    static readonly Regex Stueck = new(@"(\d+) [^,\.]+", RegexOptions.CultureInvariant);

    /// <summary>Zur ersten vollen Stunde: die Meldungen „Blitzschlag!“ und „Funkenflug!“ zählen (Ziel aus dem jüngsten Einschlag).</summary>
    static void Anmelden(Stadtwirtschaft w, Zaehler z)
    {
        if (z.Angemeldet) return;
        z.Angemeldet = true;
        w.Meldung += m =>
        {
            var blitz = m.StartsWith("Blitzschlag! ", StringComparison.Ordinal);
            if (!blitz && !m.StartsWith("Funkenflug! ", StringComparison.Ordinal)) return;
            if (blitz) z.Einschlaege++; else z.Spruenge++;
            if (w.Blitz.Einschlaege[^1].Ziel.Besitzer is { } k) { if (k == w.Konten[0]) z.Spieler++; else z.Ki++; }
            foreach (Match r in Reparatur.Matches(m)) z.Reparatur += double.Parse(r.Groups[1].Value.Replace(".", ""), System.Globalization.CultureInfo.InvariantCulture);
            var i = m.IndexOf("verbrannt: ", StringComparison.Ordinal);
            if (i >= 0)
                foreach (Match s in Stueck.Matches(m[(i + 11)..])) z.Verbrannt += int.Parse(s.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        };
    }
}
