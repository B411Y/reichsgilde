using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Wie die Lage einer Rohstoffquelle ausfällt – dieselben vier Stufen wie bei der Ernte (<see cref="Erntegute"/>, gleiche Reihenfolge):
/// gut, gewöhnlich, schlecht, schwer.
/// </summary>
public enum Lagegute
{
    Gut,
    Gewoehnlich,
    Schlecht,
    Schwer,
}

/// <summary>
/// Abschnitt „lagen“ (ab Datenversion 92, Nutzer 15.11.2026): Die Erntejahre gelten für alle Rohstoffquellen. Jede Quelle (ein Umlandbetrieb mit
/// seinen Rohwaren) bekommt alle vier Jahre eine Lage nach denselben Stufen, Chancen, Grenzen und derselben „Härte der Zeit“ wie die Ernte
/// (Werte aus dem Abschnitt „ernte“); nur Waren, Jahreszeit und Texte sind eigen. Fehlt der Abschnitt, gibt es nur die Ernte.
/// </summary>
public sealed class LagenDaten
{
    /// <summary>
    /// Wie stark eine Lage auch den eigenen Umlandbetrieb trifft (0 = gar nicht, 1 = wie die Grundversorgung am Markt): Leistung ×
    /// (1 + (Faktor der Stufe − 1) × Stärke × ertrag). Mit 0,5 liefert eine Grube bei „schwer“ drei Viertel (Entscheidung F2).
    /// </summary>
    public double Ertrag { get; init; } = 0.5;

    /// <summary>Der Umlandbetrieb, dessen Leistung die Ernte trifft (der Abschnitt „ernte“ kennt ihn nicht).</summary>
    public Betriebsart ErnteBetrieb { get; init; } = Betriebsart.Bauernhof;

    /// <summary>Die Quellen außer der Ernte, in fester Reihenfolge.</summary>
    public List<LagenquelleDaten> Quellen { get; init; } = new();

    internal void Pruefen(List<string> f, Wirtschaftsdaten d)
    {
        const string p = "lagen";
        if (d.Ernte is null) f.Add($"{p}: braucht den Abschnitt „ernte“ (Stufen, Chancen und Grenzen gelten für alle Quellen).");
        if (Ertrag is < 0 or > 1) f.Add($"{p}.ertrag muss zwischen 0 und 1 liegen.");
        if (!d.Betriebsarten.TryGetValue(ErnteBetrieb, out var eb) || eb.Ort != Arbeitsort.Umland) f.Add($"{p}.ernteBetrieb muss ein Umlandbetrieb sein.");
        var gesehen = new HashSet<Betriebsart> { ErnteBetrieb };
        for (var i = 0; i < Quellen.Count; i++)
        {
            var q = Quellen[i];
            var qp = $"{p}.quellen[{i}]";
            if (!gesehen.Add(q.Betrieb)) f.Add($"{qp}: Den Betrieb {q.Betrieb} gibt es schon als Quelle.");
            if (!d.Betriebsarten.TryGetValue(q.Betrieb, out var art) || art.Ort != Arbeitsort.Umland) f.Add($"{qp}.betrieb muss ein Umlandbetrieb sein.");
            PruefeWaren(f, $"{qp}.waren", q.Waren, d.Waren, leerErlaubt: false);
            foreach (var (stufe, e) in new[] { ("gut", q.Gut), ("schlecht", q.Schlecht), ("schwer", q.Schwer) })
            {
                if (string.IsNullOrWhiteSpace(e.Titel) || string.IsNullOrWhiteSpace(e.Text) || string.IsNullOrWhiteSpace(e.Vorzeichen))
                    f.Add($"{qp}.{stufe}: titel, text und vorzeichen dürfen nicht leer sein.");
                if (e.Waren is not null) PruefeWaren(f, $"{qp}.{stufe}.waren", e.Waren, d.Waren, leerErlaubt: false);
            }
        }
    }

    private static void PruefeWaren(List<string> f, string p, Dictionary<string, double> waren, IReadOnlyDictionary<string, WareDaten> alle, bool leerErlaubt)
    {
        if (!leerErlaubt && waren.Count == 0) f.Add($"{p}: Mindestens eine Ware.");
        foreach (var (ware, gewicht) in waren)
        {
            if (!alle.ContainsKey(ware)) f.Add($"{p}: Eine Ware „{ware}“ gibt es nicht.");
            if (gewicht is <= 0 or > 1) f.Add($"{p}.{ware} muss größer als 0 und höchstens 1 sein.");
        }
    }
}

/// <summary>Eine Rohstoffquelle: Umlandbetrieb, Jahreszeit der Lage, betroffene Waren und die drei Ereignisse.</summary>
public sealed class LagenquelleDaten
{
    public Betriebsart Betrieb { get; init; }

    /// <summary>In welcher Jahreszeit die Lage beginnt (alle vier Jahre; angesagt im Jahr davor).</summary>
    public Jahreszeit Jahreszeit { get; init; }

    /// <summary>Betroffene Waren und Gewicht: Rohwaren 1, Waren der ersten Werkstatt 0,25 (Entscheidung F1).</summary>
    public Dictionary<string, double> Waren { get; init; } = new();

    public LagenereignisDaten Gut { get; init; } = new();
    public LagenereignisDaten Schlecht { get; init; } = new();
    public LagenereignisDaten Schwer { get; init; } = new();

    public LagenereignisDaten? Von(Lagegute g) => g switch
    {
        Lagegute.Gut => Gut,
        Lagegute.Schlecht => Schlecht,
        Lagegute.Schwer => Schwer,
        _ => null,
    };
}

/// <summary>Ein Ereignis einer Quelle, z. B. „Wassereinbruch“.</summary>
public sealed class LagenereignisDaten
{
    /// <summary>Überschrift, z. B. „Wassereinbruch“ (Meldung, Zeitstrahl).</summary>
    public string Titel { get; init; } = "";

    /// <summary>Im Satz, mit Artikel, z. B. „ein Wassereinbruch in den Gruben“.</summary>
    public string Text { get; init; } = "";

    /// <summary>Was man ein Jahr vorher merkt, z. B. „Die Schneeschmelze drückt Wasser in die Stollen“.</summary>
    public string Vorzeichen { get; init; } = "";

    /// <summary>Eigene Waren und Gewichte nur für dieses Ereignis (z. B. reiche Ader: Silber voll, Eisen halb); sonst die der Quelle.</summary>
    public Dictionary<string, double>? Waren { get; init; }
}
