using Godot;
using Reichsgilde.Core;

namespace Reichsgilde;

/// <summary>
/// Eine Liste der Spielstände zum Anklicken – im Hauptmenü und im Spielmenü gleich.
/// </summary>
public partial class SpielstandListe : VBoxContainer
{
    private VBoxContainer _eintraege = null!;

    /// <summary>Ein Spielstand wurde gewählt (Pfad der Datei).</summary>
    public event Action<string>? Gewaehlt;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);
        var rollen = new ScrollContainer { CustomMinimumSize = new Vector2(460, 300), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(rollen);
        _eintraege = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _eintraege.AddThemeConstantOverride("separation", 4);
        rollen.AddChild(_eintraege);

        var ort = new Label { Text = $"Ordner: {Spielstaende.OrdnerAufDerPlatte}", AutowrapMode = TextServer.AutowrapMode.Arbitrary, CustomMinimumSize = new Vector2(460, 0) };
        ort.AddThemeFontSizeOverride("font_size", 11);
        ort.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
        AddChild(ort);
    }

    /// <summary>Liest den Ordner neu ein.</summary>
    public void Auffrischen()
    {
        foreach (var kind in _eintraege.GetChildren()) kind.QueueFree();
        var liste = Spielstaende.Liste();
        if (liste.Count == 0)
        {
            _eintraege.AddChild(new Label { Text = "Noch keine Spielstände vorhanden." });
            return;
        }
        foreach (var (pfad, kopf) in liste)
        {
            var auto = pfad.EndsWith(Spielstaende.AutoDatei) ? "  (automatisch)" : "";
            var knopf = new Button
            {
                Text = $"{kopf.Titel}{auto}\n{kopf.Gespeichert:dd.MM.yyyy HH:mm}",
                Alignment = HorizontalAlignment.Left,
                FocusMode = FocusModeEnum.None,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            knopf.Pressed += () => Gewaehlt?.Invoke(pfad);
            _eintraege.AddChild(knopf);
        }
    }
}
