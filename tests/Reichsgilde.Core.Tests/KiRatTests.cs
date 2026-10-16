using Reichsgilde.Core;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests: Wie KI-Häuser im Rat wählen – nach ihrem Charakter statt immer für den Amtsinhaber.</summary>
static class KiRatTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Eine Stadt mit einem KI-Haus und drei weiteren Häusern: arm, reich (Bürgermeister) und ein Feind der KI.</summary>
    static (Stadtwirtschaft W, KiRivale Ki, Konto Arm, Konto Reich, Konto Feind, Amt Amt) Stadt(KiCharakter charakter)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
        var w = sim.Wirtschaft!;
        var kiKonto = w.KontoEroeffnen("Haus Eisen", startvermoegen: 20_000);
        w.BetriebEroeffnen("Gießerei Eisen", Betriebsart.Giesserei, kiKonto);
        var arm = w.KontoEroeffnen("Haus Arm", startvermoegen: 2_000);
        var reich = w.KontoEroeffnen("Haus Reich", startvermoegen: 90_000);
        var feind = w.KontoEroeffnen("Haus Feind", startvermoegen: 5_000);
        var ki = w.KiAnmelden(kiKonto, Partieaufbau.ProfilFuer(charakter));
        var amt = w.Rat.Aemter.OrderByDescending(a => a.Stufe).First();
        amt.Ratsherr = null;
        amt.Konto = reich;
        return (w, ki, arm, reich, feind, amt);
    }

    /// <summary>Der Feind hat einen Betrieb der KI sabotiert, und die KI hat den Beweis.</summary>
    static void Feindschaft(Stadtwirtschaft w, Konto feind, KiRivale ki)
    {
        var spur = w.Justiz.TatMelden(feind, Vergehen.Sabotage, "Gießerei Eisen", 300);
        Pruefe.Wahr(spur is not null, "Spur angelegt");
        w.Justiz.BeweisGeben(ki.Konto, spur!);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Rat: Vorsichtig bleibt beim Amtsinhaber, ausgewogen wählt den Ungefährlicheren, wagemutig nie das führende Haus", () =>
        {
            foreach (var (charakter, erwartet) in new[] { (KiCharakter.Vorsichtig, "Haus Reich"), (KiCharakter.Ausgewogen, "Haus Arm"), (KiCharakter.Wagemutig, "Haus Arm") })
            {
                var (_, ki, arm, reich, _, amt) = Stadt(charakter);
                Pruefe.Gleich(erwartet, ki.Wahlstimme(amt, new Konto?[] { arm, reich })?.Inhaber, charakter.ToString());
            }
        }),

        ("KI-Rat: Ein Ratsherr ist ungefährlicher als jedes Haus – ausgewogen wählt ihn", () =>
        {
            var (_, ki, arm, _, _, amt) = Stadt(KiCharakter.Ausgewogen);
            Pruefe.Gleich(null, ki.Wahlstimme(amt, new Konto?[] { arm, null }));
        }),

        ("KI-Rat: Wer der KI geschadet hat (sie hat den Beweis), bekommt ihre Stimme nicht – auch nicht als Amtsinhaber", () =>
        {
            var (w, ki, arm, _, feind, amt) = Stadt(KiCharakter.Vorsichtig);
            Feindschaft(w, feind, ki);
            amt.Konto = feind;
            Pruefe.Gleich("Haus Arm", ki.Wahlstimme(amt, new Konto?[] { arm, feind })?.Inhaber);
            var (w2, ki2, _, reich2, feind2, amt2) = Stadt(KiCharakter.Ausgewogen);
            Feindschaft(w2, feind2, ki2);
            Pruefe.Gleich("Haus Reich", ki2.Wahlstimme(amt2, new Konto?[] { feind2, reich2 })?.Inhaber, "Ein Feind ist gefährlicher als das reichste Haus");
        }),

        ("KI-Rat: Wagemutig stimmt lieber für einen Feind als für das führende Haus", () =>
        {
            var (w, ki, _, reich, feind, amt) = Stadt(KiCharakter.Wagemutig);
            Feindschaft(w, feind, ki);
            Pruefe.Gleich("Haus Feind", ki.Wahlstimme(amt, new Konto?[] { feind, reich })?.Inhaber);
            Pruefe.Gleich("Haus Reich", ki.Wahlstimme(amt, new Konto?[] { reich })?.Inhaber, "Gibt es nur das führende Haus, bleibt nur dieses");
        }),

        ("KI-Rat: In der Sitzung zählt die Stimme der KI – und die Wahlprognose rechnet mit ihr", () =>
        {
            var (w, ki, arm, reich, _, bm) = Stadt(KiCharakter.Ausgewogen);
            // Gewählt wird ein Amt der untersten Stufe (dort darf man sich ohne Amt bewerben); der Amtsinhaber ist das reiche Haus.
            // Die KI sitzt höher im Rat und wählt mit.
            bm.Konto = null; bm.Ratsherr = w.Rat.Ratsherren[0];
            var amt = w.Rat.Aemter.OrderBy(a => a.Stufe).ThenBy(a => a.Name, StringComparer.Ordinal).First();
            amt.Ratsherr = null;
            amt.Konto = reich;
            var kiAmt = w.Rat.Aemter.First(a => a.Stufe > amt.Stufe && a != bm);
            kiAmt.Ratsherr = null;
            kiAmt.Konto = ki.Konto;
            arm.Stand = Math.Max(arm.Stand, w.Daten.StandStufe(amt.Daten.MindestStand));
            var bewerbung = w.Rat.Bewerben(arm, amt.Name);
            Pruefe.Wahr(bewerbung.Erfolg, bewerbung.Meldung);
            var (sicher, _, _) = w.Rat.WahlPrognose(amt, arm);
            var nurRatsherren = w.Rat.Waehler(amt).Count(s => s.Ratsherr is { } r && w.Rat.Punkte(r, amt, arm) > w.Rat.Punkte(r, amt, reich) + 1e-9);
            Pruefe.Gleich(nurRatsherren + 1, sicher, "Prognose: die Ratsherren für Arm plus die Stimme der KI");
        }),
    };
}
