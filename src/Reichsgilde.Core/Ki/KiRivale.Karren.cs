using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Karren verkaufen (PR #205): Die KI gibt einen Karren ab, wenn sie knapp bei Kasse ist oder ein Karren ein ganzes Jahr nicht gefahren ist.
/// Den letzten Karren eines Betriebs, der Ware fährt, verkauft sie nie – nur Wache und Geldleihe brauchen keinen.
///
/// Karren wählen nach Durchfluss (phase5/gehtempo, Befund H1 der Prüfung): Seit der Kern die Fahrzeit nach Weglänge und Karrenart rechnet
/// (Datenversion 65; Ochse 0,8, Handkarren 1,0, Pferd 1,3 m/s), kauft die KI die Art, die auf dem echten Weg am meisten bringt, und tauscht an der
/// Grenze von <see cref="KiProfil.MaxKarrenJeBetrieb"/> Karren einen langsamen gegen einen schnelleren (<see cref="KarrenNachDurchfluss"/>).
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>Braucht dieser Betrieb Karren? Alle außer Wache und Geldleihe (die haben nichts zu fahren).</summary>
    private bool BrauchtKarren(Betrieb b) => !_w.Wache.IstWachbetrieb(b) && !_w.Bank.IstBankbetrieb(b);

    /// <summary>Die Karren dieses Betriebs, die die KI hergeben darf (am Betrieb, und nicht der letzte eines Betriebs, der ihn braucht).</summary>
    private IEnumerable<(int Nummer, Karren Karren)> Verkaeufliche(Betrieb b) =>
        b.Karren.Count <= (BrauchtKarren(b) ? 1 : 0)
            ? Enumerable.Empty<(int, Karren)>()
            : b.Karren.Select((k, i) => (i, k)).Where(x => _w.KarrenVerkaufVerboten(Konto, b, x.Item1) is null);

    /// <summary>
    /// Jede Stunde, auch aus dem Kerker (Betriebssache), höchstens ein Karren: Ist die KI in Not (<see cref="InNot"/>: der Kämmerer hat gemahnt,
    /// und nach den offenen Jahreskosten bleibt nichts), verkauft sie den Karren, der am meisten bringt – vor einem ganzen Betrieb.
    /// Steht er gerade nicht am Betrieb, versucht sie es in der nächsten Stunde.
    /// </summary>
    private void KarrenNotverkauf()
    {
        if (!InNot) return;
        var wahl = Betriebe
            .SelectMany(b => Verkaeufliche(b).Select(x => (Betrieb: b, x.Nummer, x.Karren)))
            .OrderByDescending(x => _w.KarrenErloes(x.Karren))
            .ThenBy(x => x.Betrieb.Name, StringComparer.Ordinal)
            .ThenByDescending(x => x.Nummer) // der zuletzt gekaufte zuerst
            .FirstOrDefault();
        if (wahl.Betrieb is null) return;
        Befehlen(new KarrenVerkaufen(wahl.Betrieb.Name, wahl.Nummer),
            $"verkauft {wahl.Karren.Name} von {wahl.Betrieb.Name} ({Zahlenformat.Ganz(_w.KarrenErloes(wahl.Karren))} {_w.Daten.Allgemein.Waehrung}) – " +
            "das Geld ist knapp, der Kämmerer hat gemahnt.");
    }

    /// <summary>
    /// Zum Jahresende, vor dem Karrenkauf: Ein Karren, der im ganzen Vorjahr keine Fahrt gemacht hat, wird verkauft – je Betrieb höchstens einer,
    /// und nur, wenn der Betrieb nicht so oft stillstand, dass die KI gleich wieder einen kaufen würde (<see cref="KiProfil.StillstandFuerKarrenkauf"/>).
    /// </summary>
    private void UngenutzteKarrenVerkaufen(Betrieb betrieb)
    {
        var anteil = betrieb.StillstandMinutenVorjahr / (double)betrieb.Arbeitsminuten;
        if (anteil >= Profil.StillstandFuerKarrenkauf) return;
        var wahl = Verkaeufliche(betrieb).Where(x => x.Karren.FahrtenVorjahr == 0).OrderByDescending(x => x.Nummer).FirstOrDefault();
        if (wahl.Karren is null) return;
        Befehlen(new KarrenVerkaufen(betrieb.Name, wahl.Nummer),
            $"verkauft {wahl.Karren.Name} von {betrieb.Name} ({Zahlenformat.Ganz(_w.KarrenErloes(wahl.Karren))} {_w.Daten.Allgemein.Waehrung}) – " +
            "er ist das ganze Jahr nicht gefahren.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Karren wählen nach Durchfluss (Fahrzeit nach Weglänge)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ein Plan für die Karren eines Betriebs: einen Karren der Art <paramref name="Typ"/> kaufen (<paramref name="Tausch"/> null) oder den Karren
    /// Nr. <paramref name="Tausch"/> (ab 0) verkaufen und dafür einen kaufen.
    /// </summary>
    /// <param name="Kosten">Preis des neuen Karrens, beim Tausch abzüglich des Erlöses für den alten.</param>
    /// <param name="StillstandNachher">Erwarteter Anteil Stillstand an der Arbeitszeit danach (0,3 = 30 %).</param>
    /// <param name="Mehrgewinn">Erwartet mehr im Jahr: (Stillstand vorher − nachher) × Wert eines vollen Arbeitsjahres.</param>
    internal readonly record struct Karrenplan(string Typ, int? Tausch, double Kosten, double StillstandNachher, double Mehrgewinn);

    /// <summary>
    /// Rechnet der Kern die Fahrzeit dieses Betriebs nach Weglänge (Abschnitt <c>logistik.weglaenge</c>, Datenversion 65, und ein Weg zum Markt)?
    /// Nur dann fahren die Karrenarten verschieden lang. Sonst (keine Karte, kein Weg, alte Daten) gilt die feste Fahrzeit für alle Arten,
    /// und die KI kauft wie bisher: Handkarren für eine Werkstatt unter Stufe 2, sonst Ochsenkarren, ohne Tausch.
    /// </summary>
    internal bool KarrenwahlNachDurchfluss(Betrieb b) =>
        _w.Daten.Logistik.Weglaenge is not null && _w.Fahrten(b) is var (hin, zurueck) && hin.Count > 0 && zurueck.Count > 0;

    /// <summary>
    /// Wie viele Stück ein Karren dieser Art je Minute seiner Fahrt bewegt: Ladung ÷ (Fahrzeit hin + zurück). Die Fahrzeit ist die des Kerns
    /// (<see cref="Stadtwirtschaft.Fahrzeit(Betrieb, string, bool)"/>: Weg, Tempo der Art, Steigung, Belag samt Wegebau, Furten, Mindest- und
    /// Höchstdauer). Ladung: was der Karren fasst, aber höchstens, was das Lager hergibt – Lager je Ware × Zahl der Waren (Umland: seine
    /// Rohstoffe; Werkstatt: Waren in Arbeit oder Zutaten-Sorten, was mehr ist). Ein Holzschlag (nur Holz, Lager 40) füllt den Kriegskarren
    /// (75) also nie; ein Bauernhof mit Gerste und Roggen schon.
    /// </summary>
    internal double Durchfluss(Betrieb b, string typ)
    {
        var daten = _w.Daten.Logistik.Karren[typ];
        var minuten = _w.Fahrzeit(b, typ, zumMarkt: true) + _w.Fahrzeit(b, typ, zumMarkt: false);
        var waren = Math.Max(1, b.IstWerkstatt ? Math.Max(b.WarenInArbeit.Count, b.Zutaten.Count) : b.Rohstoffe.Count);
        return Math.Min(daten.Gesamtkapazitaet, b.LagerKapazitaetJeWare * waren) / (double)Math.Max(1, minuten);
    }

    /// <summary>
    /// Was die KI für einen Betrieb erwägt, der im Vorjahr den Anteil <paramref name="stillstand"/> der Arbeitszeit stillstand:
    ///   • unter der Grenze von <see cref="KiProfil.MaxKarrenJeBetrieb"/> Karren je Art einen Kauf;
    ///   • an der Grenze je verkäuflichem Karren und jeder Art mit mehr Durchfluss einen Tausch (Preis − Erlös des alten).
    /// Die Wirkung schätzt sie aus dem Vorjahr: <c>Stillstand nachher = Stillstand vorher ^ (Durchfluss nachher ÷ Durchfluss vorher)</c> –
    /// jeder zusätzliche Durchfluss drückt den Stillstand um denselben Faktor (auf den Kölner Umlandbetrieben nachgemessen: zwei Ochsen 50 %,
    /// drei Ochsen geschätzt 35 % und gemessen 32–38 %; Ochse und zwei Pferde geschätzt 23 %, gemessen 22 %). Ein linearer Ansatz versprach für
    /// einen zweiten Karren derselben Art zu viel. Mehrgewinn = (vorher − nachher) × Wert eines vollen Arbeitsjahres (Leistung × Arbeitsminuten ×
    /// <see cref="Kalkulation.WertJeArbeitsminute"/>; Lohn und Unterhalt fallen ohnehin an). Kosten je Fahrt (Eskorte) zählen nicht: Ein Karren
    /// fährt zwei bis drei Mal im Jahr, das sind wenige Dutzend Gulden.
    /// </summary>
    internal IEnumerable<Karrenplan> Karrenplaene(Betrieb b, double stillstand)
    {
        var vorher = Math.Clamp(stillstand, 0, 1);
        var jahreswert = Kalkulation.Leistung(_w, b) * b.Arbeitsminuten * WertJeArbeitsminute(b);
        var jetzt = b.Karren.Sum(k => Durchfluss(b, k.Typ));
        double Nachher(double fluss) => jetzt <= 0 ? 0 : DetMathe.Pow(vorher, fluss / jetzt); // DetMathe: auf jedem Rechner bitgleich (Mehrspieler)
        Karrenplan Plan(string typ, int? tausch, double kosten, double fluss)
        {
            var nachher = Nachher(fluss);
            return new Karrenplan(typ, tausch, kosten, nachher, (vorher - nachher) * jahreswert);
        }
        var arten = _w.Daten.Logistik.Karren.OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
        if (b.Karren.Count < Profil.MaxKarrenJeBetrieb)
        {
            foreach (var (typ, d) in arten)
                yield return Plan(typ, null, d.Preis, jetzt + Durchfluss(b, typ));
            yield break;
        }
        foreach (var (nummer, alt) in Verkaeufliche(b))
        {
            var weg = Durchfluss(b, alt.Typ);
            foreach (var (typ, d) in arten)
                if (Durchfluss(b, typ) is var neu && neu > weg + 1e-12)
                    yield return Plan(typ, nummer, d.Preis - _w.KarrenErloes(alt), jetzt - weg + neu);
        }
    }

    /// <summary>
    /// Der Plan mit dem größten Wert im Jahr: Mehrgewinn − Kosten ÷ <see cref="Amortisationsjahre"/> (wie bei Kauf und Bau eines Betriebs). Er muss
    /// sich in dieser Frist bezahlen, und danach bleibt die Reserve. Nach dem Wert, nicht nach Durchfluss je Gulden: Der billige Handkarren
    /// brächte je Gulden oft am meisten, belegte aber einen der drei Plätze (Greybox, immer Handkarren: KI-Häuser bei „Schwer“ −10 %).
    /// Gleichstand: der billigere, dann nach Art und Nummer. Null: nichts lohnt sich.
    /// </summary>
    internal Karrenplan? BesterKarrenplan(Betrieb b, double stillstand)
    {
        var jahre = Amortisationsjahre;
        foreach (var p in Karrenplaene(b, stillstand)
                     .Where(p => p.Mehrgewinn > 0 && p.Kosten <= p.Mehrgewinn * jahre)
                     .Where(p => Konto.Vermoegen - p.Kosten >= Profil.Reserve
                                 && Konto.Vermoegen + (p.Tausch is { } nr ? _w.KarrenErloes(b.Karren[nr]) : 0) >= _w.Daten.Logistik.Karren[p.Typ].Preis)
                     .OrderByDescending(p => p.Mehrgewinn - p.Kosten / jahre)
                     .ThenBy(p => p.Kosten)
                     .ThenBy(p => p.Typ, StringComparer.Ordinal)
                     .ThenBy(p => p.Tausch ?? -1))
            return p;
        return null;
    }

    /// <summary>Den besten Plan ausführen (<see cref="BesterKarrenplan"/>): beim Tausch erst den alten Karren verkaufen, dann den neuen kaufen und die Eskorte wählen.</summary>
    internal void KarrenNachDurchfluss(Betrieb b, double stillstand)
    {
        if (BesterKarrenplan(b, stillstand) is not { } plan) return;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        var preis = _w.Daten.Logistik.Karren[plan.Typ].Preis;
        var wirkung = $"Stillstand ≈ {Prozent(plan.StillstandNachher)} statt {Prozent(stillstand)}, ≈ {Zahlenformat.Ganz(plan.Mehrgewinn)} {waehrung} mehr im Jahr";
        string text;
        if (plan.Tausch is { } nr)
        {
            var alt = b.Karren[nr];
            var erloes = _w.KarrenErloes(alt);
            if (!Befehlen(new KarrenVerkaufen(b.Name, nr),
                    $"{b.Name} stand {Prozent(stillstand)} der Arbeitszeit still – verkauft {alt.Name} ({Zahlenformat.Ganz(erloes)} {waehrung}) für einen {plan.Typ}."))
                return;
            text = $"{b.Name} stand {Prozent(stillstand)} der Arbeitszeit still – tauscht einen {alt.Typ} gegen einen {plan.Typ} " +
                   $"({Zahlenformat.Ganz(preis)} {waehrung}; {wirkung}).";
        }
        else
            text = $"{b.Name} stand {Prozent(stillstand)} der Arbeitszeit still – {plan.Typ} gekauft ({Zahlenformat.Ganz(preis)} {waehrung}; {wirkung}).";
        if (Befehlen(new KarrenKaufen(b.Name, plan.Typ), text))
            EskorteWaehlen(b);
    }
}
