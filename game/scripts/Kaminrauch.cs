using Godot;

namespace Reichsgilde;

/// <summary>
/// Was für ein Rauch: Schornsteine dünn und hell (Wohnhaus), mittel (Werkstatt) oder dick und dunkel (Gießerei, Schmiede, Brauerei …); Feuer im Freien:
/// Fackel (dünn, kurz), Feuerkorb, Esse, Gebäudebrand (riesig, schwarz) und Fensterbrand.
/// </summary>
public enum RauchSorte { Wohnhaus, Werkstatt, Schwer, Fackel, Feuerkorb, Esse, Brand, Fensterbrand }

/// <summary>
/// Der Kaminrauch der ganzen Stadt: <b>ein</b> <see cref="MultiMeshInstance3D"/> mit <b>einem</b> Material (<c>kaminrauch.gdshader</c>), also ein Zeichenaufruf
/// für alle Schornsteine. Die Instanz ist ein Schornstein (Ort, Stärke, Menge, Ruß, Saat in der Matrix der Instanz), das Netz besteht aus einem Dutzend Vierecken,
/// den Rauchballen (Vertexfarbe = Nummer und Zufall); der Shader setzt jeden Ballen aus seinem Alter in die Welt (aufsteigen, mit dem Wind abknicken,
/// wachsen, ausdünnen). Es gibt nichts zu bewegen: keine Partikel, kein Zustand.
/// <para>
/// Einer je Szene (die Stadtansicht und die Testszene legen ihn an, <see cref="Aktuell"/>). Ein <see cref="Feuer"/> der Art Kamin meldet sich beim Bau
/// dort an (<see cref="Anmelden"/>) und gibt Stärke und Sorte weiter; ohne <see cref="Aktuell"/> baut es seinen Rauch wie früher selbst. Der Ort folgt dem
/// Anker (alle Viertelsekunde geprüft: Ort, das Haus kann in der Kartenwerkstatt wandern, und Sichtbarkeit, ein Haus zeigt nur seine gewählte Stufe). Die Stufe der Wettereffekte (Niedrig, Mittel, Hoch) wählt Zahl der
/// Ballen und Feinheit des Shaders; sie wird jedes Bild nachgeführt.
/// </para>
/// </summary>
public partial class Kaminrauch : Node3D
{
    /// <summary>Der Rauch dieser Szene (null: keiner; die Kamine bauen dann ihren eigenen).</summary>
    public static Kaminrauch? Aktuell { get; private set; }

    /// <summary>Nur für Messungen und Tests: Stufe festhalten (0 Niedrig, 1 Mittel, 2 Hoch) statt der Einstellung des Spielers; −1 = Einstellung.</summary>
    public static int StufeFest { get; set; } = -1;

    /// <summary>Nur für Tests: Zahl der Ballen je Schornstein festhalten; 0 = je nach Stufe.</summary>
    public static int BallenFest { get; set; }

    /// <summary>Wie viele Ballen je Schornstein auf den Stufen Niedrig, Mittel, Hoch.</summary>
    private static readonly int[] BallenJeStufe = { 9, 12, 16 };

    private sealed class Platz
    {
        public Node3D? Anker;
        public Vector3 Ort;
        public Vector3 Versatz;
        public float Staerke;
        public RauchSorte Sorte;
        public float Saat;
        public bool Frei = true;
        /// <summary>Ist der Anker im Baum sichtbar? Ein Haus zeigt nur die gewählte Stufe und Variante, die anderen Schornsteine (unsichtbare Teile) rauchen nicht.</summary>
        public bool Sichtbar = true;
    }

    private readonly List<Platz> _plaetze = new();
    private readonly Stack<int> _frei = new();
    private MultiMeshInstance3D _instanz = null!;
    private MultiMesh _mm = null!;
    private ShaderMaterial _material = null!;
    private int _kapazitaet;
    private float[] _puffer = Array.Empty<float>();   // 12 Zahlen je Instanz (Matrix, Zeilen), wie Godot sie im MultiMesh erwartet
    private bool _schmutzig;
    private int _stufe = -1;
    private int _ballen;
    private double _ortUhr;
    private static readonly System.Collections.Generic.Dictionary<int, ArrayMesh> _netze = new();

    public override void _EnterTree()
    {
        Name = "Kaminrauch";
        TopLevel = true;   // die Matrizen der Instanzen sind Weltwerte, die Lage der Eltern darf sie nicht ändern
    }

    public override void _ExitTree()
    {
        if (Aktuell == this) Aktuell = null;
    }

    public override void _Ready()
    {
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/kaminrauch.gdshader") };
        _material.SetShaderParameter("rausch", GD.Load<Texture2D>("res://assets/texturen/feuer/rauschen.png"));
        Feuer.Zeitgeber(_material);
        _instanz = new MultiMeshInstance3D
        {
            Name = "Rauchballen",
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Layers = Feuer.Ebene,
            // Der Shader setzt die Ballen selbst (bis über zehn Meter vom Schornstein, im Sturm mehr); Godot kennt ihre Orte nicht
            CustomAabb = new Aabb(new Vector3(-4000, -100, -4000), new Vector3(8000, 400, 8000)),
        };
        AddChild(_instanz);
        Aufbauen(64, BallenWahl(AktuelleStufe()));
        Aktuell = this;   // erst jetzt: Anmelden braucht das fertige MultiMesh
    }

    /// <summary>Für das Protokoll der Testszene: angemeldete Schornsteine, Stufe, Ballen je Schornstein, Platz.</summary>
    public string Zustand() => $"kamine={_plaetze.Count(p => !p.Frei)} stufe={_stufe} ballen={_ballen} platz={_kapazitaet} ebene={_instanz.Layers} sichtbar={_instanz.IsVisibleInTree()} " +
        $"puffer0={string.Join(',', _puffer.Take(12).Select(f => f.ToString("0.##")))} mm={_mm.InstanceCount}/{_mm.Buffer.Length}";

    /// <summary>Stellt die Sichtebene um (Pixel-Look, siehe <see cref="Feuer.EbeneSetzen"/>).</summary>
    public void EbeneAnwenden()
    {
        if (_instanz is not null) _instanz.Layers = Feuer.Ebene;
    }

    public override void _Process(double delta)
    {
        StufeNachfuehren();
        if (_schmutzig)
        {
            _mm.Buffer = _puffer;   // alle Schornsteine auf einmal (eine Übergabe statt einer je Instanz und Änderung)
            _schmutzig = false;
        }
        _ortUhr += delta;
        if (_ortUhr < 0.25) return;
        _ortUhr = 0;
        for (var i = 0; i < _plaetze.Count; i++)
        {
            var p = _plaetze[i];
            if (p.Frei || p.Anker is null) continue;
            if (!IsInstanceValid(p.Anker)) { Freigeben(i); continue; }
            var ort = p.Anker.GlobalPosition + p.Versatz;
            var sichtbar = p.Anker.IsVisibleInTree();
            if (ort.DistanceSquaredTo(p.Ort) < 1e-6f && sichtbar == p.Sichtbar) continue;
            p.Ort = ort;
            p.Sichtbar = sichtbar;
            Schreiben(i);
        }
    }

    private static int AktuelleStufe() => StufeFest >= 0 ? Math.Clamp(StufeFest, 0, 2) : (int)Fenster.AktuelleWettereffekte;

    private static int BallenWahl(int stufe) => BallenFest > 0 ? BallenFest : BallenJeStufe[stufe];

    private void StufeNachfuehren()
    {
        var stufe = AktuelleStufe();
        var ballen = BallenWahl(stufe);
        if (stufe == _stufe && ballen == _ballen) return;
        Aufbauen(_kapazitaet, ballen);
    }

    /// <summary>
    /// Baut das MultiMesh <b>fertig</b> (Netz, Zahl, alle Instanzen) und hängt es erst dann an die Instanz: Ein schon angehängtes MultiMesh, dem man Netz und
    /// Zahl nachträglich setzt, zeichnete in Godot 4.7.2 nichts (belegt in der Testszene). Darum auch bei jeder Änderung (Stufe, mehr Platz) ein neues.
    /// </summary>
    private void Aufbauen(int kapazitaet, int ballen)
    {
        _stufe = AktuelleStufe();
        _ballen = ballen;
        _material.SetShaderParameter("stufe", _stufe);
        _kapazitaet = kapazitaet;
        _mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = Netz(ballen), InstanceCount = kapazitaet };
        _puffer = new float[kapazitaet * 12];
        for (var i = 0; i < kapazitaet; i++) Schreiben(i);
        _mm.Buffer = _puffer;
        _schmutzig = false;
        _instanz.Multimesh = _mm;
    }

    /// <summary>Das Netz mit <paramref name="n"/> Vierecken: Ecke (−1…1) als Ort, Vertexfarbe = (Nummer, drei Zufallszahlen), je Zahl einmal gebaut.</summary>
    private static ArrayMesh Netz(int n)
    {
        if (_netze.TryGetValue(n, out var vorhanden)) return vorhanden;
        var orte = new Vector3[n * 4];
        var farben = new Color[n * 4];
        var uv = new Vector2[n * 4];
        var index = new int[n * 6];
        var zufall = new RandomNumberGenerator { Seed = 7919UL + (ulong)n };
        for (var i = 0; i < n; i++)
        {
            // Die Nummer bekommt einen kleinen Zufallsversatz, damit die Ballen nicht wie Perlen im gleichen Abstand sitzen
            var nr = (i + 0.5f + (zufall.Randf() - 0.5f) * 0.5f) / n;
            var farbe = new Color(Mathf.Clamp(nr, 0f, 1f), zufall.Randf(), zufall.Randf(), zufall.Randf());
            for (var e = 0; e < 4; e++)
            {
                var x = (e & 1) == 0 ? -1f : 1f;
                var y = (e & 2) == 0 ? -1f : 1f;
                orte[i * 4 + e] = new Vector3(x, y, 0f);
                farben[i * 4 + e] = farbe;
                uv[i * 4 + e] = new Vector2((x + 1f) * 0.5f, (1f - y) * 0.5f);
            }
            int[] dreiecke = { 0, 1, 2, 1, 3, 2 };
            for (var k = 0; k < 6; k++) index[i * 6 + k] = i * 4 + dreiecke[k];
        }
        var feld = new Godot.Collections.Array();
        feld.Resize((int)Mesh.ArrayType.Max);
        feld[(int)Mesh.ArrayType.Vertex] = orte;
        feld[(int)Mesh.ArrayType.Color] = farben;
        feld[(int)Mesh.ArrayType.TexUV] = uv;
        feld[(int)Mesh.ArrayType.Index] = index;
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, feld);
        return _netze[n] = netz;
    }

    /// <summary>
    /// Meldet einen Schornstein an (am <paramref name="anker"/> beginnt der Rauch); die Stärke ist zunächst 0. Gibt die Nummer zurück, mit der er sich
    /// später abmeldet und Stärke und Sorte setzt.
    /// </summary>
    public int Anmelden(Node3D anker, RauchSorte sorte, float saat, Vector3 versatz = default)
    {
        int nr;
        if (_frei.Count > 0) nr = _frei.Pop();
        else
        {
            nr = _plaetze.Count;
            _plaetze.Add(new Platz());
            if (_plaetze.Count > _kapazitaet) Aufbauen(_kapazitaet * 2, _ballen);
        }
        var p = _plaetze[nr];
        p.Frei = false;
        p.Anker = anker;
        p.Versatz = versatz;
        p.Ort = anker.IsInsideTree() ? anker.GlobalPosition + versatz : Vector3.Zero;
        p.Sichtbar = !anker.IsInsideTree() || anker.IsVisibleInTree();
        p.Staerke = 0f;
        p.Sorte = sorte;
        p.Saat = Mathf.Clamp(saat, 0f, 0.999f);
        Schreiben(nr);
        return nr;
    }

    public void Freigeben(int nr)
    {
        if (nr < 0 || nr >= _plaetze.Count || _plaetze[nr].Frei) return;
        var p = _plaetze[nr];
        p.Frei = true;
        p.Anker = null;
        p.Staerke = 0f;
        _frei.Push(nr);
        Schreiben(nr);
    }

    public void StaerkeSetzen(int nr, float staerke)
    {
        if (nr < 0 || nr >= _plaetze.Count || _plaetze[nr].Frei) return;
        var p = _plaetze[nr];
        staerke = Mathf.Clamp(staerke, 0f, 1f);
        if (Mathf.IsEqualApprox(p.Staerke, staerke)) return;
        p.Staerke = staerke;
        Schreiben(nr);
    }

    public void SorteSetzen(int nr, RauchSorte sorte)
    {
        if (nr < 0 || nr >= _plaetze.Count || _plaetze[nr].Frei || _plaetze[nr].Sorte == sorte) return;
        _plaetze[nr].Sorte = sorte;
        Schreiben(nr);
    }

    /// <summary>
    /// Die Werte einer Sorte: Menge (dünn … dick: Deckung, Größe, Auftrieb), Ruß (hell … dunkel), Größe (Faktor auf Ballen, Höhe und Lebensdauer),
    /// Glut nachts und bei Tag (rötlicher Schein am Fuß des Rauchs).
    /// </summary>
    private static (float Menge, float Russ, float Groesse, float GlutNacht, float GlutTag) Werte(RauchSorte s) => s switch
    {
        RauchSorte.Wohnhaus => (0.25f, 0.12f, 1f, 0.5f, 0f),
        RauchSorte.Werkstatt => (0.55f, 0.20f, 1f, 0.7f, 0f),
        RauchSorte.Schwer => (1.00f, 0.55f, 1f, 1.0f, 0f),
        RauchSorte.Fackel => (0.30f, 0.60f, 0.45f, 0.6f, 0.05f),
        RauchSorte.Feuerkorb => (0.50f, 0.50f, 0.80f, 0.8f, 0.2f),
        RauchSorte.Esse => (0.45f, 0.45f, 0.70f, 0.8f, 0.2f),
        RauchSorte.Brand => (1.00f, 0.85f, 3.00f, 1.0f, 0.5f),
        RauchSorte.Fensterbrand => (0.80f, 0.80f, 1.50f, 1.0f, 0.4f),
        _ => (0.55f, 0.20f, 1f, 0.7f, 0f),
    };

    private void Schreiben(int nr)
    {
        if (nr >= _kapazitaet) return;
        var o = nr * 12;
        Array.Clear(_puffer, o, 12);
        _schmutzig = true;
        if (nr >= _plaetze.Count || _plaetze[nr].Frei) return;   // Matrix aus Nullen: Stärke 0, der Shader zeichnet nichts
        // Die Daten des Schornsteins stecken in der Matrix (der Shader liest sie dort, siehe kaminrauch.gdshader): Stärke x.x, Saat x.y, Größe x.z, Menge y.y, Glut nachts y.z, Glut bei Tag z.y, Ruß z.z (Spalte.Zeile der Matrix),
        // dazu der Ort. Godot erwartet die Zeilen: (x.x, y.x, z.x, Ort.x), (x.y, y.y, z.y, Ort.y), (x.z, y.z, z.z, Ort.z)
        var p = _plaetze[nr];
        var (menge, russ, groesse, glutNacht, glutTag) = Werte(p.Sorte);
        _puffer[o + 0] = p.Sichtbar ? p.Staerke : 0f;
        _puffer[o + 8] = groesse;
        _puffer[o + 3] = p.Ort.X;
        _puffer[o + 4] = p.Saat;
        _puffer[o + 5] = menge;
        _puffer[o + 9] = glutNacht;
        _puffer[o + 7] = p.Ort.Y;
        _puffer[o + 6] = glutTag;
        _puffer[o + 10] = russ;
        _puffer[o + 11] = p.Ort.Z;
    }
}
