using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Wie groß eine Karte ist: Spielfeld, Randstreifen, Kamera-Grenzen (Kartenwerkstatt, Schritt 5a)
//
//  Jede Karte ist ein Rechteck, das Spielfeld: Breite (x) mal Tiefe (z) in Metern um ihre Mitte (Feld „mitte“, ohne Angabe der Ursprung =
//  der Marktplatz). Rundherum liegt der Randstreifen (Feld „rand“, Vorgabe 300 m): nicht bespielbar – keine Gebäude, Bauzonen, Mauern –,
//  aber Wald, Streu, Felder, Pflanzen, Wege und Wasser dürfen hinein. Spielfeld und Rand zusammen sind die ganze Karte (Aussen); dahinter
//  zeichnet die Darstellung nichts mehr (sie lässt den Rand im Dunst verblassen).
//
//  Die Kamera: Ihr Blickpunkt bleibt im Spielfeld (BlickpunktKlemmen), und herauszoomen darf sie nur so weit, dass das Bild auch am Rand
//  des Spielfelds nicht über den Randstreifen hinausreicht (GroessterZoom) – nie Himmel, nie Leere.
//
//  Alte Karten ohne Größe (bis Schritt 5a): Die Größe wird aus dem Inhalt abgeleitet (Ableiten) – das Spielfeld umfasst alles Bespielbare
//  mit 20 m Abstand, auf 50 m gerundet, und ist groß genug, dass Wege, Natur und Wasser in Spielfeld und Rand passen.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein achsparalleles Rechteck auf dem Boden (Meter, x = Osten, z = Süden).</summary>
public readonly record struct Kartenrechteck(double X0, double Z0, double X1, double Z1)
{
    public double Breite => X1 - X0;
    public double Tiefe => Z1 - Z0;
    public Kartenort Mitte => new((X0 + X1) / 2, (Z0 + Z1) / 2);

    /// <summary>Liegt der Punkt im Rechteck (der Rand zählt dazu, mit 1 mm Spiel)?</summary>
    public bool Enthaelt(double x, double z) => x >= X0 - 1e-3 && x <= X1 + 1e-3 && z >= Z0 - 1e-3 && z <= Z1 + 1e-3;

    public bool Enthaelt(Kartenort p) => Enthaelt(p.X, p.Z);

    /// <summary>Überdecken sich die beiden Rechtecke mit einer Fläche (Berühren an einer Kante zählt nicht)?</summary>
    public bool Ueberdeckt(Kartenrechteck o) => Math.Min(X1, o.X1) - Math.Max(X0, o.X0) > 1e-3 && Math.Min(Z1, o.Z1) - Math.Max(Z0, o.Z0) > 1e-3;

    /// <summary>Der nächste Punkt im Rechteck.</summary>
    public Kartenort Klemmen(double x, double z) => new(Math.Clamp(x, X0, X1), Math.Clamp(z, Z0, Z1));

    /// <summary>Um <paramref name="meter"/> nach allen Seiten größer (negativ: kleiner).</summary>
    public Kartenrechteck Vergroessert(double meter) => new(X0 - meter, Z0 - meter, X1 + meter, Z1 + meter);

    /// <summary>Das Rechteck um Punkte – null ohne Punkte.</summary>
    public static Kartenrechteck? Um(IEnumerable<Kartenort> punkte)
    {
        double x0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, z1 = double.MinValue;
        foreach (var p in punkte) (x0, z0, x1, z1) = (Math.Min(x0, p.X), Math.Min(z0, p.Z), Math.Max(x1, p.X), Math.Max(z1, p.Z));
        return x0 > x1 ? null : new Kartenrechteck(x0, z0, x1, z1);
    }
}

/// <summary>Größe, Lage und Randstreifen einer Karte, dazu die Grenzen der Kamera (siehe Kopf der Datei).</summary>
public sealed record Kartengrenzen(double Breite, double Tiefe, Kartenort Mitte, double Randbreite)
{
    /// <summary>Breite und Tiefe gehen in Schritten von so vielen Metern (auch der Rand).</summary>
    public const double Schritt = 50;

    /// <summary>Die kleinste Karte (Breite × Tiefe).</summary>
    public const double MinBreite = 400, MinTiefe = 300;

    /// <summary>Die größte Karte (2 × 2 km).</summary>
    public const double MaxSeite = 2000;

    /// <summary>Größe einer neuen Karte in der Kartenwerkstatt.</summary>
    public const double NeueBreite = 600, NeueTiefe = 450;

    /// <summary>Breite des Randstreifens ohne Angabe in der Karte – und Grenzen für die Einstellung.</summary>
    public const double RandVorgabe = 300, RandMin = 100, RandMax = 600;

    /// <summary>So viel Abstand hält das abgeleitete Spielfeld einer alten Karte um ihren bespielbaren Inhalt.</summary>
    public const double AbleitenAbstand = 20;

    /// <summary>Das bespielbare Rechteck: Gebäude, Bauzonen, Mauern, Häfen, der Blickpunkt der Kamera.</summary>
    public Kartenrechteck Spielfeld => new(Mitte.X - Breite / 2, Mitte.Z - Tiefe / 2, Mitte.X + Breite / 2, Mitte.Z + Tiefe / 2);

    /// <summary>Die ganze Karte: Spielfeld und Randstreifen. Dahinter ist nichts.</summary>
    public Kartenrechteck Aussen => Spielfeld.Vergroessert(Randbreite);

    /// <summary>Liegt der Punkt im Spielfeld?</summary>
    public bool ImSpielfeld(double x, double z) => Spielfeld.Enthaelt(x, z);

    /// <summary>Liegt der Punkt in der Karte (Spielfeld oder Rand)?</summary>
    public bool InDerKarte(double x, double z) => Aussen.Enthaelt(x, z);

    /// <summary>Liegt der Punkt im Randstreifen (in der Karte, aber nicht im Spielfeld)?</summary>
    public bool ImRand(double x, double z) => InDerKarte(x, z) && !ImSpielfeld(x, z);

    /// <summary>Der Blickpunkt der Kamera, ins Spielfeld geholt (der nächste Punkt darin). Weiches Abbremsen macht die Darstellung.</summary>
    public Kartenort BlickpunktKlemmen(double x, double z) => Spielfeld.Klemmen(x, z);

    /// <summary>
    /// Wie weit die Kamera höchstens herauszoomen darf: die sichtbare Bildhöhe in Metern (Godot <c>Camera3D.Size</c> einer orthogonalen Kamera,
    /// die die Höhe hält), bei der das Bild auch mit dem Blickpunkt auf dem Rand des Spielfelds nicht über den Randstreifen hinausreicht.
    /// Der Blickpunkt ist die Bildmitte auf dem Boden. Am Boden ist das Bild ein Rechteck: quer <c>Size · Seitenverhältnis</c>, in
    /// Blickrichtung <c>Size / sin(Neigung)</c>. <paramref name="drehung"/> (Grad um die Hochachse) rechnet für genau diese Blickrichtung;
    /// ohne Angabe für jede Drehung (das gedrehte Rechteck reicht dann höchstens so weit wie seine halbe Diagonale) – so springt der Zoom beim
    /// Drehen nicht. <paramref name="reserve"/>: so viele Meter bleiben vor der Außenkante frei (für den Dunst).
    /// </summary>
    /// <param name="neigung">Neigung der Kamera über dem Boden in Grad (das Spiel: 40).</param>
    /// <param name="seitenverhaeltnis">Breite durch Höhe des Bildes (z. B. 16/9).</param>
    public double GroessterZoom(double neigung, double seitenverhaeltnis, double? drehung = null, double reserve = 0)
    {
        var platz = Math.Max(0, Randbreite - reserve);
        var quer = seitenverhaeltnis / 2;                                   // halbe Bildbreite am Boden je Meter Size
        var laengs = 1 / (2 * DetMathe.Sin(Math.Clamp(neigung, 1, 90) * Math.PI / 180)); // halbe Bildtiefe am Boden je Meter Size
        double reichweite;
        if (drehung is { } d)
        {
            var w = d * Math.PI / 180;
            var (c, s) = (Math.Abs(DetMathe.Cos(w)), Math.Abs(DetMathe.Sin(w)));
            reichweite = Math.Max(quer * c + laengs * s, quer * s + laengs * c);
        }
        else reichweite = Math.Sqrt(quer * quer + laengs * laengs);
        return platz / reichweite;
    }

    /// <summary>Eine Seitenlänge auf 50 m gerundet und zwischen <paramref name="min"/> und 2.000 m.</summary>
    public static double SeiteRunden(double meter, double min) => Math.Clamp(Math.Round(meter / Schritt) * Schritt, min, MaxSeite);

    /// <summary>Eine Randbreite auf 50 m gerundet und zwischen 100 und 600 m.</summary>
    public static double RandRunden(double meter) => Math.Clamp(Math.Round(meter / Schritt) * Schritt, RandMin, RandMax);

    /// <summary>
    /// Die Größe einer alten Karte ohne Angabe: das Spielfeld um den bespielbaren Inhalt (<paramref name="spielinhalt"/>, mit 20 m Abstand) und
    /// so groß, dass der übrige Inhalt (<paramref name="uebriges"/>: Wege, Natur, Wasser) mit dem Rand in die Karte passt; jede Kante auf 50 m
    /// nach außen gerundet, mindestens 400 × 300 m (gleichmäßig nach beiden Seiten), höchstens 2.000 m. Ohne Inhalt: 400 × 300 m um den Ursprung.
    /// </summary>
    public static Kartengrenzen Ableiten(IEnumerable<Kartenort> spielinhalt, IEnumerable<Kartenort> uebriges, double randbreite = RandVorgabe)
    {
        var spiel = Kartenrechteck.Um(spielinhalt)?.Vergroessert(AbleitenAbstand);
        var rest = Kartenrechteck.Um(uebriges)?.Vergroessert(-randbreite);
        // Der übrige Inhalt darf bis an die Außenkante reichen: Das Spielfeld muss ihn nur bis auf die Randbreite umfassen.
        var r = (spiel, rest) switch
        {
            ({ } a, { } b) => new Kartenrechteck(Math.Min(a.X0, b.X0), Math.Min(a.Z0, b.Z0), Math.Max(a.X1, b.X1), Math.Max(a.Z1, b.Z1)),
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => new Kartenrechteck(0, 0, 0, 0),
        };
        var (x0, x1) = Kante(r.X0, r.X1, MinBreite);
        var (z0, z1) = Kante(r.Z0, r.Z1, MinTiefe);
        return new Kartengrenzen(x1 - x0, z1 - z0, new Kartenort((x0 + x1) / 2, (z0 + z1) / 2), randbreite);

        static (double, double) Kante(double a, double b, double min)
        {
            // ein umgedrehtes Rechteck (der übrige Inhalt passt schon in den Rand) zählt wie ein Punkt in seiner Mitte
            if (a > b) a = b = (a + b) / 2;
            var (u, o) = (Math.Floor(a / Schritt) * Schritt, Math.Ceiling(b / Schritt) * Schritt);
            if (o - u < min)
            {
                // so gleichmäßig wie möglich nach beiden Seiten, jede Kante weiter auf 50 m (die untere bekommt den ungeraden Schritt)
                var fehlt = Math.Ceiling((min - (o - u)) / Schritt) * Schritt;
                var unten = Math.Ceiling(fehlt / 2 / Schritt) * Schritt;
                (u, o) = (u - unten, o + fehlt - unten);
            }
            if (o - u > MaxSeite)
            {
                var mitte = Math.Round((u + o) / 2 / Schritt) * Schritt;
                (u, o) = (mitte - MaxSeite / 2, mitte + MaxSeite / 2);
            }
            return (u + 0.0, o + 0.0);
        }
    }

    /// <summary>Die vier Ecken der Grundfläche eines Gebäudes (Baustein mal Maßstab, gedreht, mit Vorplatz) – leer ohne Größe.</summary>
    public static IReadOnlyList<Kartenort> GebaeudeEcken(Baustein? b, double x, double z, double drehung, double massstab)
    {
        if (b?.Groesse is not { Length: 2 } g) return Array.Empty<Kartenort>();
        var (w, t) = (g[0] * massstab / 2, g[1] * massstab / 2);
        return new[] { (-w, -t), (w, -t), (w, t), (-w, t) }.Select(e =>
        {
            var d = Kartengeometrie.Dreh(e.Item1, e.Item2, drehung);
            return new Kartenort(x + d.X, z + d.Z);
        }).ToArray();
    }
}
