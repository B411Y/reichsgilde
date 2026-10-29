using Reichsgilde.Core.Zeit;
using Reichsgilde.Core;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests für die Gauner-Regeln (Datenversion 64): Die Streife der Wache fasst am Markt öfter Taschendiebe –
/// Fangchance × (1 + wache.patrouille.taschendiebFang × Schutz der Streife), auch die Wache des eigenen Hauses.
/// </summary>
static class WacheTaschendiebTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Ein Dieb allein wird in seiner Stunde zu zwei Dritteln erwischt – mit der Streife eines Wachhauses (× 1,5) sicher.</summary>
    static readonly Lazy<Wirtschaftsdaten> Knapp = new(() => Wirtschaftsdaten.AusJson(Json.Replace("\"fangJeStunde\": 0.012", "\"fangJeStunde\": 0.667")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    /// <summary>Ein Wachhaus voll mit Gesellen (vier Leute = Schutz 0,1).</summary>
    static Betrieb Wachhaus(Stadtwirtschaft w, Konto k, string name)
    {
        var b = w.BetriebEroeffnen($"Wachhaus {name}", Betriebsart.Wachhaus, k);
        w.Einstellen(b, "Geselle", b.Plaetze);
        return b;
    }

    /// <summary>Bis 09:01 – die erste Arbeitsstunde (ab 8 Uhr) ist abgerechnet.</summary>
    static void ErsteArbeitsstunde(Simulation sim)
    {
        for (var i = 0; i < 9 * 60 + 1 - Spielzeit.TagesbeginnMinute; i++) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: wache.patrouille.taschendiebFang 5, nicht negativ; ohne Angabe 0 – dann fängt die Streife keine Taschendiebe", () =>
        {
            Pruefe.Ungefaehr(5, Daten.Value.Wache!.Patrouille.TaschendiebFang, 1e-12);
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace("\"taschendiebFang\": 5", "\"taschendiebFang\": -1")), "taschendiebFang");
            var alt = Wirtschaftsdaten.AusJson(Json.Replace(", \"taschendiebFang\": 5", ""));
            Pruefe.Ungefaehr(0, alt.Wache!.Patrouille.TaschendiebFang, 1e-12);
            var sim = NeueSimulation(alt);
            var w = sim.Wirtschaft!;
            Wachhaus(w, w.KontoEroeffnen("Familie Wacker", startvermoegen: 10_000), "Wacker");
            Pruefe.Ungefaehr(0.1, w.Wache.Patrouilleschutz, 1e-9);
            Pruefe.Ungefaehr(1, w.Wache.Taschendiebfaktor, 1e-12);
        }),

        ("Fangchance: ohne Wache × 1, ein Wachhaus mit vier Gesellen × 1,5, zwei Wachhäuser × 2 – nach der Streife, nicht nach der Zahl der Häuser", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 10_000);
            var loch = w.BetriebEroeffnen("Schmugglerloch Elster", Betriebsart.Schmugglerloch, dieb, startkarren: "");
            w.Einstellen(loch, "Geselle", 4);
            var ohne = w.Diebe.Fangchance(loch);
            Pruefe.Ungefaehr(1, w.Wache.Taschendiebfaktor, 1e-12);
            Pruefe.Ungefaehr(0.012 * w.Familien.ZeugenFaktor(dieb), ohne, 1e-12);

            var wacker = w.KontoEroeffnen("Familie Wacker", startvermoegen: 10_000);
            var stube = Wachhaus(w, wacker, "Wacker");
            Pruefe.Ungefaehr(1.5, w.Wache.Taschendiebfaktor, 1e-9);
            Pruefe.Ungefaehr(ohne * 1.5, w.Diebe.Fangchance(loch), 1e-12);
            Pruefe.Ungefaehr(ohne * 1.5, w.Diebe.Fangchance(dieb, 1), 1e-12);

            // Halb besetzt: halbe Streife, halber Zuschlag.
            w.Einstellen(stube, "Geselle", 2);
            Pruefe.Ungefaehr(1.25, w.Wache.Taschendiebfaktor, 1e-9);
            w.Einstellen(stube, "Geselle", 4);

            Wachhaus(w, w.KontoEroeffnen("Familie Hellebard", startvermoegen: 10_000), "Hellebard");
            Pruefe.Ungefaehr(2, w.Wache.Taschendiebfaktor, 1e-9);
            Pruefe.Ungefaehr(ohne * 2, w.Diebe.Fangchance(loch), 1e-12);
        }),

        ("Fangchance: Mehr als der Deckel der Streife (40 % Schutz) gibt es nicht – höchstens × 3", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            for (var i = 0; i < 6; i++) Wachhaus(w, w.KontoEroeffnen($"Familie Wache{i}", startvermoegen: 10_000), $"Wache{i}");
            Pruefe.Ungefaehr(0.4, w.Wache.Patrouilleschutz, 1e-12);
            Pruefe.Ungefaehr(3, w.Wache.Taschendiebfaktor, 1e-12);
        }),

        ("Fang: Mit einem Wachhaus in der Stadt wird ein Dieb, der sonst zu zwei Dritteln erwischt würde, sicher gefasst – die Wache hat Beweis und Kopfgeld", () =>
        {
            var sim = NeueSimulation(Knapp.Value);
            var w = sim.Wirtschaft!;
            var wacker = w.KontoEroeffnen("Familie Wacker", startvermoegen: 10_000);
            Wachhaus(w, wacker, "Wacker");
            var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 10_000);
            var loch = w.BetriebEroeffnen("Schmugglerloch Elster", Betriebsart.Schmugglerloch, dieb, startkarren: "");
            w.Einstellen(loch, "Geselle", 1);
            Pruefe.Wahr(w.Diebe.Fangchance(loch) >= 1, $"Fangchance {w.Diebe.Fangchance(loch)}");
            ErsteArbeitsstunde(sim);
            Pruefe.Gleich(1, w.Diebe.DiesesJahr(dieb).Faenge);
            Pruefe.Gleich(0, loch.Belegt);
            var spur = w.Justiz.SpurenVon(dieb).Single();
            Pruefe.Gleich(Vergehen.Taschendiebstahl, spur.Art);
            Pruefe.Wahr(w.Justiz.KenntSpur(wacker, spur), "Die Wache hat den Beweis");
            Pruefe.Ungefaehr(50, w.Wache.DiesesJahr(wacker).Kopfgeld, 1e-9);
        }),

        ("Fang: Die Wache des eigenen Hauses schützt die eigenen Taschendiebe nicht – sie zählt zur Streife, das Kopfgeld und den Beweis bekommt sie nicht", () =>
        {
            var sim = NeueSimulation(Knapp.Value);
            var w = sim.Wirtschaft!;
            var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 10_000);
            var loch = w.BetriebEroeffnen("Schmugglerloch Elster", Betriebsart.Schmugglerloch, dieb, startkarren: "");
            w.Einstellen(loch, "Geselle", 1);
            var ohne = w.Diebe.Fangchance(loch);
            Wachhaus(w, dieb, "Elster");
            var anderes = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 10_000);
            Pruefe.Ungefaehr(ohne * 1.5, w.Diebe.Fangchance(loch), 1e-12);
            ErsteArbeitsstunde(sim);
            Pruefe.Gleich(1, w.Diebe.DiesesJahr(dieb).Faenge);
            var spur = w.Justiz.SpurenVon(dieb).Single();
            Pruefe.Wahr(w.Justiz.KenntSpur(anderes, spur), "Ein Bürger erzählt es dem anderen Haus");
            Pruefe.Ungefaehr(0, w.Wache.DiesesJahr(dieb).Kopfgeld, 1e-12);
        }),

        ("Die Streife ändert keine Würfe: Ohne Diebe verläuft ein Jahr mit Wachhaus mit und ohne Zuschlag zeichengleich", () =>
        {
            var alt = Wirtschaftsdaten.AusJson(Json.Replace(", \"taschendiebFang\": 5", ""));
            string Lauf(Wirtschaftsdaten d)
            {
                var sim = NeueSimulation(d);
                var w = sim.Wirtschaft!;
                Wachhaus(w, w.KontoEroeffnen("Familie Wacker", startvermoegen: 10_000), "Wacker");
                var k = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 10_000);
                w.Einstellen(w.BetriebEroeffnen("Gießerei Eisenbart", Betriebsart.Giesserei, k), "Geselle", 2);
                for (var i = 0; i < Spielzeit.MinutenProTag; i++) sim.Tick();
                return string.Join("|", w.Konten.Select(x => $"{x.Inhaber}:{x.Vermoegen:R}"));
            }
            Pruefe.Gleich(Lauf(alt), Lauf(Daten.Value));
        }),
    };
}
