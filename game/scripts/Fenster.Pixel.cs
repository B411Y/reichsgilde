using Godot;

namespace Reichsgilde;

/// <summary>
/// Der Pixel-Look (Nutzer 03.11.2026, Versuch Runde 4 „Pixel 2 fein“; seit PR „drei Zoomstufen“ Standard) – ein Teil der <c>partial class Fenster</c>: ein Schalter an/aus
/// (Standard an; aus = der gemalt-realistische Look mit stufenlosem Zoom) und „Glühen“ (weicher Schein um Fenster, Feuer und Laternen). Die Größe der Weltpixel
/// wählt die Stadtansicht je Zoomstufe selbst (weit und mittel 2 × 2 Fensterpixel, nah und Innenräume 3 × 3; Stadtansicht.Pixel.cs).
/// Alle übrigen Werte stehen fest im Shader (<c>pixel_farbe.gdshader</c>). Die Wahl gehört nicht zu den Voreinstellungen von Niedrig bis Ultra: Sie ändert den Stil, nicht die Güte.
/// </summary>
public partial class Fenster
{
    /// <summary>Pixel-Look an (Standard). Aus: der gemalt-realistische Look.</summary>
    public static bool PixelLookAn { get; private set; } = true;

    /// <summary>Glühen im Pixel-Look (gerechnet im Low-Res-Maßstab, sehr billig); wirkt nur bei eingeschaltetem Pixel-Look.</summary>
    public static bool PixelGluehenAn { get; private set; } = true;

    /// <summary>Die Pixelgröße, die die Stadtansicht gerade wählt (2 oder 3, je Zoomstufe); nur sie schreibt.</summary>
    internal static int PixelGroesseGewaehlt { get; set; } = 2;

    /// <summary>Wie viele Fensterpixel ein Weltpixel breit sind: 1 = Pixel-Look aus.</summary>
    public static int PixelGroesse => PixelLookAn ? PixelGroesseGewaehlt : 1;

    public static void PixelLookSetzen(bool an, bool speichern = true)
    {
        PixelLookAn = an;
        if (speichern) Speichern("pixellook_an", an);
        Melden();
    }

    public static void PixelGluehenSetzen(bool an, bool speichern = true)
    {
        PixelGluehenAn = an;
        if (speichern) Speichern("pixelgluehen", an);
        Melden();
    }

    /// <summary>Lädt die Wahl; ohne gespeicherten Wert (neue Spieler, ältere Einstellungsdateien) ist der Pixel-Look an. Der frühere Schlüssel „pixellook“ (Aus/Pixel2/Pixel3) gilt nicht mehr.</summary>
    private static void PixelLaden(ConfigFile datei, bool geladen)
    {
        PixelLookAn = !geladen || datei.GetValue("anzeige", "pixellook_an", true).AsBool();
        PixelGluehenAn = !geladen || datei.GetValue("anzeige", "pixelgluehen", true).AsBool();
    }

    /// <summary>Startoptionen <c>--pixellook=0|1</c> (die früheren Werte 2 und 3 und Pixel2/Pixel3 gelten als „an“, Aus/aus als „aus“) und <c>--pixelgluehen=0|1</c>: nur für diesen Start (Aufruf aus <c>StartoptionenAnwenden</c>).</summary>
    private static void PixelStartoptionen(Dictionary<string, string> optionen)
    {
        if (optionen.TryGetValue("pixellook", out var wert))
            PixelLookSetzen(!(wert == "0" || wert.Equals("Aus", StringComparison.OrdinalIgnoreCase) || wert.Equals("false", StringComparison.OrdinalIgnoreCase)), speichern: false);
        if (optionen.TryGetValue("pixelgluehen", out var gluehen))
            PixelGluehenSetzen(!gluehen.Equals("Aus", StringComparison.OrdinalIgnoreCase) && gluehen != "0", speichern: false);
    }
}
