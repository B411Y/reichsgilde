using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>Tests für den Parfumeur: Waren und Politur, Moschus, Zwietracht, Heiligenduft, Giftdolch und Gegengift, KI, Speichern.</summary>
static class ParfumeurTests
{
    static readonly string Json = File.ReadAllText(Datenpfad.Wirtschaft());
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json));

    /// <summary>Der Giftdolch trifft immer.</summary>
    static readonly Lazy<Wirtschaftsdaten> SichererDolch = new(() =>
        Wirtschaftsdaten.AusJson(Json.Replace("\"Giftdolch\": { \"chance\": 0.6", "\"Giftdolch\": { \"chance\": 1")));

    static Simulation NeueSimulation(Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static void BisNacht(Simulation sim)
    {
        while (sim.Zeit.UhrzeitInMinuten != 21 * 60 + 1) sim.Tick();
    }

    /// <summary>Ein Haus mit Familie, Geld und Aktionspunkten.</summary>
    static (Stadtwirtschaft W, Konto K, Familie F) Haus(Simulation sim, string name, double geld = 20_000, Geschlecht g = Geschlecht.Mann)
    {
        var w = sim.Wirtschaft!;
        var k = w.KontoEroeffnen(name, startvermoegen: geld);
        var f = w.Familien.Gruenden(k, Volk.Mensch, g, alter: 30);
        k.Aktionspunkte.Setzen(12);
        return (w, k, f);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Daten: Siederei → Duftwerkstatt → Parfumerie (2/3/4), Duftgarten mit Lavendel und Jasmin; Politur in Goldkette und Prunktruhe; ein Dolch ohne Vergiftung fällt auf", () =>
        {
            var d = Daten.Value;
            Pruefe.Wahr(new[] { Betriebsart.Siederei, Betriebsart.Duftwerkstatt, Betriebsart.Parfumerie }.Select(a => d.Art(a).Plaetze).SequenceEqual(new[] { 2, 3, 4 }), "Plätze");
            Pruefe.Wahr(d.Art(Betriebsart.Duftgarten).Rohstoffe.SequenceEqual(new[] { "Lavendel", "Jasmin" }), "Duftgarten");
            Pruefe.Gleich(Betriebsart.Duftgarten, d.Berufe["Parfumeur"].Umland);
            Pruefe.Gleich(1, d.Waren["Politur"].Zutaten["Lavendel"]);
            Pruefe.Gleich(1, d.Waren["Goldkette"].Zutaten["Politur"]);
            Pruefe.Gleich(1, d.Waren["Prunktruhe"].Zutaten["Politur"]);
            Pruefe.Wahr(d.Art(Betriebsart.Parfumerie).Produkte.Contains("Schattendolch"), "Schattendolch in der Parfumerie");
            var falsch = Json.Replace("\"krankheit\": \"Vergiftung\"", "\"krankheit\": \"Husten\"");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(falsch), "gegengift");
        }),

        ("Siederei: macht aus Lavendel und Jasmin Politur, daraus mit Eisen einen Giftdolch", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var k = w.KontoEroeffnen("Familie Rosenhain", startvermoegen: 5_000);
            var b = w.BetriebEroeffnen("Siederei Rosenhain", Betriebsart.Siederei, k, startkarren: "");
            w.Einstellen(b, "Geselle", 2);
            b.Produkt = "Politur";
            b.Einlagern("Lavendel", 20);
            b.Einlagern("Jasmin", 20);
            while (sim.Zeit.UhrzeitInMinuten != 12 * 60) sim.Tick();
            Pruefe.Wahr(b.ImLager("Politur") > 0, "Politur hergestellt");
            Pruefe.Gleich(20 - b.ImLager("Politur"), b.ImLager("Lavendel"));
        }),

        ("Moschus: Zuneigung über das Jahreslimit hinaus, einmal im Jahr je Kandidat; vom Markt zum Marktpreis", () =>
        {
            var sim = NeueSimulation();
            var (w, k, _) = Haus(sim, "Familie Rosenhain");
            var buch = w.Familien;
            var kandidat = buch.Kandidaten.First(x => buch.WerbenVerboten(k, x) is null);
            var nr = kandidat.Person.Nummer;
            Pruefe.Wahr(w.Ausfuehren(k, new Werben(nr, 10_000)).Erfolg, "Werben bis zum Limit");
            Pruefe.Ungefaehr(0, buch.ZuneigungRest(k, kandidat), 1e-9);
            var vorher = buch.Zuneigung(k, kandidat);
            var preis = w.Markt["Moschusduft"].Preis;
            var familie = k.Laufend.Familie;
            var e = w.Ausfuehren(k, new MoschusSchenken(nr));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Ungefaehr(vorher + 10, buch.Zuneigung(k, kandidat), 1e-9);
            Pruefe.Ungefaehr(familie + preis, k.Laufend.Familie, 1e-6);
            var zweimal = w.Ausfuehren(k, new MoschusSchenken(nr));
            Pruefe.Wahr(!zweimal.Erfolg && zweimal.Meldung.Contains("schon Moschus"), zweimal.Meldung);
            JahrBeenden(sim);
            if (buch.KandidatNummer(nr) is not null && buch.WerbenVerboten(k, buch.KandidatNummer(nr)!) is null)
                Pruefe.Wahr(!w.Duft.MoschusSchonKandidat(k, nr), "im neuen Jahr wieder");
        }),

        ("Moschus beim Ratsherrn: Gunst ohne Bestechungsspur; aus dem eigenen Lager kostenlos", () =>
        {
            var sim = NeueSimulation();
            var (w, k, _) = Haus(sim, "Familie Rosenhain");
            var lager = w.BetriebEroeffnen("Siederei Rosenhain", Betriebsart.Siederei, k, startkarren: "");
            lager.Einlagern("Moschusduft", 1);
            var r = w.Rat.Ratsherren[0];
            var geld = k.Vermoegen;
            var e = w.Ausfuehren(k, new MoschusRatsherr(r.Name));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Ungefaehr(12, w.Rat.Gunst(r, k), 1e-9);
            Pruefe.Ungefaehr(geld, k.Vermoegen, 1e-9);
            Pruefe.Gleich(0, lager.ImLager("Moschusduft"));
            Pruefe.Gleich(0, w.Justiz.SpurenVon(k).Count());
        }),

        ("Blume der Zwietracht: Der Ratsherr verliert 15 Gunst für das Opfer (auch unter null) – eine Intrige mit Spur", () =>
        {
            var sim = NeueSimulation();
            var (w, k, _) = Haus(sim, "Familie Rosenhain");
            var opfer = w.KontoEroeffnen("Haus Eisenbart", startvermoegen: 5_000);
            var r = w.Rat.Ratsherren[0];
            var e = w.Ausfuehren(k, new ZwietrachtSaeen(r.Name, opfer.Inhaber));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Ungefaehr(-15, w.Rat.Gunst(r, opfer), 1e-9);
            Pruefe.Gleich(Vergehen.Intrige, w.Justiz.SpurenVon(k).Single().Art);
            Pruefe.Wahr(k.Laufend.Gaunerei > 0, "als Gaunerei gebucht");
        }),

        ("Heiligenduft: +4 Ansehen bis Ende des nächsten Jahres, solange er wirkt kein zweiter", () =>
        {
            var sim = NeueSimulation();
            var (w, k, _) = Haus(sim, "Familie Rosenhain");
            var (_, nachbar, _) = Haus(sim, "Familie Nachbar");
            var preis = w.Markt["Heiligenduft"].Preis;
            Pruefe.Wahr(w.Ausfuehren(k, new HeiligenduftStiften()).Erfolg, "stiften");
            Pruefe.Ungefaehr(4, w.Duft.AnsehenBonus(k), 1e-9);
            // gleich reiches Haus ohne Duft: 4 Ansehen weniger – bis auf das, was der Kauf am Vermögen kostete
            Pruefe.Ungefaehr(4 - preis / 1000 * w.Daten.Politik.Gunst.AnsehenJe1000Gulden, w.Rat.Ansehen(k) - w.Rat.Ansehen(nachbar), 1e-6);
            Pruefe.Wahr(!w.Ausfuehren(k, new HeiligenduftStiften()).Erfolg, "zweimal");
            JahrBeenden(sim);
            Pruefe.Ungefaehr(4, w.Duft.AnsehenBonus(k), 1e-9);
            JahrBeenden(sim);
            Pruefe.Ungefaehr(0, w.Duft.AnsehenBonus(k), 1e-9);
        }),

        ("Giftdolch: nachts vergiftet; Medicus und Schnaps helfen nicht, Gegengift heilt – Spur Giftanschlag", () =>
        {
            var sim = NeueSimulation(SichererDolch.Value);
            var (w, taeter, _) = Haus(sim, "Familie Rosenhain");
            var (_, opfer, f) = Haus(sim, "Haus Eisenbart");
            var e = w.Ausfuehren(taeter, new Giftanschlag(opfer.Inhaber, "Giftdolch"));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Gleich(1, w.Duft.AnschlaegeDiesesJahr(taeter));
            Pruefe.Wahr(!w.Ausfuehren(taeter, new Giftanschlag(opfer.Inhaber, "Giftdolch")).Erfolg, "nur einer je Nacht und Haus");
            BisNacht(sim);
            Pruefe.Gleich("Vergiftung", f.Oberhaupt.Krankheit?.Name);
            Pruefe.Gleich(Vergehen.Giftanschlag, w.Justiz.SpurenVon(taeter).Single().Art);
            var nr = f.Oberhaupt.Nummer;
            var m = w.Ausfuehren(opfer, new MedicusRufen(nr));
            Pruefe.Wahr(!m.Erfolg && m.Meldung.Contains("Gegengift"), m.Meldung);
            Pruefe.Wahr(!w.Ausfuehren(opfer, new SchnapsGeben(nr)).Erfolg, "Schnaps hilft nicht");
            var preis = w.Gesundheit.GegengiftPreis(opfer);
            var familie = opfer.Laufend.Familie;
            Pruefe.Wahr(w.Ausfuehren(opfer, new GegengiftGeben(nr)).Erfolg, "Gegengift");
            Pruefe.Wahr(f.Oberhaupt.Krankheit is null, "geheilt");
            Pruefe.Ungefaehr(familie + preis, opfer.Laufend.Familie, 1e-6);
            Pruefe.Wahr(w.Justiz.Daten.KerkerFuer.Contains(Vergehen.Giftanschlag), "Giftanschlag führt in den Kerker");
        }),

        ("Ohne Gift kein Wurf: Eine Partie ohne Anschlag verläuft gleich, ob Düfte in den Daten stehen oder nicht", () =>
        {
            var ohne = Wirtschaftsdaten.AusJson(Json.Replace("\"parfum\": {", "\"parfumAus\": {"));
            Pruefe.Wahr(ohne.Parfum is null, "Abschnitt entfernt");
            var a = NeueSimulation();
            var b = NeueSimulation(ohne);
            for (var i = 0; i < 26 * 60; i++) { a.Tick(); b.Tick(); }
            Pruefe.Ungefaehr(a.Wirtschaft!.Konten.Sum(k => k.Vermoegen), b.Wirtschaft!.Konten.Sum(k => k.Vermoegen), 1e-9);
        }),

        ("KI: Eine wagemutige KI vergiftet das führende Haus; die vergiftete KI nimmt Gegengift", () =>
        {
            var sim = NeueSimulation(SichererDolch.Value);
            var (w, ki, _) = Haus(sim, "Haus Rabe", geld: 6_000);
            w.KiAnmelden(ki, Partieaufbau.ProfilFuer(KiCharakter.Wagemutig));
            var (_, reich, f) = Haus(sim, "Haus Gold", geld: 80_000);
            w.KiAnmelden(reich, KiProfil.Standard with { Reserve = 78_000 }); // kauft keine Werkstatt – sonst lohnte eine Bande mehr als das Gift
            for (var i = 0; i < 7 * 60; i++) sim.Tick(); // über die Politikstunde
            Pruefe.Gleich(1, w.Duft.AnschlaegeDiesesJahr(ki), string.Join(" | ", w.Rivalen.Single(r => r.Konto == ki).Entscheidungen));
            Pruefe.Gleich(reich, w.Duft.Auftraege.Single().Ziel);
            BisNacht(sim);
            Pruefe.Wahr(f.Oberhaupt.Krankheit?.NurGegengift == true, "vergiftet");
            JahrBeenden(sim);
            Pruefe.Wahr(f.Oberhaupt.Lebt && f.Oberhaupt.Krankheit?.NurGegengift == true, "im ersten Jahr stirbt niemand am Gift");
            for (var i = 0; i < 7 * 60; i++) sim.Tick(); // die Politikstunde des neuen Jahres
            Pruefe.Wahr(f.Oberhaupt.Krankheit is null, "Gegengift genommen");
        }),

        ("Speichern: Giftanschlag der Nacht, Moschus des Jahres und Heiligenduft überstehen Speichern und Laden; Startberuf Parfumeur", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos },
                Daten.Value, new NeuePartie(Beruf: "Parfumeur", Startvermoegen: 10_000));
            var w = sim.Wirtschaft!;
            Pruefe.Gleich("Siederei am Weinmarkt", w.Betriebe.Single(b => b.Konto == spieler).Name);
            spieler.Aktionspunkte.Setzen(12);
            var eisenbart = w.Konten.Single(k => k.Inhaber == "Haus Eisenbart");
            Pruefe.Wahr(w.Ausfuehren(spieler, new HeiligenduftStiften()).Erfolg, "Heiligenduft");
            var r = w.Rat.Ratsherren[0];
            Pruefe.Wahr(w.Ausfuehren(spieler, new MoschusRatsherr(r.Name)).Erfolg, "Moschus");
            Pruefe.Wahr(w.Ausfuehren(spieler, new Giftanschlag(eisenbart.Inhaber, "Giftdolch")).Erfolg, "Gift");
            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var kw = kopie.Wirtschaft!;
            Pruefe.Gleich(1, kw.Duft.Auftraege.Count);
            Pruefe.Wahr(kw.Duft.MoschusSchonRatsherr(kopieSpieler!, r.Name), "Moschus gemerkt");
            Pruefe.Gleich(w.Duft.HeiligBis(spieler), kw.Duft.HeiligBis(kopieSpieler!));
            for (var i = 0; i < Spielzeit.MinutenProTag; i++) { sim.Tick(); kopie.Tick(); }
            var o1 = w.Familien.Von(eisenbart)!.Oberhaupt;
            var o2 = kw.Familien.Von(kw.Konten.Single(k => k.Inhaber == "Haus Eisenbart"))!.Oberhaupt;
            Pruefe.Gleich(o1.Krankheit?.Name, o2.Krankheit?.Name);
            Pruefe.Gleich(o1.Lebt, o2.Lebt);
        }),
    };
}
