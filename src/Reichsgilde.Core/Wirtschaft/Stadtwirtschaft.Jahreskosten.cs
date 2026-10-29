namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Was ein Haus erst zum Jahresabschluss zahlt (PR #232): eine Abfrage für den Verwalter (Rücklage beim Zutateneinkauf, <see cref="Karren"/>)
/// und für die KI-Häuser (Hochrechnung zum Jahresende, <c>KiRivale</c>) – damit beide dasselbe zählen.
/// </summary>
public sealed partial class Stadtwirtschaft
{
    /// <summary>
    /// Was erst zum Jahresabschluss fällig wird: Löhne, Wächter und Unterhalt bzw. Pacht aller Betriebe des Hauses, Unterhalt der Schiffe,
    /// Unterhalt des Wohnhauses und der Hauswächter, Haushalt und Ausbildung der Familie – jeweils für das ganze Jahr.
    /// </summary>
    public double OffeneJahreskosten(Konto konto)
    {
        var waechter = Daten.Gauner?.WaechterKosten ?? 0;
        var betriebe = _betriebe.Where(b => b.Konto == konto).Sum(OffeneKosten);
        var schiffe = Schiffahrt.SchiffeVon(konto).Sum(s => s.Daten.Unterhalt);
        var haus = Wohnen.Unterhalt(konto) + (konto.Hauswaechter ? waechter : 0);
        var familie = Familien.Von(konto) is { } f ? Familien.Haushaltskosten(f) + Familien.Ausbildungskosten(f) : 0;
        return betriebe + schiffe + haus + familie;
    }

    /// <summary>Was dieser Betrieb erst zum Jahresabschluss zahlt: Löhne, Wächter und Unterhalt bzw. Pacht für das ganze Jahr.</summary>
    public double OffeneKosten(Betrieb b) =>
        b.Lohnkosten + b.Artdaten.Fixkosten + (b.Waechter ? Daten.Gauner?.WaechterKosten ?? 0 : 0);
}
