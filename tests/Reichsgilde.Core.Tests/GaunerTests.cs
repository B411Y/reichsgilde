using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für die Unterwelt: Diebstahl, Sabotage, Überfall, Wächter, Zeugen, Erpressung, Kerker und die KI.</summary>
static class GaunerTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.EinVersuchJeNacht(File.ReadAllText(Datenpfad.Wirtschaft()))));

    /// <summary>Daten mit festen Ausgängen: Gaunerstücke gelingen immer, Zeugen nach Wunsch, Spitzel finden alles.</summary>
    static Wirtschaftsdaten Sicher(double zeugen = 0) => Wirtschaftsdaten.AusJson(Datenpfad.EinVersuchJeNacht(File.ReadAllText(Datenpfad.Wirtschaft()))
        .Replace("\"chance\": 0.7", "\"chance\": 1.0")
        .Replace("\"chance\": 0.6", "\"chance\": 1.0")
        .Replace("\"kosten\": 300, \"staerke\": 0.8", "\"kosten\": 300, \"staerke\": 1.0")
        .Replace("\"zeugenChance\": 0.25", $"\"zeugenChance\": {zeugen.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
        .Replace("\"zeugenChanceWaechter\": 0.6", $"\"zeugenChanceWaechter\": {zeugen.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
        .Replace("\"spitzelChance\": 0.5", "\"spitzelChance\": 1.0"));

    static readonly Lazy<Wirtschaftsdaten> OhneZeugen = new(() => Sicher(0));
    static readonly Lazy<Wirtschaftsdaten> MitZeugen = new(() => Sicher(1));

    static Simulation NeueSimulation(Wirtschaftsdaten daten) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: daten);

    static Konto Haus(Stadtwirtschaft w, string name, double vermoegen = 20_000) => w.KontoEroeffnen(name, startvermoegen: vermoegen);

    static void BisZurNacht(Simulation sim)
    {
        while (sim.Zeit.MinuteImTag != Unterwelt.Nachtstunde) sim.Tick();
        sim.Tick();
    }

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Hinterzimmer: Bande kostet im Voraus; nicht gegen sich selbst, nur gegen Betriebe des Ziels, ein Auftrag je Nacht", () =>
        {
            var w = NeueSimulation(Daten.Value).Wirtschaft!;
            var a = Haus(w, "Familie Dieb");
            var z = Haus(w, "Familie Opfer");
            var giesserei = w.BetriebEroeffnen("Gießerei Opfer", Betriebsart.Giesserei, z);
            w.BetriebEroeffnen("Gießerei Dieb", Betriebsart.Giesserei, a);
            Pruefe.Wahr(w.Gauner.IstAktiv, "keine Unterwelt");
            Pruefe.Wahr(!w.Ausfuehren(a, new Beauftragen(a.Inhaber, Gaunerstueck.Diebstahl, "Gießerei Dieb")).Erfolg, "gegen sich selbst");
            Pruefe.Wahr(!w.Ausfuehren(a, new Beauftragen(z.Inhaber, Gaunerstueck.Diebstahl, "Gießerei Dieb")).Erfolg, "fremder Betrieb");
            var e = w.Ausfuehren(a, new Beauftragen(z.Inhaber, Gaunerstueck.Ueberfall));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Umlandwegen"), e.Meldung);
            var vorher = a.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(a, new Beauftragen(z.Inhaber, Gaunerstueck.Diebstahl, giesserei.Name)).Erfolg, "Auftrag");
            Pruefe.Gleich(150.0, vorher - a.Vermoegen);
            Pruefe.Gleich(150.0, a.Laufend.Gaunerei);
            Pruefe.Wahr(!w.Ausfuehren(a, new Beauftragen(z.Inhaber, Gaunerstueck.Diebstahl, giesserei.Name)).Erfolg, "doppelter Auftrag");
        }),

        ("Diebstahl: nachts um 21 Uhr, die wertvollste Ware zuerst, höchstens 12 Stück, der Hehler zahlt die Hälfte", () =>
        {
            var sim = NeueSimulation(OhneZeugen.Value);
            var w = sim.Wirtschaft!;
            var a = Haus(w, "Familie Dieb");
            var z = Haus(w, "Familie Opfer");
            var b = w.BetriebEroeffnen("Gießerei Opfer", Betriebsart.Giesserei, z, startkarren: ""); // ohne Karren bleibt alles im Lager
            b.Einlagern("Rapier", 5);
            b.Einlagern("Eisen", 20);
            w.Ausfuehren(a, new Beauftragen(z.Inhaber, Gaunerstueck.Diebstahl, b.Name));
            while (sim.Zeit.MinuteImTag != Unterwelt.Nachtstunde) sim.Tick();
            var beute = w.Gauner.MoeglicheBeute(b);
            Pruefe.Gleich(12, beute.Sum(x => x.Stueck));
            Pruefe.Gleich("Rapier", beute[0].Ware); // die wertvollste Ware zuerst
            Pruefe.Wahr(beute.Zip(beute.Skip(1)).All(p => w.Markt[p.First.Ware].Preis >= w.Markt[p.Second.Ware].Preis), "nach Preis absteigend");
            var lagerVorher = beute.ToDictionary(x => x.Ware, x => b.ImLager(x.Ware));
            var wert = beute.Sum(x => x.Wert);
            var vorher = a.Vermoegen;
            sim.Tick(); // 21:00 – die Bande schlägt zu
            foreach (var (ware, stueck, _) in beute)
                Pruefe.Gleich(lagerVorher[ware] - stueck, b.ImLager(ware));
            Pruefe.Ungefaehr(wert, z.Laufend.Raubwert, 0.01);
            Pruefe.Ungefaehr(wert * 0.5, a.Vermoegen - vorher, 0.01);
            var spur = w.Justiz.SpurenVon(a).Single();
            Pruefe.Gleich(Vergehen.Diebstahl, spur.Art);
            Pruefe.Gleich(0, w.Justiz.Beweise(z, a).Count()); // ohne Zeugen weiß das Opfer nicht, wer es war
            Pruefe.Wahr(w.Gauner.NachrichtenFuer(z, false).Any(t => t.Contains("Einbruch in Gießerei Opfer")), "öffentliche Nachricht");
            Pruefe.Wahr(w.Gauner.NachrichtenFuer(a, false).Any(t => t.Contains("Hehler")), "Bericht an den Auftraggeber");
            Pruefe.Wahr(!w.Gauner.NachrichtenFuer(z, false).Any(t => t.Contains("Hehler")), "Der Hehlerbericht ist nicht öffentlich");
            Pruefe.Gleich(1400, w.Gauner.LetzterAngriff(b.Name));
        }),

        ("Zeuge: Mit Zeugen hat das Opfer sofort einen Beweis – und die Stadt erfährt, wer es war", () =>
        {
            var sim = NeueSimulation(MitZeugen.Value);
            var w = sim.Wirtschaft!;
            var a = Haus(w, "Familie Dieb");
            var z = Haus(w, "Familie Opfer");
            var b = w.BetriebEroeffnen("Gießerei Opfer", Betriebsart.Giesserei, z);
            w.Ausfuehren(a, new Beauftragen(z.Inhaber, Gaunerstueck.Sabotage, b.Name));
            BisZurNacht(sim);
            Pruefe.Gleich(1, w.Justiz.Beweise(z, a).Count());
            Pruefe.Wahr(w.Gauner.NachrichtenFuer(z, false).Any(t => t.Contains("Zeuge") && t.Contains(a.Inhaber)), "Zeugenmeldung");
        }),

        ("Wächter: halbiert die Chance auf Diebstahl und Sabotage, macht Zeugen wahrscheinlicher, kostet Lohn", () =>
        {
            var sim = NeueSimulation(Daten.Value);
            var w = sim.Wirtschaft!;
            var z = Haus(w, "Familie Opfer");
            var b = w.BetriebEroeffnen("Gießerei Opfer", Betriebsart.Giesserei, z);
            Pruefe.Ungefaehr(0.7, w.Gauner.Erfolgschance(Gaunerstueck.Diebstahl, b), 1e-9);
            Pruefe.Wahr(w.Ausfuehren(z, new WaechterSetzen(b.Name, true)).Erfolg, "Wächter");
            Pruefe.Ungefaehr(0.35, w.Gauner.Erfolgschance(Gaunerstueck.Diebstahl, b), 1e-9);
            Pruefe.Ungefaehr(0.3, w.Gauner.Erfolgschance(Gaunerstueck.Sabotage, b), 1e-9);
            Pruefe.Ungefaehr(0.6, w.Gauner.Zeugenchance(b, true), 1e-9);
            Pruefe.Ungefaehr(1.0, w.Gauner.Zeugenchance(b, false), 1e-9); // misslungen: doppelt so oft gesehen, höchstens 100 %
            JahrBeenden(sim);
            Pruefe.Ungefaehr(150, z.Vorjahr!.Personal, 1e-9);
        }),

        ("Sabotage: Der Betrieb steht sechs Arbeitsstunden still, die Reparatur kostet das Opfer", () =>
        {
            var sim = NeueSimulation(OhneZeugen.Value);
            var w = sim.Wirtschaft!;
            var a = Haus(w, "Familie Täter");
            var z = Haus(w, "Familie Opfer");
            var b = w.BetriebEroeffnen("Gießerei Opfer", Betriebsart.Giesserei, z);
            w.Einstellen(b, "Geselle", 2);
            b.Einlagern("Eisen", 50);
            b.Einlagern("Silber", 50);
            w.Ausfuehren(a, new Beauftragen(z.Inhaber, Gaunerstueck.Sabotage, b.Name));
            BisZurNacht(sim);
            Pruefe.Gleich(360, b.SabotageMinuten);
            Pruefe.Ungefaehr(150, z.Laufend.Schaden, 1e-9);
            // Am nächsten Arbeitstag: erst 6 Stunden Reparatur, dann wird wieder gearbeitet.
            while (sim.Zeit.UhrzeitInMinuten != 13 * 60) sim.Tick();
            Pruefe.Wahr(b.Stillstand?.Contains("repariert") == true, $"Stillstand: {b.Stillstand}");
            while (sim.Zeit.UhrzeitInMinuten != 15 * 60) sim.Tick();
            Pruefe.Gleich(0, b.SabotageMinuten);
            Pruefe.Wahr(b.Stillstand?.Contains("repariert") != true, "Reparatur sollte vorbei sein");
        }),

        ("Überfall: Wegelagerer lauern der nächsten beladenen Umlandfahrt auf – die Eskorte schützt", () =>
        {
            var sim = NeueSimulation(OhneZeugen.Value);
            var w = sim.Wirtschaft!;
            var a = Haus(w, "Familie Räuber");
            var z = Haus(w, "Familie Grube");
            var mine = w.BetriebEroeffnen("Grube Opfer", Betriebsart.Mine, z);
            w.Einstellen(mine, "Bergmann", 3);
            w.Ausfuehren(mine.Konto, new EskorteWaehlen(mine.Name, 0, "Keine"));
            Pruefe.Wahr(w.Ausfuehren(a, new Beauftragen(z.Inhaber, Gaunerstueck.Ueberfall)).Erfolg, "Auftrag");
            BisZurNacht(sim);
            Pruefe.Gleich(1, w.Gauner.Hinterhalte.Count);
            var vorher = a.Vermoegen;
            while (w.Gauner.Hinterhalte.Count > 0 && sim.Zeit.Jahr == 1400) sim.Tick();
            if (w.Gauner.Hinterhalte.Count > 0) { JahrBeenden(sim); }
            var spur = w.Justiz.SpurenVon(a).SingleOrDefault();
            Pruefe.Wahr(spur?.Art == Vergehen.Raub, "Spur Raub fehlt: " + string.Join(" | ", w.Gauner.NachrichtenFuer(a, false)));
            if (spur!.Betrag > 0)
            {
                Pruefe.Ungefaehr(spur.Betrag * 0.5, a.Vermoegen - vorher, 0.01);
                Pruefe.Ungefaehr(spur.Betrag, z.Laufend.Raubwert + (z.Vorjahr?.Raubwert ?? 0), 0.01);
            }
        }),

        ("Überfall: Ohne beladene Fahrt ziehen die Wegelagerer nach einem Tag wieder ab", () =>
        {
            var sim = NeueSimulation(OhneZeugen.Value);
            var w = sim.Wirtschaft!;
            var a = Haus(w, "Familie Räuber");
            var z = Haus(w, "Familie Grube");
            w.BetriebEroeffnen("Grube Opfer", Betriebsart.Mine, z); // ohne Bergleute fährt nichts
            w.Ausfuehren(a, new Beauftragen(z.Inhaber, Gaunerstueck.Ueberfall));
            BisZurNacht(sim);
            Pruefe.Gleich(1, w.Gauner.Hinterhalte.Count);
            BisZurNacht(sim);
            BisZurNacht(sim);
            Pruefe.Gleich(0, w.Gauner.Hinterhalte.Count);
            Pruefe.Wahr(w.Gauner.NachrichtenFuer(a, true).Concat(w.Gauner.NachrichtenFuer(a, false)).Any(t => t.Contains("vergeblich")), "Abzug gemeldet");
        }),

        ("Erpressung: erst ab 3 Beweisen; der Brief ist selbst ein Beweis; wer zahlt, dessen Beweise verbrennen", () =>
        {
            var sim = NeueSimulation(OhneZeugen.Value);
            var w = sim.Wirtschaft!;
            var erpresser = Haus(w, "Familie Erpresser");
            var opfer = Haus(w, "Familie Opfer");
            var ratsherr = w.Rat.Ratsherren[0].Name;
            w.Ausfuehren(opfer, new Schenken(ratsherr, 100));
            w.Ausfuehren(opfer, new Schenken(ratsherr, 100));
            w.Ausfuehren(erpresser, new SpitzelAnsetzen(opfer.Inhaber));
            JahrBeenden(sim);
            var e = w.Ausfuehren(erpresser, new Erpressen(opfer.Inhaber, 500));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("mindestens 3"), e.Meldung);
            w.Ausfuehren(opfer, new Schenken(ratsherr, 100));
            w.Ausfuehren(erpresser, new SpitzelAnsetzen(opfer.Inhaber));
            JahrBeenden(sim);
            Pruefe.Gleich(3, w.Justiz.Beweise(erpresser, opfer).Count());

            Pruefe.Wahr(w.Ausfuehren(erpresser, new Erpressen(opfer.Inhaber, 500)).Erfolg, "Erpressen");
            Pruefe.Gleich(1, w.Justiz.Beweise(opfer, erpresser).Count()); // der Brief
            Pruefe.Gleich(Vergehen.Erpressung, w.Justiz.Beweise(opfer, erpresser).Single().Art);
            var vorher = (erpresser.Vermoegen, opfer.Vermoegen);
            Pruefe.Wahr(w.Ausfuehren(opfer, new ErpressungBeantworten(erpresser.Inhaber, true)).Erfolg, "Zahlen");
            Pruefe.Ungefaehr(500, erpresser.Vermoegen - vorher.Item1, 1e-9);
            Pruefe.Ungefaehr(500, vorher.Item2 - opfer.Vermoegen, 1e-9);
            Pruefe.Gleich(0, w.Justiz.Beweise(erpresser, opfer).Count());
            Pruefe.Wahr(!w.Ausfuehren(erpresser, new Anklagen(opfer.Inhaber)).Erfolg, "Nach dem Schweigegeld hat der Erpresser nichts mehr in der Hand");
        }),

        ("Erpressung: Unbeantwortet gilt der Brief bis Ende des Folgejahres, dann als abgelehnt", () =>
        {
            var sim = NeueSimulation(OhneZeugen.Value);
            var w = sim.Wirtschaft!;
            var erpresser = Haus(w, "Familie Erpresser");
            var opfer = Haus(w, "Familie Opfer");
            for (var i = 0; i < 3; i++) w.Ausfuehren(opfer, new Schenken(w.Rat.Ratsherren[i].Name, 100));
            w.Ausfuehren(erpresser, new SpitzelAnsetzen(opfer.Inhaber));
            JahrBeenden(sim);
            w.Ausfuehren(erpresser, new Erpressen(opfer.Inhaber, 300));
            JahrBeenden(sim);
            Pruefe.Wahr(w.Gauner.ForderungAn(opfer, erpresser) is not null, "Der Brief sollte noch gelten");
            JahrBeenden(sim);
            Pruefe.Wahr(w.Gauner.ForderungAn(opfer, erpresser) is null, "Der Brief sollte verfallen sein");
            Pruefe.Wahr(w.Ausfuehren(erpresser, new Anklagen(opfer.Inhaber)).Erfolg, "Nach dem Nein kann der Erpresser klagen");
        }),

        ("Kerker: Ein Überfall vor Gericht bringt ein Jahr Kerker – nur die Betriebe laufen weiter", () =>
        {
            var sim = NeueSimulation(MitZeugen.Value);
            var w = sim.Wirtschaft!;
            var raeuber = Haus(w, "Familie Räuber");
            var z = Haus(w, "Familie Grube");
            var eigene = w.BetriebEroeffnen("Gießerei Räuber", Betriebsart.Giesserei, raeuber);
            var mine = w.BetriebEroeffnen("Grube Opfer", Betriebsart.Mine, z);
            w.Einstellen(mine, "Bergmann", 3);
            w.Ausfuehren(z, new EskorteWaehlen(mine.Name, 0, "Keine"));
            for (var i = 0; i < 2; i++) w.Ausfuehren(raeuber, new Schenken(w.Rat.Ratsherren[i].Name, 100));
            w.Ausfuehren(raeuber, new Beauftragen(z.Inhaber, Gaunerstueck.Ueberfall));
            w.Ausfuehren(z, new SpitzelAnsetzen(raeuber.Inhaber));
            JahrBeenden(sim); // Zeuge beim Überfall + Spitzel findet die Geschenke
            if (w.Justiz.SpurenVon(raeuber).All(s => s.Art != Vergehen.Raub))
                JahrBeenden(sim);
            Pruefe.Wahr(w.Justiz.Beweise(z, raeuber).Any(b => b.Art == Vergehen.Raub), "Beweis für den Überfall fehlt");
            Pruefe.Wahr(w.Ausfuehren(z, new Anklagen(raeuber.Inhaber)).Erfolg, "Anklage");
            JahrBeenden(sim);
            Pruefe.Wahr(w.Justiz.LetzteUrteile.Single().Schuldig, w.Justiz.LetzteUrteile.Single().Text);
            Pruefe.Wahr(w.Justiz.ImKerker(raeuber), "Kerker");
            var e = w.Ausfuehren(raeuber, new Schenken(w.Rat.Ratsherren[0].Name, 100));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Kerker"), e.Meldung);
            Pruefe.Wahr(w.Ausfuehren(raeuber, new PersonalSetzen(eigene.Name, "Geselle", 1)).Erfolg, "Betriebsbefehle laufen weiter");
            JahrBeenden(sim);
            Pruefe.Wahr(!w.Justiz.ImKerker(raeuber), "Nach einem Jahr wieder frei");
        }),

        ("KI wagemutig: heuert gegen das führende Haus Banden an; die KI stellt nach einem Angriff Wächter ein", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 7 },
                Daten.Value, new NeuePartie(Rivale: KiCharakter.Wagemutig));
            var w = sim.Wirtschaft!;
            var rivale = w.Konten[1];
            var auftraege = 0;
            w.Meldung += m => { if (m.StartsWith(rivale.Inhaber + ": heuert im Hinterzimmer")) auftraege++; };
            for (var i = 0; i < 6; i++) JahrBeenden(sim);
            Pruefe.Wahr(auftraege > 0, "Der wagemutige Rivale sollte in 6 Jahren Banden angeheuert haben");

            // Der Spieler bricht beim Rivalen ein – danach stellt die KI einen Wächter ein.
            var giesserei = w.Betriebe.First(b => b.Konto == rivale && b.IstWerkstatt);
            w.Ausfuehren(spieler, new Beauftragen(rivale.Inhaber, Gaunerstueck.Diebstahl, giesserei.Name));
            JahrBeenden(sim);
            JahrBeenden(sim);
            Pruefe.Wahr(giesserei.Waechter, "Die KI sollte nach dem Einbruch einen Wächter eingestellt haben");
        }),

        ("KI vorsichtig: heuert nie eine Bande an", () =>
        {
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 7 },
                Daten.Value, new NeuePartie(Rivale: KiCharakter.Vorsichtig));
            var w = sim.Wirtschaft!;
            for (var i = 0; i < 6; i++) JahrBeenden(sim);
            Pruefe.Gleich(0, w.Justiz.SpurenVon(w.Konten[1]).Count(s => s.Art != Vergehen.Bestechung));
        }),

        ("KI als Erpressungsopfer: zahlt, wenn das Schweigegeld billiger ist als die Strafe, sonst nicht", () =>
        {
            foreach (var (betrag, zahlt) in new[] { (300.0, true), (50_000.0, false) })
            {
                var sim = NeueSimulation(OhneZeugen.Value);
                var w = sim.Wirtschaft!;
                var erpresser = Haus(w, "Familie Erpresser");
                var ki = Haus(w, "Haus Opfer");
                for (var i = 0; i < 3; i++) w.Ausfuehren(ki, new Schenken(w.Rat.Ratsherren[i].Name, 200));
                w.Ausfuehren(erpresser, new SpitzelAnsetzen(ki.Inhaber));
                JahrBeenden(sim);
                w.KiAnmelden(ki, KiProfil.Standard with { Gaunerei = 0 });
                w.Ausfuehren(erpresser, new Erpressen(ki.Inhaber, betrag));
                var vorher = erpresser.Vermoegen;
                while (sim.Zeit.MinuteImTag < 8 * 60) sim.Tick(); // die KI überlegt einmal am Tag
                Pruefe.Gleich(zahlt, erpresser.Vermoegen > vorher);
                Pruefe.Wahr(w.Gauner.ForderungAn(ki, erpresser) is null, "Die KI sollte geantwortet haben");
            }
        }),

        ("Speichern: Aufträge, Hinterhalte, Briefe, Wächter, Sabotage und Kerker überstehen Speichern und Laden", () =>
        {
            var sim = NeueSimulation(OhneZeugen.Value);
            var w = sim.Wirtschaft!;
            var a = Haus(w, "Familie Täter");
            var z = Haus(w, "Familie Opfer");
            var b = w.BetriebEroeffnen("Gießerei Opfer", Betriebsart.Giesserei, z);
            w.BetriebEroeffnen("Grube Opfer", Betriebsart.Mine, z);
            w.Ausfuehren(z, new WaechterSetzen(b.Name, true));
            w.Ausfuehren(a, new Beauftragen(z.Inhaber, Gaunerstueck.Sabotage, b.Name));
            w.Ausfuehren(a, new Beauftragen(z.Inhaber, Gaunerstueck.Ueberfall));
            BisZurNacht(sim);
            w.Ausfuehren(a, new Beauftragen(z.Inhaber, Gaunerstueck.Diebstahl, b.Name));
            var (kopie, _) = Speicherung.Laden(Speicherung.Speichern(sim, a));
            var w2 = kopie.Wirtschaft!;
            var b2 = w2.Betriebe.First(x => x.Name == b.Name);
            Pruefe.Gleich(true, b2.Waechter);
            Pruefe.Gleich(b.SabotageMinuten, b2.SabotageMinuten);
            Pruefe.Gleich(w.Gauner.Hinterhalte.Count, w2.Gauner.Hinterhalte.Count);
            Pruefe.Gleich(1, w2.Gauner.Auftraege.Count);
            Pruefe.Gleich(w.Gauner.LetzterAngriff(b.Name), w2.Gauner.LetzterAngriff(b.Name));
            Pruefe.Gleich(string.Join("|", w.Gauner.NachrichtenFuer(a, false)), string.Join("|", w2.Gauner.NachrichtenFuer(w2.Konten[0], false)));
        }),
    };
}
