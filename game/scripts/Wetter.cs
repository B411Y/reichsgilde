using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>Die Wetterlagen der Stadt (nur Darstellung, ohne Wirkung auf Wirtschaft oder Simulation).</summary>
public enum Wetterlage { Klar, Wolkig, Regen, Sturm, Gewitter, Schnee, Nebel }

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
        Wetterlage.Sturm => new(0.8f, 0.3f, 0, 1f, 0, 0),
        Wetterlage.Gewitter => new(0.95f, 1f, 0, 0.75f, 0.05f, 1f),
        Wetterlage.Schnee => new(0.8f, 0, 0.8f, 0.3f, 0.2f, 0),
        Wetterlage.Nebel => new(0.5f, 0, 0, 0.05f, 1f, 0),
        _ => new(0.1f, 0, 0, 0.25f, 0, 0),
    };
}

/// <summary>
/// Der Wetterablauf einer Partie: fest aus dem Startwert der Partie und der Spielzeit gerechnet, nichts davon wird gespeichert. Nach dem
/// Laden läuft das Wetter darum genau so weiter, wie es vorher gelaufen wäre, und zwei Spieler mit derselben Partie sehen dasselbe Wetter.
///
/// Jedes Spieljahr (ein Spieltag, 04–23 Uhr) ist eine Folge von Abschnitten zu 2½ bis 5½ Stunden, jeder mit einer Lage. Welche Lage folgt,
/// würfelt ein Generator aus Startwert und Jahr nach den Gewichten der Jahreszeit (<see cref="Gewichte"/>): Schnee nur im Winter, Gewitter vor
/// allem im Sommer und am Nachmittag, Nebel im Herbst und Frühling und vor allem am Morgen. Die ersten <see cref="UebergangMinuten"/> eines
/// Abschnitts gleitet das Wetter von der alten Lage in die neue.
/// </summary>
public sealed class Wetterablauf
{
    /// <summary>So lange (Spielminuten) gleitet das Wetter zu Beginn eines Abschnitts von der alten Lage in die neue.</summary>
    public const int UebergangMinuten = 45;

    private const int KuerzesterAbschnitt = 150, LaengsterAbschnitt = 330;

    private readonly int _startwert;
    private readonly Dictionary<int, (int Beginn, Wetterlage Lage)[]> _jahre = new();

    public Wetterablauf(int startwert) => _startwert = startwert;

    /// <summary>
    /// Gewichte der Lagen je Jahreszeit (Klar, Wolkig, Regen, Sturm, Gewitter, Schnee, Nebel). Vor 9 Uhr zählt Nebel doppelt und später nur
    /// ein Drittel, Gewitter ab Mittag anderthalbfach und am Morgen ein Drittel (<see cref="Gewicht"/>).
    /// </summary>
    private static readonly Dictionary<Jahreszeit, int[]> Gewichte = new()
    {
        [Jahreszeit.Fruehling] = new[] { 25, 30, 22, 6, 5, 0, 12 },
        [Jahreszeit.Sommer] = new[] { 40, 25, 12, 5, 14, 0, 4 },
        [Jahreszeit.Herbst] = new[] { 18, 28, 22, 14, 2, 0, 16 },
        [Jahreszeit.Winter] = new[] { 22, 28, 4, 8, 0, 30, 12 },
    };

    private static float Gewicht(Jahreszeit jahreszeit, Wetterlage lage, int minuteImTag, Wetterlage? vorher)
    {
        float g = Gewichte[jahreszeit][(int)lage];
        var stunde = 5 + minuteImTag / 60;
        if (lage == Wetterlage.Nebel) g *= stunde < 9 ? 2f : 0.33f;
        if (lage == Wetterlage.Gewitter) g *= stunde >= 12 ? 1.5f : 0.33f;
        if (lage == vorher) g *= 0.4f; // dieselbe Lage noch einmal ist seltener: Das Wetter soll sich über den Tag ändern
        return g;
    }

    /// <summary>Die Jahreszeit eines Jahres (gezählt ab 0 = 1400), wie <see cref="Spielzeit.Jahreszeit"/>.</summary>
    private static Jahreszeit JahreszeitVon(int jahr) => (Jahreszeit)(jahr % 4);

    /// <summary>Die Abschnitte eines Jahres: Beginn (Minute im Tag) und Lage, einmal gerechnet und gemerkt.</summary>
    private (int Beginn, Wetterlage Lage)[] Abschnitte(int jahr)
    {
        if (_jahre.TryGetValue(jahr, out var fertig)) return fertig;
        var zufall = new Random(unchecked(_startwert * 7919 + jahr * 104729 + 17));
        var jahreszeit = JahreszeitVon(jahr);
        var liste = new List<(int, Wetterlage)>();
        Wetterlage? vorher = null;
        for (var beginn = 0; beginn < Spielzeit.MinutenProTag; beginn += zufall.Next(KuerzesterAbschnitt, LaengsterAbschnitt + 1))
        {
            var summe = 0f;
            foreach (var lage in Enum.GetValues<Wetterlage>()) summe += Gewicht(jahreszeit, lage, beginn, vorher);
            var wurf = (float)zufall.NextDouble() * summe;
            var gewaehlt = Wetterlage.Klar;
            foreach (var lage in Enum.GetValues<Wetterlage>())
            {
                var g = Gewicht(jahreszeit, lage, beginn, vorher);
                if (g <= 0) continue;
                gewaehlt = lage;
                if ((wurf -= g) < 0) break;
            }
            liste.Add((beginn, gewaehlt));
            vorher = gewaehlt;
        }
        if (_jahre.Count > 8) _jahre.Clear(); // es zählen nur das laufende Jahr und seine Nachbarn
        return _jahre[jahr] = liste.ToArray();
    }

    /// <summary>
    /// Die Lage zur Spielzeit (<paramref name="minute"/> = vergangene Minuten seit Spielbeginn) für die Anzeige: Sie wechselt in der Mitte
    /// des Übergangs, wenn das Bild schon halb nach der neuen Lage aussieht.
    /// </summary>
    public Wetterlage Lage(long minute)
    {
        var (abschnitte, i) = GezeigterAbschnitt(minute);
        return abschnitte[i].Lage;
    }

    private ((int Beginn, Wetterlage Lage)[] Abschnitte, int Nummer) GezeigterAbschnitt(long minute)
    {
        var (abschnitte, i, imTag) = Stelle(minute);
        if (i > 0 && imTag - abschnitte[i].Beginn < UebergangMinuten / 2) i--;
        return (abschnitte, i);
    }

    private ((int Beginn, Wetterlage Lage)[] Abschnitte, int Nummer, int MinuteImTag) Stelle(long minute)
    {
        var jahr = (int)(minute / Spielzeit.MinutenProTag);
        var imTag = (int)(minute % Spielzeit.MinutenProTag);
        var abschnitte = Abschnitte(jahr);
        var i = abschnitte.Length - 1;
        while (i > 0 && abschnitte[i].Beginn > imTag) i--;
        return (abschnitte, i, imTag);
    }

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
        for (var k = i + 1; k < abschnitte.Length && liste.Count < anzahl; k++)
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
