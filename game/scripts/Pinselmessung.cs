using Godot;

namespace Reichsgilde;

/// <summary>
/// Messung der Werkstatt-Pinsel (Startoption <c>--pinselmessung=&lt;datei.md&gt;</c> bzw. <c>=1</c>, nur mit <c>--kartenwerkstatt=…</c>): Die Werkstatt
/// malt nach dem Laden mit jeder Pinselart einen Strich aus 30 Tupfern (je Tupfer ein Bild) und schreibt je Tupfer und Teilschritt die Rechenzeit auf
/// (Kartenwerkstatt.Pinselmessung.cs). Hier liegen nur die Summen: <c>var t = Pinselmessung.Uhr(); …; Pinselmessung.Dazu("Gewichtsbild", t);</c>.
/// Ohne die Option tut die Klasse nichts. Headless misst die Rechenzeit auf dem Hauptfaden, nicht Grafikkarte und Bildschirm.
/// </summary>
public static class Pinselmessung
{
    private static readonly string? Wert = OS.GetCmdlineUserArgs().LastOrDefault(a => a.StartsWith("--pinselmessung="))?["--pinselmessung=".Length..];

    /// <summary>Ist die Messung an?</summary>
    public static bool An => Wert is { Length: > 0 } && Wert != "0";

    /// <summary>Wohin die Tabelle geht (eine Datei auf .md) – sonst nur ins Log.</summary>
    public static string? Datei => Wert is { } w && w.EndsWith(".md") ? w : null;

    private static readonly Dictionary<string, (ulong Usec, int Anzahl)> _summen = new();

    /// <summary>Der Anfang einer Messung (0, wenn die Messung aus ist).</summary>
    public static ulong Uhr() => An ? Time.GetTicksUsec() : 0;

    /// <summary>Zählt die Zeit seit <paramref name="anfang"/> zur Summe <paramref name="name"/>.</summary>
    public static void Dazu(string name, ulong anfang)
    {
        if (!An) return;
        var (usec, anzahl) = _summen.GetValueOrDefault(name);
        _summen[name] = (usec + Time.GetTicksUsec() - anfang, anzahl + 1);
    }

    /// <summary>Die Summen seit dem letzten Abholen (Name → Mikrosekunden) – danach leer.</summary>
    public static Dictionary<string, ulong> Abholen()
    {
        var ergebnis = _summen.ToDictionary(e => e.Key, e => e.Value.Usec);
        _summen.Clear();
        return ergebnis;
    }
}
