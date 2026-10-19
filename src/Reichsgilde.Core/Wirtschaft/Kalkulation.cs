namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Die Rechnungen eines guten Kaufmanns – zu aktuellen Marktpreisen.
///
/// Spieler (im Bedienfeld) und KI-Rivalen (bei ihren Entscheidungen) benutzen GENAU diese
/// Rechnungen. So sieht der Spieler dieselben Zahlen, nach denen die KI handelt.
/// </summary>
public static class Kalkulation
{
    /// <summary>
    /// Gewinn je Arbeitsminute, wenn man diese Ware jetzt herstellt und verkauft:
    /// (Verkaufspreis nach Steuer − Zutaten zum Marktpreis) ÷ Herstellungszeit.
    /// Für Rohstoffe (keine Zutaten) ist das einfach Preis nach Steuer ÷ Förderzeit.
    /// </summary>
    public static double GewinnJeArbeitsminute(Stadtwirtschaft w, string ware)
    {
        var daten = w.Daten.Waren[ware];
        var erloes = w.Markt[ware].Preis * (1 - w.Daten.Allgemein.Umsatzsteuer);
        var zutaten = daten.Zutaten.Sum(z => w.Markt[z.Key].Preis * z.Value);
        return (erloes - zutaten) / daten.Zeit;
    }

    /// <summary>
    /// Wert einer Arbeitsminute in diesem Betrieb bei der aktuellen Ausrichtung – in einer Werkstatt nach den Waren in Arbeit gewichtet,
    /// mit der Einrichtung (weniger Zeit je Stück = mehr wert je Minute).
    /// </summary>
    public static double WertJeArbeitsminute(Stadtwirtschaft w, Betrieb betrieb) =>
        betrieb.OhneProduktion ? w.WertOhneWare(betrieb.Artdaten)
        : betrieb.IstWerkstatt ? Werkstattmix(betrieb, ware => GewinnJeArbeitsminute(w, ware) / betrieb.ZeitFaktor(ware))
        : Gemischt(betrieb, ware => GewinnJeArbeitsminute(w, ware));

    /// <summary>Werkstatt: nach dem Anteil der Waren an der Arbeit gewichtet (eine Ware: genau deren Wert).</summary>
    private static double Werkstattmix(Betrieb b, Func<string, double> wert) =>
        b.WarenInArbeit.Count == 1 ? wert(b.WarenInArbeit[0]) : b.WarenAnteile.Sum(x => x.Value * wert(x.Key));

    /// <summary>Umland: Wert der Förderung – bei zwei Rohstoffen nach der Aufteilung der Arbeitszeit gewichtet.</summary>
    private static double Gemischt(Betrieb b, Func<string, double> wert) => b.Rohstoffe.Count switch
    {
        0 => 0,
        1 => wert(b.Rohstoffe[0]),
        _ => (1 - b.Silberanteil) * wert(b.Rohstoffe[0]) + b.Silberanteil * wert(b.Rohstoffe[1]),
    };

    /// <summary>
    /// Erwarteter Jahresertrag einer Arbeitskraft in diesem Betrieb:
    /// Leistung (× Völkerbonus) × Arbeitsminuten × Wert je Minute − Lohn − Risiko × Schaden.
    /// </summary>
    public static double Jahresertrag(Stadtwirtschaft w, Betrieb betrieb, string arbeitskraft)
    {
        var kraft = w.Daten.Arbeitskraefte[arbeitskraft];
        return kraft.TempoAm(betrieb.Ort) * w.VolksFaktor(betrieb.Konto, betrieb.Artdaten.Beruf) // Völkerbonus des Hauses
               * w.Daten.Allgemein.Arbeitsminuten * WertJeArbeitsminute(w, betrieb)
               - kraft.Kosten
               - kraft.Risiko * kraft.Schaden;
    }

    /// <summary>
    /// Erwartete Kosten einer beladenen Umland-Fahrt mit dieser Eskorte:
    /// Eskortenlohn + Chance auf Überfall × Erfolgschance der Banden × Wert der Ladung × Vorsicht.
    /// Gerechnet wird mit der Tageschance (nachts ist sie höher).
    /// </summary>
    public static double ErwarteteFahrtkosten(Stadtwirtschaft w, string eskorte, double ladungswert, double vorsicht = 1)
    {
        var l = w.Daten.Logistik;
        var schutz = l.Eskorten[eskorte].Schutz;
        var gewichte = l.Banden.Sum(b => b.Gewicht);
        var erfolg = gewichte <= 0 ? 0 : l.Banden.Sum(b => b.Gewicht * Math.Min(1, b.Staerke * (1 - schutz))) / gewichte;
        var verlust = Math.Min(1, l.Ueberfall.ChanceTag) * erfolg * ladungswert;
        return l.Eskorten[eskorte].KostenJeFahrt + verlust * vorsicht;
    }

    /// <summary>
    /// Erwarteter Jahresgewinn eines NEUEN Betriebs dieser Art bei voller Belegung mit der besten
    /// erlaubten Arbeitskraft und dem einträglichsten Produkt – zu heutigen Preisen.
    /// Grundlage für „Lohnt sich der Kauf?" (Spieler-Anzeige und KI).
    /// </summary>
    /// <param name="statut">Unter welchem Statut gerechnet wird (ohne Angabe: das geltende) – für „Was brächte Statut C?".</param>
    /// <param name="leistung">Faktor auf die Leistung der Arbeitskräfte – etwa der Völkerbonus des Hauses (<see cref="Stadtwirtschaft.VolksFaktor"/>).</param>
    public static double ErwarteterJahresgewinn(Stadtwirtschaft w, Betriebsart art, Fronstatut? statut = null, double leistung = 1)
    {
        var geltend = statut ?? w.Statut;
        var d = w.Daten.Art(art);
        var ort = d.Ort;
        var wertJeMinute = d.OhneProduktion ? w.WertOhneWare(d)
            : (d.IstWerkstatt ? d.Produkte : d.Rohstoffe).Max(ware => GewinnJeArbeitsminute(w, ware));
        var besterKopf = w.Daten.Arbeitskraefte.Values
            .Where(k => k.IstErlaubt(geltend, ort))
            .Select(k => k.TempoAm(ort) * leistung * w.Daten.Allgemein.Arbeitsminuten * wertJeMinute - k.Kosten - k.Risiko * k.Schaden)
            .DefaultIfEmpty(0)
            .Max();
        return Math.Max(0, besterKopf) * w.Daten.Plaetze(art) - w.Daten.Fixkosten(art);
    }

    /// <summary>
    /// Was der Ausbau zur nächsten Stufe im Jahr mehr bringt (voll belegt, bestes Produkt, heutige Preise) –
    /// 0, wenn es keine nächste Stufe gibt.
    /// </summary>
    public static double AusbauMehrgewinn(Stadtwirtschaft w, Betrieb betrieb) =>
        betrieb.Artdaten.Ausbau is { } neu
            ? ErwarteterJahresgewinn(w, neu) - ErwarteterJahresgewinn(w, betrieb.Art)
            : 0;

    /// <summary>Was eine volle Ladung dieses Karrens ungefähr wert ist (mit den Waren, die der Betrieb verkauft).</summary>
    public static double Ladungswert(Stadtwirtschaft w, Karren karren)
    {
        var betrieb = karren.Heimat;
        var preis = betrieb.OhneProduktion ? betrieb.Lager.Keys.Select(ware => w.Markt[ware].Preis).DefaultIfEmpty(0).Max() // Diebesgut
            : betrieb.IstWerkstatt ? Werkstattmix(betrieb, ware => w.Markt[ware].Preis) : Gemischt(betrieb, ware => w.Markt[ware].Preis);
        return karren.Daten.Gesamtkapazitaet * preis;
    }
}
