using Godot;

namespace Reichsgilde;

/// <summary>
/// Die 3D-Bühne des großen Briefs im Brief-Fenster (<see cref="BriefAnzeige"/>): ein SubViewport (Szene <c>res://scenes/brief_buehne.tscn</c>: eigene Welt,
/// durchsichtiger Grund, Kamera von vorn ohne Fluchtpunkt, zwei Lichter) mit dem Modell <c>assets/modelle/oberflaeche/brief_gross.glb</c>
/// (Clips <c>auffalten</c>, <c>zufalten</c>; Knoten <c>Siegel</c>, <c>Siegel_Symbol</c>, <c>Inhaltsflaeche</c>; Knochen <c>Streck</c>, <c>Anker_oben</c>,
/// <c>Anker_unten</c>). Der Ablauf steht ohne Godot in <see cref="BriefAblauf"/>; Aussehen und Maße kommen aus der Szene und aus <c>meldung_thema.tres</c>
/// (<see cref="MeldungStil"/>, <c>brief_gross_…</c>), hier steht keine Farbe und keine Größe.
/// Zwei kleine 2D-Bilder gehören dazu: das <b>Abbild</b> des Briefinhalts (dieselben Steuerelemente wie die echte Tafel, noch einmal gebaut), das während
/// des Auf- und Zufaltens auf der Inhaltsfläche liegt, und das eingedrückte Zeichen des Siegels.
/// Die Tafel gibt die Größe vor: Ihre Breite ist die Breite der Inhaltsfläche (0,26 m), ihre Höhe streckt das Blatt nach der Streckregel des Modells
/// (0,42 bis 0,60 m). Gerechnet wird nur, solange sich der Brief bewegt, und ein paar Bilder nach einer Änderung.
/// </summary>
public sealed partial class BriefBuehne : Node
{
    private const string SzenePfad = "res://scenes/brief_buehne.tscn", ModellPfad = "res://assets/modelle/oberflaeche/brief_gross.glb";

    private SubViewport? _bild, _abbild, _zeichen;
    private Camera3D? _kamera;
    private AnimationPlayer? _spieler;
    private Skeleton3D? _skelett;
    private int _streck = -1, _ankerOben = -1, _ankerUnten = -1;
    private Vector3 _ruheOben, _ruheUnten;
    private MeshInstance3D? _inhalt;
    private StandardMaterial3D? _siegelStoff;
    private Praegebild? _praegung;
    private string _clip = "";
    private double _hoehe = BriefAblauf.HoeheMin;
    private Vector2 _tafel;
    private float _faktor;
    private int _nachlauf, _abbildNachlauf;

    /// <summary>Steht die Bühne (Szene und Modell geladen)? Sonst zeigt das Brief-Fenster ein Blatt in 2D, ohne Falten.</summary>
    public bool Bereit => _bild is not null && _spieler is not null;

    /// <summary>Das Bild des Briefs.</summary>
    public Texture2D? Bild => _bild?.GetTexture();

    /// <summary>Hierhin baut das Brief-Fenster seinen Inhalt ein zweites Mal – das Bild auf der Inhaltsfläche. So groß wie die Tafel, nimmt keine Eingaben an.</summary>
    public MarginContainer? Abbild { get; private set; }

    /// <summary>Die Zeiten für <see cref="BriefAblauf"/>: Cliplängen aus der .glb, der Rest aus dem Thema.</summary>
    public BriefMasse Masse
    {
        get
        {
            double Laenge(string clip) => _spieler?.HasAnimation(clip) == true ? _spieler.GetAnimation(clip).Length : 0.9;
            var m = MeldungStil.Mass;
            return new BriefMasse(Laenge("auffalten"), Laenge("zufalten"), m("brief_gross_inhalt_ab_ms") / 1000.0, m("brief_gross_inhalt_bis_ms") / 1000.0,
                                  m("brief_gross_einblenden_ms") / 1000.0, m("brief_gross_ausblenden_ms") / 1000.0);
        }
    }

    public override void _Ready()
    {
        if (!ResourceLoader.Exists(SzenePfad) || !ResourceLoader.Exists(ModellPfad))
        {
            GD.PushWarning("[brief] Szene oder Modell fehlt – das Brief-Fenster zeigt ein Blatt in 2D.");
            return;
        }
        var bild = GD.Load<PackedScene>(SzenePfad)?.InstantiateOrNull<SubViewport>();
        var wurzel = GD.Load<PackedScene>(ModellPfad)?.InstantiateOrNull<Node3D>();
        _kamera = bild?.GetNodeOrNull<Camera3D>("Kamera");
        var platz = bild?.GetNodeOrNull<Node3D>("Brief");
        var spieler = wurzel?.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
        _skelett = wurzel?.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();
        if (bild is null || wurzel is null || _kamera is null || platz is null || spieler is null || _skelett is null)
        {
            GD.PushWarning("[brief] Szene oder Modell unvollständig (Kamera, Brief, AnimationPlayer, Gerüst) – das Brief-Fenster zeigt ein Blatt in 2D.");
            bild?.QueueFree();
            wurzel?.QueueFree();
            return;
        }
        bild.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        AddChild(bild);
        platz.AddChild(wurzel);
        HandschuhZeiger.MaterialienEinrichten(wurzel); // Umriss: invertierte Hülle, schwarz ohne Licht, wie beim Handschuh
        // Die Netze sind in der gefalteten Rast-Pose vermessen: feste Hülle, damit offen und gestreckt nichts weggeschnitten wird (Vorgabe des Modells)
        foreach (var netz in wurzel.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            if (netz.Skin is not null) netz.CustomAabb = new Aabb(new Vector3(-0.3f, -0.45f, -0.2f), new Vector3(0.6f, 0.9f, 0.4f));
        foreach (var name in new[] { "auffalten", "zufalten" })
            if (spieler.HasAnimation(name)) spieler.GetAnimation(name).LoopMode = Animation.LoopModeEnum.None;
        spieler.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual; // die Zeit gibt BriefAblauf vor
        _streck = _skelett.FindBone("Streck");
        _ankerOben = _skelett.FindBone("Anker_oben");
        _ankerUnten = _skelett.FindBone("Anker_unten");
        if (_ankerOben >= 0) _ruheOben = _skelett.GetBoneRest(_ankerOben).Origin;
        if (_ankerUnten >= 0) _ruheUnten = _skelett.GetBoneRest(_ankerUnten).Origin;

        // Das Abbild des Inhalts: ein 2D-Bild mit durchsichtigem Grund, als Textur auf der Inhaltsfläche (ohne Licht, damit die Tinte wie auf der Tafel aussieht)
        _abbild = new SubViewport
        {
            Disable3D = true, TransparentBg = true, HandleInputLocally = false, GuiDisableInput = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            Size = new Vector2I(64, 64),
        };
        Abbild = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _abbild.AddChild(Abbild);
        AddChild(_abbild);
        _inhalt = wurzel.FindChild("Inhaltsflaeche", true, false) as MeshInstance3D;
        if (_inhalt is not null)
        {
            _inhalt.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
            {
                AlbedoTexture = _abbild.GetTexture(), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
                Uv1Scale = new Vector3(1, -1, 1), Uv1Offset = new Vector3(0, 1, 0), // das Bild eines SubViewports steht auf diesen Flächen sonst kopf (v läuft im Modell nach oben)
                // Ohne Tiefenprüfung: Die Falze federn noch um ein, zwei Grad nach und stünden sonst vor dem Bild (der Fuß des Briefs fehlte kurz)
                NoDepthTest = true, RenderPriority = 1,
            });
            _inhalt.Visible = false;
        }

        // Das Siegel: Wachs in der Farbe der Meldungsart, das Zeichen eingedrückt (ein kleines 2D-Bild als Textur der Symbolfläche)
        _zeichen = new SubViewport
        {
            Disable3D = true, TransparentBg = true, HandleInputLocally = false, GuiDisableInput = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            Size = new Vector2I(128, 128),
        };
        _praegung = new Praegebild { Size = new Vector2(128, 128), MouseFilter = Control.MouseFilterEnum.Ignore };
        _zeichen.AddChild(_praegung);
        AddChild(_zeichen);
        if (wurzel.FindChild("Siegel", true, false) is MeshInstance3D siegel)
        {
            _siegelStoff = siegel.GetActiveMaterial(0)?.Duplicate() as StandardMaterial3D ?? new StandardMaterial3D();
            siegel.SetSurfaceOverrideMaterial(0, _siegelStoff);
        }
        if (wurzel.FindChild("Siegel_Symbol", true, false) is MeshInstance3D symbol)
            symbol.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
            {
                AlbedoTexture = _zeichen.GetTexture(), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
                Roughness = _siegelStoff?.Roughness ?? 0.42f, Uv1Scale = new Vector3(1, -1, 1), Uv1Offset = new Vector3(0, 1, 0),
            });
        _bild = bild;
        _spieler = spieler;
        GD.Print($"[brief] Großer Brief gebaut: Clips {string.Join(", ", spieler.GetAnimationList())}, Inhaltsfläche {(_inhalt is null ? "fehlt" : "da")}, " +
                 $"Siegel {(_siegelStoff is null ? "fehlt" : "da")}, Streckknochen {(_streck >= 0 && _ankerOben >= 0 && _ankerUnten >= 0 ? "da" : "fehlen")}");
    }

    /// <summary>Farbe und Zeichen der Meldungsart auf das Siegel legen.</summary>
    public void SiegelSetzen(string thema, Color farbe)
    {
        if (!Bereit) return;
        if (_siegelStoff is not null) _siegelStoff.AlbedoColor = farbe;
        if (_praegung is null || _zeichen is null) return;
        _praegung.Thema = thema;
        _praegung.Farbe = farbe.Darkened(MeldungStil.Mass("brief_praegung_prozent") / 100f);
        _praegung.QueueRedraw();
        _zeichen.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        _nachlauf = 4;
    }

    /// <summary>Der Inhalt des Abbilds wurde neu gebaut: ein paar Bilder lang neu zeichnen (die Anordnung braucht ein, zwei Bilder).</summary>
    public void AbbildGeaendert()
    {
        _abbildNachlauf = 4;
        _nachlauf = 5;
    }

    /// <summary>Die Blatthöhe in Metern zur Größe der Tafel (Oberflächen-Pixel): Die Breite der Tafel ist die Breite der Inhaltsfläche.</summary>
    public static double Blatthoehe(Vector2 tafel) => BriefAblauf.Blatthoehe(tafel.Y / PixelJeMeter(tafel));

    private static double PixelJeMeter(Vector2 tafel) => Math.Max(1, tafel.X) / BriefAblauf.InhaltBreite;

    /// <summary>Die Höhe der Tafel (Oberflächen-Pixel bei dieser Breite), bei der das Blatt seine kleinste bzw. größte Höhe hat.</summary>
    public static (float Kleinste, float Groesste) Tafelhoehen(float breite)
    {
        var k = breite / BriefAblauf.InhaltBreite;
        return ((float)((BriefAblauf.HoeheMin - 2 * BriefAblauf.Rand) * k), (float)((BriefAblauf.HoeheMax - 2 * BriefAblauf.Rand) * k));
    }

    /// <summary>Wie weit das Blatt (mit Umriss) über die Tafel hinausragt, in Oberflächen-Pixeln bei dieser Tafelbreite: der Rand von 2 cm.</summary>
    public static float Blattrand(float breite) => (float)(BriefAblauf.Rand * breite / BriefAblauf.InhaltBreite);

    /// <summary>
    /// Stellt die Bühne auf die Größe der Tafel ein (Oberflächen-Pixel; <paramref name="faktor"/> = echte Pixel je Oberflächen-Pixel): Blatthöhe, Sichtfeld,
    /// Bildgröße. Gibt das Rechteck zurück, in das das Bild gezeichnet wird – bezogen auf die linke obere Ecke der Tafel, sodass die Inhaltsfläche des
    /// Blatts genau unter der Tafel liegt.
    /// </summary>
    public Rect2 Einstellen(Vector2 tafel, float faktor)
    {
        if (_bild is null || _kamera is null || _abbild is null || Abbild is null) return new Rect2(Vector2.Zero, tafel);
        var k = PixelJeMeter(tafel);
        var hoehe = Blatthoehe(tafel);
        var rand = MeldungStil.Mass("brief_gross_rand_mm") / 1000.0;
        var sicht = new Vector2((float)(BriefAblauf.BlattBreite + 2 * rand), (float)(hoehe + 2 * rand));
        if (!tafel.IsEqualApprox(_tafel) || !Mathf.IsEqualApprox(faktor, _faktor) || Math.Abs(hoehe - _hoehe) > 1e-6)
        {
            _tafel = tafel;
            _faktor = faktor;
            _hoehe = hoehe;
            var fein = faktor * MeldungStil.Mass("brief_gross_abtastung_prozent") / 100f;
            _bild.Size = new Vector2I(Math.Clamp((int)Math.Round(sicht.X * k * fein), 16, 4096), Math.Clamp((int)Math.Round(sicht.Y * k * fein), 16, 4096));
            _kamera.Size = sicht.Y; // ohne Fluchtpunkt: Size ist die sichtbare Höhe in Metern
            _abbild.Size = new Vector2I(Math.Clamp((int)Math.Round(tafel.X * faktor), 16, 4096), Math.Clamp((int)Math.Round(tafel.Y * faktor), 16, 4096));
            Abbild.Position = Vector2.Zero;
            Abbild.Size = tafel;
            Abbild.Scale = new Vector2(faktor, faktor);
            AbbildGeaendert();
        }
        var groesse = sicht * (float)k;
        return new Rect2(tafel / 2 - groesse / 2, groesse);
    }

    /// <summary>
    /// Überträgt den Stand des Ablaufs auf das Modell – jedes Bild, solange das Brief-Fenster zu sehen ist: Stelle im Clip, danach die Streckung (die drei
    /// Streckknochen haben keine Clipspur), Inhaltsfläche an oder aus. Schaltet das Rechnen der Bilder nur ein, solange sich etwas ändert.
    /// </summary>
    public void Weiter(BriefAblauf a)
    {
        if (_bild is null || _spieler is null || _skelett is null || _abbild is null) return;
        if (_spieler.HasAnimation(a.Clip))
        {
            if (_clip != a.Clip || !_spieler.IsPlaying())
            {
                _spieler.Play(a.Clip);
                _clip = a.Clip;
            }
            _spieler.Seek(Math.Min(a.Clipzeit, _spieler.GetAnimation(a.Clip).Length - 0.001), true);
            _spieler.Advance(0); // im Modus Manual übernimmt erst das die Haltung sofort
        }
        var (skala, versatz, inhaltSkala) = BriefAblauf.Streckung(_hoehe);
        if (_streck >= 0) _skelett.SetBonePoseScale(_streck, new Vector3(1, (float)skala, 1));
        if (_ankerOben >= 0) _skelett.SetBonePosePosition(_ankerOben, _ruheOben + new Vector3(0, (float)versatz, 0));
        if (_ankerUnten >= 0) _skelett.SetBonePosePosition(_ankerUnten, _ruheUnten - new Vector3(0, (float)versatz, 0));
        if (_inhalt is not null)
        {
            _inhalt.Scale = new Vector3(1, (float)inhaltSkala, 1);
            if (_inhalt.Visible != a.BildSichtbar)
            {
                _inhalt.Visible = a.BildSichtbar;
                _nachlauf = 3;
            }
        }
        var laeuft = a.Zustand is BriefZustand.Auffalten or BriefZustand.Zufalten;
        var rechnen = a.Sichtbar && (laeuft || _nachlauf > 0);
        if (_nachlauf > 0) _nachlauf--;
        Modus(_bild, rechnen);
        Modus(_abbild, a.Sichtbar && _abbildNachlauf > 0);
        if (_abbildNachlauf > 0) _abbildNachlauf--;
    }

    /// <summary>Das Brief-Fenster ist zu: nichts mehr rechnen.</summary>
    public void Ruhen()
    {
        if (_bild is not null) Modus(_bild, false);
        if (_abbild is not null) Modus(_abbild, false);
    }

    private static void Modus(SubViewport bild, bool rechnen)
    {
        var modus = rechnen ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        if (bild.RenderTargetUpdateMode != modus) bild.RenderTargetUpdateMode = modus;
    }

    /// <summary>Das eingedrückte Zeichen des Siegels (<see cref="Siegelzeichner.Praegung"/>).</summary>
    private sealed partial class Praegebild : Control
    {
        public string Thema = "stadt";
        public Color Farbe = Colors.Black;

        public override void _Draw() => Siegelzeichner.Praegung(this, new Rect2(Vector2.Zero, Size), Farbe, Thema);
    }
}
