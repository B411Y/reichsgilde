using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Personal der KI-Häuser (PR #224): befördern, wenn sich der Brief bezahlt macht, und die Eigenschaft des Vorarbeiters nach dem Gewinn wählen.
/// Die KI rechnet mit denselben Abfragen wie die Oberfläche (<see cref="Kalkulation.Jahresertrag"/>, <see cref="Personalwesen"/>).
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>
    /// Einmal am Tag (Politikstunde, vor Rat und Hinterzimmer – eine Beförderung kostet einen Aktionspunkt): jeden befördern, der darf und bei dem
    /// sich der Brief in höchstens <see cref="KiProfil.MaxAmortisationJahre"/> Jahren bezahlt macht.
    ///   • zur zweiten Stufe: Mehrertrag = Jahresertrag der nächsten Stufe − Jahresertrag jetzt (Tempo gegen Lohn);
    ///   • zur dritten Stufe: dazu der Erwartungswert der besten von drei gezogenen Eigenschaften (<see cref="Personalwesen.ErwarteterVorarbeiterGewinn"/>)
    ///     – die KI kennt die Ziehung vorher nicht;
    ///   • Nach dem Brief bleibt die halbe Reserve (zur zweiten Stufe) bzw. die ganze (zur dritten) als Bargeld; nicht in der Not.
    /// Danach wählt sie für jeden Vorarbeiter mit offener Wahl die Eigenschaft mit dem höchsten Mehrgewinn (<see cref="EigenschaftWaehlenNachGewinn"/>).
    /// </summary>
    private void Befoerdern()
    {
        var p = _w.Personal;
        if (!p.IstAktiv) return;
        if (!InNot)
            foreach (var b in Betriebe.ToList())
                foreach (var a in b.Arbeiter.OrderByDescending(a => a.Erfahrung).ThenBy(a => a.Nummer).ToList())
                {
                    if (p.BefoerdernVerboten(b, a.Nummer) is not null || p.NaechsteStufe(b, a) is not { } zu || p.BriefKosten(b, a) is not { } brief) continue;
                    var vorarbeiter = p.Stufe(zu) == 3;
                    var mehr = Kalkulation.Jahresertrag(_w, b, zu) - Kalkulation.Jahresertrag(_w, b, a.Arbeitskraft)
                               + (vorarbeiter ? p.ErwarteterVorarbeiterGewinn(b) : 0);
                    if (mehr <= 0 || brief / mehr > Profil.MaxAmortisationJahre) continue;
                    if (Konto.Vermoegen - brief < (vorarbeiter ? Profil.Reserve : Profil.Reserve / 2)) continue;
                    var waehrung = _w.Daten.Allgemein.Waehrung;
                    Befehlen(new ArbeiterBefoerdern(b.Name, a.Nummer),
                        $"befördert {Personalwesen.Wer(a)} in {b.Name} zum {zu} (Brief {Zahlenformat.Ganz(brief)} {waehrung}, " +
                        $"erwartet ≈ {Zahlenformat.Ganz(mehr)} {waehrung} mehr im Jahr).");
                }
        foreach (var b in Betriebe.ToList())
            foreach (var a in b.Arbeiter.Where(a => a.EigenschaftWahl is not null).ToList())
                EigenschaftWaehlenNachGewinn(b, a);
    }

    /// <summary>
    /// Die Eigenschaft mit dem höchsten Mehrgewinn im Jahr (<see cref="Personalwesen.MehrgewinnJeJahr"/>); bei Gleichstand (etwa ohne Gulden-Wirkung)
    /// die erste der Wahl – also die aus der Gruppe Ware.
    /// </summary>
    private void EigenschaftWaehlenNachGewinn(Betrieb b, Arbeiter a)
    {
        if (a.EigenschaftWahl is not { Count: > 0 } wahl) return;
        var p = _w.Personal;
        var beste = wahl.Select((k, i) => (Kennung: k, Wert: p.MehrgewinnJeJahr(b, k), Platz: i))
            .OrderByDescending(x => x.Wert).ThenBy(x => x.Platz).First();
        var name = p.Eigenschaft(b, beste.Kennung)?.Name ?? beste.Kennung;
        Befehlen(new EigenschaftWaehlen(b.Name, a.Nummer, beste.Kennung),
            $"{Personalwesen.Wer(a)} in {b.Name} wird „{name}“ " +
            $"(≈ {Zahlenformat.Ganz(beste.Wert)} {_w.Daten.Allgemein.Waehrung} im Jahr; zur Wahl: {string.Join(", ", wahl.Select(k => p.Eigenschaft(b, k)?.Name ?? k))}).");
    }

    /// <summary>
    /// Zum Jahresende nach der Einrichtung der Betriebe (PR #224): ein Stück fürs Wohnhaus, wenn es sich in der Frist der Einrichtung bezahlt macht
    /// (<see cref="Wohnen.Wohnungswesen.MehrgewinnJeJahr"/>: Vorratskammer nach Haushalt und Unterhalt, Riegel und Hund nach einem Einbruch);
    /// höchstens eins im Jahr, mit derselben Reserve wie die Einrichtung.
    /// </summary>
    private void WohnhausEinrichtungKaufen()
    {
        if (EinrichtungHoechstensJahre is not { } grenze || _w.Wohnen.Daten is not { } d || InNot || (Spart && Profil.Gaunerei <= 0)) return;
        var reserve = _w.Daten.Ki!.Einrichtung!.ReserveFaktor * Profil.Reserve;
        var bestes = d.Einrichtung
            .Where(e => e.Vorhanden is null && _w.Wohnen.EinrichtungVerboten(Konto, e.Kennung) is null && Konto.Vermoegen - e.Preis >= reserve)
            .Select(e => (Stueck: e, Mehr: _w.Wohnen.MehrgewinnJeJahr(Konto, e.Kennung)))
            .Where(x => x.Mehr > 0 && x.Stueck.Preis / x.Mehr <= grenze)
            .OrderBy(x => x.Stueck.Preis / x.Mehr).ThenBy(x => x.Stueck.Kennung, StringComparer.Ordinal)
            .FirstOrDefault();
        if (bestes.Stueck is not { } stueck) return;
        var waehrung = _w.Daten.Allgemein.Waehrung;
        Befehlen(new Wirtschaft.WohnhausEinrichtungKaufen(stueck.Kennung),
            $"kauft fürs Wohnhaus: {stueck.Name} ({Zahlenformat.Ganz(stueck.Preis)} {waehrung}) – erwartet ≈ {Zahlenformat.Ganz(bestes.Mehr)} {waehrung} im Jahr.");
    }
}
