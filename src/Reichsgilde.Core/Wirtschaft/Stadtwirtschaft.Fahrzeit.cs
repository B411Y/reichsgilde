using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Fahrzeit der Karren nach Weglänge und Karrenart (Abschnitt <c>logistik.weglaenge</c>, Datenversion 65; Entscheidung des Nutzers vom 31.10.2026).
///
///   • Hat die Karte Karrenstraßen, sucht der Kern einmal je Betrieb den kürzesten Weg über sie vom Platz des Betriebs zum Markt
///     (<see cref="Karrennetz"/>, Hin- und Rückweg als Stücke mit Länge, Anstieg, Fahrbahn und Belag).
///   • Ein Karren fährt so schnell wie seine Art (<see cref="KarrenDaten.Tempo"/>: Ochse 0,8, Handkarren 1,0, Pferd 1,3 Meter je Sekunde bei
///     Tempo 1, umgerechnet in Meter je Spielminute über <see cref="Zeit.Taktgeber.SekundenJeSpielminute"/>) – genau so schnell, wie man ihn
///     im Bild fahren sieht. Jedes Stück dauert Länge ÷ Tempo × <see cref="Zeitfaktor"/>:
///       – Steigung (Kartenwerkstatt, Schritt 5b, <c>logistik.steigung</c>): bergauf ab 6 % länger, bergab und eben wie auf ebenem Weg;
///       – Belag (Schritt 5d, <c>wegebau</c>): auf dem Fahrweg (Bezug) wie angegeben, auf Kies und Pflaster schneller (höchstens 15 %,
///         <c>wegebau.ausbauHoechstens</c>), auf Trampelpfaden langsamer.
///     Furten und Fähren (Schritt 5c, <c>logistik.wasser</c>) kosten ihre Minuten dazu (<see cref="WasserDaten.Aufschlag"/>), Brücken nichts.
///     Die Summe auf ganze Minuten, mindestens <see cref="WeglaengeDaten.MindestMinuten"/>, höchstens <see cref="WeglaengeDaten.HoechstMinuten"/>.
///   • Keine Karte, kein Platz oder kein Weg zum Markt: die feste Fahrzeit des Arbeitsorts (<c>logistik.fahrzeiten</c>: Werkstatt 30, Umland 120).
///   • Alte Daten ohne <c>logistik.weglaenge</c> (vor Datenversion 65): die feste Fahrzeit, mit Steigung und Belag nach Länge gemittelt
///     malgenommen, dazu Furten und Fähren – für alle Karrenarten gleich, wie bis PR #182.
///   • Der Belag ändert sich zum Jahreswechsel und wenn ein Ausbau fertig ist; dann rechnet der Kern die Fahrzeiten neu
///     (<see cref="FahrzeitenVeraltet"/>). Nichts davon steht im Spielstand: Karte und Daten sind dieselben, also auch die Strecken; der Belag
///     steht im Stand des Wegebaus, die Ankunft jedes Karrens als Spielminute.
/// </summary>
public sealed partial class Stadtwirtschaft
{
    private Karrennetz? _karrennetz;

    /// <summary>Gerechnete Fahrzeiten je Betrieb und Karrenart (hin, zurück).</summary>
    private readonly Dictionary<Betrieb, Dictionary<string, (int Hin, int Zurueck)>> _fahrzeiten = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<Betrieb, (IReadOnlyList<Fahrtstueck> Hin, IReadOnlyList<Fahrtstueck> Zurueck)> _strecken = new(ReferenceEqualityComparer.Instance);

    /// <summary>Der Belag hat sich geändert: Die Fahrzeiten werden beim nächsten Mal neu gerechnet (die Strecken bleiben).</summary>
    internal void FahrzeitenVeraltet() => _fahrzeiten.Clear();

    /// <summary>Das Netz der Karrenstraßen der Karte (null ohne Karte) – einmal gebaut, die Karte einer Partie ändert sich nicht.</summary>
    public Karrennetz? Karrennetz => Karte is { } k ? _karrennetz ??= Karrennetz.Von(k) : null;

    /// <summary>Die Daten dieser Karrenart, wenn der Kern ihre Fahrzeit nach der Weglänge rechnet (Abschnitt <c>logistik.weglaenge</c> und ein Tempo).</summary>
    private KarrenDaten? NachWeglaenge(string karrenTyp) =>
        Daten.Logistik.Weglaenge is not null && Daten.Logistik.Karren.TryGetValue(karrenTyp, out var k) && k.Tempo > 0 ? k : null;

    /// <summary>Wie lange dieser Karren fährt (Spielminuten): <paramref name="zumMarkt"/> zum Markt, sonst zurück zu seinem Betrieb.</summary>
    public int Fahrzeit(Karren karren, bool zumMarkt) => Fahrzeit(karren.Heimat, karren.Typ, zumMarkt);

    /// <summary>
    /// Wie lange ein Karren der Art <paramref name="karrenTyp"/> von diesem Betrieb fährt (Spielminuten): <paramref name="zumMarkt"/> zum Markt,
    /// sonst zurück. Nach Weglänge und Tempo der Art (siehe oben); die Darstellung fährt den Karren in genau dieser Zeit.
    /// </summary>
    public int Fahrzeit(Betrieb betrieb, string karrenTyp, bool zumMarkt)
    {
        var basis = Daten.Logistik.FahrzeitAb(betrieb.Ort);
        if (Karte is not { } karte) return basis;
        var art = NachWeglaenge(karrenTyp);
        var steigung = karte.Gelaendeform.HatHoehen ? Daten.Logistik.Steigung : null;
        var wasser = Daten.Logistik.Wasser is { } w && karte.Wasserplan.HaeltAuf ? w : null;
        var belag = Wegebau.IstAktiv ? Wegebau : null;
        if (art is null && steigung is null && wasser is null && belag is null) return basis;
        if (!_fahrzeiten.TryGetValue(betrieb, out var jeArt))
            _fahrzeiten[betrieb] = jeArt = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        if (!jeArt.TryGetValue(karrenTyp, out var zeiten))
        {
            if (Fahrten(betrieb) is not var (hin, zurueck)) return basis; // noch kein Platz: nicht merken, er kann noch kommen
            jeArt[karrenTyp] = zeiten = art is not null
                ? (NachLaenge(hin, art, basis), NachLaenge(zurueck, art, basis))
                : (Minuten(FestMalFaktor(hin)), Minuten(FestMalFaktor(zurueck)));
        }
        return zumMarkt ? zeiten.Hin : zeiten.Zurueck;

        // alte Daten (vor Datenversion 65): die feste Zeit mal dem Mittel der Faktoren über die Strecke, dazu Furten und Fähren
        double FestMalFaktor(IReadOnlyList<Fahrtstueck> fahrt)
        {
            double faktor;
            if (belag is null) faktor = steigung?.Faktor(fahrt) ?? 1;
            else
            {
                var laenge = fahrt.Sum(s => s.Laenge);
                faktor = laenge <= 0 ? 1 : fahrt.Sum(s => s.Laenge * Zeitfaktor(s)) / laenge;
            }
            return basis * faktor + (wasser?.Aufschlag(fahrt) ?? 0);
        }
    }

    /// <summary>
    /// Die Fahrzeit einer Fahrt nach Weglänge: <see cref="Fahrminuten"/> auf ganze Minuten, zwischen Mindest- und Höchstdauer. Ohne Weg (keine
    /// Stücke oder Länge 0: Betrieb und Markt sind nicht verbunden) die feste Fahrzeit <paramref name="fest"/>.
    /// </summary>
    private int NachLaenge(IReadOnlyList<Fahrtstueck> fahrt, KarrenDaten art, int fest)
    {
        if (fahrt.Sum(s => s.Laenge) <= 0) return fest;
        var grenzen = Daten.Logistik.Weglaenge!;
        return Math.Clamp(Minuten(Fahrminuten(fahrt, art.MeterJeMinute)), grenzen.MindestMinuten, grenzen.HoechstMinuten);
    }

    /// <summary>
    /// Wie viele Spielminuten eine Fahrt mit <paramref name="meterJeMinute"/> (auf ebenem Bezugsbelag) dauert, ungerundet und ohne Mindest- und
    /// Höchstdauer: jedes Stück Länge ÷ Tempo × <see cref="Zeitfaktor"/>, dazu Furten und Fähren.
    /// </summary>
    public double Fahrminuten(IReadOnlyList<Fahrtstueck> fahrt, double meterJeMinute)
    {
        var wasser = Daten.Logistik.Wasser is { } w && Karte?.Wasserplan.HaeltAuf == true ? w : null;
        return fahrt.Sum(s => s.Laenge * Zeitfaktor(s)) / Math.Max(meterJeMinute, 1e-9) + (wasser?.Aufschlag(fahrt) ?? 0);
    }

    /// <summary>
    /// Wie viel mal so lang ein Stück einer Fahrt dauert wie auf ebenem Fahrweg (dem Bezugsbelag): Steigung (nur auf Karten mit Höhen,
    /// <see cref="SteigungDaten.Faktor(double)"/>) mal Belag (<see cref="Bauen.Wegebau.Faktor"/>, ein Ausbau höchstens 15 % schneller). 1 ohne
    /// Karte. Die Darstellung rechnet mit demselben Faktor (sichtbares Tempo = Tempo der Art ÷ Faktor).
    /// </summary>
    public double Zeitfaktor(Fahrtstueck s)
    {
        if (Karte is not { } karte) return 1;
        var steigung = karte.Gelaendeform.HatHoehen ? Daten.Logistik.Steigung : null;
        return (steigung?.Faktor(s.Steigung) ?? 1) * (Wegebau.IstAktiv ? Wegebau.Faktor(s) : 1);
    }

    /// <summary>
    /// Wie viele Spielminuten ein Meter ebener Fahrweg die Karren dieses Betriebs kostet (für die gesparten Minuten eines Ausbaus,
    /// <see cref="Bauen.Wegebau.ErsparteMinutenJeJahr"/>): nach Weglänge 1 ÷ Tempo, gemittelt über seine Karren; sonst die feste Fahrzeit auf
    /// die Länge der Strecke (<paramref name="laenge"/>, Meter) verteilt. Mindest- und Höchstdauer bleiben dabei außen vor.
    /// </summary>
    internal double MinutenJeMeter(Betrieb betrieb, double laenge)
    {
        var arten = betrieb.Karren.Select(k => NachWeglaenge(k.Typ)).OfType<KarrenDaten>().ToList();
        if (arten.Count > 0) return arten.Average(k => 1 / k.MeterJeMinute);
        return laenge <= 0 ? 0 : Daten.Logistik.FahrzeitAb(betrieb.Ort) / laenge;
    }

    private static int Minuten(double x) => Math.Max(1, (int)Math.Round(x, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Die Fahrt eines Betriebs zum Markt und zurück über die Karrenstraßen (Stücke mit Länge und Anstieg) – null, wenn der Betrieb (noch)
    /// keinen Platz in der Karte hat. Leere Fahrten: kein Weg (dann gilt die feste Fahrzeit). Einmal gerechnet je Betrieb (der Platz eines
    /// Betriebs ändert sich nicht).
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
