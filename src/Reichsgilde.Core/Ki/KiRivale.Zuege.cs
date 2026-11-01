namespace Reichsgilde.Core.Ki;

/// <summary>
/// Die Charakterzüge des Oberhaupts (PR #227) verschieben, wie das KI-Haus handelt – der Charakter im <see cref="KiProfil"/> bleibt.
/// Stirbt das Oberhaupt, handelt das Haus nach den Zügen des Erben. Ohne Züge (alte Daten) ist alles wie vorher.
///
///   Gierig / Großzügig      Zins der Geldleihe ±, Geschenke an den Rat ×0,7 / ×1,3, Großzügig holt Barden
///   Fleißig / Träge          stellt schneller / seltener auf ein besseres Produkt um (Schalter „ueben“ ungenutzt)
///   Fromm / Zynisch          Ablass mit halbem / doppeltem Polster, Fromm tritt nie zum Stadtglauben über
///   Mutig / Feige            Eskorte nach Raubfurcht ×0,85 / ×1,5; Feige: Amortisation 1 Jahr kürzer
///   Ehrlich / Verschlagen    ein bezahltes Gaunerstück weniger (nie unter eins, keine Richter bestechen) / mehr im Jahr
///   Geduldig / Jähzornig     Produktwechsel ×1,3 / ×0,7, Klage erst mit einem Beweis mehr / schon mit einem weniger
///   Gesellig / Scheu         Geschenke ×1,2 / ×0,8, Barde immer / nie
///   Bescheiden / Hochmütig   keine Wohnhaus-Einrichtung / Brautschau nach Stand
/// </summary>
public sealed partial class KiRivale
{
    /// <summary>Alle Züge des Oberhaupts zusammen.</summary>
    public ZugNeigung Neigung => _w.Familien.KiNeigung(Konto);

    /// <summary>Raubfurcht für die Eskorte (Profil × Mutig/Feige).</summary>
    private double Vorsicht => Profil.Vorsicht * Neigung.Vorsicht;

    private double Wechselschwelle => Profil.Wechselschwelle * Neigung.Wechselschwelle;

    /// <summary>So viele Jahre darf ein Kauf, Ausbau oder Brief zum Bezahlen brauchen (Profil ± Mutig/Feige, mindestens 1).</summary>
    internal double Amortisationsjahre => Math.Max(1, Profil.MaxAmortisationJahre + Neigung.Amortisation);

    /// <summary>
    /// Ab dieser Beweiskraft klagt die KI an: so viele Beweise, wie ein sicherer Schuldspruch braucht (auch gegen bestochene Richter),
    /// dazu die Beweise mehr oder weniger aus den Zügen (Geduldig +1, Jähzornig −1) – nie weniger, als ein Schuldspruch überhaupt braucht.
    /// </summary>
    internal double Klageschwelle
    {
        get
        {
            var d = _w.Justiz.Daten;
            var sicher = (int)Math.Ceiling(d.SichereBeweiskraft / d.BeweiskraftJeBeweis - 1e-9);
            var mindestens = (int)Math.Ceiling(d.SchuldSchwelle / d.BeweiskraftJeBeweis - 1e-9);
            return Math.Max(mindestens, sicher + Neigung.KlageBeweise) * d.BeweiskraftJeBeweis;
        }
    }
}
