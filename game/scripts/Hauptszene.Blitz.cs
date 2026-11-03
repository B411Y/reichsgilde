using Godot;
using Reichsgilde.Core.Entwickler;

namespace Reichsgilde;

/// <summary>
/// Blitzschlag in der Hauptszene (ein Teil der <c>partial class Hauptszene</c>): Trifft der Blitz ein Haus des Spielers, ist die Meldung eine Warnung
/// („⚠“, sie bleibt bis zum Klick stehen). Startoption <c>--blitzschlag=&lt;Gebäude&gt;[@Sekunden]</c>: so viele Sekunden nach dem Ladebildschirm
/// (ohne Angabe 3) schlägt der Blitz dort ein – über den Entwicklerbefehl <see cref="EntwicklerBlitz"/>, darum nur mit <c>--entwickler=1</c>.
/// Gebäude: Kennung der Karte („Rathaus“, „Dom“, „Martin“ in Köln …), Name eines Betriebs, <c>haus</c> oder <c>werkstatt</c> (die eigenen) oder
/// <c>1</c> (eines nach den Gewichten des Gewitters).
/// </summary>
public partial class Hauptszene
{
    /// <summary>Eine Meldung „Blitzschlag! …“ oder „Funkenflug! …“ (übergesprungenes Feuer) über ein Gebäude des Spielers wird zur Warnung.</summary>
    private string BlitzMeldung(string meldung)
    {
        if (!(meldung.StartsWith("Blitzschlag! ", StringComparison.Ordinal) || meldung.StartsWith("Funkenflug! ", StringComparison.Ordinal))
            || _simulation?.Wirtschaft is not { } w || _spieler is null) return meldung;
        var e = w.Blitz.Einschlaege.LastOrDefault();
        return e?.Ziel.Besitzer == _spieler ? "⚠ " + meldung : meldung;
    }

    /// <summary>Nach dem Ladebildschirm: <c>--blitzschlag</c> auslösen.</summary>
    private void BlitzschlagVorgeben(Dictionary<string, string> optionen)
    {
        if (!optionen.TryGetValue("blitzschlag", out var wert) || _spieler is null) return;
        var teile = wert.Split('@');
        var ziel = teile[0] is "" or "1" ? null : teile[0];
        var sekunden = teile.Length > 1 && double.TryParse(teile[1], System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 3.0;
        GetTree().CreateTimer(sekunden).Timeout += () =>
        {
            if (!IsInstanceValid(this) || !IsInsideTree()) return;
            _simulation.Bote.Einreihen(_spieler, new EntwicklerBlitz(ziel), ergebnis =>
                GD.Print($"[blitz] --blitzschlag={wert}: {(ergebnis.Erfolg ? "" : "abgelehnt – ")}{ergebnis.Meldung}"));
        };
    }
}
