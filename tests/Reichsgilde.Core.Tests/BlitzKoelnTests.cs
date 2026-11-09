using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Blitzhöhen der Kölner Sonderbauten aus Paket 3 (Kölner Rathaus, Stapelhaus, Rheinkran): ohne eigene Zeile in <c>blitz.hoehen</c> gälte
/// die Standardhöhe 10 m. Geprüft in den Daten und an den Blitzzielen einer echten Köln-Partie.
/// </summary>
static class BlitzKoelnTests
{
    static readonly (string Katalog, string Kennung, double Hoehe)[] Erwartet =
    {
        ("koelner_rathaus", "Rathaus", 16),
        ("stapelhaus", "Stapelhaus", 14),
        ("rheinkran", "Rheinkran", 6),
    };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Blitz Köln: Daten – Kölner Rathaus 16 m, Stapelhaus 14 m, Rheinkran 6 m", () =>
        {
            var d = Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft())).Blitz!;
            foreach (var (katalog, _, hoehe) in Erwartet)
            {
                Pruefe.Wahr(d.Hoehen.ContainsKey(katalog), $"blitz.hoehen kennt {katalog}");
                Pruefe.Gleich(hoehe, d.Hoehen[katalog], katalog);
            }
        }),

        ("Blitz Köln: Ziele – Rathaus, Stapelhaus und Rheinkran der Kölner Karte mit ihrer Höhe statt der Standardhöhe", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft()));
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos },
                daten, new NeuePartie(), karte: Stadtkarte.AusDatei(Datenpfad.Karte("koeln")));
            var w = sim.Wirtschaft!;
            var ziele = w.Blitz.Ziele();
            foreach (var (katalog, kennung, hoehe) in Erwartet)
            {
                Pruefe.Gleich(katalog, w.Karte!.GebaeudeMit(kennung)?.Katalog, $"{kennung} steht als {katalog} auf der Karte");
                var ziel = ziele.FirstOrDefault(z => z.Kennung == kennung);
                Pruefe.Wahr(ziel is not null, $"{kennung} ist ein Blitzziel");
                Pruefe.Gleich(hoehe, ziel!.Hoehe, kennung);
            }
        }),
    };
}
