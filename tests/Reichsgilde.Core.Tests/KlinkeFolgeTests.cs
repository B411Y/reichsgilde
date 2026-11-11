using Reichsgilde;

/// <summary>Klinke-Folge der Hand (<c>game/scripts/KlinkeFolge.cs</c>), Haltung Bereit (<c>HandschuhModell.cs</c>) und Klinke in der Blende, ohne Godot.</summary>
static class KlinkeFolgeTests
{
    const double Bild = 1.0 / 60.0, Greifen = 0.417, Druecken = 0.833, Loslassen = 0.5;

    static KlinkeFolge Gestartet(out double? vorlauf)
    {
        var k = new KlinkeFolge();
        vorlauf = k.Starten(true, Greifen, Druecken, Loslassen);
        return k;
    }

    static double Bis(KlinkeFolge k, Func<bool> bedingung)
    {
        var t = 0.0;
        while (!bedingung() && t < 20) { k.Schritt(Bild); t += Bild; }
        return t;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Klinke: Ablauf Greifen, Drücken, Gehalten – Clips und Schloss-Aktionen", () =>
        {
            var k = Gestartet(out var vorlauf);
            Pruefe.Wahr(vorlauf is { } v && Math.Abs(v - (Greifen + Druecken * KlinkeFolge.VorlaufAnteil)) < 1e-9, "Vorlauf bis zur Blende");
            Pruefe.Gleich(KlinkePhase.Greifen, k.Phase);
            Pruefe.Gleich("Klinke_greifen", k.Clip);
            Pruefe.Gleich((string?)null, k.SchlossAktion);
            var t = Bis(k, () => k.Phase == KlinkePhase.Druecken);
            Pruefe.Wahr(Math.Abs(t - Greifen) < 0.03, $"Greifen dauert {Greifen} s, war {t}");
            Pruefe.Gleich("Klinke_druecken", k.Clip);
            Pruefe.Gleich("Druecken", k.SchlossAktion);
            t = Bis(k, () => k.Phase == KlinkePhase.Gehalten);
            Pruefe.Wahr(Math.Abs(t - Druecken) < 0.03, $"Drücken dauert {Druecken} s, war {t}");
            Pruefe.Gleich((string?)null, k.Clip);
            Pruefe.Wahr(k.Laeuft, "gehalten läuft weiter");
        }),
        ("Klinke: Gegenprobe – Gehalten bleibt, bis jemand loslässt (keine Eigenbewegung)", () =>
        {
            var k = Gestartet(out _);
            Bis(k, () => k.Phase == KlinkePhase.Gehalten);
            for (var i = 0; i < 600; i++) k.Schritt(Bild);
            Pruefe.Gleich(KlinkePhase.Gehalten, k.Phase);
        }),
        ("Klinke: Loslassen endet in Aus und der Clip Klinke_loslassen mit Schloss-Aktion Loslassen", () =>
        {
            var k = Gestartet(out _);
            Bis(k, () => k.Phase == KlinkePhase.Gehalten);
            Pruefe.Wahr(k.Loslassen(), "loslassen");
            Pruefe.Gleich("Klinke_loslassen", k.Clip);
            Pruefe.Gleich("Loslassen", k.SchlossAktion);
            var t = Bis(k, () => k.Phase == KlinkePhase.Aus);
            Pruefe.Wahr(Math.Abs(t - Loslassen) < 0.03, $"Loslassen dauert {Loslassen} s, war {t}");
            Pruefe.Wahr(!k.Laeuft, "nicht mehr laufend");
        }),
        ("Klinke: Abbruch mitten im Greifen oder Drücken lässt auch los", () =>
        {
            var k = Gestartet(out _);
            for (var i = 0; i < 5; i++) k.Schritt(Bild);
            Pruefe.Wahr(k.Loslassen(), "aus dem Greifen");
            Pruefe.Gleich(KlinkePhase.Loslassen, k.Phase);
            var k2 = Gestartet(out _);
            Bis(k2, () => k2.Phase == KlinkePhase.Druecken);
            for (var i = 0; i < 5; i++) k2.Schritt(Bild);
            Pruefe.Wahr(k2.Loslassen(), "aus dem Drücken");
        }),
        ("Klinke: Gegenprobe – ohne Haltung Bereit keine Folge, kein zweiter Start, Loslassen ohne Folge tut nichts", () =>
        {
            var k = new KlinkeFolge();
            Pruefe.Wahr(k.Starten(false, Greifen, Druecken, Loslassen) is null, "nicht bereit");
            Pruefe.Wahr(!k.Loslassen(), "nichts loszulassen");
            Pruefe.Wahr(k.Starten(true, 0, Druecken, Loslassen) is null, "Clip ohne Länge (fehlt in der Datei)");
            Pruefe.Gleich(KlinkePhase.Aus, k.Phase);
            Pruefe.Wahr(k.Starten(true, Greifen, Druecken, Loslassen) is not null, "bereit");
            Pruefe.Wahr(k.Starten(true, Greifen, Druecken, Loslassen) is null, "zweiter Start");
            Pruefe.Wahr(k.Schritt(Bild) == false, "ein Bild ändert die Stufe nicht");
        }),
        ("Klinke: Schritt meldet genau die Stufenwechsel", () =>
        {
            var k = Gestartet(out _);
            var wechsel = 0;
            for (var i = 0; i < 200; i++) if (k.Schritt(Bild)) wechsel++;
            Pruefe.Gleich(2, wechsel); // Greifen -> Drücken -> Gehalten
            k.Loslassen();
            for (var i = 0; i < 200; i++) if (k.Schritt(Bild)) wechsel++;
            Pruefe.Gleich(3, wechsel); // Loslassen -> Aus
        }),
        ("Klinke: Blende wartet genau die Vorlaufzeit, dann Schwarz", () =>
        {
            var k = Gestartet(out var vorlauf);
            var u = new InnenraumUebergang();
            u.Starten(UebergangRichtung.Betreten, "dom", true, vorlauf!.Value);
            Pruefe.Gleich(UebergangPhase.Klinke, u.Phase);
            long g = 0;
            var t = 0.0;
            while (u.Phase == UebergangPhase.Klinke && t < 5) { g++; u.Schritt(Bild, g); t += Bild; }
            Pruefe.Wahr(Math.Abs(t - vorlauf.Value) < 0.03, $"Klinke dauert {vorlauf}, war {t}");
            Pruefe.Gleich(UebergangPhase.BlendeAus, u.Phase);
            Pruefe.Gleich(0.0, u.Alpha);
        }),
        ("Klinke: Blende ohne eigene Dauer nimmt die Vorgabe von 0,7 s", () =>
        {
            var u = new InnenraumUebergang();
            u.Starten(UebergangRichtung.Betreten, "dom", true);
            long g = 0;
            var t = 0.0;
            while (u.Phase == UebergangPhase.Klinke && t < 5) { g++; u.Schritt(Bild, g); t += Bild; }
            Pruefe.Wahr(Math.Abs(t - InnenraumUebergang.KlinkeSekunden) < 0.03, $"Vorgabe {InnenraumUebergang.KlinkeSekunden}, war {t}");
        }),
        ("Haltung Bereit: Tür schlägt Zeigen, Text und Ziehen schlagen die Tür", () =>
        {
            Pruefe.Gleich(HandPose.Bereit, HandschuhModell.Wunsch(false, true, false, true));
            Pruefe.Gleich(HandPose.Zeigen, HandschuhModell.Wunsch(false, true, false, false));
            Pruefe.Gleich(HandPose.Text, HandschuhModell.Wunsch(false, true, true, true));
            Pruefe.Gleich(HandPose.Greifen, HandschuhModell.Wunsch(true, true, false, true));
            Pruefe.Gleich(HandPose.Ruhe, HandschuhModell.Wunsch(false, false, false, false));
        }),
        ("Haltung Bereit: Übergänge aus Ruhe, Zeigen, Text direkt, aus Greifen über Ruhe; Schleife heißt Klinke_bereit", () =>
        {
            var a = new HandschuhAblauf();
            Pruefe.Gleich("Uebergang_Ruhe_Bereit", a.Entscheiden(HandPose.Bereit));
            a.EinmalFertig();
            Pruefe.Gleich("Klinke_bereit", HandschuhModell.Schleife(a.Pose));
            Pruefe.Gleich("Uebergang_Bereit_Zeigen", a.Entscheiden(HandPose.Zeigen));
            a.EinmalFertig();
            Pruefe.Gleich("Uebergang_Zeigen_Bereit", a.Entscheiden(HandPose.Bereit));
            a.EinmalFertig();
            Pruefe.Gleich("Uebergang_Bereit_Text", a.Entscheiden(HandPose.Text));
            a.EinmalFertig();
            Pruefe.Gleich("Uebergang_Text_Bereit", a.Entscheiden(HandPose.Bereit));
            a.EinmalFertig();
            Pruefe.Gleich("Uebergang_Bereit_Ruhe", a.Entscheiden(HandPose.Greifen)); // kein Clip Bereit->Greifen
            a.EinmalFertig();
            Pruefe.Gleich("Uebergang_Ruhe_Greifen", a.Entscheiden(HandPose.Greifen));
        }),
        ("Haltung Bereit: Klick auf die Tür tippt nicht und lässt die Hand in Bereit (die Folge übernimmt)", () =>
        {
            var a = new HandschuhAblauf();
            a.Entscheiden(HandPose.Bereit); a.EinmalFertig();
            a.TippenMerken();
            Pruefe.Gleich((string?)null, a.Entscheiden(HandPose.Bereit));
            Pruefe.Gleich(HandPose.Bereit, a.Pose);
            // zum Vergleich: über Anklickbarem ohne Tür tippt sie
            var b = new HandschuhAblauf();
            b.Entscheiden(HandPose.Zeigen); b.EinmalFertig();
            b.TippenMerken();
            Pruefe.Gleich("Tippen", b.Entscheiden(HandPose.Zeigen));
        }),
        ("Klickregel: der erste Klick zeigt die Tafel, Einzelklick bleibt offen", () =>
        {
            Pruefe.Gleich(KlickFolge.TafelZeigen, KlickRegel.Entscheide(false, true, false));
            Pruefe.Gleich(KlickFolge.TafelZeigen, KlickRegel.Entscheide(false, false, false));
        }),
        ("Klickregel: Doppelklick auf ein betretbares Gebäude schließt die Tafel und betritt (Klinke/Blende)", () =>
        {
            Pruefe.Gleich(KlickFolge.TafelSchliessenUndBetreten, KlickRegel.Entscheide(true, true, false));
        }),
        ("Klickregel: Gegenprobe – Doppelklick ohne Innenraum zeigt die Tafel, während eines Übergangs zählt kein Klick", () =>
        {
            Pruefe.Gleich(KlickFolge.TafelZeigen, KlickRegel.Entscheide(true, false, false));
            Pruefe.Gleich(KlickFolge.Nichts, KlickRegel.Entscheide(true, true, true));
            Pruefe.Gleich(KlickFolge.Nichts, KlickRegel.Entscheide(false, true, true));
        }),
        ("Handschuh Text: Hand steht aufrecht – Drehung gleicht die Kamera aus, Zeigen bleibt schräg", () =>
        {
            Pruefe.Gleich(0.0, HandschuhModell.PoseDrehungGrad(0));
            Pruefe.Gleich(-40.0, HandschuhModell.PoseDrehungGrad(1));
            Pruefe.Gleich(-40.0, HandschuhModell.PoseDrehungGrad(3)); // Gewicht wird begrenzt
            Pruefe.Gleich(40.0, HandschuhModell.FingerBildwinkelGrad(HandschuhModell.KameraDrehungGrad, HandschuhModell.PoseDrehungGrad(0)));
            Pruefe.Gleich(0.0, HandschuhModell.FingerBildwinkelGrad(HandschuhModell.KameraDrehungGrad, HandschuhModell.PoseDrehungGrad(1)));
            var halb = HandschuhModell.FingerBildwinkelGrad(HandschuhModell.KameraDrehungGrad, HandschuhModell.PoseDrehungGrad(0.5));
            Pruefe.Ungefaehr(20.0, halb, 1e-9); // beim Gleiten stetig
        }),
        ("Schlosseinwuchs: dauert genau die Übergangszeit der Hand (7 Bilder bei 24/s) und folgt Smoothstep", () =>
        {
            var e = new SchlossEinwuchs();
            Pruefe.Ungefaehr(0.2917, SchlossEinwuchs.UebergangSekunden, 0.001);
            e.ZielSetzen(1);
            var t = 0.0;
            while (e.T < 1 && t < 2) { e.Schritt(1.0 / 240); t += 1.0 / 240; }
            Pruefe.Ungefaehr(SchlossEinwuchs.UebergangSekunden, t, 0.03);
            Pruefe.Gleich(1.0, e.S);
            // halbe Zeit: Smoothstep(0,5) = 0,5; ein Viertel: 0,15625
            var f = new SchlossEinwuchs(); f.ZielSetzen(1); f.Schritt(SchlossEinwuchs.UebergangSekunden / 4);
            Pruefe.Ungefaehr(0.15625, f.S, 1e-9);
        }),
        ("Schlosseinwuchs: kein Sprung – am Anfang 0, Austritt rückwärts gleich lang, Umkehr mittendrin stetig", () =>
        {
            var e = new SchlossEinwuchs();
            Pruefe.Gleich(0.0, e.S);
            e.ZielSetzen(1);
            e.Schritt(SchlossEinwuchs.UebergangSekunden * 0.4);
            var mitte = e.S;
            e.ZielSetzen(0); // Umkehr mitten im Wachsen
            e.Schritt(0.0);
            Pruefe.Gleich(mitte, e.S);
            e.Schritt(SchlossEinwuchs.UebergangSekunden * 0.4);
            Pruefe.Wahr(e.S < 1e-9, "rückwärts mit gleichem Tempo wieder bei 0");
            e.ZielSetzen(1); e.Schritt(10); e.ZielSetzen(0);
            var t = 0.0;
            while (e.T > 0 && t < 2) { e.Schritt(1.0 / 240); t += 1.0 / 240; }
            Pruefe.Ungefaehr(SchlossEinwuchs.UebergangSekunden, t, 0.03);
        }),
        ("Schlosseinwuchs: Gegenprobe – Ziel bleibt, ohne neuen Übergang ändert sich nichts; fremde Clips ändern das Ziel nicht", () =>
        {
            var e = new SchlossEinwuchs();
            e.ZielSetzen(1); e.Schritt(5);
            e.Schritt(5);
            Pruefe.Gleich(1.0, e.S);
            Pruefe.Wahr(SchlossEinwuchs.ZielFuerClip("Uebergang_Ruhe_Zeigen") is null, "Ruhe_Zeigen");
            Pruefe.Wahr(SchlossEinwuchs.ZielFuerClip("Tippen") is null, "Tippen");
            Pruefe.Gleich(1.0, SchlossEinwuchs.ZielFuerClip("Uebergang_Ruhe_Bereit")!.Value);
            Pruefe.Gleich(1.0, SchlossEinwuchs.ZielFuerClip("Uebergang_Text_Bereit")!.Value);
            Pruefe.Gleich(0.0, SchlossEinwuchs.ZielFuerClip("Uebergang_Bereit_Zeigen")!.Value);
            Pruefe.Gleich(0.0, SchlossEinwuchs.ZielFuerClip("Uebergang_Bereit_Ruhe")!.Value);
        }),
        ("Klinke: Ruhelage oben in Aus (Bereit, Übergang) und Greifen; nach unten nur Drücken, Halten, Loslassen", () =>
        {
            var k = new KlinkeFolge();
            Pruefe.Wahr(k.KlinkeInRuhelage, "Aus = Bereit und Übergänge: Klinke oben");
            Pruefe.Gleich((string?)null, k.SchlossAktion);
            k.Starten(true, Greifen, Druecken, Loslassen);
            Pruefe.Wahr(k.KlinkeInRuhelage, "Greifen: Klinke noch oben");
            Pruefe.Gleich((string?)null, k.SchlossAktion); // Druecken läuft nicht vor dem Drücken der Hand
            Bis(k, () => k.Phase == KlinkePhase.Druecken);
            Pruefe.Wahr(!k.KlinkeInRuhelage, "Drücken: Klinke geht nach unten");
            Pruefe.Gleich("Druecken", k.SchlossAktion);
            Bis(k, () => k.Phase == KlinkePhase.Gehalten);
            Pruefe.Wahr(!k.KlinkeInRuhelage, "Halten: unten");
            Pruefe.Gleich((string?)null, k.SchlossAktion);
            k.Loslassen();
            Pruefe.Wahr(!k.KlinkeInRuhelage, "Loslassen: kommt hoch");
            Pruefe.Gleich("Loslassen", k.SchlossAktion);
            Bis(k, () => k.Phase == KlinkePhase.Aus);
            Pruefe.Wahr(k.KlinkeInRuhelage, "danach wieder oben");
        }),
        ("Text aufrecht: Drehung für Finger nach oben – aus jeder Ausgangslage, auch kopfüber", () =>
        {
            foreach (var ausgang in new[] { 0.0, 40.0, -40.0, 90.0, 180.0, -135.0 })
            {
                // Richtung Stulpe -> Fingerspitze im Bild, y nach unten: bei Drehung g um die Hochachse dreht die Richtung mit; bei g = -ausgang zeigt sie nach oben
                (double X, double Y) Richtung(double g) { var w = (g + ausgang) * Math.PI / 180.0; return (Math.Sin(w), -Math.Cos(w)); }
                var diff = Math.Abs(((HandschuhModell.AufrechtDrehungGrad(Richtung) + ausgang) % 360.0 + 540.0) % 360.0 - 180.0);
                Pruefe.Wahr(diff < 0.1, $"Ausgangslage {ausgang}: Abweichung {diff}");
            }
        }),
        ("Text aufrecht: Gegenprobe – kopfüber (Finger unten) wird nicht für aufrecht gehalten", () =>
        {
            (double X, double Y) KopfUeber(double g) => (0.0, 1.0); // zeigt immer nach unten: bester Fehler ist 180 Grad, der Löser findet keine bessere Drehung als irgendeine
            (double X, double Y) Dreht(double g) { var w = g * Math.PI / 180.0; return (Math.Sin(w), Math.Cos(w)); } // bei 0 nach unten
            var g0 = HandschuhModell.AufrechtDrehungGrad(Dreht);
            Pruefe.Wahr(Math.Abs(Math.Abs(g0) - 180.0) < 0.1, $"nach unten bei 0, aufrecht erst bei 180 Grad, war {g0}");
            Pruefe.Gleich(1.0, KopfUeber(0).Y);
        }),
        ("Haltung Bereit: PoseSetzen stellt die Haltung ohne Clip (nach dem Loslassen)", () =>
        {
            var a = new HandschuhAblauf();
            a.PoseSetzen(HandPose.Bereit);
            Pruefe.Gleich(HandPose.Bereit, a.Pose);
            Pruefe.Wahr(!a.EinmalLaeuft, "kein Einmalclip");
            Pruefe.Gleich((string?)null, a.Entscheiden(HandPose.Bereit));
        }),
    };
}
