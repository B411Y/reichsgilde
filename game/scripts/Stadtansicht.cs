using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Die 2,5D-Stadtansicht als „Greybox": Alle Gebäude bestehen aus einfachen Grundformen
/// (Quader, Prismen, Zylinder). So lässt sich Aufbau, Kamera und Zusammenspiel mit der
/// Simulation erproben, bevor echte Grafiken entstehen. Später werden die Grundformen
/// einfach durch Modelle ersetzt – die Logik bleibt.
///
/// Was diese Ansicht zeigt (alles kommt aus dem Simulationskern):
///   • Karren fahren entlang der Wege zum Markt und zurück
///   • Sonne, Himmel und Licht folgen der Spieluhr
///   • Die Esse einer Gießerei glüht, solange dort gearbeitet wird
///   • Gebäude lassen sich anklicken (Ereignis <see cref="GebaeudeAngeklickt"/>)
///
/// Steuerung: Mausrad = Zoom, rechte Maustaste ziehen oder WASD/Pfeiltasten = verschieben,
/// Q/E = um 90° drehen, Linksklick = Gebäude auswählen.
/// </summary>
public partial class Stadtansicht : Node3D
{
    // ── Farben der Greybox (gedämpft, bodenständig) ─────────────────────────
    private static readonly Color Wiese = new(0.33f, 0.40f, 0.24f);
    private static readonly Color Pflaster = new(0.47f, 0.44f, 0.40f);
    private static readonly Color Marktpflaster = new(0.55f, 0.51f, 0.46f);
    private static readonly Color Weg = new(0.45f, 0.36f, 0.26f);
    private static readonly Color Mauerstein = new(0.52f, 0.49f, 0.45f);
    private static readonly Color Putz = new(0.80f, 0.75f, 0.64f);
    private static readonly Color Fachwerk = new(0.62f, 0.55f, 0.45f);
    private static readonly Color Ziegel = new(0.55f, 0.25f, 0.18f);
    private static readonly Color Schiefer = new(0.28f, 0.30f, 0.34f);
    private static readonly Color Fels = new(0.40f, 0.37f, 0.34f);
    private static readonly Color Tanne = new(0.16f, 0.27f, 0.17f);
    private static readonly Color Holz = new(0.36f, 0.25f, 0.16f);
    private static readonly Color Spieler = new(0.20f, 0.42f, 0.72f);
    private static readonly Color Rivale = new(0.70f, 0.20f, 0.16f);

    private const float KameraMinZoom = 25f;
    private const float KameraMaxZoom = 140f;

    private Simulation? _simulation;
    private Node3D _kameraDrehung = null!;
    private Camera3D _kamera = null!;
    private DirectionalLight3D _sonne = null!;
    private WorldEnvironment _umgebung = null!;
    private float _zielDrehung = 45f;
    private bool _ziehen;

    private readonly Dictionary<Betrieb, BetriebsAnsicht> _betriebe = new();
    private readonly Dictionary<Karren, Node3D> _karren = new();
    private readonly List<Label3D> _beschriftungen = new();
    private const float BezugsZoom = 95f;

    /// <summary>Wird ausgelöst, wenn ein Gebäude angeklickt wird. Übergeben wird seine Kennung.</summary>
    public event Action<string>? GebaeudeAngeklickt;

    /// <summary>Darstellung eines Betriebs: Grundstück, Wegstrecke zum Markt und Esse.</summary>
    private sealed record BetriebsAnsicht(Vector3 Hof, Vector3[] WegZumMarkt, OmniLight3D? Glut, StandardMaterial3D? Esse, bool Spieler);

    // Feste Punkte der Stadt (Meter). Der Marktplatz liegt im Ursprung.
    public static readonly Vector3 Marktplatz = Vector3.Zero;
    public static readonly Vector3 Osttor = new(36, 0, 0);

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        UmgebungUndLicht();
        KameraAufbauen();

        Flaeche(new Vector3(20, 0, 0), new Vector2(240, 200), Wiese, -0.05f);
        Flaeche(Vector3.Zero, new Vector2(72, 72), Pflaster, 0f);
        Flaeche(Vector3.Zero, new Vector2(24, 24), Marktpflaster, 0.02f);

        Stadtmauer();
        Marktstaende();
        Rathaus(new Vector3(0, 0, -20));
        Dom(new Vector3(-20, 0, -14));
        Wohnhaeuser();
        Wald();

        // Wege: Ostgasse zum Tor und hinaus zur Grube
        WegZiehen(Marktplatz, Osttor, 5);
        WegZiehen(Osttor, new Vector3(56, 0, -2), 4);
    }

    private void UmgebungUndLicht()
    {
        var umgebung = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.47f, 0.60f, 0.72f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.75f, 0.75f, 0.80f),
            AmbientLightEnergy = 0.5f,
        };
        _umgebung = new WorldEnvironment { Environment = umgebung };
        AddChild(_umgebung);

        _sonne = new DirectionalLight3D { ShadowEnabled = true, LightEnergy = 1.1f };
        _sonne.RotationDegrees = new Vector3(-50, 30, 0);
        AddChild(_sonne);
    }

    private void KameraAufbauen()
    {
        // Drehung (um die Hochachse) → Neigung (35° schräg von oben) → Kamera mit Abstand.
        _kameraDrehung = new Node3D { Name = "Kamerafuehrung", RotationDegrees = new Vector3(0, _zielDrehung, 0) };
        AddChild(_kameraDrehung);
        var neigung = new Node3D { RotationDegrees = new Vector3(-35, 0, 0) };
        _kameraDrehung.AddChild(neigung);
        _kamera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal, // keine Fluchtpunkte = klassischer 2,5D-Look
            Size = 95,
            Position = new Vector3(0, 0, 150),
            Far = 400,
        };
        neigung.AddChild(_kamera);
        _kameraDrehung.Position = new Vector3(24, 0, -6); // Stadt und Grube im Blick
    }

    private void Stadtmauer()
    {
        const float rand = 36, hoehe = 6, dicke = 2;
        var stein = Material(Mauerstein);
        // Nord, Süd, West als ganze Mauern; Ost mit Tor in der Mitte.
        Quader(new Vector3(0, hoehe / 2, -rand), new Vector3(2 * rand, hoehe, dicke), stein);
        Quader(new Vector3(0, hoehe / 2, rand), new Vector3(2 * rand, hoehe, dicke), stein);
        Quader(new Vector3(-rand, hoehe / 2, 0), new Vector3(dicke, hoehe, 2 * rand), stein);
        Quader(new Vector3(rand, hoehe / 2, -20), new Vector3(dicke, hoehe, 32), stein);
        Quader(new Vector3(rand, hoehe / 2, 20), new Vector3(dicke, hoehe, 32), stein);

        foreach (var ecke in new[] { new Vector3(-rand, 0, -rand), new Vector3(rand, 0, -rand), new Vector3(-rand, 0, rand), new Vector3(rand, 0, rand) })
            Turm(ecke, 3.5f, 10, Schiefer);

        // Torhaus über dem Osttor
        Quader(new Vector3(rand, 8, 0), new Vector3(5, 4, 9), stein);
        Turm(new Vector3(rand, 0, -5.5f), 2.6f, 11, Schiefer);
        Turm(new Vector3(rand, 0, 5.5f), 2.6f, 11, Schiefer);
        Klickflaeche("Osttor", new Vector3(rand, 5, 0), new Vector3(6, 10, 12));
        Beschriftung("Osttor", new Vector3(rand, 15, 0), 48);
    }

    private void Marktstaende()
    {
        var planen = new[] { new Color(0.72f, 0.62f, 0.38f), new Color(0.55f, 0.20f, 0.18f), new Color(0.25f, 0.38f, 0.30f), new Color(0.62f, 0.55f, 0.44f) };
        var i = 0;
        foreach (var (x, z) in new[] { (-8f, -8f), (0f, -9f), (8f, -8f), (-9f, 2f), (9f, 2f), (-4f, 8f), (4f, 8f) })
        {
            var p = new Vector3(x, 0, z);
            Quader(p + new Vector3(0, 0.6f, 0), new Vector3(3, 1.2f, 2), Material(Holz));
            Dach(p + new Vector3(0, 2.4f, 0), new Vector3(3.6f, 1f, 2.6f), Material(planen[i++ % planen.Length]), quer: true);
        }
        // Brunnen
        var brunnen = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 1.6f, BottomRadius = 1.8f, Height = 1f }, MaterialOverride = Material(Mauerstein) };
        brunnen.Position = new Vector3(0, 0.5f, 0);
        AddChild(brunnen);
        Klickflaeche("Marktplatz", new Vector3(0, 1, 0), new Vector3(22, 2, 22));
        Beschriftung("Marktplatz", new Vector3(0, 6, 0), 56);
    }

    private void Rathaus(Vector3 p)
    {
        Haus(p, new Vector3(16, 9, 10), Putz, Schiefer, quer: true);
        Turm(p + new Vector3(7, 0, 3), 2.2f, 20, Schiefer, eckig: true);
        Klickflaeche("Rathaus", p + new Vector3(0, 5, 0), new Vector3(16, 10, 10));
        Beschriftung("Rathaus", p + new Vector3(0, 18, 0), 56);
    }

    private void Dom(Vector3 p)
    {
        Haus(p, new Vector3(10, 12, 22), Mauerstein, Schiefer, quer: false);
        Turm(p + new Vector3(0, 0, 13), 3.4f, 30, Schiefer, eckig: true);
        Klickflaeche("Dom", p + new Vector3(0, 6, 2), new Vector3(10, 12, 26));
        Beschriftung("Dom", p + new Vector3(0, 34, 13), 56);
    }

    private void Wohnhaeuser()
    {
        // Feste Liste – jede Partie sieht gleich aus. Später erzeugt der Stadtplan die Häuser.
        var haeuser = new (float X, float Z, bool Quer)[]
        {
            (-29, -29, true), (-20, -29, true), (-11, -29, true), (10, -29, true), (19, -29, true), (28, -29, true),
            (-29, 29, true), (-4, 29, true), (5, 29, true), (29, 29, true),
            (-29, 6, false), (-29, -6, false), (29, -12, false), (29, 10, false),
            (14, -18, false), (-29, 18, false),
        };
        var i = 0;
        foreach (var (x, z, quer) in haeuser)
        {
            var wand = i % 3 == 0 ? Fachwerk : Putz;
            var dach = i % 4 == 0 ? Schiefer : Ziegel;
            Haus(new Vector3(x, 0, z), new Vector3(7, 6 + i % 3, 6), wand, dach, quer);
            i++;
        }
    }

    private void Wald()
    {
        // Deterministischer „Zufall" – gleiche Bäume bei jedem Start.
        var zufall = new Random(1400);
        for (var i = 0; i < 70; i++)
        {
            var winkel = zufall.NextDouble() * Math.Tau;
            var abstand = 48 + zufall.NextDouble() * 50;
            var p = new Vector3((float)(Math.Cos(winkel) * abstand) + 10, 0, (float)(Math.Sin(winkel) * abstand));
            if (p.DistanceTo(new Vector3(74, 0, -14)) < 22 || Mathf.Abs(p.Z) < 6 && p.X > 30) continue; // Grube und Straße frei lassen
            var hoehe = 6 + (float)zufall.NextDouble() * 5;
            var baum = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = 2.2f, Height = hoehe, RadialSegments = 7 },
                MaterialOverride = Material(Tanne),
                Position = p + new Vector3(0, hoehe / 2, 0),
            };
            AddChild(baum);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Verbindung zur Simulation
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Stellt die Betriebe der Simulation in die Stadt.
    /// </summary>
    /// <param name="plaetze">Betrieb → Grundstück (Mittelpunkt) und ob er dem Spieler gehört.</param>
    public void Verbinden(Simulation simulation, IEnumerable<(Betrieb Betrieb, Vector3 Grundstueck, bool Spieler)> plaetze)
    {
        _simulation = simulation;
        foreach (var (betrieb, platz, spieler) in plaetze)
        {
            var farbe = spieler ? Spieler : Rivale;
            var ansicht = betrieb.Art == Betriebsart.Mine
                ? MineBauen(betrieb, platz, farbe)
                : GiessereiBauen(betrieb, platz, farbe);
            _betriebe[betrieb] = ansicht with { Spieler = spieler };
        }
    }

    private BetriebsAnsicht GiessereiBauen(Betrieb betrieb, Vector3 p, Color besitzer)
    {
        Haus(p, new Vector3(10, 6, 8), Fachwerk, Ziegel, quer: true);
        // Wappenfahne in der Besitzerfarbe
        Quader(p + new Vector3(-5.6f, 5, 0), new Vector3(0.2f, 3, 2), Material(besitzer));
        // Esse mit Glut
        var esse = Material(new Color(0.3f, 0.28f, 0.27f));
        esse.EmissionEnabled = true;
        esse.Emission = new Color(1f, 0.45f, 0.1f);
        esse.EmissionEnergyMultiplier = 0;
        Quader(p + new Vector3(3.5f, 6, 2.5f), new Vector3(1.6f, 12, 1.6f), Material(Mauerstein));
        Quader(p + new Vector3(3.5f, 12.3f, 2.5f), new Vector3(1.8f, 0.6f, 1.8f), esse);
        var glut = new OmniLight3D { LightColor = new Color(1f, 0.55f, 0.2f), OmniRange = 12, LightEnergy = 0, Position = p + new Vector3(3.5f, 13.5f, 2.5f) };
        AddChild(glut);

        Klickflaeche(BetriebsKennung(betrieb), p + new Vector3(0, 4, 0), new Vector3(10, 8, 8));
        Beschriftung(betrieb.Name, p + new Vector3(0, 11, 0), 44);

        // Weg zur nächsten Gasse und dann zum Markt
        var gasse = new Vector3(p.X * 0.45f, 0, p.Z * 0.45f);
        WegZiehen(p, gasse, 3.5f);
        WegZiehen(gasse, Marktplatz, 3.5f);
        return new BetriebsAnsicht(p + new Vector3(0, 0, 5.5f), new[] { p + new Vector3(0, 0, 5.5f), gasse, Marktplatz }, glut, esse, false);
    }

    private BetriebsAnsicht MineBauen(Betrieb betrieb, Vector3 p, Color besitzer)
    {
        // Berg aus zwei abgeflachten Kugeln
        foreach (var (versatz, radius) in new[] { (new Vector3(10, 0, -8), 18f), (new Vector3(20, 0, 4), 13f) })
        {
            var berg = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = radius, Height = radius * 1.3f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = Material(Fels),
                Position = p + versatz,
            };
            AddChild(berg);
        }
        Quader(p + new Vector3(2, 2, 0), new Vector3(3, 4, 4), Material(new Color(0.08f, 0.07f, 0.07f))); // Stolleneingang
        Haus(p + new Vector3(-6, 0, 6), new Vector3(6, 4, 5), Holz, Schiefer, quer: true);                // Schuppen
        Quader(p + new Vector3(-9.2f, 3.5f, 6), new Vector3(0.2f, 3, 2), Material(besitzer));

        Klickflaeche(BetriebsKennung(betrieb), p + new Vector3(2, 4, 0), new Vector3(16, 8, 14));
        Beschriftung(betrieb.Name, p + new Vector3(2, 9, 0), 44);

        var hof = p + new Vector3(-2, 0, 0);
        WegZiehen(new Vector3(56, 0, -2), hof, 4);
        return new BetriebsAnsicht(hof, new[] { hof, new Vector3(56, 0, -2), Osttor, Marktplatz }, null, null, false);
    }

    public static string BetriebsKennung(Betrieb b) => "betrieb:" + b.Name;

    /// <summary>Jedes Bild: Licht nach Uhrzeit, Esse, Karren.</summary>
    public void Aktualisieren(double delta)
    {
        if (_simulation is null) return;
        var zeit = _simulation.Zeit;
        LichtNachUhrzeit(zeit.UhrzeitInMinuten);

        if (_simulation.Wirtschaft is not { } w) return;
        var arbeitszeit = w.IstArbeitszeit(zeit);

        foreach (var (betrieb, ansicht) in _betriebe)
        {
            var arbeitet = arbeitszeit && betrieb.Belegt > 0 && betrieb.Stillstand is null;
            if (ansicht.Glut is not null) ansicht.Glut.LightEnergy = arbeitet ? 3f : 0f;
            if (ansicht.Esse is not null) ansicht.Esse.EmissionEnergyMultiplier = arbeitet ? 2.5f : 0f;

            var fahrzeit = w.Daten.Logistik.FahrzeitAb(betrieb.Ort);
            for (var i = 0; i < betrieb.Karren.Count; i++)
                KarrenBewegen(betrieb.Karren[i], i, ansicht, fahrzeit, zeit, delta, ansicht.Spieler);
        }
    }

    private void KarrenBewegen(Karren karren, int nummer, BetriebsAnsicht ansicht, int fahrzeit, Spielzeit zeit, double delta, bool spieler)
    {
        if (!_karren.TryGetValue(karren, out var knoten))
        {
            knoten = KarrenModell(spieler ? Spieler : Rivale);
            knoten.Position = ansicht.Hof;
            _karren[karren] = knoten;
        }

        Vector3 ziel;
        Vector3 blick;
        if (karren.Zustand == KarrenZustand.AmBetrieb)
        {
            ziel = ansicht.Hof + new Vector3(nummer * 3f, 0, 0);
            blick = ansicht.WegZumMarkt[1];
        }
        else
        {
            var rest = Math.Clamp((karren.Ankunft - zeit.VergangeneMinuten) / (double)fahrzeit, 0, 1);
            var anteil = karren.Zustand == KarrenZustand.ZumMarkt ? 1 - rest : rest;
            ziel = PunktAufWeg(ansicht.WegZumMarkt, (float)anteil);
            var weiter = PunktAufWeg(ansicht.WegZumMarkt, (float)Math.Clamp(anteil + (karren.Zustand == KarrenZustand.ZumMarkt ? 0.02 : -0.02), 0, 1));
            blick = weiter;
        }

        // Weich hinterhergleiten, damit Minutensprünge nicht ruckeln.
        knoten.Position = knoten.Position.Lerp(ziel, (float)Math.Min(1, delta * 6));
        var richtung = blick - knoten.Position;
        richtung.Y = 0;
        if (richtung.LengthSquared() > 0.01f)
            knoten.Rotation = new Vector3(0, Mathf.Atan2(richtung.X, richtung.Z), 0);

        // Ladung sichtbar machen, Eskorte als Begleiter
        knoten.GetNode<Node3D>("Ladung").Visible = karren.Ladung.Count > 0;
        knoten.GetNode<Node3D>("Eskorte").Visible = karren.Eskorte != "Keine" && karren.Ladung.Count > 0;
    }

    private static Vector3 PunktAufWeg(Vector3[] weg, float anteil)
    {
        var laengen = new float[weg.Length - 1];
        float gesamt = 0;
        for (var i = 0; i < laengen.Length; i++) { laengen[i] = weg[i].DistanceTo(weg[i + 1]); gesamt += laengen[i]; }
        var strecke = anteil * gesamt;
        for (var i = 0; i < laengen.Length; i++)
        {
            if (strecke <= laengen[i]) return weg[i].Lerp(weg[i + 1], laengen[i] > 0 ? strecke / laengen[i] : 0);
            strecke -= laengen[i];
        }
        return weg[^1];
    }

    private Node3D KarrenModell(Color besitzer)
    {
        var karren = new Node3D();
        AddChild(karren);
        karren.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.8f, 2.6f) }, MaterialOverride = Material(Holz), Position = new Vector3(0, 1.1f, 0) });
        foreach (var x in new[] { -0.95f, 0.95f })
        {
            var rad = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.6f, Height = 0.2f }, MaterialOverride = Material(new Color(0.2f, 0.15f, 0.1f)) };
            rad.Position = new Vector3(x, 0.6f, 0);
            rad.RotationDegrees = new Vector3(0, 0, 90);
            karren.AddChild(rad);
        }
        var ladung = new MeshInstance3D { Name = "Ladung", Mesh = new BoxMesh { Size = new Vector3(1.3f, 0.7f, 2f) }, MaterialOverride = Material(besitzer), Position = new Vector3(0, 1.85f, 0) };
        karren.AddChild(ladung);
        var eskorte = new MeshInstance3D { Name = "Eskorte", Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.8f }, MaterialOverride = Material(new Color(0.35f, 0.35f, 0.38f)), Position = new Vector3(1.8f, 0.9f, 0.6f) };
        karren.AddChild(eskorte);
        return karren;
    }

    // ── Tag und Nacht ───────────────────────────────────────────────────────

    private void LichtNachUhrzeit(int uhrzeitInMinuten)
    {
        var stunde = uhrzeitInMinuten / 60f;
        var tag = Mathf.Clamp(Mathf.Sin((stunde - 6f) / 12f * Mathf.Pi), 0f, 1f); // 0 nachts, 1 mittags

        if (stunde is >= 6 and <= 18)
        {
            // Sonne wandert von Osten (+X) über Süden nach Westen.
            _sonne.RotationDegrees = new Vector3(-(8 + 52 * tag), 90 - (stunde - 6) / 12f * 180, 0);
            _sonne.LightColor = new Color(1f, 0.82f, 0.62f).Lerp(new Color(1f, 0.97f, 0.9f), tag);
            _sonne.LightEnergy = 0.35f + 0.85f * tag;
        }
        else
        {
            // Mondlicht
            _sonne.RotationDegrees = new Vector3(-45, 20, 0);
            _sonne.LightColor = new Color(0.55f, 0.62f, 0.85f);
            _sonne.LightEnergy = 0.18f;
        }

        var umgebung = _umgebung.Environment;
        umgebung.BackgroundColor = Himmelsfarbe(uhrzeitInMinuten);
        umgebung.AmbientLightColor = new Color(0.35f, 0.38f, 0.55f).Lerp(new Color(0.85f, 0.83f, 0.78f), tag);
        umgebung.AmbientLightEnergy = 0.35f + 0.35f * tag;
    }

    // Stützpunkte für den Himmel: (Uhrzeit in Minuten, Farbe).
    private static readonly (int Minute, Color Farbe)[] HimmelsVerlauf =
    {
        (0 * 60,  new Color(0.04f, 0.05f, 0.10f)),
        (5 * 60,  new Color(0.10f, 0.10f, 0.18f)),
        (6 * 60,  new Color(0.65f, 0.48f, 0.38f)),
        (9 * 60,  new Color(0.52f, 0.66f, 0.78f)),
        (14 * 60, new Color(0.55f, 0.69f, 0.82f)),
        (18 * 60, new Color(0.70f, 0.45f, 0.30f)),
        (21 * 60, new Color(0.14f, 0.12f, 0.20f)),
        (24 * 60, new Color(0.04f, 0.05f, 0.10f)),
    };

    public static Color Himmelsfarbe(int uhrzeitInMinuten)
    {
        for (var i = 0; i < HimmelsVerlauf.Length - 1; i++)
        {
            var (von, farbeVon) = HimmelsVerlauf[i];
            var (bis, farbeBis) = HimmelsVerlauf[i + 1];
            if (uhrzeitInMinuten < bis)
                return farbeVon.Lerp(farbeBis, (float)(uhrzeitInMinuten - von) / (bis - von));
        }
        return HimmelsVerlauf[0].Farbe;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kamera und Auswahl
    // ═════════════════════════════════════════════════════════════════════════

    public override void _Process(double delta)
    {
        // Beschriftungen mitskalieren, damit sie bei jedem Zoom gleich groß wirken.
        var massstab = _kamera.Size / BezugsZoom;
        foreach (var label in _beschriftungen)
            label.Scale = Vector3.One * massstab;

        // Sanft auf die Zieldrehung zudrehen (Q/E)
        var aktuell = _kameraDrehung.RotationDegrees.Y;
        _kameraDrehung.RotationDegrees = new Vector3(0, Mathf.LerpAngle(Mathf.DegToRad(aktuell), Mathf.DegToRad(_zielDrehung), (float)Math.Min(1, delta * 8)) * 180 / Mathf.Pi, 0);

        // Verschieben mit Tastatur
        var richtung = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) richtung.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) richtung.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) richtung.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) richtung.X += 1;
        if (richtung != Vector2.Zero)
            Verschieben(richtung.Normalized() * (float)delta * _kamera.Size * 0.8f);
    }

    private void Verschieben(Vector2 bildschirm)
    {
        var basis = _kameraDrehung.GlobalTransform.Basis;
        var rechts = new Vector3(basis.X.X, 0, basis.X.Z).Normalized();
        var vor = new Vector3(basis.Z.X, 0, basis.Z.Z).Normalized();
        var neu = _kameraDrehung.Position + rechts * bildschirm.X + vor * bildschirm.Y;
        _kameraDrehung.Position = new Vector3(Mathf.Clamp(neu.X, -70, 110), 0, Mathf.Clamp(neu.Z, -80, 80));
    }

    public override void _UnhandledInput(InputEvent eingabe)
    {
        switch (eingabe)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                _kamera.Size = Mathf.Clamp(_kamera.Size * 0.9f, KameraMinZoom, KameraMaxZoom);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                _kamera.Size = Mathf.Clamp(_kamera.Size * 1.1f, KameraMinZoom, KameraMaxZoom);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } rechts:
                _ziehen = rechts.Pressed;
                break;
            case InputEventMouseMotion bewegung when _ziehen:
                // Pixel → Meter: die sichtbare Höhe entspricht _kamera.Size
                var meterJePixel = _kamera.Size / GetViewport().GetVisibleRect().Size.Y;
                Verschieben(-bewegung.Relative * meterJePixel);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } klick:
                Auswaehlen(klick.Position);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q }:
                _zielDrehung -= 90;
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.E }:
                _zielDrehung += 90;
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void Auswaehlen(Vector2 mausposition)
    {
        var von = _kamera.ProjectRayOrigin(mausposition);
        var bis = von + _kamera.ProjectRayNormal(mausposition) * 500;
        var treffer = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(von, bis));
        if (treffer.Count > 0 && treffer["collider"].As<Node>() is { } koerper && koerper.HasMeta("kennung"))
            GebaeudeAngeklickt?.Invoke(koerper.GetMeta("kennung").AsString());
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Bausteine der Greybox
    // ═════════════════════════════════════════════════════════════════════════

    private static StandardMaterial3D Material(Color farbe) => new() { AlbedoColor = farbe, Roughness = 0.95f };

    private void Quader(Vector3 mitte, Vector3 groesse, StandardMaterial3D material)
    {
        AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = groesse }, MaterialOverride = material, Position = mitte });
    }

    private void Flaeche(Vector3 mitte, Vector2 groesse, Color farbe, float hoehe)
    {
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = groesse }, MaterialOverride = Material(farbe), Position = mitte + new Vector3(0, hoehe, 0) });
    }

    /// <summary>Satteldach: ein Prisma, dessen First entlang Z (quer = entlang X) läuft.</summary>
    private void Dach(Vector3 unterkante, Vector3 groesse, StandardMaterial3D material, bool quer)
    {
        var dach = new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = quer ? new Vector3(groesse.Z, groesse.Y, groesse.X) : groesse },
            MaterialOverride = material,
            Position = unterkante + new Vector3(0, groesse.Y / 2, 0),
        };
        if (quer) dach.RotationDegrees = new Vector3(0, 90, 0);
        AddChild(dach);
    }

    /// <summary>Ein Haus: Wandquader plus Satteldach.</summary>
    private void Haus(Vector3 grund, Vector3 groesse, Color wand, Color dach, bool quer)
    {
        Quader(grund + new Vector3(0, groesse.Y / 2, 0), groesse, Material(wand));
        var dachhoehe = Mathf.Min(groesse.X, groesse.Z) * 0.6f;
        Dach(grund + new Vector3(0, groesse.Y, 0), new Vector3(groesse.X + 0.6f, dachhoehe, groesse.Z + 0.6f), Material(dach), quer);
    }

    /// <summary>Ein Turm mit spitzem Dach (rund oder eckig).</summary>
    private void Turm(Vector3 grund, float radius, float hoehe, Color dach, bool eckig = false)
    {
        var segmente = eckig ? 4 : 10;
        var schaft = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = hoehe, RadialSegments = segmente },
            MaterialOverride = Material(Mauerstein),
            Position = grund + new Vector3(0, hoehe / 2, 0),
        };
        var spitze = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = radius * 1.25f, Height = radius * 2.6f, RadialSegments = segmente },
            MaterialOverride = Material(dach),
            Position = grund + new Vector3(0, hoehe + radius * 1.3f, 0),
        };
        if (eckig) { schaft.RotationDegrees = new Vector3(0, 45, 0); spitze.RotationDegrees = new Vector3(0, 45, 0); }
        AddChild(schaft);
        AddChild(spitze);
    }

    /// <summary>Ein flacher Weg zwischen zwei Punkten.</summary>
    private void WegZiehen(Vector3 von, Vector3 bis, float breite)
    {
        var mitte = (von + bis) / 2;
        var laenge = von.DistanceTo(bis);
        var weg = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(breite, 0.06f, laenge + breite * 0.5f) },
            MaterialOverride = Material(Weg),
            Position = new Vector3(mitte.X, 0.04f, mitte.Z),
            Rotation = new Vector3(0, Mathf.Atan2(bis.X - von.X, bis.Z - von.Z), 0),
        };
        AddChild(weg);
    }

    /// <summary>Unsichtbarer Körper, damit Mausklicks das Gebäude treffen.</summary>
    private void Klickflaeche(string kennung, Vector3 mitte, Vector3 groesse)
    {
        var koerper = new StaticBody3D { Position = mitte };
        koerper.SetMeta("kennung", kennung);
        koerper.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = groesse } });
        AddChild(koerper);
    }

    private void Beschriftung(string text, Vector3 position, int groesse)
    {
        var label = new Label3D
        {
            Text = text,
            Position = position,
            FontSize = groesse,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            OutlineSize = 14,
            OutlineModulate = new Color(0.08f, 0.07f, 0.06f),
            Modulate = new Color(0.97f, 0.94f, 0.86f),
            PixelSize = 0.04f,
        };
        _beschriftungen.Add(label);
        AddChild(label);
    }
}
