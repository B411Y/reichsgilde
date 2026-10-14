using Godot;

namespace Reichsgilde;

/// <summary>
/// Ein Zahlenfeld, das getippte Zahlen auch dann übernimmt, wenn man danach gleich einen Knopf klickt.
///
/// Warum? Godots SpinBox übernimmt den getippten Text erst bei Enter oder wenn das Feld den Fokus verliert.
/// Unsere Knöpfe nehmen aber absichtlich keinen Fokus (FocusMode None – sonst würde die Leertaste, unsere
/// Pausetaste, den zuletzt geklickten Knopf noch einmal drücken). Ein Klick auf „Partie beginnen" ließ das
/// Feld also im Fokus, und der Knopf las den alten Wert. Darum übernimmt das Feld die Eingabe selbst, sobald
/// irgendwo außerhalb geklickt wird. _Input läuft vor der Oberfläche – der Knopf sieht schon den neuen Wert.
/// </summary>
public partial class Zahlfeld : SpinBox
{
    public override void _Input(InputEvent ereignis)
    {
        if (ereignis is not InputEventMouseButton { Pressed: true }) return;
        var eingabe = GetLineEdit();
        if (!eingabe.HasFocus() || GetGlobalRect().HasPoint(GetGlobalMousePosition())) return;
        Apply();
        eingabe.ReleaseFocus();
    }
}
