using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Stadtkasse, Anträge und Ämter der KI-Häuser (PR #200). Die KI stellt Anträge nach ihren Interessen, stimmt nach ihnen ab und nutzt
/// die Befugnisse ihres Amtes – mit denselben Befehlen und Prognosen wie der Spieler.
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>Ab so viel Umsatzsteuer im Vorjahr beantragt die KI eine niedrigere Steuer.</summary>
    public const double SteuerAntragAb = 400;

    /// <summary>
    /// Stimme zu Steuer, Bau oder Fest:
    ///   • Steuersatz: dafür, wenn er sinkt; dagegen, wenn er steigt – außer die Kasse ist im Minus und die KI hat ein Amt (ihr Gehalt).
    ///   • Bauvorhaben: dafür, wenn es ihr nützt (Hafen: eigene Schiffe; Marktplatz: eigene Werkstätten; Mauer: eigene Betriebe in der Stadt;
    ///     Brunnen: ihre Familie) und die Kasse danach nicht ins Minus fällt.
    ///   • Fest: dafür, wenn sie eine Schänke oder Werkstätten hat und die Kasse nicht ins Minus fällt – nie für das führende Haus
    ///     (das Fest bringt dem Antragsteller Ansehen).
    /// </summary>
    internal bool StimmtFuerRatsantrag(Ratsantrag a)
    {
        var kasse = _w.Kasse;
        var ra = _w.Ratsantraege;
        var nachher = kasse.Stand - ra.Kosten(a);
        switch (a.Art)
        {
            case Antragsart.Steuersatz:
                return a.Prozent < kasse.SteuersatzProzent || kasse.Stand < 0 && _w.Rat.AmtVon(Konto) is not null;
            case Antragsart.Bauvorhaben:
                return nachher >= 0 && ra.BauvorhabenNamens(a.Bauvorhaben) is { } b && Nuetzt(b);
            default:
                return nachher >= 0 && a.Antragsteller != FuehrendesHaus
                       && Betriebe.Any(b => b.IstWerkstatt || b.IstSchaenke);
        }
    }

    /// <summary>Nützt dieses Bauvorhaben dem Haus?</summary>
    private bool Nuetzt(BauvorhabenDaten b) => b.Wirkung switch
    {
        Bauwirkung.Hafen => _w.Schiffahrt.SchiffeVon(Konto).Any(),
        Bauwirkung.Nachfrage => Betriebe.Any(x => x.IstWerkstatt),
        Bauwirkung.Nachtschutz => Betriebe.Any(x => x.InDerStadt),
        _ => _w.Familien.IstAktiv && _w.Familien.Von(Konto) is not null,
    };

    /// <summary>
    /// Einmal am Tag, im Rat (kostet Aktionspunkte – der Kämmerer zahlt für den Steuersatz keine, der Bürgermeister für einen Antrag im Jahr):
    ///   • Steuersatz: als Kämmerer bei leerer Kasse einen Punkt höher; sonst einen Punkt niedriger, wenn das Haus im Vorjahr mindestens
    ///     <see cref="SteuerAntragAb"/> Steuern zahlte.
    ///   • Fest: wenn das Haus sich gerade um ein Amt bewirbt (das Ansehen zählt schon bei der Wahl).
    ///   • Bauvorhaben: das erste, das dem Haus nützt.
    /// Jeweils nur, wenn der Antrag nach der Prognose durchginge.
    /// </summary>
    private void Stadtpolitik()
    {
        var ra = _w.Ratsantraege;
        if (!ra.IstAktiv || _w.Rat.AmtVon(Konto) is null) return;
        var kasse = _w.Kasse;
        var k = kasse.Daten;

        if (ra.Antrag(Antragsart.Steuersatz) is null)
        {
            var satz = kasse.SteuersatzProzent;
            int? neu = _w.Befugnisse.HatAmt(Konto, Amtsbefugnisse.Kaemmerer) && kasse.Stand < 0 && satz < k.SteuerMax ? satz + 1
                : (Konto.Vorjahr?.Steuer ?? 0) >= SteuerAntragAb && satz > k.SteuerMin ? satz - 1
                : null;
            if (neu is { } p && Durchsetzbar(new Ratsantrag(Antragsart.Steuersatz, Konto, Prozent: p)))
                Befehlen(new SteuersatzBeantragen(p), p > satz
                    ? $"beantragt als Kämmerer eine Umsatzsteuer von {p} % – die Stadtkasse ist leer."
                    : $"beantragt eine Umsatzsteuer von {p} % (zahlte im Vorjahr {Zahlenformat.Ganz(Konto.Vorjahr!.Steuer)} Gulden Steuern).");
        }

        if (ra.Antrag(Antragsart.Fest) is null && _w.Rat.Bewerbungen.ContainsKey(Konto)
            && Durchsetzbar(new Ratsantrag(Antragsart.Fest, Konto)))
            Befehlen(new FestBeantragen(), "beantragt ein Fest der Stadt – das Ansehen hilft bei der Wahl.");

        if (ra.Antrag(Antragsart.Bauvorhaben) is null
            && ra.Bauvorhaben.FirstOrDefault(b => Nuetzt(b) && Durchsetzbar(new Ratsantrag(Antragsart.Bauvorhaben, Konto, Bauvorhaben: b.Kennung))) is { } bau)
            Befehlen(new BauvorhabenBeantragen(bau.Kennung), $"beantragt ein Bauvorhaben der Stadt: {bau.Name}.");

        bool Durchsetzbar(Ratsantrag a)
        {
            if (ra.BeantragenVerboten(a) is not null) return false;
            var (ja, nein, _, bm) = ra.Prognose(a);
            return Stadtrat.IstAngenommen(ja, nein, bm);
        }
    }

    /// <summary>
    /// Einmal am Tag: die Befugnisse des eigenen Amtes (keine Aktionspunkte).
    ///   • Stadtrichter: härtere Buße für ein Haus, das ihm geschadet hat; mildere, wenn der Ankläger ihm geschadet hat.
    ///   • Stadthauptmann: die Streife in das Viertel mit den meisten eigenen Betrieben in der Stadt.
    ///   • Gefängniswärter: lässt frei, wer die Kaution zahlen kann – das Geld bekommt er.
    /// </summary>
    private void Amtspolitik()
    {
        var bf = _w.Befugnisse;
        if (!bf.IstAktiv || _w.Rat.AmtVon(Konto) is not { } amt) return;
        switch (amt.Name)
        {
            case Amtsbefugnisse.Stadtrichter when _w.Justiz.IstAktiv:
                var spanne = (int)Math.Round(bf.Daten.RichterSpanne * 100);
                foreach (var v in _w.Justiz.Verfahren.ToList())
                {
                    var ziel = HatGeschadet(v.Beklagter) ? spanne : HatGeschadet(v.Anklaeger) ? -spanne : 0;
                    if (ziel == bf.Strafmass(v.Anklaeger, v.Beklagter) || bf.StrafmassVerboten(Konto, v.Anklaeger, v.Beklagter, ziel) is not null) continue;
                    Befehlen(new StrafmassSetzen(v.Anklaeger.Inhaber, v.Beklagter.Inhaber, ziel),
                        $"setzt als Stadtrichter die Buße für {v.Beklagter.Inhaber} {(ziel > 0 ? "hinauf" : "herab")}.");
                }
                break;
            case Amtsbefugnisse.Stadthauptmann:
                var viertel = Betriebe.Where(b => b.InDerStadt).GroupBy(bf.ViertelVon)
                    .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => (Stadtviertel?)g.Key).FirstOrDefault();
                if (viertel is { } x && bf.Streife != x)
                    Befehlen(new StreifeVerlegen(x), $"schickt als Stadthauptmann die Streife ins {Amtsbefugnisse.ViertelName(x)}.");
                break;
            case Amtsbefugnisse.Gefaengniswaerter when _w.Justiz.IstAktiv:
                foreach (var g in _w.Konten.Where(g => g != Konto && g.IstAktiv && bf.FreilassenVerboten(Konto, g) is null).ToList())
                    Befehlen(new GefangenenFreilassen(g.Inhaber),
                        $"lässt als Gefängniswärter {g.Inhaber} gegen {Zahlenformat.Ganz(bf.Kaution(g))} Gulden Kaution frei.");
                break;
        }
    }
}
