using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Die Klappbrücke über den Rhein (Datenversion 90, Entscheidung des Nutzers vom 15.11.2026): offen für das Zierschiff von und nach flussauf,
/// je Durchfahrt <c>logistik.klappbruecke.oeffnungMinuten</c>; Karren über die Brücke warten, solange sie offen ist. In Köln fährt heute kein
/// Karren über den Rhein (jedes Haus hat seinen Markt auf seiner Seite) – die Prüfkarte nimmt darum den Neumarkt in Deutz weg.
/// </summary>
static class KlappbrueckeTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    internal static Wirtschaftsdaten DatenMit(Action<JsonObject> aendern)
    {
        var knoten = JsonNode.Parse(Json.Value)!.AsObject();
        aendern(knoten);
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    internal static Wirtschaftsdaten OhneKlappbruecke() => DatenMit(d => d["logistik"]!.AsObject().Remove("klappbruecke"));

    /// <summary>Köln ohne den Neumarkt in Deutz: Die Häuser in Deutz fahren über die Rheinbrücke zu Heumarkt oder Alter Markt.</summary>
    internal static Stadtkarte Pruefkarte()
    {
        var pfad = Altkarte.Pfad("koeln");
        var karte = JsonNode.Parse(File.ReadAllText(pfad))!.AsObject();
        var gebaeude = karte["gebaeude"]!.AsArray();
        gebaeude.Remove(gebaeude.Single(g => (string?)g!["kennung"] == "MarktDeutz"));
        return Stadtkarte.AusJson(karte.ToJsonString(), Gebaeudekatalog.AusDatei(Stadtkarte.KatalogZu(pfad)));
    }

    /// <summary>Eine Partie „Schwer“ mit allen KI-Häusern der Karte.</summary>
    internal static Simulation Partie(Stadtkarte karte, Wirtschaftsdaten daten, int startwert = 1400)
    {
        var haeuser = Kartenhaeuser.Anzahl(File.ReadAllText(Altkarte.Pfad("koeln")));
        var (sim, _) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer, Zufallsstartwert = startwert },
            daten, FesteStartorte.Partie("koeln", new NeuePartie(Rivalen: haeuser, Aufstellung: Aufstellungsart.Vorgabe)), karte: karte); // feste Startorte: drei Häuser in Deutz
        return sim;
    }

    static Stadtwirtschaft Mit(Wirtschaftsdaten daten)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, new Spielzeit(0), daten);
        sim.Wirtschaft!.KarteSetzen(Stadtkarte.AusDatei(Altkarte.Pfad("koeln")));
        return sim.Wirtschaft;
    }

    /// <summary>Die Öffnungen eines Fensters, wie sie aus dem Zierfahrplan folgen (Köln: Brücke flussauf vom Hafen).</summary>
    static List<Brueckenoeffnung> Erwartet(long fenster, int dauer, bool winter)
    {
        var liste = new List<Brueckenoeffnung>();
        if (winter) return liste;
        var anfang = Zierfahrplan.Anfang(fenster);
        var (ankunft, liegen, vonAuf, nachAuf) = Zierfahrplan.Streuung(fenster);
        if (vonAuf) liste.Add(new(anfang + ankunft - dauer, anfang + ankunft));
        if (nachAuf) liste.Add(new(anfang + ankunft + liegen, anfang + ankunft + liegen + dauer));
        return liste;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Klappbrücke: Daten ab 90 – Rhein, 20 Minuten je Durchfahrt; Prüfung meldet leeres Gewässer und Dauer außerhalb 1–60; ohne Abschnitt keine Brücke", () =>
        {
            var d = Daten.Value;
            Pruefe.Gleich(95, d.Version);
            Pruefe.Gleich("Rhein", d.Logistik.Klappbruecke!.Gewaesser);
            Pruefe.Gleich(20, d.Logistik.Klappbruecke.OeffnungMinuten);
            Pruefe.WirftMitMeldung(() => DatenMit(j => j["logistik"]!["klappbruecke"]!["oeffnungMinuten"] = 0), "oeffnungMinuten");
            Pruefe.WirftMitMeldung(() => DatenMit(j => j["logistik"]!["klappbruecke"]!["oeffnungMinuten"] = 61), "oeffnungMinuten");
            Pruefe.WirftMitMeldung(() => DatenMit(j => j["logistik"]!["klappbruecke"]!["gewaesser"] = ""), "gewaesser");
            var ohne = Mit(OhneKlappbruecke());
            Pruefe.Wahr(ohne.Klappbruecke.Daten is null && !ohne.Klappbruecke.Gibt, "ohne Abschnitt keine Klappbrücke");
            Pruefe.Wahr(Enumerable.Range(0, 2 * Spielzeit.MinutenProTag).All(m => !ohne.Klappbruecke.IstOffen(m)) && ohne.Klappbruecke.NaechsteOeffnung(0) is null,
                "ohne Abschnitt nie offen");
        }),

        ("Klappbrücke: In Köln die Rheinbrücke, flussauf vom Hafen – offen vor der Ankunft eines Schiffs von flussauf und nach dem Ablegen nach flussauf, " +
         "nicht im Winter; Gegenproben Schiffshandel aktiv, Greybox, anderes Gewässer", () =>
        {
            var w = Mit(Daten.Value);
            var k = w.Klappbruecke;
            Pruefe.Wahr(k.Gibt, "Köln hat die Klappbrücke");
            Pruefe.Gleich("Rheinbrücke", w.Karte!.Wege[k.Bruecken.Single().Weg].Name);
            var rhein = w.Karte.Wasser.Single(g => g.Name == "Rhein").Linie!;
            Pruefe.Wahr(Klappbruecke.Stelle(rhein, k.Bruecken[0].Mitte) < Klappbruecke.Stelle(rhein, w.Schiffahrt.Haefen[0].Liegeplatz), "Brücke flussauf vom Hafen");
            var gesamt = 0;
            for (long f = 0; f < 16; f++)
            {
                var winter = new Spielzeit(Zierfahrplan.Anfang(f)).Jahreszeit == Jahreszeit.Winter;
                var erwartet = Erwartet(f, 20, winter);
                Pruefe.Gleich(string.Join(";", erwartet), string.Join(";", k.OeffnungenIm(f)), $"Fenster {f}");
                gesamt += erwartet.Count;
                if (winter) Pruefe.Gleich(0, k.OeffnungenIm(f).Count, $"Winter, Fenster {f}");
            }
            Pruefe.Wahr(gesamt >= 8, $"{gesamt} Öffnungen in acht Jahren");

            // Gegenproben: Läuft der Schiffshandel, kommt kein Zierschiff; Greybox (Grenzfluss) und ein anderes Gewässer haben keine Klappbrücke
            var aktiv = Mit(DatenMit(j => j["schiffshandel"]!["aktiv"] = true));
            Pruefe.Wahr(Enumerable.Range(0, 16).All(f => aktiv.Klappbruecke.OeffnungenIm(f).Count == 0), "Schiffshandel aktiv: zu");
            var andere = Mit(DatenMit(j => j["logistik"]!["klappbruecke"]!["gewaesser"] = "Duffesbach"));
            Pruefe.Wahr(!andere.Klappbruecke.Gibt, "Duffesbach: kein Hafen an diesem Gewässer");
            var (grey, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value,
                new NeuePartie(Rivalen: 1), karte: Stadtkarte.AusDatei(Altkarte.Pfad("greybox")));
            Pruefe.Wahr(!grey.Wirtschaft!.Klappbruecke.Gibt, "Greybox: keine Klappbrücke");
        }),

        ("Klappbrücke: IstOffen und NaechsteOeffnung passen Minute für Minute zusammen (drei Jahre)", () =>
        {
            var k = Mit(Daten.Value).Klappbruecke;
            var offen = 0;
            for (long m = 0; m < 3 * Spielzeit.MinutenProTag; m++)
            {
                var n = k.NaechsteOeffnung(m);
                Pruefe.Wahr(n is { } o && o.Bis > m, $"Minute {m}: nächste Öffnung");
                var istOffen = k.IstOffen(m);
                Pruefe.Gleich(n!.Value.Enthaelt(m), istOffen, $"Minute {m}");
                if (istOffen) { offen++; Pruefe.Gleich(n, k.Oeffnung(m), $"Minute {m}: dieselbe Öffnung"); }
            }
            Pruefe.Wahr(offen > 0 && offen % 20 == 0, $"{offen} offene Minuten");
        }),

        ("Klappbrücke: Prüfkarte – die Deutzer Gießerei fährt über die Brücke und wartet genau, wenn sie offen ist, bis sie zugeht; " +
         "Gegenprobe Köln: keine Fahrt zum Markt über den Rhein", () =>
        {
            var w = Partie(Pruefkarte(), Daten.Value).Wirtschaft!;
            var b = w.Betriebe.Single(x => x.Name == "Gießerei Scherfgin");
            var (hin, zurueck) = w.Fahrten(b)!.Value;
            Pruefe.Wahr(w.Klappbruecke.FuehrtUeber(hin) && w.Klappbruecke.FuehrtUeber(zurueck), "über die Brücke hin und zurück");
            var fahrzeit = w.Fahrzeit(b, "Handkarren", true);
            var o = w.Klappbruecke.NaechsteOeffnung(0)!.Value;
            while (o.Von < fahrzeit + 5) o = w.Klappbruecke.NaechsteOeffnung(o.Bis)!.Value; // vorher genug Zeit für die ganze Fahrt
            long? bisZurBruecke = null;
            var (warten, nicht) = (0, 0);
            for (var ab = o.Von - fahrzeit - 5; ab < o.Bis + 5; ab++)
            {
                if (w.Klappbruecke.Halt(hin, ab, fahrzeit) is not { } h) { nicht++; continue; }
                bisZurBruecke ??= h.Von - ab;
                Pruefe.Gleich(bisZurBruecke, h.Von - ab, "die Brücke liegt immer gleich weit");
                Pruefe.Wahr(h.Von >= ab && h.Von <= ab + fahrzeit && w.Klappbruecke.Oeffnung(h.Von) is { } oh && oh.Bis == h.Bis, $"Abfahrt {ab}");
                if (h.Bis == o.Bis) warten++; // eine spätere Öffnung im selben Fenster zählt nicht
            }
            Pruefe.Gleich(20, warten, "jede Minute der Öffnung einmal");
            Pruefe.Wahr(nicht > 0, "vorher und nachher keine Wartezeit");

            var koeln = Partie(Stadtkarte.AusDatei(Altkarte.Pfad("koeln")), Daten.Value).Wirtschaft!;
            foreach (var x in koeln.Betriebe)
                Pruefe.Wahr(koeln.Fahrten(x) is not var (h2, z2) || (!koeln.Klappbruecke.FuehrtUeber(h2) && !koeln.Klappbruecke.FuehrtUeber(z2)), $"{x.Name} bleibt auf seiner Seite");
        }),

        ("Klappbrücke: Prüfkarte – Karren warten in der Partie an der offenen Brücke (Ankunft um die Wartezeit später), Spielstand mitten im Warten " +
         "fährt bitgleich weiter; Gegenprobe ohne Abschnitt: kein Karren wartet", () =>
        {
            var sim = Partie(Pruefkarte(), Daten.Value);
            var w = sim.Wirtschaft!;
            var zustand = new Dictionary<Karren, KarrenZustand>(ReferenceEqualityComparer.Instance);
            var geprueft = 0;
            for (var i = 0; i < 3 * Spielzeit.MinutenProTag && geprueft == 0; i++)
            {
                sim.Tick();
                var jetzt = sim.Zeit.VergangeneMinuten;
                foreach (var karren in w.Betriebe.SelectMany(b => b.Karren))
                {
                    var vorher = zustand.GetValueOrDefault(karren, KarrenZustand.AmBetrieb);
                    zustand[karren] = karren.Zustand;
                    if (karren.Zustand == vorher || karren.Zustand == KarrenZustand.AmBetrieb || karren.Heimat.Ort != Arbeitsort.Werkstatt) continue;
                    if (karren.BrueckeWartenAb is not { } ab || karren.BrueckeWartenBis is not { } bis || ab < jetzt - 1) continue;
                    // gerade aufgebrochen (in der Minute vor jetzt) und wartet auf dieser Fahrt
                    var abfahrt = jetzt - 1;
                    var fahrzeit = w.Fahrzeit(karren, karren.Zustand == KarrenZustand.ZumMarkt);
                    Pruefe.Gleich(abfahrt + fahrzeit + (bis - ab), karren.Ankunft, $"{karren.Name}: Ankunft um die Wartezeit später");
                    Pruefe.Wahr(ab <= abfahrt + fahrzeit && w.Klappbruecke.IstOffen(ab) && w.Klappbruecke.Oeffnung(ab)!.Value.Bis == bis, $"{karren.Name}: wartet in der Öffnung");
                    geprueft++;
                }
            }
            Pruefe.Wahr(geprueft > 0, "ein Karren hat an der Brücke gewartet");

            // Bis er an der Brücke steht, dann speichern, laden und beide weiterlaufen lassen
            var wartend = w.Betriebe.SelectMany(b => b.Karren).First(k => k.BrueckeWartenAb is { } ab && ab >= sim.Zeit.VergangeneMinuten - 1);
            for (var i = 0; i < 200 && !wartend.WartetAnBruecke(sim.Zeit); i++) sim.Tick();
            Pruefe.Wahr(wartend.WartetAnBruecke(sim.Zeit) && wartend.ZustandText(sim.Zeit).Contains("Klappbrücke"), "steht an der Brücke");
            var spieler = w.Konten[0];
            var text = Speicherung.Speichern(sim, spieler, new DateTime(2026, 1, 1));
            var (geladen, _) = Speicherung.Laden(text);
            var wartend2 = geladen.Wirtschaft!.Betriebe.Single(b => b.Name == wartend.Heimat.Name).Karren.Single(k => k.Name == wartend.Name);
            Pruefe.Wahr(wartend2.WartetAnBruecke(geladen.Zeit) && wartend2.BrueckeWartenBis == wartend.BrueckeWartenBis && wartend2.Ankunft == wartend.Ankunft, "geladen: wartet weiter");
            for (var i = 0; i < 300; i++) { sim.Tick(); geladen.Tick(); }
            Pruefe.Gleich(Speicherung.Speichern(sim, spieler, new DateTime(2026, 1, 1)),
                Speicherung.Speichern(geladen, geladen.Wirtschaft.Konten.Single(x => x.Inhaber == spieler.Inhaber), new DateTime(2026, 1, 1)));

            // Gegenprobe: dieselbe Karte ohne Abschnitt – ein Jahr lang wartet kein Karren
            var ohne = Partie(Pruefkarte(), OhneKlappbruecke());
            for (var i = 0; i < Spielzeit.MinutenProTag; i++)
            {
                ohne.Tick();
                Pruefe.Wahr(ohne.Wirtschaft!.Betriebe.SelectMany(b => b.Karren).All(k => k.BrueckeWartenAb is null), "ohne Abschnitt wartet keiner");
            }
        }),
    };
}
