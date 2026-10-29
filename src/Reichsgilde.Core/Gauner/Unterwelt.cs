using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Gauner;

/// <summary>Ein Auftrag im Hinterzimmer – ausgeführt in der nächsten Nacht.</summary>
public sealed record Gaunerauftrag(Konto Auftraggeber, Konto Ziel, Gaunerstueck Art, string? Betrieb);

/// <summary>Wegelagerer auf der Lauer: Die nächste beladene Umlandfahrt des Ziels wird überfallen.</summary>
public sealed record Hinterhalt(Konto Auftraggeber, Konto Ziel, long BisMinute);

/// <summary>Ein Erpresserbrief: Schweigegeld gegen die Beweise des Erpressers.</summary>
public sealed record Forderung(Konto Erpresser, Konto Opfer, double Betrag, int Jahr);

/// <summary>
/// Was nachts geschah. <see cref="Fuer"/> = null: Die ganze Stadt weiß es (Einbruch, Brand, Überfall).
/// Sonst nur dieses Haus (z. B. der Auftraggeber erfährt, was der Hehler zahlt).
/// </summary>
public sealed record Nachtereignis(Konto? Fuer, string Text);

/// <summary>
/// Die Unterwelt der Stadt: Aufträge im Hinterzimmer, Hinterhalte im Umland, Erpresserbriefe.
///
/// Über das Jahr:
///   • Ein Haus gibt ein Gaunerstück gegen ein anderes in Auftrag (Befehl Beauftragen) und zahlt sofort.
///   • Mit genug Beweisen verlangt man Schweigegeld (Befehl Erpressen), das Opfer zahlt oder nicht.
///
/// Jede Nacht um 20:00 (Beginn der Dunklen Nacht):
///   • Diebstahl und Sabotage werden ausgeführt, Wegelagerer legen sich auf die Lauer.
///   • Jedes Gaunerstück hinterlässt eine Spur fürs Gericht. Mit etwas Glück (oder dank eines Wächters)
///     gibt es einen Zeugen – dann hat das Opfer sofort einen Beweis.
///
/// Alles Würfeln geschieht mit einem eigenen Zufallsgenerator – Wirtschaft, Familie und Gericht bleiben unberührt.
/// </summary>
public sealed class Unterwelt
{
    /// <summary>Spielminute im Tag, zu der die Gauner losziehen (20:00 = Beginn der Dunklen Nacht).</summary>
    public const int Nachtstunde = 20 * 60 - Spielzeit.TagesbeginnMinute;

    /// <summary>Volle Stunden der Nacht, zu denen eine Bande es versuchen kann: 20, 21, 22 und 23 Uhr (23 Uhr = Ende des Spieltags, vor der Jahresrechnung).</summary>
    public const int NachtstundenMoeglich = (Spielzeit.MinutenProTag - Nachtstunde) / 60 + 1;

    /// <summary>
    /// Der wievielte Versuch der Nacht zu dieser Minute im Tag fällig ist (0 = 20 Uhr …), sonst null. Die Stunde um 23 Uhr (Ende des
    /// Spieltags) fragt die Jahresrechnung mit <see cref="Spielzeit.MinutenProTag"/>.
    /// </summary>
    public int? VersuchUm(int minuteImTag)
    {
        if (minuteImTag < Nachtstunde || (minuteImTag - Nachtstunde) % 60 != 0) return null;
        var k = (minuteImTag - Nachtstunde) / 60;
        return k < (_d?.NachtVersuche ?? 1) ? k : null;
    }

    // Der laufende Versuch einer Nacht (Gaunerei je Nachtstunde): Faktor auf die Chance, letzter Versuch?, hatte dieser Auftrag schon Erfolg?
    private double _chanceFaktor = 1;
    private bool _letzterVersuch = true, _bisherErfolg, _versuchErfolg;
    private readonly List<bool> _erfolgreich = new(); // je Auftrag in _auftraege: schon ein Erfolg in dieser Nacht?

    private readonly Stadtwirtschaft _w;
    private readonly GaunerDaten? _d;
    private Zufallsquelle _zufall;
    private readonly List<Gaunerauftrag> _auftraege = new();
    private readonly List<Hinterhalt> _hinterhalte = new();
    private readonly List<Forderung> _forderungen = new();
    private List<Nachtereignis> _diesesJahr = new();
    private List<Nachtereignis> _letztesJahr = new();
    private readonly Dictionary<string, int> _letzterAngriff = new();

    public bool IstAktiv => _d is not null;
    public GaunerDaten Daten => _d ?? throw new InvalidOperationException("Es gibt keine Unterwelt (gauner fehlt in den Daten).");

    public IReadOnlyList<Gaunerauftrag> Auftraege => _auftraege;
    public IReadOnlyList<Hinterhalt> Hinterhalte => _hinterhalte;
    public IReadOnlyList<Forderung> Forderungen => _forderungen;
    public IReadOnlyList<Nachtereignis> DiesesJahr => _diesesJahr;
    public IReadOnlyList<Nachtereignis> LetztesJahr => _letztesJahr;

    /// <summary>Die Banden haben ihre Aufträge ausgeführt (für Oberflächen, die sich dann neu aufbauen).</summary>
    public event Action? NachtVorbei;

    /// <summary>Was dieses Haus über die Nächte weiß: alles Öffentliche und seine eigenen Berichte.</summary>
    public IEnumerable<string> NachrichtenFuer(Konto konto, bool letztesJahr) =>
        (letztesJahr ? _letztesJahr : _diesesJahr).Where(e => e.Fuer is null || e.Fuer == konto).Select(e => e.Text);

    internal Unterwelt(Stadtwirtschaft w, GaunerDaten? daten, int zufallsstartwert)
    {
        _w = w;
        _d = daten;
        _zufall = new Zufallsquelle(unchecked(zufallsstartwert * 29 + 11));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Rechnungen – dieselben, die die Hinterzimmer-Tafel zeigt und nach denen die KI entscheidet
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Die wertvollste Ware im Lager, höchstens so viel, wie die Bande tragen kann.</summary>
    public List<(string Ware, int Stueck, double Wert)> MoeglicheBeute(Betrieb b)
    {
        var rest = Daten.Diebstahl.HoechstensStueck;
        var beute = new List<(string, int, double)>();
        foreach (var (ware, stueck) in b.Lager.Where(l => l.Value > 0)
                     .OrderByDescending(l => _w.Markt[l.Key].Preis).ThenBy(l => l.Key, StringComparer.Ordinal))
        {
            if (rest <= 0) break;
            var n = Math.Min(rest, stueck);
            beute.Add((ware, n, n * _w.Markt[ware].Preis));
            rest -= n;
        }
        return beute;
    }

    /// <summary>
    /// Warenwert des 1., 2., … gelungenen Griffs einer Nacht (Gaunerei je Nachtstunde): Jeder holt die wertvollste übrige Ware,
    /// höchstens <paramref name="stueckJeMal"/> Stück – der zweite findet ein schon geplündertes Lager.
    /// </summary>
    internal List<double> Warenfolge(Betrieb b, int stueckJeMal, int griffe)
    {
        var lager = b.Lager.Where(l => l.Value > 0).OrderByDescending(l => _w.Markt[l.Key].Preis).ThenBy(l => l.Key, StringComparer.Ordinal)
            .Select(l => (Preis: _w.Markt[l.Key].Preis, Stueck: l.Value)).ToList();
        var folge = new List<double>();
        var j = 0;
        for (var g = 0; g < griffe; g++)
        {
            var (rest, wert) = (stueckJeMal, 0.0);
            while (rest > 0 && j < lager.Count)
            {
                var n = Math.Min(rest, lager[j].Stueck);
                wert += n * lager[j].Preis;
                rest -= n;
                lager[j] = (lager[j].Preis, lager[j].Stueck - n);
                if (lager[j].Stueck == 0) j++;
            }
            folge.Add(wert);
        }
        return folge;
    }

    /// <summary>Versuche je Nacht für Diebstahl, Sabotage, Krötenschleim, Einbruch und Brand (ohne Unterwelt einer).</summary>
    public int VersucheJeNacht => _d?.NachtVersuche ?? 1;

    /// <summary>
    /// Chance, dass ein Auftrag in einer Nacht mindestens <paramref name="k"/>-mal gelingt (Gaunerei je Nachtstunde):
    /// <see cref="VersucheJeNacht"/> Versuche zu je <paramref name="chance"/> × <see cref="GaunerDaten.VersuchFaktor"/>.
    /// Ohne Unterwelt ein Versuch mit der vollen Chance.
    /// </summary>
    public double ChanceMindestensErfolge(double chance, int k)
    {
        var n = VersucheJeNacht;
        if (k <= 0) return 1;
        if (k > n) return 0;
        var p = Math.Clamp(chance * (_d?.VersuchFaktor ?? 1), 0, 1);
        double summe = 0, binom = 1; // binom = (n über j), beginnend bei j = 0
        for (var j = 0; j <= n; j++)
        {
            if (j > 0) binom = binom * (n - j + 1) / j;
            if (j >= k) summe += binom * DetMathe.Pow(p, j) * DetMathe.Pow(1 - p, n - j);
        }
        return summe;
    }

    /// <summary>
    /// Erwarteter Ertrag eines Auftrags über die ganze Nacht: Σ über k (Chance auf mindestens k Erfolge × Ertrag des k-ten Erfolgs).
    /// <paramref name="ertrag"/> bekommt k = 1, 2, … – so leeren sich Lager und Truhe von Griff zu Griff.
    /// </summary>
    public double ErwartetJeNacht(double chance, Func<int, double> ertrag)
    {
        double summe = 0;
        for (var k = 1; k <= VersucheJeNacht; k++) summe += ChanceMindestensErfolge(chance, k) * ertrag(k);
        return summe;
    }

    /// <summary>
    /// Erfolgschance eines Gaunerstücks gegen diesen Betrieb. Wächter halbieren sie; das Talent
    /// „Bei Nacht und Nebel" des Auftraggebers hebt sie (Überfall: sein Kampf stärkt die Wegelagerer).
    /// </summary>
    public double Erfolgschance(Gaunerstueck art, Betrieb? b, Konto? auftraggeber = null)
    {
        var d = Daten;
        var schutz = b?.Waechter == true ? d.WaechterSchutz : 1;
        var nacht = auftraggeber is null ? 0 : _w.Familien.NachtBonus(auftraggeber);
        return art switch
        {
            // Die Streife der Wache macht nächtliche Taten in der Stadt schwerer.
            // Eisentür bzw. Löschfass der Werkstatt machen es zusätzlich schwerer.
            Gaunerstueck.Diebstahl => Math.Min(0.95, d.Diebstahl.Chance * schutz + nacht) * _w.Wache.Patrouillefaktor(b) * (b?.EinbruchFaktor ?? 1),
            Gaunerstueck.Sabotage => Math.Min(0.95, d.Sabotage.Chance * schutz + nacht) * _w.Wache.Patrouillefaktor(b) * (b?.BrandFaktor ?? 1),
            // Wächter schützen Betriebe, nicht die Tafel; ein Alchimist mischt selbst – sicherer
            Gaunerstueck.Kroetenschleim => Math.Min(0.95, d.Kroetenschleim.Chance + nacht + (MischtSelbst(auftraggeber) ? d.Kroetenschleim.BerufBonus : 0)),
            _ => Staerke(auftraggeber), // vor der Eskorte des jeweiligen Karrens
        };
    }

    /// <summary>Beherrscht das Haus das Handwerk, mit dem man Krötenschleim selbst mischt (Alchimist)?</summary>
    public bool MischtSelbst(Konto? auftraggeber) =>
        auftraggeber is not null && Daten.Kroetenschleim.Beruf != "" && auftraggeber.Beherrscht(Daten.Kroetenschleim.Beruf);

    /// <summary>Was ein Gaunerstück dieses Haus kostet – Alchimisten zahlen für Krötenschleim weniger.</summary>
    public double Kosten(Gaunerstueck art, Konto? auftraggeber) =>
        Daten.Kosten(art) * (art == Gaunerstueck.Kroetenschleim && MischtSelbst(auftraggeber) ? Daten.Kroetenschleim.BerufKostenFaktor : 1);

    /// <summary>Stärke angeheuerter Wegelagerer – der Kampf des Auftraggebers macht sie gefährlicher.</summary>
    public double Staerke(Konto? auftraggeber) =>
        Math.Min(1, Daten.Ueberfall.Staerke + (auftraggeber is null ? 0 : _w.Familien.KampfUeberfall(auftraggeber)));

    /// <summary>
    /// Was ein Diebstahl in einer Nacht im Mittel einbringt: über alle Versuche (je Erfolg Hehlerpreis der übrigen Ware + Geld aus der Kasse
    /// eines Geldleihers) − Kosten.
    /// </summary>
    public double ErwarteterGewinn(Betrieb b, Konto? auftraggeber = null)
    {
        var waren = Warenfolge(b, Daten.Diebstahl.HoechstensStueck, VersucheJeNacht);
        return ErwartetJeNacht(Erfolgschance(Gaunerstueck.Diebstahl, b, auftraggeber),
                   k => waren[k - 1] * Daten.HehlerAnteil + (_w.Bank.IstAktiv ? _w.Bank.Kassenbeute(b, k) : 0))
               - Daten.Diebstahl.Kosten;
    }

    /// <summary>„3 Dolch und 2 Silberring (Wert 412)“, „120 Gulden aus der Kasse“ oder beides – für die öffentliche Meldung.</summary>
    internal string Diebesgut(IReadOnlyCollection<(string Ware, int Stueck, double Wert)> beute, double kasse)
    {
        var teile = new List<string>();
        if (beute.Count > 0)
            teile.Add($"{string.Join(" und ", beute.Select(x => $"{x.Stueck} {x.Ware}"))} (Wert {Zahlenformat.Ganz(beute.Sum(x => x.Wert))})");
        if (kasse > 0) teile.Add($"{Zahlenformat.Ganz(kasse)} {_w.Daten.Allgemein.Waehrung} aus der Kasse");
        return string.Join(" und ", teile);
    }

    /// <summary>
    /// Was eine Sabotage dem Opfer in einer Nacht im Mittel kostet: entgangene Arbeit + Reparatur × Chance, dass einer der Versuche gelingt
    /// (sie wirkt höchstens einmal je Nacht).
    /// </summary>
    public double ErwarteterSchaden(Betrieb b, Konto? auftraggeber = null) =>
        ChanceMindestensErfolge(Erfolgschance(Gaunerstueck.Sabotage, b, auftraggeber), 1) * SabotageSchaden(b);

    /// <summary>Schaden einer gelungenen Sabotage (für die Anzeige).</summary>
    public double Sabotageschaden(Betrieb b) => SabotageSchaden(b);

    private double SabotageSchaden(Betrieb b) =>
        Daten.Sabotage.Reparatur + Daten.Sabotage.StillstandMinuten * Math.Max(0, Kalkulation.WertJeArbeitsminute(_w, b));

    // ── Gegen das Wohnhaus ───────────────────────────────────────────────────

    /// <summary>Gibt es Gaunerstücke gegen Wohnhäuser? (Abschnitt gauner.wohnhaus und Wohnhäuser in der Stadt.)</summary>
    public bool GegenWohnhaus => _d?.Wohnhaus is not null && _w.Wohnen.IstAktiv;

    /// <summary>
    /// Erfolgschance von Einbruch oder Brand am Wohnhaus dieses Hauses. Ein Hauswächter halbiert sie, die Streife
    /// der Wache wirkt wie bei Betrieben in der Stadt, „Bei Nacht und Nebel“ des Auftraggebers hebt sie.
    /// </summary>
    public double ErfolgschanceWohnhaus(Gaunerstueck art, Konto ziel, Konto? auftraggeber = null)
    {
        var wh = Daten.Wohnhaus ?? throw new InvalidOperationException("Es gibt keine Gaunerstücke gegen Wohnhäuser.");
        var grund = art == Gaunerstueck.Brandstiftung ? wh.Brand.Chance : wh.Einbruch.Chance;
        var schutz = (ziel.Hauswaechter ? Daten.WaechterSchutz : 1)
                     * (art == Gaunerstueck.Brandstiftung ? _w.Wohnen.BrandFaktor(ziel) : _w.Wohnen.EinbruchFaktor(ziel)); // Riegel, Hund (PR #224)
        var nacht = auftraggeber is null ? 0 : _w.Familien.NachtBonus(auftraggeber);
        return Math.Min(0.95, grund * schutz + nacht) * _w.Wache.Patrouillefaktor(null);
    }

    /// <summary>Was ein Einbruch ins Wohnhaus holt: ein Anteil vom Bargeld, höchstens <c>hoechstensJeStufe</c> × (Stufe + 1).</summary>
    public double Hausbeute(Konto ziel) => Hausbeute(ziel, ziel.Vermoegen);

    private double Hausbeute(Konto ziel, double vermoegen)
    {
        var e = Daten.Wohnhaus!.Einbruch;
        return Math.Min(e.Anteil * Math.Max(0, vermoegen), e.HoechstensJeStufe * (ziel.WohnhausStufe + 1));
    }

    /// <summary>Reparatur nach einem Brand am Wohnhaus (Anteil vom Ausbaupreis der Stufe, mindestens <c>mindestReparatur</c>).</summary>
    public double Brandschaden(Konto ziel)
    {
        var b = Daten.Wohnhaus!.Brand;
        return Math.Max(b.MindestReparatur, b.ReparaturAnteil * (_w.Wohnen.Stufe(ziel)?.Ausbau ?? 0));
    }

    /// <summary>
    /// Was ein Einbruch ins Wohnhaus in einer Nacht im Mittel einbringt: über alle Versuche (je Erfolg die Beute aus der übrigen Truhe)
    /// − Kosten (Bargeld braucht keinen Hehler).
    /// </summary>
    public double ErwarteterHausgewinn(Konto ziel, Konto? auftraggeber = null)
    {
        var beute = new List<double>();
        var vermoegen = ziel.Vermoegen;
        for (var k = 0; k < VersucheJeNacht; k++)
        {
            beute.Add(Hausbeute(ziel, vermoegen));
            vermoegen -= beute[^1];
        }
        return ErwartetJeNacht(ErfolgschanceWohnhaus(Gaunerstueck.Hauseinbruch, ziel, auftraggeber), k => beute[k - 1])
               - Kosten(Gaunerstueck.Hauseinbruch, auftraggeber);
    }

    /// <summary>Was ein Brand am Wohnhaus das Opfer in einer Nacht im Mittel kostet: Es brennt höchstens einmal (danach ist das Haus beschädigt).</summary>
    public double ErwarteterBrandschaden(Konto ziel, Konto? auftraggeber = null) =>
        ChanceMindestensErfolge(ErfolgschanceWohnhaus(Gaunerstueck.Brandstiftung, ziel, auftraggeber), 1) * Brandschaden(ziel);

    /// <summary>Unter diesem Namen merken sich Gericht und Stadt Taten gegen das Wohnhaus eines Hauses.</summary>
    public static string WohnhausVon(Konto k) => $"Wohnhaus von {k.Inhaber}";

    /// <summary>In welchem Jahr das Wohnhaus dieses Hauses zuletzt angegriffen wurde (öffentlich).</summary>
    public int? LetzterAngriffWohnhaus(Konto k) => LetzterAngriff(WohnhausVon(k));

    /// <summary>Zeugenchance am Wohnhaus – wie im Betrieb, mit Hauswächter statt Betriebswächter.</summary>
    public double ZeugenchanceWohnhaus(Konto ziel, bool erfolgreich, Konto? auftraggeber = null)
    {
        var d = Daten;
        var chance = (ziel.Hauswaechter ? d.ZeugenChanceWaechter : d.ZeugenChance) + _w.Wache.ZeugenBonusInDerStadt;
        if (auftraggeber is not null) chance *= _w.Familien.ZeugenFaktor(auftraggeber);
        return Math.Min(1, erfolgreich ? chance : chance * d.ZeugenFaktorMisserfolg);
    }

    /// <summary>Betriebe im Umland, deren beladene Karren man überfallen könnte.</summary>
    public bool HatUmlandfahrten(Konto ziel) => _w.Betriebe.Any(b => b.Konto == ziel && b.Ort == Arbeitsort.Umland && b.Karren.Count > 0);

    /// <summary>Chance auf einen Zeugen – Wächter sehen mehr, wer „Bei Nacht und Nebel" kann, wird seltener gesehen.</summary>
    public double Zeugenchance(Betrieb? b, bool erfolgreich, Konto? auftraggeber = null)
    {
        var d = Daten;
        var chance = (b?.Waechter == true ? d.ZeugenChanceWaechter : d.ZeugenChance) + _w.Wache.ZeugenBonus(b);
        if (auftraggeber is not null) chance *= _w.Familien.ZeugenFaktor(auftraggeber);
        return Math.Min(1, erfolgreich ? chance : chance * d.ZeugenFaktorMisserfolg);
    }

    /// <summary>In welchem Jahr dieser Betrieb zuletzt angegriffen wurde (öffentlich – die ganze Stadt redet davon).</summary>
    public int? LetzterAngriff(string betrieb) => _letzterAngriff.TryGetValue(betrieb, out var jahr) ? jahr : null;

    public Forderung? ForderungAn(Konto opfer, Konto erpresser) => _forderungen.FirstOrDefault(f => f.Opfer == opfer && f.Erpresser == erpresser);

    // ═════════════════════════════════════════════════════════════════════════
    //  Befehle (über Stadtwirtschaft.Ausfuehren)
    // ═════════════════════════════════════════════════════════════════════════

    internal Befehlsergebnis Beauftragen(Konto auftraggeber, Konto ziel, Gaunerstueck art, string? betriebName)
    {
        var d = Daten;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        if (ziel == auftraggeber) return Befehlsergebnis.Abgelehnt("Gegen das eigene Haus heuert man keine Bande an.");
        if (!ziel.IstAktiv) return Befehlsergebnis.Abgelehnt($"{ziel.Inhaber} ist nicht mehr im Spiel.");
        if (_auftraege.Any(a => a.Auftraggeber == auftraggeber && a.Ziel == ziel && a.Art == art))
            return Befehlsergebnis.Abgelehnt($"Für heute Nacht ist gegen {ziel.Inhaber} schon ein {art.Name()} bestellt.");

        if (art == Gaunerstueck.Kroetenschleim)
        {
            if (!_w.Gesundheit.IstAktiv || _w.Familien.Von(ziel) is null)
                return Befehlsergebnis.Abgelehnt("Krötenschleim braucht ein Oberhaupt, das krank werden kann.");
            betriebName = null;
        }
        else if (art is Gaunerstueck.Hauseinbruch or Gaunerstueck.Brandstiftung)
        {
            if (!GegenWohnhaus)
                return Befehlsergebnis.Abgelehnt("In dieser Stadt nimmt keine Bande Aufträge gegen Wohnhäuser an.");
            if (art == Gaunerstueck.Brandstiftung && _w.Wohnen.IstBeschaedigt(ziel))
                return Befehlsergebnis.Abgelehnt($"Das Wohnhaus von {ziel.Inhaber} ist vom letzten Brand noch nicht instand gesetzt.");
            betriebName = null;
        }
        else if (art == Gaunerstueck.Ueberfall)
        {
            if (!HatUmlandfahrten(ziel))
                return Befehlsergebnis.Abgelehnt($"{ziel.Inhaber} hat keine Karren auf Umlandwegen – in der Stadt lauert keine Bande.");
            if (_hinterhalte.Any(h => h.Auftraggeber == auftraggeber && h.Ziel == ziel))
                return Befehlsergebnis.Abgelehnt($"Eure Wegelagerer liegen schon auf der Lauer gegen {ziel.Inhaber}.");
            betriebName = null;
        }
        else
        {
            var betrieb = _w.Betriebe.FirstOrDefault(b => b.Name == betriebName);
            if (betrieb is null || betrieb.Konto != ziel)
                return Befehlsergebnis.Abgelehnt($"{ziel.Inhaber} hat keinen Betrieb „{betriebName}“.");
        }

        var kosten = Kosten(art, auftraggeber);
        if (auftraggeber.Vermoegen < kosten)
            return Befehlsergebnis.Abgelehnt($"Die Bande will {Zahlenformat.Ganz(kosten)} {waehrung} im Voraus.");
        auftraggeber.Gaunerkosten(kosten);
        _auftraege.Add(new Gaunerauftrag(auftraggeber, ziel, art, betriebName));
        return Befehlsergebnis.Ok($"Die Bande nimmt das Geld ({Zahlenformat.Ganz(kosten)} {waehrung}). " +
                                  (art == Gaunerstueck.Ueberfall ? $"Heute Nacht legen sich Wegelagerer gegen {ziel.Inhaber} auf die Lauer."
                                      : art == Gaunerstueck.Kroetenschleim ? $"Heute Nacht gelangt Krötenschleim in die Küche von {ziel.Inhaber}."
                                      : art == Gaunerstueck.Hauseinbruch ? $"Heute Nacht um 20 Uhr steigt die Bande ins Wohnhaus von {ziel.Inhaber} ein."
                                      : art == Gaunerstueck.Brandstiftung ? $"Heute Nacht um 20 Uhr brennt es am Wohnhaus von {ziel.Inhaber}."
                                      : $"{art.Name()} in {betriebName} – heute Nacht um 20 Uhr."));
    }

    internal Befehlsergebnis Erpressen(Konto erpresser, Konto opfer, double betrag)
    {
        var d = Daten;
        if (!_w.Justiz.IstAktiv) return Befehlsergebnis.Abgelehnt("Ohne Gericht schreckt keine Drohung mit Beweisen.");
        if (opfer == erpresser || !opfer.IstAktiv) return Befehlsergebnis.Abgelehnt("Dieses Haus lässt sich nicht erpressen.");
        if (betrag <= 0 || double.IsNaN(betrag)) return Befehlsergebnis.Abgelehnt("Wie viel Schweigegeld soll es denn sein?");
        var beweise = _w.Justiz.Beweise(erpresser, opfer).Count();
        if (beweise < d.ErpressungMindestBeweise)
            return Befehlsergebnis.Abgelehnt($"Mit {beweise} {(beweise == 1 ? "Beweis" : "Beweisen")} lacht {opfer.Inhaber} euch aus – es braucht mindestens {d.ErpressungMindestBeweise}.");
        if (ForderungAn(opfer, erpresser) is not null)
            return Befehlsergebnis.Abgelehnt($"{opfer.Inhaber} hat euren Brief schon – wartet die Antwort ab.");
        if (_w.Justiz.VerfahrenGegen(erpresser, opfer) is not null)
            return Befehlsergebnis.Abgelehnt($"Die Anklage gegen {opfer.Inhaber} läuft schon – zum Erpressen ist es zu spät.");

        _forderungen.Add(new Forderung(erpresser, opfer, betrag, _w.Jahr));
        // Der Brief selbst ist ein Beweis – das Opfer weiß, wer schreibt.
        if (_w.Justiz.TatMelden(erpresser, Vergehen.Erpressung, opfer.Inhaber, betrag) is { } spur)
            _w.Justiz.BeweisGeben(opfer, spur);
        _w.Melden($"{opfer.Inhaber} erhält einen Erpresserbrief von {erpresser.Inhaber}: " +
                  $"{Zahlenformat.Ganz(betrag)} {_w.Daten.Allgemein.Waehrung} Schweigegeld, sonst kommt es vor Gericht.");
        return Befehlsergebnis.Ok($"Der Brief ist zugestellt. {opfer.Inhaber} hat bis zum Jahresende Zeit. Aber Vorsicht: Der Brief ist selbst ein Beweis gegen euch.");
    }

    internal Befehlsergebnis ErpressungBeantworten(Konto opfer, Konto erpresser, bool zahlen)
    {
        if (ForderungAn(opfer, erpresser) is not { } f)
            return Befehlsergebnis.Abgelehnt($"{erpresser.Inhaber} hat euch nicht erpresst.");
        var waehrung = _w.Daten.Allgemein.Waehrung;
        if (zahlen)
        {
            if (opfer.Vermoegen < f.Betrag)
                return Befehlsergebnis.Abgelehnt($"So viel Geld hat {opfer.Inhaber} nicht.");
            opfer.Gaunerkosten(f.Betrag);
            erpresser.Gaunerkosten(-f.Betrag);
            _w.Justiz.BeweiseVerbrennen(erpresser, opfer);
            _forderungen.Remove(f);
            Ereignis(null, $"{opfer.Inhaber} zahlt {erpresser.Inhaber} Schweigegeld – die Beweise gehen ins Feuer.");
            return Befehlsergebnis.Ok($"Ihr zahlt {Zahlenformat.Ganz(f.Betrag)} {waehrung}. {erpresser.Inhaber} verbrennt seine Beweise gegen euch.");
        }
        _forderungen.Remove(f);
        Ereignis(null, $"{opfer.Inhaber} weist den Erpresserbrief von {erpresser.Inhaber} zurück.");
        return Befehlsergebnis.Ok($"Ihr zahlt nicht. Nun kann {erpresser.Inhaber} klagen – und ihr habt seinen Brief als Beweis gegen ihn.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Nacht und Jahresende
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Um 20:00: alle Aufträge ausführen, abgelaufene Hinterhalte auflösen.</summary>
    /// <param name="versuch">Der wievielte Versuch der Nacht (0 = 20 Uhr, bis <see cref="GaunerDaten.NachtVersuche"/> − 1).</param>
    internal void Nacht(Spielzeit zeit, int versuch = 0)
    {
        if (!IstAktiv) return;
        var d = Daten;
        var letzter = versuch >= d.NachtVersuche - 1;
        while (_erfolgreich.Count < _auftraege.Count) _erfolgreich.Add(false); // Aufträge, die erst im Lauf der Nacht kamen
        for (var i = 0; i < _auftraege.Count; i++)
        {
            var a = _auftraege[i];
            var erledigt = true; // Aufträge, die nur einmal wirken, sind danach vorbei
            if (!a.Auftraggeber.IstAktiv || !a.Ziel.IstAktiv) { }
            else if (a.Betrieb is { } name && _w.Betriebe.FirstOrDefault(b => b.Name == name)?.Konto != a.Ziel)
            {
                // Inzwischen verkauft: Die Bande behält ihr Geld, rührt aber den neuen Besitzer nicht an.
                Ereignis(a.Auftraggeber, $"{name} gehört nicht mehr {a.Ziel.Inhaber} – eure Bande zieht unverrichteter Dinge ab.");
            }
            else if (a.Art == Gaunerstueck.Ueberfall)
            {
                _hinterhalte.Add(new Hinterhalt(a.Auftraggeber, a.Ziel, zeit.VergangeneMinuten + d.Ueberfall.GueltigMinuten));
                Ereignis(a.Auftraggeber, $"Eure Wegelagerer legen sich gegen {a.Ziel.Inhaber} auf die Lauer – bis morgen Nacht.");
            }
            else
            {
                // Gaunerei je Nachtstunde: jeder Versuch mit einem Teil der Chance; eine Spur je Erfolg, ohne jeden Erfolg eine für den Versuch
                _chanceFaktor = d.VersuchFaktor;
                _letzterVersuch = letzter;
                _bisherErfolg = _erfolgreich[i];
                _versuchErfolg = false;
                switch (a.Art)
                {
                    case Gaunerstueck.Diebstahl: Stehlen(a); break;
                    case Gaunerstueck.Sabotage: Sabotieren(a); break;
                    case Gaunerstueck.Kroetenschleim: Vergiften(a); break;
                    case Gaunerstueck.Hauseinbruch: InsHausEinbrechen(a); break;
                    case Gaunerstueck.Brandstiftung: HausAnzuenden(a, zeit); break;
                }
                _erfolgreich[i] |= _versuchErfolg;
                (_chanceFaktor, _letzterVersuch) = (1, true);
                // Eine Sabotage wirkt höchstens einmal je Nacht (wie der Brand am Wohnhaus): Die Werkstatt brennt schon (PR #219).
                erledigt = letzter || (a.Art == Gaunerstueck.Sabotage && _erfolgreich[i]);
            }
            if (erledigt)
            {
                _auftraege.RemoveAt(i);
                _erfolgreich.RemoveAt(i);
                i--;
            }
        }
        if (!letzter) return;
        _auftraege.Clear();
        _erfolgreich.Clear();

        foreach (var h in _hinterhalte.Where(h => h.BisMinute < zeit.VergangeneMinuten).ToList())
        {
            _hinterhalte.Remove(h);
            Ereignis(h.Auftraggeber, $"Eure Wegelagerer haben vergeblich auf einen Karren von {h.Ziel.Inhaber} gewartet und ziehen ab.");
        }
        NachtVorbei?.Invoke();
    }

    private void Stehlen(Gaunerauftrag a)
    {
        var d = Daten;
        var b = _w.Betriebe.First(x => x.Name == a.Betrieb);
        var erfolg = _zufall.NextDouble() < Erfolgschance(Gaunerstueck.Diebstahl, b, a.Auftraggeber) * _chanceFaktor;
        double wert = 0;
        string oeffentlich;
        if (erfolg)
        {
            var beute = MoeglicheBeute(b);
            foreach (var (ware, stueck, _) in beute)
                b.Auslagern(ware, stueck);
            var waren = beute.Sum(x => x.Wert);
            // Beim Geldleiher liegt Geld in der Kasse – Bargeld braucht keinen Hehler, der Auftraggeber bekommt es ganz.
            var kasse = _w.Bank.IstAktiv ? _w.Bank.KasseBestehlen(b) : 0;
            wert = waren + kasse;
            if (wert > 0)
            {
                b.RaubBuchen(wert);
                a.Auftraggeber.Gaunerkosten(-(waren * d.HehlerAnteil + kasse));
                oeffentlich = $"Einbruch in {b.Name}: {Diebesgut(beute, kasse)} gestohlen.";
                Ereignis(a.Auftraggeber, kasse > 0 && waren <= 0
                    ? $"Eure Diebe haben aus der Kasse von {b.Name} {Zahlenformat.Ganz(kasse)} {_w.Daten.Allgemein.Waehrung} geholt."
                    : $"Eure Diebe haben in {b.Name} Ware für {Zahlenformat.Ganz(waren)} erbeutet – der Hehler zahlt {Zahlenformat.Ganz(waren * d.HehlerAnteil)}" +
                      (kasse > 0 ? $", dazu {Zahlenformat.Ganz(kasse)} {_w.Daten.Allgemein.Waehrung} aus der Kasse." : "."));
            }
            else
                oeffentlich = $"Einbruch in {b.Name} – doch das Lager war leer.";
        }
        else
            oeffentlich = $"Einbruchsversuch in {b.Name} – die Diebe wurden verscheucht{(b.Waechter ? " (der Wächter war wachsam)" : "")}.";
        Spur(a, Vergehen.Diebstahl, b.Name, wert, b, erfolg, oeffentlich);
    }

    private void Sabotieren(Gaunerauftrag a)
    {
        var d = Daten;
        var b = _w.Betriebe.First(x => x.Name == a.Betrieb);
        var erfolg = _zufall.NextDouble() < Erfolgschance(Gaunerstueck.Sabotage, b, a.Auftraggeber) * _chanceFaktor;
        double schaden = 0;
        string oeffentlich;
        if (erfolg)
        {
            schaden = SabotageSchaden(b);
            b.BrandBuchen(schaden);
            b.Sabotieren(d.Sabotage.StillstandMinuten);
            b.SchadenBuchen(d.Sabotage.Reparatur);
            oeffentlich = $"Anschlag auf {b.Name}: Feuer in der Werkstatt! {d.Sabotage.StillstandMinuten / 60} Arbeitsstunden Stillstand, " +
                          $"Reparatur {Zahlenformat.Ganz(d.Sabotage.Reparatur)}.";
        }
        else
            oeffentlich = $"Anschlag auf {b.Name} vereitelt{(b.Waechter ? " – der Wächter hat das Feuer bemerkt" : "")}.";
        Spur(a, Vergehen.Sabotage, b.Name, schaden, b, erfolg, oeffentlich);
    }

    private void InsHausEinbrechen(Gaunerauftrag a)
    {
        if (!GegenWohnhaus) return;
        var ziel = a.Ziel;
        var haus = _w.Wohnen.Stufe(ziel)!.Name;
        var erfolg = _zufall.NextDouble() < ErfolgschanceWohnhaus(Gaunerstueck.Hauseinbruch, ziel, a.Auftraggeber) * _chanceFaktor;
        double beute = 0;
        string oeffentlich;
        if (erfolg && (beute = Hausbeute(ziel)) > 0)
        {
            ziel.Schaden(beute);
            a.Auftraggeber.Gaunerkosten(-beute);
            oeffentlich = $"Einbruch ins {haus} von {ziel.Inhaber}: {Zahlenformat.Ganz(beute)} {_w.Daten.Allgemein.Waehrung} aus der Truhe gestohlen.";
            _w.Familien.ZugAnlass(ziel, "einbruch"); // das Oberhaupt stellt sich den Dieben – vielleicht eine Narbe (PR #227)
            Ereignis(a.Auftraggeber, $"Eure Leute haben im Wohnhaus von {ziel.Inhaber} {Zahlenformat.Ganz(beute)} erbeutet – Bargeld, kein Hehler nötig.");
        }
        else if (erfolg)
            oeffentlich = $"Einbruch ins {haus} von {ziel.Inhaber} – doch die Truhe war leer.";
        else
            oeffentlich = $"Einbruchsversuch am {haus} von {ziel.Inhaber} – die Diebe wurden verscheucht{(ziel.Hauswaechter ? " (der Hauswächter war wachsam)" : "")}.";
        Spur(a, Vergehen.Diebstahl, WohnhausVon(ziel), beute, null, erfolg, oeffentlich, wohnhaus: true);
    }

    private void HausAnzuenden(Gaunerauftrag a, Spielzeit zeit)
    {
        if (!GegenWohnhaus) return;
        var ziel = a.Ziel;
        var stufe = _w.Wohnen.Stufe(ziel)!;
        var brand = Daten.Wohnhaus!.Brand;
        var erfolg = !_w.Wohnen.IstBeschaedigt(ziel) && _zufall.NextDouble() < ErfolgschanceWohnhaus(Gaunerstueck.Brandstiftung, ziel, a.Auftraggeber) * _chanceFaktor;
        double schaden = 0;
        string oeffentlich;
        if (erfolg)
        {
            schaden = Brandschaden(ziel);
            ziel.Schaden(schaden);
            _w.Wohnen.Brand(ziel, zeit.VergangeneMinuten + brand.InstandMinuten);
            _w.Familien.ZugAnlass(ziel, "brand"); // Brandnarbe (PR #227)
            oeffentlich = $"Feuer am {stufe.Name} von {ziel.Inhaber}! Reparatur {Zahlenformat.Ganz(schaden)} {_w.Daten.Allgemein.Waehrung}" +
                          (stufe.Ansehen > 0 ? $", bis zur Instandsetzung fehlt das Ansehen des Hauses (−{Zahlenformat.Ganz(stufe.Ansehen)})." : ".");
        }
        else
            oeffentlich = $"Brandanschlag auf das {stufe.Name} von {ziel.Inhaber} vereitelt{(ziel.Hauswaechter ? " – der Hauswächter hat das Feuer bemerkt" : "")}.";
        Spur(a, Vergehen.Sabotage, WohnhausVon(ziel), schaden, null, erfolg, oeffentlich, wohnhaus: true);
    }

    private void Vergiften(Gaunerauftrag a)
    {
        if (_w.Familien.Von(a.Ziel) is not { } f) return;
        var o = f.Oberhaupt;
        var erfolg = _zufall.NextDouble() < Erfolgschance(Gaunerstueck.Kroetenschleim, null, a.Auftraggeber) * _chanceFaktor;
        string oeffentlich;
        if (erfolg && o.Krankheit is null && _w.Gesundheit.KroetenschleimKrankheit() is { } krankheit
            && _w.Gesundheit.Erkranken(a.Ziel, o, krankheit))
        {
            oeffentlich = $"Gift im Haus {f.Nachname}: Jemand hat {o.Vorname} Krötenschleim ins Essen gemischt – {o.Krankheit!.Beschreibung}.";
            Ereignis(a.Auftraggeber, $"Der Krötenschleim wirkt: {o.Name} liegt mit {krankheit} darnieder.");
        }
        else if (erfolg)
            oeffentlich = $"Krötenschleim im Essen von {o.Name} – doch {o.Vorname} ist ohnehin krank und merkt nichts.";
        else
            oeffentlich = $"Im Haus {f.Nachname} wurde ein Fremder in der Küche ertappt – das Essen blieb unberührt.";
        // Bußgrundlage: was ein Medicus gekostet hätte
        Spur(a, Vergehen.Giftmischerei, a.Ziel.Inhaber, _w.Gesundheit.IstAktiv ? _w.Gesundheit.Daten.MedicusKosten : 0, null, erfolg, oeffentlich);
    }

    /// <summary>
    /// Wird beim Losfahren eines beladenen Umlandkarrens gefragt. Liegt ein Hinterhalt gegen sein Haus,
    /// greifen die angeheuerten Wegelagerer an – statt der zufälligen Banden.
    /// </summary>
    /// <returns>true, wenn ein Hinterhalt zuschlug (dann entfällt der gewöhnliche Überfall-Wurf).</returns>
    internal bool HinterhaltAusloesen(Karren karren, EskorteDaten eskorte, Spielzeit zeit)
    {
        if (!IstAktiv) return false;
        var h = _hinterhalte.FirstOrDefault(x => x.Ziel == karren.Heimat.Konto && x.BisMinute >= zeit.VergangeneMinuten && x.Auftraggeber.IstAktiv);
        if (h is null) return false;
        _hinterhalte.Remove(h);

        var d = Daten;
        var schutz = Math.Min(0.95, eskorte.Schutz + _w.Familien.KampfSchutz(karren.Heimat.Konto));
        var erfolg = _zufall.NextDouble() < Staerke(h.Auftraggeber) * (1 - schutz) * _w.Wache.UeberfallFaktor(eskorte);
        double wert = 0;
        string oeffentlich;
        if (erfolg)
        {
            var verloren = karren.LadungVerlieren();
            wert = verloren.Sum(v => _w.Markt[v.Key].Preis * v.Value);
            karren.Heimat.Konto.Raub(wert);
            h.Auftraggeber.Gaunerkosten(-wert * d.HehlerAnteil);
            oeffentlich = $"Hinterhalt auf {karren.Name} von {karren.Heimat.Name}: Wegelagerer erbeuten {string.Join(" und ", verloren.Select(v => $"{v.Value} {v.Key}"))} " +
                          $"(Wert {Zahlenformat.Ganz(wert)}).";
            Ereignis(h.Auftraggeber, $"Eure Wegelagerer haben {karren.Heimat.Name} um Ware für {Zahlenformat.Ganz(wert)} erleichtert – der Hehler zahlt {Zahlenformat.Ganz(wert * d.HehlerAnteil)}.");
        }
        else
            oeffentlich = $"Hinterhalt auf {karren.Name} von {karren.Heimat.Name} – die Eskorte schlägt die Wegelagerer in die Flucht.";
        Spur(new Gaunerauftrag(h.Auftraggeber, h.Ziel, Gaunerstueck.Ueberfall, karren.Heimat.Name), Vergehen.Raub, karren.Heimat.Name, wert, null, erfolg, oeffentlich);
        return true;
    }

    /// <summary>Spur fürs Gericht, öffentliche Nachricht – und vielleicht ein Zeuge, der den Auftraggeber erkannt hat.</summary>
    private void Spur(Gaunerauftrag a, Vergehen vergehen, string betroffener, double betrag, Betrieb? b, bool erfolg, string oeffentlich, bool wohnhaus = false)
    {
        _versuchErfolg = erfolg;
        // Gaunerei je Nachtstunde: Ein misslungener Versuch fällt nur auf, wenn die ganze Nacht nichts gelang (dann eine Spur, im letzten Versuch).
        if (!erfolg && !(_letzterVersuch && !_bisherErfolg)) return;
        _letzterAngriff[betroffener] = _w.Jahr;
        var spur = _w.Justiz.TatMelden(a.Auftraggeber, vergehen, betroffener, betrag);
        var zeuge = _zufall.NextDouble() < (wohnhaus ? ZeugenchanceWohnhaus(a.Ziel, erfolg, a.Auftraggeber) : Zeugenchance(b, erfolg, a.Auftraggeber));
        if (zeuge && spur is not null)
        {
            _w.Justiz.BeweisGeben(a.Ziel, spur);
            oeffentlich += $" Ein Zeuge hat Leute von {a.Auftraggeber.Inhaber} erkannt!";
        }
        Ereignis(null, oeffentlich);
    }

    /// <summary>Ein Betrieb wurde angegriffen (auch von Dieben) – die Stadt redet davon, die KI stellt Wächter ein.</summary>
    internal void AngriffMerken(string betrieb) => _letzterAngriff[betrieb] = _w.Jahr;

    /// <summary>Der Betrieb wurde verkauft: Der nächste Besitzer erbt keine alten Anschläge.</summary>
    internal void BetriebAufgegeben(Betrieb b) => _letzterAngriff.Remove(b.Name);

    /// <summary>Nachricht aus der Unterwelt (auch von den Dieben) – erscheint im Hinterzimmer und im Protokoll.</summary>
    internal void Melden(Konto? fuer, string text) => Ereignis(fuer, text);

    private void Ereignis(Konto? fuer, string text)
    {
        _diesesJahr.Add(new Nachtereignis(fuer, text));
        _w.Melden(text);
    }

    /// <summary>
    /// Jahresende (nach dem Jahresabschluss): Erpresserbriefe gelten bis zum Ende des Folgejahres –
    /// sonst verfiele ein spät im Jahr geschickter Brief, bevor jemand antworten kann. Danach heißt Schweigen Nein.
    /// </summary>
    internal void Jahresende()
    {
        if (!IstAktiv) return;
        foreach (var f in _forderungen.Where(f => f.Jahr < _w.Jahr - 1).ToList())
        {
            _forderungen.Remove(f);
            Ereignis(null, $"{f.Opfer.Inhaber} hat auf den Erpresserbrief von {f.Erpresser.Inhaber} nicht geantwortet – das gilt als Nein.");
        }
        _letztesJahr = _diesesJahr;
        _diesesJahr = new List<Nachtereignis>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Speichern
    // ═════════════════════════════════════════════════════════════════════════

    internal UnterweltStand Sichern(Func<Konto, int> nr) => new()
    {
        ZufallWuerfe = _zufall.Wuerfe,
        Auftraege = _auftraege.Select((a, i) => new AuftragStand
        {
            Auftraggeber = nr(a.Auftraggeber), Ziel = nr(a.Ziel), Art = a.Art, Betrieb = a.Betrieb,
            Erfolgreich = i < _erfolgreich.Count && _erfolgreich[i],
        }).ToList(),
        Hinterhalte = _hinterhalte.Select(h => new HinterhaltStand { Auftraggeber = nr(h.Auftraggeber), Ziel = nr(h.Ziel), BisMinute = h.BisMinute }).ToList(),
        Forderungen = _forderungen.Select(f => new ForderungStand { Erpresser = nr(f.Erpresser), Opfer = nr(f.Opfer), Betrag = f.Betrag, Jahr = f.Jahr }).ToList(),
        DiesesJahr = _diesesJahr.Select(e => new NachtereignisStand { Fuer = e.Fuer is null ? null : nr(e.Fuer), Text = e.Text }).ToList(),
        LetztesJahr = _letztesJahr.Select(e => new NachtereignisStand { Fuer = e.Fuer is null ? null : nr(e.Fuer), Text = e.Text }).ToList(),
        LetzterAngriff = _letzterAngriff.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value),
    };

    internal void Wiederherstellen(UnterweltStand s, IReadOnlyList<Konto> k)
    {
        _zufall = new Zufallsquelle(_zufall.Startwert, s.ZufallWuerfe);
        _auftraege.Clear();
        _auftraege.AddRange(s.Auftraege.Select(a => new Gaunerauftrag(k[a.Auftraggeber], k[a.Ziel], a.Art, a.Betrieb)));
        _erfolgreich.Clear();
        _erfolgreich.AddRange(s.Auftraege.Select(a => a.Erfolgreich));
        _hinterhalte.Clear();
        _hinterhalte.AddRange(s.Hinterhalte.Select(h => new Hinterhalt(k[h.Auftraggeber], k[h.Ziel], h.BisMinute)));
        _forderungen.Clear();
        _forderungen.AddRange(s.Forderungen.Select(f => new Forderung(k[f.Erpresser], k[f.Opfer], f.Betrag, f.Jahr)));
        _diesesJahr = s.DiesesJahr.Select(e => new Nachtereignis(e.Fuer is int n ? k[n] : null, e.Text)).ToList();
        _letztesJahr = s.LetztesJahr.Select(e => new Nachtereignis(e.Fuer is int n ? k[n] : null, e.Text)).ToList();
        _letzterAngriff.Clear();
        foreach (var (betrieb, jahr) in s.LetzterAngriff) _letzterAngriff[betrieb] = jahr;
    }
}
