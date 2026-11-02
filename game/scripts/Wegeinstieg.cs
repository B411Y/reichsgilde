namespace Reichsgilde;

/// <summary>
/// Wie eine Figur, die schon unterwegs ist, auf ihren neuen Weg kommt (Nutzer, 1.11.2026): nicht über den nächsten Knoten des Gassennetzes (der oft
/// hinter ihr liegt – dann liefe sie erst zurück und danach den Weg, auf dem sie ohnehin war), sondern vom Wegstück aus, auf dem sie gerade geht, und
/// gleich in Zielrichtung. Am Ziel ebenso: Sie verlässt das Netz am Knoten, von dem aus der Rest am kürzesten ist.
///
/// Nur Rechnung ohne Godot (Punkte als x, z), damit ein Test sie mitübersetzen kann (<c>WegeinstiegTests</c>); die Darstellung
/// (Stadtansicht.Passantenwege.cs) gibt ihr die Pfade, sagt, welche Punkte Knoten des Netzes sind, und sucht mit <c>Gassennetz.PfadZwischen</c> den
/// kürzesten Weg zwischen allen Einstiegen und Ausstiegen zugleich.
/// </summary>
public static class Wegeinstieg
{
    /// <summary>
    /// Ein Einstieg ins Netz (oder Ausstieg): der <see cref="Knoten"/>, die Meter bis dorthin (oder von dort bis zum Ziel) und die Punkte des Pfades,
    /// über die es geht (Nummern im Pfad, in Gehrichtung; beim Einstieg ohne die Stelle selbst, beim Ausstieg ab dem Knoten bis zum Ende).
    /// </summary>
    public readonly record struct Anlauf(int Knoten, float Laenge, int[] Folge);

    /// <summary>
    /// Die Einstiege von der Stelle nach <paramref name="strecke"/> Metern auf dem bisherigen <paramref name="pfad"/>: vorwärts bis zum ersten Punkt, der ein
    /// Knoten des Netzes ist (<paramref name="knotenAn"/> gibt seine Nummer oder −1), und ebenso rückwärts. Höchstens zwei; keiner, wenn der Pfad keinen
    /// Knoten hat.
    /// </summary>
    public static List<Anlauf> Einstiege(IReadOnlyList<(float X, float Z)> pfad, float strecke, Func<int, int> knotenAn)
    {
        var liste = new List<Anlauf>();
        if (pfad.Count < 2) return liste;
        // das Stück, auf dem die Stelle liegt, und die Stelle selbst
        var (i, rest) = (0, MathF.Max(strecke, 0));
        while (i + 2 < pfad.Count && rest > Abstand(pfad[i], pfad[i + 1])) { rest -= Abstand(pfad[i], pfad[i + 1]); i++; }
        var stueck = Abstand(pfad[i], pfad[i + 1]);
        rest = MathF.Min(rest, stueck);
        var stelle = Lerp(pfad[i], pfad[i + 1], stueck > 0 ? rest / stueck : 0);

        // vorwärts
        var (folge, laenge, davor) = (new List<int>(), 0f, stelle);
        for (var k = i + 1; k < pfad.Count; k++)
        {
            laenge += Abstand(davor, pfad[k]);
            folge.Add(k);
            davor = pfad[k];
            if (knotenAn(k) is var knoten and >= 0) { liste.Add(new Anlauf(knoten, laenge, folge.ToArray())); break; }
        }
        // rückwärts (umkehren)
        (folge, laenge, davor) = (new List<int>(), 0f, stelle);
        for (var k = i; k >= 0; k--)
        {
            laenge += Abstand(davor, pfad[k]);
            folge.Add(k);
            davor = pfad[k];
            if (knotenAn(k) is var knoten and >= 0) { liste.Add(new Anlauf(knoten, laenge, folge.ToArray())); break; }
        }
        return liste;
    }

    /// <summary>Die Ausstiege eines neuen Pfades: jeder Punkt, der ein Knoten des Netzes ist, mit den Metern von dort bis zum Ende und den Punkten dorthin.</summary>
    public static List<Anlauf> Ausstiege(IReadOnlyList<(float X, float Z)> pfad, Func<int, int> knotenAn)
    {
        var liste = new List<Anlauf>();
        var rest = 0f;
        for (var k = pfad.Count - 1; k >= 0; k--)
        {
            if (k + 1 < pfad.Count) rest += Abstand(pfad[k], pfad[k + 1]);
            if (knotenAn(k) is var knoten and >= 0) liste.Add(new Anlauf(knoten, rest, Enumerable.Range(k, pfad.Count - k).ToArray()));
        }
        return liste;
    }

    /// <summary>
    /// Welche Punkte eines Pfades bleiben, wenn er nirgends kehrtmacht: Ein Punkt, an dem der Weg auf derselben Linie umkehrt (hin und gleich wieder
    /// zurück, etwa „bis zum Knoten und dann denselben Weg zurück“), fällt weg, ebenso doppelte Punkte (unter 5 cm). Anfang und Ende bleiben immer. Ergebnis:
    /// die Nummern der übrigen Punkte, in Reihenfolge.
    /// </summary>
    public static List<int> OhneUmkehr(IReadOnlyList<(float X, float Z)> punkte)
    {
        var bleiben = Enumerable.Range(0, punkte.Count).ToList();
        var geaendert = true;
        while (geaendert)
        {
            geaendert = false;
            for (var k = 1; k + 1 < bleiben.Count; k++)
            {
                var (a, b, c) = (punkte[bleiben[k - 1]], punkte[bleiben[k]], punkte[bleiben[k + 1]]);
                var (ab, bc) = (Abstand(a, b), Abstand(b, c));
                // doppelt, oder Umkehr: die Richtung dreht um mehr als 150° und der Rückweg liegt auf derselben Linie (höchstens 0,3 m daneben) –
                // dann liegt der Punkt nur „auf dem Hinweg“; eine spitze Gabelung zweier Gassen bleibt (dort ginge die Abkürzung durch die Häuser)
                var doppelt = ab < 0.05f;
                var umkehr = !doppelt && bc >= 0.05f && ((b.X - a.X) * (c.X - b.X) + (b.Z - a.Z) * (c.Z - b.Z)) / (ab * bc) < -0.866f
                             && MathF.Min(Seitab(c, a, b), Seitab(a, b, c)) < 0.3f;
                if (!doppelt && !umkehr) continue;
                bleiben.RemoveAt(k);
                geaendert = true;
                break;
            }
        }
        // am Ende doppelt: den vorletzten weglassen, das Ende bleibt
        if (bleiben.Count > 2 && Abstand(punkte[bleiben[^2]], punkte[bleiben[^1]]) < 0.05f) bleiben.RemoveAt(bleiben.Count - 2);
        return bleiben;
    }

    /// <summary>Wie weit (Meter) der Punkt <paramref name="p"/> neben der Geraden durch <paramref name="a"/> und <paramref name="b"/> liegt.</summary>
    private static float Seitab((float X, float Z) p, (float X, float Z) a, (float X, float Z) b)
    {
        var laenge = Abstand(a, b);
        return laenge < 1e-6f ? Abstand(p, a) : MathF.Abs((b.X - a.X) * (p.Z - a.Z) - (b.Z - a.Z) * (p.X - a.X)) / laenge;
    }

    private static float Abstand((float X, float Z) a, (float X, float Z) b) => MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));

    private static (float X, float Z) Lerp((float X, float Z) a, (float X, float Z) b, float t) => (a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
}
