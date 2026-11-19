using Godot;
using Reichsgilde.Core.Dynastie;

namespace Reichsgilde;

/// <summary>
/// Die 3D-Bühne des Siegelstempels: ein kleines Bild über dem Schriftstück, in dem der Stempel (<c>siegelstempel.glb</c>) das Wachs siegelt.
/// Szene <c>siegelstempel_buehne.tscn</c> (Blick und Licht), Maße und Zeiten aus <c>meldung_thema.tres</c> (keine festen Farben oder Größen hier),
/// der Ablauf in <see cref="SiegelAblauf"/> (ohne Godot, getestet). Das Wachs bekommt die Farbe des Schriftstücks, der Abdruck das Wappen des
/// Hauses (seine Farben über dem aufgehellten Wachston); er ist erst zu sehen, wenn der Stempel abgehoben hat (Schwelle des Modells).
/// Gerechnet wird nur, solange der Stempel läuft. Fehlt Szene oder Modell, meldet <see cref="Siegeln"/> sofort „fertig“.
/// </summary>
public sealed partial class SiegelstempelBuehne : TextureRect
{
    private const string SzenePfad = "res://scenes/siegelstempel_buehne.tscn", ModellPfad = "res://assets/modelle/oberflaeche/siegelstempel.glb";

    private SubViewport? _bild, _zeichen;
    private AnimationPlayer? _spieler;
    private MeshInstance3D? _abdruck;
    private StandardMaterial3D? _abdruckStoff;
    private readonly List<StandardMaterial3D> _wachs = new();
    private WappenBild? _wappen;
    private readonly SiegelAblauf _ablauf = new();
    private Action? _fertig;
    private string _clip = "";
    private double _ruhe;

    /// <summary>Modell und Szene sind da.</summary>
    public bool Bereit => _bild is not null && _spieler is not null;

    /// <summary>Läuft der Stempel gerade (bis das Bild wieder weg ist)?</summary>
    public bool Laeuft => _fertig is not null;

    /// <summary>Kantenlänge des Bildes in Pixeln (Thema).</summary>
    public static int Groesse => MeldungStil.Mass("siegelstempel_groesse");

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ExpandMode = ExpandModeEnum.IgnoreSize;
        StretchMode = StretchModeEnum.Scale;
        Visible = false;
        if (!ResourceLoader.Exists(SzenePfad) || !ResourceLoader.Exists(ModellPfad))
        {
            GD.PushWarning("[siegelstempel] Szene oder Modell fehlt, es wird ohne Stempel bestätigt.");
            return;
        }
        var bild = GD.Load<PackedScene>(SzenePfad)?.InstantiateOrNull<SubViewport>();
        var wurzel = GD.Load<PackedScene>(ModellPfad)?.InstantiateOrNull<Node3D>();
        var platz = bild?.GetNodeOrNull<Node3D>("Stempel");
        var spieler = wurzel?.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (bild is null || wurzel is null || platz is null || spieler is null)
        {
            GD.PushWarning("[siegelstempel] Szene oder Modell unvollständig (Stempel, AnimationPlayer), es wird ohne Stempel bestätigt.");
            bild?.QueueFree();
            wurzel?.QueueFree();
            return;
        }
        bild.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        var kante = Math.Clamp(Groesse * MeldungStil.Mass("siegelstempel_abtastung_prozent") / 100, 16, 2048);
        bild.Size = new Vector2I(kante, kante);
        AddChild(bild);
        platz.AddChild(wurzel);
        HandschuhZeiger.MaterialienEinrichten(wurzel); // Umriss: invertierte Hülle, schwarz ohne Licht, wie beim Handschuh
        // Hülle über alle Bilder des Clips (Doku des Modells), sonst schneidet Godot den gehobenen Stempel weg
        var huelle = new Aabb(new Vector3(-0.13f, -0.02f, -0.10f), new Vector3(0.29f, 0.17f, 0.20f));
        foreach (var netz in wurzel.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (netz.Skin is not null) netz.CustomAabb = huelle;
            for (var i = 0; i < (netz.Mesh?.GetSurfaceCount() ?? 0); i++)
                if (netz.Mesh!.SurfaceGetMaterial(i) is StandardMaterial3D { ResourceName: "Siegel_Wachs" } stoff)
                {
                    var kopie = (StandardMaterial3D)stoff.Duplicate();
                    netz.SetSurfaceOverrideMaterial(i, kopie);
                    _wachs.Add(kopie);
                }
        }
        foreach (var name in spieler.GetAnimationList()) spieler.GetAnimation(name).LoopMode = Animation.LoopModeEnum.None;
        spieler.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual; // die Zeit gibt SiegelAblauf vor

        // Der Abdruck: das Wappen des Hauses als kleines 2D-Bild mit durchsichtigem Grund auf der Fläche Abdruck_Wappen (UV 0 bis 1)
        _zeichen = new SubViewport
        {
            Disable3D = true, TransparentBg = true, HandleInputLocally = false, GuiDisableInput = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            Size = new Vector2I(128, 128),
        };
        _wappen = WappenBild.Neu(null, 112);
        _wappen.MouseFilter = MouseFilterEnum.Ignore;
        _wappen.Size = _wappen.CustomMinimumSize;
        _wappen.Position = (new Vector2(128, 128) - _wappen.Size) / 2;
        _zeichen.AddChild(_wappen);
        AddChild(_zeichen);
        _abdruck = wurzel.FindChild("Abdruck_Wappen", true, false) as MeshInstance3D;
        if (_abdruck is not null)
        {
            _abdruckStoff = new StandardMaterial3D
            {
                ResourceName = "Siegel_Abdruck", AlbedoTexture = _zeichen.GetTexture(), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear, Roughness = _wachs.FirstOrDefault()?.Roughness ?? 0.42f,
                Uv1Scale = new Vector3(1, -1, 1), Uv1Offset = new Vector3(0, 1, 0), // das Bild eines SubViewports steht auf diesen Flächen sonst kopf
            };
            _abdruck.MaterialOverride = _abdruckStoff;
            _abdruck.Visible = false;
        }
        Texture = bild.GetTexture();
        _bild = bild;
        _spieler = spieler;
        GD.Print($"[siegelstempel] gebaut: Clips {string.Join(", ", spieler.GetAnimationList())}, Wachs {_wachs.Count}, Abdruck {(_abdruck is null ? "fehlt" : "da")}");
    }

    /// <summary>
    /// Siegelt: Wachs in <paramref name="wachs"/>, Abdruck mit dem Wappen. <paramref name="fertig"/> wird genau einmal gerufen, wenn das Siegel
    /// sitzt und kurz gestanden hat – sofort, wenn Szene oder Modell fehlen oder die Bewegung abgeschaltet ist („Siegel und Briefe bewegen“).
    /// </summary>
    public void Siegeln(Color wachs, Wappen? wappen, Action fertig)
    {
        if (Laeuft) return;
        if (!Bereit || !Meldungseinstellungen.Bewegung)
        {
            fertig();
            return;
        }
        foreach (var stoff in _wachs) stoff.AlbedoColor = wachs;
        if (_abdruckStoff is not null) _abdruckStoff.AlbedoColor = wachs.Lightened(MeldungStil.Mass("siegelstempel_abdruck_hell_prozent") / 100f);
        if (_wappen is not null) _wappen.Wappen = wappen;
        _wappen?.QueueRedraw();
        _zeichen!.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        _ablauf.Zuruecksetzen();
        _ablauf.Siegeln(true);
        _fertig = fertig;
        _ruhe = 0;
        CustomMinimumSize = Size = new Vector2(Groesse, Groesse);
        Modulate = Colors.White;
        _bild!.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        Stellen();
        Show();
    }

    /// <summary>Bricht ab, ohne „fertig“ zu melden (das Schriftstück ist weg).</summary>
    public void Abbrechen()
    {
        _fertig = null;
        Ruhen();
    }

    private void Ruhen()
    {
        Hide();
        if (_bild is not null) _bild.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
    }

    private void Stellen()
    {
        if (_spieler is null) return;
        var clip = _ablauf.Clip;
        if (_spieler.HasAnimation(clip))
        {
            if (_clip != clip || !_spieler.IsPlaying())
            {
                _spieler.Play(clip);
                _clip = clip;
            }
            _spieler.Seek(Math.Min(_ablauf.Clipzeit, _spieler.GetAnimation(clip).Length - 0.001), true);
            _spieler.Advance(0);
        }
        if (_abdruck is not null) _abdruck.Visible = _ablauf.AbdruckSichtbar;
    }

    public override void _Process(double delta)
    {
        if (_fertig is null) return;
        _ablauf.Weiter(delta);
        Stellen();
        if (_ablauf.Zustand != SiegelZustand.Gesiegelt) return;
        _ruhe += delta * 1000;
        var (halten, aus) = (MeldungStil.Mass("siegelstempel_halten_ms"), Math.Max(1, MeldungStil.Mass("siegelstempel_ausblenden_ms")));
        Modulate = new Color(1, 1, 1, (float)Math.Clamp(1 - (_ruhe - halten) / aus, 0, 1));
        if (_ruhe < halten + aus) return;
        var fertig = _fertig;
        _fertig = null;
        Ruhen();
        fertig();
    }
}
