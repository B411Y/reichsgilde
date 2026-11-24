using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Reichsgilde.Core.Speichern;

/// <summary>
/// Umbenannte Karrenarten in alten Spielständen (Format bis 3; Entscheidung des Nutzers, 23.11.2026, Datenversion 99):
///   • den Ochsenkarren gibt es nicht mehr – an seine Stelle tritt der „Pferdekarren“ mit einem Pferd und denselben Werten;
///   • der bisherige „Pferdekarren“ mit zwei Pferden heißt „Großer Pferdekarren“ (gleiche Werte);
///   • der „Kriegskarren“ (bis 04.11.2026 der Name des Zweispänners) wird ebenfalls zum „Großen Pferdekarren“.
///
/// Weil „Pferdekarren“ vorher und nachher etwas anderes bedeutet, geht das nicht über den Namen allein: Es gilt nur für Spielstände bis Format 3
/// (ab Format 4 tragen sie die neuen Namen). Wie bei der <see cref="Warenumbenennung"/> wird der JSON-Text vor dem Lesen umgeschrieben –
/// Schlüssel (Karren der mitgespeicherten Daten), Werte (Art und Name jedes Karrens, Startkarren, Befehle in der Warteschlange) und Texte
/// (Meldungen, Chronik). Alle drei Namen werden in einem Durchgang ersetzt, so wird ein Ochsenkarren nie zweimal umbenannt.
/// Die Reihenfolge der Schlüssel bleibt; die Werte kommen aus den mitgespeicherten Daten – eine alte Partie läuft mit ihren Zahlen weiter.
/// </summary>
public static class Karrenumbenennung
{
    /// <summary>Ab diesem Format tragen Spielstände die neuen Namen.</summary>
    public const int FormatMitNeuenNamen = 4;

    /// <summary>Alte Kennungen und ihre neuen (für Spielstände bis Format 3).</summary>
    public static readonly IReadOnlyDictionary<string, string> Namen = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Ochsenkarren"] = "Pferdekarren",
        ["Pferdekarren"] = "Großer Pferdekarren",
        ["Kriegskarren"] = "Großer Pferdekarren",
    };

    /// <summary>Die alten Namen als ganze Wörter am Wortanfang (auch „Ochsenkarren 2“, „Pferdekarrens“).</summary>
    private static readonly Regex ImText = new(@"(?<!\p{L})(Ochsenkarren|Pferdekarren|Kriegskarren)", RegexOptions.CultureInvariant);

    /// <summary>Ein Wert oder Text mit alten Namen, in einem Durchgang umgeschrieben („Pferdekarren 2“ → „Großer Pferdekarren 2“).</summary>
    public static string Text(string text) => ImText.Replace(text, m => Namen[m.Value]);

    /// <summary>Schreibt einen Spielstand bis Format 3 auf die neuen Namen um (neuere bleiben, wie sie sind).</summary>
    public static void Umrechnen(JsonObject stand)
    {
        if ((int?)stand["Format"] is int format && format >= FormatMitNeuenNamen) return;
        Umbenennen(stand);
    }

    private static void Umbenennen(JsonNode? knoten)
    {
        switch (knoten)
        {
            case JsonObject o:
                if (o.Any(p => Namen.ContainsKey(p.Key)))
                {
                    // Schlüssel umbenennen, Reihenfolge behalten: alle heraus, umbenannt wieder hinein
                    var eintraege = o.ToList();
                    o.Clear();
                    foreach (var (name, wert) in eintraege) o[Namen.GetValueOrDefault(name, name)] = wert;
                }
                foreach (var (name, wert) in o.ToList())
                {
                    if (wert is JsonValue v && v.TryGetValue<string>(out var s))
                    {
                        if (Text(s) is var neu && neu != s) o[name] = neu;
                    }
                    else Umbenennen(wert);
                }
                break;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                {
                    if (a[i] is JsonValue v && v.TryGetValue<string>(out var s))
                    {
                        if (Text(s) is var neu && neu != s) a[i] = neu;
                    }
                    else Umbenennen(a[i]);
                }
                break;
        }
    }
}
