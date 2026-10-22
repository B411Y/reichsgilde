using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Einbruch in die Kasse eines Geldleihers (PR #177): Ein gelungener Diebstahl (Hinterzimmer) oder Einbruch (eigene Diebe) in eine
/// Wechselstube, Geldleihe oder ein Bankhaus holt 5 % der Kasse, höchstens 500 Gulden.
/// </summary>
static class KasseneinbruchTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());

    /// <summary>Diebstahl und Einbruch gelingen immer (95 % bzw. 100 %), keine Zeugen.</summary>
    static readonly Lazy<Wirtschaftsdaten> Sicher = new(() => Wirtschaftsdaten.AusJson(Json
        .Replace("\"diebstahl\": { \"kosten\": 150, \"chance\": 0.7", "\"diebstahl\": { \"kosten\": 150, \"chance\": 1")
        .Replace("\"zeugenChance\": 0.25", "\"zeugenChance\": 0")
        .Replace("\"zeugenChanceWaechter\": 0.6", "\"zeugenChanceWaechter\": 0")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten ?? Sicher.Value);

    /// <summary>Ein Geldleiher mit einer Wechselstube voller Schreiber und so viel Geld in der Kasse.</summary>
    static (Konto K, Betrieb B) Bank(Stadtwirtschaft w, double kasse)
    {
        var k = w.KontoEroeffnen("Familie Fugger", startvermoegen: 30_000);
        var b = w.BetriebEroeffnen("Wechselstube Fugger", Betriebsart.Wechselstube, k, startkarren: "");
        w.Einstellen(b, "Geselle", b.Plaetze);
        Pruefe.Wahr(w.Ausfuehren(k, new KasseEinlegen(b.Name, kasse)).Erfolg, "Kasse füllen");
        return (k, b);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Kasseneinbruch: Daten – 5 % der Kasse, höchstens 500; ohne Abschnitt keine Beute (alte Spielstände)", () =>
        {
            var d = Wirtschaftsdaten.AusJson(Json).Geldleiher!;
            Pruefe.Ungefaehr(0.05, d.Einbruch.Anteil, 1e-12);
            Pruefe.Ungefaehr(500, d.Einbruch.Hoechstens, 1e-12);
            var ohne = Wirtschaftsdaten.AusJson(Json.Replace("\"einbruch\": { \"anteil\": 0.05, \"hoechstens\": 500 },", ""));
            var w = NeueSimulation(ohne).Wirtschaft!;
            var (_, b) = Bank(w, 4000);
            Pruefe.Gleich(0.0, w.Bank.Kassenbeute(b));
            var w2 = NeueSimulation().Wirtschaft!;
            var (_, b2) = Bank(w2, 4000);
            Pruefe.Ungefaehr(200, w2.Bank.Kassenbeute(b2), 1e-9);
            var (_, b3) = (w2.KontoEroeffnen("Familie Welser", startvermoegen: 30_000), w2.BetriebEroeffnen("Gießerei Welser", Betriebsart.Giesserei, w2.Konten[^1], startkarren: ""));
            Pruefe.Gleich(0.0, w2.Bank.Kassenbeute(b3)); // nur beim Geldleiher
        }),

        ("Kasseneinbruch: Ein Diebstahl aus dem Hinterzimmer holt 5 % der Kasse – der Auftraggeber bekommt das Geld ganz, Meldung wie bei anderen Einbrüchen", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (k, b) = Bank(w, 4000);
            var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 10_000);
            Pruefe.Wahr(w.Gauner.ErwarteterGewinn(b, dieb) > 0, "die Kasse lohnt sich für Diebe");
            Pruefe.Wahr(w.Ausfuehren(dieb, new Beauftragen(k.Inhaber, Gaunerstueck.Diebstahl, b.Name)).Erfolg, "Auftrag");
            var forderungVorher = k.Forderungen; // das Bargeld wächst tagsüber mit Zins und Wechsel – die Kasse nicht
            var diebVorher = dieb.Vermoegen;
            while (sim.Zeit.MinuteImTag != Unterwelt.Nachtstunde) sim.Tick();
            sim.Tick(); // 21:00 – die Bande schlägt zu
            Pruefe.Ungefaehr(3800, w.Bank.Kasse(b), 1e-6);
            Pruefe.Ungefaehr(200, forderungVorher - k.Forderungen, 1e-6);
            Pruefe.Ungefaehr(200, k.Laufend.Raubwert, 1e-6);
            Pruefe.Ungefaehr(200, b.Laufend.Raub, 1e-6);
            Pruefe.Ungefaehr(200, w.Bank.DiesesJahr(k).Gestohlen, 1e-6);
            Pruefe.Ungefaehr(200, dieb.Vermoegen - diebVorher, 1e-6);
            Pruefe.Wahr(w.Gauner.NachrichtenFuer(k, false).Any(t => t.Contains("Einbruch in Wechselstube Fugger: 200 Gulden aus der Kasse gestohlen")),
                string.Join(" | ", w.Gauner.NachrichtenFuer(k, false)));
        }),

        ("Kasseneinbruch: höchstens 500 Gulden – auch beim Einbruch der eigenen Diebe; die Eisentür halbiert die Chance", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (k, b) = Bank(w, 20_000);
            var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 10_000);
            var loch = w.BetriebEroeffnen("Schmugglerloch Elster", Betriebsart.Schmugglerloch, dieb, startkarren: "");
            w.Einstellen(loch, "Geselle", loch.Plaetze);
            Pruefe.Ungefaehr(500, w.Bank.Kassenbeute(b), 1e-9);
            var chance = w.Diebe.Einbruchchance(b, dieb);
            Pruefe.Ungefaehr(500 * chance, w.Diebe.ErwarteterEinbruch(b, dieb), 1e-9);
            var e = w.Ausfuehren(dieb, new Einbrechen(k.Inhaber, b.Name));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            var gaunereiVorher = dieb.Laufend.Gaunerei; // tagsüber bringen die Taschendiebe Geld – die Beute aus der Kasse steht unter Gaunerei
            for (var i = 0; i < 15 * 60 + 1; i++) sim.Tick();
            // Gelungen oder nicht – es ist entweder genau der Deckel oder nichts verschwunden.
            var weg = 20_000 - w.Bank.Kasse(b);
            Pruefe.Wahr(Math.Abs(weg) < 1e-6 || Math.Abs(weg - 500) < 1e-6, $"weg {weg}");
            Pruefe.Ungefaehr(weg, gaunereiVorher - dieb.Laufend.Gaunerei, 1e-6);

            Pruefe.Wahr(w.Ausfuehren(k, new EinrichtungKaufen(b.Name, "Eisentuer")).Erfolg, "Eisentür");
            Pruefe.Ungefaehr(chance * b.EinbruchFaktor, w.Diebe.Einbruchchance(b, dieb), 1e-9);
            var (kopie, _) = Speicherung.Laden(Speicherung.Speichern(sim, k));
            Pruefe.Ungefaehr(weg, kopie.Wirtschaft!.Bank.DiesesJahr(kopie.Wirtschaft.Konten.Single(x => x.Inhaber == k.Inhaber)).Gestohlen, 1e-6);
        }),
    };
}
