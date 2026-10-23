using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Wasser: Flüsse, Bäche, Seen und Teiche mit ihrem Spiegel und ihrem Bett (Kartenwerkstatt, Schritt 5c)
//
//  Ein Fluss (oder Bach) ist eine Linie mit Breite je Stützpunkt; er fließt in Zeichenrichtung. Wie ein glatter Weg läuft er als weiche
//  Kurve durch alle Stützpunkte (Wegkurve, etwa alle 1 m ein Punkt); Breite und Tiefe gehen zwischen zwei Stützpunkten gleichmäßig über.
//  Sein Spiegel folgt dem Gelände: die Höhe des gespeicherten Rasters unter der Mitte, über ±8 m gemittelt, um den Freibord tiefer (ein Bach
//  0,4 m, je breiter, desto tiefer, ab 14 m Breite 1 m – so sind die Modelle von Brücke, Fähre und Hafen gebaut: Uferkante auf Höhe 0, Wasser
//  1 m darunter) – aber nie höher als weiter oben am Fluss. Wasser fließt nicht bergauf: Steigt das Gelände in Fließrichtung an, bleibt der
//  Spiegel auf seiner Höhe, und das Bett schneidet sich ein (wie ein Durchstich). Wie tief über den Freibord hinaus, steht in
//  GroessterEinschnitt – die Prüfung meldet ab 1,6 m einen Hinweis („läuft bergauf?“). Das ist eine bewusste Entscheidung: Jeder
//  Pinselstrich ändert das Gelände, ein harter Fehler würde beim Malen ständig aufleuchten; so bleibt der Fluss immer möglich, und man
//  sieht, wo er sich eingräbt.
//  Ein See (oder Teich) ist ein Vieleck mit einem Wasserstand (ohne Angabe 0,4 m unter dem tiefsten Punkt seines Ufers) und einer Tiefe.
//
//  Das Bett: Wie das Bett der Wege liegt es in der Schicht „Boden“ (Gelaendeform), das gespeicherte Raster bleibt unverändert. Im Fluss
//  hat der Boden ein Querprofil (Parabel: am Rand der Spiegel, in der Mitte die Tiefe), daneben ein Ufer, das vom Spiegel bis zur
//  Uferkante ins Gelände übergeht (0,5 m + das Anderthalbfache des Freibords breit: bei 1 m Freibord 2 m, steil wie die Böschung der
//  Modelle). Im See fällt der Boden vom Ufer mit 30 % bis zur Tiefe ab, außen 1,5 m Ufer. Unter dem Kern eines Weges oder einer Mauer
//  bleibt das Ufer stehen (der Weg führt bis ans Wasser, eine Brücke setzt dort an); das Flussbett selbst gräbt sich auch unter Wegen ein.
//
//  Ein Hafen (ein Gebäude am Ufer, Baustein.AmUfer) hat eine Kaimauer an seiner Rückseite. Wie im Modell steht sie im Wasser: Vor ihr ist
//  das Ufer bis ins Wasser ausgehoben, ein Hafenbecken (so breit wie der Hafen und 1 m mehr je Seite, bis 1 m ins Wasser, mindestens 4 m vor
//  der Kaimauer – so weit liegen die Kähne), sein Boden 1,5 m unter dem Spiegel des Gewässers davor. Das Becken zählt nicht als Gewässer
//  (An, IstWasser, Prüfung und Wege sehen es nicht); es ändert nur den Boden, die Darstellung zeichnet darin Wasser (Becken).
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

/// <summary>
/// Die Kaimauer eines Gebäudes am Ufer (der Hafen): seine Rückseite von <see cref="A"/> nach <see cref="B"/>; das Wasser liegt in Richtung
/// (<see cref="NachX"/>, <see cref="NachZ"/>), einem Einheitsvektor.
/// </summary>
public readonly record struct Kaimauer(string Name, Kartenort A, Kartenort B, double NachX, double NachZ)
{
    /// <summary>Die Kaimauer eines Gebäudes mit Mitte (x, z), Drehung (Grad wie Godot <c>Rotation.Y</c>) und Grundfläche: seine Rückseite (lokal −z).</summary>
    public static Kaimauer Von(string name, double x, double z, double drehung, double breite, double tiefe)
    {
        Kartenort Lokal(double lx, double lz)
        {
            var d = Kartengeometrie.Dreh(lx, lz, drehung);
            return new Kartenort(x + d.X, z + d.Z);
        }
        var nach = Kartengeometrie.Dreh(0, -1, drehung);
        return new Kaimauer(name, Lokal(-breite / 2, -tiefe / 2), Lokal(breite / 2, -tiefe / 2), nach.X, nach.Z);
    }
}

/// <summary>Ein Hafenbecken vor einer Kaimauer (siehe Kopf der Datei): Umriss (Viereck), Spiegel des Gewässers davor und Höhe seines Bodens.</summary>
public sealed class Hafenbecken
{
    public string Name { get; }

    /// <summary>Die vier Ecken: an der Kaimauer links, rechts, draußen rechts, links.</summary>
    public IReadOnlyList<Kartenort> Umriss { get; }

    /// <summary>Die Höhe des Spiegels (des Gewässers vor der Kaimauer).</summary>
    public double Spiegel { get; }

    /// <summary>Die Höhe des Bodens im Becken (<see cref="Wasserform.BeckenTiefe"/> unter dem Spiegel).</summary>
    public double Boden => Spiegel - Wasserform.BeckenTiefe;

    public Kartenrechteck Bereich { get; }

    internal Hafenbecken(string name, IReadOnlyList<Kartenort> umriss, double spiegel)
    {
        (Name, Umriss, Spiegel) = (name, umriss, spiegel);
        Bereich = Kartenrechteck.Um(umriss)!.Value;
    }

    /// <summary>Liegt der Ort im Becken?</summary>
    public bool Enthaelt(Kartenort p) => Bereich.Enthaelt(p) && Kartengeometrie.ImVieleck(p, Umriss);
}

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
    /// So tief (Meter) schneidet sich das Bett höchstens ein: wie weit der Spiegel unter dem Gelände (über ±8 m gemittelt) liegt, über den
    /// Freibord hinaus – mehr als 0 heißt: Hier stieg das Gelände in Fließrichtung an. Dazu der Ort.
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

    /// <summary>Liegt der Punkt im Fluss oder an seinem Ufer (bis zur Uferkante, <see cref="Wasserform.Uferbreite"/>)?</summary>
    public bool ImUfer(Kartenort p)
    {
        if (!Bereich.Enthaelt(p) || Naechste(p) is not ({ } k, { } t, { } d)) return false;
        var w = Bei(k, t);
        return d <= w.Breite / 2 + Wasserform.Uferbreite(w.Breite);
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
    /// <summary>So tief (Meter) liegt der Spiegel eines Sees ohne Wasserstand unter dem tiefsten Punkt seines Ufers.</summary>
    public const double SeeFreibord = 0.4;

    /// <summary>
    /// So tief (Meter) liegt der Spiegel eines Flusses dieser Breite unter dem Gelände, wo nichts ihn zwingt: 0,3 m + ein Zwanzigstel der Breite,
    /// 0,4 bis 1 m (Bach 3 m: 0,45 m; ab 14 m: 1 m wie in den Modellen von Brücke, Fähre und Hafen).
    /// </summary>
    public static double Freibord(double breite) => Math.Clamp(0.3 + 0.05 * breite, 0.4, 1);

    /// <summary>Über so viele Meter vor und hinter einer Stelle wird das Gelände unter einem Fluss gemittelt (Dreiecksgewicht).</summary>
    public const double Glaettung = 8;

    /// <summary>Ein See wird vom Ufer an um so viel tiefer je Meter (30 %), bis er seine Tiefe erreicht.</summary>
    public const double Seeboeschung = 0.3;

    /// <summary>Tiefe eines Sees ohne Angabe (Meter).</summary>
    public const double SeeTiefeVorgabe = 2;

    /// <summary>Breite des Ufers um einen See (Meter).</summary>
    public const double SeeUfer = 1.5;

    /// <summary>Ab so viel Einschnitt (Meter über den Freibord hinaus) meldet die Prüfung einen Fluss, der bergauf läuft.</summary>
    public const double Schluchtgrenze = 1.6;

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

    /// <summary>
    /// Breite des Ufers neben einem Fluss dieser Breite (Meter): vom Wasser bis zur Uferkante, 0,5 m + das Anderthalbfache des Freibords
    /// (Bach 1,6 m breit: 1,1 m; ab 14 m: 2 m).
    /// </summary>
    public static double Uferbreite(double breite) => 0.5 + 1.5 * Freibord(breite);

    /// <summary>So tief (Meter) liegt der Boden eines Hafenbeckens unter dem Spiegel.</summary>
    public const double BeckenTiefe = 1.5;

    /// <summary>So weit (Meter) reicht ein Hafenbecken mindestens vor die Kaimauer (dort liegen die Kähne) und so viel weiter ins Wasser.</summary>
    public const double BeckenMindest = 4, BeckenImWasser = 1;

    /// <summary>So viel (Meter) ist ein Hafenbecken auf jeder Seite breiter als die Kaimauer (die Kähne ragen darüber hinaus).</summary>
    public const double BeckenRand = 1;

    /// <summary>So weit (Meter) vor der Kaimauer beginnt das Becken (der Boden unter dem Hafen bleibt stehen).</summary>
    public const double BeckenAbstand = 0.25;

    /// <summary>So weit (Meter) vor der Kaimauer wird das Wasser gesucht – weiter weg gibt es kein Becken (die Prüfung erlaubt 3 m).</summary>
    public const double BeckenSuche = 6;

    private readonly Hoehenraster _roh;
    private readonly Func<IReadOnlyList<Gewaesser>> _gewaesser;
    private readonly Func<IReadOnlyList<Kaimauer>> _kaie;
    private (List<Wasserlauf> Laeufe, List<Wassersee> Seen, List<Hafenbecken> Becken)? _stand;

    /// <param name="roh">Das gespeicherte Höhenraster (für den Spiegel).</param>
    /// <param name="gewaesser">Die Gewässer der Karte – wird nach jedem <see cref="AllesVeraltet"/> neu gefragt.</param>
    /// <param name="kaie">Die Kaimauern der Häfen – ebenso; null: keine.</param>
    public Wasserform(Hoehenraster roh, Func<IReadOnlyList<Gewaesser>> gewaesser, Func<IReadOnlyList<Kaimauer>>? kaie = null) =>
        (_roh, _gewaesser, _kaie) = (roh, gewaesser, kaie ?? (() => Array.Empty<Kaimauer>()));

    /// <summary>Gewässer, Häfen oder Gelände haben sich geändert: beim nächsten Lesen alles neu.</summary>
    public void AllesVeraltet() => _stand = null;

    private (List<Wasserlauf> Laeufe, List<Wassersee> Seen, List<Hafenbecken> Becken) Stand => _stand ??= Rechnen();

    /// <summary>Die Flüsse und Bäche.</summary>
    public IReadOnlyList<Wasserlauf> Laeufe => Stand.Laeufe;

    /// <summary>Die Seen und Teiche.</summary>
    public IReadOnlyList<Wassersee> Seen => Stand.Seen;

    /// <summary>Die Hafenbecken vor den Kaimauern (kein Gewässer: nur Boden und Bild, siehe Kopf der Datei).</summary>
    public IReadOnlyList<Hafenbecken> Becken => Stand.Becken;

    /// <summary>Hat die Karte kein Wasser?</summary>
    public bool IstLeer => Laeufe.Count == 0 && Seen.Count == 0;

    /// <summary>Die Rechtecke um jedes Gewässer mit seinem Ufer und um jedes Hafenbecken (dort ändern sie den Boden).</summary>
    public IEnumerable<Kartenrechteck> Bereiche => Laeufe.Select(l => l.Bereich).Concat(Seen.Select(s => s.Bereich)).Concat(Becken.Select(b => b.Bereich));

    // ── Rechnen ──────────────────────────────────────────────────────────────

    private (List<Wasserlauf>, List<Wassersee>, List<Hafenbecken>) Rechnen()
    {
        var (laeufe, seen) = GewaesserRechnen();
        _stand = (laeufe, seen, new List<Hafenbecken>()); // die Becken fragen An – das braucht die Gewässer schon
        var becken = new List<Hafenbecken>();
        if (laeufe.Count + seen.Count > 0)
            foreach (var kai in _kaie())
                if (BeckenRechnen(kai) is { } b) becken.Add(b);
        return (laeufe, seen, becken);
    }

    /// <summary>
    /// Das Becken vor einer Kaimauer: Links, in der Mitte und rechts wird das Wasser gesucht (alle 0,25 m bis <see cref="BeckenSuche"/>); das Becken reicht bis
    /// <see cref="BeckenImWasser"/> ins Wasser, mindestens <see cref="BeckenMindest"/> weit. Kein Wasser vor der Mitte: kein Becken.
    /// </summary>
    private Hafenbecken? BeckenRechnen(Kaimauer kai)
    {
        var laenge = kai.A.Abstand(kai.B);
        if (laenge <= 0) return null;
        var (tx, tz) = ((kai.B.X - kai.A.X) / laenge, (kai.B.Z - kai.A.Z) / laenge);
        Kartenort Ort(Kartenort von, double entlang, double hinaus) =>
            new(von.X + tx * entlang + kai.NachX * hinaus, von.Z + tz * entlang + kai.NachZ * hinaus);
        (double Abstand, double Spiegel)? Wasser(Kartenort von)
        {
            for (var s = 0.0; s <= BeckenSuche + 1e-9; s += 0.25)
                if (An(Ort(von, 0, s).X, Ort(von, 0, s).Z) is { } w) return (s, w.Spiegel);
            return null;
        }
        var mitte = Ort(kai.A, laenge / 2, 0);
        if (Wasser(mitte) is not var (abstand, spiegel)) return null;
        foreach (var rand in new[] { Ort(kai.A, -BeckenRand, 0), Ort(kai.B, BeckenRand, 0) })
            abstand = Math.Max(abstand, Wasser(rand)?.Abstand ?? BeckenSuche);
        var weit = Math.Max(abstand + BeckenImWasser, BeckenMindest);
        return new Hafenbecken(kai.Name, new[]
        {
            Ort(kai.A, -BeckenRand, BeckenAbstand), Ort(kai.B, BeckenRand, BeckenAbstand), Ort(kai.B, BeckenRand, weit), Ort(kai.A, -BeckenRand, weit),
        }, spiegel);
    }

    private (List<Wasserlauf>, List<Wassersee>) GewaesserRechnen()
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
            var a = Math.Min(verlauf.Abschnitte[k], stuetz.Count - 2);
            var t = Anteil(k);
            var (pa, pb) = (stuetz[a], stuetz[a + 1]);
            var breite = PunktBreite(g, pa) + (PunktBreite(g, pb) - PunktBreite(g, pa)) * t;
            var freibord = Freibord(breite);
            spiegel = Math.Min(spiegel, glatt[k] - freibord);
            var zuTief = glatt[k] - freibord - spiegel;
            if (zuTief > einschnitt) (einschnitt, einschnittOrt) = (zuTief, orte[k]);
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
        var stand = g.Wasserstand ?? tiefst - SeeFreibord;
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

    /// <summary>Liegt der Ort im Wasser oder an einem Ufer (bis zur Uferkante, wo das Ufer ins Gelände übergeht)?</summary>
    public bool ImUfer(double x, double z)
    {
        var p = new Kartenort(x, z);
        return Laeufe.Any(l => l.ImUfer(p)) || Seen.Any(s => s.Bereich.Enthaelt(p) && (s.Enthaelt(p) || s.Randabstand(p) <= SeeUfer));
    }

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
        foreach (var b in Becken)
            if (b.Enthaelt(p)) ziel = Math.Min(ziel, b.Boden); // auch unter einem Weg: Die Kaimauer steht im Wasser
        return ziel;
    }
}
