namespace Reichsgilde.Core.Gauner;

/// <summary>Was man im Hinterzimmer in Auftrag geben kann.</summary>
public enum Gaunerstueck
{
    /// <summary>Einbruch ins Lager eines Betriebs – der Hehler zahlt einen Teil des Marktwerts.</summary>
    Diebstahl,
    /// <summary>Anschlag auf einen Betrieb – Stillstand und Reparaturkosten.</summary>
    Sabotage,
    /// <summary>Hinterhalt auf die nächste beladene Umlandfahrt eines Hauses.</summary>
    Ueberfall,
    /// <summary>Krötenschleim ins Essen: Das Oberhaupt eines Hauses erkrankt.</summary>
    Kroetenschleim,
}

public static class GaunerstueckTexte
{
    public static string Name(this Gaunerstueck g) => g switch
    {
        Gaunerstueck.Ueberfall => "Überfall",
        Gaunerstueck.Kroetenschleim => "Krötenschleim",
        _ => g.ToString(),
    };
}

public sealed class DiebstahlDaten
{
    public double Kosten { get; init; } = 150;
    public double Chance { get; init; } = 0.7;

    /// <summary>So viele Stück schleppt die Bande höchstens fort (die wertvollsten zuerst).</summary>
    public int HoechstensStueck { get; init; } = 12;
}

public sealed class SabotageDaten
{
    public double Kosten { get; init; } = 250;
    public double Chance { get; init; } = 0.6;

    /// <summary>So viele Arbeitsminuten steht der Betrieb still (360 = ein halber Arbeitstag).</summary>
    public int StillstandMinuten { get; init; } = 360;

    /// <summary>Reparaturkosten für das Opfer.</summary>
    public double Reparatur { get; init; } = 150;
}

public sealed class KroetenschleimDaten
{
    public double Kosten { get; init; } = 200;

    /// <summary>Chance, dass der Schleim ins Essen gelangt (Wächter helfen nicht – es trifft die Person, nicht den Betrieb).</summary>
    public double Chance { get; init; } = 0.6;

    /// <summary>Wer dieses Handwerk beherrscht, mischt den Schleim selbst (ab Datenversion 15; leer = kein Vorteil).</summary>
    public string Beruf { get; init; } = "";

    /// <summary>Kostenfaktor für Häuser mit diesem Beruf (0,5 = halber Preis).</summary>
    public double BerufKostenFaktor { get; init; } = 1;

    /// <summary>So viel höher ist ihre Erfolgschance.</summary>
    public double BerufBonus { get; init; }
}

public sealed class UeberfallDaten
{
    public double Kosten { get; init; } = 300;

    /// <summary>Stärke der angeheuerten Wegelagerer – Erfolg = Stärke × (1 − Schutz der Eskorte).</summary>
    public double Staerke { get; init; } = 0.8;

    /// <summary>So lange (Spielminuten) liegt der Hinterhalt auf der Lauer. 1020 = ein Spieltag (05–22 Uhr) = ein Jahr.</summary>
    public int GueltigMinuten { get; init; } = 1020;
}

/// <summary>
/// Alle Zahlen zur Unterwelt (Abschnitt "gauner" in wirtschaft.json).
///
/// Ausgeführt wird bei Nacht und Nebel (20:00, Beginn der Dunklen Nacht). Jedes Gaunerstück hinterlässt
/// eine Spur fürs Gericht; mit <see cref="ZeugenChance"/> hat das Opfer sofort einen Beweis.
/// Ein Wächter halbiert die Erfolgschance von Diebstahl und Sabotage und macht Zeugen wahrscheinlicher.
/// </summary>
public sealed class GaunerDaten
{
    /// <summary>Anteil des Marktwerts, den der Hehler für Diebesgut und Beute zahlt.</summary>
    public double HehlerAnteil { get; init; } = 0.5;

    public double ZeugenChance { get; init; } = 0.25;
    public double ZeugenChanceWaechter { get; init; } = 0.6;

    /// <summary>Misslingt ein Gaunerstück, wird die Bande leichter gesehen: Zeugenchance × diesen Faktor.</summary>
    public double ZeugenFaktorMisserfolg { get; init; } = 2;

    public double WaechterKosten { get; init; } = 150;

    /// <summary>Faktor auf die Erfolgschance von Diebstahl und Sabotage, wenn ein Wächter da ist.</summary>
    public double WaechterSchutz { get; init; } = 0.5;

    public DiebstahlDaten Diebstahl { get; init; } = new();
    public SabotageDaten Sabotage { get; init; } = new();
    public UeberfallDaten Ueberfall { get; init; } = new();
    public KroetenschleimDaten Kroetenschleim { get; init; } = new();

    /// <summary>Ab so vielen Beweisen kann man Schweigegeld verlangen.</summary>
    public int ErpressungMindestBeweise { get; init; } = 3;

    public double Kosten(Gaunerstueck g) => g switch
    {
        Gaunerstueck.Diebstahl => Diebstahl.Kosten,
        Gaunerstueck.Sabotage => Sabotage.Kosten,
        Gaunerstueck.Kroetenschleim => Kroetenschleim.Kosten,
        _ => Ueberfall.Kosten,
    };

    internal void Pruefen(List<string> f)
    {
        foreach (var (name, wert) in new[] { ("hehlerAnteil", HehlerAnteil), ("zeugenChance", ZeugenChance), ("zeugenChanceWaechter", ZeugenChanceWaechter),
                     ("waechterSchutz", WaechterSchutz), ("diebstahl.chance", Diebstahl.Chance), ("sabotage.chance", Sabotage.Chance), ("ueberfall.staerke", Ueberfall.Staerke),
                     ("kroetenschleim.chance", Kroetenschleim.Chance), ("kroetenschleim.berufKostenFaktor", Kroetenschleim.BerufKostenFaktor),
                     ("kroetenschleim.berufBonus", Kroetenschleim.BerufBonus) })
            if (wert is < 0 or > 1) f.Add($"gauner.{name} muss zwischen 0 und 1 liegen.");
        if (WaechterKosten < 0 || Diebstahl.Kosten < 0 || Sabotage.Kosten < 0 || Ueberfall.Kosten < 0 || Sabotage.Reparatur < 0 || Kroetenschleim.Kosten < 0)
            f.Add("gauner: Kosten dürfen nicht negativ sein.");
        if (Diebstahl.HoechstensStueck < 1 || Sabotage.StillstandMinuten < 0 || Ueberfall.GueltigMinuten < 1)
            f.Add("gauner: hoechstensStueck und gueltigMinuten müssen mindestens 1 sein, stillstandMinuten mindestens 0.");
        if (ErpressungMindestBeweise < 1) f.Add("gauner.erpressungMindestBeweise muss mindestens 1 sein.");
        if (ZeugenFaktorMisserfolg < 1) f.Add("gauner.zeugenFaktorMisserfolg muss mindestens 1 sein.");
    }
}
