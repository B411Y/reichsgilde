namespace Reichsgilde.Core.Dynastie;

/// <summary>Haarfarben der Porträts. Welche ein Volk haben kann, steht in <see cref="Aussehenspalette.Haarfarben"/>.</summary>
public enum Haarfarbe
{
    Schwarz,
    Dunkelbraun,
    Braun,
    Kastanie,
    Rot,
    Blond,
    Hellblond,
    Silberblond,
    /// <summary>Kein Haar (Orks, Oger) – das Porträt färbt nichts um.</summary>
    Kahl,
}

/// <summary>Augenfarben der Porträts. Welche ein Volk haben kann, steht in <see cref="Aussehenspalette.Augenfarben"/>.</summary>
public enum Augenfarbe
{
    Dunkelbraun,
    Braun,
    Haselnuss,
    Gruen,
    Grau,
    Blau,
    Violett,
    Bernstein,
    Rot,
}

/// <summary>Welches Bild eines Gesichts das Porträt zeigt – nach dem Alter in Menschenjahren (<see cref="Aussehenspalette.AltersstufeAus"/>).</summary>
public enum Altersstufe
{
    /// <summary>Unter 14 Menschenjahren.</summary>
    Kind,
    /// <summary>14 bis 29.</summary>
    Jung,
    /// <summary>30 bis 54.</summary>
    Mittel,
    /// <summary>Ab 55.</summary>
    Alt,
}

/// <summary>Der Hintergrund des Porträts: Lehmwand, Holzvertäfelung, Wandteppich oder Gewölbe.</summary>
public enum Standgruppe
{
    Gemeiner,
    Buerger,
    Patrizier,
    /// <summary>Edler, Freiherr und Reichsgraf.</summary>
    Adel,
}

/// <summary>Die Kleidung im Porträt; sie wechselt beim Aufstieg zum Patrizier.</summary>
public enum Kleidung
{
    /// <summary>Gemeiner und Bürger.</summary>
    Schlicht,
    /// <summary>Patrizier und Adel.</summary>
    Vornehm,
}

/// <summary>
/// Wie eine Person im Porträt aussieht. Das Bild wählt die Darstellung aus Volk, Geschlecht und <see cref="Gesicht"/>
/// (dazu <see cref="Altersstufe"/> und <see cref="Kleidung"/>); Haar und Augen färbt sie um.
/// Das Aussehen wirkt auf nichts in der Simulation.
/// </summary>
/// <param name="Gesicht">0 bis <see cref="Aussehenspalette.Gesichter"/> − 1, je Volk und Geschlecht.</param>
public readonly record struct Aussehen(int Gesicht, Haarfarbe Haar, Augenfarbe Augen);

/// <summary>
/// Die Paletten je Volk und die Regeln fürs Würfeln und Vererben.
///
/// Vererben: Das Kind bekommt das Gesicht von Vater oder Mutter – vom Elternteil gleichen Geschlechts mit
/// <see cref="GleichesGeschlecht"/> (70 %). Hat es ein anderes Geschlecht als dieser Elternteil, bekommt es dieselbe
/// Gesichtsnummer im anderen Geschlecht. Haar und Augen kommen je von einem Elternteil (halb und halb); mit
/// <see cref="Abweichung"/> (10 %) wird es eine andere Farbe der Palette. Passt die Farbe nicht zum Volk des Kindes
/// (Ehe zwischen Völkern), würfelt es eine Farbe seines Volkes.
/// </summary>
public static class Aussehenspalette
{
    /// <summary>So viele Gesichter gibt es je Volk und Geschlecht.</summary>
    public const int Gesichter = 5;

    /// <summary>Chance, dass das Kind das Gesicht des Elternteils gleichen Geschlechts bekommt.</summary>
    public const double GleichesGeschlecht = 0.7;

    /// <summary>Chance, dass Haar- oder Augenfarbe nicht vom Elternteil kommt, sondern eine andere der Palette ist.</summary>
    public const double Abweichung = 0.1;

    private static readonly Dictionary<Volk, Haarfarbe[]> HaarJeVolk = new()
    {
        [Volk.Mensch] = new[] { Haarfarbe.Schwarz, Haarfarbe.Dunkelbraun, Haarfarbe.Braun, Haarfarbe.Rot, Haarfarbe.Blond, Haarfarbe.Hellblond },
        [Volk.Zwerg] = new[] { Haarfarbe.Schwarz, Haarfarbe.Dunkelbraun, Haarfarbe.Braun, Haarfarbe.Kastanie, Haarfarbe.Rot },
        [Volk.Elb] = new[] { Haarfarbe.Silberblond, Haarfarbe.Hellblond, Haarfarbe.Schwarz },
        [Volk.Halbling] = new[] { Haarfarbe.Dunkelbraun, Haarfarbe.Braun, Haarfarbe.Kastanie, Haarfarbe.Rot, Haarfarbe.Blond },
        [Volk.Ork] = new[] { Haarfarbe.Schwarz, Haarfarbe.Kahl },
        [Volk.Oger] = new[] { Haarfarbe.Kahl, Haarfarbe.Schwarz, Haarfarbe.Dunkelbraun },
    };

    private static readonly Dictionary<Volk, Augenfarbe[]> AugenJeVolk = new()
    {
        [Volk.Mensch] = new[] { Augenfarbe.Braun, Augenfarbe.Haselnuss, Augenfarbe.Gruen, Augenfarbe.Grau, Augenfarbe.Blau },
        [Volk.Zwerg] = new[] { Augenfarbe.Dunkelbraun, Augenfarbe.Braun, Augenfarbe.Grau, Augenfarbe.Blau },
        [Volk.Elb] = new[] { Augenfarbe.Grau, Augenfarbe.Blau, Augenfarbe.Gruen, Augenfarbe.Violett },
        [Volk.Halbling] = new[] { Augenfarbe.Braun, Augenfarbe.Haselnuss, Augenfarbe.Gruen, Augenfarbe.Blau },
        [Volk.Ork] = new[] { Augenfarbe.Bernstein, Augenfarbe.Rot, Augenfarbe.Dunkelbraun },
        [Volk.Oger] = new[] { Augenfarbe.Dunkelbraun, Augenfarbe.Braun, Augenfarbe.Bernstein },
    };

    /// <summary>Die Haarfarben, die zu diesem Volk passen (alle gleich oft).</summary>
    public static IReadOnlyList<Haarfarbe> Haarfarben(Volk volk) => HaarJeVolk[volk];

    /// <summary>Die Augenfarben, die zu diesem Volk passen.</summary>
    public static IReadOnlyList<Augenfarbe> Augenfarben(Volk volk) => AugenJeVolk[volk];

    /// <summary>Warum dieses Aussehen nicht zum Volk passt (null = es passt) – etwa für die Startseite.</summary>
    public static string? Verboten(Volk volk, Aussehen a)
    {
        if (a.Gesicht is < 0 or >= Gesichter) return $"Gesicht {a.Gesicht} gibt es nicht (0 bis {Gesichter - 1}).";
        if (!HaarJeVolk[volk].Contains(a.Haar)) return $"Die Haarfarbe {a.Haar} passt nicht zum Volk {volk.Einzahl()}.";
        if (!AugenJeVolk[volk].Contains(a.Augen)) return $"Die Augenfarbe {a.Augen} passt nicht zum Volk {volk.Einzahl()}.";
        return null;
    }

    /// <summary>Die Altersstufe nach Menschenjahren: unter 14 Kind, 14–29 jung, 30–54 mittel, ab 55 alt.</summary>
    public static Altersstufe AltersstufeAus(double menschenjahre) => menschenjahre switch
    {
        < 14 => Altersstufe.Kind,
        < 30 => Altersstufe.Jung,
        < 55 => Altersstufe.Mittel,
        _ => Altersstufe.Alt,
    };

    /// <summary>Patrizier und Adel tragen vornehme Kleidung, Gemeiner und Bürger schlichte.</summary>
    public static Kleidung KleidungAus(Standgruppe gruppe) => gruppe >= Standgruppe.Patrizier ? Kleidung.Vornehm : Kleidung.Schlicht;

    /// <summary>Ein Aussehen für jemanden ohne Eltern im Spiel: alles gleich wahrscheinlich. Verbraucht immer drei Würfe.</summary>
    internal static Aussehen Wuerfeln(Zufallsquelle z, Volk volk)
    {
        var gesicht = z.Next(Gesichter);
        var haar = HaarJeVolk[volk][z.Next(HaarJeVolk[volk].Length)];
        var augen = AugenJeVolk[volk][z.Next(AugenJeVolk[volk].Length)];
        return new Aussehen(gesicht, haar, augen);
    }

    /// <summary>
    /// Ein festes Aussehen nur aus Nummer und Volk – für Personen aus Spielständen von vor den Porträts.
    /// Ein eigener Generator je Person: Die Würfe der Partie verschieben sich dadurch nicht.
    /// </summary>
    internal static Aussehen Abgeleitet(int nummer, Volk volk) =>
        Wuerfeln(new Zufallsquelle(unchecked(nummer * 7919 + (int)volk * 104_729 + 61)), volk);

    /// <summary>
    /// Das Aussehen eines Kindes (Regeln oben). Fehlt ein Elternteil, erbt es alles vom anderen; fehlen beide, wird gewürfelt.
    /// Verbraucht mit Eltern immer sieben Würfe – so hängt die Folge nicht davon ab, was ein Wurf ergab.
    /// </summary>
    internal static Aussehen Vererben(Zufallsquelle z, Volk volk, Geschlecht geschlecht, Person? vater, Person? mutter)
    {
        if (vater is null && mutter is null) return Wuerfeln(z, volk);
        var gleich = (geschlecht == Geschlecht.Mann ? vater : mutter) ?? (vater ?? mutter)!;
        var anders = (geschlecht == Geschlecht.Mann ? mutter : vater) ?? gleich;
        var gesicht = (z.NextDouble() < GleichesGeschlecht ? gleich : anders).Aussehen.Gesicht;

        Person Elternteil() => z.Next(2) == 0 ? vater ?? mutter! : mutter ?? vater!;
        var haar = Farbe(z, Elternteil().Aussehen.Haar, HaarJeVolk[volk]);
        var augen = Farbe(z, Elternteil().Aussehen.Augen, AugenJeVolk[volk]);
        return new Aussehen(gesicht, haar, augen);
    }

    /// <summary>Die geerbte Farbe – oder mit <see cref="Abweichung"/> eine andere der Palette. Verbraucht immer zwei Würfe.</summary>
    private static T Farbe<T>(Zufallsquelle z, T geerbt, T[] palette) where T : struct, Enum
    {
        var abweichen = z.NextDouble() < Abweichung;
        var wurf = z.NextDouble();
        if (Array.IndexOf(palette, geerbt) < 0) return palette[(int)(wurf * palette.Length)];
        if (!abweichen) return geerbt;
        var andere = palette.Where(f => !f.Equals(geerbt)).ToArray();
        return andere.Length == 0 ? geerbt : andere[(int)(wurf * andere.Length)];
    }
}
