using Godot;

namespace Reichsgilde;

/// <summary>
/// Der gemeinsame Stil aller Steuerelemente (PR „Lesbarkeit und mittige Fenster“), einmal im Standard-Thema gesetzt statt in jeder Tafel:
/// Knöpfe mit sichtbarer Fläche, Rand und Hover, Schalter und Häkchenfelder, die auch ausgeschaltet klar zu erkennen sind, und lesbare Farben
/// für gesperrte Knöpfe. Schriftgrößen und Mindestkontrast stehen in <see cref="Schriften"/>, Tabellenzeilen in <see cref="Streifenraster"/>.
///
/// Wer einen Knopf selbst gestaltet (<c>AddThemeStyleboxOverride</c>, <c>Flat</c>), behält seine Gestaltung – das Thema gilt nur dort, wo nichts überschrieben ist.
/// Läuft einmal beim Start (Autoload <see cref="Fenster"/>); <see cref="Freigeben"/> räumt beim Beenden auf, wie bei <see cref="Schriften"/>.
/// </summary>
public static class Oberflaechenstil
{
    public static readonly Color Schrift = new(0.92f, 0.88f, 0.80f);
    public static readonly Color Gold = new(0.95f, 0.82f, 0.45f);
    private static readonly Color Linie = new(0.62f, 0.52f, 0.36f);

    /// <summary>Zeilenhintergrund jeder zweiten Tabellenzeile (heller als der Tafelgrund, ohne die Schrift zu stören).</summary>
    public static readonly Color Streifen = new(1f, 0.95f, 0.80f, 0.055f);

    private static readonly List<Resource> Gehalten = new();
    private static readonly List<(string Name, string Typ, int Art)> Eintraege = new();

    private static T Halten<T>(T r) where T : Resource
    {
        Gehalten.Add(r);
        return r;
    }

    private static StyleBoxFlat Kasten(Color grund, Color rand, int randBreite = 1, int quer = 10, int hoch = 4)
    {
        var k = new StyleBoxFlat
        {
            BgColor = grund, BorderColor = rand,
            BorderWidthLeft = randBreite, BorderWidthTop = randBreite, BorderWidthRight = randBreite, BorderWidthBottom = randBreite,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            ContentMarginLeft = quer, ContentMarginRight = quer, ContentMarginTop = hoch, ContentMarginBottom = hoch,
        };
        return Halten(k);
    }

    public static void Einrichten()
    {
        var thema = ThemeDB.GetDefaultTheme();
        var normal = Kasten(new Color(0.22f, 0.18f, 0.13f), Linie);
        var hover = Kasten(new Color(0.34f, 0.27f, 0.17f), Gold);
        var gedrueckt = Kasten(new Color(0.12f, 0.10f, 0.07f), Gold, 2);
        var gesperrt = Kasten(new Color(0.15f, 0.14f, 0.13f), new Color(0.34f, 0.31f, 0.26f));
        var leer = Halten(new StyleBoxEmpty());
        foreach (var typ in new[] { "Button", "OptionButton", "MenuButton" })
        {
            Stil(thema, "normal", typ, normal);
            Stil(thema, "hover", typ, hover);
            Stil(thema, "pressed", typ, gedrueckt);
            Stil(thema, "hover_pressed", typ, gedrueckt);
            Stil(thema, "disabled", typ, gesperrt);
            Stil(thema, "focus", typ, leer);
            Farbe(thema, "font_color", typ, Schrift);
            Farbe(thema, "font_hover_color", typ, new Color(1f, 0.95f, 0.78f));
            Farbe(thema, "font_pressed_color", typ, Gold);
            Farbe(thema, "font_hover_pressed_color", typ, Gold);
            Farbe(thema, "font_focus_color", typ, Schrift);
            Farbe(thema, "font_disabled_color", typ, new Color(0.62f, 0.58f, 0.52f)); // gegen den gesperrten Knopf rund 4,6 : 1
        }
        // Schalter und Häkchen: ein Knopf, der auf dunklem Grund auch ausgeschaltet eine Fläche mit hellem Rand hat
        foreach (var typ in new[] { "CheckButton", "CheckBox" })
        {
            Stil(thema, "normal", typ, leer);
            Stil(thema, "hover", typ, leer);
            Stil(thema, "pressed", typ, leer);
            Stil(thema, "hover_pressed", typ, leer);
            Stil(thema, "disabled", typ, leer);
            Stil(thema, "focus", typ, leer);
            Farbe(thema, "font_color", typ, Schrift);
            Farbe(thema, "font_hover_color", typ, new Color(1f, 0.95f, 0.78f));
            Farbe(thema, "font_pressed_color", typ, Schrift);
            Farbe(thema, "font_hover_pressed_color", typ, new Color(1f, 0.95f, 0.78f));
            Farbe(thema, "font_disabled_color", typ, new Color(0.62f, 0.58f, 0.52f));
        }
        var aus = Schalter(false, false);
        var an = Schalter(true, false);
        var ausGesperrt = Schalter(false, true);
        var anGesperrt = Schalter(true, true);
        foreach (var (name, bild) in new[] { ("unchecked", aus), ("checked", an), ("unchecked_disabled", ausGesperrt), ("checked_disabled", anGesperrt),
                                              ("unchecked_mirrored", aus), ("checked_mirrored", an), ("unchecked_disabled_mirrored", ausGesperrt), ("checked_disabled_mirrored", anGesperrt) })
            Bild(thema, name, "CheckButton", bild);
        var kastenAus = Haken(false, false);
        var kastenAn = Haken(true, false);
        var kastenAusGesperrt = Haken(false, true);
        var kastenAnGesperrt = Haken(true, true);
        foreach (var (name, bild) in new[] { ("unchecked", kastenAus), ("checked", kastenAn), ("unchecked_disabled", kastenAusGesperrt), ("checked_disabled", kastenAnGesperrt) })
            Bild(thema, name, "CheckBox", bild);
    }

    private static void Stil(Theme t, string name, string typ, StyleBox s)
    {
        t.SetStylebox(name, typ, s);
        Eintraege.Add((name, typ, 0));
    }

    private static void Farbe(Theme t, string name, string typ, Color f) => t.SetColor(name, typ, f);

    private static void Bild(Theme t, string name, string typ, Texture2D bild)
    {
        t.SetIcon(name, typ, bild);
        Eintraege.Add((name, typ, 1));
    }

    /// <summary>Beim Beenden: Stile und Bilder aus dem Thema nehmen und freigeben (sonst meldet Godot sie als Leck).</summary>
    public static void Freigeben()
    {
        var thema = ThemeDB.GetDefaultTheme();
        foreach (var (name, typ, art) in Eintraege)
        {
            if (art == 0) thema.ClearStylebox(name, typ);
            else thema.ClearIcon(name, typ);
        }
        Eintraege.Clear();
        foreach (var r in Gehalten) r.Dispose();
        Gehalten.Clear();
    }

    // ── Gezeichnete Bilder ──────────────────────────────────────────────────

    /// <summary>Deckung (0–1) eines Punktes in einem Rechteck mit runden Ecken – für weiche Kanten ohne Zeichenfläche.</summary>
    private static float Deckung(float x, float y, float w, float h, float radius)
    {
        var qx = MathF.Abs(x - w / 2) - (w / 2 - radius);
        var qy = MathF.Abs(y - h / 2) - (h / 2 - radius);
        var abstand = MathF.Sqrt(MathF.Max(qx, 0) * MathF.Max(qx, 0) + MathF.Max(qy, 0) * MathF.Max(qy, 0)) + MathF.Min(MathF.Max(qx, qy), 0) - radius;
        return Math.Clamp(0.5f - abstand, 0f, 1f);
    }

    private static void Mischen(Image bild, int x, int y, Color farbe, float deckung)
    {
        if (deckung <= 0f) return;
        var alt = bild.GetPixel(x, y);
        var a = farbe.A * deckung;
        var neuA = a + alt.A * (1 - a);
        var rgb = neuA <= 0 ? farbe : new Color((farbe.R * a + alt.R * alt.A * (1 - a)) / neuA, (farbe.G * a + alt.G * alt.A * (1 - a)) / neuA, (farbe.B * a + alt.B * alt.A * (1 - a)) / neuA);
        bild.SetPixel(x, y, new Color(rgb.R, rgb.G, rgb.B, neuA));
    }

    /// <summary>Ein Schiebeschalter, 44×22: heller Rand und dunkle Fläche (aus), goldene Fläche und Knopf rechts (an).</summary>
    private static ImageTexture Schalter(bool an, bool gesperrt)
    {
        const int b = 44, h = 22;
        var dim = gesperrt ? 0.55f : 1f;
        var rand = new Color(0.80f * dim + 0.1f * (1 - dim), 0.72f * dim + 0.1f * (1 - dim), 0.55f * dim + 0.1f * (1 - dim));
        var grund = an ? new Color(0.62f, 0.46f, 0.16f) : new Color(0.10f, 0.09f, 0.08f);
        if (gesperrt) grund = an ? new Color(0.36f, 0.30f, 0.18f) : new Color(0.14f, 0.13f, 0.12f);
        var knopf = an ? new Color(1f, 0.93f, 0.70f) : new Color(0.78f, 0.72f, 0.62f);
        if (gesperrt) knopf = new Color(0.55f, 0.52f, 0.46f);
        var bild = Image.CreateEmpty(b, h, false, Image.Format.Rgba8);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < b; x++)
            {
                Mischen(bild, x, y, rand, Deckung(x + 0.5f, y + 0.5f, b, h, h / 2f));
                Mischen(bild, x, y, grund, Deckung(x - 1.5f + 0.5f, y - 1.5f + 0.5f, b - 3, h - 3, (h - 3) / 2f));
            }
        var mx = an ? b - 11.5f : 11.5f;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < b; x++)
            {
                var d = MathF.Sqrt((x + 0.5f - mx) * (x + 0.5f - mx) + (y + 0.5f - h / 2f) * (y + 0.5f - h / 2f));
                Mischen(bild, x, y, knopf, Math.Clamp(7.5f - d, 0f, 1f));
            }
        return Halten(ImageTexture.CreateFromImage(bild));
    }

    /// <summary>Ein Häkchenfeld, 22×22: heller Rand auf dunklem Grund; angekreuzt mit goldenem Haken.</summary>
    private static ImageTexture Haken(bool an, bool gesperrt)
    {
        const int g = 22;
        var rand = gesperrt ? new Color(0.45f, 0.41f, 0.34f) : new Color(0.80f, 0.72f, 0.55f);
        var grund = gesperrt ? new Color(0.14f, 0.13f, 0.12f) : new Color(0.10f, 0.09f, 0.08f);
        var bild = Image.CreateEmpty(g, g, false, Image.Format.Rgba8);
        for (var y = 0; y < g; y++)
            for (var x = 0; x < g; x++)
            {
                Mischen(bild, x, y, rand, Deckung(x + 0.5f, y + 0.5f, g, g, 4));
                Mischen(bild, x, y, grund, Deckung(x - 1.5f + 0.5f, y - 1.5f + 0.5f, g - 3, g - 3, 3));
            }
        if (an)
        {
            var haken = gesperrt ? new Color(0.6f, 0.55f, 0.4f) : Gold;
            (Vector2, Vector2)[] striche = { (new Vector2(5, 11.5f), new Vector2(9.5f, 16)), (new Vector2(9.5f, 16), new Vector2(17, 6.5f)) };
            for (var y = 0; y < g; y++)
                for (var x = 0; x < g; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    foreach (var (a, c) in striche)
                    {
                        var ac = c - a;
                        var t = Math.Clamp((p - a).Dot(ac) / ac.LengthSquared(), 0f, 1f);
                        Mischen(bild, x, y, haken, Math.Clamp(1.75f - (p - (a + ac * t)).Length(), 0f, 1f));
                    }
                }
        }
        return Halten(ImageTexture.CreateFromImage(bild));
    }
}

/// <summary>
/// Ein Raster für Tabellen: jede zweite Zeile bekommt einen leicht helleren Hintergrund (die Kopfzeile, Zeile 0, bleibt glatt).
/// Gezeichnet wird hinter den Zellen nach den tatsächlichen Zeilenhöhen, die das Raster selbst bestimmt; unsichtbare Zellen zählen nicht.
/// </summary>
public partial class Streifenraster : GridContainer
{
    public Streifenraster()
    {
        SortChildren += QueueRedraw;
    }

    public override void _Draw()
    {
        if (Columns < 1) return;
        var sichtbar = new List<Control>();
        foreach (var kind in GetChildren())
            if (kind is Control { Visible: true, TopLevel: false } c) sichtbar.Add(c);
        var abstand = GetThemeConstant("v_separation");
        for (var zeile = 1; zeile * Columns < sichtbar.Count; zeile += 2)
        {
            float oben = float.MaxValue, unten = float.MinValue;
            for (var i = zeile * Columns; i < Math.Min((zeile + 1) * Columns, sichtbar.Count); i++)
            {
                oben = Math.Min(oben, sichtbar[i].Position.Y);
                unten = Math.Max(unten, sichtbar[i].Position.Y + sichtbar[i].Size.Y);
            }
            DrawRect(new Rect2(-6, oben - abstand / 2f, Size.X + 12, unten - oben + abstand), Oberflaechenstil.Streifen);
        }
    }
}
