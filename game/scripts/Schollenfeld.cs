using System;
using System.Collections.Generic;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Eisschollen auf einem Fluss als Körper (realistisches Wasser, Phase 3c/3d, <c>docs/wasser/phase3.md</c>): Jede Scholle ist eine Kopie derselben
/// runden Scheibe (<see cref="ScheibenNetz"/>) in einem <see cref="MultiMesh"/>; ihre gebrochene Form, die Dicke, das Schaukeln auf den Wellen und
/// die Oberseite (gemeinsames Eismaterial) rechnet <c>eis_scholle.gdshaderinc</c>. Zwei Sorten:
/// <list type="bullet">
/// <item><b>Treibende Schollen</b> (<see cref="Bild"/>): eine eigene, leichte 2D-Simulation in den Laufachsen (Stelle, quer) – keine Physik-Engine.
/// Jede Scholle ist ein Kreis; die Strömung zieht sie (in der Mitte schneller als am Rand), sie stoßen aneinander (Abstoßen statt Durchdringen, ein
/// Stoß bremst und dreht), prallen an Brückenpfeilern, dem Prahm der Fähre und den Kähnen ab und gleiten vorbei; trifft eine einen Pfeiler von
/// vorn, bleibt sie ein paar Sekunden hängen, die folgenden stauen sich dahinter, dann löst sich der Stau. Nachbarn findet ein Gitter (2 m) ohne
/// Speicheranforderung je Bild. Reine Darstellung: nicht im Kern, nicht deterministisch.</item>
/// <item><b>Feste Schollen</b> im Randeis (<see cref="FesteBauen"/>): festgefrorene Schollen am Ufer, so wie das Randeis großer Flüsse aus
/// angefrorenem Treibeis und Eisbrei besteht. Sie geben der Eiskante Dicke und dieselbe Anmutung wie die treibenden; zwischen ihnen liegt das flache
/// Eis des Wasser-Shaders (dasselbe Material). Jede hat eine Schwelle: Sie wächst auf, sobald die Eiskante des Shaders sie erreicht (Uniform
/// <c>eis_fest</c>), die CPU schreibt sie nur einmal; gezeichnet in Stücken zu 32 m (Godot lässt Stücke außerhalb des Bildes weg), nah fein, fern grob.</item>
/// </list>
/// Die Wellen müssen dieselben sein wie auf der Wasserfläche: <see cref="Wasserarten.WellenSetzen"/> belegt den Stoff der Schollen wie den des Flusses.
/// </summary>
public sealed class Schollenfeld
{
    /// <summary>Ein Punkt der Mittellinie (je Meter): Ort, Höhe des Spiegels, halbe Breite, Fließrichtung (Einheitsvektor).</summary>
    public readonly record struct Laufpunkt(Vector2 Ort, float Spiegel, float Halb, Vector2 Richtung);

    /// <summary>Ein Hindernis in Laufachsen: quer (m), Stelle (m) und seine halbe Ausdehnung quer und längs. Schollen prallen daran ab.</summary>
    public readonly record struct Hindernis(float Quer, float Stelle, float HalbQuer, float HalbLaengs);

    private struct Scholle
    {
        public float S0, Anteil, Halbmesser, Dicke, Gier, Dreh, Schwelle, Saat, Schnee;
    }

    /// <summary>Bis zu diesem Abstand (m) zeichnet die feine Scheibe, dahinter die grobe.</summary>
    private const float NahBis = 70f;

    /// <summary>So weit (m) hält eine Scholle mindestens vom Ufer bzw. von der Kante des Randeises (beim vollen Randeis um die Hälfte breiter: die Kante ist unregelmäßig).</summary>
    private const float UferAbstand = 0.8f;

    /// <summary>Stoßhalbmesser einer Scholle als Anteil ihres Halbmessers (der Umriss ist ein Vieleck, meist innerhalb des Halbmessers).</summary>
    private const float StossAnteil = 0.82f;

    /// <summary>Gitter für die Nachbarsuche (m).</summary>
    private const float Zelle = 2f;

    /// <summary>
    /// Nur in diesem Fenster (m entlang des Laufs) um die Kamera stoßen die Schollen; weiter weg treiben sie nur (dort sieht man sie kaum, und sie
    /// treiben gleich schnell, laufen also nicht ineinander). Spart den größten Teil der Rechenzeit.
    /// </summary>
    private const float StossFenster = 160f;

    /// <summary>Stücke der festen Schollen entlang des Laufs (m) und bis zu welchem Abstand (m) sie fein gezeichnet werden.</summary>
    private const float FestStueck = 32f, FestNahBis = 60f;

    private readonly Laufpunkt[] _lauf;
    private readonly float _laenge, _tempo, _randeis;
    private readonly Scholle[] _schollen;
    private readonly List<Hindernis> _festeHindernisse;
    private readonly MultiMeshInstance3D _nah, _fern;
    private readonly float[] _pufferNah, _pufferFern;
    private readonly int _platz;
    private double _zeit;
    private float _eisFestZuletzt = -1;

    // Zustand der treibenden Schollen in Laufachsen: Stelle, quer, Tempo längs und quer (m/s), Gier und Drehtempo, Haftzeit an einem Pfeiler (s)
    private readonly float[] _s, _q, _vs, _vq, _gier, _om, _haft, _rStoss, _halbHier;
    private readonly int[] _pfeilerSchritt; // Teilschritt, in dem die Scholle zuletzt einen Pfeiler berührte (erster Stoß = nicht im vorigen)
    private readonly bool[] _weg;           // außerhalb des Fensters um die Kamera: treibt nur, ohne Stöße
    private int _schritt, _kameraTipp = -1;
    private readonly int[] _kopf, _naechster;
    private readonly int _zellenS, _zellenQ;
    private readonly float _halbMax, _rStossMax;
    private uint _zufall;

    /// <summary>Der Knoten mit den Schollen-Netzen (in die Szene hängen).</summary>
    public Node3D Knoten { get; }

    /// <summary>Der Stoff der Schollen (Wellen-Uniforms wie der Fluss; <c>eis_fest</c> für die festen).</summary>
    public ShaderMaterial Stoff { get; }

    /// <summary>Wie viele treibende Schollen es gibt, wie viele im letzten Bild gezeichnet wurden und wie viele feste im Randeis liegen.</summary>
    public int Anzahl => _schollen.Length;
    public int Gezeichnet { get; private set; }
    public int Feste { get; private set; }

    /// <summary>Rechenzeit der Simulation im letzten Bild (ms) und wie viele Schollen dabei aktiv waren (für die Messung).</summary>
    public double SimulationMs { get; private set; }
    public int Aktiv { get; private set; }

    /// <summary>Zähler für die Messung (letztes Bild): Paarprüfungen, Berührungen, Teilschritte, Zeit der Stöße (ms).</summary>
    public long Pruefungen { get; private set; }
    public int Beruehrungen { get; private set; }
    public int Teilschritte { get; private set; }
    public double StoesseMs { get; private set; }

    /// <param name="lauf">Die Mittellinie je Meter (Index = Stelle in m), mindestens zwei Punkte.</param>
    /// <param name="tempo">Strömung des Flusses (m/s); die Schollen treiben mit 0,9 davon.</param>
    /// <param name="randeis">Reichweite des Randeises bei vollem Eis (m): so weit bleiben die Schollen vom Ufer weg.</param>
    /// <param name="saat">Startwert (aus der Karte): gleiche Karte, gleiche Schollen.</param>
    /// <param name="stufe">Grafikstufe: Niedrig nur große Schollen (einfacher Shader) und keine festen, Mittel ohne die kleinsten, Hoch/Ultra alle (mit Luftblasen).</param>
    /// <param name="hindernisse">Feste Hindernisse (Brückenpfeiler) in Laufachsen.</param>
    /// <param name="festFrei">Ob an einem Weltort mit Halbmesser eine feste Scholle liegen darf (Rinnen, Löcher, Kai); null = überall.</param>
    public Schollenfeld(Laufpunkt[] lauf, float tempo, float randeis, int saat, Wasserstufe stufe, List<Hindernis> hindernisse, string name,
        Func<Vector2, float, bool>? festFrei = null)
    {
        _lauf = lauf;
        _laenge = lauf.Length - 1;
        _tempo = Mathf.Max(tempo, 0.05f) * 0.9f;
        _randeis = randeis;
        _festeHindernisse = hindernisse;
        _schollen = Verteilen(saat, stufe);
        _platz = Math.Min(_schollen.Length, stufe >= Wasserstufe.Hoch ? 2400 : 1600);
        _zufall = (uint)saat * 2654435761u + 1u;

        Stoff = new ShaderMaterial
        {
            Shader = GD.Load<Shader>(stufe >= Wasserstufe.Hoch ? "res://assets/shader/eis_scholle.gdshader" : "res://assets/shader/eis_scholle_einfach.gdshader"),
            ResourceName = "Eisschollen",
        };
        Wasserarten.EisStoffSetzen(Stoff, Wasserarten.Fluss);
        Knoten = new Node3D { Name = "Eisschollen_" + name };
        (_nah, _pufferNah) = Teil("Nah", ScheibenNetz(24, fein: true));
        (_fern, _pufferFern) = Teil("Fern", ScheibenNetz(10, fein: false));
        Knoten.Visible = false;

        // Zustand und Gitter der Simulation (einmal angelegt, jedes Bild wiederverwendet)
        var n = _schollen.Length;
        (_s, _q, _vs, _vq, _gier, _om, _haft, _rStoss) = (new float[n], new float[n], new float[n], new float[n], new float[n], new float[n], new float[n], new float[n]);
        _halbHier = new float[n];
        _pfeilerSchritt = new int[n];
        _weg = new bool[n];
        _naechster = new int[n];
        foreach (var p in _lauf) _halbMax = Mathf.Max(_halbMax, p.Halb);
        _zellenS = (int)MathF.Ceiling(_laenge / Zelle) + 1;
        _zellenQ = (int)MathF.Ceiling(2f * _halbMax / Zelle) + 1;
        _kopf = new int[_zellenS * _zellenQ];
        for (var i = 0; i < n; i++)
        {
            var sch = _schollen[i];
            _s[i] = sch.S0;
            _q[i] = sch.Anteil * (Platz(sch.S0, 1f) - sch.Halbmesser);
            _vs[i] = _tempo * Profil(_q[i], Punkt(sch.S0).Halb);
            _gier[i] = sch.Gier;
            _om[i] = sch.Dreh;
            _halbHier[i] = Punkt(sch.S0).Halb;
            _pfeilerSchritt[i] = -10;
            _rStossMax = Mathf.Max(_rStossMax, sch.Halbmesser * StossAnteil);
        }

        if (stufe > Wasserstufe.Niedrig) FesteBauen(saat, festFrei);
    }

    private (MultiMeshInstance3D, float[]) Teil(string name, ArrayMesh netz)
    {
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = netz,
        };
        mm.InstanceCount = Math.Max(_platz, 1);
        mm.VisibleInstanceCount = 0;
        // Der ganze Fluss als Rahmen (die Schollen wandern; die Sichtprüfung je Scholle macht Bild())
        var (min, max) = Rahmen(0, _lauf.Length - 1, 1f);
        mm.CustomAabb = new Aabb(min, max - min);
        var knoten = new MultiMeshInstance3D
        {
            Name = name, Multimesh = mm, MaterialOverride = Stoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        Knoten.AddChild(knoten);
        return (knoten, new float[Math.Max(_platz, 1) * 20]);
    }

    private (Vector3 Min, Vector3 Max) Rahmen(int von, int bis, float hoehe)
    {
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (var i = Math.Max(0, von); i <= Math.Min(_lauf.Length - 1, bis); i++)
        {
            var p = _lauf[i];
            min = min.Min(new Vector3(p.Ort.X - p.Halb - 2f, p.Spiegel - hoehe, p.Ort.Y - p.Halb - 2f));
            max = max.Max(new Vector3(p.Ort.X + p.Halb + 2f, p.Spiegel + hoehe, p.Ort.Y + p.Halb + 2f));
        }
        return (min, max);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Verteilung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Setzt die Schollen von groß nach klein an zufällige Stellen (Stelle, quer), ohne dass sie sich berühren; in den Feldern fast jede, auf den
    /// freien Strecken nur wenige und keine großen. Quer gilt der Platz beim vollen Randeis (bei weniger Eis rücken sie im selben Verhältnis
    /// auseinander). Das ist der Anfang; danach treiben sie frei (<see cref="Bild"/>).
    /// </summary>
    private Scholle[] Verteilen(int saat, Wasserstufe stufe)
    {
        var rnd = new Random(saat);
        var liste = new List<Scholle>();
        var raster = new Dictionary<(int, int), List<int>>();
        const float rasterM = 4f;
        var groessteHalb = 0f;
        foreach (var p in _lauf) groessteHalb = Mathf.Max(groessteHalb, p.Halb);
        var versuche = (int)(_laenge * groessteHalb * 2f * 0.4f);
        var kleinste = stufe switch { Wasserstufe.Niedrig => 0.75f, Wasserstufe.Mittel => 0.45f, _ => 0.25f };
        for (var k = 0; k < versuche; k++)
        {
            // von groß nach klein: viele kleine, wenige große (Halbmesser 0,25–3,25 m)
            var halb = 0.25f + 3f * MathF.Pow(1f - (float)k / versuche, 2.4f);
            if (halb < kleinste) break;
            var s = (float)rnd.NextDouble() * _laenge;
            var feld = Feld(s, saat);
            var annahme = Mathf.Lerp(0.05f, 1f, feld) * (halb > 1.4f ? feld : 1f);
            var wurf = (float)rnd.NextDouble();
            var anteilWurf = (float)rnd.NextDouble() * 2f - 1f;
            if (wurf > annahme) continue;
            var platz = Platz(s, 1f);
            if (platz < halb + 0.3f) continue;
            var quer = anteilWurf * (platz - halb);
            // keine Berührung mit den schon gesetzten (Lücke 0,25 m)
            var (rs, rq) = ((int)MathF.Floor(s / rasterM), (int)MathF.Floor(quer / rasterM));
            var frei = true;
            for (var i = -2; i <= 2 && frei; i++)
                for (var j = -2; j <= 2 && frei; j++)
                    if (raster.TryGetValue((rs + i, rq + j), out var nachbarn))
                        foreach (var n in nachbarn)
                        {
                            var o = liste[n];
                            var oq = o.Anteil * (Platz(o.S0, 1f) - o.Halbmesser);
                            var (ds, dq) = (o.S0 - s, oq - quer);
                            if (ds * ds + dq * dq < (o.Halbmesser + halb + 0.25f) * (o.Halbmesser + halb + 0.25f)) { frei = false; break; }
                        }
            if (!frei) continue;
            if (!raster.TryGetValue((rs, rq), out var zelle)) raster[(rs, rq)] = zelle = new List<int>();
            zelle.Add(liste.Count);
            liste.Add(new Scholle
            {
                S0 = s, Anteil = anteilWurf, Halbmesser = halb,
                Dicke = Mathf.Clamp(0.08f + 0.05f * halb + 0.1f * (float)rnd.NextDouble(), 0.08f, 0.32f),
                Gier = (float)rnd.NextDouble() * Mathf.Tau, Dreh = ((float)rnd.NextDouble() - 0.5f) * 0.06f / (0.5f + halb),
                Schwelle = (float)rnd.NextDouble(), Saat = rnd.Next(0, 9973), Schnee = (float)rnd.NextDouble() * 0.35f,
            });
        }
        return liste.ToArray();
    }

    /// <summary>Felder dichter Schollen entlang des Laufs (0 = fast frei, 1 = dicht), glattes Rauschen mit etwa 50 m Abstand.</summary>
    private static float Feld(float s, int saat)
    {
        var x = s * 0.02f + saat % 1000 * 0.137f;
        var n = Rauschen(x) * 0.65f + Rauschen(x * 2.3f + 5.1f) * 0.35f;
        return Mathf.SmoothStep(0.3f, 0.68f, n);
    }

    private static float Rauschen(float x)
    {
        var i = MathF.Floor(x);
        var f = x - i;
        f = f * f * (3 - 2 * f);
        return Mathf.Lerp(Hash((int)i), Hash((int)i + 1), f);
    }

    private static float Hash(int i)
    {
        var n = (uint)i * 1597334673u;
        n = (n ^ (n >> 15)) * 2246822519u;
        n ^= n >> 13;
        return (n & 0xFFFFFF) / 16777216f;
    }

    /// <summary>Platz quer von der Mitte (m) an der Stelle beim Eis <paramref name="eis"/>: halbe Breite minus Randeis (mit Zugabe) und Abstand.</summary>
    private float Platz(float s, float eis)
    {
        var p = Punkt(s);
        return p.Halb - _randeis * eis * 1.5f - UferAbstand;
    }

    /// <summary>Strömungsprofil quer: in der Mitte etwas schneller als die mittlere Strömung, zum Ufer hin langsamer.</summary>
    private static float Profil(float quer, float halb)
    {
        var a = quer / Mathf.Max(halb, 1f);
        return 1.12f - 0.5f * a * a;
    }

    private Laufpunkt Punkt(float s)
    {
        var i = Math.Clamp((int)s, 0, _lauf.Length - 2);
        var t = Mathf.Clamp(s - i, 0f, 1f);
        var (a, b) = (_lauf[i], _lauf[i + 1]);
        return new Laufpunkt(a.Ort.Lerp(b.Ort, t), Mathf.Lerp(a.Spiegel, b.Spiegel, t), Mathf.Lerp(a.Halb, b.Halb, t), a.Richtung.Lerp(b.Richtung, t).Normalized());
    }

    /// <summary>
    /// Der nächste Punkt der Mittellinie zu einem Weltort: (quer, Stelle) in Laufachsen und der Abstand (für Hindernisse). <paramref name="tipp"/> ist
    /// der Index vom letzten Mal (−1: ganz suchen).
    /// </summary>
    public (float Quer, float Stelle, float Abstand) InLaufachsen(Vector2 ort, ref int tipp)
    {
        var (von, bis) = tipp < 0 ? (0, _lauf.Length - 1) : (Math.Max(0, tipp - 40), Math.Min(_lauf.Length - 1, tipp + 40));
        var beste = von;
        var besteD = float.MaxValue;
        for (var i = von; i <= bis; i++)
        {
            var d = _lauf[i].Ort.DistanceSquaredTo(ort);
            if (d < besteD) (besteD, beste) = (d, i);
        }
        tipp = beste;
        var p = _lauf[beste];
        var quer = new Vector2(-p.Richtung.Y, p.Richtung.X);
        var rel = ort - p.Ort;
        return (rel.Dot(quer), beste + rel.Dot(p.Richtung), MathF.Sqrt(besteD));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Feste Schollen im Randeis
    // ═════════════════════════════════════════════════════════════════════════

    // Dasselbe Rauschen wie w_hash/w_rausch/w_fbm in wasser_wellen.gdshaderinc (Ganzzahl-Hash): Die Kante des Randeises im Shader und die Schwellen
    // der festen Schollen folgen denselben Buchten und Zungen.
    private static float WHash(float x, float y)
    {
        unchecked
        {
            var qx = (uint)(int)MathF.Floor(x) * 1597334673u;
            var qy = (uint)(int)MathF.Floor(y) * 3812015801u;
            var n = (qx ^ qy) * 1597334673u;
            return (n >> 8) * (1f / 16777216f);
        }
    }

    private static float WRausch(float x, float y)
    {
        var (ix, iy) = (MathF.Floor(x), MathF.Floor(y));
        var (fx, fy) = (x - ix, y - iy);
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        var a = Mathf.Lerp(WHash(ix, iy), WHash(ix + 1, iy), fx);
        var b = Mathf.Lerp(WHash(ix, iy + 1), WHash(ix + 1, iy + 1), fx);
        return Mathf.Lerp(a, b, fy);
    }

    private static float WFbm(float x, float y) =>
        WRausch(x, y) * 0.55f + WRausch(x * 2.13f + 7.1f, y * 2.13f + 7.1f) * 0.3f + WRausch(x * 4.7f + 3.3f, y * 4.7f + 3.3f) * 0.15f;

    /// <summary>
    /// Legt die festen Schollen im Randeis beider Ufer: zufällig im Band vom Ufer bis zur weitesten Eiskante, ohne sich zu durchdringen (kleine
    /// Überlappung: die Umrisse sind Vielecke), Halbmesser 0,45–1,75 m, dicker als treibende, mit mehr Schnee. Je Scholle die Schwelle: der
    /// Eisfortschritt, bei dem die glatte Kante des Shaders (Reichweite · Eis + Buchten) knapp über ihre Mitte hinausreicht – davor wächst sie auf.
    /// Nicht in Rinnen, Löchern, am Kai (<paramref name="frei"/>) und nicht an Pfeilern.
    /// </summary>
    private void FesteBauen(int saat, Func<Vector2, float, bool>? frei)
    {
        if (_randeis < 0.5f || _laenge < 20f) return;
        var rnd = new Random(saat * 31 + 7);
        var unruhe = Mathf.Min(_randeis, 12f) * 0.42f;
        var band = _randeis + unruhe + 0.8f;
        var liste = new List<(float S, float Q, float R, float Schwelle)>();
        var raster = new Dictionary<(int, int), List<int>>();
        const float rasterM = 3f;
        var versuche = (int)(_laenge * 2f * band * 0.9f);
        for (var k = 0; k < versuche; k++)
        {
            var u = (float)rnd.NextDouble();
            var r = 0.45f + 1.3f * u * u;
            var s = 3f + (float)rnd.NextDouble() * (_laenge - 6f);
            var seite = rnd.NextDouble() < 0.5 ? -1f : 1f;
            var d = 0.3f + (float)rnd.NextDouble() * band; // Abstand der Mitte vom Ufer
            var p = Punkt(s);
            if (d > p.Halb * 0.45f) continue; // schmaler Fluss: nicht über die Mitte hinaus
            var q = seite * (p.Halb - d);
            var querW = new Vector2(-p.Richtung.Y, p.Richtung.X);
            var ort = p.Ort + querW * q;
            // Schwelle: die Kante des Shaders bei Eis e liegt bei e · Reichweite + Buchten · Anlauf(e); die Scholle erscheint, wenn sie 35 % ihres
            // Halbmessers vor der Mitte steht
            var bucht = (WFbm(ort.X * 0.045f + 17f, ort.Y * 0.045f + 17f) - 0.5f) * 2f * unruhe;
            var noetig = d - 0.35f * r;
            float Kante(float e) => e * _randeis + bucht * Mathf.SmoothStep(0f, 0.3f, e);
            if (Kante(1f) < noetig) continue;
            float lo = 0f, hi = 1f;
            for (var it = 0; it < 14; it++)
            {
                var mitte = (lo + hi) / 2f;
                if (Kante(mitte) >= noetig) hi = mitte; else lo = mitte;
            }
            var schwelle = Mathf.Max(hi, 0.002f);
            if (frei is not null && !frei(ort, r)) continue;
            var anPfeiler = false;
            foreach (var h in _festeHindernisse)
                if (MathF.Abs(s - h.Stelle) < h.HalbLaengs + r + 0.5f && MathF.Abs(q - h.Quer) < h.HalbQuer + r + 0.5f) { anPfeiler = true; break; }
            if (anPfeiler) continue;
            var (rs, rq) = ((int)MathF.Floor(s / rasterM), (int)MathF.Floor(q / rasterM));
            var platzFrei = true;
            for (var i = -1; i <= 1 && platzFrei; i++)
                for (var j = -1; j <= 1 && platzFrei; j++)
                    if (raster.TryGetValue((rs + i, rq + j), out var nachbarn))
                        foreach (var n in nachbarn)
                        {
                            var o = liste[n];
                            var (ds, dq) = (o.S - s, o.Q - q);
                            var min = 0.8f * (o.R + r);
                            if (ds * ds + dq * dq < min * min) { platzFrei = false; break; }
                        }
            if (!platzFrei) continue;
            if (!raster.TryGetValue((rs, rq), out var zelle)) raster[(rs, rq)] = zelle = new List<int>();
            zelle.Add(liste.Count);
            liste.Add((s, q, r, schwelle));
        }
        Feste = liste.Count;
        if (liste.Count == 0) return;

        // In Stücke zu FestStueck m; je Stück ein feines Netz (nah) und ein grobes (fern), beide mit denselben Kopien
        var stuecke = (int)MathF.Ceiling(_laenge / FestStueck);
        var jeStueck = new List<int>[stuecke];
        for (var i = 0; i < liste.Count; i++)
        {
            var k = Math.Clamp((int)(liste[i].S / FestStueck), 0, stuecke - 1);
            (jeStueck[k] ??= new List<int>()).Add(i);
        }
        var fein = ScheibenNetz(16, fein: true);
        var grob = ScheibenNetz(10, fein: false);
        var festKnoten = new Node3D { Name = "Feste" };
        Knoten.AddChild(festKnoten);
        for (var k = 0; k < stuecke; k++)
        {
            if (jeStueck[k] is not { Count: > 0 } teil) continue;
            var puffer = new float[teil.Count * 20];
            for (var n = 0; n < teil.Count; n++)
            {
                var (s, q, r, schwelle) = liste[teil[n]];
                var p = Punkt(s);
                var querW = new Vector2(-p.Richtung.Y, p.Richtung.X);
                var ort2 = p.Ort + querW * q;
                var gier = (float)rnd.NextDouble() * Mathf.Tau;
                var (sin, cos) = MathF.SinCos(gier);
                var o = n * 20;
                puffer[o + 0] = cos * r; puffer[o + 1] = 0; puffer[o + 2] = sin * r; puffer[o + 3] = ort2.X;
                puffer[o + 4] = 0; puffer[o + 5] = r; puffer[o + 6] = 0; puffer[o + 7] = p.Spiegel;
                puffer[o + 8] = -sin * r; puffer[o + 9] = 0; puffer[o + 10] = cos * r; puffer[o + 11] = ort2.Y;
                // COLOR: Form, Dicke (am Ufer dicker), Schnee (mehr: altes Eis), Schwelle; CUSTOM wie bei den treibenden (nicht gebraucht)
                puffer[o + 12] = rnd.Next(0, 9973); puffer[o + 13] = 0.14f + 0.14f * (float)rnd.NextDouble();
                puffer[o + 14] = 0.25f + 0.45f * (float)rnd.NextDouble(); puffer[o + 15] = schwelle;
                puffer[o + 16] = q; puffer[o + 17] = s; puffer[o + 18] = p.Richtung.X; puffer[o + 19] = p.Richtung.Y;
            }
            var (min, max) = Rahmen((int)(k * FestStueck) - 2, (int)((k + 1) * FestStueck) + 2, 1.2f);
            foreach (var (netz, nah) in new[] { (fein, true), (grob, false) })
            {
                var mm = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = netz,
                    InstanceCount = teil.Count,
                };
                RenderingServer.MultimeshSetBuffer(mm.GetRid(), puffer);
                mm.CustomAabb = new Aabb(min, max - min);
                festKnoten.AddChild(new MultiMeshInstance3D
                {
                    Name = $"Fest_{k}_{(nah ? "Nah" : "Fern")}", Multimesh = mm, MaterialOverride = Stoff,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    VisibilityRangeBegin = nah ? 0f : FestNahBis, VisibilityRangeEnd = nah ? FestNahBis : 0f,
                });
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild: treiben, stoßen, zeichnen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Schiebt die Schollen weiter (Simulation in Teilschritten zu höchstens 1/30 s) und schreibt die sichtbaren in die beiden Netze.
    /// <paramref name="eis"/> ist der Eisfortschritt der Art (0–1): Dichte, Breite des Randeises und die festen Schollen; <paramref name="schollenAn"/>
    /// 0 = keine treibenden Schollen (dann kostet es nichts). <paramref name="beweglich"/>: Hindernisse, die sich bewegen (Prahm, Kähne), in Laufachsen.
    /// </summary>
    public void Bild(double delta, Camera3D? kamera, float eis, float schollenAn, List<Hindernis> beweglich)
    {
        _zeit += delta;
        if (MathF.Abs(eis - _eisFestZuletzt) > 0.0005f)
        {
            _eisFestZuletzt = eis;
            Stoff.SetShaderParameter("eis_fest", eis);
        }
        var dichte = schollenAn * Mathf.SmoothStep(0.15f, 0.85f, eis);
        var festeSichtbar = Feste > 0 && eis > 0.001f;
        if ((dichte <= 0.001f && !festeSichtbar) || kamera is null || !GodotObject.IsInstanceValid(kamera))
        {
            if (Knoten.Visible) Knoten.Visible = false;
            return;
        }
        if (!Knoten.Visible) Knoten.Visible = true;
        if (dichte <= 0.001f)
        {
            Schreiben(_nah, _pufferNah, 0);
            Schreiben(_fern, _pufferFern, 0);
            Gezeichnet = 0;
            return;
        }

        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        var schritte = Math.Clamp((int)MathF.Ceiling((float)delta * 30f), 1, 2);
        var dt = Mathf.Min((float)delta, 0.1f) / schritte;
        (Pruefungen, Beruehrungen, Teilschritte, StoesseMs) = (0, 0, schritte, 0);
        var augeXz = new Vector2(kamera.GlobalPosition.X, kamera.GlobalPosition.Z);
        var sKamera = InLaufachsen(augeXz, ref _kameraTipp).Stelle;
        for (var k = 0; k < schritte; k++) Schritt(dt, dichte, eis, beweglich, sKamera);
        SimulationMs = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        var ebenen = kamera.GetFrustum();
        Span<Plane> plan = stackalloc Plane[6];
        var zahlEbenen = Math.Min(ebenen.Count, 6);
        for (var i = 0; i < zahlEbenen; i++) plan[i] = ebenen[i];
        var auge = kamera.GlobalPosition;
        var (nahZahl, fernZahl) = (0, 0);

        for (var i = 0; i < _schollen.Length; i++)
        {
            ref var sch = ref _schollen[i];
            if (sch.Schwelle >= dichte) continue;
            var s = _s[i];
            var wachsen = Mathf.SmoothStep(sch.Schwelle, sch.Schwelle + 0.08f, dichte);
            var ende = Mathf.SmoothStep(0f, 14f, s) * Mathf.SmoothStep(_laenge, _laenge - 14f, s);
            var r = sch.Halbmesser * wachsen * ende;
            if (r < 0.06f) continue;
            var p = Punkt(s);
            var querW = new Vector2(-p.Richtung.Y, p.Richtung.X);
            var ort2 = p.Ort + querW * _q[i];
            var ort = new Vector3(ort2.X, p.Spiegel, ort2.Y);

            // im Bild? (Kugel um die Scholle gegen den Sichtkegel)
            var drin = true;
            for (var e = 0; e < zahlEbenen; e++)
                if (plan[e].DistanceTo(ort) > r + 0.5f) { drin = false; break; }
            if (!drin) continue;
            var weite = auge.DistanceTo(ort);
            // sehr kleine Schollen nur nah, kleine nur mittelweit (sie wären weniger als ein paar Bildpunkte)
            if ((weite > 140f && sch.Halbmesser < 0.55f) || (weite > 300f && sch.Halbmesser < 1.2f)) continue;
            var nah = weite < NahBis;
            if (nah ? nahZahl >= _platz : fernZahl >= _platz) continue;
            var puffer = nah ? _pufferNah : _pufferFern;
            var o = (nah ? nahZahl++ : fernZahl++) * 20;

            var (sin, cos) = MathF.SinCos(_gier[i]);
            // Lage: Drehung um die Hochachse, gleichmäßig auf den Halbmesser skaliert (Zeilen der Basis, dann der Ort)
            puffer[o + 0] = cos * r; puffer[o + 1] = 0; puffer[o + 2] = sin * r; puffer[o + 3] = ort.X;
            puffer[o + 4] = 0; puffer[o + 5] = r; puffer[o + 6] = 0; puffer[o + 7] = ort.Y;
            puffer[o + 8] = -sin * r; puffer[o + 9] = 0; puffer[o + 10] = cos * r; puffer[o + 11] = ort.Z;
            // COLOR: Form, Dicke, Schnee, 0 = treibt; CUSTOM: Laufachsen und Fließrichtung für die Wellen
            puffer[o + 12] = sch.Saat; puffer[o + 13] = sch.Dicke * Mathf.Lerp(0.6f, 1f, wachsen); puffer[o + 14] = sch.Schnee; puffer[o + 15] = 0;
            puffer[o + 16] = _q[i]; puffer[o + 17] = s; puffer[o + 18] = p.Richtung.X; puffer[o + 19] = p.Richtung.Y;
        }
        Schreiben(_nah, _pufferNah, nahZahl);
        Schreiben(_fern, _pufferFern, fernZahl);
        Gezeichnet = nahZahl + fernZahl;
    }

    /// <summary>
    /// Ein Schritt der Simulation (Laufachsen: s längs, stromab positiv; q quer):
    /// 1. Gitter der aktiven Schollen füllen; 2. Strömung zieht (Zeitkonstante je nach Größe), am Pfeiler hängende bremsen; 3. Stöße untereinander
    /// (Lage auseinanderschieben nach Masse, Tempo unelastisch ausgleichen, Reibung dreht); 4. Hindernisse (Pfeiler als Kapsel, Prahm und Kähne):
    /// herausschieben, abprallen, entlanggleiten, beim ersten Stoß von vorn an einen Pfeiler einige Sekunden hängen bleiben; 5. bewegen, am Ufer bzw.
    /// Randeis abprallen, am Ende des Laufs wieder oben einsetzen.
    /// </summary>
    private void Schritt(float dt, float dichte, float eis, List<Hindernis> beweglich, float sKamera)
    {
        var n = _schollen.Length;
        var randeis = _randeis * eis * 1.5f + UferAbstand;
        var halbeLaenge = _laenge / 2f;
        _schritt++;
        Array.Fill(_kopf, -1);
        var aktiv = 0;
        for (var i = 0; i < n; i++)
        {
            ref readonly var sch = ref _schollen[i];
            if (sch.Schwelle >= dichte) { _rStoss[i] = 0; continue; }
            _rStoss[i] = sch.Halbmesser * StossAnteil * Mathf.SmoothStep(sch.Schwelle, sch.Schwelle + 0.08f, dichte);
            aktiv++;
            // Weit weg von der Kamera: nur treiben (gleich schnell wie zuletzt), keine Stöße
            var abK = MathF.Abs(_s[i] - sKamera);
            _weg[i] = MathF.Min(abK, _laenge - abK) > StossFenster;
            if (_weg[i])
            {
                _s[i] += _vs[i] * dt;
                if (_s[i] >= _laenge) _s[i] -= _laenge;
                else if (_s[i] < 0) _s[i] += _laenge;
                _gier[i] += _om[i] * dt;
                continue;
            }
            var zelle = Zellindex(_s[i], _q[i]);
            _naechster[i] = _kopf[zelle];
            _kopf[zelle] = i;
        }
        Aktiv = aktiv;

        // 2. Strömung
        var t = (float)_zeit;
        for (var i = 0; i < n; i++)
        {
            if (_rStoss[i] <= 0 || _weg[i]) continue;
            ref readonly var sch = ref _schollen[i];
            var zielS = _tempo * Profil(_q[i], _halbHier[i]);
            // leichtes Pendeln quer (Wirbel), je Scholle anders
            var zielQ = 0.06f * (Rauschen(t * 0.05f + sch.Saat * 0.37f) - 0.5f) * 2f;
            var tau = 1.2f + 0.7f * sch.Halbmesser;
            if (_haft[i] > 0)
            {
                _haft[i] -= dt;
                zielS = 0;
                tau = 0.4f;
            }
            var k = Mathf.Min(dt / tau, 1f);
            _vs[i] += (zielS - _vs[i]) * k;
            _vq[i] += (zielQ - _vq[i]) * k;
            _om[i] += (sch.Dreh - _om[i]) * Mathf.Min(dt / 5f, 1f);
        }

        // 3. Stöße untereinander
        var stossStart = System.Diagnostics.Stopwatch.GetTimestamp();
        long pruef = 0;
        var beruehrt0 = 0;
        for (var i = 0; i < n; i++)
        {
            var ri = _rStoss[i];
            if (ri <= 0 || _weg[i]) continue;
            var reichweite = ri + _rStossMax;
            var (s0, s1) = ((int)((_s[i] - reichweite) / Zelle), (int)((_s[i] + reichweite) / Zelle));
            var (q0, q1) = ((int)((_q[i] + _halbMax - reichweite) / Zelle), (int)((_q[i] + _halbMax + reichweite) / Zelle));
            s0 = Math.Max(s0, 0); s1 = Math.Min(s1, _zellenS - 1);
            q0 = Math.Max(q0, 0); q1 = Math.Min(q1, _zellenQ - 1);
            for (var cs = s0; cs <= s1; cs++)
                for (var cq = q0; cq <= q1; cq++)
                    for (var j = _kopf[cs * _zellenQ + cq]; j >= 0; j = _naechster[j])
                    {
                        if (j <= i) continue;
                        pruef++;
                        var dS = _s[j] - _s[i];
                        if (MathF.Abs(dS) > halbeLaenge) continue;
                        var dQ = _q[j] - _q[i];
                        var rj = _rStoss[j];
                        var min = ri + rj;
                        var d2 = dS * dS + dQ * dQ;
                        if (d2 >= min * min || d2 < 1e-8f) continue;
                        var d = MathF.Sqrt(d2);
                        beruehrt0++;
                        var (nS, nQ) = (dS / d, dQ / d);
                        var tief = min - d;
                        var (mi, mj) = (ri * ri, rj * rj);
                        var summe = mi + mj;
                        // Lage: 80 % der Überlappung, die leichtere weicht mehr
                        var korr = tief * 0.8f;
                        _s[i] -= nS * korr * mj / summe; _q[i] -= nQ * korr * mj / summe;
                        _s[j] += nS * korr * mi / summe; _q[j] += nQ * korr * mi / summe;
                        // Tempo: unelastischer Stoß (Eis federt kaum), Reibung längs der Berührung dreht beide
                        var rel = (_vs[j] - _vs[i]) * nS + (_vq[j] - _vq[i]) * nQ;
                        if (rel < 0)
                        {
                            var stoss = -1.15f * rel / (1f / mi + 1f / mj);
                            _vs[i] -= stoss / mi * nS; _vq[i] -= stoss / mi * nQ;
                            _vs[j] += stoss / mj * nS; _vq[j] += stoss / mj * nQ;
                        }
                        var tang = (_vs[j] - _vs[i]) * -nQ + (_vq[j] - _vq[i]) * nS;
                        _om[i] += tang * 0.25f / Mathf.Max(ri, 0.2f) * dt * 4f;
                        _om[j] += tang * 0.25f / Mathf.Max(rj, 0.2f) * dt * 4f;
                        // Wer an einer hängenden auffährt, bremst mit (Stau)
                        if (_haft[i] > 0 && dS < 0 && _haft[j] <= 0) _haft[j] = Mathf.Min(_haft[i], 1.5f);
                        if (_haft[j] > 0 && dS > 0 && _haft[i] <= 0) _haft[i] = Mathf.Min(_haft[j], 1.5f);
                    }
        }

        Pruefungen += pruef;
        Beruehrungen += beruehrt0;
        StoesseMs += System.Diagnostics.Stopwatch.GetElapsedTime(stossStart).TotalMilliseconds;

        // 4. Hindernisse: nur die Schollen in den Zellen um jedes Hindernis
        foreach (var h in _festeHindernisse) HindernisPruefen(h, pfeiler: true, halbeLaenge, dt);
        foreach (var h in beweglich) HindernisPruefen(h, pfeiler: false, halbeLaenge, dt);

        // 5. Bewegen, Ufer, Ende
        for (var i = 0; i < n; i++)
        {
            var ri = _rStoss[i];
            if (ri <= 0 || _weg[i]) continue;
            _s[i] += _vs[i] * dt;
            _q[i] += _vq[i] * dt;
            _gier[i] += _om[i] * dt;
            if (_s[i] >= _laenge) _s[i] -= _laenge;
            else if (_s[i] < 0) _s[i] += _laenge;
            _halbHier[i] = HalbAn(_s[i]);
            var platz = _halbHier[i] - randeis - ri * 0.7f;
            if (platz < 0) platz = 0;
            if (MathF.Abs(_q[i]) > platz)
            {
                var seite = MathF.Sign(_q[i]);
                _q[i] = seite * platz;
                if (_vq[i] * seite > 0) _vq[i] *= -0.3f; // an der Eiskante abprallen
                _vs[i] *= 0.98f;
            }
        }
    }

    /// <summary>Halbe Breite an der Stelle (nur sie, ohne Ort und Richtung: billiger als <see cref="Punkt"/>).</summary>
    private float HalbAn(float s)
    {
        var i = Math.Clamp((int)s, 0, _lauf.Length - 2);
        var t = Mathf.Clamp(s - i, 0f, 1f);
        return _lauf[i].Halb + (_lauf[i + 1].Halb - _lauf[i].Halb) * t;
    }

    /// <summary>Die Schollen in den Zellen um ein Hindernis (über das Gitter, auch über das Ende des Laufs hinweg) gegen das Hindernis prüfen.</summary>
    private void HindernisPruefen(Hindernis h, bool pfeiler, float halbeLaenge, float dt)
    {
        var rS = h.HalbLaengs + _rStossMax + 1f;
        var rQ = h.HalbQuer + _rStossMax + 1f;
        var q0 = Math.Max((int)((h.Quer - rQ + _halbMax) / Zelle), 0);
        var q1 = Math.Min((int)((h.Quer + rQ + _halbMax) / Zelle), _zellenQ - 1);
        var s0 = (int)MathF.Floor((h.Stelle - rS) / Zelle);
        var s1 = (int)MathF.Floor((h.Stelle + rS) / Zelle);
        for (var cs = s0; cs <= s1; cs++)
        {
            var c = ((cs % _zellenS) + _zellenS) % _zellenS;
            for (var cq = q0; cq <= q1; cq++)
                for (var j = _kopf[c * _zellenQ + cq]; j >= 0; j = _naechster[j])
                    Abprallen(j, _rStoss[j], h, pfeiler, halbeLaenge, dt);
        }
    }

    private int Zellindex(float s, float q)
    {
        var cs = Math.Clamp((int)(s / Zelle), 0, _zellenS - 1);
        var cq = Math.Clamp((int)((q + _halbMax) / Zelle), 0, _zellenQ - 1);
        return cs * _zellenQ + cq;
    }

    /// <summary>
    /// Stoß einer Scholle (Kreis, Stoßhalbmesser <paramref name="ri"/>) gegen ein Hindernis als Kapsel entlang seiner längeren Seite: herausschieben,
    /// das Tempo in die Wand hinein umkehren (wenig federnd), längs der Wand etwas bremsen und drehen. Ein Pfeiler, den sie von vorn (stromauf) trifft,
    /// hält sie beim ersten Stoß 1–6 s fest; danach schiebt die Strömung sie seitlich am Pfeilerkopf vorbei. Liefert, ob sie das Hindernis berührt.
    /// </summary>
    private bool Abprallen(int i, float ri, Hindernis h, bool pfeiler, float halbeLaenge, float dt)
    {
        var dS = _s[i] - h.Stelle;
        if (dS > halbeLaenge) dS -= _laenge;
        else if (dS < -halbeLaenge) dS += _laenge;
        var dQ = _q[i] - h.Quer;
        if (MathF.Abs(dS) > h.HalbLaengs + ri + 1f || MathF.Abs(dQ) > h.HalbQuer + ri + 1f) return false;
        // Kapsel: Strecke entlang der längeren Seite, Halbmesser = kürzere halbe Seite
        float rh, cS = 0, cQ = 0;
        if (h.HalbLaengs >= h.HalbQuer)
        {
            rh = h.HalbQuer;
            cS = Mathf.Clamp(dS, -(h.HalbLaengs - rh), h.HalbLaengs - rh);
        }
        else
        {
            rh = h.HalbLaengs;
            cQ = Mathf.Clamp(dQ, -(h.HalbQuer - rh), h.HalbQuer - rh);
        }
        var (aS, aQ) = (dS - cS, dQ - cQ);
        var d = MathF.Sqrt(aS * aS + aQ * aQ);
        var tief = ri + rh - d;
        if (tief <= 0) return false;
        float nS, nQ;
        if (d > 1e-4f) (nS, nQ) = (aS / d, aQ / d);
        else (nS, nQ) = (-1f, 0f);
        _s[i] += nS * tief;
        _q[i] += nQ * tief;
        var vn = _vs[i] * nS + _vq[i] * nQ;
        if (vn < 0)
        {
            // in die Wand hinein: umkehren, kaum federnd; längs der Wand nur leichte Reibung (sonst klebte sie am Pfeiler)
            var vt = _vs[i] * -nQ + _vq[i] * nS;
            vt *= 1f - Mathf.Min(0.8f * dt, 0.5f);
            vn *= -0.15f;
            _vs[i] = vn * nS - vt * nQ;
            _vq[i] = vn * nQ + vt * nS;
            _om[i] += vt * 0.9f / Mathf.Max(ri, 0.2f) * dt; // Reibung an der Wand dreht
            // Von vorn gegen den Pfeiler: kurz hängen bleiben (Stau), dann gleitet sie seitlich vorbei
            if (pfeiler && _pfeilerSchritt[i] < _schritt - 1 && nS < -0.55f && _haft[i] <= 0)
            {
                _zufall = _zufall * 1664525u + 1013904223u;
                _haft[i] = 1f + 5f * ((_zufall >> 8) * (1f / 16777216f));
                // zur Seite, auf der sie schon etwas liegt, einen kleinen Schub für das spätere Vorbeigleiten
                _vq[i] += (MathF.Abs(dQ) > 0.05f ? MathF.Sign(dQ) : ((_zufall & 1) == 0 ? 1f : -1f)) * 0.12f;
            }
        }
        if (pfeiler) _pfeilerSchritt[i] = _schritt;
        // Vorn am Pfeilerkopf, nicht (mehr) festgehalten: zur Seite abgleiten, auf der sie liegt (sonst balancierte sie auf der Spitze)
        if (pfeiler && nS < -0.3f && _haft[i] <= 0)
            _vq[i] += (MathF.Abs(dQ) > 0.02f ? MathF.Sign(dQ) : (_schollen[i].Saat % 2 < 1 ? 1f : -1f)) * 0.9f * dt;
        return true;
    }

    private static void Schreiben(MultiMeshInstance3D knoten, float[] puffer, int zahl)
    {
        var mm = knoten.Multimesh;
        if (zahl > 0) RenderingServer.MultimeshSetBuffer(mm.GetRid(), puffer);
        mm.VisibleInstanceCount = zahl;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Netz der Scheibe
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die runde Scheibe, aus der der Shader jede Scholle formt: Ringe um die Mitte mit UV.x = Halbmesser (0–1), UV.y = Höhe in Dicken (0 oben,
    /// −1 unten), UV2.x = Rolle (0 Oberseite, dazwischen die gerundete Kante, 1 Seite). <paramref name="teile"/> Ecken je Ring; grob: weniger Ringe.
    /// Ohne Boden (er liegt unter Wasser und zeigt nach unten).
    /// </summary>
    public static ArrayMesh ScheibenNetz(int teile, bool fein)
    {
        // (Halbmesser, Höhe, Rolle)
        var ringe = fein
            ? new[] { (0.55f, 0f, 0f), (0.86f, 0f, 0f), (0.96f, -0.05f, 0.45f), (1f, -0.2f, 0.85f), (1f, -0.45f, 1f), (0.95f, -1f, 1f) }
            : new[] { (0.85f, 0f, 0f), (1f, -0.15f, 0.7f), (0.96f, -1f, 1f) };
        var ecken = new List<Vector3> { Vector3.Zero };
        var uv = new List<Vector2> { Vector2.Zero };
        var uv2 = new List<Vector2> { Vector2.Zero };
        foreach (var (halb, hoehe, rolle) in ringe)
            for (var k = 0; k < teile; k++)
            {
                var w = Mathf.Tau * k / teile;
                ecken.Add(new Vector3(MathF.Cos(w), 0, MathF.Sin(w)));
                uv.Add(new Vector2(halb, hoehe));
                uv2.Add(new Vector2(rolle, 0));
            }
        var idx = new List<int>();
        // Fächer um die Mitte, dann Bänder zwischen den Ringen; von oben bzw. außen gesehen im Uhrzeigersinn (Godots Vorderseite, wie
        // DreieckNachOben in Stadtansicht.Wasser.cs)
        for (var k = 0; k < teile; k++)
            idx.AddRange(new[] { 0, 1 + k, 1 + (k + 1) % teile });
        for (var r = 0; r + 1 < ringe.Length; r++)
            for (var k = 0; k < teile; k++)
            {
                int a = 1 + r * teile + k, b = 1 + r * teile + (k + 1) % teile, c = a + teile, d = b + teile;
                idx.AddRange(new[] { a, c, b, b, c, d });
            }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = ecken.ConvertAll(_ => Vector3.Up).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV2] = uv2.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        netz.CustomAabb = new Aabb(new Vector3(-2f, -1f, -2f), new Vector3(4f, 2f, 4f)); // der Shader streckt den Umriss bis etwa 1,8
        return netz;
    }
}
