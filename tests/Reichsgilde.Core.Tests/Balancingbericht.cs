using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Balancing-Bericht (Messung, kein Test) – PR #176 und #177, „Balancing-Messung 1“ bis „3“ in docs/WIRTSCHAFTSMODELL.md
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen bericht [Jahre] [Startwerte] [--karte=greybox]
//                                                                          [--teile=1,2,…] [--parallel=n] [--datei=pfad.json]
//                                                                          [--fairness=greybox,probestadt,koeln] [--voelkerberufe=alle|Schmied,…]
//  Schreibt docs/messungen/balancing-<datum>.json und eine kurze Übersicht auf die Konsole. Ändert keinen Wert.
//
//  Abschnitte: 1 Berufe, 2 Völker, 3 Startfairness, 4 Häuser (1/3/5/8, Messstadt), 5 KI-Stärke, 6 Einrichtung, 7 Gauner/Wache/Bank,
//  8 Befunde. Jede Partie läuft für sich (eigene Daten, eigene Karte, eigener Zufall) – parallel, und trotzdem Wurf für Wurf gleich.
// ═════════════════════════════════════════════════════════════════════════════

static partial class Balancingbericht
{
    /// <summary>Die Jahre, an denen der Bericht misst: 5, 10, 20, 30 … bis zur Partielänge, dazu das letzte Jahr.</summary>
    public static IReadOnlyList<int> Stichjahre(int jahre) =>
        new[] { 5, 10 }.Concat(Enumerable.Range(2, Math.Max(0, jahre / 10)).Select(x => x * 10)).Append(jahre)
            .Where(x => x >= 1 && x <= jahre).Distinct().OrderBy(x => x).ToList();

    /// <summary>Das Band, in dem ein Beruf als gleichwertig gilt (Entscheidung zur Balancing-Runde): ±15 % um das Mittel.</summary>
    public const double Band = 15;

    /// <summary>Die Berufe mit gleichem Völkerbonus-Muster im Abschnitt Völker: der Schmied und ein Beruf, den andere Völker gut können.</summary>
    public static readonly string[] Voelkerberufe = { "Schmied", "Parfumeur" };

    public static readonly Volk[] SpielbareVoelker = { Volk.Mensch, Volk.Zwerg, Volk.Elb, Volk.Halbling };

    /// <summary>Häuser in der Messstadt (KI, die ersten n der Karte).</summary>
    public static readonly int[] Hausstufen = { 1, 3, 5, 8 };

    /// <summary>Karten mit Fluss und Hafen für den Abschnitt Hafen und Stadtkasse (PR #199, #200).</summary>
    public static readonly string[] Hafenkarten = { "greybox", "probestadt" };

    /// <summary>
    /// Kennzahl einer Stichprobe: Anzahl, Mittel, Streuung (Standardabweichung), Fehler des Mittels (Streuung ÷ √n – ein Unterschied unter etwa
    /// zwei solchen Fehlern kann Zufall sein), Minimum, Quantile 10/25/50/75/90 %, Maximum.
    /// </summary>
    public static JsonObject Kennzahl(IEnumerable<double> werte, int nachkomma = 0)
    {
        var w = werte.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToArray();
        if (w.Length == 0) return new JsonObject { ["n"] = 0 };
        var mittel = w.Average();
        var streuung = w.Length > 1 ? Math.Sqrt(w.Sum(x => (x - mittel) * (x - mittel)) / (w.Length - 1)) : 0;
        return new JsonObject
        {
            ["n"] = w.Length, ["mittel"] = R(mittel, nachkomma), ["streuung"] = R(streuung, nachkomma),
            ["fehlerMittel"] = R(streuung / Math.Sqrt(w.Length), nachkomma), ["min"] = R(w[0], nachkomma),
            ["q10"] = R(Quantil(w, 0.10), nachkomma), ["q25"] = R(Quantil(w, 0.25), nachkomma), ["median"] = R(Quantil(w, 0.5), nachkomma),
            ["q75"] = R(Quantil(w, 0.75), nachkomma), ["q90"] = R(Quantil(w, 0.90), nachkomma), ["max"] = R(w[^1], nachkomma),
        };
    }

    /// <summary>Quantil einer sortierten Stichprobe, linear zwischen den Nachbarn (wie R „type 7“, Excel QUANTIL.INKL).</summary>
    public static double Quantil(IReadOnlyList<double> sortiert, double p)
    {
        if (sortiert.Count == 0) return double.NaN;
        var h = (sortiert.Count - 1) * p;
        var unten = (int)Math.Floor(h);
        var oben = Math.Min(unten + 1, sortiert.Count - 1);
        return sortiert[unten] + (h - unten) * (sortiert[oben] - sortiert[unten]);
    }

    public static double R(double x, int nachkomma = 0) => Math.Round(x, nachkomma, MidpointRounding.AwayFromZero);

    private static double Mittel(IEnumerable<double> werte) => werte.DefaultIfEmpty(double.NaN).Average();

    /// <summary>Abstand in Prozent: (wert − bezug) / |bezug| × 100.</summary>
    public static double Abstand(double wert, double bezug) => bezug == 0 ? double.NaN : (wert - bezug) / Math.Abs(bezug) * 100;

    private static JsonObject JeJahr(IEnumerable<int> jahre, Func<int, JsonNode?> wert)
    {
        var o = new JsonObject();
        foreach (var j in jahre) o[j.ToString()] = wert(j);
        return o;
    }

    /// <summary>Mit Vorzeichen („+3“, „−12,5“, „0“) für Prozente in Konsole und Befunden.</summary>
    public static string Vz(double x, int nachkomma = 0)
    {
        if (double.IsNaN(x)) return "–";
        var r = R(x, nachkomma);
        var betrag = Math.Abs(r).ToString(nachkomma == 0 ? "0" : "0." + new string('0', nachkomma), Zahlenformat.Deutsch);
        return r > 0 ? "+" + betrag : r < 0 ? "−" + betrag : betrag;
    }

    /// <summary>Eine Zahl im deutschen Format („0,95“); fehlt sie, ein Strich.</summary>
    public static string Zahl(double? x, string format) => x is { } v && !double.IsNaN(v) ? v.ToString(format, Zahlenformat.Deutsch) : "–";

    /// <summary>Ganze Gulden mit Tausenderpunkt für Konsole und Befunde („12.345“).</summary>
    private static string Text(double x) => Zahlenformat.Ganz(R(x));

    // ═════════════════════════════════════════════════════════════════════════
    //  Ablauf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Was ein Aufruf misst (aus den Argumenten nach „messen bericht“).</summary>
    sealed record Einstellung(int Jahre, int Startwerte, string Karte, HashSet<int> Teile, int Parallel, string? Datei, string[] Fairness, string[] VoelkerBerufe)
    {
        public IEnumerable<int> Werte => Enumerable.Range(1, Startwerte).Select(s => s * 101);
        public IReadOnlyList<int> Stich => Stichjahre(Jahre);
        public int Einrichtungsjahre => Math.Min(Jahre, 10);
    }

    public static int Erstellen(string[] args)
    {
        var worte = args.Where(a => !a.StartsWith("--")).ToList();
        string? Schalter(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}="))?.Split('=', 2)[1];
        var daten0 = Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft()));
        var e = new Einstellung(
            worte.Count > 0 ? int.Parse(worte[0]) : 20,
            worte.Count > 1 ? int.Parse(worte[1]) : 20,
            Schalter("karte") ?? "greybox",
            (Schalter("teile") ?? "1,2,3,4,5,6,7,8,9").Split(',').Select(int.Parse).ToHashSet(),
            int.Parse(Schalter("parallel") ?? Environment.ProcessorCount.ToString()),
            Schalter("datei"),
            (Schalter("fairness") ?? "greybox,probestadt").Split(','),
            Schalter("voelkerberufe") is { } vb ? (vb == "alle" ? daten0.Berufe.Keys.ToArray() : vb.Split(',')) : Voelkerberufe);
        var uhr = Stopwatch.StartNew();
        var q = new Messquellen(Schalter("ohne")); // --ohne=schiffshandel: alle Partien ohne diesen Abschnitt (Diagnose)
        var daten = Wirtschaftsdaten.AusJson(q.Wirtschaft);

        // ── Alle Partien, die die Abschnitte brauchen (gleiche Aufbauten nur einmal) ──
        var auftraege = new HashSet<Messaufbau>();
        foreach (var teil in e.Teile) auftraege.UnionWith(Aufbauten(teil, e, daten));
        Console.Error.WriteLine($"Balancing-Bericht: {auftraege.Count} Partien zu {e.Jahre} Jahren, {e.Startwerte} Startwerte, {e.Parallel} parallel …");
        var ergebnisse = new ConcurrentDictionary<Messaufbau, Partieergebnis>();
        var fertig = 0;
        Parallel.ForEach(auftraege.OrderByDescending(Aufwand).ThenBy(a => a.ToString(), StringComparer.Ordinal),
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, e.Parallel) }, a =>
            {
                Partieergebnis r;
                try { r = Messpartie.Spielen(a, q); }
                catch (Exception ex) { r = new Partieergebnis { Aufbau = a, Fehler = ex.ToString() }; }
                ergebnisse[a] = r;
                var n = Interlocked.Increment(ref fertig);
                if (n % 20 == 0 || n == auftraege.Count)
                    Console.Error.WriteLine($"  {n}/{auftraege.Count} Partien ({Zahl(uhr.Elapsed.TotalMinutes, "0.0")} Min.)");
            });
        var fehler = ergebnisse.Values.Where(r => r.Fehler is not null).ToList();
        foreach (var r in fehler.Take(5)) Console.Error.WriteLine($"FEHLER in {r.Aufbau}: {r.Fehler}");

        Partieergebnis P(Messaufbau a) => ergebnisse[a];
        var bericht = new JsonObject
        {
            ["bericht"] = "Balancing-Messung 6",
            ["datum"] = DateTime.Now.ToString("yyyy-MM-dd"),
            ["mainCommit"] = Git("rev-parse --short origin/main"),
            ["commit"] = Git("rev-parse --short HEAD"),
            ["datenversion"] = daten.Version,
            ["aufruf"] = "messen bericht " + string.Join(' ', args),
            ["parameter"] = new JsonObject
            {
                ["jahre"] = e.Jahre, ["startwerte"] = e.Startwerte, ["zufallsstartwerte"] = $"101, 202 … {e.Startwerte * 101} (s × 101)",
                ["stichjahre"] = new JsonArray(e.Stich.Select(x => (JsonNode)x).ToArray()), ["karte"] = e.Karte,
                ["fairnesskarten"] = new JsonArray(e.Fairness.Select(x => (JsonNode)x).ToArray()),
                ["teile"] = new JsonArray(e.Teile.OrderBy(x => x).Select(x => (JsonNode)x).ToArray()),
                ["band"] = $"±{Band} % um das Mittel", ["startvermoegen"] = daten.Allgemein.Startvermoegen,
                ["ohneAbschnitt"] = Schalter("ohne"),
            },
            ["partien"] = auftraege.Count,
            ["partienMitFehler"] = fehler.Count,
        };
        var befunde = new List<string>();
        if (e.Teile.Contains(1)) bericht["berufe"] = Berufe(e, q, daten, P, befunde);
        if (e.Teile.Contains(2)) bericht["voelker"] = Voelker(e, daten, P, befunde);
        if (e.Teile.Contains(3)) bericht["startfairness"] = Startfairness(e, q, daten, P, befunde);
        if (e.Teile.Contains(4)) bericht["haeuser"] = Haeuser(e, daten, P, befunde);
        if (e.Teile.Contains(5)) bericht["kiStaerke"] = KiStaerke(e, daten, ergebnisse, P, befunde);
        if (e.Teile.Contains(6)) bericht["einrichtung"] = Einrichtung(e, q, daten, ergebnisse, P, befunde);
        if (e.Teile.Contains(7)) bericht["gaunerWacheBank"] = GaunerWacheBank(e, daten, ergebnisse, P, befunde);
        if (e.Teile.Contains(8)) bericht["hafenKasse"] = HafenKasse(e, daten, ergebnisse, P, befunde);
        if (e.Teile.Contains(9)) bericht["statutC"] = StatutC(e, q, daten, befunde);
        bericht["befunde"] = new JsonArray(befunde.Select(b => (JsonNode)b).ToArray());
        bericht["laufzeitSekunden"] = R(uhr.Elapsed.TotalSeconds);

        var datei = e.Datei ?? Path.Combine(Projektordner(), "docs", "messungen", $"balancing-{DateTime.Now:yyyy-MM-dd}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(datei))!);
        File.WriteAllText(datei, bericht.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
        Console.WriteLine();
        Console.WriteLine("Befunde:");
        foreach (var b in befunde) Console.WriteLine("  • " + b);
        Console.WriteLine($"geschrieben: {datei} ({auftraege.Count} Partien, {Zahl(uhr.Elapsed.TotalMinutes, "0.0")} Min.)");
        return fehler.Count == 0 ? 0 : 1;
    }

    /// <summary>Grobe Rechenzeit einer Partie – die teuersten zuerst, damit am Ende keine lange allein läuft.</summary>
    private static double Aufwand(Messaufbau a) => a.Jahre * (a.Karte == "messstadt" ? 1 + (a.Rivalen > 0 ? a.Rivalen : 8) / 3.0 : 1.5);

    /// <summary>Die Partien eines Abschnitts.</summary>
    private static IEnumerable<Messaufbau> Aufbauten(int teil, Einstellung e, Wirtschaftsdaten daten)
    {
        var berufe = daten.Berufe.Keys.ToList();
        foreach (var s in e.Werte)
            switch (teil)
            {
                case 1:
                    foreach (var b in berufe) yield return Beruf(e, s, b);
                    yield return Beruf(e, s, "Dieb") with { BezugCharakter = KiCharakter.Wagemutig };
                    break;
                case 2:
                    foreach (var b in e.VoelkerBerufe)
                        foreach (var v in SpielbareVoelker) yield return Beruf(e, s, b) with { Volk = v };
                    break;
                case 3:
                    foreach (var (karte, haeuser) in Fairnesskarten(e))
                        for (var r = 0; r <= haeuser; r++) yield return new Messaufbau(karte, s, e.Jahre, Rotation: r);
                    break;
                case 4:
                    foreach (var n in Hausstufen)
                        yield return new Messaufbau("messstadt", s, e.Jahre, Bezug: Bezugsart.Geist, Rivalen: n, MarktMessen: true);
                    break;
                case 5:
                    yield return new Messaufbau(e.Karte, s, e.Jahre, Bezug: Bezugsart.WieKi);
                    yield return new Messaufbau(e.Karte, s, e.Jahre, Bezug: Bezugsart.WieKi, Schwierigkeit: Schwierigkeit.Schwer);
                    break;
                case 6:
                    foreach (var b in berufe)
                    {
                        yield return Beruf(e, s, b);
                        if (Ertragsstuecke(daten, b) is { Count: > 0 } stuecke)
                            yield return Beruf(e, s, b) with { Jahre = e.Einrichtungsjahre, Einrichtung = string.Join(',', stuecke.Select(x => x.Kennung)) };
                    }
                    yield return new Messaufbau(e.Karte, s, e.Jahre, Bezug: Bezugsart.WieKi);
                    break;
                case 7:
                    foreach (var c in new[] { KiCharakter.Ausgewogen, KiCharakter.Wagemutig })
                        foreach (var wache in new[] { false, true })
                            yield return Beruf(e, s, "Dieb") with { BezugCharakter = c, WachhausBeimErsten = wache };
                    yield return Beruf(e, s, "Gardist");
                    yield return Beruf(e, s, "Geldleiher");
                    yield return Beruf(e, s, "Schmied");
                    yield return new Messaufbau(e.Karte, s, e.Jahre, Bezug: Bezugsart.WieKi);
                    break;
                case 8:
                    foreach (var karte in Hafenkarten)
                        foreach (var ohne in new[] { false, true })
                            yield return new Messaufbau(karte, s, e.Jahre, Bezug: Bezugsart.Passiv, OhneSchiffshandel: ohne);
                    break;
            }
    }

    private static Messaufbau Beruf(Einstellung e, int startwert, string beruf) => new(e.Karte, startwert, e.Jahre, Beruf: beruf);

    /// <summary>Die Karten der Startfairness mit der Zahl ihrer KI-Häuser (Rotation 0 … Häuser).</summary>
    private static IEnumerable<(string Karte, int Haeuser)> Fairnesskarten(Einstellung e) =>
        e.Fairness.Select(k => (k, JsonNode.Parse(File.ReadAllText(Datenpfad.Karte(k)))!["start"]!["haeuser"]!.AsArray().Count));

    /// <summary>
    /// Die Einrichtung, die gleich mehr einbringt (Werkstatt: kürzere Zeit; Wache: Lohn; Geldleiher: Rahmen, Wechsel, Ausfall) und ab Stufe 1
    /// ohne Voraussetzung zu haben ist – das kauft der Spieler im Abschnitt Einrichtung am Start.
    /// </summary>
    public static List<EinrichtungDaten> Ertragsstuecke(Wirtschaftsdaten daten, string beruf) =>
        daten.Werkstatt?.EinrichtungFuer(beruf).Where(x => x.AbStufe <= 1 && x.Voraussetzung is null && x.Wirkung is var w
            && (w.ZeitFaktor < 1 || w.LohnFaktor < 1 || w.RahmenFaktor > 1 || w.WechselFaktor > 1 || w.AusfallFaktor < 1)).ToList() ?? new();

    private static string Projektordner() =>
        Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!;

    private static string Git(string befehl)
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("git", befehl)
            {
                WorkingDirectory = Projektordner(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            })!;
            var text = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return p.ExitCode == 0 && text != "" ? text : "unbekannt";
        }
        catch (Exception) { return "unbekannt"; }
    }

    /// <summary>Eine frische Partie zu Startpreisen (Karte des Berichts), dazu ein reiches Messkonto für Betriebe zur Probe.</summary>
    private static (Stadtwirtschaft W, Konto Mess) Startmarkt(Messquellen q, string karte)
    {
        var daten = Wirtschaftsdaten.AusJson(q.Wirtschaft);
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 101, Siegbedingung = Siegbedingung.Endlos },
            daten, new NeuePartie(), karte: Stadtkarte.AusJson(q.Karte(karte), Gebaeudekatalog.AusJson(q.Katalog)));
        var w = sim.Wirtschaft!;
        return (w, w.KontoEroeffnen("Haus Messung", startvermoegen: 1_000_000));
    }

    /// <summary>Die Betriebsarten eines Berufs in der Stadt, Stufe 1 bis 3.</summary>
    public static List<Betriebsart> Stufen(Wirtschaftsdaten daten, string beruf)
    {
        var liste = new List<Betriebsart>();
        Betriebsart? art = daten.Berufe[beruf].Werkstatt;
        while (art is { } a && liste.Count < 10)
        {
            liste.Add(a);
            art = daten.Art(a).Ausbau;
        }
        return liste;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  1 Berufe
    // ═════════════════════════════════════════════════════════════════════════

    private static JsonObject Berufe(Einstellung e, Messquellen q, Wirtschaftsdaten daten, Func<Messaufbau, Partieergebnis> p, List<string> befunde)
    {
        // (a) statisch: erwarteter Jahresgewinn eines neuen Betriebs zu Startpreisen (Kalkulation), dazu Kollekte und Ausschank
        var (w, mess) = Startmarkt(q, e.Karte);
        var zeilen = new List<(string Beruf, int Stufe, double Kalk, double Zusatz, JsonObject Json)>();
        var steuer = daten.Allgemein.Umsatzsteuer;
        foreach (var beruf in daten.Berufe.Keys)
        {
            var stufen = Stufen(daten, beruf);
            for (var i = 0; i < stufen.Count; i++)
            {
                var art = stufen[i];
                var d = daten.Art(art);
                var wert = d.OhneProduktion ? w.WertOhneWare(d) : d.Produkte.Max(x => Kalkulation.GewinnJeArbeitsminute(w, x));
                var bestes = d.OhneProduktion ? null : d.Produkte.OrderByDescending(x => Kalkulation.GewinnJeArbeitsminute(w, x)).ThenBy(x => x, StringComparer.Ordinal).First();
                var kalk = Kalkulation.ErwarteterJahresgewinn(w, art);
                double zusatz = 0;
                string? zusatzArt = null;
                if (w.Kirchenwesen.IstAktiv && beruf == w.Kirchenwesen.Daten.Beruf)
                {
                    var b = w.BetriebEroeffnen($"Probe {d.Name}", art, mess);
                    zusatz = w.Kirchenwesen.ErwarteteKollekte(b) * (1 - steuer);
                    zusatzArt = "Kollekte der Messe (die einzige Kirche der Stadt, nach Steuer)";
                }
                else if (w.Gastwirtschaft.IstAktiv && beruf == w.Gastwirtschaft.Daten.Beruf)
                {
                    var b = w.BetriebEroeffnen($"Probe {d.Name}", art, mess);
                    var ausschank = w.Gastwirtschaft.Daten.Ausschank.Where(d.Produkte.Contains)
                        .OrderByDescending(x => Kalkulation.GewinnJeArbeitsminute(w, x)).ThenBy(x => x, StringComparer.Ordinal).FirstOrDefault();
                    if (ausschank is not null)
                    {
                        zusatz = w.Gastwirtschaft.ErwarteteGaeste(b) * (w.Gastwirtschaft.Ausschankpreis(ausschank) - w.Markt[ausschank].Preis) * (1 - steuer);
                        zusatzArt = $"Ausschank: erwartete Gäste × (Ausschankpreis − Marktpreis) für {ausschank} (die einzige Schänke, nach Steuer)";
                    }
                }
                zeilen.Add((beruf, i + 1, kalk, zusatz, new JsonObject
                {
                    ["beruf"] = beruf, ["stufe"] = i + 1, ["betrieb"] = d.Name, ["plaetze"] = d.Plaetze, ["arbeitsminuten"] = daten.Arbeitsminuten(art),
                    ["fixkosten"] = d.Fixkosten, ["quelle"] = bestes ?? QuelleOhneWare(w, d),
                    ["wertJeArbeitsminute"] = R(wert, 3), ["kalkulation"] = R(kalk), ["zusatz"] = R(zusatz),
                    ["zusatzArt"] = zusatzArt, ["gesamt"] = R(kalk + zusatz),
                }));
            }
        }
        foreach (var gruppe in zeilen.GroupBy(z => z.Stufe))
        {
            var mittel = gruppe.Average(z => z.Kalk + z.Zusatz);
            foreach (var z in gruppe)
            {
                var abstand = Abstand(z.Kalk + z.Zusatz, mittel);
                z.Json["abstandProzent"] = R(abstand, 1);
                z.Json["imBand"] = Math.Abs(abstand) <= Band;
            }
        }
        var mittelJeStufe = new JsonObject();
        foreach (var gruppe in zeilen.GroupBy(z => z.Stufe))
            mittelJeStufe[gruppe.Key.ToString()] = R(gruppe.Average(z => z.Kalk + z.Zusatz));
        foreach (var gruppe in zeilen.GroupBy(z => z.Stufe))
        {
            var mittel = gruppe.Average(z => z.Kalk + z.Zusatz);
            var aussen = gruppe.Where(z => Math.Abs(Abstand(z.Kalk + z.Zusatz, mittel)) > Band)
                .Select(z => $"{z.Beruf} {Vz(Abstand(z.Kalk + z.Zusatz, mittel))} %").ToList();
            if (aussen.Count > 0)
                befunde.Add($"Berufe statisch, Stufe {gruppe.Key} (Mittel {Text(mittel)} Gulden im Jahr): außerhalb ±{Band} %: {string.Join(", ", aussen)}.");
        }

        // (b) simuliert: der Spieler mit genau einem Betrieb, von der KI geführt (Ausbau nach ihren Regeln)
        var sim = new JsonArray();
        var zuwachsJeBeruf = new Dictionary<string, Dictionary<int, double>>();
        var gewinnJeBeruf = new Dictionary<string, Dictionary<int, double>>();
        var startgeld = daten.Allgemein.Startvermoegen;
        var buergerJeBeruf = new List<string>();
        var reihen = daten.Berufe.Keys.Select(b => (Name: b, Aufbau: (Func<int, Messaufbau>)(s => Beruf(e, s, b)), ImMittel: true))
            .Append(("Dieb (wagemutig geführt)", s => Beruf(e, s, "Dieb") with { BezugCharakter = KiCharakter.Wagemutig }, false)).ToList();
        foreach (var (name, aufbau, imMittel) in reihen)
        {
            var laeufe = e.Werte.Select(s => p(aufbau(s))).Where(r => r.Fehler is null).ToList();
            if (laeufe.Count == 0) continue;
            var beruf = laeufe[0].Aufbau.Beruf;
            if (imMittel)
            {
                zuwachsJeBeruf[name] = e.Stich.ToDictionary(j => j, j => laeufe.Average(r => r.Spieler.Im(j).Rein - startgeld));
                gewinnJeBeruf[name] = e.Stich.ToDictionary(j => j, j => laeufe.Average(r => r.Spieler.Im(j).Gewinn));
            }
            var sonderSchluessel = laeufe.SelectMany(r => r.Spieler.Jahre.SelectMany(x => x.Sonder.Keys)).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            var zeile = new JsonObject
            {
                ["beruf"] = name, ["volk"] = laeufe[0].Aufbau.Volk.ToString(),
                ["volksbonusProzent"] = R(daten.Berufe[beruf].Voelker.GetValueOrDefault(laeufe[0].Aufbau.Volk) * 100, 1),
                ["fuehrung"] = $"KI ({laeufe[0].Aufbau.BezugCharakter}), genau ein Betrieb",
                ["n"] = laeufe.Count,
                ["jahre"] = JeJahr(e.Stich, j => new JsonObject
                {
                    ["reinvermoegen"] = Kennzahl(laeufe.Select(r => r.Spieler.Im(j).Rein)),
                    ["gewinn"] = Kennzahl(laeufe.Select(r => r.Spieler.Im(j).Gewinn)),
                    ["stufe"] = Kennzahl(laeufe.Select(r => (double)r.Spieler.Im(j).Stufe), 2),
                    ["kiHaeuserReinvermoegen"] = Kennzahl(laeufe.SelectMany(r => r.Ki.Select(h => h.Im(j).Rein))),
                    ["sonder"] = sonderSchluessel.Count == 0 ? null : new JsonObject(sonderSchluessel.Select(k =>
                        new KeyValuePair<string, JsonNode?>(k, R(laeufe.Average(r => r.Spieler.Im(j).Sonder.GetValueOrDefault(k)), 1)))),
                }),
                ["jeJahrImMittel"] = Zerlegung(laeufe.Select(r => r.Spieler)),
                ["jahreBisBuerger"] = Kennzahl(laeufe.Where(r => r.Spieler.BuergerReinJahr is not null).Select(r => (double)r.Spieler.BuergerReinJahr!.Value), 1),
                ["buergerErreicht"] = laeufe.Count(r => r.Spieler.BuergerReinJahr is not null),
                ["jahreBisStandBuerger"] = Kennzahl(laeufe.Where(r => r.Spieler.BuergerStandJahr is not null).Select(r => (double)r.Spieler.BuergerStandJahr!.Value), 1),
                ["jahrStufe2"] = Kennzahl(laeufe.Where(r => r.Spieler.Stufe2Jahr is not null).Select(r => (double)r.Spieler.Stufe2Jahr!.Value), 1),
                ["jahrStufe3"] = Kennzahl(laeufe.Where(r => r.Spieler.Stufe3Jahr is not null).Select(r => (double)r.Spieler.Stufe3Jahr!.Value), 1),
                ["bankrott"] = laeufe.Count(r => r.Spieler.BankrottJahr is not null),
            };
            sim.Add(zeile);
            var buerger = laeufe.Where(r => r.Spieler.BuergerReinJahr is not null).Select(r => (double)r.Spieler.BuergerReinJahr!.Value).ToList();
            var imZiel = buerger.Count(x => x is >= 8 and <= 12);
            zeile["buergerIn8bis12"] = imZiel;
            zeile["buergerVor8"] = buerger.Count(x => x < 8);
            buergerJeBeruf.Add($"{name} {Zahl(buerger.Count > 0 ? buerger.Average() : double.NaN, "0.0")} ({imZiel}/{laeufe.Count})");
            if (imMittel && (buerger.Count < laeufe.Count || buerger.Count > 0 && (buerger.Average() < 8 || buerger.Average() > 12)))
                befunde.Add($"Z2 (Bürger mit einem Betrieb in 8–12 Jahren), {name}: " +
                            (buerger.Count == 0 ? $"in {e.Jahre} Jahren nie erreicht" : $"Ø Jahr {Zahl(buerger.Average(), "0.0")}, erreicht {buerger.Count}/{laeufe.Count}, davon im Ziel {imZiel}") + ".");
        }

        // Abstand zum Mittel aller zehn Berufe (Vermögenszuwachs seit dem Start und Jahresgewinn)
        var simMittel = new JsonObject();
        foreach (var j in e.Stich)
        {
            var zuwachsMittel = zuwachsJeBeruf.Values.Average(x => x[j]);
            var gewinnMittel = gewinnJeBeruf.Values.Average(x => x[j]);
            simMittel[j.ToString()] = new JsonObject { ["zuwachs"] = R(zuwachsMittel), ["gewinn"] = R(gewinnMittel) };
            foreach (var zeile in sim.Select(x => x!.AsObject()))
            {
                var name = (string)zeile["beruf"]!;
                var jahrJson = zeile["jahre"]![j.ToString()]!.AsObject();
                var zuwachs = (double)jahrJson["reinvermoegen"]!["mittel"]! - startgeld;
                var gewinn = (double)jahrJson["gewinn"]!["mittel"]!;
                jahrJson["abstandZuwachsProzent"] = R(Abstand(zuwachs, zuwachsMittel), 1);
                jahrJson["abstandGewinnProzent"] = R(Abstand(gewinn, gewinnMittel), 1);
                jahrJson["imBand"] = Math.Abs(Abstand(zuwachs, zuwachsMittel)) <= Band;
                _ = name;
            }
        }
        befunde.Add("Jahre bis Bürger (Reinvermögen 10.000) je Beruf, Ø (im Ziel 8–12 von n): " + string.Join(", ", buergerJeBeruf) + ".");
        var letztes = e.Stich[^1];
        var ausserhalb = sim.Select(x => x!.AsObject()).Where(z => zuwachsJeBeruf.ContainsKey((string)z["beruf"]!)
                && !(bool)z["jahre"]![letztes.ToString()]!["imBand"]!)
            .Select(z => $"{z["beruf"]} {Vz((double)z["jahre"]![letztes.ToString()]!["abstandZuwachsProzent"]!)} %").ToList();
        befunde.Add($"Berufe simuliert (ein Betrieb, {e.Jahre} Jahre, Vermögenszuwachs gegen das Mittel {Text((double)simMittel[letztes.ToString()]!["zuwachs"]!)}): " +
                    (ausserhalb.Count == 0 ? $"alle im Band ±{Band} %." : $"außerhalb ±{Band} %: {string.Join(", ", ausserhalb)}."));

        Console.WriteLine($"1 Berufe – Vermögenszuwachs nach {letztes} Jahren (ein Betrieb, KI geführt):");
        foreach (var z in sim.Select(x => x!.AsObject()))
            Console.WriteLine($"  {(string)z["beruf"]!,-26} {Text((double)z["jahre"]![letztes.ToString()]!["reinvermoegen"]!["mittel"]! - startgeld),10}  " +
                              $"{Vz((double)z["jahre"]![letztes.ToString()]!["abstandZuwachsProzent"]!, 1),7} %   Bürger Ø Jahr {Zahl((double?)z["jahreBisBuerger"]!["mittel"], "0.0")}");

        return new JsonObject
        {
            ["erklaerung"] = "Statisch: Kalkulation.ErwarteterJahresgewinn eines neuen Betriebs, voll mit Gesellen besetzt, bestes Produkt bzw. Beute, Sold, " +
                             "Zins und Wechsel, zu Startpreisen (erste Minute der Partie auf der Karte des Berichts); dazu für Prediger und Wirt Kollekte bzw. " +
                             "Ausschank (Schätzung). Abstand zum Mittel der zehn Berufe derselben Stufe. Simuliert: Der Spieler (Mensch) beginnt mit der Werkstatt " +
                             "seines Berufs und wird von der KI geführt wie ein KI-Haus (Profil ausgewogen), aber mit genau einem Betrieb (MaxBetriebe 0): Personal, " +
                             "Ware, Meisterprüfung, Ausbau und Einrichtung nach den Regeln der KI (Ausbau, sobald Meistergrad und Rechnung es erlauben; Einrichtung " +
                             "zum Jahresende, wenn sie sich in höchstens 4 Jahren bezahlt macht, PR #177), keine Expansion. Gegen die drei KI-Häuser der Karte. „Zuwachs“ = Reinvermögen − Startvermögen; Abstand zum Mittel der zehn Berufe. " +
                             "Der Dieb nutzt unter der ausgewogenen KI nur die Taschendiebe (Einbruch und Schutzgeld nur wagemutig) – deshalb eine zweite Zeile, " +
                             "die nicht ins Mittel zählt. Menschen haben +5 % als Tischler und Gardist. „jeJahrImMittel“: Betriebsgewinn und was " +
                             "außerhalb der Betriebe dazukam (Amtseinkommen im Rat) oder abging (Familie, Geschenke, Gericht, Gaunerei, Investitionen wie " +
                             "Ausbau und Wohnhaus, Standesgebühren) – Mittel je Jahr über die ganze Partie.",
            ["statisch"] = new JsonArray(zeilen.Select(z => (JsonNode)z.Json).ToArray()),
            ["statischMittelJeStufe"] = mittelJeStufe,
            ["simuliert"] = sim,
            ["simuliertMittel"] = simMittel,
        };
    }

    /// <summary>Woher das Vermögen kommt: Mittel je Jahr über die ganze Partie – Betriebsgewinn und was außerhalb der Betriebe hinzukam oder abging.</summary>
    private static JsonObject Zerlegung(IEnumerable<Hausverlauf> haeuser)
    {
        var h = haeuser.ToList();
        JsonNode? M(Func<Jahreswert, double> f) => R(h.Average(x => x.Jahre.Average(f)));
        return new JsonObject
        {
            ["gewinn"] = M(x => x.Gewinn), ["amtseinkommen"] = M(x => x.Amt), ["familie"] = M(x => -x.Familie), ["geschenke"] = M(x => -x.Geschenke),
            ["gericht"] = M(x => -x.Gericht), ["gaunerei"] = M(x => -x.Gaunerei), ["investitionen"] = M(x => -x.Investitionen),
            ["standesgebuehren"] = M(x => -x.Standesgebuehren), ["zinsen"] = M(x => -x.Zinsen),
            // schon enthalten: die Steuer im Gewinn, die Spitzel im Gericht (PR #208)
            ["davonSteuer"] = M(x => -x.Steuer), ["davonSpitzel"] = M(x => -x.Spitzel),
            ["schiffeAmEnde"] = R(h.Average(x => (double)x.Jahre[^1].Schiffe), 2),
        };
    }

    private static string QuelleOhneWare(Stadtwirtschaft w, BetriebsartDaten d) =>
        w.Diebe.IstAktiv && d.Beruf == w.Diebe.Daten.Beruf ? "Beute der Taschendiebe"
        : w.Wache.IstAktiv && d.Beruf == w.Wache.Daten.Beruf ? "Sold der Stadt"
        : w.Bank.IstAktiv && d.Beruf == w.Bank.Daten.Beruf ? "Stadtzinsen und Wechsel (volle Kasse)"
        : "–";

    // ═════════════════════════════════════════════════════════════════════════
    //  2 Völker
    // ═════════════════════════════════════════════════════════════════════════

    private static JsonObject Voelker(Einstellung e, Wirtschaftsdaten daten, Func<Messaufbau, Partieergebnis> p, List<string> befunde)
    {
        var zeilen = new JsonArray();
        var letztes = e.Stich[^1];
        var startgeld = daten.Allgemein.Startvermoegen;
        Console.WriteLine($"2 Völker – Vermögenszuwachs nach {letztes} Jahren (ein Betrieb):");
        foreach (var beruf in e.VoelkerBerufe)
        {
            var mensch = e.Werte.Select(s => p(Beruf(e, s, beruf))).Where(r => r.Fehler is null).ToList();
            var bezug = mensch.Average(r => r.Spieler.Im(letztes).Rein - startgeld);
            foreach (var volk in SpielbareVoelker)
            {
                var laeufe = e.Werte.Select(s => p(Beruf(e, s, beruf) with { Volk = volk })).Where(r => r.Fehler is null).ToList();
                if (laeufe.Count == 0) continue;
                var zuwachs = laeufe.Average(r => r.Spieler.Im(letztes).Rein - startgeld);
                var bonus = daten.Berufe[beruf].Voelker.GetValueOrDefault(volk) * 100;
                zeilen.Add(new JsonObject
                {
                    ["beruf"] = beruf, ["volk"] = volk.ToString(), ["volksbonusProzent"] = R(bonus, 1), ["n"] = laeufe.Count,
                    ["jahre"] = JeJahr(e.Stich, j => new JsonObject
                    {
                        ["reinvermoegen"] = Kennzahl(laeufe.Select(r => r.Spieler.Im(j).Rein)),
                        ["gewinn"] = Kennzahl(laeufe.Select(r => r.Spieler.Im(j).Gewinn)),
                        ["stufe"] = Kennzahl(laeufe.Select(r => (double)r.Spieler.Im(j).Stufe), 2),
                        ["sonder"] = new JsonObject(laeufe.SelectMany(r => r.Spieler.Im(j).Sonder.Keys).Distinct().OrderBy(x => x, StringComparer.Ordinal).Select(k =>
                            new KeyValuePair<string, JsonNode?>(k, R(laeufe.Average(r => r.Spieler.Im(j).Sonder.GetValueOrDefault(k)), 2)))),
                    }),
                    ["jahreBisBuerger"] = Kennzahl(laeufe.Where(r => r.Spieler.BuergerReinJahr is not null).Select(r => (double)r.Spieler.BuergerReinJahr!.Value), 1),
                    ["jahrStufe2"] = Kennzahl(laeufe.Where(r => r.Spieler.Stufe2Jahr is not null).Select(r => (double)r.Spieler.Stufe2Jahr!.Value), 1),
                    ["jahrStufe3"] = Kennzahl(laeufe.Where(r => r.Spieler.Stufe3Jahr is not null).Select(r => (double)r.Spieler.Stufe3Jahr!.Value), 1),
                    ["abstandZuwachsZuMenschProzent"] = R(Abstand(zuwachs, bezug), 1),
                    ["jeJahrImMittel"] = Zerlegung(laeufe.Select(r => r.Spieler)),
                    ["gewinnAbstandZuMenschProzent"] = R(Abstand(laeufe.Average(r => r.Spieler.Im(letztes).Gewinn), mensch.Average(r => r.Spieler.Im(letztes).Gewinn)), 1),
                });
                Console.WriteLine($"  {beruf,-10} {volk,-9} Bonus {Vz(bonus),3} %  {Text(zuwachs),10}  {Vz(Abstand(zuwachs, bezug), 1),7} % gegen Mensch");
            }
            var spanne = zeilen.Select(z => z!.AsObject()).Where(z => (string)z["beruf"]! == beruf).Select(z => (double)z["abstandZuwachsZuMenschProzent"]!).ToList();
            befunde.Add($"Völker, {beruf}: Vermögenszuwachs nach {letztes} Jahren je Volk {Vz(spanne.Min())} … {Vz(spanne.Max())} % gegen Menschen " +
                        $"(Bonus {string.Join(", ", SpielbareVoelker.Select(v => $"{v} {Vz(daten.Berufe[beruf].Voelker.GetValueOrDefault(v) * 100)}"))}).");
        }
        var jeVolk = new JsonObject();
        if (e.VoelkerBerufe.Length > 2)
            foreach (var volk in SpielbareVoelker)
            {
                var abst = zeilen.Select(z => z!.AsObject()).Where(z => (string)z["volk"]! == volk.ToString()).Select(z => (double)z["abstandZuwachsZuMenschProzent"]!).ToList();
                var bestes = zeilen.Select(z => z!.AsObject()).Where(z => (string)z["volk"]! == volk.ToString())
                    .Max(z => (double)z["jahre"]![letztes.ToString()]!["reinvermoegen"]!["mittel"]!);
                jeVolk[volk.ToString()] = new JsonObject { ["mittelAbstandZuMenschProzent"] = R(abst.Average(), 1), ["besterBerufReinvermoegen"] = R(bestes) };
                Console.WriteLine($"  Volk {volk,-9} Mittel über {abst.Count} Berufe {Vz(abst.Average(), 1),7} % gegen Mensch, bester Beruf {Text(bestes)}");
            }
        return new JsonObject
        {
            ["jeVolkUeberBerufe"] = jeVolk.Count > 0 ? jeVolk : null,
            ["erklaerung"] = "Wie Berufe (simuliert), derselbe Beruf mit jedem spielbaren Volk. Der Völkerbonus wirkt auf die Leistung (±5 %); das Volk " +
                             "ändert auch Familie (Kinderzahl, Lebenserwartung) und Talente – die Messung zeigt die Summe. Abstand zum Menschen derselben Startwerte.",
            ["zeilen"] = zeilen,
        };
    }
}
