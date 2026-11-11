using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Wahl „Eigener Mauszeiger“ (Lederhandschuh nach Stand, dazu Wache und Dieb zur freien Wahl, <see cref="Mauszeiger"/>) – ein Teil der <c>partial class Fenster</c>.
/// Standard an; aus = der Zeiger des Betriebssystems. Gespeichert in <c>user://einstellungen.cfg</c>, Abschnitt „anzeige“, Schlüssel „eigener_zeiger“; der Handschuh (automatisch nach Stand oder fest 1..8; ein älterer gespeicherter Wert 0..6 bleibt gültig) im Schlüssel „zeiger_stufe“.
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

    /// <summary>Gewählter Handschuh: 0 = nach dem Stand des Hauses (Standard), 1..6 fest, 7 Wache, 8 Dieb (rein kosmetisch; Schlüssel „zeiger_stufe“, nicht im Spielstand).</summary>
    public static int ZeigerStufe { get; private set; }

    public static void ZeigerStufeSetzen(int stufe, bool speichern = true)
    {
        ZeigerStufe = Math.Clamp(stufe, 0, Mauszeiger.Haende);
        if (speichern) Speichern("zeiger_stufe", ZeigerStufe);
        Mauszeiger.Auffrischen();
    }

    /// <summary>Der Zeiger ist ein 3D-Handschuh (<see cref="HandschuhZeiger"/>) statt der 2D-Bilder (Standard an; Schlüssel „handschuh_3d“ im Abschnitt „anzeige“). Bleibt auch bei niedriger Bildrate, solange er eingestellt ist.</summary>
    public static bool Handschuh3dAn { get; private set; } = true;

    public static void Handschuh3dSetzen(bool an, bool speichern = true)
    {
        Handschuh3dAn = an;
        if (speichern) Speichern("handschuh_3d", an);
        Mauszeiger.Auffrischen();
    }

    private static void ZeigerLaden(ConfigFile datei, bool geladen)
    {
        Handschuh3dAn = !geladen || datei.GetValue("anzeige", "handschuh_3d", true).AsBool();
        EigenerZeigerAn = !geladen || datei.GetValue("anzeige", "eigener_zeiger", true).AsBool();
        ZeigerStufe = geladen ? Math.Clamp(datei.GetValue("anzeige", "zeiger_stufe", 0).AsInt32(), 0, Mauszeiger.Haende) : 0;
    }

    /// <summary>Startoptionen <c>--zeiger=0|1</c>, <c>--handschuh=0|1</c> (3D-Handschuh nur für diesen Start aus/an) (nur für diesen Start) und <c>--zeigerstufe=1..8</c> (Handschuh fest, statt des Standes des Hauses; 7 Wache, 8 Dieb).</summary>
    private static void ZeigerStartoptionen(Dictionary<string, string> optionen)
    {
        if (optionen.TryGetValue("zeiger", out var wert))
            EigenerZeigerSetzen(!(wert == "0" || wert.Equals("Aus", StringComparison.OrdinalIgnoreCase)), speichern: false);
        if (optionen.TryGetValue("handschuh", out var drei))
            Handschuh3dSetzen(!(drei == "0" || drei.Equals("Aus", StringComparison.OrdinalIgnoreCase)), speichern: false);
        if (optionen.TryGetValue("zeigerstufe", out var stufe) && int.TryParse(stufe, out var nummer))
            Mauszeiger.StufeFest = Math.Clamp(nummer, 1, Mauszeiger.Haende);
    }
}
