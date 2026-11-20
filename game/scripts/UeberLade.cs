using Godot;

namespace Reichsgilde;

/// <summary>
/// Behälter über das ganze Bild für Tafeln ohne <see cref="Fensterrahmen"/> (Spielmenü, Einstellungen, Hilfe): Er stellt jedes sichtbare Kind dorthin, wo auch
/// die Fenster stehen – waagrecht in der Bildmitte, die Unterkante „fenster_unten“ über dem Bildrand, also über der Lade (<see cref="Fensterrahmen.LageUeberLade"/>).
/// Früher lagen diese Tafeln in einem CenterContainer und verdeckten die Lade.
/// </summary>
public partial class UeberLade : Container
{
    /// <summary>Ein Behälter über das ganze Bild, der selbst keine Maus fängt.</summary>
    public static UeberLade Neu()
    {
        var mitte = new UeberLade { MouseFilter = MouseFilterEnum.Ignore };
        mitte.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return mitte;
    }

    public override void _Notification(int was)
    {
        if (was != NotificationSortChildren) return;
        foreach (var kind in GetChildren())
        {
            if (kind is not Control tafel || !tafel.Visible) continue;
            if (tafel.GetNodeOrNull<Rollbereich>("Rollbereich") is { } rolle)
            {
                // Wie beim Fensterrahmen: Eine hohe Tafel rollt, bevor sie Zeitleiste oder Lade verdeckt.
                rolle.ObenFrei = Rollbereich.KopfHoehe - rolle.Abstand;
                rolle.UntenFrei = Rollbereich.LeistenHoehe - rolle.Abstand;
            }
            var groesse = tafel.GetCombinedMinimumSize();
            FitChildInRect(tafel, new Rect2(Fensterrahmen.LageUeberLade(groesse, Size), groesse));
        }
    }
}
