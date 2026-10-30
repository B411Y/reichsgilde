using System;
using System.Collections.Generic;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Treibende Eisschollen auf einem Fluss als Körper (realistisches Wasser, Phase 3c, <c>docs/wasser/phase3.md</c>): Jede Scholle ist eine Kopie
/// derselben runden Scheibe (<see cref="ScheibenNetz"/>) in einem <see cref="MultiMesh"/>; ihre gebrochene Form, die Dicke und das Schaukeln auf den
/// Wellen rechnet <c>eis_scholle.gdshaderinc</c>. Diese Klasse verteilt die Schollen einmal (fester Startwert: gleiche Karte, gleiches Eis) und
/// schiebt sie jedes Bild mit der Strömung den Fluss hinab:
/// <list type="bullet">
/// <item><b>Verteilung</b> (<see cref="Verteilen"/>): von groß nach klein zufällig gesetzt, ohne Überlappung, in Feldern dichter Schollen und fast
/// freien Strecken (Rauschen entlang des Laufs, etwa 50 m). Jede Scholle hat eine Schwelle: Sie erscheint (wächst auf), sobald das Eis der Art
/// über sie steigt – im frühen Winter wenige, im tiefen Winter dichtes Treibeis.</item>
/// <item><b>Treiben</b> (<see cref="Bild"/>): alle Schollen gemeinsam mit 0,9 × Strömung (das Feld bleibt beisammen, keine Scholle läuft durch eine
/// andere), jede dreht sich langsam. Quer halten sie Abstand vom Randeis (das mit dem Winter breiter wird) und weichen Hindernissen seitlich aus:
/// Brückenpfeilern, dem Prahm der Fähre, Kähnen (<see cref="Hindernis"/>). Am Anfang und Ende des Laufs wachsen bzw. schwinden sie.</item>
/// <item><b>Zeichnen</b>: nur Schollen im Bild (Sichtkegel der Kamera); nah (bis 70 m) die feine Scheibe, weiter weg eine grobe, sehr kleine
/// Schollen nur nah. Kein Schattenwurf (die Schollen ragen nur Zentimeter aus dem Wasser).</item>
/// </list>
/// Die Wellen müssen dieselben sein wie auf der Wasserfläche: <see cref="Wasserarten.WellenSetzen"/> belegt den Stoff der Schollen wie den des Flusses.
/// </summary>
public sealed class Schollenfeld
{
    /// <summary>Ein Punkt der Mittellinie (je Meter): Ort, Höhe des Spiegels, halbe Breite, Fließrichtung (Einheitsvektor).</summary>
    public readonly record struct Laufpunkt(Vector2 Ort, float Spiegel, float Halb, Vector2 Richtung);

    /// <summary>Ein Hindernis in Laufachsen: quer (m), Stelle (m) und seine halbe Ausdehnung quer und längs. Schollen weichen ihm seitlich aus.</summary>
    public readonly record struct Hindernis(float Quer, float Stelle, float HalbQuer, float HalbLaengs);

    private struct Scholle
    {
        public float S0, Anteil, Halbmesser, Dicke, Gier, Dreh, Schwelle, Saat, Schnee;
    }

    /// <summary>Bis zu diesem Abstand (m) zeichnet die feine Scheibe, dahinter die grobe.</summary>
    private const float NahBis = 70f;

    /// <summary>So weit (m) hält eine Scholle mindestens vom Ufer bzw. von der Kante des Randeises (beim vollen Randeis um die Hälfte breiter: die Kante ist unregelmäßig).</summary>
    private const float UferAbstand = 0.8f;

    private readonly Laufpunkt[] _lauf;
    private readonly float _laenge, _tempo, _randeis;
    private readonly Scholle[] _schollen;
    private readonly List<Hindernis> _festeHindernisse;
    private readonly MultiMeshInstance3D _nah, _fern;
    private readonly float[] _pufferNah, _pufferFern;
    private readonly int _platz;
    private double _drift, _zeit;
    private float _eisZuletzt = -1;

    /// <summary>Der Knoten mit beiden Schollen-Netzen (in die Szene hängen).</summary>
    public Node3D Knoten { get; }

    /// <summary>Der Stoff der Schollen (Wellen-Uniforms wie der Fluss).</summary>
    public ShaderMaterial Stoff { get; }

    /// <summary>Wie viele Schollen es insgesamt gibt und wie viele im letzten Bild gezeichnet wurden.</summary>
    public int Anzahl => _schollen.Length;
    public int Gezeichnet { get; private set; }

    /// <param name="lauf">Die Mittellinie je Meter (Index = Stelle in m), mindestens zwei Punkte.</param>
    /// <param name="tempo">Strömung des Flusses (m/s); die Schollen treiben mit 0,9 davon.</param>
    /// <param name="randeis">Reichweite des Randeises bei vollem Eis (m): so weit bleiben die Schollen vom Ufer weg.</param>
    /// <param name="saat">Startwert (aus der Karte): gleiche Karte, gleiche Schollen.</param>
    /// <param name="stufe">Grafikstufe: Niedrig nur große Schollen (einfacher Shader), Mittel ohne die kleinsten, Hoch/Ultra alle (mit Luftblasen).</param>
    /// <param name="hindernisse">Feste Hindernisse (Brückenpfeiler) in Laufachsen.</param>
    public Schollenfeld(Laufpunkt[] lauf, float tempo, float randeis, int saat, Wasserstufe stufe, List<Hindernis> hindernisse, string name)
    {
        _lauf = lauf;
        _laenge = lauf.Length - 1;
        _tempo = Mathf.Max(tempo, 0.05f) * 0.9f;
        _randeis = randeis;
        _festeHindernisse = hindernisse;
        _schollen = Verteilen(saat, stufe);
        _platz = Math.Min(_schollen.Length, stufe >= Wasserstufe.Hoch ? 2400 : 1600);

        Stoff = new ShaderMaterial
        {
            Shader = GD.Load<Shader>(stufe >= Wasserstufe.Hoch ? "res://assets/shader/eis_scholle.gdshader" : "res://assets/shader/eis_scholle_einfach.gdshader"),
            ResourceName = "Eisschollen",
        };
        Knoten = new Node3D { Name = "Eisschollen_" + name };
        (_nah, _pufferNah) = Teil("Nah", ScheibenNetz(24, fein: true));
        (_fern, _pufferFern) = Teil("Fern", ScheibenNetz(10, fein: false));
        Knoten.Visible = false;
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
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        foreach (var p in _lauf)
        {
            min = min.Min(new Vector3(p.Ort.X - p.Halb, p.Spiegel - 1f, p.Ort.Y - p.Halb));
            max = max.Max(new Vector3(p.Ort.X + p.Halb, p.Spiegel + 1f, p.Ort.Y + p.Halb));
        }
        mm.CustomAabb = new Aabb(min, max - min);
        var knoten = new MultiMeshInstance3D
        {
            Name = name, Multimesh = mm, MaterialOverride = Stoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        Knoten.AddChild(knoten);
        return (knoten, new float[Math.Max(_platz, 1) * 20]);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Verteilung
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Setzt die Schollen von groß nach klein an zufällige Stellen (Stelle, quer), ohne dass sie sich berühren; in den Feldern fast jede, auf den
    /// freien Strecken nur wenige und keine großen. Quer gilt der Platz beim vollen Randeis (bei weniger Eis rücken sie im selben Verhältnis
    /// auseinander).
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
    //  Jedes Bild
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Schiebt die Schollen weiter und schreibt die sichtbaren in die beiden Netze. <paramref name="eis"/> ist der Eisfortschritt der Art (0–1): Dichte
    /// und Breite des Randeises; <paramref name="schollenAn"/> 0 = keine Schollen (dann kostet es nichts). <paramref name="beweglich"/>: Hindernisse,
    /// die sich bewegen (Prahm), in Laufachsen.
    /// </summary>
    public void Bild(double delta, Camera3D? kamera, float eis, float schollenAn, List<Hindernis> beweglich)
    {
        _zeit += delta;
        _drift = (_drift + delta * _tempo) % _laenge;
        var dichte = schollenAn * Mathf.SmoothStep(0.15f, 0.85f, eis);
        if (dichte <= 0.001f || kamera is null || !GodotObject.IsInstanceValid(kamera))
        {
            if (Knoten.Visible) Knoten.Visible = false;
            return;
        }
        if (!Knoten.Visible) Knoten.Visible = true;
        _eisZuletzt = eis;

        var ebenen = kamera.GetFrustum();
        var plan = new Plane[ebenen.Count];
        for (var i = 0; i < plan.Length; i++) plan[i] = ebenen[i];
        var auge = kamera.GlobalPosition;
        var (nahZahl, fernZahl) = (0, 0);
        var randeis = _randeis * eis * 1.5f + UferAbstand;

        foreach (ref var sch in _schollen.AsSpan())
        {
            if (sch.Schwelle >= dichte) continue;
            var s = (float)((sch.S0 + _drift) % _laenge);
            var wachsen = Mathf.SmoothStep(sch.Schwelle, sch.Schwelle + 0.08f, dichte);
            var ende = Mathf.SmoothStep(0f, 14f, s) * Mathf.SmoothStep(_laenge, _laenge - 14f, s);
            var r = sch.Halbmesser * wachsen * ende;
            if (r < 0.06f) continue;
            var p = Punkt(s);
            var platz = p.Halb - randeis;
            if (platz < sch.Halbmesser + 0.2f) continue;
            var quer = sch.Anteil * (platz - sch.Halbmesser);
            quer = Ausweichen(quer, s, sch.Halbmesser, sch.Anteil, _festeHindernisse);
            quer = Ausweichen(quer, s, sch.Halbmesser, sch.Anteil, beweglich);
            quer = Mathf.Clamp(quer, -(platz - r * 0.6f), platz - r * 0.6f);
            var querW = new Vector2(-p.Richtung.Y, p.Richtung.X);
            var ort2 = p.Ort + querW * quer;
            var ort = new Vector3(ort2.X, p.Spiegel, ort2.Y);

            // im Bild? (Kugel um die Scholle gegen den Sichtkegel)
            var drin = true;
            foreach (var e in plan)
                if (e.DistanceTo(ort) > r + 0.5f) { drin = false; break; }
            if (!drin) continue;
            var weite = auge.DistanceTo(ort);
            // sehr kleine Schollen nur nah, kleine nur mittelweit (sie wären weniger als ein paar Bildpunkte)
            if ((weite > 140f && sch.Halbmesser < 0.55f) || (weite > 300f && sch.Halbmesser < 1.2f)) continue;
            var nah = weite < NahBis;
            if (nah ? nahZahl >= _platz : fernZahl >= _platz) continue;
            var puffer = nah ? _pufferNah : _pufferFern;
            var o = (nah ? nahZahl++ : fernZahl++) * 20;

            var gier = sch.Gier + sch.Dreh * (float)_zeit;
            var (sin, cos) = MathF.SinCos(gier);
            // Lage: Drehung um die Hochachse, gleichmäßig auf den Halbmesser skaliert (Zeilen der Basis, dann der Ort)
            puffer[o + 0] = cos * r; puffer[o + 1] = 0; puffer[o + 2] = sin * r; puffer[o + 3] = ort.X;
            puffer[o + 4] = 0; puffer[o + 5] = r; puffer[o + 6] = 0; puffer[o + 7] = ort.Y;
            puffer[o + 8] = -sin * r; puffer[o + 9] = 0; puffer[o + 10] = cos * r; puffer[o + 11] = ort.Z;
            // COLOR: Form, Dicke, Schnee; CUSTOM: Laufachsen und Fließrichtung für die Wellen
            puffer[o + 12] = sch.Saat; puffer[o + 13] = sch.Dicke * Mathf.Lerp(0.6f, 1f, wachsen); puffer[o + 14] = sch.Schnee; puffer[o + 15] = 0;
            puffer[o + 16] = quer; puffer[o + 17] = s; puffer[o + 18] = p.Richtung.X; puffer[o + 19] = p.Richtung.Y;
        }
        Schreiben(_nah, _pufferNah, nahZahl);
        Schreiben(_fern, _pufferFern, fernZahl);
        Gezeichnet = nahZahl + fernZahl;
    }

    private static void Schreiben(MultiMeshInstance3D knoten, float[] puffer, int zahl)
    {
        var mm = knoten.Multimesh;
        if (zahl > 0) RenderingServer.MultimeshSetBuffer(mm.GetRid(), puffer);
        mm.VisibleInstanceCount = zahl;
    }

    /// <summary>
    /// Seitlich ausweichen: Liegt ein Hindernis voraus oder neben der Scholle (längs bis 5 m vor seinem Rand), schiebt es sie quer an sich vorbei –
    /// auf die Seite, auf der sie schon ist (bei genau mittiger Lage nach ihrem Anteil), stetig mit dem Abstand längs.
    /// </summary>
    private float Ausweichen(float quer, float s, float halb, float anteil, List<Hindernis> hindernisse)
    {
        foreach (var h in hindernisse)
        {
            var ds = s - h.Stelle;
            if (ds > _laenge / 2) ds -= _laenge;
            else if (ds < -_laenge / 2) ds += _laenge;
            var weit = h.HalbLaengs + halb + 5f;
            if (MathF.Abs(ds) > weit) continue;
            var gewicht = Mathf.SmoothStep(weit, h.HalbLaengs + halb, MathF.Abs(ds));
            var noetig = h.HalbQuer + halb + 0.3f;
            var dq = quer - h.Quer;
            if (MathF.Abs(dq) >= noetig) continue;
            var seite = MathF.Abs(dq) > 0.05f ? MathF.Sign(dq) : (anteil >= 0 ? 1f : -1f);
            quer += (seite * noetig - dq) * gewicht;
        }
        return quer;
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
