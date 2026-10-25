using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests: Karren verkaufen (PR #205) – Befehl, Verbote, Erlös (halber Kaufpreis), Ladung ins Lager, Fahrten je Jahr, die Regeln der KI
/// (in der Not, ein ganzes Jahr ungenutzt, nie der letzte Karren) und alte Spielstände.
/// </summary>
static class KarrenVerkaufenTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten ?? Daten.Value);

    /// <summary>Ein Haus mit einer Gießerei (Handkarren der Startausstattung) und einem gekauften Ochsenkarren.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K, Betrieb B) GiessereiMitZweiKarren()
    {
        var sim = NeueSimulation();
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 5_000);
        var b = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k);
        Pruefe.Wahr(w.Ausfuehren(k, new KarrenKaufen(b.Name, "Ochsenkarren")).Erfolg, "Ochsenkarren gekauft");
        return (sim, w, k, b);
    }

    /// <summary>Bis zum Jahreswechsel (die Jahresabrechnung ist gelaufen), dann noch so viele Minuten.</summary>
    static void Jahr(Simulation sim, int danach = 0)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
        for (var i = 0; i < danach; i++) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Karren verkaufen: halber Kaufpreis als Verkaufserlös, der Karren ist weg, die Meldung nennt Karren und Erlös", () =>
        {
            var (_, w, k, b) = GiessereiMitZweiKarren();
            var vorher = k.Vermoegen;
            Pruefe.Ungefaehr(200, w.KarrenErloes(b.Karren[1]), 1e-9); // Ochsenkarren 400 × 0,5
            var e = w.Ausfuehren(k, new KarrenVerkaufen(b.Name, 1));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Wahr(e.Meldung.Contains("Ochsenkarren 2") && e.Meldung.Contains("200"), e.Meldung);
            Pruefe.Gleich("Handkarren", b.Karren.Single().Typ);
            Pruefe.Ungefaehr(vorher + 200, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(200, k.Laufend.Verkaeufe, 1e-9);
        }),

        ("Karren verkaufen: der Anteil kommt aus logistik.karrenVerkaufAnteil; außerhalb 0 bis 1 fällt er auf", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte(
                File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"karrenVerkaufAnteil\": 0.5", "\"karrenVerkaufAnteil\": 0.25")));
            Pruefe.Ungefaehr(0.5, Daten.Value.Logistik.KarrenVerkaufAnteil, 1e-12);
            var w = NeueSimulation(daten).Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Lind");
            var b = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k);
            Pruefe.Ungefaehr(150 * 0.25, w.KarrenErloes(b.Karren[0]), 1e-9);
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(
                File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"karrenVerkaufAnteil\": 0.5", "\"karrenVerkaufAnteil\": 1.5")), "karrenVerkaufAnteil");
        }),

        ("Karren verkaufen: nur der eigene, nur eine Nummer, die es gibt, nicht unterwegs – der letzte darf der Spieler verkaufen", () =>
        {
            var (sim, w, k, b) = GiessereiMitZweiKarren();
            var fremd = w.KontoEroeffnen("Haus Eichwald");
            Pruefe.Wahr(!w.Ausfuehren(fremd, new KarrenVerkaufen(b.Name, 0)).Erfolg, "fremder Befehl");
            Pruefe.Wahr(w.KarrenVerkaufVerboten(fremd, b, 0)!.Contains("gehört nicht"), "fremder Karren");
            Pruefe.Wahr(w.KarrenVerkaufVerboten(k, b, 2)!.Contains("keinen Karren Nr. 3"), "Nummer zu groß");
            Pruefe.Wahr(w.KarrenVerkaufVerboten(k, b, -1) is not null, "Nummer negativ");
            Pruefe.Gleich<string?>(null, w.KarrenVerkaufVerboten(k, b, 0));

            // Ohne Zutaten im Lager fährt ein Karren gleich los, um welche zu holen.
            for (var i = 0; i < 120 && b.Karren.All(x => x.Zustand == KarrenZustand.AmBetrieb); i++) sim.Tick();
            var nr = b.Karren.ToList().FindIndex(x => x.Zustand != KarrenZustand.AmBetrieb);
            Pruefe.Wahr(nr >= 0, "ein Karren ist unterwegs");
            Pruefe.Wahr(w.KarrenVerkaufVerboten(k, b, nr)!.Contains("unterwegs"), "unterwegs gesperrt");
            var e = w.Ausfuehren(k, new KarrenVerkaufen(b.Name, nr));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("unterwegs"), e.Meldung);
            Pruefe.Gleich(2, b.Karren.Count);

            // Zurück am Betrieb: Beide lassen sich verkaufen, auch der letzte (der Spieler entscheidet selbst).
            for (var i = 0; i < 1000 && b.Karren.Any(x => x.Zustand != KarrenZustand.AmBetrieb); i++) sim.Tick();
            Pruefe.Wahr(w.Ausfuehren(k, new KarrenVerkaufen(b.Name, 1)).Erfolg, "zweiter verkauft");
            Pruefe.Wahr(w.Ausfuehren(k, new KarrenVerkaufen(b.Name, 0)).Erfolg, "letzter verkauft");
            Pruefe.Gleich(0, b.Karren.Count);
        }),

        ("Karren verkaufen: Eine Ladung kommt vorher ins Lager, ein vorgemerkter Auftrag verfällt", () =>
        {
            var (_, w, k, b) = GiessereiMitZweiKarren();
            var karren = b.Karren[1];
            karren.Wiederherstellen(karren.Sichern() with { Ladung = new() { ["Eisen"] = 7 } });
            karren.Vormerkung = new Vormerkung(new Dictionary<string, int>(), new Dictionary<string, int> { ["Eisen"] = 5 });
            var lager = b.ImLager("Eisen");
            var e = w.Ausfuehren(k, new KarrenVerkaufen(b.Name, 1));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(lager + 7, b.ImLager("Eisen"));
            Pruefe.Wahr(e.Meldung.Contains("7 Eisen") && e.Meldung.Contains("verfällt"), e.Meldung);
        }),

        ("Karren verkaufen: Ein neuer Karren nimmt die nächste freie Nummer – kein Name doppelt", () =>
        {
            var (_, w, k, b) = GiessereiMitZweiKarren(); // Handkarren 1, Ochsenkarren 2
            w.Ausfuehren(k, new KarrenVerkaufen(b.Name, 0));
            w.Ausfuehren(k, new KarrenKaufen(b.Name, "Ochsenkarren"));
            Pruefe.Gleich("Ochsenkarren 2,Ochsenkarren 3", string.Join(",", b.Karren.Select(x => x.Name)));
            w.Ausfuehren(k, new KarrenKaufen(b.Name, "Handkarren"));
            Pruefe.Gleich("Handkarren 3", b.Karren[^1].Name); // die Nummer zählt über alle Typen, Handkarren 3 gab es noch nicht
        }),

        ("Fahrten: gezählt je Jahr; das Vorjahr gilt erst, wenn der Karren es ganz erlebt hat", () =>
        {
            var (sim, _, _, b) = GiessereiMitZweiKarren();
            for (var i = 0; i < 120 && b.Karren.All(x => x.FahrtenImJahr == 0); i++) sim.Tick();
            Pruefe.Wahr(b.Karren.Sum(x => x.FahrtenImJahr) >= 1, "eine Fahrt gezählt");
            Jahr(sim);
            Pruefe.Wahr(b.Karren.All(x => x.FahrtenVorjahr is null), "erstes Jahr nicht ganz");
            Pruefe.Wahr(b.Karren.All(x => x.FahrtenImJahr <= 1), "neu gezählt");
            Jahr(sim);
            Pruefe.Wahr(b.Karren.All(x => x.FahrtenVorjahr is not null), "zweites Jahr ganz");
        }),

        ("KI: Ein Karren, der ein ganzes Jahr nicht fuhr, wird zum Jahresende verkauft – je Jahr einer, der letzte bleibt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 50_000);
            var grube = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k); // ohne Personal: keine Ware, keine Zutaten – kein Karren fährt
            w.Ausfuehren(k, new KarrenKaufen(grube.Name, "Ochsenkarren"));
            w.Ausfuehren(k, new KarrenKaufen(grube.Name, "Kriegskarren"));
            var ki = w.KiAnmelden(k, new KiProfil(Reserve: 1e9)); // stellt niemanden ein, kauft und baut nichts
            Jahr(sim, danach: 1);
            Pruefe.Gleich(3, grube.Karren.Count); // das erste Jahr zählt nicht ganz
            Jahr(sim, danach: 1);
            Pruefe.Gleich("Ochsenkarren 1,Ochsenkarren 2", string.Join(",", grube.Karren.Select(x => x.Name)));
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("verkauft Kriegskarren 3") && e.Contains("nicht gefahren")), "Begründung");
            Pruefe.Ungefaehr(450, k.Laufend.Verkaeufe, 1e-9); // Kriegskarren 900 × 0,5 – schon im neuen Jahr gebucht
            Jahr(sim, danach: 1);
            Jahr(sim, danach: 1);
            Pruefe.Gleich("Ochsenkarren 1", string.Join(",", grube.Karren.Select(x => x.Name)));
        }),

        ("KI: Ein Betrieb, der oft stillstand, behält seine Karren – auch einen, der nicht fuhr", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 50_000);
            var grube = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k);
            w.Ausfuehren(k, new KarrenKaufen(grube.Name, "Ochsenkarren"));
            w.KiAnmelden(k, new KiProfil(Reserve: 1e9, StillstandFuerKarrenkauf: 0)); // jeder Stillstand (auch keiner) gilt als „zu oft“
            Jahr(sim, danach: 1);
            Jahr(sim, danach: 1);
            Pruefe.Gleich(2, grube.Karren.Count);
        }),

        ("KI in Not: verkauft den Karren, der am meisten bringt – nie den letzten eines Betriebs", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Lind", startvermoegen: -3_000); // verschuldet
            var grube = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k);
            grube.KarrenHinzufuegen("Kriegskarren"); // geschenkt – das Haus hat kein Geld
            Jahr(sim, danach: 1);
            Pruefe.Wahr(k.Schuldenjahre > 0, $"gemahnt (Reinvermögen {k.Reinvermoegen})");
            var ki = w.KiAnmelden(k, new KiProfil(Reserve: 1e9));
            for (var i = 0; i < 6 * 60 && grube.Karren.Count == 2; i++) sim.Tick();
            Pruefe.Gleich("Ochsenkarren 1", string.Join(",", grube.Karren.Select(x => x.Name)));
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("verkauft Kriegskarren 2") && e.Contains("Kämmerer")), "Begründung");
            for (var i = 0; i < 6 * 60; i++) sim.Tick();
            Pruefe.Gleich(1, grube.Karren.Count);
        }),

        ("Speichern: Fahrten bleiben erhalten; alte Spielstände ohne Fahrten laden, ihr laufendes Jahr zählt nicht ganz", () =>
        {
            var (sim, w, k, b) = GiessereiMitZweiKarren();
            Jahr(sim, danach: 200);
            var json = Speicherung.Speichern(sim, k);
            var w2 = Speicherung.Laden(json).Simulation.Wirtschaft!;
            var b2 = w2.Betriebe.Single(x => x.Name == b.Name);
            Pruefe.Gleich(string.Join(",", b.Karren.Select(x => $"{x.FahrtenImJahr}/{x.FahrtenVorjahr}")),
                          string.Join(",", b2.Karren.Select(x => $"{x.FahrtenImJahr}/{x.FahrtenVorjahr}")));

            var knoten = JsonNode.Parse(json)!;
            foreach (var karren in Knoten(knoten).Where(n => n is JsonObject o && o.Any(x => x.Key.Equals("nachtsFahren", StringComparison.OrdinalIgnoreCase))).ToList())
                foreach (var feld in karren!.AsObject().Select(x => x.Key).Where(x => x.StartsWith("fahrten", StringComparison.OrdinalIgnoreCase)).ToList())
                    karren.AsObject().Remove(feld);
            var (alt, _) = Speicherung.Laden(knoten.ToJsonString());
            var b3 = alt.Wirtschaft!.Betriebe.Single(x => x.Name == b.Name);
            Pruefe.Wahr(b3.Karren.All(x => x.FahrtenImJahr == 0 && x.FahrtenVorjahr is null), "alter Stand: keine Fahrten");
            Jahr(alt);
            Pruefe.Wahr(b3.Karren.All(x => x.FahrtenVorjahr is null), "das angebrochene Jahr zählt nicht als ungenutzt");
        }),
    };

    /// <summary>Alle Knoten eines JSON-Baums.</summary>
    static IEnumerable<JsonNode?> Knoten(JsonNode? n)
    {
        yield return n;
        if (n is JsonObject o) foreach (var (_, v) in o) foreach (var x in Knoten(v)) yield return x;
        if (n is JsonArray a) foreach (var v in a) foreach (var x in Knoten(v)) yield return x;
    }
}
