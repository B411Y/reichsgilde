using Godot;

namespace Reichsgilde;

/// <summary>
/// Das Kleid eines Fensters (UI-Umbau „Zunftlade“, Teil 2): Es hängt als Kind an einer Tafel (<see cref="PanelContainer"/>) und zeichnet ihr den
/// <b>Rand aus Stein</b>, den <b>Leib aus Pergament</b> und – wenn sie eine Überschrift hat – den <b>Kopf aus Holz</b> mit dem Titel. Dazu setzt es
/// der Tafel das Thema der Fenster (<see cref="FensterStil"/>): Alles darin schreibt in dunkler Tinte, Knöpfe sind aus Holz.
///
/// <b>Titel:</b> Die Tafeln tragen ihre Überschrift als erstes Label im Inhalt (Schriftgrad ab <see cref="Schriften.UeberschriftAb"/>). Das Kleid
/// findet es, blendet es aus und schreibt seinen Text in den Kopf; ändert die Tafel den Text, ändert sich der Kopf. Eine Tafel ohne solche
/// Überschrift (die Auswahl rechts) bekommt Rand und Pergament, aber keinen Kopf. <see cref="Titel"/> setzt ihn fest.
///
/// Was gezeichnet wird, steht im Thema: „FensterStoff“ (Grund, Rahmen), „FensterKopf“ (Holz, Höhe, Schrift), „FensterTafel“ und
/// „FensterTafelKopf“ (Innenabstand ohne und mit Kopf).
/// </summary>
public partial class Fensterkleid : Node
{
    private readonly PanelContainer _tafel;
    private Label? _ueberschrift;
    private string _titel = "";
    private bool _stilGesetzt;
    private StyleBox? _stil;

    /// <summary>Fester Titel statt der Überschrift der Tafel (null: selbst suchen; leer: kein Kopf).</summary>
    public string? Titel { get; set; }

    /// <summary>Breite, die rechts im Kopf die Knöpfe brauchen (setzt der <see cref="Fensterrahmen"/>; ohne Knöpfe 0).</summary>
    public float KnopfPlatz
    {
        get => _knopfPlatz;
        set
        {
            if (Mathf.IsEqualApprox(_knopfPlatz, value)) return;
            _knopfPlatz = value;
            _tafel.QueueRedraw();
        }
    }

    private float _knopfPlatz;

    /// <summary>Hat das Fenster gerade einen Kopf?</summary>
    public bool MitKopf => _titel.Length > 0;

    /// <summary>Breite des Steinrands.</summary>
    public static float Rand => FensterStil.Mass("rahmen_breite", "FensterStoff");

    /// <summary>Höhe von Rand und Kopf zusammen: der Streifen, an dem man das Fenster zieht.</summary>
    public float Griffhoehe => MitKopf ? Rand + FensterStil.Mass("hoehe", "FensterKopf") : FensterStil.Mass("griff_ohne_kopf");

    public Fensterkleid(PanelContainer tafel)
    {
        _tafel = tafel;
        Name = "Fensterkleid";
    }

    public override void _Ready()
    {
        // Das eigene Thema der Tafel oder das ihrer Eltern (Ratskammer, Stimmrunde) bleibt gültig; das Pergament kommt darunter
        Theme? eigenes = null;
        for (Node? n = _tafel; n is not null && eigenes is null; n = n.GetParent())
            eigenes = (n as Control)?.Theme;
        _tafel.Theme = FensterStil.ThemaMit(eigenes);
        _tafel.SetMeta(FensterStil.Stoff, FensterStil.Pergament);
        _tafel.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        _tafel.Draw += Zeichnen;
        _tafel.Resized += _tafel.QueueRedraw;
        TitelLesen();
        StilSetzen();
        FensterStil.AllesWandeln(_tafel);
    }

    public override void _Process(double delta)
    {
        if (!_tafel.IsVisibleInTree()) return;
        var vorher = _titel;
        TitelLesen();
        // Eine Tafel, die sich später selbst wieder einen dunklen Kasten gibt, bekommt das Kleid zurück
        if (vorher == _titel && _stilGesetzt && _tafel.GetThemeStylebox("panel") == _stil) return;
        StilSetzen();
        _tafel.QueueRedraw();
    }

    private void StilSetzen()
    {
        _stilGesetzt = true;
        _stil = FensterStil.Thema.GetStylebox("panel", MitKopf ? "FensterTafelKopf" : "FensterTafel");
        _tafel.AddThemeStyleboxOverride("panel", _stil);
    }

    private void TitelLesen()
    {
        if (Titel is not null)
        {
            _titel = Titel;
            return;
        }
        if (_ueberschrift is null || !IsInstanceValid(_ueberschrift) || !_tafel.IsAncestorOf(_ueberschrift))
            _ueberschrift = UeberschriftSuchen(_tafel, 0);
        if (_ueberschrift is null)
        {
            _titel = "";
            return;
        }
        if (_ueberschrift.Visible) _ueberschrift.Visible = false;
        _titel = _ueberschrift.Text.Trim();
    }

    /// <summary>
    /// Die Überschrift ist das erste sichtbare Label ganz oben im Inhalt: Der Weg dorthin führt immer durch das erste sichtbare Kind (höchstens ein
    /// Bild oder Abstand davor). Ist dieses Label keine Überschrift, hat die Tafel keine.
    /// </summary>
    private Label? UeberschriftSuchen(Node knoten, int tiefe)
    {
        if (tiefe > 7) return null;
        var uebersprungen = 0;
        foreach (var kind in knoten.GetChildren())
        {
            if (kind is not Control { TopLevel: false } steuer || (!steuer.Visible && steuer != _ueberschrift)) continue;
            if (steuer is Label label)
                return label.GetThemeFontSize("font_size") >= Schriften.UeberschriftAb && label.Text.Trim().Length > 0 ? label : null;
            if (steuer is Container or ScrollContainer) return UeberschriftSuchen(steuer, tiefe + 1);
            if (++uebersprungen > 1) return null; // ein Bild oder Abstand vor dem Titel ist in Ordnung, mehr nicht
        }
        return null;
    }

    private void Zeichnen()
    {
        var thema = FensterStil.Thema;
        var flaeche = new Rect2(Vector2.Zero, _tafel.Size);
        var rand = Rand;
        var innen = flaeche.Grow(-rand);
        if (innen.Size.X <= 0 || innen.Size.Y <= 0) return;
        LadeZeichner.Grund(_tafel, thema.GetIcon("grund", "FensterStoff"), innen, thema.GetConstant("grund_kachel", "FensterStoff"), _tafel.Position);
        if (MitKopf)
        {
            var hoehe = thema.GetConstant("hoehe", "FensterKopf");
            var kopf = new Rect2(innen.Position, new Vector2(innen.Size.X, hoehe));
            LadeZeichner.Grund(_tafel, thema.GetIcon("grund", "FensterKopf"), kopf, thema.GetConstant("grund_kachel", "FensterKopf"), Vector2.Zero);
            var linie = thema.GetConstant("linie", "FensterKopf");
            _tafel.DrawRect(new Rect2(kopf.Position.X, kopf.End.Y - linie, kopf.Size.X, linie), thema.GetColor("linie", "FensterKopf"));
            var schrift = thema.GetFont("font", "FensterKopf");
            var grad = thema.GetFontSize("font_size", "FensterKopf");
            var links = thema.GetConstant("titel_links", "FensterKopf");
            // Rechts bleibt Platz für die Knöpfe (Schließen, Festhalten); ein Titel, der nicht passt, wird kleiner gesetzt (bis „font_size_klein“)
            var breite = Math.Max(40, kopf.Size.X - 2 * links - KnopfPlatz);
            var klein = thema.GetFontSize("font_size_klein", "FensterKopf");
            while (grad > klein && schrift.GetStringSize(_titel, HorizontalAlignment.Left, -1, grad).X > breite) grad--;
            var grundlinie = kopf.Position.Y + (hoehe - linie + schrift.GetAscent(grad) - schrift.GetDescent(grad)) / 2;
            _tafel.DrawString(schrift, new Vector2(kopf.Position.X + links, grundlinie), _titel, HorizontalAlignment.Left, breite, grad, thema.GetColor("font_color", "FensterKopf"));
        }
        LadeZeichner.Rahmen(_tafel, thema.GetIcon("rahmen", "FensterStoff"), flaeche, thema.GetConstant("rahmen_quelle", "FensterStoff"), (int)rand, LadeFlaeche.Seiten.Alle);
    }
}
