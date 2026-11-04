using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Wahl „Eigener Mauszeiger“ (Lederhandschuh nach Stand, <see cref="Mauszeiger"/>) – ein Teil der <c>partial class Fenster</c>.
/// Standard an; aus = der Zeiger des Betriebssystems. Gespeichert in <c>user://einstellungen.cfg</c>, Abschnitt „anzeige“, Schlüssel „eigener_zeiger“.
/// </summary>
public partial class Fenster
{
    /// <summary>Der eigene Mauszeiger ist an (Standard).</summary>
    public static bool EigenerZeigerAn { get; private set; } = true;

    public static void EigenerZeigerSetzen(bool an, bool speichern = true)
    {
        EigenerZeigerAn = an;
        if (speichern) Speichern("eigener_zeiger", an);
        Mauszeiger.Auffrischen();
    }

    private static void ZeigerLaden(ConfigFile datei, bool geladen) =>
        EigenerZeigerAn = !geladen || datei.GetValue("anzeige", "eigener_zeiger", true).AsBool();

    /// <summary>Startoptionen <c>--zeiger=0|1</c> (nur für diesen Start) und <c>--zeigerstufe=1..6</c> (Handstufe fest, statt des Standes des Hauses).</summary>
    private static void ZeigerStartoptionen(Dictionary<string, string> optionen)
    {
        if (optionen.TryGetValue("zeiger", out var wert))
            EigenerZeigerSetzen(!(wert == "0" || wert.Equals("Aus", StringComparison.OrdinalIgnoreCase)), speichern: false);
        if (optionen.TryGetValue("zeigerstufe", out var stufe) && int.TryParse(stufe, out var nummer))
            Mauszeiger.StufeFest = Math.Clamp(nummer, 1, Mauszeiger.Stufen);
    }
}
