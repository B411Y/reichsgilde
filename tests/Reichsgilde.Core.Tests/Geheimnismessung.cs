using System.Collections.Concurrent;
using Reichsgilde.Core;
using Reichsgilde.Core.Geheimnisse;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Geheimnismessung – wie oft Geheimnisse entstehen, entdeckt und genutzt werden (Messung, kein Test; Charaktersystem Teil 3c, 9.11.2026)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen geheimnisse [Jahre] [Startwerte]
//                [--berufe=Schmied,Wirt,Dieb,Parfumeur] [--karte=greybox] [--parallel=n]
//
//  Je Beruf und Startwert eine Partie wie im Balancing-Bericht (alle Häuser KI-geführt). Jede volle Stunde ein Blick auf die Geheimnisse:
//  neue (nach Art, bei Häusern oder Ratsherren), neues Wissen (nach Quelle), öffentlich gewordene. Dazu die Meldungen: Entscheidungen der KI
//  (Anzeige, Erpresserbrief, Ratsstimme, Bloßstellen, Spitzel auf Ratsherren), gezahlte Briefe und Urteile. Ausgabe je Partie (Mittel).
// ═════════════════════════════════════════════════════════════════════════════

static class Geheimnismessung
{
    sealed class Zaehler
    {
        public bool Angemeldet;
        public readonly HashSet<int> Gesehen = new();
        public readonly HashSet<(string, int)> Gewusst = new();
        public readonly HashSet<int> Oeffentlich = new();
        public readonly SortedDictionary<string, int> Zahl = new(StringComparer.Ordinal);
        public void Plus(string was, int n = 1) => Zahl[was] = Zahl.GetValueOrDefault(was) + n;
    }

    public static int Erstellen(string[] args)
    {
        string? Schalter(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1];
        var worte = args.Where(a => !a.StartsWith("--")).ToList();
        var jahre = worte.Count > 0 ? int.Parse(worte[0]) : 20;
        var startwerte = worte.Count > 1 ? int.Parse(worte[1]) : 10;
        var berufe = (Schalter("berufe") ?? "Schmied,Wirt,Dieb,Parfumeur").Split(',');
        var karte = Schalter("karte") ?? "greybox";
        var parallel = int.Parse(Schalter("parallel") ?? Environment.ProcessorCount.ToString());
        var q = new Messquellen();
        var daten = Wirtschaftsdaten.AusJson(q.Wirtschaft);

        var laeufe = (from b in berufe from s in Enumerable.Range(1, startwerte) select new Messaufbau(karte, s * 101, jahre, Beruf: b)).ToList();
        Console.Error.WriteLine($"Geheimnismessung: {laeufe.Count} Partien zu {jahre} Jahren, {parallel} parallel …");
        var ergebnisse = new ConcurrentDictionary<Messaufbau, (Partieergebnis P, Zaehler Z)>();
        var fertig = 0;
        Parallel.ForEach(laeufe, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel) }, a =>
        {
            var z = new Zaehler();
            Partieergebnis r;
            try { r = Messpartie.Spielen(a, q, jeStunde: w => Stunde(w, z)); }
            catch (Exception ex) { r = new Partieergebnis { Aufbau = a, Fehler = ex.ToString() }; }
            ergebnisse[a] = (r, z);
            var n = Interlocked.Increment(ref fertig);
            if (n % 10 == 0 || n == laeufe.Count) Console.Error.WriteLine($"  {n}/{laeufe.Count}");
        });
        foreach (var (a, (r, _)) in ergebnisse.Where(x => x.Value.P.Fehler is not null).Take(5)) Console.Error.WriteLine($"FEHLER in {a}: {r.Fehler}");

        Console.WriteLine($"Datenversion {daten.Version}, {jahre} Jahre, {startwerte} Startwerte, Karte {karte}, " +
                          $"main {Balancingbericht.Git("rev-parse --short origin/main")}, Stand {Balancingbericht.Git("rev-parse --short HEAD")}");
        var alle = ergebnisse.Values.Where(x => x.P.Fehler is null).Select(x => x.Z).ToList();
        if (alle.Count == 0) return 1;
        var schluessel = alle.SelectMany(z => z.Zahl.Keys).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Console.WriteLine($"Je Partie ({alle.Count} Partien, {jahre} Jahre; Mittel, in Klammern Anteil der Partien mit mindestens einem):");
        foreach (var k in schluessel)
        {
            var werte = alle.Select(z => z.Zahl.GetValueOrDefault(k)).ToList();
            Console.WriteLine($"  {k,-46} {werte.Average(),7:0.00}   ({100.0 * werte.Count(x => x > 0) / werte.Count,3:0} %)");
        }
        foreach (var b in berufe)
        {
            var teil = ergebnisse.Where(x => x.Key.Beruf == b && x.Value.P.Fehler is null).Select(x => x.Value.Z).ToList();
            if (teil.Count == 0) continue;
            string M(string k) => $"{teil.Average(z => z.Zahl.GetValueOrDefault(k)):0.00}";
            Console.WriteLine($"  Partien mit {b}: Geheimnisse {M("Entstanden (alle)")}, Briefe {M("KI: Erpresserbrief")}, Ratsstimmen {M("KI: Ratsstimme erpresst")}, " +
                              $"Anzeigen {M("KI: Anzeige")}, Bloßstellen {M("KI: Bloßstellen")}");
        }
        return ergebnisse.Values.Any(x => x.P.Fehler is not null) ? 1 : 0;
    }

    private static void Stunde(Stadtwirtschaft w, Zaehler z)
    {
        var gw = w.Geheimnisse;
        if (!gw.IstAktiv) return;
        if (!z.Angemeldet)
        {
            z.Angemeldet = true;
            w.Meldung += m => Meldung(m, z, gw);
            // Zu Partiebeginn: die Geheimnisse der Ratsherren
            foreach (var g in gw.Alle) { z.Gesehen.Add(g.Nummer); z.Plus("Entstanden zu Partiebeginn (Ratsherren)"); }
        }
        foreach (var g in gw.Alle)
        {
            if (z.Gesehen.Add(g.Nummer))
            {
                z.Plus("Entstanden (alle)");
                z.Plus($"Entstanden: {g.Art} ({(g.Inhaber.StartsWith("R:", StringComparison.Ordinal) ? "Ratsherr" : "Haus")})");
            }
            if (g.Oeffentlich && z.Oeffentlich.Add(g.Nummer)) z.Plus("Öffentlich geworden");
        }
        foreach (var wissen in gw.AllesWissen)
            if (z.Gewusst.Add((wissen.Haus.Inhaber, wissen.Geheimnis)))
                z.Plus($"Erfahren durch {wissen.Quelle}");
    }

    private static void Meldung(string m, Zaehler z, Geheimniswesen gw)
    {
        // Entscheidungen der KI („Haus X: …“) – die Texte aus KiRivale.Geheimnisse.cs
        if (m.Contains(" vor Gericht an: ", StringComparison.Ordinal)) z.Plus("KI: Anzeige");
        else if (m.Contains("einen Erpresserbrief: ", StringComparison.Ordinal) && m.Contains(" Schweigegeld für „", StringComparison.Ordinal)) z.Plus("KI: Erpresserbrief");
        else if (m.Contains(": erpresst die Ratsstimme von ", StringComparison.Ordinal)) z.Plus("KI: Ratsstimme erpresst");
        else if (m.Contains(" bloß: ", StringComparison.Ordinal) && m.Contains(": ", StringComparison.Ordinal) && !m.Contains("Die ganze Stadt", StringComparison.Ordinal)) z.Plus("KI: Bloßstellen");
        else if (m.Contains(" – seine Stimme fehlt", StringComparison.Ordinal)) z.Plus("KI: Spitzel auf Ratsherrn");
        // Folgen
        if (m.EndsWith(" Schweigegeld.", StringComparison.Ordinal) && m.Contains(" zahlt ", StringComparison.Ordinal)) z.Plus("Brief mit Geheimnis gezahlt");
        if (m.StartsWith("Gerichtstag ", StringComparison.Ordinal) && gw.Daten.Arten.Values.Any(a => m.Contains(a.Name, StringComparison.Ordinal)))
            z.Plus(m.Contains("SCHULDIG", StringComparison.Ordinal) ? "Urteil mit Geheimnis: schuldig" : "Urteil mit Geheimnis: frei");
        if (m.StartsWith("Unfriede im Haus ", StringComparison.Ordinal)) z.Plus("Ehepartner entdeckt Ehebruch");
    }
}
