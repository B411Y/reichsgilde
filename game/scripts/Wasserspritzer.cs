using Godot;

namespace Reichsgilde;

/// <summary>
/// Wasser aus einem Eimer, das gegen ein brennendes Haus geschüttet wird (Löschkette, <c>Stadtansicht.Loeschkette.cs</c>): ein Schwall Tropfen fliegt in einem
/// Bogen nach vorn (lokal +Z, Länge = Skalierung in Z, ein Meter bei 1), spritzt am Ziel auseinander, dazu ein Hauch Dampf; alle 2,6 Sekunden ein Schwall. Ein
/// einziges Netz aus 48 Flächen, die Flugbahn rechnet der Shader (<c>wasserspritzer.gdshader</c>): ein Zeichenaufruf, keine Arbeit des Prozessors, kein Partikelsystem.
/// Netz und Material sind für alle Spritzer gemeinsam (die Uhr kommt von <see cref="Feuer"/>).
/// </summary>
public sealed partial class Wasserspritzer : Node3D
{
    private const int Tropfen = 48;
    private static ArrayMesh? _netz;
    private static ShaderMaterial? _material;

    private static ArrayMesh Netz()
    {
        if (_netz is not null) return _netz;
        var ecken = new Vector3[Tropfen * 4];
        var farben = new Color[Tropfen * 4];
        var uv = new Vector2[Tropfen * 4];
        var index = new int[Tropfen * 6];
        var kanten = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        for (var i = 0; i < Tropfen; i++)
        {
            for (var k = 0; k < 4; k++)
            {
                ecken[i * 4 + k] = Vector3.Zero;
                farben[i * 4 + k] = new Color((i + 0.5f) / Tropfen, 0, 0, 1);
                uv[i * 4 + k] = kanten[k];
            }
            var b = i * 4;
            (index[i * 6], index[i * 6 + 1], index[i * 6 + 2], index[i * 6 + 3], index[i * 6 + 4], index[i * 6 + 5]) = (b, b + 1, b + 2, b, b + 2, b + 3);
        }
        var felder = new Godot.Collections.Array();
        felder.Resize((int)Mesh.ArrayType.Max);
        felder[(int)Mesh.ArrayType.Vertex] = ecken;
        felder[(int)Mesh.ArrayType.Color] = farben;
        felder[(int)Mesh.ArrayType.TexUV] = uv;
        felder[(int)Mesh.ArrayType.Index] = index;
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, felder);
        netz.CustomAabb = new Aabb(new Vector3(-2, -1, -1), new Vector3(4, 4, 3)); // die Ecken liegen alle im Ursprung: die Form sagt der Shader nicht, darum die Hülle von Hand
        return _netz = netz;
    }

    public override void _Ready()
    {
        if (_material is null)
        {
            _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/wasserspritzer.gdshader") };
            Feuer.Zeitgeber(_material);   // gemeinsame Uhr und Tag/Nacht wie bei den Flammen
        }
        AddChild(new MeshInstance3D
        {
            Name = "Tropfen", Mesh = Netz(), MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 4f,
        });
    }
}
