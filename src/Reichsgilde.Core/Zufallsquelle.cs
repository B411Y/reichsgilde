namespace Reichsgilde.Core;

/// <summary>
/// Ein Zufallsgenerator, den man speichern kann.
///
/// Das Problem: Der eingebaute .NET-Zufallsgenerator (<see cref="Random"/>) verrät seinen inneren
/// Zustand nicht – man kann ihn nicht in eine Datei schreiben.
///
/// Der Trick: Diese Hülle zählt nur mit, wie oft gewürfelt wurde. Zum Wiederherstellen erzeugt man
/// einen frischen Generator mit demselben Startwert und lässt ihn genauso oft „leer würfeln".
/// Danach ist er exakt im selben Zustand – die nächsten Würfe sind dieselben wie im Original.
///
/// Voraussetzung: Jeder Aufruf verbraucht genau einen Wurf. Das gilt für <see cref="NextDouble"/>
/// und <see cref="Next(int)"/> beim .NET-Generator mit Startwert (ein Test prüft das).
/// </summary>
public sealed class Zufallsquelle
{
    private readonly Random _random;

    public int Startwert { get; }

    /// <summary>Wie oft bisher gewürfelt wurde.</summary>
    public long Wuerfe { get; private set; }

    public Zufallsquelle(int startwert, long bisherigeWuerfe = 0)
    {
        Startwert = startwert;
        _random = new Random(startwert);
        for (long i = 0; i < bisherigeWuerfe; i++)
            _random.NextDouble(); // vorspulen
        Wuerfe = bisherigeWuerfe;
    }

    /// <summary>Kommazahl von 0 (einschließlich) bis 1 (ausschließlich).</summary>
    public double NextDouble()
    {
        Wuerfe++;
        return _random.NextDouble();
    }

    /// <summary>Ganzzahl von 0 (einschließlich) bis <paramref name="obergrenze"/> (ausschließlich).</summary>
    public int Next(int obergrenze)
    {
        Wuerfe++;
        return _random.Next(obergrenze);
    }
}
