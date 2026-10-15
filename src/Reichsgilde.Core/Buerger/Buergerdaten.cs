using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Buerger;

// ─────────────────────────────────────────────────────────────────────────────
//  Bürger-KI: Grundbegriffe
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Was einen Einwohner antreibt. Jedes Bedürfnis ist eine Dringlichkeit von 0 (gestillt) bis 100 (sehr dringend).
/// Über den Tag steigt jedes Bedürfnis an, Handlungen stillen es wieder.
/// </summary>
public enum Beduerfnis
{
    /// <summary>Geld verdienen, sein Tagwerk tun.</summary>
    Auskommen,
    /// <summary>Brot, Bier, Kerzen, Tuch – was ein Haushalt am Markt kauft.</summary>
    Versorgung,
    /// <summary>Geselligkeit, Trinken, Würfeln, Klatsch.</summary>
    Vergnuegen,
    /// <summary>Messe, Gebet, Beichte.</summary>
    Glaube,
    /// <summary>Essen, Schlafen, Feierabend im eigenen Heim.</summary>
    Ruhe,
}

/// <summary>Was ein Einwohner in einer Stunde tun kann. Jede Handlung stillt einige Bedürfnisse.</summary>
public enum Handlung
{
    Ruhen,
    Arbeiten,
    Einkaufen,
    Einkehren,
    Beten,
    Flanieren,
}

/// <summary>Wo sich ein Einwohner gerade aufhält – das liest die Darstellung, um Passanten zu zeigen.</summary>
public enum Stadtort
{
    /// <summary>Zu Hause (Wohnhaus in der Stadt).</summary>
    Heim,
    Markt,
    /// <summary>Gassen und Plätze der Stadt.</summary>
    Gasse,
    /// <summary>In einer Schänke (welche: <see cref="Einwohner.Betrieb"/>).</summary>
    Schaenke,
    /// <summary>In einer Kirche (welche: <see cref="Einwohner.Betrieb"/>; leer = die Stadtkirche).</summary>
    Kirche,
    Rathaus,
    /// <summary>Am Stadttor (Boten, Fuhrleute).</summary>
    Tor,
    /// <summary>Vor der Stadt (Felder, Weiden, Wald).</summary>
    Umland,
}

/// <summary>Woher ein Einwohner stammt.</summary>
public enum Herkunft
{
    /// <summary>Gewöhnlicher Bewohner der Stadt – nur die Bürger-KI kennt ihn.</summary>
    Stadt,
    /// <summary>Ein Ratsherr aus dem Stadtrat; Wahl und Gunst regelt weiter der Rat.</summary>
    Ratsherr,
    /// <summary>Ein Heiratskandidat aus der Brautschau; Heirat und Weggang regelt weiter das Familienbuch.</summary>
    Kandidat,
}

// ─────────────────────────────────────────────────────────────────────────────
//  Daten (Abschnitt „buerger" in wirtschaft.json, ab Datenversion 25)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Eine Rolle in der Stadt, z. B. Tagelöhner oder Magd – ohne eigenen Betrieb.</summary>
public sealed class RolleDaten
{
    /// <summary>Wie oft die Rolle unter den gewöhnlichen Einwohnern vorkommt (0 = nur für Ratsherren/Kandidaten).</summary>
    public double Gewicht { get; init; } = 1;

    /// <summary>Wo die Rolle ihr Tagwerk verrichtet.</summary>
    public Stadtort Arbeitsort { get; init; } = Stadtort.Markt;

    /// <summary>Bezeichnung für Frauen (leer = wie der Name der Rolle), z. B. „Magd" zu „Knecht".</summary>
    public string Frau { get; init; } = "";

    /// <summary>Darf ein lediger Heiratskandidat (aus dem Stand Gemeiner) diese Rolle haben?</summary>
    public bool Ledig { get; init; } = true;
}

/// <summary>Wie ein Bedürfnis über den Tag wächst.</summary>
public sealed class BeduerfnisDaten
{
    /// <summary>Dringlichkeit um 05:00 Uhr (Beginn des Spieltags).</summary>
    public double Start { get; init; }

    /// <summary>So viel steigt die Dringlichkeit je Stunde.</summary>
    public double Anstieg { get; init; } = 5;
}

/// <summary>Wann eine Handlung möglich ist und was sie stillt.</summary>
public sealed class HandlungDaten
{
    /// <summary>Erste Stunde (Uhrzeit), in der die Handlung möglich ist.</summary>
    public int Von { get; init; } = Spielzeit.TagesbeginnMinute / 60;

    /// <summary>Stunde, ab der sie nicht mehr möglich ist.</summary>
    public int Bis { get; init; } = Spielzeit.TagesendeMinute / 60;

    /// <summary>Um wie viel die Handlung in einer Stunde jedes Bedürfnis senkt.</summary>
    public Dictionary<Beduerfnis, double> Stillt { get; init; } = new();
}

/// <summary>
/// Die Einwohner der Stadt (Bürger-KI). Je Haus leben <see cref="JeHaus"/> gewöhnliche Einwohner in der Stadt,
/// dazu auf Wunsch die Ratsherren und die Heiratskandidaten. Sie führen keinen Betrieb, sondern folgen einem Tagesablauf
/// nach ihren Bedürfnissen. In diesem Schritt haben sie noch keine Wirkung auf Markt, Schänke oder Kirche.
/// </summary>
public sealed class BuergerDaten
{
    /// <summary>Gewöhnliche Einwohner je Haus (Spieler und KI), festgelegt beim Spielstart.</summary>
    public int JeHaus { get; init; } = 25;

    /// <summary>Sind die Ratsherren als Einwohner in der Stadt unterwegs?</summary>
    public bool MitRatsherren { get; init; } = true;

    /// <summary>Sind die Heiratskandidaten als Einwohner in der Stadt unterwegs?</summary>
    public bool MitKandidaten { get; init; } = true;

    /// <summary>Wie sich die gewöhnlichen Einwohner auf die Völker verteilen (Gewichte). Orks und Oger haben kein Bürgerrecht.</summary>
    public Dictionary<Volk, double> Voelker { get; init; } = new();

    /// <summary>Rollen der gewöhnlichen Einwohner (Name → Daten).</summary>
    public Dictionary<string, RolleDaten> Rollen { get; init; } = new();

    /// <summary>Rolle der Ratsherren.</summary>
    public string RolleRatsherr { get; init; } = "Ratsherr";

    /// <summary>Rolle der Heiratskandidaten aus Bürger- und Patrizierfamilien.</summary>
    public string RolleKandidatAbBuerger { get; init; } = "Bürgersohn";

    public Dictionary<Beduerfnis, BeduerfnisDaten> Beduerfnisse { get; init; } = new();

    public Dictionary<Handlung, HandlungDaten> Handlungen { get; init; } = new();

    /// <summary>Gewichte je Volk und Bedürfnis (1 = wie Menschen; fehlt ein Eintrag, gilt 1).</summary>
    public Dictionary<Volk, Dictionary<Beduerfnis, double>> Gewichte { get; init; } = new();

    /// <summary>Jeder wacht anders auf: Startwert jedes Bedürfnisses ± diese Streuung (damit nicht alle zugleich dasselbe tun).</summary>
    public double StartStreuung { get; init; } = 15;

    /// <summary>Zufälliges Schwanken der Entscheidungen: Nutzen × (1 ± Rauschen). 0 = ganz ohne Zufall.</summary>
    public double Rauschen { get; init; } = 0.2;

    /// <summary>Bonus für die Handlung der letzten Stunde – damit niemand stündlich hin und her springt.</summary>
    public double Beharrung { get; init; } = 5;

    /// <summary>Zur Messe-Stunde der Kirchen wirkt Beten so viel stärker.</summary>
    public double Messe { get; init; } = 2;

    /// <summary>Gewicht eines Volkes für ein Bedürfnis.</summary>
    public double Gewicht(Volk volk, Beduerfnis b) =>
        Gewichte.TryGetValue(volk, out var g) && g.TryGetValue(b, out var x) ? x : 1;

    public void Pruefen(List<string> f)
    {
        const string p = "buerger";
        if (JeHaus < 0 || JeHaus > 100) f.Add($"{p}.jeHaus muss zwischen 0 und 100 liegen.");
        if (Voelker.Count == 0 || Voelker.Values.Any(g => g < 0) || Voelker.Values.Sum() <= 0)
            f.Add($"{p}.voelker braucht mindestens ein Volk mit Gewicht über 0, keine negativen Gewichte.");
        foreach (var volk in Voelker.Keys.Concat(Gewichte.Keys).Where(v => v is Volk.Ork or Volk.Oger).Distinct())
            f.Add($"{p}: {volk.Mehrzahl()} haben kein Bürgerrecht und leben nicht als Einwohner in der Stadt.");
        if (Rollen.Count == 0 || Rollen.Values.Any(r => r.Gewicht < 0) || Rollen.Values.Sum(r => r.Gewicht) <= 0)
            f.Add($"{p}.rollen braucht mindestens eine Rolle mit Gewicht über 0, keine negativen Gewichte.");
        if (!Rollen.Values.Any(r => r.Ledig && r.Gewicht > 0))
            f.Add($"{p}.rollen: Mindestens eine Rolle mit Gewicht muss „ledig“ sein (für die Heiratskandidaten).");
        if (MitRatsherren && !Rollen.ContainsKey(RolleRatsherr)) f.Add($"{p}.rolleRatsherr: Die Rolle „{RolleRatsherr}“ fehlt unter „rollen“.");
        if (MitKandidaten && !Rollen.ContainsKey(RolleKandidatAbBuerger))
            f.Add($"{p}.rolleKandidatAbBuerger: Die Rolle „{RolleKandidatAbBuerger}“ fehlt unter „rollen“.");
        foreach (var b in Enum.GetValues<Beduerfnis>())
            if (!Beduerfnisse.TryGetValue(b, out var bd)) f.Add($"{p}.beduerfnisse: {b} fehlt.");
            else if (bd.Start is < 0 or > 100 || bd.Anstieg < 0) f.Add($"{p}.beduerfnisse.{b}: start zwischen 0 und 100, anstieg nicht negativ.");
        var erste = Spielzeit.TagesbeginnMinute / 60;
        var ende = Spielzeit.TagesendeMinute / 60;
        foreach (var h in Enum.GetValues<Handlung>())
            if (!Handlungen.TryGetValue(h, out var hd)) f.Add($"{p}.handlungen: {h} fehlt.");
            else
            {
                if (hd.Von < erste || hd.Bis > ende || hd.Von >= hd.Bis) f.Add($"{p}.handlungen.{h}: von ≥ {erste}, bis ≤ {ende}, von vor bis.");
                if (hd.Stillt.Count == 0 || hd.Stillt.Values.Any(x => x <= 0)) f.Add($"{p}.handlungen.{h}.stillt: mindestens ein Bedürfnis mit Wert über 0.");
            }
        if (Handlungen.TryGetValue(Handlung.Ruhen, out var ruhen) && (ruhen.Von != erste || ruhen.Bis != ende))
            f.Add($"{p}.handlungen.Ruhen muss den ganzen Tag möglich sein ({erste}–{ende}), sonst hätte jemand nichts zu tun.");
        foreach (var (volk, g) in Gewichte)
            foreach (var (b, x) in g.Where(x => x.Value is < 0.5 or > 2))
                f.Add($"{p}.gewichte.{volk}.{b}: Gewicht zwischen 0,5 und 2 (war {x}).");
        if (Rauschen is < 0 or > 1) f.Add($"{p}.rauschen muss zwischen 0 und 1 liegen.");
        if (StartStreuung is < 0 or > 50) f.Add($"{p}.startStreuung muss zwischen 0 und 50 liegen.");
        if (Beharrung < 0 || Messe < 1) f.Add($"{p}: beharrung nicht negativ, messe mindestens 1.");
    }
}
