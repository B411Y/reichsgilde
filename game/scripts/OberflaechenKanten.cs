using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Kantendaten für <c>oberflaeche.gdshader</c>: Wo zwei Flächen eines Modells in einem Winkel aufeinandertreffen (Dach und Giebel, Wandecken, Firste), soll sich die Struktur
/// über die Kante fortsetzen und die Kante einen leichten Schatten bekommen. Dafür braucht jeder Bildpunkt den Abstand zur nächsten echten Kante und die Normale der Fläche
/// dahinter. Beides liefert diese Klasse als Eckendaten (CUSTOM0 bis CUSTOM2, je vier Zahlen) in einer Kopie des Netzes:
/// <list type="bullet">
/// <item>Das Netz wird entindiziert (jedes Dreieck eigene Ecken), je Ecke die Höhe über der jeweils gegenüberliegenden Dreieckskante – im Bildpunkt entsteht daraus durch
/// die Interpolation der Abstand zu den drei Kanten (wie ein Drahtgitter-Shader).</item>
/// <item>Eine Kante ist „echt“, wenn ein anderes Dreieck dieselben zwei Eckpunkte teilt und seine Normale mehr als etwa 26 Grad abweicht (Skalarprodukt unter 0,9; runde
/// Türme mit 16 oder mehr Seiten zählen nicht). Gleiche Fläche, Diagonalen von Vierecken und offene Ränder sind keine echten Kanten (Abstand 10000, keine Normale).</item>
/// <item>CUSTOM0 = (Abstand zu Kante 0, 1, 2, Normale 2 z), CUSTOM1 = (Normale 0 xyz, Normale 1 x), CUSTOM2 = (Normale 1 yz, Normale 2 xy). Kante 0 liegt der Ecke 0 gegenüber.</item>
/// </list>
/// Nur für unbewegte Netze (keine Knochen, keine Formen); Abstände in Netzkoordinaten (der Shader multipliziert mit dem Maßstab des Knotens).
/// </summary>
internal static class OberflaechenKanten
{
    private const float KeineKante = 1e4f;
    private const float Gleichheit = 1e-4f;      // Eckpunkte gelten bis auf diesen Abstand als dieselben
    private const float Abweichung = 0.9f;       // Skalarprodukt der Normalen unter dem eine Kante „echt“ ist

    /// <summary>
    /// Die Kopie des Netzes mit Kantendaten, oder null, wenn das Netz dafür nicht taugt (Knochen, Formen, schon eigene Zusatzdaten, keine Dreiecke) oder keine echte Kante hat.
    /// </summary>
    public static ArrayMesh? Berechnen(Mesh original, out int kanten)
    {
        kanten = 0;
        if (original is not ArrayMesh am || am.GetBlendShapeCount() > 0) return null;
        var neu = new ArrayMesh();
        var irgendeine = false;
        for (var f = 0; f < original.GetSurfaceCount(); f++)
        {
            var arrays = original.SurfaceGetArrays(f);
            if (am.SurfaceGetPrimitiveType(f) != Mesh.PrimitiveType.Triangles
                || arrays[(int)Mesh.ArrayType.Bones].VariantType != Variant.Type.Nil
                || arrays[(int)Mesh.ArrayType.Custom0].VariantType != Variant.Type.Nil
                || arrays[(int)Mesh.ArrayType.Custom1].VariantType != Variant.Type.Nil
                || arrays[(int)Mesh.ArrayType.Custom2].VariantType != Variant.Type.Nil)
                return null;
            var flaeche = Flaeche(arrays, out var anzahl);
            if (flaeche is null) return null;
            kanten += anzahl;
            irgendeine |= anzahl > 0;
            var format = (Mesh.ArrayFormat)(((ulong)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom0Shift)
                | ((ulong)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom1Shift)
                | ((ulong)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom2Shift));
            neu.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, flaeche, null, null, format);
            neu.SurfaceSetMaterial(f, original.SurfaceGetMaterial(f));
        }
        return irgendeine ? neu : null;
    }

    private static Godot.Collections.Array? Flaeche(Godot.Collections.Array arrays, out int echteKanten)
    {
        echteKanten = 0;
        var pos = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        if (pos.Length < 3) return null;
        var nor = arrays[(int)Mesh.ArrayType.Normal].VariantType == Variant.Type.Nil ? null : arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var farbe = arrays[(int)Mesh.ArrayType.Color].VariantType == Variant.Type.Nil ? null : arrays[(int)Mesh.ArrayType.Color].AsColorArray();
        var uv = arrays[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.Nil ? null : arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        var uv2 = arrays[(int)Mesh.ArrayType.TexUV2].VariantType == Variant.Type.Nil ? null : arrays[(int)Mesh.ArrayType.TexUV2].AsVector2Array();
        var tang = arrays[(int)Mesh.ArrayType.Tangent].VariantType == Variant.Type.Nil ? null : arrays[(int)Mesh.ArrayType.Tangent].AsFloat32Array();
        var index = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? null : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        var dreiecke = (index?.Length ?? pos.Length) / 3;
        if (dreiecke == 0) return null;
        int Ecke(int d, int k) => index is null ? d * 3 + k : index[d * 3 + k];

        // Flächennormale je Dreieck (zur Vertex-Normale hin ausgerichtet) und Schlüssel der Eckpunkte
        var fn = new Vector3[dreiecke];
        var schluessel = new (long, long, long)[dreiecke * 3];
        for (var d = 0; d < dreiecke; d++)
        {
            Vector3 a = pos[Ecke(d, 0)], b = pos[Ecke(d, 1)], c = pos[Ecke(d, 2)];
            var g = (b - a).Cross(c - a);
            g = g.LengthSquared() > 1e-18f ? g.Normalized() : Vector3.Up;
            if (nor is not null)
            {
                var vn = nor[Ecke(d, 0)] + nor[Ecke(d, 1)] + nor[Ecke(d, 2)];
                if (g.Dot(vn) < 0f) g = -g;
            }
            fn[d] = g;
            for (var k = 0; k < 3; k++)
            {
                var p = pos[Ecke(d, k)];
                schluessel[d * 3 + k] = ((long)MathF.Round(p.X / Gleichheit), (long)MathF.Round(p.Y / Gleichheit), (long)MathF.Round(p.Z / Gleichheit));
            }
        }
        // Kanten -> Dreiecke (Kante k eines Dreiecks liegt der Ecke k gegenüber: Ecken k+1 und k+2)
        var kantenListe = new Dictionary<((long, long, long), (long, long, long)), List<int>>();
        for (var d = 0; d < dreiecke; d++)
            for (var k = 0; k < 3; k++)
            {
                var s1 = schluessel[d * 3 + (k + 1) % 3];
                var s2 = schluessel[d * 3 + (k + 2) % 3];
                if (s1 == s2) continue;
                var schl = s1.CompareTo(s2) <= 0 ? (s1, s2) : (s2, s1);
                if (!kantenListe.TryGetValue(schl, out var l)) kantenListe[schl] = l = new List<int>(2);
                l.Add(d);
            }

        var ecken = dreiecke * 3;
        var vertex = new Vector3[ecken];
        var normal = nor is null ? null : new Vector3[ecken];
        var farbeNeu = farbe is null ? null : new Color[ecken];
        var uvNeu = uv is null ? null : new Vector2[ecken];
        var uv2Neu = uv2 is null ? null : new Vector2[ecken];
        var tangNeu = tang is null ? null : new float[ecken * 4];
        var c0 = new float[ecken * 4];
        var c1 = new float[ecken * 4];
        var c2 = new float[ecken * 4];
        var nb = new Vector3[3];
        var echt = new bool[3];
        for (var d = 0; d < dreiecke; d++)
        {
            for (var k = 0; k < 3; k++)
            {
                echt[k] = false;
                nb[k] = Vector3.Zero;
                var s1 = schluessel[d * 3 + (k + 1) % 3];
                var s2 = schluessel[d * 3 + (k + 2) % 3];
                if (s1 == s2) continue;
                var schl = s1.CompareTo(s2) <= 0 ? (s1, s2) : (s2, s1);
                var nachbarn = kantenListe[schl];
                var best = 2f;
                var bestN = Vector3.Zero;
                var gleiche = false;
                foreach (var m in nachbarn)
                {
                    if (m == d) continue;
                    var dot = fn[d].Dot(fn[m]);
                    if (dot >= Abweichung) { gleiche = true; break; }   // dieselbe Fläche (oder fast): keine echte Kante
                    if (dot < best) { best = dot; bestN = fn[m]; }
                }
                if (!gleiche && best < 2f) { echt[k] = true; nb[k] = bestN; echteKanten++; }
            }
            for (var k = 0; k < 3; k++)
            {
                var i = d * 3 + k;
                var q = pos[Ecke(d, k)];
                vertex[i] = q;
                if (normal is not null) normal[i] = nor![Ecke(d, k)];
                if (farbeNeu is not null) farbeNeu[i] = farbe![Ecke(d, k)];
                if (uvNeu is not null) uvNeu[i] = uv![Ecke(d, k)];
                if (uv2Neu is not null) uv2Neu[i] = uv2![Ecke(d, k)];
                if (tangNeu is not null) for (var z = 0; z < 4; z++) tangNeu[i * 4 + z] = tang![Ecke(d, k) * 4 + z];
                // Abstand zur Kante j: an der Ecke j die Höhe über der Kante, an den anderen Ecken 0 (dazwischen interpoliert der Rasterer); keine echte Kante: überall 1e4
                var a = pos[Ecke(d, (k + 1) % 3)];
                var b = pos[Ecke(d, (k + 2) % 3)];
                var laenge = (b - a).Length();
                var hoehe = laenge > 1e-9f ? (q - a).Cross(b - a).Length() / laenge : 0f;
                var dist = new float[3];
                for (var j = 0; j < 3; j++) dist[j] = !echt[j] ? KeineKante : (j == k ? hoehe : 0f);
                c0[i * 4] = dist[0]; c0[i * 4 + 1] = dist[1]; c0[i * 4 + 2] = dist[2]; c0[i * 4 + 3] = nb[2].Z;
                c1[i * 4] = nb[0].X; c1[i * 4 + 1] = nb[0].Y; c1[i * 4 + 2] = nb[0].Z; c1[i * 4 + 3] = nb[1].X;
                c2[i * 4] = nb[1].Y; c2[i * 4 + 1] = nb[1].Z; c2[i * 4 + 2] = nb[2].X; c2[i * 4 + 3] = nb[2].Y;
            }
        }
        var neu = new Godot.Collections.Array();
        neu.Resize((int)Mesh.ArrayType.Max);
        neu[(int)Mesh.ArrayType.Vertex] = vertex;
        if (normal is not null) neu[(int)Mesh.ArrayType.Normal] = normal;
        if (tangNeu is not null) neu[(int)Mesh.ArrayType.Tangent] = tangNeu;
        if (farbeNeu is not null) neu[(int)Mesh.ArrayType.Color] = farbeNeu;
        if (uvNeu is not null) neu[(int)Mesh.ArrayType.TexUV] = uvNeu;
        if (uv2Neu is not null) neu[(int)Mesh.ArrayType.TexUV2] = uv2Neu;
        neu[(int)Mesh.ArrayType.Custom0] = c0;
        neu[(int)Mesh.ArrayType.Custom1] = c1;
        neu[(int)Mesh.ArrayType.Custom2] = c2;
        return neu;
    }
}
