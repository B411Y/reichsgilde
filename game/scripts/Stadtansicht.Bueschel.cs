using System.Threading.Tasks;
using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Grasbüschel an den Grenzen der Wiese (Kartenwerkstatt, Schritt 5h) – ein Teil der <c>partial class Stadtansicht</c>. Wo die Wiese an Erde, Kies, Sand,
/// Lehm, Mist, Fels oder einen Weg (Pfad, Fahrweg, Kies) grenzt, stehen von selbst kleine Büschel aus Halmen (<c>natur_boden</c>, „Gras1“ und „Gras2“),
/// nur in der Bodenqualität Hoch (<see cref="Fenster.AktuelleBodenqualitaet"/>). Nichts davon wird gespeichert:
/// <list type="bullet">
/// <item><b>Kandidaten</b> je Geländekachel (128 m): ein festes Gitter zu 0,5 m mit einem Ort je Zelle (aus der Zelle gemischt, auf jedem Rechner gleich).
/// An Wegen aus dem Bild der Wegeschicht (<see cref="BueschelAusWegen"/>, parallel mit ihr gerechnet: Abstand zum Rand mit demselben Rauschen wie im
/// Boden-Shader), an gemalten Grenzen aus den Gewichten des Bodenrasters (<see cref="BueschelAusBoden"/>: wo die Wiese etwa zur Hälfte deckt).</item>
/// <item><b>Knoten</b> je Kachel mit einem MultiMesh je Halmform (<see cref="BueschelKachelBauen"/>): ohne Gebäude, Wasser und steile Hänge; wie die
/// übrige Bodendeko ohne Schatten, mit Natur-Dichte und Sichtweite der Feinheiten (Stadtansicht.Grafik.cs).</item>
/// <item><b>Neu</b> wird nur, was sich ändert: die Kacheln einer neu gerechneten Wegeschicht, eines Pinselstrichs (nach dem Loslassen) oder des
/// Geländes; einmal je Bild in <see cref="BueschelBild"/>.</item>
/// </list>
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Größe einer Zelle des Gitters (Meter): ein möglicher Büschel je Zelle.</summary>
    private const float BueschelZelle = 0.5f;

    /// <summary>Die Halmformen aus <c>natur_boden</c> und wie oft sie vorkommen (der Rest ist Gras1).</summary>
    private const string BueschelModell = "natur/natur_boden";
    private const float BueschelAnteilGras2 = 0.3f;

    /// <summary>Kandidaten je Geländekachel: Ort (x, z), Größe und ein Wurf 0 … 1 (Drehung, Form, Reihenfolge für die Natur-Dichte).</summary>
    private readonly Dictionary<(int I, int J), Vector4[]> _bueschelWeg = new(), _bueschelBoden = new();

    private readonly Dictionary<(int I, int J), Node3D> _bueschelKnoten = new();
    private readonly HashSet<(int I, int J)> _bueschelBauen = new(), _bueschelBodenNeu = new();
    private bool _bueschelBodenAlles = true, _bueschelAlleBauen = true;
    private Node3D? _bueschelWurzel;

    private static bool BueschelAn => Fenster.AktuelleBodenqualitaet == Bodenqualitaet.Hoch;

    // ═════════════════════════════════════════════════════════════════════════
    //  Melden
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Der Boden wird neu gebaut (Stadtansicht.Gelaende.cs): Die Knoten hingen an ihm, alle Kacheln neu.</summary>
    private void BueschelVergessen()
    {
        _bueschelWurzel = null;
        _bueschelKnoten.Clear();
        _bueschelBodenAlles = true;
        _bueschelAlleBauen = true;
        GrasVergessen(); // der Grasteppich hängt ebenso am Boden (Stadtansicht.Gras.cs)
    }

    /// <summary>Die Wegeschicht einer Kachel ist neu gerechnet (Stadtansicht.Wegeschicht.cs).</summary>
    private void BueschelWegeGesetzt((int I, int J) kachel, Vector4[]? kandidaten)
    {
        GrasKachelNeu(kachel); // auch der Grasteppich liest die Wegeschicht (Stadtansicht.Gras.cs)
        if (kandidaten is { Length: > 0 }) _bueschelWeg[kachel] = kandidaten;
        else if (!_bueschelWeg.Remove(kachel)) return;
        _bueschelBauen.Add(kachel);
    }

    /// <summary>Der Boden einer Kachel hat sich geändert (Pinsel, Rückgängig): die Kandidaten der Grenzen neu.</summary>
    private void BueschelBodenGeaendert(int ti, int tj)
    {
        _bueschelBodenNeu.Add((ti, tj));
        GrasKachelNeu((ti, tj));
    }

    /// <summary>Das Gelände einer Kachel hat sich geändert: Die Büschel stehen auf der neuen Höhe.</summary>
    private void BueschelHoeheGeaendert(int ti, int tj)
    {
        _bueschelBauen.Add((ti, tj));
        GrasKachelNeu((ti, tj));
    }

    /// <summary>In der Werkstatt haben sich Hindernisse (Gebäude, Wege, Bauzonen, Mauer, Wasser) im Rechteck geändert: die Knoten dort neu.</summary>
    private void BueschelBereich(Rect2 r)
    {
        GrasBereichNeu(r);
        const float kachel = KachelZellen * 2f;
        for (var j = Mathf.FloorToInt(r.Position.Y / kachel); j <= Mathf.FloorToInt(r.End.Y / kachel); j++)
            for (var i = Mathf.FloorToInt(r.Position.X / kachel); i <= Mathf.FloorToInt(r.End.X / kachel); i++)
                if (_bueschelWeg.ContainsKey((i, j)) || _bueschelBoden.ContainsKey((i, j))) _bueschelBauen.Add((i, j));
    }

    /// <summary>Nur in Hoch zu sehen (aus <see cref="BodenqualitaetNachfuehren"/>).</summary>
    private void BueschelSichtbarkeit()
    {
        if (_bueschelWurzel is not null && IsInstanceValid(_bueschelWurzel)) _bueschelWurzel.Visible = BueschelAn;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Jedes Bild
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Jedes Bild (nach der Wegeschicht): in Hoch die Kandidaten geänderter Böden rechnen und die Knoten geänderter Kacheln bauen. Während eines
    /// Pinselstrichs in der Werkstatt wartet es (wie die Deko), sonst würde jede Mausbewegung alles neu streuen.
    /// </summary>
    private void BueschelBild()
    {
        if (!BueschelAn || _gelaendeKnoten is null || _randKnoten is null) return;
        if (_wsBodenStrich || _entwurf is { BodenStrichLaeuft: true } or { FelsStrichLaeuft: true }) return; // auch der Felsstrich malt Boden (F6)
        if (_bueschelBodenAlles || _bueschelBodenNeu.Count > 0) BueschelBodenRechnen();
        var alle = _bueschelAlleBauen;
        if (alle)
        {
            _bueschelAlleBauen = false;
            _bueschelBauen.UnionWith(_bueschelWeg.Keys);
            _bueschelBauen.UnionWith(_bueschelBoden.Keys);
            _bueschelBauen.UnionWith(_bueschelKnoten.Keys);
        }
        if (_bueschelBauen.Count == 0) return;
        if (_bueschelWurzel is null || !IsInstanceValid(_bueschelWurzel))
        {
            _bueschelWurzel = new Node3D { Name = "Grasbueschel" };
            _randKnoten.AddChild(_bueschelWurzel);
        }
        Naturfreiraum? frei = _entwurf is not null ? _entwurf.Freiraum(Modellmass) : _karte is { } k ? Naturfreiraum.Aus(k, Modellmass) : null;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        foreach (var kachel in _bueschelBauen) BueschelKachelBauen(kachel, frei);
        if (alle) GD.Print($"Grasbüschel: {_bueschelWeg.Values.Sum(v => v.Length)} am Weg, {_bueschelBoden.Values.Sum(v => v.Length)} an Grenzen (vor dem Freiraum), " +
                           $"{_bueschelKnoten.Count} Kacheln, {uhr.ElapsedMilliseconds} ms");
        _bueschelBauen.Clear();
        _naturDetails.RemoveAll(d => !IsInstanceValid(d.Teil));
    }

    /// <summary>Die Kandidaten der gemalten Grenzen für alle bzw. die gemeldeten Kacheln (parallel).</summary>
    private void BueschelBodenRechnen()
    {
        var raster = BodenRaster;
        List<(int I, int J)> liste;
        if (_bueschelBodenAlles)
        {
            liste = _gelaendeKacheln.Keys.ToList();
            liste.AddRange(_bueschelBoden.Keys.Where(k => !_gelaendeKacheln.ContainsKey(k)));
        }
        else liste = _bueschelBodenNeu.ToList();
        _bueschelBodenAlles = false;
        _bueschelBodenNeu.Clear();
        var ergebnis = new Vector4[]?[liste.Count];
        if (raster is { IstLeer: false })
            Parallel.For(0, liste.Count, k =>
            {
                if (BodenKachelGemalt(raster, liste[k].I, liste[k].J)) ergebnis[k] = BueschelAusBoden(raster, liste[k].I, liste[k].J);
            });
        for (var k = 0; k < liste.Count; k++)
        {
            if (ergebnis[k] is { Length: > 0 } neu) _bueschelBoden[liste[k]] = neu;
            else if (!_bueschelBoden.Remove(liste[k])) continue;
            _bueschelBauen.Add(liste[k]);
        }
    }

    /// <summary>Baut den Knoten einer Kachel neu: Kandidaten von Wegen und Boden, ohne Gebäude, Wasser und steile Hänge.</summary>
    private void BueschelKachelBauen((int I, int J) kachel, Naturfreiraum? frei)
    {
        if (_bueschelKnoten.Remove(kachel, out var alt) && IsInstanceValid(alt)) alt.QueueFree();
        var wege = _bueschelWeg.GetValueOrDefault(kachel);
        var boden = _bueschelBoden.GetValueOrDefault(kachel);
        if (wege is null && boden is null) return;
        var wasser = GewaesserForm;
        var formen = new[] { new List<(Transform3D Lage, float Wurf)>(), new List<(Transform3D Lage, float Wurf)>() };
        void Setzen(Vector4 k, bool amWeg)
        {
            if (frei is not null && (frei.AufGebaeude(k.X, k.Y, 0.3) || (!amWeg && frei.AufWeg(k.X, k.Y, 0.2)))) return;
            if (wasser is not null && wasser.IstWasser(k.X, k.Y)) return;
            if (BodenNormale(k.X, k.Y).Y < 0.87f) return; // ab 30° Gefälle ist Fels (boden.gdshaderinc)
            var form = Teil(k.W, 0) < BueschelAnteilGras2 ? 1 : 0;
            var drehung = Teil(k.W, 1) * Mathf.Tau;
            var lage = new Transform3D(new Basis(Vector3.Up, drehung).Scaled(Vector3.One * k.Z), new Vector3(k.X, BodenHoehe(k.X, k.Y) - 0.02f, k.Y));
            formen[form].Add((lage, k.W));
        }
        if (wege is not null) foreach (var k in wege) Setzen(k, true);
        if (boden is not null) foreach (var k in boden) Setzen(k, false);
        if (formen[0].Count + formen[1].Count == 0 || _bueschelWurzel is null) return;

        var knoten = new Node3D { Name = $"Bueschel_{kachel.I}_{kachel.J}" };
        _bueschelWurzel.AddChild(knoten);
        _bueschelKnoten[kachel] = knoten;
        for (var f = 0; f < formen.Length; f++)
        {
            if (formen[f].Count == 0 || NaturNetz(BueschelModell, f == 0 ? "Gras1" : "Gras2") is not { } netz) continue;
            // gemischt nach dem Wurf: Die Natur-Dichte zeigt nur die ersten Kopien, und die sollen über die Kachel verteilt sein
            var lagen = formen[f].OrderBy(l => l.Wurf).ToList();
            var vielfach = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = netz, InstanceCount = lagen.Count };
            for (var i = 0; i < lagen.Count; i++) vielfach.SetInstanceTransform(i, lagen[i].Lage);
            var teil = new MultiMeshInstance3D
            {
                Name = f == 0 ? "Gras1" : "Gras2", Multimesh = vielfach, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = _feinheitenSichtbar ?? true,
            };
            knoten.AddChild(teil);
            NaturDetailMelden(teil, BueschelModell); // Natur-Dichte und Sichtweite der Feinheiten
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kandidaten (laufen parallel, lesen nur)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Büschel am Rand von Pfad, Fahrweg und Kies einer Kachel aus dem Bild der Wegeschicht (<see cref="SchichtKachelRechnen"/>): dort, wo der Rand im
    /// Boden-Shader liegt (Abstand zum Rand mit demselben Rauschen), ein Band von etwa einem halben Meter nach außen. Nicht auf Pflaster, Baustellen und
    /// in der Stadt (Pflaster als Grund), nur wo der Boden Wiese ist.
    /// </summary>
    private static Vector4[]? BueschelAusWegen(int ti, int tj, byte[] bild, byte[] info, Bodenraster? raster)
    {
        const float kachel = KachelZellen * 2f, t = SchichtTexel;
        const int n = (int)(kachel / BueschelZelle);
        var (x0, z0) = (ti * kachel, tj * kachel);
        var liste = new List<Vector4>();
        for (var cj = 0; cj < n; cj++)
            for (var ci = 0; ci < n; ci++)
            {
                var h = BueschelWurf(ti * n + ci, tj * n + cj, 1);
                var (x, z) = (x0 + (ci + Teil(h, 2)) * BueschelZelle, z0 + (cj + Teil(h, 3)) * BueschelZelle);
                // Bildpunkt k deckt [Kachel + (k − 1) · 0,25 m, Kachel + k · 0,25 m]
                var a = Math.Clamp((int)((x - x0) / t) + 1, 0, SchichtBild - 1);
                var b = Math.Clamp((int)((z - z0) / t) + 1, 0, SchichtBild - 1);
                var o = (b * SchichtBild + a) * 4;
                var e = bild[o + 1] / 255f * (SchichtRand - SchichtRandMin) + SchichtRandMin;
                if (e is < -0.9f or > 1.4f) continue;
                var st = bild[o + 2] / 63.75f;
                if (st > 2.5f) continue; // Pflaster und Baustelle: klare Kante bzw. aufgewühlt
                // grobes Bild: Punkt k liegt bei Kachel + k − 0,5 m
                var ia = Math.Clamp((int)MathF.Floor(x - x0 + 1), 0, SchichtInfoBild - 1);
                var ib = Math.Clamp((int)MathF.Floor(z - z0 + 1), 0, SchichtInfoBild - 1);
                if (info[(ib * SchichtInfoBild + ia) * 4 + 2] > 76) continue; // Pflaster als Grund (Stadt): Gassen ohne Gras
                // der Rand wie im Boden-Shader (weg() in boden.gdshaderinc): Abstand plus Rauschen je Belag
                var n1 = Rausch(x * 0.8f, z * 0.8f) - 0.5f;
                var n2 = Rausch(x * 3.3f, z * 3.3f) - 0.5f;
                var n3 = Rausch(x * 11f, z * 11f) - 0.5f;
                var (en, mitte) = st switch
                {
                    < 0.5f => (e + n1 * 0.55f + n2 * 0.25f + n3 * 0.08f, 0.3f),  // Trampelpfad: außen am getretenen Gras
                    < 1.5f => (e + n1 * 0.5f + n2 * 0.22f + n3 * 0.08f, 0.25f),  // Fahrweg
                    _ => (e + n1 * 0.25f + n2 * 0.15f + n3 * 0.12f, 0.3f),        // Kies: knapp außerhalb, zwischen den Steinchen
                };
                var chance = 0.9f * Mathf.Max(0f, 1f - Mathf.Abs(en - mitte) / 0.45f);
                if (Teil(h, 4) >= chance) continue;
                if (raster is { IstLeer: false } && raster.Anteil(x, z, Bodentextur.Wiese) < 0.5) continue;
                liste.Add(new Vector4(x, z, 0.45f + 0.35f * Teil(h, 5), Teil(h, 6)));
            }
        return liste.Count > 0 ? liste.ToArray() : null;
    }

    /// <summary>
    /// Büschel an gemalten Grenzen der Wiese zu Erde, Fels, Kies, Sand, Lehm oder Mist: wo die Wiese um 40 % deckt (dort liegt die Grenze der
    /// Höhenmischung, Wiese steht in der Rangfolge über diesen Texturen) und der Rest überwiegend eine dieser Texturen ist.
    /// </summary>
    private static Vector4[]? BueschelAusBoden(Bodenraster raster, int ti, int tj)
    {
        const float kachel = KachelZellen * 2f;
        const int n = (int)(kachel / BueschelZelle);
        var (i0, j0) = (ti * KachelZellen * 2 - 1, tj * KachelZellen * 2 - 1);
        var splat = new byte[3][];
        for (var g = 0; g < 3; g++) splat[g] = raster.Splat(i0, j0, BodenBild, BodenBild, g);
        // Wert einer Textur an einem Ort (bilinear zwischen den Punkten wie im Shader; Punkt k liegt bei i0 + k)
        float Wert(int textur, float x, float z)
        {
            var (fx, fz) = (x - i0, z - j0);
            var (a, b) = ((int)MathF.Floor(fx), (int)MathF.Floor(fz));
            var (tx, tz) = (fx - a, fz - b);
            var feld = splat[textur / 4];
            float P(int i, int j) => feld[(Math.Clamp(j, 0, BodenBild - 1) * BodenBild + Math.Clamp(i, 0, BodenBild - 1)) * 4 + textur % 4];
            return Mathf.Lerp(Mathf.Lerp(P(a, b), P(a + 1, b), tx), Mathf.Lerp(P(a, b + 1), P(a + 1, b + 1), tx), tz) / 255f;
        }
        int[] nachbarn = { (int)Bodentextur.Erde, (int)Bodentextur.Fels, (int)Bodentextur.Kies, (int)Bodentextur.Sand, (int)Bodentextur.Lehm, (int)Bodentextur.Mist };
        var (x0, z0) = (ti * kachel, tj * kachel);
        var liste = new List<Vector4>();
        for (var cj = 0; cj < n; cj++)
            for (var ci = 0; ci < n; ci++)
            {
                var h = BueschelWurf(ti * n + ci, tj * n + cj, 2);
                var (x, z) = (x0 + (ci + Teil(h, 2)) * BueschelZelle, z0 + (cj + Teil(h, 3)) * BueschelZelle);
                var wiese = Wert(0, x, z);
                var chance = 0.8f * Mathf.Max(0f, 1f - Mathf.Abs(wiese - 0.4f) / 0.12f);
                if (chance <= 0f || Teil(h, 4) >= chance) continue;
                var nachbar = 0f;
                foreach (var t in nachbarn) nachbar += Wert(t, x, z);
                if (nachbar < 0.6f * (1f - wiese)) continue; // Waldboden, Moos, Schlamm oder Pflaster: kein Gras
                liste.Add(new Vector4(x, z, 0.45f + 0.35f * Teil(h, 5), Teil(h, 6)));
            }
        return liste.Count > 0 ? liste.ToArray() : null;
    }

    /// <summary>Eine feste Zahl je Zelle des Gitters und Art (Hash-Mischung, auf jedem Rechner gleich).</summary>
    private static ulong BueschelWurf(int ci, int cj, int art)
    {
        unchecked
        {
            var x = (((ulong)(uint)ci << 32) | (uint)cj) ^ ((ulong)art * 0x9E3779B97F4A7C15UL);
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }

    /// <summary>Der n-te Wurf 0 … &lt;1 aus der Zahl einer Zelle (je 8 Bit).</summary>
    private static float Teil(ulong h, int n) => ((h >> (n * 8)) & 0xFF) / 256f;

    /// <summary>Der Wurf eines Kandidaten (0 … 1) weiter geteilt: Form, Drehung.</summary>
    private static float Teil(float wurf, int n) => n == 0 ? wurf * 7.13f % 1f : wurf * 31.7f % 1f;

    // ── Das Rauschen des Boden-Shaders (hash, rausch in boden.gdshaderinc), damit die Büschel am selben Rand stehen ──

    private static float Hash(float x, float y)
    {
        unchecked
        {
            var (qx, qy) = ((uint)(int)MathF.Floor(x) * 1597334673u, (uint)(int)MathF.Floor(y) * 3812015801u);
            var n = (qx ^ qy) * 1597334673u;
            return (n >> 8) * (1f / 16777216f);
        }
    }

    private static float Rausch(float x, float y)
    {
        var (ix, iy) = (MathF.Floor(x), MathF.Floor(y));
        var (fx, fy) = (x - ix, y - iy);
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx), Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx), fy);
    }
}
