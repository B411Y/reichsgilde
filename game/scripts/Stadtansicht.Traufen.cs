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
/// <item><b>Nasse Tropfkante am Boden</b> (PR „Tropfkante“): kein Partikel, sondern Nässe des Bodens selbst. <see cref="TropfkanteZeichnen"/> schreibt um den
/// Blickpunkt ein Bild mit dem Abstand jedes Punkts zur nächsten Traufkante (Grundriss der Kanten); <c>boden.gdshaderinc</c> liest es (globales
/// <c>traufe_maske</c>, Lage und Stärke in <c>traufe_feld</c>) und macht daraus in <c>wetter.gdshaderinc</c> einen unregelmäßigen nassen Streifen mit kleinen
/// Pfützen und feinen Ringen. Es zählt der kleinste Abstand, nicht die Summe: Nichts wird nässer als nass. Am Boden selbst bleiben nur kleine Spritzer
/// (<c>regenring.gdshader</c> mit <c>traufe = true</c>).</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    private const int TraufenPunkteMax = 192;

    /// <summary>Weiter herausgezoomt als so viel (Kamerahöhe in Metern) tropft nichts mehr: Tropfen wären Bildpunkte.</summary>
    private const float TraufenMaxZoom = 55f;

    /// <summary>Ab diesem Zoom blassen Tropfen, Einschläge und nasse Tropfkante aus, bei <see cref="TraufenMaxZoom"/> sind sie ganz weg (kein Aufpoppen).</summary>
    private const float TraufenBlassAb = 33f;

    /// <summary>Seitenlänge des Bilds der Tropfkante in Bildpunkten (R8, 256 KB); es deckt je nach Zoom 48–160 m um den Blickpunkt.</summary>
    private const int TropfBild = 512;

    /// <summary>Abstand zur Traufkante (m), ab dem der Boden nicht mehr nass ist (Wert 255 im Bild); muss zu <c>TRAUFE_WEIT</c> in wetter.gdshaderinc passen.</summary>
    private const float TropfWeit = 1.2f;

    /// <summary>Tropfstellen am Giebel tropfen weniger als die Traufe: Ihr Fleck am Boden ist um so viel (m) kleiner.</summary>
    private const float TropfGiebelKleiner = 0.25f;

    private readonly List<(Node3D Modell, Vector3[] Punkte, (Vector3 A, Vector3 B)[] Kanten)> _traufModelle = new();
    private readonly Dictionary<Mesh, (Vector3[] Punkte, (Vector3 A, Vector3 B)[] Kanten)> _traufPunkte = new();
    private GpuParticles3D? _traufTropfen;
    private ParticleProcessMaterial? _traufStoff;
    private ShaderMaterial? _traufFarbe; // traufe.gdshader: einzelne dicke Tropfen, immer senkrecht
    private GpuParticles3D? _traufRinge; // Spritzer am Boden, wo die Tropfen aufschlagen
    private ParticleProcessMaterial? _traufRingStoff;
    private ShaderMaterial? _traufRingFarbe;
    private double _traufUhr;
    private float _traufBlass = 1f;
    private Vector3 _traufFokus = new(1e9f, 0, 1e9f);

    // Bild der nassen Tropfkante (Abstand zur nächsten Traufkante je Bildpunkt) und seine Lage
    private byte[]? _tropfDaten;
    private Image? _tropfBild;
    private ImageTexture? _tropfTextur;
    private int _tropfPruefsumme;
    private Vector4 _tropfFeld = new(0, 0, 0, -1); // (Ecke x, Ecke z, 1 / Seitenlänge, Stärke), zuletzt an die Shader gegeben

    /// <summary>Merkt die Traufkanten der Dächer eines eben eingesetzten Modells.</summary>
    private void TraufenMelden(Node3D modell)
    {
        var alle = new List<Vector3>();
        var alleKanten = new List<(Vector3 A, Vector3 B)>();
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not { } netz) continue;
            if (!_traufPunkte.TryGetValue(netz, out var traufe)) _traufPunkte[netz] = traufe = TraufPunkte(netz);
            if (traufe.Punkte.Length == 0) continue;
            var lage = Transform3D.Identity; // Netz in den Raum des Modells (über die lokalen Lagen, das Modell muss noch nicht im Baum stehen)
            for (Node? k = teil; k is Node3D knoten && k != modell; k = k.GetParent()) lage = knoten.Transform * lage;
            foreach (var p in traufe.Punkte) alle.Add(lage * p);
            foreach (var (a, b) in traufe.Kanten) alleKanten.Add((lage * a, lage * b));
        }
        if (_traufModelle.Count > 300) _traufModelle.RemoveAll(m => !IsInstanceValid(m.Modell));
        if (alle.Count > 0) _traufModelle.Add((modell, alle.ToArray(), alleKanten.ToArray()));
    }

    /// <summary>
    /// Die Tropfstellen einer Traufe aus einem Netz (im Raum des Netzes): die tiefsten waagerechten Kanten der schrägen Dachdreiecke, alle ~1,2 m ein Punkt.
    /// Dazu die Kanten selbst (für die nasse Tropfkante am Boden); eine Giebel-Tropfstelle ist eine Kante der Länge 0.
    /// </summary>
    private static (Vector3[] Punkte, (Vector3 A, Vector3 B)[] Kanten) TraufPunkte(Mesh netz)
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
        var traufKanten = new List<(Vector3 A, Vector3 B)>();
        var kantenGesehen = new HashSet<((int, int, int), (int, int, int))>();
        foreach (var (a, b) in kanten)
        {
            if (Mathf.Min(a.Y, b.Y) > tiefstes + 0.3f) continue;
            var ka = Raster(a);
            var kb = Raster(b);
            if (kantenGesehen.Add(ka.CompareTo(kb) <= 0 ? (ka, kb) : (kb, ka))) traufKanten.Add((a, b));
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
        traufKanten.AddRange(giebel.Select(g => (g, g)));
        return (traufe.ToArray(), traufKanten.ToArray());
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

        // Am Boden unter der Traufe: kleine Spritzer (regenring.gdshader, traufe = true) dort, wo die Tropfen landen; kurzlebige Partikel auf denselben
        // Stellen, am Boden statt am Dach. Die nasse Tropfkante selbst ist Nässe des Bodens (TropfkanteZeichnen, boden.gdshaderinc), kein Partikel.
        _traufRingStoff = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Points, Gravity = Vector3.Zero, ScaleMin = 0.45f, ScaleMax = 0.8f,
        };
        _traufRingFarbe = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/regenring.gdshader") };
        _traufRingFarbe.SetShaderParameter("traufe", true);
        _traufRinge = new GpuParticles3D
        {
            Name = "Traufspritzer", Amount = 160, Lifetime = 0.4, LocalCoords = false, Emitting = false, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ProcessMaterial = _traufRingStoff,
            DrawPass1 = new QuadMesh { Size = Vector2.One, Orientation = PlaneMesh.OrientationEnum.Y, Material = _traufRingFarbe },
            VisibilityAabb = new Aabb(new Vector3(-200, -20, -200), new Vector3(400, 40, 400)),
        };
        AddChild(_traufRinge);
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
            if (_traufRinge is { Visible: true }) { _traufRinge.Visible = false; _traufRinge.Emitting = false; }
            TropfFeldSetzen(_tropfFeld with { W = 0 });
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
            TropfkanteZeichnen(fokus);
            var reichweite = Mathf.Max(18f, _kamera.Size * 0.8f);
            var welt = new List<Vector3>();
            foreach (var (modell, punkte, _) in _traufModelle)
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
                if (_traufRinge is not null) _traufRinge.Visible = _traufRinge.Emitting = false;
            }
            else
            {
                var bild = Image.CreateEmpty(welt.Count, 1, false, Image.Format.Rgbf);
                for (var i = 0; i < welt.Count; i++) bild.SetPixel(i, 0, new Color(welt[i].X, welt[i].Y - 0.05f, welt[i].Z));
                _traufStoff!.EmissionPointTexture = ImageTexture.CreateFromImage(bild);
                _traufStoff.EmissionPointCount = welt.Count;
                tropfen.Visible = true;
                tropfen.Emitting = true;
                // dieselben Stellen am Boden (Höhe des Geländes + 6 cm)
                var boden = Image.CreateEmpty(welt.Count, 1, false, Image.Format.Rgbf);
                for (var i = 0; i < welt.Count; i++) boden.SetPixel(i, 0, new Color(welt[i].X, BodenHoehe(welt[i].X, welt[i].Z) + 0.06f, welt[i].Z));
                _traufRingStoff!.EmissionPointTexture = ImageTexture.CreateFromImage(boden);
                _traufRingStoff.EmissionPointCount = welt.Count;
                _traufRinge!.Visible = true;
                _traufRinge.Emitting = true;
            }
        }
        var hell = PartikelHelligkeit(); // unbeleuchtet: nachts dunkel
        // Beim Herauszoomen ausblassen (Tropfen, Spritzer und die nasse Kante behalten ihre Größe in der Welt und würden zu Bildpunkten)
        var blass = 1f - Mathf.SmoothStep(TraufenBlassAb, TraufenMaxZoom, _kamera.Size);
        _traufFarbe!.SetShaderParameter("farbe", new Color(0.82f * hell, 0.88f * hell, 0.96f * hell, blass));
        if (Mathf.Abs(blass - _traufBlass) > 0.01f) // nur bei merklicher Änderung an den RenderingServer
        {
            _traufBlass = blass;
            _traufRingFarbe!.SetShaderParameter("weit", blass);
        }
        // Nasse Tropfkante: Sie bleibt nach dem Regen länger als die Tropfen (der Boden darunter trocknet langsamer); ohne Kanten im Bild nichts
        var nass = _tropfPruefsumme == 0 ? 0f : Mathf.Clamp(_naesse * (0.6f + 0.6f * _wetter.Regen), 0, 1) * blass;
        TropfFeldSetzen(_tropfFeld with { W = Mathf.Snapped(nass, 0.01f) });
        var k = Mathf.Max(0.3f, _kamera.Size / BezugsZoom);
        _traufStoff!.ScaleMin = 0.5f * k;
        _traufStoff.ScaleMax = 0.9f * k;
        tropfen.AmountRatio = Mathf.Snapped(Mathf.Clamp(staerke, 0.1f, 1f), 0.05f);
        _traufRingFarbe!.SetShaderParameter("farbe", new Color(0.8f * hell, 0.85f * hell, 0.92f * hell, 0.42f));
        _traufRinge!.AmountRatio = tropfen.AmountRatio;
        var weit = Mathf.Max(1f, k * 1.5f); // Spritzer wachsen erst beim Herauszoomen mit (sonst Bildpunkte)
        _traufRingStoff!.ScaleMin = 0.4f * weit;
        _traufRingStoff.ScaleMax = 0.6f * weit;
    }

    /// <summary>
    /// Nasse Tropfkante: schreibt das Bild um den Blickpunkt neu – je Bildpunkt der Abstand zur nächsten Traufkante im Grundriss (0–<see cref="TropfWeit"/> m
    /// auf 0–255, weiter weg 255). Es zählt der kleinste Abstand, nicht die Summe: Wo zwei Kanten nahe beieinanderliegen (Hausecken, Nachbarhäuser), wird
    /// der Boden nicht nässer als nass. Die Ecke des Bilds rastet auf ganze Bildpunkte ein, damit dieselbe Stelle beim Verschieben denselben Wert bekommt
    /// (kein Flimmern). Hochgeladen wird nur, was sich geändert hat (Prüfsumme). Das Bild deckt die ganze Ansicht (die Kamera blickt diagonal: rund 2,7 ×
    /// Zoom), auf 16 m gerundet, 48–160 m – je Bildpunkt 9–31 cm.
    /// </summary>
    private void TropfkanteZeichnen(Vector3 fokus)
    {
        var seite = Mathf.Clamp(Mathf.Ceil((2.7f * _kamera.Size + 8f) / 16f) * 16f, 48f, 160f);
        var texel = seite / TropfBild;
        var ecke = new Vector2(Mathf.Floor((fokus.X - seite / 2) / texel) * texel, Mathf.Floor((fokus.Z - seite / 2) / texel) * texel);
        _tropfDaten ??= new byte[TropfBild * TropfBild];
        Array.Fill(_tropfDaten, (byte)255);
        var pruef = new HashCode();
        pruef.Add(seite);
        pruef.Add(ecke);
        var gezeichnet = 0;
        foreach (var (modell, _, kanten) in _traufModelle)
        {
            if (!modell.IsVisibleInTree()) continue;
            var lage = modell.GlobalTransform;
            var o = lage.Origin; // grob: Modelle weit außerhalb des Bilds gar nicht erst umrechnen
            if (o.X < ecke.X - 40 || o.X > ecke.X + seite + 40 || o.Z < ecke.Y - 40 || o.Z > ecke.Y + seite + 40) continue;
            foreach (var (a, b) in kanten)
            {
                var wa = lage * a;
                var wb = lage * b;
                if (KanteZeichnen(new Vector2(wa.X, wa.Z), new Vector2(wb.X, wb.Z), a == b ? TropfGiebelKleiner : 0f, ecke, texel))
                {
                    gezeichnet++;
                    pruef.Add(wa);
                    pruef.Add(wb);
                }
            }
        }
        var summe = gezeichnet == 0 ? 0 : pruef.ToHashCode() | 1; // 0 heißt: keine Kante im Bild
        var feld = new Vector4(ecke.X, ecke.Y, 1f / seite, _tropfFeld.W);
        if (summe != _tropfPruefsumme && summe != 0)
        {
            if (_tropfBild is null)
            {
                _tropfBild = Image.CreateFromData(TropfBild, TropfBild, false, Image.Format.R8, _tropfDaten);
                _tropfTextur = ImageTexture.CreateFromImage(_tropfBild);
                RenderingServer.GlobalShaderParameterSet("traufe_maske", _tropfTextur);
            }
            else
            {
                _tropfBild.SetData(TropfBild, TropfBild, false, Image.Format.R8, _tropfDaten);
                _tropfTextur!.Update(_tropfBild);
            }
        }
        _tropfPruefsumme = summe;
        TropfFeldSetzen(feld);
    }

    /// <summary>Eine Traufkante (Grundriss a–b, Welt-x/z) ins Bild: Abstand jedes nahen Bildpunkts zur Strecke plus <paramref name="kleiner"/>, der kleinere Wert gewinnt.</summary>
    private bool KanteZeichnen(Vector2 a, Vector2 b, float kleiner, Vector2 ecke, float texel)
    {
        var daten = _tropfDaten!;
        var weit = TropfWeit - kleiner;
        var i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.X, b.X) - weit - ecke.X) / texel));
        var i1 = Mathf.Min(TropfBild - 1, Mathf.CeilToInt((Mathf.Max(a.X, b.X) + weit - ecke.X) / texel));
        var j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.Y, b.Y) - weit - ecke.Y) / texel));
        var j1 = Mathf.Min(TropfBild - 1, Mathf.CeilToInt((Mathf.Max(a.Y, b.Y) + weit - ecke.Y) / texel));
        if (i0 > i1 || j0 > j1) return false;
        var ab = b - a;
        var l2 = ab.LengthSquared();
        for (var j = j0; j <= j1; j++)
        {
            var z = ecke.Y + (j + 0.5f) * texel;
            for (var i = i0; i <= i1; i++)
            {
                var p = new Vector2(ecke.X + (i + 0.5f) * texel, z);
                var t = l2 > 1e-6f ? Mathf.Clamp((p - a).Dot(ab) / l2, 0f, 1f) : 0f;
                var d = (p - (a + ab * t)).Length() + kleiner;
                if (d >= TropfWeit) continue;
                var wert = (byte)Mathf.RoundToInt(d / TropfWeit * 255f);
                var stelle = j * TropfBild + i;
                if (wert < daten[stelle]) daten[stelle] = wert;
            }
        }
        return true;
    }

    /// <summary>Lage und Stärke der nassen Tropfkante an die Shader (globales <c>traufe_feld</c>), nur wenn sie sich geändert haben.</summary>
    private void TropfFeldSetzen(Vector4 feld)
    {
        if (feld == _tropfFeld) return;
        _tropfFeld = feld;
        RenderingServer.GlobalShaderParameterSet("traufe_feld", feld);
    }
}
