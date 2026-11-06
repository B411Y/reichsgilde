using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Einbruch und Banden bei Nacht in der Darstellung (Wachen und Diebe, Paket P4): der Ablauf als reine Rechnung (<see cref="Nachtgang"/>, ohne Godot)
/// und seine Anbindung an die echten Vorfälle des Kerns (jeder zeigbare Vorfall hat eine Tür, einen Zeitpunkt zur vollen Stunde und einen Plan).
/// </summary>
static class NachtgangTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Nachtgang: Abschnitte folgen der Zeit; ohne Einstieg entfällt „Drinnen“, danach ist der Täter weg", () =>
        {
            var plan = new Nachtgang.Plan(10, 4, 18, true);
            Pruefe.Gleich(Nachtgang.Abschnitt.Vorher, Nachtgang.Bei(-0.1, plan, out _));
            Pruefe.Gleich(Nachtgang.Abschnitt.Anschleichen, Nachtgang.Bei(5, plan, out var a));
            Pruefe.Wahr(Math.Abs(a - 0.5) < 1e-9, "Anteil beim Anschleichen");
            Pruefe.Gleich(Nachtgang.Abschnitt.Warten, Nachtgang.Bei(10 + 1, plan, out _));
            Pruefe.Gleich(Nachtgang.Abschnitt.Naehern, Nachtgang.Bei(10 + Nachtgang.WartenDauer + 1, plan, out _));
            Pruefe.Gleich(Nachtgang.Abschnitt.Arbeiten, Nachtgang.Bei(14 + Nachtgang.WartenDauer + 1, plan, out _));
            Pruefe.Gleich(Nachtgang.Abschnitt.Drinnen, Nachtgang.Bei(14 + Nachtgang.WartenDauer + Nachtgang.ArbeitDauer + 1, plan, out _));
            Pruefe.Gleich(Nachtgang.Abschnitt.Flucht, Nachtgang.Bei(14 + Nachtgang.WartenDauer + Nachtgang.ArbeitDauer + Nachtgang.DrinnenDauer + 1, plan, out _));
            Pruefe.Gleich(Nachtgang.Abschnitt.Nachher, Nachtgang.Bei(plan.Gesamt + 0.01, plan, out var ende));
            Pruefe.Gleich(1.0, ende, "Anteil nachher");
            var draussen = plan with { Betritt = false };
            Pruefe.Wahr(draussen.Gesamt < plan.Gesamt, "ohne Einstieg kürzer");
            Pruefe.Gleich(Nachtgang.Abschnitt.Flucht, Nachtgang.Bei(14 + Nachtgang.WartenDauer + Nachtgang.ArbeitDauer + 1, draussen, out _));
            // Alle Abschnitte kommen in dieser Reihenfolge, nie rückwärts, und der Anteil bleibt zwischen 0 und 1
            var letzter = Nachtgang.Abschnitt.Vorher;
            for (var t = -1.0; t < plan.Gesamt + 2; t += 0.25)
            {
                var ab = Nachtgang.Bei(t, plan, out var an);
                Pruefe.Wahr(ab >= letzter, $"Abschnitt springt zurück bei {t}");
                Pruefe.Wahr(an is >= 0 and <= 1, $"Anteil {an} bei {t}");
                letzter = ab;
            }
        }),

        ("Nachtgang: Wer steigt ein, wer trägt einen Sack, was wird gezeigt (nur Kernfälle, ohne Taschendiebe und Überfall)", () =>
        {
            Pruefe.Wahr(Nachtgang.Zeigbar(Vorfallart.Einbruch) && Nachtgang.Zeigbar(Vorfallart.Hausbrand) && Nachtgang.Zeigbar(Vorfallart.Auskundschaften), "Nachtvorfälle");
            Pruefe.Wahr(!Nachtgang.Zeigbar(Vorfallart.Taschendiebstahl) && !Nachtgang.Zeigbar(Vorfallart.Ueberfall), "Tag und Landstraße");
            Pruefe.Wahr(Nachtgang.Betritt(Vorfallart.Einbruch, true) && Nachtgang.Betritt(Vorfallart.Hauseinbruch, true) && Nachtgang.Betritt(Vorfallart.Entfuehrung, true), "steigt ein");
            Pruefe.Wahr(!Nachtgang.Betritt(Vorfallart.Einbruch, false), "misslungen: bleibt an der Tür");
            Pruefe.Wahr(!Nachtgang.Betritt(Vorfallart.Brandanschlag, true) && !Nachtgang.Betritt(Vorfallart.Auskundschaften, true), "Feuer und Spähen: nicht hinein");
            Pruefe.Wahr(Nachtgang.TraegtSack(Vorfallart.Einbruch, true, 30), "Beute: Sack");
            Pruefe.Wahr(!Nachtgang.TraegtSack(Vorfallart.Einbruch, true, 0), "leeres Lager: kein Sack");
            Pruefe.Wahr(!Nachtgang.TraegtSack(Vorfallart.Einbruch, false, 30) && !Nachtgang.TraegtSack(Vorfallart.Entfuehrung, true, 30), "nur Erfolg und nur Einbruch");
            Pruefe.Gleich("Knacken", Nachtgang.Arbeit(Vorfallart.Einbruch));
            Pruefe.Gleich("Knien", Nachtgang.Arbeit(Vorfallart.Hausbrand));
            Pruefe.Gleich("Stehen", Nachtgang.Arbeit(Vorfallart.Auskundschaften));
            Pruefe.Gleich(1, Nachtgang.Anzahl(false, 1260, "Gießerei"), "eigener Dieb allein");
            for (var m = 0; m < 200; m++)
            {
                var n = Nachtgang.Anzahl(true, 1260 + m * 60, "Wohnhaus von Haus Lindenau");
                Pruefe.Wahr(n is 2 or 3, $"Bande mit {n} Mitgliedern");
                Pruefe.Gleich(n, Nachtgang.Anzahl(true, 1260 + m * 60, "Wohnhaus von Haus Lindenau"), "fest");
            }
            Pruefe.Wahr(Enumerable.Range(0, 200).Select(m => Nachtgang.Anzahl(true, 1260 + m * 60, "Gießerei")).Distinct().Count() == 2, "Banden mit zwei und drei Leuten kommen vor");
            Pruefe.Wahr(Nachtgang.Zustand(Nachtgang.Abschnitt.Flucht, Vorfallart.Einbruch, true, true, false).Contains("Sack", StringComparison.Ordinal), "Text mit Sack");
            Pruefe.Wahr(Nachtgang.Zustand(Nachtgang.Abschnitt.Flucht, Vorfallart.Einbruch, true, true, true).Contains("Zeuge", StringComparison.Ordinal), "Zeuge löst Alarm aus");
        }),

        ("Nachtgang: Jeder zeigbare Vorfall des Kerns hat eine Tür und liegt auf der vollen Stunde (ein Lauf über mehrere Nächte mit eigenen Dieben und Banden)", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(
                Partieeinstellungen.Standard with { Zufallsstartwert = 4711, Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
                Daten.Value, new NeuePartie(Rivale: KiCharakter.Wagemutig), karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
            var w = sim.Wirtschaft!;
            var dieb = w.KontoEroeffnen("Familie Elster", startvermoegen: 10_000);
            var versteck = w.BetriebEroeffnen("Unterschlupf Familie Elster", Betriebsart.Unterschlupf, dieb, startkarren: "");
            var opfer = w.KontoEroeffnen("Haus Lindenau", startvermoegen: 10_000);
            var giesserei = w.BetriebEroeffnen("Gießerei Haus Lindenau", Betriebsart.Giesserei, opfer, startkarren: "");
            var vorfaelle = new List<Vorfall>();
            var befehle = new List<string>();
            w.Gauner.Vorfaelle.Neu += v => vorfaelle.Add(v);
            for (var t = 0; t < 8 * Spielzeit.MinutenProTag; t++)
            {
                if (sim.Zeit.UhrzeitInMinuten == 10 * 60)
                {
                    giesserei.Einlagern("Dolch", 20);
                    w.Einstellen(versteck, "Geselle", versteck.Plaetze);
                    befehle.Add(w.Ausfuehren(dieb, new Einbrechen("Haus Lindenau", giesserei.Name)).Meldung);
                    befehle.Add(w.Ausfuehren(dieb, new Auskundschaften("Haus Lindenau")).Meldung);
                }
                sim.Tick();
            }
            var nacht = vorfaelle.Where(v => Nachtgang.Zeigbar(v.Art)).ToList();
            Pruefe.Wahr(nacht.Count >= 8, $"Zu wenige Nachtvorfälle im Test: {nacht.Count}");
            var arten = string.Join(", ", nacht.Select(v => v.Art + (v.IstBande ? "/Bande" : "")));
            var meldungen = string.Join(" | ", befehle.Distinct());
            Pruefe.Wahr(nacht.Any(v => v.Art == Vorfallart.Einbruch && !v.IstBande), $"Kein Einbruch der eigenen Diebe ({nacht.Count} Nachtvorfälle: {arten}; Befehle: {meldungen})");
            foreach (var v in nacht)
            {
                Pruefe.Gleich(0, v.Uhrzeit % 60, $"{v.Art}: Uhrzeit {v.Uhrzeit} liegt nicht auf der vollen Stunde");
                Pruefe.Wahr(v.Uhrzeit is >= 20 * 60 and <= 23 * 60, $"{v.Art}: nicht in der Nacht ({v.Uhrzeit})");
                Pruefe.Wahr(v.Versuch is >= 0, $"{v.Art}: kein Versuch der Nacht");
                Pruefe.Wahr(v.Taeterhaus is not null, $"{v.Art}: Täterhaus fehlt");
                // Die Tür: Ort Betrieb nennt den Betrieb, Ort Wohnhaus das Opfer – sonst fände die Darstellung kein Ziel
                if (v.Ort == Vorfallort.Betrieb) Pruefe.Wahr(v.Betrieb is not null, $"{v.Art}: Betrieb fehlt");
                else Pruefe.Wahr(v.Opfer is not null, $"{v.Art}: Opfer fehlt");
                Pruefe.Wahr(v.IstBande == (v.Bande is not null), "IstBande");
                if (v.IstBande) Pruefe.Wahr(v.TaeterName is null && v.TaeterArbeiter is null, "Banden sind namenlos");
                var anzahl = Nachtgang.Anzahl(v.IstBande, v.Minute, v.Ziel);
                Pruefe.Wahr(v.IstBande ? anzahl is 2 or 3 : anzahl == 1, "Zahl der Täter");
                // Die echte Minute des Kerns stimmt mit der Uhrzeit überein, außer um 23 Uhr (Eintrag zum Tageswechsel): dort gibt es nichts mehr zu zeigen
                if (v.Uhrzeit >= Spielzeit.TagesendeMinute) Pruefe.Gleich(0.0, Nachtgang.Verfuegbar(v.Uhrzeit), "23 Uhr: Nacht vorbei");
                else Pruefe.Gleich(v.Uhrzeit, new Spielzeit(v.Minute).UhrzeitInMinuten, "Minute und Uhrzeit des Vorfalls");
                // Der Plan mit üblichen Wegen (14 m anschleichen, 3,5 m näher, 18 m Flucht bei 1 m je Minute) endet vor der nächsten Stunde
                var plan = new Nachtgang.Plan(Nachtgang.Dauer(Nachtgang.AnschleichMeter, 0.8), Nachtgang.Dauer(Nachtgang.NaeherMeter, 0.8), Nachtgang.Dauer(Nachtgang.FluchtMeter, 1.0), Nachtgang.Betritt(v.Art, v.Erfolg));
                Pruefe.Wahr(plan.Gesamt + (anzahl - 1) * Nachtgang.BandeAbstand < 75, $"{v.Art}: Szene dauert {plan.Gesamt} min");
            }
        }),

        ("Nachtgang: Mit dem echten Gehtempo endet jede Szene bei jeder Rundenlänge vor dem Tagesende (22:59) und vor dem nächsten Versuch", () =>
        {
            foreach (var runde in new[] { 60.0, 120, 300, 600, 900, 1800, 3600 })
                foreach (var uhr in new[] { 20 * 60, 21 * 60, 22 * 60 })
                    foreach (var betritt in new[] { true, false })
                        foreach (var anzahl in new[] { 1, 2, 3 })
                        {
                            var plan = new Nachtgang.Plan(
                                Nachtgang.Dauer(Nachtgang.AnschleichMeter, Gehtempo.MeterJeSpielminute(Gehtempo.Gebeugt, runde)),
                                Nachtgang.Dauer(Nachtgang.NaeherMeter, Gehtempo.MeterJeSpielminute(Gehtempo.Gebeugt, runde)),
                                Nachtgang.Dauer(Nachtgang.FluchtMeter, Gehtempo.MeterJeSpielminute(1.1, runde)), betritt)
                                .Stauchen(Nachtgang.Verfuegbar(uhr) - (anzahl - 1) * Nachtgang.BandeAbstand);
                            var ende = uhr + plan.Gesamt + (anzahl - 1) * Nachtgang.BandeAbstand * plan.Skala;
                            Pruefe.Wahr(ende <= Spielzeit.TagesendeMinute - 1 + 1e-9, $"Runde {runde}s, {uhr / 60} Uhr, {anzahl} Mann: endet erst {ende}");
                            Pruefe.Wahr(plan.Gesamt <= Nachtgang.HoechstDauer + 1e-9, "Höchstdauer");
                            // kein Abschnitt fällt aus: Anteile bleiben gültig
                            for (var t = 0.0; t < plan.Gesamt + 1; t += 0.5) Pruefe.Wahr(Nachtgang.Bei(t, plan, out var an) >= Nachtgang.Abschnitt.Vorher && an is >= 0 and <= 1, "Anteil");
                        }
            Pruefe.Gleich(0.0, Nachtgang.Verfuegbar(23 * 60), "23 Uhr");
            Pruefe.Wahr(Nachtgang.Verfuegbar(22 * 60 + 40) < 20, "kurz vor Tagesende knapp");
            var kurz = new Nachtgang.Plan(10, 4, 18, true);
            Pruefe.Wahr(Math.Abs(kurz.Stauchen(1000).Gesamt - kurz.Gesamt) < 1e-9, "nie gedehnt");
        }),

        ("Nachtgang: Die Zahl der Täter hängt nicht an der Reihenfolge der Aufrufe (Darstellung ohne Zufallsquelle)", () =>
        {
            var a = Enumerable.Range(0, 40).Select(m => Nachtgang.Anzahl(true, 1260 + m, "Tinkturei Gryn")).ToList();
            var b = Enumerable.Range(0, 40).Reverse().Select(m => Nachtgang.Anzahl(true, 1260 + m, "Tinkturei Gryn")).Reverse().ToList();
            Pruefe.Wahr(a.SequenceEqual(b), "Anzahl hängt von der Reihenfolge ab");
        }),
    };
}
