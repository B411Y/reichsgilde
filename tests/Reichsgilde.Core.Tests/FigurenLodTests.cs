using Reichsgilde;

/// <summary>
/// Detailstufen der Figuren mit Fingerknochen (Darstellung, <c>game/scripts/FigurenLod.cs</c>, hier mitübersetzt): Innenräume immer Nah,
/// Stadt nach Zoom (Nah bis zur nächsten Zoomstufe, Fern ab der VAT-Grenze), Ego-Ansicht nach Abstand; Hysterese gegen Flattern; das
/// Fingergewicht blendet weich über.
/// </summary>
static class FigurenLodTests
{
    const float Vat = 43f;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Figuren-LOD: Innenraum immer Nah, Stadt nach Zoom: nächste Zoomstufe Nah, dazwischen Mittel, ab der VAT-Grenze Fern", () =>
        {
            Pruefe.Gleich(FigurStufe.Nah, FigurenLod.Waehlen(FigurStufe.Fern, true, 100f, Vat));
            Pruefe.Gleich(FigurStufe.Nah, FigurenLod.Waehlen(FigurStufe.Mittel, false, 20f, Vat));
            Pruefe.Gleich(FigurStufe.Mittel, FigurenLod.Waehlen(FigurStufe.Mittel, false, 35f, Vat));
            Pruefe.Gleich(FigurStufe.Fern, FigurenLod.Waehlen(FigurStufe.Mittel, false, 50f, Vat));
            Pruefe.Gleich(FigurStufe.Fern, FigurenLod.Waehlen(FigurStufe.Nah, false, Vat, Vat));
        }),
        ("Figuren-LOD: Hysterese an der Grenze Nah/Mittel – kein Wechsel beim Wackeln um die Grenze (Gegenprobe: weit genug wechselt es)", () =>
        {
            var g = FigurenLod.NahBisZoom;
            // knapp über der Grenze: wer Nah ist, bleibt Nah; wer Mittel ist, bleibt Mittel
            Pruefe.Gleich(FigurStufe.Nah, FigurenLod.Waehlen(FigurStufe.Nah, false, g * 1.05f, Vat));
            Pruefe.Gleich(FigurStufe.Mittel, FigurenLod.Waehlen(FigurStufe.Mittel, false, g * 0.95f, Vat));
            // Wackeln zwischen 0,95 und 1,05 der Grenze ändert die Stufe nie
            foreach (var start in new[] { FigurStufe.Nah, FigurStufe.Mittel })
            {
                var s = start;
                for (var i = 0; i < 20; i++)
                    s = FigurenLod.Waehlen(s, false, g * (i % 2 == 0 ? 0.95f : 1.05f), Vat);
                Pruefe.Gleich(start, s, $"Wackeln ab {start}");
            }
            // weit genug: es wechselt
            Pruefe.Gleich(FigurStufe.Mittel, FigurenLod.Waehlen(FigurStufe.Nah, false, g * 1.2f, Vat));
            Pruefe.Gleich(FigurStufe.Nah, FigurenLod.Waehlen(FigurStufe.Mittel, false, g * 0.8f, Vat));
        }),
        ("Figuren-LOD: Ego-Ansicht nach Abstand (Zoom zählt nicht), mit Hysterese", () =>
        {
            var a = FigurenLod.EgoNahAbstand;
            Pruefe.Gleich(FigurStufe.Nah, FigurenLod.Waehlen(FigurStufe.Mittel, false, 100f, Vat, a * 0.5f));
            Pruefe.Gleich(FigurStufe.Mittel, FigurenLod.Waehlen(FigurStufe.Nah, false, 10f, Vat, a * 1.5f));
            Pruefe.Gleich(FigurStufe.Nah, FigurenLod.Waehlen(FigurStufe.Nah, false, 100f, Vat, a * 1.05f));
            Pruefe.Gleich(FigurStufe.Mittel, FigurenLod.Waehlen(FigurStufe.Mittel, false, 100f, Vat, a * 0.95f));
        }),
        ("Figuren-LOD: Fingergewicht blendet in der Überblendzeit weich über und bleibt in 0..1; Finger werden nur in Nah oder beim Überblenden gerechnet", () =>
        {
            var w = 0f;
            var schritte = 0;
            while (w < 1f && schritte < 1000) { w = FigurenLod.FingerGewicht(w, FigurStufe.Nah, 1f / 60f); schritte++; }
            Pruefe.Gleich(1f, w);
            Pruefe.Wahr(System.Math.Abs(schritte - FigurenLod.Ueberblendung * 60f) <= 1f, $"Überblendung in {schritte} Bildern");
            var halb = FigurenLod.FingerGewicht(1f, FigurStufe.Mittel, FigurenLod.Ueberblendung / 2f);
            Pruefe.Ungefaehr(0.5, halb, 1e-5);
            Pruefe.Wahr(FigurenLod.FingerRechnen(FigurStufe.Mittel, halb), "beim Überblenden noch gerechnet");
            Pruefe.Gleich(0f, FigurenLod.FingerGewicht(halb, FigurStufe.Fern, 10f));
            Pruefe.Wahr(!FigurenLod.FingerRechnen(FigurStufe.Mittel, 0f), "Mittel ohne Überblendung: keine Finger");
            Pruefe.Wahr(FigurenLod.FingerRechnen(FigurStufe.Nah, 0f), "Nah: Finger");
        }),
    };
}
