using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Waren-Knöpfe (128 × 128, Hintergrund nach Beruf, Motiv, Rahmen nach Stufe; zusammengesetzt mit tools/icons/knoepfe_bauen.gd,
/// liegen in res://assets/oberflaeche/icons/).
/// Fehlt ein Icon (z. B. eine neue Ware in den Daten), zeigt das Spiel einen Platzhalter mit dem Anfangsbuchstaben.
/// </summary>
public static class WarenIcons
{
    private static readonly Dictionary<string, Texture2D?> Zwischenspeicher = new();

    /// <summary>„Kräuterschnaps" → „kraeuterschnaps.png" – dieselbe Regel wie im Bauskript.</summary>
    public static string Dateiname(string ware) =>
        ware.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss").Replace(" ", "_") + ".png";

    public static Texture2D? Icon(string ware)
    {
        if (Zwischenspeicher.TryGetValue(ware, out var bild)) return bild;
        var pfad = "res://assets/oberflaeche/icons/" + Dateiname(ware);
        bild = ResourceLoader.Exists(pfad) ? GD.Load<Texture2D>(pfad) : null;
        Zwischenspeicher[ware] = bild;
        return bild;
    }

    /// <summary>
    /// Das Icon als Bildfeld in <paramref name="groesse"/> Pixeln – weich skaliert (linear mit Mipmaps),
    /// damit der gemalte Knopf auch klein sauber aussieht.
    /// </summary>
    public static Control Bild(string ware, int groesse)
    {
        if (Icon(ware) is { } textur)
            return new TextureRect
            {
                Texture = textur,
                TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(groesse, groesse),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
        var platzhalter = new Label
        {
            Text = ware.Length > 0 ? ware[..1] : "?",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(groesse, groesse),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        platzhalter.AddThemeFontSizeOverride("font_size", groesse / 2);
        return platzhalter;
    }
}
