namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Der Markt einer fernen Stadt am Fluss. Er kennt je Ware nur eine Zahl, den „Druck“: wie viele Stück die Häuser dort zuletzt
/// verkauft (bei Waren, die die Stadt kauft) bzw. gekauft haben (bei Waren, die sie verkauft). Der Druck drückt den Preis:
///
///   Stadt kauft:     Preis = Grundpreis × Faktor × (Menge ÷ (Menge + Druck))^Elastizität   – je mehr verkauft, desto weniger
///   Stadt verkauft:  Preis = Grundpreis × Faktor × ((Menge + Druck) ÷ Menge)^Elastizität   – je mehr gekauft, desto teurer
///
/// Jede Spielstunde holt der Markt <see cref="SchiffshandelDaten.ErholungJeStunde"/> davon auf. So lohnt Fernhandel, ohne dass ein Haus
/// dieselbe Ware Fahrt um Fahrt zum selben Preis loswird – und mehrere Häuser teilen sich denselben fernen Markt.
/// </summary>
public sealed class FerneStadt
{
    private readonly Wirtschaftsdaten _daten;
    private readonly SortedDictionary<string, double> _druck = new(StringComparer.Ordinal);

    public FerneStadtDaten Daten { get; }
    public string Name => Daten.Name;

    internal FerneStadt(FerneStadtDaten daten, Wirtschaftsdaten wirtschaft)
    {
        Daten = daten;
        _daten = wirtschaft;
    }

    /// <summary>Kauft die Stadt diese Ware (wir können sie dort verkaufen)?</summary>
    public bool Kauft(string ware) => Daten.Kauft.ContainsKey(ware);

    /// <summary>Verkauft die Stadt diese Ware (wir können sie dort kaufen)?</summary>
    public bool Verkauft(string ware) => Daten.Verkauft.ContainsKey(ware);

    /// <summary>Der Druck dieser Ware: zuletzt gehandelte Stück, die der Markt noch nicht verdaut hat (0 = ausgeglichen).</summary>
    public double Druck(string ware) => _druck.GetValueOrDefault(ware);

    /// <summary>Was die Stadt jetzt für ein Stück zahlt (0, wenn sie die Ware nicht kauft).</summary>
    public double Ankaufpreis(string ware) => AnkaufBei(ware, 0);

    /// <summary>Was ein Stück jetzt dort kostet (0, wenn die Stadt die Ware nicht verkauft).</summary>
    public double Verkaufspreis(string ware) => VerkaufBei(ware, 0);

    private double AnkaufBei(string ware, double zusatz) =>
        Daten.Kauft.TryGetValue(ware, out var d)
            ? _daten.Waren[ware].Grundpreis * d.Faktor * DetMathe.Pow(d.Menge / (d.Menge + Druck(ware) + zusatz), _daten.Allgemein.Elastizitaet)
            : 0;

    private double VerkaufBei(string ware, double zusatz) =>
        Daten.Verkauft.TryGetValue(ware, out var d)
            ? _daten.Waren[ware].Grundpreis * d.Faktor * DetMathe.Pow((d.Menge + Druck(ware) + zusatz) / d.Menge, _daten.Allgemein.Elastizitaet)
            : 0;

    /// <summary>Was der Verkauf so vieler Stück dort brächte (jedes Stück drückt den Preis für das nächste) – ohne zu verkaufen.</summary>
    /// <param name="vorher">So viele Stück wären vorher schon verkauft (für eine Rechnung in Schritten).</param>
    public double Erloes(string ware, int stueck, int vorher = 0)
    {
        double summe = 0;
        for (var i = 0; i < stueck; i++) summe += AnkaufBei(ware, vorher + i);
        return summe;
    }

    /// <summary>Was der Kauf so vieler Stück dort kostete (jedes Stück hebt den Preis für das nächste) – ohne zu kaufen.</summary>
    /// <param name="vorher">So viele Stück wären vorher schon gekauft.</param>
    public double Kosten(string ware, int stueck, int vorher = 0)
    {
        double summe = 0;
        for (var i = 0; i < stueck; i++) summe += VerkaufBei(ware, vorher + i);
        return summe;
    }

    /// <summary>Verkauft an die Stadt: Erlös (vor dem Verhandeln) und der Druck steigt.</summary>
    internal double Verkaufen(string ware, int stueck)
    {
        var erloes = Erloes(ware, stueck);
        _druck[ware] = Druck(ware) + stueck;
        return erloes;
    }

    /// <summary>Kauft bei der Stadt: Kosten (vor dem Verhandeln) und der Druck steigt.</summary>
    internal double Kaufen(string ware, int stueck)
    {
        var kosten = Kosten(ware, stueck);
        _druck[ware] = Druck(ware) + stueck;
        return kosten;
    }

    /// <summary>Eine Spielstunde: Der Markt erholt sich ein Stück.</summary>
    internal void Erholen(double anteil)
    {
        foreach (var ware in _druck.Keys.ToList())
        {
            var neu = _druck[ware] * (1 - anteil);
            if (neu < 0.01) _druck.Remove(ware);
            else _druck[ware] = neu;
        }
    }

    internal Dictionary<string, double> Sichern() => new(_druck);

    internal void Wiederherstellen(IReadOnlyDictionary<string, double> druck)
    {
        _druck.Clear();
        foreach (var (ware, wert) in druck)
            if (Daten.Kauft.ContainsKey(ware) || Daten.Verkauft.ContainsKey(ware))
                _druck[ware] = wert;
    }
}
