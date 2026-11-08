using Godot;

namespace Reichsgilde;

/// <summary>
/// <b>Schattenproxys</b> (PR „weniger Zeichenaufrufe“) – ein Teil der <c>partial class Stadtansicht</c>. Die Sonne zeichnet jedes Netz, das Schatten wirft, noch einmal in ihre
/// Schattenkarte, und zwar <b>je Fläche</b> (Surface) ein Zeichenaufruf – im Pixel-Look in beiden Ansichten (Welt und Beleuchtungsbild), weil jede ihre Schattenkarte selbst
/// rechnet. Ein Wohnhaus mit 13 Flächen (Mauer, Fachwerk, Dach, Fenster …) kostete so 26 Zeichenaufrufe nur für den Schatten; zusammen mehr als die Hälfte aller Aufrufe.
/// Für den Schatten zählt aber nur die Form, nicht das Material.
///
/// <b>Darum</b> bekommt jedes Netz eines Modells (<see cref="ModellEinsetzen"/>) mit zwei oder mehr Flächen einen <b>Schattenwerfer</b>: eine Kopie der Form mit allen Flächen zu
/// <b>einer</b> verschmolzen (<c>MultiMeshInstance3D</c> mit einer Instanz, nur Schatten, Name „Schattenproxy“), das Original wirft selbst keinen Schatten mehr.
/// Der Schatten ist derselbe (gleiche Dreiecke, alle Modelle sind doppelseitig und undurchsichtig), das Bild ändert sich nicht. Das Netz bleibt für die Ansicht, was es war:
/// Auswahl, Fenster, Material, Sichtbarkeit (der Werfer hängt als Kind am Netz und folgt ihm).
///
/// <b>Als <c>MultiMeshInstance3D</c> statt <c>MeshInstance3D</c>,</b> damit die vielen Suchläufe über alle <c>MeshInstance3D</c> eines Modells (Schornsteine, Fenster, Traufen,
/// Oberflächendetails, Pixel-Look-Zwillinge …) den Werfer nicht sehen.
///
/// <b>Nicht verschmolzen</b> werden: Netze mit Knochen oder Formschlüsseln (Figuren, Karren-Räder am Skelett), mit einem durchsichtigen oder ausgestanzten Material, mit einer anderen
/// Ebene als 1, die Fahne (Windshader) und die Wasserverdränger. Startoption <c>--zeichensparen=…</c> schaltet es aus (siehe <see cref="ZeichenSparen"/>).
/// </summary>
public partial class Stadtansicht
{
    private const string FensterShaderPfad = "res://assets/shader/fenster.gdshader";
    /// <summary>
    /// Startoption <c>--zeichensparen=…</c> für Vorher-Nachher-Bilder und Messungen mit demselben Bau: ohne die Option sind alle Maßnahmen dieses PR an, <c>0</c> schaltet alle aus,
    /// sonst gelten nur die genannten (Komma getrennt): <c>proxy</c> (Schattenwerfer der Modelle), <c>merge</c> (Flächen der Tiere und Vögel).
    /// </summary>
    private static bool ZeichenSparen(string massnahme)
    {
        var wert = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--zeichensparen="))?["--zeichensparen=".Length..];
        return wert is null || (wert != "0" && wert.Split(',').Contains(massnahme));
    }

    internal static readonly bool ZeichenSparenProxy = ZeichenSparen("proxy"), ZeichenSparenMerge = ZeichenSparen("merge");

    /// <summary>Verschmolzene Formen je Ausgangsnetz (alle Instanzen desselben Modells teilen sich die Netze; das Fensternetz ist je Datei eines).</summary>
    private readonly Dictionary<ulong, ArrayMesh?> _schattenNetze = new();

    /// <summary>Für den Bericht und die Zeichenanalyse: wie viele Netze einen Werfer bekamen, wie viele Flächen dadurch wegfielen, wie viele Netze übrig blieben.</summary>
    internal static readonly Dictionary<string, int> SchattenProxyGruende = new();
    internal static int SchattenProxyNetze, SchattenProxyFlaechenVorher, SchattenProxyUebrig;

    /// <summary>Verschmilzt die Flächen aller schattenwerfenden Netze unter <paramref name="modell"/> (siehe Klassenkopf).</summary>
    private void SchattenProxysAnlegen(Node3D modell)
    {
        if (!ZeichenSparenProxy) return;
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().ToList())
        {
            if (teil.Mesh is not { } mesh || mesh.GetSurfaceCount() < 2) continue;
            var name = teil.Name.ToString();
            if (name is "Fahne" || name.StartsWith("Verdraenger")) continue;
            if (teil.CastShadow != GeometryInstance3D.ShadowCastingSetting.On || teil.Layers != 1 || teil.Skin is not null || teil.GetBlendShapeCount() > 0) continue;
            if (teil.GetNodeOrNull("Schattenproxy") is not null) continue;
            if (SchattenFormFuer(teil, mesh) is not { } form) { SchattenProxyUebrig++; continue; }
            var viele = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = form, InstanceCount = 1 };
            viele.SetInstanceTransform(0, Transform3D.Identity);
            teil.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            teil.AddChild(new MultiMeshInstance3D
            {
                Name = "Schattenproxy", Multimesh = viele, CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly, GIMode = GeometryInstance3D.GIModeEnum.Disabled,
            });
            SchattenProxyNetze++;
            SchattenProxyFlaechenVorher += mesh.GetSurfaceCount();
        }
    }

    /// <summary>
    /// Die verschmolzene Form des Netzes oder null, wenn es nicht geht (durchsichtiges Material, Linien statt Dreiecken …). Flächen mit gleicher Schnittart zählen zusammen; bei
    /// lauter doppelseitigen Flächen (alle Modelle) bleibt eine einzige.
    /// </summary>
    private ArrayMesh? SchattenFormFuer(MeshInstance3D teil, Mesh mesh)
    {
        // Wichtig: die Schnittart (Vorder-, Rückseite, beide) bestimmt, welche Seite im Schatten zählt; Flächen mit anderer Art bleiben eine eigene Fläche des Werfers
        var arten = new int[mesh.GetSurfaceCount()];
        for (var i = 0; i < arten.Length; i++)
        {
            if (mesh is not ArrayMesh netz || netz.SurfaceGetPrimitiveType(i) != Mesh.PrimitiveType.Triangles) return null;
            var stoff = teil.MaterialOverride ?? teil.GetActiveMaterial(i);
            switch (stoff)
            {
                case BaseMaterial3D { Transparency: BaseMaterial3D.TransparencyEnum.Disabled } standard: arten[i] = (int)standard.CullMode; break;
                case ShaderMaterial { Shader.ResourcePath: FensterShaderPfad }: arten[i] = (int)BaseMaterial3D.CullModeEnum.Disabled; break; // fenster.gdshader: undurchsichtig, doppelseitig
                default:
                    SchattenProxyGruende[$"{stoff?.GetType().Name ?? "null"} {(stoff as BaseMaterial3D)?.Transparency} {stoff?.ResourceName}"] = SchattenProxyGruende.GetValueOrDefault($"{stoff?.GetType().Name ?? "null"} {(stoff as BaseMaterial3D)?.Transparency} {stoff?.ResourceName}") + 1;
                    return null; // durchsichtig, ausgestanzt, eigener Shader oder gar keins: nicht anfassen
            }
        }
        var schluessel = mesh.GetInstanceId();
        if (_schattenNetze.TryGetValue(schluessel, out var bekannt)) return bekannt;

        var form = new ArrayMesh();
        foreach (var art in arten.Distinct().OrderBy(a => a))
        {
            var ecken = new List<Vector3>();
            var indizes = new List<int>();
            for (var i = 0; i < arten.Length; i++)
            {
                if (arten[i] != art) continue;
                var felder = mesh.SurfaceGetArrays(i);
                var punkte = felder[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                if (punkte.Length == 0) continue;
                var vorhanden = felder[(int)Mesh.ArrayType.Index];
                var versatz = ecken.Count;
                ecken.AddRange(punkte);
                if (vorhanden.VariantType == Variant.Type.Nil)
                    for (var k = 0; k < punkte.Length; k++) indizes.Add(versatz + k);
                else
                    foreach (var k in vorhanden.AsInt32Array()) indizes.Add(versatz + k);
            }
            if (indizes.Count == 0) continue;
            var felder2 = new Godot.Collections.Array();
            felder2.Resize((int)Mesh.ArrayType.Max);
            felder2[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
            felder2[(int)Mesh.ArrayType.Index] = indizes.ToArray();
            form.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, felder2);
            // eine eigene Fläche je Schnittart: undurchsichtig, ohne Beleuchtung (nur der Schatten zählt), gleiche Schnittart wie das Original
            form.SurfaceSetMaterial(form.GetSurfaceCount() - 1, new StandardMaterial3D { CullMode = (BaseMaterial3D.CullModeEnum)art, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded });
        }
        ArrayMesh? ergebnis = form.GetSurfaceCount() > 0 ? form : null;
        _schattenNetze[schluessel] = ergebnis;
        return ergebnis;
    }
}
