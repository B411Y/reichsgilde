using Godot;

namespace Reichsgilde;

/// <summary>
/// Schatten der Passanten in allen Zoomstufen – ein Teil der <c>partial class Stadtansicht</c>.
/// Bis zur Zoomstufe 35 m werfen die Passanten echte Schatten (<see cref="FigurSchatten"/>, Sonne und Mond). Bei 70 und 140 m sind
/// echte Schatten für hunderte Figuren zu teuer (jede Figur kommt mit ihren Teilen noch einmal in den Schattendurchgang) und die Figuren sind klein;
/// dort zeichnet diese Datei stattdessen <b>einen Fleck je Passant</b>: eine flache Ellipse am Boden, in Schattenrichtung der Sonne gestreckt und
/// am Fuß verankert, alle zusammen in <b>einem</b> Vielfach-Zeichenaufruf (<see cref="MultiMesh"/>, Puffer in einem Zug).
/// <list type="bullet">
/// <item>Der Fleck <b>multipliziert</b> das fertige Bild (unbeleuchtet, <c>blend_mul</c>, ohne Tiefenschreiben, mit Tiefenprüfung – Häuser verdecken ihn). Er liegt im Pixel-Look
/// auf der Teilchen-Ebene, das Beleuchtungsbild sieht ihn nicht (sonst stünde dort ein undurchsichtiges Rechteck, siehe <see cref="PixelTeilchenEbene"/>); die harte Kante der
/// Ellipse rastert im Low-Res-Bild zu Weltpixeln, wie die Kante der echten Schatten.</item>
/// <item>Die Dunkelheit folgt dem Licht: Verhältnis Umgebungslicht zu Umgebung plus Sonne (bzw. Mond), mal der Schattendeckkraft; bei Wolken, Regen und
/// in der Dämmerung wird der Fleck blasser, wie die echten Schatten.</item>
/// <item>Startoption <c>--figurenschatten=blob|echt|aus</c> (nur für Vergleiche und Messungen): <c>blob</c> (Standard) echte Schatten nah, Flecken fern; <c>echt</c> immer echte; <c>aus</c> fern keine.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Wie die Passanten ihren Schatten zeigen: Flecken ab Zoom 70 (Standard), immer echt (teuer) oder fern gar nicht (Stand vor dieser Änderung).</summary>
    private enum FigurSchattenArt { Fleck, Echt, Aus }

    private static readonly FigurSchattenArt PassantenSchattenArt = Wasserarten.Startoption("figurenschatten") switch
    {
        "echt" => FigurSchattenArt.Echt,
        "aus" => FigurSchattenArt.Aus,
        _ => FigurSchattenArt.Fleck,
    };

    /// <summary>Halbe Breite des Flecks (Schulterbreite) in Metern und seine Länge je Meter Körpergröße bei Sonnenhöhe 45°; Grenzen der Länge.</summary>
    private const float FleckHalbBreite = 0.22f, FleckGroesse = 1.65f, FleckLaengeMin = 0.7f, FleckLaengeMax = 3.2f;

    /// <summary>So viel höher als der Boden der Figur liegt der Fleck (gegen Flackern; Hänge: der Fleck liegt flach).</summary>
    private const float FleckHoehe = 0.07f;

    private MultiMeshInstance3D? _fleckPassanten;
    private StandardMaterial3D? _fleckStoff;
    private float[] _fleckPuffer = Array.Empty<float>();
    private float _fleckDunkel = -1f;

    /// <summary>Einmal je Bild, nach den Passanten: Flecken an die Füße der sichtbaren Passanten setzen (oder alle ausblenden).</summary>
    private void PassantenFleckenAktualisieren()
    {
        var an = !_passantenSchatten && PassantenSchattenArt == FigurSchattenArt.Fleck && !_stadtVerdeckt && _sonne.ShadowEnabled && _passanten.Count > 0;
        if (!an)
        {
            if (_fleckPassanten is { Multimesh.VisibleInstanceCount: > 0 }) _fleckPassanten.Multimesh.VisibleInstanceCount = 0;
            return;
        }
        FleckenBereitstellen(_passanten.Count);
        var mm = _fleckPassanten!.Multimesh;

        // Schattenrichtung (Richtung des Lichts, flach) und Länge nach Sonnenhöhe
        var licht = (GlobalBasis.Inverse() * -_sonne.GlobalBasis.Z).Normalized();
        var flach = new Vector3(licht.X, 0, licht.Z);
        var hoehe = Mathf.Max(-licht.Y, 0.2f); // sin der Sonnenhöhe
        var richtung = flach.LengthSquared() > 1e-6f ? flach.Normalized() : Vector3.Right;
        var laenge = Mathf.Clamp(FleckGroesse * Mathf.Sqrt(Mathf.Max(1f - hoehe * hoehe, 0f)) / hoehe, FleckLaengeMin, FleckLaengeMax);
        var halb = laenge * 0.5f;
        var quer = new Vector3(-richtung.Z, 0, richtung.X);
        FleckDunkelSetzen(hoehe);

        var puffer = _fleckPuffer;
        var n = 0;
        foreach (var p in _passanten.Values)
        {
            var knoten = p.Knoten;
            if (!knoten.Visible || p.Figur is null || !ImBild(knoten.Position, 3f)) continue;
            var o = knoten.Position + richtung * (halb * 0.55f) + new Vector3(0, FleckHoehe, 0); // am Fuß verankert, zum größeren Teil in Schattenrichtung
            var i = n++ * 12;
            // Reihen der 3×4-Matrix: X-Achse = Längsrichtung (halbe Länge), Y unbenutzt (flach), Z-Achse = quer (halbe Breite)
            puffer[i + 0] = richtung.X * halb; puffer[i + 1] = 0; puffer[i + 2] = quer.X * FleckHalbBreite; puffer[i + 3] = o.X;
            puffer[i + 4] = 0;                 puffer[i + 5] = 1; puffer[i + 6] = 0;                         puffer[i + 7] = o.Y;
            puffer[i + 8] = richtung.Z * halb; puffer[i + 9] = 0; puffer[i + 10] = quer.Z * FleckHalbBreite; puffer[i + 11] = o.Z;
        }
        mm.VisibleInstanceCount = n;
        if (n > 0) mm.Buffer = puffer;
    }

    /// <summary>Legt Netz und Material einmal an und vergrößert den Puffer bei Bedarf.</summary>
    private void FleckenBereitstellen(int zahl)
    {
        if (_fleckPassanten is null)
        {
            // Ellipse als Fächer um die Mitte (Einheitskreis, 16 Ecken); die Instanz streckt sie
            var ecken = new Vector3[16 * 3];
            for (var k = 0; k < 16; k++)
            {
                float a0 = k * Mathf.Tau / 16f, a1 = (k + 1) * Mathf.Tau / 16f;
                ecken[k * 3] = Vector3.Zero;
                ecken[k * 3 + 1] = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                ecken[k * 3 + 2] = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
            }
            var felder = new Godot.Collections.Array();
            felder.Resize((int)Mesh.ArrayType.Max);
            felder[(int)Mesh.ArrayType.Vertex] = ecken;
            var netz = new ArrayMesh();
            netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, felder);
            _fleckStoff = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BlendMode = BaseMaterial3D.BlendModeEnum.Mul, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = false, AlbedoColor = Colors.White, DisableFog = true,
            };
            netz.SurfaceSetMaterial(0, _fleckStoff);
            _fleckPassanten = new MultiMeshInstance3D
            {
                Name = "Passantenflecken", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, GIMode = GeometryInstance3D.GIModeEnum.Disabled,
                ExtraCullMargin = 16384f, // die Flecken liegen verstreut über die Stadt: nie wegen der Umgrenzung ausblenden
                Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = netz },
            };
            AddChild(_fleckPassanten);
        }
        var mm = _fleckPassanten.Multimesh;
        if (mm.InstanceCount < zahl)
        {
            var neu = Math.Max(zahl + 64, mm.InstanceCount * 2);
            mm.VisibleInstanceCount = 0;
            mm.InstanceCount = neu;
            _fleckPuffer = new float[neu * 12];
        }
        else if (_fleckPuffer.Length < mm.InstanceCount * 12) _fleckPuffer = new float[mm.InstanceCount * 12];
    }

    /// <summary>
    /// Grauwert des Flecks: Anteil des Lichts, der im Schatten bleibt (Umgebung ÷ Umgebung plus gerichtetes Licht, das schräg einfällt), mit der Schattendeckkraft verblasst.
    /// Nach unten begrenzt – die echten Schatten sind im Pixel-Look auch nie ganz schwarz.
    /// </summary>
    private void FleckDunkelSetzen(float sonnenhoehe)
    {
        var umgebung = _umgebung.Environment;
        float Hell(Color c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
        var ambient = Hell(umgebung.AmbientLightColor) * umgebung.AmbientLightEnergy;
        var direkt = Hell(_sonne.LightColor) * _sonne.LightEnergy * sonnenhoehe;
        var verhaeltnis = ambient / Mathf.Max(ambient + direkt, 0.001f);
        var dunkel = Mathf.Lerp(1f, Mathf.Max(verhaeltnis, FleckDunkelMin), _sonne.ShadowOpacity);
        if (Mathf.Abs(dunkel - _fleckDunkel) < 0.01f) return;
        _fleckDunkel = dunkel;
        _fleckStoff!.AlbedoColor = new Color(dunkel, dunkel, dunkel, 1f);
    }

    /// <summary>Untergrenze des Flecks: so dunkel wie die dunkelste Stufe der echten Schatten im Pixel-Look etwa wirkt.</summary>
    private const float FleckDunkelMin = 0.5f;
}
