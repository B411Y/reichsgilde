using Reichsgilde.Core;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Tests für die Haltegründe des Taktgebers: Ratssitzung und Jahresbericht halten im Einzelspiel die Zeit an, und weder die Leertaste
/// (Spielerpause) noch eine Tempotaste hebt das auf. Ist der letzte Grund frei, läuft die Zeit wie vorher weiter.
/// </summary>
static class ZeithaltTests
{
    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Haltegrund: Die Zeit steht, Leertaste und Tempotaste heben ihn nicht auf", () =>
        {
            var takt = new Taktgeber { RundenDauerSekunden = 60 };
            takt.Anhalten("Jahresbericht");
            Pruefe.Wahr(takt.IstPausiert, "Mit Haltegrund muss die Zeit stehen");
            Pruefe.Gleich(0, takt.Voranschreiten(5));
            takt.PauseUmschalten(); // Leertaste
            takt.PauseUmschalten();
            takt.IstPausiert = false; // was eine Tempotaste setzt
            Pruefe.Wahr(takt.IstPausiert, "Gegenprobe: Pause- und Tempotaste dürfen den Haltegrund nicht aufheben");
            Pruefe.Gleich(0, takt.Voranschreiten(5));
            takt.Freigeben("Jahresbericht");
            Pruefe.Wahr(!takt.IstPausiert, "Ohne Grund läuft die Zeit");
            Pruefe.Wahr(takt.Voranschreiten(1) > 0, "Nach dem Freigeben müssen wieder Ticks fällig werden");
        }),

        ("Haltegrund: Reihenfolge Sitzung → Jahresbericht → weiter, Tempo und Spielerpause wie vorher", () =>
        {
            var takt = new Taktgeber { RundenDauerSekunden = 60, Tempo = 4 };
            takt.Anhalten("Ratssitzung");
            takt.Anhalten("Jahresbericht"); // die Sitzung endet: der Jahresbericht hält an, bevor die Sitzung freigibt
            takt.Freigeben("Ratssitzung");
            Pruefe.Wahr(takt.IstPausiert, "Zwischen Sitzung und Jahresbericht darf keine Lücke sein");
            Pruefe.Gleich("Jahresbericht", string.Join(",", takt.Haltegruende));
            takt.Freigeben("Jahresbericht");
            Pruefe.Wahr(!takt.IstPausiert && !takt.Spielerpause, "Danach läuft die Zeit");
            Pruefe.Gleich(4.0, takt.Tempo);
        }),

        ("Haltegrund: Doppelt gesetzt zählt einmal, Freigeben ohne Grund schadet nicht, die Spielerpause bleibt", () =>
        {
            var takt = new Taktgeber();
            takt.IstPausiert = true; // der Spieler hatte selbst angehalten
            takt.Anhalten("Ratssitzung");
            takt.Anhalten("Ratssitzung");
            Pruefe.Gleich(1, takt.Haltegruende.Count);
            takt.Freigeben("Ratssitzung");
            takt.Freigeben("Jahresbericht");
            Pruefe.Wahr(!takt.IstAngehalten, "Kein Grund mehr");
            Pruefe.Wahr(takt.IstPausiert && takt.Spielerpause, "Die eigene Pause des Spielers bleibt nach dem Freigeben");
        }),

        ("Haltegrund: Ein Ereignis am Jahresende hält im selben Bild an, kein weiterer Tick", () =>
        {
            // Letzte Minute des Jahres; das Bild bringt viele Ticks, der Jahreswechsel hält an (wie die Hauptszene im Einzelspiel)
            var sim = new Simulation(zeit: new Spielzeit(Spielzeit.MinutenProTag - 1));
            sim.Takt.RundenDauerSekunden = 60;
            sim.Takt.Tempo = 8;
            var jahre = 0;
            sim.JahrBeendet += _ => { jahre++; sim.Takt.Anhalten("Jahresbericht"); };
            sim.Aktualisieren(2); // weit mehr als ein Tick fällig
            Pruefe.Gleich(1, jahre);
            Pruefe.Gleich((long)Spielzeit.MinutenProTag, sim.Zeit.VergangeneMinuten);
            // Gegenprobe: Solange der Grund steht, läuft die Zeit auch über viele Bilder nicht weiter
            for (var i = 0; i < 30; i++) sim.Aktualisieren(1);
            Pruefe.Gleich((long)Spielzeit.MinutenProTag, sim.Zeit.VergangeneMinuten);
            sim.Takt.Freigeben("Jahresbericht");
            sim.Aktualisieren(1);
            Pruefe.Wahr(sim.Zeit.VergangeneMinuten > Spielzeit.MinutenProTag, "Nach dem Schließen läuft die Zeit weiter");
        }),

        ("Zeithalt: feste Reihenfolge Stimmrunde → Ratssitzung → Jahresbericht, im Mehrspieler kein Grund", () =>
        {
            Pruefe.Gleich("Stimmrunde", string.Join(",", Zeithalt.Gruende(true, true, false, false, false)));
            Pruefe.Gleich("Ratssitzung", string.Join(",", Zeithalt.Gruende(true, false, true, false, true))); // der Bericht wartet: die Sitzung hält
            Pruefe.Gleich("Jahresbericht", string.Join(",", Zeithalt.Gruende(true, false, false, true, false)));
            Pruefe.Gleich("", string.Join(",", Zeithalt.Gruende(true, false, false, false, false)));
            // Gegenprobe Mehrspieler: nichts hält an, auch nicht die offene Stimmrunde
            Pruefe.Gleich("", string.Join(",", Zeithalt.Gruende(false, true, true, true, true)));
        }),

        ("Zeithalt: Einzelspiel – Zeit steht in der Stimmrunde bis „Fertig“, dann Sitzung, dann Jahresbericht, dann weiter im alten Tempo", () =>
        {
            // 21:00 am letzten Tag: die Stimmrunde beginnt
            var sim = new Simulation(zeit: new Spielzeit(Spielzeit.MinutenProTag - 120));
            sim.Takt.RundenDauerSekunden = 60;
            sim.Takt.Tempo = 4;
            bool stimmrunde = true, sitzung = false, bericht = false;
            var log = new List<string>();
            void Abgleichen() => log.AddRange(Zeithalt.Abgleichen(sim.Takt, Zeithalt.Gruende(true, stimmrunde, sitzung, bericht, false)));
            sim.JahrBeendet += _ => { sitzung = true; Abgleichen(); }; // wie die Hauptszene: die Szene wird vorgemerkt
            Abgleichen();
            var start = sim.Zeit.VergangeneMinuten;
            for (var i = 0; i < 20; i++) { sim.Takt.PauseUmschalten(); sim.Takt.IstPausiert = false; sim.Aktualisieren(1); } // Leertaste, Tempotaste: wirkungslos
            Pruefe.Gleich(start, sim.Zeit.VergangeneMinuten);
            sim.Takt.IstPausiert = false; // die eigene Pause wie vor der Stimmrunde
            stimmrunde = false; Abgleichen(); // „Fertig“
            for (var i = 0; i < 40 && !sitzung; i++) sim.Aktualisieren(1);
            Pruefe.Wahr(sitzung, "Nach „Fertig“ läuft die Zeit bis zum Jahresende");
            Pruefe.Gleich((long)Spielzeit.MinutenProTag, sim.Zeit.VergangeneMinuten); // kein Tick des neuen Jahres
            for (var i = 0; i < 10; i++) sim.Aktualisieren(1);
            Pruefe.Gleich((long)Spielzeit.MinutenProTag, sim.Zeit.VergangeneMinuten);
            bericht = true; sitzung = false; Abgleichen(); // Szene zu, Jahresbericht auf
            for (var i = 0; i < 10; i++) sim.Aktualisieren(1);
            Pruefe.Gleich((long)Spielzeit.MinutenProTag, sim.Zeit.VergangeneMinuten);
            bericht = false; Abgleichen(); // „Weiter“
            sim.Aktualisieren(1);
            Pruefe.Wahr(sim.Zeit.VergangeneMinuten > Spielzeit.MinutenProTag, "Nach dem Jahresbericht läuft die Zeit");
            Pruefe.Gleich(4.0, sim.Takt.Tempo);
            Pruefe.Gleich("angehalten (Stimmrunde)|frei (Stimmrunde)|angehalten (Ratssitzung)|angehalten (Jahresbericht)|frei (Ratssitzung)|frei (Jahresbericht)",
                string.Join("|", log.Select(z => z[..z.IndexOf(" –", StringComparison.Ordinal)])));
        }),

        ("Zeithalt: Mehrspieler – die Zeit läuft während der Stimmrunde weiter", () =>
        {
            var sim = new Simulation(zeit: new Spielzeit(Spielzeit.MinutenProTag - 120));
            sim.Takt.RundenDauerSekunden = 60;
            Zeithalt.Abgleichen(sim.Takt, Zeithalt.Gruende(false, true, false, false, false));
            var start = sim.Zeit.VergangeneMinuten;
            sim.Aktualisieren(1);
            Pruefe.Wahr(sim.Zeit.VergangeneMinuten > start, "Im Mehrspieler hält die Stimmrunde die Zeit nicht an");
        }),
    };
}
