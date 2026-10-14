using System.Text.Json;
using System.Text.Json.Serialization;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Das städtische Fron- und Sklavenstatut – ein Gesetz, das der Stadtrat ändern kann.
/// In der Datendatei heißen die Statuten kurz "A", "B" und "C".
/// </summary>
public enum Fronstatut
{
    /// <summary>A: Jede Knechtschaft verboten.</summary>
    FreieStadt,

    /// <summary>B: Orks und Oger als Fronarbeiter, nur für Grobarbeit im Umland (Standard).</summary>
    Bestienfron,

    /// <summary>C: Unfreie aller Völker in allen Gewerben.</summary>
    Leibeigenschaft,
}

/// <summary>Wo ein Betrieb liegt – bestimmt, welche Arbeitskräfte dort erlaubt sind.</summary>
public enum Arbeitsort
{
    Werkstatt,
    Umland,
}

public static class FronstatutTexte
{
    public static string Kuerzel(this Fronstatut statut) => statut switch
    {
        Fronstatut.FreieStadt => "A",
        Fronstatut.Bestienfron => "B",
        Fronstatut.Leibeigenschaft => "C",
        _ => "?",
    };

    public static string Name(this Fronstatut statut) => statut switch
    {
        Fronstatut.FreieStadt => "Freie Stadt",
        Fronstatut.Bestienfron => "Bestienfron",
        Fronstatut.Leibeigenschaft => "Ewige Leibeigenschaft",
        _ => statut.ToString(),
    };
}

// ─────────────────────────────────────────────────────────────────────────────
//  Die Datenklassen entsprechen 1:1 dem Aufbau von game/data/wirtschaft.json.
//  Der JSON-Leser ordnet "grundpreis" automatisch der Eigenschaft Grundpreis zu.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Allgemeine Stellschrauben (Abschnitt "allgemein").</summary>
public sealed class AllgemeineDaten
{
    public string Waehrung { get; init; } = "Gulden";

    /// <summary>Arbeitsminuten pro Spieljahr. 720 = 08:00 bis 20:00 Uhr.</summary>
    public int Arbeitsminuten { get; init; } = 720;

    public double Elastizitaet { get; init; } = 0.5;
    public double PreisMin { get; init; } = 0.5;
    public double PreisMax { get; init; } = 2.0;
    public double Umsatzsteuer { get; init; } = 0.05;
    public double UnterhaltGiesserei { get; init; } = 80;
    public double PachtMine { get; init; } = 400;
    public double Startvermoegen { get; init; } = 4000;
    public double SchwelleBuerger { get; init; } = 10000;
    public int PlaetzeGiesserei { get; init; } = 2;
    public int PlaetzeMine { get; init; } = 3;
}

/// <summary>Eine Ware (Abschnitt "waren").</summary>
public sealed class WareDaten
{
    public string Typ { get; init; } = "";

    /// <summary>Preis bei ausgeglichenem Markt (Angebot = Nachfrage).</summary>
    public double Grundpreis { get; init; }

    /// <summary>Herstellungszeit für einen Gesellen in Spielminuten.</summary>
    public double Zeit { get; init; }

    /// <summary>Zutaten pro Stück: Warenname → Menge.</summary>
    public Dictionary<string, int> Zutaten { get; init; } = new();

    /// <summary>Wie viele Stück die Stadt pro Jahr kauft.</summary>
    public double Nachfrage { get; init; }

    /// <summary>Anteil der Nachfrage, den Händler und Handwerker ohne Spieler decken (0 bis unter 1).</summary>
    public double NpcAnteil { get; init; }
}

/// <summary>Eine Art von Arbeitskraft (Abschnitt "arbeitskraefte").</summary>
public sealed class ArbeitskraftDaten
{
    /// <summary>Lohn oder Unterhalt pro Jahr.</summary>
    public double Kosten { get; init; }

    /// <summary>Arbeitstempo im Umland (1,0 = Geselle).</summary>
    public double Tempo { get; init; } = 1;

    /// <summary>Arbeitstempo in einer Werkstatt.</summary>
    public double TempoWerkstatt { get; init; } = 1;

    /// <summary>Wahrscheinlichkeit pro Jahr für Ausbruch oder Aufstand (0 bis 1).</summary>
    public double Risiko { get; init; }

    /// <summary>Kosten, wenn der Ausbruch oder Aufstand eintritt.</summary>
    public double Schaden { get; init; }

    /// <summary>Statut-Kürzel ("A"/"B"/"C") → erlaubte Arbeitsorte ("Werkstatt"/"Umland").</summary>
    public Dictionary<string, List<string>> Erlaubt { get; init; } = new();

    public bool IstErlaubt(Fronstatut statut, Arbeitsort ort) =>
        Erlaubt.TryGetValue(statut.Kuerzel(), out var orte) && orte.Contains(ort.ToString());

    public double TempoAm(Arbeitsort ort) => ort == Arbeitsort.Werkstatt ? TempoWerkstatt : Tempo;
}

/// <summary>Wird geworfen, wenn die Datendatei fehlerhaft ist. Enthält alle gefundenen Fehler.</summary>
public sealed class WirtschaftsdatenFehler : Exception
{
    public IReadOnlyList<string> Fehler { get; }

    public WirtschaftsdatenFehler(IReadOnlyList<string> fehler)
        : base("Die Wirtschaftsdaten sind fehlerhaft:\n  • " + string.Join("\n  • ", fehler))
    {
        Fehler = fehler;
    }
}

/// <summary>
/// Alle Zahlen des Wirtschaftsmodells, geladen aus game/data/wirtschaft.json.
/// Die Datei entsteht mit dem Balancing-Rechner (Export „Werte kopieren").
/// </summary>
public sealed class Wirtschaftsdaten
{
    public int Version { get; init; } = 1;
    public AllgemeineDaten Allgemein { get; init; } = new();
    public Dictionary<string, WareDaten> Waren { get; init; } = new();
    public Dictionary<string, ArbeitskraftDaten> Arbeitskraefte { get; init; } = new();

    /// <summary>Karren, Fahrzeiten, Eskorten und Banden (ab Datenversion 2).</summary>
    public LogistikDaten Logistik { get; init; } = new();

    /// <summary>Die Stände von unten nach oben (ab Datenversion 3). Der erste ist der Startstand.</summary>
    public List<StandDaten> Staende { get; init; } = new()
    {
        new() { Name = "Gemeiner" },
        new() { Name = "Bürger", Schwelle = 10000, Gebuehr = 1000 },
    };

    /// <summary>Wann Schulden zum Bankrott führen (ab Datenversion 3).</summary>
    public SchuldenDaten Schulden { get; init; } = new();

    /// <summary>Ämter, Ratsherren und Gunst-Regeln (ab Datenversion 5). Leer = ohne Stadtrat.</summary>
    public Politik.PolitikDaten Politik { get; init; } = new();

    /// <summary>Völker, Leben, Heirat, Kinder und Erbe (ab Datenversion 6). Leer = ohne Dynastien.</summary>
    public Dynastie.DynastieDaten Dynastie { get; init; } = new();

    /// <summary>Freie Werkstätten und Gruben, die man erwerben kann (ab Datenversion 4).</summary>
    public List<LiegenschaftDaten> Liegenschaften { get; init; } = new();

    /// <summary>Stufe eines Standes nach Namen (−1, wenn es ihn nicht gibt).</summary>
    public int StandStufe(string name) => Staende.FindIndex(st => st.Name == name);

    /// <summary>Jährliche Fixkosten einer Betriebsart (Unterhalt der Werkstatt bzw. Pacht der Grube).</summary>
    public double Fixkosten(Betriebsart art) => art == Betriebsart.Mine ? Allgemein.PachtMine : Allgemein.UnterhaltGiesserei;

    /// <summary>Arbeitsplätze einer Betriebsart.</summary>
    public int Plaetze(Betriebsart art) => art == Betriebsart.Mine ? Allgemein.PlaetzeMine : Allgemein.PlaetzeGiesserei;

    private static readonly JsonSerializerOptions Optionen = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }, // "art": "Mine" → Betriebsart.Mine
    };

    /// <summary>Liest die Daten aus einem JSON-Text und prüft sie.</summary>
    /// <exception cref="WirtschaftsdatenFehler">Wenn die Datei nicht lesbar ist oder Fehler enthält.</exception>
    public static Wirtschaftsdaten AusJson(string json)
    {
        Wirtschaftsdaten? daten;
        try
        {
            daten = JsonSerializer.Deserialize<Wirtschaftsdaten>(json, Optionen);
        }
        catch (JsonException ex)
        {
            throw new WirtschaftsdatenFehler(new[] { $"Kein gültiges JSON (Zeile {ex.LineNumber + 1}): {ex.Message}" });
        }

        if (daten is null)
            throw new WirtschaftsdatenFehler(new[] { "Die Datei ist leer." });

        var fehler = daten.Pruefen();
        if (fehler.Count > 0)
            throw new WirtschaftsdatenFehler(fehler);

        return daten;
    }

    /// <summary>Liest die Daten aus einer Datei (für Tests und Werkzeuge).</summary>
    public static Wirtschaftsdaten AusDatei(string pfad) => AusJson(File.ReadAllText(pfad));

    /// <summary>Prüft die Daten auf Widersprüche. Leere Liste = alles in Ordnung.</summary>
    public List<string> Pruefen()
    {
        var f = new List<string>();
        var a = Allgemein;

        if (a.Arbeitsminuten is <= 0 or > 1440) f.Add("allgemein.arbeitsminuten muss zwischen 1 und 1440 liegen.");
        if (a.Elastizitaet < 0) f.Add("allgemein.elastizitaet darf nicht negativ sein.");
        if (a.PreisMin <= 0 || a.PreisMax < a.PreisMin) f.Add("allgemein.preisMin muss > 0 und ≤ preisMax sein.");
        if (a.Umsatzsteuer is < 0 or >= 1) f.Add("allgemein.umsatzsteuer muss zwischen 0 und 1 liegen.");
        if (a.PlaetzeGiesserei < 1 || a.PlaetzeMine < 1) f.Add("allgemein.plaetze… müssen mindestens 1 sein.");
        if (Waren.Count == 0) f.Add("Es sind keine Waren definiert.");

        foreach (var (name, w) in Waren)
        {
            if (w.Grundpreis <= 0) f.Add($"Ware {name}: grundpreis muss größer als 0 sein.");
            if (w.Zeit <= 0) f.Add($"Ware {name}: zeit muss größer als 0 sein.");
            if (w.Nachfrage <= 0) f.Add($"Ware {name}: nachfrage muss größer als 0 sein.");
            if (w.NpcAnteil is < 0 or >= 1) f.Add($"Ware {name}: npcAnteil muss zwischen 0 und unter 1 liegen.");
            foreach (var (zutat, menge) in w.Zutaten)
            {
                if (!Waren.ContainsKey(zutat)) f.Add($"Ware {name}: Zutat „{zutat}“ gibt es nicht.");
                if (menge <= 0) f.Add($"Ware {name}: Menge von {zutat} muss größer als 0 sein.");
            }
        }

        var gueltigeStatuten = new[] { "A", "B", "C" };
        var gueltigeOrte = Enum.GetNames<Arbeitsort>();
        foreach (var (name, k) in Arbeitskraefte)
        {
            if (k.Kosten < 0) f.Add($"Arbeitskraft {name}: kosten dürfen nicht negativ sein.");
            if (k.Tempo < 0 || k.TempoWerkstatt < 0) f.Add($"Arbeitskraft {name}: tempo darf nicht negativ sein.");
            if (k.Risiko is < 0 or > 1) f.Add($"Arbeitskraft {name}: risiko muss zwischen 0 und 1 liegen.");
            foreach (var (statut, orte) in k.Erlaubt)
            {
                if (!gueltigeStatuten.Contains(statut)) f.Add($"Arbeitskraft {name}: unbekanntes Statut „{statut}“ (erlaubt: A, B, C).");
                foreach (var ort in orte)
                    if (!gueltigeOrte.Contains(ort)) f.Add($"Arbeitskraft {name}: unbekannter Ort „{ort}“ (erlaubt: {string.Join(", ", gueltigeOrte)}).");
            }
        }

        Logistik.Pruefen(f);

        if (Staende.Count == 0) f.Add("Es sind keine Stände definiert.");
        for (var i = 0; i < Staende.Count; i++)
        {
            var st = Staende[i];
            if (string.IsNullOrWhiteSpace(st.Name)) f.Add($"staende[{i}]: name fehlt.");
            if (st.Schwelle < 0 || st.Gebuehr < 0) f.Add($"Stand {st.Name}: schwelle und gebuehr dürfen nicht negativ sein.");
            if (st.Gebuehr > st.Schwelle) f.Add($"Stand {st.Name}: gebuehr darf nicht höher als die schwelle sein.");
            if (i > 0 && st.Schwelle <= Staende[i - 1].Schwelle) f.Add($"Stand {st.Name}: schwelle muss höher sein als beim Stand darunter.");
        }
        if (Staende.Count > 0 && Staende[0].Schwelle != 0) f.Add($"Der erste Stand ({Staende[0].Name}) ist der Startstand und braucht schwelle 0.");
        var buerger = Staende.FirstOrDefault(st => st.Name == "Bürger");
        if (buerger is not null && buerger.Schwelle != a.SchwelleBuerger)
            f.Add($"Stand Bürger: schwelle ({buerger.Schwelle}) passt nicht zu allgemein.schwelleBuerger ({a.SchwelleBuerger}).");
        if (Schulden.JahreBisBankrott < 1) f.Add("schulden.jahreBisBankrott muss mindestens 1 sein.");

        foreach (var l in Liegenschaften)
        {
            if (string.IsNullOrWhiteSpace(l.Name)) f.Add("liegenschaften: name fehlt.");
            if (l.Kaufpreis < 0) f.Add($"Liegenschaft {l.Name}: kaufpreis darf nicht negativ sein.");
            if (StandStufe(l.MindestStand) < 0) f.Add($"Liegenschaft {l.Name}: mindestStand „{l.MindestStand}“ gibt es nicht.");
            if (!Enum.IsDefined(l.Art)) f.Add($"Liegenschaft {l.Name}: unbekannte art.");
        }
        foreach (var doppelt in Liegenschaften.GroupBy(l => l.Name).Where(g => g.Count() > 1))
            f.Add($"Liegenschaft „{doppelt.Key}“ ist doppelt eingetragen.");

        Politik.Pruefen(f, StandStufe);
        Dynastie.Pruefen(f, StandStufe);
        return f;
    }

    /// <summary>Zutatenkosten eines Stücks zu Grundpreisen.</summary>
    public double ZutatenZumGrundpreis(string ware) =>
        Waren[ware].Zutaten.Sum(z => Waren[z.Key].Grundpreis * z.Value);
}
