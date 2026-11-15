using Reichsgilde;

/// <summary>
/// Stoff mit Federketten (Darstellung, <c>game/scripts/FigurenLod.cs</c>): nur die nächsten Figuren federn, wer federt, bleibt bei fast
/// gleichem Abstand dabei; ein Sprung der Figur beginnt die Feder neu.
/// </summary>
static class FigurenLodStoffTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Figuren-Stoff: es federn nur die nächsten, wer nicht in Frage kommt (negativer Abstand), nie", () =>
        {
            var wahl = FigurenLod.StoffWaehlen(new[] { 5f, 1f, -1f, 3f, 9f }, new[] { false, false, false, false, false }, 2);
            Pruefe.Gleich(false, wahl[0]);
            Pruefe.Gleich(true, wahl[1]);
            Pruefe.Gleich(false, wahl[2]);
            Pruefe.Gleich(true, wahl[3]);
            Pruefe.Gleich(false, wahl[4]);
            // Gegenprobe: mit Platz für alle federn alle, die in Frage kommen, und nur sie
            wahl = FigurenLod.StoffWaehlen(new[] { 5f, 1f, -1f, 3f, 9f }, new[] { false, false, false, false, false }, 99);
            Pruefe.Gleich(4, wahl.Count(w => w));
            Pruefe.Gleich(false, wahl[2]);
            // Obergrenze 0 (Messhilfe --stofffeder=0): niemand
            Pruefe.Gleich(0, FigurenLod.StoffWaehlen(new[] { 1f, 2f }, new[] { true, true }, 0).Count(w => w));
        }),
        ("Figuren-Stoff: wer schon federt, behält bei fast gleichem Abstand den Platz (Gegenprobe: deutlich näher verdrängt ihn)", () =>
        {
            // 0 federt bei 10 m, 1 kommt auf 9 m heran: 10 × 0,8 = 8 < 9, also bleibt 0
            var wahl = FigurenLod.StoffWaehlen(new[] { 10f, 9f }, new[] { true, false }, 1);
            Pruefe.Gleich(true, wahl[0]);
            Pruefe.Gleich(false, wahl[1]);
            wahl = FigurenLod.StoffWaehlen(new[] { 10f, 7f }, new[] { true, false }, 1);
            Pruefe.Gleich(false, wahl[0]);
            Pruefe.Gleich(true, wahl[1]);
            // Gleichstand ohne Vorgeschichte: die kleinere Stelle
            wahl = FigurenLod.StoffWaehlen(new[] { 4f, 4f }, new[] { false, false }, 1);
            Pruefe.Gleich(true, wahl[0]);
        }),
        ("Figuren-Stoff: Knochennamen wie in passanten_stoff.py; ein Sprung über die Grenze beginnt die Feder neu, ein Schritt nicht", () =>
        {
            Pruefe.Gleich("Stoff_0_0", FigurenLod.StoffKnochen(0, 0));
            Pruefe.Gleich("Stoff_7_3", FigurenLod.StoffKnochen(FigurenLod.StoffBahnen - 1, FigurenLod.StoffGlieder));
            Pruefe.Gleich(false, FigurenLod.StoffNeuBeginnen(0.05f));
            Pruefe.Gleich(true, FigurenLod.StoffNeuBeginnen(3f));
        }),
    };
}
