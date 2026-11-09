using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Stress (Charaktersystem Teil 3b) in den Entscheidungen der KI-Häuser:
///
///   Meiden    Ab der ersten Stufe („angespannt“) lässt die KI jeden Befehl, der ihrem Oberhaupt Stress brächte (<see cref="Familienbuch.StressDurch"/>):
///             Ein ehrliches Oberhaupt heuert keine Bande mehr an, ein scheues gibt kein Gastmahl, ein träges übt nicht. Ventile bleiben erlaubt.
///   Ventil    Hat ihr Oberhaupt mindestens <see cref="StressDaten.KiVentilAb"/> Stress und bleibt in der Politikstunde nach allem anderen ein
///             Aktionspunkt übrig, nutzt sie ein Ventil (Wirtshaus, Messe, Jagd) – das mit dem meisten Abbau je AP, also zuerst eines, das ihr
///             Oberhaupt mag; nie eines, das es nicht mag. Mit dem eigenen Ehepartner, wenn auch er gestresst ist (dann sinkt bei beiden der
///             Stress), sonst mit dem Ratsherrn, bei dem die Aktivität die meiste Meinung verspricht. Muss sie ohnehin einen Ratsherrn umwerben,
///             nimmt sie dafür ein Ventil, wenn es eines gibt (<see cref="BesteAktivitaet"/>).
///   Vorsicht  Ab „zermürbt“ rechnet sie vorsichtiger (<see cref="StressStufeDaten.KiVorsicht"/>, 1,2 auf die Raubfurcht). Kredite nimmt die KI
///             ohnehin keine – „nicht mehr auf Pump“ ändert darum nichts.
/// Ohne Abschnitt "stress" ist alles wie vorher.
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>Stress des eigenen Oberhaupts (0 ohne Stress oder Familie).</summary>
    private double OberhauptStress => _w.Familien.Von(Konto) is { } f ? _w.Familien.Stress(f.Oberhaupt) : 0;

    /// <summary>Meidet die KI diesen Befehl, weil er ihrem schon angespannten Oberhaupt Stress brächte?</summary>
    internal bool StressMeiden(Befehl befehl) =>
        _w.Familien.HatStresswesen && _w.Familien.StressStufe(OberhauptStress) >= 1 && _w.Familien.StressDurch(Konto, befehl) > 0;

    /// <summary>Soll die KI jetzt ein Ventil suchen? (Stress des Oberhaupts ab <see cref="StressDaten.KiVentilAb"/>.)</summary>
    private bool SuchtVentil => _w.Familien.StressDaten is { } d && OberhauptStress >= d.KiVentilAb - 1e-9;

    /// <summary>Nach allem anderen in der Politikstunde: ein Ventil mit übrigen Aktionspunkten.</summary>
    private void Ventil()
    {
        var fb = _w.Familien;
        var mw = _w.Meinungen;
        if (!SuchtVentil || !mw.IstAktiv || fb.Von(Konto) is not { } f) return;
        var ich = f.Oberhaupt;
        var partner = new List<ICharakter>();
        if (f.Ehepartner is { Lebt: true } e && fb.Stress(e) >= fb.StressDaten!.KiVentilAb - 1e-9) partner.Add(e);
        else partner.AddRange(_w.Rat.Ratsherren);
        var beste = partner
            .SelectMany(p => mw.Aktivitaeten(Konto, p)
                .Where(a => a.Art != mw.GeschenkArt && a.Sperrgrund is null && Konto.Vermoegen - a.Gulden >= Profil.Reserve)
                .Select(a => (Mit: p, Angebot: a, Abbau: -fb.StressDurch(Konto, new Aktivitaet(a.Art, p.Kennung)))))
            .Where(x => x.Abbau > 0)
            .OrderByDescending(x => x.Abbau / Math.Max(1, x.Angebot.Ap))
            .ThenByDescending(x => x.Angebot.Erwartet)
            .ThenBy(x => x.Angebot.Gulden)
            .ThenBy(x => x.Angebot.Art, StringComparer.Ordinal)
            .ThenBy(x => x.Mit.Kennung, StringComparer.Ordinal)
            .FirstOrDefault();
        if (beste.Angebot is not { } angebot) return;
        Befehlen(new Aktivitaet(angebot.Art, beste.Mit.Kennung), $"{ich.Vorname} sucht Ruhe vom Stress: {angebot.Name} mit {beste.Mit.Name}.");
    }

    /// <summary>Baut diese Aktivität beim eigenen Oberhaupt Stress ab (nur, wenn die KI gerade ein Ventil sucht)?</summary>
    private bool IstVentilFuerMich(Aktivitaetsangebot a, ICharakter mit) =>
        SuchtVentil && _w.Familien.StressDurch(Konto, new Aktivitaet(a.Art, mit.Kennung)) < 0;
}
