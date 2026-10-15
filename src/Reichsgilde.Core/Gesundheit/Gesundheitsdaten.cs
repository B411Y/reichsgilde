using Reichsgilde.Core.Dynastie;

namespace Reichsgilde.Core.Gesundheit;

/// <summary>Eine gewöhnliche Krankheit (Abschnitt „gesundheit.krankheiten").</summary>
public sealed class KrankheitDaten
{
    /// <summary>Chance je Person und Jahr, daran zu erkranken.</summary>
    public double Chance { get; init; }

    /// <summary>So viele Jahreswechsel dauert sie, wenn niemand sie behandelt (mindestens 1).</summary>
    public int Dauer { get; init; } = 1;

    /// <summary>Abzug an Sternen je Talent, solange sie dauert.</summary>
    public Dictionary<Talent, double> Talente { get; init; } = new();

    /// <summary>Abzug am Ansehen des Hauses, wenn das Oberhaupt sie hat (Haarausfall, Lachanfälle).</summary>
    public double Ansehen { get; init; }

    /// <summary>Chance, am Jahresende daran zu sterben (unbehandelt).</summary>
    public double Sterben { get; init; }

    /// <summary>Heilt Kräuterschnaps sie? (Sonst hilft nur der Medicus.)</summary>
    public bool Schnaps { get; init; }

    /// <summary>Ab diesem Alter dreimal so häufig (0 = immer gleich) – z. B. Vergesslichkeit.</summary>
    public int AlterAb { get; init; }

    /// <summary>
    /// Eine Vergiftung (Giftdolch des Parfumeurs): Weder Medicus noch Kräuterschnaps helfen – nur Gegengift (ab Datenversion 21).
    /// Gestorben wird nur am Ende des letzten Jahres (<see cref="Dauer"/>) – bis dahin bleibt Zeit für das Gegengift.
    /// </summary>
    public bool Gegengift { get; init; }
}

/// <summary>Die Pest: eine Seuche, die alle paar Jahrzehnte die ganze Stadt trifft.</summary>
public sealed class PestDaten
{
    /// <summary>Zwischen zwei Pestjahren liegen so viele Jahre (gewürfelt, gleichverteilt).</summary>
    public int AbstandMin { get; init; } = 25;
    public int AbstandMax { get; init; } = 40;

    /// <summary>Chance jeder Person, im Pestjahr zu erkranken (mal Anfälligkeit des Volkes).</summary>
    public double Ansteckung { get; init; } = 0.35;

    /// <summary>Chance eines Erkrankten, am Jahresende zu sterben (unbehandelt).</summary>
    public double Sterben { get; init; } = 0.4;

    /// <summary>Faktor auf das Sterberisiko, wenn ein Medicus kam bzw. Kräuterschnaps gegeben wurde (beides zusammen: Produkt).</summary>
    public double MedicusFaktor { get; init; } = 0.5;
    public double SchnapsFaktor { get; init; } = 0.8;

    /// <summary>Im Pestjahr fehlt dieser Anteil der Arbeitskräfte – alle Betriebe arbeiten so viel langsamer.</summary>
    public double Krankenstand { get; init; } = 0.3;

    /// <summary>Talentabzug für Pestkranke (auf alle Talente).</summary>
    public double TalentAbzug { get; init; } = 2;

    /// <summary>Anfälligkeit je Volk (Faktor auf die Ansteckung, Standard 1).</summary>
    public Dictionary<Volk, double> Voelker { get; init; } = new();

    public double Anfaelligkeit(Volk volk) => Voelker.GetValueOrDefault(volk, 1);
}

/// <summary>Abschnitt „gesundheit" in wirtschaft.json (ab Datenversion 11). Fehlt er, wird niemand krank.</summary>
public sealed class GesundheitDaten
{
    public Dictionary<string, KrankheitDaten> Krankheiten { get; init; } = new();
    public PestDaten? Pest { get; init; }

    /// <summary>Ein Medicus heilt jede gewöhnliche Krankheit sofort; bei der Pest senkt er das Sterberisiko.</summary>
    public double MedicusKosten { get; init; } = 120;
    public double MedicusKostenPest { get; init; } = 300;

    /// <summary>Name der Ware auf dem Markt, die als Heilmittel taugt.</summary>
    public string Schnaps { get; init; } = "Kräuterschnaps";

    /// <summary>Ware, die eine Vergiftung heilt (aus dem eigenen Lager oder vom Markt; leer = keine).</summary>
    public string Gegengift { get; init; } = "";

    /// <summary>Krötenschleim im Hinterzimmer: Diese Krankheiten kann er auslösen (leer = alle gewöhnlichen).</summary>
    public List<string> Kroetenschleim { get; init; } = new();

    public void Pruefen(List<string> f, IReadOnlyDictionary<string, Wirtschaft.WareDaten> waren)
    {
        if (Krankheiten.ContainsKey("Pest")) f.Add("gesundheit.krankheiten: „Pest“ ist reserviert – sie steht unter „pest“.");
        foreach (var (name, k) in Krankheiten)
        {
            if (k.Chance is < 0 or > 1) f.Add($"gesundheit.krankheiten.{name}.chance muss zwischen 0 und 1 liegen.");
            if (k.Sterben is < 0 or > 1) f.Add($"gesundheit.krankheiten.{name}.sterben muss zwischen 0 und 1 liegen.");
            if (k.Dauer < 1) f.Add($"gesundheit.krankheiten.{name}.dauer muss mindestens 1 sein.");
        }
        if (Krankheiten.Values.Sum(k => k.Chance) > 0.9) f.Add("gesundheit.krankheiten: Die Chancen ergeben zusammen mehr als 90 %.");
        if (Pest is { } p)
        {
            if (p.AbstandMin < 1 || p.AbstandMax < p.AbstandMin) f.Add("gesundheit.pest.abstandMin/abstandMax: 1 ≤ min ≤ max.");
            foreach (var (name, wert) in new[] { ("ansteckung", p.Ansteckung), ("sterben", p.Sterben), ("medicusFaktor", p.MedicusFaktor),
                                                  ("schnapsFaktor", p.SchnapsFaktor), ("krankenstand", p.Krankenstand) })
                if (wert is < 0 or > 1) f.Add($"gesundheit.pest.{name} muss zwischen 0 und 1 liegen.");
        }
        if (MedicusKosten < 0 || MedicusKostenPest < 0) f.Add("gesundheit.medicusKosten dürfen nicht negativ sein.");
        if (!waren.ContainsKey(Schnaps)) f.Add($"gesundheit.schnaps: Die Ware „{Schnaps}“ fehlt unter „waren“.");
        if (Gegengift != "" && !waren.ContainsKey(Gegengift)) f.Add($"gesundheit.gegengift: Die Ware „{Gegengift}“ fehlt unter „waren“.");
        if (Gegengift == "" && Krankheiten.Values.Any(k => k.Gegengift)) f.Add("gesundheit: Eine Vergiftung braucht „gegengift“ – sonst heilt sie nichts.");
        foreach (var name in Kroetenschleim.Where(n => !Krankheiten.ContainsKey(n)))
            f.Add($"gesundheit.kroetenschleim: Die Krankheit „{name}“ gibt es nicht.");
    }
}
