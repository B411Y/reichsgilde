using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Pflanzen in den Reihen der Äcker (Teil der <c>partial class Stadtansicht</c>): Auf jedem Furchenkamm (Geometrie in <see cref="FeldNetzeBauen"/>) stehen Büschel
/// aus sieben Halmen. Reine Darstellung: Die Stücke kommen vom Kern (<see cref="FeldVerteilung"/>), Höhe und Farbe nach Jahreszeit rechnet
/// <c>feldpflanzen.gdshader</c> aus den globalen Jahreszeit-Anteilen (Frühling Saat, Sommer hohe Frucht, Herbst Stoppeln oder noch stehend, Winter Stümpfe).
/// Gebaut wird nach und nach (<see cref="FeldPflanzenBauer"/>): je Frucht und Zelle von <see cref="FeldPflanzenZelle"/> m ein MultiMesh, die Zellen nahe der Kamera zuerst,
/// höchstens ein paar Millisekunden je Bild – so wächst die Ladezeit nicht, und Godot zeichnet nur die Zellen im Bild.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Kantenlänge der Zellen (Meter), in die die Pflanzen je Frucht geteilt werden.</summary>
    private const float FeldPflanzenZelle = 24f;

    /// <summary>Abstand der Büschel längs einer Reihe (Meter); die Reihen sind die Kämme, <see cref="FeldFurchenAbstand"/> auseinander.</summary>
    private const float FeldPflanzenAbstand = 0.55f;

    /// <summary>Aussehen je Frucht: Höhe im Sommer (Meter), Farbe der Ähre bzw. Blüte, Anteil der Büschel, die stehen (Brache: lückenhaft).</summary>
    private static readonly Dictionary<string, (float Hoehe, Color Spitze, float Anteil)> PflanzenLook = new()
    {
        ["weizen"] = (0.95f, new Color(0.84f, 0.70f, 0.34f), 1f),
        ["roggen"] = (1.25f, new Color(0.72f, 0.64f, 0.34f), 1f),
        ["gerste"] = (0.85f, new Color(0.82f, 0.72f, 0.38f), 1f),
        ["hafer"] = (1.05f, new Color(0.64f, 0.64f, 0.34f), 1f),
        ["flachs"] = (0.8f, new Color(0.46f, 0.56f, 0.86f), 1f),
        ["brache"] = (0.35f, new Color(0.44f, 0.42f, 0.22f), 0.45f),
    };

    private static Shader? _pflanzenShader;
    private static ArrayMesh? _pflanzenNetz;
    private readonly Dictionary<string, ShaderMaterial> _pflanzenStoffe = new();

    private ShaderMaterial PflanzenStoff(string frucht)
    {
        if (_pflanzenStoffe.TryGetValue(frucht, out var stoff)) return stoff;
        var (hoehe, spitze, anteil) = PflanzenLook.GetValueOrDefault(frucht, PflanzenLook["brache"]);
        var (farbe, _) = FruchtLook.GetValueOrDefault(frucht, FruchtLook["brache"]);
        _pflanzenShader ??= GD.Load<Shader>("res://assets/shader/feldpflanzen.gdshader");
        stoff = new ShaderMaterial { Shader = _pflanzenShader, ResourceName = "Feldpflanzen " + frucht };
        stoff.SetShaderParameter("farbe", farbe);
        stoff.SetShaderParameter("spitze", spitze);
        stoff.SetShaderParameter("hoehe", hoehe);
        stoff.SetShaderParameter("anteil", anteil);
        stoff.SetShaderParameter("kammhoehe", FeldKammHoehe);
        return _pflanzenStoffe[frucht] = stoff;
    }

    /// <summary>
    /// Ein Büschel: sieben Halme (je ein Dreieck: zwei Fußpunkte am Ring, die Spitze darüber, nach außen geneigt). VERTEX.y = Höhe als Anteil (Spitzen 0,85–1,15),
    /// UV.x = Anteil der Höhe 0–1, NORMAL = Richtung nach außen.
    /// </summary>
    private static ArrayMesh PflanzenNetz()
    {
        if (_pflanzenNetz is not null) return _pflanzenNetz;
        const int n = 7;
        float[] spitzen = { 1.0f, 0.88f, 1.12f, 0.95f, 1.06f, 0.85f, 1.1f };
        var ecken = new List<Vector3>();
        var normalen = new List<Vector3>();
        var uv = new List<Vector2>();
        var indizes = new List<int>();
        for (var i = 0; i < n; i++) // Fußpunkte am Ring
        {
            var a = Mathf.Tau * i / n;
            ecken.Add(new Vector3(Mathf.Cos(a) * 0.10f, 0, Mathf.Sin(a) * 0.10f));
            normalen.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
            uv.Add(Vector2.Zero);
        }
        for (var i = 0; i < n; i++) // Spitzen zwischen zwei Fußpunkten
        {
            var a = Mathf.Tau * (i + 0.5f) / n;
            ecken.Add(new Vector3(Mathf.Cos(a) * 0.20f, spitzen[i], Mathf.Sin(a) * 0.20f));
            normalen.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
            uv.Add(new Vector2(1, 0));
            indizes.AddRange(new[] { i, n + i, (i + 1) % n }); // Fuß i, Spitze, Fuß i+1
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normalen.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indizes.ToArray();
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        netz.CustomAabb = new Aabb(new Vector3(-0.6f, -0.2f, -0.6f), new Vector3(1.2f, 2f, 1.2f));
        return _pflanzenNetz = netz;
    }

    /// <summary>Meldet die Stücke (Äcker des Kerns) und die Umrisse der Feldflächen dem Bauer, der die Pflanzen nach und nach setzt.</summary>
    private void FeldPflanzenSetzen(IReadOnlyList<Feldparzelle> parzellen, List<Vector2[]> polygone, Node3D eltern)
    {
        if (parzellen.Count == 0) return;
        eltern.AddChild(new FeldPflanzenBauer(parzellen, polygone, (x, z) => BodenHoehe(x, z), (BodenBewegt ? FeldHoeheAmHang : FeldHoehe) - 0.02f, PflanzenStoff, PflanzenNetz())
        { Name = "Pflanzen" });
    }
}

/// <summary>Baut die Pflanzen der Äcker eines Knotens Zelle für Zelle (nächste zur Kamera zuerst, höchstens <see cref="Millisekunden"/> je Bild).</summary>
public partial class FeldPflanzenBauer : Node3D
{
    private const double Millisekunden = 2.5;

    private readonly IReadOnlyList<Feldparzelle> _parzellen;
    private readonly List<Vector2[]> _polygone;
    private readonly Func<float, float, float> _hoehe;
    private readonly float _fuss;
    private readonly Func<string, ShaderMaterial> _stoff;
    private readonly Mesh _netz;
    /// <summary>Was noch fehlt: (Frucht, Zelle) → Nummern der Stücke, die in die Zelle reichen.</summary>
    private readonly Dictionary<(string Frucht, int I, int J), List<int>> _offen = new();

    public FeldPflanzenBauer() { _parzellen = Array.Empty<Feldparzelle>(); _polygone = new(); _hoehe = (_, _) => 0; _stoff = _ => new ShaderMaterial(); _netz = new ArrayMesh(); }

    public FeldPflanzenBauer(IReadOnlyList<Feldparzelle> parzellen, List<Vector2[]> polygone, Func<float, float, float> hoehe, float fuss, Func<string, ShaderMaterial> stoff, Mesh netz)
    {
        (_parzellen, _polygone, _hoehe, _fuss, _stoff, _netz) = (parzellen, polygone, hoehe, fuss, stoff, netz);
        const float z = 24f;
        for (var n = 0; n < parzellen.Count; n++)
        {
            var p = parzellen[n];
            var basis = new Basis(Vector3.Up, Mathf.DegToRad((float)p.Drehung));
            var (b, l) = ((float)p.Breite / 2, (float)p.Laenge / 2);
            var (min, max) = (new Vector2(float.MaxValue, float.MaxValue), new Vector2(float.MinValue, float.MinValue));
            foreach (var e in new[] { new Vector3(-b, 0, -l), new Vector3(b, 0, -l), new Vector3(b, 0, l), new Vector3(-b, 0, l) })
            {
                var w = basis * e;
                var q = new Vector2((float)p.X + w.X, (float)p.Z + w.Z);
                min = new Vector2(Mathf.Min(min.X, q.X), Mathf.Min(min.Y, q.Y));
                max = new Vector2(Mathf.Max(max.X, q.X), Mathf.Max(max.Y, q.Y));
            }
            for (var j = Mathf.FloorToInt(min.Y / z); j <= Mathf.FloorToInt(max.Y / z); j++)
                for (var i = Mathf.FloorToInt(min.X / z); i <= Mathf.FloorToInt(max.X / z); i++)
                {
                    if (!_offen.TryGetValue((p.Frucht, i, j), out var liste)) _offen[(p.Frucht, i, j)] = liste = new();
                    liste.Add(n);
                }
        }
    }

    public override void _Process(double delta)
    {
        if (_offen.Count == 0) { SetProcess(false); return; }
        var start = Time.GetTicksUsec();
        var kamera = GetViewport()?.GetCamera3D();
        var fokus = Vector2.Zero;
        if (kamera is not null)
        {
            var o = kamera.GlobalPosition;
            var v = -kamera.GlobalTransform.Basis.Z;
            var t = v.Y < -0.05f ? -o.Y / v.Y : 0f;
            fokus = new Vector2(o.X + v.X * t, o.Z + v.Z * t);
        }
        while (_offen.Count > 0 && (Time.GetTicksUsec() - start) / 1000.0 < Millisekunden)
        {
            var naechste = _offen.Keys.MinBy(k => (new Vector2((k.I + 0.5f) * 24f, (k.J + 0.5f) * 24f) - fokus).LengthSquared());
            var liste = _offen[naechste];
            _offen.Remove(naechste);
            Zelle(naechste.Frucht, naechste.I, naechste.J, liste);
        }
    }

    private static uint Hash(uint a, uint b, uint c)
    {
        var h = a * 374761393u + b * 668265263u + c * 2246822519u;
        h = (h ^ (h >> 13)) * 1274126177u;
        return h ^ (h >> 16);
    }

    private static float Zufall(uint h) => (h & 0xFFFFFF) / 16777216f;

    /// <summary>Eine Zelle einer Frucht: die Büschel aller Stücke darin, auf den Kämmen, nur in der Feldfläche und weg vom Rand.</summary>
    private void Zelle(string frucht, int zi, int zj, List<int> stuecke)
    {
        const float z = 24f;
        var puffer = new List<float>();
        foreach (var n in stuecke)
        {
            var p = _parzellen[n];
            var (b, l) = ((float)p.Breite / 2, (float)p.Laenge / 2);
            var basis = new Basis(Vector3.Up, Mathf.DegToRad((float)p.Drehung));
            var anzahl = Mathf.Max(1, Mathf.RoundToInt(2 * b / 0.8f));
            var reihe = 2 * b / anzahl;
            var stehend = frucht == "brache" || p.Variante % 2 == 0 ? 1f : 0f;
            var stich = 0.94f + 0.04f * ((p.Variante - 1) % 4);
            var seed = (uint)(int)(p.X * 10) * 31u + (uint)(int)(p.Z * 10);
            var schritte = (int)(2 * l / FeldAbstand);
            for (var k = 0; k < anzahl; k++)
                for (var m = 0; m <= schritte; m++)
                {
                    var h1 = Hash(seed, (uint)k, (uint)m);
                    var u = -b + (k + 0.5f) * reihe + (Zufall(h1) - 0.5f) * 0.1f;
                    var v = -l + m * FeldAbstand + (Zufall(h1 >> 3) - 0.5f) * 0.2f;
                    if (v > l) continue;
                    var w = basis * new Vector3(u, 0, v);
                    var (x, zz) = ((float)p.X + w.X, (float)p.Z + w.Z);
                    if (Mathf.FloorToInt(x / z) != zi || Mathf.FloorToInt(zz / z) != zj) continue;
                    if (!Innen(x, zz, 0.9f + 0.6f * Zufall(h1 >> 6))) continue;
                    var s = 0.85f + 0.4f * Zufall(h1 >> 9);
                    var dreh = Mathf.Tau * Zufall(h1 >> 12);
                    var (c, sn) = (Mathf.Cos(dreh) * s, Mathf.Sin(dreh) * s);
                    var y = _hoehe(x, zz) + _fuss;
                    var helle = stich * (0.9f + 0.2f * Zufall(h1 >> 15));
                    // Transform3D als zwölf Zahlen (Zeilen der Basis mit dem Ort) und die Farbe
                    puffer.AddRange(new[] { c, 0, sn, x, 0, s, 0, y, -sn, 0, c, zz, helle, helle, helle * (p.Variante % 2 == 0 ? 1.02f : 0.97f), stehend });
                }
        }
        if (puffer.Count == 0) return;
        var vielfach = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = _netz, InstanceCount = puffer.Count / 16,
        };
        vielfach.Buffer = puffer.ToArray();
        AddChild(new MultiMeshInstance3D
        {
            Name = $"Pflanzen_{frucht}_{zi}_{zj}", Multimesh = vielfach, MaterialOverride = _stoff(frucht), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    private const float FeldAbstand = 0.55f;

    /// <summary>Liegt der Punkt in einer Feldfläche und mindestens <paramref name="rand"/> Meter vom Rand entfernt?</summary>
    private bool Innen(float x, float z, float rand)
    {
        var q = new Vector2(x, z);
        foreach (var pg in _polygone)
        {
            var innen = false;
            var d = float.MaxValue;
            for (int i = 0, j = pg.Length - 1; i < pg.Length; j = i++)
            {
                var (a, c) = (pg[j], pg[i]);
                if ((c.Y > z) != (a.Y > z) && x < (a.X - c.X) * (z - c.Y) / (a.Y - c.Y) + c.X) innen = !innen;
                var ac = a - c;
                var t = Mathf.Clamp((q - c).Dot(ac) / Mathf.Max(ac.LengthSquared(), 1e-6f), 0f, 1f);
                d = Mathf.Min(d, (q - (c + ac * t)).LengthSquared());
            }
            if (innen && d >= rand * rand) return true;
        }
        return false;
    }
}
