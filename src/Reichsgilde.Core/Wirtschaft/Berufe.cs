namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Was eine Betriebsart ist und kann (Abschnitt „betriebsarten" in wirtschaft.json, ab Datenversion 12).
///
/// Werkstätten haben <see cref="Produkte"/> (Waren mit Zutaten), Umlandbetriebe fördern <see cref="Rohstoffe"/>
/// (eine oder zwei Waren ohne Zutaten; bei zwei teilt man die Arbeitszeit auf). Ein neuer Beruf ist damit
/// vor allem ein Eintrag in den Daten – nur Aussehen und Platz in der Stadt kommen aus der Darstellung.
/// </summary>
public sealed class BetriebsartDaten
{
    /// <summary>Anzeigename der Art („Gießerei", „Grube", „Tischlerei", „Holzschlag").</summary>
    public string Name { get; init; } = "";

    /// <summary>Zu welchem Beruf sie gehört (Name aus „berufe").</summary>
    public string Beruf { get; init; } = "";

    public Arbeitsort Ort { get; init; } = Arbeitsort.Werkstatt;
    public int Plaetze { get; init; } = 2;

    /// <summary>Unterhalt (Werkstatt) bzw. Pacht (Umland) im Jahr.</summary>
    public double Fixkosten { get; init; }

    /// <summary>Werkstatt: was sie herstellen kann (Waren mit Zutaten).</summary>
    public List<string> Produkte { get; init; } = new();

    /// <summary>Werkstatt: womit ein neuer Betrieb anfängt.</summary>
    public string? Standardprodukt { get; init; }

    /// <summary>Umland: was gefördert wird (eine oder zwei Waren ohne Zutaten).</summary>
    public List<string> Rohstoffe { get; init; } = new();

    /// <summary>Karren, mit dem ein neuer Betrieb anfängt.</summary>
    public string Startkarren { get; init; } = "Handkarren";

    public bool IstWerkstatt => Produkte.Count > 0;

    /// <summary>Weder Produkte noch Rohstoffe (Dieb, Gardist): Die Leute verdienen anders – siehe Diebeswesen und Wachwesen.</summary>
    public bool OhneProduktion => Produkte.Count == 0 && Rohstoffe.Count == 0;

    /// <summary>Ein Gebäude in der Stadt (mit Gebäudestufen) – Werkstatt oder Betrieb ohne Ware.</summary>
    public bool InDerStadt => Ort == Arbeitsort.Werkstatt;

    /// <summary>Gebäudestufe 1–3 (Gießerei → Schmiede → Goldschmiede). Umlandbetriebe bleiben auf Stufe 1.</summary>
    public int Stufe { get; init; } = 1;

    /// <summary>Zu welcher Art die Werkstatt am Platz ausgebaut werden kann (null = höchste Stufe).</summary>
    public Betriebsart? Ausbau { get; init; }

    /// <summary>Gulden für den Umbau zur nächsten Stufe.</summary>
    public double AusbauKosten { get; init; }

    /// <summary>
    /// Werkstatt der Stufe 1: was sie kostet – beim Neubau und als Grundlage ihres Werts beim Verkauf (ab Datenversion 40;
    /// vorher galt der Kaufpreis einer freien Werkstatt dieser Art unter „liegenschaften“). 0 = nicht angegeben.
    /// </summary>
    public double Kaufpreis { get; init; }

    /// <summary>
    /// Arbeitsminuten je Spieljahr in dieser Betriebsart, ab 08:00 gezählt (ab Datenversion 43; null = <c>allgemein.arbeitsminuten</c>).
    /// Im Wirtshaus 840 = bis 22 Uhr, so lange wie die Gäste bleiben. Der Lohn gilt je Stunde: wer länger arbeitet, kostet mehr.
    /// </summary>
    public int? Arbeitsminuten { get; init; }

    /// <summary>
    /// Faktor auf die Erfahrung, die die Leute dieses Betriebs dem Haus bringen (Vorgabe 1; ab Datenversion 48). Diebe und Wache haben
    /// doppelt so viele Plätze wie eine Werkstatt derselben Stufe (4/6/8 statt 2/3/4) und deshalb 0,5 – sonst erreichte ihr Haus die
    /// Meistergrade doppelt so schnell (PR #177).
    /// </summary>
    public double ErfahrungFaktor { get; init; } = 1;
}

/// <summary>
/// Meistergrade 1–6 je Beruf und Haus (Abschnitt „meistergrade", ab Datenversion 13).
///
/// Erfahrung sammelt ein Haus durch Arbeit: Jedes hergestellte oder geförderte Stück bringt so viele
/// Erfahrungsstunden, wie ein Geselle dafür braucht. Ist genug beisammen, legt das Oberhaupt die
/// Meisterprüfung ab (Gebühr und Aktionspunkte). Der Grad gehört dem Haus und geht beim Erbfall nicht verloren.
/// </summary>
public sealed class MeistergradDaten
{
    /// <summary>Die Prüfungen für Grad 2, 3, … (aufsteigend). Grad 1 hat, wer den Beruf beherrscht.</summary>
    public List<Meisterpruefung> Pruefungen { get; init; } = new();

    /// <summary>Ab welchem Grad die Gebäudestufen 1, 2, 3 erlaubt sind (Standard 1, 3, 5).</summary>
    public List<int> StufeAbGrad { get; init; } = new() { 1, 3, 5 };

    /// <summary>Ab diesem Grad (in irgendeinem Beruf) bekommt das Haus <see cref="ApBonus"/> AP mehr je Runde.</summary>
    public int ApBonusAbGrad { get; init; } = 6;
    public int ApBonus { get; init; } = 1;

    /// <summary>So viele Arbeitsminuten ruht die Werkstatt während des Umbaus (360 = ein halber Arbeitstag).</summary>
    public int UmbauMinuten { get; init; } = 360;

    public int HoechsterGrad => Pruefungen.Count + 1;

    /// <summary>Die Prüfung, mit der man Grad <paramref name="grad"/> erreicht (null für Grad 1 oder über dem Höchsten).</summary>
    public Meisterpruefung? PruefungFuer(int grad) => grad >= 2 && grad - 2 < Pruefungen.Count ? Pruefungen[grad - 2] : null;

    /// <summary>Welcher Grad für diese Gebäudestufe nötig ist.</summary>
    public int GradFuerStufe(int stufe) => stufe >= 1 && stufe <= StufeAbGrad.Count ? StufeAbGrad[stufe - 1] : int.MaxValue;

    public void Pruefen(List<string> f, int hoechstensAp)
    {
        if (Pruefungen.Count == 0) f.Add("meistergrade.pruefungen ist leer.");
        for (var i = 0; i < Pruefungen.Count; i++)
        {
            var p = Pruefungen[i];
            if (p.Erfahrung < 0 || p.Gebuehr < 0) f.Add($"meistergrade.pruefungen[{i}]: erfahrung und gebuehr dürfen nicht negativ sein.");
            if (p.Ap < 0 || p.Ap > hoechstensAp) f.Add($"meistergrade.pruefungen[{i}]: {p.Ap} AP liegt nicht zwischen 0 und dem höchsten Vorrat ({hoechstensAp}).");
            if (i > 0 && p.Erfahrung <= Pruefungen[i - 1].Erfahrung) f.Add("meistergrade.pruefungen: Die nötige Erfahrung muss von Grad zu Grad steigen.");
        }
        if (StufeAbGrad.Count == 0 || StufeAbGrad[0] != 1) f.Add("meistergrade.stufeAbGrad: Stufe 1 muss ab Grad 1 erlaubt sein.");
        for (var i = 1; i < StufeAbGrad.Count; i++)
            if (StufeAbGrad[i] <= StufeAbGrad[i - 1] || StufeAbGrad[i] > HoechsterGrad)
                f.Add("meistergrade.stufeAbGrad: Die Grade müssen aufsteigen und dürfen den höchsten Grad nicht überschreiten.");
        if (ApBonus < 0) f.Add("meistergrade.apBonus darf nicht negativ sein.");
        if (UmbauMinuten < 0) f.Add("meistergrade.umbauMinuten darf nicht negativ sein.");
    }
}

/// <summary>Eine Meisterprüfung: so viel Erfahrung (Arbeitsstunden im Beruf) muss da sein, Gebühr und AP werden fällig.</summary>
public sealed record Meisterpruefung
{
    public double Erfahrung { get; init; }
    public double Gebuehr { get; init; }
    public int Ap { get; init; }
}

/// <summary>Ein Beruf (Abschnitt „berufe"): welche Betriebe dazugehören und was es kostet, ihn zu erlernen.</summary>
public sealed class BerufDaten
{
    /// <summary>Die Werkstatt des Berufs (Stufe 1) – mit ihr beginnt, wer den Beruf als Startberuf wählt.</summary>
    public Betriebsart Werkstatt { get; init; }

    /// <summary>Der Umlandbetrieb, der die Rohstoffe liefert (null: der Beruf hat keinen, z. B. der Dieb).</summary>
    public Betriebsart? Umland { get; init; }

    /// <summary>Gulden für Lehrgeld und Werkzeug, wenn man den Beruf später erlernt (die AP stehen bei „aktionspunkte").</summary>
    public double Lehrgeld { get; init; } = 500;

    public string Beschreibung { get; init; } = "";

    /// <summary>
    /// Völkerboni (ab Datenversion 23): So viel mehr (oder weniger) leisten die Betriebe dieses Berufs, wenn das
    /// Oberhaupt des Hauses diesem Volk angehört (0,05 = +5 %). Fehlt ein Volk, gilt 0.
    /// </summary>
    public Dictionary<Volk, double> Voelker { get; init; } = new();
}
