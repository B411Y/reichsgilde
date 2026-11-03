using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Gefahren;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Blitzschaden (Variante B, Nutzer 3.11.2026): Werkstatt zahlt eine Reparatur nach dem Bauwert und verliert einen Teil des Lagers, das Wohnhaus
/// zahlt eine Reparatur nach der Formel der Brandstiftung – je nach Schwere (aus der gewürfelten Brenndauer). Wächter löschen mit, das Löschfass
/// senkt den Schaden. Das Feuer springt höchstens einmal aufs Nachbarhaus. Die Einschläge stehen im Spielstand (auch der Entwicklerblitz, auch
/// nach einem Umbau dasselbe Haus); alte Stände ohne Feld: Daten 68 bis 70 rechnen nach, ältere beginnen ohne Einschläge.
/// </summary>
static class BlitzschadenTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 11, 3, 12, 0, 0);

    /// <summary>Die echten Daten, ein Wert ersetzt (Text muss genau einmal vorkommen).</summary>
    static Wirtschaftsdaten Mit(params (string Alt, string Neu)[] ersetzen)
    {
        var json = Json.Value;
        foreach (var (alt, neu) in ersetzen)
        {
            Pruefe.Wahr(json.Split(alt).Length == 2, $"„{alt}“ genau einmal in wirtschaft.json");
            json = json.Replace(alt, neu);
        }
        return Wirtschaftsdaten.AusJson(json);
    }

    static readonly (string, string) OhneGewitter = ("\"chance\": 0.25,\n    \"dauerMinuten\"", "\"chance\": 0,\n    \"dauerMinuten\"");
    static readonly (string, string) Gewitter1 = ("\"chance\": 0.25,\n    \"dauerMinuten\"", "\"chance\": 1,\n    \"dauerMinuten\"");
    static readonly (string, string) SpringtNie = ("\"chanceJeStunde\": 0.15", "\"chanceJeStunde\": 0");
    static readonly (string, string) SpringtImmer = ("\"chanceJeStunde\": 0.15", "\"chanceJeStunde\": 1");

    static (Simulation Sim, Konto Spieler) Partie(Wirtschaftsdaten daten, int startwert = 1400, string karte = "greybox")
    {
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            daten, new NeuePartie(), karte: Stadtkarte.AusDatei(Datenpfad.Karte(karte)));
        sim.EntwicklerErlaubt = true;
        return (sim, spieler);
    }

    static void BisMinute(Simulation sim, long minute)
    {
        while (sim.Zeit.VergangeneMinuten < minute)
        {
            sim.Tick();
            while (sim.Ratssitzung is not null) sim.RatssitzungWeiter();
        }
    }

    static Blitzeinschlag Blitz(Simulation sim, Konto spieler, string gebaeude)
    {
        var vorher = sim.Wirtschaft!.Blitz.Einschlaege.Count;
        var e = sim.Bote.SofortAusfuehren(spieler, new EntwicklerBlitz(gebaeude));
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        Pruefe.Gleich(vorher + 1, sim.Wirtschaft.Blitz.Einschlaege.Count, "ein neuer Einschlag");
        return sim.Wirtschaft.Blitz.Einschlaege[^1];
    }

    /// <summary>Ein Gebäude mit mindestens einem Nachbarn (Grundflächen höchstens <c>sprung.abstand</c> auseinander) – Kennung und Nachbarn.</summary>
    static (Blitzziel Ziel, List<Blitzziel> Nachbarn) MitNachbarn(Stadtwirtschaft w, Func<Blitzziel, bool>? auswahl = null)
    {
        var a = w.Daten.Blitz!.Sprung!.Abstand;
        var ziele = w.Blitz.Ziele();
        foreach (var z in ziele.Where(auswahl ?? (_ => true)))
        {
            var n = ziele.Where(x => x.Kennung != z.Kennung && z.AbstandImQuadrat(x) <= a * a).OrderBy(x => x.Kennung, StringComparer.Ordinal).ToList();
            if (n.Count > 0) return (z, n);
        }
        throw new Exception("Kein Gebäude mit Nachbarn");
    }

    static double Erwartet(double[] paar, double schwere) => paar[0] + (paar[1] - paar[0]) * schwere;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Blitzschaden: Daten – Reparatur 10–25 % vom Bauwert (mindestens 100), Lager 10–40 %, Wächter ein Drittel kürzer, Sprung 6 m / 15 %; ohne Werte keine Wirkung", () =>
        {
            var d = Wirtschaftsdaten.AusJson(Json.Value).Blitz!;
            Pruefe.Gleich((0.10, 0.25, 100.0), (d.ReparaturAnteil[0], d.ReparaturAnteil[1], d.Reparatur));
            Pruefe.Gleich((0.10, 0.40), (d.LagerAnteil[0], d.LagerAnteil[1]));
            Pruefe.Gleich(0.67, d.WaechterFaktor);
            Pruefe.Gleich((6.0, 0.15), (d.Sprung!.Abstand, d.Sprung.ChanceJeStunde));
            // Vorgaben (Daten bis 70 haben die Felder nicht): ohne Wirkung
            var alt = new BlitzDaten();
            Pruefe.Gleich((0.0, 0.0, 0.0, 1.0), (alt.ReparaturAnteil[1], alt.LagerAnteil[1], alt.WohnhausAnteil[1], alt.WaechterFaktor));
            Pruefe.Wahr(alt.Sprung is null, "ohne Sprung");
            var f = new List<string>();
            new BlitzDaten { LagerAnteil = new[] { 0.5, 1.5 }, ReparaturAnteil = new[] { 0.3, 0.1 }, WaechterFaktor = 0 }.Pruefen(f);
            Pruefe.Gleich(3, f.Count, string.Join(" | ", f));
        }),

        ("Blitzschaden: Schwere aus der gewürfelten Dauer – 60 Minuten leicht (0), 120 halb, 180 schwer (1)", () =>
        {
            var (sim, _) = Partie(Mit(OhneGewitter));
            var b = sim.Wirtschaft!.Blitz;
            Pruefe.Gleich((0.0, 0.5, 1.0), (b.Schwere(60), b.Schwere(120), b.Schwere(180)));
        }),

        ("Blitzschaden: Werkstatt – Reparatur nach Bauwert und Schwere, ein Teil jeder Ware im Lager verbrennt (ganze Stück, abgerundet), Meldung nennt beides", () =>
        {
            var (sim, spieler) = Partie(Mit(OhneGewitter, SpringtNie));
            BisMinute(sim, 300);
            var w = sim.Wirtschaft!;
            var b = w.Betriebe.First(x => x.Konto == spieler && x.IstWerkstatt);
            foreach (var ware in b.Lager.Keys.ToList()) b.Auslagern(ware, 1000);
            b.Einlagern("Eisen", 20);
            b.Einlagern(b.Produkt, 7);
            var meldungen = new List<string>();
            w.Meldung += m => meldungen.Add(m);
            var geld = spieler.Vermoegen;
            var e = Blitz(sim, spieler, "werkstatt");
            var d = w.Daten.Blitz!;
            Pruefe.Ungefaehr(w.Blitz.Schwere((int)(e.BrandEnde - e.Minute)), e.Schwere, 1e-12);
            var reparatur = Math.Round(Math.Max(d.Reparatur, Erwartet(d.ReparaturAnteil, e.Schwere) * w.Bauwert(b)) * b.BrandFaktor);
            Pruefe.Wahr(w.Bauwert(b) >= 3000, $"Bauwert {w.Bauwert(b)}");
            Pruefe.Ungefaehr(geld - reparatur, spieler.Vermoegen, 1e-6);
            var anteil = Erwartet(d.LagerAnteil, e.Schwere);
            Pruefe.Gleich(20 - (int)Math.Floor(20 * anteil), b.ImLager("Eisen"), $"Eisen (Anteil {anteil})");
            Pruefe.Gleich(7 - (int)Math.Floor(7 * anteil), b.ImLager(b.Produkt), $"{b.Produkt}");
            Pruefe.Gleich(d.StillstandMinuten, b.SabotageMinuten, "Stillstand wie bisher");
            var m = meldungen.Single(x => x.StartsWith("Blitzschlag! "));
            Pruefe.Wahr(m.Contains($"Reparatur {Zahlenformat.Ganz(reparatur)}") && m.Contains("verbrannt: ") && m.Contains(" Eisen"), m);
        }),

        ("Blitzschaden: Wohnhaus – Ansehen fehlt wie bisher, Reparatur = Brandschaden der Brandstiftung × (0,5 … 1 nach Schwere)", () =>
        {
            var (sim, spieler) = Partie(Mit(OhneGewitter, SpringtNie));
            BisMinute(sim, 300);
            var w = sim.Wirtschaft!;
            var geld = spieler.Vermoegen;
            var e = Blitz(sim, spieler, "haus");
            var d = w.Daten.Blitz!;
            var erwartet = Math.Round(Math.Max(d.WohnhausReparatur, Erwartet(d.WohnhausAnteil, e.Schwere) * w.Gauner.Brandschaden(spieler)) * w.Wohnen.BrandFaktor(spieler));
            Pruefe.Wahr(erwartet >= 50, $"Reparatur {erwartet}");
            Pruefe.Ungefaehr(geld - erwartet, spieler.Vermoegen, 1e-6);
            Pruefe.Gleich(e.BrandEnde + d.InstandMinuten, spieler.WohnhausInstandBis);
            Pruefe.Wahr(w.Blitz.WohnhausBrand(spieler) == e, "WohnhausBrand findet den Einschlag (fürs Bild)");
        }),

        ("Blitzschaden: Wächter – das Feuer brennt ein Drittel kürzer und ist leichter (gleicher Wurf); Löschfass senkt die Reparatur", () =>
        {
            Blitzeinschlag Lauf(bool waechter, out double reparatur)
            {
                var (sim, spieler) = Partie(Mit(OhneGewitter, SpringtNie));
                BisMinute(sim, 300);
                var b = sim.Wirtschaft!.Betriebe.First(x => x.Konto == spieler && x.IstWerkstatt);
                b.Waechter = waechter;
                var geld = spieler.Vermoegen;
                var e = Blitz(sim, spieler, "werkstatt");
                reparatur = geld - spieler.Vermoegen;
                return e;
            }
            var ohne = Lauf(false, out var r1);
            var mit = Lauf(true, out var r2);
            var d = Wirtschaftsdaten.AusJson(Json.Value).Blitz!;
            Pruefe.Gleich((long)Math.Max(1, Math.Round((ohne.BrandEnde - ohne.Minute) * d.WaechterFaktor)), mit.BrandEnde - mit.Minute, "kürzer");
            Pruefe.Ungefaehr(ohne.Schwere * d.WaechterFaktor, mit.Schwere, 1e-12);
            Pruefe.Wahr(r2 <= r1, $"Reparatur mit Wächter {r2} ≤ ohne {r1}");
        }),

        ("Blitzschaden: Alte Werte (Daten 68: Reparatur 0, keine Anteile, kein Sprung) – nur Stillstand, kein Geld, kein Lager (Verhalten wie bisher)", () =>
        {
            var knoten = JsonNode.Parse(Json.Value)!;
            var blitz = knoten["blitz"]!.AsObject();
            foreach (var feld in new[] { "reparaturAnteil", "lagerAnteil", "waechterFaktor", "wohnhausAnteil", "sprung" }) blitz.Remove(feld);
            blitz["reparatur"] = 0;
            var (sim, spieler) = Partie(Wirtschaftsdaten.AusJson(knoten.ToJsonString()));
            BisMinute(sim, 300);
            var w = sim.Wirtschaft!;
            var b = w.Betriebe.First(x => x.Konto == spieler && x.IstWerkstatt);
            b.Einlagern("Eisen", 20);
            b.Waechter = true;
            var geld = spieler.Vermoegen;
            var eisen = b.ImLager("Eisen");
            var e = Blitz(sim, spieler, "werkstatt");
            Pruefe.Gleich(geld, spieler.Vermoegen, "kein Geld");
            Pruefe.Gleich(eisen, b.ImLager("Eisen"), "kein Lager");
            Pruefe.Gleich(w.Daten.Blitz!.StillstandMinuten, b.SabotageMinuten);
            Pruefe.Wahr(e.BrandEnde - e.Minute is >= 60 and <= 180, "Wächter ohne Wirkung");
            Pruefe.Gleich(0, w.Blitz.Anstehend.Count, "kein Sprung");
        }),

        ("Blitzschaden: Sprung (Chance 1) – jeder Nachbar fängt zur Mitte der ersten Stunde Feuer, kürzer und leichter, und springt nicht weiter", () =>
        {
            var (sim, spieler) = Partie(Mit(OhneGewitter, SpringtImmer));
            BisMinute(sim, 300);
            var w = sim.Wirtschaft!;
            var (ziel, nachbarn) = MitNachbarn(w);
            var meldungen = new List<string>();
            w.Meldung += m => meldungen.Add(m);
            var e = Blitz(sim, spieler, ziel.Kennung);
            var sp = w.Daten.Blitz!.Sprung!;
            Pruefe.Gleich(nachbarn.Count, w.Blitz.Anstehend.Count, "ein Sprung je Nachbar, gewürfelt beim Einschlag");
            Pruefe.Gleich(string.Join(",", nachbarn.Select(n => n.Kennung)), string.Join(",", w.Blitz.Anstehend.Select(j => j.Ziel.Kennung)), "nach Kennung");
            BisMinute(sim, e.Minute + 31);
            Pruefe.Gleich(0, w.Blitz.Anstehend.Count);
            var spruenge = w.Blitz.Einschlaege.Where(x => x.IstUebersprung).ToList();
            Pruefe.Gleich(nachbarn.Count, spruenge.Count, "alle Nachbarn brennen");
            foreach (var j in spruenge)
            {
                Pruefe.Gleich(e.Minute + 30, j.Minute);
                Pruefe.Wahr(j.BrandEnde - j.Minute < e.BrandEnde - e.Minute, "kürzer");
                Pruefe.Wahr(j.Schwere <= e.Schwere * sp.SchwereFaktor + 1e-12, "leichter");
                Pruefe.Gleich(ziel.Name, j.Von);
                Pruefe.Wahr(w.Blitz.Brand(j.Ziel.Kennung) == j, "brennt");
            }
            Pruefe.Wahr(meldungen.Count(m => m.StartsWith("Funkenflug! ")) == nachbarn.Count, string.Join(" | ", meldungen));
            // Kein zweiter Sprung: Die übergesprungenen Feuer würfeln nicht
            BisMinute(sim, e.BrandEnde + 200);
            Pruefe.Gleich(1 + nachbarn.Count, w.Blitz.Einschlaege.Count, "keine Kette");
        }),

        ("Blitzschaden: Sprung auf eine Werkstatt bucht deren Schaden erst, wenn das Feuer überspringt", () =>
        {
            var (sim, spieler) = Partie(Mit(OhneGewitter, SpringtImmer));
            BisMinute(sim, 300);
            var w = sim.Wirtschaft!;
            var nachbar = default(Blitzziel);
            var (ziel, _) = MitNachbarn(w, z =>
            {
                var a = w.Daten.Blitz!.Sprung!.Abstand;
                nachbar = w.Blitz.Ziele().FirstOrDefault(x => x.Kennung != z.Kennung && x.Betrieb is { IstWerkstatt: true } && z.AbstandImQuadrat(x) <= a * a);
                return nachbar is not null;
            });
            var b = nachbar!.Betrieb!;
            var e = Blitz(sim, spieler, ziel.Kennung);
            var vorher = b.SabotageMinuten;
            BisMinute(sim, e.Minute + 30);
            Pruefe.Gleich(vorher, b.SabotageMinuten, "noch nicht");
            BisMinute(sim, e.Minute + 31);
            Pruefe.Wahr(b.SabotageMinuten > vorher, $"{b.Name} repariert nach dem Übersprung");
        }),

        ("Blitzschaden: Sprung ist deterministisch – zwei Läufe Köln, acht Jahre, Chance 1 je Gewitter, ergeben dieselben Einschläge und Sprünge", () =>
        {
            string Lauf()
            {
                var (sim, _) = Partie(Mit(Gewitter1, ("\"chanceJeStunde\": 0.15", "\"chanceJeStunde\": 0.6")), startwert: 1401, karte: "koeln");
                var liste = new List<string>();
                sim.Wirtschaft!.Meldung += m => { if (m.StartsWith("Blitzschlag!") || m.StartsWith("Funkenflug!")) liste.Add($"{sim.Zeit.VergangeneMinuten} {m}"); };
                BisMinute(sim, 8L * Spielzeit.MinutenProTag);
                Pruefe.Wahr(liste.Any(m => m.Contains("Funkenflug!")), "mindestens ein Sprung: " + string.Join(" | ", liste));
                return string.Join("\n", liste);
            }
            Pruefe.Gleich(Lauf(), Lauf());
        }),

        ("Blitzschaden: Speichern und Laden mitten im Brand – Einschlag aus dem Spielstand (Entwicklerblitz, Betrieb per Name), Sprung wartet und wirkt einmal", () =>
        {
            var (sim, spieler) = Partie(Mit(OhneGewitter, SpringtImmer));
            BisMinute(sim, 300);
            var w = sim.Wirtschaft!;
            var (ziel, nachbarn) = MitNachbarn(w, z => z.Betrieb is not null);
            var e = Blitz(sim, spieler, ziel.Kennung);
            BisMinute(sim, e.Minute + 10);
            // Laden vor dem Sprung: Die Sprünge stehen im Spielstand und kommen einmal
            var (geladen, gs) = Speicherung.Laden(Speicherung.Speichern(sim, spieler, Zeitpunkt));
            var gw = geladen.Wirtschaft!;
            Pruefe.Gleich(nachbarn.Count, gw.Blitz.Anstehend.Count, "Sprünge im Spielstand");
            var g = gw.Blitz.Einschlaege.Single();
            Pruefe.Gleich((e.Minute, e.BrandEnde, e.Ziel.Kennung, e.Ziel.Name, e.Schwere), (g.Minute, g.BrandEnde, g.Ziel.Kennung, g.Ziel.Name, g.Schwere));
            Pruefe.Gleich(e.Ziel.Betrieb!.Name, g.Ziel.Betrieb?.Name, "Betrieb per Name");
            BisMinute(sim, e.Minute + 45);
            BisMinute(geladen, e.Minute + 45);
            Pruefe.Gleich(w.Blitz.Einschlaege.Count, gw.Blitz.Einschlaege.Count, "gleich viele Feuer");
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(sim), Zustandspruefsumme.Berechnen(geladen), "Prüfsumme nach dem Sprung");
            // Laden mitten im übergesprungenen Brand: kein zweiter Schaden, gleiche Prüfsumme
            var j = gw.Blitz.Einschlaege.First(x => x.IstUebersprung);
            var (zweit, _) = Speicherung.Laden(Speicherung.Speichern(geladen, gs, Zeitpunkt));
            var zj = zweit.Wirtschaft!.Blitz.Einschlaege.Single(x => x.IstUebersprung && x.Ziel.Kennung == j.Ziel.Kennung);
            Pruefe.Gleich((j.Minute, j.BrandEnde, j.Von, Brandursache.Uebersprung), (zj.Minute, zj.BrandEnde, zj.Von, zj.Ursache));
            Pruefe.Wahr(zweit.Wirtschaft.Blitz.Brand(j.Ziel.Kennung) is not null, "brennt nach dem Laden weiter");
            BisMinute(geladen, e.BrandEnde + 5);
            BisMinute(zweit, e.BrandEnde + 5);
            BisMinute(sim, e.BrandEnde + 5);
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(sim), Zustandspruefsumme.Berechnen(zweit), "Prüfsumme nach zweimal Laden");
            Pruefe.Gleich(Zustandspruefsumme.Berechnen(geladen), Zustandspruefsumme.Berechnen(zweit), "Prüfsumme geladen/zweit");
        }),

        ("Blitzschaden: Wohnhaus aus dem Spielstand – Bewohner per Kontonummer, das Bild findet den Brandbeginn (WohnhausBrand) auch nach dem Laden", () =>
        {
            var (sim, spieler) = Partie(Mit(OhneGewitter, SpringtNie));
            BisMinute(sim, 300);
            var e = Blitz(sim, spieler, "haus");
            BisMinute(sim, 330);
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler, Zeitpunkt));
            var g = geladen.Wirtschaft!.Blitz.Einschlaege.Single();
            Pruefe.Gleich((e.Ziel.Kennung, e.Ziel.Name, e.Minute, e.BrandEnde), (g.Ziel.Kennung, g.Ziel.Name, g.Minute, g.BrandEnde));
            Pruefe.Wahr(g.Ziel.Bewohner is { } k && k.Inhaber == spieler.Inhaber, "Bewohner per Kontonummer");
            Pruefe.Wahr(geladen.Wirtschaft.Blitz.WohnhausBrand(g.Ziel.Bewohner!) == g, "WohnhausBrand nach dem Laden");
        }),

        ("Blitzschaden: Alter Spielstand ohne Feld – Daten 68 bis 70 rechnen nach wie bisher, Daten 67 beginnen ohne Einschläge (keine Geisterfeuer)", () =>
        {
            // Ein Gewitter mit Chance 1, Spielstand mitten im Brand – dann das Feld „Blitz“ entfernen
            var daten = Mit(Gewitter1, SpringtNie);
            var (sim, spieler) = Partie(daten);
            var w = sim.Wirtschaft!;
            long gewitter = -1;
            for (var jahr = 0; jahr < 40 && gewitter < 0; jahr++)
                foreach (var m in w.Blitz.Gewitterminuten(jahr))
                    if (m > 0) { gewitter = m; break; }
            BisMinute(sim, gewitter + 20);
            var e = w.Blitz.Einschlaege.Single();
            string OhneFeld(int version)
            {
                var knoten = JsonNode.Parse(Speicherung.Speichern(sim, spieler, Zeitpunkt))!;
                knoten["Wirtschaft"]!.AsObject().Remove("Blitz");
                knoten["Daten"]!["Version"] = version;
                return knoten.ToJsonString();
            }
            foreach (var version in new[] { 68, 69, 70 }) // 69 Jahreszeiten, 70 Hafen ruht – beide ohne Feld „Blitz“
            {
                var (neu, _) = Speicherung.Laden(OhneFeld(version));
                Pruefe.Wahr(neu.Wirtschaft!.Blitz.Einschlaege.SingleOrDefault() is { } n && n.Minute == e.Minute && n.BrandEnde == e.BrandEnde, $"Daten {version}: nachgerechnet");
            }
            var (alt, _) = Speicherung.Laden(OhneFeld(67));
            Pruefe.Gleich(0, alt.Wirtschaft!.Blitz.Einschlaege.Count, "Daten 67: keine Einschläge");
            BisMinute(alt, gewitter + 60);
            Pruefe.Gleich(0, alt.Wirtschaft.Blitz.Einschlaege.Count, "auch danach keine");
        }),

        ("Blitzschaden: Entwicklerblitz übersteht das Laden (früher verschwand das Feuer)", () =>
        {
            var (sim, spieler) = Partie(Mit(OhneGewitter, SpringtNie));
            BisMinute(sim, 300);
            var e = Blitz(sim, spieler, "Rathaus");
            BisMinute(sim, 320);
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler, Zeitpunkt));
            Pruefe.Wahr(geladen.Wirtschaft!.Blitz.Brand(e.Ziel.Kennung) is { } g && g.Minute == e.Minute, "brennt nach dem Laden");
        }),
    };
}
