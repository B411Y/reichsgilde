using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Der Charakter eines KI-Rivalen. Zwei Rivalen mit verschiedenen Profilen
/// entscheiden in derselben Lage unterschiedlich.
/// </summary>
/// <param name="Reserve">So viel Gulden behält die KI immer in der Kasse, bevor sie investiert.</param>
/// <param name="Wechselschwelle">Um so viel (0,15 = 15 %) muss ein anderes Produkt besser sein, damit umgestellt wird.</param>
/// <param name="MindestStundenJeProdukt">So lange bleibt die KI mindestens bei einem Produkt.</param>
/// <param name="Vorsicht">1 = rechnet nüchtern; 2 = fürchtet Raubverluste doppelt so stark (nimmt eher Eskorte).</param>
/// <param name="MaxKarrenJeBetrieb">Mehr Karren kauft die KI für einen Betrieb nicht.</param>
/// <param name="StillstandFuerKarrenkauf">Ab diesem Anteil Stillstand an der Arbeitszeit (0,2 = 20 %) kauft sie einen Karren.</param>
public sealed record KiProfil(
    double Reserve = 1500,
    double Wechselschwelle = 0.15,
    int MindestStundenJeProdukt = 6,
    double Vorsicht = 1.0,
    int MaxKarrenJeBetrieb = 3,
    double StillstandFuerKarrenkauf = 0.2)
{
    public static KiProfil Standard { get; } = new();
}

/// <summary>
/// Ein Rivale, der seine Betriebe selbst führt – mit denselben Regeln und Informationen,
/// die auch der Spieler hat (Marktpreise, Daten, eigenes Konto). Die KI schummelt nicht:
/// Sie sieht keine Zukunft und kennt keine Zufallswürfe.
///
/// Was sie entscheidet und wann:
///
///   jede volle Stunde   • Gießerei: das Produkt mit dem höchsten Gewinn je Arbeitsminute
///                       • Mine: Aufteilung der Förderung zwischen Eisen und Silber
///                       • freie Arbeitsplätze mit der ertragreichsten erlaubten Arbeitskraft besetzen
///                       • Eskorte je Karren nach erwartetem Verlust wählen
///   zum Jahresende      • Karren kaufen, wenn ein Betrieb zu oft stillstand
///
/// Jede Entscheidung wird als Meldung ausgegeben, damit der Spieler sie nachvollziehen kann.
/// </summary>
public sealed class KiRivale
{
    private readonly Stadtwirtschaft _w;
    private readonly Dictionary<Betrieb, int> _stundenSeitWechsel = new();
    private readonly List<string> _entscheidungen = new();
    private const int MaxGemerkteEntscheidungen = 20;

    public Konto Konto { get; }
    public KiProfil Profil { get; }

    /// <summary>Die letzten Entscheidungen, neueste zuerst.</summary>
    public IReadOnlyList<string> Entscheidungen => _entscheidungen;

    /// <summary>Alle Betriebe, die diesem Konto gehören.</summary>
    public IEnumerable<Betrieb> Betriebe => _w.Betriebe.Where(b => b.Konto == Konto);

    internal KiRivale(Konto konto, KiProfil profil, Stadtwirtschaft w)
    {
        Konto = konto;
        Profil = profil;
        _w = w;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ablauf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Stündliche Überlegungen.</summary>
    internal void Stunde()
    {
        foreach (var betrieb in Betriebe.ToList())
        {
            _stundenSeitWechsel[betrieb] = _stundenSeitWechsel.GetValueOrDefault(betrieb, int.MaxValue / 2) + 1;
            if (betrieb.Art == Betriebsart.Giesserei)
                ProduktWaehlen(betrieb);
            else
                FoerderungAufteilen(betrieb);
            PersonalBesetzen(betrieb);
            EskorteWaehlen(betrieb);
        }
    }

    /// <summary>Überlegungen zum Jahresende (nach der Abrechnung).</summary>
    internal void Jahresende()
    {
        foreach (var betrieb in Betriebe.ToList())
            KarrenKaufen(betrieb);
    }

    private static string Prozent(double anteil) => $"{Zahlenformat.Ganz(anteil * 100)} %";

    private void Entscheiden(string text)
    {
        var meldung = $"{Konto.Inhaber}: {text}";
        _entscheidungen.Insert(0, meldung);
        if (_entscheidungen.Count > MaxGemerkteEntscheidungen)
            _entscheidungen.RemoveAt(_entscheidungen.Count - 1);
        _w.Melden(meldung);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Rechnungen – öffentlich, damit Tests und später die Oberfläche sie zeigen können
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gewinn je Arbeitsminute, wenn man diese Ware jetzt herstellt und verkauft:
    /// (Verkaufspreis nach Steuer − Zutaten zum Marktpreis) ÷ Herstellungszeit.
    /// Für Rohstoffe (keine Zutaten) ist das einfach Preis nach Steuer ÷ Förderzeit.
    /// </summary>
    public double GewinnJeArbeitsminute(string ware)
    {
        var daten = _w.Daten.Waren[ware];
        var erloes = _w.Markt[ware].Preis * (1 - _w.Daten.Allgemein.Umsatzsteuer);
        var zutaten = daten.Zutaten.Sum(z => _w.Markt[z.Key].Preis * z.Value);
        return (erloes - zutaten) / daten.Zeit;
    }

    /// <summary>Wert einer Arbeitsminute in diesem Betrieb bei der aktuellen Ausrichtung.</summary>
    public double WertJeArbeitsminute(Betrieb betrieb) => betrieb.Art == Betriebsart.Giesserei
        ? GewinnJeArbeitsminute(betrieb.Produkt)
        : (1 - betrieb.Silberanteil) * GewinnJeArbeitsminute("Eisen") + betrieb.Silberanteil * GewinnJeArbeitsminute("Silber");

    /// <summary>
    /// Erwarteter Jahresertrag einer Arbeitskraft in diesem Betrieb:
    /// Leistung × Arbeitsminuten × Wert je Minute − Lohn − Risiko × Schaden.
    /// </summary>
    public double Jahresertrag(Betrieb betrieb, string arbeitskraft)
    {
        var kraft = _w.Daten.Arbeitskraefte[arbeitskraft];
        return kraft.TempoAm(betrieb.Ort) * _w.Daten.Allgemein.Arbeitsminuten * WertJeArbeitsminute(betrieb)
               - kraft.Kosten
               - kraft.Risiko * kraft.Schaden;
    }

    /// <summary>
    /// Erwartete Kosten einer beladenen Fahrt mit dieser Eskorte:
    /// Eskortenlohn + Chance auf Überfall × Erfolgschance der Banden × Wert der Ladung × Vorsicht.
    /// Die KI rechnet mit der Tageschance, weil ihre Karren nachts nicht ins Umland fahren.
    /// </summary>
    public double ErwarteteFahrtkosten(string eskorte, double ladungswert)
    {
        var l = _w.Daten.Logistik;
        var schutz = l.Eskorten[eskorte].Schutz;
        var gewichte = l.Banden.Sum(b => b.Gewicht);
        var erfolg = gewichte <= 0 ? 0 : l.Banden.Sum(b => b.Gewicht * Math.Min(1, b.Staerke * (1 - schutz))) / gewichte;
        var verlust = Math.Min(1, l.Ueberfall.ChanceTag) * erfolg * ladungswert;
        return l.Eskorten[eskorte].KostenJeFahrt + verlust * Profil.Vorsicht;
    }

    /// <summary>Was eine volle Ladung dieses Karrens ungefähr wert ist (mit den Waren, die der Betrieb verkauft).</summary>
    public double Ladungswert(Karren karren)
    {
        var betrieb = karren.Heimat;
        double preis = betrieb.Art == Betriebsart.Giesserei
            ? _w.Markt[betrieb.Produkt].Preis
            : (1 - betrieb.Silberanteil) * _w.Markt["Eisen"].Preis + betrieb.Silberanteil * _w.Markt["Silber"].Preis;
        return karren.Daten.Gesamtkapazitaet * preis;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Entscheidungen
    // ═════════════════════════════════════════════════════════════════════════

    private void ProduktWaehlen(Betrieb betrieb)
    {
        if (_stundenSeitWechsel[betrieb] < Profil.MindestStundenJeProdukt)
            return;

        var aktuell = GewinnJeArbeitsminute(betrieb.Produkt);
        var (bestes, wert) = _w.Daten.Waren
            .Where(x => x.Value.Zutaten.Count > 0)
            .Select(x => (Ware: x.Key, Wert: GewinnJeArbeitsminute(x.Key)))
            .OrderByDescending(x => x.Wert)
            .ThenBy(x => x.Ware, StringComparer.Ordinal)
            .First();

        // Umstellen nur bei deutlichem Vorsprung – sonst springt die KI bei jeder Preisschwankung hin und her.
        if (bestes == betrieb.Produkt || wert <= aktuell + Math.Abs(aktuell) * Profil.Wechselschwelle)
            return;

        var alt = betrieb.Produkt;
        betrieb.Produkt = bestes;
        _stundenSeitWechsel[betrieb] = 0;
        Entscheiden($"{betrieb.Name} stellt von {alt} auf {bestes} um " +
                    $"({Zahlenformat.Kommazahl(aktuell)} → {Zahlenformat.Kommazahl(wert)} {_w.Daten.Allgemein.Waehrung} je Arbeitsminute).");
    }

    private void FoerderungAufteilen(Betrieb betrieb)
    {
        if (_stundenSeitWechsel[betrieb] < Profil.MindestStundenJeProdukt)
            return;

        // Eisen und Silber bringen je Arbeitsminute ähnlich viel; die Preise entscheiden.
        // Drei feste Stufen statt einer gleitenden Zahl – so pendelt die Mine nicht bei jeder kleinen Preisbewegung.
        // Nie ganz auf eine Ware setzen: Ein volles Lager würde die Förderung sonst stoppen.
        var eisen = Math.Max(0.01, GewinnJeArbeitsminute("Eisen"));
        var silber = Math.Max(0.01, GewinnJeArbeitsminute("Silber"));
        var vorsprung = (silber - eisen) / (silber + eisen); // −1 … +1
        var anteil = Math.Abs(vorsprung) switch
        {
            < 0.10 => 0.5,
            < 0.25 => vorsprung > 0 ? 0.65 : 0.35,
            _ => vorsprung > 0 ? 0.8 : 0.2,
        };
        if (Math.Abs(anteil - betrieb.Silberanteil) < 0.01)
            return;
        betrieb.Silberanteil = anteil;
        _stundenSeitWechsel[betrieb] = 0;
        Entscheiden($"{betrieb.Name} fördert nun zu {Prozent(anteil)} Silber und {Prozent(1 - anteil)} Eisen.");
    }

    private void PersonalBesetzen(Betrieb betrieb)
    {
        var frei = betrieb.Plaetze - betrieb.Belegt;
        if (frei <= 0) return;

        var beste = _w.Daten.Arbeitskraefte.Keys
            .Where(k => _w.Daten.Arbeitskraefte[k].IstErlaubt(_w.Statut, betrieb.Ort))
            .Select(k => (Kraft: k, Ertrag: Jahresertrag(betrieb, k)))
            .Where(x => x.Ertrag > 0)
            .OrderByDescending(x => x.Ertrag)
            .ThenBy(x => x.Kraft, StringComparer.Ordinal)
            .FirstOrDefault();
        if (beste.Kraft is null) return;

        // Löhne werden erst am Jahresende fällig – trotzdem keine Einstellung, wenn sie die Reserve auffressen.
        var lohn = _w.Daten.Arbeitskraefte[beste.Kraft].Kosten;
        var bezahlbar = (int)Math.Floor((Konto.Vermoegen - Profil.Reserve / 2) / lohn);
        var anzahl = Math.Min(frei, bezahlbar);
        if (anzahl <= 0) return;

        _w.Einstellen(betrieb, beste.Kraft, betrieb.Belegung.GetValueOrDefault(beste.Kraft) + anzahl);
        Entscheiden($"{betrieb.Name} stellt {anzahl}× {beste.Kraft} ein " +
                    $"(erwarteter Ertrag {Zahlenformat.Ganz(beste.Ertrag)} {_w.Daten.Allgemein.Waehrung} je Kopf und Jahr).");
    }

    private void EskorteWaehlen(Betrieb betrieb)
    {
        foreach (var karren in betrieb.Karren)
        {
            // In der Stadt gibt es keine Überfälle – Eskorte wäre verschenktes Geld.
            var beste = betrieb.Ort == Arbeitsort.Werkstatt
                ? "Keine"
                : _w.Daten.Logistik.Eskorten.Keys
                    .OrderBy(e => ErwarteteFahrtkosten(e, Ladungswert(karren)))
                    .ThenBy(e => _w.Daten.Logistik.Eskorten[e].KostenJeFahrt)
                    .First();
            if (!_w.Daten.Logistik.Eskorten.ContainsKey(beste) || beste == karren.Eskorte)
                continue;
            var alt = karren.Eskorte;
            karren.Eskorte = beste;
            Entscheiden($"{karren.Name} von {betrieb.Name} fährt nun mit Eskorte „{beste}“ statt „{alt}“.");
        }
    }

    private void KarrenKaufen(Betrieb betrieb)
    {
        if (betrieb.Belegt == 0 || betrieb.Karren.Count >= Profil.MaxKarrenJeBetrieb)
            return;

        var anteil = betrieb.StillstandMinutenVorjahr / (double)_w.Daten.Allgemein.Arbeitsminuten;
        if (anteil < Profil.StillstandFuerKarrenkauf)
            return;

        // Kurze Stadtwege: kleiner, billiger Karren. Lange, gefährliche Umlandwege: viel Ladung je Fahrt.
        var typ = betrieb.Ort == Arbeitsort.Werkstatt ? "Handkarren" : "Ochsenkarren";
        if (!_w.Daten.Logistik.Karren.TryGetValue(typ, out var daten))
            return;
        if (Konto.Vermoegen - daten.Preis < Profil.Reserve)
            return;

        var karren = betrieb.KarrenKaufen(typ);
        Entscheiden($"{betrieb.Name} stand {Prozent(anteil)} der Arbeitszeit still – {karren.Name} gekauft " +
                    $"({Zahlenformat.Ganz(daten.Preis)} {_w.Daten.Allgemein.Waehrung}).");
        EskorteWaehlen(betrieb);
    }
}
