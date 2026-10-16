using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
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
            Ausbauen();
            Expandieren();
        }
        if (_stundenImJahr == Politikstunde)
        {
            Unterweltschutz(); // Erpresserbriefe beantworten, Wächter einstellen – kostet keine Aktionspunkte
            Gesundheitspolitik(); // Medicus und Kräuterschnaps – auch aus dem Kerker (der Verwalter sorgt für die Familie)
            Gastwirtschaftspolitik(); // Barden und Würfeltische – Betriebssache, keine AP
            Bankpolitik(); // Kasse füllen, Zins setzen – Betriebssache, keine AP
            if (frei)
            {
                // Die Aktionspunkte reichen nicht für alles: Familie und Verteidigung zuerst, dann nach Charakter –
                // der Wagemutige geht ins Hinterzimmer, bevor er Ratsherren beschenkt; die anderen umgekehrt.
                Familienpolitik();
                Verteidigung();
                Kirchenpolitik(); // Ablass ist Verteidigung
                Wachdienst();     // Razzien der eigenen Wache
                Meisterpruefung();
                if (Profil.Gaunerei >= 2)
                {
                    Diebeszuege(); // die eigenen Leute zuerst – sie kosten kein Geld
                    Gaunerei();
                    Politik();
                    Glaubenspolitik();
                    Anklagen();
                }
                else
                {
                    Politik();
                    Glaubenspolitik();
                    Anklagen();
                    Gaunerei();
                }
                Trainieren();
            }
        }
        foreach (var betrieb in Betriebe.ToList())
        {
            _stundenSeitWechsel[betrieb] = _stundenSeitWechsel.GetValueOrDefault(betrieb, int.MaxValue / 2) + 1;
            if (betrieb.IstWerkstatt)
                ProduktWaehlen(betrieb);
            else if (betrieb.Rohstoffe.Count >= 2)
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
        if (!_w.ApReichen(Konto, befehl)) return false; // keine Aktionspunkte mehr – still verzichten statt Fehlversuch
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
        var (bestes, wert) = betrieb.Artdaten.Produkte
            .Select(x => (Ware: x, Wert: GewinnJeArbeitsminute(x)))
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

        // Beide Rohstoffe (Grube: Eisen und Silber) bringen je Arbeitsminute ähnlich viel; die Preise entscheiden.
        // Drei feste Stufen statt einer gleitenden Zahl – so pendelt die Grube nicht bei jeder kleinen Preisbewegung.
        // Nie ganz auf eine Ware setzen: Ein volles Lager würde die Förderung sonst stoppen.
        var (ersterName, zweiterName) = (betrieb.Rohstoffe[0], betrieb.Rohstoffe[1]);
        var eisen = Math.Max(0.01, GewinnJeArbeitsminute(ersterName));
        var silber = Math.Max(0.01, GewinnJeArbeitsminute(zweiterName));
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
            $"{betrieb.Name} fördert nun zu {Prozent(anteil)} {zweiterName} und {Prozent(1 - anteil)} {ersterName}.");
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
                : _w.Daten.Logistik.Eskorten.Keys.Where(_w.Wache.EskorteVerfuegbar)
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
        if (Konto.Reinvermoegen < neu.Schwelle || Konto.Vermoegen - neu.Gebuehr < Profil.Reserve) return;
        Befehlen(new StandErwerben(),
            $"steigt in den Stand {neu.Name} auf ({Zahlenformat.Ganz(neu.Gebuehr)} {_w.Daten.Allgemein.Waehrung} Gebühr).");
    }

    /// <summary>Mehr Handwerke als dieses erlernt die KI nicht.</summary>
    private const int MaxBerufe = 2;

    /// <summary>Ab diesem Stand führt ein KI-Haus einen Betrieb mehr als <see cref="KiProfil.MaxBetriebe"/>.</summary>
    private const string ReichAbStand = "Patrizier";

    /// <summary>Wie viele Betriebe das Haus führen darf – ein reiches Haus (Patrizier) einen mehr.</summary>
    public int BetriebeHoechstens => Profil.MaxBetriebe + (IstReich ? 1 : 0);

    /// <summary>Ein reiches Haus (Patrizier) darf einen Betrieb mehr führen und ein zweites Handwerk erlernen.</summary>
    private bool IstReich
    {
        get
        {
            var reich = _w.Daten.StandStufe(ReichAbStand);
            return reich >= 0 && Konto.Stand >= reich;
        }
    }

    /// <summary>Führt das Haus eine Werkstatt dieses Handwerks (in der Stadt)?</summary>
    private bool HatWerkstattFuer(string beruf) => Betriebe.Any(b => b.Ort == Arbeitsort.Werkstatt && b.Artdaten.Beruf == beruf);

    /// <summary>Hat das Haus von diesem Handwerk nur den Umlandbetrieb, aber keine Werkstatt?</summary>
    private bool NurUmland(string beruf) => Betriebe.Any(b => b.Artdaten.Beruf == beruf) && !HatWerkstattFuer(beruf);

    /// <summary>Müsste das Haus für diese Liegenschaft erst ein Handwerk erlernen (und darf es das)?</summary>
    private bool NeuerBeruf(string beruf) =>
        _w.Daten.Berufe.Count > 0 && Konto.Berufe.Count > 0 && !Konto.Beherrscht(beruf);

    /// <summary>Den Dieb erlernt nur ein wagemutiges Haus – die anderen fürchten um Ansehen und Ehre.</summary>
    public bool DiebErlaubt => Profil.Gaunerei >= 2;

    /// <summary>
    /// Ein wagemutiges Haus rechnet bei einem Betrieb des Diebes die nächtlichen Züge (Einbruch, Schutzgeld) mit:
    /// Die Taschendiebe allein sind nur ein Teil dessen, was das Schmugglerloch bringt.
    /// </summary>
    private double DiebesZuschlag(Betriebsart art) =>
        _w.Daten.Art(art) is not { OhneProduktion: true } d ? 1
        : DiebErlaubt && _w.Diebe.IstAktiv && d.Beruf == _w.Diebe.Daten.Beruf ? 1.5
        // Ein ehrbares Haus schätzt an der Wache auch Ansehen, Kopfgeld und Geleitgeld – nicht nur den Sold.
        : WacheErlaubt && _w.Wache.IstAktiv && d.Beruf == _w.Wache.Daten.Beruf ? 1.5
        : 1;

    /// <summary>Den Gardisten erlernen nur ehrbare Häuser (vorsichtig, ausgewogen) – ein wagemutiges nie.</summary>
    public bool WacheErlaubt => Profil.Gaunerei <= 1;

    private bool DarfBerufLernen(string beruf) =>
        (!_w.Diebe.IstAktiv || beruf != _w.Diebe.Daten.Beruf || DiebErlaubt)
        && (!_w.Wache.IstAktiv || beruf != _w.Wache.Daten.Beruf || WacheErlaubt)
        && IstReich && Konto.Berufe.Count < MaxBerufe && _w.Daten.Berufe.ContainsKey(beruf) && _w.ApReichen(Konto, new BerufErlernen(beruf));

    /// <summary>
    /// Eine freie Werkstatt kaufen oder einen Umlandbetrieb pachten, wenn es sich rechnet:
    ///   • erwarteter Jahresgewinn positiv; Kaufpreis plus Lehrgeld in höchstens <see cref="KiProfil.MaxAmortisationJahre"/> Jahren verdient,
    ///   • nach Kaufpreis, Lehrgeld, einem Jahr Unterhalt/Pacht und den Löhnen bleibt die Reserve übrig.
    /// Gehört der Betrieb zu einem fremden Handwerk, lernt die KI es vorher (Lehrgeld und AP) – höchstens ein zweites, und erst als Patrizier.
    /// Unter mehreren Kandidaten gewinnt der höchste Gewinn nach Abzug der anteiligen Kosten; das eigene Handwerk
    /// ist damit im Vorteil, weil kein Lehrgeld anfällt.
    /// Hat das Haus von einem Handwerk nur den Umlandbetrieb (z. B. den Bauernhof des Wirts), holt es die Werkstatt dazu nach –
    /// auch über die Höchstzahl an Betrieben hinaus. Sonst lernte die KI den Wirt nur für den Bauernhof und schenkte nie aus.
    /// </summary>
    private void Expandieren()
    {
        var voll = Betriebe.Count() >= BetriebeHoechstens;
        if (voll && !Betriebe.Any(b => b.Ort == Arbeitsort.Umland && !HatWerkstattFuer(b.Artdaten.Beruf))) return;

        var daten = _w.Daten;
        var wahl = _w.FreieLiegenschaften
            .Where(l => Konto.Stand >= daten.StandStufe(l.MindestStand))
            .Where(l => !voll || daten.Art(l.Art).Ort == Arbeitsort.Werkstatt && NurUmland(daten.Art(l.Art).Beruf))
            // Mit dem Völkerbonus: Ein Zwergenhaus sucht eher eine Werkstatt, in der Zwerge gut sind.
            .Select(l => (Liegenschaft: l, Beruf: daten.Art(l.Art).Beruf,
                          Gewinn: Kalkulation.ErwarteterJahresgewinn(_w, l.Art, leistung: _w.VolksFaktor(Konto, daten.Art(l.Art).Beruf)) * DiebesZuschlag(l.Art)))
            .Select(x => (x.Liegenschaft, x.Beruf, x.Gewinn, Lehrgeld: NeuerBeruf(x.Beruf) ? daten.Berufe[x.Beruf].Lehrgeld : -1))
            .Where(x => x.Lehrgeld < 0 || DarfBerufLernen(x.Beruf))
            .Select(x => (x.Liegenschaft, x.Beruf, x.Gewinn, Lehrgeld: Math.Max(0, x.Lehrgeld), Neu: x.Lehrgeld >= 0))
            .Where(x => x.Gewinn > 0 && x.Liegenschaft.Kaufpreis + x.Lehrgeld <= x.Gewinn * Profil.MaxAmortisationJahre)
            .Where(x => Konto.Vermoegen - x.Liegenschaft.Kaufpreis - x.Lehrgeld - daten.Fixkosten(x.Liegenschaft.Art)
                        - daten.Plaetze(x.Liegenschaft.Art) * HoechsterLohn(x.Liegenschaft.Art) >= Profil.Reserve)
            .OrderByDescending(x => x.Gewinn - (x.Liegenschaft.Kaufpreis + x.Lehrgeld) / Profil.MaxAmortisationJahre)
            .ThenBy(x => x.Liegenschaft.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        if (wahl.Liegenschaft is null) return;

        var l = wahl.Liegenschaft;
        var waehrung = daten.Allgemein.Waehrung;
        if (wahl.Neu && !Befehlen(new BerufErlernen(wahl.Beruf),
                $"erlernt das Handwerk der {wahl.Beruf} ({Zahlenformat.Ganz(wahl.Lehrgeld)} {waehrung} Lehrgeld), um {l.Name} zu übernehmen."))
            return;
        Befehlen(new LiegenschaftErwerben(l.Name),
            $"{(l.IstPacht ? "pachtet" : "kauft")} {l.Name} (erwarteter Gewinn ≈ {Zahlenformat.Ganz(wahl.Gewinn)} {waehrung} im Jahr).");
    }

    /// <summary>
    /// Einmal am Tag, für jede eigene Geldleihe:
    ///   • den Zins für Häuser nach Charakter setzen – vorsichtig 10 %, ausgewogen 14 %, wagemutig 25 % (Wucher),
    ///   • die Kasse bis zum Rahmen bei voller Besetzung füllen, solange das Doppelte der Reserve als Bargeld bleibt,
    ///   • wird das Bargeld knapp (unter der halben Reserve), Geld aus der Kasse nehmen.
    /// </summary>
    private void Bankpolitik()
    {
        if (!_w.Bank.IstAktiv) return;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        var zins = Profil.Gaunerei >= 2 ? 0.25 : Profil.Gaunerei >= 1 ? 0.14 : 0.10;
        foreach (var b in Betriebe.Where(_w.Bank.IstBankbetrieb).OrderBy(b => b.Name, StringComparer.Ordinal).ToList())
        {
            if (Math.Abs(_w.Bank.Zinssatz(Konto) - zins) > 1e-9)
                Befehlen(new ZinsSetzen(b.Name, zins), $"verleiht Geld an Häuser zu {Prozent(zins)}.");
            var kasse = _w.Bank.Kasse(b);
            if (Konto.Vermoegen < Profil.Reserve / 2 && kasse > 0)
            {
                var betrag = Math.Min(kasse, Profil.Reserve - Konto.Vermoegen);
                Befehlen(new KasseAbheben(b.Name, betrag), $"nimmt {Zahlenformat.Ganz(betrag)} {waehrung} aus der Kasse von {b.Name}.");
                continue;
            }
            var fehlt = Math.Min(_w.Bank.RahmenVoll(b) - kasse, Konto.Vermoegen - 2 * Profil.Reserve);
            if (fehlt >= 100)
                Befehlen(new KasseEinlegen(b.Name, Math.Floor(fehlt)), $"legt {Zahlenformat.Ganz(fehlt)} {waehrung} in die Kasse von {b.Name}.");
        }
    }

    /// <summary>
    /// Eine eigene Werkstatt zur nächsten Stufe ausbauen, wenn der Meistergrad reicht und es sich rechnet:
    /// Mehrgewinn (volle Belegung, bestes Produkt, heutige Preise) zahlt die Kosten in höchstens
    /// <see cref="KiProfil.MaxAmortisationJahre"/> Jahren zurück, und nach Umbau, Unterhalt und Löhnen bleibt die Reserve.
    /// Ausbauen geht vor Expandieren: Ein Umbau ist billiger als eine zweite Werkstatt.
    /// </summary>
    private void Ausbauen()
    {
        var waehrung = _w.Daten.Allgemein.Waehrung;
        foreach (var b in Betriebe.Where(b => b.InDerStadt).OrderBy(b => b.Name, StringComparer.Ordinal).ToList())
        {
            if (_w.AusbauVerboten(Konto, b) is not null) continue;
            var neu = _w.Daten.Art(b.Artdaten.Ausbau!.Value);
            var kosten = b.Artdaten.AusbauKosten;
            var mehr = Kalkulation.AusbauMehrgewinn(_w, b);
            if (mehr <= 0 || kosten > mehr * Profil.MaxAmortisationJahre) continue;
            var zusatzLohn = (neu.Plaetze - b.Plaetze) * HoechsterLohn(b.Art);
            if (Konto.Vermoegen - kosten - neu.Fixkosten - zusatzLohn < Profil.Reserve) continue;
            Befehlen(new BetriebAusbauen(b.Name),
                $"baut {b.Name} aus – Stufe {neu.Stufe}, {neu.Name} ({Zahlenformat.Ganz(kosten)} {waehrung}, " +
                $"erwartet ≈ {Zahlenformat.Ganz(mehr)} {waehrung} mehr im Jahr).");
            return; // höchstens ein Umbau je Stunde
        }
    }

    /// <summary>
    /// Einmal am Tag, für jede eigene Schänke:
    ///   • einen Barden holen, wenn er mehr einbringt, als er kostet – oder wenn er die Stammtisch-Gunst verdoppelt
    ///     (Ratsherren sind der KI viel wert); nur mit voller Reserve,
    ///   • ein Würfeltisch steht nur bei einem wagemutigen Haus – die anderen fürchten um ihr Ansehen.
    /// </summary>
    private void Gastwirtschaftspolitik()
    {
        var g = _w.Gastwirtschaft;
        if (!g.IstAktiv) return;
        var d = g.Daten;
        foreach (var b in Betriebe.Where(b => b.IstSchaenke).OrderBy(b => b.Name, StringComparer.Ordinal).ToList())
        {
            var tisch = Profil.Gaunerei >= 2 && b.Stufe >= d.Wuerfeltisch.AbStufe;
            if (tisch != g.HatWuerfeltisch(b))
                Befehlen(new WuerfeltischAufstellen(b.Name, tisch),
                    tisch ? $"stellt in {b.Name} einen Würfeltisch auf." : $"baut den Würfeltisch in {b.Name} ab.");

            if (g.BardeGebucht(b) || b.Belegt == 0) continue;
            var lohnt = g.BardeNutzen(b) >= d.Barde.Kosten || (g.HatStammtisch(b) && _w.Rat.IstAktiv);
            if (lohnt && Konto.Vermoegen - d.Barde.Kosten >= Profil.Reserve)
                Befehlen(new BardeEinladen(b.Name),
                    $"holt einen Barden nach {b.Name} ({Zahlenformat.Ganz(d.Barde.Kosten)} {_w.Daten.Allgemein.Waehrung}).");
        }
    }

    /// <summary>
    /// Einmal am Tag, rund um die Kirche:
    ///   • Ablass: Haben andere Beweise für eine eigene Tat, kauft die KI sie in einer fremden Kirche frei – solange die halbe Reserve hält.
        ///   • Wagemutig als Prediger: das Beichtgeheimnis gegen das stärkste Haus brechen, über das er etwas weiß;
    ///     eine Hetzschrift gegen das führende Haus, wenn gerade keine gegen es umgeht.
    /// </summary>
    private void Kirchenpolitik()
    {
        var kw = _w.Kirchenwesen;
        if (!kw.IstAktiv) return;
        var waehrung = _w.Daten.Allgemein.Waehrung;

        if (_w.Justiz.IstAktiv)
        {
            var kirche = kw.Kirchen.Where(b => b.Konto != Konto).OrderBy(b => b.Name, StringComparer.Ordinal).FirstOrDefault();
            while (kirche is not null && kw.AblassFuer(Konto) is { } spur && _w.Justiz.BeweiseFuer(spur) > 0
                   && Konto.Vermoegen - kw.Ablasspreis(spur) >= Profil.Reserve / 2)
                if (!Befehlen(new AblassKaufen(kirche.Name),
                        $"kauft in {kirche.Name} Ablass für „{spur.Beschreibung}“ ({Zahlenformat.Ganz(kw.Ablasspreis(spur))} {waehrung}) – andere hatten Beweise."))
                    break;
        }

        if (Profil.Gaunerei < 2) return;
        var rangliste = _w.Rangliste.Where(k => k != Konto && k.IstAktiv).ToList();
        if (_w.Justiz.IstAktiv && rangliste.FirstOrDefault(k => kw.Beichtwissen(Konto, k).Any()) is { } beichtZiel)
            Befehlen(new BeichtgeheimnisBrechen(beichtZiel.Inhaber),
                $"bricht das Beichtgeheimnis: {kw.Beichtwissen(Konto, beichtZiel).Count()} Beweise gegen {beichtZiel.Inhaber}.");
        if (rangliste.FirstOrDefault() is { } fuehrender && kw.GehetztBis(fuehrender) is null
            && Konto.Vermoegen - kw.Schriftpreis(Konto, kw.Daten.Hetzschrift.Ware) >= Profil.Reserve)
            Befehlen(new HetzschriftVerbreiten(fuehrender.Inhaber), $"lässt eine Hetzschrift gegen {fuehrender.Inhaber} verbreiten.");
    }

    /// <summary>
    /// Einmal am Tag: die nächste Meisterprüfung ablegen, sobald die Erfahrung reicht und die Gebühr die Reserve nicht angreift.
    /// Zuerst im Beruf mit den meisten eigenen Werkstätten – dort bringt der Grad am meisten.
    /// </summary>
    private void Meisterpruefung()
    {
        var reihe = Konto.Berufe
            .OrderByDescending(beruf => Betriebe.Count(b => b.InDerStadt && b.Artdaten.Beruf == beruf))
            .ThenBy(beruf => beruf, StringComparer.Ordinal)
            .ToList();
        foreach (var beruf in reihe)
        {
            if (_w.MeisterpruefungVerboten(Konto, beruf) is not null) continue;
            var p = _w.NaechstePruefung(Konto, beruf)!;
            if (Konto.Vermoegen - p.Gebuehr < Profil.Reserve) continue;
            Befehlen(new MeisterpruefungAblegen(beruf),
                $"legt die Meisterprüfung ab: {beruf} im {Konto.Meistergrad(beruf) + 1}. Grad " +
                $"({Zahlenformat.Ganz(p.Gebuehr)} {_w.Daten.Allgemein.Waehrung}).");
        }
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
        // Reichen die Geschenke dieses Jahr nicht, hilft Moschusduft über das Limit hinaus.
        var duft = _w.Duft;
        if (duft.IstAktiv && buch.Bereitschaft(Konto, k) < buch.Heiratsschwelle(k) && buch.ZuneigungRest(Konto, k) <= 0.001
            && duft.MoschusVerboten(Konto, p.Nummer) is null
            && Konto.Vermoegen - duft.Preis(Konto, duft.Daten.Moschus.Ware) - hochzeit >= Profil.Reserve)
            Befehlen(new MoschusSchenken(p.Nummer), $"schenkt {p.Name} Moschusduft.");

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
    //  Gesundheit
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>So viele Flaschen Kräuterschnaps hält die KI in der Hausapotheke vorrätig.</summary>
    private const int SchnapsVorrat = 2;

    /// <summary>
    /// Einmal im Jahr, früh: Kranke versorgen und die Hausapotheke auffüllen.
    ///   • Pest: Schnaps für jeden Kranken, Medicus für alle, solange die halbe Reserve bleibt.
    ///   • Lebensgefahr (Ruhr, Lungenentzündung): Medicus.
    ///   • Leichte Krankheiten: Schnaps aus der Hausapotheke.
    ///   • Was dem Oberhaupt Talente oder Ansehen kostet und Schnaps nicht heilt: Medicus, wenn die volle Reserve bleibt.
    /// </summary>
    private void Gesundheitspolitik()
    {
        var g = _w.Gesundheit;
        if (!g.IstAktiv || _w.Familien.Von(Konto) is not { } f) return;
        var schnapsPreis = g.SchnapsPreis;
        var fehlt = SchnapsVorrat - g.Apotheke(Konto);
        if (fehlt > 0 && Konto.Vermoegen - fehlt * schnapsPreis >= Profil.Reserve)
            Befehlen(new SchnapsKaufen(fehlt), $"füllt die Hausapotheke auf ({fehlt} × {g.Daten.Schnaps}).");

        foreach (var p in f.Lebende.Where(p => p.Krankheit is not null).OrderBy(p => p == f.Oberhaupt ? 0 : 1).ThenBy(p => p.Nummer).ToList())
        {
            var k = p.Krankheit!;
            var medicus = g.MedicusKosten(k);
            if (k.IstPest)
            {
                if (!k.Schnaps && g.Apotheke(Konto) > 0)
                    Befehlen(new SchnapsGeben(p.Nummer), $"gibt {p.Vorname} gegen die Pest Kräuterschnaps.");
                if (!p.Krankheit!.Medicus && Konto.Vermoegen - medicus >= Profil.Reserve / 2)
                    Befehlen(new MedicusRufen(p.Nummer), $"holt den Medicus zu {p.Vorname} (Pest).");
                continue;
            }
            if (k.NurGegengift)
            {
                // Gift: Der Medicus hilft nicht – nur Gegengift, und es geht um das Leben.
                if (Konto.Vermoegen - g.GegengiftPreis(Konto) >= Profil.Reserve / 4)
                    Befehlen(new GegengiftGeben(p.Nummer), $"gibt {p.Vorname} Gegengift ({k.Name}).");
                continue;
            }
            if (k.Schnapsheilbar && g.Apotheke(Konto) > 0)
            {
                Befehlen(new SchnapsGeben(p.Nummer), $"kuriert {p.Vorname} mit Kräuterschnaps ({k.Name}).");
                continue;
            }
            var lebensgefahr = k.SterbeChance > 0;
            var hindert = p == f.Oberhaupt && (k.AnsehenAbzug > 0 || k.Talentabzug.Values.Any(v => v > 0));
            if ((lebensgefahr && Konto.Vermoegen - medicus >= Profil.Reserve / 2) || (hindert && Konto.Vermoegen - medicus >= Profil.Reserve))
                Befehlen(new MedicusRufen(p.Nummer), $"holt den Medicus zu {p.Vorname} ({k.Name}).");
        }
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
    private void Verteidigung()
    {
        if (!_w.Justiz.IstAktiv) return;
        foreach (var v in _w.Justiz.Verfahren.Where(v => v.Beklagter == Konto).ToList())
            Verteidigen(v);
    }

    private void Anklagen()
    {
        var gericht = _w.Justiz;
        if (!gericht.IstAktiv) return;
        var d = gericht.Daten;

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
        var nutzen = gericht.DrohendeBusse(v.Anklaeger, Konto)
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
        var strafe = gericht.Busse(Konto, beweise) + PrangerWert;
        if (_w.Rat.AmtVon(Konto) is { } amt) strafe += amt.Daten.Einkommen * NutzenJahre;
        if (beweise.Any(b => d.KerkerFuer.Contains(b.Art))) strafe += KerkerWert;
        return strafe;
    }

    /// <summary>Grobe Schätzungen in Gulden: Was kosten drei Jahre Pranger, was ein Jahr Kerker?</summary>
    private const double PrangerWert = 500;
    private const double KerkerWert = 1500;

    /// <summary>Einmal am Tag: Erpresserbriefe beantworten, Wächter einstellen (auch aus dem Kerker – der Verwalter handelt).</summary>
    private void Unterweltschutz()
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

        // 2. Forderungen von Dieben: Lösegeld zahlt jede KI, sobald sie kann (die Familie geht vor);
        //    Schutzgeld nur, wenn es billiger ist als der drohende Brand – sonst ein Wächter für das Brandziel.
        var diebe = _w.Diebe;
        if (diebe.IstAktiv)
            foreach (var brief in diebe.Briefe.Where(b => b.Opfer == Konto).ToList())
            {
                if (brief.Art == Diebesbriefart.Loesegeld)
                {
                    if (Konto.Vermoegen - brief.Betrag >= Profil.Reserve / 4)
                        Befehlen(new DiebesforderungBeantworten(brief.Dieb.Inhaber, brief.Art, true),
                            $"zahlt {brief.Dieb.Inhaber} {Zahlenformat.Ganz(brief.Betrag)} Lösegeld – die Familie geht vor.");
                    continue;
                }
                var brand = diebe.DrohenderBrand(Konto, brief.Dieb);
                var zahlen = brief.Betrag <= brand && Konto.Vermoegen - brief.Betrag >= Profil.Reserve / 2;
                if (!zahlen && diebe.Brandziel(Konto) is { Waechter: false } ziel && Konto.Vermoegen - g.WaechterKosten >= Profil.Reserve / 2)
                    Befehlen(new WaechterSetzen(ziel.Name, true), $"stellt gegen die Drohung von {brief.Dieb.Inhaber} einen Wächter für {ziel.Name} ein.");
                Befehlen(new DiebesforderungBeantworten(brief.Dieb.Inhaber, brief.Art, zahlen), zahlen
                    ? $"zahlt {brief.Dieb.Inhaber} {Zahlenformat.Ganz(brief.Betrag)} Schutzgeld (ein Brand kostete ≈ {Zahlenformat.Ganz(brand)})."
                    : $"weist die Schutzgeldforderung von {brief.Dieb.Inhaber} zurück ({Zahlenformat.Ganz(brief.Betrag)} verlangt, ein Brand kostete ≈ {Zahlenformat.Ganz(brand)}).");
            }

        // 3. Wächter für Betriebe, die in den letzten zwei Jahren angegriffen wurden.
        foreach (var b in Betriebe.Where(b => !b.Waechter && unterwelt.LetzterAngriff(b.Name) >= _w.Jahr - 1).ToList())
            if (Konto.Vermoegen - g.WaechterKosten >= Profil.Reserve)
                Befehlen(new WaechterSetzen(b.Name, true), $"stellt nach dem Angriff einen Wächter für {b.Name} ein.");
    }

    /// <summary>
    /// Selbst Banden anheuern – nur, wer es seinem Charakter nach tut. Unter den Augen der Spitzel hält
    /// der Ausgewogene still; der Wagemutige macht weiter und riskiert Gericht und Kerker.
    /// </summary>
    private void Gaunerei()
    {
        var unterwelt = _w.Gauner;
        if (!unterwelt.IstAktiv) return;
        var g = unterwelt.Daten;
        if (Profil.Gaunerei <= 0 || _gaunereienImJahr >= Profil.Gaunerei) return;
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
        if (beste.Wert <= 0)
        {
            if (Profil.Gaunerei >= 2) Vergiften(ziel); // keine Bande lohnt – dann vielleicht ein Dolch
            return;
        }
        if (Konto.Vermoegen - g.Kosten(beste.Art) < Profil.Reserve) return;
        if (Befehlen(new Beauftragen(ziel.Inhaber, beste.Art, beste.Betrieb),
                $"heuert im Hinterzimmer eine Bande an: {beste.Art.Name()} gegen {ziel.Inhaber}" +
                $"{(beste.Betrieb is null ? "" : $" ({beste.Betrieb})")} – erwarteter Vorteil ≈ {Zahlenformat.Ganz(beste.Wert)}."))
            _gaunereienImJahr++;
    }

    /// <summary>
    /// Wagemutig, wenn sich keine Bande lohnt, höchstens einmal im Jahr (zählt zum Gaunerei-Budget): das führende Haus vergiften,
    /// wenn es mindestens anderthalbmal so reich ist und sein Oberhaupt gesund – mit einem Schattendolch aus dem eigenen Lager,
    /// sonst dem billigsten Dolch vom Markt.
    /// </summary>
    private bool Vergiften(Konto ziel)
    {
        var duft = _w.Duft;
        if (!duft.IstAktiv || duft.AnschlaegeDiesesJahr(Konto) > 0 || ziel.Reinvermoegen < 1.5 * Math.Max(1, Konto.Reinvermoegen)
            || _w.Familien.Von(ziel)?.Oberhaupt is not { Krankheit: null }) return false;
        var dolch = duft.Daten.Gift.Dolche.Keys.Where(d => duft.Preis(Konto, d) == 0).OrderByDescending(d => duft.Daten.Gift.Dolche[d].Chance)
                        .ThenBy(d => d, StringComparer.Ordinal).FirstOrDefault()
                    ?? duft.Daten.Gift.Dolche.OrderBy(d => _w.Markt[d.Key].Preis).ThenBy(d => d.Key, StringComparer.Ordinal).Select(d => d.Key).FirstOrDefault();
        if (dolch is null || duft.GiftVerboten(Konto, ziel, dolch) is not null || Konto.Vermoegen - duft.Preis(Konto, dolch) < Profil.Reserve) return false;
        if (!Befehlen(new Giftanschlag(ziel.Inhaber, dolch), $"lässt heute Nacht {ziel.Inhaber} mit einem {dolch} vergiften.")) return false;
        _gaunereienImJahr++;
        return true;
    }

    /// <summary>
    /// Ein Haus mit eigener Wache macht seine Razzien (2 AP): zuerst bei Häusern mit Dieben, sonst beim Haus,
    /// über dessen Taten die Stadt am meisten weiß (öffentlich bekannte Spuren). Gegen Unbescholtene keine.
    /// </summary>
    private void Wachdienst()
    {
        var wache = _w.Wache;
        if (!wache.IstAktiv || wache.Wachstube(Konto) is null) return;
        var kandidaten = _w.Konten.Where(k => k != Konto && k.IstAktiv)
            .Select(k => (Haus: k, Diebe: _w.Betriebe.Any(b => b.Konto == k && _w.Diebe.IstDiebesbetrieb(b)), Bekannt: _w.Justiz.IstAktiv ? _w.Justiz.BekannteSpuren(k) : 0))
            .Where(x => x.Diebe || x.Bekannt > 0)
            .OrderByDescending(x => x.Diebe).ThenByDescending(x => x.Bekannt).ThenBy(x => x.Haus.Inhaber, StringComparer.Ordinal)
            .ToList();
        foreach (var (haus, diebe, bekannt) in kandidaten)
        {
            if (wache.RazziaVerboten(Konto, haus) is not null) break;
            if (!Befehlen(new Razzia(haus.Inhaber), $"lässt seine Wache bei {haus.Inhaber} eine Razzia machen{(diebe ? " (ein Haus von Dieben)" : $" ({bekannt} bekannte Taten)")}."))
                break;
        }
    }

    /// <summary>
    /// Ein wagemutiges Haus mit eigenen Dieben (Schmugglerloch, Unterschlupf, Diebesgilde), einmal am Tag:
    ///   • Einbruch in den Betrieb, der im Mittel am meisten bringt,
    ///   • Auskundschaften beim führenden Haus (Beweise für eine spätere Erpressung oder Anklage),
    ///   • ab Unterschlupf Schutzgeld vom reichsten Haus – 80 % dessen, was ein Brand dort kostete,
    ///   • in der Diebesgilde eine Entführung beim reichsten Haus mit Angehörigen (10 % seines Vermögens, 500–3.000).
    /// Einbruch, Schutzgeld und Entführung zählen wie Gaunerstücke (höchstens <see cref="KiProfil.Gaunerei"/> im Jahr);
    /// die Zahl der Aufträge je Nacht begrenzt die Stufe, die Aktionspunkte tun ihr Übriges.
    /// </summary>
    private void Diebeszuege()
    {
        var diebe = _w.Diebe;
        if (!diebe.IstAktiv || diebe.Versteck(Konto) is not { } versteck) return;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        var rangliste = _w.Rangliste.Where(k => k != Konto && k.IstAktiv).ToList();
        bool BudgetFrei() => _gaunereienImJahr < Profil.Gaunerei; // Einbruch, Schutzgeld und Entführung zählen wie Gaunerstücke

        if (BudgetFrei() && diebe.Verboten(Konto, Diebesstueck.Einbruch) is null)
        {
            var ziel = _w.Betriebe.Where(b => b.Konto != Konto && b.Konto.IstAktiv)
                .Select(b => (Betrieb: b, Wert: diebe.ErwarteterEinbruch(b, Konto)))
                .Where(x => x.Wert > 150)
                .OrderByDescending(x => x.Wert).ThenBy(x => x.Betrieb.Name, StringComparer.Ordinal).FirstOrDefault();
            if (ziel.Betrieb is not null && Befehlen(new Einbrechen(ziel.Betrieb.Konto.Inhaber, ziel.Betrieb.Name),
                    $"schickt seine Leute aus {versteck.Name} zum Einbruch in {ziel.Betrieb.Name} (erwartet ≈ {Zahlenformat.Ganz(ziel.Wert)} {waehrung})."))
                _gaunereienImJahr++;
        }

        var reichstes = rangliste.OrderByDescending(k => k.Vermoegen).ThenBy(k => k.Inhaber, StringComparer.Ordinal).FirstOrDefault();
        if (BudgetFrei() && reichstes is not null && versteck.Stufe >= diebe.Daten.Schutzgeld.AbStufe && diebe.BriefAn(reichstes, Konto, Diebesbriefart.Schutzgeld) is null)
        {
            var betrag = Math.Max(100, Math.Round(diebe.DrohenderBrand(reichstes, Konto) * 0.8 / 50) * 50);
            if (Befehlen(new SchutzgeldFordern(reichstes.Inhaber, betrag), $"verlangt von {reichstes.Inhaber} {Zahlenformat.Ganz(betrag)} {waehrung} Schutzgeld."))
                _gaunereienImJahr++;
        }

        if (BudgetFrei() && diebe.Verboten(Konto, Diebesstueck.Entfuehrung) is null && !diebe.Briefe.Any(b => b.Dieb == Konto && b.Art == Diebesbriefart.Loesegeld)
            && rangliste.Where(k => diebe.Entfuehrbar(k).Any()).OrderByDescending(k => k.Vermoegen).ThenBy(k => k.Inhaber, StringComparer.Ordinal).FirstOrDefault() is { } opfer)
        {
            var person = diebe.Entfuehrbar(opfer).OrderBy(p => p.Geburtsjahr).ThenBy(p => p.Nummer).First();
            var loesegeld = Math.Clamp(Math.Round(opfer.Vermoegen * 0.1 / 100) * 100, 500, 3000);
            if (Befehlen(new Entfuehren(opfer.Inhaber, person.Nummer, loesegeld),
                    $"lässt heute Nacht {person.Vorname} aus dem Haus {opfer.Inhaber} entführen ({Zahlenformat.Ganz(loesegeld)} {waehrung} Lösegeld)."))
                _gaunereienImJahr++;
        }

        // Ausspähen kostet niemanden etwas – es bereitet Erpressung und Anklage vor.
        if (diebe.Verboten(Konto, Diebesstueck.Auskundschaften) is null && rangliste.FirstOrDefault() is { } fuehrend)
            Befehlen(new Auskundschaften(fuehrend.Inhaber), $"lässt {fuehrend.Inhaber} ausspähen.");
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
        var strafe = gericht.Busse(ziel, beweise) + PrangerWert
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
        {
            // Bewerben lohnt nur, wenn die Aktionspunkte auch für die Geschenke reichen, die die Mehrheit bringen –
            // sonst spart die KI lieber (der Vorrat wächst bis zur Obergrenze).
            var bewerbung = new AmtBewerben(ziel.Name);
            var geschenke = WahlPlan(ziel)?.Count ?? 0;
            if (Konto.Aktionspunkte.Reicht(_w.ApKosten(bewerbung) + geschenke * _w.ApKosten(new Schenken("", 0))))
                Befehlen(bewerbung, $"bewirbt sich um das Amt {ziel.Name} ({Zahlenformat.Ganz(ziel.Daten.Einkommen)} Gulden im Jahr).");
        }

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
    /// Einmal am Tag, rund um den Glauben:
    ///   • Gilt ein fremder Stadtglaube, tritt die KI über – der Glaubenszins kostet jedes Jahr, der Übertritt einmal.
    ///   • Im Rat beantragt sie ihren Glauben als Stadtglauben, wenn er durchginge – sonst wenigstens Glaubensfreiheit.
    ///   • Als Prediger hält sie eine Bekehrungspredigt, wenn sonst Aktionspunkte verfielen.
    /// </summary>
    private void Glaubenspolitik()
    {
        var gw = _w.Glaube;
        if (!gw.IstAktiv || gw.GlaubeVon(Konto) is not { } eigener) return;
        var waehrung = _w.Daten.Allgemein.Waehrung;

        if (gw.Stadtglaube is { } stadt && stadt != eigener && gw.KonvertierenVerboten(Konto, stadt) is null
            && Konto.Vermoegen - gw.Daten.Konvertieren.Gebuehr >= Profil.Reserve)
            if (Befehlen(new Konvertieren(stadt),
                    $"tritt zum {stadt.Glaube()} über – der Stadtglaube kostet sonst {Zahlenformat.Ganz(gw.Daten.Stadtglaube.Abgabe)} {waehrung} Glaubenszins im Jahr."))
                eigener = stadt;

        if (_w.Rat.IstAktiv && _w.Rat.AmtVon(Konto) is not null && gw.Antrag is null && gw.Stadtglaube != eigener && eigener.IstKirchlich())
        {
            if (Durchsetzbar(eigener))
                Befehlen(new StadtglaubeBeantragen(eigener), $"beantragt im Rat den {eigener.Glaube()} als Stadtglauben.");
            else if (gw.Stadtglaube is not null && Durchsetzbar(null))
                Befehlen(new StadtglaubeBeantragen(null), "beantragt im Rat Glaubensfreiheit.");
        }

        var d = _w.Daten.Aktionspunkte;
        if (d is not null && Konto.Aktionspunkte.Vorrat + _w.AktionspunkteJeRunde > d.Hoechstens && gw.BekehrenVerboten(Konto) is null
            && (gw.Stadtglaube is null || gw.Stadtglaube == eigener)) // keine Ketzerpredigt
            Befehlen(new Bekehrungspredigt(), $"hält eine Bekehrungspredigt für den {eigener.Glaube()} (sonst verfielen Aktionspunkte).");

        bool Durchsetzbar(Konfession? glaube)
        {
            var (ja, nein, _, bm) = gw.AbstimmungsPrognose(Konto, glaube);
            return Stadtrat.IstAngenommen(ja, nein, bm);
        }
    }

    /// <summary>
    /// Geschenke für die Wahl: Für jeden wählenden Ratsherrn ausrechnen, wie viel Gunst fehlt, um ihn
    /// umzustimmen. Die billigsten zuerst – aber nur, wenn das Budget für eine Mehrheit reicht.
    /// Sonst wird gar nichts verschenkt (halbe Bestechung ist rausgeworfenes Geld).
    /// </summary>
    private void FuerWahlWerben(Amt amt)
    {
        if (WahlKosten(amt) is { } w)
            Schenkplan(w.Kosten, w.Noetig, $"für die Wahl zum {amt.Name}", amt.Daten.Einkommen * NutzenJahre);
    }

    /// <summary>Die Geschenke, mit denen die KI die Wahl gewänne – null, wenn das nicht geht oder sich nicht lohnt.</summary>
    private List<(Ratsherr Ratsherr, double Gulden)>? WahlPlan(Amt amt) =>
        WahlKosten(amt) is { } w ? Schenkplanen(w.Kosten, w.Noetig, "", amt.Daten.Einkommen * NutzenJahre, melden: false) : null;

    private (List<(Ratsherr Ratsherr, double Gulden)> Kosten, int Noetig)? WahlKosten(Amt amt)
    {
        var rat = _w.Rat;
        var g = rat.Daten.Gunst;
        var mitbewerber = rat.Bewerbungen.Where(b => b.Value == amt.Name && b.Key != Konto).Select(b => b.Key).ToList();
        var waehler = rat.Waehler(amt).Where(w => w.Konto != Konto && !mitbewerber.Contains(w.Konto!)).ToList();
        if (waehler.Count == 0 || g.JeGulden <= 0) return null;

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
        return (kosten, waehler.Count / 2 + 1 - sicher);
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
        if (Schenkplanen(kosten, noetig, zweck, nutzen, melden: true) is not { Count: > 0 } plan) return;
        // Halbe Bestechung ist rausgeworfenes Geld – reichen die Aktionspunkte nicht für alle Geschenke, gar keine.
        var ap = plan.Count * _w.ApKosten(new Schenken("", 0));
        if (!Konto.Aktionspunkte.Reicht(ap))
        {
            Entscheiden($"spart Aktionspunkte: Geschenke {zweck} bräuchten {ap} AP, es sind nur {Konto.Aktionspunkte.Vorrat}.");
            return;
        }
        foreach (var (ratsherr, gulden) in plan)
            Befehlen(new Schenken(ratsherr.Name, gulden),
                $"beschenkt {ratsherr.Name} mit {Zahlenformat.Ganz(gulden)} Gulden {zweck}.");
    }

    /// <summary>Die billigsten Geschenke für eine Mehrheit – leer, wenn keine nötig sind; null, wenn es nicht geht oder sich nicht lohnt.</summary>
    private List<(Ratsherr Ratsherr, double Gulden)>? Schenkplanen(List<(Ratsherr Ratsherr, double Gulden)> kosten, int noetig, string zweck,
        double nutzen, bool melden)
    {
        if (noetig <= 0) return new();
        if (kosten.Count < noetig) return null;
        var plan = kosten.OrderBy(k => k.Gulden).ThenBy(k => k.Ratsherr.Name, StringComparer.Ordinal).Take(noetig).ToList();
        var summe = plan.Sum(p => p.Gulden);
        if (summe > Politikbudget) return null;
        // Ohne Spitzel im Nacken zählt nur das Budget – untere Ämter sind Sprossen auf der Leiter, nicht nur Einkommen.
        // Unter Beobachtung wägt die KI ab: Kosten samt erwarteter Buße gegen den Nutzen einiger Jahre.
        if (Bestechungsrisiko > 1 && summe * Bestechungsrisiko > nutzen)
        {
            if (melden)
                Entscheiden($"verzichtet auf Geschenke {zweck} – unter den Augen der Spitzel zu riskant " +
                            $"({Zahlenformat.Ganz(summe)} {_w.Daten.Allgemein.Waehrung} plus erwartete Buße gegen {Zahlenformat.Ganz(nutzen)} Nutzen).");
            return null;
        }
        return plan;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Training
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktionspunkte, die bei der nächsten Runde verfielen, steckt die KI ins Üben. Sonst spart sie –
    /// Wahlen und Anklagen brauchen mehrere AP auf einmal.
    /// Reihenfolge: Handwerk (Leistung der Betriebe), Verhandeln (Erlöse), Rhetorik (Rat, Gericht);
    /// ein wagemutiges Haus übt zuerst Kampf und Bei Nacht und Nebel.
    /// </summary>
    private void Trainieren()
    {
        if (_w.Daten.Aktionspunkte is not { } d || !_w.Familien.HatTalente) return;
        var wunsch = Profil.Gaunerei >= 2
            ? new[] { Talent.Kampf, Talent.NachtUndNebel, Talent.Handwerk, Talent.Verhandeln, Talent.Rhetorik }
            : new[] { Talent.Handwerk, Talent.Verhandeln, Talent.Rhetorik };
        // Üben wird mit jedem Stern teurer – die KI nimmt das billigste Wunschtalent zuerst.
        var reihe = wunsch.Select((t, i) => (Talent: t, Rang: i))
            .OrderBy(x => _w.ApKosten(Konto, new TalentTrainieren(x.Talent))).ThenBy(x => x.Rang)
            .Select(x => x.Talent).ToList();
        // Erst eine Lehrschrift (1 AP, bis 3 Sterne) – billiger als Üben; aus dem Lager oder, mit doppelter Reserve, vom Markt.
        if (_w.Kirchenwesen.IstAktiv && Konto.Aktionspunkte.Vorrat + _w.AktionspunkteJeRunde > d.Hoechstens)
        {
            var kw = _w.Kirchenwesen;
            var preis = kw.Schriftpreis(Konto, kw.Daten.Lehrschrift);
            if (preis == 0 || Konto.Vermoegen - preis >= 2 * Profil.Reserve)
                foreach (var talent in wunsch)
                    if (kw.LesenVerboten(Konto, talent) is null)
                    {
                        Befehlen(new LehrschriftLesen(talent), $"liest eine Lehrschrift über {talent.Name()} (sonst verfielen Aktionspunkte).");
                        break;
                    }
        }
        foreach (var talent in reihe)
        {
            var ap = Konto.Aktionspunkte.Vorrat;
            if (ap + _w.AktionspunkteJeRunde <= d.Hoechstens) return; // nichts verfiele
            if (_w.Familien.TrainingVerboten(Konto, talent) is not null) continue;
            Befehlen(new TalentTrainieren(talent), $"übt {talent.Name()} (sonst verfielen Aktionspunkte).");
        }
    }

    /// <summary>Vorsichtige Schätzung der Löhne: der teuerste Lohn, den eine erlaubte Arbeitskraft dort kostet.</summary>
    private double HoechsterLohn(Betriebsart art)
    {
        var ort = _w.Daten.Art(art).Ort;
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
        // Ab Stufe 2 braucht die Werkstatt mehr Zutaten-Sorten auf einmal – dann lohnt der Ochsenkarren mit zwei Ladeflächen.
        var typ = betrieb.Ort == Arbeitsort.Werkstatt && betrieb.Stufe < 2 ? "Handkarren" : "Ochsenkarren";
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
