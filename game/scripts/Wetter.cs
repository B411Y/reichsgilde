using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

// Die Wetterlagen (Wetterlage) und ihr Plan (Wetterplan) stehen seit dem Blitzschlag (2.11.2026) im Kern (Reichsgilde.Core.Zeit): Im Gewitter
// kann der Blitz einschlagen. Hier bleibt, was die Lagen am Bild ändern, und das Mischen zwischen ihnen.

/// <summary>
/// Was eine Wetterlage an der Darstellung ändert, jeder Wert 0 … 1. Zwischen zwei Lagen wird gemischt (<see cref="Mischen"/>).
/// </summary>
/// <param name="Wolken">Bedeckung: dämpft Sonne und Schatten, färbt den Himmel grau.</param>
/// <param name="Regen">Regenmenge: Tropfen um die Kamera, nasser Boden, Pfützen.</param>
/// <param name="Schnee">Schneefall: Flocken um die Kamera; im Winter dazu die Schneedecke.</param>
/// <param name="Wind">Windstärke: Gras, Bäume, Fahnen, Wolken und Regen.</param>
/// <param name="Nebel">Nebel über der ganzen Stadt.</param>
/// <param name="Blitze">Wie oft es blitzt (1 = Gewitter).</param>
public readonly record struct Wetterwerte(float Wolken, float Regen, float Schnee, float Wind, float Nebel, float Blitze)
{
    public static Wetterwerte Mischen(Wetterwerte a, Wetterwerte b, float t) => new(
        a.Wolken + (b.Wolken - a.Wolken) * t, a.Regen + (b.Regen - a.Regen) * t, a.Schnee + (b.Schnee - a.Schnee) * t,
        a.Wind + (b.Wind - a.Wind) * t, a.Nebel + (b.Nebel - a.Nebel) * t, a.Blitze + (b.Blitze - a.Blitze) * t);

    /// <summary>Die Werte jeder Lage.</summary>
    public static Wetterwerte Von(Wetterlage lage) => lage switch
    {
        Wetterlage.Wolkig => new(0.6f, 0, 0, 0.4f, 0, 0),
        Wetterlage.Regen => new(0.85f, 0.75f, 0, 0.5f, 0.15f, 0),
        Wetterlage.Sturm => new(0.8f, 0.75f, 0, 1f, 0, 0),
        Wetterlage.Gewitter => new(0.95f, 1f, 0, 0.75f, 0.05f, 1f),
        Wetterlage.Schnee => new(0.8f, 0, 0.8f, 0.3f, 0.2f, 0),
        Wetterlage.Nebel => new(0.5f, 0, 0, 0.05f, 1f, 0),
        _ => new(0.1f, 0, 0, 0.25f, 0, 0),
    };
}

/// <summary>
/// Der Wetterablauf einer Partie für die Darstellung: Der Plan der Lagen kommt aus dem Kern (<see cref="Wetterplan"/>, fest aus dem Startwert der
/// Partie und der Spielzeit, nichts davon wird gespeichert); hier wird zwischen den Lagen gemischt. Die ersten <see cref="UebergangMinuten"/>
/// eines Abschnitts gleitet das Wetter von der alten Lage in die neue.
/// </summary>
public sealed class Wetterablauf
{
    /// <summary>So lange (Spielminuten) gleitet das Wetter zu Beginn eines Abschnitts von der alten Lage in die neue.</summary>
    public const int UebergangMinuten = 45;

    private readonly Wetterplan _plan;

    public Wetterablauf(int startwert) => _plan = new Wetterplan(startwert);

    /// <summary>Der Plan des Kerns (dieselben Abschnitte wie für den Blitzschlag).</summary>
    public Wetterplan Plan => _plan;

    /// <summary>
    /// Die Lage zur Spielzeit (<paramref name="minute"/> = vergangene Minuten seit Spielbeginn) für die Anzeige: Sie wechselt in der Mitte
    /// des Übergangs, wenn das Bild schon halb nach der neuen Lage aussieht.
    /// </summary>
    public Wetterlage Lage(long minute)
    {
        var (abschnitte, i) = GezeigterAbschnitt(minute);
        return abschnitte[i].Lage;
    }

    private (IReadOnlyList<Wetterabschnitt> Abschnitte, int Nummer) GezeigterAbschnitt(long minute)
    {
        var (abschnitte, i, imTag) = Stelle(minute);
        if (i > 0 && imTag - abschnitte[i].Beginn < UebergangMinuten / 2) i--;
        return (abschnitte, i);
    }

    private (IReadOnlyList<Wetterabschnitt> Abschnitte, int Nummer, int MinuteImTag) Stelle(long minute) => _plan.Stelle(minute);

    /// <summary>
    /// Die Werte zur Spielzeit (mit Bruchteil der Minute), am Beginn eines Abschnitts weich aus der vorigen Lage. Über den Jahreswechsel
    /// (23 → 4 Uhr) springt das Wetter; dort blendet die Stadtansicht in Echtzeit über.
    /// </summary>
    public Wetterwerte Werte(double minute)
    {
        var (abschnitte, i, imTag) = Stelle((long)minute);
        var jetzt = Wetterwerte.Von(abschnitte[i].Lage);
        if (i == 0) return jetzt;
        var t = (float)((imTag + (minute - Math.Floor(minute)) - abschnitte[i].Beginn) / UebergangMinuten);
        if (t >= 1) return jetzt;
        t = t * t * (3 - 2 * t);
        return Wetterwerte.Mischen(Wetterwerte.Von(abschnitte[i - 1].Lage), jetzt, t);
    }

    /// <summary>
    /// Wie weit ein Gewitter ist (0 = beginnt, 0,5 = Mitte, 1 = zu Ende), wenn zur Spielzeit <paramref name="minute"/> gerade ein Gewitter-Abschnitt läuft; sonst −1.
    /// Nur gelesen (Ton und Blitzabstand: Das Gewitter zieht heran und weiter, siehe <c>KlangUmgebungModell.BlitzAbstandWuerfeln</c>).
    /// </summary>
    public float GewitterFortschritt(double minute)
    {
        var (abschnitte, i, imTag) = Stelle((long)minute);
        if (abschnitte[i].Lage != Wetterlage.Gewitter) return -1f;
        var laenge = Math.Max(1, abschnitte[i].Laenge);
        return Math.Clamp((float)((imTag + (minute - Math.Floor(minute)) - abschnitte[i].Beginn) / laenge), 0f, 1f);
    }

    /// <summary>
    /// Wie nass der Boden ist (0 … 1): Regen der letzten zwei Stunden, der jüngste zählt am meisten (alle 10 Minuten eine Probe). Nach dem
    /// Regen trocknet es so in etwa einer Stunde ab, und der Boden wird erst nach und nach nass.
    /// </summary>
    public float Naesse(double minute)
    {
        var tagesbeginn = Math.Floor(minute / Spielzeit.MinutenProTag) * Spielzeit.MinutenProTag;
        float summe = 0, gewichte = 0, gewicht = 1;
        for (var k = 0; k < 12; k++, gewicht *= 0.78f)
        {
            var t = minute - 10 * k;
            gewichte += gewicht;
            if (t < tagesbeginn) continue; // vor 4 Uhr (in der Nacht) ist der Boden getrocknet
            summe += gewicht * Math.Min(1f, Werte(t).Regen * 1.6f);
        }
        return Math.Min(1f, summe / gewichte * 1.6f);
    }

    /// <summary>
    /// Aussicht: die nächsten Abschnitte des Tages ab <paramref name="minute"/> (Beginn als Uhrzeit in Minuten nach Mitternacht und Lage),
    /// höchstens <paramref name="anzahl"/>, nur bis 23 Uhr.
    /// </summary>
    public IReadOnlyList<(int Uhrzeit, Wetterlage Lage)> Aussicht(long minute, int anzahl)
    {
        var (abschnitte, i) = GezeigterAbschnitt(minute);
        var liste = new List<(int, Wetterlage)>();
        for (var k = i + 1; k < abschnitte.Count && liste.Count < anzahl; k++)
            liste.Add((Spielzeit.TagesbeginnMinute + abschnitte[k].Beginn, abschnitte[k].Lage));
        return liste;
    }

    /// <summary>Anzeigename einer Lage.</summary>
    public static string Name(Wetterlage lage) => lage switch
    {
        Wetterlage.Klar => "Klar",
        Wetterlage.Wolkig => "Wolkig",
        Wetterlage.Regen => "Regen",
        Wetterlage.Sturm => "Sturm",
        Wetterlage.Gewitter => "Gewitter",
        Wetterlage.Schnee => "Schnee",
        Wetterlage.Nebel => "Nebel",
        _ => lage.ToString(),
    };
}
