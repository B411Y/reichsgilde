namespace Reichsgilde.Core.Dynastie;

/// <summary>Ein Eintrag im Stress einer Person, wie ihn der Tooltip zeigt: „1403: Trauer um Ehefrau Agnes +30“.</summary>
/// <param name="Jahr">Das Spieljahr, in dem es geschah.</param>
/// <param name="Wert">Die tatsächliche Änderung (positiv: Quelle, negativ: Ventil, Abklingen, Zusammenbruch).</param>
public sealed record StressEintrag(int Jahr, string Text, double Wert);

/// <summary>
/// Der Stress einer Person (Charaktersystem Teil 3b): der Wert, die letzten Einträge für den Tooltip und die Ventile, die in diesem Jahr
/// schon gewirkt haben. Geändert wird er nur vom <see cref="Familienbuch"/> (Spielereignisse, kein eigener Befehl).
/// </summary>
public sealed class Stresslage
{
    /// <summary>So viele Einträge bleiben (die ältesten fallen weg; am Jahresende alles vor dem letzten Jahr).</summary>
    internal const int HoechstensEintraege = 10;

    private readonly List<StressEintrag> _eintraege = new();
    private readonly List<string> _ventile = new();

    /// <summary>Stress 0 … Höchstwert (100).</summary>
    public double Wert { get; internal set; }

    /// <summary>Die letzten Quellen und Ventile, die älteste zuerst.</summary>
    public IReadOnlyList<StressEintrag> Eintraege => _eintraege;

    /// <summary>Aktivitäten (Kennungen), deren Stress in diesem Jahr schon gewirkt hat – je Art einmal im Jahr.</summary>
    public IReadOnlyList<string> Ventile => _ventile;

    /// <summary>Sterne weniger in jedem Talent aus der Stufe (gesetzt vom Familienbuch, wenn sich der Stress ändert und beim Laden).</summary>
    internal double TalentAbzug { get; set; }

    /// <summary>Nichts gesammelt? (Dann schreibt der Spielstand nichts.)</summary>
    public bool IstLeer => Wert == 0 && _eintraege.Count == 0 && _ventile.Count == 0;

    internal void Eintragen(StressEintrag e)
    {
        _eintraege.Add(e);
        while (_eintraege.Count > HoechstensEintraege) _eintraege.RemoveAt(0);
    }

    internal bool VentilGenutzt(string art) => _ventile.Contains(art);

    internal void VentilMerken(string art)
    {
        if (!_ventile.Contains(art)) _ventile.Add(art);
    }

    /// <summary>Am Jahresende: Die Ventile sind wieder frei, Einträge vor <paramref name="abJahr"/> fallen weg.</summary>
    internal void JahrAbschliessen(int abJahr)
    {
        _ventile.Clear();
        _eintraege.RemoveAll(e => e.Jahr < abJahr);
    }
}
