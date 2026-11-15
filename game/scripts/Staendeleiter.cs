using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Ständeleiter auf der Ständetafel der Amtsstube (Nutzer, Rathaus Stufe 10): alle Stände aus den Spieldaten (<c>staende</c> in
/// wirtschaft.json, dieselbe Liste, aus der „Euer Stand“ kommt) mit ihrer Schwelle, der eigene hervorgehoben. Nur vorhandene Daten,
/// Zahlen über <see cref="Zahlenformat"/>. Logik ohne Godot (<c>StaendeleiterTests</c>); die Schilder setzt Stadtansicht.Rathausneubau.cs.
/// </summary>
public static class Staendeleiter
{
    /// <summary>Eine Zeile der Tafel: Text und ob es der eigene Stand ist.</summary>
    public sealed record Zeile(string Text, bool Eigener);

    /// <summary>Die Zeilen vom untersten Stand (Index 0) aufwärts; <paramref name="eigenerStand"/> wie <c>Konto.Stand</c> (wird begrenzt).</summary>
    public static IReadOnlyList<Zeile> Zeilen(IReadOnlyList<StandDaten> staende, int eigenerStand, string waehrung)
    {
        var eigener = staende.Count == 0 ? -1 : Math.Clamp(eigenerStand, 0, staende.Count - 1);
        return staende.Select((s, i) => new Zeile(
            (i == eigener ? "» " : "") + s.Name + (s.Schwelle > 0 ? $" · ab {Zahlenformat.Ganz(s.Schwelle)} {waehrung}" : "") + (i == eigener ? " «" : ""),
            i == eigener)).ToList();
    }
}
