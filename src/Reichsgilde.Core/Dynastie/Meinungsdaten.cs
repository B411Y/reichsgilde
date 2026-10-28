using Reichsgilde.Core.Justiz;

namespace Reichsgilde.Core.Dynastie;

/// <summary>Verwandtschaft als fester Grund der Meinung (Abschnitt "meinungen.verwandtschaft").</summary>
public sealed class VerwandtschaftMeinungDaten
{
    /// <summary>Eheleute übereinander.</summary>
    public double Ehe { get; init; } = 15;

    /// <summary>Eltern über ihre Kinder und Kinder über ihre Eltern.</summary>
    public double ElternKind { get; init; } = 15;

    /// <summary>Geschwister (ein gemeinsamer Elternteil genügt).</summary>
    public double Geschwister { get; init; } = 5;
}

/// <summary>Der Stand als fester Grund (Abschnitt "meinungen.stand"): Wer höher steht, wird geachtet, wer tiefer steht, belächelt.</summary>
public sealed class StandMeinungDaten
{
    /// <summary>So viel je Stufe, die der andere höher (+) oder tiefer (−) steht.</summary>
    public double JeStufe { get; init; } = 3;

    /// <summary>Höchstens so viel in jede Richtung.</summary>
    public double Hoechstens { get; init; } = 9;
}

/// <summary>Ein Erbe, der zu lange warten muss, wird ungeduldig (Abschnitt "meinungen.erbe").</summary>
public sealed class ErbeMeinungDaten
{
    /// <summary>Ab diesem Alter (in Menschenjahren) wartet der Erbe ungeduldig.</summary>
    public double UngeduldAb { get; init; } = 30;

    /// <summary>Meinung über das Oberhaupt je Jahr darüber (negativ).</summary>
    public double JeJahr { get; init; } = -3;

    /// <summary>Höchstens so viel (negativ).</summary>
    public double Hoechstens { get; init; } = -30;
}

/// <summary>Was Taten an Meinung bringen (Abschnitt "meinungen.taten"). Sie verblassen jedes Jahr.</summary>
public sealed class TatenMeinungDaten
{
    /// <summary>Alle wichtigen Personen über das Oberhaupt, dessen Fest der Rat beschlossen hat.</summary>
    public double Fest { get; init; } = 5;

    /// <summary>Ein Bewerber oder Antragsteller über jeden, der im Rat für ihn stimmte.</summary>
    public double StimmeFuer { get; init; } = 5;

    /// <summary>… und über jeden, der gegen ihn stimmte (bei Wahlen: für einen anderen).</summary>
    public double StimmeGegen { get; init; } = -5;

    /// <summary>Der Beklagte über den Kläger.</summary>
    public double Klage { get; init; } = -15;

    /// <summary>Das Opfer über den Täter, wenn ein Zeuge ihn erkannt hat – je Vergehen.</summary>
    public Dictionary<Vergehen, double> Ertappt { get; init; } = new();
}

/// <summary>Streit im Haus (Abschnitt "meinungen.wirkung.streit").</summary>
public sealed class StreitDaten
{
    /// <summary>Ab dieser Meinung des Erben über das Oberhaupt (und darunter) kann es Streit geben.</summary>
    public double Ab { get; init; } = -50;

    /// <summary>Chance im Jahr.</summary>
    public double Chance { get; init; } = 0.25;

    /// <summary>So viel Ansehen kostet der Streit bis zum Ende des nächsten Jahres.</summary>
    public double Ansehen { get; init; } = 5;
}

/// <summary>Was die Meinung bewirkt (Abschnitt "meinungen.wirkung").</summary>
public sealed class MeinungWirkungDaten
{
    /// <summary>Haushaltskosten der Familie: −Meinung des Ehepartners über das Oberhaupt × dieser Wert (0,002 → +25 spart 5 %).</summary>
    public double HaushaltJePunkt { get; init; } = 0.002;

    /// <summary>Höchstens so viel Rabatt oder Aufschlag auf den Haushalt.</summary>
    public double HaushaltHoechstens { get; init; } = 0.1;

    public StreitDaten Streit { get; init; } = new();

    /// <summary>Brautschau: Meinung des Kandidaten über das Oberhaupt × diesem Wert zählt zur Bereitschaft.</summary>
    public double HeiratBereitschaftJePunkt { get; init; } = 0.5;

    /// <summary>Brautschau: Gulden je Zuneigung × (1 − Meinung × diesem Wert).</summary>
    public double HeiratKostenJePunkt { get; init; } = 0.005;

    /// <summary>KI-Häuser verschonen Häuser, über deren Oberhaupt ihr Oberhaupt mindestens so gut denkt (kein Diebstahl, keine Klage auf Verdacht).</summary>
    public double KiVerschontAb { get; init; } = 25;

    /// <summary>… und nehmen sich Häuser vor, über die es höchstens so denkt (auch wenn sie hinter ihnen liegen).</summary>
    public double KiVerhasstAb { get; init; } = -25;

    /// <summary>Bei Wahlen: Gefahr eines Hauses × (1 − Meinung × diesem Wert) – ein geliebtes Haus scheint weniger gefährlich.</summary>
    public double KiGefahrJePunkt { get; init; } = 0.01;
}

/// <summary>Wie Züge der Person eine Aktivität färben (Abschnitt "meinungen.zugwirkung").</summary>
public sealed class ZugwirkungDaten
{
    /// <summary>Faktor auf die Wirkung je Zug, der die Aktivität mag.</summary>
    public double Mag { get; init; } = 1.5;

    /// <summary>Faktor auf die Wirkung je Zug, der sie nicht mag …</summary>
    public double MagNicht { get; init; } = 0.5;

    /// <summary>… und Faktor auf die Gefahr.</summary>
    public double Gefahr { get; init; } = 2;
}

/// <summary>
/// Eine Aktivität mit einer wichtigen Person (Abschnitt "meinungen.aktivitaeten", Schlüssel = Kennung, z. B. "Gastmahl").
/// Sie kostet Aktionspunkte und Gulden und hebt die Meinung der Person über das Oberhaupt – oder senkt sie, wenn etwas schiefgeht.
/// </summary>
public sealed class AktivitaetDaten
{
    public string Name { get; init; } = "";
    public string Beschreibung { get; init; } = "";
    public int Ap { get; init; } = 1;

    /// <summary>Was die Aktivität kostet (beim Geschenk: der Betrag nach Wahl).</summary>
    public double Gulden { get; init; }

    /// <summary>Meinung bei Erfolg (vor den Zügen).</summary>
    public double Wirkung { get; init; }

    /// <summary>Züge der Person, die die Aktivität mögen bzw. nicht mögen (Kennungen).</summary>
    public List<string> Mag { get; init; } = new();
    public List<string> MagNicht { get; init; } = new();

    /// <summary>Chance, dass es schiefgeht (vor Zügen und Talent).</summary>
    public double Gefahr { get; init; }

    /// <summary>Was dann geschieht („Streit bei Tisch“) und was es an Meinung kostet (negativ).</summary>
    public string Unglueck { get; init; } = "";
    public double UnglueckWirkung { get; init; }

    /// <summary>Ein Talent des eigenen Oberhaupts senkt die Gefahr je Stern um <see cref="GefahrJeStern"/>.</summary>
    public Talent? Talent { get; init; }
    public double GefahrJeStern { get; init; } = 0.02;

    /// <summary>Nur mit jemandem desselben Glaubens (Messe).</summary>
    public bool GleicherGlaube { get; init; }

    /// <summary>Erst ab diesem Stand des eigenen Hauses (Jagd ab Patrizier).</summary>
    public string? MindestStand { get; init; }

    /// <summary>Beim Unglück: Mit dieser Chance holt sich das eigene Oberhaupt diese Krankheit (Jagdunfall → Knochenbruch).</summary>
    public string? Verletzung { get; init; }
    public double VerletzungChance { get; init; }

    /// <summary>
    /// Das Geschenk: Betrag nach Wahl, immer ein Erfolg, beliebig oft. Wirkung = Betrag × <c>politik.gunst.jeGulden</c> × Bestechlichkeit
    /// der Person (Zug-Wert <c>bestechlich</c>: Gierig mehr, Ehrlich und Bescheiden weniger) × <see cref="Abnahme"/> je Geschenk, das sie in
    /// diesem Jahr schon bekam. An einen Ratsherrn ist es Bestechung (Spur fürs Gericht).
    /// </summary>
    public bool Geschenk { get; init; }
    public double Abnahme { get; init; } = 0.7;
}

/// <summary>
/// Meinungen und Aktivitäten (Abschnitt "meinungen" in wirtschaft.json, ab Datenversion 61, Charaktersystem Teil 2, PR #231).
/// Fehlt der Abschnitt (alte Spielstände bringen ihre Daten mit), kennt die Stadt nur die Gründe, die es vorher schon gab:
/// Gunst aus Geschenken und Taten, Volk und Glaube – genau wie die frühere Gunst der Ratsherren.
/// </summary>
public sealed class MeinungDaten
{
    /// <summary>Die Meinung liegt zwischen −Höchstwert und +Höchstwert.</summary>
    public double Hoechstwert { get; init; } = 100;

    /// <summary>So viel verblassen die Gründe aus Taten jedes Jahr (wie früher die Gunst).</summary>
    public double VerfallJeJahr { get; init; } = 0.3;

    public VerwandtschaftMeinungDaten Verwandtschaft { get; init; } = new();
    public StandMeinungDaten Stand { get; init; } = new();
    public ErbeMeinungDaten Erbe { get; init; } = new();
    public TatenMeinungDaten Taten { get; init; } = new();
    public MeinungWirkungDaten Wirkung { get; init; } = new();
    public ZugwirkungDaten Zugwirkung { get; init; } = new();
    public Dictionary<string, AktivitaetDaten> Aktivitaeten { get; init; } = new();

    internal void Pruefen(List<string> f, Func<string, int> standStufe, ICollection<string> zuege, ICollection<string> krankheiten)
    {
        const string p = "meinungen";
        if (Hoechstwert <= 0) f.Add($"{p}.hoechstwert muss größer als 0 sein.");
        if (VerfallJeJahr is < 0 or > 1) f.Add($"{p}.verfallJeJahr muss zwischen 0 und 1 liegen.");
        if (Stand.Hoechstens < 0) f.Add($"{p}.stand.hoechstens darf nicht negativ sein.");
        if (Erbe.JeJahr > 0 || Erbe.Hoechstens > 0) f.Add($"{p}.erbe: jeJahr und hoechstens sind negativ (Ungeduld).");
        if (Wirkung.Streit.Chance is < 0 or > 1) f.Add($"{p}.wirkung.streit.chance muss zwischen 0 und 1 liegen.");
        if (Wirkung.HaushaltHoechstens is < 0 or >= 1) f.Add($"{p}.wirkung.haushaltHoechstens muss zwischen 0 und 1 liegen.");
        if (Zugwirkung.Mag < 0 || Zugwirkung.MagNicht < 0 || Zugwirkung.Gefahr < 0) f.Add($"{p}.zugwirkung: Faktoren dürfen nicht negativ sein.");
        foreach (var (kennung, a) in Aktivitaeten)
        {
            var q = $"{p}.aktivitaeten.{kennung}";
            if (string.IsNullOrWhiteSpace(a.Name)) f.Add($"{q}: name fehlt.");
            if (a.Ap < 0 || a.Gulden < 0) f.Add($"{q}: ap und gulden dürfen nicht negativ sein.");
            if (a.Gefahr is < 0 or > 1 || a.VerletzungChance is < 0 or > 1) f.Add($"{q}: gefahr und verletzungChance müssen zwischen 0 und 1 liegen.");
            if (a.UnglueckWirkung > 0) f.Add($"{q}.unglueckWirkung ist negativ (oder 0).");
            if (a.Gefahr > 0 && string.IsNullOrWhiteSpace(a.Unglueck)) f.Add($"{q}: Mit Gefahr braucht es den Text „unglueck“.");
            if (a.Geschenk && (a.Abnahme is <= 0 or > 1)) f.Add($"{q}.abnahme muss größer als 0 und höchstens 1 sein.");
            if (a.MindestStand is { } st && standStufe(st) < 0) f.Add($"{q}.mindestStand „{st}“ gibt es nicht.");
            if (a.Verletzung is { } v && !krankheiten.Contains(v)) f.Add($"{q}.verletzung: Krankheit „{v}“ gibt es nicht.");
            foreach (var z in a.Mag.Concat(a.MagNicht).Where(z => zuege.Count > 0 && !zuege.Contains(z))) // ohne Abschnitt "charakterzuege" wirken sie nur nicht
                f.Add($"{q}: Charakterzug „{z}“ gibt es nicht.");
        }
        if (Aktivitaeten.Count(a => a.Value.Geschenk) > 1) f.Add($"{p}.aktivitaeten: Nur eine Aktivität kann das Geschenk sein.");
    }
}
