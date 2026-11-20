using Godot;

namespace Reichsgilde;

/// <summary>
/// Die gepunktete Linie zwischen zwei Taten einer Gruppe im Schwarzen Buch (wie „border-bottom: 1px dotted“ im Entwurf). Godots Flächen
/// (StyleBox) kennen keine Punkte, darum zeichnet dieser Knoten selbst – Farbe („punktlinie“), Punktlänge („punkt“) und Höhe
/// („punktlinie_hoehe“) kommen aus dem Thema des Hinterzimmers.
/// </summary>
public partial class HinterzimmerPunktlinie : Control
{
    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, HinterzimmerStil.Mass("punktlinie_hoehe"));
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        var y = Mathf.Floor(Size.Y / 2) + 0.5f;
        DrawDashedLine(new Vector2(0, y), new Vector2(Size.X, y), HinterzimmerStil.Farbe("punktlinie"), 1, HinterzimmerStil.Mass("punkt"), false);
    }
}
