namespace Reichsgilde.Core.Zeit;

/// <summary>Die Wetterlagen der Stadt. Sie wirken nicht auf die Wirtschaft; nur im Gewitter kann der Blitz einschlagen (<see cref="Gefahren.Blitzwesen"/>).</summary>
public enum Wetterlage { Klar, Wolkig, Regen, Sturm, Gewitter, Schnee, Nebel }

/// <summary>
/// Der Plan des Wetters einer Partie: fest aus dem Startwert der Partie und der Spielzeit gerechnet, nichts davon wird gespeichert. Nach dem
/// Laden läuft das Wetter darum genau so weiter, wie es vorher gelaufen wäre, und zwei Spieler mit derselben Partie sehen dasselbe Wetter.
///
/// Jedes Spieljahr (ein Spieltag, 04–23 Uhr) ist eine Folge von Abschnitten zu 2½ bis 5½ Stunden, jeder mit einer Lage. Welche Lage folgt,
/// würfelt eine eigene <see cref="Zufallsquelle"/> aus Startwert und Jahr nach den Gewichten der Jahreszeit (<see cref="Gewichte"/>): Schnee nur
/// im Winter, Gewitter vor allem im Sommer und am Nachmittag, Nebel im Herbst und Frühling und vor allem am Morgen.
///
/// Bis zum Blitzschlag (2.11.2026) stand der Plan in der Darstellung (<c>game/scripts/Wetter.cs</c>, <c>Wetterablauf</c>); der Kern braucht
/// ihn jetzt für die Gewitter. Er würfelt genau wie vorher (dieselben Zahlen aus demselben Startwert), das Wetter bestehender Partien bleibt gleich.
/// Die Darstellung liest ihn über <c>Wetterablauf</c> und mischt dort die Bildwerte.
/// </summary>
public sealed class Wetterplan
{
    private const int KuerzesterAbschnitt = 150, LaengsterAbschnitt = 330;

    private readonly int _startwert;
    private readonly Dictionary<int, Wetterabschnitt[]> _jahre = new();

    public Wetterplan(int startwert) => _startwert = startwert;

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

    private static readonly Wetterlage[] Lagen = Enum.GetValues<Wetterlage>();

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

    /// <summary>
    /// Die Abschnitte eines Jahres (gezählt ab 0 = 1400): Beginn und Ende (Minute im Tag, das letzte endet um 23 Uhr) und Lage, einmal gerechnet
    /// und gemerkt.
    /// </summary>
    public IReadOnlyList<Wetterabschnitt> Abschnitte(int jahr)
    {
        if (_jahre.TryGetValue(jahr, out var fertig)) return fertig;
        // Bis 2.11.2026 new Random(…) mit Next(von, bis + 1): Zufallsquelle liefert mit demselben Startwert dieselben Zahlen
        var zufall = new Zufallsquelle(unchecked(_startwert * 7919 + jahr * 104729 + 17));
        var jahreszeit = JahreszeitVon(jahr);
        var liste = new List<(int Beginn, Wetterlage Lage)>();
        Wetterlage? vorher = null;
        for (var beginn = 0; beginn < Spielzeit.MinutenProTag; beginn += KuerzesterAbschnitt + zufall.Next(LaengsterAbschnitt + 1 - KuerzesterAbschnitt))
        {
            var summe = 0f;
            foreach (var lage in Lagen) summe += Gewicht(jahreszeit, lage, beginn, vorher);
            var wurf = (float)zufall.NextDouble() * summe;
            var gewaehlt = Wetterlage.Klar;
            foreach (var lage in Lagen)
            {
                var g = Gewicht(jahreszeit, lage, beginn, vorher);
                if (g <= 0) continue;
                gewaehlt = lage;
                if ((wurf -= g) < 0) break;
            }
            liste.Add((beginn, gewaehlt));
            vorher = gewaehlt;
        }
        var abschnitte = new Wetterabschnitt[liste.Count];
        for (var i = 0; i < liste.Count; i++)
            abschnitte[i] = new Wetterabschnitt(liste[i].Beginn, i + 1 < liste.Count ? liste[i + 1].Beginn : Spielzeit.MinutenProTag, liste[i].Lage);
        if (_jahre.Count > 8) _jahre.Clear(); // es zählen nur das laufende Jahr und seine Nachbarn
        return _jahre[jahr] = abschnitte;
    }

    /// <summary>Der Abschnitt zur Spielzeit (<paramref name="minute"/> = vergangene Minuten seit Spielbeginn), mit seiner Nummer im Jahr.</summary>
    public (IReadOnlyList<Wetterabschnitt> Abschnitte, int Nummer, int MinuteImTag) Stelle(long minute)
    {
        var jahr = (int)(minute / Spielzeit.MinutenProTag);
        var imTag = (int)(minute % Spielzeit.MinutenProTag);
        var abschnitte = Abschnitte(jahr);
        var i = abschnitte.Count - 1;
        while (i > 0 && abschnitte[i].Beginn > imTag) i--;
        return (abschnitte, i, imTag);
    }

    /// <summary>Die Lage, die im Plan zur Spielzeit gilt (ohne den weichen Übergang der Darstellung).</summary>
    public Wetterlage Lage(long minute)
    {
        var (abschnitte, i, _) = Stelle(minute);
        return abschnitte[i].Lage;
    }
}

/// <summary>Ein Abschnitt des Wetters: von <paramref name="Beginn"/> bis <paramref name="Ende"/> (Minuten im Tag ab 4 Uhr) mit einer Lage.</summary>
public readonly record struct Wetterabschnitt(int Beginn, int Ende, Wetterlage Lage)
{
    public int Laenge => Ende - Beginn;

    /// <summary>Die Mitte des Abschnitts (Minute im Tag): Ein Gewitter steht dann über der Stadt (der Donner folgt dem Blitz am schnellsten).</summary>
    public int Mitte => Beginn + Laenge / 2;
}
