using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Der Belag der Wege (Kartenwerkstatt, Schritt 5d): Trampelpfad, Fahrweg mit Spurrillen, Kies und Pflaster, gezeichnet von <c>wegbelag.gdshader</c>.
/// <list type="bullet">
/// <item><b>Ein Band je Weg</b> (<see cref="BelagBauen"/>): alle 1 m ein Schnitt quer über den Weg; jeder Schnitt trägt die Gewichte der vier Stufen,
/// Austritt und Zustand (Vertexdaten). An den Grenzen der Abschnitte gehen die Stufen auf 3 m ineinander über. Der Rand läuft weich ins
/// Gelände aus – darum liegt unter dem Belag kein zweites, braunes Band. Brücken (<see cref="WegLuecken"/>) und der Marktplatz bleiben frei.</item>
/// <item><b>Baustellen:</b> aufgewühlte Erde (zweite Fläche des Netzes) mit Absperrung an beiden Enden und Haufen von Kies und Steinen.</item>
/// <item><b>Im Spiel</b> kommen die Stücke aus <c>Stadtwirtschaft.Wegebau</c> (alle 5 m Stufe, Austritt, Zustand); ändert sich der Belag
/// (<c>BelagGeaendert</c>), werden nur die Wege neu gezeichnet, deren Stücke sich geändert haben. In der Kartenwerkstatt zeichnet jeder Weg seine
/// Abschnitte (<c>EntwurfWeg.BelagAbschnitte</c>).</item>
/// <item><b>Anklicken</b> (<see cref="WegAngeklickt"/>): Ein Klick auf freien Boden neben keinem Gebäude fragt <c>Wegebau.WegstelleBei</c>; die Tafel
/// markiert den gewählten Abschnitt (<see cref="WegAuswahlMarkieren"/>).</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Der Belag liegt 1,2 cm über dem Band der Wege (<see cref="WegHoehe"/>).</summary>
    private const float BelagHoehe = WegHoehe + 0.012f;

    /// <summary>Abstand der Schnitte (Meter) und Länge (Meter) der Übergänge zwischen zwei Abschnitten, je zur Hälfte auf jeder Seite.</summary>
    private const float BelagSchritt = 1f, BelagUebergang = 1.5f;

    private static Shader? _belagShader;
    private ShaderMaterial? _belagStoff, _baustellenStoff;

    /// <summary>Die gezeichneten Wege des Spiels: Knoten und Kennzeichen der Stücke (nur geänderte Wege werden neu gezeichnet).</summary>
    private readonly Dictionary<int, (Node3D Knoten, string Zeichen)> _belagWege = new();
    private bool _belagVeraltet;
    private Node3D? _wegAuswahlKnoten;

    /// <summary>Ein Klick auf einen Weg: Nummer in <c>Karte.Wege</c> und Stelle (Meter entlang des Verlaufs).</summary>
    public event Action<int, double>? WegAngeklickt;

    private ShaderMaterial BelagStoff(bool baustelle)
    {
        _belagShader ??= GD.Load<Shader>("res://assets/shader/wegbelag.gdshader");
        if (baustelle) return _baustellenStoff ??= NeuerBelagStoff(1f, "Wegbaustelle");
        return _belagStoff ??= NeuerBelagStoff(0f, "Wegbelag");
    }

    private ShaderMaterial NeuerBelagStoff(float baustelle, string name)
    {
        var stoff = new ShaderMaterial { Shader = _belagShader, ResourceName = name };
        stoff.SetShaderParameter("baustelle", baustelle);
        return stoff;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Zeichnen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Hat die Ansicht einen Belag zu zeichnen? (Der Shader fehlt nur in einem kaputten Export.)</summary>
    private static bool BelagMoeglich => ResourceLoader.Exists("res://assets/shader/wegbelag.gdshader");

    /// <summary>
    /// Die Stücke, die ein Weg des Spiels zeigt: die 5-m-Stücke des Wegebaus, ohne Wegebau (alte Karte ohne Abschnitt <c>wegebau</c>) der Startbelag der Karte.
    /// </summary>
    private IReadOnlyList<Belagabschnitt> BelagStuecke(int weg) =>
        _wirtschaft?.Wegebau is { IstAktiv: true } bau && bau.StueckZahl(weg) > 0 ? bau.Stuecke(weg)
        : _karte is { } k && weg >= 0 && weg < k.Wege.Count ? Wegbelag.Startabschnitte(k.Wege[weg].Klasse, k.Wege[weg].Laenge, k.Wege[weg].Belag)
        : Array.Empty<Belagabschnitt>();

    private static string BelagZeichen(IReadOnlyList<Belagabschnitt> stuecke, IEnumerable<Wegbaustelle> baustellen) =>
        string.Join("|", stuecke.Select(s => $"{(int)s.Stufe}:{(int)Math.Round(s.Austritt * 20)}:{(int)Math.Round(s.Zustand * 20)}:{(s.Baustelle ? 1 : 0)}"))
        + "#" + string.Join("|", baustellen.Select(b => $"{b.Von:0.#}-{b.Bis:0.#}"));

    /// <summary>Zeichnet den Belag eines Weges des Spiels in einen eigenen Knoten (<c>_belagWege</c>); ein vorhandener wird ersetzt.</summary>
    private void WegBelagZeichnen(int nummer, IReadOnlyList<Vector3> verlauf, float breite, bool neuAuchGleich = false)
    {
        var stuecke = BelagStuecke(nummer);
        var baustellen = _wirtschaft?.Wegebau.Baustellen.Where(b => b.Weg == nummer).ToList() ?? new List<Wegbaustelle>();
        var zeichen = BelagZeichen(stuecke, baustellen);
        if (!neuAuchGleich && _belagWege.TryGetValue(nummer, out var alt) && alt.Zeichen == zeichen) return;
        if (_belagWege.Remove(nummer, out var weg)) KnotenEntfernen(weg.Knoten);
        var knoten = BelagBauen(verlauf, stuecke, breite, this, WegLuecken(nummer), WegBoden(nummer), baustellen.Select(b => (b.Von, b.Bis)).ToList());
        _belagWege[nummer] = (knoten, zeichen);
    }

    /// <summary>Der Belag hat sich geändert (Ereignis des Wegebaus): beim nächsten Bild die geänderten Wege neu zeichnen und die Fahrtzeiten der Karren neu verteilen.</summary>
    private void BelagVeraltetMelden()
    {
        _belagVeraltet = true;
    }

    /// <summary>Jedes Bild (aus <see cref="_Process"/>): ist der Belag veraltet, werden die Wege mit geänderten Stücken neu gezeichnet.</summary>
    private void BelagBild()
    {
        if (!_belagVeraltet || _karte is not { } karte) return;
        _belagVeraltet = false;
        _fahrtprofile.Clear(); // das Tempo der Karren hängt am Belag
        for (var i = 0; i < karte.Wege.Count; i++)
        {
            var w = karte.Wege[i];
            var punkte = w.Verlauf.Where(p => p is { Length: 2 }).Select(Punkt3).ToArray();
            if (punkte.Length >= 2) WegBelagZeichnen(i, punkte, (float)w.BreiteWirksam);
        }
    }

    /// <summary>
    /// Baut das Band eines Weges: <paramref name="verlauf"/> (die Punkte des Verlaufs), <paramref name="stuecke"/> (lückenlos von 0 bis zur Länge),
    /// <paramref name="luecken"/> (Meter ohne Band: Brücken), <paramref name="baustellen"/> (Meter: Absperrung und Haufen).
    /// </summary>
    private Node3D BelagBauen(IReadOnlyList<Vector3> verlauf, IReadOnlyList<Belagabschnitt> stuecke, float breite, Node3D eltern,
        List<(float Von, float Bis)>? luecken, Func<float, float, float>? boden, IReadOnlyList<(double Von, double Bis)>? baustellen)
    {
        var knoten = new Node3D { Name = "Belag" };
        eltern.AddChild(knoten);
        var roh = verlauf.Select(p => new Vector2(p.X, p.Z)).ToList();
        roh = roh.Where((p, i) => i == 0 || p.DistanceTo(roh[i - 1]) > 0.01f).ToList();
        if (roh.Count < 2 || stuecke.Count == 0) return knoten;
        var linie = Verfeinert(roh, BelagSchritt);
        var stelle = new float[linie.Count];
        for (var i = 1; i < linie.Count; i++) stelle[i] = stelle[i - 1] + linie[i].DistanceTo(linie[i - 1]);

        // Ränder an den Knicken auf der Winkelhalbierenden (wie BandDreiecke), die Breite etwas größer: der Rand läuft weich aus
        var halb = breite / 2 + 0.25f;
        var links = new Vector2[linie.Count];
        var rechts = new Vector2[linie.Count];
        for (var i = 0; i < linie.Count; i++)
        {
            var vor = i > 0 ? (linie[i] - linie[i - 1]).Normalized() : Vector2.Zero;
            var nach = i + 1 < linie.Count ? (linie[i + 1] - linie[i]).Normalized() : Vector2.Zero;
            var richtung = (vor + nach).Normalized();
            if (richtung == Vector2.Zero) richtung = nach != Vector2.Zero ? nach : vor != Vector2.Zero ? vor : Vector2.Right;
            var quer = new Vector2(-richtung.Y, richtung.X);
            var bezug = nach != Vector2.Zero ? nach : vor;
            var gehrung = Mathf.Min(3, 1 / Mathf.Max(0.33f, Mathf.Abs(quer.Dot(new Vector2(-bezug.Y, bezug.X)))));
            links[i] = linie[i] + quer * halb * gehrung;
            rechts[i] = linie[i] - quer * halb * gehrung;
        }

        // Werte je Schnitt
        var gewichte = new Color[linie.Count];
        var austritt = new Vector2[linie.Count];
        var bau = new bool[linie.Count];
        for (var i = 0; i < linie.Count; i++) (gewichte[i], austritt[i], bau[i]) = BelagBei(stuecke, stelle[i], halb);

        var hoehe = boden ?? (BodenBewegt ? BodenHoehe : (_, _) => 0f);
        Vector3 Ecke(Vector2 p) => new(p.X, (BodenBewegt ? hoehe(p.X, p.Y) : 0f) + BelagHoehe, p.Y);
        var flaechen = new[] { new BelagFlaeche(), new BelagFlaeche() };
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            var mitte = (stelle[i] + stelle[i + 1]) / 2;
            if (luecken is not null && luecken.Any(l => mitte > l.Von && mitte < l.Bis)) continue;
            var a3 = new Vector3(linie[i].X, 0, linie[i].Y);
            var b3 = new Vector3(linie[i + 1].X, 0, linie[i + 1].Y);
            if (OhneMarktplatz(a3, b3, breite) is { } rest && rest.Sum(r => r.Von.DistanceTo(r.Bis)) < a3.DistanceTo(b3) * 0.5f) continue; // auf dem Marktplatz liegt das Pflaster des Modells
            var f = flaechen[bau[i] && bau[i + 1] ? 1 : 0];
            var (la, ra, lb, rb) = (links[i], rechts[i], links[i + 1], rechts[i + 1]);
            // zwei Dreiecke, Vorderseite oben; UV.y = Abstand von der Mitte (links +)
            f.Viereck(Ecke(la), Ecke(ra), Ecke(lb), Ecke(rb), BodenNormale(linie[i].X, linie[i].Y), BodenNormale(linie[i + 1].X, linie[i + 1].Y),
                (stelle[i], stelle[i + 1]), halb, (gewichte[i], gewichte[i + 1]), (austritt[i], austritt[i + 1]));
        }

        var netz = new ArrayMesh();
        var stoffe = new List<ShaderMaterial>();
        foreach (var (f, baustelle) in new[] { (flaechen[0], false), (flaechen[1], true) })
        {
            if (f.Ecken.Count == 0) continue;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = f.Ecken.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = f.Normalen.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = f.Farben.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = f.Uv.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV2] = f.Uv2.ToArray();
            netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            stoffe.Add(BelagStoff(baustelle));
        }
        for (var s = 0; s < stoffe.Count; s++) netz.SurfaceSetMaterial(s, stoffe[s]);
        knoten.AddChild(new MeshInstance3D { Name = "Band", Mesh = netz, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        if (baustellen is not null)
            foreach (var (von, bis) in baustellen) BaustelleAufstellen(knoten, linie, stelle, (float)von, (float)bis, breite, hoehe);
        return knoten;
    }

    /// <summary>Gewichte der Stufen (r Trampelpfad, g Fahrweg, b Kies; Pflaster = Rest, a = halbe Breite ÷ 8), Austritt/Zustand und Baustelle an einer Stelle; an Grenzen weich.</summary>
    private static (Color Gewichte, Vector2 Austritt, bool Baustelle) BelagBei(IReadOnlyList<Belagabschnitt> stuecke, float s, float halbBreite)
    {
        var i = stuecke.Count - 1;
        for (var k = 0; k < stuecke.Count; k++)
            if (s < stuecke[k].Bis) { i = k; break; }
        var (hier, nachbar, anteil) = (stuecke[i], -1, 0f);
        if (s - (float)hier.Von < BelagUebergang && i > 0) (nachbar, anteil) = (i - 1, 0.5f * (1 - (s - (float)hier.Von) / BelagUebergang));
        else if ((float)hier.Bis - s < BelagUebergang && i + 1 < stuecke.Count) (nachbar, anteil) = (i + 1, 0.5f * (1 - ((float)hier.Bis - s) / BelagUebergang));
        var farbe = new Color(0, 0, 0, halbBreite / 8f);
        void Zu(Belagabschnitt b, float gewicht)
        {
            switch (b.Stufe)
            {
                case Belagstufe.Trampelpfad: farbe.R += gewicht; break;
                case Belagstufe.Fahrweg: farbe.G += gewicht; break;
                case Belagstufe.Kies: farbe.B += gewicht; break;
            }
        }
        Zu(hier, 1 - anteil);
        var (a, z) = ((float)hier.Austritt * (1 - anteil), (float)hier.Zustand * (1 - anteil));
        if (nachbar >= 0)
        {
            Zu(stuecke[nachbar], anteil);
            a += (float)stuecke[nachbar].Austritt * anteil;
            z += (float)stuecke[nachbar].Zustand * anteil;
        }
        return (farbe, new Vector2(a, z), hier.Baustelle);
    }

    /// <summary>Sammelt die Dreiecke einer Fläche des Belagnetzes.</summary>
    private sealed class BelagFlaeche
    {
        public readonly List<Vector3> Ecken = new(), Normalen = new();
        public readonly List<Color> Farben = new();
        public readonly List<Vector2> Uv = new(), Uv2 = new();

        public void Viereck(Vector3 la, Vector3 ra, Vector3 lb, Vector3 rb, Vector3 na, Vector3 nb, (float A, float B) stelle, float halb,
            (Color A, Color B) farbe, (Vector2 A, Vector2 B) austritt)
        {
            // (la, ra, lb) und (ra, rb, lb): gegen den Uhrzeigersinn von oben gesehen, wenn die Linie nach +z läuft und „links“ bei −x liegt
            foreach (var (p, n, s, q, f, a) in new[]
            {
                (la, na, stelle.A, halb, farbe.A, austritt.A), (lb, nb, stelle.B, halb, farbe.B, austritt.B), (ra, na, stelle.A, -halb, farbe.A, austritt.A),
                (ra, na, stelle.A, -halb, farbe.A, austritt.A), (lb, nb, stelle.B, halb, farbe.B, austritt.B), (rb, nb, stelle.B, -halb, farbe.B, austritt.B),
            })
            {
                Ecken.Add(p);
                Normalen.Add(n);
                Farben.Add(f);
                Uv.Add(new Vector2(s, q));
                Uv2.Add(a);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Baustelle
    // ═════════════════════════════════════════════════════════════════════════

    private static readonly Color Absperrholz = new(0.46f, 0.34f, 0.20f), Absperrweiss = new(0.86f, 0.82f, 0.72f), Absperrrot = new(0.62f, 0.20f, 0.16f);
    private static readonly Color Kieshaufen = new(0.66f, 0.61f, 0.52f), Steinstapel = new(0.52f, 0.50f, 0.47f);

    private static Vector3 PunktBei(IReadOnlyList<Vector2> linie, IReadOnlyList<float> stelle, float s, out Vector2 richtung)
    {
        var i = 0;
        while (i + 2 < linie.Count && stelle[i + 1] < s) i++;
        var laenge = Mathf.Max(0.001f, stelle[i + 1] - stelle[i]);
        var t = Mathf.Clamp((s - stelle[i]) / laenge, 0, 1);
        richtung = (linie[i + 1] - linie[i]).Normalized();
        var p = linie[i].Lerp(linie[i + 1], t);
        return new Vector3(p.X, 0, p.Y);
    }

    /// <summary>Absperrung an beiden Enden des Abschnitts (zwei Pfosten, zwei Latten) und Haufen aus Kies und Steinen dazwischen.</summary>
    private void BaustelleAufstellen(Node3D eltern, IReadOnlyList<Vector2> linie, IReadOnlyList<float> stelle, float von, float bis, float breite, Func<float, float, float> hoehe)
    {
        var wurzel = new Node3D { Name = "Baustelle" };
        eltern.AddChild(wurzel);
        float Y(Vector3 p) => (BodenBewegt ? hoehe(p.X, p.Z) : 0f) + WegHoehe;
        MeshInstance3D Quader(Vector3 mass, Color farbe, Vector3 ort, float gier)
        {
            var m = new MeshInstance3D { Mesh = new BoxMesh { Size = mass }, MaterialOverride = Material(farbe), Position = ort, Rotation = new Vector3(0, gier, 0) };
            wurzel.AddChild(m);
            return m;
        }
        foreach (var s in new[] { von, bis })
        {
            var p = PunktBei(linie, stelle, s, out var r);
            var gier = Mathf.Atan2(r.Y, r.X) * -1f + Mathf.Pi / 2; // quer zur Laufrichtung
            var seite = new Vector3(-r.Y, 0, r.X);
            var halb = breite / 2f;
            var grund = Y(p);
            foreach (var vorzeichen in new[] { -1f, 1f })
            {
                var ort = p + seite * halb * vorzeichen;
                Quader(new Vector3(0.12f, 1.1f, 0.12f), Absperrholz, new Vector3(ort.X, Y(ort) + 0.55f, ort.Z), gier);
            }
            Quader(new Vector3(breite, 0.14f, 0.04f), Absperrrot, new Vector3(p.X, grund + 0.95f, p.Z), gier);
            Quader(new Vector3(breite, 0.14f, 0.04f), Absperrweiss, new Vector3(p.X, grund + 0.70f, p.Z), gier);
        }
        // Haufen: Kies (gedrückte Kugeln) und Steinstapel, verteilt zwischen den Absperrungen
        var anzahl = Mathf.Clamp(Mathf.CeilToInt((bis - von) / 10f), 1, 6);
        for (var k = 0; k < anzahl; k++)
        {
            var s = von + (bis - von) * (k + 0.5f) / anzahl;
            var p = PunktBei(linie, stelle, s, out var r);
            var seite = new Vector3(-r.Y, 0, r.X) * (breite * 0.30f * (k % 2 == 0 ? 1 : -1));
            var ort = p + seite;
            if (k % 2 == 0)
            {
                var haufen = new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = 0.55f, Height = 1.1f, RadialSegments = 12, Rings = 6 }, MaterialOverride = Material(Kieshaufen),
                    Position = new Vector3(ort.X, Y(ort) + 0.12f, ort.Z), Scale = new Vector3(1.2f, 0.45f, 1f),
                };
                wurzel.AddChild(haufen);
            }
            else
                for (var i = 0; i < 3; i++)
                    Quader(new Vector3(0.5f, 0.28f, 0.34f), Steinstapel, new Vector3(ort.X, Y(ort) + 0.14f + i * 0.28f, ort.Z), 0.35f * i);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Anklicken und Markieren
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Wo trifft der Strahl durch diese Bildschirmstelle den Boden? (Eine Ebene, auf Gelände dreimal der Höhe des Bodens nachgeführt.)</summary>
    private Vector3? BodenPunktBei(Vector2 bildschirm)
    {
        var kamera = _kamera;
        var von = kamera.ProjectRayOrigin(bildschirm);
        var richtung = kamera.ProjectRayNormal(bildschirm);
        if (Mathf.Abs(richtung.Y) < 1e-4f) return null;
        var y = 0f;
        var p = von;
        for (var i = 0; i < 4; i++)
        {
            var t = (y - von.Y) / richtung.Y;
            p = von + richtung * t;
            y = BodenHoehe(p.X, p.Z);
        }
        return new Vector3(p.X, y, p.Z);
    }

    /// <summary>Welcher Weg liegt unter dem Punkt? Nummer und Stelle – null, wenn keiner nah genug ist (halbe Breite plus 0,5 m).</summary>
    private (int Weg, double Stelle)? WegBeiPunkt(Vector3 p)
    {
        if (_wirtschaft?.Wegebau is not { IstAktiv: true } bau || _karte is not { } karte) return null;
        if (bau.WegstelleBei(p.X, p.Z, 8) is not var (weg, stelle, abstand)) return null;
        if (abstand > karte.Wege[weg].BreiteWirksam / 2 + 0.5) return null;
        return (weg, stelle);
    }

    /// <summary>Der Ort einer Stelle auf einem Weg (x, z in Metern) – null bei einer falschen Nummer.</summary>
    public Vector2? WegOrt(int weg, double stelle)
    {
        if (_karte is not { } k || weg < 0 || weg >= k.Wege.Count) return null;
        var p = Wegbelag.Punkt(k.Wege[weg].Verlauf, stelle);
        return new Vector2((float)p.X, (float)p.Z);
    }

    /// <summary>Für den Klicktest (<c>--auswahl=weg:&lt;nr&gt;</c>): die Mitte eines Weges.</summary>
    public (int Weg, double Stelle)? WegMitte(int weg) =>
        _karte is { } k && weg >= 0 && weg < k.Wege.Count ? (weg, k.Wege[weg].Laenge / 2) : null;

    /// <summary>Markiert den gewählten Abschnitt eines Weges mit einem goldenen Band (<paramref name="weg"/> −1 oder von ≥ bis: Markierung weg).</summary>
    public void WegAuswahlMarkieren(int weg, double von, double bis)
    {
        if (_karte is not { } karte || weg < 0 || weg >= karte.Wege.Count)
        {
            WegStreckeMarkieren(null, 0, 0, 0);
            return;
        }
        WegStreckeMarkieren(karte.Wege[weg].Verlauf.Where(p => p is { Length: 2 }).Select(p => new Vector2((float)p[0], (float)p[1])).ToList(), (float)karte.Wege[weg].BreiteWirksam, von, bis);
    }

    /// <summary>Markiert einen Abschnitt einer Linie (Meter entlang) mit einem goldenen Band – auch für einen Weg des Kartenentwurfs; <paramref name="linie"/> null: Markierung weg.</summary>
    public void WegStreckeMarkieren(IReadOnlyList<Vector2>? linie, float breite, double von, double bis)
    {
        if (_wegAuswahlKnoten is not null)
        {
            KnotenEntfernen(_wegAuswahlKnoten);
            _wegAuswahlKnoten = null;
        }
        if (linie is null || linie.Count < 2 || bis <= von) return;
        var teil = Verfeinert(linie, 1f);
        var punkte = new List<Vector2>();
        var s = 0f;
        for (var i = 0; i < teil.Count; i++)
        {
            if (i > 0) s += teil[i].DistanceTo(teil[i - 1]);
            if (s >= von && s <= bis) punkte.Add(teil[i]);
        }
        if (punkte.Count < 2) return;
        var band = DurchsichtigesNetz(BandDreiecke(punkte, breite / 2 + 0.4f, 0.2f), Auswahlgold, 0.30f);
        ((StandardMaterial3D)band.MaterialOverride).RenderPriority = 3; // über dem Belag (beide durchscheinend)
        _wegAuswahlKnoten = new Node3D { Name = "WegAuswahl" };
        _wegAuswahlKnoten.AddChild(band);
        AddChild(_wegAuswahlKnoten);
    }

    /// <summary>Der Name eines Weges für Tafeln: die Bezeichnung aus der Karte, sonst die Klasse („Landstraße“).</summary>
    public string WegName(int weg) =>
        _karte is { } k && weg >= 0 && weg < k.Wege.Count ? (k.Wege[weg].Name != "" ? k.Wege[weg].Name : Kartenweg.Anzeigename(k.Wege[weg].Klasse)) : "Weg";
}
