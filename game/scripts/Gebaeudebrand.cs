using Godot;

namespace Reichsgilde;

/// <summary>Ein Fenster eines Hauses (im Raum des Hauses): Mitte der Scheibe, Richtung nach außen (waagerecht) und Maße in Metern.</summary>
public sealed record FensterAnker(Vector3 Mitte, Vector3 Normale, float Breite, float Hoehe);

/// <summary>Ein Punkt auf dem Dach (im Raum des Hauses): die Stelle auf der Dachfläche, ihre Normale (nach oben außen) und eine Größe der Flamme (1 = Normalfall).</summary>
public sealed record DachAnker(Vector3 Punkt, Vector3 Normale, float Groesse = 1f);

/// <summary>Wie weit ein Brand ist: Flammenstärke (0–1), Glut (0–1), Rauch (0–1), Licht (0–1) und der Name der Phase (Anfang, Vollbrand, Ausklingen, Glut).</summary>
public readonly record struct BrandStand(float Flamme, float Glut, float Rauch, float Licht, string Phase);

/// <summary>
/// Der Ablauf eines Gebäudebrands aus der Zeit seit dem Ausbruch (Spielminuten): <b>Anfang</b> (Rauch quillt, Glut im Dach, die ersten Fenster züngeln),
/// <b>Vollbrand</b> (alle Fenster lodern, das Dach bricht durch), <b>Ausklingen</b> (die Flammen sinken, das Dach zuerst), <b>Glut</b> (nach dem Ende der
/// Flammen: kein Flammenbild mehr, aber Brandflecken und Fenster glühen, schwacher Rauch). Die Zeiten kommen vom Kern (Beginn, Ende der Flammen,
/// Dauer des Schwelens); hier steckt nur die Form der Kurve. Reine Rechnung ohne Godot-Objekte.
/// </summary>
public static class BrandVerlauf
{
    private static float Weich(float x) { x = Mathf.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }

    /// <param name="t">Spielminuten seit dem Ausbruch (mit Bruchteil).</param>
    /// <param name="brennMinuten">So lange schlagen Flammen (Kern: Anschlag 120, Blitz je nach Einschlag).</param>
    /// <param name="glimmMinuten">So lange schwelt es danach.</param>
    public static BrandStand Rechnen(float t, float brennMinuten, float glimmMinuten)
    {
        var b = Mathf.Max(brennMinuten, 12f);
        t = Mathf.Max(t, 0f);
        var anfang = Mathf.Clamp(b * 0.28f, 8f, 30f);        // Ende des Anfangs
        var voll = Mathf.Max(b * 0.45f, anfang + 4f);        // Beginn des Vollbrands
        var klingt = Mathf.Max(b * 0.75f, voll + 4f);        // Beginn des Ausklingens
        if (t < b)
        {
            float flamme;
            if (t < anfang) flamme = 0.38f * Weich(t / anfang);
            else if (t < voll) flamme = Mathf.Lerp(0.38f, 1f, Weich((t - anfang) / (voll - anfang)));
            else if (t < klingt) flamme = 1f;
            else flamme = Mathf.Lerp(1f, 0.12f, Weich((t - klingt) / (b - klingt)));
            flamme *= Weich((b - t) / 2.5f);                   // die letzten Minuten: die Flammen fallen in sich zusammen
            var glut = t < anfang ? 0.55f * Weich(t / anfang) : 0.55f + 0.3f * Weich((t - voll) / Mathf.Max(b - voll, 1f));
            var rauch = t < 4f ? Mathf.Clamp(t / 4f, 0.05f, 1f) : 1f;
            var phase = t < anfang ? "Anfang" : t < klingt ? "Vollbrand" : "Ausklingen";
            return new BrandStand(flamme, glut, rauch, Mathf.Pow(flamme, 0.8f) + 0.1f * glut, phase);
        }
        var u = Mathf.Clamp((t - b) / Mathf.Max(glimmMinuten, 1f), 0f, 1f);
        var rest = 1f - u;
        return new BrandStand(0f, 0.85f * rest * rest * (0.6f + 0.4f * rest), 1f - 0.85f * u, 0.07f * rest, u < 1f ? "Glut" : "Aus");
    }
}

/// <summary>
/// Der Brand eines Hauses (Auftrag „Feuer überall“, 30.10.2026): Flammen lodern <b>aus den Fenstern</b> und <b>aus dem Dach</b>, nicht als Fläche davor
/// oder darüber. Die Stellen kommen als Anker (<see cref="FensterAnker"/>, <see cref="DachAnker"/>) von außen: das Spiel findet sie im Modell
/// (<c>Stadtansicht.Gebaeudebrand.cs</c>), die Testszene setzt sie von Hand.
/// <list type="bullet">
/// <item><b>Fenster</b>: eine wandparallele Fläche mit dem Flipbook „fenster“ (<see cref="FeuerFlipbook.Fenster"/>: die Flamme klebt an der Fassade und
/// schlägt aus der Öffnung nach oben), dahinter die glühende Öffnung (<c>brand_oeffnung.gdshader</c>, Modus 1).</item>
/// <item><b>Dach</b>: je Stelle eine Flamme (Flipbook „dach“, Fuß im Dach) mit einem Brandfleck auf der Dachfläche (Modus 0: Kohle mit glühenden Rissen), Funken
/// und, an der ersten Stelle, dem Rauch und dem Licht.</item>
/// <item><b>Ablauf</b>: <see cref="Setzen"/> bekommt den Stand (<see cref="BrandVerlauf"/>) und lässt jede Stelle bei ihrer Schwelle anspringen und weich
/// wachsen; nach dem Ende der Flammen glühen Öffnungen und Flecken weiter.</item>
/// </list>
/// Alles hängt an diesem Knoten; er gehört ins Modell (geht mit ihm). Keine Grafikstufen: dieselbe Fassung überall.
/// </summary>
public sealed partial class Gebaeudebrand : Node3D
{
    private sealed class Stelle
    {
        public Feuer? Feuer;
        public MeshInstance3D? Flaeche;     // Öffnung (Fenster) oder Brandfleck (Dach)
        public float Schwelle;
        public float Staerke = -1f;
        public bool Fenster;
        public float Glutanteil = 1f;
    }

    private readonly List<Stelle> _stellen = new();
    private ShaderMaterial _fleck = null!, _oeffnung = null!;
    private BrandStand _stand = new(0, 0, 0, 0, "Anfang");

    public BrandStand Stand => _stand;
    public int Fenster => _stellen.Count(s => s.Fenster);
    public int Daecher => _stellen.Count(s => !s.Fenster);

    /// <summary>Alle Feuer des Brands (Fenster und Dach); für Wechsel der Ebene und Messungen.</summary>
    public IEnumerable<Feuer> Feuerknoten => _stellen.Where(s => s.Feuer is not null).Select(s => s.Feuer!);

    private static ShaderMaterial Material(int modus)
    {
        var m = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/brand_oeffnung.gdshader"), RenderPriority = 2 };
        m.SetShaderParameter("modus", modus);
        m.SetShaderParameter("rausch", GD.Load<Texture2D>("res://assets/texturen/feuer/rauschen.png"));
        return m;
    }

    private static uint Hash(int a, int b)
    {
        var h = (uint)a * 2654435761u ^ (uint)b * 2246822519u;
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
        return h;
    }

    /// <summary>Baut den Brand: bis zu die Hälfte der Fenster-Anker, alle Dach-Anker. <paramref name="mitLicht"/>: Dachlicht und Fensterlichter (nur die ersten beiden Fenster).</summary>
    public static Gebaeudebrand Bauen(IReadOnlyList<FensterAnker> fenster, IReadOnlyList<DachAnker> dach, int saat, bool mitLicht = true)
    {
        var brand = new Gebaeudebrand { Name = "Brand" };
        brand._fleck = Material(0);
        brand._oeffnung = Material(1);
        brand._stellen.AddRange(Enumerable.Empty<Stelle>());
        for (var k = 0; k < fenster.Count; k++) brand.FensterBauen(fenster[k], k, saat, mitLicht && k < 2);
        for (var k = 0; k < dach.Count; k++) brand.DachBauen(dach[k], k, saat, mitLicht && k < 3);
        return brand;
    }

    private static Basis WandBasis(Vector3 n)
    {
        var w = new Vector3(n.X, 0f, n.Z);
        if (w.LengthSquared() < 1e-6f) w = Vector3.Back;
        return Basis.LookingAt(-w.Normalized(), Vector3.Up);
    }

    private void FensterBauen(FensterAnker a, int k, int saat, bool licht)
    {
        var basis = WandBasis(a.Normale);
        var phase = (Hash(saat, k * 7 + 1) & 0xFFFF) / 65535f;
        var groesse = Mathf.Clamp(a.Breite / 0.9f, 0.75f, 1.5f);
        var stelle = new Stelle { Fenster = true, Schwelle = 0.02f + 0.06f * k };
        // Die Öffnung: ein glühender Innenraum in der Scheibe (knapp davor), die Flamme schlägt oben und vor der Wand heraus
        var offen = new MeshInstance3D
        {
            Name = "Oeffnung", Mesh = new QuadMesh { Size = new Vector2(a.Breite * 0.92f, a.Hoehe * 0.92f) }, MaterialOverride = _oeffnung,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 2f, Basis = basis, Position = a.Mitte + a.Normale * 0.035f, Visible = false,
        };
        AddChild(offen);
        var feuer = Feuer.Neu(FeuerVorgabe.Fensterflammen, licht, phase, groesse);
        feuer.Ausrichtung = FeuerAusrichtung.Wand;
        feuer.Aussen = true;
        feuer.Basis = basis;
        feuer.Position = a.Mitte + a.Normale * 0.09f - new Vector3(0, a.Hoehe * 0.38f, 0);
        feuer.Scale = Vector3.One * 0.001f;
        AddChild(feuer);
        stelle.Feuer = feuer;
        stelle.Flaeche = offen;
        _stellen.Add(stelle);
    }

    private void DachBauen(DachAnker a, int k, int saat, bool licht)
    {
        var phase = (Hash(saat, k * 13 + 5) & 0xFFFF) / 65535f;
        var n = a.Normale.LengthSquared() > 1e-6f ? a.Normale.Normalized() : Vector3.Up;
        var stelle = new Stelle { Fenster = false, Schwelle = 0.30f + 0.07f * k, Glutanteil = k == 0 ? 1f : 0.75f };
        var art = k == 0 ? FeuerVorgabe.Dachbrand : FeuerVorgabe.Dachherd;
        var feuer = Feuer.Neu(art, licht, phase, a.Groesse);
        feuer.Ausrichtung = FeuerAusrichtung.Hochachse;
        feuer.Aussen = true;
        feuer.Position = a.Punkt - n * 0.12f;
        feuer.Scale = Vector3.One * 0.001f;
        AddChild(feuer);
        stelle.Feuer = feuer;
        // Der Brandfleck liegt der Dachfläche an: ein Quadrat flach (Normale +Y), gedreht auf die Dachnormale
        var fleck = new MeshInstance3D
        {
            Name = "Brandfleck", Mesh = new QuadMesh { Size = new Vector2(1.9f, 1.4f) * a.Groesse * (0.7f + 0.55f * (Hash(saat, k * 3 + 9) & 0xFF) / 255f), Orientation = PlaneMesh.OrientationEnum.Y }, MaterialOverride = _fleck,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 2f, Position = a.Punkt + n * 0.05f, Visible = false,
            Basis = new Basis(new Quaternion(Vector3.Up, n)) * new Basis(Vector3.Up, phase * Mathf.Tau),
        };
        AddChild(fleck);
        stelle.Flaeche = fleck;
        _stellen.Add(stelle);
    }

    private static float Weich(float x) { x = Mathf.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }

    /// <summary>Stellt den Brand auf diesen Stand: Flammen wachsen je Stelle über ihre Schwelle, Glut, Rauch und Licht folgen.</summary>
    public void Setzen(BrandStand s)
    {
        _stand = s;
        foreach (var m in new[] { _fleck, _oeffnung })
        {
            m.SetShaderParameter("zeit", (float)Feuer.Zeit);
            m.SetShaderParameter("staerke", s.Flamme);
            m.SetShaderParameter("glut", s.Glut);
            m.SetShaderParameter("umgebung", Feuer.Umgebung);
        }
        // Kamera: nah (Zoom 25) ist der Rauch dünner (er deckt sonst die Nachbarn), fern (Zoom 100) werden Flammen und Rauch größer, damit der Brand ein Signal bleibt
        var zoom = 40f;
        if (IsInsideTree() && GetViewport()?.GetCamera3D() is { } kam)
            zoom = kam.Projection == Camera3D.ProjectionType.Orthogonal ? kam.Size : kam.GlobalPosition.DistanceTo(GlobalPosition) * 0.5f;
        var nah = Mathf.Lerp(0.45f, 1f, Mathf.Clamp((zoom - 25f) / 55f, 0f, 1f));
        var fern = 1f + 0.8f * Mathf.Clamp((zoom - 40f) / 60f, 0f, 1f);
        var tag = 0.55f + 0.45f * Feuer.Umgebung; // bei Tag mehr Rauch (er ist hell auf hellem Grund sonst kaum zu sehen)
        // Regen: kleinere Flammen, gedämpfte Glut, mehr Rauch und Dampf
        var nass = Mathf.Clamp(Feuer.Regen, 0f, 1f);
        fern *= 1f - 0.3f * nass;
        var fensterNr = 0;
        foreach (var st in _stellen)
        {
            var f = Weich((s.Flamme - st.Schwelle) / (st.Fenster ? 0.12f : 0.2f)); // Fenster springen schnell an: schon im Anfang sieht man Flammen
            var gross = 0.55f + 0.67f * Weich((s.Flamme - 0.45f) / 0.5f); // der Vollbrand ist deutlich größer als der Anfang
            // Die Fläche (Öffnung oder Brandfleck) glimmt schon, bevor die Flamme kommt, und glüht nach dem Feuer weiter
            var glimm = Mathf.Clamp(Mathf.Max(f, s.Glut * st.Glutanteil * (st.Fenster ? 0.8f : 1f) * (1f - 0.35f * nass)), 0f, 1f);
            if (st.Feuer is { } feuer)
            {
                var an = f > 0.02f;
                if (feuer.FlammeAn != an) feuer.FlammeAn = an;
                var skala = Mathf.Max(f * gross * fern, 0.001f);
                if (!Mathf.IsEqualApprox(feuer.Scale.X, skala)) feuer.Scale = Vector3.One * skala;
                feuer.LichtFaktor = s.Licht * (st.Fenster ? 1f : 1f);
                // Rauch: aus dem Fenster dünn, ehe die Flamme da ist; schwächer, wenn nur noch Glut ist
                var rauch = (st.Fenster ? s.Rauch * Mathf.Clamp(0.3f + f, 0f, 1f) * (fensterNr < 2 ? 1f : 0.6f) : s.Rauch * Mathf.Clamp(0.25f + 0.75f * Mathf.Max(f, s.Glut * 0.3f), 0f, 1f)) * nah * tag * (1f + 0.3f * nass);
                if (Mathf.Abs(feuer.RauchStaerke - rauch) > 0.01f) feuer.RauchStaerke = rauch;
            }
            if (st.Flaeche is { } flaeche)
            {
                var sichtbar = glimm > 0.02f;
                if (flaeche.Visible != sichtbar) flaeche.Visible = sichtbar;
                var g = Mathf.Max(glimm, 0.001f);
                if (!Mathf.IsEqualApprox(flaeche.Scale.X, st.Fenster ? 1f : g)) flaeche.Scale = st.Fenster ? Vector3.One : new Vector3(g, 1f, g);
                // Die Öffnung leuchtet nach der Stärke ihrer Stelle (im Shader: staerke = gemeinsame Flamme; hier die eigene über die Deckkraft in der Tiefe der Fläche)
                if (st.Fenster && !Mathf.IsEqualApprox(st.Staerke, glimm))
                {
                    st.Staerke = glimm;
                    flaeche.Scale = new Vector3(1f, 1f, 1f + glimm); // Tiefe: die Stärke dieser Öffnung (der Shader liest das Verhältnis)
                }
            }
            if (st.Fenster) fensterNr++;
        }
    }
}
