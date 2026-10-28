using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Abfragen für die Tooltips (PR #219): Lohn je Arbeiter im Jahr (<see cref="Betrieb.LohnJeArbeiter"/>, wie ihn die Jahresabrechnung zahlt)
/// und Gewinn je Stück nach Steuer, Zutaten, Lohn- und Unterhaltsanteil (<see cref="Kalkulation.GewinnJeStueck"/>).
/// </summary>
static class StueckrechnungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);

    /// <summary>Eine Gießerei voller Gesellen mit Zutaten für das ganze Jahr.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K, Betrieb B) Giesserei()
    {
        var sim = NeueSimulation();
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 20_000);
        var b = w.BetriebEroeffnen("Gießerei Lind", Betriebsart.Giesserei, k, startkarren: "");
        w.Einstellen(b, "Geselle", b.Plaetze);
        foreach (var (zutat, _) in w.Daten.Waren[b.Produkt].Zutaten) b.Einlagern(zutat, 500);
        return (sim, w, k, b);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Tooltips: Lohn je Arbeiter = Grundlohn × Schicht × Lohnstufe – genau das bucht die Jahresabrechnung", () =>
        {
            var (sim, w, _, b) = Giesserei();
            Pruefe.Wahr(w.Ausfuehren(b.Konto, new LohnstufeSetzen(b.Name, Lohnstufe.Knapp)).Erfolg, "Lohnstufe");
            var lohn = b.LohnJeArbeiter("Geselle");
            Pruefe.Ungefaehr(w.Daten.Lohn("Geselle", b.Art) * w.Daten.Werkstatt!.Stufe(Lohnstufe.Knapp).LohnFaktor, lohn, 1e-9);
            Pruefe.Wahr(lohn < w.Daten.Lohn("Geselle", b.Art), "knapper Lohn ist weniger als der Grundlohn");
            Pruefe.Ungefaehr(b.Belegt * lohn, b.Lohnkosten, 1e-9);
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick();
            Pruefe.Ungefaehr(b.Belegt * lohn, b.Vorjahr!.Personal, 1e-6);
        }),

        ("Tooltips: Gewinn je Stück = Preis − Steuer − Zutaten − Lohnanteil − Unterhaltsanteil; über ein Jahr ergeben die Anteile Lohn und Unterhalt", () =>
        {
            var (sim, w, k, b) = Giesserei();
            var ware = b.Produkt;
            var r = Kalkulation.GewinnJeStueck(w, b, ware);
            Pruefe.Ungefaehr(w.Markt[ware].Preis * w.Familien.VerkaufsFaktor(k), r.Preis, 1e-9);
            Pruefe.Ungefaehr(r.Preis * w.SteuersatzMarkt(k), r.Steuer, 1e-9);
            Pruefe.Ungefaehr(w.Daten.Waren[ware].Zutaten.Sum(z => w.Markt[z.Key].Preis * z.Value), r.Zutaten, 1e-9);
            Pruefe.Ungefaehr(b.MinutenJeStueck(ware), r.Minuten, 1e-9);
            Pruefe.Ungefaehr(r.Preis - r.Steuer - r.Zutaten - r.Lohn - r.Unterhalt, r.Gewinn, 1e-9);
            Pruefe.Wahr(r.Lohn > 0 && r.Unterhalt > 0, $"Lohn {r.Lohn}, Unterhalt {r.Unterhalt}");

            // Stück im Jahr bei dieser Leistung: Lohnanteil × Stück = Lohnkosten, Unterhaltsanteil × Stück = Unterhalt.
            var stueck = Kalkulation.Leistung(w, b) * b.Arbeitsminuten / r.Minuten;
            Pruefe.Ungefaehr(b.Lohnkosten, r.Lohn * stueck, 1e-6);
            Pruefe.Ungefaehr(b.Artdaten.Fixkosten, r.Unterhalt * stueck, 1e-6);

            // … und so viele stellt die Werkstatt im Jahr wirklich her (die Laune der Arbeiter ändert das Tempo ein wenig).
            var vorher = b.ImLager(ware);
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick();
            var hergestellt = b.ImLager(ware) - vorher;
            Pruefe.Wahr(Math.Abs(hergestellt - stueck) <= Math.Max(2, 0.15 * stueck), $"{hergestellt} hergestellt, erwartet ≈ {stueck:0.0}");
        }),

        ("Tooltips: Ohne Personal rechnet der Gewinn je Stück mit einem Gesellen; ein Wächter zählt zum Unterhalt", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Lind", startvermoegen: 20_000);
            var b = w.BetriebEroeffnen("Gießerei Lind", Betriebsart.Giesserei, k, startkarren: "");
            Pruefe.Gleich(0, b.Belegt);
            var r = Kalkulation.GewinnJeStueck(w, b, b.Produkt);
            var geselle = w.Daten.Arbeitskraefte["Geselle"].TempoAm(b.Ort) * b.Arbeitsminuten;
            Pruefe.Ungefaehr(b.LohnJeArbeiter("Geselle") / geselle * r.Minuten, r.Lohn, 1e-9);
            Pruefe.Ungefaehr(b.Artdaten.Fixkosten / geselle * r.Minuten, r.Unterhalt, 1e-9);
            Pruefe.Wahr(w.Ausfuehren(k, new WaechterSetzen(b.Name, true)).Erfolg, "Wächter");
            Pruefe.Ungefaehr((b.Artdaten.Fixkosten + w.Daten.Gauner!.WaechterKosten) / geselle * r.Minuten,
                Kalkulation.GewinnJeStueck(w, b, b.Produkt).Unterhalt, 1e-9);
        }),
    };
}
