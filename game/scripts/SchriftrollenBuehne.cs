using Godot;

namespace Reichsgilde;

/// <summary>
/// Die gemeinsame kleine 3D-Bühne für die Schriftrollen der Meldungsleiste: <b>ein</b> SubViewport (Szene <c>res://scenes/schriftrolle_buehne.tscn</c>:
/// eigene Welt, durchsichtiger Grund, Kamera von vorn ohne Fluchtpunkt, zwei Lichter) mit einer Reihe von Plätzen. Jede Meldung der Leiste
/// (<see cref="Siegel"/>) nimmt sich einen Platz und zeichnet dessen Ausschnitt aus dem einen Bild. Das Modell ist
/// <c>assets/modelle/oberflaeche/schriftrolle.glb</c> (Clips <c>aufrollen</c>, <c>zurollen</c>; Knoten <c>Symbolflaeche</c>), der Ablauf je Rolle steht ohne Godot
/// in <see cref="SchriftrolleAblauf"/>. Aussehen und Maße kommen aus der Szene und aus <c>meldung_thema.tres</c> (<see cref="MeldungStil"/>), hier steht
/// keine Farbe und keine Größe. Die Symbole aller Meldungsarten liegen auf einem zweiten, kleinen 2D-Bild (ein Blatt mit Feldern), das nur neu
/// gezeichnet wird, wenn ein Symbol dazukommt oder sich die Größe ändert.
/// Kosten: gerechnet wird nur, solange eine Rolle da und die Leiste sichtbar ist; ohne Bewegung nur nach einer Änderung.
/// </summary>
public sealed partial class SchriftrollenBuehne : Node
{
    private const string SzenePfad = "res://scenes/schriftrolle_buehne.tscn", ModellPfad = "res://assets/modelle/oberflaeche/schriftrolle.glb";
    private const int BlattSeite = 4; // Felder je Seite des Symbolblatts (16 Symbole; es gibt sieben Themen)

    /// <summary>Eine Rolle auf ihrem Platz der Bühne.</summary>
    public sealed class Rolle
    {
        internal int Platz;
        internal Node3D Wurzel = null!;
        internal AnimationPlayer Spieler = null!;
        internal MeshInstance3D? Symbol;
        internal StandardMaterial3D? SymbolStoff;
        internal string Clip = "";
        internal bool Belegt;
        public SchriftrolleAblauf Ablauf { get; internal set; } = null!;
    }

    private SubViewport? _bild;
    private Camera3D? _kamera;
    private Node3D? _rollen;
    private PackedScene? _modell;
    private Rolle?[] _plaetze = Array.Empty<Rolle?>();
    private SubViewport? _blatt;
    private Symbolblatt? _zeichner;
    private readonly List<(string Thema, Color Farbe)> _symbole = new();
    private int _zellePixel;
    private float _sicht = 1f;
    private int _nachlauf;
    private bool _warSichtbar;

    /// <summary>Ist die Leiste gerade zu sehen? Sonst ruht die Bühne (hinter großen Tafeln, im Menü).</summary>
    public Func<bool>? Sichtbar;

    /// <summary>Steht die Bühne (Szene und Modell geladen)? Sonst zeichnen die Siegel wie früher in 2D.</summary>
    public bool Bereit => _bild is not null && _modell is not null;

    /// <summary>Das eine Bild aller Rollen.</summary>
    public Texture2D? Bild => _bild?.GetTexture();

    /// <summary>Seitenlänge eines Platzes in Oberflächen-Pixeln (bei 1600 × 900).</summary>
    public float Zelle => MeldungStil.Mass("rolle_zelle");

    public override void _Ready()
    {
        if (!ResourceLoader.Exists(SzenePfad) || !ResourceLoader.Exists(ModellPfad))
        {
            GD.PushWarning("[schriftrolle] Szene oder Modell fehlt – die Meldungsleiste zeigt Siegel in 2D.");
            return;
        }
        _bild = GD.Load<PackedScene>(SzenePfad)?.InstantiateOrNull<SubViewport>();
        _modell = GD.Load<PackedScene>(ModellPfad);
        _kamera = _bild?.GetNodeOrNull<Camera3D>("Kamera");
        _rollen = _bild?.GetNodeOrNull<Node3D>("Rollen");
        if (_bild is null || _modell is null || _kamera is null || _rollen is null)
        {
            GD.PushWarning("[schriftrolle] Szene unvollständig (Kamera, Rollen) – die Meldungsleiste zeigt Siegel in 2D.");
            _bild?.QueueFree();
            _bild = null;
            return;
        }
        _plaetze = new Rolle?[Math.Clamp(MeldungStil.Mass("rolle_plaetze"), 1, 32)];
        _sicht = Math.Max(0.1f, MeldungStil.Mass("rolle_sicht_mm") / 1000f);
        _kamera.Size = _sicht;
        _bild.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        AddChild(_bild);
        // Das Blatt der Symbole: ein kleines 2D-Bild, dessen Felder die Symbolflächen der Rollen als Textur zeigen
        _blatt = new SubViewport
        {
            Disable3D = true, TransparentBg = false, HandleInputLocally = false, GuiDisableInput = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            Size = new Vector2I(64, 64),
        };
        _zeichner = new Symbolblatt { Symbole = _symbole, MouseFilter = Control.MouseFilterEnum.Ignore };
        _blatt.AddChild(_zeichner);
        AddChild(_blatt);
        Groesse();
    }

    /// <summary>Nimmt einen freien Platz für eine Meldung dieser Art; null, wenn die Bühne nicht steht oder alle Plätze belegt sind.</summary>
    public Rolle? Nehmen(string themaId, Color farbe)
    {
        if (!Bereit) return null;
        var platz = Array.FindIndex(_plaetze, p => p is null || !p.Belegt);
        if (platz < 0) return null;
        var rolle = _plaetze[platz] ??= Bauen(platz);
        if (rolle is null) return null;
        var m = MeldungStil.Mass;
        var clip = rolle.Spieler.HasAnimation("aufrollen") ? rolle.Spieler.GetAnimation("aufrollen").Length : 0.9;
        var masse = new RollenMasse(
            ClipSekunden: clip, SymbolAb: m("rolle_symbol_ms") / 1000.0, NeigungGrad: m("rolle_neigung_grad"), PendelGrad: Math.Min(2, m("rolle_pendel_grad")),
            HuepfHoehe: m("rolle_huepfen_mm") / 1000.0, HuepfSekunden: m("rolle_huepfen_ms") / 1000.0, AusblendSekunden: m("rolle_ausblenden_ms") / 1000.0);
        rolle.Ablauf = new SchriftrolleAblauf(masse, GD.Randf(), GD.Randf());
        rolle.Belegt = true;
        rolle.Wurzel.Visible = true;
        SymbolSetzen(rolle, themaId, farbe);
        Anwenden(rolle);
        _nachlauf = 3;
        return rolle;
    }

    /// <summary>Gibt den Platz wieder frei (das Modell bleibt für die nächste Meldung stehen, unsichtbar).</summary>
    public void Geben(Rolle? rolle)
    {
        if (rolle is null) return;
        rolle.Belegt = false;
        if (IsInstanceValid(rolle.Wurzel)) rolle.Wurzel.Visible = false;
        _nachlauf = 3;
    }

    /// <summary>
    /// Überträgt den Stand der Rolle sofort auf das Modell (nach <see cref="SchriftrolleAblauf.Erscheinen"/> von außen): Sonst zeigte das nächste Bild noch
    /// einmal die alte Haltung, weil die Bühne in diesem Bild schon gerechnet hat.
    /// </summary>
    public void Uebernehmen(Rolle? rolle)
    {
        if (rolle is not { Belegt: true }) return;
        Anwenden(rolle);
        _nachlauf = 3;
    }

    /// <summary>Der Ausschnitt der Rolle im Bild (Pixel der Textur).</summary>
    public Rect2 Ausschnitt(Rolle rolle) => new(rolle.Platz * _zellePixel, 0, _zellePixel, _zellePixel);

    private Rolle? Bauen(int platz)
    {
        if (_modell!.InstantiateOrNull<Node3D>() is not { } wurzel) return null;
        if (wurzel.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault() is not { } spieler)
        {
            GD.PushWarning("[schriftrolle] Modell ohne AnimationPlayer.");
            wurzel.QueueFree();
            return null;
        }
        _rollen!.AddChild(wurzel);
        HandschuhZeiger.MaterialienEinrichten(wurzel); // Umriss: invertierte Hülle, schwarz ohne Licht, wie beim Handschuh
        // Die Clips verformen das Netz weit über seine Rast-Pose hinaus: feste Hülle, damit nichts weggeschnitten wird (Empfehlung des Modells)
        foreach (var netz in wurzel.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            if (netz.Name.ToString().StartsWith("Schriftrolle")) netz.CustomAabb = new Aabb(new Vector3(-0.5f, -0.5f, -0.5f), Vector3.One);
        foreach (var name in new[] { "aufrollen", "zurollen" })
            if (spieler.HasAnimation(name)) spieler.GetAnimation(name).LoopMode = Animation.LoopModeEnum.None;
        spieler.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual; // die Zeit gibt SchriftrolleAblauf vor
        var rolle = new Rolle { Platz = platz, Wurzel = wurzel, Spieler = spieler };
        rolle.Symbol = wurzel.FindChild("Symbolflaeche", true, false) as MeshInstance3D;
        if (rolle.Symbol is { } symbol && symbol.GetActiveMaterial(0) is StandardMaterial3D stoff && _blatt is not null)
        {
            // Eigener Stoff je Rolle (das Netz teilen sich alle): die Textur ist das gemeinsame Blatt, der Ausschnitt das Feld der Meldungsart
            rolle.SymbolStoff = (StandardMaterial3D)stoff.Duplicate();
            rolle.SymbolStoff.AlbedoTexture = _blatt.GetTexture();
            rolle.SymbolStoff.TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear;
            rolle.SymbolStoff.Uv1Scale = new Vector3(1f / BlattSeite, 1f / BlattSeite, 1);
            symbol.SetSurfaceOverrideMaterial(0, rolle.SymbolStoff);
        }
        GD.Print($"[schriftrolle] Rolle für Platz {platz} gebaut: Clips {string.Join(", ", spieler.GetAnimationList())}, Symbolfläche {(rolle.Symbol is null ? "fehlt" : "da")}");
        return rolle;
    }

    /// <summary>Legt das Symbol der Meldungsart auf die Rolle: das Feld des Blatts, neu angelegt, wenn es diese Art noch nicht gab.</summary>
    private void SymbolSetzen(Rolle rolle, string themaId, Color farbe)
    {
        if (rolle.SymbolStoff is null) return;
        var feld = _symbole.FindIndex(s => s.Thema == themaId && s.Farbe == farbe);
        if (feld < 0 && _symbole.Count < BlattSeite * BlattSeite)
        {
            _symbole.Add((themaId, farbe));
            feld = _symbole.Count - 1;
            BlattZeichnen();
        }
        if (feld < 0) feld = 0;
        rolle.SymbolStoff.Uv1Offset = new Vector3(feld % BlattSeite / (float)BlattSeite, feld / BlattSeite / (float)BlattSeite, 0);
    }

    private void BlattZeichnen()
    {
        if (_blatt is null || _zeichner is null) return;
        _zeichner.QueueRedraw();
        _blatt.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        _nachlauf = 3;
    }

    /// <summary>Passt die Bildgröße an das Fenster an: so viele echte Pixel, wie die Rolle auf dem Bildschirm braucht (mal Überabtastung), damit sie scharf bleibt.</summary>
    private void Groesse()
    {
        if (_bild is null || _blatt is null || _zeichner is null) return;
        var faktor = Math.Max(GetWindow().GetFinalTransform().Scale.X, 0.1f);
        var pixel = Math.Clamp((int)Math.Round(Zelle * faktor * MeldungStil.Mass("rolle_abtastung_prozent") / 100f), 32, 512);
        if (pixel == _zellePixel) return;
        _zellePixel = pixel;
        _bild.Size = new Vector2I(pixel * _plaetze.Length, pixel);
        // Die Plätze stehen nebeneinander, jeder genau ein Sichtfeld breit
        for (var i = 0; i < _plaetze.Length; i++)
            if (_plaetze[i] is { } r) r.Wurzel.Position = Ort(i);
        // Ein Feld des Blatts etwa doppelt so fein, wie die Symbolfläche im Bild groß ist (0,2 m von einem Sichtfeld)
        var feld = Math.Clamp((int)Math.Round(pixel * 0.2f / _sicht * 2), 24, 256);
        _blatt.Size = new Vector2I(feld * BlattSeite, feld * BlattSeite);
        _zeichner.Size = _blatt.Size;
        _zeichner.Feld = feld;
        BlattZeichnen();
    }

    private Vector3 Ort(int platz) => new((platz + 0.5f - _plaetze.Length / 2f) * _sicht, 0, 0);

    public override void _Process(double delta)
    {
        if (_bild is null) return;
        // Bildzeit, nicht Spielzeit: Die Rollen gehören zur Oberfläche und laufen unabhängig vom Spieltempo (auch in der Pause des Spiels)
        var dt = Math.Min(delta, 0.25);
        var bewegt = Meldungseinstellungen.Bewegung;
        var belegt = false;
        foreach (var r in _plaetze)
        {
            if (r is not { Belegt: true }) continue;
            belegt = true;
            r.Ablauf.Weiter(dt, bewegt);
            Anwenden(r);
        }
        var sichtbar = belegt && Sichtbar?.Invoke() != false;
        if (sichtbar && !_warSichtbar) _nachlauf = 3;
        _warSichtbar = sichtbar;
        if (sichtbar) Groesse();
        // Mit Bewegung jedes Bild; ohne Bewegung nur ein paar Bilder nach einer Änderung (das letzte Bild bleibt in der Textur stehen)
        var rechnen = sichtbar && (bewegt || _nachlauf > 0);
        if (_nachlauf > 0) _nachlauf--;
        var modus = rechnen ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        if (_bild.RenderTargetUpdateMode != modus) _bild.RenderTargetUpdateMode = modus;
    }

    /// <summary>Überträgt den Stand des Ablaufs auf das Modell: Stelle im Clip, Symbol an oder aus, Sprung, Neigung und Drehen im Leerlauf.</summary>
    private void Anwenden(Rolle r)
    {
        var a = r.Ablauf;
        if (r.Spieler.HasAnimation(a.Clip))
        {
            if (r.Clip != a.Clip || !r.Spieler.IsPlaying())
            {
                r.Spieler.Play(a.Clip);
                r.Clip = a.Clip;
            }
            r.Spieler.Seek(Math.Min(a.Clipzeit, r.Spieler.GetAnimation(a.Clip).Length - 0.001), true);
            r.Spieler.Advance(0); // im Modus Manual übernimmt erst das die Haltung sofort (sonst hinkt sie ein Bild nach)
        }
        if (r.Symbol is { } symbol && symbol.Visible != a.SymbolSichtbar) symbol.Visible = a.SymbolSichtbar;
        var (x, y, z) = a.Drehung;
        r.Wurzel.Position = Ort(r.Platz) + new Vector3(0, (float)a.Hoehe, 0);
        // Ablauf: positiv X = Oberkante nach hinten (Blick nach oben); in Godot dreht das eine negative Drehung um X
        r.Wurzel.RotationDegrees = new Vector3(-(float)x, (float)y, (float)z);
    }

    /// <summary>Das Blatt der Symbole: je Feld die Marke einer Meldungsart (<see cref="Siegelzeichner.Marke"/>).</summary>
    private sealed partial class Symbolblatt : Control
    {
        public List<(string Thema, Color Farbe)> Symbole = new();
        public int Feld = 16;

        public override void _Draw()
        {
            var grund = MeldungStil.RollenFarbe("symbol_grund");
            var rand = MeldungStil.Mass("rolle_symbol_rand_prozent") / 100f;
            for (var i = 0; i < Symbole.Count; i++)
                Siegelzeichner.Marke(this, new Rect2(i % BlattSeite * Feld, i / BlattSeite * Feld, Feld, Feld), Symbole[i].Farbe, Symbole[i].Thema, grund, rand);
        }
    }
}
