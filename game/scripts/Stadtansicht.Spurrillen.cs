using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Spurrillen an Kreuzungen (Kartenwerkstatt, Schritt 5j) – ein Teil der <c>partial class Stadtansicht</c>. Karren biegen nicht im rechten Winkel ab:
/// An jeder Kreuzung (T, X, Y, auch zwei Wegenden aneinander) laufen die Rillen in Bögen von einem Weg in den anderen.
/// <list type="bullet">
/// <item><b>Kreuzungen finden</b> (<see cref="SchichtKreuzungen"/>): ein Wegende auf einem anderen Weg (bis 1,2 m daneben) oder zwei Wege, die sich
/// schneiden. Jeder Weg, der die Kreuzung verlässt, ist ein Arm mit seiner Richtung.</item>
/// <item><b>Bögen:</b> Zwischen je zwei Armen, die um 12° bis 125° abbiegen, ein Kreisbogen, der beide Mittellinien berührt (Halbmesser 4–10 m:
/// rechtwinklig etwa 6 m, flache Abzweige weiter). Liegen zwei Arme fast in einer Linie, fahren die Karren geradeaus durch.</item>
/// <item><b>Spurlinien:</b> Die Rillen liegen links und rechts im Abstand der halben Spurweite (<see cref="SpurWeite"/>, alle Karren gleich) neben
/// einer Spurlinie – der Mitte jedes Weges und jedem Bogen. Endet ein Weg an einer Kreuzung mit Bögen, enden seine eigenen Rillen dort, wo der Bogen beginnt; an freien Enden laufen sie aus.
/// Die Schicht trägt je Bildpunkt die beiden nächsten Spurlinien (<see cref="SpurenRechnen"/>) – so kreuzen sich Rillen, statt einander abzuschneiden.</item>
/// <item><b>Ausgefahrene Ecken:</b> Außerhalb der Stadt wird jeder Bogen auch als schmaler Fahrweg in die Schicht gelegt: Die Innenecke einer
/// Kreuzung ist abgefahren, wie auf alten Fotos von Feldwegen.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Bis zu diesem Abstand von einer Spurlinie (Meter) trägt die Schicht ihn ein (Rillen liegen bei 0,45–0,85 m).</summary>
    private const float SpurReichweite = 2.55f;

    /// <summary>Halbe Spurweite: Abstand der Rillen zur Spurlinie (Meter). Alle Karren haben dieselbe Spur; auch <c>SPUR</c> im Boden-Shader.</summary>
    private const float SpurWeite = 0.75f;

    /// <summary>Auf so vielen Metern laufen die Rillen an einem freien Wegende aus.</summary>
    private const float SpurAuslauf = 2.5f;

    /// <summary>
    /// Eine Spurlinie: die Linie (als Teil, Halb so, dass sie <see cref="SpurReichweite"/> weit rechnet), ob die Rillen an den Enden auslaufen, und je
    /// halben Meter, ob dort Rillen sind (1 auf einem Fahrweg und in der Stadt, 0 auf Pfad, Kies und Pflaster draußen).
    /// </summary>
    private sealed class Spurlinie
    {
        public required Schichtteil Teil;
        public bool AuslaufVon, AuslaufBis;
        public float[]? Faktor;

        public float Staerke(float s)
        {
            var a = 1f;
            if (AuslaufVon) a = Mathf.Min(a, s / SpurAuslauf);
            if (AuslaufBis) a = Mathf.Min(a, (Teil.Stelle[^1] - s) / SpurAuslauf);
            if (Faktor is not null) a *= SchichtWertBei(Faktor, s);
            return Mathf.Clamp(a, 0f, 1f);
        }
    }

    /// <summary>Ein Arm einer Kreuzung: der Weg (Nummer in der Liste), die Stelle an der Kreuzung (0 bzw. Länge an einem Ende) und die Richtung (+1 vorwärts).</summary>
    private readonly record struct Kreuzungsarm(int Weg, float Stelle, int Sinn);

    /// <summary>Wie stark ein Weg dieser Stufe Rillen trägt: ein Fahrweg ganz, Pfad und Kies nicht, Pflaster nur in der Stadt.</summary>
    private static float SpurFaktor(float stufe, bool inStadt) =>
        float.IsNaN(stufe) ? 0f : inStadt ? (stufe < 3.5f ? 1f : 0f) : Mathf.Clamp(2f - 2f * Mathf.Abs(stufe - 1f), 0f, 1f);

    /// <summary>
    /// Findet die Kreuzungen der Wege und gibt die Spurlinien zurück (Wege und Bögen), dazu die Bögen außerhalb der Stadt als schmale Fahrwege
    /// (ausgefahrene Ecken). <paramref name="stadt"/>: die Flächen in den Mauern (dort sind die Gassen gepflastert, die Ecken bleiben, wie sie sind).
    /// </summary>
    private static (List<Schichtteil> Ecken, List<Spurlinie> Spuren) SchichtKreuzungen(List<Schichtteil> wege, List<Vector2[]> stadt)
    {
        var knoten = new List<(Vector2 Ort, List<Kreuzungsarm> Arme)>();
        void Melden(Vector2 ort, List<Kreuzungsarm> arme)
        {
            var liste = knoten.FirstOrDefault(k => k.Ort.DistanceTo(ort) < 2.5f).Arme;
            if (liste is null) knoten.Add((ort, liste = new List<Kreuzungsarm>()));
            foreach (var a in arme)
                if (!liste.Any(b => b.Weg == a.Weg && b.Sinn == a.Sinn && Mathf.Abs(b.Stelle - a.Stelle) < 3f)) liste.Add(a);
        }
        void ArmeVon(int w, float s, List<Kreuzungsarm> liste)
        {
            var laenge = wege[w].Stelle[^1];
            if (s <= 1.5f) liste.Add(new Kreuzungsarm(w, 0, 1));
            else if (s >= laenge - 1.5f) liste.Add(new Kreuzungsarm(w, laenge, -1));
            else
            {
                liste.Add(new Kreuzungsarm(w, s, 1));
                liste.Add(new Kreuzungsarm(w, s, -1));
            }
        }

        // Wegenden auf einem anderen Weg
        for (var a = 0; a < wege.Count; a++)
            foreach (var ende in new[] { 0f, wege[a].Stelle[^1] })
            {
                var p = LinienPunkt(wege[a], ende);
                for (var b = 0; b < wege.Count; b++)
                {
                    if (b == a || !wege[b].Rahmen.HasPoint(p)) continue;
                    var (abstand, sb) = LinieNaechster(wege[b], p);
                    if (abstand > 1.2f) continue;
                    var arme = new List<Kreuzungsarm>();
                    ArmeVon(a, ende, arme);
                    ArmeVon(b, sb, arme);
                    Melden(LinienPunkt(wege[b], sb), arme);
                }
            }
        // Wege, die sich schneiden (nicht an ihren Enden: das ist oben)
        for (var a = 0; a < wege.Count; a++)
            for (var b = a + 1; b < wege.Count; b++)
            {
                var (wa, wb) = (wege[a], wege[b]);
                if (!wa.Rahmen.Intersects(wb.Rahmen)) continue;
                for (var i = 0; i + 1 < wa.Linie.Length; i++)
                {
                    var (p, q) = (wa.Linie[i], wa.Linie[i + 1]);
                    var ra = new Rect2(p, Vector2.Zero).Expand(q);
                    if (!ra.Grow(0.01f).Intersects(wb.Rahmen)) continue;
                    for (var j = 0; j + 1 < wb.Linie.Length; j++)
                    {
                        var (r, s) = (wb.Linie[j], wb.Linie[j + 1]);
                        if (!ra.Grow(0.01f).Intersects(new Rect2(r, Vector2.Zero).Expand(s))) continue;
                        if (Schnitt(p, q, r, s) is not { } x) continue;
                        var sa = wa.Stelle[i] + p.DistanceTo(x);
                        var sb = wb.Stelle[j] + r.DistanceTo(x);
                        if (sa <= 1.5f || sa >= wa.Stelle[^1] - 1.5f || sb <= 1.5f || sb >= wb.Stelle[^1] - 1.5f) continue;
                        var arme = new List<Kreuzungsarm>();
                        ArmeVon(a, sa, arme);
                        ArmeVon(b, sb, arme);
                        Melden(x, arme);
                    }
                }
            }

        // je Kreuzung: Bögen zwischen den Armen
        var ecken = new List<Schichtteil>();
        var spuren = new List<Spurlinie>();
        var kuerzenVon = new float[wege.Count];
        var kuerzenBis = new float[wege.Count];
        var durchVon = new bool[wege.Count];
        var durchBis = new bool[wege.Count];
        foreach (var (ort, arme) in knoten)
        {
            if (arme.Count < 2) continue;
            var inStadt = stadt.Any(f => ImVieleck(ort, f));
            var n = arme.Count;
            var richtung = new Vector2[n];
            var laenge = new float[n];
            var stufe = new float[n];
            var fahrbar = new bool[n];
            var bogenLaenge = new float[n];
            var durch = new bool[n];
            for (var i = 0; i < n; i++)
            {
                var (w, s, sinn) = arme[i];
                var t = wege[w];
                laenge[i] = sinn > 0 ? t.Stelle[^1] - s : s;
                var vor = Mathf.Min(2f, laenge[i] * 0.5f);
                richtung[i] = LinienRichtung(t, s + sinn * vor) * sinn;
                stufe[i] = SchichtStufeBei(t, s + sinn * Mathf.Min(3f, laenge[i] * 0.5f));
                // Rillen hat ein Fahrweg (in der Stadt jede Gasse); Pfad, Kies und Pflaster draußen nicht
                fahrbar[i] = SpurFaktor(stufe[i], inStadt) > 0.5f;
            }
            for (var i = 0; i < n; i++)
                for (var j = i + 1; j < n; j++)
                {
                    var cos = Mathf.Clamp(richtung[i].Dot(richtung[j]), -1f, 1f);
                    var phi = Mathf.Acos(cos);           // Winkel zwischen den Armen
                    var abbiegen = Mathf.Pi - phi;       // so weit dreht der Karren
                    if (abbiegen < Mathf.DegToRad(12f))
                    {
                        durch[i] = durch[j] = true;      // fast eine Linie: geradeaus durch
                        continue;
                    }
                    if (abbiegen > Mathf.DegToRad(125f) || !fahrbar[i] || !fahrbar[j]) continue;
                    var tan = Mathf.Tan(abbiegen / 2);
                    var halbmesser = Mathf.Clamp(6f / tan, 4f, 10f);
                    var tangente = halbmesser * tan;
                    var hoechstens = 0.85f * Mathf.Min(laenge[i], laenge[j]);
                    if (tangente > hoechstens)
                    {
                        tangente = hoechstens;
                        halbmesser = tangente / tan;
                    }
                    if (halbmesser < 2.5f) continue;
                    var punkte = Bogen(ort, richtung[i], richtung[j], halbmesser, tangente, phi);
                    if (punkte is null) continue;
                    bogenLaenge[i] = Mathf.Max(bogenLaenge[i], tangente);
                    bogenLaenge[j] = Mathf.Max(bogenLaenge[j], tangente);
                    spuren.Add(new Spurlinie { Teil = Spurteil(punkte) });
                    if (inStadt) continue;
                    // die ausgefahrene Innenecke: ein schmaler Fahrweg auf dem Bogen
                    var (ti, tj) = (wege[arme[i].Weg], wege[arme[j].Weg]);
                    var austritt = Mathf.Min(SchichtWertBei(ti.Austritt, arme[i].Stelle), SchichtWertBei(tj.Austritt, arme[j].Stelle));
                    var zustand = Mathf.Min(SchichtWertBei(ti.Zustand, arme[i].Stelle), SchichtWertBei(tj.Zustand, arme[j].Stelle));
                    var lang = Bogenlaenge(punkte);
                    var stueck = new[] { new Belagabschnitt(0, lang + 1, Belagstufe.Fahrweg, Austritt: austritt, Zustand: zustand) };
                    if (SchichtWeg(punkte, 2 * (SpurWeite + 0.45f), stueck, null) is { } ecke) ecken.Add(ecke);
                }
            for (var i = 0; i < n; i++)
            {
                var (w, s, sinn) = arme[i];
                var amAnfang = s == 0 && sinn > 0;
                var amEnde = sinn < 0 && s >= wege[w].Stelle[^1];
                if (!amAnfang && !amEnde) continue;
                if (durch[i]) (amAnfang ? durchVon : durchBis)[w] = true;
                else if (amAnfang) kuerzenVon[w] = Mathf.Max(kuerzenVon[w], bogenLaenge[i]);
                else kuerzenBis[w] = Mathf.Max(kuerzenBis[w], bogenLaenge[i]);
            }
        }

        // die eigenen Rillen jedes Weges: bis zum Beginn der Bögen, an freien Enden auslaufend
        for (var w = 0; w < wege.Count; w++)
        {
            var t = wege[w];
            var (von, bis) = (durchVon[w] ? 0 : kuerzenVon[w], t.Stelle[^1] - (durchBis[w] ? 0 : kuerzenBis[w]));
            if (bis - von < 0.5f) continue;
            var linie = Spurteil(Teilstueck(t, von, bis));
            var faktor = new float[Mathf.CeilToInt(linie.Stelle[^1] / SchichtBelagSchritt) + 2];
            for (var k = 0; k < faktor.Length; k++)
            {
                var s = Mathf.Min(von + k * SchichtBelagSchritt, t.Stelle[^1]);
                var p = LinienPunkt(t, s);
                faktor[k] = SpurFaktor(SchichtStufeBei(t, s), stadt.Any(f => ImVieleck(p, f)));
            }
            if (faktor.All(f => f == 0)) continue;
            spuren.Add(new Spurlinie
            {
                Teil = linie, Faktor = faktor,
                AuslaufVon = !durchVon[w] && kuerzenVon[w] == 0, AuslaufBis = !durchBis[w] && kuerzenBis[w] == 0,
            });
        }
        return (ecken, spuren);
    }

    /// <summary>
    /// Der Kreisbogen, der von <paramref name="di"/> nach <paramref name="dj"/> abbiegt (beide zeigen von der Kreuzung weg) und beide Mittellinien im
    /// Abstand <paramref name="tangente"/> berührt; Punkte etwa alle 0,4 m. Null, wenn die Arme fast gleich oder entgegengesetzt laufen.
    /// </summary>
    private static Vector2[]? Bogen(Vector2 ort, Vector2 di, Vector2 dj, float halbmesser, float tangente, float phi)
    {
        var mitte = di + dj;
        if (mitte.LengthSquared() < 1e-6f || Mathf.Sin(phi / 2) < 1e-3f) return null;
        var zentrum = ort + mitte.Normalized() * (halbmesser / Mathf.Sin(phi / 2));
        var (a, b) = (ort + di * tangente, ort + dj * tangente);
        var wa = (a - zentrum).Angle();
        var delta = Mathf.Wrap((b - zentrum).Angle() - wa, -Mathf.Pi, Mathf.Pi);
        var schritte = Math.Max(4, Mathf.CeilToInt(Mathf.Abs(delta) * halbmesser / 0.4f));
        var punkte = new Vector2[schritte + 1];
        for (var k = 0; k <= schritte; k++)
        {
            var w = wa + delta * k / schritte;
            punkte[k] = zentrum + new Vector2(Mathf.Cos(w), Mathf.Sin(w)) * halbmesser;
        }
        (punkte[0], punkte[^1]) = (a, b);
        return punkte;
    }

    /// <summary>Der Schnittpunkt zweier Strecken (null, wenn sie sich nicht schneiden oder parallel laufen).</summary>
    private static Vector2? Schnitt(Vector2 p, Vector2 q, Vector2 r, Vector2 s)
    {
        var (d, e) = (q - p, s - r);
        var nenner = d.Cross(e);
        if (Mathf.Abs(nenner) < 1e-9f) return null;
        var u = (r - p).Cross(e) / nenner;
        var v = (r - p).Cross(d) / nenner;
        return u is >= 0 and <= 1 && v is >= 0 and <= 1 ? p + d * u : null;
    }

    private static float Bogenlaenge(Vector2[] punkte)
    {
        var l = 0f;
        for (var i = 1; i < punkte.Length; i++) l += punkte[i].DistanceTo(punkte[i - 1]);
        return l;
    }

    /// <summary>Eine Linie als Teil für <see cref="LinieAbstand"/>, der <see cref="SpurReichweite"/> weit rechnet.</summary>
    private static Schichtteil Spurteil(Vector2[] linie)
    {
        var stelle = new float[linie.Length];
        for (var i = 1; i < linie.Length; i++) stelle[i] = stelle[i - 1] + linie[i].DistanceTo(linie[i - 1]);
        return new Schichtteil { Linie = linie, Stelle = stelle, Halb = SpurReichweite - SchichtRand, Rahmen = Rahmen(linie, SpurReichweite)!.Value };
    }

    /// <summary>Der Punkt an der Stelle <paramref name="s"/> (Meter entlang) einer Linie.</summary>
    private static Vector2 LinienPunkt(Schichtteil t, float s)
    {
        if (s <= 0) return t.Linie[0];
        if (s >= t.Stelle[^1]) return t.Linie[^1];
        var k = Array.BinarySearch(t.Stelle, s);
        if (k >= 0) return t.Linie[k];
        k = ~k;
        var (s0, s1) = (t.Stelle[k - 1], t.Stelle[k]);
        return t.Linie[k - 1].Lerp(t.Linie[k], (s - s0) / Mathf.Max(s1 - s0, 1e-6f));
    }

    /// <summary>Die Richtung einer Linie an der Stelle <paramref name="s"/> (gemittelt über 2 m).</summary>
    private static Vector2 LinienRichtung(Schichtteil t, float s)
    {
        var d = LinienPunkt(t, s + 1f) - LinienPunkt(t, s - 1f);
        return d.LengthSquared() > 1e-8f ? d.Normalized() : (t.Linie[^1] - t.Linie[0]).Normalized();
    }

    /// <summary>Abstand eines Punktes zur Linie und die Stelle (Meter entlang) des nächsten Punkts.</summary>
    private static (float Abstand, float Stelle) LinieNaechster(Schichtteil t, Vector2 p)
    {
        var (best, stelle) = (float.MaxValue, 0f);
        for (var i = 0; i + 1 < t.Linie.Length; i++)
        {
            var (a, b) = (t.Linie[i], t.Linie[i + 1]);
            var ab = b - a;
            var u = ab.LengthSquared() < 1e-8f ? 0f : Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
            var d = p.DistanceTo(a + ab * u);
            if (d < best) (best, stelle) = (d, t.Stelle[i] + u * ab.Length());
        }
        return (best, stelle);
    }

    /// <summary>Das Stück einer Linie zwischen zwei Stellen.</summary>
    private static Vector2[] Teilstueck(Schichtteil t, float von, float bis)
    {
        var liste = new List<Vector2> { LinienPunkt(t, von) };
        for (var i = 0; i < t.Linie.Length; i++)
            if (t.Stelle[i] > von + 0.01f && t.Stelle[i] < bis - 0.01f) liste.Add(t.Linie[i]);
        liste.Add(LinienPunkt(t, bis));
        return liste.ToArray();
    }

    private static float SchichtStufeBei(Schichtteil t, float s) => SchichtWertBei(t.Stufe, s);

    private static float SchichtWertBei(float[] werte, float s) => werte[Math.Clamp((int)MathF.Round(s / SchichtBelagSchritt), 0, werte.Length - 1)];

    /// <summary>
    /// Die beiden nächsten Spurlinien je Bildpunkt eines Ausschnitts (<paramref name="n"/>², ab <paramref name="x0"/>, <paramref name="z0"/>): Abstand und
    /// Stärke. Hinter dem Ende einer Linie zählt sie nicht – sonst lägen Rillen im Halbkreis um ihr Ende –, und wo sie keine Rillen trägt, auch nicht.
    /// </summary>
    private static void SpurenRechnen(List<Spurlinie> spuren, float x0, float z0, int n, float[] d1, float[] d2, float[] s1, float[] s2)
    {
        const float t = SchichtTexel;
        var flaeche = new Rect2(x0, z0, n * t, n * t);
        foreach (var linie in spuren)
        {
            var teil = linie.Teil;
            if (!teil.Rahmen.Intersects(flaeche)) continue;
            var r = teil.Rahmen.Intersection(flaeche);
            var a0 = Math.Clamp((int)Mathf.Floor((r.Position.X - x0) / t), 0, n - 1);
            var b0 = Math.Clamp((int)Mathf.Floor((r.Position.Y - z0) / t), 0, n - 1);
            var a1 = Math.Clamp((int)Mathf.Ceil((r.End.X - x0) / t), 0, n - 1);
            var b1 = Math.Clamp((int)Mathf.Ceil((r.End.Y - z0) / t), 0, n - 1);
            var (w, h) = (a1 - a0 + 1, b1 - b0 + 1);
            var abstand = new float[w * h];
            var ort = new float[w * h];
            Array.Fill(abstand, float.MaxValue);
            LinieAbstand(teil, x0, z0, a0, b0, w, h, abstand, ort);
            var laenge = teil.Stelle[^1];
            var (anfang, ende) = (teil.Linie[0], teil.Linie[^1]);
            var (vorAnfang, hinterEnde) = ((teil.Linie[0] - teil.Linie[1]).Normalized(), (teil.Linie[^1] - teil.Linie[^2]).Normalized());
            for (var b = 0; b < h; b++)
                for (var a = 0; a < w; a++)
                {
                    var d = abstand[b * w + a];
                    if (d >= SpurReichweite) continue;
                    var s = ort[b * w + a];
                    if (s < 0.01f || s > laenge - 0.01f)
                    {
                        var p = new Vector2(x0 + (a + a0 + 0.5f) * t, z0 + (b + b0 + 0.5f) * t);
                        if (s < 0.01f ? (p - anfang).Dot(vorAnfang) > 0.01f : (p - ende).Dot(hinterEnde) > 0.01f) continue;
                    }
                    var staerke = linie.Staerke(s);
                    if (staerke <= 0.01f) continue;
                    var i = (b + b0) * n + a + a0;
                    if (d < d1[i])
                    {
                        (d2[i], s2[i]) = (d1[i], s1[i]);
                        (d1[i], s1[i]) = (d, staerke);
                    }
                    else if (d < d2[i]) (d2[i], s2[i]) = (d, staerke);
                }
        }
    }
}
