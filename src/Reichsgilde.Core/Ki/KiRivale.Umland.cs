using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Umland nach dem Karrenweg (phase5/deutz-ki). Seit der Kern die Fahrzeit nach Weglänge rechnet (Datenversion 65), liegt das Kölner Umland
/// weit draußen: Die Umlandbetriebe der KI-Häuser fahren mit dem Ochsenkarren im Median 263 Minuten zum Markt (vorher fest 116), die KI-Häuser
/// lagen 11–13 % unter dem alten main. Fast der ganze Verlust kam aus dem Umland (Teil 5, Normal, je KI-Haus in 20 Jahren 32.971 → 26.510 Gulden),
/// am meisten aus Liegenschaften, die die KI ohne Blick auf den Weg pachtete: Der Holzschlag im Eichenforst fährt an der Höchstdauer
/// (360 Minuten), steht trotz drei Karren 53 % still und brachte 62 Gulden im Jahr; der Kräutergarten am Bach (325 Minuten) 26 % Stillstand.
///
/// Die Gewinnschätzung beim Pachten (<see cref="Kalkulation.ErwarteterJahresgewinn"/>) rechnet mit voller Förderung. Für einen Umlandbetrieb
/// zieht die KI jetzt ab, was die Karren voraussichtlich nicht wegbringen (<see cref="UmlandSchaetzen"/>): Gewinn − (1 − Anteil) × Wert der
/// Förderung; die Karren, die sie dafür über den Startkarren hinaus kaufen wird, zählen zu den Kosten wie der Kaufpreis (<see cref="NachDemWeg"/>).
/// Der Anteil kommt aus dem echten Weg der Liegenschaft (dieselbe Strecke, die der Kern nach der Pacht rechnet) und den Karren, die die KI dort
/// höchstens hätte (<see cref="KiProfil.MaxKarrenJeBetrieb"/> der Art mit dem meisten Durchfluss, wie beim Karrenkauf). Ein Weg, auf dem drei
/// Karren die Förderung nicht schaffen, gilt so viel weniger, dass die KI eine nähere Liegenschaft, eine Werkstatt oder nichts nimmt.
/// Ohne Fahrzeit nach Weglänge (alte Daten, keine Karte, kein Weg) bleibt es bei der alten Schätzung.
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>
    /// So viele Minuten im Jahr fährt ein Karren eines Umlandbetriebs im Mittel (beladen hin, leer zurück): mehr als die Hälfte des Spieltags wartet
    /// er aufs Laden, vor dem Tor oder hinter dem Karren davor. Geeicht an den Kölner Liegenschaften (main ec36ff33, Teil 5, 10 Startwerte, Mittel
    /// über die Jahre mit ein bis drei Karren): Holzschlag im Eichenforst gemessen 47 % weggebracht (geschätzt 37 %), Holzschlag Hardevust 72 % (79 %),
    /// Kräutergarten am Bach 74 % (76 %), Steinbruch am Felsenhang 88 % (83 %).
    /// </summary>
    internal const double FahrminutenJeKarrenUndJahr = 600;

    /// <summary>Die Strecke jeder freien Liegenschaft (die Karte einer Partie ändert sich nicht; den Belag rechnet <see cref="Stadtwirtschaft.FahrzeitenAuf"/> jedes Mal neu).</summary>
    private readonly Dictionary<string, Fahrweg?> _liegenschaftsWege = new(StringComparer.Ordinal);

    /// <summary>
    /// Wie lange ein Karren der Art <paramref name="karrenTyp"/> von der freien Liegenschaft <paramref name="name"/> zum Markt und zurück führe
    /// (Spielminuten): die Strecke, die der Kern nach der Pacht rechnet (<see cref="Stadtwirtschaft.StreckeWieImBild"/> ab dem Platz der Karte,
    /// <see cref="Stadtwirtschaft.FahrzeitenAuf"/> mit Tempo, Steigung, Belag, Furten, Mindest- und Höchstdauer). Null ohne Fahrzeit nach Weglänge,
    /// ohne Karte, ohne Platz der Liegenschaft oder ohne Weg zu einem Markt.
    /// </summary>
    internal (int Hin, int Zurueck)? FahrzeitDerLiegenschaft(string name, Betriebsart art, string karrenTyp)
    {
        if (_w.Daten.Logistik.Weglaenge is null || _w.Karte is not { } karte || _w.Karrennetz is not { } netz) return null;
        if (!_liegenschaftsWege.TryGetValue(name, out var weg))
        {
            weg = null;
            if (_w.Bauen.PlatzVon(name) is { } platz)
            {
                var (hin, zurueck, markt, punkte) = Stadtwirtschaft.StreckeWieImBild(Karrenweg.Start(platz, art, _w.Daten.Art(art), karte.Katalog), karte, netz);
                if (markt is not null && hin.Count > 0 && zurueck.Count > 0)
                    weg = new Fahrweg(markt, hin, zurueck, punkte ?? Array.Empty<Kartenwerkstatt.Kartenort>(), true);
            }
            _liegenschaftsWege[name] = weg;
        }
        return weg is null ? null : _w.FahrzeitenAuf(weg, art, karrenTyp);
    }

    /// <summary>
    /// Was die KI von einer freien Umland-Liegenschaft erwartet, wenn sie dort Karren der Art mit dem meisten Durchfluss hält (wie beim Karrenkauf):
    ///   • <paramref name="Anteil"/>: welcher Teil der Förderung weggebracht wird (0 … 1), <c>min(1, Karren × Durchfluss × <see cref="FahrminutenJeKarrenUndJahr"/>
    ///     ÷ Förderung im Jahr)</c> mit höchstens <see cref="KiProfil.MaxKarrenJeBetrieb"/> Karren. Durchfluss: Ladung (was der Karren fasst, höchstens
    ///     Lager je Ware × Zahl der Rohstoffe) ÷ Fahrzeit hin und zurück. Förderung: volle Belegung mit der besten Arbeitskraft, die Ware mit dem
    ///     meisten Gewinn je Minute (wie <see cref="Kalkulation.ErwarteterJahresgewinn"/>);
    ///   • <paramref name="Wert"/>: Wert der vollen Förderung im Jahr (Gewinn je Arbeitsminute × Arbeitsminuten aller Plätze);
    ///   • <paramref name="Karren"/> und <paramref name="Typ"/>: so viele Karren dieser Art braucht es dafür (mindestens einer, höchstens die Grenze).
    /// </summary>
    internal readonly record struct Umlandschaetzung(double Anteil, double Wert, int Karren, string Typ);

    /// <summary>Die <see cref="Umlandschaetzung"/> einer freien Liegenschaft – null für Werkstätten, Betriebe ohne Förderung oder ohne Fahrzeit nach Weglänge.</summary>
    internal Umlandschaetzung? UmlandSchaetzen(string name, Betriebsart art, double leistung = 1)
    {
        var d = _w.Daten.Art(art);
        if (d.Ort != Arbeitsort.Umland || d.OhneProduktion || d.Rohstoffe.Count == 0) return null;
        var (fluss, typ) = (0.0, "");
        foreach (var (t, k) in _w.Daten.Logistik.Karren.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (FahrzeitDerLiegenschaft(name, art, t) is not var (hin, zurueck)) return null;
            var f = Math.Min(k.Gesamtkapazitaet, _w.Daten.Logistik.LagerJeWare * d.Rohstoffe.Count) / (double)Math.Max(1, hin + zurueck);
            // bei gleichem Durchfluss (etwa an der Höchstdauer) die billigere Art – wie der Karrenkauf (BesterKarrenplan)
            if (f > fluss + 1e-12 || (Math.Abs(f - fluss) <= 1e-12 && typ != "" && k.Preis < _w.Daten.Logistik.Karren[typ].Preis)) (fluss, typ) = (f, t);
        }
        if (FoerderungUndWert(art, leistung) is not var (stueck, wert) || stueck <= 0 || fluss <= 0) return null;
        var jeKarren = fluss * FahrminutenJeKarrenUndJahr;
        var karren = Math.Clamp((int)Math.Ceiling(stueck / jeKarren - 1e-9), 1, Math.Max(1, Profil.MaxKarrenJeBetrieb));
        return new Umlandschaetzung(Math.Min(1, karren * jeKarren / stueck), wert, karren, typ);
    }

    /// <summary>
    /// Stück im Jahr, die ein voll belegter Umlandbetrieb dieser Art fördert, und ihr Wert (Gewinn je Arbeitsminute × Arbeitsminuten): mit der
    /// besten erlaubten Arbeitskraft und der einträglichsten Ware, wie in <see cref="Kalkulation.ErwarteterJahresgewinn"/>. Null ohne Arbeitskraft.
    /// </summary>
    private (double Stueck, double Wert)? FoerderungUndWert(Betriebsart art, double leistung)
    {
        var daten = _w.Daten;
        var d = daten.Art(art);
        var ware = d.Rohstoffe.OrderByDescending(r => Kalkulation.GewinnJeArbeitsminute(_w, r)).ThenBy(r => r, StringComparer.Ordinal).First();
        var wertJeMinute = Kalkulation.GewinnJeArbeitsminute(_w, ware);
        var minuten = daten.Arbeitsminuten(art);
        var schicht = daten.Schichtfaktor(art);
        var beste = daten.Arbeitskraefte.Where(x => x.Value.IstErlaubt(_w.Statut, d.Ort) && daten.KraftPasst(art, x.Key))
            .OrderByDescending(x => x.Value.TempoAm(d.Ort) * leistung * minuten * wertJeMinute - x.Value.Kosten * schicht - x.Value.Risiko * x.Value.Schaden)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => x.Value)
            .FirstOrDefault();
        if (beste is null) return null;
        var arbeit = daten.Plaetze(art) * beste.TempoAm(d.Ort) * leistung * minuten;
        return (arbeit / daten.Waren[ware].Zeit, arbeit * wertJeMinute);
    }

    /// <summary>
    /// Gewinn und Kosten einer freien Liegenschaft nach dem Weg (<see cref="Expandieren"/>): Für einen Umlandbetrieb zieht die KI vom erwarteten
    /// Jahresgewinn (<paramref name="gewinn"/>, <see cref="Kalkulation.ErwarteterJahresgewinn"/>) ab, was die Karren nicht wegbringen
    /// (<c>(1 − Anteil) × Wert der Förderung</c>), und rechnet die Karren, die sie über den Startkarren hinaus kaufen wird, zu den Kosten
    /// (Preis der Art mit dem meisten Durchfluss). Ohne Schätzung (Werkstatt, alte Daten, kein Weg) unverändert und ohne Karren.
    /// </summary>
    internal (double Gewinn, double Karrenkosten) NachDemWeg(string name, Betriebsart art, double gewinn, double leistung = 1)
    {
        if (UmlandSchaetzen(name, art, leistung) is not { } s) return (gewinn, 0);
        return (gewinn - (1 - s.Anteil) * s.Wert, (s.Karren - 1) * _w.Daten.Logistik.Karren[s.Typ].Preis);
    }

    /// <summary>
    /// Was eine Eigenschaft des Vorarbeiters diesem Betrieb im Jahr einbringt (<see cref="EigenschaftWaehlenNachGewinn"/>). Grundsätzlich die Rechnung
    /// des Kerns (<see cref="Personalwesen.MehrgewinnJeJahr"/>), die mehr Lager nicht bewertet. Ein Umlandbetrieb, der im Vorjahr ab
    /// <see cref="KiProfil.StillstandFuerKarrenkauf"/> stillstand, weil die Karren nicht nachkommen (Fahrzeit nach Weglänge), bewertet sie wie
    /// beim Karrenkauf:
    ///   • mehr Lager je Ware: jeder Karren lädt mehr (Durchfluss mit dem größeren Lager), der Stillstand fällt auf
    ///     <c>vorher ^ (Durchfluss nachher ÷ vorher)</c>, Mehrgewinn = (vorher − nachher) × Wert eines vollen Arbeitsjahres – am Kölner Holzschlag
    ///     (nur Holz, Lager 40) fasst jeder Kriegskarren dann 60 statt 40;
    ///   • alles andere bringt nur für den Teil der Förderung etwas, der auch weggefahren wird: × (1 − Stillstand).
    /// </summary>
    internal double EigenschaftWert(Betrieb b, string kennung)
    {
        var kern = _w.Personal.MehrgewinnJeJahr(b, kennung);
        if (b.IstWerkstatt || b.Arbeitsminuten <= 0 || b.Karren.Count == 0 || !KarrenwahlNachDurchfluss(b)) return kern;
        var vorher = Math.Clamp(b.StillstandMinutenVorjahr / (double)b.Arbeitsminuten, 0, 1);
        if (vorher < Profil.StillstandFuerKarrenkauf) return kern;
        var plus = b.EigenschaftVon(kennung)?.Wirkung.LagerPlus ?? 0;
        if (plus <= 0) return kern * (1 - vorher);
        var jetzt = b.Karren.Sum(k => Durchfluss(b, k.Typ));
        var nachher = b.Karren.Sum(k => Durchfluss(b, k.Typ, b.LagerKapazitaetJeWare + plus));
        if (jetzt <= 0) return kern;
        var jahreswert = Kalkulation.Leistung(_w, b) * b.Arbeitsminuten * WertJeArbeitsminute(b);
        return kern * (1 - vorher) + (vorher - DetMathe.Pow(vorher, nachher / jetzt)) * jahreswert;
    }
}
