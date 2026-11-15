using Godot;

namespace Reichsgilde;

// Der Handschuh über Popups: Unterfenster (OptionButton-Liste, PopupMenu, Dialoge) werden von Godot über allen CanvasLayern des Hauptfensters gezeichnet,
// also auch über dem Handschuh (Ebene 128): dort verschwand der Zeiger (der System-Zeiger ist unsichtbar gestellt). Darum bekommt jedes Fenster außer dem Hauptfenster
// eine eigene Ebene 128 mit demselben Bild der Bühne (HandschuhSpiegel); sie liest die Lage vom Handschuh und zeichnet nur, solange das Fenster sichtbar ist.
public partial class HandschuhZeiger
{
    private int _spiegel;

    /// <summary>Das Bild der Bühne (für die Spiegel in den Unterfenstern).</summary>
    public Texture2D Textur => _bild.GetTexture();

    private bool _textImFenster;

    /// <summary>Ein Spiegel meldet ein Textfeld unter der Maus in seinem Unterfenster (Dialog, Popup): das Messing-I gilt auch dort; gelesen und gelöscht im nächsten Bild.</summary>
    public void TextImFensterMelden() => _textImFenster = true;

    /// <summary>Textfeld in einem Unterfenster seit dem letzten Bild? Liest und löscht die Meldung der Spiegel.</summary>
    private bool TextImFensterNehmen()
    {
        var war = _textImFenster;
        _textImFenster = false;
        return war;
    }

    private void SpiegelAnmelden()
    {
        GetTree().NodeAdded += FensterPruefen;
        foreach (var fenster in GetTree().Root.FindChildren("*", "Window", true, false).OfType<Window>()) FensterPruefen(fenster);
    }

    private void FensterPruefen(Node knoten)
    {
        if (knoten is not Window fenster || fenster == GetTree().Root || fenster.GetNodeOrNull("HandschuhSpiegel") is not null) return;
        fenster.CallDeferred(Node.MethodName.AddChild, new HandschuhSpiegel(this)); // der Eltern-Knoten richtet gerade seine Kinder ein
        _spiegel++;
        if (LebenLog) GD.Print($"[spiegel] Handschuh-Ebene für Fenster {fenster.GetType().Name} {fenster.Name} (Nr. {_spiegel})");
    }

    /// <summary>
    /// Wo und wie groß der Handschuh in diesem Unterfenster zu zeichnen ist (Einheiten des Fensters), oder false, wenn er nicht zu sehen ist. Zeichenfläche und Zeigerpunkt des Hauptfensters
    /// werden über die Faktoren beider Fenster (Oberflächengröße) umgerechnet; die Mausposition kommt vom Unterfenster selbst.
    /// </summary>
    public bool SpiegelLage(Window fenster, out Vector2 position, out Vector2 groesse)
    {
        position = groesse = Vector2.Zero;
        if (!_flaeche.Visible) return false;
        var hauptSkala = Math.Max(GetWindow().GetFinalTransform().Scale.X, 0.1f);
        var fensterSkala = Math.Max(fenster.GetFinalTransform().Scale.X, 0.1f);
        var verhaeltnis = hauptSkala / fensterSkala;
        groesse = _flaeche.Size * verhaeltnis;
        var maus = fenster.GetMousePosition();
        var (x, y) = HandschuhModell.FensterPosition(maus.X, maus.Y, _imBildLetzte.X * verhaeltnis, _imBildLetzte.Y * verhaeltnis);
        position = new Vector2((float)x, (float)y);
        return true;
    }
}

/// <summary>Die Handschuh-Ebene (128) eines Unterfensters: zeichnet das Bild der Bühne an der Mausposition dieses Fensters; unsichtbar, solange das Fenster zu ist oder der Handschuh nicht gilt.</summary>
public partial class HandschuhSpiegel : CanvasLayer
{
    private readonly HandschuhZeiger _quelle;
    private TextureRect _flaeche = null!;
    private Window? _fenster;

    public HandschuhSpiegel(HandschuhZeiger quelle)
    {
        _quelle = quelle;
        Layer = 128;
        Name = "HandschuhSpiegel";
        ProcessMode = ProcessModeEnum.Always;
        ProcessPriority = 1001; // nach dem Handschuh selbst (1000)
    }

    public override void _Ready()
    {
        _fenster = GetParent() as Window;
        _flaeche = new TextureRect
        {
            Texture = _quelle.Textur, MouseFilter = Control.MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = CanvasItem.TextureFilterEnum.Linear, Visible = false, FocusMode = Control.FocusModeEnum.None,
        };
        AddChild(_flaeche);
        if (_fenster is not null) // geschlossene Fenster brauchen kein Bild-für-Bild (es gibt über 80 Popups)
        {
            SetProcess(_fenster.Visible);
            _fenster.VisibilityChanged += () => SetProcess(_fenster.Visible);
        }
    }

    public override void _Process(double delta)
    {
        if (_fenster is null || !IsInstanceValid(_quelle) || !_fenster.Visible || !_quelle.SpiegelLage(_fenster, out var position, out var groesse))
        {
            _flaeche.Visible = false;
            return;
        }
        _flaeche.Position = position;
        _flaeche.Size = groesse;
        _flaeche.Visible = true;
        // Textfeld im Unterfenster (das Hauptfenster sieht seine Steuerelemente nicht): dem Handschuh melden, er wechselt zum Messing-I
        if (_fenster.GuiGetHoveredControl() is { } steuerung && _fenster.GetVisibleRect().HasPoint(_fenster.GetMousePosition())
            && steuerung.GetCursorShape(steuerung.GetLocalMousePosition()) == Control.CursorShape.Ibeam)
            _quelle.TextImFensterMelden();
    }
}
