using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Die Ware einer Schänke (Abschnitt <c>ki.schaenke</c>): Eine Werkstatt wählt ihre Ware nach dem Gewinn je Arbeitsminute zum Marktpreis
/// (<see cref="Kalkulation.GewinnJeArbeitsminute"/>). In einer Schänke geht aber ein großer Teil an die Gäste – zum festen Ausschankpreis
/// (<see cref="Gastwirtschaft.Ausschankpreis"/>), der mit der Jahreszeit nicht steigt oder fällt. Sonst wählte die KI im Winter, wenn Weingeist
/// teurer ist, die Körnermahlzeit, obwohl ein Gast für Dünnbier viel mehr zahlt.
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>
    /// Wert einer Arbeitsminute, wenn diese Schänke nur diese Ware herstellt:
    /// (Erlös je Stück − Zutaten zum Marktpreis) ÷ Herstellzeit, mit
    /// Erlös = Anteil der Gäste × Ausschankpreis + Rest × Marktpreis, beides nach Umsatzsteuer.
    /// Anteil der Gäste = erwartete Gäste im Jahr × <see cref="KiSchaenkeDaten.Gaesteanteil"/> ÷ Stück im Jahr, höchstens 1;
    /// Stück im Jahr = Leistung × Arbeitsminuten ÷ Minuten je Stück (ohne Personal: alles für die Gäste).
    /// Für Waren, die nicht ausgeschenkt werden (Weingeist), und ohne <c>ki.schaenke</c> genau <see cref="Kalkulation.GewinnJeArbeitsminute"/>.
    /// </summary>
    public double SchankwertJeArbeitsminute(Betrieb betrieb, string ware)
    {
        var g = _w.Gastwirtschaft;
        if (_w.Daten.Ki?.Schaenke is not { } k || !betrieb.IstSchaenke || !g.IstAktiv || !g.Daten.Ausschank.Contains(ware))
            return GewinnJeArbeitsminute(ware);
        var daten = _w.Daten.Waren[ware];
        var stueckImJahr = Kalkulation.Leistung(_w, betrieb) * betrieb.Arbeitsminuten / betrieb.MinutenJeStueck(ware);
        var gaeste = g.ErwarteteGaeste(betrieb) * k.Gaesteanteil;
        var anteil = stueckImJahr > 0 ? Math.Min(1, gaeste / stueckImJahr) : 1;
        var preis = anteil * g.Ausschankpreis(ware) + (1 - anteil) * _w.Markt[ware].Preis;
        var zutaten = daten.Zutaten.Sum(z => _w.Markt[z.Key].Preis * z.Value);
        return (preis * (1 - _w.Umsatzsteuer) - zutaten) / daten.Zeit;
    }

    /// <summary>Wert einer Ware für die Produktwahl dieses Betriebs: in einer Schänke mit <c>ki.schaenke</c> nach dem Ausschank, sonst zum Marktpreis.</summary>
    private double ProduktWert(Betrieb betrieb, string ware) =>
        betrieb.IstSchaenke ? SchankwertJeArbeitsminute(betrieb, ware) : GewinnJeArbeitsminute(ware);
}
