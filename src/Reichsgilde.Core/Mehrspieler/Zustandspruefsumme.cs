using System.Security.Cryptography;
using System.Text.Json;
using Reichsgilde.Core.Speichern;

namespace Reichsgilde.Core.Mehrspieler;

/// <summary>
/// Die Prüfsumme des Zustands: SHA-256 über den Spielstand als JSON – zwei Rechner mit gleicher Prüfsumme rechnen dieselbe Partie.
/// Heute fürs Entwicklerfenster, später der Vergleich im Gleichschritt (ein Rechner, der abweicht, ist auseinandergelaufen).
///
/// Weggelassen wird, was nicht zum Zustand gehört oder auf jedem Rechner anders ist: Speicherzeit und Titel, Tempo (die Pause steht ohnehin
/// nicht im Spielstand), das Konto des Menschen an diesem Rechner und die Warteschlange – sie hält Befehle für spätere Ticks, und die
/// kommen nicht bei allen Rechnern im selben Augenblick an. Was schon gewirkt hat, steht im übrigen Zustand.
///
/// Geschrieben wird kompakt, ohne Einrückung: Eingerücktes JSON schreibt in .NET 8 <c>Environment.NewLine</c>, auf Windows also <c>\r\n</c> –
/// so ergäben Linux und Windows bei gleichem Zustand verschiedene Prüfsummen. Die eine Prüfsumme des Kerns: Entwicklerfenster und
/// Gleichheitstest (Mehrspieler M1, <c>GleichheitTests.cs</c>) benutzen sie beide.
/// </summary>
public static class Zustandspruefsumme
{
    private static readonly JsonSerializerOptions Kompakt = new(Speicherung.Optionen) { WriteIndented = false };

    /// <summary>Die Prüfsumme als 64 Zeichen Hexadezimal (klein) – null während einer angehaltenen Ratssitzung (dann lässt sich nicht speichern).</summary>
    public static string? Berechnen(Simulation simulation)
    {
        if (Speicherung.SpeichernVerboten(simulation) is not null) return null;
        var stand = Speicherung.Erstellen(simulation, spieler: null, zeitpunkt: DateTime.UnixEpoch) with
        {
            Titel = "",
            Tempo = 1,
            Spielerkonto = null,
            Warteschlange = null,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(stand, Kompakt);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>Die ersten 8 Zeichen – kurz genug für eine Zeile im Entwicklerfenster.</summary>
    public static string? Kurz(Simulation simulation) => Berechnen(simulation)?[..8];
}
