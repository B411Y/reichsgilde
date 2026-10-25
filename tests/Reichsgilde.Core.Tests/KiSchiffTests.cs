using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests: Die KI-Häuser treiben Schiffshandel nach derselben Gewinnrechnung wie der Spieler (PR #199) – sie kaufen zum Jahresende ein Schiff,
/// wenn es sich in höchstens 4 Jahren bezahlt macht, und schicken es jede Stunde mit dem besten Auftrag und dem günstigsten Geleit los.
/// </summary>
static class KiSchiffTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    const string KiZeile = "\"schiffe\": { \"hoechstens\": 1, \"mindestGewinnJeFahrt\": 100, \"auslastung\": 0.5, \"hoechstensJahre\": 4, \"charakterJahre\": 1, \"reserveFaktor\": 2 }";

    static Wirtschaftsdaten DatenMit(string ersatz)
    {
        Pruefe.Wahr(Json.Value.Contains(KiZeile), "ki.schiffe steht nicht wie erwartet in wirtschaft.json");
        return Wirtschaftsdaten.AusJson(Json.Value.Replace(KiZeile, ersatz));
    }

    static Wirtschaftsdaten Grenze(double jahre) =>
        DatenMit(KiZeile.Replace("\"hoechstensJahre\": 4", $"\"hoechstensJahre\": {jahre.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));

    /// <summary>Ein KI-Haus ohne Betriebe in einer Stadt mit Karte, um 9 Uhr im ersten Jahr.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K, KiRivale Ki) KiHaus(double geld = 30_000, Wirtschaftsdaten? daten = null, string karte = "probestadt")
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
            new Spielzeit(9 * 60 - Spielzeit.TagesbeginnMinute), daten ?? Daten.Value);
        var w = sim.Wirtschaft!;
        w.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte(karte)));
        var k = w.KontoEroeffnen("Haus Fernhandel", startvermoegen: geld);
        return (sim, w, k, w.KiAnmelden(k));
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI kauft zum Jahresende ein Schiff, wenn es sich in höchstens 4 Jahren bezahlt macht – höchstens eins; nicht ohne Geld über der Reserve", () =>
        {
            // In der leeren Stadt (kein Haus wirtschaftet, kleiner Markt) braucht ein Schiff knapp 5 Jahre – die Grenze hier 6.
            var (_, w, k, ki) = KiHaus(daten: Grenze(6));
            var typ = w.Schiffahrt.Daten!.Schiffe["Flussschiff"];
            var v = Kalkulation.BesteSchiffsfahrt(w, k, typ, k.Vermoegen - typ.Preis - 2 * ki.Profil.Reserve)!;
            var jahre = typ.Preis / (v.Rechnung.GewinnJeJahr * 0.5 - typ.Unterhalt);
            Pruefe.Wahr(jahre is > 0 and <= 6, $"ein Schiff lohnt ({jahre:0.0} Jahre)");
            Pruefe.Gleich(6.0, ki.SchiffHoechstensJahre);
            Pruefe.Gleich(4.0, KiHaus().Ki.SchiffHoechstensJahre);
            ki.Jahresende();
            Pruefe.Gleich(1, w.Schiffahrt.SchiffeVon(k).Count());
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("kauft ein Flussschiff") && e.Contains("bezahlt in")), "begründet");
            ki.Jahresende();
            Pruefe.Gleich(1, w.Schiffahrt.SchiffeVon(k).Count());

            var (_, w2, k2, ki2) = KiHaus(geld: typ.Preis + 2 * ki2Reserve() - 1, daten: Grenze(6));
            ki2.Jahresende();
            Pruefe.Gleich(0, w2.Schiffahrt.SchiffeVon(k2).Count());
            static double ki2Reserve() => KiProfil.Standard.Reserve;
        }),

        ("KI ohne Hafen oder ohne Abschnitt ki.schiffe kauft kein Schiff; mit strenger Grenze auch nicht", () =>
        {
            var (_, w, k, ki) = KiHaus(daten: DatenMit("\"aus\": {}"));
            Pruefe.Gleich(null, ki.SchiffHoechstensJahre);
            ki.Jahresende();
            Pruefe.Gleich(0, w.Schiffahrt.SchiffeVon(k).Count());
            var (_, w2, k2, ki2) = KiHaus(daten: Grenze(1.1));
            ki2.Jahresende();
            Pruefe.Gleich(0, w2.Schiffahrt.SchiffeVon(k2).Count());
            var (_, w3, k3, ki3) = KiHaus(karte: "messstadt");
            ki3.Jahresende();
            Pruefe.Wahr(!w3.Schiffahrt.IstAktiv && !w3.Schiffahrt.SchiffeVon(k3).Any(), "Messstadt ohne Fluss");
        }),

        ("KI schickt ihr Schiff in der nächsten Stunde los: der beste Auftrag, das Geleit mit dem kleinsten Lohn plus Raub", () =>
        {
            var (_, w, k, ki) = KiHaus(daten: Grenze(6));
            ki.Jahresende();
            var s = w.Schiffahrt.SchiffeVon(k).Single();
            var budget = k.Vermoegen - ki.Profil.Reserve;
            var v = Kalkulation.BesteSchiffsfahrt(w, k, s.Daten, budget)!;
            var stadt = w.Schiffahrt.Stadt(v.Ziel)!;
            var geleit = w.Daten.Logistik.Eskorten.Keys.Where(w.Wache.EskorteVerfuegbar)
                .OrderBy(e => Kalkulation.Schiffsfahrt(w, k, s.Daten, stadt, v.Hinfracht, v.Rueckfracht, false, e) is var r ? r.Geleit + r.Risiko : 0)
                .First();
            var erwartet = Kalkulation.BesteSchiffsfahrt(w, k, s.Daten, budget, geleit)!;
            ki.Stunde();
            Pruefe.Gleich(SchiffZustand.Beladen, s.Zustand);
            Pruefe.Gleich(geleit, s.Geleit);
            Pruefe.Gleich(erwartet.Ziel, s.Auftrag!.Ziel);
            static string Text(IReadOnlyDictionary<string, int> x) => string.Join(", ", x.OrderBy(y => y.Key, StringComparer.Ordinal).Select(y => $"{y.Key}={y.Value}"));
            Pruefe.Gleich(Text(erwartet.Hinfracht), Text(s.Auftrag.Hinfracht));
            Pruefe.Gleich(Text(erwartet.Rueckfracht), Text(s.Auftrag.Rueckfracht));
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains($"schickt {s.Name} nach {erwartet.Ziel}") && e.Contains("erwarteter Gewinn")), "begründet");
        }),

        ("KI lässt das Schiff am Kai, wenn keine Fahrt den Mindestgewinn bringt", () =>
        {
            var (_, w, k, ki) = KiHaus(daten: DatenMit(KiZeile.Replace("\"mindestGewinnJeFahrt\": 100", "\"mindestGewinnJeFahrt\": 1000000")));
            w.Ausfuehren(k, new SchiffKaufen());
            ki.Stunde();
            Pruefe.Gleich(SchiffZustand.AmKai, w.Schiffahrt.SchiffeVon(k).Single().Zustand);
        }),

        ("Ein Jahr KI-Schiffshandel in der Probestadt: Das Schiff fährt mehrmals und bringt nach Unterhalt Gewinn", () =>
        {
            var (sim, w, k, ki) = KiHaus(daten: Grenze(6));
            ki.Jahresende();
            var s = w.Schiffahrt.SchiffeVon(k).Single();
            while (sim.Zeit.Jahr < Spielzeit.StartJahr + 2) sim.Tick();
            Pruefe.Wahr(s.Fahrten >= 4, $"Fahrten: {s.Fahrten}");
            Pruefe.Wahr(s.ErtragVorjahr > 0, $"Ertrag im Vorjahr nach Unterhalt: {s.ErtragVorjahr}");
        }),
    };
}
