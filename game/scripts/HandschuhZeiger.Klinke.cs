using Godot;

namespace Reichsgilde;

// Türschloss und Klinke-Folge des 3D-Handschuhs (Teil 3c). Zustandslogik: KlinkeFolge.cs (ohne Godot, mit Tests);
// Ablauf der Blende: Uebergangsblende.cs (Haken KlinkeStarter / KlinkeLoslasser); Modell: tuerschloss_zeiger.glb (Teil 3a).
public partial class HandschuhZeiger
{
    private const string SchlossPfad = "res://assets/modelle/zeiger/tuerschloss_zeiger.glb";

    /// <summary>Lage des Schlosses im Handraum (Teil 3a, glTF-Koordinaten): 9 cm unter dem Handrücken-Raum, wie die Kamera der Bühne um 40 Grad um die Hochachse gedreht.</summary>
    private static readonly Vector3 SchlossLage = new(0.0419f, -0.090f, -0.0585f);
    private const float SchlossDrehungGrad = -40f;


    /// <summary>Für den Test (<c>--klinketest=1</c>): tut so, als schwebe die Maus über einer Tür.</summary>
    public static bool TestTuer { get; set; }

    /// <summary>Log der Klinke-Folge (Test und Messung).</summary>
    public static bool KlinkeLog { get; set; }

    private readonly KlinkeFolge _klinke = new();
    private Node3D? _schloss, _schlossAnker;
    private AnimationPlayer? _schlossSpieler;
    private double _schlossGewicht;
    private readonly SchlossEinwuchs _einwuchs = new();
    private Node3D? _klinkeKnoten;
    private double _uebergangDauer = SchlossEinwuchs.UebergangSekunden;
    private bool _schlossBereit;
    private ulong _klinkeAnfang;

    private void KlinkeAnmelden()
    {
        Uebergangsblende.KlinkeStarter = KlinkeStarten;
        Uebergangsblende.KlinkeLoslasser = KlinkeLoslassen;
        KlinkeLog |= OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--klinketest="));
    }

    public override void _ExitTree()
    {
        if (Uebergangsblende.KlinkeStarter == KlinkeStarten) Uebergangsblende.KlinkeStarter = null;
        if (Uebergangsblende.KlinkeLoslasser == KlinkeLoslassen) Uebergangsblende.KlinkeLoslasser = null;
    }

    /// <summary>Stellt das Türschloss neben die Hand (gleiche Bühne, gleiche Neigung); fehlt die Datei oder die Hand die Klinke-Clips, bleibt es bei Zeigen.</summary>
    private void SchlossLaden()
    {
        if (_neigung is null || _spieler is null || !_spieler.HasAnimation(KlinkeFolge.ClipBereit)) return;
        var szene = ResourceLoader.Exists(SchlossPfad) ? GD.Load<PackedScene>(SchlossPfad) : null;
        if (szene?.Instantiate() is not Node3D wurzel)
        {
            GD.PushWarning($"[handschuh] Türschloss fehlt oder nicht ladbar: {SchlossPfad}; die Haltung Bereit entfällt");
            return;
        }
        wurzel.Position = SchlossLage;
        wurzel.RotationDegrees = new Vector3(0, SchlossDrehungGrad, 0);
        wurzel.Visible = false;
        _neigung.AddChild(wurzel);
        MaterialienEinrichten(wurzel);
        _schloss = wurzel;
        _schlossAnker = wurzel.FindChild("Anker_Schloss", true, false) as Node3D;
        _schlossSpieler = wurzel.FindChildren("*", "AnimationPlayer", true, false).FirstOrDefault() as AnimationPlayer;
        if (_schlossSpieler is not null)
            foreach (var name in _schlossSpieler.GetAnimationList()) _schlossSpieler.GetAnimation(name).LoopMode = Animation.LoopModeEnum.None;
        // Ruhelage der Klinke: oben (Drehung 0). Die Datei trägt im Knoten die Endlage von Druecken (-28 Grad); ohne Korrektur stünde die Klinke beim Einblenden schon unten.
        _klinkeKnoten = wurzel.FindChild("Klinke", true, false) as Node3D;
        var dateiDrehung = _klinkeKnoten is null ? 0.0 : _klinkeKnoten.RotationDegrees.Y;
        KlinkeRuhelage();
        GD.Print($"[handschuh] Türschloss: Klinke in der Datei bei {dateiDrehung:0.0} Grad, gestellt auf {(_klinkeKnoten?.RotationDegrees.Y ?? 0):0.0} Grad (Ruhelage oben)");
        _schlossBereit = _schlossAnker is not null;
        GD.Print($"[handschuh] Türschloss geladen: Anker_Schloss {(_schlossAnker is null ? "FEHLT" : "da")}, Druecken {SchlossLaenge("Druecken"):0.000} s, Klinke_druecken {Laenge("Klinke_druecken"):0.000} s");
    }

    /// <summary>Stellt die Klinke in die Ruhelage (oben, Drehung 0): die Schloss-Aktionen laufen erst mit dem Drücken und Loslassen der Hand.</summary>
    private void KlinkeRuhelage()
    {
        if (_klinkeKnoten is not null && !_klinkeKnoten.Quaternion.IsEqualApprox(Quaternion.Identity)) _klinkeKnoten.Quaternion = Quaternion.Identity;
    }

    private double SchlossLaenge(string aktion) => _schlossSpieler?.HasAnimation(aktion) == true ? _schlossSpieler.GetAnimation(aktion).Length : 0;

    /// <summary>
    /// Schloss einwachsen und Zeigerpunkt gleiten: beide laufen mit <see cref="SchlossEinwuchs"/> (Länge des Übergangsclips der Hand, Smoothstep), das Schloss wächst um <c>Anker_Schloss</c> von 0 auf 1.
    /// Das Ziel setzt <see cref="SchlossAufUebergang"/> im selben Bild, in dem der Übergangsclip der Hand beginnt; in Bereit und in der Klinke-Folge bleibt es 1.
    /// </summary>
    private void SchlossAktualisieren(double dt)
    {
        if (_schloss is null) return;
        if (_klinke.Laeuft) _einwuchs.ZielSetzen(1);
        _einwuchs.Schritt(dt, _uebergangDauer);
        _schlossGewicht = _einwuchs.S;
        if (_klinke.KlinkeInRuhelage && _schlossSpieler?.IsPlaying() != true) KlinkeRuhelage(); // Bereit, Übergang, Greifen: die Klinke steht oben
        _schloss.Visible = _schlossGewicht > 0.001;
        var s = (float)_schlossGewicht;
        _schloss.Scale = Vector3.One * Math.Max(s, 0.001f);
        // Wachsen um die Schlossmitte: die Wurzel (Mitte der Plattenunterseite) rückt so, dass Anker_Schloss an seinem Platz bleibt
        var mitte = _schlossAnker is null ? Vector3.Zero : _schlossAnker.Position;
        var drehung = new Basis(Vector3.Up, Mathf.DegToRad(SchlossDrehungGrad));
        _schloss.Position = SchlossLage + drehung * mitte * (1f - s);
    }

    /// <summary>Beginnt der Übergangsclip <paramref name="clip"/> der Hand? Dann startet in diesem Bild das Einwachsen (nach Bereit) oder Ausblenden (von Bereit weg) des Schlosses mit der Länge dieses Clips.</summary>
    private void SchlossAufUebergang(string clip)
    {
        if (SchlossEinwuchs.ZielFuerClip(clip) is not { } ziel) return;
        _einwuchs.ZielSetzen(ziel);
        var laenge = Laenge(clip);
        _uebergangDauer = laenge > 0 ? laenge : SchlossEinwuchs.UebergangSekunden;
        if (KlinkeLog) GD.Print($"[klinke] Übergang {clip}: Hand {laenge:0.0000} s, Schloss und Zeigerpunkt Ziel {ziel:0} mit {_uebergangDauer:0.0000} s Smoothstep");
    }

    /// <summary>Die Klinke-Folge führt die Hand: je Stufe der Clip (Greifen, Drücken, Loslassen) samt Schloss-Aktion; nach dem Loslassen steht die Hand wieder in Bereit.</summary>
    private void KlinkeHaltung()
    {
        var vorher = _klinke.Phase;
        if (!_klinke.Schritt(_letztesDt)) return;
        if (_klinke.Phase == KlinkePhase.Aus)
        {
            _ablauf.PoseSetzen(HandPose.Bereit); // Klinke_loslassen endet im ersten Bild von Klinke_bereit
            _spielt = "";
        }
        else KlinkeSpielen();
        if (KlinkeLog) GD.Print($"[klinke] {vorher} -> {_klinke.Phase} nach {(Time.GetTicksUsec() - _klinkeAnfang) / 1000} ms, Schloss-Klinke {_klinkeKnoten?.RotationDegrees.Y ?? 0:0.0} Grad, Schloss-Aktion {_schlossSpieler?.CurrentAnimation}");
    }

    private void KlinkeSpielen()
    {
        if (_spieler is null) return;
        if (_klinke.Clip is { } clip && _spieler.HasAnimation(clip))
        {
            _spieler.Play(clip, HandschuhModell.BlendEinmal, 1f); // Tempo 1: die Längen stimmen mit denen des Schlosses überein
            _spielt = clip;
        }
        if (_klinke.SchlossAktion is { } aktion && _schlossSpieler?.HasAnimation(aktion) == true) _schlossSpieler.Play(aktion);
    }

    /// <summary>Haken der Blende beim Betreten: steht die Hand in Bereit, beginnt die Folge, und die Blende erfährt, wie lange sie warten soll; sonst null (keine Klinke).</summary>
    private double? KlinkeStarten()
    {
        if (!_an || _spieler is null || _schloss is null || _ablauf.Pose != HandPose.Bereit || _ablauf.EinmalLaeuft) return null;
        var dauer = _klinke.Starten(true, Laenge(KlinkeFolge.ClipGreifen), Laenge(KlinkeFolge.ClipDruecken), Laenge(KlinkeFolge.ClipLoslassen));
        if (dauer is null) return null;
        _klinkeAnfang = Time.GetTicksUsec();
        KlinkeSpielen();
        if (KlinkeLog) GD.Print($"[klinke] Aus -> Greifen; Blende nach {dauer:0.000} s (Greifen {Laenge(KlinkeFolge.ClipGreifen):0.000}, Drücken {Laenge(KlinkeFolge.ClipDruecken):0.000}, Loslassen {Laenge(KlinkeFolge.ClipLoslassen):0.000} s)");
        return dauer;
    }

    /// <summary>Haken der Blende: der Raum ist offen oder das Betreten abgebrochen, die Hand lässt los.</summary>
    private void KlinkeLoslassen()
    {
        if (!_klinke.Loslassen()) return;
        KlinkeSpielen();
        if (KlinkeLog) GD.Print($"[klinke] -> Loslassen nach {(Time.GetTicksUsec() - _klinkeAnfang) / 1000} ms");
    }

    /// <summary>
    /// Rechenvorschau (kein Bild): Lage von Langschild und Klinke im Bild gegen die Hand in Bereit, in Anzeigepixeln bei der aktuellen Zeigergröße, und wie viel davon nicht von der Hand verdeckt ist.
    /// Die Hand ist als Kapseln um die Knochen genähert (Mittelhand dick, Finger dünn); die Hand liegt dem Betrachter näher als das Schloss, was sie überdeckt, zählt als verdeckt.
    /// </summary>
    private void SchlossVorschau()
    {
        if (_schloss is null || _skelett is null) return;
        var skala = _flaeche.Size.X / _bild.Size.X;
        var pixelJeMeter = _flaeche.Size.X / (float)Sichtfeld;
        Vector2 Bildpunkt(Vector3 welt) => _kamera.UnprojectPosition(welt) * skala;
        var kapseln = new List<(Vector2 A, Vector2 B, float R)>();
        for (var i = 0; i < _skelett.GetBoneCount(); i++)
        {
            var eltern = _skelett.GetBoneParent(i);
            if (eltern < 0) continue;
            var name = _skelett.GetBoneName(i);
            var dick = name.StartsWith("metacarpal") || name.StartsWith("wrist") || name.StartsWith("palm");
            kapseln.Add((Bildpunkt(_skelett.GlobalTransform * _skelett.GetBoneGlobalPose(eltern).Origin), Bildpunkt(_skelett.GlobalTransform * _skelett.GetBoneGlobalPose(i).Origin), (dick ? 0.030f : 0.012f) * pixelJeMeter));
        }
        bool Verdeckt(Vector2 q) => kapseln.Any(k =>
        {
            var ab = k.B - k.A;
            var t = ab.LengthSquared() < 1e-6f ? 0f : Mathf.Clamp((q - k.A).Dot(ab) / ab.LengthSquared(), 0f, 1f);
            return q.DistanceTo(k.A + ab * t) <= k.R;
        });
        var platte = new List<Vector2>();
        var verdeckt = 0;
        for (var x = 0; x < 12; x++)
            for (var z = 0; z < 24; z++)
            {
                var punkt = Bildpunkt(_schloss.GlobalTransform * new Vector3(-0.028f + 0.056f * x / 11f, 0.005f, -0.08f + 0.16f * z / 23f));
                platte.Add(punkt);
                if (Verdeckt(punkt)) verdeckt++;
            }
        var klinke = new List<Vector2>();
        var klinkeVerdeckt = 0;
        for (var i = 0; i < 24; i++) // grob: gerade Linie von der Welle bis zum Ende der Klinke (S-Form, 11,4 cm), Achse 4,5 cm über der Platte
        {
            var punkt = Bildpunkt(_schloss.GlobalTransform * new Vector3(0.114f * i / 23f, 0.045f, -0.032f));
            klinke.Add(punkt);
            if (Verdeckt(punkt)) klinkeVerdeckt++;
        }
        Vector2 Min(IEnumerable<Vector2> l) => new(l.Min(p => p.X), l.Min(p => p.Y));
        Vector2 Max(IEnumerable<Vector2> l) => new(l.Max(p => p.X), l.Max(p => p.Y));
        var hand = kapseln.SelectMany(k => new[] { k.A, k.B }).ToList();
        GD.Print($"[klinketest] Vorschau (Anzeigepixel, Zeigergröße {_groesse}, Bild {_flaeche.Size.X:0}): Langschild x {Min(platte).X:0}..{Max(platte).X:0}, y {Min(platte).Y:0}..{Max(platte).Y:0}; Klinke bis x {Max(klinke).X:0}, y {klinke[^1].Y:0}; Hand x {Min(hand).X:0}..{Max(hand).X:0}, y {Min(hand).Y:0}..{Max(hand).Y:0}");
        GD.Print($"[klinketest] Vorschau: Langschild sichtbar {100 - 100 * verdeckt / platte.Count} %, Klinke sichtbar {100 - 100 * klinkeVerdeckt / klinke.Count} % (Näherung)");
    }

    /// <summary><c>--klinketest=1</c>: Hover über einer Tür vortäuschen, auf die Haltung Bereit warten, die Folge spielen, nach 1,5 s loslassen; alles ins Log (<c>[klinketest]</c>).</summary>
    public async void KlinkeTestStarten()
    {
        TestTuer = true;
        KlinkeLog = true;
        var anfang = Time.GetTicksMsec();
        for (var i = 0; i < 900 && _ablauf.Pose != HandPose.Bereit; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"[klinketest] Haltung {_ablauf.Pose} nach {Time.GetTicksMsec() - anfang} ms, Schloss {(_schloss is null ? "fehlt" : "da")}, Schloss sichtbar {_schloss?.Visible}");
        for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"[klinketest] Schloss nach Einwuchs: Größe {_schlossGewicht:0.000}, Zeigerpunkt-Gewicht {_schlossGewicht:0.000}, Dauer Übergang {_uebergangDauer:0.0000} s (Hand = Schloss = Zeigerpunkt)");
        GD.Print($"[klinketest] Schloss-Klinke vor der Folge: Drehung {_klinkeKnoten?.RotationDegrees.Y ?? 0:0.0} Grad (Soll 0 = oben), Schloss-Aktion läuft: {_schlossSpieler?.IsPlaying()}");
        SchlossVorschau();
        var vorlauf = KlinkeStarten();
        GD.Print($"[klinketest] Start der Folge: Vorlauf {(vorlauf is null ? "keiner (Hand nicht in Bereit)" : $"{vorlauf:0.000} s")}");
        var stand = Time.GetTicksMsec();
        while (_klinke.Phase != KlinkePhase.Gehalten && _klinke.Laeuft && Time.GetTicksMsec() - stand < 8000) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"[klinketest] Stufe {_klinke.Phase} nach {Time.GetTicksMsec() - stand} ms");
        var halten = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - halten < 1500) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        KlinkeLoslassen();
        var los = Time.GetTicksMsec();
        while (_klinke.Laeuft && Time.GetTicksMsec() - los < 8000) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"[klinketest] Loslassen nach {Time.GetTicksMsec() - los} ms, Haltung danach {_ablauf.Pose}, Spieler spielt {_spieler?.CurrentAnimation}");
        TestTuer = false;
        GD.Print("[klinketest] Ende");
    }
}
