using Godot;
using Reichsgilde.Core;

namespace Reichsgilde;

/// <summary>
/// Die Probepartie der Kartenwerkstatt („Karte ausprobieren“) in der Hauptszene – ein Teil der <c>partial class Hauptszene</c>.
///
/// Die Werkstatt stellt die Partie auf ihrem Entwurf auf und legt sie in ihren Briefkasten (<see cref="Kartenwerkstatt.ProbeAbholen"/>); die
/// Hauptszene holt sie beim Aufbau ab, statt eine Partie aus dem Hauptmenü zu beginnen. Eine Probepartie schreibt keinen Auto-Stand (er würde
/// den der echten Partie überschreiben), unter dem Zeitstrahl steht ein Hinweis, und das Spielmenü (Esc) hat „Zurück zur Kartenwerkstatt“: Dort ist der Entwurf
/// noch, wie er war, samt Rückgängig.
/// </summary>
public partial class Hauptszene
{
    /// <summary>Läuft eine Probepartie der Kartenwerkstatt?</summary>
    private bool _probepartie;

    /// <summary>Holt die Probepartie ab, verbindet sie und zeigt den Hinweis – null, wenn keine wartet.</summary>
    private Simulation? ProbepartieAbholen()
    {
        if (Kartenwerkstatt.ProbeAbholen() is not var (simulation, spieler)) return null;
        _probepartie = true;
        SimulationVerbinden(simulation, spieler);
        var stadt = simulation.Wirtschaft?.Karte?.Stadt is { Length: > 0 } name ? $"„{name}“" : "der Karte";
        Protokollieren($"Probepartie auf {stadt} als {spieler.Inhaber} – Esc → Spielmenü → „Zurück zur Kartenwerkstatt“. Es wird nichts automatisch gespeichert.");

        var hinweis = new Label
        {
            Text = $"Probepartie auf {stadt} – Esc → Zurück zur Kartenwerkstatt", HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hinweis.AddThemeFontSizeOverride("font_size", 18);
        hinweis.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.45f));
        hinweis.AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.07f, 0.06f));
        hinweis.AddThemeConstantOverride("outline_size", 8);
        hinweis.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.KeepSize, (int)Rollbereich.KopfHoehe); // unter dem Zeitstrahl
        hinweis.GrowHorizontal = Control.GrowDirection.Both;
        GetNode<CanvasLayer>("Oberflaeche").AddChild(hinweis);
        return simulation;
    }

    /// <summary>Im Spielmenü einer Probepartie: der Knopf zurück in die Kartenwerkstatt.</summary>
    private void ProbeKnopfBauen()
    {
        if (!_probepartie) return;
        var knopf = Hauptmenue.Knopf(_menueKnoepfe, "Zurück zur Kartenwerkstatt", () => Kartenwerkstatt.ZurueckAusDerProbe(GetTree()));
        knopf.TooltipText = "Die Probepartie endet; die Karte ist in der Werkstatt, wie sie war – mit Rückgängig und Kamera.";
    }
}
