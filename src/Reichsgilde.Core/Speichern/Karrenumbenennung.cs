using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Reichsgilde.Core.Wirtschaft;

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

    /// <summary>Die alten Namen als ganze Wörter am Wortanfang (auch „Ochsenkarren 2“, „Pferdekarrens“), mit einem davorstehenden „einen “,
    /// falls vorhanden – Meldungen schreiben „für einen Pferdekarren“, das wird zu „für einen Großen Pferdekarren“ (Akkusativ).</summary>
    private static readonly Regex ImText = new(@"(?<einen>(?<!\p{L})[Ee]inen )?(?<!\p{L})(?<name>Ochsenkarren|Pferdekarren|Kriegskarren)", RegexOptions.CultureInvariant);

    /// <summary>Ein Wert oder Text mit alten Namen, in einem Durchgang umgeschrieben („Pferdekarren 2“ → „Großer Pferdekarren 2“,
    /// „einen Kriegskarren“ → „einen Großen Pferdekarren“).</summary>
    public static string Text(string text) => ImText.Replace(text, m =>
    {
        var neu = Namen[m.Groups["name"].Value];
        var einen = m.Groups["einen"];
        return einen.Success ? einen.Value + LogistikDaten.KarrenartAkkusativ(neu) : neu;
    });

    /// <summary>Schreibt einen Spielstand bis Format 3 auf die neuen Namen um (neuere bleiben, wie sie sind).
    /// Ein Stand ohne Feld „Format“ bleibt ebenfalls, wie er ist: Das Lesen setzt dann das aktuelle Format ein (<see cref="Speicherstand.Format"/>),
    /// und jede Programmversion hat das Feld geschrieben – ein solcher Stand ist von Hand gebaut und trägt schon die neuen Namen.
    /// Ein umgeschriebener Stand trägt danach Format 4, ein zweiter Lauf ändert nichts.</summary>
    public static void Umrechnen(JsonObject stand)
    {
        if ((int?)stand["Format"] is not int format || format >= FormatMitNeuenNamen) return;
        Umbenennen(stand);
        // Wie die Uhrumstellung: danach trägt der Stand das neue Format, ein zweiter Lauf ändert nichts mehr
        // (Format 1 bleibt, damit das Lesen ihn weiter als zu alt ablehnt).
        if (format >= 2) stand["Format"] = FormatMitNeuenNamen;
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
