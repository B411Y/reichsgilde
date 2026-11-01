using Godot;
using Reichsgilde.Core.Buerger;

namespace Reichsgilde;

/// <summary>
/// Markt v3 (Teil der <c>partial class Stadtansicht</c>, zu Stadtansicht.Markt.cs und Stadtansicht.Passanten.cs; Nutzer, 1.11.2026): Auf dem Marktplatz
/// laufen die Leute nicht mehr auf festen Linien, sondern frei über die Fläche.
///   • <b>Freie Fläche</b> (<see cref="Marktflaeche"/>): das Pflaster aus der Karte ohne Stände, Brunnen, Bänke und Kreuz. Jeder Weg über den Platz
///     wird darauf gesucht und gerade gezogen, mit einem Zwischenpunkt je Person neben der Geraden (die Leute fächern sich auf). Ausweichen hat dort
///     so viel Platz, wie bis zum nächsten Hindernis frei ist (statt der Wegbreite).
///   • <b>Bummeln</b> (<see cref="Marktbummel"/>): Wer am Markt ist, geht von Ziel zu Ziel (Stand, Gespräch, Brunnen, Brunnenrand, Bank, Schauen), jede
///     Pause anders lang. Ein Gang über den Platz ist ein Weg wie jeder andere (<see cref="Laufen"/>, mit Spur und Ausweichen).
///   • <b>Gespräche</b>: Gruppen bis vier, zwei stehen sich gegenüber; geredet wird erst zu zweit, allein schaut man sich um und geht nach einer Weile weiter.
///   • <b>Brunnen</b>: Wasser holen (bücken), am Rand lehnen, schauen (gesessen wird nur auf Bänken); nur wenige gehen ein Stück um ihn herum, in beide Richtungen.
/// Mit <c>--passantenmarkt=v2</c> (und <c>--passantenspur=v1|alt</c>) bleibt der Markt wie vorher: feste Stellen, Rundgang, Wege um Kreise.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Startoption <c>--passantenmarkt=v2</c>: der Markt wie vor v3 (zum Vergleichen).</summary>
    private static readonly bool MarktV2 = SpurV1 || Wasserarten.Startoption("passantenmarkt") is "v2";

    /// <summary>So viel Abstand (Meter) hält die Mitte eines Weges über den Platz zu jedem Hindernis: Körper (bis 0,32 m), Spur rechts davon (bis 0,75 m) und Luft.</summary>
    private const float FlaecheFrei = 1.1f;

    /// <summary>Die Stände mit Lage (samt Maßstab des Platzes) und Form – daraus die Umrisse der freien Fläche.</summary>
    private readonly List<(MarktplatzAnsicht Platz, Transform3D Lage, string Form)> _standUmrisse = new();

    /// <summary>Ein Aufenthalt auf dem Markt (für <see cref="StreckenEnde"/>, das nur den Ort braucht).</summary>
    private static readonly Aufenthalt MarktAufenthalt = new(Stadtort.Markt, null, 0, null);

    /// <summary>Die echte Zeit (Sekunden, 0 in der Pause) dieses Bildes – für das Drehen der Stehenden.</summary>
    private double _bildSekunden;

    /// <summary>Die Stellen jeder Gesprächsgruppe in der Reihenfolge, in der sie sich füllen (einmal gesammelt).</summary>
    private Dictionary<int, List<int>>? _gruppenStellen;

    // ═════════════════════════════════════════════════════════════════════════
    //  Aufbau: Fläche, Gruppen, Brunnenrand, Schaustellen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Baut für Markt v3 die freie Fläche des Platzes (Hindernisse aus dem Modell <c>marktplatz.glb</c> und den Ständen, Maße aus
    /// <c>tools/modelle/markt.py</c>), die Gesprächsgruppen zu viert um die Mitten der Plauderrunden, Plätze zum Anlehnen am Brunnenrand und Stellen zum Schauen.
    /// </summary>
    private void MarktV3Aufbauen(MarktplatzAnsicht platz, Dictionary<string, Transform3D> anker, List<(int Gruppe, Vector3 Ort)> gruppen)
    {
        var lage = platz.Lage;
        var m = lage.Basis.X.Length();
        var achse = lage.Basis.X / m;
        var mitte = lage.Origin;
        var flaeche = new Marktflaeche(mitte.X, mitte.Z, achse.X, achse.Z, platz.Halb);
        Vector3 Im(float x, float y, float z) => lage * new Vector3(x, y, z);
        // Brunnen: Becken mit Rand (Halbmesser 1,98 m im Modell), dazu der Eimer
        platz.Brunnen = new Vector3(mitte.X, 0, mitte.Z);
        platz.BrunnenRadius = 1.98f * m;
        flaeche.Kreis(mitte.X, mitte.Z, platz.BrunnenRadius);
        var eimer = Im(1.7f, 0, 1.5f);
        flaeche.Kreis(eimer.X, eimer.Z, 0.22f * m);
        // Marktkreuz: Sockel 1,4 × 1,4 m
        var kreuz = Im(-10.4f, 0, 10.4f);
        flaeche.Rechteckig(new Marktflaeche.Rechteck(kreuz.X, kreuz.Z, achse.X, achse.Z, 0.72f * m, 0.72f * m));
        // Bänke: 2,4 × 0,42 m, je zwischen zwei Sitzankern
        for (var n = 1; anker.TryGetValue($"Anker_Sitz{2 * n - 1}", out var a) && anker.TryGetValue($"Anker_Sitz{2 * n}", out var b); n++)
        {
            var bm = (a.Origin + b.Origin) / 2;
            var ba = new Vector3(b.Origin.X - a.Origin.X, 0, b.Origin.Z - a.Origin.Z).Normalized();
            flaeche.Rechteckig(new Marktflaeche.Rechteck(bm.X, bm.Z, ba.X, ba.Z, 1.25f * m, 0.26f * m));
        }
        // Stände: Tisch mit Pfosten 2,9 m breit, hinten 0,8 m, vorn 1,0 m (Tischkante 0,7, angelehnter Mühlstein); der Handkarren mit Rädern und Deichsel
        foreach (var (standPlatz, standLage, form) in _standUmrisse)
        {
            if (standPlatz != platz) continue;
            var ux = standLage.Basis.X;
            var (su, sv) = (ux.Length(), standLage.Basis.Z.Length());
            ux /= su;
            void Umriss(float x, float z, float hu, float hv)
            {
                var c = standLage * new Vector3(x, 0, z);
                flaeche.Stand(new Marktflaeche.Rechteck(c.X, c.Z, ux.X, ux.Z, hu * su, hv * sv));
            }
            if (form == "Stand_Karren") { Umriss(0, 0, 1.35f, 0.92f); Umriss(-1.83f, 0, 0.55f, 0.55f); }
            else Umriss(0, 0.1f, 1.45f, 0.9f); // vorn bis 1,0 m: der angelehnte Mühlstein der Steinauslage
        }
        platz.Flaeche = flaeche;

        // Gesprächsgruppen: um die Mitte jeder Plauderrunde vier Plätze, in der Reihenfolge, in der sie sich füllen (zwei gegenüber, dann dazwischen)
        foreach (var gruppe in gruppen.GroupBy(g => g.Gruppe).OrderBy(g => g.Key))
        {
            var gm = gruppe.Aggregate(Vector3.Zero, (s, g) => s + g.Ort) / gruppe.Count();
            var anfang = 0.4f * gruppe.Key;
            foreach (var viertel in Marktbummel.Gruppenfolge)
            {
                var w = anfang + viertel * Mathf.Pi / 2;
                var ort = gm + new Vector3(Mathf.Cos(w), 0, Mathf.Sin(w)) * Marktbummel.GruppeRadius;
                var zur = gm - ort;
                _marktstellen.Add(new Marktstelle(ort, Mathf.Atan2(zur.X, zur.Z), "Reden", Gruppe: platz.Nummer * 100 + gruppe.Key) { Platz = platz, Art = Marktbummel.Art.Gruppe });
            }
        }

        // Brunnenrand: Plätze zum Anlehnen rundum (mit dem Rücken am Becken stehen, Blick nach außen), nicht vor den Wasserstellen und nicht am Eimer.
        // Gesessen wird nur auf Bänken: Der Rand ist 0,78 m hoch, die Beine hingen in der Luft (Teamleiter, 1.11.2026)
        var wasser = _marktstellen.Where(s => s.Platz == platz && s.Art == Marktbummel.Art.Brunnen).Select(s => s.Ort).ToList();
        for (var k = 0; k < 8; k++)
        {
            var w = Mathf.Pi / 8 + k * Mathf.Pi / 4;
            var aussen = Im(Mathf.Cos(w) * 2.6f, 0, Mathf.Sin(w) * 2.6f);
            if (wasser.Any(q => new Vector2(q.X - aussen.X, q.Z - aussen.Z).Length() < 1.3f * m)) continue;
            if (new Vector2(eimer.X - aussen.X, eimer.Z - aussen.Z).Length() < 1.2f * m) continue;
            var lehnen = Im(Mathf.Cos(w) * 1.98f, 0, Mathf.Sin(w) * 1.98f);
            var raus = new Vector3(lehnen.X - mitte.X, 0, lehnen.Z - mitte.Z).Normalized();
            lehnen += raus * 0.26f; // Körper (Rücken) gerade am Beckenrand
            _marktstellen.Add(new Marktstelle(new Vector3(lehnen.X, platz.Hoehe, lehnen.Z), Mathf.Atan2(raus.X, raus.Z), "Stehen") { Platz = platz, Art = Marktbummel.Art.Rand });
        }

        // Stellen zum Schauen: frei auf dem Platz, mit Abstand zu allem anderen (fest: am weitesten von den schon gewählten, höchstens zehn)
        var belegt = _marktstellen.Where(s => s.Platz == platz).Select(s => new Vector2(s.Ort.X, s.Ort.Z)).ToList();
        var kandidaten = new List<Vector2>();
        for (var u = -platz.Halb + 2.5f; u <= platz.Halb - 2.5f; u += 1.5f)
            for (var v = -platz.Halb + 2.5f; v <= platz.Halb - 2.5f; v += 1.5f)
            {
                var (x, z) = flaeche.Welt(u, v);
                if (flaeche.Abstand(x, z) < 1.4f || belegt.Any(q => q.DistanceTo(new Vector2(x, z)) < 2.2f)) continue;
                kandidaten.Add(new Vector2(x, z));
            }
        var schau = new List<Vector2>();
        while (schau.Count < 10 && kandidaten.Count > 0)
        {
            var naechster = schau.Count == 0 ? kandidaten[kandidaten.Count / 2]
                : kandidaten.OrderByDescending(c => schau.Min(s => s.DistanceTo(c))).ThenBy(c => c.X).ThenBy(c => c.Y).First();
            if (schau.Count > 0 && schau.Min(s => s.DistanceTo(naechster)) < 3f) break;
            schau.Add(naechster);
            kandidaten.Remove(naechster);
        }
        for (var i = 0; i < schau.Count; i++)
        {
            var ort = new Vector3(schau[i].X, platz.Hoehe, schau[i].Y);
            var zum = new Vector3(mitte.X - ort.X, 0, mitte.Z - ort.Z);
            var blick = Mathf.Atan2(zum.X, zum.Z) + (i % 3 - 1) * 0.7f; // zum Brunnen, mal etwas daneben (auf die Stände)
            _marktstellen.Add(new Marktstelle(ort, blick, "Stehen") { Platz = platz, Art = Marktbummel.Art.Schauen });
        }
        GD.Print($"Markt {platz.Name}: freie Fläche {2 * platz.Halb:0} m mit {flaeche.Hindernisse} Hindernissen, {gruppen.Select(g => g.Gruppe).Distinct().Count()} Gesprächsgruppen, " +
                 $"{_marktstellen.Count(s => s.Platz == platz && s.Art == Marktbummel.Art.Rand)} Plätze zum Anlehnen am Brunnenrand, {schau.Count} Stellen zum Schauen");
    }

    private double _personenSeit;

    /// <summary>So viele Spielminuten vor dem Losgehen wird der Weg über den Platz gesucht (oder sofort, wenn noch Zeit im Bild ist).</summary>
    private const double PlatzVorlauf = 20;

    /// <summary>
    /// So viele Millisekunden je Bild dürfen Wegsuchen höchstens kosten (neue Wege des Kerns außerhalb des Bildes, Flächen der Plätze, Bummelgänge); was nicht
    /// mehr passt, kommt im nächsten Bild dran. Eine Suche wird nie mitten abgebrochen; die erste im Bild läuft immer.
    /// </summary>
    private const double WegsucheJeBildMs = 2;
    private double _wegsucheMs;
    private int _wegsucheBild = -1;

    /// <summary>Rechnet Zeit einer Wegplanung dem Bild zu (für <see cref="WegsucheFrei"/>).</summary>
    private void WegsucheZaehlen(double ms)
    {
        if (_wegsucheBild != _figurenBild) (_wegsucheBild, _wegsucheMs) = (_figurenBild, 0);
        _wegsucheMs += ms;
    }

    /// <summary>Ist in diesem Bild noch Zeit für eine Wegsuche (Gassennetz neuer Wege des Kerns, Flächen der Plätze, Bummelgänge)?</summary>
    private bool WegsucheFrei()
    {
        if (_wegsucheBild != _figurenBild) (_wegsucheBild, _wegsucheMs) = (_figurenBild, 0);
        return _wegsucheMs < WegsucheJeBildMs;
    }

    /// <summary>
    /// Sucht für einen Weg des Kerns, dessen Teil auf einem Platz noch offen ist, den Weg über die freie Fläche (<see cref="UeberDenPlatz(IReadOnlyList{Vector3}, int)"/>).
    /// Der Plan bleibt im selben Tempo: Die Laufzeit wächst oder schrumpft mit der Länge. False, wenn in diesem Bild keine Zeit mehr war.
    /// </summary>
    private bool PlatzNachplanen(Passant p, double minuteGenau)
    {
        if (!WegsucheFrei()) return false;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var alt = p.Laenge;
        p.Pfad = UeberDenPlatz(p.Pfad, p.Nummer).ToArray();
        p.Laenge = Gassennetz.Laenge(p.Pfad);
        p.Mitte = null;
        p.PlatzOffen = false;
        if (alt > 0.01f && p.LaufEnde > p.LaufStart)
            p.LaufEnde = p.LaufStart + (p.LaufEnde - p.LaufStart) * p.Laenge / alt; // gleiches Tempo; wer schon geht, bleibt an seiner Stelle
        if (!SpurAlt && p.Pfad.Length > 0) MitteVon(p); // Bodenmeter gleich hier (zählt zur Wegsuche)
        _wegsucheMs += uhr.Elapsed.TotalMilliseconds;
        return true;
    }

    /// <summary>Berührt der Pfad das Pflaster eines Marktplatzes (ein Punkt darauf oder ein Stück hindurch)?</summary>
    private bool BeruehrtPlatz(IReadOnlyList<Vector3> pfad)
    {
        foreach (var platz in _maerkte)
        {
            if (platz.Halb <= 0) continue;
            if (pfad.Count > 0 && platz.Auf(pfad[0])) return true;
            for (var i = 0; i + 1 < pfad.Count; i++)
                if (ImPflaster(platz, pfad[i], pfad[i + 1]) is not null) return true;
        }
        return false;
    }

    /// <summary>
    /// Jedes Bild (v3): Die Belegung der Streifen verblasst; einmal je Sekunde erfährt jede Fläche, wer auf ihr steht oder sitzt (Wege machen einen Bogen um sie).
    /// </summary>
    private void MarktflaechenPflegen(double sekunden)
    {
        if (MarktV2 || _maerkte.Count == 0) return;
        foreach (var platz in _maerkte) platz.Flaeche?.Altern((float)sekunden);
        _personenSeit += sekunden;
        if (_personenSeit < 1) return;
        _personenSeit = 0;
        foreach (var platz in _maerkte)
        {
            if (platz.Flaeche is not { } f) continue;
            f.Personen(_passanten.Values.Where(q => q.Stand is not null && q.Knoten.Visible && f.Auf(q.Knoten.Position.X, q.Knoten.Position.Z, -0.5f))
                                         .Select(q => (q.Knoten.Position.X, q.Knoten.Position.Z)).ToList());
        }
    }

    /// <summary>Der Marktplatz, auf dessen Pflaster der Punkt liegt (null, wenn keiner).</summary>
    private MarktplatzAnsicht? PlatzBei(Vector3 p)
    {
        foreach (var platz in _maerkte)
            if (platz.Flaeche is { } f && f.Auf(p.X, p.Z)) return platz;
        return null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Wege über die freie Fläche
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Punkte zwischen <paramref name="von"/> und <paramref name="bis"/> (ohne diese beiden) über die freie Fläche des Platzes: gefächert je Person
    /// (<see cref="Marktflaeche.Gefaechert"/>), sonst gerade. Gibt es keinen freien Weg (Ende in einer Ecke voller Stände), die Gerade.
    /// </summary>
    private List<Vector3> FlaechenZwischenpunkte(MarktplatzAnsicht platz, Vector3 von, Vector3 bis, int nummer, int schluessel)
    {
        var punkte = new List<Vector3>();
        if (platz.Flaeche is not { } f) return punkte;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        try { return FlaechenWeg(platz, f, von, bis, nummer, schluessel, punkte); }
        finally { _flaechenZeit += uhr.Elapsed.TotalMilliseconds; _flaechenWege++; }
    }

    /// <summary>Rechenzeit (ms) und Zahl der Wege über die freie Fläche seit der letzten Zeile im Protokoll.</summary>
    private double _flaechenZeit;
    private int _flaechenWege, _bummelVersuche, _bummelGaenge;

    private List<Vector3> FlaechenWeg(MarktplatzAnsicht platz, Marktflaeche f, Vector3 von, Vector3 bis, int nummer, int schluessel, List<Vector3> punkte)
    {
        // Ein Sitz (Bank) liegt im Hindernis: Man steht nach vorn auf bzw. setzt sich von vorn, nie über die Lehne
        var (vorVon, vorBis) = (Vorpunkt(von), Vorpunkt(bis));
        var (a, b) = (vorVon ?? von, vorBis ?? bis);
        if (vorVon is { } v1) punkte.Add(v1);
        var seite = Marktbummel.Seite(nummer, schluessel, 0);
        // gibt es mit vollem Abstand keinen Weg (Ende in einer engen Ecke), dann mit knapperem – nie gerade durch Stand oder Brunnen, wenn es anders geht
        var weg = f.GefaechertGemerkt((a.X, a.Z), (b.X, b.Z), FlaecheFrei, seite) ?? f.Pfad((a.X, a.Z), (b.X, b.Z), 0.6f);
        if (weg is not null)
        {
            for (var i = 1; i + 1 < weg.Count; i++) punkte.Add(new Vector3(weg[i].X, platz.Hoehe, weg[i].Z));
            f.Belegen(weg); // wer danach den Platz quert, nimmt einen freieren Streifen
        }
        if (vorBis is { } v2) punkte.Add(v2);
        return punkte;
    }

    /// <summary>
    /// Der Punkt, über den man eine Stelle betritt und verlässt: bei einem Sitz (Bank) 0,6 m davor in Blickrichtung (man setzt sich von vorn,
    /// nie über die Lehne), bei einem Platz in einer Gesprächsgruppe 0,8 m außen (man kommt von außen dazu, nicht mitten durch die anderen). Sonst null.
    /// </summary>
    private Vector3? Vorpunkt(Vector3 p)
    {
        foreach (var s in _marktstellen)
        {
            if (Mathf.Abs(s.Ort.X - p.X) > 0.05f || Mathf.Abs(s.Ort.Z - p.Z) > 0.05f) continue;
            var weit = s.Bewegung == "Sitzen" ? 0.6f : s.Art == Marktbummel.Art.Gruppe ? -0.8f : 0;
            if (weit == 0) return null;
            return new Vector3(p.X + Mathf.Sin(s.Blick) * weit, p.Y, p.Z + Mathf.Cos(s.Blick) * weit);
        }
        return null;
    }

    /// <summary>Ein fester Schlüssel aus zwei Punkten (auf 10 cm gerundet): Derselbe Weg einer Person fächert sich immer gleich, ein anderer anders.</summary>
    private static int Wegschluessel(Vector3 a, Vector3 b) =>
        unchecked(Mathf.RoundToInt(a.X * 10) * 73856093 ^ Mathf.RoundToInt(a.Z * 10) * 19349663 ^ Mathf.RoundToInt(b.X * 10) * 83492791 ^ Mathf.RoundToInt(b.Z * 10) * 2654435);

    /// <summary>
    /// Wie weit eine Laufende von der Mittellinie ihres Weges aus (<paramref name="ort"/>) auf der Fläche nach links (≤ 0) und rechts (≥ 0) stehen darf, ohne
    /// einem Hindernis näher als ihr Körper und etwas Luft zu kommen (höchstens 2,5 m) – null, wenn sie mehr als 3 m vor dem Pflaster ist (dann gilt nur die Breite
    /// des Weges). <c>Auf</c>: auf dem Pflaster (dort gilt nur dieser Raum, davor zusätzlich die Wegbreite).
    /// </summary>
    private (float Links, float Rechts, bool Auf)? FlaechenRaum(Passant p, Vector3 ort, Vector3 rechts)
    {
        if (MarktV2) return null;
        foreach (var platz in _maerkte)
        {
            // schon 3 m vor dem Pflaster: Wer auf breiter Straße weit rechts geht, ist beim Eintreten schon so weit eingerückt, dass er keinen Stand streift
            if (platz.Flaeche is not { } f || f.Innen(ort.X, ort.Z) is var innen && innen < -3f) continue;
            var (links, rechtsRaum) = f.Seitlich(ort.X, ort.Z, rechts.X, rechts.Z, p.Radius + 0.1f, 2.5f, mitKante: innen >= 0);
            return (links, rechtsRaum, innen >= 0);
        }
        return null;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Stellen wählen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Markt v3: Sucht für einen Einwohner eine freie Stelle auf dem Platz, als sein <paramref name="zaehler"/>-tes Ziel seit der Ankunft
    /// (<see cref="Marktbummel.Waehlen"/>), nicht <paramref name="ausser"/>, und belegt sie. −1, wenn alles voll ist.
    /// </summary>
    private int BummelBelegen(int nummer, long ankunft, int zaehler, MarktplatzAnsicht? platz, int ausser = -1)
    {
        platz ??= _maerkte.FirstOrDefault();
        if (platz is null) return -1;
        var stellen = Enumerable.Range(0, _marktstellen.Count).Where(k => _marktstellen[k].Platz == platz && k != ausser && MarktstelleFrei(k)).ToList();
        if (stellen.Count == 0) return -1;
        var ki = _kiPassanten.Contains(nummer);
        var wahl = Passantenspur.Bruchteil(nummer, 7000 + zaehler * 13 + (int)(ankunft % 997));
        // Ein anderer Stand als zuletzt (sonst stünde sie nur einen Platz weiter am selben Tisch)
        var alterStand = ausser >= 0 ? _marktstellen[ausser].Stand : -1;
        foreach (var art in Marktbummel.Reihenfolge(Marktbummel.Waehlen(nummer, zaehler, ankunft)))
        {
            if (ki && art is Marktbummel.Art.Bank) continue; // der KI-Schmied kann noch nicht sitzen
            var passend = stellen.Where(k => _marktstellen[k].Art == art && (art != Marktbummel.Art.Stand || _marktstellen[k].Stand != alterStand)).ToList();
            if (passend.Count == 0) continue;
            var gewaehlt = art == Marktbummel.Art.Gruppe ? GruppenplatzWaehlen(passend, wahl, ausser) : passend[(int)(wahl * passend.Count) % passend.Count];
            if (gewaehlt < 0) continue;
            _marktBelegt[gewaehlt] = nummer;
            return gewaehlt;
        }
        return -1;
    }

    /// <summary>
    /// Ein Platz in einer Gesprächsgruppe: am liebsten bei jemandem, der allein dasteht, sonst in einer Gruppe mit zwei oder drei, sonst eine leere Gruppe
    /// (dort wartet sie, bis jemand kommt). In der Gruppe der erste freie Platz in der Reihenfolge der Plätze (zwei stehen sich gegenüber).
    /// </summary>
    private int GruppenplatzWaehlen(List<int> freie, float wahl, int ausser)
    {
        var alteGruppe = ausser >= 0 ? _marktstellen[ausser].Gruppe : -1;
        var gruppen = freie.Select(k => _marktstellen[k].Gruppe).Where(g => g != alteGruppe).Distinct().ToList();
        if (gruppen.Count == 0) return -1;
        int Belegt(int g) => GruppenStellen(g).Count(k => _marktBelegt[k] >= 0);
        var beste = gruppen.Where(g => Belegt(g) == 1).ToList();
        if (beste.Count == 0) beste = gruppen.Where(g => Belegt(g) is >= 2 and < Marktbummel.GruppeHoechstens).ToList();
        if (beste.Count == 0) beste = gruppen;
        var gruppe = beste[(int)(wahl * beste.Count) % beste.Count];
        // der freie Platz am weitesten von denen, die schon da sind (gegenüber, wenn einer steht); in einer leeren Gruppe der erste
        var plaetze = GruppenStellen(gruppe);
        var belegt = plaetze.Where(k => _marktBelegt[k] >= 0).Select(k => _marktstellen[k].Ort).ToList();
        var frei = plaetze.Where(freie.Contains).ToList();
        if (frei.Count == 0) return -1;
        if (belegt.Count == 0) return frei[0];
        var mitte = belegt.Aggregate(Vector3.Zero, (s, o) => s + o) / belegt.Count;
        return frei.OrderByDescending(k => _marktstellen[k].Ort.DistanceSquaredTo(mitte)).ThenBy(k => k).First();
    }

    /// <summary>Die Stellen einer Gesprächsgruppe in der Reihenfolge, in der sie sich füllen.</summary>
    private List<int> GruppenStellen(int gruppe)
    {
        if (_gruppenStellen is null || _gruppenStellen.Values.Sum(l => l.Count) != _marktstellen.Count(m => m.Gruppe >= 0))
            _gruppenStellen = Enumerable.Range(0, _marktstellen.Count).Where(k => _marktstellen[k].Gruppe >= 0).GroupBy(k => _marktstellen[k].Gruppe).ToDictionary(g => g.Key, g => g.ToList());
        return _gruppenStellen.TryGetValue(gruppe, out var liste) ? liste : new List<int>();
    }

    /// <summary>Wie viele außer <paramref name="nummer"/> an den Stellen dieser Gruppe angekommen sind und dort stehen; dazu die Mitte der Anwesenden.</summary>
    private (int Anzahl, Vector3 Mitte) Anwesende(int gruppe, int nummer)
    {
        var (anzahl, summe) = (0, Vector3.Zero);
        foreach (var k in GruppenStellen(gruppe))
        {
            var wer = _marktBelegt[k];
            // angekommen und steht dort (auch wer gleich geht und noch auf seinen Abmarsch wartet)
            if (wer < 0 || wer == nummer || !_passanten.TryGetValue(wer, out var q) || q.Stand is null || q.Marktstelle != k && q.AlteMarktstelle != k || !q.Knoten.Visible) continue;
            anzahl++;
            summe += q.Knoten.Position;
        }
        return (anzahl, anzahl > 0 ? summe / anzahl : Vector3.Zero);
    }

    /// <summary>Steht jemand an dieser Stelle (angekommen, nicht nur unterwegs dorthin)?</summary>
    private bool StelleBesetzt(int k) =>
        _marktBelegt[k] is var wer and >= 0 && _passanten.TryGetValue(wer, out var q) && q.Stand is not null && (q.Marktstelle == k || q.AlteMarktstelle == k) && q.Knoten.Visible;

    // ═════════════════════════════════════════════════════════════════════════
    //  Bummeln: Pause an der Stelle, dann zum nächsten Ziel
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Jedes Bild für alle, die auf dem Markt angekommen sind (Markt v3): Bei der Ankunft an einer Stelle beginnt die Pause; danach (oder wer allein an
    /// einer Gesprächsstelle zu lange wartet) geht sie zum nächsten Ziel. True, wenn sie jetzt losgeht – dann ist sie schon gestellt (<see cref="Laufen"/>).
    /// </summary>
    private bool Bummeln(Passant p, Einwohner e, int stelle, bool angekommen, double minuteGenau)
    {
        if (angekommen)
        {
            if (p.BummelAnkunft < 0) (p.BummelAnkunft, p.BummelZaehler) = ((long)minuteGenau, 0); // die erste Stelle nach dem Weg des Kerns
            var art = stelle >= 0 ? _marktstellen[stelle].Art : Marktbummel.Art.Schauen;
            (p.BummelBis, p.AnStelleSeit, p.AlleinSeit) = (minuteGenau + Marktbummel.Verweilen(art, p.Nummer, p.BummelZaehler, p.BummelAnkunft), minuteGenau, -1);
            return false;
        }
        if (stelle >= 0 && _marktstellen[stelle].Gruppe >= 0)
        {
            // Allein an der Gesprächsstelle: eine Weile warten und umschauen, dann weiter; ist jemand da, zählt die Pause erst ab dann
            var (anzahl, _) = Anwesende(_marktstellen[stelle].Gruppe, p.Nummer);
            if (anzahl > 0)
            {
                if (p.AlleinSeit >= 0) p.BummelBis = Math.Max(p.BummelBis, minuteGenau + Marktbummel.Verweilen(Marktbummel.Art.Gruppe, p.Nummer, p.BummelZaehler + 50, p.BummelAnkunft));
                p.AlleinSeit = -1;
            }
            else
            {
                if (p.AlleinSeit < 0) p.AlleinSeit = minuteGenau;
                if (minuteGenau - p.AlleinSeit > Marktbummel.Alleinwarten(p.Nummer, p.BummelZaehler)) p.BummelBis = Math.Min(p.BummelBis, minuteGenau);
            }
        }
        if (minuteGenau < p.BummelBis) return false;
        return BummelLosgehen(p, e, stelle, minuteGenau);
    }

    /// <summary>Wählt das nächste Ziel auf demselben Platz, belegt es, legt den Weg über die Fläche (vielleicht ein Stück um den Brunnen) und geht los.</summary>
    private bool BummelLosgehen(Passant p, Einwohner e, int alt, double minuteGenau)
    {
        if (!WegsucheFrei()) return false; // keine Zeit mehr in diesem Bild: im nächsten (die Pause ist dann ein Bild länger)
        var suchUhr = System.Diagnostics.Stopwatch.StartNew();
        try { return BummelGang(p, e, alt, minuteGenau); }
        finally { _wegsucheMs += suchUhr.Elapsed.TotalMilliseconds; }
    }

    private bool BummelGang(Passant p, Einwohner e, int alt, double minuteGenau)
    {
        var platz = alt >= 0 ? _marktstellen[alt].Platz : MarktFuer(e);
        // klappt es nicht, später noch einmal (und die Wartezeit allein beginnt neu, sonst versuchte sie es jedes Bild)
        _bummelVersuche++;
        if (platz?.Flaeche is not { } flaeche) { (p.BummelBis, p.AlleinSeit) = (minuteGenau + 30, minuteGenau); return false; }
        var zaehler = p.BummelZaehler + 1;
        var ziel = BummelBelegen(p.Nummer, p.BummelAnkunft, zaehler, platz, alt);
        if (ziel < 0) { (p.BummelBis, p.AlleinSeit) = (minuteGenau + 10, minuteGenau); return false; }
        MarktstelleRaeumen(alt, p.Nummer);
        p.Marktstelle = ziel;
        p.BummelZaehler = zaehler;
        var von = new Vector3(p.Knoten.Position.X, platz.Hoehe, p.Knoten.Position.Z);
        if (alt >= 0 && _marktstellen[alt].Art is Marktbummel.Art.Bank or Marktbummel.Art.Rand) von = StreckenEnde(MarktAufenthalt, e, alt, 0, minuteGenau);
        (p.Drehen, p.GrenzeLinks, p.GrenzeRechts) = (false, -3f, 3f);
        var bis = StreckenEnde(MarktAufenthalt, e, ziel, 0, minuteGenau);
        var pfad = new List<Vector3> { von };
        var art = _marktstellen[ziel].Art;
        if (Marktbummel.Kreist(art, p.Nummer, zaehler, p.BummelAnkunft) && _passanten.Values.Count(q => q.KreistAuf == platz.Nummer && q.ImBummel) < Marktbummel.KreisenHoechstens
            && KreisWeg(platz, flaeche, p.Nummer, zaehler, p.BummelAnkunft, von, bis) is { } bogen)
        {
            pfad.AddRange(bogen);
            p.KreistAuf = platz.Nummer;
        }
        else
        {
            pfad.AddRange(FlaechenZwischenpunkte(platz, von, bis, p.Nummer, Wegschluessel(von, bis)));
            p.KreistAuf = -1;
        }
        pfad.Add(bis);
        p.Pfad = pfad.ToArray();
        p.Laenge = Gassennetz.Laenge(p.Pfad);
        p.Mitte = null;
        p.VersatzStart = Vector3.Zero;
        (p.Ausweichen, p.Seitlich, p.Seite, p.Rueckstand, p.SeitRate, p.Haltezeit, p.Bremse) = (0, 0, 0, 0, 0, 0, 1);
        var sekundenJeMinute = _simulation!.Takt.RundenDauerSekunden / Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag;
        var tempo = p.Gehtempo * Marktbummel.Tempoanteil(p.Nummer, zaehler, p.BummelAnkunft);
        (p.LaufStart, p.LaufEnde) = (minuteGenau, minuteGenau + p.Laenge / Math.Max(tempo * sekundenJeMinute, 1e-3));
        p.ImBummel = true;
        _bummelGaenge++;
        Laufen(p, p.Nummer, minuteGenau, _simulation.Takt.TicksProSekunde);
        return true;
    }

    /// <summary>
    /// Der Weg mit einem Bogen um den Brunnen (ohne Anfang und Ende): über die Fläche zum Bogen, ein Stück herum (<see cref="Marktbummel.Kreisbogen"/>),
    /// dann über die Fläche zum Ziel. Null, wenn der Bogen an einem Hindernis vorbei müsste.
    /// </summary>
    private List<Vector3>? KreisWeg(MarktplatzAnsicht platz, Marktflaeche f, int nummer, int zaehler, long ankunft, Vector3 von, Vector3 bis)
    {
        var (richtung, spanne, abstand) = Marktbummel.Kreisbogen(nummer, zaehler, ankunft);
        var m = platz.Brunnen;
        var radius = platz.BrunnenRadius + abstand;
        var anfang = Mathf.Atan2(von.Z - m.Z, von.X - m.X);
        // Der Bogen endet vor dem Winkel des Ziels (sonst ginge sie danach ein Stück zurück): erst die gewürfelte Richtung, sonst die andere; bleiben keine 80°, kein Bogen
        var zielWinkel = Mathf.Atan2(bis.Z - m.Z, bis.X - m.X);
        var gewaehlt = 0;
        foreach (var r in new[] { richtung, -richtung })
        {
            var bisZiel = Mathf.PosMod(r * (zielWinkel - anfang), Mathf.Tau);
            var s = Mathf.Min(spanne, bisZiel - 0.35f);
            if (s < Mathf.DegToRad(80)) continue;
            (gewaehlt, spanne) = (r, s);
            break;
        }
        if (gewaehlt == 0) return null;
        var bogen = Marktbummel.Bogen(m.X, m.Z, radius, anfang, spanne, gewaehlt);
        if (bogen.Any(q => f.Frei(q.X, q.Z) < FlaecheFrei - 0.2f || !f.Auf(q.X, q.Z, 0.5f))) return null;
        var punkte = new List<Vector3>();
        var start = new Vector3(bogen[0].X, platz.Hoehe, bogen[0].Z);
        punkte.AddRange(FlaechenZwischenpunkte(platz, von, start, nummer, 0));
        punkte.AddRange(bogen.Select(q => new Vector3(q.X, platz.Hoehe, q.Z)));
        var ende = punkte[^1];
        punkte.AddRange(FlaechenZwischenpunkte(platz, ende, bis, nummer, 0));
        // nichts zurück auf derselben Linie (vom Bogen zum Ziel, wenn das Ziel hinter dem Anfang liegt)
        return OhneUmkehr(new List<Vector3> { von }.Concat(punkte).Append(bis).ToList()).Skip(1).SkipLast(1).ToList();
    }

    /// <summary>
    /// Blick und Bewegung der Stehenden auf dem Markt v3, jedes Bild: In der Gruppe redet nur, wer nicht allein ist, und schaut die anderen an; allein schaut
    /// man sich um. Am Brunnen erst schöpfen (bücken), dann stehen; an den Schaustellen umschauen. Die Drehung folgt weich.
    /// </summary>
    private void MarktStehen(Passant p, Figur figur, Marktstelle s, float grundblick, double minuteGenau)
    {
        var sekunden = _marktUhr; // echte Zeit: auch bei hohem Spieltempo schaut man sich ruhig um
        var blick = grundblick;
        var bewegung = s.Bewegung;
        switch (s.Art)
        {
            case Marktbummel.Art.Gruppe:
                var (anzahl, mitte) = Anwesende(s.Gruppe, p.Nummer);
                if (Marktbummel.Redet(anzahl))
                {
                    var zu = mitte - p.Knoten.Position;
                    if (zu.X * zu.X + zu.Z * zu.Z > 0.01f) blick = Mathf.Atan2(zu.X, zu.Z);
                    bewegung = "Reden";
                }
                else (blick, bewegung) = (grundblick + Marktbummel.Umschauen(p.Nummer, sekunden), "Stehen");
                break;
            case Marktbummel.Art.Schauen:
                blick = grundblick + Marktbummel.Umschauen(p.Nummer, sekunden);
                break;
            case Marktbummel.Art.Rand: // angelehnt: nur wenig umschauen, der Rücken bleibt am Becken
                blick = grundblick + Marktbummel.Umschauen(p.Nummer, sekunden, 0.35f);
                break;
            case Marktbummel.Art.Brunnen:
                if (minuteGenau - p.AnStelleSeit < Marktbummel.Schoepfen) bewegung = "Buecken";
                else blick = grundblick + Marktbummel.Umschauen(p.Nummer, sekunden, 0.5f);
                break;
        }
        // weich dorthin drehen (nach der Ankunft zügiger); wer gerade erst zu sehen ist, steht gleich richtig
        p.Knoten.Rotation = new Vector3(0, p.Drehen || p.Knoten.Visible ? WeichDrehen(p, blick, p.Drehen ? 4 : 2.5f) : blick, 0);
        BewegungSetzen(figur, bewegung);
    }

    /// <summary>Dreht die Figur ein Stück zur Blickrichtung <paramref name="ziel"/> (echte Zeit) und sagt, wohin; ist sie fast da, endet <see cref="Passant.Drehen"/>.</summary>
    private float WeichDrehen(Passant p, float ziel, float rate = 4)
    {
        var jetzt = Mathf.LerpAngle(p.Knoten.Rotation.Y, ziel, (float)Math.Min(1, _bildSekunden * rate));
        if (Mathf.Abs(Mathf.AngleDifference(jetzt, ziel)) < 0.03f) p.Drehen = false;
        return jetzt;
    }

    /// <summary>Echte Sekunden seit dem Start (ohne Pause) – die Uhr des Umschauens.</summary>
    private double _marktUhr;

    // ═════════════════════════════════════════════════════════════════════════
    //  Messung (Startoption --passantennaehe=1): allein Redende, in Ständen, um den Brunnen
    // ═════════════════════════════════════════════════════════════════════════

    private double _marktMessSekunden, _alleinSumme, _inStandSumme, _kreisSumme, _amPlatzSumme, _marktMessBilder;
    private int _inStandGemeldet;
    private long _alleinNeu, _inStandNeu, _kreisLinks, _kreisRechts, _inStandStehend, _inStandBummel, _inStandEnde;
    private readonly HashSet<int> _alleinJetzt = new(), _inStandJetzt = new();
    private readonly Dictionary<int, double> _alleinSeit = new();

    /// <summary>
    /// Zählt im Bild: wer redet, ohne dass jemand Stehendes in 2 m ist (allein redend, ab einer halben Sekunde); wessen Körper in einen Stand reicht (mehr als 5 cm); wer auf dem
    /// Platz um den Brunnen geht (bis 7,5 m vom Brunnen, Bewegung zu mehr als 80 % quer zum Brunnen, über 0,3 m/s), im Verhältnis zu allen auf dem Platz.
    /// </summary>
    private void MarktMessen(double sekunden)
    {
        if (sekunden <= 0) return;
        _marktMessSekunden += sekunden;
        _marktMessBilder++;
        var (allein, inStand) = (new HashSet<int>(), new HashSet<int>());
        var stehende = _passanten.Values.Where(q => q.Knoten.Visible && q.Stand is not null).Select(q => q.Knoten.Position).ToList();
        foreach (var (nummer, p) in _passanten)
        {
            if (!p.Knoten.Visible) { p.Messlage = null; continue; }
            var ort = p.Knoten.Position;
            var vorher = p.Messlage;
            p.Messlage = ort;
            if (!ImBild(ort)) continue;
            if (p.Figur?.Bewegung == "Reden" && !stehende.Any(q => q != ort && new Vector2(q.X - ort.X, q.Z - ort.Z).Length() < 2f)) allein.Add(nummer);
            foreach (var platz in _maerkte)
            {
                var f = platz.Flaeche ?? StandFlaeche(platz);
                if (!f.Auf(ort.X, ort.Z, -0.5f)) continue;
                if (f.Staende.Any(r => r.Abstand(ort.X, ort.Z) < p.Radius - 0.05f))
                {
                    inStand.Add(nummer);
                    if (!_inStandJetzt.Contains(nummer))
                    {
                        if (p.Stand is not null) _inStandStehend++;
                        else if (p.ImBummel) _inStandBummel++;
                        else if (p.AmRand < 1.5f) _inStandEnde++;
                        if (_inStandGemeldet++ < 12) // die ersten Fälle genau (zum Suchen der Ursache)
                            GD.Print($"[passantenmarkt] in Stand: Nummer {nummer} bei ({ort.X:0.00}|{ort.Z:0.00}), steht {p.Stand is not null}, bummelt {p.ImBummel}, " +
                                     $"Pfad {p.Pfad.Length} Punkte ({string.Join(" ", p.Pfad.Take(12).Select(q => $"{q.X:0.0}|{q.Z:0.0}"))}), am Rand {p.AmRand:0.0} m, " +
                                     $"seitlich {p.Seitlich:0.00}, Karren {p.Ausweichen:0.00}, Versatz ({p.VersatzJetzt.X:0.00}|{p.VersatzJetzt.Z:0.00}), Grenzen {p.GrenzeLinks:0.00}/{p.GrenzeRechts:0.00}");
                    }
                }
                _amPlatzSumme += 1;
                var brunnen = platz.Flaeche is null ? platz.Lage.Origin : platz.Brunnen;
                var r = new Vector2(ort.X - brunnen.X, ort.Z - brunnen.Z);
                if (vorher is not { } v || r.Length() > 7.5f || r.Length() < 0.5f) continue;
                var bewegung = new Vector2(ort.X - v.X, ort.Z - v.Z) / (float)sekunden;
                if (bewegung.Length() < 0.3f) continue;
                var quer = (r.X * bewegung.Y - r.Y * bewegung.X) / r.Length();
                if (Mathf.Abs(quer) < 0.8f * bewegung.Length()) continue;
                _kreisSumme += 1;
                if (quer > 0) _kreisLinks++; else _kreisRechts++;
            }
        }
        // allein redend zählt erst, wenn es eine halbe Sekunde anhält (wer gerade aus der Gruppe geht, ist ein Bild später erst unterwegs)
        foreach (var n in _alleinSeit.Keys.ToList()) if (!allein.Contains(n)) _alleinSeit.Remove(n);
        foreach (var n in allein) _alleinSeit[n] = _alleinSeit.GetValueOrDefault(n) + sekunden;
        var lange = allein.Where(n => _alleinSeit[n] >= 0.5).ToHashSet();
        _alleinSumme += lange.Count;
        _inStandSumme += inStand.Count;
        _alleinNeu += lange.Count(n => !_alleinJetzt.Contains(n));
        _inStandNeu += inStand.Count(n => !_inStandJetzt.Contains(n));
        _alleinJetzt.Clear();
        _alleinJetzt.UnionWith(lange);
        _inStandJetzt.Clear();
        _inStandJetzt.UnionWith(inStand);
    }

    private readonly Dictionary<MarktplatzAnsicht, Marktflaeche> _messFlaechen = new();

    /// <summary>Für die Messung mit <c>--passantenmarkt=v2</c>: die Fläche nur mit den Ständen (sonst baut sie Markt v3).</summary>
    private Marktflaeche StandFlaeche(MarktplatzAnsicht platz)
    {
        if (_messFlaechen.TryGetValue(platz, out var f)) return f;
        var lage = platz.Lage;
        var achse = lage.Basis.X.Normalized();
        f = new Marktflaeche(lage.Origin.X, lage.Origin.Z, achse.X, achse.Z, platz.Halb);
        foreach (var (standPlatz, standLage, form) in _standUmrisse)
        {
            if (standPlatz != platz) continue;
            var ux = standLage.Basis.X;
            var (su, sv) = (ux.Length(), standLage.Basis.Z.Length());
            ux /= su;
            void Umriss(float x, float z, float hu, float hv)
            {
                var c = standLage * new Vector3(x, 0, z);
                f.Stand(new Marktflaeche.Rechteck(c.X, c.Z, ux.X, ux.Z, hu * su, hv * sv));
            }
            if (form == "Stand_Karren") { Umriss(0, 0, 1.35f, 0.92f); Umriss(-1.83f, 0, 0.55f, 0.55f); }
            else Umriss(0, 0.1f, 1.45f, 0.9f); // vorn bis 1,0 m: der angelehnte Mühlstein der Steinauslage
        }
        return _messFlaechen[platz] = f;
    }

    /// <summary>Die Messzeile <c>[passantenmarkt]</c> (alle 10 Sekunden, Summen seit dem Start).</summary>
    private void MarktMelden()
    {
        var minuten = Math.Max(_marktMessSekunden, 1) / 60.0;
        var bilder = Math.Max(_marktMessBilder, 1);
        GD.Print($"[passantenmarkt] Markt {(MarktV2 ? "v2" : "v3")}: seit {_marktMessSekunden:0} s im Bild – allein redend im Mittel {_alleinSumme / bilder:0.00} " +
                 $"({_alleinNeu} Fälle, {_alleinNeu / minuten:0.0} je Minute), in Ständen im Mittel {_inStandSumme / bilder:0.00} ({_inStandNeu} Fälle, {_inStandNeu / minuten:0.0} je Minute; " +
                 $"davon stehend {_inStandStehend}, bummelnd {_inStandBummel}, auf dem Weg des Kerns nahe Anfang oder Ziel {_inStandEnde}), " +
                 $"um den Brunnen kreisend im Mittel {_kreisSumme / bilder:0.00} von {_amPlatzSumme / bilder:0.0} auf dem Platz ({100 * _kreisSumme / Math.Max(_amPlatzSumme, 1):0.0} %; " +
                 $"Bilder in die eine Richtung {_kreisLinks}, in die andere {_kreisRechts})");
    }
}
