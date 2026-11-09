using System.Threading.Tasks;
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
        Ladezeit.Teil("Boden: DunstBauen", () => DunstBauen(g, aussen));
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
        _dunstRing.SetShaderParameter("deckung_max", _himmelAn ? RingDeckung : 1f); // mit Himmel (Ego, freie Kamera) schwächer: dahinter läuft das Fernland weiter
        _randKnoten!.AddChild(new MeshInstance3D { Name = "Randdunst", Mesh = netz, MaterialOverride = _dunstRing, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        FernlandBauen(aussen, innen); // Stadtansicht.Himmel.cs: Land hinter dem Ring, nur für den Blick zum Horizont
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
        // Mit der freien Kamera (Stadtansicht.FreieKamera.cs) und ohne Himmel (--himmel=0) sieht man den Hintergrund hinter dem Kartenrand. Godot zeichnet die Hintergrundfarbe
        // dunkler als dieselbe Farbe im Ring (gemessen: Himmel 64,107,156, Ring 137,173,205 – wie einmal zu oft von sRGB nach linear gewandelt);
        // die Spielkamera sieht den Hintergrund nie. Ein so gewandelter Ring geht ohne Kante in den Himmel über.
        if (FreiAktiv && !_himmelAn) farbe = farbe.SrgbToLinear(); // mit dem Himmel (Stadtansicht.Himmel.cs) gilt die Farbe unverändert: Himmel und Ring zeigen dieselbe Dunstfarbe
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

    /// <summary>Ein Material je Frucht (alle Netze einer Frucht teilen es; die Jahreszeit liest acker.gdshader aus <c>jahreszeit_anteile</c>, Stadtansicht.Wetter.cs).</summary>
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
        return _feldStoffe[frucht] = stoff;
    }

    /// <summary>Höhe der Äcker über dem Boden: über der Wiese, unter Wegen.</summary>
    private const float FeldHoehe = 0.02f;

    /// <summary>
    /// Auf Gelände folgen die Äcker dem Boden in Zellen bis zu dieser Größe (Meter) und liegen <see cref="FeldHoeheAmHang"/> darüber: Zwischen den Ecken einer
    /// Zelle ist das Netz des Bodens (2-m-Dreiecke) etwas anders gebogen – bei 3 m Zellen bis etwa 5 cm auf unruhigem Boden, sonst schaute er durch die Äcker.
    /// </summary>
    private const float FeldZelle = 3f, FeldHoeheAmHang = 0.1f;

    /// <summary>Abstand der Furchen (Meter, Halbstreifen: halb so breit; acker.gdshader <c>abstand</c>) und Höhe der Kämme über den Furchen (<c>kamm</c>).</summary>
    private const float FeldFurchenAbstand = 0.8f, FeldKammHoehe = 0.14f;

    /// <summary>Das fertige Netz der Äcker einer Frucht (in <see cref="FeldRechnen"/> angelegt, so kann es in einem Hintergrundfaden entstehen).</summary>
    private sealed record FeldNetzDaten(string Frucht, ArrayMesh Netz);

    /// <summary>
    /// Alles, was die Äcker einer Karte an Rechnung brauchen, fertig für den Hauptfaden: Netze je Frucht, die Stücke für den Grasteppich, die Maske des Feldrains
    /// und die Pflanzen. Entsteht ohne Godot-Objekte in <see cref="FeldRechnen"/> (auch in einem Hintergrundfaden), <see cref="FeldAnlegen"/> legt es als Knoten an.
    /// </summary>
    private sealed class FeldRechnung
    {
        public Feldmaske Maske = null!;
        public readonly List<FeldNetzDaten> Netze = new();
        public readonly List<(Vector2 Mitte, float Drehung, Vector2 Halb)> Sperr = new();
        public IReadOnlyList<Feldparzelle> Parzellen = Array.Empty<Feldparzelle>();
        public SortedDictionary<(string Frucht, int I, int J), MultiMesh> Pflanzen = new();
        /// <summary>Wie lange die Teilschritte gerechnet haben (Name → Mikrosekunden), für <c>--ladezeit</c>.</summary>
        public readonly List<(string Name, long Usec)> Zeiten = new();
    }

    /// <summary>
    /// Baut die Äcker unter <paramref name="eltern"/> (rechnen und anlegen in einem Zug; der Ladebildschirm rechnet vorab im Hintergrund, <see cref="FelderVorrechnen"/>).
    /// </summary>
    private void FeldNetzeBauen(IReadOnlyList<Feldparzelle> parzellen, IReadOnlyList<double[][]> umrisse, Node3D eltern)
    {
        FeldGelaendeVorbereiten(); // das Gelände (in der Werkstatt nach dem Pinseln veraltet) im Hauptfaden aktualisieren, bevor mehrere Fäden es lesen
        FeldAnlegen(FeldRechnen(parzellen, umrisse), eltern);
    }

    /// <summary>
    /// Rechnet die Äcker: je Frucht ein Netz. Die Stücke des Kerns (mit „bis zum Rand“, <see cref="FeldVerteilung.Flaeche"/>) liegen zum Teil über den Rand ihrer
    /// Feldfläche hinaus; jede Zelle (auf Gelände bis <see cref="FeldZelle"/> m, auf flachem Boden das ganze Stück) wird gerade an die <paramref name="umrisse"/> der
    /// Feldflächen zugeschnitten (Geometry2D) und in Dreiecke zerlegt – so läuft der Rand als gerade Linie statt in Treppen aus ganzen Stücken. Furchen und Bewuchs
    /// sind im Shader; die Vertexdaten tragen den Ort im Stück (UV), seine Drehung und den Abstand zum Rand der Fläche in Metern (UV2: der Shader lässt dort einen
    /// Feldrain ins Gras auslaufen), einen leichten Farbstich der Variante und die halbe Breite des Stücks (COLOR). Dazu die Pflanzen (<see cref="FeldPflanzenBauer.Rechnen"/>).
    /// Rechnet nur Zahlen und liest das Gelände (das der Aufrufer vorher einmal im Hauptfaden angefragt haben muss, siehe <see cref="FeldGelaendeVorbereiten"/>): darf in einem Hintergrundfaden laufen.
    /// </summary>
    private FeldRechnung FeldRechnen(IReadOnlyList<Feldparzelle> parzellen, IReadOnlyList<double[][]> umrisse, ParallelOptions? po = null)
    {
        po ??= new ParallelOptions();
        var r = new FeldRechnung { Parzellen = parzellen };
        var gesamt = System.Diagnostics.Stopwatch.StartNew();
        void Zeit(string name, System.Diagnostics.Stopwatch uhr) { lock (r.Zeiten) r.Zeiten.Add((name, uhr.ElapsedTicks * 1_000_000 / System.Diagnostics.Stopwatch.Frequency)); uhr.Restart(); }
        var polygone = umrisse.Select(u => u.Where(p => p is { Length: 2 }).Select(p => new Vector2((float)p[0], (float)p[1])).ToArray()).Where(u => u.Length >= 3).ToList();
        var rahmen = polygone.Select(pg => { var q0 = new Rect2(pg[0], Vector2.Zero); foreach (var q in pg) q0 = q0.Expand(q); return q0; }).ToList();
        var einfach = polygone.Select(FeldEinfach).ToArray();
        var maske = r.Maske = new Feldmaske(parzellen, polygone, po); // wo wirklich Acker liegt und der Abstand zu seinem Rand
        Zeit("Felder: Feldmaske", gesamt);
        var fuss = BodenBewegt ? FeldHoeheAmHang : FeldHoehe;
        var gruppen = parzellen.GroupBy(p => p.Frucht).ToList();
        var ergebnisse = new (FeldNetzDaten? Netz, List<(Vector2 Mitte, float Drehung, Vector2 Halb)> Sperr)[gruppen.Count];
        // Die Pflanzen brauchen nur die Maske und laufen neben den Netzen der Früchte; die Früchte sind voneinander unabhängig und rechnen alle zugleich (die Reihenfolge der Ergebnisse bleibt die der Früchte).
        var netzeAufgabe = Task.Run(() => Parallel.For(0, gruppen.Count, po, g => ergebnisse[g] = FeldFruchtRechnen(gruppen[g].Key, gruppen[g].ToList(), polygone, rahmen, einfach, maske, fuss, po, Zeit)));
        var uhrPflanzen = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            r.Pflanzen = FeldPflanzenBauer.Rechnen(parzellen, maske, (x, z) => BodenHoehe(x, z), fuss - 0.02f, PflanzenNetz(), po); // Stadtansicht.Feldpflanzen.cs
        }
        finally
        {
            try { netzeAufgabe.Wait(); } catch (Exception) { } // auch bei Abbruch oder Fehler abwarten: kein Faden rechnet weiter, wenn der Aufrufer zurückkehrt
        }
        netzeAufgabe.Wait(); // wirft, falls die Früchte gescheitert sind
        Zeit("Felder: Pflanzen rechnen (mit MultiMesh)", uhrPflanzen);
        foreach (var (netz, sperr) in ergebnisse)
        {
            if (netz is null) continue;
            r.Netze.Add(netz);
            r.Sperr.AddRange(sperr);
        }
        Zeit("Felder: gerechnet insgesamt (Wand)", gesamt);
        return r;
    }

    /// <summary>
    /// Die Netze einer Frucht (reine Zahlen, in <see cref="FeldRechnen"/> neben den anderen Früchten): Teile zuschneiden (je Stück eines Ackers in einem eigenen Lauf),
    /// danach Ecken, Normalen, UV, Farben und Indizes aller Teile in Arrays genau der Größe, die ein Prefixsummen-Lauf ergibt (jeder Teil schreibt seinen eigenen Bereich, die
    /// Reihenfolge ist die der Teile – dasselbe Netz wie bei der Rechnung nacheinander) und das <see cref="ArrayMesh"/>.
    /// </summary>
    private (FeldNetzDaten? Netz, List<(Vector2 Mitte, float Drehung, Vector2 Halb)> Sperr) FeldFruchtRechnen(string frucht, List<Feldparzelle> stuecke, List<Vector2[]> polygone, List<Rect2> rahmen, bool[] einfach,
        Feldmaske maske, float fuss, ParallelOptions po, Action<string, System.Diagnostics.Stopwatch> zeit)
    {
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        // Erst werden die Teile (Vielecke in der Welt) gesammelt, danach rechnen alle Prozessorkerne ihre Ecken (Abstand zum Rand, Höhe, Normale): Das war der Löwenanteil der Ladezeit.
        var infos = new (Vector2 Mitte, float Winkel, Basis Zurueck, Color Farbstich)[stuecke.Count];
        var teileJe = new List<Vector2[]>[stuecke.Count];
        Parallel.For(0, stuecke.Count, po, nr =>
        {
            {
                var p = stuecke[nr];
                var (b, l) = ((float)p.Breite / 2, (float)p.Laenge / 2);
                var winkel = Mathf.DegToRad((float)p.Drehung);
                var basis = new Basis(Vector3.Up, winkel);
                var zurueck = basis.Inverse();
                var mitte = new Vector2((float)p.X, (float)p.Z);
                // Farbstich aus Ort und Variante: gleiche Karte, gleiche Äcker (kein Zufall je Start)
                var stich = 0.94f + 0.04f * ((p.Variante - 1) % 4) + 0.02f * Mathf.PosMod((float)(p.X * 0.071 - p.Z * 0.053), 1f);
                var farbstich = new Color(stich, stich, stich * (p.Variante % 2 == 0 ? 1.02f : 0.97f), Mathf.Min(1f, b / 16f)); // Alpha: halbe Breite für die Furchen am Rand
                infos[nr] = (mitte, winkel, zurueck, farbstich);
                var meine = teileJe[nr] = new List<Vector2[]>();

                void Teil(Vector2[] stueck) => meine.Add(stueck);
                Vector2 Welt(float lx, float lz) { var w = basis * new Vector3(lx, 0, lz); return mitte + new Vector2(w.X, w.Z); }

                // Quer zum Stück liegt an jeder Furche und jedem Kamm eine Kante (Halbstreifen, acker.gdshader hebt die Kämme); auf Gelände längs Zellen bis FeldZelle Meter.
                // Grobe Zellen (mehrere Halbstreifen, bis FeldZelle breit) werden an den Rand der Feldflächen zugeschnitten; was ganz drin liegt, teilt sich ohne Schnitt.
                var anzahl = Mathf.Max(1, Mathf.RoundToInt(2 * b / FeldFurchenAbstand));
                var halbe = 2 * anzahl;
                var breiteHalb = 2 * b / halbe;
                var gruppeBreite = Mathf.Max(1, Mathf.RoundToInt(FeldZelle / breiteHalb));
                var nz = BodenBewegt ? Mathf.Max(1, Mathf.CeilToInt(2 * l / FeldZelle)) : 1;
                for (var iz = 0; iz < nz; iz++)
                {
                    var (v0, v1) = (-l + 2 * l * iz / nz, -l + 2 * l * (iz + 1) / nz);
                    for (var i0 = 0; i0 < halbe; i0 += gruppeBreite)
                    {
                        var i1 = Mathf.Min(halbe, i0 + gruppeBreite);
                        var (u0, u1) = (-b + breiteHalb * i0, i1 == halbe ? b : -b + breiteHalb * i1);
                        var zelle = new[] { Welt(u0, v0), Welt(u1, v0), Welt(u1, v1), Welt(u0, v1) };
                        foreach (var (stueck, ganz) in FeldZelleZuschneiden(zelle, polygone, rahmen, einfach))
                        {
                            if (ganz)
                            {
                                for (var i = i0; i < i1; i++)
                                {
                                    var (ua, ub) = (-b + breiteHalb * i, i + 1 == halbe ? b : -b + breiteHalb * (i + 1));
                                    Teil(new[] { Welt(ua, v0), Welt(ub, v0), Welt(ub, v1), Welt(ua, v1) });
                                }
                                continue;
                            }
                            // zugeschnittenes Stück: in die Halbstreifen teilen, die es berührt
                            var lokal = stueck.Select(q => { var e3 = zurueck * new Vector3(q.X - mitte.X, 0, q.Y - mitte.Y); return new Vector2(e3.X, e3.Z); }).ToArray();
                            var (lo, hi) = (lokal.Min(q => q.X), lokal.Max(q => q.X));
                            var (ia, ib) = (Mathf.Clamp(Mathf.FloorToInt((lo + b) / breiteHalb), i0, i1 - 1), Mathf.Clamp(Mathf.FloorToInt((hi + b) / breiteHalb), i0, i1 - 1));
                            if (ia == ib) { Teil(stueck); continue; }
                            for (var i = ia; i <= ib; i++)
                            {
                                var (ua, ub) = (i == i0 ? -1e4f : -b + breiteHalb * i, i == i1 - 1 ? 1e4f : -b + breiteHalb * (i + 1));
                                var streifen = new[] { new Vector2(ua, -1e4f), new Vector2(ub, -1e4f), new Vector2(ub, 1e4f), new Vector2(ua, 1e4f) };
                                foreach (var teil in Geometry2D.IntersectPolygons(lokal, streifen))
                                    if (teil.Length >= 3 && !Geometry2D.IsPolygonClockwise(teil)) Teil(teil.Select(q => Welt(q.X, q.Y)).ToArray());
                            }
                        }
                    }
                }
            }
        });
        var teile = new List<(Vector2[] Stueck, int Nr)>();
        for (var nr = 0; nr < teileJe.Length; nr++)
            foreach (var t in teileJe[nr]) teile.Add((t, nr));
        zeit("Felder: Teile sammeln und zuschneiden (CPU)", uhr);
        if (teile.Count == 0) return (null, new());

        // Dreiecke: Vierecke bekommen ihre zwei ohne Aufruf, andere Vielecke zerlegt Godot (Geometry2D, ohne Szene, in jedem Faden erlaubt); eine Zerlegung ohne Dreieck lässt den Teil weg.
        var viereck = new[] { 0, 1, 2, 0, 2, 3 };
        var zerlegt = new int[teile.Count][];
        Parallel.For(0, teile.Count, po, t =>
        {
            var idx = teile[t].Stueck.Length == 4 ? viereck : Geometry2D.TriangulatePolygon(teile[t].Stueck);
            zerlegt[t] = idx.Length < 3 ? Array.Empty<int>() : idx;
        });
        var (eckenOffset, indexOffset) = (new int[teile.Count + 1], new int[teile.Count + 1]);
        for (var t = 0; t < teile.Count; t++)
        {
            var gueltig = zerlegt[t].Length >= 3;
            eckenOffset[t + 1] = eckenOffset[t] + (gueltig ? teile[t].Stueck.Length : 0);
            indexOffset[t + 1] = indexOffset[t] + zerlegt[t].Length / 3 * 3;
        }
        var ecken = new Vector3[eckenOffset[^1]];
        var normalen = new Vector3[ecken.Length];
        var uv = new Vector2[ecken.Length];
        var uv2 = new Vector2[ecken.Length];
        var farben = new Color[ecken.Length];
        var indizes = new int[indexOffset[^1]];
        var grenzeMin = new Vector2[teile.Count];
        var grenzeMax = new Vector2[teile.Count];
        // Je Stück eines Ackers ein Lauf: Die Teile eines Stücks teilen sich Ecken (Nachbarstreifen, die Naht zur nächsten Zelle längs), und Höhe, Normale und Abstand zum Rand
        // hängen nur vom Ort ab – jede Ecke wird je Stück einmal gerechnet und danach aus einer Merkliste gelesen (gleiche Zahlen wie bei jeder Ecke einzeln).
        var teilVon = new int[infos.Length + 1];
        foreach (var (_, nr) in teile) teilVon[nr + 1]++;
        for (var nr = 0; nr < infos.Length; nr++) teilVon[nr + 1] += teilVon[nr];
        Parallel.For(0, infos.Length, po, nr =>
        {
            var (mitte, winkel, zurueck, farbstich) = infos[nr];
            var merk = new Dictionary<(float, float), (float Hoehe, Vector3 Normale, Vector2 Uv, float Abstand)>();
            for (var t = teilVon[nr]; t < teilVon[nr + 1]; t++)
            {
                var stueck = teile[t].Stueck;
                var idx = zerlegt[t];
                if (idx.Length == 0) continue;
                var anfang = eckenOffset[t];
                var (mn, mx) = (new Vector2(float.MaxValue, float.MaxValue), new Vector2(float.MinValue, float.MinValue));
                for (var i = 0; i < stueck.Length; i++)
                {
                    var q = stueck[i];
                    if (!merk.TryGetValue((q.X, q.Y), out var w))
                    {
                        var e3 = zurueck * new Vector3(q.X - mitte.X, 0, q.Y - mitte.Y);
                        merk[(q.X, q.Y)] = w = (BodenHoehe(q.X, q.Y) + fuss, BodenNormale(q.X, q.Y), new Vector2(e3.X, e3.Z), Mathf.Clamp(maske.Abstand(q.X, q.Y), 0f, FeldRandMax));
                    }
                    ecken[anfang + i] = new Vector3(q.X, w.Hoehe, q.Y);
                    normalen[anfang + i] = w.Normale;
                    uv[anfang + i] = w.Uv;
                    uv2[anfang + i] = new Vector2(winkel, w.Abstand);
                    farben[anfang + i] = farbstich;
                    (mn, mx) = (new Vector2(Mathf.Min(mn.X, w.Uv.X), Mathf.Min(mn.Y, w.Uv.Y)), new Vector2(Mathf.Max(mx.X, w.Uv.X), Mathf.Max(mx.Y, w.Uv.Y)));
                }
                (grenzeMin[t], grenzeMax[t]) = (mn, mx);
                var k0 = indexOffset[t];
                for (var k = 0; k + 2 < idx.Length; k += 3)
                {
                    int i0 = anfang + idx[k], i1 = anfang + idx[k + 1], i2 = anfang + idx[k + 2];
                    var gegen = (new Vector2(ecken[i1].X, ecken[i1].Z) - new Vector2(ecken[i0].X, ecken[i0].Z)).Cross(new Vector2(ecken[i2].X, ecken[i2].Z) - new Vector2(ecken[i0].X, ecken[i0].Z)) >= 0;
                    (indizes[k0 + k], indizes[k0 + k + 1], indizes[k0 + k + 2]) = gegen ? (i0, i1, i2) : (i0, i2, i1);
                }
            }
        });
        var grenzen = new (Vector2 Min, Vector2 Max)[infos.Length];
        for (var i = 0; i < grenzen.Length; i++) grenzen[i] = (new Vector2(float.MaxValue, float.MaxValue), new Vector2(float.MinValue, float.MinValue));
        for (var t = 0; t < teile.Count; t++) // das Stück nach dem Zuschneiden, aus den UV seiner Teile
        {
            if (zerlegt[t].Length == 0) continue;
            var nr = teile[t].Nr;
            grenzen[nr] = (new Vector2(Mathf.Min(grenzen[nr].Min.X, grenzeMin[t].X), Mathf.Min(grenzen[nr].Min.Y, grenzeMin[t].Y)), new Vector2(Mathf.Max(grenzen[nr].Max.X, grenzeMax[t].X), Mathf.Max(grenzen[nr].Max.Y, grenzeMax[t].Y)));
        }
        var sperr = new List<(Vector2 Mitte, float Drehung, Vector2 Halb)>();
        for (var nr = 0; nr < grenzen.Length; nr++) // ein gedrehtes Rechteck je Stück für den Grasteppich
        {
            var (minL, maxL) = grenzen[nr];
            if (minL.X > maxL.X) continue;
            var (mitte, winkel, _, _) = infos[nr];
            var mitteL = (minL + maxL) / 2;
            var weltM = new Basis(Vector3.Up, winkel) * new Vector3(mitteL.X, 0, mitteL.Y);
            sperr.Add((mitte + new Vector2(weltM.X, weltM.Z), winkel, (maxL - minL) / 2));
        }
        zeit("Felder: Ecken und Arrays (CPU)", uhr);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = ecken;
        arrays[(int)Mesh.ArrayType.Normal] = normalen;
        arrays[(int)Mesh.ArrayType.TexUV] = uv;
        arrays[(int)Mesh.ArrayType.TexUV2] = uv2;
        arrays[(int)Mesh.ArrayType.Color] = farben;
        arrays[(int)Mesh.ArrayType.Index] = indizes;
        if (FeldPruefsumme) GD.Print($"[felder] Prüfsumme Netz {frucht}: {PruefsummeF(ecken.SelectMany(e => new[] { e.X, e.Y, e.Z }).Concat(normalen.SelectMany(e => new[] { e.X, e.Y, e.Z })).Concat(uv.SelectMany(e => new[] { e.X, e.Y })).Concat(uv2.SelectMany(e => new[] { e.X, e.Y })).Concat(farben.SelectMany(e => new[] { e.R, e.G, e.B, e.A })).Concat(indizes.Select(i => (float)i)))} ({ecken.Length} Ecken, {indizes.Length} Indizes)");
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        zeit("Felder: ArrayMesh anlegen (CPU)", uhr);
        return (new FeldNetzDaten(frucht, netz), sperr);
    }

    /// <summary>Prüfsumme über die Bitmuster von Zahlen (FNV), nur für den Vergleich von Läufen unter <c>--ladezeit</c>.</summary>
    /// <summary>Startoption <c>--feldsumme=1</c>: schreibt Prüfsummen der Netze und Pflanzen ins Log (Vergleich zweier Fassungen, ohne Wirkung sonst; kostet Zeit).</summary>
    internal static readonly bool FeldPruefsumme = Wasserarten.Startoption("feldsumme") == "1";

    internal static uint PruefsummeF(IEnumerable<float> zahlen)
    {
        var h = 2166136261u;
        foreach (var f in zahlen) h = (h ^ (uint)BitConverter.SingleToInt32Bits(f)) * 16777619u;
        return h;
    }

    /// <summary>Fragt das Gelände einmal im Hauptfaden an (es rechnet seine Höhen bei Bedarf); danach darf <see cref="FeldRechnen"/> es aus jedem Faden lesen.</summary>
    private void FeldGelaendeVorbereiten() { BodenHoehe(0, 0); BodenNormale(0, 0); }

    /// <summary>Hängt die gerechneten Äcker als Knoten unter <paramref name="eltern"/> (Hauptfaden): Netze, Grasteppich-Sperren, Pflanzen.</summary>
    private void FeldAnlegen(FeldRechnung r, Node3D eltern)
    {
        foreach (var (name, usec) in r.Zeiten) Ladezeit.Summe(name + " (gerechnet)", usec);
        var uhrNetze = Ladezeit.Uhr();
        foreach (var daten in r.Netze)
            eltern.AddChild(new MeshInstance3D
            {
                Name = "Acker_" + daten.Frucht, Mesh = daten.Netz, MaterialOverride = FeldStoff(daten.Frucht), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        Ladezeit.Dazu("Felder: Netze einhängen", uhrNetze);
        Ladezeit.Teil("Felder: GrasAeckerMelden", () => GrasAeckerMelden(eltern, r.Sperr, r.Maske)); // über Äckern kein Grasteppich (Stadtansicht.Gras.cs)
        if (r.Parzellen.Count > 0) eltern.AddChild(new FeldPflanzenBauer(r.Pflanzen, PflanzenStoff) { Name = "Pflanzen" }); // Pflanzen in den Reihen (Stadtansicht.Feldpflanzen.cs)
    }

    /// <summary>
    /// Die Zelle (vier Ecken in der Welt) gerade an den Rand der Feldflächen geschnitten: die Teile in der Fläche, jedes ein Vieleck. Eine Zelle, die
    /// ganz in einer Fläche liegt (alle Ecken drin, keine Ecke der Fläche in der Zelle), bleibt unverändert.
    /// Die meisten Zellen liegen klar innen oder klar außen: Das entscheidet <see cref="FeldPunktLage"/> in C#, ohne die teuren Aufrufe von Geometry2D (Marshalling der
    /// Vielecke); nur Zellen am Rand (eine Ecke näher als <see cref="FeldGrenzabstand"/> an einer Kante, oder eine Kante läuft durch die Zelle) gehen wie früher an Godot.
    /// Das Ergebnis ist dasselbe: wo C# „innen“ oder „außen“ sagt, ist es kein Grenzfall.
    /// </summary>
    private static IEnumerable<(Vector2[] Teil, bool Ganz)> FeldZelleZuschneiden(Vector2[] zelle, List<Vector2[]> polygone, List<Rect2> rahmen, bool[] einfach)
    {
        var box = new Rect2(zelle[0], Vector2.Zero);
        foreach (var q in zelle) box = box.Expand(q);
        for (var i = 0; i < polygone.Count; i++)
        {
            if (!rahmen[i].Intersects(box, true)) continue;
            var pg = polygone[i];
            var (innen, sicher) = (0, einfach[i]);
            if (sicher) foreach (var q in zelle)
            {
                var (drin, fern) = FeldPunktLage(q, pg);
                sicher &= fern;
                if (drin) innen++;
            }
            if (sicher && innen == 0 && !FeldKanteInBox(pg, box.Grow(FeldGrenzabstand))) continue; // klar außerhalb: der Schnitt wäre leer
            var ganz = sicher ? innen == 4 && !pg.Any(q => box.HasPoint(q)) : zelle.All(q => Geometry2D.IsPointInPolygon(q, pg)) && !pg.Any(q => box.HasPoint(q));
            if (ganz) { yield return (zelle, true); yield break; }
            foreach (var teil in Geometry2D.IntersectPolygons(zelle, pg))
                if (teil.Length >= 3 && !Geometry2D.IsPolygonClockwise(teil)) yield return (teil, false); // Löcher (im Uhrzeigersinn) zeichnet niemand
        }
    }

    /// <summary>
    /// Ein Umriss ohne doppelte Punkte, dessen Kanten sich nicht kreuzen oder berühren: nur für solche (die Felder der Karten) rechnet <see cref="FeldZelleZuschneiden"/> klare Fälle in C#;
    /// kreuzende oder entartete Umrisse (Werkstatt, beim Ziehen einer Ecke) gehen immer an Geometry2D.
    /// </summary>
    private static bool FeldEinfach(Vector2[] pg)
    {
        for (var i = 0; i < pg.Length; i++)
            for (var j = i + 1; j < pg.Length; j++)
                if (pg[i] == pg[j]) return false;
        static float Kreuz(Vector2 o, Vector2 a, Vector2 b) => (a - o).Cross(b - o);
        static bool Auf(Vector2 a, Vector2 b, Vector2 q) => q.X >= Mathf.Min(a.X, b.X) && q.X <= Mathf.Max(a.X, b.X) && q.Y >= Mathf.Min(a.Y, b.Y) && q.Y <= Mathf.Max(a.Y, b.Y);
        for (var i = 0; i < pg.Length; i++)
        {
            var (a, b) = (pg[i], pg[(i + 1) % pg.Length]);
            for (var j = i + 1; j < pg.Length; j++)
            {
                if (j == i + 1 || (i == 0 && j == pg.Length - 1)) continue; // Nachbarkanten teilen einen Punkt
                var (c, d) = (pg[j], pg[(j + 1) % pg.Length]);
                var (d1, d2, d3, d4) = (Kreuz(a, b, c), Kreuz(a, b, d), Kreuz(c, d, a), Kreuz(c, d, b));
                if (((d1 > 0) != (d2 > 0) || d1 == 0 || d2 == 0) && ((d3 > 0) != (d4 > 0) || d3 == 0 || d4 == 0)
                    && (d1 != 0 || Auf(a, b, c)) && (d2 != 0 || Auf(a, b, d)) && (d3 != 0 || Auf(c, d, a)) && (d4 != 0 || Auf(c, d, b))) return false;
            }
            // Nachbarkanten dürfen sich nicht überlagern (Rückspitze): der Punkt vor a liegt nicht auf der Strecke a–b und umgekehrt
            var vor = pg[(i + pg.Length - 1) % pg.Length];
            if (Kreuz(vor, a, b) == 0 && (vor - a).Dot(b - a) > 0) return false;
        }
        return true;
    }

    /// <summary>So nah (Meter) an einer Kante des Umrisses gilt ein Punkt als Grenzfall und geht an Godot.</summary>
    private const float FeldGrenzabstand = 0.001f;

    /// <summary>Liegt der Punkt im Vieleck (gerade-ungerade-Regel wie <c>Geometry2D.IsPointInPolygon</c>), und ist er mehr als <see cref="FeldGrenzabstand"/> von jeder Kante weg?</summary>
    private static (bool Innen, bool Fern) FeldPunktLage(Vector2 q, Vector2[] pg)
    {
        var (innen, fern) = (false, true);
        for (int i = 0, j = pg.Length - 1; i < pg.Length; j = i++)
        {
            var (a, c) = (pg[j], pg[i]);
            if ((c.Y > q.Y) != (a.Y > q.Y) && q.X < (a.X - c.X) * (q.Y - c.Y) / (a.Y - c.Y) + c.X) innen = !innen;
            var kante = c - a;
            var laenge2 = kante.LengthSquared();
            var t = laenge2 > 0 ? Mathf.Clamp((q - a).Dot(kante) / laenge2, 0f, 1f) : 0f;
            if ((q - (a + kante * t)).LengthSquared() <= FeldGrenzabstand * FeldGrenzabstand) fern = false;
        }
        return (innen, fern);
    }

    /// <summary>Berührt eine Kante des Umrisses die Box, oder liegt eine Ecke des Umrisses darin?</summary>
    private static bool FeldKanteInBox(Vector2[] pg, Rect2 box)
    {
        var (lo, hi) = (box.Position, box.End);
        for (int i = 0, j = pg.Length - 1; i < pg.Length; j = i++)
        {
            var (a, c) = (pg[j], pg[i]);
            if (box.HasPoint(c)) return true;
            // Strecke gegen Box (Liang-Barsky)
            var d = c - a;
            float t0 = 0, t1 = 1;
            var ausserhalb = false;
            for (var achse = 0; achse < 2 && !ausserhalb; achse++)
            {
                var (p0, dd, l, h) = achse == 0 ? (a.X, d.X, lo.X, hi.X) : (a.Y, d.Y, lo.Y, hi.Y);
                if (Mathf.Abs(dd) < 1e-12f) { if (p0 < l || p0 > h) ausserhalb = true; continue; }
                var (ta, tb) = ((l - p0) / dd, (h - p0) / dd);
                if (ta > tb) (ta, tb) = (tb, ta);
                t0 = Mathf.Max(t0, ta);
                t1 = Mathf.Min(t1, tb);
                if (t0 > t1) ausserhalb = true;
            }
            if (!ausserhalb) return true;
        }
        return false;
    }

    /// <summary>Weiter als so viel (Meter) vom Rand des Ackers (<see cref="Feldmaske"/>) weg zählt der Abstand nicht mehr (der Feldrain im Shader ist schmaler).</summary>
    private const float FeldRandMax = 4f;

    /// <summary>Die Hintergrundrechnung der Äcker der Karte (<see cref="FelderVorrechnen"/>): Stücke des Kerns und alles, was <see cref="FeldRechnen"/> daraus macht.</summary>
    private Task<(List<Feldparzelle> Parzellen, FeldRechnung? Rechnung)>? _felderAufgabe;

    /// <summary>
    /// Startet die Rechnung der Äcker der Karte im Hintergrund (Ladebildschirm, Stadtansicht.Karte.cs): Verteilung im Kern (<see cref="FeldVerteilung.Flaeche"/> mit
    /// <see cref="Naturfreiraum"/>), Zuschneiden, Ecken, Netze und Pflanzen sind reine Zahlen und brauchen den Hauptfaden nicht; sie laufen, während er Boden, Mauern,
    /// Markt und Natur lädt. Was Godot braucht, rechnet der Hauptfaden vorher: die Maße der Umlandmodelle (<see cref="Modellmass"/>) und das Gelände.
    /// </summary>
    private void FelderVorrechnen(Stadtkarte karte)
    {
        if (_felderAufgabe is not null) return;
        var uhr = Ladezeit.Uhr();
        var masse = new Dictionary<Baustein, (double X, double Z)?>(ReferenceEqualityComparer.Instance);
        var umland = karte.Gebaeude.Select(g => g.Baustein).OfType<Baustein>().Where(b => b.Art is Bausteinart.Umlandbetrieb or Bausteinart.Umlandstelle).Distinct().ToList();
        foreach (var b in umland) masse[b] = Modellmass(b);
        Ladezeit.Dazu("Felder: vorbereiten: Modellmass", uhr);
        uhr = Ladezeit.Uhr();
        FeldGelaendeVorbereiten();
        Ladezeit.Dazu("Felder: vorbereiten: Gelände anfragen", uhr);
        uhr = Ladezeit.Uhr();
        PflanzenNetz(); // das gemeinsame Büschelnetz im Hauptfaden anlegen
        Ladezeit.Dazu("Felder: vorbereiten: Pflanzennetz", uhr);
        // Der Hauptfaden lädt währenddessen weiter und braucht seine Kerne: die Hintergrundrechnung nimmt höchstens die Hälfte (sie hat Zeit bis zum Schritt „Felder“).
        var grad = Math.Max(2, System.Environment.ProcessorCount / 2);
        _felderAbbruch = new CancellationTokenSource();
        var po = new ParallelOptions { MaxDegreeOfParallelism = grad, CancellationToken = _felderAbbruch.Token, TaskScheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, grad).ConcurrentScheduler }; // ein Planer für alle Schleifen: auch verschachtelte teilen sich die Fäden
        _felderAufgabe = Task.Run(() =>
        {
            var uhrKern = System.Diagnostics.Stopwatch.StartNew();
            var frei = Naturfreiraum.Aus(karte, b => masse.GetValueOrDefault(b));
            var felder = karte.Natur.Felder;
            var je = new List<Feldparzelle>[felder.Count]; // jede Feldfläche für sich (der Freiraum wird nur gelesen), danach in der Reihenfolge der Datei
            Parallel.For(0, felder.Count, po, i => je[i] = FeldVerteilung.Flaeche(felder[i], frei, true));
            var parzellen = je.SelectMany(l => l).ToList();
            var kern = uhrKern.ElapsedTicks * 1_000_000 / System.Diagnostics.Stopwatch.Frequency;
            if (parzellen.Count == 0) return (parzellen, (FeldRechnung?)null);
            var rechnung = FeldRechnen(parzellen, karte.Natur.Felder.Select(f => f.Flaeche.ToArray()).ToList(), po);
            rechnung.Zeiten.Insert(0, ("Felder: Kern (Freiraum und Verteilung)", kern));
            return (parzellen, rechnung);
        });
    }

    private CancellationTokenSource? _felderAbbruch;

    /// <summary>Beim Verlassen der Stadtansicht (<c>_ExitTree</c>): Läuft die Hintergrundrechnung der Äcker noch (Beenden mitten im Laden), bricht sie ab und der Aufrufer wartet, bis kein Faden mehr Godot-Objekte anfasst.</summary>
    private void FelderAufgeben()
    {
        if (_felderAufgabe is not { } aufgabe) return;
        _felderAbbruch?.Cancel();
        for (var versuch = 0; versuch < 6 && !aufgabe.IsCompleted; versuch++)
        {
            try { if (aufgabe.Wait(10000)) break; }
            catch (Exception) { break; } // abgebrochen oder gescheitert: sie läuft nicht mehr
            GD.PushWarning("Felder: Die Hintergrundrechnung läuft nach dem Abbruch noch, warte weiter.");
        }
        if (!aufgabe.IsCompleted) GD.PushError("Felder: Die Hintergrundrechnung ließ sich nicht abwarten (60 s).");
        _felderAufgabe = null;
    }

    /// <summary>Die Äcker der Karte im Spiel (Kern: <see cref="FeldVerteilung.Alle"/>), alle in einem Knoten: nimmt die Hintergrundrechnung (<see cref="FelderVorrechnen"/>) und legt sie an.</summary>
    private void FelderAusKarte(Stadtkarte karte)
    {
        FelderVorrechnen(karte);
        var uhr = Ladezeit.Uhr();
        (List<Feldparzelle> Parzellen, FeldRechnung? Rechnung) ergebnis;
        try { ergebnis = _felderAufgabe!.GetAwaiter().GetResult(); }
        catch (Exception e) // die Hintergrundrechnung ist gescheitert: im Hauptfaden wie früher rechnen, das Spiel soll trotzdem laden
        {
            GD.PushError("Felder: Hintergrundrechnung gescheitert (" + e.Message + "), rechne im Hauptfaden.");
            FeldGelaendeVorbereiten();
            var parzellenNeu = FeldVerteilung.Alle(karte, Modellmass, bisZumRand: true);
            ergebnis = (parzellenNeu, parzellenNeu.Count == 0 ? null : FeldRechnen(parzellenNeu, karte.Natur.Felder.Select(f => f.Flaeche.ToArray()).ToList()));
        }
        _felderAufgabe = null;
        var rechnung = ergebnis.Rechnung;
        Ladezeit.Dazu("Felder: auf die Hintergrundrechnung gewartet", uhr);
        if (rechnung is null) return;
        var uhrAnlegen = Ladezeit.Uhr();
        var felder = new Node3D { Name = "Felder" };
        AddChild(felder);
        FeldAnlegen(rechnung, felder);
        Ladezeit.Dazu("Felder: anlegen (Knoten, Stoffe)", uhrAnlegen);
    }
}
