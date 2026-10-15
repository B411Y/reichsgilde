using Godot;
using Reichsgilde.Core.Dynastie;

namespace Reichsgilde;

/// <summary>
/// Zeichnet ein Wappen aus dem Kern (<see cref="Wappen"/>) als Vektorgrafik – ohne Bilddateien, scharf in jeder Größe.
///
/// So entsteht das Bild (alles in „Schildkoordinaten“: x von −1 bis 1, y von −1 oben bis 1,25 an der Spitze):
///   1. Schild in Farbe 1,
///   2. die Teilung bzw. das Band in Farbe 2 (mit dem Schild verschnitten: Geometry2D.IntersectPolygons),
///   3. die Figur in Farbe 2 – und wo sie über Farbe 2 liegt, das Stück noch einmal in Farbe 1 („verwechselte Farben“),
///   4. Schatten, Lichtverlauf und Rand, damit es nach bemaltem Holz aussieht und nicht nach Clipart.
/// </summary>
public partial class WappenBild : Control
{
    private Wappen? _wappen;

    /// <summary>Das gezeigte Wappen; null zeichnet nichts.</summary>
    public Wappen? Wappen
    {
        get => _wappen;
        set
        {
            if (Equals(_wappen, value)) return;
            _wappen = value;
            TooltipText = value?.Beschreibung() ?? "";
            QueueRedraw();
        }
    }

    /// <summary>Ein Wappen als Bildfeld mit <paramref name="groesse"/> Pixeln Höhe (Tooltip: die Wappenbeschreibung).</summary>
    public static WappenBild Neu(Wappen? wappen, int groesse) => new()
    {
        Wappen = wappen,
        CustomMinimumSize = new Vector2(MathF.Round(groesse * 0.9f), groesse),
        MouseFilter = MouseFilterEnum.Pass,
        SizeFlagsVertical = SizeFlags.ShrinkCenter,
    };

    /// <summary>Die Tinkturen in gedeckten, etwas dunklen Tönen – passend zur Stimmung des Spiels.</summary>
    public static Color Farbe(Tinktur t) => t switch
    {
        Tinktur.Gold => new Color("c9a23a"),
        Tinktur.Silber => new Color("d6d4cc"),
        Tinktur.Rot => new Color("9e2a2b"),
        Tinktur.Blau => new Color("2b4a86"),
        Tinktur.Gruen => new Color("2f6a3a"),
        Tinktur.Schwarz => new Color("2a2626"),
        _ => new Color("6b2f6b"),
    };

    private static readonly Color Randfarbe = new(0.08f, 0.06f, 0.05f);

    public override void _Draw()
    {
        if (_wappen is not { } w) return;

        // Schildkoordinaten → Pixel: der Schild (Breite 2, Höhe 2,25) füllt das Feld mit etwas Luft.
        var k = MathF.Min(Size.X / 2.2f, Size.Y / 2.45f);
        var mitte = new Vector2(Size.X / 2, Size.Y / 2 - 0.125f * k);
        Vector2[] P(Vector2[] punkte) => punkte.Select(p => mitte + p * k).ToArray();

        var schild = WappenFormen.Schild;
        var f1 = Farbe(w.Farbe1);
        var f2 = Farbe(w.Farbe2);

        // 1.–2. Feld und Teilung
        var zweitflaechen = new List<Vector2[]>();
        if (w.Teilung == Teilung.Bord)
        {
            Flaeche(P(schild), f2);
            Flaeche(P(WappenFormen.SchildInnen), f1);
        }
        else
        {
            Flaeche(P(schild), f1);
            foreach (var bereich in WappenFormen.Teilungsbereiche(w.Teilung))
                foreach (var stueck in Geometry2D.IntersectPolygons(schild, bereich))
                {
                    zweitflaechen.Add(stueck);
                    Flaeche(P(stueck), f2);
                }
        }

        // 3. Figur: Schatten, Figur, verwechselte Stücke
        var (versatz, massstab) = WappenFormen.Figurplatz(w.Teilung);
        var figur = WappenFormen.FigurTeile(w.Figur).Select(teil => teil.Select(p => versatz + p * massstab).ToArray()).ToList();
        var schatten = new Vector2(1, 1.2f) * MathF.Max(1, k * 0.035f);
        foreach (var teil in figur)
            DrawColoredPolygon(P(teil).Select(p => p + schatten).ToArray(), new Color(0, 0, 0, 0.35f));
        foreach (var teil in figur)
            Flaeche(P(teil), f2);
        foreach (var teil in figur)
            foreach (var bereich in zweitflaechen)
                foreach (var stueck in Geometry2D.IntersectPolygons(teil, bereich))
                    Flaeche(P(stueck), f1);

        // 4. Lichtverlauf (oben links hell, zur Spitze dunkel) und Rand
        var licht = schild.Select(p => p.Y < 0
            ? new Color(1, 1, 1, 0.10f * (1 - (p.Y + 1)) + (p.X < 0 ? 0.04f : 0))
            : new Color(0, 0, 0, 0.30f * p.Y / 1.25f)).ToArray();
        DrawPolygon(P(schild), licht);
        var rand = P(schild).Append(mitte + schild[0] * k).ToArray();
        DrawPolyline(rand, Randfarbe, MathF.Max(1.5f, k * 0.07f), true);
    }

    /// <summary>Eine Fläche mit geglättetem Rand (dünne Linie in derselben Farbe gegen Treppenstufen).</summary>
    private void Flaeche(Vector2[] punkte, Color farbe)
    {
        if (punkte.Length < 3) return;
        DrawColoredPolygon(punkte, farbe);
        DrawPolyline(punkte.Append(punkte[0]).ToArray(), farbe, 1, true);
    }
}

/// <summary>
/// Die Formen der Wappen in Schildkoordinaten: Schildumriss, Teilungen und die Figuren als Liste einfacher Vielecke.
/// Eine Figur besteht aus mehreren Teilen, die sich überlappen dürfen (z. B. Hammerkopf und Stiel).
/// Löcher gibt es keine – ein Rad besteht daher aus vielen Kranzstücken statt aus einem Ring mit Loch.
/// </summary>
public static class WappenFormen
{
    /// <summary>Der Schild: oben gerade, die Seiten senkrecht, unten zur Spitze gebogen.</summary>
    public static readonly Vector2[] Schild = SchildUmriss(1f, -1f, 0.1f, 1.25f);

    /// <summary>Das Innenfeld beim Bord (schmaler Rand ringsum).</summary>
    public static readonly Vector2[] SchildInnen = SchildUmriss(0.8f, -0.8f, 0.1f, 1.0f);

    private static Vector2[] SchildUmriss(float halbeBreite, float oben, float knick, float spitze)
    {
        var punkte = new List<Vector2> { new(-halbeBreite, oben), new(halbeBreite, oben), new(halbeBreite, knick) };
        // Rechte Seite: Bogen vom Knick zur Spitze (quadratische Bézierkurve), dann gespiegelt die linke.
        var rechts = new List<Vector2>();
        for (var i = 1; i <= 12; i++)
        {
            var t = i / 12f;
            var a = new Vector2(halbeBreite, knick);
            var b = new Vector2(halbeBreite, knick + (spitze - knick) * 0.55f);
            var c = new Vector2(0, spitze);
            rechts.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c);
        }
        punkte.AddRange(rechts);
        for (var i = rechts.Count - 2; i >= 0; i--) punkte.Add(new Vector2(-rechts[i].X, rechts[i].Y));
        punkte.Add(new Vector2(-halbeBreite, knick));
        return punkte.ToArray();
    }

    /// <summary>Die Bereiche in Farbe 2 – absichtlich größer als der Schild, sie werden mit ihm verschnitten.</summary>
    public static IEnumerable<Vector2[]> Teilungsbereiche(Teilung teilung) => teilung switch
    {
        Teilung.Geteilt => new[] { Rechteck(-2, 0.05f, 2, 2) },
        Teilung.Gespalten => new[] { Rechteck(0, -2, 2, 2) },
        Teilung.Schraeg => new[] { new Vector2[] { new(-2, -2), new(2, 2.45f), new(-2, 2.45f) } },
        Teilung.Geviert => new[] { Rechteck(0, -2, 2, 0.05f), Rechteck(-2, 0.05f, 0, 2) },
        Teilung.Schildhaupt => new[] { Rechteck(-2, -2, 2, -0.42f) },
        Teilung.Balken => new[] { Rechteck(-2, -0.25f, 2, 0.33f) },
        Teilung.Pfahl => new[] { Rechteck(-0.32f, -2, 0.32f, 2) },
        Teilung.Sparren => new[] { new Vector2[] { new(-1.4f, 0.95f), new(0, -0.5f), new(1.4f, 0.95f), new(1.4f, 1.45f), new(0, 0f), new(-1.4f, 1.45f) } },
        _ => Array.Empty<Vector2[]>(),
    };

    /// <summary>Wo die Figur sitzt und wie groß sie ist: unter dem Schildhaupt tiefer und kleiner, im Bord kleiner.</summary>
    public static (Vector2 Versatz, float Massstab) Figurplatz(Teilung teilung) => teilung switch
    {
        Teilung.Schildhaupt => (new Vector2(0, 0.33f), 0.78f),
        Teilung.Bord => (new Vector2(0, 0.08f), 0.8f),
        Teilung.Sparren => (new Vector2(0, 0.62f), 0.5f), // klein unter der Spitze des Sparrens
        _ => (new Vector2(0, 0.05f), 1f),
    };

    private static readonly Dictionary<Figur, List<Vector2[]>> Zwischenspeicher = new();

    /// <summary>Die Teile der Figur, etwa im Bereich −0,65 … 0,65.</summary>
    public static List<Vector2[]> FigurTeile(Figur figur)
    {
        if (!Zwischenspeicher.TryGetValue(figur, out var teile))
            Zwischenspeicher[figur] = teile = Bauen(figur);
        return teile;
    }

    private static List<Vector2[]> Bauen(Figur figur) => figur switch
    {
        Core.Dynastie.Figur.Hammer => new()
        {
            Rechteck(-0.07f, -0.32f, 0.07f, 0.64f),
            new Vector2[] { new(-0.44f, -0.5f), new(0.34f, -0.52f), new(0.42f, -0.44f), new(0.42f, -0.28f), new(0.34f, -0.22f), new(-0.44f, -0.24f) },
        },
        Core.Dynastie.Figur.Amboss => new()
        {
            new Vector2[] { new(-0.7f, -0.3f), new(0.52f, -0.32f), new(0.52f, -0.1f), new(-0.32f, -0.1f) },
            Rechteck(-0.14f, -0.12f, 0.26f, 0.24f),
            new Vector2[] { new(-0.2f, 0.22f), new(0.32f, 0.22f), new(0.48f, 0.46f), new(-0.36f, 0.46f) },
        },
        Core.Dynastie.Figur.Baum => new()
        {
            Rechteck(-0.08f, -0.05f, 0.08f, 0.55f),
            new Vector2[] { new(-0.08f, 0.42f), new(0.08f, 0.42f), new(0.3f, 0.64f), new(-0.3f, 0.64f) },
            Kreis(new(0, -0.3f), 0.32f), Kreis(new(-0.3f, -0.12f), 0.24f), Kreis(new(0.3f, -0.12f), 0.24f),
            Kreis(new(-0.2f, -0.5f), 0.2f), Kreis(new(0.2f, -0.5f), 0.2f),
        },
        Core.Dynastie.Figur.Wellen => new() { Welle(-0.32f), Welle(0f), Welle(0.32f) },
        Core.Dynastie.Figur.Stern => new() { Stern(Vector2.Zero, 0.6f, 0.26f, 6) },
        Core.Dynastie.Figur.Schluessel => Kranz(new(0, -0.36f), 0.13f, 0.27f, 16)
            .Append(Rechteck(-0.065f, -0.12f, 0.065f, 0.64f))
            .Append(Rechteck(0.05f, 0.36f, 0.27f, 0.46f))
            .Append(Rechteck(0.05f, 0.52f, 0.22f, 0.62f)).ToList(),
        Core.Dynastie.Figur.Turm => new()
        {
            new Vector2[] { new(-0.3f, -0.22f), new(0.3f, -0.22f), new(0.34f, 0.62f), new(-0.34f, 0.62f) },
            Rechteck(-0.4f, -0.32f, 0.4f, -0.18f),
            Rechteck(-0.4f, -0.52f, -0.22f, -0.3f), Rechteck(-0.09f, -0.52f, 0.09f, -0.3f), Rechteck(0.22f, -0.52f, 0.4f, -0.3f),
        },
        Core.Dynastie.Figur.Krone => new()
        {
            Rechteck(-0.5f, 0.08f, 0.5f, 0.3f),
            new Vector2[] { new(-0.5f, 0.1f), new(-0.52f, -0.36f), new(-0.25f, -0.08f), new(0, -0.46f), new(0.25f, -0.08f), new(0.52f, -0.36f), new(0.5f, 0.1f) },
            Kreis(new(-0.52f, -0.4f), 0.075f), Kreis(new(0, -0.5f), 0.075f), Kreis(new(0.52f, -0.4f), 0.075f),
        },
        Core.Dynastie.Figur.Rad => Kranz(Vector2.Zero, 0.4f, 0.54f, 24)
            .Append(Kreis(Vector2.Zero, 0.12f))
            .Concat(Enumerable.Range(0, 6).Select(i => GedrehtesRechteck(Vector2.Zero, 0.06f, 0.44f, i * MathF.PI / 3)))
            .ToList(),
        Core.Dynastie.Figur.Aehre => Enumerable.Range(0, 4)
            .SelectMany(i => new[]
            {
                Ellipse(new(-0.12f, -0.42f + i * 0.17f), 0.075f, 0.15f, 0.5f),
                Ellipse(new(0.12f, -0.42f + i * 0.17f), 0.075f, 0.15f, -0.5f),
            })
            .Prepend(Ellipse(new(0, -0.6f), 0.07f, 0.14f, 0))
            .Prepend(Rechteck(-0.03f, -0.4f, 0.03f, 0.66f))
            .Append(Ellipse(new(-0.16f, 0.36f), 0.05f, 0.16f, 0.9f))
            .ToList(),
        Core.Dynastie.Figur.Lilie => new()
        {
            new Vector2[] { new(0, -0.66f), new(0.13f, -0.48f), new(0.15f, -0.25f), new(0.08f, 0.02f), new(-0.08f, 0.02f), new(-0.15f, -0.25f), new(-0.13f, -0.48f) },
            new Vector2[] { new(-0.08f, 0.04f), new(-0.32f, -0.02f), new(-0.5f, -0.2f), new(-0.5f, -0.38f), new(-0.4f, -0.44f), new(-0.42f, -0.3f), new(-0.32f, -0.18f), new(-0.12f, -0.12f) },
            new Vector2[] { new(0.08f, 0.04f), new(0.12f, -0.12f), new(0.32f, -0.18f), new(0.42f, -0.3f), new(0.4f, -0.44f), new(0.5f, -0.38f), new(0.5f, -0.2f), new(0.32f, -0.02f) },
            Rechteck(-0.3f, 0.02f, 0.3f, 0.14f),
            new Vector2[] { new(-0.07f, 0.14f), new(0.07f, 0.14f), new(0.26f, 0.48f), new(0.1f, 0.38f), new(0, 0.56f), new(-0.1f, 0.38f), new(-0.26f, 0.48f) },
        },
        Core.Dynastie.Figur.Rose => Enumerable.Range(0, 5)
            .Select(i => Kreis(Vector2.FromAngle(-MathF.PI / 2 + i * 2 * MathF.PI / 5) * 0.3f, 0.24f))
            .Append(Kreis(Vector2.Zero, 0.26f))
            .ToList(),
        Core.Dynastie.Figur.Fisch => new()
        {
            Ellipse(new(-0.08f, 0), 0.44f, 0.21f, 0),
            new Vector2[] { new(0.26f, 0), new(0.62f, -0.26f), new(0.54f, 0), new(0.62f, 0.26f) },
            new Vector2[] { new(-0.18f, -0.18f), new(0.02f, -0.34f), new(0.1f, -0.16f) },
        },
        Core.Dynastie.Figur.Schwert => new()
        {
            new Vector2[] { new(-0.065f, -0.34f), new(0.065f, -0.34f), new(0.065f, 0.5f), new(0, 0.66f), new(-0.065f, 0.5f) },
            Rechteck(-0.32f, -0.42f, 0.32f, -0.33f),
            Rechteck(-0.045f, -0.62f, 0.045f, -0.4f),
            Kreis(new(0, -0.65f), 0.075f),
        },
        Core.Dynastie.Figur.Kelch => new()
        {
            new Vector2[] { new(-0.36f, -0.52f), new(0.36f, -0.52f), new(0.32f, -0.26f), new(0.13f, -0.06f), new(-0.13f, -0.06f), new(-0.32f, -0.26f) },
            Rechteck(-0.05f, -0.08f, 0.05f, 0.38f),
            Kreis(new(0, 0.12f), 0.09f),
            new Vector2[] { new(-0.08f, 0.34f), new(0.08f, 0.34f), new(0.32f, 0.56f), new(-0.32f, 0.56f) },
        },
        _ => new() { Mond() },
    };

    // ── Grundformen ──────────────────────────────────────────────────────────

    private static Vector2[] Rechteck(float x0, float y0, float x1, float y1) =>
        new Vector2[] { new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1) };

    private static Vector2[] Kreis(Vector2 m, float r, int ecken = 20) => Ellipse(m, r, r, 0, ecken);

    private static Vector2[] Ellipse(Vector2 m, float rx, float ry, float drehung, int ecken = 16) =>
        Enumerable.Range(0, ecken)
            .Select(i => m + new Vector2(MathF.Cos(i * MathF.Tau / ecken) * rx, MathF.Sin(i * MathF.Tau / ecken) * ry).Rotated(drehung))
            .ToArray();

    /// <summary>Ein Stab vom Mittelpunkt nach außen, um <paramref name="winkel"/> gedreht (Speiche).</summary>
    private static Vector2[] GedrehtesRechteck(Vector2 m, float halbeBreite, float laenge, float winkel) =>
        Rechteck(-halbeBreite, 0, halbeBreite, laenge).Select(p => m + p.Rotated(winkel)).ToArray();

    /// <summary>Ein Ring aus vielen Viereck-Stücken – so braucht er kein Loch.</summary>
    private static IEnumerable<Vector2[]> Kranz(Vector2 m, float innen, float aussen, int stuecke) =>
        Enumerable.Range(0, stuecke).Select(i =>
        {
            float a = i * MathF.Tau / stuecke, b = (i + 1.05f) * MathF.Tau / stuecke;
            return new[] { m + Vector2.FromAngle(a) * innen, m + Vector2.FromAngle(a) * aussen, m + Vector2.FromAngle(b) * aussen, m + Vector2.FromAngle(b) * innen };
        });

    private static Vector2[] Stern(Vector2 m, float aussen, float innen, int zacken) =>
        Enumerable.Range(0, zacken * 2)
            .Select(i => m + Vector2.FromAngle(-MathF.PI / 2 + i * MathF.PI / zacken) * (i % 2 == 0 ? aussen : innen))
            .ToArray();

    /// <summary>Ein Wellenband quer über die Mitte.</summary>
    private static Vector2[] Welle(float y)
    {
        const int schritte = 24;
        var oben = Enumerable.Range(0, schritte + 1).Select(i => -0.7f + 1.4f * i / schritte)
            .Select(x => new Vector2(x, y + 0.06f * MathF.Sin(x * MathF.PI * 2.2f)));
        var unten = oben.Reverse().Select(p => p + new Vector2(0, 0.13f));
        return oben.Concat(unten).ToArray();
    }

    /// <summary>Halbmond mit den Hörnern nach oben: äußerer Bogen unten herum, innerer zurück (beide treffen sich an den Spitzen).</summary>
    private static Vector2[] Mond()
    {
        var punkte = new List<Vector2>();
        for (var i = 0; i <= 20; i++)
        {
            var w = Mathf.DegToRad(200 - 220f * i / 20); // 200° … −20° über 90° (unten)
            punkte.Add(new Vector2(MathF.Cos(w), MathF.Sin(w)) * 0.5f + new Vector2(0, 0.05f));
        }
        for (var i = 1; i <= 19; i++)
        {
            var w = Mathf.DegToRad(180f * i / 20); // 0° … 180° über 90° (unten)
            punkte.Add(new Vector2(MathF.Cos(w) * 0.47f, -0.17f + MathF.Sin(w) * 0.47f) + new Vector2(0, 0.05f));
        }
        return punkte.ToArray();
    }
}
