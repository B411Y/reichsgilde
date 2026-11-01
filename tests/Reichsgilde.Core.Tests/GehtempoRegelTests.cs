using Reichsgilde;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Nachbesserung nach der Prüfung von phase5/gehtempo (1.11.2026, Befunde M5 und M6):
///   • Eine Regel für alles, was sich im Bild bewegt (<see cref="Gehtempo.MeterJeSpielminute"/>): Meter je echter Sekunde bei der eingestellten
///     Rundendauer – Karren wie Passanten. Bei 15 Minuten je Runde ist das genau das Tempo, mit dem der Kern die Fahrzeit rechnet; bei längeren
///     Runden ist der Karren im Bild früher am Markt und wartet, bei kürzeren später und kehrt sofort um (<see cref="Karrenzug"/>).
///   • Eine Fahrt, die der Kern an der Höchstdauer kappt, dauert im Bild länger: Das Profil wird nie gestaucht, der Karren kommt nach dem Verkauf
///     an und wendet sofort.
/// </summary>
static class GehtempoRegelTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Fahrprofil Eben(double laenge, double meterJeMinute) => new(new List<Karrentempo.Stueck> { new(1, 1) }, laenge, meterJeMinute);

    /// <summary>Lässt den Zug Minute für Minute fahren: Der Kern hat den Karren bis <paramref name="verkauftUm"/> auf dem Weg zum Markt, danach auf dem Rückweg.</summary>
    static Karrenzug Fahren(Fahrprofil hin, Fahrprofil zurueck, int minuten, int verkauftUm, int zurueckUm)
    {
        var zug = new Karrenzug();
        for (var m = 0; m < minuten; m++)
        {
            var kern = m < verkauftUm ? KarrenZustand.ZumMarkt : m < zurueckUm ? KarrenZustand.ZumBetrieb : KarrenZustand.AmBetrieb;
            zug.Weiter(1, kern, hin, zurueck);
        }
        return zug;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Gehtempo: eine Regel fürs Bild – Meter je echter Sekunde bei der eingestellten Rundendauer (Karren und Figuren), bei 15 Minuten bitgleich mit dem Tempo des Kerns", () =>
        {
            foreach (var (art, karren) in Daten.Value.Logistik.Karren)
            {
                Pruefe.Gleich(karren.MeterJeMinute, Gehtempo.MeterJeSpielminute(karren.Tempo, Taktgeber.StandardRundenDauerSekunden), art);
                foreach (var runde in new[] { Taktgeber.MinRundenDauerSekunden, Taktgeber.StandardRundenDauerSekunden, 30 * 60.0, Taktgeber.MaxRundenDauerSekunden })
                {
                    var takt = new Taktgeber { RundenDauerSekunden = runde };
                    // was man sieht: Meter je Spielminute mal Spielminuten je echter Sekunde
                    Pruefe.Ungefaehr(karren.Tempo, Gehtempo.MeterJeSpielminute(karren.Tempo, runde) * takt.TicksProSekunde, 1e-12);
                    Pruefe.Ungefaehr(Gehtempo.Gehen, Gehtempo.MeterJeSpielminute(Gehtempo.Gehen, runde) * takt.TicksProSekunde, 1e-6);
                }
            }
            // Vorher fuhr das Bild mit dem Tempo des Kerns (fest 15 Minuten je Runde): bei 2 Stunden je Runde nur 0,125 m/s, bei 1 Minute 15 m/s
            var lang = new Taktgeber { RundenDauerSekunden = Taktgeber.MaxRundenDauerSekunden };
            Pruefe.Ungefaehr(0.125, Daten.Value.Logistik.Karren["Handkarren"].MeterJeMinute * lang.TicksProSekunde, 1e-9);
        }),

        ("Gehtempo: lange Runden – der Karren ist im Bild früher am Markt und wartet dort, bis der Kern verkauft; kurze Runden – er kommt später und kehrt sofort um", () =>
        {
            var hand = Daten.Value.Logistik.Karren["Handkarren"];
            const double weg = 24; // die Werkstatt des Spielers in Köln
            var kern = (int)Math.Round(weg / hand.MeterJeMinute, MidpointRounding.AwayFromZero); // 30 Minuten, wie der Kern sie rechnet
            Pruefe.Gleich(30, kern);

            // 2 Stunden je Runde: im Bild 24 m in 24 s = 3,8 Spielminuten
            var schnell = Eben(weg, Gehtempo.MeterJeSpielminute(hand.Tempo, Taktgeber.MaxRundenDauerSekunden));
            Pruefe.Ungefaehr(weg / hand.Tempo * Spielzeit.MinutenProTag / Taktgeber.MaxRundenDauerSekunden, schnell.Dauer, 1e-9);
            Pruefe.Gleich(Etappe.Markt, Fahren(schnell, schnell, 5, kern, 2 * kern).Etappe, "nach 5 Minuten am Markt");
            Pruefe.Gleich(Etappe.Markt, Fahren(schnell, schnell, kern, kern, 2 * kern).Etappe, "wartet, bis der Kern verkauft");
            Pruefe.Gleich(Etappe.Zurueck, Fahren(schnell, schnell, kern + 1, kern, 2 * kern).Etappe, "nach dem Verkauf zurück");

            // 1 Minute je Runde: im Bild 24 m in 24 s = 456 Spielminuten, der Kern ist längst zurück
            var langsam = Eben(weg, Gehtempo.MeterJeSpielminute(hand.Tempo, Taktgeber.MinRundenDauerSekunden));
            Pruefe.Ungefaehr(456, langsam.Dauer, 1e-9);
            var zug = Fahren(langsam, langsam, 457, kern, 2 * kern);
            Pruefe.Gleich(Etappe.Zurueck, zug.Etappe, "am Markt angekommen und sofort umgekehrt");
            Pruefe.Wahr(zug.Minute < 1.5, $"seit {zug.Minute:0.0} Minuten auf dem Rückweg");
        }),

        ("Gehtempo: gekappte Fahrt – der Kern rechnet höchstens 360 Minuten, das Bild fährt im festen Tempo weiter (nie gestaucht) und wendet am Markt sofort", () =>
        {
            var daten = Daten.Value;
            var ochse = daten.Logistik.Karren["Ochsenkarren"];
            var hoechst = daten.Logistik.Weglaenge!.HoechstMinuten;
            const double weg = 300; // weiter, als ein Ochse in 360 Minuten kommt (227 m)
            var frei = weg / ochse.MeterJeMinute;
            Pruefe.Wahr(frei > hoechst + 100, $"{frei:0} Minuten ohne Grenze");
            var profil = Eben(weg, Gehtempo.MeterJeSpielminute(ochse.Tempo, Taktgeber.StandardRundenDauerSekunden));
            Pruefe.Ungefaehr(frei, profil.Dauer, 1e-9);
            Pruefe.Ungefaehr(ochse.MeterJeMinute, profil.MeterJeMinute(100), 1e-9); // Tempo der Art, nicht schneller
            // Der Kern verkauft nach 360 Minuten und ist nach 720 zurück; der Zug kommt nach 475 Minuten am Markt an und kehrt sofort um
            var zug = Fahren(profil, profil, (int)Math.Ceiling(frei) + 1, hoechst, 2 * hoechst);
            Pruefe.Gleich(Etappe.Zurueck, zug.Etappe);
            Pruefe.Wahr(zug.Minute < 2, $"seit {zug.Minute:0.0} Minuten auf dem Rückweg");
        }),
    };
}
