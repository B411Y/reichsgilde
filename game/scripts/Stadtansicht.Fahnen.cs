using Godot;

namespace Reichsgilde;

/// <summary>
/// Fahnen im Wind (Teil der <c>partial class Stadtansicht</c>): Das Tuch ist ein feines Netz (8 × 4 Felder) mit dem Shader
/// <c>res://assets/shader/fahne.gdshader</c>, der es im Wind biegt – ohne Skript je Bild. Der Wind ist für alle Fahnen gleich
/// (globale Shader-Uniforms <c>wind_richtung</c> und <c>wind_staerke</c> in <c>project.godot</c>).
///
/// Das Netz liegt in der Ebene XY: der Mast bei X = 0, das freie Ende bei X = Länge, die Mitte in Höhe 0. Der Shader schlägt
/// das Tuch senkrecht dazu (Z) aus. Mit einer <see cref="Basis"/> wird es an die Stelle gedreht, an der die Fahne hängt.
/// </summary>
public partial class Stadtansicht
{
    private const string FahnenShaderPfad = "res://assets/shader/fahne.gdshader";

    /// <summary>Felder des Netzes entlang der Länge und über die Höhe. Höhe gerade, damit die Kerbe eine Ecke trifft.</summary>
    private const int FahnenFelderLaenge = 8, FahnenFelderHoehe = 4;

    private static Shader? _fahnenShader;
    private readonly Dictionary<(float Laenge, float Hoehe, float Kerbe), ArrayMesh> _fahnenNetze = new();
    private readonly Dictionary<(Color Farbe, bool Wand, Color? Rand), ShaderMaterial> _fahnenStoffe = new();
    /// <summary>Zu jedem Modell-Mesh: das Mesh ohne Tuch und die Lage des neuen Tuchs (einmal ermittelt).</summary>
    private readonly Dictionary<Mesh, (ArrayMesh OhneTuch, FahnenLage Tuch)?> _modellFahnen = new();

    /// <summary>Wo das Tuch einer Modell-Fahne sitzt: Mastpunkt, Achsen und Maße im Raum des Knotens „Fahne“.</summary>
    private readonly record struct FahnenLage(Vector3 Mast, Vector3 Laengsachse, Vector3 Hoehenachse, float Laenge, float Hoehe, float Kerbe);

    /// <summary>Breite des Randes (Meter), wenn ein Tuch einen hat (<see cref="FahnenStoff"/>).</summary>
    private const float FahnenRandBreite = 0.4f;

    /// <summary>Der Fahnen-Stoff in einer Farbe – je Farbe nur einmal, alle Fahnen dieser Farbe teilen ihn.
    /// <paramref name="wand"/>: Wandbehang, der nur von der Wand weg ausschlägt; <paramref name="rand"/>: ein Rand ringsum in dieser Farbe.</summary>
    private ShaderMaterial FahnenStoff(Color farbe, bool wand = false, Color? rand = null)
    {
        if (_fahnenStoffe.TryGetValue((farbe, wand, rand), out var stoff)) return stoff;
        _fahnenShader ??= GD.Load<Shader>(FahnenShaderPfad);
        stoff = new ShaderMaterial { Shader = _fahnenShader };
        stoff.SetShaderParameter("farbe", farbe);
        stoff.SetShaderParameter("wandbehang", wand);
        if (rand is { } r)
        {
            stoff.SetShaderParameter("rand_farbe", r);
            stoff.SetShaderParameter("rand_breite", FahnenRandBreite);
        }
        return _fahnenStoffe[(farbe, wand, rand)] = stoff;
    }

    /// <summary>
    /// Das Netz eines Tuchs, <paramref name="laenge"/> × <paramref name="hoehe"/> Meter, in der Ebene XY (Normale +Z).
    /// UV: u (0 am Mast bis 1 am freien Ende), v über die Höhe; UV2: Länge und Höhe in Metern für den Shader.
    /// <paramref name="kerbe"/> (Anteil der Länge) schneidet ein V in das freie Ende (Schwalbenschwanz), 0 = gerade.
    /// </summary>
    private ArrayMesh FahnenNetz(float laenge, float hoehe, float kerbe = 0)
    {
        var schluessel = (MathF.Round(laenge, 3), MathF.Round(hoehe, 3), MathF.Round(kerbe, 3));
        if (_fahnenNetze.TryGetValue(schluessel, out var vorhanden)) return vorhanden;

        const int nu = FahnenFelderLaenge, nv = FahnenFelderHoehe;
        var ecken = new Vector3[(nu + 1) * (nv + 1)];
        var normalen = new Vector3[ecken.Length];
        var uv = new Vector2[ecken.Length];
        var uv2 = new Vector2[ecken.Length];
        for (var j = 0; j <= nv; j++)
            for (var i = 0; i <= nu; i++)
            {
                float u = (float)i / nu, v = (float)j / nv;
                var ende = 1 - kerbe * (1 - MathF.Abs(2 * v - 1)); // Länge dieser Reihe: in der Mitte um die Kerbe kürzer
                var n = j * (nu + 1) + i;
                ecken[n] = new Vector3(u * laenge * ende, (v - 0.5f) * hoehe, 0);
                normalen[n] = Vector3.Back;
                uv[n] = new Vector2(u, v);
                uv2[n] = new Vector2(laenge, hoehe);
            }
        var indizes = new int[nu * nv * 6];
        var k = 0;
        for (var j = 0; j < nv; j++)
            for (var i = 0; i < nu; i++)
            {
                int a = j * (nu + 1) + i, b = a + 1, c = a + nu + 1, d = c + 1;
                // gegen den Uhrzeigersinn von +Z gesehen: Vorderseite zeigt nach +Z (X × Y = Z)
                (indizes[k++], indizes[k++], indizes[k++]) = (a, b, d);
                (indizes[k++], indizes[k++], indizes[k++]) = (a, d, c);
            }

        var felder = new Godot.Collections.Array();
        felder.Resize((int)Mesh.ArrayType.Max);
        felder[(int)Mesh.ArrayType.Vertex] = ecken;
        felder[(int)Mesh.ArrayType.Normal] = normalen;
        felder[(int)Mesh.ArrayType.TexUV] = uv;
        felder[(int)Mesh.ArrayType.TexUV2] = uv2;
        felder[(int)Mesh.ArrayType.Index] = indizes;
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, felder);
        // Das Tuch schlägt aus der Fläche heraus: Der Umriss muss das einschließen, sonst verschwindet es am Bildrand zu früh.
        netz.CustomAabb = new Aabb(new Vector3(0, -hoehe / 2, -0.4f * laenge), new Vector3(laenge, hoehe, 0.8f * laenge));
        return _fahnenNetze[schluessel] = netz;
    }

    /// <summary>
    /// Ein flatterndes Tuch. <paramref name="mast"/>: Punkt der Mastseite (Mitte der festen Kante) im Raum von <paramref name="eltern"/>;
    /// <paramref name="laengsachse"/>: vom Mast zum freien Ende; <paramref name="hoehenachse"/>: Richtung der Höhe (senkrecht dazu).
    /// Das Tuch steht dann in der Ebene beider Achsen; der Wind schlägt es senkrecht dazu aus.
    /// </summary>
    private MeshInstance3D FahnenTuch(Vector3 mast, Vector3 laengsachse, Vector3 hoehenachse, float laenge, float hoehe, Color farbe,
        Node3D? eltern = null, float kerbe = 0, bool wand = false, Color? rand = null)
    {
        laengsachse = laengsachse.Normalized();
        hoehenachse = hoehenachse.Normalized();
        var tuch = new MeshInstance3D
        {
            Name = "Tuch",
            Mesh = FahnenNetz(laenge, hoehe, kerbe),
            MaterialOverride = FahnenStoff(farbe, wand, rand),
            Transform = new Transform3D(new Basis(laengsachse, hoehenachse, laengsachse.Cross(hoehenachse)), mast),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        (eltern ?? this).AddChild(tuch);
        return tuch;
    }

    /// <summary>Ein Tuch an einem senkrechten Mast: weht in Richtung <paramref name="weg"/> (±X oder ±Z) vom Mast fort, Oberkante bei <paramref name="mast"/>.Y + <paramref name="hoehe"/> / 2.</summary>
    private MeshInstance3D FahnenTuchAmMast(Vector3 mast, Vector3 weg, float laenge, float hoehe, Color farbe, Node3D? eltern = null, float kerbe = 0, Color? rand = null)
        => FahnenTuch(mast, weg, Vector3.Up, laenge, hoehe, farbe, eltern, kerbe, rand: rand);

    /// <summary>
    /// Ein Tuch, das von einer Stange herabhängt (Wandbehang): fest an der Oberkante bei <paramref name="oben"/> (Mitte der Kante),
    /// <paramref name="breitenachse"/> läuft über die Breite, <paramref name="nachAussen"/> ist die Seite, auf die es vor allem ausschlägt.
    /// </summary>
    private MeshInstance3D FahnenBehang(Vector3 oben, Vector3 breitenachse, Vector3 nachAussen, float laenge, float breite, Color farbe, Node3D? eltern = null)
    {
        // Längsachse = abwärts; Normale = Längs × Breite soll nach außen zeigen, sonst die Breitenachse umdrehen
        var breit = breitenachse.Normalized();
        if (Vector3.Down.Cross(breit).Dot(nachAussen) < 0) breit = -breit;
        return FahnenTuch(oben, Vector3.Down, breit, laenge, breite, farbe, eltern, wand: true);
    }

    // ── Fahnen der Modelle (.glb) ───────────────────────────────────────────

    /// <summary>
    /// Tauscht in einem Modell-Knoten „Fahne“ (Mast „Holz“, Knauf „Eisen“, Tuch „Hausfarbe“, ohne UV) das starre Tuch gegen ein flatterndes:
    /// Das alte Tuch fällt aus dem Mesh, ein feines Netz aus seinem Umriss kommt als Kind des Knotens dazu (blendet also mit ihm aus).
    /// Hat das Mesh die erwarteten Flächen nicht, bleibt es unverändert.
    /// </summary>
    private void ModellFahneBelebt(MeshInstance3D fahne, Color farbe)
    {
        if (fahne.Mesh is not { } mesh) return;
        if (!_modellFahnen.TryGetValue(mesh, out var tausch)) _modellFahnen[mesh] = tausch = ModellFahneVorbereiten(mesh);
        if (tausch is not { } t) return;
        fahne.Mesh = t.OhneTuch;
        var l = t.Tuch;
        FahnenTuch(l.Mast, l.Laengsachse, l.Hoehenachse, l.Laenge, l.Hoehe, farbe, fahne, l.Kerbe);
    }

    /// <summary>Sucht die Tuch-Fläche („Hausfarbe“) und den Mast („Holz“) im Mesh und baut das Mesh ohne Tuch. Null, wenn sie fehlen.</summary>
    private static (ArrayMesh, FahnenLage)? ModellFahneVorbereiten(Mesh mesh)
    {
        int tuchFlaeche = -1, mastFlaeche = -1;
        for (var i = 0; i < mesh.GetSurfaceCount(); i++)
            switch ((mesh.SurfaceGetMaterial(i) as BaseMaterial3D)?.ResourceName)
            {
                case "Hausfarbe": tuchFlaeche = i; break;
                case "Holz": mastFlaeche = i; break;
            }
        if (tuchFlaeche < 0 || mastFlaeche < 0) return null;

        var punkte = Ecken(mesh, tuchFlaeche);
        var mastPunkte = Ecken(mesh, mastFlaeche);
        if (punkte.Length == 0 || mastPunkte.Length == 0) return null;
        var tuch = Umriss(punkte);
        var kasten = Umriss(mastPunkte);

        // Achsen: lang = Länge des Tuchs, dünn = Dicke (Ausschlagsrichtung), die dritte = Höhe
        var masse = new[] { tuch.Size.X, tuch.Size.Y, tuch.Size.Z };
        int lang = 0, duenn = 0;
        for (var a = 1; a < 3; a++)
        {
            if (masse[a] > masse[lang]) lang = a;
            if (masse[a] < masse[duenn]) duenn = a;
        }
        if (lang == duenn) return null;
        var hoch = 3 - lang - duenn;
        Vector3 Achse(int a) => a == 0 ? Vector3.Right : a == 1 ? Vector3.Up : Vector3.Back;
        var mitte = tuch.GetCenter();
        var mastMitte = kasten.GetCenter();
        // Der Mast steht an dem Ende, das seinem Mittelpunkt näher liegt
        var richtung = mastMitte[lang] > mitte[lang] ? -1f : 1f; // Richtung vom Mast zum freien Ende: weg vom Mastende
        var mastEnde = richtung > 0 ? tuch.Position[lang] : tuch.End[lang];
        var mast = mitte;
        mast[lang] = mastEnde;
        var laengsachse = Achse(lang) * richtung;
        var hoehenachse = Achse(hoch);
        var laenge = masse[lang];
        var hoehe = masse[hoch];

        // Kerbe (Schwalbenschwanz): Wie weit reicht das Tuch auf Höhe der Mitte? Gerade Tücher haben dort keine Ecke.
        var reicht = 0f;
        foreach (var q in punkte)
            if (MathF.Abs(q[hoch] - mitte[hoch]) < 0.1f * hoehe)
                reicht = MathF.Max(reicht, MathF.Abs(q[lang] - mastEnde));
        var kerbe = reicht > 0 && reicht < laenge * 0.98f ? 1 - reicht / laenge : 0;

        // Mesh ohne das alte Tuch: die übrigen Flächen unverändert kopieren
        var ohne = new ArrayMesh();
        for (var i = 0; i < mesh.GetSurfaceCount(); i++)
        {
            if (i == tuchFlaeche) continue;
            ohne.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, mesh.SurfaceGetArrays(i));
            ohne.SurfaceSetMaterial(ohne.GetSurfaceCount() - 1, mesh.SurfaceGetMaterial(i));
        }
        return (ohne, new FahnenLage(mast, laengsachse, hoehenachse, laenge, hoehe, kerbe));
    }

    private static Vector3[] Ecken(Mesh mesh, int flaeche) => mesh.SurfaceGetArrays(flaeche)[(int)Mesh.ArrayType.Vertex].AsVector3Array();

    private static Aabb Umriss(Vector3[] punkte)
    {
        var kasten = new Aabb(punkte[0], Vector3.Zero);
        foreach (var q in punkte) kasten = kasten.Expand(q);
        return kasten;
    }
}
