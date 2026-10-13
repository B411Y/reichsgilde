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
}
