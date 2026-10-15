namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Handel von Hand (Abschnitt „handel" in wirtschaft.json, ab Datenversion 24): Ein Karren bekommt einen Auftrag,
/// fährt zum Markt, verkauft und kauft dort und bringt die Einkäufe ins Lager. Fehlt der Abschnitt, gibt es keinen
/// Handel von Hand – die Karren fahren nur nach Verwalterregel.
/// </summary>
public sealed class HandelDaten
{
    /// <summary>Händlerspanne beim Einkauf von Hand: Marktpreis × (1 + Aufschlag).</summary>
    public double Aufschlag { get; init; } = 0.10;

    /// <summary>Der Markt hat von … bis … Uhr offen (volle Stunden) – nur dann wird von Hand gehandelt.</summary>
    public int MarktVon { get; init; } = 8;
    public int MarktBis { get; init; } = 20;

    public void Pruefen(List<string> f)
    {
        if (Aufschlag is < 0 or > 1) f.Add("handel.aufschlag muss zwischen 0 und 1 liegen.");
        if (MarktVon < Zeit.Spielzeit.TagesbeginnMinute / 60 || MarktBis > Zeit.Spielzeit.TagesendeMinute / 60 || MarktVon >= MarktBis)
            f.Add($"handel.marktVon/marktBis: {Zeit.Spielzeit.TagesbeginnMinute / 60} ≤ von < bis ≤ {Zeit.Spielzeit.TagesendeMinute / 60}.");
    }
}

/// <summary>Was ein Karren auf dem Markt kaufen soll (die Verkaufsware hat er schon geladen).</summary>
public sealed record Handelsplan(IReadOnlyDictionary<string, int> Kauf);
