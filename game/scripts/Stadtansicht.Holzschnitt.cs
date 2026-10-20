using Godot;

namespace Reichsgilde;

/// <summary>
/// Der Holzschnitt-Stil (Grafikoption, <see cref="Fenster.HolzschnittAn"/>) – ein Teil der <c>partial class Stadtansicht</c>.
///
/// Die Arbeit mit den Materialien steht in <see cref="Holzschnitt"/>; hier ist die Verdrahtung:
/// <list type="bullet">
/// <item>Beim Einschalten wird alles umgestellt, was schon da ist, und <see cref="SceneTree.NodeAdded"/> meldet alles, was danach
/// dazukommt (Neubau, Ausbau, Karrenkauf, Passanten, Innenräume): am Ende des Bildes, wenn das Spiel das Material des neuen Knotens
/// gesetzt hat, bekommt er sein Holzschnitt-Material. Ausgeschaltet hört die Stadt nicht zu und kostet nichts.</item>
/// <item>Beim Ausschalten gehen die Materialien zurück an die Knoten, der Umriss fällt weg: kein Rest.</item>
/// <item>Je Bild: Umgebungslicht für den Shader, Strichbreite des Umrisses und das Nachziehen umgefärbter Materialien.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Null = Holzschnitt aus. Sonst die Materialien, die er ersetzt hat.</summary>
    private Holzschnitt? _holzschnitt;

    private readonly List<Node> _holzschnittNeu = new();
    private bool _holzschnittPlanung;
    private MeshInstance3D? _umrissFlaeche;
    private ShaderMaterial? _umrissStoff;
    private Vector3? _holzschnittUmgebung;
    private float? _umrissStaerke;
    private bool? _holzschnittImRaum;
    private readonly bool _holzschnittKompatibel = RenderingServer.GetCurrentRenderingMethod() == "gl_compatibility";

    /// <summary>
    /// Strichbreite des Umrisses bei 50 %: Der Shader zeichnet die Linie beiderseits der Kante, ihre Breite ist also das Doppelte des Werts.
    /// Bei Zoom 95 (9,5 Pixel je Meter) etwa ein Pixel, bei nahem Zoom 25 (36 Pixel je Meter) etwas mehr: Figuren sind dort nur 40 Pixel hoch und würden von einer dickeren Linie ganz schwarz (<see cref="UmrissBreite"/>).
    /// </summary>
    private const float UmrissGrundbreite = 0.5f, UmrissJeMeter = 0.008f, UmrissMinimum = 0.55f, UmrissMaximum = 1.0f;

    /// <summary>Im Raum (Perspektive, feste Ansicht) ist der Umriss immer gleich breit, bezogen auf 900 Pixel Bildhöhe.</summary>
    private const float RaumUmrissBreite = 1.0f, BezugsHoehe = 900f;

    /// <summary>Einmal in <c>_Ready</c>, vor dem Aufbau: auf die Einstellung hören und den Stil gleich anwenden, wenn er an ist.</summary>
    private void HolzschnittEinrichten()
    {
        Fenster.HolzschnittGeaendert += HolzschnittNachfuehren;
        HolzschnittNachfuehren();
    }

    /// <summary>
    /// In <c>_ExitTree</c>: nicht mehr zuhören und dem RenderingServer sein altes Bild zurückgeben. Die Holzschnitt-Materialien gehen danach an
    /// den Müllsammler, die Knoten stehen aber noch bis zum Ende des Baums: Keiner soll dann noch auf ein freigegebenes Material zeigen.
    /// </summary>
    private void HolzschnittAufgeben()
    {
        Fenster.HolzschnittGeaendert -= HolzschnittNachfuehren;
        if (_holzschnitt is not null) HolzschnittAusschalten(beimBeenden: true);
    }

    private void HolzschnittNachfuehren()
    {
        if (Fenster.HolzschnittAn == (_holzschnitt is not null)) return; // nur der Umriss hat gewechselt: das liest HolzschnittBild jedes Bild
        if (Fenster.HolzschnittAn) HolzschnittEinschalten();
        else HolzschnittAusschalten();
    }

    private void HolzschnittEinschalten()
    {
        _holzschnitt = new Holzschnitt();
        GetTree().NodeAdded += HolzschnittKnotenNeu;
        foreach (var teil in FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()) _holzschnitt.Umstellen(teil);
        foreach (var teil in FindChildren("*", nameof(MultiMeshInstance3D), true, false).OfType<MultiMeshInstance3D>()) _holzschnitt.Umstellen(teil);
    }

    private void HolzschnittAusschalten(bool beimBeenden = false)
    {
        GetTree().NodeAdded -= HolzschnittKnotenNeu;
        foreach (var teil in FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()) Holzschnitt.Zuruecksetzen(teil);
        foreach (var teil in FindChildren("*", nameof(MultiMeshInstance3D), true, false).OfType<MultiMeshInstance3D>()) _holzschnitt!.Zuruecksetzen(teil);
        if (!beimBeenden) _umrissFlaeche?.QueueFree(); // beim Beenden fällt es mit der Kamera
        _umrissFlaeche = null;
        _umrissStoff = null;
        _umrissStaerke = null;
        _holzschnittUmgebung = null;
        _holzschnittImRaum = null;
        _holzschnitt = null;
        _holzschnittNeu.Clear();
    }

    /// <summary>Ein Knoten kam in den Baum: Netze und MultiMesh der Stadt merken und am Ende des Bildes umstellen (dann hat das Spiel sein Material gesetzt).</summary>
    private void HolzschnittKnotenNeu(Node knoten)
    {
        if (knoten is not (MeshInstance3D or MultiMeshInstance3D) || !IsAncestorOf(knoten)) return;
        _holzschnittNeu.Add(knoten);
        if (_holzschnittPlanung) return;
        _holzschnittPlanung = true;
        Callable.From(HolzschnittNeueUmstellen).CallDeferred();
    }

    private void HolzschnittNeueUmstellen()
    {
        _holzschnittPlanung = false;
        var neu = _holzschnittNeu.ToList();
        _holzschnittNeu.Clear();
        if (_holzschnitt is null) return;
        foreach (var knoten in neu)
        {
            if (!IsInstanceValid(knoten)) continue;
            if (knoten is MeshInstance3D teil) _holzschnitt.Umstellen(teil);
            else if (knoten is MultiMeshInstance3D vielfach) _holzschnitt.Umstellen(vielfach);
        }
    }

    /// <summary>Das Netz, das ein MultiMesh jetzt zeigen soll: mit Holzschnitt-Materialien, wenn der Stil an ist (Jahreszeitwechsel der Natur).</summary>
    private Mesh HolzschnittNetz(Mesh netz) => _holzschnitt?.Netz(netz) ?? netz;

    // ═════════════════════════════════════════════════════════════════════════
    //  Je Bild
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jedes Bild, solange der Stil an ist: umgefärbte Materialien nachziehen, Umgebungslicht und Umriss einstellen.</summary>
    private void HolzschnittBild()
    {
        if (_holzschnitt is null) return;
        _holzschnitt.Abgleichen();

        // Grundhelligkeit ohne Sonne: Farbe × Stärke des Umgebungslichts der Umgebung, die gerade gezeigt wird (Stadt oder Raum).
        // Godot rechnet das Umgebungslicht in Forward+ linear; der Compatibility-Renderer rechnet in sRGB.
        var umgebung = _raum is not null ? _raumUmgebung : _umgebung.Environment;
        var licht = umgebung.AmbientLightColor;
        if (!_holzschnittKompatibel) licht = licht.SrgbToLinear();
        var grundhelligkeit = new Vector3(licht.R, licht.G, licht.B) * umgebung.AmbientLightEnergy;
        if (_holzschnittUmgebung != grundhelligkeit)
        {
            _holzschnittUmgebung = grundhelligkeit;
            RenderingServer.GlobalShaderParameterSet("holzschnitt_umgebung", grundhelligkeit);
        }
        var imRaum = _raum is not null;
        if (_holzschnittImRaum != imRaum)
        {
            _holzschnittImRaum = imRaum;
            RenderingServer.GlobalShaderParameterSet("holzschnitt_raum", imRaum ? 1f : 0f); // Schraffur und Schattenstufen im Raum (holzschnitt.gdshader)
        }
        HolzschnittUmriss();
    }

    /// <summary>
    /// Der Umriss ist ein Quad vor der aktiven Kamera (Stadtkamera oder Raumkamera): Es zeichnet an Sprüngen der Tiefe eine schwarze Linie.
    /// Die Strichbreite folgt dem Zoom (<see cref="UmrissBreite"/>) und dem Regler „Umriss“; bei 0 % fehlt der Umriss ganz.
    /// </summary>
    private void HolzschnittUmriss()
    {
        var kamera = _raum is not null ? _raumKamera : _kamera;
        if (Fenster.HolzschnittUmriss <= 0)
        {
            if (_umrissFlaeche is not null) _umrissFlaeche.Visible = false;
            return;
        }
        if (_umrissFlaeche is null)
        {
            _umrissStoff = new ShaderMaterial { Shader = GD.Load<Shader>("res://shader/holzschnitt_umriss.gdshader"), RenderPriority = -128 }; // vor den Schildern
            _umrissFlaeche = new MeshInstance3D
            {
                Name = "HolzschnittUmriss",
                Mesh = new QuadMesh { Size = new Vector2(2, 2) }, // der Shader legt es über das ganze Bild, die Größe zählt nicht
                MaterialOverride = _umrissStoff,
                ExtraCullMargin = 16384f, // sonst fällt es aus der Sichtprüfung der Kamera
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
        }
        if (_umrissFlaeche.GetParent() != kamera)
        {
            _umrissFlaeche.GetParent()?.RemoveChild(_umrissFlaeche);
            kamera.AddChild(_umrissFlaeche);
            _umrissFlaeche.Position = new Vector3(0, 0, -2);
        }
        _umrissFlaeche.Visible = true;

        var hoehe = GetViewport().GetVisibleRect().Size.Y;
        var breite = _raum is not null ? RaumUmrissBreite * hoehe / BezugsHoehe : UmrissBreite(hoehe / _kamera.Size);
        // Bei FSR rechnet Godot das Bild kleiner: Die Breite zählt in Pixeln des kleinen Bildes
        var staerke = breite * UmrissFaktor(Fenster.HolzschnittUmriss) * GetViewport().Scaling3DScale;
        if (_umrissStaerke == staerke) return;
        _umrissStaerke = staerke;
        _umrissStoff!.SetShaderParameter("staerke", staerke);
    }

    /// <summary>Strichbreite in Pixeln der Stadt bei 50 %: dünn bei weitem Zoom, dicker, wenn man nah heranzoomt (Pixel je Meter der Stadt).</summary>
    internal static float UmrissBreite(float pixelJeMeter) => Mathf.Clamp(UmrissGrundbreite + UmrissJeMeter * pixelJeMeter, UmrissMinimum, UmrissMaximum);

    /// <summary>Der Regler „Umriss“ (0–100 %) als Faktor auf die Strichbreite: 50 % = 1, je 50 Punkte doppelt oder halb so breit (10 % ≈ 0,57, 100 % = 2).</summary>
    internal static float UmrissFaktor(int prozent) => Mathf.Pow(2f, (prozent - 50) / 50f);
}
