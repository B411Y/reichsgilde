using Reichsgilde.Core;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Gleichlauf mitten in der Nacht (gefunden mit dem Befehlsprotokoll, Mehrspieler M2): Ob ein Gaunerauftrag in dieser Nacht schon Erfolg
/// hatte, fehlte im Spielstand. Ein misslungener späterer Versuch hinterließ nach dem Laden dann eine Spur, die es ohne Speichern nicht gab.
/// </summary>
static class GaunerNachtSpeicherTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static readonly DateTime Zeitpunkt = new(2026, 10, 29, 12, 0, 0);

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Gleichlauf: Gespeichert um 22 Uhr, laufen Original und Kopie über die Nacht gleich (Erfolg des Gaunerauftrags im Spielstand)", () =>
        {
            // Startwert 9: Ein Dieb bricht in dieser Nacht ein; vor PR M2 legte die Kopie um 23 Uhr eine Spur mehr an.
            var (original, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Zufallsstartwert = 9, Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie());
            for (var i = 0; i < Spielzeit.MinutenProTag - 50; i++) original.Tick();
            var json = Speicherung.Speichern(original, spieler, Zeitpunkt);
            Pruefe.Wahr(json.Contains("\"Erfolgreich\": true"), "Der Test braucht einen Auftrag, der in dieser Nacht schon Erfolg hatte (Daten geändert?).");
            var (kopie, kopieSpieler) = Speicherung.Laden(json);
            for (var i = 0; i < 100; i++) { original.Tick(); kopie.Tick(); }
            Pruefe.Wahr(Speicherung.Speichern(original, spieler, Zeitpunkt) == Speicherung.Speichern(kopie, kopieSpieler, Zeitpunkt),
                "Original und Kopie laufen in der Nacht auseinander.");
        }),
    };
}
