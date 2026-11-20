using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Modulares Wohnhaussystem (Variante A, Nutzer 20.11.2026): Häuserzeilen aus Modulen – ein Teil der <c>partial class Stadtansicht</c>.
/// Die Module eines Satzes (Volk × Stufe) liegen in <c>gebaeude/zeilen/zeile_&lt;satz&gt;.glb</c> (<c>tools/modelle/neubau/zeile_module.py</c>),
/// welches Modul wo steht, plant <see cref="Zeilenplan"/> (Nachbarregel, Eckhaus, Keilstück). Jede Zeile wird zu EINEM Netz
/// verschmolzen (Flächen: Trim, Strohkanten, Fenster): Ebene, AO-Ebene, Putz- und Akzentfarbe trägt jede Ecke (CUSTOM0/1,
/// <c>building_trim_zeile.gdshader</c>), so stehen Häuser verschiedener Völker und Stufen in einer Zeile mit einem Material.
/// Die Türen liegen als Knoten „Tuer_&lt;Parzelle&gt;“ an der Zeile, die Sperrfläche ist der Grundriss des Erdgeschosses je Haus.
/// Startoption <c>--zeilenprobe=1[@x,z]</c>: eine feste Probezeile mit Ecke (ohne Kern, ohne Kartenänderung).
/// </summary>
public partial class Stadtansicht
{
    private const string ZeilenOrdner = "gebaeude/zeilen/zeile_";
    private static readonly string? ZeilenProbe = NeubauOption("--zeilenprobe");

    /// <summary>Netzdaten eines Moduls: je Fläche die Arrays und die Art (trim, kante, fenster).</summary>
    private sealed record ZeilenModul(List<(Godot.Collections.Array Felder, string Art)> Flaechen, Vector3? Tuer);

    private readonly Dictionary<string, Dictionary<string, ZeilenModul>> _zeilenSaetze = new();
    private readonly List<ShaderMaterial> _zeilenStoffe = new();
    private ShaderMaterial? _zeilenTrim, _zeilenKante;

    /// <summary>Die Module eines Satzes aus der GLB (einmal gelesen).</summary>
    private Dictionary<string, ZeilenModul>? ZeilenModule(string satz)
    {
        if (_zeilenSaetze.TryGetValue(satz, out var fertig)) return fertig;
        var szene = Modell(ZeilenOrdner + satz);
        if (szene is null) return null;
        var wurzel = szene.Instantiate<Node3D>();
        var module = new Dictionary<string, ZeilenModul>();
        foreach (var kind in wurzel.GetChildren())
        {
            if (kind is not MeshInstance3D { Mesh: ArrayMesh netz } teil) continue;
            var flaechen = new List<(Godot.Collections.Array, string)>();
            for (var i = 0; i < netz.GetSurfaceCount(); i++)
            {
                var name = netz.SurfaceGetMaterial(i)?.ResourceName ?? "";
                var art = name switch { "Fenster" => "fenster", "Strohkante" or "Schieferkante" => "kante", _ => "trim" };
                flaechen.Add((netz.SurfaceGetArrays(i), art));
            }
            var tuerKnoten = wurzel.GetNodeOrNull<Node3D>("Tuer_" + teil.Name);
            module[teil.Name] = new ZeilenModul(flaechen, tuerKnoten?.Position);
        }
        wurzel.QueueFree();
        return _zeilenSaetze[satz] = module;
    }

    /// <summary>Das Material der Zeilen (eins für alle Zeilen, bzw. die Kantenvariante): Arrays der Häuser, AO-Array der Zeilen.</summary>
    private ShaderMaterial ZeilenStoff(bool kante)
    {
        var vorhanden = kante ? _zeilenKante : _zeilenTrim;
        if (vorhanden is not null) return vorhanden;
        var stoff = new ShaderMaterial
        {
            Shader = GD.Load<Shader>(kante ? "res://assets/shader/building_trim_zeile_kante.gdshader" : "res://assets/shader/building_trim_zeile.gdshader"),
            ResourceName = kante ? "Zeile Kante" : "Zeile",
        };
        stoff.SetShaderParameter("albedo_tex", NeubauTextur<TextureLayered>("haeuser_ebenen_albedo.png"));
        stoff.SetShaderParameter("normal_tex", NeubauTextur<TextureLayered>("haeuser_ebenen_normal.png"));
        stoff.SetShaderParameter("orm_tex", NeubauTextur<TextureLayered>("haeuser_ebenen_orm.png"));
        stoff.SetShaderParameter("ao_uv2_tex", NeubauTextur<TextureLayered>("zeilen_ao_ebenen.png"));
        stoff.SetShaderParameter("pixel_look", Fenster.PixelLookAn ? 1f : 0f);
        if (kante) stoff.SetShaderParameter("kante_schwelle", NeubauBrand.KantenSchwelle(Fenster.PixelLookAn));
        _zeilenStoffe.Add(stoff);
        if (kante) _zeilenKante = stoff; else _zeilenTrim = stoff;
        return stoff;
    }

    /// <summary>Pixel-Look an oder aus (aus <see cref="NeubauPixelSetzen"/>): wie die Trim-Materialien der Neubauten.</summary>
    private void ZeilenPixelSetzen(bool an)
    {
        foreach (var stoff in _zeilenStoffe)
        {
            stoff.SetShaderParameter("pixel_look", an ? 1f : 0f);
            if (stoff == _zeilenKante) stoff.SetShaderParameter("kante_schwelle", NeubauBrand.KantenSchwelle(an));
        }
    }

    /// <summary>Farben und Ebenen eines Hauses der Zeile (fest aus Zeile und Parzelle, wie bei den Neubauten aus dem Standort).</summary>
    private static (Color Putz, Color Akzent) ZeilenFarben(string zeile, int parzelle, string satz)
    {
        var saat = (uint)(Zahl(zeile) * 31 + (uint)parzelle * 7919);
        var hell = satz.StartsWith("zwerg") || satz.StartsWith("elb") || satz.StartsWith("halbling");
        return ((hell ? NeubauStein : NeubauPutz)[saat % (uint)NeubauPutz.Length], NeubauAkzent[(saat / 7) % (uint)NeubauAkzent.Length]);
        static uint Zahl(string s) { uint h = 2166136261; foreach (var c in s) h = (h ^ c) * 16777619; return h; }
    }

    private static (Volk Volk, int Stufe) ZeilenVolk(string satz)
    {
        var s = Zeilenplan.SatzVon(satz);
        var volk = s?.Volk switch { "zwerg" => Volk.Zwerg, "elb" => Volk.Elb, "halbling" => Volk.Halbling, _ => Volk.Mensch };
        return (volk, s?.Stufe ?? 1);
    }

    /// <summary>
    /// Baut eine Zeile: plant die Module (<see cref="Zeilenplan.Planen"/>), verschmilzt sie zu einem Netz, hängt Türknoten an und merkt
    /// die Sperrflächen (Grundriss des Erdgeschosses je Haus). Gibt den Knoten der Zeile zurück (null, wenn ein Satz fehlt).
    /// </summary>
    private Node3D? ZeileBauen(string kennung, IReadOnlyList<(float X, float Z)> linie, bool links, IReadOnlyList<Zeilenplan.Haus> haeuser)
    {
        var lagen = Zeilenplan.Planen(linie, links, haeuser);
        var knoten = new Node3D { Name = "Zeile_" + kennung };
        AddChild(knoten);
        var sammler = new Dictionary<string, ZeilenSammler> { ["trim"] = new(), ["kante"] = new(), ["fenster"] = new() };
        var tueren = 0;
        foreach (var l in lagen)
        {
            if (ZeilenModule(l.Satz) is not { } module || !module.TryGetValue(l.Modul, out var modul)) continue;
            var (volk, stufe) = ZeilenVolk(l.Satz);
            var (putz, akzent) = ZeilenFarben(kennung, l.Parzelle, l.Satz);
            var ebene = Steinebenen.Ebene(volk, stufe);
            var aoEbene = Zeilenplan.AoEbene(l.Satz);
            // Bodenhöhe: Häuser je Parzelle in der Mitte des Grundrisses, Seitenstücke mit ihrem Haus
            var mitteX = l.Ox + l.Ex * l.Breite / 2f + l.Tx * Zeilenplan.Tiefe / 2f;
            var mitteZ = l.Oz + l.Ez * l.Breite / 2f + l.Tz * Zeilenplan.Tiefe / 2f;
            var y = BodenHoehe(mitteX, mitteZ);
            var lage = ZeilenLage(l, y);
            foreach (var (felder, art) in modul.Flaechen)
                sammler[art].Hinzu(felder, lage, new Color(putz.R, putz.G, putz.B, ebene), new Color(akzent.R, akzent.G, akzent.B, aoEbene));
            if (modul.Tuer is { } tuer && !l.Gespiegelt)
            {
                knoten.AddChild(new Node3D { Name = $"Tuer_{l.Parzelle}", Transform = lage * new Transform3D(Basis.Identity, tuer) });
                tueren++;
            }
            if (Zeilenplan.Breite(l.Modul) > 0 && !l.Gespiegelt)
            {
                // Sperrfläche: Erdgeschoss (ohne Auskragung, ohne Dach); Giebelhäuser ohne die halbe Traufgasse
                var ein = Zeilenplan.StellungVon(l.Modul) == Zeilenplan.Stellung.Giebel ? Zeilenplan.Ein : 0f;
                var halb = new Vector3(l.Breite / 2f - ein, 2f, Zeilenplan.Tiefe / 2f);
                EgoMauerTeil(new Aabb(-halb, halb * 2f), lage * new Transform3D(Basis.Identity, new Vector3(l.Breite / 2f, 2f, -Zeilenplan.Tiefe / 2f)), $"Zeile {kennung} Haus {l.Parzelle}");
            }
        }
        var netz = new ArrayMesh { ResourceName = "Zeile_" + kennung };
        foreach (var (art, s) in sammler)
        {
            if (s.Leer) continue;
            var flaeche = netz.GetSurfaceCount();
            s.Anhaengen(netz, art == "fenster");
            netz.SurfaceSetMaterial(flaeche, art switch { "kante" => ZeilenStoff(true), "fenster" => FensterStoff(Fenstergruppe.Wohnen), _ => ZeilenStoff(false) });
        }
        knoten.AddChild(new MeshInstance3D { Name = "Netz", Mesh = netz });
        GD.Print($"[zeilen] {kennung}: {lagen.Count} Module, {lagen.Count(l => Zeilenplan.Breite(l.Modul) > 0 && !l.Gespiegelt && l.Parzelle >= 0)} Häuser, "
                 + $"{netz.GetSurfaceCount()} Flächen, {sammler.Values.Sum(s => s.Dreiecke)} Dreiecke, {tueren} Türen");
        return knoten;
    }

    /// <summary>Lage eines Moduls: Modul-x entlang der Gasse, Modul-z zur Gasse (die GLB hat die Tiefe bei −z), gespiegelt x → Breite − x.</summary>
    private static Transform3D ZeilenLage(Zeilenplan.Lage l, float y)
    {
        var ex = new Vector3(l.Ex, 0, l.Ez);
        var ez = -new Vector3(l.Tx, 0, l.Tz);
        var ursprung = new Vector3(l.Ox, y, l.Oz);
        if (l.Gespiegelt) { ursprung += ex * l.Breite; ex = -ex; }
        return new Transform3D(new Basis(ex, Vector3.Up, ez), ursprung);
    }

    /// <summary>Sammelt die Flächen einer Art aus vielen Modulen in ein Netz (Ecken in die Lage gerechnet, gespiegelte mit umgedrehtem Umlauf).</summary>
    private sealed class ZeilenSammler
    {
        readonly List<Vector3> _ecken = new(), _normalen = new();
        readonly List<float> _tangenten = new(), _c0 = new(), _c1 = new();
        readonly List<Vector2> _uv = new(), _uv2 = new();
        readonly List<Color> _farben = new();
        readonly List<int> _index = new();
        public bool Leer => _index.Count == 0;
        public int Dreiecke => _index.Count / 3;

        public void Hinzu(Godot.Collections.Array felder, Transform3D lage, Color c0, Color c1)
        {
            var ecken = felder[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normalen = felder[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var tangenten = felder[(int)Mesh.ArrayType.Tangent].VariantType == Variant.Type.Nil ? null : felder[(int)Mesh.ArrayType.Tangent].AsFloat32Array();
            var uv = felder[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            var uv2 = felder[(int)Mesh.ArrayType.TexUV2].VariantType == Variant.Type.Nil ? null : felder[(int)Mesh.ArrayType.TexUV2].AsVector2Array();
            var farben = felder[(int)Mesh.ArrayType.Color].VariantType == Variant.Type.Nil ? null : felder[(int)Mesh.ArrayType.Color].AsColorArray();
            var index = felder[(int)Mesh.ArrayType.Index].AsInt32Array();
            var spiegel = lage.Basis.Determinant() < 0;
            var normBasis = lage.Basis.Inverse().Transposed();
            var start = _ecken.Count;
            for (var i = 0; i < ecken.Length; i++)
            {
                _ecken.Add(lage * ecken[i]);
                _normalen.Add((normBasis * normalen[i]).Normalized());
                if (tangenten is not null)
                {
                    var t = (lage.Basis * new Vector3(tangenten[i * 4], tangenten[i * 4 + 1], tangenten[i * 4 + 2])).Normalized();
                    _tangenten.AddRange(new[] { t.X, t.Y, t.Z, spiegel ? -tangenten[i * 4 + 3] : tangenten[i * 4 + 3] });
                }
                else _tangenten.AddRange(new[] { 1f, 0f, 0f, 1f });
                _uv.Add(uv[i]);
                _uv2.Add(uv2 is null ? Vector2.Zero : uv2[i]);
                _farben.Add(farben is null ? new Color(0, 0, 0) : farben[i]);
                _c0.AddRange(new[] { c0.R, c0.G, c0.B, c0.A });
                _c1.AddRange(new[] { c1.R, c1.G, c1.B, c1.A });
            }
            for (var i = 0; i + 2 < index.Length; i += 3)
            {
                if (spiegel) { _index.Add(start + index[i]); _index.Add(start + index[i + 2]); _index.Add(start + index[i + 1]); }
                else { _index.Add(start + index[i]); _index.Add(start + index[i + 1]); _index.Add(start + index[i + 2]); }
            }
        }

        public void Anhaengen(ArrayMesh netz, bool fenster)
        {
            var felder = new Godot.Collections.Array();
            felder.Resize((int)Mesh.ArrayType.Max);
            var ecken = _ecken.ToArray();
            var index = _index.ToArray();
            felder[(int)Mesh.ArrayType.Vertex] = ecken;
            felder[(int)Mesh.ArrayType.Normal] = _normalen.ToArray();
            felder[(int)Mesh.ArrayType.Tangent] = _tangenten.ToArray();
            felder[(int)Mesh.ArrayType.TexUV] = _uv.ToArray();
            if (fenster)
            {
                // jedes Fenster mit eigener Kennung (Fensterlicht, Silhouetten der Bewohner), wie FensterNetz
                var (farben, flaechenUv) = FensterKennungen(ecken, index);
                felder[(int)Mesh.ArrayType.Color] = farben;
                felder[(int)Mesh.ArrayType.TexUV2] = flaechenUv;
            }
            else
            {
                felder[(int)Mesh.ArrayType.Color] = _farben.ToArray();
                felder[(int)Mesh.ArrayType.TexUV2] = _uv2.ToArray();
                felder[(int)Mesh.ArrayType.Custom0] = _c0.ToArray();
                felder[(int)Mesh.ArrayType.Custom1] = _c1.ToArray();
            }
            felder[(int)Mesh.ArrayType.Index] = index;
            var format = fenster ? 0 : ((long)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom0Shift)
                                     | ((long)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom1Shift);
            netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, felder, null, null, (Mesh.ArrayFormat)format);
        }
    }

    /// <summary>
    /// Startoption <c>--zeilenprobe=1[@x,z]</c> (aus <see cref="NeubauProbeStarten"/>): die Probezeile des Konzepts (Kapitel 6) um die Stelle
    /// x,z (Standard −38, 22): Schenkel A von links nach rechts Giebel 6, Giebel 4 (Traufgasse dazwischen), Trauf 6, Trauf 6 und das
    /// Eckhaus am rechten Knick, Schenkel B um die Ecke Giebel 6, Trauf 8, Giebel 4 bis zum freien Ende. Ohne Kern, ohne Kartenänderung.
    /// </summary>
    private void ZeilenProbeStarten()
    {
        if (ZeilenProbe is not { Length: > 0 } probe || probe == "0") return;
        var ort = new Vector2(-38, 22);
        if (probe.Split('@') is { Length: 2 } teile && teile[1].Split(',') is { Length: 2 } xz
            && float.TryParse(xz[0], System.Globalization.CultureInfo.InvariantCulture, out var x)
            && float.TryParse(xz[1], System.Globalization.CultureInfo.InvariantCulture, out var z)) ort = new Vector2(x, z);
        // Vorderlinie: 30 m nach Osten (22 m Häuser und das Eckhaus), dann 26 m nach Norden; die Häuser stehen links (nördlich bzw. westlich)
        var linie = new List<(float, float)> { (ort.X - 22, ort.Y + 8), (ort.X + 8, ort.Y + 8), (ort.X + 8, ort.Y - 18) };
        var s = "mensch_s1";
        var haeuser = new List<Zeilenplan.Haus>
        {
            new(s, "k_g6"), new(s, "k_g4"), new(s, "k_t6"), new(s, "k_t6"), new(s, "eck"), new(s, "k_g6"), new(s, "k_t8"), new(s, "k_g4"),
        };
        ZeileBauen("Probe", linie, true, haeuser);
    }
}
