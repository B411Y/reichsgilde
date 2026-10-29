using System.Runtime.CompilerServices;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Kapazitätsprüfung: Passen die Gebäude, die eine Partie mit 8 Häusern braucht, wirklich in die Bauzonen der Stadt?
//
//  Ein Überschlag mit Flächen reicht nicht – Gebäude sind Rechtecke, Zonen sind schräg, und jedes Gebäude braucht eine Seite an einer
//  Straße. Deshalb setzt die Prüfung die Gebäude probeweise auf das Raster (bis Schritt 4 tools/karten/kapazitaet.py, gleiche Rechnung):
//
//    1. Jede Bauzone in der Stadt (Kern, Mauerring, Vorstadt) wird in Rasterfelder zerlegt (Mittelpunkt liegt in der Zone).
//    2. Für jedes Feld steht fest, wie weit die nächste Karrenstraße und der nächste Weg überhaupt entfernt sind.
//    3. Alle Gebäude der Karte belegen ihre Felder zuerst – die Grundfläche aus dem Katalog, bei schräger Drehung das umschließende
//       Rechteck (wie im Kern).
//    4. Dann setzt die Prüfung die Gebäude der Reihe nach, Werkstätten zuerst. Jedes nimmt den ersten freien Platz, der möglichst nah an der
//       Straße liegt („gierig“). Werkstätten brauchen eine Karrenstraße, Wohnhäuser dürfen auch an einer Gasse stehen (16.10.2026).
//
//  Ein gieriges Verfahren findet nicht immer die dichteste Packung. Passt alles hinein, ist die Antwort sicher „ja“; passt etwas nicht,
//  könnte ein geschickterer Spieler es vielleicht doch unterbringen – die Prüfung ist also eher streng, und das ist gewollt.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Gebäude der Kapazitätsprüfung: Art (oder „fest“ für die Gebäude der Karte) und die Rasterfelder, die es belegt.</summary>
public sealed record Kapazitaetsplatz(string Art, IReadOnlyCollection<(int I, int J)> Felder);

/// <summary>Das Ergebnis der Kapazitätsprüfung: Befunde (was nicht passt), Bericht (Zeilen wie das Werkzeug) und die gesetzten Gebäude.</summary>
public sealed record Kapazitaet(IReadOnlyList<Befund> Befunde, IReadOnlyList<string> Bericht, IReadOnlyList<Kapazitaetsplatz> Plaetze, double Raster);

/// <summary>Die Kapazitätsprüfung (siehe Kopf der Datei).</summary>
public static class Kapazitaetspruefung
{
    /// <summary>Gebäudegrößen in Rasterfeldern (Breite, Tiefe), entschieden 16.10.2026 – <c>docs/LEVELDESIGN.md</c>, „Gebäudegrößen“.</summary>
    public static readonly IReadOnlyDictionary<string, (int Breite, int Tiefe)> Groessen = new Dictionary<string, (int, int)>
    {
        ["Werkstatt"] = (8, 7),             // alle Stufen, 16 × 14 m
        ["Wohnhaus"] = (4, 5),              // Fachwerkhaus → Bürgerhaus → Steinhaus, 8 × 10 m
        ["Stadtpalais"] = (6, 5),           // 12 × 10 m, eigener Bau
        ["Stadthof"] = (8, 7),              // 16 × 14 m, eigener Bau, Kernlage oder Mauerring
        ["Einwohner-Wohnhaus"] = (3, 3),    // 6 × 6 m
        ["Einwohner-Bürgerhaus"] = (4, 4),  // 7 × 7 m, belegt 8 × 8 m
        ["Ratsherrenhaus"] = (4, 4),        // 8 × 8 m
    };

    /// <summary>So weit (Meter) darf ein Feld an der Vorderseite höchstens von der Straßenmitte weg sein; jedes Gebäude braucht zwei solche.</summary>
    public const double Strassennah = 7;

    /// <summary>
    /// Was eine Partie mit 8 Häusern in der Stadt braucht (ohne die Plätze der Karte). Pflicht: je Haus zwei weitere Werkstätten (KI-Häuser
    /// führen bis zu 3 Betriebe, ab Patrizier 4; einer ist die Startwerkstatt, einer liegt oft im Umland) und die Häuser der Einwohner (25 je
    /// Haus, Haushalte 4–6, im Mittel 5). Reserve: was wachsende Häuser später bauen könnten – jedes ein Stadtpalais, die Hälfte einen Stadthof.
    /// </summary>
    public static readonly IReadOnlyList<(string Art, int Anzahl)> Pflicht = new[]
    {
        ("Werkstatt", 16), ("Ratsherrenhaus", 13), ("Einwohner-Bürgerhaus", 3), ("Einwohner-Wohnhaus", (int)Math.Ceiling(8 * 25 / 5.0)),
    };

    public static readonly IReadOnlyList<(string Art, int Anzahl)> Reserve = new[] { ("Stadthof", 4), ("Stadtpalais", 8) };

    /// <summary>
    /// Prüft die Karte. <paramref name="schwere"/>: wie schwer es wiegt, wenn die Pflicht nicht passt (Köln: Fehler, neue Karten: Hinweis).
    /// Die Reserve steht nur im Bericht.
    /// </summary>
    public static Kapazitaet Pruefen(Kartenentwurf e, Schwere schwere = Schwere.Fehler) => new Belegung(e).Pruefen(schwere);

    /// <summary>
    /// Die Felder der Stadt-Bauzonen und was darauf steht – die Rechnung von <c>kapazitaet.py</c>. Die Felder stehen in Arrays über dem
    /// umschließenden Rechteck aller Stadt-Bauzonen (Feld (i, j) hat die Nummer (i − i0) · Höhe + (j − j0)), nicht in Wörterbüchern: Die Prüfung
    /// fragt Köln mit seinen 8.000 Feldern hunderttausendfach ab (27.10.2026: so mit den Blöcken unten von rund 75 auf 20 ms, dasselbe Ergebnis).
    /// </summary>
    private sealed class Belegung
    {
        /// <summary>So viele Felder je Richtung fasst ein Block, für den <see cref="Streckenfaecher"/> die Strecken in der Nähe einmal sammelt.</summary>
        private const int Block = 4;

        private readonly Kartenentwurf _e;
        private readonly double _r;
        private readonly int _i0, _j0, _breite, _hoehe;
        private readonly Lage?[] _zone;             // null: keine Stadt-Bauzone
        private readonly double[] _karren, _alle;   // Abstand zur nächsten Karrenstraße bzw. zum nächsten Weg
        private readonly bool[] _frei;
        private readonly int[] _felder;             // die Nummern aller Zonenfelder, aufsteigend
        private int _freiAnzahl;
        private readonly List<Kapazitaetsplatz> _gesetzt = new();

        // Die heißen Schleifen sind gleich voll optimiert (AggressiveOptimization): Die Kartenwerkstatt ruft die Prüfung nur ab und zu auf,
        // .NET würde sie sonst erst nach 30 Aufrufen nachoptimieren, und bis dahin dauert sie doppelt so lange.
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public Belegung(Kartenentwurf e)
        {
            (_e, _r) = (e, e.Raster);
            var zonen = e.Bauzonen.Where(z => z.Art != Lage.Umland && z.Ecken.Count > 0).Select(z => (z.Art, Ecken: z.Ecken,
                I0: (int)Math.Floor(z.Ecken.Min(q => q.X) / _r), I1: (int)Math.Floor(z.Ecken.Max(q => q.X) / _r),
                J0: (int)Math.Floor(z.Ecken.Min(q => q.Z) / _r), J1: (int)Math.Floor(z.Ecken.Max(q => q.Z) / _r))).ToList();
            if (zonen.Count > 0)
                (_i0, _j0, _breite, _hoehe) = (zonen.Min(z => z.I0), zonen.Min(z => z.J0),
                    zonen.Max(z => z.I1) - zonen.Min(z => z.I0) + 1, zonen.Max(z => z.J1) - zonen.Min(z => z.J0) + 1);
            _zone = new Lage?[_breite * _hoehe];
            // Eine spätere Zone überschreibt eine frühere, wie vorher im Wörterbuch. Je Rasterzeile (gleiches j, also gleiches Z) schneiden
            // dieselben Kanten mit denselben X-Werten wie in Kartengeometrie.ImVieleck; ein Feld liegt drin, wenn rechts von seiner Mitte
            // ungerade viele Schnitte liegen. Dasselbe Ergebnis, aber die Ecken werden je Zeile statt je Feld durchgegangen.
            var schnitte = new List<double>();
            foreach (var z in zonen)
                for (var j = z.J0; j <= z.J1; j++)
                {
                    var (p, pz) = (z.Ecken, Mitte(0, j).Z);
                    schnitte.Clear();
                    for (int a = 0, b = p.Count - 1; a < p.Count; b = a++)
                    {
                        var (x1, z1, x2, z2) = (p[a].X, p[a].Z, p[b].X, p[b].Z);
                        if ((z1 > pz) != (z2 > pz)) schnitte.Add((x2 - x1) * (pz - z1) / (z2 - z1) + x1);
                    }
                    schnitte.Sort();
                    var links = 0;   // so viele Schnitte liegen nicht rechts von der Feldmitte
                    for (var i = z.I0; i <= z.I1; i++)
                    {
                        var px = Mitte(i, j).X;
                        while (links < schnitte.Count && !(px < schnitte[links])) links++;
                        if ((schnitte.Count - links) % 2 == 1) _zone[Nummer(i, j)] = z.Art;
                    }
                }
            _felder = Enumerable.Range(0, _zone.Length).Where(n => _zone[n] is not null).ToArray();
            _frei = new bool[_zone.Length];
            foreach (var n in _felder) _frei[n] = true;
            _freiAnzahl = _felder.Length;

            // Die Abstände zur Straße blockweise: Felder eines Blocks teilen sich die Strecken in ihrer Nähe.
            _karren = new double[_zone.Length];
            _alle = new double[_zone.Length];
            var wege = e.Wege.Where(w => w.Punkte.Count > 0).ToList();
            var karren = new Streckenfaecher(wege.Where(w => w.IstKarrenstrasse));
            var alle = new Streckenfaecher(wege);
            var nummern = new List<int>(Block * Block);
            var punkte = new Kartenort[Block * Block];
            var abstand = new double[Block * Block];
            for (var bi = 0; bi < _breite; bi += Block)
                for (var bj = 0; bj < _hoehe; bj += Block)
                {
                    nummern.Clear();
                    for (var i = bi; i < Math.Min(bi + Block, _breite); i++)
                        for (var j = bj; j < Math.Min(bj + Block, _hoehe); j++)
                            if (_zone[i * _hoehe + j] is not null)
                            {
                                punkte[nummern.Count] = Mitte(_i0 + i, _j0 + j);
                                nummern.Add(i * _hoehe + j);
                            }
                    if (nummern.Count == 0) continue;
                    karren.Naechste(punkte, nummern.Count, abstand);
                    for (var k = 0; k < nummern.Count; k++) _karren[nummern[k]] = abstand[k];
                    alle.Naechste(punkte, nummern.Count, abstand);
                    for (var k = 0; k < nummern.Count; k++) _alle[nummern[k]] = abstand[k];
                }
        }

        private Kartenort Mitte(int i, int j) => new((i + 0.5) * _r, (j + 0.5) * _r);

        private int Nummer(int i, int j) => (i - _i0) * _hoehe + (j - _j0);

        /// <summary>Die Nummer des Feldes, oder −1, wenn es außerhalb aller Stadt-Bauzonen liegt.</summary>
        private int Zonenfeld(int i, int j) =>
            i < _i0 || j < _j0 || i >= _i0 + _breite || j >= _j0 + _hoehe || _zone[Nummer(i, j)] is null ? -1 : Nummer(i, j);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void Belegen(List<(int I, int J)> felder)
        {
            for (var k = 0; k < felder.Count; k++)
                if (_frei[Nummer(felder[k].I, felder[k].J)])
                {
                    _frei[Nummer(felder[k].I, felder[k].J)] = false;
                    _freiAnzahl--;
                }
        }

        /// <summary>Die Gebäude der Karte nehmen ihre Felder zuerst weg (Grundfläche aus dem Katalog).</summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void FestBelegen()
        {
            foreach (var g in _e.Gebaeude)
            {
                if (g.Groesse is not var (breite, tiefe) || g.Massstab <= 0) continue;
                var w = g.Drehung * Math.PI / 180;
                (double X, double Z) richtung;
                if (Kartengeometrie.Mod(g.Drehung, 90) != 0)
                {
                    // schräg: das umschließende Rechteck mit Drehung 0, wie der Kern
                    var (c, s) = (Math.Abs(DetMathe.Cos(w)), Math.Abs(DetMathe.Sin(w)));
                    (breite, tiefe, richtung) = (breite * c + tiefe * s, breite * s + tiefe * c, (0, 1));
                }
                else richtung = (Math.Round(DetMathe.Sin(w)), Math.Round(DetMathe.Cos(w)));
                var felder = RechteckFelder(g.Punkt, breite, tiefe, richtung);
                if (felder.Count == 0) continue;
                Belegen(felder);
                _gesetzt.Add(new Kapazitaetsplatz("fest", felder));
            }
        }

        /// <summary>Die Zonenfelder unter einem gedrehten Rechteck (Breite quer zur Richtung, Tiefe entlang), jedes einmal.</summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private List<(int I, int J)> RechteckFelder(Kartenort mitte, double breite, double tiefe, (double X, double Z) richtung)
        {
            var felder = new List<(int I, int J)>();
            var (ux, uz) = richtung;
            var (vx, vz) = (-uz, ux);
            var r = Math.Sqrt(breite * breite + tiefe * tiefe) / 2 + _r;
            for (var i = (int)Math.Floor((mitte.X - r) / _r); i <= (int)Math.Floor((mitte.X + r) / _r); i++)
                for (var j = (int)Math.Floor((mitte.Z - r) / _r); j <= (int)Math.Floor((mitte.Z + r) / _r); j++)
                {
                    var (cx, cz) = ((i + 0.5) * _r - mitte.X, (j + 0.5) * _r - mitte.Z);
                    if (Math.Abs(cx * vx + cz * vz) <= breite / 2 && Math.Abs(cx * ux + cz * uz) <= tiefe / 2 && Zonenfeld(i, j) >= 0)
                        felder.Add((i, j));
                }
            return felder;
        }

        /// <summary>Die Felder in der Reihenfolge, in der Werkstätten bzw. Wohnhäuser sie versuchen (einmal je Lauf; belegte werden übersprungen).</summary>
        private int[]? _folgeWerkstatt, _folgeWohnen;

        /// <summary>
        /// Ab welcher Stelle der Reihenfolge eine Gebäudeart weitersucht: Felder davor passten schon beim letzten Mal nicht, und weil Felder nur
        /// belegt, nie frei werden, passen sie auch später nicht (dasselbe Ergebnis, nur schneller).
        /// </summary>
        private readonly Dictionary<string, int> _ab = new();

        /// <summary>Setzt ein Gebäude auf den ersten passenden Platz; <paramref name="erlaubt"/> schränkt die Lagen ein (Stadthof nicht in der Vorstadt).</summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private bool Setzen(string art, IReadOnlySet<Lage>? erlaubt = null)
        {
            var (b, t) = Groessen[art];
            var werkstatt = art == "Werkstatt";
            var dist = werkstatt ? _karren : _alle;
            // Werkstätten so nah wie möglich an der Karrenstraße; Wohnhäuser lassen die Karrenstraßen den Werkstätten und suchen zuerst Gassen.
            // Die Reihenfolge hängt nur an den Abständen – sie gilt für alle Felder, die noch frei sind (wie das Sortieren der freien Felder).
            // Wohnhäuser: erst die straßennahen Felder, darunter erst die fern der Karrenstraße. Bei Gleichstand entscheidet die Nummer, also
            // erst i, dann j.
            var folge = werkstatt ? _folgeWerkstatt ??= Reihenfolge(_felder.Select(n => 0).ToArray(), dist)
                : _folgeWohnen ??= Reihenfolge(_felder.Select(n => (dist[n] > Strassennah ? 2 : 0) + (_karren[n] <= Strassennah ? 1 : 0)).ToArray(), dist);
            for (var n = _ab.GetValueOrDefault(art); n < folge.Length; n++)
            {
                if (!_frei[folge[n]]) continue;
                var (i, j) = (_i0 + folge[n] / _hoehe, _j0 + folge[n] % _hoehe);
                foreach (var (w, h) in new[] { (b, t), (t, b) })
                    foreach (var (oi, oj) in new[] { (0, 0), (-(w - 1), 0), (0, -(h - 1)), (-(w - 1), -(h - 1)) })
                    {
                        var (i0, j0) = (i + oi, j + oj);
                        if (!Passt(i0, j0, w, h, dist, erlaubt)) continue;
                        var felder = new List<(int, int)>(w * h);
                        for (var a = 0; a < w; a++)
                            for (var c = 0; c < h; c++)
                                felder.Add((i0 + a, j0 + c));
                        Belegen(felder);
                        _gesetzt.Add(new Kapazitaetsplatz(art, felder));
                        _ab[art] = n;
                        return true;
                    }
            }
            _ab[art] = folge.Length;
            return false;
        }

        /// <summary>
        /// Die Zonenfelder nach Gruppe (je Feld in <paramref name="gruppe"/>, gleiche Stelle wie in <see cref="_felder"/>), dann nach Abstand,
        /// dann nach Nummer. Ein stabiles Sortieren durch Mischen: Die Felder kommen schon nach Nummer geordnet herein, und bei Gleichstand
        /// bleibt diese Ordnung.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private int[] Reihenfolge(int[] gruppe, double[] dist)
        {
            var stellen = Enumerable.Range(0, _felder.Length).ToArray();
            var puffer = new int[stellen.Length];
            for (var breite = 1; breite < stellen.Length; breite *= 2)
            {
                for (var links = 0; links < stellen.Length; links += 2 * breite)
                {
                    var (mitte, rechts) = (Math.Min(links + breite, stellen.Length), Math.Min(links + 2 * breite, stellen.Length));
                    var (a, b, k) = (links, mitte, links);
                    while (a < mitte && b < rechts)
                    {
                        var (x, y) = (stellen[a], stellen[b]);
                        // y kommt nur vor x, wenn es echt kleiner ist (stabil)
                        var yVorher = gruppe[y] != gruppe[x] ? gruppe[y] < gruppe[x] : dist[_felder[y]] < dist[_felder[x]];
                        puffer[k++] = yVorher ? stellen[b++] : stellen[a++];
                    }
                    while (a < mitte) puffer[k++] = stellen[a++];
                    while (b < rechts) puffer[k++] = stellen[b++];
                }
                (stellen, puffer) = (puffer, stellen);
            }
            return stellen.Select(st => _felder[st]).ToArray();
        }

        /// <summary>Sind alle Felder des Rechtecks frei (und in einer erlaubten Lage), und liegen mindestens zwei nah genug an der Straße?</summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private bool Passt(int i0, int j0, int w, int h, double[] dist, IReadOnlySet<Lage>? erlaubt)
        {
            // Felder außerhalb des Rasters liegen in keiner Bauzone, sind also nie frei.
            if (i0 < _i0 || j0 < _j0 || i0 + w > _i0 + _breite || j0 + h > _j0 + _hoehe) return false;
            var nah = 0;
            for (var a = 0; a < w; a++)
            {
                var zeile = Nummer(i0 + a, j0);
                for (var f = zeile; f < zeile + h; f++)
                {
                    if (!_frei[f]) return false;
                    if (erlaubt is not null && !erlaubt.Contains(_zone[f]!.Value)) return false;
                    if (dist[f] <= Strassennah) nah++;
                }
            }
            return nah >= 2;
        }

        public Kapazitaet Pruefen(Schwere schwere)
        {
            FestBelegen();
            var befunde = new List<Befund>();
            var bericht = new List<string>();
            var gesamt = _felder.Length;
            bericht.Add($"Stadt-Bauzonen: {gesamt} Felder, davon fest belegt {gesamt - _freiAnzahl}");
            foreach (var (art, anzahl) in Pflicht)
            {
                var gesetzt = Enumerable.Range(0, anzahl).Count(_ => Setzen(art));
                var text = $"Pflicht {art}: {gesetzt} von {anzahl} passen";
                bericht.Add(text);
                if (gesetzt != anzahl)
                    befunde.Add(new Befund(schwere, "kapazitaet", $"Kapazität (Partie mit 8 Häusern): {text} in die Bauzonen der Stadt.", Array.Empty<string>(), null));
            }
            var nachPflicht = _freiAnzahl;
            var kernUndRing = new HashSet<Lage> { Lage.Kern, Lage.Mauerring };
            foreach (var (art, anzahl) in Reserve)
            {
                var gesetzt = Enumerable.Range(0, anzahl).Count(_ => Setzen(art, art == "Stadthof" ? kernUndRing : null));
                bericht.Add($"Reserve {art}: {gesetzt} von {anzahl} passen");
            }
            var anteil = gesamt == 0 ? 0 : (int)Math.Round(100.0 * nachPflicht / gesamt, MidpointRounding.ToEven);
            bericht.Add($"Frei nach der Pflicht: {nachPflicht} Felder ({anteil}%), nach der Reserve: {_freiAnzahl} Felder");
            return new Kapazitaet(befunde, bericht, _gesetzt, _r);
        }
    }
}

/// <summary>
/// Die Strecken der Wege in Fächern zu 8 × 8 m, damit der nächste Weg eines Feldes nur unter den Strecken in der Nähe gesucht wird (Köln:
/// 8.000 Felder × 5.000 Strecken, die geglätteten Wege sind fein unterteilt). Abstände über <see cref="Reichweite"/> rechnet er nicht genau –
/// siehe dort. Alles liegt in schlichten Arrays (die Fächer als ein Rechteck, je Fach ein Abschnitt in <see cref="_inhalt"/>): Die Prüfung
/// fragt sie zehntausendfach ab.
/// </summary>
internal sealed class Streckenfaecher
{
    /// <summary>
    /// Weiter als so viele Meter von der Straße kann kein Gebäude der Kapazitätsprüfung anfangen: Es braucht zwei Felder höchstens 7 m von ihr,
    /// und seine Felder liegen höchstens 7 Felder (14 m) je Richtung auseinander – also höchstens 7 + √(14² + 14²) ≈ 27 m. Abstände darüber
    /// bekommen alle diesen Wert. Das ändert kein Ergebnis (von solchen Feldern aus passt nie ein Gebäude), spart aber die meiste Rechnung.
    /// </summary>
    public const double Reichweite = 30;

    /// <summary>Rechenspielraum beim Aussortieren: Eine Strecke fällt nur weg, wenn sie sicher (nicht nur auf Rundung genau) weiter weg ist.</summary>
    private const double Spiel = 1e-9;

    private const double Fach = 8;
    private readonly Kartenort[] _a, _b;
    private readonly double[] _x0, _x1, _z0, _z1;     // der Rahmen jeder Strecke
    private readonly int _fi0, _fj0, _fBreite, _fHoehe;
    private readonly int[] _beginn, _inhalt;          // Fach f enthält die Strecken _inhalt[_beginn[f] .. _beginn[f + 1]]
    private readonly int[] _gesehen;
    private int _lauf;

    // Die Strecken in der Nähe der Punkte: Mindestabstand zum Rechteck der Punkte (danach sortiert) und Nummer.
    private double[] _mindestens = new double[64];
    private int[] _nummer = new int[64];
    private int _anzahl;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public Streckenfaecher(IEnumerable<EntwurfWeg> wege)
    {
        var strecken = new List<(Kartenort A, Kartenort B)>();
        foreach (var w in wege)
            for (var i = 0; i + 1 < w.Verlauf.Count; i++)
                strecken.Add((w.Verlauf[i], w.Verlauf[i + 1]));
        var anzahl = strecken.Count;
        (_a, _b) = (new Kartenort[anzahl], new Kartenort[anzahl]);
        (_x0, _x1, _z0, _z1) = (new double[anzahl], new double[anzahl], new double[anzahl], new double[anzahl]);
        for (var n = 0; n < anzahl; n++)
        {
            var (a, b) = strecken[n];
            (_a[n], _b[n]) = (a, b);
            (_x0[n], _x1[n], _z0[n], _z1[n]) = (Math.Min(a.X, b.X), Math.Max(a.X, b.X), Math.Min(a.Z, b.Z), Math.Max(a.Z, b.Z));
        }
        if (anzahl > 0)
        {
            _fi0 = (int)Math.Floor(_x0.Min() / Fach);
            _fj0 = (int)Math.Floor(_z0.Min() / Fach);
            _fBreite = (int)Math.Floor(_x1.Max() / Fach) - _fi0 + 1;
            _fHoehe = (int)Math.Floor(_z1.Max() / Fach) - _fj0 + 1;
        }
        // Zweimal durch die Strecken: erst zählen, wie viele in jedes Fach kommen, dann eintragen.
        _beginn = new int[_fBreite * _fHoehe + 1];
        for (var lauf = 0; lauf < 2; lauf++)
        {
            var stelle = lauf == 0 ? null : (int[])_beginn.Clone();
            for (var n = 0; n < anzahl; n++)
                for (var i = (int)Math.Floor(_x0[n] / Fach); i <= (int)Math.Floor(_x1[n] / Fach); i++)
                    for (var j = (int)Math.Floor(_z0[n] / Fach); j <= (int)Math.Floor(_z1[n] / Fach); j++)
                    {
                        var f = (i - _fi0) * _fHoehe + (j - _fj0);
                        if (stelle is null) _beginn[f + 1]++;
                        else _inhalt![stelle[f]++] = n;
                    }
            if (lauf == 0)
            {
                for (var f = 0; f < _fBreite * _fHoehe; f++) _beginn[f + 1] += _beginn[f];
                _inhalt = new int[_beginn[^1]];
            }
        }
        _inhalt ??= Array.Empty<int>();
        _gesehen = new int[anzahl];
    }

    /// <summary>
    /// Für jeden Punkt der kürzeste Abstand zu einer Strecke (<see cref="Kartengeometrie.AbstandStrecke"/>), höchstens <see cref="Reichweite"/>,
    /// (die ersten <paramref name="anzahl"/>) nach <paramref name="abstand"/> – genau derselbe Wert wie mit allen Strecken. Für nahe beieinander liegende Punkte gedacht (ein Block von
    /// Feldern), in drei Schritten:
    ///   1. Die Fächer werden ringweise um das Rechteck der Punkte abgesucht. Dabei steht eine obere Grenze fest: Kein Punkt ist weiter weg als
    ///      die Mitte des Rechtecks von der nächsten Strecke plus die halbe Diagonale. Liegt der nächste Ring weiter weg, kann er nichts Näheres
    ///      enthalten (jede Strecke liegt in allen Fächern ihres Rahmens, also auch in dem ihres Punktes, der einem Punkt am nächsten ist).
    ///   2. Jede gefundene Strecke bekommt ihren Mindestabstand zum Rechteck (Abstand der Rahmen, nie größer als der eines Punktes zur Strecke);
    ///      nur die unter der oberen Grenze bleiben, sortiert.
    ///   3. Jeder Punkt geht die Liste durch und hört auf, sobald der Mindestabstand über seinem besten Abstand liegt.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Naechste(Kartenort[] punkte, int anzahl, double[] abstand)
    {
        var (x0, x1, z0, z1) = (double.MaxValue, double.MinValue, double.MaxValue, double.MinValue);
        for (var p = 0; p < anzahl; p++)
            (x0, x1, z0, z1) = (Math.Min(x0, punkte[p].X), Math.Max(x1, punkte[p].X), Math.Min(z0, punkte[p].Z), Math.Max(z1, punkte[p].Z));
        var mitte = new Kartenort((x0 + x1) / 2, (z0 + z1) / 2);
        var halbeDiagonale = Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0)) / 2;
        var (fi0, fi1, fj0, fj1) = ((int)Math.Floor(x0 / Fach), (int)Math.Floor(x1 / Fach), (int)Math.Floor(z0 / Fach), (int)Math.Floor(z1 / Fach));
        _lauf++;
        _anzahl = 0;
        var oben = Reichweite;
        // Ring k: die Fächer mit Abstand k (in Fächern) um die Fächer des Rechtecks – mindestens (k − 1) Fächer vom Rechteck entfernt.
        for (var k = 0; (k - 1) * Fach <= oben + Spiel; k++)
            for (var i = fi0 - k; i <= fi1 + k; i++)
                for (var j = fj0 - k; j <= fj1 + k; j++)
                {
                    if (i != fi0 - k && i != fi1 + k && j != fj0 - k && j != fj1 + k) j = fj1 + k;   // das Innere hatten frühere Ringe
                    if (i < _fi0 || j < _fj0 || i >= _fi0 + _fBreite || j >= _fj0 + _fHoehe) continue;
                    var f = (i - _fi0) * _fHoehe + (j - _fj0);
                    for (var s = _beginn[f]; s < _beginn[f + 1]; s++)
                    {
                        var n = _inhalt[s];
                        if (_gesehen[n] == _lauf) continue;
                        _gesehen[n] = _lauf;
                        var (dx, dz) = (Math.Max(0, Math.Max(_x0[n] - x1, x0 - _x1[n])), Math.Max(0, Math.Max(_z0[n] - z1, z0 - _z1[n])));
                        var mindestens = Math.Sqrt(dx * dx + dz * dz);
                        if (mindestens > oben + Spiel) continue;
                        Merken(mindestens, n);
                        oben = Math.Min(oben, Kartengeometrie.AbstandStrecke(mitte, _a[n], _b[n]) + halbeDiagonale);
                    }
                }
        // Aussortieren, was über der endgültigen oberen Grenze liegt, und den Rest der Größe nach ordnen (Einfügen: es sind nur wenige).
        var behalten = 0;
        for (var c = 0; c < _anzahl; c++)
        {
            if (_mindestens[c] > oben + Spiel) continue;
            var (m, n) = (_mindestens[c], _nummer[c]);
            var d = behalten++;
            for (; d > 0 && _mindestens[d - 1] > m; d--) (_mindestens[d], _nummer[d]) = (_mindestens[d - 1], _nummer[d - 1]);
            (_mindestens[d], _nummer[d]) = (m, n);
        }
        for (var p = 0; p < anzahl; p++)
        {
            var best = Reichweite;
            for (var c = 0; c < behalten && _mindestens[c] <= best + Spiel; c++)
                best = Math.Min(best, Kartengeometrie.AbstandStrecke(punkte[p], _a[_nummer[c]], _b[_nummer[c]]));
            abstand[p] = best;
        }
    }

    private void Merken(double mindestens, int n)
    {
        if (_anzahl == _mindestens.Length)
        {
            Array.Resize(ref _mindestens, 2 * _anzahl);
            Array.Resize(ref _nummer, 2 * _anzahl);
        }
        (_mindestens[_anzahl], _nummer[_anzahl]) = (mindestens, n);
        _anzahl++;
    }
}
