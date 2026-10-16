using Reichsgilde.Core;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für den KI-Rivalen: Rechnungen, Entscheidungen und Langzeitverhalten.</summary>
static class KiTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation(Fronstatut statut = Fronstatut.Bestienfron, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { StartStatut = statut, Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos },
            wirtschaftsdaten: Daten.Value);

    static void Ticken(Simulation sim, int minuten)
    {
        for (var i = 0; i < minuten; i++)
            sim.Tick();
    }

    /// <summary>Die Startaufstellung aus dem Spiel: Spieler mit Gießerei, Rivale mit Gießerei und Mine.</summary>
    static (Simulation Sim, Konto Spieler, Konto Rivale) Startaufstellung(bool mitKi, int startwert = 1400)
    {
        var sim = NeueSimulation(startwert: startwert);
        var w = sim.Wirtschaft!;
        var spieler = w.KontoEroeffnen("Spieler");
        var g = w.BetriebEroeffnen("Gießerei Spieler", Betriebsart.Giesserei, spieler);
        g.Produkt = "Silberring";
        w.Einstellen(g, "Geselle", 2);
        var rivale = w.KontoEroeffnen("Rivale");
        var rg = w.BetriebEroeffnen("Gießerei Rivale", Betriebsart.Giesserei, rivale);
        rg.Produkt = "Rapier";
        w.Einstellen(rg, "Geselle", 2);
        var mine = w.BetriebEroeffnen("Grube Rivale", Betriebsart.Mine, rivale);
        w.Einstellen(mine, "Ork", 3);
        mine.Karren[0].Eskorte = "Normal";
        if (mitKi) w.KiAnmelden(rivale);
        return (sim, spieler, rivale);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Rechnung: Gewinn je Arbeitsminute = (Preis nach Steuer − Zutaten) ÷ Zeit", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var ki = w.KiAnmelden(w.KontoEroeffnen("Rivale"));
            var m = w.Markt;
            var steuer = w.Daten.Allgemein.Umsatzsteuer;
            var d = w.Daten.Waren;
            Pruefe.Ungefaehr((m["Silberring"].Preis * (1 - steuer) - m["Eisen"].Preis - m["Silber"].Preis) / d["Silberring"].Zeit,
                             ki.GewinnJeArbeitsminute("Silberring"), 1e-9);
            Pruefe.Ungefaehr(m["Eisen"].Preis * (1 - steuer) / d["Eisen"].Zeit, ki.GewinnJeArbeitsminute("Eisen"), 1e-9);
        }),

        ("KI stellt um, wenn der Markt für ihr Produkt überschwemmt ist", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var konto = w.KontoEroeffnen("Rivale");
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, konto);
            g.Produkt = "Silberring";
            w.Markt["Silberring"].Verkaufen(2000); // Preis fällt auf das Minimum
            var ki = w.KiAnmelden(konto);
            Pruefe.Wahr(g.Produkt != "Silberring", $"Produkt ist noch {g.Produkt}");
            Pruefe.Wahr(ki.Entscheidungen.Any(e => e.Contains("stellt von Silberring auf")), "Umstellung nicht gemeldet");
        }),

        ("KI bleibt beim Produkt, wenn kein anderes deutlich besser ist", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var konto = w.KontoEroeffnen("Rivale");
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, konto);
            var ki = w.KiAnmelden(konto, KiProfil.Standard with { Wechselschwelle = 10 });
            Pruefe.Gleich("Silberring", g.Produkt);
            Pruefe.Gleich(0, ki.Entscheidungen.Count(e => e.Contains("stellt von")));
        }),

        ("KI besetzt Plätze neu, wenn ein neues Statut ihre Orks verbietet", () =>
        {
            var sim = NeueSimulation(Fronstatut.Bestienfron);
            var w = sim.Wirtschaft!;
            var konto = w.KontoEroeffnen("Rivale");
            var mine = w.BetriebEroeffnen("M", Betriebsart.Mine, konto);
            w.Einstellen(mine, "Ork", 3);
            w.KiAnmelden(konto);

            w.StatutAendern(Fronstatut.FreieStadt); // Orks dürfen nicht mehr ins Umland
            Pruefe.Gleich(0, mine.Belegt);
            Ticken(sim, 61); // Minute 0–59, dann die volle Stunde selbst
            Pruefe.Gleich(3, mine.Belegung.GetValueOrDefault("Bergmann"));
        }),

        ("KI wählt die Arbeitskraft mit dem höchsten Jahresertrag (Mine unter Statut B: Ork)", () =>
        {
            var w = NeueSimulation(Fronstatut.Bestienfron).Wirtschaft!;
            var konto = w.KontoEroeffnen("Rivale");
            var mine = w.BetriebEroeffnen("M", Betriebsart.Mine, konto);
            var ki = w.KiAnmelden(konto);
            Pruefe.Gleich(3, mine.Belegung.GetValueOrDefault("Ork"));
            Pruefe.Wahr(ki.Jahresertrag(mine, "Ork") > ki.Jahresertrag(mine, "Bergmann"), "Ork sollte mehr einbringen");
        }),

        ("KI stellt nicht mehr ein, als ihre Kasse über die Reserve hinaus trägt", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var konto = w.KontoEroeffnen("Rivale");
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, konto);
            // Reserve so hoch, dass nach Abzug der halben Reserve nur noch ein Gesellenlohn (220) übrig ist.
            w.KiAnmelden(konto, KiProfil.Standard with { Reserve = 2 * (konto.Vermoegen - 300) });
            Pruefe.Gleich(1, g.Belegt);
        }),

        ("KI fährt in der Stadt ohne Eskorte, im Umland mit der günstigsten", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var konto = w.KontoEroeffnen("Rivale");
            var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, konto);
            var mine = w.BetriebEroeffnen("M", Betriebsart.Mine, konto);
            g.Karren[0].Eskorte = "Schwer";
            var ki = w.KiAnmelden(konto);
            Pruefe.Gleich("Keine", g.Karren[0].Eskorte);

            var wert = ki.Ladungswert(mine.Karren[0]);
            var guenstigste = w.Daten.Logistik.Eskorten.Keys.Where(w.Wache.EskorteVerfuegbar).OrderBy(e => ki.ErwarteteFahrtkosten(e, wert)).First(); // die Stadtwache nur mit Gardist
            Pruefe.Gleich(guenstigste, mine.Karren[0].Eskorte);
            Pruefe.Gleich("Normal", guenstigste); // passt zur Langzeitmessung aus Phase 2 (Normal lohnt sich am meisten)
        }),

        ("Eine vorsichtige KI nimmt die schwere Eskorte", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var konto = w.KontoEroeffnen("Rivale");
            var mine = w.BetriebEroeffnen("M", Betriebsart.Mine, konto);
            w.KiAnmelden(konto, KiProfil.Standard with { Vorsicht = 3 });
            Pruefe.Gleich("Schwer", mine.Karren[0].Eskorte);
        }),

        ("KI kauft einen Ochsenkarren, wenn die Mine mit dem Handkarren nicht nachkommt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var konto = w.KontoEroeffnen("Rivale");
            var mine = w.BetriebEroeffnen("M", Betriebsart.Mine, konto, startkarren: "Handkarren");
            w.KiAnmelden(konto);
            Ticken(sim, Spielzeit.MinutenProTag);
            Pruefe.Wahr(mine.StillstandMinutenVorjahr > 0.2 * w.Daten.Allgemein.Arbeitsminuten,
                $"nur {mine.StillstandMinutenVorjahr} Minuten Stillstand");
            Pruefe.Gleich(2, mine.Karren.Count);
            Pruefe.Gleich("Ochsenkarren", mine.Karren[1].Typ);
            Pruefe.Ungefaehr(400, konto.Laufend.Investitionen, 1e-9); // Kauf nach der Abrechnung → zählt ins neue Jahr
        }),

        ("KI kauft keine Karren, wenn der Betrieb nicht stillsteht", () =>
        {
            var (sim, _, rivale) = Startaufstellung(mitKi: true);
            Ticken(sim, 3 * Spielzeit.MinutenProTag);
            foreach (var b in sim.Wirtschaft!.Betriebe.Where(b => b.Konto == rivale))
                Pruefe.Gleich(1, b.Karren.Count);
        }),

        ("Ein Konto kann nur einmal an die KI übergeben werden", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var konto = w.KontoEroeffnen("Rivale");
            w.KiAnmelden(konto);
            Pruefe.WirftMitMeldung(() => w.KiAnmelden(konto), "bereits von der KI geführt");
        }),

        ("KI ist deterministisch: gleicher Startwert = gleiches Vermögen und gleiche Entscheidungen", () =>
        {
            var (simA, _, rivaleA) = Startaufstellung(mitKi: true, startwert: 77);
            var (simB, _, rivaleB) = Startaufstellung(mitKi: true, startwert: 77);
            Ticken(simA, 3 * Spielzeit.MinutenProTag);
            Ticken(simB, 3 * Spielzeit.MinutenProTag);
            Pruefe.Gleich(rivaleA.Vermoegen, rivaleB.Vermoegen);
            Pruefe.Gleich(string.Join("|", simA.Wirtschaft!.Rivalen[0].Entscheidungen),
                          string.Join("|", simB.Wirtschaft!.Rivalen[0].Entscheidungen));
        }),

        ("Langzeit (20 Jahre): Mit KI verdient der Rivale mindestens 5 % mehr und entscheidet selten", () =>
        {
            var (ohne, _, rivaleOhne) = Startaufstellung(mitKi: false);
            var (mit, _, rivaleMit) = Startaufstellung(mitKi: true);
            var entscheidungen = 0;
            double gebuehren = 0;
            var wechsel = 0;
            mit.Wirtschaft!.Meldung += m =>
            {
                if (!m.StartsWith("Rivale:")) return;
                entscheidungen++;
                if (m.Contains(" stellt von ")) wechsel++;
            };
            mit.Wirtschaft!.Aufgestiegen += (_, stand) => gebuehren += stand.Gebuehr;
            Ticken(ohne, 20 * Spielzeit.MinutenProTag);
            Ticken(mit, 20 * Spielzeit.MinutenProTag);
            // Standesgebühren sind kein Verlust, sondern ein Kauf: Sie werden zum Vergleich wieder hinzugerechnet.
            Pruefe.Wahr(rivaleMit.Vermoegen + gebuehren > 1.05 * rivaleOhne.Vermoegen,
                $"mit KI {rivaleMit.Vermoegen:0} + {gebuehren:0} Gebühren, ohne KI {rivaleOhne.Vermoegen:0}");
            Pruefe.Wahr(rivaleMit.Stand >= 2, "Die KI sollte nach 20 Jahren mindestens Patrizier sein");
            // Kein hektisches Hin und Her: im Schnitt höchstens 2 Produktwechsel und 4 Entscheidungen pro Jahr
            // (mit Meisterprüfungen, Ausbau, Ständen und Rat kommen über 20 Jahre einige dazu; seit PR #60 holt die KI
            // die Werkstatt zum gelernten Handwerk nach – ein Betrieb mehr mit Kauf, Personal, Karren und Produkt: 90 statt 80).
            Pruefe.Wahr(wechsel <= 40, $"{wechsel} Produktwechsel in 20 Jahren");
            Pruefe.Wahr(entscheidungen <= 90, $"{entscheidungen} Entscheidungen in 20 Jahren");
        }),
    };
}
