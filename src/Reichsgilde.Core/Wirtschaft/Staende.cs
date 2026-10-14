namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Ein Stand der Ständegesellschaft (Abschnitt "staende" in wirtschaft.json).
///
/// Aufstieg: Wer mindestens <see cref="Schwelle"/> Gulden besitzt, kann im Rathaus gegen
/// <see cref="Gebuehr"/> in diesen Stand aufsteigen – immer nur eine Stufe auf einmal.
/// Ein Stand bleibt erhalten, auch wenn das Vermögen danach wieder sinkt
/// (aberkennen kann ihn später nur ein Gericht).
/// </summary>
public sealed class StandDaten
{
    public string Name { get; init; } = "";

    /// <summary>Nötiges Vermögen, um den Aufstieg zu beantragen.</summary>
    public double Schwelle { get; init; }

    /// <summary>Gebühr für den Aufstieg (Bürgerrecht, Adelsbrief …).</summary>
    public double Gebuehr { get; init; }

    /// <summary>Was der Stand erlaubt – bisher nur als Beschreibung, die Regeln folgen mit Rat und Gericht.</summary>
    public string Privilegien { get; init; } = "";
}

/// <summary>Regeln für Schulden (Abschnitt "schulden").</summary>
public sealed class SchuldenDaten
{
    /// <summary>
    /// So viele Jahresabschlüsse in Folge mit negativem Vermögen führen zum Bankrott.
    /// Beim ersten gibt es eine Warnung.
    /// </summary>
    public int JahreBisBankrott { get; init; } = 3;
}

/// <summary>
/// Ein freier Betrieb, den man erwerben kann (Abschnitt "liegenschaften").
///   • Werkstätten werden gekauft (<see cref="Kaufpreis"/>), danach fällt jährlich Unterhalt an.
///   • Gruben werden gepachtet (Kaufpreis 0), die Pacht wird jährlich fällig.
/// Wer zuerst kommt, mahlt zuerst: Jede Liegenschaft gibt es nur einmal.
/// </summary>
public sealed class LiegenschaftDaten
{
    public string Name { get; init; } = "";
    public Betriebsart Art { get; init; }
    public double Kaufpreis { get; init; }

    /// <summary>Ab welchem Stand man sie erwerben darf (Name aus "staende").</summary>
    public string MindestStand { get; init; } = "Bürger";

    /// <summary>Ohne Kaufpreis wird gepachtet (Umland): Man zahlt nur die Jahrespacht.</summary>
    public bool IstPacht => Kaufpreis <= 0;
}
