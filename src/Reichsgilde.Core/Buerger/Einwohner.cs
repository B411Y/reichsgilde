using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;

namespace Reichsgilde.Core.Buerger;

/// <summary>
/// Ein Einwohner der Stadt: Name, Volk, Rolle und ein Tagesablauf nach Bedürfnissen.
/// Er führt keinen Betrieb und gehört zu keinem Haus. Die Darstellung liest <see cref="Ort"/> und <see cref="Betrieb"/>,
/// um ihn als Passanten zu zeigen.
/// </summary>
public sealed class Einwohner
{
    private readonly double[] _beduerfnisse = new double[Enum.GetValues<Beduerfnis>().Length];

    /// <summary>Eindeutige Nummer innerhalb der Einwohnerschaft (eigene Zählung, nicht die der Familien).</summary>
    public int Nummer { get; }

    public string Vorname { get; internal set; }
    public string Familienname { get; internal set; }
    public string Name => $"{Vorname} {Familienname}";
    public Volk Volk { get; internal set; }
    public Geschlecht Geschlecht { get; internal set; }
    public int Geburtsjahr { get; internal set; }
    public Konfession Glaube { get; internal set; }

    /// <summary>Schlüssel der Rolle in den Daten (z. B. „Knecht").</summary>
    public string Rolle { get; internal set; }

    public Herkunft Herkunft { get; }

    /// <summary>Bei Ratsherren der Name des Ratsherrn, bei Kandidaten die Personennummer – sonst leer.</summary>
    public string Quelle { get; }

    /// <summary>Was er in dieser Stunde tut.</summary>
    public Handlung Handlung { get; internal set; } = Handlung.Ruhen;

    /// <summary>Wo er sich in dieser Stunde aufhält.</summary>
    public Stadtort Ort { get; internal set; } = Stadtort.Heim;

    /// <summary>Name der Schänke oder Kirche, in der er ist (sonst null; in der Kirche null = Stadtkirche).</summary>
    public string? Betrieb { get; internal set; }

    internal Einwohner(int nummer, Herkunft herkunft, string quelle, string vorname, string familienname, Volk volk,
                       Geschlecht geschlecht, int geburtsjahr, Konfession glaube, string rolle)
    {
        Nummer = nummer;
        Herkunft = herkunft;
        Quelle = quelle;
        Vorname = vorname;
        Familienname = familienname;
        Volk = volk;
        Geschlecht = geschlecht;
        Geburtsjahr = geburtsjahr;
        Glaube = glaube;
        Rolle = rolle;
    }

    public int Alter(int jahr) => jahr - Geburtsjahr;

    /// <summary>Dringlichkeit eines Bedürfnisses (0–100).</summary>
    public double Wert(Beduerfnis b) => _beduerfnisse[(int)b];

    internal void Setzen(Beduerfnis b, double wert) => _beduerfnisse[(int)b] = Math.Clamp(wert, 0, 100);

    /// <summary>Wie zufrieden er ist: 100 = alle Bedürfnisse gestillt, 0 = alle sehr dringend.</summary>
    public double Stimmung => 100 - _beduerfnisse.Average();

    internal IReadOnlyList<double> Beduerfnisse => _beduerfnisse;
}
