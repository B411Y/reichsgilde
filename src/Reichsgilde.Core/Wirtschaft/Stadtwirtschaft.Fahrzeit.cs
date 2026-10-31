using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Fahrzeit der Karren nach Weglänge und Karrenart (Abschnitt <c>logistik.weglaenge</c>, Datenversion 65; Entscheidung des Nutzers vom 31.10.2026).
///
///   • Hat die Karte Karrenstraßen, sucht der Kern einmal je Betrieb den kürzesten Weg über sie vom Platz des Betriebs zum Markt
///     (<see cref="Karrennetz"/>, Hin- und Rückweg als Stücke mit Länge, Anstieg, Fahrbahn und Belag). Hat die Stadt mehrere Marktplätze
///     (ein Stadtmarkt mit gemeinsamen Preisen und gemeinsamem Bestand), fährt der Karren zum nächsten (<see cref="MarktplatzVon"/>).
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

    /// <summary>Strecke je Betrieb (hin, zurück) und der Marktplatz, zu dem sie führt (null: die Karte hat keinen, dann der Ursprung).</summary>
    private readonly Dictionary<Betrieb, (IReadOnlyList<Fahrtstueck> Hin, IReadOnlyList<Fahrtstueck> Zurueck, Kartengebaeude? Markt)> _strecken =
        new(ReferenceEqualityComparer.Instance);

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
    /// Die Fahrzeit einer Fahrt nach Weglänge: <see cref="Fahrminuten"/> auf ganze Minuten, zwischen Mindest- und Höchstdauer (ein Stück der Länge 0:
    /// Betrieb und Markt hängen an derselben Stelle des Netzes – die Mindestdauer). Ohne Stücke (Betrieb und Markt nicht verbunden, oder sie
    /// hängen am selben Knoten, das unterscheidet <see cref="Karrennetz.Fahrt"/> nicht) die feste Fahrzeit <paramref name="fest"/>.
    /// </summary>
    private int NachLaenge(IReadOnlyList<Fahrtstueck> fahrt, KarrenDaten art, int fest)
    {
        if (fahrt.Count == 0) return fest;
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
    /// Wie viele Spielminuten eine Fahrt der Karren dieses Betriebs kürzer würde, wenn sie <paramref name="gespartMeter"/> Meter ebenen Fahrweg
    /// weniger dauerte (für einen Ausbau, <see cref="Bauen.Wegebau.ErsparteMinutenJeJahr"/>). Nach Weglänge: je Karren des Betriebs die Fahrzeit vorher
    /// minus nachher, beide wie im Spiel auf ganze Minuten zwischen Mindest- und Höchstdauer (bleibt die Fahrt auch nach dem Ausbau an der
    /// Höchstdauer, spart er nichts), gemittelt über die Karren. Sonst (alte Daten, kein Karren mit Tempo) die feste Fahrzeit auf die Länge der
    /// Strecke verteilt, wie bis Datenversion 64.
    /// </summary>
    internal double GesparteMinuten(Betrieb betrieb, IReadOnlyList<Fahrtstueck> fahrt, double gespartMeter)
    {
        var arten = betrieb.Karren.Select(k => NachWeglaenge(k.Typ)).OfType<KarrenDaten>().ToList();
        if (arten.Count > 0 && Daten.Logistik.Weglaenge is { } grenzen && fahrt.Count > 0)
        {
            int Begrenzt(double minuten) => Math.Clamp(Minuten(minuten), grenzen.MindestMinuten, grenzen.HoechstMinuten);
            return arten.Average(k =>
            {
                var vorher = Fahrminuten(fahrt, k.MeterJeMinute);
                return Begrenzt(vorher) - Begrenzt(vorher - gespartMeter / k.MeterJeMinute);
            });
        }
        var laenge = fahrt.Sum(s => s.Laenge);
        return laenge <= 0 ? 0 : gespartMeter * Daten.Logistik.FahrzeitAb(betrieb.Ort) / laenge;
    }

    private static int Minuten(double x) => Math.Max(1, (int)Math.Round(x, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Die Fahrt eines Betriebs zum Markt und zurück über die Karrenstraßen (Stücke mit Länge und Anstieg) – null, wenn der Betrieb (noch)
    /// keinen Platz in der Karte hat. Leere Fahrten: kein Weg (dann gilt die feste Fahrzeit). Einmal gerechnet je Betrieb (der Platz eines
    /// Betriebs ändert sich nicht).
    /// Hat die Stadt mehrere Marktplätze (<see cref="Stadtkarte.Marktplaetze"/>), fährt der Karren zum nächsten: dem mit dem kürzesten Hinweg nach
    /// den Kosten der Wegwahl (<see cref="Karrennetz.Kosten"/>: Länge, Furt und Fähre teurer), bei Gleichstand dem ersten der Karte. Gewählt wird
    /// wie der Weg nach der Strecke, nicht nach dem Belag – so bleibt der Platz eines Betriebs fest, auch wenn ein Weg ausgebaut wird.
    /// Ohne Marktplatz in der Karte gilt der Ursprung (wie bis Oktober 2026).
    /// </summary>
    public (IReadOnlyList<Fahrtstueck> Hin, IReadOnlyList<Fahrtstueck> Zurueck)? Fahrten(Betrieb betrieb) =>
        Strecke(betrieb) is var (hin, zurueck, _) ? (hin, zurueck) : null;

    /// <summary>
    /// Der Marktplatz, zu dem die Karren dieses Betriebs fahren (siehe <see cref="Fahrten"/>) – null ohne Karte, ohne Platz des Betriebs oder ohne
    /// Marktplatz in der Karte. Ist keiner über die Karrenstraßen erreichbar, der erste der Karte (die Fahrt dauert dann die feste Zeit). Für die
    /// Darstellung (der Karren fährt im Bild dorthin) und die Oberfläche.
    /// </summary>
    public Kartengebaeude? MarktplatzVon(Betrieb betrieb) => Strecke(betrieb)?.Markt;

    private (IReadOnlyList<Fahrtstueck> Hin, IReadOnlyList<Fahrtstueck> Zurueck, Kartengebaeude? Markt)? Strecke(Betrieb betrieb)
    {
        if (_strecken.TryGetValue(betrieb, out var bekannt)) return bekannt;
        if (Karte is not { } karte || Karrennetz is not { } netz || Bauen.PlatzVon(betrieb.Name) is not { } platz) return null;
        var ort = new Kartenort(platz.Punkt.X, platz.Punkt.Z);
        var maerkte = karte.Marktplaetze;
        if (maerkte.Count == 0) return _strecken[betrieb] = (netz.Fahrt(ort, default), netz.Fahrt(default, ort), null);
        (IReadOnlyList<Fahrtstueck> Hin, Kartengebaeude? Markt, double Kosten) best = (Array.Empty<Fahrtstueck>(), null, double.MaxValue);
        foreach (var m in maerkte)
        {
            var hin = netz.Fahrt(ort, Mitte(m));
            if (hin.Count > 0 && Karrennetz.Kosten(hin) is var kosten && kosten < best.Kosten) best = (hin, m, kosten);
        }
        // Kein Platz erreichbar: wie bisher die (leeren) Fahrten zum ersten – es gilt die feste Fahrzeit.
        var markt = best.Markt ?? maerkte[0];
        return _strecken[betrieb] = (best.Markt is null ? netz.Fahrt(ort, Mitte(markt)) : best.Hin, netz.Fahrt(Mitte(markt), ort), markt);

        static Kartenort Mitte(Kartengebaeude g) => new(g.Punkt[0], g.Punkt[1]);
    }
}
