using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Feuer in der Stadt (Phase 2 „Feuer und Flammen“, Teil der <c>partial class Stadtansicht</c>). Alle Flammen und aller Rauch sind <see cref="Feuer"/>-Knoten
/// in der einen Fassung, die der Nutzer gewählt hat (Flipbook der Simulation, Rauch im Shader, für alle Grafikstufen gleich):
/// <list type="bullet">
/// <item><b>Kaminrauch</b>: Die Modelle haben keine Anker für Schornsteine; das Spiel findet sie selbst (<see cref="SchornsteineSuchen"/>: eine kleine,
/// waagerechte Fläche hoch oben, über der nichts liegt und um die herum das Dach tiefer liegt) – einmal je Modelldatei. Wohnhäuser heizen und kochen
/// nach Tageszeit und Jahreszeit (<see cref="WohnhausHeizt"/>), Werkstätten rauchen, solange gearbeitet wird. Der Rauch blendet weich ein und aus.</item>
/// <item><b>Gebäudebrand</b>: Meldet der Kern ein Feuer – in einer Werkstatt steigen die Reparaturminuten (<see cref="Betrieb.SabotageMinuten"/>), am
/// Wohnhaus eines Spielhauses steht <see cref="Konto.WohnhausInstandBis"/> –, brennt das Gebäude sichtbar: große Flammen aus dem Dach (mit Licht),
/// Flammen aus Fenstern, eine dicke Rauchsäule; nach <see cref="BrennMinuten"/> schwelt es nur noch und raucht aus, nach <see cref="GlimmMinuten"/> ist es
/// vorbei. Das Dach bleibt bis zur Instandsetzung rußig. Alles nur Darstellung: Der Kern weiß nichts von Flammen.</item>
/// </list>
/// Die Nachtlichter (Fackeln, Laternen, Feuerkörbe) stehen in Stadtansicht.Nachtlichter.cs, Kerzen und Esse der Innenräume in Stadtansicht.Innenmodell.cs.
/// </summary>
public partial class Stadtansicht
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Geometrie: zusammenhängende Teile einer Fläche
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein zusammenhängendes Stück einer Fläche (etwa eine Fensterscheibe, eine Kerzenflamme), im Raum des Modells.</summary>
    private sealed record Bauteil(Vector3 Min, Vector3 Max, Vector3 Normale, int Ecken)
    {
        public Vector3 Mitte => (Min + Max) * 0.5f;
        public Vector3 Groesse => Max - Min;
    }

    /// <summary>
    /// Zerlegt eine Fläche in zusammenhängende Teile: Ecken desselben Dreiecks und Ecken an derselben Stelle (auf den Millimeter) gehören zusammen
    /// (wie <c>FensterKennungen</c>). <paramref name="lage"/> bringt die Ecken in den gewünschten Raum.
    /// </summary>
    private static List<Bauteil> Bauteile(Mesh netz, int flaeche, Transform3D lage)
    {
        var felder = netz.SurfaceGetArrays(flaeche);
        var ecken = felder[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var normalen = felder[(int)Mesh.ArrayType.Normal].VariantType == Variant.Type.Nil ? null : felder[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var dreiecke = felder[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? Enumerable.Range(0, ecken.Length).ToArray() : felder[(int)Mesh.ArrayType.Index].AsInt32Array();
        var wurzel = Enumerable.Range(0, ecken.Length).ToArray();
        int Finden(int i)
        {
            while (wurzel[i] != i) i = wurzel[i] = wurzel[wurzel[i]];
            return i;
        }
        void Vereinen(int a, int b) => wurzel[Finden(a)] = Finden(b);
        for (var i = 0; i + 2 < dreiecke.Length; i += 3)
        {
            Vereinen(dreiecke[i], dreiecke[i + 1]);
            Vereinen(dreiecke[i], dreiecke[i + 2]);
        }
        var stellen = new Dictionary<(int, int, int), int>();
        for (var i = 0; i < ecken.Length; i++)
        {
            var stelle = ((int)Mathf.Round(ecken[i].X * 1000), (int)Mathf.Round(ecken[i].Y * 1000), (int)Mathf.Round(ecken[i].Z * 1000));
            if (stellen.TryGetValue(stelle, out var andere)) Vereinen(i, andere);
            else stellen[stelle] = i;
        }
        var teile = new Dictionary<int, (Vector3 Min, Vector3 Max, Vector3 Normale, int Ecken)>();
        for (var i = 0; i < ecken.Length; i++)
        {
            var p = lage * ecken[i];
            var n = normalen is null ? Vector3.Zero : lage.Basis * normalen[i];
            var w = Finden(i);
            teile[w] = teile.TryGetValue(w, out var t) ? (t.Min.Min(p), t.Max.Max(p), t.Normale + n, t.Ecken + 1) : (p, p, n, 1);
        }
        return teile.Values.Select(t => new Bauteil(t.Min, t.Max, t.Normale.LengthSquared() > 1e-8f ? t.Normale.Normalized() : Vector3.Zero, t.Ecken)).ToList();
    }

    /// <summary>Teile eines Modells, die nie zusammen mit den übrigen zu sehen sind oder nicht zum Haus gehören.</summary>
    private static bool Nebenteil(string name) => name is "Geruest" or "Leerstand" or "Fahne" or "Wappen";

    /// <summary>Schließen sich zwei Teile aus (verschiedene Stufen, verschiedene Varianten)? Dann verdeckt das eine das andere nie.</summary>
    private static bool Ausschliessend(string a, string b) =>
        a != b && ((a.StartsWith("Stufe") && b.StartsWith("Stufe")) || (a.StartsWith("Variante") && b.StartsWith("Variante")));

    // ═════════════════════════════════════════════════════════════════════════
    //  Schornsteine finden
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Gefundene Schornsteine je Modelldatei: Pfad des Teils (vom Modell aus) und die Mündung im Raum dieses Teils.</summary>
    private static readonly Dictionary<string, List<(string Teil, Vector3 Lage)>> _schornsteinListen = new();

    private sealed class Flaechenstueck
    {
        public Vector3 A, B, C;
        public float MinX, MaxX, MinZ, MaxZ;
        public int Teil;
    }

    /// <summary>
    /// Sucht die Schornsteine eines Modells (einmal je Datei, das Ergebnis gilt für jede Kopie): Kandidaten sind waagerechte, nach oben zeigende
    /// Flächen höher als 3 m, die zusammen höchstens 1,3 m breit und etwa quadratisch sind (Kopf eines Schornsteins, Rand einer Esse; nicht die
    /// Mauerkrone eines Treppengiebels). Ein Kandidat zählt, wenn über
    /// seiner Mitte nichts liegt und ringsum (0,6 m neben seinem Rand) alles mindestens 0,3 m tiefer liegt – so fallen Firste, Simse, Fensterbänke
    /// und Gauben heraus. Dicht beieinander liegende Funde gelten als einer.
    /// </summary>
    private static List<(string Teil, Vector3 Lage)> SchornsteineSuchen(Node3D modell)
    {
        var teile = modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
                          .Where(t => t.Mesh is not null && !Nebenteil(t.Name.ToString())).ToList();
        var namen = teile.Select(t => t.Name.ToString()).ToArray();
        var alle = new List<Flaechenstueck>();
        var oben = new List<(Flaechenstueck D, float Flaeche)>();
        for (var ti = 0; ti < teile.Count; ti++)
        {
            var netz = teile[ti].Mesh!;
            var lage = LageUnter(modell, teile[ti]);
            for (var f = 0; f < netz.GetSurfaceCount(); f++)
            {
                if (netz is ArrayMesh am && am.SurfaceGetPrimitiveType(f) != Mesh.PrimitiveType.Triangles) continue;
                var felder = netz.SurfaceGetArrays(f);
                var ecken = felder[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var normalen = felder[(int)Mesh.ArrayType.Normal].VariantType == Variant.Type.Nil ? null : felder[(int)Mesh.ArrayType.Normal].AsVector3Array();
                var index = felder[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? Enumerable.Range(0, ecken.Length).ToArray() : felder[(int)Mesh.ArrayType.Index].AsInt32Array();
                for (var i = 0; i + 2 < index.Length; i += 3)
                {
                    var d = new Flaechenstueck { A = lage * ecken[index[i]], B = lage * ecken[index[i + 1]], C = lage * ecken[index[i + 2]], Teil = ti };
                    d.MinX = Mathf.Min(d.A.X, Mathf.Min(d.B.X, d.C.X));
                    d.MaxX = Mathf.Max(d.A.X, Mathf.Max(d.B.X, d.C.X));
                    d.MinZ = Mathf.Min(d.A.Z, Mathf.Min(d.B.Z, d.C.Z));
                    d.MaxZ = Mathf.Max(d.A.Z, Mathf.Max(d.B.Z, d.C.Z));
                    alle.Add(d);
                    var kreuz = (d.B - d.A).Cross(d.C - d.A);
                    var laenge = kreuz.Length();
                    if (laenge < 1e-5f) continue;
                    var n = kreuz / laenge;
                    if (normalen is not null)
                    {
                        var ecke = lage.Basis * (normalen[index[i]] + normalen[index[i + 1]] + normalen[index[i + 2]]);
                        if (n.Dot(ecke) < 0) n = -n;
                    }
                    else n = n.Abs();
                    if (n.Y > 0.95f && d.A.Y > 3f) oben.Add((d, laenge * 0.5f));
                }
            }
        }

        // Nach oben zeigende Flächen gleicher Höhe, die sich berühren, zu Gruppen zusammenfassen
        var gruppe = Enumerable.Range(0, oben.Count).ToArray();
        int Finden(int i)
        {
            while (gruppe[i] != i) i = gruppe[i] = gruppe[gruppe[i]];
            return i;
        }
        for (var i = 0; i < oben.Count; i++)
            for (var j = i + 1; j < oben.Count; j++)
            {
                var (a, b) = (oben[i].D, oben[j].D);
                if (Mathf.Abs(a.A.Y - b.A.Y) > 0.02f || a.Teil != b.Teil) continue;
                if (a.MaxX + 0.05f < b.MinX || b.MaxX + 0.05f < a.MinX || a.MaxZ + 0.05f < b.MinZ || b.MaxZ + 0.05f < a.MinZ) continue;
                gruppe[Finden(i)] = Finden(j);
            }

        // Höchster Punkt des Modells über (x, z), ohne die Teile, die sich mit dem Teil des Kandidaten ausschließen
        float Hoechster(float x, float z, int teil)
        {
            var hoch = float.NegativeInfinity;
            foreach (var d in alle)
            {
                if (x < d.MinX || x > d.MaxX || z < d.MinZ || z > d.MaxZ || Ausschliessend(namen[d.Teil], namen[teil])) continue;
                // Baryzentrisch in der Ebene x/z
                var v0 = new Vector2(d.B.X - d.A.X, d.B.Z - d.A.Z);
                var v1 = new Vector2(d.C.X - d.A.X, d.C.Z - d.A.Z);
                var v2 = new Vector2(x - d.A.X, z - d.A.Z);
                var nenner = v0.X * v1.Y - v1.X * v0.Y;
                if (Mathf.Abs(nenner) < 1e-8f) continue;
                var u = (v2.X * v1.Y - v1.X * v2.Y) / nenner;
                var v = (v0.X * v2.Y - v2.X * v0.Y) / nenner;
                if (u < -1e-4f || v < -1e-4f || u + v > 1.0001f) continue;
                hoch = Mathf.Max(hoch, d.A.Y + u * (d.B.Y - d.A.Y) + v * (d.C.Y - d.A.Y));
            }
            return hoch;
        }

        var funde = new List<(Vector3 Lage, int Teil)>();
        foreach (var g in Enumerable.Range(0, oben.Count).GroupBy(Finden))
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var flaeche = 0f;
            var y = 0f;
            var teil = 0;
            foreach (var i in g)
            {
                var d = oben[i].D;
                min = new Vector2(Mathf.Min(min.X, d.MinX), Mathf.Min(min.Y, d.MinZ));
                max = new Vector2(Mathf.Max(max.X, d.MaxX), Mathf.Max(max.Y, d.MaxZ));
                flaeche += oben[i].Flaeche;
                y = d.A.Y;
                teil = d.Teil;
            }
            var ausdehnung = max - min;
            // Ein Schornsteinkopf ist etwa quadratisch; der oberste Block eines Treppengiebels ist eine Mauerkrone (1 × 0,6 m)
            if (ausdehnung.X > 1.3f || ausdehnung.Y > 1.3f || flaeche < 0.06f) continue;
            if (Mathf.Max(ausdehnung.X, ausdehnung.Y) > 1.5f * Mathf.Min(ausdehnung.X, ausdehnung.Y)) continue;
            var mitte = (min + max) * 0.5f;
            if (Hoechster(mitte.X, mitte.Y, teil) > y + 0.05f) continue;   // etwas liegt darüber
            var r = Mathf.Max(ausdehnung.X, ausdehnung.Y) * 0.5f + 0.6f;
            var frei = true;
            for (var k = 0; k < 8 && frei; k++)
            {
                var w = k * Mathf.Tau / 8;
                var hoch = Hoechster(mitte.X + Mathf.Cos(w) * r, mitte.Y + Mathf.Sin(w) * r, teil);
                frei = hoch < y - 0.3f;
            }
            if (frei) funde.Add((new Vector3(mitte.X, y + 0.03f, mitte.Y), teil));
        }
        // Dicht beieinander (Rand einer Esse in vier Stücken): einer genügt, der höchste
        funde.Sort((a, b) => b.Lage.Y.CompareTo(a.Lage.Y));
        var ergebnis = new List<(string, Vector3)>();
        var genommen = new List<Vector3>();
        foreach (var (lage, teil) in funde)
        {
            if (genommen.Any(g => new Vector2(g.X - lage.X, g.Z - lage.Z).Length() < 1.6f)) continue;
            genommen.Add(lage);
            ergebnis.Add((modell.GetPathTo(teile[teil]).ToString(), LageUnter(modell, teile[teil]).AffineInverse() * lage));
        }
        return ergebnis;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kaminrauch
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Ein Schornstein mit seinem Rauch: wem er gehört (Kennung des Ankers: Betrieb, Wohnhaus, Einwohner, Kulisse) und wie stark er gerade raucht.</summary>
    private sealed class Kamin
    {
        public required Feuer Rauch { get; init; }
        public required string Kennung { get; init; }
        public required uint Saat { get; init; }
        public float Stand { get; set; }
    }

    private readonly List<Kamin> _kamine = new();
    private readonly Dictionary<string, Betrieb> _betriebNachName = new();

    /// <summary>Setzt auf jeden Schornstein des Modells einen Kaminrauch (zunächst aus); <paramref name="kennung"/> sagt, wem das Haus gehört.</summary>
    private void KamineAnbringen(Node3D modell, string datei, string kennung)
    {
        if (kennung == "Dom") return; // Fialen und Turmhelme sind keine Schornsteine, und eine Kirche heizt nicht
        if (OS.GetEnvironment("RG_OHNERAUCH") != "") return; // nur für Messungen: ohne Kaminrauch
        if (!_schornsteinListen.TryGetValue(datei, out var liste))
        {
            _schornsteinListen[datei] = liste = SchornsteineSuchen(modell);
            GD.Print($"[feuer] Schornsteine in {datei}: {liste.Count}");
        }
        foreach (var (pfad, lage) in liste)
        {
            if (modell.GetNodeOrNull<Node3D>(pfad) is not { } teil) continue;
            var saat = Streuung(kennung.Length * 131 + kennung.Sum(c => c), (int)(lage.X * 10 + lage.Z * 7));
            var rauch = Feuer.Neu(FeuerVorgabe.Kamin, false, (saat & 0xFFFF) / 65535f);
            rauch.Name = "Kaminrauch";
            rauch.Position = lage;
            rauch.RauchStaerke = 0f;
            teil.AddChild(rauch);
            _kamine.Add(new Kamin { Rauch = rauch, Kennung = kennung, Saat = saat });
        }
    }

    /// <summary>
    /// Heizt und kocht ein Wohnhaus gerade? Im Winter brennt das Feuer den ganzen Tag, im Frühling und Herbst morgens (5–9 Uhr) und abends
    /// (16:30–22 Uhr), im Sommer nur zum Kochen (5:30–7:30 und 18–20 Uhr). Jedes Haus hat seinen eigenen Versatz (±36 min), und nicht jedes macht mit
    /// (Winter fast alle, Sommer gut die Hälfte) – so qualmen nie alle Schornsteine zugleich an und aus.
    /// </summary>
    private static bool WohnhausHeizt(uint saat, float uhrzeitMinuten, Jahreszeit jahreszeit)
    {
        var h = uhrzeitMinuten / 60f - ((saat & 0xFF) / 255f - 0.5f) * 1.2f;
        var dabei = ((saat >> 8) & 0xFF) / 255f;
        bool Zwischen(float von, float bis) => h >= von && h < bis;
        return jahreszeit switch
        {
            Jahreszeit.Winter => dabei < 0.95f && Zwischen(4.5f, 23f),
            Jahreszeit.Sommer => dabei < 0.6f && (Zwischen(5.5f, 7.5f) || Zwischen(18f, 20f)),
            _ => dabei < 0.85f && (Zwischen(5f, 9f) || Zwischen(16.5f, 22f)),
        };
    }

    /// <summary>Jedes Bild: Rauch nach Haus, Tageszeit und Arbeit weich ein- und ausblenden (etwa vier Sekunden). Ein brennendes Haus hat keinen Kaminrauch.</summary>
    private void KamineAktualisieren(Stadtwirtschaft w, Spielzeit zeit, float uhrzeit, double delta)
    {
        if (_kamine.Count == 0) return;
        if (_betriebNachName.Count != _betriebe.Count)
        {
            _betriebNachName.Clear();
            foreach (var b in _betriebe.Keys) _betriebNachName[b.Name] = b;
        }
        var jahreszeit = GezeigteJahreszeit(zeit.Jahreszeit);
        var schritt = (float)delta * 0.25f;
        var weg = false;
        foreach (var k in _kamine)
        {
            if (!IsInstanceValid(k.Rauch)) { weg = true; continue; }
            bool soll;
            if (_braende.ContainsKey(k.Kennung)) soll = false;
            else if (_betriebNachName.TryGetValue(k.Kennung, out var b))
                soll = w.IstArbeitszeit(b, zeit) && b.Belegt > 0 && b.Stillstand is null && b.SabotageMinuten == 0;
            else soll = WohnhausHeizt(k.Saat, uhrzeit, jahreszeit);
            var stand = Mathf.MoveToward(k.Stand, soll ? 1f : 0f, schritt);
            if (Mathf.IsEqualApprox(stand, k.Stand)) continue;
            k.Stand = stand;
            k.Rauch.RauchStaerke = stand;
        }
        if (weg) _kamine.RemoveAll(k => !IsInstanceValid(k.Rauch));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Gebäudebrand
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>So lange (Spielminuten) schlagen nach dem Ausbruch Flammen aus dem Haus; danach schwelt es nur noch und raucht aus.</summary>
    private const int BrennMinuten = 120;
    /// <summary>So lange (Spielminuten nach dem Ende der Flammen) steigt noch Rauch auf.</summary>
    private const int GlimmMinuten = 180;

    private sealed class Brand
    {
        public required Node3D Wurzel { get; init; }
        public required long Start { get; init; }
        public List<Feuer> Feuer { get; } = new();
        public bool Flammen { get; set; } = true;
    }

    /// <summary>Brennende Gebäude nach Kennung (Betriebsname oder Wohnhaus-Kennung).</summary>
    private readonly Dictionary<string, Brand> _braende = new();
    /// <summary>Wie viele Reparaturminuten jede Werkstatt zuletzt hatte: steigen sie, hat es gebrannt (der Kern meldet keinen Zeitpunkt).</summary>
    private readonly Dictionary<Betrieb, int> _sabotageGesehen = new();
    private readonly HashSet<Betrieb> _russDach = new();
    /// <summary>Startoption <c>--brand=…</c> (mit Komma mehrere): Feuer nur in der Darstellung – die Werkstatt des Spielers (<c>werkstatt</c> oder <c>1</c>), sein Wohnhaus (<c>haus</c>) oder ein Betrieb mit Namen.</summary>
    private readonly HashSet<string> _brandVorgabe = (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--brand="))?["--brand=".Length..] ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();

    /// <summary>Brennt oder schwelt es dort gerade (nach dem Ausbruch zur Spielminute <paramref name="start"/>)?</summary>
    private static bool BrandLaeuft(long start, long jetzt) => jetzt >= start && jetzt < start + BrennMinuten + GlimmMinuten;

    /// <summary>
    /// Zündet das Modell an: ein großes Feuer aus dem Dach (mit Licht und dicker Rauchsäule) und Flammen aus bis zu drei Fenstern, vorzugsweise vorn
    /// und unten. Die Größe folgt der Grundfläche. Alles hängt an einem Knoten im Modell und geht mit ihm.
    /// </summary>
    private void BrandAnbringen(string kennung, ModellInstanz modell, long start)
    {
        // Brennt dasselbe Modell schon, bleibt es dabei; ein neu gebautes Modell (Wohnhaus nach einer Änderung) bekommt das Feuer mit derselben Startzeit
        if (_braende.TryGetValue(kennung, out var alt) && IsInstanceValid(alt.Wurzel) && alt.Wurzel.GetParent() == modell.Wurzel) return;
        var (min, max) = ModellUmriss(modell.Wurzel);
        if (min.X > max.X) return;
        var (dachMin, dachMax) = ModellUmriss(modell.Wurzel, min.Y + (max.Y - min.Y) * 0.7f);
        if (dachMin.X > dachMax.X) (dachMin, dachMax) = (min, max);
        var wurzel = new Node3D { Name = "Brand" };
        modell.Wurzel.AddChild(wurzel);
        var brand = new Brand { Wurzel = wurzel, Start = start };
        // Über der Mitte des Dachs (der obere Teil des Modells, nicht der Hof), etwas unter dem First
        var mitte = (dachMin + dachMax) * 0.5f;
        var groesse = Mathf.Clamp(Mathf.Min(dachMax.X - dachMin.X, dachMax.Z - dachMin.Z) / 5f, 0.7f, 1.3f);
        var dach = Feuer.Neu(FeuerVorgabe.Gebaeudebrand, true, (Streuung(kennung.Sum(c => c), 41) & 0xFFFF) / 65535f, groesse);
        dach.Position = new Vector3(mitte.X, max.Y - Mathf.Min(2.4f, (max.Y - min.Y) * 0.3f), mitte.Z);
        wurzel.AddChild(dach);
        brand.Feuer.Add(dach);
        foreach (var lage in BrandFenster(modell, 3))
        {
            var f = Feuer.Neu(FeuerVorgabe.Fensterbrand, false, (Streuung((int)(lage.X * 100), (int)(lage.Z * 100)) & 0xFFFF) / 65535f);
            f.Position = lage;
            wurzel.AddChild(f);
            brand.Feuer.Add(f);
        }
        _braende[kennung] = brand;
        GD.Print($"[feuer] Brand: {kennung} ab Spielminute {start} ({brand.Feuer.Count - 1} Fenster)");
    }

    /// <summary>
    /// Der Umriss eines Modells (lokal), ohne Gerüst, Fahne, Wappen und unsichtbare Teile (andere Stufen). Mit <paramref name="abHoehe"/> nur der
    /// Teil darüber, aus den Ecken der Netze (so liegt die Mitte des Dachs über dem Haus, nicht über dem Hof).
    /// </summary>
    private static (Vector3 Min, Vector3 Max) ModellUmriss(Node3D modell, float? abHoehe = null)
    {
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = -min;
        foreach (var teil in modell.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (Nebenteil(teil.Name.ToString()) || !teil.Visible || teil.Mesh is not { } netz) continue;
            var lage = LageUnter(modell, teil);
            if (abHoehe is not { } hoehe)
            {
                var aabb = lage * netz.GetAabb();
                min = min.Min(aabb.Position);
                max = max.Max(aabb.End);
                continue;
            }
            for (var f = 0; f < netz.GetSurfaceCount(); f++)
                foreach (var ecke in netz.SurfaceGetArrays(f)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                {
                    var p = lage * ecke;
                    if (p.Y < hoehe) continue;
                    min = min.Min(p);
                    max = max.Max(p);
                }
        }
        return (min, max);
    }

    /// <summary>
    /// Bis zu <paramref name="anzahl"/> Fenster für Flammen: je Scheibe die Mitte und die Richtung nach außen (aus den Fensterflächen des Modells, die
    /// <see cref="ModellEinsetzen"/> gemerkt hat). Bevorzugt die Vorderseite (+Z) und die unteren Geschosse, mindestens 2,5 m auseinander. Der Fuß der
    /// Flamme liegt etwas vor der Scheibe und unter ihrer Mitte.
    /// </summary>
    private static List<Vector3> BrandFenster(ModellInstanz modell, int anzahl)
    {
        var scheiben = new List<Bauteil>();
        foreach (var (teil, flaeche) in modell.Fenster)
            if (teil.IsVisibleInTree() && teil.Mesh is { } netz)
                scheiben.AddRange(Bauteile(netz, flaeche, LageUnter(modell.Wurzel, teil)).Where(b => b.Groesse.Y > 0.3f && Mathf.Abs(b.Normale.Y) < 0.5f));
        var gewaehlt = new List<Vector3>();
        foreach (var s in scheiben.OrderByDescending(s => s.Normale.Z > 0.6f).ThenBy(s => s.Mitte.Y).ThenBy(s => s.Mitte.X))
        {
            var lage = s.Mitte + s.Normale * 0.12f + new Vector3(0, -s.Groesse.Y * 0.3f, 0);
            if (gewaehlt.Any(g => g.DistanceTo(lage) < 2.5f)) continue;
            gewaehlt.Add(lage);
            if (gewaehlt.Count >= anzahl) break;
        }
        return gewaehlt;
    }

    /// <summary>Färbt das Dach eines Werkstattmodells rußig (nach einem Brand, bis die Reparatur fertig ist) oder gibt ihm seine Farbe zurück.</summary>
    private void DachRussSetzen(ModellInstanz modell, bool russ)
    {
        foreach (var teil in modell.Wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not { } netz) continue;
            for (var i = 0; i < netz.GetSurfaceCount(); i++)
                if (netz.SurfaceGetMaterial(i) is BaseMaterial3D stoff && Dachstoffe.Contains(stoff.ResourceName))
                    teil.SetSurfaceOverrideMaterial(i, russ ? Umgefaerbt(stoff, stoff.AlbedoColor.Lerp(Russ, 0.8f)) : null);
        }
    }

    /// <summary>
    /// Jedes Bild für jede Werkstatt: Steigen die Reparaturminuten (Kern: <see cref="Betrieb.SabotageMinuten"/>), ist gerade ein Feuer ausgebrochen;
    /// beim ersten Blick (nach dem Laden) gilt ein laufender Schaden als alter Brand (nur Ruß, keine Flammen). Mit <c>--brand</c> brennt die Werkstatt
    /// des Spielers (oder ein Betrieb mit Namen) gleich beim Start, nur in der Darstellung.
    /// </summary>
    private void WerkstattBrandPruefen(Betrieb b, BetriebsAnsicht a, long jetzt)
    {
        if (a.Modell is not { } modell) return;
        var minuten = b.SabotageMinuten;
        var neu = _sabotageGesehen.TryGetValue(b, out var vorher) && minuten > vorher;
        if (_brandVorgabe.Count > 0 && (_brandVorgabe.Remove(b.Name) || (a.Spieler && (_brandVorgabe.Remove("1") | _brandVorgabe.Remove("werkstatt")))))
            neu = true;
        _sabotageGesehen[b] = minuten;
        if (neu) BrandAnbringen(b.Name, modell, jetzt);
        var russ = minuten > 0 || _braende.ContainsKey(b.Name);
        if (russ != _russDach.Contains(b))
        {
            if (russ) _russDach.Add(b); else _russDach.Remove(b);
            DachRussSetzen(modell, russ);
        }
    }

    /// <summary>Wann das Wohnhaus eines Spielhauses zu brennen begann (Spielminute), wenn es noch instand gesetzt wird; sonst null.</summary>
    private static long? WohnhausBrandStart(Konto k, Stadtwirtschaft w, long jetzt)
    {
        if (k.WohnhausInstandBis is not { } bis || bis <= jetzt) return null;
        var dauer = w.Gauner.IstAktiv && w.Gauner.Daten.Wohnhaus is { } wh ? wh.Brand.InstandMinuten : Spielzeit.MinutenProTag;
        return bis - dauer;
    }

    /// <summary>Jedes Bild: Flammen aus, wenn die Brennzeit um ist (dann schwelt es und der Rauch wird dünner), und weg, wenn alles vorbei ist.</summary>
    private void BraendeAktualisieren(long jetzt, double bruchteil)
    {
        if (_braende.Count == 0) return;
        List<string>? fertig = null;
        foreach (var (kennung, brand) in _braende)
        {
            var t = jetzt - brand.Start + (float)bruchteil;
            if (!IsInstanceValid(brand.Wurzel) || t >= BrennMinuten + GlimmMinuten)
            {
                (fertig ??= new()).Add(kennung);
                continue;
            }
            var flammen = t < BrennMinuten;
            if (flammen != brand.Flammen)
            {
                brand.Flammen = flammen;
                foreach (var f in brand.Feuer) f.FlammeAn = flammen;
            }
            // Ausbruch: der Rauch quillt in den ersten Minuten auf; Schwelen: er wird dünner
            var rauch = t < 4f ? Mathf.Clamp(t / 4f, 0.05f, 1f) : flammen ? 1f : 1f - 0.85f * (t - BrennMinuten) / GlimmMinuten;
            foreach (var f in brand.Feuer)
                if (Mathf.Abs(f.RauchStaerke - rauch) > 0.01f) f.RauchStaerke = rauch;
        }
        if (fertig is null) return;
        foreach (var kennung in fertig)
        {
            GD.Print($"[feuer] Brand vorbei: {kennung} ({(IsInstanceValid(_braende[kennung].Wurzel) ? "ausgebrannt" : "Modell neu gebaut")}, Spielminute {jetzt})");
            if (IsInstanceValid(_braende[kennung].Wurzel)) _braende[kennung].Wurzel.QueueFree();
            _braende.Remove(kennung);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Innenräume: Kerzen und Esse
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Das Feuer der Esse im Raum: die Flammen der Esse ohne eigenes Glutbett (das Modell hat seine Glut) und ohne Rauch (er stiege durch den Rauchfang).</summary>
    private static FeuerArt? _herdfeuer;
    private static FeuerArt Herdfeuer()
    {
        if (_herdfeuer is not null) return _herdfeuer;
        _herdfeuer = (FeuerArt)FeuerArt.Neu(FeuerVorgabe.Esse).Duplicate();
        _herdfeuer.Form = FeuerForm.Flamme;
        _herdfeuer.RauchMenge = 0f;
        _herdfeuer.Zungen = 4;
        _herdfeuer.Helligkeit = 1.1f; // im hellen Raum genügt weniger, sonst wird die Esse ein Fleck im Bloom
        return _herdfeuer;
    }

    /// <summary>
    /// Echte Flammen in einem Raummodell: Die Kerzen der Modelle haben eine kleine Flamme aus dem Material „Glut“ (ein Kegel von 6–8 cm auf dem
    /// Wachs); auf jeden solchen Kegel setzt das Spiel eine Kerzenflamme (<see cref="FeuerVorgabe.Kerze"/>). In der Schmiede brennen über der
    /// flachen Glut der Esse (und der zweiten Esse ab Stufe 2) niedrige Flammen mit Funken. Ohne eigenes Licht: Die Räume haben ihre Glut-Lichter.
    /// Sie brennen, wenn die Glut glüht (<see cref="ModellRaumDaten.Glueht"/>). Die Flammen eines immer brennenden Kamins (Teil „Flammen“) bleiben, wie sie sind.
    /// </summary>
    private void InnenFlammenAnlegen(ModellRaumDaten md, Node3D wurzel, string datei, bool dauerfeuer)
    {
        var schmiede = datei.Contains("schmied");
        int kerzen = 0, essen = 0;
        foreach (var teil in wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (teil.Mesh is not { } netz || (dauerfeuer && teil.Name.ToString() == "Flammen")) continue;
            for (var i = 0; i < netz.GetSurfaceCount(); i++)
            {
                if (netz.SurfaceGetMaterial(i)?.ResourceName != "Glut") continue;
                foreach (var b in Bauteile(netz, i, Transform3D.Identity))
                {
                    var g = b.Groesse;
                    Feuer? flamme = null;
                    if (g.X <= 0.06f && g.Z <= 0.06f && g.Y is >= 0.03f and <= 0.13f)
                    {
                        // Kerzenflamme: der Kegel steht auf dem Docht, die echte Flamme so hoch wie er
                        flamme = Feuer.Neu(FeuerVorgabe.Kerze, false, (Streuung(kerzen + 7, (int)(b.Mitte.X * 1000)) & 0xFFFF) / 65535f, Mathf.Clamp(g.Y / 0.065f, 0.8f, 1.3f));
                        flamme.Position = new Vector3(b.Mitte.X, b.Min.Y, b.Mitte.Z);
                        kerzen++;
                    }
                    else if (schmiede && g.Y < 0.12f && Mathf.Min(g.X, g.Z) is >= 0.4f and <= 2f && Mathf.Max(g.X, g.Z) <= 2.2f && b.Normale.Y > 0.7f)
                    {
                        // Glutbett der Esse: niedrige Flammen über der Mitte
                        flamme = Feuer.Neu(FeuerVorgabe.Esse, false, (Streuung(essen + 3, 77) & 0xFFFF) / 65535f, Mathf.Clamp(Mathf.Min(g.X, g.Z) / 0.75f, 0.9f, 1.25f));
                        flamme.Art = Herdfeuer();
                        flamme.Position = new Vector3(b.Mitte.X, b.Max.Y, b.Mitte.Z);
                        essen++;
                    }
                    if (flamme is null) continue;
                    // Im Raum blickt die Kamera nur leicht von oben: aufrecht stehende Flammen (nur um die Hochachse gedreht), sonst neigt sich die Fläche
                    // nach vorn und legt sich vor eine Figur, die vor der Esse steht
                    flamme.Ausrichtung = FeuerAusrichtung.Hochachse;
                    flamme.Name = "Flamme";
                    flamme.Visible = md.Glueht;
                    teil.AddChild(flamme);
                    md.Flammen.Add(flamme);
                }
            }
        }
        if (kerzen + essen > 0) GD.Print($"[feuer] Raum {datei}: {kerzen} Kerzen, {essen} Essen mit Flamme");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Einstieg
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Jedes Bild aus <see cref="Aktualisieren"/>: Kaminrauch und brennende Häuser.</summary>
    private void FeuerAktualisieren(Stadtwirtschaft w, Spielzeit zeit, double bruchteil, double delta)
    {
        KamineAktualisieren(w, zeit, zeit.UhrzeitInMinuten + (float)bruchteil, delta);
        BraendeAktualisieren(zeit.VergangeneMinuten, bruchteil);
    }
}
