using Reichsgilde;

/// <summary>
/// Zustandsautomat der Schwarzblende (<c>game/scripts/InnenraumUebergang.cs</c>) und die Haltung Text des Handschuhs (<c>HandschuhModell.cs</c>), ohne Godot.
/// </summary>
static class InnenraumUebergangTests
{
    const double Bild = 1.0 / 60.0;

    /// <summary>Läuft Bild für Bild, bis die Bedingung gilt (höchstens 16 Sekunden); liefert die Bilderzahl.</summary>
    static int Bis(InnenraumUebergang u, ref long gezeichnet, Func<bool> bedingung)
    {
        var n = 0;
        while (!bedingung() && n < 1000) { gezeichnet++; u.Schritt(Bild, gezeichnet); n++; }
        return n;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Übergang: voller Ablauf ohne Klinke – Aus, Laden, Warten (2 Bilder), Ein, Ruhe", () =>
        {
            var u = new InnenraumUebergang();
            long g = 100;
            Pruefe.Gleich(UebergangPhase.Ruhe, u.Phase);
            Pruefe.Wahr(u.Starten(UebergangRichtung.Betreten, "betrieb:x", false), "Start");
            Pruefe.Gleich(UebergangPhase.BlendeAus, u.Phase);
            Bis(u, ref g, () => u.Phase == UebergangPhase.Laden);
            Pruefe.Gleich(1.0, u.Alpha);
            Pruefe.Wahr(u.Gesamtdauer is > 0.39 and < 0.43, $"Hineinblenden dauert etwa 0,4 s, war {u.Gesamtdauer}");
            u.LadenFertig(g);
            Pruefe.Gleich(UebergangPhase.Warten, u.Phase);
            var wartet = Bis(u, ref g, () => u.Phase == UebergangPhase.BlendeEin);
            Pruefe.Gleich(InnenraumUebergang.MindestBilder, wartet);
            Pruefe.Gleich(1.0, u.Alpha);
            Bis(u, ref g, () => u.Phase == UebergangPhase.Fertig);
            Pruefe.Gleich(0.0, u.Alpha);
            u.Schritt(Bild, ++g);
            Pruefe.Gleich(UebergangPhase.Ruhe, u.Phase);
            Pruefe.Wahr(!u.Laufend, "nicht mehr laufend");
        }),
        ("Übergang: Gegenprobe – ohne Meldung „Raum fertig“ kein Herausblenden, Alpha bleibt 1", () =>
        {
            var u = new InnenraumUebergang();
            long g = 0;
            u.Starten(UebergangRichtung.Betreten, "dom", false);
            Bis(u, ref g, () => u.Phase == UebergangPhase.Laden);
            for (var i = 0; i < 300; i++) { g++; u.Schritt(Bild, g); } // fünf Sekunden Laden
            Pruefe.Gleich(UebergangPhase.Laden, u.Phase);
            Pruefe.Gleich(1.0, u.Alpha);
        }),
        ("Übergang: Gegenprobe – ein gezeichnetes Bild nach „fertig“ genügt nicht", () =>
        {
            var u = new InnenraumUebergang();
            long g = 0;
            u.Starten(UebergangRichtung.Betreten, "dom", false);
            Bis(u, ref g, () => u.Phase == UebergangPhase.Laden);
            u.LadenFertig(g);
            g++; u.Schritt(Bild, g);
            Pruefe.Gleich(UebergangPhase.Warten, u.Phase);
            g++; u.Schritt(Bild, g);
            Pruefe.Gleich(UebergangPhase.BlendeEin, u.Phase);
        }),
        ("Übergang: gleiche Bildnummer (kein Bild gezeichnet, etwa minimiertes Fenster) hält im Schwarzen", () =>
        {
            var u = new InnenraumUebergang();
            u.Starten(UebergangRichtung.Betreten, "dom", false);
            long g = 5;
            Bis(u, ref g, () => u.Phase == UebergangPhase.Laden);
            u.LadenFertig(g);
            for (var i = 0; i < 100; i++) u.Schritt(Bild, g); // die Zahl der gezeichneten Bilder steigt nicht
            Pruefe.Gleich(UebergangPhase.Warten, u.Phase);
        }),
        ("Übergang: mit Klinke erst 0,7 s Klinke, dann Blende", () =>
        {
            var u = new InnenraumUebergang();
            long g = 0;
            u.Starten(UebergangRichtung.Betreten, "dom", true);
            Pruefe.Gleich(UebergangPhase.Klinke, u.Phase);
            Pruefe.Gleich(0.0, u.Alpha);
            var n = Bis(u, ref g, () => u.Phase == UebergangPhase.BlendeAus);
            Pruefe.Wahr(Math.Abs(n * Bild - InnenraumUebergang.KlinkeSekunden) < 0.05, $"Klinke {n * Bild} s");
            Pruefe.Gleich(0.0, u.Alpha);
        }),
        ("Übergang: Verlassen kennt keine Klinke und läuft wie Betreten", () =>
        {
            var u = new InnenraumUebergang();
            u.Starten(UebergangRichtung.Verlassen, null, true);
            Pruefe.Gleich(UebergangPhase.BlendeAus, u.Phase);
            Pruefe.Wahr(!u.KlinkeNoetig, "keine Klinke beim Verlassen");
        }),
        ("Übergang: Alpha steigt nie fallend und fällt nie steigend (kein Flackern)", () =>
        {
            var u = new InnenraumUebergang();
            long g = 0;
            u.Starten(UebergangRichtung.Betreten, "dom", false);
            var letzte = 0.0;
            while (u.Phase != UebergangPhase.Laden) { g++; u.Schritt(Bild, g); Pruefe.Wahr(u.Alpha >= letzte, "nur steigend"); letzte = u.Alpha; }
            u.LadenFertig(g);
            while (u.Phase != UebergangPhase.BlendeEin) { g++; u.Schritt(Bild, g); }
            while (u.Phase == UebergangPhase.BlendeEin) { g++; u.Schritt(Bild, g); Pruefe.Wahr(u.Alpha <= letzte, "nur fallend"); letzte = u.Alpha; }
        }),
        ("Übergang: Abbruch in der Klinke gibt sofort Ruhe, im Hineinblenden geht es ohne Sprung zurück", () =>
        {
            var u = new InnenraumUebergang();
            long g = 0;
            u.Starten(UebergangRichtung.Betreten, "dom", true);
            g++; u.Schritt(Bild, g);
            Pruefe.Wahr(u.Abbrechen(), "Abbruch in der Klinke");
            Pruefe.Gleich(UebergangPhase.Ruhe, u.Phase);
            Pruefe.Gleich(0.0, u.Alpha);

            u.Starten(UebergangRichtung.Betreten, "dom", false);
            for (var i = 0; i < 12; i++) { g++; u.Schritt(Bild, g); }
            var vorher = u.Alpha;
            Pruefe.Wahr(vorher > 0.1 && vorher < 0.9, "mitten im Hineinblenden");
            Pruefe.Wahr(u.Abbrechen(), "Abbruch im Hineinblenden");
            Pruefe.Gleich(UebergangPhase.BlendeEin, u.Phase);
            g++; u.Schritt(0.0, g);
            Pruefe.Ungefaehr(vorher, u.Alpha, 0.01);
            Bis(u, ref g, () => u.Phase == UebergangPhase.Fertig);
            Pruefe.Gleich(0.0, u.Alpha);
        }),
        ("Übergang: Abbruch im Laden geht nicht (der Raum wird fertig gebaut)", () =>
        {
            var u = new InnenraumUebergang();
            long g = 0;
            u.Starten(UebergangRichtung.Betreten, "dom", false);
            Bis(u, ref g, () => u.Phase == UebergangPhase.Laden);
            Pruefe.Wahr(!u.Abbrechen(), "nicht abbrechbar");
            Pruefe.Gleich(UebergangPhase.Laden, u.Phase);
        }),
        ("Übergang: Notbremse – nach der Höchstdauer wird trotzdem herausgeblendet", () =>
        {
            var u = new InnenraumUebergang();
            long g = 0;
            u.Starten(UebergangRichtung.Betreten, "dom", false);
            var n = Bis(u, ref g, () => u.Phase == UebergangPhase.BlendeEin);
            Pruefe.Wahr(n * Bild >= InnenraumUebergang.HoechstdauerSekunden - 0.1, "erst nach der Höchstdauer");
        }),
        ("Übergang: zweiter Start während des Laufens wird abgelehnt", () =>
        {
            var u = new InnenraumUebergang();
            Pruefe.Wahr(u.Starten(UebergangRichtung.Betreten, "a", false), "erster");
            Pruefe.Wahr(!u.Starten(UebergangRichtung.Verlassen, null, false), "zweiter");
            Pruefe.Gleich("a", u.Ziel);
        }),
        ("Übergang: Hover – Vorladen einmal je Gebäude, neues Gebäude wieder, Hover beendet Ruhe", () =>
        {
            var u = new InnenraumUebergang();
            u.Schweben("a");
            Pruefe.Gleich(UebergangPhase.Vorladen, u.Phase);
            Pruefe.Wahr(u.VorladenNoetig, "noch nicht vorbereitet");
            u.VorladenFertig("a");
            Pruefe.Wahr(!u.VorladenNoetig, "schon vorbereitet");
            u.Schweben("b");
            Pruefe.Wahr(u.VorladenNoetig, "anderes Gebäude");
            u.Schweben(null);
            Pruefe.Gleich(UebergangPhase.Ruhe, u.Phase);
            Pruefe.Wahr(!u.VorladenNoetig, "nichts zu tun");
            u.Schweben("a");
            u.VorladenVergessen();
            Pruefe.Wahr(u.VorladenNoetig, "vergessen");
        }),
        ("Übergang: Schweben ändert nichts, solange ein Übergang läuft", () =>
        {
            var u = new InnenraumUebergang();
            u.Starten(UebergangRichtung.Betreten, "a", false);
            u.Schweben(null);
            Pruefe.Gleich(UebergangPhase.BlendeAus, u.Phase);
            Pruefe.Gleich("a", u.Ziel);
        }),
        ("Übergang: Sanft ist 0 bei 0, 1 bei 1 und monoton", () =>
        {
            Pruefe.Gleich(0.0, InnenraumUebergang.Sanft(-1));
            Pruefe.Gleich(1.0, InnenraumUebergang.Sanft(2));
            var letzte = -1.0;
            for (var i = 0; i <= 20; i++) { var y = InnenraumUebergang.Sanft(i / 20.0); Pruefe.Wahr(y >= letzte, "monoton"); letzte = y; }
        }),

        // ── ohne Haltung Text (über Textfeldern steht jetzt das Messing-I, ZeigerSymboleTests.cs) ──
        ("Handschuh: Wunsch ohne Text – Ziehen schlägt Tür, Tür schlägt Zeigen", () =>
        {
            Pruefe.Gleich(HandPose.Bereit, HandschuhModell.Wunsch(false, true, true));
            Pruefe.Gleich(HandPose.Greifen, HandschuhModell.Wunsch(true, false, true));
            Pruefe.Gleich(HandPose.Zeigen, HandschuhModell.Wunsch(false, true, false));
            Pruefe.Gleich(HandPose.Ruhe, HandschuhModell.Wunsch(false, false));
        }),
        ("Handschuh: kurzer Name der Übergangsclips", () =>
        {
            Pruefe.Gleich("Ruhe_Zeigen", HandschuhModell.UebergangKurz(HandPose.Ruhe, HandPose.Zeigen));
            Pruefe.Gleich("Zeigen", HandschuhModell.Schleife(HandPose.Zeigen));
            Pruefe.Wahr(!Enum.GetNames<HandPose>().Contains("Text"), "keine Haltung Text mehr");
        }),
    };
}
