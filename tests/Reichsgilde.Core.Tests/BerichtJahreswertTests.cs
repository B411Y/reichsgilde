using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Fehler aus dem Klicktest auf Köln: Das Fach „Bericht“ zeigte „Jahr −126“. Das war keine Jahreszahl, sondern der Gewinn des
/// laufenden Jahres in Gulden (4000 Startvermögen, 3874 in der Kasse). Die Anzeige heißt jetzt „Jahresgewinn −126“; dieser Test
/// hält fest, was der Kern dafür liefert: Die laufende Jahresrechnung gehört zum Spieljahr 1400, ihr Gewinn ist ein Guldenbetrag.
/// </summary>
static class BerichtJahreswertTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Bericht: laufende Jahresrechnung im Startjahr 1400, Gewinn = Gulden seit Jahresbeginn (nicht das Jahr)", () =>
        {
            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Konrad", startvermoegen: 4_000);
            Pruefe.Gleich(Spielzeit.StartJahr, sim.Zeit.Jahr);
            Pruefe.Gleich(sim.Zeit.Jahr, k.Laufend.Jahr);
            Pruefe.Gleich(0.0, k.Laufend.Gewinn);

            k.Zutaten(126); // wie im Klicktest: 126 Gulden ausgegeben, noch nichts verkauft
            Pruefe.Gleich(-126.0, k.Laufend.Gewinn);
            Pruefe.Gleich(3_874.0, k.Vermoegen);
            Pruefe.Gleich(Spielzeit.StartJahr, k.Laufend.Jahr); // die Jahreszahl bleibt 1400
        }),
    };
}
