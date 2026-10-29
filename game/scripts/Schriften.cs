using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Schriften des Spiels (beide frei unter der SIL Open Font License, Lizenztexte in res://assets/oberflaeche/schriften/):
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

    /// <summary>
    /// Kleinster Schriftgrad für lesbaren Text (bei 1600×900): Tabellenköpfe, Hilfezeilen, Fußzeilen und Beschriftungen, die Tafeln mit 11–13 angeben,
    /// werden beim Eintritt in den Baum auf diesen Wert angehoben. Eine Stelle für das ganze Spiel.
    /// </summary>
    public const int MindestGroesse = 14;

    /// <summary>Grund der Tafeln (Tafelfarbe aus <see cref="Hauptmenue.Tafel"/>): Gegen ihn wird der Kontrast gemessen.</summary>
    private static readonly Color TafelGrund = new(0.12f, 0.11f, 0.10f);

    /// <summary>Kontrast nach WCAG (1 bis 21) zweier Farben.</summary>
    public static float Kontrast(Color a, Color b)
    {
        static float Leuchtdichte(Color c)
        {
            static float Linear(float x) => x <= 0.03928f ? x / 12.92f : MathF.Pow((x + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * Linear(c.R) + 0.7152f * Linear(c.G) + 0.0722f * Linear(c.B);
        }
        var (hell, dunkel) = (Leuchtdichte(a), Leuchtdichte(b));
        if (hell < dunkel) (hell, dunkel) = (dunkel, hell);
        return (hell + 0.05f) / (dunkel + 0.05f);
    }

    /// <summary>
    /// Hellt eine blasse Schriftfarbe auf, bis sie auf dem Tafelgrund mindestens 4,5 : 1 erreicht. Dunkle Farben (Schrift auf hellem Grund,
    /// Leuchtdichte unter 0,1 wie auf Goldknöpfen) bleiben unberührt.
    /// </summary>
    public static Color LesbareFarbe(Color farbe)
    {
        if (Kontrast(farbe, TafelGrund) >= 4.5f || farbe.Luminance < 0.2f) return farbe;
        for (var t = 0.1f; t <= 1f; t += 0.1f)
        {
            var heller = farbe.Lerp(Colors.White, t);
            heller.A = farbe.A;
            if (Kontrast(heller, TafelGrund) >= 4.5f) return heller;
        }
        return farbe;
    }

    private static FontVariation? _text;
    private static FontVariation? _ueberschrift;

    /// <summary>
    /// Alegreya mit „lnum“ und „tnum“: Ziffern in Versalhöhe und alle gleich breit. Von Haus aus hat Alegreya
    /// Mediävalziffern (wie im Buch: 3, 4, 5 hängen unter die Zeile) – schön im Satz, aber in Spalten mit Gulden-Beträgen unruhig.
    /// </summary>
    public static FontVariation Text => _text ??= new FontVariation
    {
        BaseFont = GD.Load<FontFile>("res://assets/oberflaeche/schriften/Alegreya-Variabel.ttf"),
        VariationOpentype = new Godot.Collections.Dictionary { { Kennzeichen("wght"), 500 } },
        OpentypeFeatures = new Godot.Collections.Dictionary { { Kennzeichen("lnum"), 1 }, { Kennzeichen("tnum"), 1 } },
    };

    /// <summary>OpenType-Kürzel („wght“, „lnum“) → Zahl, wie Godot sie als Schlüssel erwartet.</summary>
    private static long Kennzeichen(string name) => TextServerManager.GetPrimaryInterface().NameToTag(name);

    public static FontVariation Ueberschrift => _ueberschrift ??= new FontVariation
    {
        BaseFont = GD.Load<FontFile>("res://assets/oberflaeche/schriften/GermaniaOne-Regular.ttf"),
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
            if (knoten is not Control steuer || steuer.HasMeta(Merker)) return;
            if (steuer is Label label)
            {
                if (!label.HasThemeFontSizeOverride("font_size") || label.HasThemeFontOverride("font")) return;
                label.SetMeta(Merker, true); // ein Label kann den Baum verlassen und wieder betreten
                var groesse = label.GetThemeFontSize("font_size");
                if (groesse >= UeberschriftAb)
                    label.AddThemeFontOverride("font", Ueberschrift);
                else
                {
                    label.AddThemeFontOverride("font", Text);
                    label.AddThemeFontSizeOverride("font_size", Math.Max(groesse + TextZuschlag, MindestGroesse));
                }
                if (label.HasThemeColorOverride("font_color")) label.AddThemeColorOverride("font_color", LesbareFarbe(label.GetThemeColor("font_color")));
                return;
            }
            // Knöpfe, Auswahlfelder, Eingabefelder und Textfelder: nur die Mindestgröße (sie hatten schon die richtige Schrift)
            foreach (var name in new[] { "font_size", "normal_font_size" })
                if (steuer.HasThemeFontSizeOverride(name) && steuer.GetThemeFontSize(name) < MindestGroesse)
                {
                    steuer.SetMeta(Merker, true);
                    steuer.AddThemeFontSizeOverride(name, MindestGroesse);
                }
        };
    }

    private const string Merker = "rg_schrift";

    /// <summary>
    /// Beim Beenden aufrufen (Autoload <see cref="Fenster"/>): Thema und Ersatzschrift lassen die Schriften los, die eigenen
    /// Verweise werden sofort freigegeben. Sonst hielte C# sie bis zum Abbau der Laufzeit fest – dann kann die letzte
    /// Freigabe nach dem Textserver kommen, und Godot meldet dessen Glyphen-Texturen als Leck („DummyTexture leaked“)
    /// oder stürzt ab.
    /// </summary>
    public static void Freigeben()
    {
        ThemeDB.GetDefaultTheme().DefaultFont = null;
        ThemeDB.FallbackFont = null;
        _ueberschrift?.Dispose();
        _ueberschrift = null;
        _text?.Dispose();
        _text = null;
    }
}
