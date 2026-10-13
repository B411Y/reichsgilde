namespace Reichsgilde.Core.Wirtschaft;

public enum Betriebsart
{
    /// <summary>Werkstatt des Schmieds, Stufe 1. Stellt Beschlag, Rapier und Silberring her.</summary>
    Giesserei,

    /// <summary>Umlandbetrieb (gepachtet). Fördert Eisen und Silber.</summary>
    Mine,
}

/// <summary>
/// Ein Betrieb mit Arbeitskräften, der während der Arbeitszeit produziert.
///
/// Vereinfachung für den Vertical Slice: Ein „Verwalter" kauft die Zutaten genau dann
/// auf dem Markt, wenn ein Stück fertig wird, und verkauft das Stück sofort.
/// Lager, Karren und Transportwege kommen in einem späteren Schritt.
/// </summary>
public sealed class Betrieb
{
    private readonly Wirtschaftsdaten _daten;
    private readonly Dictionary<string, int> _belegung = new();
    private double _fortschritt;        // Gießerei: Arbeitsminuten am aktuellen Stück
    private double _fortschrittEisen;   // Mine
    private double _fortschrittSilber;  // Mine
    private string _produkt = "Silberring";
    private double _silberanteil = 0.5;

    public string Name { get; }
    public Betriebsart Art { get; }
    public Konto Konto { get; }

    public Arbeitsort Ort => Art == Betriebsart.Mine ? Arbeitsort.Umland : Arbeitsort.Werkstatt;

    public int Plaetze => Art == Betriebsart.Mine ? _daten.Allgemein.PlaetzeMine : _daten.Allgemein.PlaetzeGiesserei;

    public IReadOnlyDictionary<string, int> Belegung => _belegung;

    public int Belegt => _belegung.Values.Sum();

    /// <summary>Was die Gießerei herstellt. Nur Waren mit Zutaten (Beschlag, Rapier, Silberring).</summary>
    public string Produkt
    {
        get => _produkt;
        set
        {
            if (Art != Betriebsart.Giesserei)
                throw new InvalidOperationException("Nur eine Gießerei hat ein Produkt.");
            if (!_daten.Waren.TryGetValue(value, out var ware) || ware.Zutaten.Count == 0)
                throw new ArgumentException($"„{value}“ kann eine Gießerei nicht herstellen.");
            if (value != _produkt) _fortschritt = 0;
            _produkt = value;
        }
    }

    /// <summary>Mine: Anteil der Arbeitszeit für Silber (0 = nur Eisen, 1 = nur Silber).</summary>
    public double Silberanteil
    {
        get => _silberanteil;
        set => _silberanteil = Math.Clamp(value, 0, 1);
    }

    internal Betrieb(string name, Betriebsart art, Konto konto, Wirtschaftsdaten daten)
    {
        Name = name;
        Art = art;
        Konto = konto;
        _daten = daten;
    }

    /// <summary>
    /// Legt fest, wie viele Arbeitskräfte einer Art hier arbeiten.
    /// Prüft dabei das geltende Fronstatut und die Zahl der Arbeitsplätze.
    /// </summary>
    /// <exception cref="InvalidOperationException">Wenn die Belegung nicht erlaubt ist – mit verständlicher Begründung.</exception>
    public void Einstellen(string arbeitskraft, int anzahl, Fronstatut statut)
    {
        if (!_daten.Arbeitskraefte.TryGetValue(arbeitskraft, out var kraft))
            throw new ArgumentException($"Die Arbeitskraft „{arbeitskraft}“ gibt es nicht.");
        if (anzahl < 0)
            throw new ArgumentOutOfRangeException(nameof(anzahl));
        if (anzahl > 0 && !kraft.IstErlaubt(statut, Ort))
            throw new InvalidOperationException(
                $"{arbeitskraft} darf unter dem Statut „{statut.Name()}“ nicht im {(Ort == Arbeitsort.Umland ? "Umland" : "Werkstattbetrieb")} arbeiten.");

        var andere = Belegt - _belegung.GetValueOrDefault(arbeitskraft);
        if (andere + anzahl > Plaetze)
            throw new InvalidOperationException($"{Name} hat nur {Plaetze} Arbeitsplätze ({andere} sind schon belegt).");

        if (anzahl == 0) _belegung.Remove(arbeitskraft);
        else _belegung[arbeitskraft] = anzahl;
    }

    /// <summary>Entlässt alle Arbeitskräfte, die unter dem Statut nicht erlaubt sind.</summary>
    /// <returns>Die Namen und Anzahlen der Entlassenen.</returns>
    internal List<(string Kraft, int Anzahl)> UnerlaubteEntlassen(Fronstatut statut)
    {
        var entlassen = _belegung
            .Where(b => !_daten.Arbeitskraefte[b.Key].IstErlaubt(statut, Ort))
            .Select(b => (b.Key, b.Value))
            .ToList();
        foreach (var (kraft, _) in entlassen)
            _belegung.Remove(kraft);
        return entlassen;
    }

    /// <summary>Arbeitsminuten, die die Belegschaft in einer Spielminute schafft.</summary>
    public double LeistungProMinute =>
        _belegung.Sum(b => b.Value * _daten.Arbeitskraefte[b.Key].TempoAm(Ort));

    /// <summary>Eine Spielminute Arbeit. Wird nur während der Arbeitszeit aufgerufen.</summary>
    internal void Arbeitsminute(Markt markt)
    {
        var leistung = LeistungProMinute;
        if (leistung <= 0) return;

        if (Art == Betriebsart.Giesserei)
        {
            _fortschritt += leistung;
            var zeit = _daten.Waren[_produkt].Zeit;
            while (_fortschritt >= zeit)
            {
                _fortschritt -= zeit;
                StueckFertig(_produkt, markt);
            }
        }
        else
        {
            _fortschrittEisen += leistung * (1 - _silberanteil);
            _fortschrittSilber += leistung * _silberanteil;
            Foerdern("Eisen", ref _fortschrittEisen, markt);
            Foerdern("Silber", ref _fortschrittSilber, markt);
        }
    }

    private void StueckFertig(string ware, Markt markt)
    {
        // Verwalter: Zutaten einkaufen ...
        foreach (var (zutat, menge) in _daten.Waren[ware].Zutaten)
            Konto.Zutaten(markt[zutat].Kaufen(menge));

        // ... und das fertige Stück verkaufen.
        Verkaufen(ware, markt);
    }

    private void Foerdern(string ware, ref double fortschritt, Markt markt)
    {
        var zeit = _daten.Waren[ware].Zeit;
        while (fortschritt >= zeit)
        {
            fortschritt -= zeit;
            Verkaufen(ware, markt);
        }
    }

    private void Verkaufen(string ware, Markt markt)
    {
        var erloes = markt[ware].Verkaufen(1);
        Konto.Umsatz(erloes);
        Konto.Steuer(erloes * _daten.Allgemein.Umsatzsteuer);
        Konto.Hergestellt(ware);
    }

    /// <summary>Jahresende: Löhne, Unterhalt bzw. Pacht und zufällige Ausbrüche oder Aufstände.</summary>
    internal void Jahresabrechnung(Random zufall, Action<string> melden)
    {
        foreach (var (name, anzahl) in _belegung)
        {
            var kraft = _daten.Arbeitskraefte[name];
            Konto.Personal(anzahl * kraft.Kosten);

            // Jede unfreie Arbeitskraft hat ihr eigenes Risiko.
            for (var i = 0; i < anzahl; i++)
            {
                if (kraft.Risiko > 0 && zufall.NextDouble() < kraft.Risiko)
                {
                    Konto.Schaden(kraft.Schaden);
                    melden(Art == Betriebsart.Mine
                        ? $"Ausbruch in {Name}: Ein {name} hat sich losgerissen. Schaden: {Zahlenformat.Ganz(kraft.Schaden)} {_daten.Allgemein.Waehrung}."
                        : $"Aufstand in {Name}: Ein {name} hat sich erhoben. Schaden: {Zahlenformat.Ganz(kraft.Schaden)} {_daten.Allgemein.Waehrung}.");
                }
            }
        }

        Konto.Fixkosten(Art == Betriebsart.Mine ? _daten.Allgemein.PachtMine : _daten.Allgemein.UnterhaltGiesserei);
    }
}
