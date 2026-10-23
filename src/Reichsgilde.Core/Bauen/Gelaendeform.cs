using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Das Gelände, wie man es sieht: Höhenraster mit eingeebneten Wegen und Mauern, Fundamente, Hangneigung (Kartenwerkstatt, Schritt 5b)
//
//  Das Höhenraster (Hoehenraster) ist das, was die Karte speichert und die Pinsel ändern. Wege und Mauern liegen auf dem Gelände, sollen
//  aber nicht schief hängen: Unter jeder Linie wird das Gelände quer eingeebnet – das „Bett“. Dazu bekommt die Linie ein Längsprofil (die
//  Höhe des Rasters unter der Mittellinie, über ±4 m gemittelt, damit der Weg nicht jede Delle mitnimmt); jeder Rasterpunkt im Bett
//  (halbe Breite) bekommt die Höhe des Profils an der nächsten Stelle der Linie, im Bankett daneben (2 m) geht er weich ins Gelände über.
//  Das gespeicherte Raster bleibt dabei unverändert: Der Unterschied liegt in einer eigenen Schicht und wird neu gerechnet, wenn sich Raster,
//  Wege oder Mauern ändern – nach einem Pinselstrich nur um den Strich herum.
//
//  Daraus fragt die Darstellung: die Höhe des Bodens (Hoehe, Ausschnitt für die Geländekacheln), das Profil eines Weges oder einer Mauer
//  (Profil – das Wegband liegt darauf; im Bett hat der Boden genau diese Höhe) und das Fundament eines Gebäudes (Fundament: es steht auf
//  dem höchsten Punkt seines Umrisses, darunter ein Sockel bis zum tiefsten). Die Prüfung fragt die Hangneigung eines Umrisses (Hang).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Eine Linie, unter der das Gelände quer eingeebnet wird: Mittellinie, Breite des Betts (Meter), Ring oder offen.</summary>
public readonly record struct Bettlinie(IReadOnlyList<Kartenort> Linie, double Breite, bool Geschlossen = false);

/// <summary>Das Fundament eines Gebäudes: Höhe des Bodens im Haus (höchster Punkt unter dem Umriss) und Tiefe des Sockels bis zum tiefsten.</summary>
public readonly record struct Fundament(double Hoehe, double Tiefe);

/// <summary>Die Hangneigung unter einem Umriss: die steilste Stelle (Anteil, 0,15 = 15 %) und wo sie liegt.</summary>
public readonly record struct Hanglage(double Gefaelle, Kartenort Ort);

/// <summary>Höhenraster mit dem Bett der Wege und Mauern (siehe Kopf der Datei).</summary>
public sealed class Gelaendeform
{
    /// <summary>So breit (Meter) geht das Bett neben der halben Breite weich ins Gelände über.</summary>
    public const double Bankett = 2;

    /// <summary>Über so viele Meter vor und hinter einer Stelle wird das Längsprofil gemittelt (Dreiecksgewicht).</summary>
    public const double Glaettung = 4;

    /// <summary>Abstand der Profilpunkte entlang einer Linie (Meter, höchstens).</summary>
    public const double Profilschritt = 1;

    /// <summary>Breite des Betts unter einer Mauer (Meter).</summary>
    public const double Mauerbett = 4;

    /// <summary>Seitenlänge der Fenster, in denen die Hangneigung gemessen wird (Meter; 4 × 4 Rasterzellen).</summary>
    public const double Hangfenster = 8;

    private readonly Func<IReadOnlyList<Bettlinie>> _linien;

    /// <summary>Boden minus Raster in cm – nur unter Wegen und Mauern ungleich 0.</summary>
    private readonly Hoehenraster _bett = new();

    private bool _allesNeu = true;
    private readonly List<Kartenrechteck> _veraltet = new();

    /// <param name="roh">Das gespeicherte Raster (die Form ändert es nie).</param>
    /// <param name="linien">Wege und Mauern – wird bei jedem Neurechnen gefragt.</param>
    public Gelaendeform(Hoehenraster roh, Func<IReadOnlyList<Bettlinie>> linien) => (Roh, _linien) = (roh, linien);

    /// <summary>Das gespeicherte Höhenraster, wie die Pinsel es ändern.</summary>
    public Hoehenraster Roh { get; }

    /// <summary>Ist das Gelände ganz flach (kein Höhenpunkt ungleich 0)? Dann ist auch das Bett überall 0.</summary>
    public bool IstFlach => Roh.IstLeer;

    // ── Neu rechnen ──────────────────────────────────────────────────────────

    /// <summary>Wege oder Mauern haben sich geändert: das ganze Bett beim nächsten Lesen neu.</summary>
    public void AllesVeraltet() => _allesNeu = true;

    /// <summary>Das Raster hat sich in diesem Rechteck geändert: das Bett dort (mit Reichweite der Profile) beim nächsten Lesen neu.</summary>
    public void Veraltet(Kartenrechteck bereich) => _veraltet.Add(bereich);

    /// <summary>Rechnet das Bett neu, wo es veraltet ist.</summary>
    private void Aktualisieren()
    {
        if (!_allesNeu && _veraltet.Count == 0) return;
        if (Roh.IstLeer)
        {
            // flach: das Profil ist überall 0, das Bett auch
            if (!_bett.IstLeer) _bett.BereichLeeren(int.MinValue / 2, int.MinValue / 2, int.MaxValue / 2, int.MaxValue / 2);
            _allesNeu = false;
            _veraltet.Clear();
            return;
        }
        var linien = _linien().Where(l => l.Linie.Count >= 2 && l.Breite > 0).ToList();
        if (_allesNeu)
        {
            _bett.BereichLeeren(int.MinValue / 2, int.MinValue / 2, int.MaxValue / 2, int.MaxValue / 2);
            BettRechnen(linien, null);
        }
        else
        {
            var reichweite = Glaettung + Profilschritt + Hoehenraster.Abstand + (linien.Count == 0 ? 0 : linien.Max(l => l.Breite / 2)) + Bankett;
            foreach (var r in _veraltet)
            {
                var (i0, j0, i1, j1) = Hoehenraster.Punkte(r.Vergroessert(reichweite));
                _bett.BereichLeeren(i0, j0, i1, j1);
                BettRechnen(linien, (i0, j0, i1, j1));
            }
        }
        _allesNeu = false;
        _veraltet.Clear();
    }

    /// <summary>Rechnet das Bett aller Linien, nur für die Rasterpunkte in <paramref name="nur"/> (null: überall).</summary>
    private void BettRechnen(IReadOnlyList<Bettlinie> linien, (int I0, int J0, int I1, int J1)? nur)
    {
        // je Rasterpunkt der beste Kandidat: höheres Gewicht gewinnt, bei gleichem der kleinere Abstand
        var beste = new Dictionary<(int, int), (double Gewicht, double Abstand, double Ziel)>();
        foreach (var l in linien)
        {
            var halb = l.Breite / 2;
            var weite = halb + Bankett;
            if (nur is { } bereich)
            {
                // eine Linie, deren Bett den Bereich nicht trifft, braucht kein Profil
                var (li0, lj0, li1, lj1) = Hoehenraster.Punkte(Kartenrechteck.Um(l.Linie)!.Value.Vergroessert(weite));
                if (li0 > bereich.I1 || li1 < bereich.I0 || lj0 > bereich.J1 || lj1 < bereich.J0) continue;
            }
            var punkte = l.Geschlossen && l.Linie[0] != l.Linie[^1] ? l.Linie.Append(l.Linie[0]).ToList() : l.Linie.ToList();
            var profil = Laengsprofil(punkte);
            for (var s = 0; s + 1 < punkte.Count; s++)
            {
                var (a, b) = (punkte[s], punkte[s + 1]);
                var (dx, dz) = (b.X - a.X, b.Z - a.Z);
                var l2 = dx * dx + dz * dz;
                var laenge = Math.Sqrt(l2);
                var r = new Kartenrechteck(Math.Min(a.X, b.X), Math.Min(a.Z, b.Z), Math.Max(a.X, b.X), Math.Max(a.Z, b.Z)).Vergroessert(weite);
                var (i0, j0, i1, j1) = Hoehenraster.Punkte(r);
                if (nur is { } n) (i0, j0, i1, j1) = (Math.Max(i0, n.I0), Math.Max(j0, n.J0), Math.Min(i1, n.I1), Math.Min(j1, n.J1));
                for (var j = j0; j <= j1; j++)
                    for (var i = i0; i <= i1; i++)
                    {
                        var (px, pz) = (i * Hoehenraster.Abstand, j * Hoehenraster.Abstand);
                        var t = l2 == 0 ? 0 : Math.Clamp(((px - a.X) * dx + (pz - a.Z) * dz) / l2, 0, 1);
                        var (qx, qz) = (a.X + t * dx - px, a.Z + t * dz - pz);
                        var d = Math.Sqrt(qx * qx + qz * qz);
                        if (d >= weite) continue;
                        var w = d <= halb ? 1 : Weich((weite - d) / Bankett);
                        if (beste.TryGetValue((i, j), out var alt) && (alt.Gewicht > w || (alt.Gewicht == w && alt.Abstand <= d))) continue;
                        beste[(i, j)] = (w, d, profil.Hoehe(s, t * laenge));
                    }
            }
        }
        foreach (var ((i, j), (w, _, ziel)) in beste)
        {
            int roh = Roh.Wert(i, j);
            var boden = roh + (ziel * 100 - roh) * w;
            var unterschied = (int)Math.Round(boden - roh, MidpointRounding.AwayFromZero);
            if (unterschied != 0) _bett.WertSetzen(i, j, unterschied);
        }
    }

    /// <summary>Weicher Übergang 0 … 1 (Smoothstep: flach am Anfang und am Ende).</summary>
    internal static double Weich(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    // ── Längsprofil ──────────────────────────────────────────────────────────

    /// <summary>Das gemittelte Längsprofil einer Linie: Profilpunkte je Strecke, Höhe an jeder Stelle linear dazwischen.</summary>
    private sealed class Profilkette
    {
        /// <summary>Je Strecke der Linie: die Abstände der Profilpunkte vom Streckenanfang und ihre gemittelten Höhen.</summary>
        public readonly List<(double[] Stellen, double[] Hoehen)> Strecken = new();

        /// <summary>Je Strecke die Orte ihrer Profilpunkte (der erste und der letzte sind die Punkte der Linie).</summary>
        public readonly List<Kartenort[]> Orte = new();

        /// <summary>Die Höhe auf Strecke <paramref name="s"/>, <paramref name="weg"/> Meter nach ihrem Anfang.</summary>
        public double Hoehe(int s, double weg)
        {
            var (stellen, hoehen) = Strecken[s];
            if (weg <= stellen[0]) return hoehen[0];
            for (var k = 1; k < stellen.Length; k++)
                if (weg <= stellen[k])
                {
                    var t = (weg - stellen[k - 1]) / (stellen[k] - stellen[k - 1]);
                    return hoehen[k - 1] + (hoehen[k] - hoehen[k - 1]) * t;
                }
            return hoehen[^1];
        }
    }

    /// <summary>Tastet die Linie alle (höchstens) 1 m ab, liest das Raster und mittelt über ±4 m (Dreiecksgewicht).</summary>
    private Profilkette Laengsprofil(IReadOnlyList<Kartenort> punkte)
    {
        // alle Profilpunkte der Linie: Ort entlang der ganzen Linie und Rohhöhe
        var orte = new List<double>();
        var punkteAlle = new List<Kartenort>();
        var roh = new List<double>();
        var jeStrecke = new List<(int Erster, int Anzahl, double Anfang)>();
        var entlang = 0.0;
        for (var s = 0; s + 1 < punkte.Count; s++)
        {
            var (a, b) = (punkte[s], punkte[s + 1]);
            var laenge = a.Abstand(b);
            var teile = Math.Max(1, (int)Math.Ceiling(laenge / Profilschritt - 1e-9));
            jeStrecke.Add((orte.Count, teile + 1, entlang));
            for (var k = 0; k <= teile; k++)
            {
                var t = (double)k / teile;
                var p = k == teile ? b : new Kartenort(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
                orte.Add(entlang + laenge * t);
                punkteAlle.Add(p);
                roh.Add(Roh.Hoehe(p.X, p.Z));
            }
            entlang += laenge;
        }
        // mitteln (die Punkte an den Streckengrenzen stehen doppelt – das stört das Mittel nicht, beide haben dieselbe Höhe)
        var glatt = new double[orte.Count];
        var unten = 0;
        for (var k = 0; k < orte.Count; k++)
        {
            while (orte[unten] < orte[k] - Glaettung) unten++;
            double summe = 0, gewichte = 0;
            for (var m = unten; m < orte.Count && orte[m] <= orte[k] + Glaettung; m++)
            {
                var g = 1 - Math.Abs(orte[m] - orte[k]) / Glaettung;
                if (g <= 0) continue;
                summe += g * roh[m];
                gewichte += g;
            }
            glatt[k] = gewichte > 0 ? summe / gewichte : roh[k];
        }
        var kette = new Profilkette();
        foreach (var (erster, anzahl, anfang) in jeStrecke)
        {
            kette.Strecken.Add((orte.Skip(erster).Take(anzahl).Select(o => o - anfang).ToArray(), glatt.Skip(erster).Take(anzahl).ToArray()));
            kette.Orte.Add(punkteAlle.Skip(erster).Take(anzahl).ToArray());
        }
        return kette;
    }

    /// <summary>
    /// Das Höhenprofil einer Linie (Weg oder Mauer) an ihren Punkten, in Metern: die Höhe, auf der das Wegband liegt – im Bett hat der Boden
    /// genau diese Höhe. <paramref name="geschlossen"/>: Ring (die letzte Strecke führt zum ersten Punkt zurück, der nicht doppelt kommt).
    /// </summary>
    public double[] Profil(IReadOnlyList<Kartenort> linie, bool geschlossen = false)
    {
        if (linie.Count == 0) return Array.Empty<double>();
        if (Roh.IstLeer || linie.Count == 1) return linie.Select(p => Roh.Hoehe(p.X, p.Z)).ToArray();
        var punkte = geschlossen && linie[0] != linie[^1] ? linie.Append(linie[0]).ToList() : linie.ToList();
        var kette = Laengsprofil(punkte);
        var aus = new double[linie.Count];
        for (var k = 0; k < linie.Count; k++)
            aus[k] = k < kette.Strecken.Count ? kette.Hoehe(k, 0) : kette.Hoehe(kette.Strecken.Count - 1, double.MaxValue);
        return aus;
    }

    /// <summary>
    /// Das Längsprofil einer Linie als Punktfolge: höchstens alle 1 m ein Punkt (die Punkte der Linie sind dabei, keiner doppelt) mit der
    /// Höhe, auf der das Wegband bzw. die Mauer liegt. Darüber legt die Darstellung das Band – auch eine gerade Straße mit nur zwei Punkten
    /// folgt so dem Gelände. Ein Ring (<paramref name="geschlossen"/>) endet wieder an seinem ersten Punkt. Flach: alle Höhen 0.
    /// </summary>
    public IReadOnlyList<(Kartenort Ort, double Hoehe)> Profillinie(IReadOnlyList<Kartenort> linie, bool geschlossen = false)
    {
        if (linie.Count < 2) return linie.Select(p => (p, Roh.Hoehe(p.X, p.Z))).ToList();
        var punkte = geschlossen && linie[0] != linie[^1] ? linie.Append(linie[0]).ToList() : linie.ToList();
        var kette = Laengsprofil(punkte);
        var aus = new List<(Kartenort, double)>();
        for (var s = 0; s < kette.Strecken.Count; s++)
            for (var k = s == 0 ? 0 : 1; k < kette.Orte[s].Length; k++)
                aus.Add((kette.Orte[s][k], kette.Strecken[s].Hoehen[k]));
        return aus;
    }

    // ── Boden lesen ──────────────────────────────────────────────────────────

    /// <summary>Die Höhe des Bodens in Metern (Raster mit Bett der Wege und Mauern), bilinear.</summary>
    public double Hoehe(double x, double z)
    {
        if (Roh.IstLeer) return 0;
        Aktualisieren();
        return Roh.Hoehe(x, z) + _bett.Hoehe(x, z);
    }

    /// <summary>Die Höhe eines Rasterpunkts des Bodens in Metern.</summary>
    public double Punkthoehe(int i, int j)
    {
        if (Roh.IstLeer) return 0;
        Aktualisieren();
        return (Roh.Wert(i, j) + _bett.Wert(i, j)) / 100.0;
    }

    /// <summary>Die Steigung des Bodens: Höhenänderung je Meter nach Osten (x) und Süden (z).</summary>
    public (double X, double Z) Steigung(double x, double z)
    {
        if (Roh.IstLeer) return (0, 0);
        Aktualisieren();
        var (a, b) = (Roh.Steigung(x, z), _bett.Steigung(x, z));
        return (a.X + b.X, a.Z + b.Z);
    }

    /// <summary>Die Normale des Bodens (Einheitsvektor, Y nach oben).</summary>
    public (double X, double Y, double Z) Normale(double x, double z)
    {
        var (sx, sz) = Steigung(x, z);
        var l = Math.Sqrt(sx * sx + 1 + sz * sz);
        return (-sx / l, 1 / l, -sz / l);
    }

    /// <summary>
    /// Ein Ausschnitt der Rasterpunkte des Bodens in Metern: <paramref name="breite"/> × <paramref name="tiefe"/> Punkte ab (i0, j0), Zeile
    /// für Zeile (j) – für die Geländekacheln der Darstellung (Punkt (i, j) liegt bei x = 2·i, z = 2·j).
    /// </summary>
    public float[] Ausschnitt(int i0, int j0, int breite, int tiefe)
    {
        var aus = Roh.Ausschnitt(i0, j0, breite, tiefe);
        if (Roh.IstLeer) return aus;
        Aktualisieren();
        var bett = _bett.Ausschnitt(i0, j0, breite, tiefe);
        for (var k = 0; k < aus.Length; k++) aus[k] += bett[k];
        return aus;
    }

    // ── Gebäude ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Das Fundament eines Gebäudes mit diesem Umriss (Ecken der Grundfläche): Der Boden im Haus liegt auf dem höchsten Punkt des Bodens
    /// darunter (Ecken und Rasterpunkte darin), der Sockel reicht bis zum tiefsten. Das Gelände bleibt unverändert. Flach: (0, 0).
    /// </summary>
    public Fundament Fundament(IReadOnlyList<Kartenort> umriss)
    {
        if (Roh.IstLeer || umriss.Count == 0) return default;
        double hoch = double.MinValue, tief = double.MaxValue;
        foreach (var h in Proben(umriss, boden: true))
            (hoch, tief) = (Math.Max(hoch, h), Math.Min(tief, h));
        return new Fundament(hoch, hoch - tief);
    }

    /// <summary>Die Höhen unter einem Umriss: die Ecken, dazu jeder Rasterpunkt darin (vom Boden oder vom gespeicherten Raster).</summary>
    private IEnumerable<double> Proben(IReadOnlyList<Kartenort> umriss, bool boden)
    {
        foreach (var e in umriss) yield return boden ? Hoehe(e.X, e.Z) : Roh.Hoehe(e.X, e.Z);
        if (umriss.Count < 3) yield break;
        var (i0, j0, i1, j1) = Hoehenraster.Punkte(Kartenrechteck.Um(umriss)!.Value);
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
                if (Kartengeometrie.ImVieleck(new Kartenort(i * Hoehenraster.Abstand, j * Hoehenraster.Abstand), umriss))
                    yield return boden ? Punkthoehe(i, j) : Roh.Wert(i, j) / 100.0;
    }

    /// <summary>
    /// Die Hangneigung unter einem Umriss (Gebäude oder Bauzone), gemessen am gespeicherten Raster (die Einebnung der Wege zählt nicht): Der
    /// Boden ist in Fenster zu 8 × 8 m geteilt (am Boden verankert); in jedem Fenster, das den Umriss trifft, wird eine Ebene durch seine
    /// 5 × 5 Rasterpunkte gelegt (kleinste Quadrate) – deren Neigung ist das Gefälle dort, kleine Buckel heben sich auf. Ergebnis: das
    /// steilste Fenster. Flach: 0 an der Mitte des Umrisses.
    /// </summary>
    public Hanglage Hang(IReadOnlyList<Kartenort> umriss)
    {
        if (umriss.Count == 0) return default;
        var mitte = Kartengeometrie.Mitte(umriss);
        if (Roh.IstLeer) return new Hanglage(0, mitte);
        var n = (int)(Hangfenster / Hoehenraster.Abstand); // Rasterzellen je Fenster (4)
        var r = Kartenrechteck.Um(umriss)!.Value;
        var (fi0, fj0) = ((int)Math.Floor(r.X0 / Hangfenster), (int)Math.Floor(r.Z0 / Hangfenster));
        var (fi1, fj1) = ((int)Math.Floor(r.X1 / Hangfenster), (int)Math.Floor(r.Z1 / Hangfenster));
        var best = new Hanglage(-1, mitte);
        for (var fj = fj0; fj <= fj1; fj++)
            for (var fi = fi0; fi <= fi1; fi++)
            {
                var fenster = new Kartenrechteck(fi * Hangfenster, fj * Hangfenster, (fi + 1) * Hangfenster, (fj + 1) * Hangfenster);
                if (!Trifft(fenster, umriss)) continue;
                // Ebene durch die 5 × 5 Punkte: Neigung je Achse = Σ(Abstand von der Mitte · Höhe) / Σ Abstand²
                double sx = 0, sz = 0, q = 0;
                for (var b = 0; b <= n; b++)
                    for (var a = 0; a <= n; a++)
                    {
                        var h = Roh.Wert(fi * n + a, fj * n + b) / 100.0;
                        var (dx, dz) = ((a - n / 2.0) * Hoehenraster.Abstand, (b - n / 2.0) * Hoehenraster.Abstand);
                        (sx, sz, q) = (sx + dx * h, sz + dz * h, q + dx * dx);
                    }
                var gefaelle = Math.Sqrt(sx * sx + sz * sz) / q;
                if (gefaelle > best.Gefaelle) best = new Hanglage(gefaelle, fenster.Mitte);
            }
        return best.Gefaelle < 0 ? new Hanglage(0, mitte) : best;
    }

    /// <summary>Trifft das Fenster den Umriss? (Eine Ecke des einen liegt im anderen, oder Kanten schneiden sich.)</summary>
    private static bool Trifft(Kartenrechteck f, IReadOnlyList<Kartenort> umriss)
    {
        var ecken = new[] { new Kartenort(f.X0, f.Z0), new Kartenort(f.X1, f.Z0), new Kartenort(f.X1, f.Z1), new Kartenort(f.X0, f.Z1) };
        if (umriss.Any(p => f.Enthaelt(p)) || (umriss.Count >= 3 && (Kartengeometrie.ImVieleck(f.Mitte, umriss) || ecken.Any(e => Kartengeometrie.ImVieleck(e, umriss)))))
            return true;
        foreach (var (a, b) in Kartengeometrie.Strecken(umriss, umriss.Count >= 3))
            foreach (var (c, d) in Kartengeometrie.Strecken(ecken))
                if (Kartengeometrie.Schnitt(a, b, c, d) is not null) return true;
        return false;
    }
}
