using System.Text.Json;
using System.Text.Json.Serialization;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Kartendatei einer Stadt (game/data/karten/<kennung>.json, Format 1)
//
//  Erzeugt vom Kartenwerkzeug des Leveldesigns (tools/karten/), beschrieben in docs/LEVELDESIGN.md §9.
//  Der Kern liest nur, was für Regeln mit Wirkung gebraucht wird: Wege mit ihrer Klasse, Bauzonen
//  und feste Bauten (darunter die Wohnhausplätze der Häuser). Alles andere (Mauer, Wasser, Gelände …)
//  zeichnet die Darstellung und überspringt der Leser hier.
//
//  Koordinaten: Meter auf dem Boden, x = Osten, z = Süden – wie in der Stadtansicht.
//  Wie bei wirtschaft.json bekommt der Kern den Text, nicht den Pfad: Godot liest die Datei (res://),
//  die Tests lesen sie von der Platte.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Lage einer Bauzone (LEVELDESIGN §2). In der Datei kleingeschrieben („kern“, „mauerring“ …).</summary>
public enum Lage
{
    Kern,
    Mauerring,
    Vorstadt,
    Umland,
}

/// <summary>Klasse eines Weges (LEVELDESIGN §5). Land-, Haupt-, Ring- und Wallstraße tragen Karren, Gassen und Leinpfade nur Fußgänger.</summary>
public enum Wegklasse
{
    Land,
    Haupt,
    Ring,
    Wall,
    Gasse,
    Leinpfad,
}

/// <summary>Ein Weg der Karte: Name, Klasse und Mittellinie als Punktfolge.</summary>
public sealed class Kartenweg
{
    public string Name { get; init; } = "";
    public Wegklasse Klasse { get; init; }

    /// <summary>Die Mittellinie, je Punkt [x, z].</summary>
    public List<double[]> Linie { get; init; } = new();

    /// <summary>Gefahrenfaktor um 1,0 (Vorschlag Leveldesign, noch ohne Wirkung).</summary>
    public double Gefahr { get; init; } = 1;

    /// <summary>Fahren hier Karren? (Werkstätten brauchen ihren Eingang an einer Karrenstraße.)</summary>
    [JsonIgnore]
    public bool IstKarrenstrasse => Klasse is Wegklasse.Land or Wegklasse.Haupt or Wegklasse.Ring or Wegklasse.Wall;

    /// <summary>Kürzester Abstand eines Punktes zur Mittellinie (Meter).</summary>
    public double Abstand(double x, double z)
    {
        var best = double.MaxValue;
        for (var i = 0; i + 1 < Linie.Count; i++)
            best = Math.Min(best, Geometrie.AbstandZurStrecke(x, z, Linie[i][0], Linie[i][1], Linie[i + 1][0], Linie[i + 1][1]));
        return Linie.Count == 1 ? Math.Sqrt(Math.Pow(x - Linie[0][0], 2) + Math.Pow(z - Linie[0][1], 2)) : best;
    }
}

/// <summary>Eine Bauzone: eine Fläche zwischen den Straßen, in der frei auf dem Raster gebaut wird (LEVELDESIGN §3).</summary>
public sealed class Bauzone
{
    public string Kennung { get; init; } = "";
    public Lage Art { get; init; }

    /// <summary>Umriss als Vieleck, je Punkt [x, z] (auf dem 2-m-Raster).</summary>
    public List<double[]> Flaeche { get; init; } = new();

    /// <summary>Liegt der Punkt in der Zone?</summary>
    public bool Enthaelt(double x, double z) => Geometrie.ImVieleck(x, z, Flaeche);

    /// <summary>Abstand eines Punktes zum Rand der Zone (0, wenn er drin liegt).</summary>
    public double Abstand(double x, double z)
    {
        if (Enthaelt(x, z)) return 0;
        var best = double.MaxValue;
        for (var i = 0; i < Flaeche.Count; i++)
        {
            var (a, b) = (Flaeche[i], Flaeche[(i + 1) % Flaeche.Count]);
            best = Math.Min(best, Geometrie.AbstandZurStrecke(x, z, a[0], a[1], b[0], b[1]));
        }
        return best;
    }
}

/// <summary>
/// Ein fester Bau der Karte: Sonderbau, Kirche, Startgrundstück, Leerstand – und (ab der Greybox) die Wohnhausplätze
/// der Häuser (<c>art: "wohnhaus"</c>, in Konto-Reihenfolge) sowie die Werkstätten (<c>art: "werkstatt"</c>, <see cref="Name"/> = Betriebsname).
/// </summary>
public sealed class FesterBau
{
    public string Kennung { get; init; } = "";

    /// <summary>„start“, „sonderbau“, „kirche“, „leerstand“, „wohnhaus“ oder „werkstatt“ (Unbekanntes wird nur gelesen).</summary>
    public string Art { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Mitte der belegten Fläche [x, z] (inkl. Vorplatz).</summary>
    public double[] Punkt { get; init; } = { 0, 0 };

    /// <summary>Ein Punkt zur Straße hin – nur seine Richtung zählt (auf 90° eingerastet). Fehlt er, zeigt der Eingang nach Süden.</summary>
    public double[]? Eingang { get; init; }

    /// <summary>[Breite entlang der Straße, Tiefe] in Metern. Fehlt sie, blockiert der Bau keinen Bauplatz (außer Kirche und Leerstand, siehe <see cref="Grundriss"/>).</summary>
    public double[]? Groesse { get; init; }

    /// <summary>Konfession einer Kirche (nur gelesen).</summary>
    public string? Konfession { get; init; }

    /// <summary>
    /// Die Fläche, die der Bau belegt – oder null, wenn auf ihm nichts im Weg steht (z. B. der offene Markt).
    /// Kirche und Leerstand ohne Größe belegen 16 × 14 m, wie im Kapazitätswerkzeug (tools/karten/kapazitaet.py).
    /// </summary>
    [JsonIgnore]
    public Grundriss? Grundriss
    {
        get
        {
            var groesse = Groesse ?? (Art is "kirche" or "leerstand" ? new double[] { 16, 14 } : null);
            if (groesse is not { Length: 2 }) return null;
            var drehung = Eingang is { Length: 2 } e ? Bauen.Grundriss.DrehungZu(e[0] - Punkt[0], e[1] - Punkt[1]) : 0;
            return new Grundriss(Punkt[0], Punkt[1], groesse[0], groesse[1], drehung);
        }
    }
}

/// <summary>Die Kartendatei ist nicht lesbar oder widerspricht sich. Enthält alle gefundenen Fehler.</summary>
public sealed class KartenFehler : Exception
{
    public IReadOnlyList<string> Fehler { get; }

    public KartenFehler(IReadOnlyList<string> fehler)
        : base("Die Kartendatei ist fehlerhaft:\n  • " + string.Join("\n  • ", fehler))
    {
        Fehler = fehler;
    }
}

/// <summary>
/// Die Karte einer Stadt, so weit sie der Kern braucht: Wege, Bauzonen, feste Bauten und Wohnhausplätze.
/// Liegt im Spielstand mit (wie die Wirtschaftsdaten), damit eine Partie mit genau ihrer Karte weiterläuft.
/// </summary>
public sealed class Stadtkarte
{
    /// <summary>Das Format, das dieser Leser kennt. Neue Felder ohne Bedeutungsänderung brauchen keine neue Nummer.</summary>
    public const int BekanntesFormat = 1;

    public int Format { get; init; } = BekanntesFormat;
    public string Kennung { get; init; } = "";
    public string Stadt { get; init; } = "";

    /// <summary>Rastergröße der Bauzonen in Metern (2).</summary>
    public double Raster { get; init; } = 2;

    public List<Kartenweg> Wege { get; init; } = new();
    public List<Bauzone> Bauzonen { get; init; } = new();
    public List<FesterBau> Feste { get; init; } = new();

    /// <summary>Die Wohnhausplätze der Häuser in Konto-Reihenfolge (feste Bauten der Art „wohnhaus“).</summary>
    [JsonIgnore]
    public IReadOnlyList<FesterBau> Wohnhausplaetze => Feste.Where(f => f.Art == "wohnhaus").ToList();

    private static readonly JsonSerializerOptions Optionen = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }, // "klasse": "haupt" → Wegklasse.Haupt (ohne Rücksicht auf Groß/klein)
    };

    /// <summary>Liest die Karte aus dem JSON-Text der Kartendatei und prüft sie.</summary>
    /// <exception cref="KartenFehler">Wenn der Text kein gültiges JSON ist oder die Karte Fehler enthält.</exception>
    public static Stadtkarte AusJson(string json)
    {
        Stadtkarte? karte;
        try
        {
            karte = JsonSerializer.Deserialize<Stadtkarte>(json, Optionen);
        }
        catch (JsonException ex)
        {
            throw new KartenFehler(new[] { $"Kein gültiges JSON (Zeile {ex.LineNumber + 1}): {ex.Message}" });
        }
        if (karte is null)
            throw new KartenFehler(new[] { "Die Datei ist leer." });
        var fehler = karte.Pruefen();
        if (fehler.Count > 0)
            throw new KartenFehler(fehler);
        return karte;
    }

    /// <summary>Liest die Karte aus einer Datei (für Tests und Werkzeuge).</summary>
    public static Stadtkarte AusDatei(string pfad) => AusJson(File.ReadAllText(pfad));

    /// <summary>Prüft, was der Kern braucht. Die Kartenregeln selbst prüft schon das Werkzeug beim Schreiben.</summary>
    public List<string> Pruefen()
    {
        var f = new List<string>();
        if (Format > BekanntesFormat) f.Add($"Format {Format} ist neuer als dieser Leser (kennt bis {BekanntesFormat}).");
        if (Raster <= 0) f.Add("raster muss größer als 0 sein.");
        foreach (var w in Wege)
        {
            if (w.Linie.Count < 2) f.Add($"Weg „{w.Name}“: Die Linie braucht mindestens zwei Punkte.");
            if (w.Linie.Any(p => p.Length != 2)) f.Add($"Weg „{w.Name}“: Jeder Punkt hat genau zwei Zahlen [x, z].");
        }
        foreach (var z in Bauzonen)
        {
            if (z.Flaeche.Count < 3) f.Add($"Bauzone {z.Kennung}: Die Fläche braucht mindestens drei Punkte.");
            if (z.Flaeche.Any(p => p.Length != 2)) f.Add($"Bauzone {z.Kennung}: Jeder Punkt hat genau zwei Zahlen [x, z].");
        }
        foreach (var b in Feste)
        {
            if (b.Punkt.Length != 2) f.Add($"Fester Bau {b.Kennung}: punkt hat genau zwei Zahlen [x, z].");
            if (b.Eingang is { Length: not 2 }) f.Add($"Fester Bau {b.Kennung}: eingang hat genau zwei Zahlen [x, z].");
            if (b.Groesse is { } g && (g.Length != 2 || g[0] <= 0 || g[1] <= 0)) f.Add($"Fester Bau {b.Kennung}: groesse ist [Breite, Tiefe], beide größer als 0.");
            if (b.Art is "wohnhaus" or "werkstatt" && b.Groesse is null) f.Add($"Fester Bau {b.Kennung} ({b.Art}): groesse fehlt.");
            if (b.Art == "werkstatt" && string.IsNullOrWhiteSpace(b.Name)) f.Add($"Fester Bau {b.Kennung}: Eine Werkstatt braucht den Betriebsnamen als name.");
        }
        foreach (var doppelt in Feste.GroupBy(b => b.Kennung).Where(g => g.Count() > 1))
            f.Add($"Feste Bauten: Die Kennung „{doppelt.Key}“ ist doppelt.");
        return f;
    }

    /// <summary>Die Bauzone, in der der Punkt liegt – sonst die nächste (null ohne Bauzonen).</summary>
    public Bauzone? ZoneBei(double x, double z) =>
        Bauzonen.FirstOrDefault(b => b.Enthaelt(x, z))
        ?? Bauzonen.OrderBy(b => b.Abstand(x, z)).ThenBy(b => b.Kennung, StringComparer.Ordinal).FirstOrDefault();
}

/// <summary>Kleine Rechenhilfen für Punkte und Flächen auf dem Boden.</summary>
internal static class Geometrie
{
    /// <summary>Kürzester Abstand vom Punkt p zur Strecke a–b.</summary>
    public static double AbstandZurStrecke(double px, double pz, double ax, double az, double bx, double bz)
    {
        var (dx, dz) = (bx - ax, bz - az);
        var l2 = dx * dx + dz * dz;
        var t = l2 == 0 ? 0 : Math.Clamp(((px - ax) * dx + (pz - az) * dz) / l2, 0, 1);
        var (qx, qz) = (ax + t * dx - px, az + t * dz - pz);
        return Math.Sqrt(qx * qx + qz * qz);
    }

    /// <summary>Liegt der Punkt im Vieleck? (Strahlverfahren, wie im Kartenwerkzeug.)</summary>
    public static bool ImVieleck(double x, double z, IReadOnlyList<double[]> vieleck)
    {
        var drin = false;
        for (int i = 0, j = vieleck.Count - 1; i < vieleck.Count; j = i++)
        {
            var (x1, z1, x2, z2) = (vieleck[i][0], vieleck[i][1], vieleck[j][0], vieleck[j][1]);
            if ((z1 > z) != (z2 > z) && x < (x2 - x1) * (z - z1) / (z2 - z1) + x1)
                drin = !drin;
        }
        return drin;
    }
}
