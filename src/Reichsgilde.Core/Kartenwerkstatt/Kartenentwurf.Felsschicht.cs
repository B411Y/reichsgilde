using Reichsgilde.Core.Bauen;

namespace Reichsgilde.Core.Kartenwerkstatt;

// ═════════════════════════════════════════════════════════════════════════════
//  Der Hang als eigene Schicht im Kartenentwurf (Felspinsel P4a, 10.11.2026; Rechnung in Bauen/Felsschicht.cs)
//
//  Hat kein Stück „hang“, gibt es nur ein Raster (_hoehen), wie bisher. Sobald eins „hang“ bekommt (oder eine Karte mit einem solchen Stück
//  geöffnet wird), trennt der Entwurf:
//    • _grund – die Grundhöhe: was gespeichert wird und was die Geländewerkzeuge (Pinsel, Rampe, Höhenkarte) und ihr Rückgängig ändern;
//    • _hoehen – die wirksame Höhe (Grundhöhe + Hang aller Stücke mit „hang“): was Darstellung, Prüfung und Felsraum lesen (Hoehen).
//  Die Schicht wird nach jedem Befehl, Rückgängig und Wiederholen nachgeführt (MeldungenAusloesen → SchichtAuffrischen), bevor die Änderungen
//  gemeldet werden. Neu gerechnet wird der Hang nur, wenn sich ein Stück mit „hang“ geändert hat oder die Grundhöhe in seiner Reichweite;
//  geschrieben werden nur Kacheln, in denen sich etwas ändert, und gemeldet wird nur das Rechteck, das sich wirklich geändert hat.
//  Da die Schicht aus den Stücken folgt, ist das Verschieben einer Wand mit Hang ein einziger kleiner Schritt (die Werte des Stücks) – die
//  Geschichte hält dafür keine Kacheln.
// ═════════════════════════════════════════════════════════════════════════════

public sealed partial class Kartenentwurf
{
    /// <summary>Die Grundhöhe, sobald es eine Hang-Schicht gibt (sonst null: dann ist <see cref="_hoehen"/> beides).</summary>
    private Hoehenraster? _grund;

    /// <summary>Die Punkte, die die Schicht über die Grundhöhe hebt, mit ihrer wirksamen Höhe (cm) – nur zum Nachschlagen, nie in seiner Reihenfolge gelesen.</summary>
    private Dictionary<(int I, int J), short> _schicht = new();

    /// <summary>Ein Stück mit „hang“ (oder die Karte) hat sich geändert: die Schicht ganz neu rechnen.</summary>
    private bool _schichtNeu;

    /// <summary>Hier hat sich die Grundhöhe geändert (null: nirgends).</summary>
    private Kartenrechteck? _grundGeaendert;

    /// <summary>Das Raster, das die Geländewerkzeuge ändern und das gespeichert wird.</summary>
    internal Hoehenraster Grundraster => _grund ?? _hoehen;

    /// <summary>Die gespeicherte Grundhöhe (Feld <c>hoehen</c>) – ohne Hang-Schicht dasselbe Raster wie <see cref="Hoehen"/>. Nur lesen.</summary>
    public Hoehenraster Grundhoehen => Grundraster;

    /// <summary>Gibt es eine Hang-Schicht (hatte in diesem Entwurf je ein Stück „hang“)? Dann sind <see cref="Hoehen"/> und <see cref="Grundhoehen"/> zwei Raster.</summary>
    public bool HatHangschicht => _grund is not null;

    /// <summary>Wie viele Rasterpunkte die Hang-Schicht gerade hebt.</summary>
    public int HangschichtPunkte => _schicht.Count;

    // ── Abfragen ─────────────────────────────────────────────────────────────

    /// <summary>Das Rechteck (Meter), in dem der Hang eines Stücks das Gelände ändern kann – null, wenn seine Form keinen Hang hat (P4b: nur diese Kacheln neu bauen).</summary>
    public static Kartenrechteck? FelsHangBereich(EntwurfFels f) => Felsschicht.Reichweite(f.AlsLage());

    /// <summary>
    /// Wie das wirksame Gelände aussähe, wenn die Stücke diese Lagen hätten (Hang und Ort aus der Lage) – ohne etwas zu ändern: die Rasterpunkte,
    /// deren Höhe (Meter) sich gegenüber jetzt ändern würde, nach j und i geordnet. Für die Vorschau beim Ziehen am Gizmo (P4b, zusammen mit
    /// <see cref="FelsVorschau"/>). Leer, wenn kein Stück einen Hang trägt.
    /// </summary>
    public IReadOnlyList<(int I, int J, double Hoehe)> FelsHangVorschau(IEnumerable<(EntwurfFels Fels, Felslage Lage)> lagen)
    {
        var ersatz = new Dictionary<EntwurfFels, Felslage>(ReferenceEqualityComparer.Instance);
        foreach (var (f, l) in lagen) ersatz[f] = l;
        if (_grund is null && !ersatz.Values.Any(l => l.Hang)) return Array.Empty<(int, int, double)>();
        return Unterschied(Schichtvorschau(ersatz, Array.Empty<Felslage>())).Select(p => (p.I, p.J, p.Cm / 100.0)).ToList();
    }

    /// <summary>
    /// Was „Hebung ablösen“ an diesen Stücken täte (<see cref="FelsHebungAbloesen"/>), ohne etwas zu ändern: das Rechteck, die Zahl der gesenkten
    /// Rasterpunkte und die größte Senkung (Meter). Null, wenn nichts zu senken ist.
    /// </summary>
    public (Kartenrechteck Bereich, int Punkte, double Tiefste)? FelsHebungVorschau(IEnumerable<EntwurfFels> felsen)
    {
        var liste = Geordnet(felsen).Where(f => Felsschicht.HatHang(f.Form)).ToList();
        if (liste.Count == 0) return null;
        var probe = Grundraster.Kopie();
        if (Felsschicht.Abloesen(probe, Kartengrenzen.Aussen, liste.Select(f => f.AlsLage())) is not { Bereich: { } bereich } a) return null;
        var (punkte, tiefste) = (0, 0);
        foreach (var (_, vorher, nachher) in a.Kacheln())
            for (var k = 0; k < Hoehenraster.Kachel * Hoehenraster.Kachel; k++)
            {
                var d = (vorher?[k] ?? 0) - (nachher?[k] ?? 0);
                if (d == 0) continue;
                punkte++;
                tiefste = Math.Max(tiefste, d);
            }
        return (bereich, punkte, tiefste / 100.0);
    }

    /// <summary>
    /// Die schwebenden Stücke („Invalid“ im Foliage-Werkzeug von Unreal): deren tiefster Punkt der Unterkante (wie <see cref="Felsraum.Aufsetzen"/>
    /// misst) mehr als <paramref name="toleranz"/> Meter über dem Boden liegt – etwa nachdem das Gelände unter ihnen gesenkt wurde. In der Reihenfolge
    /// des Entwurfs; <see cref="FelsEinrasten"/> setzt sie wieder auf.
    /// </summary>
    public IReadOnlyList<EntwurfFels> FelsSchwebende(double toleranz = 0.1)
    {
        var aus = new List<EntwurfFels>();
        foreach (var f in _felsen)
        {
            var l = f.AlsLage();
            var tief = l.Einsinken * Felsraum.Groesse(l).Y;
            if (f.Versatz - Felsraum.Aufsetzen(l, _hoehen) > tief + toleranz) aus.Add(f);
        }
        return aus;
    }

    // ── Befehle ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Schaltet den eigenen Hang („hang“) an Stücken an oder aus – an nur bei Formen mit Hang (<see cref="Felsschicht.HatHang"/>). Der Versatz
    /// gleicht aus, was sich am Boden unter der Mitte ändert: Die Stücke bleiben, wo sie stehen. Ein Schritt; false, wenn sich nichts ändert.
    /// <para>Ein altes Stück, dessen Hang schon fest im Gelände steht, bekäme so einen zweiten Hang darüber: Dafür ist <see cref="FelsHebungAbloesen"/>.</para>
    /// </summary>
    public bool FelsHangSetzen(IEnumerable<EntwurfFels> felsen, bool an)
    {
        FelsStrichBeenden();
        GelaendeStrichBeenden();
        var liste = Geordnet(felsen).Where(f => f.Hang != an && (!an || Felsschicht.HatHang(f.Form))).ToList();
        if (liste.Count == 0) return false;
        SchichtEinrichten();
        SchichtAuffrischen();
        var befehle = HangBefehle(liste, an);
        Schritt(befehle, an ? "mit eigenem Hang" : "ohne eigenen Hang");
        return befehle.Count > 0;
    }

    /// <summary>
    /// „Hebung ablösen“ (siehe Bauen/Felsschicht.cs): nimmt die alte, fest ins Gelände geschriebene Hebung hinter den Stücken aus der Grundhöhe und gibt
    /// ihnen den eigenen Hang („hang“) – danach folgt der Hang ihnen beim Verschieben, Drehen und Löschen, und das Gelände sieht etwa aus wie vorher.
    /// Nur Formen mit Hang; ein Schritt (Gelände und Stücke); false, wenn sich nichts ändert.
    /// </summary>
    public bool FelsHebungAbloesen(IEnumerable<EntwurfFels> felsen)
    {
        FelsStrichBeenden();
        GelaendeStrichBeenden();
        BodenStrichBeenden();
        var liste = Geordnet(felsen).Where(f => Felsschicht.HatHang(f.Form)).ToList();
        if (liste.Count == 0) return false;
        SchichtEinrichten();
        SchichtAuffrischen();
        var vorher = liste.Select(f => _hoehen.Hoehe(f.X, f.Z)).ToList();
        // die Grundhöhe ändert sich dabei sofort (wie bei Felshang.Formen); der Befehl setzt dieselben Kacheln noch einmal
        var gelaende = GelaendeBefehl("Hebung abgelöst", Felsschicht.Abloesen(_grund!, Kartengrenzen.Aussen, liste.Select(f => f.AlsLage())));
        var befehle = new List<IKartenbefehl>();
        if (gelaende is not null) befehle.Add(gelaende);
        befehle.AddRange(HangBefehle(liste, true, vorher));
        if (befehle.Count == 0) return false;
        Geschichte.Ausfuehren(new Sammelbefehl(liste.Count == 1 ? $"Hebung von {liste[0].Bezeichnung} abgelöst" : $"Hebung von {liste.Count} Felsstücken abgelöst", befehle));
        return true;
    }

    /// <summary>
    /// Die Änderungen „hang“ an/aus samt Ausgleich des Versatzes: Boden unter der Mitte nachher (Grundhöhe mit der Schicht, wie sie dann gälte) minus
    /// vorher (<paramref name="vorher"/>, sonst die wirksame Höhe jetzt).
    /// </summary>
    private List<TeilAendern> HangBefehle(List<EntwurfFels> liste, bool an, IReadOnlyList<double>? vorher = null)
    {
        var ersatz = new Dictionary<EntwurfFels, Felslage>(ReferenceEqualityComparer.Instance);
        foreach (var f in liste) ersatz[f] = f.AlsLage() with { Hang = an };
        // die Stücke bleiben, wo sie stehen: ihr Hang rechnet von dort (Versatz danach ausgeglichen)
        var nachher = Schichtvorschau(ersatz, Array.Empty<Felslage>(), new HashSet<EntwurfFels>(liste, ReferenceEqualityComparer.Instance));
        var befehle = new List<TeilAendern>();
        for (var k = 0; k < liste.Count; k++)
        {
            var f = liste[k];
            var dy = Felsschicht.Hoehe(Grundraster, nachher, f.X, f.Z) - (vorher?[k] ?? _hoehen.Hoehe(f.X, f.Z));
            if (Aenderung(f, n =>
                {
                    n.Hang = an;
                    n.Versatz = Kartenraster.Punkt(Math.Clamp(n.Versatz - dy, -Felsformen.VersatzMax, Felsformen.VersatzMax));
                }) is { } b) befehle.Add(b);
        }
        return befehle;
    }

    /// <summary>Eine Änderung der Grundhöhe als Befehl (null: es änderte sich nichts; Kacheln, die gleich blieben, fallen weg).</summary>
    private static GelaendeAendern? GelaendeBefehl(string text, Gelaendeaenderung? aenderung)
    {
        if (aenderung?.Bereich is not { } bereich) return null;
        var kacheln = aenderung.Kacheln().Where(k => !(k.Vorher ?? LeereKachel).AsSpan().SequenceEqual(k.Nachher ?? LeereKachel)).ToList();
        return kacheln.Count == 0 ? null : new GelaendeAendern(text, kacheln, bereich);
    }

    // ── Stempel mit Hang-Schicht ─────────────────────────────────────────────

    /// <summary>
    /// Für <see cref="FelsStempeln"/> mit <see cref="Felspinsel.HangSchicht"/>: Die neuen Stücke mit Hang-Form bekommen „hang“, ihr Versatz gleicht den
    /// Boden unter der Mitte aus (wie beim festen Hang). Gibt das Gelände zurück, wie es nach dem Stempel gilt (für den Boden um den Fels) – null,
    /// wenn keins der Stücke einen Hang trägt.
    /// </summary>
    private Hoehenraster? StempelMitSchicht(List<EntwurfFels> neue)
    {
        foreach (var f in neue) f.Hang = Felsschicht.HatHang(f.Form);
        if (!neue.Any(f => f.Hang)) return null;
        GelaendeStrichBeenden();
        SchichtEinrichten();
        SchichtAuffrischen();
        var vorher = neue.Select(f => _hoehen.Hoehe(f.X, f.Z)).ToList();
        var nachher = Schichtvorschau(new Dictionary<EntwurfFels, Felslage>(ReferenceEqualityComparer.Instance), neue.Select(f => f.AlsLage()).ToList());
        // die Stücke bleiben, wo die Vorschau sie zeigte (ihr Hang rechnet von dort, Schichtvorschau): der Versatz gleicht den gehobenen Boden aus
        for (var i = 0; i < neue.Count; i++)
            neue[i].Versatz = Kartenraster.Punkt(Math.Clamp(neue[i].Versatz - (Felsschicht.Hoehe(_grund!, nachher, neue[i].X, neue[i].Z) - vorher[i]),
                -Felsformen.VersatzMax, Felsformen.VersatzMax));
        // das Gelände nach dem Stempel als Kopie (der Befehl setzt die Schicht erst beim Ausführen)
        var danach = _hoehen.Kopie();
        foreach (var (i, j, cm) in Unterschied(nachher)) danach.WertSetzen(i, j, cm);
        return danach;
    }

    // ── Die Schicht nachführen ───────────────────────────────────────────────

    /// <summary>Trennt Grundhöhe und wirksame Höhe (einmal je Entwurf; vorher sind beide gleich).</summary>
    private void SchichtEinrichten() => _grund ??= _hoehen.Kopie();

    /// <summary>Ein Stück mit „hang“ kam dazu, fiel weg oder änderte sich (vorher oder nachher mit „hang“): die Schicht neu rechnen.</summary>
    private void SchichtVormerken()
    {
        SchichtEinrichten();
        _schichtNeu = true;
    }

    /// <summary>Die Grundhöhe hat sich in diesem Rechteck geändert. Ohne Schicht ist das gleich die wirksame Höhe.</summary>
    private void GrundGeaendert(Kartenrechteck bereich)
    {
        if (_grund is null)
        {
            GelaendeGeaendert(bereich);
            return;
        }
        _grundGeaendert = _grundGeaendert is { } g
            ? new Kartenrechteck(Math.Min(g.X0, bereich.X0), Math.Min(g.Z0, bereich.Z0), Math.Max(g.X1, bereich.X1), Math.Max(g.Z1, bereich.Z1)) : bereich;
    }

    /// <summary>
    /// Die Schicht aller Stücke mit „hang“ aus der Grundhöhe – einzelne Stücke mit anderer Lage (<paramref name="ersatz"/>), dazu neue (<paramref name="dazu"/>).
    /// Stücke in <paramref name="fest"/> bleiben, wo sie jetzt stehen (ihr Ursprung auf der wirksamen Höhe jetzt; danach gleicht ihr Versatz aus),
    /// die neuen dort, wo ihre Lage sie auf die wirksame Höhe jetzt setzt.
    /// </summary>
    private Dictionary<(int I, int J), short> Schichtvorschau(IReadOnlyDictionary<EntwurfFels, Felslage> ersatz, IReadOnlyList<Felslage> dazu,
        HashSet<EntwurfFels>? fest = null)
    {
        var lagen = new List<(Felslage, double?)>();
        foreach (var f in _felsen)
        {
            var l = ersatz.TryGetValue(f, out var e) ? e : f.AlsLage();
            if (l.Hang) lagen.Add((l, fest?.Contains(f) == true ? Felsraum.UrsprungY(l, _hoehen) : null));
        }
        foreach (var l in dazu)
            if (l.Hang) lagen.Add((l, Felsraum.UrsprungY(l, _hoehen)));
        return lagen.Count == 0 ? new() : Felsschicht.Rechnen(Grundraster, Kartengrenzen.Aussen, lagen);
    }

    /// <summary>Die Punkte, deren wirksame Höhe (cm) mit dieser Schicht anders wäre als jetzt – nach j und i geordnet.</summary>
    private List<(int I, int J, short Cm)> Unterschied(Dictionary<(int I, int J), short> schicht)
    {
        var aus = new List<(int I, int J, short Cm)>();
        foreach (var (i, j) in _schicht.Keys.Concat(schicht.Keys).Distinct().OrderBy(p => p.J).ThenBy(p => p.I))
        {
            var cm = schicht.TryGetValue((i, j), out var w) ? w : Grundraster.Wert(i, j);
            if (cm != _hoehen.Wert(i, j)) aus.Add((i, j, cm));
        }
        return aus;
    }

    /// <summary>
    /// Führt die wirksame Höhe nach (siehe Kopf der Datei): Hat sich ein Stück mit „hang“ geändert oder die Grundhöhe in der Reichweite eines solchen,
    /// wird die Schicht neu gerechnet; dann wird jede betroffene Kachel aus Grundhöhe und Schicht gebaut und, wenn sie anders ist, gesetzt. Mit
    /// <paramref name="melden"/> wird das geänderte Rechteck als Gelände gemeldet.
    /// </summary>
    private void SchichtAuffrischen(bool melden = true)
    {
        if (_grund is not { } grund)
        {
            (_schichtNeu, _grundGeaendert) = (false, null);
            return;
        }
        if (!_schichtNeu && _grundGeaendert is null) return;
        var geaendert = _grundGeaendert;
        var lagen = _felsen.Where(f => f.Hang).Select(f => f.AlsLage()).ToList();
        var allesNeu = _schichtNeu || (geaendert is { } g0 && lagen.Any(l => Felsschicht.Reichweite(l) is { } r && Schneiden(r, g0)));
        var neu = allesNeu ? (lagen.Count == 0 ? new() : Felsschicht.Rechnen(grund, Kartengrenzen.Aussen, lagen)) : _schicht;
        // die Kacheln, die sich ändern können: wo die Grundhöhe sich änderte, und (bei neuer Schicht) wo die alte oder die neue Schicht liegt
        var kacheln = new SortedSet<(int J, int I)>();
        if (geaendert is { } g)
        {
            var (i0, j0, i1, j1) = Hoehenraster.Punkte(g);
            var (k0, k1) = (Hoehenraster.KachelVon(i0, j0), Hoehenraster.KachelVon(i1, j1));
            for (var kj = k0.J; kj <= k1.J; kj++)
                for (var ki = k0.I; ki <= k1.I; ki++) kacheln.Add((kj, ki));
        }
        var jeKachel = new Dictionary<(int I, int J), List<(int Stelle, short Cm)>>();
        foreach (var ((i, j), cm) in neu)
        {
            var k = Hoehenraster.KachelVon(i, j);
            if (!jeKachel.TryGetValue(k, out var liste)) jeKachel[k] = liste = new();
            liste.Add((((j & (Hoehenraster.Kachel - 1)) * Hoehenraster.Kachel) | (i & (Hoehenraster.Kachel - 1)), cm));
            if (allesNeu) kacheln.Add((k.J, k.I));
        }
        if (allesNeu)
            foreach (var (i, j) in _schicht.Keys)
            {
                var k = Hoehenraster.KachelVon(i, j);
                kacheln.Add((k.J, k.I));
            }
        int? ai0 = null, aj0 = null, ai1 = null, aj1 = null;
        foreach (var (kj, ki) in kacheln)
        {
            var soll = grund.KachelKopie((ki, kj)) ?? new short[Hoehenraster.Kachel * Hoehenraster.Kachel];
            if (jeKachel.TryGetValue((ki, kj), out var punkte))
                foreach (var (stelle, cm) in punkte) soll[stelle] = cm;
            var ist = _hoehen.KachelKopie((ki, kj));
            var erste = true;
            for (var s = 0; s < soll.Length; s++)
            {
                if (soll[s] == (ist?[s] ?? 0)) continue;
                var (pi, pj) = (ki * Hoehenraster.Kachel + (s & (Hoehenraster.Kachel - 1)), kj * Hoehenraster.Kachel + s / Hoehenraster.Kachel);
                (ai0, aj0, ai1, aj1) = (Math.Min(ai0 ?? pi, pi), Math.Min(aj0 ?? pj, pj), Math.Max(ai1 ?? pi, pi), Math.Max(aj1 ?? pj, pj));
                erste = false;
            }
            if (erste) continue; // die Kachel ist schon so
            _hoehen.KachelSetzen((ki, kj), Array.TrueForAll(soll, v => v == 0) ? null : soll);
        }
        (_schicht, _schichtNeu, _grundGeaendert) = (neu, false, null);
        if (ai0 is null) return;
        var bereich = new Kartenrechteck(ai0.Value * Hoehenraster.Abstand, aj0!.Value * Hoehenraster.Abstand, ai1!.Value * Hoehenraster.Abstand, aj1!.Value * Hoehenraster.Abstand);
        if (melden) GelaendeGeaendert(bereich);
        else _gelaende.Veraltet(bereich);
    }

    /// <summary>Berühren oder überdecken sich zwei Rechtecke (der Rand zählt mit)?</summary>
    private static bool Schneiden(Kartenrechteck a, Kartenrechteck b) => a.X0 <= b.X1 && b.X0 <= a.X1 && a.Z0 <= b.Z1 && b.Z0 <= a.Z1;
}
