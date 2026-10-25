using Reichsgilde.Core;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;
using static StadtkasseTests;

/// <summary>
/// Tests für die KI rund um Stadtkasse und Ämter (PR #200): Sie stimmt nach ihren Interessen über Steuer, Bau und Fest ab,
/// stellt eigene Anträge, wenn sie durchgingen, und nutzt die Befugnisse ihres Amtes.
/// </summary>
static class KiStadtkasseTests
{
    /// <summary>Bis nach der Politikstunde der KI im laufenden Jahr (sechste volle Stunde, 11 Uhr).</summary>
    static void BisNachPolitik(Simulation sim)
    {
        while (sim.Zeit.UhrzeitInMinuten < 11 * 60 + 5) sim.Tick();
    }

    static (Simulation Sim, Stadtwirtschaft W, Konto Ki) Aufbau(string amt)
    {
        var sim = NeueSimulation();
        var w = sim.Wirtschaft!;
        var ki = Haus(w, "KI-Haus", 50_000);
        w.KiAnmelden(ki);
        Sitz(w, amt, ki);
        return (sim, w, ki);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Stimme: Steuer runter ja, rauf nein (außer die Kasse ist im Minus); Bau, wenn er ihr nützt; Fest mit Werkstatt, nie für das führende Haus", () =>
        {
            var (_, w, ki) = Aufbau("Zöllner");
            var anderer = Haus(w, "Anderer", 10_000);
            var reich = Haus(w, "Reich", 1_000_000);
            var r = w.Rivalen.Single();
            KasseAuf(w, 20_000);
            Pruefe.Wahr(r.StimmtFuerRatsantrag(new Ratsantrag(Antragsart.Steuersatz, anderer, Prozent: 4)), "runter");
            Pruefe.Wahr(!r.StimmtFuerRatsantrag(new Ratsantrag(Antragsart.Steuersatz, anderer, Prozent: 6)), "rauf");
            Pruefe.Wahr(!r.StimmtFuerRatsantrag(new Ratsantrag(Antragsart.Bauvorhaben, anderer, Bauvorhaben: "marktplatz")), "ohne Werkstatt");
            Pruefe.Wahr(!r.StimmtFuerRatsantrag(new Ratsantrag(Antragsart.Fest, anderer)), "Fest ohne Werkstatt");
            w.BetriebEroeffnen("KI-Gießerei", Betriebsart.Giesserei, ki);
            Pruefe.Wahr(r.StimmtFuerRatsantrag(new Ratsantrag(Antragsart.Bauvorhaben, anderer, Bauvorhaben: "marktplatz")), "Marktplatz");
            Pruefe.Wahr(r.StimmtFuerRatsantrag(new Ratsantrag(Antragsart.Bauvorhaben, anderer, Bauvorhaben: "mauer")), "Mauer");
            Pruefe.Wahr(!r.StimmtFuerRatsantrag(new Ratsantrag(Antragsart.Bauvorhaben, anderer, Bauvorhaben: "hafen")), "ohne Schiffe");
            Pruefe.Wahr(r.StimmtFuerRatsantrag(new Ratsantrag(Antragsart.Fest, anderer)), "Fest");
            Pruefe.Wahr(!r.StimmtFuerRatsantrag(new Ratsantrag(Antragsart.Fest, reich)), "nicht für das führende Haus");
            KasseAuf(w, 1000);
            Pruefe.Wahr(!r.StimmtFuerRatsantrag(new Ratsantrag(Antragsart.Bauvorhaben, anderer, Bauvorhaben: "marktplatz")), "Kasse fiele ins Minus");
            KasseAuf(w, -100);
            Pruefe.Wahr(r.StimmtFuerRatsantrag(new Ratsantrag(Antragsart.Steuersatz, anderer, Prozent: 6)), "Kasse im Minus: rauf");
            // Die Prognose zählt die KI wie in der Sitzung
            Sitz(w, "Marktmeister", anderer);
            var (ja, _, enth, _) = w.Ratsantraege.Prognose(new Ratsantrag(Antragsart.Steuersatz, anderer, Prozent: 6));
            Pruefe.Gleich((7, 0), (ja, enth)); // leere Kasse: alle Ratsherren, die KI und der Antragsteller dafür
        }),

        ("KI-Antrag: Wer viel Steuern zahlt, beantragt eine niedrigere Steuer – wenn sie durchginge", () =>
        {
            var (sim, w, ki) = Aufbau("Zöllner");
            ki.Steuer(500);
            JahrBeenden(sim);
            KasseAuf(w, 3000); // zu wenig: Die Ratsherren senken nicht
            BisNachPolitik(sim);
            Pruefe.Wahr(w.Ratsantraege.Antrag(Antragsart.Steuersatz) is null, "nicht durchsetzbar – kein Antrag");
            ki.Steuer(500);
            JahrBeenden(sim);
            KasseAuf(w, 20_000);
            BisNachPolitik(sim);
            var a = w.Ratsantraege.Antrag(Antragsart.Steuersatz);
            Pruefe.Wahr(a is { Prozent: 4 } && a.Antragsteller == ki, $"Antrag: {a}");
            JahrBeenden(sim);
            Pruefe.Gleich(4, w.Kasse.SteuersatzProzent);
        }),

        ("KI-Antrag: Als Kämmerer erhöht sie bei leerer Kasse die Steuer", () =>
        {
            var (sim, w, ki) = Aufbau("Kämmerer");
            KasseAuf(w, -5000); // so leer, dass auch Gebühren und Steuern der KI bis mittags nicht reichen
            BisNachPolitik(sim);
            var a = w.Ratsantraege.Antrag(Antragsart.Steuersatz);
            Pruefe.Wahr(a is { Prozent: 6 } && a.Antragsteller == ki, $"Antrag: {a} – {string.Join(" | ", w.Rivalen.Single().Entscheidungen)}");
        }),

        ("KI-Antrag: Wer sich um ein Amt bewirbt, beantragt ein Fest; mit Werkstatt den größeren Marktplatz", () =>
        {
            var (sim, w, ki) = Aufbau("Zöllner");
            Ok(w, Wohnhaushilfe.MitWohnhaus(w, ki), new StandErwerben());
            Ok(w, ki, new AmtBewerben("Marktmeister"));
            w.Einstellen(w.BetriebEroeffnen("KI-Gießerei", Betriebsart.Giesserei, ki), "Geselle", 1);
            KasseAuf(w, 30_000);
            BisNachPolitik(sim);
            Pruefe.Wahr(w.Ratsantraege.Antrag(Antragsart.Fest)?.Antragsteller == ki, "Fest");
            var bau = w.Ratsantraege.Antrag(Antragsart.Bauvorhaben);
            Pruefe.Wahr(bau is not null && bau.Antragsteller == ki && bau.Bauvorhaben is "marktplatz" or "mauer", $"Bau: {bau}");
        }),

        ("KI-Befugnisse: Stadthauptmann verlegt die Streife zu den eigenen Betrieben; Wärter lässt gegen Kaution frei; Richter straft, wer ihm schadete", () =>
        {
            var (sim, w, ki) = Aufbau("Stadthauptmann");
            var giesserei = w.BetriebEroeffnen("KI-Gießerei", Betriebsart.Giesserei, ki);
            BisNachPolitik(sim);
            Pruefe.Gleich<Stadtviertel?>(w.Befugnisse.ViertelVon(giesserei), w.Befugnisse.Streife);

            // Wärter
            var (sim2, w2, ki2) = Aufbau("Gefängniswärter");
            var klaeger = Haus(w2, "Kläger");
            var raeuber = Haus(w2, "Räuber", 50_000);
            for (var i = 0; i < 2; i++) w2.Justiz.BeweisGeben(klaeger, w2.Justiz.TatMelden(raeuber, Vergehen.Raub, "Kläger", 500)!);
            Ok(w2, klaeger, new Anklagen("Räuber"));
            JahrBeenden(sim2);
            NurAmt(w2, "Gefängniswärter", ki2); // die KI bewirbt sich selbst weiter – hier bleibt sie Wärter
            Pruefe.Wahr(w2.Justiz.ImKerker(raeuber), "im Kerker");
            var geld = ki2.Vermoegen;
            BisNachPolitik(sim2);
            Pruefe.Wahr(!w2.Justiz.ImKerker(raeuber), "freigelassen – " + string.Join(" | ", w2.Rivalen.Single().Entscheidungen));
            Pruefe.Wahr(ki2.Vermoegen > geld, "Kaution kassiert");

            // Richter
            var (sim3, w3, ki3) = Aufbau("Stadtrichter");
            var anklaeger = Haus(w3, "Anklägerhaus");
            var schurke = Haus(w3, "Schurke");
            w3.Justiz.BeweisGeben(ki3, w3.Justiz.TatMelden(schurke, Vergehen.Diebstahl, ki3.Inhaber, 300)!); // er hat der KI geschadet
            for (var i = 0; i < 2; i++) w3.Justiz.BeweisGeben(anklaeger, w3.Justiz.TatMelden(schurke, Vergehen.Diebstahl, "Anklägerhaus", 300)!);
            Ok(w3, anklaeger, new Anklagen("Schurke"));
            BisNachPolitik(sim3);
            Pruefe.Gleich(25, w3.Befugnisse.Strafmass(anklaeger, schurke));
        }),
    };
}
