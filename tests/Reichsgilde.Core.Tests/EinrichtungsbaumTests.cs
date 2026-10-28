using Reichsgilde.Core;
using Reichsgilde.Core.Gauner;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Tests für die Einrichtung als Talentbaum (PR #224): neue Stücke ab Stufe 3 (Werkstatt-Stück je Beruf, Ladentheke, Lehrlingsstube),
/// Spalten und Reihen, die Abfrage „Baum“ und die Einrichtung des Wohnhauses (Haushalt, Ansehen, Schutz).
/// </summary>
static class EinrichtungsbaumTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static Simulation NeueSimulation(int startwert = 1400) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: Daten.Value);

    static Befehlsergebnis Ok(Stadtwirtschaft w, Konto k, Befehl befehl)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(e.Erfolg, $"{befehl.GetType().Name} abgelehnt: {e.Meldung}");
        return e;
    }

    static void Abgelehnt(Stadtwirtschaft w, Konto k, Befehl befehl, string textInMeldung)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains(textInMeldung), $"{befehl.GetType().Name}: erwartet abgelehnt mit „{textInMeldung}“, war {e.Erfolg}: {e.Meldung}");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Einrichtung Stufe 3: Daten – je Beruf ein Werkstatt-Stück (−15 % für die Waren der Stufe 3, 1.500), Ladentheke (1.000), Lehrlingsstube (600); jedes Stück hat eine Spalte", () =>
        {
            var d = Daten.Value.Werkstatt!;
            Pruefe.Gleich("Werkstatt|Lager & Handel|Schutz & Personal", string.Join("|", d.Spalten));
            var stufe3 = new Dictionary<string, (string Kennung, string Waren)>
            {
                ["Schmied"] = ("Schleifstein", "Goldkette Prunkschwert"), ["Tischler"] = ("Drechselbank", "Schrank Prunktruhe"),
                ["Steinmetz"] = ("Steinsaege", "Steinfigur Grabmal"), ["Alchimist"] = ("Athanor", "Theriak Zinnober"),
                ["Parfumeur"] = ("Enfleurage", "Heiligenduft Schattendolch"), ["Wirt"] = ("Gewoelbekeller", "Festbier Schädelbrand"),
                ["Prediger"] = ("Skriptorium", "Stundenbuch Reliquiar"),
            };
            var arten = Daten.Value.Betriebsarten.Values;
            foreach (var (beruf, (kennung, waren)) in stufe3)
            {
                var e = d.Stueck(beruf, kennung)!;
                Pruefe.Gleich(3, e.AbStufe, kennung);
                Pruefe.Gleich(1500.0, e.Preis, kennung);
                Pruefe.Gleich("Werkzeug2", e.Voraussetzung, kennung);
                Pruefe.Ungefaehr(0.85, e.Wirkung.ZeitFaktor, 1e-12);
                Pruefe.Gleich(waren, string.Join(" ", e.Wirkung.Waren), kennung);
                // Genau die Waren, die erst die Stufe 3 herstellt.
                var drei = arten.Single(a => a.Beruf == beruf && a.Stufe == 3 && a.IstWerkstatt);
                var zwei = arten.Single(a => a.Beruf == beruf && a.Stufe == 2 && a.IstWerkstatt);
                Pruefe.Gleich(waren, string.Join(" ", drei.Produkte.Except(zwei.Produkte)), beruf);
                Pruefe.Gleich(1000.0, d.Stueck(beruf, "Ladentheke")!.Preis);
                Pruefe.Gleich("Regale", d.Stueck(beruf, "Ladentheke")!.Voraussetzung);
            }
            foreach (var (beruf, liste) in d.Einrichtung)
            {
                Pruefe.Gleich(600.0, d.Stueck(beruf, "Lehrlingsstube")!.Preis, beruf);
                Pruefe.Ungefaehr(1.5, d.Stueck(beruf, "Lehrlingsstube")!.Wirkung.ErfahrungFaktor, 1e-12);
                foreach (var e in liste) Pruefe.Wahr(e.Spalte is { } s && d.Spalten.Contains(s), $"{beruf}.{e.Kennung}: Spalte {e.Spalte}");
            }
            Pruefe.Wahr(d.Stueck("Dieb", "Ladentheke") is null && d.Stueck("Gardist", "Ladentheke") is null, "Dieb und Wache haben keinen Laden");
        }),

        ("Einrichtung Stufe 3: Tretrad-Schleifstein erst in der Goldschmiede nach dem Meisterwerkzeug; −15 % Zeit nur für Goldkette und Prunkschwert; alte Kennung Wasserhammer lädt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 20_000);
            var g = w.BetriebEroeffnen("Goldschmiede", Betriebsart.Goldschmiede, k, startkarren: "");
            Abgelehnt(w, k, new EinrichtungKaufen(g.Name, "Schleifstein"), "Erst braucht es: Meisterwerkzeug");
            Ok(w, k, new EinrichtungKaufen(g.Name, "Werkzeug1"));
            Ok(w, k, new EinrichtungKaufen(g.Name, "Werkzeug2"));
            var vorher = k.Vermoegen;
            Ok(w, k, new EinrichtungKaufen(g.Name, "Schleifstein"));
            Pruefe.Ungefaehr(1500, vorher - k.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(0.81 * 0.85, g.ZeitFaktor("Goldkette"), 1e-12);
            Pruefe.Ungefaehr(0.81, g.ZeitFaktor("Silberring"), 1e-12);
            var s = w.BetriebEroeffnen("Schmiede", Betriebsart.Schmiede, k, startkarren: "");
            Pruefe.Wahr(w.Werkstaetten.EinrichtungVerboten(s, "Schleifstein")!.Contains("erst ab Stufe 3"), "Stufe 2");
            Pruefe.Gleich("Tretrad-Schleifstein", w.Werkstaetten.Daten!.Stueck("Schmied", "Schleifstein")!.Name);
            // Ein Spielstand mit der früheren Kennung „Wasserhammer“ lädt als Schleifstein (Entscheidung des Nutzers vom 28.10.2026).
            var knoten = System.Text.Json.Nodes.JsonNode.Parse(Reichsgilde.Core.Speichern.Speicherung.Speichern(sim))!;
            var liste = knoten["Wirtschaft"]!["Betriebe"]!.AsArray().Single(b => (string?)b!["Name"] == g.Name)!["Einrichtung"]!.AsArray();
            var platz = liste.Select((x, i) => ((string?)x, i)).Single(x => x.Item1 == "Schleifstein").i;
            liste[platz] = "Wasserhammer";
            var g2 = Reichsgilde.Core.Speichern.Speicherung.Laden(knoten.ToJsonString()).Simulation.Wirtschaft!.Betriebe.Single(b => b.Name == g.Name);
            Pruefe.Wahr(g2.HatEinrichtung("Schleifstein") && !g2.HatEinrichtung("Wasserhammer"), string.Join(", ", g2.Einrichtung));
            Pruefe.Ungefaehr(0.81 * 0.85, g2.ZeitFaktor("Goldkette"), 1e-12);
        }),

        ("Einrichtung Stufe 3: Ladentheke – ein Viertel der Ware 5 % teurer, im Mittel 1,25 % mehr Erlös am Markt", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 20_000);
            var g = w.BetriebEroeffnen("Goldschmiede", Betriebsart.Goldschmiede, k, startkarren: "");
            var h = w.BetriebEroeffnen("Goldschmiede ohne", Betriebsart.Goldschmiede, k, startkarren: "");
            Abgelehnt(w, k, new EinrichtungKaufen(g.Name, "Ladentheke"), "Erst braucht es");
            Ok(w, k, new EinrichtungKaufen(g.Name, "Regale"));
            Ok(w, k, new EinrichtungKaufen(g.Name, "Ladentheke"));
            Pruefe.Ungefaehr(1.0125, g.PreisFaktor, 1e-12);
            var u1 = k.Laufend.Umsatz;
            g.Verkaufen("Silberring", 1, w.Markt, steuersatz: 0);
            var mit = k.Laufend.Umsatz - u1;
            var preis = w.Markt["Silberring"].Preis;
            var u2 = k.Laufend.Umsatz;
            h.Verkaufen("Silberring", 1, w.Markt, steuersatz: 0);
            var ohne = k.Laufend.Umsatz - u2;
            Pruefe.Wahr(mit > ohne * 1.0124 && mit < ohne * 1.03, $"mit {mit}, ohne {ohne} (Preis {preis})");
            Pruefe.Wahr(w.Werkstaetten.Einrichtungsliste(g).Single(e => e.Kennung == "Ladentheke").Wirkung.Contains("bis zu 25 % der Ware"), "Text");
        }),

        ("Talentbaum: Abfrage „Baum“ – drei Spalten, drei Reihen (Gebäudestufen), Zustand gekauft, kaufbar, nicht jetzt (Gulden) oder gesperrt (Stufe, Voraussetzung), Amortisation", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 600);
            var g = w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k, startkarren: "");
            w.Einstellen(g, "Geselle", 2);
            Ok(w, k, new ProduktWaehlen(g.Name, "Rapier")); // der Blasebalg hilft beim Rapier
            Ok(w, k, new EinrichtungKaufen(g.Name, "Werkzeug1"));
            var baum = w.Werkstaetten.Baum(g);
            Pruefe.Gleich("Werkstatt|Lager & Handel|Schutz & Personal", string.Join("|", baum.Spalten));
            Pruefe.Gleich(3, baum.Reihen);
            Pruefe.Gleich(1, baum.Stufe);
            Pruefe.Gleich(10, baum.Stuecke.Count);
            BaumStueck Stueck(string kennung) => baum.Stuecke.Single(s => s.Kennung == kennung);
            Pruefe.Gleich(Baumzustand.Gekauft, Stueck("Werkzeug1").Zustand);
            Pruefe.Gleich(Baumzustand.Gesperrt, Stueck("Werkzeug2").Zustand); // erst ab Stufe 2
            Pruefe.Gleich(Baumzustand.Gesperrt, Stueck("Schleifstein").Zustand);
            Pruefe.Gleich(Baumzustand.Kaufbar, Stueck("Schuerzen").Zustand); // 80 Gulden – 300 sind noch da
            Pruefe.Gleich(Baumzustand.NichtJetzt, Stueck("Blasebalg").Zustand); // 350 Gulden: Stufe passt, das Geld nicht
            Pruefe.Gleich("Werkzeug2", Stueck("Schleifstein").Voraussetzung);
            Pruefe.Gleich(3, Stueck("Schleifstein").Reihe);
            Pruefe.Gleich("Lager & Handel", Stueck("Ladentheke").Spalte);
            Pruefe.Gleich("Schutz & Personal", Stueck("Lehrlingsstube").Spalte);
            var blasebalg = Stueck("Blasebalg");
            Pruefe.Wahr(blasebalg.MehrgewinnJeJahr > 0 && blasebalg.Amortisationsjahre is { } j && Math.Abs(j - 350 / blasebalg.MehrgewinnJeJahr) < 1e-9,
                $"Blasebalg {blasebalg.MehrgewinnJeJahr} / {blasebalg.Amortisationsjahre}");
            Pruefe.Gleich(null, Stueck("Regale").Amortisationsjahre); // ohne Gulden-Wirkung
            // Umland und Schreiber haben keinen Baum.
            var m = w.BetriebEroeffnen("Grube", Betriebsart.Mine, k, startkarren: "");
            Pruefe.Gleich(0, w.Werkstaetten.Baum(m).Stuecke.Count);
        }),

        ("Wohnhaus-Einrichtung: Daten – Haushalt (Hausapotheke, Vorratskammer, Küche), Ansehen (Wappen, Stube, Bibliothek), Schutz (Hauswächter, Riegel, Hund)", () =>
        {
            var d = Daten.Value.Wohnen!;
            Pruefe.Gleich("Haushalt|Ansehen|Schutz", string.Join("|", d.Spalten));
            Pruefe.Gleich("Hausapotheke Vorratskammer Kueche", string.Join(" ", d.Einrichtung.Where(e => e.Spalte == "Haushalt").Select(e => e.Kennung)));
            Pruefe.Gleich("Hauswappen Stube Bibliothek", string.Join(" ", d.Einrichtung.Where(e => e.Spalte == "Ansehen").Select(e => e.Kennung)));
            Pruefe.Gleich("Hauswaechter Riegel Hund", string.Join(" ", d.Einrichtung.Where(e => e.Spalte == "Schutz").Select(e => e.Kennung)));
            Pruefe.Gleich("1 2 3 1 2 3 1 2 3", string.Join(" ", d.Spalten.SelectMany(s => d.Einrichtung.Where(e => e.Spalte == s)).Select(e => e.AbStufe)));
            Pruefe.Gleich("Hausapotheke", d.Stueck("Hausapotheke")!.Vorhanden);
            Pruefe.Gleich("Hauswaechter", d.Stueck("Hauswaechter")!.Vorhanden);
        }),

        ("Wohnhaus-Einrichtung: kaufen nach Stufe des Wohnhauses und Voraussetzung; Vorratskammer spart 20 % Haushalt, Wappen +2 Ansehen, Riegel halbiert den Einbruch", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 20_000);
            w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k, startkarren: "");
            w.Familien.Gruenden(k, Volk.Mensch, Reichsgilde.Core.Dynastie.Geschlecht.Mann);
            Abgelehnt(w, k, new WohnhausEinrichtungKaufen("Vorratskammer"), "passt erst in ein Bürgerhaus");
            Abgelehnt(w, k, new WohnhausEinrichtungKaufen("Hausapotheke"), "hat jedes Haus");
            Abgelehnt(w, k, new WohnhausEinrichtungKaufen("Stube"), "passt erst");
            var ansehen = w.Rat.Ansehen(k);
            Ok(w, k, new WohnhausEinrichtungKaufen("Hauswappen"));
            Pruefe.Ungefaehr(ansehen + 2 - 0.25 * w.Daten.Politik.Gunst.AnsehenJe1000Gulden, w.Rat.Ansehen(k), 1e-9); // 250 Gulden weniger Vermögen
            Pruefe.Ungefaehr(20_000 - 250, k.Vermoegen, 1e-9);
            Abgelehnt(w, k, new WohnhausEinrichtungKaufen("Hauswappen"), "hat schon");
            k.WohnhausStufe = 1; // Bürgerhaus
            var einbruch = w.Gauner.ErfolgschanceWohnhaus(Gaunerstueck.Hauseinbruch, k);
            Ok(w, k, new WohnhausEinrichtungKaufen("Riegel"));
            Pruefe.Ungefaehr(einbruch / 2, w.Gauner.ErfolgschanceWohnhaus(Gaunerstueck.Hauseinbruch, k), 1e-12);
            Ok(w, k, new WohnhausEinrichtungKaufen("Vorratskammer"));
            Pruefe.Ungefaehr(0.8, w.Wohnen.HaushaltFaktor(k), 1e-12);
            Ok(w, k, new WohnhausEinrichtungKaufen("Hauswaechter")); // der Hauswächter wird eingestellt
            Pruefe.Wahr(k.Hauswaechter, "Hauswächter");
            var baum = w.Wohnen.Baum(k);
            Pruefe.Gleich(2, baum.Stufe);
            BaumStueck Stueck(string kennung) => baum.Stuecke.Single(s => s.Kennung == kennung);
            Pruefe.Gleich(Baumzustand.Gekauft, Stueck("Hausapotheke").Zustand);
            Pruefe.Gleich(Baumzustand.Gekauft, Stueck("Hauswaechter").Zustand);
            Pruefe.Wahr(Stueck("Hauswaechter").Jaehrlich && Stueck("Hauswaechter").Preis > 0, "Lohn im Jahr");
            Pruefe.Gleich(Baumzustand.Gekauft, Stueck("Riegel").Zustand);
            Pruefe.Gleich(Baumzustand.Kaufbar, Stueck("Stube").Zustand);
            Pruefe.Gleich(Baumzustand.Gesperrt, Stueck("Kueche").Zustand);
            // Haushalt und Unterhalt am Jahresende × 0,8.
            var f = w.Familien.Von(k)!;
            var kosten = w.Familien.Haushaltskosten(f) + w.Familien.Ausbildungskosten(f);
            var vorher = k.Laufend.Familie;
            w.Familien.UnterhaltZahlen();
            Pruefe.Ungefaehr(kosten * 0.8, k.Laufend.Familie - vorher, 1e-9);
        }),

        ("Wohnhaus-Einrichtung: übersteht Speichern und Laden; alte Spielstände ohne Feld haben keine", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Haus Fuchs", startvermoegen: 20_000);
            w.BetriebEroeffnen("Gießerei", Betriebsart.Giesserei, k, startkarren: "");
            Ok(w, k, new WohnhausEinrichtungKaufen("Hauswappen"));
            var json = Reichsgilde.Core.Speichern.Speicherung.Speichern(sim);
            var k2 = Reichsgilde.Core.Speichern.Speicherung.Laden(json).Simulation.Wirtschaft!.Konten.Single(x => x.Inhaber == k.Inhaber);
            Pruefe.Gleich("Hauswappen", string.Join(" ", k2.WohnhausEinrichtung));
            var alt = System.Text.RegularExpressions.Regex.Replace(json, @"""WohnhausEinrichtung"": \[[^\]]*\],?", "");
            var k3 = Reichsgilde.Core.Speichern.Speicherung.Laden(alt).Simulation.Wirtschaft!.Konten.Single(x => x.Inhaber == k.Inhaber);
            Pruefe.Gleich(0, k3.WohnhausEinrichtung.Count);
        }),
    };
}
