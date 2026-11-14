using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Ratsbaum PR 1 (Nutzer, 11.11.2026): Kern-Abfragen für den Amtsbaum – <see cref="Stadtrat.Wurzelaemter"/>, <see cref="Stadtrat.Untergebene"/>,
/// <see cref="Stadtrat.BewerbungPruefen"/> (eine Prüfung für Anzeige und Befehl), <see cref="Stadtrat.BewerberFuer"/> – und der Befehl
/// <see cref="BewerbungZurueckziehen"/> (F8: ohne AP-Erstattung). Jeder Test mit Gegenprobe.
/// </summary>
static class AmtsbaumTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));
    static readonly Lazy<Wirtschaftsdaten> OhneAp = new(() => Wirtschaftsdaten.AusJson(Datenpfad.OhneAktionspunkte()));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    /// <summary>Ein Haus im gewünschten Stand (0 Gemeiner, 1 Bürger, 2 Patrizier) mit Aktionspunkten.</summary>
    static Konto Haus(Stadtwirtschaft w, string name, int stand, int ap = 12)
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 1_000_000);
        k.Aktionspunkte.Setzen(50);
        for (var i = 0; i < stand; i++)
            Ok(w, Wohnhaushilfe.MitWohnhaus(w, k), new StandErwerben());
        k.Aktionspunkte.Setzen(ap);
        return k;
    }

    static void Ok(Stadtwirtschaft w, Konto k, Befehl befehl)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(e.Erfolg, e.Meldung);
    }

    static void Abgelehnt(Stadtwirtschaft w, Konto k, Befehl befehl, string grund)
    {
        var e = w.Ausfuehren(k, befehl);
        Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains(grund), $"erwartet abgelehnt mit „{grund}“, war: {e.Erfolg} – {e.Meldung}");
    }

    static string Namen(IEnumerable<Amt> aemter) => string.Join(", ", aemter.Select(a => a.Name));

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Amtsbaum: Wurzel Bürgermeister, Untergebene aus „vorgesetzt“ in der Reihenfolge der Daten", () =>
        {
            var rat = NeueSimulation().Wirtschaft!.Rat;
            Pruefe.Gleich("Bürgermeister", Namen(rat.Wurzelaemter()));
            Pruefe.Gleich("Stadtrichter, Kämmerer", Namen(rat.Untergebene(rat.AmtNamens("Bürgermeister")!)));
            Pruefe.Gleich("Stadthauptmann", Namen(rat.Untergebene(rat.AmtNamens("Stadtrichter")!)));
            Pruefe.Gleich("Marktmeister", Namen(rat.Untergebene(rat.AmtNamens("Kämmerer")!)));
            Pruefe.Gleich("Gefängniswärter", Namen(rat.Untergebene(rat.AmtNamens("Stadthauptmann")!)));
            Pruefe.Gleich("Zöllner", Namen(rat.Untergebene(rat.AmtNamens("Marktmeister")!)));
            Pruefe.Gleich("", Namen(rat.Untergebene(rat.AmtNamens("Zöllner")!)));
            // Jedes Amt hängt genau einmal im Baum: als Wurzel oder als Untergebener genau eines Amtes.
            var imBaum = rat.Wurzelaemter().Concat(rat.Aemter.SelectMany(rat.Untergebene)).ToList();
            Pruefe.Gleich(7, imBaum.Count);
            Pruefe.Gleich(7, imBaum.Distinct().Count());
            // Dieselben Amt-Objekte wie in Aemter (die Oberfläche vergleicht per Verweis).
            Pruefe.Wahr(imBaum.All(a => rat.Aemter.Contains(a)), "fremde Amt-Objekte");

            // Gegenprobe: Hängt der Zöllner in den Daten unter dem Stadthauptmann, folgt der Baum den Daten.
            var umgehaengt = Json.Replace("\"vorgesetzt\": \"Marktmeister\"", "\"vorgesetzt\": \"Stadthauptmann\"");
            Pruefe.Wahr(umgehaengt != Json, "Zöllner-Zeile nicht gefunden");
            var rat2 = NeueSimulation(Wirtschaftsdaten.AusJson(umgehaengt)).Wirtschaft!.Rat;
            Pruefe.Gleich("Gefängniswärter, Zöllner", Namen(rat2.Untergebene(rat2.AmtNamens("Stadthauptmann")!)));
            Pruefe.Gleich("", Namen(rat2.Untergebene(rat2.AmtNamens("Marktmeister")!)));
        }),

        ("Amtsbaum: BewerbungPruefen listet alle Voraussetzungen, grün und rot", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var rat = w.Rat;
            var buerger = Haus(w, "Bürgerhaus", 1);
            var ok = rat.BewerbungPruefen(buerger, "Zöllner");
            Pruefe.Gleich("Aktionspunkte, NochNichtBeworben, Stand, Stufe", string.Join(", ", ok.Select(v => v.Bedingung)));
            Pruefe.Wahr(ok.All(v => v.Erfuellt), string.Join(" | ", ok.Select(v => v.Text)));
            Pruefe.Wahr(ok.Single(v => v.Bedingung == Bewerbungsbedingung.Aktionspunkte).Text.Contains("Kostet 1 AP"), "AP-Text");

            // Gegenproben: je eine Voraussetzung fehlt – genau sie ist rot, mit verständlichem Text.
            var gemeiner = Haus(w, "Gemeinerhaus", 0);
            var stand = rat.BewerbungPruefen(gemeiner, "Zöllner");
            Pruefe.Gleich("Stand", string.Join(", ", stand.Where(v => !v.Erfuellt).Select(v => v.Bedingung)));
            Pruefe.Wahr(stand.Single(v => !v.Erfuellt).Text.Contains("mindestens den Stand Bürger (Gemeinerhaus: Gemeiner)"), stand.Single(v => !v.Erfuellt).Text);

            var stufe = rat.BewerbungPruefen(buerger, "Marktmeister");
            Pruefe.Gleich("Stufe", string.Join(", ", stufe.Where(v => !v.Erfuellt).Select(v => v.Bedingung)));
            Pruefe.Wahr(stufe.Single(v => !v.Erfuellt).Text.Contains("zuerst ein Amt der Stufe 1"), "Stufe-Text");

            var patrizier = rat.BewerbungPruefen(buerger, "Bürgermeister"); // Stand und Stufe fehlen zugleich
            Pruefe.Gleich("Stand, Stufe", string.Join(", ", patrizier.Where(v => !v.Erfuellt).Select(v => v.Bedingung)));

            var arm = Haus(w, "Ohne AP", 1, ap: 0);
            var ap = rat.BewerbungPruefen(arm, "Zöllner");
            Pruefe.Gleich("Aktionspunkte", string.Join(", ", ap.Where(v => !v.Erfuellt).Select(v => v.Bedingung)));
            Pruefe.Wahr(ap.Single(v => !v.Erfuellt).Text.Contains("Nicht genug Aktionspunkte"), "AP-Text rot");

            Ok(w, buerger, new AmtBewerben("Zöllner"));
            var schon = rat.BewerbungPruefen(buerger, "Gefängniswärter");
            Pruefe.Gleich("NochNichtBeworben", string.Join(", ", schon.Where(v => !v.Erfuellt).Select(v => v.Bedingung)));
            Pruefe.Wahr(schon.Single(v => !v.Erfuellt).Text.Contains("schon beworben (als Zöllner)"), schon.Single(v => !v.Erfuellt).Text);

            var unbekannt = rat.BewerbungPruefen(buerger, "Henker");
            Pruefe.Gleich("Aktionspunkte, Amt", string.Join(", ", unbekannt.Select(v => v.Bedingung)));
            Pruefe.Gleich(false, unbekannt[1].Erfuellt);

            // Ohne Aktionspunkte in den Daten ist die Bewerbung frei – die Zeile bleibt, grün.
            var w2 = NeueSimulation(OhneAp.Value).Wirtschaft!;
            var frei = w2.Rat.BewerbungPruefen(Haus(w2, "Frei", 1, ap: 0), "Zöllner");
            Pruefe.Wahr(frei.All(v => v.Erfuellt), "ohne AP-Daten nicht frei");
            Pruefe.Gleich("Kostet keine Aktionspunkte.", frei.Single(v => v.Bedingung == Bewerbungsbedingung.Aktionspunkte).Text);
        }),

        ("Amtsbaum: Befehl und Anzeige laufen nie auseinander (jedes Amt, jeder Stand, mit und ohne AP)", () =>
        {
            var faelle = 0;
            foreach (var ap in new[] { 0, 12 })
            for (var stand = 0; stand <= 2; stand++)
            {
                var w = NeueSimulation().Wirtschaft!;
                var k = Haus(w, $"Haus {stand}", stand, ap);
                foreach (var amt in w.Rat.Aemter.Select(a => a.Name).Append("Henker").ToList())
                {
                    var liste = w.Rat.BewerbungPruefen(k, amt);
                    var vorher = k.Aktionspunkte.Vorrat;
                    var e = w.Ausfuehren(k, new AmtBewerben(amt));
                    Pruefe.Gleich(liste.All(v => v.Erfuellt), e.Erfolg, $"{amt}, Stand {stand}, AP {ap}");
                    if (!e.Erfolg)
                    {
                        Pruefe.Gleich(liste.First(v => !v.Erfuellt).Text, e.Meldung, $"{amt}, Stand {stand}, AP {ap}");
                        Pruefe.Gleich(vorher, k.Aktionspunkte.Vorrat, "abgelehnt kostet nichts");
                    }
                    else
                    {
                        faelle++;
                        Pruefe.Gleich(vorher - 1, k.Aktionspunkte.Vorrat, "Bewerbung kostet 1 AP");
                        Pruefe.Wahr(w.Rat.BewerbungPruefen(k, amt).Single(v => v.Bedingung == Bewerbungsbedingung.NochNichtBeworben) is { Erfuellt: false }, "danach beworben");
                    }
                }
            }
            Pruefe.Gleich(2, faelle); // Bürger und Patrizier mit AP: je die erste Stufe-1-Bewerbung
        }),

        ("Amtsbaum: BewerberFuer ordnet nach Kontonummer, nicht nach Name oder Eingang", () =>
        {
            var w = NeueSimulation(OhneAp.Value).Wirtschaft!;
            var zeta = Haus(w, "Zeta", 1);
            var alpha = Haus(w, "Alpha", 1);
            var mitte = Haus(w, "Mitte", 1);
            Ok(w, mitte, new AmtBewerben("Zöllner"));
            Ok(w, alpha, new AmtBewerben("Zöllner"));
            Ok(w, zeta, new AmtBewerben("Zöllner"));
            var zoellner = w.Rat.AmtNamens("Zöllner")!;
            Pruefe.Gleich("Zeta, Alpha, Mitte", string.Join(", ", w.Rat.BewerberFuer(zoellner).Select(k => k.Inhaber)));
            // Gegenprobe: Die Wahlliste ordnet nach Namen (plus Amtsinhaber) – die Bewerberreihe nicht.
            Pruefe.Gleich("Alpha, Mitte, Zeta, Hinnerk Tannenkamp", string.Join(", ", w.Rat.Kandidaten(zoellner).Select(k => k.Name)));
            Pruefe.Gleich(0, w.Rat.BewerberFuer(w.Rat.AmtNamens("Gefängniswärter")!).Count);
        }),

        ("Amtsbaum: Bewerbung zurückziehen – ohne AP-Erstattung, nur die eigene, nicht in der Sitzung", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Rückzieher", 1, ap: 5);
            var anderer = Haus(w, "Anderer", 1, ap: 5);
            Abgelehnt(w, k, new BewerbungZurueckziehen("Zöllner"), "nicht um das Amt Zöllner beworben");
            Ok(w, k, new AmtBewerben("Zöllner"));
            Ok(w, anderer, new AmtBewerben("Zöllner"));
            Pruefe.Gleich(4, k.Aktionspunkte.Vorrat);
            Abgelehnt(w, k, new BewerbungZurueckziehen("Gefängniswärter"), "nicht um das Amt Gefängniswärter beworben");
            Abgelehnt(w, k, new BewerbungZurueckziehen("Henker"), "gibt es nicht");
            Pruefe.Gleich("Zöllner", w.Rat.Bewerbungen[k]);

            var e = w.Ausfuehren(k, new BewerbungZurueckziehen("Zöllner"));
            Pruefe.Wahr(e.Erfolg && e.Meldung.Contains("zieht die Bewerbung um das Amt Zöllner zurück"), e.Meldung);
            Pruefe.Gleich(4, k.Aktionspunkte.Vorrat); // keine Erstattung (F8), der Rückzug selbst ist frei
            Pruefe.Wahr(!w.Rat.Bewerbungen.ContainsKey(k), "Bewerbung noch da");
            var zoellner = w.Rat.AmtNamens("Zöllner")!;
            Pruefe.Gleich("Anderer", string.Join(", ", w.Rat.BewerberFuer(zoellner).Select(b => b.Inhaber)));
            Pruefe.Wahr(w.Rat.Kandidaten(zoellner).All(c => c.Haus != k), "noch Kandidat");
            Pruefe.Gleich("Zöllner", w.Rat.Bewerbungen[anderer]); // die fremde Bewerbung bleibt
            Abgelehnt(w, k, new BewerbungZurueckziehen("Zöllner"), "nicht um das Amt Zöllner beworben");

            // Neu bewerben geht – und kostet wieder.
            Ok(w, k, new AmtBewerben("Gefängniswärter"));
            Pruefe.Gleich(3, k.Aktionspunkte.Vorrat);

            // In der Ratssitzung wird nur abgestimmt.
            w.RatssitzungAnhalten = _ => true;
            JahrBeenden(sim);
            Pruefe.Wahr(sim.Ratssitzung is not null, "keine Sitzung");
            Abgelehnt(w, anderer, new BewerbungZurueckziehen("Zöllner"), "Ratssitzung läuft");
        }),

        ("Amtsbaum: Wer zurückzieht, wird nicht gewählt (Gegenprobe: ohne Rückzug gewinnt er)", () =>
        {
            foreach (var zurueck in new[] { false, true })
            {
                var sim = NeueSimulation(OhneAp.Value);
                var w = sim.Wirtschaft!;
                var k = Haus(w, "Bewerber", 1);
                Ok(w, k, new AmtBewerben("Zöllner"));
                foreach (var sitz in w.Rat.Waehler(w.Rat.AmtNamens("Zöllner")!).Where(s => s.Ratsherr is not null))
                    Ok(w, k, new Schenken(sitz.Ratsherr!.Name, 1000));
                if (zurueck) Ok(w, k, new BewerbungZurueckziehen("Zöllner"));
                JahrBeenden(sim);
                Pruefe.Gleich(zurueck ? null : k, w.Rat.AmtNamens("Zöllner")!.Konto, zurueck ? "mit Rückzug" : "ohne Rückzug");
            }
        }),

        ("Amtsbaum: Rückzug im Rat lässt die Bewerbung als Domprediger stehen", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var k = Haus(w, "Prediger", 1);
            w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann, alter: 30, glaube: Konfession.Katholisch);
            k.BerufLernen("Prediger");
            var kapelle = w.BetriebEroeffnen("Kapelle Prediger", Betriebsart.Kirche, k, startkarren: "");
            w.Einstellen(kapelle, "Geselle", 1);
            Ok(w, k, new DomBewerben());
            Ok(w, k, new AmtBewerben("Zöllner"));
            Pruefe.Wahr(w.Domamt.Bewerbungen.Contains(k), "Dom-Bewerbung fehlt");
            Ok(w, k, new BewerbungZurueckziehen("Zöllner"));
            Pruefe.Wahr(!w.Rat.Bewerbungen.ContainsKey(k), "Ratsbewerbung noch da");
            Pruefe.Wahr(w.Domamt.Bewerbungen.Contains(k), "Dom-Bewerbung mit zurückgezogen");
            // Gegenprobe: Der Domprediger ist kein Amt des Stadtrats – zurückziehen lässt er sich so nicht.
            Abgelehnt(w, k, new BewerbungZurueckziehen(Reichsgilde.Core.Kirche.Domamt.Amtsname), "gibt es nicht");
            Pruefe.Wahr(w.Domamt.Bewerbungen.Contains(k), "Dom-Bewerbung weg");
        }),

        ("Amtsbaum: Ein Rückzug übersteht Speichern und Laden (die Bewerbung bleibt weg)", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = Haus(w, "Spieler", 1);
            var anderer = Haus(w, "Anderer", 1);
            Ok(w, k, new AmtBewerben("Zöllner"));
            Ok(w, anderer, new AmtBewerben("Gefängniswärter"));
            Ok(w, k, new BewerbungZurueckziehen("Zöllner"));
            var (geladen, spieler) = Speicherung.Laden(Speicherung.Speichern(sim, k));
            var rat = geladen.Wirtschaft!.Rat;
            Pruefe.Wahr(!rat.Bewerbungen.ContainsKey(spieler!), "Bewerbung wieder da");
            Pruefe.Gleich("Anderer", string.Join(", ", rat.BewerberFuer(rat.AmtNamens("Gefängniswärter")!).Select(b => b.Inhaber)));
        }),
    };
}
