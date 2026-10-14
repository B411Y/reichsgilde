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

    /// <summary>
    /// Übernimmt eine getippte Zahl, sobald woanders hingeklickt wird.
    ///
    /// Warum? Ein SpinBox übernimmt getippten Text erst bei Enter oder wenn er den Fokus verliert. Unsere Knöpfe
    /// nehmen absichtlich keinen Fokus an (sonst löste die Leertaste – Pause – den zuletzt geklickten Knopf aus).
    /// Klickt man nach dem Tippen direkt „Partie beginnen“, behält das Feld den Fokus und der alte Wert gilt.
    /// Deshalb lösen wir den Fokus beim Drücken der Maustaste selbst – der Knopf löst erst beim Loslassen aus,
    /// liest also schon den neuen Wert. Aus _Input aufrufen (vor der Oberfläche), nicht aus _UnhandledInput.
    /// </summary>
    public static void ZahlBeiKlickUebernehmen(Viewport viewport, InputEvent eingabe)
    {
        if (eingabe is not InputEventMouseButton { Pressed: true } klick)
            return;
        if (viewport.GuiGetFocusOwner() is not LineEdit { } feld || feld.GetParent() is not SpinBox zahl)
            return;
        if (zahl.GetGlobalRect().HasPoint(klick.Position))
            return; // Klick ins Feld selbst oder auf die Pfeile – weiter tippen
        zahl.Apply();
        feld.ReleaseFocus();
    }
}
