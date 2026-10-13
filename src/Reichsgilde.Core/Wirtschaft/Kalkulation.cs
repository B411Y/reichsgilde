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

    /// <summary>Wert einer Arbeitsminute in diesem Betrieb bei der aktuellen Ausrichtung.</summary>
    public static double WertJeArbeitsminute(Stadtwirtschaft w, Betrieb betrieb) => betrieb.Art == Betriebsart.Giesserei
        ? GewinnJeArbeitsminute(w, betrieb.Produkt)
        : (1 - betrieb.Silberanteil) * GewinnJeArbeitsminute(w, "Eisen") + betrieb.Silberanteil * GewinnJeArbeitsminute(w, "Silber");

    /// <summary>
    /// Erwarteter Jahresertrag einer Arbeitskraft in diesem Betrieb:
    /// Leistung × Arbeitsminuten × Wert je Minute − Lohn − Risiko × Schaden.
    /// </summary>
    public static double Jahresertrag(Stadtwirtschaft w, Betrieb betrieb, string arbeitskraft)
    {
        var kraft = w.Daten.Arbeitskraefte[arbeitskraft];
        return kraft.TempoAm(betrieb.Ort) * w.Daten.Allgemein.Arbeitsminuten * WertJeArbeitsminute(w, betrieb)
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

    /// <summary>Was eine volle Ladung dieses Karrens ungefähr wert ist (mit den Waren, die der Betrieb verkauft).</summary>
    public static double Ladungswert(Stadtwirtschaft w, Karren karren)
    {
        var betrieb = karren.Heimat;
        var preis = betrieb.Art == Betriebsart.Giesserei
            ? w.Markt[betrieb.Produkt].Preis
            : (1 - betrieb.Silberanteil) * w.Markt["Eisen"].Preis + betrieb.Silberanteil * w.Markt["Silber"].Preis;
        return karren.Daten.Gesamtkapazitaet * preis;
    }
}
