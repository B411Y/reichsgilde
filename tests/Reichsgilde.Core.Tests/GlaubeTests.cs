using Reichsgilde.Core.Zeit;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für Glaube und Konfessionen: Startglaube, Glaubensnähe im Rat, Mischehe, Konvertieren, Messe, Bekehrung, Stadtglaube, KI, Speichern.</summary>
static class GlaubeTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Ein Haus mit Familie, Geld und Aktionspunkten.</summary>
    static (Stadtwirtschaft W, Konto K, Familie F) Haus(Simulation sim, string name, Konfession glaube, double geld = 20_000, Volk volk = Volk.Mensch)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: geld);
        var f = w.Familien.Gruenden(k, volk, Geschlecht.Mann, alter: 30, glaube: glaube);
        k.Aktionspunkte.Setzen(12);
        return (w, k, f);
    }

    /// <summary>Ein Prediger mit eigener Kirche (ein Geselle, keine Karren).</summary>
    static (Konto K, Betrieb Kirche) Prediger(Simulation sim, string name, Konfession glaube)
    {
        var (w, k, _) = Haus(sim, name, glaube);
        var b = w.BetriebEroeffnen($"Kirche {name}", Betriebsart.Kirche, k, startkarren: "");
        w.Einstellen(b, "Geselle", 1);
        return (k, b);
    }

    /// <summary>Setzt das Haus auf den Sitz des Zöllners (statt Hinnerk Tannenkamp, katholisch).</summary>
    static void InDenRat(Stadtwirtschaft w, Konto k)
    {
        var amt = w.Rat.AmtNamens("Zöllner")!;
        amt.Ratsherr = null;
        amt.Konto = k;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: Startglaube je Volk (Menschen und Elben katholisch, Zwerge und Halblinge evangelisch, Orks Naturglaube); Rat 7 katholisch, 6 evangelisch; Naturglaube nur für Orks und Oger", () =>
        {
            var d = Daten.Value.Glaube!;
            Pruefe.Gleich(Konfession.Katholisch, d.Startglaube(Volk.Mensch));
            Pruefe.Gleich(Konfession.Katholisch, d.Startglaube(Volk.Elb));
            Pruefe.Gleich(Konfession.Evangelisch, d.Startglaube(Volk.Zwerg));
            Pruefe.Gleich(Konfession.Evangelisch, d.Startglaube(Volk.Halbling));
            Pruefe.Gleich(Konfession.Naturglaube, d.Startglaube(Volk.Ork));
            var rat = Daten.Value.Politik.Ratsherren;
            Pruefe.Gleich(7, rat.Count(r => r.Glaube == Konfession.Katholisch));
            Pruefe.Gleich(6, rat.Count(r => r.Glaube == Konfession.Evangelisch));
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Replace("\"Mensch\": { \"Katholisch\": 0.6, \"Evangelisch\": 0.4 }",
                "\"Mensch\": { \"Katholisch\": 0.6, \"Naturglaube\": 0.4 }")), "Naturglaube");

            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(Konfession.Katholisch, w.Glaube.GlaubeVon(spieler));
            Pruefe.Gleich(Konfession.Evangelisch, w.Glaube.GlaubeVon(w.Konten.Single(k => k.Inhaber == "Haus Eisenbart")));
            Pruefe.Gleich(Konfession.Katholisch, w.Glaube.GlaubeVon(w.Konten.Single(k => k.Inhaber == "Haus Eichwald")));
            Pruefe.Gleich(Konfession.Evangelisch, w.Glaube.GlaubeVon(w.Konten.Single(k => k.Inhaber == "Haus Kieselbach")));
            var (sim2, spieler2) = Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Glaube: Konfession.Evangelisch));
            Pruefe.Gleich(Konfession.Evangelisch, sim2.Wirtschaft!.Glaube.GlaubeVon(spieler2));
            Pruefe.WirftMitMeldung(() => Partieaufbau.Erstellen(Partieeinstellungen.Standard, Daten.Value, new NeuePartie(Glaube: Konfession.Naturglaube)), "Orks und Ogern");
        }),

        ("Rat: Glaubensbruder +5, andere Konfession −3, Heide −8 – als Grund der Meinung in den Wahlpunkten (PR #231)", () =>
        {
            var sim = NeueSimulation();
            var (w, k, f) = Haus(sim, "Haus Kreuz", Konfession.Katholisch);
            var kath = w.Rat.RatsherrNamens("Ulrich Wendelstein")!;
            var ev = w.Rat.RatsherrNamens("Konrad Eisenfaust")!;
            Pruefe.Gleich(5.0, w.Glaube.Naehe(kath, k));
            Pruefe.Gleich(-3.0, w.Glaube.Naehe(ev, k));
            var amt = w.Rat.Aemter.First(a => a.Stufe == 1);
            Pruefe.Ungefaehr(5, w.Meinungen.Gruende(kath, f.Oberhaupt).Single(g => g.Text.StartsWith("Gleicher Glaube")).Wert, 1e-9);
            Pruefe.Ungefaehr(-3, w.Meinungen.Gruende(ev, f.Oberhaupt).Single(g => g.Text.StartsWith("Anderer Glaube")).Wert, 1e-9);
            Pruefe.Ungefaehr(w.Rat.Ansehen(k) + w.Rat.Meinung(kath, k) + w.Familien.RhetorikPunkte(k),
                w.Rat.Punkte(kath, amt, k), 1e-9);
            f.Oberhaupt.Glaube = Konfession.Naturglaube;
            Pruefe.Gleich(-8.0, w.Glaube.Naehe(kath, k));
            Pruefe.Gleich(-8.0, w.Glaube.Naehe(ev, k));
            Pruefe.Ungefaehr(-8, w.Meinungen.Gruende(kath, f.Oberhaupt).Single(g => g.Text == "Ein Heide").Wert, 1e-9);
        }),

        ("Mischehe: Der Partner tritt zum Glauben des Hauses über – bei den Ratsherren seines alten Glaubens −2; Kinder im Glauben des Hauses", () =>
        {
            var sim = NeueSimulation();
            var (w, k, f) = Haus(sim, "Haus Kreuz", Konfession.Katholisch, geld: 50_000);
            var kandidat = w.Familien.Kandidaten.First(x => w.Familien.WerbenVerboten(k, x) is null);
            kandidat.Person.GlaubeSetzen(Konfession.Evangelisch);
            w.Familien.ZuneigungSchenken(k, kandidat.Person.Nummer, 500);
            var ergebnis = w.Ausfuehren(k, new Heiraten(kandidat.Person.Nummer));
            Pruefe.Wahr(ergebnis.Erfolg, ergebnis.Meldung);
            Pruefe.Gleich(Konfession.Katholisch, f.Ehepartner!.Glaube);
            Pruefe.Gleich(Konfession.Evangelisch, f.Ehepartner.Herkunftsglaube);
            Pruefe.Wahr(w.Familien.DiesesJahr.Any(e => e.Text.Contains("tritt zum katholischen Glauben über")), "Übertritt nicht gemeldet");
            Pruefe.Gleich(5.0, w.Glaube.Naehe(w.Rat.RatsherrNamens("Ulrich Wendelstein")!, k));
            Pruefe.Gleich(-5.0, w.Glaube.Naehe(w.Rat.RatsherrNamens("Konrad Eisenfaust")!, k));
            Pruefe.Gleich(Konfession.Katholisch, w.Familien.KindAufnehmen(k, 3).Glaube);
        }),

        ("Konvertieren: 2 AP und 300 Gulden, das ganze Haus wechselt; 3 Ansehen weniger bis Ende des nächsten Jahres; Heiden lassen sich taufen", () =>
        {
            var sim = NeueSimulation();
            var (w, k, f) = Haus(sim, "Haus Wende", Konfession.Katholisch);
            var kind = w.Familien.KindAufnehmen(k, 5);
            var ansehen = w.Rat.Ansehen(k);
            var geld = k.Vermoegen;
            Pruefe.Wahr(!w.Ausfuehren(k, new Konvertieren(Konfession.Katholisch)).Erfolg, "schon katholisch");
            Pruefe.Wahr(!w.Ausfuehren(k, new Konvertieren(Konfession.Naturglaube)).Erfolg, "Naturglaube");
            var e = w.Ausfuehren(k, new Konvertieren(Konfession.Evangelisch));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(10, k.Aktionspunkte.Vorrat);
            Pruefe.Ungefaehr(geld - 300, k.Vermoegen, 1e-9);
            Pruefe.Wahr(f.Oberhaupt.Glaube == Konfession.Evangelisch && kind.Glaube == Konfession.Evangelisch, "ganzes Haus");
            Pruefe.Gleich(Konfession.Katholisch, f.Oberhaupt.Herkunftsglaube);
            Pruefe.Ungefaehr(ansehen - 3 - 300 / 1000.0 * 0.4, w.Rat.Ansehen(k), 1e-9); // dazu weniger Vermögen
            JahrBeenden(sim);
            Pruefe.Gleich(3.0, w.Glaube.AnsehenAbzug(k));
            JahrBeenden(sim);
            Pruefe.Gleich(0.0, w.Glaube.AnsehenAbzug(k));

            var (_, ork, of) = Haus(sim, "Haus Grunz", Konfession.Katholisch);
            of.Oberhaupt.Glaube = Konfession.Naturglaube;
            var taufe = w.Ausfuehren(ork, new Konvertieren(Konfession.Katholisch));
            Pruefe.Wahr(taufe.Erfolg && taufe.Meldung.Contains("taufen"), taufe.Meldung);
        }),

        ("Messe: Jede Konfession geht in ihre Kirche (55 % / 45 %); ohne eigene Kirche in eine fremde, aber nur mit halber Spende", () =>
        {
            var sim = NeueSimulation();
            var (_, kath) = Prediger(sim, "Familie Rom", Konfession.Katholisch);
            var w = sim.Wirtschaft!;
            Pruefe.Ungefaehr(200, w.Kirchenwesen.ErwarteteGlaeubige(kath), 1e-9);
            Pruefe.Ungefaehr(200 * 0.55 * 2.2 + 200 * 0.45 * 2.2 * 0.5, w.Kirchenwesen.ErwarteteKollekte(kath), 1e-9); // 2,2 je Gläubigem seit PR #177
            var (_, ev) = Prediger(sim, "Familie Wittenberg", Konfession.Evangelisch);
            for (var i = 0; i < 10 * 60 + 1 - Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // nach der Messe um 10 Uhr
            Pruefe.Ungefaehr(110, w.Kirchenwesen.DiesesJahr(kath).Glaeubige, 1e-9);
            Pruefe.Ungefaehr(90, w.Kirchenwesen.DiesesJahr(ev).Glaeubige, 1e-9);
            Pruefe.Ungefaehr(90 * 2.2, w.Kirchenwesen.DiesesJahr(ev).Kollekte, 1e-9);
        }),

        ("Bekehrungspredigt: nur Prediger, einmal im Jahr; +4 Punkte × Rhetorik für die eigene Gemeinde, +3 Gunst bei Ratsherren des Glaubens; die Anteile kehren jährlich um 10 % zurück", () =>
        {
            var sim = NeueSimulation();
            var (w, laie, _) = Haus(sim, "Haus Laie", Konfession.Katholisch);
            Pruefe.WirftMitMeldung(() => throw new Exception(w.Ausfuehren(laie, new Bekehrungspredigt()).Meldung), "nur ein Prediger");
            var (k, _) = Prediger(sim, "Familie Rom", Konfession.Katholisch);
            var kraft = w.Glaube.Bekehrungskraft(k);
            Pruefe.Ungefaehr(0.04 * (1 + w.Familien.Talent(k, Talent.Rhetorik) * 0.1), kraft, 1e-9);
            var e = w.Ausfuehren(k, new Bekehrungspredigt());
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(11, k.Aktionspunkte.Vorrat);
            Pruefe.Ungefaehr(0.55 + kraft, w.Glaube.Anteil(Konfession.Katholisch), 1e-9);
            Pruefe.Ungefaehr(0.45 - kraft, w.Glaube.Anteil(Konfession.Evangelisch), 1e-9);
            Pruefe.Gleich(3.0, w.Rat.Gunst(w.Rat.RatsherrNamens("Ulrich Wendelstein")!, k));
            Pruefe.Gleich(0.0, w.Rat.Gunst(w.Rat.RatsherrNamens("Konrad Eisenfaust")!, k));
            Pruefe.Wahr(!w.Ausfuehren(k, new Bekehrungspredigt()).Erfolg, "zweimal im Jahr");
            Pruefe.Wahr(w.Justiz.SpurenVon(k).All(s => s.Art != Vergehen.Ketzerei), "ohne Stadtglauben keine Ketzerei");
            JahrBeenden(sim);
            Pruefe.Ungefaehr(0.55 + kraft * 0.9, w.Glaube.Anteil(Konfession.Katholisch), 1e-9);
            Pruefe.Ungefaehr(1, w.Glaube.Anteile.Values.Sum(), 1e-9);
        }),

        ("Stadtglaube: Antrag nur im Rat; Ratsherren stimmen nach Glauben; Andersgläubige zahlen 150 Gulden Glaubenszins, ihre Messe ist Ketzerei", () =>
        {
            var sim = NeueSimulation();
            var (w, k, _) = Haus(sim, "Haus Kreuz", Konfession.Katholisch);
            var (ev, kirche) = Prediger(sim, "Familie Wittenberg", Konfession.Evangelisch);
            Pruefe.WirftMitMeldung(() => throw new Exception(w.Ausfuehren(k, new StadtglaubeBeantragen(Konfession.Katholisch)).Meldung), "nur Mitglieder des Rates");
            InDenRat(w, k);
            var prognose = w.Glaube.AbstimmungsPrognose(k, Konfession.Katholisch);
            Pruefe.Gleich(4, prognose.Ja);
            Pruefe.Gleich(3, prognose.Nein);
            var e = w.Ausfuehren(k, new StadtglaubeBeantragen(Konfession.Katholisch));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(10, k.Aktionspunkte.Vorrat);
            Pruefe.Wahr(!w.Ausfuehren(k, new StadtglaubeBeantragen(null)).Erfolg, "zweiter Antrag");
            JahrBeenden(sim);
            Pruefe.Gleich((Konfession?)Konfession.Katholisch, w.Glaube.Stadtglaube);
            Pruefe.Wahr(w.Rat.LetzteSitzung.Any(s => s.Contains("Stadtglauben") && s.Contains("ANGENOMMEN")), string.Join(" | ", w.Rat.LetzteSitzung));
            Pruefe.Wahr(w.Glaube.IstAndersglaeubig(ev) && !w.Glaube.IstAndersglaeubig(k), "andersgläubig");

            // Glaubensfreiheit: Die drei evangelischen Ratsherren wären dafür, die katholischen und der Bürgermeister dagegen.
            var frei = w.Glaube.AbstimmungsPrognose(ev, null);
            Pruefe.Gleich(3, frei.Ja);
            Pruefe.Wahr(!Reichsgilde.Core.Politik.Stadtrat.IstAngenommen(frei.Ja, frei.Nein, frei.Buergermeister), "Freiheit ginge durch");

            JahrBeenden(sim);
            Pruefe.Gleich(150.0, w.Glaube.AbgabeVorjahr(ev));
            Pruefe.Gleich(0.0, w.Glaube.AbgabeVorjahr(k));
            var ketzerei = w.Justiz.SpurenVon(ev).Single(s => s.Art == Vergehen.Ketzerei);
            Pruefe.Wahr(w.Justiz.KenntSpur(k, ketzerei), "die Katholiken haben die Messe gesehen");

            // Bekehrungspredigt gegen den Stadtglauben: noch eine Spur.
            ev.Aktionspunkte.Setzen(12);
            Pruefe.Wahr(w.Ausfuehren(ev, new Bekehrungspredigt()).Meldung.Contains("Ketzerei"), "Ketzerpredigt");
            Pruefe.Gleich(2, w.Justiz.SpurenVon(ev).Count(s => s.Art == Vergehen.Ketzerei));
            Pruefe.Wahr(kirche.Konto == ev, "Kirche");
        }),

        ("KI: tritt unter fremdem Stadtglauben über; beantragt im Rat ihren Glauben; predigt, wenn sonst AP verfielen", () =>
        {
            var sim = NeueSimulation();
            var (w, zwerg, _) = Haus(sim, "Haus Amboss", Konfession.Evangelisch, geld: 10_000, volk: Volk.Zwerg);
            w.KiAnmelden(zwerg, KiProfil.Standard);
            w.Glaube.Stadtglaube = Konfession.Katholisch;
            var (_, ratsKi, _) = Haus(sim, "Haus Kreuz", Konfession.Katholisch);
            w.KiAnmelden(ratsKi, KiProfil.Standard);
            InDenRat(w, ratsKi);
            var (prediger, _) = Prediger(sim, "Familie Rom", Konfession.Katholisch);
            w.KiAnmelden(prediger, KiProfil.Standard with { Reserve = 19_000 }); // kein Aufstieg, keine Käufe, kaum Werben – die AP bleiben übrig
            var anteil = w.Glaube.Anteil(Konfession.Katholisch);
            for (var i = 0; i < 12 * 60 - Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // über die Politikstunde
            Pruefe.Gleich((Konfession?)Konfession.Katholisch, w.Glaube.GlaubeVon(zwerg));
            Pruefe.Wahr(w.Glaube.Antrag is null, "Katholisch gilt schon – kein Antrag");
            Pruefe.Wahr(w.Glaube.Anteil(Konfession.Katholisch) > anteil, "keine Bekehrungspredigt: " + string.Join(" | ", w.Rivalen.Single(r => r.Konto == prediger).Entscheidungen));

            w.Glaube.Stadtglaube = null;
            JahrBeenden(sim);
            ratsKi.Aktionspunkte.Setzen(12);
            for (var i = 0; i < 12 * 60 - Spielzeit.TagesbeginnMinute; i++) sim.Tick();
            Pruefe.Gleich(ratsKi, w.Glaube.Antrag?.Antragsteller);
            Pruefe.Gleich((Konfession?)Konfession.Katholisch, w.Glaube.Antrag?.Glaube);
        }),

        ("Speichern: Glaube der Personen, Anteile, Stadtglaube, Antrag und Konvertiten überstehen Speichern und Laden; Startglaube evangelisch", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Beruf: "Prediger", Glaube: Konfession.Evangelisch, Startvermoegen: 10_000));
            var w = sim.Wirtschaft!;
            spieler.Aktionspunkte.Setzen(12);
            Pruefe.Wahr(w.Ausfuehren(spieler, new Bekehrungspredigt()).Erfolg, "Predigt");
            InDenRat(w, spieler);
            Pruefe.Wahr(w.Ausfuehren(spieler, new StadtglaubeBeantragen(Konfession.Evangelisch)).Erfolg, "Antrag");
            var eisenbart = w.Konten.Single(k => k.Inhaber == "Haus Eisenbart");
            eisenbart.Aktionspunkte.Setzen(12);
            Pruefe.Wahr(w.Ausfuehren(eisenbart, new Konvertieren(Konfession.Katholisch)).Erfolg, "Konvertieren");
            w.Glaube.Stadtglaube = Konfession.Katholisch;

            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var g = kopie.Wirtschaft!.Glaube;
            var kEisenbart = kopie.Wirtschaft.Konten.Single(k => k.Inhaber == "Haus Eisenbart");
            Pruefe.Ungefaehr(w.Glaube.Anteil(Konfession.Evangelisch), g.Anteil(Konfession.Evangelisch), 1e-12);
            Pruefe.Gleich((Konfession?)Konfession.Katholisch, g.Stadtglaube);
            Pruefe.Gleich(kopieSpieler, g.Antrag?.Antragsteller);
            Pruefe.Gleich((Konfession?)Konfession.Evangelisch, g.Antrag?.Glaube);
            Pruefe.Gleich((Konfession?)Konfession.Katholisch, g.GlaubeVon(kEisenbart));
            Pruefe.Gleich(Konfession.Evangelisch, kopie.Wirtschaft.Familien.Von(kEisenbart)!.Oberhaupt.Herkunftsglaube);
            Pruefe.Gleich(w.Glaube.KonvertiertBis(eisenbart), g.KonvertiertBis(kEisenbart));
            Pruefe.Wahr(g.BekehrenVerboten(kopieSpieler!)!.Contains("schon"), "Predigt des Jahres gemerkt");
            for (var i = 0; i < 2 * 60; i++) { sim.Tick(); kopie.Tick(); }
            Pruefe.Ungefaehr(w.Konten.Sum(k => k.Vermoegen), kopie.Wirtschaft.Konten.Sum(k => k.Vermoegen), 1e-9);
        }),

        ("Ohne Abschnitt „glaube“: keine Glaubensnähe, die Messe geht ganz an die Kirche, die Befehle werden abgelehnt", () =>
        {
            var ohne = Wirtschaftsdaten.AusJson(Json.Replace("\"glaube\": {\n    \"voelker\"", "\"glaubeAus\": {\n    \"voelker\""));
            Pruefe.Wahr(ohne.Glaube is null, "Abschnitt entfernt");
            var sim = NeueSimulation(ohne);
            var (k, kirche) = Prediger(sim, "Familie Rom", Konfession.Katholisch);
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(0.0, w.Glaube.Naehe(w.Rat.Ratsherren[0], k));
            Pruefe.Ungefaehr(440, w.Kirchenwesen.ErwarteteKollekte(kirche), 1e-9); // 200 × 2,2 seit PR #177
            Pruefe.Wahr(!w.Ausfuehren(k, new Bekehrungspredigt()).Erfolg, "Predigt ohne Glauben");
            Pruefe.Wahr(!w.Ausfuehren(k, new Konvertieren(Konfession.Evangelisch)).Erfolg, "Konvertieren ohne Glauben");
        }),
    };
}
