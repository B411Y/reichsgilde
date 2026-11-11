using System.Text.Json.Serialization;

namespace Reichsgilde.Core.Partie;

// ═════════════════════════════════════════════════════════════════════════════
//  Hofstellen (Nutzerentscheidung 09.11.2026)
//
//  Eine Hofstelle ist der Platz eines Hauses in der Stadt: ein Werkstattplatz und ein Wohnhausplatz der Karte, die zusammengehören. Sie
//  ersetzt die festen Startgrundstücke (S1–S8 in Köln) und die Werkstatt-Bauplätze (Bauzonen mit „nur“: „werkstatt“). Zu Spielbeginn
//  bekommen der Spieler (gewählt oder gelost) und jedes KI-Haus (gelost) je eine Hofstelle, die übrigen bleiben frei: Ihr Werkstatt- und
//  ihr Wohnhausteil werden Bauplätze (Bauwesen). Die beiden Plätze bleiben gewöhnliche Gebäude der Karte – Darstellung, Fahrzeit,
//  Überlappung und Kapazität arbeiten weiter mit ihnen; die Hofstelle fasst sie nur zusammen.
//
//  In der Kartendatei stehen die Hofstellen unter „start“ → „hofstellen“ (Startbelegung.Hofstellen). Wer ohne Los wo sitzt (die Vorgabe:
//  Tests, Vergleichsmessungen), sagt der übrige Abschnitt „start“ wie bisher.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Eine Hofstelle der Karte: Werkstattplatz und Wohnhausplatz eines Hauses.</summary>
public sealed class Hofstelle
{
    /// <summary>Eindeutig in der Karte („S1“, „H9“).</summary>
    public string Kennung { get; init; } = "";

    /// <summary>
    /// Die Lage, nach der die Werkstatt des Spielers heißt („Heumarkt“ → „Gießerei am Heumarkt“; ein Name mit eigener Präposition wie
    /// „an der Rheingasse“ bleibt, wie er ist). Fehlt er, gilt die nächste Karrenstraße. Auch der Name in der Auswahl des Startmenüs.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>Kennung des Werkstattplatzes der Karte.</summary>
    public string Werkstatt { get; init; } = "";

    /// <summary>Kennung des Wohnhausplatzes der Karte.</summary>
    public string Wohnhaus { get; init; } = "";

    /// <summary>
    /// Taugt sie als Startplatz? Nur unter diesen wählt der Spieler und wird gelost; für sie gilt die Fairness (Karrenweg zum Markt ±10 %,
    /// Kartenprüfung). Ohne Angabe (false): eine Hofstelle nur zum späteren Bauen, z. B. am Hafen.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Start { get => _start || Nummer is not null; init => _start = value; }
    private readonly bool _start;

    /// <summary>
    /// Nummer des Hofes, 1 bis <see cref="Partiebesetzung.HoechstensHoefe"/> (Nutzerentscheidung 11.11.2026): Hof 1 bekommt Spieler 1, Hof 2
    /// Spieler 2 usw. – ob Mensch oder KI, entscheidet erst die Partie. Eine Hofstelle mit Nummer ist immer ein Startplatz. Hat eine Karte
    /// nummerierte Höfe, sind nur sie die Startplätze, und die Partie vergibt sie nach der Nummer statt durch das Los
    /// (<see cref="Hofstellenvergabe.Zuteilen"/>). Null: eine Hofstelle ohne Nummer wie bisher.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Nummer { get; init; }

    /// <summary>Gehört dieser Platz der Karte (Kennung) zur Hofstelle?</summary>
    public bool Umfasst(string? platz) => platz is not null && (platz == Werkstatt || platz == Wohnhaus);

    /// <summary>Der Name für Anzeigen: <see cref="Name"/>, sonst die Kennung.</summary>
    [JsonIgnore]
    public string Anzeigename => string.IsNullOrWhiteSpace(Name) ? Kennung : Name;
}

/// <summary>Wie die Startplätze einer neuen Partie vergeben werden (<see cref="NeuePartie.Startplaetze"/>).</summary>
public enum Startplatzvergabe
{
    /// <summary>
    /// Wie im Spiel: Der Spieler bekommt die gewählte Hofstelle (<see cref="NeuePartie.Hofstelle"/>, ohne Wahl gelost), jedes mitspielende
    /// KI-Haus eine geloste unter den übrigen Start-Hofstellen.
    /// </summary>
    Gelost,

    /// <summary>
    /// Ohne Los: Jedes Haus sitzt, wo die Startbelegung der Karte es hinsetzt (der Start vor den Hofstellen) – für Tests, Vergleichsmessungen
    /// und „Karte ausprobieren“ mit festen Plätzen. Freie Hofstellen werden trotzdem Bauplätze.
    /// </summary>
    Vorgabe,
}
