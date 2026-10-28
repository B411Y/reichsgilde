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
        var erloes = w.Markt[ware].Preis * (1 - w.Umsatzsteuer);
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
    /// Leistung (× Völkerbonus) × Arbeitsminuten des Betriebs × Wert je Minute − Lohn (für diese Schicht) − Risiko × Schaden.
    /// </summary>
    public static double Jahresertrag(Stadtwirtschaft w, Betrieb betrieb, string arbeitskraft)
    {
        var kraft = w.Daten.Arbeitskraefte[arbeitskraft];
        return kraft.TempoAm(betrieb.Ort) * w.VolksFaktor(betrieb.Konto, betrieb.Artdaten.Beruf) // Völkerbonus des Hauses
               * betrieb.Arbeitsminuten * WertJeArbeitsminute(w, betrieb)
               - w.Daten.Lohn(arbeitskraft, betrieb.Art)
               - kraft.Risiko * kraft.Schaden;
    }

    /// <summary>
    /// Erwartete Kosten einer beladenen Umland-Fahrt mit dieser Eskorte:
    /// Eskortenlohn + Chance auf Überfall × Erfolgschance der Banden × Wert der Ladung × Vorsicht.
    /// Gerechnet wird mit der Tageschance (nachts ist sie höher); beim Geleit der Wache mit ihren Kettenhemden.
    /// </summary>
    public static double ErwarteteFahrtkosten(Stadtwirtschaft w, string eskorte, double ladungswert, double vorsicht = 1)
    {
        var l = w.Daten.Logistik;
        var schutz = l.Eskorten[eskorte].Schutz;
        var faktor = w.Wache.UeberfallFaktor(l.Eskorten[eskorte]);
        var gewichte = l.Banden.Sum(b => b.Gewicht);
        var erfolg = gewichte <= 0 ? 0 : l.Banden.Sum(b => b.Gewicht * Math.Min(1, b.Staerke * (1 - schutz) * faktor)) / gewichte;
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
        var minuten = w.Daten.Arbeitsminuten(art);
        var schicht = w.Daten.Schichtfaktor(art); // Lohn je Stunde gleich: die längere Schicht im Wirtshaus kostet mehr
        var besterKopf = w.Daten.Arbeitskraefte.Values
            .Where(k => k.IstErlaubt(geltend, ort))
            .Select(k => k.TempoAm(ort) * leistung * minuten * wertJeMinute - k.Kosten * schicht - k.Risiko * k.Schaden)
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

    // ── Schiffshandel (PR #199) ─────────────────────────────────────────────

    /// <summary>
    /// Erwartete Rechnung einer Schiffsfahrt zu heutigen Preisen – die Zahlen der Oberfläche und der KI:
    /// <code>
    /// Gewinn = Erlös dort − Einkauf hier + Erlös hier − Einkauf dort − Zoll − Geleit − Risiko
    /// </code>
    /// Einkauf hier: ohne Betrieb der Preis von Hand am Markt (jedes Stück hebt ihn); mit Betrieb, was die Ware am Markt gebracht hätte
    /// (nach Steuer). Erlös hier: was die Rückfracht am Markt bringt (nach Steuer; jedes Stück drückt den Preis) – auch für Ware, die ins
    /// Lager geht. Zoll je beladener Fahrt: Zollstellen × Anteil × Wert der Ladung zum Marktpreis. Geleit je beladener Fahrt.
    /// Risiko: Chance der Piraten × Erfolgschance (mit Geleit) × Beuteanteil × Wert der Ladung × <paramref name="vorsicht"/>.
    /// </summary>
    public static Fahrtrechnung Schiffsfahrt(Stadtwirtschaft w, Konto konto, SchiffstypDaten typ, FerneStadt stadt,
        IReadOnlyDictionary<string, int> hinfracht, IReadOnlyDictionary<string, int> rueckfracht, bool ausLager = false, string geleit = "Keine",
        double vorsicht = 1)
    {
        var d = w.Schiffahrt.Daten ?? throw new InvalidOperationException("In dieser Stadt gibt es keinen Schiffshandel.");
        var steuer = w.SteuersatzMarkt(konto); // der Marktmeister zahlt weniger
        var vf = w.Familien.VerkaufsFaktor(konto);
        var ef = w.Familien.EinkaufsFaktor(konto);
        var hin = hinfracht.Where(x => x.Value > 0).ToList();
        var rueck = rueckfracht.Where(x => x.Value > 0).ToList();
        var einkauf = hin.Sum(x => ausLager ? w.Markt[x.Key].Verkaufswert(x.Value) * (1 - steuer) * vf : w.HandKaufpreis(konto, x.Key, x.Value, d.HeimatmarktAnteil));
        var erloesFern = hin.Sum(x => stadt.Erloes(x.Key, x.Value)) * vf;
        var einkaufFern = rueck.Sum(x => stadt.Kosten(x.Key, x.Value)) * ef;
        var erloesHeim = rueck.Sum(x => w.Markt[x.Key].Verkaufswert(x.Value)) * (1 - steuer) * vf;
        var wertHin = hin.Sum(x => w.Markt[x.Key].Preis * x.Value);
        var wertRueck = rueck.Sum(x => w.Markt[x.Key].Preis * x.Value);
        var zoll = w.Schiffahrt.Zollstellen(konto, stadt) * d.ZollJeStelle * (wertHin + wertRueck);
        var fahrten = (hin.Count > 0 ? 1 : 0) + (rueck.Count > 0 ? 1 : 0);
        var risiko = SchiffsRisiko(w, konto, geleit) * (wertHin + wertRueck) * vorsicht;
        return new Fahrtrechnung(einkauf, erloesFern, einkaufFern, erloesHeim, zoll, w.Schiffahrt.GeleitKosten(geleit) * fahrten, risiko,
            Schiffahrt.Rundreise(typ, stadt, w.Ratsantraege.Hafenfaktor));
    }

    /// <summary>
    /// Erwarteter Verlust je beladener Fahrt als Anteil vom Wert der Ladung: Chance der Piraten × Erfolgschance (Stärke × (1 − Schutz des
    /// Geleits und der Kampfkunst), Kettenhemden der Wache) × Beuteanteil.
    /// </summary>
    public static double SchiffsRisiko(Stadtwirtschaft w, Konto konto, string geleit)
    {
        if (w.Schiffahrt.Daten is not { } d) return 0;
        var e = w.Daten.Logistik.Eskorten.GetValueOrDefault(geleit) ?? new EskorteDaten();
        var schutz = Math.Min(0.95, e.Schutz + w.Familien.KampfSchutz(konto));
        var erfolg = Math.Min(1, d.Piraten.Staerke * (1 - schutz) * w.Wache.UeberfallFaktor(e));
        return d.Piraten.Chance * erfolg * d.Piraten.BeuteAnteil;
    }

    /// <summary>
    /// Der einträglichste Auftrag für ein Schiff zu heutigen Preisen (Ware vom Markt, Rückfracht auf den Markt) – für die KI und als Vorschlag
    /// der Oberfläche. Je ferner Stadt füllt er Laderaum um Laderaum mit der Ware, die den meisten Gewinn dazubringt (ganz, zwei Drittel oder
    /// ein Drittel voll), solange sie etwas bringt und das Geld (<paramref name="budget"/>) für die Hinfracht reicht; gewählt wird die Stadt mit
    /// dem höchsten Gewinn je Jahr (nach der Dauer der Fahrt). Null, wenn keine Fahrt Gewinn bringt.
    /// </summary>
    /// <param name="ziel">Nur diese ferne Stadt (der Vorschlag der Hafentafel für das gewählte Ziel); null = alle.</param>
    public static Fahrtvorschlag? BesteSchiffsfahrt(Stadtwirtschaft w, Konto konto, SchiffstypDaten typ, double budget, string geleit = "Keine",
        double vorsicht = 1, string? ziel = null)
    {
        if (w.Schiffahrt.Daten is not { } d) return null;
        var steuer = w.SteuersatzMarkt(konto);
        var vf = w.Familien.VerkaufsFaktor(konto);
        var ef = w.Familien.EinkaufsFaktor(konto);
        var hk = w.HandKaufFaktor(konto);
        var rate = SchiffsRisiko(w, konto, geleit) * vorsicht;
        Fahrtvorschlag? bester = null;
        foreach (var stadt in w.Schiffahrt.Staedte.Where(s => ziel is null || s.Name == ziel))
        {
            var zollRate = w.Schiffahrt.Zollstellen(konto, stadt) * d.ZollJeStelle + rate;
            // Hinfracht: am Markt kaufen, dort verkaufen
            var hin = Fuellen(typ, stadt.Daten.Kauft.Keys, (ware, n, q) =>
                stadt.Erloes(ware, q, n) * vf - w.Markt[ware].HandKaufpreis(q, hk, n, d.HeimatmarktAnteil) - zollRate * w.Markt[ware].Preis * q,
                (ware, n, q) => w.Markt[ware].HandKaufpreis(q, hk, n, d.HeimatmarktAnteil), budget);
            // Rückfracht: dort kaufen, hier verkaufen
            var rueck = Fuellen(typ, stadt.Daten.Verkauft.Keys, (ware, n, q) =>
                w.Markt[ware].Verkaufswert(q, n) * (1 - steuer) * vf - stadt.Kosten(ware, q, n) * ef - zollRate * w.Markt[ware].Preis * q,
                (_, _, _) => 0, double.MaxValue);
            if (hin.Count + rueck.Count == 0) continue;
            var r = Schiffsfahrt(w, konto, typ, stadt, hin, rueck, false, geleit, vorsicht);
            // Lohnt eine Richtung das Geleit nicht, fährt sie leer.
            if (rueck.Count > 0 && hin.Count > 0)
            {
                var ohneHin = Schiffsfahrt(w, konto, typ, stadt, new Dictionary<string, int>(), rueck, false, geleit, vorsicht);
                var ohneRueck = Schiffsfahrt(w, konto, typ, stadt, hin, new Dictionary<string, int>(), false, geleit, vorsicht);
                if (ohneHin.Gewinn > r.Gewinn) (hin, r) = (new Dictionary<string, int>(), ohneHin);
                if (ohneRueck.Gewinn > r.Gewinn) (rueck, r) = (new Dictionary<string, int>(), ohneRueck);
            }
            if (r.Gewinn <= 0) continue;
            if (bester is null || r.GewinnJeJahr > bester.Rechnung.GewinnJeJahr)
                bester = new Fahrtvorschlag(stadt.Name, hin, rueck, r);
        }
        return bester;
    }

    /// <summary>Füllt die Laderäume eines Schiffs nacheinander mit der Ware, deren nächster Laderaum den meisten Gewinn dazubringt.</summary>
    /// <param name="gewinn">(Ware, schon geplant, dazu) → Mehrgewinn.</param>
    /// <param name="kosten">(Ware, schon geplant, dazu) → was das dazu kostet (gegen das Budget).</param>
    private static Dictionary<string, int> Fuellen(SchiffstypDaten typ, IEnumerable<string> waren, Func<string, int, int, double> gewinn,
        Func<string, int, int, double> kosten, double budget)
    {
        var plan = new Dictionary<string, int>();
        var liste = waren.OrderBy(x => x, StringComparer.Ordinal).ToList();
        var mengen = new[] { typ.KapazitaetJeSorte, typ.KapazitaetJeSorte * 2 / 3, typ.KapazitaetJeSorte / 3 }.Where(q => q > 0).Distinct().ToList();
        for (var raum = 0; raum < typ.Sorten; raum++)
        {
            (string Ware, int Menge, double Gewinn, double Kosten) best = ("", 0, 0, 0);
            foreach (var ware in liste)
                foreach (var q in mengen)
                {
                    var n = plan.GetValueOrDefault(ware);
                    var g = gewinn(ware, n, q);
                    if (g <= best.Gewinn + 1e-9) continue;
                    var k = kosten(ware, n, q);
                    if (k > budget) continue;
                    best = (ware, q, g, k);
                }
            if (best.Menge == 0) break;
            plan[best.Ware] = plan.GetValueOrDefault(best.Ware) + best.Menge;
            budget -= best.Kosten;
        }
        return plan;
    }

    /// <summary>
    /// Arbeitsminuten eines Gesellen, die der Betrieb in einer Spielminute schafft – wie die Simulation sie rechnet: Tempo und Laune der
    /// Arbeiter (wer nach einem Unfall fehlt, zählt nicht) × Handwerkskunst des Oberhaupts × Völkerbonus × (1 − Krankenstand im Pestjahr).
    /// </summary>
    public static double Leistung(Stadtwirtschaft w, Betrieb b) =>
        b.LeistungProMinute * w.Familien.LeistungsFaktor(b.Konto) * w.VolksFaktor(b.Konto, b.Artdaten.Beruf) * (1 - w.Gesundheit.Krankenstand);

    /// <summary>
    /// Gewinn je Stück dieser Ware in diesem Betrieb (Tooltips): Erlös am Markt (mit Verhandlungsgeschick) − Umsatzsteuer des Hauses
    /// − Zutaten zum Marktpreis − Lohnanteil − Unterhaltsanteil. Lohn (wie ihn die Jahresabrechnung zahlt, <see cref="Betrieb.LohnJeArbeiter"/>)
    /// und Unterhalt (mit dem Wächter) werden nach der Herstellzeit verteilt: Jahreskosten ÷ (Leistung × Arbeitsminuten) × Minuten je Stück.
    /// Ohne Personal rechnet sie mit einem Gesellen. Umland: Förderzeit statt Herstellzeit, keine Zutaten.
    /// </summary>
    public static Stueckrechnung GewinnJeStueck(Stadtwirtschaft w, Betrieb b, string ware)
    {
        var daten = w.Daten.Waren[ware];
        var preis = w.Markt[ware].Preis * w.Familien.VerkaufsFaktor(b.Konto);
        var steuer = preis * w.SteuersatzMarkt(b.Konto);
        var zutaten = daten.Zutaten.Sum(z => w.Markt[z.Key].Preis * z.Value);
        var minuten = b.IstWerkstatt ? b.MinutenJeStueck(ware) : daten.Zeit;
        var leistung = Leistung(w, b);
        var lohn = b.Lohnkosten;
        if (leistung <= 0 && w.Daten.Arbeitskraefte.TryGetValue("Geselle", out var geselle))
        {
            // Ohne Personal: was ein Geselle kostete und schaffte
            leistung = geselle.TempoAm(b.Ort) * w.Familien.LeistungsFaktor(b.Konto) * w.VolksFaktor(b.Konto, b.Artdaten.Beruf) * (1 - w.Gesundheit.Krankenstand);
            lohn = b.LohnJeArbeiter("Geselle");
        }
        var unterhalt = b.Artdaten.Fixkosten + (b.Waechter && w.Daten.Gauner is { } g ? g.WaechterKosten : 0);
        var jeMinute = leistung > 0 ? 1 / (leistung * b.Arbeitsminuten) : 0; // Anteil an den Jahreskosten je Arbeitsminute eines Gesellen
        return new Stueckrechnung(preis, steuer, zutaten, lohn * jeMinute * minuten, unterhalt * jeMinute * minuten, minuten);
    }

    /// <summary>Was eine volle Ladung dieses Karrens ungefähr wert ist (mit den Waren, die der Betrieb verkauft).</summary>
    public static double Ladungswert(Stadtwirtschaft w, Karren karren)
    {
        var betrieb = karren.Heimat;
        var preis = betrieb.OhneProduktion ? betrieb.Lager.Keys.Select(ware => w.Markt[ware].Preis).DefaultIfEmpty(0).Max() // Diebesgut
            : betrieb.IstWerkstatt ? Werkstattmix(betrieb, ware => w.Markt[ware].Preis) : Gemischt(betrieb, ware => w.Markt[ware].Preis);
        return karren.Daten.Gesamtkapazitaet * preis;
    }
}

/// <summary>Gewinn je Stück einer Ware in einem Betrieb (<see cref="Kalkulation.GewinnJeStueck"/>), alles in Gulden je Stück.</summary>
/// <param name="Preis">Erlös am Markt zum jetzigen Preis, mit dem Verhandlungsgeschick des Oberhaupts.</param>
/// <param name="Steuer">Umsatzsteuer des Hauses auf den Erlös.</param>
/// <param name="Zutaten">Zutaten zum Marktpreis.</param>
/// <param name="Lohn">Anteil der Löhne an der Herstellzeit.</param>
/// <param name="Unterhalt">Anteil von Unterhalt bzw. Pacht (und Wächter) an der Herstellzeit.</param>
/// <param name="Minuten">Arbeitsminuten eines Gesellen je Stück (mit der Einrichtung).</param>
public sealed record Stueckrechnung(double Preis, double Steuer, double Zutaten, double Lohn, double Unterhalt, double Minuten)
{
    /// <summary>Was ein Stück nach Steuer, Zutaten, Lohn und Unterhalt bringt.</summary>
    public double Gewinn => Preis - Steuer - Zutaten - Lohn - Unterhalt;
}

/// <summary>
/// Die erwartete Rechnung einer Schiffsfahrt (<see cref="Kalkulation.Schiffsfahrt"/>), alles in Gulden.
/// </summary>
/// <param name="Einkauf">Hinfracht: am Markt gekauft bzw. aus dem Lager (was sie am Markt gebracht hätte).</param>
/// <param name="ErloesFern">Verkauf der Hinfracht in der fernen Stadt.</param>
/// <param name="EinkaufFern">Kauf der Rückfracht in der fernen Stadt.</param>
/// <param name="ErloesHeim">Verkauf der Rückfracht am Markt (nach Steuer).</param>
/// <param name="Minuten">Dauer der ganzen Fahrt (beladen, hin, liegen, zurück).</param>
public sealed record Fahrtrechnung(double Einkauf, double ErloesFern, double EinkaufFern, double ErloesHeim, double Zoll, double Geleit,
    double Risiko, int Minuten)
{
    public double Gewinn => ErloesFern - Einkauf + ErloesHeim - EinkaufFern - Zoll - Geleit - Risiko;

    /// <summary>Gewinn im Jahr, wenn das Schiff solche Fahrten ohne Pause machte (ohne Unterhalt).</summary>
    public double GewinnJeJahr => Minuten <= 0 ? 0 : Gewinn * Zeit.Spielzeit.MinutenProTag / Minuten;
}

/// <summary>Ein Vorschlag für einen Schiffsauftrag (<see cref="Kalkulation.BesteSchiffsfahrt"/>).</summary>
public sealed record Fahrtvorschlag(string Ziel, IReadOnlyDictionary<string, int> Hinfracht, IReadOnlyDictionary<string, int> Rueckfracht,
    Fahrtrechnung Rechnung);
