using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für den abgeleiteten Fuhrmann eines Karrens (nur fürs Bild) und die Abfrage „fährt“.</summary>
static class KarrenFuhrmannTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: Daten.Value);

    static Betrieb BetriebMitKarren(Stadtwirtschaft w, string name)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 10_000);
        return w.BetriebEroeffnen($"Giesserei {name}", Betriebsart.Giesserei, k, startkarren: "Handkarren");
    }

    static Wirtschaftsdaten TorDaten() => Wirtschaftsdaten.AusJson(Datenpfad.OhneJahreszeiten());

    static long Um(int stunde, int minute = 0) => (stunde * 60 + minute) - Spielzeit.TagesbeginnMinute;

    static (Simulation Sim, Konto K, Betrieb Mine) Aufbau(int stunde)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { StartStatut = Fronstatut.FreieStadt, Siegbedingung = Siegbedingung.Endlos },
            new Spielzeit(Um(stunde)), TorDaten());
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Spieler");
        var mine = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k);
        mine.Einlagern("Eisen", 40);
        return (sim, k, mine);
    }

    static void Ticken(Simulation sim, long bisMinute)
    {
        while (sim.Zeit.VergangeneMinuten < bisMinute) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fuhrmann: derselbe Karren hat immer denselben Fuhrmann, in jeder Simulation und bei jedem Abruf", () =>
        {
            var a = NeueSimulation().Wirtschaft!;
            var b = NeueSimulation().Wirtschaft!;
            var ka = BetriebMitKarren(a, "Familie Krug").Karren[0];
            var kb = BetriebMitKarren(b, "Familie Krug").Karren[0];
            Pruefe.Gleich(ka.FuhrmannIn(a), ka.FuhrmannIn(a));
            Pruefe.Gleich(ka.FuhrmannIn(a), kb.FuhrmannIn(b));
            Pruefe.Wahr(ka.FuhrmannIn(a).Startwert >= 0, "Startwert nicht negativ");
        }),
        ("Fuhrmann: Ableitung verbraucht keine Würfel der Partie und ändert nichts am Karren", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var karren = BetriebMitKarren(w, "Familie Krug").Karren[0];
            var vorher = w.Zufall.Wuerfe;
            for (var i = 0; i < 5; i++) karren.FuhrmannIn(w);
            Pruefe.Gleich(vorher, w.Zufall.Wuerfe);
            Pruefe.Gleich(KarrenZustand.AmBetrieb, karren.Zustand);
        }),
        ("Fuhrmann: viele Karren ergeben verschiedene Fuhrleute, nur Bürgervölker (ohne Ork und Oger im Menschenhaus), auch Frauen", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var voelker = new HashSet<Volk>();
            var gesichter = new HashSet<(Volk, int)>();
            var frauen = 0;
            for (var i = 0; i < 60; i++)
            {
                var f = Fuhrmann.Ableiten($"Haus {i}", "Handkarren 1", Volk.Mensch, 0.6, 0.15);
                voelker.Add(f.Volk);
                gesichter.Add((f.Volk, f.Aussehen.Gesicht));
                if (f.Geschlecht == Geschlecht.Frau) frauen++;
                Pruefe.Wahr(f.Volk is Volk.Mensch or Volk.Zwerg or Volk.Elb or Volk.Halbling, "nur Bürgervölker");
            }
            Pruefe.Wahr(voelker.Count >= 3, "mehrere Völker");
            Pruefe.Wahr(gesichter.Count >= 10, "verschiedene Gesichter");
            Pruefe.Wahr(frauen is > 0 and < 30, "einige Frauen, überwiegend Männer");
        }),
        ("Fuhrmann: ein Ork-Haus bekommt Ork-Fuhrleute (nur dann), ein Menschenhaus nie", () =>
        {
            var ork = 0;
            for (var i = 0; i < 60; i++)
            {
                if (Fuhrmann.Ableiten($"Haus {i}", "Ochsenkarren 1", Volk.Ork, 1.0, 0.15).Volk == Volk.Ork) ork++;
                Pruefe.Wahr(Fuhrmann.Ableiten($"Haus {i}", "Ochsenkarren 1", Volk.Mensch, 1.0, 0.15).Volk == Volk.Mensch, "Menschenhaus mit Eigenanteil 1");
            }
            Pruefe.Gleich(60, ork);
        }),
        ("Fuhrmann: Zwergenhaus – das Volk des Hauses kommt vor (echte Familie), nie ein Ork", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var konto = w.KontoEroeffnen("Haus Zwergenfeld", startvermoegen: 10_000);
            w.Familien.Gruenden(konto, Volk.Zwerg, Geschlecht.Mann);
            var zwerge = 0;
            for (var i = 0; i < 40; i++)
            {
                var betrieb = w.BetriebEroeffnen($"Giesserei Zwerg {i}", Betriebsart.Giesserei, konto, startkarren: "Handkarren");
                var f = betrieb.Karren[0].FuhrmannIn(w);
                if (f.Volk == Volk.Zwerg) zwerge++;
                Pruefe.Wahr(f.Volk is Volk.Mensch or Volk.Zwerg or Volk.Elb or Volk.Halbling, "nur Bürgervölker");
            }
            Pruefe.Wahr(zwerge >= 10, $"das Hausvolk überwiegt (Zwerge: {zwerge} von 40)");
        }),
        ("Karren: Faehrt – am Betrieb nein, auf dem Hinweg ja, am geschlossenen Tor nein, auf dem Rückweg ja", () =>
        {
            // Am Betrieb: nein
            var sim0 = NeueSimulation();
            var k0 = BetriebMitKarren(sim0.Wirtschaft!, "Familie Krug").Karren[0];
            Pruefe.Wahr(!k0.Faehrt(sim0.Zeit), "am Betrieb");
            Pruefe.Gleich(KarrenZustand.AmBetrieb, k0.Zustand);

            // Hinweg am Nachmittag (Tor offen): ja; Rückweg: ja
            var (sim, _, mine) = Aufbau(17);
            sim.Tick();
            var karren = mine.Karren[0];
            Pruefe.Gleich(KarrenZustand.ZumMarkt, karren.Zustand);
            Pruefe.Wahr(karren.Faehrt(sim.Zeit), "Hinweg bei offenem Tor");
            Ticken(sim, Um(19, 5));
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, karren.Zustand);
            Pruefe.Wahr(karren.Faehrt(sim.Zeit), "Rückweg");

            // Abends losgefahren, um 21:30 vor dem geschlossenen Tor: nein; am Morgen wieder unterwegs: ja
            var (sim2, _, mine2) = Aufbau(19);
            sim2.Tick();
            var spaet = mine2.Karren[0];
            Pruefe.Wahr(spaet.Faehrt(sim2.Zeit), "kurz nach der Abfahrt");
            Ticken(sim2, Um(21, 30));
            Pruefe.Wahr(spaet.WartetAmTor(sim2.Zeit), "wartet am Tor");
            Pruefe.Wahr(!spaet.Faehrt(sim2.Zeit), "am geschlossenen Tor");
            Ticken(sim2, Spielzeit.MinutenProTag + 2);
            Pruefe.Wahr(spaet.Faehrt(sim2.Zeit), "am Morgen nach dem Tor wieder unterwegs");
        }),
    };
}
