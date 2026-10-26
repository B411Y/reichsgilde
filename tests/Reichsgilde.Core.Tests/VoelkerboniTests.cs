using Reichsgilde.Core.Zeit;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für die Völkerboni je Beruf: Daten, Leistung in Betrieben mit und ohne Ware, Erfahrung, Kalkulation der KI.</summary>
static class VoelkerboniTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Ohne Diebesfang – sonst verschwinden Diebe zufällig aus dem Vergleich.</summary>
    static readonly Lazy<Wirtschaftsdaten> Unerwischt = new(() => Wirtschaftsdaten.AusJson(Json.Replace("\"fangJeStunde\": 0.012", "\"fangJeStunde\": 0")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    /// <summary>Ein Haus dieses Volkes mit einem Betrieb, alle Talente 0 (nur der Völkerbonus soll wirken).</summary>
    static (Konto K, Betrieb B) Haus(Simulation sim, string name, Volk volk, Betriebsart art, string kraft, int anzahl)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: 20_000);
        var f = w.Familien.Gruenden(k, volk, Geschlecht.Mann);
        foreach (var t in TalentTexte.Alle) f.Oberhaupt.TalentSetzen(t, 0);
        var b = w.BetriebEroeffnen($"{art} {name}", art, k);
        w.Einstellen(b, kraft, anzahl);
        return (k, b);
    }

    static double Stueck(Konto k) => k.Laufend.Hergestellt.Values.Sum();

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: Zwerge Schmied/Steinmetz/Geldleiher +5 %, Dieb/Parfumeur −5 %; Elben Gelehrte +, Grobarbeit −; Halblinge Wirt/Dieb +, Gardist/Schmied −; Menschen ohne Mali", () =>
        {
            var b = Daten.Value.Berufe;
            Pruefe.Gleich(0.05, b["Schmied"].Voelker[Volk.Zwerg]);
            Pruefe.Gleich(-0.05, b["Schmied"].Voelker[Volk.Halbling]);
            Pruefe.Gleich(0.05, b["Geldleiher"].Voelker[Volk.Zwerg]);
            Pruefe.Gleich(-0.05, b["Dieb"].Voelker[Volk.Zwerg]);
            Pruefe.Gleich(0.05, b["Prediger"].Voelker[Volk.Elb]);
            Pruefe.Gleich(-0.05, b["Steinmetz"].Voelker[Volk.Elb]);
            Pruefe.Gleich(0.05, b["Wirt"].Voelker[Volk.Halbling]);
            Pruefe.Gleich(-0.05, b["Gardist"].Voelker[Volk.Halbling]);
            Pruefe.Gleich(0.05, b["Tischler"].Voelker[Volk.Mensch]);
            Pruefe.Wahr(b.Values.All(x => x.Voelker.GetValueOrDefault(Volk.Mensch) >= 0), "Menschen haben keine Mali");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace("\"voelker\": { \"Mensch\": 0.05 }", "\"voelker\": { \"Mensch\": 0.6 }")), "Völkerbonus");
        }),

        ("Leistung: Eine Zwergengrube fördert 5 % mehr als eine Menschengrube, eine Halblingsgrube 5 % weniger – Erfahrung im selben Maß", () =>
        {
            var sim = NeueSimulation();
            var (zwerg, _) = Haus(sim, "Zwerg", Volk.Zwerg, Betriebsart.Mine, "Bergmann", 3);
            var (mensch, _) = Haus(sim, "Mensch", Volk.Mensch, Betriebsart.Mine, "Bergmann", 3);
            var (halbling, _) = Haus(sim, "Halbling", Volk.Halbling, Betriebsart.Mine, "Bergmann", 3);
            var w = sim.Wirtschaft!;
            Pruefe.Ungefaehr(1.05, w.VolksFaktor(zwerg, "Schmied"), 1e-12);
            Pruefe.Ungefaehr(0.95, w.VolksFaktor(halbling, "Schmied"), 1e-12);
            Pruefe.Ungefaehr(1, w.VolksFaktor(mensch, "Schmied"), 1e-12);
            for (var i = 0; i < Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag - 1; i++) sim.Tick();
            Pruefe.Ungefaehr(1.05, Stueck(zwerg) / Stueck(mensch), 0.03);
            Pruefe.Ungefaehr(0.95, Stueck(halbling) / Stueck(mensch), 0.03);
            Pruefe.Ungefaehr(1.05, zwerg.Erfahrung("Schmied") / mensch.Erfahrung("Schmied"), 0.03);
        }),

        ("Ohne Ware: Halblingsdiebe arbeiten 5 % mehr Diebesminuten als Menschen, Zwergendiebe 5 % weniger (auch Erfahrung)", () =>
        {
            var sim = NeueSimulation(Unerwischt.Value);
            var (halbling, _) = Haus(sim, "Halbling", Volk.Halbling, Betriebsart.Schmugglerloch, "Geselle", 4);
            var (mensch, _) = Haus(sim, "Mensch", Volk.Mensch, Betriebsart.Schmugglerloch, "Geselle", 4);
            var (zwerg, _) = Haus(sim, "Zwerg", Volk.Zwerg, Betriebsart.Schmugglerloch, "Geselle", 4);
            for (var i = 0; i < 11 * 60 - Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // bis 11 Uhr: drei Arbeitsstunden
            Pruefe.Ungefaehr(1.05, halbling.Erfahrung("Dieb") / mensch.Erfahrung("Dieb"), 1e-9);
            Pruefe.Ungefaehr(0.95, zwerg.Erfahrung("Dieb") / mensch.Erfahrung("Dieb"), 1e-9);
            Pruefe.Ungefaehr(1.05, halbling.Laufend.Umsatz / mensch.Laufend.Umsatz, 1e-9);
        }),

        ("KI und Kalkulation: Der erwartete Gewinn rechnet den Völkerbonus mit; ohne Familie kein Bonus", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var ohne = Kalkulation.ErwarteterJahresgewinn(w, Betriebsart.Giesserei);
            var mit = Kalkulation.ErwarteterJahresgewinn(w, Betriebsart.Giesserei, leistung: 1.05);
            Pruefe.Wahr(mit > ohne, $"Bonus nicht eingerechnet ({mit} / {ohne})");
            var familienlos = w.KontoEroeffnen("Ohne Familie");
            Pruefe.Gleich(1.0, w.VolksFaktor(familienlos, "Schmied"));
            var (zwerg, _) = Haus(sim, "Zwerg", Volk.Zwerg, Betriebsart.Giesserei, "Geselle", 2);
            Pruefe.Gleich(0.0, w.VolksBonus(zwerg, "Tischler"));
            Pruefe.Gleich(0.0, w.VolksBonus(zwerg, "Unbekannt"));
        }),
    };
}
