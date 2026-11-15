using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Buerger;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Klappbrücke über den Rhein (Köln, Entscheidung des Nutzers vom 15.11.2026, Datenversion 90)
//
//  In der Mitte der Rheinbrücke liegt eine doppelte Klappbrücke: Zwei Klappen schwingen auf, damit Schiffe mit Mast hindurchkommen.
//  Wann sie offen ist, folgt allein aus dem Fahrplan des Zierschiffs (Zierfahrplan, eine reine Funktion der Spielminute): Kommt das Schiff
//  von der Seite der Brücke zum Hafen, ist sie die letzten <c>oeffnungMinuten</c> vor seiner Ankunft am Kai offen; legt es zur Seite der
//  Brücke ab, die ersten <c>oeffnungMinuten</c> nach dem Ablegen. Welche Seite das ist, sagt die Lage der Brücke am Fluss: liegt sie
//  flussauf vom Hafen (Köln: die Brücke bei z = 8, der Hafen bei z = −54, der Rhein fließt nach Norden), öffnet sie für Schiffe von und
//  nach flussauf. Das Zierschiff fährt nur, solange der Schiffshandel ruht und der Hafen offen ist (nicht im Winter, nicht bei Eis) –
//  sonst bleibt die Brücke zu.
//
//  Einzige Wirkung: Ein Karren, dessen Fahrt über diese Brücke führt und der sie erreicht, während sie offen ist, wartet, bis sie zugeht
//  (Karren.BrueckeWartenAb/-Bis, im Spielstand). Kein Zoll, nichts sonst. Ohne Zufall, ohne Uhrzeit: gleiche Minute, gleiche Antwort.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Eine Öffnung der Klappbrücke: von Minute <see cref="Von"/> (seit Spielbeginn) bis vor <see cref="Bis"/>.</summary>
public readonly record struct Brueckenoeffnung(long Von, long Bis)
{
    public bool Enthaelt(long minute) => minute >= Von && minute < Bis;
}

/// <summary>Die Klappbrücke (siehe Kopf der Datei). Die Darstellung liest nur <see cref="IstOffen"/>, <see cref="Oeffnung"/> und <see cref="NaechsteOeffnung"/>.</summary>
public sealed class Klappbruecke
{
    private readonly Stadtwirtschaft _w;
    private (Stadtkarte Karte, IReadOnlyList<Wasserquerung> Bruecken, bool? Flussauf)? _lage;

    internal Klappbruecke(Stadtwirtschaft w) => _w = w;

    /// <summary>Die Werte (<c>logistik.klappbruecke</c>; null: keine Klappbrücke, wie vor Datenversion 90).</summary>
    public KlappbrueckeDaten? Daten => _w.Daten.Logistik.Klappbruecke;

    /// <summary>Die Brücken der Karte über das Gewässer der Klappbrücke (an Karrenstraßen; Köln: die Rheinbrücke). Leer ohne Daten oder Karte.</summary>
    public IReadOnlyList<Wasserquerung> Bruecken => Lage()?.Bruecken ?? Array.Empty<Wasserquerung>();

    /// <summary>
    /// Gibt es die Klappbrücke? Daten, eine Brücke über ihr Gewässer und ein Hafen an demselben Fluss, dessen Schiffe unter ihr hindurch
    /// müssen (die Brücke liegt flussauf oder flussab vom Liegeplatz).
    /// </summary>
    public bool Gibt => Lage() is { Bruecken.Count: > 0, Flussauf: not null };

    /// <summary>Ist die Klappbrücke zur Minute <paramref name="minute"/> (seit Spielbeginn) offen?</summary>
    public bool IstOffen(long minute) => Oeffnung(minute) is not null;

    /// <summary>Die Öffnung, in der die Minute liegt – null, wenn die Brücke dann zu ist.</summary>
    public Brueckenoeffnung? Oeffnung(long minute)
    {
        if (!Gibt) return null;
        var fenster = Math.Max(0, minute / Zierfahrplan.Fenster);
        // Eine Öffnung liegt ganz in ihrem Fenster (Prüfung der Daten), darum genügt das Fenster der Minute.
        foreach (var o in OeffnungenIm(fenster))
            if (o.Enthaelt(minute)) return o;
        return null;
    }

    /// <summary>
    /// Die Öffnung, in der die Minute liegt, oder die nächste danach – null, wenn es keine Klappbrücke gibt oder in den nächsten
    /// fünf Spieljahren keine kommt (Winter, Schiffshandel läuft).
    /// </summary>
    public Brueckenoeffnung? NaechsteOeffnung(long minute)
    {
        if (!Gibt) return null;
        var start = Math.Max(0, minute / Zierfahrplan.Fenster);
        for (var f = start; f < start + 10; f++)
            foreach (var o in OeffnungenIm(f))
                if (o.Bis > minute) return o;
        return null;
    }

    /// <summary>Die Öffnungen im Fenster <paramref name="fenster"/> des Zierfahrplans (keine, eine oder zwei; nach der Zeit geordnet).</summary>
    public IReadOnlyList<Brueckenoeffnung> OeffnungenIm(long fenster)
    {
        if (Lage() is not { Flussauf: { } flussauf } || Daten is not { } d || !_w.Schiffahrt.Ruht) return Array.Empty<Brueckenoeffnung>();
        var anfang = Zierfahrplan.Anfang(fenster);
        var (ankunft, liegen, vonAuf, nachAuf) = Zierfahrplan.Streuung(fenster);
        var liste = new List<Brueckenoeffnung>(2);
        // Schiffe kommen nur, solange der Hafen offen ist (gleiche Regel wie Schiffahrt.HafenOffen, zur Minute der Öffnung)
        if (vonAuf == flussauf && SchiffFaehrt(anfang + ankunft - d.OeffnungMinuten))
            liste.Add(new Brueckenoeffnung(anfang + ankunft - d.OeffnungMinuten, anfang + ankunft));
        var ablegen = anfang + ankunft + liegen;
        if (nachAuf == flussauf && SchiffFaehrt(ablegen))
            liste.Add(new Brueckenoeffnung(ablegen, ablegen + d.OeffnungMinuten));
        return liste;
    }

    /// <summary>Fährt zu dieser Minute ein Schiff? Der Hafen ist offen: nicht im Winter (mit Jahreszeiten), der Fluss nicht zugefroren.</summary>
    private bool SchiffFaehrt(long minute)
    {
        var zeit = new Spielzeit(Math.Max(0, minute));
        return !_w.Jahreszeiten.ZugefrorenUm(zeit) && !(_w.Jahreszeiten.IstAktiv && zeit.Jahreszeit == Jahreszeit.Winter);
    }

    /// <summary>
    /// Wo und wie lange ein Karren an der Klappbrücke wartet, wenn er zur Minute <paramref name="abfahrt"/> aufbricht und
    /// <paramref name="fahrzeit"/> Minuten fährt (ohne Warten): Er erreicht die Brücke nach dem Anteil der Fahrt bis zu ihrem ersten Stück
    /// (Fahrminuten wie die Logistik: Länge, Steigung, Belag, Schlamm, Furten) und wartet, wenn sie dann offen ist, bis sie zugeht.
    /// Null: Die Fahrt führt nicht über die Brücke oder sie ist dann zu.
    /// </summary>
    internal Brueckenoeffnung? Halt(IReadOnlyList<Fahrtstueck> fahrt, long abfahrt, int fahrzeit)
    {
        if (!Gibt || ErstesStueck(fahrt) is not { } i) return null;
        var ganz = _w.Fahrminuten(fahrt, 1);
        var bis = _w.Fahrminuten(fahrt.Take(i).ToList(), 1);
        var an = abfahrt + (ganz > 0 ? (long)Math.Round(fahrzeit * bis / ganz, MidpointRounding.AwayFromZero) : 0);
        return Oeffnung(an) is { } o ? new Brueckenoeffnung(an, o.Bis) : null;
    }

    /// <summary>Führt diese Fahrt über die Klappbrücke (ein Stück auf einer ihrer <see cref="Bruecken"/>)?</summary>
    public bool FuehrtUeber(IReadOnlyList<Fahrtstueck> fahrt) => ErstesStueck(fahrt) is not null;

    /// <summary>Das erste Stück der Fahrt auf der Brücke (null: keins).</summary>
    private int? ErstesStueck(IReadOnlyList<Fahrtstueck> fahrt)
    {
        var bruecken = Bruecken;
        for (var i = 0; i < fahrt.Count; i++)
        {
            var s = fahrt[i];
            if (s.Art == Fahrbahn.Bruecke && bruecken.Any(q => q.Weg == s.Weg && s.Stelle >= q.VonStelle && s.Stelle <= q.BisStelle)) return i;
        }
        return null;
    }

    /// <summary>Brücken über das Gewässer und ob sie flussauf vom Hafen liegen (null: kein Hafen an diesem Fluss) – je Karte einmal gerechnet.</summary>
    private (Stadtkarte Karte, IReadOnlyList<Wasserquerung> Bruecken, bool? Flussauf)? Lage()
    {
        if (Daten is not { } d || _w.Karte is not { } karte) return null;
        if (_lage is { } l && ReferenceEquals(l.Karte, karte)) return l;
        var bruecken = karte.Wasserplan.Bruecken.Where(q => q.Karren && q.GewaesserName == d.Gewaesser).ToList();
        bool? flussauf = null;
        var fluss = karte.Wasser.FirstOrDefault(g => g.Name == d.Gewaesser && g.Linie is { Count: >= 2 });
        var hafen = _w.Schiffahrt.Haefen.FirstOrDefault(h => h.Fluss == d.Gewaesser);
        if (bruecken.Count > 0 && fluss is not null && hafen is not null)
        {
            // Der Fluss fließt in Zeichenrichtung: eine kleinere Stelle entlang der Linie liegt flussauf.
            var b = Stelle(fluss.Linie!, bruecken[0].Mitte);
            var h = Stelle(fluss.Linie!, hafen.Liegeplatz);
            if (b != h) flussauf = b < h;
        }
        _lage = (karte, bruecken, flussauf);
        return _lage;
    }

    /// <summary>Meter entlang der Linie bis zum Punkt der Linie, der <paramref name="p"/> am nächsten liegt.</summary>
    internal static double Stelle(IReadOnlyList<double[]> linie, Kartenort p)
    {
        var (beste, abstand, laufend) = (0.0, double.MaxValue, 0.0);
        for (var i = 0; i + 1 < linie.Count; i++)
        {
            double ax = linie[i][0], az = linie[i][1], dx = linie[i + 1][0] - ax, dz = linie[i + 1][1] - az;
            var laenge2 = dx * dx + dz * dz;
            var t = laenge2 > 0 ? Math.Clamp(((p.X - ax) * dx + (p.Z - az) * dz) / laenge2, 0, 1) : 0;
            double qx = ax + t * dx - p.X, qz = az + t * dz - p.Z;
            var a = qx * qx + qz * qz;
            var laenge = Math.Sqrt(laenge2);
            if (a < abstand) (beste, abstand) = (laufend + t * laenge, a);
            laufend += laenge;
        }
        return beste;
    }
}
