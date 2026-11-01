using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Die Felsstücke der Karte in der 3D-Welt (Felspinsel F5) – ein Teil der <c>partial class Stadtansicht</c>, im Spiel und in der Kartenwerkstatt gleich.
/// Lage, Form, Gestein, Größe, Drehung, Neigung, Kippen und Einsinken kommen aus dem Kern (<see cref="Stadtkarte.Felsen"/> bzw. dem Entwurf); hier
/// wird nur gezeichnet:
/// <list type="bullet">
/// <item><b>Zellen</b> zu <see cref="FelsZelle"/> Metern: Je Zelle, Form und Gesteinsart ein <see cref="MultiMeshInstance3D"/> – 300 Stücke kosten so nur
/// ein paar Zeichenaufrufe, und was nicht im Bild liegt, schneidet Godot zellenweise weg. In der Werkstatt baut eine Änderung nur ihre Zelle neu.</item>
/// <item><b>Feinheit</b>: Die Netze <c>&lt;Form&gt;_0/1/2</c> aus <c>natur/natur_felsen.glb</c> (F1) wechseln mit dem Zoom der Spielkamera bzw. mit dem
/// Abstand der freien Kamera zur Zelle (<see cref="FelsStufe"/>, mit Spielraum gegen Flackern). <b>Schatten</b> werfen nur die feine und die mittlere
/// Stufe, also nur nah; weit herausgezoomt sind die Felsen klein und ohne.</item>
/// <item><b>Material</b>: das Fels-Material aus F2 (<see cref="Gesteinsarten"/>) mit den gebackenen Atlanten und Vertexfarben der F1-Formen, eins je
/// Gesteinsart für alle Stücke; dreiseitig ab Boden „Mittel“, eine Richtung auf „Niedrig“. Nässe und Schnee über <c>wetter.gdshaderinc</c>.
/// Die Einsinktiefe in Metern geht als Instanzdaten (x) an den Shader: Er zeichnet dort, wo das Stück aus dem Boden kommt, einen Moossaum.</item>
/// <item><b>Werkstatt</b>: einzelne Stücke wählen (Strahl gegen den Kasten des Netzes, sonst die Grundfläche unter der Maus), ziehen, mit Q/E drehen und
/// mit Entf löschen – der Kern rechnet (<see cref="Kartenentwurf.FelsVerschieben"/>, <see cref="Kartenentwurf.FelsDrehen"/>); beim Ziehen wandert nur
/// die eine Instanz mit, bis Loslassen den Befehl gibt.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Ein Felsstück, wie es gezeichnet wird (aus der Karte oder dem Entwurf), mit der Gesteinsart, die gilt.</summary>
    private readonly record struct Felsbild(string Form, string Gestein, double X, double Z, double Drehung, double Massstab, double Neigung,
        double Einsinken, double KippenX, double KippenZ);

    private const string FelsModell = "natur/natur_felsen";

    /// <summary>Kantenlänge der Zellen (Meter): klein genug zum Wegschneiden, groß genug für wenige Zeichenaufrufe.</summary>
    private const float FelsZelle = 64f;

    /// <summary>Feinheit nach Zoom (Meter, die das Bild hoch zeigt): bis hier fein (Stufe 0), bis hier mittel (1), darüber grob (2).</summary>
    private const float FelsFeinBis = 45f, FelsMittelBis = 100f;

    /// <summary>Spielraum gegen Flackern beim Wechsel der Feinheit (Anteil der Grenze).</summary>
    private const float FelsSpielraum = 0.08f;

    /// <summary>Schatten werfen nur die Stufen bis hier (0 fein, 1 mittel).</summary>
    private const int FelsSchattenBisStufe = 1;

    private sealed class Felszelle
    {
        public required Node3D Knoten { get; init; }

        /// <summary>Die Stücke der Zelle (Schlüssel: <see cref="EntwurfFels"/> in der Werkstatt, die Nummer in der Karte im Spiel).</summary>
        public List<object> Stuecke { get; } = new();

        /// <summary>Je Form und Gestein ein Teil; <c>Schutt</c>: die Schuttsteine vor den Stücken (F6, ohne Schatten, nur nah).</summary>
        public List<(MultiMeshInstance3D Teil, string Form, bool Schutt)> Teile { get; } = new();
        public Vector3 Mitte { get; set; }
        public int Stufe { get; set; } = -1;
        public bool Neu { get; set; } = true;
    }

    private Node3D? _felsenKnoten;
    private readonly Dictionary<(int I, int J), Felszelle> _felsZellen = new();
    private readonly Dictionary<object, Felsbild> _felsBilder = new();
    private readonly Dictionary<object, (int I, int J)> _felsZelleVon = new();
    private readonly Dictionary<object, (MultiMesh Vielfach, int Index)> _felsInstanz = new();
    private readonly Dictionary<object, Transform3D> _felsLagen = new();
    private readonly Dictionary<string, ShaderMaterial> _felsStoffe = new();
    private readonly Dictionary<string, Aabb> _felsKaesten = new();
    private bool _felsZellenNeu;
    private bool _felsDreiseitig = FelsDreiseitigGewuenscht;

    /// <summary>Struktur dreiseitig ab Boden „Mittel“, eine Richtung auf „Niedrig“ (wie die Material-Bibliothek der Modelle).</summary>
    private static bool FelsDreiseitigGewuenscht => Fenster.AktuelleBodenqualitaet != Bodenqualitaet.Niedrig;

    /// <summary>Alle Felsstücke (für Tests und Messung).</summary>
    private int FelsAnzahl => _felsBilder.Count;

    // ═════════════════════════════════════════════════════════════════════════
    //  Netze, Material, Lage
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Das Netz einer Form in einer Stufe (0 fein … 2 grob). Das Platzhalter-Material „Fels“ der glb wird einmal entfernt: Die Stücke bekommen ihr
    /// Fels-Material als Override, und ohne Flächenmaterial lässt die Material-Bibliothek (Oberflaechendetails) das Netz, wie es ist – mit den
    /// Schattennetzen des Imports.
    /// </summary>
    private Mesh? FelsNetz(string form, int stufe)
    {
        if (Felsformen.Finde(form) is not { } f || NaturNetz(FelsModell, $"{f.Knoten}_{Math.Clamp(stufe, 0, 2)}") is not { } netz) return null;
        if (netz is ArrayMesh a && a.GetSurfaceCount() > 0 && a.SurfaceGetMaterial(0) is not null) a.SurfaceSetMaterial(0, null);
        return netz;
    }

    /// <summary>Der Kasten des Netzes (mittlere Stufe) im Modell – für den Treffer der Maus.</summary>
    private Aabb? FelsKasten(string form)
    {
        if (_felsKaesten.TryGetValue(form, out var k)) return k;
        if (FelsNetz(form, 1) is not { } netz) return null;
        return _felsKaesten[form] = netz.GetAabb();
    }

    /// <summary>Das Fels-Material einer Gesteinsart (eins je Art, für alle Stücke geteilt).</summary>
    private ShaderMaterial FelsStoff(string gestein)
    {
        if (!_felsStoffe.TryGetValue(gestein, out var stoff))
            _felsStoffe[gestein] = stoff = Gesteinsarten.Material(Gesteinsarten.Nach(gestein), _felsDreiseitig, f1: true); // die Stufe stellt FelsenStufeNachfuehren für alle um
        return stoff;
    }

    /// <summary>Bei einer Änderung der Grafikeinstellungen: dreiseitig oder eine Richtung (aus <see cref="GrafikNachfuehren"/>).</summary>
    private void FelsenStufeNachfuehren()
    {
        var dreiseitig = FelsDreiseitigGewuenscht;
        if (dreiseitig == _felsDreiseitig) return;
        _felsDreiseitig = dreiseitig;
        foreach (var stoff in _felsStoffe.Values) Gesteinsarten.StufeSetzen(stoff, dreiseitig);
    }

    /// <summary>
    /// Die Lage eines Stücks in der Welt: Drehung um die Senkrechte, geneigt zur Hangnormalen (Anteil Neigung), gekippt um die eigenen Achsen, um
    /// Einsinken seiner Höhe unter den Boden unter der Mitte gesenkt, gleichmäßig skaliert. Die Hangnormale rechnet der Kern aus dem Höhenraster.
    /// </summary>
    private Transform3D FelsLage(in Felsbild b)
    {
        var hoehe = (float)((Felsformen.Finde(b.Form)?.Hoehe ?? 1) * b.Massstab);
        var oben = Vector3.Up;
        if (b.Neigung > 0 && (_entwurf?.Hoehen ?? _gelaende?.Roh) is { } raster)
        {
            var (gx, gz) = Felsstreuung.Hang(raster, b.X, b.Z);
            var normale = new Vector3(-(float)gx, 1, -(float)gz).Normalized();
            oben = oben.Slerp(normale, (float)b.Neigung).Normalized();
        }
        var basis = new Basis(Vector3.Up, Mathf.DegToRad((float)b.Drehung))
                    * new Basis(Vector3.Right, Mathf.DegToRad((float)b.KippenX)) * new Basis(Vector3.Back, Mathf.DegToRad((float)b.KippenZ));
        if (!oben.IsEqualApprox(Vector3.Up))
        {
            var achse = Vector3.Up.Cross(oben).Normalized();
            basis = new Basis(achse, Vector3.Up.AngleTo(oben)) * basis;
        }
        var boden = BodenHoehe((float)b.X, (float)b.Z);
        return new Transform3D(basis.Scaled(Vector3.One * (float)b.Massstab), new Vector3((float)b.X, boden - (float)b.Einsinken * hoehe, (float)b.Z));
    }

    /// <summary>Wie tief das Stück im Boden steckt (Meter) – für den Moossaum im Shader.</summary>
    private static float FelsTiefe(in Felsbild b) => (float)((Felsformen.Finde(b.Form)?.Hoehe ?? 1) * b.Massstab * b.Einsinken);

    private static (int I, int J) FelsZelleBei(double x, double z) => ((int)Math.Floor(x / FelsZelle), (int)Math.Floor(z / FelsZelle));

    // ═════════════════════════════════════════════════════════════════════════
    //  Stücke setzen und Zellen bauen
    // ═════════════════════════════════════════════════════════════════════════

    private void FelsenKnotenAnlegen()
    {
        if (_felsenKnoten is not null) return;
        _felsenKnoten = new Node3D { Name = "Felsen" };
        AddChild(_felsenKnoten);
    }

    /// <summary>Setzt ein Stück (neu oder geändert) und merkt seine alte und neue Zelle zum Neubau vor.</summary>
    private void FelsSetzen(object schluessel, in Felsbild bild)
    {
        FelsWeg(schluessel);
        FelsenKnotenAnlegen();
        var ort = FelsZelleBei(bild.X, bild.Z);
        if (!_felsZellen.TryGetValue(ort, out var zelle))
        {
            zelle = new Felszelle { Knoten = new Node3D { Name = $"Zelle_{ort.I}_{ort.J}" } };
            _felsenKnoten!.AddChild(zelle.Knoten);
            _felsZellen[ort] = zelle;
        }
        zelle.Stuecke.Add(schluessel);
        zelle.Neu = true;
        _felsBilder[schluessel] = bild;
        _felsZelleVon[schluessel] = ort;
        _felsZellenNeu = true;
    }

    /// <summary>Nimmt ein Stück weg (seine Zelle wird neu gebaut).</summary>
    private void FelsWeg(object schluessel)
    {
        if (!_felsZelleVon.Remove(schluessel, out var ort)) return;
        _felsBilder.Remove(schluessel);
        _felsInstanz.Remove(schluessel);
        _felsLagen.Remove(schluessel);
        if (!_felsZellen.TryGetValue(ort, out var zelle)) return;
        zelle.Stuecke.Remove(schluessel);
        zelle.Neu = true;
        _felsZellenNeu = true;
    }

    /// <summary>Baut die vorgemerkten Zellen neu (jedes Bild gefragt; in der Werkstatt auch am Ende von <see cref="Nachziehen"/>).</summary>
    private void FelsenNachziehen()
    {
        if (!_felsZellenNeu) return;
        _felsZellenNeu = false;
        foreach (var (ort, zelle) in _felsZellen.ToList())
        {
            if (!zelle.Neu) continue;
            FelsZelleBauen(zelle);
            if (zelle.Stuecke.Count > 0) continue;
            zelle.Knoten.QueueFree();
            _felsZellen.Remove(ort);
        }
    }

    /// <summary>Eine Zelle neu: je Form und Gesteinsart ein MultiMesh in der Stufe, die zuletzt galt (die nächste Prüfung stellt sie richtig).</summary>
    private void FelsZelleBauen(Felszelle zelle)
    {
        foreach (var (teil, _, _) in zelle.Teile) teil.QueueFree();
        zelle.Teile.Clear();
        zelle.Neu = false;
        if (zelle.Stuecke.Count == 0) return;
        var stufe = zelle.Stufe < 0 ? 1 : zelle.Stufe;
        var gruppen = new List<((string Form, string Gestein) Art, List<object> Stuecke)>();
        var summe = Vector3.Zero;
        foreach (var s in zelle.Stuecke)
        {
            var b = _felsBilder[s];
            var art = (b.Form, b.Gestein);
            var gruppe = gruppen.FindIndex(g => g.Art == art);
            if (gruppe < 0) gruppen.Add((art, new List<object> { s }));
            else gruppen[gruppe].Stuecke.Add(s);
        }
        // die Schuttsteine (F6) je Gestein, nach den Stücken der Zelle
        var schutt = new List<(string Gestein, List<Felsbild> Steine)>();
        foreach (var s in zelle.Stuecke)
        {
            var steine = FelsSchutt(_felsBilder[s]);
            if (steine.Count == 0) continue;
            var k = schutt.FindIndex(x => x.Gestein == steine[0].Gestein);
            if (k < 0) schutt.Add((steine[0].Gestein, steine));
            else schutt[k].Steine.AddRange(steine);
        }
        foreach (var (gestein, steine) in schutt)
        {
            if (FelsNetz(Felsboden.Schuttform, stufe) is not { } netz) continue;
            var vielfach = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = netz, InstanceCount = steine.Count,
            };
            for (var i = 0; i < steine.Count; i++)
            {
                vielfach.SetInstanceTransform(i, FelsLage(steine[i]));
                vielfach.SetInstanceCustomData(i, new Color(FelsTiefe(steine[i]), 0, 0, 0));
            }
            var teil = new MultiMeshInstance3D
            {
                Name = "Schutt", Multimesh = vielfach, MaterialOverride = FelsStoff(gestein), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = stufe < 2,
            };
            zelle.Knoten.AddChild(teil);
            zelle.Teile.Add((teil, Felsboden.Schuttform, true));
        }
        foreach (var ((form, gestein), stuecke) in gruppen)
        {
            if (FelsNetz(form, stufe) is not { } netz) continue;
            var vielfach = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = netz, InstanceCount = stuecke.Count,
            };
            for (var i = 0; i < stuecke.Count; i++)
            {
                var b = _felsBilder[stuecke[i]];
                var lage = FelsLage(b);
                vielfach.SetInstanceTransform(i, lage);
                vielfach.SetInstanceCustomData(i, new Color(FelsTiefe(b), 0, 0, 0));
                _felsLagen[stuecke[i]] = lage;
                _felsInstanz[stuecke[i]] = (vielfach, i);
                summe += lage.Origin;
            }
            var teil = new MultiMeshInstance3D
            {
                Name = form, Multimesh = vielfach, MaterialOverride = FelsStoff(gestein),
                CastShadow = stufe <= FelsSchattenBisStufe ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
            };
            zelle.Knoten.AddChild(teil);
            zelle.Teile.Add((teil, form, false));
        }
        zelle.Mitte = summe / Math.Max(1, zelle.Stuecke.Count);
    }

    /// <summary>
    /// Die Stufe der Feinheit für einen Zoom (bei der freien Kamera: die Höhe des Bildes im Abstand der Zelle). <paramref name="alt"/>: die Stufe, die
    /// gerade gilt – um sie herum ist die Grenze um <see cref="FelsSpielraum"/> verschoben, damit es beim Zoomen an der Grenze nicht flackert.
    /// </summary>
    private static int FelsStufe(float zoom, int alt)
    {
        float Grenze(float g, int darunter) => alt == darunter ? g * (1 + FelsSpielraum) : alt == darunter + 1 ? g * (1 - FelsSpielraum) : g;
        if (zoom <= Grenze(FelsFeinBis, 0)) return 0;
        return zoom <= Grenze(FelsMittelBis, 1) ? 1 : 2;
    }

    /// <summary>Jedes Bild: vorgemerkte Zellen bauen, Feinheit und Schatten je Zelle nach Zoom bzw. Abstand.</summary>
    private void FelsenBild()
    {
        if (_felsenKnoten is null) return;
        FelsenNachziehen();
        var kamera = StadtKamera;
        var ortho = kamera.Projection == Camera3D.ProjectionType.Orthogonal;
        var bildhoehe = ortho ? 0f : 2f * Mathf.Tan(Mathf.DegToRad(kamera.Fov) / 2f);
        var auge = kamera.GlobalPosition;
        foreach (var zelle in _felsZellen.Values)
        {
            var zoom = ortho ? kamera.Size : bildhoehe * auge.DistanceTo(zelle.Mitte);
            var stufe = FelsStufe(zoom, zelle.Stufe);
            if (stufe == zelle.Stufe) continue;
            zelle.Stufe = stufe;
            var schatten = stufe <= FelsSchattenBisStufe ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
            foreach (var (teil, form, schutt) in zelle.Teile)
            {
                if (FelsNetz(form, stufe) is { } netz) teil.Multimesh.Mesh = netz;
                teil.CastShadow = schutt ? GeometryInstance3D.ShadowCastingSetting.Off : schatten;
                if (schutt) teil.Visible = stufe < 2;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Spiel: Felsen aus der Karte
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Felsstücke der Karte (im Spiel; die Werkstatt baut sie aus dem Entwurf, <see cref="FelsenAufbauen"/>).</summary>
    private void FelsenAusKarte(Stadtkarte karte)
    {
        if (_entwurf is not null) return;
        var grundKarte = Felsformen.IstGestein(karte.Felsen?.Grundgestein) ? karte.Felsen!.Grundgestein! : Felsformen.GrundgesteinVorgabe;
        FelsbodenFarbe(grundKarte, karte.Felsen is not null); // Stadtansicht.Felsboden.cs: ohne Felsen bleibt der Boden wie bisher
        if (karte.Felsen?.Stuecke is not { Count: > 0 } stuecke) return;
        var grund = grundKarte;
        for (var i = 0; i < stuecke.Count; i++)
        {
            var s = stuecke[i];
            if (s?.Punkt is not { Length: 2 } p || Felsformen.Finde(s.Form) is null) continue;
            var gestein = Felsformen.IstGestein(s.Gestein) ? s.Gestein! : grund;
            FelsSetzen(i, new Felsbild(s.Form, gestein, p[0], p[1], s.Drehung, s.Massstab, s.Neigung, s.Einsinken,
                s.Kippen is { Length: 2 } k ? k[0] : 0, s.Kippen is { Length: 2 } k2 ? k2[1] : 0));
        }
        FelsenNachziehen();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Werkstatt: Stücke aus dem Entwurf, wählen, ziehen
    // ═════════════════════════════════════════════════════════════════════════

    private Felsbild FelsBildVon(EntwurfFels f, double? x = null, double? z = null) =>
        new(f.Form, _entwurf!.GesteinVon(f), x ?? f.X, z ?? f.Z, f.Drehung, f.Massstab, f.Neigung, f.Einsinken, f.KippenX, f.KippenZ);

    /// <summary>Beim Aufbau der Werkstatt: alle Stücke des Entwurfs.</summary>
    private void FelsenAufbauen()
    {
        if (_entwurf is null) return;
        FelsenKnotenAnlegen();
        foreach (var f in _entwurf.Felsen) FelsSetzen(f, FelsBildVon(f));
        FelsenNachziehen();
        FelsbodenFarbeNachEntwurf();
    }

    /// <summary>Ein Stück neu, geändert oder weg (<paramref name="weg"/>) – aus der Meldung des Entwurfs.</summary>
    private void FelsBauen(EntwurfFels f, bool weg)
    {
        if (weg || _entwurf is null || Felsformen.Finde(f.Form) is null) FelsWeg(f);
        else FelsSetzen(f, FelsBildVon(f));
        FelsbodenFarbeNachEntwurf();
        FelsMarkieren(f);
    }

    /// <summary>Das Grundgestein hat sich geändert: alle Stücke ohne eigenes Gestein bekommen das neue Material.</summary>
    private void FelsenGesteinNeu()
    {
        if (_entwurf is null) return;
        foreach (var f in _entwurf.Felsen) FelsSetzen(f, FelsBildVon(f));
        FelsbodenFarbeNachEntwurf();
    }

    /// <summary>Das Gelände hat sich in diesem Rechteck geändert: Die Zellen darin folgen dem Boden (aus <see cref="GelaendeNachziehen"/>).</summary>
    private void FelsenImBereich(Rect2 rect)
    {
        var weit = rect.Grow(20f); // größter Radius eines Stücks samt Schuttkegel (Grundfläche + 0,8 · Felsboden.KegelMax)
        foreach (var (ort, zelle) in _felsZellen)
        {
            if (!new Rect2(ort.I * FelsZelle, ort.J * FelsZelle, FelsZelle, FelsZelle).Intersects(weit)) continue;
            zelle.Neu = true;
            _felsZellenNeu = true;
        }
        if (WerkstattGewaehlt is EntwurfFels gewaehlt) FelsMarkieren(gewaehlt);
    }

    private MeshInstance3D? _felsMarke;

    /// <summary>Der goldene Ring unter dem gewählten Stück (nur eins ist gewählt); für andere Stücke nichts.</summary>
    private void FelsMarkieren(EntwurfFels f, Vector2? versatz = null)
    {
        if (!ReferenceEquals(WerkstattGewaehlt, f) || _entwurf?.Enthaelt(f) != true || Felsformen.Finde(f.Form) is not { } form)
        {
            if (WerkstattGewaehlt is not EntwurfFels && _felsMarke is not null) _felsMarke.Visible = false; // ein anderes gewähltes Stück behält seinen Ring
            return;
        }
        if (_felsMarke is null)
        {
            _felsMarke = new MeshInstance3D
            {
                Name = "FelsMarkierung", MaterialOverride = ImmerSichtbar(Transparent(Auswahlgold, 0.95f)),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_felsMarke);
        }
        var radius = Mathf.Max(0.8f, (float)(Math.Max(form.Breite, form.Tiefe) / 2 * f.Massstab));
        if (_felsMarke.Mesh is not TorusMesh ring || !Mathf.IsEqualApprox(ring.InnerRadius, radius))
            _felsMarke.Mesh = new TorusMesh { InnerRadius = radius, OuterRadius = radius + 0.3f, Rings = 32 };
        var (x, z) = ((float)f.X + (versatz?.X ?? 0), (float)f.Z + (versatz?.Y ?? 0));
        _felsMarke.Position = new Vector3(x, BodenHoehe(x, z) + 0.2f, z);
        _felsMarke.Visible = WerkstattTeilSichtbar(f);
    }

    /// <summary>
    /// Das Felsstück unter der Maus: zuerst der nächste Treffer des Blickstrahls in den Kasten eines Stücks, sonst das nächste Stück, in dessen
    /// Grundfläche der Bodenpunkt liegt (mindestens <paramref name="toleranz"/>). Null: keins (oder die Ebene „Baum- und Naturzonen“ ist aus).
    /// </summary>
    private EntwurfFels? FelsBei(Vector2 maus, Vector2 boden, float toleranz)
    {
        if (_entwurf is null || !WerkstattEbeneSichtbar(Werkstattebene.Natur)) return null;
        var kamera = StadtKamera;
        var von = kamera.ProjectRayOrigin(maus);
        var richtung = kamera.ProjectRayNormal(maus);
        EntwurfFels? beste = null;
        var naechst = float.MaxValue;
        foreach (var f in _entwurf.Felsen)
        {
            if (!_felsLagen.TryGetValue(f, out var lage) || FelsKasten(f.Form) is not { } kasten) continue;
            var umgekehrt = lage.AffineInverse();
            // Im Modell trifft o + d·t denselben Punkt wie von + richtung·t in der Welt: t ist für alle Stücke vergleichbar
            if (StrahlInKasten(kasten, umgekehrt * von, umgekehrt.Basis * richtung) is { } t && t < naechst) (beste, naechst) = (f, t);
        }
        // Nur, was vor dem Boden unter der Maus liegt (1 m Spielraum): nicht ein Stück hinter einer Kuppe, an dessen Kasten der Blick vorbeistreift
        var bisBoden = von.DistanceTo(new Vector3(boden.X, BodenHoehe(boden.X, boden.Y), boden.Y)) + 1f;
        if (beste is not null && naechst <= bisBoden) return beste;
        beste = null;
        var abstand = float.MaxValue;
        foreach (var f in _entwurf.Felsen)
        {
            var d = boden.DistanceTo(new Vector2((float)f.X, (float)f.Z));
            if (d <= Mathf.Max((float)f.Radius * 0.8f, toleranz) && d < abstand) (beste, abstand) = (f, d);
        }
        return beste;
    }

    /// <summary>Wo der Strahl o + d·t den Kasten zuerst trifft (t ≥ 0, Verfahren der Platten je Achse) – null: gar nicht.</summary>
    private static float? StrahlInKasten(Aabb kasten, Vector3 o, Vector3 d)
    {
        var (t0, t1) = (0f, float.MaxValue);
        for (var a = 0; a < 3; a++)
        {
            var (lo, hi) = (kasten.Position[a], kasten.End[a]);
            if (Mathf.Abs(d[a]) < 1e-8f)
            {
                if (o[a] < lo || o[a] > hi) return null;
                continue;
            }
            var (ta, tb) = ((lo - o[a]) / d[a], (hi - o[a]) / d[a]);
            if (ta > tb) (ta, tb) = (tb, ta);
            (t0, t1) = (Mathf.Max(t0, ta), Mathf.Min(t1, tb));
            if (t0 > t1) return null;
        }
        return t0;
    }

    /// <summary>Beim Ziehen: Die Instanz des Stücks steht vorläufig dort (der Entwurf ändert sich erst beim Loslassen); null: zurück an ihren Platz.</summary>
    private void FelsZiehenZeigen(EntwurfFels f, Vector2? versatz)
    {
        if (_entwurf is null || !_felsInstanz.TryGetValue(f, out var instanz)) return;
        var b = FelsBildVon(f, f.X + (versatz?.X ?? 0), f.Z + (versatz?.Y ?? 0));
        instanz.Vielfach.SetInstanceTransform(instanz.Index, FelsLage(b));
        FelsMarkieren(f, versatz);
    }

    /// <summary>Werkstatt: Felsen folgen der Ebene „Baum- und Naturzonen“.</summary>
    private void FelsenEbeneAnwenden(bool sichtbar)
    {
        if (_felsenKnoten is not null && _felsenKnoten.Visible != sichtbar) _felsenKnoten.Visible = sichtbar;
        if (_felsMarke is not null && !sichtbar) _felsMarke.Visible = false;
    }
}
