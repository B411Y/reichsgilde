using Reichsgilde.Core.Zeit;
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

    /// <summary>Das Jahr zu Ende spielen und nach jedem Tick <paramref name="nachTick"/> aufrufen.</summary>
    static void JahrBeendenMit(Simulation sim, Action nachTick)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) { sim.Tick(); nachTick(); }
    }

    /// <summary>Bis nach der Messe (10 Uhr; die Partie beginnt um 6).</summary>
    static void NachDerMesse(Simulation sim)
    {
        for (var i = 0; i < 10 * 60 + 1 - Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // 04:00 → 10:01
    }

    /// <summary>Bis zu dieser Uhrzeit (Minuten nach Mitternacht) am selben Tag.</summary>
    static void BisMinute(Simulation sim, int minute)
    {
        while (sim.Zeit.UhrzeitInMinuten < minute) sim.Tick();
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

        ("Messe: um 10 Uhr kommen die 200 Gläubigen des Jahres und spenden je 2,2 Gulden – steuerfrei an den Prediger", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Prediger(sim);
            var vorher = k.Laufend.Umsatz;
            for (var i = 0; i < 9 * 60 + 59 - Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // 9:59 – noch keine Messe
            Pruefe.Gleich(0.0, w.Kirchenwesen.DiesesJahr(b).Kollekte);
            sim.Tick(); sim.Tick();
            var buch = w.Kirchenwesen.DiesesJahr(b);
            Pruefe.Ungefaehr(200, buch.Glaeubige, 1e-9);
            Pruefe.Ungefaehr(440, buch.Kollekte, 1e-9); // 2,2 je Gläubigem seit PR #177
            Pruefe.Ungefaehr(vorher + 440, k.Laufend.Umsatz, 1e-9);
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

        ("Lehrschrift: vom Markt oder aus dem eigenen Lager, +½ Stern bis höchstens 3 Sterne, einmal je Jahr, 1 Aktionspunkt", () =>
        {
            var sim = NeueSimulation();
            var (w, k, b) = Prediger(sim);
            var f = w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann, alter: 30);
            f.Oberhaupt.TalentSetzen(Talent.Rhetorik, 1);
            f.Oberhaupt.TalentSetzen(Talent.Handwerk, 2.75);
            var ap = k.Aktionspunkte.Vorrat;
            var vorher = k.Vermoegen;
            var preis = w.Markt["Lehrschrift"].Preis;
            var e = w.Ausfuehren(k, new LehrschriftLesen(Talent.Rhetorik));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Ungefaehr(1.5, f.Oberhaupt.Anlage(Talent.Rhetorik), 1e-9);
            Pruefe.Ungefaehr(vorher - preis, k.Vermoegen, 1);
            Pruefe.Gleich(ap - 1, k.Aktionspunkte.Vorrat);
            Pruefe.Wahr(!w.Ausfuehren(k, new LehrschriftLesen(Talent.Handwerk)).Erfolg, "zweimal im Jahr");
            JahrBeenden(sim);
            b.Einlagern("Lehrschrift", 1);
            vorher = k.Vermoegen;
            Pruefe.Wahr(w.Ausfuehren(k, new LehrschriftLesen(Talent.Handwerk)).Erfolg, "im neuen Jahr");
            Pruefe.Ungefaehr(vorher, k.Vermoegen, 1e-9);
            Pruefe.Gleich(0, b.ImLager("Lehrschrift"));
            Pruefe.Ungefaehr(3, f.Oberhaupt.Anlage(Talent.Handwerk), 1e-9); // nur bis 3 Sterne
            JahrBeenden(sim);
            var e2 = w.Ausfuehren(k, new LehrschriftLesen(Talent.Handwerk));
            Pruefe.Wahr(!e2.Erfolg && e2.Meldung.Contains("nichts mehr lernen"), e2.Meldung);
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
            Wohnhaushilfe.MitWohnhaus(w, suender); // Wohnhaus steht: die KI steigt auf wie vor dem Wohnhaus
            w.KiAnmelden(suender, KiProfil.Standard);
            var andere = Suender(w, "Haus Krug");
            w.Justiz.BeweisGeben(prediger, w.Justiz.SpurenVon(suender).Single());
            JahrBeenden(sim);
            for (var i = 0; i < 12 * 60 - Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // über die Politikstunde (12 Uhr)
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
            Pruefe.Gleich(w.Kirchenwesen.Beichtwissen(spieler, rivale).Count(), kw.Kirchenwesen.Beichtwissen(kopieSpieler!, kr).Count());
            Pruefe.Gleich(w.Kirchenwesen.GehetztBis(rivale), kw.Kirchenwesen.GehetztBis(kr));
        }),

        ("Gläubige im Raum: nachts keine, zur Messe (9:30–11 Uhr) voll, sonst wenige Betende – höchstens 16 Plätze; gepredigt wird 10–11 Uhr", () =>
        {
            var sim = NeueSimulation();
            var (w, _, b) = Prediger(sim);
            var kw = w.Kirchenwesen;
            Pruefe.Gleich(0, kw.GlaeubigeJetzt(b)); // vor 8 Uhr
            BisMinute(sim, 8 * 60);
            Pruefe.Gleich(3, kw.GlaeubigeJetzt(b)); // allein in der Stadt: 200 ÷ 8 × 0,125 ≈ 3,1
            BisMinute(sim, 9 * 60 + 29);
            Pruefe.Gleich(3, kw.GlaeubigeJetzt(b));
            Pruefe.Wahr(!kw.MesseLaeuft(b), "9:29");
            BisMinute(sim, 9 * 60 + 30);
            Pruefe.Gleich(16, kw.GlaeubigeJetzt(b)); // 200 ÷ 8 = 25 – gedeckelt
            Pruefe.Gleich(25, kw.GlaeubigeJetzt(b, hoechstens: 40));
            Pruefe.Gleich(5, kw.GlaeubigeJetzt(b, hoechstens: 5));
            Pruefe.Wahr(!kw.MesseLaeuft(b), "9:30 – die Gläubigen kommen, gepredigt wird ab 10");
            BisMinute(sim, 10 * 60);
            Pruefe.Wahr(kw.MesseLaeuft(b), "10:00");
            Pruefe.Gleich(16, kw.GlaeubigeJetzt(b));
            BisMinute(sim, 10 * 60 + 59);
            Pruefe.Wahr(kw.MesseLaeuft(b) && kw.GlaeubigeJetzt(b) == 16, "10:59");
            BisMinute(sim, 11 * 60);
            Pruefe.Wahr(!kw.MesseLaeuft(b), "11:00");
            Pruefe.Gleich(3, kw.GlaeubigeJetzt(b));
            BisMinute(sim, 15 * 60);
            Pruefe.Gleich(3, kw.GlaeubigeJetzt(b));
            BisMinute(sim, 19 * 60 + 59);
            Pruefe.Gleich(3, kw.GlaeubigeJetzt(b));
            BisMinute(sim, 20 * 60);
            Pruefe.Gleich(0, kw.GlaeubigeJetzt(b)); // Feierabend
            Pruefe.Wahr(!kw.MesseLaeuft(b) && !kw.BeichteJetzt(b), "abends");
        }),

        ("Gläubige im Raum: Bei Konkurrenz nach Anziehung – der Dom hat mehr als die Kirche; ohne Arbeiter keine Gläubigen und keine Messe; ohne Kirchenwesen und auf dem Klosterhof nichts", () =>
        {
            var sim = NeueSimulation();
            var (w, k, kirche) = Prediger(sim, name: "Familie Psalm");
            var (_, _, dom) = Prediger(sim, Betriebsart.Dom, name: "Familie Weihrauch");
            var kw = w.Kirchenwesen;
            BisMinute(sim, 8 * 60);
            Pruefe.Gleich((1, 2), (kw.GlaeubigeJetzt(kirche), kw.GlaeubigeJetzt(dom))); // 80 bzw. 120 Gläubige ÷ 8 × 0,125
            BisMinute(sim, 10 * 60);
            var messe = (kw.GlaeubigeJetzt(kirche), kw.GlaeubigeJetzt(dom));
            Pruefe.Gleich((int)Math.Round(kw.ErwarteteGlaeubige(kirche) / 8, MidpointRounding.AwayFromZero), messe.Item1);
            Pruefe.Gleich((int)Math.Round(kw.ErwarteteGlaeubige(dom) / 8, MidpointRounding.AwayFromZero), messe.Item2);
            Pruefe.Wahr(messe.Item2 > messe.Item1 && messe.Item1 > 2, $"zur Messe {messe}");
            Pruefe.Wahr(kw.MesseLaeuft(kirche) && kw.MesseLaeuft(dom), "Messe in beiden");
            Pruefe.Wahr(w.Ausfuehren(k, new PersonalSetzen(kirche.Name, "Geselle", 0)).Erfolg, "Geselle entlassen");
            Pruefe.Gleich(0, kw.GlaeubigeJetzt(kirche));
            Pruefe.Wahr(!kw.MesseLaeuft(kirche) && !kw.BeichteJetzt(kirche), "leere Kirche");
            Pruefe.Gleich(16, kw.GlaeubigeJetzt(dom)); // jetzt allein in der Stadt
            var hof = w.BetriebEroeffnen("Klosterhof Weihrauch", Betriebsart.Klosterhof, dom.Konto, startkarren: "");
            Pruefe.Wahr(kw.GlaeubigeJetzt(hof) == 0 && !kw.MesseLaeuft(hof) && !kw.BeichteJetzt(hof), "Klosterhof");

            var ohne = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 },
                wirtschaftsdaten: Wirtschaftsdaten.AusJson(File.ReadAllText(Datenpfad.Wirtschaft()).Replace("\"kirche\": {", "\"kircheAlt\": {")));
            var (w2, _, b2) = Prediger(ohne);
            BisMinute(ohne, 10 * 60);
            Pruefe.Wahr(!w2.Kirchenwesen.IstAktiv, "Kirchenwesen aktiv");
            Pruefe.Wahr(w2.Kirchenwesen.GlaeubigeJetzt(b2) == 0 && !w2.Kirchenwesen.MesseLaeuft(b2) && !w2.Kirchenwesen.BeichteJetzt(b2), "ohne Kirchenwesen");
        }),

        ("Gläubige im Raum: Gebeichtet wird in festen Viertelstunden der Arbeitszeit, je nach Beichtchance – nie zur Messe", () =>
        {
            string Beichtzeiten(Wirtschaftsdaten daten)
            {
                var sim = NeueSimulation(daten);
                var (w, _, b) = Prediger(sim);
                var zeiten = new List<string>();
                while (sim.Zeit.UhrzeitInMinuten < 22 * 60 - 1)
                {
                    var minute = sim.Zeit.UhrzeitInMinuten;
                    var beichte = w.Kirchenwesen.BeichteJetzt(b);
                    if (minute % 15 == 0 && beichte) zeiten.Add($"{minute / 60}:{minute % 60:00}");
                    // in jeder Viertelstunde dasselbe
                    if (minute % 15 != 0) Pruefe.Gleich(zeiten.Contains($"{minute / 60}:{minute / 15 * 15 % 60:00}"), beichte, $"{minute / 60}:{minute % 60:00}");
                    sim.Tick();
                }
                return string.Join(" ", zeiten);
            }
            Pruefe.Gleich("8:30 9:00 11:00 11:45 13:00 14:15 15:00 16:15 17:00 18:15 19:30", Beichtzeiten(Daten.Value)); // Chance 0,25: 11 von 42
            var immer = Beichtzeiten(Redselig.Value).Split(' ');
            Pruefe.Gleich(48 - 6, immer.Length); // 8–20 Uhr ohne 9:30–11 Uhr
            Pruefe.Wahr(immer.First() == "8:00" && immer.Last() == "19:45" && !immer.Contains("9:30") && !immer.Contains("10:45") && immer.Contains("11:00"), string.Join(" ", immer));
        }),

        ("Gläubige im Raum: Die Abfragen ändern nichts – ein Jahr mit ständigen Abfragen verläuft zeichengleich", () =>
        {
            string Spielen(bool abfragen)
            {
                var sim = NeueSimulation();
                var (w, k, b) = Prediger(sim, Betriebsart.Dom, name: "Familie Psalm");
                Prediger(sim, name: "Familie Weihrauch");
                var summe = 0;
                for (var jahr = 0; jahr < 2; jahr++)
                    JahrBeendenMit(sim, () =>
                    {
                        if (!abfragen) return;
                        foreach (var x in w.Betriebe)
                            summe += w.Kirchenwesen.GlaeubigeJetzt(x) + (w.Kirchenwesen.MesseLaeuft(x) ? 1 : 0) + (w.Kirchenwesen.BeichteJetzt(x) ? 1 : 0);
                    });
                Pruefe.Wahr(!abfragen || summe > 0, "keine Gläubigen gesehen");
                return Speicherung.Speichern(sim, k, new DateTime(2026, 10, 21, 12, 0, 0));
            }
            Pruefe.Gleich(Spielen(false), Spielen(true));
        }),
    };
}
