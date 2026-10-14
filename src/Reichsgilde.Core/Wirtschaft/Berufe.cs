namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Was eine Betriebsart ist und kann (Abschnitt „betriebsarten" in wirtschaft.json, ab Datenversion 12).
///
/// Werkstätten haben <see cref="Produkte"/> (Waren mit Zutaten), Umlandbetriebe fördern <see cref="Rohstoffe"/>
/// (eine oder zwei Waren ohne Zutaten; bei zwei teilt man die Arbeitszeit auf). Ein neuer Beruf ist damit
/// vor allem ein Eintrag in den Daten – nur Aussehen und Platz in der Stadt kommen aus der Darstellung.
/// </summary>
public sealed class BetriebsartDaten
{
    /// <summary>Anzeigename der Art („Gießerei", „Grube", „Tischlerei", „Holzschlag").</summary>
    public string Name { get; init; } = "";

    /// <summary>Zu welchem Beruf sie gehört (Name aus „berufe").</summary>
    public string Beruf { get; init; } = "";

    public Arbeitsort Ort { get; init; } = Arbeitsort.Werkstatt;
    public int Plaetze { get; init; } = 2;

    /// <summary>Unterhalt (Werkstatt) bzw. Pacht (Umland) im Jahr.</summary>
    public double Fixkosten { get; init; }

    /// <summary>Werkstatt: was sie herstellen kann (Waren mit Zutaten).</summary>
    public List<string> Produkte { get; init; } = new();

    /// <summary>Werkstatt: womit ein neuer Betrieb anfängt.</summary>
    public string? Standardprodukt { get; init; }

    /// <summary>Umland: was gefördert wird (eine oder zwei Waren ohne Zutaten).</summary>
    public List<string> Rohstoffe { get; init; } = new();

    /// <summary>Karren, mit dem ein neuer Betrieb anfängt.</summary>
    public string Startkarren { get; init; } = "Handkarren";

    public bool IstWerkstatt => Produkte.Count > 0;
}

/// <summary>Ein Beruf (Abschnitt „berufe"): welche Betriebe dazugehören und was es kostet, ihn zu erlernen.</summary>
public sealed class BerufDaten
{
    /// <summary>Die Werkstatt des Berufs (Stufe 1) – mit ihr beginnt, wer den Beruf als Startberuf wählt.</summary>
    public Betriebsart Werkstatt { get; init; }

    /// <summary>Der Umlandbetrieb, der die Rohstoffe liefert.</summary>
    public Betriebsart Umland { get; init; }

    /// <summary>Gulden für Lehrgeld und Werkzeug, wenn man den Beruf später erlernt (die AP stehen bei „aktionspunkte").</summary>
    public double Lehrgeld { get; init; } = 500;

    public string Beschreibung { get; init; } = "";
}
