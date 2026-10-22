using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für den Gardisten: Sold, Patrouille, Wachturm, Razzia, Kopfgeld, Geleitschutz, Ansehen, KI.</summary>
static class GardistTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Jeder Taschendieb wird in der ersten Stunde erwischt.</summary>
    static readonly Lazy<Wirtschaftsdaten> Tolpatschig = new(() => Wirtschaftsdaten.AusJson(Json.Replace("\"fangJeStunde\": 0.012", "\"fangJeStunde\": 1")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Ein Haus mit einer Wachstube der gewünschten Stufe, voll mit Gesellen.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb B) Wache(Simulation sim, Betriebsart art = Betriebsart.Wachhaus, string name = "Familie Wacker", int? leute = null)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: 10_000);
        var b = w.BetriebEroeffnen($"{w.Daten.Art(art).Name} {name}", art, k);
        w.Einstellen(b, "Geselle", leute ?? b.Plaetze);
        return (w, k, b);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: Wachhaus → Stadtwache → Garnison (4/6/8), Wachturm im Umland, beide ohne Ware; die Eskorte „Stadtwache“ stellt nur die Wache", () =>
        {
            var d = Daten.Value;
            Pruefe.Wahr(new[] { Betriebsart.Wachhaus, Betriebsart.Stadtwache, Betriebsart.Garnison }.Select(a => d.Art(a).Plaetze).SequenceEqual(new[] { 4, 6, 8 }), "Plätze");
            Pruefe.Gleich(Betriebsart.Garnison, d.Art(Betriebsart.Stadtwache).Ausbau);
            Pruefe.Wahr(d.Art(Betriebsart.Wachturm).OhneProduktion && !d.Art(Betriebsart.Wachturm).InDerStadt, "Wachturm im Umland ohne Ware");
            Pruefe.Gleich<Betriebsart?>(Betriebsart.Wachturm, d.Berufe["Gardist"].Umland);
            Pruefe.Wahr(d.Logistik.Eskorten["Stadtwache"].Wache, "Eskorte der Wache");
            var falsch = Json.Replace("\"geleit\": { \"eskorte\": \"Stadtwache\"", "\"geleit\": { \"eskorte\": \"Normal\"");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(falsch), "\"wache\": true");
        }),

        ("Sold: 4 Gesellen bekommen an einem Arbeitstag 4 × 720 × 0,68 Gulden (versteuert), halbe Erfahrung (vier Plätze) – das Haus ist beliebt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Wacker", startvermoegen: 10_000);
            var ansehen = w.Rat.Ansehen(k);
            var b = w.BetriebEroeffnen("Wachhaus Wacker", Betriebsart.Wachhaus, k);
            w.Einstellen(b, "Geselle", 4);
            Pruefe.Ungefaehr(ansehen + 3, w.Rat.Ansehen(k), 1e-9);
            Pruefe.Gleich(0, b.Karren.Count);
            for (var i = 0; i < 16 * 60; i++) sim.Tick(); // bis 21 Uhr – die Arbeitszeit (8–20 Uhr) ist abgerechnet
            // PR #177: Sold 0,75 → 0,68 je Minute; Erfahrung × 0,5 (erfahrungFaktor – doppelt so viele Plätze wie eine Werkstatt)
            Pruefe.Ungefaehr(4 * 720 * 0.68, w.Wache.DiesesJahr(k).Sold, 1e-6);
            Pruefe.Ungefaehr(4 * 720 * 0.68 * 0.05, k.Laufend.Steuer, 1e-6);
            Pruefe.Ungefaehr(24, k.Erfahrung("Gardist"), 1e-6);
            Pruefe.Ungefaehr(0.68 * 0.95 * 720 - 220, Kalkulation.Jahresertrag(w, b, "Geselle"), 1e-6);
        }),

        ("Patrouille: 4 Wachleute senken nächtliche Taten in der Stadt um 10 % und sehen öfter zu – im Umland nicht", () =>
        {
            var sim = NeueSimulation();
            var (w, _, _) = Wache(sim);
            var opfer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
            var giesserei = w.BetriebEroeffnen("Gießerei Eisenbart", Betriebsart.Giesserei, opfer, startkarren: "");
            var grube = w.BetriebEroeffnen("Grube Eisenbart", Betriebsart.Mine, opfer, startkarren: "");
            Pruefe.Ungefaehr(0.1, w.Wache.Patrouilleschutz, 1e-9);
            Pruefe.Ungefaehr(0.7 * 0.9, w.Gauner.Erfolgschance(Gaunerstueck.Diebstahl, giesserei), 1e-9);
            Pruefe.Ungefaehr(0.7, w.Gauner.Erfolgschance(Gaunerstueck.Diebstahl, grube), 1e-9);
            Pruefe.Ungefaehr(0.25 + 0.05, w.Gauner.Zeugenchance(giesserei, true), 1e-9);
        }),

        ("Wachturm: 3 Leute senken die Überfallchance auf allen Umlandwegen um 30 %", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            Pruefe.Ungefaehr(1, w.Wache.Landstrassenfaktor, 1e-12);
            var k = w.KontoEroeffnen("Familie Wacker", startvermoegen: 10_000);
            var turm = w.BetriebEroeffnen("Wachturm Wacker", Betriebsart.Wachturm, k);
            w.Einstellen(turm, "Bergmann", 3);
            Pruefe.Ungefaehr(0.7, w.Wache.Landstrassenfaktor, 1e-12);
            Pruefe.Wahr(Kalkulation.ErwarteterJahresgewinn(w, Betriebsart.Wachturm) > 0, "Wachturm rechnet sich");
        }),

        ("Razzia: 2 AP, eine im Jahr im Wachhaus – Diebesgut wird beschlagnahmt", () =>
        {
            var sim = NeueSimulation();
            var (w, k, _) = Wache(sim);
            var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 5_000);
            var loch = w.BetriebEroeffnen("Schmugglerloch Elster", Betriebsart.Schmugglerloch, dieb, startkarren: "");
            loch.Einlagern("Silber", 12);
            var ap = k.Aktionspunkte.Vorrat;
            var e = w.Ausfuehren(k, new Razzia(dieb.Inhaber));
            Pruefe.Wahr(e.Erfolg && e.Meldung.Contains("beschlagnahmt"), e.Meldung);
            Pruefe.Gleich(0, loch.ImLager("Silber"));
            Pruefe.Gleich(ap - 2, k.Aktionspunkte.Vorrat);
            e = w.Ausfuehren(k, new Razzia(dieb.Inhaber));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("genug Razzien"), e.Meldung);
            var ohne = w.KontoEroeffnen("Familie Ehrlich", startvermoegen: 5_000);
            e = w.Ausfuehren(ohne, new Razzia(dieb.Inhaber));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Wachhaus"), e.Meldung);
        }),

        ("Razzia: findet Beweise für die Taten eines Hauses (in der Garnison mit 70 % je Tat)", () =>
        {
            var sim = NeueSimulation();
            var (w, k, _) = Wache(sim, Betriebsart.Garnison);
            var suender = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
            for (var i = 0; i < 3; i++)
                Pruefe.Wahr(w.Ausfuehren(suender, new Schenken(w.Rat.Ratsherren[i].Name, 100)).Erfolg, "Bestechung");
            Pruefe.Ungefaehr(0.7, w.Wache.Razziachance(k), 1e-9);
            Pruefe.Gleich(3, w.Wache.FreieRazzien(k));
            Pruefe.Wahr(w.Ausfuehren(k, new Razzia(suender.Inhaber)).Erfolg, "Razzia");
            Pruefe.Wahr(w.Justiz.Beweise(k, suender).Any(), "keine Beweise gefunden");
            Pruefe.Gleich(2, w.Wache.FreieRazzien(k));
        }),

        ("Kopfgeld: Gewinnt die Wache eine Anklage, zahlt die Stadt ein Viertel der Buße", () =>
        {
            var sim = NeueSimulation();
            var (w, k, _) = Wache(sim);
            var suender = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
            for (var i = 0; i < 3; i++)
                Pruefe.Wahr(w.Ausfuehren(suender, new Schenken(w.Rat.Ratsherren[i].Name, 100)).Erfolg, "Bestechung");
            foreach (var spur in w.Justiz.SpurenVon(suender).ToList())
                w.Justiz.BeweisGeben(k, spur);
            k.Aktionspunkte.Setzen(6); // 3 Bestechungen gab es nicht – die Anklage braucht 2 AP
            var e = w.Ausfuehren(k, new Anklagen(suender.Inhaber));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            JahrBeenden(sim);
            var busse = suender.Vorjahr!.Gericht;
            Pruefe.Wahr(busse > 0, "nicht verurteilt");
            Pruefe.Ungefaehr(busse * 0.25, w.Wache.Vorjahr(k)!.Kopfgeld, 1e-6);
        }),

        ("Kopfgeld: Einen Taschendieb fasst die Wache – sie bekommt den Beweis und 50 Gulden", () =>
        {
            var sim = NeueSimulation(Tolpatschig.Value);
            var (w, wache, _) = Wache(sim);
            var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 5_000);
            var loch = w.BetriebEroeffnen("Schmugglerloch Elster", Betriebsart.Schmugglerloch, dieb, startkarren: "");
            w.Einstellen(loch, "Geselle", 4);
            w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
            for (var i = 0; i < 4 * 60 + 1; i++) sim.Tick(); // 09:01 – die erste Arbeitsstunde ist abgerechnet
            var spur = w.Justiz.SpurenVon(dieb).Single();
            Pruefe.Wahr(w.Justiz.KenntSpur(wache, spur), "Die Wache hat den Beweis");
            Pruefe.Ungefaehr(50, w.Wache.DiesesJahr(wache).Kopfgeld, 1e-9);
        }),

        ("Geleitschutz: Die Stadtwache gibt es erst mit einem Wachhaus – der Lohn je beladener Fahrt geht an die Wache", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var kunde = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
            var grube = w.BetriebEroeffnen("Grube Eisenbart", Betriebsart.Mine, kunde);
            var e = w.Ausfuehren(kunde, new EskorteWaehlen(grube.Name, 0, "Stadtwache"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Wachhaus"), e.Meldung);
            var (_, wache, _) = Wache(sim);
            Pruefe.Wahr(w.Ausfuehren(kunde, new EskorteWaehlen(grube.Name, 0, "Stadtwache")).Erfolg, "jetzt geht es");
            grube.Einlagern("Eisen", 30);
            var transport = kunde.Laufend.Transport;
            for (var i = 0; i < 120 && w.Wache.DiesesJahr(wache).Geleitgeld == 0; i++) sim.Tick();
            Pruefe.Ungefaehr(20, w.Wache.DiesesJahr(wache).Geleitgeld, 1e-9);
            Pruefe.Ungefaehr(transport + 20, kunde.Laufend.Transport, 1e-9);
        }),

        ("KI: Ehrbare Häuser dürfen den Gardisten lernen, wagemutige nicht; eine KI-Wache macht Razzia bei Dieben", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (_, k, _) = Wache(sim, name: "Haus Wacker");
            w.KiAnmelden(k, KiProfil.Standard);
            var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 5_000);
            var loch = w.BetriebEroeffnen("Schmugglerloch Elster", Betriebsart.Schmugglerloch, dieb, startkarren: "");
            loch.Einlagern("Silber", 5);
            for (var i = 0; i < 7 * 60; i++) sim.Tick(); // über die Politikstunde
            Pruefe.Gleich(1, w.Wache.DiesesJahr(k).Razzien);
            Pruefe.Gleich(0, loch.ImLager("Silber"));
            Pruefe.Wahr(w.Rivalen.Single(r => r.Konto == k).WacheErlaubt, "ausgewogen darf");
            var wagemutig = w.KontoEroeffnen("Haus Rabe", startvermoegen: 5_000);
            w.KiAnmelden(wagemutig, Partieaufbau.ProfilFuer(KiCharakter.Wagemutig));
            Pruefe.Wahr(!w.Rivalen.Single(r => r.Konto == wagemutig).WacheErlaubt, "wagemutig darf nicht");
        }),

        ("Speichern: Wachbuch übersteht Speichern und Laden; Startberuf Gardist", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Beruf: "Gardist", Startvermoegen: 10_000));
            var w = sim.Wirtschaft!;
            var wachhaus = w.Betriebe.Single(b => b.Konto == spieler);
            Pruefe.Gleich("Wachhaus am Weinmarkt", wachhaus.Name);
            for (var i = 0; i < 5 * 60 + 30; i++) sim.Tick(); // 11:30
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var kw = kopie.Wirtschaft!;
            Pruefe.Gleich(w.Wache.DiesesJahr(spieler), kw.Wache.DiesesJahr(kopieSpieler!));
            for (var i = 0; i < 120; i++) { sim.Tick(); kopie.Tick(); }
            Pruefe.Gleich(w.Wache.DiesesJahr(spieler), kw.Wache.DiesesJahr(kopieSpieler!));
        }),
    };
}
