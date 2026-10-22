namespace Reichsgilde.Core.Wirtschaft;

/// <summary>Wie gut ein Betrieb seine Leute bezahlt – ein Faktor auf den Lohn und ein Ziel für die Laune.</summary>
public enum Lohnstufe
{
    Knapp,
    Ueblich,
    Gut,
}

public static class LohnstufeTexte
{
    public static string Name(this Lohnstufe stufe) => stufe switch
    {
        Lohnstufe.Knapp => "knapp",
        Lohnstufe.Ueblich => "üblich",
        Lohnstufe.Gut => "gut",
        _ => stufe.ToString(),
    };
}

/// <summary>
/// Die Werkstatt wie in Gilde 1 (Abschnitt „werkstatt", ab Datenversion 36): Lohnstufen, Prämie, Laune, Unfälle,
/// Beförderung und kaufbare Einrichtung je Beruf. Fehlt der Abschnitt, arbeiten alle mit Laune neutral, ohne Unfälle
/// und ohne Einrichtung – wie vorher. Die Zuteilung einzelner Arbeiter zu Waren gibt es auch ohne Abschnitt.
/// </summary>
public sealed class WerkstattDaten
{
    /// <summary>Lohnfaktor und Laune-Ziel je Stufe. Fehlt eine Stufe, gilt Faktor 1 und Laune neutral.</summary>
    public Dictionary<Lohnstufe, LohnstufeDaten> Lohnstufen { get; init; } = new();

    public PraemieDaten Praemie { get; init; } = new();
    public LauneDaten Laune { get; init; } = new();
    public UnfallDaten Unfall { get; init; } = new();
    public BefoerderungDaten Befoerderung { get; init; } = new();

    /// <summary>Kaufbare Einrichtung je Beruf (Name aus „berufe"). Fehlt ein Beruf, gibt es für ihn keine.</summary>
    public Dictionary<string, List<EinrichtungDaten>> Einrichtung { get; init; } = new();

    public LohnstufeDaten Stufe(Lohnstufe stufe) => Lohnstufen.GetValueOrDefault(stufe) ?? LohnstufeDaten.Neutral;

    /// <summary>Die Einrichtung, die eine Werkstatt dieses Berufs kaufen kann (leer, wenn es keine gibt).</summary>
    public IReadOnlyList<EinrichtungDaten> EinrichtungFuer(string beruf) =>
        Einrichtung.TryGetValue(beruf, out var liste) ? liste : Array.Empty<EinrichtungDaten>();

    public EinrichtungDaten? Stueck(string beruf, string kennung) => EinrichtungFuer(beruf).FirstOrDefault(e => e.Kennung == kennung);

    public void Pruefen(List<string> f, IReadOnlyDictionary<string, BerufDaten> berufe, IReadOnlyDictionary<string, WareDaten> waren,
        IReadOnlyDictionary<string, ArbeitskraftDaten> kraefte)
    {
        foreach (var (stufe, d) in Lohnstufen)
        {
            if (d.LohnFaktor < 0) f.Add($"werkstatt.lohnstufen.{stufe}.lohnFaktor darf nicht negativ sein.");
            if (Math.Abs(d.Laune) > 100) f.Add($"werkstatt.lohnstufen.{stufe}.laune liegt zwischen −100 und 100.");
        }
        if (Praemie.KostenJeArbeiter < 0) f.Add("werkstatt.praemie.kostenJeArbeiter darf nicht negativ sein.");
        if (Praemie.Minuten < 0) f.Add("werkstatt.praemie.minuten darf nicht negativ sein.");
        var l = Laune;
        if (l.Neutral is <= 0 or >= 100) f.Add("werkstatt.laune.neutral muss zwischen 0 und 100 liegen (ohne die Grenzen).");
        if (l.TempoBis is < 0 or >= 1) f.Add("werkstatt.laune.tempoBis muss zwischen 0 und unter 1 liegen.");
        if (l.JeStunde < 0) f.Add("werkstatt.laune.jeStunde darf nicht negativ sein.");
        if (l.KuendigungUnter is < 0 or > 100) f.Add("werkstatt.laune.kuendigungUnter muss zwischen 0 und 100 liegen.");
        if (l.KuendigungNachStunden < 1) f.Add("werkstatt.laune.kuendigungNachStunden muss mindestens 1 sein.");
        var u = Unfall;
        foreach (var (beruf, gefahr) in u.Gefahr)
        {
            if (berufe.Count > 0 && !berufe.ContainsKey(beruf)) f.Add($"werkstatt.unfall.gefahr: Den Beruf „{beruf}“ gibt es nicht.");
            if (gefahr is < 0 or > 1) f.Add($"werkstatt.unfall.gefahr.{beruf} muss zwischen 0 und 1 liegen.");
        }
        if (u.LauneMinuten < 0) f.Add("werkstatt.unfall.launeMinuten darf nicht negativ sein.");
        var b = Befoerderung;
        if (b.Von != "" && !kraefte.ContainsKey(b.Von)) f.Add($"werkstatt.befoerderung.von: Die Arbeitskraft „{b.Von}“ gibt es nicht.");
        if (b.Zu != "" && !kraefte.ContainsKey(b.Zu)) f.Add($"werkstatt.befoerderung.zu: Die Arbeitskraft „{b.Zu}“ gibt es nicht.");
        if (b.DienstMinuten < 0) f.Add("werkstatt.befoerderung.dienstMinuten darf nicht negativ sein.");
        foreach (var (beruf, liste) in Einrichtung)
        {
            if (berufe.Count > 0 && !berufe.ContainsKey(beruf)) f.Add($"werkstatt.einrichtung: Den Beruf „{beruf}“ gibt es nicht.");
            foreach (var doppelt in liste.GroupBy(e => e.Kennung).Where(g => g.Count() > 1))
                f.Add($"werkstatt.einrichtung.{beruf}: „{doppelt.Key}“ ist doppelt eingetragen.");
            foreach (var e in liste)
            {
                var wo = $"werkstatt.einrichtung.{beruf}.{e.Kennung}";
                if (string.IsNullOrWhiteSpace(e.Kennung) || e.Kennung.Any(c => !char.IsAsciiLetterOrDigit(c)))
                    f.Add($"werkstatt.einrichtung.{beruf}: Die Kennung „{e.Kennung}“ darf nur Buchstaben und Ziffern ohne Umlaute haben (Knoten Einrichtung_<Kennung> im Raummodell).");
                if (e.Preis < 0) f.Add($"{wo}.preis darf nicht negativ sein.");
                if (e.AbStufe is < 1 or > 3) f.Add($"{wo}.abStufe muss zwischen 1 und 3 liegen.");
                if (e.Voraussetzung is { } v && liste.All(x => x.Kennung != v)) f.Add($"{wo}.voraussetzung: „{v}“ gibt es beim {beruf} nicht.");
                if (e.Voraussetzung == e.Kennung) f.Add($"{wo}: Ein Stück kann nicht sich selbst voraussetzen.");
                var w = e.Wirkung;
                if (w.ZeitFaktor is <= 0 or > 1) f.Add($"{wo}.wirkung.zeitFaktor muss größer 0 und höchstens 1 sein.");
                if (w.LagerPlus < 0) f.Add($"{wo}.wirkung.lagerPlus darf nicht negativ sein.");
                foreach (var (name, faktor) in new[] { ("unfallFaktor", w.UnfallFaktor), ("brandFaktor", w.BrandFaktor), ("einbruchFaktor", w.EinbruchFaktor),
                             ("einbruchBonus", w.EinbruchBonus), ("fangFaktor", w.FangFaktor), ("spurenFaktor", w.SpurenFaktor),
                             ("razziaBonus", w.RazziaBonus), ("geleitFaktor", w.GeleitFaktor), ("lohnFaktor", w.LohnFaktor),
                             ("ausfallFaktor", w.AusfallFaktor) })
                    if (faktor is < 0 or > 1) f.Add($"{wo}.wirkung.{name} muss zwischen 0 und 1 liegen.");
                foreach (var (name, faktor) in new[] { ("streifenFaktor", w.StreifenFaktor), ("rahmenFaktor", w.RahmenFaktor), ("wechselFaktor", w.WechselFaktor) })
                    if (faktor is < 1 or > 2) f.Add($"{wo}.wirkung.{name} muss zwischen 1 und 2 liegen.");
                foreach (var ware in w.Waren.Where(x => !waren.ContainsKey(x)))
                    f.Add($"{wo}.wirkung.waren: Die Ware „{ware}“ gibt es nicht.");
            }
        }
    }
}

/// <summary>Eine Lohnstufe: Faktor auf den Lohn der Arbeitskraft und so viel über oder unter neutral zieht es die Laune.</summary>
public sealed class LohnstufeDaten
{
    public static readonly LohnstufeDaten Neutral = new();

    public double LohnFaktor { get; init; } = 1;
    public double Laune { get; init; }
}

/// <summary>Eine einmalige Prämie: kostet je Arbeiter, hebt das Laune-Ziel eine Weile.</summary>
public sealed class PraemieDaten
{
    public double KostenJeArbeiter { get; init; } = 30;
    public double Laune { get; init; } = 20;

    /// <summary>So lange (Spielminuten) wirkt sie. 1020 = ein Spieltag.</summary>
    public int Minuten { get; init; } = 1020;
}

/// <summary>
/// Laune 0–100 je Arbeiter. Sie läuft in jeder Arbeitsstunde um <see cref="JeStunde"/> Punkte auf ihr Ziel zu
/// (neutral + Lohnstufe + Prämie + Unfall). Das Tempo ändert sich linear: Laune 0 = −<see cref="TempoBis"/>, 100 = +<see cref="TempoBis"/>.
/// </summary>
public sealed class LauneDaten
{
    public double Neutral { get; init; } = 50;
    public double TempoBis { get; init; } = 0.2;
    public double JeStunde { get; init; } = 3;

    /// <summary>Wer so viele Arbeitsstunden am Stück unter dieser Laune bleibt, kündigt.</summary>
    public double KuendigungUnter { get; init; } = 20;
    public int KuendigungNachStunden { get; init; } = 6;
}

/// <summary>
/// Unfälle in Werkstätten: Gefahr je Arbeiter und Jahr nach Beruf. Wer verunglückt, fällt für den Rest des Tages aus
/// (bis zum Jahreswechsel, Lohn läuft weiter) – im Mittel ein halber Arbeitstag, der Leistungsverlust ist also Gefahr ÷ 2.
/// Danach ist die Laune aller im Betrieb eine Weile gedrückt.
/// </summary>
public sealed class UnfallDaten
{
    /// <summary>Beruf → Wahrscheinlichkeit je Arbeiter und Spieljahr (fehlt der Beruf: keine Unfälle).</summary>
    public Dictionary<string, double> Gefahr { get; init; } = new();

    public double Laune { get; init; } = -15;
    public int LauneMinuten { get; init; } = 1020;
}

/// <summary>Beförderung, z. B. Lehrling → Geselle nach einer Dienstzeit. Danach gilt der Lohn der neuen Arbeitskraft.</summary>
public sealed class BefoerderungDaten
{
    public string Von { get; init; } = "Lehrling";
    public string Zu { get; init; } = "Geselle";

    /// <summary>Dienstzeit in Spielminuten (2040 = zwei Spieljahre).</summary>
    public long DienstMinuten { get; init; } = 2 * Zeit.Spielzeit.MinutenProTag;
}

/// <summary>
/// Ein Stück Einrichtung, das eine Werkstatt kaufen kann. Die <see cref="Kennung"/> ist auch der Name des Knotens
/// „Einrichtung_&lt;Kennung&gt;" im Raummodell. Gekauft bleibt es beim Ausbau und geht beim Verkauf mit dem Haus.
/// </summary>
public sealed class EinrichtungDaten
{
    public string Kennung { get; init; } = "";
    public string Name { get; init; } = "";
    public double Preis { get; init; }

    /// <summary>Ab welcher Gebäudestufe man es kaufen kann.</summary>
    public int AbStufe { get; init; } = 1;

    /// <summary>Kennung eines Stücks, das schon da sein muss (null = keine Voraussetzung).</summary>
    public string? Voraussetzung { get; init; }

    public EinrichtungWirkung Wirkung { get; init; } = new();
}

/// <summary>Was ein Stück Einrichtung bewirkt. Mehrere Stücke wirken zusammen (Faktoren multipliziert, Lager addiert).</summary>
public sealed class EinrichtungWirkung
{
    /// <summary>Faktor auf die Zeit je Stück (0,9 = 10 % schneller).</summary>
    public double ZeitFaktor { get; init; } = 1;

    /// <summary>Nur für diese Waren (leer = alle).</summary>
    public List<string> Waren { get; init; } = new();

    /// <summary>So viel mehr Lager je Ware.</summary>
    public int LagerPlus { get; init; }

    /// <summary>Faktor auf die Unfallgefahr (0,5 = halb so oft).</summary>
    public double UnfallFaktor { get; init; } = 1;

    /// <summary>Faktor auf die Erfolgschance von Feuer (Sabotage, Brand nach verweigertem Schutzgeld).</summary>
    public double BrandFaktor { get; init; } = 1;

    /// <summary>Faktor auf die Erfolgschance von Diebstahl und Einbruch ins Lager.</summary>
    public double EinbruchFaktor { get; init; } = 1;

    /// <summary>
    /// Dieb: So viel höher ist die Erfolgschance der eigenen Einbrüche – absolut wie <c>dieb.einbruch.chanceBonus</c>
    /// (0,1 = +10 Prozentpunkte vor Wächter und Patrouille). Mehrere Stücke zählen zusammen.
    /// </summary>
    public double EinbruchBonus { get; init; }

    /// <summary>Dieb: Faktor auf die Chance, dass ein Taschendieb am Markt erwischt wird (0,75 = 25 % seltener).</summary>
    public double FangFaktor { get; init; } = 1;

    /// <summary>
    /// Dieb: Faktor auf die Spuren des Hauses – Zeugen der eigenen nächtlichen Aufträge und Beweise, die Auskundschafter
    /// und Razzien bei ihm finden (0,75 = 25 % seltener).
    /// </summary>
    public double SpurenFaktor { get; init; } = 1;

    /// <summary>
    /// Wache: So viel höher ist die Chance je Tat, bei einer Razzia einen Beweis zu finden – absolut wie
    /// <c>wache.razzia.chanceJeStufe</c> (0,1 = +10 Prozentpunkte; Deckel 95 %). Mehrere Stücke zählen zusammen.
    /// </summary>
    public double RazziaBonus { get; init; }

    /// <summary>
    /// Wache: Faktor auf den Schutz, den die Streife dieses Wachhauses gibt (1,25 = 25 % mehr) – vor dem Deckel
    /// <c>wache.patrouille.hoechstens</c>. Geprüft zwischen 1 und 2.
    /// </summary>
    public double StreifenFaktor { get; init; } = 1;

    /// <summary>Wache: Faktor auf die Erfolgschance von Überfällen auf Karren mit dem Geleit dieses Hauses (0,8 = 20 % seltener).</summary>
    public double GeleitFaktor { get; init; } = 1;

    /// <summary>
    /// Faktor auf die Lohnkosten der Leute dieses Betriebs (0,9 = 10 % weniger) – Waffenkammer der Wache: eigene Ausrüstung
    /// im Haus, kein Ausrüstungsgeld. Nicht der Sold, den die Stadt dem Haus zahlt.
    /// </summary>
    public double LohnFaktor { get; init; } = 1;

    /// <summary>
    /// Geldleiher: Faktor auf den Rahmen je Schreiber – so viel Kapital der Kasse betreuen die Schreiber (1,1 = 10 % mehr).
    /// Mehrere Stücke werden multipliziert (Rechenbretter und Hauptbücher: 1,21). Geprüft zwischen 1 und 2.
    /// </summary>
    public double RahmenFaktor { get; init; } = 1;

    /// <summary>Geldleiher: Faktor auf die Wechselgebühr dieses Betriebs (1,1 = 10 % mehr). Geprüft zwischen 1 und 2.</summary>
    public double WechselFaktor { get; init; } = 1;

    /// <summary>
    /// Geldleiher: Faktor auf den Verlust, wenn ein Kunde ausfällt (0,75 = 25 % weniger) – Stadtkunden der Kasse und Häuser,
    /// die mit einem Kredit aus dem Spiel gehen (dort das Pfand: Der Rest × (1 − Faktor) kommt zurück).
    /// </summary>
    public double AusfallFaktor { get; init; } = 1;

    public bool GiltFuer(string ware) => Waren.Count == 0 || Waren.Contains(ware);

    /// <summary>Die Wirkung in einem Satz für die Oberfläche.</summary>
    public string Text()
    {
        var teile = new List<string>();
        if (ZeitFaktor < 1)
            teile.Add($"{Zahlenformat.Ganz((1 - ZeitFaktor) * 100)} % weniger Zeit je Stück{(Waren.Count > 0 ? $" ({string.Join(", ", Waren)})" : "")}");
        if (LagerPlus > 0) teile.Add($"{LagerPlus} mehr Lager je Ware");
        if (UnfallFaktor < 1) teile.Add(UnfallFaktor == 0.5 ? "Unfälle halb so oft" : $"Unfälle {Zahlenformat.Ganz((1 - UnfallFaktor) * 100)} % seltener");
        if (BrandFaktor < 1) teile.Add($"Feuer {Zahlenformat.Ganz((1 - BrandFaktor) * 100)} % seltener");
        if (EinbruchFaktor < 1) teile.Add($"Diebstahl und Einbruch {Zahlenformat.Ganz((1 - EinbruchFaktor) * 100)} % seltener");
        if (EinbruchBonus > 0) teile.Add($"+{Zahlenformat.Ganz(EinbruchBonus * 100)} % Erfolg beim Einbruch");
        if (FangFaktor < 1) teile.Add($"Taschendiebe {Zahlenformat.Ganz((1 - FangFaktor) * 100)} % seltener gefasst");
        if (SpurenFaktor < 1) teile.Add($"Zeugen und Beweise {Zahlenformat.Ganz((1 - SpurenFaktor) * 100)} % seltener");
        if (RazziaBonus > 0) teile.Add($"+{Zahlenformat.Ganz(RazziaBonus * 100)} % Beweise bei Razzien");
        if (StreifenFaktor > 1) teile.Add($"Streife schützt {Zahlenformat.Ganz((StreifenFaktor - 1) * 100)} % mehr");
        if (LohnFaktor < 1) teile.Add($"Lohn der Leute {Zahlenformat.Ganz((1 - LohnFaktor) * 100)} % niedriger");
        if (GeleitFaktor < 1) teile.Add($"Überfälle auf das Geleit {Zahlenformat.Ganz((1 - GeleitFaktor) * 100)} % seltener erfolgreich");
        if (RahmenFaktor > 1) teile.Add($"Rahmen je Schreiber {Zahlenformat.Ganz((RahmenFaktor - 1) * 100)} % größer");
        if (WechselFaktor > 1) teile.Add($"Wechselgebühren {Zahlenformat.Ganz((WechselFaktor - 1) * 100)} % höher");
        if (AusfallFaktor < 1) teile.Add($"Verlust bei Ausfall eines Kunden {Zahlenformat.Ganz((1 - AusfallFaktor) * 100)} % kleiner");
        return teile.Count == 0 ? "keine Wirkung" : string.Join(", ", teile);
    }
}
