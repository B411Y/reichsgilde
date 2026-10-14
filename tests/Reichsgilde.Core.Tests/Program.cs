using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Zeit;

// ─────────────────────────────────────────────────────────────────────────────
//  Automatische Tests für den Simulationskern
//  Start:  dotnet run --project tests/Reichsgilde.Core.Tests
//
//  Jeder Test ist ein Name + eine Funktion. Schlägt eine Prüfung fehl,
//  wird eine Ausnahme geworfen und der Test als FEHLER gemeldet.
// ─────────────────────────────────────────────────────────────────────────────

// Balance-Messungen statt Tests:  dotnet run --project tests/Reichsgilde.Core.Tests -- messen [Jahre] [Startwerte] [Volk]
if (args.Length > 0 && args[0] == "messen")
    return Messung.Dynastie(args[1..]);
// Spielstand für Klicktests:  dotnet run --project tests/Reichsgilde.Core.Tests -- spielstand gericht <Datei>
if (args.Length > 2 && args[0] == "spielstand" && args[1] == "gericht")
    return Messung.GerichtsSpielstand(args[2]);
if (args.Length > 2 && args[0] == "spielstand" && args[1] == "familie")
    return Messung.FamilienSpielstand(args[2]);
if (args.Length > 2 && args[0] == "spielstand" && args[1] == "meister")
    return Messung.MeisterSpielstand(args[2], args.Length > 3 ? int.Parse(args[3]) : 0);
if (args.Length > 2 && args[0] == "spielstand" && args[1] == "gesundheit")
    return Messung.FamilienSpielstand(args[2], pest: true);

var tests = new List<(string Name, Action Pruefung)>
{
    // ── Spielzeit ───────────────────────────────────────────────────────────
    ("Spiel beginnt im Frühling 1400 um 06:00 zur Zunftglocke", () =>
    {
        var zeit = new Spielzeit();
        Pruefe.Gleich(1400, zeit.Jahr);
        Pruefe.Gleich(1, zeit.Runde);
        Pruefe.Gleich(Jahreszeit.Fruehling, zeit.Jahreszeit);
        Pruefe.Gleich("06:00", zeit.UhrzeitText);
        Pruefe.Gleich(Tagesphase.Zunftglocke, zeit.Tagesphase);
        Pruefe.Gleich("Frühling 1400", zeit.DatumText);
    }),

    ("Jahreszeiten wechseln jedes Jahr: Frühling 1400, Sommer 1401, Herbst 1402, Winter 1403, Frühling 1404", () =>
    {
        var erwartet = new[] { Jahreszeit.Fruehling, Jahreszeit.Sommer, Jahreszeit.Herbst, Jahreszeit.Winter, Jahreszeit.Fruehling };
        for (var jahr = 0; jahr < erwartet.Length; jahr++)
        {
            var zeit = new Spielzeit(jahr * (long)Spielzeit.MinutenProTag);
            Pruefe.Gleich(1400 + jahr, zeit.Jahr);
            Pruefe.Gleich(erwartet[jahr], zeit.Jahreszeit);
        }
    }),

    ("Tagesphasen liegen an den richtigen Uhrzeiten", () =>
    {
        // Minuten nach Tagesbeginn (06:00) → erwartete Uhrzeit und Phase
        (int minute, string uhr, Tagesphase phase)[] faelle =
        {
            (0,    "06:00", Tagesphase.Zunftglocke),
            (299,  "10:59", Tagesphase.Zunftglocke),
            (300,  "11:00", Tagesphase.HoherMittag),
            (600,  "16:00", Tagesphase.Vesper),
            (900,  "21:00", Tagesphase.DunkleNacht),
            (1080, "00:00", Tagesphase.DunkleNacht),
            (1439, "05:59", Tagesphase.DunkleNacht),
        };
        foreach (var (minute, uhr, phase) in faelle)
        {
            var zeit = new Spielzeit(minute);
            Pruefe.Gleich(uhr, zeit.UhrzeitText, $"Uhrzeit bei Minute {minute}");
            Pruefe.Gleich(phase, zeit.Tagesphase, $"Phase bei Minute {minute}");
        }
    }),

    // ── Taktgeber ───────────────────────────────────────────────────────────
    ("Standard: 15 Minuten Echtzeit ergeben genau ein Spieljahr", () =>
    {
        var sim = new Simulation();
        SimuliereEchtzeit(sim, sekunden: 15 * 60);
        Pruefe.Gleich(1401, sim.Zeit.Jahr);
        Pruefe.Gleich("06:00", sim.Zeit.UhrzeitText);
    }),

    ("Pause hält die Zeit an", () =>
    {
        var sim = new Simulation();
        sim.Takt.IstPausiert = true;
        SimuliereEchtzeit(sim, sekunden: 600);
        Pruefe.Gleich(0L, sim.Zeit.VergangeneMinuten);
    }),

    ("Tempo 2 halbiert die Echtzeit pro Jahr", () =>
    {
        var sim = new Simulation();
        sim.Takt.Tempo = 2;
        SimuliereEchtzeit(sim, sekunden: 450);
        Pruefe.Gleich(1401, sim.Zeit.Jahr);
    }),

    ("Rundendauer ist einstellbar (Option): 5 Minuten pro Jahr", () =>
    {
        var sim = new Simulation();
        sim.Takt.RundenDauerSekunden = 5 * 60;
        SimuliereEchtzeit(sim, sekunden: 5 * 60);
        Pruefe.Gleich(1401, sim.Zeit.Jahr);
    }),

    ("Rundendauer wird auf sinnvolle Grenzen begrenzt", () =>
    {
        var takt = new Taktgeber { RundenDauerSekunden = 1 };
        Pruefe.Gleich(Taktgeber.MinRundenDauerSekunden, takt.RundenDauerSekunden);
    }),

    ("Ein langer Ruckler holt nicht unbegrenzt Zeit nach", () =>
    {
        var takt = new Taktgeber();
        Pruefe.Gleich(Taktgeber.MaxTicksProAufruf, takt.Voranschreiten(10_000));
    }),

    // ── Ereignisse ──────────────────────────────────────────────────────────
    ("Jahresabrechnung wird genau einmal pro Jahr ausgelöst", () =>
    {
        var sim = new Simulation();
        var beendeteJahre = new List<int>();
        sim.JahrBeendet += jahr => beendeteJahre.Add(jahr);

        for (var i = 0; i < 3 * Spielzeit.MinutenProTag; i++)
            sim.Tick();

        Pruefe.Gleich("1400,1401,1402", string.Join(",", beendeteJahre));
    }),

    ("Phasenwechsel werden in der richtigen Reihenfolge gemeldet", () =>
    {
        var sim = new Simulation();
        var phasen = new List<Tagesphase>();
        sim.PhaseBegonnen += phase => phasen.Add(phase);

        for (var i = 0; i < Spielzeit.MinutenProTag; i++)
            sim.Tick();

        Pruefe.Gleich("HoherMittag,Vesper,DunkleNacht,Zunftglocke", string.Join(",", phasen));
    }),

    // ── Partie-Einstellungen ────────────────────────────────────────────────
    ("Standard-Einstellungen: 4 AP, 15 Minuten, Endjahr 1600 – und gültig", () =>
    {
        var e = Partieeinstellungen.Standard;
        Pruefe.Gleich(4, e.AktionspunkteProRunde);
        Pruefe.Gleich(15.0, e.RundenDauerMinuten);
        Pruefe.Gleich(Siegbedingung.Endjahr, e.Siegbedingung);
        Pruefe.Gleich(1600, e.Endjahr);
        Pruefe.Gleich(0, e.Pruefen().Count);
    }),

    ("Ungültige Einstellungen werden mit Meldung abgelehnt", () =>
    {
        var e = Partieeinstellungen.Standard with { AktionspunkteProRunde = 0, Endjahr = 1399 };
        Pruefe.Gleich(2, e.Pruefen().Count);
        Pruefe.Wirft(() => new Simulation(e));
    }),

    ("Rundendauer aus den Einstellungen wird an den Taktgeber übergeben", () =>
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { RundenDauerMinuten = 5 });
        Pruefe.Gleich(300.0, sim.Takt.RundenDauerSekunden);
    }),

    // ── Spielende ───────────────────────────────────────────────────────────
    ("Siegbedingung Endjahr: Partie endet mit Beginn des Endjahres", () =>
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Endjahr = 1402 });
        var beendet = 0;
        sim.PartieBeendet += () => beendet++;

        for (var i = 0; i < 5 * Spielzeit.MinutenProTag; i++)
            sim.Tick();

        Pruefe.Gleich(true, sim.IstBeendet);
        Pruefe.Gleich(1, beendet);
        Pruefe.Gleich(1402, sim.Zeit.Jahr);
        Pruefe.Gleich("06:00", sim.Zeit.UhrzeitText); // danach läuft keine Zeit mehr
        Pruefe.Gleich(0, sim.Aktualisieren(60));
    }),

    ("Siegbedingungen entscheiden richtig", () =>
    {
        var basis = Partieeinstellungen.Standard;
        var stand = new Spielstand(Jahr: 1450, LebendeDynastien: 3, HoechstesVermoegen: 150_000);

        Pruefe.Gleich(false, Spielende.IstErreicht(basis, stand));
        Pruefe.Gleich(true, Spielende.IstErreicht(basis with { Endjahr = 1450 }, stand));
        Pruefe.Gleich(false, Spielende.IstErreicht(basis with { Siegbedingung = Siegbedingung.Zielvermoegen }, stand));
        Pruefe.Gleich(true, Spielende.IstErreicht(basis with { Siegbedingung = Siegbedingung.Zielvermoegen, Zielvermoegen = 100_000 }, stand));
        Pruefe.Gleich(false, Spielende.IstErreicht(basis with { Siegbedingung = Siegbedingung.LetzteDynastie }, stand));
        Pruefe.Gleich(true, Spielende.IstErreicht(basis with { Siegbedingung = Siegbedingung.LetzteDynastie }, stand with { LebendeDynastien = 1 }));
        Pruefe.Gleich(false, Spielende.IstErreicht(basis with { Siegbedingung = Siegbedingung.Endlos }, stand with { Jahr = 9999 }));
    }),

    ("Endloses Spiel läuft über 1600 hinaus", () =>
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                                 new Spielzeit(199L * Spielzeit.MinutenProTag)); // Frühjahr 1599
        for (var i = 0; i < 2 * Spielzeit.MinutenProTag; i++)
            sim.Tick();
        Pruefe.Gleich(false, sim.IstBeendet);
        Pruefe.Gleich(1601, sim.Zeit.Jahr);
    }),

    // ── Zahlenformat ────────────────────────────────────────────────────────
    ("Zahlenformat: deutsch und unabhängig von der Systemeinstellung", () =>
    {
        Pruefe.Gleich("4.000", Zahlenformat.Ganz(4000));
        Pruefe.Gleich("1.234.567", Zahlenformat.Ganz(1234567.4));
        Pruefe.Gleich("12,6", Zahlenformat.Kommazahl(12.63));
        Pruefe.Gleich("+627", Zahlenformat.MitVorzeichen(627));
        Pruefe.Gleich("−1.120", Zahlenformat.MitVorzeichen(-1120));
    }),

    // ── Aktionspunkte ───────────────────────────────────────────────────────
    ("Aktionspunkte: neue Runde addiert, Rest wird übertragen", () =>
    {
        var ap = new Aktionspunkte();
        ap.NeueRunde(4);
        Pruefe.Gleich(true, ap.Ausgeben(3));
        ap.NeueRunde(4, bonus: 1);   // z. B. 6. Meistergrad
        Pruefe.Gleich(6, ap.Vorrat); // 1 Rest + 4 + 1 Bonus
    }),

    ("Aktionspunkte: zu teure Maßnahme wird abgelehnt und kostet nichts", () =>
    {
        var ap = new Aktionspunkte(startVorrat: 3);
        Pruefe.Gleich(false, ap.Ausgeben(8)); // z. B. Berufswechsel
        Pruefe.Gleich(3, ap.Vorrat);
    }),
};

tests.AddRange(WirtschaftTests.Alle());
tests.AddRange(LogistikTests.Alle());
tests.AddRange(KiTests.Alle());
tests.AddRange(BefehlTests.Alle());
tests.AddRange(StandTests.Alle());
tests.AddRange(LiegenschaftTests.Alle());
tests.AddRange(SpeicherTests.Alle());
tests.AddRange(RatTests.Alle());
tests.AddRange(DynastieTests.Alle());
tests.AddRange(GerichtTests.Alle());
tests.AddRange(GaunerTests.Alle());
tests.AddRange(TalenteTests.Alle());
tests.AddRange(AktionspunkteTests.Alle());
tests.AddRange(GesundheitTests.Alle());
tests.AddRange(TischlerTests.Alle());
tests.AddRange(MeistergradeTests.Alle());
tests.AddRange(SteinmetzTests.Alle());
tests.AddRange(AlchimistTests.Alle());

// ── Testlauf ────────────────────────────────────────────────────────────────
var fehler = 0;
foreach (var (name, pruefung) in tests)
{
    try
    {
        pruefung();
        Console.WriteLine($"  OK      {name}");
    }
    catch (Exception ex)
    {
        fehler++;
        Console.WriteLine($"  FEHLER  {name}");
        Console.WriteLine($"          → {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine(fehler == 0
    ? $"Alle {tests.Count} Tests bestanden."
    : $"{fehler} von {tests.Count} Tests fehlgeschlagen.");
return fehler == 0 ? 0 : 1;

// Simuliert Echtzeit so, wie Godot sie liefert: in Häppchen von 1/60 Sekunde.
static void SimuliereEchtzeit(Simulation sim, double sekunden)
{
    const double bild = 1.0 / 60.0;
    var bilder = (int)Math.Round(sekunden / bild);
    for (var i = 0; i < bilder; i++)
        sim.Aktualisieren(bild);
}

/// <summary>Kleine Prüf-Hilfe: wirft einen Fehler mit verständlicher Meldung.</summary>
static class Pruefe
{
    public static void Gleich<T>(T erwartet, T tatsaechlich, string? hinweis = null)
    {
        if (!EqualityComparer<T>.Default.Equals(erwartet, tatsaechlich))
            throw new Exception($"{(hinweis is null ? "" : hinweis + ": ")}erwartet {erwartet}, war aber {tatsaechlich}");
    }

    /// <summary>Prüft Kommazahlen mit erlaubter relativer Abweichung (0,05 = ±5 %).</summary>
    public static void Ungefaehr(double erwartet, double tatsaechlich, double toleranz)
    {
        var abweichung = Math.Abs(tatsaechlich - erwartet) / Math.Max(Math.Abs(erwartet), 1e-9);
        if (abweichung > toleranz)
            throw new Exception($"erwartet ≈ {erwartet:N1} (±{toleranz:P0}), war aber {tatsaechlich:N1} (Abweichung {abweichung:P1})");
    }

    /// <summary>Erwartet einen Fehler, dessen Meldung den Text enthält.</summary>
    public static void WirftMitMeldung(Action aktion, string textInMeldung)
    {
        try { aktion(); }
        catch (Exception ex)
        {
            if (!ex.Message.Contains(textInMeldung))
                throw new Exception($"Fehlermeldung enthält nicht „{textInMeldung}“: {ex.Message}");
            return;
        }
        throw new Exception("Es wurde ein Fehler erwartet, aber keiner ausgelöst.");
    }

    /// <summary>Prüft eine Bedingung; die Meldung erklärt, was stattdessen der Fall war.</summary>
    public static void Wahr(bool bedingung, string meldung)
    {
        if (!bedingung)
            throw new Exception(meldung);
    }

    public static void Wirft(Action aktion)
    {
        try { aktion(); }
        catch { return; }
        throw new Exception("Es wurde ein Fehler erwartet, aber keiner ausgelöst.");
    }
}
