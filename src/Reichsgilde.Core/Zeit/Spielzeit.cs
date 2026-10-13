namespace Reichsgilde.Core.Zeit;

/// <summary>
/// Die Spielzeit: WELCHER Zeitpunkt ist gerade in der Spielwelt?
///
/// Grundregel (wie im Original „Die Gilde"):
///   1 Runde = 1 Spieltag = 1 Spieljahr.
///
/// Die Zeit zählt in ganzen „Spielminuten" (Ticks). Ein Spieltag hat 1440 Minuten
/// und beginnt um 06:00 Uhr mit der Zunftglocke. Nach 1440 Ticks ist das Jahr vorbei.
///
/// Wichtig: Diese Klasse weiß nichts über Echtzeit oder Spielgeschwindigkeit.
/// Das erledigt der <see cref="Taktgeber"/>. So läuft die Simulation immer gleich ab –
/// egal ob eine Runde 5 oder 30 Minuten dauert. (Das brauchen wir später für den Multiplayer.)
/// </summary>
public sealed class Spielzeit
{
    public const int MinutenProTag = 24 * 60;
    public const int StartJahr = 1400;

    /// <summary>Uhrzeit, zu der jeder Spieltag beginnt (in Minuten nach Mitternacht).</summary>
    public const int TagesbeginnMinute = 6 * 60;

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

    /// <summary>Wie viele Minuten des aktuellen Spieltags schon vergangen sind (0 bis 1439).</summary>
    public int MinuteImTag => (int)(VergangeneMinuten % MinutenProTag);

    /// <summary>Anteil des Spieltags, der schon vorbei ist (0.0 bis knapp 1.0).</summary>
    public double TagesFortschritt => (double)MinuteImTag / MinutenProTag;

    /// <summary>Uhrzeit in Minuten nach Mitternacht (0 bis 1439).</summary>
    public int UhrzeitInMinuten => (TagesbeginnMinute + MinuteImTag) % MinutenProTag;

    /// <summary>Uhrzeit als Text, z. B. "06:00".</summary>
    public string UhrzeitText => $"{UhrzeitInMinuten / 60:00}:{UhrzeitInMinuten % 60:00}";

    /// <summary>In welchem Abschnitt des Tages befinden wir uns?</summary>
    public Tagesphase Tagesphase => MinuteImTag switch
    {
        < 5 * 60 => Tagesphase.Zunftglocke,   // 06:00–11:00
        < 10 * 60 => Tagesphase.HoherMittag,  // 11:00–16:00
        < 15 * 60 => Tagesphase.Vesper,       // 16:00–21:00
        _ => Tagesphase.DunkleNacht,          // 21:00–06:00
    };

    /// <summary>Datum für die Anzeige, z. B. "Frühling 1400".</summary>
    public string DatumText => $"{KalenderTexte.Name(Jahreszeit)} {Jahr}";

    /// <summary>Stellt die Uhr um genau eine Spielminute weiter.</summary>
    public void Weiter() => VergangeneMinuten++;
}
