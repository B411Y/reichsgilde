using Reichsgilde.Core.Speichern;
namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Ein Marktstand für eine Ware.
///
/// Preisformel (siehe Dokument „Wirtschaftsmodell", Abschnitt 3):
///   Angebot    = Nachfrage × NPC-Anteil + Spieler-Verkäufe des letzten Spieljahres (19 Spielstunden)
///   Faktor     = (Nachfrage / Angebot) ^ Elastizität, begrenzt auf [PreisMin … PreisMax]
///   Marktpreis = Grundpreis × Faktor
///
/// Weil ein Spieltag ein Spieljahr ist, sind „die letzten 19 Spielstunden" (04–23 Uhr) genau ein Jahr.
/// Die Verkäufe werden in 20 Stunden-Fächern gesammelt: dem laufenden Fach und den
/// 19 vorherigen. Vom ältesten Fach zählt nur der Teil, der noch in die letzten 19 Stunden
/// fällt. So umfasst das „gleitende Fenster" zu jeder Minute genau ein Spieljahr.
/// </summary>
public sealed class Marktstand
{
    private readonly WareDaten _ware;
    private readonly AllgemeineDaten _allgemein;
    private readonly Markt? _markt;
    private readonly double[] _verkaufteStueckProStunde = new double[Zeit.Spielzeit.StundenProTag + 1];
    private int _aktuellesFach;
    private int _minuteInStunde;

    public string Ware { get; }

    public Marktstand(string ware, WareDaten daten, AllgemeineDaten allgemein) : this(ware, daten, allgemein, null) { }

    internal Marktstand(string ware, WareDaten daten, AllgemeineDaten allgemein, Markt? markt)
    {
        Ware = ware;
        _ware = daten;
        _allgemein = allgemein;
        _markt = markt;
    }

    /// <summary>
    /// Nachfrage der Stadt nach ihren Einwohnern: die Daten mal <see cref="Markt.Einwohnerfaktor"/> (PR #177). Danach richtet sich
    /// auch die Grundversorgung – eine größere Stadt hat mehr Käufer und mehr Händler.
    /// </summary>
    public double Grundnachfrage => _ware.Nachfrage * (_markt?.Einwohnerfaktor ?? 1);

    /// <summary>Was Spieler und KI-Rivalen im letzten Spieljahr (19 Spielstunden) verkauft haben.</summary>
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
    public double Gesamtangebot => Grundnachfrage * _ware.NpcAnteil + SpielerAngebotImJahr;

    public double Preisfaktor => FaktorBei(0);

    /// <summary>
    /// Bürger-KI: Faktor auf die Nachfrage der Stadt (1 = wie in den Daten). Setzt die Einwohnerschaft jede Stunde
    /// nach den Einkäufen der Einwohner; die Grundversorgung (Nachfrage × NPC-Anteil) bleibt davon unberührt.
    /// </summary>
    public double NachfrageFaktor { get; internal set; } = 1;

    /// <summary>
    /// Nachfrage der Stadt im Jahr – <see cref="Grundnachfrage"/> mal <see cref="NachfrageFaktor"/> mal <see cref="Markt.Ratsfaktor"/>
    /// (größerer Marktplatz, Fest; PR #200).
    /// </summary>
    public double Nachfrage => Grundnachfrage * NachfrageFaktor * (_markt?.Ratsfaktor ?? 1);

    /// <summary>Art der Ware aus den Daten (Rohstoff, Zwischenprodukt, Fertigprodukt, Heilmittel).</summary>
    public string Typ => _ware.Typ;

    /// <summary>Preisfaktor, wenn das Angebot um <paramref name="zusatz"/> Stück anders wäre (negativ = gekauft).</summary>
    private double FaktorBei(double zusatz)
    {
        var faktor = Math.Pow(Nachfrage / Math.Max(Gesamtangebot + zusatz, 1e-9), _allgemein.Elastizitaet);
        return Math.Clamp(faktor, _allgemein.PreisMin, _allgemein.PreisMax);
    }

    /// <summary>Aktueller Marktpreis für ein Stück.</summary>
    public double Preis => _ware.Grundpreis * Preisfaktor;

    /// <summary>
    /// Der Preis zu Beginn der nächsten Stunde, wenn bis dahin niemand verkauft (Vorschau des Marktmeisters, PR #200): Dann fällt das
    /// älteste Stundenfach aus dem Fenster – was vor 19 Stunden verkauft wurde, drückt den Preis nicht mehr. Die Nachfrage bleibt wie jetzt.
    /// </summary>
    public double PreisNaechsteStunde
    {
        get
        {
            var aeltestes = (_aktuellesFach + 1) % _verkaufteStueckProStunde.Length;
            var angebot = Grundnachfrage * _ware.NpcAnteil + _verkaufteStueckProStunde.Sum() - _verkaufteStueckProStunde[aeltestes];
            var faktor = Math.Pow(Nachfrage / Math.Max(angebot, 1e-9), _allgemein.Elastizitaet);
            return _ware.Grundpreis * Math.Clamp(faktor, _allgemein.PreisMin, _allgemein.PreisMax);
        }
    }

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
    /// <param name="vorher">So viele Stück wären vorher schon gekauft (für eine Rechnung in Schritten, z. B. die Ladung eines Schiffs).</param>
    /// <param name="anteil">So viel jedes Stücks fehlt danach im Angebot (1 = ganz; die Hinfracht der Schiffe weniger, <see cref="SchiffshandelDaten.HeimatmarktAnteil"/>).</param>
    public double HandKaufpreis(int stueck, double faktor, int vorher = 0, double anteil = 1)
    {
        double summe = 0;
        for (var i = 0; i < stueck; i++)
            summe += _ware.Grundpreis * FaktorBei(-(vorher + i) * anteil) * faktor;
        return summe;
    }

    /// <summary>
    /// Was der Verkauf so vieler Stück jetzt brächte (vor Steuer und Verhandeln) – jedes Stück drückt den Preis für das nächste,
    /// genau wie <see cref="Verkaufen"/>, aber ohne zu verkaufen.
    /// </summary>
    /// <param name="vorher">So viele Stück wären vorher schon verkauft.</param>
    public double Verkaufswert(int stueck, int vorher = 0)
    {
        double summe = 0;
        for (var i = 0; i < stueck; i++)
            summe += _ware.Grundpreis * FaktorBei(vorher + i);
        return summe;
    }

    /// <summary>Kauft von Hand: wie <see cref="HandKaufpreis"/>, und das Angebot sinkt um die gekauften Stück (× <paramref name="anteil"/>).</summary>
    internal double HandKaufen(int stueck, double faktor, double anteil = 1)
    {
        var kosten = HandKaufpreis(stueck, faktor, anteil: anteil);
        _verkaufteStueckProStunde[_aktuellesFach] -= stueck * anteil;
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
            _staende[name] = new Marktstand(name, ware, daten.Allgemein, this);
    }

    /// <summary>
    /// Faktor der Einwohner auf Nachfrage und Grundversorgung aller Waren (1 = wie in den Daten). Die Stadtwirtschaft setzt ihn
    /// zu Beginn jeder Spielminute und nach dem Laden (<see cref="StadtDaten"/>).
    /// </summary>
    public double Einwohnerfaktor { get; internal set; } = 1;

    /// <summary>
    /// Faktor des Rates auf die Nachfrage aller Waren (größerer Marktplatz, Fest; 1 = keiner). Anders als der Einwohnerfaktor wirkt er nur
    /// auf die Käufer, nicht auf die Grundversorgung. Die Stadtwirtschaft setzt ihn mit dem Einwohnerfaktor.
    /// </summary>
    public double Ratsfaktor { get; internal set; } = 1;

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
