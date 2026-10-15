using Reichsgilde.Core;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für den Geldleiher: Kasse, Stadtzinsen, Wechsel, Kredite an Häuser, Pfändung, Wucher, Ausfälle, AP, KI.</summary>
static class GeldleiherTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Jedes Jahr fallen Stadtkunden aus.</summary>
    static readonly Lazy<Wirtschaftsdaten> Pleitejahr = new(() => Wirtschaftsdaten.AusJson(Json.Replace("\"ausfall\": { \"chance\": 0.2", "\"ausfall\": { \"chance\": 1")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Ein Haus mit einer Geldleihe der gewünschten Stufe, voll mit Gesellen (Schreibern).</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb B) Geldleihe(Simulation sim, Betriebsart art = Betriebsart.Wechselstube, string name = "Familie Fugger", double geld = 20_000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: geld);
        var b = w.BetriebEroeffnen($"{w.Daten.Art(art).Name} {name}", art, k, startkarren: "");
        w.Einstellen(b, "Geselle", b.Plaetze);
        return (w, k, b);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: Wechselstube → Geldleihe → Bankhaus (2/3/4 Schreiber), ohne Ware und ohne Umland; ein Standardzins außerhalb der Grenzen fällt auf", () =>
        {
            var d = Daten.Value;
            Pruefe.Wahr(new[] { Betriebsart.Wechselstube, Betriebsart.Geldleihe, Betriebsart.Bankhaus }.Select(a => d.Art(a).Plaetze).SequenceEqual(new[] { 2, 3, 4 }), "Plätze");
            Pruefe.Gleich(Betriebsart.Bankhaus, d.Art(Betriebsart.Geldleihe).Ausbau);
            Pruefe.Wahr(d.Art(Betriebsart.Wechselstube).OhneProduktion && d.Art(Betriebsart.Wechselstube).InDerStadt, "ohne Ware, in der Stadt");
            Pruefe.Gleich<Betriebsart?>(null, d.Berufe["Geldleiher"].Umland);
            var falsch = Json.Replace("\"zinsStandard\": 0.12", "\"zinsStandard\": 0.9");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(falsch), "zinsStandard");
        }),

        ("Kasse: Einlegen macht aus Bargeld eine Forderung – das Reinvermögen bleibt; mehr als das Bargeld geht nicht", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Geldleihe(sim, geld: 5_000);
            Pruefe.Wahr(w.Ausfuehren(k, new KasseEinlegen(b.Name, 4_000)).Erfolg, "einlegen");
            Pruefe.Ungefaehr(1_000, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(4_000, k.Forderungen, 1e-9);
            Pruefe.Ungefaehr(5_000, k.Reinvermoegen, 1e-9);
            Pruefe.Wahr(!w.Ausfuehren(k, new KasseEinlegen(b.Name, 2_000)).Erfolg, "zu wenig Bargeld");
            Pruefe.Wahr(w.Ausfuehren(k, new KasseAbheben(b.Name, 9_999)).Erfolg, "abheben – höchstens die Kasse");
            Pruefe.Ungefaehr(5_000, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(0, w.Bank.Kasse(b), 1e-9);
        }),

        ("Stadtzinsen und Wechsel: 2 Schreiber mit voller Kasse verdienen an einem Arbeitstag 12 % auf 4.000 Gulden plus Wechselgebühr (versteuert)", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Geldleihe(sim);
            w.Ausfuehren(k, new KasseEinlegen(b.Name, 10_000)); // mehr als der Rahmen – der Rest bringt nichts
            Pruefe.Ungefaehr(4_000, w.Bank.RahmenVoll(b), 1e-9);
            var wechsel = 2 * 720 * w.Bank.WechselJeMinute;
            while (sim.Zeit.UhrzeitInMinuten != 21 * 60) sim.Tick();
            var buch = w.Bank.DiesesJahr(k);
            Pruefe.Ungefaehr(480, buch.Stadtzinsen, 1e-6);
            Pruefe.Ungefaehr(wechsel, buch.Wechsel, wechsel * 0.05); // der Markt bewegt sich ein wenig
            Pruefe.Ungefaehr((buch.Stadtzinsen + buch.Wechsel) * 0.05, k.Laufend.Steuer, 1e-6);
            Pruefe.Gleich(0, b.Karren.Count);
        }),

        ("Wechsel: Mehr Schreiber, als die Stadt braucht, teilen sich die Gebühr", () =>
        {
            var sim = NeueSimulation();
            var (w, a, _) = Geldleihe(sim, Betriebsart.Bankhaus, "Familie Fugger");
            var (_, b, _) = Geldleihe(sim, Betriebsart.Bankhaus, "Familie Welser");
            var topf = w.Daten.Geldleiher!.Wechsel.Gebuehr * w.Bank.Marktvolumen;
            while (sim.Zeit.UhrzeitInMinuten != 21 * 60) sim.Tick();
            var summe = w.Bank.DiesesJahr(a).Wechsel + w.Bank.DiesesJahr(b).Wechsel;
            Pruefe.Ungefaehr(topf, summe, topf * 0.05);
            Pruefe.Ungefaehr(w.Bank.DiesesJahr(a).Wechsel, w.Bank.DiesesJahr(b).Wechsel, 1e-6);
        }),

        ("Aktionspunkte: Geldleihe +1, Bankhaus +2 je Runde, die Wechselstube nichts", () =>
        {
            var sim = NeueSimulation();
            var (w, a, _) = Geldleihe(sim, Betriebsart.Wechselstube, "Familie Fugger");
            var (_, b, _) = Geldleihe(sim, Betriebsart.Geldleihe, "Familie Welser");
            var (_, c, _) = Geldleihe(sim, Betriebsart.Bankhaus, "Familie Medici");
            Pruefe.Gleich(0, w.ApBonus(a));
            Pruefe.Gleich(1, w.ApBonus(b));
            Pruefe.Gleich(2, w.ApBonus(c));
            var vorher = c.Aktionspunkte.Vorrat;
            JahrBeenden(sim);
            Pruefe.Gleich(Math.Min(w.Daten.Aktionspunkte!.Hoechstens, vorher + w.AktionspunkteJeRunde + 2), c.Aktionspunkte.Vorrat);
        }),

        ("Kredit: Das Bargeld des Schuldners steigt, sein Reinvermögen nicht; Grenzen nach Kreditwürdigkeit, Stufe und Haus", () =>
        {
            var sim = NeueSimulation();
            var (w, geber, b) = Geldleihe(sim);
            var nehmer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 4_000);
            Pruefe.Ungefaehr(2_000, w.Bank.Hoechstkredit(geber, nehmer), 1e-9); // halbes Reinvermögen
            var e = w.Ausfuehren(nehmer, new KreditAufnehmen(geber.Inhaber, 2_500, 2));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("höchstens"), e.Meldung);
            var ap = nehmer.Aktionspunkte.Vorrat;
            Pruefe.Wahr(w.Ausfuehren(nehmer, new KreditAufnehmen(geber.Inhaber, 2_000, 2)).Erfolg, "leihen");
            Pruefe.Gleich(ap - 1, nehmer.Aktionspunkte.Vorrat);
            Pruefe.Ungefaehr(6_000, nehmer.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(4_000, nehmer.Reinvermoegen, 1e-9);
            Pruefe.Ungefaehr(2_000, geber.Forderungen, 1e-9);
            Pruefe.Ungefaehr(20_000, geber.Reinvermoegen, 1e-9);
            Pruefe.Wahr(!w.Ausfuehren(nehmer, new KreditAufnehmen(geber.Inhaber, 100, 1)).Erfolg, "nur ein Kredit");
            Pruefe.Wahr(!w.Ausfuehren(geber, new KreditAufnehmen(geber.Inhaber, 100, 1)).Erfolg, "nicht bei sich selbst");
            w.Einstellen(b, "Geselle", 0);
            var dritter = w.KontoEroeffnen("Haus Eichwald", startvermoegen: 10_000);
            e = w.Ausfuehren(dritter, new KreditAufnehmen(geber.Inhaber, 100, 1));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Schreibern"), e.Meldung);
        }),

        ("Kredit: Zinsen laufen stündlich auf, am Jahresende sind Zins und Rate fällig (Posten Zinsen, Hauszinsen beim Geldleiher)", () =>
        {
            var sim = NeueSimulation();
            var (w, geber, _) = Geldleihe(sim);
            var nehmer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 10_000);
            w.Ausfuehren(nehmer, new KreditAufnehmen(geber.Inhaber, 3_000, 3)); // zur Zunftglocke (05:00), 12 %
            for (var i = 0; i < 13 * 60 + 1; i++) sim.Tick(); // bis einschließlich der Stunde um 18:00 (13 Stunden)
            Pruefe.Ungefaehr(3_000 * 0.12 * 13 / 17, w.Bank.KreditVon(nehmer)!.Aufgelaufen, 1e-6); // ein Jahr hat 17 Stunden
            JahrBeenden(sim);
            var zins = 3_000 * 0.12; // ein ganzes Jahr
            Pruefe.Ungefaehr(zins, nehmer.Vorjahr!.Zinsen, 1e-6);
            Pruefe.Ungefaehr(zins, w.Bank.Vorjahr(geber)!.Hauszinsen, 1e-6);
            var kredit = w.Bank.KreditVon(nehmer)!;
            Pruefe.Ungefaehr(2_000, kredit.Rest, 1e-6);
            Pruefe.Gleich(2, kredit.JahreOffen);
            Pruefe.Ungefaehr(2_000, nehmer.Schulden, 1e-6);
            Pruefe.Ungefaehr(2_000, geber.Forderungen, 1e-6);
        }),

        ("Ablösen: Rest und aufgelaufene Zinsen auf einmal – danach ist der Kredit weg", () =>
        {
            var sim = NeueSimulation();
            var (w, geber, _) = Geldleihe(sim);
            var nehmer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 10_000);
            w.Ausfuehren(nehmer, new KreditAufnehmen(geber.Inhaber, 2_400, 2));
            for (var i = 0; i < 121; i++) sim.Tick(); // zwei volle Stunden (06:00 und 07:00)
            var kredit = w.Bank.KreditVon(nehmer)!;
            var zinsen = 2_400 * 0.12 * 2 / 17;
            Pruefe.Ungefaehr(zinsen, kredit.Aufgelaufen, 1e-9);
            Pruefe.Wahr(w.Ausfuehren(nehmer, new KreditTilgen()).Erfolg, "ablösen");
            Pruefe.Gleich<object?>(null, w.Bank.KreditVon(nehmer));
            Pruefe.Ungefaehr(10_000 - zinsen, nehmer.Vermoegen, 1e-6);
            Pruefe.Ungefaehr(0, nehmer.Schulden, 1e-9);
            Pruefe.Ungefaehr(0, geber.Forderungen, 1e-9);
        }),

        ("Pfändung: Fehlt das Bargeld für die Rate, holt der Büttel Lagerware zu 75 % des Marktpreises", () =>
        {
            var sim = NeueSimulation();
            var (w, geber, _) = Geldleihe(sim);
            var nehmer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 4_000);
            var giesserei = w.BetriebEroeffnen("Gießerei Eisenbart", Betriebsart.Giesserei, nehmer, startkarren: "");
            w.Ausfuehren(nehmer, new KreditAufnehmen(geber.Inhaber, 2_000, 1));
            giesserei.Einlagern("Silber", 20);
            // Das Bargeld ist weg (z. B. ausgegeben) – die ganze Rate ist am Jahresende fällig.
            nehmer.Familienkosten(nehmer.Vermoegen);
            JahrBeenden(sim);
            Pruefe.Wahr(giesserei.ImLager("Silber") < 20, "Silber gepfändet");
            Pruefe.Gleich<object?>(null, w.Bank.KreditVon(nehmer));
            Pruefe.Ungefaehr(0, geber.Forderungen, 1e-6);
        }),

        ("Wucher: Ein Zins über 20 % ist eine Spur, der Schuldner hat den Beweis; das Haus verliert Ansehen, solange der Kredit läuft", () =>
        {
            var sim = NeueSimulation();
            var (w, geber, b) = Geldleihe(sim);
            var ansehen = w.Rat.Ansehen(geber);
            var z = w.Ausfuehren(geber, new ZinsSetzen(b.Name, 0.3));
            Pruefe.Wahr(z.Erfolg && z.Meldung.Contains("Wucher"), z.Meldung);
            Pruefe.Wahr(!w.Ausfuehren(geber, new ZinsSetzen(b.Name, 0.8)).Erfolg, "höchstens 50 %");
            var nehmer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 10_000);
            w.Ausfuehren(nehmer, new KreditAufnehmen(geber.Inhaber, 1_000, 2));
            var spur = w.Justiz.SpurenVon(geber).Single();
            Pruefe.Gleich(Vergehen.Wucher, spur.Art);
            Pruefe.Ungefaehr(0.1 * 1_000 * 2, spur.Betrag, 1e-9);
            Pruefe.Wahr(w.Justiz.KenntSpur(nehmer, spur), "Schuldschein als Beweis");
            Pruefe.Ungefaehr(ansehen - 5, w.Rat.Ansehen(geber), 1e-9);
            w.Ausfuehren(nehmer, new KreditTilgen());
            Pruefe.Ungefaehr(ansehen, w.Rat.Ansehen(geber), 1e-9);
        }),

        ("Ausfall: Stadtkunden auf und davon – 10 % der Kasse sind verloren (Schaden), das Bargeld bleibt", () =>
        {
            var sim = NeueSimulation(Pleitejahr.Value);
            var (w, k, b) = Geldleihe(sim);
            w.Ausfuehren(k, new KasseEinlegen(b.Name, 3_000));
            JahrBeenden(sim);
            Pruefe.Ungefaehr(2_700, w.Bank.Kasse(b), 1e-6);
            Pruefe.Ungefaehr(300, w.Bank.Vorjahr(k)!.Ausfall, 1e-6);
            Pruefe.Wahr(k.Vorjahr!.Schaden >= 300 - 1e-6, "als Schaden gebucht");
        }),

        ("Schuldner fällt aus: Ist das Haus bankrott, verliert der Geldleiher den Rest", () =>
        {
            var sim = NeueSimulation();
            var (w, geber, _) = Geldleihe(sim);
            var nehmer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 4_000);
            w.Ausfuehren(nehmer, new KreditAufnehmen(geber.Inhaber, 2_000, 3));
            nehmer.IstBankrott = true;
            JahrBeenden(sim);
            Pruefe.Gleich<object?>(null, w.Bank.KreditVon(nehmer));
            Pruefe.Ungefaehr(0, geber.Forderungen, 1e-9);
            Pruefe.Ungefaehr(2_000, w.Bank.Vorjahr(geber)!.Ausfall, 1e-9);
        }),

        ("Reinvermögen: Mit geliehenem Geld steigt man nicht in einen höheren Stand auf", () =>
        {
            var sim = NeueSimulation();
            var (w, geber, b) = Geldleihe(sim, Betriebsart.Bankhaus, geld: 40_000);
            var nehmer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 9_000);
            w.Ausfuehren(nehmer, new KreditAufnehmen(geber.Inhaber, 4_000, 2));
            var e = w.Ausfuehren(nehmer, new StandErwerben());
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("fehlen noch 1.000"), e.Meldung);
        }),

        ("KI: Eine KI-Geldleihe füllt ihre Kasse bis zum Rahmen und setzt ihren Zins nach Charakter", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Geldleihe(sim, name: "Haus Fugger");
            w.KiAnmelden(k, KiProfil.Standard);
            for (var i = 0; i < 7 * 60; i++) sim.Tick(); // über die Politikstunde
            Pruefe.Ungefaehr(w.Bank.RahmenVoll(b), w.Bank.Kasse(b), 1e-9);
            Pruefe.Ungefaehr(0.14, w.Bank.Zinssatz(k), 1e-9);
        }),

        ("Speichern: Kasse, Kredit, Zinssatz und Bankbuch überstehen Speichern und Laden; Startberuf Geldleiher", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Beruf: "Geldleiher", Startvermoegen: 10_000));
            var w = sim.Wirtschaft!;
            var stube = w.Betriebe.Single(b => b.Konto == spieler);
            Pruefe.Gleich("Wechselstube am Weinmarkt", stube.Name);
            w.Ausfuehren(spieler, new KasseEinlegen(stube.Name, 4_000));
            w.Ausfuehren(spieler, new ZinsSetzen(stube.Name, 0.18));
            var eisenbart = w.Konten.Single(k => k.Inhaber == "Haus Eisenbart");
            Pruefe.Wahr(w.Ausfuehren(eisenbart, new KreditAufnehmen(spieler.Inhaber, 1_000, 2)).Erfolg, "Eisenbart leiht");
            for (var i = 0; i < 5 * 60 + 30; i++) sim.Tick(); // 11:30
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var kw = kopie.Wirtschaft!;
            Pruefe.Gleich(w.Bank.DiesesJahr(spieler), kw.Bank.DiesesJahr(kopieSpieler!));
            Pruefe.Ungefaehr(0.18, kw.Bank.Zinssatz(kopieSpieler!), 1e-12);
            for (var i = 0; i < Spielzeit.MinutenProTag; i++) { sim.Tick(); kopie.Tick(); }
            Pruefe.Gleich(w.Bank.Vorjahr(spieler), kw.Bank.Vorjahr(kopieSpieler!));
            Pruefe.Ungefaehr(w.Bank.Kasse(stube), kw.Bank.Kasse(kw.Betriebe.Single(b => b.Name == stube.Name)), 1e-9);
            Pruefe.Ungefaehr(w.Bank.Kredite.Single().Rest, kw.Bank.Kredite.Single().Rest, 1e-9);
            Pruefe.Ungefaehr(spieler.Forderungen, kopieSpieler!.Forderungen, 1e-9);
        }),
    };
}
