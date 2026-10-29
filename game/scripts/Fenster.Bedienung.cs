using Godot;

namespace Reichsgilde;

/// <summary>
/// Einstellungen der Bedienung (PR #214): die Zeit bis ein Tooltip festklebt (<see cref="Tooltips"/>) und die gemerkte Lage der Fenster
/// (<see cref="Fensterrahmen"/>). Beides steht in <c>user://einstellungen.cfg</c>, Abschnitte „bedienung“ und „fenster“; fehlen sie, gelten die Grundwerte.
/// </summary>
public partial class Fenster
{
    /// <summary>Kleinste, größte und Grund-Zeit in Sekunden, die ein Tooltip verweilen muss, bis er festklebt.</summary>
    public const double TooltipFestMin = 0.5, TooltipFestMax = 5, TooltipFestStandard = 1.5;

    private static double? _tooltipFest;
    private static bool? _tooltipNurTaste;

    /// <summary>Sekunden bis zum Festkleben eines Tooltips (0,5–5).</summary>
    public static double TooltipFestZeit
    {
        get
        {
            Bedienung();
            return _tooltipFest!.Value;
        }
    }

    /// <summary>Tooltips kleben nur mit der mittleren Maustaste oder der Taste T fest (kein Füllkreis).</summary>
    public static bool TooltipNurMitTaste
    {
        get
        {
            Bedienung();
            return _tooltipNurTaste!.Value;
        }
    }

    private static void Bedienung()
    {
        if (_tooltipFest is not null) return;
        var einstellungen = new ConfigFile();
        var geladen = einstellungen.Load(Datei) == Error.Ok;
        _tooltipFest = geladen ? Math.Clamp(einstellungen.GetValue("bedienung", "tooltip_fest", TooltipFestStandard).AsDouble(), TooltipFestMin, TooltipFestMax)
                               : TooltipFestStandard;
        _tooltipNurTaste = geladen && einstellungen.GetValue("bedienung", "tooltip_nur_taste", false).AsBool();
    }

    /// <summary>Setzt die Zeit bis zum Festkleben und ob nur eine Taste festklebt; <paramref name="speichern"/> = false gilt nur für diesen Start (Startoption <c>--tooltipfest</c>).</summary>
    public static void TooltipFestSetzen(double sekunden, bool nurTaste, bool speichern = true)
    {
        _tooltipFest = Math.Clamp(sekunden, TooltipFestMin, TooltipFestMax);
        _tooltipNurTaste = nurTaste;
        if (!speichern) return;
        var einstellungen = new ConfigFile();
        einstellungen.Load(Datei);
        einstellungen.SetValue("bedienung", "tooltip_fest", _tooltipFest.Value);
        einstellungen.SetValue("bedienung", "tooltip_nur_taste", nurTaste);
        einstellungen.Save(Datei);
    }
}
