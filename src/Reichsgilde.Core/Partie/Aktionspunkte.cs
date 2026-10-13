namespace Reichsgilde.Core.Partie;

/// <summary>
/// Der Aktionspunkte-Vorrat eines Charakters (wie im Original „Die Gilde").
///
///   • Zu Beginn jeder Runde kommen neue AP dazu (Standard: 4).
///   • Nicht verbrauchte AP bleiben erhalten und werden in die nächste Runde übertragen.
///   • Maßnahmen wie Training, Intrigen oder Privilegien kosten AP.
/// </summary>
public sealed class Aktionspunkte
{
    public int Vorrat { get; private set; }

    public Aktionspunkte(int startVorrat = 0)
    {
        if (startVorrat < 0)
            throw new ArgumentOutOfRangeException(nameof(startVorrat));
        Vorrat = startVorrat;
    }

    /// <summary>Neue Runde: Die AP pro Runde kommen zum Rest aus der Vorrunde dazu.</summary>
    /// <param name="proRunde">Grundwert aus den Partie-Einstellungen.</param>
    /// <param name="bonus">Zusätzliche AP, z. B. durch den 6. Meistergrad oder Gegenstände.</param>
    public void NeueRunde(int proRunde, int bonus = 0) => Vorrat += Math.Max(0, proRunde + bonus);

    public bool Reicht(int kosten) => kosten >= 0 && Vorrat >= kosten;

    /// <summary>Gibt AP aus. Liefert false (und zieht nichts ab), wenn der Vorrat nicht reicht.</summary>
    public bool Ausgeben(int kosten)
    {
        if (!Reicht(kosten))
            return false;
        Vorrat -= kosten;
        return true;
    }
}
