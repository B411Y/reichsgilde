using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Wasser: Flüsse, Bäche, Seen und Teiche mit ihrem Spiegel und ihrem Bett (Kartenwerkstatt, Schritt 5c)
//
//  Ein Fluss (oder Bach) ist eine Linie mit Breite je Stützpunkt; er fließt in Zeichenrichtung. Wie ein glatter Weg läuft er als weiche
//  Kurve durch alle Stützpunkte (Wegkurve, etwa alle 1 m ein Punkt); Breite und Tiefe gehen zwischen zwei Stützpunkten gleichmäßig über.
//  Sein Spiegel folgt dem Gelände: die Höhe des gespeicherten Rasters unter der Mitte, über ±8 m gemittelt, 0,4 m tiefer (Freibord) –
//  aber nie höher als weiter oben am Fluss. Wasser fließt nicht bergauf: Steigt das Gelände in Fließrichtung an, bleibt der Spiegel auf
//  seiner Höhe, und das Bett schneidet sich ein (wie ein Durchstich). Wie tief höchstens, steht in GroessterEinschnitt – die Prüfung
//  meldet ab 2 m einen Hinweis („läuft bergauf?“). Das ist eine bewusste Entscheidung: Jeder Pinselstrich ändert das Gelände, ein harter
//  Fehler würde beim Malen ständig aufleuchten; so bleibt der Fluss immer möglich, und man sieht, wo er sich eingräbt.
//  Ein See (oder Teich) ist ein Vieleck mit einem Wasserstand (ohne Angabe 0,4 m unter dem tiefsten Punkt seines Ufers) und einer Tiefe.
//
//  Das Bett: Wie das Bett der Wege liegt es in der Schicht „Boden“ (Gelaendeform), das gespeicherte Raster bleibt unverändert. Im Fluss
//  hat der Boden ein Querprofil (Parabel: am Rand der Spiegel, in der Mitte die Tiefe), daneben ein weiches Ufer (2 m + ein Fünftel der
//  Breite), das vom Spiegel ins Gelände übergeht. Im See fällt der Boden vom Ufer mit 30 % bis zur Tiefe ab, außen dasselbe weiche Ufer
//  (3 m). Unter dem Kern eines Weges oder einer Mauer bleibt das Ufer stehen (der Weg führt bis ans Wasser, eine Brücke setzt dort an);
//  das Flussbett selbst gräbt sich auch unter Wegen ein.
//
//  Abfragen für Darstellung und Prüfung: Laeufe (je Fluss die Punkte mit Breite, Tiefe, Spiegel und Richtung), Seen (Umriss, Wasserstand),
//  An(x, z) (welches Wasser, Spiegel, Strömung), IstWasser, Uferabstand, Ueberlappt (ein Umriss im Wasser). Die Tiefe am Ort (Spiegel minus
//  Boden) gibt Gelaendeform.Wassertiefe.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Punkt eines Wasserlaufs (etwa alle 1 m entlang der Mittellinie): Ort, Stelle (Meter ab dem Anfang), Breite und Tiefe (Meter),
/// Spiegel (Höhe der Wasseroberfläche) und die Fließrichtung als Einheitsvektor.
/// </summary>
public readonly record struct Wasserpunkt(Kartenort Ort, double Stelle, double Breite, double Tiefe, double Spiegel, double RichtungX, double RichtungZ);

/// <summary>Ein Fluss oder Bach, wie Darstellung und Prüfung ihn sehen (aus einem <see cref="Gewaesser"/> mit Linie).</summary>
public sealed class Wasserlauf
{
    /// <summary>Welcher Eintrag der Liste <c>wasser</c> (0 …).</summary>
    public int Nummer { get; }

    public string Name { get; }

    /// <summary>„fluss“ oder „bach“.</summary>
    public string Art { get; }

    /// <summary>Strömung in der Mitte (m/s); zum Ufer hin halb so schnell.</summary>
    public double Stroemung { get; }

    /// <summary>Die Mittellinie in Fließrichtung, etwa alle 1 m ein Punkt (die Stützpunkte sind dabei).</summary>
    public IReadOnlyList<Wasserpunkt> Punkte { get; }

    /// <summary>Für jeden Punkt: das Stück zwischen Stützpunkt i und i + 1, auf dem er liegt.</summary>
    public IReadOnlyList<int> Abschnitte { get; }

    /// <summary>
    /// So tief (Meter) liegt der Spiegel höchstens unter dem Gelände (über ±8 m gemittelt) – mehr als der Freibord heißt: Hier stieg das Gelände
    /// in Fließrichtung an, und das Bett schneidet sich ein. Dazu der Ort.
    /// </summary>
    public double GroessterEinschnitt { get; }

    public Kartenort EinschnittOrt { get; }

    /// <summary>Das Rechteck um Fluss und Ufer.</summary>
    public Kartenrechteck Bereich { get; }

    /// <summary>Die Länge in Metern.</summary>
    public double Laenge => Punkte.Count == 0 ? 0 : Punkte[^1].Stelle;

    private readonly double _breitesteHalbe;
    private readonly Dictionary<(int, int), List<int>> _raster = new();
    private const double Feld = 16;

    internal Wasserlauf(int nummer, string name, string art, double stroemung, IReadOnlyList<Wasserpunkt> punkte, IReadOnlyList<int> abschnitte,
        double einschnitt, Kartenort einschnittOrt)
    {
        (Nummer, Name, Art, Stroemung, Punkte, Abschnitte) = (nummer, name, art, stroemung, punkte, abschnitte);
        (GroessterEinschnitt, EinschnittOrt) = (einschnitt, einschnittOrt);
        _breitesteHalbe = punkte.Count == 0 ? 0 : punkte.Max(p => p.Breite / 2 + Wasserform.Uferbreite(p.Breite));
        Bereich = (Kartenrechteck.Um(punkte.Select(p => p.Ort)) ?? default).Vergroessert(_breitesteHalbe);
        for (var k = 0; k + 1 < punkte.Count; k++)
        {
            var (a, b) = (punkte[k].Ort, punkte[k + 1].Ort);
            var r = Math.Max(punkte[k].Breite, punkte[k + 1].Breite) / 2 + Wasserform.Uferbreite(Math.Max(punkte[k].Breite, punkte[k + 1].Breite));
            for (var i = (int)Math.Floor((Math.Min(a.X, b.X) - r) / Feld); i <= (int)Math.Floor((Math.Max(a.X, b.X) + r) / Feld); i++)
                for (var j = (int)Math.Floor((Math.Min(a.Z, b.Z) - r) / Feld); j <= (int)Math.Floor((Math.Max(a.Z, b.Z) + r) / Feld); j++)
                {
                    if (!_raster.TryGetValue((i, j), out var liste)) _raster[(i, j)] = liste = new List<int>();
                    liste.Add(k);
                }
        }
    }

    /// <summary>
    /// Die nächste Stelle der Mittellinie zum Punkt: Stück k (zwischen Punkt k und k + 1), Anteil t darauf und Abstand – nur, wenn der Punkt im
    /// Fluss oder seinem Ufer liegen kann (sonst null).
    /// </summary>
    internal (int K, double T, double Abstand)? Naechste(Kartenort p)
    {
        if (Punkte.Count < 2 || !_raster.TryGetValue(((int)Math.Floor(p.X / Feld), (int)Math.Floor(p.Z / Feld)), out var liste)) return null;
        var best = (K: -1, T: 0.0, Abstand: double.MaxValue);
        foreach (var k in liste)
        {
            var (a, b) = (Punkte[k].Ort, Punkte[k + 1].Ort);
            var (dx, dz) = (b.X - a.X, b.Z - a.Z);
            var l2 = dx * dx + dz * dz;
            var t = l2 == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / l2, 0, 1);
            var d = p.Abstand(new Kartenort(a.X + t * dx, a.Z + t * dz));
            if (d < best.Abstand) best = (k, t, d);
        }
        return best.K < 0 ? null : best;
    }

    /// <summary>Der Punkt auf Stück k beim Anteil t (Ort, Stelle, Breite, Tiefe, Spiegel linear, die Richtung des Stücks).</summary>
    public Wasserpunkt Bei(int k, double t)
    {
        var (a, b) = (Punkte[k], Punkte[k + 1]);
        double M(double x, double y) => x + (y - x) * t;
        return new Wasserpunkt(new Kartenort(M(a.Ort.X, b.Ort.X), M(a.Ort.Z, b.Ort.Z)), M(a.Stelle, b.Stelle), M(a.Breite, b.Breite),
            M(a.Tiefe, b.Tiefe), M(a.Spiegel, b.Spiegel), a.RichtungX, a.RichtungZ);
    }

    /// <summary>Der Punkt an der Stelle (Meter ab dem Anfang, geklemmt).</summary>
    public Wasserpunkt AnStelle(double stelle)
    {
        if (Punkte.Count == 1) return Punkte[0];
        var k = 0;
        while (k + 2 < Punkte.Count && Punkte[k + 1].Stelle < stelle) k++;
        var laenge = Punkte[k + 1].Stelle - Punkte[k].Stelle;
        return Bei(k, laenge <= 0 ? 0 : Math.Clamp((stelle - Punkte[k].Stelle) / laenge, 0, 1));
    }
}

/// <summary>Ein See oder Teich, wie Darstellung und Prüfung ihn sehen (aus einem <see cref="Gewaesser"/> mit Fläche).</summary>
public sealed class Wassersee
{
    /// <summary>Welcher Eintrag der Liste <c>wasser</c> (0 …).</summary>
    public int Nummer { get; }

    public string Name { get; }

    /// <summary>„see“ oder „teich“.</summary>
    public string Art { get; }

    public IReadOnlyList<Kartenort> Umriss { get; }

    /// <summary>Die Höhe des Spiegels in Metern (aus der Karte oder 0,4 m unter dem tiefsten Punkt des Ufers).</summary>
    public double Wasserstand { get; }

    /// <summary>Steht der Wasserstand nicht in der Karte (dann folgt er dem Gelände am Ufer)?</summary>
    public bool StandAutomatisch { get; }

    /// <summary>Die größte Tiefe (Meter).</summary>
    public double Tiefe { get; }

    /// <summary>Der tiefste Punkt des Geländes am Ufer (gespeichertes Raster) – liegt er unter dem Wasserstand, liefe der See dort über.</summary>
    public double TiefstesUfer { get; }

    public Kartenort TiefstesUferOrt { get; }

    /// <summary>Das Rechteck um See und Ufer.</summary>
    public Kartenrechteck Bereich { get; }

    internal Wassersee(int nummer, string name, string art, IReadOnlyList<Kartenort> umriss, double wasserstand, bool automatisch, double tiefe,
        double tiefstesUfer, Kartenort tiefstesUferOrt)
    {
        (Nummer, Name, Art, Umriss, Wasserstand, StandAutomatisch, Tiefe) = (nummer, name, art, umriss, wasserstand, automatisch, tiefe);
        (TiefstesUfer, TiefstesUferOrt) = (tiefstesUfer, tiefstesUferOrt);
        Bereich = (Kartenrechteck.Um(umriss) ?? default).Vergroessert(Wasserform.SeeUfer);
    }

    /// <summary>Liegt der Punkt im See?</summary>
    public bool Enthaelt(Kartenort p) => Umriss.Count >= 3 && Bereich.Enthaelt(p) && Kartengeometrie.ImVieleck(p, Umriss);

    /// <summary>Abstand zum Rand des Umrisses (Meter; innen wie außen positiv).</summary>
    public double Randabstand(Kartenort p)
    {
        var best = double.MaxValue;
        for (var i = 0; i < Umriss.Count; i++)
            best = Math.Min(best, Kartengeometrie.AbstandStrecke(p, Umriss[i], Umriss[(i + 1) % Umriss.Count]));
        return best;
    }
}

/// <summary>
/// Was an einem Ort im Wasser ist: der Fluss oder der See, die Höhe des Spiegels, die Strömung (m/s nach x und z; im See 0) und der Abstand
/// zum Ufer (Meter, nach innen).
/// </summary>
public readonly record struct Wasserstelle(Wasserlauf? Lauf, Wassersee? See, double Spiegel, double StroemungX, double StroemungZ, double Uferabstand)
{
    public string Name => Lauf?.Name ?? See?.Name ?? "";

    /// <summary>Die Nummer des Eintrags in der Liste <c>wasser</c>.</summary>
    public int Nummer => Lauf?.Nummer ?? See?.Nummer ?? -1;

    /// <summary>Wie schnell das Wasser hier fließt (m/s).</summary>
    public double Tempo => Math.Sqrt(StroemungX * StroemungX + StroemungZ * StroemungZ);
}

/// <summary>Alle Gewässer einer Karte mit Spiegel und Bett (siehe Kopf der Datei). Gehört zu einer <see cref="Gelaendeform"/>.</summary>
public sealed class Wasserform
{
    /// <summary>So tief (Meter) liegt der Spiegel unter dem Gelände, wo nichts ihn zwingt (Fluss: unter der Mitte; See: am tiefsten Ufer).</summary>
    public const double Freibord = 0.4;

    /// <summary>Über so viele Meter vor und hinter einer Stelle wird das Gelände unter einem Fluss gemittelt (Dreiecksgewicht).</summary>
    public const double Glaettung = 8;

    /// <summary>Ein See wird vom Ufer an um so viel tiefer je Meter (30 %), bis er seine Tiefe erreicht.</summary>
    public const double Seeboeschung = 0.3;

    /// <summary>Tiefe eines Sees ohne Angabe (Meter).</summary>
    public const double SeeTiefeVorgabe = 2;

    /// <summary>Breite des weichen Ufers um einen See (Meter).</summary>
    public const double SeeUfer = 3;

    /// <summary>Ab so viel Einschnitt (Meter, Spiegel unter dem gemittelten Gelände) meldet die Prüfung einen Fluss, der bergauf läuft.</summary>
    public const double Schluchtgrenze = 2;

    /// <summary>Schmaler und flacher geht es nicht (Meter).</summary>
    public const double MindestBreite = 0.5, MindestTiefe = 0.1;

    /// <summary>Breiter und tiefer auch nicht (Meter).</summary>
    public const double HoechstBreite = 300, HoechstTiefe = 20;

    /// <summary>Fließt Wasser dieser Art (Fluss, Bach) – oder steht es (See, Teich)?</summary>
    public static bool IstFliessend(string art) => art is "fluss" or "bach";

    /// <summary>Die Arten, die eine Karte kennt.</summary>
    public static readonly IReadOnlyList<string> Arten = new[] { "fluss", "bach", "see", "teich" };

    /// <summary>Breite eines Flusses ohne Angabe: Bach 3 m, Fluss 12 m.</summary>
    public static double Vorgabebreite(string art) => art == "bach" ? 3 : 12;

    /// <summary>Tiefe ohne Angabe aus der Breite: 0,15 m + ein Zehntel der Breite, 0,3 bis 5 m (Bach 3 m: 0,45 m – eine Furt; Fluss 12 m: 1,35 m).</summary>
    public static double Vorgabetiefe(double breite) => Math.Clamp(0.15 + 0.1 * breite, 0.3, 5);

    /// <summary>Strömung ohne Angabe (m/s): Bach 0,5, Fluss 1.</summary>
    public static double VorgabeStroemung(string art) => art == "bach" ? 0.5 : 1;

    /// <summary>Breite des weichen Ufers neben einem Fluss dieser Breite (Meter): 2 m + ein Fünftel der Breite.</summary>
    public static double Uferbreite(double breite) => 2 + 0.2 * breite;

    private readonly Hoehenraster _roh;
    private readonly Func<IReadOnlyList<Gewaesser>> _gewaesser;
    private (List<Wasserlauf> Laeufe, List<Wassersee> Seen)? _stand;

    /// <param name="roh">Das gespeicherte Höhenraster (für den Spiegel).</param>
    /// <param name="gewaesser">Die Gewässer der Karte – wird nach jedem <see cref="AllesVeraltet"/> neu gefragt.</param>
    public Wasserform(Hoehenraster roh, Func<IReadOnlyList<Gewaesser>> gewaesser) => (_roh, _gewaesser) = (roh, gewaesser);

    /// <summary>Gewässer oder Gelände haben sich geändert: beim nächsten Lesen alles neu.</summary>
    public void AllesVeraltet() => _stand = null;

    private (List<Wasserlauf> Laeufe, List<Wassersee> Seen) Stand => _stand ??= Rechnen();

    /// <summary>Die Flüsse und Bäche.</summary>
    public IReadOnlyList<Wasserlauf> Laeufe => Stand.Laeufe;

    /// <summary>Die Seen und Teiche.</summary>
    public IReadOnlyList<Wassersee> Seen => Stand.Seen;

    /// <summary>Hat die Karte kein Wasser?</summary>
    public bool IstLeer => Laeufe.Count == 0 && Seen.Count == 0;

    /// <summary>Die Rechtecke um jedes Gewässer mit seinem Ufer (dort ändert es den Boden).</summary>
    public IEnumerable<Kartenrechteck> Bereiche => Laeufe.Select(l => l.Bereich).Concat(Seen.Select(s => s.Bereich));

    // ── Rechnen ──────────────────────────────────────────────────────────────

    private (List<Wasserlauf>, List<Wassersee>) Rechnen()
    {
        var laeufe = new List<Wasserlauf>();
        var seen = new List<Wassersee>();
        var liste = _gewaesser();
        for (var n = 0; n < liste.Count; n++)
        {
            var g = liste[n];
            if (g.IstLinie)
            {
                if (g.Linie is { Count: >= 2 } l && l.All(p => p is { Length: >= 2 }) && LaufRechnen(n, g) is { } lauf) laeufe.Add(lauf);
            }
            else if (g.Flaeche is { Count: >= 3 } f && f.All(p => p is { Length: >= 2 }))
                seen.Add(SeeRechnen(n, g));
        }
        return (laeufe, seen);
    }

    /// <summary>Breite eines Stützpunkts: [x, z, breite] – sonst die des Flusses, sonst die Vorgabe der Art.</summary>
    public static double PunktBreite(Gewaesser g, double[] p) =>
        p.Length >= 3 && p[2] > 0 ? p[2] : g.Breite > 0 ? g.Breite : Vorgabebreite(g.Art);

    /// <summary>Tiefe eines Stützpunkts: [x, z, breite, tiefe] – sonst die des Flusses, sonst aus der Breite.</summary>
    public static double PunktTiefe(Gewaesser g, double[] p) =>
        p.Length >= 4 && p[3] > 0 ? p[3] : g.Tiefe is > 0 ? g.Tiefe.Value : Vorgabetiefe(PunktBreite(g, p));

    private Wasserlauf? LaufRechnen(int nummer, Gewaesser g)
    {
        var stuetz = g.Linie!;
        var verlauf = Wegkurve.Verlauf(stuetz.Select(p => new[] { p[0], p[1] }).ToList(), glatt: true);
        if (verlauf.Punkte.Count < 2) return null;
        var orte = verlauf.Punkte.Select(Kartenort.Aus).ToList();
        // Stelle je Punkt und Anteil im Stück zwischen zwei Stützpunkten (die Stützpunkte liegen auf dem Verlauf)
        var stelle = new double[orte.Count];
        for (var k = 1; k < orte.Count; k++) stelle[k] = stelle[k - 1] + orte[k - 1].Abstand(orte[k]);
        var anfangVon = new Dictionary<int, (double Anfang, double Ende)>();
        for (var k = 0; k < orte.Count; k++)
        {
            var a = verlauf.Abschnitte[k];
            anfangVon[a] = anfangVon.TryGetValue(a, out var x) ? (Math.Min(x.Anfang, stelle[k]), Math.Max(x.Ende, stelle[k])) : (stelle[k], stelle[k]);
        }
        // das Stück endet am nächsten Stützpunkt (dem ersten Punkt des nächsten Stücks)
        double Anteil(int k)
        {
            var a = verlauf.Abschnitte[k];
            var anfang = anfangVon[a].Anfang;
            var ende = anfangVon.TryGetValue(a + 1, out var n) ? n.Anfang : anfangVon[a].Ende;
            return ende > anfang ? Math.Clamp((stelle[k] - anfang) / (ende - anfang), 0, 1) : 0;
        }
        // Gelände unter der Mitte, gemittelt
        var roh = orte.Select(p => _roh.Hoehe(p.X, p.Z)).ToArray();
        var glatt = new double[orte.Count];
        var unten = 0;
        for (var k = 0; k < orte.Count; k++)
        {
            while (stelle[unten] < stelle[k] - Glaettung) unten++;
            double summe = 0, gewichte = 0;
            for (var m = unten; m < orte.Count && stelle[m] <= stelle[k] + Glaettung; m++)
            {
                var w = 1 - Math.Abs(stelle[m] - stelle[k]) / Glaettung;
                if (w <= 0) continue;
                (summe, gewichte) = (summe + w * roh[m], gewichte + w);
            }
            glatt[k] = gewichte > 0 ? summe / gewichte : roh[k];
        }
        var punkte = new List<Wasserpunkt>(orte.Count);
        var spiegel = double.MaxValue;
        var (einschnitt, einschnittOrt) = (0.0, orte[0]);
        for (var k = 0; k < orte.Count; k++)
        {
            spiegel = Math.Min(spiegel, glatt[k] - Freibord);
            var unterGelaende = glatt[k] - spiegel;
            if (unterGelaende > einschnitt) (einschnitt, einschnittOrt) = (unterGelaende, orte[k]);
            var a = Math.Min(verlauf.Abschnitte[k], stuetz.Count - 2);
            var t = Anteil(k);
            var (pa, pb) = (stuetz[a], stuetz[a + 1]);
            var breite = PunktBreite(g, pa) + (PunktBreite(g, pb) - PunktBreite(g, pa)) * t;
            var tiefe = PunktTiefe(g, pa) + (PunktTiefe(g, pb) - PunktTiefe(g, pa)) * t;
            var (von, nach) = k + 1 < orte.Count ? (orte[k], orte[k + 1]) : (orte[k - 1], orte[k]);
            var laenge = von.Abstand(nach);
            var (rx, rz) = laenge > 0 ? ((nach.X - von.X) / laenge, (nach.Z - von.Z) / laenge) : (0.0, 1.0);
            punkte.Add(new Wasserpunkt(orte[k], stelle[k], Math.Max(breite, MindestBreite), Math.Max(tiefe, MindestTiefe), spiegel, rx, rz));
        }
        var tempo = g.Stroemung is { } s && s >= 0 ? s : VorgabeStroemung(g.Art);
        return new Wasserlauf(nummer, g.Name, g.Art, tempo, punkte, verlauf.Abschnitte, einschnitt, einschnittOrt);
    }

    private Wassersee SeeRechnen(int nummer, Gewaesser g)
    {
        var umriss = g.Flaeche!.Select(Kartenort.Aus).ToList();
        // der tiefste Punkt des Ufers: die Kanten alle 1 m abgetastet
        var (tiefst, ort) = (double.MaxValue, umriss[0]);
        foreach (var (a, b) in Kartengeometrie.Strecken(umriss))
        {
            var teile = Math.Max(1, (int)Math.Ceiling(a.Abstand(b)));
            for (var i = 0; i < teile; i++)
            {
                var p = new Kartenort(a.X + (b.X - a.X) * i / teile, a.Z + (b.Z - a.Z) * i / teile);
                var h = _roh.Hoehe(p.X, p.Z);
                if (h < tiefst) (tiefst, ort) = (h, p);
            }
        }
        var stand = g.Wasserstand ?? tiefst - Freibord;
        var tiefe = g.Tiefe is > 0 ? g.Tiefe.Value : SeeTiefeVorgabe;
        return new Wassersee(nummer, g.Name, g.Art, umriss, stand, g.Wasserstand is null, Math.Max(tiefe, MindestTiefe), tiefst, ort);
    }

    // ── Abfragen ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Das Wasser am Ort – null an Land. Liegt der Ort in zwei Gewässern (eine Mündung), gilt das mit dem tieferen Spiegel. Die Strömung zeigt
    /// in Fließrichtung, in der Mitte mit der Strömung des Flusses, zum Ufer hin halb so schnell.
    /// </summary>
    public Wasserstelle? An(double x, double z)
    {
        var p = new Kartenort(x, z);
        Wasserstelle? best = null;
        foreach (var l in Laeufe)
        {
            if (!l.Bereich.Enthaelt(p) || l.Naechste(p) is not ({ } k, { } t, { } d)) continue;
            var w = l.Bei(k, t);
            var halb = w.Breite / 2;
            if (d > halb) continue;
            var tempo = l.Stroemung * (1 - 0.5 * (d / halb) * (d / halb));
            if (best is null || w.Spiegel < best.Value.Spiegel)
                best = new Wasserstelle(l, null, w.Spiegel, w.RichtungX * tempo, w.RichtungZ * tempo, halb - d);
        }
        foreach (var s in Seen)
            if (s.Enthaelt(p) && (best is null || s.Wasserstand < best.Value.Spiegel))
                best = new Wasserstelle(null, s, s.Wasserstand, 0, 0, s.Randabstand(p));
        return best;
    }

    /// <summary>Liegt der Ort im Wasser (Fluss, Bach, See, Teich)?</summary>
    public bool IstWasser(double x, double z) => An(x, z) is not null;

    /// <summary>Liegt der Ort in einem See oder Teich?</summary>
    public bool ImSee(double x, double z) => Seen.Any(s => s.Enthaelt(new Kartenort(x, z)));

    /// <summary>Die Höhe des Spiegels am Ort – null an Land.</summary>
    public double? Spiegel(double x, double z) => An(x, z)?.Spiegel;

    /// <summary>Die Strömung am Ort (m/s nach x und z, Tempo) – an Land und im See (0, 0, 0).</summary>
    public (double X, double Z, double Tempo) Stroemung(double x, double z) =>
        An(x, z) is { } w ? (w.StroemungX, w.StroemungZ, w.Tempo) : (0, 0, 0);

    /// <summary>
    /// Abstand zum nächsten Wasser (Meter): 0 im Wasser, <see cref="double.MaxValue"/> ohne Wasser in der Nähe. Ein Fluss wird nur im Umkreis
    /// seines Ufers gefunden (höchstens <paramref name="hoechstens"/> Meter weit gesucht).
    /// </summary>
    public double Uferabstand(double x, double z, double hoechstens = 50)
    {
        var p = new Kartenort(x, z);
        var best = double.MaxValue;
        foreach (var l in Laeufe)
        {
            if (!l.Bereich.Vergroessert(hoechstens).Enthaelt(p)) continue;
            // in der Nähe der Linie die genaue Stelle; weiter weg je Punkt der Linie (selten gefragt)
            if (l.Naechste(p) is ({ } k, { } t, { } d))
            {
                best = Math.Min(best, Math.Max(0, d - l.Bei(k, t).Breite / 2));
                continue;
            }
            foreach (var w in l.Punkte) best = Math.Min(best, Math.Max(0, w.Ort.Abstand(p) - w.Breite / 2));
        }
        foreach (var s in Seen)
            best = Math.Min(best, s.Enthaelt(p) ? 0 : s.Randabstand(p));
        return best > hoechstens ? double.MaxValue : best;
    }

    /// <summary>
    /// Überdeckt ein Umriss (Vieleck, z. B. die Grundfläche eines Gebäudes) Wasser? Dann das Gewässer und ein Ort im Wasser – sonst null.
    /// Ein Fluss wird entlang seiner Mittellinie (alle 1 m) gegen den Umriss geprüft, ein See als Vieleck.
    /// </summary>
    public (string Name, Kartenort Ort)? Ueberlappt(IReadOnlyList<Kartenort> umriss)
    {
        if (umriss.Count < 3 || IstLeer) return null;
        var rahmen = Kartenrechteck.Um(umriss)!.Value;
        foreach (var l in Laeufe)
        {
            if (!l.Bereich.Ueberdeckt(rahmen.Vergroessert(0.01))) continue;
            foreach (var w in l.Punkte)
            {
                var halb = w.Breite / 2;
                if (!rahmen.Vergroessert(halb).Enthaelt(w.Ort)) continue;
                var d = Kartengeometrie.ImVieleck(w.Ort, umriss) ? 0 : Kartengeometrie.Strecken(umriss).Min(s => Kartengeometrie.AbstandStrecke(w.Ort, s.A, s.B));
                if (d < halb - 0.01) return (l.Name, w.Ort);
            }
        }
        foreach (var s in Seen)
        {
            if (!s.Bereich.Ueberdeckt(rahmen)) continue;
            foreach (var e in umriss)
                if (s.Enthaelt(e)) return (s.Name, e);
            foreach (var e in s.Umriss)
                if (Kartengeometrie.ImVieleck(e, umriss)) return (s.Name, e);
            foreach (var (a, b) in Kartengeometrie.Strecken(umriss))
                foreach (var (c, d) in Kartengeometrie.Strecken(s.Umriss))
                    if (Kartengeometrie.Schnitt(a, b, c, d) is { } x) return (s.Name, x);
        }
        return null;
    }

    // ── Bett (für Gelaendeform) ──────────────────────────────────────────────

    /// <summary>
    /// Die Höhe des Bodens am Ort mit dem Bett aller Gewässer: <paramref name="boden"/> ist der Boden ohne Wasser (Raster mit dem Bett der
    /// Wege), <paramref name="wegkern"/> sagt, ob der Ort im Kern eines Weges oder einer Mauer liegt (dort bleibt das Ufer stehen).
    /// </summary>
    internal double Boden(double x, double z, double boden, bool wegkern)
    {
        var p = new Kartenort(x, z);
        var ziel = boden;
        foreach (var l in Laeufe)
        {
            if (!l.Bereich.Enthaelt(p) || l.Naechste(p) is not ({ } k, { } t, { } d)) continue;
            var w = l.Bei(k, t);
            var halb = w.Breite / 2;
            if (d <= halb)
                ziel = Math.Min(ziel, w.Spiegel - w.Tiefe * (1 - (d / halb) * (d / halb)));
            else if (!wegkern && boden > w.Spiegel && d < halb + Uferbreite(w.Breite))
                ziel = Math.Min(ziel, w.Spiegel + (boden - w.Spiegel) * Gelaendeform.Weich((d - halb) / Uferbreite(w.Breite)));
        }
        foreach (var s in Seen)
        {
            if (!s.Bereich.Enthaelt(p)) continue;
            var rand = s.Randabstand(p);
            if (s.Enthaelt(p))
                ziel = Math.Min(ziel, s.Wasserstand - Math.Min(s.Tiefe, rand * Seeboeschung));
            else if (!wegkern && boden > s.Wasserstand && rand < SeeUfer)
                ziel = Math.Min(ziel, s.Wasserstand + (boden - s.Wasserstand) * Gelaendeform.Weich(rand / SeeUfer));
        }
        return ziel;
    }
}
