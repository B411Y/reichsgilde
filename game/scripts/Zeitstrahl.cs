using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Das Band des Zeitstrahls oben in der Mitte (PR #197): der Spieltag von 5 bis 22 Uhr in seinen vier Phasen (Zunftglocke, Hoher Mittag, Vesper,
/// Nacht), Striche für die vollen Stunden und als Zeiger die Sonne – in der Dunklen Nacht der Mond. Nur gezeichnet, keine Knöpfe: Die Hauptszene
/// setzt jedes Bild die Minute (<see cref="Minute"/>), gezeichnet wird nur, wenn sie sich ändert. Die Gestaltung im Holzschnitt-Stil kommt später.
/// </summary>
public sealed partial class Zeitstrahl : Control
{
    private const float Bandhoehe = 16, Zeigerplatz = 12;

    /// <summary>Beginn jeder Phase in Minuten seit 5 Uhr (wie <see cref="Spielzeit.Tagesphase"/>), dazu ihre Farbe und ihr kurzer Name.</summary>
    private static readonly (int Ab, Color Farbe, string Name)[] Phasen =
    {
        (0, new Color(0.62f, 0.55f, 0.40f), "Zunftglocke"),
        (5 * 60, new Color(0.80f, 0.68f, 0.36f), "Hoher Mittag"),
        (10 * 60, new Color(0.62f, 0.40f, 0.26f), "Vesper"),
        (15 * 60, new Color(0.18f, 0.20f, 0.32f), "Nacht"),
    };

    private int _minute = -1;

    /// <summary>Minuten seit Tagesbeginn (0 = 05:00, 1020 = 22:00).</summary>
    public int Minute
    {
        get => _minute;
        set
        {
            if (value == _minute) return;
            _minute = value;
            QueueRedraw();
        }
    }

    public Zeitstrahl()
    {
        CustomMinimumSize = new Vector2(340, Bandhoehe + Zeigerplatz + 2);
        MouseFilter = MouseFilterEnum.Stop; // für den Tooltip
        TooltipText = "Der Spieltag von 5 bis 22 Uhr: Frühe Zunftglocke (5–10), Hoher Mittag (10–15), Vesper & Zwielicht (15–20), Dunkle Nacht (20–22).\n"
                      + "Gearbeitet wird von 8 bis 20 Uhr (im Wirtshaus bis 22 Uhr), Gauner ziehen ab 20 Uhr los. Danach folgt die Jahresrechnung.";
    }

    private float X(int minute) => Size.X * minute / Spielzeit.MinutenProTag;

    public override void _Draw()
    {
        var oben = Zeigerplatz;
        var schrift = GetThemeDefaultFont();
        for (var i = 0; i < Phasen.Length; i++)
        {
            var (ab, farbe, name) = Phasen[i];
            var bis = i + 1 < Phasen.Length ? Phasen[i + 1].Ab : Spielzeit.MinutenProTag;
            var feld = new Rect2(X(ab), oben, X(bis) - X(ab), Bandhoehe);
            DrawRect(feld, farbe);
            // Der Name klein ins Feld, wenn er hineinpasst
            var breite = schrift.GetStringSize(name, HorizontalAlignment.Left, -1, 11).X;
            if (breite + 4 < feld.Size.X)
                DrawString(schrift, new Vector2(feld.Position.X + (feld.Size.X - breite) / 2, oben + Bandhoehe - 4), name, HorizontalAlignment.Left, -1, 11,
                           i == Phasen.Length - 1 ? new Color(0.85f, 0.85f, 0.9f) : new Color(0.12f, 0.1f, 0.08f));
        }
        // Volle Stunden als kurze Striche am unteren Rand, die Arbeitszeit (8–20 Uhr) als dünne Linie darunter
        for (var stunde = 6; stunde < 22; stunde++)
        {
            var x = X((stunde - 5) * 60);
            DrawLine(new Vector2(x, oben + Bandhoehe - 3), new Vector2(x, oben + Bandhoehe), new Color(0, 0, 0, 0.5f));
        }
        DrawLine(new Vector2(X(3 * 60), oben + Bandhoehe + 1), new Vector2(X(15 * 60), oben + Bandhoehe + 1), new Color(0.95f, 0.82f, 0.45f, 0.7f), 2);
        DrawRect(new Rect2(0, oben, Size.X, Bandhoehe), new Color(0.05f, 0.04f, 0.03f), false, 1);

        if (_minute < 0) return;
        // Der Zeiger: eine Sonne mit Strahlen über dem Band, in der Nacht ein Mond
        var mitte = new Vector2(Mathf.Clamp(X(_minute), 7, Size.X - 7), oben - 1);
        DrawLine(new Vector2(mitte.X, oben), new Vector2(mitte.X, oben + Bandhoehe), new Color(0.1f, 0.08f, 0.06f), 2);
        if (_minute < 15 * 60)
        {
            for (var i = 0; i < 8; i++)
            {
                var richtung = Vector2.Right.Rotated(Mathf.Tau * i / 8);
                DrawLine(mitte + richtung * 6, mitte + richtung * 9, new Color(0.98f, 0.8f, 0.25f), 1.5f);
            }
            DrawCircle(mitte, 5, new Color(0.98f, 0.82f, 0.3f));
            DrawArc(mitte, 5, 0, Mathf.Tau, 16, new Color(0.35f, 0.22f, 0.08f), 1);
        }
        else
        {
            DrawCircle(mitte, 6, new Color(0.9f, 0.9f, 0.82f));
            DrawCircle(mitte + new Vector2(3, -2), 5, new Color(0.16f, 0.15f, 0.13f)); // Sichel: Grund der Tafel darüber
            DrawArc(mitte, 6, 0, Mathf.Tau, 16, new Color(0.1f, 0.1f, 0.14f), 1);
        }
    }
}
