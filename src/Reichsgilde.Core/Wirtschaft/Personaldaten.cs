namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Personal wie in Gilde 1 (Abschnitt „personal", ab Datenversion 59, PR #224): Jeder Betrieb hat eine Laufbahn mit drei Stufen
/// (Werkstatt Lehrling → Geselle → Vorarbeiter, Umland Knecht → Altknecht → Großknecht, Diebe Taschendieb → Dieb → Meisterdieb,
/// Wache Rekrut → Gefreiter → Feldwebel). Eingestellt werden nur Anfänger (erste Stufe); befördert wird von Hand gegen einen Brief,
/// sobald die Erfahrung reicht. Höchstens ein Vorarbeiter (dritte Stufe) je Betrieb; er bekommt eine Eigenschaft aus drei gezogenen.
/// Unfreie und Orks (Arbeitskräfte in keiner Laufbahn) haben keine Stufen. Fehlt der Abschnitt, gilt die alte Beförderung
/// (<see cref="WerkstattDaten.Befoerderung"/>) – so laufen alte Spielstände mit ihren eigenen Daten weiter.
/// </summary>
public sealed class PersonalDaten
{
    /// <summary>
    /// Erfahrungsstunden für die zweite und die dritte Stufe (gezählt ab dem Einstellen). Eine Arbeitsstunde bringt so viele Stunden,
    /// wie der Arbeiter in ihr schafft (Tempo × Laune): Ein Lehrling (0,67) sammelt in einem Spieljahr (12 Arbeitsstunden) etwa 8,
    /// ein Geselle 12 – 24 sind also etwa 3 Jahre als Lehrling, 84 weitere 5 Jahre als Geselle.
    /// </summary>
    public List<double> Erfahrung { get; init; } = new() { 24, 84 };

    /// <summary>Was die Beförderung zur zweiten und zur dritten Stufe kostet (Gesellenbrief, Vorarbeiterbrief).</summary>
    public List<double> Briefe { get; init; } = new() { 150, 400 };

    /// <summary>So viele Arbeiter der dritten Stufe darf ein Betrieb haben.</summary>
    public int VorarbeiterJeBetrieb { get; init; } = 1;

    /// <summary>Die Laufbahnen; für einen Betrieb gilt die erste, deren Beruf und Ort passen (die letzte ohne Bedingung ist die Vorgabe).</summary>
    public List<LaufbahnDaten> Laufbahnen { get; init; } = new();

    /// <summary>
    /// Wie ein neuer Arbeiter heißt und aussieht: der Anteil der Arbeiter, die das Volk des Hauses haben (sonst eines der Völker mit Namen
    /// unter „dynastie.voelker“), und der Anteil der Frauen.
    /// </summary>
    public double EigenesVolk { get; init; } = 0.6;
    public double Frauen { get; init; } = 0.15;

    /// <summary>Namen für Völker ohne Eintrag unter „dynastie.voelker“ (Orks, Oger) – Volk → Namen.</summary>
    public Dictionary<Volk, ArbeiternamenDaten> Namen { get; init; } = new();

    /// <summary>Die Laufbahn, die für einen Betrieb dieses Berufs an diesem Ort gilt (null: keine passt).</summary>
    public LaufbahnDaten? LaufbahnFuer(string beruf, Arbeitsort ort) =>
        Laufbahnen.FirstOrDefault(l => (l.Berufe.Count == 0 || l.Berufe.Contains(beruf)) && (l.Ort is null || l.Ort == ort));

    /// <summary>Stufe 1–3 einer Arbeitskraft (0: in keiner Laufbahn – Unfreie, Orks).</summary>
    public int StufeVon(string arbeitskraft)
    {
        foreach (var l in Laufbahnen)
        {
            var i = l.Stufen.IndexOf(arbeitskraft);
            if (i >= 0) return i + 1;
        }
        return 0;
    }

    /// <summary>Erfahrungsstunden, ab denen ein Arbeiter diese Stufe (2 oder 3) erreichen kann; Stufe 1: 0.</summary>
    public double Schwelle(int stufe) => stufe <= 1 ? 0 : Erfahrung[Math.Min(stufe, 3) - 2];

    /// <summary>Was der Brief zu dieser Stufe (2 oder 3) kostet.</summary>
    public double Brief(int stufe) => stufe <= 1 ? 0 : Briefe[Math.Min(stufe, 3) - 2];

    internal void Pruefen(List<string> f, IReadOnlyDictionary<string, ArbeitskraftDaten> kraefte, IReadOnlyDictionary<string, BerufDaten> berufe,
        IReadOnlyDictionary<string, WareDaten> waren)
    {
        if (Erfahrung.Count != 2 || Erfahrung[0] <= 0 || Erfahrung[1] <= Erfahrung[0])
            f.Add("personal.erfahrung: zwei Werte (zweite und dritte Stufe), größer 0 und steigend.");
        if (Briefe.Count != 2 || Briefe.Any(b => b < 0)) f.Add("personal.briefe: zwei Werte (zweite und dritte Stufe), nicht negativ.");
        if (VorarbeiterJeBetrieb < 0) f.Add("personal.vorarbeiterJeBetrieb darf nicht negativ sein.");
        if (EigenesVolk is < 0 or > 1) f.Add("personal.eigenesVolk muss zwischen 0 und 1 liegen.");
        if (Frauen is < 0 or > 1) f.Add("personal.frauen muss zwischen 0 und 1 liegen.");
        if (Laufbahnen.Count == 0) f.Add("personal.laufbahnen darf nicht leer sein.");
        if (Laufbahnen.Count > 0 && (Laufbahnen[^1].Berufe.Count > 0 || Laufbahnen[^1].Ort is not null))
            f.Add("personal.laufbahnen: Die letzte Laufbahn gilt für alle übrigen Betriebe – ohne berufe und ort.");
        foreach (var l in Laufbahnen)
        {
            var wo = $"personal.laufbahnen.{l.Name}";
            if (string.IsNullOrWhiteSpace(l.Name)) f.Add("personal.laufbahnen: Jede Laufbahn braucht einen Namen.");
            if (l.Stufen.Count != 3 || l.Stufen.Distinct().Count() != 3) f.Add($"{wo}.stufen: genau drei verschiedene Arbeitskräfte.");
            foreach (var k in l.Stufen.Where(k => !kraefte.ContainsKey(k))) f.Add($"{wo}.stufen: Die Arbeitskraft „{k}“ gibt es nicht.");
            foreach (var b in l.Berufe.Where(b => berufe.Count > 0 && !berufe.ContainsKey(b))) f.Add($"{wo}.berufe: Den Beruf „{b}“ gibt es nicht.");
            if (l.Eigenschaften.Count == 0) f.Add($"{wo}.eigenschaften: Ein Vorarbeiter braucht Eigenschaften zur Wahl.");
            foreach (var g in l.Eigenschaften)
            {
                if (string.IsNullOrWhiteSpace(g.Gruppe) || g.Stuecke.Count == 0) f.Add($"{wo}.eigenschaften: Jede Gruppe braucht einen Namen und Stücke.");
                foreach (var e in g.Stuecke)
                {
                    if (string.IsNullOrWhiteSpace(e.Kennung) || e.Kennung.Any(c => !char.IsAsciiLetterOrDigit(c)))
                        f.Add($"{wo}: Die Kennung „{e.Kennung}“ darf nur Buchstaben und Ziffern ohne Umlaute haben.");
                    if (string.IsNullOrWhiteSpace(e.Name)) f.Add($"{wo}.{e.Kennung} braucht einen Namen.");
                    e.Wirkung.Pruefen(f, $"{wo}.{e.Kennung}.wirkung", waren);
                }
            }
            foreach (var doppelt in l.Eigenschaften.SelectMany(g => g.Stuecke).GroupBy(e => e.Kennung).Where(g => g.Count() > 1))
                f.Add($"{wo}: Die Eigenschaft „{doppelt.Key}“ ist doppelt eingetragen.");
        }
        // Eine Arbeitskraft hat in allen Laufbahnen dieselbe Stufe (Lehrling ist überall Stufe 1).
        foreach (var k in Laufbahnen.SelectMany(l => l.Stufen).Distinct())
            if (Laufbahnen.Select(l => l.Stufen.IndexOf(k)).Where(i => i >= 0).Distinct().Count() > 1)
                f.Add($"personal.laufbahnen: „{k}“ steht in zwei Laufbahnen auf verschiedenen Stufen.");
        foreach (var (volk, n) in Namen)
            if (n.VornamenMann.Count == 0 || n.VornamenFrau.Count == 0 || n.Familiennamen.Count == 0)
                f.Add($"personal.namen.{volk}: vornamenMann, vornamenFrau und familiennamen dürfen nicht leer sein.");
    }
}

/// <summary>Eine Laufbahn: drei Arbeitskräfte von der ersten zur dritten Stufe und die Eigenschaften, die ein Arbeiter der dritten Stufe haben kann.</summary>
public sealed class LaufbahnDaten
{
    public string Name { get; init; } = "";

    /// <summary>Für Betriebe dieser Berufe (leer = alle).</summary>
    public List<string> Berufe { get; init; } = new();

    /// <summary>Nur an diesem Ort (null = überall).</summary>
    public Arbeitsort? Ort { get; init; }

    /// <summary>Die drei Arbeitskräfte (Namen unter „arbeitskraefte“), z. B. Lehrling, Geselle, Vorarbeiter.</summary>
    public List<string> Stufen { get; init; } = new();

    /// <summary>Gruppen von Eigenschaften (Ware, Führung, Betrieb); beim Befördern zur dritten Stufe wird aus jeder Gruppe eine gezogen.</summary>
    public List<EigenschaftsgruppeDaten> Eigenschaften { get; init; } = new();

    public string Anfaenger => Stufen.Count > 0 ? Stufen[0] : "";

    public EigenschaftDaten? Eigenschaft(string kennung) =>
        Eigenschaften.SelectMany(g => g.Stuecke).FirstOrDefault(e => e.Kennung == kennung);

    /// <summary>Die Gruppe, in der diese Eigenschaft steht.</summary>
    public string? GruppeVon(string kennung) => Eigenschaften.FirstOrDefault(g => g.Stuecke.Any(e => e.Kennung == kennung))?.Gruppe;
}

public sealed class EigenschaftsgruppeDaten
{
    public string Gruppe { get; init; } = "";
    public List<EigenschaftDaten> Stuecke { get; init; } = new();
}

/// <summary>Eine Eigenschaft eines Vorarbeiters. Sie wirkt wie Einrichtung auf den ganzen Betrieb, solange er dort arbeitet.</summary>
public sealed class EigenschaftDaten
{
    public string Kennung { get; init; } = "";
    public string Name { get; init; } = "";
    public EinrichtungWirkung Wirkung { get; init; } = new();
}

/// <summary>Vornamen und Familiennamen für Arbeiter eines Volkes ohne Eintrag unter „dynastie.voelker“.</summary>
public sealed class ArbeiternamenDaten
{
    public List<string> VornamenMann { get; init; } = new();
    public List<string> VornamenFrau { get; init; } = new();
    public List<string> Familiennamen { get; init; } = new();
}
