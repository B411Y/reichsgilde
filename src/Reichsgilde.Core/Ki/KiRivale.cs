using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
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
/// <param name="MaxBetriebe">Mehr Betriebe erwirbt die KI nicht – sonst bliebe für Mitspieler nichts übrig.</param>
/// <param name="MaxAmortisationJahre">Ein Kauf muss sich in so vielen Jahren aus dem erwarteten Gewinn bezahlt machen.</param>
/// <param name="PolitikAnteil">Höchstens dieser Anteil des Vermögens geht pro Tag in Geschenke an Ratsherren.</param>
/// <param name="StatutVorteil">So viele Gulden im Jahr muss ein anderes Statut mehr bringen, damit die KI es beantragt oder dafür stimmt.</param>
/// <param name="Gaunerei">0 = nie; 1 = selten (ein Diebstahl oder eine Sabotage im Jahr, nur gegen Häuser vor ihr);
/// 2 = oft (zwei Gaunerstücke im Jahr, auch Überfälle und Erpressung, gegen jeden).</param>
public sealed record KiProfil(
    double Reserve = 1500,
    double Wechselschwelle = 0.15,
    int MindestStundenJeProdukt = 6,
    double Vorsicht = 1.0,
    int MaxKarrenJeBetrieb = 3,
    double StillstandFuerKarrenkauf = 0.2,
    int MaxBetriebe = 3,
    double MaxAmortisationJahre = 6,
    double PolitikAnteil = 0.15,
    double StatutVorteil = 200,
    int Gaunerei = 1)
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
///   einmal am Tag       • Politik im Rat, Brautschau, Gericht (Spitzel, Anklage, Verteidigung)
///   zum Jahresende      • Karren kaufen, wenn ein Betrieb zu oft stillstand
///
/// Jede Entscheidung wird als Meldung ausgegeben, damit der Spieler sie nachvollziehen kann.
/// </summary>
public sealed class KiRivale
{
    private readonly Stadtwirtschaft _w;
    private readonly Dictionary<Betrieb, int> _stundenSeitWechsel = new();
    private readonly List<string> _entscheidungen = new();
    private int _stundenImJahr;
    private int _gaunereienImJahr;

    /// <summary>Zu dieser Stunde des Spieltags kümmert sich die KI um die Politik (mittags, einmal am Tag).</summary>
    private const int Politikstunde = 6;
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

    internal RivaleStand Sichern(int kontoNummer) => new()
    {
        Konto = kontoNummer,
        Profil = Profil,
        StundenSeitWechsel = _stundenSeitWechsel.ToDictionary(x => x.Key.Name, x => x.Value),
        Entscheidungen = new(_entscheidungen),
        StundenImJahr = _stundenImJahr,
        GaunereienImJahr = _gaunereienImJahr,
    };

    internal void Wiederherstellen(RivaleStand s)
    {
        foreach (var (name, stunden) in s.StundenSeitWechsel)
            if (_w.Betriebe.FirstOrDefault(b => b.Name == name) is { } betrieb)
                _stundenSeitWechsel[betrieb] = stunden;
        _entscheidungen.AddRange(s.Entscheidungen);
        _stundenImJahr = s.StundenImJahr;
        _gaunereienImJahr = s.GaunereienImJahr;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Ablauf
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Stündliche Überlegungen.</summary>
    internal void Stunde()
    {
        if (!Konto.IstAktiv) return;
        _stundenImJahr++;
        // Im Kerker führt nur der Verwalter die Betriebe – alles Persönliche ruht.
        var frei = !(_w.Justiz.IstAktiv && _w.Justiz.ImKerker(Konto));
        if (frei)
        {
            StandPruefen();
            Expandieren();
        }
        if (_stundenImJahr == Politikstunde)
        {
            Unterweltpolitik(frei); // Erpresserbriefe beantworten, Wächter, eigene Gaunerstücke
            if (frei)
            {
                Familienpolitik();
                Justizpolitik(); // zuerst verteidigen – das Geld für Geschenke ist begrenzt
                Politik();
            }
        }
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
        _stundenImJahr = 0;
        _gaunereienImJahr = 0;
        foreach (var betrieb in Betriebe.ToList())
            KarrenNachkaufen(betrieb);
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

    // Die Rechnungen stehen in Kalkulation – dieselben, die der Spieler im Bedienfeld sieht.
    public double GewinnJeArbeitsminute(string ware) => Kalkulation.GewinnJeArbeitsminute(_w, ware);
    public double WertJeArbeitsminute(Betrieb betrieb) => Kalkulation.WertJeArbeitsminute(_w, betrieb);
    public double Jahresertrag(Betrieb betrieb, string arbeitskraft) => Kalkulation.Jahresertrag(_w, betrieb, arbeitskraft);
    public double ErwarteteFahrtkosten(string eskorte, double ladungswert) =>
        Kalkulation.ErwarteteFahrtkosten(_w, eskorte, ladungswert, Profil.Vorsicht);
    public double Ladungswert(Karren karren) => Kalkulation.Ladungswert(_w, karren);

    /// <summary>
    /// Die KI ändert nichts direkt, sondern schickt Befehle – genau wie der Spieler.
    /// Wird ein Befehl abgelehnt, war die Überlegung falsch; das wird gemeldet statt verschwiegen.
    /// </summary>
    private bool Befehlen(Befehl befehl, string begruendung)
    {
        var ergebnis = _w.Ausfuehren(Konto, befehl);
        Entscheiden(ergebnis.Erfolg ? begruendung : $"wollte, durfte aber nicht: {ergebnis.Meldung}");
        return ergebnis.Erfolg;
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

        _stundenSeitWechsel[betrieb] = 0;
        Befehlen(new ProduktWaehlen(betrieb.Name, bestes),
            $"{betrieb.Name} stellt von {betrieb.Produkt} auf {bestes} um " +
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
        _stundenSeitWechsel[betrieb] = 0;
        Befehlen(new FoerderungAufteilen(betrieb.Name, anteil),
            $"{betrieb.Name} fördert nun zu {Prozent(anteil)} Silber und {Prozent(1 - anteil)} Eisen.");
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

        Befehlen(new PersonalSetzen(betrieb.Name, beste.Kraft, betrieb.Belegung.GetValueOrDefault(beste.Kraft) + anzahl),
            $"{betrieb.Name} stellt {anzahl}× {beste.Kraft} ein " +
            $"(erwarteter Ertrag {Zahlenformat.Ganz(beste.Ertrag)} {_w.Daten.Allgemein.Waehrung} je Kopf und Jahr).");
    }

    private void EskorteWaehlen(Betrieb betrieb)
    {
        for (var nummer = 0; nummer < betrieb.Karren.Count; nummer++)
        {
            var karren = betrieb.Karren[nummer];
            // In der Stadt gibt es keine Überfälle – Eskorte wäre verschenktes Geld.
            var beste = betrieb.Ort == Arbeitsort.Werkstatt
                ? "Keine"
                : _w.Daten.Logistik.Eskorten.Keys
                    .OrderBy(e => ErwarteteFahrtkosten(e, Ladungswert(karren)))
                    .ThenBy(e => _w.Daten.Logistik.Eskorten[e].KostenJeFahrt)
                    .First();
            if (!_w.Daten.Logistik.Eskorten.ContainsKey(beste) || beste == karren.Eskorte)
                continue;
            Befehlen(new EskorteWaehlen(betrieb.Name, nummer, beste),
                $"{karren.Name} von {betrieb.Name} fährt nun mit Eskorte „{beste}“ statt „{karren.Eskorte}“.");
        }
    }

    /// <summary>Aufsteigen, sobald Schwelle erreicht ist und nach der Gebühr die Reserve bleibt.</summary>
    private void StandPruefen()
    {
        if (_w.NaechsterStand(Konto) is not { } neu) return;
        if (Konto.Vermoegen < neu.Schwelle || Konto.Vermoegen - neu.Gebuehr < Profil.Reserve) return;
        Befehlen(new StandErwerben(),
            $"steigt in den Stand {neu.Name} auf ({Zahlenformat.Ganz(neu.Gebuehr)} {_w.Daten.Allgemein.Waehrung} Gebühr).");
    }

    /// <summary>
    /// Eine freie Werkstatt kaufen oder Grube pachten, wenn sie sich rechnet:
    ///   • erwarteter Jahresgewinn positiv und Kaufpreis in höchstens <see cref="KiProfil.MaxAmortisationJahre"/> Jahren verdient,
    ///   • nach Kaufpreis, einem Jahr Unterhalt/Pacht und den Löhnen bleibt die Reserve übrig.
    /// Unter mehreren Kandidaten gewinnt der höchste Gewinn nach Abzug der anteiligen Kaufkosten.
    /// </summary>
    private void Expandieren()
    {
        if (Betriebe.Count() >= Profil.MaxBetriebe) return;

        var daten = _w.Daten;
        var wahl = _w.FreieLiegenschaften
            .Where(l => Konto.Stand >= daten.StandStufe(l.MindestStand))
            .Select(l => (Liegenschaft: l, Gewinn: Kalkulation.ErwarteterJahresgewinn(_w, l.Art)))
            .Where(x => x.Gewinn > 0 && x.Liegenschaft.Kaufpreis <= x.Gewinn * Profil.MaxAmortisationJahre)
            .Where(x => Konto.Vermoegen - x.Liegenschaft.Kaufpreis - daten.Fixkosten(x.Liegenschaft.Art)
                        - daten.Plaetze(x.Liegenschaft.Art) * HoechsterLohn(x.Liegenschaft.Art) >= Profil.Reserve)
            .OrderByDescending(x => x.Gewinn - x.Liegenschaft.Kaufpreis / Profil.MaxAmortisationJahre)
            .ThenBy(x => x.Liegenschaft.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        if (wahl.Liegenschaft is null) return;

        var l = wahl.Liegenschaft;
        Befehlen(new LiegenschaftErwerben(l.Name),
            $"{(l.IstPacht ? "pachtet" : "kauft")} {l.Name} (erwarteter Gewinn ≈ {Zahlenformat.Ganz(wahl.Gewinn)} {daten.Allgemein.Waehrung} im Jahr).");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Familie
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Unter so vielen erwarteten Kindern lohnt eine Ehe für die KI nicht.</summary>
    private const double MindestKinder = 0.5;

    /// <summary>Was die Ehe mit diesem Kandidaten noch kostet: Hochzeit plus die Geschenke, die bis zur Schwelle fehlen.</summary>
    public double Heiratskosten(Kandidat k)
    {
        var buch = _w.Familien;
        var fehlt = Math.Max(0, buch.Heiratsschwelle(k) - buch.Bereitschaft(Konto, k));
        return buch.Hochzeitskosten(k) + fehlt * buch.GuldenJeZuneigung(Konto);
    }

    /// <summary>
    /// Einmal am Tag: Ein lediges Oberhaupt sucht eine Ehe, die Kinder verspricht.
    /// Jeder erlaubte Kandidat wird bewertet mit „erwartete Kinder je Gulden" – dieselbe Rechnung,
    /// die der Spieler in der Familientafel sieht. Geworben wird nur so viel, wie dieses Jahr wirkt,
    /// und nur, wenn danach Hochzeit und Reserve bezahlbar bleiben.
    /// </summary>
    private void Familienpolitik()
    {
        var buch = _w.Familien;
        if (buch.Von(Konto) is not { } f) return;
        Erziehung(f);
        Testament(f);
        if (f.Ehepartner is not null) return;

        var wahl = buch.Kandidaten
            .Where(k => buch.WerbenVerboten(Konto, k) is null)
            .Select(k => (Kandidat: k, Kinder: buch.ErwarteteKinder(f, k.Person), Kosten: Heiratskosten(k)))
            .Where(x => x.Kinder >= MindestKinder)
            .OrderByDescending(x => x.Kinder / Math.Max(1, x.Kosten))
            .ThenBy(x => x.Kandidat.Person.Nummer)
            .FirstOrDefault();
        if (wahl.Kandidat is not { } k) return;

        var p = k.Person;
        var hochzeit = buch.Hochzeitskosten(k);
        var fehlt = buch.Heiratsschwelle(k) - buch.Bereitschaft(Konto, k);
        var wirkung = Math.Min(fehlt, buch.ZuneigungRest(Konto, k));
        if (fehlt > 0 && wirkung > 0.001)
        {
            var gulden = Math.Ceiling(wirkung * buch.GuldenJeZuneigung(Konto) / 10) * 10;
            if (Konto.Vermoegen - gulden - hochzeit < Profil.Reserve) return;
            Befehlen(new Werben(p.Nummer, gulden),
                $"wirbt um {p.Name} ({p.Volk.Einzahl()}, {p.Alter(buch.Jahr)} Jahre) mit Geschenken für {Zahlenformat.Ganz(gulden)} {_w.Daten.Allgemein.Waehrung}.");
        }

        if (buch.Bereitschaft(Konto, k) >= buch.Heiratsschwelle(k) && Konto.Vermoegen - hochzeit >= Profil.Reserve / 2)
            Befehlen(new Heiraten(p.Nummer),
                $"{f.Oberhaupt.Vorname} heiratet {p.Name} (erwartet ≈ {Zahlenformat.Kommazahl(wahl.Kinder)} Kinder).");
    }

    /// <summary>So viele Jahreskosten muss die KI über der Reserve haben, bevor sie ein Kind auf die Universität schickt.</summary>
    private const double UniversitaetPolster = 5;

    /// <summary>
    /// Ausbildung der Kinder – eine feste Hausregel, die sich nicht täglich umentscheidet:
    ///   • Das älteste Kind (der wahrscheinliche Erbe) geht auf die Universität, wenn das Haus Bürger ist und
    ///     sie sich leisten kann – sonst in die Klosterschule (Rhetorik hilft im Rat und vor Gericht).
    ///   • Ein wagemutiges Haus schickt sein jüngstes Kind (ab dem zweiten) auf die Gasse.
    ///   • Alle anderen lernen in der Zunft – Handwerk hebt die Leistung der Betriebe.
    /// Gewählt wird nur für Kinder ohne Ausbildung; einzige Ausnahme ist der Aufstieg von Klosterschule zur Universität.
    /// </summary>
    private void Erziehung(Familie f)
    {
        var buch = _w.Familien;
        if (!buch.HatTalente) return;
        var kinder = f.Kinder.Where(p => p.Lebt && !buch.IstVolljaehrig(p)).ToList();
        var aeltestes = f.Kinder.FirstOrDefault(p => p.Lebt);
        for (var i = 0; i < kinder.Count; i++)
        {
            var kind = kinder[i];
            string wunsch;
            if (kind == aeltestes)
                wunsch = buch.AusbildungVerboten(Konto, "Universität") is null
                         && Konto.Vermoegen - Profil.Reserve >= UniversitaetPolster * Ausbildungskosten("Universität")
                    ? "Universität" : "Klosterschule";
            else if (Profil.Gaunerei >= 2 && i == kinder.Count - 1)
                wunsch = "Gasse";
            else
                wunsch = "Zunftlehre";

            var aufstieg = kind.Ausbildung == "Klosterschule" && wunsch == "Universität";
            if (kind.Ausbildung is not null && !aufstieg) continue;
            if (buch.AusbildungVerboten(Konto, wunsch) is not null) continue; // Weg fehlt in den Daten
            Befehlen(new AusbildungWaehlen(kind.Nummer, wunsch), $"schickt {kind.Vorname} ({kind.Alter(buch.Jahr)} Jahre) in die Ausbildung: {wunsch}.");
        }
    }

    private double Ausbildungskosten(string weg) =>
        _w.Daten.Dynastie.Talente.Ausbildungen.TryGetValue(weg, out var a) ? a.Kosten : 0;

    /// <summary>Testament: Das begabteste volljährige Kind soll erben (bei Gleichstand das ältere).</summary>
    private void Testament(Familie f)
    {
        var buch = _w.Familien;
        if (!buch.HatTalente) return;
        var bester = f.Kinder.Where(p => p.Lebt && buch.IstVolljaehrig(p))
            .OrderByDescending(p => Math.Round(p.TalentSumme, 6))
            .FirstOrDefault();
        if (bester is null || f.Testament == bester) return;
        if (f.Testament is { Lebt: true } alt && buch.IstVolljaehrig(alt) && Math.Round(alt.TalentSumme, 6) >= Math.Round(bester.TalentSumme, 6)) return;
        Befehlen(new ErbenBestimmen(bester.Nummer),
            $"setzt {bester.Vorname} als Erben ein ({TalentTexte.Sterne(bester.TalentSumme)} Sterne in allen Talenten).");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Gericht
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Einmal am Tag:
    ///   • Verteidigen: Droht in einem Verfahren gegen die KI ein Schuldspruch, beschenkt sie die billigsten
    ///     Richter (Ratsherren), bis eine Mehrheit freispräche – aber nur, wenn das Budget dafür reicht.
    ///   • Anklagen: erst, wenn die Beweise so stark sind, dass auch bestochene Richter schuldig sprächen.
    ///   • Spitzel: auf das stärkste andere Haus, das politisch mitspielt (ab Bürger), einmal im Jahr.
    /// </summary>
    private void Justizpolitik()
    {
        var gericht = _w.Justiz;
        if (!gericht.IstAktiv) return;
        var d = gericht.Daten;

        foreach (var v in gericht.Verfahren.Where(v => v.Beklagter == Konto).ToList())
            Verteidigen(v);

        foreach (var gegner in _w.Konten.Where(k => k != Konto && k.IstAktiv).ToList())
        {
            if (gericht.VerfahrenGegen(Konto, gegner) is not null) continue;
            if (_w.Gauner.IstAktiv && _w.Gauner.ForderungAn(gegner, Konto) is not null) continue; // erst die Antwort auf den Brief abwarten
            if (gericht.Beweiskraft(Konto, gegner) < d.SichereBeweiskraft - 1e-9
                || Konto.Vermoegen - d.Gerichtsgebuehr - d.Entschaedigung < Profil.Reserve) continue;
            Befehlen(new Anklagen(gegner.Inhaber),
                $"klagt {gegner.Inhaber} wegen Bestechung an ({gericht.Beweise(Konto, gegner).Count()} Beweise – genug für einen sicheren Schuldspruch).");
        }

        var ziel = _w.Rangliste.FirstOrDefault(k => k != Konto && k.IstAktiv && k.Stand >= 1 && !gericht.HatSpitzel(Konto, k));
        if (ziel is not null && Konto.Vermoegen - d.SpitzelKosten >= Profil.Reserve)
            Befehlen(new SpitzelAnsetzen(ziel.Inhaber), $"setzt einen Spitzel auf {ziel.Inhaber} an ({Zahlenformat.Ganz(d.SpitzelKosten)} {_w.Daten.Allgemein.Waehrung}).");
    }

    private void Verteidigen(Verfahren v)
    {
        var gericht = _w.Justiz;
        var d = gericht.Daten;
        var g = _w.Rat.Daten.Gunst;
        var stimmen = gericht.Prognose(v);
        if (!Stadtgericht.IstSchuldig(stimmen) || d.GunstFaktor <= 0 || g.JeGulden <= 0) return;

        // Wie viel Gunst fehlt einem Richter, bis er freispricht? Die Gunstwirkung muss unter (Schwelle − Beweiskraft) sinken –
        // geht das wegen der Höchstwirkung nicht, ist dieser Richter nicht umzustimmen.
        var kraft = gericht.Beweiskraft(v.Anklaeger, Konto);
        var ziel = d.SchuldSchwelle - kraft - 0.5; // gewünschte Gunstwirkung, etwas unter der Schwelle
        var kosten = new List<(Ratsherr, double)>();
        if (ziel > -d.GunstHoechstwirkung)
            foreach (var s in stimmen.Where(s => s.Schuldig && s.Richter.Ratsherr is not null))
            {
                var r = s.Richter.Ratsherr!;
                // Gunstwirkung = Faktor × Gunstdifferenz + Plädoyer (Rhetorik), gedeckelt
                var differenz = _w.Rat.Gunst(r, v.Anklaeger) - _w.Rat.Gunst(r, Konto);
                var plaedoyer = _w.Familien.Plaedoyer(v.Anklaeger, Konto);
                var fehlt = differenz - (ziel - plaedoyer) / d.GunstFaktor;           // so viel Gunst muss die KI gewinnen
                var gulden = Math.Ceiling(fehlt / g.JeGulden / 10) * 10;
                if (gulden > 0 && _w.Rat.Gunst(r, Konto) + gulden * g.JeGulden <= g.Hoechstwert)
                    kosten.Add((r, gulden));
            }
        var freisprechen = stimmen.Count(s => !s.Schuldig);
        // Ein Freispruch erspart die Buße (auf die schon bekannten Taten) und das Amt.
        var nutzen = Math.Max(d.Mindestbusse, d.BusseFaktor * gericht.Beweise(v.Anklaeger, Konto).Sum(b => b.Betrag))
                     + (_w.Rat.AmtVon(Konto) is { } amt ? amt.Daten.Einkommen * NutzenJahre : 0);
        Schenkplan(kosten, stimmen.Count / 2 + 1 - freisprechen, $"vor dem Gerichtstag (angeklagt von {v.Anklaeger.Inhaber})", nutzen);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Unterwelt
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Was eine Verurteilung kosten würde, wenn <paramref name="anklaeger"/> mit seinen Beweisen klagt:
    /// Buße, drei Jahre Amtseinkommen, Pranger und Kerker (grob in Gulden geschätzt).
    /// </summary>
    public double DrohendeStrafe(Konto anklaeger)
    {
        var gericht = _w.Justiz;
        if (!gericht.IstAktiv) return 0;
        var d = gericht.Daten;
        var beweise = gericht.Beweise(anklaeger, Konto).ToList();
        if (beweise.Count * d.BeweiskraftJeBeweis < d.SchuldSchwelle) return 0; // reicht nicht für einen Schuldspruch
        var strafe = Math.Max(d.Mindestbusse, d.BusseFaktor * beweise.Sum(b => b.Betrag)) + PrangerWert;
        if (_w.Rat.AmtVon(Konto) is { } amt) strafe += amt.Daten.Einkommen * NutzenJahre;
        if (beweise.Any(b => d.KerkerFuer.Contains(b.Art))) strafe += KerkerWert;
        return strafe;
    }

    /// <summary>Grobe Schätzungen in Gulden: Was kosten drei Jahre Pranger, was ein Jahr Kerker?</summary>
    private const double PrangerWert = 500;
    private const double KerkerWert = 1500;

    /// <summary>Einmal am Tag: Erpresserbriefe beantworten, Wächter einstellen, selbst Banden anheuern.</summary>
    private void Unterweltpolitik(bool frei)
    {
        var unterwelt = _w.Gauner;
        if (!unterwelt.IstAktiv) return;
        var g = unterwelt.Daten;

        // 1. Erpresserbriefe: zahlen, wenn das Schweigegeld billiger ist als die drohende Strafe.
        foreach (var f in unterwelt.Forderungen.Where(f => f.Opfer == Konto).ToList())
        {
            var strafe = DrohendeStrafe(f.Erpresser);
            var zahlen = f.Betrag <= strafe && Konto.Vermoegen - f.Betrag >= Profil.Reserve / 2;
            Befehlen(new ErpressungBeantworten(f.Erpresser.Inhaber, zahlen), zahlen
                ? $"zahlt {f.Erpresser.Inhaber} {Zahlenformat.Ganz(f.Betrag)} Schweigegeld (eine Verurteilung kostete ≈ {Zahlenformat.Ganz(strafe)})."
                : $"weist den Erpresserbrief von {f.Erpresser.Inhaber} zurück ({Zahlenformat.Ganz(f.Betrag)} verlangt, die Strafe wäre ≈ {Zahlenformat.Ganz(strafe)}).");
        }

        // 2. Wächter für Betriebe, die in den letzten zwei Jahren angegriffen wurden.
        foreach (var b in Betriebe.Where(b => !b.Waechter && unterwelt.LetzterAngriff(b.Name) >= _w.Jahr - 1).ToList())
            if (Konto.Vermoegen - g.WaechterKosten >= Profil.Reserve)
                Befehlen(new WaechterSetzen(b.Name, true), $"stellt nach dem Angriff einen Wächter für {b.Name} ein.");

        // 3. Selbst Banden anheuern – nur, wer es seinem Charakter nach tut. Unter den Augen der Spitzel hält
        //    der Ausgewogene still; der Wagemutige macht weiter und riskiert Gericht und Kerker.
        if (!frei || Profil.Gaunerei <= 0 || _gaunereienImJahr >= Profil.Gaunerei) return;
        if (Bestechungsrisiko > 1 && Profil.Gaunerei < 2) return;
        var rangliste = _w.Rangliste.Where(k => k.IstAktiv).ToList();
        var eigenerRang = rangliste.IndexOf(Konto);
        var ziele = rangliste.Where(k => k != Konto && (Profil.Gaunerei >= 2 || rangliste.IndexOf(k) < eigenerRang)).ToList();
        if (ziele.Count == 0) return;
        var ziel = ziele[0];

        if (Profil.Gaunerei >= 2 && Erpressen(ziel)) return;

        // Wert je Gaunerstück: Diebstahl = erwarteter Hehlererlös; Sabotage = halber Schaden beim Gegner (er ist Konkurrent);
        // Überfall = erwartete Beute aus der Ware, die auf Abtransport wartet. Jeweils abzüglich der Kosten.
        var moeglich = new List<(Gaunerstueck Art, string? Betrieb, double Wert)>();
        foreach (var b in _w.Betriebe.Where(b => b.Konto == ziel))
        {
            moeglich.Add((Gaunerstueck.Diebstahl, b.Name, unterwelt.ErwarteterGewinn(b, Konto)));
            moeglich.Add((Gaunerstueck.Sabotage, b.Name, unterwelt.ErwarteterSchaden(b, Konto) * 0.5 - g.Sabotage.Kosten));
            if (Profil.Gaunerei >= 2 && b.Ort == Arbeitsort.Umland && b.Karren.Count > 0)
                moeglich.Add((Gaunerstueck.Ueberfall, null,
                    unterwelt.MoeglicheBeute(b).Sum(x => x.Wert) * g.HehlerAnteil * unterwelt.Staerke(Konto) * 0.6 - g.Ueberfall.Kosten));
        }
        var beste = moeglich.Where(m => m.Wert > 0).OrderByDescending(m => m.Wert).ThenBy(m => m.Art).FirstOrDefault();
        if (beste.Wert <= 0 || Konto.Vermoegen - g.Kosten(beste.Art) < Profil.Reserve) return;
        if (Befehlen(new Beauftragen(ziel.Inhaber, beste.Art, beste.Betrieb),
                $"heuert im Hinterzimmer eine Bande an: {beste.Art.Name()} gegen {ziel.Inhaber}" +
                $"{(beste.Betrieb is null ? "" : $" ({beste.Betrieb})")} – erwarteter Vorteil ≈ {Zahlenformat.Ganz(beste.Wert)}."))
            _gaunereienImJahr++;
    }

    /// <summary>Wagemutig: Mit genug Beweisen Schweigegeld verlangen – 60 % dessen, was den Gegner eine Verurteilung kostete.</summary>
    private bool Erpressen(Konto ziel)
    {
        var unterwelt = _w.Gauner;
        var gericht = _w.Justiz;
        if (!gericht.IstAktiv || gericht.Beweise(Konto, ziel).Count() < unterwelt.Daten.ErpressungMindestBeweise
            || unterwelt.ForderungAn(ziel, Konto) is not null || gericht.VerfahrenGegen(Konto, ziel) is not null) return false;
        var d = gericht.Daten;
        var beweise = gericht.Beweise(Konto, ziel).ToList();
        var strafe = Math.Max(d.Mindestbusse, d.BusseFaktor * beweise.Sum(b => b.Betrag)) + PrangerWert
                     + (_w.Rat.AmtVon(ziel) is { } amt ? amt.Daten.Einkommen * NutzenJahre : 0);
        var betrag = Math.Round(strafe * 0.6 / 50) * 50;
        if (Befehlen(new Erpressen(ziel.Inhaber, betrag), $"schickt {ziel.Inhaber} einen Erpresserbrief: {Zahlenformat.Ganz(betrag)} Schweigegeld."))
        {
            _gaunereienImJahr++;
            return true;
        }
        return false;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Politik
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Was die eigenen Betriebe unter einem Statut im Jahr erwarten lassen (volle Belegung, heutige Preise).</summary>
    public double ErwartungUnter(Fronstatut statut) =>
        Betriebe.Sum(b => Kalkulation.ErwarteterJahresgewinn(_w, b.Art, statut));

    /// <summary>Das Statut, das der KI am meisten bringt (bei Gleichstand das geltende).</summary>
    public Fronstatut BevorzugtesStatut => Enum.GetValues<Fronstatut>()
        .OrderByDescending(s => ErwartungUnter(s))
        .ThenBy(s => s == _w.Statut ? 0 : 1)
        .First();

    /// <summary>Stimme im Rat: dafür, wenn das beantragte Statut deutlich mehr bringt als das geltende.</summary>
    internal bool StimmtFuerStatut(Fronstatut statut) =>
        ErwartungUnter(statut) >= ErwartungUnter(_w.Statut) + Profil.StatutVorteil;

    /// <summary>Wie viel die KI heute höchstens für Geschenke ausgibt.</summary>
    private double Politikbudget => Math.Max(0, Math.Min(Konto.Vermoegen - Profil.Reserve, Konto.Vermoegen * Profil.PolitikAnteil));

    /// <summary>Einmal am Tag: bewerben, für die Wahl werben, das Statut betreiben.</summary>
    private void Politik()
    {
        var rat = _w.Rat;
        if (!rat.IstAktiv) return;

        if (!rat.Bewerbungen.ContainsKey(Konto) && Konto.Vermoegen > Profil.Reserve
            && rat.WaehlbareAemter(Konto).OrderByDescending(a => a.Daten.Einkommen).ThenBy(a => a.Name, StringComparer.Ordinal).FirstOrDefault() is { } ziel)
            Befehlen(new AmtBewerben(ziel.Name), $"bewirbt sich um das Amt {ziel.Name} ({Zahlenformat.Ganz(ziel.Daten.Einkommen)} Gulden im Jahr).");

        if (rat.Bewerbungen.TryGetValue(Konto, out var amtName) && rat.AmtNamens(amtName) is { } amt)
            FuerWahlWerben(amt);

        var bevorzugt = BevorzugtesStatut;
        if (rat.AmtVon(Konto) is not null && rat.Antrag is null && bevorzugt != _w.Statut
            && ErwartungUnter(bevorzugt) >= ErwartungUnter(_w.Statut) + Profil.StatutVorteil)
            Befehlen(new StatutBeantragen(bevorzugt),
                $"beantragt im Rat das Statut {bevorzugt.Kuerzel()} – {bevorzugt.Name()} " +
                $"(erwartet {Zahlenformat.MitVorzeichen(ErwartungUnter(bevorzugt) - ErwartungUnter(_w.Statut))} Gulden im Jahr).");

        if (rat.Antrag is { } antrag && antrag.Antragsteller == Konto)
            FuerStatutWerben(antrag.Statut);
    }

    /// <summary>
    /// Geschenke für die Wahl: Für jeden wählenden Ratsherrn ausrechnen, wie viel Gunst fehlt, um ihn
    /// umzustimmen. Die billigsten zuerst – aber nur, wenn das Budget für eine Mehrheit reicht.
    /// Sonst wird gar nichts verschenkt (halbe Bestechung ist rausgeworfenes Geld).
    /// </summary>
    private void FuerWahlWerben(Amt amt)
    {
        var rat = _w.Rat;
        var g = rat.Daten.Gunst;
        var mitbewerber = rat.Bewerbungen.Where(b => b.Value == amt.Name && b.Key != Konto).Select(b => b.Key).ToList();
        var waehler = rat.Waehler(amt).Where(w => w.Konto != Konto && !mitbewerber.Contains(w.Konto!)).ToList();
        if (waehler.Count == 0 || g.JeGulden <= 0) return;

        var kosten = new List<(Ratsherr Ratsherr, double Gulden)>();
        var sicher = 0;
        foreach (var sitz in waehler)
        {
            if (sitz.Ratsherr is not { } r) continue; // Häuser im Rat lassen sich (noch) nicht beschenken
            var gegner = Math.Max(amt.Ratsherr is null && amt.Konto is null ? 0 : rat.Punkte(r, amt, amt.Konto),
                                  mitbewerber.Select(m => rat.Punkte(r, amt, m)).DefaultIfEmpty(0).Max());
            var fehlt = gegner + 1 - rat.Punkte(r, amt, Konto);
            if (fehlt <= 0) sicher++;
            else kosten.Add((r, Math.Ceiling(fehlt / g.JeGulden / 10) * 10));
        }
        Schenkplan(kosten, waehler.Count / 2 + 1 - sicher, $"für die Wahl zum {amt.Name}", amt.Daten.Einkommen * NutzenJahre);
    }

    /// <summary>Geschenke für den eigenen Statut-Antrag: Ratsherren eine Stufe neben dem Ziel über die Gunst-Schwelle heben.</summary>
    private void FuerStatutWerben(Fronstatut statut)
    {
        var rat = _w.Rat;
        var g = rat.Daten.Gunst;
        var besetzt = rat.Aemter.Where(a => a.IstBesetzt).ToList();
        var ja = besetzt.Count(a => a.Konto == Konto || a.Ratsherr is { } r && rat.StimmtZu(r, statut, Konto));
        var kosten = besetzt
            .Where(a => a.Ratsherr is { } r && !rat.StimmtZu(r, statut, Konto) && Math.Abs((int)statut - (int)r.Haltung) == 1)
            .Select(a => (a.Ratsherr!, Math.Ceiling((g.SchwelleNachbarstatut - rat.Gunst(a.Ratsherr!, Konto)) / g.JeGulden / 10) * 10))
            .ToList();
        Schenkplan(kosten, besetzt.Count / 2 + 1 - ja, $"für das Statut {statut.Kuerzel()}",
            (ErwartungUnter(statut) - ErwartungUnter(_w.Statut)) * NutzenJahre);
    }

    /// <summary>So viele Jahre Nutzen setzt die KI gegen die Kosten einer Bestechung.</summary>
    private const double NutzenJahre = 3;

    /// <summary>
    /// Wie teuer eine Bestechung wirklich ist. Nur öffentliches Wissen zählt: Wurde die KI am letzten Gerichtstag
    /// bespitzelt oder steht sie am Pranger, rechnet sie mit Entdeckung – jeder Gulden kostet dann im Mittel
    /// zusätzlich Bußfaktor × Spitzelchance Gulden Buße.
    /// </summary>
    public double Bestechungsrisiko
    {
        get
        {
            var gericht = _w.Justiz;
            if (!gericht.IstAktiv || !gericht.WurdeBeobachtet(Konto) && gericht.Schande(Konto) <= 0) return 1;
            return 1 + gericht.Daten.BusseFaktor * gericht.Daten.SpitzelChance;
        }
    }

    private void Schenkplan(List<(Ratsherr Ratsherr, double Gulden)> kosten, int noetig, string zweck, double nutzen)
    {
        if (noetig <= 0 || kosten.Count < noetig) return;
        var plan = kosten.OrderBy(k => k.Gulden).ThenBy(k => k.Ratsherr.Name, StringComparer.Ordinal).Take(noetig).ToList();
        var summe = plan.Sum(p => p.Gulden);
        if (summe > Politikbudget) return;
        // Ohne Spitzel im Nacken zählt nur das Budget – untere Ämter sind Sprossen auf der Leiter, nicht nur Einkommen.
        // Unter Beobachtung wägt die KI ab: Kosten samt erwarteter Buße gegen den Nutzen einiger Jahre.
        if (Bestechungsrisiko > 1 && summe * Bestechungsrisiko > nutzen)
        {
            Entscheiden($"verzichtet auf Geschenke {zweck} – unter den Augen der Spitzel zu riskant " +
                        $"({Zahlenformat.Ganz(summe)} {_w.Daten.Allgemein.Waehrung} plus erwartete Buße gegen {Zahlenformat.Ganz(nutzen)} Nutzen).");
            return;
        }
        foreach (var (ratsherr, gulden) in plan)
            Befehlen(new Schenken(ratsherr.Name, gulden),
                $"beschenkt {ratsherr.Name} mit {Zahlenformat.Ganz(gulden)} Gulden {zweck}.");
    }

    /// <summary>Vorsichtige Schätzung der Löhne: der teuerste Lohn, den eine erlaubte Arbeitskraft dort kostet.</summary>
    private double HoechsterLohn(Betriebsart art)
    {
        var ort = art == Betriebsart.Mine ? Arbeitsort.Umland : Arbeitsort.Werkstatt;
        return _w.Daten.Arbeitskraefte.Values.Where(k => k.IstErlaubt(_w.Statut, ort)).Select(k => k.Kosten).DefaultIfEmpty(0).Max();
    }

    private void KarrenNachkaufen(Betrieb betrieb)
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

        if (Befehlen(new KarrenKaufen(betrieb.Name, typ),
                $"{betrieb.Name} stand {Prozent(anteil)} der Arbeitszeit still – {typ} gekauft " +
                $"({Zahlenformat.Ganz(daten.Preis)} {_w.Daten.Allgemein.Waehrung})."))
            EskorteWaehlen(betrieb);
    }
}
