namespace Reichsgilde.Core.Dynastie;

/// <summary>Die fünf Talente einer Person (0 bis 6 Sterne, halbe Sterne möglich).</summary>
public enum Talent
{
    /// <summary>Bessere Erlöse auf dem Markt.</summary>
    Verhandeln,
    /// <summary>Die Arbeitskräfte schaffen mehr.</summary>
    Handwerk,
    /// <summary>Gaunerstücke gelingen öfter, Zeugen sehen weniger.</summary>
    NachtUndNebel,
    /// <summary>Stärkere Wegelagerer, besser geschützte eigene Karren.</summary>
    Kampf,
    /// <summary>Wahlen im Rat, Plädoyer vor Gericht, Werben in der Brautschau.</summary>
    Rhetorik,
}

public static class TalentTexte
{
    public static readonly Talent[] Alle = Enum.GetValues<Talent>();

    public static string Name(this Talent t) => t switch
    {
        Talent.Handwerk => "Handwerkskunst",
        Talent.NachtUndNebel => "Bei Nacht und Nebel",
        _ => t.ToString(),
    };

    /// <summary>Kurzname für enge Anzeigen.</summary>
    public static string Kurz(this Talent t) => t switch
    {
        Talent.Verhandeln => "Verhandeln",
        Talent.Handwerk => "Handwerk",
        Talent.NachtUndNebel => "Nacht",
        Talent.Kampf => "Kampf",
        _ => "Rhetorik",
    };

    /// <summary>„2½" statt „2,5" – Sterne in halben Schritten.</summary>
    public static string Sterne(double wert)
    {
        var halbe = (int)Math.Round(wert * 2);
        var ganz = halbe / 2;
        return halbe % 2 == 0 ? ganz.ToString() : ganz == 0 ? "½" : $"{ganz}½";
    }
}

/// <summary>Ein Ausbildungsweg für Kinder (Abschnitt "dynastie.talente.ausbildungen").</summary>
public sealed class AusbildungDaten
{
    /// <summary>Kosten je Jahr, solange das Kind in der Ausbildung ist.</summary>
    public double Kosten { get; init; }

    /// <summary>Ab welchem Stand des Hauses der Weg offensteht (null = für alle).</summary>
    public string? MindestStand { get; init; }

    /// <summary>Zuwachs je Ausbildungsjahr – für ein Menschenkind; andere Völker werden auf dieselbe Gesamtsumme umgerechnet.</summary>
    public Dictionary<Talent, double> Talente { get; init; } = new();

    public string Beschreibung { get; init; } = "";
}

/// <summary>
/// Wie stark ein Stern des Oberhaupts wirkt (Abschnitt "dynastie.talente.wirkung").
/// Gezählt wird immer das Talent des Oberhaupts – es führt das Haus.
/// </summary>
public sealed class TalentWirkung
{
    /// <summary>Mehr Erlös je Stern Verhandeln (0,015 = +1,5 %).</summary>
    public double Verhandeln { get; init; } = 0.015;

    /// <summary>Mehr Leistung aller Arbeitskräfte je Stern Handwerkskunst (0,03 = +3 %).</summary>
    public double Handwerk { get; init; } = 0.03;

    /// <summary>Punkte bei Ratswahlen je Stern Rhetorik.</summary>
    public double RhetorikRat { get; init; } = 2;

    /// <summary>Plädoyer vor Gericht: Überzeugung je Stern Unterschied zwischen Ankläger und Beklagtem (innerhalb der Höchstwirkung).</summary>
    public double RhetorikGericht { get; init; } = 1;

    /// <summary>Mehr Zuneigung je Gulden und Stern Rhetorik (0,05 = +5 %).</summary>
    public double RhetorikWerben { get; init; } = 0.05;

    /// <summary>Mehr Erfolgschance je Stern Bei Nacht und Nebel (0,04 = +4 Prozentpunkte).</summary>
    public double NachtErfolg { get; init; } = 0.04;

    /// <summary>Weniger Zeugen je Stern Bei Nacht und Nebel (0,08 = −8 % der Zeugenchance).</summary>
    public double NachtZeugen { get; init; } = 0.08;

    /// <summary>Stärkere Wegelagerer je Stern Kampf des Auftraggebers.</summary>
    public double KampfUeberfall { get; init; } = 0.04;

    /// <summary>Mehr Schutz für die eigenen Karren je Stern Kampf.</summary>
    public double KampfSchutz { get; init; } = 0.03;
}

/// <summary>
/// Talente und Ausbildung (Abschnitt "dynastie.talente").
///
/// Woher die Talente kommen:
///   • Erwachsene, die neu ins Spiel kommen (Oberhaupt, Heiratskandidaten): Grundwert ihres Volkes ± Streuung.
///   • Kinder: Vererbung × Mittel der Eltern + (1 − Vererbung) × Grundwert des Volkes, dazu ± halbe Streuung.
///   • Ausbildung: Zuwachs je Jahr, solange das Kind im Ausbildungsalter ist (zweite Hälfte der Kindheit).
/// </summary>
public sealed class TalentDaten
{
    public double Hoechstwert { get; init; } = 6;
    public double Streuung { get; init; } = 1;
    public double Vererbung { get; init; } = 0.5;

    /// <summary>Ausbildung ab diesem Anteil der Volljährigkeit (0,5 = Mensch ab 8 Jahren).</summary>
    public double AusbildungAb { get; init; } = 0.5;

    /// <summary>So viele Ausbildungsjahre hat ein Menschenkind – die Zuwächse anderer Völker werden darauf umgerechnet.</summary>
    public double ReferenzJahre { get; init; } = 8;

    public TalentWirkung Wirkung { get; init; } = new();
    public Dictionary<Volk, Dictionary<Talent, double>> Voelker { get; init; } = new();
    public Dictionary<string, AusbildungDaten> Ausbildungen { get; init; } = new();

    public bool IstAktiv => Voelker.Count > 0;

    public double Grundwert(Volk volk, Talent t) =>
        Voelker.TryGetValue(volk, out var werte) ? werte.GetValueOrDefault(t) : 0;

    internal void Pruefen(List<string> f, Func<string, int> standStufe)
    {
        if (!IstAktiv) return;
        if (Hoechstwert <= 0 || Streuung < 0 || Vererbung is < 0 or > 1 || AusbildungAb is < 0 or >= 1 || ReferenzJahre <= 0)
            f.Add("dynastie.talente: hoechstwert > 0, streuung ≥ 0, vererbung und ausbildungAb zwischen 0 und 1, referenzJahre > 0.");
        foreach (var (volk, werte) in Voelker)
            foreach (var (t, w) in werte)
                if (w < 0 || w > Hoechstwert) f.Add($"dynastie.talente.voelker.{volk}.{t}: muss zwischen 0 und {Hoechstwert} liegen.");
        foreach (var (name, a) in Ausbildungen)
        {
            if (a.Kosten < 0) f.Add($"Ausbildung {name}: kosten dürfen nicht negativ sein.");
            if (a.MindestStand is { } st && standStufe(st) < 0) f.Add($"Ausbildung {name}: mindestStand „{st}“ gibt es nicht.");
            if (a.Talente.Count == 0) f.Add($"Ausbildung {name}: talente fehlen.");
        }
    }
}
