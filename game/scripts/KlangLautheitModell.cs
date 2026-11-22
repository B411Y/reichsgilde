using System.Text.Json;

namespace Reichsgilde;

/// <summary>
/// Die Lautheits-Angleichung der Oberflächenklänge (Ton Schritt 7), ohne Godot (Test: <c>KlangLautheitTests.cs</c>). Die Datei
/// <c>daten/klang_lautheit.json</c> kommt aus <c>tools/ton/lautheit_angleichen.py</c> und gibt je Audiodatei an, um wie viele dB die
/// Variante eines Ereignisses lauter oder leiser gespielt wird, damit alle Varianten eines Ereignisses gleich laut sind
/// (höchste 400-ms-Lautheit, BS.1770). Die Audiodateien selbst bleiben unverändert.
/// </summary>
public static class KlangLautheitModell
{
    /// <summary>Liest <c>{"angleichung_db": {"datei": dB, …}}</c>; Dateien ohne Eintrag bleiben bei 0 dB.</summary>
    public static Dictionary<string, float> Laden(string json)
    {
        var ergebnis = new Dictionary<string, float>(StringComparer.Ordinal);
        using var dok = JsonDocument.Parse(json);
        if (dok.RootElement.TryGetProperty("angleichung_db", out var feld))
            foreach (var p in feld.EnumerateObject()) ergebnis[p.Name] = (float)p.Value.GetDouble();
        return ergebnis;
    }

    /// <summary>Angleichung der Datei in dB (0, wenn keine eingetragen ist).</summary>
    public static float Angleichung(IReadOnlyDictionary<string, float> tabelle, string datei) =>
        tabelle.TryGetValue(datei, out var db) ? db : 0f;
}
