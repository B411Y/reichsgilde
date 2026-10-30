using System;
using System.Collections.Generic;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Eisschollen auf einem Fluss als Körper (realistisches Wasser, Phase 3c/3d und Schollen 2, <c>docs/wasser/phase3.md</c>): Jede Scholle ist eine
/// Kopie derselben runden Scheibe (<see cref="ScheibenNetz"/>) in einem <see cref="MultiMesh"/>; ihre gebrochene Form, die Dicke, das Schaukeln auf
/// den Wellen und die Oberseite (gemeinsames Eismaterial) rechnet <c>eis_scholle.gdshaderinc</c>. Zwei Sorten:
/// <list type="bullet">
/// <item><b>Treibende Schollen:</b> Ihre Bewegung rechnet <see cref="Schollensimulation"/> (eigene 2D-Simulation in den Laufachsen, weiche Kontakte,
/// Bruch, Untertauchen, Schmelzen); diese Klasse setzt das Stoßfenster an den Blickpunkt und zeichnet die sichtbaren (<see cref="Bild"/>), Bruchstücke
/// als Ausschnitt derselben Scheibe, schmelzende runder und tiefer. Reine Darstellung: nicht im Kern, nicht deterministisch.</item>
/// <item><b>Feste Schollen</b> im Randeis (<see cref="FesteBauen"/>): festgefrorene Schollen am Ufer, so wie das Randeis großer Flüsse aus
/// angefrorenem Treibeis und Eisbrei besteht. Jede hat eine Schwelle: Sie wächst auf, sobald die Eiskante des Shaders sie erreicht (Uniform
/// <c>eis_fest</c>), die CPU schreibt sie nur einmal; gezeichnet in Stücken zu 32 m, nah fein, fern grob.</item>
/// </list>
/// Je Kopie (20 Zahlen): Lage (Drehung um die Hochachse, beim Tauchen gekippt, gleichmäßig auf den Halbmesser skaliert), COLOR = (Saat der Form,
/// Dicke, Ausschnitt-Ende β + 8 oder 0 = ganz, −Schmelzgrad; feste: Schnee und Schwelle > 0 in b und a), CUSTOM = (quer, Stelle, Winkel der
/// Fließrichtung, Ausschnitt-Anfang α + 8 oder 0) – keine instance uniforms.
/// Die Wellen müssen dieselben sein wie auf der Wasserfläche: <see cref="Wasserarten.WellenSetzen"/> belegt den Stoff der Schollen wie den des Flusses.
/// </summary>
public sealed class Schollenfeld
{
    /// <summary>Bis zu diesem Abstand (m) zeichnet die feine Scheibe, dahinter die grobe.</summary>
    private const float NahBis = 70f;

    /// <summary>Stücke der festen Schollen entlang des Laufs (m) und bis zu welchem Abstand (m) sie fein gezeichnet werden.</summary>
    private const float FestStueck = 32f, FestNahBis = 60f;

    /// <summary>Versatz eines Ausschnittwinkels in COLOR.b und CUSTOM.w (0 = ganze Scholle, α = 0 bleibt gültig).</summary>
    public const float SektorVersatz = 8f;

    private readonly Schollensimulation _sim;
    private readonly Schollensimulation.Laufpunkt[] _lauf;
    private readonly float _laenge, _randeis;
    private readonly List<Schollensimulation.Hindernis> _festeHindernisse;
    private readonly MultiMeshInstance3D _nah, _fern;
    private readonly float[] _pufferNah, _pufferFern;
    private readonly int _platz;
    private float _eisFestZuletzt = -1, _tauenZuletzt = -1;
    private int _kameraTipp = -1, _blickTipp = -1;

    private static readonly StringName ParamEisFest = "eis_fest", ParamEisTauen = "eis_tauen";

    /// <summary>Startoption <c>--schollenmessung=1</c> (vor dem Bau setzen): Messung des Verhaltens (<see cref="Schollenmessung"/>).</summary>
    public static bool MessungAn;

    /// <summary>Startoption <c>--schollenumriss=1</c> (vor dem Bau setzen): Umrisse und Stoßkreise der C#-Rechnung als Linien über den Schollen.</summary>
    public static bool UmrissAn;

    /// <summary>Messung des Verhaltens (null ohne <c>--schollenmessung=1</c>).</summary>
    public Schollenmessung? Messung => _sim.Messung;

    /// <summary>Name des Laufs (für das Protokoll).</summary>
    public string Name { get; }

    /// <summary>Der Knoten mit den Schollen-Netzen (in die Szene hängen).</summary>
    public Node3D Knoten { get; }

    /// <summary>Der Stoff der Schollen (Wellen-Uniforms wie der Fluss; <c>eis_fest</c> für die festen).</summary>
    public ShaderMaterial Stoff { get; }

    /// <summary>Wie viele treibende Schollen es gibt, wie viele im letzten Bild gezeichnet wurden und wie viele feste im Randeis liegen.</summary>
    public int Anzahl => _sim.Anzahl;
    public int Gezeichnet { get; private set; }
    public int Feste { get; private set; }

    /// <summary>Rechenzeit der Simulation im letzten Bild (ms), aktive Schollen, Kontakte, Schritte, Suche und Lösen (für die Messung).</summary>
    public double SimulationMs => _sim.SimulationMs;
    public int Aktiv => _sim.Aktiv;
    public int Kontakte => _sim.Kontakte;
    public int Teilschritte => _sim.Teilschritte;
    public double SucheMs => _sim.SucheMs;
    public double LoesenMs => _sim.LoesenMs;
    public int ImFenster => _sim.ImFenster;

    /// <summary>Protokoll je Bruch (Startoption <c>--schollenbruch</c>).</summary>
    public Action<string>? BruchProtokoll { set => _sim.BruchProtokoll = value; }

    /// <summary>Zeit für das Schreiben der Kopien im letzten Bild (ms).</summary>
    public double ZeichnenMs { get; private set; }

    /// <param name="lauf">Die Mittellinie je Meter (Index = Stelle in m), mindestens zwei Punkte.</param>
    /// <param name="tempo">Strömung des Flusses (m/s); die Schollen treiben mit 0,9 davon.</param>
    /// <param name="randeis">Reichweite des Randeises bei vollem Eis (m): so weit bleiben die Schollen vom Ufer weg.</param>
    /// <param name="saat">Startwert (aus der Karte): gleiche Karte, gleiche Schollen.</param>
    /// <param name="stufe">Grafikstufe: Niedrig nur große Schollen (einfacher Shader) und keine festen, Mittel ohne die kleinsten, Hoch/Ultra alle (mit Luftblasen).</param>
    /// <param name="hindernisse">Feste Hindernisse (Brückenpfeiler) in Laufachsen.</param>
    /// <param name="festFrei">Ob an einem Weltort mit Halbmesser eine feste Scholle liegen darf (Rinnen, Löcher, Kai); null = überall.</param>
    public Schollenfeld(Schollensimulation.Laufpunkt[] lauf, float tempo, float randeis, int saat, Wasserstufe stufe, List<Schollensimulation.Hindernis> hindernisse,
        string name, Func<Vector2, float, bool>? festFrei = null)
    {
        _lauf = lauf;
        Name = name;
        _laenge = lauf.Length - 1;
        _randeis = randeis;
        _festeHindernisse = hindernisse;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var kleinste = stufe switch { Wasserstufe.Niedrig => 0.75f, Wasserstufe.Mittel => 0.45f, _ => 0.25f };
        _sim = new Schollensimulation(lauf, tempo, randeis, saat, kleinste, hindernisse, MessungAn);
        var bauMs = uhr.Elapsed.TotalMilliseconds;
        _platz = Math.Min(_sim.Plaetze, stufe >= Wasserstufe.Hoch ? 2400 : 1600);

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
        if (stufe > Wasserstufe.Niedrig) FesteBauen(saat, festFrei);
        if (UmrissAn) UmrissAnlegen();
        GD.Print($"[wasser] Schollen {name}: Simulation und Formen ({Schollensimulation.SaatZahl} Saaten) in {bauMs:0} ms, {_sim.Plaetze - _sim.Anzahl} Plätze Vorrat für Bruchstücke");
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

    /// <summary>
    /// Der nächste Punkt der Mittellinie zu einem Weltort: (quer, Stelle) in Laufachsen und der Abstand (für Hindernisse). <paramref name="tipp"/> ist
    /// der Index vom letzten Mal (−1: ganz suchen).
    /// </summary>
    public (float Quer, float Stelle, float Abstand) InLaufachsen(Vector2 ort, ref int tipp) => _sim.InLaufachsen(ort, ref tipp);

    // ═════════════════════════════════════════════════════════════════════════
    //  Feste Schollen im Randeis
    // ═════════════════════════════════════════════════════════════════════════

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
        var versuche = (int)(_laenge * 2f * band * 1.8f); // dicht: Randeis großer Flüsse ist fast ganz angefrorenes Treibeis
        for (var k = 0; k < versuche; k++)
        {
            var u = (float)rnd.NextDouble();
            var r = 0.45f + 1.3f * u * u;
            var s = 3f + (float)rnd.NextDouble() * (_laenge - 6f);
            var seite = rnd.NextDouble() < 0.5 ? -1f : 1f;
            var d = 0.3f + (float)rnd.NextDouble() * band; // Abstand der Mitte vom Ufer
            var p = _sim.Punkt(s);
            if (d > p.Halb * 0.45f) continue; // schmaler Fluss: nicht über die Mitte hinaus
            var q = seite * (p.Halb - d);
            var querW = new Vector2(-p.Richtung.Y, p.Richtung.X);
            var ort = p.Ort + querW * q;
            // Schwelle: die Kante des Shaders bei Eis e liegt bei e · Reichweite + Buchten · Anlauf(e); die Scholle erscheint, wenn sie 35 % ihres
            // Halbmessers vor der Mitte steht
            var bucht = (Schollensimulation.WFbm(ort.X * 0.045f + 17f, ort.Y * 0.045f + 17f) - 0.5f) * 2f * unruhe;
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
                            var min = 0.78f * (o.R + r);
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
                var p = _sim.Punkt(s);
                var querW = new Vector2(-p.Richtung.Y, p.Richtung.X);
                var ort2 = p.Ort + querW * q;
                var gier = (float)rnd.NextDouble() * Mathf.Tau;
                var (sin, cos) = MathF.SinCos(gier);
                var o = n * 20;
                puffer[o + 0] = cos * r; puffer[o + 1] = 0; puffer[o + 2] = sin * r; puffer[o + 3] = ort2.X;
                puffer[o + 4] = 0; puffer[o + 5] = r; puffer[o + 6] = 0; puffer[o + 7] = p.Spiegel;
                puffer[o + 8] = -sin * r; puffer[o + 9] = 0; puffer[o + 10] = cos * r; puffer[o + 11] = ort2.Y;
                // COLOR: Form, Dicke (am Ufer dicker), Schnee (mehr: altes Eis), Schwelle; CUSTOM wie bei den treibenden (ganze Scholle, kein Ausschnitt)
                puffer[o + 12] = rnd.Next(0, 9973); puffer[o + 13] = 0.14f + 0.14f * (float)rnd.NextDouble();
                puffer[o + 14] = 0.25f + 0.45f * (float)rnd.NextDouble(); puffer[o + 15] = schwelle;
                puffer[o + 16] = q; puffer[o + 17] = s; puffer[o + 18] = MathF.Atan2(p.Richtung.Y, p.Richtung.X); puffer[o + 19] = 0;
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
    //  Jedes Bild: Simulation, dann zeichnen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Schiebt die Schollen weiter (<see cref="Schollensimulation.Bild"/>, Stoßfenster am Blickpunkt der Kamera auf dem Wasser) und schreibt die
    /// sichtbaren in die beiden Netze. <paramref name="eis"/> ist der Eisfortschritt der Art (0–1): Dichte, Breite des Randeises und die festen
    /// Schollen; <paramref name="schollenAn"/> 0 = keine treibenden Schollen (dann kostet es nichts). <paramref name="beweglich"/>: Hindernisse, die
    /// sich bewegen (Prahm, Kähne), in Laufachsen mit ihrem Tempo. <paramref name="tauen"/>: Eisgang am Winterende (<see cref="Wasserarten.Tauwetter"/>):
    /// Das Randeis bricht auf, es treiben erst mehr Schollen, dann schmelzen sie (kleiner, dünner, grau), bei 1 ist nichts mehr da.
    /// </summary>
    public void Bild(double delta, Camera3D? kamera, float eis, float schollenAn, List<Schollensimulation.Hindernis> beweglich, float tauen = 0f)
    {
        if (Messung is null)
        {
            BildRechnen(delta, kamera, eis, schollenAn, beweglich, tauen);
            return;
        }
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var lief = BildRechnen(delta, kamera, eis, schollenAn, beweglich, tauen);
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        if (lief) Messung.Bild(delta, _sim.SBlick, _sim.Kontakte, _sim.SimulationMs, bytes, _sim.Obergrenze);
    }

    private bool BildRechnen(double delta, Camera3D? kamera, float eis, float schollenAn, List<Schollensimulation.Hindernis> beweglich, float tauen)
    {
        if (MathF.Abs(eis - _eisFestZuletzt) > 0.0005f)
        {
            _eisFestZuletzt = eis;
            Stoff.SetShaderParameter(ParamEisFest, eis);
        }
        if (MathF.Abs(tauen - _tauenZuletzt) > 0.0005f)
        {
            _tauenZuletzt = tauen;
            Stoff.SetShaderParameter(ParamEisTauen, tauen);
        }
        var dichte = schollenAn * Mathf.SmoothStep(0.15f, 0.85f, eis) * Mathf.Lerp(0.75f, 1f, Mathf.SmoothStep(0f, 0.3f, tauen))
                     * (1f - Mathf.SmoothStep(0.65f, 1f, tauen));
        var festeSichtbar = Feste > 0 && eis > 0.001f && tauen < 0.999f;
        if ((dichte <= 0.001f && !festeSichtbar) || kamera is null || !GodotObject.IsInstanceValid(kamera))
        {
            if (Knoten.Visible) Knoten.Visible = false;
            return false;
        }
        if (!Knoten.Visible) Knoten.Visible = true;
        if (dichte <= 0.001f)
        {
            Schreiben(_nah, _pufferNah, 0);
            Schreiben(_fern, _pufferFern, 0);
            Gezeichnet = 0;
            return false;
        }

        // Das Stoßfenster folgt dem Blickpunkt auf dem Wasser (aus Spielhöhe liegt der sichtbare Fluss oft 50–150 m vor der Kamera)
        var lage = kamera.GetCameraTransform();
        var auge = lage.Origin;
        var sKamera = _sim.InLaufachsen(new Vector2(auge.X, auge.Z), ref _kameraTipp).Stelle;
        var sBlick = sKamera;
        var vorn = -lage.Basis.Z;
        if (vorn.Y < -0.05f)
        {
            var spiegel = _sim.Punkt(sKamera).Spiegel;
            var t = (spiegel - auge.Y) / vorn.Y;
            if (t > 0f && t < 400f)
            {
                var treffer = auge + vorn * t;
                sBlick = _sim.InLaufachsen(new Vector2(treffer.X, treffer.Z), ref _blickTipp).Stelle;
            }
        }
        // Das Randeis bricht im Eisgang weg: Die Schollen bekommen nach und nach die ganze Breite
        var eisRand = eis * (1f - Mathf.SmoothStep(0.15f, 0.85f, tauen));
        _sim.Bild(delta, Mathf.Clamp(sBlick, 0f, _laenge), dichte, eisRand, tauen, beweglich);

        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        // Sichtkegel ohne Speicheranforderung (GetFrustum legt jedes Bild ein neues Feld an): Ebenen der Projektion, in die Welt gedreht
        Span<Plane> plan = stackalloc Plane[6];
        var zahlEbenen = Sichtkegel(kamera, plan);
        var (nahZahl, fernZahl) = (0, 0);
        var sim = _sim;
        for (var i = 0; i < sim.Plaetze; i++)
        {
            if (sim.Zustand[i] == Schollenmessung.Aus) continue;
            var r = sim.ZR[i];
            var senk = sim.ZSenk[i];
            if (r < 0.06f || senk > 5f) continue;
            // grobe Lage (Schwerpunkt) für die Sichtprüfung
            var p = sim.Punkt(sim.ZS[i]);
            var querW = new Vector2(-p.Richtung.Y, p.Richtung.X);
            var mitte2 = p.Ort + querW * sim.ZQ[i];
            var mitte = new Vector3(mitte2.X, p.Spiegel, mitte2.Y);
            var drin = true;
            for (var e = 0; e < zahlEbenen; e++)
                if (plan[e].DistanceTo(mitte) > r * 1.3f + 0.5f) { drin = false; break; }
            if (!drin) continue;
            var weite = auge.DistanceTo(mitte);
            // sehr kleine Schollen nur nah, kleine nur mittelweit (sie wären weniger als ein paar Bildpunkte)
            var r0 = sim.Ausgangshalbmesser(i);
            if ((weite > 140f && r0 < 0.55f) || (weite > 300f && r0 < 1.2f)) continue;
            var nah = weite < NahBis;
            if (nah ? nahZahl >= _platz : fernZahl >= _platz) continue;
            var puffer = nah ? _pufferNah : _pufferFern;
            var o = (nah ? nahZahl++ : fernZahl++) * 20;

            // Ursprung des Netzes (neben dem Schwerpunkt) und Gier: Winkel in Laufachsen → Welt (g = −Winkel − ψ)
            var (os, oq) = sim.Ursprung(i);
            var po = sim.Punkt(os);
            var querO = new Vector2(-po.Richtung.Y, po.Richtung.X);
            var ort2 = po.Ort + querO * oq;
            var psi = MathF.Atan2(po.Richtung.Y, po.Richtung.X);
            var saat = sim.Saat(i);
            // kleiner fester Höhenversatz je Saat: übereinander geschobene Schollen flimmern nicht (Tiefenkampf)
            var hub = Schollensimulation.WHash(saat, 13f) * 0.008f;
            var (sin, cos) = MathF.SinCos(-sim.ZW[i] - psi);
            var kipp = sim.ZKipp[i];
            if (kipp == 0f)
            {
                // Lage: Drehung um die Hochachse, gleichmäßig auf den Halbmesser skaliert (Zeilen der Basis, dann der Ort)
                puffer[o + 0] = cos * r; puffer[o + 1] = 0; puffer[o + 2] = sin * r;
                puffer[o + 4] = 0; puffer[o + 5] = r; puffer[o + 6] = 0;
                puffer[o + 8] = -sin * r; puffer[o + 9] = 0; puffer[o + 10] = cos * r;
            }
            else
            {
                // Tauchen: um die Querachse gekippt (Vorderkante stromab nach unten), Basis = Kippen · Gier · r
                var (ax, az) = (querO.X, querO.Y);
                var (ks, kc) = MathF.SinCos(kipp);
                var k1 = 1f - kc;
                // Rodrigues mit Achse (ax, 0, az): Zeilen der Kippmatrix
                var (a00, a01, a02) = (1f - k1 * az * az, -ks * az, k1 * ax * az);
                var (a10, a11, a12) = (ks * az, 1f - k1 * (ax * ax + az * az), -ks * ax);
                var (a20, a21, a22) = (k1 * ax * az, ks * ax, 1f - k1 * ax * ax);
                // Gier: Zeilen (cos, 0, sin), (0, 1, 0), (−sin, 0, cos)
                puffer[o + 0] = (a00 * cos - a02 * sin) * r; puffer[o + 1] = a01 * r; puffer[o + 2] = (a00 * sin + a02 * cos) * r;
                puffer[o + 4] = (a10 * cos - a12 * sin) * r; puffer[o + 5] = a11 * r; puffer[o + 6] = (a10 * sin + a12 * cos) * r;
                puffer[o + 8] = (a20 * cos - a22 * sin) * r; puffer[o + 9] = a21 * r; puffer[o + 10] = (a20 * sin + a22 * cos) * r;
            }
            puffer[o + 3] = ort2.X; puffer[o + 7] = po.Spiegel + hub - senk; puffer[o + 11] = ort2.Y;
            // COLOR: Form, Dicke, Ende des Ausschnitts (0 = ganz), −Schmelzgrad (treibend); CUSTOM: Laufachsen des Schwerpunkts, Winkel der
            // Fließrichtung, Anfang des Ausschnitts
            var sekA = sim.SektorA(i);
            puffer[o + 12] = saat; puffer[o + 13] = sim.ZDicke[i]; puffer[o + 14] = sekA < 0 ? 0f : sim.SektorB(i) + SektorVersatz; puffer[o + 15] = -sim.ZM[i];
            puffer[o + 16] = sim.ZQ[i]; puffer[o + 17] = sim.ZS[i]; puffer[o + 18] = psi; puffer[o + 19] = sekA < 0 ? 0f : sekA + SektorVersatz;
        }
        Schreiben(_nah, _pufferNah, nahZahl);
        Schreiben(_fern, _pufferFern, fernZahl);
        Gezeichnet = nahZahl + fernZahl;
        ZeichnenMs = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if (_umriss is not null) UmrissZeichnen(auge);
        return true;
    }

    private static bool _kegelGeprueft;

    /// <summary>
    /// Die sechs Ebenen des Sichtkegels in der Welt (Normalen nach außen, wie <see cref="Camera3D.GetFrustum"/>), ohne Speicheranforderung. Beim
    /// ersten Aufruf einmal gegen GetFrustum geprüft (Protokoll).
    /// </summary>
    public static int Sichtkegel(Camera3D kamera, Span<Plane> plan)
    {
        var proj = kamera.GetCameraProjection();
        var lage = kamera.GetCameraTransform();
        for (var i = 0; i < 6; i++) plan[i] = lage * proj.GetProjectionPlane((Projection.Planes)i);
        if (!_kegelGeprueft)
        {
            _kegelGeprueft = true;
            var echt = kamera.GetFrustum();
            var abweichung = 0f;
            for (var i = 0; i < Math.Min(echt.Count, 6); i++)
                abweichung = Mathf.Max(abweichung, (echt[i].Normal - plan[i].Normal).Length() + Mathf.Abs(echt[i].D - plan[i].D) / Mathf.Max(1f, Mathf.Abs(echt[i].D)));
            if (abweichung > 1e-3f) GD.PushWarning($"[wasser] Sichtkegel der Schollen weicht von GetFrustum ab ({abweichung:0.0000})");
            else GD.Print($"[wasser] Sichtkegel der Schollen gleich GetFrustum (Abweichung {abweichung:0.000000})");
        }
        return 6;
    }

    private static void Schreiben(MultiMeshInstance3D knoten, float[] puffer, int zahl)
    {
        var mm = knoten.Multimesh;
        if (zahl > 0) RenderingServer.MultimeshSetBuffer(mm.GetRid(), puffer);
        mm.VisibleInstanceCount = zahl;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Prüfhilfe: Umrisse und Stoßkreise (--schollenumriss=1)
    // ═════════════════════════════════════════════════════════════════════════

    private ImmediateMesh? _umriss;

    private void UmrissAnlegen()
    {
        _umriss = new ImmediateMesh();
        var stoff = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, NoDepthTest = true,
        };
        Knoten.AddChild(new MeshInstance3D { Name = "Umrisse", Mesh = _umriss, MaterialOverride = stoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Gelb der Umriss aus C# (wie gezeichnet), rot die Stoßkreise – für Schollen bis 60 m von der Kamera (nur zur Prüfung, legt Speicher an).</summary>
    private void UmrissZeichnen(Vector3 auge)
    {
        var m = _umriss!;
        m.ClearSurfaces();
        Span<float> ps = stackalloc float[32], pq = stackalloc float[32];
        var begonnen = false;
        var gelb = new Color(1f, 0.9f, 0.1f);
        var rot = new Color(1f, 0.2f, 0.1f);
        for (var i = 0; i < _sim.Plaetze; i++)
        {
            if (_sim.Zustand[i] != Schollenmessung.ImFenster || _sim.ZR[i] < 0.06f || _sim.ZSenk[i] > 0.01f) continue;
            var p = _sim.Punkt(_sim.ZS[i]);
            var w2 = p.Ort + new Vector2(-p.Richtung.Y, p.Richtung.X) * _sim.ZQ[i];
            if (auge.DistanceTo(new Vector3(w2.X, p.Spiegel, w2.Y)) > 60f) continue;
            if (!begonnen)
            {
                m.SurfaceBegin(Mesh.PrimitiveType.Lines);
                begonnen = true;
            }
            var n = _sim.UmrissInLauf(i, ps, pq);
            var hoehe = p.Spiegel + 0.08f;
            for (var k = 0; k < n; k++)
            {
                var k2 = (k + 1) % n;
                m.SurfaceSetColor(gelb);
                m.SurfaceAddVertex(Welt(ps[k], pq[k], hoehe));
                m.SurfaceSetColor(gelb);
                m.SurfaceAddVertex(Welt(ps[k2], pq[k2], hoehe));
            }
            for (var k = 0; k < 2; k++)
            {
                var (ks, kq, kr) = _sim.Kreis(i, k);
                if (kr <= 0) continue;
                for (var a = 0; a < 16; a++)
                {
                    var (w0, w1) = (a * Mathf.Tau / 16, (a + 1) * Mathf.Tau / 16);
                    m.SurfaceSetColor(rot);
                    m.SurfaceAddVertex(Welt(ks + kr * MathF.Cos(w0), kq + kr * MathF.Sin(w0), hoehe));
                    m.SurfaceSetColor(rot);
                    m.SurfaceAddVertex(Welt(ks + kr * MathF.Cos(w1), kq + kr * MathF.Sin(w1), hoehe));
                }
            }
        }
        if (begonnen) m.SurfaceEnd();
    }

    private Vector3 Welt(float s, float q, float hoehe)
    {
        var p = _sim.Punkt(s);
        var w = p.Ort + new Vector2(-p.Richtung.Y, p.Richtung.X) * q;
        return new Vector3(w.X, hoehe, w.Y);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Netz der Scheibe
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die runde Scheibe, aus der der Shader jede Scholle formt: Ringe um die Mitte mit UV.x = Halbmesser (0–1), UV.y = Höhe in Dicken (0 oben,
    /// −1 unten), UV2.x = Rolle (0 Oberseite, dazwischen die gerundete Kante, 1 Seite, 2 Boden), UV2.y = Zahl der Ecken je Ring (für den Schnitt
    /// eines Bruchstücks im Shader). <paramref name="teile"/> Ecken je Ring; grob: weniger Ringe. Mit Boden (beim Tauchen und Kippen sieht man ihn).
    /// </summary>
    public static ArrayMesh ScheibenNetz(int teile, bool fein)
    {
        // (Halbmesser, Höhe, Rolle)
        var ringe = fein
            ? new[] { (0.55f, 0f, 0f), (0.86f, 0f, 0f), (0.96f, -0.05f, 0.45f), (1f, -0.2f, 0.85f), (1f, -0.45f, 1f), (0.95f, -1f, 1f) }
            : new[] { (0.85f, 0f, 0f), (1f, -0.15f, 0.7f), (0.96f, -1f, 1f) };
        var ecken = new List<Vector3> { Vector3.Zero };
        var uv = new List<Vector2> { Vector2.Zero };
        var uv2 = new List<Vector2> { new(0f, teile) };
        foreach (var (halb, hoehe, rolle) in ringe)
            for (var k = 0; k < teile; k++)
            {
                var w = Mathf.Tau * k / teile;
                ecken.Add(new Vector3(MathF.Cos(w), 0, MathF.Sin(w)));
                uv.Add(new Vector2(halb, hoehe));
                uv2.Add(new Vector2(rolle, teile));
            }
        // Mitte des Bodens
        var boden = ecken.Count;
        ecken.Add(Vector3.Zero);
        uv.Add(new Vector2(0f, -1f));
        uv2.Add(new Vector2(2f, teile));
        var idx = new List<int>();
        // Fächer um die Mitte, dann Bänder zwischen den Ringen; von oben bzw. außen gesehen im Uhrzeigersinn (Godots Vorderseite, wie
        // DreieckNachOben in Stadtansicht.Wasser.cs); der Boden von unten gesehen
        for (var k = 0; k < teile; k++)
            idx.AddRange(new[] { 0, 1 + k, 1 + (k + 1) % teile });
        for (var r = 0; r + 1 < ringe.Length; r++)
            for (var k = 0; k < teile; k++)
            {
                int a = 1 + r * teile + k, b = 1 + r * teile + (k + 1) % teile, c = a + teile, d = b + teile;
                idx.AddRange(new[] { a, c, b, b, c, d });
            }
        var letzter = 1 + (ringe.Length - 1) * teile;
        for (var k = 0; k < teile; k++)
            idx.AddRange(new[] { boden, letzter + (k + 1) % teile, letzter + k });
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
