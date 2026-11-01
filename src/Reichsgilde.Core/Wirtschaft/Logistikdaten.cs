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

    /// <summary>
    /// Wie schnell der Karren fährt: Meter je echter Sekunde bei Tempo 1 und Standard-Rundendauer (ab Datenversion 65, Nutzer 31.10.2026) –
    /// so schnell, wie man ihn im Bild fahren sieht (Ochse 0,8, Handkarren 1,0, Pferd 1,3). Gilt auf ebenem Fahrweg (dem Bezugsbelag).
    /// 0: nicht angegeben (alte Daten).
    /// </summary>
    public double Tempo { get; init; }

    /// <summary>Das <see cref="Tempo"/> in Metern je Spielminute (<see cref="Taktgeber.SekundenJeSpielminute"/>): ein Handkarren 0,79.</summary>
    public double MeterJeMinute => Tempo * Taktgeber.SekundenJeSpielminute;

    public int Gesamtkapazitaet => Sorten * KapazitaetJeSorte;
}

/// <summary>
/// Fahrzeit nach Weglänge (Abschnitt "logistik.weglaenge", ab Datenversion 65; Entscheidung des Nutzers vom 31.10.2026). Hat die Karte ein
/// Karrennetz und der Betrieb einen Platz, fährt ein Karren seinen Weg zum Markt mit dem Tempo seiner Art (<see cref="KarrenDaten.Tempo"/>):
/// Fahrzeit = Länge ÷ Tempo, jedes Stück mit seiner Steigung und seinem Belag, dazu Furten und Fähren – so lange, wie man ihn im Bild fahren
/// sieht. Ohne Karte, ohne Platz oder ohne Weg gelten die festen Fahrzeiten (<see cref="LogistikDaten.Fahrzeiten"/>). Null: feste Fahrzeiten
/// wie vor Datenversion 65.
/// </summary>
public sealed class WeglaengeDaten
{
    /// <summary>
    /// So viele Spielminuten dauert jede Fahrt mindestens: Anspannen, vom Hof fahren, sich am Markt einreihen. Wirkt erst unter etwa 8 m Weg
    /// (Handkarren) – vor allem für einen Betrieb direkt am Markt, der sonst im Minutentakt pendelte.
    /// </summary>
    public int MindestMinuten { get; init; } = 10;

    /// <summary>
    /// So viele Spielminuten dauert eine Fahrt höchstens, auch auf sehr langen Wegen: Ein Karren soll an einem Arbeitstag (8 bis 20 Uhr,
    /// 720 Minuten) wenigstens einmal hin und zurück kommen. Das Bild fährt den Karren dann trotzdem in seinem festen Tempo (Darstellung,
    /// Entscheidung 31.10.2026: nie gestaucht); er kommt später an, als der Kern verkauft, und kehrt am Markt sofort um.
    /// </summary>
    public int HoechstMinuten { get; init; } = 360;

    internal void Pruefen(List<string> f, IReadOnlyDictionary<string, KarrenDaten> karren)
    {
        if (MindestMinuten < 1) f.Add("logistik.weglaenge.mindestMinuten muss mindestens 1 sein.");
        if (HoechstMinuten < MindestMinuten) f.Add("logistik.weglaenge.hoechstMinuten darf nicht kleiner als mindestMinuten sein.");
        foreach (var (name, k) in karren)
            if (k.Tempo is <= 0 or > 10) f.Add($"Karren {name}: tempo (Meter je Sekunde) muss über 0 und höchstens 10 sein, wenn logistik.weglaenge gilt.");
    }
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

    /// <summary>Faktor für Fahrten in der Dunklen Nacht (20–23 Uhr).</summary>
    public double FaktorNacht { get; init; } = 2;
}

/// <summary>
/// Die Stadttore (Abschnitt "logistik.tore", ab Datenversion 30). Ab <see cref="SchliessenUm"/> Uhr sind sie zu,
/// mit Tagesbeginn (04:00) wieder offen. Umlandkarren kommen dann nur mit Nachtfahrt-Erlaubnis gegen
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
/// um <see cref="ZuschlagJeProzent"/>, höchstens das <see cref="HoechstensFaktor"/>-fache. Bergab und in der Ebene wie bisher. Nach Weglänge
/// (ab Datenversion 65) dauert jedes Stück Länge ÷ Tempo × Faktor; mit den festen Fahrzeiten wird die Zeit mit dem Mittel über die Strecke
/// (nach Länge gewichtet) malgenommen – eine flache Karte bleibt dort genau, wie sie war.
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

/// <summary>
/// Wie Wasser die Karren aufhält (Abschnitt "logistik.wasser", ab Datenversion 51; Kartenwerkstatt, Schritt 5c). Fährt ein Karren über das
/// Karrennetz (<see cref="Bauen.Karrennetz"/>) durch eine Furt, dauert die Fahrt <see cref="FurtMinuten"/> länger; setzt er mit einer Fähre über,
/// <see cref="FaehreMinuten"/> (Warten und Überfahrt). Brücken kosten nichts. Eine Karte ohne Furt und Fähre bleibt genau, wie sie war.
/// </summary>
public sealed class WasserDaten
{
    /// <summary>So viele Spielminuten länger dauert eine Fahrt je Furt.</summary>
    public int FurtMinuten { get; init; } = 5;

    /// <summary>So viele Spielminuten länger dauert eine Fahrt je Fähre (warten und übersetzen).</summary>
    public int FaehreMinuten { get; init; } = 30;

    /// <summary>
    /// Die Minuten, die eine Fahrt durch Furten und über Fähren länger dauert: je Furt bzw. Fähre einmal (mehrere Stücke hintereinander auf
    /// derselben Fahrbahn sind ein Übergang).
    /// </summary>
    public int Aufschlag(IReadOnlyList<Bauen.Fahrtstueck> fahrt)
    {
        var minuten = 0;
        var vorher = Bauen.Fahrbahn.Strasse;
        foreach (var s in fahrt)
        {
            if (s.Art != vorher)
                minuten += s.Art switch
                {
                    Bauen.Fahrbahn.Furt => FurtMinuten,
                    Bauen.Fahrbahn.Faehre => FaehreMinuten,
                    _ => 0,
                };
            vorher = s.Art;
        }
        return minuten;
    }

    internal void Pruefen(List<string> f)
    {
        if (FurtMinuten < 0) f.Add("logistik.wasser.furtMinuten darf nicht negativ sein.");
        if (FaehreMinuten < 0) f.Add("logistik.wasser.faehreMinuten darf nicht negativ sein.");
    }
}

/// <summary>Alle Logistik-Stellschrauben (Abschnitt "logistik").</summary>
public sealed class LogistikDaten
{
    /// <summary>
    /// Fahrzeit vom Betrieb zum Markt in Spielminuten, je Arbeitsort ("Werkstatt", "Umland"). Seit Datenversion 65 nur noch der Ersatz, wenn
    /// sich keine Weglänge rechnen lässt (keine Karte, kein Platz, kein Weg; <see cref="Weglaenge"/>).
    /// </summary>
    public Dictionary<string, int> Fahrzeiten { get; init; } = new() { ["Werkstatt"] = 30, ["Umland"] = 120 };

    /// <summary>Wie viele Stück je Ware ein Betriebslager fasst.</summary>
    public int LagerJeWare { get; init; } = 40;

    /// <summary>Ab welchem Füllgrad der Verwalter losfährt (0,75 = drei Viertel).</summary>
    public double AbfahrtAbFuellgrad { get; init; } = 0.75;

    /// <summary>Unter wie vielen herstellbaren Stück der Verwalter Zutaten nachkauft.</summary>
    public int NachkaufUnterStueck { get; init; } = 5;

    /// <summary>
    /// Für wie viele Arbeitsstunden der Verwalter Zutaten vorrätig hält (PR #232): Er kauft jede Zutat nur bis zum Bedarf dieser Stunden
    /// bei der jetzigen Belegschaft auf – mindestens für doppelt so viele Stück, wie <see cref="NachkaufUnterStueck"/> sagt, höchstens bis zur Lagergrenze.
    /// </summary>
    public double ZutatenVorratStunden { get; init; } = 4;

    /// <summary>
    /// Welchen Anteil seines freien Geldes der Verwalter auf einer Fahrt höchstens für Zutaten ausgibt (PR #232). Frei ist, was über
    /// die Kosten des Jahresabschlusses hinausgeht (<see cref="Stadtwirtschaft.OffeneJahreskosten"/>: Löhne, Wächter und Unterhalt der Betriebe,
    /// Schiffe, Wohnhaus, Haushalt, Ausbildung). Jede Fahrt nimmt so nur einen Teil des Rests; mehrere Fahrten verbrauchen ihn nach und nach.
    /// Stockt die Werkstatt, kauft er trotzdem die Zutaten für <see cref="NachkaufUnterStueck"/> Stück, solange Geld da ist.
    /// </summary>
    public double ZutatenAnteilKasse { get; init; } = 0.5;

    /// <summary>Spätestens nach so vielen Spielminuten fährt der Verwalter mit der Ware los, die da ist.</summary>
    public int MaxWartezeitMinuten { get; init; } = 240;

    /// <summary>Ein eigener Karren bringt beim Verkauf diesen Anteil seines Kaufpreises (0,5 = die Hälfte; ab Datenversion 55, PR #205).</summary>
    public double KarrenVerkaufAnteil { get; init; } = 0.5;

    public Dictionary<string, KarrenDaten> Karren { get; init; } = new();
    public Dictionary<string, EskorteDaten> Eskorten { get; init; } = new();
    public UeberfallDaten Ueberfall { get; init; } = new();
    public List<BandeDaten> Banden { get; init; } = new();

    /// <summary>Stadttore mit Torschluss (null: keine Tore, wie vor Datenversion 30).</summary>
    public ToreDaten? Tore { get; init; }

    /// <summary>Wie Steigung die Karren bremst (null: gar nicht, wie vor Datenversion 50).</summary>
    public SteigungDaten? Steigung { get; init; }

    /// <summary>Wie Furten und Fähren die Karren aufhalten (null: gar nicht, wie vor Datenversion 51).</summary>
    public WasserDaten? Wasser { get; init; }

    /// <summary>Fahrzeit nach Weglänge und Karrenart (null: die festen <see cref="Fahrzeiten"/>, wie vor Datenversion 65).</summary>
    public WeglaengeDaten? Weglaenge { get; init; }

    public int FahrzeitAb(Arbeitsort ort) => Fahrzeiten.GetValueOrDefault(ort.ToString(), 30);

    internal void Pruefen(List<string> f)
    {
        foreach (var ort in Enum.GetNames<Arbeitsort>())
            if (!Fahrzeiten.TryGetValue(ort, out var minuten) || minuten <= 0)
                f.Add($"logistik.fahrzeiten.{ort} fehlt oder ist nicht größer als 0.");
        if (LagerJeWare <= 0) f.Add("logistik.lagerJeWare muss größer als 0 sein.");
        Tore?.Pruefen(f);
        Steigung?.Pruefen(f);
        Wasser?.Pruefen(f);
        Weglaenge?.Pruefen(f, Karren);
        if (AbfahrtAbFuellgrad is <= 0 or > 1) f.Add("logistik.abfahrtAbFuellgrad muss zwischen 0 und 1 liegen.");
        if (MaxWartezeitMinuten <= 0) f.Add("logistik.maxWartezeitMinuten muss größer als 0 sein.");
        if (ZutatenVorratStunden <= 0) f.Add("logistik.zutatenVorratStunden muss größer als 0 sein.");
        if (ZutatenAnteilKasse is <= 0 or > 1) f.Add("logistik.zutatenAnteilKasse muss größer als 0 und höchstens 1 sein.");
        if (KarrenVerkaufAnteil is < 0 or > 1) f.Add("logistik.karrenVerkaufAnteil muss zwischen 0 und 1 liegen.");
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
