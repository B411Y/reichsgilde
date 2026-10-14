using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für den Stadtrat: Laufbahn, Wahlen, Gunst, Amtseinkommen und Abstimmungen über das Statut.</summary>
static class RatTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);

    /// <summary>Ein Haus im gewünschten Stand mit dem gewünschten Vermögen.</summary>
    static Konto Haus(Stadtwirtschaft w, string name, int stand, double vermoegen)
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

    /// <summary>Beschenkt so viele Wähler eines Amtes, dass das Haus sicher gewinnt.</summary>
    static void MehrheitKaufen(Stadtwirtschaft w, Konto k, string amt, double jeRatsherr = 1000)
    {
        foreach (var sitz in w.Rat.Waehler(w.Rat.AmtNamens(amt)!).Where(s => s.Ratsherr is not null))
            Pruefe.Wahr(w.Ausfuehren(k, new Schenken(sitz.Ratsherr!.Name, jeRatsherr)).Erfolg, "Geschenk abgelehnt");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Rat: sieben Ämter in vier Stufen, zu Beginn alle mit Ratsherren besetzt", () =>
        {
            var rat = NeueSimulation().Wirtschaft!.Rat;
            Pruefe.Wahr(rat.IstAktiv, "Rat nicht aktiv");
            Pruefe.Gleich(7, rat.Aemter.Count);
            Pruefe.Wahr(rat.Aemter.All(a => a.Ratsherr is not null), "unbesetzte Ämter");
            Pruefe.Gleich("Ulrich Wendelstein", rat.AmtNamens("Bürgermeister")!.Inhaber);
            Pruefe.Gleich("Marktmeister", rat.AmtNamens("Zöllner")!.Daten.Vorgesetzt);
            Pruefe.Gleich(4, rat.Daten.HoechsteStufe);
        }),

        ("Laufbahn: ab Bürger, unten beginnen, eine Bewerbung pro Jahr", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var gemeiner = Haus(w, "Gemeiner", 0, 50_000);
            var e = w.Ausfuehren(gemeiner, new AmtBewerben("Zöllner"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("mindestens den Stand Bürger"), e.Meldung);

            var buerger = Haus(w, "Bürger", 1, 50_000);
            e = w.Ausfuehren(buerger, new AmtBewerben("Marktmeister"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Laufbahn beginnt unten"), e.Meldung);
            Pruefe.Wahr(w.Ausfuehren(buerger, new AmtBewerben("Zöllner")).Erfolg, "Bewerbung abgelehnt");
            e = w.Ausfuehren(buerger, new AmtBewerben("Gefängniswärter"));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("Nur eine Bewerbung pro Jahr"), e.Meldung);
            Pruefe.Gleich("Gefängniswärter, Zöllner", string.Join(", ", w.Rat.WaehlbareAemter(buerger).Select(a => a.Name).OrderBy(n => n)), "wählbar");
        }),

        ("Wahl: Ohne Gunst gewinnt der Ratsherr, mit Geschenken an die Mehrheit das Haus", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var ohne = Haus(w, "Ohne Geschenke", 1, 20_000);
            w.Ausfuehren(ohne, new AmtBewerben("Zöllner"));
            JahrBeenden(sim);
            Pruefe.Gleich("Hinnerk Tannenkamp", w.Rat.AmtNamens("Zöllner")!.Inhaber);
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(s => s.Contains("bleibt im Amt")), "Wahl nicht protokolliert");

            var mit = Haus(w, "Mit Geschenken", 1, 20_000);
            w.Ausfuehren(mit, new AmtBewerben("Zöllner"));
            MehrheitKaufen(w, mit, "Zöllner");
            Pruefe.Ungefaehr(1000 * 5, mit.Laufend.Geschenke, 1e-9); // 5 Wähler: alle Ämter der Stufen 2–4
            JahrBeenden(sim);
            Pruefe.Gleich(mit, w.Rat.AmtNamens("Zöllner")!.Konto);
            Pruefe.Gleich(w.Rat.AmtNamens("Zöllner"), w.Rat.AmtVon(mit));
        }),

        ("Wer aufsteigt, gibt sein altes Amt ab – ein Nachrücker übernimmt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Aufsteiger", 1, 50_000);
            w.Ausfuehren(k, new AmtBewerben("Zöllner"));
            MehrheitKaufen(w, k, "Zöllner");
            JahrBeenden(sim);
            w.Ausfuehren(k, new AmtBewerben("Marktmeister"));
            MehrheitKaufen(w, k, "Marktmeister");
            JahrBeenden(sim);
            Pruefe.Gleich("Marktmeister", w.Rat.AmtVon(k)!.Name);
            var zoellner = w.Rat.AmtNamens("Zöllner")!;
            Pruefe.Wahr(zoellner.Ratsherr is not null, "Zöllner unbesetzt");
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(s => s.Contains("rückt als Zöllner")), "Nachrücker nicht gemeldet");
        }),

        ("Amtseinkommen am Jahresende, getrennt vom Betriebsgewinn", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Zöllnerhaus", 1, 20_000);
            w.Ausfuehren(k, new AmtBewerben("Zöllner"));
            MehrheitKaufen(w, k, "Zöllner");
            JahrBeenden(sim); // gewählt
            JahrBeenden(sim); // ein Jahr im Amt
            Pruefe.Ungefaehr(150, k.Vorjahr!.Amtseinkommen, 1e-9);
            Pruefe.Ungefaehr(0, k.Vorjahr.Gewinn, 1e-9);
        }),

        ("Gunst: Geschenke wirken, sind gedeckelt und verblassen jedes Jahr", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Schenker", 0, 10_000);
            var r = w.Rat.RatsherrNamens("Dorla Kupferbart")!;
            w.Ausfuehren(k, new Schenken(r.Name, 200));
            Pruefe.Ungefaehr(10, w.Rat.Gunst(r, k), 1e-9); // 200 × 0,05
            w.Ausfuehren(k, new Schenken(r.Name, 5000));
            Pruefe.Ungefaehr(100, w.Rat.Gunst(r, k), 1e-9); // Höchstwert
            JahrBeenden(sim);
            Pruefe.Ungefaehr(70, w.Rat.Gunst(r, k), 1e-9);  // −30 %
            Pruefe.Wahr(!w.Ausfuehren(k, new Schenken("Graf Dracula", 10)).Erfolg, "unbekannter Ratsherr angenommen");
        }),

        ("Antrag: nur Ratsmitglieder, Ratsherren stimmen nach Haltung – Gunst öffnet das Nachbarstatut", () =>
        {
            var sim = NeueSimulation(); // Statut B, Rat: 3× A, 3× B, 1× C
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Reformer", 1, 50_000);
            Pruefe.WirftMitMeldung(() => throw new Exception(w.Ausfuehren(k, new StatutBeantragen(Fronstatut.FreieStadt)).Meldung), "nur Mitglieder des Rates");

            w.Ausfuehren(k, new AmtBewerben("Zöllner"));
            MehrheitKaufen(w, k, "Zöllner");
            JahrBeenden(sim);
            // Ohne Gunst: A-Ratsherren (3) + eigener Sitz (1) = 4 von 7 → angenommen
            Pruefe.Wahr(w.Ausfuehren(k, new StatutBeantragen(Fronstatut.FreieStadt)).Erfolg, "Antrag abgelehnt");
            Pruefe.Wahr(!w.Ausfuehren(k, new StatutBeantragen(Fronstatut.Leibeigenschaft)).Erfolg, "zweiter Antrag angenommen");
            JahrBeenden(sim);
            Pruefe.Gleich(Fronstatut.FreieStadt, w.Statut);
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(s => s.Contains("ANGENOMMEN")), "Abstimmung nicht protokolliert");

            // Zurück zu C (zwei Stufen von A entfernt): A-Ratsherren lehnen trotz Gunst ab.
            var r = w.Rat.RatsherrNamens("Ulrich Wendelstein")!;
            Pruefe.Wahr(!w.Rat.StimmtZu(r, Fronstatut.Leibeigenschaft, k), "A-Ratsherr für C");
            w.Ausfuehren(k, new Schenken(r.Name, 2000));
            Pruefe.Wahr(!w.Rat.StimmtZu(r, Fronstatut.Leibeigenschaft, k), "Gunst sollte zwei Stufen nicht überbrücken");
            Pruefe.Wahr(w.Rat.StimmtZu(r, Fronstatut.Bestienfron, k), "Gunst sollte eine Stufe überbrücken");
        }),

        ("Bankrott kostet das Amt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Pleite", 1, 20_000);
            w.Ausfuehren(k, new AmtBewerben("Zöllner"));
            MehrheitKaufen(w, k, "Zöllner");
            JahrBeenden(sim);
            k.Schaden(k.Vermoegen + 100_000);
            for (var i = 0; i < w.Daten.Schulden.JahreBisBankrott; i++) JahrBeenden(sim);
            Pruefe.Gleich(true, k.IstBankrott);
            Pruefe.Gleich<Amt?>(null, w.Rat.AmtVon(k));
            Pruefe.Wahr(w.Rat.AmtNamens("Zöllner")!.Ratsherr is not null, "Sitz nicht neu besetzt");
        }),

        ("KI: stimmt für ein Statut nur bei deutlichem Vorteil – Orks in der Grube: gegen A", () =>
        {
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie());
            var ki = sim.Wirtschaft!.Rivalen.First(r => r.Konto.Inhaber == Partieaufbau.Rivalenname); // Haus Eisenbart mit Grube
            Pruefe.Wahr(ki.ErwartungUnter(Fronstatut.Bestienfron) > ki.ErwartungUnter(Fronstatut.FreieStadt), "Orks sollten unter B mehr bringen");
            Pruefe.Gleich(false, ki.StimmtFuerStatut(Fronstatut.FreieStadt));
        }),

        ("KI: bewirbt sich als Bürger und kauft sich eine Mehrheit, wenn das Budget reicht", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "KI-Haus", 1, 15_000); // zu wenig für den Patrizier – sonst reicht ihr Ansehen ohne Geschenke
            w.KiAnmelden(k);
            for (var i = 0; i < 8 * 60 + 1; i++) sim.Tick(); // über die Politikstunde hinweg
            Pruefe.Wahr(w.Rat.Bewerbungen.ContainsKey(k), "keine Bewerbung");
            Pruefe.Wahr(k.Laufend.Geschenke > 0, "keine Geschenke");
            JahrBeenden(sim);
            Pruefe.Wahr(w.Rat.AmtVon(k) is not null, "nicht gewählt: " + string.Join(" | ", w.Rat.LetzteSitzung));
        }),

        ("Speichern: Ämter, Gunst, Bewerbungen und Antrag überstehen Speichern und Laden", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Spieler", 1, 50_000);
            w.Ausfuehren(k, new AmtBewerben("Zöllner"));
            MehrheitKaufen(w, k, "Zöllner", 300);
            JahrBeenden(sim);
            w.Ausfuehren(k, new StatutBeantragen(Fronstatut.Leibeigenschaft));
            w.Ausfuehren(k, new AmtBewerben("Marktmeister"));
            w.Ausfuehren(k, new Schenken("Dorla Kupferbart", 400));

            var (geladen, spieler) = Speicherung.Laden(Speicherung.Speichern(sim, k));
            var rat = geladen.Wirtschaft!.Rat;
            Pruefe.Gleich("Spieler", rat.AmtNamens("Zöllner")!.Konto?.Inhaber);
            Pruefe.Gleich("Marktmeister", rat.Bewerbungen[spieler!]);
            Pruefe.Gleich(Fronstatut.Leibeigenschaft, rat.Antrag!.Value.Statut);
            Pruefe.Ungefaehr(w.Rat.Gunst(w.Rat.RatsherrNamens("Dorla Kupferbart")!, k),
                             rat.Gunst(rat.RatsherrNamens("Dorla Kupferbart")!, spieler!), 1e-9);
            Pruefe.Gleich(string.Join("|", w.Rat.LetzteSitzung), string.Join("|", rat.LetzteSitzung));
        }),

        ("Prognose: zeigt vor der Wahl, wie viele Stimmen sicher sind – und trifft zu", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Rechner", 1, 20_000);
            w.Ausfuehren(k, new AmtBewerben("Zöllner"));
            var zoellner = w.Rat.AmtNamens("Zöllner")!;
            Pruefe.Gleich((0, 0, 5), w.Rat.WahlPrognose(zoellner, k));
            var waehler = w.Rat.Waehler(zoellner).Select(s => s.Ratsherr!).Take(3).ToList();
            foreach (var r in waehler) w.Ausfuehren(k, new Schenken(r.Name, 1000));
            Pruefe.Gleich((3, 0, 5), w.Rat.WahlPrognose(zoellner, k));
            JahrBeenden(sim);
            Pruefe.Gleich(k, zoellner.Konto);

            var (ja, nein, _, _) = w.Rat.AbstimmungsPrognose(k, Fronstatut.FreieStadt);
            // 3 A-Ratsherren + eigener Sitz + Thorgrim (Haltung B, aber dank Geschenk genug Gunst für das Nachbarstatut A)
            Pruefe.Gleich((5, 2), (ja, nein));
        }),

        ("Daten: Orks und Oger können nicht im Rat sitzen", () =>
        {
            var json = File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"volk\": \"Zwerg\"", "\"volk\": \"Ork\"");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json), "kein Bürgerrecht");
        }),
    };
}
