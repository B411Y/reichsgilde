using System.Collections.Concurrent;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Stressmessung – wie viel Stress die Oberhäupter in der Partie tragen (Messung, kein Test; Charaktersystem Teil 3b, 9.11.2026)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen stress [Jahre] [Startwerte]
//                [--berufe=Schmied,Wirt,Dieb,Parfumeur] [--karte=greybox] [--parallel=n] [--abklingen=N]
//
//  Je Beruf und Startwert eine Partie wie im Balancing-Bericht (Spieler KI-geführt, ein Betrieb, dazu die KI-Häuser der Karte). Am Ende jedes
//  Jahres je Haus im Spiel: Stress und Stufe des Oberhaupts (nach dem Abklingen), dazu alle Einträge des abgelaufenen Jahres bei Oberhaupt,
//  Ehepartner und Erbe (Quellen, Ventile, Zusammenbrüche). Die Stresslage hält höchstens zehn Einträge je Person – in sehr vollen Jahren
//  fehlen die ältesten. Ausgabe: Mittel und Quantile, Anteil der Oberhaupt-Jahre je Stufe, Zusammenbrüche, Quellen und Ventile nach Art.
// ═════════════════════════════════════════════════════════════════════════════

static class Stressmessung
{
    sealed class Zaehler
    {
        public readonly List<double> Stress = new();
        public readonly List<int> Stufe = new();
        public readonly List<int> StufeHoechst = new();
        public readonly List<(string Art, double Wert)> Eintraege = new();
        public int Zusammenbrueche;
        /// <summary>Je Haus und Jahresende der niedrigste Preisfaktor der Waren seiner Werkstätten (für die Schwelle „Preise verfallen“).</summary>
        public readonly List<double> Preisfaktor = new();
        /// <summary>Je Person, die Oberhaupt war (Nummer): Jahre als Oberhaupt, Jahre mit Stufe 1 bzw. 2 (höchster Stand im Jahr), Zusammenbrüche.</summary>
        public readonly SortedDictionary<int, Leben> Leben = new();
    }

    sealed class Leben
    {
        public int Jahre, JahreStufe1, JahreStufe2, Zusammenbrueche;
        public double Hoechst;
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
        // --abklingen=N: Probe mit anderem Abklingen je Jahr (Text der Daten ersetzt, Datei bleibt)
        var q = new Messquellen(anpassen: Schalter("abklingen") is { } ab
            ? t => t.Contains("\"abklingen\": 15,", StringComparison.Ordinal) ? t.Replace("\"abklingen\": 15,", $"\"abklingen\": {ab},")
                : throw new InvalidDataException("„stress.abklingen“ steht nicht wie erwartet in den Daten.")
            : null);
        var daten = Wirtschaftsdaten.AusJson(q.Wirtschaft);

        var laeufe = (from b in berufe from s in Enumerable.Range(1, startwerte) select new Messaufbau(karte, s * 101, jahre, Beruf: b)).ToList();
        Console.Error.WriteLine($"Stressmessung: {laeufe.Count} Partien zu {jahre} Jahren, {parallel} parallel …");
        var ergebnisse = new ConcurrentDictionary<Messaufbau, (Partieergebnis P, Zaehler Z)>();
        var fertig = 0;
        Parallel.ForEach(laeufe, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel) }, a =>
        {
            var z = new Zaehler();
            Partieergebnis r;
            try { r = Messpartie.Spielen(a, q, jeStunde: w => Stunde(w, z), jahresende: (w, _) => Jahresende(w, z)); }
            catch (Exception ex) { r = new Partieergebnis { Aufbau = a, Fehler = ex.ToString() }; }
            ergebnisse[a] = (r, z);
            var n = Interlocked.Increment(ref fertig);
            if (n % 10 == 0 || n == laeufe.Count) Console.Error.WriteLine($"  {n}/{laeufe.Count}");
        });
        foreach (var (a, (r, _)) in ergebnisse.Where(x => x.Value.P.Fehler is not null).Take(5)) Console.Error.WriteLine($"FEHLER in {a}: {r.Fehler}");

        Console.WriteLine($"Datenversion {daten.Version}, {jahre} Jahre, {startwerte} Startwerte, Karte {karte}, Abklingen {daten.Stress?.Abklingen}, " +
                          $"main {Balancingbericht.Git("rev-parse --short origin/main")}, Stand {Balancingbericht.Git("rev-parse --short HEAD")}");
        var alle = ergebnisse.Values.Where(x => x.P.Fehler is null).Select(x => x.Z).ToList();
        void Zeile(string titel, IReadOnlyList<Zaehler> liste)
        {
            var stress = liste.SelectMany(z => z.Stress).OrderBy(x => x).ToList();
            var stufen = liste.SelectMany(z => z.Stufe).ToList();
            if (stress.Count == 0) return;
            string Anteil(int s) => $"{100.0 * stufen.Count(x => x == s) / stufen.Count:0.0} %";
            var brueche = liste.Sum(z => z.Zusammenbrueche);
            Console.WriteLine($"{titel}: {stress.Count} Oberhaupt-Jahre, Stress am Jahresende Ø {stress.Average():0.0}, Median {Balancingbericht.Quantil(stress, 0.5):0}, " +
                              $"q90 {Balancingbericht.Quantil(stress, 0.9):0}, höchstens {stress[^1]:0}; ruhig {Anteil(0)}, angespannt {Anteil(1)}, zermürbt {Anteil(2)}; " +
                              $"Zusammenbrüche {brueche} ({100.0 * brueche / stress.Count:0.00} je 100 Oberhaupt-Jahre)");
        }
        Zeile("Alle Häuser", alle);
        // Höchster Stand im Jahr (stündlich, vor dem Abklingen, Zusammenbruch = 100)
        var hoechst = alle.SelectMany(z => z.StufeHoechst).ToList();
        if (hoechst.Count > 0)
            Console.WriteLine($"  höchster Stand im Jahr: ruhig {100.0 * hoechst.Count(x => x == 0) / hoechst.Count:0.0} %, angespannt {100.0 * hoechst.Count(x => x == 1) / hoechst.Count:0.0} %, " +
                              $"zermürbt oder mehr {100.0 * hoechst.Count(x => x >= 2) / hoechst.Count:0.0} % der Oberhaupt-Jahre");
        var pf = alle.SelectMany(z => z.Preisfaktor).OrderBy(x => x).ToList();
        if (pf.Count > 0)
            Console.WriteLine($"  niedrigster Preisfaktor der eigenen Werkstattware am Jahresende: q10 {Balancingbericht.Quantil(pf, 0.1):0.00}, q25 {Balancingbericht.Quantil(pf, 0.25):0.00}, " +
                              $"Median {Balancingbericht.Quantil(pf, 0.5):0.00}, q75 {Balancingbericht.Quantil(pf, 0.75):0.00}");
        var leben = alle.SelectMany(z => z.Leben.Values).Where(l => l.Jahre >= 5).ToList();
        if (leben.Count > 0)
        {
            var jahreL = leben.Sum(l => l.Jahre);
            Console.WriteLine($"  Oberhäupter mit mindestens 5 Jahren: {leben.Count} (Ø {1.0 * jahreL / leben.Count:0.0} Jahre als Oberhaupt); je 25 Oberhaupt-Jahre " +
                              $"(etwa ein Leben) {25.0 * leben.Sum(l => l.JahreStufe1) / jahreL:0.0} Jahre angespannt oder mehr, {25.0 * leben.Sum(l => l.JahreStufe2) / jahreL:0.00} zermürbt oder mehr; " +
                              $"mindestens einmal angespannt {100.0 * leben.Count(l => l.JahreStufe1 > 0) / leben.Count:0} %, zermürbt {100.0 * leben.Count(l => l.JahreStufe2 > 0) / leben.Count:0} %, " +
                              $"zusammengebrochen {100.0 * leben.Count(l => l.Zusammenbrueche > 0) / leben.Count:0.0} % (hochgerechnet auf 25 Jahre {100.0 * leben.Sum(l => l.Zusammenbrueche) * 25 / jahreL:0.0} %)");
        }
        foreach (var b in berufe)
            Zeile($"  Partien mit {b}", ergebnisse.Where(x => x.Key.Beruf == b && x.Value.P.Fehler is null).Select(x => x.Value.Z).ToList());

        var jahreGesamt = Math.Max(1, alle.Sum(z => z.Stress.Count));
        Console.WriteLine("Quellen und Ventile (Oberhaupt, Ehepartner, Erbe; Summe je 100 Oberhaupt-Jahre, Anzahl je 100):");
        foreach (var g in alle.SelectMany(z => z.Eintraege).GroupBy(e => e.Art, StringComparer.Ordinal)
                     .OrderByDescending(g => Math.Abs(g.Sum(e => e.Wert))).ThenBy(g => g.Key, StringComparer.Ordinal))
            Console.WriteLine($"  {g.Key,-48} {100 * g.Sum(e => e.Wert) / jahreGesamt,9:0.0}   {100.0 * g.Count() / jahreGesamt,7:0.0}×");
        return ergebnisse.Values.Any(x => x.P.Fehler is not null) ? 1 : 0;
    }

    /// <summary>„Trauer um Ehefrau Agnes“ → „Trauer um Ehefrau“, „Angeklagt von Haus X“ → „Angeklagt“, „Gaunerstück … (ehrlich)“ bleibt.</summary>
    private static string Art(string text)
    {
        if (text.StartsWith("Trauer um ", StringComparison.Ordinal) || text.StartsWith("Verschleppt: ", StringComparison.Ordinal))
            return string.Join(' ', text.Split(' ').Take(text.StartsWith("Trauer", StringComparison.Ordinal) ? 3 : 2));
        foreach (var anfang in new[] { "Angeklagt", "Streit mit", "Erkrankt", "Abklingen", "Tod im Haus", "Bestohlen", "Brand oder Anschlag", "Überfallen", "Verlust in", "Preise verfallen", "Wahl zum Amt", "Amt " })
            if (text.StartsWith(anfang, StringComparison.Ordinal)) return anfang;
        return text;
    }

    /// <summary>Jede volle Stunde: der höchste Stand des Oberhaupts in diesem Jahr.</summary>
    private static void Stunde(Stadtwirtschaft w, Zaehler z)
    {
        var fb = w.Familien;
        if (!fb.HatStresswesen) return;
        foreach (var k in w.Konten.Where(k => k.IstAktiv))
            if (fb.Von(k) is { } f)
            {
                if (!z.Leben.TryGetValue(f.Oberhaupt.Nummer, out var l)) z.Leben[f.Oberhaupt.Nummer] = l = new Leben();
                l.Hoechst = Math.Max(l.Hoechst, fb.Stress(f.Oberhaupt));
            }
    }

    private static void Jahresende(Stadtwirtschaft w, Zaehler z)
    {
        var fb = w.Familien;
        if (!fb.HatStresswesen) return;
        var jahr = fb.Jahr - 1; // das abgelaufene Jahr (der Jahreswechsel hat schon weitergezählt)
        foreach (var k in w.Konten.Where(k => k.IstAktiv))
        {
            if (fb.Von(k) is not { } f) continue;
            z.Stress.Add(fb.Stress(f.Oberhaupt));
            z.Stufe.Add(fb.StressStufe(f.Oberhaupt));
            var faktoren = w.Betriebe.Where(b => b.Konto == k && b.IstWerkstatt).Select(b => w.Markt[b.Produkt].Preisfaktor).ToList();
            if (faktoren.Count > 0) z.Preisfaktor.Add(faktoren.Min());
            // höchster Stand im Jahr: stündlich, dazu der Stand vor dem Abklingen; ein Zusammenbruch heißt 100
            var o = f.Oberhaupt;
            if (!z.Leben.TryGetValue(o.Nummer, out var l)) z.Leben[o.Nummer] = l = new Leben();
            var diesesJahr = o.Stresslage.Eintraege.Where(e => e.Jahr == jahr).ToList();
            var vorAbklingen = fb.Stress(o) - diesesJahr.Where(e => e.Text.StartsWith("Abklingen", StringComparison.Ordinal)).Sum(e => e.Wert);
            var brueche = diesesJahr.Count(e => e.Text == "Zusammenbruch");
            var hoch = Math.Max(l.Hoechst, Math.Max(vorAbklingen, brueche > 0 ? 100 : 0));
            var stufe = fb.StressStufe(hoch);
            z.StufeHoechst.Add(stufe);
            l.Jahre++;
            if (stufe >= 1) l.JahreStufe1++;
            if (stufe >= 2) l.JahreStufe2++;
            l.Zusammenbrueche += brueche;
            l.Hoechst = fb.Stress(o);
            var personen = new List<Person> { f.Oberhaupt };
            if (f.Ehepartner is { } partner && !personen.Contains(partner)) personen.Add(partner);
            if (fb.Erbe(f) is { } erbe && !personen.Contains(erbe)) personen.Add(erbe);
            foreach (var e in personen.SelectMany(p => p.Stresslage.Eintraege).Where(e => e.Jahr == jahr))
            {
                z.Eintraege.Add((Art(e.Text), e.Wert));
                if (e.Text == "Zusammenbruch") z.Zusammenbrueche++;
            }
        }
    }
}
