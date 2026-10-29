using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Entwickler;

/// <summary>Ein Betrieb eines KI-Hauses, wie die KI ihn rechnet.</summary>
/// <param name="Ware">Die Ware einer Werkstatt (null: Umland oder Betrieb ohne Ware).</param>
/// <param name="WertJeArbeitsminute">Gulden je Arbeitsminute bei der jetzigen Ausrichtung (<see cref="Kalkulation.WertJeArbeitsminute"/>).</param>
/// <param name="Verlustjahre">Jahre in Folge mit Verlust – ab einigen verkauft die KI.</param>
public sealed record KiBetriebEinblick(string Betrieb, string? Ware, double WertJeArbeitsminute, int Verlustjahre);

/// <summary>
/// Was ein KI-Haus rechnet und zuletzt entschieden hat – fürs Entwicklerfenster (nur lesen, ändert nichts und würfelt nicht).
/// </summary>
/// <param name="Profil">Die Eigenschaften der KI (Reserve, Vorsicht, Gaunerei …).</param>
/// <param name="ReinvermoegenAmJahresende">Ihre Hochrechnung des Reinvermögens zum Jahresabschluss (danach kauft und verkauft sie).</param>
/// <param name="BevorzugtesStatut">Das Fron- und Sklavenstatut, das ihr am meisten bringt (danach beantragt und stimmt sie).</param>
/// <param name="Entscheidungen">Die letzten Entscheidungen mit Begründung, neueste zuerst (höchstens 20, wie in den Meldungen).</param>
public sealed record KiEinblick(
    string Haus, bool Aktiv, KiProfil Profil, double Vermoegen, double ReinvermoegenAmJahresende, int BetriebeHoechstens,
    Fronstatut BevorzugtesStatut, IReadOnlyList<KiBetriebEinblick> Betriebe, IReadOnlyList<string> Entscheidungen)
{
    /// <summary>Alle KI-Häuser der Stadt in Konto-Reihenfolge.</summary>
    public static IReadOnlyList<KiEinblick> Alle(Stadtwirtschaft w) => w.Rivalen.Select(r => Von(w, r)).ToList();

    /// <summary>Der Einblick in ein KI-Haus.</summary>
    public static KiEinblick Von(Stadtwirtschaft w, KiRivale r) => new(
        r.Konto.Inhaber, r.Konto.IstAktiv, r.Profil, r.Konto.Vermoegen, r.ReinvermoegenAmJahresende, r.BetriebeHoechstens,
        r.BevorzugtesStatut,
        r.Betriebe.Select(b => new KiBetriebEinblick(b.Name, b.IstWerkstatt ? b.Produkt : null, r.WertJeArbeitsminute(b), r.Verlustjahre(b))).ToList(),
        r.Entscheidungen.ToList());

    /// <summary>Lesbare Zeilen für das Fenster: Kopf, Betriebe, Entscheidungen.</summary>
    public IReadOnlyList<string> Zeilen
    {
        get
        {
            var zeilen = new List<string>
            {
                $"{Haus}{(Aktiv ? "" : " (nicht mehr im Spiel)")}: {Zahlenformat.Ganz(Vermoegen)} Gulden, zum Jahresende erwartet {Zahlenformat.Ganz(ReinvermoegenAmJahresende)}",
                $"Profil: Reserve {Zahlenformat.Ganz(Profil.Reserve)}, Vorsicht {Zahlenformat.Kommazahl(Profil.Vorsicht)}, höchstens {BetriebeHoechstens} Betriebe, " +
                $"Gaunerei {Profil.Gaunerei}, bevorzugtes Statut {BevorzugtesStatut}",
            };
            zeilen.AddRange(Betriebe.Select(b =>
                $"  {b.Betrieb}{(b.Ware is null ? "" : $" ({b.Ware})")}: {Zahlenformat.Kommazahl(b.WertJeArbeitsminute)} je Arbeitsminute" +
                (b.Verlustjahre > 0 ? $", {b.Verlustjahre} Verlustjahre" : "")));
            zeilen.AddRange(Entscheidungen.Select(e => "  · " + e));
            return zeilen;
        }
    }
}
