using Reichsgilde.Core.Zeit;

/// <summary>Tests für die angebrochene Spielminute (flüssige Darstellung von Licht und Karren).</summary>
static class TaktgeberTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Angebrochene Minute: Bruchteile bleiben zwischen den ganzen Ticks lesbar", () =>
        {
            // 60 s pro Jahr → 17 Spielminuten pro Sekunde; 0,1 s = 1,7 Minuten → 1 Tick fällig, 0,7 angebrochen
            var takt = new Taktgeber { RundenDauerSekunden = 60 };
            Pruefe.Gleich(1, takt.Voranschreiten(0.1));
            Pruefe.Ungefaehr(0.7, takt.AngebrocheneMinute, 1e-6);
        }),

        ("Angebrochene Minute wächst stetig und bleibt unter 1", () =>
        {
            var takt = new Taktgeber();
            var vorher = takt.AngebrocheneMinute;
            for (var i = 0; i < 30; i++)
            {
                var ticks = takt.Voranschreiten(1 / 60.0);
                var jetzt = takt.AngebrocheneMinute;
                Pruefe.Wahr(jetzt >= 0 && jetzt < 1, $"Bruchteil außerhalb 0..1: {jetzt}");
                if (ticks == 0) Pruefe.Wahr(jetzt > vorher, "Ohne neuen Tick muss der Bruchteil wachsen");
                vorher = jetzt;
            }
        }),

        ("Angebrochene Minute steht bei Pause still", () =>
        {
            var takt = new Taktgeber();
            takt.Voranschreiten(0.3);
            var vorher = takt.AngebrocheneMinute;
            takt.IstPausiert = true;
            takt.Voranschreiten(5);
            Pruefe.Gleich(vorher, takt.AngebrocheneMinute);
        }),
    };
}
