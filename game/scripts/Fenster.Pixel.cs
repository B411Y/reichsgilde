using Godot;

namespace Reichsgilde;

/// <summary>
/// Der Pixel-Look (Nutzer 03.11.2026, Versuch Runde 4 „Pixel 2 fein“) – ein Teil der <c>partial class Fenster</c>: aus (Standard, der gemalt-realistische
/// Look), Pixel 2 oder Pixel 3 (die Welt der Stadtansicht wird mit 1/2 bzw. 1/3 der Fensterauflösung gerechnet und hart hochgezogen) und „Glühen“
/// (weicher Schein um Fenster, Feuer und Laternen). Alle übrigen Werte stehen fest im Shader (Stadtansicht.Pixel.cs, <c>pixel_farbe.gdshader</c>).
/// Die Wahl gehört nicht zu den Voreinstellungen von Niedrig bis Ultra: Sie ändert den Stil, nicht die Güte.
/// </summary>
public partial class Fenster
{
    public static PixelLook AktuellerPixelLook { get; private set; } = PixelLook.Aus;

    /// <summary>Glühen im Pixel-Look (gerechnet im Low-Res-Maßstab, sehr billig); wirkt nur bei Pixel 2 und Pixel 3.</summary>
    public static bool PixelGluehenAn { get; private set; } = true;

    /// <summary>Wie viele Fensterpixel ein Weltpixel breit sind: 1 = Pixel-Look aus.</summary>
    public static int PixelGroesse => AktuellerPixelLook switch { PixelLook.Pixel2 => 2, PixelLook.Pixel3 => 3, _ => 1 };

    public static void PixelLookSetzen(PixelLook look, bool speichern = true)
    {
        AktuellerPixelLook = look;
        if (speichern) Speichern("pixellook", look.ToString());
        Melden();
    }

    public static void PixelGluehenSetzen(bool an, bool speichern = true)
    {
        PixelGluehenAn = an;
        if (speichern) Speichern("pixelgluehen", an);
        Melden();
    }

    /// <summary>Liest die Wahl beim Start (aus <c>GrafikLaden</c>): ohne gespeicherten Wert aus.</summary>
    private static void PixelLaden(ConfigFile datei, bool geladen)
    {
        var text = geladen ? datei.GetValue("anzeige", "pixellook", PixelLook.Aus.ToString()).AsString() : PixelLook.Aus.ToString();
        AktuellerPixelLook = Enum.TryParse<PixelLook>(text, out var look) && Enum.IsDefined(look) ? look : PixelLook.Aus;
        PixelGluehenAn = !geladen || datei.GetValue("anzeige", "pixelgluehen", true).AsBool();
    }

    /// <summary>Startoptionen <c>--pixellook=0|2|3</c> (auch aus/Pixel2/Pixel3) und <c>--pixelgluehen=0|1</c>: nur für diesen Start (Aufruf aus <c>StartoptionenAnwenden</c>).</summary>
    private static void PixelStartoptionen(Dictionary<string, string> optionen)
    {
        if (optionen.TryGetValue("pixellook", out var wert))
        {
            var look = wert switch { "0" => PixelLook.Aus, "2" => PixelLook.Pixel2, "3" => PixelLook.Pixel3, _ => Enum.TryParse<PixelLook>(wert, true, out var l) ? l : AktuellerPixelLook };
            PixelLookSetzen(look, speichern: false);
        }
        if (optionen.TryGetValue("pixelgluehen", out var gluehen))
            PixelGluehenSetzen(!gluehen.Equals("Aus", StringComparison.OrdinalIgnoreCase) && gluehen != "0", speichern: false);
    }
}

/// <summary>Pixel-Look der Welt: Aus (gemalt-realistisch), Pixel 2 (Weltpixel = 2 × 2 Fensterpixel), Pixel 3 (3 × 3).</summary>
public enum PixelLook { Aus, Pixel2, Pixel3 }
