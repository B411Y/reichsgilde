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
//  gemeldet werden. Neu gerechnet wird der Hang nur, wenn sich ein Stück mit „hang“ geändert hat oder die Grundhöhe in seiner Reichweite, und
//  dann nur im Gebiet dieser Stücke (seit P4b, Prüfer-Befund: Streuen und Pinseln am Hang hängen nicht von der Zahl der Wände ab);
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

    /// <summary>
    /// Der Stand der letzten Rechnung der Schicht (in der Reihenfolge der Stücke): je Stück mit „hang“ Lage, Ursprung und ob es einen Hang formt –
    /// daraus rechnet <see cref="SchichtAuffrischen"/> nur nach, was sich geändert hat. Null: beim nächsten Mal die ganze Schicht.
    /// </summary>
    private List<(EntwurfFels Fels, Felslage Lage, double Ursprung, double Basis, bool Aktiv)>? _schichtStand;

    /// <summary>Wie oft die Schicht ganz bzw. nur in einem Gebiet neu gerechnet wurde (für Tests und Messungen).</summary>
    internal int SchichtGanzGerechnet, SchichtTeilGerechnet;

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
    /// <see cref="FelsVorschau"/>). <paramref name="neue"/> (P4b): Lagen noch nicht gesetzter Stücke, etwa die Geister des Stempels (mit <see cref="Felslage.Hang"/>).
    /// Leer, wenn kein Stück einen Hang trägt.
    /// </summary>
    public IReadOnlyList<(int I, int J, double Hoehe)> FelsHangVorschau(IEnumerable<(EntwurfFels Fels, Felslage Lage)> lagen, IEnumerable<Felslage>? neue = null)
    {
        var ersatz = new Dictionary<EntwurfFels, Felslage>(ReferenceEqualityComparer.Instance);
        foreach (var (f, l) in lagen) ersatz[f] = l;
        var dazu = neue?.ToList() ?? new List<Felslage>();
        if (_grund is null && !ersatz.Values.Any(l => l.Hang) && !dazu.Any(l => l.Hang)) return Array.Empty<(int, int, double)>();
        return Unterschied(Schichtvorschau(ersatz, dazu)).Select(p => (p.I, p.J, p.Cm / 100.0)).ToList();
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

    /// <summary>So viele Rechtecke darf das Gebiet des Nachrechnens haben; mehr: die ganze Schicht neu.</summary>
    private const int SchichtBereicheMax = 64;

    /// <summary>
    /// Führt die wirksame Höhe nach (siehe Kopf der Datei): Neu gerechnet wird nur, was sich ändern kann (Felspinsel P4b, Prüfer-Befund) – das Gebiet
    /// aus den Reichweiten der Stücke, die dazukamen, wegfielen oder sich änderten (alte und neue Lage, auch wenn ein Stück vorgelagert wurde oder
    /// nicht mehr), dazu die geänderte Grundhöhe, wenn sie eine Reichweite trifft; liegt die Mitte eines weiteren Stücks darin (sein Ursprung kann
    /// sich ändern), kommt seine Reichweite dazu, bis sich nichts mehr ausbreitet. In diesem Gebiet wird die Schicht aus allen Stücken gerechnet, die
    /// es erreichen; die übrigen behalten ihren Ursprung. Ohne Stand (Öffnen, neue Kartengröße) oder bei sehr großem Gebiet die ganze Schicht.
    /// Dann wird jede betroffene Kachel aus Grundhöhe und Schicht gebaut und, wenn sie anders ist, gesetzt. Mit <paramref name="melden"/> wird das
    /// geänderte Rechteck als Gelände gemeldet. <paramref name="fest"/>: dieses Stück steht auf diesem Ursprung (Streuen: es bleibt, wo es gesetzt
    /// wurde; danach gleicht sein Versatz aus).
    /// </summary>
    private void SchichtAuffrischen(bool melden = true, (EntwurfFels Fels, double Ursprung)? fest = null)
    {
        if (_grund is not { } grund)
        {
            (_schichtNeu, _grundGeaendert, _schichtStand) = (false, null, null);
            return;
        }
        if (!_schichtNeu && _grundGeaendert is null && fest is null) return;
        var geaendert = _grundGeaendert;
        var karte = Kartengrenzen.Aussen;
        var hang = _felsen.Where(f => f.Hang).ToList();
        var lagen = hang.Select(f => f.AlsLage()).ToList();
        var n = hang.Count;
        double? Fest(int k) => fest is { } x && ReferenceEquals(x.Fels, hang[k]) ? x.Ursprung : null;
        Dictionary<(int I, int J), short> neu;
        double[] ursprung;
        bool[] aktiv;
        var bereiche = Schichtbereiche(grund, hang, lagen, geaendert, fest?.Fels, out aktiv, out var markiert);
        var stand = bereiche is null ? null : SchichtStandIndex();
        ursprung = hang.Select(f => stand is not null && stand.TryGetValue(f, out var s) ? s.Ursprung : 0).ToArray();
        var basis = hang.Select(f => stand is not null && stand.TryGetValue(f, out var s) ? s.Basis : 0).ToArray();
        List<int>? auswahl = null;
        Dictionary<(int I, int J), int>? sperre = null;
        double[]? teilUrsprung = null, teilBasis = null;
        if (bereiche is { Count: > 0 })
        {
            // Liegt die Mitte eines weiteren Stücks im Gebiet, wird sein Ursprung mitgerechnet; ändert er sich, kommt seine Reichweite dazu – bis sich
            // nichts mehr ausbreitet. Stücke, deren Reichweite das Gebiet erreicht, aber deren Mitte nicht darin liegt, behalten ihren Ursprung.
            var reich = lagen.Select(Felsschicht.Reichweite).ToArray();
            sperre = Felsschicht.Sperre(lagen); // aus allen Stücken: die Nummern sind ihre Stellen in der ganzen Liste
            for (var weiter = true; weiter && bereiche is not null;)
            {
                weiter = false;
                var b0 = bereiche;
                var mitte = Enumerable.Range(0, n).Select(k => !markiert[k] && MitteIn(lagen[k], b0)).ToArray();
                // Quellen: alles, was das Gebiet erreicht, dazu was die Mittelzelle eines mitgerechneten Stücks erreicht (sie liegt nicht immer ganz im Gebiet)
                var zellen = Enumerable.Range(0, n).Where(k => mitte[k]).Select(k => Mittelzelle(lagen[k])).ToList();
                auswahl = Enumerable.Range(0, n).Where(k => reich[k] is { } r && (b0.Any(b => Schneiden(r, b)) || zellen.Any(z => Schneiden(r, z)))).ToList();
                (teilUrsprung, teilBasis) = Felsschicht.Urspruenge(grund, karte,
                    auswahl.Select(k => markiert[k] ? (lagen[k], Fest(k), (double?)null) : mitte[k] ? (lagen[k], null, null) : (lagen[k], ursprung[k], basis[k])).ToList(),
                    auswahl.Select(k => aktiv[k]).ToArray(), auswahl, sperre);
                for (var a = 0; a < auswahl.Count; a++)
                {
                    var k = auswahl[a];
                    if (!mitte[k] || (teilUrsprung[a] == ursprung[k] && teilBasis[a] == basis[k])) continue;
                    markiert[k] = weiter = true;
                    if (reich[k] is { } r) bereiche.Add(r);
                }
                if (bereiche.Count > SchichtBereicheMax) bereiche = null;
            }
        }
        if (bereiche is null)
        {
            // die ganze Schicht
            SchichtGanzGerechnet++;
            (neu, ursprung, basis, aktiv) = n == 0 ? (new(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<bool>())
                : Felsschicht.RechnenMitStand(grund, karte, Enumerable.Range(0, n).Select(k => (lagen[k], Fest(k))).ToList());
        }
        else
        {
            neu = _schicht;
            if (bereiche.Count > 0)
            {
                SchichtTeilGerechnet++;
                var punkte = bereiche.Select(Hoehenraster.Punkte).ToList();
                for (var a = 0; a < auswahl!.Count; a++) (ursprung[auswahl[a]], basis[auswahl[a]]) = (teilUrsprung![a], teilBasis![a]);
                var teilLagen = auswahl.Select(k => lagen[k]).ToList();
                var teil = Felsschicht.Teilweise(grund, karte, teilLagen, teilUrsprung!, teilBasis!, auswahl.Select(k => aktiv[k]).ToArray(), bereiche, auswahl, sperre!);
                // in der Schicht selbst: das Gebiet leeren und neu setzen (der Rest bleibt, wie er ist)
                foreach (var (i0, j0, i1, j1) in punkte)
                    for (var j = j0; j <= j1; j++)
                        for (var i = i0; i <= i1; i++) neu.Remove((i, j));
                foreach (var (p, w) in teil) neu[p] = w;
            }
        }
        _schichtStand = Enumerable.Range(0, n).Select(k => (hang[k], lagen[k], ursprung[k], basis[k], aktiv[k])).ToList();
        // die Kacheln, die sich ändern können: wo die Grundhöhe sich änderte und wo neu gerechnet wurde (bzw. bei der ganzen Schicht, wo die alte oder die neue liegt)
        var kacheln = new SortedSet<(int J, int I)>();
        void KachelnIn(Kartenrechteck b)
        {
            var (i0, j0, i1, j1) = Hoehenraster.Punkte(b);
            var (k0, k1) = (Hoehenraster.KachelVon(i0, j0), Hoehenraster.KachelVon(i1, j1));
            for (var kj = k0.J; kj <= k1.J; kj++)
                for (var ki = k0.I; ki <= k1.I; ki++) kacheln.Add((kj, ki));
        }
        if (geaendert is { } g) KachelnIn(g);
        if (bereiche is not null) foreach (var b in bereiche) KachelnIn(b);
        else
            foreach (var (i, j) in _schicht.Keys.Concat(neu.Keys))
            {
                var k = Hoehenraster.KachelVon(i, j);
                kacheln.Add((k.J, k.I));
            }
        var jeKachel = new Dictionary<(int I, int J), List<(int Stelle, short Cm)>>();
        void Punkt(int i, int j, short cm)
        {
            var k = Hoehenraster.KachelVon(i, j);
            if (!jeKachel.TryGetValue(k, out var liste)) jeKachel[k] = liste = new();
            liste.Add((((j & (Hoehenraster.Kachel - 1)) * Hoehenraster.Kachel) | (i & (Hoehenraster.Kachel - 1)), cm));
        }
        if (bereiche is null)
            foreach (var ((i, j), cm) in neu) Punkt(i, j, cm);
        else // nur die Punkte der betroffenen Kacheln (nachschlagen statt die ganze Schicht durchgehen)
            foreach (var (kj, ki) in kacheln)
                for (var j = kj * Hoehenraster.Kachel; j < (kj + 1) * Hoehenraster.Kachel; j++)
                    for (var i = ki * Hoehenraster.Kachel; i < (ki + 1) * Hoehenraster.Kachel; i++)
                        if (neu.TryGetValue((i, j), out var cm)) Punkt(i, j, cm);
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

    /// <summary>
    /// Das Gebiet, in dem die Schicht neu zu rechnen ist (siehe <see cref="SchichtAuffrischen"/>), und welche Stücke ihren Ursprung neu suchen
    /// (<paramref name="markiert"/>) – null: die ganze Schicht (kein Stand oder zu viele Rechtecke). Leer: nichts zu rechnen.
    /// </summary>
    private List<Kartenrechteck>? Schichtbereiche(Hoehenraster grund, List<EntwurfFels> hang, List<Felslage> lagen, Kartenrechteck? geaendert,
        EntwurfFels? fest, out bool[] aktiv, out bool[] markiert)
    {
        var n = hang.Count;
        aktiv = Felsschicht.Aktiv(grund, lagen);
        markiert = new bool[n];
        if (_schichtStand is null || n == 0) return null;
        var stand = SchichtStandIndex();
        var reich = lagen.Select(Felsschicht.Reichweite).ToArray();
        var bereiche = new List<Kartenrechteck>();
        for (var k = 0; k < n; k++)
        {
            if (stand.TryGetValue(hang[k], out var s))
            {
                if (s.Lage == lagen[k] && s.Aktiv == aktiv[k] && !ReferenceEquals(hang[k], fest)) continue;
                if (Felsschicht.Reichweite(s.Lage) is { } alt) bereiche.Add(alt);
            }
            markiert[k] = true;
            if (reich[k] is { } r) bereiche.Add(r);
        }
        var jetzt = new HashSet<EntwurfFels>(hang, ReferenceEqualityComparer.Instance);
        foreach (var (f, lage, _, _, _) in _schichtStand)
            if (!jetzt.Contains(f) && Felsschicht.Reichweite(lage) is { } weg) bereiche.Add(weg);
        if (geaendert is { } g && reich.Any(r => r is { } x && Schneiden(x, g)))
        {
            bereiche.Add(g);
            // die Grundhöhe unter der Mitte ist der Fuß des Hangs (Felshang: basis): Auch wenn der Ursprung gleich bleibt (die Mitte liegt im Hang
            // eines anderen Stücks), ändert sich dann der ganze Hang – das Stück gilt als geändert
            var nurG = new List<Kartenrechteck> { g };
            for (var k = 0; k < n; k++)
            {
                if (markiert[k] || !MitteIn(lagen[k], nurG)) continue;
                markiert[k] = true;
                if (reich[k] is { } r) bereiche.Add(r);
            }
        }
        return bereiche.Count > SchichtBereicheMax ? null : bereiche;
    }

    /// <summary>Liegt einer der vier Rasterpunkte um die Mitte des Stücks (aus denen seine Höhe kommt) in einem der Rechtecke?</summary>
    private static bool MitteIn(Felslage l, List<Kartenrechteck> bereiche)
    {
        var mitte = Mittelzelle(l);
        return bereiche.Any(b => Schneiden(b, mitte));
    }

    /// <summary>Die Zelle (Meter) der vier Rasterpunkte um die Mitte eines Stücks.</summary>
    private static Kartenrechteck Mittelzelle(Felslage l)
    {
        var (i, j) = ((int)Math.Floor(l.X / Hoehenraster.Abstand), (int)Math.Floor(l.Z / Hoehenraster.Abstand));
        return new Kartenrechteck(i * Hoehenraster.Abstand, j * Hoehenraster.Abstand, (i + 1) * Hoehenraster.Abstand, (j + 1) * Hoehenraster.Abstand);
    }

    /// <summary>Der Stand der letzten Rechnung je Stück: Lage, Ursprung, Auswahl (nur zum Nachschlagen).</summary>
    private Dictionary<EntwurfFels, (Felslage Lage, double Ursprung, double Basis, bool Aktiv)> SchichtStandIndex()
    {
        var aus = new Dictionary<EntwurfFels, (Felslage, double, double, bool)>(ReferenceEqualityComparer.Instance);
        foreach (var (f, l, u, b, a) in _schichtStand!) aus[f] = (l, u, b, a);
        return aus;
    }

    /// <summary>Berühren oder überdecken sich zwei Rechtecke (der Rand zählt mit)?</summary>
    private static bool Schneiden(Kartenrechteck a, Kartenrechteck b) => a.X0 <= b.X1 && b.X0 <= a.X1 && a.Z0 <= b.Z1 && b.Z0 <= a.Z1;
}
