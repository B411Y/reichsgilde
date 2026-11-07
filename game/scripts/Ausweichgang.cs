namespace Reichsgilde;

/// <summary>
/// Wie ein Dieb der Streife ausweicht (Darstellung, nur Bild; Wachen und Diebe, Nutzer 07.11.2026). Nur Rechnung ohne Godot, damit ein Test sie
/// mitübersetzen kann (<c>AusweichgangTests</c>); die Stadtansicht (Stadtansicht.Ausweichen.cs) legt Wege und Figuren danach.
///
/// Die Szenen der Diebe sind reine Funktionen der Zeit entlang fester Wege. Das Ausweichen bleibt es: Beim Aufbau der Szene wird <b>einmal</b> aus den Runden der
/// Streife (je Schicht fest) ausgerechnet, ob und wann sie dem Dieb auf seinem Weg zwischen zwei Taten näher als <see cref="Radius"/> Meter kommt – dann gibt es
/// ein <see cref="Fenster"/> und eine zweite, kurze Bahn: Der Dieb tritt zu einem Seitenpunkt (Seitengasse bzw. Schatten), bleibt dort, bis die Streife vorbei ist,
/// und ist am Ende des Fensters wieder auf seinem Weg, genau dort, wo er ohne Ausweichen wäre. Je Bild kostet das nichts als die Stelle auf der Bahn.
/// Zur Tat steht der Dieb immer am Ziel; das Ergebnis würfelt allein der Kern.
/// </summary>
public static class Ausweichgang
{
    /// <summary>Kommt die Streife einem Dieb näher als so viele Meter, weicht er aus.</summary>
    public const float Radius = 12f;

    /// <summary>So viele Spielminuten vor der ersten Nähe tritt der Dieb schon beiseite (er sieht die Laterne bzw. den Hellebardenträger kommen) …</summary>
    public const double Anlauf = 3;
    /// <summary>… und so lange nach der letzten Nähe bleibt er noch stehen, ehe er zurück auf seinen Weg geht.</summary>
    public const double Nachlauf = 2;

    /// <summary>Das Ausweichen dauert mindestens so lange (Spielminuten; sonst wäre es ein Zucken) und höchstens so lange (geht die Streife nicht weiter, kehrt er dennoch zurück).</summary>
    public const double MinDauer = 4, MaxDauer = 14;

    /// <summary>Der Anteil der Zeit, die der Dieb am Seitenpunkt steht (der Rest verteilt sich nach den Weglängen auf Hin- und Rückweg).</summary>
    public const double HaltAnteil = 0.3;

    /// <summary>Wo in der Zeit das Ausweichen liegt: von, bis und der Moment der größten Nähe der Streife.</summary>
    public readonly record struct Fenster(double Von, double Bis, double Naechste);

    /// <summary>Wo im Ausweichen die Zeit liegt.</summary>
    public enum Phase { Vorher, Hinaus, Halt, Zurueck, Nachher }

    /// <summary>
    /// Das erste Ausweichen im Zeitraum <paramref name="von"/> bis <paramref name="bis"/> (Spielminuten): Zu jeder ganzen Minute fragt es
    /// <paramref name="abstand"/> nach dem Abstand (Meter) des Diebes zur nächsten Streife (<c>float.MaxValue</c>, wenn keine zu zeigen ist). Kommt eine näher als
    /// <see cref="Radius"/>, beginnt das Fenster <see cref="Anlauf"/> Minuten vorher und endet <see cref="Nachlauf"/> Minuten nach der letzten Nähe des ersten
    /// zusammenhängenden Zeitraums, mindestens <see cref="MinDauer"/>, höchstens <see cref="MaxDauer"/> lang, immer innerhalb des Zeitraums. Null, wenn die Streife
    /// fern bleibt oder der Zeitraum zu kurz ist.
    /// </summary>
    public static Fenster? Finden(double von, double bis, Func<double, float> abstand)
    {
        if (bis - von < MinDauer) return null;
        double? erste = null;
        var letzte = 0.0;
        var naechste = 0.0;
        var best = float.MaxValue;
        for (var t = von; t <= bis + 1e-9; t += 1)
        {
            var d = abstand(t);
            if (d < Radius)
            {
                erste ??= t;
                letzte = t;
                if (d < best) { best = d; naechste = t; }
            }
            else if (erste is not null && t - letzte > 2) break;
        }
        if (erste is not { } anfang) return null;
        var ta = System.Math.Max(von, anfang - Anlauf);
        var te = System.Math.Min(bis, letzte + Nachlauf);
        te = System.Math.Min(te, ta + MaxDauer);
        if (te - ta < MinDauer)
        {
            te = System.Math.Min(bis, ta + MinDauer);
            ta = System.Math.Max(von, te - MinDauer);
        }
        return new Fenster(ta, te, System.Math.Clamp(naechste, ta, te));
    }

    /// <summary>
    /// Der Abschnitt zur Zeit <paramref name="t"/> und wie weit er ist (<paramref name="anteil"/>, 0 bis 1). <paramref name="hinweg"/> ist der Anteil des Hin- am
    /// Gesamtweg (Länge Hinweg / Länge beider Wege): Die Zeit ohne den Halt teilt sich danach auf.
    /// </summary>
    public static Phase Bei(Fenster f, double t, double hinweg, out double anteil)
    {
        anteil = 0;
        if (t < f.Von) return Phase.Vorher;
        if (t >= f.Bis) { anteil = 1; return Phase.Nachher; }
        var u = (t - f.Von) / (f.Bis - f.Von);
        var gehen = 1 - HaltAnteil;
        var hin = gehen * System.Math.Clamp(hinweg, 0.1, 0.9);
        if (u < hin) { anteil = u / hin; return Phase.Hinaus; }
        if (u < hin + HaltAnteil) { anteil = (u - hin) / HaltAnteil; return Phase.Halt; }
        anteil = (u - hin - HaltAnteil) / (gehen - hin);
        return Phase.Zurueck;
    }
}
