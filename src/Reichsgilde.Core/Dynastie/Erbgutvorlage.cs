using System.Text.Json;
using System.Text.Json.Serialization;

namespace Reichsgilde.Core.Dynastie;

/// <summary>Wofür ein Gen steht – nur zum Ordnen (Editor, Darstellung); die Regeln sind für alle gleich.</summary>
public enum Genart
{
    /// <summary>Körperbau: Größe, Gewicht, Proportionen.</summary>
    Makro,
    /// <summary>Gesichtszüge: Nase, Kiefer, Kinn, Augen, Ohren …</summary>
    Merkmal,
    /// <summary>Koordinate auf einer Farbtafel der Darstellung (Haut, Haar, Augen).</summary>
    Farbe,
    /// <summary>Neigung oder Auswahl: Frisur, Bart, Glatze, Ergrauen.</summary>
    Zubehoer,
}

/// <summary>Wie ein Gen ohne Eltern gewürfelt wird.</summary>
public enum Genverteilung
{
    /// <summary>Glockenkurve um das Mittel des Volkes, abgeschnitten bei ±2 Streuungen.</summary>
    Normal,
    /// <summary>Jeder Wert von min bis max gleich oft – für Auswahl-Gene wie die Frisur.</summary>
    Gleich,
}

/// <summary>Grenzen eines Gens für ein Volk: kein Allel liegt außerhalb von <paramref name="Min"/> bis <paramref name="Max"/>.</summary>
/// <param name="Mittel">Mitte der Glockenkurve beim Würfeln.</param>
/// <param name="Streuung">Breite der Glockenkurve (eine Standardabweichung); 0 = immer das Mittel.</param>
public readonly record struct GenGrenzen(int Min, int Mittel, int Max, int Streuung)
{
    /// <summary>Den Wert in die Grenzen des Volkes holen.</summary>
    public int Begrenzen(int wert) => Math.Clamp(wert, Min, Max);
}

/// <summary>Ein Punkt der Alterskurve: Mit <paramref name="Jahre"/> Menschenjahren verschiebt sich der sichtbare Wert um <paramref name="Verschiebung"/>.</summary>
public readonly record struct Alterspunkt(int Jahre, int Verschiebung);

/// <summary>
/// Eine Korrelationsregel: Beim Würfeln (Personen ohne Eltern) verschiebt der sichtbare Wert von <paramref name="Von"/> beide Allele von
/// <paramref name="Zu"/> – um (Wert − Mittel des Volkes) × <paramref name="Staerke"/> ÷ 100. Bei Kindern wirkt sie nicht noch einmal: Sie
/// erben die Allele, in denen die Regel schon steckt.
/// </summary>
public sealed record GenKorrelation(string Von, string Zu, int Staerke, string Grund);

/// <summary>Ein Gen der Vorlage: Kennung, Regeln fürs Vererben und die Grenzen je Volk.</summary>
public sealed class GenVorlage
{
    private readonly GenGrenzen[] _grenzen;

    /// <summary>Kennung im Spielstand und für die Darstellung („nase_laenge“) – ändert sich nie, sonst gilt das Gen als neu.</summary>
    public string Kennung { get; }
    public string Name { get; }
    public string Beschreibung { get; }
    public Genart Art { get; }

    /// <summary>Chance in Prozent (0–100), dass von zwei verschiedenen Allelen das höhere sichtbar wird. 50 = Zufall, 25 = das niedrigere setzt sich meist durch.</summary>
    public int Dominanz { get; }

    /// <summary>Um höchstens so viel weicht ein vererbtes Allel vom Allel des Elternteils ab (gleich verteilt, ± Mutation).</summary>
    public int Mutation { get; }

    public Genverteilung Verteilung { get; }

    /// <summary>
    /// Bei Frauen zeigt sich statt dieses Gens das Gen mit dieser Kennung (null = bei beiden Geschlechtern dieses). So hat der Bart der Frauen
    /// eigene Werte je Volk (Zwerginnen meist leicht, selten voll), ohne dass die Allele vom Geschlecht abhängen: Beide Gene trägt und vererbt
    /// jede Person, sichtbar wird je Geschlecht eines (<see cref="Erbgut.SichtbarerWert(string, Geschlecht)"/>).
    /// </summary>
    public string? BeiFrauen { get; }

    /// <summary>Die Stelle in der Vorlage (0, 1, 2 …) – in dieser Reihenfolge stehen die Allele im <see cref="Erbgut"/>.</summary>
    public int Index { get; }

    /// <summary>Wie sich der sichtbare Wert mit dem Alter verschiebt (für die Darstellung; leer = gar nicht). Nach Jahren aufsteigend.</summary>
    public IReadOnlyList<Alterspunkt> Alterskurve { get; }

    /// <summary>Fest aus der Kennung: macht den Würfel jedes Gens unabhängig von der Reihenfolge der Gene.</summary>
    internal int Schluessel { get; }

    internal GenVorlage(int index, string kennung, string name, string beschreibung, Genart art, int dominanz, int mutation,
        Genverteilung verteilung, GenGrenzen[] grenzen, IReadOnlyList<Alterspunkt> alterskurve, string? beiFrauen)
    {
        BeiFrauen = beiFrauen;
        Index = index;
        Kennung = kennung;
        Name = name;
        Beschreibung = beschreibung;
        Art = art;
        Dominanz = dominanz;
        Mutation = mutation;
        Verteilung = verteilung;
        _grenzen = grenzen;
        Alterskurve = alterskurve;
        Schluessel = Erbgutvorlage.Streuwert(kennung);
    }

    /// <summary>Die Grenzen dieses Gens für ein Volk.</summary>
    public GenGrenzen Grenzen(Volk volk) => _grenzen[(int)volk];

    /// <summary>
    /// Um wie viel sich der sichtbare Wert mit so vielen Menschenjahren verschiebt: zwischen zwei Punkten der Kurve gerade verbunden,
    /// vor dem ersten und nach dem letzten wie dort. Nur ganze Zahlen.
    /// </summary>
    public int Altersverschiebung(int menschenjahre)
    {
        var k = Alterskurve;
        if (k.Count == 0) return 0;
        if (menschenjahre <= k[0].Jahre) return k[0].Verschiebung;
        for (var i = 1; i < k.Count; i++)
        {
            if (menschenjahre > k[i].Jahre) continue;
            var (a, b) = (k[i - 1], k[i]);
            return a.Verschiebung + Erbgut.GerundetGeteilt((b.Verschiebung - a.Verschiebung) * (menschenjahre - a.Jahre), b.Jahre - a.Jahre);
        }
        return k[^1].Verschiebung;
    }
}

/// <summary>Die Gen-Vorlage passt nicht – mit allen gefundenen Fehlern.</summary>
public sealed class ErbgutvorlageFehler : Exception
{
    public IReadOnlyList<string> Fehler { get; }

    public ErbgutvorlageFehler(IReadOnlyList<string> fehler)
        : base("Die Gen-Vorlage (erbgut.json) ist fehlerhaft:\n  • " + string.Join("\n  • ", fehler)) => Fehler = fehler;
}

/// <summary>
/// Die Gen-Vorlage: welche Gene es gibt und wie sie sich je Volk verteilen und vererben – nach dem Vorbild der „gene templates“ von CK3.
///
/// Die Werte stehen in <c>game/daten/erbgut.json</c>. Die Datei ist fest in den Kern eingebaut (<see cref="Standard"/>, eingebettete Ressource):
/// So braucht weder das Spiel noch ein Test einen Pfad, und jeder Rechner einer Partie hat dieselbe Vorlage. Sie steht nicht im Spielstand –
/// das Erbgut wirkt auf nichts in der Simulation, und die Allele jeder Person stehen dort ohnehin.
/// </summary>
public sealed class Erbgutvorlage
{
    /// <summary>Die Ressource im Kern (siehe <c>Reichsgilde.Core.csproj</c>).</summary>
    private const string Ressource = "Reichsgilde.Core.erbgut.json";

    private static readonly Lazy<Erbgutvorlage> StandardVorlage = new(() =>
    {
        using var strom = typeof(Erbgutvorlage).Assembly.GetManifestResourceStream(Ressource)
                          ?? throw new InvalidOperationException($"Die Gen-Vorlage {Ressource} fehlt im Kern.");
        using var leser = new StreamReader(strom);
        return AusJson(leser.ReadToEnd());
    });

    // Nur zum Nachschlagen, nie aufgezählt – die Reihenfolge kommt aus der Liste.
    private readonly Dictionary<string, GenVorlage> _nachKennung;

    /// <summary>Die Vorlage aus <c>game/daten/erbgut.json</c>.</summary>
    public static Erbgutvorlage Standard => StandardVorlage.Value;

    /// <summary>Version der Vorlage (<c>version</c>) – steht beim Speichern im Spielstand, damit eine spätere Version alte Werte umrechnen kann.</summary>
    public int Version { get; }

    /// <summary>Alle Gene in der Reihenfolge der Datei.</summary>
    public IReadOnlyList<GenVorlage> Gene { get; }

    /// <summary>Die Korrelationsregeln in der Reihenfolge der Datei (sie wirken nacheinander).</summary>
    public IReadOnlyList<GenKorrelation> Korrelationen { get; }

    private Erbgutvorlage(int version, IReadOnlyList<GenVorlage> gene, IReadOnlyList<GenKorrelation> korrelationen)
    {
        Version = version;
        Gene = gene;
        Korrelationen = korrelationen;
        _nachKennung = gene.ToDictionary(g => g.Kennung, StringComparer.Ordinal);
    }

    /// <summary>Das Gen mit dieser Kennung – oder null.</summary>
    public GenVorlage? Gen(string kennung) => _nachKennung.GetValueOrDefault(kennung);

    /// <summary>Die Stelle des Gens in der Vorlage – oder −1.</summary>
    public int Index(string kennung) => _nachKennung.TryGetValue(kennung, out var g) ? g.Index : -1;

    /// <summary>Die Gene einer Art in der Reihenfolge der Datei.</summary>
    public IEnumerable<GenVorlage> GeneDerArt(Genart art) => Gene.Where(g => g.Art == art);

    /// <summary>Liest eine Vorlage aus JSON und prüft sie.</summary>
    /// <exception cref="ErbgutvorlageFehler">Mit allen Fehlern auf einmal.</exception>
    public static Erbgutvorlage AusJson(string json)
    {
        VorlageDatei? datei;
        try
        {
            datei = JsonSerializer.Deserialize<VorlageDatei>(json, Optionen);
        }
        catch (JsonException ex)
        {
            // Auch ein Feld, das es nicht gibt, landet hier (UnmappedMemberHandling) – der Name steht in der Meldung von .NET.
            throw new ErbgutvorlageFehler(new[] { $"Kein gültiges JSON oder ein unbekanntes Feld (Tippfehler?) in Zeile {ex.LineNumber + 1}: {ex.Message}" });
        }
        if (datei is null) throw new ErbgutvorlageFehler(new[] { "Die Datei ist leer." });

        var fehler = Pruefen(datei);
        if (fehler.Count > 0) throw new ErbgutvorlageFehler(fehler);

        var gene = datei.Gene.Select((g, i) => new GenVorlage(i, g.Kennung, g.Name, g.Beschreibung ?? "", g.Art, g.Dominanz, g.Mutation,
            g.Verteilung, Enum.GetValues<Volk>().Select(v => g.Voelker![v].AlsGrenzen()).ToArray(),
            (g.Alter ?? new()).Select(p => new Alterspunkt(p.Jahre, p.Verschiebung)).ToList(), g.BeiFrauen)).ToList();
        var korrelationen = (datei.Korrelationen ?? new()).Select(k => new GenKorrelation(k.Von, k.Zu, k.Staerke, k.Grund ?? "")).ToList();
        return new Erbgutvorlage(datei.Version, gene, korrelationen);
    }

    /// <summary>Liest eine Vorlage aus einer Datei (für Tests und Werkzeuge).</summary>
    public static Erbgutvorlage AusDatei(string pfad) => AusJson(File.ReadAllText(pfad));

    private static List<string> Pruefen(VorlageDatei d)
    {
        var fehler = new List<string>();
        if (d.Version < 1) fehler.Add("„version“ fehlt oder ist kleiner als 1.");
        if (d.Gene is not { Count: > 0 }) { fehler.Add("Es gibt kein Gen („gene“ ist leer)."); return fehler; }
        if (d.Gene.Any(g => g is null) || (d.Korrelationen?.Any(k => k is null) ?? false))
        {
            fehler.Add("Ein Eintrag in „gene“ oder „korrelationen“ ist leer (null).");
            return fehler;
        }

        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in d.Gene)
        {
            var wer = string.IsNullOrEmpty(g.Kennung) ? "Ein Gen ohne Kennung" : $"Gen „{g.Kennung}“";
            if (string.IsNullOrEmpty(g.Kennung) || !g.Kennung.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
                fehler.Add($"{wer}: Die Kennung darf nur aus a–z, 0–9 und _ bestehen.");
            else if (!gesehen.Add(g.Kennung))
                fehler.Add($"{wer} steht zweimal in der Vorlage.");
            if (string.IsNullOrWhiteSpace(g.Name)) fehler.Add($"{wer}: Der Name fehlt.");
            if (g.Dominanz is < 0 or > 100) fehler.Add($"{wer}: Die Dominanz muss zwischen 0 und 100 liegen (war {g.Dominanz}).");
            if (g.Mutation is < 0 or > 32) fehler.Add($"{wer}: Die Mutation muss zwischen 0 und 32 liegen (war {g.Mutation}).");
            foreach (var volk in Enum.GetValues<Volk>())
            {
                if (g.Voelker is null || !g.Voelker.TryGetValue(volk, out var v))
                {
                    fehler.Add($"{wer}: Die Grenzen für {volk.Mehrzahl()} fehlen.");
                    continue;
                }
                if (v.Min < 0 || v.Max > 255 || v.Min > v.Mittel || v.Mittel > v.Max)
                    fehler.Add($"{wer}, {volk.Mehrzahl()}: Es muss 0 ≤ min ≤ mittel ≤ max ≤ 255 gelten (min {v.Min}, mittel {v.Mittel}, max {v.Max}).");
                if (v.Streuung is < 0 or > 128) fehler.Add($"{wer}, {volk.Mehrzahl()}: Die Streuung muss zwischen 0 und 128 liegen (war {v.Streuung}).");
            }
            var vorher = int.MinValue;
            foreach (var p in g.Alter ?? new())
            {
                if (p.Jahre <= vorher || p.Jahre is < 0 or > 300)
                    fehler.Add($"{wer}: Die Alterskurve braucht aufsteigende Jahre zwischen 0 und 300 (bei {p.Jahre}).");
                if (p.Verschiebung is < -255 or > 255) fehler.Add($"{wer}: Eine Verschiebung im Alter muss zwischen −255 und 255 liegen (war {p.Verschiebung}).");
                vorher = p.Jahre;
            }
        }
        foreach (var g in d.Gene.Where(g => g.BeiFrauen is not null))
        {
            var wer = $"Gen „{g.Kennung}“";
            if (d.Gene.FirstOrDefault(x => x.Kennung == g.BeiFrauen) is not { } frau)
                fehler.Add($"{wer}: Das Gen „{g.BeiFrauen}“ für Frauen gibt es nicht.");
            else if (frau == g || frau.BeiFrauen is not null)
                fehler.Add($"{wer}: Das Gen für Frauen („{g.BeiFrauen}“) muss ein anderes sein und darf selbst kein „beiFrauen“ haben.");
        }
        foreach (var k in d.Korrelationen ?? new())
        {
            var wer = $"Korrelation {k.Von} → {k.Zu}";
            if (!gesehen.Contains(k.Von)) fehler.Add($"{wer}: Ein Gen „{k.Von}“ gibt es nicht.");
            if (!gesehen.Contains(k.Zu)) fehler.Add($"{wer}: Ein Gen „{k.Zu}“ gibt es nicht.");
            if (k.Von == k.Zu) fehler.Add($"{wer}: Ein Gen kann nicht mit sich selbst zusammenhängen.");
            if (k.Staerke is < -100 or > 100) fehler.Add($"{wer}: Die Stärke muss zwischen −100 und 100 liegen (war {k.Staerke}).");
        }
        return fehler;
    }

    /// <summary>
    /// Eine feste Zahl aus einem Text (FNV-1a, 32 Bit) – auf jedem Rechner und in jedem Programmlauf gleich, anders als
    /// <c>string.GetHashCode</c>, das .NET bei jedem Start neu würfelt.
    /// </summary>
    internal static int Streuwert(string text)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var c in text)
            {
                h ^= c;
                h *= 16777619u;
            }
            return (int)h;
        }
    }

    // ── Dateiformat ─────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions Optionen = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Ein Feld, das es nicht gibt („domianz“), ist fast immer ein Tippfehler – lieber laut ablehnen als still mit dem Standardwert weiterrechnen.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed class VorlageDatei
    {
        public int Version { get; set; }
        public string? Beschreibung { get; set; }
        public List<GenDatei> Gene { get; set; } = new();
        public List<KorrelationDatei>? Korrelationen { get; set; }
    }

    private sealed class GenDatei
    {
        public string Kennung { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Beschreibung { get; set; }
        public Genart Art { get; set; }
        public int Dominanz { get; set; } = 50;
        public int Mutation { get; set; } = 3;
        public Genverteilung Verteilung { get; set; } = Genverteilung.Normal;
        public string? BeiFrauen { get; set; }
        public List<AlterspunktDatei>? Alter { get; set; }
        public Dictionary<Volk, GrenzenDatei>? Voelker { get; set; }
    }

    private sealed class GrenzenDatei
    {
        public int Min { get; set; }
        public int Mittel { get; set; }
        public int Max { get; set; }
        public int Streuung { get; set; }
        public GenGrenzen AlsGrenzen() => new(Min, Mittel, Max, Streuung);
    }

    private sealed class AlterspunktDatei
    {
        public int Jahre { get; set; }
        public int Verschiebung { get; set; }
    }

    private sealed class KorrelationDatei
    {
        public string Von { get; set; } = "";
        public string Zu { get; set; } = "";
        public int Staerke { get; set; }
        public string? Grund { get; set; }
    }
}
