namespace Reichsgilde.Core;

/// <summary>
/// Die Völker der Alternativwelt (siehe Design-Dossier, Abschnitt 2).
/// Menschen, Zwerge, Elben und Halblinge sind Bürger des Reiches;
/// Orks und Oger gelten vor Kaiser und Kirche als Bestien.
/// Banditen und Räuber kann es in jedem Volk geben – auch in gemischten Banden.
/// </summary>
public enum Volk
{
    Mensch,
    Zwerg,
    Elb,
    Halbling,
    Ork,
    Oger,
}

public static class VolkTexte
{
    public static string Einzahl(this Volk volk) => volk switch
    {
        Volk.Mensch => "Mensch",
        Volk.Zwerg => "Zwerg",
        Volk.Elb => "Elb",
        Volk.Halbling => "Halbling",
        Volk.Ork => "Ork",
        Volk.Oger => "Oger",
        _ => volk.ToString(),
    };

    public static string Mehrzahl(this Volk volk) => volk switch
    {
        Volk.Mensch => "Menschen",
        Volk.Zwerg => "Zwerge",
        Volk.Elb => "Elben",
        Volk.Halbling => "Halblinge",
        Volk.Ork => "Orks",
        Volk.Oger => "Oger",
        _ => volk.ToString(),
    };

    /// <summary>„1 Halbling", „2 Menschen".</summary>
    public static string MitAnzahl(this Volk volk, int anzahl) =>
        $"{anzahl} {(anzahl == 1 ? volk.Einzahl() : volk.Mehrzahl())}";
}
