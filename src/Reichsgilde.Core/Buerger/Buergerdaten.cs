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
    /// <summary>Am Hafen, bei der Hafenarbeit (<see cref="Buerger.Hafenarbeit"/>): Steg, Stapel und Lagerschuppen.</summary>
    Hafen,
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
    /// <summary>Ein Torwächter der Stadt (<see cref="Buerger.Torwache"/>): echte Figur mit Namen, ohne Gewicht für die Wirtschaft.</summary>
    Torwache,
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

    /// <summary>Stelle im Umland (bei Arbeitsort Umland), z. B. „Weide“ – muss unter <see cref="BuergerDaten.Umland"/> stehen.</summary>
    public string? Stelle { get; init; }

    /// <summary>Arbeit im Dienst: Bei Arbeitsort Heim arbeitet er nicht im eigenen Haus, sondern in einem Bürger- oder Ratsherrenhaus.</summary>
    public bool Dienst { get; init; }
}

/// <summary>
/// Wie stark die Einwohner auf die Zahlen der Stadt wirken (0 = gar nicht, Schritt 1; 1 = ganz).
/// Gemischt wird: (1 − Kopplung) × bisherige Rechnung + Kopplung × Rechnung nach den Einwohnern.
/// </summary>
public sealed class KopplungDaten
{
    /// <summary>Gäste der Schänken: Menge und Verteilung auf die Schänken nach den eingekehrten Einwohnern.</summary>
    public double Schaenke { get; init; }

    /// <summary>Messe: Gläubige und Kollekte × (Betende ÷ üblich zur Messe).</summary>
    public double Kirche { get; init; }

    /// <summary>Markt: Nachfrage der Waren aus <see cref="MarktTypen"/> × (Einkaufsstunden der letzten 19 Stunden ÷ üblich am Tag).</summary>
    public double Markt { get; init; }

    /// <summary>Für den Markt eigene, engere Grenze: Faktor zwischen 1 − marktSpanne und 1 + marktSpanne.</summary>
    public double MarktSpanne { get; init; } = 0.2;

    /// <summary>Taschendiebe: Beute × (Passanten dieser Stunde ÷ üblich zu dieser Uhrzeit), begrenzt auf 1 ± spanne.</summary>
    public double Diebe { get; init; }

    /// <summary>So viel dringender wird die Versorgung eines Bestohlenen (er muss nachkaufen).</summary>
    public double Bestohlen { get; init; } = 20;

    /// <summary>Welche Warenarten die Einwohner kaufen (Typ der Ware). Rohstoffe und Zwischenwaren kaufen die Handwerker.</summary>
    public List<string> MarktTypen { get; init; } = new() { "Fertigprodukt", "Heilmittel" };

    /// <summary>So weit darf der Faktor „Einkehrende ÷ üblich“ von 1 abweichen (0,5 = zwischen 0,5 und 1,5).</summary>
    public double Spanne { get; init; } = 0.5;

    /// <summary>Wie schnell sich das „Übliche“ je Uhrzeit an neue Tage anpasst (0,2 = jeder Tag zählt 20 %).</summary>
    public double Glaettung { get; init; } = 0.2;
}

/// <summary>
/// Warenkorb je Volk und Rolle (Abschnitt „buerger.warenkorb“): Wer einkauft, verschiebt die Nachfrage zwischen den
/// Warengruppen. Eine Gruppe sind die Waren eines Handwerks (Beruf der Betriebsart, die sie herstellt).
/// Das Volk wirkt auf die Gruppe, die Rolle auf die Preisklasse (und einzelne Gruppen, z. B. Mönche auf Kirchenbedarf).
/// Die Nachfrage der Stadt in Gulden bleibt gleich – es wird nur umverteilt. Fehlt der Abschnitt, bleibt alles bei 1.
/// </summary>
public sealed class WarenkorbDaten
{
    /// <summary>So weit darf der Faktor einer Ware von 1 abweichen (0,1 = zwischen 0,9 und 1,1).</summary>
    public double Spanne { get; init; } = 0.1;

    /// <summary>Anzeigename je Gruppe (Schlüssel: Beruf), z. B. „Schmied“ → „Schmiedewaren“.</summary>
    public Dictionary<string, string> Gruppen { get; init; } = new();

    /// <summary>Günstig sind Waren mit Grundpreis unter diesem Wert …</summary>
    public double GuenstigBis { get; init; } = 150;

    /// <summary>… teuer ab diesem Grundpreis; dazwischen „mittel“.</summary>
    public double TeuerAb { get; init; } = 250;

    /// <summary>Neigung je Volk und Gruppe (Schlüssel: Beruf), z. B. Zwerg → Schmied 1,5. Fehlt ein Eintrag, gilt 1.</summary>
    public Dictionary<Volk, Dictionary<string, double>> Voelker { get; init; } = new();

    /// <summary>
    /// Neigung je Rolle: Schlüssel „guenstig“, „mittel“, „teuer“ (Preisklasse) oder ein Beruf (Gruppe), z. B.
    /// Tagelöhner → guenstig 1,5, teuer 0,5. Fehlt ein Eintrag, gilt 1.
    /// </summary>
    public Dictionary<string, Dictionary<string, double>> Rollen { get; init; } = new();

    public void Pruefen(List<string> f, string p)
    {
        if (Spanne is < 0 or >= 1) f.Add($"{p}.spanne muss zwischen 0 und unter 1 liegen.");
        if (GuenstigBis <= 0 || TeuerAb < GuenstigBis) f.Add($"{p}: guenstigBis über 0, teuerAb nicht kleiner als guenstigBis.");
        foreach (var (wer, neigungen) in Voelker.Select(x => (x.Key.ToString(), x.Value)).Concat(Rollen.Select(x => (x.Key, x.Value))))
            foreach (var (was, x) in neigungen)
                if (x is <= 0 or > 3) f.Add($"{p}.{wer}.{was}: Neigung über 0 bis 3 (war {x}).");
    }
}

/// <summary>Wie lange Wege dauern (in Spielminuten). Der Kern zählt nur die Zeit – den Weg durch die Gassen zeichnet die Darstellung.</summary>
public sealed class WegeDaten
{
    /// <summary>Ein Weg innerhalb der Stadt dauert im Mittel so lange …</summary>
    public int Stadt { get; init; } = 30;

    /// <summary>… plus/minus so viel (fest je Einwohner und Stunde ausgewürfelt).</summary>
    public int Streuung { get; init; } = 10;

    /// <summary>Weg zum oder vom Stadttor.</summary>
    public int Tor { get; init; } = 40;

    /// <summary>Weg ins oder aus dem Umland (durch das Tor hinaus).</summary>
    public int Umland { get; init; } = 50;

    /// <summary>Nicht alle laufen zur vollen Stunde los: Abmarsch 0 bis so viele Minuten danach.</summary>
    public int Verzoegerung { get; init; } = 9;
}

/// <summary>
/// Tagesplan (Abschnitt „buerger.tagesplan“, Spiel-KI 5.11.2026): Wo ein Einwohner tagsüber seine Stunde verbringt, wenn er sie sonst drinnen
/// verbrächte. Er ändert nur den Ort, nie die Handlung – darum bleiben Einkäufe, Gäste der Schänken und Betende, wie sie waren.
/// Wer welche Stunde draußen ist, folgt fest aus Nummer, Tag und Stunde (ohne Würfel, gleich auf jedem Rechner).
/// </summary>
public sealed class TagesplanDaten
{
    /// <summary>Erste Stunde (Uhrzeit), ab der der Tagesplan gilt …</summary>
    public int Von { get; init; } = 8;

    /// <summary>… und die Stunde, ab der wieder alle drinnen ruhen.</summary>
    public int Bis { get; init; } = 20;

    /// <summary>Anteil der Ruhestunden am Tag, die ein Einwohner draußen rastet (auf dem Markt: Schwätzchen, Brunnen, Bank) statt zu Hause.</summary>
    public double RastDraussen { get; init; }

    /// <summary>Anteil der Dienststunden (Knecht, Magd), in denen er Besorgungen auf dem Markt macht statt im Haus der Herrschaft zu arbeiten.</summary>
    public double Besorgungen { get; init; }

    public void Pruefen(List<string> f, string p)
    {
        var erste = Spielzeit.TagesbeginnMinute / 60;
        var ende = Spielzeit.TagesendeMinute / 60;
        if (Von < erste || Bis > ende || Von >= Bis) f.Add($"{p}: von ≥ {erste}, bis ≤ {ende}, von vor bis.");
        if (RastDraussen is < 0 or > 1) f.Add($"{p}.rastDraussen muss zwischen 0 und 1 liegen.");
        if (Besorgungen is < 0 or > 1) f.Add($"{p}.besorgungen muss zwischen 0 und 1 liegen.");
    }
}

/// <summary>
/// Torwache der Stadt (Abschnitt „buerger.torwache“, Spiel-KI 5.11.2026, P1 „Wachen und Diebe“): je Tor der Karte zwei Mannschaften zu je
/// <see cref="JeTorTag"/> Wächtern, die sich täglich mit Früh- und Spätschicht abwechseln. Nur Bild und Auskunft – ohne Gewicht für die Wirtschaft,
/// ohne Sold. Fehlt der Abschnitt, gibt es keine Torwache (wie bis Datenversion 72).
/// </summary>
public sealed class TorwacheDaten
{
    /// <summary>Rolle der Torwächter (Schlüssel unter „rollen“, Gewicht 0 – kein gewöhnlicher Einwohner bekommt sie).</summary>
    public const string Rolle = "Torwächter";

    /// <summary>Wächter je Tor am Tag (je Schicht).</summary>
    public int JeTorTag { get; init; } = 2;

    /// <summary>Wie viele davon ab <see cref="NachtAb"/> Uhr bleiben.</summary>
    public int JeTorNacht { get; init; } = 1;

    /// <summary>Stunde des Schichtwechsels (Uhrzeit): Die Spätschicht löst die Frühschicht ab.</summary>
    public int Wechsel { get; init; } = 14;

    /// <summary>Ab dieser Stunde steht nur noch die Nachtwache am Tor.</summary>
    public int NachtAb { get; init; } = 20;

    /// <summary>Minuten für den Weg zwischen Stadt und Tor beim Schichtwechsel.</summary>
    public int Weg { get; init; } = 15;

    public void Pruefen(List<string> f, string p, BuergerDaten b)
    {
        var erste = Spielzeit.TagesbeginnMinute / 60;
        var ende = Spielzeit.TagesendeMinute / 60;
        if (JeTorTag is < 1 or > 4) f.Add($"{p}.jeTorTag muss zwischen 1 und 4 liegen.");
        if (JeTorNacht < 1 || JeTorNacht > JeTorTag) f.Add($"{p}.jeTorNacht muss zwischen 1 und jeTorTag liegen.");
        if (Wechsel <= erste || NachtAb <= Wechsel || NachtAb >= ende) f.Add($"{p}: {erste} < wechsel < nachtAb < {ende}.");
        if (Weg is < 1 or > 59) f.Add($"{p}.weg muss zwischen 1 und 59 Minuten liegen.");
        if (!b.Rollen.TryGetValue(Rolle, out var r)) f.Add($"{p}: Die Rolle „{Rolle}“ fehlt unter „rollen“.");
        else if (r.Gewicht != 0) f.Add($"{p}: Die Rolle „{Rolle}“ braucht Gewicht 0 (kein gewöhnlicher Einwohner wird Torwächter).");
    }
}

/// <summary>
/// Hafenarbeit (Abschnitt „buerger.hafenarbeit“, Spiel-KI 6.11.2026, Entladen Stufe 3): Ruht der Schiffshandel und legt das Zierschiff an
/// (<see cref="Zierfahrplan"/>), tragen 2–4 Tagelöhner die Ladung zwischen dem Stapel auf dem Steg und dem Lagerschuppen (Stapelhaus), solange
/// es liegt: morgens vom Niederländer hinein, nachmittags zum Oberländer wieder heraus; ein Vorarbeiter beaufsichtigt. Nur Ort und Weg –
/// keine Handlung, kein Lohn, keine Ware.
/// Fehlt der Abschnitt, gibt es keine Hafenarbeit (der Stapel bleibt wie bis PR #332 zwischen den Besuchen am Steg stehen).
/// </summary>
public sealed class HafenarbeitDaten
{
    /// <summary>Rolle der Träger (Schlüssel unter „rollen“). Bei der Arbeit holt die Hafenarbeit sie nur, wenn die Rolle am Markt arbeitet (dort zählt der Ort für nichts).</summary>
    public string Rolle { get; init; } = "Tagelöhner";

    /// <summary>So viele Träger je Schicht mindestens …</summary>
    public int TraegerVon { get; init; } = 2;

    /// <summary>… und höchstens (fest aus Tag und Schicht gewählt).</summary>
    public int TraegerBis { get; init; } = 4;

    /// <summary>Stücke der Ladung (so viele hebt der Kran; die Darstellung zeigt dieselben).</summary>
    public int Stuecke { get; init; } = 18;

    /// <summary>Minuten für einen Gang: Stück aufnehmen, tragen, ablegen, zurück.</summary>
    public int Runde { get; init; } = 12;

    public void Pruefen(List<string> f, string p, BuergerDaten b)
    {
        if (!b.Rollen.ContainsKey(Rolle)) f.Add($"{p}: Die Rolle „{Rolle}“ fehlt unter „rollen“.");
        if (TraegerVon < 1 || TraegerBis < TraegerVon || TraegerBis > 8) f.Add($"{p}: 1 ≤ traegerVon ≤ traegerBis ≤ 8.");
        if (Stuecke is < 1 or > 40) f.Add($"{p}.stuecke muss zwischen 1 und 40 liegen.");
        if (Runde is < 2 or > 30) f.Add($"{p}.runde muss zwischen 2 und 30 Minuten liegen.");
        // Die Schichten müssen in den Spieltag passen (die Träger kommen frühestens zur ersten Stunde und gehen spätestens zur letzten)
        if (TraegerVon >= 1 && TraegerBis >= TraegerVon && Stuecke >= 1 && Runde >= 1)
            for (var fenster = 0L; fenster < 400; fenster++)
            {
                var s = Hafenarbeit.Planen(this, b.Wege, fenster, TraegerVon);
                // bis höchstens zur letzten Planung (22 Uhr), sonst gingen sie nie mehr heim
                if (s.VonStunde >= Spielzeit.TagesbeginnMinute / 60 && s.BisStunde <= Spielzeit.TagesendeMinute / 60 - 1) continue;
                f.Add($"{p}: Die Hafenarbeit im Fenster {fenster} passt nicht in den Spieltag (Stunden {s.VonStunde}–{s.BisStunde}).");
                break;
            }
    }
}

/// <summary>Wie ein Bedürfnis über den Tag wächst.</summary>
public sealed class BeduerfnisDaten
{
    /// <summary>Dringlichkeit um 04:00 Uhr (Beginn des Spieltags).</summary>
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

    /// <summary>
    /// Gewöhnliche Einwohner je Hausplatz der Karte (Einwohnerplätze und Kulissenhäuser, ohne die Plätze der Ratsherren- und Bürgerhäuser),
    /// festgelegt beim Spielstart: Eine große Stadt hat mehr Häuser und damit mehr Einwohner. Es gilt die größere Zahl aus beiden Regeln
    /// (<see cref="JeHaus"/> × Häuser bzw. jeHausplatz × Hausplätze). 0 = nur je Haus (wie bis Datenversion 72; ohne Karte immer so).
    /// </summary>
    public int JeHausplatz { get; init; }

    /// <summary>Wo die Einwohner tagsüber ihre Stunden verbringen (null = drinnen, wie bis Datenversion 72).</summary>
    public TagesplanDaten? Tagesplan { get; init; }

    /// <summary>Torwache an den Stadttoren (null = keine, wie bis Datenversion 72).</summary>
    public TorwacheDaten? Torwache { get; init; }

    /// <summary>Hafenarbeit der Tagelöhner am Zierschiff (null = keine, wie bis PR #332).</summary>
    public HafenarbeitDaten? Hafenarbeit { get; init; }

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

    /// <summary>Stellen im Umland, an denen Einwohner arbeiten (z. B. Weide, Feld, Wald).</summary>
    public List<string> Umland { get; init; } = new();

    /// <summary>Wie viele Menschen ein Haushalt (ein einfaches Wohnhaus) hat: von … bis.</summary>
    public int HaushaltVon { get; init; } = 4;
    public int HaushaltBis { get; init; } = 6;

    /// <summary>Bürgerhäuser für die Kandidaten aus Bürger- und Patrizierfamilien.</summary>
    public int Buergerhaeuser { get; init; } = 3;

    public WegeDaten Wege { get; init; } = new();

    public KopplungDaten Kopplung { get; init; } = new();

    /// <summary>Warenkorb je Volk und Rolle (null = alle Waren gleich).</summary>
    public WarenkorbDaten? Warenkorb { get; init; }

    /// <summary>Gewichte je Volk und Bedürfnis (1 = wie Menschen; fehlt ein Eintrag, gilt 1).</summary>
    public Dictionary<Volk, Dictionary<Beduerfnis, double>> Gewichte { get; init; } = new();

    /// <summary>Jeder wacht anders auf: Startwert jedes Bedürfnisses ± diese Streuung (damit nicht alle zugleich dasselbe tun).</summary>
    public double StartStreuung { get; init; } = 15;

    /// <summary>Zufälliges Schwanken der Entscheidungen: Nutzen × (1 ± Rauschen). 0 = ganz ohne Zufall.</summary>
    public double Rauschen { get; init; } = 0.2;

    /// <summary>Bonus für die Handlung der letzten Stunde – damit niemand stündlich hin und her springt.</summary>
    public double Beharrung { get; init; } = 5;

    /// <summary>In der Stunde vor der Messe-Abrechnung (z. B. 9–10 Uhr bei Messe um 10) wirkt Beten so viel stärker.</summary>
    public double Messe { get; init; } = 2;

    /// <summary>Gewicht eines Volkes für ein Bedürfnis.</summary>
    public double Gewicht(Volk volk, Beduerfnis b) =>
        Gewichte.TryGetValue(volk, out var g) && g.TryGetValue(b, out var x) ? x : 1;

    public void Pruefen(List<string> f)
    {
        const string p = "buerger";
        if (JeHaus < 0 || JeHaus > 100) f.Add($"{p}.jeHaus muss zwischen 0 und 100 liegen.");
        if (JeHausplatz < 0 || JeHausplatz > 20) f.Add($"{p}.jeHausplatz muss zwischen 0 und 20 liegen.");
        Tagesplan?.Pruefen(f, $"{p}.tagesplan");
        Torwache?.Pruefen(f, $"{p}.torwache", this);
        Hafenarbeit?.Pruefen(f, $"{p}.hafenarbeit", this);
        if (Voelker.Count == 0 || Voelker.Values.Any(g => g < 0) || Voelker.Values.Sum() <= 0)
            f.Add($"{p}.voelker braucht mindestens ein Volk mit Gewicht über 0, keine negativen Gewichte.");
        foreach (var volk in Voelker.Keys.Concat(Gewichte.Keys).Where(v => v is Volk.Ork or Volk.Oger).Distinct())
            f.Add($"{p}: {volk.Mehrzahl()} haben kein Bürgerrecht und leben nicht als Einwohner in der Stadt.");
        if (Rollen.Values.Any(r => r.Arbeitsort == Stadtort.Hafen))
            f.Add($"{p}.rollen: Arbeitsort Hafen gibt es nicht (an den Hafen schickt nur die Hafenarbeit).");
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
        // Wirtshaus 8–22 Uhr: Wer nach dem Schankschluss noch einkehren dürfte, säße in einer geschlossenen Schänke.
        if (Handlungen.TryGetValue(Handlung.Einkehren, out var einkehren) && einkehren.Bis > Wirtschaft.Gastwirtschaft.SchliesstUm)
            f.Add($"{p}.handlungen.Einkehren.bis ({einkehren.Bis}) darf nicht nach dem Schankschluss liegen ({Wirtschaft.Gastwirtschaft.SchliesstUm} Uhr).");
        // Ebenso, wer in der Schänke arbeitet (Rolle mit arbeitsort Schaenke): Arbeiten endet dann spätestens mit dem Schankschluss.
        if (Handlungen.TryGetValue(Handlung.Arbeiten, out var arbeiten) && arbeiten.Bis > Wirtschaft.Gastwirtschaft.SchliesstUm
            && Rollen.Where(r => r.Value.Arbeitsort == Stadtort.Schaenke).Select(r => r.Key).Order(StringComparer.Ordinal).FirstOrDefault() is { } schankrolle)
            f.Add($"{p}.handlungen.Arbeiten.bis ({arbeiten.Bis}) darf nicht nach dem Schankschluss liegen ({Wirtschaft.Gastwirtschaft.SchliesstUm} Uhr), solange die Rolle „{schankrolle}“ in der Schänke arbeitet.");
        foreach (var (volk, g) in Gewichte)
            foreach (var (b, x) in g.Where(x => x.Value is < 0.5 or > 2))
                f.Add($"{p}.gewichte.{volk}.{b}: Gewicht zwischen 0,5 und 2 (war {x}).");
        if (Rauschen is < 0 or > 1) f.Add($"{p}.rauschen muss zwischen 0 und 1 liegen.");
        if (StartStreuung is < 0 or > 50) f.Add($"{p}.startStreuung muss zwischen 0 und 50 liegen.");
        if (Beharrung < 0 || Messe < 1) f.Add($"{p}: beharrung nicht negativ, messe mindestens 1.");
        foreach (var (name, r) in Rollen)
        {
            if (r.Arbeitsort == Stadtort.Umland && (r.Stelle is null || !Umland.Contains(r.Stelle)))
                f.Add($"{p}.rollen.{name}: Wer im Umland arbeitet, braucht eine stelle aus „umland“ ({string.Join(", ", Umland)}).");
            if (r.Arbeitsort != Stadtort.Umland && r.Stelle is not null) f.Add($"{p}.rollen.{name}: stelle gibt es nur bei arbeitsort Umland.");
            if (r.Dienst && r.Arbeitsort != Stadtort.Heim) f.Add($"{p}.rollen.{name}: dienst gibt es nur bei arbeitsort Heim.");
        }
        if (Kopplung.Schaenke is < 0 or > 1) f.Add($"{p}.kopplung.schaenke muss zwischen 0 und 1 liegen.");
        if (Kopplung.Kirche is < 0 or > 1) f.Add($"{p}.kopplung.kirche muss zwischen 0 und 1 liegen.");
        if (Kopplung.Markt is < 0 or > 1) f.Add($"{p}.kopplung.markt muss zwischen 0 und 1 liegen.");
        if (Kopplung.Diebe is < 0 or > 1) f.Add($"{p}.kopplung.diebe muss zwischen 0 und 1 liegen.");
        if (Kopplung.Bestohlen is < 0 or > 100) f.Add($"{p}.kopplung.bestohlen muss zwischen 0 und 100 liegen.");
        if (Kopplung.MarktSpanne is < 0 or >= 1) f.Add($"{p}.kopplung.marktSpanne muss zwischen 0 und unter 1 liegen.");
        if (Kopplung.Spanne is < 0 or >= 1 || Kopplung.Glaettung is <= 0 or > 1) f.Add($"{p}.kopplung: spanne zwischen 0 und unter 1, glaettung über 0 bis 1.");
        Warenkorb?.Pruefen(f, $"{p}.warenkorb");
        if (HaushaltVon < 1 || HaushaltBis < HaushaltVon || HaushaltBis > 20) f.Add($"{p}: haushaltVon ≥ 1, haushaltBis ≥ haushaltVon und ≤ 20.");
        if (Buergerhaeuser < 1) f.Add($"{p}.buergerhaeuser muss mindestens 1 sein.");
        var wg = Wege;
        if (wg.Stadt < 1 || wg.Streuung < 0 || wg.Streuung >= wg.Stadt || wg.Tor < 1 || wg.Umland < 1 || wg.Verzoegerung < 0)
            f.Add($"{p}.wege: stadt, tor, umland mindestens 1; streuung kleiner als stadt; verzoegerung nicht negativ.");
        if (wg.Verzoegerung + Math.Max(wg.Umland, Math.Max(wg.Tor, wg.Stadt + wg.Streuung)) >= 60)
            f.Add($"{p}.wege: Verzögerung plus längster Weg muss unter einer Stunde bleiben – sonst käme jemand erst nach der nächsten Entscheidung an.");
    }
}
