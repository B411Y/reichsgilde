namespace Reichsgilde.Core.Wirtschaft;

// ═════════════════════════════════════════════════════════════════════════════
//  Daten des Schiffshandels (Abschnitt „schiffshandel“ in wirtschaft.json, ab Datenversion 53; PR #199)
//
//  Am Kartenrand führt der Fluss zu fernen Städten – flussauf und flussab. Ein Haus kauft am Hafen ein Schiff und schickt es mit
//  einem Auftrag los: Hinfracht (am Markt der Stadt gekauft oder aus dem Lager eines eigenen Betriebs), dort verkaufen, Rückfracht
//  kaufen, heimbringen (ins Lager oder auf den Markt). Fehlt der Abschnitt oder hat die Karte keinen Hafen, gibt es keinen Schiffshandel.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Ein Schiffstyp (Abschnitt „schiffshandel.schiffe“), z. B. das Flussschiff.</summary>
public sealed class SchiffstypDaten
{
    /// <summary>Zahl der Laderäume. Jeder Laderaum fasst genau eine Warensorte; eine Sorte darf mehrere belegen.</summary>
    public int Sorten { get; init; } = 4;

    /// <summary>Stück je Laderaum.</summary>
    public int KapazitaetJeSorte { get; init; } = 30;

    /// <summary>Kaufpreis in Gulden (Investition).</summary>
    public double Preis { get; init; }

    /// <summary>Unterhalt im Jahr (Mannschaft, Pech, Segeltuch) – als Fixkosten zum Jahresende.</summary>
    public double Unterhalt { get; init; }

    /// <summary>Spielminuten für eine Strecke der Länge 1 flussab (mit der Strömung).</summary>
    public int MinutenFlussab { get; init; } = 60;

    /// <summary>Spielminuten für eine Strecke der Länge 1 flussauf (gegen die Strömung: getreidelt oder gesegelt).</summary>
    public int MinutenFlussauf { get; init; } = 100;

    /// <summary>So lange wird am Kai beladen, bevor das Schiff ablegt (Spielminuten).</summary>
    public int LadeMinuten { get; init; } = 20;

    /// <summary>So lange liegt das Schiff in der fernen Stadt (verkaufen, kaufen; Spielminuten).</summary>
    public int LiegeMinuten { get; init; } = 30;

    /// <summary>Tempo auf der sichtbaren Strecke der Karte in Metern je Spielminute (flussab; flussauf im Verhältnis der Minuten langsamer).</summary>
    public double MeterJeMinute { get; init; } = 8;

    public int Gesamtkapazitaet => Sorten * KapazitaetJeSorte;
}

/// <summary>Eine Ware, die eine ferne Stadt kauft oder verkauft.</summary>
public sealed class FernwareDaten
{
    /// <summary>Preis bei ausgeglichenem Markt als Vielfaches des Grundpreises der Ware (0,5 = halber Preis).</summary>
    public double Faktor { get; init; } = 1;

    /// <summary>
    /// Wie tief der Markt ist (Stück). Jedes gehandelte Stück verschiebt den Preis: Wer n Stück verkauft hat, bekommt
    /// Faktor × (Menge ÷ (Menge + n))^Elastizität; wer n Stück gekauft hat, zahlt Faktor × ((Menge + n) ÷ Menge)^Elastizität.
    /// </summary>
    public double Menge { get; init; } = 100;
}

/// <summary>Eine ferne Stadt am Fluss, außerhalb der Karte.</summary>
public sealed class FerneStadtDaten
{
    public string Name { get; init; } = "";

    /// <summary>„flussauf“ oder „flussab“ vom Hafen aus.</summary>
    public string Richtung { get; init; } = Flussauf;

    /// <summary>Länge der Strecke (1 = Minuten des Schiffstyps); die Fahrtzeit ist Länge × Minuten flussab bzw. flussauf.</summary>
    public double Entfernung { get; init; } = 1;

    /// <summary>So viele Zollstellen liegen auf dem Weg; jede nimmt je beladener Fahrt <see cref="SchiffshandelDaten.ZollJeStelle"/> vom Wert der Ladung.</summary>
    public int Zollstellen { get; init; }

    /// <summary>Was die Stadt verkauft (wir kaufen dort ein): Ware → Preis und Tiefe.</summary>
    public Dictionary<string, FernwareDaten> Verkauft { get; init; } = new();

    /// <summary>Was die Stadt kauft (wir verkaufen dort): Ware → Preis und Tiefe.</summary>
    public Dictionary<string, FernwareDaten> Kauft { get; init; } = new();

    public const string Flussauf = "flussauf", Flussab = "flussab";

    /// <summary>Liegt die Stadt flussauf (gegen die Strömung)?</summary>
    public bool IstFlussauf => Richtung == Flussauf;
}

/// <summary>Flusspiraten (Abschnitt „schiffshandel.piraten“): wie die Banden an den Karren, mit Geleit als Schutz.</summary>
public sealed class PiratenDaten
{
    public string Name { get; init; } = "Flusspiraten";

    /// <summary>Chance, dass eine beladene Fahrt (hin oder zurück) angegriffen wird.</summary>
    public double Chance { get; init; } = 0.05;

    /// <summary>Kampfkraft. Erfolgschance = Stärke × (1 − Schutz des Geleits), höchstens 100 %.</summary>
    public double Staerke { get; init; } = 1;

    /// <summary>Welcher Anteil jeder Ware der Ladung verloren geht, wenn der Überfall gelingt (aufgerundet).</summary>
    public double BeuteAnteil { get; init; } = 0.5;
}

/// <summary>Alle Stellschrauben des Schiffshandels (Abschnitt „schiffshandel“).</summary>
public sealed class SchiffshandelDaten
{
    public Dictionary<string, SchiffstypDaten> Schiffe { get; init; } = new();

    /// <summary>Die fernen Städte (Namen je Karte später in der Kartenwerkstatt, Schritt 6).</summary>
    public List<FerneStadtDaten> Staedte { get; init; } = new();

    /// <summary>Anteil vom Wert der Ladung (zum Marktpreis der Heimatstadt), den jede Zollstelle je beladener Fahrt nimmt.</summary>
    public double ZollJeStelle { get; init; } = 0.02;

    /// <summary>
    /// Je Spielstunde holt der Markt einer fernen Stadt diesen Anteil auf: Gehandelte Stück zählen danach um so viel weniger
    /// (0,08: nach einem Spieljahr von 17 Stunden ist noch ein Viertel übrig).
    /// </summary>
    public double ErholungJeStunde { get; init; } = 0.08;

    public PiratenDaten Piraten { get; init; } = new();

    /// <summary>Das Geleit eines Schiffs kostet so viel mal die Eskorte eines Karrens (<c>logistik.eskorten</c>).</summary>
    public double GeleitFaktor { get; init; } = 3;

    /// <summary>Wer ein Schiff verkauft, bekommt diesen Anteil des Kaufpreises zurück.</summary>
    public double VerkaufAnteil { get; init; } = 0.5;

    /// <summary>Ein Hafen gilt als Liegeplatz an einem Fluss, wenn dessen Mittellinie höchstens so weit (Meter) von der Kaimauer entfernt ist.</summary>
    public double HafenAbstand { get; init; } = 40;

    internal void Pruefen(List<string> f, IReadOnlyDictionary<string, WareDaten> waren, IReadOnlyDictionary<string, EskorteDaten> eskorten)
    {
        const string p = "schiffshandel";
        if (Schiffe.Count == 0) f.Add($"{p}.schiffe: Es ist kein Schiffstyp definiert.");
        foreach (var (name, s) in Schiffe)
        {
            if (s.Sorten <= 0 || s.KapazitaetJeSorte <= 0) f.Add($"{p}.schiffe.{name}: sorten und kapazitaetJeSorte müssen größer als 0 sein.");
            if (s.Preis < 0 || s.Unterhalt < 0) f.Add($"{p}.schiffe.{name}: preis und unterhalt dürfen nicht negativ sein.");
            if (s.MinutenFlussab <= 0 || s.MinutenFlussauf <= 0) f.Add($"{p}.schiffe.{name}: minutenFlussab und minutenFlussauf müssen größer als 0 sein.");
            if (s.LadeMinuten < 0 || s.LiegeMinuten < 0) f.Add($"{p}.schiffe.{name}: ladeMinuten und liegeMinuten dürfen nicht negativ sein.");
            if (s.MeterJeMinute <= 0) f.Add($"{p}.schiffe.{name}.meterJeMinute muss größer als 0 sein.");
        }
        if (Staedte.Count == 0) f.Add($"{p}.staedte: Es ist keine ferne Stadt definiert.");
        foreach (var doppelt in Staedte.GroupBy(s => s.Name).Where(g => g.Count() > 1))
            f.Add($"{p}.staedte: „{doppelt.Key}“ ist doppelt eingetragen.");
        foreach (var s in Staedte)
        {
            var wer = $"{p}.staedte „{s.Name}“";
            if (string.IsNullOrWhiteSpace(s.Name)) f.Add($"{p}.staedte: Eine Stadt hat keinen Namen.");
            if (s.Richtung is not (FerneStadtDaten.Flussauf or FerneStadtDaten.Flussab)) f.Add($"{wer}: richtung muss „flussauf“ oder „flussab“ sein.");
            if (s.Entfernung <= 0) f.Add($"{wer}: entfernung muss größer als 0 sein.");
            if (s.Zollstellen < 0) f.Add($"{wer}: zollstellen darf nicht negativ sein.");
            if (s.Verkauft.Count + s.Kauft.Count == 0) f.Add($"{wer}: Die Stadt kauft und verkauft nichts.");
            foreach (var (ware, d) in s.Verkauft.Concat(s.Kauft))
            {
                if (!waren.ContainsKey(ware)) f.Add($"{wer}: Die Ware „{ware}“ gibt es nicht.");
                if (d.Faktor <= 0 || d.Menge <= 0) f.Add($"{wer}, {ware}: faktor und menge müssen größer als 0 sein.");
            }
            foreach (var ware in s.Verkauft.Keys.Where(s.Kauft.ContainsKey))
                f.Add($"{wer}: „{ware}“ steht unter kauft und verkauft – eine Stadt handelt eine Ware nur in eine Richtung.");
        }
        if (ZollJeStelle is < 0 or >= 1) f.Add($"{p}.zollJeStelle muss zwischen 0 und 1 liegen.");
        if (ErholungJeStunde is < 0 or > 1) f.Add($"{p}.erholungJeStunde muss zwischen 0 und 1 liegen.");
        if (Piraten.Chance is < 0 or > 1) f.Add($"{p}.piraten.chance muss zwischen 0 und 1 liegen.");
        if (Piraten.Staerke < 0) f.Add($"{p}.piraten.staerke darf nicht negativ sein.");
        if (Piraten.BeuteAnteil is <= 0 or > 1) f.Add($"{p}.piraten.beuteAnteil muss über 0 und höchstens 1 sein.");
        if (GeleitFaktor < 0) f.Add($"{p}.geleitFaktor darf nicht negativ sein.");
        if (VerkaufAnteil is < 0 or > 1) f.Add($"{p}.verkaufAnteil muss zwischen 0 und 1 liegen.");
        if (HafenAbstand <= 0) f.Add($"{p}.hafenAbstand muss größer als 0 sein.");
        if (eskorten.Count > 0 && !eskorten.ContainsKey("Keine")) f.Add($"{p}: Das Geleit „Keine“ fehlt unter logistik.eskorten.");
    }
}
