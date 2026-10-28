using Reichsgilde.Core;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests: Die KI-Häuser befördern selbst (PR #224) – sobald die Erfahrung reicht und sich der Brief bezahlt macht – und wählen die Eigenschaft
/// des Vorarbeiters nach dem Gewinn.
/// </summary>
static class KiPersonalTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Ein KI-Haus mit einer Gießerei an Rapieren, Lager voll Zutaten, ohne Karren.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K, Betrieb B, KiRivale Ki) Giesserei(string kraft, double vermoegen = 10_000)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Probe", startvermoegen: vermoegen);
        var b = w.BetriebEroeffnen("Gießerei Probe", Betriebsart.Giesserei, k, startkarren: "");
        b.Produkt = "Rapier";
        b.Einlagern("Eisen", 40);
        b.Einlagern("Silber", 40);
        w.Einstellen(b, kraft, 2);
        return (sim, w, k, b, w.KiAnmelden(k));
    }

    /// <summary>Bis zur Politikstunde der KI (11 Uhr) des laufenden Tages ticken.</summary>
    static void BisZurPolitikstunde(Simulation sim)
    {
        var tag = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == tag && sim.Zeit.UhrzeitInMinuten < 12 * 60) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Personal: befördert einen Lehrling, sobald seine Erfahrung reicht – Gesellenbrief und Aktionspunkt; vorher nicht", () =>
        {
            var (sim, w, k, b, ki) = Giesserei("Lehrling");
            BisZurPolitikstunde(sim);
            Pruefe.Gleich(2, b.Belegung.GetValueOrDefault("Lehrling"));
            foreach (var a in b.Arbeiter) a.Erfahrung = w.Personal.Daten!.Schwelle(2);
            var tag = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == tag) sim.Tick();
            BisZurPolitikstunde(sim);
            Pruefe.Wahr(b.Belegung.GetValueOrDefault("Geselle") >= 1, $"Belegung: {string.Join(", ", b.Belegung)} – {string.Join(" | ", ki.Entscheidungen.Take(5))}");
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("zum Geselle")), string.Join(" | ", ki.Entscheidungen.Take(5)));
        }),

        ("KI-Personal: befördert einen Gesellen zum Vorarbeiter, wenn sich der Brief bezahlt macht, und wählt die Eigenschaft mit dem höchsten Mehrgewinn", () =>
        {
            var (sim, w, k, b, ki) = Giesserei("Geselle", vermoegen: 20_000);
            foreach (var a in b.Arbeiter) a.Erfahrung = w.Personal.Daten!.Schwelle(3);
            Pruefe.Wahr(w.Personal.ErwarteterVorarbeiterGewinn(b) > 80 + 400 / ki.Profil.MaxAmortisationJahre, $"Erwartung {w.Personal.ErwarteterVorarbeiterGewinn(b)}");
            BisZurPolitikstunde(sim);
            var v = b.Arbeiter.SingleOrDefault(a => a.Arbeitskraft == "Vorarbeiter");
            Pruefe.Wahr(v is not null, $"kein Vorarbeiter: {string.Join(" | ", ki.Entscheidungen.Take(5))}");
            Pruefe.Gleich(1, b.Belegung.GetValueOrDefault("Vorarbeiter")); // höchstens einer
            Pruefe.Wahr(v!.Eigenschaft is not null && v.EigenschaftWahl is null, "Eigenschaft gewählt");
            var wahl = w.Personal.EigenschaftenZiehen(b, v);
            // Nach der Wahl wirkt die eigene schon – die anderen rechnen auf ihr auf; gewählt war die mit dem höchsten Wert davor.
            Pruefe.Wahr(wahl.Contains(v.Eigenschaft!), $"{v.Eigenschaft} nicht in {string.Join(", ", wahl)}");
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("wird „")), string.Join(" | ", ki.Entscheidungen.Take(5)));
        }),

        ("KI-Personal: ohne Geld über der halben Reserve kein Brief", () =>
        {
            var (sim, w, k, b, ki) = Giesserei("Lehrling", vermoegen: 600);
            foreach (var a in b.Arbeiter) a.Erfahrung = w.Personal.Daten!.Schwelle(2);
            BisZurPolitikstunde(sim);
            Pruefe.Gleich(2, b.Belegung.GetValueOrDefault("Lehrling"));
        }),
    };
}
