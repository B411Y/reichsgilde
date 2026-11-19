using Godot;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// <b>3D-Gegenstände im Kontor</b> – ein Teil der <c>partial class Stadtansicht</c>: der Kartentisch mit der Pergamentkarte und die Geldtruhe
/// (<c>kartentisch.glb</c>, <c>geldtruhe.glb</c> aus <c>tools/modelle/gegenstaende3d/</c>; Knoten, Clips und Maße in
/// <c>docs/bereiche/darstellung.md</c>, „3D-Gegenstände“). Gebaut wie die 3D-Bücher (<c>Stadtansicht.Innenbuecher.cs</c>):
///
/// Ein Klick auf einen Gegenstand meldet <see cref="GegenstandAngeklickt"/>; die Hauptszene (<c>Hauptszene.Gegenstaende.cs</c>) prüft, ob es das
/// eigene Haus ist, und ruft <see cref="GegenstandSchauOeffnen"/>. Dann schwenkt eine eigene Kamera in 0,8 s abgebremst vor den Gegenstand, die
/// Karte entrollt sich bzw. der Deckel springt auf (Clips des Modells); beim Schließen läuft es zurück. Den Ablauf rechnet
/// <see cref="GegenstandAblauf"/> (ohne Godot, getestet). Die Truhe zeigt ihre Füllstufe nach dem Bargeld des Hauses (<see cref="Truhenfuellung"/>).
/// Einstellung „Siegel und Briefe bewegen“ aus: kein Schwenk, kein Clip – der Gegenstand steht sofort offen im Bild.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Gegenstand an seinem Platz im Raum.</summary>
    private sealed class GegenstandStand
    {
        public required GegenstandArt Art { get; init; }
        public required Node3D Knoten { get; init; }
        public required string RaumKennung { get; init; }
        public required Func<Konto?> Konto { get; init; }
        public AnimationPlayer? Spieler { get; init; }
        /// <summary>Die Füllstufen der Truhe: Name der Stufe („viertel“, „halb“, „voll“) → Knoten <c>Fuellstufe_&lt;stufe&gt;</c>.</summary>
        public Dictionary<string, Node3D> Fuellstufen { get; } = new();
        public string Clip { get; set; } = "";
    }

    /// <summary>Umriss der Netze mit Haut (CustomAabb aus der Doku des Modells): Karte und Deckel bewegen sich über die Hülle der Rast-Pose hinaus.</summary>
    private static Aabb GegenstandUmriss(GegenstandArt art) => art == GegenstandArt.Kartentisch
        ? new Aabb(new Vector3(-0.62f, -0.02f, -0.42f), new Vector3(1.24f, 0.89f, 0.84f))
        : new Aabb(new Vector3(-0.36f, -0.01f, -0.42f), new Vector3(0.72f, 0.82f, 0.69f));

    /// <summary>Der Kasten, auf den man klickt (Gegenstand in Ruhe): Tisch 1,20 × 0,80 m bis über die Kartenrolle, Truhe 0,60 × 0,38 × 0,36 m.</summary>
    private static Aabb GegenstandKlickkasten(GegenstandArt art) => art == GegenstandArt.Kartentisch
        ? new Aabb(new Vector3(-0.60f, 0, -0.40f), new Vector3(1.20f, 0.86f, 0.80f))
        : new Aabb(new Vector3(-0.30f, 0, -0.19f), new Vector3(0.60f, 0.37f, 0.38f));

    private readonly Dictionary<(InnenAnsicht, GegenstandArt), GegenstandStand> _gegenstaende = new();
    private Camera3D? _gegenstandKamera;
    private GegenstandStand? _gegenstandOffen;
    private GegenstandAblauf? _gegenstandAblauf;
    private Transform3D _gegenstandBlickVon, _gegenstandBlickZiel;

    /// <summary>Der Spieler hat einen Gegenstand im Raum angeklickt (die Hauptszene entscheidet, ob er sich öffnet).</summary>
    public event Action<GegenstandArt>? GegenstandAngeklickt;

    /// <summary>Der Spieler hat bei offenem Gegenstand daneben geklickt oder die rechte Taste gedrückt: Die Hauptszene schließt ihn.</summary>
    public event Action? GegenstandSchliessenGewuenscht;

    /// <summary>Läuft eine Schau (Schwenk, offener Gegenstand, Rückweg)?</summary>
    public bool GegenstandSchauAktiv => _gegenstandAblauf?.Aktiv == true;

    /// <summary>Schwenkt die Kamera gerade hin oder zurück? Dann ist nichts anklickbar.</summary>
    public bool GegenstandGesperrt => _gegenstandAblauf?.Gesperrt == true;

    /// <summary>Liegt der Gegenstand ruhig offen (Karte glatt, Deckel oben)?</summary>
    public bool GegenstandOffenSichtbar => _gegenstandAblauf?.OffenSichtbar == true;

    /// <summary>Der Gegenstand der laufenden Schau, sonst null.</summary>
    public GegenstandArt? GegenstandOffen => GegenstandSchauAktiv ? _gegenstandOffen?.Art : null;

    /// <summary>Gibt es diesen Gegenstand im gezeigten Raum?</summary>
    public bool GegenstandImRaum(GegenstandArt art) => GegenstandHier(art) is not null;

    /// <summary>Wem gehört das Haus, in dem dieser Gegenstand im gezeigten Raum steht? Null: es gibt ihn hier nicht (oder das Haus ist herrenlos).</summary>
    public Konto? GegenstandKonto(GegenstandArt art) => GegenstandHier(art)?.Konto();

    private GegenstandStand? GegenstandHier(GegenstandArt art) =>
        _innen is { } a && _raum is { } raum && _gegenstaende.TryGetValue((a, art), out var stand) && stand.RaumKennung == raum.Kennung && stand.Knoten.IsVisibleInTree()
            ? stand : null;

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kontor (<c>KontorEinrichten</c>): die Geldtruhe an der Ostwand (Vorderseite zum Raum) und der Kartentisch vorn links auf dem freien Boden.
    /// Gibt false zurück, wenn die Truhe fehlt – dann baut das Kontor seine alte Truhe aus Grundkörpern.
    /// </summary>
    private bool KontorGegenstaendeAnlegen(InnenAnsicht a, Vector3 k, Hausstand haus)
    {
        if (GegenstandLaden(GegenstandArt.Kartentisch, RaumKontor, haus.Konto) is { } tisch)
        {
            a.Innen!.AddChild(tisch.Knoten);
            tisch.Knoten.Position = a.Mitte + k + KartentischOrt;
            tisch.Knoten.RotationDegrees = new Vector3(0, KartentischGrad, 0);
            GegenstandPose(tisch, "zu", 0);
            _gegenstaende[(a, GegenstandArt.Kartentisch)] = tisch;
        }
        if (GegenstandLaden(GegenstandArt.Geldtruhe, RaumKontor, haus.Konto) is not { } truhe) return false;
        a.Innen!.AddChild(truhe.Knoten);
        truhe.Knoten.Position = a.Mitte + k + GeldtruheOrt;
        truhe.Knoten.RotationDegrees = new Vector3(0, GeldtruheGrad, 0);
        GegenstandPose(truhe, "zu", 0);
        _gegenstaende[(a, GegenstandArt.Geldtruhe)] = truhe;
        a.Raumzustaende.Add((_, _) => TruheFuellen(truhe));
        TruheFuellen(truhe);
        return true;
    }

    /// <summary>Plätze im Kontor (um die Raummitte, Boden bei y = 0,1) und Drehung um die Hochachse in Grad; im Klicktest angesehen.</summary>
    private static readonly Vector3 KartentischOrt = new(-1.75f, 0.1f, 1.35f), GeldtruheOrt = new(3.4f, 0.1f, -0.6f);
    private const float KartentischGrad = 0, GeldtruheGrad = -90;

    /// <summary>Zeigt die Füllstufe nach dem Bargeld des Hauses: genau ein Knoten <c>Fuellstufe_*</c> an, leer = alle aus (Doku des Modells).</summary>
    private static void TruheFuellen(GegenstandStand truhe)
    {
        var stufe = Truhenfuellung.Stufe(truhe.Konto()?.Vermoegen ?? 0);
        foreach (var (name, knoten) in truhe.Fuellstufen) knoten.Visible = name == stufe;
    }

    /// <summary>Lädt einen Gegenstand: Umriss unbeleuchtet mit Rückseiten-Culling, kein Schatten der Umrisse, weiter Umriss für die Knochen, Clips ohne Schleife.</summary>
    private GegenstandStand? GegenstandLaden(GegenstandArt art, string raumKennung, Func<Konto?> konto)
    {
        if (Modell($"oberflaeche/{art.ToString().ToLowerInvariant()}") is not { } szene) return null;
        var knoten = szene.Instantiate<Node3D>();
        knoten.Name = art.ToString();
        var umriss = GegenstandUmriss(art);
        foreach (var teil in knoten.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString();
            if (teil.Skin is not null) teil.CustomAabb = umriss;
            if (name == "Kartenflaeche") KartenbildSetzen(teil);
            if (!name.Contains("Umriss") || teil.Mesh is not { } netz) continue;
            teil.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            for (var i = 0; i < netz.GetSurfaceCount(); i++)
            {
                if (netz.SurfaceGetMaterial(i) is not StandardMaterial3D stoff) continue;
                var kopie = (StandardMaterial3D)stoff.Duplicate();
                kopie.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                kopie.CullMode = BaseMaterial3D.CullModeEnum.Back;
                teil.SetSurfaceOverrideMaterial(i, kopie);
            }
        }
        var spieler = knoten.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (spieler is not null)
        {
            foreach (var name in spieler.GetAnimationList()) spieler.GetAnimation(name).LoopMode = Animation.LoopModeEnum.None;
            spieler.SpeedScale = 0; // die Stelle im Clip setzt die Schau selbst (GegenstandPose)
        }
        var stand = new GegenstandStand { Art = art, Knoten = knoten, RaumKennung = raumKennung, Konto = konto, Spieler = spieler };
        foreach (var stufe in Truhenfuellung.Stufen)
            if (knoten.FindChild($"Fuellstufe_{stufe}", true, false) is Node3D fuellung)
            {
                fuellung.Visible = false;
                stand.Fuellstufen[stufe] = fuellung;
            }
        return stand;
    }

    /// <summary>
    /// Legt das Bild der gespielten Karte (Vorschaubild der Kartenwahl) auf die Inhaltsfläche des Kartentischs: UV 0 bis 1 über die ganze Karte,
    /// sie rollt mit. Ohne Bild bleibt die Fläche durchsichtig (Material <c>Karte_Inhalt</c> des Modells), man sieht das blanke Pergament.
    /// </summary>
    private static void KartenbildSetzen(MeshInstance3D flaeche)
    {
        flaeche.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var pfad = $"res://assets/oberflaeche/karten/{Stadtkarten.StartKennung}.png";
        if (!ResourceLoader.Exists(pfad) || ResourceLoader.Load<Texture2D>(pfad) is not { } bild) return;
        flaeche.MaterialOverride = new StandardMaterial3D
        {
            ResourceName = "Karte_Inhalt", AlbedoTexture = bild, Roughness = 1, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1, 1, 1, 0.85f), // die Tinte der Karte liegt auf dem Pergament, das Blatt scheint leicht durch
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
        };
    }

    private static void GegenstandPose(GegenstandStand stand, string clip, double zeit)
    {
        if (stand.Spieler is not { } spieler || !spieler.HasAnimation(clip)) return;
        if (stand.Clip != clip || !spieler.IsPlaying())
        {
            spieler.Play(clip);
            stand.Clip = clip;
        }
        spieler.Seek(Math.Min(zeit, spieler.GetAnimation(clip).Length), true);
        spieler.Advance(0); // die Knochen stehen sofort (wie bei den Büchern)
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Schau: Kamera, Ablauf, Eingabe
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Öffnet den Gegenstand im gezeigten Raum. Gibt false zurück, wenn es dort keinen gibt oder schon eine Schau (auch eines Buchs) läuft.</summary>
    public bool GegenstandSchauOeffnen(GegenstandArt art)
    {
        if (GegenstandSchauAktiv || BuchSchauAktiv || GegenstandHier(art) is not { } stand) return false;
        if (_gegenstandKamera is null)
        {
            _gegenstandKamera = new Camera3D { Name = "Gegenstandkamera", Near = 0.03f };
            AddChild(_gegenstandKamera);
        }
        _gegenstandKamera.Environment = _raumKamera.Environment;
        _gegenstandKamera.Attributes = _raumKamera.Attributes;
        _gegenstandKamera.CullMask = _raumKamera.CullMask;
        _gegenstandKamera.Fov = _raumKamera.Fov;
        _gegenstandKamera.Far = _raumKamera.Far;
        _gegenstandBlickVon = _raumKamera.GlobalTransform;
        _gegenstandBlickZiel = GegenstandBlick(stand, GegenstandMasse.Von(art), _raumKamera.Fov);
        _gegenstandOffen = stand;
        _gegenstandAblauf = new GegenstandAblauf(GegenstandMasse.Von(art));
        _gegenstandAblauf.Oeffnen(Meldungseinstellungen.Bewegung);
        _raumSchild.Visible = false;
        _hoverTag = null;
        _gegenstandKamera.GlobalTransform = _gegenstandBlickVon;
        _gegenstandKamera.MakeCurrent();
        GegenstandSchauAktualisieren(0);
        return true;
    }

    /// <summary>
    /// Prüfhilfe (Startoption <c>--gegenstandblick=</c>, nur für Bildschirmfotos): stellt die Raumkamera schräg vor den Gegenstand, damit man ihn
    /// und seine Umgebung sieht. Gibt false zurück, wenn es ihn im gezeigten Raum nicht gibt.
    /// </summary>
    public bool GegenstandBlickPruefen(GegenstandArt art)
    {
        if (GegenstandHier(art) is not { } stand) return false;
        var lage = stand.Knoten.GlobalTransform;
        var vorn = new Vector3(lage.Basis.Z.X, 0, lage.Basis.Z.Z).Normalized();
        var seite = new Vector3(vorn.Z, 0, -vorn.X);
        var m = GegenstandMasse.Von(art);
        var weite = (float)Math.Max(m.Breite, 0.8);
        _raumKamera.LookAtFromPosition(lage.Origin + vorn * 2.0f * weite + seite * 0.8f * weite + Vector3.Up * (0.4f + 1.1f * weite), lage.Origin + Vector3.Up * (float)m.MitteY * 0.6f, Vector3.Up);
        _blickZiel = _raumKamera.GlobalTransform;
        return true;
    }

    /// <summary>Schließt den Gegenstand und fährt die Kamera zurück. Während des Schwenks geschieht nichts.</summary>
    public void GegenstandSchauSchliessen()
    {
        if (_gegenstandAblauf?.Schliessen(Meldungseinstellungen.Bewegung) == true) GegenstandSchauAktualisieren(0);
    }

    /// <summary>Sofort zurück zur Raumkamera (Raumwechsel, Haus verlassen): Gegenstand zu, kein Schwenk.</summary>
    private void GegenstandSchauAbbrechen()
    {
        if (_gegenstandAblauf is null) return;
        _gegenstandAblauf.Abbrechen();
        GegenstandSchauBeenden();
    }

    private void GegenstandSchauBeenden()
    {
        if (_gegenstandOffen is { } stand) GegenstandPose(stand, "zu", 0);
        _gegenstandOffen = null;
        _gegenstandAblauf = null;
        if (_gegenstandKamera is { Current: true } && _raum is not null) _raumKamera.MakeCurrent();
    }

    /// <summary>
    /// Der Blick auf den offenen Gegenstand: von vorn oben auf seine Mitte (Neigung gegen die Senkrechte aus den Maßen), die Rückseite oben im
    /// Bild, so nah, dass die gerahmte Fläche höchstens 80 % der Höhe und 86 % der Breite füllt.
    /// </summary>
    private Transform3D GegenstandBlick(GegenstandStand stand, GegenstandMasse m, float fov)
    {
        var lage = stand.Knoten.GlobalTransform.Orthonormalized();
        var fenster = GetViewport().GetVisibleRect().Size;
        var seiten = fenster.Y > 0 ? fenster.X / fenster.Y : 16f / 9f;
        var tan = 2 * Mathf.Tan(Mathf.DegToRad(fov) / 2);
        var abstand = Mathf.Max((float)m.Tiefe / (0.80f * tan), (float)m.Breite / (0.86f * seiten * tan));
        var neigung = Mathf.DegToRad((float)m.Neigung);
        var ziel = lage * new Vector3((float)m.MitteX, (float)m.MitteY, (float)m.MitteZ);
        var auge = ziel + (lage.Basis.Y * Mathf.Cos(neigung) + lage.Basis.Z * Mathf.Sin(neigung)) * abstand;
        return new Transform3D(Basis.Identity, auge).LookingAt(ziel, -lage.Basis.Z);
    }

    /// <summary>Jedes Bild im Raum: Ablauf weiterrechnen, Kamera und Clip setzen. Gibt true zurück, solange eine Schau läuft.</summary>
    private bool GegenstandSchauAktualisieren(double delta)
    {
        if (_gegenstandAblauf is not { } ablauf || _gegenstandOffen is not { } stand || _gegenstandKamera is not { } kamera) return false;
        ablauf.Weiter(delta);
        if (!ablauf.Aktiv)
        {
            GegenstandSchauBeenden();
            return false;
        }
        var w = (float)ablauf.Kamera;
        kamera.GlobalTransform = new Transform3D(
            new Basis(_gegenstandBlickVon.Basis.GetRotationQuaternion().Slerp(_gegenstandBlickZiel.Basis.GetRotationQuaternion(), w)),
            _gegenstandBlickVon.Origin.Lerp(_gegenstandBlickZiel.Origin, w));
        GegenstandPose(stand, ablauf.Clip, ablauf.Clipzeit);
        return true;
    }

    /// <summary>Liegt die Bildschirmstelle auf dem Gegenstand? Sein Kasten, mit der laufenden Kamera ins Bild gerechnet.</summary>
    private bool GegenstandUnter(GegenstandStand stand, Vector2 stelle, bool offen)
    {
        var kamera = GetViewport().GetCamera3D();
        if (kamera is null) return false;
        var kasten = offen ? GegenstandUmriss(stand.Art) : GegenstandKlickkasten(stand.Art);
        var lage = stand.Knoten.GlobalTransform;
        Rect2? flaeche = null;
        for (var i = 0; i < 8; i++)
        {
            var punkt = lage * kasten.GetEndpoint(i);
            if (kamera.IsPositionBehind(punkt)) return false;
            var bild = kamera.UnprojectPosition(punkt);
            flaeche = flaeche is { } f ? f.Expand(bild) : new Rect2(bild, Vector2.Zero);
        }
        return flaeche is { } r && r.Grow(4).HasPoint(stelle);
    }

    /// <summary>
    /// Eingabe der Raumansicht, noch vor den Büchern. Läuft eine Schau, schluckt sie Maus und Tasten des Raums: Während des Schwenks geschieht
    /// nichts, bei offenem Gegenstand schließt ein Rechtsklick oder ein Klick daneben. Sonst öffnet ein Linksklick auf einen Gegenstand die Schau.
    /// </summary>
    private bool GegenstandEingabe(InputEvent eingabe)
    {
        if (GegenstandSchauAktiv)
        {
            if (eingabe is InputEventMouseButton { Pressed: true } klick && !GegenstandGesperrt && _gegenstandOffen is { } offen
                && (klick.ButtonIndex == MouseButton.Right || (klick.ButtonIndex == MouseButton.Left && !GegenstandUnter(offen, klick.Position, true))))
                GegenstandSchliessenGewuenscht?.Invoke();
            return eingabe is InputEventMouseButton or InputEventMouseMotion or InputEventKey { Keycode: Key.Q or Key.E };
        }
        if (BuchSchauAktiv || eingabe is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } druck || _ratssitzung is not null) return false;
        foreach (var art in Enum.GetValues<GegenstandArt>())
            if (GegenstandHier(art) is { } stand && GegenstandUnter(stand, druck.Position, false))
            {
                GegenstandAngeklickt?.Invoke(art);
                return true;
            }
        return false;
    }
}
