using Reichsgilde.Core.Speichern;
namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Ein Marktstand für eine Ware.
///
/// Preisformel (siehe Dokument „Wirtschaftsmodell", Abschnitt 3):
///   Angebot    = Nachfrage × NPC-Anteil + Spieler-Verkäufe des letzten Spieljahres (17 Spielstunden)
///   Faktor     = (Nachfrage / Angebot) ^ Elastizität, begrenzt auf [PreisMin … PreisMax]
///   Marktpreis = Grundpreis × Faktor
///
/// Weil ein Spieltag ein Spieljahr ist, sind „die letzten 17 Spielstunden" (05–22 Uhr) genau ein Jahr.
/// Die Verkäufe werden in 18 Stunden-Fächern gesammelt: dem laufenden Fach und den
/// 17 vorherigen. Vom ältesten Fach zählt nur der Teil, der noch in die letzten 17 Stunden
/// fällt. So umfasst das „gleitende Fenster" zu jeder Minute genau ein Spieljahr.
/// </summary>
public sealed class Marktstand
{
    private readonly WareDaten _ware;
    private readonly AllgemeineDaten _allgemein;
    private readonly double[] _verkaufteStueckProStunde = new double[Zeit.Spielzeit.StundenProTag + 1];
    private int _aktuellesFach;
    private int _minuteInStunde;

    public string Ware { get; }

    public Marktstand(string ware, WareDaten daten, AllgemeineDaten allgemein)
    {
        Ware = ware;
        _ware = daten;
        _allgemein = allgemein;
    }

    /// <summary>Was Spieler und KI-Rivalen im letzten Spieljahr (17 Spielstunden) verkauft haben.</summary>
    public double SpielerAngebotImJahr
    {
        get
        {
            var aeltestes = (_aktuellesFach + 1) % _verkaufteStueckProStunde.Length;
            var anteilAeltestes = 1.0 - _minuteInStunde / 60.0;
            return _verkaufteStueckProStunde.Sum() - _verkaufteStueckProStunde[aeltestes] * (1 - anteilAeltestes);
        }
    }

    /// <summary>Gesamtangebot pro Jahr: NPC-Grundversorgung plus Spieler.</summary>
    public double Gesamtangebot => _ware.Nachfrage * _ware.NpcAnteil + SpielerAngebotImJahr;

    public double Preisfaktor => FaktorBei(0);

    /// <summary>Preisfaktor, wenn das Angebot um <paramref name="zusatz"/> Stück anders wäre (negativ = gekauft).</summary>
    private double FaktorBei(double zusatz)
    {
        var faktor = Math.Pow(_ware.Nachfrage / Math.Max(Gesamtangebot + zusatz, 1e-9), _allgemein.Elastizitaet);
        return Math.Clamp(faktor, _allgemein.PreisMin, _allgemein.PreisMax);
    }

    /// <summary>Aktueller Marktpreis für ein Stück.</summary>
    public double Preis => _ware.Grundpreis * Preisfaktor;

    public double Grundpreis => _ware.Grundpreis;

    /// <summary>Verkauft Ware an den Markt. Jedes Stück drückt den Preis ein wenig.</summary>
    /// <returns>Der Erlös in Gulden.</returns>
    public double Verkaufen(int stueck)
    {
        double erloes = 0;
        for (var i = 0; i < stueck; i++)
        {
            erloes += Preis;
            _verkaufteStueckProStunde[_aktuellesFach]++;
        }
        return erloes;
    }

    /// <summary>
    /// Kauft Ware vom Markt. Käufe verändern den Preis nicht: Die Nachfrage der Stadt
    /// enthält den Bedarf der Handwerker bereits (Entscheidung Wirtschaftsmodell, Entwurf 1).
    /// </summary>
    /// <returns>Die Kosten in Gulden.</returns>
    public double Kaufen(int stueck) => Preis * stueck;

    /// <summary>
    /// Was ein Einkauf von Hand kosten würde: Jedes Stück hebt den Preis für das nächste (es fehlt im Angebot) –
    /// anders als der Bedarf der Werkstätten, den die Nachfrage der Stadt schon enthält.
    /// </summary>
    /// <param name="faktor">Händlerspanne und Verhandeln, z. B. 1,1 × 0,97.</param>
    public double HandKaufpreis(int stueck, double faktor)
    {
        double summe = 0;
        for (var i = 0; i < stueck; i++)
            summe += _ware.Grundpreis * FaktorBei(-i) * faktor;
        return summe;
    }

    /// <summary>Kauft von Hand: wie <see cref="HandKaufpreis"/>, und das Angebot sinkt um die gekauften Stück.</summary>
    internal double HandKaufen(int stueck, double faktor)
    {
        var kosten = HandKaufpreis(stueck, faktor);
        _verkaufteStueckProStunde[_aktuellesFach] -= stueck;
        return kosten;
    }

    /// <summary>Eine Spielstunde ist vorbei: Das älteste Fach wird zum neuen, leeren laufenden Fach.</summary>
    internal void NeueStunde()
    {
        _aktuellesFach = (_aktuellesFach + 1) % _verkaufteStueckProStunde.Length;
        _verkaufteStueckProStunde[_aktuellesFach] = 0;
        _minuteInStunde = 0;
    }

    /// <summary>Eine Spielminute ist vergangen (für die anteilige Gewichtung des ältesten Fachs).</summary>
    internal void NeueMinute() => _minuteInStunde = Math.Min(_minuteInStunde + 1, 59);

    internal MarktstandStand Sichern() => new()
    {
        VerkaufteStueckProStunde = (double[])_verkaufteStueckProStunde.Clone(),
        AktuellesFach = _aktuellesFach,
        MinuteInStunde = _minuteInStunde,
    };

    internal void Wiederherstellen(MarktstandStand s)
    {
        if (s.VerkaufteStueckProStunde.Length != _verkaufteStueckProStunde.Length)
            throw new InvalidDataException($"Marktstand {Ware}: falsche Anzahl Stundenfächer im Spielstand.");
        Array.Copy(s.VerkaufteStueckProStunde, _verkaufteStueckProStunde, _verkaufteStueckProStunde.Length);
        _aktuellesFach = s.AktuellesFach;
        _minuteInStunde = s.MinuteInStunde;
    }
}

/// <summary>Der Marktplatz der Stadt: ein Marktstand je Ware.</summary>
public sealed class Markt
{
    private readonly Dictionary<string, Marktstand> _staende = new();

    public Markt(Wirtschaftsdaten daten)
    {
        foreach (var (name, ware) in daten.Waren)
            _staende[name] = new Marktstand(name, ware, daten.Allgemein);
    }

    public Marktstand this[string ware] => _staende.TryGetValue(ware, out var stand)
        ? stand
        : throw new KeyNotFoundException($"Auf dem Markt gibt es keine Ware „{ware}“.");

    public IEnumerable<Marktstand> Staende => _staende.Values;

    internal void NeueStunde()
    {
        foreach (var stand in _staende.Values)
            stand.NeueStunde();
    }

    internal void NeueMinute()
    {
        foreach (var stand in _staende.Values)
            stand.NeueMinute();
    }
}
