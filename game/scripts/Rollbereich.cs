using Godot;

namespace Reichsgilde;

/// <summary>
/// Hält eine Tafel innerhalb des Fensters: Ist ihr Inhalt höher (oder breiter) als das Fenster, wird er
/// gerollt statt abgeschnitten. Passt der Inhalt, ist die Tafel genau so groß wie vorher und ohne Balken.
///
/// Warum ein eigenes Steuerelement? Ein ScrollContainer hat in Godot keine „natürliche" Größe: Er ist so
/// klein, wie man ihn macht. Ohne Hilfe wäre jede Tafel entweder winzig oder immer gleich hoch. Der
/// Rollbereich misst deshalb jedes Bild seinen Inhalt und setzt seine Mindestgröße auf
/// min(Inhalt, Platz im Fenster). Das ist billig – es sind nur ein paar Zahlen.
///
/// Benutzung: <see cref="Einhuellen"/> am Ende des Aufbaus einer Tafel aufrufen.
/// </summary>
public partial class Rollbereich : ScrollContainer
{
    private PanelContainer _tafel = null!;
    private Control _inhalt = null!;

    /// <summary>Luft zwischen Tafel und Fensterrand (je Seite) – wie der Abstand der Ecktafeln.</summary>
    public float Abstand { get; set; } = 16;

    /// <summary>
    /// Legt einen Rollbereich zwischen die Tafel und ihren Inhalt (das einzige Kind, meist ein MarginContainer).
    /// Eine feste Breite der Tafel wandert auf den Inhalt – sonst könnte die Tafel nie schmaler als das Fenster werden.
    /// </summary>
    public static Rollbereich Einhuellen(PanelContainer tafel, float abstand = 16)
    {
        var inhalt = tafel.GetChild<Control>(0);
        var rolle = new Rollbereich { _tafel = tafel, _inhalt = inhalt, Abstand = abstand, Name = "Rollbereich" };
        var breite = tafel.CustomMinimumSize.X;
        if (breite > 0)
        {
            var rahmen = tafel.GetThemeStylebox("panel").GetMinimumSize().X;
            inhalt.CustomMinimumSize = new Vector2(Mathf.Max(inhalt.CustomMinimumSize.X, breite - rahmen), inhalt.CustomMinimumSize.Y);
            tafel.CustomMinimumSize = new Vector2(0, tafel.CustomMinimumSize.Y);
        }
        tafel.AddChild(rolle);
        inhalt.Reparent(rolle, false); // Reparent behält den Besitzer – %Namen aus der Szene finden ihn weiter
        return rolle;
    }

    public override void _Ready()
    {
        HorizontalScrollMode = ScrollMode.Auto;
        VerticalScrollMode = ScrollMode.Auto;
        // Mausrad über einer Tafel rollt die Tafel – und nie die Kamera dahinter. Container lassen Mausereignisse
        // sonst durch (MouseFilter Pass): Was die Tafel nicht verbraucht (Rad am Ende der Liste, Klick auf
        // leere Fläche), landete in _UnhandledInput der Stadtansicht und zoomte oder wählte ein Gebäude.
        MouseFilter = MouseFilterEnum.Stop;
        _tafel.MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Process(double delta)
    {
        if (!_tafel.IsVisibleInTree()) return;

        var fenster = GetViewportRect().Size;
        var rahmen = _tafel.GetThemeStylebox("panel").GetMinimumSize();
        var platz = fenster - rahmen - new Vector2(2 * Abstand, 2 * Abstand);
        var inhalt = _inhalt.GetCombinedMinimumSize();
        var balkenBreite = GetVScrollBar().GetCombinedMinimumSize().X;
        var balkenHoehe = GetHScrollBar().GetCombinedMinimumSize().Y;

        // Braucht es einen senkrechten Balken? Dann wird es auch einen Balken breiter – und umgekehrt.
        var senkrecht = inhalt.Y > platz.Y;
        var waagrecht = inhalt.X + (senkrecht ? balkenBreite : 0) > platz.X;
        if (waagrecht && !senkrecht) senkrecht = inhalt.Y + balkenHoehe > platz.Y;

        var groesse = new Vector2(
            Mathf.Max(60, Mathf.Min(inhalt.X + (senkrecht ? balkenBreite : 0), platz.X)),
            Mathf.Max(60, Mathf.Min(inhalt.Y + (waagrecht ? balkenHoehe : 0), platz.Y)));
        if (!groesse.IsEqualApprox(CustomMinimumSize))
            CustomMinimumSize = groesse;

        TafelAnpassen();
    }

    /// <summary>
    /// Ecktafeln (Kontor, Auswahl) hängen an Ankern statt in einem Container. Godot lässt solche Tafeln
    /// wachsen, aber nie schrumpfen – nach einem kleineren Fenster bliebe die Tafel groß und der Balken
    /// erschiene nie. Deshalb setzen wir die Größe selbst und halten dabei die angeheftete Ecke fest.
    /// </summary>
    private void TafelAnpassen()
    {
        if (_tafel.GetParent() is Container) return; // Container (z. B. die Mitte) erledigen das selbst
        var soll = _tafel.GetCombinedMinimumSize();
        var ist = _tafel.Size;
        if (soll.IsEqualApprox(ist)) return;
        var ort = _tafel.Position;
        if (_tafel.GrowHorizontal == GrowDirection.Begin) ort.X += ist.X - soll.X;
        if (_tafel.GrowVertical == GrowDirection.Begin) ort.Y += ist.Y - soll.Y;
        _tafel.Size = soll;
        _tafel.Position = ort;
    }
}
