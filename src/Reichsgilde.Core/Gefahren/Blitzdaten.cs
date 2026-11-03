namespace Reichsgilde.Core.Gefahren;

/// <summary>
/// Blitzschlag im Gewitter (Abschnitt <c>blitz</c> in wirtschaft.json, ab Datenversion 68; Wunsch des Nutzers vom 2.11.2026). Fehlt der Abschnitt,
/// schlägt kein Blitz ein. Schaden (Blitzschaden, Variante B, Nutzer 3.11.2026): Eine Werkstatt steht still wie nach einer Sabotage, zahlt eine
/// Reparatur nach ihrem Bauwert und verliert einen Teil ihres Lagers; ein Wohnhaus verliert bis zur Instandsetzung sein Ansehen und zahlt eine
/// Reparatur nach der Formel der Brandstiftung. Wie schwer, folgt aus der gewürfelten Brenndauer (<see cref="Gefahren.Blitzwesen.Schwere"/>).
/// Das Feuer kann auf ein Nachbarhaus überspringen (<see cref="Sprung"/>).
/// Alle neuen Werte haben Vorgaben ohne Wirkung: Spielstände mit älteren Daten (bis Datenversion 70) rechnen wie bisher.
/// </summary>
public sealed class BlitzDaten
{
    /// <summary>Chance je Gewitter, dass der Blitz in ein Gebäude der Stadt einschlägt (0,25: etwa jedes vierte Gewitter).</summary>
    public double Chance { get; init; } = 0.25;

    /// <summary>So lange brennt es (Spielminuten, gewürfelt von … bis); danach ist das Feuer gelöscht oder das Haus ausgebrannt.</summary>
    public int[] DauerMinuten { get; init; } = { 60, 180 };

    /// <summary>
    /// Höhe der Gebäude in Metern nach Kennung im Gebäudekatalog (Werkstätten und Wohnhäuser nach ihrem Modell: „schmied“, „stadtpalais“ …). Hohe
    /// Gebäude und Gebäude auf Anhöhen trifft es öfter: Gewicht = Höhe + Höhe des Bodens über dem Mittel der Stadt (linear). Kirchtürme haben um 1400
    /// keinen Blitzableiter. Gebäude, die hier fehlen, haben <see cref="Standardhoehe"/>; Höhe 0 nimmt ein Gebäude aus (Marktplatz).
    /// </summary>
    public Dictionary<string, double> Hoehen { get; init; } = new();

    /// <summary>Höhe der Gebäude, die in <see cref="Hoehen"/> fehlen (Meter).</summary>
    public double Standardhoehe { get; init; } = 10;

    /// <summary>
    /// Werkstatt: So viele Arbeitsminuten wird nach dem Einschlag repariert statt gearbeitet (wie <c>gauner.sabotage.stillstandMinuten</c>). Sie
    /// beginnen mit dem Einschlag, darum ruht die Arbeit auch, solange es brennt – mindestens so lange wie das längste Feuer.
    /// </summary>
    public int StillstandMinuten { get; init; } = 300;

    /// <summary>
    /// Werkstatt: Reparatur für den Besitzer mindestens so viele Gulden (bis Datenversion 70 der feste Betrag, dort 0). Sonst gilt
    /// <see cref="ReparaturAnteil"/> vom Bauwert.
    /// </summary>
    public double Reparatur { get; init; }

    /// <summary>
    /// Werkstatt: Reparatur als Anteil am Bauwert (Kaufpreis der Stufe 1 plus Ausbauten, ohne Einrichtung) – [leichtes, schweres Feuer]; dazwischen
    /// nach der <see cref="Gefahren.Blitzwesen.Schwere"/>. Vorgabe [0, 0] = nur <see cref="Reparatur"/>.
    /// </summary>
    public double[] ReparaturAnteil { get; init; } = { 0, 0 };

    /// <summary>Werkstatt: Anteil jeder Ware im Lager, der verbrennt – [leicht, schwer]; ganze Stück, abgerundet. Vorgabe [0, 0] = nichts.</summary>
    public double[] LagerAnteil { get; init; } = { 0, 0 };

    /// <summary>
    /// Wächter (Werkstatt) bzw. Hauswächter (Wohnhaus) löschen mit: Brenndauer und Schwere mal diesem Faktor (0,67: ein Drittel kürzer).
    /// Vorgabe 1 = keine Wirkung.
    /// </summary>
    public double WaechterFaktor { get; init; } = 1;

    /// <summary>Wohnhaus: So lange (Spielminuten nach dem Ende des Feuers) wird instand gesetzt; bis dahin fehlt das Ansehen des Hauses.</summary>
    public int InstandMinuten { get; init; } = Zeit.Spielzeit.MinutenProTag;

    /// <summary>Wohnhaus: Reparatur für den Bewohner mindestens so viele Gulden (bis Datenversion 70 der feste Betrag, dort 0).</summary>
    public double WohnhausReparatur { get; init; }

    /// <summary>
    /// Wohnhaus: Reparatur als Vielfaches des Brandschadens nach einer Brandstiftung (<c>gauner.wohnhaus.brand</c>: Anteil vom Ausbaupreis,
    /// mindestens …) – [leicht, schwer]. Vorgabe [0, 0] = nur <see cref="WohnhausReparatur"/>. Ohne Abschnitt „gauner“ gilt nur der Mindestbetrag.
    /// </summary>
    public double[] WohnhausAnteil { get; init; } = { 0, 0 };

    /// <summary>Überspringen auf Nachbarhäuser (Nutzer 3.11.2026). Null = es springt nicht (Daten bis 70).</summary>
    public BlitzSprungDaten? Sprung { get; init; }

    public void Pruefen(List<string> f)
    {
        if (Chance is < 0 or > 1) f.Add("blitz.chance muss zwischen 0 und 1 liegen.");
        if (DauerMinuten is not { Length: 2 } d || d[0] < 1 || d[1] < d[0])
            f.Add("blitz.dauerMinuten braucht [von, bis] mit 1 ≤ von ≤ bis.");
        else if (StillstandMinuten < d[1])
            f.Add("blitz.stillstandMinuten muss mindestens so lang sein wie das längste Feuer (dauerMinuten[1]) – sonst arbeitet die Werkstatt, während es brennt.");
        if (Hoehen.Any(h => h.Value < 0)) f.Add("blitz.hoehen: Keine Höhe darf negativ sein.");
        if (Standardhoehe <= 0) f.Add("blitz.standardhoehe muss größer als 0 sein.");
        if (Reparatur < 0 || WohnhausReparatur < 0) f.Add("blitz.reparatur und blitz.wohnhausReparatur dürfen nicht negativ sein.");
        if (InstandMinuten < 0) f.Add("blitz.instandMinuten darf nicht negativ sein.");
        foreach (var (name, paar) in new[] { ("reparaturAnteil", ReparaturAnteil), ("lagerAnteil", LagerAnteil), ("wohnhausAnteil", WohnhausAnteil) })
            if (paar is not { Length: 2 } p || p[0] < 0 || p[1] < p[0] || (name == "lagerAnteil" && p[1] > 1))
                f.Add($"blitz.{name} braucht [leicht, schwer] mit 0 ≤ leicht ≤ schwer{(name == "lagerAnteil" ? " ≤ 1" : "")}.");
        if (WaechterFaktor is <= 0 or > 1) f.Add("blitz.waechterFaktor muss größer als 0 und höchstens 1 sein.");
        Sprung?.Pruefen(f);
    }
}

/// <summary>
/// Das Feuer springt auf ein Nachbarhaus über (Abschnitt <c>blitz.sprung</c>; Nutzer 3.11.2026, zurückhaltende Startwerte). Höchstens ein Sprung:
/// Ein übergesprungenes Feuer springt nicht weiter. Der Zufall kommt aus der Zufallsquelle des Gewitters, hinter den Würfen für Chance, Ziel und Dauer.
/// </summary>
public sealed class BlitzSprungDaten
{
    /// <summary>Nachbar ist, wessen Grundfläche höchstens so viele Meter von der des brennenden Gebäudes entfernt ist (Kante zu Kante, achsparallel).</summary>
    public double Abstand { get; init; } = 6;

    /// <summary>Chance je volle Spielstunde des Feuers und je Nachbar (gewürfelt zur Mitte jeder Stunde). Wächter und Löschfass des Nachbarn senken sie.</summary>
    public double ChanceJeStunde { get; init; } = 0.15;

    /// <summary>Hat der Nachbar einen Wächter bzw. Hauswächter: Chance mal diesem Faktor.</summary>
    public double WaechterFaktor { get; init; } = 0.5;

    /// <summary>Das übergesprungene Feuer brennt so viel kürzer: gewürfelte Dauer des ersten Feuers mal diesem Faktor.</summary>
    public double DauerFaktor { get; init; } = 0.5;

    /// <summary>Das übergesprungene Feuer ist so viel leichter: Schwere des ersten Feuers mal diesem Faktor.</summary>
    public double SchwereFaktor { get; init; } = 0.5;

    public void Pruefen(List<string> f)
    {
        if (Abstand < 0) f.Add("blitz.sprung.abstand darf nicht negativ sein.");
        if (ChanceJeStunde is < 0 or > 1 || WaechterFaktor is < 0 or > 1) f.Add("blitz.sprung.chanceJeStunde und waechterFaktor liegen zwischen 0 und 1.");
        if (DauerFaktor is <= 0 or > 1 || SchwereFaktor is < 0 or > 1) f.Add("blitz.sprung.dauerFaktor (größer als 0) und schwereFaktor liegen zwischen 0 und 1.");
    }
}
