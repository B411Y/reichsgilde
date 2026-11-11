using Godot;

namespace Reichsgilde;

/// <summary>
/// Der 3D-Handschuh als Mauszeiger: eine eigene kleine 3D-Bühne (<see cref="SubViewport"/> mit eigener Welt, eigenem Licht, Umriss-Hülle und durchsichtigem Grund),
/// die als Bild auf einer hohen <see cref="CanvasLayer"/>-Ebene über allem liegt und der Maus folgt. Das Modell ist <c>assets/modelle/zeiger/handschuh_zeiger.glb</c>
/// (Pixal3D-Hand mit Gerüst, siehe <c>tools/modelle/handschuh/</c>); Clip-Namen und Längen werden aus der Datei gelesen.
/// Haltungen: Ruhe (Schleife), Zeigen (über Anklickbarem), Greifen (Ziehen mit gedrückter Taste), dazu Tippen beim Klick; dazwischen laufen die Übergangsclips
/// (Ablauf: <see cref="HandschuhAblauf"/>). Die Neigung folgt der Mausgeschwindigkeit mit Trägheit.
/// <b>Zeigerpunkt:</b> die Spitze des Zeigefingers in der Haltung Zeigen (Knoten <c>Anker_Spitze</c> der .glb) wird jedes Bild mit dem Gerüst und der Neigung durch die Kamera projiziert und das Bild so gelegt,
/// dass sie genau auf der Mausposition liegt. Das Fragezeichen (<c>Hilfe_Marke</c>) erscheint nur, wenn <see cref="Mauszeiger"/> einen echten Tooltip meldet.
/// <b>Eingabe:</b> nichts hier fängt Mausereignisse ab (<c>MouseFilter Ignore</c>, <see cref="SubViewport"/> ohne Eingabe, kein <c>_Input</c>); die Klickweitergabe
/// (<c>Stadtansicht.MausUeberOberflaeche</c>) bleibt unberührt. Aus: Einstellung „3D-Handschuh“ (<see cref="Fenster.Handschuh3dAn"/>), <c>--handschuh=0</c>, fehlende Datei
/// oder Rückfall bei zu wenig Bildern je Sekunde (<see cref="HandschuhLeistung"/>); dann zeichnet <see cref="Mauszeiger"/> wie bisher den 2D-Zeiger.
/// </summary>
public partial class HandschuhZeiger : CanvasLayer
{
    private const string ModellPfad = "res://assets/modelle/zeiger/handschuh_zeiger.glb";

    /// <summary>Sichtfeld der Kamera in Metern (die Hand ist etwa 0,26 m lang): bei 48 Pixel Zeigergröße etwa 41 Pixel Handlänge.</summary>
    private const double Sichtfeld = 0.45;

    /// <summary>Seitenlänge des Bildes in Zeigergrößen (Platz für Neigung und Marke).</summary>
    private const double Bildgroesse = 1.5;

    /// <summary>Gerendert wird mit dieser Auflösung mal der Anzeigegröße und beim Zeigen verkleinert (glatte Linien bei 48 Pixel).</summary>
    private const int Superabtastung = 2;

    private const double TippenTempo = 1.5;
    private const double MaxNeigungGrad = 11.0;

    private static readonly HandschuhLeistung Leistung = new();
    private static bool _ausgefallen;
    private static bool? _vorhanden;

    /// <summary>Datei da und nicht beim Laden gescheitert.</summary>
    public static bool Verfuegbar => !_ausgefallen && (_vorhanden ??= ResourceLoader.Exists(ModellPfad));

    /// <summary>Soll der 3D-Handschuh jetzt gezeichnet werden (Einstellung an, Datei da, kein Rückfall, Größe ab 48 Pixel)?</summary>
    public static bool Wirksam(int groesse) =>
        Fenster.Handschuh3dAn && Verfuegbar && !Leistung.Rueckfall && groesse >= HandschuhModell.MindestGroesse;

    private bool _an, _hilfe, _anklickbar;
    private int _groesse;
    private bool _geladen;
    private SubViewport _bild = null!;
    private TextureRect _flaeche = null!;
    private Camera3D _kamera = null!;
    private Node3D _neigung = null!;
    private Node3D? _modell, _anker, _marke, _markeUmriss;
    private Skeleton3D? _skelett;
    private int _knochen = -1;
    private Transform3D _spitzeLokal; // Anker_Spitze in der Haltung Zeigen, im Raum des Skeletts
    private AnimationPlayer? _spieler;
    private readonly HandschuhAblauf _ablauf = new();
    private string _spielt = "";
    private ulong _zuletzt;
    private Vector2 _letzteMaus, _druckStart;
    private bool _gedrueckt, _ziehen, _hatMaus;
    private double _vx, _vy, _nickWinkel, _rollWinkel;

    public HandschuhZeiger()
    {
        Layer = 128; // über Tooltips (110) und Ladebildschirm (120)
        Name = "Handschuh3D";
        ProcessMode = ProcessModeEnum.Always;
        ProcessPriority = 1000; // nach den Animationen der Hand dieses Knotens und nach Mauszeiger.Anwenden
    }

    /// <summary>Von <see cref="Mauszeiger"/> jedes Bild: ob der 3D-Handschuh gilt, Zeigergröße, ob über Anklickbarem und ob ein Tooltip da ist (Fragezeichen).</summary>
    public void Setze(bool an, int groesse, bool anklickbar, bool hilfe)
    {
        _an = an;
        _groesse = groesse;
        _anklickbar = anklickbar;
        _hilfe = hilfe;
    }

    public override void _Ready()
    {
        _bild = new SubViewport
        {
            OwnWorld3D = true, TransparentBg = true, Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            HandleInputLocally = false, Size = new Vector2I(64, 64), AudioListenerEnable3D = false, PhysicsObjectPicking = false,
        };
        _bild.GuiDisableInput = true;
        AddChild(_bild);
        _flaeche = new TextureRect
        {
            Texture = _bild.GetTexture(), MouseFilter = Control.MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = CanvasItem.TextureFilterEnum.Linear, Visible = false, FocusMode = Control.FocusModeEnum.None,
        };
        AddChild(_flaeche);
        Buehne();
        _zuletzt = Time.GetTicksUsec();
    }

    /// <summary>Kamera (schräg von oben, Finger nach links oben wie bei einem Zeiger), zwei Lichter und Umgebungslicht; das Modell kommt erst beim ersten Bedarf.</summary>
    private void Buehne()
    {
        // Wie die Bildansicht „zeiger“ des Entwurfs: Blick von oben (glTF +Y, die Finger zeigen nach -Z), Blickpunkt etwa Handmitte, 16 Grad Neigung nach vorn,
        // Bild um 40 Grad gedreht, sodass der Zeigefinger schräg nach links oben zeigt (nachgerechnet: Richtung auf dem Bild etwa -0,7/-0,7).
        // LookAtFromPosition arbeitet mit Weltkoordinaten, darum wird die Drehung selbst eingerechnet und die Kamera hängt direkt an der Bühne.
        var dreh = new Basis(Vector3.Up, Mathf.DegToRad(-40f));
        var mitte = new Vector3(0, 0, -0.045f);
        _kamera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = (float)Sichtfeld, Near = 0.05f, Far = 3f, Current = true };
        _bild.AddChild(_kamera);
        const float tilt = 16f * Mathf.Pi / 180f, abstand = 0.8f;
        _kamera.LookAtFromPosition(mitte + dreh * new Vector3(0, abstand * Mathf.Cos(tilt), abstand * Mathf.Sin(tilt)), mitte, dreh * new Vector3(0, 0, -1));
        // Lichter hängen an der Kamera: Haupt von links oben vorn (warm), Auf von rechts (kühl). Ohne Schatten.
        _kamera.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-42, 32, 0), LightColor = new Color(1.0f, 0.93f, 0.82f), LightEnergy = 1.25f, ShadowEnabled = false });
        _kamera.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-18, -52, 0), LightColor = new Color(0.82f, 0.88f, 1.0f), LightEnergy = 0.55f, ShadowEnabled = false });
        _kamera.Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor, AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.62f, 0.60f, 0.58f), AmbientLightEnergy = 0.7f, TonemapMode = Godot.Environment.ToneMapper.Linear,
        };
        _neigung = new Node3D();
        _bild.AddChild(_neigung);
    }

    private bool ModellLaden()
    {
        if (_geladen) return _modell is not null;
        _geladen = true;
        var szene = ResourceLoader.Exists(ModellPfad) ? GD.Load<PackedScene>(ModellPfad) : null;
        if (szene is null || szene.Instantiate() is not Node3D wurzel)
        {
            GD.PushWarning($"[handschuh] Modell fehlt oder nicht ladbar: {ModellPfad}; es bleibt beim 2D-Zeiger");
            _ausgefallen = true;
            return false;
        }
        _modell = wurzel;
        _neigung.AddChild(wurzel);
        _skelett = wurzel.FindChildren("*", "Skeleton3D", true, false).FirstOrDefault() as Skeleton3D;
        _spieler = wurzel.FindChildren("*", "AnimationPlayer", true, false).FirstOrDefault() as AnimationPlayer;
        _anker = wurzel.FindChild("Anker_Spitze", true, false) as Node3D;
        _marke = wurzel.FindChild("Hilfe_Marke", true, false) as Node3D;
        _markeUmriss = wurzel.FindChild("Hilfe_Marke_Umriss", true, false) as Node3D;
        if (_skelett is null || _spieler is null || _anker?.GetParent() is not BoneAttachment3D halter || (_knochen = _skelett.FindBone(halter.BoneName)) < 0)
        {
            GD.PushWarning("[handschuh] Modell unvollständig (Gerüst, Animationen oder Anker_Spitze fehlt); es bleibt beim 2D-Zeiger");
            _ausgefallen = true;
            wurzel.QueueFree();
            _modell = null;
            return false;
        }
        MaterialienEinrichten(wurzel);
        // Zeigerpunkt: die Spitze des Zeigefingers in der Haltung Zeigen, im Raum des Skeletts festgehalten. So bleibt die Hand beim Wechsel der Haltung (Faust: Spitze eingerollt)
        // ruhig unter der Maus, statt zu springen; in Ruhe und beim Tippen weicht die echte Spitze um wenige Pixel ab.
        if (_spieler.HasAnimation("Zeigen")) { _spieler.Play("Zeigen"); _spieler.Seek(0, true); _spieler.Advance(0); }
        _spitzeLokal = _skelett.GetBoneGlobalPose(_knochen) * _anker.Transform;
        _spieler.Stop();
        // Schleifen: Linear; Längen und Namen stehen in der Datei (HandschuhModell.Schleife / Uebergang), hier nichts Festes.
        foreach (var name in _spieler.GetAnimationList())
        {
            var clip = _spieler.GetAnimation(name);
            clip.LoopMode = name == "Ruhe" || name == "Zeigen" || name == "Greifen" ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        }
        _spieler.AnimationFinished += _ => _ablauf.EinmalFertig();
        GD.Print($"[handschuh] Modell geladen: {_spieler.GetAnimationList().Length} Clips, {_skelett.GetBoneCount()} Knochen, Ruhe {Laenge("Ruhe"):0.00} s, Ruhe->Greifen {Laenge("Uebergang_Ruhe_Greifen"):0.00} s");
        return true;
    }

    private double Laenge(string clip) => _spieler?.HasAnimation(clip) == true ? _spieler.GetAnimation(clip).Length : 0;

    /// <summary>
    /// Leder: Eckenfarbe (COLOR_0 = Faltenfarbe, in der .glb mit <c>glb_faltenfarbe.py</c> an die erste Stelle getauscht) als Albedo einschalten.
    /// Umriss-Hüllen: schwarz, ohne Licht; ihre Flächen sind in der Datei nach innen gedreht (invertierte Hülle), darum Rückseiten ausblenden (Back, wie importiert).
    /// </summary>
    private static void MaterialienEinrichten(Node wurzel)
    {
        foreach (var knoten in wurzel.FindChildren("*", "MeshInstance3D", true, false))
        {
            var netz = (MeshInstance3D)knoten;
            netz.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            var umriss = netz.Name.ToString().Contains("Umriss");
            for (var flaeche = 0; flaeche < (netz.Mesh?.GetSurfaceCount() ?? 0); flaeche++)
            {
                if (netz.Mesh!.SurfaceGetMaterial(flaeche) is not StandardMaterial3D stoff) continue;
                if (umriss)
                {
                    stoff.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    stoff.CullMode = BaseMaterial3D.CullModeEnum.Back;
                }
                else if (netz.Name == "Handschuh") stoff.VertexColorUseAsAlbedo = true;
            }
        }
    }

    public override void _Process(double delta)
    {
        var jetzt = Time.GetTicksUsec();
        var dt = Math.Min((jetzt - _zuletzt) / 1e6, 0.25);
        var echteDt = (jetzt - _zuletzt) / 1e6;
        _zuletzt = jetzt;
        var maus = GetViewport().GetMousePosition();
        var sichtbar = _an && Input.MouseMode == Input.MouseModeEnum.Visible && GetViewport().GetVisibleRect().HasPoint(maus);
        Leistung.Takt(echteDt, _an && sichtbar);
        if (!_an || !ModellLaden())
        {
            _flaeche.Visible = false;
            _bild.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            _hatMaus = false;
            _gedrueckt = false;
            return;
        }
        if (!sichtbar)
        {
            _flaeche.Visible = false;
            _bild.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            _hatMaus = false;
            return;
        }
        Groesse();
        Eingabe(maus);
        Geschwindigkeit(maus, dt);
        Haltung();
        Neigung(dt);
        if (_marke is not null) _marke.Visible = _hilfe;
        if (_markeUmriss is not null) _markeUmriss.Visible = _hilfe;
        _bild.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        Platzieren(maus);
        _flaeche.Visible = true;
    }

    /// <summary>Bildgröße: Zeigergröße (Fensterpixel) mal 1,5, in Zeichenflächeneinheiten umgerechnet (die Oberfläche kann skaliert sein).</summary>
    private void Groesse()
    {
        var faktor = Math.Max(GetWindow().GetFinalTransform().Scale.X, 0.1f);
        var seite = (float)(_groesse * Bildgroesse / faktor);
        _flaeche.Size = new Vector2(seite, seite);
        var aufloesung = Math.Max(32, (int)Math.Round(_groesse * Bildgroesse * Superabtastung));
        if (_bild.Size.X != aufloesung) _bild.Size = new Vector2I(aufloesung, aufloesung);
    }

    /// <summary>Gedrückte Taste und Ziehen aus der Abfrage (nichts wird verbraucht): linke Taste startet das Tippen, jede Taste mit Weg über der Schwelle ist Greifen.</summary>
    private void Eingabe(Vector2 maus)
    {
        var links = Input.IsMouseButtonPressed(MouseButton.Left);
        var gedrueckt = links || Input.IsMouseButtonPressed(MouseButton.Right) || Input.IsMouseButtonPressed(MouseButton.Middle);
        if (gedrueckt && !_gedrueckt)
        {
            _druckStart = maus;
            _ziehen = false;
            if (links) _ablauf.TippenMerken();
        }
        if (gedrueckt && !_ziehen && maus.DistanceTo(_druckStart) > HandschuhModell.ZiehSchwelle) _ziehen = true;
        if (!gedrueckt) _ziehen = false;
        _gedrueckt = gedrueckt;
    }

    private void Geschwindigkeit(Vector2 maus, double dt)
    {
        if (_hatMaus && dt > 1e-4)
        {
            var v = (maus - _letzteMaus) / (float)dt;
            _vx = HandschuhModell.Gleiten(_vx, v.X, dt, 0.06);
            _vy = HandschuhModell.Gleiten(_vy, v.Y, dt, 0.06);
        }
        else (_vx, _vy) = (0, 0);
        _letzteMaus = maus;
        _hatMaus = true;
    }

    private void Haltung()
    {
        var spieler = _spieler!;
        if (!_ablauf.EinmalLaeuft)
        {
            var clip = _ablauf.Entscheiden(HandschuhModell.Wunsch(_ziehen, _anklickbar));
            if (clip is not null)
            {
                if (spieler.HasAnimation(clip))
                {
                    spieler.Play(clip, 0.06, clip == "Tippen" ? (float)TippenTempo : 1f);
                    _spielt = clip;
                }
                else _ablauf.EinmalFertig(); // Clip fehlt in der Datei: Haltung trotzdem wechseln, statt zu hängen
            }
            var schleife = HandschuhModell.Schleife(_ablauf.Pose);
            if (!_ablauf.EinmalLaeuft && _spielt != schleife && spieler.HasAnimation(schleife))
            {
                spieler.Play(schleife, 0.10);
                _spielt = schleife;
            }
        }
    }

    /// <summary>Neigung aus der Mausgeschwindigkeit: die Hand hinkt nach (Rolle um die Blickachse, Nicken um die Bildquerachse), gleitet weich zurück.</summary>
    private void Neigung(double dt)
    {
        var rolle = -HandschuhModell.NeigungsStaerke(_vx) * MaxNeigungGrad;
        var nick = HandschuhModell.NeigungsStaerke(_vy) * MaxNeigungGrad;
        _rollWinkel = HandschuhModell.Gleiten(_rollWinkel, rolle, dt, 0.09);
        _nickWinkel = HandschuhModell.Gleiten(_nickWinkel, nick, dt, 0.09);
        var achsen = _kamera.GlobalTransform.Basis;
        _neigung.Basis = new Basis(achsen.Z.Normalized(), Mathf.DegToRad((float)_rollWinkel)) * new Basis(achsen.X.Normalized(), Mathf.DegToRad((float)_nickWinkel));
    }

    /// <summary>Zeigerpunkt: Spitze des Zeigefingers (Anker_Spitze) durch die Kamera auf Bildpunkte gelegt und auf die Mausposition geschoben.</summary>
    private void Platzieren(Vector2 maus)
    {
        var spitze = _skelett!.GlobalTransform * _spitzeLokal;
        var imBild = _kamera.UnprojectPosition(spitze.Origin) * (_flaeche.Size.X / _bild.Size.X);
        var (x, y) = HandschuhModell.BildEcke(maus.X, maus.Y, imBild.X, imBild.Y);
        _flaeche.Position = new Vector2((float)x, (float)y);
    }
}
