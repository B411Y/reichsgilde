using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>Die Lage einer Quelle ab einem Stichjahr: Sie gilt vier Spieljahre bis zur nächsten Lage dieser Quelle.</summary>
public sealed record Quellenlage(Rohstofflage Quelle, int Stichjahr, Lagegute Guete)
{
    /// <summary>Das letzte Jahr, in dem diese Lage gilt.</summary>
    public int LetztesJahr => Stichjahr + 3;

    /// <summary>Das Ereignis dazu (null bei „gewöhnlich“), z. B. „Wassereinbruch“.</summary>
    public LagenereignisDaten? Ereignis => Quelle.Ereignis(Guete);
}

/// <summary>
/// Eine Rohstoffquelle mit ihrer Lage (Datenversion 92, „Lagen für alle Rohstoffquellen“; die Ernte ist die erste, siehe <see cref="Erntewesen"/>):
///   • Alle vier Spieljahre beginnt in der Jahreszeit der Quelle (<see cref="Stichjahreszeit"/>) eine neue Lage: gut, gewöhnlich, schlecht oder
///     schwer. Sie gilt bis zur nächsten und verschiebt die Grundversorgung der Waren der Quelle am Markt (Faktor, begrenzt) – und halb die
///     Leistung des eigenen Umlandbetriebs (<see cref="Ertragsfaktor"/>).
///   • Im Jahr vor dem Stichjahr steht sie fest (<see cref="Angesagt"/>) und trifft immer ein. Nach einer schweren Lage ist die nächste nie
///     schlecht oder schwer (Schonjahr).
///   • Stufen, Chancen, Grenzen und Härte der Zeit kommen für alle Quellen aus dem Abschnitt „ernte“ – jede Quelle ist gleich oft und mit
///     denselben Faktoren betroffen; nur Waren und Texte sind eigen.
/// Nichts davon steht im Spielstand: Jede Lage kommt aus dem Startwert der Partie, dem Salz der Quelle und dem Stichjahr (ein Wurf je Lage),
/// dazu der Lage davor (Schonjahr).
/// </summary>
public sealed class Rohstofflage
{
    private readonly Stadtwirtschaft _w;
    private readonly int _startwert;
    private readonly int _salz;
    private readonly List<Lagegute> _lagen = new(); // Güte der 1., 2., … Lage (nur nach vorn gerechnet)
    private readonly LagenquelleDaten? _quelle;

    /// <summary>Stufen, Chancen und Grenzen (Abschnitt „ernte“).</summary>
    public ErnteDaten Werte { get; }

    /// <summary>Betroffene Waren und Gewicht (ohne eigene Waren eines Ereignisses).</summary>
    public IReadOnlyDictionary<string, double> Waren { get; }

    /// <summary>Der Umlandbetrieb, dessen Leistung die Lage trifft (null: keiner, z. B. die Ernte ohne Abschnitt „lagen“).</summary>
    public Betriebsart? Betrieb { get; }

    /// <summary>In welcher Jahreszeit eine Lage beginnt.</summary>
    public Jahreszeit Stichjahreszeit { get; }

    /// <summary>Anteil der Wirkung auf den eigenen Umlandbetrieb (0 = keine).</summary>
    public double Ertragsanteil { get; }

    /// <summary>Kennung der Quelle: der Umlandbetrieb, bei der Ernte „Ernte“.</summary>
    public string Kennung { get; }

    internal Rohstofflage(Stadtwirtschaft w, string kennung, ErnteDaten werte, IReadOnlyDictionary<string, double> waren, Jahreszeit stich, int salz,
        int startwert, Betriebsart? betrieb, double ertragsanteil, LagenquelleDaten? quelle)
    {
        _w = w;
        Kennung = kennung;
        Werte = werte;
        Waren = waren;
        Stichjahreszeit = stich;
        _salz = salz;
        _startwert = startwert;
        Betrieb = betrieb;
        Ertragsanteil = ertragsanteil;
        _quelle = quelle;
    }

    /// <summary>Das erste Stichjahr: das erste Jahr nach dem Startjahr in der Jahreszeit der Quelle (Frühling 1404, Sommer 1401, Herbst 1402, Winter 1403).</summary>
    public int ErstesStichjahr => Spielzeit.StartJahr + ((int)Stichjahreszeit == 0 ? 4 : (int)Stichjahreszeit);

    /// <summary>Name der Quelle für Texte, z. B. „Grube“ (bei der Ernte „Ernte“).</summary>
    public string Name => Betrieb is { } b && _quelle is not null && _w.Daten.Betriebsarten.TryGetValue(b, out var a) ? a.Name : Kennung;

    /// <summary>Das Ereignis einer Stufe (null bei „gewöhnlich“ oder für die Ernte, die ihre Texte selbst hat).</summary>
    public LagenereignisDaten? Ereignis(Lagegute g) => _quelle?.Von(g);

    // ═════════════════════════════════════════════════════════════════════════
    //  Abfragen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die Lage, die jetzt gilt (null vor der ersten).</summary>
    public Quellenlage? Jetzt => GiltIm(_w.Uhr.Jahr);

    /// <summary>Die Lage, die jetzt angesagt ist: im Jahr vor einem Stichjahr die des Stichjahres, sonst null.</summary>
    public Quellenlage? Angesagt => AngesagtIm(_w.Uhr.Jahr);

    /// <summary>Die Lage, die im Jahr <paramref name="jahr"/> gilt (null vor der ersten).</summary>
    public Quellenlage? GiltIm(int jahr)
    {
        var erstes = ErstesStichjahr;
        if (jahr < erstes) return null;
        return LageVon(jahr - (jahr - erstes) % 4);
    }

    /// <summary>Die Lage, die im Jahr <paramref name="jahr"/> angesagt ist: nur im Jahr vor einem Stichjahr.</summary>
    public Quellenlage? AngesagtIm(int jahr) => jahr + 1 >= ErstesStichjahr && (jahr + 1 - ErstesStichjahr) % 4 == 0 ? LageVon(jahr + 1) : null;

    /// <summary>Die Lage eines Stichjahres (null, wenn das Jahr keins ist).</summary>
    public Quellenlage? LageVon(int stichjahr)
    {
        var erstes = ErstesStichjahr;
        if (stichjahr < erstes || (stichjahr - erstes) % 4 != 0) return null;
        var nummer = (stichjahr - erstes) / 4;
        while (_lagen.Count <= nummer)
            _lagen.Add(Wuerfeln(erstes + 4 * _lagen.Count, _lagen.Count > 0 ? _lagen[^1] : Lagegute.Gewoehnlich));
        return new Quellenlage(this, stichjahr, _lagen[nummer]);
    }

    /// <summary>Faktor der jetzigen Lage auf die Grundversorgung dieser Ware (1, wenn sie nicht betroffen ist oder keine Lage gilt).</summary>
    public double Faktor(string ware) => Jetzt is { } l ? Faktor(ware, l.Guete) : 1;

    /// <summary>Gewicht einer Ware bei dieser Stufe (0 = nicht betroffen): eigene Waren des Ereignisses, sonst die der Quelle.</summary>
    public double Gewicht(string ware, Lagegute g) =>
        (Ereignis(g)?.Waren ?? Waren).TryGetValue(ware, out var gewicht) ? gewicht : 0;

    /// <summary>
    /// Faktor einer Stufe auf eine Ware: 1 + (Faktor der Stufe − 1) × Stärke der Härte × Gewicht der Ware, begrenzt auf
    /// [<see cref="ErnteDaten.UntereGrenze"/>, <see cref="ErnteDaten.ObereGrenze"/>].
    /// </summary>
    public double Faktor(string ware, Lagegute g)
    {
        var gewicht = Gewicht(ware, g);
        if (gewicht == 0) return 1;
        var f = 1 + (Werte.FaktorVon((Erntegute)g) - 1) * Werte.Von(_w.HaerteDerZeit).Staerke * gewicht;
        return Math.Clamp(f, Werte.UntereGrenze, Werte.ObereGrenze);
    }

    /// <summary>
    /// Faktor der jetzigen Lage auf die Leistung des eigenen Umlandbetriebs <see cref="Betrieb"/>: 1 + (Faktor der Stufe − 1) × Stärke ×
    /// <see cref="Ertragsanteil"/>, begrenzt wie die Waren (1 ohne Lage oder ohne Betrieb).
    /// </summary>
    public double Ertragsfaktor => Jetzt is { } l ? ErtragsfaktorBei(l.Guete) : 1;

    /// <summary>Faktor einer Stufe auf die Leistung des eigenen Umlandbetriebs.</summary>
    public double ErtragsfaktorBei(Lagegute g)
    {
        if (Betrieb is null || Ertragsanteil == 0 || g == Lagegute.Gewoehnlich) return 1;
        var f = 1 + (Werte.FaktorVon((Erntegute)g) - 1) * Werte.Von(_w.HaerteDerZeit).Staerke * Ertragsanteil;
        return Math.Clamp(f, Werte.UntereGrenze, Werte.ObereGrenze);
    }

    /// <summary>Die Waren mit vollem oder halbem Gewicht (die Rohwaren) einer Stufe, für Texte.</summary>
    public IEnumerable<string> Rohwaren(Lagegute g) => (Ereignis(g)?.Waren ?? Waren).Where(p => p.Value >= 0.5).Select(p => p.Key);

    // ═════════════════════════════════════════════════════════════════════════
    //  Würfeln
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Güte einer Lage: ein Wurf aus einer eigenen <see cref="Zufallsquelle"/> (Startwert der Partie mit dem Salz der Quelle, Stichjahr). Unten
    /// liegt „schwer“, darüber „schlecht“, ganz oben „gut“, dazwischen „gewöhnlich“. Nach „schwer“ wird eine schlechte Lage gewöhnlich (Schonjahr).
    /// Für die Ernte genau der Wurf von Datenversion 91.
    /// </summary>
    private Lagegute Wuerfeln(int stichjahr, Lagegute vorige)
    {
        var s = Werte.Von(_w.HaerteDerZeit);
        var wurf = new Zufallsquelle(Jahreszeitenwesen.Mischen(_startwert ^ _salz, stichjahr)).NextDouble();
        var guete = wurf < s.ChanceMissernte ? Lagegute.Schwer
            : wurf < s.ChanceMissernte + s.ChanceMager ? Lagegute.Schlecht
            : wurf >= 1 - s.ChanceGut ? Lagegute.Gut
            : Lagegute.Gewoehnlich;
        if (vorige == Lagegute.Schwer && guete is Lagegute.Schlecht or Lagegute.Schwer) guete = Lagegute.Gewoehnlich;
        return guete;
    }

    /// <summary>
    /// Salz einer Quelle aus ihrer Kennung: FNV-1a über die Zeichen (auf jedem Rechner gleich; <c>string.GetHashCode</c> wechselt je Start).
    /// </summary>
    internal static int SalzAus(string kennung)
    {
        unchecked
        {
            var h = 2166136261u;
            foreach (var c in kennung)
            {
                h ^= c;
                h *= 16777619u;
            }
            return (int)h;
        }
    }
}
