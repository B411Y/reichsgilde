using Reichsgilde.Core.Zeit;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Karren selbst fahren (Nutzer 23.11.2026, wie in der „Gilde 1“): die Handgriffe, die der Spieler über Befehle auslöst. Geprüft hat vorher die
/// Stadtwirtschaft (<c>Stadtwirtschaft.Karrenfahrt.cs</c>: Besitzer, Zustand, Lager, Ladeflächen, Geld, Marktzeit); hier wird nur ausgeführt.
/// </summary>
public sealed partial class Karren
{
    /// <summary>Wie viele Ladeflächen diese Mengen brauchen (je Fläche eine Ware, höchstens <see cref="KarrenDaten.KapazitaetJeSorte"/> Stück).</summary>
    public int Flaechen(IEnumerable<KeyValuePair<string, int>> mengen) =>
        mengen.Where(x => x.Value > 0).Sum(x => (x.Value + Daten.KapazitaetJeSorte - 1) / Daten.KapazitaetJeSorte);

    /// <summary>Wie viele Ladeflächen die jetzige Ladung belegt.</summary>
    public int BelegteFlaechen => Flaechen(_ladung);

    /// <summary>Losfahren vom Standort nach <paramref name="nach"/> (null: zum Markt); ins Umland erst, wenn das Tor offen ist.</summary>
    internal void Losschicken(Betrieb? nach, Spielzeit zeit, Stadtwirtschaft w)
    {
        var von = Zustand == KarrenZustand.AmMarkt ? null : StandortVon(w) ?? Heimat;
        _wareWartetSeit = null;
        Abfahren(nach is null ? KarrenZustand.ZumMarkt : KarrenZustand.ZumBetrieb, von, nach, zeit, w);
    }

    /// <summary>Waren aus dem Lager von <paramref name="lager"/> auf den Karren laden.</summary>
    internal void Laden(Betrieb lager, IReadOnlyDictionary<string, int> waren)
    {
        foreach (var (ware, stueck) in waren.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal))
            _ladung[ware] = _ladung.GetValueOrDefault(ware) + lager.Auslagern(ware, stueck);
        LadungAufraeumen();
    }

    /// <summary>Ladung ins Lager von <paramref name="lager"/> abladen: <paramref name="waren"/> null = alles. Zurück: was abgeladen wurde.</summary>
    internal Dictionary<string, int> AbladenIn(Betrieb lager, IReadOnlyDictionary<string, int>? waren)
    {
        var abgeladen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var ware in _ladung.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList())
        {
            var stueck = Math.Min(_ladung[ware], waren is null ? int.MaxValue : waren.GetValueOrDefault(ware));
            if (stueck <= 0) continue;
            lager.Einlagern(ware, stueck);
            _ladung[ware] -= stueck;
            abgeladen[ware] = stueck;
        }
        LadungAufraeumen();
        return abgeladen;
    }

    /// <summary>Am Markt: Ladung verkaufen (wie der Verwalter: Verhandeln, Aufschlag am Hafen, Steuer). Zurück: der Erlös.</summary>
    internal double Verkaufen(IReadOnlyDictionary<string, int> verkauf, Stadtwirtschaft w)
    {
        var vorher = Heimat.Konto.Laufend.Umsatz;
        foreach (var (ware, wunsch) in verkauf.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var stueck = Math.Min(wunsch, _ladung.GetValueOrDefault(ware));
            if (stueck <= 0) continue;
            Heimat.Verkaufen(ware, stueck, w.Markt, w.KarrenVerkaufsFaktor(Heimat.Konto, ware, ZielHafen), w.SteuersatzMarkt(Heimat.Konto));
            _ladung[ware] -= stueck;
        }
        LadungAufraeumen();
        return Heimat.Konto.Laufend.Umsatz - vorher;
    }

    /// <summary>Am Markt: Waren auf den Karren kaufen (Marktpreis + Händlerspanne, jedes Stück hebt den Preis), als Zutaten gebucht. Zurück: die Kosten.</summary>
    internal double Kaufen(IReadOnlyDictionary<string, int> kauf, Stadtwirtschaft w)
    {
        var faktor = w.HandKaufFaktor(Heimat.Konto);
        double summe = 0;
        foreach (var (ware, stueck) in kauf.Where(x => x.Value > 0).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var kosten = w.Markt[ware].HandKaufen(stueck, faktor);
            Heimat.ZutatenBuchen(kosten);
            _ladung[ware] = _ladung.GetValueOrDefault(ware) + stueck;
            summe += kosten;
        }
        return summe;
    }

    /// <summary>Leere Einträge entfernen (eine Ware mit 0 Stück belegt keine Ladefläche und steht nicht im Spielstand).</summary>
    private void LadungAufraeumen()
    {
        foreach (var ware in _ladung.Where(x => x.Value <= 0).Select(x => x.Key).ToList()) _ladung.Remove(ware);
    }
}
