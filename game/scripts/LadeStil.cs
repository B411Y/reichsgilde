using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Brücke zum Thema der Zunftlade (<c>res://scenes/zunftlade_thema.tres</c>), gebaut wie <see cref="RatStil"/> und <see cref="MeldungStil"/>:
/// Aussehen steht nur dort (Stoffe, Farben, Schriften, Abstände, Maße), dieser Code kennt nur die <b>Namen</b> der Typ-Varianten („LadeFach“,
/// „LadeKnopf“ …), der Maße und der Farben – nie eine Farbe, Größe oder einen Rand. Wer die Lade neu gestaltet, tauscht Thema und Bilder.
/// </summary>
public static class LadeStil
{
    private static Theme? _thema;

    /// <summary>Das Thema, einmal geladen.</summary>
    public static Theme Thema => _thema ??= GD.Load<Theme>("res://scenes/zunftlade_thema.tres");

    /// <summary>Ein Maß der Familie „LadeMasse“ (z. B. „lade_hoehe“), in Einheiten der Oberfläche.</summary>
    public static int Mass(string name) => Thema.GetConstant(name, "LadeMasse");

    /// <summary>Eine Farbe der Familie „LadeFarben“ (z. B. „portraet_grund“).</summary>
    public static Color Farbe(string name) => Thema.GetColor(name, "LadeFarben");

    /// <summary>Ein Bild einer Typ-Variante (z. B. „bogen“ in „LadePortraet“), sonst null.</summary>
    public static Texture2D? Bild(string name, string variante) => Thema.HasIcon(name, variante) ? Thema.GetIcon(name, variante) : null;

    /// <summary>Setzt das Thema an die Wurzel eines Teils der Lade; die Kinder erben es.</summary>
    public static void Anwenden(Control wurzel) => wurzel.Theme = Thema;

    /// <summary>Eine Beschriftung in einer Typ-Variante („LadeText“, „LadeWert“ …). Fängt keine Maus, solange der Aufrufer es nicht ändert.</summary>
    public static Label Text(Node eltern, string text, string variante = "LadeText")
    {
        var label = new Label { Text = text, ThemeTypeVariation = variante, MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        eltern.AddChild(label);
        return label;
    }

    /// <summary>Wechselt die Typ-Variante nur bei einer Änderung (z. B. Wert → Wert in Rot).</summary>
    public static void Variante(Control knoten, string variante)
    {
        if (knoten.ThemeTypeVariation != variante) knoten.ThemeTypeVariation = variante;
    }

    /// <summary>Ein Knopf der Lade („LadeKnopf“): ohne Tastaturfokus, mit Hinweis und Aktion.</summary>
    public static Button Knopf(Node eltern, string text, string tipp, Action? aktion, string variante = "LadeKnopf")
    {
        var knopf = new Button { Text = text, TooltipText = tipp, ThemeTypeVariation = variante, FocusMode = Control.FocusModeEnum.None };
        if (aktion is not null) knopf.Pressed += aktion;
        eltern.AddChild(knopf);
        return knopf;
    }

    /// <summary>Ein Behälter in einer Typ-Variante („LadeSpalte“, „LadeReihe“, „LadeRaster“ …).</summary>
    public static T Behaelter<T>(Node eltern, string variante) where T : Container, new()
    {
        var b = new T { ThemeTypeVariation = variante, MouseFilter = Control.MouseFilterEnum.Ignore };
        eltern.AddChild(b);
        return b;
    }

    /// <summary>Eine Fläche aus Holz, Stein oder Pergament („LadeKorpus“, „LadeFach“ …), siehe <see cref="LadeFlaeche"/>.</summary>
    public static LadeFlaeche Flaeche(Node? eltern, string variante, LadeFlaeche.Seiten rand = LadeFlaeche.Seiten.Alle)
    {
        var f = new LadeFlaeche { ThemeTypeVariation = variante, Rand = rand };
        eltern?.AddChild(f);
        return f;
    }

    /// <summary>Ein Symbol der Familie „LadeSymbole“ (z. B. „gulden“) in der Größe des Maßes <paramref name="mass"/>; fängt keine Maus.</summary>
    public static TextureRect Symbol(Node eltern, string name, string mass)
    {
        var groesse = Mass(mass);
        var bild = new TextureRect
        {
            Texture = Bild(name, "LadeSymbole"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(groesse, groesse), MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        eltern.AddChild(bild);
        return bild;
    }
}
