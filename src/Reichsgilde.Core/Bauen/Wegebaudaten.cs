namespace Reichsgilde.Core.Bauen;

/// <summary>
/// Belag der Wege (Abschnitt „wegebau“ in wirtschaft.json, ab Datenversion 52; Kartenwerkstatt, Schritt 5d). Fehlt der Abschnitt, haben
/// Wege keinen Belag im Spiel: Karren fahren wie bisher, nichts tritt aus oder verfällt, gebaut wird nicht.
/// </summary>
public sealed class WegebauDaten
{
    /// <summary>
    /// So schnell fahren Karren je Stufe (Anteil des Tempos auf neuem Pflaster). Schlüssel: „Trampelpfad“, „Fahrweg“, „Kies“, „Pflaster“.
    /// </summary>
    public Dictionary<string, double> Tempo { get; init; } = new() { ["Trampelpfad"] = 0.70, ["Fahrweg"] = 0.85, ["Kies"] = 0.95, ["Pflaster"] = 1.00 };

    /// <summary>
    /// Für diesen Belag gelten die festen Fahrzeiten (<c>logistik.fahrzeiten</c>): Ein Stück mit diesem Tempo dauert so lang wie bisher,
    /// ein schnelleres kürzer, ein langsameres länger. So spielt eine Karte, deren Karrenstraßen alle Fahrwege sind, genau wie vorher.
    /// </summary>
    public Belagstufe Bezug { get; init; } = Belagstufe.Fahrweg;

    /// <summary>Länge eines Wegstücks im Spiel (Meter): Belag, Austritt, Verkehr und Bauaufträge gelten je Stück.</summary>
    public double Stueck { get; init; } = 5;

    /// <summary>Austreten und Verwildern von Trampelpfad und Fahrweg.</summary>
    public WegverkehrDaten Verkehr { get; init; } = new();

    /// <summary>So viel Zustand verlieren Kies und Pflaster je Jahr ohne Unterhalt (Schlüssel „Kies“, „Pflaster“; 1 = neu).</summary>
    public Dictionary<string, double> Verfall { get; init; } = new() { ["Kies"] = 0.05, ["Pflaster"] = 0.03 };

    /// <summary>Unter diesem Zustand fällt der Belag eine Stufe zurück (Pflaster → Kies, Kies → Fahrweg).</summary>
    public double VerfallSchwelle { get; init; } = 0.25;

    /// <summary>Was ein Meter kostet (Gulden; Schlüssel „Kies“, „Pflaster“). Ausbessern kostet den Anteil, der fehlt.</summary>
    public Dictionary<string, double> PreisJeMeter { get; init; } = new() { ["Kies"] = 6, ["Pflaster"] = 15 };

    /// <summary>So viele Spielminuten dauert jeder Bauauftrag mindestens (Baustelle einrichten).</summary>
    public int BauzeitGrund { get; init; } = 60;

    /// <summary>Spielminuten je gebautem Meter (Schlüssel „Kies“, „Pflaster“).</summary>
    public Dictionary<string, double> BauzeitJeMeter { get; init; } = new() { ["Kies"] = 1, ["Pflaster"] = 2 };

    /// <summary>So lang darf ein Bauauftrag höchstens sein (Meter).</summary>
    public double HoechstensMeter { get; init; } = 300;

    /// <summary>Der Stadtrat pflastert Hauptstraßen und Markt; Ratsantrag „Weg pflastern“.</summary>
    public WegeratDaten Rat { get; init; } = new();

    public double TempoVon(Belagstufe s) => Tempo.GetValueOrDefault(s.ToString(), 1);
    public double VerfallVon(Belagstufe s) => Verfall.GetValueOrDefault(s.ToString());
    public double PreisVon(Belagstufe s) => PreisJeMeter.GetValueOrDefault(s.ToString());
    public double BauzeitVon(Belagstufe s) => BauzeitJeMeter.GetValueOrDefault(s.ToString());

    public void Pruefen(List<string> f)
    {
        const string p = "wegebau";
        foreach (var s in Enum.GetValues<Belagstufe>())
            if (!Tempo.TryGetValue(s.ToString(), out var t) || t is <= 0 or > 1) f.Add($"{p}.tempo.{s} fehlt oder liegt nicht über 0 bis 1.");
        foreach (var s in Enum.GetValues<Belagstufe>().Where(s => s < Belagstufe.Pflaster))
            if (TempoVon(s) > TempoVon(s + 1)) f.Add($"{p}.tempo: {s} darf nicht schneller sein als {s + 1}.");
        foreach (var s in new[] { Belagstufe.Kies, Belagstufe.Pflaster })
        {
            if (!PreisJeMeter.TryGetValue(s.ToString(), out var preis) || preis <= 0) f.Add($"{p}.preisJeMeter.{s} fehlt oder ist nicht größer als 0.");
            if (BauzeitVon(s) < 0) f.Add($"{p}.bauzeitJeMeter.{s} darf nicht negativ sein.");
            if (VerfallVon(s) is < 0 or >= 1) f.Add($"{p}.verfall.{s} muss zwischen 0 und 1 liegen.");
        }
        foreach (var k in Tempo.Keys.Concat(Verfall.Keys).Concat(PreisJeMeter.Keys).Concat(BauzeitJeMeter.Keys))
            if (!Enum.TryParse<Belagstufe>(k, out _)) f.Add($"{p}: unbekannte Stufe „{k}“ (erlaubt: {string.Join(", ", Enum.GetNames<Belagstufe>())}).");
        if (Stueck is < 1 or > 50) f.Add($"{p}.stueck muss zwischen 1 und 50 Metern liegen.");
        if (VerfallSchwelle is < 0 or >= 1) f.Add($"{p}.verfallSchwelle muss zwischen 0 und 1 liegen.");
        if (BauzeitGrund < 0) f.Add($"{p}.bauzeitGrund darf nicht negativ sein.");
        if (HoechstensMeter < Stueck) f.Add($"{p}.hoechstensMeter muss mindestens ein Stück lang sein.");
        Verkehr.Pruefen(f);
        Rat.Pruefen(f);
    }
}

/// <summary>
/// Wie Verkehr Trampelpfade austritt und Fahrwege ohne Verkehr verwildern (<c>wegebau.verkehr</c>). Verkehr eines Stücks im Jahr =
/// Karrenfahrten des Vorjahres + Grundverkehr der Wegklasse (Fernhandel, Bauern) + Fußgänger (Gebäude in der Nähe × Gewicht × Faktor).
/// Austritt ändert sich je Jahr um (Verkehr − <see cref="Halten"/>) ÷ <see cref="Voll"/>, höchstens <see cref="HoechstensJeJahr"/> hinauf
/// und <see cref="Verwildern"/> hinab. Ein Trampelpfad mit Austritt 1 wird Fahrweg, ein Fahrweg mit Austritt 0 Trampelpfad.
/// </summary>
public sealed class WegverkehrDaten
{
    /// <summary>So viel Verkehr im Jahr hält einen Weg, wie er ist.</summary>
    public double Halten { get; init; } = 5;

    /// <summary>Je so viel Verkehr über <see cref="Halten"/> steigt der Austritt in einem Jahr um 1.</summary>
    public double Voll { get; init; } = 50;

    /// <summary>Höchstens so viel Austritt mehr je Jahr.</summary>
    public double HoechstensJeJahr { get; init; } = 0.5;

    /// <summary>Höchstens so viel Austritt weniger je Jahr (ein Weg ohne jeden Verkehr).</summary>
    public double Verwildern { get; init; } = 0.2;

    /// <summary>Verkehr je Jahr, den jedes Stück dieser Wegklasse ohnehin hat (Schlüssel wie „Land“, „Haupt“).</summary>
    public Dictionary<string, double> Grundverkehr { get; init; } = new() { ["Land"] = 40, ["Haupt"] = 40 };

    /// <summary>Gebäude bis so weit (Meter) von der Mitte eines Stücks zählen für seinen Fußverkehr.</summary>
    public double FussRadius { get; init; } = 40;

    /// <summary>Fußverkehr je Gebäude dieser Art (Schlüssel: Bausteinart, z. B. „einwohnerplatz“, „sonderbau“).</summary>
    public Dictionary<string, double> FussGewicht { get; init; } = new()
    {
        [Bausteinart.Einwohnerplatz] = 1, [Bausteinart.Wohnhausplatz] = 2, [Bausteinart.Werkstattplatz] = 2, [Bausteinart.Sonderbau] = 5,
    };

    /// <summary>Wie viele Karrenfahrten ein Punkt Fußverkehr wert ist.</summary>
    public double FussFaktor { get; init; } = 1;

    public double GrundverkehrVon(Wegklasse k) => Grundverkehr.GetValueOrDefault(k.ToString());

    public void Pruefen(List<string> f)
    {
        const string p = "wegebau.verkehr";
        if (Halten < 0) f.Add($"{p}.halten darf nicht negativ sein.");
        if (Voll <= 0) f.Add($"{p}.voll muss größer als 0 sein.");
        if (HoechstensJeJahr is <= 0 or > 1) f.Add($"{p}.hoechstensJeJahr muss über 0 bis 1 liegen.");
        if (Verwildern is < 0 or > 1) f.Add($"{p}.verwildern muss zwischen 0 und 1 liegen.");
        if (FussRadius < 0 || FussFaktor < 0) f.Add($"{p}.fussRadius und fussFaktor dürfen nicht negativ sein.");
        foreach (var (k, v) in Grundverkehr)
            if (!Enum.TryParse<Wegklasse>(k, out _) || v < 0) f.Add($"{p}.grundverkehr.{k}: unbekannte Wegklasse oder negativer Wert.");
        foreach (var (k, v) in FussGewicht)
            if (v < 0) f.Add($"{p}.fussGewicht.{k} darf nicht negativ sein.");
    }
}

/// <summary>
/// Der Stadtrat und der Wegebau (<c>wegebau.rat</c>): Jedes Jahr legt die Stadt <see cref="BudgetJeJahr"/> in die Wegekasse (höchstens
/// <see cref="KasseHoechstens"/>). Daraus bessert der Rat zuerst sein Pflaster an Hauptstraßen und Markt aus (unter
/// <see cref="AusbessernAb"/>), dann pflastert er dort den Abschnitt mit dem meisten Verkehr (höchstens <see cref="HoechstensMeterJeJahr"/>).
/// Ein Ratsmitglied kann beantragen, einen Weg zu pflastern: Ratsherren stimmen dafür bei Gunst ab <see cref="AntragGunst"/> oder Verkehr ab
/// <see cref="AntragVerkehr"/>; angenommen zahlt die Wegekasse, und der Antragsteller gewinnt <see cref="AntragAnsehen"/> Ansehen für
/// <see cref="AnsehenJahre"/> Jahre.
/// </summary>
public sealed class WegeratDaten
{
    public double BudgetJeJahr { get; init; } = 600;
    public double KasseHoechstens { get; init; } = 3000;

    /// <summary>Wegstücke bis so weit (Meter) von der Mitte des Marktes zählen zum Markt.</summary>
    public double MarktRadius { get; init; } = 30;

    public double AusbessernAb { get; init; } = 0.6;
    public double HoechstensMeterJeJahr { get; init; } = 100;
    public double AntragGunst { get; init; } = 10;
    public double AntragVerkehr { get; init; } = 30;
    public double AntragAnsehen { get; init; } = 5;
    public int AnsehenJahre { get; init; } = 5;

    public void Pruefen(List<string> f)
    {
        const string p = "wegebau.rat";
        if (BudgetJeJahr < 0 || KasseHoechstens < 0) f.Add($"{p}.budgetJeJahr und kasseHoechstens dürfen nicht negativ sein.");
        if (MarktRadius < 0 || HoechstensMeterJeJahr < 0) f.Add($"{p}.marktRadius und hoechstensMeterJeJahr dürfen nicht negativ sein.");
        if (AusbessernAb is < 0 or > 1) f.Add($"{p}.ausbessernAb muss zwischen 0 und 1 liegen.");
        if (AntragAnsehen < 0 || AnsehenJahre < 0) f.Add($"{p}.antragAnsehen und ansehenJahre dürfen nicht negativ sein.");
    }
}
