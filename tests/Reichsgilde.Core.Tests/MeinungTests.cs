using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Justiz;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Politik;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für die Meinungen und Aktivitäten (Charaktersystem Teil 2, PR #231).</summary>
static class MeinungTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    /// <summary>Dieselben Daten ohne den Abschnitt "meinungen" – wie ein Spielstand von vor PR #231.</summary>
    static readonly Lazy<Wirtschaftsdaten> OhneMeinungen = new(() => Ohne("meinungen"));

    static Wirtschaftsdaten Ohne(string abschnitt)
    {
        var knoten = JsonNode.Parse(Json.Value, documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!.AsObject();
        knoten.Remove(abschnitt);
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    }

    static Simulation NeueSimulation(int startwert = 1400, Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static (Konto Konto, Familie Familie) Haus(Stadtwirtschaft w, string name, Volk volk = Volk.Mensch, Konfession glaube = Konfession.Katholisch,
        double geld = 50_000)
    {
        var konto = w.KontoEroeffnen(name, startvermoegen: geld);
        var f = w.Familien.Gruenden(konto, volk, Geschlecht.Mann, alter: 30, glaube: glaube);
        konto.Aktionspunkte.Setzen(12);
        return (konto, f);
    }

    /// <summary>Setzt die Züge einer Person fest (Kennungen).</summary>
    static void Zuege(Stadtwirtschaft w, Person p, params string[] kennungen)
    {
        p.ZuegeLeeren();
        foreach (var k in kennungen) p.ZugHinzufuegen(w.Daten.Charakterzuege!.Zug(k)!);
    }

    static double Grund(Stadtwirtschaft w, ICharakter von, ICharakter ueber, string text) =>
        w.Meinungen.Gruende(von, ueber).Where(g => g.Text == text).Sum(g => g.Wert);

    static int _nummer = 95_000;

    static Person Kind(Stadtwirtschaft w, Familie f, int alter, Geschlecht g = Geschlecht.Mann)
    {
        var p = new Person(_nummer++, "Kind", f.Nachname, f.Volk, g, w.Familien.Jahr - alter, vater: f.Oberhaupt.Nummer);
        p.GlaubeSetzen(f.Oberhaupt.Glaube);
        f.MitgliederListe.Add(p);
        return p;
    }

    static Person Verheiraten(Stadtwirtschaft w, Konto konto)
    {
        var k = w.Familien.Kandidaten.First(x => w.Familien.WerbenVerboten(konto, x) is null);
        w.Familien.ZuneigungSchenken(konto, k.Person.Nummer, 500);
        var e = w.Ausfuehren(konto, new Heiraten(k.Person.Nummer));
        Pruefe.Wahr(e.Erfolg, e.Meldung);
        return w.Familien.Von(konto)!.Ehepartner!;
    }

    /// <summary>Ein Ratsherr mit festen Zügen (aus den Daten).</summary>
    static Wirtschaftsdaten MitRatsherrnZuegen(string name, string zuege) =>
        Wirtschaftsdaten.AusJson(Json.Value.Replace($"\"name\": \"{name}\",", $"\"name\": \"{name}\", \"zuege\": [{zuege}],"));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Meinungen: Daten – Datenversion 61, sechs Aktivitäten (eine davon das Geschenk), Fehler fallen auf", () =>
        {
            var d = Daten.Value.Meinungen!;
            Pruefe.Gleich(64, Daten.Value.Version);
            Pruefe.Gleich("Geschenk,Gastmahl,Zechen,Messe,Brettspiel,Jagd", string.Join(",", d.Aktivitaeten.Keys));
            Pruefe.Wahr(d.Aktivitaeten["Geschenk"].Geschenk, "Geschenk");
            Pruefe.Gleich(2, d.Aktivitaeten["Jagd"].Ap);
            Pruefe.Gleich("Patrizier", d.Aktivitaeten["Jagd"].MindestStand);
            Pruefe.Wahr(d.Aktivitaeten["Messe"].GleicherGlaube, "Messe nur mit gleichem Glauben");
            Pruefe.Gleich(0.3, d.VerfallJeJahr);
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"mag\": [ \"Mutig\", \"Stark\" ]", "\"mag\": [ \"Mutig\", \"Bucklig\" ]")),
                "Charakterzug „Bucklig“ gibt es nicht");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"mindestStand\": \"Patrizier\"", "\"mindestStand\": \"Kaiser\"")),
                "mindestStand „Kaiser“ gibt es nicht");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"verletzung\": \"Knochenbruch\"", "\"verletzung\": \"Schnupfen\"")),
                "Krankheit „Schnupfen“ gibt es nicht");
            Pruefe.Gleich(0.8, Daten.Value.Charakterzuege!.Zug("Bescheiden")!.Daten.Bestechlich); // Bescheiden nimmt ungern Geschenke
        }),

        ("Meinungen: Züge – gleicher Zug +10, Gegenteil −10, Schön für alle +10; dazu Volk und Glaube; −100 … +100", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (_, a) = Haus(w, "Haus Anna");
            var (_, b) = Haus(w, "Haus Berta", glaube: Konfession.Evangelisch);
            Zuege(w, a.Oberhaupt, "Gierig", "Fromm", "Mutig");
            Zuege(w, b.Oberhaupt, "Gierig", "Zynisch", "Geduldig", "Schoen");
            Pruefe.Gleich(10.0, Grund(w, a.Oberhaupt, b.Oberhaupt, "Beide gierig"));
            Pruefe.Gleich(-10.0, Grund(w, a.Oberhaupt, b.Oberhaupt, "Fromm gegen zynisch"));
            Pruefe.Gleich(10.0, Grund(w, a.Oberhaupt, b.Oberhaupt, "Schön"));
            Pruefe.Gleich(5.0, Grund(w, a.Oberhaupt, b.Oberhaupt, "Gleiches Volk (Menschen)"));
            Pruefe.Gleich(-3.0, Grund(w, a.Oberhaupt, b.Oberhaupt, "Anderer Glaube (evangelisch)"));
            Pruefe.Ungefaehr(12, w.Meinungen.Meinung(a.Oberhaupt, b.Oberhaupt), 1e-9);
            // Umgekehrt: Schön zählt nur für den, der hinsieht – B sieht keinen schönen A.
            Pruefe.Ungefaehr(10 - 10 + 5 - 3, w.Meinungen.Meinung(b.Oberhaupt, a.Oberhaupt), 1e-9);
            Pruefe.Wahr(w.Meinungen.Gruende(a.Oberhaupt, b.Oberhaupt).All(g => !g.Verblasst), "feste Gründe verblassen nicht");
            // Höchstwert
            w.Meinungen.TatEintragen(a.Oberhaupt.Kennung, b.Oberhaupt.Kennung, "test", "Test", 500);
            Pruefe.Ungefaehr(100, w.Meinungen.Meinung(a.Oberhaupt, b.Oberhaupt), 1e-9);
        }),

        ("Meinungen: Familie – Ehe +20, Eltern und Kinder +20, Geschwister +10, Enkel und Schwiegerfamilie +5; ein Erbe über 30 wartet ungeduldig (−3 je Jahr, höchstens −30)", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Sippe");
            var partner = Verheiraten(w, konto);
            Pruefe.Gleich(20.0, Grund(w, partner, f.Oberhaupt, f.Oberhaupt.Ehepartnertitel));
            Pruefe.Gleich(20.0, Grund(w, f.Oberhaupt, partner, partner.Ehepartnertitel));
            var sohn = Kind(w, f, 20);
            var tochter = Kind(w, f, 18, Geschlecht.Frau);
            Pruefe.Gleich(20.0, Grund(w, sohn, f.Oberhaupt, "Vater"));
            Pruefe.Gleich(20.0, Grund(w, f.Oberhaupt, tochter, "Tochter"));
            Pruefe.Gleich(10.0, Grund(w, sohn, tochter, "Schwester"));
            var enkel = new Person(_nummer++, "Enkel", f.Nachname, f.Volk, Geschlecht.Mann, w.Familien.Jahr - 1, vater: sohn.Nummer);
            f.MitgliederListe.Add(enkel);
            Pruefe.Gleich(5.0, Grund(w, enkel, f.Oberhaupt, "Großvater"));
            Pruefe.Gleich(5.0, Grund(w, f.Oberhaupt, enkel, "Enkel"));
            Pruefe.Gleich(0.0, w.Meinungen.Gruende(enkel, partner).Where(g => g.Text.StartsWith("Groß")).Sum(g => g.Wert)); // nicht ihr Enkel (sohn ist nur Kind des Vaters)
            Pruefe.Gleich(0.0, Grund(w, sohn, f.Oberhaupt, "Wartet aufs Erbe"));
            var alt = Kind(w, f, 40); // der Älteste erbt – und wartet
            Pruefe.Gleich(alt, w.Familien.Erbe(f));
            Pruefe.Gleich(-30.0, Grund(w, alt, f.Oberhaupt, "Wartet aufs Erbe"));
            Pruefe.Wahr(w.Meinungen.WichtigePersonen().Any(p => p.Charakter == alt && p.Rolle.StartsWith("Erbe")), "Erbe ist wichtig");
            Pruefe.Wahr(w.Meinungen.WichtigePersonen().Any(p => p.Charakter == partner), "Ehepartner ist wichtig");
            Pruefe.Wahr(w.Meinungen.WichtigePersonen().All(p => p.Charakter != sohn), "nur der Erbe, nicht alle Kinder");
        }),

        ("Meinungen: Stand – wer höher steht, wird geachtet (+3 je Stufe, höchstens 9); ein Kandidat achtet das Haus, um das er geworben wird", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (k1, a) = Haus(w, "Haus Hoch");
            var (_, b) = Haus(w, "Haus Tief");
            k1.Stand = 2;
            Pruefe.Gleich(6.0, w.Meinungen.Gruende(b.Oberhaupt, a.Oberhaupt).Single(g => g.Text.StartsWith("Höherer Stand")).Wert);
            Pruefe.Gleich(-6.0, w.Meinungen.Gruende(a.Oberhaupt, b.Oberhaupt).Single(g => g.Text.StartsWith("Niedrigerer Stand")).Wert);
            k1.Stand = 5;
            Pruefe.Gleich(9.0, w.Meinungen.Gruende(b.Oberhaupt, a.Oberhaupt).Single(g => g.Text.StartsWith("Höherer Stand")).Wert);
            var kandidat = w.Familien.Kandidaten.First(x => x.Stand == 0);
            Pruefe.Gleich(9.0, w.Meinungen.Gruende(kandidat.Person, a.Oberhaupt).Single(g => g.Text.StartsWith("Höherer Stand")).Wert);
        }),

        ("Meinungen: Geschenk an einen Ratsherrn – Grund „Geschenke“, jedes weitere im Jahr ×0,7, verblasst 30 % im Jahr, bleibt Bestechung", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Gabe");
            var r = w.Rat.Ratsherren.First(x => Math.Abs(x.Bestechlichkeit - 1) < 1e-9);
            var vorher = w.Rat.Meinung(r, konto);
            Pruefe.Wahr(w.Ausfuehren(konto, new Aktivitaet("Geschenk", r.Kennung, 200)).Erfolg, "Geschenk");
            Pruefe.Ungefaehr(10, w.Rat.Gunst(r, konto), 1e-9); // 200 × 0,05
            Pruefe.Ungefaehr(vorher + 10, w.Rat.Meinung(r, konto), 1e-9);
            Pruefe.Ungefaehr(0.05 * 0.7, w.Rat.GunstJeGulden(r, konto), 1e-9);
            Pruefe.Wahr(w.Ausfuehren(konto, new Schenken(r.Name, 200)).Erfolg, "zweites Geschenk");
            Pruefe.Ungefaehr(17, w.Rat.Gunst(r, konto), 1e-9); // + 200 × 0,05 × 0,7
            Pruefe.Gleich(2, w.Justiz.SpurenVon(konto).Count(s => s.Art == Vergehen.Bestechung));
            Pruefe.Wahr(w.Meinungen.Gruende(r, f.Oberhaupt).Single(g => g.Text == "Geschenke").Verblasst, "Geschenke verblassen");
            Pruefe.Gleich(10, konto.Aktionspunkte.Vorrat);
            JahrBeenden(sim);
            Pruefe.Ungefaehr(17 * 0.7, w.Rat.Gunst(r, konto), 1e-9);
            Pruefe.Ungefaehr(0.05, w.Rat.GunstJeGulden(r, konto), 1e-9); // neues Jahr, volle Wirkung
            // An andere wichtige Personen: keine Bestechung, Wirkung nach ihren Zügen (Gierig nimmt gern).
            var (andere, g) = Haus(w, "Haus Gier");
            Zuege(w, g.Oberhaupt, "Gierig");
            var e = w.Ausfuehren(konto, new Aktivitaet("Geschenk", g.Oberhaupt.Kennung, 100));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            Pruefe.Ungefaehr(100 * 0.05 * 1.3, Grund(w, g.Oberhaupt, f.Oberhaupt, "Geschenke"), 1e-9);
            Pruefe.Gleich(2, w.Justiz.SpurenVon(konto).Count(s => s.Art == Vergehen.Bestechung));
            // Wer kein Ratsherr ist, lässt sich mit Geschenken insgesamt nur bis +15 gewinnen (Entscheidung des Teamleiters).
            Pruefe.Wahr(w.Ausfuehren(konto, new Aktivitaet("Geschenk", g.Oberhaupt.Kennung, 1000)).Erfolg, "großes Geschenk");
            Pruefe.Ungefaehr(15, Grund(w, g.Oberhaupt, f.Oberhaupt, "Geschenke"), 1e-9);
            var angebot = w.Meinungen.Aktivitaet(konto, g.Oberhaupt, "Geschenk", 500)!;
            Pruefe.Ungefaehr(0, angebot.Wirkung, 1e-9);
            Pruefe.Wahr(angebot.Hinweis.Contains("höchstens bis +15"), angebot.Hinweis);
            var gunstVorher = w.Rat.Gunst(r, konto);
            Pruefe.Wahr(w.Ausfuehren(konto, new Schenken(r.Name, 200)).Erfolg, "Ratsherr ohne Deckel");
            Pruefe.Ungefaehr(gunstVorher + 200 * w.Rat.GunstJeGulden(r), w.Rat.Gunst(r, konto), 1e-9); // erstes Geschenk im neuen Jahr
            Pruefe.Wahr(w.Rat.Gunst(r, konto) > 15, "über 15");
        }),

        ("Meinungen: Aktivität – Kosten, Wirkung +8 (Gesellig ×1,5, Scheu ×0,5 und doppelte Gefahr), Talent senkt die Gefahr, einmal im Jahr je Person", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Gast");
            var (_, b) = Haus(w, "Haus Geselle");
            Zuege(w, f.Oberhaupt);
            Zuege(w, b.Oberhaupt, "Gesellig");
            var angebot = w.Meinungen.Aktivitaet(konto, b.Oberhaupt, "Gastmahl")!;
            Pruefe.Gleich(1, angebot.Ap);
            Pruefe.Gleich(60.0, angebot.Gulden);
            Pruefe.Ungefaehr(12, angebot.Wirkung, 1e-9);
            Pruefe.Ungefaehr(Math.Max(0, 0.1 - f.Oberhaupt.Talent(Talent.Rhetorik) * 0.02), angebot.Gefahr, 1e-9);
            Pruefe.Wahr(angebot.Hinweis.Contains("Gesellig: mag es"), angebot.Hinweis);
            Pruefe.Gleich(null, angebot.Sperrgrund);
            Zuege(w, b.Oberhaupt, "Scheu");
            var scheu = w.Meinungen.Aktivitaet(konto, b.Oberhaupt, "Gastmahl")!;
            Pruefe.Ungefaehr(4, scheu.Wirkung, 1e-9);
            f.Oberhaupt.TalentSetzen(Talent.Rhetorik, 0);
            Pruefe.Ungefaehr(0.2, w.Meinungen.Aktivitaet(konto, b.Oberhaupt, "Gastmahl")!.Gefahr, 1e-9);
            f.Oberhaupt.TalentSetzen(Talent.Rhetorik, 3);
            Pruefe.Ungefaehr(0.14, w.Meinungen.Aktivitaet(konto, b.Oberhaupt, "Gastmahl")!.Gefahr, 1e-9);
            Pruefe.Ungefaehr(0.86 * 4 + 0.14 * -5, w.Meinungen.Aktivitaet(konto, b.Oberhaupt, "Gastmahl")!.Erwartet, 1e-9);

            var geld = konto.Vermoegen;
            var vorher = w.Meinungen.Meinung(b.Oberhaupt, f.Oberhaupt);
            var e = w.Ausfuehren(konto, new Aktivitaet("Gastmahl", b.Oberhaupt.Kennung));
            Pruefe.Wahr(e.Erfolg, e.Meldung);
            var nachher = w.Meinungen.Meinung(b.Oberhaupt, f.Oberhaupt);
            Pruefe.Wahr(Math.Abs(nachher - vorher - 4) < 1e-9 || Math.Abs(nachher - vorher + 5) < 1e-9, $"Gastmahl: {vorher} → {nachher}");
            Pruefe.Ungefaehr(geld - 60, konto.Vermoegen, 1e-9);
            Pruefe.Gleich(11, konto.Aktionspunkte.Vorrat);
            var zweimal = w.Ausfuehren(konto, new Aktivitaet("Gastmahl", b.Oberhaupt.Kennung));
            Pruefe.Wahr(!zweimal.Erfolg && zweimal.Meldung.Contains("in diesem Jahr schon"), zweimal.Meldung);
            Pruefe.Wahr(w.Ausfuehren(konto, new Aktivitaet("Brettspiel", b.Oberhaupt.Kennung)).Erfolg, "andere Art geht");
            Pruefe.Wahr(!w.Ausfuehren(konto, new Aktivitaet("Gastmahl", f.Oberhaupt.Kennung)).Erfolg, "nicht mit sich selbst");
            Pruefe.Wahr(!w.Ausfuehren(konto, new Aktivitaet("Tanz", b.Oberhaupt.Kennung)).Erfolg, "unbekannte Aktivität");
        }),

        ("Meinungen: Messe nur mit gleichem Glauben, Jagd erst ab Patrizier (2 AP); keine Aktivität mit Kindern, die nicht erben", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Kirche");
            var (_, ev) = Haus(w, "Haus Luther", glaube: Konfession.Evangelisch);
            var (_, kath) = Haus(w, "Haus Rom");
            Pruefe.Wahr(w.Meinungen.Aktivitaet(konto, ev.Oberhaupt, "Messe")!.Sperrgrund!.Contains("anderen Glauben"), "Messe mit Evangelischen");
            Pruefe.Gleich(null, w.Meinungen.Aktivitaet(konto, kath.Oberhaupt, "Messe")!.Sperrgrund);
            Pruefe.Wahr(w.Meinungen.Aktivitaet(konto, kath.Oberhaupt, "Jagd")!.Sperrgrund!.Contains("Patrizier"), "Jagd als Gemeiner");
            konto.Stand = w.Daten.StandStufe("Patrizier");
            Pruefe.Gleich(null, w.Meinungen.Aktivitaet(konto, kath.Oberhaupt, "Jagd")!.Sperrgrund);
            Pruefe.Wahr(w.Ausfuehren(konto, new Aktivitaet("Jagd", kath.Oberhaupt.Kennung)).Erfolg, "Jagd");
            Pruefe.Gleich(10, konto.Aktionspunkte.Vorrat);
            Pruefe.Gleich(2, w.ApKosten(konto, new Aktivitaet("Jagd", kath.Oberhaupt.Kennung)));
            Kind(w, f, 30);
            var jung = Kind(w, f, 5);
            Pruefe.Wahr(w.Meinungen.Aktivitaet(konto, jung, "Brettspiel")!.Sperrgrund!.Contains("nicht zu den wichtigen"), "jüngeres Kind");
            konto.Aktionspunkte.Setzen(0);
            Pruefe.Wahr(w.Meinungen.Aktivitaet(konto, kath.Oberhaupt, "Brettspiel")!.Sperrgrund!.Contains("AP"), "ohne AP");
        }),

        ("Meinungen: Rat – Wahlpunkte = Ansehen + Meinung + Rhetorik + Amt; Nachbarstatut ab Meinung 25 (Züge des Ratsherrn zählen mit)", () =>
        {
            const string name = "Ulrich Wendelstein";
            var w = NeueSimulation(daten: MitRatsherrnZuegen(name, "\"Gierig\", \"Fromm\", \"Mutig\"")).Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Wahl");
            Zuege(w, f.Oberhaupt, "Gierig", "Fromm", "Mutig");
            var r = w.Rat.RatsherrNamens(name)!;
            var amt = w.Rat.Aemter.First(a => a.Stufe == 1);
            Pruefe.Ungefaehr(30 + 5 + 5, w.Rat.Meinung(r, konto), 1e-9); // drei gleiche Züge, Volk, Glaube
            Pruefe.Ungefaehr(w.Rat.Ansehen(konto) + w.Rat.Meinung(r, konto) + w.Familien.RhetorikPunkte(konto), w.Rat.Punkte(r, amt, konto), 1e-9);
            var nachbar = Enum.GetValues<Fronstatut>().First(s => Math.Abs((int)s - (int)r.Haltung) == 1);
            Pruefe.Wahr(w.Rat.StimmtZu(r, nachbar, konto), "Meinung 40 ≥ 25");
            Zuege(w, f.Oberhaupt, "Grosszuegig", "Zynisch", "Feige");
            Pruefe.Ungefaehr(-30 + 10, w.Rat.Meinung(r, konto), 1e-9);
            Pruefe.Wahr(!w.Rat.StimmtZu(r, nachbar, konto), "Meinung −20 < 25");
        }),

        ("Meinungen: Haushalt – ein zufriedener Ehepartner spart (−0,2 % je Punkt, höchstens 10 %), ein unzufriedener kostet", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Herd");
            var partner = Verheiraten(w, konto);
            Zuege(w, f.Oberhaupt);
            Zuege(w, partner);
            var meinung = w.Meinungen.Meinung(partner, f.Oberhaupt);
            Pruefe.Ungefaehr(1 - meinung * 0.002, w.Meinungen.HaushaltFaktor(f), 1e-9);
            w.Meinungen.TatEintragen(partner.Kennung, f.Oberhaupt.Kennung, "test", "Test", 100);
            Pruefe.Ungefaehr(0.9, w.Meinungen.HaushaltFaktor(f), 1e-9);
            w.Meinungen.TatEintragen(partner.Kennung, f.Oberhaupt.Kennung, "test", "Test", -200);
            Pruefe.Ungefaehr(1 - Math.Max(-0.1, w.Meinungen.Meinung(partner, f.Oberhaupt) * 0.002), w.Meinungen.HaushaltFaktor(f), 1e-9);
            Pruefe.Wahr(w.Meinungen.HaushaltFaktor(f) > 1, "unzufrieden kostet mehr");
        }),

        ("Meinungen: Streit im Haus – ein Erbe mit Meinung ≤ −50 über das Oberhaupt: Ansehen −5 bis Ende des nächsten Jahres", () =>
        {
            var daten = Wirtschaftsdaten.AusJson(Json.Value.Replace("\"streit\": { \"ab\": -50, \"chance\": 0.25", "\"streit\": { \"ab\": -50, \"chance\": 1"));
            var sim = NeueSimulation(daten: daten);
            var w = sim.Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Zank");
            var erbe = Kind(w, f, 25);
            w.Meinungen.TatEintragen(erbe.Kennung, f.Oberhaupt.Kennung, "test", "Test", -120); // höchstens −100 je Art
            JahrBeenden(sim);
            Pruefe.Wahr(w.Familien.LetztesJahr.Concat(w.Familien.DiesesJahr).Any(e => e.Text.StartsWith("Streit im Haus")), "Streit gemeldet");
            Pruefe.Gleich(5.0, w.Meinungen.AnsehenAbzug(konto));
            // Der Abzug wirkt noch in der Ratssitzung am Ende des folgenden Jahres (Wahlpunkte).
            konto.Stand = w.Daten.StandStufe("Bürger");
            var amt = w.Rat.Aemter.First(a => a.Stufe == 1);
            Pruefe.Wahr(w.Ausfuehren(konto, new AmtBewerben(amt.Name)).Erfolg, "Bewerbung");
            double? inDerSitzung = null;
            w.RatssitzungAnhalten = _ => { inDerSitzung = w.Meinungen.AnsehenAbzug(konto); return false; };
            w.Meinungen.TatEintragen(erbe.Kennung, f.Oberhaupt.Kennung, "test", "Test", 200); // versöhnt
            JahrBeenden(sim);
            Pruefe.Gleich(5.0, inDerSitzung);
            Pruefe.Gleich(0.0, w.Meinungen.AnsehenAbzug(konto)); // danach ist er vorbei
        }),

        ("Meinungen: Taten – Fest +5 für alle wichtigen Personen, Klage −15, ertappter Diebstahl −20 (nur beim Opfer), Stimme im Rat ±5", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (a, fa) = Haus(w, "Haus Fest");
            var (b, fb) = Haus(w, "Haus Opfer");
            w.BetriebEroeffnen("Gießerei Opfer", Betriebsart.Giesserei, b);
            w.Meinungen.FestAusgerichtet(a);
            Pruefe.Gleich(5.0, Grund(w, fb.Oberhaupt, fa.Oberhaupt, "Hat ein Fest ausgerichtet"));
            Pruefe.Gleich(5.0, Grund(w, w.Rat.Ratsherren[3], fa.Oberhaupt, "Hat ein Fest ausgerichtet"));
            Pruefe.Gleich(0.0, Grund(w, fa.Oberhaupt, fa.Oberhaupt, "Hat ein Fest ausgerichtet"));
            w.Meinungen.Verklagt(b, a);
            Pruefe.Gleich(-15.0, Grund(w, fa.Oberhaupt, fb.Oberhaupt, "Hat ihn verklagt"));
            var spur = w.Justiz.TatMelden(a, Vergehen.Diebstahl, "Gießerei Opfer", 300)!;
            w.Justiz.BeweisGeben(b, spur);
            Pruefe.Gleich(-20.0, Grund(w, fb.Oberhaupt, fa.Oberhaupt, "Ertappt: Diebstahl"));
            var (c, fc) = Haus(w, "Haus Wache");
            w.Justiz.BeweisGeben(c, spur); // kein Opfer – nur ein Zeuge
            Pruefe.Gleich(0.0, Grund(w, fc.Oberhaupt, fa.Oberhaupt, "Ertappt: Diebstahl"));
            var (namensvetter, fn) = Haus(w, "Opfer"); // „Opfer“ steckt in „Gießerei Opfer“ – zählt nicht
            w.Justiz.BeweisGeben(namensvetter, spur);
            Pruefe.Gleich(0.0, Grund(w, fn.Oberhaupt, fa.Oberhaupt, "Ertappt: Diebstahl"));
            var sitz = w.Rat.Aemter.First(x => x.Ratsherr is not null);
            w.Meinungen.StimmeGemerkt(sitz, a, true);
            Pruefe.Gleich(5.0, Grund(w, fa.Oberhaupt, sitz.Ratsherr!, "Stimmte im Rat für ihn"));
            w.Meinungen.StimmeGemerkt(sitz, b, false);
            Pruefe.Gleich(-5.0, Grund(w, fb.Oberhaupt, sitz.Ratsherr!, "Stimmte im Rat gegen ihn"));
            Pruefe.Wahr(w.Meinungen.Gruende(fb.Oberhaupt, fa.Oberhaupt).Where(g => g.Text.StartsWith("Ertappt")).All(g => g.Verblasst), "Taten verblassen");
        }),

        ("Meinungen: Brautschau – die Meinung des Kandidaten zählt halb zur Bereitschaft und macht das Werben billiger (0,5 % je Punkt)", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (konto, f) = Haus(w, "Haus Braut");
            var k = w.Familien.Kandidaten.First(x => w.Familien.WerbenVerboten(konto, x) is null);
            var meinung = w.Meinungen.Meinung(k.Person, f.Oberhaupt);
            Pruefe.Ungefaehr(w.Familien.Zuneigung(konto, k) + w.Rat.Ansehen(konto) + meinung * 0.5, w.Familien.Bereitschaft(konto, k), 1e-9);
            Pruefe.Ungefaehr(w.Familien.GuldenJeZuneigung(konto) * Math.Clamp(1 - meinung * 0.005, 0.75, 1.25), w.Familien.GuldenJeZuneigung(konto, k), 1e-9);
        }),

        ("Meinungen: Spielstand – Gründe aus Taten, Aktivitäten des Jahres, Streit und Würfel bleiben; danach rechnet die Partie gleich weiter", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 21 },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            spieler.Aktionspunkte.Setzen(12);
            var r = w.Rat.Ratsherren[0];
            Pruefe.Wahr(w.Ausfuehren(spieler, new Aktivitaet("Brettspiel", r.Kennung)).Erfolg, "Brettspiel");
            Pruefe.Wahr(w.Ausfuehren(spieler, new Schenken(r.Name, 100)).Erfolg, "Geschenk");
            for (var i = 0; i < 2; i++) JahrBeenden(sim);
            Pruefe.Wahr(w.Ausfuehren(spieler, new Aktivitaet("Gastmahl", r.Kennung)).Erfolg, "Gastmahl");
            var zeitpunkt = new DateTime(2026, 10, 28, 12, 0, 0);
            var json = Speicherung.Speichern(sim, spieler, zeitpunkt);
            Pruefe.Wahr(json.Contains("\"Meinungen\": {"), "Meinungen im Spielstand");
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt));
            var g = geladen.Wirtschaft!;
            Pruefe.Ungefaehr(w.Rat.Meinung(r, spieler), g.Rat.Meinung(g.Rat.RatsherrNamens(r.Name)!, geladenerSpieler!), 1e-9);
            Pruefe.Wahr(!g.Ausfuehren(geladenerSpieler!, new Aktivitaet("Gastmahl", r.Kennung)).Erfolg, "Gastmahl schon in diesem Jahr");
            for (var i = 0; i < 4; i++) { JahrBeenden(sim); JahrBeenden(geladen); }
            Pruefe.Gleich(Speicherung.Speichern(sim, spieler, zeitpunkt), Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt));
        }),

        ("Meinungen: alter Spielstand – die Gunst der Ratsherren wird zum Grund „Geschenke“", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 22 },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var knoten = JsonNode.Parse(Speicherung.Speichern(sim, spieler))!;
            var wirtschaft = knoten["Wirtschaft"]!.AsObject();
            wirtschaft.Remove("Meinungen");
            var nummer = w.Konten.ToList().IndexOf(spieler);
            wirtschaft["Rat"]!["Gunst"] = new JsonArray(new JsonObject { ["Ratsherr"] = "Ulrich Wendelstein", ["Konto"] = nummer, ["Wert"] = 23.5 });
            var (geladen, geladenerSpieler) = Speicherung.Laden(knoten.ToJsonString());
            var g = geladen.Wirtschaft!;
            var r = g.Rat.RatsherrNamens("Ulrich Wendelstein")!;
            Pruefe.Ungefaehr(23.5, g.Rat.Gunst(r, geladenerSpieler!), 1e-9);
            var o = g.Familien.Von(geladenerSpieler!)!.Oberhaupt;
            Pruefe.Ungefaehr(23.5, g.Meinungen.Gruende(r, o).Single(x => x.Text == "Geschenke").Wert, 1e-9);
        }),

        ("Meinungen: ohne Abschnitt „meinungen“ – nur die frühere Gunst mit Volk und Glaube, keine Aktivitäten, kein Haushaltsrabatt", () =>
        {
            var w = NeueSimulation(daten: OhneMeinungen.Value).Wirtschaft!;
            Pruefe.Wahr(!w.Meinungen.IstAktiv, "ohne Abschnitt aus");
            var (konto, f) = Haus(w, "Haus Alt");
            var r = w.Rat.Ratsherren.First(x => x.Daten.Volk == Volk.Mensch);
            Pruefe.Wahr(w.Ausfuehren(konto, new Schenken(r.Name, 200)).Erfolg, "Geschenk");
            Pruefe.Wahr(w.Ausfuehren(konto, new Schenken(r.Name, 200)).Erfolg, "zweites Geschenk");
            Pruefe.Ungefaehr(400 * 0.05 * r.Bestechlichkeit, w.Rat.Gunst(r, konto), 1e-9); // ohne Abnahme
            Pruefe.Ungefaehr(w.Rat.Gunst(r, konto) + w.Familien.Volksnaehe(r.Daten.Volk, konto) + w.Glaube.Naehe(r, konto), w.Rat.Meinung(r, konto), 1e-9);
            var e = w.Ausfuehren(konto, new Aktivitaet("Gastmahl", r.Kennung));
            Pruefe.Wahr(!e.Erfolg && e.Meldung.Contains("keine Aktivitäten"), e.Meldung);
            Pruefe.Gleich(1.0, w.Meinungen.HaushaltFaktor(f));
            Pruefe.Gleich(0, w.Meinungen.Aktivitaeten(konto, r).Count);
        }),
    };
}
