using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Der Rand der Karte – ein Teil der <c>partial class Stadtansicht</c> (Kartenwerkstatt, Schritt 5a). Jede Karte hat ein Spielfeld und einen
/// Randstreifen darum (<see cref="Kartengrenzen"/> des Kerns); hier steht, was die Darstellung daraus macht:
/// <list type="bullet">
/// <item><b>Boden und Dunst</b> (<see cref="BodenBauen"/>): Die Wiese liegt unter der ganzen Karte und noch <see cref="RandZugabe"/> Meter darüber
/// hinaus. Darüber liegt ein flacher Ring (randdunst.gdshader) in der Farbe des Himmels: innen durchsichtig, nach außen dichter, am Ende ganz
/// zu. So läuft die Randlandschaft in den Dunst aus, und selbst über die Außenkante hinaus sieht die Kamera weder Himmel noch eine harte Kante.</item>
/// <item><b>Kamera</b> (<see cref="KameraGrenzenAnwenden"/>, <see cref="KameraGebremst"/>, <see cref="KameraGroessterZoom"/>): Der Blickpunkt bleibt im
/// Spielfeld und wird am Rand weich gebremst (je weiter hinaus, desto zäher, dann federt er zurück). Herauszoomen geht nur so weit, dass das
/// Bild auch am Rand des Spielfelds im Randstreifen bleibt (<see cref="Kartengrenzen.GroessterZoom"/>, mit dem Dunst als Reserve).
/// In der Kartenwerkstatt darf der Blickpunkt bis an die Außenkante (zum Malen im Rand), und der Dunst beginnt erst dort.</item>
/// <item><b>Felder</b> (<see cref="FeldNetzeBauen"/>): Die Äcker rechnet der Kern (<see cref="FeldVerteilung"/>); hier wird je Frucht ein Netz aus
/// flachen Vierecken gebaut, Furchen und Bewuchs zeichnet acker.gdshader. Im Spiel liegen alle Äcker in einem Knoten, in der Werkstatt je Fläche einer.</item>
/// <item><b>Kartenrand als Linie</b> (nur Werkstatt, <see cref="KartenrandZeichnen"/>): die Grenze des Spielfelds (gold) und die Außenkante (blau).</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>So weit (Meter) liegt Boden und Dunst über die Außenkante der Karte hinaus: Die Kamera am Rand sieht dahinter nur noch Dunst.</summary>
    private const float RandZugabe = 300f;

    /// <summary>
    /// Im Spiel beginnt der Dunst nach diesem Anteil der Randbreite (vom Spielfeld aus gemessen) und deckt den Boden bei diesem Anteil ganz zu.
    /// Dahinter (die letzten 12 % des Randes) ist die Reserve: Die Kamera zeigt das nie (<see cref="Kartengrenzen.GroessterZoom"/>).
    /// </summary>
    private const float DunstAnfang = 0.35f, DunstVoll = 0.88f;

    /// <summary>In der Kartenwerkstatt wächst der Dunst jenseits der Außenkante auf dieser Breite (Meter) zu.</summary>
    private const float WerkstattDunstBreite = 70f;

    /// <summary>Höhe des Dunstrings über dem Boden: über Wegen (7 cm), Feldern und der Wiese.</summary>
    private const float DunstRingHoehe = 0.12f;

    /// <summary>Größe, Lage und Randbreite der Karte, die gerade gezeichnet wird (null: keine – dann gilt der Rahmen der Gebäude).</summary>
    private Kartengrenzen? _grenzen;

    private Node3D? _randKnoten;

    /// <summary>Die Shader von Dunst und Äckern, einmal geladen und über Szenenwechsel gehalten (siehe <see cref="_modellSzenen"/>).</summary>
    private static Shader? _dunstShader, _ackerShader;
    private ShaderMaterial? _dunstRing;
    private Color _dunstFarbe = new(-1, 0, 0);

    // ═════════════════════════════════════════════════════════════════════════
    //  Boden, Dunst, Kartenrand
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Legt den Boden und den Dunstring um die Karte (neu, wenn sich die Größe ändert – die Werkstatt ruft es dann wieder). In der Werkstatt
    /// kommt der Kartenrand als Linie dazu.
    /// </summary>
    private void BodenBauen(Kartengrenzen g)
    {
        _grenzen = g;
        if (_randKnoten is not null) KnotenEntfernen(_randKnoten);
        _randKnoten = new Node3D { Name = "Boden" };
        AddChild(_randKnoten);

        var aussen = g.Aussen.Vergroessert(RandZugabe);
        GelaendeBauen(_randKnoten, g.Aussen, RandZugabe); // Stadtansicht.Gelaende.cs: die eine Ebene der flachen Karte oder die Kacheln des Geländes
        DunstBauen(g, aussen);
        if (_entwurf is not null) KartenrandZeichnen(g);
    }

    /// <summary>Der Ring um die Karte: außen die Wiese bis <paramref name="aussen"/>, innen das Rechteck, in dem der Dunst noch nicht beginnt.</summary>
    private void DunstBauen(Kartengrenzen g, Kartenrechteck aussen)
    {
        var (innen, breite) = _entwurf is null
            ? (g.Spielfeld.Vergroessert(DunstAnfang * g.Randbreite), (DunstVoll - DunstAnfang) * g.Randbreite)
            : (g.Aussen, WerkstattDunstBreite);
        var ecken = new List<Vector3>();
        // Im Spiel auf hügeligem Boden folgt der Ring dem Gelände (Stadtansicht.Gelaende.cs), sonst läge er in den Hügeln des Randes unter dem Boden
        var amBoden = BodenBewegt && _entwurf is null;
        void Flach(double x0, double z0, double x1, double z1)
        {
            if (x1 - x0 < 0.01 || z1 - z0 < 0.01) return;
            var (a, b, c, d) = (new Vector2((float)x0, (float)z0), new Vector2((float)x1, (float)z0), new Vector2((float)x1, (float)z1), new Vector2((float)x0, (float)z1));
            DreieckFlach(ecken, a, b, c, DunstRingHoehe);
            DreieckFlach(ecken, a, c, d, DunstRingHoehe);
        }
        void Rechteck(double x0, double z0, double x1, double z1)
        {
            if (!amBoden)
            {
                Flach(x0, z0, x1, z1);
                return;
            }
            // der Teil in der Karte folgt dem Boden, der Rest darum ist flach
            var k = g.Aussen;
            var (ix0, iz0, ix1, iz1) = (Math.Max(x0, k.X0), Math.Max(z0, k.Z0), Math.Min(x1, k.X1), Math.Min(z1, k.Z1));
            if (ix0 >= ix1 || iz0 >= iz1)
            {
                Flach(x0, z0, x1, z1);
                return;
            }
            Flach(x0, z0, x1, iz0);
            Flach(x0, iz1, x1, z1);
            Flach(x0, iz0, ix0, iz1);
            Flach(ix1, iz0, x1, iz1);
            DunstAmBoden(ecken, ix0, iz0, ix1, iz1);
        }
        Rechteck(aussen.X0, aussen.Z0, aussen.X1, innen.Z0);   // Norden, über die ganze Breite
        Rechteck(aussen.X0, innen.Z1, aussen.X1, aussen.Z1);   // Süden
        Rechteck(aussen.X0, innen.Z0, innen.X0, innen.Z1);     // Westen
        Rechteck(innen.X1, innen.Z0, aussen.X1, innen.Z1);     // Osten

        var netz = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        _dunstShader ??= GD.Load<Shader>("res://assets/shader/randdunst.gdshader");
        _dunstRing = new ShaderMaterial { Shader = _dunstShader, ResourceName = "Randdunst" };
        _dunstRing.SetShaderParameter("innen", new Vector4((float)innen.X0, (float)innen.Z0, (float)innen.X1, (float)innen.Z1));
        _dunstRing.SetShaderParameter("breite", breite);
        _dunstFarbe = new Color(-1, 0, 0); // DunstFarbeNachfuehren setzt sie im nächsten Bild
        DunstFarbeNachfuehren();
        _randKnoten!.AddChild(new MeshInstance3D { Name = "Randdunst", Mesh = netz, MaterialOverride = _dunstRing, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Größe der Zellen, in denen der Dunstring dem Gelände folgt (Meter).</summary>
    private const double DunstZelle = 8;

    /// <summary>
    /// Ein Teil des Dunstrings über hügeligem Boden: Zellen von <see cref="DunstZelle"/> Metern, jede Ecke liegt <see cref="DunstRingHoehe"/> über dem
    /// höchsten Punkt des Bodens in ihrer Umgebung (die vier Zellen um sie) – der Ring schwebt an Hängen ein wenig, schneidet aber nie in den Boden.
    /// </summary>
    private void DunstAmBoden(List<Vector3> ecken, double x0, double z0, double x1, double z1)
    {
        var nx = Math.Max(1, (int)Math.Ceiling((x1 - x0) / DunstZelle));
        var nz = Math.Max(1, (int)Math.Ceiling((z1 - z0) / DunstZelle));
        var (sx, sz) = ((x1 - x0) / nx, (z1 - z0) / nz);
        var hoehe = new float[(nx + 1) * (nz + 1)];
        for (var j = 0; j <= nz; j++)
            for (var i = 0; i <= nx; i++)
            {
                var (cx, cz) = (x0 + i * sx, z0 + j * sz);
                var spitze = float.MinValue;
                // Proben im Abstand der Rasterpunkte (2 m) über die vier Zellen um die Ecke: Das Netz hat seine Gipfel nur an den Rasterpunkten
                for (var pz = cz - sz; pz <= cz + sz + 1e-6; pz += Hoehenraster.Abstand)
                    for (var px = cx - sx; px <= cx + sx + 1e-6; px += Hoehenraster.Abstand)
                        spitze = Mathf.Max(spitze, BodenHoehe((float)Math.Clamp(px, x0, x1), (float)Math.Clamp(pz, z0, z1)));
                hoehe[j * (nx + 1) + i] = spitze + DunstRingHoehe;
            }
        Vector3 V(int i, int j) => new((float)(x0 + i * sx), hoehe[j * (nx + 1) + i], (float)(z0 + j * sz));
        for (var j = 0; j < nz; j++)
            for (var i = 0; i < nx; i++)
            {
                var (a, b, c, d) = (V(i, j), V(i + 1, j), V(i + 1, j + 1), V(i, j + 1));
                ecken.AddRange(new[] { a, b, c, a, c, d }); // gegen den Uhrzeigersinn in (x, z): Vorderseite nach oben
            }
    }

    /// <summary>Jedes Bild: Der Dunst hat die Farbe des Himmels zur Tageszeit (die Beleuchtung stellt <c>BackgroundColor</c> ein).</summary>
    private void DunstFarbeNachfuehren()
    {
        if (_dunstRing is null) return;
        var farbe = _umgebung.Environment.BackgroundColor;
        if (farbe == _dunstFarbe) return;
        _dunstFarbe = farbe;
        _dunstRing.SetShaderParameter("farbe", farbe);
    }

    private bool _kartenrandSichtbar = true;

    /// <summary>Zeigt die Werkstatt die Linien des Kartenrands (Spielfeld gold, Außenkante blau)? Bleibt über eine neue Größe erhalten.</summary>
    public bool WerkstattKartenrandSichtbar
    {
        get => _kartenrandSichtbar;
        set
        {
            _kartenrandSichtbar = value;
            if (_randKnoten?.GetNodeOrNull<Node3D>("Kartenrand") is { } linien) linien.Visible = value;
        }
    }

    private static readonly Color Spielfeldlinie = new(0.98f, 0.82f, 0.30f), Aussenlinie = new(0.45f, 0.75f, 1f);

    /// <summary>Die Grenze des Spielfelds (gold) und die Außenkante des Randes (blau) als Linie am Boden – nur in der Kartenwerkstatt.</summary>
    private void KartenrandZeichnen(Kartengrenzen g)
    {
        var rand = new Node3D { Name = "Kartenrand", Visible = _kartenrandSichtbar };
        _randKnoten!.AddChild(rand);
        void Linie(Kartenrechteck r, Color farbe, float halb, float hoehe)
        {
            var punkte = new List<Vector2>
            {
                new((float)r.X0, (float)r.Z0), new((float)r.X1, (float)r.Z0), new((float)r.X1, (float)r.Z1), new((float)r.X0, (float)r.Z1), new((float)r.X0, (float)r.Z0),
            };
            rand.AddChild(DurchsichtigesNetz(BandDreiecke(punkte, halb, hoehe), farbe, 0.9f));
        }
        Linie(g.Spielfeld, Spielfeldlinie, 0.4f, 0.16f);
        Linie(g.Aussen, Aussenlinie, 0.55f, 0.15f);
        var nw = g.Spielfeld;
        var spielfeld = Beschriftung($"Spielfeld {Zahl(g.Breite)} × {Zahl(g.Tiefe)} m", new Vector3((float)nw.X0 + 30, 6, (float)nw.Z0 + 10), 44, rand);
        spielfeld.Modulate = Spielfeldlinie.Lightened(0.3f);
        var aussen = g.Aussen;
        var kante = Beschriftung($"Kartenrand · {Zahl(g.Randbreite)} m Randstreifen", new Vector3((float)aussen.X0 + 40, 6, (float)aussen.Z0 + 10), 44, rand);
        kante.Modulate = Aussenlinie.Lightened(0.3f);

        static string Zahl(double m) => Kartenschreiber.Zahl(m);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kamera: weiche Grenze und größter Zoom
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Das Rechteck, in dem der Blickpunkt bleibt: im Spiel das Spielfeld, in der Kartenwerkstatt die ganze Karte (null: keine Karte).</summary>
    private Kartenrechteck? KameraRechteck() => _grenzen is null ? null : _entwurf is not null ? _grenzen.Aussen : _grenzen.Spielfeld;

    /// <summary>Wie viele Meter der Blickpunkt höchstens über die Grenze hinaus darf (und wie weich er dort bremst): ein Bruchteil der Bildhöhe, höchstens ein Zehntel des Randes.</summary>
    private float KameraWeichheit() => Mathf.Clamp(_kamera.Size * 0.12f, 4f, Mathf.Max(4f, (float)(_grenzen?.Randbreite ?? 100) * 0.1f));

    /// <summary>
    /// Der größte Zoom (sichtbare Bildhöhe in Metern): Im Spiel so, dass das Bild mit dem Blickpunkt auf dem Rand des Spielfelds noch im Randstreifen
    /// bleibt (<see cref="Kartengrenzen.GroessterZoom"/>, mit dem Dunst der letzten Meter als Reserve), nie mehr als <see cref="KameraMaxZoom"/>.
    /// In der Kartenwerkstatt und ohne Karte gilt nur <see cref="KameraMaxZoom"/> (dort deckt der Dunst jenseits der Außenkante alles zu).
    /// </summary>
    private float KameraGroessterZoom()
    {
        if (_grenzen is not { } g || _entwurf is not null) return KameraMaxZoom;
        var bild = GetViewport().GetVisibleRect().Size;
        var seiten = bild.Y > 0 ? bild.X / bild.Y : 16f / 9f;
        var reserve = (1 - DunstVoll) * g.Randbreite;
        return Mathf.Clamp((float)g.GroessterZoom(KameraNeigung, seiten, null, reserve), KameraMinZoom, KameraMaxZoom);
    }

    /// <summary>
    /// Ein Schritt des Blickpunkts mit weicher Grenze: Innerhalb des Rechtecks frei; darüber hinaus wird jeder Meter zäher – der Überstand
    /// nähert sich dem Höchstwert <see cref="KameraWeichheit"/> wie eine abklingende Kurve, ohne je anzustoßen. Zurück nach innen ist frei.
    /// </summary>
    private Vector3 KameraGebremst(Vector3 alt, Vector3 neu)
    {
        if (KameraRechteck() is not { } r) return neu;
        var d = KameraWeichheit();
        return new Vector3(Gebremst(alt.X, neu.X, (float)r.X0, (float)r.X1, d), 0, Gebremst(alt.Z, neu.Z, (float)r.Z0, (float)r.Z1, d));

        static float Gebremst(float a, float n, float lo, float hi, float d)
        {
            var schritt = n - a;
            if (schritt == 0) return a;
            // nach hi (schritt > 0) oder nach lo (gespiegelt): u = wie weit hinter der Grenze, negativ davor
            var (grenze, u, s, vorzeichen) = schritt > 0 ? (hi, a - hi, schritt, 1f) : (lo, lo - a, -schritt, -1f);
            if (u < 0)
            {
                var frei = Mathf.Min(s, -u);
                u += frei;
                s -= frei;
            }
            if (s > 0)
            {
                var ueber = Mathf.Max(u, 0);
                u = d - (d - ueber) * Mathf.Exp(-s / d);
            }
            return grenze + vorzeichen * u;
        }
    }

    /// <summary>
    /// Jedes Bild: Der Blickpunkt federt in die Grenze zurück, wenn er darüber hinaus steht und nicht gerade geschoben oder gefahren wird; der Zoom
    /// folgt dem größten Zoom (ändert sich das Seitenverhältnis des Fensters, wird das Bild sanft kleiner).
    /// </summary>
    private void KameraGrenzenAnwenden(double delta)
    {
        if (_innen is not null || _raum is not null || _kamerafahrt?.IsRunning() == true) return;
        var weich = 1 - Mathf.Exp(-(float)delta * 10);
        var groesst = KameraGroessterZoom();
        if (_kamera.Size > groesst + 0.01f) _kamera.Size = Mathf.Lerp(_kamera.Size, groesst, weich);
        else if (_kamera.Size > groesst) _kamera.Size = groesst;
        if (KameraRechteck() is not { } r || _ziehen || KameraTasteGedrueckt()) return;
        var p = _kameraDrehung.Position;
        var ziel = r.Klemmen(p.X, p.Z);
        var (zx, zz) = ((float)ziel.X, (float)ziel.Z);
        if (Mathf.Abs(zx - p.X) < 0.002f && Mathf.Abs(zz - p.Z) < 0.002f)
        {
            if (p.X != zx || p.Z != zz) _kameraDrehung.Position = new Vector3(zx, 0, zz);
            return;
        }
        _kameraDrehung.Position = new Vector3(Mathf.Lerp(p.X, zx, weich * 0.5f), 0, Mathf.Lerp(p.Z, zz, weich * 0.5f));
    }

    private static bool KameraTasteGedrueckt() =>
        Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.D)
        || Input.IsKeyPressed(Key.Up) || Input.IsKeyPressed(Key.Down) || Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.Right);

    // ═════════════════════════════════════════════════════════════════════════
    //  Felder
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Aussehen einer Frucht: Farbe im Sommer und wie dicht sie auf den Kämmen steht (acker.gdshader).</summary>
    private static readonly Dictionary<string, (Color Farbe, float Bewuchs)> FruchtLook = new()
    {
        ["weizen"] = (new Color(0.74f, 0.62f, 0.32f), 0.88f),
        ["roggen"] = (new Color(0.60f, 0.58f, 0.32f), 0.88f),
        ["gerste"] = (new Color(0.68f, 0.64f, 0.36f), 0.85f),
        ["hafer"] = (new Color(0.52f, 0.58f, 0.30f), 0.82f),
        ["flachs"] = (new Color(0.42f, 0.56f, 0.40f), 0.78f),
        ["brache"] = (new Color(0.36f, 0.38f, 0.22f), 0.32f),
    };

    private static readonly Color AckerErde = new(0.34f, 0.25f, 0.16f);

    /// <summary>Ein Material je Frucht (alle Netze einer Frucht teilen es; die Jahreszeit stellt <see cref="FeldJahreszeit"/> ein).</summary>
    private readonly Dictionary<string, ShaderMaterial> _feldStoffe = new();

    private ShaderMaterial FeldStoff(string frucht)
    {
        if (_feldStoffe.TryGetValue(frucht, out var stoff)) return stoff;
        var (farbe, bewuchs) = FruchtLook.GetValueOrDefault(frucht, FruchtLook["brache"]);
        _ackerShader ??= GD.Load<Shader>("res://assets/shader/acker.gdshader");
        stoff = new ShaderMaterial { Shader = _ackerShader, ResourceName = "Acker " + frucht };
        stoff.SetShaderParameter("farbe", farbe);
        stoff.SetShaderParameter("erde", AckerErde);
        stoff.SetShaderParameter("bewuchs", bewuchs);
        stoff.SetShaderParameter("winter", FeldWinter());
        return _feldStoffe[frucht] = stoff;
    }

    private float FeldWinter() => _jahreszeit switch { Jahreszeit.Winter => 1f, Jahreszeit.Herbst => 0.4f, _ => 0f };

    /// <summary>Die Jahreszeit hat gewechselt: Die Äcker liegen im Winter kahl da, im Herbst abgeerntet (aus <see cref="JahreszeitSetzen"/>).</summary>
    private void FeldJahreszeit()
    {
        foreach (var stoff in _feldStoffe.Values) stoff.SetShaderParameter("winter", FeldWinter());
    }

    /// <summary>Höhe der Äcker über dem Boden: über der Wiese, unter Wegen.</summary>
    private const float FeldHoehe = 0.02f;

    /// <summary>
    /// Auf Gelände folgen die Äcker dem Boden in Zellen bis zu dieser Größe (Meter) und liegen <see cref="FeldHoeheAmHang"/> darüber: Zwischen den Ecken einer
    /// Zelle ist das Netz des Bodens (2-m-Dreiecke) etwas anders gebogen – bei 3 m Zellen bis etwa 5 cm auf unruhigem Boden, sonst schaute er durch die Äcker.
    /// </summary>
    private const float FeldZelle = 3f, FeldHoeheAmHang = 0.1f;

    /// <summary>
    /// Baut die Äcker unter <paramref name="eltern"/>: je Frucht ein Netz aus flachen Vierecken (ein Viereck je Stück, Länge entlang der lokalen
    /// z-Achse) mit dem Material der Frucht. Furchen und Bewuchs sind im Shader; die Vertexdaten tragen den Ort im Stück (UV), seine Drehung (UV2),
    /// einen leichten Farbstich der Variante und die halbe Breite des Stücks (COLOR).
    /// </summary>
    private void FeldNetzeBauen(IReadOnlyList<Feldparzelle> parzellen, Node3D eltern)
    {
        GrasAeckerMelden(eltern, parzellen); // über Äckern kein Grasteppich (Stadtansicht.Gras.cs)
        foreach (var gruppe in parzellen.GroupBy(p => p.Frucht))
        {
            var ecken = new List<Vector3>();
            var uv = new List<Vector2>();
            var uv2 = new List<Vector2>();
            var farben = new List<Color>();
            var indizes = new List<int>();
            foreach (var p in gruppe)
            {
                var (b, l) = ((float)p.Breite / 2, (float)p.Laenge / 2);
                var winkel = Mathf.DegToRad((float)p.Drehung);
                var basis = new Basis(Vector3.Up, winkel);
                // Farbstich aus Ort und Variante: gleiche Karte, gleiche Äcker (kein Zufall je Start)
                var stich = 0.94f + 0.04f * ((p.Variante - 1) % 4) + 0.02f * Mathf.PosMod((float)(p.X * 0.071 - p.Z * 0.053), 1f);
                var farbstich = new Color(stich, stich, stich * (p.Variante % 2 == 0 ? 1.02f : 0.97f), Mathf.Min(1f, b / 16f)); // Alpha: halbe Breite für die Furchen am Rand
                // Auf flachem Boden ein Viereck je Stück; auf Gelände ein Gitter mit Zellen bis FeldZelle Meter, jede Ecke auf dem Boden
                var (nx, nz) = BodenBewegt ? (Mathf.Max(1, Mathf.CeilToInt(2 * b / FeldZelle)), Mathf.Max(1, Mathf.CeilToInt(2 * l / FeldZelle))) : (1, 1);
                var anfang = ecken.Count;
                for (var iz = 0; iz <= nz; iz++)
                    for (var ix = 0; ix <= nx; ix++)
                    {
                        var e = new Vector2(-b + 2 * b * ix / nx, -l + 2 * l * iz / nz);
                        var welt = basis * new Vector3(e.X, 0, e.Y);
                        var (wx, wz) = ((float)p.X + welt.X, (float)p.Z + welt.Z);
                        ecken.Add(new Vector3(wx, BodenHoehe(wx, wz) + (BodenBewegt ? FeldHoeheAmHang : FeldHoehe), wz));
                        uv.Add(e);
                        uv2.Add(new Vector2(winkel, 0));
                        farben.Add(farbstich);
                    }
                // Vorderseite nach oben: in (x, z) gegen den Uhrzeigersinn (wie Dreieck/Vieleck); die Drehung ändert die Richtung nicht, ein Blick auf die erste Zelle genügt
                var a = new Vector2(ecken[anfang].X, ecken[anfang].Z);
                var b1 = new Vector2(ecken[anfang + 1].X, ecken[anfang + 1].Z);
                var c = new Vector2(ecken[anfang + nx + 2].X, ecken[anfang + nx + 2].Z);
                var gegen = (b1 - a).Cross(c - a) >= 0;
                for (var iz = 0; iz < nz; iz++)
                    for (var ix = 0; ix < nx; ix++)
                    {
                        int v00 = anfang + iz * (nx + 1) + ix, v10 = v00 + 1, v01 = v00 + nx + 1, v11 = v01 + 1;
                        if (gegen) indizes.AddRange(new[] { v00, v10, v11, v00, v11, v01 });
                        else indizes.AddRange(new[] { v00, v11, v10, v00, v01, v11 });
                    }
            }
            if (ecken.Count == 0) continue;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = ecken.Select(e => BodenNormale(e.X, e.Z)).ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV2] = uv2.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = farben.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = indizes.ToArray();
            var netz = new ArrayMesh();
            netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            eltern.AddChild(new MeshInstance3D
            {
                Name = "Acker_" + gruppe.Key, Mesh = netz, MaterialOverride = FeldStoff(gruppe.Key), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
    }

    /// <summary>Die Äcker der Karte im Spiel (Kern: <see cref="FeldVerteilung.Alle"/>), alle in einem Knoten.</summary>
    private void FelderAusKarte(Stadtkarte karte)
    {
        var parzellen = FeldVerteilung.Alle(karte, Modellmass);
        if (parzellen.Count == 0) return;
        var felder = new Node3D { Name = "Felder" };
        AddChild(felder);
        FeldNetzeBauen(parzellen, felder);
    }
}
