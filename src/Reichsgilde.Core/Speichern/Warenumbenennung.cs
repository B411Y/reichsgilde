using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Reichsgilde.Core.Speichern;

/// <summary>
/// Umbenannte Waren in alten Spielständen: Seit PR #232 heißt das Rapier des Schmieds „Dolch“.
///
/// Ein Warenname steht an vielen Stellen im Spielstand – als Schlüssel (Lager, Markt, Ladung eines Karrens, Waren der Daten …),
/// als Wert (Produkt eines Betriebs, Ware eines Arbeiters, Zutat …) und in Texten (Chronik, Meldungen, Berufsbeschreibung).
/// Statt jede Klasse einzeln anzufassen, wird wie bei der <see cref="Uhrumstellung"/> der JSON-Text umgeschrieben, bevor er gelesen wird:
///   • ein Schlüssel, der genau „Rapier“ ist, wird „Dolch“ (die Reihenfolge der Schlüssel bleibt);
///   • in Werten und Texten wird das ganze Wort mit seinen Endungen ersetzt: Rapier → Dolch, Rapiere → Dolche, Rapieren → Dolchen,
///     Rapiers → Dolchs.
/// Ein Spielstand ohne „Rapier“ bleibt, wie er ist. Die Werte der Ware (und das alte Rezept des Giftdolchs aus Eisen) kommen aus den
/// mitgespeicherten Daten – eine alte Partie läuft mit ihren Zahlen weiter, nur unter dem neuen Namen.
/// </summary>
public static class Warenumbenennung
{
    /// <summary>Alte Warennamen und ihre neuen.</summary>
    public static readonly IReadOnlyDictionary<string, string> Namen = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Rapier"] = "Dolch",
    };

    /// <summary>Das Wort mit deutschen Endungen (Plural, Dativ Plural, Genitiv) – nur ganze Wörter.</summary>
    private static readonly Regex ImText = new(@"\bRapier(en|e|s)?\b", RegexOptions.CultureInvariant);

    /// <summary>Der Name, unter dem die Ware heute geführt wird (ein unbekannter bleibt, wie er ist).</summary>
    public static string NeuerName(string ware) => Namen.TryGetValue(ware, out var neu) ? neu : ware;

    /// <summary>Ein Wert oder Text mit dem alten Namen, umgeschrieben („Rapier“ → „Dolch“, „3 Rapiere verkauft“ → „3 Dolche verkauft“).</summary>
    public static string Text(string text) => text.Contains("Rapier", StringComparison.Ordinal) ? ImText.Replace(text, "Dolch$1") : text;

    /// <summary>Schreibt alle alten Warennamen im Spielstand um (Schlüssel, Werte, Texte).</summary>
    public static void Umbenennen(JsonNode? knoten)
    {
        switch (knoten)
        {
            case JsonObject o:
                // gäbe es den neuen Schlüssel schon, bliebe der alte stehen (kommt nicht vor)
                if (o.Any(p => Namen.TryGetValue(p.Key, out var n) && !o.ContainsKey(n)))
                {
                    // Schlüssel umbenennen, Reihenfolge behalten: alle heraus, umbenannt wieder hinein
                    var eintraege = o.ToList();
                    o.Clear();
                    foreach (var (name, wert) in eintraege)
                        o[eintraege.Any(e => e.Key == NeuerName(name)) ? name : NeuerName(name)] = wert;
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
