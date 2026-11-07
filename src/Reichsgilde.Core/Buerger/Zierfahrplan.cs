using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Buerger;

/// <summary>Wo das Zierschiff gerade ist: fern (nicht zu sehen), auf der Anfahrt, am Steg oder auf der Abfahrt.</summary>
public enum Besuchsphase { Fern, Anfahrt, Liegen, Abfahrt }

/// <summary>
/// Ein Besuch des Zierschiffs zu einer Minute: Phase und Fortschritt darin (0…1), Route der Anfahrt und der Abfahrt (flussauf oder nicht),
/// die Minute der Ankunft am Steg (<see cref="LiegeBeginn"/>, seit Spielbeginn) und das Fenster des Fahrplans.
/// </summary>
public readonly record struct Zierbesuch(Besuchsphase Phase, double Anteil, bool VonFlussauf, bool NachFlussauf, double LiegeBeginn, long Fenster)
{
    /// <summary>Ein Niederländer (gerade Fenster, wird entladen) oder ein Oberländer (ungerade Fenster, lädt vom Stapel auf)?</summary>
    public bool Nieder => Zierfahrplan.IstNieder(Fenster);
}

/// <summary>
/// Der Fahrplan des Zierschiffs (Hafen ruht, PR #293; Kran #327, Stapelrecht #332), als reine Funktion der Spielminute – ohne Zufall,
/// ohne Spielstand, auf jedem Rechner gleich. Bis PR „Hafenarbeit“ rechnete ihn nur die Darstellung (<c>Stadtansicht.Zierschiff.cs</c>);
/// jetzt steht er hier, damit die Hafenarbeit der Tagelöhner (<see cref="Hafenarbeit"/>) und das Bild dieselben Zeiten haben.
///
/// Jedes Spieljahr (ein Spieltag, 04:00–23:00) hat zwei Fenster zu je 570 Minuten. Im Fenster legt das Schiff 70–130 Minuten nach dem
/// Fensteranfang an und liegt 60–150 Minuten; Ankunft, Liegezeit und Seiten folgen aus einer Streuzahl der Fensternummer.
/// Gerade Fenster (morgens, Anlegen 05:10–06:10) kommt ein Niederländer, ungerade (nachmittags, 14:40–15:40) ein Oberländer.
/// </summary>
public static class Zierfahrplan
{
    /// <summary>Ein Fenster des Fahrplans in Spielminuten: zwei je Spieljahr.</summary>
    public const int Fenster = Spielzeit.MinutenProTag / 2;

    /// <summary>Anteile der Liegezeit: Der Kran entlädt den Niederländer von <see cref="EntladeAb"/> bis <see cref="NiederEntladeBis"/>, belädt den Oberländer von <see cref="OberLadeAb"/> bis <see cref="OberLadeBis"/>.</summary>
    public const double EntladeAb = 0.02, NiederEntladeBis = 0.88, OberLadeAb = 0.08, OberLadeBis = 0.90;

    /// <summary>
    /// Wie viele Stücke ein Hub des Krans hebt (ein Bündel am Haken, Darstellung: <c>Stadtansicht.Kranladen.cs</c>): Der Kran schwenkt deutlich langsamer als bei
    /// einem Stück je Hub, also hat er weniger Hübe in derselben Liegezeit. Alle Stücke eines Bündels werden zugleich auf den Stapel gesetzt bzw. gehoben.
    /// </summary>
    public const int HubGroesse = 3;

    /// <summary>
    /// Der erste Hub beim Entladen dauert nur so viel mal einen der übrigen: Ab dem ersten Absetzen tragen die Träger, und bei nur zwei Trägern sind sie sonst erst
    /// gut eine Stunde nach dem Ablegen fertig (mit 0,5 bleibt der Nachlauf unter etwa 55 Minuten).
    /// </summary>
    public const double ErsterHubGewicht = 0.5;

    /// <summary>
    /// Anteil der Liegezeit, in dem der Hub <paramref name="hub"/> (0 …) von <paramref name="huebe"/> läuft: Entladen von <see cref="EntladeAb"/> bis <see cref="NiederEntladeBis"/>
    /// (der erste Hub kürzer, <see cref="ErsterHubGewicht"/>), Beladen von <see cref="OberLadeAb"/> bis <see cref="OberLadeBis"/> in gleich langen Hüben. Der Kran hebt das Bündel bei
    /// <c>Von + KranHebtBei · (Bis − Von)</c> und setzt es bei <c>Bis</c> ab.
    /// </summary>
    public static (double Von, double Bis) HubAnteil(bool entladen, int hub, int huebe)
    {
        var von = entladen ? EntladeAb : OberLadeAb;
        var bis = entladen ? NiederEntladeBis : OberLadeBis;
        var erster = entladen ? ErsterHubGewicht : 1.0;
        var einheit = (bis - von) / (huebe - 1 + erster);
        double Start(int h) => h <= 0 ? von : h >= huebe ? bis : von + einheit * (erster + h - 1);
        return (Start(hub), Start(hub + 1));
    }

    /// <summary>Wie lange die Fahrt zwischen Kartenrand und Kai dauert (Minuten): Routenlänge / 10 m, nicht unter 20 und nicht über 70.</summary>
    public static int FahrtMinuten(double routenLaenge) => (int)Math.Clamp(routenLaenge / 10, 20, 70);

    /// <summary>Gerade Fenster der Niederländer (Stapelrecht, Köln 1259: er wird entladen, die Ware kommt auf den Stapel), ungerade der Oberländer.</summary>
    public static bool IstNieder(long fenster) => fenster % 2 == 0;

    /// <summary>Ankunft (Minute im Fenster, 70–130), Liegezeit (60–150 min) und die Seiten von Anfahrt und Abfahrt im Fenster <paramref name="fenster"/>.</summary>
    public static (int Ankunft, int Liegen, bool VonFlussauf, bool NachFlussauf) Streuung(long fenster)
    {
        unchecked
        {
            // Streuzahl aus der Nummer des Fensters (Jahr * 2 + Hälfte): gleiche Minute, gleiche Zahlen
            var z = (uint)(fenster * 2654435761L + 40503) ^ 0x9E3779B9u;
            z ^= z >> 15; z *= 0x2C1B3C6Du; z ^= z >> 12; z *= 0x297A2D39u; z ^= z >> 15;
            return (70 + (int)(z % 61), 60 + (int)((z >> 8) % 91), (z & 0x10000) != 0, (z & 0x20000) != 0);
        }
    }

    /// <summary>Erste Minute (seit Spielbeginn) des Fensters.</summary>
    public static long Anfang(long fenster) => fenster * Fenster;

    /// <summary>
    /// Wo steht das Zierschiff zur Minute <paramref name="minute"/> (seit Spielbeginn, mit Bruchteil), wenn die Fahrt zwischen Kartenrand und Kai
    /// <paramref name="fahrt"/> Minuten dauert? Rein aus den Argumenten berechnet.
    /// </summary>
    public static Zierbesuch Plan(double minute, int fahrt)
    {
        var fenster = (long)Math.Floor(minute / Fenster);
        var im = minute - fenster * Fenster;
        var (ankunft, liegen, vonAuf, nachAuf) = Streuung(fenster);
        var anfang = ankunft - fahrt; // die Anfahrt beginnt frühestens 0 min nach dem Fensteranfang
        var ablegen = ankunft + liegen;
        if (im < anfang || im >= ablegen + fahrt) return new(Besuchsphase.Fern, 0, vonAuf, nachAuf, 0, fenster);
        var besuch = (double)(fenster * Fenster + ankunft);
        if (im < ankunft) return new(Besuchsphase.Anfahrt, (im - anfang) / fahrt, vonAuf, nachAuf, besuch, fenster);
        if (im < ablegen) return new(Besuchsphase.Liegen, (im - ankunft) / liegen, vonAuf, nachAuf, besuch, fenster);
        return new(Besuchsphase.Abfahrt, (im - ablegen) / fahrt, vonAuf, nachAuf, besuch, fenster);
    }
}
