using Godot;
using Reichsgilde.Core;

namespace Reichsgilde;

/// <summary>
/// Kamera und Maus in Räumen aus Modellen – zwölfter Teil der <c>partial class Stadtansicht</c> (PR #152).
///
/// <b>Blickpunkte:</b> Jeder Raum des Modells hat feste Blickpunkte (<c>Blick_&lt;Raum&gt;_0</c> in der Mitte, <c>_L1</c>, <c>_L2</c> nach links,
/// <c>_R1</c> … nach rechts, <c>_O1</c> nach oben, <c>_U1</c> nach unten). Die Kamera steht auf einem Blickpunkt (sie blickt mit −Z, wie der Anker);
/// ein Blickpunkt weiter schwenkt sie weich. Fährt die Maus an einen Bildrand und bleibt dort kurz, geht es zum nächsten Blickpunkt in
/// dieser Richtung; goldene Pfeile am Rand zeigen, wie oft es noch geht (einfach = einmal, doppelt = zweimal oder öfter, keiner = Ende).
/// Die Blickpunkte liegen auf Ketten: links ← … L2, L1, 0, R1, R2 … → rechts, ebenso oben O1, 0, U1 unten.
///
/// <b>Maus:</b> Unter der Maus erscheint der Name von Gegenstand, Tür, Einrichtung, Warenstapel oder Arbeiter. Ein Klick auf Bedienung
/// öffnet die Tafel (Ereignis <see cref="BedienungAngeklickt"/>, die Hauptszene öffnet sie), auf eine Tür wechselt er den Raum, auf einen Arbeiter
/// wählt er ihn (Umschalt oder Strg: dazuwählen); mit gedrückter linker Taste auf freier Fläche zieht man einen Rahmen um mehrere. Rechtsklick verlässt das Haus.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Blickpunkt: Kürzel („0", „L1", „R2", „O1", „U1") und der Anker im Modell.</summary>
    private sealed record Blickpunkt(string Kuerzel, Node3D Anker)
    {
        /// <summary>Richtung des Blickpunkts: 'L', 'R', 'O', 'U' – oder '0' für die Mitte.</summary>
        public char Seite => Kuerzel[0];

        /// <summary>Wie weit er in seiner Richtung liegt (L1 = 1, L2 = 2; die Mitte 0).</summary>
        public int Weite => Kuerzel.Length > 1 && int.TryParse(Kuerzel[1..], out var n) ? n : 0;

        /// <summary>Reihenfolge in Listen: Mitte, dann links, rechts, oben, unten.</summary>
        public int Rang => Seite == '0' ? 0 : ("LROU".IndexOf(Seite) + 1) * 10 + Weite;
    }

    /// <summary>So breit (Pixel) ist die Randzone, in der die Maus schwenkt – klein, damit es nicht ungewollt geschieht.</summary>
    private const float RandZone = 22f;

    /// <summary>So lange (Sekunden) muss die Maus in der Randzone bleiben, bis die Kamera schwenkt.</summary>
    private const double RandVerweil = 0.5;

    /// <summary>Wie schnell die Kamera zum Blickpunkt schwenkt (größer = schneller).</summary>
    private const float SchwenkTempo = 6f;

    private Blickpunkt? _blickpunkt;
    private Transform3D _blickZiel;
    private bool _mausBewegt;
    private bool _mausImFenster = true;
    private char _randRichtung = ' ';
    private double _randZeit;
    private int? _hoverArbeiter;
    private string? _hoverTag;

    private CanvasLayer _raumUi = null!;
    private RaumPfeile _raumPfeile = null!;
    private RaumRahmen _raumRahmen = null!;
    private PanelContainer _raumSchild = null!;
    private Label _raumSchildName = null!, _raumSchildHinweis = null!;
    private Vector2? _druckPunkt;
    private bool _rahmenZieht;
    private readonly HashSet<int> _gewaehlt = new();

    /// <summary>Ein Gegenstand der Bedienung wurde angeklickt: Tafel („Personal", „Produktion", „Lager", „Karren", „Einrichten") und Name des Gegenstands.</summary>
    public event Action<string, string>? BedienungAngeklickt;

    /// <summary>Die Auswahl der Arbeiter im Raum hat sich geändert.</summary>
    public event Action? ArbeiterAuswahlGeaendert;

    /// <summary>Nummern der Arbeiter, die im Raum ausgewählt sind (für die Produktionstafel).</summary>
    public IReadOnlyCollection<int> GewaehlteArbeiter => _gewaehlt;

    /// <summary>Die Hauptszene meldet, dass die Maus über einer offenen Tafel liegt: Dann kein Schild, kein Randschwenken.</summary>
    public bool UnterTafel { get; set; }

    /// <summary>Ob der gezeigte Raum aus einem Modell stammt (feste Blickpunkte, Bedienung zum Anklicken).</summary>
    public bool HatBedienung => _raum?.Blicke is not null;

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau der Oberfläche im Raum
    // ═════════════════════════════════════════════════════════════════════════

    private void RaumOberflaecheAufbauen()
    {
        _raumUi = new CanvasLayer { Name = "RaumOberflaeche", Visible = false };
        AddChild(_raumUi);
        // Verlässt die Maus das Fenster, bleibt ihre letzte Stelle am Rand stehen: Dann darf nicht weitergeschwenkt werden.
        GetWindow().MouseExited += () => _mausImFenster = false;
        GetWindow().MouseEntered += () => _mausImFenster = true;
        _raumRahmen = new RaumRahmen();
        _raumUi.AddChild(_raumRahmen);
        _raumPfeile = new RaumPfeile();
        _raumUi.AddChild(_raumPfeile);

        _raumSchild = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _raumSchild.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.10f, 0.08f, 0.95f),
            BorderColor = new Color(0.62f, 0.50f, 0.32f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6,
        });
        var spalte = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        spalte.AddThemeConstantOverride("separation", 0);
        _raumSchild.AddChild(spalte);
        _raumSchildName = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _raumSchildName.AddThemeFontSizeOverride("font_size", 17);
        _raumSchildName.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.45f));
        spalte.AddChild(_raumSchildName);
        _raumSchildHinweis = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _raumSchildHinweis.AddThemeFontSizeOverride("font_size", 12);
        _raumSchildHinweis.AddThemeColorOverride("font_color", new Color(0.78f, 0.74f, 0.66f));
        spalte.AddChild(_raumSchildHinweis);
        _raumUi.AddChild(_raumSchild);
    }

    /// <summary>Beim Betreten oder Verlassen: Pfeile und Schild an oder aus, Auswahl und Randzeit zurücksetzen.</summary>
    private void RaumOberflaecheUmschalten(bool innen)
    {
        _raumUi.Visible = innen;
        _randRichtung = ' ';
        _randZeit = 0;
        _druckPunkt = null;
        _rahmenZieht = false;
        _raumRahmen.Rahmen = null;
        _raumSchild.Visible = false;
        _hoverArbeiter = null;
        if (!innen && _gewaehlt.Count > 0)
        {
            _gewaehlt.Clear();
            ArbeiterAuswahlGeaendert?.Invoke();
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Blickpunkte
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Stellt die Kamera auf einen Blickpunkt – sofort oder mit dem weichen Schwenk von <see cref="RaumKameraNachfuehren"/>.</summary>
    private void BlickSetzen(Blickpunkt blick, bool sofort)
    {
        _blickpunkt = blick;
        _blickZiel = new Transform3D(blick.Anker.GlobalTransform.Basis.Orthonormalized(), blick.Anker.GlobalPosition);
        if (sofort) _raumKamera.GlobalTransform = _blickZiel;
    }

    /// <summary>Wählt den Blickpunkt mit diesem Kürzel („0", „L1", „R2", „O1" …) im gezeigten Raum, sofort (Startoption --innenblick). Gibt false zurück, wenn es ihn nicht gibt.</summary>
    public bool InnenBlickWaehlen(string kuerzel)
    {
        if (_raum?.Blicke?.FirstOrDefault(b => b.Kuerzel.Equals(kuerzel, StringComparison.OrdinalIgnoreCase)) is not { } blick) return false;
        BlickSetzen(blick, true);
        return true;
    }

    /// <summary>Die Blickpunkte einer Richtung als Kette von der äußersten Seite bis zur anderen: links … L2, L1, 0, R1, R2 … rechts (waagerecht) bzw. O … 0 … U (senkrecht).</summary>
    private List<Blickpunkt> BlickKette(Raum raum, bool waagerecht)
    {
        var (davor, danach) = waagerecht ? ('L', 'R') : ('O', 'U');
        var blicke = raum.Blicke!;
        return blicke.Where(b => b.Seite == davor).OrderByDescending(b => b.Weite)
            .Concat(blicke.Where(b => b.Seite == '0'))
            .Concat(blicke.Where(b => b.Seite == danach).OrderBy(b => b.Weite)).ToList();
    }

    /// <summary>Wie viele Blickpunkte es von hier aus in dieser Richtung ('L', 'R', 'O', 'U') noch gibt, und welcher der nächste ist.</summary>
    private (int Anzahl, Blickpunkt? Naechster) BlickSchritt(char richtung)
    {
        if (_raum?.Blicke is null || _blickpunkt is null) return (0, null);
        var kette = BlickKette(_raum, richtung is 'L' or 'R');
        var stelle = kette.IndexOf(_blickpunkt);
        if (stelle < 0) return (0, null); // auf der anderen Kette (z. B. oben): hier geht es nicht weiter
        var schritt = richtung is 'L' or 'O' ? -1 : 1;
        var anzahl = schritt < 0 ? stelle : kette.Count - 1 - stelle;
        return (anzahl, anzahl > 0 ? kette[stelle + schritt] : null);
    }

    /// <summary>Die Kamera schwenkt weich zum Ziel: Blickrichtung und Ort laufen jedes Bild ein Stück darauf zu.</summary>
    private void RaumKameraNachfuehren(double delta)
    {
        if (_blickpunkt is null) return;
        var ist = _raumKamera.GlobalTransform;
        if (ist.IsEqualApprox(_blickZiel)) return;
        var t = 1f - Mathf.Exp(-SchwenkTempo * (float)delta);
        var drehung = ist.Basis.GetRotationQuaternion().Slerp(_blickZiel.Basis.GetRotationQuaternion(), t);
        var ort = ist.Origin.Lerp(_blickZiel.Origin, t);
        _raumKamera.GlobalTransform = ort.DistanceTo(_blickZiel.Origin) < 0.002f && drehung.AngleTo(_blickZiel.Basis.GetRotationQuaternion()) < 0.0005f
            ? _blickZiel
            : new Transform3D(new Basis(drehung), ort);
    }

    public override void _Input(InputEvent eingabe)
    {
        if (eingabe is InputEventMouseMotion) _mausBewegt = true; // vorher kennt der Rand die Mausstelle nicht (Fenster ohne Maus, Klicktest)
    }

    /// <summary>Bleibt die Maus am Bildrand, schwenkt die Kamera zum nächsten Blickpunkt in dieser Richtung.</summary>
    private void RaumRandPruefen(double delta, bool ueberOberflaeche)
    {
        var groesse = GetViewport().GetVisibleRect().Size;
        var maus = GetViewport().GetMousePosition();
        var richtung = ' ';
        if (_mausBewegt && !ueberOberflaeche && !_rahmenZieht && _druckPunkt is null && _mausImFenster
            && maus.X >= 0 && maus.Y >= 0 && maus.X < groesse.X && maus.Y < groesse.Y)
        {
            if (maus.X < RandZone) richtung = 'L';
            else if (maus.X > groesse.X - RandZone) richtung = 'R';
            else if (maus.Y < RandZone) richtung = 'O';
            else if (maus.Y > groesse.Y - RandZone) richtung = 'U';
        }
        if (richtung != _randRichtung)
        {
            _randRichtung = richtung;
            _randZeit = 0;
            return;
        }
        if (richtung == ' ') return;
        _randZeit += delta;
        if (_randZeit < RandVerweil) return;
        _randZeit = 0; // der nächste Schritt braucht wieder die volle Verweilzeit
        if (BlickSchritt(richtung).Naechster is { } blick) BlickSetzen(blick, false);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild im Raum
    // ═════════════════════════════════════════════════════════════════════════

    private void RaumBildAktualisieren(double delta)
    {
        if (_raum?.Blicke is null) return;
        var ueberOberflaeche = UnterTafel || GetViewport().GuiGetHoveredControl() is not null;
        RaumKameraNachfuehren(delta);
        RaumRandPruefen(delta, ueberOberflaeche);
        _raumPfeile.Setzen(BlickSchritt('L').Anzahl, BlickSchritt('R').Anzahl, BlickSchritt('O').Anzahl, BlickSchritt('U').Anzahl);

        // Name unter der Maus
        var tag = ueberOberflaeche || _rahmenZieht ? null : InnenTagBei(GetViewport().GetMousePosition());
        _hoverArbeiter = tag is not null && tag.StartsWith("arbeiter:") && int.TryParse(tag[9..], out var nr) ? nr : null;
        if (tag != _hoverTag)
        {
            _hoverTag = tag;
            var (name, hinweis) = tag is not null && _innen is BetriebsAnsicht a ? InnenText(a, tag) : ("", "");
            _raumSchildName.Text = name;
            _raumSchildHinweis.Text = hinweis;
            _raumSchildHinweis.Visible = hinweis != "";
            _raumSchild.Visible = name != "";
            _raumSchild.ResetSize();
        }
        if (_raumSchild.Visible)
        {
            var fenster = GetViewport().GetVisibleRect().Size;
            var ort = GetViewport().GetMousePosition() + new Vector2(18, 22);
            _raumSchild.Position = new Vector2(Mathf.Min(ort.X, fenster.X - _raumSchild.Size.X - 8), Mathf.Min(ort.Y, fenster.Y - _raumSchild.Size.Y - 8));
        }
    }

    /// <summary>Die Klickfläche unter dieser Bildschirmstelle: ihr Tag („bedienung:Personal:Buch", „tuer:Werk:Lager", „arbeiter:2" …) oder null.</summary>
    private string? InnenTagBei(Vector2 bildschirm)
    {
        var von = _raumKamera.ProjectRayOrigin(bildschirm);
        var bis = von + _raumKamera.ProjectRayNormal(bildschirm) * 60;
        var treffer = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(von, bis));
        return treffer.Count > 0 && treffer["collider"].As<Node>() is { } koerper && koerper.HasMeta("innen")
            ? koerper.GetMeta("innen").AsString() : null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Eingabe
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Klick im Modellraum: Bedienung öffnet ihre Tafel, Tür wechselt den Raum, Arbeiter wird gewählt, leere Fläche hebt die Wahl auf.</summary>
    private void RaumKlick(Vector2 stelle, bool dazu)
    {
        var teile = (InnenTagBei(stelle) ?? "").Split(':');
        switch (teile[0])
        {
            case "bedienung" when teile.Length >= 3:
                BedienungAngeklickt?.Invoke(teile[1], teile[2]);
                break;
            case "einrichtung" when teile.Length >= 2:
                BedienungAngeklickt?.Invoke("Einrichten", teile[1]);
                break;
            case "ware" when teile.Length >= 2:
                BedienungAngeklickt?.Invoke("Lager", teile[1]);
                break;
            case "tuer" when teile.Length >= 3:
                RaumWaehlen("raum:" + teile[2].ToLowerInvariant());
                break;
            case "arbeiter" when teile.Length >= 2 && int.TryParse(teile[1], out var nr):
                if (!dazu) _gewaehlt.Clear();
                if (!(dazu && _gewaehlt.Remove(nr))) _gewaehlt.Add(nr);
                ArbeiterAuswahlGeaendert?.Invoke();
                break;
            default:
                if (dazu || _gewaehlt.Count == 0) break;
                _gewaehlt.Clear();
                ArbeiterAuswahlGeaendert?.Invoke();
                break;
        }
    }

    /// <summary>Wählt alle sichtbaren Arbeiter, deren Kopf im Rahmen liegt (Umschalt oder Strg: zur Auswahl dazu).</summary>
    private void RahmenAuswaehlen(Rect2 rahmen, bool dazu)
    {
        if (_innen is not BetriebsAnsicht { Modellraum: { } md }) return;
        var neu = dazu ? new HashSet<int>(_gewaehlt) : new HashSet<int>();
        foreach (var arbeiter in md.Arbeiter.Values)
        {
            if (!arbeiter.Halter.Visible) continue;
            var punkt = arbeiter.Halter.GlobalPosition + Vector3.Up * 1.0f;
            if (!_raumKamera.IsPositionBehind(punkt) && rahmen.HasPoint(_raumKamera.UnprojectPosition(punkt))) neu.Add(arbeiter.Nummer);
        }
        if (neu.SetEquals(_gewaehlt)) return;
        _gewaehlt.Clear();
        _gewaehlt.UnionWith(neu);
        ArbeiterAuswahlGeaendert?.Invoke();
    }

    /// <summary>Wählt genau diese Arbeiter (Startoption --arbeiter).</summary>
    public void ArbeiterWaehlen(IEnumerable<int> nummern)
    {
        _gewaehlt.Clear();
        _gewaehlt.UnionWith(nummern);
        ArbeiterAuswahlGeaendert?.Invoke();
    }

    /// <summary>Eingabe im Modellraum (linke Taste, Rahmen, rechte Taste). Gibt false zurück, wenn sie nichts davon ist.</summary>
    private bool ModellRaumEingabe(InputEvent eingabe)
    {
        switch (eingabe)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } druck:
                _druckPunkt = druck.Position;
                _rahmenZieht = false;
                return true;
            case InputEventMouseMotion bewegung when _druckPunkt is not null && !bewegung.ButtonMask.HasFlag(MouseButtonMask.Left):
                _druckPunkt = null; // die Taste wurde anderswo losgelassen (über einer Tafel): kein Rahmen mehr
                _rahmenZieht = false;
                _raumRahmen.Rahmen = null;
                return false;
            case InputEventMouseMotion bewegung when _druckPunkt is { } start:
                if (_rahmenZieht || start.DistanceTo(bewegung.Position) > 8)
                {
                    _rahmenZieht = true;
                    _raumRahmen.Rahmen = new Rect2(start, Vector2.Zero).Expand(bewegung.Position);
                }
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } los when _druckPunkt is { } start:
                var dazu = los.ShiftPressed || los.CtrlPressed;
                _druckPunkt = null;
                if (_rahmenZieht) RahmenAuswaehlen(new Rect2(start, Vector2.Zero).Expand(los.Position), dazu);
                else RaumKlick(los.Position, dazu);
                _rahmenZieht = false;
                _raumRahmen.Rahmen = null;
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
                InnenansichtVerlassen();
                return true;
        }
        return false;
    }
}
