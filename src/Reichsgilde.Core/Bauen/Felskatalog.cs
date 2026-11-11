using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Der Katalog der Felsgruppen (Felspinsel P5, wie „Packed Level Actor“ in Unreal)
//
//  Eine gut gebaute Formation (Wand, Fuß, Brocken) wird ein Eintrag, den man gedreht und gespiegelt wieder stempelt oder streut. Der Katalog ist
//  gemeinsam für alle Karten (Nutzer, Frage 5 des Fels-Plans): Die Werkstatt legt ihn als eigene Datei neben die Karten (user://felsgruppen.json),
//  nicht in eine Karte. Er enthält die vier festen Gruppen (Felsformen.Gruppen, nicht zu ändern) und die eigenen – nur die eigenen stehen in der Datei.
//  Eine Karte speichert nie die Vorlage, nur die gestempelten Stücke (mit der Nummer ihrer Gruppe, „gruppe“ am Stück): Löschen oder Umbenennen
//  im Katalog ändert keine Karte, und jede Maschine liest dieselben Kartenbytes (Fels bleibt Kartendatei, kein Spielbefehl).
//
//  Datei: { "version": 1, "gruppen": [ { "kennung": "eigen_1", "name": "Steinbruchkante", "stuecke": [ { "form": "wand_gross_1",
//  "punkt": [0, 0], "drehung": 0, "massstab": 1 }, { "form": "block", "punkt": [4.5, 3.2], "drehung": 35, "massstab": 0.8, "einsinken": -0.05,
//  "gestein": "trachyt", "kippen": [2, -3], "achsen": [1, 1.2, 1], "versatz": 0.3 } ] } ] } – alles hinter „massstab“ nur, wenn es nicht der Vorgabe
//  entspricht. Geschrieben mit dem Kartenschreiber: gleicher Katalog, gleiche Bytes.
//
//  Ablegen (Kartenentwurf.FelsGruppenstuecke): Das größte Stück wird die Mitte (Lage 0, 0, Drehung 0) – der Stempel dreht die Gruppe so, wie er
//  dieses Stück drehen würde (bei „Schauseite hangabwärts“ zeigt es hangabwärts). Die anderen liegen relativ dazu, gerundet wie in der Karte.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Die festen und eigenen Felsgruppen (siehe Kopf der Datei). Unveränderlich: Ablegen, Umbenennen und Löschen geben einen neuen Katalog.</summary>
public sealed class Felskatalog
{
    /// <summary>Höchstens so viele Stücke hat eine eigene Gruppe (die festen haben 5 bis 8; ein Stempel bleibt so ein kleiner Schritt).</summary>
    public const int StueckeMax = 64;

    /// <summary>Mindestens so viele Stücke braucht eine eigene Gruppe (ein Stück ist eine Form, keine Gruppe).</summary>
    public const int StueckeMin = 2;

    /// <summary>Höchstens so viele eigene Gruppen.</summary>
    public const int GruppenMax = 200;

    /// <summary>Längster Name (Zeichen).</summary>
    public const int NameMax = 40;

    /// <summary>So beginnt die Kennung jeder eigenen Gruppe („eigen_1“, „eigen_2“ …) – sie kann nie mit einer festen Gruppe zusammenfallen.</summary>
    public const string KennungVorsatz = "eigen_";

    /// <summary>Der Dateiname des Katalogs im Benutzerordner der Werkstatt.</summary>
    public const string Dateiname = "felsgruppen.json";

    /// <summary>Ein leerer Katalog: nur die festen Gruppen.</summary>
    public static readonly Felskatalog Leer = new(Array.Empty<Felsgruppe>());

    private Felskatalog(IReadOnlyList<Felsgruppe> eigene) => Eigene = eigene;

    /// <summary>Die eigenen Gruppen in der Reihenfolge, in der sie abgelegt wurden.</summary>
    public IReadOnlyList<Felsgruppe> Eigene { get; }

    /// <summary>Alle Gruppen: erst die festen, dann die eigenen (Reihenfolge der Tafel).</summary>
    public IReadOnlyList<Felsgruppe> Alle => Felsformen.Gruppen.Concat(Eigene).ToList();

    /// <summary>Die Gruppe mit dieser Kennung (fest oder eigen) – oder null.</summary>
    public Felsgruppe? Finde(string? kennung) => Felsformen.Gruppe(kennung) ?? Eigene.FirstOrDefault(g => g.Kennung == kennung);

    /// <summary>Ist das eine eigene Gruppe dieses Katalogs (nur die lassen sich umbenennen und löschen)?</summary>
    public bool IstEigen(string? kennung) => Eigene.Any(g => g.Kennung == kennung);

    // ── Ändern ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Legt eine Gruppe aus diesen Stücken ab (aus <see cref="Kartenentwurf.FelsGruppenstuecke"/>): neue Kennung „eigen_n“ (die kleinste freie Nummer
    /// über allen bisher vergebenen), der Name bereinigt. Die Stücke werden geprüft und gerundet wie in der Datei.
    /// </summary>
    /// <exception cref="ArgumentException">Leerer oder doppelter Name, zu wenige oder zu viele Stücke, unbekannte Form, voller Katalog.</exception>
    public Felskatalog Ablegen(string name, IReadOnlyList<Gruppenstueck> stuecke, out Felsgruppe neu)
    {
        if (Eigene.Count >= GruppenMax) throw new ArgumentException($"Der Katalog ist voll ({GruppenMax} eigene Gruppen) – erst eine löschen.");
        var n = Name(name);
        var nummer = 1 + Eigene.Select(g => Nummer(g.Kennung)).DefaultIfEmpty(0).Max();
        neu = new Felsgruppe(KennungVorsatz + nummer, n, Pruefen(stuecke).ToList(), Eigen: true);
        return new Felskatalog(Eigene.Append(neu).ToList());
    }

    /// <summary>Gibt einer eigenen Gruppe einen neuen Namen (Kennung und Stücke bleiben).</summary>
    /// <exception cref="ArgumentException">Keine eigene Gruppe mit dieser Kennung, leerer oder doppelter Name.</exception>
    public Felskatalog Umbenennen(string kennung, string name)
    {
        var alt = Eigen(kennung);
        var n = Name(name, ausser: kennung);
        return new Felskatalog(Eigene.Select(g => g == alt ? g with { Name = n } : g).ToList());
    }

    /// <summary>Löscht eine eigene Gruppe. Karten mit ihren Stücken bleiben, wie sie sind (sie speichern nur die Stücke).</summary>
    /// <exception cref="ArgumentException">Keine eigene Gruppe mit dieser Kennung (die festen lassen sich nicht löschen).</exception>
    public Felskatalog Loeschen(string kennung)
    {
        var alt = Eigen(kennung);
        return new Felskatalog(Eigene.Where(g => g != alt).ToList());
    }

    private Felsgruppe Eigen(string kennung) => Eigene.FirstOrDefault(g => g.Kennung == kennung)
        ?? throw new ArgumentException(Felsformen.Gruppe(kennung) is not null
            ? $"„{Felsformen.Gruppe(kennung)!.Name}“ ist eine feste Gruppe – sie lässt sich nicht ändern."
            : $"Eine eigene Felsgruppe „{kennung}“ gibt es nicht.", nameof(kennung));

    /// <summary>Der Name ohne Leerraum am Rand, höchstens <see cref="NameMax"/> Zeichen; nicht leer und nicht schon vergeben (auch nicht an eine feste Gruppe).</summary>
    private string Name(string name, string? ausser = null)
    {
        var n = string.Join(' ', (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (n.Length == 0) throw new ArgumentException("Die Gruppe braucht einen Namen.", nameof(name));
        if (n.Length > NameMax) n = n[..NameMax].TrimEnd();
        if (Alle.Any(g => g.Kennung != ausser && string.Equals(g.Name, n, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Eine Gruppe „{n}“ gibt es schon.", nameof(name));
        return n;
    }

    /// <summary>Die Nummer hinter „eigen_“ (0, wenn keine).</summary>
    private static int Nummer(string kennung) =>
        kennung.StartsWith(KennungVorsatz, StringComparison.Ordinal) && int.TryParse(kennung.AsSpan(KennungVorsatz.Length), System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;

    /// <summary>Die Stücke geprüft (Zahl, Formen) und in ihren Grenzen gerundet wie in der Karte.</summary>
    private static IEnumerable<Gruppenstueck> Pruefen(IReadOnlyList<Gruppenstueck> stuecke)
    {
        if (stuecke.Count < StueckeMin) throw new ArgumentException($"Eine Gruppe braucht mindestens {StueckeMin} Stücke.", nameof(stuecke));
        if (stuecke.Count > StueckeMax) throw new ArgumentException($"Eine Gruppe hat höchstens {StueckeMax} Stücke (gewählt: {stuecke.Count}).", nameof(stuecke));
        if (stuecke.FirstOrDefault(s => Felsformen.Finde(s.Form) is null) is { } fremd)
            throw new ArgumentException($"Eine Felsform „{fremd.Form}“ gibt es nicht.", nameof(stuecke));
        return stuecke.Select(Gerundet);
    }

    /// <summary>Ein Stück in seinen Grenzen, gerundet wie in der Karte (5 cm, 0,1°, 0,01).</summary>
    internal static Gruppenstueck Gerundet(Gruppenstueck s)
    {
        static double F(double v, double min, double max) => Math.Round(Math.Clamp(v, min, max), 2) + 0.0;
        return s with
        {
            X = Kartenraster.Punkt(s.X), Z = Kartenraster.Punkt(s.Z), Drehung = Felsraum.Winkel(s.Drehung),
            Massstab = F(s.Massstab, Felsformen.MassstabMin, Felsformen.MassstabMax),
            Einsinken = F(s.Einsinken, -Felsformen.EinsinkenMax, Felsformen.EinsinkenMax),
            Gestein = Felsformen.IstGestein(s.Gestein) ? s.Gestein : null,
            KippenX = Felsraum.Winkel(s.KippenX), KippenZ = Felsraum.Winkel(s.KippenZ),
            AchseX = F(s.AchseX, Felsformen.AchseMin, Felsformen.AchseMax), AchseY = F(s.AchseY, Felsformen.AchseMin, Felsformen.AchseMax),
            AchseZ = F(s.AchseZ, Felsformen.AchseMin, Felsformen.AchseMax),
            Versatz = Kartenraster.Punkt(Math.Clamp(s.Versatz, -Felsformen.VersatzMax, Felsformen.VersatzMax)),
        };
    }

    /// <summary>
    /// Eine eigene Gruppe für den Pinsel bereinigt: Stücke mit unbekannter Form fallen weg, die übrigen gerundet; null, wenn keins bleibt
    /// (<see cref="Felspinsel.Begrenzt"/>). Feste Gruppen bleiben, wie sie sind.
    /// </summary>
    internal static Felsgruppe? Bereinigt(Felsgruppe? g)
    {
        if (g is null || !g.Eigen) return g;
        var stuecke = g.Stuecke.Where(s => Felsformen.Finde(s.Form) is not null).Take(StueckeMax).Select(Gerundet).ToList();
        return stuecke.Count == 0 ? null : g with { Stuecke = stuecke };
    }

    // ── Datei ────────────────────────────────────────────────────────────────

    /// <summary>Die Datei des Katalogs (nur die eigenen Gruppen), im Zeilenformat des Kartenschreibers: gleicher Katalog, gleiche Bytes.</summary>
    public string AlsJson()
    {
        static JsonNode Z(double x) => JsonValue.Create(x);
        var gruppen = new JsonArray();
        foreach (var g in Eigene)
        {
            var stuecke = new JsonArray();
            foreach (var s in g.Stuecke)
            {
                var o = new JsonObject { ["form"] = s.Form, ["punkt"] = new JsonArray(Z(s.X), Z(s.Z)), ["drehung"] = s.Drehung, ["massstab"] = s.Massstab };
                if (s.Einsinken != 0) o["einsinken"] = s.Einsinken;
                if (s.Gestein is { } gestein) o["gestein"] = gestein;
                if (s.KippenX != 0 || s.KippenZ != 0) o["kippen"] = new JsonArray(Z(s.KippenX), Z(s.KippenZ));
                if (s.AchseX != 1 || s.AchseY != 1 || s.AchseZ != 1) o["achsen"] = new JsonArray(Z(s.AchseX), Z(s.AchseY), Z(s.AchseZ));
                if (s.Versatz != 0) o["versatz"] = s.Versatz;
                stuecke.Add(o);
            }
            gruppen.Add(new JsonObject { ["kennung"] = g.Kennung, ["name"] = g.Name, ["stuecke"] = stuecke });
        }
        return Kartenschreiber.Kompakt(new JsonObject { ["version"] = 1, ["gruppen"] = gruppen });
    }

    /// <summary>
    /// Liest einen Katalog. Fehlerhafte Einträge fallen weg und stehen in <paramref name="fehler"/> (eine kaputte Gruppe nimmt nicht den ganzen
    /// Katalog mit); ein Text, der gar kein JSON ist, ergibt den leeren Katalog mit einer Meldung.
    /// </summary>
    public static Felskatalog AusJson(string? text, List<string> fehler)
    {
        if (string.IsNullOrWhiteSpace(text)) return Leer;
        JsonNode? wurzel;
        try
        {
            wurzel = JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            fehler.Add($"Felsgruppen: Die Datei ist kein gültiges JSON ({ex.Message}).");
            return Leer;
        }
        var katalog = Leer;
        if (wurzel?["gruppen"] is not JsonArray gruppen) return katalog;
        foreach (var eintrag in gruppen)
        {
            try
            {
                var kennung = eintrag?["kennung"]?.GetValue<string>() ?? throw new ArgumentException("Eine Gruppe ohne Kennung.");
                if (Nummer(kennung) <= 0) throw new ArgumentException($"Die Kennung „{kennung}“ ist keine eigene (eigen_1, eigen_2 …).");
                if (katalog.IstEigen(kennung)) throw new ArgumentException($"Die Kennung „{kennung}“ steht doppelt.");
                var name = eintrag["name"]?.GetValue<string>() ?? kennung;
                var stuecke = (eintrag["stuecke"] as JsonArray ?? new JsonArray()).Select(Stueck).ToList();
                var geprueft = Pruefen(stuecke).ToList();
                var g = new Felsgruppe(kennung, katalog.Name(name), geprueft, Eigen: true);
                if (katalog.Eigene.Count >= GruppenMax) throw new ArgumentException($"Mehr als {GruppenMax} Gruppen.");
                katalog = new Felskatalog(katalog.Eigene.Append(g).ToList());
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or JsonException)
            {
                fehler.Add($"Felsgruppen: Eintrag übersprungen – {ex.Message}");
            }
        }
        return katalog;
    }

    private static Gruppenstueck Stueck(JsonNode? s)
    {
        if (s is not JsonObject o) throw new ArgumentException("Ein Stück ist kein Objekt.");
        double Zahl(string feld, double vorgabe) => o[feld] is JsonValue v ? v.GetValue<double>() : vorgabe;
        double[] Zahlen(string feld, int anzahl, double vorgabe) => o[feld] is JsonArray a && a.Count == anzahl
            ? a.Select(x => x is JsonValue v ? v.GetValue<double>() : throw new ArgumentException($"„{feld}“ enthält etwas anderes als Zahlen.")).ToArray()
            : Enumerable.Repeat(vorgabe, anzahl).ToArray(); // null oder ein Objekt im Feld: der Eintrag fällt weg, das Laden geht weiter
        var punkt = Zahlen("punkt", 2, 0);
        var kippen = Zahlen("kippen", 2, 0);
        var achsen = Zahlen("achsen", 3, 1);
        return new Gruppenstueck(o["form"]?.GetValue<string>() ?? "", punkt[0], punkt[1], Zahl("drehung", 0), Zahl("massstab", 1), Zahl("einsinken", 0),
            o["gestein"]?.GetValue<string>(), kippen[0], kippen[1], achsen[0], achsen[1], achsen[2], Zahl("versatz", 0));
    }
}
