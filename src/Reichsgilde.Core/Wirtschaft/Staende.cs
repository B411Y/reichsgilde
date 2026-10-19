using System.Text.Json.Serialization;

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
/// Verkauft ein Haus einen Betrieb (<see cref="BetriebVerkaufen"/>), wird er wieder eine Liegenschaft –
/// dann mit seiner Stufe, dem vollen Wert als Kaufpreis und <see cref="Bebaut"/> (siehe <see cref="Stadtwirtschaft.FreieLiegenschaften"/>).
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

    /// <summary>
    /// Ein verkaufter Betrieb: Das Gebäude steht schon (mit seiner Stufe, <see cref="Art"/> kann z. B. Schmiede sein) –
    /// kein leerer Bauplatz. Nie in wirtschaft.json, nur aus <see cref="Stadtwirtschaft.FreieLiegenschaften"/>.
    /// </summary>
    [JsonIgnore]
    public bool Bebaut { get; init; }

    /// <summary>Welchen Meistergrad im Beruf der Käufer braucht – wie beim Ausbau zu dieser Stufe (Stufe 1: Grad 1).</summary>
    [JsonIgnore]
    public int MindestGrad { get; init; } = 1;

    /// <summary>Ein verkaufter Betrieb: Diese Einrichtung ist im Haus geblieben und geht an den nächsten Käufer (Kennungen).</summary>
    [JsonIgnore]
    public List<string> Einrichtung { get; init; } = new();
}

/// <summary>
/// Betriebe verkaufen (Abschnitt "verkauf", ab Datenversion 33): Werkstätten bringen einen Teil ihres Werts,
/// die Lagerware geht mit einem Abschlag auf den Marktpreis weg. Fehlt der Abschnitt, gelten die Standardwerte.
/// </summary>
public sealed class VerkaufDaten
{
    /// <summary>Anteil des Werts, den eine Werkstatt beim Verkauf bringt (0,75 = 75 %). Wert = Kaufpreis Stufe 1 + alle Ausbauten.</summary>
    public double Anteil { get; init; } = 0.75;

    /// <summary>Bearbeitungsgebühr auf die mitverkaufte Lagerware (0,2 = Marktpreis −20 %).</summary>
    public double WarenAbschlag { get; init; } = 0.2;

    public void Pruefen(List<string> f)
    {
        if (Anteil is < 0 or > 1) f.Add("verkauf.anteil muss zwischen 0 und 1 liegen.");
        if (WarenAbschlag is < 0 or > 1) f.Add("verkauf.warenAbschlag muss zwischen 0 und 1 liegen.");
    }
}
