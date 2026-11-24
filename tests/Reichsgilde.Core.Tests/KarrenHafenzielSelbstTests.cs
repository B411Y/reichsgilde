using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Karren selbst fahren, Hafen als eigenes Ziel (K4, Nutzer 24.11.2026): Ein selbst gefahrener Karren fährt auf Befehl zum Hafen
/// (<c>KarrenLosschicken(…, Hafen: true)</c>), steht dort, handelt dort wie am Marktplatz (derselbe Markt, Aufschlag am Hafen für Ausfuhrware) und
/// fährt von dort zu einem Betrieb oder zum Marktplatz. Nur, wenn Karren zum Hafen fahren, die Stadt einen Hafen hat und er offen ist.
/// Die Proben stehen in Köln an dem Bauort mit der kürzesten Fahrt zum Hafen (wie <see cref="HafenAufschlagTests"/>).
/// </summary>
static class KarrenHafenzielSelbstTests
{
    static readonly Lazy<string> Json = new(() => HafenAufschlagTests.Daten(0.03));

    /// <summary>Köln im Sommer um 9 Uhr, Gießerei nahe dem Hafen mit 15 Silberringen, selbst gefahren.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K, Betrieb B, Karren Karren) Aufbau(string? json = null, int jahr = 1, int stunde = 9)
    {
        var (sim, w, k) = HafenAufschlagTests.Koeln(json ?? Json.Value, HafenAufschlagTests.Minute(jahr, stunde));
        w.KarrenAutomatisch = false;
        var e = w.Ausfuehren(k, HafenAufschlagTests.NahAmHafen.Value);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        var name = w.Bauen.Grundstuecke.Last(g => g.Art == Grundstuecksart.Werkstatt).Betrieb!;
        var b = w.Betriebe.Single(x => x.Name == name);
        b.Einlagern("Silberring", 15);
        return (sim, w, k, b, b.Karren[0]);
    }

    static void Ticken(Simulation sim, long bis)
    {
        while (sim.Zeit.VergangeneMinuten < bis) sim.Tick();
    }

    static Dictionary<string, int> D(params (string Ware, int Stueck)[] x) => x.ToDictionary(y => y.Ware, y => y.Stueck);

    static void Ok(Befehlsergebnis e) => Pruefe.Wahr(e.Erfolg, e.Meldung);

    static void Nein(Befehlsergebnis e, string teil)
    {
        Pruefe.Wahr(!e.Erfolg, $"abgelehnt erwartet: {e.Meldung}");
        Pruefe.Wahr(e.Meldung.Contains(teil, StringComparison.Ordinal), $"„{teil}“ erwartet: {e.Meldung}");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Karren selbst, Hafen: zum Hafen fahren (Fahrzeit zum Hafen), dort stehen, mit Aufschlag verkaufen, zum Marktplatz und heim", () =>
        {
            var (sim, w, k, b, karren) = Aufbau();
            Pruefe.Gleich(Jahreszeit.Sommer, w.Uhr.Jahreszeit);
            Ok(w.Ausfuehren(k, new KarrenBeladen(b.Name, 0, D(("Silberring", 15)))));
            var los = sim.Zeit.VergangeneMinuten;
            var e = w.Ausfuehren(k, new KarrenLosschicken(b.Name, new[] { 0 }, null, Hafen: true));
            Ok(e);
            Pruefe.Wahr(e.Meldung.Contains("zum Hafen"), e.Meldung);
            Pruefe.Wahr(karren.ZielHafen && karren.Zustand == KarrenZustand.ZumMarkt, "unterwegs zum Hafen");
            Pruefe.Gleich(los + w.FahrzeitZumHafen(b, karren.Typ, hin: true)!.Value, karren.Ankunft);
            Ticken(sim, karren.Ankunft + 1);
            Pruefe.Gleich(KarrenZustand.AmMarkt, karren.Zustand);
            Pruefe.Wahr(karren.ZustandText(sim.Zeit).Contains("steht am Hafen"), karren.ZustandText(sim.Zeit));
            Nein(w.Ausfuehren(k, new KarrenLosschicken(b.Name, new[] { 0 }, null, Hafen: true)), "schon am Hafen");

            var erwartet = w.KarrenHandelBarerloes(karren, "Silberring", 15);
            var ohneAufschlag = w.Markt["Silberring"].Verkaufswert(15) * w.Familien.VerkaufsFaktor(k) * b.PreisFaktor * (1 - w.SteuersatzMarkt(k));
            var bar = k.Vermoegen;
            Ok(w.Ausfuehren(k, new KarrenHandeln(b.Name, 0, D(("Silberring", 15)), D())));
            Pruefe.Ungefaehr(erwartet, k.Vermoegen - bar, 1e-6);
            Pruefe.Wahr(erwartet > ohneAufschlag * 1.02, $"Aufschlag am Hafen: {erwartet} gegen {ohneAufschlag}");

            // Vom Hafen zum Marktplatz (eigene Fahrzeit, nicht 0), dann heim.
            var ab = sim.Zeit.VergangeneMinuten;
            Ok(w.Ausfuehren(k, new KarrenLosschicken(b.Name, new[] { 0 }, null)));
            Pruefe.Wahr(!karren.ZielHafen && karren.VonHandelsort && karren.Herkunft(w) is null, "vom Hafen zum Marktplatz");
            Pruefe.Gleich(ab + w.FahrzeitZwischenHandelsorten(karren, zumHafen: false), karren.Ankunft);
            Pruefe.Wahr(karren.Ankunft > ab, "fährt eine Weile");
            Ticken(sim, karren.Ankunft + 1);
            Pruefe.Wahr(karren.Zustand == KarrenZustand.AmMarkt && !karren.ZielHafen && !karren.VonHandelsort, "am Marktplatz");
            var heim = sim.Zeit.VergangeneMinuten;
            Ok(w.Ausfuehren(k, new KarrenZurueck(b.Name, new[] { 0 })));
            Pruefe.Gleich(heim + w.Fahrzeit(b, karren.Typ, zumMarkt: false), karren.Ankunft);
        }),

        ("Karren selbst, Hafen: vom Hafen direkt heim (Rückfahrt vom Hafen); der Spielstand behält Hafen und Fahrt zwischen den Handelsorten", () =>
        {
            var (sim, w, k, b, karren) = Aufbau();
            Ok(w.Ausfuehren(k, new KarrenLosschicken(b.Name, new[] { 0 }, null, Hafen: true)));
            Ticken(sim, karren.Ankunft + 1);
            Ok(w.Ausfuehren(k, new KarrenLosschicken(b.Name, new[] { 0 }, null)));
            var text = Speicherung.Speichern(sim, k, new DateTime(2026, 1, 1));
            Pruefe.Wahr(text.Contains("\"VonHandelsort\": true", StringComparison.Ordinal), "Feld im Spielstand");
            var (geladen, spieler) = Speicherung.Laden(text);
            var gk = geladen.Wirtschaft!.Betriebe.Single(x => x.Name == b.Name).Karren[0];
            Pruefe.Wahr(gk.VonHandelsort && !gk.ZielHafen, "geladen");
            Pruefe.Gleich(text, Speicherung.Speichern(geladen, spieler, new DateTime(2026, 1, 1)));

            var (sim2, w2, k2, b2, karren2) = Aufbau();
            Ok(w2.Ausfuehren(k2, new KarrenLosschicken(b2.Name, new[] { 0 }, null, Hafen: true)));
            Ticken(sim2, karren2.Ankunft + 1);
            var heim = sim2.Zeit.VergangeneMinuten;
            Ok(w2.Ausfuehren(k2, new KarrenZurueck(b2.Name, new[] { 0 })));
            Pruefe.Gleich(heim + w2.FahrzeitZumHafen(b2, karren2.Typ, hin: false)!.Value, karren2.Ankunft);
            var ohne = Speicherung.Speichern(sim2, k2, new DateTime(2026, 1, 1));
            Pruefe.Wahr(!ohne.Contains("VonHandelsort", StringComparison.Ordinal), "ohne Fahrt zwischen Handelsorten kein Feld");
        }),

        ("Karren selbst, Hafen: abgelehnt mit Betrieb als Ziel, im Winter (Hafen zu) und ohne „karrenZumHafen“; ohne Karte kein Hafen", () =>
        {
            var (_, w, k, b, _) = Aufbau();
            Nein(w.Ausfuehren(k, new KarrenLosschicken(b.Name, new[] { 0 }, b.Name, Hafen: true)), "entweder");
            var (simw, ww, kw, bw, _) = Aufbau();
            while (ww.Uhr.Jahreszeit != Jahreszeit.Winter) simw.Tick();
            Pruefe.Wahr(!ww.Schiffahrt.HafenOffen, "Hafen zu");
            Nein(ww.Ausfuehren(kw, new KarrenLosschicken(bw.Name, new[] { 0 }, null, Hafen: true)), "geschlossen");
            var (_, wo, ko, bo, _) = Aufbau(HafenAufschlagTests.Daten(0.03, zumHafen: false));
            Nein(wo.Ausfuehren(ko, new KarrenLosschicken(bo.Name, new[] { 0 }, null, Hafen: true)), "nicht zum Hafen");

            var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, KarrenSelbstFahren = true },
                new Spielzeit(HafenAufschlagTests.Minute(1, 9)), Wirtschaftsdaten.AusJson(Json.Value));
            var w0 = sim.Wirtschaft!;
            var k0 = w0.KontoEroeffnen("Spieler");
            var g = w0.BetriebEroeffnen("G", Betriebsart.Giesserei, k0);
            Nein(w0.Ausfuehren(k0, new KarrenLosschicken("G", new[] { 0 }, null, Hafen: true)), "nicht zum Hafen");
        }),

        ("Karren selbst, Hafen: automatisch fahrende Karren wählen den Handelsort wie bisher (Befehl ohne Hafen ändert nichts am Verwalter)", () =>
        {
            var (sim, w, k, b, karren) = Aufbau();
            w.KarrenAutomatisch = true;
            for (var i = 0; i < Spielzeit.MinutenProTag && karren.Zustand != KarrenZustand.ZumMarkt; i++) sim.Tick();
            Pruefe.Wahr(karren.ZielHafen && !karren.VonHandelsort, "Verwalter fährt zum näheren Hafen");
            Pruefe.Gleich(w.Fahrzeit(karren, zumMarkt: true), w.KarrenFahrt(karren, b, null).Minuten);
        }),
    };
}
