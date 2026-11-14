using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// „Verwalter kauft nur das Nötigste“ kommt höchstens einmal am Spieltag – auch über Speichern und Laden. Vorher stand der Tag der letzten
/// Meldung nicht im Spielstand: Nach dem Laden kam sie am selben Tag ein zweites Mal, und der Verlauf der Meldungen (in der Prüfsumme) lief
/// gegen die durchgehende Partie auseinander (Gleichheit 4711, gefunden mit den Versehrungen, Datenversion 89).
/// </summary>
static class VerwalterMeldungLadenTests
{
    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
            null, Wirtschaftsdaten.AusJson(Datenpfad.OhneJahreszeiten()));

    static void TickenBis(Simulation sim, Func<bool> bedingung)
    {
        for (var i = 0; i < Spielzeit.MinutenProTag; i++)
        {
            if (bedingung()) return;
            sim.Tick();
        }
        throw new Exception("Bedingung in einem Jahr nicht erreicht.");
    }

    /// <summary>Eine Gießerei, deren Kasse nur für das Nötigste reicht; bis zur ersten Fahrt mit der Meldung.</summary>
    static (Simulation Sim, Konto Konto) KnappeKasse()
    {
        var sim = NeueSimulation();
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Spieler");
        var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, k);
        w.Einstellen(g, "Geselle", 2);
        k.Zutaten(k.Vermoegen - w.OffeneJahreskosten(k) - 10);
        TickenBis(sim, () => g.Karren[0].Zustand == KarrenZustand.ZumBetrieb);
        return (sim, k);
    }

    static int ZweiteFahrt(Simulation sim)
    {
        var w = sim.Wirtschaft!;
        var meldungen = new List<string>();
        w.Meldung += meldungen.Add;
        var karren = w.Betriebe.Single(b => b.Name == "G").Karren[0];
        TickenBis(sim, () => karren.Zustand == KarrenZustand.AmBetrieb);
        TickenBis(sim, () => karren.Zustand == KarrenZustand.ZumBetrieb);
        Pruefe.Gleich(5, karren.Ladung.GetValueOrDefault("Silber"), "zweite Fahrt: das Nötigste");
        return meldungen.Count(m => m.Contains("nur das Nötigste"));
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Verwalter: „nur das Nötigste“ nach Speichern und Laden am selben Tag nicht noch einmal – wie in der durchgehenden Partie", () =>
        {
            var (durchgehend, _) = KnappeKasse();
            Pruefe.Gleich(0, ZweiteFahrt(durchgehend), "Gegenprobe ohne Laden");

            var (sim, konto) = KnappeKasse();
            var json = Speicherung.Speichern(sim, konto);
            Pruefe.Wahr(json.Contains("\"KasseKnappAm\": 0"), "der Tag steht im Spielstand");
            var (geladen, _) = Speicherung.Laden(json);
            Pruefe.Gleich(0, ZweiteFahrt(geladen), "nach dem Laden");
        }),

        ("Verwalter: Der Tag der Meldung steht nur im Spielstand, solange es der laufende Spieltag ist – alte Stände ohne Feld laden", () =>
        {
            var (sim, konto) = KnappeKasse();
            var alt = System.Text.RegularExpressions.Regex.Replace(Speicherung.Speichern(sim, konto), @",\s*""KasseKnappAm"":\s*0", "");
            Pruefe.Wahr(!alt.Contains("KasseKnappAm"), "Feld entfernt");
            Pruefe.Gleich(1, ZweiteFahrt(Speicherung.Laden(alt).Simulation), "Gegenprobe: ohne das Feld (alter Stand) kommt die Meldung noch einmal");
            var jahr = sim.Zeit.Jahr;
            while (sim.Zeit.Jahr == jahr) sim.Tick();
            Pruefe.Wahr(!Speicherung.Speichern(sim, konto).Contains("KasseKnappAm"), "am nächsten Tag nicht mehr");
        }),
    };
}
