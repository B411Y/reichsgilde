using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Entwickler;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Karren selbst fahren (Nutzer 23.11.2026, wie in der „Gilde 1“): Der Spieler schickt seine Karren mit Befehlen zum Markt oder zu eigenen Betrieben,
/// handelt am Markt, lädt und lädt ab; ohne Befehl steht der Karren mit seiner Ladung. Die KI fährt immer automatisch; der Kern-Standard bleibt
/// automatisch. Ohne Karte gelten die festen Fahrzeiten (Werkstatt 30, Umland 120 Minuten); Betrieb → Betrieb dauert dann so lange wie über den Markt.
/// </summary>
static class KarrenSelbstFahrenTests
{
    static readonly string Json = Datenpfad.OhneJahreszeiten(); // ohne Schlamm im Frühling 1400

    /// <summary>Spielminute dieser Uhrzeit am ersten Tag.</summary>
    static long Um(int stunde, int minute = 0) => (stunde * 60 + minute) - Spielzeit.TagesbeginnMinute;

    /// <summary>Eine Stadt ohne Karte, Spieler fährt selbst: Grube (Umland, 40 Eisen) und Gießerei „G“ (Stadt), dazu ein fremdes Haus mit Gießerei „F“.</summary>
    static (Simulation Sim, Stadtwirtschaft W, Konto K, Betrieb Grube, Betrieb G, Betrieb F) Aufbau(int stunde = 10, bool selbst = true)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with
            {
                StartStatut = Fronstatut.FreieStadt, Siegbedingung = Siegbedingung.Endlos, KarrenSelbstFahren = selbst,
            },
            new Spielzeit(Um(stunde)), Wirtschaftsdaten.AusJson(Json));
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Spieler", startvermoegen: 5_000);
        var grube = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k);
        grube.Einlagern("Eisen", 40);
        var g = w.BetriebEroeffnen("G", Betriebsart.Giesserei, k);
        var fremd = w.KontoEroeffnen("Fremd");
        var f = w.BetriebEroeffnen("F", Betriebsart.Giesserei, fremd);
        return (sim, w, k, grube, g, f);
    }

    static void Ticken(Simulation sim, long bisMinute)
    {
        while (sim.Zeit.VergangeneMinuten < bisMinute) sim.Tick();
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
        ("Karren selbst: Kern-Standard automatisch; Partie-Einstellung „selbst fahren“ gilt für Spieler, KI-Häuser fahren immer automatisch", () =>
        {
            var (_, wa, ka, grubeA, _, _) = Aufbau(selbst: false);
            Pruefe.Wahr(wa.KarrenAutomatisch && wa.FaehrtAutomatisch(grubeA.Karren[0]), "Standard: automatisch");
            Pruefe.Wahr(!Partieeinstellungen.Standard.KarrenSelbstFahren, "Einstellung: Standard aus");
            var (_, w, k, grube, _, _) = Aufbau();
            Pruefe.Wahr(!w.KarrenAutomatisch && !w.FaehrtAutomatisch(grube.Karren[0]) && !w.HausFaehrtAutomatisch(k), "selbst");

            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, KarrenSelbstFahren = true },
                Wirtschaftsdaten.AusJson(Json), new NeuePartie(Rivalen: 2), new Spielzeit(Um(10)));
            var wp = sim.Wirtschaft!;
            Pruefe.Wahr(!wp.HausFaehrtAutomatisch(spieler), "Spieler fährt selbst");
            Pruefe.Wahr(wp.Rivalen.Count == 2 && wp.Rivalen.All(r => wp.HausFaehrtAutomatisch(r.Konto) && wp.IstKiHaus(r.Konto)), "KI fährt automatisch");
        }),

        ("Karren selbst: Ohne Befehl steht der Karren – auch bei vollem Lager kein Aufbruch, kein Verweilen", () =>
        {
            var (sim, _, _, grube, _, _) = Aufbau();
            Ticken(sim, Um(16));
            var karren = grube.Karren[0];
            Pruefe.Gleich(KarrenZustand.AmBetrieb, karren.Zustand);
            Pruefe.Gleich(0, karren.Ladung.Count);
            Pruefe.Gleich(40, grube.ImLager("Eisen"));
            Pruefe.Wahr(karren.VerweiltBis is null && karren.StandortBetrieb is null, "steht am Heimatbetrieb");
        }),

        ("Karren selbst: Beladen, zum Markt schicken (120 Min.), Befehle unterwegs abgelehnt; am Markt steht er mit Ladung und meldet sich", () =>
        {
            var (sim, w, k, grube, _, _) = Aufbau();
            var karren = grube.Karren[0];
            var meldungen = new List<Meldung>();
            w.Gemeldet += meldungen.Add;
            Ok(w.Ausfuehren(k, new KarrenBeladen("Grube", 0, D(("Eisen", 20)))));
            Pruefe.Gleich(20, karren.Ladung["Eisen"]);
            Pruefe.Gleich(20, grube.ImLager("Eisen"));
            var los = sim.Zeit.VergangeneMinuten;
            var e = w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0 }, null));
            Ok(e);
            Pruefe.Wahr(e.Meldung.Contains("zum Markt") && e.Meldung.Contains("12:00"), e.Meldung);
            Pruefe.Gleich(KarrenZustand.ZumMarkt, karren.Zustand);
            Pruefe.Gleich(los + 120, karren.Ankunft);
            Pruefe.Wahr(!karren.ZielHafen, "selbst gefahren zum Marktplatz");
            Nein(w.Ausfuehren(k, new KarrenBeladen("Grube", 0, D(("Eisen", 1)))), "unterwegs");
            Nein(w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0 }, "G")), "unterwegs");

            Ticken(sim, los + 121);
            Pruefe.Gleich(KarrenZustand.AmMarkt, karren.Zustand);
            Pruefe.Gleich(20, karren.Ladung["Eisen"]);
            Pruefe.Gleich(0.0, k.Laufend.Umsatz);
            Pruefe.Wahr(karren.VerweiltBis is null && !karren.Faehrt(sim.Zeit), "steht, verweilt nicht");
            Pruefe.Wahr(karren.ZustandText(sim.Zeit).Contains("steht am Markt"), karren.ZustandText(sim.Zeit));
            var m = meldungen.Single(x => x.Art == Meldungsart.KarrenAngekommen);
            Pruefe.Wahr(m.Text.Contains("am Markt angekommen mit 20 Eisen"), m.Text);
            Ticken(sim, los + 400);
            Pruefe.Gleich(KarrenZustand.AmMarkt, karren.Zustand); // ohne Befehl bleibt er
            Nein(w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0 }, null)), "schon am Markt");
            Nein(w.Ausfuehren(k, new KarrenBeladen("Grube", 0, D(("Eisen", 1)))), "am Markt");
            Nein(w.Ausfuehren(k, new KarrenAbladen("Grube", 0)), "am Markt");
        }),

        ("Karren selbst: Am Markt verkaufen und kaufen (Händlerspanne), dann zu einem anderen eigenen Betrieb; dort steht er mit der Ladung, „alles abladen“", () =>
        {
            var (sim, w, k, grube, g, _) = Aufbau();
            var karren = grube.Karren[0];
            Ok(w.Ausfuehren(k, new KarrenBeladen("Grube", 0, D(("Eisen", 20)))));
            Ok(w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0 }, null)));
            Nein(w.Ausfuehren(k, new KarrenHandeln("Grube", 0, D(("Eisen", 5)), D())), "unterwegs");
            Ticken(sim, karren.Ankunft + 1);
            var preis = w.Markt["Eisen"].Preis;
            var kosten = w.HandKaufpreis(k, "Holz", 5);
            Nein(w.Ausfuehren(k, new KarrenHandeln("Grube", 0, D(("Eisen", 21)), D())), "nur 20 Eisen");
            var e = w.Ausfuehren(k, new KarrenHandeln("Grube", 0, D(("Eisen", 20)), D(("Holz", 5))));
            Ok(e);
            Pruefe.Wahr(k.Laufend.Umsatz > 15 * preis, $"Umsatz {k.Laufend.Umsatz}");
            Pruefe.Ungefaehr(kosten, grube.Laufend.Zutaten, 1e-6);
            Pruefe.Gleich(5, karren.Ladung["Holz"]);
            Pruefe.Wahr(!karren.Ladung.ContainsKey("Eisen"), "Eisen verkauft");

            var ab = sim.Zeit.VergangeneMinuten;
            Ok(w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0 }, "G")));
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, karren.Zustand);
            Pruefe.Gleich("G", karren.ZielBetrieb);
            Pruefe.Gleich(ab + w.Fahrzeit(g, karren.Typ, zumMarkt: false), karren.Ankunft);
            Pruefe.Wahr(karren.ZustandText(sim.Zeit).Contains("unterwegs nach G"), karren.ZustandText(sim.Zeit));
            Ticken(sim, karren.Ankunft + 1);
            Pruefe.Gleich(KarrenZustand.AmBetrieb, karren.Zustand);
            Pruefe.Gleich("G", karren.StandortBetrieb);
            Pruefe.Gleich(g, karren.StandortVon(w));
            Pruefe.Gleich(5, karren.Ladung["Holz"]); // kein Abladen von allein
            Pruefe.Gleich(0, g.ImLager("Holz"));
            Nein(w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0 }, "G")), "steht schon an G");
            Ok(w.Ausfuehren(k, new KarrenAbladen("Grube", 0)));
            Pruefe.Gleich(5, g.ImLager("Holz"));
            Pruefe.Gleich(0, karren.Ladung.Count);
            Nein(w.Ausfuehren(k, new KarrenAbladen("Grube", 0)), "nichts geladen");
        }),

        ("Karren selbst: Betrieb → Betrieb ohne Karte dauert wie über den Markt (Stadt 30 + Umland 120); zurück fährt er heim, ins Umland nachts erst am Morgen", () =>
        {
            var (sim, w, k, grube, g, _) = Aufbau(stunde: 19);
            var karren = g.Karren[0];
            Pruefe.Gleich(150, w.FahrzeitZwischen(g, grube, karren.Typ));
            Ok(w.Ausfuehren(k, new KarrenLosschicken("G", new[] { 0 }, "Grube")));
            Pruefe.Gleich(Um(19) + 150, karren.Ankunft);
            Ticken(sim, karren.Ankunft + 1);
            Pruefe.Gleich("Grube", karren.StandortBetrieb);

            // Abends von der Grube in die Stadt (18 Uhr ab, 20:30 am Tor): am geschlossenen Tor wartet er bis zum Morgen.
            var (sim2, w2, k2, grube2, g2, _) = Aufbau(stunde: 18);
            var k2Karren = grube2.Karren[0];
            Ok(w2.Ausfuehren(k2, new KarrenLosschicken("Grube", new[] { 0 }, "G")));
            Ticken(sim2, k2Karren.Ankunft + 1);
            Pruefe.Wahr(k2Karren.WartetAmTor(sim2.Zeit) && k2Karren.Zustand == KarrenZustand.ZumBetrieb, "wartet vor dem Tor");
            Ticken(sim2, Spielzeit.MinutenProTag + 1);
            Pruefe.Gleich(KarrenZustand.AmBetrieb, k2Karren.Zustand);
            Pruefe.Gleich("G", k2Karren.StandortBetrieb);

            // Aus der Stadt ins Umland nach Torschluss: drinnen warten, dann fahren; KarrenZurueck = heim.
            var (sim3, w3, k3, grube3, g3, _) = Aufbau(stunde: 21);
            var k3Karren = g3.Karren[0];
            Ok(w3.Ausfuehren(k3, new KarrenLosschicken("G", new[] { 0 }, "Grube")));
            Pruefe.Wahr(k3Karren.WartetAmTor(sim3.Zeit), "wartet in der Stadt bis zum Morgen");
            Pruefe.Gleich((long)Spielzeit.MinutenProTag + 150, k3Karren.Ankunft);
            Ticken(sim3, k3Karren.Ankunft + 1);
            Pruefe.Gleich("Grube", k3Karren.StandortBetrieb);
            Ok(w3.Ausfuehren(k3, new KarrenZurueck("G", new[] { 0 })));
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, k3Karren.Zustand);
            Pruefe.Wahr(k3Karren.ZielBetrieb is null, "Ziel Heimat");
            Ticken(sim3, k3Karren.Ankunft + 1);
            Pruefe.Gleich(KarrenZustand.AmBetrieb, k3Karren.Zustand);
            Pruefe.Wahr(k3Karren.StandortBetrieb is null, "daheim");
        }),

        ("Karren selbst: Mehrere Karren zugleich; Ablehnungen (fremder Betrieb, doppelt, Ladefläche, Lager, Eskorte, Marktzeit)", () =>
        {
            var (sim, w, k, grube, g, _) = Aufbau();
            Ok(w.Ausfuehren(k, new KarrenKaufen("Grube", grube.Karren[0].Typ)));
            Pruefe.Gleich(2, grube.Karren.Count);
            Nein(w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0 }, "F")), "gehört nicht");
            Nein(w.Ausfuehren(k, new KarrenHandeln("G", 0, D(), D(("Holz", 1)))), "nicht am Markt");
            Nein(w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0, 0 }, null)), "doppelt");
            Nein(w.Ausfuehren(k, new KarrenLosschicken("Grube", Array.Empty<int>(), null)), "Kein Karren");
            Nein(w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0 }, "Nirgendwo")), "gibt es nicht");
            Nein(w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0 }, null, "Gibtsnicht")), "Eskorte");
            Nein(w.Ausfuehren(k, new KarrenBeladen("Grube", 0, D(("Eisen", 41)))), "nur 40 Eisen");
            var daten = grube.Karren[0].Daten;
            if (daten.Sorten * daten.KapazitaetJeSorte < 40)
                Nein(w.Ausfuehren(k, new KarrenBeladen("Grube", 0, D(("Eisen", daten.Sorten * daten.KapazitaetJeSorte + 1)))), "So viel fasst");
            Nein(w.Ausfuehren(k, new KarrenBeladen("Grube", 5, D(("Eisen", 1)))), "keinen Karren Nr. 6");
            var e = w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0, 1 }, null));
            Ok(e);
            Pruefe.Wahr(grube.Karren.All(x => x.Zustand == KarrenZustand.ZumMarkt), e.Meldung);

            // Marktzeit: am Markt nach 20 Uhr kein Handel
            var (sim2, w2, k2, grube2, _, _) = Aufbau(stunde: 19);
            Ok(w2.Ausfuehren(k2, new KarrenBeladen("Grube", 0, D(("Eisen", 5)))));
            Ok(w2.Ausfuehren(k2, new KarrenLosschicken("Grube", new[] { 0 }, null, "Keine")));
            Ticken(sim2, Spielzeit.MinutenProTag + 1); // am Tor gewartet, früh am Morgen am Markt
            Pruefe.Gleich(KarrenZustand.AmMarkt, grube2.Karren[0].Zustand);
            Nein(w2.Ausfuehren(k2, new KarrenHandeln("Grube", 0, D(("Eisen", 5)), D())), "Uhr offen");
        }),

        ("Karren selbst: Handelsauftrag und „Ware zurückhalten“ abgelehnt, Freigeben geht; der Verwalter fährt nicht – im automatischen Spiel alles wie bisher", () =>
        {
            var (_, w, k, grube, g, _) = Aufbau();
            Nein(w.Ausfuehren(k, new Handelsauftrag("G", 0, D(), D(("Holz", 1)))), "selbst");
            Nein(w.Ausfuehren(k, new WareZurueckhalten("G", "Eisen", true)), "selbst");
            Ok(w.Ausfuehren(k, new WareZurueckhalten("G", "Eisen", false)));
            var (_, wa, ka, _, _, _) = Aufbau(selbst: false);
            Nein(wa.Ausfuehren(ka, new KarrenLosschicken("Grube", new[] { 0 }, null)), "von selbst");
            Ok(wa.Ausfuehren(ka, new WareZurueckhalten("G", "Eisen", true)));
        }),

        ("Karren selbst: Entwicklerhaken „Karren fahren automatisch“ – ein Karren am Markt fährt heim und lädt ab, einer an einem anderen Betrieb ebenso", () =>
        {
            var (sim, w, k, grube, g, _) = Aufbau();
            w.Entwicklerhilfe.Erlaubt = true;
            var a = grube.Karren[0];
            var b = g.Karren[0];
            Ok(w.Ausfuehren(k, new KarrenBeladen("Grube", 0, D(("Eisen", 10)))));
            Ok(w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0 }, null)));
            Ok(w.Ausfuehren(k, new KarrenLosschicken("G", new[] { 0 }, "Grube")));
            Ticken(sim, Math.Max(a.Ankunft, b.Ankunft) + 1);
            Pruefe.Gleich(KarrenZustand.AmMarkt, a.Zustand);
            Pruefe.Gleich("Grube", b.StandortBetrieb);
            Ok(w.Ausfuehren(k, new EntwicklerKarrenAutomatisch(true)));
            Pruefe.Wahr(w.KarrenAutomatisch, "Haken an");
            sim.Tick();
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, a.Zustand);
            Pruefe.Gleich(KarrenZustand.ZumBetrieb, b.Zustand);
            Pruefe.Wahr(b.ZielBetrieb is null && b.VonBetrieb == "Grube", "b fährt von der Grube heim");
            Ok(w.Ausfuehren(k, new EntwicklerKarrenAutomatisch(false)));
            Pruefe.Wahr(!w.KarrenAutomatisch, "Haken aus");
        }),

        ("Karren selbst: Spielstand – Einstellung, Standort, Ziel und Herkunft überstehen Speichern und Laden; automatische Partien schreiben nichts davon", () =>
        {
            var (simA, _, kA, _, _, _) = Aufbau(selbst: false);
            var textA = Speicherung.Speichern(simA, kA, new DateTime(2026, 1, 1));
            foreach (var feld in new[] { "KarrenSelbstFahren", "KarrenAutomatisch", "StandortBetrieb", "ZielBetrieb", "VonBetrieb" })
                Pruefe.Wahr(!textA.Contains(feld, StringComparison.Ordinal), $"{feld} nicht im automatischen Spielstand");

            var (sim, w, k, grube, g, _) = Aufbau();
            Ok(w.Ausfuehren(k, new KarrenBeladen("Grube", 0, D(("Eisen", 10)))));
            Ok(w.Ausfuehren(k, new KarrenLosschicken("Grube", new[] { 0 }, "G")));
            Ticken(sim, sim.Zeit.VergangeneMinuten + 30);
            var text = Speicherung.Speichern(sim, k, new DateTime(2026, 1, 1));
            Pruefe.Wahr(text.Contains("\"KarrenAutomatisch\": false", StringComparison.Ordinal), "Kennzeichen im Spielstand");
            var (geladen, spieler) = Speicherung.Laden(text);
            var wg = geladen.Wirtschaft!;
            Pruefe.Wahr(!wg.KarrenAutomatisch && geladen.Einstellungen.KarrenSelbstFahren, "geladen: selbst");
            var gk = wg.Betriebe.Single(b => b.Name == "Grube").Karren[0];
            Pruefe.Gleich("G", gk.ZielBetrieb);
            Pruefe.Gleich("Grube", gk.VonBetrieb);
            Pruefe.Gleich(text, Speicherung.Speichern(geladen, spieler, new DateTime(2026, 1, 1)));
            Ticken(geladen, gk.Ankunft + 1);
            Ticken(sim, grube.Karren[0].Ankunft + 1);
            Pruefe.Gleich("G", gk.StandortBetrieb);
            Pruefe.Gleich(Speicherung.Speichern(sim, k, new DateTime(2026, 1, 1)), Speicherung.Speichern(geladen, spieler, new DateTime(2026, 1, 1)));

            // Kein Haken im Spielstand und Partie selbst gefahren: Der Haken gewinnt (Entwickler hat umgeschaltet).
            var knoten = JsonNode.Parse(text)!;
            knoten["Wirtschaft"]!.AsObject().Remove("KarrenAutomatisch");
            var (umgeschaltet, _) = Speicherung.Laden(knoten.ToJsonString());
            Pruefe.Wahr(umgeschaltet.Wirtschaft!.KarrenAutomatisch, "ohne Feld: automatisch");
        }),

        ("Karren selbst: Betrieb verkaufen geht nicht, solange ein Karren woanders steht oder ein Karren an ihm steht", () =>
        {
            var (sim, w, k, grube, g, _) = Aufbau();
            Ok(w.Ausfuehren(k, new KarrenLosschicken("G", new[] { 0 }, "Grube")));
            Ticken(sim, g.Karren[0].Ankunft + 1);
            Pruefe.Wahr(w.VerkaufVerboten(k, g)?.Contains("steht an Grube") == true, w.VerkaufVerboten(k, g) ?? "null");
            Pruefe.Wahr(w.VerkaufVerboten(k, grube)?.Contains("steht an Grube") == true, w.VerkaufVerboten(k, grube) ?? "null");
        }),
    };
}
