using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Schänkenmessung – Sitzplätze der Wirtshäuser (Messung, kein Test; Datenversion 66)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen schaenke [Jahre] [Startwerte]
//                [--karten=greybox,koeln] [--parallel=n] [--ohne-plaetze] [--datei=pfad.json]
//
//  Je Karte drei Aufbauten mit allen KI-Häusern der Karte: der Spieler als Wirt mit einem Betrieb (KI geführt, ohne Expansion),
//  als ausbauender Wirt (KI mit dem Profil der KI-Häuser) und als Schmied mit einem Betrieb (wie Teil 3 des Balancing-Berichts).
//  Gemessen: Reinvermögen von Spieler und KI-Häusern, Jahr bis Bürger, Umsatz der Schänken (Ausschank und Würfelgeld) des Spielers
//  und der KI-Häuser, Gäste, „voll“ (Gäste, die in ihrer Schänke keinen Platz fanden) und „ausgefallen“ (nirgends Platz),
//  dazu je Stunde, wie oft eine Schänke voll war und wie oft mehr Einwohner darin saßen, als sie Plätze hat.
//  --ohne-plaetze nimmt gastwirtschaft.sitzplaetzeJeStufe aus den Daten (Rechnung wie bis Datenversion 65) und zählt, wie oft eine
//  Schänke mit 20/30/40 Plätzen voll gewesen wäre. Ändert keinen Wert.
// ═════════════════════════════════════════════════════════════════════════════

static class Schaenkenmessung
{
    /// <summary>Die Plätze, mit denen „wäre voll gewesen“ ohne Plätze in den Daten gezählt wird (Entscheidung des Nutzers, 1.11.2026).</summary>
    static readonly int[] PlaetzeVergleich = { 20, 30, 40 };

    sealed class Zaehler
    {
        public double UmsatzSpieler, UmsatzKi;
        public int GaesteSpieler, GaesteKi, Voll, Ausgefallen, Abgewiesen, Schaenkenjahre, SchaenkenKi, SchaenkenSpieler;
        public int StundenOffen, StundenVoll, StundenUeberPlaetzen, EinwohnerUeber, EinwohnerMax;
        public readonly Dictionary<string, (int Kommen, int Voll)> Stand = new(StringComparer.Ordinal);
    }

    public static int Erstellen(string[] args)
    {
        string? Schalter(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1];
        var worte = args.Where(a => !a.StartsWith("--")).ToList();
        var jahre = worte.Count > 0 ? int.Parse(worte[0]) : 20;
        var startwerte = worte.Count > 1 ? int.Parse(worte[1]) : 20;
        var karten = (Schalter("karten") ?? "greybox,koeln").Split(',');
        var parallel = int.Parse(Schalter("parallel") ?? "2");
        var ohnePlaetze = args.Contains("--ohne-plaetze");
        var q = new Messquellen(anpassen: ohnePlaetze ? OhnePlaetze : null);
        var daten = Wirtschaftsdaten.AusJson(q.Wirtschaft);

        var aufbauten = new List<Messaufbau>();
        foreach (var karte in karten)
            for (var s = 1; s <= startwerte; s++)
            {
                aufbauten.Add(new Messaufbau(karte, s * 101, jahre, Beruf: "Wirt"));
                aufbauten.Add(new Messaufbau(karte, s * 101, jahre, Beruf: "Wirt", Bezug: Bezugsart.WieKi));
                aufbauten.Add(new Messaufbau(karte, s * 101, jahre));
            }
        Console.Error.WriteLine($"Schänkenmessung: {aufbauten.Count} Partien zu {jahre} Jahren, {parallel} parallel{(ohnePlaetze ? ", ohne Sitzplätze" : "")} …");
        var ergebnisse = new ConcurrentDictionary<Messaufbau, (Partieergebnis P, Zaehler Z)>();
        var fertig = 0;
        Parallel.ForEach(aufbauten, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel) }, a =>
        {
            var z = new Zaehler();
            Partieergebnis r;
            try { r = Messpartie.Spielen(a, q, w => Stunde(w, z), (w, _) => Jahresende(w, z)); }
            catch (Exception ex) { r = new Partieergebnis { Aufbau = a, Fehler = ex.ToString() }; }
            ergebnisse[a] = (r, z);
            var n = Interlocked.Increment(ref fertig);
            if (n % 10 == 0 || n == aufbauten.Count) Console.Error.WriteLine($"  {n}/{aufbauten.Count}");
        });
        foreach (var (a, (r, _)) in ergebnisse.Where(x => x.Value.P.Fehler is not null).Take(5)) Console.Error.WriteLine($"FEHLER in {a}: {r.Fehler}");

        var gruppen = new JsonArray();
        Console.WriteLine($"Datenversion {daten.Version}{(ohnePlaetze ? " ohne Sitzplätze" : "")}, {jahre} Jahre, {startwerte} Startwerte");
        Console.WriteLine("Karte | Aufbau | Spieler J" + jahre + " | KI-Häuser J" + jahre + " | Bürger Ø Jahr (im Ziel 8–12) | bankrott | Umsatz Schänke Spieler/Jahr | " +
                          "Umsatz Schänken KI/Jahr | Gäste/Jahr | ohne Ware/Jahr | voll/Jahr | ausgefallen/Jahr (Anteil) | Stunden voll | Stunden über Plätzen | Einwohner über Plätzen (max)");
        foreach (var g in ergebnisse.Where(x => x.Value.P.Fehler is null)
                     .GroupBy(x => (x.Key.Karte, Aufbau: $"{x.Key.Beruf} {(x.Key.Bezug == Bezugsart.WieKi ? "ausbauend" : "ein Betrieb")}"))
                     .OrderBy(x => x.Key.Karte, StringComparer.Ordinal).ThenBy(x => x.Key.Aufbau, StringComparer.Ordinal))
        {
            var liste = g.OrderBy(x => x.Key.Startwert).Select(x => x.Value).ToList();
            var spieler = liste.Select(x => x.P.Spieler.Im(jahre).Rein).ToList();
            var ki = liste.SelectMany(x => x.P.Ki.Select(h => h.Im(jahre).Rein)).ToList();
            var buerger = liste.Where(x => x.P.Spieler.BuergerReinJahr is not null).Select(x => (double)x.P.Spieler.BuergerReinJahr!.Value).ToList();
            var imZiel = buerger.Count(x => x is >= 8 and <= 12);
            var bankrott = liste.Count(x => x.P.Spieler.BankrottJahr is not null) + liste.Sum(x => x.P.Ki.Count(h => h.BankrottJahr is not null));
            double JeJahr(Func<Zaehler, double> f) => liste.Average(x => f(x.Z)) / jahre;
            var gaeste = JeJahr(z => z.GaesteSpieler + z.GaesteKi);
            var ausgefallen = JeJahr(z => z.Ausgefallen);
            double Anteil(Func<Zaehler, int> f) => liste.Sum(x => x.Z.StundenOffen) is var n and > 0 ? 100.0 * liste.Sum(x => f(x.Z)) / n : 0;
            Console.WriteLine($"{g.Key.Karte} | {g.Key.Aufbau} | {spieler.Average():0} | {ki.Average():0} | " +
                              $"{(buerger.Count > 0 ? buerger.Average() : double.NaN):0.0} ({imZiel}/{liste.Count}) | {bankrott} | " +
                              $"{JeJahr(z => z.UmsatzSpieler):0} | {JeJahr(z => z.UmsatzKi):0} | {gaeste:0.0} | {JeJahr(z => z.Abgewiesen):0.0} | {JeJahr(z => z.Voll):0.00} | " +
                              $"{ausgefallen:0.00} ({(gaeste + ausgefallen > 0 ? 100 * ausgefallen / (gaeste + ausgefallen) : 0):0.0} %) | " +
                              $"{Anteil(z => z.StundenVoll):0.0} % | {Anteil(z => z.StundenUeberPlaetzen):0.0} % | " +
                              $"{Anteil(z => z.EinwohnerUeber):0.0} % ({liste.Max(x => x.Z.EinwohnerMax)})");
            gruppen.Add(new JsonObject
            {
                ["karte"] = g.Key.Karte, ["aufbau"] = g.Key.Aufbau, ["n"] = liste.Count,
                ["spielerReinvermoegen"] = Balancingbericht.Kennzahl(spieler), ["kiReinvermoegen"] = Balancingbericht.Kennzahl(ki),
                ["buergerJahr"] = Balancingbericht.Kennzahl(buerger, 1), ["buergerErreicht"] = buerger.Count, ["buergerImZiel"] = imZiel, ["bankrott"] = bankrott,
                ["umsatzSchaenkeSpielerJeJahr"] = Math.Round(JeJahr(z => z.UmsatzSpieler)), ["umsatzSchaenkenKiJeJahr"] = Math.Round(JeJahr(z => z.UmsatzKi)),
                ["gaesteSpielerJeJahr"] = Math.Round(JeJahr(z => z.GaesteSpieler), 1), ["gaesteKiJeJahr"] = Math.Round(JeJahr(z => z.GaesteKi), 1),
                ["vollJeJahr"] = Math.Round(JeJahr(z => z.Voll), 2), ["ausgefallenJeJahr"] = Math.Round(ausgefallen, 2),
                ["abgewiesenOhneWareJeJahr"] = Math.Round(JeJahr(z => z.Abgewiesen), 2),
                ["schaenkenSpielerImMittel"] = Math.Round(JeJahr(z => z.SchaenkenSpieler), 2), ["schaenkenKiImMittel"] = Math.Round(JeJahr(z => z.SchaenkenKi), 2),
                ["stundenOffen"] = liste.Sum(x => x.Z.StundenOffen), ["stundenVollProzent"] = Math.Round(Anteil(z => z.StundenVoll), 2),
                ["stundenUeberPlaetzenProzent"] = Math.Round(Anteil(z => z.StundenUeberPlaetzen), 2),
                ["einwohnerUeberPlaetzenProzent"] = Math.Round(Anteil(z => z.EinwohnerUeber), 2), ["einwohnerMax"] = liste.Max(x => x.Z.EinwohnerMax),
            });
        }
        var datei = Schalter("datei");
        if (datei is not null)
        {
            var json = new JsonObject
            {
                ["messung"] = "Schänkenmessung (Sitzplätze)", ["datenversion"] = daten.Version, ["ohnePlaetze"] = ohnePlaetze,
                ["mainCommit"] = Balancingbericht.Git("rev-parse --short origin/main"), ["commit"] = Balancingbericht.Git("rev-parse --short HEAD"),
                ["aufruf"] = "messen schaenke " + string.Join(' ', args), ["jahre"] = jahre, ["startwerte"] = startwerte,
                ["fehler"] = ergebnisse.Count(x => x.Value.P.Fehler is not null), ["gruppen"] = gruppen,
            };
            File.WriteAllText(datei, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            Console.Error.WriteLine($"geschrieben: {datei}");
        }
        return ergebnisse.Values.Any(x => x.P.Fehler is not null) ? 1 : 0;
    }

    /// <summary>Die Wirtschaftsdaten ohne <c>gastwirtschaft.sitzplaetzeJeStufe</c>: Gäste ohne Grenze wie bis Datenversion 65.</summary>
    static string OhnePlaetze(string json)
    {
        var knoten = JsonNode.Parse(json)!.AsObject();
        knoten["gastwirtschaft"]!.AsObject().Remove("sitzplaetzeJeStufe");
        return knoten.ToJsonString();
    }

    /// <summary>Zu jeder vollen Stunde: Wer kam in der vergangenen Stunde, war die Schänke voll, wie viele Einwohner sitzen darin.</summary>
    static void Stunde(Stadtwirtschaft w, Zaehler z)
    {
        var g = w.Gastwirtschaft;
        if (!g.IstAktiv) return;
        foreach (var b in g.Schaenken)
        {
            var buch = g.DiesesJahr(b);
            var kommen = buch.Bewirtet + buch.Abgewiesen;
            var alt = z.Stand.GetValueOrDefault(b.Name);
            if (kommen < alt.Kommen || buch.Voll < alt.Voll) alt = (0, 0); // neues Jahr: das Gästebuch beginnt von vorn
            z.Stand[b.Name] = (kommen, buch.Voll);
            var plaetze = g.Sitzplaetze(b) ?? PlaetzeVergleich[Math.Clamp(b.Stufe - 1, 0, PlaetzeVergleich.Length - 1)];
            var einwohner = w.Einwohnerschaft.IstAktiv ? w.Einwohnerschaft.Einkehrende(b) : 0;
            z.EinwohnerMax = Math.Max(z.EinwohnerMax, einwohner);
            if (kommen - alt.Kommen == 0 && buch.Voll == alt.Voll && einwohner == 0) continue; // geschlossen oder leer
            z.StundenOffen++;
            if (buch.Voll > alt.Voll) z.StundenVoll++;
            if (kommen - alt.Kommen > plaetze || buch.Voll > alt.Voll) z.StundenUeberPlaetzen++; // ohne Grenze: wäre voll gewesen
            if (einwohner > plaetze) z.EinwohnerUeber++;
        }
    }

    /// <summary>Am Ende jedes Jahres: das Gästebuch des Vorjahres jeder Schänke, nach Spieler und KI-Häusern.</summary>
    static void Jahresende(Stadtwirtschaft w, Zaehler z)
    {
        var g = w.Gastwirtschaft;
        if (!g.IstAktiv) return;
        var spieler = w.Konten[0];
        foreach (var b in w.Betriebe.Where(b => b.IstSchaenke))
        {
            if (g.Vorjahr(b) is not { } buch) continue;
            var umsatz = buch.Einnahmen + buch.Wuerfelgeld;
            z.Schaenkenjahre++;
            if (b.Konto == spieler) { z.UmsatzSpieler += umsatz; z.GaesteSpieler += buch.Bewirtet; z.SchaenkenSpieler++; }
            else { z.UmsatzKi += umsatz; z.GaesteKi += buch.Bewirtet; z.SchaenkenKi++; }
            z.Voll += buch.Voll;
            z.Ausgefallen += buch.Ausgefallen;
            z.Abgewiesen += buch.Abgewiesen;
        }
    }
}
