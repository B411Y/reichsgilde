using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests: Die KI-Häuser und die Jahreszeit (Datenversion 67) – im strengen Winter kein Schiff mehr auf den Fluss (schon bevor er zufriert, die
/// Meldung zum Jahresbeginn sagt es an), und im späten Herbst füllt der Verwalter die Zutaten, die im Winter teurer werden oder mit der Ernte billig
/// sind, bis zur Lagergrenze auf.
/// </summary>
static class KiJahreszeitenTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));

    const string SchiffZeile = "\"schiffe\": { \"hoechstens\": 1, \"mindestGewinnJeFahrt\": 100, \"auslastung\": 0.5, \"hoechstensJahre\": 4,";

    /// <summary>Die echten Daten, ein Schiff lohnt sich für die KI schon in 8 Jahren (die leere Stadt hat einen kleinen Markt, wie in KiSchiffTests).</summary>
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() =>
    {
        Pruefe.Wahr(Json.Value.Contains(SchiffZeile), "ki.schiffe steht nicht wie erwartet in wirtschaft.json");
        return Wirtschaftsdaten.AusJson(Json.Value.Replace(SchiffZeile, SchiffZeile.Replace("\"hoechstensJahre\": 4", "\"hoechstensJahre\": 8")));
    });

    static long Minute(int jahr, int stunde) => jahr * (long)Spielzeit.MinutenProTag + stunde * 60 - Spielzeit.TagesbeginnMinute;

    /// <summary>Der erste Startwert mit strengem Winter 1403, an dem der Fluss erst um 9 oder 10 Uhr zufriert.</summary>
    static int StrengerStartwert()
    {
        for (var s = 1; s < 5000; s++)
            if (new Stadtwirtschaft(Daten.Value, Partieeinstellungen.Standard.StartStatut, s).Jahreszeiten.WinterVon(1403) is { Streng: true } l
                && l.ZufrierenMinute >= 9 * 60 - Spielzeit.TagesbeginnMinute)
                return s;
        throw new Exception("kein Startwert gefunden");
    }

    /// <summary>Ein KI-Haus mit Geld in der Probestadt (mit Hafen) zu dieser Minute.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K, KiRivale Ki) KiHaus(long minute, int startwert = 1400)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert },
            new Spielzeit(minute), Daten.Value);
        var w = sim.Wirtschaft!;
        w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte("probestadt")));
        var k = w.KontoEroeffnen("Haus Winterhafen", startvermoegen: 30_000);
        return (sim, w, k, w.KiAnmelden(k));
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Jahreszeiten: Im strengen Winter bleibt das Schiff der KI am Kai – vor dem Zufrieren und im Eis; im Sommer fährt es los", () =>
        {
            var (_, sommer, ks, kiSommer) = KiHaus(Minute(1, 6));
            kiSommer.Jahresende(); // kauft ein Schiff
            var schiffSommer = sommer.Schiffahrt.SchiffeVon(ks).Single();
            kiSommer.Stunde();
            Pruefe.Wahr(schiffSommer.Zustand != SchiffZustand.AmKai, "im Sommer schickt die KI das Schiff los");

            var s = StrengerStartwert();
            var (sim, w, k, ki) = KiHaus(Minute(3, 5), s);
            var lage = w.Jahreszeiten.DiesesJahr!;
            ki.Jahresende();
            var schiff = w.Schiffahrt.SchiffeVon(k).Single();
            Pruefe.Wahr(w.Jahreszeiten.EisDroht, "Eis droht");
            ki.Stunde();
            Pruefe.Gleich(SchiffZustand.AmKai, schiff.Zustand);
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("lässt die Schiffe am Kai") && e.Contains("Sandfluss")), string.Join(" | ", ki.Entscheidungen));
            while (sim.Zeit.MinuteImTag < lage.TauwetterMinute && sim.Zeit.MinuteImTag > 0) sim.Tick(); // die KI überlegt jede Stunde mit
            Pruefe.Gleich(SchiffZustand.AmKai, schiff.Zustand);
            if (!lage.BisJahresende)
            {
                for (var i = 0; i < 61; i++) sim.Tick(); // nach dem Eisgang: die nächste volle Stunde
                Pruefe.Wahr(schiff.Zustand != SchiffZustand.AmKai, "nach dem Eisgang fährt es wieder");
            }
        }),

        ("KI-Jahreszeiten: Vorrat vor dem Winter – im Herbst ab 17 Uhr füllt der KI-Verwalter Holz, Gerste, Roggen, Kräuter bis zur Lagergrenze, sonst nicht", () =>
        {
            var (_, w, k, ki) = KiHaus(Minute(2, 17));
            var tischlerei = w.BetriebEroeffnen("Tischlerei Vorrat", Betriebsart.Tischlerei, k);
            var schaenke = w.BetriebEroeffnen("Schänke Vorrat", Betriebsart.Schaenke, k);
            Pruefe.Gleich(tischlerei.LagerKapazitaetJeWare, ki.WinterVorrat(tischlerei, "Holz"));
            Pruefe.Gleich(schaenke.LagerKapazitaetJeWare, ki.WinterVorrat(schaenke, "Gerste"));
            Pruefe.Gleich(null, ki.WinterVorrat(tischlerei, "Holzzapfen")); // weder im Winter gefragt noch Ernte
            Pruefe.Gleich(null, ki.WinterVorrat(tischlerei, "Beschlag"));
            w.Uhr = new Spielzeit(Minute(2, 16));
            Pruefe.Gleich(null, ki.WinterVorrat(tischlerei, "Holz"));
            w.Uhr = new Spielzeit(Minute(1, 18)); // Sommer
            Pruefe.Gleich(null, ki.WinterVorrat(tischlerei, "Holz"));
            w.Uhr = new Spielzeit(Minute(3, 18)); // Winter: der Vorrat ist angelegt, jetzt wird verbraucht
            Pruefe.Gleich(null, ki.WinterVorrat(tischlerei, "Holz"));
        }),

        ("KI-Jahreszeiten: Der Verwalter einer KI-Tischlerei kauft im späten Herbst Holz bis zur Lagergrenze (40), im Sommer den üblichen Vorrat (20)", () =>
        {
            int Holz(long minute)
            {
                var (sim, w, k, _) = KiHaus(minute);
                // Ein Kriegskarren (drei Ladeflächen zu 25): Ohne Leute ist der übliche Vorrat Holz für 10 Schemel (20 Stück), im späten Herbst 40.
                var t = w.BetriebEroeffnen("Tischlerei Vorrat", Betriebsart.Tischlerei, k, startkarren: "Kriegskarren");
                var karren = t.Karren[0];
                // leeres Lager: Der Verwalter fährt gleich los und kauft ein; gezählt wird, was er vom Markt mitbringt
                for (var i = 0; i < 120 && karren.Zustand != KarrenZustand.ZumBetrieb; i++) sim.Tick();
                Pruefe.Gleich(KarrenZustand.ZumBetrieb, karren.Zustand);
                return t.ImLager("Holz") + karren.Ladung.GetValueOrDefault("Holz");
            }
            var herbst = Holz(Minute(2, 17));
            var sommer = Holz(Minute(1, 17));
            Pruefe.Gleich((40, 20), (herbst, sommer));
        }),
    };
}
