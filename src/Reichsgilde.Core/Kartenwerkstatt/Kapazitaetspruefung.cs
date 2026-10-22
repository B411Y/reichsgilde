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

    /// <summary>Die Felder der Stadt-Bauzonen und was darauf steht – die Rechnung von <c>kapazitaet.py</c>.</summary>
    private sealed class Belegung
    {
        private readonly Kartenentwurf _e;
        private readonly double _r;
        private readonly Dictionary<(int, int), Lage> _zone = new();
        private readonly Dictionary<(int, int), double> _karren = new(), _alle = new();
        private readonly HashSet<(int, int)> _frei;
        private readonly List<Kapazitaetsplatz> _gesetzt = new();

        public Belegung(Kartenentwurf e)
        {
            (_e, _r) = (e, e.Raster);
            foreach (var z in e.Bauzonen.Where(z => z.Art != Lage.Umland && z.Ecken.Count > 0))
            {
                var p = z.Ecken;
                for (var i = (int)Math.Floor(p.Min(q => q.X) / _r); i <= (int)Math.Floor(p.Max(q => q.X) / _r); i++)
                    for (var j = (int)Math.Floor(p.Min(q => q.Z) / _r); j <= (int)Math.Floor(p.Max(q => q.Z) / _r); j++)
                        if (Kartengeometrie.ImVieleck(Mitte((i, j)), p))
                            _zone[(i, j)] = z.Art;
            }
            var wege = e.Wege.Where(w => w.Punkte.Count > 0).ToList();
            var karren = new Streckenfaecher(wege.Where(w => w.IstKarrenstrasse));
            var alle = new Streckenfaecher(wege);
            foreach (var f in _zone.Keys)
            {
                var m = Mitte(f);
                _karren[f] = karren.Naechste(m);
                _alle[f] = alle.Naechste(m);
            }
            _frei = _zone.Keys.ToHashSet();
        }

        private Kartenort Mitte((int I, int J) f) => new((f.I + 0.5) * _r, (f.J + 0.5) * _r);

        /// <summary>Die Gebäude der Karte nehmen ihre Felder zuerst weg (Grundfläche aus dem Katalog).</summary>
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
                    var (c, s) = (Math.Abs(Math.Cos(w)), Math.Abs(Math.Sin(w)));
                    (breite, tiefe, richtung) = (breite * c + tiefe * s, breite * s + tiefe * c, (0, 1));
                }
                else richtung = (Math.Round(Math.Sin(w)), Math.Round(Math.Cos(w)));
                var felder = RechteckFelder(g.Punkt, breite, tiefe, richtung).Where(_zone.ContainsKey).ToHashSet();
                if (felder.Count == 0) continue;
                _frei.ExceptWith(felder);
                _gesetzt.Add(new Kapazitaetsplatz("fest", felder));
            }
        }

        /// <summary>Felder unter einem gedrehten Rechteck (Breite quer zur Richtung, Tiefe entlang).</summary>
        private IEnumerable<(int, int)> RechteckFelder(Kartenort mitte, double breite, double tiefe, (double X, double Z) richtung)
        {
            var (ux, uz) = richtung;
            var (vx, vz) = (-uz, ux);
            var r = Math.Sqrt(breite * breite + tiefe * tiefe) / 2 + _r;
            for (var i = (int)Math.Floor((mitte.X - r) / _r); i <= (int)Math.Floor((mitte.X + r) / _r); i++)
                for (var j = (int)Math.Floor((mitte.Z - r) / _r); j <= (int)Math.Floor((mitte.Z + r) / _r); j++)
                {
                    var (cx, cz) = ((i + 0.5) * _r - mitte.X, (j + 0.5) * _r - mitte.Z);
                    if (Math.Abs(cx * vx + cz * vz) <= breite / 2 && Math.Abs(cx * ux + cz * uz) <= tiefe / 2)
                        yield return (i, j);
                }
        }

        /// <summary>Die Felder in der Reihenfolge, in der Werkstätten bzw. Wohnhäuser sie versuchen (einmal je Lauf; belegte werden übersprungen).</summary>
        private List<(int, int)>? _folgeWerkstatt, _folgeWohnen;

        /// <summary>
        /// Ab welcher Stelle der Reihenfolge eine Gebäudeart weitersucht: Felder davor passten schon beim letzten Mal nicht, und weil Felder nur
        /// belegt, nie frei werden, passen sie auch später nicht (dasselbe Ergebnis, nur schneller).
        /// </summary>
        private readonly Dictionary<string, int> _ab = new();

        /// <summary>Setzt ein Gebäude auf den ersten passenden Platz; <paramref name="erlaubt"/> schränkt die Lagen ein (Stadthof nicht in der Vorstadt).</summary>
        private bool Setzen(string art, IReadOnlySet<Lage>? erlaubt = null)
        {
            var (b, t) = Groessen[art];
            var werkstatt = art == "Werkstatt";
            var dist = werkstatt ? _karren : _alle;
            // Werkstätten so nah wie möglich an der Karrenstraße; Wohnhäuser lassen die Karrenstraßen den Werkstätten und suchen zuerst Gassen.
            // Die Reihenfolge hängt nur an den Abständen – sie gilt für alle Felder, die noch frei sind (wie das Sortieren der freien Felder).
            var folge = werkstatt
                ? _folgeWerkstatt ??= _zone.Keys.OrderBy(f => dist[f]).ThenBy(f => f.Item1).ThenBy(f => f.Item2).ToList()
                : _folgeWohnen ??= _zone.Keys.OrderBy(f => dist[f] > Strassennah).ThenBy(f => _karren[f] <= Strassennah).ThenBy(f => dist[f])
                    .ThenBy(f => f.Item1).ThenBy(f => f.Item2).ToList();
            for (var n = _ab.GetValueOrDefault(art); n < folge.Count; n++)
            {
                var (i, j) = folge[n];
                if (!_frei.Contains((i, j))) continue;
                foreach (var (w, h) in new[] { (b, t), (t, b) })
                    foreach (var (oi, oj) in new[] { (0, 0), (-(w - 1), 0), (0, -(h - 1)), (-(w - 1), -(h - 1)) })
                    {
                        var (i0, j0) = (i + oi, j + oj);
                        if (!Passt(i0, j0, w, h, dist, erlaubt)) continue;
                        var felder = new List<(int, int)>(w * h);
                        for (var a = 0; a < w; a++)
                            for (var c = 0; c < h; c++)
                                felder.Add((i0 + a, j0 + c));
                        _frei.ExceptWith(felder);
                        _gesetzt.Add(new Kapazitaetsplatz(art, felder));
                        _ab[art] = n;
                        return true;
                    }
            }
            _ab[art] = folge.Count;
            return false;
        }

        /// <summary>Sind alle Felder des Rechtecks frei (und in einer erlaubten Lage), und liegen mindestens zwei nah genug an der Straße?</summary>
        private bool Passt(int i0, int j0, int w, int h, Dictionary<(int, int), double> dist, IReadOnlySet<Lage>? erlaubt)
        {
            var nah = 0;
            for (var a = 0; a < w; a++)
                for (var c = 0; c < h; c++)
                {
                    var f = (i0 + a, j0 + c);
                    if (!_frei.Contains(f)) return false;
                    if (erlaubt is not null && !erlaubt.Contains(_zone[f])) return false;
                    if (dist[f] <= Strassennah) nah++;
                }
            return nah >= 2;
        }

        public Kapazitaet Pruefen(Schwere schwere)
        {
            FestBelegen();
            var befunde = new List<Befund>();
            var bericht = new List<string>();
            var gesamt = _zone.Count;
            bericht.Add($"Stadt-Bauzonen: {gesamt} Felder, davon fest belegt {gesamt - _frei.Count}");
            foreach (var (art, anzahl) in Pflicht)
            {
                var gesetzt = Enumerable.Range(0, anzahl).Count(_ => Setzen(art));
                var text = $"Pflicht {art}: {gesetzt} von {anzahl} passen";
                bericht.Add(text);
                if (gesetzt != anzahl)
                    befunde.Add(new Befund(schwere, "kapazitaet", $"Kapazität (Partie mit 8 Häusern): {text} in die Bauzonen der Stadt.", Array.Empty<string>(), null));
            }
            var nachPflicht = _frei.Count;
            var kernUndRing = new HashSet<Lage> { Lage.Kern, Lage.Mauerring };
            foreach (var (art, anzahl) in Reserve)
            {
                var gesetzt = Enumerable.Range(0, anzahl).Count(_ => Setzen(art, art == "Stadthof" ? kernUndRing : null));
                bericht.Add($"Reserve {art}: {gesetzt} von {anzahl} passen");
            }
            var anteil = gesamt == 0 ? 0 : (int)Math.Round(100.0 * nachPflicht / gesamt, MidpointRounding.ToEven);
            bericht.Add($"Frei nach der Pflicht: {nachPflicht} Felder ({anteil}%), nach der Reserve: {_frei.Count} Felder");
            return new Kapazitaet(befunde, bericht, _gesetzt, _r);
        }
    }
}

/// <summary>
/// Die Strecken der Wege in Fächern zu 16 × 16 m, damit der nächste Weg eines Feldes nur unter den Strecken in der Nähe gesucht wird (Köln:
/// 5.000 Felder × 200 Strecken). Abstände über <see cref="Reichweite"/> rechnet er nicht genau – siehe dort.
/// </summary>
internal sealed class Streckenfaecher
{
    /// <summary>
    /// Weiter als so viele Meter von der Straße kann kein Gebäude der Kapazitätsprüfung anfangen: Es braucht zwei Felder höchstens 7 m von ihr,
    /// und seine Felder liegen höchstens 7 Felder (14 m) je Richtung auseinander – also höchstens 7 + √(14² + 14²) ≈ 27 m. Abstände darüber
    /// bekommen alle diesen Wert. Das ändert kein Ergebnis (von solchen Feldern aus passt nie ein Gebäude), spart aber die meiste Rechnung.
    /// </summary>
    public const double Reichweite = 30;

    private const double Fach = 16;
    private readonly List<(Kartenort A, Kartenort B)> _strecken = new();
    private readonly Dictionary<(int, int), List<int>> _faecher = new();
    private readonly int[] _gesehen;
    private int _lauf;

    public Streckenfaecher(IEnumerable<EntwurfWeg> wege)
    {
        foreach (var w in wege)
            for (var i = 0; i + 1 < w.Verlauf.Count; i++)
                _strecken.Add((w.Verlauf[i], w.Verlauf[i + 1]));
        for (var n = 0; n < _strecken.Count; n++)
        {
            var (a, b) = _strecken[n];
            for (var i = (int)Math.Floor(Math.Min(a.X, b.X) / Fach); i <= (int)Math.Floor(Math.Max(a.X, b.X) / Fach); i++)
                for (var j = (int)Math.Floor(Math.Min(a.Z, b.Z) / Fach); j <= (int)Math.Floor(Math.Max(a.Z, b.Z) / Fach); j++)
                {
                    if (!_faecher.TryGetValue((i, j), out var liste)) _faecher[(i, j)] = liste = new List<int>();
                    liste.Add(n);
                }
        }
        _gesehen = new int[_strecken.Count];
    }

    /// <summary>
    /// Der kürzeste Abstand von p zu einer Strecke (<see cref="Kartengeometrie.AbstandStrecke"/>), höchstens <see cref="Reichweite"/>. Jede
    /// Strecke näher als die Reichweite liegt in einem Fach des Quadrats p ± Reichweite – das Ergebnis ist also genau.
    /// </summary>
    public double Naechste(Kartenort p)
    {
        var best = Reichweite;
        _lauf++;
        for (var i = (int)Math.Floor((p.X - Reichweite) / Fach); i <= (int)Math.Floor((p.X + Reichweite) / Fach); i++)
            for (var j = (int)Math.Floor((p.Z - Reichweite) / Fach); j <= (int)Math.Floor((p.Z + Reichweite) / Fach); j++)
            {
                if (!_faecher.TryGetValue((i, j), out var liste)) continue;
                foreach (var n in liste)
                {
                    if (_gesehen[n] == _lauf) continue;
                    _gesehen[n] = _lauf;
                    best = Math.Min(best, Kartengeometrie.AbstandStrecke(p, _strecken[n].A, _strecken[n].B));
                }
            }
        return best;
    }
}
