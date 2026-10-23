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
///   • Belag (Schritt 5d, Abschnitt <c>wegebau</c>, Datenversion 52): Jedes Stück dauert zusätzlich Bezugstempo ÷ Tempo seines Belags
///     (<see cref="Bauen.Wegebau.Faktor"/>) so lang – auf Fahrwegen (dem Bezug) wie bisher, auf Pflaster kürzer, auf Trampelpfaden länger.
///     Der Belag ändert sich zum Jahreswechsel und wenn ein Ausbau fertig ist; dann rechnet der Kern die Fahrzeiten neu.
///   • Flache Karte ohne Furt und Fähre und ohne Belag, keine Karte, kein Abschnitt <c>steigung</c>, <c>wasser</c> bzw. <c>wegebau</c>, kein
///     Platz oder kein Weg: die feste Fahrzeit, genau wie bisher.
///   • Nichts davon steht im Spielstand: Karte und Daten sind dieselben, also auch die Strecken; der Belag steht im Stand des Wegebaus.
/// </summary>
public sealed partial class Stadtwirtschaft
{
    private Karrennetz? _karrennetz;
    private readonly Dictionary<Betrieb, (int Hin, int Zurueck)> _fahrzeiten = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Betrieb, (IReadOnlyList<Fahrtstueck> Hin, IReadOnlyList<Fahrtstueck> Zurueck)> _strecken = new(ReferenceEqualityComparer.Instance);

    /// <summary>Der Belag hat sich geändert: Die Fahrzeiten werden beim nächsten Mal neu gerechnet (die Strecken bleiben).</summary>
    internal void FahrzeitenVeraltet() => _fahrzeiten.Clear();

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
        var belag = Wegebau.IstAktiv ? Wegebau : null;
        if (steigung is null && wasser is null && belag is null) return basis;
        if (!_fahrzeiten.TryGetValue(betrieb, out var zeiten))
        {
            if (Fahrten(betrieb) is not var (hin, zurueck)) return basis; // noch kein Platz: nicht merken, er kann noch kommen
            double Faktor(IReadOnlyList<Fahrtstueck> fahrt)
            {
                if (belag is null) return steigung?.Faktor(fahrt) ?? 1;
                var laenge = fahrt.Sum(s => s.Laenge);
                return laenge <= 0 ? 1 : fahrt.Sum(s => s.Laenge * (steigung?.Faktor(s.Steigung) ?? 1) * belag.Faktor(s)) / laenge;
            }
            double Dauer(IReadOnlyList<Fahrtstueck> fahrt) => basis * Faktor(fahrt) + (wasser?.Aufschlag(fahrt) ?? 0);
            _fahrzeiten[betrieb] = zeiten = (Minuten(Dauer(hin)), Minuten(Dauer(zurueck)));
        }
        return zumMarkt ? zeiten.Hin : zeiten.Zurueck;

        static int Minuten(double x) => Math.Max(1, (int)Math.Round(x, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Die Fahrt eines Betriebs zum Markt und zurück über die Karrenstraßen (Stücke mit Länge und Anstieg) – null, wenn der Betrieb (noch)
    /// keinen Platz in der Karte hat. Leere Fahrten: kein Weg (dann bremst nichts). Einmal gerechnet je Betrieb (der Platz eines Betriebs
    /// ändert sich nicht).
    /// </summary>
    public (IReadOnlyList<Fahrtstueck> Hin, IReadOnlyList<Fahrtstueck> Zurueck)? Fahrten(Betrieb betrieb)
    {
        if (_strecken.TryGetValue(betrieb, out var bekannt)) return bekannt;
        if (Karte is not { } karte || Karrennetz is not { } netz || Bauen.PlatzVon(betrieb.Name) is not { } platz) return null;
        var markt = karte.Gebaeude.FirstOrDefault(g => g.Katalog == "markt") is { } m ? new Kartenort(m.Punkt[0], m.Punkt[1]) : default;
        var ort = new Kartenort(platz.Punkt.X, platz.Punkt.Z);
        return _strecken[betrieb] = (netz.Fahrt(ort, markt), netz.Fahrt(markt, ort));
    }
}
