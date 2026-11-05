using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Taschendiebe und Festnahme in der Darstellung (Wachen und Diebe, Paket P3): der Ablauf als reine Rechnung (<see cref="Taschendiebgang"/>, ohne Godot)
/// und der Entwicklerbefehl für die Testhilfe <c>--diebtest</c>.
/// </summary>
static class TaschendiebgangTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Taschendiebgang: Abschnitte eines Griffs folgen der Zeit, eigene Diebe kommen früher als fremde", () =>
        {
            const double griff = 600;
            foreach (var eigen in new[] { true, false })
            {
                Pruefe.Gleich(Taschendiebgang.Abschnitt.Vorher, Taschendiebgang.AbschnittBei(griff - Taschendiebgang.Vorlauf(eigen) - 0.1, griff, eigen, out _), "vorher");
                Pruefe.Gleich(Taschendiebgang.Abschnitt.Anschleichen, Taschendiebgang.AbschnittBei(griff - 1, griff, eigen, out var a), "anschleichen");
                Pruefe.Wahr(a is > 0.8 and < 1, "Anteil kurz vor dem Griff");
                Pruefe.Gleich(Taschendiebgang.Abschnitt.Griff, Taschendiebgang.AbschnittBei(griff + 0.5, griff, eigen, out _), "griff");
                Pruefe.Gleich(Taschendiebgang.Abschnitt.Abgang, Taschendiebgang.AbschnittBei(griff + Taschendiebgang.GriffDauer + 1, griff, eigen, out _), "abgang");
                Pruefe.Gleich(Taschendiebgang.Abschnitt.Nachher, Taschendiebgang.AbschnittBei(griff + Taschendiebgang.GriffDauer + Taschendiebgang.Abgang(eigen) + 0.1, griff, eigen, out _), "nachher");
            }
            Pruefe.Wahr(Taschendiebgang.Vorlauf(true) > Taschendiebgang.Vorlauf(false), "Eigene früher");
            Pruefe.Gleich(0.0, Taschendiebgang.Neigung(0));
            Pruefe.Gleich(1.0, Taschendiebgang.Neigung(0.5));
            Pruefe.Gleich(Taschendiebgang.Streu(3, 77, 1), Taschendiebgang.Streu(3, 77, 1), "Hash fest");
            Pruefe.Wahr(Taschendiebgang.Streu(3, 77, 1) != Taschendiebgang.Streu(4, 77, 1), "Hash streut");
        }),

        ("Taschendiebgang: Festnahme läuft, packt zu, führt ab und ist dann fertig", () =>
        {
            Pruefe.Gleich(Taschendiebgang.Fest.Lauf, Taschendiebgang.FestnahmeBei(0, 5, 40, out _));
            Pruefe.Gleich(Taschendiebgang.Fest.Zugriff, Taschendiebgang.FestnahmeBei(5.5, 5, 40, out _));
            Pruefe.Gleich(Taschendiebgang.Fest.Abfuehren, Taschendiebgang.FestnahmeBei(5 + Taschendiebgang.ZugriffDauer + 20, 5, 40, out var a));
            Pruefe.Wahr(a is > 0.4 and < 0.6, "halber Weg");
            Pruefe.Gleich(Taschendiebgang.Fest.Fertig, Taschendiebgang.FestnahmeBei(1000, 5, 40, out _));
        }),

        ("Entwicklerbefehl Diebeshaus: Versteck mit Dieben, die laut Kern am Markt greifen; ohne Sperre abgelehnt", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 1400, Siegbedingung = Siegbedingung.Endlos }, Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            for (var i = 0; i < 300; i++) sim.Tick(); // 09:00
            Pruefe.Wahr(!sim.Bote.SofortAusfuehren(spieler, new EntwicklerDiebeshaus()).Erfolg, "Ohne Entwickler-Sperre erlaubt");
            sim.EntwicklerErlaubt = true;
            var e = sim.Bote.SofortAusfuehren(spieler, new EntwicklerDiebeshaus());
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            var versteck = w.Betriebe.First(b => b.Konto == spieler && w.Diebe.IstDiebesbetrieb(b));
            Pruefe.Gleich(versteck.Plaetze, versteck.Belegt, "voll besetzt");
            Pruefe.Wahr(sim.Bote.SofortAusfuehren(spieler, new EntwicklerDiebeshaus()).Erfolg, "zweimal erlaubt");
            Pruefe.Gleich(1, w.Betriebe.Count(b => b.Konto == spieler && w.Diebe.IstDiebesbetrieb(b)), "kein zweites Versteck");
            Pruefe.Wahr(w.Diebe.AnwesenheitJetzt(versteck).Any(s => s.Ort == Diebesort.Markt), "Niemand am Markt");
            Pruefe.Wahr(w.Diebe.GriffeDieserStunde(versteck).Count >= 2, "Keine Griffe");
        }),
    };
}
