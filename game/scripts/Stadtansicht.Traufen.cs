using Godot;

namespace Reichsgilde;

/// <summary>
/// Tropfen an den Traufen (PR #218, Teil der <c>partial class Stadtansicht</c>): Bei Regen und danach, solange die Dächer nass sind, tropft es an den
/// unteren Dachkanten. Nur Stimmung, nur nahe der Kamera (sparsam: höchstens <see cref="TraufenPunkteMax"/> Tropfstellen, ein einziges
/// <see cref="GpuParticles3D"/> mit Emissionspunkten).
/// <list type="bullet">
/// <item><b>Traufkante finden</b>: Aus dem Netz eines Dachs (Materialien <see cref="Dachstoffe"/>) kommen die waagerechten Kanten der schrägen Dreiecke, die am
/// niedrigsten liegen – das ist die Traufe. Je Netz einmal gerechnet und gemerkt (Modelle teilen ihre Netze), Punkte alle ~1,2 m.</item>
/// <item><b>Giebelkante</b>: Dazu ein paar Tropfstellen an den Randkanten der Dachflächen, die nicht waagerecht laufen (höchstens 8 je Netz, alle ~4 m – deutlich weniger als an der Traufe).</item>
/// <item><b>Jedes Modell</b> meldet sich beim Einsetzen (<see cref="TraufenMelden"/>); gezeigt werden die Punkte der Modelle in der Nähe des Blickpunkts.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    private const int TraufenPunkteMax = 192;

    /// <summary>Weiter herausgezoomt als so viel (Kamerahöhe in Metern) tropft nichts mehr: Tropfen wären Bildpunkte.</summary>
    private const float TraufenMaxZoom = 55f;

    private readonly List<(Node3D Modell, Vector3[] Punkte)> _traufModelle = new();
    private readonly Dictionary<Mesh, Vector3[]> _traufPunkte = new();
    private GpuParticles3D? _traufTropfen;
    private ParticleProcessMaterial? _traufStoff;
    private ShaderMaterial? _traufFarbe; // traufe.gdshader: einzelne dicke Tropfen, immer senkrecht
    private GpuParticles3D? _traufRinge, _traufNass; // Einschläge und nasse Tropfkante am Boden unter den Traufen
    private ParticleProcessMaterial? _traufRingStoff, _traufNassStoff;
    private ShaderMaterial? _traufRingFarbe;
    private double _traufUhr;
    private Vector3 _traufFokus = new(1e9f, 0, 1e9f);

    /// <summary>Merkt die Traufkanten der Dächer eines eben eingesetzten Modells.</summary>
    private void TraufenMelden(Node3D modell)
    {
        var alle = new List<Vector3>();
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not { } netz) continue;
            if (!_traufPunkte.TryGetValue(netz, out var punkte)) _traufPunkte[netz] = punkte = TraufPunkte(netz);
            if (punkte.Length == 0) continue;
            var lage = Transform3D.Identity; // Netz in den Raum des Modells (über die lokalen Lagen, das Modell muss noch nicht im Baum stehen)
            for (Node? k = teil; k is Node3D knoten && k != modell; k = k.GetParent()) lage = knoten.Transform * lage;
            foreach (var p in punkte) alle.Add(lage * p);
        }
        if (_traufModelle.Count > 300) _traufModelle.RemoveAll(m => !IsInstanceValid(m.Modell));
        if (alle.Count > 0) _traufModelle.Add((modell, alle.ToArray()));
    }

    /// <summary>Die Tropfstellen einer Traufe aus einem Netz (im Raum des Netzes): die tiefsten waagerechten Kanten der schrägen Dachdreiecke, alle ~1,2 m ein Punkt.</summary>
    private static Vector3[] TraufPunkte(Mesh netz)
    {
        var kanten = new List<(Vector3 A, Vector3 B)>();
        var schraegKanten = new Dictionary<((int, int, int), (int, int, int)), (Vector3 A, Vector3 B, int Zahl)>(); // alle Kanten der schrägen Dreiecke, Zahl = wie oft sie vorkommen
        var tiefstes = float.MaxValue;
        for (var f = 0; f < netz.GetSurfaceCount(); f++)
        {
            if (netz.SurfaceGetMaterial(f)?.ResourceName is not { } name || !Dachstoffe.Contains(name) || name.EndsWith("kante") || name.StartsWith("First")) continue;
            var daten = netz.SurfaceGetArrays(f);
            if (daten[(int)Mesh.ArrayType.Vertex].AsVector3Array() is not { Length: > 0 } ecken) continue;
            var index = daten[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? null : daten[(int)Mesh.ArrayType.Index].AsInt32Array();
            var anzahl = index?.Length ?? ecken.Length;
            for (var i = 0; i + 2 < anzahl; i += 3)
            {
                var v = index is null ? new[] { ecken[i], ecken[i + 1], ecken[i + 2] } : new[] { ecken[index[i]], ecken[index[i + 1]], ecken[index[i + 2]] };
                var n = (v[1] - v[0]).Cross(v[2] - v[0]);
                if (n.LengthSquared() < 1e-8f) continue;
                n = n.Normalized();
                if (Mathf.Abs(n.Y) is < 0.25f or > 0.93f) continue; // schräg: weder Wand noch flach
                for (var k = 0; k < 3; k++)
                {
                    var a = v[k];
                    var b = v[(k + 1) % 3];
                    var ka = Raster(a);
                    var kb = Raster(b);
                    var schluessel = ka.CompareTo(kb) <= 0 ? (ka, kb) : (kb, ka);
                    schraegKanten[schluessel] = schraegKanten.TryGetValue(schluessel, out var alt) ? (alt.A, alt.B, alt.Zahl + 1) : (a, b, 1);
                    if (Mathf.Abs(a.Y - b.Y) > 0.06f || (a - b).Length() < 0.4f) continue; // waagerechte, nicht winzige Kante
                    kanten.Add((a, b));
                    tiefstes = Mathf.Min(tiefstes, Mathf.Min(a.Y, b.Y));
                }
            }
        }
        var punkte = new List<Vector3>();
        var gesehen = new HashSet<(int, int, int)>();
        foreach (var (a, b) in kanten)
        {
            if (Mathf.Min(a.Y, b.Y) > tiefstes + 0.3f) continue;
            var schritte = Mathf.Max(1, Mathf.RoundToInt((a - b).Length() / 1.2f));
            for (var s = 0; s <= schritte; s++)
            {
                var p = a.Lerp(b, s / (float)schritte);
                if (gesehen.Add((Mathf.RoundToInt(p.X * 3), Mathf.RoundToInt(p.Y * 3), Mathf.RoundToInt(p.Z * 3)))) punkte.Add(p);
            }
        }
        var traufe = punkte.Count > 40 ? punkte.Where((_, i) => i % ((punkte.Count + 39) / 40) == 0).ToList() : punkte;
        // Giebelkante: Randkanten der Dachflächen (nur ein Dreieck), die nicht waagerecht laufen – deutlich weniger Tropfstellen als an der Traufe (alle ~4 m)
        var giebel = new List<Vector3>();
        foreach (var (a, b, zahl) in schraegKanten.Values)
        {
            if (zahl != 1 || Mathf.Abs(a.Y - b.Y) < 0.4f || (a - b).Length() < 1.5f) continue;
            var schritte = Mathf.Max(2, Mathf.RoundToInt((a - b).Length() / 4f)); // mindestens die Mitte
            for (var s = 1; s < schritte; s++) giebel.Add(a.Lerp(b, s / (float)schritte));
        }
        if (giebel.Count > 8) giebel = giebel.Where((_, i) => i % ((giebel.Count + 7) / 8) == 0).ToList();
        traufe.AddRange(giebel);
        return traufe.ToArray();
    }

    private static (int, int, int) Raster(Vector3 p) => (Mathf.RoundToInt(p.X * 50), Mathf.RoundToInt(p.Y * 50), Mathf.RoundToInt(p.Z * 50));

    private void TraufenEinrichten()
    {
        _traufStoff = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Points,
            Direction = Vector3.Down,
            Spread = 0, // senkrecht, auch bei Wind
            InitialVelocityMin = 0.1f,
            InitialVelocityMax = 0.4f,
            Gravity = new Vector3(0, -9.8f, 0),
            ScaleMin = 0.5f,
            ScaleMax = 0.9f,
        };
        _traufFarbe = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/traufe.gdshader") };
        _traufTropfen = new GpuParticles3D
        {
            Name = "Traufentropfen", Amount = 400, Lifetime = 0.8, LocalCoords = false, Emitting = false, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ProcessMaterial = _traufStoff,
            DrawPass1 = new QuadMesh { Size = Vector2.One, Material = _traufFarbe },
            VisibilityAabb = new Aabb(new Vector3(-200, -20, -200), new Vector3(400, 40, 400)),
        };
        AddChild(_traufTropfen);

        // Am Boden unter der Traufe: Ringe und Spritzer (regenring.gdshader) dort, wo die Tropfen landen, dazu ein weicher dunkler Fleck (traufnass.gdshader),
        // der dicht an dicht die nasse Tropfkante entlang der Hauswand ergibt. Kurzlebige Partikel auf den Emissionspunkten, am Boden statt am Dach.
        _traufRingStoff = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Points, Gravity = Vector3.Zero, ScaleMin = 0.45f, ScaleMax = 0.8f,
        };
        _traufRingFarbe = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/regenring.gdshader") };
        _traufRinge = new GpuParticles3D
        {
            Name = "Traufeinschlaege", Amount = 200, Lifetime = 0.5, LocalCoords = false, Emitting = false, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ProcessMaterial = _traufRingStoff,
            DrawPass1 = new QuadMesh { Size = Vector2.One, Orientation = PlaneMesh.OrientationEnum.Y, Material = _traufRingFarbe },
            VisibilityAabb = new Aabb(new Vector3(-200, -20, -200), new Vector3(400, 40, 400)),
        };
        AddChild(_traufRinge);
        _traufNassStoff = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Points, Gravity = Vector3.Zero, ScaleMin = 0.5f, ScaleMax = 0.9f,
        };
        _traufNass = new GpuParticles3D
        {
            Name = "Traufnaesse", Amount = 200, Lifetime = 3.0, Preprocess = 3.0, LocalCoords = false, Emitting = false, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ProcessMaterial = _traufNassStoff,
            DrawPass1 = new QuadMesh { Size = Vector2.One, Orientation = PlaneMesh.OrientationEnum.Y, Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/traufnass.gdshader") } },
            VisibilityAabb = new Aabb(new Vector3(-200, -20, -200), new Vector3(400, 40, 400)),
        };
        AddChild(_traufNass);
    }

    /// <summary>Jedes Bild: Tropfstellen der Dächer nahe dem Blickpunkt setzen (neu gesammelt, wenn der Blick wandert oder alle 0,5 s), Menge nach Nässe und Regen.</summary>
    private void TraufenStellen(double delta)
    {
        var stufe = Fenster.AktuelleWettereffekte;
        // nasse Dächer tropfen: bei Regen stark, danach mit dem Abtrocknen (Nässe) weniger; Niedrig: keine
        var staerke = stufe == Wettereffekte.Niedrig || _raum is not null || _innen is not null || _kamera.Size > TraufenMaxZoom
            ? 0f : Mathf.Clamp(_naesse * (0.25f + 1.1f * _wetter.Regen), 0, 1);
        if (staerke < 0.03f)
        {
            if (_traufTropfen is { Visible: true }) { _traufTropfen.Visible = false; _traufTropfen.Emitting = false; }
            foreach (var g in new[] { _traufRinge, _traufNass }) if (g is { Visible: true }) { g.Visible = false; g.Emitting = false; }
            return;
        }
        if (_traufTropfen is null) TraufenEinrichten();
        var tropfen = _traufTropfen!;
        var fokus = BlickpunktAmBoden();
        _traufUhr += delta;
        if (_traufUhr > 0.5 || new Vector2(fokus.X - _traufFokus.X, fokus.Z - _traufFokus.Z).Length() > 4f)
        {
            _traufUhr = 0;
            _traufFokus = fokus;
            _traufModelle.RemoveAll(m => !IsInstanceValid(m.Modell));
            var reichweite = Mathf.Max(18f, _kamera.Size * 0.8f);
            var welt = new List<Vector3>();
            foreach (var (modell, punkte) in _traufModelle)
            {
                if (!modell.IsVisibleInTree()) continue;
                var lage = modell.GlobalTransform;
                foreach (var p in punkte)
                {
                    var w = lage * p;
                    if (new Vector2(w.X - fokus.X, w.Z - fokus.Z).LengthSquared() < reichweite * reichweite) welt.Add(w);
                }
            }
            if (welt.Count > TraufenPunkteMax) welt = welt.Where((_, i) => i % ((welt.Count + TraufenPunkteMax - 1) / TraufenPunkteMax) == 0).ToList();
            if (welt.Count == 0)
            {
                tropfen.Visible = tropfen.Emitting = false;
                foreach (var g in new[] { _traufRinge, _traufNass }) if (g is not null) g.Visible = g.Emitting = false;
                return;
            }
            var bild = Image.CreateEmpty(welt.Count, 1, false, Image.Format.Rgbf);
            for (var i = 0; i < welt.Count; i++) bild.SetPixel(i, 0, new Color(welt[i].X, welt[i].Y - 0.05f, welt[i].Z));
            _traufStoff!.EmissionPointTexture = ImageTexture.CreateFromImage(bild);
            _traufStoff.EmissionPointCount = welt.Count;
            tropfen.Visible = true;
            tropfen.Emitting = true;
            // dieselben Stellen am Boden (Höhe des Geländes + 6 cm)
            var boden = Image.CreateEmpty(welt.Count, 1, false, Image.Format.Rgbf);
            for (var i = 0; i < welt.Count; i++) boden.SetPixel(i, 0, new Color(welt[i].X, BodenHoehe(welt[i].X, welt[i].Z) + 0.06f, welt[i].Z));
            var bodenTextur = ImageTexture.CreateFromImage(boden);
            foreach (var (g, m) in new[] { (_traufRinge!, _traufRingStoff!), (_traufNass!, _traufNassStoff!) })
            {
                m.EmissionPointTexture = bodenTextur;
                m.EmissionPointCount = welt.Count;
                g.Visible = true;
                g.Emitting = true;
            }
        }
        var hell = PartikelHelligkeit(); // unbeleuchtet: nachts dunkel
        _traufFarbe!.SetShaderParameter("farbe", new Color(0.82f * hell, 0.88f * hell, 0.96f * hell, 1f));
        var k = Mathf.Max(0.3f, _kamera.Size / BezugsZoom);
        _traufStoff!.ScaleMin = 0.5f * k;
        _traufStoff.ScaleMax = 0.9f * k;
        tropfen.AmountRatio = Mathf.Snapped(Mathf.Clamp(staerke, 0.1f, 1f), 0.05f);
        _traufRingFarbe!.SetShaderParameter("farbe", new Color(0.8f * hell, 0.85f * hell, 0.95f * hell, 1f));
        _traufRinge!.AmountRatio = _traufNass!.AmountRatio = tropfen.AmountRatio;
        var weit = Mathf.Max(1f, k * 1.5f); // Einschläge wachsen erst beim Herauszoomen mit (sonst Bildpunkte)
        _traufRingStoff!.ScaleMin = 0.8f * weit;
        _traufRingStoff.ScaleMax = 1.3f * weit;
        _traufNassStoff!.ScaleMin = 1.0f * weit;
        _traufNassStoff.ScaleMax = 1.7f * weit;
    }
}
