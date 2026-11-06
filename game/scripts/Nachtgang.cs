using Reichsgilde.Core.Gauner;

namespace Reichsgilde;

/// <summary>
/// Der Ablauf eines nächtlichen Vorfalls in der Darstellung (Wachen und Diebe, Paket P4: Einbruch und Banden bei Nacht). Nur Rechnung ohne Godot, damit ein
/// Test sie mitübersetzen kann (<c>NachtgangTests</c>); die Stadtansicht (Stadtansicht.Nachtdiebe.cs) setzt Figuren und Wege danach.
///
/// Der Kern meldet einen Versuch zur vollen Stunde (20, 21, 22, 23 Uhr) in <see cref="Vorfallbuch"/>; <see cref="Vorfall.Minute"/> ist dieser Zeitpunkt. Die
/// Darstellung zeigt die Tat <i>danach</i> – sie kennt das Ziel erst, wenn der Kern gewürfelt hat – als Funktion der Spielzeit seit dieser Minute
/// (Pause, Sprung und Laden stimmen immer). Ablauf eines Täters (<c>seit</c> = Minuten seit dem Vorfall, alle Längen in Spielminuten):
///   • <see cref="Abschnitt.Anschleichen"/>: auf dem dunkelsten Weg zum Ziel, gebückt;
///   • <see cref="Abschnitt.Warten"/>: <see cref="WartenDauer"/> Minuten kniend im Schatten, ein paar Meter vor der Tür;
///   • <see cref="Abschnitt.Naehern"/>: die letzten Schritte zur Tür;
///   • <see cref="Abschnitt.Arbeiten"/>: <see cref="ArbeitDauer"/> Minuten an Tür oder Fenster;
///   • <see cref="Abschnitt.Drinnen"/>: <see cref="DrinnenDauer"/> Minuten im Haus (nur bei einem Einbruch, der gelang; sonst entfällt der Abschnitt);
///   • <see cref="Abschnitt.Flucht"/>: zurück ins Dunkel – bei Beute mit dem Sack, bei einem Zeugen eilig, sonst leise;
///   • danach <see cref="Abschnitt.Nachher"/>: weg.
/// Banden haben 2 bis 3 Mitglieder, die um <see cref="BandeAbstand"/> Minuten versetzt gehen.
/// </summary>
public static class Nachtgang
{
    /// <summary>So lange wartet der Täter im Schatten, bevor er an die Tür geht.</summary>
    public const double WartenDauer = 4;
    /// <summary>So lange arbeitet er an Tür oder Fenster.</summary>
    public const double ArbeitDauer = 8;
    /// <summary>So lange ist er im Haus, wenn er einsteigt.</summary>
    public const double DrinnenDauer = 8;

    /// <summary>Länge (Meter) des Anschleichens bis zum Wartepunkt, der Weg vom Wartepunkt zur Tür und die Länge der Flucht.</summary>
    public const float AnschleichMeter = 14, NaeherMeter = 3.5f, FluchtMeter = 18;

    /// <summary>Mitglieder einer Bande gehen so viele Spielminuten versetzt hintereinander.</summary>
    public const double BandeAbstand = 1.0;

    /// <summary>Wo im Ablauf die Zeit liegt.</summary>
    public enum Abschnitt { Vorher, Anschleichen, Warten, Naehern, Arbeiten, Drinnen, Flucht, Nachher }

    /// <summary>Die Dauern eines Täters: Anschleichen, Näherkommen und Flucht aus Weglänge und Tempo, dazu ob er einsteigt.</summary>
    public readonly record struct Plan(double Anschleichen, double Naehern, double Flucht, bool Betritt)
    {
        public double Drinnen => Betritt ? DrinnenDauer : 0;
        /// <summary>Alles zusammen (Minuten seit dem Vorfall bis zum Verschwinden).</summary>
        public double Gesamt => Anschleichen + WartenDauer + Naehern + ArbeitDauer + Drinnen + Flucht;
    }

    /// <summary>Wird ein Vorfall dieser Art nachts gezeigt? Taschendiebe (Paket P3) und der Überfall im Umland nicht.</summary>
    public static bool Zeigbar(Vorfallart art) => art is not (Vorfallart.Taschendiebstahl or Vorfallart.Ueberfall);

    /// <summary>Steigt der Täter ins Haus? Nur beim Einbruch und der Entführung, die der Kern als gelungen meldet – sonst scheitert er an der Tür.</summary>
    public static bool Betritt(Vorfallart art, bool erfolg) => erfolg && art is Vorfallart.Einbruch or Vorfallart.Hauseinbruch or Vorfallart.Entfuehrung;

    /// <summary>Trägt der Täter einen Sack heraus? Nur wenn ein Einbruch Beute brachte.</summary>
    public static bool TraegtSack(Vorfallart art, bool erfolg, double wert) => erfolg && wert > 0 && art is Vorfallart.Einbruch or Vorfallart.Hauseinbruch;

    /// <summary>Wie viele gehen mit: eigene Diebe einer, eine Bande 2 oder 3 (fest aus Minute und Ziel, ohne Zufall).</summary>
    public static int Anzahl(bool bande, long minute, string ziel) => bande ? 2 + (int)(Taschendiebgang.Streu(ziel.Length + ziel.Sum(c => c), minute, 11) % 2) : 1;

    /// <summary>Der Abschnitt zur Zeit <paramref name="seit"/> (Minuten seit dem Vorfall) und wie weit er ist (<paramref name="anteil"/> 0 bis 1).</summary>
    public static Abschnitt Bei(double seit, Plan plan, out double anteil)
    {
        anteil = 0;
        if (seit < 0) return Abschnitt.Vorher;
        var t = seit;
        if (Teil(ref t, plan.Anschleichen, out anteil)) return Abschnitt.Anschleichen;
        if (Teil(ref t, WartenDauer, out anteil)) return Abschnitt.Warten;
        if (Teil(ref t, plan.Naehern, out anteil)) return Abschnitt.Naehern;
        if (Teil(ref t, ArbeitDauer, out anteil)) return Abschnitt.Arbeiten;
        if (plan.Betritt && Teil(ref t, DrinnenDauer, out anteil)) return Abschnitt.Drinnen;
        if (Teil(ref t, plan.Flucht, out anteil)) return Abschnitt.Flucht;
        anteil = 1;
        return Abschnitt.Nachher;
    }

    private static bool Teil(ref double t, double dauer, out double anteil)
    {
        if (t < dauer) { anteil = dauer <= 0 ? 1 : t / dauer; return true; }
        t -= dauer;
        anteil = 1;
        return false;
    }

    /// <summary>Wie lange ein Weg der Länge <paramref name="meter"/> bei <paramref name="meterJeMinute"/> dauert (mindestens 1 Minute, damit nie durch null geteilt wird).</summary>
    public static double Dauer(float meter, double meterJeMinute) => System.Math.Max(1, meter / System.Math.Max(0.05, meterJeMinute));

    /// <summary>Welche Arbeit an der Tür: Dietrich beim Einbruch und der Entführung, Knien beim Legen von Feuer und Gift, Lauschen beim Auskundschaften.</summary>
    public static string Arbeit(Vorfallart art) => art switch
    {
        Vorfallart.Einbruch or Vorfallart.Hauseinbruch or Vorfallart.Entfuehrung => "Knacken",
        Vorfallart.Auskundschaften => "Stehen",
        _ => "Knien",
    };

    /// <summary>Was der Täter gerade tut (für die Auswahltafel).</summary>
    public static string Zustand(Abschnitt a, Vorfallart art, bool betritt, bool sack, bool zeuge) => a switch
    {
        Abschnitt.Anschleichen => "schleicht sich im Dunkeln heran",
        Abschnitt.Warten => "wartet im Schatten und lauscht",
        Abschnitt.Naehern => "huscht zur Tür",
        Abschnitt.Arbeiten => art switch
        {
            Vorfallart.Auskundschaften => "lauscht an Tür und Fenster",
            Vorfallart.Brandanschlag or Vorfallart.Hausbrand => "legt Feuer an die Tür",
            Vorfallart.Giftanschlag => "schleicht zur Küche",
            _ => "macht sich an Tür und Fenster zu schaffen",
        },
        Abschnitt.Drinnen => "ist im Haus",
        Abschnitt.Flucht => zeuge ? "flieht eilig – ein Zeuge hat Alarm geschlagen" : sack ? "flieht mit dem Sack über der Schulter" : betritt ? "schleicht mit leeren Händen davon" : "zieht unverrichteter Dinge ab",
        _ => "",
    };
}
