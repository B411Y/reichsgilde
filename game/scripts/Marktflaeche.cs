namespace Reichsgilde;

/// <summary>
/// Die freie Fläche eines Marktplatzes für die Passanten (Nutzer, 1.11.2026: Markt v3): das Pflaster aus der Karte (ein Quadrat, auch gedreht) ohne
/// Stände, Brunnen, Bänke, Kreuz und Fässer. Nur Rechnung ohne Godot, damit ein Test sie mitübersetzen kann (<c>MarktflaecheTests</c>); die Darstellung
/// (Stadtansicht.Marktbummel.cs) trägt die Hindernisse ein und fragt Wege und Platz ab.
///
/// So geht es:
///   • Hindernisse sind Kreise und gedrehte Rechtecke in Metern der Stadt (x, z). <see cref="Abstand"/> ist der genaue Abstand eines Punktes zum nächsten
///     Hindernis (innen negativ).
///   • Ein Raster mit Zellen von <see cref="Zelle"/> Metern über das Quadrat merkt sich diesen Abstand je Zellmitte (einmal gerechnet). Darauf sucht
///     <see cref="Pfad"/> mit A* (acht Nachbarn) einen Weg, auf dem überall mindestens <c>frei</c> Meter bis zum nächsten Hindernis bleiben, und zieht
///     ihn danach straff (Sichtlinie: Ecken nur dort, wo ein Hindernis im Weg steht).
///   • <see cref="Raum"/> sagt, wie weit man von einem Punkt in eine Richtung gehen kann, bis ein Hindernis zu nahe kommt – für das Ausweichen auf der
///     Fläche (statt der Wegbreite in den Gassen).
/// Kein Zufall und keine Uhr: Gleiche Eingaben geben den gleichen Weg.
/// </summary>
public sealed class Marktflaeche
{
    private readonly float _ox, _oz, _ax, _az, _halb;
    private readonly int _n;
    private readonly List<(float X, float Z, float R)> _kreise = new();
    private readonly List<Rechteck> _rechtecke = new();
    private float[] _abstand = Array.Empty<float>();

    /// <summary>Ein gedrehtes Rechteck: Mitte, Richtung der ersten Achse (Länge 1), halbe Längen längs und quer.</summary>
    public readonly record struct Rechteck(float X, float Z, float Ux, float Uz, float HalbU, float HalbV)
    {
        /// <summary>Abstand eines Punktes zum Rechteck (Meter, innen negativ).</summary>
        public float Abstand(float x, float z)
        {
            var (dx, dz) = (x - X, z - Z);
            var (u, v) = (MathF.Abs(dx * Ux + dz * Uz) - HalbU, MathF.Abs(-dx * Uz + dz * Ux) - HalbV);
            var aussen = MathF.Sqrt(MathF.Max(u, 0) * MathF.Max(u, 0) + MathF.Max(v, 0) * MathF.Max(v, 0));
            return aussen + MathF.Min(MathF.Max(u, v), 0);
        }
    }

    /// <summary>Kantenlänge einer Rasterzelle (Meter).</summary>
    public float Zelle { get; }

    /// <summary>Die Rechtecke der Stände (für die Messung „in einem Stand“; <see cref="Stand"/> trägt sie ein).</summary>
    public List<Rechteck> Staende { get; } = new();

    /// <summary>
    /// Eine Fläche um die Mitte (<paramref name="mitteX"/>, <paramref name="mitteZ"/>), deren lokale x-Achse in Richtung (<paramref name="achseX"/>,
    /// <paramref name="achseZ"/>) zeigt (Länge 1), mit der halben Seitenlänge <paramref name="halb"/>.
    /// </summary>
    public Marktflaeche(float mitteX, float mitteZ, float achseX, float achseZ, float halb, float zelle = 0.25f)
    {
        var l = MathF.Sqrt(achseX * achseX + achseZ * achseZ);
        (_ox, _oz, _ax, _az, _halb, Zelle) = (mitteX, mitteZ, l > 0 ? achseX / l : 1, l > 0 ? achseZ / l : 0, MathF.Max(halb, 0.5f), zelle);
        _n = Math.Max(1, (int)MathF.Ceiling(2 * _halb / zelle));
    }

    public float Halb => _halb;

    public void Kreis(float x, float z, float radius) { _kreise.Add((x, z, radius)); _abstand = Array.Empty<float>(); _gemerkt.Clear(); }

    public void Rechteckig(Rechteck r) { _rechtecke.Add(r); _abstand = Array.Empty<float>(); _gemerkt.Clear(); }

    /// <summary>Ein Stand: Hindernis und für die Messung gemerkt.</summary>
    public void Stand(Rechteck r) { Rechteckig(r); Staende.Add(r); }

    /// <summary>Wie viele Hindernisse eingetragen sind.</summary>
    public int Hindernisse => _kreise.Count + _rechtecke.Count;

    /// <summary>Genauer Abstand (Meter) eines Punktes zum nächsten Hindernis, innen negativ; ohne Hindernisse sehr groß.</summary>
    public float Abstand(float x, float z)
    {
        var d = 1e6f;
        foreach (var (kx, kz, r) in _kreise)
        {
            var (dx, dz) = (x - kx, z - kz);
            d = MathF.Min(d, MathF.Sqrt(dx * dx + dz * dz) - r);
        }
        foreach (var r in _rechtecke) d = MathF.Min(d, r.Abstand(x, z));
        return d;
    }

    /// <summary>Lokale Koordinaten (Meter ab der Mitte, längs der Achsen des Platzes).</summary>
    public (float U, float V) Lokal(float x, float z)
    {
        var (dx, dz) = (x - _ox, z - _oz);
        return (dx * _ax + dz * _az, -dx * _az + dz * _ax);
    }

    public (float X, float Z) Welt(float u, float v) => (_ox + u * _ax - v * _az, _oz + u * _az + v * _ax);

    /// <summary>Liegt der Punkt auf dem Pflaster (mit <paramref name="rand"/> Metern Abstand zur Kante)?</summary>
    public bool Auf(float x, float z, float rand = 0)
    {
        var (u, v) = Lokal(x, z);
        return MathF.Abs(u) <= _halb - rand && MathF.Abs(v) <= _halb - rand;
    }

    /// <summary>Wie weit (Meter) der Punkt innen von der Kante des Pflasters ist (außen negativ).</summary>
    public float Innen(float x, float z)
    {
        var (u, v) = Lokal(x, z);
        return _halb - MathF.Max(MathF.Abs(u), MathF.Abs(v));
    }

    /// <summary>
    /// So viel (Meter) zählt die Kante des Pflasters weniger als ein Hindernis: Wege dürfen bis auf <c>frei − Kantenzugabe</c> an sie heran (dort münden
    /// Gassen, aber dahinter stehen oft Häuser), Anfang und Ende an der Kante bleiben möglich.
    /// </summary>
    public const float Kantenzugabe = 0.6f;

    /// <summary>Wie frei ein Punkt für einen Weg ist: Abstand zum nächsten Hindernis, nahe der Kante weniger (<see cref="Kantenzugabe"/>).</summary>
    public float Frei(float x, float z) => MathF.Min(Abstand(x, z), Innen(x, z) + Kantenzugabe);

    // ── Raster ──

    private void Bereit()
    {
        if (_abstand.Length == _n * _n) return;
        _abstand = new float[_n * _n];
        for (var j = 0; j < _n; j++)
            for (var i = 0; i < _n; i++)
            {
                var (x, z) = Mitte(i, j);
                _abstand[j * _n + i] = Frei(x, z);
            }
    }

    private (float X, float Z) Mitte(int i, int j) => Welt(-_halb + (i + 0.5f) * Zelle, -_halb + (j + 0.5f) * Zelle);

    private (int I, int J) ZelleBei(float x, float z)
    {
        var (u, v) = Lokal(x, z);
        return (Math.Clamp((int)MathF.Floor((u + _halb) / Zelle), 0, _n - 1), Math.Clamp((int)MathF.Floor((v + _halb) / Zelle), 0, _n - 1));
    }

    /// <summary>Abstand zum nächsten Hindernis aus dem Raster (Mitte der Zelle, in der der Punkt liegt); außerhalb des Pflasters −1.</summary>
    public float Rasterabstand(float x, float z)
    {
        Bereit();
        if (!Auf(x, z, -0.01f)) return -1;
        var (i, j) = ZelleBei(x, z);
        return _abstand[j * _n + i];
    }

    /// <summary>
    /// Steht nichts auf der geraden Linie von a nach b, das näher als <paramref name="frei"/> Meter heranreicht (und bleibt sie auf dem Pflaster)?
    /// Abgetastet alle halbe Zelle; nahe an einem Ende, das selbst dichter an einem Hindernis liegt (ein Platz am Stand), genügt dessen Abstand.
    /// </summary>
    public bool Sichtlinie((float X, float Z) a, (float X, float Z) b, float frei)
    {
        var (dx, dz) = (b.X - a.X, b.Z - a.Z);
        var laenge = MathF.Sqrt(dx * dx + dz * dz);
        if (laenge < 1e-5f) return true;
        // Stehende: nicht mitten durch (außer jemand steht am Anfang oder Ziel selbst – der Nachbar am Stand, der Partner im Gespräch); einmal je Person
        // genau gerechnet (nächster Punkt der Strecke), nicht je Abtastpunkt
        if (_personen.Count > 0 && frei >= PersonFreiAb && !_einfach)
            foreach (var (px, pz) in _personen)
            {
                var (ax, az, bx, bz) = (px - a.X, pz - a.Z, px - b.X, pz - b.Z);
                if (ax * ax + az * az < PersonAmEnde * PersonAmEnde || bx * bx + bz * bz < PersonAmEnde * PersonAmEnde) continue;
                var t = Math.Clamp((ax * dx + az * dz) / (laenge * laenge), 0, 1);
                var (ex, ez) = (ax - dx * t, az - dz * t);
                if (ex * ex + ez * ez < PersonAbstand * PersonAbstand) return false;
            }
        var (startFrei, zielFrei) = (Frei(a.X, a.Z), Frei(b.X, b.Z));
        // In Sprüngen so groß wie die Luft über dem Nötigen (der Abstand ändert sich je Meter höchstens um einen Meter, so wird nichts übersprungen),
        // mindestens 2 cm
        for (var s = 0f; ; )
        {
            var t = s / laenge;
            var (x, z) = (a.X + dx * t, a.Z + dz * t);
            if (!Auf(x, z, -0.05f)) return false;
            // Nahe an einem Ende, das selbst dicht am Hindernis liegt (ein Platz vor dem Stand), reicht der Abstand dieses Endes (sonst käme man nie
            // dorthin); näher als das Ende selbst kommt der Weg dem Hindernis nie
            var noetig = frei;
            if (s < frei + Zelle) noetig = MathF.Min(noetig, startFrei - 0.05f);
            if (laenge - s < frei + Zelle) noetig = MathF.Min(noetig, zielFrei - 0.05f);
            var luft = Frei(x, z) - noetig;
            if (luft < 0) return false;

            if (s >= laenge) return true;
            // die Lockerung an den Enden gilt nur dort: nicht über ihre Grenze hinwegspringen
            // (mindestens 8 cm: Wege an der Grenze des Abstands entlang würden sonst in winzigen Schritten abgetastet; was dazwischen fehlt, sind Millimeter)
            var sprung = MathF.Max(0.08f, MathF.Min(luft, frei - noetig > 0 ? 0.1f : luft));
            s = MathF.Min(laenge, s + sprung);
        }
    }

    /// <summary>
    /// Wie weit (Meter, höchstens <paramref name="hoechstens"/>) man vom Punkt in Richtung (<paramref name="rx"/>, <paramref name="rz"/>, Länge 1) gehen
    /// kann, bis ein Hindernis näher als <paramref name="frei"/> kommt oder die Kante des Pflasters (mit <see cref="Kantenzugabe"/>) – in Sprüngen aus dem genauen Abstand.
    /// Steht der Punkt schon zu nah, 0.
    /// </summary>
    public float Raum(float x, float z, float rx, float rz, float frei, float hoechstens, bool mitKante = true)
    {
        var weit = 0f;
        // in Sprüngen so groß wie der freie Abstand (genau, ohne etwas zu überspringen), am Ende fein
        for (var k = 0; k < 64 && weit < hoechstens; k++)
        {
            var (px, pz) = (x + rx * weit, z + rz * weit);
            var luft = (mitKante ? Frei(px, pz) : Abstand(px, pz)) - frei;
            if (luft <= 0.005f) return k == 0 ? 0 : weit;
            weit = MathF.Min(hoechstens, weit + MathF.Max(luft, 0.05f));
        }
        return MathF.Min(weit, hoechstens);
    }

    /// <summary>
    /// Wie weit eine Figur an der Stelle (<paramref name="x"/>, <paramref name="z"/>) der Mittellinie ihres Weges nach links (≤ 0) und rechts (≥ 0) seitlich
    /// stehen darf, ohne einem Hindernis näher als <paramref name="frei"/> (Körper und Luft) zu kommen, höchstens <paramref name="hoechstens"/> je Seite.
    /// Die Darstellung begrenzt damit auf dem Platz die ganze seitliche Lage – Spur, Schwanken und Ausweichen; vor dem Platz (in der Gasse davor) ohne die
    /// Kante (<paramref name="mitKante"/> = false), dort zählen nur die Hindernisse.
    /// </summary>
    public (float Links, float Rechts) Seitlich(float x, float z, float rx, float rz, float frei, float hoechstens, bool mitKante = true) =>
        (-Raum(x, z, -rx, -rz, frei, hoechstens, mitKante), Raum(x, z, rx, rz, frei, hoechstens, mitKante));

    /// <summary>
    /// Mehr Zellen fasst eine Suche nicht an (etwa ein Drittel des Rasters eines 24-m-Platzes): Zur vollen Stunde soll keine einzelne Suche ein Bild
    /// sprengen; ohne Ergebnis nimmt die Darstellung einen einfacheren Weg.
    /// </summary>
    public const int HoechstensBesucht = 4000;

    /// <summary>Gewicht der Schätzung in A*, solange Belegung oder Stehende die Zellen teurer machen.</summary>
    public const float SchaetzungGewicht = 2f;

    private bool _einfach;

    /// <summary>
    /// Der einfache Rückfall, wenn <see cref="Gefaechert"/> zu teuer wurde: nur um die festen Hindernisse (ohne Belegung und Stehende), Schätzung leicht gewichtet,
    /// ohne Obergrenze – auf dem freien Raster fast geradeaus und schnell. Nie durch einen Stand.
    /// </summary>
    public List<(float X, float Z)>? PfadEinfach((float X, float Z) von, (float X, float Z) nach, float frei)
    {
        _einfach = true;
        try { return Pfad(von, nach, frei); }
        finally { _einfach = false; }
    }

    /// <summary>Wie viele Zellen die letzte Suche angefasst hat (zum Prüfen der Kosten).</summary>
    public int Besucht { get; private set; }

    /// <summary>
    /// Ein Weg über das Pflaster von <paramref name="von"/> nach <paramref name="nach"/>, auf dem überall <paramref name="frei"/> Meter bis zu jedem
    /// Hindernis bleiben (A* auf dem Raster, dann straff gezogen). Anfang und Ende sind genau die beiden Punkte, auch wenn sie selbst näher an einem
    /// Hindernis liegen (ein Platz vor dem Stand, auf der Bank): Dann beginnt die Suche in der nächsten freien Zelle. Null, wenn keiner geht.
    /// </summary>
    public List<(float X, float Z)>? Pfad((float X, float Z) von, (float X, float Z) nach, float frei)
    {
        Bereit();
        if (Sichtlinie(von, nach, frei)) return new() { von, nach };
        var start = FreieZelle(von, frei);
        var ziel = FreieZelle(nach, frei);
        if (start < 0 || ziel < 0) return null;
        var zellen = Suche(start, ziel, frei);
        if (zellen is null) return null;
        var roh = new List<(float X, float Z)> { von, Mitte(start % _n, start / _n) };
        foreach (var k in zellen) roh.Add(Mitte(k % _n, k / _n));
        roh.Add(nach);
        return Straff(roh, frei);
    }

    /// <summary>Die Zelle des Punktes, wenn sie frei ist, sonst die nächste freie (Ringe nach außen, höchstens 3 m). −1, wenn keine.</summary>
    private int FreieZelle((float X, float Z) p, float frei)
    {
        var (i0, j0) = ZelleBei(p.X, p.Z);
        if (_abstand[j0 * _n + i0] >= frei) return j0 * _n + i0;
        // die nächste freie Zelle, zu der man vom Punkt aus gerade hinkommt (nicht hinter Bank oder Stand); ohne eine solche die nächste überhaupt
        var (beste, besterAbstand, notfalls, notAbstand) = (-1, float.MaxValue, -1, float.MaxValue);
        var ringe = (int)MathF.Ceiling(3f / Zelle);
        var (ersterRing, pruefungen) = (-1, 0); // höchstens zwei Ringe über den ersten freien hinaus und 40 Sichtlinien (sonst kostet ein Ende mitten im Gedränge ein Bild)
        for (var r = 1; r <= ringe && beste < 0 && (ersterRing < 0 || r <= ersterRing + 2) && pruefungen < 40; r++)
            for (var dj = -r; dj <= r; dj++)
                for (var di = -r; di <= r; di++)
                {
                    if (Math.Max(Math.Abs(di), Math.Abs(dj)) != r) continue;
                    var (i, j) = (i0 + di, j0 + dj);
                    if (i < 0 || j < 0 || i >= _n || j >= _n || _abstand[j * _n + i] < frei) continue;
                    var (x, z) = Mitte(i, j);
                    var d = (x - p.X) * (x - p.X) + (z - p.Z) * (z - p.Z);
                    if (d < notAbstand) (notfalls, notAbstand) = (j * _n + i, d);
                    if (ersterRing < 0) ersterRing = r;
                    if (d >= besterAbstand || pruefungen >= 40) continue;
                    pruefungen++;
                    if (Sichtlinie(p, (x, z), frei)) (beste, besterAbstand) = (j * _n + i, d);
                }
        return beste >= 0 ? beste : notfalls;
    }

    private static readonly (int Di, int Dj, float Kosten)[] Nachbarn =
    {
        (1, 0, 1), (-1, 0, 1), (0, 1, 1), (0, -1, 1), (1, 1, 1.41421356f), (1, -1, 1.41421356f), (-1, 1, 1.41421356f), (-1, -1, 1.41421356f),
    };

    private float[] _kosten = Array.Empty<float>();
    private int[] _her = Array.Empty<int>();
    private int[] _stempel = Array.Empty<int>();
    private int _lauf;
    private readonly PriorityQueue<int, float> _offen = new();

    /// <summary>A* von Zelle zu Zelle (acht Nachbarn, Schrägen nur, wenn beide Ecken frei sind); die Zellen vom ersten nach dem Start bis zum Ziel.</summary>
    private List<int>? Suche(int start, int ziel, float frei)
    {
        var anzahl = _n * _n;
        if (_kosten.Length != anzahl) (_kosten, _her, _stempel) = (new float[anzahl], new int[anzahl], new int[anzahl]);
        _lauf++;
        _offen.Clear();
        var (zi, zj) = (ziel % _n, ziel / _n);
        // Mit Belegung und Stehenden (Zellen teurer als ihr Weg) ist die Schätzung zu vorsichtig, und A* fasst fast das ganze Raster an: dann gewichtet
        // (doppelt), so geht die Suche zielstrebig – der Weg ist höchstens ein wenig länger als der beste, das Straffziehen glättet ihn ohnehin
        var gewicht = _einfach ? 1.5f : _belegt ? SchaetzungGewicht : 1f;
        float Schaetzung(int k)
        {
            var (di, dj) = (Math.Abs(k % _n - zi), Math.Abs(k / _n - zj));
            return gewicht * (Math.Max(di, dj) + 0.41421356f * Math.Min(di, dj));
        }
        (_kosten[start], _her[start], _stempel[start]) = (0, -1, _lauf);
        _offen.Enqueue(start, Schaetzung(start));
        Besucht = 0;
        while (_offen.TryDequeue(out var k, out var prio))
        {
            if (k == ziel) break;
            if (Besucht >= HoechstensBesucht && !_einfach) return null; // zu teuer für ein Bild (die Darstellung nimmt dann einen einfacheren Weg)
            if (prio > _kosten[k] + Schaetzung(k) + 1e-3f) continue; // veralteter Eintrag
            Besucht++;
            var (i, j) = (k % _n, k / _n);
            foreach (var (di, dj, kosten) in Nachbarn)
            {
                var (ni, nj) = (i + di, j + dj);
                if (ni < 0 || nj < 0 || ni >= _n || nj >= _n) continue;
                var nk = nj * _n + ni;
                if (_abstand[nk] < frei) continue;
                if (di != 0 && dj != 0 && (_abstand[j * _n + ni] < frei || _abstand[nj * _n + i] < frei)) continue; // nicht über Eck durch ein Hindernis
                var neu = _kosten[k] + kosten * (_einfach ? 1 : 1 + Zusatz(nk)); // belegte Streifen und Stehende machen die Zelle teurer (nie billiger: die Schätzung bleibt richtig)
                if (_stempel[nk] == _lauf && neu >= _kosten[nk]) continue;
                (_kosten[nk], _her[nk], _stempel[nk]) = (neu, k, _lauf);
                _offen.Enqueue(nk, neu + Schaetzung(nk));
            }
        }
        if (_stempel[ziel] != _lauf) return null;
        var weg = new List<int>();
        for (var k = ziel; k != start; k = _her[k]) weg.Add(k);
        weg.Reverse();
        return weg;
    }

    /// <summary>Straff ziehen: von jedem Eckpunkt aus so weit wie möglich geradeaus (Sichtlinie), Ecken nur, wo es sonst nicht geht.</summary>
    private List<(float X, float Z)> Straff(List<(float X, float Z)> roh, float frei)
    {
        // Vom Eckpunkt aus der fernste sichtbare Punkt: in Halbierungsschritten gesucht (Sichtbarkeit fällt längs eines A*-Weges fast immer nur einmal ab),
        // so braucht jeder Eckpunkt nur wenige Sichtlinien statt einer je Rasterpunkt
        var aus = new List<(float X, float Z)> { roh[0] };
        var anker = 0;
        while (anker < roh.Count - 1)
        {
            var (sichtbar, unsichtbar) = (anker + 1, roh.Count);
            if (Sichtlinie(roh[anker], roh[^1], frei)) sichtbar = roh.Count - 1;
            else
                while (unsichtbar - sichtbar > 1)
                {
                    var mitte = (sichtbar + unsichtbar) / 2;
                    if (Sichtlinie(roh[anker], roh[mitte], frei)) sichtbar = mitte; else unsichtbar = mitte;
                }
            anker = sichtbar;
            aus.Add(roh[anker]);
        }
        return aus;
    }

    /// <summary>
    /// Ein Weg wie <see cref="Pfad"/>, aber über einen Zwischenpunkt neben der Geraden: um <paramref name="seite"/> (−1 bis 1) mal einen Anteil der Strecke
    /// (höchstens <paramref name="hoechstens"/> Meter) seitlich der Mitte. So gehen zwei Personen mit demselben Ziel nicht auf derselben Linie, die Leute
    /// fächern sich auf dem Platz auf. Liegt der Zwischenpunkt zu nah an einem Hindernis, rückt er zur Geraden; ganz ohne geht es gerade.
    /// </summary>
    public List<(float X, float Z)>? Gefaechert((float X, float Z) von, (float X, float Z) nach, float frei, float seite, float anteil = 0.3f, float hoechstens = 3.5f)
    {
        Bereit();
        var (dx, dz) = (nach.X - von.X, nach.Z - von.Z);
        var laenge = MathF.Sqrt(dx * dx + dz * dz);
        var (rx, rz) = laenge > 0 ? (-dz / laenge, dx / laenge) : (0f, 0f);
        if (laenge >= 3f && _belegt) seite = FreiesteSeite(von, nach, frei, seite, MathF.Min(laenge * anteil, hoechstens), rx, rz);
        if (laenge < 3f || MathF.Abs(seite) < 0.05f) return Pfad(von, nach, frei);
        var quer = MathF.Min(laenge * anteil, hoechstens) * seite;
        // ein Zwischenpunkt (näher an der Geraden, wenn der weite in einem Hindernis läge); gelingt der Weg über ihn nicht, gleich der direkte – jede weitere
        // Suche kostete zur vollen Stunde Zeit im selben Bild
        foreach (var f in new[] { 1f, 0.6f, 0.3f })
        {
            var mitte = (von.X + dx * 0.5f + rx * quer * f, von.Z + dz * 0.5f + rz * quer * f);
            if (!Auf(mitte.Item1, mitte.Item2, frei) || Abstand(mitte.Item1, mitte.Item2) < frei + 0.2f) continue;
            var erst = Pfad(von, mitte, frei);
            var dann = erst is null ? null : Pfad(mitte, nach, frei);
            if (erst is null || dann is null) break;
            erst.AddRange(dann.Skip(1));
            return erst;
        }
        return Pfad(von, nach, frei);
    }

    // ── Belegung: Wer schon geht und wer steht (Markt v3b, Teamleiter 1.11.2026) ──

    /// <summary>So viel kostet ein Rasterschritt mehr, je Weg, der gerade über die Zelle führt (Mitte des Streifens; zum Rand des Streifens weniger).</summary>
    public const float StreifenKosten = 1.2f;
    /// <summary>Halbe Breite (Meter) des Streifens, den ein Weg belegt.</summary>
    public const float StreifenHalb = 0.7f;
    /// <summary>Nach so vielen Sekunden ist die Belegung eines Weges nur noch halb so viel wert (die Leute sind dann ein gutes Stück weiter).</summary>
    public const float StreifenHalbwert = 15f;
    /// <summary>Um Stehende: so viel teurer (dicht bei der Person, bis <see cref="PersonUmkreis"/> auf null), damit Wege nicht durch Gruppen und Wartende führen.</summary>
    public const float PersonKosten = 4f, PersonUmkreis = 1.1f;
    /// <summary>Eine gerade Linie darf an Stehenden nicht näher als so viel vorbeiführen; steht jemand so nah am Anfang oder Ziel, zählt er nicht.</summary>
    public const float PersonAbstand = 0.6f, PersonAmEnde = 1.3f;
    /// <summary>Nur Wege mit so viel Abstand achten auf Stehende (der knappe Rückfallweg nicht).</summary>
    public const float PersonFreiAb = 1f;

    private float[] _streifen = Array.Empty<float>(), _personKosten = Array.Empty<float>();
    private readonly List<(float X, float Z)> _personen = new();
    private bool _belegt;
    private float _alterung;

    private float Zusatz(int k) => (_streifen.Length > k ? _streifen[k] * StreifenKosten : 0) + (_personKosten.Length > k ? _personKosten[k] : 0);

    /// <summary>Ein Weg wird gegangen: Er belegt seinen Streifen (fällt mit der Zeit ab, <see cref="Altern"/>).</summary>
    public void Belegen(IReadOnlyList<(float X, float Z)> weg)
    {
        if (weg.Count < 2) return;
        if (_streifen.Length != _n * _n) _streifen = new float[_n * _n];
        var r = (int)MathF.Ceiling(StreifenHalb / Zelle);
        var zuletzt = -1;
        for (var i = 1; i < weg.Count; i++)
        {
            var (dx, dz) = (weg[i].X - weg[i - 1].X, weg[i].Z - weg[i - 1].Z);
            var l = MathF.Sqrt(dx * dx + dz * dz);
            for (var t = 0f; t <= l; t += Zelle * 2)
            {
                var (x, z) = (weg[i - 1].X + dx * t / MathF.Max(l, 1e-5f), weg[i - 1].Z + dz * t / MathF.Max(l, 1e-5f));
                var (ci, cj) = ZelleBei(x, z);
                if (cj * _n + ci == zuletzt) continue;
                zuletzt = cj * _n + ci;
                for (var dj = -r; dj <= r; dj++)
                    for (var di = -r; di <= r; di++)
                    {
                        var (ii, jj) = (ci + di, cj + dj);
                        if (ii < 0 || jj < 0 || ii >= _n || jj >= _n) continue;
                        var d = MathF.Sqrt(di * di + dj * dj) * Zelle;
                        if (d > StreifenHalb) continue;
                        // alle 0,5 m ein Abtastpunkt: in der Mitte des Streifens kommt so etwa 1 je Weg zusammen
                        _streifen[jj * _n + ii] += 0.5f * (1 - d / StreifenHalb);
                    }
            }
        }
        _belegt = true;
    }

    /// <summary>Die Belegung verblasst (Halbwertszeit <see cref="StreifenHalbwert"/> Sekunden); gerechnet höchstens zweimal je Sekunde.</summary>
    public void Altern(float sekunden)
    {
        _alterung += sekunden;
        if (_alterung < 0.5f || !_belegt) return;
        var faktor = MathF.Pow(0.5f, _alterung / StreifenHalbwert);
        _alterung = 0;
        var noch = false;
        for (var k = 0; k < _streifen.Length; k++)
        {
            var w = _streifen[k] * faktor;
            _streifen[k] = w < 0.02f ? 0 : w;
            noch |= w >= 0.02f;
        }
        _belegt = noch || _personen.Count > 0;
    }

    /// <summary>Wer gerade auf dem Platz steht (oder sitzt): Wege machen einen Bogen um sie. Gemerkte Wege gelten danach nicht mehr.</summary>
    public void Personen(IEnumerable<(float X, float Z)> stehende)
    {
        _personen.Clear();
        _personen.AddRange(stehende);
        if (_personKosten.Length != _n * _n) _personKosten = new float[_n * _n];
        else Array.Clear(_personKosten);
        var r = (int)MathF.Ceiling(PersonUmkreis / Zelle);
        foreach (var (x, z) in _personen)
        {
            var (ci, cj) = ZelleBei(x, z);
            for (var dj = -r; dj <= r; dj++)
                for (var di = -r; di <= r; di++)
                {
                    var (ii, jj) = (ci + di, cj + dj);
                    if (ii < 0 || jj < 0 || ii >= _n || jj >= _n) continue;
                    var (mx, mz) = Mitte(ii, jj);
                    var d = MathF.Sqrt((mx - x) * (mx - x) + (mz - z) * (mz - z));
                    if (d < PersonUmkreis) _personKosten[jj * _n + ii] += PersonKosten * (1 - d / PersonUmkreis);
                }
        }
        if (_personen.Count > 0) _belegt = true;
        _gemerkt.Clear();
    }

    /// <summary>Wie stark der Streifen (Belegung und Stehende) an einem Punkt ist (für Tests und die Wahl der Seite).</summary>
    public float Belegung(float x, float z)
    {
        if (!Auf(x, z)) return 0;
        var (i, j) = ZelleBei(x, z);
        return Zusatz(j * _n + i);
    }

    /// <summary>
    /// Die Seite (fünf Stufen von −1 bis 1) mit der wenigsten Belegung entlang von Anfang → Zwischenpunkt → Ziel; bei gleicher Belegung die nächste zur eigenen
    /// <paramref name="seite"/>. So weichen Ströme einander aus: Wer nach anderen den Platz quert, nimmt einen freieren Streifen.
    /// </summary>
    private float FreiesteSeite((float X, float Z) von, (float X, float Z) nach, float frei, float seite, float quer, float rx, float rz)
    {
        var (beste, besteKosten) = (seite, float.MaxValue);
        var (dx, dz) = (nach.X - von.X, nach.Z - von.Z);
        var laenge = MathF.Sqrt(dx * dx + dz * dz);
        foreach (var stufe in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
        {
            var mitte = (von.X + dx * 0.5f + rx * quer * stufe, von.Z + dz * 0.5f + rz * quer * stufe);
            if (stufe != 0 && (!Auf(mitte.Item1, mitte.Item2, frei) || Abstand(mitte.Item1, mitte.Item2) < frei + 0.2f)) continue;
            var summe = 0f;
            foreach (var (a, b) in new[] { (von, mitte), (mitte, nach) })
            {
                var l = MathF.Sqrt((b.Item1 - a.Item1) * (b.Item1 - a.Item1) + (b.Item2 - a.Item2) * (b.Item2 - a.Item2));
                for (var t = 0.5f; t < l; t += 0.5f) summe += Belegung(a.Item1 + (b.Item1 - a.Item1) * t / l, a.Item2 + (b.Item2 - a.Item2) * t / l) * 0.5f;
            }
            var kosten = summe + 0.3f * MathF.Abs(stufe - seite) * laenge * 0.1f;
            if (kosten < besteKosten - 1e-4f) (beste, besteKosten) = (stufe, kosten);
        }
        return beste;
    }

    private readonly Dictionary<(int, int, int, int, int, int), List<(float X, float Z)>?> _gemerkt = new();

    /// <summary>
    /// Wie <see cref="Gefaechert"/>, aber gemerkt: Die Wege beginnen und enden fast immer an denselben Punkten (Einmündungen der Gassen, Stellen auf dem
    /// Platz), darum lohnt es sich – zur vollen Stunde suchen sonst Dutzende zugleich. Die Seite wird dafür auf fünf Stufen gerundet (−1, −0,5, 0, 0,5, 1),
    /// die Enden auf 5 cm. Höchstens 4000 Wege, dann beginnt der Speicher neu. Gibt eine Kopie zurück.
    /// </summary>
    public List<(float X, float Z)>? GefaechertGemerkt((float X, float Z) von, (float X, float Z) nach, float frei, float seite)
    {
        var stufe = (int)MathF.Round(Math.Clamp(seite, -1, 1) * 2);
        var schluessel = ((int)MathF.Round(von.X * 20), (int)MathF.Round(von.Z * 20), (int)MathF.Round(nach.X * 20), (int)MathF.Round(nach.Z * 20), stufe, (int)MathF.Round(frei * 100));
        if (!_gemerkt.TryGetValue(schluessel, out var weg))
        {
            if (_gemerkt.Count >= 4000) _gemerkt.Clear();
            _gemerkt[schluessel] = weg = Gefaechert(von, nach, frei, stufe / 2f);
            // (mit Belegung wählt Gefaechert die Seite selbst; der Schlüssel bleibt die eigene Stufe – bis zur nächsten Belegung der Stehenden, dann neu)
        }
        if (weg is null) return null;
        // genau die verlangten Enden (gerundet gemerkt)
        var kopie = new List<(float X, float Z)>(weg) { [0] = von };
        kopie[^1] = nach;
        return kopie;
    }

    /// <summary>Wie viele Wege gemerkt sind.</summary>
    public int Gemerkt => _gemerkt.Count;

    /// <summary>Länge einer Punktfolge (Meter).</summary>
    public static float Laenge(IReadOnlyList<(float X, float Z)> punkte)
    {
        var s = 0f;
        for (var i = 1; i < punkte.Count; i++)
        {
            var (dx, dz) = (punkte[i].X - punkte[i - 1].X, punkte[i].Z - punkte[i - 1].Z);
            s += MathF.Sqrt(dx * dx + dz * dz);
        }
        return s;
    }
}
