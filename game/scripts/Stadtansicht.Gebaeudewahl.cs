using Godot;

namespace Reichsgilde;

/// <summary>
/// Gebäude wählen für die Entwicklerwerkzeuge (Entwicklerfenster „Blitz auf Gebäude …“, „Brand hier …“) – ein Teil der <c>partial class Stadtansicht</c>:
/// Ein Hinweis oben fordert zum Klick auf ein Gebäude auf, der nächste Klick (nicht über Tafeln) gibt dessen Kennung an die Rückruffunktion, Esc bricht ab.
/// Der Klick wird verbraucht (kein Auswählen, kein Betreten); die Kamera bleibt, wo sie ist. Nach dem Klick endet die Wahl.
/// </summary>
public partial class Stadtansicht
{
    private Action<string>? _gebaeudeWahl;
    private CanvasLayer? _gebaeudeWahlEbene;
    private Label? _gebaeudeWahlText;

    /// <summary>Wartet die Ansicht auf den Klick, der ein Gebäude für ein Entwicklerwerkzeug wählt?</summary>
    private Func<string, bool>? _gebaeudeWahlGueltig;

    public bool GebaeudeWahlLaeuft => _gebaeudeWahl is not null;

    /// <summary>Beginnt die Wahl; <paramref name="gewaehlt"/> bekommt die Kennung des angeklickten Gebäudes. Drinnen (Innenansicht, Raum) geht es nicht: false.</summary>
    public bool GebaeudeWaehlen(string hinweis, Action<string> gewaehlt, Func<string, bool>? gueltig = null)
    {
        _gebaeudeWahlGueltig = gueltig;
        if (_raum is not null || _innen is not null || _entwurf is not null || _ego is not null) return false;
        _gebaeudeWahl = gewaehlt;
        GebaeudeWahlHinweis(hinweis);
        return true;
    }

    /// <summary>Bricht eine laufende Wahl ab.</summary>
    public void GebaeudeWahlAbbrechen()
    {
        _gebaeudeWahl = null;
        if (_gebaeudeWahlEbene is not null) _gebaeudeWahlEbene.Visible = false;
    }

    private void GebaeudeWahlHinweis(string text)
    {
        if (_gebaeudeWahlEbene is null)
        {
            _gebaeudeWahlEbene = new CanvasLayer { Layer = 90, Name = "GebaeudeWahl" };
            AddChild(_gebaeudeWahlEbene);
            _gebaeudeWahlText = new Label
            {
                MouseFilter = Control.MouseFilterEnum.Ignore, AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -320, OffsetRight = 320, OffsetTop = 142, // unter der Kopfleiste (Zeitstrahl und Tempoknöpfe enden bei etwa 132 Pixeln; Hauptszene.Kopf.cs), sonst verdeckt der Hinweis die Beschriftung des Zeitstrahls
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            _gebaeudeWahlText.AddThemeStyleboxOverride("normal", new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.06f, 0.04f, 0.75f), ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 5, ContentMarginBottom = 5,
                CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            });
            _gebaeudeWahlText.AddThemeFontSizeOverride("font_size", 15);
            _gebaeudeWahlText.AddThemeColorOverride("font_color", new Color(1f, 0.97f, 0.88f));
            _gebaeudeWahlEbene.AddChild(_gebaeudeWahlText);
        }
        _gebaeudeWahlText!.Text = text + " · Esc: abbrechen";
        _gebaeudeWahlEbene.Visible = true;
        Input.SetDefaultCursorShape(Input.CursorShape.Cross);
    }

    /// <summary>Eingabe während der Wahl (aus <c>_UnhandledInput</c>, also nicht über Tafeln): Linksklick wählt das Gebäude unter der Maus, Esc bricht ab. True, wenn verbraucht.</summary>
    private bool GebaeudeWahlEingabe(InputEvent eingabe)
    {
        if (_gebaeudeWahl is not { } rueck) return false;
        switch (eingabe)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }:
                GebaeudeWahlEnde();
                return true;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } klick:
                if (GebaeudeWahlKennung(klick.Position) is { } kennung)
                {
                    GebaeudeWahlEnde();
                    rueck(kennung);
                }
                return true; // auch ein Fehlklick: keine normale Aktion
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right }:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Das Gebäude unter der Maus für die Wahl: Der Strahl aus der Kamera trifft die Klickflächen der Reihe nach (nah bis fern, bis zu acht); gewählt wird die erste, die
    /// eine Kennung hat und (falls die Wahl eine Prüfung mitbrachte) ein gültiges Ziel ist – so verdeckt die hohe Klickfläche eines Nachbarn kein Wohnhaus dahinter, und
    /// Figuren und Vögel zählen hier nicht. Jeder Klick steht im Protokoll (Mausposition, getroffene Kennungen, Wahl).
    /// </summary>
    private string? GebaeudeWahlKennung(Vector2 bildschirm)
    {
        var kamera = _raum is not null ? _raumKamera : StadtKamera;
        var von = kamera.ProjectRayOrigin(bildschirm);
        var bis = von + kamera.ProjectRayNormal(bildschirm) * 500;
        var raum = GetWorld3D().DirectSpaceState;
        var aus = new Godot.Collections.Array<Rid>();
        var treffer = new List<string>();
        string? erste = null, gewaehlt = null;
        for (var i = 0; i < 8; i++)
        {
            var anfrage = PhysicsRayQueryParameters3D.Create(von, bis);
            anfrage.Exclude = aus;
            var t = raum.IntersectRay(anfrage);
            if (t.Count == 0) break;
            var koerper = t["collider"].As<CollisionObject3D>();
            aus.Add(koerper.GetRid());
            if (!koerper.HasMeta("kennung")) { treffer.Add("(ohne Kennung)"); continue; }
            var kennung = koerper.GetMeta("kennung").AsString();
            var gueltig = _gebaeudeWahlGueltig?.Invoke(kennung) ?? true;
            treffer.Add($"{kennung}{(gueltig ? "" : " (kein Ziel)")}");
            erste ??= kennung;
            if (gueltig) { gewaehlt = kennung; break; }
        }
        gewaehlt ??= erste; // nichts Gültiges: die Meldung des Entwicklerfensters erklärt dem Nutzer, was an dem ersten Treffer nicht geht
        GD.Print($"[gebaeudewahl] Klick {bildschirm}: Treffer {(treffer.Count == 0 ? "keiner" : string.Join(" > ", treffer))} -> {gewaehlt ?? "nichts getroffen"}");
        return gewaehlt;
    }

    private void GebaeudeWahlEnde()
    {
        GebaeudeWahlAbbrechen();
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
    }
}
