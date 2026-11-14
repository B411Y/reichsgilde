using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Jedes Haus einer Partie führt ein eigenes Wappen – Spieler und KI, auch bei zufällig gewürfelten Gegnern (Fehlerpaket 1, Punkt 10).</summary>
static class WappenEindeutigTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Startet eine Partie wie das Menü (Spieler gewürfelt, KI gewürfelt) und gibt die Wappen aller Häuser zurück.</summary>
    static List<Wappen> HausWappen(Stadtkarte karte, int startwert, int rivalen, IReadOnlyList<Hausvorgabe?>? vorgaben = null)
    {
        var d = Daten.Value;
        var b = Partiebesetzung.SpielerWuerfeln(d, karte, startwert, kiVorgaben: vorgaben);
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos }, d,
            new NeuePartie(Familienname: b.Familienname, Volk: b.Volk, Glaube: b.Glaube, Beruf: b.Beruf, Wappen: b.Wappen, Rivalen: rivalen, KiVorgaben: vorgaben), karte: karte);
        return sim.Wirtschaft!.Konten.Select(k => k.Wappen).ToList();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Wappen: gewürfelte Häuser tragen nie dasselbe Wappen (alle Karten, höchste KI-Zahl, Startwerte 1 bis 200)", () =>
        {
            foreach (var kennung in new[] { "koeln", "greybox", "probestadt", "messstadt" })
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                var max = Partiebesetzung.HoechstensKi(karte, Aufstellungsart.Gelost);
                for (var s = 1; s <= 200; s++)
                {
                    var w = HausWappen(karte, s, max);
                    Pruefe.Gleich(max + 1, w.Count, $"{kennung} {s}: Zahl der Häuser");
                    Pruefe.Gleich(w.Count, w.Distinct().Count(), $"{kennung} Startwert {s}: doppeltes Wappen");
                }
            }
        }),

        ("Wappen: Sperre im Menü – Kartenhaus, anderes Haus und Spieler sperren; freies nicht; ein anderes Wappen gibt das alte frei", () =>
        {
            var start = Stadtkarte.AusDatei(Datenpfad.Karte("koeln")).Startbelegung;
            var h0 = start.Haeuser[0];
            var h1 = start.Haeuser[1];
            var spieler = Wappen.Vorlagen.First(w => start.Haeuser.All(h => h.Wappen != w));
            var keine = new Wappen?[] { null, null, null };
            // Gegenprobe: das Wappen eines Kartenhauses ist für ein anderes Haus gesperrt, das eigene der Zeile nicht
            var fuer1 = Partiebesetzung.VergebeneWappen(start, 3, spieler, "Haus Spieler", keine, 1);
            Pruefe.Wahr(fuer1.Any(x => x.Wappen == h0.Wappen && x.Haus == h0.Name), "Wappen des Kartenhauses 1 gesperrt für Zeile 2");
            Pruefe.Wahr(fuer1.All(x => x.Wappen != h1.Wappen), "das eigene Kartenhaus der Zeile zählt nicht");
            Pruefe.Wahr(fuer1.Any(x => x.Wappen == spieler), "Wappen des Spielers gesperrt");
            var frei = Wappen.Vorlagen.First(w => w != spieler && start.Haeuser.All(h => h.Wappen != w));
            Pruefe.Wahr(fuer1.All(x => x.Wappen != frei), "ein freies Wappen ist nicht gesperrt");
            // Ein anderes Haus wählt ein Wappen: es ist gesperrt, und sein altes Kartenwappen wird frei
            var gewaehlt = new Wappen?[] { frei, null, null };
            var jetzt = Partiebesetzung.VergebeneWappen(start, 3, spieler, "Haus Spieler", gewaehlt, 1);
            Pruefe.Wahr(jetzt.Any(x => x.Wappen == frei), "Wahl eines anderen Hauses sperrt");
            Pruefe.Wahr(jetzt.All(x => x.Wappen != h0.Wappen), "sein altes Wappen ist wieder frei");
            // Für den Spieler (-1) zählen die Kartenhäuser, nicht er selbst
            var fuerSpieler = Partiebesetzung.VergebeneWappen(start, 3, spieler, "Haus Spieler", keine, -1);
            Pruefe.Wahr(fuerSpieler.Any(x => x.Wappen == h0.Wappen) && fuerSpieler.All(x => x.Wappen != spieler), "Spieler: Kartenhäuser gesperrt");
        }),

        ("Wappen: Gegenprobe – ein vorgegebenes Wappen, das ein Haus der Karte schon trägt, wird mit den Hausnamen abgewiesen", () =>
        {
            var koeln = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
            var fest = koeln.Startbelegung.Haeuser.First(h => !h.Gewuerfelt);
            var j = koeln.Startbelegung.Haeuser.ToList().IndexOf(fest);
            var vorgaben = new Hausvorgabe?[2];
            vorgaben[j == 0 ? 1 : 0] = new(Wappen: fest.Wappen); // auf ein anderes Haus als das, dem das Wappen gehört
            var max = Partiebesetzung.HoechstensKi(koeln, Aufstellungsart.Gelost);
            var meldung = "";
            try { HausWappen(koeln, 5, max, vorgaben); }
            catch (ArgumentException e) { meldung = e.Message; }
            Pruefe.Wahr(meldung.Contains("dasselbe Wappen") && meldung.Contains("und"), "Meldung nennt beide Häuser: " + meldung);
        }),
    };
}
