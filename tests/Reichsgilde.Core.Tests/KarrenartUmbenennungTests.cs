using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests: Umbenannte Karrenarten. Seit Datenversion 99 (Nutzer, 23.11.2026) gibt es den Ochsenkarren nicht mehr: Der „Pferdekarren“ mit einem
/// Pferd hat seine Werte, der bisherige „Pferdekarren“ mit zwei Pferden heißt „Großer Pferdekarren“ (bis 04.11.2026 „Kriegskarren“).
/// Spielstände bis Format 3 tragen die alten Namen in ihren mitgespeicherten Daten, in den Karren, in Befehlen und Texten – beim Laden werden
/// sie in einem Durchgang umgestellt (nie zweimal), die Partie läuft genau so weiter.
/// </summary>
static class KarrenartUmbenennungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static readonly DateTime Zeitpunkt = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Ein Haus mit einer Grube (Pferdekarren der Startausstattung) und einem gekauften Großen Pferdekarren, eine Weile gelaufen.</summary>
    static (Simulation Sim, Konto K) GrubeMitGrossemPferdekarren()
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 5_000);
        var b = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k);
        Pruefe.Wahr(w.Ausfuehren(k, new KarrenKaufen(b.Name, "Großer Pferdekarren")).Erfolg, "Großer Pferdekarren gekauft");
        for (var i = 0; i < 300; i++) sim.Tick();
        return (sim, k);
    }

    /// <summary>Ein heutiger Spielstand, wie ihn eine Version bis Datenversion 97 geschrieben hätte: Format 3 und die alten Namen.</summary>
    static string AlsAlterSpielstand(string json, string zweispaenner = "Pferdekarren")
    {
        var alt = Regex.Replace(json, "Großer Pferdekarren|Pferdekarren", m => m.Value == "Pferdekarren" ? "Ochsenkarren" : zweispaenner);
        var knoten = JsonNode.Parse(alt)!.AsObject();
        Pruefe.Gleich(Speicherstand.AktuellesFormat, (int)knoten["Format"]!);
        knoten["Format"] = 3;
        return knoten.ToJsonString();
    }

    /// <summary>Was von den Karren zählt: Name, Art, Zustand, Ankunft, Ladung – und das Geld des Hauses.</summary>
    static string Zustand(Simulation sim) =>
        string.Join(" | ", sim.Wirtschaft!.Konten.Select(k => k.Vermoegen.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .Concat(sim.Wirtschaft.Betriebe.SelectMany(b => b.Karren).Select(c =>
                $"{c.Name}/{c.Typ}/{c.Zustand}/{c.Ankunft}/{string.Join(",", c.Ladung.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}={x.Value}"))}")));

    static void GleichWeiter(Simulation neuSim, Simulation altSim)
    {
        Pruefe.Gleich(Zustand(neuSim), Zustand(altSim));
        neuSim.Takt.IstPausiert = false;
        altSim.Takt.IstPausiert = false;
        for (var i = 0; i < 600; i++) { neuSim.Tick(); altSim.Tick(); }
        Pruefe.Gleich(Zustand(neuSim), Zustand(altSim));
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Karrenart: Die Daten kennen Handkarren, Pferdekarren (2 × 20, 400, 0,8) und Großen Pferdekarren (3 × 25, 900, 1,3), keinen Ochsenkarren", () =>
        {
            var karren = Daten.Value.Logistik.Karren;
            Pruefe.Gleich("Handkarren, Pferdekarren, Großer Pferdekarren", string.Join(", ", karren.Keys));
            var p = karren["Pferdekarren"];
            Pruefe.Gleich((2, 20, 400.0, 0.8), (p.Sorten, p.KapazitaetJeSorte, p.Preis, p.Tempo));
            var g = karren["Großer Pferdekarren"];
            Pruefe.Gleich((3, 25, 900.0, 1.3), (g.Sorten, g.KapazitaetJeSorte, g.Preis, g.Tempo));
            Pruefe.Gleich("Pferdekarren", LogistikDaten.Karrenart("Ochsenkarren"));
            Pruefe.Gleich("Großer Pferdekarren", LogistikDaten.Karrenart("Kriegskarren"));
            Pruefe.Gleich("Pferdekarren", LogistikDaten.Karrenart("Pferdekarren")); // heute der Einspänner: bleibt
            Pruefe.Gleich("Großen Pferdekarren", LogistikDaten.KarrenartAkkusativ("Großer Pferdekarren"));
            Pruefe.Gleich("Pferdekarren", LogistikDaten.KarrenartAkkusativ("Pferdekarren"));
            Pruefe.Wahr(Daten.Value.Betriebsarten.Values.All(a => a.Startkarren is "" or "Handkarren" or "Pferdekarren"), "Startkarren");
        }),

        ("Karrenart: Texte werden in einem Durchgang umgestellt – ein Ochsenkarren wird Pferdekarren, nicht Großer Pferdekarren", () =>
        {
            Pruefe.Gleich("Pferdekarren 1, Großer Pferdekarren 2, Großer Pferdekarren 3",
                Karrenumbenennung.Text("Ochsenkarren 1, Pferdekarren 2, Kriegskarren 3"));
        }),

        ("Karrenart: Ein Spielstand (Format 3) mit Ochsen- und Pferdekarren lädt als Pferdekarren und Großer Pferdekarren und läuft genau wie der heutige weiter", () =>
        {
            var (sim, k) = GrubeMitGrossemPferdekarren();
            var json = Speicherung.Speichern(sim, k, Zeitpunkt);
            var alt = AlsAlterSpielstand(json);
            Pruefe.Wahr(alt.Contains("\"Ochsenkarren\"") && alt.Contains("\"Pferdekarren\"") && !alt.Contains("Großer"), "der alte Spielstand trägt die alten Namen");

            var (neuSim, _) = Speicherung.Laden(json);
            var (altSim, _) = Speicherung.Laden(alt);
            var karren = altSim.Wirtschaft!.Betriebe.Single().Karren;
            Pruefe.Gleich("Pferdekarren 1, Großer Pferdekarren 2", string.Join(", ", karren.Select(c => c.Name)));
            Pruefe.Gleich("Pferdekarren, Großer Pferdekarren", string.Join(", ", karren.Select(c => c.Typ)));
            Pruefe.Gleich("Handkarren, Pferdekarren, Großer Pferdekarren", string.Join(", ", altSim.Wirtschaft.Daten.Logistik.Karren.Keys));
            Pruefe.Gleich(400.0, altSim.Wirtschaft.Daten.Logistik.Karren["Pferdekarren"].Preis);
            GleichWeiter(neuSim, altSim);
        }),

        ("Karrenart: Ein Spielstand (Format 3) mit „Kriegskarren“ lädt ihn als Großen Pferdekarren", () =>
        {
            var (sim, k) = GrubeMitGrossemPferdekarren();
            var json = Speicherung.Speichern(sim, k, Zeitpunkt);
            var alt = AlsAlterSpielstand(json, zweispaenner: "Kriegskarren");
            Pruefe.Wahr(alt.Contains("\"Kriegskarren\""), "der alte Spielstand trägt die alte Kennung");
            var (neuSim, _) = Speicherung.Laden(json);
            var (altSim, _) = Speicherung.Laden(alt);
            Pruefe.Gleich("Pferdekarren 1, Großer Pferdekarren 2", string.Join(", ", altSim.Wirtschaft!.Betriebe.Single().Karren.Select(c => c.Name)));
            GleichWeiter(neuSim, altSim);
        }),

        ("Karrenart: Ein heutiger Spielstand (Format 4) bleibt, wie er ist – sein Pferdekarren wird nicht zum Großen", () =>
        {
            var (sim, k) = GrubeMitGrossemPferdekarren();
            var json = Speicherung.Speichern(sim, k, Zeitpunkt);
            Pruefe.Gleich(4, (int)JsonNode.Parse(json)!["Format"]!);
            var (geladen, _) = Speicherung.Laden(json);
            Pruefe.Gleich("Pferdekarren, Großer Pferdekarren", string.Join(", ", geladen.Wirtschaft!.Betriebe.Single().Karren.Select(c => c.Typ)));
            Pruefe.Gleich(Zustand(sim), Zustand(geladen));
        }),

        ("Karrenart: Befehle mit alter Kennung (Protokoll, Startoption) kaufen Pferdekarren (Ochsenkarren) und Großen Pferdekarren (Kriegskarren)", () =>
        {
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 5_000);
            var b = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k);
            var ergebnis = w.Ausfuehren(k, new KarrenKaufen(b.Name, "Kriegskarren"));
            Pruefe.Wahr(ergebnis.Erfolg, ergebnis.Meldung);
            ergebnis = w.Ausfuehren(k, new KarrenKaufen(b.Name, "Ochsenkarren"));
            Pruefe.Wahr(ergebnis.Erfolg, ergebnis.Meldung);
            Pruefe.Gleich("Großer Pferdekarren 2, Pferdekarren 3", string.Join(", ", b.Karren.Skip(1).Select(c => c.Name)));
            Pruefe.Gleich(5_000 - 900.0 - 400.0, k.Vermoegen);
        }),
    };
}
