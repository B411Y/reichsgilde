using Reichsgilde;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Wirtschaft;
using M = Reichsgilde.MusikModell;

/// <summary>
/// Das Kampfstück („Jetzt Aber Schnell!“) hat seinen Auslöser wieder (Nutzer, 11.11.2026): echte Kämpfe aus den Vorfällen des Kerns, nur beim eigenen Haus.
/// Gegenproben: ein gelungener Überfall oder Einbruch ist kein Kampf, fremde Häuser lösen nichts aus.
/// </summary>
static class MusikKampfTests
{
    static readonly Konto Ich = new("Haus Ich", 100, 1400), Fremd = new("Haus Fremd", 100, 1400);

    static Vorfall V(Vorfallart art, bool erfolg, Konto? opfer = null, bool waechter = false, Konto? wache = null, Konto? taeter = null) =>
        new() { Art = art, Ort = Vorfallort.Betrieb, Ziel = "x", Erfolg = erfolg, Opfer = opfer, Waechter = waechter, Wache = wache, Taeterhaus = taeter };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Musik Kampf: abgewehrter Überfall auf den eigenen Karren und Geleit der Wache = Kampf; gelungener Überfall ohne Wache nicht", () =>
        {
            Pruefe.Gleich(M.Anlass.Kampf, M.AnlassAusVorfall(V(Vorfallart.Ueberfall, false, Ich), Ich));
            Pruefe.Gleich(M.Anlass.Kampf, M.AnlassAusVorfall(V(Vorfallart.Ueberfall, true, Ich, wache: Fremd), Ich));
            Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusVorfall(V(Vorfallart.Ueberfall, true, Ich), Ich));
            Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusVorfall(V(Vorfallart.Ueberfall, false, Fremd), Ich));
        }),

        ("Musik Kampf: Einbruch, den ein Wächter vereitelt = Kampf; ohne Wächter, gelungen oder bei Fremden nicht", () =>
        {
            foreach (var art in new[] { Vorfallart.Einbruch, Vorfallart.Hauseinbruch })
            {
                Pruefe.Gleich(M.Anlass.Kampf, M.AnlassAusVorfall(V(art, false, Ich, waechter: true), Ich));
                Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusVorfall(V(art, true, Ich, waechter: true), Ich));
                Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusVorfall(V(art, false, Ich, waechter: false), Ich));
                Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusVorfall(V(art, false, Fremd, waechter: true), Ich));
            }
        }),

        ("Musik Kampf: die Stadtwache stellt einen Dieb des eigenen Hauses (oder die eigene Wache fasst ihn) = Kampf; fremder Dieb, fremde Wache, kein Fang nicht", () =>
        {
            Pruefe.Gleich(M.Anlass.Kampf, M.AnlassAusVorfall(V(Vorfallart.Taschendiebstahl, false, wache: Fremd, taeter: Ich), Ich));
            Pruefe.Gleich(M.Anlass.Kampf, M.AnlassAusVorfall(V(Vorfallart.Taschendiebstahl, false, wache: Ich, taeter: Fremd), Ich));
            Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusVorfall(V(Vorfallart.Taschendiebstahl, false, wache: Fremd, taeter: Fremd), Ich));
            Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusVorfall(V(Vorfallart.Taschendiebstahl, false, wache: null, taeter: Ich), Ich));
        }),

        ("Musik Kampf: andere Vorfälle (Brand, Entführung, Auskundschaften) sind kein Kampf; Brand und gelungener Überfall bleiben Gefahr (Meldung)", () =>
        {
            foreach (var art in new[] { Vorfallart.Brandanschlag, Vorfallart.Hausbrand, Vorfallart.Entfuehrung, Vorfallart.Auskundschaften, Vorfallart.Giftanschlag })
                Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusVorfall(V(art, false, Ich, waechter: true, wache: Ich, taeter: Ich), Ich));
            Pruefe.Gleich(M.Anlass.Gefahr, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Feuer));
            Pruefe.Gleich(M.Anlass.Gefahr, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Ueberfall));
        }),

        ("Musik Kampf: ein gemeldeter Kampf spielt das Stück „Jetzt Aber Schnell!“ und geht vor Gefahr", () =>
        {
            var (s, _) = MusikTests.Liste();
            var l = new MusikTests.Lauf(s, 41);
            l.Schritte(20);
            l.Steuerung.Melden(M.Anlass.Gefahr);
            l.Schritte(1);
            l.Steuerung.Melden(M.Anlass.Kampf);
            l.Schritte(1);
            Pruefe.Gleich(M.Anlass.Kampf, l.Steuerung.LaufenderAnlass);
            Pruefe.Gleich("kampf", l.Steuerung.Laufend!.Zusatz);
        }),
    };
}
