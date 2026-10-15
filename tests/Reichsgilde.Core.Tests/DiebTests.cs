using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für den Dieb: Taschendiebe, Fang, Einbruch, Auskundschaften, Schutzgeld, Entführung, Ansehen, KI.</summary>
static class DiebTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Niemand wird je erwischt – für Rechnungen ohne Zufall.</summary>
    static readonly Lazy<Wirtschaftsdaten> Unerwischt = new(() => Wirtschaftsdaten.AusJson(Json.Replace("\"fangJeStunde\": 0.012", "\"fangJeStunde\": 0")));

    /// <summary>Jede Stunde wird einer erwischt.</summary>
    static readonly Lazy<Wirtschaftsdaten> Tolpatschig = new(() => Wirtschaftsdaten.AusJson(Json.Replace("\"fangJeStunde\": 0.012", "\"fangJeStunde\": 1")));

    /// <summary>Alles gelingt so sicher wie möglich (95 %), niemand wird am Markt erwischt.</summary>
    static readonly Lazy<Wirtschaftsdaten> Geschickt = new(() => Wirtschaftsdaten.AusJson(Json
        .Replace("\"fangJeStunde\": 0.012", "\"fangJeStunde\": 0")
        .Replace("\"auskundschaften\": { \"abStufe\": 1, \"chance\": 0.4 }", "\"auskundschaften\": { \"abStufe\": 1, \"chance\": 1 }")
        .Replace("\"entfuehrung\": { \"abStufe\": 3, \"chance\": 0.55 }", "\"entfuehrung\": { \"abStufe\": 3, \"chance\": 1 }")
        .Replace("\"diebstahl\": { \"kosten\": 150, \"chance\": 0.7", "\"diebstahl\": { \"kosten\": 150, \"chance\": 1")
        .Replace("\"sabotage\": { \"kosten\": 250, \"chance\": 0.6", "\"sabotage\": { \"kosten\": 250, \"chance\": 1")
        .Replace("\"zeugenChance\": 0.25", "\"zeugenChance\": 0")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Bis 21:01 – nach Arbeitszeit (6–18 Uhr) und der Nacht der Gauner.</summary>
    static void BisNachDerNacht(Simulation sim)
    {
        for (var i = 0; i < 15 * 60 + 1; i++) sim.Tick();
    }

    /// <summary>Ein Haus mit einem Diebesbetrieb der gewünschten Stufe, voll mit Gesellen, ohne Karren.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb B) Dieb(Simulation sim, Betriebsart art = Betriebsart.Schmugglerloch, string name = "Familie Elster", int? leute = null)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: 10_000);
        var b = w.BetriebEroeffnen($"{w.Daten.Art(art).Name} {name}", art, k, startkarren: "");
        w.Einstellen(b, "Geselle", leute ?? b.Plaetze);
        return (w, k, b);
    }

    /// <summary>Ein Opfer mit einer Gießerei, deren Lager voller Rapiere ist.</summary>
    static (Konto K, Betrieb G) Opfer(Stadtwirtschaft w, string name = "Haus Eisenbart")
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 10_000);
        var g = w.BetriebEroeffnen($"Gießerei {name}", Betriebsart.Giesserei, k, startkarren: "");
        g.Einlagern("Rapier", 20);
        return (k, g);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: Schmugglerloch → Unterschlupf → Diebesgilde mit 4/6/8 Plätzen, ohne Ware; der Dieb hat kein Umland", () =>
        {
            var d = Daten.Value;
            Pruefe.Wahr(d.Art(Betriebsart.Schmugglerloch).OhneProduktion, "Schmugglerloch stellt nichts her");
            Pruefe.Gleich(Betriebsart.Unterschlupf, d.Art(Betriebsart.Schmugglerloch).Ausbau);
            Pruefe.Gleich(Betriebsart.Diebesgilde, d.Art(Betriebsart.Unterschlupf).Ausbau);
            Pruefe.Wahr(new[] { Betriebsart.Schmugglerloch, Betriebsart.Unterschlupf, Betriebsart.Diebesgilde }.Select(a => d.Art(a).Plaetze).SequenceEqual(new[] { 4, 6, 8 }), "Plätze");
            Pruefe.Gleich<Betriebsart?>(null, d.Berufe["Dieb"].Umland);
            Pruefe.Wahr(d.Justiz!.KerkerFuer.Contains(Vergehen.Entfuehrung), "Entführung führt in den Kerker");
            var falsch = Json.Replace("\"produkte\": [ \"Papier\", \"Kerze\", \"Lehrschrift\" ], \"standardprodukt\": \"Kerze\", ", "");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(falsch), "nur ein Betrieb des Diebes");
        }),

        ("Taschendiebe: 4 Gesellen bringen an einem Arbeitstag 4 × 720 × 0,8 Gulden – steuerfrei, mit Erfahrung im Beruf", () =>
        {
            var sim = NeueSimulation(Unerwischt.Value);
            var (w, k, b) = Dieb(sim);
            var umsatz = k.Laufend.Umsatz;
            BisNachDerNacht(sim);
            Pruefe.Ungefaehr(4 * 720 * 0.8, w.Diebe.DiesesJahr(k).Beute, 1e-6);
            Pruefe.Ungefaehr(umsatz + 4 * 720 * 0.8, k.Laufend.Umsatz, 1e-6);
            Pruefe.Gleich(0.0, k.Laufend.Steuer);
            Pruefe.Ungefaehr(48, k.Erfahrung("Dieb"), 1e-6);
            Pruefe.Ungefaehr(0.8 * 1.3, w.Diebe.WertJeMinute(3), 1e-12);
            Pruefe.Ungefaehr(720 * 0.8 - 220, Kalkulation.Jahresertrag(w, b, "Geselle"), 1e-6);
        }),

        ("Fang: Ein erwischter Dieb kommt in den Turm (Lehrling zuerst) – jedes andere Haus hat einen Beweis", () =>
        {
            var sim = NeueSimulation(Tolpatschig.Value);
            var (w, k, b) = Dieb(sim, leute: 3);
            w.Einstellen(b, "Lehrling", 1);
            var anderes = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
            for (var i = 0; i < 3 * 60 + 1; i++) sim.Tick(); // 09:01 – die erste Arbeitsstunde (ab 8 Uhr) ist abgerechnet
            Pruefe.Gleich(0, b.Belegung.GetValueOrDefault("Lehrling"));
            Pruefe.Gleich(3, b.Belegt);
            var spur = w.Justiz.SpurenVon(k).Single();
            Pruefe.Gleich(Vergehen.Taschendiebstahl, spur.Art);
            Pruefe.Wahr(w.Justiz.KenntSpur(anderes, spur), "Die Stadt weiß, wem der Dieb diente");
            Pruefe.Gleich(1, w.Diebe.DiesesJahr(k).Faenge);
        }),

        ("Ansehen: Ein Haus mit Schmugglerloch hat 3 Ansehen weniger", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Elster", startvermoegen: 10_000);
            var vorher = w.Rat.Ansehen(k);
            w.BetriebEroeffnen("Schmugglerloch Elster", Betriebsart.Schmugglerloch, k, startkarren: "");
            Pruefe.Ungefaehr(vorher - 3, w.Rat.Ansehen(k), 1e-9);
        }),

        ("Einbruch: eigene Leute, 1 AP, ein Auftrag je Nacht im Schmugglerloch – die Beute landet im eigenen Lager", () =>
        {
            var sim = NeueSimulation(Geschickt.Value);
            var (w, k, b) = Dieb(sim);
            var (opfer, giesserei) = Opfer(w);
            var ap = k.Aktionspunkte.Vorrat;
            var e = w.Ausfuehren(k, new Einbrechen(opfer.Inhaber, giesserei.Name));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(ap - 1, k.Aktionspunkte.Vorrat);
            e = w.Ausfuehren(k, new Auskundschaften(opfer.Inhaber));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("genug zu tun"), e.Meldung);
            e = w.Ausfuehren(k, new Entfuehren(opfer.Inhaber, 1, 500));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Stufe 3"), e.Meldung);
            BisNachDerNacht(sim);
            Pruefe.Gleich(12, b.ImLager("Rapier"));
            Pruefe.Gleich(8, giesserei.ImLager("Rapier"));
            Pruefe.Gleich(Vergehen.Diebstahl, w.Justiz.SpurenVon(k).Single().Art);
        }),

        ("Einbruch: Ohne Diebesbetrieb geht es nicht; im Unterschlupf tragen die Leute 18 Stück", () =>
        {
            var sim = NeueSimulation(Geschickt.Value);
            var w = sim.Wirtschaft!;
            var ohne = w.KontoEroeffnen("Familie Ehrlich", startvermoegen: 10_000);
            var (opfer, giesserei) = Opfer(w);
            var e = w.Ausfuehren(ohne, new Einbrechen(opfer.Inhaber, giesserei.Name));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Schmugglerloch"), e.Meldung);
            var (_, k, b) = Dieb(sim, Betriebsart.Unterschlupf);
            Pruefe.Gleich(18, w.Diebe.EinbruchStueck(k));
            Pruefe.Gleich(2, w.Diebe.FreieAuftraege(k));
        }),

        ("Auskundschaften: Die Diebe finden Beweise für die Taten eines Hauses – ohne selbst eine Spur zu hinterlassen", () =>
        {
            var sim = NeueSimulation(Geschickt.Value);
            var (w, k, _) = Dieb(sim);
            var (opfer, _) = Opfer(w);
            Pruefe.Wahr(w.Ausfuehren(opfer, new Schenken(w.Rat.Ratsherren[0].Name, 100)).Erfolg, "Bestechung");
            Pruefe.Wahr(w.Ausfuehren(k, new Auskundschaften(opfer.Inhaber)).Erfolg, "Auftrag");
            BisNachDerNacht(sim);
            Pruefe.Gleich(1, w.Justiz.Beweise(k, opfer).Count());
            Pruefe.Gleich(0, w.Justiz.SpurenVon(k).Count());
        }),

        ("Schutzgeld: erst ab Unterschlupf; die Forderung ist ein Beweis; wer zahlt, zahlt – wer nicht, dem brennt nachts ein Betrieb", () =>
        {
            var sim = NeueSimulation(Geschickt.Value);
            var (w, klein, _) = Dieb(sim, name: "Familie Klein");
            var (opfer, giesserei) = Opfer(w);
            var e = w.Ausfuehren(klein, new SchutzgeldFordern(opfer.Inhaber, 300));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Unterschlupf"), e.Meldung);

            var (_, k, _) = Dieb(sim, Betriebsart.Unterschlupf);
            Pruefe.Wahr(w.Ausfuehren(k, new SchutzgeldFordern(opfer.Inhaber, 300)).Erfolg, "Forderung");
            var spur = w.Justiz.SpurenVon(k).Single();
            Pruefe.Gleich(Vergehen.Schutzgeld, spur.Art);
            Pruefe.Wahr(w.Justiz.KenntSpur(opfer, spur), "Das Opfer weiß, wer fordert");
            var (vorherOpfer, vorherDieb) = (opfer.Vermoegen, k.Vermoegen);
            Pruefe.Wahr(w.Ausfuehren(opfer, new DiebesforderungBeantworten(k.Inhaber, Diebesbriefart.Schutzgeld, true)).Erfolg, "zahlen");
            Pruefe.Ungefaehr(vorherOpfer - 300, opfer.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(vorherDieb + 300, k.Vermoegen, 1e-9);

            Pruefe.Wahr(w.Ausfuehren(k, new SchutzgeldFordern(opfer.Inhaber, 500)).Erfolg, "zweite Forderung");
            Pruefe.Wahr(w.Ausfuehren(opfer, new DiebesforderungBeantworten(k.Inhaber, Diebesbriefart.Schutzgeld, false)).Erfolg, "ablehnen");
            Pruefe.Wahr(w.Diebe.Auftraege.Any(a => a.Art == Diebesstueck.Brandstiftung && a.Betrieb == giesserei.Name), "Brand vorgemerkt");
            BisNachDerNacht(sim);
            Pruefe.Wahr(giesserei.SabotageMinuten > 0, "Es brennt");
            Pruefe.Wahr(w.Justiz.SpurenVon(k).Any(s => s.Art == Vergehen.Sabotage), "Spur der Brandstiftung");
        }),

        ("Entführung: erst in der Diebesgilde; nie das Oberhaupt; Geisel erbt nicht – Lösegeld befreit sie", () =>
        {
            var sim = NeueSimulation(Geschickt.Value);
            var (w, k, _) = Dieb(sim, Betriebsart.Diebesgilde);
            var (opfer, _) = Opfer(w);
            var f = w.Familien.Gruenden(opfer, Volk.Mensch, Geschlecht.Mann, alter: 50);
            var tochter = w.Familien.KindAufnehmen(opfer, 20);
            Pruefe.Gleich(tochter, w.Familien.Erbe(f));
            var e = w.Ausfuehren(k, new Entfuehren(opfer.Inhaber, f.Oberhaupt.Nummer, 800));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Oberhaupt"), e.Meldung);
            var ap = k.Aktionspunkte.Vorrat;
            Pruefe.Wahr(w.Ausfuehren(k, new Entfuehren(opfer.Inhaber, tochter.Nummer, 800)).Erfolg, "Auftrag");
            Pruefe.Gleich(ap - 2, k.Aktionspunkte.Vorrat);
            BisNachDerNacht(sim);
            Pruefe.Wahr(w.Diebe.IstGeisel(tochter), "Geisel");
            Pruefe.Gleich<Person?>(null, w.Familien.Erbe(f));
            Pruefe.Gleich(Vergehen.Entfuehrung, w.Justiz.SpurenVon(k).Single().Art);
            Pruefe.Wahr(w.Justiz.KenntSpur(opfer, w.Justiz.SpurenVon(k).Single()), "Der Lösegeldbrief ist ein Beweis");
            var vorher = k.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(opfer, new DiebesforderungBeantworten(k.Inhaber, Diebesbriefart.Loesegeld, true)).Erfolg, "Lösegeld");
            Pruefe.Ungefaehr(vorher + 800, k.Vermoegen, 1e-9);
            Pruefe.Wahr(!w.Diebe.IstGeisel(tochter) && tochter.Lebt, "frei");
            Pruefe.Gleich(tochter, w.Familien.Erbe(f));
        }),

        ("Entführung: Kommt bis Ende des Folgejahres kein Lösegeld, wird die Geisel in die Fremde verkauft", () =>
        {
            var sim = NeueSimulation(Geschickt.Value);
            var (w, k, _) = Dieb(sim, Betriebsart.Diebesgilde);
            var (opfer, _) = Opfer(w);
            var f = w.Familien.Gruenden(opfer, Volk.Zwerg, Geschlecht.Mann, alter: 50);
            var sohn = w.Familien.KindAufnehmen(opfer, 10, Geschlecht.Mann);
            Pruefe.Wahr(w.Ausfuehren(k, new Entfuehren(opfer.Inhaber, sohn.Nummer, 5_000)).Erfolg, "Auftrag");
            BisNachDerNacht(sim);
            Pruefe.Wahr(w.Diebe.IstGeisel(sohn), "Geisel");
            JahrBeenden(sim);
            Pruefe.Wahr(sohn.Lebt && w.Diebe.IstGeisel(sohn), "Die Frist läuft bis Ende des Folgejahres");
            JahrBeenden(sim);
            Pruefe.Wahr(!sohn.Lebt && !w.Diebe.IstGeisel(sohn), "verkauft");
            Pruefe.Gleich("in die Fremde verschleppt", sohn.Todesursache);
            Pruefe.Wahr(f.Oberhaupt.Lebt, "Das Oberhaupt bleibt");
        }),

        ("KI: Nur ein wagemutiges Haus erlernt den Dieb; jede KI zahlt Lösegeld, wenn sie es sich leisten kann", () =>
        {
            var sim = NeueSimulation(Geschickt.Value);
            var w = sim.Wirtschaft!;
            var (_, dieb, _) = Dieb(sim, Betriebsart.Diebesgilde);
            var (opfer, _) = Opfer(w);
            w.Familien.Gruenden(opfer, Volk.Zwerg, Geschlecht.Mann, alter: 50);
            var tochter = w.Familien.KindAufnehmen(opfer, 18);
            w.KiAnmelden(opfer, KiProfil.Standard);
            Pruefe.Wahr(w.Ausfuehren(dieb, new Entfuehren(opfer.Inhaber, tochter.Nummer, 600)).Erfolg, "Auftrag");
            BisNachDerNacht(sim);
            Pruefe.Wahr(w.Diebe.IstGeisel(tochter), "Geisel");
            for (var i = 0; i < 16 * 60; i++) sim.Tick(); // über die Politikstunde des nächsten Tages
            Pruefe.Wahr(!w.Diebe.IstGeisel(tochter) && tochter.Lebt, "Die KI zahlt");

            var ki = w.Rivalen.Single(r => r.Konto == opfer);
            var wagemutig = w.KontoEroeffnen("Haus Rabe", startvermoegen: 50_000);
            w.KiAnmelden(wagemutig, Partieaufbau.ProfilFuer(KiCharakter.Wagemutig));
            Pruefe.Wahr(w.Rivalen.Single(r => r.Konto == wagemutig).DiebErlaubt, "Wagemutig darf");
            Pruefe.Wahr(!ki.DiebErlaubt, "Ausgewogen darf nicht");
        }),

        ("KI: Ein wagemutiges Haus mit Unterschlupf bricht ein und verlangt Schutzgeld – das Opfer antwortet gleich", () =>
        {
            var sim = NeueSimulation(Geschickt.Value);
            var w = sim.Wirtschaft!;
            var (_, dieb, versteck) = Dieb(sim, Betriebsart.Unterschlupf, name: "Haus Rabe");
            w.KiAnmelden(dieb, Partieaufbau.ProfilFuer(KiCharakter.Wagemutig));
            var (opfer, giesserei) = Opfer(w);
            w.KiAnmelden(opfer, KiProfil.Standard);
            BisNachDerNacht(sim);
            Pruefe.Wahr(versteck.ImLager("Rapier") > 0, "kein Einbruch");
            Pruefe.Wahr(w.Justiz.SpurenVon(dieb).Any(x => x.Art == Vergehen.Schutzgeld), "kein Schutzgeld verlangt");
            // Die Opfer-KI antwortet in derselben Politikstunde – zahlen oder Wächter und Nein.
            Pruefe.Wahr(w.Diebe.BriefAn(opfer, dieb, Diebesbriefart.Schutzgeld) is null, "Das Opfer hat nicht geantwortet");
        }),

        ("Speichern: Aufträge, Forderungen, Geiseln und Diebesbuch überstehen Speichern und Laden; Startberuf Dieb", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                Unerwischt.Value, new NeuePartie(Beruf: "Dieb", Startvermoegen: 10_000));
            var w = sim.Wirtschaft!;
            var loch = w.Betriebe.Single(b => b.Konto == spieler);
            Pruefe.Gleich("Schmugglerloch am Weinmarkt", loch.Name);
            for (var i = 0; i < 5 * 60 + 30; i++) sim.Tick(); // 11:30 – die Taschendiebe waren schon unterwegs
            var rivale = w.Konten.First(k => k != spieler);
            Pruefe.Wahr(w.Ausfuehren(spieler, new Auskundschaften(rivale.Inhaber)).Erfolg, "Auftrag");
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var kw = kopie.Wirtschaft!;
            Pruefe.Gleich(w.Diebe.DiesesJahr(spieler), kw.Diebe.DiesesJahr(kopieSpieler!));
            Pruefe.Gleich(1, kw.Diebe.Auftraege.Count);
            Pruefe.Ungefaehr(loch.Fortschritt, kw.Betriebe.Single(b => b.Konto == kopieSpieler).Fortschritt, 1e-9);
            for (var i = 0; i < 120; i++) { sim.Tick(); kopie.Tick(); }
            Pruefe.Gleich(w.Diebe.DiesesJahr(spieler), kw.Diebe.DiesesJahr(kopieSpieler!));
        }),
    };
}
