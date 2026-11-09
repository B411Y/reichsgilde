namespace Reichsgilde.Core.Geheimnisse;

/// <summary>Die vier Arten von Geheimnissen (Charaktersystem Teil 3c).</summary>
public enum Geheimnisart
{
    /// <summary>Abstrakt: keine Liebschaft als Person, keine Kinder daraus (Entscheidung des Nutzers).</summary>
    Ehebruch,
    /// <summary>Heimlich gegen den Glauben, den die Person nach außen hat.</summary>
    Ketzerei,
    /// <summary>Das Oberhaupt steckt tief in Schulden (Bargeld im Minus oder hohe Kredite) – endet von selbst.</summary>
    Schulden,
    /// <summary>Ein Opfer starb an Krötenschleim oder Giftdolch; Täter ist das Oberhaupt des Auftraggebers. Verjährt nie.</summary>
    Mord,
}

/// <summary>Was man mit einem bekannten Geheimnis tun kann (Befehl <see cref="Wirtschaft.GeheimnisNutzen"/>). Schweigen braucht keinen Befehl.</summary>
public enum Geheimnisnutzung
{
    /// <summary>Erpressen um Gulden: ein Erpresserbrief wie der vorhandene.</summary>
    Gulden,
    /// <summary>Erpressen um die Ratsstimme: In der nächsten Sitzung stimmt der Erpresste wie der Erpresser.</summary>
    Ratsstimme,
    /// <summary>Anzeige vor Gericht: Das Geheimnis zählt als Beweis.</summary>
    Anzeige,
    /// <summary>Bloßstellen: öffentlich, ohne Gericht und ohne Spur.</summary>
    Blossstellen,
}

public static class GeheimnisTexte
{
    public static string Name(this Geheimnisnutzung n) => n switch
    {
        Geheimnisnutzung.Gulden => "Erpressen um Gulden",
        Geheimnisnutzung.Ratsstimme => "Erpressen um die Ratsstimme",
        Geheimnisnutzung.Anzeige => "Anzeige vor Gericht",
        _ => "Bloßstellen",
    };
}

/// <summary>Eine Art von Geheimnis (Abschnitt "geheimnisse.arten"): wie es entsteht und was es in der Hand eines anderen wert ist.</summary>
public sealed class GeheimnisArtDaten
{
    /// <summary>„Ehebruch“ – für Fenster, Meldungen und Gericht.</summary>
    public string Name { get; init; } = "";

    /// <summary>Ein Satz für den Tooltip („Hintergeht heimlich den Ehepartner.“).</summary>
    public string Beschreibung { get; init; } = "";

    /// <summary>Chance je Jahr für eine Person, die es haben kann (Ehebruch: verheiratet; Ketzerei: nur mit einem Zug aus <see cref="NurMit"/>).</summary>
    public double Chance { get; init; }

    /// <summary>Faktor je Charakterzug auf <see cref="Chance"/> (mehrere werden multipliziert).</summary>
    public Dictionary<string, double> Zuege { get; init; } = new();

    /// <summary>Die jährliche Chance gilt nur mit einem dieser Züge (leer: für alle).</summary>
    public List<string> NurMit { get; init; } = new();

    /// <summary>Gewichte je Zug, wenn ein Ratsherr ein Geheimnis bekommt (ohne Angabe: <see cref="Zuege"/>).</summary>
    public Dictionary<string, double>? RatsherrZuege { get; init; }

    // ── Ehebruch ────────────────────────────────────────────────────────────

    /// <summary>Denkt die Person höchstens so über ihren Ehepartner …</summary>
    public double MeinungEhepartnerAb { get; init; } = -25;

    /// <summary>… gilt dieser Faktor auf die Chance (0 oder 1: ohne).</summary>
    public double MeinungFaktor { get; init; } = 1;

    /// <summary>Ab dieser Stressstufe (2 = zermürbt) gilt <see cref="StressFaktor"/> (0: nie).</summary>
    public int StressStufe { get; init; }

    public double StressFaktor { get; init; } = 1;

    // ── Ketzerei ────────────────────────────────────────────────────────────

    /// <summary>Chance bei einem Glaubenswechsel (Konvertieren, Übertritt bei der Heirat).</summary>
    public double Wechsel { get; init; }

    /// <summary>Faktor je Zug auf <see cref="Wechsel"/> (Fromm 2, Zynisch 0).</summary>
    public Dictionary<string, double> WechselZuege { get; init; } = new();

    /// <summary>Anzeigbar nur, wenn ein Stadtglaube gilt und der Inhaber sich nach außen zu ihm bekennt (<see cref="Geheimniswesen.KeinVergehen"/>).</summary>
    public bool NurMitStadtglaube { get; init; }

    // ── Schulden ────────────────────────────────────────────────────────────

    /// <summary>Schulden, wenn das Bargeld am Jahresende negativ ist oder die Kredite mehr als dieser Anteil des Reinvermögens sind.</summary>
    public double KreditAnteil { get; init; }

    /// <summary>Bloßgestellt: Faktor auf den Kreditrahmen bei den Geldleihern, solange die Schande wirkt (1: ohne).</summary>
    public double KreditFaktor { get; init; } = 1;

    // ── Anzeige vor Gericht ─────────────────────────────────────────────────

    /// <summary>So viel Beweiskraft hat das Geheimnis vor Gericht (0: kein Vergehen, nicht anzeigbar).</summary>
    public double Beweiskraft { get; init; }

    /// <summary>Buße bei einem Schuldspruch (dazu die der übrigen Beweise; höchstens der Anteil am Vermögen aus "justiz").</summary>
    public double Busse { get; init; }

    /// <summary>Jahre Kerker bei einem Schuldspruch (Mord 1).</summary>
    public int KerkerJahre { get; init; }

    // ── Bloßstellen ─────────────────────────────────────────────────────────

    /// <summary>So viel Ansehen verliert das Haus des Inhabers (beim Ratsherrn: Wahlpunkte) für <see cref="GeheimnisDaten.AnsehenJahre"/>.</summary>
    public double Ansehen { get; init; }

    /// <summary>Ehebruch: was der Ehepartner danach über den Inhaber denkt (verblasst).</summary>
    public double MeinungEhepartner { get; init; }

    /// <summary>Ketzerei: was die Ratsherren desselben Glaubens danach über den Inhaber denken.</summary>
    public double MeinungGlaube { get; init; }

    /// <summary>Mord: was die Angehörigen des Opfers danach über den Inhaber denken.</summary>
    public double MeinungAngehoerige { get; init; }
}

/// <summary>Stress durch Geheimnisse (Abschnitt "geheimnisse.stress"), nur für Stressträger (Teil 3b).</summary>
public sealed class GeheimnisStressDaten
{
    /// <summary>Je Jahr für jedes Geheimnis, das die Person hütet (solange es geheim ist).</summary>
    public double Jahr { get; init; } = 2;

    /// <summary>Stattdessen so viel je Jahr mit diesem Zug (Ehrlich, Fromm 5; der höchste zählt).</summary>
    public Dictionary<string, double> JahrZuege { get; init; } = new();

    /// <summary>Erpresst werden (Gulden oder Ratsstimme).</summary>
    public double Erpresst { get; init; } = 10;

    /// <summary>Bloßgestellt oder angezeigt werden.</summary>
    public double Blossgestellt { get; init; } = 15;
}

/// <summary>Wie Geheimnisse ans Licht kommen (Abschnitt "geheimnisse.entdecken").</summary>
public sealed class GeheimnisEntdeckenDaten
{
    /// <summary>Spitzel auf ein Haus: Chance je Geheimnis von Oberhaupt, Ehepartner oder Erbe.</summary>
    public double Spitzel { get; init; } = 0.3;

    /// <summary>Je Stern „Bei Nacht und Nebel“: das eigene Oberhaupt plus, der Inhaber minus.</summary>
    public double SpitzelJeStern { get; init; } = 0.03;

    public double SpitzelMin { get; init; } = 0.1;
    public double SpitzelMax { get; init; } = 0.6;

    /// <summary>Spitzel auf einen Ratsherrn: Chance je Geheimnis.</summary>
    public double SpitzelRatsherr { get; init; } = 0.4;

    /// <summary>Gerede: jedes Haus je fremdem Geheimnis und Jahr (dazu der Knoten Gerüchteküche).</summary>
    public double Gerede { get; init; } = 0.02;

    /// <summary>Beichte: Eine Kirche desselben Glaubens erfährt es im Jahr mit dieser Chance (nutzbar erst nach dem Beichtbruch).</summary>
    public double Beichte { get; init; } = 0.25;

    /// <summary>Nur wer einen dieser Züge hat, beichtet.</summary>
    public List<string> BeichteZuege { get; init; } = new();

    /// <summary>Der Ehepartner entdeckt einen Ehebruch selbst (je Jahr) – das bleibt privat.</summary>
    public double Ehepartner { get; init; } = 0.1;

    public Dictionary<string, double> EhepartnerZuege { get; init; } = new();

    /// <summary>Was der Ehepartner danach über den Inhaber denkt (verblasst).</summary>
    public double EhepartnerMeinung { get; init; } = -40;

    /// <summary>Stress für beide, wenn der Ehepartner es entdeckt.</summary>
    public double EhepartnerStress { get; init; } = 15;
}

/// <summary>Geheimnisse der Ratsherren (Abschnitt "geheimnisse.ratsherren").</summary>
public sealed class GeheimnisRatsherrenDaten
{
    /// <summary>Chance je Ratsherr, zu Partiebeginn ein Geheimnis zu haben.</summary>
    public double Start { get; init; } = 0.25;

    /// <summary>Chance je Ratsherr und Jahr auf ein weiteres.</summary>
    public double JeJahr { get; init; } = 0.02;

    /// <summary>Welche Arten ein Ratsherr haben kann, mit Grundgewicht (mal den Zug-Gewichten der Art).</summary>
    public Dictionary<Geheimnisart, double> Arten { get; init; } = new();

    /// <summary>So viele Jahre rückt ein verurteilter Ratsherr in kein Amt nach.</summary>
    public int SperreJahre { get; init; } = 3;
}

/// <summary>Was die KI-Häuser mit Geheimnissen tun (Abschnitt "geheimnisse.ki").</summary>
public sealed class GeheimnisKiDaten
{
    /// <summary>Erpresserbrief: dieser Anteil vom Bargeld des Opfers …</summary>
    public double Anteil { get; init; } = 0.1;

    /// <summary>… höchstens so viel.</summary>
    public double Hoechstens { get; init; } = 1500;

    /// <summary>So viele Gulden ist der KI ein Punkt Ansehen wert (Schaden durch Bloßstellen).</summary>
    public double GuldenJeAnsehen { get; init; } = 100;
}

/// <summary>
/// Geheimnisse (Abschnitt "geheimnisse" in wirtschaft.json, ab Datenversion 78, Charaktersystem Teil 3c). Fehlt der Abschnitt, gibt es keine
/// Geheimnisse (alte Spielstände bringen ihre Daten mit).
/// </summary>
public sealed class GeheimnisDaten
{
    /// <summary>Aktionspunkte je Nutzung.</summary>
    public Dictionary<Geheimnisnutzung, int> Ap { get; init; } = new();

    public Dictionary<Geheimnisart, GeheimnisArtDaten> Arten { get; init; } = new();
    public GeheimnisStressDaten Stress { get; init; } = new();
    public GeheimnisEntdeckenDaten Entdecken { get; init; } = new();
    public GeheimnisRatsherrenDaten Ratsherren { get; init; } = new();
    public GeheimnisKiDaten Ki { get; init; } = new();

    /// <summary>So viele Jahre (dieses und die folgenden) wirkt die Schande nach dem Bloßstellen.</summary>
    public int AnsehenJahre { get; init; } = 2;

    /// <summary>Was der Erpresste über den Erpresser denkt (Ratsstimme) …</summary>
    public double MeinungErpresser { get; init; } = -20;

    /// <summary>… und der Bloßgestellte über den, der ihn bloßstellt.</summary>
    public double MeinungBlosssteller { get; init; } = -20;

    public bool IstAktiv => Arten.Count > 0;

    public GeheimnisArtDaten Art(Geheimnisart art) => Arten.TryGetValue(art, out var d) ? d : new GeheimnisArtDaten { Name = art.ToString() };

    internal void Pruefen(List<string> f, ICollection<string>? zuege)
    {
        const string q = "geheimnisse";
        if (!IstAktiv) return;
        foreach (var art in Enum.GetValues<Geheimnisart>().Where(a => !Arten.ContainsKey(a)))
            f.Add($"{q}.arten: Die Art „{art}“ fehlt.");
        foreach (var (art, d) in Arten)
        {
            var wo = $"{q}.arten.{art}";
            if (string.IsNullOrWhiteSpace(d.Name)) f.Add($"{wo}: name fehlt.");
            if (d.Chance is < 0 or > 1 || d.Wechsel is < 0 or > 1) f.Add($"{wo}: chance und wechsel liegen zwischen 0 und 1.");
            if (d.Beweiskraft < 0 || d.Busse < 0 || d.KerkerJahre < 0 || d.Ansehen < 0) f.Add($"{wo}: beweiskraft, busse, kerkerJahre und ansehen dürfen nicht negativ sein.");
            if (d.KreditAnteil < 0 || d.KreditFaktor is < 0 or > 1) f.Add($"{wo}: kreditAnteil ab 0, kreditFaktor zwischen 0 und 1.");
            if (d.MeinungEhepartner > 0 || d.MeinungGlaube > 0 || d.MeinungAngehoerige > 0) f.Add($"{wo}: Meinungen nach dem Bloßstellen sind 0 oder negativ.");
            if (d.StressStufe < 0 || d.StressFaktor < 0 || d.MeinungFaktor < 0) f.Add($"{wo}: stressStufe, stressFaktor und meinungFaktor ab 0.");
            foreach (var z in d.Zuege.Keys.Concat(d.NurMit).Concat(d.WechselZuege.Keys).Concat(d.RatsherrZuege?.Keys ?? Enumerable.Empty<string>())
                         .Distinct().Where(z => zuege is not null && !zuege.Contains(z)))
                f.Add($"{wo}: Den Charakterzug „{z}“ gibt es nicht.");
            if (d.Zuege.Values.Concat(d.WechselZuege.Values).Concat(d.RatsherrZuege?.Values ?? Enumerable.Empty<double>()).Any(x => x < 0))
                f.Add($"{wo}: Zug-Faktoren dürfen nicht negativ sein.");
        }
        foreach (var n in Enum.GetValues<Geheimnisnutzung>())
            if (Ap.GetValueOrDefault(n) is < 0 or > 12) f.Add($"{q}.ap.{n} liegt zwischen 0 und 12.");
        if (Stress.Jahr < 0 || Stress.Erpresst < 0 || Stress.Blossgestellt < 0 || Stress.JahrZuege.Values.Any(x => x < 0))
            f.Add($"{q}.stress: Werte ab 0.");
        var e = Entdecken;
        foreach (var (name, wert) in new[] { ("spitzel", e.Spitzel), ("spitzelMin", e.SpitzelMin), ("spitzelMax", e.SpitzelMax), ("spitzelRatsherr", e.SpitzelRatsherr),
                     ("gerede", e.Gerede), ("beichte", e.Beichte), ("ehepartner", e.Ehepartner) })
            if (wert is < 0 or > 1) f.Add($"{q}.entdecken.{name} liegt zwischen 0 und 1.");
        if (e.SpitzelMin > e.SpitzelMax) f.Add($"{q}.entdecken: spitzelMin höchstens spitzelMax.");
        if (e.EhepartnerMeinung > 0 || e.EhepartnerStress < 0) f.Add($"{q}.entdecken: ehepartnerMeinung höchstens 0, ehepartnerStress ab 0.");
        foreach (var z in e.BeichteZuege.Concat(e.EhepartnerZuege.Keys).Concat(Stress.JahrZuege.Keys).Distinct().Where(z => zuege is not null && !zuege.Contains(z)))
            f.Add($"{q}: Den Charakterzug „{z}“ gibt es nicht.");
        if (Ratsherren.Start is < 0 or > 1 || Ratsherren.JeJahr is < 0 or > 1 || Ratsherren.SperreJahre < 0 || Ratsherren.Arten.Values.Any(x => x < 0))
            f.Add($"{q}.ratsherren: start und jeJahr zwischen 0 und 1, sperreJahre und Gewichte ab 0.");
        if (Ratsherren.Arten.ContainsKey(Geheimnisart.Mord)) f.Add($"{q}.ratsherren.arten: Ratsherren morden nicht (sie handeln nicht selbst).");
        if (AnsehenJahre < 1) f.Add($"{q}.ansehenJahre muss mindestens 1 sein.");
        if (MeinungErpresser > 0 || MeinungBlosssteller > 0) f.Add($"{q}: meinungErpresser und meinungBlosssteller höchstens 0.");
        if (Ki.Anteil is < 0 or > 1 || Ki.Hoechstens < 0 || Ki.GuldenJeAnsehen < 0) f.Add($"{q}.ki: anteil zwischen 0 und 1, hoechstens und guldenJeAnsehen ab 0.");
    }
}
