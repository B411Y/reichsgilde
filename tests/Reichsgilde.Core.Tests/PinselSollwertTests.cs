using System.Security.Cryptography;
using System.Text;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Sollwert der alten Pinsel (Pinsel-Paket 1, Prüferbefund): Eine feste Folge von Strichen mit den Pinseln von vor dem Paket – Heben, Senken,
/// Glätten, Ebnen ohne Zielhöhe, Rauschen, Rampe, Texturen, Radierer – ergibt genau die Kartendatei, die der Kern vor dem Paket (main 2c5275c8)
/// schrieb. Der Test benutzt nur Schnittstellen, die es damals schon gab; der Sollwert ist dort gerechnet.
/// </summary>
static class PinselSollwertTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static Kartenort O(double x, double z) => new(x, z);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Alte Pinsel: dieselbe Kartendatei wie vor dem Pinsel-Paket 1 (Prüfsumme vom Kern auf main 2c5275c8)", () =>
        {
            var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
            var strich = new[] { O(-120, -40), O(-10, 25.5), O(90, -60), O(160, 10) };
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, 35, 2.5, 0.6), strich);
            e.GelaendePinseln(new Pinsel(Pinselart.Senken, 18, 1.75, 0.3), new[] { O(-200, 100), O(-150, 140) });
            e.GelaendePinseln(new Pinsel(Pinselart.Rauschen, 90, 3, 0.4, 11, 17), new[] { O(0, 0), O(120, 90) });
            e.GelaendePinseln(new Pinsel(Pinselart.Glaetten, 25, 0.7, 0.5), strich);
            e.GelaendePinseln(new Pinsel(Pinselart.Ebnen, 22, 0.8, 0.5), new[] { O(40, -20), O(70, 5) });
            e.Rampe(O(-300, -200), O(-200, -120), 8, 5);
            e.GelaendePinseln(new Pinsel(Pinselart.Heben, 30, 80, 0), new[] { O(300, 200) });
            e.BodenMalen(new Bodenpinsel(Bodentextur.Fels, 20, 0.8, 0.5, true, 1.3), strich);
            e.BodenMalen(new Bodenpinsel(Bodentextur.Waldboden, 12, 0.5, 0.2, true, 0.7), new[] { O(-60, 0), O(60, 0) });
            e.BodenMalen(new Bodenpinsel(Bodentextur.Wiese, 10, 1, 0.5, true, 1), new[] { O(0, 0) });
            e.BodenMalen(Bodenpinsel.Radierer(6, 0.4), new[] { O(20, 10), O(30, -10) });
            e.Rueckgaengig();
            e.Wiederholen();
            var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(e.ZuJson()))).ToLowerInvariant();
            Pruefe.Gleich("f69af5eb5ceee5d762a6f61ccc4ee5e1ed6853fb8b7252d9b80ea5785919a41b", sha, "SHA-256 der Kartendatei");
        }),
    };
}
