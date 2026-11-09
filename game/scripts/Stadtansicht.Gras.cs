using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Der Grasteppich (Kartenwerkstatt, Schritt 5i) – ein Teil der <c>partial class Stadtansicht</c>. Auf der Wiese steht ein dichter Teppich kleiner
/// 3D-Halme, aber nur im Bild: Die Grafikkarte zeichnet ihn (<c>gras.gdshader</c>), hier wird nur vorbereitet, was sie dafür braucht. Einstellung
/// „Gras“ (<see cref="Fenster.AktuellerGrasteppich"/>): Dicht in der Voreinstellung Hoch, Sehr dicht in Ultra, sonst aus. Nichts davon wird gespeichert.
/// <list type="bullet">
/// <item><b>Felder</b>: Der Boden ist in Felder zu 4 × 4 m geteilt. Jedes Feld zeichnet dasselbe Netz aus Büscheln (<see cref="GrasNetze"/>, Stufen
/// von 1,5 bis 64 Büscheln je m²); der Shader spiegelt es je Feld, setzt es auf den Boden und lässt weg, was nicht auf der Wiese steht.</item>
/// <item><b>Kacheln</b> (<see cref="GrasKachelRechnen"/>, parallel): je Geländekachel (128 m) zwei Bilder zu 1 m – Dichte der Wiese (aus dem
/// Bodenraster, ohne stehende Gebäude, Baustellen, Äcker, Wasser, Fels am Hang und die Stadt; freie Plätze und Bauzonen tragen im Spiel Gras), Höhe und Normale des Bodens; Makro-Flecken
/// und Horste –, dazu die Felder, auf denen überhaupt Gras wächst. Neu gerechnet wird nur, was sich ändert – dieselben Meldungen wie für die
/// Grasbüschel (Stadtansicht.Bueschel.cs).</item>
/// <item><b>Im Bild</b> (<see cref="GrasImBild"/>): Je Kachel ein MultiMesh mit den Feldern, die die Kamera sieht (die Ecken des Bildes auf den Boden
/// geworfen, bei der tiefsten und der höchsten Stelle der Kachel); neu nur, wenn sich Kamera, Zoom oder eine Kachel ändern. Mit dem Zoom wählt es das
/// Netz: weit weg weniger, dafür breitere Halme – es stehen etwa gleich viele im Bild.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Seitenlänge eines Feldes (Meter) und Felder je Seite einer Geländekachel.</summary>
    private const float GrasFeld = 4f;
    private const int GrasFelder = (int)(KachelZellen * 2 / GrasFeld);

    /// <summary>Büschel je m² der Netze eines Feldes (der Zoom wählt das kleinste, das genügt; die Schritte sind klein, weil jede Ecke Zeit kostet).</summary>
    private static readonly float[] GrasNetzDichten = { 1.5f, 2, 2.5f, 3, 3.5f, 4, 5, 6, 7, 8, 10, 12, 16, 24, 32, 48, 64 };

    /// <summary>Rasterpunkte des Geländes (2 m), die eine Kachel für ihr Bild liest: einer davor, zwei dahinter.</summary>
    private const int GrasRaster = KachelZellen + 3;

    /// <summary>Je Stufe der Einstellung: Büschel je m² beim Standard-Zoom und höchstens (nah), Höhe der Halme (Meter).</summary>
    private static (float Dichte, float Hoechstens, float Hoehe) GrasWerte(Grasteppich g)
    {
        var (dichte, hoechstens, hoehe) = g switch { Grasteppich.SehrDicht => (6f, 64f, 0.38f), _ => (2.5f, 32f, 0.36f) };
        // Pixel-Look: Halme kleiner als ein Weltpixel sind nur Rauschen – weniger Halme sehen gleich aus und sparen Zeichenarbeit (Stadtansicht.Pixel.cs)
        var pixel = Fenster.PixelLookAn ? 0.4f : 1f; // ein fester Wert, damit der Wechsel der Pixelgröße beim Zoomen das Gras nicht neu baut
        return (dichte * pixel, hoechstens * pixel, hoehe);
    }

    /// <summary>Die Netze der Felder (einmal gebaut, über Szenenwechsel gehalten wie die Shader).</summary>
    private static ArrayMesh[]? _grasNetze;
    private static Shader? _grasShader;
    private static Color? _wiesenFarbe;

    private sealed class GrasKachel
    {
        public required ShaderMaterial Stoff;
        public required MultiMeshInstance3D Knoten;
        public ImageTexture? Bild, Farbe;
        /// <summary>Die Felder mit Gras (Nummer = Zeile · <see cref="GrasFelder"/> + Spalte).</summary>
        public int[] Felder = Array.Empty<int>();
        /// <summary>Tiefste und höchste Stelle des Bodens in der Kachel.</summary>
        public float Unten, Oben;
        public float[] Puffer = Array.Empty<float>();
        /// <summary>Das zuletzt gesetzte Bild (je Punkt Dichte, Höhe, Normale x, z) – für das schnelle Nachführen der Höhe beim Malen.</summary>
        public float[]? Daten;
    }

    private readonly Dictionary<(int I, int J), GrasKachel> _grasKacheln = new();

    /// <summary>Das grobe Bild der Wegeschicht je Kachel (Stadtansicht.Wegeschicht.cs, B = Pflaster als Grund): In der Stadt steht kein Gras.</summary>
    private readonly Dictionary<(int I, int J), byte[]> _grasStadt = new();
    private readonly HashSet<(int I, int J)> _grasNeu = new();
    private bool _grasAlles = true;
    private Node3D? _grasWurzel;
    private bool _grasMakroStark = true;

    /// <summary>Wofür die Felder im Bild zuletzt gewählt wurden (Kamera, Zoom, Bildgröße, Netz); null = neu wählen.</summary>
    private (Vector3 Ort, Vector3 Blick, float Zoom, Vector2 Bild, int Netz, bool Pixel)? _grasBlickFuer;

    /// <summary>Die Äcker, über denen kein Gras steht: je Knoten der Äcker (im Spiel einer, in der Werkstatt einer je Feldfläche) die Stücke.</summary>
    private readonly Dictionary<Node3D, (Vector2 Mitte, float Drehung, Vector2 Halb)[]> _grasAecker = new();

    /// <summary>Die ausgefranste Feldmaske zu jedem Knoten der Äcker (<see cref="Feldmaske"/>): Das Gras dünnt am Feldrain aus, wie der Acker dort ausläuft.</summary>
    private readonly Dictionary<Node3D, Feldmaske> _grasMasken = new();

    private static bool GrasAn => Fenster.AktuellerGrasteppich != Grasteppich.Aus;

    // ═════════════════════════════════════════════════════════════════════════
    //  Melden (aus den Meldungen der Grasbüschel, Stadtansicht.Bueschel.cs)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Der Boden wird neu gebaut: Die Knoten hingen an ihm, alles neu.</summary>
    private void GrasVergessen()
    {
        _grasWurzel = null;
        _grasKacheln.Clear();
        _grasNeu.Clear();
        _grasAlles = true;
        _grasBlickFuer = null;
    }

    /// <summary>Boden, Höhe, Wege oder Hindernisse einer Kachel haben sich geändert.</summary>
    private void GrasKachelNeu((int I, int J) kachel) => _grasNeu.Add(kachel);

    /// <summary>Im Rechteck (Welt-X/Z) hat sich etwas geändert: die Kacheln darin neu.</summary>
    private void GrasBereichNeu(Rect2 r)
    {
        const float kachel = KachelZellen * 2f;
        for (var j = Mathf.FloorToInt(r.Position.Y / kachel); j <= Mathf.FloorToInt(r.End.Y / kachel); j++)
            for (var i = Mathf.FloorToInt(r.Position.X / kachel); i <= Mathf.FloorToInt(r.End.X / kachel); i++)
                _grasNeu.Add((i, j));
    }

    /// <summary>Auf einem Platz steht jetzt ein Haus oder keins mehr (Betrieb, Einwohnerhaus): die Kacheln um ihn neu.</summary>
    private void GrasPlatz(Vector3 mitte, Vector2 groesse)
    {
        var r = groesse.Length() / 2 + 1;
        GrasBereichNeu(new Rect2(mitte.X - r, mitte.Z - r, 2 * r, 2 * r));
    }

    /// <summary>Ein Grundstück hat sich geändert (Stadtansicht.Bauen.cs): Haus, Baustelle oder Bauplatz.</summary>
    private void GrasGrundstueck(Grundriss g) =>
        GrasBereichNeu(new Rect2((float)(g.X - g.HalbX) - 1, (float)(g.Z - g.HalbZ) - 1, (float)g.HalbX * 2 + 2, (float)g.HalbZ * 2 + 2));

    /// <summary>Äcker sind gebaut worden (Stadtansicht.Rand.cs): Dort wächst kein Gras.</summary>
    private void GrasAeckerMelden(Node3D knoten, IReadOnlyList<(Vector2 Mitte, float Drehung, Vector2 Halb)> stuecke, Feldmaske maske)
    {
        if (stuecke.Count == 0) return;
        _grasAecker[knoten] = stuecke.ToArray();
        _grasMasken[knoten] = maske;
        GrasBereichNeu(AeckerRahmen(stuecke));
    }

    /// <summary>Die Äcker unter diesem Knoten sind weg (Werkstatt).</summary>
    private void GrasAeckerWeg(Node3D knoten)
    {
        _grasMasken.Remove(knoten);
        if (_grasAecker.Remove(knoten, out var alt)) GrasBereichNeu(AeckerRahmen(alt));
    }

    private static Rect2 AeckerRahmen(IReadOnlyList<(Vector2 Mitte, float Drehung, Vector2 Halb)> stuecke)
    {
        var r = new Rect2(stuecke[0].Mitte, Vector2.Zero);
        foreach (var p in stuecke)
        {
            var halb = Mathf.Max(p.Halb.X, p.Halb.Y) * 1.42f + 1; // auch gedreht noch im Rahmen
            r = r.Merge(new Rect2(p.Mitte - new Vector2(halb, halb), new Vector2(2 * halb, 2 * halb)));
        }
        return r;
    }

    /// <summary>Die Einstellung hat sich geändert (aus <see cref="BodenqualitaetNachfuehren"/>): Werte der Stoffe und Netz neu.</summary>
    private void GrasNachfuehren()
    {
        foreach (var k in _grasKacheln.Values) GrasStoffWerte(k.Stoff);
        if (_grasMakroStark != (Fenster.AktuelleBodenqualitaet == Bodenqualitaet.Hoch)) _grasAlles = true; // die Makro-Flecken stehen im Bild der Kacheln
        foreach (var (stoff, _) in _bodenStoffe.Values) stoff.SetShaderParameter("teppich", GrasAn ? 1f : 0f); // die Wiese darunter (boden.gdshaderinc)
        _grasBlickFuer = null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Jedes Bild (nach den Grasbüscheln): geänderte Kacheln neu rechnen und die Felder im Bild wählen. Aus in der Raumansicht und ohne
    /// Geländekacheln; während eines Pinselstrichs in der Werkstatt wartet das Rechnen (wie bei der Deko).
    /// </summary>
    private void GrasBild()
    {
        var an = GrasAn && _raum is null && _gelaendeKnoten is not null && _randKnoten is not null && GrasLaden();
        if (_grasWurzel is not null && IsInstanceValid(_grasWurzel)) _grasWurzel.Visible = an;
        if (!an) return;
        if (_grasWurzel is null || !IsInstanceValid(_grasWurzel))
        {
            _grasWurzel = new Node3D { Name = "Grasteppich" };
            _randKnoten!.AddChild(_grasWurzel);
            _grasKacheln.Clear();
            _grasAlles = true;
        }
        // Während eines Pinselstrichs wartet das volle Rechnen bis zum Loslassen; beim Geländepinsel folgen die Halme der Höhe schon beim Malen (GrasHoeheNachfuehren)
        var messen = Pinselmessung.Uhr();
        GrasHoeheNachfuehren();
        Pinselmessung.Dazu("  Gras: Höhe beim Malen nachführen", messen);
        // (auch erst, wenn die Nacharbeit eines Geländestrichs fertig ist: Sie meldet weitere Kacheln, und so rechnet jede nur einmal)
        if ((_grasAlles || _grasNeu.Count > 0) && !_wsBodenStrich && _entwurf is not { PinselstrichLaeuft: true } && _wsNacharbeit.Count == 0) GrasKachelnRechnen();
        GrasImBild();
    }

    /// <summary>Während eines Geländestrichs: wo sich die Höhe geändert hat (null: nirgends) – <see cref="GrasHoeheNachfuehren"/> führt dort die Halme nach.</summary>
    private Kartenrechteck? _grasHoehe;

    private void GrasHoeheVormerken(Kartenrechteck r) =>
        _grasHoehe = _grasHoehe is { } a ? new Kartenrechteck(Math.Min(a.X0, r.X0), Math.Min(a.Z0, r.Z0), Math.Max(a.X1, r.X1), Math.Max(a.Z1, r.Z1)) : r;

    /// <summary>
    /// Beim Malen mit dem Geländepinsel: In den Bildern der Kacheln nur Höhe und Normale der Punkte im geänderten Rechteck neu (gerechnet wie in
    /// <see cref="GrasKachelRechnen"/>), die Dichte bleibt bis zum Loslassen – dann rechnet <see cref="GrasKachelnRechnen"/> die Kacheln ganz. Vorher wartete
    /// das Gras entweder bis zum Loslassen (die Halme schwebten oder versanken) oder rechnete in jedem Bild die ganzen Kacheln (Köln 12–18 ms je Tupfer).
    /// </summary>
    private void GrasHoeheNachfuehren()
    {
        if (_grasHoehe is not { } r || _gelaende is null) return;
        _grasHoehe = null;
        const int n = BodenBild;
        const float abstand = (float)Hoehenraster.Abstand;
        var blickNeu = false;
        foreach (var ((ti, tj), k) in _grasKacheln)
        {
            if (k.Daten is not { } daten || k.Bild is null) continue;
            var (i0, j0) = (ti * KachelZellen * 2 - 1, tj * KachelZellen * 2 - 1);
            var (a0, a1) = (Math.Max(0, (int)Math.Floor(r.X0) - i0 - 1), Math.Min(n - 1, (int)Math.Ceiling(r.X1) - i0 + 1));
            var (b0, b1) = (Math.Max(0, (int)Math.Floor(r.Z0) - j0 - 1), Math.Min(n - 1, (int)Math.Ceiling(r.Z1) - j0 + 1));
            if (a0 > a1 || b0 > b1) continue;
            // die Rasterpunkte (2 m) unter diesen Punkten, samt dem Nachbarn rechts bzw. unten
            var (gi0, gj0) = ((int)MathF.Floor((i0 + a0) / abstand), (int)MathF.Floor((j0 + b0) / abstand));
            var (gb, gt) = ((int)MathF.Floor((i0 + a1) / abstand) + 2 - gi0, (int)MathF.Floor((j0 + b1) / abstand) + 2 - gj0);
            var hoehen = _gelaende.Ausschnitt(gi0, gj0, gb, gt);
            float H(int i, int j) => hoehen[(j - gj0) * gb + i - gi0];
            var (unten, oben) = (k.Unten, k.Oben);
            for (var b = b0; b <= b1; b++)
                for (var a = a0; a <= a1; a++)
                {
                    var (x, z) = ((float)(i0 + a), (float)(j0 + b));
                    var (fx, fz) = (x / abstand, z / abstand);
                    var (i, j) = ((int)MathF.Floor(fx), (int)MathF.Floor(fz));
                    var (tx, tz) = (fx - i, fz - j);
                    float h00 = H(i, j), h10 = H(i + 1, j), h01 = H(i, j + 1), h11 = H(i + 1, j + 1);
                    var h = tx >= tz ? h00 + (h10 - h00) * tx + (h11 - h10) * tz : h00 + (h11 - h01) * tx + (h01 - h00) * tz;
                    var sx = ((h10 - h00) * (1 - tz) + (h11 - h01) * tz) / abstand;
                    var sz = ((h01 - h00) * (1 - tx) + (h11 - h10) * tx) / abstand;
                    var normale = new Vector3(-sx, 1, -sz).Normalized();
                    var o = (b * n + a) * 4;
                    daten[o + 1] = h;
                    daten[o + 2] = normale.X;
                    daten[o + 3] = normale.Z;
                    (unten, oben) = (Mathf.Min(unten, h), Mathf.Max(oben, h));
                }
            k.Bild.Update(Image.CreateFromData(n, n, false, Image.Format.Rgbaf, MemoryMarshal.AsBytes(daten.AsSpan()).ToArray()));
            if (unten < k.Unten || oben > k.Oben)
            {
                (k.Unten, k.Oben) = (unten, oben);
                var (x0, z0) = (ti * KachelZellen * 2f, tj * KachelZellen * 2f);
                k.Knoten.CustomAabb = new Aabb(new Vector3(x0 - 2, unten - 1, z0 - 2), new Vector3(KachelZellen * 2f + 4, oben - unten + 2.5f, KachelZellen * 2f + 4));
                blickNeu = true;
            }
        }
        if (blickNeu) _grasBlickFuer = null; // die Felder im Bild hängen an tiefster und höchster Stelle
    }

    /// <summary>Shader, Netze und die Farbe der Wiese (einmal); false, wenn etwas fehlt (dann kein Gras).</summary>
    private bool GrasLaden()
    {
        if (_grasShader is not null) return true;
        if (!BodenShaderLaden() || !ResourceLoader.Exists("res://assets/shader/gras.gdshader")) return false;
        _grasNetze ??= GrasNetze();
        _wiesenFarbe ??= WiesenFarbe(_bodenTexturen);
        _grasShader = GD.Load<Shader>("res://assets/shader/gras.gdshader");
        return _grasShader is not null;
    }

    /// <summary>Das Mittel der Wiesentextur (Schicht 0 von <c>boden_farbe_hoehe.webp</c>): die Grundfarbe der Halme.</summary>
    private static Color WiesenFarbe(TextureLayered? texturen)
    {
        if (texturen?.GetLayerData(0) is not { } bild) return new Color(0.33f, 0.4f, 0.19f);
        bild = (Image)bild.Duplicate();
        if (bild.IsCompressed()) bild.Decompress();
        bild.ClearMipmaps();
        bild.Convert(Image.Format.Rgba8);
        var daten = bild.GetData();
        double r = 0, g = 0, b = 0;
        var n = 0;
        for (var i = 0; i < daten.Length; i += 4 * 7) // jeder siebte Bildpunkt genügt
        {
            r += daten[i];
            g += daten[i + 1];
            b += daten[i + 2];
            n++;
        }
        return n == 0 ? new Color(0.33f, 0.4f, 0.19f) : new Color((float)(r / n / 255), (float)(g / n / 255), (float)(b / n / 255));
    }

    /// <summary>Die Werte der Einstellung in einem Stoff.</summary>
    private static void GrasStoffWerte(ShaderMaterial stoff)
    {
        var (dichte, hoechstens, hoehe) = GrasWerte(Fenster.AktuellerGrasteppich);
        stoff.SetShaderParameter("dichte", dichte);
        stoff.SetShaderParameter("dichte_max", hoechstens);
        stoff.SetShaderParameter("hoehe", hoehe);
        stoff.SetShaderParameter("ruhig", Fenster.PixelLookAn); // Pixel-Look: Halme wie der Boden beleuchtet, kein Flackern
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kacheln rechnen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Rechnet die gemeldeten (oder alle) Kacheln parallel und gibt ihnen Bild, Stoff und Knoten.</summary>
    private void GrasKachelnRechnen()
    {
        var liste = _grasAlles ? _gelaendeKacheln.Keys.ToList() : _grasNeu.Where(_gelaendeKacheln.ContainsKey).ToList();
        var alles = _grasAlles;
        _grasAlles = false;
        _grasNeu.Clear();
        if (liste.Count == 0) return;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var messen = Pinselmessung.Uhr();
        // auf dem Hauptfaden sammeln, was die Rechnung liest
        var raster = BodenRaster;
        var uhrGras = Ladezeit.Uhr();
        var frei = GrasFreiraum();
        var wasser = GrasWasser(GewaesserForm);
        var verbuende = GewaesserForm?.Verbuende ?? (IReadOnlyList<Wasserverbund>)Array.Empty<Wasserverbund>();
        // Bauzonen wie Seen als Vielecke: In der Werkstatt (ihre farbige Fläche bleibt frei von Halmen) und im Hauptmenü; im Spiel wächst dort Gras,
        // bis gebaut wird (Schritt 5j, die Grundstücke stehen in den Sperrflächen)
        if (_entwurf is not null) wasser.Seen.AddRange(_entwurf.Bauzonen.Select(z => z.Ecken.Select(e => new Vector2((float)e.X, (float)e.Z)).ToArray()));
        else if (_karte is not null && _wirtschaft is null) wasser.Seen.AddRange(_karte.Bauzonen.Select(z => z.Flaeche.Select(e => new Vector2((float)e[0], (float)e[1])).ToArray()));
        wasser.Seen.RemoveAll(v => v.Length < 3);
        var flaechen = GrasSperrflaechen();
        var masken = _grasMasken.Where(m => IsInstanceValid(m.Key) && !m.Key.IsQueuedForDeletion()).Select(m => m.Value).ToArray();
        // die Höhen der Rasterpunkte je Kachel am Stück (der Kern rechnet das Bett der Wege dabei nach, das geht nicht parallel)
        var hoehen = liste.Select(k => BodenBewegt ? _gelaende!.Ausschnitt(k.I * KachelZellen - 1, k.J * KachelZellen - 1, GrasRaster, GrasRaster) : null).ToArray();
        Ladezeit.Dazu("Gras: Vorbereiten und Höhen", uhrGras);
        Pinselmessung.Dazu("  Gras: sammeln (Freiraum, Wasser, Höhen)", messen);
        messen = Pinselmessung.Uhr();
        uhrGras = Ladezeit.Uhr();
        var ergebnis = new (float[] Daten, byte[] Farbe, float Unten, float Oben, int[] Felder)[liste.Count];
        var stadt = liste.Select(k => _grasStadt.GetValueOrDefault(k)).ToArray();
        var stark = _grasMakroStark = Fenster.AktuelleBodenqualitaet == Bodenqualitaet.Hoch;
        var felsKarte = _felsbodenAn == true; // nur Karten mit Felsen: die Wiese um und zwischen den Flecken wird steinig (Stadtansicht.Felsboden.cs)
        Parallel.For(0, liste.Count, n => ergebnis[n] = GrasKachelRechnen(liste[n].I, liste[n].J, hoehen[n], stadt[n], raster, frei, wasser, verbuende, flaechen, masken, stark, felsKarte));
        Ladezeit.Dazu("Gras: Kacheln rechnen (Parallel)", uhrGras);
        Pinselmessung.Dazu("  Gras: Kacheln rechnen (parallel)", messen);
        messen = Pinselmessung.Uhr();
        uhrGras = Ladezeit.Uhr();
        for (var n = 0; n < liste.Count; n++) GrasKachelSetzen(liste[n], ergebnis[n]);
        Ladezeit.Dazu("Gras: Kacheln setzen", uhrGras);
        Pinselmessung.Dazu("  Gras: Bilder setzen", messen);
        _grasBlickFuer = null;
        if (alles) GD.Print($"Grasteppich: {_grasKacheln.Count} Kacheln, {_grasKacheln.Values.Sum(k => k.Felder.Length)} Felder mit Gras, {uhr.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// Die Gebäude der Karte, unter denen kein Gras steht. In der Werkstatt und im Hauptmenü alle (dort steht auf jedem Platz ein Modell). Im Spiel
    /// (Schritt 5j) nur, was wirklich steht: Kulissen, Sonderbauten, Umland und die Werkstattplätze mit einem Betrieb oder einem leeren Haus –
    /// freie Werkstatt-, Wohnhaus- und Einwohnerplätze tragen Gras; die Häuser darauf stehen in <see cref="GrasSperrflaechen"/>. In der Mauer ist
    /// Stadtlehm, dort wächst ohnehin keins (auch nicht auf freien Plätzen, Entscheidung des Nutzers) – außer im Rasen der Restflächen (Stadtansicht.Rasen.cs).
    /// </summary>
    private Naturfreiraum? GrasFreiraum()
    {
        if (_entwurf is not null) return _entwurf.Freiraum(Modellmass);
        if (_karte is not { } karte) return null;
        if (_wirtschaft is null) return new Naturfreiraum(Array.Empty<(IReadOnlyList<double[]>, double)>(), GrasHindernisse(karte.Gebaeude), Array.Empty<IReadOnlyList<double[]>>(), Array.Empty<IReadOnlyList<double[]>>());
        var stehen = _betriebe.Keys.Select(b => b.Name).Concat(_leereHaeuser.Keys).Select(n => PlatzVon(n)?.Gebaeude).OfType<Kartengebaeude>().ToHashSet();
        var gebaeude = GrasHindernisse(karte.Gebaeude.Where(g => g.Art is not (Bausteinart.Werkstattplatz or Bausteinart.Wohnhausplatz or Bausteinart.Einwohnerplatz) || stehen.Contains(g))).ToList();
        return new Naturfreiraum(Array.Empty<(IReadOnlyList<double[]>, double)>(), gebaeude, Array.Empty<IReadOnlyList<double[]>>(), Array.Empty<IReadOnlyList<double[]>>());
    }

    /// <summary>
    /// Wo kaum Gras steht, obwohl Wiese gemalt ist (als gedrehte Rechtecke: Mitte, Drehung wie <c>Rotation.Y</c>, halbe Breite und Tiefe): im Spiel die
    /// Grundstücke (Häuser, Baustellen, neue Werkstätten – nicht der leere Bauplatz) mit ihrem Vorplatz und die Einwohnerhäuser. Das Gras endet dort weich
    /// (<see cref="GrasRandFaktor"/>). Die Äcker sind keine Rechtecke mehr, sie haben ihre Feldmaske (<see cref="_grasMasken"/>).
    /// </summary>
    private List<(Vector2 Mitte, float Drehung, Vector2 Halb)> GrasSperrflaechen()
    {
        var liste = new List<(Vector2, float, Vector2)>();
        if (_simulation?.Wirtschaft is { } w && w.Bauen.IstAktiv)
            foreach (var g in w.Bauen.Grundstuecke)
            {
                if (g.Zustand == Bauzustand.Bauplatz) continue;
                var gr = w.Bauen.Flaeche(g); // ein schräger Wohnhausplatz: wie der Platz, nicht das umschließende Rechteck
                liste.Add((new Vector2((float)gr.X, (float)gr.Z), DrehungY(gr), new Vector2((float)gr.Breite / 2, (float)gr.Tiefe / 2)));
            }
        if (_wirtschaft is not null)
            foreach (var p in _gebautePlaetze) liste.Add((new Vector2(p.Mitte.X, p.Mitte.Z), p.Drehung, p.Groesse / 2));
        return liste;
    }

    /// <summary>Breite (Meter) des weichen Übergangs am Rand von Gebäuden, Grundstücken und Höfen.</summary>
    private const float GrasRandBreite = 1.4f;

    /// <summary>
    /// Wie viel Gras am Rand eines Gebäudes oder Grundstücks steht, nach dem Abstand dazu (Meter; drinnen negativ): an der Wand fast nichts, nach
    /// <see cref="GrasRandBreite"/> Metern volle Dichte. Der Verlauf beginnt kurz drinnen, damit zwischen Mauer und Halmen keine kahle Fuge bleibt.
    /// </summary>
    private static float GrasRandFaktor(float abstand) => Mathf.SmoothStep(-0.15f, GrasRandBreite, abstand);

    /// <summary>Ein Rauschen für den Abstand (± 0,45 m, Wellen von gut 2 m): Der Rand franst aus, statt als gerade Linie zu laufen.</summary>
    private static float GrasRandRauschen(float x, float z) => (Rausch(x * 0.47f + 3.1f, z * 0.47f + 8.3f) - 0.5f) * 0.9f;

    /// <summary>
    /// Wie viel Gras am Feldrain steht: Wie im Acker-Shader (acker.gdshader) wird der Abstand zum Rand der Feldfläche mit einem Rauschen verschoben, ab dem
    /// der Acker die Zellen füllt (0 bis 1 m); das Gras nimmt im selben Maß ab. Am äußeren Rand des Ackers ist das Gras also noch voll da und mischt sich mit
    /// dem Randgras des Feldrains (<see cref="FeldPflanzenBauer.Feldrain"/>).
    /// </summary>
    private static float GrasFeldFaktor(Feldmaske[] masken, float x, float z)
    {
        var abstand = float.NegativeInfinity;
        foreach (var m in masken) abstand = Mathf.Max(abstand, m.Abstand(x, z));
        if (abstand < -1.2f) return 1f; // weit vom Feld (auch: außerhalb aller Raster)
        var rd = abstand + (Rausch(x * 0.35f + 11.2f, z * 0.35f + 5.7f) - 0.5f) * 0.9f + (Rausch(x * 0.8f + 2.5f, z * 0.8f + 9.1f) - 0.5f) * 0.35f;
        return 1f - Mathf.SmoothStep(0f, 1.1f, rd);
    }

    /// <summary>Das Wasser als Stücke der Flüsse und Bäche (mit halber Breite) und Umrisse der Seen – wie im Freiraum der Natur.</summary>
    private static (List<(Vector2 Von, Vector2 Bis, float Halb)> Fluesse, List<Vector2[]> Seen) GrasWasser(Wasserform? wasser)
    {
        if (wasser is not { IstLeer: false }) return (new(), new());
        static Vector2 V(double[] p) => new((float)p[0], (float)p[1]);
        return (Naturfreiraum.Flussstuecke(wasser).Select(f => (V(f.Linie[0]), V(f.Linie[1]), (float)f.Breite / 2)).ToList(),
                Naturfreiraum.Seeflaechen(wasser).Where(s => s.Count >= 3).Select(s => s.Select(V).ToArray()).ToList());
    }

    private static float AbstandZurStrecke(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = ab.LengthSquared() < 1e-6f ? 0f : Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    private static bool ImVieleck(Vector2 p, Vector2[] ecken)
    {
        var drin = false;
        for (int i = 0, j = ecken.Length - 1; i < ecken.Length; j = i++)
            if ((ecken[i].Y > p.Y) != (ecken[j].Y > p.Y) && p.X < (ecken[j].X - ecken[i].X) * (p.Y - ecken[i].Y) / (ecken[j].Y - ecken[i].Y) + ecken[i].X)
                drin = !drin;
        return drin;
    }

    /// <summary>
    /// Das Bild einer Kachel (läuft parallel, liest nur): je Punkt (1 m, wie die Gewichte des Bodens: 130 × 130 ab einem Punkt vor der Kachel) Dichte der
    /// Wiese, Höhe und Normale des Bodens; dazu die tiefste und höchste Stelle und die Felder mit Gras.
    /// </summary>
    private static (float[] Daten, byte[] Farbe, float Unten, float Oben, int[] Felder) GrasKachelRechnen(int ti, int tj, float[]? hoehen, byte[]? stadt, Bodenraster? raster,
        Naturfreiraum? frei, (List<(Vector2 Von, Vector2 Bis, float Halb)> Fluesse, List<Vector2[]> Seen) wasser, IReadOnlyList<Wasserverbund> verbuende,
        List<(Vector2 Mitte, float Drehung, Vector2 Halb)> flaechen, Feldmaske[] masken, bool stark, bool felsKarte)
    {
        const int n = BodenBild;
        var (i0, j0) = (ti * KachelZellen * 2 - 1, tj * KachelZellen * 2 - 1);
        // Höhe und Normale wie BodenHoehe (Dreiecke des Netzes) und Gelaendeform.Normale (Steigung in der Zelle), aus den Rasterpunkten der Kachel
        var (gi0, gj0) = (ti * KachelZellen - 1, tj * KachelZellen - 1);
        float H(int i, int j) => hoehen![(j - gj0) * GrasRaster + i - gi0];
        var splat = raster is { IstLeer: false } && BodenKachelGemalt(raster, ti, tj) ? raster.Splat(i0, j0, n, n, 0) : null;
        // Fels R5: Wo der Boden steinig ist (Felsnähe wie im Boden-Shader), stehen keine vollen Halme
        var naehe = splat is not null && felsKarte && raster is not null ? FelsnaeheRechnen(raster, i0, j0, n) : null;
        var daten = new float[n * n * 4];
        var farbe = new byte[n * n * 4];
        var (unten, oben) = (float.MaxValue, float.MinValue);
        for (var b = 0; b < n; b++)
            for (var a = 0; a < n; a++)
            {
                var (x, z) = ((float)(i0 + a), (float)(j0 + b));
                var o = (b * n + a) * 4;
                var (normale, h) = (Vector3.Up, 0f);
                if (hoehen is not null)
                {
                    var (fx, fz) = (x / (float)Hoehenraster.Abstand, z / (float)Hoehenraster.Abstand);
                    var (i, j) = ((int)MathF.Floor(fx), (int)MathF.Floor(fz));
                    var (tx, tz) = (fx - i, fz - j);
                    float h00 = H(i, j), h10 = H(i + 1, j), h01 = H(i, j + 1), h11 = H(i + 1, j + 1);
                    h = tx >= tz ? h00 + (h10 - h00) * tx + (h11 - h10) * tz : h00 + (h11 - h01) * tx + (h01 - h00) * tz;
                    var sx = ((h10 - h00) * (1 - tz) + (h11 - h01) * tz) / (float)Hoehenraster.Abstand;
                    var sz = ((h01 - h00) * (1 - tx) + (h11 - h10) * tx) / (float)Hoehenraster.Abstand;
                    normale = new Vector3(-sx, 1, -sz).Normalized();
                }
                var d = splat is null ? 1f : splat[o] / 255f; // Gruppe 0, R = Wiese
                d *= 1f - Mathf.SmoothStep(0.86f, 0.8f, normale.Y); // Fels am Hang (boden.gdshaderinc: ab etwa 33°)
                if (naehe is not null) d *= 1f - 0.9f * Mathf.SmoothStep(0.3f, 0.8f, naehe[b * n + a]); // steiniger Boden: Kies, Erde und Fels statt Wiese
                // Pflaster als Grund (Punkt k des groben Bildes liegt bei Kachel + k − 0,5 m, also gut einen halben Meter neben diesem): wie im Shader
                if (stadt is not null) d *= 1f - Mathf.SmoothStep(0.3f, 0.6f, stadt[(Math.Min(b, SchichtInfoBild - 1) * SchichtInfoBild + Math.Min(a, SchichtInfoBild - 1)) * 4 + 2] / 255f);
                if (d > 0f && frei is not null) d *= GrasRandFaktor((float)frei.GebaeudeAbstand(x, z, GrasRandBreite + 0.5) + GrasRandRauschen(x, z)); // Gebäude: das Gras dünnt zur Wand hin aus
                if (d > 0f && masken.Length > 0) d *= GrasFeldFaktor(masken, x, z); // Äcker: das Gras wächst bis in den Feldrain
                daten[o] = d;
                daten[o + 1] = h;
                daten[o + 2] = normale.X;
                daten[o + 3] = normale.Z;
                // Makro-Flecken der Wiese (wie makro_farbe im Shader, als Faktor auf ihre Farbe, halbiert) und die Horste – hier einmal statt in jeder Ecke
                var f = MakroFaktor(x, z, stark);
                farbe[o] = Byte(f.X * 127.5f);
                farbe[o + 1] = Byte(f.Y * 127.5f);
                farbe[o + 2] = Byte(f.Z * 127.5f);
                farbe[o + 3] = Byte(Mathf.SmoothStep(0.25f, 0.75f, Rausch(x * 0.33f + 4.7f, z * 0.33f + 4.7f)) * 255f);
                if (a > 0 && b > 0 && a < n - 1 && b < n - 1)
                {
                    unten = Mathf.Min(unten, h);
                    oben = Mathf.Max(oben, h);
                }
            }
        // Sperrflächen und Wasser: nur die Punkte in ihrem Rahmen prüfen (das ist viel schneller, als jeden Punkt nach allem zu fragen)
        void Loeschen(Vector2 von, Vector2 bis, Func<Vector2, bool> drin)
        {
            var (a0, a1) = (Math.Max(0, (int)MathF.Floor(von.X) - i0), Math.Min(n - 1, (int)MathF.Ceiling(bis.X) - i0));
            var (b0, b1) = (Math.Max(0, (int)MathF.Floor(von.Y) - j0), Math.Min(n - 1, (int)MathF.Ceiling(bis.Y) - j0));
            for (var b = b0; b <= b1; b++)
                for (var a = a0; a <= a1; a++)
                    if (daten[(b * n + a) * 4] > 0f && drin(new Vector2(i0 + a, j0 + b))) daten[(b * n + a) * 4] = 0f;
        }
        foreach (var (mitte, drehung, halb) in flaechen) // zurückgedreht in ihr Rechteck, das Gras dünnt am Rand aus
        {
            var r = halb.Length() + GrasRandBreite + 0.5f;
            var zurueck = new Basis(Vector3.Up, -drehung);
            var (a0, a1) = (Math.Max(0, (int)MathF.Floor(mitte.X - r) - i0), Math.Min(n - 1, (int)MathF.Ceiling(mitte.X + r) - i0));
            var (b0, b1) = (Math.Max(0, (int)MathF.Floor(mitte.Y - r) - j0), Math.Min(n - 1, (int)MathF.Ceiling(mitte.Y + r) - j0));
            for (var b = b0; b <= b1; b++)
                for (var a = a0; a <= a1; a++)
                {
                    var o = (b * n + a) * 4;
                    if (daten[o] <= 0f) continue;
                    var (px, pz) = ((float)(i0 + a), (float)(j0 + b));
                    var l = zurueck * new Vector3(px - mitte.X, 0, pz - mitte.Y);
                    var (ax, az) = (Mathf.Abs(l.X) - halb.X, Mathf.Abs(l.Z) - halb.Y);
                    var abstand = ax > 0 && az > 0 ? MathF.Sqrt(ax * ax + az * az) : Mathf.Max(ax, az);
                    daten[o] *= GrasRandFaktor(abstand + GrasRandRauschen(px, pz));
                }
        }
        foreach (var (von, bis, halb) in wasser.Fluesse) // Flüsse und Bäche als Stücke mit halber Breite
        {
            var r = halb + 0.3f;
            Loeschen(von.Min(bis) - new Vector2(r, r), von.Max(bis) + new Vector2(r, r), p => AbstandZurStrecke(p, von, bis) < r);
        }
        // Wasserverbund (Fluss und See als eine Fläche): Lücke und Kehle sind Wasser, liegen aber weder im Fluss noch im See – dort wuchsen sonst Halme im
        // Wasser (NAS 09.11.2026, Blick verbund_ecke). Wie beim Fluss 0,3 m Rand; der Kern rechnet je Punkt (liest nur, geht parallel)
        foreach (var v in verbuende)
            Loeschen(new Vector2((float)v.Bereich.X0, (float)v.Bereich.Z0), new Vector2((float)v.Bereich.X1, (float)v.Bereich.Z1),
                p => v.AbstandAm(p.X, p.Y) is { } s && s < 0.3);
        foreach (var see in wasser.Seen) // Seen und Bauzonen
        {
            var (von, bis) = (see[0], see[0]);
            foreach (var e in see) (von, bis) = (von.Min(e), bis.Max(e));
            // mit gut einem halben Meter Rand: Das Bild hat 1 m, und die Halme sollen nicht über den Rand einer Bauzone ragen
            const float r = 0.7f;
            Loeschen(von - new Vector2(1, 1), bis + new Vector2(1, 1), p => ImVieleck(p, see) || ImVieleck(p + new Vector2(r, 0), see) ||
                ImVieleck(p - new Vector2(r, 0), see) || ImVieleck(p + new Vector2(0, r), see) || ImVieleck(p - new Vector2(0, r), see));
        }
        // Felder mit Gras: irgendein Punkt im Feld oder einen Meter daneben mit genug Wiese (Punkt k liegt bei Kachel − 1 m + k)
        var felder = new List<int>();
        const int schritt = (int)GrasFeld;
        for (var fj = 0; fj < GrasFelder; fj++)
            for (var fi = 0; fi < GrasFelder; fi++)
            {
                var gras = false;
                for (var b = fj * schritt; b <= Math.Min(fj * schritt + schritt + 2, n - 1) && !gras; b++)
                    for (var a = fi * schritt; a <= Math.Min(fi * schritt + schritt + 2, n - 1); a++)
                        if (daten[(b * n + a) * 4] > 0.35f)
                        {
                            gras = true;
                            break;
                        }
                if (gras) felder.Add(fj * GrasFelder + fi);
            }
        return (daten, farbe, unten, oben, felder.ToArray());
    }

    /// <summary>
    /// Die Makro-Variation der Wiese an einem Ort als Faktor auf ihre Farbe: dieselbe Rechnung wie <c>makro_farbe</c> in <c>boden_gemein.gdshaderinc</c>
    /// (mit Wiese = 1), damit die Halme die Flecken des Bodens darunter tragen.
    /// </summary>
    private static Vector3 MakroFaktor(float x, float z, bool stark)
    {
        float R(float s, float v) => Rausch(x * s + v, z * s + v);
        var makro = R(0.011f, 0f) * 0.62f + R(0.037f, 7.3f) * 0.38f;
        var trocken = Mathf.SmoothStep(0.45f, 0.8f, R(0.021f, 11.7f));
        if (!stark)
        {
            var c0 = Vector3.One * Mathf.Lerp(0.86f, 1.1f, makro);
            return c0.Lerp(c0 * new Vector3(1.1f, 1.03f, 0.8f), trocken * 0.7f);
        }
        var c = Vector3.One * Mathf.Lerp(0.78f, 1.15f, makro);
        var mitte = R(0.13f, 3.1f);
        c *= new Vector3(0.78f, 0.86f, 0.74f).Lerp(new Vector3(1.13f, 1.1f, 0.9f), Mathf.SmoothStep(0.22f, 0.78f, mitte));
        return c.Lerp(c * new Vector3(1.14f, 1.04f, 0.74f), trocken * 0.85f);
    }

    /// <summary>Gibt einer Kachel das neue Bild (Stoff und Knoten beim ersten Mal); ohne Gras wird ihr Knoten entfernt.</summary>
    private void GrasKachelSetzen((int I, int J) kachel, (float[] Daten, byte[] Farbe, float Unten, float Oben, int[] Felder) e)
    {
        if (e.Felder.Length == 0)
        {
            if (_grasKacheln.Remove(kachel, out var weg) && IsInstanceValid(weg.Knoten)) weg.Knoten.QueueFree();
            return;
        }
        if (!_grasKacheln.TryGetValue(kachel, out var k))
        {
            var stoff = new ShaderMaterial { Shader = _grasShader, ResourceName = $"Gras_{kachel.I}_{kachel.J}" };
            stoff.SetShaderParameter("ursprung", new Vector2(kachel.I * KachelZellen * 2 - 1, kachel.J * KachelZellen * 2 - 1));
            stoff.SetShaderParameter("bildgroesse", (float)BodenBild);
            stoff.SetShaderParameter("wiese_farbe", _wiesenFarbe ?? new Color(0.33f, 0.4f, 0.19f));
            GrasStoffWerte(stoff);
            var knoten = new MultiMeshInstance3D
            {
                Name = $"Gras_{kachel.I}_{kachel.J}", MaterialOverride = stoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = _grasNetze![0] },
            };
            _grasWurzel!.AddChild(knoten);
            k = new GrasKachel { Stoff = stoff, Knoten = knoten };
            _grasKacheln[kachel] = k;
        }
        var bild = Image.CreateFromData(BodenBild, BodenBild, false, Image.Format.Rgbaf, MemoryMarshal.AsBytes(e.Daten.AsSpan()).ToArray());
        var farbbild = Image.CreateFromData(BodenBild, BodenBild, false, Image.Format.Rgba8, e.Farbe);
        if (k.Bild is null)
        {
            k.Bild = ImageTexture.CreateFromImage(bild);
            k.Farbe = ImageTexture.CreateFromImage(farbbild);
            k.Stoff.SetShaderParameter("grasbild", k.Bild);
            k.Stoff.SetShaderParameter("grasfarbe", k.Farbe);
        }
        else
        {
            k.Bild.Update(bild);
            k.Farbe!.Update(farbbild);
        }
        // die Wegeschicht der Kachel (Stadtansicht.Wegeschicht.cs)
        var wege = _schichtKacheln.TryGetValue(kachel, out var schicht);
        k.Stoff.SetShaderParameter("wege_an", wege);
        if (wege)
        {
            k.Stoff.SetShaderParameter("wege", schicht.Bild);
            k.Stoff.SetShaderParameter("wege_info", schicht.Info);
            k.Stoff.SetShaderParameter("wege_ursprung", new Vector2(kachel.I * KachelZellen * 2f, kachel.J * KachelZellen * 2f));
        }
        k.Felder = e.Felder;
        k.Daten = e.Daten;
        (k.Unten, k.Oben) = (e.Unten, e.Oben);
        // alle Felder als mögliche Kopien (gezeigt werden die ersten VisibleInstanceCount); der Boden ist bis zur Höhe der Halme darin
        var mm = k.Knoten.Multimesh;
        mm.VisibleInstanceCount = 0;
        mm.InstanceCount = e.Felder.Length;
        k.Puffer = new float[e.Felder.Length * 12];
        var x0 = kachel.I * KachelZellen * 2f;
        var z0 = kachel.J * KachelZellen * 2f;
        k.Knoten.CustomAabb = new Aabb(new Vector3(x0 - 2, e.Unten - 1, z0 - 2), new Vector3(KachelZellen * 2f + 4, e.Oben - e.Unten + 2.5f, KachelZellen * 2f + 4));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Felder im Bild
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wählt je Kachel die Felder im Bild (nur wenn sich Kamera, Zoom, Bildgröße oder eine Kachel geändert haben).</summary>
    private void GrasImBild()
    {
        var bildgroesse = GetViewport().GetVisibleRect().Size;
        var (dichte, hoechstens, _) = GrasWerte(Fenster.AktuellerGrasteppich);
        var ziel = Mathf.Min(dichte * (95f / _kamera.Size) * (95f / _kamera.Size), hoechstens);
        var netz = 0;
        while (netz < GrasNetzDichten.Length - 1 && GrasNetzDichten[netz] < ziel) netz++;
        var t = _kamera.GlobalTransform;
        var stand = (Ort: t.Origin.Snapped(Vector3.One * 0.5f), Blick: t.Basis.Z.Snapped(Vector3.One * 0.002f), Zoom: Mathf.Snapped(_kamera.Size, 0.25f), Bild: bildgroesse, Netz: netz, Pixel: Fenster.PixelLookAn);
        if (_grasBlickFuer == stand) return;
        _grasBlickFuer = stand;
        var ecken = new[] { Vector2.Zero, new Vector2(bildgroesse.X, 0), bildgroesse, new Vector2(0, bildgroesse.Y) };
        var strahlen = ecken.Select(e => (Von: _kamera.ProjectRayOrigin(e), Richtung: _kamera.ProjectRayNormal(e))).ToArray();
        if (strahlen.Any(s => s.Richtung.Y > -0.05f)) return; // die Kamera schaut nie flacher (40°), nur zur Sicherheit
        const float kachelLaenge = KachelZellen * 2f;
        foreach (var ((ti, tj), k) in _grasKacheln)
        {
            var mm = k.Knoten.Multimesh;
            if (mm.Mesh != _grasNetze![netz]) mm.Mesh = _grasNetze[netz];
            // das Bild auf dem Boden: die Ecken auf der tiefsten und der höchsten Stelle der Kachel (samt Halmen), die Hülle darum
            var punkte = new List<Vector2>(8);
            foreach (var y in new[] { k.Unten - 0.5f, k.Oben + 0.8f })
                foreach (var (von, richtung) in strahlen)
                {
                    var p = von + richtung * ((y - von.Y) / richtung.Y);
                    punkte.Add(new Vector2(p.X, p.Z));
                }
            var huelle = Geometry2D.ConvexHull(punkte.ToArray());
            var (x0, z0) = (ti * kachelLaenge, tj * kachelLaenge);
            var anzahl = 0;
            if (Rahmen(huelle).Grow(GrasFeld).Intersects(new Rect2(x0, z0, kachelLaenge, kachelLaenge)))
                foreach (var f in k.Felder)
                {
                    var (fx, fz) = (x0 + f % GrasFelder * GrasFeld, z0 + f / GrasFelder * GrasFeld);
                    if (!InHuelle(huelle, new Vector2(fx + GrasFeld / 2, fz + GrasFeld / 2), GrasFeld * 0.75f + 0.6f)) continue;
                    var o = anzahl * 12;
                    k.Puffer[o] = 1f;
                    k.Puffer[o + 3] = fx;
                    k.Puffer[o + 5] = 1f;
                    k.Puffer[o + 10] = 1f;
                    k.Puffer[o + 11] = fz;
                    anzahl++;
                }
            if (anzahl > 0) mm.Buffer = k.Puffer;
            mm.VisibleInstanceCount = anzahl;
        }
    }

    private static Rect2 Rahmen(Vector2[] punkte)
    {
        var r = new Rect2(punkte[0], Vector2.Zero);
        foreach (var p in punkte) r = r.Expand(p);
        return r;
    }

    /// <summary>Liegt der Punkt in der konvexen Hülle, mit <paramref name="rand"/> Metern Zugabe? (Die Hülle kann geschlossen sein: letzter = erster Punkt.)</summary>
    private static bool InHuelle(Vector2[] huelle, Vector2 p, float rand)
    {
        var flaeche = 0f;
        for (var i = 0; i < huelle.Length; i++) flaeche += huelle[i].Cross(huelle[(i + 1) % huelle.Length]);
        var richtung = Mathf.Sign(flaeche);
        for (var i = 0; i < huelle.Length; i++)
        {
            var (a, b) = (huelle[i], huelle[(i + 1) % huelle.Length]);
            var kante = b - a;
            var laenge = kante.Length();
            if (laenge < 1e-4f) continue;
            if (richtung * kante.Cross(p - a) / laenge < -rand) return false;
        }
        return true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Netze
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Netze eines Feldes (4 × 4 m), je <see cref="GrasNetzDichten"/> eins. Die Büschel folgen einer Folge, die in jedem Anfangsstück gleichmäßig
    /// verteilt ist (R2-Folge, nach Roberts) – ein Netz ist der Anfang des nächsten, und der Shader kann weit weg Büschel vom Ende her weglassen, ohne
    /// Lücken. Ein Büschel ist ein Kranz aus drei oder vier Halmen: Die Fußpunkte liegen auf einem Kreis (Halbmesser 1, der Shader skaliert ihn) und
    /// gehören je zwei Nachbarhalmen, die Spitze steht über der Mitte dazwischen und neigt sich nach außen. Ein Dreieck je Halm mit Index – die
    /// Grafikkarte rechnet so nur zwei Ecken je Halm statt drei (die Zahl der Ecken bestimmt, was der Teppich kostet). Aufbau: <c>gras.gdshader</c>.
    /// </summary>
    private static ArrayMesh[] GrasNetze()
    {
        var anzahl = (int)(GrasNetzDichten[^1] * GrasFeld * GrasFeld);
        var ecken = new List<Vector3>();
        var normalen = new List<Vector3>();
        var uv = new List<Vector2>();
        var uv2 = new List<Vector2>();
        var indizes = new List<int>();
        var bis = new int[anzahl + 1]; // Indizes bis zum Büschel
        var zufall = new Random(51);
        float Z() => (float)zufall.NextDouble();
        for (var i = 0; i < anzahl; i++)
        {
            var wurzel = new Vector2(Mathf.PosMod(0.5f + i * 0.7548776662f, 1f), Mathf.PosMod(0.5f + i * 0.5698402910f, 1f)) * GrasFeld;
            var rang = (i + 0.5f) / (GrasFeld * GrasFeld); // ab dieser Dichte (Büschel je m²) steht es
            var halme = 3;
            var drehung = Z() * Mathf.Tau;
            var anfang = ecken.Count;
            void Ecke(Vector2 ort, float y, Vector2 richtung, float neigung)
            {
                ecken.Add(new Vector3(wurzel.X + ort.X, y, wurzel.Y + ort.Y));
                normalen.Add(new Vector3(richtung.X, 0, richtung.Y));
                uv.Add(new Vector2(neigung, rang));
                uv2.Add(wurzel);
            }
            var winkel = Enumerable.Range(0, halme).Select(j => drehung + Mathf.Tau * (j + 0.35f * (Z() - 0.5f)) / halme).ToArray();
            foreach (var w in winkel)
            {
                var r = new Vector2(Mathf.Cos(w), Mathf.Sin(w));
                Ecke(r * (0.8f + 0.4f * Z()), 0f, r, 0f); // Fußpunkte
            }
            for (var j = 0; j < halme; j++)
            {
                var (a, b) = (ecken[anfang + j], ecken[anfang + (j + 1) % halme]);
                var mitte = new Vector2((a.X + b.X) / 2 - wurzel.X, (a.Z + b.Z) / 2 - wurzel.Y);
                var aussen = mitte.Normalized().Rotated((Z() - 0.5f) * 0.6f);
                Ecke(mitte, 0.65f + 0.35f * Z(), aussen, 0.15f + 0.3f * Z()); // Spitze
                indizes.AddRange(new[] { anfang + j, anfang + (j + 1) % halme, anfang + halme + j });
            }
            bis[i + 1] = indizes.Count;
        }
        var netze = new ArrayMesh[GrasNetzDichten.Length];
        for (var s = 0; s < netze.Length; s++)
        {
            var ende = bis[(int)(GrasNetzDichten[s] * GrasFeld * GrasFeld)];
            var teil = indizes.GetRange(0, ende).ToArray();
            var eckenBis = teil.Max() + 1;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = ecken.GetRange(0, eckenBis).ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = normalen.GetRange(0, eckenBis).ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = uv.GetRange(0, eckenBis).ToArray();
            arrays[(int)Mesh.ArrayType.TexUV2] = uv2.GetRange(0, eckenBis).ToArray();
            arrays[(int)Mesh.ArrayType.Index] = teil;
            netze[s] = new ArrayMesh { ResourceName = $"Grasfeld_{s}" };
            netze[s].AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        }
        return netze;
    }
}
