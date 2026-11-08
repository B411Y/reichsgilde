using Godot;

namespace Reichsgilde;

/// <summary>
/// <b>Flächen zusammenlegen</b> für Tiere und Vögel (PR „weniger Zeichenaufrufe“) – ein Teil der <c>partial class Stadtansicht</c>. Jede Fläche (Surface) eines Netzes ist ein eigener Zeichenaufruf, im Pixel-Look
/// in jeder der beiden Ansichten. Eine Taube hat sieben Flächen (Gefieder hell, dunkel, Hals, Fuß, Auge …), die sich nur in der Farbe unterscheiden: Das waren allein für die rund 25 sichtbaren
/// Vögel gut 300 Zeichenaufrufe, für Hunde, Katzen und Hühner noch einmal so viel.
///
/// <b>Darum</b> werden die Flächen eines Tier- oder Vogelnetzes, deren Material schlicht ist (Farbe, sonst nichts: kein Leuchten, keine Textur, keine Durchsicht, keine Struktur der
/// Oberflächendetails), beim Anlegen zu <b>einer</b> Fläche je Materialeigenschaften verschmolzen; die Farbe jeder alten Fläche steht danach als <b>Eckenfarbe</b> im Netz (wie beim Material „Farben“
/// der Umlandmodelle, <see cref="FarbenEinrichten"/>). Knochen und Gewichte bleiben, die Figur bewegt sich wie vorher. Auf dem Bildschirm ändert sich nichts: gleiche Dreiecke, gleiche Farbe
/// (Weiß mal Eckenfarbe in sRGB gegen die Farbe des Materials), gleiche Rauheit. Netze mit einer Fläche, mit Blendformen oder mit irgendeinem nicht schlichten Material bleiben, wie sie sind.
/// Startoption <c>--zeichensparen=…</c> schaltet es aus (<see cref="ZeichenSparen"/>).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Zusammengelegte Netze je Ausgangsnetz (alle Instanzen derselben Datei teilen sich Netz und Material); null = bleibt.</summary>
    private readonly Dictionary<ulong, ArrayMesh?> _flaechenMerge = new();

    /// <summary>Für den Bericht: Netze, die zusammengelegt wurden, und Flächen vorher und nachher.</summary>
    internal static int FlaechenMergeNetze, FlaechenMergeVorher, FlaechenMergeNachher;

    /// <summary>Legt die Flächen aller Netze unter <paramref name="wurzel"/> zusammen (siehe Klassenkopf); aufrufen, bevor <paramref name="wurzel"/> in den Baum kommt.</summary>
    private void FlaechenZusammenlegen(Node3D wurzel)
    {
        if (!ZeichenSparenMerge || _oberflaeche is null) return;
        foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            if (teil.Mesh is ArrayMesh netz && netz.GetSurfaceCount() >= 2 && teil.MaterialOverride is null && teil.GetBlendShapeCount() == 0
                && Enumerable.Range(0, netz.GetSurfaceCount()).All(i => teil.GetSurfaceOverrideMaterial(i) is null)
                && ZusammengelegtesNetz(netz) is { } neu)
                teil.Mesh = neu;
    }

    private ArrayMesh? ZusammengelegtesNetz(ArrayMesh netz)
    {
        var schluessel = netz.GetInstanceId();
        if (_flaechenMerge.TryGetValue(schluessel, out var bekannt)) return bekannt;
        return _flaechenMerge[schluessel] = ZusammengelegtesNetzBauen(netz);
    }

    /// <summary>Alles, was neben der Farbe das Aussehen einer schlichten Fläche bestimmt: Flächen mit gleichem Schlüssel dürfen eine werden.</summary>
    private static string SchlichtSchluessel(BaseMaterial3D s) =>
        $"{s.Roughness:R}|{s.Metallic:R}|{s.MetallicSpecular:R}|{s.CullMode}|{s.DiffuseMode}|{s.SpecularMode}|{s.ShadingMode}|{s.TextureFilter}|{s.DisableReceiveShadows}|{s.DisableAmbientLight}|{s.NoDepthTest}|{s.BlendMode}";

    private ArrayMesh? ZusammengelegtesNetzBauen(ArrayMesh netz)
    {
        var n = netz.GetSurfaceCount();
        var gruppen = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < n; i++)
        {
            if (netz.SurfaceGetPrimitiveType(i) != Mesh.PrimitiveType.Triangles) return null;
            if (netz.SurfaceGetMaterial(i) is not StandardMaterial3D stoff) return null;
            // schlicht: nur Farbe. Alles, was die Oberflächendetails ändern würden (Struktur, Bibliothek) oder was leuchtet, durchscheint, Textur trägt, bleibt wie es ist
            if (stoff.Transparency != BaseMaterial3D.TransparencyEnum.Disabled || stoff.EmissionEnabled || stoff.AlbedoTexture is not null || stoff.NormalEnabled
                || stoff.RimEnabled || stoff.ClearcoatEnabled || stoff.VertexColorUseAsAlbedo || stoff.RoughnessTexture is not null || stoff.MetallicTexture is not null
                || stoff.HeightmapEnabled || stoff.Get("subsurf_scatter_enabled").AsBool() || stoff.Get("backlight_enabled").AsBool() || stoff.Uv1Scale != Vector3.One || stoff.Uv1Offset != Vector3.Zero
                || stoff.BillboardMode != BaseMaterial3D.BillboardModeEnum.Disabled || stoff.Get("grow").AsBool() || stoff.AlbedoColor.A < 0.999f) return null;
            if (_oberflaeche!.Ableiten(stoff) is not null) return null;
            var felder = netz.SurfaceGetArrays(i);
            if (felder[(int)Mesh.ArrayType.Color].VariantType != Variant.Type.Nil || felder[(int)Mesh.ArrayType.Vertex].VariantType == Variant.Type.Nil) return null;
            var art = SchlichtSchluessel(stoff);
            if (!gruppen.TryGetValue(art, out var liste)) gruppen[art] = liste = new List<int>();
            liste.Add(i);
        }
        if (gruppen.Count >= n) return null; // nichts zu gewinnen

        var neu = new ArrayMesh { ResourceName = netz.ResourceName, CustomAabb = netz.CustomAabb };
        foreach (var (_, flaechen) in gruppen)
        {
            var ecken = new List<Vector3>();
            var normalen = new List<Vector3>();
            var tangenten = new List<float>();
            var uv = new List<Vector2>();
            var farben = new List<Color>();
            var knochen = new List<int>();
            var gewichte = new List<float>();
            var indizes = new List<int>();
            var mitNormalen = true; var mitTangenten = true; var mitUv = true; var mitKnochen = true; var ohneKnochen = true;
            foreach (var i in flaechen)
            {
                var f = netz.SurfaceGetArrays(i);
                mitNormalen &= f[(int)Mesh.ArrayType.Normal].VariantType != Variant.Type.Nil;
                mitTangenten &= f[(int)Mesh.ArrayType.Tangent].VariantType != Variant.Type.Nil;
                mitUv &= f[(int)Mesh.ArrayType.TexUV].VariantType != Variant.Type.Nil;
                var hat = f[(int)Mesh.ArrayType.Bones].VariantType != Variant.Type.Nil && f[(int)Mesh.ArrayType.Weights].VariantType != Variant.Type.Nil;
                mitKnochen &= hat;
                ohneKnochen &= !hat;
            }
            if (!mitKnochen && !ohneKnochen) return null; // nur ein Teil der Flächen hat Knochen: nicht zusammenlegen, sonst gingen sie verloren
            // Knochen je Ecke müssen in allen Flächen gleich viele sein (4 oder 8)
            var knochenJeEcke = 0;
            foreach (var i in flaechen)
            {
                var f = netz.SurfaceGetArrays(i);
                var punkte = f[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                if (punkte.Length == 0) continue;
                if (mitKnochen)
                {
                    var je = f[(int)Mesh.ArrayType.Bones].AsInt32Array().Length / punkte.Length;
                    if (knochenJeEcke == 0) knochenJeEcke = je; else if (knochenJeEcke != je) return null;
                }
                var stoff = (StandardMaterial3D)netz.SurfaceGetMaterial(i);
                var versatz = ecken.Count;
                ecken.AddRange(punkte);
                if (mitNormalen) normalen.AddRange(f[(int)Mesh.ArrayType.Normal].AsVector3Array());
                if (mitTangenten) tangenten.AddRange(f[(int)Mesh.ArrayType.Tangent].AsFloat32Array());
                if (mitUv) uv.AddRange(f[(int)Mesh.ArrayType.TexUV].AsVector2Array());
                if (mitKnochen) { knochen.AddRange(f[(int)Mesh.ArrayType.Bones].AsInt32Array()); gewichte.AddRange(f[(int)Mesh.ArrayType.Weights].AsFloat32Array()); }
                for (var k = 0; k < punkte.Length; k++) farben.Add(stoff.AlbedoColor);
                if (f[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil)
                    for (var k = 0; k < punkte.Length; k++) indizes.Add(versatz + k);
                else
                    foreach (var k in f[(int)Mesh.ArrayType.Index].AsInt32Array()) indizes.Add(versatz + k);
            }
            if (indizes.Count == 0) return null;
            var felder = new Godot.Collections.Array();
            felder.Resize((int)Mesh.ArrayType.Max);
            felder[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
            if (mitNormalen) felder[(int)Mesh.ArrayType.Normal] = normalen.ToArray();
            if (mitTangenten) felder[(int)Mesh.ArrayType.Tangent] = tangenten.ToArray();
            if (mitUv) felder[(int)Mesh.ArrayType.TexUV] = uv.ToArray();
            felder[(int)Mesh.ArrayType.Color] = farben.ToArray();
            if (mitKnochen) { felder[(int)Mesh.ArrayType.Bones] = knochen.ToArray(); felder[(int)Mesh.ArrayType.Weights] = gewichte.ToArray(); }
            felder[(int)Mesh.ArrayType.Index] = indizes.ToArray();
            neu.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, felder);
            // Material: eine Kopie der ersten Fläche der Gruppe, Farbe jetzt aus der Eckenfarbe (sRGB, wie „Farben“)
            var kopie = (StandardMaterial3D)((StandardMaterial3D)netz.SurfaceGetMaterial(flaechen[0])).Duplicate();
            kopie.AlbedoColor = Colors.White;
            kopie.VertexColorUseAsAlbedo = true;
            kopie.VertexColorIsSrgb = true;
            kopie.ResourceName = "Zusammengelegt";
            neu.SurfaceSetMaterial(neu.GetSurfaceCount() - 1, kopie);
        }
        FlaechenMergeNetze++;
        FlaechenMergeVorher += n;
        FlaechenMergeNachher += neu.GetSurfaceCount();
        return neu;
    }
}
