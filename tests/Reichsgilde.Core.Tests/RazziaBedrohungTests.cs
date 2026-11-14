using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using M = Reichsgilde.MusikModell;

/// <summary>
/// Gefahrenmeldung der Wache (Nutzer, 14.11.2026): Eine Razzia meldet <see cref="Meldungsart.Bedrohung"/> mit dem durchsuchten Haus und Wichtigkeit Hoch;
/// die Musik spielt „Gefahr“ nur, wenn das durchsuchte Haus das eigene ist. Gegenproben: Razzia bei einem KI-Haus, die Wache selbst, andere Meldungen.
/// </summary>
static class RazziaBedrohungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft())));

    /// <summary>Eine Stadt mit einem Wachhaus (Haus Wacker), dem Spieler und einem KI-Haus; die Wache durchsucht <paramref name="ziel"/> (0 = Spieler, 1 = KI).</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto Wache, Konto Spieler, Konto Ki, List<Meldung> Meldungen) Razzia(int ziel)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: Daten.Value);
        var w = sim.Wirtschaft!;
        var wache = w.KontoEroeffnen("Familie Wacker", startvermoegen: 10_000);
        var b = w.BetriebEroeffnen($"{w.Daten.Art(Betriebsart.Wachhaus).Name} Wacker", Betriebsart.Wachhaus, wache);
        w.Einstellen(b, "Geselle", b.Plaetze);
        var spieler = w.KontoEroeffnen("Familie Spieler", startvermoegen: 5_000);
        var ki = w.KontoEroeffnen("Familie Rivale", startvermoegen: 5_000);
        var meldungen = new List<Meldung>();
        w.Gemeldet += meldungen.Add;
        var e = w.Ausfuehren(wache, new Razzia((ziel == 0 ? spieler : ki).Inhaber));
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        return (sim, w, wache, spieler, ki, meldungen);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Bedrohung: neue Art hinten angehängt – die Zahlen der alten Arten bleiben", () =>
        {
            Pruefe.Gleich(0, (int)Meldungsart.Sonstiges);
            Pruefe.Gleich(19, (int)Meldungsart.Entscheidung);
            Pruefe.Gleich(20, (int)Meldungsart.Bedrohung);
            Pruefe.Gleich(Meldungswichtigkeit.Hoch, Meldung.Vorgabe(Meldungsart.Bedrohung));
        }),

        ("Bedrohung: Razzia beim Spieler meldet Bedrohung (Haus = durchsuchtes Haus, Hoch) und ergibt Musik „Gefahr“, aber keine Ansage", () =>
        {
            var (_, w, wache, spieler, ki, meldungen) = Razzia(0);
            var m = meldungen.Single(x => x.Text.StartsWith("Razzia"));
            Pruefe.Gleich(Meldungsart.Bedrohung, m.Art);
            Pruefe.Gleich(Meldungswichtigkeit.Hoch, m.Wichtigkeit);
            Pruefe.Wahr(w.HausVon(m) == spieler, "betroffen ist das durchsuchte Haus");
            Pruefe.Gleich(M.Anlass.Gefahr, M.AnlassAusMeldung(m, w, spieler));
            Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusMeldung(m, w, wache), "die Wache selbst hört keine Gefahr");
            Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusMeldung(m, w, ki), "ein Unbeteiligter auch nicht");
            Pruefe.Wahr(KlangAnsagenErkennung.Erkennen(m, w, spieler) is null, "der Haushofmeister sagt die Razzia nicht an");
        }),

        ("Bedrohung: Razzia bei einem KI-Haus gibt dem Spieler keine Gefahr", () =>
        {
            var (_, w, _, spieler, ki, meldungen) = Razzia(1);
            var m = meldungen.Single(x => x.Text.StartsWith("Razzia"));
            Pruefe.Gleich(Meldungsart.Bedrohung, m.Art);
            Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusMeldung(m, w, spieler));
            Pruefe.Gleich(M.Anlass.Gefahr, M.AnlassAusMeldung(m, w, ki));
        }),

        ("Bedrohung: andere Meldungen geben denselben Anlass wie bisher (über die Ansage)", () =>
        {
            var (_, w, _, spieler, ki, _) = Razzia(1);
            int Nr(Konto k) => w.Konten.ToList().IndexOf(k);
            foreach (var art in Enum.GetValues<Meldungsart>().Where(a => a != Meldungsart.Bedrohung))
                foreach (var haus in new[] { spieler, ki })
                    foreach (var wichtig in Enum.GetValues<Meldungswichtigkeit>())
                    {
                        var m = new Meldung(art, "beliebiger Text") { Haus = Nr(haus), Wichtigkeit = wichtig };
                        Pruefe.Gleich(M.AnlassAusAnsage(KlangAnsagenErkennung.Erkennen(m, w, spieler)), M.AnlassAusMeldung(m, w, spieler), $"{art} {haus.Inhaber} {wichtig}");
                    }
            var feuer = new Meldung(Meldungsart.Feuer, "Feuer") { Haus = Nr(spieler), Wichtigkeit = Meldungswichtigkeit.Dringend };
            Pruefe.Gleich(M.Anlass.Gefahr, M.AnlassAusMeldung(feuer, w, spieler));
            Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusMeldung(new Meldung(Meldungsart.Sonstiges, "Razzia der Wache") { Haus = Nr(spieler) }, w, spieler));
        }),

        ("Bedrohung: übersteht Speichern und Laden im Meldungsverlauf", () =>
        {
            var (sim, _, _, spieler, _, _) = Razzia(0);
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler, DateTime.UnixEpoch));
            var m = geladen.Wirtschaft!.Meldungsverlauf.Last(x => x.Text.StartsWith("Razzia"));
            Pruefe.Gleich(Meldungsart.Bedrohung, m.Art);
            Pruefe.Gleich(Meldungswichtigkeit.Hoch, m.Wichtigkeit);
            Pruefe.Wahr(geladen.Wirtschaft.HausVon(m)?.Inhaber == spieler.Inhaber, "Haus nach dem Laden");
        }),
    };
}
