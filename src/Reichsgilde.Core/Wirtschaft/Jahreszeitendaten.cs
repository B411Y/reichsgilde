using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Folgen der Jahreszeiten für die Wirtschaft (Abschnitt „jahreszeiten“ in wirtschaft.json, ab Datenversion 67; Entscheidung des Nutzers vom
/// 31.10.2026, Weg B: ein Spieltag bleibt ein Jahr, die Jahreszeit des Jahres wird spürbar). Fehlt der Abschnitt, hat die Jahreszeit keine
/// Wirkung auf die Wirtschaft – nur Licht, Wetter und Ton richten sich nach ihr, wie bis Datenversion 66.
/// </summary>
public sealed class JahreszeitenDaten
{
    public JahreszeitDaten Fruehling { get; init; } = new();
    public JahreszeitDaten Sommer { get; init; } = new();
    public JahreszeitDaten Herbst { get; init; } = new();
    public JahreszeitDaten Winter { get; init; } = new();

    /// <summary>Der strenge Winter: Kälte, Eis auf dem Fluss, Hafen und Fähre zu. Null: Der Fluss friert nie zu.</summary>
    public StrengerWinterDaten? StrengerWinter { get; init; }

    /// <summary>Festmärkte (Messen) an bestimmten Stunden bestimmter Jahreszeiten.</summary>
    public List<FestmarktDaten> Festmaerkte { get; init; } = new();

    public JahreszeitDaten Von(Jahreszeit j) => j switch
    {
        Jahreszeit.Fruehling => Fruehling,
        Jahreszeit.Sommer => Sommer,
        Jahreszeit.Herbst => Herbst,
        _ => Winter,
    };

    public void Pruefen(List<string> f, IReadOnlyDictionary<string, WareDaten> waren, IReadOnlyDictionary<Betriebsart, BetriebsartDaten> arten)
    {
        foreach (var j in Enum.GetValues<Jahreszeit>())
            Von(j).Pruefen(f, $"jahreszeiten.{KleinerName(j)}", waren, arten);
        StrengerWinter?.Pruefen(f);
        var namen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in Festmaerkte)
        {
            m.Pruefen(f);
            if (!namen.Add(m.Name)) f.Add($"jahreszeiten.festmaerkte: Den Festmarkt „{m.Name}“ gibt es zweimal.");
        }
        foreach (var g in Festmaerkte.GroupBy(m => m.Jahreszeit).Where(g => g.Count() > 1))
        {
            var liste = g.OrderBy(m => m.Von).ToList();
            for (var i = 1; i < liste.Count; i++)
                if (liste[i].Von < liste[i - 1].Bis)
                    f.Add($"jahreszeiten.festmaerkte: „{liste[i - 1].Name}“ und „{liste[i].Name}“ überschneiden sich.");
        }
    }

    internal static string KleinerName(Jahreszeit j) => j.ToString().ToLowerInvariant();
}

/// <summary>Was in einer Jahreszeit anders ist als im Sommer (alle Faktoren 1 = wie in den Daten).</summary>
public sealed class JahreszeitDaten
{
    /// <summary>Faktor auf die Nachfrage der Stadt je Ware (Winter: Brennholz, Kerzen, Schnaps).</summary>
    public Dictionary<string, double> Nachfrage { get; init; } = new();

    /// <summary>Faktor auf die Grundversorgung der Händler und Bauern des Umlands je Ware (Herbst: die Ernte kommt auf den Markt).</summary>
    public Dictionary<string, double> Angebot { get; init; } = new();

    /// <summary>Faktor auf die Leistung je Betriebsart (Schlüssel wie in „betriebsarten“, z. B. „Holzfaellerei“): Arbeit im Freien.</summary>
    public Dictionary<string, double> Arbeit { get; init; } = new();

    /// <summary>
    /// Schlamm: so viel mal so lang dauert ein Stück eines Weges mit diesem Belag (Schlüssel „Trampelpfad“, „Fahrweg“; Kies und Pflaster
    /// sind befestigt und bleiben gleich). Wirkt im Zeitfaktor des Kerns – das Bild fährt den Karren mit demselben Faktor.
    /// </summary>
    public Dictionary<string, double> Schlamm { get; init; } = new();

    /// <summary>Faktor auf die feste Fahrzeit aus dem Umland (Landwege ohne Strecke auf der Karte, <c>logistik.fahrzeiten.Umland</c>).</summary>
    public double SchlammUmland { get; init; } = 1;

    public double NachfrageVon(string ware) => Nachfrage.GetValueOrDefault(ware, 1);
    public double AngebotVon(string ware) => Angebot.GetValueOrDefault(ware, 1);
    public double ArbeitVon(Betriebsart art) => Arbeit.GetValueOrDefault(art.ToString(), 1);
    public double SchlammVon(Belagstufe s) => Wegbelag.IstBefestigt(s) ? 1 : Schlamm.GetValueOrDefault(s.ToString(), 1);

    internal void Pruefen(List<string> f, string p, IReadOnlyDictionary<string, WareDaten> waren, IReadOnlyDictionary<Betriebsart, BetriebsartDaten> arten)
    {
        foreach (var (ware, x) in Nachfrage.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!waren.ContainsKey(ware)) f.Add($"{p}.nachfrage: Eine Ware „{ware}“ gibt es nicht.");
            if (x is <= 0 or > 3) f.Add($"{p}.nachfrage.{ware} muss über 0 und höchstens 3 sein.");
        }
        foreach (var (ware, x) in Angebot.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!waren.ContainsKey(ware)) f.Add($"{p}.angebot: Eine Ware „{ware}“ gibt es nicht.");
            if (x is <= 0 or > 3) f.Add($"{p}.angebot.{ware} muss über 0 und höchstens 3 sein.");
        }
        foreach (var (art, x) in Arbeit.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!Enum.TryParse<Betriebsart>(art, out var a) || !arten.ContainsKey(a)) f.Add($"{p}.arbeit: Eine Betriebsart „{art}“ gibt es nicht.");
            if (x is <= 0 or > 2) f.Add($"{p}.arbeit.{art} muss über 0 und höchstens 2 sein.");
        }
        foreach (var (stufe, x) in Schlamm.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!Enum.TryParse<Belagstufe>(stufe, out var s)) f.Add($"{p}.schlamm: Einen Belag „{stufe}“ gibt es nicht.");
            else if (Wegbelag.IstBefestigt(s)) f.Add($"{p}.schlamm.{stufe}: {stufe} ist befestigt – Schlamm gibt es nur auf Trampelpfad und Fahrweg.");
            if (x is < 1 or > 3) f.Add($"{p}.schlamm.{stufe} muss zwischen 1 und 3 liegen.");
        }
        if (SchlammUmland is < 1 or > 3) f.Add($"{p}.schlammUmland muss zwischen 1 und 3 liegen.");
    }
}

/// <summary>
/// Der strenge Winter (nur in einem Winterjahr): Zu Jahresbeginn wird die Kälte des Winters gewürfelt (0–1, aus einer <see cref="Zufallsquelle"/>
/// mit Startwert der Partie und Jahr). Ab 1 − <see cref="Chance"/> ist er streng: Die Nachfrage des Winters wächst noch einmal, und der Fluss friert
/// zu – um eine gewürfelte Stunde zwischen <see cref="ZufrierenVon"/> und <see cref="ZufrierenBis"/> Uhr, für eine Dauer, die mit der Kälte von
/// <see cref="DauerMin"/> bis <see cref="DauerMax"/> Stunden wächst (bis höchstens zum Jahresende um 23 Uhr).
/// </summary>
public sealed class StrengerWinterDaten
{
    /// <summary>So oft ist ein Winter streng (0,3 = in drei von zehn Wintern).</summary>
    public double Chance { get; init; } = 0.3;

    /// <summary>Im strengen Winter wächst jeder Nachfragefaktor des Winters um diesen Anteil seines Zuschlags (0,5: 1,4 → 1,6).</summary>
    public double NachfrageZuschlag { get; init; } = 0.5;

    /// <summary>Frühste und späteste Stunde, zu der der Fluss zufriert (volle Stunde, Uhrzeit).</summary>
    public int ZufrierenVon { get; init; } = 7;
    public int ZufrierenBis { get; init; } = 10;

    /// <summary>Wie lange er zu bleibt (Stunden): im mildesten strengen Winter <see cref="DauerMin"/>, im kältesten <see cref="DauerMax"/>.</summary>
    public int DauerMin { get; init; } = 6;
    public int DauerMax { get; init; } = 14;

    /// <summary>So viele Minuten kostet eine Fähre, solange der Fluss zu ist (sie ruht; wer hinüber muss, wartet oder nimmt den Umweg).</summary>
    public int FaehreMinuten { get; init; } = 120;

    internal void Pruefen(List<string> f)
    {
        const string p = "jahreszeiten.strengerWinter";
        if (Chance is < 0 or > 1) f.Add($"{p}.chance muss zwischen 0 und 1 liegen.");
        if (NachfrageZuschlag is < 0 or > 2) f.Add($"{p}.nachfrageZuschlag muss zwischen 0 und 2 liegen.");
        var erste = Spielzeit.TagesbeginnMinute / 60;
        var letzte = Spielzeit.TagesendeMinute / 60 - 1;
        if (ZufrierenVon < erste || ZufrierenBis > letzte || ZufrierenVon > ZufrierenBis)
            f.Add($"{p}: zufrierenVon und zufrierenBis müssen zwischen {erste} und {letzte} Uhr liegen (von vor bis).");
        if (DauerMin < 1 || DauerMax < DauerMin) f.Add($"{p}: dauerMin muss mindestens 1 sein und dauerMax mindestens dauerMin.");
        if (FaehreMinuten < 0) f.Add($"{p}.faehreMinuten darf nicht negativ sein.");
    }
}

/// <summary>Ein Festmarkt (Messe): in einer Jahreszeit von <see cref="Von"/> bis <see cref="Bis"/> Uhr mehr Käufer, Gäste und Besucher.</summary>
public sealed class FestmarktDaten
{
    public string Name { get; init; } = "";

    /// <summary>In welcher Jahreszeit (Name wie im Kalender: „Fruehling“, „Sommer“, „Herbst“, „Winter“).</summary>
    public Jahreszeit Jahreszeit { get; init; }

    /// <summary>Von dieser vollen Stunde bis zu jener (Uhrzeit, ohne die Bis-Stunde).</summary>
    public int Von { get; init; } = 10;
    public int Bis { get; init; } = 16;

    /// <summary>Zuschlag auf die Nachfrage aller Waren, solange der Markt läuft (0,2 = +20 %).</summary>
    public double Nachfrage { get; init; }

    /// <summary>Zuschlag auf die Gäste der Wirtshäuser in diesen Stunden.</summary>
    public double Gaeste { get; init; }

    /// <summary>Zuschlag auf die Besucher in der Stadt (nur Darstellung: Passanten, Stände; der Kern rechnet nichts damit).</summary>
    public double Besucher { get; init; }

    internal void Pruefen(List<string> f)
    {
        var p = $"jahreszeiten.festmaerkte „{Name}“";
        if (Name == "") f.Add("jahreszeiten.festmaerkte: Jeder Festmarkt braucht einen Namen.");
        var erste = Spielzeit.TagesbeginnMinute / 60;
        var letzte = Spielzeit.TagesendeMinute / 60;
        if (Von < erste || Bis > letzte || Von >= Bis) f.Add($"{p}: von und bis müssen zwischen {erste} und {letzte} Uhr liegen, von vor bis.");
        if (Nachfrage is < 0 or > 2 || Gaeste is < 0 or > 2 || Besucher is < 0 or > 5) f.Add($"{p}: nachfrage und gaeste zwischen 0 und 2, besucher zwischen 0 und 5.");
    }
}
