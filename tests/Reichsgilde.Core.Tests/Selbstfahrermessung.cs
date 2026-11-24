using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

// ═════════════════════════════════════════════════════════════════════════════
//  Selbstfahrermessung – was ein Spieler gewinnt oder verliert, der seine Karren selbst fährt (Messung, kein Test; Paket M, 24.11.2026)
//
//  Aufruf:  dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen selbstfahrer [Jahre] [Startwerte] [--karten=greybox,koeln]
//                                                                                              [--datei=pfad.json] [--parallel=n]
//
//  Je Karte und Startwert dieselbe Partie (alle KI-Häuser der Karte, Spieler Schmied mit seiner Gießerei, passiv: kein Personal, kein Ausbau)
//  in mehreren Szenarien; gemessen wird das Reinvermögen des Spielers nach den Jahren. Die KI fährt immer automatisch.
//    • Werkstatt allein: automatisch mit Verweilen 20 / 10 / 0 Minuten (nur der Datenwert logistik.verweilenMinuten geändert, für alle
//      automatisch fahrenden Karren) gegen „selbst“ (Messspieler, Verweilen 20 für die KI).
//    • Mine und Werkstatt: der Spieler pachtet zu Beginn eine freie Grube (3 Altknechte); automatisch (20 / 0) gegen selbst über den Markt
//      und selbst direkt (Erz der Grube fährt zur eigenen Gießerei, nur der Rest zum Markt).
//  Der Messspieler fährt mit den Befehlen des Selbstfahrens (KarrenBeladen, KarrenLosschicken, KarrenHandeln, KarrenZurueck, KarrenAbladen)
//  nach einer einfachen Regel wie der Verwalter: losfahren, wenn genug Ware da ist (Füllgrad), die Zutaten knapp werden oder die Ware zu lange
//  wartet; am Markt wartet er auf die Marktzeit (8–20 Uhr), verkauft alles und kauft die knappen Zutaten (seit K4 zum Marktpreis ohne
//  Händlerspanne wie der Verwalter), fährt
//  zurück und lädt ab. Er verweilt nicht.
// ═════════════════════════════════════════════════════════════════════════════

static class Selbstfahrermessung
{
    enum Fahrweise { Automatisch, Selbst, SelbstDirekt }

    /// <param name="MitHafen">Der Messspieler fährt wie der Verwalter zum näheren Handelsort (Hafen als Ziel, K4); sonst nur zum Marktplatz.</param>
    /// <param name="Ganztags">Zur Aufteilung: Markt von 4 bis 23 Uhr (handel.marktVon/-Bis), der Messspieler wartet nie auf die Marktzeit.</param>
    /// <param name="OhneHafen">Zur Aufteilung: automatische Karren fahren nie zum Hafen (schiffshandel.karrenZumHafen false) – wie der Messspieler.</param>
    sealed record Szenario(string Name, bool MitMine, Fahrweise Weise, int Verweilen, bool MitHafen = false, bool Ganztags = false, bool OhneHafen = false);

    static readonly Szenario[] Szenarien =
    {
        new("Werkstatt, automatisch (Verweilen 20)", false, Fahrweise.Automatisch, 20),
        new("Werkstatt, automatisch (Verweilen 10)", false, Fahrweise.Automatisch, 10),
        new("Werkstatt, automatisch (Verweilen 0)", false, Fahrweise.Automatisch, 0),
        new("Werkstatt, automatisch (Verweilen 0, nur Marktplatz)", false, Fahrweise.Automatisch, 0, OhneHafen: true),
        new("Werkstatt, selbst nur Marktplatz", false, Fahrweise.Selbst, 20),
        new("Werkstatt, selbst mit Hafen", false, Fahrweise.Selbst, 20, MitHafen: true),
        new("Werkstatt, selbst mit Hafen, Markt ganztags", false, Fahrweise.Selbst, 20, MitHafen: true, Ganztags: true),
        new("Mine + Werkstatt, automatisch (Verweilen 20)", true, Fahrweise.Automatisch, 20),
        new("Mine + Werkstatt, automatisch (Verweilen 0)", true, Fahrweise.Automatisch, 0),
        new("Mine + Werkstatt, selbst über den Markt mit Hafen", true, Fahrweise.Selbst, 20, MitHafen: true),
        new("Mine + Werkstatt, selbst direkt mit Hafen", true, Fahrweise.SelbstDirekt, 20, MitHafen: true),
    };

    /// <summary>Ein Lauf: Reinvermögen des Spielers und der KI am Ende, dazu was seine Karren taten.</summary>
    sealed class Lauf
    {
        public double Rein, KiRein, Stillstand;
        public long Fahrten, FahrtenDirekt, Verweilt, MarktWarten, Abgelehnt;
        public string? Fehler, Grube;
    }

    public static int Erstellen(string[] args)
    {
        var jahre = args.Length > 0 && int.TryParse(args[0], out var j) ? j : 20;
        var startwerte = args.Length > 1 && int.TryParse(args[1], out var s) ? s : 20;
        string Wert(string name, string vorgabe) => args.FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.Ordinal))?[(name.Length + 3)..] ?? vorgabe;
        var karten = Wert("karten", "greybox,koeln").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var parallel = int.Parse(Wert("parallel", Math.Max(1, Environment.ProcessorCount - 2).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var datei = Wert("datei", Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "..", "..", "docs", "messungen", "balancing-2026-11-24-selbstfahrer-hafen.json"));

        var json = File.ReadAllText(Datenpfad.Wirtschaft());
        var katalog = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Datenpfad.Wirtschaft())!, "katalog.json"));
        var kartenText = karten.ToDictionary(k => k, k => File.ReadAllText(Datenpfad.Karte(k)), StringComparer.Ordinal);
        var auftraege = (from k in karten from sz in Szenarien from st in Enumerable.Range(0, startwerte) select (Karte: k, Szenario: sz, Startwert: 101 * (st + 1))).ToList();
        var ergebnisse = new Lauf[auftraege.Count];
        var beginn = DateTime.Now;
        var fertig = 0;
        Parallel.For(0, auftraege.Count, new ParallelOptions { MaxDegreeOfParallelism = parallel }, i =>
        {
            var (karte, sz, st) = auftraege[i];
            try { ergebnisse[i] = Spielen(json, katalog, kartenText[karte], sz, st, jahre); }
            catch (Exception e) { ergebnisse[i] = new Lauf { Fehler = e.Message }; }
            var n = Interlocked.Increment(ref fertig);
            if (n % 20 == 0) Console.WriteLine($"  {n}/{auftraege.Count} Partien ({(DateTime.Now - beginn).TotalMinutes:0.0} Min.)");
        });

        var wurzel = new JsonObject
        {
            ["messung"] = "Selbstfahrer (Paket M, ab K4 mit Hafen als Ziel und Einkauf ohne Händlerspanne)", ["jahre"] = jahre, ["startwerte"] = startwerte,
            ["erklaerung"] = "Reinvermögen des Spielers (Schmied, passiv) nach den Jahren je Szenario: Median, Mittel, kleinstes, größtes über die Startwerte; " +
                             "KI = Mittel aller KI-Häuser. Fahrten = Fahrten zum Markt je Jahr (direkt: zur eigenen Gießerei), Verweilt und MarktWarten = Minuten je Jahr, " +
                             "Stillstand = Anteil der Arbeitszeit, in dem die Betriebe des Spielers standen.",
        };
        var karteJson = new JsonObject();
        foreach (var karte in karten)
        {
            Console.WriteLine();
            Console.WriteLine($"Karte {karte}, {jahre} Jahre, {startwerte} Startwerte – Reinvermögen des Spielers (Median / Mittel / Min–Max), KI im Mittel:");
            var sz0 = new JsonObject();
            foreach (var sz in Szenarien)
            {
                var laeufe = auftraege.Select((a, i) => (a, i)).Where(x => x.a.Karte == karte && x.a.Szenario == sz).Select(x => ergebnisse[x.i]).ToList();
                var fehler = laeufe.Where(l => l.Fehler is not null).Select(l => l.Fehler!).Distinct().ToList();
                var ok = laeufe.Where(l => l.Fehler is null).ToList();
                if (ok.Count == 0) { Console.WriteLine($"  {sz.Name}: nur Fehler – {string.Join(" | ", fehler)}"); continue; }
                var rein = ok.Select(l => l.Rein).OrderBy(x => x).ToList();
                double Mittel(Func<Lauf, double> f) => ok.Average(f);
                var median = rein.Count % 2 == 1 ? rein[rein.Count / 2] : (rein[rein.Count / 2 - 1] + rein[rein.Count / 2]) / 2;
                Console.WriteLine($"  {sz.Name,-46} {median,9:0} / {rein.Average(),9:0} / {rein[0],8:0}–{rein[^1],-8:0}  KI {Mittel(l => l.KiRein),8:0}  " +
                                  $"Fahrten {Mittel(l => l.Fahrten) / jahre,5:0.0} (direkt {Mittel(l => l.FahrtenDirekt) / jahre,4:0.0})  verweilt {Mittel(l => l.Verweilt) / jahre,5:0} min  " +
                                  $"Markt warten {Mittel(l => l.MarktWarten) / jahre,5:0} min  Stillstand {Mittel(l => l.Stillstand) * 100,4:0.0} %  abgelehnt {Mittel(l => l.Abgelehnt),4:0}" +
                                  (fehler.Count > 0 ? $"  Fehler {fehler.Count}: {fehler[0]}" : ""));
                sz0[sz.Name] = new JsonObject
                {
                    ["median"] = Math.Round(median), ["mittel"] = Math.Round(rein.Average()), ["min"] = Math.Round(rein[0]), ["max"] = Math.Round(rein[^1]),
                    ["kiMittel"] = Math.Round(Mittel(l => l.KiRein)), ["fahrtenJeJahr"] = Math.Round(Mittel(l => l.Fahrten) / jahre, 2),
                    ["fahrtenDirektJeJahr"] = Math.Round(Mittel(l => l.FahrtenDirekt) / jahre, 2), ["verweiltMinutenJeJahr"] = Math.Round(Mittel(l => l.Verweilt) / jahre),
                    ["marktWartenMinutenJeJahr"] = Math.Round(Mittel(l => l.MarktWarten) / jahre), ["stillstandProzent"] = Math.Round(Mittel(l => l.Stillstand) * 100, 1),
                    ["abgelehnteBefehle"] = Math.Round(Mittel(l => l.Abgelehnt), 1), ["partien"] = ok.Count, ["fehler"] = fehler.Count,
                    ["werte"] = new JsonArray(ok.Select(l => (JsonNode)JsonValue.Create(Math.Round(l.Rein))!).ToArray()),
                };
            }
            karteJson[karte] = sz0;
        }
        wurzel["karten"] = karteJson;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(datei))!);
        File.WriteAllText(datei, wurzel.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"geschrieben: {Path.GetFullPath(datei)} ({auftraege.Count} Partien, {(DateTime.Now - beginn).TotalMinutes:0.0} Min.)");
        return 0;
    }

    /// <summary>Eine Partie eines Szenarios.</summary>
    static Lauf Spielen(string json, string katalog, string kartenText, Szenario sz, int startwert, int jahre)
    {
        const string zeile = "\"verweilenMinuten\": 20,";
        if (!json.Contains(zeile, StringComparison.Ordinal)) throw new InvalidDataException("logistik.verweilenMinuten nicht gefunden");
        json = json.Replace(zeile, $"\"verweilenMinuten\": {sz.Verweilen},", StringComparison.Ordinal);
        const string handel = "\"handel\": { \"aufschlag\": 0.10, \"marktVon\": 8, \"marktBis\": 20 },";
        if (!json.Contains(handel, StringComparison.Ordinal)) throw new InvalidDataException("handel nicht gefunden");
        json = json.Replace(handel, $"\"handel\": {{ \"aufschlag\": 0.10, \"marktVon\": {(sz.Ganztags ? 4 : 8)}, \"marktBis\": {(sz.Ganztags ? 23 : 20)} }},",
            StringComparison.Ordinal);
        if (sz.OhneHafen)
        {
            const string hafen = "\"karrenZumHafen\": true,";
            if (!json.Contains(hafen, StringComparison.Ordinal)) throw new InvalidDataException("karrenZumHafen nicht gefunden");
            json = json.Replace(hafen, "\"karrenZumHafen\": false,", StringComparison.Ordinal);
        }
        var daten = Wirtschaftsdaten.AusJson(json);
        var karte = Stadtkarte.AusJson(kartenText, Gebaeudekatalog.AusJson(katalog));
        var anzahl = Math.Min(Kartenhaeuser.Anzahl(kartenText), Partiebesetzung.HoechstensKi(karte, Aufstellungsart.Gelost, daten));
        var selbst = sz.Weise != Fahrweise.Automatisch;
        var (sim, spieler) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos, KarrenSelbstFahren = selbst },
            daten, new NeuePartie(Rivalen: anzahl), karte: karte);
        var w = sim.Wirtschaft!;
        var lauf = new Lauf();
        var werkstatt = w.Betriebe.First(b => b.Konto == spieler);
        Betrieb? grube = null;
        if (sz.MitMine)
        {
            var frei = w.FreieLiegenschaften.Where(l => l.Art == Betriebsart.Mine).Select(l => l.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            // Die Gruben gibt es erst ab dem Stand Bürger: Für die Messung steigt der Spieler am Start dorthin auf (in allen Grubenszenarien gleich),
            // und er bekommt das Geld für Pacht bzw. Kauf und das erste Jahr dazu – gemessen wird der Unterschied zwischen den Fahrweisen.
            foreach (var name in frei)
            {
                var l = w.FreieLiegenschaften.First(x => x.Name == name);
                spieler.Stand = Math.Max(spieler.Stand, daten.StandStufe(l.MindestStand));
                spieler.Umsatz(l.Kaufpreis + daten.Fixkosten(l.Art));
                if (!w.Ausfuehren(spieler, new LiegenschaftErwerben(name)).Erfolg) continue;
                grube = w.Betriebe.Single(b => b.Name == name);
                w.Einstellen(grube, "Altknecht", 3);
                break;
            }
            if (grube is null)
                throw new InvalidOperationException("keine freie Grube: " + string.Join(", ", w.FreieLiegenschaften.Select(l => $"{l.Name} ({l.Art}): {w.LiegenschaftErwerbVerboten(spieler, l.Name) ?? "frei"}")));
            lauf.Grube = grube.Name;
        }
        var fahrer = selbst ? new Messspieler(w, spieler, werkstatt, sz.Weise == Fahrweise.SelbstDirekt ? grube : null, lauf, sz.MitHafen) : null;
        var vorher = new Dictionary<Karren, KarrenZustand>(ReferenceEqualityComparer.Instance);
        double stillstand = 0, arbeit = 0;
        for (var jahr = 0; jahr < jahre; jahr++)
        {
            for (var m = 0; m < Spielzeit.MinutenProTag; m++)
            {
                fahrer?.Minute();
                sim.Tick();
                foreach (var k in w.Betriebe.Where(b => b.Konto == spieler).SelectMany(b => b.Karren))
                {
                    var alt = vorher.GetValueOrDefault(k, KarrenZustand.AmBetrieb);
                    if (k.Zustand != alt && k.Unterwegs && !(alt is KarrenZustand.ZumMarkt or KarrenZustand.ZumBetrieb))
                    {
                        if (k.Zustand == KarrenZustand.ZumMarkt) lauf.Fahrten++;
                        else if (k.ZielBetrieb is not null) lauf.FahrtenDirekt++;
                    }
                    vorher[k] = k.Zustand;
                    if (k.Verweilt(sim.Zeit)) lauf.Verweilt++;
                    if (k.Zustand == KarrenZustand.AmMarkt) lauf.MarktWarten++;
                }
            }
            foreach (var b in w.Betriebe.Where(b => b.Konto == spieler))
            {
                stillstand += b.StillstandMinutenVorjahr;
                arbeit += b.Arbeitsminuten;
            }
        }
        lauf.Rein = spieler.Reinvermoegen;
        var ki = w.Konten.Where(k => k != spieler).ToList();
        lauf.KiRein = ki.Count > 0 ? ki.Average(k => k.Reinvermoegen) : 0;
        lauf.Stillstand = arbeit > 0 ? stillstand / arbeit : 0;
        return lauf;
    }

    /// <summary>
    /// Der Messspieler: fährt die Karren des Spielers mit den Befehlen des Selbstfahrens nach einer einfachen Regel (siehe Kopf). Jede Minute vor dem Takt.
    /// Mit <c>direkt</c> fährt der Karren dieser Grube ihr Erz zuerst zur Gießerei (soweit sie es braucht), den Rest zum Markt.
    /// </summary>
    sealed class Messspieler
    {
        private readonly Stadtwirtschaft _w;
        private readonly Konto _k;
        private readonly Betrieb _werkstatt;
        private readonly Betrieb? _direkt;
        private readonly Lauf _lauf;
        private readonly bool _mitHafen;
        private readonly Dictionary<Karren, long> _wareWartetSeit = new(ReferenceEqualityComparer.Instance);

        public Messspieler(Stadtwirtschaft w, Konto k, Betrieb werkstatt, Betrieb? direkt, Lauf lauf, bool mitHafen) =>
            (_w, _k, _werkstatt, _direkt, _lauf, _mitHafen) = (w, k, werkstatt, direkt, lauf, mitHafen);

        private LogistikDaten L => _w.Daten.Logistik;

        private bool Befehl(Befehl b)
        {
            var e = _w.Ausfuehren(_k, b);
            if (!e.Erfolg) _lauf.Abgelehnt++;
            return e.Erfolg;
        }

        private bool MarktOffen(long minute)
        {
            var h = _w.Daten.Handel!;
            var uhr = Spielzeit.TagesbeginnMinute + (int)(minute % Spielzeit.MinutenProTag);
            return uhr >= h.MarktVon * 60 && uhr < h.MarktBis * 60;
        }

        public void Minute()
        {
            foreach (var b in _w.Betriebe.Where(x => x.Konto == _k).ToList())
                for (var i = 0; i < b.Karren.Count; i++)
                    Karren(b, i);
        }

        private void Karren(Betrieb heimat, int nr)
        {
            var k = heimat.Karren[nr];
            if (k.Unterwegs) return;
            var jetzt = _w.Uhr.VergangeneMinuten;
            if (k.Zustand == KarrenZustand.AmMarkt)
            {
                if (!MarktOffen(jetzt)) return;
                var verkauf = k.Ladung.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
                var kauf = Einkauf(heimat, k, verkauf);
                if (verkauf.Count > 0 || kauf.Count > 0)
                    if (!Befehl(new KarrenHandeln(heimat.Name, nr, verkauf, kauf)) && kauf.Count > 0 && verkauf.Count > 0)
                        Befehl(new KarrenHandeln(heimat.Name, nr, verkauf, new Dictionary<string, int>()));
                Befehl(new KarrenZurueck(heimat.Name, new[] { nr }));
                return;
            }
            var ort = k.StandortVon(_w)!;
            if (ort != heimat)
            {
                // Direkt geliefert: abladen, was die Gießerei braucht; der Rest zum Markt, sonst heim.
                var ab = k.Ladung.Where(x => ort.Zutaten.ContainsKey(x.Key))
                    .Select(x => (x.Key, Stueck: Math.Min(x.Value, Math.Max(0, ort.LagerKapazitaetJeWare - ort.ImLager(x.Key)))))
                    .Where(x => x.Stueck > 0).ToDictionary(x => x.Key, x => x.Stueck, StringComparer.Ordinal);
                if (ab.Count > 0) Befehl(new KarrenAbladen(heimat.Name, nr, ab));
                if (k.Ladung.Count > 0)
                {
                    Befehl(new KarrenLosschicken(heimat.Name, new[] { nr }, null));
                    return;
                }
                Befehl(new KarrenZurueck(heimat.Name, new[] { nr }));
                return;
            }
            // Daheim: Mitgebrachtes abladen, dann wie der Verwalter entscheiden.
            if (k.Ladung.Count > 0) Befehl(new KarrenAbladen(heimat.Name, nr));
            if (heimat.Ort == Arbeitsort.Umland && _w.Uhr.Tagesphase == Tagesphase.DunkleNacht && !k.NachtsFahren) return;
            var knapp = heimat.Zutaten.Count > 0 && heimat.ZutatenReichweite < L.NachkaufUnterStueck;
            var ware = heimat.Verkaufsware().ToList();
            if (ware.Count == 0) _wareWartetSeit.Remove(k);
            else if (!_wareWartetSeit.ContainsKey(k)) _wareWartetSeit[k] = jetzt;
            var plan = LadePlan(k, ware);
            var genug = plan.Values.Sum() >= Math.Ceiling(k.Daten.Gesamtkapazitaet * L.AbfahrtAbFuellgrad)
                        || (ware.Count > 0 && ware.Max(x => heimat.ImLager(x.Ware)) >= heimat.LagerKapazitaetJeWare * L.AbfahrtAbFuellgrad)
                        || (_wareWartetSeit.TryGetValue(k, out var seit) && jetzt - seit >= L.MaxWartezeitMinuten);
            if (!knapp && !genug) return;

            // Direkt: Braucht die eigene Gießerei Erz, fährt die Grube es dorthin (ankommen vor dem Torschluss).
            if (_direkt == heimat && plan.Count > 0)
            {
                var noetig = plan.Where(x => _werkstatt.Zutaten.ContainsKey(x.Key) && _werkstatt.ImLager(x.Key) < _werkstatt.LagerKapazitaetJeWare / 2).ToList();
                if (noetig.Count > 0)
                {
                    var an = jetzt + _w.KarrenFahrt(k, heimat, _werkstatt).Minuten;
                    var uhr = Spielzeit.TagesbeginnMinute + (int)(an % Spielzeit.MinutenProTag);
                    if (uhr >= 20 * 60) return;
                    if (Befehl(new KarrenBeladen(heimat.Name, nr, plan))) _wareWartetSeit.Remove(k);
                    Befehl(new KarrenLosschicken(heimat.Name, new[] { nr }, _werkstatt.Name));
                    return;
                }
            }
            if (plan.Count > 0 && Befehl(new KarrenBeladen(heimat.Name, nr, plan))) _wareWartetSeit.Remove(k);
            // Wie der Verwalter zum näheren Handelsort (Hafen, wenn er offen und die Rundfahrt dorthin kürzer ist).
            Befehl(new KarrenLosschicken(heimat.Name, new[] { nr }, null, Hafen: _mitHafen && _w.FaehrtZumHafen(heimat, k.Typ)));
        }

        /// <summary>Wie der Verwalter: jede Ladefläche mit der Ware, von der am meisten übrig ist.</summary>
        private static Dictionary<string, int> LadePlan(Karren k, IEnumerable<(string Ware, int Stueck)> ware)
        {
            var rest = ware.ToDictionary(x => x.Ware, x => x.Stueck, StringComparer.Ordinal);
            var plan = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var f = 0; f < k.Daten.Sorten; f++)
            {
                var naechste = rest.Where(r => r.Value > 0).OrderByDescending(r => r.Value).ThenBy(r => r.Key, StringComparer.Ordinal).Select(r => r.Key).FirstOrDefault();
                if (naechste is null) break;
                var menge = Math.Min(rest[naechste], k.Daten.KapazitaetJeSorte);
                plan[naechste] = plan.GetValueOrDefault(naechste) + menge;
                rest[naechste] -= menge;
            }
            return plan;
        }

        /// <summary>
        /// Wie der Verwalter (Karren.ZutatenEinkaufen): die knappste Zutat zuerst, bis zum Vorrat für einige Arbeitsstunden, höchstens die Hälfte des
        /// freien Geldes – zum Preis des Selbstfahrens (<see cref="Stadtwirtschaft.KarrenKaufpreis"/>). Die Ladeflächen nach dem Verkauf der ganzen Ladung.
        /// </summary>
        private Dictionary<string, int> Einkauf(Betrieb b, Karren k, IReadOnlyDictionary<string, int> verkauf)
        {
            var kauf = new Dictionary<string, int>(StringComparer.Ordinal);
            if (b.Zutaten.Count == 0) return kauf;
            var minutenJeRunde = b.WarenInArbeit.Sum(b.MinutenJeStueck);
            var rundenJeStunde = minutenJeRunde > 0 ? Kalkulation.Leistung(_w, b) * 60 / minutenJeRunde : 0;
            var runden = Math.Max(2.0 * L.NachkaufUnterStueck, rundenJeStunde * L.ZutatenVorratStunden);
            var budget = Math.Max(0, (_k.Vermoegen - _w.OffeneJahreskosten(_k)) * L.ZutatenAnteilKasse);
            var stockt = b.ZutatenReichweite < L.NachkaufUnterStueck;
            var frei = k.Daten.Sorten - k.Flaechen(k.Ladung.Select(x => new KeyValuePair<string, int>(x.Key, x.Value - verkauf.GetValueOrDefault(x.Key))));
            for (var f = 0; f < frei; f++)
            {
                var knappste = b.Zutaten
                    .Select(z => (Zutat: z.Key, Menge: z.Value, Bestand: b.ImLager(z.Key) + kauf.GetValueOrDefault(z.Key),
                                  Ziel: (int)Math.Min(b.LagerKapazitaetJeWare, Math.Ceiling(runden * z.Value))))
                    .Where(z => z.Bestand < z.Ziel)
                    .OrderBy(z => z.Bestand / z.Menge).ThenBy(z => z.Zutat, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (knappste.Zutat is null) break;
                var preis = _w.KarrenKaufpreis(knappste.Zutat, 1);
                var platz = Math.Min(k.Daten.KapazitaetJeSorte, knappste.Ziel - knappste.Bestand);
                var menge = (int)Math.Min(platz, Math.Floor(budget / preis));
                var noetig = stockt ? Math.Min(platz, L.NachkaufUnterStueck * knappste.Menge - knappste.Bestand) : 0;
                if (menge < noetig) menge = (int)Math.Min(noetig, Math.Floor(Math.Max(0, _k.Vermoegen) / preis));
                if (menge <= 0) break;
                budget -= menge * preis;
                kauf[knappste.Zutat] = kauf.GetValueOrDefault(knappste.Zutat) + menge;
            }
            return kauf;
        }
    }
}
