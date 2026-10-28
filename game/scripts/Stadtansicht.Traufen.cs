using Godot;

namespace Reichsgilde;

/// <summary>
/// Tropfen an den Traufen (PR #218, Teil der <c>partial class Stadtansicht</c>): Bei Regen und danach, solange die Dächer nass sind, tropft es an den
/// unteren Dachkanten. Nur Stimmung, nur nahe der Kamera (sparsam: höchstens <see cref="TraufenPunkteMax"/> Tropfstellen, ein einziges
/// <see cref="GpuParticles3D"/> mit Emissionspunkten).
/// <list type="bullet">
/// <item><b>Traufkante finden</b>: Aus dem Netz eines Dachs (Materialien <see cref="Dachstoffe"/>) kommen die waagerechten Kanten der schrägen Dreiecke, die am
/// niedrigsten liegen – das ist die Traufe. Je Netz einmal gerechnet und gemerkt (Modelle teilen ihre Netze), Punkte alle ~1,2 m.</item>
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
        return punkte.Count > 48 ? punkte.Where((_, i) => i % ((punkte.Count + 47) / 48) == 0).ToArray() : punkte.ToArray();
    }

    private void TraufenEinrichten()
    {
        _traufStoff = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Points,
            Direction = Vector3.Down,
            Spread = 4,
            InitialVelocityMin = 0.3f,
            InitialVelocityMax = 0.8f,
            Gravity = new Vector3(0, -9.8f, 0),
            ScaleMin = 0.5f,
            ScaleMax = 0.9f,
        };
        _traufTropfen = new GpuParticles3D
        {
            Name = "Traufentropfen", Amount = 400, Lifetime = 0.8, LocalCoords = false, Emitting = false, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ProcessMaterial = _traufStoff,
            DrawPass1 = new QuadMesh { Size = Vector2.One, Material = _regenFarbe },
            VisibilityAabb = new Aabb(new Vector3(-200, -20, -200), new Vector3(400, 40, 400)),
        };
        AddChild(_traufTropfen);
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
                return;
            }
            var bild = Image.CreateEmpty(welt.Count, 1, false, Image.Format.Rgbf);
            for (var i = 0; i < welt.Count; i++) bild.SetPixel(i, 0, new Color(welt[i].X, welt[i].Y - 0.05f, welt[i].Z));
            _traufStoff!.EmissionPointTexture = ImageTexture.CreateFromImage(bild);
            _traufStoff.EmissionPointCount = welt.Count;
            tropfen.Visible = true;
            tropfen.Emitting = true;
        }
        var k = Mathf.Max(0.3f, _kamera.Size / BezugsZoom);
        _traufStoff!.ScaleMin = 0.5f * k;
        _traufStoff.ScaleMax = 0.9f * k;
        tropfen.AmountRatio = Mathf.Snapped(Mathf.Clamp(staerke, 0.1f, 1f), 0.05f);
    }
}
