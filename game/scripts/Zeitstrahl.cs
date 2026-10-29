using Godot;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Das Band des Zeitstrahls oben in der Mitte (PR #197): der Spieltag von 4 bis 23 Uhr in seinen vier Phasen (Zunftglocke, Hoher Mittag, Vesper,
/// Nacht), Striche für die vollen Stunden und als Zeiger die Sonne – nachts der Mond. Wann es Nacht ist, sagen die Sonnenzeiten der Jahreszeit
/// (<see cref="Sonnenzeiten"/>, dieselben wie beim Licht der Stadt): Die Felder dunkeln vor der Dämmerung und nach dem Dunkelwerden ab, dazwischen
/// gehen sie weich ineinander über; Sonne als Zeiger zwischen Aufgang und Untergang, sonst der Mond. Nur gezeichnet, keine Knöpfe: Die Hauptszene
/// setzt jedes Bild die Minute (<see cref="Minute"/>), gezeichnet wird nur, wenn sie sich ändert. Die Gestaltung im Holzschnitt-Stil kommt später.
/// </summary>
public sealed partial class Zeitstrahl : Control
{
    private const float Bandhoehe = 20, Zeigerplatz = 16;

    /// <summary>Schriftgrad der Namen im Band (nicht unter <see cref="Schriften.MindestGroesse"/>).</summary>
    private const int Schriftgrad = Schriften.MindestGroesse;

    /// <summary>Minuten seit Tagesbeginn zu einer Uhrzeit (volle Stunde).</summary>
    private static int Ab(int stunde) => stunde * 60 - Spielzeit.TagesbeginnMinute;

    /// <summary>Beginn jeder Phase in Minuten seit Tagesbeginn (wie <see cref="Spielzeit.Tagesphase"/>), dazu ihre Farbe und ihr kurzer Name.</summary>
    private static readonly (int Ab, Color Farbe, string Name)[] Phasen =
    {
        (0, new Color(0.62f, 0.55f, 0.40f), "Zunftglocke"),
        (Ab(10), new Color(0.80f, 0.68f, 0.36f), "Hoher Mittag"),
        (Ab(15), new Color(0.62f, 0.40f, 0.26f), "Vesper"),
        (Ab(20), new Color(0.18f, 0.20f, 0.32f), "Nacht"),
    };

    private int _minute = -1;
    private Jahreszeit _jahreszeit = Jahreszeit.Fruehling;

    /// <summary>Die Jahreszeit, deren Sonnenzeiten das Band zeigt.</summary>
    public Jahreszeit Jahreszeit
    {
        get => _jahreszeit;
        set
        {
            if (value == _jahreszeit) return;
            _jahreszeit = value;
            TooltipSetzen();
            QueueRedraw();
        }
    }

    /// <summary>Wie hell es zu einer Minute seit Tagesbeginn ist (0 = Nacht, 1 = Tag): von der Dämmerung bis zum Aufgang und vom Untergang bis zum Dunkelwerden gleitet es.</summary>
    private float Tageslicht(int minute)
    {
        var lauf = Sonnenzeiten.Fuer(_jahreszeit);
        var uhr = minute + Spielzeit.TagesbeginnMinute;
        return uhr < lauf.Aufgang ? Mathf.SmoothStep(lauf.Daemmerung, lauf.Aufgang, uhr) : 1 - Mathf.SmoothStep(lauf.Untergang, lauf.Dunkel, uhr);
    }

    private static readonly Color NachtFarbe = new(0.18f, 0.20f, 0.32f);

    /// <summary>Minuten seit Tagesbeginn (0 = 04:00, 1140 = 23:00).</summary>
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
        TooltipSetzen();
    }

    private void TooltipSetzen()
    {
        static string Uhr(int minute) => $"{minute / 60}:{minute % 60:00}";
        var lauf = Sonnenzeiten.Fuer(_jahreszeit);
        TooltipText = "Der Spieltag von 4 bis 23 Uhr: Frühe Zunftglocke (4–10), Hoher Mittag (10–15), Vesper & Zwielicht (15–20), Dunkle Nacht (20–23).\n"
                      + "Gearbeitet wird von 8 bis 20 Uhr (im Wirtshaus bis 22 Uhr), Gauner ziehen ab 20 Uhr los. Danach folgt die Jahresrechnung.\n"
                      + $"Sonne ({KalenderTexte.Name(_jahreszeit)}): Dämmerung ab {Uhr(lauf.Daemmerung)}, Aufgang {Uhr(lauf.Aufgang)}, Untergang {Uhr(lauf.Untergang)}, dunkel ab {Uhr(lauf.Dunkel)} Uhr. Das Band wird dort dunkel.";
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
            // Das Feld in schmalen Streifen: Tagesfarbe der Phase (die Nacht-Phase zeigt bei Tag die Farbe der Vesper), je nach Tageslicht zur Nachtfarbe abgedunkelt
            var tagFarbe = i == Phasen.Length - 1 ? Phasen[i - 1].Farbe : farbe;
            for (var x = feld.Position.X; x < feld.End.X; x += 2)
            {
                var minute = (int)((x + 1) / Size.X * Spielzeit.MinutenProTag);
                DrawRect(new Rect2(x, oben, Mathf.Min(2, feld.End.X - x), Bandhoehe), NachtFarbe.Lerp(tagFarbe, Tageslicht(minute)));
            }
            // Der Name klein ins Feld, wenn er hineinpasst
            var breite = schrift.GetStringSize(name, HorizontalAlignment.Left, -1, Schriftgrad).X;
            if (breite + 4 < feld.Size.X)
                DrawString(schrift, new Vector2(feld.Position.X + (feld.Size.X - breite) / 2, oben + Bandhoehe - 5), name, HorizontalAlignment.Left, -1, Schriftgrad,
                           Tageslicht((ab + bis) / 2) < 0.5f ? new Color(0.92f, 0.92f, 0.96f) : new Color(0.08f, 0.06f, 0.04f));
        }
        // Volle Stunden als kurze Striche am unteren Rand, die Arbeitszeit (8–20 Uhr) als dünne Linie darunter
        for (var stunde = Spielzeit.TagesbeginnMinute / 60 + 1; stunde < Spielzeit.TagesendeMinute / 60; stunde++)
        {
            var x = X(Ab(stunde));
            DrawLine(new Vector2(x, oben + Bandhoehe - 3), new Vector2(x, oben + Bandhoehe), new Color(0, 0, 0, 0.5f));
        }
        DrawLine(new Vector2(X(Ab(8)), oben + Bandhoehe + 1), new Vector2(X(Ab(20)), oben + Bandhoehe + 1), new Color(0.95f, 0.82f, 0.45f, 0.7f), 2);
        DrawRect(new Rect2(0, oben, Size.X, Bandhoehe), new Color(0.05f, 0.04f, 0.03f), false, 1);

        if (_minute < 0) return;
        // Der Zeiger: eine Sonne mit Strahlen über dem Band, in der Nacht ein Mond
        // Die Marke sitzt ganz über dem Band (F7: Sie verdeckte sonst den Namen des Abschnitts); nur eine kurze Spitze zeigt auf die Minute.
        var mitte = new Vector2(Mathf.Clamp(X(_minute), 7, Size.X - 7), oben - 8);
        DrawLine(new Vector2(mitte.X, oben - 1), new Vector2(mitte.X, oben + 3), new Color(0.1f, 0.08f, 0.06f), 2);
        if (Sonnenzeiten.IstTag(_jahreszeit, _minute + Spielzeit.TagesbeginnMinute))
        {
            for (var i = 0; i < 8; i++)
            {
                var richtung = Vector2.Right.Rotated(Mathf.Tau * i / 8);
                DrawLine(mitte + richtung * 5.5f, mitte + richtung * 7.5f, new Color(0.98f, 0.8f, 0.25f), 1.5f);
            }
            DrawCircle(mitte, 4, new Color(0.98f, 0.82f, 0.3f));
            DrawArc(mitte, 4, 0, Mathf.Tau, 16, new Color(0.35f, 0.22f, 0.08f), 1);
        }
        else
        {
            DrawCircle(mitte, 6, new Color(0.9f, 0.9f, 0.82f));
            DrawCircle(mitte + new Vector2(3, -2), 5, new Color(0.16f, 0.15f, 0.13f)); // Sichel: Grund der Tafel darüber
            DrawArc(mitte, 6, 0, Mathf.Tau, 16, new Color(0.1f, 0.1f, 0.14f), 1);
        }
    }
}
