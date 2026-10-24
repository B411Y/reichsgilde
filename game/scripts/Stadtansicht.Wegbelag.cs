using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Der Belag der Wege (Kartenwerkstatt, Schritt 5d): Trampelpfad, Fahrweg mit Spurrillen, Kies und Pflaster. Seit Schritt 5g ist er in den Boden
/// eingebacken (Stadtansicht.Wegeschicht.cs, <c>boden.gdshader</c>); hier bleibt, was auf dem Weg steht und wie man ihn anklickt:
/// <list type="bullet">
/// <item><b>Im Spiel</b> kommen die Stücke aus <c>Stadtwirtschaft.Wegebau</c> (alle 5 m Stufe, Austritt, Zustand); ändert sich der Belag
/// (<c>BelagGeaendert</c>), werden nur die Kacheln der Wege neu gerechnet, deren Stücke sich geändert haben. In der Kartenwerkstatt rechnet jeder Weg
/// seine Abschnitte (<c>EntwurfWeg.BelagAbschnitte</c>).</item>
/// <item><b>Baustellen:</b> Der Boden ist aufgewühlt (Stufe 4 der Schicht), dazu Absperrung an beiden Enden und Haufen von Kies und Steinen.</item>
/// <item><b>Anklicken</b> (<see cref="WegAngeklickt"/>): Ein Klick auf freien Boden neben keinem Gebäude fragt <c>Wegebau.WegstelleBei</c>; die Tafel
/// markiert den gewählten Abschnitt (<see cref="WegAuswahlMarkieren"/>).</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Länge (Meter) der Übergänge zwischen zwei Abschnitten, je zur Hälfte auf jeder Seite.</summary>
    private const float BelagUebergang = 1.5f;

    /// <summary>Was auf den Wegen des Spiels steht (Baustellen) und das Kennzeichen ihrer Stücke (nur geänderte Wege werden neu gerechnet).</summary>
    private readonly Dictionary<int, (Node3D Knoten, string Zeichen)> _belagWege = new();
    private bool _belagVeraltet;
    private Node3D? _wegAuswahlKnoten;

    /// <summary>Ein Klick auf einen Weg: Nummer in <c>Karte.Wege</c> und Stelle (Meter entlang des Verlaufs).</summary>
    public event Action<int, double>? WegAngeklickt;

    // ═════════════════════════════════════════════════════════════════════════
    //  Zeichnen
    // ═════════════════════════════════════════════════════════════════════════

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

    /// <summary>
    /// Ein Weg des Spiels ist neu oder sein Belag hat sich geändert: Seine Kacheln der Wegeschicht werden neu gerechnet, die Baustellen neu aufgestellt
    /// (in einem eigenen Knoten, <c>_belagWege</c>).
    /// </summary>
    private void WegBelagZeichnen(int nummer, IReadOnlyList<Vector3> verlauf, float breite, bool neuAuchGleich = false)
    {
        var stuecke = BelagStuecke(nummer);
        var baustellen = _wirtschaft?.Wegebau.Baustellen.Where(b => b.Weg == nummer).ToList() ?? new List<Wegbaustelle>();
        var zeichen = BelagZeichen(stuecke, baustellen);
        if (!neuAuchGleich && _belagWege.TryGetValue(nummer, out var alt) && alt.Zeichen == zeichen) return;
        if (_belagWege.Remove(nummer, out var weg)) KnotenEntfernen(weg.Knoten);
        var linie = verlauf.Select(p => new Vector2(p.X, p.Z)).ToList();
        if (Rahmen(linie, breite / 2) is { } rahmen) SchichtVeraltet(rahmen);
        var knoten = new Node3D { Name = "Belag" };
        AddChild(knoten);
        if (linie.Count >= 2)
        {
            var stelle = new float[linie.Count];
            for (var i = 1; i < linie.Count; i++) stelle[i] = stelle[i - 1] + linie[i].DistanceTo(linie[i - 1]);
            var boden = WegBoden(nummer) ?? ((x, z) => BodenHoehe(x, z));
            foreach (var b in baustellen) BaustelleAufstellen(knoten, linie, stelle, (float)b.Von, (float)b.Bis, breite, boden);
        }
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
