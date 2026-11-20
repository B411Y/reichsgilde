using Godot;

namespace Reichsgilde;

/// <summary>
/// <b>3D-Bücher in den Innenansichten</b> – ein Teil der <c>partial class Stadtansicht</c> (Bereich Oberfläche): das Personalbuch auf dem Lesepult
/// und das Kontorbuch am Schreibpult im Werkraum jeder Werkstatt, das Familienbuch auf dem Stubentisch der Wohnstube, das Schwarze Buch auf dem
/// Kartentisch im Hinterzimmer des Wohnhauses (nur im gebauten Raum; die Tafel dazu ist das Hinterzimmer, Taste H öffnet sie weiter ohne Buch).
///
/// Ein Klick auf ein Buch meldet <see cref="BuchAngeklickt"/>; die Hauptszene (<c>Hauptszene.Buecher.cs</c>) prüft, ob es das eigene Haus ist, und ruft
/// <see cref="BuchSchauOeffnen"/>. Dann schwenkt eine <b>eigene Kamera</b> („Buchkamera“, nicht die Raumkamera und nicht die Bühne der Schriftrollen)
/// in 0,8 s abgebremst vor das Buch, es klappt auf (Clips des Modells), und die Hauptszene legt die echte 2D-Tafel darüber, sobald
/// <see cref="BuchTafelSichtbar"/> gilt. Beim Reiterwechsel blättert es um (<see cref="BuchUmblaettern"/>), beim Schließen klappt es zu und die Kamera
/// fährt zurück. Den Ablauf rechnet <see cref="BuchAblauf"/> (ohne Godot, getestet). Während des Schwenks nimmt die Raumansicht keine Eingabe an.
/// Einstellung „Siegel und Briefe bewegen“ aus: kein Schwenk, kein Klappen, kein Blättern – das Buch steht sofort offen im Bild.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Buch an seinem Platz im Raum.</summary>
    private sealed class BuchStand
    {
        public required BuchArt Art { get; init; }
        public required Node3D Buch { get; init; }
        public required string RaumKennung { get; init; }
        public AnimationPlayer? Spieler { get; init; }
        public StandardMaterial3D? Wappenfeld { get; init; }
        public Func<Color>? Hausfarbe { get; init; }
        /// <summary>Mitte der aufgeschlagenen Doppelseite in den Koordinaten des Buchs.</summary>
        public Vector3 MitteOffen { get; set; }
        public string Clip { get; set; } = "";
    }

    /// <summary>Umriss, den jedes Netz der Bücher braucht: Die Knochen klappen die Deckel weit über den Ruheumriss hinaus (Doku des Modells).</summary>
    private static readonly Aabb BuchUmriss = new(new Vector3(-0.42f, -0.05f, -0.25f), new Vector3(0.84f, 0.45f, 0.5f));

    /// <summary>
    /// Platz des Schreibpults im Werkraum je Innenmodell: Ort auf dem Boden und Drehung um die Hochachse in Grad (0: der Schreiber steht südlich
    /// und blickt nach Norden). An der Wand, frei von Möbeln, Arbeitsplätzen, Türen und Hoftor (aus den Belegungskarten der Modelle, im Klicktest angesehen).
    /// </summary>
    private static readonly Dictionary<string, (Vector3 Ort, float Grad)> SchreibpultPlaetze = new()
    {
        ["innen/alchimist_innen"] = (new Vector3(2.6f, 0, 3.05f), 180),
        ["innen/dieb_innen"] = (new Vector3(-2.55f, 0, 2.95f), 180),
        ["innen/gardist_innen"] = (new Vector3(5.6f, 0, -0.5f), -90),
        ["innen/geldleiher_innen"] = (new Vector3(5.6f, 0, -0.9f), -90),
        ["innen/parfumeur_innen"] = (new Vector3(2.9f, 0, 2.95f), 180),
        ["innen/prediger_innen"] = (new Vector3(-9.2f, 0, -0.7f), 90),
        ["innen/schmied_innen"] = (new Vector3(4.1f, 0, 0.95f), -90),
        ["innen/steinmetz_innen"] = (new Vector3(3.62f, 0, 1.0f), -90),
        ["innen/tischler_innen"] = (new Vector3(1.0f, 0, 2.95f), 180),
        ["innen/wirt_innen"] = (new Vector3(-0.42f, 0, 3.0f), 180),
    };

    private readonly Dictionary<(InnenAnsicht, BuchArt), BuchStand> _buchStaende = new();
    private Camera3D? _buchKamera;
    private BuchStand? _buchOffen;
    private BuchAblauf? _buchAblauf;
    private Transform3D _buchBlickVon, _buchBlickZiel;

    /// <summary>Der Spieler hat ein Buch im Raum angeklickt (die Hauptszene entscheidet, ob es sich öffnet).</summary>
    public event Action<BuchArt>? BuchAngeklickt;

    /// <summary>Der Spieler hat bei offenem Buch daneben geklickt oder die rechte Taste gedrückt: Die Hauptszene schließt Tafel und Buch.</summary>
    public event Action? BuchSchliessenGewuenscht;

    /// <summary>Läuft eine Buchschau (Schwenk, offenes Buch, Rückweg)?</summary>
    public bool BuchSchauAktiv => _buchAblauf?.Aktiv == true;

    /// <summary>Schwenkt die Kamera gerade hin oder zurück? Dann ist nichts anklickbar.</summary>
    public bool BuchGesperrt => _buchAblauf?.Gesperrt == true;

    /// <summary>Liegen die Seiten flach, sodass die 2D-Tafel darüber gehört?</summary>
    public bool BuchTafelSichtbar => _buchAblauf?.TafelSichtbar == true;

    /// <summary>Das Buch der laufenden Schau, sonst null.</summary>
    public BuchArt? BuchOffen => BuchSchauAktiv ? _buchOffen?.Art : null;

    /// <summary>Gibt es dieses Buch im gezeigten Raum?</summary>
    public bool BuchImRaum(BuchArt art) => BuchStandHier(art) is not null;

    private BuchStand? BuchStandHier(BuchArt art) =>
        _innen is { } a && _raum is { } raum && _buchStaende.TryGetValue((a, art), out var stand) && stand.RaumKennung == raum.Kennung && IsInstanceValid(stand.Buch) && stand.Buch.IsVisibleInTree()
            ? stand : null;

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau: Pulte und Bücher
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Werkraum eines Innenmodells: Das Lesepult mit dem Personalbuch ersetzt den alten Ständer <c>Bedienung_Personal_Buch</c> (gleicher Ort, gleiche
    /// Richtung; seine Klickfläche und sein Namensschild bleiben), das Schreibpult mit dem Kontorbuch steht an seinem Platz aus <see cref="SchreibpultPlaetze"/>.
    /// </summary>
    private void InnenBuecherAnlegen(BetriebsAnsicht a, ModellRaumDaten md, Node3D wurzel, string datei)
    {
        if (wurzel.FindChild("Raum_Werk", true, false) is not Node3D werk) return;
        var ebene = RaumEbene("Werk");
        if (md.Bedienungen.FirstOrDefault(b => b.Tafel == "Personal").Knoten is MeshInstance3D alt && StaenderLage(alt) is var (ort, grad)
            && PultMitBuch(werk, "requisiten/lesepult", BuchArt.Personal, ort, grad) is var (pult, stand))
        {
            alt.Layers = 0; // der alte Ständer bleibt als Klickfläche, gezeichnet wird das Lesepult
            alt.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            EbeneSetzen(pult, ebene);
            _buchStaende[(a, BuchArt.Personal)] = stand;
        }
        if (SchreibpultPlaetze.TryGetValue(datei, out var platz)
            && PultMitBuch(werk, "requisiten/schreibpult", BuchArt.Kontor, platz.Ort, platz.Grad) is var (schreibpult, kontor))
        {
            EbeneSetzen(schreibpult, ebene);
            InnenKlickAnlegen(a, md, schreibpult, "bedienung:Kontor:Buch"); // Namensschild unter der Maus wie bei den anderen Gegenständen
            _buchStaende[(a, BuchArt.Kontor)] = kontor;
        }
        GD.Print($"[buecher] {datei}: Lesepult {(_buchStaende.TryGetValue((a, BuchArt.Personal), out var p) ? p.Buch.GlobalPosition.ToString("0.00") : "fehlt")}, " +
                 $"Schreibpult {(_buchStaende.TryGetValue((a, BuchArt.Kontor), out var k) ? k.Buch.GlobalPosition.ToString("0.00") : "fehlt")}");
    }

    /// <summary>Wohnstube: das Familienbuch liegt links auf dem Stubentisch, das Wappenfeld des Einbands trägt die Hausfarbe.</summary>
    private void FamilienbuchAnlegen(InnenAnsicht a, Vector3 s, Func<Color> hausfarbe)
    {
        if (BuchLaden(BuchArt.Familie, RaumWohnstube, hausfarbe) is not { } stand) return;
        a.Innen!.AddChild(stand.Buch);
        stand.Buch.Position = a.Mitte + s + new Vector3(-0.5f, 0.83f, 0.05f); // Tischplatte: Mitte 0,78 m, 0,1 m dick
        BuchMitteMessen(stand);
        _buchStaende[(a, BuchArt.Familie)] = stand;
    }

    /// <summary>
    /// Hinterzimmer des Wohnhauses (Raummodell „…_hinterzimmer_innen“, <c>Stadtansicht.Wohnraeume.cs</c>): das Schwarze Buch liegt zu auf dem
    /// Kartentisch (Netz <c>Gegenstand_Kartentisch</c>), der Fuß mit den Lesebändern zum Betrachter. Der Tisch ist in jedem Haus derselbe, steht
    /// aber je Volk und Grundform anders und trägt Karte, Briefe, Kerze, Dolch und Flasche im selben Netz; darum sucht <see cref="TischplatzSuchen"/>
    /// die freie, ebene Stelle. Das Buch hängt am Raum: ohne gebautes Hinterzimmer gibt es den Raum nicht und damit kein Buch.
    /// </summary>
    private void SchwarzesBuchAnlegen(InnenAnsicht a, Node3D wurzel)
    {
        if (wurzel.FindChild("Gegenstand_Kartentisch", true, false) is not MeshInstance3D tisch || tisch.Mesh is not { } netz) return;
        var ecken = netz.GetFaces();
        var inRaum = wurzel.GlobalTransform.AffineInverse() * tisch.GlobalTransform;
        for (var i = 0; i < ecken.Length; i++) ecken[i] = inRaum * ecken[i];
        var auge = wurzel.FindChild("Blick_Auge", true, false) is Node3D blick ? wurzel.ToLocal(blick.GlobalPosition) : (Vector3?)null;
        if (TischplatzSuchen(ecken, auge) is not var (ort, grad) || BuchLaden(BuchArt.Schwarz, RaumHinterzimmer, null) is not { } stand) return;
        wurzel.AddChild(stand.Buch);
        stand.Buch.Position = ort;
        stand.Buch.RotationDegrees = new Vector3(0, grad, 0);
        BuchMitteMessen(stand);
        _buchStaende[(a, BuchArt.Schwarz)] = stand;
        GD.Print($"[buecher] Schwarzes Buch auf dem Kartentisch: {ort.ToString("0.00")}, {grad:0}°");
    }

    /// <summary>
    /// Sucht auf einem runden Tisch (Dreiecke im Raum, y nach oben) den Platz für ein zugeklapptes Buch: auf einem Ring um die Tischmitte die Stelle,
    /// unter der die Auflage eben ist (höchstens Papier darunter, kein Leuchter, kein Dolch), möglichst nahe der Tischmitte und nicht am vorderen Rand (den verdeckt die Leiste der Oberfläche). Das Buch
    /// zeigt mit dem Fuß zum Betrachter, leicht verdreht. Gibt Ort (Unterseite des Buchs) und Drehung um die Hochachse zurück.
    /// </summary>
    private static (Vector3 Ort, float Grad)? TischplatzSuchen(Vector3[] ecken, Vector3? auge)
    {
        if (ecken.Length < 3) return null;
        var (min, max) = (ecken[0], ecken[0]);
        foreach (var e in ecken) (min, max) = (min.Min(e), max.Max(e));
        var mitte = (min + max) / 2;
        var radius = Mathf.Min(max.X - min.X, max.Z - min.Z) / 2;
        var zumAuge = auge is { } p && new Vector2(p.X - mitte.X, p.Z - mitte.Z) is { } r && r.LengthSquared() > 0.01f ? r.Normalized() : new Vector2(0, 1);
        var links = new Vector2(-zumAuge.Y, zumAuge.X); // links vom Betrachter aus
        const float halbBreit = 0.115f, halbTief = 0.15f, eben = 0.008f; // Buch zu: 0,21 × 0,28 m mit etwas Luft; mehr als 8 mm Stufe ist ein Gegenstand
        (Vector3 Ort, float Grad)? bester = null;
        var besteNote = float.MaxValue;
        foreach (var ring in new[] { 0.15f, 0.30f, 0.45f })
        for (var schritt = 0; schritt < 24; schritt++)
        {
            var winkel = schritt * Mathf.Tau / 24;
            var seite = new Vector2(Mathf.Cos(winkel), Mathf.Sin(winkel)) * ring;
            var ort = new Vector2(mitte.X, mitte.Z) + seite * radius;
            // das Buch schaut zum Betrachter (sein +z), dazu 12° verdreht, damit es nicht ausgerichtet daliegt
            var grad = Mathf.RadToDeg(Mathf.Atan2(zumAuge.X, zumAuge.Y)) + 12;
            var basis = Basis.FromEuler(new Vector3(0, Mathf.DegToRad(grad), 0));
            var (tief, hoch, getroffen) = (float.MaxValue, float.MinValue, true);
            for (var ix = -2; ix <= 2 && getroffen; ix++)
            for (var iz = -3; iz <= 3; iz++)
            {
                var probe = basis * new Vector3(ix * halbBreit / 2, 0, iz * halbTief / 3);
                if (HoeheUnter(ecken, ort.X + probe.X, ort.Y + probe.Z) is not { } h) { getroffen = false; break; }
                (tief, hoch) = (Mathf.Min(tief, h), Mathf.Max(hoch, h));
            }
            if (!getroffen) continue;
            // Note: uneben ist schlecht (je mm über der Grenze); vorn am Tischrand verdeckt die Leiste der Oberfläche das Buch, also lieber
            // zur Tischmitte hin und etwas links vom Betrachter aus
            var vorn = seite.Dot(zumAuge);
            var note = Mathf.Max(0, hoch - tief - eben) * 1000 + 4 * Mathf.Max(0, vorn - 0.1f) + Mathf.Abs(vorn) + 0.3f * (ring - seite.Dot(links));
            if (note >= besteNote) continue;
            besteNote = note;
            bester = (new Vector3(ort.X, hoch + 0.001f, ort.Y), grad);
        }
        return bester;
    }

    /// <summary>Die höchste Stelle der Dreiecke senkrecht unter (x, z); null, wenn dort nichts liegt.</summary>
    private static float? HoeheUnter(Vector3[] ecken, float x, float z)
    {
        float? hoch = null;
        for (var i = 0; i + 2 < ecken.Length; i += 3)
        {
            var (a, b, c) = (ecken[i], ecken[i + 1], ecken[i + 2]);
            if ((x < a.X && x < b.X && x < c.X) || (x > a.X && x > b.X && x > c.X) || (z < a.Z && z < b.Z && z < c.Z) || (z > a.Z && z > b.Z && z > c.Z)) continue;
            var nenner = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
            if (Mathf.Abs(nenner) < 1e-9f) continue;
            var u = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / nenner;
            var v = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / nenner;
            if (u < 0 || v < 0 || u + v > 1) continue;
            var h = u * a.Y + v * b.Y + (1 - u - v) * c.Y;
            if (hoch is null || h > hoch) hoch = h;
        }
        return hoch;
    }

    /// <summary>Mitte und Richtung des alten Ständers aus seinem Netz: Die hohe Kante der schrägen Platte liegt vom Leser weg.</summary>
    private static (Vector3 Ort, float Grad)? StaenderLage(MeshInstance3D alt)
    {
        if (alt.Mesh is not { } netz || netz.GetFaces() is not { Length: > 0 } ecken) return null;
        var umriss = netz.GetAabb();
        var mitte = umriss.GetCenter();
        var oben = umriss.End.Y - 0.03f;
        var hoch = Vector3.Zero;
        var zahl = 0;
        foreach (var ecke in ecken)
            if (ecke.Y > oben) { hoch += ecke; zahl++; }
        if (zahl == 0) return null;
        var leser = mitte - hoch / zahl;
        leser.Y = 0;
        if (leser.LengthSquared() < 1e-6f) return null;
        var ort = alt.Transform * new Vector3(mitte.X, umriss.Position.Y, mitte.Z);
        var richtung = alt.Transform.Basis * leser;
        return (ort, Mathf.RadToDeg(Mathf.Atan2(richtung.X, richtung.Z)));
    }

    /// <summary>Stellt ein Pult in den Raum und legt das Buch auf seinen Knoten <c>Buch_Auflage</c> (ohne Versatz und Drehung, Doku des Modells).</summary>
    private (Node3D Pult, BuchStand Stand)? PultMitBuch(Node3D raum, string pultDatei, BuchArt art, Vector3 ort, float grad)
    {
        if (Modell(pultDatei) is not { } szene || BuchLaden(art, RaumWerk, null) is not { } stand) return null;
        var pult = szene.Instantiate<Node3D>();
        pult.Name = art == BuchArt.Personal ? "Lesepult" : "Schreibpult";
        raum.AddChild(pult);
        pult.Position = ort;
        pult.RotationDegrees = new Vector3(0, grad, 0);
        foreach (var teil in pult.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            if (teil.Mesh is { } netz)
                for (var i = 0; i < netz.GetSurfaceCount(); i++)
                    if (netz.SurfaceGetMaterial(i) is StandardMaterial3D { ResourceName: "Farben" } stoff) FarbenEinrichten(stoff);
        var auflage = pult.FindChild("Buch_Auflage", true, false) as Node3D ?? pult;
        auflage.AddChild(stand.Buch);
        BuchMitteMessen(stand);
        return (pult, stand);
    }

    /// <summary>Lädt ein Buch: Umriss unbeleuchtet mit Rückseiten-Culling, kein Schatten, weiter Umriss für die Knochen, Inhaltsflächen aus (die Tafel ist 2D).</summary>
    private BuchStand? BuchLaden(BuchArt art, string raumKennung, Func<Color>? hausfarbe)
    {
        if (Modell($"oberflaeche/buch_{art.ToString().ToLowerInvariant()}") is not { } szene) return null;
        var buch = szene.Instantiate<Node3D>();
        buch.Name = $"Buch_{art}";
        StandardMaterial3D? wappen = null;
        foreach (var teil in buch.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString();
            teil.CustomAabb = BuchUmriss;
            teil.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            if (name.StartsWith("Inhaltsflaeche")) { teil.Visible = false; continue; }
            if (teil.Mesh is not { } netz) continue;
            for (var i = 0; i < netz.GetSurfaceCount(); i++)
            {
                if (netz.SurfaceGetMaterial(i) is not StandardMaterial3D stoff) continue;
                if (name.Contains("Umriss"))
                {
                    var kopie = (StandardMaterial3D)stoff.Duplicate();
                    kopie.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    kopie.CullMode = BaseMaterial3D.CullModeEnum.Back;
                    teil.SetSurfaceOverrideMaterial(i, kopie);
                }
                else if (name == "Wappenfeld")
                {
                    wappen = (StandardMaterial3D)stoff.Duplicate();
                    wappen.AlbedoTexture = null; // die reine Hausfarbe, nicht mit dem Platzhalterbild des Modells gemischt
                    teil.SetSurfaceOverrideMaterial(i, wappen);
                }
            }
        }
        var spieler = buch.FindChildren("*", nameof(AnimationPlayer), true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (spieler is not null)
        {
            foreach (var name in spieler.GetAnimationList()) spieler.GetAnimation(name).LoopMode = Animation.LoopModeEnum.None;
            spieler.SpeedScale = 0; // die Stelle im Clip setzt die Buchschau selbst (BuchPose)
        }
        return new BuchStand { Art = art, Buch = buch, RaumKennung = raumKennung, Spieler = spieler, Wappenfeld = wappen, Hausfarbe = hausfarbe };
    }

    /// <summary>Misst die Mitte der aufgeschlagenen Doppelseite (Rücken zwischen den beiden Deckeln im Clip „offen“) und legt das Buch wieder zu.</summary>
    private static void BuchMitteMessen(BuchStand stand)
    {
        stand.MitteOffen = new Vector3(0, 0.04f, 0);
        BuchPose(stand, "offen", 0);
        if (stand.Buch.FindChildren("*", nameof(Skeleton3D), true, false).OfType<Skeleton3D>().FirstOrDefault() is { } skelett)
        {
            var (hinten, vorn) = (skelett.FindBone("Deckel_hinten"), skelett.FindBone("Deckel_vorn"));
            if (hinten >= 0 && vorn >= 0)
            {
                var ruecken = (skelett.GetBoneGlobalPose(hinten).Origin + skelett.GetBoneGlobalPose(vorn).Origin) / 2;
                var imBuch = stand.Buch.GlobalTransform.AffineInverse() * skelett.GlobalTransform * ruecken;
                stand.MitteOffen = new Vector3(imBuch.X, Mathf.Max(imBuch.Y, 0.02f), 0);
            }
        }
        BuchPose(stand, "zu", 0);
    }

    private static void BuchPose(BuchStand stand, string clip, double zeit)
    {
        if (stand.Spieler is not { } spieler || !spieler.HasAnimation(clip)) return;
        if (stand.Clip != clip || !spieler.IsPlaying())
        {
            spieler.Play(clip);
            stand.Clip = clip;
        }
        spieler.Seek(Math.Min(zeit, spieler.GetAnimation(clip).Length), true);
        spieler.Advance(0); // die Knochen stehen sofort (wie HandschuhZeiger)
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Buchschau: Kamera, Ablauf, Eingabe
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Öffnet das Buch im gezeigten Raum. Gibt false zurück, wenn es dort keines gibt oder schon eine Schau läuft.</summary>
    public bool BuchSchauOeffnen(BuchArt art)
    {
        if (BuchSchauAktiv || BuchStandHier(art) is not { } stand) return false;
        if (_buchKamera is null)
        {
            _buchKamera = new Camera3D { Name = "Buchkamera", Near = 0.03f };
            AddChild(_buchKamera);
        }
        if (stand.Wappenfeld is { } wappen && stand.Hausfarbe is { } farbe) wappen.AlbedoColor = farbe();
        _buchKamera.Environment = _raumKamera.Environment;
        _buchKamera.Attributes = _raumKamera.Attributes;
        _buchKamera.CullMask = _raumKamera.CullMask;
        _buchKamera.Fov = _raumKamera.Fov;
        _buchKamera.Far = _raumKamera.Far;
        _buchBlickVon = _raumKamera.GlobalTransform;
        _buchBlickZiel = BuchBlick(stand, BuchMasse.Von(art), _raumKamera.Fov);
        _buchOffen = stand;
        _buchAblauf = new BuchAblauf(BuchMasse.Von(art));
        _buchAblauf.Oeffnen(Meldungseinstellungen.Bewegung);
        _raumSchild.Visible = false;
        _hoverTag = null;
        _buchKamera.GlobalTransform = _buchBlickVon;
        _buchKamera.MakeCurrent();
        BuchSchauAktualisieren(0);
        return true;
    }

    /// <summary>
    /// Prüfhilfe (Startoption <c>--buchblick=</c>, nur für Bildschirmfotos): stellt die Raumkamera schräg vor das Buch, damit man Pult, Buch und
    /// Umgebung sieht. Gibt false zurück, wenn es das Buch im gezeigten Raum nicht gibt.
    /// </summary>
    public bool BuchBlickPruefen(BuchArt art)
    {
        if (BuchStandHier(art) is not { } stand) return false;
        var buch = stand.Buch.GlobalTransform;
        var vorn = new Vector3(buch.Basis.Z.X, 0, buch.Basis.Z.Z).Normalized();
        var seite = new Vector3(vorn.Z, 0, -vorn.X);
        _raumKamera.LookAtFromPosition(buch.Origin + vorn * 2.4f + seite * 0.9f + Vector3.Up * 1.3f, buch.Origin + Vector3.Down * 0.3f, Vector3.Up);
        _blickZiel = _raumKamera.GlobalTransform;
        return true;
    }

    /// <summary>Reiterwechsel der Tafel: Das Buch blättert um (vorwärts oder zurück).</summary>
    public void BuchUmblaettern(bool vorwaerts) => _buchAblauf?.Blaettern(vorwaerts, Meldungseinstellungen.Bewegung);

    /// <summary>Klappt das Buch zu und fährt die Kamera zurück. Während des Schwenks geschieht nichts.</summary>
    public void BuchSchauSchliessen()
    {
        if (_buchAblauf?.Schliessen(Meldungseinstellungen.Bewegung) == true) BuchSchauAktualisieren(0);
    }

    /// <summary>Sofort zurück zur Raumkamera (Raumwechsel, Haus verlassen): Buch zu, kein Schwenk.</summary>
    private void BuchSchauAbbrechen()
    {
        if (_buchAblauf is null) return;
        _buchAblauf.Abbrechen();
        BuchSchauBeenden();
    }

    private void BuchSchauBeenden()
    {
        if (_buchOffen is { } stand) BuchPose(stand, "zu", 0);
        _buchOffen = null;
        _buchAblauf = null;
        if (_buchKamera is { Current: true } && _raum is not null) _raumKamera.MakeCurrent();
    }

    /// <summary>
    /// Der Blick auf das aufgeschlagene Buch: senkrecht auf die Doppelseite, der Kopf der Seiten oben, so nah, dass das Buch das Bild fast füllt
    /// (höchstens 80 % der Höhe und 86 % der Breite – die Deckel rahmen die Tafel).
    /// </summary>
    private Transform3D BuchBlick(BuchStand stand, BuchMasse masse, float fov)
    {
        var buch = stand.Buch.GlobalTransform.Orthonormalized();
        var fenster = GetViewport().GetVisibleRect().Size;
        var seiten = fenster.Y > 0 ? fenster.X / fenster.Y : 16f / 9f;
        var tan = 2 * Mathf.Tan(Mathf.DegToRad(fov) / 2);
        var abstand = Mathf.Max((float)masse.HoeheOffen / (0.80f * tan), (float)masse.BreiteOffen / (0.86f * seiten * tan));
        var basis = new Basis(buch.Basis.X, -buch.Basis.Z, buch.Basis.Y);
        return new Transform3D(basis, buch * stand.MitteOffen + buch.Basis.Y * abstand);
    }

    /// <summary>Jedes Bild im Raum: Ablauf weiterrechnen, Kamera und Clip setzen. Gibt true zurück, solange eine Schau läuft.</summary>
    private bool BuchSchauAktualisieren(double delta)
    {
        if (_buchAblauf is not { } ablauf || _buchOffen is not { } stand || _buchKamera is not { } kamera) return false;
        ablauf.Weiter(delta);
        if (!ablauf.Aktiv)
        {
            BuchSchauBeenden();
            return false;
        }
        var w = (float)ablauf.Kamera;
        kamera.GlobalTransform = new Transform3D(
            new Basis(_buchBlickVon.Basis.GetRotationQuaternion().Slerp(_buchBlickZiel.Basis.GetRotationQuaternion(), w)),
            _buchBlickVon.Origin.Lerp(_buchBlickZiel.Origin, w));
        BuchPose(stand, ablauf.Clip, ablauf.Clipzeit);
        return true;
    }

    /// <summary>Liegt die Bildschirmstelle auf dem Buch? Der Umriss des Buchs (zu oder offen), mit der laufenden Kamera ins Bild gerechnet.</summary>
    private bool BuchUnter(BuchStand stand, Vector2 stelle, bool offen)
    {
        var kamera = GetViewport().GetCamera3D();
        if (kamera is null) return false;
        var m = BuchMasse.Von(stand.Art);
        var (halbX, halbZ) = offen ? ((float)m.BreiteOffen / 2, (float)m.HoeheOffen / 2) : ((float)m.BreiteOffen / 4 + 0.01f, (float)m.HoeheOffen / 2);
        var mitte = offen ? stand.MitteOffen : new Vector3(0, 0.04f, 0);
        var buch = stand.Buch.GlobalTransform;
        Rect2? flaeche = null;
        foreach (var x in new[] { -halbX, halbX })
        foreach (var y in offen ? new[] { mitte.Y } : new[] { 0f, 0.08f }) // offen: die Ebene der Seiten; zu: der ganze Buchblock
        foreach (var z in new[] { -halbZ, halbZ })
        {
            var punkt = buch * (new Vector3(mitte.X + x, y, z));
            if (kamera.IsPositionBehind(punkt)) return false;
            var bild = kamera.UnprojectPosition(punkt);
            flaeche = flaeche is { } f ? f.Expand(bild) : new Rect2(bild, Vector2.Zero);
        }
        return flaeche is { } r && r.Grow(4).HasPoint(stelle);
    }

    /// <summary>
    /// Eingabe der Raumansicht, bevor sie irgendetwas anderes tut. Läuft eine Schau, schluckt sie Maus und Tasten des Raums: Während des Schwenks
    /// geschieht nichts, bei offenem Buch schließt ein Klick neben das Buch (links oder rechts). Sonst öffnet ein Linksklick auf ein Buch die Schau.
    /// </summary>
    private bool BuchEingabe(InputEvent eingabe)
    {
        if (BuchSchauAktiv)
        {
            if (eingabe is InputEventMouseButton { Pressed: true } klick && !BuchGesperrt && _buchOffen is { } offen
                && (klick.ButtonIndex == MouseButton.Right || (klick.ButtonIndex == MouseButton.Left && !BuchUnter(offen, klick.Position, true))))
                BuchSchliessenGewuenscht?.Invoke();
            return eingabe is InputEventMouseButton or InputEventMouseMotion or InputEventKey { Keycode: Key.Q or Key.E };
        }
        if (eingabe is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } druck || _ratssitzung is not null) return false;
        foreach (var art in Enum.GetValues<BuchArt>())
            if (BuchStandHier(art) is { } stand && BuchUnter(stand, druck.Position, false))
            {
                BuchAngeklickt?.Invoke(art);
                return true;
            }
        return false;
    }
}
