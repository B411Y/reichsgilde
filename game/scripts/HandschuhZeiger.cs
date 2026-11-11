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
/// (<c>Stadtansicht.MausUeberOberflaeche</c>) bleibt unberührt. Aus: Einstellung „3D-Handschuh“ (<see cref="Fenster.Handschuh3dAn"/>), <c>--handschuh=0</c>, oder fehlende Datei
/// (<see cref="HandschuhModell.Gilt"/>); die Bildrate schaltet nie ab, der Spieler entscheidet allein. Dann zeichnet <see cref="Mauszeiger"/> wie bisher den 2D-Zeiger.
/// </summary>
public partial class HandschuhZeiger : CanvasLayer
{
    private const string ModellPfad = "res://assets/modelle/zeiger/handschuh_zeiger.glb";

    /// <summary>Sichtfeld der Kamera in Metern (die Hand ist etwa 0,26 m lang): bei 48 Pixel Zeigergröße und Bildgröße 2,0 etwa 55 Pixel Handlänge (das 2D-Bild ist an der Hand etwa 44 × 40 Pixel groß, schräg gemessen etwa 55).</summary>
    private const double Sichtfeld = 0.45;

    /// <summary>Seitenlänge des Bildes in Zeigergrößen (Platz für Neigung und Marke). Bei 2,0 ist die Hand etwa 1,35-mal so groß wie vorher (1,5), also etwa so groß wie das 2D-Bild.</summary>
    private const double Bildgroesse = 2.0;

    /// <summary>Gerendert wird mit dieser Auflösung mal der Anzeigegröße und beim Zeigen verkleinert (glatte Linien bei 48 Pixel).</summary>
    private const int Superabtastung = 2;

    /// <summary>Das Fragezeichen (<c>Hilfe_Marke</c>) wird um seine Mitte so viel größer (vorher 1,0) und ein Stück von der Hand weg gerückt (glTF-Meter), damit es auch bei 48 Pixel lesbar ist.</summary>
    private const float MarkeFaktor = 1.9f;
    private static readonly Vector3 MarkeVersatz = new(-0.008f, 0f, 0.018f);

    private const double MaxNeigungGrad = 11.0;

    /// <summary>Blickpunkt der Kamera in der Bühne (etwa Handmitte); die Drehung der Haltung Text geht um die Hochachse durch diesen Punkt.</summary>
    private static readonly Vector3 BuehnenMitte = new(0, 0, -0.045f);

    private static bool _ausgefallen;
    private static bool? _vorhanden;

    /// <summary>Datei da und nicht beim Laden gescheitert.</summary>
    public static bool Verfuegbar => !_ausgefallen && (_vorhanden ??= ResourceLoader.Exists(ModellPfad));

    /// <summary>Soll der 3D-Handschuh jetzt gezeichnet werden (Einstellung an, Datei da, kein Rückfall, Größe ab 48 Pixel)?</summary>
    public static bool Wirksam(int groesse) => HandschuhModell.Gilt(Fenster.Handschuh3dAn, Verfuegbar, groesse);

    private bool _an, _hilfe, _anklickbar, _text, _rahmen, _textEingabe;
    private double _letztesDt;
    private Vector2 _imBildLetzte; // Zeigerpunkt im Bild (Zeichenflächeneinheiten des Hauptfensters), für die Spiegel in Popups
    private readonly RandomNumberGenerator _zufall = new(); // nur für die Darstellung (Zucken, Winken); der Kern hat seine eigene Zufallsquelle
    private readonly BereitZucken _zucken;
    private readonly WinkenLogik _winken;
    private bool _eingabeBild, _winkenAktiv, _winkenClip;
    private double _zuckZeit, _winkZeit, _winkNominal;

    /// <summary><c>--winktest=1</c>: Winken nach 3 statt 60 Sekunden, Wiederholung nach 4 bis 6 statt 120 bis 180 Sekunden, Log <c>[winken]</c>.</summary>
    public static bool WinkTest { get; } = OS.GetCmdlineUserArgs().Contains("--winktest=1");

    /// <summary>Log der Zustände von Zucken und Winken.</summary>
    public static bool LebenLog { get; set; } = WinkTest || OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--klinketest="));
    private bool _tuer;
    private double _textGewicht; // 0 = Zeigerpunkt ist die Fingerspitze, 1 = Anker_Text (Haltung Text)
    private Transform3D? _textLokal; // Anker_Text in der Haltung Text, im Raum des Skeletts
    private Node3D? _ankerText;
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
        _zufall.Randomize();
        _zucken = new BereitZucken(() => _zufall.Randf());
        _winken = WinkTest ? new WinkenLogik(() => _zufall.Randf(), 3.0, 4.0, 6.0) : new WinkenLogik(() => _zufall.Randf());
        Layer = 128; // über Tooltips (110) und Ladebildschirm (120)
        Name = "Handschuh3D";
        ProcessMode = ProcessModeEnum.Always;
        ProcessPriority = 1000; // nach den Animationen der Hand dieses Knotens und nach Mauszeiger.Anwenden
    }

    /// <summary>Von <see cref="Mauszeiger"/> jedes Bild: ob der 3D-Handschuh gilt, Zeigergröße, ob über Anklickbarem und ob ein Tooltip da ist (Fragezeichen).</summary>
    public void Setze(bool an, int groesse, bool anklickbar, bool hilfe, bool text = false, bool tuer = false, bool rahmen = false, bool textEingabe = false)
    {
        _an = an;
        _groesse = groesse;
        _anklickbar = anklickbar;
        _hilfe = hilfe;
        _text = text;
        _rahmen = rahmen; // Auswahlrechteck in der Welt: Zeigen statt Ziehen-Faust
        _textEingabe = textEingabe; // ein Textfeld hat den Fokus: kein Winken
        _tuer = tuer || TestTuer; // Tür: betretbares Gebäude unter der Maus (Haltung Bereit, Schloss darunter; HandschuhZeiger.Klinke.cs)
    }

    /// <summary>Jede Eingabe (Maus, Taste, Rad, Berührung) setzt die Ruhezeit für das Winken zurück; nichts wird verbraucht.</summary>
    public override void _Input(InputEvent eingabe)
    {
        if (eingabe is InputEventMouseMotion or InputEventMouseButton or InputEventKey or InputEventScreenTouch or InputEventScreenDrag or InputEventJoypadButton) _eingabeBild = true;
    }

    public override void _Ready()
    {
        KlinkeAnmelden();
        SpiegelAnmelden(); // Handschuh auch über Popups und Unterfenstern (HandschuhZeiger.Spiegel.cs)
        BuehneEinrichten();
    }

    private void BuehneEinrichten()
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
        var dreh = new Basis(Vector3.Up, Mathf.DegToRad((float)HandschuhModell.KameraDrehungGrad));
        var mitte = BuehnenMitte;
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
        SchlossLaden(); // das Türschloss mit Klinke in derselben Bühne (HandschuhZeiger.Klinke.cs)
        _winkenClip = _spieler.HasAnimation("Winken");
        GD.Print($"[handschuh] Leben: Bereit_atmen {(_spieler.HasAnimation("Bereit_atmen") ? "da" : "fehlt")}, Bereit_zucken {(_spieler.HasAnimation("Bereit_zucken") ? "da" : "fehlt")}, Winken {(_winkenClip ? "da" : "fehlt")}");
        foreach (var marke in new[] { _marke, _markeUmriss })
            if (marke is not null)
            {
                marke.Scale *= MarkeFaktor;
                marke.Position += MarkeVersatz;
            }
        // Zeigerpunkt: die Spitze des Zeigefingers in der Haltung Zeigen, im Raum des Skeletts festgehalten. So bleibt die Hand beim Wechsel der Haltung (Faust: Spitze eingerollt)
        // ruhig unter der Maus, statt zu springen; in Ruhe und beim Tippen weicht die echte Spitze um wenige Pixel ab.
        if (_spieler.HasAnimation("Zeigen")) { _spieler.Play("Zeigen"); _spieler.Seek(0, true); _spieler.Advance(0); }
        _spitzeLokal = _skelett.GetBoneGlobalPose(_knochen) * _anker.Transform;
        // Haltung Text: Anker_Text (Teil 2) auf dieselbe Art festhalten; fehlt er oder der Clip, bleibt die Hand beim Zeigen.
        _ankerText = wurzel.FindChild("Anker_Text", true, false) as Node3D;
        if (_ankerText?.GetParent() is BoneAttachment3D textHalter && _spieler.HasAnimation("Text") && _skelett.FindBone(textHalter.BoneName) is var textKnochen and >= 0)
        {
            _spieler.Play("Text"); _spieler.Seek(0, true); _spieler.Advance(0);
            _textLokal = _skelett.GetBoneGlobalPose(textKnochen) * _ankerText.Transform;
            TextAufrichten(textKnochen); // Drehung für aufrechte Hand aus der Pose berechnen (HandschuhZeiger.Text.cs)
        }
        _spieler.Stop();
        // Schleifen: Linear; Längen und Namen stehen in der Datei (HandschuhModell.Schleife / Uebergang), hier nichts Festes.
        foreach (var name in _spieler.GetAnimationList())
        {
            var clip = _spieler.GetAnimation(name);
            clip.LoopMode = name is "Ruhe" or "Zeigen" or "Greifen" or "Text" or "Klinke_bereit" or "Bereit_atmen" ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
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
        _zuletzt = jetzt;
        _letztesDt = dt;
        var maus = GetViewport().GetMousePosition();
        var sichtbar = _an && Input.MouseMode == Input.MouseModeEnum.Visible && GetViewport().GetVisibleRect().HasPoint(maus);
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
        SchlossAktualisieren(dt);
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

    /// <summary>Welche Schleife zur Haltung gehört: in Bereit atmet die Hand (<c>Bereit_atmen</c>), sonst wie bisher <c>Klinke_bereit</c> bzw. der Haltungsname.</summary>
    private string SchleifenName(HandPose pose) => pose == HandPose.Bereit && _spieler!.HasAnimation("Bereit_atmen") ? "Bereit_atmen" : HandschuhModell.Schleife(pose);

    /// <summary>Tempo eines Einmalclips: die Übergänge von und nach Bereit und Winken sind schon auf ihre Zeit gebaut (7 Bilder bei 24/s) und laufen mit 1, die älteren mit <see cref="HandschuhModell.UebergangTempo"/>.</summary>
    private static float UebergangsTempo(string clip) =>
        clip == "Tippen" ? (float)HandschuhModell.TippenTempo : clip.Contains("Bereit") || clip.Contains("Winken") ? 1f : (float)HandschuhModell.UebergangTempo;

    private void Haltung()
    {
        var spieler = _spieler!;
        if (_ziehen) _ablauf.TippenVerwerfen(); // aus dem Klick wurde ein Ziehen (Auswahlrechteck, Greifen): kein Tippen mehr
        if (_klinke.Laeuft) { _zucken.Schritt(_letztesDt, false, true); KlinkeHaltung(); return; } // die Klinke-Folge führt die Hand (HandschuhZeiger.Klinke.cs)
        var wunsch = HandschuhModell.Wunsch(_ziehen, _anklickbar, _text && _textLokal is not null, _tuer && _schlossBereit, _rahmen, _winkenAktiv && _winkenClip && !_ziehen);
        if (!_ablauf.EinmalLaeuft)
        {
            var clip = _ablauf.Entscheiden(wunsch);
            if (clip is not null && !spieler.HasAnimation(clip) && clip.StartsWith("Uebergang_") && spieler.HasAnimation(clip["Uebergang_".Length..])) clip = clip["Uebergang_".Length..]; // kürzere Namen (Ruhe_Text)
            if (clip is not null) SchlossAufUebergang(clip);
            if (clip is not null)
            {
                if (spieler.HasAnimation(clip))
                {
                    spieler.Play(clip, HandschuhModell.BlendEinmal, UebergangsTempo(clip));
                    _spielt = clip;
                }
                else _ablauf.EinmalFertig(); // Clip fehlt in der Datei: Haltung trotzdem wechseln, statt zu hängen
            }
            var schleife = SchleifenName(_ablauf.Pose);
            if (!_ablauf.EinmalLaeuft && _spielt != schleife && !_zucken.Zuckt && spieler.HasAnimation(schleife))
            {
                spieler.Play(schleife, _ablauf.Pose == HandPose.Winken ? HandschuhModell.BlendEinmal : HandschuhModell.BlendSchleife);
                _spielt = schleife;
                if (_ablauf.Pose == HandPose.Winken) _winkZeit = 0; // der Clip Winken beginnt jetzt
            }
        }
        LebenAktualisieren(wunsch);
    }

    /// <summary>Das Leben der Hand: Zucken in Bereit (alle 3 bis 6 s) und das Winken nach langer Ruhe (Zustandslogik: <see cref="BereitZucken"/>, <see cref="WinkenLogik"/>).</summary>
    private void LebenAktualisieren(HandPose wunsch)
    {
        var spieler = _spieler!;
        var dt = _letztesDt;
        // Zucken: nur in Bereit ohne Übergang und Klinke; ein Klick (Taste) unterbricht sofort, dann läuft die Schleife weich weiter
        var inBereit = _ablauf.Pose == HandPose.Bereit && !_ablauf.EinmalLaeuft && wunsch == HandPose.Bereit;
        if (_zucken.Zuckt)
        {
            _zuckZeit += dt;
            if (_zuckZeit >= Laenge("Bereit_zucken")) { _zucken.Beendet(); _spielt = ""; } // der Clip ist durch: die Schleife übernimmt
        }
        if (_zucken.Schritt(dt, inBereit, _gedrueckt) && spieler.HasAnimation("Bereit_zucken"))
        {
            spieler.Play("Bereit_zucken", HandschuhModell.BlendEinmal);
            _spielt = "Bereit_zucken";
            _zuckZeit = 0;
            if (LebenLog) GD.Print("[leben] Zucken beginnt");
        }
        else if (!_zucken.Zuckt && _spielt == "Bereit_zucken") _spielt = ""; // unterbrochen: die Schleife beginnt weich im nächsten Bild
        // Winken: Eingaben dieses Bildes (Maus, Taste, Rad) setzen zurück
        var sperre = _text || _textEingabe || _klinke.Laeuft || Uebergangsblende.Laeuft;
        var ereignis = _winken.Schritt(dt, _eingabeBild, sperre);
        _eingabeBild = false;
        if (ereignis == WinkenEreignis.Start) { _winkenAktiv = true; _winkZeit = 0; _winkNominal = 0; if (LebenLog) GD.Print($"[winken] Start nach Leerlauf (Clip {(_winkenClip ? "da" : "fehlt")})"); }
        else if (ereignis == WinkenEreignis.Abbruch) { _winkenAktiv = false; if (LebenLog) GD.Print("[winken] Abbruch (Eingabe oder Sperre)"); }
        if (_winkenAktiv)
        {
            _winkNominal += dt;
            var laenge = _winkenClip ? Laenge("Winken") : 2.5; // ohne Clip (Test) zählt eine Nominalzeit
            var winkt = !_winkenClip || (_ablauf.Pose == HandPose.Winken && !_ablauf.EinmalLaeuft);
            if (winkt) _winkZeit += dt;
            if (_winkZeit >= laenge || _winkNominal > laenge + 3.0)
            {
                _winkenAktiv = false;
                _winken.Beendet();
                if (LebenLog) GD.Print($"[winken] Ende nach {_winkZeit:0.00} s, Hand geht zurück in die vorige Haltung (Wunsch {HandschuhModell.Wunsch(_ziehen, _anklickbar, _text, _tuer, _rahmen, false)})");
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
        var neigung = new Basis(achsen.Z.Normalized(), Mathf.DegToRad((float)_rollWinkel)) * new Basis(achsen.X.Normalized(), Mathf.DegToRad((float)_nickWinkel));
        // Haltung Text: die Hand steht aufrecht (wie in der Ansicht des Blender-Vergleichs): sie dreht mit der Kamera (-40 Grad um die Hochachse), um die Blickmitte der Bühne
        var haltung = new Basis(Vector3.Up, Mathf.DegToRad((float)HandschuhModell.PoseDrehungGrad(_textGewicht, _textDrehung)));
        _neigung.Basis = neigung * haltung;
        _neigung.Position = neigung * (BuehnenMitte - haltung * BuehnenMitte);
    }

    /// <summary>Zeigerpunkt: Spitze des Zeigefingers (Anker_Spitze) durch die Kamera auf Bildpunkte gelegt und auf die Mausposition geschoben.</summary>
    private void Platzieren(Vector2 maus)
    {
        // Zeigerpunkt: Fingerspitze, in der Haltung Text (und im Übergang dorthin) gleitend der Textpunkt Anker_Text
        var ziel = _textLokal is not null && _text && !_ziehen ? 1.0 : 0.0;
        _textGewicht = HandschuhModell.Gleiten(_textGewicht, ziel, _letztesDt, HandschuhModell.TextGewichtZeit);
        var skala = _flaeche.Size.X / _bild.Size.X;
        var spitze = _skelett!.GlobalTransform * _spitzeLokal;
        var imBild = _kamera.UnprojectPosition(spitze.Origin) * skala;
        if (_textLokal is { } textPunkt && _textGewicht > 0.001)
        {
            var textBild = _kamera.UnprojectPosition((_skelett.GlobalTransform * textPunkt).Origin) * skala;
            imBild = imBild.Lerp(textBild, (float)_textGewicht);
        }
        if (_schlossAnker is not null && _schlossGewicht > 0.001) // über der Tür und in der Klinke-Folge: Zeigerpunkt ist die Mitte des Schlosses, weich überblendet
            imBild = imBild.Lerp(_kamera.UnprojectPosition(_schlossAnker.GlobalPosition) * skala, (float)_schlossGewicht);
        var (x, y) = HandschuhModell.BildEcke(maus.X, maus.Y, imBild.X, imBild.Y);
        _flaeche.Position = new Vector2((float)x, (float)y);
        _imBildLetzte = imBild;
    }
}
