using System.Collections.Concurrent;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Hafenmessung – Karren zum Hafen und Handgeld für Lehrlinge (Messung, kein Test; Datenversion 73, Nutzer 08.11.2026)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen hafen [Jahre] [Startwerte]
//                [--karten=greybox,koeln] [--parallel=n] [--beruf=Schmied|Wirt|Ausbau] [--ohne-hafenziel] [--handgeld=Gulden]
//                [--hafenaufschlag=0.03]
//
//  Je Karte und Startwert drei Aufbauten mit allen KI-Häusern der Karte (wie messen jahreszeiten): der Spieler als Schmied und als Wirt mit
//  einem Betrieb (KI geführt, ohne Expansion) und der ausbauende Spieler („Ausbau“, Profil der KI-Häuser).
//  Gemessen je Karte und Aufbau: Reinvermögen von Spieler und KI-Häusern am Ende, Bankrotte, Fahrten der Karren je Jahr, ihre mittlere
//  Hinfahrt und Hin- plus Rückfahrt (Minuten nach dem Kern, zum Handelsort der Fahrt), der Anteil der Fahrten zum Hafen (alle Jahreszeiten
//  und je Jahreszeit), das gezahlte Handgeld je Haus und Jahr und die Köpfe je Haus am Jahresende.
//  --ohne-hafenziel setzt schiffshandel.karrenZumHafen aus (die Karren fahren nur zu den Marktplätzen, wie bis Datenversion 72);
//  --handgeld=N setzt personal.handgeld (0 = keins, wie bis Datenversion 72); --hafenaufschlag=x setzt schiffshandel.hafenAufschlag (0 = keiner,
//  wie bis Datenversion 78). Ohne die Schalter gelten die Spieldaten. Dazu je Gruppe die Werkstätten in der Stadt, die nach dem Start
//  dazukamen (gebaut oder gekauft), wie viele davon näher am Hafen liegen (Handkarren hin + zurück), und der Umsatz je Jahr.
// ═════════════════════════════════════════════════════════════════════════════

static class Hafenmessung
{
    static readonly string[] Jahreszeiten = { "Frühling", "Sommer", "Herbst", "Winter" };

    sealed class Zaehler
    {
        public readonly Dictionary<Karren, int> Fahrten = new(ReferenceEqualityComparer.Instance);
        /// <summary>Je Gruppe (0 Spieler, 1 KI) und Jahreszeit: Fahrten, davon zum Hafen, Summe der Hinfahrt, Summe hin + zurück (Minuten).</summary>
        public readonly double[,] Anzahl = new double[2, 4], Hafen = new double[2, 4], Hin = new double[2, 4], Rund = new double[2, 4];
        public double HandgeldSpieler, HandgeldKi, KoepfeSpieler, KoepfeKi;
        public int Jahre, KiHaeuserJahre;
        /// <summary>Betriebe in der Stadt beim Start; je später dazugekommener Werkstatt (gebaut oder gekauft): Gruppe (0 Spieler, 1 KI) und ob ihr Handkarren näher am Hafen ist.</summary>
        public HashSet<Betrieb>? Start;
        public readonly Dictionary<Betrieb, (int Gruppe, bool AmHafen)> Neue = new(ReferenceEqualityComparer.Instance);
        /// <summary>Umsatz je Gruppe (Summe der Jahre) – für den Anteil, den der Aufschlag am Hafen ausmachen kann.</summary>
        public readonly double[] Umsatz = new double[2];
    }

    public static int Erstellen(string[] args)
    {
        string? Schalter(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1];
        var worte = args.Where(a => !a.StartsWith("--")).ToList();
        var jahre = worte.Count > 0 ? int.Parse(worte[0]) : 20;
        var startwerte = worte.Count > 1 ? int.Parse(worte[1]) : 4;
        var karten = (Schalter("karten") ?? "greybox,koeln").Split(',');
        var parallel = int.Parse(Schalter("parallel") ?? "2");
        var nurBeruf = Schalter("beruf");
        var ohneHafenziel = args.Contains("--ohne-hafenziel");
        var handgeld = Schalter("handgeld") is { } h ? double.Parse(h, System.Globalization.CultureInfo.InvariantCulture) : (double?)null;
        var aufschlag = Schalter("hafenaufschlag") is { } s0 ? double.Parse(s0, System.Globalization.CultureInfo.InvariantCulture) : (double?)null;
        if (nurBeruf is not (null or "Schmied" or "Wirt" or "Ausbau")) throw new ArgumentException($"Unbekannter Aufbau „{nurBeruf}“ (Schmied, Wirt oder Ausbau).");
        var q = new Messquellen(anpassen: json =>
        {
            if (ohneHafenziel) json = Datenpfad.OhneHafenziel(json);
            if (handgeld is { } g) json = Datenpfad.MitHandgeld(json, g);
            if (aufschlag is { } a) json = MitHafenaufschlag(json, a);
            return json;
        });
        var daten = Wirtschaftsdaten.AusJson(q.Wirtschaft);

        var aufbauten = new List<Messaufbau>();
        foreach (var karte in karten)
            for (var s = 1; s <= startwerte; s++)
            {
                if (nurBeruf is null or "Schmied") aufbauten.Add(new Messaufbau(karte, s * 101, jahre));
                if (nurBeruf is null or "Wirt") aufbauten.Add(new Messaufbau(karte, s * 101, jahre, Beruf: "Wirt"));
                if (nurBeruf is null or "Ausbau") aufbauten.Add(new Messaufbau(karte, s * 101, jahre, Bezug: Bezugsart.WieKi));
            }
        Console.Error.WriteLine($"Hafenmessung: {aufbauten.Count} Partien zu {jahre} Jahren, {parallel} parallel, Karren zum Hafen " +
                                $"{(daten.Schiffshandel?.KarrenZumHafen == true ? "an" : "aus")}, Handgeld {Zahlenformat.Ganz(daten.Personal?.Handgeld ?? 0)} …");
        var ergebnisse = new ConcurrentDictionary<Messaufbau, (Partieergebnis P, Zaehler Z)>();
        var fertig = 0;
        Parallel.ForEach(aufbauten, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel) }, a =>
        {
            var z = new Zaehler();
            Partieergebnis r;
            try { r = Messpartie.Spielen(a, q, jahresende: (w, _) => Jahresende(w, z), jeMinute: w => Minute(w, z)); }
            catch (Exception ex) { r = new Partieergebnis { Aufbau = a, Fehler = ex.ToString() }; }
            ergebnisse[a] = (r, z);
            var n = Interlocked.Increment(ref fertig);
            if (n % 6 == 0 || n == aufbauten.Count) Console.Error.WriteLine($"  {n}/{aufbauten.Count}");
        });
        foreach (var (a, (r, _)) in ergebnisse.Where(x => x.Value.P.Fehler is not null).Take(5)) Console.Error.WriteLine($"FEHLER in {a}: {r.Fehler}");

        Console.WriteLine($"Hafenmessung: {jahre} Jahre, {startwerte} Startwerte je Karte und Aufbau; Karren zum Hafen {(daten.Schiffshandel?.KarrenZumHafen == true ? "an" : "aus")}, " +
                          $"Handgeld {Zahlenformat.Ganz(daten.Personal?.Handgeld ?? 0)}, Aufschlag am Hafen {Zahlenformat.Kommazahl(100 * (daten.Schiffshandel?.HafenAufschlag ?? 0))} %");
        foreach (var karte in karten)
            foreach (var aufbau in new[] { "Schmied", "Wirt", "Ausbau" }.Where(x => nurBeruf is null || nurBeruf == x))
            {
                var gruppe = ergebnisse.Where(x => x.Key.Karte == karte && Art(x.Key) == aufbau && x.Value.P.Fehler is null)
                    .OrderBy(x => x.Key.Startwert).Select(x => x.Value).ToList();
                if (gruppe.Count == 0) continue;
                var spieler = gruppe.Select(x => x.P.Spieler.Im(jahre).Rein).ToList();
                var ki = gruppe.Select(x => x.P.Ki.Select(h => h.Im(jahre).Rein).DefaultIfEmpty(0).Average()).ToList();
                var bankrott = gruppe.Sum(x => x.P.Haeuser.Count(h => h.BankrottJahr is not null));
                var bankrottSpieler = gruppe.Count(x => x.P.Spieler.BankrottJahr is not null);
                Console.WriteLine($"\n{karte} / {aufbau} ({gruppe.Count} Partien)");
                Console.WriteLine($"  Reinvermögen J{jahre}: Spieler Ø {Zahlenformat.Ganz(spieler.Average())} (±{Zahlenformat.Ganz(Streuung(spieler))}), " +
                                  $"KI-Häuser Ø {Zahlenformat.Ganz(ki.Average())} (±{Zahlenformat.Ganz(Streuung(ki))}); Bankrotte {bankrott} (Spieler {bankrottSpieler})");
                var jahreSumme = gruppe.Sum(x => x.Z.Jahre);
                var kiJahre = gruppe.Sum(x => x.Z.KiHaeuserJahre);
                Console.WriteLine($"  Handgeld je Jahr: Spieler {Zahlenformat.Kommazahl(gruppe.Sum(x => x.Z.HandgeldSpieler) / Math.Max(1, jahreSumme))}, " +
                                  $"KI-Haus {Zahlenformat.Kommazahl(gruppe.Sum(x => x.Z.HandgeldKi) / Math.Max(1, kiJahre))}; " +
                                  $"Köpfe am Jahresende: Spieler {Zahlenformat.Kommazahl(gruppe.Sum(x => x.Z.KoepfeSpieler) / Math.Max(1, jahreSumme))}, " +
                                  $"KI-Haus {Zahlenformat.Kommazahl(gruppe.Sum(x => x.Z.KoepfeKi) / Math.Max(1, kiJahre))}");
                for (var g = 0; g < 2; g++)
                {
                    var anzahl = gruppe.Sum(x => Enumerable.Range(0, 4).Sum(j => x.Z.Anzahl[g, j]));
                    var hafen = gruppe.Sum(x => Enumerable.Range(0, 4).Sum(j => x.Z.Hafen[g, j]));
                    var hin = gruppe.Sum(x => Enumerable.Range(0, 4).Sum(j => x.Z.Hin[g, j]));
                    var rund = gruppe.Sum(x => Enumerable.Range(0, 4).Sum(j => x.Z.Rund[g, j]));
                    var jeJahr = anzahl / Math.Max(1, g == 0 ? jahreSumme : kiJahre);
                    var jeZeit = string.Join(", ", Enumerable.Range(0, 4).Select(j =>
                    {
                        var n = gruppe.Sum(x => x.Z.Anzahl[g, j]);
                        return $"{Jahreszeiten[j]} {Prozent(gruppe.Sum(x => x.Z.Hafen[g, j]), n)}";
                    }));
                    Console.WriteLine($"  {(g == 0 ? "Spieler" : "KI-Haus")}: {Zahlenformat.Kommazahl(jeJahr)} Fahrten je Jahr, hin Ø {Zahlenformat.Kommazahl(hin / Math.Max(1, anzahl))} min, " +
                                      $"hin + zurück Ø {Zahlenformat.Kommazahl(rund / Math.Max(1, anzahl))} min, zum Hafen {Prozent(hafen, anzahl)} ({jeZeit})");
                    var neue = gruppe.Sum(x => x.Z.Neue.Values.Count(v => v.Gruppe == g));
                    var amHafen = gruppe.Sum(x => x.Z.Neue.Values.Count(v => v.Gruppe == g && v.AmHafen));
                    Console.WriteLine($"    neue Werkstätten in der Stadt (gebaut oder gekauft): {neue}, davon näher am Hafen {amHafen} ({Prozent(amHafen, neue)}); " +
                                      $"Umsatz je Jahr {Zahlenformat.Ganz(gruppe.Sum(x => x.Z.Umsatz[g]) / Math.Max(1, g == 0 ? jahreSumme : kiJahre))}");
                }
            }
        return 0;
    }

    static string Art(Messaufbau a) => a.Bezug == Bezugsart.WieKi ? "Ausbau" : a.Beruf;

    static string Prozent(double teil, double ganz) => ganz <= 0 ? "–" : $"{Zahlenformat.Kommazahl(100 * teil / ganz)} %";

    static double Streuung(List<double> werte)
    {
        if (werte.Count < 2) return 0;
        var m = werte.Average();
        return Math.Sqrt(werte.Sum(x => (x - m) * (x - m)) / (werte.Count - 1));
    }

    /// <summary>Jede Minute: Welche Karren sind seit der letzten Minute aufgebrochen (<see cref="Karren.FahrtenImJahr"/> gestiegen bzw. nach dem Jahreswechsel neu)?</summary>
    static void Minute(Stadtwirtschaft w, Zaehler z)
    {
        var spieler = w.Konten[0];
        z.Start ??= new HashSet<Betrieb>(w.Betriebe, ReferenceEqualityComparer.Instance);
        var jz = (int)w.Uhr.Jahreszeit;
        foreach (var b in w.Betriebe)
            foreach (var k in b.Karren)
            {
                var vorher = z.Fahrten.GetValueOrDefault(k, 0);
                var neu = k.FahrtenImJahr >= vorher ? k.FahrtenImJahr - vorher : k.FahrtenImJahr; // nach dem Jahresabschluss beginnt der Zähler bei 0
                z.Fahrten[k] = k.FahrtenImJahr;
                if (neu <= 0) continue;
                var g = b.Konto == spieler ? 0 : 1;
                z.Anzahl[g, jz] += neu;
                if (k.ZielHafen) z.Hafen[g, jz] += neu;
                var hin = w.Fahrzeit(k, zumMarkt: true);
                z.Hin[g, jz] += neu * hin;
                z.Rund[g, jz] += neu * (hin + w.Fahrzeit(k, zumMarkt: false));
            }
    }

    static void Jahresende(Stadtwirtschaft w, Zaehler z)
    {
        z.Jahre++;
        var spieler = w.Konten[0];
        z.HandgeldSpieler += spieler.Vorjahr?.Handgeld ?? 0;
        z.KoepfeSpieler += w.Betriebe.Where(b => b.Konto == spieler).Sum(b => b.Belegt);
        z.Umsatz[0] += spieler.Vorjahr?.Umsatz ?? 0;
        foreach (var k in w.Konten.Skip(1).Where(k => k.IstAktiv))
        {
            z.KiHaeuserJahre++;
            z.HandgeldKi += k.Vorjahr?.Handgeld ?? 0;
            z.KoepfeKi += w.Betriebe.Where(b => b.Konto == k).Sum(b => b.Belegt);
            z.Umsatz[1] += k.Vorjahr?.Umsatz ?? 0;
        }
        // Werkstätten, die nach dem Start dazukamen: Liegt ihr Hafen (Handkarren hin + zurück) näher als der Marktplatz?
        z.Start ??= new HashSet<Betrieb>(w.Betriebe, ReferenceEqualityComparer.Instance);
        foreach (var b in w.Betriebe.Where(b => b.InDerStadt && b.IstWerkstatt && !z.Start!.Contains(b) && !z.Neue.ContainsKey(b)))
        {
            var hafen = w.FahrzeitZumHafen(b, "Handkarren", true) + w.FahrzeitZumHafen(b, "Handkarren", false);
            var markt = w.Fahrzeit(b, "Handkarren", true) + w.Fahrzeit(b, "Handkarren", false);
            z.Neue[b] = (b.Konto == spieler ? 0 : 1, hafen is { } hz && hz < markt);
        }
    }

    /// <summary>Die Daten mit diesem Aufschlag am Hafen (<c>schiffshandel.hafenAufschlag</c>, Datenversion 79).</summary>
    static string MitHafenaufschlag(string json, double aufschlag)
    {
        var knoten = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        knoten["schiffshandel"]!["hafenAufschlag"] = aufschlag;
        return knoten.ToJsonString();
    }
}
