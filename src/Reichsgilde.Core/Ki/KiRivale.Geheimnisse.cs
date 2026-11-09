using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Geheimnisse;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Geheimnisse (Charaktersystem Teil 3c) in den Entscheidungen der KI-Häuser, in der Politikstunde:
///
///   Verschonen  Ein Haus, über das ihr Oberhaupt gut denkt (<c>kiVerschontAb</c>), verschont sie: Sie schweigt.
///   Ratsherr    Fehlt ihr die Stimme eines Ratsherrn bei der eigenen Wahl oder dem eigenen Antrag auf ein Statut, erpresst sie seine Ratsstimme,
///               wenn sie ein Geheimnis von ihm kennt – nie mit ehrlichem Oberhaupt –, und zwar bevor sie Geschenke plant (die erpresste Stimme
///               zählt dann als sicher). Kennt sie keins, setzt sie einen Spitzel auf ihn an – aus dem Budget der Spitzel auf Häuser
///               (höchstens jedes zweite Jahr einer) und nur, wenn die Aktionspunkte für Spitzel und Erpressung reichen; das Wissen nützt im nächsten Jahr.
///   Rivale      Mord zeigt sie immer an. Ehebruch oder Ketzerei zeigt sie an, wenn sie den Inhaber hasst (<c>kiVerhasstAb</c>), bei Ketzerei
///               auch mit frommem Oberhaupt, oder wenn ihr Oberhaupt jähzornig ist. Sonst erpresst sie Gulden, wenn ihr Oberhaupt gierig oder
///               verschlagen ist (<see cref="GeheimnisKiDaten.Anteil"/> vom Bargeld des Opfers, höchstens <see cref="GeheimnisKiDaten.Hoechstens"/>).
///               Hat das Opfer einen Brief nicht bezahlt, macht sie die Drohung wahr: Anzeige, sonst Bloßstellen. Sonst stellt sie den Inhaber
///               bloß, wenn sein Haus ihr bei der Wahl im Weg steht.
///   Opfer       Einen Brief mit einem Geheimnis zahlt sie, wenn der Betrag unter dem erwarteten Schaden liegt (<see cref="GeheimnisSchaden"/>).
/// Aktionspunkte und Stress (KiRivale.Stress.cs) begrenzen wie bei jedem Befehl. Ohne Abschnitt "geheimnisse" ist alles wie vorher.
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>Nach Politik und Anklagen: die Geheimnisse gegen Häuser (anzeigen, erpressen, bloßstellen).</summary>
    private void Geheimnispolitik()
    {
        var gw = _w.Geheimnisse;
        if (!gw.IstAktiv || _w.Familien.Von(Konto) is not { } f) return;
        var ich = f.Oberhaupt;
        foreach (var g in gw.NutzbarFuer(Konto).Where(g => gw.Inhaber(g) is Person).OrderBy(g => g.Nummer).ToList())
        {
            if (g.Oeffentlich || gw.HausVon(g) is not { IstAktiv: true } haus || Verschont(haus)) continue;
            GegenHaus(g, haus, ich);
        }
    }

    private void GegenHaus(Geheimnis g, Konto haus, Person ich)
    {
        var gw = _w.Geheimnisse;
        var d = gw.Daten;
        var name = gw.Inhaber(g)?.Name ?? haus.Inhaber;
        var was = gw.Beschreibung(g);
        var wissen = gw.Wissen(Konto, g);
        var offenerBrief = _w.Gauner.IstAktiv && _w.Gauner.ForderungAn(haus, Konto) is { Geheimnis: int n } && n == g.Nummer;
        if (offenerBrief) return; // erst die Antwort abwarten
        var abgelehnt = wissen?.BriefIm is not null; // ein Brief ging hinaus, das Wissen ist noch da: Das Opfer hat nicht gezahlt

        var anzeigen = g.Art == Geheimnisart.Mord || abgelehnt
                       || g.Art is Geheimnisart.Ehebruch or Geheimnisart.Ketzerei
                       && (Verhasst(haus) || g.Art == Geheimnisart.Ketzerei && ich.HatZug("Fromm") || ich.HatZug("Jaehzornig"));
        var gericht = _w.Justiz.IstAktiv ? _w.Justiz.Gerichtsgebuehr(Konto) + _w.Justiz.Daten.Entschaedigung : 0;
        if (anzeigen && gw.NutzenVerboten(Konto, g, Geheimnisnutzung.Anzeige) is null && Konto.Vermoegen - gericht >= Profil.Reserve
            && Befehlen(new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Anzeige), $"zeigt {name} ({haus.Inhaber}) vor Gericht an: {was}."))
            return;
        if (abgelehnt)
        {
            if (gw.NutzenVerboten(Konto, g, Geheimnisnutzung.Blossstellen) is null)
                Befehlen(new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Blossstellen), $"macht die Drohung wahr und stellt {name} bloß: {was}.");
            return;
        }
        if (ich.HatZug("Gierig") || ich.HatZug("Verschlagen"))
        {
            var betrag = Math.Min(d.Ki.Hoechstens, Math.Round(haus.Vermoegen * d.Ki.Anteil / 50) * 50);
            if (betrag >= 50 && gw.NutzenVerboten(Konto, g, Geheimnisnutzung.Gulden, betrag) is null)
                Befehlen(new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Gulden, betrag),
                    $"schickt {haus.Inhaber} einen Erpresserbrief: {Zahlenformat.Ganz(betrag)} Schweigegeld für „{was}“ ({name}).");
            return;
        }
        if (StehtImWeg(haus) && gw.NutzenVerboten(Konto, g, Geheimnisnutzung.Blossstellen) is null)
            Befehlen(new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Blossstellen), $"stellt {name} vor der Wahl bloß: {was}.");
    }

    /// <summary>Steht dieses Haus der KI bei der Wahl im Weg – bewirbt es sich um dasselbe Amt oder hält es dieses Amt?</summary>
    private bool StehtImWeg(Konto haus)
    {
        var rat = _w.Rat;
        if (!rat.IstAktiv || !rat.Bewerbungen.TryGetValue(Konto, out var amtName) || rat.AmtNamens(amtName) is not { } amt) return false;
        return amt.Konto == haus || rat.Bewerbungen.TryGetValue(haus, out var anderes) && anderes == amtName;
    }

    /// <summary>
    /// Vor dem Werben (eigene Wahl, eigener Statut-Antrag): Ratsherren, deren Stimme fehlt und deren Geheimnis die KI kennt, um die Ratsstimme
    /// erpressen (nie mit ehrlichem Oberhaupt) – nur so viele, wie zur Mehrheit fehlen (jede Erpressung ist eine Spur und kostet Meinung).
    /// Fehlt dann noch eine Stimme ohne bekanntes Geheimnis, vielleicht ein Spitzel auf ihn.
    /// </summary>
    private void RatsstimmenErpressen()
    {
        var gw = _w.Geheimnisse;
        var rat = _w.Rat;
        if (!gw.IstAktiv || !rat.IstAktiv || _w.Familien.Von(Konto) is not { } f || f.Oberhaupt.HatZug("Ehrlich")) return;
        var wahl = EigeneWahl();
        var antrag = rat.Antrag is { } a && a.Antragsteller == Konto ? a.Statut : (Fronstatut?)null;
        var (fehltWahl, fehltAntrag) = (wahl is null ? 0 : FehlendeWahlstimmen(wahl), antrag is { } s ? FehlendeStatutstimmen(s) : 0);
        Ratsherr? ohneWissen = null;
        foreach (var r in rat.Ratsherren.ToList())
        {
            var (fuerWahl, fuerAntrag) = (fehltWahl > 0 && BrauchtWahlstimme(r, wahl), fehltAntrag > 0 && BrauchtStatutstimme(r, antrag));
            if (!fuerWahl && !fuerAntrag) continue;
            var g = gw.Von(r).Where(x => gw.NutzenVerboten(Konto, x, Geheimnisnutzung.Ratsstimme) is null).OrderBy(x => x.Nummer).FirstOrDefault();
            if (g is null)
            {
                if (!gw.Von(r).Any(x => gw.KenntNutzbar(Konto, x))) ohneWissen ??= r;
                continue;
            }
            if (!Befehlen(new GeheimnisNutzen(g.Nummer, Geheimnisnutzung.Ratsstimme),
                    $"erpresst die Ratsstimme von {r.Name} – sie weiß von „{gw.Beschreibung(g)}“.")) continue;
            if (fuerWahl) fehltWahl--;
            if (fuerAntrag) fehltAntrag--;
        }
        if (fehltWahl <= 0 && fehltAntrag <= 0) return; // die Mehrheit steht
        if (ohneWissen is not null && !(fehltWahl > 0 && BrauchtWahlstimme(ohneWissen, wahl) || fehltAntrag > 0 && BrauchtStatutstimme(ohneWissen, antrag)))
            ohneWissen = null;
        // Ein Spitzel auf einen Ratsherrn, dessen Stimme fehlt – aus demselben Budget wie die Spitzel auf Häuser (höchstens jedes zweite Jahr
        // einer, PR #208; der Ratsherr geht vor, weil die Politik vor den Anklagen kommt), nur mit Aktionspunkten für Spitzel und Erpressung.
        if (ohneWissen is null || _spitzelDiesesJahr.Count > 0 || _spitzelVorjahr.Count > 0 || !_w.Justiz.IstAktiv) return;
        var spitzel = new SpitzelAnsetzen(ohneWissen.Kennung);
        if (gw.RatsspitzelVerboten(Konto, ohneWissen) is null && Konto.Vermoegen - _w.Justiz.Daten.SpitzelKosten >= Profil.Reserve
            && Konto.Aktionspunkte.Reicht(_w.ApKosten(Konto, spitzel) + gw.ApKosten(Geheimnisnutzung.Ratsstimme))
            && Befehlen(spitzel, $"setzt einen Spitzel auf {ohneWissen.Name} an – seine Stimme fehlt, vielleicht hat er etwas zu verbergen."))
            _spitzelDiesesJahr.Add(ohneWissen.Kennung);
    }

    /// <summary>Das Amt, um das sich die KI dieses Jahr bewirbt (null: keins, oder sie steht nicht zur Wahl).</summary>
    private Amt? EigeneWahl() =>
        _w.Rat.Bewerbungen.TryGetValue(Konto, out var amtName) && _w.Rat.AmtNamens(amtName) is { } amt && _w.Rat.KandidatenListe(amt).Contains(Konto) ? amt : null;

    /// <summary>Wie viele Stimmen der KI bei ihrer Wahl zur Mehrheit der Wähler fehlen (sichere und erpresste zählen schon).</summary>
    private int FehlendeWahlstimmen(Amt amt)
    {
        var rat = _w.Rat;
        var kandidaten = rat.KandidatenListe(amt);
        var waehler = rat.WahlWaehler(amt, kandidaten);
        var sicher = waehler.Count(s => rat.WahlStimmeVonSitz(s, amt, kandidaten) is { Count: 1 } stimme && stimme[0] == Konto);
        return waehler.Count / 2 + 1 - sicher;
    }

    /// <summary>Wie viele Ja-Stimmen dem eigenen Antrag auf ein Statut zur Mehrheit der besetzten Sitze fehlen (erpresste zählen schon).</summary>
    private int FehlendeStatutstimmen(Fronstatut statut)
    {
        var rat = _w.Rat;
        var besetzt = rat.Aemter.Where(a => a.IstBesetzt).ToList();
        return besetzt.Count / 2 + 1 - besetzt.Count(a => rat.StatutStimmeVonSitz(rat.StimmSitz(a), Konto, statut) == true);
    }

    /// <summary>Er sitzt im Rat, seine Stimme ist nicht schon erpresst, und er wählt bei der eigenen Wahl nicht sicher die KI.</summary>
    private bool BrauchtWahlstimme(Ratsherr r, Amt? amt)
    {
        var rat = _w.Rat;
        if (amt is null || rat.Aemter.FirstOrDefault(a => a.Ratsherr == r) is not { } sitz || rat.Erpresser(sitz) is not null) return false;
        var kandidaten = rat.KandidatenListe(amt);
        return rat.WahlWaehler(amt, kandidaten).Contains(sitz) && rat.WahlStimmeVonSitz(sitz, amt, kandidaten) is var stimme && !(stimme.Count == 1 && stimme[0] == Konto);
    }

    /// <summary>Er sitzt im Rat, seine Stimme ist nicht schon erpresst, und er stimmt beim eigenen Antrag auf ein Statut nicht dafür.</summary>
    private bool BrauchtStatutstimme(Ratsherr r, Fronstatut? statut)
    {
        var rat = _w.Rat;
        if (statut is not { } s || rat.Aemter.FirstOrDefault(a => a.Ratsherr == r) is not { } sitz || rat.Erpresser(sitz) is not null) return false;
        return rat.StatutStimmeVonSitz(sitz, Konto, s) != true;
    }

    /// <summary>
    /// Was ein Geheimnis die KI kostete, wenn der Erpresser es nutzt: das Schlimmere aus Anzeige (Buße, Pranger, Kerker, Amt – nur wenn es ein
    /// Vergehen ist) und Bloßstellen (Ansehen in Gulden über die Jahre der Schande). 0, wenn es das Geheimnis nicht mehr gibt.
    /// </summary>
    public double GeheimnisSchaden(int nummer)
    {
        var gw = _w.Geheimnisse;
        if (!gw.IstAktiv || gw.Nummer(nummer) is not { } g) return 0;
        var a = gw.Daten.Art(g.Art);
        var anzeige = 0.0;
        if (_w.Justiz.IstAktiv && a.Beweiskraft > 0 && (!a.NurMitStadtglaube || _w.Glaube.IstAktiv && _w.Glaube.Stadtglaube is not null))
        {
            var j = _w.Justiz.Daten;
            var deckel = Math.Max(j.Mindestbusse, j.BusseHoechstensAnteil > 0 ? j.BusseHoechstensAnteil * Math.Max(0, Konto.Reinvermoegen) : double.MaxValue); // wie am Gerichtstag
            anzeige = Math.Min(a.Busse, deckel) + PrangerWert
                      + (a.KerkerJahre > 0 ? KerkerWert : 0) + (_w.Rat.AmtVon(Konto) is { } amt ? amt.Daten.Einkommen * NutzenJahre : 0);
        }
        var bloss = a.Ansehen * gw.Daten.Ki.GuldenJeAnsehen * gw.Daten.AnsehenJahre;
        return Math.Max(anzeige, bloss);
    }
}
