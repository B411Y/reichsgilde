namespace Reichsgilde.Core.Dynastie;

/// <summary>
/// Was ein Volk für die Dynastie bedeutet (Abschnitt "dynastie.voelker" in wirtschaft.json).
///
/// Sterben: Das Risiko folgt dem Gompertz-Gesetz der Sterblichkeit (Benjamin Gompertz, 1825):
/// Es wächst mit dem Alter exponentiell und verdoppelt sich etwa alle Streuung × 0,7 Jahre.
///   Sterberisiko im Jahr = 1 − e^−(Grundrisiko + H(Alter+1) − H(Alter)),   H(x) = e^((x − Sterbealter) / Streuung)
/// „Sterbealter" ist das häufigste Sterbealter; im Schnitt sterben Erwachsene etwas früher.
/// </summary>
public sealed class VolkDaten
{
    /// <summary>Alter des Oberhaupts bei Spielbeginn.</summary>
    public int Startalter { get; init; } = 24;

    /// <summary>Häufigstes Sterbealter (Modalwert der Gompertz-Verteilung).</summary>
    public double Sterbealter { get; init; } = 64;

    /// <summary>Wie breit die Sterbealter streuen (in Jahren). Größer = gleichmäßiger über die Jahre verteilt.</summary>
    public double Streuung { get; init; } = 8;

    /// <summary>Ab diesem Alter ist man volljährig und erbberechtigt.</summary>
    public int Volljaehrig { get; init; } = 16;

    /// <summary>In diesem Alter kann eine Frau dieses Volkes Kinder bekommen.</summary>
    public int FruchtbarVon { get; init; } = 16;
    public int FruchtbarBis { get; init; } = 40;

    /// <summary>Chance auf ein Kind je Jahr für ein Ehepaar (vor Abzug für schon geborene Kinder).</summary>
    public double Geburtenrate { get; init; } = 0.3;

    /// <summary>Lebenshaltung je Jahr für Oberhaupt und Ehepartner dieses Volkes.</summary>
    public double Haushalt { get; init; } = 50;

    /// <summary>Unterhalt je Jahr für jedes weitere Familienmitglied (Kinder, Geschwister, verwitwete Eltern).</summary>
    public double Unterhalt { get; init; } = 40;

    /// <summary>Was eine Hochzeit mit jemandem dieses Volkes kostet (Fest, Brautgabe, Mitgift-Forderungen).</summary>
    public double Hochzeit { get; init; } = 300;

    /// <summary>Wie gut ein Oberhaupt dieses Volkes umwirbt: Geschenke wirken so viel stärker (1,3 = 30 %).</summary>
    public double Werbefaktor { get; init; } = 1;

    /// <summary>Wie oft dieses Volk unter den Heiratskandidaten der Stadt vorkommt (Gewicht).</summary>
    public double Kandidatenanteil { get; init; } = 1;

    public List<string> VornamenMann { get; init; } = new();
    public List<string> VornamenFrau { get; init; } = new();
    public List<string> Familiennamen { get; init; } = new();
}

/// <summary>Ehen zwischen Völkern: erlaubt, aber mit Folgen.</summary>
public sealed class MischeheDaten
{
    /// <summary>Faktor auf die Geburtenrate (die niedrigere der beiden Völker).</summary>
    public double Fruchtbarkeit { get; init; } = 0.5;

    /// <summary>Ansehen bei Ratsherren vom Volk des Oberhaupts (die Traditionellen rümpfen die Nase).</summary>
    public double AnsehenEigenesVolk { get; init; } = -5;

    /// <summary>Ansehen bei Ratsherren vom Volk des Ehepartners (angeheiratete Verwandtschaft).</summary>
    public double AnsehenPartnervolk { get; init; } = 3;
}

/// <summary>Alle Zahlen zu Leben, Heirat, Kindern und Erbe (Abschnitt "dynastie" in wirtschaft.json).</summary>
public sealed class DynastieDaten
{
    /// <summary>
    /// Witwenrecht der Zünfte: Stirbt das Oberhaupt ohne volljähriges Kind, führt der Ehepartner das Haus weiter.
    /// Ohne Witwenrecht gilt das Dossier streng: nur volljährige leibliche Kinder erben.
    /// </summary>
    public bool Witwenrecht { get; init; } = true;

    /// <summary>Sterberisiko je Jahr unabhängig vom Alter (Unfälle, Fieber). Pest und Mord kommen später dazu.</summary>
    public double Grundrisiko { get; init; } = 0.006;

    /// <summary>Sterberisiko je Jahr für Kinder bis zur Volljährigkeit.</summary>
    public double Kinderrisiko { get; init; } = 0.02;

    /// <summary>Jedes lebende Kind des Paares senkt die Geburtenrate auf diesen Anteil (0,85 = −15 %).</summary>
    public double KinderAbnahme { get; init; } = 0.85;

    /// <summary>So viele ledige Heiratskandidaten gibt es in der Stadt.</summary>
    public int Kandidaten { get; init; } = 10;

    /// <summary>Chance je Jahr, dass ein Kandidat anderweitig heiratet und die Brautschau verlässt.</summary>
    public double KandidatGehtJeJahr { get; init; } = 0.15;

    /// <summary>Aus welchem Stand die Kandidaten stammen (Gewichte). Man darf nur bis zum eigenen Stand werben.</summary>
    public Dictionary<string, double> KandidatenStaende { get; init; } = new() { ["Gemeiner"] = 1 };

    public double ZuneigungJeGulden { get; init; } = 0.05;

    /// <summary>Mehr Zuneigung lässt sich in einem Jahr nicht gewinnen – Werben braucht Zeit.</summary>
    public double ZuneigungJeJahr { get; init; } = 20;

    /// <summary>Anteil der Zuneigung, der am Jahresende verblasst.</summary>
    public double ZuneigungVerfall { get; init; } = 0.3;

    /// <summary>Zuneigung + Ansehen des Hauses, ab der ein Kandidat einwilligt (Gemeiner).</summary>
    public double Heiratsschwelle { get; init; } = 30;

    /// <summary>So viel mehr verlangt ein Kandidat je Stufe seines Standes.</summary>
    public double HeiratsschwelleJeStand { get; init; } = 10;

    /// <summary>Hochzeitskosten steigen je Stufe des Kandidaten-Standes um diesen Anteil (0,5 = +50 %).</summary>
    public double HochzeitJeStand { get; init; } = 0.5;

    public MischeheDaten Mischehe { get; init; } = new();

    public Dictionary<Volk, VolkDaten> Voelker { get; init; } = new();

    /// <summary>Ohne Völker-Daten gibt es keine Dynastien (ältere Daten, Tests).</summary>
    public bool IstAktiv => Voelker.Count > 0;

    public VolkDaten Volk(Volk volk) =>
        Voelker.TryGetValue(volk, out var v) ? v : throw new KeyNotFoundException($"Für das Volk {volk.Einzahl()} fehlen Dynastie-Daten.");

    internal void Pruefen(List<string> f, Func<string, int> standStufe)
    {
        if (!IstAktiv) return;
        if (Grundrisiko is < 0 or >= 1 || Kinderrisiko is < 0 or >= 1) f.Add("dynastie: grundrisiko und kinderrisiko müssen zwischen 0 und 1 liegen.");
        if (KinderAbnahme is <= 0 or > 1) f.Add("dynastie.kinderAbnahme muss zwischen 0 (ausschließlich) und 1 liegen.");
        if (Kandidaten < 0) f.Add("dynastie.kandidaten darf nicht negativ sein.");
        if (KandidatGehtJeJahr is < 0 or > 1) f.Add("dynastie.kandidatGehtJeJahr muss zwischen 0 und 1 liegen.");
        if (ZuneigungVerfall is < 0 or > 1) f.Add("dynastie.zuneigungVerfall muss zwischen 0 und 1 liegen.");
        if (ZuneigungJeGulden <= 0 || ZuneigungJeJahr <= 0) f.Add("dynastie: zuneigungJeGulden und zuneigungJeJahr müssen größer als 0 sein.");
        if (Mischehe.Fruchtbarkeit is < 0 or > 1) f.Add("dynastie.mischehe.fruchtbarkeit muss zwischen 0 und 1 liegen.");
        if (KandidatenStaende.Count == 0 || KandidatenStaende.Values.Sum() <= 0) f.Add("dynastie.kandidatenStaende braucht mindestens einen Stand mit Gewicht.");
        foreach (var stand in KandidatenStaende.Keys.Where(s => standStufe(s) < 0))
            f.Add($"dynastie.kandidatenStaende: Stand „{stand}“ gibt es nicht.");

        foreach (var volk in new[] { Reichsgilde.Core.Volk.Mensch, Reichsgilde.Core.Volk.Zwerg, Reichsgilde.Core.Volk.Elb, Reichsgilde.Core.Volk.Halbling })
            if (!Voelker.ContainsKey(volk)) f.Add($"dynastie.voelker: {volk} fehlt.");
        foreach (var (volk, v) in Voelker)
        {
            var p = $"dynastie.voelker.{volk}";
            if (volk is Reichsgilde.Core.Volk.Ork or Reichsgilde.Core.Volk.Oger) f.Add($"{p}: Orks und Oger sind nicht spielbar und haben kein Bürgerrecht.");
            if (v.Sterbealter <= v.Startalter) f.Add($"{p}: sterbealter muss über dem startalter liegen.");
            if (v.Streuung <= 0) f.Add($"{p}: streuung muss größer als 0 sein.");
            if (v.Volljaehrig < 1 || v.Startalter < v.Volljaehrig) f.Add($"{p}: volljaehrig muss ≥ 1 und das startalter volljährig sein.");
            if (v.FruchtbarVon > v.FruchtbarBis) f.Add($"{p}: fruchtbarVon muss ≤ fruchtbarBis sein.");
            if (v.Geburtenrate is < 0 or > 1) f.Add($"{p}: geburtenrate muss zwischen 0 und 1 liegen.");
            if (v.Haushalt < 0 || v.Unterhalt < 0 || v.Hochzeit < 0) f.Add($"{p}: Kosten dürfen nicht negativ sein.");
            if (v.Werbefaktor <= 0) f.Add($"{p}: werbefaktor muss größer als 0 sein.");
            if (v.VornamenMann.Count == 0 || v.VornamenFrau.Count == 0 || v.Familiennamen.Count == 0)
                f.Add($"{p}: vornamenMann, vornamenFrau und familiennamen dürfen nicht leer sein.");
        }
    }
}
