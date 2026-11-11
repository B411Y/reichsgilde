using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Fläche füllen nach Regeln (Felspinsel P6, wie PCG in Unreal – aber als feste Regelkette statt Knoten-Editor)
//
//  Eine Fläche (Umriss als Vieleck oder Kreis) wird mit einem Befehl gefüllt: Jede Regel ist ein Felspinsel (Vorlage, Größe, Dichte, Abstand,
//  Filter, Aussparen, eigene Gruppen aus P5 …) und dazu:
//    • Lücken: Rauschen (Wertrauschen mit Zellen zu „Rauschweite“ Metern) lässt ungefähr diesen Anteil der Fläche frei – so entstehen Gruppen
//      und Lücken statt eines gleichmäßigen Teppichs;
//    • Höhe in der Fläche: von–bis als Anteil zwischen tiefstem und höchstem Punkt der Fläche (0 unten, 1 oben) – „Kuppen oben, Schutt unten“
//      ohne Meterzahlen, die auf jeder Karte andere wären;
//    • Auf Fels setzen: die Stücke kommen nur auf große Stücke (Grundfläche ab AufFelsRadius) – kleine auf große.
//  Die Kette prüft an jedem Ort: Steigung → Höhe (Meter des Pinsels, dann Anteil der Fläche) → Boden → Rauschen; die Regeln laufen „groß vor
//  klein“ (nach dem größten Stück ihrer Vorlage, bei Gleichstand in ihrer Reihenfolge), damit kleine Stücke um die großen herum Platz finden.
//  Die Dichte gilt für den Teil der Fläche, in dem die Regel wirkt (sonst stünde eine Regel nur für Steilhänge dort viel zu dicht).
//  Obergrenze: Ein Füllbefehl soll höchstens <see cref="Felsfuellung.Obergrenze"/> Stücke setzen (Nutzer, Plan Frage 6). Das ist eine Warnung,
//  kein Verbot: Die Werkstatt zeigt die Zahl vorab (Schätzung, dann genau in der Vorschau) und füllt darüber erst nach Bestätigung.
//  Zufall nur aus der Zufallsquelle des Befehls (Startwert); gleicher Stand, gleiche Fläche, gleicher Startwert – dieselben Stücke.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Die Fläche, die ein Füllbefehl füllt: ein Umriss (Vieleck, mindestens drei Ecken, gerundet wie in der Datei). Ein Kreis ist ein Vieleck mit
/// <see cref="KreisEcken"/> Ecken (<see cref="Kreis"/>).
/// </summary>
public sealed class Felsflaeche
{
    /// <summary>So viele Ecken hat ein Kreis.</summary>
    public const int KreisEcken = 32;

    /// <summary>Kleinste Fläche (m²), die sich füllen lässt.</summary>
    public const double FlaecheMin = 1;

    public Felsflaeche(IEnumerable<Kartenort> umriss)
    {
        Umriss = umriss.Select(p => new Kartenort(Kartenraster.Punkt(p.X), Kartenraster.Punkt(p.Z))).ToList();
        if (Umriss.Count == 0) return;
        (X0, Z0, X1, Z1) = (Umriss.Min(p => p.X), Umriss.Min(p => p.Z), Umriss.Max(p => p.X), Umriss.Max(p => p.Z));
        var summe = 0.0; // Gaußsche Trapezformel
        for (var i = 0; i < Umriss.Count; i++)
        {
            var (a, b) = (Umriss[i], Umriss[(i + 1) % Umriss.Count]);
            summe += a.X * b.Z - b.X * a.Z;
        }
        Flaeche = Math.Abs(summe) / 2;
    }

    /// <summary>Ein Kreis um <paramref name="mitte"/> (Radius in Metern) als Vieleck.</summary>
    public static Felsflaeche Kreis(Kartenort mitte, double radius) =>
        new(Enumerable.Range(0, KreisEcken).Select(i => Kartengeometrie.Dreh(0, Math.Max(0.5, radius), i * 360.0 / KreisEcken))
            .Select(o => new Kartenort(mitte.X + o.X, mitte.Z + o.Z)));

    /// <summary>Die Ecken (gerundet).</summary>
    public IReadOnlyList<Kartenort> Umriss { get; }

    /// <summary>Das umschließende Rechteck (Meter).</summary>
    public double X0 { get; }
    public double Z0 { get; }
    public double X1 { get; }
    public double Z1 { get; }

    /// <summary>Inhalt in m².</summary>
    public double Flaeche { get; }

    /// <summary>Lässt sie sich füllen (mindestens drei Ecken und <see cref="FlaecheMin"/>)?</summary>
    public bool IstGueltig => Umriss.Count >= 3 && Flaeche >= FlaecheMin;

    /// <summary>Liegt der Punkt in der Fläche?</summary>
    public bool Enthaelt(double x, double z) => x >= X0 && x <= X1 && z >= Z0 && z <= Z1 && Kartengeometrie.ImVieleck(new Kartenort(x, z), Umriss);
}

/// <summary>
/// Eine Regel der Füllung (siehe Kopf der Datei): <paramref name="Pinsel"/> – was und wie dicht (Vorlage, Größe, Dichte, Abstand, Filter, Höhe in Metern,
/// Aussparen, Gestein, Boden, Spiegeln, Verbinden; „Hang mitformen“ gilt beim Füllen nicht); <paramref name="Luecken"/> – ungefährer Anteil der Fläche,
/// den das Rauschen frei lässt (0 … 0,9); <paramref name="Rauschweite"/> – Größe der Flecken (Meter, 4 … 200); <paramref name="HoeheAnteil"/> – nur
/// zwischen diesen Anteilen der Höhe in der Fläche (0 unten, 1 oben; null: überall); <paramref name="AufFels"/> – nur auf große Stücke.
/// </summary>
public sealed record Felsregel(Felspinsel Pinsel, double Luecken = 0, double Rauschweite = 24, Felsbereich? HoeheAnteil = null, bool AufFels = false)
{
    public const double LueckenMax = 0.9, RauschweiteMin = 4, RauschweiteMax = 200;

    /// <summary>Die Regel mit Werten in ihren Grenzen (der Pinsel ohne Radierer, Neu verteilen und Hang).</summary>
    public Felsregel Begrenzt() => this with
    {
        Pinsel = Pinsel.Begrenzt() with { Radierer = false, Ausduennen = false, Verteilen = null, HangFormen = false, HangSchicht = false },
        Luecken = Math.Clamp(Luecken, 0, LueckenMax),
        Rauschweite = Math.Clamp(Rauschweite, RauschweiteMin, RauschweiteMax),
        HoeheAnteil = HoeheAnteil?.Begrenzt(0, 1),
    };

    /// <summary>Der Radius (Meter) der größten Grundfläche, die die Regel setzt (bei mittlerer Größe) – für „groß vor klein“.</summary>
    public double Groessenrang
    {
        get
        {
            var g = Pinsel.Groesse;
            return Pinsel.GruppeWirksam is { } gr
                ? gr.Stuecke.Max(s => (Felsformen.Finde(s.Form)?.Radius ?? 1) * s.Massstab * g)
                : Pinsel.FormenWirksam.Max(f => f.Radius * g);
        }
    }

    /// <summary>Mittlere Grundfläche (m², Kreise) eines Treffers – für die Schätzung der Stückzahl.</summary>
    internal double MittlererPlatz
    {
        get
        {
            var p = Pinsel;
            var g2 = p.Groesse * p.Groesse * (1 + p.Streuung * p.Streuung / 3); // Mittel von (G·(1 ± s))² bei gleich verteiltem Wurf
            return p.GruppeWirksam is { } gr
                ? gr.Stuecke.Sum(s => Math.PI * Sq((Felsformen.Finde(s.Form)?.Radius ?? 1) * s.Massstab) * s.AchseX * s.AchseZ) * g2
                : p.FormenWirksam.Average(f => Math.PI * f.Radius * f.Radius) * g2;
        }
    }

    /// <summary>Stücke je Treffer (eine Gruppe ganz, sonst eins).</summary>
    internal int StueckeJeTreffer => Pinsel.GruppeWirksam?.Stuecke.Count ?? 1;

    private static double Sq(double x) => x * x;
}

/// <summary>
/// Eine Füllung: eine Kette von Regeln mit Kennung und Namen (Voreinstellungen <see cref="Steinbruch"/>, <see cref="Schutthalde"/>, <see cref="Felskuppe"/>
/// oder <see cref="Einfach"/> – „im Umriss“ mit dem Pinsel der Tafel).
/// </summary>
public sealed record Felsfuellung(string Kennung, string Name, IReadOnlyList<Felsregel> Regeln)
{
    /// <summary>So viele Stücke soll ein Füllbefehl höchstens setzen – darüber warnt die Werkstatt und füllt erst nach Bestätigung (kein Verbot; Nutzer, Frage 6).</summary>
    public const int Obergrenze = 1500;

    /// <summary>Höchstens so viele Versuche je Regel (Schutz gegen sehr große Flächen; keine Grenze der Stücke).</summary>
    public const int VersucheMax = 250_000;

    /// <summary>Nach so vielen abgewiesenen Treffern in Folge (zu nah, ausgespart, außerhalb) ist die Regel voll.</summary>
    public const int FehlversucheMax = 400;

    /// <summary>„Auf Fels setzen“: Ab diesem Radius der Grundfläche (Meter) trägt ein Stück andere; gesetzt wird innerhalb dieses Anteils seines Radius.</summary>
    public const double AufFelsRadius = 2.5, AufFelsInnen = 0.45;

    /// <summary>Auf so viel der Höhe eines tragenden Stücks (Anteil, von seiner Unterkante) setzen kleine Stücke auf.</summary>
    public const double AufFelsHoehe = 0.8;

    /// <summary>Kennung der einfachen Füllung (ein Pinsel, keine Regeln).</summary>
    public const string EinfachKennung = "umriss";

    /// <summary>„Im Umriss“: der Pinsel der Tafel als einzige Regel (mit Lücken bzw. „auf Fels“, wenn eingestellt).</summary>
    public static Felsfuellung Einfach(Felspinsel pinsel, double luecken = 0, double rauschweite = 24, bool aufFels = false) =>
        new(EinfachKennung, "Im Umriss", new[] { new Felsregel(pinsel, luecken, rauschweite, AufFels: aufFels) });

    private static Felspinsel Formen(double dichte, double groesse, double streuung, Pinselfilter? filter, params string[] formen) =>
        new(formen, null, null, Dichte: dichte, Groesse: groesse, Streuung: streuung, Filter: filter);

    /// <summary>Steinbruch: mittlere Wände am Steilhang, Blöcke und Platten am Hang, Schutt im Flachen, Schutt auf den Wänden.</summary>
    public static readonly Felsfuellung Steinbruch = new("steinbruch", "Steinbruch", new[]
    {
        new Felsregel(Formen(0.45, 1, 0.2, new Pinselfilter(SteigungAb: 45), "wand_mittel_1", "wand_mittel_2", "wand_mittel_3"), Luecken: 0.2),
        new Felsregel(Formen(0.15, 0.9, 0.35, new Pinselfilter(SteigungAb: 8, SteigungBis: 45), "block", "platte")),
        new Felsregel(Formen(0.06, 1.4, 0.5, new Pinselfilter(SteigungBis: 30), "schuttbrocken"), Luecken: 0.35),
        new Felsregel(Formen(0.15, 1, 0.3, null, "schuttbrocken"), AufFels: true),
    });

    /// <summary>Schutthalde: Wände am Steilhang, Brocken und Schutt am Fuß (untere Hälfte der Fläche), dichter Schutt im Kies.</summary>
    public static readonly Felsfuellung Schutthalde = new("schutthalde", "Schutthalde", new[]
    {
        new Felsregel(Formen(0.3, 1, 0.25, new Pinselfilter(SteigungAb: 60), "wand", "rippe", "felsnase"), Luecken: 0.25),
        new Felsregel(Formen(0.2, 0.8, 0.4, new Pinselfilter(SteigungAb: 10, SteigungBis: 45), "block", "platte"), HoeheAnteil: new Felsbereich(0, 0.6)),
        new Felsregel(Formen(0.12, 1.5, 0.5, new Pinselfilter(SteigungBis: 35), "schuttbrocken"), Luecken: 0.25, HoeheAnteil: new Felsbereich(0, 0.5)),
        new Felsregel(Formen(0.2, 1.3, 0.5, new Pinselfilter(Boden: Bodentextur.Kies), "schuttbrocken")),
    });

    /// <summary>Felskuppe: Kuppen oben, Blöcke in der oberen Hälfte, Schutt auf den Kuppen und spärlich unten.</summary>
    public static readonly Felsfuellung Felskuppe = new("felskuppe", "Felskuppe", new[]
    {
        new Felsregel(new Felspinsel(Gruppe: "kuppe", Dichte: 0.25, Groesse: 1, Streuung: 0.25), Luecken: 0.3, Rauschweite: 30, HoeheAnteil: new Felsbereich(0.55, 1)),
        new Felsregel(Formen(0.1, 1, 0.3, null, "block"), HoeheAnteil: new Felsbereich(0.25, 1)),
        new Felsregel(Formen(0.2, 1, 0.3, null, "schuttbrocken"), AufFels: true),
        new Felsregel(Formen(0.05, 1.3, 0.5, null, "schuttbrocken"), HoeheAnteil: new Felsbereich(0, 0.4)),
    });

    /// <summary>Die Voreinstellungen (ohne „im Umriss“).</summary>
    public static readonly IReadOnlyList<Felsfuellung> Voreinstellungen = new[] { Steinbruch, Schutthalde, Felskuppe };

    /// <summary>Eine Voreinstellung nach Kennung (null: keine).</summary>
    public static Felsfuellung? Finde(string? kennung) => Voreinstellungen.FirstOrDefault(v => v.Kennung == kennung);

    /// <summary>
    /// Die Füllung mit den Häkchen des Pinsels der Tafel für alle Regeln: Gestein, Boden mitmalen, Aussparen (Gebäude, Wege, Bauzonen), Spiegeln,
    /// Verbinden. Vorlage, Größe, Dichte und Filter bleiben die der Regel.
    /// </summary>
    public Felsfuellung MitPinsel(Felspinsel p) => this with
    {
        Regeln = Regeln.Select(r => r with
        {
            Pinsel = r.Pinsel with
            {
                Gestein = p.Gestein, Boden = p.Boden, Aussparen = p.Aussparen, AussparenBauzonen = p.AussparenBauzonen, Spiegeln = p.Spiegeln, Verbinden = p.Verbinden,
            },
        }).ToList(),
    };

    /// <summary>Die Regeln in der Reihenfolge, in der sie füllen: groß vor klein (stabil), „auf Fels“ nach allen anderen.</summary>
    public IReadOnlyList<(int Nr, Felsregel Regel)> Kette() =>
        Regeln.Select((r, i) => (Nr: i, Regel: r.Begrenzt())).OrderBy(x => x.Regel.AufFels).ThenByDescending(x => x.Regel.Groessenrang).ToList();

    /// <summary>
    /// Wertrauschen (0 … 1) an (x, z): Zufallswerte an den Ecken eines Gitters mit <paramref name="weite"/> Metern, dazwischen weich gemischt.
    /// Nur aus Ganzzahlen und dem <paramref name="salz"/> – auf jeder Maschine gleich.
    /// </summary>
    public static double Rauschen(double x, double z, double weite, int salz)
    {
        var (fx, fz) = (x / weite, z / weite);
        var (i, j) = ((int)Math.Floor(fx), (int)Math.Floor(fz));
        var (tx, tz) = (Weich(fx - i), Weich(fz - j));
        double W(int a, int b) => NaturVerteilung.Streuung(unchecked(a * 7919 + salz), (long)b * 104729 + salz * 31L) % 4096 / 4095.0;
        var (w00, w10, w01, w11) = (W(i, j), W(i + 1, j), W(i, j + 1), W(i + 1, j + 1));
        return (w00 + (w10 - w00) * tx) * (1 - tz) + (w01 + (w11 - w01) * tx) * tz;
    }

    private static double Weich(double t) => t * t * (3 - 2 * t);

    /// <summary>Der Faktor (0 … 1) für Lücken und Höhe in der Fläche an einem Ort (<paramref name="anteil"/>: Höhe in der Fläche 0 … 1).</summary>
    internal static double Maske(Felsregel r, double x, double z, double anteil, int salz)
    {
        var f = 1.0;
        if (r.HoeheAnteil is { } h)
        {
            const double rand = 0.05; // weich über 5 % der Höhe
            var abstand = Math.Min(h.Von <= 0 ? double.MaxValue : anteil - h.Von, h.Bis >= 1 ? double.MaxValue : h.Bis - anteil); // am Rand 0 bzw. 1 keine Grenze
            f = Math.Min(f, Math.Clamp(abstand / rand + 0.5, 0, 1));
        }
        if (r.Luecken > 0 && f > 0) f = Math.Min(f, Math.Clamp((Rauschen(x, z, r.Rauschweite, salz) - r.Luecken) / 0.1, 0, 1));
        return f;
    }
}
