using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Wetterzeile der Zeittafel oben links (PR #195): das Symbol des Wetters gerade jetzt mit seinem Namen, daneben klein die nächsten
/// Wetterabschnitte des Tages mit ihrer Uhrzeit. Der Tooltip sagt dasselbe in Worten. Die Symbole zeichnet <c>tools/icons/wetter_icons.py</c>
/// (Holzschnitt wie die übrigen Icons), sie liegen in <c>res://assets/oberflaeche/icons/wetter/</c>. Ohne Wetter (keine Partie) bleibt die Zeile leer.
/// </summary>
public sealed partial class WetterAnzeige : HBoxContainer
{
    private const int Gross = 30, Klein = 20, Aussichten = 3;

    private readonly TextureRect _symbol;
    private readonly Label _name;
    private readonly HBoxContainer _aussicht;
    private string _gezeigt = "";

    public WetterAnzeige()
    {
        Name = "Wetter";
        Alignment = AlignmentMode.Center;
        MouseFilter = MouseFilterEnum.Stop; // für den Tooltip
        AddThemeConstantOverride("separation", 6);
        _symbol = Symbolfeld(Gross);
        AddChild(_symbol);
        _name = new Label { MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        _name.AddThemeFontSizeOverride("font_size", 16);
        AddChild(_name);
        _aussicht = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _aussicht.AddThemeConstantOverride("separation", 2);
        AddChild(_aussicht);
    }

    private static readonly Dictionary<Wetterlage, Texture2D?> Symbole = new();

    /// <summary>Das Symbol einer Lage (null, wenn die Datei fehlt).</summary>
    public static Texture2D? Symbol(Wetterlage lage)
    {
        if (Symbole.TryGetValue(lage, out var bild)) return bild;
        var pfad = $"res://assets/oberflaeche/icons/wetter/{lage.ToString().ToLowerInvariant()}.png";
        return Symbole[lage] = ResourceLoader.Exists(pfad) ? GD.Load<Texture2D>(pfad) : null;
    }

    private static TextureRect Symbolfeld(int groesse) => new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(groesse, groesse),
        MouseFilter = MouseFilterEnum.Ignore,
    };

    private static string Uhrzeit(int minuten) => $"{minuten / 60:00}:{minuten % 60:00}";

    /// <summary>Zeigt das Wetter (jedes Bild gerufen, neu aufgebaut wird nur bei einer Änderung).</summary>
    public void Zeigen(Wetterlage? jetzt, IReadOnlyList<(int Uhrzeit, Wetterlage Lage)> aussicht)
    {
        var schluessel = jetzt + "|" + string.Join(",", aussicht.Take(Aussichten).Select(a => $"{a.Uhrzeit}:{a.Lage}"));
        if (schluessel == _gezeigt) return;
        _gezeigt = schluessel;
        Visible = jetzt is not null;
        if (jetzt is not { } lage) return;

        _symbol.Texture = Symbol(lage);
        _name.Text = Wetterablauf.Name(lage);
        foreach (var alt in _aussicht.GetChildren()) alt.QueueFree();
        var tipp = $"Wetter jetzt: {Wetterablauf.Name(lage)}";
        if (aussicht.Count == 0) tipp += "\nBis zum Abend bleibt es so.";
        else
        {
            _aussicht.AddChild(new Label { Text = "→", MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(0.75f, 0.75f, 0.75f) });
            tipp += "\nAussicht:";
            foreach (var (uhrzeit, folgt) in aussicht.Take(Aussichten))
            {
                var feld = Symbolfeld(Klein);
                feld.Texture = Symbol(folgt);
                _aussicht.AddChild(feld);
                var zeit = new Label { Text = $"{uhrzeit / 60}", MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
                zeit.AddThemeFontSizeOverride("font_size", 12);
                zeit.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
                _aussicht.AddChild(zeit);
                tipp += $"\n  ab {Uhrzeit(uhrzeit)} Uhr {Wetterablauf.Name(folgt).ToLowerInvariant()}";
            }
        }
        TooltipText = tipp;
    }
}
