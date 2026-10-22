using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Schreibt eine Kartendatei so, wie das Python-Werkzeug bis Schritt 4 sie schrieb (karte.py, json_kompakt)
//
//  Einrückung mit einem Leerzeichen je Ebene; was in eine Zeile von höchstens 190 Zeichen passt (ein Gebäude, ein Tor, ein Weg),
//  steht in einer Zeile, lange Punktfolgen mit mehreren Punkten je Zeile. Ganze Zahlen ohne „.0“, Umlaute im Klartext.
//  Ziel: Eine Karte, die die Kartenwerkstatt öffnet und unverändert speichert, ist Byte für Byte dieselbe Datei – so bleiben
//  Git-Änderungen klein, und Werkzeug und Werkstatt können sich bis zum Umzug (Schritt 4) abwechseln.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Schreibt JSON im Zeilenformat des Kartenwerkzeugs.</summary>
public static class Kartenschreiber
{
    /// <summary>So breit darf eine Zeile werden, bevor sie umbricht (wie im Werkzeug).</summary>
    public const int Zeilenbreite = 190;

    /// <summary>Der ganze Text der Datei (mit Zeilenende am Schluss, wie das Werkzeug).</summary>
    public static string Kompakt(JsonNode? wurzel) => Ausgabe(wurzel, 0) + "\n";

    private static string Ausgabe(JsonNode? x, int tiefe)
    {
        var flach = Flach(x);
        var leer = x is JsonObject { Count: 0 } or JsonArray { Count: 0 };
        if (Laenge(flach) + tiefe <= Zeilenbreite || x is not (JsonObject or JsonArray) || leer)
            return flach;
        var innen = new string(' ', tiefe + 1);
        var aussen = new string(' ', tiefe);
        if (x is JsonObject obj)
            return "{\n" + string.Join(",\n", obj.Select(e => $"{innen}{Text(e.Key)}: {Ausgabe(e.Value, tiefe + 1)}")) + "\n" + aussen + "}";

        var liste = (JsonArray)x;
        if (liste.All(IstPunkt))
        {
            // Punktfolgen: so viele Punkte je Zeile, bis die Zeile 20 Zeichen vor dem Rand ist
            var zeilen = new List<string>();
            var zeile = new List<string>();
            foreach (var p in liste)
            {
                zeile.Add(Flach(p));
                if (Laenge(string.Join(", ", zeile)) + tiefe > Zeilenbreite - 20)
                {
                    zeilen.Add(string.Join(", ", zeile));
                    zeile.Clear();
                }
            }
            if (zeile.Count > 0) zeilen.Add(string.Join(", ", zeile));
            return "[\n" + string.Join(",\n", zeilen.Select(z => innen + z)) + "\n" + aussen + "]";
        }
        return "[\n" + string.Join(",\n", liste.Select(v => innen + Ausgabe(v, tiefe + 1))) + "\n" + aussen + "]";
    }

    /// <summary>Ein Punkt [x, z]: genau zwei Zahlen.</summary>
    private static bool IstPunkt(JsonNode? v) => v is JsonArray { Count: 2 } a && a.All(z => z is JsonValue w && w.GetValueKind() == JsonValueKind.Number);

    /// <summary>Alles in einer Zeile, Trenner „, “ und „: “ (wie <c>json.dumps(…, separators=(", ", ": "))</c>).</summary>
    private static string Flach(JsonNode? x) => x switch
    {
        null => "null",
        JsonObject o => "{" + string.Join(", ", o.Select(e => Text(e.Key) + ": " + Flach(e.Value))) + "}",
        JsonArray a => "[" + string.Join(", ", a.Select(Flach)) + "]",
        JsonValue v => Wert(v),
        _ => throw new InvalidOperationException("Unbekannter JSON-Knoten"),
    };

    private static string Wert(JsonValue v)
    {
        switch (v.GetValueKind())
        {
            case JsonValueKind.String: return Text(v.GetValue<string>());
            case JsonValueKind.True: return "true";
            case JsonValueKind.False: return "false";
            case JsonValueKind.Null: return "null";
            case JsonValueKind.Number:
                if (v.TryGetValue<JsonElement>(out var e))
                    return e.TryGetInt64(out var ganz) ? ganz.ToString(CultureInfo.InvariantCulture) : Zahl(e.GetDouble());
                if (v.TryGetValue<int>(out var i)) return i.ToString(CultureInfo.InvariantCulture);
                if (v.TryGetValue<long>(out var l)) return l.ToString(CultureInfo.InvariantCulture);
                return Zahl(v.GetValue<double>());
            default: throw new InvalidOperationException($"Unbekannter JSON-Wert {v.GetValueKind()}");
        }
    }

    /// <summary>
    /// Eine Zahl wie Python: ganze Zahlen ohne „.0“ (das Werkzeug schreibt 50 statt 50.0), sonst die kürzeste Schreibweise, die beim
    /// Lesen denselben Wert ergibt („-11.15“, „0.85“). Beide Sprachen finden dieselben Ziffern; nur den Exponenten schreibt Python klein.
    /// </summary>
    public static string Zahl(double x)
    {
        if (x == Math.Floor(x) && Math.Abs(x) < 1e15) return ((long)x).ToString(CultureInfo.InvariantCulture);
        return x.ToString("R", CultureInfo.InvariantCulture).Replace("E", "e");
    }

    /// <summary>Eine Zeichenkette wie <c>json.dumps(…, ensure_ascii=False)</c>: Umlaute bleiben, Steuerzeichen werden umschrieben.</summary>
    private static string Text(string s)
    {
        var sb = new StringBuilder(s.Length + 2).Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    /// <summary>Länge in Zeichen wie Python (ein Zeichen außerhalb der Grundebene zählt einmal, nicht zweimal).</summary>
    private static int Laenge(string s)
    {
        var n = s.Length;
        foreach (var c in s)
            if (char.IsLowSurrogate(c)) n--;
        return n;
    }
}
