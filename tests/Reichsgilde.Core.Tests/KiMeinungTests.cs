using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests: Meinungen in den KI-Häusern (PR #231) – Aktivitäten mit dem Rat, geliebte und verhasste Häuser.</summary>
static class KiMeinungTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("KI-Meinung: Bei der Wahl wirkt ein Haus, über das ihr Oberhaupt gut denkt, weniger gefährlich (Reinvermögen × (1 − Meinung × 1 %))", () =>
        {
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
            var w = sim.Wirtschaft!;
            Konto Haus(string name, double geld)
            {
                var k = w.KontoEroeffnen(name, startvermoegen: geld);
                w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann, alter: 30);
                return k;
            }
            var kiKonto = Haus("Haus Eisen", 20_000);
            w.BetriebEroeffnen("Gießerei Eisen", Betriebsart.Giesserei, kiKonto);
            var klein = Haus("Haus Klein", 20_000);
            var gross = Haus("Haus Gross", 30_000);
            var ki = w.KiAnmelden(kiKonto, Partieaufbau.ProfilFuer(KiCharakter.Ausgewogen));
            var amt = w.Rat.Aemter.OrderByDescending(a => a.Stufe).First();
            var ich = w.Meinungen.Oberhaupt(kiKonto)!;
            // Gleich gestimmt: Die festen Gründe (Züge) sollen nicht entscheiden.
            foreach (var p in new[] { ich, w.Meinungen.Oberhaupt(klein)!, w.Meinungen.Oberhaupt(gross)! }) p.ZuegeLeeren();
            Pruefe.Gleich("Haus Klein", ki.Wahlstimme(amt, new Konto?[] { klein, gross })?.Inhaber);
            w.Meinungen.TatEintragen(ich.Kennung, w.Meinungen.Oberhaupt(gross)!.Kennung, "test", "Test", 70);
            Pruefe.Wahr(ki.MeinungUeber(gross) >= 70, $"Meinung {ki.MeinungUeber(gross)}");
            Pruefe.Gleich("Haus Gross", ki.Wahlstimme(amt, new Konto?[] { klein, gross })?.Inhaber);

            // Verschuldete Häuser: Die Meinung färbt nichts – sonst würde ein geliebtes Haus mit Schulden „gefährlicher“.
            var tief = Haus("Haus Tief", -5_000);
            var flach = Haus("Haus Flach", -1_000);
            foreach (var k in new[] { tief, flach }) w.Meinungen.Oberhaupt(k)!.ZuegeLeeren();
            Pruefe.Wahr(tief.Reinvermoegen < flach.Reinvermoegen && flach.Reinvermoegen < 0, $"{tief.Reinvermoegen} / {flach.Reinvermoegen}");
            w.Meinungen.TatEintragen(ich.Kennung, w.Meinungen.Oberhaupt(tief)!.Kennung, "test", "Test", 80);
            Pruefe.Gleich("Haus Tief", ki.Wahlstimme(amt, new Konto?[] { tief, flach })?.Inhaber);
        }),

        ("KI-Meinung: KI-Häuser unternehmen Aktivitäten mit den Ratsherren (vor Wahlen und Anträgen) – nicht nur Geschenke", () =>
        {
            var arten = Daten.Value.Meinungen!.Aktivitaeten.Where(a => !a.Value.Geschenk).SelectMany(a => new[] { a.Value.Name, a.Value.Unglueck }).ToHashSet();
            var gefunden = new List<string>();
            foreach (var startwert in new[] { 31, 32, 33 })
            {
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert },
                    Daten.Value, new NeuePartie());
                var w = sim.Wirtschaft!;
                for (var i = 0; i < 12; i++)
                {
                    JahrBeenden(sim);
                    foreach (var ki in w.Rivalen)
                        if (w.Meinungen.Oberhaupt(ki.Konto) is { } o)
                            foreach (var r in w.Rat.Ratsherren)
                                gefunden.AddRange(w.Meinungen.Gruende(r, o).Where(g => arten.Contains(g.Text)).Select(g => $"{startwert}/{i}: {r.Name} über {ki.Konto.Inhaber}: {g.Text} {g.Wert:0.0}"));
                }
            }
            Pruefe.Wahr(gefunden.Count > 0, "keine Aktivität einer KI mit einem Ratsherrn");
        }),
    };
}
