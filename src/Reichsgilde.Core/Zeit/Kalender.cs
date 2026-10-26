namespace Reichsgilde.Core.Zeit;

/// <summary>
/// Jede Runde (= ein Spieljahr) bekommt eine Jahreszeit. Sie wechselt von Jahr zu Jahr:
/// Frühling 1400, Sommer 1401, Herbst 1402, Winter 1403, Frühling 1404 ...
/// (Vorbild: „Die Gilde", Handbuch S. 23)
/// </summary>
public enum Jahreszeit
{
    Fruehling,
    Sommer,
    Herbst,
    Winter,
}

/// <summary>
/// Die vier Abschnitte eines Spieltags (siehe Design-Dossier, Abschnitt 5).
/// </summary>
public enum Tagesphase
{
    /// <summary>04:00–10:00 – Betriebe öffnen, Karren werden beladen.</summary>
    Zunftglocke,

    /// <summary>10:00–15:00 – Marktverkehr auf Maximum, Gerichtsverhandlungen.</summary>
    HoherMittag,

    /// <summary>15:00–20:00 – Ratsversammlungen, Ämterwahlen, Schankbetrieb.</summary>
    Vesper,

    /// <summary>20:00–23:00 – Schurkenzüge, Brandstiftung, Ork-Überfälle; danach die Jahresrechnung.</summary>
    DunkleNacht,
}

/// <summary>
/// Deutsche Anzeigenamen für die Aufzählungen. (Bezeichner im Code dürfen
/// keine Umlaute und Leerzeichen haben – die Anzeige schon.)
/// </summary>
public static class KalenderTexte
{
    public static string Name(Jahreszeit jahreszeit) => jahreszeit switch
    {
        Jahreszeit.Fruehling => "Frühling",
        Jahreszeit.Sommer => "Sommer",
        Jahreszeit.Herbst => "Herbst",
        Jahreszeit.Winter => "Winter",
        _ => jahreszeit.ToString(),
    };

    public static string Name(Tagesphase phase) => phase switch
    {
        Tagesphase.Zunftglocke => "Frühe Zunftglocke",
        Tagesphase.HoherMittag => "Hoher Mittag",
        Tagesphase.Vesper => "Vesper & Zwielicht",
        Tagesphase.DunkleNacht => "Dunkle Nacht",
        _ => phase.ToString(),
    };
}
