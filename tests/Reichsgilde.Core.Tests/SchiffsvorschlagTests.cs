using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Der Vorschlag der Hafentafel (PR #199): <see cref="Kalkulation.BesteSchiffsfahrt"/> für ein gewähltes Ziel – die Oberfläche zeigt je ferne Stadt
/// den besten Auftrag und füllt mit „Vorschlag“ die Fracht für das Ziel, das der Spieler gewählt hat.
/// </summary>
static class SchiffsvorschlagTests
{
    static (Stadtwirtschaft W, Konto Spieler) Partie()
    {
        var daten = Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft()));
        var karte = Stadtkarte.AusDatei(Datenpfad.Karte("probestadt"));
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 7, Siegbedingung = Siegbedingung.Endlos },
            daten, new NeuePartie(Startvermoegen: 20_000, Rivalen: karte.Startbelegung.Haeuser.Count), karte: karte);
        return (sim.Wirtschaft!, spieler);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Schiffsvorschlag: Mit Ziel nur für diese Stadt, mit Waren, die sie kauft bzw. verkauft; ohne Ziel der beste aller Städte", () =>
        {
            var (w, spieler) = Partie();
            Pruefe.Wahr(w.Schiffahrt.IstAktiv, "Die Probestadt hat Schiffshandel");
            var typ = w.Schiffahrt.Daten!.Schiffe.Values.First();
            var alle = Kalkulation.BesteSchiffsfahrt(w, spieler, typ, spieler.Vermoegen);
            var je = w.Schiffahrt.Staedte.Select(s => Kalkulation.BesteSchiffsfahrt(w, spieler, typ, spieler.Vermoegen, ziel: s.Name)).ToList();
            Pruefe.Wahr(je.Any(v => v is not null), "Mindestens eine Stadt lohnt eine Fahrt");
            for (var i = 0; i < je.Count; i++)
            {
                if (je[i] is not { } v) continue;
                var stadt = w.Schiffahrt.Staedte[i];
                Pruefe.Gleich(stadt.Name, v.Ziel, "Ziel des Vorschlags");
                Pruefe.Wahr(v.Hinfracht.Keys.All(stadt.Kauft) && v.Rueckfracht.Keys.All(stadt.Verkauft), $"Waren passen zu {stadt.Name}");
            }
            // Ohne Ziel: derselbe Vorschlag wie der beste der Städte (nach Gewinn je Jahr)
            var bester = je.Where(v => v is not null).MaxBy(v => v!.Rechnung.GewinnJeJahr)!;
            Pruefe.Gleich(bester.Ziel, alle!.Ziel, "bester Vorschlag ohne Ziel");
            Pruefe.Ungefaehr(bester.Rechnung.Gewinn, alle.Rechnung.Gewinn, 1e-9);
            Pruefe.Gleich(null, Kalkulation.BesteSchiffsfahrt(w, spieler, typ, spieler.Vermoegen, ziel: "Atlantis"), "unbekanntes Ziel");
        }),
    };
}
