using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Gefahren;
using Reichsgilde.Core.Kirche;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Blitz auf Kirche, Dom und Rathaus und die Brandkasse (Nutzer 22.11.2026, Datenversion 95, Varianten A1, B1, C1, D1, E1):
/// eine brennende Kirche hält keine Messe; der Dom bis zur Instandsetzung auch nicht, die Stadtkasse zahlt (nicht unter 0); steht das Rathaus
/// zur Ratssitzung nicht wieder, entfallen die Anträge; die städtische Brandkasse zahlt gedeckten Mitgliedern 60 % der Reparatur.
/// Jeder Test mit Gegenprobe.
/// </summary>
static class BlitzStadtbauTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 11, 22, 12, 0, 0);

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
    static readonly (string, string) SpringtNie = ("\"chanceJeStunde\": 0.15", "\"chanceJeStunde\": 0");

    /// <summary>Die Daten wie vor Datenversion 95: ohne „ohneMesse“, „stadtbau“ und „brandkasse“.</summary>
    static Wirtschaftsdaten Alt()
    {
        var knoten = JsonNode.Parse(Json.Value.Replace(OhneGewitter.Item1, OhneGewitter.Item2).Replace(SpringtNie.Item1, SpringtNie.Item2))!;
        var blitz = knoten["blitz"]!.AsObject();
        blitz.Remove("ohneMesse");
        blitz.Remove("stadtbau");
        blitz.Remove("brandkasse");
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    static (Simulation Sim, Konto Spieler) Partie(Wirtschaftsdaten? daten = null, int startwert = 1400, NeuePartie? neu = null)
    {
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            daten ?? Mit(OhneGewitter, SpringtNie), neu ?? new NeuePartie(), karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
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

    /// <summary>Die erste Minute des nächsten Spieljahres (04:00), Spielminuten seit Beginn.</summary>
    static long NaechstesJahr(Simulation sim) =>
        (sim.Zeit.VergangeneMinuten / Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag + 1) * Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;

    static Blitzeinschlag Blitz(Simulation sim, Konto spieler, string gebaeude)
    {
        var e = sim.Bote.SofortAusfuehren(spieler, new EntwicklerBlitz(gebaeude));
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        return sim.Wirtschaft!.Blitz.Einschlaege[^1];
    }

    static Blitzziel Stadtbau(Stadtwirtschaft w, Stadtbauart art) => w.Blitz.Ziele().Single(z => z.Stadtbau == art);

    /// <summary>Die Kirche des Spielers (er beginnt als Prediger), die der Blitz treffen kann.</summary>
    static Blitzziel Kirche(Stadtwirtschaft w, Konto spieler) =>
        w.Blitz.Ziele().FirstOrDefault(z => z.Betrieb is { } b && b.Konto == spieler && w.Kirchenwesen.IstKirche(b) && w.Kirchenwesen.Kirchen.Contains(b))
        ?? throw new Exception("Keine Kirche des Spielers");

    static void DomInhaber(Domamt d, Konto k) => typeof(Domamt).GetProperty("Inhaber")!.SetValue(d, k);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Blitz Stadtbau: Daten – Dom 400–1.000, Rathaus 300–800 aus der Stadtkasse, Messe fällt aus, Brandkasse 2 % / mind. 20 / 60 %", () =>
        {
            var d = Wirtschaftsdaten.AusJson(Json.Value);
            Pruefe.Wahr(d.Version >= 95, "Datenversion");
            var b = d.Blitz!;
            Pruefe.Wahr(b.OhneMesse, "ohneMesse");
            Pruefe.Gleich(Stadtbauart.Dom, b.Stadtbau["dom"].Art);
            Pruefe.Gleich(Stadtbauart.Dom, b.Stadtbau["koelner_dom"].Art);
            Pruefe.Gleich(Stadtbauart.Rathaus, b.Stadtbau["rathaus"].Art);
            Pruefe.Gleich(Stadtbauart.Rathaus, b.Stadtbau["koelner_rathaus"].Art);
            Pruefe.Ungefaehr(400, b.Stadtbau["dom"].Reparatur[0], 1e-9);
            Pruefe.Ungefaehr(800, b.Stadtbau["rathaus"].Reparatur[1], 1e-9);
            Pruefe.Ungefaehr(0.02, b.Brandkasse!.BeitragAnteil, 1e-9);
            Pruefe.Ungefaehr(20, b.Brandkasse.Mindestbeitrag, 1e-9);
            Pruefe.Ungefaehr(0.6, b.Brandkasse.Deckung, 1e-9);
            var alt = Alt().Blitz!;
            Pruefe.Wahr(!alt.OhneMesse && alt.Stadtbau.Count == 0 && alt.Brandkasse is null, "ohne Werte keine Wirkung");
        }),

        ("Blitz Stadtbau: Die Greybox kennt Dom und Rathaus als Bauten der Stadt, Werkstätten und Wohnhäuser nicht", () =>
        {
            var (sim, _) = Partie();
            var w = sim.Wirtschaft!;
            Pruefe.Gleich("Dom", Stadtbau(w, Stadtbauart.Dom).Kennung);
            Pruefe.Gleich("Rathaus", Stadtbau(w, Stadtbauart.Rathaus).Kennung);
            Pruefe.Wahr(w.Blitz.Ziele().Where(z => z.Betrieb is not null || z.Bewohner is not null).All(z => z.Stadtbau is null), "nur Sonderbauten");
        }),

        ("Blitz Stadtbau: Dom brennt – Stadtkasse zahlt nach Schwere, bis zur Instandsetzung keine Domkollekte; Gegenprobe alte Daten", () =>
        {
            foreach (var neu in new[] { true, false })
            {
                var (sim, spieler) = Partie(neu ? null : Alt());
                BisMinute(sim, 300);
                var w = sim.Wirtschaft!;
                DomInhaber(w.Domamt, spieler);
                w.Kirchenwesen.Verteilen(w.Kirchenwesen.Kirchen, out var vorher);
                Pruefe.Wahr(vorher.Glaeubige > 0, "der Dom hat vor dem Feuer Gläubige");
                var kasse = w.Kasse.Stand;
                var meldungen = new List<string>();
                w.Meldung += m => meldungen.Add(m);
                var e = Blitz(sim, spieler, "Dom");
                Pruefe.Gleich(neu ? Stadtbauart.Dom : null, e.Ziel.Stadtbau);
                w.Kirchenwesen.Verteilen(w.Kirchenwesen.Kirchen, out var nachher);
                var m = meldungen.Single(x => x.StartsWith("Blitzschlag! "));
                if (!neu)
                {
                    Pruefe.Ungefaehr(kasse, w.Kasse.Stand, 1e-9);
                    Pruefe.Wahr(nachher.Glaeubige > 0 && !w.Blitz.DomOhneMesse, "alte Daten: Messe läuft weiter");
                    continue;
                }
                var d = w.Daten.Blitz!.Stadtbau["dom"].Reparatur;
                var reparatur = Math.Round(d[0] + (d[1] - d[0]) * e.Schwere);
                Pruefe.Ungefaehr(kasse - reparatur, w.Kasse.Stand, 1e-6);
                Pruefe.Ungefaehr(reparatur, w.Kasse.DiesesJahr[Kassenposten.Brandschaden], 1e-6);
                Pruefe.Wahr(w.Blitz.DomOhneMesse && nachher.Glaeubige == 0, "keine Dommesse");
                Pruefe.Wahr(m.Contains("fällt die Messe im Dom aus") && m.Contains("Stadtkasse zahlt"), m);
                // bis zur Instandsetzung (instandMinuten nach dem Feuer), danach wieder
                BisMinute(sim, e.BrandEnde + w.Daten.Blitz.InstandMinuten - 1);
                Pruefe.Wahr(w.Blitz.DomOhneMesse, "kurz vor der Instandsetzung noch ohne Messe");
                BisMinute(sim, e.BrandEnde + w.Daten.Blitz.InstandMinuten);
                Pruefe.Wahr(!w.Blitz.DomOhneMesse, "instand gesetzt");
            }
        }),

        ("Blitz Stadtbau: Die Stadtkasse zahlt nur, was sie hat (nicht unter 0)", () =>
        {
            var (sim, spieler) = Partie();
            BisMinute(sim, 300);
            var w = sim.Wirtschaft!;
            w.Kasse.Buchen(Kassenposten.Feste, w.Kasse.Stand - 150); // auf 150 Gulden
            Pruefe.Ungefaehr(150, w.Kasse.Stand, 1e-6);
            var meldungen = new List<string>();
            w.Meldung += m => meldungen.Add(m);
            Blitz(sim, spieler, "Rathaus");
            Pruefe.Ungefaehr(0, w.Kasse.Stand, 1e-6);
            Pruefe.Ungefaehr(150, w.Kasse.DiesesJahr[Kassenposten.Brandschaden], 1e-6);
            var m = meldungen.Single(x => x.StartsWith("Blitzschlag! "));
            Pruefe.Wahr(m.Contains("die Stadtkasse hat nur 150"), m);
        }),

        ("Blitz Stadtbau: Rathaus brennt – in der Ratssitzung entfallen die Anträge, gewählt wird; Gegenprobe ohne Brand und mit alten Daten", () =>
        {
            var wahlen = new List<int>();
            foreach (var (brand, neu) in new[] { (true, true), (false, true), (true, false) })
            {
                var (sim, spieler) = Partie(neu ? null : Alt());
                BisMinute(sim, 300);
                var w = sim.Wirtschaft!;
                spieler.Aktionspunkte.Setzen(12);
                var amt = w.Rat.AmtNamens("Zöllner")!;
                amt.Ratsherr = null;
                amt.Konto = spieler;
                var antrag = w.Ausfuehren(spieler, new SteuersatzBeantragen(w.Kasse.SteuersatzProzent + 1));
                Pruefe.Wahr(w.Ratsantraege.Antraege.Count == 1, $"Antrag liegt vor: {antrag.Meldung}");
                if (brand) Blitz(sim, spieler, "Rathaus");
                Pruefe.Gleich(brand && neu, w.Blitz.RathausAbgebrannt);
                var vorschau = w.Rat.Sitzungsvorschau();
                var mitAntrag = vorschau.Any(p => p.Thema == Sitzungsthema.Steuersatz);
                Pruefe.Gleich(!(brand && neu), mitAntrag, $"Steuer-Antrag in der Sitzung (Brand {brand}, neu {neu})");
                wahlen.Add(vorschau.Count(p => p.Thema == Sitzungsthema.Wahl));
                BisMinute(sim, NaechstesJahr(sim) + 1);
                Pruefe.Wahr(w.Ratsantraege.Antraege.Count == 0, "der Antrag ist weg (abgestimmt oder entfallen)");
                if (brand && neu)
                    Pruefe.Wahr(w.Rat.LetzteSitzung.Any(x => x.Contains("Ausweichquartier")), "Protokoll nennt das Ausweichquartier");
            }
            Pruefe.Gleich(wahlen[1], wahlen[0], "gewählt wird trotzdem (gleich viele Wahlen wie ohne Brand)");
        }),

        ("Blitz Stadtbau: Kirche brennt – keine Messe, solange sie brennt und still steht, ihre Gemeinde geht in andere Kirchen; Gegenprobe alte Daten", () =>
        {
            foreach (var neu in new[] { true, false })
            {
                var (sim, spieler) = Partie(neu ? null : Alt(), neu: new NeuePartie(Beruf: "Prediger"));
                BisMinute(sim, 300);
                var w = sim.Wirtschaft!;
                var ziel = Kirche(w, spieler);
                var kirche = ziel.Betrieb!;
                var e = Blitz(sim, spieler, ziel.Kennung);
                Pruefe.Gleich(neu, w.Blitz.KircheOhneMesse(kirche), "ohne Messe");
                var verteilt = w.Kirchenwesen.Verteilen(w.Kirchenwesen.Kirchen.Where(k => !w.Blitz.KircheOhneMesse(k)));
                Pruefe.Gleich(!neu, verteilt.ContainsKey(kirche), "die Kirche bekommt ihren Anteil");
                if (!neu) continue;
                // Zur Messe (10 Uhr am nächsten Tag steht sie noch still?) – die Kollekte geht nicht an die Kirche, solange sie ohne Messe ist
                var umsatz = kirche.Laufend.Umsatz;
                var messe = NaechstesJahr(sim) - Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag + (w.Kirchenwesen.Daten.MesseUm - 4) * 60;
                if (messe <= sim.Zeit.VergangeneMinuten) messe += Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;
                BisMinute(sim, messe + 1);
                if (w.Blitz.KircheOhneMesse(kirche))
                    Pruefe.Ungefaehr(umsatz, kirche.Laufend.Umsatz, 1e-9);
                // Nach Stillstand und Instandsetzung hält sie wieder Messe
                BisMinute(sim, e.BrandEnde + w.Daten.Blitz!.InstandMinuten);
                Pruefe.Wahr(!w.Blitz.KircheOhneMesse(kirche), "wieder Messe");
            }
        }),

        ("Brandkasse: Beitreten, Beitrag am Jahresende (2 % vom Bauwert, mind. 20), gedeckt erst im Folgejahr, zahlt 60 % der Reparatur aus dem Topf", () =>
        {
            var (sim, spieler) = Partie();
            BisMinute(sim, 300);
            var w = sim.Wirtschaft!;
            var bk = w.Brandkasse;
            Pruefe.Wahr(bk.IstAktiv, "aktiv");
            var e = w.Ausfuehren(spieler, new BrandkasseBeitreten());
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Wahr(!w.Ausfuehren(spieler, new BrandkasseBeitreten()).Erfolg, "nicht zweimal");
            Pruefe.Wahr(bk.IstMitglied(spieler) && !bk.IstGedeckt(spieler), "Mitglied, aber noch nicht gedeckt");
            // noch nicht gedeckt: volle Reparatur
            var geld = spieler.Vermoegen;
            var b = w.Betriebe.First(x => x.Konto == spieler && x.IstWerkstatt);
            Blitz(sim, spieler, "werkstatt");
            var voll = geld - spieler.Vermoegen;
            Pruefe.Wahr(voll > 0, "Reparatur gezahlt");
            var beitrag = bk.Beitrag(spieler);
            var wert = w.Wohnen.Hauswert(spieler) + w.Betriebe.Where(x => x.Konto == spieler).Sum(w.Bauwert);
            Pruefe.Ungefaehr(Math.Round(Math.Max(20, wert * 0.02)), beitrag, 1e-9);
            BisMinute(sim, NaechstesJahr(sim) + 300);
            Pruefe.Ungefaehr(beitrag, bk.Topf, 1e-6);
            Pruefe.Wahr(bk.IstGedeckt(spieler), "im Folgejahr gedeckt");
            // Topf auffüllen (wie nach vielen Jahren), dann zahlt sie 60 %
            typeof(Brandkasse).GetProperty("Topf")!.SetValue(bk, 100_000.0);
            var meldungen = new List<string>();
            w.Meldung += m => meldungen.Add(m);
            geld = spieler.Vermoegen;
            var feuer = Blitz(sim, spieler, b.Name);
            var d = w.Daten.Blitz!;
            var reparatur = Math.Round(Math.Max(d.Reparatur, (d.ReparaturAnteil[0] + (d.ReparaturAnteil[1] - d.ReparaturAnteil[0]) * feuer.Schwere) * w.Bauwert(b)) * b.BrandFaktor);
            var erstattet = Math.Round(reparatur * 0.6);
            Pruefe.Ungefaehr(geld - (reparatur - erstattet), spieler.Vermoegen, 1e-6);
            Pruefe.Ungefaehr(100_000 - erstattet, bk.Topf, 1e-6);
            Pruefe.Wahr(meldungen.Single(x => x.StartsWith("Blitzschlag! ")).Contains($"die Brandkasse zahlt {Zahlenformat.Ganz(erstattet)}"), "Meldung");
        }),

        ("Brandkasse: zahlt höchstens den Topf; Austritt; kein Beitritt, solange es brennt; Wohnhaus ist gedeckt", () =>
        {
            var (sim, spieler) = Partie();
            BisMinute(sim, 300);
            var w = sim.Wirtschaft!;
            var bk = w.Brandkasse;
            Pruefe.Wahr(w.Ausfuehren(spieler, new BrandkasseBeitreten()).Erfolg, "beitreten");
            BisMinute(sim, NaechstesJahr(sim) + 300);
            typeof(Brandkasse).GetProperty("Topf")!.SetValue(bk, 10.0);
            var geld = spieler.Vermoegen;
            Blitz(sim, spieler, "haus");
            var gezahlt = geld - spieler.Vermoegen;
            Pruefe.Ungefaehr(0, bk.Topf, 1e-9);
            Pruefe.Wahr(gezahlt >= 0, "Wohnhaus: Rest selbst");
            // Austritt, dann kein Beitritt, solange das Wohnhaus instand gesetzt wird
            Pruefe.Wahr(w.Ausfuehren(spieler, new BrandkasseAustreten()).Erfolg, "austreten");
            Pruefe.Wahr(!bk.IstMitglied(spieler), "kein Mitglied mehr");
            Pruefe.Wahr(!w.Ausfuehren(spieler, new BrandkasseAustreten()).Erfolg, "nicht zweimal austreten");
            var nein = w.Ausfuehren(spieler, new BrandkasseBeitreten());
            Pruefe.Wahr(!nein.Erfolg && nein.Meldung.Contains("brennt"), nein.Meldung);
        }),

        ("Brandkasse: Wer den Beitrag nicht zahlen kann, scheidet aus; ohne Abschnitt gibt es keine", () =>
        {
            var (sim, spieler) = Partie();
            BisMinute(sim, 300);
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(w.Ausfuehren(spieler, new BrandkasseBeitreten()).Erfolg, "beitreten");
            spieler.Schaden(spieler.Vermoegen + 1_000);
            w.Brandkasse.Jahresende();
            Pruefe.Wahr(!w.Brandkasse.IstMitglied(spieler), "ausgeschieden");
            Pruefe.Ungefaehr(0, w.Brandkasse.Topf, 1e-9);
            var (alt, ap) = Partie(Alt());
            Pruefe.Wahr(!alt.Wirtschaft!.Brandkasse.IstAktiv, "ohne Abschnitt");
            Pruefe.Wahr(!alt.Wirtschaft.Ausfuehren(ap, new BrandkasseBeitreten()).Erfolg, "ohne Abschnitt kein Beitritt");
        }),

        ("Brandkasse und Instandsetzung im Spielstand: Topf, Mitglieder, Dom ohne Messe bleiben nach dem Laden; alte Stände ohne Felder", () =>
        {
            var (sim, spieler) = Partie();
            BisMinute(sim, 300);
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(w.Ausfuehren(spieler, new BrandkasseBeitreten()).Erfolg, "beitreten");
            typeof(Brandkasse).GetProperty("Topf")!.SetValue(w.Brandkasse, 1234.0);
            Blitz(sim, spieler, "Dom");
            var text = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            var (geladen, gs) = Speicherung.Laden(text);
            var g = geladen.Wirtschaft!;
            Pruefe.Ungefaehr(1234, g.Brandkasse.Topf, 1e-9);
            Pruefe.Wahr(g.Brandkasse.IstMitglied(gs) && !g.Brandkasse.IstGedeckt(gs), "Mitglied seit diesem Jahr");
            Pruefe.Wahr(g.Blitz.DomOhneMesse, "Dom ohne Messe nach dem Laden");
            Pruefe.Gleich(Stadtbauart.Dom, g.Blitz.Einschlaege[^1].Ziel.Stadtbau);
            Pruefe.Gleich(text, Speicherung.Speichern(geladen, gs, Zeitpunkt), "zweimal gespeichert, gleich");
            // alter Stand: ohne die neuen Felder
            var knoten = JsonNode.Parse(text)!;
            Pruefe.Wahr(Entfernen(knoten, "Brandkasse") && Entfernen(knoten, "Instand"), "Felder gefunden");
            var (alt, ags) = Speicherung.Laden(knoten.ToJsonString());
            Pruefe.Wahr(!alt.Wirtschaft!.Brandkasse.IstMitglied(ags) && alt.Wirtschaft.Brandkasse.Topf == 0, "leerer Topf");
            Pruefe.Wahr(!alt.Wirtschaft.Blitz.DomOhneMesse, "keine Instandsetzung");
        }),
    };

    /// <summary>Entfernt ein Feld überall im Spielstand.</summary>
    static bool Entfernen(JsonNode? knoten, string feld)
    {
        var gefunden = false;
        if (knoten is JsonObject o)
        {
            gefunden |= o.Remove(feld);
            foreach (var x in o.ToList()) gefunden |= Entfernen(x.Value, feld);
        }
        else if (knoten is JsonArray a)
            foreach (var x in a) gefunden |= Entfernen(x, feld);
        return gefunden;
    }
}
