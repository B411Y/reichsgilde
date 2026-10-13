namespace Reichsgilde.Core.Partie;

/// <summary>
/// Was der Schiedsrichter wissen muss, um über das Spielende zu entscheiden.
/// (Ein „record struct" ist ein kleines, unveränderliches Datenpaket.)
/// </summary>
/// <param name="Jahr">Das aktuelle Spieljahr.</param>
/// <param name="LebendeDynastien">Wie viele Dynastien noch existieren.</param>
/// <param name="HoechstesVermoegen">Das größte Gesamtvermögen aller Dynastien.</param>
public readonly record struct Spielstand(int Jahr, int LebendeDynastien, long HoechstesVermoegen);

/// <summary>
/// Entscheidet anhand der gewählten Siegbedingung, ob die Partie vorbei ist.
/// Wird nach jeder Jahresabrechnung aufgerufen.
/// </summary>
public static class Spielende
{
    public static bool IstErreicht(Partieeinstellungen einstellungen, Spielstand stand) =>
        einstellungen.Siegbedingung switch
        {
            Siegbedingung.Endjahr => stand.Jahr >= einstellungen.Endjahr,
            Siegbedingung.Zielvermoegen => stand.HoechstesVermoegen >= einstellungen.Zielvermoegen,
            Siegbedingung.LetzteDynastie => stand.LebendeDynastien <= 1,
            Siegbedingung.Endlos => false,
            _ => false,
        };
}
