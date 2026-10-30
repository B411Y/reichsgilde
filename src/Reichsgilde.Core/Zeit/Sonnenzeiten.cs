namespace Reichsgilde.Core.Zeit;

/// <summary>
/// Wann es im Lauf eines Spieltags hell und dunkel wird. Alle Zeiten in Minuten nach Mitternacht.
/// <list type="bullet">
/// <item><see cref="Daemmerung"/>: der Himmel beginnt, sich aufzuhellen.</item>
/// <item><see cref="Aufgang"/>: die Sonne geht auf.</item>
/// <item><see cref="Untergang"/>: die Sonne ist unter dem Horizont.</item>
/// <item><see cref="Dunkel"/>: die Abenddämmerung ist vorbei, es ist Nacht.</item>
/// </list>
/// </summary>
public readonly record struct Sonnenlauf(int Daemmerung, int Aufgang, int Untergang, int Dunkel);

/// <summary>
/// Die Sonnenzeiten je Jahreszeit, nach dem Vorbild von Köln (51° N; Nutzer, 29.10.2026). Die Jahreszeit gilt für das ganze Spieljahr,
/// die Zeiten sind der Stichtag am Beginn der Jahreszeit (Frühling: Ende März, Sommer: Juni, Herbst: Ende September, Winter: Dezember);
/// Übergänge zwischen den Jahreszeiten gibt es im Spiel nicht. Nur Darstellung und Stimmung richten sich danach (Licht, Himmel,
/// Fenster, Nachtlichter) – die Spielregeln (Arbeitszeit, Gauner ab 20 Uhr) bleiben an ihren festen Uhrzeiten.
/// Der Spieltag beginnt um 04:00 Uhr: Im Frühling, Herbst und Winter ist es dann noch Nacht, im Sommer ist die Sonne schon auf.
/// </summary>
public static class Sonnenzeiten
{
    static int Zeit(int stunde, int minute) => stunde * 60 + minute;

    /// <summary>Die Zeiten der Jahreszeit.</summary>
    public static Sonnenlauf Fuer(Jahreszeit jahreszeit) => jahreszeit switch
    {
        Jahreszeit.Sommer => new(Zeit(3, 0), Zeit(3, 50), Zeit(20, 10), Zeit(21, 0)),
        Jahreszeit.Herbst => new(Zeit(5, 10), Zeit(5, 50), Zeit(17, 50), Zeit(18, 30)),
        Jahreszeit.Winter => new(Zeit(7, 25), Zeit(8, 10), Zeit(15, 50), Zeit(16, 35)),
        _ => new(Zeit(5, 15), Zeit(5, 55), Zeit(18, 5), Zeit(18, 45)), // Frühling
    };

    /// <summary>Ist es zu dieser Uhrzeit (Minuten nach Mitternacht) hell, also die Sonne auf? Von <see cref="Sonnenlauf.Aufgang"/> bis <see cref="Sonnenlauf.Untergang"/>.</summary>
    public static bool IstTag(Jahreszeit jahreszeit, int uhrzeitInMinuten)
    {
        var lauf = Fuer(jahreszeit);
        return uhrzeitInMinuten >= lauf.Aufgang && uhrzeitInMinuten < lauf.Untergang;
    }

    /// <summary>Ist es zu dieser Uhrzeit richtig dunkel? Vor der <see cref="Sonnenlauf.Daemmerung"/> und ab <see cref="Sonnenlauf.Dunkel"/>.</summary>
    public static bool IstNacht(Jahreszeit jahreszeit, int uhrzeitInMinuten)
    {
        var lauf = Fuer(jahreszeit);
        return uhrzeitInMinuten < lauf.Daemmerung || uhrzeitInMinuten >= lauf.Dunkel;
    }
}
