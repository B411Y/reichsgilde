using Reichsgilde;

/// <summary>Auswahlrechteck = Zeigen, Zucken an der Tür und das Winken des Handschuhs (<c>HandschuhModell.cs</c>, <c>HandschuhLeben.cs</c>), ohne Godot.</summary>
static class HandschuhLebenTests
{
    const double Bild = 1.0 / 60.0;

    /// <summary>Läuft sekundenweise ohne Eingabe; liefert die Zeit bis zum Start oder -1.</summary>
    static double BisStart(WinkenLogik w, double maxSekunden, bool gesperrt = false)
    {
        for (var t = 0.0; t < maxSekunden; t += Bild)
            if (w.Schritt(Bild, false, gesperrt) == WinkenEreignis.Start) return t + Bild;
        return -1;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Auswahlrechteck: Rechteck ziehen gibt Zeigen, Ware/Fenster/Kamera ziehen gibt die Faust", () =>
        {
            Pruefe.Gleich(HandPose.Zeigen, HandschuhModell.Wunsch(true, false, false, true, false));
            Pruefe.Gleich(HandPose.Greifen, HandschuhModell.Wunsch(true, false, false, false, false));
            Pruefe.Gleich(HandPose.Greifen, HandschuhModell.Wunsch(true, true, true, false, false));
            Pruefe.Gleich(HandPose.Ruhe, HandschuhModell.Wunsch(false, false, false, true, false)); // ohne Ziehen nichts Besonderes
        }),
        ("Auswahlrechteck: ein Klick, der zum Ziehen wird, tippt nicht mehr", () =>
        {
            var a = new HandschuhAblauf();
            a.Entscheiden(HandPose.Zeigen); a.EinmalFertig();
            a.TippenMerken();
            a.TippenVerwerfen();
            Pruefe.Gleich((string?)null, a.Entscheiden(HandPose.Zeigen));
        }),
        ("Winken: nicht vor 60 s, dann Start; die Wartezeit ist 60 s", () =>
        {
            var w = new WinkenLogik(() => 0.5);
            var t = BisStart(w, 100);
            Pruefe.Wahr(Math.Abs(t - 60.0) < 0.1, $"Start nach {t} s");
            Pruefe.Wahr(w.Winkt, "winkt");
            var v = new WinkenLogik(() => 0.5);
            Pruefe.Gleich(-1.0, BisStart(v, 59.5));
        }),
        ("Winken: Gegenprobe – jede Eingabe setzt die Zeit zurück und bricht ab", () =>
        {
            var w = new WinkenLogik(() => 0.5);
            BisStart(w, 50);
            Pruefe.Wahr(w.Schritt(Bild, true, false) == WinkenEreignis.Nichts, "Eingabe vor dem Winken");
            Pruefe.Wahr(w.Leerlauf == 0, "zurückgesetzt");
            var t = BisStart(w, 100);
            Pruefe.Wahr(Math.Abs(t - 60.0) < 0.1, $"wieder 60 s, war {t}");
            Pruefe.Gleich(WinkenEreignis.Abbruch, w.Schritt(Bild, true, false)); // Eingabe mitten im Winken
            Pruefe.Wahr(!w.Winkt, "winkt nicht mehr");
            Pruefe.Gleich(-1.0, BisStart(w, 59.0));
        }),
        ("Winken: Gegenprobe – nicht im Textfeld, nicht in Blende oder Klinke (gesperrt)", () =>
        {
            var w = new WinkenLogik(() => 0.5);
            Pruefe.Gleich(-1.0, BisStart(w, 300, gesperrt: true));
            Pruefe.Wahr(!w.Winkt, "nie gewunken");
            // gesperrt mitten im Warten: die Zeit beginnt neu
            var v = new WinkenLogik(() => 0.5);
            BisStart(v, 55);
            v.Schritt(Bild, false, true);
            Pruefe.Gleich(-1.0, BisStart(v, 59.0));
        }),
        ("Winken: Sperre im Winken bricht ab (Blende beginnt)", () =>
        {
            var w = new WinkenLogik(() => 0.5);
            BisStart(w, 100);
            Pruefe.Gleich(WinkenEreignis.Abbruch, w.Schritt(Bild, false, true));
        }),
        ("Winken: Wiederholung nach 2 bis 3 Minuten, solange keine Eingabe kommt", () =>
        {
            var kurz = new WinkenLogik(() => 0.0);
            BisStart(kurz, 100);
            kurz.Beendet();
            Pruefe.Gleich(-1.0, BisStart(kurz, 119.0));
            Pruefe.Wahr(BisStart(kurz, 5) > 0, "nach 120 s");
            var lang = new WinkenLogik(() => 1.0);
            BisStart(lang, 100);
            lang.Beendet();
            Pruefe.Gleich(-1.0, BisStart(lang, 179.0));
            Pruefe.Wahr(BisStart(lang, 5) > 0, "nach 180 s");
        }),
        ("Winken: Eingabe nach dem Winken setzt auf die erste Wartezeit (60 s) zurück", () =>
        {
            var w = new WinkenLogik(() => 1.0);
            BisStart(w, 100);
            w.Beendet();
            w.Schritt(Bild, true, false);
            var t = BisStart(w, 100);
            Pruefe.Wahr(Math.Abs(t - 60.0) < 0.1, $"60 s nach der Eingabe, war {t}");
        }),
        ("Winken: Prüfzeiten (--winktest) 3 s und 4 bis 6 s", () =>
        {
            var w = new WinkenLogik(() => 0.5, 3.0, 4.0, 6.0);
            Pruefe.Wahr(Math.Abs(BisStart(w, 10) - 3.0) < 0.1, "3 s");
            w.Beendet();
            Pruefe.Wahr(Math.Abs(BisStart(w, 10) - 5.0) < 0.1, "5 s");
        }),
        ("Winken: Haltung – Wunsch Winken hat Vorrang; Übergänge aus Ruhe, Zeigen, Bereit direkt, auch aus Greifen", () =>
        {
            Pruefe.Gleich(HandPose.Winken, HandschuhModell.Wunsch(false, true, true, false, true));
            var a = new HandschuhAblauf();
            Pruefe.Gleich("Uebergang_Ruhe_Winken", a.Entscheiden(HandPose.Winken)); a.EinmalFertig();
            Pruefe.Gleich("Winken", HandschuhModell.Schleife(a.Pose));
            Pruefe.Gleich("Uebergang_Winken_Zeigen", a.Entscheiden(HandPose.Zeigen)); a.EinmalFertig();
            Pruefe.Gleich("Uebergang_Zeigen_Winken", a.Entscheiden(HandPose.Winken)); a.EinmalFertig();
            Pruefe.Gleich("Uebergang_Winken_Greifen", a.Entscheiden(HandPose.Greifen)); a.EinmalFertig(); // Greifen und Winken haben einen eigenen Clip
            Pruefe.Gleich("Uebergang_Greifen_Winken", a.Entscheiden(HandPose.Winken)); a.EinmalFertig();
            Pruefe.Gleich("Uebergang_Winken_Bereit", a.Entscheiden(HandPose.Bereit)); a.EinmalFertig();
            Pruefe.Gleich("Uebergang_Bereit_Ruhe", a.Entscheiden(HandPose.Greifen)); // Bereit und Greifen: über Ruhe
        }),
        ("Winken: Gegenprobe – aus dem Tippen heraus nicht starten: erst wenn der Tipp-Clip durch ist, kommt der Übergang aus Zeigen", () =>
        {
            var a = new HandschuhAblauf();
            a.Entscheiden(HandPose.Zeigen); a.EinmalFertig();
            a.TippenMerken();
            Pruefe.Gleich("Tippen", a.Entscheiden(HandPose.Zeigen)); // Tippen läuft (kein Winken-Übergang dafür)
            Pruefe.Gleich((string?)null, a.Entscheiden(HandPose.Winken)); // wartet
            a.EinmalFertig();
            Pruefe.Gleich(HandPose.Zeigen, a.Pose);
            Pruefe.Gleich("Uebergang_Zeigen_Winken", a.Entscheiden(HandPose.Winken));
        }),
        ("Handschuh über Popups: Zeichenfläche im Unterfenster liegt so, dass die Fingerspitze auf der Maus des Fensters liegt", () =>
        {
            var (x, y) = HandschuhModell.FensterPosition(120, 80, 30, 12);
            Pruefe.Gleich(90.0, x);
            Pruefe.Gleich(68.0, y);
            // gleicher Zeigerpunkt in Maus- und Bildpunkt: das Bild beginnt bei Maus minus Zeigerpunkt, die Spitze (Bildpunkt 30/12) landet auf der Maus
            Pruefe.Gleich(120.0, x + 30);
            Pruefe.Gleich(80.0, y + 12);
        }),
        ("Zucken: nur in Bereit, nach 3 bis 6 s", () =>
        {
            var kurz = new BereitZucken(() => 0.0);
            var t = 0.0;
            while (!kurz.Schritt(Bild, true, false) && t < 20) t += Bild;
            Pruefe.Wahr(Math.Abs(t - 3.0) < 0.1, $"3 s, war {t}");
            var lang = new BereitZucken(() => 1.0);
            t = 0;
            while (!lang.Schritt(Bild, true, false) && t < 20) t += Bild;
            Pruefe.Wahr(Math.Abs(t - 6.0) < 0.1, $"6 s, war {t}");
        }),
        ("Zucken: Gegenprobe – nie außerhalb von Bereit (Zeit zählt dort nicht)", () =>
        {
            var z = new BereitZucken(() => 0.0);
            for (var i = 0; i < 60 * 30; i++) Pruefe.Wahr(!z.Schritt(Bild, false, false), "kein Zucken außerhalb");
            Pruefe.Wahr(!z.Zuckt, "zuckt nicht");
            var t = 0.0;
            while (!z.Schritt(Bild, true, false) && t < 20) t += Bild;
            Pruefe.Wahr(Math.Abs(t - 3.0) < 0.1, $"in Bereit wieder 3 s, war {t}");
        }),
        ("Zucken: ein Klick unterbricht sofort und setzt neu an; zuckt nicht weiter, bis die Zeit um ist", () =>
        {
            var z = new BereitZucken(() => 0.0);
            while (!z.Schritt(Bild, true, false)) { }
            Pruefe.Wahr(z.Zuckt, "zuckt");
            Pruefe.Wahr(!z.Schritt(Bild, true, true), "Klick");
            Pruefe.Wahr(!z.Zuckt, "unterbrochen");
            var t = 0.0;
            while (!z.Schritt(Bild, true, false) && t < 20) t += Bild;
            Pruefe.Wahr(t > 2.9, $"neues Intervall, war {t}");
            // eine andere Haltung beendet das Zucken ebenfalls
            var y = new BereitZucken(() => 0.0);
            while (!y.Schritt(Bild, true, false)) { }
            y.Schritt(Bild, false, false);
            Pruefe.Wahr(!y.Zuckt, "Haltung gewechselt");
        }),
        ("Zucken: nach dem Clip neues Intervall von 3 bis 6 s", () =>
        {
            var z = new BereitZucken(() => 0.5);
            while (!z.Schritt(Bild, true, false)) { }
            z.Beendet();
            Pruefe.Wahr(!z.Zuckt, "vorbei");
            var t = 0.0;
            while (!z.Schritt(Bild, true, false) && t < 20) t += Bild;
            Pruefe.Wahr(t is > 4.3 and < 4.7, $"4,5 s, war {t}");
        }),
    };
}
