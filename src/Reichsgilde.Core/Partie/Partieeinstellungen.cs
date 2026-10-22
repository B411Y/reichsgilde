using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Partie;

/// <summary>
/// Wann endet eine Partie? Der Spieler wählt das beim Start der Partie.
/// </summary>
public enum Siegbedingung
{
    /// <summary>Die Partie endet in einem festen Jahr (Standard: 1600 wie im Original).</summary>
    Endjahr,

    /// <summary>Wer zuerst ein Gesamtvermögen erreicht, gewinnt.</summary>
    Zielvermoegen,

    /// <summary>Die letzte Dynastie, die noch existiert, gewinnt (nur sinnvoll mit Rivalen).</summary>
    LetzteDynastie,

    /// <summary>Kein Ende – freies Spiel.</summary>
    Endlos,
}

/// <summary>Wie die KI-Häuser anfangen (Entscheidung des Nutzers zu PR #177). Beim Start der Partie wählbar.</summary>
public enum Schwierigkeit
{
    /// <summary>Alle Häuser starten wie der Spieler: eine Werkstatt ihres Berufs mit zwei Gesellen, kein Umlandbetrieb.</summary>
    Normal,

    /// <summary>Die KI-Häuser starten mit allen Betrieben ihrer Startbelegung (Werkstatt und Umlandbetrieb) – der Start vor PR #177.</summary>
    Schwer,
}

/// <summary>
/// Alle Einstellungen, die beim Start einer Partie festgelegt werden.
///
/// Ein „record" ist eine Klasse für reine Daten: Mit <c>with</c> kann man eine Kopie
/// mit geänderten Werten erzeugen, z. B.
/// <code>var schnell = Partieeinstellungen.Standard with { RundenDauerMinuten = 5 };</code>
/// </summary>
public sealed record Partieeinstellungen
{
    public const int MinAktionspunkte = 1;
    public const int MaxAktionspunkte = 12;

    /// <summary>Standardwerte (Entscheidungen vom 13.10.2026).</summary>
    public static Partieeinstellungen Standard { get; } = new();

    /// <summary>Aktionspunkte, die jede Runde dazukommen. Standard 4 wie im Original.</summary>
    public int AktionspunkteProRunde { get; init; } = 4;

    /// <summary>Wie lange ein Spieljahr bei Tempo 1 dauert. Standard 15 Minuten wie im Original.</summary>
    public double RundenDauerMinuten { get; init; } = Taktgeber.StandardRundenDauerSekunden / 60;

    public Siegbedingung Siegbedingung { get; init; } = Siegbedingung.Endjahr;

    /// <summary>Wie die KI-Häuser anfangen. Fehlt in alten Spielständen – dann Normal (wirkt nur beim Aufstellen der Partie).</summary>
    public Schwierigkeit Schwierigkeit { get; init; } = Schwierigkeit.Normal;

    /// <summary>Nur für <see cref="Siegbedingung.Endjahr"/>: In diesem Jahr endet die Partie.</summary>
    public int Endjahr { get; init; } = 1600;

    /// <summary>Nur für <see cref="Siegbedingung.Zielvermoegen"/>: benötigtes Gesamtvermögen in Gulden.</summary>
    public long Zielvermoegen { get; init; } = 200_000;

    /// <summary>Fron- und Sklavenstatut zu Spielbeginn. Standard B (Bestienfron) laut Design-Dossier.</summary>
    public Fronstatut StartStatut { get; init; } = Fronstatut.Bestienfron;

    /// <summary>
    /// Startwert für alle Zufallsereignisse. Gleicher Wert = gleicher Spielverlauf
    /// (wichtig für Multiplayer und Tests). Für jede neue Partie wird später ein neuer Wert gewürfelt.
    /// </summary>
    public int Zufallsstartwert { get; init; } = 1400;

    /// <summary>
    /// Prüft die Einstellungen und liefert eine Liste verständlicher Fehlermeldungen.
    /// Leere Liste = alles in Ordnung.
    /// </summary>
    public IReadOnlyList<string> Pruefen()
    {
        var fehler = new List<string>();

        if (AktionspunkteProRunde is < MinAktionspunkte or > MaxAktionspunkte)
            fehler.Add($"Aktionspunkte pro Runde müssen zwischen {MinAktionspunkte} und {MaxAktionspunkte} liegen.");

        var sekunden = RundenDauerMinuten * 60;
        if (sekunden < Taktgeber.MinRundenDauerSekunden || sekunden > Taktgeber.MaxRundenDauerSekunden)
            fehler.Add($"Die Rundendauer muss zwischen {Taktgeber.MinRundenDauerSekunden / 60:0} und {Taktgeber.MaxRundenDauerSekunden / 60:0} Minuten liegen.");

        if (Siegbedingung == Siegbedingung.Endjahr && Endjahr <= Spielzeit.StartJahr)
            fehler.Add($"Das Endjahr muss nach {Spielzeit.StartJahr} liegen.");

        if (Siegbedingung == Siegbedingung.Zielvermoegen && Zielvermoegen <= 0)
            fehler.Add("Das Zielvermögen muss größer als 0 sein.");

        return fehler;
    }

    /// <summary>Kurzbeschreibung für die Anzeige, z. B. „Partie endet 1600".</summary>
    public string SiegbedingungText => Siegbedingung switch
    {
        Siegbedingung.Endjahr => $"Partie endet {Endjahr}",
        Siegbedingung.Zielvermoegen => $"Ziel: {Zahlenformat.Ganz(Zielvermoegen)} Gulden Vermögen",
        Siegbedingung.LetzteDynastie => "Die letzte Dynastie gewinnt",
        Siegbedingung.Endlos => "Endloses Spiel",
        _ => Siegbedingung.ToString(),
    };
}
