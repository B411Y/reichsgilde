using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Stellen eines Gebäudebrands im Modell (Teil der <c>partial class Stadtansicht</c>; Auftrag „Feuer überall“): Fensteröffnungen und Dachflächen, aus
/// denen die Flammen schlagen. Die Modelle haben dafür keine Marker; das Spiel liest die Geometrie, die es schon kennt:
/// <list type="bullet">
/// <item><b>Fenster</b>: die Scheiben, die <see cref="ModellEinsetzen"/> gemerkt hat (<see cref="ModellInstanz.Fenster"/>: Teil und Fläche); je Scheibe die Mitte, die
/// waagerechte Normale und die Maße (<see cref="Bauteile"/>).</item>
/// <item><b>Dach</b>: die Dreiecke der Flächen, deren Material in <see cref="Dachstoffe"/> steht (dieselbe Erkennung wie der Ruß, <see cref="DachRussSetzen"/>), mit
/// Normale nach oben; gewählt werden Punkte im oberen Teil des Dachs, weit genug auseinander.</item>
/// </list>
/// Ohne Fenster oder ohne Dachflächen (Kulissenblöcke): ein Dachpunkt über der Mitte der obersten Fläche. Knoten „Anker_Fenster…“ und „Anker_Dach…“ im Modell
/// gingen vor, wenn die Modelle sie je bekommen (Wunsch an die Modellarbeit; heute gibt es keine).
/// </summary>
public partial class Stadtansicht
{
    private static float Streu(int saat, int a, int b)
    {
        var h = (uint)saat * 2654435761u ^ (uint)a * 2246822519u ^ (uint)b * 3266489917u;
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
        return (h & 0xFFFFFF) / 16777216f;
    }

    /// <summary>
    /// Bis zu <paramref name="anzahl"/> Fenster, in der Reihenfolge, in der sie Feuer fangen: das erste unten vorn (+Z), die weiteren jeweils am weitesten von den
    /// bisherigen entfernt (so sind die Flammen über die Fassaden verteilt), Fenster nach +Z und die oberen Geschosse bevorzugt. Mindestens 2,2 m Abstand.
    /// </summary>
    private static List<FensterAnker> FensterAnkerSuchen(ModellInstanz modell, int anzahl, int saat)
    {
        var alle = new List<FensterAnker>();
        foreach (var (teil, flaeche) in modell.Fenster)
        {
            if (!teil.IsVisibleInTree() || teil.Mesh is not { } netz) continue;
            foreach (var b in Bauteile(netz, flaeche, LageUnter(modell.Wurzel, teil)))
            {
                var n = new Vector3(b.Normale.X, 0, b.Normale.Z);
                if (b.Groesse.Y < 0.3f || Mathf.Abs(b.Normale.Y) > 0.5f || n.LengthSquared() < 0.01f) continue;
                n = n.Normalized();
                var quer = Mathf.Abs(n.X) > Mathf.Abs(n.Z) ? b.Groesse.Z : b.Groesse.X;
                alle.Add(new FensterAnker(b.Mitte, n, Mathf.Max(quer, 0.4f), b.Groesse.Y));
            }
        }
        var gewaehlt = new List<FensterAnker>();
        if (alle.Count == 0 || anzahl <= 0) return gewaehlt;
        // Der Brandherd: unten vorn (mit etwas Zufall, damit nicht jedes Haus gleich anfängt)
        var herd = alle.OrderByDescending(f => (f.Normale.Z > 0.6f ? 2f : 0f) - f.Mitte.Y * 0.4f + Streu(saat, (int)(f.Mitte.X * 10), (int)(f.Mitte.Y * 10)) * 0.8f).First();
        gewaehlt.Add(herd);
        while (gewaehlt.Count < anzahl)
        {
            FensterAnker? bestes = null;
            var beste = 0f;
            foreach (var f in alle)
            {
                var abstand = gewaehlt.Min(g => g.Mitte.DistanceTo(f.Mitte));
                if (abstand < 2.2f) continue;
                var wert = Mathf.Min(abstand, 8f) + (f.Normale.Z > 0.3f ? 2.5f : 0f) + f.Mitte.Y * 0.35f;
                if (wert > beste) (bestes, beste) = (f, wert);
            }
            if (bestes is null) break;
            gewaehlt.Add(bestes);
        }
        return gewaehlt;
    }

    /// <summary>
    /// Bis zu <paramref name="anzahl"/> Stellen auf dem Dach: Punkte auf den Dachflächen (Dreiecke mit Normale nach oben) im oberen Teil, die erste nahe der Mitte des
    /// Dachs, die weiteren weit auseinander (mindestens 2,2 m). Die Anzahl wächst mit der Dachfläche (etwa eine Stelle je 9 m²). Die Flamme ist 0,7- bis 1,25-mal groß.
    /// </summary>
    private static List<DachAnker> DachAnkerSuchen(ModellInstanz modell, int anzahl, int saat, float groesseMal)
    {
        var punkte = new List<(Vector3 P, Vector3 N, float Flaeche)>();
        float flaecheGesamt = 0f, hoechste = float.MinValue, tiefste = float.MaxValue;
        foreach (var teil in modell.Wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (Nebenteil(teil.Name.ToString()) || !teil.Visible || teil.Mesh is not { } netz) continue;
            var lage = LageUnter(modell.Wurzel, teil);
            for (var f = 0; f < netz.GetSurfaceCount(); f++)
            {
                if (netz.SurfaceGetMaterial(f) is not BaseMaterial3D stoff || !Dachstoffe.Contains(stoff.ResourceName)) continue;
                var felder = netz.SurfaceGetArrays(f);
                var ecken = felder[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var dreiecke = felder[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? Enumerable.Range(0, ecken.Length).ToArray() : felder[(int)Mesh.ArrayType.Index].AsInt32Array();
                for (var i = 0; i + 2 < dreiecke.Length; i += 3)
                {
                    var a = lage * ecken[dreiecke[i]];
                    var b = lage * ecken[dreiecke[i + 1]];
                    var c = lage * ecken[dreiecke[i + 2]];
                    var kreuz = (b - a).Cross(c - a);
                    var flaeche = kreuz.Length() * 0.5f;
                    if (flaeche < 0.05f) continue;
                    var n = kreuz.Normalized();
                    if (n.Y < 0) n = -n;
                    if (n.Y < 0.35f) continue; // Dachfläche, keine Giebelwand
                    var mitte = (a + b + c) / 3f;
                    punkte.Add((mitte, n, flaeche));
                    flaecheGesamt += flaeche;
                    hoechste = Mathf.Max(hoechste, mitte.Y);
                    tiefste = Mathf.Min(tiefste, mitte.Y);
                }
            }
        }
        var ergebnis = new List<DachAnker>();
        if (punkte.Count == 0 || anzahl <= 0)
        {
            // ohne Dachflächen: über der Mitte der obersten Fläche (Kulissen und Blöcke)
            var (min, max) = ModellUmriss(modell.Wurzel);
            if (min.X <= max.X)
                ergebnis.Add(new DachAnker(new Vector3((min.X + max.X) / 2, max.Y - 0.3f, (min.Z + max.Z) / 2), Vector3.Up, Mathf.Clamp(Mathf.Min(max.X - min.X, max.Z - min.Z) / 6f, 0.7f, 1.25f) * groesseMal));
            return ergebnis;
        }
        var ziele = Math.Clamp((int)(flaecheGesamt / 9f) + 1, 1, anzahl);
        var grenze = tiefste + (hoechste - tiefste) * 0.45f; // nur der obere Teil: dort bricht das Feuer durch
        var obere = punkte.Where(p => p.P.Y >= grenze).ToList();
        if (obere.Count == 0) obere = punkte;
        var mitteXZ = new Vector3(obere.Average(p => p.P.X), 0, obere.Average(p => p.P.Z));
        var gr = Mathf.Clamp(Mathf.Sqrt(flaecheGesamt) / 7f, 0.7f, 1.25f) * groesseMal;
        // erste Stelle: nahe der Mitte, im oberen Teil; dann je die am weitesten entfernte
        var erste = obere.OrderBy(p => new Vector2(p.P.X - mitteXZ.X, p.P.Z - mitteXZ.Z).Length() - (p.P.Y - grenze) * 0.4f + Streu(saat, (int)(p.P.X * 10), (int)(p.P.Z * 10)) * 1.2f).First();
        ergebnis.Add(new DachAnker(erste.P, erste.N, gr));
        while (ergebnis.Count < ziele)
        {
            (Vector3 P, Vector3 N, float Flaeche)? bestes = null;
            var beste = 0f;
            foreach (var p in obere)
            {
                var abstand = ergebnis.Min(g => g.Punkt.DistanceTo(p.P));
                if (abstand < 2.2f) continue;
                var wert = abstand + Streu(saat, ergebnis.Count, (int)(p.P.X * 7 + p.P.Z * 13)) * 1.5f;
                if (wert > beste) (bestes, beste) = (p, wert);
            }
            if (bestes is not { } b) break;
            ergebnis.Add(new DachAnker(b.P, b.N, gr * (0.8f + 0.2f * Streu(saat, ergebnis.Count, 5))));
        }
        return ergebnis;
    }
}
