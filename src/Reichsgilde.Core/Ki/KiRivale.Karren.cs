using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Ki;

/// <summary>
/// Karren verkaufen (PR #205): Die KI gibt einen Karren ab, wenn sie knapp bei Kasse ist oder ein Karren ein ganzes Jahr nicht gefahren ist.
/// Den letzten Karren eines Betriebs, der Ware fährt, verkauft sie nie – nur Wache und Geldleihe brauchen keinen.
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
}
