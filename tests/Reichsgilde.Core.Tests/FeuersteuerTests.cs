using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Gefahren;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Feuersteuer statt Brandkasse (Nutzer 22.11.2026, Datenversion 96): Die Reparatur nach einem Brand an Werkstatt und Wohnhaus eines Hauses
/// zahlt die Stadtkasse (nicht unter 0, den Rest das Haus; das Lager trägt das Haus). Dafür ein fester Aufschlag auf die Umsatzsteuer,
/// gebucht unter dem Kassenposten „Feuersteuer“; der Rat ändert ihn nicht. Jeder Test mit Gegenprobe.
/// </summary>
static class FeuersteuerTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 11, 22, 12, 0, 0);

    static readonly (string, string) OhneGewitter = ("\"chance\": 0.25,\n    \"dauerMinuten\"", "\"chance\": 0,\n    \"dauerMinuten\"");
    static readonly (string, string) SpringtNie = ("\"chanceJeStunde\": 0.15", "\"chanceJeStunde\": 0");

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

    /// <summary>Die Daten ohne „blitz.feuersteuer“ (wie bis Datenversion 95).</summary>
    static Wirtschaftsdaten OhneFeuersteuer()
    {
        var knoten = JsonNode.Parse(Json.Value.Replace(OhneGewitter.Item1, OhneGewitter.Item2).Replace(SpringtNie.Item1, SpringtNie.Item2))!;
        Pruefe.Wahr(knoten["blitz"]!.AsObject().Remove("feuersteuer"), "feuersteuer in den Daten");
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    /// <summary>Die Daten ohne Stadtkasse („politik.kasse“): Dann zahlt das Haus wie früher alles selbst.</summary>
    static Wirtschaftsdaten OhneKasse()
    {
        var knoten = JsonNode.Parse(Json.Value.Replace(OhneGewitter.Item1, OhneGewitter.Item2).Replace(SpringtNie.Item1, SpringtNie.Item2))!;
        Pruefe.Wahr(knoten["politik"]!.AsObject().Remove("kasse"), "politik.kasse in den Daten");
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    static (Simulation Sim, Konto Spieler) Partie(Wirtschaftsdaten? daten = null)
    {
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
            daten ?? Mit(OhneGewitter, SpringtNie), new NeuePartie(), karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
        sim.EntwicklerErlaubt = true;
        BisMinute(sim, 300);
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
        var e = sim.Bote.SofortAusfuehren(spieler, new EntwicklerBlitz(gebaeude));
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        return sim.Wirtschaft!.Blitz.Einschlaege[^1];
    }

    static double Anteil(double[] paar, double schwere) => paar[0] + (paar[1] - paar[0]) * schwere;

    static double WerkstattReparatur(Stadtwirtschaft w, Betrieb b, Blitzeinschlag e)
    {
        var d = w.Daten.Blitz!;
        return Math.Round(Math.Max(d.Reparatur, Anteil(d.ReparaturAnteil, e.Schwere) * w.Bauwert(b)) * b.BrandFaktor);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Feuersteuer: Daten – 0,05 Prozentpunkte auf die Umsatzsteuer, ohne Wert keine; negativ oder zu hoch ist ein Datenfehler", () =>
        {
            var d = Wirtschaftsdaten.AusJson(Json.Value);
            Pruefe.Wahr(d.Version >= 96, "Datenversion");
            Pruefe.Ungefaehr(0.0005, d.Blitz!.Feuersteuer, 1e-12);
            Pruefe.Ungefaehr(0, OhneFeuersteuer().Blitz!.Feuersteuer, 1e-12);
            foreach (var falsch in new[] { "-0.01", "0.1" })
            {
                Pruefe.WirftMitMeldung(() => Mit(("\"feuersteuer\": 0.0005", $"\"feuersteuer\": {falsch}")), "blitz.feuersteuer");
            }
        }),

        ("Feuersteuer: Aufschlag auf die Umsatzsteuer, eigener Kassenposten und Zeile der Jahresrechnung; Gegenprobe ohne Wert", () =>
        {
            foreach (var mit in new[] { true, false })
            {
                var (sim, spieler) = Partie(mit ? null : OhneFeuersteuer());
                var w = sim.Wirtschaft!;
                var f = mit ? 0.0005 : 0;
                Pruefe.Ungefaehr(f, w.Kasse.Feuersteuer, 1e-12);
                Pruefe.Ungefaehr(w.Kasse.SteuersatzProzent / 100.0 + f, w.Umsatzsteuer, 1e-12);
                var feuerVorher = w.Kasse.DiesesJahr[Kassenposten.Feuersteuer];
                var umsatzVorher = w.Kasse.DiesesJahr[Kassenposten.Umsatzsteuer];
                var bilanzVorher = spieler.Laufend.Feuersteuer;
                var steuerVorher = spieler.Laufend.Steuer;
                var betrag = 1000 * w.Umsatzsteuer; // Steuer auf 1.000 Gulden Umsatz
                spieler.Steuer(betrag);
                Pruefe.Ungefaehr(1000 * f, w.Kasse.DiesesJahr[Kassenposten.Feuersteuer] - feuerVorher, 1e-9);
                Pruefe.Ungefaehr(1000 * w.Kasse.Umsatzsteuer, w.Kasse.DiesesJahr[Kassenposten.Umsatzsteuer] - umsatzVorher, 1e-9);
                Pruefe.Ungefaehr(1000 * f, spieler.Laufend.Feuersteuer - bilanzVorher, 1e-9);
                Pruefe.Ungefaehr(betrag, spieler.Laufend.Steuer - steuerVorher, 1e-9); // die Feuersteuer steckt in der Steuer
                // andere Abgaben (Glaubenszins) tragen keine Feuersteuer
                feuerVorher = w.Kasse.DiesesJahr[Kassenposten.Feuersteuer];
                spieler.Steuer(50, Kassenposten.Glaubenszins);
                Pruefe.Ungefaehr(feuerVorher, w.Kasse.DiesesJahr[Kassenposten.Feuersteuer], 1e-12);
                Pruefe.Wahr(Kassenposten.Feuersteuer.IstEinnahme() && Kassenposten.Feuersteuer.Name() == "Feuersteuer", "Einnahme mit eigenem Namen");
            }
        }),

        ("Feuersteuer: Der Rat ändert nur die Umsatzsteuer, die Feuersteuer bleibt; ohne Stadtkasse gibt es keine", () =>
        {
            var (sim, _) = Partie();
            var w = sim.Wirtschaft!;
            w.Kasse.SteuersatzSetzen(w.Kasse.SteuersatzProzent + 2);
            Pruefe.Ungefaehr(0.0005, w.Kasse.Feuersteuer, 1e-12);
            Pruefe.Ungefaehr(w.Kasse.SteuersatzProzent / 100.0 + 0.0005, w.Umsatzsteuer, 1e-12);
            var (ohne, _) = Partie(OhneKasse());
            Pruefe.Ungefaehr(0, ohne.Wirtschaft!.Kasse.Feuersteuer, 1e-12);
            Pruefe.Ungefaehr(ohne.Wirtschaft.Daten.Allgemein.Umsatzsteuer, ohne.Wirtschaft.Umsatzsteuer, 1e-12);
        }),

        ("Feuersteuer: Werkstatt brennt – die Stadtkasse zahlt die Reparatur, das Haus nichts; Gegenprobe ohne Stadtkasse zahlt das Haus", () =>
        {
            foreach (var kasse in new[] { true, false })
            {
                var (sim, spieler) = Partie(kasse ? null : OhneKasse());
                var w = sim.Wirtschaft!;
                var b = w.Betriebe.First(x => x.Konto == spieler && x.IstWerkstatt);
                var meldungen = new List<string>();
                w.Meldung += m => meldungen.Add(m);
                var geld = spieler.Vermoegen;
                var stand = w.Kasse.Stand;
                var e = Blitz(sim, spieler, b.Name);
                var reparatur = WerkstattReparatur(w, b, e);
                Pruefe.Wahr(reparatur > 0, "die Werkstatt kostet eine Reparatur");
                var m = meldungen.Single(x => x.StartsWith("Blitzschlag! "));
                if (kasse)
                {
                    Pruefe.Ungefaehr(geld, spieler.Vermoegen, 1e-6);
                    Pruefe.Ungefaehr(stand - reparatur, w.Kasse.Stand, 1e-6);
                    Pruefe.Ungefaehr(reparatur, w.Kasse.DiesesJahr[Kassenposten.Brandschaden], 1e-6);
                    Pruefe.Ungefaehr(0, b.Laufend.Schaden, 1e-9);
                    Pruefe.Wahr(m.Contains("zahlt die Stadtkasse"), m);
                }
                else
                {
                    Pruefe.Ungefaehr(geld - reparatur, spieler.Vermoegen, 1e-6);
                    Pruefe.Ungefaehr(reparatur, b.Laufend.Schaden, 1e-6);
                    Pruefe.Wahr(!m.Contains("Stadtkasse"), m);
                }
                Pruefe.Wahr(b.Laufend.Brandschaden >= reparatur, "Brandschaden der Werkstatt samt Reparatur");
            }
        }),

        ("Feuersteuer: Wohnhaus brennt – die Stadtkasse zahlt, soweit sie reicht (nicht unter 0), den Rest das Haus; leer zahlt das Haus alles", () =>
        {
            var (sim, spieler) = Partie();
            var w = sim.Wirtschaft!;
            var d = w.Daten.Blitz!;
            var grund = w.Gauner.Brandschaden(spieler);
            var faktor = w.Wohnen.BrandFaktor(spieler);
            w.Kasse.Buchen(Kassenposten.Feste, w.Kasse.Stand - 10.6); // auf 10,6 Gulden (abgerundet wird gezahlt)
            var meldungen = new List<string>();
            w.Meldung += m => meldungen.Add(m);
            var geld = spieler.Vermoegen;
            var e = Blitz(sim, spieler, "haus");
            var reparatur = Math.Round(Math.Max(d.WohnhausReparatur, Anteil(d.WohnhausAnteil, e.Schwere) * grund) * faktor);
            Pruefe.Wahr(reparatur > 10, $"Wohnhaus kostet mehr, als in der Kasse ist: {reparatur}");
            Pruefe.Ungefaehr(0.6, w.Kasse.Stand, 1e-6);
            Pruefe.Ungefaehr(10, w.Kasse.DiesesJahr[Kassenposten.Brandschaden], 1e-6);
            Pruefe.Ungefaehr(geld - (reparatur - 10), spieler.Vermoegen, 1e-6);
            var m = meldungen.Single(x => x.StartsWith("Blitzschlag! "));
            Pruefe.Wahr(m.Contains("die Stadtkasse zahlt 10"), m);
            // die Kasse ist leer: das Haus zahlt alles (nach der Instandsetzung, schon im neuen Jahr – die Kasse wieder leeren)
            BisMinute(sim, e.BrandEnde + d.InstandMinuten + 1);
            w.Kasse.Buchen(Kassenposten.Feste, w.Kasse.Stand);
            var brandschaden = w.Kasse.DiesesJahr[Kassenposten.Brandschaden];
            meldungen.Clear();
            geld = spieler.Vermoegen;
            var zweites = Blitz(sim, spieler, "haus");
            var rep2 = Math.Round(Math.Max(d.WohnhausReparatur, Anteil(d.WohnhausAnteil, zweites.Schwere) * w.Gauner.Brandschaden(spieler)) * w.Wohnen.BrandFaktor(spieler));
            Pruefe.Ungefaehr(geld - rep2, spieler.Vermoegen, 1e-6);
            Pruefe.Gleich(brandschaden, w.Kasse.DiesesJahr[Kassenposten.Brandschaden], "die leere Kasse zahlt nichts");
            Pruefe.Ungefaehr(0, w.Kasse.Stand, 1e-9);
            Pruefe.Wahr(meldungen.Single(x => x.StartsWith("Blitzschlag! ")).Contains("Stadtkasse ist leer"), "Meldung: leer");
        }),

        ("Feuersteuer im Spielstand: Feuersteuer der Jahresrechnung bleibt; alte Stände ohne Feld und mit Brandkasse laden", () =>
        {
            var (sim, spieler) = Partie();
            var w = sim.Wirtschaft!;
            spieler.Steuer(1000 * w.Umsatzsteuer);
            var feuer = spieler.Laufend.Feuersteuer;
            Pruefe.Wahr(feuer > 0, "Feuersteuer gezahlt");
            var text = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            var (geladen, gs) = Speicherung.Laden(text);
            Pruefe.Ungefaehr(feuer, gs!.Laufend.Feuersteuer, 1e-12);
            Pruefe.Ungefaehr(w.Kasse.DiesesJahr[Kassenposten.Feuersteuer], geladen.Wirtschaft!.Kasse.DiesesJahr[Kassenposten.Feuersteuer], 1e-12);
            Pruefe.Gleich(text, Speicherung.Speichern(geladen, gs, Zeitpunkt), "zweimal gespeichert, gleich");
            // alter Stand: ohne Feuersteuer, dafür mit der Brandkasse von Datenversion 95
            var knoten = JsonNode.Parse(text)!;
            Pruefe.Wahr(Entfernen(knoten, "Feuersteuer"), "Feld gefunden");
            knoten["Wirtschaft"]!.AsObject()["Brandkasse"] = JsonNode.Parse("{\"Topf\": 1234, \"Mitglieder\": [{\"Konto\": 0, \"SeitJahr\": 1400}]}");
            var (alt, ags) = Speicherung.Laden(knoten.ToJsonString());
            Pruefe.Ungefaehr(0, ags!.Laufend.Feuersteuer, 1e-12);
            Pruefe.Wahr(alt.Wirtschaft is not null, "geladen");
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
