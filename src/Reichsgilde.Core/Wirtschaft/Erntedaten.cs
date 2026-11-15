namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// „Härte der Zeit“: wie oft und wie stark gute und schlechte Erntejahre kommen. Wird beim Start der Partie gewählt
/// (<see cref="Partie.Partieeinstellungen.HaerteDerZeit"/>) und gilt für alle Häuser gleich (Nutzer 15.11.2026).
/// Der Standard „Wie überliefert“ hat den Wert 0: Er wird im Spielstand nicht geschrieben, Spielstände mit Standard bleiben bitgleich wie vorher.
/// Für die Anzeige gilt die Reihenfolge Mild, Wie überliefert, Hart (<see cref="Partie.Partieeinstellungen.HaertenZurWahl"/>).
/// </summary>
public enum HaerteDerZeit
{
    /// <summary>„Milde Jahre“: seltene, schwache Ausschläge, keine Missernte – die Ernte ist fast nur Stimmung.</summary>
    Mild = 1,

    /// <summary>„Wie überliefert“ (Standard): magere Jahre öfter, Missernten selten.</summary>
    Ueberliefert = 0,

    /// <summary>„Harte Jahre“: magere Jahre und Missernten häufiger und stärker.</summary>
    Hart = 2,
}

/// <summary>Wie gut die Ernte eines Jahres ausfällt.</summary>
public enum Erntegute
{
    Gut,
    Gewoehnlich,
    Mager,
    Missernte,
}

/// <summary>
/// Abschnitt „ernte“ (ab Datenversion 91, „Härte um 1400“ PR 1): Erntejahr und Teuerung. Jede Ernte (im Herbstjahr) bekommt eine Güte; sie
/// verschiebt die Grundversorgung der Getreidewaren bis zur nächsten Ernte und damit ihren Preis. Fehlt der Abschnitt, gibt es keine Erntejahre.
/// </summary>
public sealed class ErnteDaten
{
    /// <summary>
    /// Die betroffenen Waren und wie stark: 1 = voll (Gerste, Roggen), 0,5 = halb (was aus Getreide gemacht wird: Körnermahlzeit, Bier).
    /// </summary>
    public Dictionary<string, double> Waren { get; init; } = new();

    /// <summary>Faktor einer guten Ernte auf die Grundversorgung (vor Stufe und Gewicht der Ware).</summary>
    public double Gut { get; init; } = 1.25;

    /// <summary>Faktor einer mageren Ernte auf die Grundversorgung.</summary>
    public double Mager { get; init; } = 0.75;

    /// <summary>Faktor einer Missernte auf die Grundversorgung.</summary>
    public double Missernte { get; init; } = 0.5;

    /// <summary>Grenzen nach unten und oben: Der Faktor einer Ware bleibt immer zwischen diesen Werten, egal wie hart die Stufe ist.</summary>
    public double UntereGrenze { get; init; } = 0.35;
    public double ObereGrenze { get; init; } = 1.5;

    public ErnteStufe Mild { get; init; } = new() { ChanceGut = 0.2, ChanceMager = 0.15, ChanceMissernte = 0, Staerke = 0.5 };
    public ErnteStufe Ueberliefert { get; init; } = new();
    public ErnteStufe Hart { get; init; } = new() { ChanceGut = 0.15, ChanceMager = 0.3, ChanceMissernte = 0.2, Staerke = 1.25 };

    public ErnteStufe Von(HaerteDerZeit h) => h switch
    {
        HaerteDerZeit.Mild => Mild,
        HaerteDerZeit.Hart => Hart,
        _ => Ueberliefert,
    };

    /// <summary>Faktor einer Güte vor Stufe und Gewicht (gewöhnlich = 1).</summary>
    public double FaktorVon(Erntegute g) => g switch
    {
        Erntegute.Gut => Gut,
        Erntegute.Mager => Mager,
        Erntegute.Missernte => Missernte,
        _ => 1,
    };

    internal void Pruefen(List<string> f, IReadOnlyDictionary<string, WareDaten> waren)
    {
        const string p = "ernte";
        foreach (var (ware, gewicht) in Waren)
        {
            if (!waren.ContainsKey(ware)) f.Add($"{p}.waren: Eine Ware „{ware}“ gibt es nicht.");
            if (gewicht is <= 0 or > 1) f.Add($"{p}.waren.{ware} muss größer als 0 und höchstens 1 sein.");
        }
        if (Gut is < 1 or > 2) f.Add($"{p}.gut muss zwischen 1 und 2 liegen.");
        if (Mager is <= 0 or > 1) f.Add($"{p}.mager muss größer als 0 und höchstens 1 sein.");
        if (Missernte <= 0 || Missernte > Mager) f.Add($"{p}.missernte muss größer als 0 und höchstens so groß wie mager sein.");
        if (UntereGrenze is <= 0 or > 1 || ObereGrenze is < 1 or > 3) f.Add($"{p}: untereGrenze muss in (0, 1], obereGrenze in [1, 3] liegen.");
        Mild.Pruefen(f, $"{p}.mild");
        Ueberliefert.Pruefen(f, $"{p}.ueberliefert");
        Hart.Pruefen(f, $"{p}.hart");
    }
}

/// <summary>Eine Stufe der „Härte der Zeit“: wie oft eine Ernte gut, mager oder eine Missernte ist (der Rest gewöhnlich) und wie stark sie wirkt.</summary>
public sealed class ErnteStufe
{
    public double ChanceGut { get; init; } = 0.2;
    public double ChanceMager { get; init; } = 0.25;
    public double ChanceMissernte { get; init; } = 0.1;

    /// <summary>Stärke der Ausschläge: Faktor einer Ware = 1 + (Faktor der Güte − 1) × Stärke × Gewicht der Ware.</summary>
    public double Staerke { get; init; } = 1;

    internal void Pruefen(List<string> f, string p)
    {
        if (ChanceGut < 0 || ChanceMager < 0 || ChanceMissernte < 0 || ChanceGut + ChanceMager + ChanceMissernte > 1)
            f.Add($"{p}: Die Chancen dürfen nicht negativ sein und zusammen höchstens 1 ergeben.");
        if (Staerke is < 0 or > 2) f.Add($"{p}.staerke muss zwischen 0 und 2 liegen.");
    }
}
