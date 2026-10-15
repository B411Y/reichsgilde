using Godot;

namespace Reichsgilde;

/// <summary>Kleine Helfer für die Oberfläche.</summary>
public static class UiHilfe
{
    /// <summary>
    /// Entfernt alle Kinder eines Knotens – SOFORT aus dem Baum, gelöscht wird am Ende des Bildes.
    ///
    /// Warum nicht nur QueueFree()? QueueFree löscht erst am Ende des Bildes. Bis dahin zählen die alten
    /// Kinder noch bei der Größe mit: Baut man im selben Bild neue Zeilen auf, ist ein Container kurz
    /// doppelt so hoch – und wer genau dann Größe oder Position berechnet, rechnet falsch.
    /// </summary>
    public static void KinderEntfernen(Node eltern)
    {
        foreach (var kind in eltern.GetChildren())
        {
            eltern.RemoveChild(kind);
            kind.QueueFree();
        }
    }

    /// <summary>Was ein Betrieb ohne Ware tut – für Kontor und Liegenschaftsfenster.</summary>
    public static string Taetigkeit(string beruf) => beruf switch
    {
        "Dieb" => "Taschendiebe",
        "Geldleiher" => "Geldgeschäfte",
        _ => "Wachdienst",
    };

    /// <summary>
    /// Hängt die Aktionspunkte-Kosten an einen Knopf („Werben · 1 AP") und sperrt ihn, wenn der Vorrat nicht reicht.
    /// Die Kosten hängen nur von der Art des Befehls ab – Betrag oder Ziel spielen keine Rolle.
    /// </summary>
    public static void ApKosten(Button knopf, Reichsgilde.Core.Wirtschaft.Stadtwirtschaft w, Reichsgilde.Core.Wirtschaft.Konto konto,
        Reichsgilde.Core.Wirtschaft.Befehl befehl)
    {
        var kosten = w.ApKosten(konto, befehl);
        if (kosten <= 0) return;
        knopf.Text += $" · {kosten} AP";
        if (konto.Aktionspunkte.Reicht(kosten) || knopf.Disabled) return;
        knopf.Disabled = true;
        knopf.TooltipText = $"Nicht genug Aktionspunkte: {kosten} nötig, {konto.Aktionspunkte.Vorrat} übrig. " +
                            $"Zum Jahreswechsel kommen {w.AktionspunkteJeRunde} dazu.";
    }
}
