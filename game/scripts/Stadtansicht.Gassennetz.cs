using Godot;

namespace Reichsgilde;

/// <summary>
/// Das Gassennetz der Darstellung: Knoten (Punkte auf dem Boden) und Kanten (gerade Wegstücke dazwischen).
/// Passanten laufen nur auf diesen Kanten. Die Simulation kennt das Netz nicht – sie legt nur fest, wann
/// jemand losgeht und ankommt; die Darstellung sucht hier den Weg und verteilt die Strecke auf diese Zeit.
///
/// So entsteht das Netz:
///   • <see cref="Weg"/> legt eine Folge von Punkten als Kanten an. Liegt ein Punkt dicht (unter <see cref="Fang"/>)
///     an einem vorhandenen Knoten, wird dieser benutzt. Kreuzt die neue Kante eine alte, entsteht dort ein
///     gemeinsamer Knoten – so sind sich kreuzende Gassen von selbst verbunden.
///   • <see cref="Anschliessen"/> sucht den nächsten Punkt auf dem Netz (z. B. vor einer Haustür) und teilt die
///     Kante dort, falls nötig.
///
/// Der kürzeste Weg (Dijkstra) wird je Knotenpaar einmal berechnet und gemerkt.
/// </summary>
public sealed class Gassennetz
{
    /// <summary>Punkte, die näher als dieser Abstand (Meter) beieinander liegen, gelten als derselbe Knoten.</summary>
    public const float Fang = 0.6f;

    private readonly List<Vector3> _knoten = new();
    private readonly HashSet<(int A, int B)> _kanten = new();

    /// <summary>
    /// Die Fähren (Kartenwerkstatt, Schritt 5c): Kanten über das Wasser zwischen zwei Anlegern. Nur Karren benutzen sie (<see cref="Pfad"/> mit
    /// <c>faehren</c>); Passanten laufen nie darüber. Für die Wahl des Weges zählt eine Fähre wie <see cref="FaehreUmweg"/> Meter Umweg
    /// (wie im Kern, <c>Karrennetz.FaehreUmweg</c>).
    /// </summary>
    private readonly HashSet<(int A, int B)> _faehren = new();

    /// <summary>Für die Wahl des Weges zählt eine Fähre so viele Meter mehr als ihre Länge (wie <c>Karrennetz.FaehreUmweg</c> im Kern).</summary>
    public const float FaehreUmweg = 300f;

    private List<(int Ziel, float Laenge, float Kosten)>[]? _nachbarn, _nachbarnMitFaehren;
    private readonly Dictionary<(int, int, bool), Vector3[]> _pfade = new();

    public IReadOnlyList<Vector3> Knoten => _knoten;
    public IEnumerable<(Vector3 Von, Vector3 Bis)> Kanten => _kanten.Select(k => (_knoten[k.A], _knoten[k.B]));

    /// <summary>Die Fähren als Strecken (Anleger zu Anleger).</summary>
    public IEnumerable<(Vector3 Von, Vector3 Bis)> FaehrKanten => _faehren.Select(k => (_knoten[k.A], _knoten[k.B]));

    /// <summary>Knoten an diesem Punkt (vorhandener in Fangweite oder neuer).</summary>
    public int KnotenBei(Vector3 p)
    {
        p.Y = 0;
        for (var i = 0; i < _knoten.Count; i++)
            if (_knoten[i].DistanceTo(p) < Fang) return i;
        // Liegt der Punkt auf einer vorhandenen Kante (Einmündung), wird sie dort geteilt.
        foreach (var kante in _kanten)
        {
            var q = NaechsterPunkt(p, _knoten[kante.A], _knoten[kante.B]);
            if (q.DistanceTo(p) < Fang) return Teilen(kante, q);
        }
        _knoten.Add(p);
        Geaendert();
        return _knoten.Count - 1;
    }

    /// <summary>Legt die Punkte der Reihe nach als Wegstücke an.</summary>
    public void Weg(params Vector3[] punkte)
    {
        for (var i = 0; i + 1 < punkte.Length; i++)
            KanteMitKreuzungen(KnotenBei(punkte[i]), KnotenBei(punkte[i + 1]));
    }

    /// <summary>Legt eine Fähre zwischen zwei Anlegern an (die Punkte hängen am Netz der Wege; die Kante selbst teilt keine Straße).</summary>
    public void Faehre(Vector3 von, Vector3 bis)
    {
        var (a, b) = (KnotenBei(von), KnotenBei(bis));
        if (a == b) return;
        _faehren.Add(Ordnen(a, b));
        Geaendert();
    }

    /// <summary>
    /// Der Knoten auf dem Netz, der diesem Punkt am nächsten liegt. Liegt der nächste Punkt mitten auf einer
    /// Kante, wird sie dort geteilt. Gibt -1 zurück, wenn das Netz leer ist.
    /// </summary>
    public int Anschliessen(Vector3 p)
    {
        p.Y = 0;
        var beste = (Kante: (-1, -1), Punkt: Vector3.Zero, Abstand: float.MaxValue);
        foreach (var (a, b) in _kanten)
        {
            var q = NaechsterPunkt(p, _knoten[a], _knoten[b]);
            var d = q.DistanceTo(p);
            if (d < beste.Abstand) beste = ((a, b), q, d);
        }
        if (beste.Kante.Item1 < 0) return -1;
        return Teilen(beste.Kante, beste.Punkt);
    }

    /// <summary>Kürzester Weg als Punktfolge (Start und Ziel eingeschlossen). Leer, wenn es keinen gibt. Mit <paramref name="faehren"/> (Karren) auch über Fähren.</summary>
    public Vector3[] Pfad(int von, int nach, bool faehren = false)
    {
        if (von == nach) return new[] { _knoten[von] };
        if (_pfade.TryGetValue((von, nach, faehren), out var gemerkt)) return gemerkt;

        var nachbarn = Nachbarn(faehren);
        var abstand = new float[_knoten.Count];
        var vorgaenger = new int[_knoten.Count];
        Array.Fill(abstand, float.MaxValue);
        Array.Fill(vorgaenger, -1);
        abstand[von] = 0;
        var offen = new PriorityQueue<int, float>();
        offen.Enqueue(von, 0);
        while (offen.TryDequeue(out var k, out var d))
        {
            if (d > abstand[k]) continue; // veralteter Eintrag
            if (k == nach) break;
            foreach (var (ziel, _, kosten) in nachbarn[k])
            {
                var neu = d + kosten;
                if (neu >= abstand[ziel]) continue;
                abstand[ziel] = neu;
                vorgaenger[ziel] = k;
                offen.Enqueue(ziel, neu);
            }
        }

        Vector3[] pfad;
        if (vorgaenger[nach] < 0)
            pfad = Array.Empty<Vector3>();
        else
        {
            var liste = new List<Vector3>();
            for (var k = nach; k >= 0; k = vorgaenger[k]) liste.Add(_knoten[k]);
            liste.Reverse();
            pfad = liste.ToArray();
        }
        _pfade[(von, nach, faehren)] = pfad;
        return pfad;
    }

    /// <summary>Der Knoten genau an diesem Punkt (auf dem Boden, höchstens <paramref name="toleranz"/> Meter daneben) oder −1 – legt keinen neuen an.</summary>
    public int KnotenGenau(Vector3 p, float toleranz = 0.05f)
    {
        for (var i = 0; i < _knoten.Count; i++)
        {
            var (dx, dz) = (_knoten[i].X - p.X, _knoten[i].Z - p.Z);
            if (dx * dx + dz * dz <= toleranz * toleranz) return i;
        }
        return -1;
    }

    /// <summary>
    /// Die Kante, auf der ein Punkt liegt (die nächste, höchstens <paramref name="toleranz"/> Meter daneben), als ihre beiden Knoten – so, wie das Netz
    /// jetzt ist (eine Kante, die später an einer Einmündung geteilt wurde, ist dann das Stück zwischen den Teilungen). Null, wenn keine so nah ist.
    /// </summary>
    public (int A, int B)? KanteBei(Vector3 p, float toleranz = 0.3f)
    {
        p.Y = 0;
        (int, int)? beste = null;
        var abstand = toleranz;
        foreach (var (a, b) in _kanten)
        {
            var d = NaechsterPunkt(p, _knoten[a] with { Y = 0 }, _knoten[b] with { Y = 0 }).DistanceTo(p);
            if (d < abstand) (beste, abstand) = ((a, b), d);
        }
        return beste;
    }

    /// <summary>
    /// Der kürzeste Weg von irgendeinem der <paramref name="anfaenge"/> zu irgendeinem der <paramref name="enden"/>, wobei zu jedem Anfang ein Vorlauf und
    /// zu jedem Ende ein Nachlauf (Meter außerhalb des Netzes) mitzählt: ein Durchgang (Dijkstra mit mehreren Quellen) statt eines je Paar. Ergebnis: die
    /// Punktfolge im Netz und welcher Anfang und welches Ende (Nummern in den Listen) es wurden; null, wenn keiner verbunden ist. Ohne Fähren.
    /// </summary>
    public (Vector3[] Pfad, int Anfang, int Ende)? PfadZwischen(IReadOnlyList<(int Knoten, float Vorlauf)> anfaenge, IReadOnlyList<(int Knoten, float Nachlauf)> enden)
    {
        if (anfaenge.Count == 0 || enden.Count == 0) return null;
        var nachbarn = Nachbarn();
        var abstand = new float[_knoten.Count];
        var vorgaenger = new int[_knoten.Count];
        var quelle = new int[_knoten.Count];
        Array.Fill(abstand, float.MaxValue);
        Array.Fill(vorgaenger, -1);
        var offen = new PriorityQueue<int, float>();
        for (var i = 0; i < anfaenge.Count; i++)
        {
            var (k, vorlauf) = anfaenge[i];
            if (k < 0 || vorlauf >= abstand[k]) continue;
            (abstand[k], quelle[k]) = (vorlauf, i);
            offen.Enqueue(k, vorlauf);
        }
        // Nachlauf je Knoten (der kleinste, falls ein Knoten mehrmals als Ende vorkommt)
        var nachlauf = new Dictionary<int, (float Laenge, int Nummer)>();
        for (var i = 0; i < enden.Count; i++)
            if (enden[i].Knoten >= 0 && (!nachlauf.TryGetValue(enden[i].Knoten, out var alt) || enden[i].Nachlauf < alt.Laenge))
                nachlauf[enden[i].Knoten] = (enden[i].Nachlauf, i);
        var (bestes, bestesEnde) = (float.MaxValue, -1);
        while (offen.TryDequeue(out var k, out var d))
        {
            if (d > abstand[k]) continue;
            if (d >= bestes) break; // weiter weg als der beste Gesamtweg: fertig
            if (nachlauf.TryGetValue(k, out var n) && d + n.Laenge < bestes) (bestes, bestesEnde) = (d + n.Laenge, k);
            foreach (var (ziel, _, kosten) in nachbarn[k])
            {
                var neu = d + kosten;
                if (neu >= abstand[ziel]) continue;
                (abstand[ziel], vorgaenger[ziel], quelle[ziel]) = (neu, k, quelle[k]);
                offen.Enqueue(ziel, neu);
            }
        }
        if (bestesEnde < 0) return null;
        var liste = new List<Vector3>();
        for (var k = bestesEnde; k >= 0; k = vorgaenger[k]) liste.Add(_knoten[k]);
        liste.Reverse();
        return (liste.ToArray(), quelle[bestesEnde], nachlauf[bestesEnde].Nummer);
    }

    /// <summary>Wie viele voneinander getrennte Teile das Netz hat (1 = alles hängt zusammen); Fähren verbinden (für Karren).</summary>
    public int Teile()
    {
        var nachbarn = Nachbarn(true);
        var teil = new int[_knoten.Count];
        Array.Fill(teil, -1);
        var anzahl = 0;
        for (var start = 0; start < _knoten.Count; start++)
        {
            if (teil[start] >= 0) continue;
            var stapel = new Stack<int>();
            stapel.Push(start);
            teil[start] = anzahl;
            while (stapel.TryPop(out var k))
                foreach (var (ziel, _, _) in nachbarn[k])
                    if (teil[ziel] < 0) { teil[ziel] = anzahl; stapel.Push(ziel); }
            anzahl++;
        }
        return anzahl;
    }

    /// <summary>Gesamtlänge eines Pfads in Metern.</summary>
    public static float Laenge(Vector3[] pfad)
    {
        var summe = 0f;
        for (var i = 0; i + 1 < pfad.Length; i++) summe += pfad[i].DistanceTo(pfad[i + 1]);
        return summe;
    }

    /// <summary>Der Punkt bei diesem Anteil (0 = Anfang, 1 = Ende) der Strecke.</summary>
    public static Vector3 PunktAuf(Vector3[] pfad, float anteil) => PunktAuf(pfad, anteil, pfad.Length == 0 ? 0 : Laenge(pfad));

    /// <summary>Wie <see cref="PunktAuf(Vector3[], float)"/>, mit der schon bekannten Länge des Pfades (spart die Rechnung, wenn mehrere Punkte je Bild gebraucht werden).</summary>
    public static Vector3 PunktAuf(Vector3[] pfad, float anteil, float laenge)
    {
        if (pfad.Length == 0) return Vector3.Zero;
        var rest = Mathf.Clamp(anteil, 0, 1) * laenge;
        for (var i = 0; i + 1 < pfad.Length; i++)
        {
            var stueck = pfad[i].DistanceTo(pfad[i + 1]);
            if (rest <= stueck) return stueck > 0 ? pfad[i].Lerp(pfad[i + 1], rest / stueck) : pfad[i];
            rest -= stueck;
        }
        return pfad[^1];
    }

    // ── intern ────────────────────────────────────────────────────────────

    private void KanteMitKreuzungen(int a, int b)
    {
        if (a == b) return;
        // Kreuzungen mit vorhandenen Kanten suchen und dort Knoten einfügen (der Reihe nach entlang a → b).
        var schnitte = new List<(float T, int Knoten)>();
        foreach (var (c, d) in _kanten.ToList())
        {
            if (c == a || c == b || d == a || d == b) continue;
            if (!Schnitt(_knoten[a], _knoten[b], _knoten[c], _knoten[d], out var t, out var punkt)) continue;
            schnitte.Add((t, Teilen((c, d), punkt)));
        }
        // Knoten, die schon auf der neuen Kante liegen (z. B. eine Einmündung), werden mit durchlaufen.
        for (var k = 0; k < _knoten.Count; k++)
        {
            if (k == a || k == b) continue;
            var ab = _knoten[b] - _knoten[a];
            var t = (_knoten[k] - _knoten[a]).Dot(ab) / ab.LengthSquared();
            if (t is <= 0 or >= 1) continue;
            if ((_knoten[a] + ab * t).DistanceTo(_knoten[k]) < Fang && !schnitte.Any(x => x.Knoten == k))
                schnitte.Add((t, k));
        }
        var folge = new List<int> { a };
        folge.AddRange(schnitte.OrderBy(s => s.T).Select(s => s.Knoten));
        folge.Add(b);
        for (var i = 0; i + 1 < folge.Count; i++)
            if (folge[i] != folge[i + 1])
                _kanten.Add(Ordnen(folge[i], folge[i + 1]));
        Geaendert();
    }

    /// <summary>Teilt eine Kante am Punkt p und gibt den Knoten dort zurück (ein Endpunkt, wenn p dicht daran liegt).</summary>
    private int Teilen((int A, int B) kante, Vector3 p)
    {
        if (_knoten[kante.A].DistanceTo(p) < Fang) return kante.A;
        if (_knoten[kante.B].DistanceTo(p) < Fang) return kante.B;
        _knoten.Add(p);
        var neu = _knoten.Count - 1;
        _kanten.Remove(Ordnen(kante.A, kante.B));
        _kanten.Add(Ordnen(kante.A, neu));
        _kanten.Add(Ordnen(neu, kante.B));
        Geaendert();
        return neu;
    }

    private void Geaendert()
    {
        _nachbarn = _nachbarnMitFaehren = null;
        _pfade.Clear();
    }

    private List<(int Ziel, float Laenge, float Kosten)>[] Nachbarn(bool faehren = false)
    {
        if (faehren ? _nachbarnMitFaehren is not null : _nachbarn is not null) return faehren ? _nachbarnMitFaehren! : _nachbarn!;
        var liste = new List<(int, float, float)>[_knoten.Count];
        for (var i = 0; i < _knoten.Count; i++) liste[i] = new();
        foreach (var (a, b) in _kanten)
        {
            var laenge = _knoten[a].DistanceTo(_knoten[b]);
            liste[a].Add((b, laenge, laenge));
            liste[b].Add((a, laenge, laenge));
        }
        if (faehren)
            foreach (var (a, b) in _faehren)
            {
                var laenge = _knoten[a].DistanceTo(_knoten[b]);
                liste[a].Add((b, laenge, laenge + FaehreUmweg));
                liste[b].Add((a, laenge, laenge + FaehreUmweg));
            }
        if (faehren) _nachbarnMitFaehren = liste; else _nachbarn = liste;
        return liste;
    }

    private static (int, int) Ordnen(int a, int b) => a < b ? (a, b) : (b, a);

    private static Vector3 NaechsterPunkt(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var t = ab.LengthSquared() > 0 ? Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0, 1) : 0;
        return a + ab * t;
    }

    /// <summary>Schneiden sich die Strecken a–b und c–d (auf dem Boden, also in X und Z)? t = Anteil auf a–b.</summary>
    private static bool Schnitt(Vector3 a, Vector3 b, Vector3 c, Vector3 d, out float t, out Vector3 punkt)
    {
        t = 0;
        punkt = Vector3.Zero;
        var r = new Vector2(b.X - a.X, b.Z - a.Z);
        var s = new Vector2(d.X - c.X, d.Z - c.Z);
        var nenner = r.X * s.Y - r.Y * s.X;
        if (Mathf.Abs(nenner) < 1e-6f) return false; // parallel
        var ca = new Vector2(c.X - a.X, c.Z - a.Z);
        t = (ca.X * s.Y - ca.Y * s.X) / nenner;
        var u = (ca.X * r.Y - ca.Y * r.X) / nenner;
        const float rand = 1e-3f;
        if (t <= rand || t >= 1 - rand || u <= rand || u >= 1 - rand) return false;
        punkt = a + (b - a) * t;
        return true;
    }
}
