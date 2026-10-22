namespace Reichsgilde.Core.Ki;

/// <summary>
/// Werte der KI-Häuser aus dem Abschnitt „ki" in <c>wirtschaft.json</c>. Der Charakter (vorsichtig, ausgewogen, wagemutig)
/// steht weiter im <see cref="KiProfil"/>; hier steht, was man ohne Code nachstellen möchte.
/// </summary>
public sealed class KiDaten
{
    /// <summary>Wann die KI Einrichtung kauft. Fehlt der Teil, kauft sie keine.</summary>
    public KiEinrichtungDaten? Einrichtung { get; init; }

    public void Pruefen(List<string> f) => Einrichtung?.Pruefen(f);
}

/// <summary>
/// Die KI kauft Einrichtung, wenn sich ein Stück schnell bezahlt macht (Entscheidung des Nutzers, Balancing 1, PR #177):
/// zum Jahresende je Betrieb höchstens ein Stück, das mit der kürzesten Amortisation.
/// </summary>
public sealed class KiEinrichtungDaten
{
    /// <summary>So viele Jahre darf es höchstens dauern, bis der Mehrgewinn den Preis bezahlt hat (ausgewogener Charakter).</summary>
    public double HoechstensJahre { get; init; } = 4;

    /// <summary>
    /// So viele Jahre weniger wartet die Vorsichtige, so viele mehr die Wagemutige
    /// (erkannt an <see cref="KiProfil.MaxAmortisationJahre"/> gegenüber dem Standard).
    /// </summary>
    public double CharakterJahre { get; init; } = 0.5;

    /// <summary>Nach dem Kauf bleibt so ein Vielfaches der <see cref="KiProfil.Reserve"/> als Bargeld (für Löhne und Expansion).</summary>
    public double ReserveFaktor { get; init; } = 2;

    public void Pruefen(List<string> f)
    {
        const string p = "ki.einrichtung";
        if (HoechstensJahre <= 0) f.Add($"{p}.hoechstensJahre muss über 0 liegen.");
        if (CharakterJahre < 0 || CharakterJahre >= HoechstensJahre)
            f.Add($"{p}.charakterJahre darf nicht negativ sein und muss unter hoechstensJahre liegen.");
        if (ReserveFaktor < 0) f.Add($"{p}.reserveFaktor darf nicht negativ sein.");
    }
}
