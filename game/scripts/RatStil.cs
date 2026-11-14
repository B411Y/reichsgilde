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
