namespace Reichsgilde;

/// <summary>Welche Runde die Streife geht (Darstellung, Wachen und Diebe P5); siehe Stadtansicht.Streife.cs.</summary>
public enum Streifenart { Markt, Viertel, Mauer }

/// <summary>
/// Die Wahl der Streifenrunde (reine Rechnung ohne Godot, vom Test mitübersetzt: <c>StreifengangTests</c>). Fest aus Wachwechsel (Stunde / 2) und Nummer des
/// Wachmanns, ohne Zufall und ohne Wirkung auf den Kern.
/// </summary>
public static class Streifengang
{
    /// <summary>
    /// Ohne verlegte Streife reihum Markt, Viertel und Mauer (ohne Mauerweg an Stelle der Mauer das Viertel); verlegt der Stadthauptmann die Streife, gehen drei von
    /// vier Wachleuten ins Viertel (das des Hauptmanns) und einer über den Markt.
    /// </summary>
    public static Streifenart Waehlen(int wechsel, int nummer, bool verlegt, bool hatMauerweg)
    {
        var platz = (int)((uint)(wechsel * 5 + nummer) % (verlegt ? 4u : 3u));
        if (verlegt) return platz == 0 ? Streifenart.Markt : Streifenart.Viertel;
        return platz switch { 0 => Streifenart.Markt, 1 => Streifenart.Viertel, _ => hatMauerweg ? Streifenart.Mauer : Streifenart.Viertel };
    }
}
