namespace Reichsgilde.Core.Zeit;

/// <summary>
/// Die Regel, welche Haltegründe (<see cref="Taktgeber.Anhalten"/>) am Jahresende gelten – in fester Reihenfolge
/// <b>Stimmrunde</b> → <b>Ratssitzung</b> → <b>Jahresbericht</b> → die Zeit läuft im alten Tempo weiter.
/// Nur im Einzelspiel; im Mehrspieler gibt es keinen Haltegrund (die Stimmrunde läuft dort bei laufender Zeit, eine gemeinsame Pause ist nicht gebaut).
/// Die Oberfläche meldet, was offen ist, und gleicht den Takt mit <see cref="Abgleichen"/> ab. Kein Befehl, kein Spielstand, ändert keinen Tick.
/// </summary>
public static class Zeithalt
{
    public const string Stimmrunde = "Stimmrunde", Ratssitzung = "Ratssitzung", Jahresbericht = "Jahresbericht";

    /// <summary>Die Gründe, die jetzt gelten, in der festen Reihenfolge.</summary>
    /// <param name="einzelspiel">Ohne Befehlsverzögerung (nur dann hält die Zeit an).</param>
    /// <param name="stimmrundeOffen">Die Tafel der Stimmrunde ist offen (bis „Fertig“).</param>
    /// <param name="sitzung">Die Szene der Jahresend-Sitzung läuft oder wartet darauf, gezeigt zu werden.</param>
    /// <param name="berichtOffen">Der Jahresbericht ist offen.</param>
    /// <param name="berichtWartet">Der Jahresbericht wartet auf das Ende der Sitzung (dann hält die Sitzung an).</param>
    public static IReadOnlyList<string> Gruende(bool einzelspiel, bool stimmrundeOffen, bool sitzung, bool berichtOffen, bool berichtWartet)
    {
        var gruende = new List<string>();
        if (!einzelspiel) return gruende;
        if (stimmrundeOffen) gruende.Add(Stimmrunde);
        if (sitzung) gruende.Add(Ratssitzung);
        if (berichtOffen || berichtWartet && !sitzung) gruende.Add(Jahresbericht);
        return gruende;
    }

    /// <summary>
    /// Gleicht den Takt ab: erst alle neuen Gründe anhalten (in der Reihenfolge), dann die alten freigeben – so bleibt beim Wechsel
    /// (Sitzung → Jahresbericht) keine Lücke. Liefert die Wechsel („angehalten (…)“ / „frei (…)“) fürs Log.
    /// </summary>
    public static IReadOnlyList<string> Abgleichen(Taktgeber takt, IReadOnlyList<string> gruende)
    {
        var wechsel = new List<string>();
        foreach (var g in gruende)
            if (!takt.Haltegruende.Contains(g))
            {
                takt.Anhalten(g);
                wechsel.Add($"angehalten ({g}) – Haltegründe: {Liste(takt)}");
            }
        foreach (var g in takt.Haltegruende.Where(g => !gruende.Contains(g)).ToList())
        {
            takt.Freigeben(g);
            wechsel.Add($"frei ({g}) – Haltegründe: {Liste(takt)}");
        }
        return wechsel;
    }

    private static string Liste(Taktgeber takt) => takt.IstAngehalten ? string.Join(", ", takt.Haltegruende) : "keine, die Zeit läuft";
}
