using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Brücke zum Thema des Hinterzimmers (<c>res://scenes/hinterzimmer_thema.tres</c>), nach dem Muster von <see cref="RatStil"/>:
/// Aussehen steht nur in der .tres-Datei, die Tafel (<see cref="GaunerAnzeige"/>) kennt nur die <b>Namen</b> der Typ-Varianten
/// („HzTitel“, „HzBlatt“, „HzBand“ …) – nie eine Farbe, Größe oder einen Rand. Wer das Aussehen ändern will (z. B. Pergament statt
/// dunkler Tafel), ändert nur die .tres-Datei.
/// </summary>
public static class HinterzimmerStil
{
    private static Theme? _thema;

    /// <summary>Das Thema, einmal geladen.</summary>
    public static Theme Thema => _thema ??= GD.Load<Theme>("res://scenes/hinterzimmer_thema.tres");

    public const string Familie = "HzFarben", Masse = "HzMasse";

    /// <summary>Hängt das Thema an ein Wurzel-Steuerelement (die Kinder erben es).</summary>
    public static void Anwenden(Control wurzel) => wurzel.Theme = Thema;

    /// <summary>Eine Farbe der Familie („gold“, „grau“, „gruen“, „rot“, „schrift“) – für Stellen, an denen sich die Farbe nach dem Zustand richtet.</summary>
    public static Color Farbe(string name) => Thema.GetColor(name, Familie);

    /// <summary>Ein Maß der Familie (z. B. „seite_breite“ = Breite einer Buchseite, „tat_breite“ = Umbruchbreite der Tat-Spalte).</summary>
    public static int Mass(string name) => Thema.GetConstant(name, Masse);

    /// <summary>
    /// Beschriftung in einer Variante („HzTitel“, „HzSeitentitel“, „HzGruppe“, „HzText“, „HzKlein“, „HzGold“, „HzGut“, „HzSchlecht“, „HzGrund“,
    /// „HzMarke“); <paramref name="breite"/> nennt ein Maß des Themas, bei dem der Text umbricht (leer: kein Umbruch).
    /// </summary>
    public static Label Text(Node eltern, string text, string variante = "HzText", string breite = "", string tipp = "")
    {
        var label = new Label { Text = text, ThemeTypeVariation = variante, TooltipText = tipp, MouseFilter = Control.MouseFilterEnum.Pass };
        if (breite != "")
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(Mass(breite), 0); // Godot kennt beim ersten Aufbau die Breite noch nicht (siehe Hauptmenue.Text)
        }
        eltern.AddChild(label);
        return label;
    }

    /// <summary>Ein Knopf in einer Variante („HzKnopf“, „HzBand“, „HzRegister“, „HzBlaettern“), gleich eingehängt.</summary>
    public static Button Knopf(Node eltern, string text, Action aktion, string variante = "HzKnopf", string tipp = "", bool gesperrt = false)
    {
        var knopf = new Button { Text = text, ThemeTypeVariation = variante, FocusMode = Control.FocusModeEnum.None, TooltipText = tipp, Disabled = gesperrt };
        knopf.Pressed += aktion;
        eltern.AddChild(knopf);
        return knopf;
    }

    /// <summary>Ein Container oder Trenner in einer Variante („HzSpalte“, „HzSeite“, „HzZeile“, „HzRaster“, „HzBlatt“ …), gleich eingehängt.</summary>
    public static T Container<T>(Node eltern, string variante) where T : Control, new()
    {
        var c = new T { ThemeTypeVariation = variante };
        eltern.AddChild(c);
        return c;
    }

    /// <summary>Die Erfolgschance als Balken (0–1) in der Breite „chance_breite“; <paramref name="blass"/> für eine gesperrte Tat.</summary>
    public static ProgressBar Chance(Node eltern, double anteil, bool blass)
    {
        var balken = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, Step = 0.001, Value = Math.Clamp(anteil, 0, 1), ShowPercentage = false, ThemeTypeVariation = blass ? "HzChanceBlass" : "HzChance",
            CustomMinimumSize = new Vector2(Mass("chance_breite"), Mass("chance_hoehe")), MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        eltern.AddChild(balken);
        return balken;
    }

    /// <summary>Ein Zeichen des Buchs (Familie „HzZeichen“ des Themas: „sack“, „flamme“, „dolch“ …, für die Lesebänder mit „_hell“); null, wenn es das Zeichen nicht gibt.</summary>
    public static Texture2D? Zeichen(string name) => Thema.HasIcon(name, "HzZeichen") ? Thema.GetIcon(name, "HzZeichen") : null;

    /// <summary>Das Zeichen vor einer Tat in der Größe „zeichen“; <paramref name="blass"/> (Deckkraft „zeichen_blass“ in Prozent) für eine gesperrte Tat. Ohne Zeichen bleibt der Platz frei, damit die Spalten bündig stehen.</summary>
    public static TextureRect ZeichenBild(Node eltern, string name, bool blass)
    {
        var groesse = Mass("zeichen");
        var bild = new TextureRect
        {
            Texture = Zeichen(name), CustomMinimumSize = new Vector2(groesse, groesse), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            Modulate = blass ? new Color(1, 1, 1, Mass("zeichen_blass") / 100f) : Colors.White,
        };
        eltern.AddChild(bild);
        return bild;
    }

    /// <summary>
    /// Der Titel einer Seite in gebrochener Schrift mit roter, größerer Initiale wie im Entwurf (Typ „HzSeitentitel“; Farbe „initiale“ und
    /// Größe „initiale_groesse“ aus dem Thema). Ein Label kann nur eine Farbe – darum ein RichTextLabel, das sich seinem Inhalt anpasst.
    /// </summary>
    public static RichTextLabel Seitentitel(Node eltern, string text, string tipp = "")
    {
        var sicher = text.Replace("[", "[lb]");
        var erster = text.Length > 0 && text[0] != '[' ? text[..1] : "";
        var rest = erster == "" ? sicher : sicher[1..];
        var titel = new RichTextLabel
        {
            ThemeTypeVariation = "HzSeitentitel", BbcodeEnabled = true, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.Off,
            TooltipText = tipp, MouseFilter = Control.MouseFilterEnum.Pass, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            Text = erster == "" ? rest : $"[font_size={Mass("initiale_groesse")}][color=#{Farbe("initiale").ToHtml(false)}]{erster}[/color][/font_size]{rest}",
        };
        eltern.AddChild(titel);
        return titel;
    }
}
