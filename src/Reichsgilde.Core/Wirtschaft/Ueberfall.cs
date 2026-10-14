using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Überfälle auf Karren im Umland.
///
///   1. Wird die Fahrt angegriffen?   Chance = ChanceTag (× FaktorNacht in der Dunklen Nacht)
///   2. Welche Bande?                 Zufällig nach Gewicht – Banditen aller Völker, oft gemischt,
///                                    dazu wilde Ork-Rotten und Oger
///   3. Gelingt der Überfall?         Chance = Stärke der Bande × (1 − Schutz der Eskorte), höchstens 100 %
///
/// Gelingt er, ist die Ladung verloren. Ihr Marktwert wird in der Jahresrechnung als „Raub" vermerkt.
/// </summary>
internal static class Ueberfall
{
    internal static void Wuerfeln(Karren karren, EskorteDaten eskorte, Spielzeit zeit, Stadtwirtschaft w)
    {
        var daten = w.Daten.Logistik;
        var chance = daten.Ueberfall.ChanceTag;
        if (zeit.Tagesphase == Tagesphase.DunkleNacht)
            chance *= daten.Ueberfall.FaktorNacht;

        if (w.Zufall.NextDouble() >= Math.Min(1, chance))
            return;

        var bande = BandeWaehlen(daten.Banden, w.Zufall);
        // Kampf des Oberhaupts: seine Leute verteidigen die Karren mit.
        var schutz = Math.Min(0.95, eskorte.Schutz + w.Familien.KampfSchutz(karren.Heimat.Konto));
        var erfolgschance = Math.Min(1, bande.Staerke * (1 - schutz));
        var wer = $"{(bande.Wild ? "Eine wilde " : "Eine Bande „")}{bande.Name}{(bande.Wild ? "" : "“")} ({bande.ZusammensetzungText})";
        var wo = karren.Zustand == KarrenZustand.ZumMarkt ? $"auf dem Weg von {karren.Heimat.Name} zum Markt" : $"auf dem Rückweg nach {karren.Heimat.Name}";

        if (w.Zufall.NextDouble() < erfolgschance)
        {
            var verloren = karren.LadungVerlieren();
            var wert = verloren.Sum(v => w.Markt[v.Key].Preis * v.Value);
            karren.Heimat.Konto.Raub(wert);
            var beute = string.Join(" und ", verloren.Select(v => $"{v.Value} {v.Key}"));
            w.Melden($"Überfall {wo}: {wer} erbeutet {beute} (Wert {Zahlenformat.Ganz(wert)} {w.Daten.Allgemein.Waehrung}).");
        }
        else
        {
            w.Melden($"Überfall {wo}: Die Eskorte schlägt {wer.Replace("Eine ", "eine ")} in die Flucht.");
        }
    }

    private static BandeDaten BandeWaehlen(IReadOnlyList<BandeDaten> banden, Zufallsquelle zufall)
    {
        var summe = banden.Sum(b => b.Gewicht);
        var wurf = zufall.NextDouble() * summe;
        foreach (var bande in banden)
        {
            wurf -= bande.Gewicht;
            if (wurf < 0) return bande;
        }
        return banden[^1];
    }
}
