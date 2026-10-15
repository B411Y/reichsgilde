using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Schriften des Spiels (beide frei unter der SIL Open Font License, Lizenztexte in res://assets/schriften/):
///   • <b>Alegreya</b> – Buchschrift für allen Text in Tafeln, Knöpfen und Listen,
///   • <b>Germania One</b> – gebrochene Schrift für Überschriften und die Namen in der Stadt.
///
/// So wirkt es überall, ohne dass jede Tafel etwas tun muss:
///   1. Alegreya wird die Schrift des Standard-Themas und die Ersatzschrift (<c>ThemeDB</c>) – jedes Control ohne
///      eigene Schrift nimmt sie.
///   2. Jedes Label mit einer Schriftgröße ab <see cref="UeberschriftAb"/> Pixeln bekommt Germania One, sobald es in
///      den Baum kommt (Ereignis <c>SceneTree.NodeAdded</c>). Auch Tafeln anderer Chats sind damit abgedeckt.
/// </summary>
public static class Schriften
{
    /// <summary>Ab dieser Schriftgröße gilt ein Label als Überschrift.</summary>
    public const int UeberschriftAb = 20;

    /// <summary>
    /// Alegreya hat kleinere Kleinbuchstaben als die alte Standardschrift: Bei gleicher Pixelzahl wirkt sie kleiner.
    /// Deshalb bekommt Fließtext einen Pixel mehr (die Tafeln geben ihre Größen für die alte Schrift an).
    /// </summary>
    public const int TextZuschlag = 1;

    private static FontVariation? _text;
    private static FontVariation? _ueberschrift;

    /// <summary>
    /// Alegreya mit „lnum“ und „tnum“: Ziffern in Versalhöhe und alle gleich breit. Von Haus aus hat Alegreya
    /// Mediävalziffern (wie im Buch: 3, 4, 5 hängen unter die Zeile) – schön im Satz, aber in Spalten mit Gulden-Beträgen unruhig.
    /// </summary>
    public static FontVariation Text => _text ??= new FontVariation
    {
        BaseFont = GD.Load<FontFile>("res://assets/schriften/Alegreya-Variabel.ttf"),
        VariationOpentype = new Godot.Collections.Dictionary { { Kennzeichen("wght"), 500 } },
        OpentypeFeatures = new Godot.Collections.Dictionary { { Kennzeichen("lnum"), 1 }, { Kennzeichen("tnum"), 1 } },
    };

    /// <summary>OpenType-Kürzel („wght“, „lnum“) → Zahl, wie Godot sie als Schlüssel erwartet.</summary>
    private static long Kennzeichen(string name) => TextServerManager.GetPrimaryInterface().NameToTag(name);

    public static FontVariation Ueberschrift => _ueberschrift ??= new FontVariation
    {
        BaseFont = GD.Load<FontFile>("res://assets/schriften/GermaniaOne-Regular.ttf"),
        // Fehlt Germania ein Zeichen (z. B. „×“), springt Alegreya ein statt eines leeren Kästchens.
        Fallbacks = new Godot.Collections.Array<Font> { Text },
    };

    /// <summary>Einmal beim Start aufrufen (Autoload <see cref="Fenster"/>).</summary>
    public static void Einrichten(SceneTree baum)
    {
        // Das eingebaute Standard-Thema bringt eine eigene Schrift mit – die wird ersetzt; die Ersatzschrift
        // gilt für alles ohne Thema (z. B. Label3D in der Stadt).
        ThemeDB.GetDefaultTheme().DefaultFont = Text;
        ThemeDB.GetDefaultTheme().DefaultFontSize = 16 + TextZuschlag;
        ThemeDB.FallbackFont = Text;
        baum.NodeAdded += knoten =>
        {
            if (knoten is not Label label || !label.HasThemeFontSizeOverride("font_size") || label.HasThemeFontOverride("font"))
                return;
            var groesse = label.GetThemeFontSize("font_size");
            if (groesse >= UeberschriftAb)
                label.AddThemeFontOverride("font", Ueberschrift);
            else
            {
                // Merken, dass schon vergrößert wurde – ein Label kann den Baum verlassen und wieder betreten.
                label.AddThemeFontOverride("font", Text);
                label.AddThemeFontSizeOverride("font_size", groesse + TextZuschlag);
            }
        };
    }
}
