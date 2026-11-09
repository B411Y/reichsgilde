using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Vorwärmen der Werkstatt-Pinsel beim Öffnen (hinter dem Ladebildschirm) – ein Teil der <c>partial class Stadtansicht</c>. Was der erste Tupfer
/// eines Pinsels sonst selbst erledigen müsste, geschieht hier einmal: die Netze aller Felsformen in allen Feinheiten und ihr Material laden, die
/// Netze der Pflanzen des Naturpinsels, und je ein Stück jeder Art (Pinselkreis, Vorschau, Felsgeist, Felsstück) für die ersten Bilder vor die Kamera
/// stellen, damit der Grafiktreiber ihre Shader übersetzt, solange noch der Ladebildschirm steht (<see cref="WerkstattVorwaermenEnde"/> räumt sie weg).
/// </summary>
public partial class Stadtansicht
{
    private Node3D? _wsVorwaermen;

    /// <summary>Lädt, was die Pinsel brauchen, und stellt die Probestücke auf (Kartenwerkstatt._Ready, vor den ersten Bildern).</summary>
    public void WerkstattVorwaermen()
    {
        if (_entwurf is null) return;
        var gestein = _entwurf.Grundgestein.Wirksam;
        foreach (var form in Felsformen.Alle)
        {
            for (var stufe = 0; stufe <= 2; stufe++) FelsNetz(form.Kennung, stufe);
            FelsStoff(gestein, form.Kennung);
        }
        foreach (var art in PinselArten.Keys)
            if (_entwurf.Katalog.Finde(art) is { Modell: { } datei, Knoten: { } knoten }) NaturNetz(datei, knoten + 1, pflicht: false);

        // Probestücke mitten im Bild: Shader und Pipelines entstehen beim ersten Zeichnen, nicht beim Laden
        _wsVorwaermen = new Node3D { Name = "Vorwaermen", Position = _kameraDrehung.Position with { Y = BodenHoehe(_kameraDrehung.Position.X, _kameraDrehung.Position.Z) + 1 } };
        AddChild(_wsVorwaermen);
        VorschauDreiecke(new[] { Vector3.Zero, Vector3.Right, Vector3.Back }, Colors.White); // legt den Stoff der Vorschau an (das Netz leert das nächste Bild)
        var farbig = new ArrayMesh();
        var felder = new Godot.Collections.Array();
        felder.Resize((int)Mesh.ArrayType.Max);
        felder[(int)Mesh.ArrayType.Vertex] = new[] { Vector3.Zero, Vector3.Right, Vector3.Back };
        felder[(int)Mesh.ArrayType.Color] = new[] { Colors.White, Colors.White, Colors.White };
        farbig.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, felder);
        _wsVorwaermen.AddChild(new MeshInstance3D { Mesh = farbig, MaterialOverride = _wsVorschauStoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _wsVorwaermen.AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.97f, OuterRadius = 1f, Rings = 48, RingSegments = 6 },
            MaterialOverride = ImmerSichtbar(Transparent(Colors.White, 0.9f)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        if (FelsNetz("block", 1) is { } fels)
        {
            _wsVorwaermen.AddChild(new MeshInstance3D { Mesh = fels, MaterialOverride = FelsStoff(gestein, "block"), Position = new Vector3(3, 0, 0) });
            _wsVorwaermen.AddChild(new MeshInstance3D
            {
                Mesh = fels, MaterialOverride = Transparent(new Color(0.92f, 0.9f, 0.82f), 0.55f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(-3, 0, 0),
            });
        }
    }

    /// <summary>Nach den ersten Bildern: die Probestücke wieder weg.</summary>
    public void WerkstattVorwaermenEnde()
    {
        if (_wsVorwaermen is not null) KnotenEntfernen(_wsVorwaermen);
        _wsVorwaermen = null;
    }
}
