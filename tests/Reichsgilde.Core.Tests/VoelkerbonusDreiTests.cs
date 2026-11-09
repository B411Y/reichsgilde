using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Völkerboni ±3 % und Vermächtnis 20 % (Datenversion 76, Entscheidung des Nutzers 9.11.2026; WIRTSCHAFTSMODELL „Völkerboni ±3 %“)
//
//  Die Boni der Berufe mit Ware sinken von ±5 auf ±3 %; Dieb, Gardist und Geldleiher bleiben bei ±2 % (Gauner-Regeln). Das Vermächtnis der
//  Lebensweise geht von 0,35 zurück auf 0,2.
// ═════════════════════════════════════════════════════════════════════════════

static class VoelkerbonusDreiTests
{
    static readonly string[] OhneWare = { "Dieb", "Gardist", "Geldleiher" };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Völkerboni ±3 %: Datenversion 76, jeder Bonus eines Berufs mit Ware ist ±3 %, ohne Ware ±2 %, Vermächtnis 0,2", () =>
        {
            var d = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
            Pruefe.Gleich(76, d.Version);
            Pruefe.Ungefaehr(0.2, d.Lebensweise!.Vermaechtnis, 1e-12);
            var anzahl = 0;
            foreach (var (beruf, b) in d.Berufe.OrderBy(x => x.Key, StringComparer.Ordinal))
                foreach (var (volk, bonus) in b.Voelker.OrderBy(x => x.Key))
                {
                    var soll = OhneWare.Contains(beruf) ? 0.02 : 0.03;
                    Pruefe.Wahr(Math.Abs(Math.Abs(bonus) - soll) < 1e-12, $"{beruf} {volk}: {bonus}");
                    anzahl++;
                }
            Pruefe.Gleich(16, anzahl); // 10 Boni der Berufe mit Ware, 6 ohne Ware
            Pruefe.Ungefaehr(-0.03, d.Berufe["Parfumeur"].Voelker[Volk.Zwerg], 1e-12);
            Pruefe.Ungefaehr(0.03, d.Berufe["Alchimist"].Voelker[Volk.Elb], 1e-12);
        }),

        ("Völkerboni ±3 %: Datenpfad.WieVorVoelkerbonusDrei gibt die Werte bis Datenversion 75 (±5 %, Vermächtnis 0,35), die Berufe ohne Ware bleiben", () =>
        {
            var neu = Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft());
            var alt = Wirtschaftsdaten.AusJson(Datenpfad.WieVorVoelkerbonusDrei());
            Pruefe.Ungefaehr(0.35, alt.Lebensweise!.Vermaechtnis, 1e-12);
            foreach (var (beruf, b) in alt.Berufe.OrderBy(x => x.Key, StringComparer.Ordinal))
                foreach (var (volk, bonus) in b.Voelker.OrderBy(x => x.Key))
                {
                    var jetzt = neu.Berufe[beruf].Voelker[volk];
                    var soll = OhneWare.Contains(beruf) ? jetzt : jetzt / 0.03 * 0.05;
                    Pruefe.Wahr(Math.Abs(bonus - soll) < 1e-12, $"{beruf} {volk}: {bonus} statt {soll}");
                }
            Pruefe.Ungefaehr(0.05, alt.Berufe["Schmied"].Voelker[Volk.Zwerg], 1e-12);
            Pruefe.Ungefaehr(-0.05, alt.Berufe["Schmied"].Voelker[Volk.Halbling], 1e-12);
            // Das Balancing nach dem Handgeld (bis Datenversion 74) baut darauf auf: ±5 %, Vermächtnis 0,2
            var vorHandgeld = Wirtschaftsdaten.AusJson(Datenpfad.WieVorBalancingHandgeld());
            Pruefe.Ungefaehr(0.2, vorHandgeld.Lebensweise!.Vermaechtnis, 1e-12);
            Pruefe.Ungefaehr(0.05, vorHandgeld.Berufe["Wirt"].Voelker[Volk.Halbling], 1e-12);
        }),
    };
}
