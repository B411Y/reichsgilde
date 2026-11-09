using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Der Gizmo für Felsstücke in der Kartenwerkstatt (Fels R3, wie in Unreal) – ein Teil der <c>partial class Stadtansicht</c>. Ist ein Felsstück gewählt
/// (Werkzeug „Auswahl“, oder gleich nach einem Stempel im Felswerkzeug, P2), steht ein Gizmo an seinem Aufsetzpunkt (bei mehreren gewählten Stücken an
/// ihrer gemeinsamen Mitte):
/// <list type="bullet">
/// <item><b>Verschieben</b>: drei Pfeile (eine Achse) und drei Quadrate (eine Ebene); <b>Drehen</b>: drei Ringe; <b>Skalieren</b>: drei Würfel an
/// Linien (eine Achse) und ein Würfel in der Mitte (gleichmäßig). Den Modus wählen die Knöpfe der Tafel oder – solange der Gizmo steht – die Tasten
/// <b>1</b>, <b>2</b>, <b>3</b> (die Gruppen 1–4 der Leiste gibt es dann erst nach Esc), <b>4</b> schaltet zwischen Welt- und Stückachsen um (beim Skalieren gelten immer die Achsen des
/// Stücks, bei mehreren Stücken die der Welt). <b>B</b> oder <b>End</b> setzt die Stücke auf den Boden, mit Umschalt richtet sie sie zugleich am Hang
/// aus. Mit gehaltener <b>Strg</b>-Taste (oder dem Häkchen in der Tafel) rastet der Zug ein: 0,25 m, 15°, 0,1. Die <b>Pfeiltasten</b> schieben die
/// gewählten Stücke in kleinen Schritten (Entscheidung des Nutzers, P2; WASD bleibt immer bei der Kamera; Stadtansicht.Felsauswahl.cs).</item>
/// <item><b>Mehrere Stücke</b>: Umschalt+Klick fügt ein Stück zur Auswahl hinzu bzw. nimmt es heraus; Ziehen ins Leere zieht einen Rahmen (Umschalt:
/// dazu), Strg+A wählt alle, Strg+I kehrt die Auswahl um (P2, Stadtansicht.Felsauswahl.cs). Der Gizmo wirkt um die gemeinsame Mitte, Entf löscht alle
/// gewählten, <b>Strg+D</b> kopiert sie (die Kopien sind dann gewählt).</item>
/// <item><b>Ein Befehl erst beim Loslassen</b>: Beim Ziehen rechnet der Kern nur die Vorschau (<see cref="Kartenentwurf.FelsVorschau"/>) und die
/// Instanzen der Stücke wandern mit; Loslassen gibt <see cref="Kartenentwurf.FelsTransformieren(IEnumerable{EntwurfFels}, Felsaenderung)"/> – ein
/// Rückgängig-Schritt je Zug. Esc nimmt den Zug zurück.</item>
/// </list>
/// Der Gizmo besteht aus einfachen Netzen mit dem Material „immer sichtbar“; gepickt wird nicht über die Physik, sondern mit einem Strahltest gegen die
/// Griffe (Strecken, Quadrate, Kreise) in den Maßen des Gizmos.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Was der Gizmo tut.</summary>
    public enum FelsGizmoArt
    {
        Verschieben,
        Drehen,
        Skalieren,
    }

    /// <summary>Der gewählte Modus des Gizmos (Tasten 1, 2, 3 bei stehendem Gizmo oder die Knöpfe der Tafel).</summary>
    public FelsGizmoArt FelsGizmoModus { get; private set; } = FelsGizmoArt.Verschieben;

    /// <summary>Welt- oder Stückachsen (Taste 4 bei stehendem Gizmo)?</summary>
    public bool FelsGizmoWelt { get; private set; } = true;

    /// <summary>Einrasten dauerhaft an (Häkchen in der Tafel); mit Strg rastet der Zug ohnehin ein.</summary>
    public bool FelsGizmoRastet { get; set; }

    /// <summary>Modus, Achsen, Einrasten oder die Auswahl haben sich geändert – die Tafel zeigt es.</summary>
    public event Action? FelsGizmoGeaendert;

    // Schrittweiten beim Einrasten (Unreal: Weg, Winkel, Größe)
    private const double GizmoRasterWeg = 0.25, GizmoRasterWinkel = 15, GizmoRasterFaktor = 0.1;

    /// <summary>Wie groß der Gizmo auf dem Bildschirm ist (Bildpunkte, Länge einer Achse).</summary>
    private const float GizmoPixel = 120f;

    private readonly List<EntwurfFels> _felsAuswahl = new();

    /// <summary>Die gewählten Felsstücke (das zuletzt gewählte ist <see cref="WerkstattGewaehlt"/>).</summary>
    public IReadOnlyList<EntwurfFels> FelsAuswahl => _felsAuswahl;

    private enum GriffArt
    {
        Achse,
        Ebene,
        Ring,
        Mitte,
    }

    private sealed class GizmoGriff
    {
        public required GriffArt Art { get; init; }

        /// <summary>Achse (0 x, 1 y, 2 z); bei einer Ebene die Achse, die senkrecht auf ihr steht.</summary>
        public int Achse { get; init; }
        public required MeshInstance3D Knoten { get; init; }
    }

    private sealed class GizmoZug
    {
        public required GizmoGriff Griff { get; init; }
        public required Vector3 Mitte { get; init; }
        public required Basis Ausrichtung { get; init; }
        public required Vector2 Maus { get; init; }

        /// <summary>Startwerte: Länge auf der Achse, Treffer auf der Ebene, Vektor in der Kreisebene.</summary>
        public float T0 { get; init; }
        public Vector3 P0 { get; init; }
        public Felsaenderung? Aenderung { get; set; }

        /// <summary>Beim Drehen: letzter Winkel zum Startvektor und die Summe der Änderungen (so läuft der Ring über einen halben Umlauf hinaus).</summary>
        public double WinkelLetzt { get; set; }
        public double WinkelSumme { get; set; }
    }

    private Node3D? _felsGizmo;
    private FelsGizmoArt? _gizmoGebaut;
    private readonly List<GizmoGriff> _gizmoGriffe = new();
    private GizmoGriff? _gizmoHover;
    private GizmoZug? _gizmoZug;
    private readonly List<MeshInstance3D> _felsMarken = new();
    private readonly Dictionary<int, TorusMesh> _felsMarkenNetze = new();
    private StandardMaterial3D? _felsMarkenStoff;
    private readonly Dictionary<int, StandardMaterial3D> _gizmoStoffe = new();
    private StandardMaterial3D? _gizmoHell;

    private static readonly Color[] GizmoFarben =
    {
        new(0.93f, 0.27f, 0.22f), new(0.50f, 0.82f, 0.26f), new(0.27f, 0.48f, 0.97f),
    };

    // ═════════════════════════════════════════════════════════════════════════
    //  Auswahl
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Nach <see cref="WerkstattWaehlen(EntwurfTeil?, bool)"/>: Die Auswahl besteht nur aus diesem Stück (oder ist leer).</summary>
    private void FelsAuswahlSetzen(EntwurfTeil? teil)
    {
        _felsAuswahl.Clear();
        if (teil is EntwurfFels fels) _felsAuswahl.Add(fels);
        FelsGizmoGeaendert?.Invoke();
    }

    /// <summary>Umschalt+Klick auf ein Stück: dazunehmen oder herausnehmen. Das zuletzt dazugenommene ist das gewählte (für die Tafel).</summary>
    private void FelsAuswahlUmschalten(EntwurfFels f)
    {
        if (WerkstattGewaehlt is not EntwurfFels)
        {
            WerkstattWaehlen(f);
            return;
        }
        if (!_felsAuswahl.Remove(f)) _felsAuswahl.Add(f);
        var vorher = WerkstattGewaehlt;
        var primaer = _felsAuswahl.Count > 0 ? _felsAuswahl[^1] : null;
        WerkstattGewaehlt = primaer;
        _wsGriff = null;
        if (vorher is not null && !ReferenceEquals(vorher, primaer)) TeilMarkieren(vorher);
        if (primaer is not null) TeilMarkieren(primaer);
        GriffeBauen();
        WerkstattAuswahlGeaendert?.Invoke(primaer);
        FelsGizmoGeaendert?.Invoke();
    }

    /// <summary>Gewählte Stücke, die inzwischen nicht mehr im Entwurf sind (gelöscht, Rückgängig), fallen aus der Auswahl.</summary>
    private void FelsAuswahlBereinigen()
    {
        if (_entwurf is null || _felsAuswahl.Count == 0) return;
        // viele gewählte (Rahmen, Strg+A): einmal alle Stücke des Entwurfs in eine Menge statt je gewähltes Stück die ganze Liste durchsuchen
        var da = _felsAuswahl.Count > 8 ? new HashSet<EntwurfFels>(_entwurf.Felsen, ReferenceEqualityComparer.Instance) : null;
        if (_felsAuswahl.RemoveAll(f => da is not null ? !da.Contains(f) : !_entwurf.Enthaelt(f)) > 0) FelsGizmoGeaendert?.Invoke();
        if (_felsAuswahl.Count > 0 && WerkstattGewaehlt is not EntwurfFels) WerkstattWaehlen(_felsAuswahl[^1]);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Tasten und Maus
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Steht der Gizmo? Mit dem Werkzeug „Auswahl“, und im Felswerkzeug beim Stempeln (P2: gleich nach dem Setzen steht er an den neuen Stücken) – sofern
    /// Felsstücke gewählt sind und die Ebene „Baum- und Naturzonen“ an ist.
    /// </summary>
    private bool FelsGizmoAktiv => _entwurf is not null && _felsAuswahl.Count > 0 && WerkstattEbeneSichtbar(Werkstattebene.Natur)
                                   && (Kartenwerkzeug == Werkstattwerkzeug.Auswahl || (Kartenwerkzeug == Werkstattwerkzeug.Fels && FelsModus == FelsArt.Stempeln));

    /// <summary>
    /// Die Tasten des Gizmos: 1, 2, 3 wählen den Modus (Verschieben, Drehen, Skalieren – wie die Knöpfe der Tafel), 4 schaltet die Achsen (Welt/Stück),
    /// B oder End setzt auf den Boden (mit <paramref name="umschalt"/> am Hang ausgerichtet). True, wenn die Taste dem Gizmo gehörte. Solange der Gizmo
    /// steht, gehören 1–4 ihm (Entscheidung des Nutzers, P2); die Gruppen 1–4 der Leiste links gibt es erst wieder ohne Gizmo (Esc). Die Pfeiltasten
    /// schieben die Stücke (<see cref="FelsPfeilTaste"/>, Stadtansicht.Felsauswahl.cs).
    /// </summary>
    private bool FelsGizmoTaste(Key taste, bool umschalt)
    {
        if (!FelsGizmoAktiv || _wsTeilZiehen is not null || _wsZiehen is not null) return false;
        switch (taste)
        {
            case Key.Key1: FelsGizmoModusSetzen(FelsGizmoArt.Verschieben); return true;
            case Key.Key2: FelsGizmoModusSetzen(FelsGizmoArt.Drehen); return true;
            case Key.Key3: FelsGizmoModusSetzen(FelsGizmoArt.Skalieren); return true;
            case Key.Key4:
                FelsGizmoWeltSetzen(!FelsGizmoWelt);
                return true;
            case Key.B or Key.End:
                FelsAufsetzen(umschalt);
                return true;
        }
        return false;
    }

    /// <summary>Gehören die Pfeiltasten gerade den gewählten Felsen (dann schieben sie die Stücke, nicht die Kamera)?</summary>
    private bool PfeileFuerGizmo => FelsGizmoAktiv;

    /// <summary>Gehören die Tasten 1–4 gerade dem Gizmo (Modus und Achsen) statt den Gruppen der Leiste links? Für die Szene (Kartenwerkstatt.cs).</summary>
    public bool ZiffernFuerGizmo => FelsGizmoAktiv && _gizmoZug is null && _wsTeilZiehen is null && _wsZiehen is null;

    /// <summary>Stellt den Modus des Gizmos ein (Tafel oder Taste).</summary>
    public void FelsGizmoModusSetzen(FelsGizmoArt art)
    {
        if (_gizmoZug is not null) return;
        FelsGizmoModus = art;
        FelsGizmoGeaendert?.Invoke();
    }

    /// <summary>Schaltet zwischen Welt- und Stückachsen um (Tafel).</summary>
    public void FelsGizmoWeltSetzen(bool welt)
    {
        if (_gizmoZug is not null || FelsGizmoWelt == welt) return;
        FelsGizmoWelt = welt;
        FelsGizmoGeaendert?.Invoke();
    }

    /// <summary>Führt einen Befehl der Werkstatt aus; ein Fehler steht unten in der Leiste (für die Zahlenfelder der Tafel).</summary>
    public void WerkstattBefehl(Action befehl) => Befehl(befehl);

    /// <summary>B: die gewählten Stücke auf den Boden setzen (mit <paramref name="ausrichten"/> zugleich am Hang ausrichten) – ein Schritt.</summary>
    public void FelsAufsetzen(bool ausrichten)
    {
        if (_entwurf is null || _felsAuswahl.Count == 0) return;
        Befehl(() => _entwurf.FelsEinrasten(_felsAuswahl.ToList(), ausrichten));
    }

    /// <summary>Linke Maustaste auf einen Griff des Gizmos: beginnt den Zug. False: kein Griff getroffen.</summary>
    private bool FelsGizmoDruecken(Vector2 maus)
    {
        if (!FelsGizmoAktiv || _felsGizmo is null || _gizmoZug is not null) return false;
        if (GizmoGriffBei(maus) is not { } griff) return false;
        var (mitte, ausrichtung) = GizmoLage();
        var kamera = StadtKamera;
        var von = kamera.ProjectRayOrigin(maus);
        var richtung = kamera.ProjectRayNormal(maus);
        var achse = ausrichtung[griff.Achse];
        var zug = new GizmoZug
        {
            Griff = griff, Mitte = mitte, Ausrichtung = ausrichtung, Maus = maus,
            T0 = griff.Art == GriffArt.Achse ? ParameterAufGerader(mitte, achse, von, richtung) : 0,
            P0 = griff.Art is GriffArt.Ebene or GriffArt.Ring ? (EbenenTreffer(mitte, achse, von, richtung) ?? mitte) : mitte,
        };
        if (griff.Art == GriffArt.Achse && Mathf.Abs(zug.T0) < 1e-3f) return false;
        _gizmoZug = zug;
        return true;
    }

    /// <summary>Linke Maustaste los: aus dem Zug wird der Befehl.</summary>
    private void FelsGizmoLoslassen()
    {
        if (_gizmoZug is not { } zug) return;
        _gizmoZug = null;
        if (_entwurf is not null) GizmoZugNachfuehren(zug, GetViewport().GetMousePosition()); // die letzte Mausbewegung zählt
        var auswahl = _felsAuswahl.ToList();
        if (_entwurf is not null && zug.Aenderung is { } a && auswahl.Count > 0)
            Befehl(() => _entwurf.FelsTransformieren(auswahl, a));
        FelsInstanzenZuruecksetzen(auswahl);
    }

    /// <summary>Esc beim Ziehen: Die Stücke gehen an ihren Platz zurück, es entsteht kein Befehl.</summary>
    private bool FelsGizmoAbbrechen()
    {
        if (_gizmoZug is null) return false;
        _gizmoZug = null;
        FelsInstanzenZuruecksetzen(_felsAuswahl.ToList());
        return true;
    }

    /// <summary>Alle Instanzen der Stücke wieder auf die Lage im Entwurf (nach einem Zug oder Abbruch).</summary>
    private void FelsInstanzenZuruecksetzen(IEnumerable<EntwurfFels> felsen)
    {
        if (_entwurf is null) return;
        foreach (var f in felsen)
            if (_felsInstanz.TryGetValue(f, out var i) && _entwurf.Enthaelt(f))
            {
                var b = FelsBildVon(f);
                i.Vielfach.SetInstanceTransform(i.Index, FelsLage(b));
                i.Vielfach.SetInstanceCustomData(i.Index, FelsDaten(b));
            }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lage des Gizmos
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wo der Gizmo steht und wie er ausgerichtet ist: Mitte der gewählten Stücke (Aufsetzpunkt, mindestens am Boden); Welt- oder Stückachsen.</summary>
    private (Vector3 Mitte, Basis Ausrichtung) GizmoLage()
    {
        var m = _entwurf!.FelsMitteWelt(_felsAuswahl);
        var mitte = new Vector3((float)m.X, (float)m.Y, (float)m.Z);
        // Skalieren: immer die Achsen des Stücks (bei mehreren die der Welt, dort gilt der Faktor für die Abstände)
        var stueck = FelsGizmoModus == FelsGizmoArt.Skalieren ? _felsAuswahl.Count == 1 : !FelsGizmoWelt && _felsAuswahl.Count > 0;
        if (!stueck || WerkstattGewaehlt is not EntwurfFels f) return (mitte, Basis.Identity);
        var b = _entwurf.FelsRahmen(f).Basis;
        return (mitte, new Basis(new Vector3((float)b.M00, (float)b.M10, (float)b.M20), new Vector3((float)b.M01, (float)b.M11, (float)b.M21),
            new Vector3((float)b.M02, (float)b.M12, (float)b.M22)).Orthonormalized());
    }

    /// <summary>Meter je Bildpunkt an einem Punkt der Welt (orthografisch überall gleich, sonst nach dem Abstand).</summary>
    private float GizmoMeterJePixel(Vector3 punkt)
    {
        var kamera = StadtKamera;
        var hoehe = GetViewport().GetVisibleRect().Size.Y;
        return kamera.Projection == Camera3D.ProjectionType.Orthogonal
            ? kamera.Size / hoehe
            : 2f * Mathf.Tan(Mathf.DegToRad(kamera.Fov) / 2f) * kamera.GlobalPosition.DistanceTo(punkt) / hoehe;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bauen
    // ═════════════════════════════════════════════════════════════════════════

    private StandardMaterial3D GizmoStoff(int achse)
    {
        if (!_gizmoStoffe.TryGetValue(achse, out var stoff))
        {
            var farbe = achse is >= 0 and <= 2 ? GizmoFarben[achse] : new Color(0.9f, 0.9f, 0.9f);
            _gizmoStoffe[achse] = stoff = ImmerSichtbar(new StandardMaterial3D
            {
                AlbedoColor = new Color(farbe, 0.92f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            });
            stoff.RenderPriority = 3;
        }
        return stoff;
    }

    private StandardMaterial3D GizmoHell => _gizmoHell ??= ImmerSichtbar(new StandardMaterial3D
    {
        AlbedoColor = new Color(1f, 0.92f, 0.35f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        RenderPriority = 4,
    });

    /// <summary>Die Drehung, die die y-Achse eines Netzes (Zylinder, Kegel, Torus) auf die Achse a legt.</summary>
    private static Basis AchseAuf(int a) => a switch
    {
        0 => new Basis(Vector3.Back, -Mathf.Pi / 2f), // y → x
        2 => new Basis(Vector3.Right, Mathf.Pi / 2f),  // y → z
        _ => Basis.Identity,
    };

    private static Vector3 Einheit(int a) => a switch { 0 => Vector3.Right, 1 => Vector3.Up, _ => Vector3.Back };

    private MeshInstance3D GizmoTeil(Mesh netz, Vector3 lage, Basis drehung, int achse)
    {
        var mi = new MeshInstance3D { Mesh = netz, MaterialOverride = GizmoStoff(achse), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Transform = new Transform3D(drehung, lage) };
        _felsGizmo!.AddChild(mi);
        return mi;
    }

    private void GizmoGriffe(GriffArt art, int achse, params MeshInstance3D[] teile)
    {
        // mehrere Netze eines Griffs hängen unter einem Knoten, damit das Aufhellen ein Material setzt
        var knoten = teile[0];
        _gizmoGriffe.Add(new GizmoGriff { Art = art, Achse = achse, Knoten = knoten });
        for (var i = 1; i < teile.Length; i++) _gizmoNebenteile[knoten] = _gizmoNebenteile.GetValueOrDefault(knoten) is { } l ? l.Append(teile[i]).ToList() : new List<MeshInstance3D> { teile[i] };
    }

    private readonly Dictionary<MeshInstance3D, List<MeshInstance3D>> _gizmoNebenteile = new();

    /// <summary>Baut die Netze des Gizmos für einen Modus (Länge einer Achse = 1; skaliert wird der ganze Knoten).</summary>
    private void FelsGizmoBauen(FelsGizmoArt art)
    {
        if (_felsGizmo is null)
        {
            _felsGizmo = new Node3D { Name = "FelsGizmo", Visible = false };
            AddChild(_felsGizmo);
        }
        foreach (var k in _felsGizmo.GetChildren()) k.QueueFree();
        _gizmoGriffe.Clear();
        _gizmoNebenteile.Clear();
        _gizmoHover = null;
        _gizmoGebaut = art;
        for (var a = 0; a < 3; a++)
        {
            var dreh = AchseAuf(a);
            var richtung = Einheit(a);
            switch (art)
            {
                case FelsGizmoArt.Verschieben:
                {
                    var linie = GizmoTeil(new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = 0.75f, RadialSegments = 8, Rings = 1 }, richtung * 0.475f, dreh, a);
                    var spitze = GizmoTeil(new CylinderMesh { TopRadius = 0, BottomRadius = 0.07f, Height = 0.22f, RadialSegments = 12, Rings = 1 }, richtung * 0.89f, dreh, a);
                    GizmoGriffe(GriffArt.Achse, a, linie, spitze);
                    // Quadrat in der Ebene senkrecht zur Achse a (die beiden anderen Achsen spannen sie auf)
                    var (u, v) = ((a + 1) % 3, (a + 2) % 3);
                    var quadrat = GizmoTeil(new QuadMesh { Size = new Vector2(0.28f, 0.28f) }, (Einheit(u) + Einheit(v)) * 0.42f,
                        new Basis(Einheit(u), Einheit(v), Einheit(a)), a);
                    quadrat.MaterialOverride = GizmoQuadratStoff(a);
                    GizmoGriffe(GriffArt.Ebene, a, quadrat);
                    break;
                }
                case FelsGizmoArt.Drehen:
                {
                    var ring = GizmoTeil(new TorusMesh { InnerRadius = 0.88f, OuterRadius = 0.92f, Rings = 48, RingSegments = 6 }, Vector3.Zero, dreh, a);
                    GizmoGriffe(GriffArt.Ring, a, ring);
                    break;
                }
                default:
                {
                    var linie = GizmoTeil(new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = 0.8f, RadialSegments = 8, Rings = 1 }, richtung * 0.5f, dreh, a);
                    var wuerfel = GizmoTeil(new BoxMesh { Size = new Vector3(0.14f, 0.14f, 0.14f) }, richtung * 0.92f, Basis.Identity, a);
                    GizmoGriffe(GriffArt.Achse, a, linie, wuerfel);
                    break;
                }
            }
        }
        if (art == FelsGizmoArt.Skalieren)
        {
            var mitte = GizmoTeil(new BoxMesh { Size = new Vector3(0.2f, 0.2f, 0.2f) }, Vector3.Zero, Basis.Identity, -1);
            GizmoGriffe(GriffArt.Mitte, 0, mitte);
        }
    }

    private readonly Dictionary<int, StandardMaterial3D> _gizmoQuadrate = new();

    private StandardMaterial3D GizmoQuadratStoff(int achse)
    {
        if (!_gizmoQuadrate.TryGetValue(achse, out var stoff))
        {
            _gizmoQuadrate[achse] = stoff = ImmerSichtbar(new StandardMaterial3D
            {
                AlbedoColor = new Color(GizmoFarben[achse], 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            });
            stoff.RenderPriority = 3;
        }
        return stoff;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jedes Bild der Werkstatt: Auswahl bereinigen, Gizmo stellen und beim Ziehen die Vorschau nachführen.</summary>
    private void FelsGizmoAktualisieren()
    {
        FelsAuswahlBereinigen();
        if (!FelsGizmoAktiv)
        {
            if (_felsGizmo is { Visible: true }) _felsGizmo.Visible = false;
            if (_gizmoZug is not null) FelsGizmoAbbrechen(); // der Zug endet ohne Befehl (Werkzeug gewechselt, Auswahl leer): Instanzen zurück
            _gizmoZug = null;
            FelsMarkenAktualisieren();
            return;
        }
        if (_gizmoGebaut != FelsGizmoModus) FelsGizmoBauen(FelsGizmoModus);
        var maus = GetViewport().GetMousePosition();
        if (_gizmoZug is { } zug) GizmoZugNachfuehren(zug, maus);
        var (mitte, ausrichtung) = _gizmoZug is { } z ? (z.Mitte + GizmoVerschiebung(z), z.Ausrichtung) : GizmoLage();
        var groesse = GizmoPixel * GizmoMeterJePixel(mitte);
        _felsGizmo!.Visible = true;
        _felsGizmo.Transform = new Transform3D(ausrichtung * Basis.FromScale(Vector3.One * groesse), mitte);
        // Aufhellen, was unter der Maus liegt (oder gezogen wird)
        var hell = _gizmoZug?.Griff ?? (GetViewport().GuiGetHoveredControl() is null ? GizmoGriffBei(maus) : null);
        if (!ReferenceEquals(hell, _gizmoHover))
        {
            GizmoAufhellen(_gizmoHover, false);
            GizmoAufhellen(hell, true);
            _gizmoHover = hell;
        }
        FelsMarkenAktualisieren();
    }

    private Vector3 GizmoVerschiebung(GizmoZug z) => z.Griff.Art is GriffArt.Achse or GriffArt.Ebene && FelsGizmoModus == FelsGizmoArt.Verschieben && z.Aenderung is { } a
        ? new Vector3((float)a.DX, (float)a.DY, (float)a.DZ) : Vector3.Zero;

    private void GizmoAufhellen(GizmoGriff? griff, bool hell)
    {
        if (griff is null) return;
        var teile = new List<MeshInstance3D> { griff.Knoten };
        if (_gizmoNebenteile.TryGetValue(griff.Knoten, out var neben)) teile.AddRange(neben);
        foreach (var t in teile)
        {
            if (!IsInstanceValid(t)) continue;
            t.MaterialOverride = hell ? GizmoHell : t.Mesh is QuadMesh ? GizmoQuadratStoff(griff.Achse) : GizmoStoff(griff.Art == GriffArt.Mitte ? -1 : griff.Achse);
        }
    }

    /// <summary>Goldene Ringe unter den gewählten Stücken, die nicht das erste sind (das erste hat <see cref="FelsMarkieren"/>).</summary>
    private void FelsMarkenAktualisieren()
    {
        var weitere = FelsGizmoAktiv && _felsAuswahl.Count > 1 ? _felsAuswahl.Where(f => !ReferenceEquals(f, WerkstattGewaehlt)).ToList() : new List<EntwurfFels>();
        while (_felsMarken.Count < weitere.Count)
        {
            _felsMarkenStoff ??= ImmerSichtbar(Transparent(Auswahlgold.Lightened(0.15f), 0.8f));
            var neu = new MeshInstance3D { Name = "FelsMarkierungWeitere", MaterialOverride = _felsMarkenStoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(neu);
            _felsMarken.Add(neu);
        }
        for (var i = 0; i < _felsMarken.Count; i++)
        {
            if (i >= weitere.Count)
            {
                _felsMarken[i].Visible = false;
                continue;
            }
            var f = weitere[i];
            if (Felsformen.Finde(f.Form) is not { } form) continue;
            // auf 0,1 m gerundet und geteilt: Bei Hunderten gewählter Stücke (Rahmen, Strg+A) gibt es so nur wenige Netze
            var radius = Mathf.Snapped(Mathf.Max(0.8f, (float)(Math.Max(form.Breite * f.AchseX, form.Tiefe * f.AchseZ) / 2 * f.Massstab)), 0.1f);
            if (_felsMarken[i].Mesh is not TorusMesh ring || !Mathf.IsEqualApprox(ring.InnerRadius, radius))
            {
                var schluessel = Mathf.RoundToInt(radius * 10);
                if (!_felsMarkenNetze.TryGetValue(schluessel, out var netz))
                    _felsMarkenNetze[schluessel] = netz = new TorusMesh { InnerRadius = radius, OuterRadius = radius + 0.3f, Rings = 32 };
                _felsMarken[i].Mesh = netz;
            }
            _felsMarken[i].Position = new Vector3((float)f.X, BodenHoehe((float)f.X, (float)f.Z) + 0.2f, (float)f.Z);
            _felsMarken[i].Visible = true;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Strahltest
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Der Griff unter der Maus (der vorderste Treffer des Strahls), sonst null.</summary>
    private GizmoGriff? GizmoGriffBei(Vector2 maus)
    {
        if (_felsGizmo is null || !FelsGizmoAktiv || _gizmoGebaut != FelsGizmoModus || _gizmoGriffe.Count == 0 || _entwurf is null) return null;
        var (mitte, ausrichtung) = GizmoLage();
        var groesse = GizmoPixel * GizmoMeterJePixel(mitte);
        var kamera = StadtKamera;
        // in den Maßen des Gizmos (Länge einer Achse = 1), Achsen = Koordinatenachsen
        var zurueck = ausrichtung.Transposed();
        var o = zurueck * (kamera.ProjectRayOrigin(maus) - mitte) / groesse;
        var d = zurueck * kamera.ProjectRayNormal(maus) / groesse;
        var toleranz = 12f / GizmoPixel;
        GizmoGriff? beste = null;
        var naechst = float.MaxValue;
        foreach (var g in _gizmoGriffe)
        {
            float? t = g.Art switch
            {
                GriffArt.Mitte => StrahlKugel(o, d, 0.17f),
                GriffArt.Achse => TrefferAchse(o, d, g.Achse, toleranz),
                GriffArt.Ebene => TrefferEbene(o, d, g.Achse),
                _ => TrefferRing(o, d, g.Achse, toleranz * 1.2f),
            };
            if (t is not { } tt) continue;
            // ein Würfel in der Mitte schlägt die Achsen; sonst zählt der vorderste Treffer, Ebenen etwas weniger als Achsen
            var gewicht = g.Art == GriffArt.Mitte ? tt - 100f : g.Art == GriffArt.Ebene ? tt + 0.001f : tt;
            if (gewicht < naechst) (beste, naechst) = (g, gewicht);
        }
        return beste;
    }

    private static float? StrahlKugel(Vector3 o, Vector3 d, float r)
    {
        var a = d.Dot(d);
        var b = 2 * o.Dot(d);
        var c = o.Dot(o) - r * r;
        var disk = b * b - 4 * a * c;
        if (disk < 0) return null;
        return (-b - Mathf.Sqrt(disk)) / (2 * a);
    }

    /// <summary>Abstand zweier Geraden (o + t·d, p + s·e) und der Parameter t auf der ersten.</summary>
    private static (float Abstand, float T, float S) Geraden(Vector3 o, Vector3 d, Vector3 p, Vector3 e)
    {
        var w = o - p;
        float a = d.Dot(d), b = d.Dot(e), c = e.Dot(e), dd = d.Dot(w), ee = e.Dot(w);
        var nenner = a * c - b * b;
        float t, s;
        if (Mathf.Abs(nenner) < 1e-9f)
        {
            t = 0;
            s = ee / c;
        }
        else
        {
            t = (b * ee - c * dd) / nenner;
            s = (a * ee - b * dd) / nenner;
        }
        return (((o + d * t) - (p + e * s)).Length(), t, s);
    }

    private static float? TrefferAchse(Vector3 o, Vector3 d, int achse, float toleranz)
    {
        var (abstand, t, s) = Geraden(o, d, Vector3.Zero, Einheit(achse));
        if (t < 0 || s < 0.08f || s > 1.05f) return null;
        return abstand <= toleranz ? t : null;
    }

    private static float? TrefferEbene(Vector3 o, Vector3 d, int achse)
    {
        var n = Einheit(achse);
        var nenner = d.Dot(n);
        if (Mathf.Abs(nenner) < 1e-6f) return null;
        var t = -o.Dot(n) / nenner;
        if (t < 0) return null;
        var p = o + d * t;
        var (u, v) = ((achse + 1) % 3, (achse + 2) % 3);
        return p[u] is >= 0.28f and <= 0.56f && p[v] is >= 0.28f and <= 0.56f ? t : null;
    }

    private static float? TrefferRing(Vector3 o, Vector3 d, int achse, float toleranz)
    {
        var n = Einheit(achse);
        var nenner = d.Dot(n);
        if (Mathf.Abs(nenner) < 1e-6f) return null;
        var t = -o.Dot(n) / nenner;
        if (t < 0) return null;
        var p = o + d * t;
        return Mathf.Abs(p.Length() - 0.9f) <= toleranz ? t : null;
    }

    /// <summary>Wo der Strahl die Ebene durch <paramref name="punkt"/> mit der Normalen <paramref name="n"/> trifft.</summary>
    private static Vector3? EbenenTreffer(Vector3 punkt, Vector3 n, Vector3 von, Vector3 richtung)
    {
        var nenner = richtung.Dot(n);
        if (Mathf.Abs(nenner) < 1e-5f) return null;
        var t = (punkt - von).Dot(n) / nenner;
        return von + richtung * t;
    }

    /// <summary>Der Parameter auf der Geraden (punkt + t·achse), der dem Strahl am nächsten liegt (Meter).</summary>
    private static float ParameterAufGerader(Vector3 punkt, Vector3 achse, Vector3 von, Vector3 richtung) =>
        Geraden(von, richtung, punkt, achse.Normalized()).S;

    // ═════════════════════════════════════════════════════════════════════════
    //  Der Zug
    // ═════════════════════════════════════════════════════════════════════════

    private static double Runden(double wert, double schritt) => Math.Round(wert / schritt) * schritt;

    /// <summary>Rechnet aus der Mausstellung die Änderung, zeigt die Vorschau (Kern, ohne Befehl) und stellt die Instanzen.</summary>
    private void GizmoZugNachfuehren(GizmoZug zug, Vector2 maus)
    {
        if (_entwurf is null) return;
        var raster = FelsGizmoRastet || Input.IsKeyPressed(Key.Ctrl);
        var kamera = StadtKamera;
        var von = kamera.ProjectRayOrigin(maus);
        var richtung = kamera.ProjectRayNormal(maus);
        var g = zug.Griff;
        var achse = zug.Ausrichtung[g.Achse].Normalized();
        Felsaenderung? a = null;
        switch (FelsGizmoModus)
        {
            case FelsGizmoArt.Verschieben:
            {
                Vector3 delta = Vector3.Zero;
                if (g.Art == GriffArt.Achse)
                {
                    var t = ParameterAufGerader(zug.Mitte, achse, von, richtung);
                    var weg = (double)(t - zug.T0);
                    delta = achse * (float)(raster ? Runden(weg, GizmoRasterWeg) : weg);
                }
                else if (EbenenTreffer(zug.Mitte, achse, von, richtung) is { } p)
                {
                    delta = p - zug.P0;
                    if (raster)
                    {
                        // in den Achsen des Gizmos runden (bei Weltachsen: x, y, z der Welt)
                        var lokal = zug.Ausrichtung.Transposed() * delta;
                        lokal = new Vector3((float)Runden(lokal.X, GizmoRasterWeg), (float)Runden(lokal.Y, GizmoRasterWeg), (float)Runden(lokal.Z, GizmoRasterWeg));
                        delta = zug.Ausrichtung * lokal;
                    }
                }
                a = new Felsaenderung(DX: Math.Round(delta.X, 3), DY: Math.Round(delta.Y, 3), DZ: Math.Round(delta.Z, 3));
                break;
            }
            case FelsGizmoArt.Drehen:
            {
                if (EbenenTreffer(zug.Mitte, achse, von, richtung) is { } p && zug.P0 != zug.Mitte)
                {
                    var (v0, v1) = ((zug.P0 - zug.Mitte).Normalized(), (p - zug.Mitte).Normalized());
                    var jetzt = (double)Mathf.RadToDeg(Mathf.Atan2(achse.Dot(v0.Cross(v1)), v0.Dot(v1)));
                    var schritt = jetzt - zug.WinkelLetzt;
                    schritt -= 360 * Math.Round(schritt / 360); // auf ±180 gebracht
                    zug.WinkelSumme += schritt;
                    zug.WinkelLetzt = jetzt;
                    var winkel = zug.WinkelSumme;
                    var grad = raster ? Runden(winkel, GizmoRasterWinkel) : Math.Round(winkel, 1);
                    var welt = FelsGizmoWelt;
                    a = new Felsaenderung(Drehachse: g.Achse switch { 0 => Felsachse.X, 1 => Felsachse.Y, _ => Felsachse.Z }, Winkel: grad,
                        UmMitte: welt, Weltachsen: welt);
                }
                break;
            }
            default:
            {
                double faktor;
                if (g.Art == GriffArt.Mitte) faktor = Math.Clamp(1 + (zug.Maus.Y - maus.Y) / 150.0, 0.1, 10);
                else
                {
                    var t = ParameterAufGerader(zug.Mitte, achse, von, richtung);
                    faktor = Math.Clamp(t / zug.T0, 0.05, 20);
                }
                faktor = raster ? Math.Max(GizmoRasterFaktor, Runden(faktor, GizmoRasterFaktor)) : Math.Round(faktor, 3);
                var (fx, fy, fz) = g.Art == GriffArt.Mitte ? (faktor, faktor, faktor) : (g.Achse == 0 ? faktor : 1, g.Achse == 1 ? faktor : 1, g.Achse == 2 ? faktor : 1);
                a = new Felsaenderung(FaktorX: fx, FaktorY: fy, FaktorZ: fz);
                break;
            }
        }
        zug.Aenderung = a;
        if (a is null) return;
        var auswahl = _felsAuswahl.ToList();
        IReadOnlyList<(EntwurfFels Fels, Felslage Lage)> vorschau;
        try
        {
            vorschau = _entwurf.FelsVorschau(auswahl, a);
        }
        catch (ArgumentException)
        {
            return;
        }
        foreach (var (f, l) in vorschau)
        {
            if (!_felsInstanz.TryGetValue(f, out var i)) continue;
            var b = new Felsbild(f.Form, _entwurf.GesteinVon(f), l.X, l.Z, l.Drehung, l.Massstab, l.Neigung, l.Einsinken, l.KippenX, l.KippenZ, l.AchseX, l.AchseY, l.AchseZ, l.Versatz);
            i.Vielfach.SetInstanceTransform(i.Index, FelsLage(b));
            i.Vielfach.SetInstanceCustomData(i.Index, FelsDaten(b));
        }
    }

    /// <summary>Für Startoptionen: wählt Felsstücke (das letzte ist das gewählte).</summary>
    public void WerkstattFelsWaehlen(IReadOnlyList<EntwurfFels> stuecke) => FelsMehrfachWaehlen(stuecke, dazu: false); // Stadtansicht.Felsauswahl.cs

    /// <summary>Für Startoptionen und Klicktests: ein Zug am Gizmo, ohne Maus (<paramref name="art"/> Verschieben, Drehen, Skalieren; ein Schritt).</summary>
    public bool WerkstattFelsGizmo(FelsGizmoArt art, Felsaenderung aenderung)
    {
        if (_entwurf is null || _felsAuswahl.Count == 0) return false;
        FelsGizmoModusSetzen(art);
        var auswahl = _felsAuswahl.ToList();
        return Befehl<bool>(() =>
        {
            _entwurf.FelsTransformieren(auswahl, aenderung);
            return true;
        });
    }
}
