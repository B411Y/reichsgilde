using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Hofstellen: die Befunde der unabhängigen Prüfung (9.11.2026) – schräge Hofstellen sind ganz bebaubar, die Werkstatt auf einer Hofstelle ist für
/// den Blitz ein Betrieb, ein Neubau bekommt nicht den Namen der Spielerwerkstatt, ein Widerspruch in der Startbelegung ohne Los fällt beim Aufbau
/// auf, ein Haus ganz außerhalb der Hofstellen bleibt beim Los, wo es ist, und die Lage gilt auf Hofstellen für Wohnhäuser weiter.
/// </summary>
static class HofstellenPruefungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Wirtschaftsdaten mit sofort fertigen Werkstätten (Bauzeit 0).</summary>
    static readonly Lazy<Wirtschaftsdaten> Sofort = new(() =>
    {
        var text = File.ReadAllText(Datenpfad.Wirtschaft());
        var alt = "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 1,";
        Pruefe.Wahr(text.Contains(alt), "Zeile der Werkstatt in bauen gefunden");
        return Wirtschaftsdaten.AusJson(text.Replace(alt, "\"werkstatt\": { \"breite\": 16, \"tiefe\": 14, \"bauzeitJahre\": 0,"));
    });

    static (Simulation Sim, Stadtwirtschaft W, Konto Spieler) Partie(Stadtkarte karte, NeuePartie neu, Wirtschaftsdaten? daten = null)
    {
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, daten ?? Daten.Value, neu, karte: karte);
        spieler.Stand = 5;
        return (sim, sim.Wirtschaft!, spieler);
    }

    static Stadtkarte Karte(string kennung) => Stadtkarte.AusDatei(Datenpfad.Karte(kennung));

    static WerkstattBauen AufWerkstatt(Grundstueck platz, Betriebsart art) => new(art, platz.Grundriss.X, platz.Grundriss.Z, platz.Grundriss.Drehung);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Hofstellen-Prüfung: Eine schräge freie Hofstelle (Köln S8, 2,6°) ist ganz bebaubar – erst das Wohnhaus, dann die Werkstatt; der Nachbarplatz bleibt Bauplatz", () =>
        {
            var (_, w, spieler) = Partie(Karte("koeln"), new NeuePartie(Startvermoegen: 200_000, Rivalen: 3, Startplaetze: Startplatzvergabe.Vorgabe, Aufstellung: Aufstellungsart.Vorgabe));
            var wohn = w.Bauen.Grundstuecke.Single(g => g.Kennung == "S8_Wohnhaus");
            var werk = w.Bauen.Grundstuecke.Single(g => g.Kennung == "S8_Werkstatt");
            Pruefe.Wahr(wohn.Grundriss.Ueberlappt(werk.Grundriss), "die umschließenden Rechtecke überdecken sich (darum der Befund)");
            var haus = new WohnhausBauen(1, wohn.Grundriss.X, wohn.Grundriss.Z, wohn.Grundriss.Drehung);
            Pruefe.Gleich(null, w.Bauen.BauVerboten(spieler, haus));
            var e = w.Ausfuehren(spieler, haus);
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Wahr(w.Bauen.Grundstuecke.Any(g => g.Kennung == "S8_Werkstatt" && Bauwesen.IstWerkstattplatz(g)), "der Werkstattteil ist noch Bauplatz");
            Pruefe.Gleich(null, w.Bauen.PlatzVerboten(AufWerkstatt(werk, Betriebsart.Giesserei)));
            // Gegenprobe: neben die Hofstelle in die Fläche des Wohnhauses geht weiter nichts
            Pruefe.Wahr(w.Bauen.PlatzVerboten(new WerkstattBauen(Betriebsart.Giesserei, wohn.Grundriss.X, wohn.Grundriss.Z, 0)) is not null, "nicht auf das neue Wohnhaus");
        }),

        ("Hofstellen-Prüfung: Eine Werkstatt auf einer freien Hofstelle ist für den Blitz ein Betrieb am Platz; als Bauplatz ist der Platz kein Ziel", () =>
        {
            var (_, w, spieler) = Partie(Karte("koeln"), new NeuePartie(Startvermoegen: 200_000, Rivalen: 3, Startplaetze: Startplatzvergabe.Vorgabe, Aufstellung: Aufstellungsart.Vorgabe), Sofort.Value);
            Pruefe.Wahr(w.Blitz.Ziele().All(z => z.Kennung != "S5_Werkstatt"), "Bauplatz: kein Ziel");
            var platz = w.Bauen.Grundstuecke.Single(g => g.Kennung == "S5_Werkstatt");
            var e = w.Ausfuehren(spieler, AufWerkstatt(platz, Betriebsart.Giesserei));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            var neu = w.Betriebe.Single(b => b.Konto == spieler && w.Bauen.PlatzVon(b.Name)?.Gebaeude?.Kennung == "S5_Werkstatt");
            var ziel = w.Blitz.Ziele().Single(z => z.Kennung == "S5_Werkstatt");
            Pruefe.Wahr(ReferenceEquals(neu, ziel.Betrieb) && ziel.Name == neu.Name, $"Ziel {ziel.Name} mit Betrieb {ziel.Betrieb?.Name}");
            Pruefe.Wahr(w.Blitz.Ziele().All(z => z.Kennung != "grundstueck:" + w.Bauen.Grundstuecke.Single(g => g.Betrieb == neu.Name).Nummer), "nicht doppelt");
        }),

        ("Hofstellen-Prüfung: Ein Neubau bekommt nie einen Namen, der zum Muster der Spielerwerkstatt passt (alte Greybox, Spieler auf H2 „an der Ostgasse“)", () =>
        {
            var karte = Stadtkarte.AusJson(HofstellenTests.OhneHofstellen(File.ReadAllText(Datenpfad.Karte("greybox"))), HofstellenTests.Katalog.Value);
            Pruefe.Gleich(null, karte.HofstelleMit("H2")!.Name);
            var (_, w, spieler) = Partie(karte, new NeuePartie(Startvermoegen: 200_000, Hofstelle: "H2", Aufstellung: Aufstellungsart.Vorgabe));
            var eigene = w.Betriebe.Single(b => b.Konto == spieler).Name;
            Pruefe.Gleich("Giesserei_Eisenbart", w.Karte!.PlatzVon(eigene)!.Kennung); // Gegenprobe: die eigene Werkstatt steht dort
            spieler.BerufLernen("Tischler");
            var namen = w.Bauen.Bauorte(spieler, new WerkstattBauen(Betriebsart.Tischlerei, 0, 0, 0)).Select(o => w.Bauen.Vorschau(spieler, o).Betrieb!).Distinct().ToList();
            Pruefe.Wahr(namen.Count > 0, "Bauorte für eine Tischlerei");
            Pruefe.Wahr(namen.All(n => w.Karte.PlatzVon(n) is null), "ein Name findet einen Platz der Karte: " + string.Join(", ", namen.Where(n => w.Karte.PlatzVon(n) is not null)));
        }),

        ("Hofstellen-Prüfung: Ohne Los mit gewählter Hofstelle – ein Widerspruch in der Startbelegung (getauschte Wohnhäuser) wird abgewiesen statt eine Partie, die sich nicht laden lässt", () =>
        {
            var json = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte("greybox")))!.AsObject();
            var haeuser = json["start"]!["haeuser"]!.AsArray();
            (haeuser[0]!["wohnhaus"], haeuser[1]!["wohnhaus"]) = ("Wohnhaus3", "Wohnhaus2"); // Eisenbart und Eichwald wohnen auf der Hofstelle des anderen
            var karte = Stadtkarte.AusJson(json.ToJsonString(), HofstellenTests.Katalog.Value);
            Pruefe.WirftMitMeldung(() => Partie(karte, new NeuePartie(Hofstelle: "H2", Startplaetze: Startplatzvergabe.Vorgabe, Aufstellung: Aufstellungsart.Vorgabe)), "doppelt vergeben");
            // Gegenprobe: ohne Wahl genau die Startbelegung, gelost jede Hofstelle einmal – beides lädt
            foreach (var neu in new[] { new NeuePartie(Startplaetze: Startplatzvergabe.Vorgabe, Aufstellung: Aufstellungsart.Vorgabe), new NeuePartie(Hofstelle: "H2", Aufstellung: Aufstellungsart.Vorgabe) })
            {
                var (sim, w, spieler) = Partie(karte, neu);
                Pruefe.Gleich(0, w.Karte!.Pruefen().Count, string.Join(" | ", w.Karte.Pruefen()));
                Reichsgilde.Core.Speichern.Speicherung.Laden(Reichsgilde.Core.Speichern.Speicherung.Speichern(sim, spieler, new DateTime(2026, 1, 1)));
            }
            // Die Startfairness der Messung (Spieler auf jeder Start-Hofstelle, ohne Los) ergibt widerspruchsfreie Partien
            foreach (var (kennung, r) in new[] { ("messstadt", 3), ("greybox", 1), ("greybox", 3), ("probestadt", 2) })
            {
                var m = Messpartie.Spielen(new Messaufbau(kennung, 101, 1, Rotation: r, Startplaetze: Startplatzvergabe.Vorgabe), new Messquellen());
                Pruefe.Wahr(m.Fehler is null, $"{kennung} r={r}: {m.Fehler}");
                var plaetze = Hofstellenvergabe.Startplaetze(Karte(kennung));
                Pruefe.Gleich(plaetze[r % plaetze.Count].Wohnhaus, m.Spieler.Wohnhausplatz, $"{kennung} r={r}");
            }
        }),

        ("Hofstellen-Prüfung: Ein Haus ganz außerhalb der Hofstellen (nur Umland, eigenes Wohnhaus) bleibt beim Los, wo es ist, und zählt für die Startplätze nicht", () =>
        {
            var json = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte("greybox")))!.AsObject();
            json["start"]!["haeuser"]!.AsArray().Add(JsonNode.Parse("""
                {"name": "Haus Ahlbeck", "volk": "Mensch", "geschlecht": "Mann", "wappen": {"teilung": "Geviert", "farbe1": "Rot", "farbe2": "Gold", "figur": "Krone"},
                 "wohnhaus": "Wohnhaus5", "betriebe": [{"platz": "Grube_am_Silberberg", "art": "Mine"}]}
                """));
            var text = json.ToJsonString();
            var e = Kartenentwurf.AusJson(text, HofstellenTests.Katalog.Value);
            Pruefe.Wahr(e.Befunde.All(b => !(b.Regel == "hofstelle" && b.Schwere == Schwere.Fehler)), string.Join(" | ", e.Befunde.Where(b => b.Regel == "hofstelle")));
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "hofstelle" && b.Schwere == Schwere.Hinweis && b.Text.Contains("Haus Ahlbeck")), "Hinweis zum Haus außerhalb");
            var karte = Stadtkarte.AusJson(text, HofstellenTests.Katalog.Value);
            foreach (var startwert in new[] { 1, 2, 3 })
            {
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
                    Daten.Value, new NeuePartie(Rivalen: 4, Aufstellung: Aufstellungsart.Vorgabe), karte: karte);
                var w = sim.Wirtschaft!;
                var ahlbeck = w.Konten.Single(k => k.Inhaber == "Haus Ahlbeck");
                Pruefe.Gleich("Wohnhaus5", w.Bauen.Wohnhaus(ahlbeck)!.Kennung);
                Pruefe.Wahr(w.Betriebe.Any(b => b.Konto == ahlbeck && b.Name == "Grube am Silberberg"), "seine Grube");
            }
        }),

        ("Hofstellen-Prüfung: Auf dem Wohnhausteil einer Hofstelle gilt die Lage weiter – ein Stadtpalais nicht in der Vorstadt, ein Bürgerhaus schon; am Mauerring beide", () =>
        {
            // Greybox mit zwei KI-Häusern: H4 (Wohnhaus4) bleibt frei; sein Wohnhausplatz liegt hier in der Vorstadt
            var json = JsonNode.Parse(File.ReadAllText(Datenpfad.Karte("greybox")))!.AsObject();
            json["gebaeude"]!.AsArray().Single(g => (string)g!["kennung"]! == "Wohnhaus4")!["lage"] = "vorstadt";
            var (_, w, _) = Partie(Stadtkarte.AusJson(json.ToJsonString(), HofstellenTests.Katalog.Value),
                new NeuePartie(Startvermoegen: 500_000, Rivalen: 2, Startplaetze: Startplatzvergabe.Vorgabe, Aufstellung: Aufstellungsart.Vorgabe));
            var vorstadt = w.Bauen.Grundstuecke.Single(g => g.Kennung == "Wohnhaus4");
            Pruefe.Wahr(Bauwesen.IstWohnhausplatz(vorstadt) && vorstadt.Lage == Lage.Vorstadt, $"{vorstadt.Art} {vorstadt.Lage}");
            var gr = vorstadt.Grundriss;
            Pruefe.Gleich(null, w.Bauen.PlatzVerboten(new WohnhausBauen(1, gr.X, gr.Z, gr.Drehung)));
            Pruefe.Wahr(w.Bauen.PlatzVerboten(new WohnhausBauen(4, gr.X, gr.Z, gr.Drehung)) is { } nein && nein.Contains("Kernlage"),
                w.Bauen.PlatzVerboten(new WohnhausBauen(4, gr.X, gr.Z, gr.Drehung)) ?? "erlaubt?");
            // Gegenprobe: ohne die Lage-Änderung (Mauerring) darf dort auch ein Stadtpalais stehen
            var (_, w2, _) = Partie(Karte("greybox"), new NeuePartie(Startvermoegen: 500_000, Rivalen: 2, Startplaetze: Startplatzvergabe.Vorgabe, Aufstellung: Aufstellungsart.Vorgabe));
            Pruefe.Gleich(null, w2.Bauen.PlatzVerboten(new WohnhausBauen(4, gr.X, gr.Z, gr.Drehung)));
        }),
    };
}
