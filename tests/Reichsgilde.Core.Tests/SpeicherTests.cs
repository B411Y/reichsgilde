using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für Speichern und Laden – und die neue Startaufstellung.</summary>
static class SpeicherTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 10, 13, 12, 0, 0);

    static (Simulation Sim, Konto Spieler) NeuePartie(int startwert = 1400, KiCharakter rivale = KiCharakter.Ausgewogen,
        Schwierigkeit schwierigkeit = Schwierigkeit.Normal) =>
        Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = startwert, Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = schwierigkeit },
            Daten.Value, new NeuePartie(Rivale: rivale));

    static void Ticken(Simulation sim, long minuten)
    {
        for (long i = 0; i < minuten; i++) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Zufallsquelle: Vorspulen ergibt exakt dieselben weiteren Würfe", () =>
        {
            var a = new Zufallsquelle(77);
            for (var i = 0; i < 500; i++) { a.NextDouble(); a.Next(7); a.Next(1000); }
            var b = new Zufallsquelle(77, a.Wuerfe);
            for (var i = 0; i < 100; i++)
            {
                Pruefe.Gleich(a.NextDouble(), b.NextDouble());
                Pruefe.Gleich(a.Next(13), b.Next(13));
            }
        }),

        ("Speichern + Laden ergibt denselben Spielstand", () =>
        {
            var (sim, spieler) = NeuePartie();
            Ticken(sim, (long)(3.5 * Spielzeit.MinutenProTag) + 17);
            var json = Speicherung.Speichern(sim, spieler, Zeitpunkt);
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, geladenerSpieler, Zeitpunkt));
            Pruefe.Gleich(spieler.Inhaber, geladenerSpieler!.Inhaber);
        }),

        ("Gleichlauf: Original und geladene Kopie laufen 5 Jahre exakt gleich weiter", () =>
        {
            // Der wichtigste Test: Vergisst das Speichern irgendein Feld (Markt-Fenster, Karren-Wartezeit,
            // Zufallswürfe, KI-Gedächtnis …), laufen die beiden Partien auseinander.
            var (original, spieler) = NeuePartie(startwert: 4711, rivale: KiCharakter.Wagemutig, Schwierigkeit.Schwer); // reiche KI: mehr geschieht
            Ticken(original, (long)(3.5 * Spielzeit.MinutenProTag) + 333);
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(original, spieler, Zeitpunkt));

            Ticken(original, 5 * Spielzeit.MinutenProTag);
            Ticken(kopie, 5 * Spielzeit.MinutenProTag);

            var a = Speicherung.Speichern(original, spieler, Zeitpunkt);
            var b = Speicherung.Speichern(kopie, kopieSpieler, Zeitpunkt);
            if (a != b)
            {
                var zeilenA = a.Split('\n');
                var zeilenB = b.Split('\n');
                var erste = Enumerable.Range(0, Math.Min(zeilenA.Length, zeilenB.Length)).First(i => zeilenA[i] != zeilenB[i]);
                throw new Exception($"Partien laufen auseinander, erste Abweichung in Zeile {erste + 1}:\n  {zeilenA[erste].Trim()}\n  {zeilenB[erste].Trim()}");
            }
            Pruefe.Wahr(original.Wirtschaft!.Konten.Any(k => k.Stand > 0), "In 8½ Jahren sollte jemand aufgestiegen sein (Test zu kurz?)");
        }),

        ("Laden: Partie ist zunächst pausiert, Ende und Endgrund bleiben erhalten", () =>
        {
            var (sim, spieler) = NeuePartie();
            Ticken(sim, 100);
            sim.Beenden("Testende");
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            Pruefe.Gleich(true, geladen.Takt.IstPausiert);
            Pruefe.Gleich(true, geladen.IstBeendet);
            Pruefe.Gleich("Testende", geladen.Endgrund);
        }),

        ("Laden: Spielstand bringt seine eigenen Wirtschaftsdaten mit", () =>
        {
            var (sim, spieler) = NeuePartie();
            var json = Speicherung.Speichern(sim, spieler);
            var (geladen, _) = Speicherung.Laden(json);
            Pruefe.Ungefaehr(Daten.Value.Allgemein.PachtMine, geladen.Wirtschaft!.Daten.Allgemein.PachtMine, 1e-9);
            Pruefe.Gleich(Daten.Value.Liegenschaften.Count, geladen.Wirtschaft.Daten.Liegenschaften.Count);
        }),

        ("Laden: verständliche Fehler bei kaputten oder zu neuen Dateien", () =>
        {
            Pruefe.WirftMitMeldung(() => Speicherung.Laden("{ kaputt"), "kein gültiger Spielstand");
            Pruefe.WirftMitMeldung(() => Speicherung.Laden("{\"Format\": 99}"), "neueren Version");
            Pruefe.WirftMitMeldung(() => Speicherung.Laden("null"), "leer");
        }),

        ("Ladedialog: Kopf liefert Titel und Zeitpunkt", () =>
        {
            var (sim, spieler) = NeuePartie();
            var kopf = Speicherung.Kopf(Speicherung.Speichern(sim, spieler, Zeitpunkt));
            Pruefe.Gleich(Zeitpunkt, kopf.Gespeichert);
            Pruefe.Wahr(kopf.Titel.StartsWith("Haus Hammerschmid · Frühling 1400"), kopf.Titel);
        }),

        ("Partieaufbau: Unter Statut A arbeiten Bergleute statt Orks in der Grube", () =>
        {
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { StartStatut = Fronstatut.FreieStadt, Schwierigkeit = Schwierigkeit.Schwer },
                Daten.Value, new NeuePartie());
            var grube = sim.Wirtschaft!.Betriebe.Single(b => b.Name == "Grube Eisenbart");
            Pruefe.Gleich(3, grube.Belegung.GetValueOrDefault("Bergmann"));
        }),

        ("Partieaufbau: eigener Familienname und Startvermögen, Rivalenname ist vergeben", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value,
                new NeuePartie("  Haus Glockengießer ", 6000));
            Pruefe.Gleich("Haus Glockengießer", spieler.Inhaber);
            Pruefe.Ungefaehr(6000, spieler.Vermoegen, 1e-9);
            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value,
                new NeuePartie(Partieaufbau.Rivalenname)), "Name eines Rivalen");
        }),
    };
}
