using Godot;

namespace Reichsgilde;

/// <summary>
/// Schatten der Passanten in allen Zoomstufen – ein Teil der <c>partial class Stadtansicht</c>.
/// Bis zur Zoomstufe 25 m werfen die Passanten echte Schatten (<see cref="FigurSchatten"/>, Sonne und Mond). Bei 50 und 100 m sind
/// echte Schatten für hunderte Figuren zu teuer (jede Figur kommt mit ihren Teilen noch einmal in den Schattendurchgang) und die Figuren sind klein.
/// Dort wirft stattdessen <b>ein einfacher Stellvertreter je Passant</b> den Schatten: ein schlankes, senkrechtes Achteck-Prisma in Körpergröße am Fuß, das
/// <b>nur in die Schattenkarte der Sonne</b> gezeichnet wird (<see cref="GeometryInstance3D.ShadowCastingSetting.ShadowsOnly"/>), alle zusammen in <b>einem</b>
/// Vielfach-Zeichenaufruf (<see cref="MultiMesh"/>, Puffer in einem Zug).
/// <list type="bullet">
/// <item><b>Der Schatten ist ein echter Sonnenschatten</b> (früher ein Fleck, der das fertige Bild multiplizierte – der addierte sich, wo sich Flecken überlappten, und lag
/// im Pixel-Look neben dem Stufenlicht, das Häuserschatten bekommen, dunkler als diese und doppelt dunkel im Hausschatten): Die Schattenkarte ist binär, überlappende
/// Schatten werden nicht dunkler, ein Schatten im Schatten eines Hauses bleibt so dunkel wie dieser, und Tag, Nacht, Wetter, Jahreszeit, Deckkraft und im Pixel-Look
/// Stufenlicht und weiche Kante sind genau die der Gebäudeschatten – ohne eigene Rechnung.</item>
/// <item>Breite, Richtung und <b>Länge</b> bleiben die des früheren Flecks (Nutzer, 09.11.2026: der echte Schatten war bei tiefer Sonne zu lang): schulterbreit (0,44 m), in
/// Schattenrichtung der Sonne, die Länge folgt der Sonnenhöhe (Körpergröße geteilt durch den Tangens, zwischen 0,7 und 3,2 m, vom Fuß aus 77,5 % davon). Dafür ist das Prisma bei
/// flacher Sonne <b>niedriger</b> als der Mensch (Höhe = Reichweite mal Tangens der wahren Sonnenhöhe, höchstens Körpergröße), sein Schatten erreicht also nie mehr als die alte Länge.</item>
/// <item>Startoption <c>--figurenschatten=blob|echt|aus</c> (nur für Vergleiche und Messungen): <c>blob</c> (Standard, Name aus der Zeit der Flecken) echte Schatten nah, Stellvertreter fern; <c>echt</c> immer echte; <c>aus</c> fern keine.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Wie die Passanten ihren Schatten zeigen: Stellvertreter ab Zoom 50 (Standard), immer echt (teuer) oder fern gar nicht (Stand vor dem Fleck).</summary>
    private enum FigurSchattenArt { Fleck, Echt, Aus }

    private static readonly FigurSchattenArt PassantenSchattenArt = Wasserarten.Startoption("figurenschatten") switch
    {
        "echt" => FigurSchattenArt.Echt,
        "aus" => FigurSchattenArt.Aus,
        _ => FigurSchattenArt.Fleck,
    };

    /// <summary>Halbe Breite des Stellvertreters (Schulterbreite) und seine größte Höhe in Metern (ein Mensch; Orks und Oger größer nach dem Maßstab ihres Modells).</summary>
    private const float FleckHalbBreite = 0.22f, FleckGroesse = 1.65f;

    /// <summary>
    /// Länge des Schattens eines Menschen bei Sonnenhöhe 45° und ihre Grenzen (Meter) – die alte Länge des Flecks (Nutzer, 09.11.2026): Bei flacher Sonne bleibt der
    /// Schatten bei 3,2 m (statt Körpergröße ÷ Tangens, bei 10° über 9 m), bei hoher Sonne mindestens 0,7 m. Der Schatten reicht vom Fuß aus in Schattenrichtung
    /// nur <see cref="FleckReichweite"/> dieser Länge weit (der alte Fleck lag zu einem Teil hinter dem Fuß).
    /// </summary>
    private const float FleckLaengeMin = 0.7f, FleckLaengeMax = 3.2f, FleckReichweite = 0.775f;

    /// <summary>Wie weit (Meter) ein Passant außerhalb des Bildes noch mitzählt: Sein Schatten kann ins Bild ragen (mindestens 3 m, sonst Schattenlänge plus 1 m).</summary>
    private const float FleckRandMin = 3f;

    private MultiMeshInstance3D? _fleckPassanten;
    private float[] _fleckPuffer = Array.Empty<float>();

    /// <summary>Einmal je Bild, nach den Passanten: Stellvertreter an die Füße der sichtbaren Passanten setzen (oder alle ausblenden).</summary>
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

        // Länge des Schattens: wie der alte Fleck (Körpergröße ÷ Tangens der Sonnenhöhe, mit Grenzen, bei sehr flacher Sonne ab sin 0,2 gerechnet).
        // Das Prisma ist so hoch, dass sein echter Sonnenschatten genau so weit reicht (Höhe = Reichweite × Tangens der wahren Sonnenhöhe), höchstens so hoch wie der Mensch.
        var licht = (GlobalBasis.Inverse() * -_sonne.GlobalBasis.Z).Normalized();
        var hoehe = Mathf.Max(-licht.Y, 0.02f); // sin der Sonnenhöhe
        var tangens = hoehe / Mathf.Sqrt(Mathf.Max(1f - hoehe * hoehe, 1e-6f));
        var sinGerechnet = Mathf.Max(hoehe, 0.2f);
        var laenge = Mathf.Clamp(FleckGroesse * Mathf.Sqrt(Mathf.Max(1f - sinGerechnet * sinGerechnet, 0f)) / sinGerechnet, FleckLaengeMin, FleckLaengeMax);
        var reichweite = laenge * FleckReichweite;
        var maxHoehe = reichweite * tangens; // höher als das wäre der Schatten länger als die alte Länge
        var rand = Mathf.Clamp(reichweite + 1f, FleckRandMin, 40f);

        var puffer = _fleckPuffer;
        var n = 0;
        foreach (var p in _passanten.Values)
        {
            var knoten = p.Knoten;
            if (!knoten.Visible || p.Figur is not { } figur || !ImBild(knoten.Position, rand)) continue;
            var o = knoten.Position;
            var i = n++ * 12;
            var breite = FleckHalbBreite * figur.Modell.Scale.X;
            var hohe = Mathf.Min(FleckGroesse * figur.Modell.Scale.Y, maxHoehe);
            // Reihen der 3×4-Matrix: senkrechtes Prisma, Grundriss rund, Höhe nach oben
            puffer[i + 0] = breite; puffer[i + 1] = 0;    puffer[i + 2] = 0;      puffer[i + 3] = o.X;
            puffer[i + 4] = 0;      puffer[i + 5] = hohe; puffer[i + 6] = 0;      puffer[i + 7] = o.Y;
            puffer[i + 8] = 0;      puffer[i + 9] = 0;    puffer[i + 10] = breite; puffer[i + 11] = o.Z;
        }
        mm.VisibleInstanceCount = n;
        if (n > 0) mm.Buffer = puffer;
    }

    /// <summary>Prisma mit Einheitsradius und Höhe 1 (Fuß bei y = 0) aus <paramref name="ecken"/> Seiten und einem Deckel; die Instanz streckt es. Nur Eckpunkte: Die Schattenkarte braucht nichts anderes.</summary>
    private static ArrayMesh FleckNetz(int ecken)
    {
        var punkte = new List<Vector3>();
        for (var k = 0; k < ecken; k++)
        {
            float a0 = k * Mathf.Tau / ecken, a1 = (k + 1) * Mathf.Tau / ecken;
            Vector3 u0 = new(Mathf.Cos(a0), 0, Mathf.Sin(a0)), u1 = new(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 o0 = u0 + Vector3.Up, o1 = u1 + Vector3.Up;
            punkte.AddRange(new[] { u0, o0, o1, u0, o1, u1 }); // Seite
            punkte.AddRange(new[] { Vector3.Up, o1, o0 });     // Deckel
        }
        var felder = new Godot.Collections.Array();
        felder.Resize((int)Mesh.ArrayType.Max);
        felder[(int)Mesh.ArrayType.Vertex] = punkte.ToArray();
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, felder);
        // beidseitig: Die Schattenkarte soll unabhängig von der Umlaufrichtung werfen
        netz.SurfaceSetMaterial(0, new StandardMaterial3D { CullMode = BaseMaterial3D.CullModeEnum.Disabled, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded });
        return netz;
    }

    /// <summary>Legt Netz und Knoten einmal an und vergrößert den Puffer bei Bedarf.</summary>
    private void FleckenBereitstellen(int zahl)
    {
        if (_fleckPassanten is null)
        {
            var netz = FleckNetz(8);
            _fleckPassanten = new MultiMeshInstance3D
            {
                Name = "Passantenschatten", CastShadow = GeometryInstance3D.ShadowCastingSetting.ShadowsOnly, GIMode = GeometryInstance3D.GIModeEnum.Disabled,
                ExtraCullMargin = 16384f, // die Stellvertreter liegen verstreut über die Stadt: nie wegen der Umgrenzung ausblenden
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
}
