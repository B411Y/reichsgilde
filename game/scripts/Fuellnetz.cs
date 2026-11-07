using Godot;

namespace Reichsgilde;

/// <summary>
/// Backt viele Kopien kleiner Modelle zu <b>einem</b> Netz mit einer Fläche je Stoff (Restflächen-Füllung, <c>Stadtansicht.Restflaechen.cs</c>).
///
/// Warum: Ein MultiMesh zeichnet eine Art mit jeder ihrer Flächen (Stoffe) einmal – auch wenn es nur drei Kopien sind. Eine Zelle mit 40 Arten und
/// rund 3 Stoffen je Art kostete so über 100 Zeichenaufrufe. Das gebackene Netz zeichnet jeden Stoff der Zelle <i>einmal</i>, gleich wie viele Arten ihn benutzen.
/// Die Eckpunkte stehen danach in Weltlage (das Netz sitzt im Ursprung der Stadtansicht).
///
/// Gleiche Stoffe (gleicher Name und gleiche Werte, auch aus verschiedenen Baukästen) kommen in eine Fläche (<see cref="StoffNummer"/>); Flächen mit
/// verschiedenem Aufbau der Eckenfelder (mit/ohne Farbe, UV, Tangenten) bleiben getrennt, damit sich nichts ändert.
/// </summary>
internal sealed class Fuellnetz
{
    /// <summary>Die Felder einer Fläche des Quellnetzes, einmal aus dem Netz gelesen.</summary>
    internal sealed class Quelle
    {
        public Vector3[] Ecken = Array.Empty<Vector3>();
        public Vector3[]? Normalen;
        public float[]? Tangenten; // x, y, z, w je Ecke
        public Color[]? Farben;
        public Vector2[]? Uv, Uv2;
        public int[] Indizes = Array.Empty<int>();
        public Material? Stoff;
        public int StoffNr, Aufbau; // Nummer des Stoffs (gleiche Werte, gleiche Nummer) und Bitmaske der vorhandenen Eckenfelder
    }

    internal sealed class Gruppe
    {
        public Material? Stoff;
        public bool Normalen, Tangenten, Farben, Uv, Uv2;
        public readonly List<(Quelle Quelle, Transform3D Lage)> Teile = new();
        public int Ecken, Indizes;
    }

    private readonly Dictionary<Mesh, Quelle[]> _quellen = new();
    private readonly Dictionary<Material, int> _stoffNummern = new();
    private readonly Dictionary<string, Material> _stoffeNachName = new(); // der erste gesehene Stoff je Name (Ziel der Ersatzstoffe)
    private readonly Dictionary<string, int> _stoffSchluessel = new();
    private readonly List<string> _vollSchluessel = new();

    /// <summary>Stoffe, die der Kurzschlüssel für gleich hielt, die sich aber in einem gespeicherten Wert unterscheiden (sollte 0 sein; nur gezählt, wenn <see cref="Pruefen"/> an ist).</summary>
    public int Widersprueche { get; private set; }

    /// <summary>Vergleicht gleich geschlüsselte Stoffe Eigenschaft für Eigenschaft (kostet Zeit, nur für die Analyse mit <c>--fuellung=analyse</c>).</summary>
    public bool Pruefen { get; set; }

    /// <summary>Wie viele verschiedene Stoffe es gibt.</summary>
    public int Stoffe => _stoffSchluessel.Count;

    /// <summary>Ein Sammelplatz für ein Netz: erst <see cref="Hinzu"/>, dann <see cref="Backen"/>.</summary>
    public sealed class Sammler
    {
        internal readonly Dictionary<(int Stoff, int Aufbau), Gruppe> Gruppen = new();
        internal readonly List<Gruppe> Reihenfolge = new();
        public int Ecken { get; internal set; }
    }

    /// <summary>Nur einfache Stoffe (ohne Textur, undurchsichtig) dürfen Ersatz sein oder ersetzt werden: sonst änderte sich das Aussehen (Textur, Alphamaske der Blätter).</summary>
    private static bool Ersetzbar(Material? stoff) => stoff is BaseMaterial3D { AlbedoTexture: null, Transparency: BaseMaterial3D.TransparencyEnum.Disabled };

    /// <summary>Liest ein Netz aus (einmal je Netz): danach kennt <see cref="Hinzu"/> seine Stoffe als mögliche Ziele von Ersatzstoffen.</summary>
    public void Vorlesen(Mesh netz) => QuellenVon(netz);

    /// <summary>
    /// Merkt eine Kopie des Netzes (alle Flächen) in der Lage vor. <paramref name="ersatz"/> (Stoffname → Stoffname) nimmt für seltene Stoffe einen ähnlichen häufigen – damit hat ein weit
    /// entferntes Bildnetz weniger Flächen (Zeichenaufrufe); das Ziel muss vorher mit <see cref="Vorlesen"/> gelesen worden sein, sonst bleibt der Stoff.
    /// </summary>
    public void Hinzu(Sammler sammler, Mesh netz, Transform3D lage, IReadOnlyDictionary<string, string>? ersatz = null)
    {
        foreach (var q in QuellenVon(netz))
        {
            var stoff = q.Stoff;
            var nummer = q.StoffNr;
            if (ersatz is not null && Ersetzbar(stoff))
            {
                if (ersatz.TryGetValue(stoff!.ResourceName, out var ziel) && _stoffeNachName.TryGetValue(ziel, out var zielStoff))
                    (stoff, nummer) = (zielStoff, StoffNummer(zielStoff));
            }
            var aufbau = ersatz is not null && Ersetzbar(stoff) ? 0 : q.Aufbau; // im Ersatzmodus (ferne Netze) einfache Stoffe unabhängig vom Aufbau der Eckenfelder in einer Fläche (Fehlendes füllt FlaecheBacken mit Vorgaben)
            if (!sammler.Gruppen.TryGetValue((nummer, aufbau), out var g))
            {
                g = new Gruppe { Stoff = stoff };
                sammler.Gruppen[(nummer, aufbau)] = g;
                sammler.Reihenfolge.Add(g);
            }
            g.Normalen |= q.Normalen is not null;
            g.Tangenten |= q.Tangenten is not null;
            g.Farben |= q.Farben is not null;
            g.Uv |= q.Uv is not null;
            g.Uv2 |= q.Uv2 is not null;
            g.Teile.Add((q, lage));
            g.Ecken += q.Ecken.Length;
            g.Indizes += q.Indizes.Length;
            sammler.Ecken += q.Ecken.Length;
        }
    }

    /// <summary>Das gebackene Netz; null, wenn nichts gesammelt wurde. Je (Stoff, Aufbau) eine Fläche, in der Reihenfolge des ersten Auftretens.</summary>
    public ArrayMesh? Backen(Sammler sammler)
    {
        if (sammler.Reihenfolge.Count == 0) return null;
        var netz = new ArrayMesh();
        foreach (var g in sammler.Reihenfolge) FlaecheBacken(netz, g);
        return netz;
    }

    /// <summary>Backt eine Fläche (alle Teile eines Stoffs) und hängt sie ans Netz – einzeln aufrufbar, damit ein großes Netz über mehrere Bilder entstehen kann.</summary>
    public void FlaecheBacken(ArrayMesh netz, Gruppe g)
    {
        {
            var ecken = new Vector3[g.Ecken];
            var normalen = g.Normalen ? new Vector3[g.Ecken] : null;
            var tangenten = g.Tangenten ? new float[g.Ecken * 4] : null;
            var farben = g.Farben ? new Color[g.Ecken] : null;
            var uv = g.Uv ? new Vector2[g.Ecken] : null;
            var uv2 = g.Uv2 ? new Vector2[g.Ecken] : null;
            var indizes = new int[g.Indizes];
            int e = 0, i = 0;
            foreach (var (q, lage) in g.Teile)
            {
                var b = lage.Basis;
                var d = b.Orthonormalized(); // Normalen und Tangenten nur drehen (gleichmäßige Größe, keine Verzerrung)
                var (r0, r1, r2, o) = (b.Row0, b.Row1, b.Row2, lage.Origin);
                var (d0, d1, d2) = (d.Row0, d.Row1, d.Row2);
                var quelle = q.Ecken;
                for (var k = 0; k < quelle.Length; k++)
                {
                    var v = quelle[k];
                    ecken[e + k] = new Vector3(r0.X * v.X + r0.Y * v.Y + r0.Z * v.Z + o.X, r1.X * v.X + r1.Y * v.Y + r1.Z * v.Z + o.Y, r2.X * v.X + r2.Y * v.Y + r2.Z * v.Z + o.Z);
                }
                if (normalen is not null)
                    for (var k = 0; k < quelle.Length; k++)
                    {
                        if (q.Normalen is null) { normalen[e + k] = Vector3.Up; continue; } // fehlt dem Teil (nur beim Zusammenlegen verschiedener Aufbauten)
                        var n = q.Normalen[k];
                        normalen[e + k] = new Vector3(d0.X * n.X + d0.Y * n.Y + d0.Z * n.Z, d1.X * n.X + d1.Y * n.Y + d1.Z * n.Z, d2.X * n.X + d2.Y * n.Y + d2.Z * n.Z);
                    }
                if (tangenten is not null)
                    for (var k = 0; k < quelle.Length; k++)
                    {
                        var t = (e + k) * 4;
                        if (q.Tangenten is null) { (tangenten[t], tangenten[t + 1], tangenten[t + 2], tangenten[t + 3]) = (1, 0, 0, 1); continue; }
                        float x = q.Tangenten[k * 4], y = q.Tangenten[k * 4 + 1], z = q.Tangenten[k * 4 + 2];
                        tangenten[t] = d0.X * x + d0.Y * y + d0.Z * z;
                        tangenten[t + 1] = d1.X * x + d1.Y * y + d1.Z * z;
                        tangenten[t + 2] = d2.X * x + d2.Y * y + d2.Z * z;
                        tangenten[t + 3] = q.Tangenten[k * 4 + 3];
                    }
                if (farben is not null)
                {
                    if (q.Farben is not null) Array.Copy(q.Farben, 0, farben, e, quelle.Length);
                    else Array.Fill(farben, Colors.White, e, quelle.Length); // wie Godot ohne Farbfeld
                }
                if (uv is not null && q.Uv is not null) Array.Copy(q.Uv, 0, uv, e, quelle.Length);
                if (uv2 is not null && q.Uv2 is not null) Array.Copy(q.Uv2, 0, uv2, e, quelle.Length);
                var ind = q.Indizes;
                for (var k = 0; k < ind.Length; k++) indizes[i + k] = ind[k] + e;
                e += quelle.Length;
                i += ind.Length;
            }
            var felder = new Godot.Collections.Array();
            felder.Resize((int)Mesh.ArrayType.Max);
            felder[(int)Mesh.ArrayType.Vertex] = ecken;
            if (normalen is not null) felder[(int)Mesh.ArrayType.Normal] = normalen;
            if (tangenten is not null) felder[(int)Mesh.ArrayType.Tangent] = tangenten;
            if (farben is not null) felder[(int)Mesh.ArrayType.Color] = farben;
            if (uv is not null) felder[(int)Mesh.ArrayType.TexUV] = uv;
            if (uv2 is not null) felder[(int)Mesh.ArrayType.TexUV2] = uv2;
            felder[(int)Mesh.ArrayType.Index] = indizes;
            netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, felder);
            netz.SurfaceSetMaterial(netz.GetSurfaceCount() - 1, g.Stoff);
        }
    }

    /// <summary>
    /// Ein Netz nur für den Schattenwurf: alle gesammelten Teile in <b>einer</b> Fläche, nur Eckpunkte und Indizes (die Tiefe im Schattenbild braucht nichts anderes), mit dem Stoff <paramref name="stoff"/>
    /// (undurchsichtig, ohne Strukturen). Kostet im sichtbaren Bild nichts, wenn der Knoten „nur Schatten“ wirft.
    /// </summary>
    public ArrayMesh? BackenSchatten(Sammler sammler, Material stoff)
    {
        if (sammler.Reihenfolge.Count == 0) return null;
        var ecken = new Vector3[sammler.Ecken];
        var indizes = new int[sammler.Reihenfolge.Sum(g => g.Indizes)];
        int e = 0, i = 0;
        foreach (var g in sammler.Reihenfolge)
            foreach (var (q, lage) in g.Teile)
            {
                var (r0, r1, r2, o) = (lage.Basis.Row0, lage.Basis.Row1, lage.Basis.Row2, lage.Origin);
                var quelle = q.Ecken;
                for (var k = 0; k < quelle.Length; k++)
                {
                    var v = quelle[k];
                    ecken[e + k] = new Vector3(r0.X * v.X + r0.Y * v.Y + r0.Z * v.Z + o.X, r1.X * v.X + r1.Y * v.Y + r1.Z * v.Z + o.Y, r2.X * v.X + r2.Y * v.Y + r2.Z * v.Z + o.Z);
                }
                var ind = q.Indizes;
                for (var k = 0; k < ind.Length; k++) indizes[i + k] = ind[k] + e;
                e += quelle.Length;
                i += ind.Length;
            }
        var felder = new Godot.Collections.Array();
        felder.Resize((int)Mesh.ArrayType.Max);
        felder[(int)Mesh.ArrayType.Vertex] = ecken;
        felder[(int)Mesh.ArrayType.Index] = indizes;
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, felder);
        netz.SurfaceSetMaterial(0, stoff);
        return netz;
    }

    private Quelle[] QuellenVon(Mesh netz)
    {
        if (_quellen.TryGetValue(netz, out var vorhanden)) return vorhanden;
        var liste = new List<Quelle>();
        for (var f = 0; f < netz.GetSurfaceCount(); f++)
        {
            if ((netz as ArrayMesh)?.SurfaceGetPrimitiveType(f) is { } art && art != Mesh.PrimitiveType.Triangles) continue; // nur Dreiecke
            var a = netz.SurfaceGetArrays(f);
            var q = new Quelle { Stoff = netz.SurfaceGetMaterial(f) };
            if (a[(int)Mesh.ArrayType.Vertex].VariantType == Variant.Type.PackedVector3Array) q.Ecken = a[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if (a[(int)Mesh.ArrayType.Normal].VariantType == Variant.Type.PackedVector3Array) q.Normalen = a[(int)Mesh.ArrayType.Normal].AsVector3Array();
            if (a[(int)Mesh.ArrayType.Tangent].VariantType == Variant.Type.PackedFloat32Array) q.Tangenten = a[(int)Mesh.ArrayType.Tangent].AsFloat32Array();
            if (a[(int)Mesh.ArrayType.Color].VariantType == Variant.Type.PackedColorArray) q.Farben = a[(int)Mesh.ArrayType.Color].AsColorArray();
            if (a[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.PackedVector2Array) q.Uv = a[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            if (a[(int)Mesh.ArrayType.TexUV2].VariantType == Variant.Type.PackedVector2Array) q.Uv2 = a[(int)Mesh.ArrayType.TexUV2].AsVector2Array();
            if (a[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.PackedInt32Array) q.Indizes = a[(int)Mesh.ArrayType.Index].AsInt32Array();
            else q.Indizes = Enumerable.Range(0, q.Ecken.Length - q.Ecken.Length % 3).ToArray();
            // Felder, die nicht zu den Ecken passen, weglassen (sollte nicht vorkommen)
            if (q.Normalen is { } n && n.Length != q.Ecken.Length) q.Normalen = null;
            if (q.Tangenten is { } t && t.Length != q.Ecken.Length * 4) q.Tangenten = null;
            if (q.Farben is { } c && c.Length != q.Ecken.Length) q.Farben = null;
            if (q.Uv is { } u && u.Length != q.Ecken.Length) q.Uv = null;
            if (q.Uv2 is { } u2 && u2.Length != q.Ecken.Length) q.Uv2 = null;
            if (q.Ecken.Length == 0 || q.Indizes.Length == 0) continue;
            q.StoffNr = StoffNummer(q.Stoff);
            if (Ersetzbar(q.Stoff) && q.Stoff!.ResourceName is { Length: > 0 } name) _stoffeNachName.TryAdd(name, q.Stoff);
            q.Aufbau = (q.Normalen is null ? 0 : 1) | (q.Tangenten is null ? 0 : 2) | (q.Farben is null ? 0 : 4) | (q.Uv is null ? 0 : 8) | (q.Uv2 is null ? 0 : 16);
            liste.Add(q);
        }
        return _quellen[netz] = liste.ToArray();
    }

    /// <summary>
    /// Die Nummer eines Stoffs: gleiche Nummer für Stoffe mit gleichem Namen und gleichen wichtigen Werten (Farbe, Rauheit, Metall, Textur …), auch wenn sie als
    /// verschiedene Objekte aus verschiedenen Baukästen kommen (die Oberflächendetails richten sich nach dem Namen). Andere Stoffe (Shader) behalten ihre eigene Nummer.
    /// </summary>
    private int StoffNummer(Material? stoff)
    {
        if (stoff is null) return 0;
        if (_stoffNummern.TryGetValue(stoff, out var vorhanden)) return vorhanden;
        var kurz = stoff is BaseMaterial3D s
            ? string.Join('|', s.GetClass(), s.ResourceName, s.AlbedoColor, s.Roughness, s.Metallic, s.MetallicSpecular, s.VertexColorUseAsAlbedo, s.CullMode, s.ShadingMode, s.Transparency,
                          s.AlbedoTexture?.GetInstanceId(), s.NormalEnabled, s.NormalTexture?.GetInstanceId(), s.EmissionEnabled, s.Emission, s.Uv1Scale, s.Uv1Triplanar, s.TextureFilter, s.NextPass?.GetInstanceId())
            : "eigen|" + stoff.GetInstanceId();
        var voll = Pruefen ? VollSchluessel(stoff) : "";
        if (_stoffSchluessel.TryGetValue(kurz, out var nr))
        {
            if (Pruefen && _vollSchluessel[nr - 1] != voll) Widersprueche++;
        }
        else
        {
            _stoffSchluessel[kurz] = nr = _stoffSchluessel.Count + 1;
            _vollSchluessel.Add(voll);
        }
        return _stoffNummern[stoff] = nr;
    }

    /// <summary>Alle gespeicherten Eigenschaften des Stoffs als Text (langsam, nur für die Prüfung).</summary>
    private static string VollSchluessel(Material stoff)
    {
        var sb = new System.Text.StringBuilder(stoff.GetClass());
        foreach (var eigenschaft in stoff.GetPropertyList())
        {
            if (((PropertyUsageFlags)(long)eigenschaft["usage"] & PropertyUsageFlags.Storage) == 0) continue;
            var name = eigenschaft["name"].AsString();
            if (name == "resource_path") continue;
            sb.Append('|').Append(name).Append('=').Append(stoff.Get(name).ToString());
        }
        return sb.ToString();
    }
}
