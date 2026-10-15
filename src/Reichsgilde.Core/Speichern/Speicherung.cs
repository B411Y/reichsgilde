using System.Text.Json;
using System.Text.Json.Serialization;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Speichern;

/// <summary>Die Speicherdatei ist beschädigt, zu neu oder passt nicht zu den Daten.</summary>
public sealed class SpielstandFehler : Exception
{
    public SpielstandFehler(string meldung, Exception? ursache = null) : base(meldung, ursache) { }
}

/// <summary>
/// Speichert eine laufende Partie als JSON-Text und lädt sie wieder.
///
/// Der Grundsatz: Nach dem Laden läuft die Partie EXAKT so weiter, als wäre nie gespeichert worden –
/// gleiche Preise, gleiche Überfälle, gleiche Entscheidungen der KI. Ein Test prüft das, indem er
/// eine Partie speichert, lädt und Original und Kopie Jahre weiterlaufen lässt.
///
/// Wo die Datei liegt, entscheidet die Oberfläche (in Godot: user://spielstaende/).
/// </summary>
public static class Speicherung
{
    private static readonly JsonSerializerOptions Optionen = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // Umlaute lesbar statt \u00FC
        Converters = { new JsonStringEnumConverter() },
        // Kommazahlen exakt speichern – sonst liefe die geladene Partie minimal anders weiter.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <summary>Erstellt den Spielstand als JSON-Text.</summary>
    /// <param name="spieler">Das Konto des menschlichen Spielers (damit die Oberfläche es nach dem Laden wiederfindet).</param>
    public static string Speichern(Simulation simulation, Konto? spieler = null, DateTime? zeitpunkt = null)
    {
        var w = simulation.Wirtschaft;
        var stand = new Speicherstand
        {
            Gespeichert = zeitpunkt ?? DateTime.Now,
            Titel = $"{spieler?.Inhaber ?? "Partie"} · {simulation.Zeit.DatumText} · {simulation.Zeit.UhrzeitText}",
            Einstellungen = simulation.Einstellungen,
            VergangeneMinuten = simulation.Zeit.VergangeneMinuten,
            Tempo = simulation.Takt.Tempo,
            IstBeendet = simulation.IstBeendet,
            Endgrund = simulation.Endgrund,
            Spielerkonto = spieler is null || w is null ? null : IndexOder(w.Konten, spieler),
            Daten = w?.Daten,
            Wirtschaft = w?.Sichern(),
        };
        return JsonSerializer.Serialize(stand, Optionen);
    }

    /// <summary>Liest nur die Kopfdaten (Titel, Zeitpunkt) – für die Liste im Ladedialog.</summary>
    public static Speicherstand Kopf(string json) => Lesen(json) with { Wirtschaft = null, Daten = null };

    /// <summary>Stellt eine Partie aus einem Spielstand wieder her.</summary>
    /// <returns>Die Simulation und das Konto des menschlichen Spielers (falls gespeichert).</returns>
    /// <exception cref="SpielstandFehler">Wenn die Datei nicht passt – mit verständlicher Begründung.</exception>
    public static (Simulation Simulation, Konto? Spieler) Laden(string json)
    {
        var stand = Lesen(json);

        if (stand.Daten is { } daten && daten.Pruefen() is { Count: > 0 } fehler)
            throw new SpielstandFehler("Die Wirtschaftsdaten im Spielstand sind fehlerhaft:\n  • " + string.Join("\n  • ", fehler));

        try
        {
            var simulation = new Simulation(stand.Einstellungen, new Spielzeit(stand.VergangeneMinuten), stand.Daten);
            simulation.Takt.Tempo = stand.Tempo > 0 ? stand.Tempo : 1;
            simulation.Takt.IstPausiert = true; // nach dem Laden erst einmal anhalten – der Spieler soll sich orientieren
            simulation.EndeWiederherstellen(stand.IstBeendet, stand.Endgrund);

            Konto? spieler = null;
            if (simulation.Wirtschaft is { } w && stand.Wirtschaft is { } ws)
            {
                w.Wiederherstellen(ws);
                if (stand.Spielerkonto is int nummer && nummer >= 0 && nummer < w.Konten.Count)
                    spieler = w.Konten[nummer];
            }
            return (simulation, spieler);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or InvalidDataException or KeyNotFoundException)
        {
            throw new SpielstandFehler($"Der Spielstand passt nicht zu dieser Programmversion: {ex.Message}", ex);
        }
    }

    private static Speicherstand Lesen(string json)
    {
        Speicherstand? stand;
        try
        {
            stand = JsonSerializer.Deserialize<Speicherstand>(json, Optionen);
        }
        catch (JsonException ex)
        {
            throw new SpielstandFehler($"Die Datei ist kein gültiger Spielstand (Zeile {ex.LineNumber + 1}).", ex);
        }
        if (stand is null)
            throw new SpielstandFehler("Die Datei ist leer.");
        if (stand.Format > Speicherstand.AktuellesFormat)
            throw new SpielstandFehler($"Der Spielstand stammt aus einer neueren Version (Format {stand.Format}, dieses Programm kennt bis {Speicherstand.AktuellesFormat}).");
        if (stand.Format < 2)
            throw new SpielstandFehler("Der Spielstand stammt aus einer älteren Version mit 24-Stunden-Tag – seit dem Spieltag von 05 bis 22 Uhr lässt er sich nicht mehr laden. Bitte eine neue Partie beginnen.");
        return stand;
    }

    private static int? IndexOder(IReadOnlyList<Konto> konten, Konto konto)
    {
        for (var i = 0; i < konten.Count; i++)
            if (konten[i] == konto) return i;
        return null;
    }
}
