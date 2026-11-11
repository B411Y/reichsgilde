using Reichsgilde;

/// <summary>
/// Ablauf des 3D-Handschuh-Zeigers ohne Godot (<c>game/scripts/HandschuhModell.cs</c>): Wunsch-Haltung, Reihenfolge von Übergängen und Tippen,
/// Geltung des 3D-Handschuhs (nie nach Bildrate), Tempo-Konstanten, Neigung nach der Mausgeschwindigkeit, Zeigerpunkt.
/// </summary>
static class HandschuhModellTests
{
    /// <summary>Spielt Entscheidungen durch: jeder Einmalclip „läuft“ und wird fertig; liefert die Reihenfolge der gestarteten Clips.</summary>
    static List<string> Ablaufen(HandschuhAblauf a, HandPose wunsch, int schritte = 6)
    {
        var clips = new List<string>();
        for (var i = 0; i < schritte; i++)
        {
            var clip = a.Entscheiden(wunsch);
            if (clip is null) break;
            clips.Add(clip);
            a.EinmalFertig();
        }
        return clips;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Handschuh: Wunsch – Ziehen schlägt Zeigen, Zeigen schlägt Ruhe", () =>
        {
            Pruefe.Gleich(HandPose.Ruhe, HandschuhModell.Wunsch(false, false));
            Pruefe.Gleich(HandPose.Zeigen, HandschuhModell.Wunsch(false, true));
            Pruefe.Gleich(HandPose.Greifen, HandschuhModell.Wunsch(true, true));
            Pruefe.Gleich(HandPose.Greifen, HandschuhModell.Wunsch(true, false));
        }),

        ("Handschuh: Übergänge laufen in der Reihenfolge der Haltungen, ohne Wunschwechsel bleibt es bei der Schleife", () =>
        {
            var a = new HandschuhAblauf();
            Pruefe.Wahr(a.Entscheiden(HandPose.Ruhe) is null, "Ruhe bleibt Ruhe");
            Pruefe.Gleich("Uebergang_Ruhe_Zeigen", string.Join(",", Ablaufen(a, HandPose.Zeigen)));
            Pruefe.Gleich(HandPose.Zeigen, a.Pose);
            Pruefe.Gleich("Uebergang_Zeigen_Greifen", string.Join(",", Ablaufen(a, HandPose.Greifen)));
            Pruefe.Gleich("Uebergang_Greifen_Ruhe", string.Join(",", Ablaufen(a, HandPose.Ruhe)));
            Pruefe.Gleich("Uebergang_Ruhe_Greifen", string.Join(",", Ablaufen(a, HandPose.Greifen)));
            Pruefe.Gleich("Uebergang_Greifen_Zeigen", string.Join(",", Ablaufen(a, HandPose.Zeigen)));
            Pruefe.Gleich("Uebergang_Zeigen_Ruhe", string.Join(",", Ablaufen(a, HandPose.Ruhe)));
        }),

        ("Handschuh: Tippen geht nur aus Zeigen – aus Ruhe erst der Übergang, dann Tippen, dann zurück nach Wunsch", () =>
        {
            var a = new HandschuhAblauf();
            a.TippenMerken();
            Pruefe.Gleich("Uebergang_Ruhe_Zeigen,Tippen,Uebergang_Zeigen_Ruhe", string.Join(",", Ablaufen(a, HandPose.Ruhe)));
            // schon in Zeigen: Tippen sofort, bleibt danach in Zeigen
            var b = new HandschuhAblauf();
            Ablaufen(b, HandPose.Zeigen);
            b.TippenMerken();
            Pruefe.Gleich("Tippen", string.Join(",", Ablaufen(b, HandPose.Zeigen)));
            Pruefe.Gleich(HandPose.Zeigen, b.Pose);
        }),

        ("Handschuh: Während ein Einmalclip läuft, wechselt nichts; wer zieht, tippt nicht", () =>
        {
            var a = new HandschuhAblauf();
            Pruefe.Gleich("Uebergang_Ruhe_Zeigen", a.Entscheiden(HandPose.Zeigen));
            Pruefe.Wahr(a.EinmalLaeuft, "läuft");
            Pruefe.Wahr(a.Entscheiden(HandPose.Greifen) is null, "kein zweiter Clip währenddessen");
            a.EinmalFertig();
            a.TippenMerken();
            Pruefe.Gleich("Uebergang_Zeigen_Greifen", a.Entscheiden(HandPose.Greifen));
            a.EinmalFertig();
            Pruefe.Wahr(a.Entscheiden(HandPose.Greifen) is null, "das Tippen ist beim Greifen verfallen");
        }),

        ("Handschuh: 3D bleibt bei jeder Bildrate, solange es eingestellt ist; nur Einstellung, Datei und Größe schalten ab", () =>
        {
            // Die Bildrate kommt in der Entscheidung gar nicht mehr vor (Nutzer, 11.11.2026): gleiche Eingaben, gleiche Antwort.
            Pruefe.Wahr(HandschuhModell.Gilt(true, true, 48), "an, Datei da, 48 Pixel: 3D");
            Pruefe.Wahr(HandschuhModell.Gilt(true, true, 80), "80 Pixel: 3D");
            Pruefe.Wahr(!HandschuhModell.Gilt(false, true, 48), "Einstellung aus: 2D");
            Pruefe.Wahr(!HandschuhModell.Gilt(true, false, 48), "Datei fehlt: 2D");
            Pruefe.Wahr(!HandschuhModell.Gilt(true, true, 32), "unter 48 Pixel: 2D");
        }),

        ("Handschuh: Tempo der Übergänge und des Tippens ist höher als in der Datei, Ziehschwelle klein", () =>
        {
            Pruefe.Wahr(HandschuhModell.UebergangTempo >= 1.7 && HandschuhModell.TippenTempo >= 2.5, "Clips laufen etwa 45 % kürzer");
            Pruefe.Wahr(HandschuhModell.BlendEinmal < 0.06 && HandschuhModell.BlendSchleife < 0.10, "Blendzeiten kürzer als vorher");
            Pruefe.Wahr(HandschuhModell.ZiehSchwelle <= 6.0 && HandschuhModell.ZiehSchwelle > 0, "Ziehen früher, aber nicht schon beim Zittern");
        }),

        ("Handschuh: Neigung ist begrenzt, gleitet unabhängig von der Bildrate und kehrt zurück", () =>
        {
            Pruefe.Gleich(0.0, HandschuhModell.NeigungsStaerke(0));
            Pruefe.Gleich(1.0, HandschuhModell.NeigungsStaerke(99999));
            Pruefe.Gleich(-1.0, HandschuhModell.NeigungsStaerke(-99999));
            double a = 0, b = 0;
            for (var i = 0; i < 60; i++) a = HandschuhModell.Gleiten(a, 10, 1.0 / 60, 0.1);
            for (var i = 0; i < 120; i++) b = HandschuhModell.Gleiten(b, 10, 1.0 / 120, 0.1);
            Pruefe.Ungefaehr(a, b, 1e-9);
            Pruefe.Wahr(a > 9.9 && a <= 10, "nach 1 s fast am Ziel");
            Pruefe.Ungefaehr(0.0, HandschuhModell.Gleiten(a, 0, 5.0, 0.1), 1e-6);
        }),

        ("Handschuh: Zeigerpunkt – das Bild liegt so, dass die Fingerspitze genau auf der Maus liegt", () =>
        {
            var (x, y) = HandschuhModell.BildEcke(400, 300, 12.5, 9.25);
            Pruefe.Ungefaehr(387.5, x, 1e-9);
            Pruefe.Ungefaehr(290.75, y, 1e-9);
            Pruefe.Wahr(HandschuhModell.MindestGroesse == 48, "ab 48 Pixel");
        }),
    };
}
