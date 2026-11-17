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
        ("Wappen KI-Zufall: Greybox und Köln, 1 bis höchste KI-Zahl, jedes Spielerwappen (auch das eines Kartenhauses), Startwerte 1 bis 3 – nie ein doppeltes Wappen", () =>
        {
            var geprueft = 0;
            foreach (var kennung in new[] { "greybox", "koeln" })
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                var max = Partiebesetzung.HoechstensKi(karte, Aufstellungsart.Gelost);
                Pruefe.Wahr(max >= 3, $"{kennung}: mindestens 3 KI möglich ({max})");
                var freie = Wappen.Vorlagen; // auch die Wappen der Kartenhäuser: die weichen aus
                for (var ki = 1; ki <= max; ki++)
                    foreach (var spieler in freie)
                        for (var s = 1; s <= 3; s++)
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

        ("Wappen KI-Zufall: Köln, Spieler wählt das Wappen von Haus Kleingedank, 7 KI – startet, Kleingedank würfelt ein anderes Wappen, zweimal gleich", () =>
        {
            var koeln = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
            var klein = koeln.Startbelegung.Haeuser.Single(h => h.Name == "Haus Kleingedank");
            var lilie = klein.Wappen; // seit 17.11.2026 der silberne Turm in Orange (verschiedene Hauptfarben der Kartenhäuser), bis dahin die Lilie
            Pruefe.Gleich(new Wappen(Teilung.Ohne, Tinktur.Orange, Tinktur.Silber, Figur.Turm), lilie, "Gegenprobe: das Wappen von Kleingedank in der Karte");
            var max = Partiebesetzung.HoechstensKi(koeln, Aufstellungsart.Gelost);
            Pruefe.Gleich(7, max, "Köln: 7 KI");
            foreach (var s in new[] { 1, 5, 42 })
            {
                var d = Daten.Value;
                var b = Partiebesetzung.SpielerWuerfeln(d, koeln, s);
                (Simulation, Konto) Neu() => Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = s, Siegbedingung = Siegbedingung.Endlos }, d,
                    new NeuePartie(Familienname: b.Familienname, Volk: b.Volk, Glaube: b.Glaube, Beruf: b.Beruf, Wappen: lilie, Rivalen: max), karte: koeln);
                var (sim, spieler) = Neu();
                var konten = sim.Wirtschaft!.Konten;
                Pruefe.Gleich(lilie, spieler.Wappen, $"Startwert {s}: Spieler führt die Lilie");
                var kleinKonto = konten.Single(k => k.Inhaber == "Haus Kleingedank");
                Pruefe.Wahr(kleinKonto.Wappen != lilie, $"Startwert {s}: Kleingedank hat ein anderes Wappen");
                Pruefe.Gleich(konten.Count, konten.Select(k => k.Wappen).Distinct().Count(), $"Startwert {s}: alle verschieden");
                // Die anderen Kartenhäuser behalten ihr Wappen aus der Karte
                foreach (var h in koeln.Startbelegung.Haeuser.Where(h => h != klein))
                    Pruefe.Gleich(h.Wappen, konten.Single(k => k.Inhaber == h.Name).Wappen, $"{h.Name} behält sein Wappen");
                Pruefe.Gleich(kleinKonto.Wappen, Neu().Item1.Wirtschaft!.Konten.Single(k => k.Inhaber == "Haus Kleingedank").Wappen, "deterministisch");
            }
            // Gegenprobe: mit 3 KI spielt Kleingedank nicht mit – kein Wurf, die anderen unverändert
            var kurz = Partiebesetzung.KiHaeuser(koeln, koeln.Startbelegung, Daten.Value, 3, lilie, null, 1);
            Pruefe.Wahr(kurz.Select((h, i) => h.Wappen == koeln.Startbelegung.Haeuser[i].Wappen).All(x => x), "nicht mitspielende Häuser zählen nicht");
            // Gegenprobe ohne Ausweichen: die rohe Karte hätte zweimal die Lilie
            Pruefe.Wahr(koeln.Startbelegung.Haeuser.Take(max).Select(h => h.Wappen).Append(lilie).GroupBy(w => w).Any(g => g.Count() > 1), "ohne Ausweichen doppelt");
        }),

        ("Wappen KI-Zufall: ein Kartenhaus weicht auch einer Vorgabe aus „Erweitert“ eines anderen Hauses aus; eine eigene Vorgabe bleibt", () =>
        {
            var koeln = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
            var h = koeln.Startbelegung.Haeuser;
            var spieler = Wappen.Vorlagen.First(w => h.All(k => k.Wappen != w));
            var vorgaben = new Hausvorgabe?[] { new(Wappen: h[1].Wappen), null };
            var liste = Partiebesetzung.KartenwappenEindeutig(h.Take(3).ToList(), spieler, vorgaben, 7);
            Pruefe.Gleich(h[0].Wappen, liste[0].Wappen, "Haus 1 behält (seine Vorgabe gilt erst beim Los)");
            Pruefe.Wahr(liste[1].Wappen != h[1].Wappen, "Haus 2 weicht der Vorgabe von Haus 1 aus");
            Pruefe.Gleich(h[2].Wappen, liste[2].Wappen, "Haus 3 unverändert");
            Pruefe.Wahr(new[] { spieler, h[0].Wappen, h[1].Wappen, h[2].Wappen }.All(w => w != liste[1].Wappen), "neues Wappen gleicht keinem anderen");
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
