using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für das Verkaufen von Werkstätten und das Kündigen von Pachten – und den Wiederkauf mit Stufe.</summary>
static class WerkstattVerkaufTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation() =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, wirtschaftsdaten: Daten.Value);

    /// <summary>Ein Schmiedehaus mit Grube und einer Startgießerei („am Weinmarkt“ – keine Liegenschaft aus den Daten).</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb G, Betrieb Grube) Schmied(Simulation sim, double vermoegen = 5000)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: vermoegen);
        var g = w.BetriebEroeffnen("Gießerei am Weinmarkt", Betriebsart.Giesserei, k);
        var grube = w.BetriebEroeffnen("Grube Fuchs", Betriebsart.Mine, k);
        return (w, k, g, grube);
    }

    /// <summary>Ein Konto, das schon Bürger ist und das gewünschte Vermögen hat.</summary>
    static Konto Buerger(Stadtwirtschaft w, string name, double vermoegen)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 10000);
        Pruefe.Wahr(w.Ausfuehren(Wohnhaushilfe.MitWohnhaus(w, k), new StandErwerben()).Erfolg, "Aufstieg zum Bürger fehlgeschlagen");
        if (vermoegen > k.Vermoegen) k.Umsatz(vermoegen - k.Vermoegen); else k.Schaden(k.Vermoegen - vermoegen);
        return k;
    }

    /// <summary>Die Fehler, die die Prüfung der Daten meldet (leer, wenn alles stimmt).</summary>
    static IReadOnlyList<string> Fehlerliste(Action laden)
    {
        try { laden(); }
        catch (WirtschaftsdatenFehler f) { return f.Fehler; }
        return Array.Empty<string>();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Verkauf: 75 % des Werts – Gießerei 2.625, Schmiede 4.500, Goldschmiede 8.250; danach frei mit Stufe und vollem Wert", () =>
        {
            foreach (var (art, wert, grad) in new[] { (Betriebsart.Giesserei, 3500.0, 1), (Betriebsart.Schmiede, 6000.0, 3), (Betriebsart.Goldschmiede, 11000.0, 5) })
            {
                var (w, k, g, _) = Schmied(NeueSimulation());
                if (art != Betriebsart.Giesserei) g.Ausbauen(art, 0);
                w.Einstellen(g, "Geselle", 2);
                (Betrieb Betrieb, LiegenschaftDaten Frei)? ereignis = null;
                w.BetriebAbgegeben += (b, l) => ereignis = (b, l);

                var v = w.VerkaufVorschau(g);
                Pruefe.Gleich(null, v.Verboten);
                Pruefe.Gleich(false, v.IstPacht);
                Pruefe.Ungefaehr(wert, v.Wert, 1e-9);
                Pruefe.Ungefaehr(wert * 0.75, v.Gebaeudeerloes, 1e-9);
                Pruefe.Gleich(2, v.Personal["Geselle"]);
                Pruefe.Gleich("Handkarren 1", v.Karren.Single());

                var vorher = k.Vermoegen;
                var e = w.Ausfuehren(k, new BetriebVerkaufen(g.Name));
                Pruefe.Wahr(e.Erfolg, e.Meldung);
                Pruefe.Ungefaehr(vorher + wert * 0.75, k.Vermoegen, 1e-9); // um 05:00 sind noch keine Löhne angefallen
                Pruefe.Ungefaehr(wert * 0.75, k.Laufend.Verkaeufe, 1e-9);
                Pruefe.Ungefaehr(0, k.Laufend.Gewinn, 1e-9); // kein Betriebsgewinn – wie eine Anschaffung, nur umgekehrt
                Pruefe.Wahr(!w.Betriebe.Contains(g), "Betrieb ist noch da");

                var frei = w.FreieLiegenschaften.Single(l => l.Name == "Gießerei am Weinmarkt");
                Pruefe.Gleich(art, frei.Art);
                Pruefe.Ungefaehr(wert, frei.Kaufpreis, 1e-9);
                Pruefe.Gleich(grad, frei.MindestGrad);
                Pruefe.Gleich(true, frei.Bebaut);
                Pruefe.Gleich("Bürger", frei.MindestStand);
                Pruefe.Gleich(g, ereignis!.Value.Betrieb);
                Pruefe.Gleich(frei, ereignis.Value.Frei);
            }
        }),

        ("Verkauf: Lagerware geht zum Marktpreis −20 % mit, der Markt merkt nichts davon", () =>
        {
            var (w, k, g, _) = Schmied(NeueSimulation());
            g.Einlagern("Silberring", 10);
            g.Einlagern("Eisen", 5);
            var ring = w.Markt["Silberring"].Preis;
            var eisen = w.Markt["Eisen"].Preis;
            var v = w.VerkaufVorschau(g);
            Pruefe.Gleich(2, v.Lagerware.Count);
            Pruefe.Ungefaehr(10 * ring * 0.8, v.Lagerware.Single(x => x.Ware == "Silberring").Wert, 1e-9);
            Pruefe.Ungefaehr(10 * ring * 0.8 + 5 * eisen * 0.8, v.Warenerloes, 1e-9);
            Pruefe.Ungefaehr(2625 + v.Warenerloes, v.Summe, 1e-9);

            var vorher = k.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(k, new BetriebVerkaufen(g.Name)).Erfolg, "Verkauf");
            Pruefe.Ungefaehr(vorher + v.Summe, k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(ring, w.Markt["Silberring"].Preis, 1e-12);
            Pruefe.Ungefaehr(0, k.Laufend.Umsatz + k.Laufend.Steuer, 1e-9); // kein Umsatz, keine Steuer
        }),

        ("Pacht kündigen: kein Erlös für die Grube, nur die Lagerware; sie wird wieder freie Pacht – auch eine aus den Daten, nur einmal", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = Buerger(w, "Spieler", 6000);
            w.BetriebEroeffnen("Gießerei Spieler", Betriebsart.Giesserei, k);
            Pruefe.Wahr(w.Ausfuehren(k, new LiegenschaftErwerben("Grube am Silberberg")).Erfolg, "Pacht");
            var grube = w.Betriebe.Single(b => b.Name == "Grube am Silberberg");
            w.Einstellen(grube, "Ork", 3);
            grube.Einlagern("Silber", 4);
            var anzahl = w.FreieLiegenschaften.Count();

            var v = w.VerkaufVorschau(grube);
            Pruefe.Gleich(true, v.IstPacht);
            Pruefe.Ungefaehr(0, v.Gebaeudeerloes, 1e-9);
            var vorher = k.Vermoegen;
            var e = w.Ausfuehren(k, new BetriebVerkaufen(grube.Name));
            Pruefe.Wahr(e.Erfolg && e.Meldung.Contains("Pacht"), e.Meldung);
            Pruefe.Ungefaehr(vorher + 4 * w.Markt["Silber"].Preis * 0.8, k.Vermoegen, 1e-9);

            Pruefe.Gleich(anzahl + 1, w.FreieLiegenschaften.Count());
            var frei = w.FreieLiegenschaften.Single(l => l.Name == "Grube am Silberberg");
            Pruefe.Gleich(true, frei.IstPacht);
            Pruefe.Gleich(Betriebsart.Mine, frei.Art);

            // Wiederpachten: leer, mit Ochsenkarren
            Pruefe.Wahr(w.Ausfuehren(k, new LiegenschaftErwerben("Grube am Silberberg")).Erfolg, "erneut pachten");
            var neu = w.Betriebe.Single(b => b.Name == "Grube am Silberberg");
            Pruefe.Wahr(neu != grube && neu.Belegt == 0 && neu.Lager.Count == 0, "nicht leer");
            Pruefe.Gleich("Ochsenkarren", neu.Karren.Single().Typ);
            Pruefe.Gleich(anzahl, w.FreieLiegenschaften.Count());
        }),

        ("Verkauf verboten: letzter Betrieb, während des Umbaus, Karren unterwegs, fremder Betrieb", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g, grube) = Schmied(sim);
            var fremd = w.KontoEroeffnen("Fremd");
            var e0 = w.Ausfuehren(fremd, new BetriebVerkaufen(g.Name));
            Pruefe.Wahr(!e0.Erfolg && e0.Meldung.Contains("gehört nicht"), e0.Meldung);

            g.Ausbauen(Betriebsart.Schmiede, 360);
            var e1 = w.Ausfuehren(k, new BetriebVerkaufen(g.Name));
            Pruefe.Wahr(!e1.Erfolg && e1.Meldung.Contains("umgebaut"), e1.Meldung);
            Pruefe.Wahr(w.VerkaufVorschau(g).Verboten?.Contains("umgebaut") == true, "Vorschau nennt den Umbau");

            grube.Einlagern("Eisen", 60); // genug für eine Fahrt: Der Ochsenkarren fährt los
            for (var i = 0; i < 5 && grube.Karren[0].Zustand == KarrenZustand.AmBetrieb; i++) sim.Tick();
            Pruefe.Wahr(grube.Karren[0].Zustand != KarrenZustand.AmBetrieb, "Karren fährt nicht los");
            var e2 = w.Ausfuehren(k, new BetriebVerkaufen(grube.Name));
            Pruefe.Wahr(!e2.Erfolg && e2.Meldung.Contains("unterwegs"), e2.Meldung);

            var (w2, k2, g2, grube2) = Schmied(NeueSimulation());
            Pruefe.Wahr(w2.Ausfuehren(k2, new BetriebVerkaufen(grube2.Name)).Erfolg, "Grube kündigen");
            var e3 = w2.Ausfuehren(k2, new BetriebVerkaufen(g2.Name));
            Pruefe.Wahr(!e3.Erfolg && e3.Meldung.Contains("letzte Betrieb"), e3.Meldung);
        }),

        ("Wiederkauf: Die verkaufte Schmiede kostet 6.000 und braucht den 3. Meistergrad; der Käufer bekommt sie leer mit Handkarren", () =>
        {
            var (w, k, g, _) = Schmied(NeueSimulation());
            g.Ausbauen(Betriebsart.Schmiede, 0);
            w.Einstellen(g, "Geselle", 3);
            g.Waechter = true;
            g.KarrenKaufen("Ochsenkarren");
            Pruefe.Wahr(w.Ausfuehren(k, new BetriebVerkaufen(g.Name)).Erfolg, "Verkauf");

            var kaeufer = Buerger(w, "Käufer", 20000);
            var e1 = w.Ausfuehren(kaeufer, new LiegenschaftErwerben("Gießerei am Weinmarkt"));
            Pruefe.Wahr(!e1.Erfolg && e1.Meldung.Contains("3. Meistergrad"), e1.Meldung); // ohne Handwerk: Grad 0
            kaeufer.BerufLernen("Schmied");
            kaeufer.GradSetzen("Schmied", 2);
            Pruefe.Wahr(w.LiegenschaftErwerbVerboten(kaeufer, "Gießerei am Weinmarkt")?.Contains("3. Meistergrad") == true, "Grad 2 reicht nicht");
            kaeufer.GradSetzen("Schmied", 3);
            Pruefe.Gleich(null, w.LiegenschaftErwerbVerboten(kaeufer, "Gießerei am Weinmarkt"));

            Betrieb? erworben = null;
            w.BetriebErworben += b => erworben = b;
            var vorher = kaeufer.Vermoegen;
            var e2 = w.Ausfuehren(kaeufer, new LiegenschaftErwerben("Gießerei am Weinmarkt"));
            Pruefe.Wahr(e2.Erfolg, e2.Meldung);
            Pruefe.Ungefaehr(vorher - 6000, kaeufer.Vermoegen, 1e-9);
            Pruefe.Gleich(Betriebsart.Schmiede, erworben!.Art);
            Pruefe.Gleich(kaeufer, erworben.Konto);
            Pruefe.Gleich(0, erworben.Belegt);
            Pruefe.Gleich(false, erworben.Waechter);
            Pruefe.Gleich("Handkarren", erworben.Karren.Single().Typ);
            Pruefe.Wahr(w.FreieLiegenschaften.All(l => l.Name != "Gießerei am Weinmarkt"), "noch frei");

            // Ein Tischler darf sie nicht kaufen – nach erneutem Verkauf
            w.BetriebEroeffnen("Grube Käufer", Betriebsart.Mine, kaeufer);
            Pruefe.Wahr(w.Ausfuehren(kaeufer, new BetriebVerkaufen("Gießerei am Weinmarkt")).Erfolg, "erneuter Verkauf");
            var tischler = Buerger(w, "Tischler", 20000);
            tischler.BerufLernen("Tischler");
            var e3 = w.Ausfuehren(tischler, new LiegenschaftErwerben("Gießerei am Weinmarkt"));
            Pruefe.Wahr(!e3.Erfolg && e3.Meldung.Contains("beherrscht dieses Handwerk nicht"), e3.Meldung);
        }),

        ("Verkauf: Löhne, Wächter und Unterhalt sind anteilig bis zum Verkauf fällig", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g, _) = Schmied(sim);
            w.Einstellen(g, "Geselle", 2);
            g.Waechter = true;
            for (var i = 0; i < Spielzeit.MinutenProTag / 2; i++) sim.Tick();
            // Der Handkarren holt Zutaten – verkauft wird, sobald er wieder am Betrieb steht.
            for (var i = 0; i < 300 && g.Karren[0].Zustand != KarrenZustand.AmBetrieb; i++) sim.Tick();
            var anteil = sim.Zeit.TagesFortschritt;
            var loehne = 2 * w.Daten.Arbeitskraefte["Geselle"].Kosten + w.Daten.Gauner!.WaechterKosten;
            var v = w.VerkaufVorschau(g);
            Pruefe.Ungefaehr((loehne + 80) * anteil, v.Abrechnung, 1e-9);
            var (personal, fix) = (k.Laufend.Personal, k.Laufend.Fixkosten);
            Pruefe.Wahr(w.Ausfuehren(k, new BetriebVerkaufen(g.Name)).Erfolg, "Verkauf");
            Pruefe.Ungefaehr(personal + loehne * anteil, k.Laufend.Personal, 1e-9);
            Pruefe.Ungefaehr(fix + 80 * anteil, k.Laufend.Fixkosten, 1e-9);
        }),

        ("Verkauf einer Geldleihe: Die Kasse kommt zurück ins Bargeld, ohne Erlös", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Wechsler", startvermoegen: 5000);
            var stube = w.BetriebEroeffnen("Wechselstube Wechsler", Betriebsart.Wechselstube, k);
            w.BetriebEroeffnen("Gießerei Wechsler", Betriebsart.Giesserei, k);
            Pruefe.Wahr(w.Ausfuehren(k, new KasseEinlegen(stube.Name, 1000)).Erfolg, "Kasse");
            Pruefe.Ungefaehr(1000, w.VerkaufVorschau(stube).Kasse, 1e-9);
            Pruefe.Wahr(w.Ausfuehren(k, new BetriebVerkaufen(stube.Name)).Erfolg, "Verkauf");
            Pruefe.Ungefaehr(4000 + 1000 + 2625, k.Vermoegen, 1e-9); // Liegenschaft „Wechselstube am Markt“ kostet 3.500
            Pruefe.Ungefaehr(0, k.Forderungen, 1e-9);
            Pruefe.Ungefaehr(0, w.Bank.Kasse(stube), 1e-9);
        }),

        ("Verkauf: Eine bestellte Bande rührt den verkauften Betrieb nicht an", () =>
        {
            var sim = NeueSimulation();
            var (w, k, g, _) = Schmied(sim);
            var feind = w.KontoEroeffnen("Feind", startvermoegen: 5000);
            g.Einlagern("Silberring", 10);
            var e = w.Ausfuehren(feind, new Beauftragen(k.Inhaber, Gaunerstueck.Diebstahl, g.Name));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Wahr(w.Ausfuehren(k, new BetriebVerkaufen(g.Name)).Erfolg, "Verkauf");
            var meldungen = new List<string>();
            w.Meldung += meldungen.Add;
            while (sim.Zeit.MinuteImTag <= Reichsgilde.Core.Gauner.Unterwelt.Nachtstunde) sim.Tick();
            Pruefe.Wahr(meldungen.Any(m => m.Contains("gehört nicht mehr")), string.Join(" | ", meldungen));
        }),

        ("Speichern: verkaufte Betriebe bleiben mit Stufe und Preis frei; alte Spielstände ohne Verkäufe laden weiter", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var g = w.Betriebe.Single(b => b.Konto == spieler);
            w.BetriebEroeffnen("Grube Hammerschmid", Betriebsart.Mine, spieler);
            g.Ausbauen(Betriebsart.Schmiede, 0);
            Pruefe.Wahr(w.Ausfuehren(spieler, new BetriebVerkaufen(g.Name)).Erfolg, "Verkauf");

            var zeitpunkt = new DateTime(2026, 10, 16, 12, 0, 0);
            var json = Speicherung.Speichern(sim, spieler, zeitpunkt);
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt));
            var frei = geladen.Wirtschaft!.FreieLiegenschaften.Single(l => l.Name == g.Name);
            Pruefe.Gleich(Betriebsart.Schmiede, frei.Art);
            Pruefe.Ungefaehr(6000, frei.Kaufpreis, 1e-9);
            Pruefe.Gleich(3, frei.MindestGrad);
            Pruefe.Gleich(true, frei.Bebaut);
            Pruefe.Ungefaehr(4500, geladenerSpieler!.Laufend.Verkaeufe, 1e-9);

            // Ein alter Spielstand: ohne verkaufte Betriebe, ohne Posten „Verkaeufe“ und ohne Abschnitt „verkauf“ in den Daten
            var (alt, altSpieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value, new NeuePartie());
            var knoten = JsonNode.Parse(Speicherung.Speichern(alt, altSpieler))!;
            knoten["Daten"]!.AsObject().Remove("Verkauf");
            knoten["Wirtschaft"]!.AsObject().Remove("Abgegeben");
            foreach (var konto in knoten["Wirtschaft"]!["Konten"]!.AsArray())
                konto!["Laufend"]!.AsObject().Remove("Verkaeufe");
            var (altGeladen, altGeladenerSpieler) = Speicherung.Laden(knoten.ToJsonString());
            var aw = altGeladen.Wirtschaft!;
            Pruefe.Gleich(Daten.Value.Liegenschaften.Count, aw.FreieLiegenschaften.Count());
            Pruefe.Ungefaehr(0.75, aw.Daten.Verkauf.Anteil, 1e-12);
            aw.BetriebEroeffnen("Grube Hammerschmid", Betriebsart.Mine, altGeladenerSpieler!);
            Pruefe.Wahr(aw.Ausfuehren(altGeladenerSpieler!, new BetriebVerkaufen("Gießerei am Weinmarkt")).Erfolg, "Verkauf im alten Spielstand");
        }),

        ("Daten: Anteil und Abschlag des Verkaufs werden geprüft; zu kaufen gibt es in den Daten weiter nur Stufe 1", () =>
        {
            var json = File.ReadAllText(Datenpfad.Wirtschaft());
            var knoten = JsonNode.Parse(json, documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!;
            knoten["verkauf"] = new JsonObject { ["anteil"] = 1.5, ["warenAbschlag"] = -0.1 };
            var fehler = Fehlerliste(() => Wirtschaftsdaten.AusJson(knoten.ToJsonString()));
            Pruefe.Wahr(fehler.Any(f => f.Contains("verkauf.anteil")) && fehler.Any(f => f.Contains("verkauf.warenAbschlag")), string.Join(" | ", fehler));

            knoten["verkauf"] = new JsonObject { ["anteil"] = 0.75, ["warenAbschlag"] = 0.2 };
            knoten["liegenschaften"]!.AsArray().Add(new JsonObject { ["name"] = "Schmiede am Tor", ["art"] = "Schmiede", ["kaufpreis"] = 6000 });
            fehler = Fehlerliste(() => Wirtschaftsdaten.AusJson(knoten.ToJsonString()));
            Pruefe.Wahr(fehler.Any(f => f.Contains("nur Werkstätten der Stufe 1")), string.Join(" | ", fehler));
        }),
    };
}
