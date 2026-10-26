namespace Reichsgilde.Core.Zeit;

/// <summary>
/// Die Spielzeit: WELCHER Zeitpunkt ist gerade in der Spielwelt?
///
/// Grundregel (wie im Original „Die Gilde"):
///   1 Runde = 1 Spieltag = 1 Spieljahr.
///
/// Die Zeit zählt in ganzen „Spielminuten" (Ticks). Gespielt wird von 04:00 Uhr (Zunftglocke) bis 23:00 Uhr
/// – 19 Stunden, 1140 Ticks (bis Datenversion 55: 05–22 Uhr, 1020 Ticks; Entscheidung des Nutzers vom 26.10.2026).
/// Dann ist das Jahr vorbei, die Jahresrechnung kommt, und das neue Jahr beginnt wieder um 04:00 Uhr.
/// Die Nachtstunden dazwischen werden nicht gespielt.
///
/// Wichtig: Diese Klasse weiß nichts über Echtzeit oder Spielgeschwindigkeit.
/// Das erledigt der <see cref="Taktgeber"/>. So läuft die Simulation immer gleich ab –
/// egal ob eine Runde 5 oder 30 Minuten dauert. (Das brauchen wir später für den Multiplayer.)
/// </summary>
public sealed class Spielzeit
{
    public const int StartJahr = 1400;

    /// <summary>Uhrzeit, zu der jeder Spieltag beginnt (in Minuten nach Mitternacht): 04:00.</summary>
    public const int TagesbeginnMinute = 4 * 60;

    /// <summary>Uhrzeit, zu der der Spieltag endet und die Jahresrechnung kommt: 23:00.</summary>
    public const int TagesendeMinute = 23 * 60;

    /// <summary>Gespielte Minuten je Tag (= Jahr): 1140.</summary>
    public const int MinutenProTag = TagesendeMinute - TagesbeginnMinute;

    /// <summary>Gespielte Stunden je Tag: 19.</summary>
    public const int StundenProTag = MinutenProTag / 60;

    /// <summary>Wie viele Spielminuten seit Spielbeginn vergangen sind.</summary>
    public long VergangeneMinuten { get; private set; }

    public Spielzeit(long vergangeneMinuten = 0)
    {
        if (vergangeneMinuten < 0)
            throw new ArgumentOutOfRangeException(nameof(vergangeneMinuten), "Die Zeit kann nicht negativ sein.");
        VergangeneMinuten = vergangeneMinuten;
    }

    /// <summary>Das aktuelle Spieljahr, z. B. 1400.</summary>
    public int Jahr => StartJahr + (int)(VergangeneMinuten / MinutenProTag);

    /// <summary>Die laufende Runde, beginnend bei 1.</summary>
    public int Runde => Jahr - StartJahr + 1;

    /// <summary>Jahreszeit dieses Jahres (wechselt jedes Jahr).</summary>
    public Jahreszeit Jahreszeit => (Jahreszeit)((Jahr - StartJahr) % 4);

    /// <summary>Wie viele Minuten des aktuellen Spieltags schon vergangen sind (0 bis 1139).</summary>
    public int MinuteImTag => (int)(VergangeneMinuten % MinutenProTag);

    /// <summary>Anteil des Spieltags, der schon vorbei ist (0.0 bis knapp 1.0).</summary>
    public double TagesFortschritt => (double)MinuteImTag / MinutenProTag;

    /// <summary>Uhrzeit in Minuten nach Mitternacht (240 = 04:00 bis 1379 = 22:59).</summary>
    public int UhrzeitInMinuten => TagesbeginnMinute + MinuteImTag;

    /// <summary>
    /// Die volle Stunde, die gerade zu Ende ging (Uhrzeit 4–22). In der ersten Minute eines Jahres ist das
    /// die letzte Stunde des Vorjahres (22–23 Uhr) – die Jahresrechnung rechnet sie noch ab.
    /// </summary>
    public int StundeDieEndete => MinuteImTag == 0 ? TagesendeMinute / 60 - 1 : UhrzeitInMinuten / 60 - 1;

    /// <summary>Uhrzeit als Text, z. B. "04:00".</summary>
    public string UhrzeitText => $"{UhrzeitInMinuten / 60:00}:{UhrzeitInMinuten % 60:00}";

    /// <summary>In welchem Abschnitt des Tages befinden wir uns?</summary>
    public Tagesphase Tagesphase => UhrzeitInMinuten switch
    {
        < 10 * 60 => Tagesphase.Zunftglocke,  // 04:00–10:00
        < 15 * 60 => Tagesphase.HoherMittag,  // 10:00–15:00
        < 20 * 60 => Tagesphase.Vesper,       // 15:00–20:00
        _ => Tagesphase.DunkleNacht,          // 20:00–23:00
    };

    /// <summary>Datum für die Anzeige, z. B. "Frühling 1400".</summary>
    public string DatumText => $"{KalenderTexte.Name(Jahreszeit)} {Jahr}";

    /// <summary>Stellt die Uhr um genau eine Spielminute weiter.</summary>
    public void Weiter() => VergangeneMinuten++;
}
