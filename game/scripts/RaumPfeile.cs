using Godot;

namespace Reichsgilde;

/// <summary>
/// Die goldenen Pfeile an den Bildrändern der Raumansicht: Sie zeigen, wie oft die Kamera in dieser Richtung noch zu einem
/// anderen Blickpunkt schwenken kann – ein einfacher Pfeil für einmal, ein Doppelpfeil für zweimal oder öfter, keiner am Ende.
///
/// Gezeichnet im Stil der Oberfläche (kolorierter Holzschnitt): ein dunkler Umriss mit Schraffur, die goldene Farbe sitzt
/// leicht neben den Linien. Der Pfeil fängt keine Mausereignisse ab.
/// </summary>
public partial class RaumPfeile : Control
{
    private static readonly Color Gold = new(0.95f, 0.78f, 0.34f);
    private static readonly Color Umriss = new(0.09f, 0.07f, 0.05f);

    /// <summary>Ein Winkel (Pfeilspitze) nach rechts, um den Ursprung: dick, mit Kerbe hinten.</summary>
    private static readonly Vector2[] Spitze =
    {
        new(-9, -24), new(3, -24), new(21, 0), new(3, 24), new(-9, 24), new(7, 0),
    };

    private readonly int[] _anzahl = new int[4]; // links, rechts, oben, unten

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>Wie oft es nach links, rechts, oben und unten noch weitergeht.</summary>
    public void Setzen(int links, int rechts, int oben, int unten)
    {
        if (_anzahl[0] == links && _anzahl[1] == rechts && _anzahl[2] == oben && _anzahl[3] == unten) return;
        _anzahl[0] = links; _anzahl[1] = rechts; _anzahl[2] = oben; _anzahl[3] = unten;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var groesse = Size;
        const float rand = 30;
        Zeichnen(_anzahl[0], new Vector2(rand, groesse.Y / 2), Mathf.Pi);
        Zeichnen(_anzahl[1], new Vector2(groesse.X - rand, groesse.Y / 2), 0);
        Zeichnen(_anzahl[2], new Vector2(groesse.X / 2, rand), -Mathf.Pi / 2);
        Zeichnen(_anzahl[3], new Vector2(groesse.X / 2, groesse.Y - rand), Mathf.Pi / 2);
    }

    /// <summary>Ein einfacher oder doppelter Pfeil mit der Spitze in Richtung <paramref name="winkel"/> (0 = rechts); der zweite sitzt zur Bildmitte hin.</summary>
    private void Zeichnen(int anzahl, Vector2 ort, float winkel)
    {
        for (var i = 0; i < Mathf.Min(anzahl, 2); i++)
        {
            DrawSetTransform(ort, winkel, Vector2.One);
            var punkte = Spitze.Select(p => p - new Vector2(i * 19, 0)).ToArray();
            DrawColoredPolygon(punkte.Select(p => p + new Vector2(2.5f, 2.5f)).ToArray(), Gold); // die Farbe sitzt neben den Linien
            DrawPolyline(punkte.Append(punkte[0]).ToArray(), Umriss, 3f, true);
            // Schraffur im Schatten: zwei kurze Striche über der unteren Hälfte
            DrawLine(punkte[3] + new Vector2(-3, -5), punkte[4] + new Vector2(4, -8), Umriss, 1.5f);
            DrawLine(punkte[3] + new Vector2(-3, -11), punkte[4] + new Vector2(4, -14), Umriss, 1.5f);
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
