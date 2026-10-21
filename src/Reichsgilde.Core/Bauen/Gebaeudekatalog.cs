using System.Text.Json;
using System.Text.Json.Serialization;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Der Gebäudekatalog (game/daten/katalog.json) – beschrieben in docs/LEVELDESIGN.md, „Gebäudekatalog“
//
//  Eine Datei für alle Karten: Je Baustein steht hier, WAS er ist (Art, Modelldatei, Grundfläche …).
//  Die Karte sagt nur, WO er steht (Katalog-Kennung, Punkt, Drehung, Name – siehe Stadtkarte).
//  Neue Modelle trägt der Modell-Agent gleich hier ein. Wie die Karte bekommt der Kern den Text, nicht den Pfad.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Die Arten der Bausteine. In der Datei kleingeschrieben. Die ersten acht stehen in Karten (<c>gebaeude</c>),
/// Natur nur in <c>natur</c>, Mauer nur in <c>mauern</c>; Werkstatt, Wohnhaus und Einwohnerhaus wählt das Spiel nach
/// Beruf, Stufe bzw. Hausart selbst – sie stehen nie direkt in einer Karte.
/// </summary>
public static class Bausteinart
{
    /// <summary>Platz für eine Werkstatt der Stadt (ohne festes Modell: Es folgt dem Beruf des Betriebs).</summary>
    public const string Werkstattplatz = "werkstattplatz";

    /// <summary>Platz für das Wohnhaus eines Spielhauses (ohne festes Modell: Es folgt der Stufe).</summary>
    public const string Wohnhausplatz = "wohnhausplatz";

    /// <summary>Platz für ein Haus der Einwohner (ohne festes Modell: Es folgt der Hausart).</summary>
    public const string Einwohnerplatz = "einwohnerplatz";

    /// <summary>Haus mit festem Modell, in dem niemand Bestimmtes wohnt.</summary>
    public const string Kulisse = "kulisse";

    /// <summary>Markt, Rathaus, Dom, später Hafen, Kirchen …</summary>
    public const string Sonderbau = "sonderbau";

    /// <summary>Betrieb vor der Stadt (Grube, Holzschlag, Hof …) mit Betriebsart.</summary>
    public const string Umlandbetrieb = "umlandbetrieb";

    /// <summary>Arbeitsstelle der Einwohner vor der Stadt (Weide, Feld, Wald).</summary>
    public const string Umlandstelle = "umlandstelle";

    /// <summary>Gelände, das ein Modell ist (der Felsberg hinter einer Grube).</summary>
    public const string Berg = "berg";

    /// <summary>Baukasten einer Mauer (Mauerstück, Mauerturm, Eckturm, Tor) – steht in <c>mauern</c>.</summary>
    public const string Mauer = "mauer";

    /// <summary>Baum, Busch, Bodenpflanze, Stein, Totholz – steht in <c>natur</c>.</summary>
    public const string Natur = "natur";

    /// <summary>Werkstattmodell eines Berufs (Stufen als Knoten im Modell).</summary>
    public const string Werkstatt = "werkstatt";

    /// <summary>Wohnhausmodell einer Stufe (0 Fachwerkhaus … 4 Stadthof).</summary>
    public const string Wohnhaus = "wohnhaus";

    /// <summary>Modell eines Einwohnerhauses (Wohnhaus, Bürgerhaus, Ratsherrenhaus).</summary>
    public const string Einwohnerhaus = "einwohnerhaus";

    /// <summary>Alle Arten, die in einer Karte unter <c>gebaeude</c> stehen dürfen.</summary>
    public static readonly IReadOnlySet<string> ImGebaeude = new HashSet<string>
    {
        Werkstattplatz, Wohnhausplatz, Einwohnerplatz, Kulisse, Sonderbau, Umlandbetrieb, Umlandstelle, Berg,
    };

    /// <summary>Alle Arten.</summary>
    public static readonly IReadOnlySet<string> Alle = new HashSet<string>(ImGebaeude) { Mauer, Natur, Werkstatt, Wohnhaus, Einwohnerhaus };
}

/// <summary>Ein Baustein des Katalogs: was er ist, welches Modell, wie viel Boden er belegt.</summary>
public sealed class Baustein
{
    /// <summary>Eindeutiger Name, auf den die Karten verweisen (klein, ohne Leerzeichen, z. B. „grube“).</summary>
    public string Kennung { get; init; } = "";

    /// <summary>Eine der <see cref="Bausteinart"/>en.</summary>
    public string Art { get; init; } = "";

    /// <summary>Anzeigename für Werkzeug und Editor (z. B. „Grube“). Fehlt er, gilt die Kennung.</summary>
    public string? Name { get; init; }

    /// <summary>Modelldatei unter <c>game/assets/modelle/</c> mit Unterordner, ohne „.glb“ (<c>gebaeude/schmied</c>). Fehlt bei Plätzen (Werkstatt-, Wohnhaus-, Einwohnerplatz) und beim Markt aus Teilen.</summary>
    public string? Modell { get; init; }

    /// <summary>Knoten im Modell, wenn die Datei ein Baukasten ist (z. B. „Weide“ in <c>umland_stellen</c>, „Tanne“ in <c>natur_baeume</c>).</summary>
    public string? Knoten { get; init; }

    /// <summary>Natur: wie viele Varianten der Knoten hat („Tanne1“ … „Tanne3“). 1 = nur „Tanne1“.</summary>
    public int Varianten { get; init; } = 1;

    /// <summary>[Breite an der Vorderseite, Tiefe] in Metern, inklusive Vorplatz. Pflicht für alles außer Mauer und Natur.</summary>
    public double[]? Groesse { get; init; }

    /// <summary>
    /// Wo der Ursprung des Modells liegt, gemessen von der Mitte der Grundfläche – lokal, ungedreht (x nach rechts, z zur Vorderseite).
    /// Fehlt er, liegt der Ursprung in der Mitte (so sind die Werkstatt- und Wohnhausmodelle gebaut).
    /// </summary>
    public double[]? Versatz { get; init; }

    /// <summary>Umlandbetrieb: welche Betriebsart (Name wie in <see cref="Betriebsart"/>, z. B. „Mine“).</summary>
    public string? Betriebsart { get; init; }

    /// <summary>Werkstatt: welcher Beruf (Name wie in <c>wirtschaft.json</c>, „berufe“).</summary>
    public string? Beruf { get; init; }

    /// <summary>Wohnhaus: welche Stufe (0 Fachwerkhaus … 4 Stadthof).</summary>
    public int? Stufe { get; init; }

    /// <summary>Einwohnerhaus: welche Hausart („Wohnhaus“, „Buergerhaus“, „Ratsherrenhaus“).</summary>
    public string? Hausart { get; init; }

    /// <summary>Höhe der Klickfläche in Metern (Umlandbetriebe).</summary>
    public double? KlickHoehe { get; init; }

    /// <summary>Höhe des Schildes mit dem Namen über dem Boden in Metern (Umlandbetriebe).</summary>
    public double? SchildHoehe { get; init; }

    /// <summary>In welchen Lagen der Baustein stehen darf (für den Level-Editor; der Kern prüft es bei Karten nicht). Fehlt sie: überall.</summary>
    public List<Lage>? Lagen { get; init; }

    /// <summary>Die Betriebsart als Wert (null, wenn keine oder unbekannt).</summary>
    [JsonIgnore]
    public Betriebsart? BetriebsartWert => Enum.TryParse<Betriebsart>(Betriebsart, ignoreCase: true, out var a) ? a : null;

    /// <summary>Der Anzeigename oder die Kennung.</summary>
    [JsonIgnore]
    public string Anzeigename => string.IsNullOrWhiteSpace(Name) ? Kennung : Name!;
}

/// <summary>
/// Der Gebäudekatalog: alle Bausteine, die in Karten stehen oder die das Spiel nach Beruf, Stufe oder Hausart wählt.
/// Liegt mit der Karte im Spielstand (<see cref="Stadtkarte.Katalog"/>), damit eine Partie mit genau ihren Maßen weiterläuft.
/// </summary>
public sealed class Gebaeudekatalog
{
    /// <summary>Das Format, das dieser Leser kennt.</summary>
    public const int BekanntesFormat = 1;

    public int Format { get; init; } = BekanntesFormat;

    public List<Baustein> Bausteine { get; init; } = new();

    private Dictionary<string, Baustein>? _nachKennung;

    /// <summary>Der Baustein mit dieser Kennung – oder null.</summary>
    public Baustein? Finde(string? kennung)
    {
        if (kennung is null) return null;
        _nachKennung ??= Bausteine.GroupBy(b => b.Kennung).ToDictionary(g => g.Key, g => g.First());
        return _nachKennung.GetValueOrDefault(kennung);
    }

    /// <summary>Das Werkstattmodell eines Berufs (Art „werkstatt“) – oder null.</summary>
    public Baustein? Werkstatt(string beruf) => Bausteine.FirstOrDefault(b => b.Art == Bausteinart.Werkstatt && b.Beruf == beruf);

    /// <summary>Das Wohnhausmodell einer Stufe (Art „wohnhaus“) – oder null.</summary>
    public Baustein? Wohnhaus(int stufe) => Bausteine.FirstOrDefault(b => b.Art == Bausteinart.Wohnhaus && b.Stufe == stufe);

    /// <summary>Das Modell eines Einwohnerhauses dieser Hausart (Art „einwohnerhaus“) – oder null.</summary>
    public Baustein? Einwohnerhaus(string hausart) => Bausteine.FirstOrDefault(b => b.Art == Bausteinart.Einwohnerhaus && b.Hausart == hausart);

    /// <summary>Der Umlandbetrieb dieser Betriebsart (Art „umlandbetrieb“) – oder null.</summary>
    public Baustein? Umland(Betriebsart art) => Bausteine.FirstOrDefault(b => b.Art == Bausteinart.Umlandbetrieb && b.BetriebsartWert == art);

    internal static readonly JsonSerializerOptions Optionen = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Liest den Katalog aus dem JSON-Text von <c>katalog.json</c> und prüft ihn.</summary>
    /// <exception cref="KartenFehler">Wenn der Text kein gültiges JSON ist oder der Katalog Fehler enthält.</exception>
    public static Gebaeudekatalog AusJson(string json)
    {
        Gebaeudekatalog? katalog;
        try
        {
            katalog = JsonSerializer.Deserialize<Gebaeudekatalog>(json, Optionen);
        }
        catch (JsonException ex)
        {
            throw new KartenFehler(new[] { $"Katalog: kein gültiges JSON (Zeile {ex.LineNumber + 1}): {ex.Message}" });
        }
        if (katalog is null)
            throw new KartenFehler(new[] { "Der Katalog ist leer." });
        var fehler = katalog.Pruefen();
        if (fehler.Count > 0)
            throw new KartenFehler(fehler);
        return katalog;
    }

    /// <summary>Liest den Katalog aus einer Datei (für Tests und Werkzeuge).</summary>
    public static Gebaeudekatalog AusDatei(string pfad) => AusJson(File.ReadAllText(pfad));

    /// <summary>Prüft jeden Baustein. Leer = alles in Ordnung.</summary>
    public List<string> Pruefen()
    {
        var f = new List<string>();
        if (Format > BekanntesFormat) f.Add($"Katalog: Format {Format} ist neuer als dieser Leser (kennt bis {BekanntesFormat}).");
        foreach (var b in Bausteine)
        {
            var wer = $"Baustein „{b.Kennung}“";
            if (string.IsNullOrWhiteSpace(b.Kennung)) { f.Add("Katalog: Ein Baustein hat keine Kennung."); continue; }
            if (!Bausteinart.Alle.Contains(b.Art))
                f.Add($"{wer}: Die Art „{b.Art}“ gibt es nicht (erlaubt: {string.Join(", ", Bausteinart.Alle.Order(StringComparer.Ordinal))}).");
            if (b.Art is not (Bausteinart.Mauer or Bausteinart.Natur) && b.Groesse is null)
                f.Add($"{wer}: groesse fehlt.");
            if (b.Groesse is { } g && (g.Length != 2 || g[0] <= 0 || g[1] <= 0))
                f.Add($"{wer}: groesse ist [Breite, Tiefe], beide größer als 0.");
            if (b.Versatz is { Length: not 2 })
                f.Add($"{wer}: versatz hat genau zwei Zahlen [x, z].");
            if (b.Varianten < 1)
                f.Add($"{wer}: varianten ist mindestens 1.");
            if (b.Art is Bausteinart.Kulisse or Bausteinart.Umlandbetrieb or Bausteinart.Umlandstelle or Bausteinart.Berg or Bausteinart.Mauer
                    or Bausteinart.Natur or Bausteinart.Werkstatt or Bausteinart.Wohnhaus or Bausteinart.Einwohnerhaus
                && string.IsNullOrWhiteSpace(b.Modell))
                f.Add($"{wer} ({b.Art}): modell fehlt.");
            if (b.Art == Bausteinart.Natur && string.IsNullOrWhiteSpace(b.Knoten))
                f.Add($"{wer} (natur): knoten fehlt.");
            if (b.Art == Bausteinart.Umlandbetrieb && b.BetriebsartWert is null)
                f.Add($"{wer} (umlandbetrieb): Die betriebsart „{b.Betriebsart}“ gibt es nicht.");
            if (b.Art == Bausteinart.Werkstatt && string.IsNullOrWhiteSpace(b.Beruf))
                f.Add($"{wer} (werkstatt): beruf fehlt.");
            if (b.Art == Bausteinart.Wohnhaus && b.Stufe is not (>= 0 and <= 4))
                f.Add($"{wer} (wohnhaus): stufe ist 0 bis 4.");
            if (b.Art == Bausteinart.Einwohnerhaus && string.IsNullOrWhiteSpace(b.Hausart))
                f.Add($"{wer} (einwohnerhaus): hausart fehlt.");
        }
        foreach (var doppelt in Bausteine.GroupBy(b => b.Kennung).Where(g => g.Count() > 1))
            f.Add($"Katalog: Die Kennung „{doppelt.Key}“ ist doppelt.");
        return f;
    }
}
