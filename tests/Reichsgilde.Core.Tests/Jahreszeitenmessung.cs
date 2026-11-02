using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

// ═════════════════════════════════════════════════════════════════════════════
//  Jahreszeitenmessung – Folgen der Jahreszeiten (Messung, kein Test; Datenversion 67)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen jahreszeiten [Jahre] [Startwerte]
//                [--karten=greybox,koeln] [--parallel=n] [--ohne-jahreszeiten] [--datei=pfad.json]
//
//  Je Karte und Startwert zwei Aufbauten mit allen KI-Häusern der Karte: der Spieler als Schmied und als Wirt mit einem Betrieb (KI geführt,
//  ohne Expansion). Gemessen: Reinvermögen von Spieler und KI-Häusern, Bankrotte, Jahr bis Bürger, Umsatz von Spieler und KI je Jahreszeit
//  (Jahresrechnung des Jahres), Preise je Jahreszeit (Mittel der vollen Stunden, Preis ÷ Grundpreis) für Holz, Kerze, Gerste, Roggen, Kräuter
//  und alle Waren, Schiffsertrag je Jahreszeit, wie oft der Fluss zufriert und wie lange.
//  --ohne-jahreszeiten nimmt den Abschnitt „jahreszeiten“ aus den Daten (Rechnung wie bis Datenversion 66 = Stand der Schänken). Ändert keinen Wert.
// ═════════════════════════════════════════════════════════════════════════════

static class Jahreszeitenmessung
{
    static readonly string[] Waren = { "Holz", "Kerze", "Gerste", "Roggen", "Kräuter" };

    sealed class Zaehler
    {
        public readonly int[] Jahre = new int[4];
        public readonly double[] UmsatzSpieler = new double[4], UmsatzKi = new double[4], Schiffsertrag = new double[4];
        public readonly double[,] Preis = new double[4, Waren.Length + 1];
        public readonly int[] Stunden = new int[4];
        public int Eiswinter, Eisstunden, Winter;
        public readonly Dictionary<string, double> SchiffVorher = new(StringComparer.Ordinal);
    }

    public static int Erstellen(string[] args)
    {
        string? Schalter(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1];
        var worte = args.Where(a => !a.StartsWith("--")).ToList();
        var jahre = worte.Count > 0 ? int.Parse(worte[0]) : 20;
        var startwerte = worte.Count > 1 ? int.Parse(worte[1]) : 20;
        var karten = (Schalter("karten") ?? "greybox,koeln").Split(',');
        var parallel = int.Parse(Schalter("parallel") ?? "2");
        var ohne = args.Contains("--ohne-jahreszeiten");
        var q = new Messquellen(ohneAbschnitt: ohne ? "jahreszeiten" : null);
        var daten = Wirtschaftsdaten.AusJson(q.Wirtschaft);

        var aufbauten = new List<Messaufbau>();
        foreach (var karte in karten)
            for (var s = 1; s <= startwerte; s++)
            {
                aufbauten.Add(new Messaufbau(karte, s * 101, jahre));
                aufbauten.Add(new Messaufbau(karte, s * 101, jahre, Beruf: "Wirt"));
            }
        Console.Error.WriteLine($"Jahreszeitenmessung: {aufbauten.Count} Partien zu {jahre} Jahren, {parallel} parallel{(ohne ? ", ohne Jahreszeiten" : "")} …");
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
        var namen = new[] { "F", "S", "H", "W" };
        Console.WriteLine($"Datenversion {daten.Version}{(ohne ? " ohne Jahreszeiten" : "")}, {jahre} Jahre, {startwerte} Startwerte");
        Console.WriteLine($"Karte | Aufbau | Spieler J{jahre} | KI-Häuser J{jahre} | Bürger Ø Jahr (im Ziel 8–12) | bankrott | Eiswinter/Winter | Eisstunden je Eiswinter | " +
                          "Umsatz Spieler F/S/H/W | Umsatz KI je Haus F/S/H/W | Schiffsertrag F/S/H/W | " + string.Join(" | ", Waren.Append("alle").Select(x => $"Preis {x} F/S/H/W")));
        foreach (var g in ergebnisse.Where(x => x.Value.P.Fehler is null)
                     .GroupBy(x => (x.Key.Karte, x.Key.Beruf))
                     .OrderBy(x => x.Key.Karte, StringComparer.Ordinal).ThenBy(x => x.Key.Beruf, StringComparer.Ordinal))
        {
            var liste = g.OrderBy(x => x.Key.Startwert).Select(x => x.Value).ToList();
            var spieler = liste.Select(x => x.P.Spieler.Im(jahre).Rein).ToList();
            var ki = liste.SelectMany(x => x.P.Ki.Select(h => h.Im(jahre).Rein)).ToList();
            var kiHaeuser = Math.Max(1, liste.Average(x => x.P.Ki.Count()));
            var buerger = liste.Where(x => x.P.Spieler.BuergerReinJahr is not null).Select(x => (double)x.P.Spieler.BuergerReinJahr!.Value).ToList();
            var imZiel = buerger.Count(x => x is >= 8 and <= 12);
            var bankrott = liste.Count(x => x.P.Spieler.BankrottJahr is not null) + liste.Sum(x => x.P.Ki.Count(h => h.BankrottJahr is not null));
            var eis = liste.Sum(x => x.Z.Eiswinter);
            var winter = liste.Sum(x => x.Z.Winter);
            double JeJahr(Func<Zaehler, double[]> f, int i) => liste.Sum(x => x.Z.Jahre[i]) is var n and > 0 ? liste.Sum(x => f(x.Z)[i]) / n : double.NaN;
            double Preis(int i, int w) => liste.Sum(x => x.Z.Stunden[i]) is var n and > 0 ? liste.Sum(x => x.Z.Preis[i, w]) / n : double.NaN;
            string Vier(Func<int, double> f, string format) => string.Join("/", Enumerable.Range(0, 4).Select(i => f(i).ToString(format, System.Globalization.CultureInfo.InvariantCulture)));
            var eisstunden = eis > 0 ? liste.Sum(x => x.Z.Eisstunden) / (double)eis : 0;
            Console.WriteLine($"{g.Key.Karte} | {g.Key.Beruf} | {spieler.Average():0} | {ki.Average():0} | " +
                              $"{(buerger.Count > 0 ? buerger.Average() : double.NaN):0.0} ({imZiel}/{liste.Count}) | {bankrott} | {eis}/{winter} | {eisstunden:0.0} | " +
                              $"{Vier(i => JeJahr(z => z.UmsatzSpieler, i), "0")} | {Vier(i => JeJahr(z => z.UmsatzKi, i) / kiHaeuser, "0")} | {Vier(i => JeJahr(z => z.Schiffsertrag, i), "0")} | " +
                              string.Join(" | ", Enumerable.Range(0, Waren.Length + 1).Select(w => Vier(i => Preis(i, w), "0.00"))));
            var jahreszeiten = new JsonObject();
            for (var i = 0; i < 4; i++)
            {
                var preise = new JsonObject();
                for (var w = 0; w <= Waren.Length; w++) preise[w < Waren.Length ? Waren[w] : "alle"] = Math.Round(Preis(i, w), 3);
                jahreszeiten[((Jahreszeit)i).ToString()] = new JsonObject
                {
                    ["jahre"] = liste.Sum(x => x.Z.Jahre[i]), ["umsatzSpieler"] = Math.Round(JeJahr(z => z.UmsatzSpieler, i)),
                    ["umsatzKiJeHaus"] = Math.Round(JeJahr(z => z.UmsatzKi, i) / kiHaeuser), ["schiffsertrag"] = Math.Round(JeJahr(z => z.Schiffsertrag, i)),
                    ["preisfaktor"] = preise,
                };
            }
            gruppen.Add(new JsonObject
            {
                ["karte"] = g.Key.Karte, ["beruf"] = g.Key.Beruf, ["n"] = liste.Count,
                ["spielerReinvermoegen"] = Balancingbericht.Kennzahl(spieler), ["kiReinvermoegen"] = Balancingbericht.Kennzahl(ki),
                ["buergerJahr"] = Balancingbericht.Kennzahl(buerger, 1), ["buergerErreicht"] = buerger.Count, ["buergerImZiel"] = imZiel, ["bankrott"] = bankrott,
                ["eiswinter"] = eis, ["winter"] = winter, ["eisstundenJeEiswinter"] = Math.Round(eisstunden, 1), ["jahreszeiten"] = jahreszeiten,
            });
        }
        var datei = Schalter("datei");
        if (datei is not null)
        {
            var json = new JsonObject
            {
                ["messung"] = "Jahreszeitenmessung", ["datenversion"] = daten.Version, ["ohneJahreszeiten"] = ohne,
                ["mainCommit"] = Balancingbericht.Git("rev-parse --short origin/main"), ["commit"] = Balancingbericht.Git("rev-parse --short HEAD"),
                ["aufruf"] = "messen jahreszeiten " + string.Join(' ', args), ["jahre"] = jahre, ["startwerte"] = startwerte,
                ["fehler"] = ergebnisse.Count(x => x.Value.P.Fehler is not null), ["gruppen"] = gruppen,
            };
            File.WriteAllText(datei, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            Console.Error.WriteLine($"geschrieben: {datei}");
        }
        return ergebnisse.Values.Any(x => x.P.Fehler is not null) ? 1 : 0;
    }

    /// <summary>Zu jeder vollen Stunde: Preise der Jahreszeit; im Winter, ob der Fluss zu ist.</summary>
    static void Stunde(Stadtwirtschaft w, Zaehler z)
    {
        if (w.Uhr.MinuteImTag == 0) return; // 04:00: das Jahr beginnt erst
        var j = (int)w.Uhr.Jahreszeit;
        z.Stunden[j]++;
        for (var i = 0; i < Waren.Length; i++) z.Preis[j, i] += w.Markt[Waren[i]].Preisfaktor;
        z.Preis[j, Waren.Length] += w.Markt.Staende.Average(s => s.Preisfaktor);
        if (w.Jahreszeiten.FlussZugefroren) z.Eisstunden++;
    }

    /// <summary>Am Ende jedes Jahres: Umsatz der Jahresrechnung von Spieler und KI-Häusern und der Schiffsertrag – nach der Jahreszeit des Jahres.</summary>
    static void Jahresende(Stadtwirtschaft w, Zaehler z)
    {
        var jahr = w.Uhr.Jahr - 1;
        var j = (int)new Spielzeit((jahr - Spielzeit.StartJahr) * (long)Spielzeit.MinutenProTag).Jahreszeit;
        z.Jahre[j]++;
        var spieler = w.Konten[0];
        foreach (var k in w.Konten)
        {
            var umsatz = k.Vorjahr?.Umsatz ?? 0;
            if (k == spieler) z.UmsatzSpieler[j] += umsatz;
            else z.UmsatzKi[j] += umsatz;
        }
        z.Schiffsertrag[j] += w.Schiffahrt.Schiffe.Sum(s => s.ErtragVorjahr ?? 0);
        if (j == (int)Jahreszeit.Winter)
        {
            z.Winter++;
            if (w.Jahreszeiten.WinterVon(jahr) is { Streng: true } && w.Jahreszeiten.HatFluss) z.Eiswinter++;
        }
    }
}
