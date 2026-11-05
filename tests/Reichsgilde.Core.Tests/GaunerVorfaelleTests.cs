using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Mehrspieler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Liste der Gauner-Vorfälle (Wachen und Diebe, Paket P2): Der Kern schreibt mit, was er ohnehin würfelt – ohne eigenen Wurf.
/// Kernprüfung: Dieselbe Partie mit und ohne Liste ist nach jedem Jahr bitgleich (Prüfsumme des Zustands).
/// </summary>
static class GaunerVorfaelleTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>
    /// Wie im Gleichheitstest und im Nachtzustand: Startwert 4711, KI wagemutig und schwer – viele Banden und Einbrüche. Dazu ein Haus mit
    /// eigenen Dieben (Unterschlupf voller Gesellen: Taschendiebe am Markt) und ein Opfer mit vollem Lager; die KI lernt den Dieb selten.
    /// </summary>
    static Simulation Partie()
    {
        var (sim, _) = Partieaufbau.Erstellen(
            Partieeinstellungen.Standard with { Zufallsstartwert = 4711, Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
            Daten.Value, new NeuePartie(Rivale: KiCharakter.Wagemutig), karte: Stadtkarte.AusDatei(Datenpfad.Karte("greybox")));
        var w = sim.Wirtschaft!;
        var dieb = w.KontoEroeffnen(Diebeshaus, startvermoegen: 10_000);
        var versteck = w.BetriebEroeffnen($"Unterschlupf {Diebeshaus}", Betriebsart.Unterschlupf, dieb, startkarren: "");
        w.Einstellen(versteck, "Geselle", versteck.Plaetze);
        var opfer = w.KontoEroeffnen(Opferhaus, startvermoegen: 10_000);
        w.BetriebEroeffnen($"Gießerei {Opferhaus}", Betriebsart.Giesserei, opfer, startkarren: "");
        return sim;
    }

    const string Diebeshaus = "Familie Elster", Opferhaus = "Haus Eisenbart";

    /// <summary>Eine Spielminute; um 10 Uhr füllt das Diebeshaus seine Leute auf und bestellt Einbruch und Auskundschaften für die Nacht.</summary>
    static void Schritt(Simulation sim)
    {
        var w = sim.Wirtschaft!;
        if (sim.Zeit.UhrzeitInMinuten == 10 * 60 && w.Konten.FirstOrDefault(k => k.Inhaber == Diebeshaus) is { IstAktiv: true } dieb)
        {
            var versteck = w.Betriebe.First(b => b.Konto == dieb);
            var giesserei = w.Betriebe.First(b => b.Name == $"Gießerei {Opferhaus}");
            if (giesserei.ImLager("Dolch") < 20) giesserei.Einlagern("Dolch", 20);
            w.Einstellen(versteck, "Geselle", versteck.Plaetze);
            w.Ausfuehren(dieb, new Einbrechen(Opferhaus, giesserei.Name));
            w.Ausfuehren(dieb, new Auskundschaften(Opferhaus));
        }
        sim.Tick();
    }

    const int Jahre = 5;

    /// <summary>Ein Lauf über <see cref="Jahre"/> Jahre: Prüfsumme nach jedem Jahr und alle Vorfälle (über <see cref="Vorfallbuch.Neu"/>).</summary>
    static (List<string> Summen, List<Vorfall> Vorfaelle, List<string> Fehler) Lauf(bool mitschreiben)
    {
        var sim = Partie();
        var w = sim.Wirtschaft!;
        w.Gauner.Vorfaelle.Mitschreiben = mitschreiben;
        var summen = new List<string>();
        var vorfaelle = new List<Vorfall>();
        var fehler = new List<string>();
        var meldungen = new List<(long Minute, string Text)>();
        w.Meldung += m => meldungen.Add((sim.Zeit.VergangeneMinuten, m));
        w.Gauner.Vorfaelle.Neu += v =>
        {
            vorfaelle.Add(v);
            // Der Täter ist derselbe Arbeiter, den die Anwesenheit unterwegs zeigt (bis 22 Uhr; um 23 Uhr steht die Uhr schon auf dem neuen Tag).
            if (v.TaeterArbeiter is int nr && v.Art != Vorfallart.Taschendiebstahl && v.Uhrzeit < Spielzeit.TagesendeMinute)
            {
                var versteck = w.Betriebe.First(b => b.Name == v.Taeterbetrieb);
                if (!w.Diebe.AnwesenheitJetzt(versteck).Any(s => s.Arbeiter.Nummer == nr && s.Ort == Diebesort.Auftrag))
                    fehler.Add($"{v.Minute}: {v.Art} von Arbeiter {nr} aus {v.Taeterbetrieb}, die Anwesenheit zeigt ihn nicht auf dem Auftrag");
            }
            // Der gefasste Taschendieb ist danach nicht mehr im Betrieb.
            if (v.Art == Vorfallart.Taschendiebstahl
                && (v.TaeterArbeiter is not int t || w.Betriebe.First(b => b.Name == v.Taeterbetrieb).Arbeiter.Any(a => a.Nummer == t)))
                fehler.Add($"{v.Minute}: gefasster Taschendieb {v.TaeterArbeiter} fehlt oder ist noch im Betrieb");
            // Nur der laufende und der letzte Tag
            if (w.Gauner.Vorfaelle.Alle.Any(x => x.Jahr < v.Jahr - 1))
                fehler.Add($"{v.Minute}: Die Liste hält Vorfälle älter als der letzte Tag");
        };
        for (var t = 0; t < Jahre * Spielzeit.MinutenProTag; t++)
        {
            var jahr = sim.Zeit.Jahr;
            Schritt(sim);
            if (mitschreiben && sim.Zeit.MinuteImTag % 60 == 0) // lesen wie die Darstellung: darf nichts ändern
                foreach (var b in w.Betriebe.Where(w.Diebe.IstDiebesbetrieb))
                {
                    _ = w.Diebe.GriffeDieserStunde(b);
                    _ = w.Gauner.Vorfaelle.AmTag(sim.Zeit.Jahr);
                }
            if (sim.Zeit.Jahr != jahr) summen.Add(Zustandspruefsumme.Berechnen(sim)!);
        }
        if (mitschreiben)
        {
            // Jeder gefasste Taschendieb steht genau einmal in der Liste und in den Meldungen.
            var gefasst = meldungen.Count(m => m.Text.StartsWith("Am Markt wird ein Taschendieb erwischt", StringComparison.Ordinal));
            var liste = vorfaelle.Count(v => v.Art == Vorfallart.Taschendiebstahl);
            if (gefasst != liste) fehler.Add($"{gefasst} gefasste Taschendiebe in den Meldungen, {liste} in der Liste");
            var festnahmen = meldungen.Count(m => m.Text.StartsWith("Am Markt wird ein Taschendieb erwischt", StringComparison.Ordinal) && m.Text.Contains("Die Wache von"));
            if (festnahmen != vorfaelle.Count(v => v.IstFestnahme)) fehler.Add($"{festnahmen} Festnahmen in den Meldungen, {vorfaelle.Count(v => v.IstFestnahme)} in der Liste");
            // Jeder gewöhnliche Überfall der Landstraße
            var ueberfaelle = meldungen.Count(m => m.Text.StartsWith("Überfall ", StringComparison.Ordinal));
            var ueberfaelleListe = vorfaelle.Count(v => v.Art == Vorfallart.Ueberfall && v.Bande != "Bande");
            if (ueberfaelle != ueberfaelleListe) fehler.Add($"{ueberfaelle} Überfälle der Landstraße in den Meldungen, {ueberfaelleListe} in der Liste");
        }
        return (summen, vorfaelle, fehler);
    }

    static readonly Lazy<(List<string> Summen, List<Vorfall> Vorfaelle, List<string> Fehler)> Mit = new(() => Lauf(true));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Gauner-Vorfälle: Partie mit und ohne Liste nach jedem von 5 Jahren bitgleich (Prüfsumme), auch mit Abfrage der Griffe", () =>
        {
            var ohne = Lauf(false);
            var mit = Mit.Value;
            Pruefe.Gleich(Jahre, mit.Summen.Count, "Jahre");
            for (var i = 0; i < Jahre; i++) Pruefe.Gleich(ohne.Summen[i], mit.Summen[i], $"Prüfsumme nach Jahr {i + 1}");
            Pruefe.Gleich(0, ohne.Vorfaelle.Count, "Ohne Mitschreiben keine Vorfälle");
        }),

        ("Gauner-Vorfälle: Banden, eigene Diebe, Taschendiebe und Überfälle stehen in der Liste, passend zu Meldungen und Anwesenheit", () =>
        {
            var (_, vorfaelle, fehler) = Mit.Value;
            if (fehler.Count > 0) throw new Exception(string.Join("\n", fehler.Take(10)));
            Pruefe.Wahr(vorfaelle.Any(v => v.IstBande && v.Ort != Vorfallort.Landstrasse), "Keine Tat einer Bande aus dem Hinterzimmer");
            Pruefe.Wahr(vorfaelle.Any(v => !v.IstBande && v.TaeterArbeiter is not null && v.Art != Vorfallart.Taschendiebstahl), "Keine Tat eigener Diebe mit Arbeiter");
            Pruefe.Wahr(vorfaelle.Any(v => v.Art == Vorfallart.Taschendiebstahl), "Kein gefasster Taschendieb");
            Pruefe.Wahr(vorfaelle.Any(v => v.Erfolg) && vorfaelle.Any(v => !v.Erfolg && v.Art != Vorfallart.Taschendiebstahl), "Erfolg und Misserfolg kommen vor");
            foreach (var v in vorfaelle)
            {
                Pruefe.Wahr(v.Ziel.Length > 0, $"{v.Minute} {v.Art}: ohne Ziel");
                Pruefe.Wahr(v.Erfolg || v.Wert == 0 || v.Art == Vorfallart.Taschendiebstahl, $"{v.Minute} {v.Art}: Wert ohne Erfolg"); // Taschendieb: Beute der Stunde
                if (v.IstBande) Pruefe.Wahr(v.TaeterArbeiter is null, $"{v.Minute}: Bande mit Arbeiter");
                // Versuch um 23 Uhr: gehört zum alten Tag, obwohl die Uhr schon auf dem neuen steht
                if (v.Versuch is int k)
                {
                    Pruefe.Gleich(20 * 60 + 60 * k, v.Uhrzeit, $"{v.Minute} {v.Art}: Uhrzeit des Versuchs {k}");
                    Pruefe.Gleich(Spielzeit.StartJahr + (int)((v.Minute - 1) / Spielzeit.MinutenProTag), v.Jahr, $"{v.Minute} {v.Art}: Tag");
                }
            }
            Pruefe.Wahr(vorfaelle.Any(v => v.Versuch == 3), "Kein Versuch um 23 Uhr – Datenstand prüfen (gauner.nachtVersuche)");
        }),

        ("Gauner-Vorfälle: Die Liste hält nur den laufenden und den letzten Tag, Abfragen nach Tag und Zeitfenster, nach dem Laden leer", () =>
        {
            var sim = Partie();
            var w = sim.Wirtschaft!;
            for (var t = 0; t < 3 * Spielzeit.MinutenProTag + 21 * 60 - Spielzeit.TagesbeginnMinute; t++) Schritt(sim); // Jahr 3, 21 Uhr
            var buch = w.Gauner.Vorfaelle;
            Pruefe.Wahr(buch.Alle.Count > 0, "Keine Vorfälle in den letzten zwei Tagen");
            Pruefe.Wahr(buch.Alle.All(v => v.Jahr >= sim.Zeit.Jahr - 1), "Älter als der letzte Tag");
            Pruefe.Gleich(buch.Alle.Count, buch.AmTag(sim.Zeit.Jahr).Count + buch.AmTag(sim.Zeit.Jahr - 1).Count, "AmTag teilt die Liste");
            Pruefe.Gleich(buch.Alle.Count, buch.Zwischen(0, sim.Zeit.VergangeneMinuten + 1).Count, "Zwischen über alles");
            Pruefe.Wahr(buch.Alle.Zip(buch.Alle.Skip(1)).All(p => p.First.Minute <= p.Second.Minute), "Nach der Zeit geordnet");

            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, w.Konten[0], new DateTime(2026, 11, 5)));
            Pruefe.Gleich(0, geladen.Wirtschaft!.Gauner.Vorfaelle.Alle.Count, "Nicht im Spielstand: nach dem Laden leer");
        }),

        ("Taschendiebe: Griffe fürs Bild nach fester Formel – 2 bis 4 je Dieb am Markt, in der laufenden Stunde, ohne Zufall", () =>
        {
            for (var nr = 1; nr < 30; nr++)
                for (var h = 8; h < 20; h++)
                    for (var tag = 0; tag < 8; tag++)
                        Pruefe.Wahr(Diebeswesen.Taschengriffe(nr, h, tag) is >= 2 and <= 4, "Griffe 2 bis 4");
            var sim = Partie();
            var w = sim.Wirtschaft!;
            var geprueft = 0;
            for (var t = 0; t < 3 * Spielzeit.MinutenProTag; t++)
            {
                Schritt(sim);
                if (sim.Zeit.MinuteImTag % 37 != 5) continue; // Stichproben zu verschiedenen Minuten
                foreach (var b in w.Betriebe.Where(w.Diebe.IstDiebesbetrieb))
                {
                    var griffe = w.Diebe.GriffeDieserStunde(b);
                    Pruefe.Gleich(griffe.Count, w.Diebe.GriffeDieserStunde(b).Count, "Zweimal gefragt, dasselbe");
                    var h = sim.Zeit.UhrzeitInMinuten / 60;
                    var amMarkt = w.Diebe.AnwesenheitJetzt(b).Where(s => s.Ort == Diebesort.Markt).Select(s => s.Arbeiter).ToList();
                    Pruefe.Wahr(griffe.Select(g => g.Dieb).Distinct().All(amMarkt.Contains), "Nur wer am Markt ist, greift");
                    foreach (var d in amMarkt)
                    {
                        var seine = griffe.Where(g => g.Dieb == d).Select(g => g.Uhrzeit).ToList();
                        Pruefe.Gleich(Diebeswesen.Taschengriffe(d.Nummer, h, sim.Zeit.Jahr - Spielzeit.StartJahr), seine.Count, "Anzahl nach Formel");
                        Pruefe.Wahr(seine.All(m => m / 60 == h) && seine.Distinct().Count() == seine.Count, "In der laufenden Stunde, verschiedene Minuten");
                    }
                    Pruefe.Wahr(griffe.All(g => g.Wert > 0), "Richtwert positiv");
                    geprueft += griffe.Count;
                }
            }
            Pruefe.Wahr(geprueft > 0, "Kein Taschendieb am Markt gesehen – Test prüft nichts");
        }),
    };
}
