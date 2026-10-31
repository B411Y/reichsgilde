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
/// (<c>regenring.gdshader</c>).</item>
/// <item><b>Schutz der Wände</b> (Regen 4): Dasselbe Bild trägt in G die Höhe der nächsten Traufe und in B und A die Unterkanten der Fenster davor
/// (unterste und nächste Reihe, aus den Fensterscheiben der Modelle). Der Shader der Modelle (<c>wand_schutz</c> in wetter.gdshaderinc) hält die Wand
/// darunter trockener: unter der Traufe je nach Wind bis 1,8 m, unter jeder Fensterbank 0,2–0,7 m. Höhen in Stufen zu <see cref="TropfStufe"/> über
/// <see cref="_tropfBasis"/> (globales <c>traufe_basis</c>).</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    private const int TraufenPunkteMax = 192;

    /// <summary>Weiter herausgezoomt als so viel (Kamerahöhe in Metern) tropft nichts mehr: Tropfen wären Bildpunkte.</summary>
    private const float TraufenMaxZoom = 55f;

    /// <summary>Ab diesem Zoom blassen Tropfen, Einschläge und nasse Tropfkante aus, bei <see cref="TraufenMaxZoom"/> sind sie ganz weg (kein Aufpoppen).</summary>
    private const float TraufenBlassAb = 33f;

    /// <summary>Seitenlänge des Bilds der Tropfkante in Bildpunkten (RGBA8, 1 MB); es deckt je nach Zoom 48–160 m um den Blickpunkt. Muss zu <c>TRAUFE_BILD</c> in wetter.gdshaderinc passen.</summary>
    private const int TropfBild = 512;

    /// <summary>Abstand zur Traufkante (m), ab dem der Boden nicht mehr nass ist (Wert 255 im Bild); muss zu <c>TRAUFE_WEIT</c> in wetter.gdshaderinc passen.</summary>
    private const float TropfWeit = 1.2f;

    /// <summary>Meter je Stufe der Höhen im Bild (G, B, A); muss zu <c>TRAUFE_STUFE</c> in wetter.gdshaderinc passen. 255 Stufen reichen 38 m über die Basis.</summary>
    private const float TropfStufe = 0.15f;

    /// <summary>Tropfstellen am Giebel tropfen weniger als die Traufe: Ihr Fleck am Boden ist um so viel (m) kleiner.</summary>
    private const float TropfGiebelKleiner = 0.25f;

    private readonly List<(Node3D Modell, Vector3[] Punkte, (Vector3 A, Vector3 B)[] Kanten)> _traufModelle = new();
    private readonly Dictionary<Mesh, (Vector3[] Punkte, (Vector3 A, Vector3 B)[] Kanten)> _traufPunkte = new();

    /// <summary>Eine Fensterscheibe im Raum des Modells: Mitte der Unterkante, Richtung nach außen (waagerecht) und halbe Breite (m).</summary>
    private readonly record struct Fensterscheibe(MeshInstance3D Teil, Vector3 Fuss, Vector3 Aussen, float Halb);
    private readonly List<(Node3D Modell, Fensterscheibe[] Scheiben)> _fensterModelle = new();
    private readonly Dictionary<(Mesh, int), Bauteil[]> _scheibenJeNetz = new();
    private readonly List<(Vector2 Fuss, Vector2 Aussen, float Halb, float Hoehe)> _tropfFenster = new();
    private float _tropfBasis = float.NaN; // Höhe (m), ab der die Höhen im Bild zählen (zuletzt an die Shader gegeben)
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
    private readonly List<(Vector2 A, Vector2 B, float Kleiner, float Hoehe)> _tropfKanten = new();
    private Vector4 _tropfFeld = new(0, 0, 0, -1); // (Ecke x, Ecke z, 1 / Seitenlänge, Stärke), zuletzt an die Shader gegeben

    /// <summary>Merkt die Traufkanten der Dächer und die Fensterscheiben (<paramref name="fenster"/>: Teil und Fläche mit dem Material „Fenster“) eines eben eingesetzten Modells.</summary>
    private void TraufenMelden(Node3D modell, List<(MeshInstance3D Teil, int Flaeche)> fenster)
    {
        var scheiben = new List<Fensterscheibe>();
        foreach (var (teil, flaeche) in fenster)
        {
            if (teil.Mesh is not { } netz) continue;
            if (!_scheibenJeNetz.TryGetValue((netz, flaeche), out var teile))
                _scheibenJeNetz[(netz, flaeche)] = teile = Bauteile(netz, flaeche, Transform3D.Identity).Where(b => b.Groesse.Y > 0.3f && Mathf.Abs(b.Normale.Y) < 0.5f).ToArray();
            var lage = Transform3D.Identity;
            for (Node? k = teil; k is Node3D knoten && k != modell; k = k.GetParent()) lage = knoten.Transform * lage;
            foreach (var b in teile)
            {
                var aussen = lage.Basis * new Vector3(b.Normale.X, 0, b.Normale.Z);
                if (aussen.LengthSquared() < 0.1f) continue;
                aussen = aussen.Normalized();
                var quer = new Vector3(-aussen.Z, 0, aussen.X);
                var groesse = lage.Basis * b.Groesse;
                scheiben.Add(new Fensterscheibe(teil, lage * new Vector3(b.Mitte.X, b.Min.Y, b.Mitte.Z), aussen, (Mathf.Abs(groesse.X * quer.X) + Mathf.Abs(groesse.Z * quer.Z)) * 0.5f));
            }
        }
        if (_fensterModelle.Count > 300) _fensterModelle.RemoveAll(m => !IsInstanceValid(m.Modell));
        if (scheiben.Count > 0) _fensterModelle.Add((modell, scheiben.ToArray()));

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

        // Am Boden unter der Traufe: kleine Spritzer (regenring.gdshader) dort, wo die Tropfen landen; kurzlebige Partikel auf denselben
        // Stellen, am Boden statt am Dach. Die nasse Tropfkante selbst ist Nässe des Bodens (TropfkanteZeichnen, boden.gdshaderinc), kein Partikel.
        _traufRingStoff = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Points, Gravity = Vector3.Zero, ScaleMin = 0.45f, ScaleMax = 0.8f,
        };
        _traufRingFarbe = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/regenring.gdshader") };
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
    /// Nasse Tropfkante und Schutz der Wände: schreibt das Bild um den Blickpunkt neu – je Bildpunkt in R der Abstand zur nächsten Traufkante im
    /// Grundriss (0–<see cref="TropfWeit"/> m auf 0–255, weiter weg 255) und in G deren Höhe, in B und A die Unterkanten der Fenster davor. Es zählt der
    /// kleinste Abstand, nicht die Summe: Wo zwei Kanten nahe beieinanderliegen (Hausecken, Nachbarhäuser), wird der Boden nicht nässer als nass. Die Ecke
    /// des Bilds rastet auf ganze Bildpunkte ein, damit dieselbe Stelle beim Verschieben denselben Wert bekommt (kein Flimmern). Gezeichnet und
    /// hochgeladen wird nur, wenn sich etwas geändert hat (Prüfsumme über Kanten und Fenster im Bild); steht der Blick, kostet es nur das Umrechnen. Das
    /// Bild deckt die ganze Ansicht (die Kamera blickt diagonal: rund 2,7 × Zoom), auf 16 m gerundet, 48–160 m – je Bildpunkt 9–31 cm.
    /// </summary>
    private void TropfkanteZeichnen(Vector3 fokus)
    {
        var seite = Mathf.Clamp(Mathf.Ceil((2.7f * _kamera.Size + 8f) / 16f) * 16f, 48f, 160f);
        var texel = seite / TropfBild;
        var ecke = new Vector2(Mathf.Floor((fokus.X - seite / 2) / texel) * texel, Mathf.Floor((fokus.Z - seite / 2) / texel) * texel);
        // Höhen zählen ab 8 m unter dem Boden am Blickpunkt (auf ganze 4 m gerundet, damit sie beim Verschieben selten springen)
        var basis = Mathf.Floor((BodenHoehe(fokus.X, fokus.Z) - 8f) / 4f) * 4f;
        // erst die Kanten und Fenster im Bild sammeln (Grundriss) und ihre Prüfsumme bilden: Hat sich nichts geändert (Blick steht), bleibt das Bild, wie es ist
        _tropfKanten.Clear();
        _tropfFenster.Clear();
        var pruef = new HashCode();
        pruef.Add(seite);
        pruef.Add(ecke);
        pruef.Add(basis);
        bool Draussen(Vector3 o) => o.X < ecke.X - 40 || o.X > ecke.X + seite + 40 || o.Z < ecke.Y - 40 || o.Z > ecke.Y + seite + 40;
        foreach (var (modell, _, kanten) in _traufModelle)
        {
            if (!modell.IsVisibleInTree()) continue;
            var lage = modell.GlobalTransform;
            if (Draussen(lage.Origin)) continue; // grob: Modelle weit außerhalb des Bilds gar nicht erst umrechnen
            foreach (var (a, b) in kanten)
            {
                var wa = lage * a;
                var wb = lage * b;
                var pa = new Vector2(wa.X, wa.Z);
                var pb = new Vector2(wb.X, wb.Z);
                var weit = TropfWeit + 0.1f;
                if (Mathf.Max(pa.X, pb.X) < ecke.X - weit || Mathf.Min(pa.X, pb.X) > ecke.X + seite + weit
                    || Mathf.Max(pa.Y, pb.Y) < ecke.Y - weit || Mathf.Min(pa.Y, pb.Y) > ecke.Y + seite + weit) continue;
                _tropfKanten.Add((pa, pb, a == b ? TropfGiebelKleiner : 0f, Mathf.Min(wa.Y, wb.Y)));
                pruef.Add(pa);
                pruef.Add(pb);
            }
        }
        _fensterModelle.RemoveAll(m => !IsInstanceValid(m.Modell));
        foreach (var (modell, scheiben) in _fensterModelle)
        {
            if (!modell.IsVisibleInTree()) continue;
            var lage = modell.GlobalTransform;
            if (Draussen(lage.Origin)) continue;
            foreach (var sch in scheiben)
            {
                if (!sch.Teil.IsVisibleInTree()) continue; // andere Stufe oder Variante
                var fuss = lage * sch.Fuss;
                var aussen = (lage.Basis * sch.Aussen) with { Y = 0 };
                if (aussen.LengthSquared() < 1e-4f) continue;
                aussen = aussen.Normalized();
                var f = new Vector2(fuss.X, fuss.Z);
                if (f.X < ecke.X - 2 || f.X > ecke.X + seite + 2 || f.Y < ecke.Y - 2 || f.Y > ecke.Y + seite + 2) continue;
                _tropfFenster.Add((f, new Vector2(aussen.X, aussen.Z), sch.Halb, fuss.Y));
                pruef.Add(f);
                pruef.Add(fuss.Y);
            }
        }
        var summe = _tropfKanten.Count == 0 ? 0 : pruef.ToHashCode() | 1; // 0 heißt: keine Kante im Bild
        if (summe != _tropfPruefsumme && summe != 0)
        {
            _tropfDaten ??= new byte[TropfBild * TropfBild * 4];
            for (var i = 0; i < _tropfDaten.Length; i += 4)
            {
                _tropfDaten[i] = 255;
                _tropfDaten[i + 1] = _tropfDaten[i + 2] = _tropfDaten[i + 3] = 0;
            }
            // erst die Traufen (Abstand und Höhe), dann die Tropfstellen am Giebel (nur Abstand), dann die Fenster
            foreach (var (a, b, kleiner, hoehe) in _tropfKanten)
                if (a != b) KanteZeichnen(a, b, kleiner, HoehenStufe(hoehe, basis), ecke, texel);
            foreach (var (a, b, kleiner, _) in _tropfKanten)
                if (a == b) KanteZeichnen(a, b, kleiner, 0, ecke, texel);
            foreach (var (fuss, aussen, halb, hoehe) in _tropfFenster) FensterZeichnen(fuss, aussen, halb, HoehenStufe(hoehe, basis), ecke, texel);
            if (OS.GetEnvironment("RG_TRAUFE") != "") GD.Print($"[traufe] Bild neu: {_tropfKanten.Count} Kanten, {_tropfFenster.Count} Fenster, Basis {basis} m, Seite {seite} m");
            if (_tropfBild is null)
            {
                _tropfBild = Image.CreateFromData(TropfBild, TropfBild, false, Image.Format.Rgba8, _tropfDaten);
                _tropfTextur = ImageTexture.CreateFromImage(_tropfBild);
                RenderingServer.GlobalShaderParameterSet("traufe_maske", _tropfTextur);
            }
            else
            {
                _tropfBild.SetData(TropfBild, TropfBild, false, Image.Format.Rgba8, _tropfDaten);
                _tropfTextur!.Update(_tropfBild);
            }
            if (basis != _tropfBasis)
            {
                _tropfBasis = basis;
                RenderingServer.GlobalShaderParameterSet("traufe_basis", basis);
            }
        }
        _tropfPruefsumme = summe;
        TropfFeldSetzen(new Vector4(ecke.X, ecke.Y, 1f / seite, _tropfFeld.W));
    }

    /// <summary>Eine Höhe (m) als Stufe im Bild: 1–255 zu <see cref="TropfStufe"/> über <paramref name="basis"/> (0 heißt „keine“).</summary>
    private static byte HoehenStufe(float hoehe, float basis) => (byte)Mathf.Clamp(Mathf.RoundToInt((hoehe - basis) / TropfStufe) + 1, 1, 255);

    /// <summary>
    /// Eine Traufkante (Grundriss a–b, Welt-x/z) ins Bild: Abstand jedes nahen Bildpunkts zur Strecke plus <paramref name="kleiner"/>, der kleinere Wert
    /// gewinnt (R); mit ihm die Höhe der Traufe (G, <paramref name="hoehe"/> 0 = keine: Tropfstellen am Giebel), aber nur, wo das Lot auf die Kante fällt.
    /// </summary>
    private void KanteZeichnen(Vector2 a, Vector2 b, float kleiner, byte hoehe, Vector2 ecke, float texel)
    {
        var daten = _tropfDaten!;
        var weit = TropfWeit - kleiner;
        var i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.X, b.X) - weit - ecke.X) / texel));
        var i1 = Mathf.Min(TropfBild - 1, Mathf.CeilToInt((Mathf.Max(a.X, b.X) + weit - ecke.X) / texel));
        var j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.Y, b.Y) - weit - ecke.Y) / texel));
        var j1 = Mathf.Min(TropfBild - 1, Mathf.CeilToInt((Mathf.Max(a.Y, b.Y) + weit - ecke.Y) / texel));
        if (i0 > i1 || j0 > j1) return;
        var ab = b - a;
        var l2 = ab.LengthSquared();
        for (var j = j0; j <= j1; j++)
        {
            var z = ecke.Y + (j + 0.5f) * texel;
            for (var i = i0; i <= i1; i++)
            {
                var p = new Vector2(ecke.X + (i + 0.5f) * texel, z);
                var roh = l2 > 1e-6f ? (p - a).Dot(ab) / l2 : 0f;
                var t = Mathf.Clamp(roh, 0f, 1f);
                var d = (p - (a + ab * t)).Length() + kleiner;
                if (d >= TropfWeit) continue;
                var wert = (byte)Mathf.RoundToInt(d / TropfWeit * 255f);
                var stelle = (j * TropfBild + i) * 4;
                if (wert >= daten[stelle]) continue;
                daten[stelle] = wert;
                // Die Höhe gehört immer zur nächsten Kante: nur eine Traufe, deren Lot auf die Kante fällt (hinter ihrem Ende, an der Giebelwand quer dazu,
                // liegt keine Wand unter ihr); liegt eine Tropfstelle am Giebel oder das Ende einer Kante näher, gibt es hier keinen Schutz (0)
                daten[stelle + 1] = hoehe > 0 && roh >= 0f && roh <= 1f ? hoehe : (byte)0;
            }
        }
    }

    /// <summary>
    /// Ein Fenster ins Bild: vor der Fensterscheibe (bis 0,45 m nach außen, 0,2 m in die Laibung, so breit wie die Fensterscheibe und 5 cm mehr) ihre Unterkante als Höhe
    /// in B und A – B die unterste Reihe, A die nächste darüber (Unterkanten bis 0,6 m auseinander zählen als eine Reihe).
    /// </summary>
    private void FensterZeichnen(Vector2 fuss, Vector2 aussen, float halb, byte hoehe, Vector2 ecke, float texel)
    {
        var daten = _tropfDaten!;
        var quer = new Vector2(-aussen.Y, aussen.X);
        var r = halb + 0.5f;
        var i0 = Mathf.Max(0, Mathf.FloorToInt((fuss.X - r - ecke.X) / texel));
        var i1 = Mathf.Min(TropfBild - 1, Mathf.CeilToInt((fuss.X + r - ecke.X) / texel));
        var j0 = Mathf.Max(0, Mathf.FloorToInt((fuss.Y - r - ecke.Y) / texel));
        var j1 = Mathf.Min(TropfBild - 1, Mathf.CeilToInt((fuss.Y + r - ecke.Y) / texel));
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
            {
                var d = new Vector2(ecke.X + (i + 0.5f) * texel, ecke.Y + (j + 0.5f) * texel) - fuss;
                var vor = d.Dot(aussen);
                if (vor < -0.2f || vor > 0.45f || Mathf.Abs(d.Dot(quer)) > halb + 0.05f) continue;
                var stelle = (j * TropfBild + i) * 4;
                ref var unten = ref daten[stelle + 2];
                ref var naechste = ref daten[stelle + 3];
                if (unten == 0) unten = hoehe;
                else if (Math.Abs(hoehe - unten) <= 4) unten = Math.Min(unten, hoehe);
                else if (hoehe < unten) { naechste = unten; unten = hoehe; }
                else if (naechste == 0 || hoehe < naechste - 4) naechste = hoehe;
                else if (Math.Abs(hoehe - naechste) <= 4) naechste = Math.Min(naechste, hoehe);
            }
    }

    /// <summary>Lage und Stärke der nassen Tropfkante an die Shader (globales <c>traufe_feld</c>), nur wenn sie sich geändert haben.</summary>
    private void TropfFeldSetzen(Vector4 feld)
    {
        if (feld == _tropfFeld) return;
        _tropfFeld = feld;
        RenderingServer.GlobalShaderParameterSet("traufe_feld", feld);
    }
}
