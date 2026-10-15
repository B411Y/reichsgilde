using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für den Prediger: Messe und Kollekte, Ablass, Beichte, Lehr- und Hetzschrift.</summary>
static class PredigerTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>Daten, in denen der Prediger jede Tat in der Beichte hört – damit der Test nicht vom Zufall abhängt.</summary>
    static readonly Lazy<Wirtschaftsdaten> Redselig = new(() => Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft())
        .Replace("\"beichte\": { \"chance\": 0.25", "\"beichte\": { \"chance\": 1.0")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    /// <summary>Bis nach der Messe (10 Uhr; die Partie beginnt um 6).</summary>
    static void NachDerMesse(Simulation sim)
    {
        for (var i = 0; i < 4 * 60 + 1; i++) sim.Tick();
    }

    /// <summary>Eine Kirche der gewünschten Art mit Personal, ohne Karren.</summary>
    static (Stadtwirtschaft W, Konto K, Betrieb B) Prediger(Simulation sim, Betriebsart art = Betriebsart.Kirche, string name = "Familie Psalm")
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: 10_000);
        var b = w.BetriebEroeffnen($"Kirche {name}", art, k, startkarren: "");
        w.Einstellen(b, "Geselle", 1);
        return (w, k, b);
    }

    /// <summary>Ein Haus, das einen Ratsherrn besticht (100 Gulden) – eine Tat, für die es Ablass braucht.</summary>
    static Konto Suender(Stadtwirtschaft w, string name = "Haus Eisenbart")
    {
        var k = w.KontoEroeffnen(name, startvermoegen: 5_000);
        Pruefe.Wahr(w.Ausfuehren(k, new Schenken(w.Rat.Ratsherren[0].Name, 100)).Erfolg, "Bestechung");
        return k;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: Klosterhof liefert Lumpen und Wachs; Kirche → Dom → Kathedrale; die Hetzschrift braucht Farbe", () =>
        {
            var d = Daten.Value;
            Pruefe.Wahr(d.Art(Betriebsart.Klosterhof).Rohstoffe.SequenceEqual(new[] { "Lumpen", "Wachs" }), "Klosterhof");
            Pruefe.Wahr(d.Art(Betriebsart.Kirche).Produkte.SequenceEqual(new[] { "Papier", "Kerze", "Lehrschrift" }), "Kirche");
            Pruefe.Gleich(Betriebsart.Dom, d.Art(Betriebsart.Kirche).Ausbau);
            Pruefe.Gleich(Betriebsart.Kathedrale, d.Art(Betriebsart.Dom).Ausbau);
            Pruefe.Gleich(1, d.Waren["Hetzschrift"].Zutaten["Farbe"]);
            Pruefe.Gleich(1, d.Waren["Stundenbuch"].Zutaten["Gold"]);
            Pruefe.Gleich(Betriebsart.Kirche, d.Berufe["Prediger"].Werkstatt);
            var json = File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"lehrschrift\": \"Lehrschrift\"", "\"lehrschrift\": \"Flugblatt\"");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(json), "„Flugblatt“ gibt es nicht");
        }),

        ("Messe: um 10 Uhr kommen die 200 Gläubigen des Jahres und spenden je 2,5 Gulden – steuerfrei an den Prediger", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Prediger(sim);
            var vorher = k.Laufend.Umsatz;
            for (var i = 0; i < 3 * 60 + 59; i++) sim.Tick(); // 9:59 – noch keine Messe
            Pruefe.Gleich(0.0, w.Kirchenwesen.DiesesJahr(b).Kollekte);
            sim.Tick(); sim.Tick();
            var buch = w.Kirchenwesen.DiesesJahr(b);
            Pruefe.Ungefaehr(200, buch.Glaeubige, 1e-9);
            Pruefe.Ungefaehr(500, buch.Kollekte, 1e-9);
            Pruefe.Ungefaehr(vorher + 500, k.Laufend.Umsatz, 1e-9);
        }),

        ("Messe: Gläubige verteilen sich nach Anziehung – ein Dom (Stufe 2) zieht anderthalbmal so viele wie eine Kirche; ohne Personal keine Messe", () =>
        {
            var sim = NeueSimulation();
            var (w, _, kirche) = Prediger(sim, name: "Familie Psalm");
            var (_, _, dom) = Prediger(sim, Betriebsart.Dom, name: "Familie Weihrauch");
            var leer = w.BetriebEroeffnen("Kapelle ohne Pfarrer", Betriebsart.Kirche, w.KontoEroeffnen("Familie Leer", startvermoegen: 1_000), startkarren: "");
            NachDerMesse(sim);
            var kw = w.Kirchenwesen;
            Pruefe.Ungefaehr(200, kw.DiesesJahr(kirche).Glaeubige + kw.DiesesJahr(dom).Glaeubige, 1e-9);
            Pruefe.Ungefaehr(1.5, kw.DiesesJahr(dom).Glaeubige / kw.DiesesJahr(kirche).Glaeubige, 1e-9);
            Pruefe.Gleich(0.0, kw.DiesesJahr(leer).Glaeubige);
        }),

        ("Ablass: 250 Gulden + halber Wert der Tat; die Tat verfällt mit allen Beweisen dafür; nicht in der eigenen Kirche", () =>
        {
            var sim = NeueSimulation();
            var (w, prediger, kirche) = Prediger(sim);
            var suender = Suender(w);
            var zeuge = w.KontoEroeffnen("Familie Fuchs", startvermoegen: 5_000);
            var spur = w.Justiz.SpurenVon(suender).Single();
            w.Justiz.BeweisGeben(zeuge, spur);
            Pruefe.Gleich(1, w.Justiz.Beweise(zeuge, suender).Count());
            Pruefe.Ungefaehr(250 + 0.5 * 100, w.Kirchenwesen.Ablasspreis(spur), 1e-9);

            var e = w.Ausfuehren(prediger, new AblassKaufen(kirche.Name));
            Pruefe.Wahr(!e.Erfolg, "Ablass in der eigenen Kirche");
            e = w.Ausfuehren(zeuge, new AblassKaufen(kirche.Name));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("nichts zu bereuen"), e.Meldung);

            var vorher = suender.Vermoegen;
            var umsatz = prediger.Laufend.Umsatz;
            e = w.Ausfuehren(suender, new AblassKaufen(kirche.Name));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Ungefaehr(vorher - 300, suender.Vermoegen, 1e-9);
            Pruefe.Ungefaehr(umsatz + 300, prediger.Laufend.Umsatz, 1e-9);
            Pruefe.Gleich(0, w.Justiz.SpurenVon(suender).Count());
            Pruefe.Gleich(0, w.Justiz.Beweise(zeuge, suender).Count());
            Pruefe.Gleich(1, w.Kirchenwesen.DiesesJahr(kirche).Ablaesse);
        }),

        ("Ablass: Gibt es mehrere Taten, verfällt zuerst die, für die andere Beweise haben", () =>
        {
            var sim = NeueSimulation();
            var (w, _, kirche) = Prediger(sim);
            var suender = Suender(w);
            Pruefe.Wahr(w.Ausfuehren(suender, new Schenken(w.Rat.Ratsherren[1].Name, 200)).Erfolg, "zweite Bestechung");
            var spuren = w.Justiz.SpurenVon(suender).OrderBy(s => s.Nummer).ToList();
            var zeuge = w.KontoEroeffnen("Familie Fuchs", startvermoegen: 5_000);
            w.Justiz.BeweisGeben(zeuge, spuren[0]); // die ältere ist bekannt
            Pruefe.Gleich(spuren[0], w.Kirchenwesen.AblassFuer(suender));
            Pruefe.Wahr(w.Ausfuehren(suender, new AblassKaufen(kirche.Name)).Erfolg, "Ablass");
            Pruefe.Gleich(spuren[1], w.Justiz.SpurenVon(suender).Single());
        }),

        ("Beichte: Am Jahresende hört der Prediger von fremden Taten; bricht er das Geheimnis, werden es Beweise – und er begeht selbst ein Vergehen", () =>
        {
            var sim = NeueSimulation(Redselig.Value);
            var (w, prediger, _) = Prediger(sim);
            var suender = Suender(w);
            Pruefe.Wahr(!w.Ausfuehren(prediger, new BeichtgeheimnisBrechen(suender.Inhaber)).Erfolg, "vor der Beichte");
            JahrBeenden(sim);
            Pruefe.Gleich(1, w.Kirchenwesen.Beichtwissen(prediger, suender).Count());
            Pruefe.Gleich(0, w.Kirchenwesen.Beichtwissen(prediger, prediger).Count());
            var e = w.Ausfuehren(prediger, new BeichtgeheimnisBrechen(suender.Inhaber));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(1, w.Justiz.Beweise(prediger, suender).Count());
            Pruefe.Gleich(0, w.Kirchenwesen.Beichtwissen(prediger, suender).Count());
            Pruefe.Gleich(Vergehen.Beichtbruch, w.Justiz.SpurenVon(prediger).Single().Art);
        }),

        ("Lehrschrift: vom Markt oder aus dem eigenen Lager, +½ Stern, einmal je Jahr, keine Aktionspunkte", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Prediger(sim);
            var f = w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann, alter: 30);
            var talent = f.Oberhaupt.Anlage(Talent.Rhetorik);
            var ap = k.Aktionspunkte.Vorrat;
            var vorher = k.Vermoegen;
            var preis = w.Markt["Lehrschrift"].Preis;
            var e = w.Ausfuehren(k, new LehrschriftLesen(Talent.Rhetorik));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Ungefaehr(talent + 0.5, f.Oberhaupt.Anlage(Talent.Rhetorik), 1e-9);
            Pruefe.Ungefaehr(vorher - preis, k.Vermoegen, 1);
            Pruefe.Gleich(ap, k.Aktionspunkte.Vorrat);
            Pruefe.Wahr(!w.Ausfuehren(k, new LehrschriftLesen(Talent.Handwerk)).Erfolg, "zweimal im Jahr");
            JahrBeenden(sim);
            b.Einlagern("Lehrschrift", 1);
            vorher = k.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(k, new LehrschriftLesen(Talent.Handwerk)).Erfolg, "im neuen Jahr");
            Pruefe.Ungefaehr(vorher, k.Vermoegen, 1e-9);
            Pruefe.Gleich(0, b.ImLager("Lehrschrift"));
        }),

        ("Hetzschrift: 5 Ansehen weniger bis Ende nächsten Jahres; der Verfasser begeht eine Verleumdung", () =>
        {
            var sim = NeueSimulation();
            var (w, autor, _) = Prediger(sim);
            var ziel = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
            var ansehen = w.Rat.Ansehen(ziel);
            Pruefe.Wahr(!w.Ausfuehren(autor, new HetzschriftVerbreiten(autor.Inhaber)).Erfolg, "gegen sich selbst");
            var e = w.Ausfuehren(autor, new HetzschriftVerbreiten(ziel.Inhaber));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Ungefaehr(ansehen - 5, w.Rat.Ansehen(ziel), 1e-9);
            Pruefe.Gleich(Vergehen.Verleumdung, w.Justiz.SpurenVon(autor).Single().Art);
            var bis = w.Kirchenwesen.GehetztBis(ziel);
            Pruefe.Gleich(sim.Zeit.Jahr + 1, bis);
            JahrBeenden(sim);
            Pruefe.Wahr(w.Kirchenwesen.AnsehenAbzug(ziel) > 0, "wirkt noch im nächsten Jahr");
            JahrBeenden(sim);
            Pruefe.Gleich(0.0, w.Kirchenwesen.AnsehenAbzug(ziel));
            Pruefe.Gleich<int?>(null, w.Kirchenwesen.GehetztBis(ziel));
        }),

        ("KI: kauft Ablass, wenn andere Beweise gegen sie haben; ein Wagemutiger bricht als Prediger das Beichtgeheimnis", () =>
        {
            var sim = NeueSimulation(Redselig.Value);
            var w = sim.Wirtschaft!;
            var (_, prediger, kirche) = Prediger(sim, name: "Haus Psalm");
            w.KiAnmelden(prediger, Partieaufbau.ProfilFuer(KiCharakter.Wagemutig));
            var suender = Suender(w, "Haus Eisenbart");
            suender.Umsatz(20_000);
            w.KiAnmelden(suender, KiProfil.Standard);
            var andere = Suender(w, "Haus Krug");
            w.Justiz.BeweisGeben(prediger, w.Justiz.SpurenVon(suender).Single());
            JahrBeenden(sim);
            for (var i = 0; i < 7 * 60; i++) sim.Tick(); // über die Politikstunde (12 Uhr)
            Pruefe.Gleich(0, w.Justiz.SpurenVon(suender).Count(s => s.Art == Vergehen.Bestechung));
            Pruefe.Wahr(w.Kirchenwesen.DiesesJahr(kirche).Ablaesse + (w.Kirchenwesen.Vorjahr(kirche)?.Ablaesse ?? 0) >= 1, "kein Ablass gekauft");
            Pruefe.Wahr(w.Justiz.Beweise(prediger, andere).Any(), "Beichtgeheimnis nicht gebrochen");
        }),

        ("Speichern: Kirchenbuch, Beichtwissen und Hetzschrift überstehen Speichern und Laden; Startberuf Prediger", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                Redselig.Value, new NeuePartie(Beruf: "Prediger", Startvermoegen: 10_000));
            var w = sim.Wirtschaft!;
            var kirche = w.Betriebe.Single(b => b.Konto == spieler && b.IstWerkstatt);
            Pruefe.Gleich("Kirche am Weinmarkt", kirche.Name);
            var rivale = w.Konten.First(k => k != spieler);
            Pruefe.Wahr(w.Ausfuehren(rivale, new Schenken(w.Rat.Ratsherren[0].Name, 100)).Erfolg, "Bestechung");
            Pruefe.Wahr(w.Ausfuehren(spieler, new HetzschriftVerbreiten(rivale.Inhaber)).Erfolg, "Hetzschrift");
            JahrBeenden(sim);
            NachDerMesse(sim);
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var kw = kopie.Wirtschaft!;
            var kk = kw.Betriebe.Single(b => b.Konto == kopieSpieler && b.IstWerkstatt);
            var kr = kw.Konten.Single(k => k.Inhaber == rivale.Inhaber);
            Pruefe.Gleich(w.Kirchenwesen.DiesesJahr(kirche), kw.Kirchenwesen.DiesesJahr(kk));
            Pruefe.Gleich(w.Kirchenwesen.Vorjahr(kirche), kw.Kirchenwesen.Vorjahr(kk));
            Pruefe.Gleich(w.Kirchenwesen.Beichtwissen(spieler, rivale).Count(), kw.Kirchenwesen.Beichtwissen(kopieSpieler, kr).Count());
            Pruefe.Gleich(w.Kirchenwesen.GehetztBis(rivale), kw.Kirchenwesen.GehetztBis(kr));
        }),
    };
}
