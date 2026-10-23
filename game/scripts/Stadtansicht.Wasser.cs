using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Das Wasser der Karte – ein Teil der <c>partial class Stadtansicht</c> (Kartenwerkstatt, Schritt 5c). Der Kern rechnet Flüsse, Bäche, Seen und Teiche samt
/// Spiegel und Bett (<see cref="Wasserform"/>) und die Übergänge darüber (<see cref="Wasserplan"/>); das Bett steckt schon im Boden
/// (Stadtansicht.Gelaende.cs). Hier steht, was die Darstellung daraus macht:
/// <list type="bullet">
/// <item><b>Flüsse und Bäche</b> (<see cref="FlussNetz"/>): ein Band entlang der Mittellinie auf der Höhe des Spiegels (er fällt nur, steigt nie), etwas breiter als
/// der Fluss – die Ränder verschwinden im Ufer. Wo der gezeichnete Boden das Bett nicht zeigt (ein schmaler Bach, eine Furt), liegt das Band knapp darüber
/// (<see cref="Wasserdeckel"/>). Die Eckenwerte (Tiefe, Breite, Strömung) lesen <c>wasser.gdshader</c>: Er färbt nach Tiefe, lässt das Ufer
/// durchsichtig und mit Schaum auslaufen und trägt Wellen mit der Strömung flussab.</item>
/// <item><b>Seen und Teiche</b> (<see cref="SeeNetz"/>): eine Fläche genau über den Zellen des Bodens (<see cref="RasterDreiecke"/>) auf der Höhe des Wasserstands, die Tiefe
/// an jeder Ecke aus dem Boden. Das Wasser liegt still.</item>
/// <item><b>Hafenbecken</b> (<see cref="BeckenNetz"/>): vor der Kaimauer eines Hafens hebt der Kern das Ufer aus; darin liegt Wasser wie in einem See, auf dem Spiegel des
/// Flusses davor, und läuft dort aus, wo der Fluss beginnt.</item>
/// <item><b>Licht</b>: Sonne und Mond spiegeln sich über die Beleuchtung der Szene; den Himmel liefert die Farbe <c>himmel</c>, die <see cref="WasserFarbeNachfuehren"/> jedes
/// Bild auf die Himmelsfarbe der Tageszeit setzt (die Szene hat keinen Himmel zum Spiegeln).</item>
/// <item><b>Übergänge, Fähren, Hafen</b>: Stadtansicht.Wasser.Uebergaenge.cs.</item>
/// </list>
/// Im Spiel entsteht das Wasser einmal mit der Karte (<see cref="GewaesserBauen"/>), in der Kartenwerkstatt neu, sobald ein Befehl Gewässer, Wege, Fähren oder
/// Gelände geändert hat (Stadtansicht.Kartenwerkstatt.Wasser.cs).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Das Band eines Flusses ist um diesen Faktor breiter als der Fluss: Die Ränder liegen unter dem Ufer, und das Wasser läuft dort durchsichtig aus.</summary>
    private const float FlussRandFaktor = 1.15f;

    /// <summary>Die Punkte des Bandes liegen so weit (Meter) auseinander (der Kern gibt einen Punkt je Meter).</summary>
    private const int FlussSchritt = 2;

    /// <summary>So hoch (Meter) liegt das Wasser mindestens über dem gezeichneten Boden (und dem Weg einer Furt) in der Mitte des Bandes.</summary>
    private const float WasserUeberBoden = 0.03f;

    private Node3D? _gewaesserKnoten;
    private static Shader? _wasserShader;
    private ShaderMaterial? _wasserStoff;
    private Color _wasserHimmel = new(-1, 0, 0);

    /// <summary>Das Wasser, das die Ansicht zeichnet: im Spiel das der Karte, in der Werkstatt das des Entwurfs (folgt jeder Änderung von selbst).</summary>
    private Wasserform? GewaesserForm => _gelaende?.Wasser;

    /// <summary>Brücken, Furten und Fähren der Karte bzw. des Entwurfs (null ohne Karte).</summary>
    private Wasserplan? Uebergangsplan => _entwurf?.Wasserplan ?? _karte?.Wasserplan;

    /// <summary>
    /// Baut Wasserflächen, Brücken, Furten, Fähren neu auf (oder zum ersten Mal). Eine Karte ohne Wasser bekommt nichts und kostet nichts.
    /// </summary>
    private void GewaesserBauen()
    {
        if (_gewaesserKnoten is not null) KnotenEntfernen(_gewaesserKnoten);
        _gewaesserKnoten = null;
        UebergaengeVergessen();
        if (GewaesserForm is not { IstLeer: false } form) return;

        _gewaesserKnoten = new Node3D { Name = "Gewaesser" };
        AddChild(_gewaesserKnoten);
        _wasserShader ??= GD.Load<Shader>("res://assets/shader/wasser.gdshader");
        _wasserStoff = new ShaderMaterial { Shader = _wasserShader, ResourceName = "Wasser", RenderPriority = -1 };
        _wasserHimmel = new Color(-1, 0, 0); // WasserFarbeNachfuehren setzt die Farbe im nächsten Bild
        WasserFarbeNachfuehren();

        foreach (var lauf in form.Laeufe)
            if (FlussNetz(lauf) is { } netz)
                _gewaesserKnoten.AddChild(WasserInstanz("Fluss_" + lauf.Name, netz));
        foreach (var see in form.Seen)
            if (SeeNetz(see) is { } netz)
                _gewaesserKnoten.AddChild(WasserInstanz("See_" + see.Name, netz));
        foreach (var becken in form.Becken)
            if (BeckenNetz(becken, form) is { } netz)
                _gewaesserKnoten.AddChild(WasserInstanz("Becken_" + becken.Name, netz));
        if (Uebergangsplan is { } plan) UebergaengeBauen(_gewaesserKnoten, plan);
    }

    private MeshInstance3D WasserInstanz(string name, ArrayMesh netz) => new()
    {
        Name = name, Mesh = netz, MaterialOverride = _wasserStoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    /// <summary>Jedes Bild: der Himmel im Wasser hat die Farbe des Himmels zur Tageszeit (die Beleuchtung stellt <c>BackgroundColor</c> ein).</summary>
    private void WasserFarbeNachfuehren()
    {
        if (_wasserStoff is null) return;
        var farbe = _umgebung.Environment.BackgroundColor;
        if (farbe == _wasserHimmel) return;
        _wasserHimmel = farbe;
        _wasserStoff.SetShaderParameter("himmel", new Vector3(farbe.R, farbe.G, farbe.B));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Flüsse und Bäche
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Das Band eines Flusses: je ein Punktepaar links und rechts der Mittellinie (alle <see cref="FlussSchritt"/> Meter), auf der Höhe des Spiegels.
    /// UV = (quer −1,15 … 1,15, Stelle), UV2 = Strömung nach x und z, COLOR = (Tiefe, halbe Breite, 0, 1) – wie <c>wasser.gdshader</c> sie liest.
    /// </summary>
    private ArrayMesh? FlussNetz(Wasserlauf lauf)
    {
        var p = lauf.Punkte;
        if (p.Count < 2) return null;
        var reihen = new List<int>();
        for (var i = 0; i < p.Count; i += FlussSchritt) reihen.Add(i);
        if (reihen[^1] != p.Count - 1) reihen.Add(p.Count - 1);

        var ecken = new List<Vector3>(reihen.Count * 2);
        var uv = new List<Vector2>(ecken.Capacity);
        var uv2 = new List<Vector2>(ecken.Capacity);
        var farben = new List<Color>(ecken.Capacity);
        foreach (var i in reihen)
        {
            var pt = p[i];
            var (vor, nach) = (p[Math.Max(0, i - 1)].Ort, p[Math.Min(p.Count - 1, i + 1)].Ort);
            var richtung = new Vector2((float)(nach.X - vor.X), (float)(nach.Z - vor.Z)).Normalized();
            if (richtung == Vector2.Zero) richtung = new Vector2((float)pt.RichtungX, (float)pt.RichtungZ);
            var quer = new Vector2(-richtung.Y, richtung.X);
            var halb = (float)pt.Breite / 2;
            var mitte = new Vector2((float)pt.Ort.X, (float)pt.Ort.Z);
            var strom = richtung * (float)lauf.Stroemung;
            var hoehe = Mathf.Max((float)pt.Spiegel, Wasserdeckel(mitte, richtung, halb));
            foreach (var seite in new[] { -1f, 1f })
            {
                var o = mitte + quer * halb * FlussRandFaktor * seite;
                ecken.Add(new Vector3(o.X, hoehe, o.Y));
                uv.Add(new Vector2(seite * FlussRandFaktor, (float)pt.Stelle));
                uv2.Add(strom);
                farben.Add(new Color((float)pt.Tiefe, halb, 0, 1));
            }
        }
        var indizes = new List<int>((reihen.Count - 1) * 6);
        for (var k = 0; k + 1 < reihen.Count; k++)
        {
            int a = 2 * k, b = a + 1, c = a + 2, d = a + 3;
            // Vorderseite nach oben: je nach Lage von „links“ ist die Reihenfolge so oder umgekehrt – cull_disabled im Shader macht beides sichtbar
            indizes.AddRange(new[] { a, c, b, b, c, d });
        }
        return NetzAusFeldern(ecken, uv, uv2, farben, indizes);
    }

    /// <summary>
    /// Wie hoch das Band an dieser Stelle mindestens liegen muss, damit der gezeichnete Boden nicht hindurchsticht: <see cref="WasserUeberBoden"/> über dem höchsten
    /// Punkt des Bodens (in einer Furt: des Weges, <see cref="FurtBoden"/>) quer bis drei Viertel der halben Breite – so weit ist das Wasser deckend, weiter außen läuft es
    /// durchsichtig aus – und längs bis zur nächsten Reihe. Der Boden ist nur alle 2 m bekannt: Das Bett eines schmalen oder schräg zum Raster laufenden Baches ist
    /// im Netz eine Kette einzelner Gruben, und ein Spiegel darin zerfiele in Flecken. Wo das Raster das Bett zeigt (ein breiter Fluss), liegt der Boden tiefer als der
    /// Spiegel, und es bleibt beim Spiegel.
    /// </summary>
    private float Wasserdeckel(Vector2 mitte, Vector2 richtung, float halb)
    {
        if (!BodenBewegt) return float.MinValue;
        var quer = new Vector2(-richtung.Y, richtung.X);
        var hoechst = float.MinValue;
        foreach (var laengs in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            foreach (var seite in new[] { -0.75f, -0.4f, 0f, 0.4f, 0.75f })
            {
                var p = mitte + richtung * (laengs * FlussSchritt / 2f) + quer * (seite * halb);
                var boden = FurtBoden(p.X, p.Y) is { } weg ? weg + WegHoehe : BodenHoehe(p.X, p.Y);
                hoechst = Mathf.Max(hoechst, boden);
            }
        return hoechst + WasserUeberBoden;
    }

    private static ArrayMesh NetzAusFeldern(List<Vector3> ecken, List<Vector2> uv, List<Vector2> uv2, List<Color> farben, List<int>? indizes)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = ecken.Select(_ => Vector3.Up).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV2] = uv2.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = farben.ToArray();
        if (indizes is not null) arrays[(int)Mesh.ArrayType.Index] = indizes.ToArray();
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return netz;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Seen und Teiche
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Fläche eines Sees: Dreiecke genau über den Zellen des Bodens (so klafft am Ufer kein Spalt), alle auf der Höhe des Wasserstands. Die Tiefe jeder
    /// Ecke (COLOR r) kommt aus dem Boden, das Wasser läuft dort durchsichtig aus, wo sie gegen 0 geht.
    /// </summary>
    private ArrayMesh? SeeNetz(Wassersee see)
    {
        if (see.Umriss.Count < 3 || _gelaende is null) return null;
        var umriss = see.Umriss.Select(o => new Vector2((float)o.X, (float)o.Z)).ToArray();
        var ecken = RasterDreiecke(umriss, 0, (float)see.Wasserstand);
        if (ecken.Count == 0) return null;
        var uv = ecken.Select(e => new Vector2(e.X, e.Z)).ToList();
        var uv2 = ecken.Select(_ => Vector2.Zero).ToList();
        var tiefen = new Dictionary<(float, float), float>();
        var farben = ecken.Select(e =>
        {
            if (!tiefen.TryGetValue((e.X, e.Z), out var t)) tiefen[(e.X, e.Z)] = t = (float)_gelaende.Wassertiefe(e.X, e.Z);
            return new Color(t, 0, 0, 0);
        }).ToList();
        return NetzAusFeldern(ecken, uv, uv2, farben, null);
    }

    /// <summary>
    /// Das Wasser eines Hafenbeckens: wie ein See genau über den Zellen des Bodens, auf dem Spiegel des Gewässers vor der Kaimauer. Wo der Fluss beginnt, ist die Tiefe
    /// der Ecke 0 – dort läuft das Becken durchsichtig aus, und das Band des Flusses übernimmt (sonst lägen zwei Wasserflächen übereinander).
    /// </summary>
    private ArrayMesh? BeckenNetz(Hafenbecken becken, Wasserform form)
    {
        if (_gelaende is null) return null;
        var spiegel = (float)becken.Spiegel;
        var ecken = RasterDreiecke(becken.Umriss.Select(o => new Vector2((float)o.X, (float)o.Z)).ToArray(), 0, spiegel);
        if (ecken.Count == 0) return null;
        var uv = ecken.Select(e => new Vector2(e.X, e.Z)).ToList();
        var uv2 = ecken.Select(_ => Vector2.Zero).ToList();
        var farben = ecken.Select(e => new Color(form.IstWasser(e.X, e.Z) ? 0 : Mathf.Max(0, spiegel - BodenHoehe(e.X, e.Z)), 0, 0, 0)).ToList();
        return NetzAusFeldern(ecken, uv, uv2, farben, null);
    }
}
