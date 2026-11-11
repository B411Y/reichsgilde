using Reichsgilde;

/// <summary>
/// Ablauf des 3D-Handschuh-Zeigers ohne Godot (<c>game/scripts/HandschuhModell.cs</c>): Wunsch-Haltung, Reihenfolge von Übergängen und Tippen,
/// Rückfall bei zu wenig Bildern je Sekunde mit Hysterese, Neigung nach der Mausgeschwindigkeit, Zeigerpunkt.
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

    static void Takten(HandschuhLeistung l, double fps, double sekunden, bool dreiD = true)
    {
        for (var t = 0.0; t < sekunden; t += 1.0 / fps) l.Takt(1.0 / fps, dreiD);
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

        ("Handschuh: Rückfall unter 40 Bildern je Sekunde nach der Schonzeit, Rückkehr erst nach Sperre und über 52", () =>
        {
            var l = new HandschuhLeistung();
            Takten(l, 30, 3.0);
            Pruefe.Wahr(!l.Rueckfall, "in der Schonzeit (Laden) kein Rückfall");
            Takten(l, 60, 6.0);
            Pruefe.Wahr(!l.Rueckfall, "60 Bilder je Sekunde: kein Rückfall");
            Takten(l, 30, 4.5);
            Pruefe.Wahr(l.Rueckfall, "30 Bilder je Sekunde: Rückfall");
            Pruefe.Gleich(1, l.Ausfaelle);
            Takten(l, 60, 4.0);
            Pruefe.Wahr(l.Rueckfall, "erst nach der Sperre zurück");
            Takten(l, 45, 14.0);
            Pruefe.Wahr(l.Rueckfall, "45 liegt zwischen 40 und 52: Hysterese, kein Flackern");
            Takten(l, 60, 6.0);
            Pruefe.Wahr(!l.Rueckfall, "60 Bilder je Sekunde nach der Sperre: wieder 3D");
        }),

        ("Handschuh: Ein einzelner Hänger (Karte laden) löst keinen Rückfall aus, dauerhaft langsame Bilder schon", () =>
        {
            var l = new HandschuhLeistung();
            Takten(l, 60, 6.0);
            l.Takt(3.0, true); // Karte wird geladen
            Takten(l, 20, 2.5); // gleich danach noch zäh: zählt in der Ruhezeit nicht
            Pruefe.Wahr(!l.Rueckfall, "ein Hänger ist kein Rückfall");
            Takten(l, 60, 6.0);
            Pruefe.Wahr(!l.Rueckfall, "danach wieder flott");
            Takten(l, 20, 5.0);
            Pruefe.Wahr(l.Rueckfall, "dauerhaft 20 Bilder je Sekunde: Rückfall");
        }),

        ("Handschuh: Nach drei Rückfällen bleibt es beim 2D-Zeiger; ohne laufenden 3D-Handschuh kein Rückfall", () =>
        {
            var l = new HandschuhLeistung();
            Takten(l, 20, 10.0, dreiD: false);
            Pruefe.Wahr(!l.Rueckfall, "ohne 3D wird nichts zurückgenommen");
            for (var i = 0; i < 3; i++)
            {
                Takten(l, 20, 5.0);
                Pruefe.Wahr(l.Rueckfall, $"Rückfall {i + 1}");
                Takten(l, 90, 16.0);
            }
            Pruefe.Gleich(3, l.Ausfaelle);
            Pruefe.Wahr(l.Rueckfall, "nach dem dritten Rückfall bleibt es aus");
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
