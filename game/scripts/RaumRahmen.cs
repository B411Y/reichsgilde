using Godot;

namespace Reichsgilde;

/// <summary>Der goldene Auswahlrahmen, den man in der Raumansicht mit gedrückter linker Taste um mehrere Arbeiter zieht. Fängt keine Mausereignisse ab.</summary>
public partial class RaumRahmen : Control
{
    private Rect2? _rahmen;

    /// <summary>Der gezogene Rahmen in Bildschirmpunkten; null: keiner.</summary>
    public Rect2? Rahmen
    {
        get => _rahmen;
        set
        {
            if (_rahmen == value) return;
            _rahmen = value;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Draw()
    {
        if (_rahmen is not { } r) return;
        DrawRect(r, new Color(0.95f, 0.78f, 0.34f, 0.14f), true);
        DrawRect(r, new Color(0.09f, 0.07f, 0.05f), false, 3f);
        DrawRect(new Rect2(r.Position + new Vector2(2, 2), r.Size), new Color(0.95f, 0.78f, 0.34f), false, 1.5f);
    }
}
