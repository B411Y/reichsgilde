using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Fehler „Zwei Häuser führen dasselbe Wappen“ beim Start ohne „Erweitert“ (15.11.2026): Gewürfelte KI-Wappen sind nie gleich – weder
/// untereinander noch mit dem Spieler oder einem Haus der Karte –, bei jeder KI-Zahl und jedem Spielerwappen. Nur ein selbst gewähltes
/// doppeltes Wappen darf abgewiesen werden.
/// </summary>
static class WappenKiZufallTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Startet eine Partie wie das Menü ohne „Erweitert“ (KI ganz gewürfelt) mit dem gegebenen Spielerwappen; gibt alle Wappen zurück.</summary>
    static List<Wappen> HausWappen(Stadtkarte karte, int startwert, int rivalen, Wappen spieler, IReadOnlyList<Hausvorgabe?>? vorgaben = null)
    {
        var d = Daten.Value;
        var b = Partiebesetzung.SpielerWuerfeln(d, karte, startwert, kiVorgaben: vorgaben);
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos }, d,
            new NeuePartie(Familienname: b.Familienname, Volk: b.Volk, Glaube: b.Glaube, Beruf: b.Beruf, Wappen: spieler, Rivalen: rivalen, KiVorgaben: vorgaben), karte: karte);
        return sim.Wirtschaft!.Konten.Select(k => k.Wappen).ToList();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Wappen KI-Zufall: Greybox und Köln, 1 bis höchste KI-Zahl, jedes freie Spielerwappen, Startwerte 1 bis 6 – nie ein doppeltes Wappen", () =>
        {
            var geprueft = 0;
            foreach (var kennung in new[] { "greybox", "koeln" })
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                var max = Partiebesetzung.HoechstensKi(karte, Aufstellungsart.Gelost);
                Pruefe.Wahr(max >= 3, $"{kennung}: mindestens 3 KI möglich ({max})");
                var freie = Wappen.Vorlagen.Where(w => karte.Startbelegung.Haeuser.All(h => h.Wappen != w)).ToList();
                for (var ki = 1; ki <= max; ki++)
                    foreach (var spieler in freie)
                        for (var s = 1; s <= 6; s++)
                        {
                            var w = HausWappen(karte, s * 31 + ki, ki, spieler);
                            Pruefe.Gleich(ki + 1, w.Count, $"{kennung} {ki} KI: Zahl der Häuser");
                            Pruefe.Gleich(spieler, w[0], $"{kennung}: Spielerwappen bleibt");
                            Pruefe.Gleich(w.Count, w.Distinct().Count(), $"{kennung} {ki} KI, Spieler {spieler.Beschreibung()}, Startwert {s}: doppeltes Wappen");
                            geprueft++;
                        }
            }
            Pruefe.Wahr(geprueft > 100, $"genug Partien geprüft ({geprueft})");
        }),

        ("Wappen KI-Zufall: Gegenprobe – ohne Neuwürfeln (belegt leer) kämen doppelte Wappen vor", () =>
        {
            // Dieselben Würfe wie beim Besetzen, nur ohne die Liste der belegten Wappen: bei 8 Häusern und vielen Startwerten trifft es irgendwann zweimal dasselbe.
            var doppelt = 0;
            for (var s = 1; s <= 2000 && doppelt == 0; s++)
            {
                var z = new Zufallsquelle(s);
                var w = Enumerable.Range(0, 8).Select(_ => Wappen.Zufall(z)).ToList();
                if (w.Distinct().Count() < w.Count) doppelt++;
            }
            Pruefe.Wahr(doppelt > 0, "ohne Neuwürfeln findet sich ein doppeltes Wappen");
            // Und mit der Liste der belegten Wappen nie (dieselben Startwerte)
            for (var s = 1; s <= 2000; s++)
            {
                var z = new Zufallsquelle(s);
                var belegt = new List<Wappen>();
                for (var i = 0; i < 8; i++) belegt.Add(Wappen.Zufall(z, belegt));
                Pruefe.Gleich(belegt.Count, belegt.Distinct().Count(), $"mit Neuwürfeln Startwert {s}");
            }
        }),

        ("Wappen KI-Zufall: Gegenprobe – selbst gewählte gleiche Wappen (alter Menüfehler: jede KI-Zeile die erste Vorlage) werden abgewiesen", () =>
        {
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("greybox"));
            var spieler = Wappen.Vorlagen[7]; // Blau, goldene Lilie – wie im Befund des Nutzers
            var vorgaben = new Hausvorgabe?[] { new(Wappen: Wappen.Vorlagen[0]), new(Wappen: Wappen.Vorlagen[0]), new(Wappen: Wappen.Vorlagen[0]) };
            var meldung = "";
            try { HausWappen(karte, 5, 3, spieler, vorgaben); }
            catch (ArgumentException e) { meldung = e.Message; }
            Pruefe.Wahr(meldung.Contains("dasselbe Wappen"), "gleiche gewählte Wappen abgewiesen: " + meldung);
            // Ohne Vorgaben (alles „zufällig“) startet dieselbe Partie
            var w = HausWappen(karte, 5, 3, spieler);
            Pruefe.Gleich(4, w.Count, "Partie mit 3 KI startet");
        }),
    };
}
