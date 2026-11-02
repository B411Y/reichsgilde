namespace Reichsgilde.Core.Gefahren;

/// <summary>
/// Blitzschlag im Gewitter (Abschnitt <c>blitz</c> in wirtschaft.json, ab Datenversion 69; Wunsch des Nutzers vom 2.11.2026). Fehlt der Abschnitt,
/// schlägt kein Blitz ein. Der Schaden ist ein Platzhalter, bis das richtige Schadensmodell kommt: Eine Werkstatt steht still wie nach einer
/// Sabotage, ein Wohnhaus verliert bis zur Instandsetzung sein Ansehen wie nach einer Brandstiftung.
/// </summary>
public sealed class BlitzDaten
{
    /// <summary>Chance je Gewitter, dass der Blitz in ein Gebäude der Stadt einschlägt (0,25: etwa jedes vierte Gewitter).</summary>
    public double Chance { get; init; } = 0.25;

    /// <summary>So lange brennt es (Spielminuten, gewürfelt von … bis); danach ist das Feuer gelöscht oder das Haus ausgebrannt.</summary>
    public int[] DauerMinuten { get; init; } = { 60, 180 };

    /// <summary>
    /// Höhe der Gebäude in Metern nach Kennung im Gebäudekatalog (Werkstätten und Wohnhäuser nach ihrem Modell: „schmied“, „stadtpalais“ …). Hohe
    /// Gebäude und Gebäude auf Anhöhen trifft es öfter: Gewicht = (Höhe + Höhe des Bodens über dem Mittel der Stadt)². Kirchtürme haben um 1400
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

    /// <summary>Werkstatt: Reparaturkosten für den Besitzer (0 = keine; Platzhalter, eine kleine Summe kann später kommen).</summary>
    public double Reparatur { get; init; }

    /// <summary>Wohnhaus: So lange (Spielminuten nach dem Ende des Feuers) wird instand gesetzt; bis dahin fehlt das Ansehen des Hauses.</summary>
    public int InstandMinuten { get; init; } = Zeit.Spielzeit.MinutenProTag;

    /// <summary>Wohnhaus: Reparaturkosten für den Bewohner (0 = keine).</summary>
    public double WohnhausReparatur { get; init; }

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
    }
}
