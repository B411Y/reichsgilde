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
//                [--karten=greybox,koeln] [--parallel=n] [--ohne-jahreszeiten] [--ohne-vorrat] [--beruf=Schmied|Wirt|Ausbau]
//                [--ohne-folge=festmarkt,schlamm,winterarbeit,eis,ernte,winternachfrage] [--datei=pfad.json]
//
//  Je Karte und Startwert drei Aufbauten mit allen KI-Häusern der Karte: der Spieler als Schmied und als Wirt mit einem Betrieb (KI geführt,
//  ohne Expansion) und der ausbauende Spieler („Ausbau“: Schmied, von der KI mit dem Profil der KI-Häuser geführt, mit Expansion).
//  --beruf=Schmied|Wirt|Ausbau misst nur einen Aufbau. Gemessen: Reinvermögen von Spieler und KI-Häusern, Bankrotte, Jahr bis Bürger, Umsatz von Spieler und KI je Jahreszeit
//  (Jahresrechnung des Jahres), Preise je Jahreszeit (Mittel der vollen Stunden, Preis ÷ Grundpreis) für Holz, Kerze, Gerste, Roggen, Kräuter
//  und alle Waren, Schiffsertrag je Jahreszeit, wie oft der Fluss zufriert und wie lange.
//  Dazu die Rechnung des Spielers je Jahreszeit (Mittel je Jahr) und über die ganze Partie: alle Posten der Jahresrechnung, Ausschank, Hauswert,
//  Rest (was ohne Posten ins Reinvermögen ging), Lagerwert am Jahresende (zum Grundpreis), Fahrten, Köpfe, Gäste und je Zutat die eingekaufte Menge
//  mit Preis (Ladung des Karrens, wenn er vom Markt abfährt; Preis zur Minute des Kaufs).
//  --ohne-jahreszeiten nimmt den Abschnitt „jahreszeiten“ aus den Daten (Rechnung wie bis Datenversion 66 = Stand der Schänken). Ändert keinen Wert.
// ═════════════════════════════════════════════════════════════════════════════

static class Jahreszeitenmessung
{
    static readonly string[] Waren = { "Holz", "Kerze", "Gerste", "Roggen", "Kräuter" };

    /// <summary>
    /// Die Posten der Spielerrechnung je Jahr. Reinvermögen = Zuwachs im Jahr; Rest = Zuwachs minus alle Posten (Mitgift, Erbe, gestohlenes Geld …);
    /// Hauswert = Zuwachs des Wohnhauswerts (der Ausbau steht in den Investitionen); Lagerwert, Fahrten und Köpfe sind Bestände am Jahresende bzw. Mittel.
    /// </summary>
    static readonly string[] Postennamen =
    {
        "Umsatz", "davon Ausschank", "Zutaten", "Personal", "Fixkosten", "Steuer", "Schaden", "Transport", "Investitionen", "Hauswert",
        "Standesgebuehren", "Amt", "Geschenke", "Familie", "Gericht", "Gaunerei", "Zinsen", "Verkaeufe", "Rest", "Reinvermoegen",
        "Lagerwert (Bestand)", "Fahrten", "Koepfe (Mittel)", "Gaeste bewirtet", "Gaeste abgewiesen", "Gaeste ausgefallen",
    };
    const int PUmsatz = 0, PAusschank = 1, PZutaten = 2, PPersonal = 3, PFix = 4, PSteuer = 5, PSchaden = 6, PTransport = 7, PInvest = 8, PHauswert = 9,
        PStand = 10, PAmt = 11, PGeschenke = 12, PFamilie = 13, PGericht = 14, PGaunerei = 15, PZinsen = 16, PVerkaeufe = 17, PRest = 18, PRein = 19,
        PLager = 20, PFahrten = 21, PKoepfe = 22, PBewirtet = 23, PAbgewiesen = 24, PAusgefallen = 25;

    sealed class Zaehler
    {
        public readonly int[] Jahre = new int[4];
        public readonly double[] UmsatzSpieler = new double[4], UmsatzKi = new double[4], Schiffsertrag = new double[4];
        public readonly double[,] Preis = new double[4, Waren.Length + 1];
        public readonly int[] Stunden = new int[4];
        public int Eiswinter, Eisstunden, Winter;
        public readonly Dictionary<string, double> SchiffVorher = new(StringComparer.Ordinal);
        /// <summary>Rechnung des Spielers: Summe der Jahre je Jahreszeit (<see cref="Postennamen"/>).</summary>
        public readonly double[,] Posten = new double[4, Postennamen.Length];
        /// <summary>Eingekaufte Zutaten des Spielers je Jahreszeit: Stück und Gulden (Stück × Marktpreis zur Minute des Kaufs).</summary>
        public readonly SortedDictionary<string, double>[] KaufStueck = Enumerable.Range(0, 4).Select(_ => new SortedDictionary<string, double>(StringComparer.Ordinal)).ToArray();
        public readonly SortedDictionary<string, double>[] KaufGulden = Enumerable.Range(0, 4).Select(_ => new SortedDictionary<string, double>(StringComparer.Ordinal)).ToArray();
        public readonly double[] KoepfeStunden = new double[4];
        public double ReinVorher = double.NaN, HauswertVorher;
        public readonly Dictionary<Karren, KarrenZustand> Zustand = new(ReferenceEqualityComparer.Instance);
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
        var ohneVorrat = args.Contains("--ohne-vorrat"); // ohne ki.jahreszeiten: die KI legt keinen Wintervorrat an
        // --ohne-folge=festmarkt,schlamm,winterarbeit,eis,ernte,winternachfrage: einzelne Folgen abschalten (Zerlegung)
        var folgen = (Schalter("ohne-folge") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var nurBeruf = Schalter("beruf");
        if (nurBeruf is not (null or "Schmied" or "Wirt" or "Ausbau")) throw new ArgumentException($"Unbekannter Aufbau „{nurBeruf}“ (Schmied, Wirt oder Ausbau).");
        if (ohne && folgen.Length > 0) throw new ArgumentException("--ohne-folge geht nur mit den Jahreszeiten, nicht zusammen mit --ohne-jahreszeiten.");
        var q = new Messquellen(ohneAbschnitt: ohne ? "jahreszeiten" : null,
            anpassen: ohneVorrat || folgen.Length > 0 ? json => OhneFolgen(ohneVorrat ? OhneVorrat(json) : json, folgen) : null);
        var daten = Wirtschaftsdaten.AusJson(q.Wirtschaft);

        var aufbauten = new List<Messaufbau>();
        foreach (var karte in karten)
            for (var s = 1; s <= startwerte; s++)
            {
                if (nurBeruf is null or "Schmied") aufbauten.Add(new Messaufbau(karte, s * 101, jahre));
                if (nurBeruf is null or "Wirt") aufbauten.Add(new Messaufbau(karte, s * 101, jahre, Beruf: "Wirt"));
                if (nurBeruf is null or "Ausbau") aufbauten.Add(new Messaufbau(karte, s * 101, jahre, Bezug: Bezugsart.WieKi));
            }
        Console.Error.WriteLine($"Jahreszeitenmessung: {aufbauten.Count} Partien zu {jahre} Jahren, {parallel} parallel{(ohne ? ", ohne Jahreszeiten" : "")} …");
        var ergebnisse = new ConcurrentDictionary<Messaufbau, (Partieergebnis P, Zaehler Z)>();
        var fertig = 0;
        Parallel.ForEach(aufbauten, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallel) }, a =>
        {
            var z = new Zaehler();
            Partieergebnis r;
            try { r = Messpartie.Spielen(a, q, w => Stunde(w, z), (w, _) => Jahresende(w, z), w => Minute(w, z)); }
            catch (Exception ex) { r = new Partieergebnis { Aufbau = a, Fehler = ex.ToString() }; }
            ergebnisse[a] = (r, z);
            var n = Interlocked.Increment(ref fertig);
            if (n % 10 == 0 || n == aufbauten.Count) Console.Error.WriteLine($"  {n}/{aufbauten.Count}");
        });
        foreach (var (a, (r, _)) in ergebnisse.Where(x => x.Value.P.Fehler is not null).Take(5)) Console.Error.WriteLine($"FEHLER in {a}: {r.Fehler}");

        var gruppen = new JsonArray();
        var kosten = new List<(string Karte, string Beruf, (List<string> Zeilen, JsonObject Json) Rechnung)>();
        var namen = new[] { "F", "S", "H", "W" };
        Console.WriteLine($"Datenversion {daten.Version}{(ohne ? " ohne Jahreszeiten" : "")}, {jahre} Jahre, {startwerte} Startwerte");
        Console.WriteLine($"Karte | Aufbau | Spieler J{jahre} | KI-Häuser J{jahre} | Bürger Ø Jahr (im Ziel 8–12) | bankrott | Eiswinter/Winter | Eisstunden je Eiswinter | " +
                          "Umsatz Spieler F/S/H/W | Umsatz KI je Haus F/S/H/W | Schiffsertrag F/S/H/W | " + string.Join(" | ", Waren.Append("alle").Select(x => $"Preis {x} F/S/H/W")));
        foreach (var g in ergebnisse.Where(x => x.Value.P.Fehler is null)
                     .GroupBy(x => (x.Key.Karte, Beruf: Aufbauname(x.Key)))
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
            var rechnung = Rechnung(liste.Select(x => x.Z).ToList(), jahre);
            kosten.Add((g.Key.Karte, g.Key.Beruf, rechnung));
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
                ["rechnungSpieler"] = rechnung.Json,
            });
        }
        foreach (var (karte, beruf, r) in kosten)
        {
            Console.WriteLine();
            Console.WriteLine($"Rechnung des Spielers, {karte}, {beruf} (Mittel je Jahr der Jahreszeit; Summe = ganze Partie, Mittel der Startwerte)");
            Console.WriteLine("Posten | F | S | H | W | Summe");
            foreach (var zeile in r.Zeilen) Console.WriteLine(zeile);
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

    /// <summary>
    /// Die Rechnung des Spielers einer Gruppe: je Posten das Mittel je Jahr jeder Jahreszeit und die Summe über die Partie (Mittel der Startwerte),
    /// dazu je eingekaufter Zutat Stück je Jahr und der mittlere Preis.
    /// </summary>
    static (List<string> Zeilen, JsonObject Json) Rechnung(List<Zaehler> liste, int jahre)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var zeilen = new List<string>();
        var json = new JsonObject();
        var n = Math.Max(1, liste.Count);
        double JeJahr(int i, Func<Zaehler, double> f) => liste.Sum(x => x.Jahre[i]) is var m and > 0 ? liste.Sum(f) / m : double.NaN;
        for (var p = 0; p < Postennamen.Length; p++)
        {
            var bestand = p is PLager or PKoepfe; // Bestände: keine Summe über die Partie
            var werte = Enumerable.Range(0, 4).Select(i => JeJahr(i, z => z.Posten[i, p])).ToArray();
            var summe = bestand ? double.NaN : liste.Sum(z => Enumerable.Range(0, 4).Sum(i => z.Posten[i, p])) / n;
            zeilen.Add($"{Postennamen[p]} | {string.Join(" | ", werte.Select(x => x.ToString("0", inv)))} | {(bestand ? "–" : summe.ToString("0", inv))}");
            json[Postennamen[p]] = new JsonObject
            {
                ["jeJahr"] = new JsonArray(werte.Select(x => (JsonNode)Math.Round(x, 1)).ToArray()),
                ["summe"] = bestand ? null : Math.Round(summe, 1),
            };
        }
        var zutaten = liste.SelectMany(z => z.KaufStueck.SelectMany(d => d.Keys)).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        var kauf = new JsonObject();
        foreach (var ware in zutaten)
        {
            var stueck = Enumerable.Range(0, 4).Select(i => JeJahr(i, z => z.KaufStueck[i].GetValueOrDefault(ware))).ToArray();
            var gulden = Enumerable.Range(0, 4).Select(i => JeJahr(i, z => z.KaufGulden[i].GetValueOrDefault(ware))).ToArray();
            var preis = Enumerable.Range(0, 4).Select(i => stueck[i] > 0 ? gulden[i] / stueck[i] : double.NaN).ToArray();
            var summe = liste.Sum(z => Enumerable.Range(0, 4).Sum(i => z.KaufGulden[i].GetValueOrDefault(ware))) / n;
            zeilen.Add($"Kauf {ware} Stück | {string.Join(" | ", stueck.Select(x => x.ToString("0", inv)))} | {(liste.Sum(z => Enumerable.Range(0, 4).Sum(i => z.KaufStueck[i].GetValueOrDefault(ware))) / n).ToString("0", inv)}");
            zeilen.Add($"Kauf {ware} Preis | {string.Join(" | ", preis.Select(x => x.ToString("0.00", inv)))} | –");
            zeilen.Add($"Kauf {ware} Gulden | {string.Join(" | ", gulden.Select(x => x.ToString("0", inv)))} | {summe.ToString("0", inv)}");
            kauf[ware] = new JsonObject
            {
                ["stueckJeJahr"] = new JsonArray(stueck.Select(x => (JsonNode)Math.Round(x, 1)).ToArray()),
                ["preis"] = new JsonArray(preis.Select(x => (JsonNode?)(double.IsNaN(x) ? null : Math.Round(x, 3))).ToArray()),
                ["guldenJeJahr"] = new JsonArray(gulden.Select(x => (JsonNode)Math.Round(x, 1)).ToArray()),
                ["guldenSumme"] = Math.Round(summe, 1),
            };
        }
        json["einkauf"] = kauf;
        return (zeilen, json);
    }

    /// <summary>
    /// Vor jedem Takt: Fährt ein Karren des Spielers vom Markt ab, ist seine Ladung genau das, was er dort gekauft hat (verkauft und abgeladen wird
    /// vorher) – Stück und Gulden zum Marktpreis dieser Minute. Dazu die Köpfe der Betriebe des Spielers zu jeder vollen Stunde.
    /// </summary>
    static void Minute(Stadtwirtschaft w, Zaehler z)
    {
        var spieler = w.Konten[0];
        if (double.IsNaN(z.ReinVorher)) { z.ReinVorher = spieler.Reinvermoegen; z.HauswertVorher = spieler.Hauswert; }
        var j = (int)w.Uhr.Jahreszeit;
        var volleStunde = w.Uhr.MinuteImTag % 60 == 0;
        foreach (var b in w.Betriebe)
        {
            if (b.Konto != spieler) continue;
            if (volleStunde) z.KoepfeStunden[j] += b.Belegt;
            foreach (var k in b.Karren)
            {
                var vorher = z.Zustand.TryGetValue(k, out var zv) ? zv : k.Zustand;
                if (vorher == KarrenZustand.ZumMarkt && k.Zustand != KarrenZustand.ZumMarkt)
                    foreach (var (ware, stueck) in k.Ladung)
                    {
                        z.KaufStueck[j][ware] = z.KaufStueck[j].GetValueOrDefault(ware) + stueck;
                        z.KaufGulden[j][ware] = z.KaufGulden[j].GetValueOrDefault(ware) + stueck * w.Markt[ware].Preis;
                    }
                z.Zustand[k] = k.Zustand;
            }
        }
    }

    /// <summary>Name des Aufbaus in Tabelle und JSON: der Beruf, beim ausbauenden Spieler „Ausbau“.</summary>
    static string Aufbauname(Messaufbau a) => a.Bezug == Bezugsart.WieKi ? "Ausbau" : a.Beruf;

    /// <summary>Schaltet einzelne Folgen der Jahreszeiten ab (Zerlegung eines Befunds).</summary>
    static string OhneFolgen(string json, string[] folgen)
    {
        if (folgen.Length == 0) return json;
        var j = JsonNode.Parse(json)!.AsObject()["jahreszeiten"]!.AsObject();
        var wurzel = j.Parent!;
        foreach (var f in folgen)
            switch (f)
            {
                case "festmarkt": j["festmaerkte"] = new JsonArray(); break;
                case "schlamm": j["fruehling"]!["schlamm"] = new JsonObject(); j["fruehling"]!["schlammUmland"] = 1; break;
                case "winterarbeit": j["winter"]!["arbeit"] = new JsonObject(); break;
                case "eis": j.Remove("strengerWinter"); break;
                case "ernte": j["herbst"]!["angebot"] = new JsonObject(); j["herbst"]!["arbeit"] = new JsonObject(); break;
                case "winternachfrage": j["winter"]!["nachfrage"] = new JsonObject(); break;
                default: throw new ArgumentException($"Unbekannte Folge „{f}“.");
            }
        return wurzel.ToJsonString();
    }

    static string OhneVorrat(string json)
    {
        var knoten = JsonNode.Parse(json)!.AsObject();
        knoten["ki"]!.AsObject().Remove("jahreszeiten");
        return knoten.ToJsonString();
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
        if (spieler.Vorjahr is { } v)
        {
            var eigene = w.Betriebe.Where(b => b.Konto == spieler).ToList();
            var ausschank = eigene.Where(b => b.IstSchaenke).Sum(b => w.Gastwirtschaft.Vorjahr(b) is { } g ? g.Einnahmen + g.Wuerfelgeld : 0);
            var hauswert = spieler.Hauswert - z.HauswertVorher;
            var rein = spieler.Reinvermoegen - z.ReinVorher;
            var gebucht = v.Umsatz - v.Zutaten - v.Personal - v.Fixkosten - v.Steuer - v.Schaden - v.Transport - v.Investitionen + hauswert
                          - v.Standesgebuehren + v.Amtseinkommen - v.Geschenke - v.Familie - v.Gericht - v.Gaunerei - v.Zinsen + v.Verkaeufe;
            var lager = eigene.Sum(b => b.Lager.Sum(x => x.Value * w.Daten.Waren[x.Key].Grundpreis)
                                        + b.Karren.Sum(k => k.Ladung.Sum(x => x.Value * w.Daten.Waren[x.Key].Grundpreis)));
            var werte = new double[Postennamen.Length];
            werte[PUmsatz] = v.Umsatz; werte[PAusschank] = ausschank; werte[PZutaten] = v.Zutaten; werte[PPersonal] = v.Personal; werte[PFix] = v.Fixkosten;
            werte[PSteuer] = v.Steuer; werte[PSchaden] = v.Schaden; werte[PTransport] = v.Transport; werte[PInvest] = v.Investitionen; werte[PHauswert] = hauswert;
            werte[PStand] = v.Standesgebuehren; werte[PAmt] = v.Amtseinkommen; werte[PGeschenke] = v.Geschenke; werte[PFamilie] = v.Familie;
            werte[PGericht] = v.Gericht; werte[PGaunerei] = v.Gaunerei; werte[PZinsen] = v.Zinsen; werte[PVerkaeufe] = v.Verkaeufe;
            werte[PRest] = rein - gebucht; werte[PRein] = rein; werte[PLager] = lager;
            werte[PFahrten] = eigene.Sum(b => b.Karren.Sum(k => k.FahrtenVorjahr ?? 0));
            werte[PKoepfe] = z.KoepfeStunden[j] / Math.Max(1, (Spielzeit.MinutenProTag - 1) / 60 + 1);
            z.KoepfeStunden[j] = 0;
            foreach (var b in eigene.Where(b => b.IstSchaenke))
                if (w.Gastwirtschaft.Vorjahr(b) is { } g)
                {
                    werte[PBewirtet] += g.Bewirtet; werte[PAbgewiesen] += g.Abgewiesen; werte[PAusgefallen] += g.Ausgefallen;
                }
            for (var p = 0; p < werte.Length; p++) z.Posten[j, p] += werte[p];
            z.ReinVorher = spieler.Reinvermoegen;
            z.HauswertVorher = spieler.Hauswert;
        }
        if (j == (int)Jahreszeit.Winter)
        {
            z.Winter++;
            if (w.Jahreszeiten.WinterVon(jahr) is { Streng: true } && w.Jahreszeiten.HatFluss) z.Eiswinter++;
        }
    }
}
