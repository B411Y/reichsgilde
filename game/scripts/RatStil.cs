using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Brücke zum zentralen Thema der Ratskammer (<c>res://scenes/rat_thema.tres</c>): Aussehen steht nur dort, Logik im Code.
/// Dieser Code kennt nur die <b>Namen</b> der Typ-Varianten („RatTitel“, „RatReiter“, „RatAmtknopf“ …) – nie eine Farbe, Größe oder einen Rand.
/// Wer das Aussehen ändern will, ändert nur die .tres-Datei (oder ersetzt sie komplett).
///
/// Die Hilfen geben die Varianten an Label, Knopf und Container weiter; ein Wurzel-Steuerelement bekommt das Thema mit <see cref="Anwenden"/>,
/// alle Kinder erben es.
/// </summary>
public static class RatStil
{
    private static Theme? _thema;

    /// <summary>Das Thema, einmal geladen.</summary>
    public static Theme Thema => _thema ??= GD.Load<Theme>("res://scenes/rat_thema.tres");

    public const string Familie = "RatFarben", Masse = "RatMasse";

    /// <summary>Eine Holzschnitt-Grafik des Ratsbaums aus dem Thema (Familie „RatBilder“): <c>amt_&lt;amt&gt;</c> (256 x 256), <c>ratsbaum_pergament</c> (1024 x 768), <c>ratsbaum_spruchband</c> (512 x 192, leer), <c>ratsbaum_linie</c> (124 x 54, waagerecht nahtlos wiederholbar). Die Pfade stehen nur in <c>rat_thema.tres</c>.</summary>
    public static Texture2D? Grafik(string name) => Thema.HasIcon(name, "RatBilder") ? Thema.GetIcon(name, "RatBilder") : null;

    /// <summary>Das Amtszeichen eines Amtes als Bild (Dateiname wie bei den Waren-Icons: Umlaute umschrieben); <paramref name="gedaempft"/> (Blässe aus „knoten_frei_blass“ in Prozent) für ein freies Amt. Null, wenn es kein Zeichen gibt.</summary>
    public static TextureRect? AmtZeichen(string amtName, int groesse, bool gedaempft = false)
    {
        var kennung = amtName.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss").Replace(" ", "_");
        if (Grafik("amt_" + kennung) is not { } bild) return null;
        return new TextureRect
        {
            Texture = bild, CustomMinimumSize = new Vector2(groesse, groesse), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = gedaempft ? new Color(1, 1, 1, Mass("knoten_frei_blass") / 100f) : Colors.White,
        };
    }

    /// <summary>Eine Fläche aus Pergament (Typ „RatBaumFlaeche“ des Themas), gleich eingehängt.</summary>
    public static PanelContainer Pergament(Node eltern) => Container<PanelContainer>(eltern, "RatBaumFlaeche");

    /// <summary>Ein Spruchband: das leere Band aus dem Holzschnitt, die Beschriftung setzt das Spiel als Label darüber (Typ „RatSpruch“).</summary>
    public static Control Spruchband(Node eltern, string text) => Spruchband(eltern, text, out _);

    /// <summary>Wie <see cref="Spruchband(Node, string)"/>, gibt aber auch das Label mit der Beschriftung zurück (für Text, der sich später ändert).</summary>
    public static Control Spruchband(Node eltern, string text, out Label beschriftung)
    {
        var breite = Mass("spruchband_grafik_breite");
        var band = Grafik("ratsbaum_spruchband");
        var hoehe = band is null ? breite * 3 / 8 : breite * band.GetHeight() / band.GetWidth();
        var wurzel = new Control { CustomMinimumSize = new Vector2(breite, hoehe), MouseFilter = Control.MouseFilterEnum.Ignore };
        if (band is not null)
        {
            var bild = new TextureRect { Texture = band, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore };
            bild.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            wurzel.AddChild(bild);
        }
        var label = new Label { Text = text, ThemeTypeVariation = "RatSpruch", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 0);
        var seite = breite * Mass("spruch_rand_prozent") / 100f;
        label.OffsetLeft = seite; label.OffsetRight = -seite; // die Rollen an den Enden freilassen
        wurzel.AddChild(label);
        beschriftung = label;
        eltern.AddChild(wurzel);
        return wurzel;
    }

    /// <summary>Breite des Schriftfeldes auf dem Spruchband (Bandbreite ohne die Rollen an den Enden).</summary>
    public static float SpruchFeldBreite() => Mass("spruchband_grafik_breite") * (100 - 2 * Mass("spruch_rand_prozent")) / 100f;

    /// <summary>Wählt die größte Schrift (von „RatSpruch“ bis „spruch_schrift_min“), bei der <paramref name="text"/> in höchstens „spruch_zeilen_max“ Zeilen in das Schriftfeld passt; ohne Treffer die kleinste. Gibt Größe und Zeilenzahl zurück (reine Rechnung, ohne Knoten).</summary>
    public static (int Groesse, int Zeilen) SpruchSchrift(string text)
    {
        var schrift = Thema.GetFont("font", "RatSpruch");
        var gross = Thema.GetFontSize("font_size", "RatSpruch");
        var klein = Mass("spruch_schrift_min");
        var zeilenMax = Mass("spruch_zeilen_max");
        var breite = SpruchFeldBreite();
        var zeilen = 1;
        for (var g = gross; g >= klein; g--)
        {
            var hoehe = schrift.GetMultilineStringSize(text, HorizontalAlignment.Left, breite, g, brkFlags: TextServer.LineBreakFlag.WordBound | TextServer.LineBreakFlag.Mandatory).Y;
            zeilen = Math.Max(1, (int)Math.Round(hoehe / schrift.GetHeight(g)));
            if (zeilen <= zeilenMax || g == klein) return (g, zeilen);
        }
        return (klein, zeilen);
    }

    /// <summary>Setzt den Text einer Spruchband-Beschriftung: umbrechen, bei Bedarf die Schrift verkleinern, nie abschneiden.</summary>
    public static void SpruchTitel(Label beschriftung, string text)
    {
        beschriftung.Text = text;
        beschriftung.AddThemeFontSizeOverride("font_size", SpruchSchrift(text).Groesse);
    }

    /// <summary>Ein Seil zwischen Punkten (Line2D mit der Seil-Kachel, wiederholt über jede Länge und jeden Winkel); <paramref name="gold"/> färbt den Weg zum eigenen Amt.</summary>
    public static Line2D Seil(Node eltern, Vector2[] punkte, bool gold)
    {
        var linie = new Line2D
        {
            Points = punkte, Width = Mass("seil_breite"), Texture = Grafik("ratsbaum_linie"), TextureMode = Line2D.LineTextureMode.Tile,
            TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled, DefaultColor = Farbe(gold ? "seil_gold" : "seil"), JointMode = Line2D.LineJointMode.Round,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };
        eltern.AddChild(linie);
        return linie;
    }

    /// <summary>Hängt das Thema an ein Wurzel-Steuerelement (die Kinder erben es).</summary>
    public static void Anwenden(Control wurzel) => wurzel.Theme = Thema;

    /// <summary>Eine Farbe der Familie („gold“, „grau“, „gruen“, „rot“, „schrift“) – für Stellen, an denen sich die Farbe nach dem Zustand richtet.</summary>
    public static Color Farbe(string name) => Thema.GetColor(name, Familie);

    /// <summary>Ein Maß der Familie („text_breite“ = Umbruchbreite, „kammer_breite“ = Mindestbreite der Ratskammer).</summary>
    public static int Mass(string name) => Thema.GetConstant(name, Masse);

    /// <summary>Gibt dem Steuerelement eine Typ-Variante des Themas.</summary>
    public static T Variante<T>(T knoten, string variante) where T : Control
    {
        knoten.ThemeTypeVariation = variante;
        return knoten;
    }

    /// <summary>Beschriftung in einer Variante („RatTitel“, „RatAbschnitt“, „RatText“, „RatHinweis“, „RatGut“, „RatSchlecht“); <paramref name="umbrechen"/> bricht bei der Breite um, die <paramref name="breite"/> im Thema nennt („text_breite“ in der Kammer, „kurz_breite“ in der Kurzübersicht).</summary>
    public static Label Text(Container eltern, string text, string variante = "RatText", bool umbrechen = false, bool zentriert = false, string breite = "text_breite")
    {
        var label = new Label { Text = text, ThemeTypeVariation = variante, MouseFilter = Control.MouseFilterEnum.Pass };
        if (umbrechen)
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(Mass(breite), 0); // Godot kennt beim ersten Aufbau die Breite noch nicht (siehe Hauptmenue.Text)
        }
        if (zentriert) label.HorizontalAlignment = HorizontalAlignment.Center;
        eltern.AddChild(label);
        return label;
    }

    /// <summary>Der Knopf „Amtsknopf“: klein, mit Rand in Gold – für alles, was ein Amt oder die Kurzübersicht auslöst.</summary>
    public static Button Amtsknopf(Container eltern, string text, Action aktion, string tipp = "")
    {
        var knopf = new Button { Text = text, ThemeTypeVariation = "RatAmtknopf", FocusMode = Control.FocusModeEnum.None, TooltipText = tipp, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        knopf.Pressed += aktion;
        eltern.AddChild(knopf);
        return knopf;
    }

    /// <summary>Ein Container in einer Variante („RatSpalte“, „RatZeile“, „RatRaster“ …), gleich eingehängt.</summary>
    public static T Container<T>(Node eltern, string variante) where T : Container, new()
    {
        var c = new T { ThemeTypeVariation = variante };
        eltern.AddChild(c);
        return c;
    }
}
