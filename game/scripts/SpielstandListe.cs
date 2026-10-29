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
        UiHilfe.KinderEntfernen(_eintraege);
        var liste = Spielstaende.Liste();
        if (Spielstaende.Veraltete > 0)
        {
            var hinweis = new Label
            {
                Text = $"{Spielstaende.Veraltete} {(Spielstaende.Veraltete == 1 ? "älterer Spielstand stammt" : "ältere Spielstände stammen")} aus der Zeit des 24-Stunden-Tags " +
                       $"und {(Spielstaende.Veraltete == 1 ? "lässt" : "lassen")} sich nicht mehr laden.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(440, 0),
            };
            hinweis.AddThemeFontSizeOverride("font_size", 12);
            hinweis.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
            _eintraege.AddChild(hinweis);
        }
        if (liste.Count == 0)
        {
            _eintraege.AddChild(new Label { Text = "Noch keine Spielstände vorhanden." });
            return;
        }
        foreach (var (pfad, kopf) in liste)
        {
            var auto = pfad.EndsWith(Spielstaende.AutoDatei) ? "  (automatisch)" : "";
            // Kennzeichen: In dieser Partie hat der Spieler die Entwicklerhilfe benutzt (Kern: Speicherstand.MitEntwicklerhilfe, bleibt für immer)
            var hilfe = kopf.MitEntwicklerhilfe ? "  [Entwicklerhilfe]" : "";
            var knopf = new Button
            {
                Text = $"{kopf.Titel}{auto}{hilfe}\n{kopf.Gespeichert:dd.MM.yyyy HH:mm}",
                TooltipText = kopf.MitEntwicklerhilfe ? "Mit Entwicklerhilfe: In dieser Partie wurden Werte von Hand gesetzt." : "",
                Alignment = HorizontalAlignment.Left,
                FocusMode = FocusModeEnum.None,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            knopf.Pressed += () => Gewaehlt?.Invoke(pfad);
            _eintraege.AddChild(knopf);
        }
    }
}
