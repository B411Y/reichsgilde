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

    /// <summary>Stelle im Umland (z. B. „Weide“, „Feld“, „Wald“), solange <see cref="Ort"/> = Umland – sonst null.</summary>
    public string? Stelle { get; internal set; }

    /// <summary>
    /// Das Haus, in dem er gerade ist, solange <see cref="Ort"/> = Heim: meist sein eigenes (<see cref="Wohnhaus"/>),
    /// beim Dienst (Knecht, Magd) das Haus der Herrschaft. Sonst 0.
    /// </summary>
    public int Haus { get; internal set; }

    /// <summary>Die Schänke, in die er heute einkehrt (einmal am Tag nach der Anziehung gewählt; über Nacht vergessen).</summary>
    public string? Stammschaenke { get; internal set; }

    /// <summary>Nummer seines Wohnhauses (siehe <see cref="Einwohnerschaft.Wohnhaeuser"/>).</summary>
    public int Wohnhaus { get; internal set; }

    /// <summary>Der letzte Weg: von wo nach wo, mit Abmarsch und Ankunft. Null, solange er noch nicht gegangen ist (morgens zu Hause).</summary>
    public Weg? Weg { get; internal set; }

    /// <summary>Wo er sich gerade aufhält bzw. wohin er unterwegs ist – alles in einem Wert.</summary>
    public Aufenthalt Aufenthalt => new(Ort, Betrieb, Haus, Stelle);

    /// <summary>Wo er zu dieser Minute ist – null, solange er unterwegs ist. Vor dem Abmarsch: noch am alten Ort.</summary>
    public Aufenthalt? Wo(long minute) =>
        Weg is not { } w || minute >= w.Ankunft ? Aufenthalt : minute < w.Abmarsch ? w.Von : null;

    /// <summary>Ist er gerade unterwegs? (<paramref name="minute"/> = <see cref="Zeit.Spielzeit.VergangeneMinuten"/>)</summary>
    public bool IstUnterwegs(long minute) => Weg is { } w && minute >= w.Abmarsch && minute < w.Ankunft;

    /// <summary>Ist er am Ziel (nicht mehr unterwegs)? Vor dem Abmarsch steht er noch am alten Ort.</summary>
    public bool IstAngekommen(long minute) => Weg is not { } w || minute >= w.Ankunft;

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

/// <summary>
/// Ein Aufenthaltsort: Ort, dazu je nach Ort der Betrieb (Schänke, Kirche), das Haus (Heim) oder die Stelle im Umland.
/// Der Kern kennt keine Koordinaten – die Darstellung ordnet jedem Aufenthalt einen Platz im Stadtplan zu.
/// </summary>
public readonly record struct Aufenthalt(Stadtort Ort, string? Betrieb, int Haus, string? Stelle);

/// <summary>
/// Ein Weg von einem Aufenthalt zum nächsten. Die Zeiten sind <see cref="Zeit.Spielzeit.VergangeneMinuten"/>:
/// vor <see cref="Abmarsch"/> steht der Einwohner noch bei <see cref="Von"/>, ab <see cref="Ankunft"/> ist er bei <see cref="Nach"/>,
/// dazwischen läuft er (Anteil des Weges = (Minute − Abmarsch) ÷ (Ankunft − Abmarsch)).
/// </summary>
public sealed record Weg(Aufenthalt Von, Aufenthalt Nach, long Abmarsch, long Ankunft)
{
    public int Dauer => (int)(Ankunft - Abmarsch);

    /// <summary>Wie weit er ist: 0 = noch am Start, 1 = angekommen. Mit Bruchteil der Minute für flüssige Bewegung.</summary>
    public double Fortschritt(double minute) => Dauer <= 0 ? 1 : Math.Clamp((minute - Abmarsch) / Dauer, 0, 1);
}

/// <summary>Was für ein Haus: einfaches Wohnhaus eines Haushalts, Bürgerhaus (Kandidaten aus Bürger- und Patrizierfamilien) oder Ratsherrenhaus.</summary>
public enum Hausart
{
    Wohnhaus,
    Buergerhaus,
    Ratsherrenhaus,
}

/// <summary>Ein Haus in der Stadt, in dem Einwohner wohnen. Wo es steht, entscheidet die Darstellung.</summary>
public sealed record Stadthaus(int Nummer, Hausart Art);
