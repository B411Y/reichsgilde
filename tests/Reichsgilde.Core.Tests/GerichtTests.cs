using Reichsgilde.Core;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für das Stadtgericht: Spuren, Spitzel, Anklage, Urteil, Strafen und die KI vor Gericht.</summary>
static class GerichtTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    /// <summary>Wie die echten Daten, aber der Spitzel findet jede Spur – so hängen die Tests nicht am Zufall.</summary>
    static readonly Lazy<Wirtschaftsdaten> SichererSpitzel = new(() =>
        Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte(File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"spitzelChance\": 0.5", "\"spitzelChance\": 1.0"))));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null, int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert },
            wirtschaftsdaten: daten ?? SichererSpitzel.Value);

    static Konto Haus(Stadtwirtschaft w, string name, int stand = 1, double vermoegen = 50_000)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 1_000_000);
        for (var i = 0; i < stand; i++)
            Pruefe.Wahr(w.Ausfuehren(k, new StandErwerben()).Erfolg, "Aufstieg fehlgeschlagen");
        k.Schaden(k.Vermoegen - vermoegen);
        return k;
    }

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Täter besticht n Ratsherren ohne Amt (also keine Richter), Kläger setzt einen Spitzel an, das Jahr endet.</summary>
    static void BeweiseSammeln(Simulation sim, Konto klaeger, Konto taeter, int bestechungen, double betrag = 200)
    {
        var w = sim.Wirtschaft!;
        var nachruecker = w.Rat.Ratsherren.Where(r => w.Rat.Aemter.All(a => a.Ratsherr != r)).ToList();
        for (var i = 0; i < bestechungen; i++)
            Pruefe.Wahr(w.Ausfuehren(taeter, new Schenken(nachruecker[i % nachruecker.Count].Name, betrag)).Erfolg, "Geschenk");
        Pruefe.Wahr(w.Ausfuehren(klaeger, new SpitzelAnsetzen(taeter.Inhaber)).Erfolg, "Spitzel");
        JahrBeenden(sim);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Gericht: Jedes Geschenk an einen Ratsherrn hinterlässt eine Spur – Brautgeschenke nicht", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            Pruefe.Wahr(w.Justiz.IstAktiv, "kein Gericht");
            var k = Haus(w, "Familie Spur");
            w.Ausfuehren(k, new Schenken(w.Rat.Ratsherren[0].Name, 300));
            var spur = w.Justiz.SpurenVon(k).Single();
            Pruefe.Gleich(Vergehen.Bestechung, spur.Art);
            Pruefe.Gleich(w.Rat.Ratsherren[0].Name, spur.Betroffener);
            Pruefe.Gleich(300.0, spur.Betrag);
            Pruefe.Gleich(1400, spur.Jahr);
            Pruefe.Gleich(0, w.Justiz.BekannteSpuren(k));
        }),

        ("Spitzel: kostet Gulden, nur einer je Haus und Jahr, deckt am Jahresende Spuren auf (Chance 50 %)", () =>
        {
            var sim = NeueSimulation(Daten.Value, startwert: 3);
            var w = sim.Wirtschaft!;
            var klaeger = Haus(w, "Familie Klage");
            var taeter = Haus(w, "Familie Tat");
            var e = w.Ausfuehren(klaeger, new SpitzelAnsetzen(klaeger.Inhaber));
            Pruefe.Wahr(!e.Erfolg, "Spitzel auf sich selbst");
            for (var i = 0; i < 40; i++) w.Ausfuehren(taeter, new Schenken(w.Rat.Ratsherren[i % 7].Name, 20));
            var vorher = klaeger.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(klaeger, new SpitzelAnsetzen(taeter.Inhaber)).Erfolg, "Spitzel");
            Pruefe.Gleich(300.0, vorher - klaeger.Vermoegen);
            Pruefe.Gleich(300.0, klaeger.Laufend.Gericht);
            Pruefe.Wahr(!w.Ausfuehren(klaeger, new SpitzelAnsetzen(taeter.Inhaber)).Erfolg, "zweiter Spitzel im selben Jahr");
            JahrBeenden(sim);
            var gefunden = w.Justiz.Beweise(klaeger, taeter).Count();
            Pruefe.Wahr(gefunden is >= 12 and <= 28, $"{gefunden} von 40 Spuren gefunden (erwartet ≈ 20)");
            Pruefe.Gleich(gefunden, w.Justiz.BekannteSpuren(taeter));
            Pruefe.Wahr(w.Justiz.LetzterGerichtstag.Any(t => t.Contains("Spitzel")), "Spitzelbericht fehlt");
        }),

        ("Anklage: nur mit Beweisen; Gebühr; Vorsitz hat der Stadtrichter, zwei Schöffen aus den Ratsherren ohne Amt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var klaeger = Haus(w, "Familie Klage");
            var taeter = Haus(w, "Familie Tat");
            var e = w.Ausfuehren(klaeger, new Anklagen(taeter.Inhaber));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Ohne Beweise"), e.Meldung);
            BeweiseSammeln(sim, klaeger, taeter, 2);
            Pruefe.Gleich(2, w.Justiz.Beweise(klaeger, taeter).Count());
            var vorher = klaeger.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(klaeger, new Anklagen(taeter.Inhaber)).Erfolg, "Anklage");
            Pruefe.Gleich(100.0, vorher - klaeger.Vermoegen);
            var v = w.Justiz.VerfahrenGegen(klaeger, taeter)!;
            Pruefe.Gleich(w.Rat.AmtNamens("Stadtrichter")!.Ratsherr, v.Richter[0].Ratsherr);
            Pruefe.Gleich(3, v.Richter.Count);
            Pruefe.Wahr(v.Richter.Skip(1).All(r => r.Ratsherr is { } s && w.Rat.Aemter.All(a => a.Ratsherr != s)), "Schöffen haben ein Amt");
            Pruefe.Wahr(!w.Ausfuehren(klaeger, new Anklagen(taeter.Inhaber)).Erfolg, "doppelte Anklage");
        }),

        ("Schuldig: Buße, Amtsverlust vor der Ratssitzung, Pranger kostet Ansehen, bestochene Ratsherren rücken ab", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var klaeger = Haus(w, "Familie Klage");
            var taeter = Haus(w, "Familie Tat");
            BeweiseSammeln(sim, klaeger, taeter, 2, 400);
            // Der Täter sitzt inzwischen im Rat: Er hat das Amt des Zöllners gewonnen.
            var zoellner = w.Rat.AmtNamens("Zöllner")!;
            zoellner.Ratsherr = null;
            zoellner.Konto = taeter;
            var ratsherr = w.Rat.RatsherrNamens(w.Justiz.Beweise(klaeger, taeter).First().Betroffener)!;
            Pruefe.Wahr(w.Rat.Gunst(ratsherr, taeter) > 0, "Gunst vor dem Urteil");
            var ansehenVorher = w.Rat.Ansehen(taeter);

            w.Ausfuehren(klaeger, new Anklagen(taeter.Inhaber));
            var v = w.Justiz.VerfahrenGegen(klaeger, taeter)!;
            var prognose = w.Justiz.Prognose(v);
            Pruefe.Wahr(Stadtgericht.IstSchuldig(prognose), "Prognose: schuldig");
            var vorher = taeter.Vermoegen;
            JahrBeenden(sim);

            var urteil = w.Justiz.LetzteUrteile.Single();
            Pruefe.Wahr(urteil.Schuldig, urteil.Text);
            Pruefe.Ungefaehr(2400, taeter.Vorjahr!.Gericht, 1e-9); // 3 × 800
            Pruefe.Wahr(vorher - taeter.Vermoegen >= 2400 - 1, "Buße bezahlt");
            Pruefe.Wahr(w.Rat.AmtVon(taeter) is null, "Amt sollte verloren sein");
            Pruefe.Wahr(zoellner.Ratsherr is not null, "Ein Nachrücker sollte das Amt übernehmen");
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(t => t.Contains("rückt als Zöllner")), "Nachrücker fehlt im Protokoll");
            Pruefe.Gleich(1404, w.Justiz.PrangerBis(taeter)); // verurteilt am Gerichtstag 1401, am Pranger 1402–1404
            var ohneSchande = Math.Min(40, taeter.Stand * 8 + taeter.Vermoegen / 1000 * 0.4); // die Buße senkt auch das Vermögen
            Pruefe.Ungefaehr(ohneSchande - 10, w.Rat.Ansehen(taeter), 1e-9);
            Pruefe.Wahr(w.Rat.Ansehen(taeter) < ansehenVorher - 10, "Pranger");
            Pruefe.Gleich(0.0, w.Rat.Gunst(ratsherr, taeter)); // der bestochene Ratsherr ist abgerückt
            Pruefe.Gleich(0, w.Justiz.Beweise(klaeger, taeter).Count()); // verhandelte Taten sind erledigt
            for (var i = 0; i < 3; i++) JahrBeenden(sim);
            Pruefe.Wahr(w.Justiz.PrangerBis(taeter) is null, "Pranger sollte nach 3 Jahren enden");
        }),

        ("Freispruch: Wer die Richter beschenkt, kommt davon – der Ankläger zahlt Entschädigung", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var klaeger = Haus(w, "Familie Klage");
            var taeter = Haus(w, "Familie Tat");
            BeweiseSammeln(sim, klaeger, taeter, 2);
            w.Ausfuehren(klaeger, new Anklagen(taeter.Inhaber));
            var v = w.Justiz.VerfahrenGegen(klaeger, taeter)!;
            // 20 Beweiskraft, Schwelle 15: Jeder Richter braucht 12 Gunst mehr für den Täter (½ × 12 = 6) → 240 Gulden.
            foreach (var r in v.Richter.Take(2))
                w.Ausfuehren(taeter, new Schenken(r.Ratsherr!.Name, 240));
            Pruefe.Wahr(!Stadtgericht.IstSchuldig(w.Justiz.Prognose(v)), "Prognose sollte Freispruch sein");
            var klaegerVorher = klaeger.Vermoegen;
            JahrBeenden(sim);
            Pruefe.Wahr(!w.Justiz.LetzteUrteile.Single().Schuldig, "Freispruch erwartet");
            Pruefe.Ungefaehr(200, klaeger.Vorjahr!.Gericht - 100, 1e-9); // Gebühr 100 + Entschädigung 200 (der Spitzel lief im Vorjahr)
            Pruefe.Ungefaehr(-200, taeter.Vorjahr!.Gericht, 1e-9);
            // Die Richter-Geschenke sind selbst Bestechung – neue Spuren, die noch niemand kennt.
            Pruefe.Gleich(2, w.Justiz.SpurenVon(taeter).Count());
        }),

        ("Ist der Stadtrichter selbst Partei, führt der Bürgermeister den Vorsitz", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var klaeger = Haus(w, "Familie Klage");
            var taeter = Haus(w, "Familie Tat");
            var richteramt = w.Rat.AmtNamens("Stadtrichter")!;
            richteramt.Ratsherr = null;
            richteramt.Konto = taeter;
            Pruefe.Gleich(w.Rat.AmtNamens("Bürgermeister")!.Ratsherr, w.Justiz.VorsitzFuer(klaeger, taeter)!.Ratsherr);
        }),

        ("Verjährung: Nach fünf Jahren verfallen Spuren und Beweise", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var klaeger = Haus(w, "Familie Klage");
            var taeter = Haus(w, "Familie Tat");
            BeweiseSammeln(sim, klaeger, taeter, 1);
            for (var i = 0; i < 3; i++) JahrBeenden(sim);
            Pruefe.Gleich(1, w.Justiz.Beweise(klaeger, taeter).Count()); // Gerichtstag 1403: Tat von 1400 noch verwertbar
            JahrBeenden(sim);
            Pruefe.Gleich(0, w.Justiz.SpurenVon(taeter).Count()); // nach dem Gerichtstag 1404 verjährt
        }),

        ("KI: Haus Eisenbart setzt Spitzel auf einen Bürger an und klagt mit genug Beweisen", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                SichererSpitzel.Value, new NeuePartie(Startvermoegen: 30_000));
            var w = sim.Wirtschaft!;
            var rivale = w.Konten[1];
            w.Ausfuehren(spieler, new StandErwerben());
            var nachruecker = w.Rat.Ratsherren.Where(r => w.Rat.Aemter.All(a => a.Ratsherr != r)).ToList();
            for (var i = 0; i < 2; i++)
                w.Ausfuehren(spieler, new Schenken(nachruecker[i].Name, 300));
            JahrBeenden(sim); // die KI setzt einen Spitzel an, er findet beide Geschenke
            Pruefe.Gleich(2, w.Justiz.Beweise(rivale, spieler).Count());
            JahrBeenden(sim); // zwei Beweise reichen der KI nicht – Bestechung könnte das Urteil noch kippen
            Pruefe.Wahr(w.Justiz.LetzteUrteile.Count == 0, "Mit zwei Beweisen sollte die KI noch nicht klagen");
            w.Ausfuehren(spieler, new Schenken(nachruecker[2].Name, 300));
            JahrBeenden(sim); // der Spitzel findet das dritte Geschenk
            Pruefe.Gleich(3, w.Justiz.Beweise(rivale, spieler).Count());
            JahrBeenden(sim); // jetzt klagt sie an – verhandelt wird am Jahresende
            Pruefe.Wahr(w.Justiz.LetzteUrteile.Any(u => u.Anklaeger == rivale && u.Beklagter == spieler && u.Schuldig),
                "Die KI sollte den Spieler verurteilen lassen: " + string.Join(" | ", w.Justiz.LetzterGerichtstag));

        }),

        ("KI: Droht ein Schuldspruch, beschenkt sie die billigsten Richter – bis zum Freispruch", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var klaeger = Haus(w, "Familie Klage");
            var ki = Haus(w, "Haus Angeklagt", vermoegen: 20_000);
            // Hohe Bestechung → hohe drohende Buße (2400). Dann lohnt die Verteidigung auch unter Beobachtung:
            // ≈ 440 Gulden für zwei Richter × Risiko 4 (Spitzel findet in diesen Testdaten jede Spur) < 2400.
            BeweiseSammeln(sim, klaeger, ki, 2, 400);
            Pruefe.Wahr(w.Ausfuehren(klaeger, new Anklagen(ki.Inhaber)).Erfolg, "Anklage");
            var v = w.Justiz.VerfahrenGegen(klaeger, ki)!;
            Pruefe.Wahr(Stadtgericht.IstSchuldig(w.Justiz.Prognose(v)) || v.Richter.Skip(1).Any(r => w.Rat.Gunst(r.Ratsherr!, ki) > 0),
                "Ausgangslage: Schuldspruch droht");
            var schuldig = Stadtgericht.IstSchuldig(w.Justiz.Prognose(v));
            w.KiAnmelden(ki);
            while (sim.Zeit.MinuteImTag < 8 * 60) sim.Tick(); // die KI überlegt einmal am Tag (6. Stunde)
            if (schuldig)
                Pruefe.Wahr(ki.Laufend.Geschenke > 0, "Die KI sollte Richter beschenkt haben");
            Pruefe.Wahr(!Stadtgericht.IstSchuldig(w.Justiz.Prognose(v)), "Nach den Geschenken sollte die Prognose Freispruch sein");
            JahrBeenden(sim);
            Pruefe.Wahr(!w.Justiz.LetzteUrteile.Single().Schuldig, "Freispruch erwartet");
        }),

        ("Speichern: Spuren, Beweise, Spitzel, Verfahren und Pranger überstehen Speichern und Laden", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var klaeger = Haus(w, "Familie Klage");
            var taeter = Haus(w, "Familie Tat");
            BeweiseSammeln(sim, klaeger, taeter, 3);
            w.Ausfuehren(klaeger, new Anklagen(taeter.Inhaber));
            w.Ausfuehren(klaeger, new SpitzelAnsetzen(taeter.Inhaber));
            w.Ausfuehren(taeter, new Schenken(w.Rat.Ratsherren[0].Name, 50));
            var (kopie, _) = Speicherung.Laden(Speicherung.Speichern(sim, klaeger));
            var a = w.Justiz;
            var b = kopie.Wirtschaft!.Justiz;
            var (k2, t2) = (kopie.Wirtschaft.Konten[0], kopie.Wirtschaft.Konten[1]);
            Pruefe.Gleich(a.SpurenVon(taeter).Count(), b.SpurenVon(t2).Count());
            Pruefe.Gleich(a.Beweise(klaeger, taeter).Count(), b.Beweise(k2, t2).Count());
            Pruefe.Gleich(true, b.HatSpitzel(k2, t2));
            var v1 = a.VerfahrenGegen(klaeger, taeter)!;
            var v2 = b.VerfahrenGegen(k2, t2)!;
            Pruefe.Gleich(string.Join(",", v1.Richter.Select(r => r.Name)), string.Join(",", v2.Richter.Select(r => r.Name)));
            JahrBeenden(sim);
            JahrBeenden(kopie);
            Pruefe.Gleich(a.LetzteUrteile.Single().Text, b.LetzteUrteile.Single().Text);
            Pruefe.Gleich(a.PrangerBis(taeter), b.PrangerBis(t2));
        }),
    };
}
