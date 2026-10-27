using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Gaunerei je Nachtstunde in der Erwartung (PR #219): Hinterzimmer-Tafel und KI rechnen mit allen Versuchen einer Nacht
/// (<c>gauner.nachtVersuche</c> zu je <c>versuchFaktor</c> der Chance) statt mit einem Versuch und der vollen Chance.
/// Der zweite Diebstahl findet ein geplündertes Lager, jeder Einbruch ins Wohnhaus nimmt wieder seinen Anteil, es brennt nur einmal.
/// </summary>
static class KiGaunereiNachtTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Echt = new(() => Wirtschaftsdaten.AusJson(Json.Value));
    static readonly Lazy<Wirtschaftsdaten> EinVersuch = new(() => Wirtschaftsdaten.AusJson(Datenpfad.EinVersuchJeNacht(Json.Value)));

    static Simulation NeueSimulation(Wirtschaftsdaten daten, int startwert = 1400, Spielzeit? zeit = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, zeit, daten);

    /// <summary>Wahrscheinlichkeit von genau j Erfolgen in n Versuchen zu je p.</summary>
    static double Genau(int n, int j, double p)
    {
        double binom = 1;
        for (var i = 1; i <= j; i++) binom = binom * (n - j + i) / i;
        return binom * Math.Pow(p, j) * Math.Pow(1 - p, n - j);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Gaunerei je Nacht: vier Versuche zu je ⅓ – Chance auf mindestens einen, zwei … Erfolge; ihre Summe sind die erwarteten Erfolge", () =>
        {
            var u = NeueSimulation(Echt.Value).Wirtschaft!.Gauner;
            Pruefe.Gleich(4, u.VersucheJeNacht);
            var p = 0.6 * 0.3333;
            Pruefe.Ungefaehr(1 - Math.Pow(1 - p, 4), u.ChanceMindestensErfolge(0.6, 1), 1e-12);
            Pruefe.Ungefaehr(Genau(4, 3, p) + Genau(4, 4, p), u.ChanceMindestensErfolge(0.6, 3), 1e-12);
            Pruefe.Ungefaehr(Math.Pow(p, 4), u.ChanceMindestensErfolge(0.6, 4), 1e-12);
            Pruefe.Gleich(0.0, u.ChanceMindestensErfolge(0.6, 5));
            Pruefe.Ungefaehr(4 * p, Enumerable.Range(1, 4).Sum(k => u.ChanceMindestensErfolge(0.6, k)), 1e-12);
            Pruefe.Ungefaehr(4 * p * 10, u.ErwartetJeNacht(0.6, _ => 10), 1e-9);

            // Mit einem Versuch je Nacht (alte Daten) bleibt es die volle Chance.
            var alt = NeueSimulation(EinVersuch.Value).Wirtschaft!.Gauner;
            Pruefe.Ungefaehr(0.6, alt.ChanceMindestensErfolge(0.6, 1), 1e-12);
            Pruefe.Gleich(0.0, alt.ChanceMindestensErfolge(0.6, 2));
        }),

        ("KI-Gaunerei je Nacht: Einbruch ins Wohnhaus – jeder Erfolg holt wieder die Beute aus der Truhe; Brand höchstens einmal", () =>
        {
            foreach (var (daten, versuche) in new[] { (Echt.Value, 4), (EinVersuch.Value, 1) })
            {
                var w = NeueSimulation(daten).Wirtschaft!;
                var u = w.Gauner;
                var ziel = w.KontoEroeffnen("Haus Reich", startvermoegen: 30_000);
                ziel.WohnhausStufe = 2;
                var ki = w.KontoEroeffnen("Haus Lind", startvermoegen: 5_000);
                var chance = u.ErfolgschanceWohnhaus(Gaunerstueck.Hauseinbruch, ziel, ki);
                var beute = u.Hausbeute(ziel); // 5 % von 30.000, höchstens 300 × 3 = 900 – auch nach drei Griffen noch 900
                Pruefe.Ungefaehr(900, beute, 1e-9);
                var erfolge = versuche == 1 ? chance : 4 * chance * daten.Gauner!.VersuchFaktor;
                Pruefe.Ungefaehr(erfolge * beute - u.Kosten(Gaunerstueck.Hauseinbruch, ki), u.ErwarteterHausgewinn(ziel, ki), 1e-6);
                var brand = u.ErfolgschanceWohnhaus(Gaunerstueck.Brandstiftung, ziel, ki);
                Pruefe.Ungefaehr(u.ChanceMindestensErfolge(brand, 1) * u.Brandschaden(ziel), u.ErwarteterBrandschaden(ziel, ki), 1e-9);
            }
        }),

        ("KI-Gaunerei je Nacht: Diebstahl – der zweite Erfolg findet ein geplündertes Lager; Sabotage wirkt je Erfolg", () =>
        {
            var w = NeueSimulation(Echt.Value).Wirtschaft!;
            var u = w.Gauner;
            var g = u.Daten;
            var dieb = w.KontoEroeffnen("Familie Dieb", startvermoegen: 10_000);
            var opfer = w.KontoEroeffnen("Familie Opfer", startvermoegen: 10_000);
            var b = w.BetriebEroeffnen("Gießerei Opfer", Betriebsart.Giesserei, opfer, startkarren: "");
            b.Einlagern("Eisen", 20); // ein voller Griff (12) und ein halber (8)
            var preis = w.Markt["Eisen"].Preis;
            Pruefe.Gleich($"{12 * preis},{8 * preis},0,0", string.Join(",", u.Warenfolge(b, 12, 4)));
            var chance = u.Erfolgschance(Gaunerstueck.Diebstahl, b, dieb);
            var erwartet = (u.ChanceMindestensErfolge(chance, 1) * 12 + u.ChanceMindestensErfolge(chance, 2) * 8) * preis * g.HehlerAnteil - g.Diebstahl.Kosten;
            Pruefe.Ungefaehr(erwartet, u.ErwarteterGewinn(b, dieb), 1e-6);
            var sabotage = u.Erfolgschance(Gaunerstueck.Sabotage, b, dieb);
            Pruefe.Ungefaehr(4 * sabotage * g.VersuchFaktor * u.Sabotageschaden(b), u.ErwarteterSchaden(b, dieb), 1e-6);
        }),

        ("KI-Gaunerei je Nacht: Die Erwartung trifft, was die Bande im Mittel aus dem Wohnhaus holt (300 Nächte)", () =>
        {
            var erfolge = 0;
            double erwartet = 0;
            const int naechte = 300;
            for (var n = 0; n < naechte; n++)
            {
                var sim = NeueSimulation(Echt.Value, 2000 + n, new Spielzeit(19 * 60 - Spielzeit.TagesbeginnMinute));
                var w = sim.Wirtschaft!;
                var ziel = w.KontoEroeffnen("Haus Reich", startvermoegen: 30_000);
                ziel.WohnhausStufe = 2;
                var dieb = w.KontoEroeffnen("Haus Lind", startvermoegen: 5_000);
                if (n == 0) erwartet = w.Gauner.ErwarteterHausgewinn(ziel, dieb) + w.Gauner.Kosten(Gaunerstueck.Hauseinbruch, dieb);
                Pruefe.Wahr(w.Ausfuehren(dieb, new Beauftragen(ziel.Inhaber, Gaunerstueck.Hauseinbruch, null)).Erfolg, "Auftrag");
                while (sim.Zeit.Jahr == Spielzeit.StartJahr) sim.Tick(); // 20 bis 23 Uhr, der letzte Versuch vor der Jahresrechnung
                erfolge += w.Gauner.NachrichtenFuer(dieb, true).Concat(w.Gauner.NachrichtenFuer(dieb, false))
                    .Count(t => t.StartsWith("Eure Leute haben im Wohnhaus"));
            }
            var mittel = 900.0 * erfolge / naechte; // jeder Erfolg 900 (5 % von 30.000 über dem Deckel)
            // Erwartet ≈ 0,8 Erfolge zu 900; Fehler des Mittels bei 300 Nächten ≈ 45
            Pruefe.Wahr(Math.Abs(mittel - erwartet) < 150, $"Beute im Mittel {mittel:0}, erwartet {erwartet:0}");
        }),

        ("KI-Gaunerei je Nacht: Die KI wählt nach der Erwartung über alle Versuche – ein Lager für einen Griff verliert gegen die Truhe", () =>
        {
            // Ein reiches Haus mit Steinhaus (Truhe 900 je Erfolg) und einer Gießerei mit wenig teurer Ware (ein Griff).
            (Simulation Sim, Stadtwirtschaft W, Konto Ki, Betrieb B, Konto Ziel) Partie(Wirtschaftsdaten daten, int stueck)
            {
                var sim = NeueSimulation(daten);
                var w = sim.Wirtschaft!;
                var ziel = w.KontoEroeffnen("Haus Reich", startvermoegen: 30_000);
                ziel.WohnhausStufe = 2;
                var b = w.BetriebEroeffnen("Gießerei Reich", Betriebsart.Giesserei, ziel, startkarren: "");
                b.Einlagern("Goldkette", stueck);
                var ki = w.KontoEroeffnen("Haus Lind", startvermoegen: 5_000);
                w.KiAnmelden(ki, new KiProfil(Gaunerei: 1));
                return (sim, w, ki, b, ziel);
            }
            // So viele Goldketten, dass mit einem Versuch und voller Chance der Diebstahl knapp vorn liegt.
            var stueck = Enumerable.Range(1, 12).First(n =>
            {
                var (_, w, ki, b, ziel) = Partie(EinVersuch.Value, n);
                return w.Gauner.ErwarteterGewinn(b, ki) > w.Gauner.ErwarteterHausgewinn(ziel, ki);
            });
            foreach (var (daten, erwartet) in new[] { (EinVersuch.Value, "Diebstahl"), (Echt.Value, "Einbruch ins Wohnhaus") })
            {
                var (sim, w, ki, b, ziel) = Partie(daten, stueck);
                var dieb = w.Gauner.ErwarteterGewinn(b, ki);
                var haus = w.Gauner.ErwarteterHausgewinn(ziel, ki);
                Pruefe.Wahr(erwartet == "Diebstahl" ? dieb > haus : haus > dieb, $"{erwartet}: Diebstahl {dieb:0}, Einbruch {haus:0} ({stueck} Goldketten)");
                var auftraege = new List<string>();
                w.Meldung += m => { if (m.StartsWith(ki.Inhaber + ": heuert im Hinterzimmer")) auftraege.Add(m); };
                for (var i = 0; i < Spielzeit.MinutenProTag; i++) sim.Tick();
                Pruefe.Wahr(auftraege.Count >= 1 && auftraege[0].Contains(erwartet), string.Join(" | ", auftraege));
            }
        }),
    };
}
