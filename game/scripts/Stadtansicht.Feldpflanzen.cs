using System.Threading.Tasks;
using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Pflanzen in den Reihen der Äcker (Teil der <c>partial class Stadtansicht</c>): Auf jedem Furchenkamm (Geometrie in <see cref="FeldNetzeBauen"/>) stehen Büschel
/// aus sieben Halmen. Reine Darstellung: Die Stücke kommen vom Kern (<see cref="FeldVerteilung"/>), Höhe und Farbe nach Jahreszeit rechnet
/// <c>feldpflanzen.gdshader</c> aus den globalen Jahreszeit-Anteilen (Frühling Saat, Sommer hohe Frucht, Herbst Stoppeln oder noch stehend, Winter Stümpfe).
/// Gebaut wird beim Laden (<see cref="FeldPflanzenBauer"/>, parallel gerechnet): je Frucht und Zelle von <see cref="FeldPflanzenZelle"/> m ein MultiMesh – Godot zeichnet nur die Zellen im Bild.
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
    private void FeldPflanzenSetzen(IReadOnlyList<Feldparzelle> parzellen, Feldmaske maske, Node3D eltern)
    {
        if (parzellen.Count == 0) return;
        eltern.AddChild(new FeldPflanzenBauer(parzellen, maske, (x, z) => BodenHoehe(x, z), (BodenBewegt ? FeldHoeheAmHang : FeldHoehe) - 0.02f, PflanzenStoff, PflanzenNetz())
        { Name = "Pflanzen" });
    }
}

/// <summary>
/// Baut die Pflanzen der Äcker eines Knotens ganz im Aufbau (Ladebildschirm): Die Büschel aller Stücke werden auf allen Prozessorkernen gerechnet (reine Zahlen,
/// <see cref="Parallel"/>), danach legt der Hauptfaden je Frucht und Zelle ein MultiMesh an – beim ersten Bild stehen alle Pflanzen. Am Rand der Feldfläche
/// (<see cref="Feldrain"/> Meter) laufen sie aus: spärlicher, kleiner, heller und mit Randgras und Unkraut dazwischen, ein Teil davon schon außerhalb der Fläche.
/// Instanzfarbe: RGB Farbstich, A = (Frucht steht im Herbst noch + Randanteil 0–1) / 2, Randanteil 0,995 = reines Unkraut (feldpflanzen.gdshader).
/// </summary>
public partial class FeldPflanzenBauer : Node3D
{
    /// <summary>Breite des Randstreifens in Metern: darin laufen Kämme, Furchen und Pflanzen aus (acker.gdshader hat dieselbe Breite).</summary>
    public const float Feldrain = 1.0f;

    private const float Zelle = 24f, Reihe = 0.55f;

    private readonly IReadOnlyList<Feldparzelle> _parzellen;
    private readonly Feldmaske _maske;
    private readonly Func<float, float, float> _hoehe;
    private readonly float _fuss;
    private readonly Func<string, ShaderMaterial> _stoff;
    private readonly Mesh _netz;

    public FeldPflanzenBauer() { _parzellen = Array.Empty<Feldparzelle>(); _maske = new Feldmaske(Array.Empty<Feldparzelle>(), Array.Empty<Vector2[]>()); _hoehe = (_, _) => 0; _stoff = _ => new ShaderMaterial(); _netz = new ArrayMesh(); }

    public FeldPflanzenBauer(IReadOnlyList<Feldparzelle> parzellen, Feldmaske maske, Func<float, float, float> hoehe, float fuss, Func<string, ShaderMaterial> stoff, Mesh netz)
    {
        (_parzellen, _maske, _hoehe, _fuss, _stoff, _netz) = (parzellen, maske, hoehe, fuss, stoff, netz);
    }

    public override void _Ready()
    {
        var anfang = Time.GetTicksMsec();
        var uhr = Ladezeit.Uhr();
        _hoehe(0, 0); // Gelände einmal im Hauptfaden anfragen (rechnet seine Höhen bei Bedarf), danach liest es jeder Faden nur
        var je = new Dictionary<(string Frucht, int I, int J), List<float>>[_parzellen.Count];
        Parallel.For(0, _parzellen.Count, n => je[n] = Stueck(n));
        var alle = new SortedDictionary<(string Frucht, int I, int J), List<float>>();
        foreach (var d in je)
            foreach (var (schluessel, liste) in d)
            {
                if (!alle.TryGetValue(schluessel, out var l)) alle[schluessel] = l = new();
                l.AddRange(liste);
            }
        var buescheln = 0;
        foreach (var ((frucht, zi, zj), puffer) in alle)
        {
            var vielfach = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = _netz, InstanceCount = puffer.Count / 16,
            };
            vielfach.Buffer = puffer.ToArray();
            buescheln += puffer.Count / 16;
            AddChild(new MultiMeshInstance3D
            {
                Name = $"Pflanzen_{frucht}_{zi}_{zj}", Multimesh = vielfach, MaterialOverride = _stoff(frucht), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
        Ladezeit.Dazu("Feldpflanzen", uhr);
        GD.Print($"[felder] Pflanzen fertig: {alle.Count} Zellen, {buescheln} Büschel, {Time.GetTicksMsec() - anfang} ms im Aufbau");
    }

    private static uint Hash(uint a, uint b, uint c)
    {
        var h = a * 374761393u + b * 668265263u + c * 2246822519u;
        h = (h ^ (h >> 13)) * 1274126177u;
        return h ^ (h >> 16);
    }

    private static float Zufall(uint h) => (h & 0xFFFFFF) / 16777216f;

    /// <summary>Weiches Wertrauschen 0–1 über der Welt (für die unregelmäßige Kante des Randes).</summary>
    private static float Rausch(float x, float z)
    {
        var (i, j) = (Mathf.FloorToInt(x), Mathf.FloorToInt(z));
        var (fx, fz) = (x - i, z - j);
        (fx, fz) = (fx * fx * (3 - 2 * fx), fz * fz * (3 - 2 * fz));
        float W(int a, int b) => Zufall(Hash((uint)a, (uint)b, 77u));
        return Mathf.Lerp(Mathf.Lerp(W(i, j), W(i + 1, j), fx), Mathf.Lerp(W(i, j + 1), W(i + 1, j + 1), fx), fz);
    }

    /// <summary>Die Büschel eines Stücks je Zelle: Frucht auf den Kämmen (ausgedünnt zum Rand), dazwischen Randgras und Unkraut am Rand.</summary>
    private Dictionary<(string, int, int), List<float>> Stueck(int n)
    {
        var ergebnis = new Dictionary<(string, int, int), List<float>>();
        var p = _parzellen[n];
        var (b, l) = ((float)p.Breite / 2, (float)p.Laenge / 2);
        var basis = new Basis(Vector3.Up, Mathf.DegToRad((float)p.Drehung));
        var anzahl = Mathf.Max(1, Mathf.RoundToInt(2 * b / 0.8f));
        var reihe = 2 * b / anzahl;
        var stehend = p.Frucht == "brache" || p.Variante % 2 == 0 ? 1f : 0f;
        var stich = 0.97f + 0.025f * ((p.Variante - 1) % 4);
        var seed = (uint)(int)(p.X * 10) * 31u + (uint)(int)(p.Z * 10);
        var schritte = (int)(2 * l / Reihe);

        void Dazu(float x, float zz, float s, float dreh, float helle, float rest)
        {
            var schluessel = (p.Frucht, Mathf.FloorToInt(x / Zelle), Mathf.FloorToInt(zz / Zelle));
            if (!ergebnis.TryGetValue(schluessel, out var puffer)) ergebnis[schluessel] = puffer = new();
            var (c, sn) = (Mathf.Cos(dreh) * s, Mathf.Sin(dreh) * s);
            var y = _hoehe(x, zz) + _fuss;
            // Transform3D als zwölf Zahlen (Zeilen der Basis mit dem Ort) und die Farbe
            puffer.AddRange(new[] { c, 0, sn, x, 0, s, 0, y, -sn, 0, c, zz, helle, helle, helle * (p.Variante % 2 == 0 ? 1.02f : 0.97f), (stehend + rest) / 2f });
        }

        for (var k = -1; k <= anzahl; k++) // eine Reihe und ein Schritt über das Stück hinaus: Unkraut wächst auch knapp außerhalb des Randes
            for (var m = -1; m <= schritte + 1; m++)
            {
                var h1 = Hash(seed, (uint)k, (uint)m);
                // Frucht auf dem Kamm
                var v = -l + m * Reihe + (Zufall(h1 >> 3) - 0.5f) * 0.2f;
                if (k >= 0 && k < anzahl && m >= 0 && v <= l)
                {
                    var u = -b + (k + 0.5f) * reihe + (Zufall(h1) - 0.5f) * 0.1f;
                    var w = basis * new Vector3(u, 0, v);
                    var (x, zz) = ((float)p.X + w.X, (float)p.Z + w.Z);
                    var d = _maske.Abstand(x, zz);
                    if (d > 0.2f)
                    {
                        // Rand: weiche, unregelmäßige Kante – Dichte und Größe wachsen mit dem Abstand, Rauschen verschiebt die Grenze von Ort zu Ort
                        var rand = 1f - Mathf.SmoothStep(0.1f, Feldrain, d); // 1 am Rand, 0 innen (wie acker.gdshader für die Kämme)
                        var t = Mathf.SmoothStep(0.15f, Feldrain, d + (Rausch(x * 0.45f, zz * 0.45f) - 0.5f) * 1.0f);
                        if (Zufall(h1 >> 6) < Mathf.Lerp(0.1f, 1f, t))
                            Dazu(x, zz, (0.85f + 0.4f * Zufall(h1 >> 9)) * Mathf.Lerp(0.55f, 1f, t), Mathf.Tau * Zufall(h1 >> 12), stich * (0.96f + 0.08f * Zufall(h1 >> 15)), Mathf.Min(rand, 0.97f));
                    }
                }
                // Randgras und Unkraut: zwischen den Reihen, innen im Randstreifen und knapp außerhalb der Fläche
                var h2 = Hash(seed ^ 0x9E3779B9u, (uint)k, (uint)m);
                var v2 = -l + (m + 0.5f) * Reihe + (Zufall(h2 >> 3) - 0.5f) * 0.3f;
                if (v2 > l + Reihe) continue;
                var u2 = -b + (k + (Zufall(h2) > 0.5f ? 0.15f : 0.85f)) * reihe + (Zufall(h2 >> 5) - 0.5f) * 0.3f;
                var w2 = basis * new Vector3(u2, 0, v2);
                var (x2, z2) = ((float)p.X + w2.X, (float)p.Z + w2.Z);
                var d2 = _maske.Abstand(x2, z2);
                if (d2 < -0.6f || d2 > Feldrain + 0.4f) continue;
                var dichte = (1f - Mathf.SmoothStep(-0.6f, Feldrain, d2)) * (0.25f + 0.55f * Rausch(x2 * 0.6f + 31f, z2 * 0.6f));
                if (Zufall(h2 >> 8) >= dichte) continue;
                Dazu(x2, z2, 0.55f + 0.5f * Zufall(h2 >> 11), Mathf.Tau * Zufall(h2 >> 14), 0.9f + 0.2f * Zufall(h2 >> 17), 0.995f);
            }
        return ergebnis;
    }
}
