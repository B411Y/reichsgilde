namespace Reichsgilde.Core.Dynastie;

/// <summary>Eine Stufe des Stresses (Abschnitt "stress.stufen"): ab welchem Wert sie gilt und was sie bewirkt. Unter der ersten ist die Person ruhig.</summary>
public sealed class StressStufeDaten
{
    /// <summary>„angespannt“, „zermürbt“ – für das Charakterfenster („wirkt angespannt“).</summary>
    public string Name { get; init; } = "";

    /// <summary>Ab diesem Stress gilt die Stufe.</summary>
    public double Ab { get; init; }

    /// <summary>Faktor auf jede Erfahrung der Lebensweise (0,75 bzw. 0,5).</summary>
    public double Erfahrung { get; init; } = 1;

    /// <summary>So viel mehr Gefahr bei Aktivitäten des Oberhaupts (0,05 = +5 Prozentpunkte).</summary>
    public double Gefahr { get; init; }

    /// <summary>So viele Sterne weniger in jedem Talent (0,5 – wie eine leichte Krankheit).</summary>
    public double Talente { get; init; }

    /// <summary>Faktor auf die Chance zu erkranken (1,25).</summary>
    public double Krankheit { get; init; } = 1;

    /// <summary>So viel Meinung hat der Ehepartner über die Person (−5, „unleidlich“).</summary>
    public double MeinungEhepartner { get; init; }

    /// <summary>Faktor auf die Vorsicht eines KI-Hauses mit diesem Oberhaupt (1,2).</summary>
    public double KiVorsicht { get; init; } = 1;
}

/// <summary>
/// Ein Zug, der eine Handlung nicht mag (Abschnitt "stress.taten"): Wer ihn hat und die Tat begeht, bekommt Stress.
/// <see cref="Tat"/> ist ein Befehl („Erpressen“), Befehl und Art („Beauftragen:Ueberfall“, „WohnhausAusbauen:Stadtpalais“) oder ein
/// Ereignis aus <see cref="StressDaten.Ereignisse"/> (Bestechung, Geschenk, Ketzerei, Buße, Pranger, verfallene Aktionspunkte).
/// </summary>
public sealed class StressTatDaten
{
    public string Zug { get; init; } = "";
    public string Tat { get; init; } = "";
    public double Stress { get; init; }

    /// <summary>Nur ab diesem Betrag (Geschenk ab 200 Gulden); 0 = immer.</summary>
    public double AbGulden { get; init; }

    /// <summary>Was die Person tat, für den Tooltip („Gaunerstück in Auftrag gegeben“).</summary>
    public string Name { get; init; } = "";
}

/// <summary>Trauer um einen Angehörigen (Abschnitt "stress.trauer").</summary>
public sealed class StressTrauerDaten
{
    public double Ehepartner { get; init; } = 30;
    public double Kind { get; init; } = 20;
    public double Elternteil { get; init; } = 10;
    public double Geschwister { get; init; } = 5;
}

/// <summary>Sorgen ums Haus (Abschnitt "stress.haus"): Streit, ein Jahr mit Verlust, Bargeld im Minus.</summary>
public sealed class StressHausDaten
{
    /// <summary>Streit im Haus – für Oberhaupt und Erben.</summary>
    public double Streit { get; init; } = 10;

    /// <summary>Betriebsgewinn des Jahres unter 0 (beim Oberhaupt).</summary>
    public double JahrImMinus { get; init; } = 5;

    /// <summary>Bargeld am Jahresende unter 0 (beim Oberhaupt).</summary>
    public double BargeldNegativ { get; init; } = 10;
}

/// <summary>Vor Gericht (Abschnitt "stress.gericht"), beim Oberhaupt des beklagten Hauses.</summary>
public sealed class StressGerichtDaten
{
    public double Angeklagt { get; init; } = 10;
    public double Verurteilt { get; init; } = 15;
    public double Kerker { get; init; } = 25;
}

/// <summary>Krankheit (Abschnitt "stress.krankheit").</summary>
public sealed class StressKrankheitDaten
{
    /// <summary>Die Person selbst erkrankt.</summary>
    public double Erkrankt { get; init; } = 5;

    /// <summary>Die Pest ist im Haus (einmal im Pestjahr für jeden Stressträger des Hauses).</summary>
    public double Pest { get; init; } = 10;
}

/// <summary>Was beim Zusammenbruch geschieht (Abschnitt "stress.zusammenbruch").</summary>
public sealed class StressZusammenbruchDaten
{
    /// <summary>Chance auf einen negativen erworbenen Zug (sonst eine Krankheit).</summary>
    public double ZugChance { get; init; } = 0.5;

    /// <summary>Aus diesen Zügen einer, den die Person noch nicht hat (gleich verteilt). Hat sie alle, gibt es eine Krankheit.</summary>
    public List<string> Zuege { get; init; } = new();

    /// <summary>Aus diesen Krankheiten eine (gleich verteilt). Ist die Person schon krank, gibt es einen Zug.</summary>
    public List<string> Krankheiten { get; init; } = new();
}

/// <summary>
/// Stress (Abschnitt "stress" in wirtschaft.json, ab Datenversion 76, Charaktersystem Teil 3b). Fehlt der Abschnitt, hat niemand Stress
/// (alte Spielstände bringen ihre Daten mit).
///
/// Stress (0 … <see cref="Hoechstwert"/>) haben nur Oberhaupt, Erbe und Ehepartner der Häuser. Er steigt durch Taten gegen den eigenen
/// Charakter (<see cref="Taten"/>), Trauer, Sorgen ums Haus, Gericht und Krankheit; er sinkt jedes Jahr um <see cref="Abklingen"/> und durch
/// Ventile (Aktivitäten aus <see cref="Ventile"/>, je Art einmal im Jahr). Ab den <see cref="Stufen"/> hat er Folgen; beim Höchstwert bricht die
/// Person zusammen (<see cref="Zusammenbruch"/>) und fällt auf <see cref="NachZusammenbruch"/>.
/// </summary>
public sealed class StressDaten
{
    /// <summary>Ereignisse, die als <see cref="StressTatDaten.Tat"/> stehen dürfen (neben den Befehlen).</summary>
    public static readonly string[] Ereignisse = { "Bestechung", "Geschenk", "Ketzerei", "Busse", "Pranger", "ApVerfallen" };

    public double Hoechstwert { get; init; } = 100;
    public double NachZusammenbruch { get; init; } = 60;

    /// <summary>Die Stufen aufsteigend nach <see cref="StressStufeDaten.Ab"/> (Stufe 1, 2 …).</summary>
    public List<StressStufeDaten> Stufen { get; init; } = new();

    /// <summary>So viel sinkt der Stress jedes Jahr.</summary>
    public double Abklingen { get; init; } = 15;

    /// <summary>Glückliche Ehe: Denkt der Ehepartner mindestens so gut über die Person …</summary>
    public double GlueckEheAb { get; init; } = 50;

    /// <summary>… sinkt ihr Stress jedes Jahr um so viel mehr.</summary>
    public double GlueckEhe { get; init; } = 5;

    /// <summary>Abklingen mehr oder weniger je Zug (Schwermütig −5).</summary>
    public Dictionary<string, double> ZugAbklingen { get; init; } = new();

    /// <summary>Aktivitäten, die Stress abbauen (Kennung aus "meinungen.aktivitaeten" → Abbau), je Art einmal im Jahr.</summary>
    public Dictionary<string, double> Ventile { get; init; } = new();

    /// <summary>Faktor auf den Abbau, wenn die Person die Aktivität mag (ein Zug aus ihrer Liste „mag“).</summary>
    public double MagFaktor { get; init; } = 1.5;

    /// <summary>Stress statt Abbau, wenn die Person eine Aktivität nicht mag (ein Zug aus „mag nicht“) – bei jeder Aktivität, einmal je Art und Jahr.</summary>
    public double MagNicht { get; init; } = 8;

    public List<StressTatDaten> Taten { get; init; } = new();
    public StressTrauerDaten Trauer { get; init; } = new();
    public StressHausDaten Haus { get; init; } = new();
    public StressGerichtDaten Gericht { get; init; } = new();
    public StressKrankheitDaten Krankheit { get; init; } = new();
    public StressZusammenbruchDaten Zusammenbruch { get; init; } = new();

    /// <summary>Ab diesem Stress ihres Oberhaupts nutzt die KI übrige Aktionspunkte für ein Ventil.</summary>
    public double KiVentilAb { get; init; } = 20;

    public bool IstAktiv => Hoechstwert > 0 && Stufen.Count > 0;

    /// <summary>
    /// Die Befehle (Teil vor dem Doppelpunkt), die in <see cref="Taten"/> vorkommen – für die schnelle Vorprüfung je Befehl. Einmal gebaut
    /// und geteilt (dieselben Daten nutzen mehrere Partien, die Messung sogar gleichzeitig), darum <see cref="Lazy{T}"/>.
    /// </summary>
    private readonly Lazy<HashSet<string>> _tatBefehle;

    public StressDaten() =>
        _tatBefehle = new Lazy<HashSet<string>>(() => Taten.Select(t => t.Tat.Split(':')[0]).ToHashSet(StringComparer.Ordinal),
            LazyThreadSafetyMode.ExecutionAndPublication);

    internal bool KenntBefehl(string befehl) => _tatBefehle.Value.Contains(befehl);

    /// <param name="zuege">null: ohne Charakterzüge – die Züge wirken dann nur nicht.</param>
    /// <param name="aktivitaeten">null: ohne Meinungen – die Ventile gibt es dann nur nicht.</param>
    /// <param name="krankheiten">null: ohne Gesundheit – ein Zusammenbruch bringt dann nur Züge.</param>
    internal void Pruefen(List<string> f, ICollection<string>? zuege, ICollection<string>? aktivitaeten, ICollection<string>? krankheiten, IEnumerable<string> befehle)
    {
        const string q = "stress";
        if (!IstAktiv) return;
        if (Hoechstwert <= 0) f.Add($"{q}.hoechstwert muss größer als 0 sein.");
        if (NachZusammenbruch < 0 || NachZusammenbruch >= Hoechstwert) f.Add($"{q}.nachZusammenbruch liegt zwischen 0 und dem Höchstwert (ohne ihn).");
        for (var i = 0; i < Stufen.Count; i++)
        {
            var s = Stufen[i];
            var wo = $"{q}.stufen[{i}]";
            if (string.IsNullOrWhiteSpace(s.Name)) f.Add($"{wo}: name fehlt.");
            if (s.Ab <= 0 || s.Ab >= Hoechstwert) f.Add($"{wo}.ab liegt über 0 und unter dem Höchstwert.");
            if (i > 0 && s.Ab <= Stufen[i - 1].Ab) f.Add($"{wo}: Die Stufen müssen nach „ab“ aufsteigend sortiert sein.");
            if (s.Erfahrung is < 0 or > 1 || s.Krankheit < 1 || s.KiVorsicht < 1) f.Add($"{wo}: erfahrung zwischen 0 und 1, krankheit und kiVorsicht ab 1.");
            if (s.Gefahr is < 0 or > 0.5 || s.Talente is < 0 or > 3 || s.MeinungEhepartner is > 0 or < -50)
                f.Add($"{wo}: gefahr 0 … 0,5, talente 0 … 3, meinungEhepartner −50 … 0.");
        }
        if (Abklingen < 0 || GlueckEhe < 0 || MagFaktor < 1 || MagNicht < 0) f.Add($"{q}: abklingen, glueckEhe und magNicht ab 0, magFaktor ab 1.");
        foreach (var z in ZugAbklingen.Keys.Concat(Taten.Select(t => t.Zug)).Concat(Zusammenbruch.Zuege).Distinct().Where(z => zuege is not null && !zuege.Contains(z)))
            f.Add($"{q}: Den Charakterzug „{z}“ gibt es nicht.");
        foreach (var (art, abbau) in Ventile)
        {
            if (aktivitaeten is not null && !aktivitaeten.Contains(art)) f.Add($"{q}.ventile: Die Aktivität „{art}“ gibt es nicht.");
            if (abbau <= 0 || abbau > Hoechstwert) f.Add($"{q}.ventile.{art}: Abbau über 0 und höchstens der Höchstwert.");
        }
        var befehlsnamen = befehle.ToHashSet(StringComparer.Ordinal);
        foreach (var t in Taten)
        {
            var wo = $"{q}.taten ({t.Zug}, {t.Tat})";
            if (!befehlsnamen.Contains(t.Tat.Split(':')[0]) && !Ereignisse.Contains(t.Tat)) f.Add($"{wo}: Einen Befehl oder ein Ereignis „{t.Tat}“ gibt es nicht.");
            if (t.Stress <= 0 || t.Stress > Hoechstwert) f.Add($"{wo}: stress über 0 und höchstens der Höchstwert.");
            if (t.AbGulden < 0) f.Add($"{wo}: abGulden darf nicht negativ sein.");
            if (string.IsNullOrWhiteSpace(t.Name)) f.Add($"{wo}: name fehlt.");
        }
        foreach (var (name, wert) in new[] { ("trauer.ehepartner", Trauer.Ehepartner), ("trauer.kind", Trauer.Kind), ("trauer.elternteil", Trauer.Elternteil),
                     ("trauer.geschwister", Trauer.Geschwister), ("haus.streit", Haus.Streit), ("haus.jahrImMinus", Haus.JahrImMinus),
                     ("haus.bargeldNegativ", Haus.BargeldNegativ), ("gericht.angeklagt", Gericht.Angeklagt), ("gericht.verurteilt", Gericht.Verurteilt),
                     ("gericht.kerker", Gericht.Kerker), ("krankheit.erkrankt", Krankheit.Erkrankt), ("krankheit.pest", Krankheit.Pest) })
            if (wert < 0 || wert > Hoechstwert) f.Add($"{q}.{name} liegt zwischen 0 und dem Höchstwert.");
        if (Zusammenbruch.ZugChance is < 0 or > 1) f.Add($"{q}.zusammenbruch.zugChance liegt zwischen 0 und 1.");
        foreach (var k in Zusammenbruch.Krankheiten.Where(k => krankheiten is not null && !krankheiten.Contains(k)))
            f.Add($"{q}.zusammenbruch.krankheiten: Die Krankheit „{k}“ gibt es nicht.");
        if (KiVentilAb < 0) f.Add($"{q}.kiVentilAb darf nicht negativ sein.");
    }
}
