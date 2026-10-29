using Godot;

namespace Reichsgilde;

/// <summary>
/// Macht eine Tafel zum Fenster (PR #214): Sie lässt sich an ihrem oberen Rand ziehen, hat oben rechts ein ✕ (und auf Wunsch einen Knopf
/// „Festhalten“) und kommt beim Anklicken nach vorn. Sie öffnet <b>immer in der Bildschirmmitte</b> (<see cref="Mittig"/>), auch wenn der Spieler sie
/// vorher verschoben hatte; eine Lage wird nicht gemerkt (PR „Lesbarkeit und mittige Fenster“, die gemerkte Lage aus PR #214 entfällt).
///
/// Benutzung: nach <see cref="Rollbereich.Einhuellen"/> einmal <see cref="Anmelden"/> aufrufen – der Rahmen hängt sich als Kind an die Tafel.
/// Eine Tafel ohne eigenen Platz (<see cref="PlatzSetzen"/>, <see cref="Mittig"/> = false) ist „oberste Ebene“ (<c>TopLevel</c>): Kein Container verschiebt sie,
/// der Rahmen setzt jedes Bild ihre Lage (Mitte, bis der Spieler sie zieht), geklemmt in den sichtbaren Bereich.
///
/// Festgehaltene Fenster bleiben offen, wenn die Hauptszene sonst „immer nur eine Tafel“ schließt (<see cref="SchliessenFallsLose"/>).
/// Esc schließt das oberste (zuletzt geöffnete oder angeklickte) Fenster (<see cref="ObersteSchliessen"/>), die Taste X alle (<see cref="AlleSchliessen"/>).
/// </summary>
public partial class Fensterrahmen : Node
{
    private static readonly List<Fensterrahmen> Alle = new();
    private static int _zaehler;

    /// <summary>Höhe des Streifens am oberen Rand, an dem man das Fenster zieht (die Innenabstände der Tafeln sind 20–26 Pixel).</summary>
    private const float Griffhoehe = 30;

    private readonly PanelContainer _tafel;
    private readonly string _schluessel;
    private Action _schliessen;
    private readonly bool _schliessbar;
    private readonly bool _festhaltbar;
    private Rollbereich? _rolle;
    private bool _umhaengen;
    private Control? _leiste;
    private Button? _fest;
    private bool _bewegt, _zieht;
    private int _geoeffnet;
    private Vector2 _ziel, _griff;
    private int _zuletzt;
    private Func<Vector2, Vector2, Vector2>? _platz;

    /// <summary>Bleibt offen, wenn eine andere Tafel „alles andere schließt“.</summary>
    public bool Festgehalten { get; private set; }

    /// <summary>
    /// true (Standard): Bei jedem Öffnen steht das Fenster in der Mitte des Bildschirms (senkrecht in der Mitte zwischen Zeitstrahl und Betriebsleiste, damit es
    /// beide frei lässt); es hält dort, bis der Spieler es zieht. false: eigene Regel über <see cref="PlatzSetzen"/> oder der Platz, den der Anker vorgibt
    /// (die Auswahl an der Maus, Charakter, Bau- und Wegtafel rechts).
    /// </summary>
    public bool Mittig { get; set; } = true;

    /// <summary>Versatz zwischen zwei festgehaltenen Fenstern, die zugleich offen sind (Pixel je weiteres Fenster).</summary>
    private const float Staffel = 28;

    /// <summary>Linke obere Ecke eines Fensters in der Mitte: waagrecht im Bild, senkrecht im Streifen zwischen Zeitstrahl und Betriebsleiste; weitere festgehaltene Fenster leicht versetzt.</summary>
    private Vector2 MitteLage(Vector2 groesse, Vector2 sichtbar)
    {
        var frei = new Rect2(0, Rollbereich.KopfHoehe, sichtbar.X, Math.Max(groesse.Y, sichtbar.Y - Rollbereich.KopfHoehe - Rollbereich.LeistenHoehe));
        var davor = Alle.Count(a => a != this && a.Festgehalten && a.Mittig && a._platz is not null && a._geoeffnet < _geoeffnet && a._tafel.IsVisibleInTree());
        return new Vector2(
            frei.Position.X + (frei.Size.X - groesse.X) / 2 + davor * Staffel,
            frei.Position.Y + (frei.Size.Y - groesse.Y) / 2 + davor * Staffel);
    }

    /// <summary>
    /// Legt fest, wo das Fenster steht, bis der Spieler es zieht: <paramref name="platz"/> bekommt Größe der Tafel und sichtbaren Bereich und liefert die linke obere Ecke
    /// (jedes Bild neu, die Größe ändert sich ja). Ziehen beendet das; null löscht die Vorgabe.
    /// </summary>
    public void PlatzSetzen(Func<Vector2, Vector2, Vector2>? platz)
    {
        _platz = platz;
        if (platz is null) return;
        Mittig = false;
        _bewegt = true;
    }

    /// <summary>Zählt bei Esc mit (false für die Auswahl, die im Ablauf von Esc ihren festen Platz hat).</summary>
    public bool FuerEsc { get; set; } = true;

    private Fensterrahmen(PanelContainer tafel, string schluessel, Action? schliessen, bool schliessbar, bool festhaltbar)
    {
        _tafel = tafel;
        _schluessel = schluessel;
        _schliessen = schliessen ?? tafel.Hide;
        _schliessbar = schliessbar;
        _festhaltbar = festhaltbar;
        Name = "Fensterrahmen";
    }

    /// <summary>
    /// Macht die Tafel zum Fenster. <paramref name="schluessel"/> benennt es (nur zur Unterscheidung). <paramref name="schliessen"/> ist, was das ✕ tut
    /// (Grundwert: die Tafel verstecken). <paramref name="schliessbar"/> = false: nur ziehbar, ohne ✕ und ohne Esc (Bautafeln, Rückfragen).
    /// <paramref name="festhaltbar"/>: ein Knopf „Festhalten“ neben dem ✕.
    /// </summary>
    public static Fensterrahmen Anmelden(PanelContainer tafel, string schluessel, Action? schliessen = null, bool schliessbar = true, bool festhaltbar = false)
    {
        var rahmen = new Fensterrahmen(tafel, schluessel, schliessen, schliessbar, festhaltbar);
        tafel.AddChild(rahmen);
        return rahmen;
    }

    /// <summary>Der Rahmen einer Tafel, sonst null.</summary>
    public static Fensterrahmen? Von(Node tafel) => tafel.GetNodeOrNull<Fensterrahmen>("Fensterrahmen");

    /// <summary>Was das ✕ dieser Tafel tut, neu bestimmen (die Hauptszene kennt z. B. das Schließen der Auswahl).</summary>
    public Fensterrahmen SchliessenSetzen(Action schliessen)
    {
        _schliessen = schliessen;
        return this;
    }

    /// <summary>
    /// Versteckt die Tafel, außer der Spieler hält sie fest – für „es ist immer nur eine Tafel offen“ der Hauptszene.
    /// Tafeln ohne Rahmen werden einfach versteckt.
    /// </summary>
    public static void SchliessenFallsLose(Control tafel, bool erzwingen = false)
    {
        if (Von(tafel) is { } rahmen)
        {
            if (rahmen.Festgehalten && !erzwingen) return;
            rahmen.Festgehalten = false;
            rahmen.FestAktualisieren();
        }
        tafel.Hide();
    }

    /// <summary>Schließt das oberste sichtbare Fenster (Esc). Gibt false zurück, wenn keins offen ist.</summary>
    public static bool ObersteSchliessen()
    {
        Fensterrahmen? oberste = null;
        foreach (var rahmen in Alle)
            if (rahmen._schliessbar && rahmen.FuerEsc && rahmen._tafel.IsVisibleInTree() && (oberste is null || rahmen._zuletzt > oberste._zuletzt))
                oberste = rahmen;
        if (oberste is null) return false;
        oberste._schliessen();
        return true;
    }

    /// <summary>Schließt alle sichtbaren Fenster, auch die festgehaltenen (Taste X).</summary>
    public static void AlleSchliessen()
    {
        foreach (var rahmen in Alle.ToArray())
        {
            if (!rahmen._schliessbar || !rahmen._tafel.IsVisibleInTree()) continue;
            rahmen.Festgehalten = false;
            rahmen.FestAktualisieren();
            rahmen._schliessen();
        }
    }

    /// <summary>Ist irgendein schließbares Fenster offen?</summary>
    public static bool IrgendeinesOffen => Alle.Any(r => r._schliessbar && r._tafel.IsVisibleInTree());

    public override void _EnterTree()
    {
        if (!Alle.Contains(this)) Alle.Add(this); // auch nach dem Umhängen in die Fensterebene
    }

    public override void _Ready()
    {
        _tafel.VisibilityChanged += BeimZeigen;
        _rolle = _tafel.GetNodeOrNull<Rollbereich>("Rollbereich");
        if (_rolle is not null) _rolle.GuiInput += BeiEingabe;
        if (Mittig && _rolle is not null)
        {
            // Zeitstrahl oben und Betriebsleiste unten bleiben frei; eine hohe Tafel rollt, bevor sie an sie stößt (F4: Rat überdeckte Zeit und Vermögen)
            _rolle.ObenFrei = Rollbereich.KopfHoehe - _rolle.Abstand;
            _rolle.UntenFrei = Rollbereich.LeistenHoehe - _rolle.Abstand;
        }
        if (_schliessbar || _festhaltbar) Callable.From(LeisteBauen).CallDeferred(); // die Tafel ist beim Eintritt noch „beschäftigt“ und nimmt kein Kind an
        if (_tafel.Visible) BeimZeigen();
    }

    public override void _ExitTree()
    {
        Alle.Remove(this);
    }

    private void LeisteBauen()
    {
        // Als „oberste Ebene“ liegt die Leiste über der Tafel, ohne dass deren Container sie anfasst; ihre Lage setzt _Process.
        _leiste = new HBoxContainer { TopLevel = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        _leiste.AddThemeConstantOverride("separation", 4);
        _tafel.AddChild(_leiste);
        if (_festhaltbar)
        {
            _fest = Kreuz.Knopf(Kreuz.Art.Nadel, "Festhalten: Das Fenster bleibt offen, wenn eine andere Tafel geöffnet wird.");
            _fest.ToggleMode = true;
            _fest.Toggled += an =>
            {
                Festgehalten = an;
                if (an && !_bewegt) { _bewegt = true; _ziel = _tafel.GlobalPosition; } // bleibt, wo es ist; eine neue Tafel nimmt den Standardplatz
                FestAktualisieren();
            };
            _leiste.AddChild(_fest);
        }
        if (_schliessbar)
        {
            var zu = Kreuz.Knopf(Kreuz.Art.Kreuz, "Schließen");
            zu.Pressed += () => _schliessen();
            _leiste.AddChild(zu);
        }
    }

    private void FestAktualisieren()
    {
        if (_fest is null) return;
        _fest.SetPressedNoSignal(Festgehalten);
        _fest.QueueRedraw();
    }

    private void BeimZeigen()
    {
        if (!_tafel.Visible)
        {
            if (_schliessbar) KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.FensterZu); // nur schließbare Fenster (keine Leisten und Hinweise)
            return;
        }
        if (_schliessbar) KlangOberflaeche.Spielen(KlangOberflaeche.Ereignis.FensterAuf);
        _zuletzt = ++_zaehler;
        _geoeffnet = _zuletzt;
        if (Mittig) // immer wieder in die Mitte, auch nach dem Ziehen
        {
            _platz = MitteLage;
            _bewegt = true;
        }
        NachVornBringen();
    }

    /// <summary>
    /// Die Fensterebene: ein durchlässiges Steuerelement in der Oberfläche, über allen Tafeln an ihrem Platz und unter Spielmenü, Hilfe und Bericht.
    /// Verschobene Fenster hängen hier, damit sich ihre Reihenfolge frei ändern lässt (Mitte und rechter Rand sind sonst feste Schichten).
    /// </summary>
    private static Control? _ebene;

    private void Umhaengen()
    {
        _umhaengen = false;
        if (!IsInstanceValid(_tafel) || !_tafel.IsInsideTree()) return;
        if (_ebene is null || !IsInstanceValid(_ebene) || !_ebene.IsInsideTree())
        {
            var schicht = _tafel.GetParent();
            while (schicht is not null and not CanvasLayer) schicht = schicht.GetParent();
            if (schicht is null) return;
            _ebene = new Control { Name = "Fensterebene", MouseFilter = Control.MouseFilterEnum.Ignore };
            _ebene.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            schicht.AddChild(_ebene);
            var hoechster = 0;
            foreach (var a in Alle)
            {
                Node? oben = a._tafel;
                while (oben is not null && oben.GetParent() != schicht) oben = oben.GetParent();
                if (oben is not null && oben != _ebene) hoechster = Math.Max(hoechster, oben.GetIndex());
            }
            schicht.MoveChild(_ebene, hoechster + 1);
        }
        if (_tafel.GetParent() != _ebene) _tafel.Reparent(_ebene, false);
        _ebene.MoveChild(_tafel, -1);
    }

    /// <summary>Hebt die Tafel über die anderen Fenster desselben Elternknotens (nicht über Knoten, die keine Fenster sind: Spielmenü, Kopfzeile).</summary>
    private void NachVornBringen()
    {
        _zuletzt = ++_zaehler;
        var eltern = _tafel.GetParent();
        if (eltern is null) return;
        if (eltern == _ebene)
        {
            eltern.MoveChild(_tafel, -1);
            return;
        }
        // Ein Fenster, das noch an seinem Platz steht, aber von einem verschobenen verdeckt wird, wird auch frei – sonst käme es nie nach vorn
        if (!_bewegt && _ebene is not null && IsInstanceValid(_ebene) && Alle.Any(a => a != this && a._tafel.GetParent() == _ebene && a._tafel.IsVisibleInTree()
                                                                                        && a._tafel.GetGlobalRect().Intersects(_tafel.GetGlobalRect())))
        {
            _bewegt = true;
            _ziel = _tafel.GlobalPosition;
            return;
        }
        var hoechster = _tafel.GetIndex();
        foreach (var anderes in Alle)
            if (anderes != this && anderes._tafel.GetParent() == eltern)
                hoechster = Math.Max(hoechster, anderes._tafel.GetIndex());
        if (hoechster > _tafel.GetIndex()) eltern.MoveChild(_tafel, hoechster);
    }

    private void BeiEingabe(InputEvent eingabe)
    {
        if (eingabe is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } klick || _zieht) return;
        if (_rolle is null) return;
        var imFenster = _rolle.Position + klick.Position; // Lage in der Tafel
        var rechts = _leiste is null ? 0 : _leiste.Size.X + 12;
        if (imFenster.Y > Griffhoehe || imFenster.X > _tafel.Size.X - rechts) return;
        _zieht = true;
        _platz = null;
        _griff = _tafel.GetViewport().GetMousePosition() - _tafel.GlobalPosition;
        _ziel = _tafel.GlobalPosition;
    }

    /// <summary>Jeder Klick in ein Fenster holt es nach vorn – auch ein Klick auf einen Knopf darin (der nie bis zum Rollbereich kommt).</summary>
    public override void _Input(InputEvent eingabe)
    {
        if (eingabe is not InputEventMouseButton { Pressed: true } || !_tafel.IsVisibleInTree()) return;
        var maus = _tafel.GetViewport().GetMousePosition();
        if (!_tafel.GetGlobalRect().HasPoint(maus)) return;
        foreach (var anderes in Alle)
            if (anderes != this && anderes._zuletzt > _zuletzt && anderes._tafel.IsVisibleInTree() && anderes._tafel.GetGlobalRect().HasPoint(maus))
                return; // ein Fenster darüber bekommt den Klick
        if (_zuletzt != _zaehler) NachVornBringen();
    }

    public override void _Process(double delta)
    {
        if (!_tafel.IsVisibleInTree()) return;
        var sichtbar = _tafel.GetViewportRect().Size;

        if (_zieht)
        {
            if (Input.IsMouseButtonPressed(MouseButton.Left))
            {
                _ziel = _tafel.GetViewport().GetMousePosition() - _griff;
                _bewegt = true;
            }
            else
            {
                _zieht = false;
                _ziel = Klemmen(_ziel, sichtbar);
            }
        }
        if (_platz is not null && !_zieht)
        {
            _ziel = _platz(_tafel.Size, sichtbar);
            _bewegt = true;
        }
        if (_bewegt)
        {
            if (!_tafel.TopLevel) _tafel.TopLevel = true; // ab jetzt hält kein Container und kein Anker die Tafel mehr
            if (!_umhaengen && _tafel.GetParent() != _ebene)
            {
                _umhaengen = true;
                Callable.From(Umhaengen).CallDeferred();
            }
            _ziel = Klemmen(_ziel, sichtbar);
            if (!_tafel.Position.IsEqualApprox(_ziel)) _tafel.Position = _ziel;
        }
        if (_leiste is not null)
        {
            var ort = _tafel.GlobalPosition + new Vector2(_tafel.Size.X - _leiste.Size.X - 10, 6);
            if (!_leiste.GlobalPosition.IsEqualApprox(ort)) _leiste.GlobalPosition = ort;
        }
    }

    /// <summary>Hält die Tafel im Bild: Der Kopf (zum Ziehen) bleibt immer erreichbar, auch wenn das Fenster kleiner wurde.</summary>
    private Vector2 Klemmen(Vector2 lage, Vector2 sichtbar)
    {
        var groesse = _tafel.Size;
        return new Vector2(
            Math.Clamp(lage.X, 0, Math.Max(0, sichtbar.X - groesse.X)),
            Math.Clamp(lage.Y, 0, Math.Max(0, sichtbar.Y - groesse.Y)));
    }
}

/// <summary>Kleine Knöpfe mit selbst gezeichnetem Zeichen (✕, Nadel): Sie brauchen keine Schrift, die das Zeichen kennt.</summary>
public static class Kreuz
{
    public enum Art { Kreuz, Nadel }

    private static readonly Color Linie = new(0.92f, 0.88f, 0.80f);
    private static readonly Color Gold = new(0.95f, 0.82f, 0.45f);

    public static Button Knopf(Art art, string tooltip, float groesse = 22)
    {
        var knopf = new Button
        {
            Flat = true, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(groesse, groesse),
            TooltipText = tooltip, MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        knopf.Draw += () =>
        {
            var m = knopf.Size / 2;
            var an = knopf.IsHovered() || (art == Art.Nadel && knopf.ButtonPressed);
            var farbe = an ? Gold : Linie;
            if (an) knopf.DrawRect(new Rect2(Vector2.Zero, knopf.Size), new Color(1, 1, 1, 0.10f));
            if (art == Art.Kreuz)
            {
                var d = groesse * 0.22f;
                knopf.DrawLine(m + new Vector2(-d, -d), m + new Vector2(d, d), farbe, 2, true);
                knopf.DrawLine(m + new Vector2(-d, d), m + new Vector2(d, -d), farbe, 2, true);
            }
            else
            {
                // Reißzwecke: Kopf, Scheibe und Nadel
                var kopf = new Vector2(0, -groesse * 0.14f) + m;
                knopf.DrawCircle(kopf, groesse * 0.20f, knopf.ButtonPressed ? Gold : new Color(farbe, 0.35f));
                knopf.DrawArc(kopf, groesse * 0.20f, 0, Mathf.Tau, 20, farbe, 1.5f, true);
                knopf.DrawLine(m + new Vector2(-groesse * 0.20f, groesse * 0.06f), m + new Vector2(groesse * 0.20f, groesse * 0.06f), farbe, 2, true);
                knopf.DrawLine(m + new Vector2(0, groesse * 0.06f), m + new Vector2(0, groesse * 0.32f), farbe, 1.5f, true);
            }
        };
        knopf.MouseEntered += knopf.QueueRedraw;
        knopf.MouseExited += knopf.QueueRedraw;
        return knopf;
    }
}
