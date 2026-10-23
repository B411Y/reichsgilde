using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Fahrzeit der Karren mit Steigung (Kartenwerkstatt, Schritt 5b; Abschnitt <c>logistik.steigung</c>, Datenversion 50).
///
///   • Grundlage bleibt die feste Fahrzeit des Arbeitsorts (<c>logistik.fahrzeiten</c>: Werkstatt 30, Umland 120 Minuten).
///   • Hat die Karte Gelände, sucht der Kern einmal je Betrieb den kürzesten Weg über die Karrenstraßen vom Betrieb zum Markt
///     (<see cref="Karrennetz"/>) und mittelt die Bremse der Stücke (<see cref="SteigungDaten.Faktor(double)"/>) nach ihrer Länge – für
///     den Hinweg und, mit umgekehrtem Anstieg, für den Rückweg. Fahrzeit = feste Fahrzeit × Faktor, auf ganze Minuten.
///   • Furten und Fähren (Schritt 5c, Abschnitt <c>logistik.wasser</c>, Datenversion 51): Führt der Weg durch eine Furt oder über eine Fähre,
///     kommen deren Minuten dazu (<see cref="WasserDaten.Aufschlag"/>); Brücken kosten nichts. Der Weg selbst meidet Furt und Fähre, wenn
///     es nicht zu weit herum geht (<see cref="Karrennetz.FurtKosten"/>, <see cref="Karrennetz.FaehreUmweg"/>).
///   • Flache Karte ohne Furt und Fähre, keine Karte, kein Abschnitt <c>steigung</c> bzw. <c>wasser</c>, kein Platz oder kein Weg: die feste
///     Fahrzeit, genau wie bisher.
///   • Nichts davon steht im Spielstand: Karte und Daten sind dieselben, also auch die Faktoren.
/// </summary>
public sealed partial class Stadtwirtschaft
{
    private Karrennetz? _karrennetz;
    private readonly Dictionary<Betrieb, (int Hin, int Zurueck)> _fahrzeiten = new(ReferenceEqualityComparer.Instance);

    /// <summary>Das Netz der Karrenstraßen der Karte (null ohne Karte) – einmal gebaut, die Karte einer Partie ändert sich nicht.</summary>
    public Karrennetz? Karrennetz => Karte is { } k ? _karrennetz ??= Karrennetz.Von(k) : null;

    /// <summary>
    /// Wie lange ein Karren dieses Betriebs fährt (Spielminuten): <paramref name="zumMarkt"/> zum Markt, sonst zurück zum Betrieb. Die feste
    /// Fahrzeit des Arbeitsorts, bergauf länger (siehe oben); die Darstellung verteilt die Strecke auf genau diese Zeit.
    /// </summary>
    public int Fahrzeit(Betrieb betrieb, bool zumMarkt)
    {
        var basis = Daten.Logistik.FahrzeitAb(betrieb.Ort);
        if (Karte is not { } karte) return basis;
        var steigung = karte.Gelaendeform.HatHoehen ? Daten.Logistik.Steigung : null;
        var wasser = Daten.Logistik.Wasser is { } w && karte.Wasserplan.HaeltAuf ? w : null;
        if (steigung is null && wasser is null) return basis;
        if (!_fahrzeiten.TryGetValue(betrieb, out var zeiten))
        {
            if (Fahrten(betrieb) is not var (hin, zurueck)) return basis; // noch kein Platz: nicht merken, er kann noch kommen
            double Dauer(IReadOnlyList<Fahrtstueck> fahrt) => basis * (steigung?.Faktor(fahrt) ?? 1) + (wasser?.Aufschlag(fahrt) ?? 0);
            _fahrzeiten[betrieb] = zeiten = (Minuten(Dauer(hin)), Minuten(Dauer(zurueck)));
        }
        return zumMarkt ? zeiten.Hin : zeiten.Zurueck;

        static int Minuten(double x) => Math.Max(1, (int)Math.Round(x, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Die Fahrt eines Betriebs zum Markt und zurück über die Karrenstraßen (Stücke mit Länge und Anstieg) – null, wenn der Betrieb (noch)
    /// keinen Platz in der Karte hat. Leere Fahrten: kein Weg (dann bremst nichts).
    /// </summary>
    public (IReadOnlyList<Fahrtstueck> Hin, IReadOnlyList<Fahrtstueck> Zurueck)? Fahrten(Betrieb betrieb)
    {
        if (Karte is not { } karte || Karrennetz is not { } netz || Bauen.PlatzVon(betrieb.Name) is not { } platz) return null;
        var markt = karte.Gebaeude.FirstOrDefault(g => g.Katalog == "markt") is { } m ? new Kartenort(m.Punkt[0], m.Punkt[1]) : default;
        var ort = new Kartenort(platz.Punkt.X, platz.Punkt.Z);
        return (netz.Fahrt(ort, markt), netz.Fahrt(markt, ort));
    }
}
