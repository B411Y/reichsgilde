using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Dynastie;

/// <summary>
/// Die Lebensweise einer Person (Charaktersystem Teil 3a): aktiver Pfad, Erfahrung je Pfad und die genommenen Knoten.
/// Nur Oberhaupt, Erbe und Ehepartner der Häuser sammeln Erfahrung; Knoten bleiben für immer.
/// </summary>
public sealed class Lebensweg
{
    private readonly double[] _erfahrung = new double[TalentTexte.Alle.Length];
    private readonly List<string> _knoten = new();

    /// <summary>Der gewählte Pfad (null: noch keiner – dann gilt der Pfad des stärksten Talents, siehe <see cref="Familienbuch.AktiverPfad"/>).</summary>
    public Talent? Pfad { get; internal set; }

    /// <summary>Gesammelte Erfahrung in diesem Pfad (auch ausgegebene – Punkte ergeben sich aus der Summe).</summary>
    public double Erfahrung(Talent pfad) => _erfahrung[(int)pfad];

    internal void ErfahrungSetzen(Talent pfad, double wert) => _erfahrung[(int)pfad] = Math.Max(0, wert);
    internal void ErfahrungDazu(Talent pfad, double wert) => _erfahrung[(int)pfad] += Math.Max(0, wert);

    /// <summary>Die genommenen Knoten (Kennungen) in der Reihenfolge der Wahl.</summary>
    public IReadOnlyList<string> Knoten => _knoten;

    public bool HatKnoten(string kennung) => _knoten.Contains(kennung);

    /// <summary>Hat die Person schon etwas gesammelt oder gewählt? (Sonst schreibt der Spielstand nichts.)</summary>
    public bool IstLeer => Pfad is null && _knoten.Count == 0 && _erfahrung.All(e => e == 0);

    /// <summary>Alle Knoten zusammen (neu bei jeder Änderung): allgemeine Wirkung und die Werkstatt-Wirkungen mit ihrer Mindeststufe.</summary>
    public LebenswegWirkung Wirkung { get; private set; } = LebenswegWirkung.Keine;
    internal IReadOnlyList<(EinrichtungWirkung Wirkung, int AbStufe)> Werkstatt { get; private set; } = Array.Empty<(EinrichtungWirkung, int)>();

    internal void KnotenHinzufuegen(string kennung, LebensweiseDaten? daten)
    {
        if (!_knoten.Contains(kennung)) _knoten.Add(kennung);
        WirkungNeu(daten);
    }

    /// <summary>Rechnet die Wirkung aus den Knoten neu (Knoten, die es in den Daten nicht mehr gibt, wirken nicht).</summary>
    internal void WirkungNeu(LebensweiseDaten? daten)
    {
        var wirkung = LebenswegWirkung.Keine;
        var werkstatt = new List<(EinrichtungWirkung, int)>();
        if (daten is not null)
            foreach (var kennung in _knoten)
                if (daten.Knoten(kennung) is { } k)
                {
                    wirkung = wirkung.Mit(k.Daten.Wirkung);
                    if (k.Daten.Einrichtung is { } e) werkstatt.Add((e, k.Daten.AbStufe));
                }
        Wirkung = wirkung;
        Werkstatt = werkstatt;
    }
}
