using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

// ═════════════════════════════════════════════════════════════════════════════
//  Jahreszeitenmessung – Folgen der Jahreszeiten (Messung, kein Test; Datenversion 69)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen jahreszeiten [Jahre] [Startwerte]
//                [--karten=greybox,koeln] [--parallel=n] [--ohne-jahreszeiten] [--ohne-vorrat] [--beruf=Schmied|Wirt|Ausbau]
//                [--ohne-folge=festmarkt,schlamm,winterarbeit,eis,ernte,winternachfrage,winternachfrage:Ware] [--ohne-schiffe] [--mit-schiffshandel] [--preise-alle] [--ki-schaenke[=anteil]] [--datei=pfad.json]
//
//  Je Karte und Startwert drei Aufbauten mit allen KI-Häusern der Karte: der Spieler als Schmied und als Wirt mit einem Betrieb (KI geführt,
//  ohne Expansion) und der ausbauende Spieler („Ausbau“: Schmied, von der KI mit dem Profil der KI-Häuser geführt, mit Expansion).
//  --beruf=Schmied|Wirt|Ausbau misst nur einen Aufbau. Gemessen: Reinvermögen von Spieler und KI-Häusern, Bankrotte, Jahr bis Bürger, Umsatz von Spieler und KI je Jahreszeit
//  (Jahresrechnung des Jahres), Preise je Jahreszeit (Mittel der vollen Stunden, Preis ÷ Grundpreis) für Holz, Kerze, Gerste, Roggen, Kräuter
//  und alle Waren, Schiffsertrag je Jahreszeit, wie oft der Fluss zufriert und wie lange.
//  Dazu die Rechnung des Spielers je Jahreszeit (Mittel je Jahr) und über die ganze Partie: alle Posten der Jahresrechnung, Ausschank, Hauswert,
//  Rest (was ohne Posten ins Reinvermögen ging), Lagerwert am Jahresende (zum Grundpreis), Fahrten, Köpfe, Gäste und je Zutat die eingekaufte Menge
//  mit Preis (Ladung des Karrens, wenn er vom Markt abfährt; Preis zur Minute des Kaufs), hergestellte Waren, Arbeiterstunden je Ware, Stufe, Schiffe
//  und Schiffskäufe; die Fahrten aller Schiffe nach Jahreszeit, Ziel, Stunde, Eis und Festmarkt (erwarteter Gewinn der KI gegen Ertrag) und je Jahr
//  Reinvermögen, Umsatz, Gewinn, Stufe und Stand des Spielers. --ohne-schiffe spielt ohne Schiffshandel (Abschnitt „schiffshandel“ fehlt).
//  --mit-schiffshandel stellt schiffshandel.aktiv an (Datenversion 70 „Hafen ruht“: in den Spieldaten aus) – so rechnet die Messung wie vorher, der Vergleich.
//  --preise-alle druckt je Gruppe für jede Ware den mittleren Preisfaktor und das mittlere Angebot der Häuser (Stück im letzten Jahr) über die Partie.
//  --ki-schaenke[=anteil] setzt „ki.schaenke“ (die KI wählt die Ware einer Schänke nach dem Ausschank, spiel-ki; anteil = gaesteanteil, Standard 1).
//  --ohne-jahreszeiten nimmt den Abschnitt „jahreszeiten“ aus den Daten (Rechnung wie bis Datenversion 68 = Stand von main ohne diesen PR). Ändert keinen Wert.
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
        "Betriebe: Umsatz", "Betriebe: Zutaten", "Betriebe: Gewinn", "Schiffe: Ertrag",
    };
    const int PUmsatz = 0, PAusschank = 1, PZutaten = 2, PPersonal = 3, PFix = 4, PSteuer = 5, PSchaden = 6, PTransport = 7, PInvest = 8, PHauswert = 9,
        PStand = 10, PAmt = 11, PGeschenke = 12, PFamilie = 13, PGericht = 14, PGaunerei = 15, PZinsen = 16, PVerkaeufe = 17, PRest = 18, PRein = 19,
        PLager = 20, PFahrten = 21, PKoepfe = 22, PBewirtet = 23, PAbgewiesen = 24, PAusgefallen = 25,
        PBetriebUmsatz = 26, PBetriebZutaten = 27, PBetriebGewinn = 28, PSchiffErtrag = 29;

    sealed class Zaehler
    {
        public readonly int[] Jahre = new int[4];
        public readonly double[] UmsatzSpieler = new double[4], UmsatzKi = new double[4], Schiffsertrag = new double[4];
        public readonly double[,] Preis = new double[4, Waren.Length + 1];
        public readonly int[] Stunden = new int[4];
        /// <summary>Je Ware über die ganze Partie: Summe der Preisfaktoren und des Angebots der Häuser im letzten Jahr, zu jeder vollen Stunde (--preise-alle).</summary>
        public readonly SortedDictionary<string, double> PreisJeWare = new(StringComparer.Ordinal), AngebotJeWare = new(StringComparer.Ordinal);
        public int Eiswinter, Eisstunden, Winter;
        public readonly Dictionary<string, double> SchiffVorher = new(StringComparer.Ordinal);
        /// <summary>Rechnung des Spielers: Summe der Jahre je Jahreszeit (<see cref="Postennamen"/>).</summary>
        public readonly double[,] Posten = new double[4, Postennamen.Length];
        /// <summary>Eingekaufte Zutaten des Spielers je Jahreszeit: Stück und Gulden (Stück × Marktpreis zur Minute des Kaufs).</summary>
        public readonly SortedDictionary<string, double>[] KaufStueck = Enumerable.Range(0, 4).Select(_ => new SortedDictionary<string, double>(StringComparer.Ordinal)).ToArray();
        public readonly SortedDictionary<string, double>[] KaufGulden = Enumerable.Range(0, 4).Select(_ => new SortedDictionary<string, double>(StringComparer.Ordinal)).ToArray();
        public readonly double[] KoepfeStunden = new double[4];
        /// <summary>Hergestellte Stück je Ware und Stunden je gewählter Ware (Produkt der Werkstatt zur vollen Stunde) des Spielers je Jahreszeit.</summary>
        public readonly SortedDictionary<string, double>[] Hergestellt = Enumerable.Range(0, 4).Select(_ => new SortedDictionary<string, double>(StringComparer.Ordinal)).ToArray();
        public readonly SortedDictionary<string, double>[] ProduktStunden = Enumerable.Range(0, 4).Select(_ => new SortedDictionary<string, double>(StringComparer.Ordinal)).ToArray();
        public readonly double[] SchiffeJahre = new double[4], SchiffErtrag = new double[4], Stufe = new double[4];
        /// <summary>Fahrten der Schiffe des Spielers nach Jahreszeit des Auslaufens und ob sie über den Jahreswechsel gingen (Index Jahreszeit × 2 + quer).</summary>
        public readonly double[] FahrtAnzahl = new double[8], FahrtErwartet = new double[8], FahrtErtrag = new double[8];
        public readonly Dictionary<Schiff, Fahrtbuch> Fahrt = new(ReferenceEqualityComparer.Instance);
        /// <summary>Erwarteter Gewinn der letzten Fahrtplanung je Schiffsname (alle Häuser).</summary>
        public readonly Dictionary<string, double> Erwartung = new(StringComparer.Ordinal);
        /// <summary>Schiffe, die beim Losschicken (Meldung der KI) schon zur selben Stadt fuhren, je Schiffsname.</summary>
        public readonly Dictionary<string, int> MitfahrerBeimAuftrag = new(StringComparer.Ordinal);
        /// <summary>Fahrten aller Schiffe nach Gruppen („alle|Winter|quer“, „alle|Eis“, „spieler|…“): Anzahl, erwartet, Ertrag.</summary>
        public readonly SortedDictionary<string, double[]> Fahrtgruppen = new(StringComparer.Ordinal);
        /// <summary>Schiffskäufe am Ende eines Jahres dieser Jahreszeit: des Spielers und aller KI-Häuser; dazu der erwartete Jahresgewinn des Spielers.</summary>
        public readonly double[] KaufSpieler = new double[4], KaufKi = new double[4], KaufErwartet = new double[4];
        public int SchiffeSpielerVorher, SchiffeKiVorher;
        public double LetzteKaufErwartung;
        public bool Angemeldet;
        public int JahrGesehen = -1;
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
        var ohneSchiffe = args.Contains("--ohne-schiffe"); // ohne Schiffshandel für alle (Zerlegung)
        var mitSchiffen = args.Contains("--mit-schiffshandel"); // schiffshandel.aktiv an: wie vor „Hafen ruht“ (Datenversion 70) – der Vergleich dazu
        if (mitSchiffen && ohneSchiffe) throw new ArgumentException("--mit-schiffshandel und --ohne-schiffe schließen sich aus.");
        var ohneVerkehr = args.Contains("--ohne-verkehr"); // schiffshandel.verkehrBeachten aus: jedes Schiff plant, als wäre es allein (wie bis zum Fix)
        // --ki-schaenke[=anteil]: ki.schaenke setzen (Ware der Schänke nach dem Ausschank, spiel-ki), auch zusammen mit --ohne-jahreszeiten
        var kiSchaenke = args.Contains("--ki-schaenke") ? "1" : Schalter("ki-schaenke");
        if (nurBeruf is not (null or "Schmied" or "Wirt" or "Ausbau")) throw new ArgumentException($"Unbekannter Aufbau „{nurBeruf}“ (Schmied, Wirt oder Ausbau).");
        if (ohne && folgen.Length > 0) throw new ArgumentException("--ohne-folge geht nur mit den Jahreszeiten, nicht zusammen mit --ohne-jahreszeiten.");
        var q = new Messquellen(ohneAbschnitt: ohne ? "jahreszeiten" : null,
            anpassen: ohneVorrat || folgen.Length > 0 || ohneVerkehr || kiSchaenke is not null || mitSchiffen
                ? json => KiSchaenke(OhneVerkehr(OhneFolgen(ohneVorrat ? OhneVorrat(MitSchiffen(json)) : MitSchiffen(json), folgen), ohneVerkehr), kiSchaenke) : null);
        string MitSchiffen(string json) => mitSchiffen ? Datenpfad.MitSchiffshandel(json) : json;
        var daten = Wirtschaftsdaten.AusJson(q.Wirtschaft);

        var aufbauten = new List<Messaufbau>();
        foreach (var karte in karten)
            for (var s = 1; s <= startwerte; s++)
            {
                if (nurBeruf is null or "Schmied") aufbauten.Add(new Messaufbau(karte, s * 101, jahre, OhneSchiffshandel: ohneSchiffe));
                if (nurBeruf is null or "Wirt") aufbauten.Add(new Messaufbau(karte, s * 101, jahre, Beruf: "Wirt", OhneSchiffshandel: ohneSchiffe));
                if (nurBeruf is null or "Ausbau") aufbauten.Add(new Messaufbau(karte, s * 101, jahre, Bezug: Bezugsart.WieKi, OhneSchiffshandel: ohneSchiffe));
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
            if (args.Contains("--preise-alle"))
            {
                // je Ware: mittlerer Preisfaktor und mittleres Angebot der Häuser (Stück im letzten Jahr) über die ganze Partie
                var stunden = Math.Max(1, liste.Sum(x => x.Z.Stunden.Sum()));
                Console.WriteLine($"  Preise {g.Key.Karte}/{g.Key.Beruf}: " + string.Join("; ", liste[0].Z.PreisJeWare.Keys.Select(ware =>
                    $"{ware} {(liste.Sum(x => x.Z.PreisJeWare[ware]) / stunden).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}" +
                    $"/{(liste.Sum(x => x.Z.AngebotJeWare[ware]) / stunden).ToString("0", System.Globalization.CultureInfo.InvariantCulture)}")));
            }
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
                ["spielerJeJahr"] = new JsonObject
                {
                    ["rein"] = new JsonArray(Enumerable.Range(1, jahre).Select(j => (JsonNode)Math.Round(liste.Average(x => x.P.Spieler.Im(j).Rein))).ToArray()),
                    ["umsatz"] = new JsonArray(Enumerable.Range(1, jahre).Select(j => (JsonNode)Math.Round(liste.Average(x => x.P.Spieler.Im(j).Umsatz))).ToArray()),
                    ["gewinn"] = new JsonArray(Enumerable.Range(1, jahre).Select(j => (JsonNode)Math.Round(liste.Average(x => x.P.Spieler.Im(j).Gewinn))).ToArray()),
                    ["stufe"] = new JsonArray(Enumerable.Range(1, jahre).Select(j => (JsonNode)Math.Round(liste.Average(x => (double)x.P.Spieler.Im(j).Stufe), 2)).ToArray()),
                    ["stand"] = new JsonArray(Enumerable.Range(1, jahre).Select(j => (JsonNode)Math.Round(liste.Average(x => (double)x.P.Spieler.Im(j).Stand), 2)).ToArray()),
                    ["reinEinzeln"] = new JsonArray(liste.Select(x => (JsonNode)Math.Round(x.P.Spieler.Im(jahre).Rein)).ToArray()),
                },
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
        foreach (var (titel, f) in new (string, Func<Zaehler, int, SortedDictionary<string, double>>)[] { ("Hergestellt", (z, i) => z.Hergestellt[i]), ("Arbeiterstunden", (z, i) => z.ProduktStunden[i]) })
        {
            var waren = liste.SelectMany(z => Enumerable.Range(0, 4).SelectMany(i => f(z, i).Keys)).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            var obj = new JsonObject();
            foreach (var ware in waren)
            {
                var werte = Enumerable.Range(0, 4).Select(i => JeJahr(i, z => f(z, i).GetValueOrDefault(ware))).ToArray();
                zeilen.Add($"{titel} {ware} | {string.Join(" | ", werte.Select(x => x.ToString("0", inv)))} | {(liste.Sum(z => Enumerable.Range(0, 4).Sum(i => f(z, i).GetValueOrDefault(ware))) / n).ToString("0", inv)}");
                obj[ware] = new JsonArray(werte.Select(x => (JsonNode)Math.Round(x, 1)).ToArray());
            }
            json[titel] = obj;
        }
        var fahrten = new JsonObject();
        for (var k = 0; k < 8; k++)
        {
            var anzahl = liste.Sum(z => z.FahrtAnzahl[k]) / n;
            var erwartet = liste.Sum(z => z.FahrtErwartet[k]) / n;
            var ertrag = liste.Sum(z => z.FahrtErtrag[k]) / n;
            var name = $"{(Jahreszeit)(k / 2)}{(k % 2 == 1 ? " über den Jahreswechsel" : "")}";
            zeilen.Add($"Schiffsfahrten ab {name}: Anzahl {anzahl.ToString("0.0", inv)}, erwartet {erwartet.ToString("0", inv)}, Ertrag {ertrag.ToString("0", inv)} (je Partie)");
            fahrten[name] = new JsonObject { ["anzahl"] = Math.Round(anzahl, 2), ["erwartet"] = Math.Round(erwartet), ["ertrag"] = Math.Round(ertrag) };
        }
        json["schiffsfahrten"] = fahrten;
        var gruppen = new JsonObject();
        foreach (var g in liste.SelectMany(z => z.Fahrtgruppen.Keys).Distinct().OrderBy(x => x, StringComparer.Ordinal))
        {
            var a = Enumerable.Range(0, 3).Select(i => liste.Sum(z => z.Fahrtgruppen.TryGetValue(g, out var x) ? x[i] : 0) / n).ToArray();
            zeilen.Add($"Fahrten {g}: Anzahl {a[0].ToString("0.00", inv)}, erwartet {a[1].ToString("0", inv)}, Ertrag {a[2].ToString("0", inv)}, je Fahrt {(a[0] > 0 ? a[1] / a[0] : 0).ToString("0", inv)} / {(a[0] > 0 ? a[2] / a[0] : 0).ToString("0", inv)} (je Partie)");
            gruppen[g] = new JsonArray(a.Select(x => (JsonNode)Math.Round(x, 2)).ToArray());
        }
        json["fahrtgruppen"] = gruppen;
        foreach (var (titel, f) in new (string, Func<Zaehler, double[]>)[] { ("Schiffskauf Spieler am Jahresende", z => z.KaufSpieler), ("Schiffskauf KI am Jahresende", z => z.KaufKi), ("Schiffskauf Spieler erwartet", z => z.KaufErwartet) })
        {
            var werte = Enumerable.Range(0, 4).Select(i => liste.Sum(z => f(z)[i]) / n).ToArray();
            zeilen.Add($"{titel} (je Partie) | {string.Join(" | ", werte.Select(x => x.ToString("0.00", inv)))} | {werte.Sum().ToString("0.00", inv)}");
            json[titel] = new JsonArray(werte.Select(x => (JsonNode)Math.Round(x, 3)).ToArray());
        }
        foreach (var (titel, f) in new (string, Func<Zaehler, double[]>)[] { ("Schiffe", z => z.SchiffeJahre), ("Schiffsertrag Spieler", z => z.SchiffErtrag), ("Stufe", z => z.Stufe) })
        {
            var werte = Enumerable.Range(0, 4).Select(i => JeJahr(i, z => f(z)[i])).ToArray();
            zeilen.Add($"{titel} | {string.Join(" | ", werte.Select(x => x.ToString("0.00", inv)))} | –");
            json[titel] = new JsonArray(werte.Select(x => (JsonNode)Math.Round(x, 2)).ToArray());
        }
        return (zeilen, json);
    }

    /// <summary>Eine Fahrt eines Schiffs: Jahreszeit, Jahr und Stunde beim Auslaufen, Ziel, erwarteter Gewinn der KI, Ertrag bis jetzt, Eis unterwegs, Festmarkt beim Auslaufen.</summary>
    sealed class Fahrtbuch
    {
        public int Jahreszeit, Jahr, Stunde;
        public double Erwartet, Ertrag, Zuletzt;
        public SchiffZustand Zustand;
        public int FahrtenGesehen;
        public bool Eis, Fest;
        /// <summary>So viele andere Schiffe fuhren beim Auslaufen schon zur selben Stadt (beladen oder unterwegs, noch nicht dort).</summary>
        public int Mitfahrer;
        public string Ziel = "";
    }

    /// <summary>
    /// Vor jedem Takt: die Fahrten aller Schiffe (Ertrag von Auslaufen bis Heimkehr, über den Jahreswechsel hinweg; erwarteter Gewinn aus der
    /// Meldung der KI). Fährt ein Karren des Spielers vom Markt ab, ist seine Ladung genau das, was er dort gekauft hat (verkauft und abgeladen wird
    /// vorher) – Stück und Gulden zum Marktpreis dieser Minute. Dazu Köpfe und gewählte Waren der Betriebe des Spielers zu jeder vollen Stunde.
    /// </summary>
    /// <summary>Eine heimgekehrte Fahrt zählen: beim Spieler je Jahreszeit, bei allen Schiffen in den Gruppen.</summary>
    static void FahrtAbschliessen(Zaehler z, Fahrtbuch f, bool istSpieler, int jahrJetzt)
    {
        var k = f.Jahreszeit * 2 + (jahrJetzt != f.Jahr ? 1 : 0);
        if (istSpieler)
        {
            z.FahrtAnzahl[k]++;
            if (!double.IsNaN(f.Erwartet)) z.FahrtErwartet[k] += f.Erwartet;
            z.FahrtErtrag[k] += f.Ertrag;
        }
        var jz = ((Jahreszeit)f.Jahreszeit).ToString();
        var quer = jahrJetzt != f.Jahr ? "quer" : "im Jahr";
        var stundenGruppe = f.Stunde < 10 ? "04-10" : f.Stunde < 16 ? "10-16" : "16-23";
        var mit = f.Mitfahrer >= 3 ? "3+" : f.Mitfahrer.ToString();
        foreach (var g in new[] { $"alle|{jz}|{quer}", $"alle|{jz}|Mitfahrer {mit}", $"mitfahrer|{mit}", $"mitfahrer|{f.Ziel}|{mit}", $"alle|{jz}|{(f.Eis ? "Eis" : "ohne Eis")}",
                     $"alle|{jz}|{(f.Fest ? "Festmarkt" : "ohne Festmarkt")}", $"alle|{jz}|{f.Ziel}", $"alle|{jz}|ab {stundenGruppe}", $"alle|{jz}" })
        {
            if (!z.Fahrtgruppen.TryGetValue(g, out var a)) z.Fahrtgruppen[g] = a = new double[3];
            a[0]++; a[1] += double.IsNaN(f.Erwartet) ? 0 : f.Erwartet; a[2] += f.Ertrag;
        }
        f.Jahr = -1;
    }

    static void Minute(Stadtwirtschaft w, Zaehler z)
    {
        var spieler = w.Konten[0];
        if (!z.Angemeldet)
        {
            z.Angemeldet = true;
            // „Haus …: schickt <Schiff> nach <Ziel>: … erwarteter Gewinn ≈ 1.234 Gulden.“ – der Gewinn, mit dem die KI die Fahrt plant
            w.Meldung += m =>
            {
                if (m.StartsWith(spieler.Inhaber + ": kauft ein ", StringComparison.Ordinal) && m.IndexOf("erwartet ≈ ", StringComparison.Ordinal) is var k and >= 0)
                    z.LetzteKaufErwartung = double.Parse(new string(m[(k + 11)..].TakeWhile(c => char.IsDigit(c) || c == '.').Where(c => c != '.').ToArray()), System.Globalization.CultureInfo.InvariantCulture);
                // jedes Haus: „<Haus>: schickt <Schiff> nach <Ziel>: … erwarteter Gewinn ≈ N Gulden.“
                if (m.IndexOf(": schickt ", StringComparison.Ordinal) is var s0 and >= 0 && m.IndexOf(" nach ", s0, StringComparison.Ordinal) is var s1 and >= 0
                    && m.IndexOf("erwarteter Gewinn ≈ ", StringComparison.Ordinal) is var s2 and >= 0)
                {
                    var name = m[(s0 + 10)..s1];
                    var wert = new string(m[(s2 + 20)..].TakeWhile(c => char.IsDigit(c) || c is '.' or '-' or '−').Where(c => c != '.').ToArray()).Replace('−', '-');
                    if (double.TryParse(wert, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var e2)) z.Erwartung[name] = e2;
                    // Die Meldung kommt gleich nach dem Auftrag: Das Schiff wird schon beladen, die nach ihm im selben Takt losgeschickten noch nicht.
                    if (w.Schiffahrt.Schiffe.FirstOrDefault(x => x.Name == name) is { Auftrag: { } a } schiff)
                        z.MitfahrerBeimAuftrag[name] = w.Schiffahrt.Schiffe.Count(x => x != schiff && x.Auftrag?.Ziel == a.Ziel
                                                                                       && x.Zustand is SchiffZustand.Beladen or SchiffZustand.Hinfahrt);
                }
            };
        }
        var jahrJetzt = w.Uhr.Jahr;
        var neuesJahr = z.JahrGesehen >= 0 && jahrJetzt != z.JahrGesehen;
        z.JahrGesehen = jahrJetzt;
        foreach (var schiff in w.Schiffahrt.Schiffe)
        {
            var istSpieler = schiff.Besitzer == spieler;
            if (!z.Fahrt.TryGetValue(schiff, out var f))
                z.Fahrt[schiff] = f = new Fahrtbuch { Zustand = schiff.Zustand, Zuletzt = schiff.ErtragLaufend, Jahr = -1, FahrtenGesehen = schiff.Fahrten };
            // Ertrag seit der letzten Minute; am Jahreswechsel stand er zuletzt im Vorjahr (ErtragVorjahr = Laufend − Unterhalt; den Unterhalt zahlt
            // nur ein Besitzer, der noch im Spiel ist – ist er es jetzt nicht mehr, war er es am Jahresende meist auch nicht)
            var vorher = neuesJahr ? (schiff.ErtragVorjahr ?? 0) + (schiff.Besitzer.IstAktiv ? schiff.Daten.Unterhalt : 0) : f.Zuletzt;
            var zuwachs = (neuesJahr ? vorher - f.Zuletzt + schiff.ErtragLaufend : schiff.ErtragLaufend - f.Zuletzt);
            f.Zuletzt = schiff.ErtragLaufend;
            if (f.Jahr >= 0) f.Ertrag += zuwachs;
            if (f.Jahr >= 0 && w.Jahreszeiten.FlussZugefroren) f.Eis = true;
            // Heimgekehrt (der Zähler der Fahrten stieg): Fahrt abschließen – auch wenn das Schiff in derselben Minute schon wieder ausläuft;
            // dessen Hinfracht steht dann noch in der alten Fahrt (selten: Heimkehr im letzten Takt des Jahres oder wiederkehrender Auftrag).
            var heim = schiff.Fahrten != f.FahrtenGesehen;
            f.FahrtenGesehen = schiff.Fahrten;
            if (heim && f.Jahr >= 0) FahrtAbschliessen(z, f, istSpieler, jahrJetzt);
            if (f.Jahr < 0 && schiff.Zustand != SchiffZustand.AmKai && (f.Zustand == SchiffZustand.AmKai || heim))
            {
                f.Jahr = jahrJetzt; f.Jahreszeit = (int)w.Uhr.Jahreszeit; f.Ertrag = 0; f.Stunde = w.Uhr.UhrzeitInMinuten / 60;
                f.Eis = false; f.Fest = w.Jahreszeiten.FestmarktJetzt is not null; f.Ziel = schiff.Auftrag?.Ziel ?? "";
                f.Mitfahrer = z.MitfahrerBeimAuftrag.Remove(schiff.Name, out var mf) ? mf // ohne Meldung (z. B. wiederkehrender Auftrag) gezählt vor dem Takt
                    : w.Schiffahrt.Schiffe.Count(x => x != schiff && x.Auftrag?.Ziel == f.Ziel && x.Zustand is SchiffZustand.Beladen or SchiffZustand.Hinfahrt);
                f.Erwartet = z.Erwartung.Remove(schiff.Name, out var e) ? e : double.NaN;
                // die Hinfracht dieser Minute steht schon im Ertrag (vor dem Takt gebucht)
                f.Ertrag = schiff.ErtragLaufend - (f.Zuletzt - zuwachs);
            }
            f.Zustand = schiff.Zustand;
        }
        if (double.IsNaN(z.ReinVorher)) { z.ReinVorher = spieler.Reinvermoegen; z.HauswertVorher = spieler.Hauswert; }
        var j = (int)w.Uhr.Jahreszeit;
        var volleStunde = w.Uhr.MinuteImTag % 60 == 0;
        foreach (var b in w.Betriebe)
        {
            if (b.Konto != spieler) continue;
            if (volleStunde) z.KoepfeStunden[j] += b.Belegt;
            if (volleStunde && b.IstWerkstatt)
                foreach (var a in b.Arbeiter)
                {
                    var ware = b.WareVon(a);
                    z.ProduktStunden[j][ware] = z.ProduktStunden[j].GetValueOrDefault(ware) + 1;
                }
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
                // winternachfrage:Ware – nur diese Ware aus der Winternachfrage (z. B. winternachfrage:Weingeist)
                case var x when x.StartsWith("winternachfrage:", StringComparison.Ordinal):
                    if (j["winter"]!["nachfrage"]!.AsObject().Remove(x["winternachfrage:".Length..]) is false) throw new ArgumentException($"„{x}“: Die Ware steht nicht in der Winternachfrage.");
                    break;
                default: throw new ArgumentException($"Unbekannte Folge „{f}“.");
            }
        return wurzel.ToJsonString();
    }

    static string OhneVerkehr(string json, bool ohne)
    {
        if (!ohne) return json;
        var knoten = JsonNode.Parse(json)!.AsObject();
        knoten["schiffshandel"]!.AsObject().Remove("verkehrBeachten");
        return knoten.ToJsonString();
    }

    /// <summary>--ki-schaenke: „ki.schaenke“ mit diesem Gästeanteil setzen (null = Daten unverändert).</summary>
    static string KiSchaenke(string json, string? anteil)
    {
        if (anteil is null) return json;
        var knoten = JsonNode.Parse(json)!.AsObject();
        knoten["ki"]!.AsObject()["schaenke"] = new JsonObject { ["gaesteanteil"] = double.Parse(anteil, System.Globalization.CultureInfo.InvariantCulture) };
        return knoten.ToJsonString();
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
        foreach (var s in w.Markt.Staende)
        {
            z.PreisJeWare[s.Ware] = z.PreisJeWare.GetValueOrDefault(s.Ware) + s.Preisfaktor;
            z.AngebotJeWare[s.Ware] = z.AngebotJeWare.GetValueOrDefault(s.Ware) + s.SpielerAngebotImJahr;
        }
        if (w.Jahreszeiten.FlussZugefroren) z.Eisstunden++;
    }

    /// <summary>Was diese Rechnung am Bargeld und an den Forderungen ändert (alle Posten mit Vorzeichen, ohne Hauswert).</summary>
    static double Netto(Jahresbilanz v) =>
        v.Umsatz - v.Zutaten - v.Personal - v.Fixkosten - v.Steuer - v.Schaden - v.Transport - v.Investitionen
        - v.Standesgebuehren + v.Amtseinkommen - v.Geschenke - v.Familie - v.Gericht - v.Gaunerei - v.Zinsen + v.Verkaeufe;

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
            // Was nach dem Abschluss des Kontos gebucht wurde (Jahresende der KI: Schiff, Einrichtung, Karren, Wohnhaus …; Ratssitzung), steht schon im
            // Laufend des neuen Jahres und dort in dessen Rechnung – hier gehört es nicht in den Zuwachs (den Hauswert behandelt der Hauswert-Posten)
            var nach = Netto(spieler.Laufend);
            var rein = spieler.Reinvermoegen - nach - z.ReinVorher;
            var gebucht = Netto(v) + hauswert;
            var lager = eigene.Sum(b => b.Lager.Sum(x => x.Value * w.Daten.Waren[x.Key].Grundpreis)
                                        + b.Karren.Sum(k => k.Ladung.Sum(x => x.Value * w.Daten.Waren[x.Key].Grundpreis)));
            var werte = new double[Postennamen.Length];
            werte[PUmsatz] = v.Umsatz; werte[PAusschank] = ausschank; werte[PZutaten] = v.Zutaten; werte[PPersonal] = v.Personal; werte[PFix] = v.Fixkosten;
            werte[PSteuer] = v.Steuer; werte[PSchaden] = v.Schaden; werte[PTransport] = v.Transport; werte[PInvest] = v.Investitionen; werte[PHauswert] = hauswert;
            werte[PStand] = v.Standesgebuehren; werte[PAmt] = v.Amtseinkommen; werte[PGeschenke] = v.Geschenke; werte[PFamilie] = v.Familie;
            werte[PGericht] = v.Gericht; werte[PGaunerei] = v.Gaunerei; werte[PZinsen] = v.Zinsen; werte[PVerkaeufe] = v.Verkaeufe;
            werte[PRest] = rein - gebucht; werte[PRein] = rein; werte[PLager] = lager;
            werte[PFahrten] = eigene.Sum(b => b.Karren.Sum(k => k.FahrtenVorjahr ?? 0));
            // Die Rechnung der Betriebe (Ausschank, Verkäufe der Karren, Zutaten der Karren, Löhne …) ohne die Schiffe, die am Konto buchen
            werte[PBetriebUmsatz] = eigene.Sum(b => b.Vorjahr?.Umsatz ?? 0);
            werte[PBetriebZutaten] = eigene.Sum(b => b.Vorjahr?.Zutaten ?? 0);
            werte[PBetriebGewinn] = eigene.Sum(b => b.Vorjahr?.Gewinn ?? 0);
            werte[PSchiffErtrag] = w.Schiffahrt.SchiffeVon(spieler).Sum(s => s.ErtragVorjahr ?? 0);
            werte[PKoepfe] = z.KoepfeStunden[j] / Math.Max(1, (Spielzeit.MinutenProTag - 1) / 60 + 1);
            z.KoepfeStunden[j] = 0;
            foreach (var b in eigene.Where(b => b.IstSchaenke))
                if (w.Gastwirtschaft.Vorjahr(b) is { } g)
                {
                    werte[PBewirtet] += g.Bewirtet; werte[PAbgewiesen] += g.Abgewiesen; werte[PAusgefallen] += g.Ausgefallen;
                }
            for (var p = 0; p < werte.Length; p++) z.Posten[j, p] += werte[p];
            foreach (var (ware, stueck) in v.Hergestellt) z.Hergestellt[j][ware] = z.Hergestellt[j].GetValueOrDefault(ware) + stueck;
            z.SchiffeJahre[j] += w.Schiffahrt.SchiffeVon(spieler).Count();
            var sp = w.Schiffahrt.SchiffeVon(spieler).Count();
            var ki = w.Schiffahrt.Schiffe.Count - sp;
            if (sp > z.SchiffeSpielerVorher) { z.KaufSpieler[j] += sp - z.SchiffeSpielerVorher; z.KaufErwartet[j] += z.LetzteKaufErwartung; }
            if (ki > z.SchiffeKiVorher) z.KaufKi[j] += ki - z.SchiffeKiVorher;
            z.SchiffeSpielerVorher = sp; z.SchiffeKiVorher = ki; z.LetzteKaufErwartung = 0;
            z.SchiffErtrag[j] += w.Schiffahrt.SchiffeVon(spieler).Sum(s => s.ErtragVorjahr ?? 0);
            z.Stufe[j] += eigene.Where(b => b.InDerStadt).Select(b => b.Stufe).DefaultIfEmpty(0).Max();
            z.ReinVorher = spieler.Reinvermoegen - nach;
            z.HauswertVorher = spieler.Hauswert;
        }
        if (j == (int)Jahreszeit.Winter)
        {
            z.Winter++;
            if (w.Jahreszeiten.WinterVon(jahr) is { Streng: true } && w.Jahreszeiten.HatFluss) z.Eiswinter++;
        }
    }
}
