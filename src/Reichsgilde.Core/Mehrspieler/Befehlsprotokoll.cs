using System.Text.Json;
using Reichsgilde.Core.Speichern;

namespace Reichsgilde.Core.Mehrspieler;

/// <summary>
/// Ein Befehlsprotokoll (Mehrspieler M2): Anfangszustand plus alle Befehle mit ihrem Ziel-Tick. Weil die Simulation bei gleichem Zustand und
/// gleichen Befehlen genau gleich weiterläuft, genügt das, um eine Partie bis <see cref="Endtick"/> nachzurechnen – später die Wiederholung,
/// wenn zwei Rechner im Mehrspieler auseinanderlaufen (Desync), und heute ein Prüfstein für den Gleichschritt.
///
/// Aufgezeichnet wird mit <see cref="Befehlsbote.AufzeichnungBeginnen"/> und <see cref="Befehlsbote.AufzeichnungBeenden"/>.
/// </summary>
/// <param name="Anfangszustand">Der Spielstand beim Beginn der Aufzeichnung (JSON aus <see cref="Speicherung.Speichern"/>).</param>
/// <param name="Befehle">Alle eingereihten oder empfangenen Befehle in der Reihenfolge, in der sie dazukamen.</param>
/// <param name="Endtick">Bis zu diesem Tick (<see cref="Zeit.Spielzeit.VergangeneMinuten"/>) lief die Partie.</param>
/// <param name="EndtickAusgefuehrt">Wurden die Befehle des Endticks schon ausgeführt (ein Befehl wirkte nach dem letzten Tick sofort, z. B. in
/// der Pause)? Sonst warten sie noch auf den Anfang dieses Ticks.</param>
/// <param name="Luecke">null, wenn das Protokoll vollständig ist – sonst, was außer Befehlen den Zustand geändert hat (z. B. eine angehaltene
/// Ratssitzung). Ein unvollständiges Protokoll lässt sich nicht abspielen.</param>
public sealed record Befehlsprotokoll(string Anfangszustand, IReadOnlyList<EingereihterBefehl> Befehle, long Endtick,
    bool EndtickAusgefuehrt = false, string? Luecke = null)
{
    public bool IstVollstaendig => Luecke is null;

    /// <summary>
    /// Rechnet die Partie nach: Anfangszustand laden, alle Befehle mit ihrem Ziel-Tick einreihen, Ticks bis zum <see cref="Endtick"/>, zuletzt
    /// die Befehle, die danach noch sofort wirkten. Ergebnis: dieselbe Partie wie am Ende der Aufzeichnung – Zeichen für Zeichen derselbe Spielstand.
    /// </summary>
    /// <param name="karte">Wie bei <see cref="Speicherung.Laden"/> (nur für Spielstände ohne mitgespeicherte Karte).</param>
    /// <exception cref="InvalidOperationException">Wenn das Protokoll unvollständig ist.</exception>
    public Simulation Abspielen(Bauen.Stadtkarte? karte = null)
    {
        if (Luecke is not null) throw new InvalidOperationException("Das Protokoll ist unvollständig: " + Luecke);
        var (simulation, _) = Speicherung.Laden(Anfangszustand, karte);
        foreach (var eintrag in Befehle)
            simulation.Bote.Empfangen(eintrag);
        while (simulation.Zeit.VergangeneMinuten < Endtick && !simulation.IstBeendet)
            simulation.Tick();
        // Was nach dem letzten Tick sofort wirkte (z. B. in der Pause), steht mit dem Ziel-Tick Endtick in der Schlange
        if (EndtickAusgefuehrt) simulation.Bote.FaelligeAusfuehren(simulation.Zeit.VergangeneMinuten);
        return simulation;
    }

    /// <summary>Das Protokoll als JSON-Text (Befehle wie im Spielstand, <see cref="BefehlKonverter"/>).</summary>
    public string AlsJson() => JsonSerializer.Serialize(this, Speicherung.Optionen);

    /// <summary>Liest ein Protokoll aus <see cref="AlsJson"/>.</summary>
    /// <exception cref="SpielstandFehler">Wenn der Text kein Protokoll ist.</exception>
    public static Befehlsprotokoll AusJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Befehlsprotokoll>(json, Speicherung.Optionen) ?? throw new SpielstandFehler("Das Protokoll ist leer.");
        }
        catch (JsonException ex)
        {
            throw new SpielstandFehler($"Die Datei ist kein gültiges Befehlsprotokoll: {ex.Message}", ex);
        }
    }
}
