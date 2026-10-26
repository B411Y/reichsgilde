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
//  Die Datenklassen entsprechen 1:1 dem Aufbau von game/daten/wirtschaft.json.
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
/// Alle Zahlen des Wirtschaftsmodells, geladen aus game/daten/wirtschaft.json.
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

    /// <summary>Das Stadtgericht (ab Datenversion 7). Fehlt der Abschnitt, gibt es kein Gericht.</summary>
    public Justiz.JustizDaten? Justiz { get; init; }

    /// <summary>Die Unterwelt (ab Datenversion 8). Fehlt der Abschnitt, gibt es kein Hinterzimmer.</summary>
    public Gauner.GaunerDaten? Gauner { get; init; }

    /// <summary>Krankheiten, Pest und Heilmittel (ab Datenversion 11). Fehlt der Abschnitt, wird niemand krank.</summary>
    public Reichsgilde.Core.Gesundheit.GesundheitDaten? Gesundheit { get; init; }

    /// <summary>Kosten persönlicher Handlungen in Aktionspunkten (ab Datenversion 10). Fehlt der Abschnitt, ist alles frei.</summary>
    public Partie.AktionspunkteDaten? Aktionspunkte { get; init; }

    /// <summary>Freie Werkstätten und Gruben, die man erwerben kann (ab Datenversion 4).</summary>
    public List<LiegenschaftDaten> Liegenschaften { get; init; } = new();

    /// <summary>Erlös beim Verkauf eines Betriebs (ab Datenversion 33). Fehlt der Abschnitt, gelten 75 % und −20 % auf die Ware.</summary>
    public VerkaufDaten Verkauf { get; init; } = new();

    /// <summary>Stufe eines Standes nach Namen (−1, wenn es ihn nicht gibt).</summary>
    public int StandStufe(string name) => Staende.FindIndex(st => st.Name == name);

    /// <summary>Betriebsarten (ab Datenversion 12). Fehlt der Abschnitt, gibt es nur Gießerei und Grube wie früher.</summary>
    public Dictionary<Betriebsart, BetriebsartDaten> Betriebsarten { get; init; } = new();

    /// <summary>Berufe (ab Datenversion 12). Fehlt der Abschnitt, ist jeder Schmied.</summary>
    public Dictionary<string, BerufDaten> Berufe { get; init; } = new();

    /// <summary>Sold, Patrouille, Wachturm, Razzia, Kopfgeld und Geleitschutz des Gardisten (ab Datenversion 19). Fehlt der Abschnitt, gibt es keine Wache.</summary>
    public Ordnung.WacheDaten? Wache { get; init; }

    /// <summary>Kasse, Wechsel, Kredite und Wucher des Geldleihers (ab Datenversion 20). Fehlt der Abschnitt, gibt es keine Geldleiher.</summary>
    public Bank.GeldleiherDaten? Geldleiher { get; init; }

    /// <summary>Düfte und Dolche des Parfumeurs (ab Datenversion 21). Fehlt der Abschnitt, wirken Düfte nicht.</summary>
    public Duft.ParfumDaten? Parfum { get; init; }

    /// <summary>Wohnhäuser je Haus: Stufen, Ausbau, Unterhalt, Ansehen, Pflicht für den Stand (ab Datenversion 31). Fehlt der Abschnitt, gibt es keine Wohnhäuser.</summary>
    public Wohnen.WohnenDaten? Wohnen { get; init; }

    /// <summary>Bauen in der Stadt: Raster, Größen, Bauzeiten, Preise für Leerstand (ab Datenversion 34). Fehlt der Abschnitt, wird nicht gebaut.</summary>
    public Bauen.BauDaten? Bauen { get; init; }

    /// <summary>
    /// Belag der Wege: Tempo der Karren, Austritt, Verfall, Bauaufträge, Pflaster des Stadtrats (ab Datenversion 52; Kartenwerkstatt, Schritt 5d).
    /// Fehlt der Abschnitt, haben Wege im Spiel keinen Belag.
    /// </summary>
    public Bauen.WegebauDaten? Wegebau { get; init; }

    /// <summary>Handel von Hand per Karrenauftrag (ab Datenversion 24). Fehlt der Abschnitt, handeln nur die Verwalter.</summary>
    public HandelDaten? Handel { get; init; }

    /// <summary>
    /// Schiffshandel mit fernen Städten am Fluss (ab Datenversion 53, PR #199): Schiffe, ferne Städte, Zoll, Flusspiraten.
    /// Fehlt der Abschnitt (oder hat die Karte keinen Hafen), gibt es keinen Schiffshandel.
    /// </summary>
    public SchiffshandelDaten? Schiffshandel { get; init; }

    /// <summary>Konfessionen, Glaubensnähe im Rat, Bekehrung und Stadtglaube (ab Datenversion 22). Fehlt der Abschnitt, spielt der Glaube keine Rolle.</summary>
    public Glaube.GlaubeDaten? Glaube { get; init; }

    /// <summary>Taschendiebe, Einbruch, Auskundschaften, Schutzgeld und Entführung (ab Datenversion 18). Fehlt der Abschnitt, gibt es keine Diebe.</summary>
    public Gauner.DiebDaten? Dieb { get; init; }

    /// <summary>Messe, Ablass, Beichte und Schriften des Predigers (ab Datenversion 17). Fehlt der Abschnitt, gibt es keine Kirchen.</summary>
    public Reichsgilde.Core.Kirche.KirchenDaten? Kirche { get; init; }

    /// <summary>Gäste, Ausschank, Barde, Würfeltisch und Stammtisch des Wirts (ab Datenversion 16). Fehlt der Abschnitt, kehrt niemand ein.</summary>
    public GastwirtschaftDaten? Gastwirtschaft { get; init; }

    /// <summary>Einwohner der Stadt mit Tagesablauf – Bürger-KI (ab Datenversion 25). Fehlt der Abschnitt, gibt es keine Einwohner.</summary>
    public Reichsgilde.Core.Buerger.BuergerDaten? Buerger { get; init; }

    /// <summary>Werte der KI-Häuser (Spiel-KI), z. B. wann sie Einrichtung kaufen. Fehlt der Abschnitt, kauft die KI keine Einrichtung.</summary>
    public Reichsgilde.Core.Ki.KiDaten? Ki { get; init; }

    /// <summary>
    /// Einwohner und Nachfrage der Stadt (ab Datenversion 48, PR #177): Die Nachfrage richtet sich nach den Einwohnern, und die wachsen
    /// mit den Häusern. Fehlt der Abschnitt, ist die Nachfrage fest wie in „waren“.
    /// </summary>
    public StadtDaten? Stadt { get; init; }

    /// <summary>
    /// Werkstatt wie in Gilde 1: Lohnstufen, Prämie, Laune, Unfälle, Beförderung und Einrichtung je Beruf (ab Datenversion 36).
    /// Fehlt der Abschnitt, arbeiten alle mit neutraler Laune, ohne Unfälle und ohne Einrichtung.
    /// </summary>
    public WerkstattDaten? Werkstatt { get; init; }

    /// <summary>Meisterprüfungen und Gebäudestufen (ab Datenversion 13). Fehlt der Abschnitt, bleibt jedes Haus auf Grad 1.</summary>
    public MeistergradDaten? Meistergrade { get; init; }

    private readonly Dictionary<Betriebsart, BetriebsartDaten> _alteArten = new();

    /// <summary>
    /// Was eine Betriebsart ist und kann. Ältere Daten (vor Version 12) kennen nur Gießerei und Grube –
    /// für sie wird der Eintrag aus „allgemein" gebaut, damit alte Spielstände unverändert weiterlaufen.
    /// </summary>
    public BetriebsartDaten Art(Betriebsart art)
    {
        if (Betriebsarten.TryGetValue(art, out var d)) return d;
        if (_alteArten.TryGetValue(art, out d)) return d;
        d = art switch
        {
            Betriebsart.Giesserei => new BetriebsartDaten
            {
                Name = "Gießerei", Beruf = "Schmied", Ort = Arbeitsort.Werkstatt, Plaetze = Allgemein.PlaetzeGiesserei,
                Fixkosten = Allgemein.UnterhaltGiesserei, Standardprodukt = "Silberring", Startkarren = "Handkarren",
                Produkte = Waren.Where(x => x.Value.Zutaten.Count > 0).Select(x => x.Key).ToList(),
            },
            Betriebsart.Mine => new BetriebsartDaten
            {
                Name = "Grube", Beruf = "Schmied", Ort = Arbeitsort.Umland, Plaetze = Allgemein.PlaetzeMine,
                Fixkosten = Allgemein.PachtMine, Startkarren = "Ochsenkarren", Rohstoffe = new() { "Eisen", "Silber" },
            },
            _ => throw new InvalidOperationException($"Die Betriebsart {art} fehlt in den Daten („betriebsarten“)."),
        };
        _alteArten[art] = d;
        return d;
    }

    /// <summary>Gibt es diese Betriebsart in den Daten (oder als alte Standardart)?</summary>
    public bool KenntArt(Betriebsart art) => Betriebsarten.ContainsKey(art) || art is Betriebsart.Giesserei or Betriebsart.Mine;

    /// <summary>Jährliche Fixkosten einer Betriebsart (Unterhalt der Werkstatt bzw. Pacht im Umland).</summary>
    public double Fixkosten(Betriebsart art) => Art(art).Fixkosten;

    /// <summary>Arbeitsplätze einer Betriebsart.</summary>
    public int Plaetze(Betriebsart art) => Art(art).Plaetze;

    /// <summary>Arbeitsminuten je Spieljahr in dieser Betriebsart (ohne eigenen Wert: <c>allgemein.arbeitsminuten</c>).</summary>
    public int Arbeitsminuten(Betriebsart art) => Art(art).Arbeitsminuten ?? Allgemein.Arbeitsminuten;

    /// <summary>
    /// Wie lang die Schicht in dieser Betriebsart im Verhältnis zur üblichen ist (Wirtshaus 840 ÷ 720 = 14/12).
    /// Die Löhne unter „arbeitskraefte“ gelten für die übliche Arbeitszeit – mit diesem Faktor ist der Lohn je Stunde überall gleich.
    /// </summary>
    public double Schichtfaktor(Betriebsart art) => Arbeitsminuten(art) / (double)Allgemein.Arbeitsminuten;

    /// <summary>Lohn einer Arbeitskraft für ein Jahr in dieser Betriebsart (übliche Lohnstufe, mit dem <see cref="Schichtfaktor"/>).</summary>
    public double Lohn(string arbeitskraft, Betriebsart art) => Arbeitskraefte[arbeitskraft].Kosten * Schichtfaktor(art);

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

        if (a.Arbeitsminuten <= 0 || Stadtwirtschaft.Arbeitsbeginn + a.Arbeitsminuten > Zeit.Spielzeit.TagesendeMinute)
            f.Add($"allgemein.arbeitsminuten muss zwischen 1 und {Zeit.Spielzeit.TagesendeMinute - Stadtwirtschaft.Arbeitsbeginn} liegen – gespielt wird bis {Zeit.Spielzeit.TagesendeMinute / 60} Uhr.");
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

        foreach (var (art, d) in Betriebsarten)
        {
            if (d.Plaetze < 1) f.Add($"betriebsarten.{art}.plaetze muss mindestens 1 sein.");
            if (d.ErfahrungFaktor is < 0 or > 1) f.Add($"betriebsarten.{art}.erfahrungFaktor muss zwischen 0 und 1 liegen.");
            if (d.Fixkosten < 0) f.Add($"betriebsarten.{art}.fixkosten darf nicht negativ sein.");
            if (Berufe.Count > 0 && !Berufe.ContainsKey(d.Beruf)) f.Add($"betriebsarten.{art}: Den Beruf „{d.Beruf}“ gibt es nicht.");
            if (d.Startkarren != "" && !Logistik.Karren.ContainsKey(d.Startkarren)) f.Add($"betriebsarten.{art}: Den Karren „{d.Startkarren}“ gibt es nicht.");
            if (d.Produkte.Count > 0 && d.Rohstoffe.Count > 0) f.Add($"betriebsarten.{art}: entweder produkte (Werkstatt) oder rohstoffe (Umland).");
            if (d.OhneProduktion && !((Dieb?.Beruf == d.Beruf || Geldleiher?.Beruf == d.Beruf) && d.Ort == Arbeitsort.Werkstatt) && Wache?.Beruf != d.Beruf)
                f.Add($"betriebsarten.{art}: Ohne produkte und rohstoffe geht nur ein Betrieb des Diebes oder Geldleihers (in der Stadt) oder der Wache.");
            if (d.Rohstoffe.Count > 2) f.Add($"betriebsarten.{art}: höchstens zwei Rohstoffe.");
            if (!d.OhneProduktion && (d.Ort == Arbeitsort.Umland) != (d.Rohstoffe.Count > 0)) f.Add($"betriebsarten.{art}: Umlandbetriebe fördern Rohstoffe, Werkstätten stellen Produkte her.");
            foreach (var w in d.Produkte)
                if (!Waren.TryGetValue(w, out var ware) || ware.Zutaten.Count == 0) f.Add($"betriebsarten.{art}: „{w}“ ist keine Ware mit Zutaten.");
            foreach (var w in d.Rohstoffe)
                if (!Waren.TryGetValue(w, out var ware) || ware.Zutaten.Count > 0) f.Add($"betriebsarten.{art}: „{w}“ ist kein Rohstoff.");
            if (d.Standardprodukt is { } sp && !d.Produkte.Contains(sp)) f.Add($"betriebsarten.{art}: Das Standardprodukt „{sp}“ steht nicht unter produkte.");
            if (d.Stufe is < 1 or > 3) f.Add($"betriebsarten.{art}.stufe muss zwischen 1 und 3 liegen.");
            if (d.Ausbau is { } ziel)
            {
                if (!Betriebsarten.TryGetValue(ziel, out var z)) f.Add($"betriebsarten.{art}: Die Ausbaustufe {ziel} fehlt unter „betriebsarten“.");
                else
                {
                    if (z.Beruf != d.Beruf || z.Stufe != d.Stufe + 1 || !z.InDerStadt || !d.InDerStadt || z.OhneProduktion != d.OhneProduktion)
                        f.Add($"betriebsarten.{art}: Der Ausbau zu {ziel} muss eine Werkstatt desselben Berufs eine Stufe höher sein.");
                    foreach (var p in d.Produkte.Where(p => !z.Produkte.Contains(p)))
                        f.Add($"betriebsarten.{art}: Nach dem Ausbau zu {ziel} ginge „{p}“ verloren – die höhere Stufe muss alles können.");
                }
                if (d.AusbauKosten < 0) f.Add($"betriebsarten.{art}.ausbauKosten darf nicht negativ sein.");
            }
            if (d.Arbeitsminuten is { } am)
            {
                if (am <= 0 || Stadtwirtschaft.Arbeitsbeginn + am > Zeit.Spielzeit.TagesendeMinute)
                    f.Add($"betriebsarten.{art}.arbeitsminuten muss zwischen 1 und {Zeit.Spielzeit.TagesendeMinute - Stadtwirtschaft.Arbeitsbeginn} liegen – gespielt wird bis {Zeit.Spielzeit.TagesendeMinute / 60} Uhr.");
                // Diebe, Wache und Schreiber rechnen ihren Ertrag je Minute aus Jahreswerten über die übliche Arbeitszeit.
                if (d.OhneProduktion) f.Add($"betriebsarten.{art}: Eine eigene Arbeitszeit (arbeitsminuten) gibt es nur für Betriebe mit Ware.");
            }
            // Der Lohn wird am Jahresende nach der Art abgerechnet, die dann gilt – ein Ausbau darf die Schicht nicht ändern.
            if (d.Ausbau is { } nach && Betriebsarten.ContainsKey(nach) && Arbeitsminuten(nach) != Arbeitsminuten(art))
                f.Add($"betriebsarten.{art}: Nach dem Ausbau zu {nach} muss die Arbeitszeit (arbeitsminuten) gleich bleiben.");
            if (d.Kaufpreis < 0) f.Add($"betriebsarten.{art}.kaufpreis darf nicht negativ sein.");
            if (d.Kaufpreis > 0 && (!d.InDerStadt || d.Stufe != 1))
                f.Add($"betriebsarten.{art}: Einen kaufpreis haben nur Werkstätten der Stufe 1 – Umland wird gepachtet, höhere Stufen entstehen durch Ausbau.");
        }
        foreach (var (name, b) in Berufe)
        {
            if (!KenntArt(b.Werkstatt) || b.Umland is { } u0 && !KenntArt(u0)) f.Add($"berufe.{name}: Werkstatt oder Umlandbetrieb fehlt unter „betriebsarten“.");
            else if (Art(b.Werkstatt).Beruf != name || b.Umland is { } u1 && Art(u1).Beruf != name) f.Add($"berufe.{name}: Werkstatt und Umlandbetrieb müssen zum Beruf gehören.");
            if (b.Umland is null && Dieb?.Beruf != name && Geldleiher?.Beruf != name) f.Add($"berufe.{name}: Ohne Umlandbetrieb kommen nur Dieb und Geldleiher aus.");
            if (b.Lehrgeld < 0) f.Add($"berufe.{name}.lehrgeld darf nicht negativ sein.");
            foreach (var (volk, bonus) in b.Voelker.Where(x => x.Value is <= -0.5 or >= 0.5))
                f.Add($"berufe.{name}.voelker.{volk}: Ein Völkerbonus liegt zwischen −0,5 und +0,5 (war {bonus}).");
            if (KenntArt(b.Werkstatt) && Art(b.Werkstatt).Stufe != 1) f.Add($"berufe.{name}: Die Werkstatt muss Stufe 1 sein.");
        }
        foreach (var l in Liegenschaften.Where(l => KenntArt(l.Art) && Art(l.Art).Stufe != 1))
            f.Add($"Liegenschaft {l.Name}: Zu kaufen gibt es nur Werkstätten der Stufe 1 – höhere entstehen durch Ausbau.");
        Meistergrade?.Pruefen(f, Aktionspunkte?.Hoechstens ?? int.MaxValue);
        Gastwirtschaft?.Pruefen(f, Waren, Berufe);
        Buerger?.Pruefen(f);
        Ki?.Pruefen(f);
        if (Buerger is { Kopplung.Schaenke: > 0 } bk && Gastwirtschaft is { } gw &&
            bk.Handlungen.TryGetValue(Reichsgilde.Core.Buerger.Handlung.Einkehren, out var einkehr) && einkehr.Von != gw.OeffnetUm)
            f.Add($"buerger.handlungen.Einkehren.von ({einkehr.Von}) muss bei Kopplung an die Schänke gastwirtschaft.oeffnetUm ({gw.OeffnetUm}) sein.");
        if (Buerger is { } bg && Dynastie.IstAktiv)
            foreach (var volk in bg.Voelker.Keys.Where(v => !Dynastie.Voelker.ContainsKey(v)))
                f.Add($"buerger.voelker: Für {volk.Mehrzahl()} fehlen Dynastie-Daten (Namen, Lebensalter).");
        Kirche?.Pruefen(f, Waren, Berufe);
        Dieb?.Pruefen(f, Berufe);
        Wache?.Pruefen(f, Berufe, Logistik.Eskorten);
        Geldleiher?.Pruefen(f, Berufe);
        Parfum?.Pruefen(f, Waren, Berufe, Gesundheit);
        if (Wache is null && Logistik.Eskorten.Values.Any(e => e.Wache)) f.Add("logistik.eskorten: Eine Eskorte der Wache braucht den Abschnitt „wache“.");
        foreach (var l in Liegenschaften.Where(l => !KenntArt(l.Art)))
            f.Add($"Liegenschaft {l.Name}: Die Betriebsart {l.Art} fehlt unter „betriebsarten“.");
        Gesundheit?.Pruefen(f, Waren);
        Aktionspunkte?.Pruefen(f);
        Logistik.Pruefen(f);
        Handel?.Pruefen(f);
        Schiffshandel?.Pruefen(f, Waren, Logistik.Eskorten);
        Wohnen?.Pruefen(f, Staende.Count);
        Bauen?.Pruefen(f, Wohnen);
        Wegebau?.Pruefen(f);
        Verkauf.Pruefen(f);
        Stadt?.Pruefen(f);
        Werkstatt?.Pruefen(f, Berufe, Waren, Arbeitskraefte);

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
        Glaube?.Pruefen(f, Politik.Ratsherren);
        Dynastie.Pruefen(f, StandStufe);
        Justiz?.Pruefen(f);
        Gauner?.Pruefen(f);
        return f;
    }

    /// <summary>Zutatenkosten eines Stücks zu Grundpreisen.</summary>
    public double ZutatenZumGrundpreis(string ware) =>
        Waren[ware].Zutaten.Sum(z => Waren[z.Key].Grundpreis * z.Value);
}

/// <summary>
/// Einwohner der Stadt und ihre Nachfrage (Abschnitt „stadt“, PR #177):
/// <code>
/// Einwohner        = einwohnerGrund + einwohnerJeHaus × Häuser, die wirtschaften (im Spiel, mit mindestens einem Arbeiter)
/// Einwohnerfaktor  = Einwohner ÷ (einwohnerGrund + einwohnerJeHaus × bezugHaeuser)
/// </code>
/// Nachfrage und Grundversorgung (Nachfrage × NPC-Anteil) jeder Ware wachsen mit dem Einwohnerfaktor. Bei <c>bezugHaeuser</c> Häusern
/// (Spieler und drei KI-Häuser) ist er 1 – die Nachfrage in „waren“ gilt für diese Stadt. Ein Haus bringt im Mittel
/// <c>einwohnerJeHaus</c> Einwohner mit (Familie, Gesinde, Arbeiter mit ihren Familien – so viele, wie die Bürger-KI je Haus ansiedelt);
/// die wirklichen Arbeiter zählen bewusst nicht einzeln, sonst schüfe jeder neue Arbeiter mehr Nachfrage, als er herstellt.
/// </summary>
public sealed class StadtDaten
{
    /// <summary>Einwohner, die von keinem Haus leben (Tagelöhner, Geistliche, Bauern vor den Toren …).</summary>
    public double EinwohnerGrund { get; init; }

    /// <summary>Einwohner, die jedes wirtschaftende Haus mitbringt.</summary>
    public double EinwohnerJeHaus { get; init; }

    /// <summary>So viele Häuser hat die Stadt, für die die Nachfrage in „waren“ gilt.</summary>
    public int BezugHaeuser { get; init; } = 4;

    /// <summary>Einwohner bei so vielen Häusern.</summary>
    public double Einwohner(int haeuser) => EinwohnerGrund + EinwohnerJeHaus * haeuser;

    /// <summary>Faktor auf Nachfrage und Grundversorgung bei so vielen Häusern.</summary>
    public double Faktor(int haeuser) => Einwohner(haeuser) / Einwohner(BezugHaeuser);

    public void Pruefen(List<string> f)
    {
        if (EinwohnerGrund < 0 || EinwohnerJeHaus < 0) f.Add("stadt: einwohnerGrund und einwohnerJeHaus dürfen nicht negativ sein.");
        if (BezugHaeuser < 1) f.Add("stadt.bezugHaeuser muss mindestens 1 sein.");
        if (EinwohnerGrund <= 0 && EinwohnerJeHaus <= 0) f.Add("stadt: Ohne Einwohner gäbe es keine Nachfrage (einwohnerGrund oder einwohnerJeHaus größer als 0).");
    }
}
