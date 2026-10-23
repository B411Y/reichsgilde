using Reichsgilde.Core.Zeit;
namespace Reichsgilde.Core.Wirtschaft;

/// <summary>Ein Karrentyp (Abschnitt "logistik.karren").</summary>
public sealed class KarrenDaten
{
    /// <summary>
    /// Zahl der Ladeflächen. Auf jeder Ladefläche liegt genau eine Warensorte;
    /// eine Sorte darf auch mehrere Ladeflächen belegen (z. B. 2 × 20 Eisen).
    /// </summary>
    public int Sorten { get; init; } = 1;

    /// <summary>Stück je Ladefläche.</summary>
    public int KapazitaetJeSorte { get; init; } = 15;

    /// <summary>Kaufpreis in Gulden.</summary>
    public double Preis { get; init; }

    public int Gesamtkapazitaet => Sorten * KapazitaetJeSorte;
}

/// <summary>Eine Eskortenstufe (Abschnitt "logistik.eskorten").</summary>
public sealed class EskorteDaten
{
    /// <summary>Kosten je beladener Fahrt. Leere Karren kosten nichts.</summary>
    public double KostenJeFahrt { get; init; }

    /// <summary>Schutz von 0 (keiner) bis 1 (vollständig). Senkt die Erfolgschance einer Bande.</summary>
    public double Schutz { get; init; }

    /// <summary>Stellt die Wache (Gardist): nur verfügbar, wenn ein Haus der Wache Geleitschutz gibt – der Lohn geht an dieses Haus.</summary>
    public bool Wache { get; init; }
}

/// <summary>
/// Eine Art von Bande (Abschnitt "logistik.banden"). Banditen gibt es in allen Völkern,
/// oft in gemischten Gruppen. Wilde Ork-Rotten und Oger kommen aus der Wildnis dazu.
/// </summary>
public sealed class BandeDaten
{
    public string Name { get; init; } = "";

    /// <summary>Zusammensetzung: Volk → Anzahl, z. B. { "Mensch": 2, "Halbling": 1, "Ork": 1 }.</summary>
    public Dictionary<string, int> Voelker { get; init; } = new();

    /// <summary>Kampfkraft. Erfolgschance eines Überfalls = Stärke × (1 − Schutz der Eskorte), höchstens 100 %.</summary>
    public double Staerke { get; init; } = 1;

    /// <summary>Relative Häufigkeit gegenüber den anderen Banden.</summary>
    public double Gewicht { get; init; } = 1;

    /// <summary>Wilde Bestien aus der Wildnis statt Gesetzloser aus der Stadt.</summary>
    public bool Wild { get; init; }

    /// <summary>„2 Menschen, 1 Halbling, 1 Ork".</summary>
    public string ZusammensetzungText => string.Join(", ",
        Voelker.Select(v => Enum.TryParse<Volk>(v.Key, out var volk) ? volk.MitAnzahl(v.Value) : $"{v.Value} {v.Key}"));
}

/// <summary>Wahrscheinlichkeit von Überfällen (Abschnitt "logistik.ueberfall").</summary>
public sealed class UeberfallDaten
{
    /// <summary>Chance, dass eine beladene Umland-Fahrt bei Tag angegriffen wird.</summary>
    public double ChanceTag { get; init; } = 0.08;

    /// <summary>Faktor für Fahrten in der Dunklen Nacht (20–22 Uhr).</summary>
    public double FaktorNacht { get; init; } = 2;
}

/// <summary>
/// Die Stadttore (Abschnitt "logistik.tore", ab Datenversion 30). Ab <see cref="SchliessenUm"/> Uhr sind sie zu,
/// mit Tagesbeginn (05:00) wieder offen. Umlandkarren kommen dann nur mit Nachtfahrt-Erlaubnis gegen
/// <see cref="Torgeld"/> durch – sonst warten sie bis zum Morgen am Tor. Fehlt der Abschnitt, gibt es keine Tore.
/// </summary>
public sealed class ToreDaten
{
    /// <summary>Volle Stunde, ab der die Tore zu sind (Dunkle Nacht: 20).</summary>
    public int SchliessenUm { get; init; } = 20;

    /// <summary>Was die Torwache je Durchfahrt nach Torschluss verlangt (Gulden).</summary>
    public double Torgeld { get; init; } = 15;

    /// <summary>Sind die Tore zu dieser Spielzeit geschlossen?</summary>
    public bool IstZu(Spielzeit zeit) => zeit.UhrzeitInMinuten >= SchliessenUm * 60;

    internal void Pruefen(List<string> f)
    {
        if (SchliessenUm * 60 <= Spielzeit.TagesbeginnMinute || SchliessenUm * 60 > Spielzeit.TagesendeMinute)
            f.Add($"logistik.tore.schliessenUm muss zwischen {Spielzeit.TagesbeginnMinute / 60 + 1} und {Spielzeit.TagesendeMinute / 60} liegen.");
        if (Torgeld < 0) f.Add("logistik.tore.torgeld darf nicht negativ sein.");
    }
}

/// <summary>
/// Wie Steigung die Karren bremst (Abschnitt "logistik.steigung", ab Datenversion 50; Kartenwerkstatt, Schritt 5b). Jedes Stück der Fahrt
/// über die Karrenstraßen (<see cref="Bauen.Karrennetz"/>) dauert bergauf ab <see cref="AbProzent"/> Steigung länger: je Prozentpunkt darüber
/// um <see cref="ZuschlagJeProzent"/>, höchstens das <see cref="HoechstensFaktor"/>-fache. Bergab und in der Ebene wie bisher. Die feste
/// Fahrzeit des Arbeitsorts wird mit dem Mittel über die Strecke (nach Länge gewichtet) malgenommen – eine flache Karte bleibt genau, wie sie war.
/// </summary>
public sealed class SteigungDaten
{
    /// <summary>Ab dieser Steigung (Prozent) bremst es.</summary>
    public double AbProzent { get; init; } = 6;

    /// <summary>So viel länger je Prozentpunkt über <see cref="AbProzent"/> (0,05 = 5 %).</summary>
    public double ZuschlagJeProzent { get; init; } = 0.05;

    /// <summary>Höchstens so viel mal so lang dauert ein Stück.</summary>
    public double HoechstensFaktor { get; init; } = 3;

    /// <summary>Wie viel mal so lang ein Stück mit dieser Steigung (Anteil, 0,1 = 10 %; negativ = bergab) dauert.</summary>
    public double Faktor(double steigung)
    {
        var prozent = steigung * 100;
        return prozent <= AbProzent ? 1 : Math.Min(HoechstensFaktor, 1 + (prozent - AbProzent) * ZuschlagJeProzent);
    }

    /// <summary>Der Zeitfaktor einer ganzen Fahrt: die Faktoren der Stücke nach ihrer Länge gemittelt (1 ohne Stücke).</summary>
    public double Faktor(IReadOnlyList<Bauen.Fahrtstueck> fahrt)
    {
        var laenge = fahrt.Sum(s => s.Laenge);
        return laenge <= 0 ? 1 : fahrt.Sum(s => s.Laenge * Faktor(s.Steigung)) / laenge;
    }

    internal void Pruefen(List<string> f)
    {
        if (AbProzent < 0) f.Add("logistik.steigung.abProzent darf nicht negativ sein.");
        if (ZuschlagJeProzent < 0) f.Add("logistik.steigung.zuschlagJeProzent darf nicht negativ sein.");
        if (HoechstensFaktor < 1) f.Add("logistik.steigung.hoechstensFaktor muss mindestens 1 sein.");
    }
}

/// <summary>Alle Logistik-Stellschrauben (Abschnitt "logistik").</summary>
public sealed class LogistikDaten
{
    /// <summary>Fahrzeit vom Betrieb zum Markt in Spielminuten, je Arbeitsort ("Werkstatt", "Umland").</summary>
    public Dictionary<string, int> Fahrzeiten { get; init; } = new() { ["Werkstatt"] = 30, ["Umland"] = 120 };

    /// <summary>Wie viele Stück je Ware ein Betriebslager fasst.</summary>
    public int LagerJeWare { get; init; } = 40;

    /// <summary>Ab welchem Füllgrad der Verwalter losfährt (0,75 = drei Viertel).</summary>
    public double AbfahrtAbFuellgrad { get; init; } = 0.75;

    /// <summary>Unter wie vielen herstellbaren Stück der Verwalter Zutaten nachkauft.</summary>
    public int NachkaufUnterStueck { get; init; } = 5;

    /// <summary>Spätestens nach so vielen Spielminuten fährt der Verwalter mit der Ware los, die da ist.</summary>
    public int MaxWartezeitMinuten { get; init; } = 240;

    public Dictionary<string, KarrenDaten> Karren { get; init; } = new();
    public Dictionary<string, EskorteDaten> Eskorten { get; init; } = new();
    public UeberfallDaten Ueberfall { get; init; } = new();
    public List<BandeDaten> Banden { get; init; } = new();

    /// <summary>Stadttore mit Torschluss (null: keine Tore, wie vor Datenversion 30).</summary>
    public ToreDaten? Tore { get; init; }

    /// <summary>Wie Steigung die Karren bremst (null: gar nicht, wie vor Datenversion 50).</summary>
    public SteigungDaten? Steigung { get; init; }

    public int FahrzeitAb(Arbeitsort ort) => Fahrzeiten.GetValueOrDefault(ort.ToString(), 30);

    internal void Pruefen(List<string> f)
    {
        foreach (var ort in Enum.GetNames<Arbeitsort>())
            if (!Fahrzeiten.TryGetValue(ort, out var minuten) || minuten <= 0)
                f.Add($"logistik.fahrzeiten.{ort} fehlt oder ist nicht größer als 0.");
        if (LagerJeWare <= 0) f.Add("logistik.lagerJeWare muss größer als 0 sein.");
        Tore?.Pruefen(f);
        Steigung?.Pruefen(f);
        if (AbfahrtAbFuellgrad is <= 0 or > 1) f.Add("logistik.abfahrtAbFuellgrad muss zwischen 0 und 1 liegen.");
        if (MaxWartezeitMinuten <= 0) f.Add("logistik.maxWartezeitMinuten muss größer als 0 sein.");
        if (Karren.Count == 0) f.Add("logistik.karren: Es ist kein Karrentyp definiert.");
        foreach (var (name, k) in Karren)
            if (k.Sorten <= 0 || k.KapazitaetJeSorte <= 0) f.Add($"Karren {name}: sorten und kapazitaetJeSorte müssen größer als 0 sein.");
        if (Eskorten.Count == 0) f.Add("logistik.eskorten: Es ist keine Eskortenstufe definiert.");
        foreach (var (name, e) in Eskorten)
            if (e.Schutz is < 0 or > 1) f.Add($"Eskorte {name}: schutz muss zwischen 0 und 1 liegen.");
        if (Ueberfall.ChanceTag is < 0 or > 1) f.Add("logistik.ueberfall.chanceTag muss zwischen 0 und 1 liegen.");
        if (Banden.Count == 0) f.Add("logistik.banden: Es ist keine Bande definiert.");
        foreach (var b in Banden)
        {
            if (b.Gewicht <= 0) f.Add($"Bande {b.Name}: gewicht muss größer als 0 sein.");
            if (b.Voelker.Count == 0) f.Add($"Bande {b.Name}: Es fehlen die Völker.");
            foreach (var volk in b.Voelker.Keys)
                if (!Enum.TryParse<Volk>(volk, out _))
                    f.Add($"Bande {b.Name}: unbekanntes Volk „{volk}“ (erlaubt: {string.Join(", ", Enum.GetNames<Volk>())}).");
        }
    }
}
