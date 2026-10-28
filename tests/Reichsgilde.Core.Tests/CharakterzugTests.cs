using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Ki;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

/// <summary>Tests für die Charakterzüge (Charaktersystem Teil 1, PR #227).</summary>
static class CharakterzugTests
{
    static readonly Lazy<string> Json = new(() => File.ReadAllText(Datenpfad.Wirtschaft()));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusJson(Json.Value));

    /// <summary>Dieselben Daten ohne den Abschnitt "charakterzuege" – wie vor PR #227.</summary>
    static readonly Lazy<Wirtschaftsdaten> OhneZuege = new(() =>
    {
        var knoten = JsonNode.Parse(Json.Value, documentOptions: new() { CommentHandling = System.Text.Json.JsonCommentHandling.Skip })!.AsObject();
        knoten.Remove("charakterzuege");
        return Wirtschaftsdaten.AusJson(knoten.ToJsonString());
    });

    static Simulation NeueSimulation(int startwert = 1400, Wirtschaftsdaten? daten = null) =>
        new(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert }, wirtschaftsdaten: daten ?? Daten.Value);

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static (Konto Konto, Familie Familie) Haus(Stadtwirtschaft w, string name, Volk volk = Volk.Mensch)
    {
        var konto = w.KontoEroeffnen(name, startvermoegen: 50_000);
        return (konto, w.Familien.Gruenden(konto, volk, Geschlecht.Mann));
    }

    static int Persoenlich(Person p) => p.Zuege.Count(z => z.Art == Zugart.Persoenlichkeit);

    static void KeineGegensaetze(IReadOnlyList<Charakterzug> zuege, string wer)
    {
        foreach (var z in zuege)
            Pruefe.Wahr(z.Gegenteil is null || zuege.All(x => x.Kennung != z.Gegenteil), $"{wer}: {z.Name} und sein Gegenteil");
        Pruefe.Gleich(zuege.Count, zuege.Distinct().Count(), $"{wer}: doppelter Zug");
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Charakterzüge: Daten – 24 Züge, 16 Persönlichkeitszüge in 8 Gegensatzpaaren, 4 angeborene, 4 erworbene; Datenversion 60", () =>
        {
            var d = Daten.Value.Charakterzuege!;
            Pruefe.Gleich(60, Daten.Value.Version);
            Pruefe.Gleich(24, d.Alle.Count);
            var persoenlich = d.Alle.Where(z => z.Art == Zugart.Persoenlichkeit).ToList();
            Pruefe.Gleich(16, persoenlich.Count);
            Pruefe.Wahr(persoenlich.All(z => z.Gegenteil is { } g && d.Zug(g)!.Gegenteil == z.Kennung), "jeder Persönlichkeitszug hat ein Gegenteil, das zurückzeigt");
            Pruefe.Gleich(4, d.Alle.Count(z => z.Art == Zugart.Angeboren));
            Pruefe.Gleich(4, d.Alle.Count(z => z.Art == Zugart.Erworben));
            Pruefe.Gleich(3, d.JePerson);
            Pruefe.Gleich("Jähzornig", d.Zug("Jaehzornig")!.Name);
            Pruefe.Gleich(Zugart.Persoenlichkeit, d.Alle[0].Art); // nach Art geordnet
            Pruefe.Gleich(Zugart.Erworben, d.Alle[^1].Art);
            // Jeder Persönlichkeitszug ändert mindestens ein Talent und das Handeln der KI; für Teil 2 steht schon eine Meinung da.
            foreach (var z in persoenlich)
            {
                Pruefe.Wahr(z.Daten.Talente.Count > 0, $"{z.Name}: Talente");
                Pruefe.Wahr(ZugNeigung.Aus(new[] { z }) != ZugNeigung.Neutral, $"{z.Name}: KI-Verhalten");
                Pruefe.Wahr(!string.IsNullOrWhiteSpace(z.Daten.Verhalten), $"{z.Name}: Verhalten als Text");
                Pruefe.Wahr(z.Daten.Meinung.Gleich > 0 && z.Daten.Meinung.Gegenteil < 0, $"{z.Name}: Meinung");
            }
            Pruefe.Wahr(d.Alle.All(z => Math.Abs(z.Daten.Talente.Values.DefaultIfEmpty(0).Max()) <= 1 && z.Daten.Talente.Values.DefaultIfEmpty(0).Min() >= -1),
                "Talente höchstens ±1 Stern je Zug");
        }),

        ("Charakterzüge: Daten werden geprüft – falsches Gegenteil, unbekannter Anlass, unbekannte Ausbildung, Zug eines Ratsherrn", () =>
        {
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"gegenteil\": \"Grosszuegig\"", "\"gegenteil\": \"Fleissig\"")),
                "muss auf „Gierig“ zurückzeigen");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"abschluss:Universität\": 1", "\"abschluss:Hexenschule\": 1")),
                "Ausbildung „Hexenschule“");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"einbruch\": 0.3", "\"mondfinsternis\": 0.3")),
                "Anlass „mondfinsternis“");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"erziehung\": { \"Kontorlehre\": 2 }", "\"erziehung\": { \"Seefahrt\": 2 }")),
                "Ausbildung „Seefahrt“");
            Pruefe.WirftMitMeldung(() => Wirtschaftsdaten.AusJson(Json.Value.Replace("\"name\": \"Ulrich Wendelstein\",", "\"name\": \"Ulrich Wendelstein\", \"zuege\": [\"Bucklig\"],")),
                "Charakterzug „Bucklig“");
        }),

        ("Charakterzüge: Zu Partiebeginn hat jedes Oberhaupt und jeder Heiratskandidat drei Persönlichkeitszüge ohne Gegensatz", () =>
        {
            for (var s = 0; s < 20; s++)
            {
                var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = s },
                    Daten.Value, new NeuePartie());
                var buch = sim.Wirtschaft!.Familien;
                var personen = sim.Wirtschaft.Konten.Select(k => buch.Von(k)!.Oberhaupt).Concat(buch.Kandidaten.Select(k => k.Person)).ToList();
                foreach (var p in personen)
                {
                    Pruefe.Gleich(3, Persoenlich(p), p.Name);
                    KeineGegensaetze(p.Zuege, p.Name);
                    Pruefe.Wahr(p.Zuege.All(z => z.Art != Zugart.Erworben), $"{p.Name}: nichts erworben");
                }
            }
        }),

        ("Charakterzüge: Ratsherren haben drei Persönlichkeitszüge, in jeder Partie dieselben; feste Züge aus den Daten gehen vor", () =>
        {
            var a = NeueSimulation(1).Wirtschaft!.Rat.Ratsherren;
            var b = NeueSimulation(2).Wirtschaft!.Rat.Ratsherren;
            Pruefe.Wahr(a.Count >= 5, "Ratsherren");
            for (var i = 0; i < a.Count; i++)
            {
                Pruefe.Gleich(3, a[i].Zuege.Count(z => z.Art == Zugart.Persoenlichkeit), a[i].Name);
                KeineGegensaetze(a[i].Zuege, a[i].Name);
                Pruefe.Gleich(string.Join(",", a[i].Zuege), string.Join(",", b[i].Zuege), a[i].Name);
            }
            Pruefe.Wahr(a.Select(r => string.Join(",", r.Zuege)).Distinct().Count() > a.Count / 2, "die Ratsherren unterscheiden sich");

            var name = a[0].Name;
            var daten = Wirtschaftsdaten.AusJson(Json.Value.Replace($"\"name\": \"{name}\",", $"\"name\": \"{name}\", \"zuege\": [\"Gierig\", \"Klug\", \"Fromm\"],"));
            var r = NeueSimulation(1, daten).Wirtschaft!.Rat.RatsherrNamens(name)!;
            Pruefe.Gleich("Gierig,Fromm,Klug", string.Join(",", r.Zuege.Select(z => z.Kennung)));
            Pruefe.Ungefaehr(1.3 * 0.9, r.Bestechlichkeit, 1e-9);
            var w = NeueSimulation(1, daten).Wirtschaft!;
            var konto = w.KontoEroeffnen("Haus Geber", startvermoegen: 5_000);
            var gierig = w.Rat.RatsherrNamens(name)!;
            Pruefe.Ungefaehr(w.Rat.Daten.Gunst.JeGulden * 1.17, w.Rat.GunstJeGulden(gierig), 1e-9);
            Pruefe.Wahr(w.Ausfuehren(konto, new Schenken(name, 100)).Erfolg, "schenken");
            Pruefe.Ungefaehr(100 * w.Rat.Daten.Gunst.JeGulden * 1.17, w.Rat.Gunst(gierig, konto), 1e-9);

            Pruefe.Gleich(0, NeueSimulation(1, OhneZuege.Value).Wirtschaft!.Rat.Ratsherren[0].Zuege.Count);
        }),

        ("Charakterzüge: Talente – Anlage + Züge (gedeckelt bei 6, nicht unter 0) − Krankheit; ohne Züge wie bisher", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (k, f) = Haus(w, "Haus Probe");
            var o = f.Oberhaupt;
            var buch = w.Familien;
            o.ZuegeLeeren();
            o.ZugHinzufuegen(buch.Zug("Gierig")!);     // Verhandeln +½, Rhetorik −½
            o.ZugHinzufuegen(buch.Zug("Hochmuetig")!); // Verhandeln +½, Handwerk −½
            o.ZugHinzufuegen(buch.Zug("Jaehzornig")!); // Kampf +½, Verhandeln −½
            o.TalentSetzen(Talent.Verhandeln, 2);
            o.TalentSetzen(Talent.Rhetorik, 0);
            o.TalentSetzen(Talent.Kampf, 5.75);
            Pruefe.Gleich(0.5, o.Zugwirkung(Talent.Verhandeln));
            Pruefe.Gleich(2.5, o.Talent(Talent.Verhandeln));
            Pruefe.Gleich(2.0, o.Anlage(Talent.Verhandeln));
            Pruefe.Gleich(2.0, o.TalentOhneZuege(Talent.Verhandeln));
            Pruefe.Gleich(0.0, o.Talent(Talent.Rhetorik)); // nicht unter 0
            Pruefe.Gleich(6.0, o.Talent(Talent.Kampf));    // nicht über 6
            Pruefe.Ungefaehr(1 + 2.5 * buch.TalentDaten.Wirkung.Verhandeln, buch.VerkaufsFaktor(k), 1e-12); // die Wirkung im Spiel
            Pruefe.Gleich(-0.5, o.Zugwirkung(Talent.Handwerk));
            Pruefe.Wahr(w.Gesundheit.Erkranken(k, o, "Husten"), "krank"); // Rhetorik −1
            o.TalentSetzen(Talent.Rhetorik, 3);
            Pruefe.Gleich(1.5, o.Talent(Talent.Rhetorik));
            Pruefe.Gleich(2.0, o.TalentOhneZuege(Talent.Rhetorik));
            Pruefe.Wahr(o.Zuege.Select(z => z.Wirkungstext).All(t => t.Contains("Als KI-Haus:")), "Wirkungstext nennt das KI-Verhalten");
            Pruefe.Gleich("Verhandeln +½, Rhetorik −½. Haushalt −10 %. als Ratsherr: Geschenke wirken +30 %. Als KI-Haus: verleiht Geld zu Wucherzinsen und spart an Geschenken für den Rat.",
                buch.Zug("Gierig")!.Wirkungstext);

            var ohne = NeueSimulation(1400, OhneZuege.Value).Wirtschaft!;
            var (_, f2) = Haus(ohne, "Haus Ohne");
            Pruefe.Gleich(0, f2.Oberhaupt.Zuege.Count);
            Pruefe.Wahr(TalentTexte.Alle.All(t => f2.Oberhaupt.Talent(t) == f2.Oberhaupt.Anlage(t)), "ohne Züge: Talent = Anlage");
        }),

        ("Charakterzüge: Haushalt nach Zügen (Bescheiden 90 %, Hochmütig 110 %); Kränklich erkrankt doppelt so oft", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var (_, f) = Haus(w, "Haus Probe");
            var buch = w.Familien;
            var o = f.Oberhaupt;
            o.ZuegeLeeren();
            var grund = buch.Haushaltskosten(f);
            o.ZugHinzufuegen(buch.Zug("Bescheiden")!);
            Pruefe.Ungefaehr(grund * 0.9, buch.Haushaltskosten(f), 1e-9);
            o.ZuegeLeeren();
            o.ZugHinzufuegen(buch.Zug("Hochmuetig")!);
            Pruefe.Ungefaehr(grund * 1.1, buch.Haushaltskosten(f), 1e-9);
            o.ZugHinzufuegen(buch.Zug("Kraenklich")!);
            Pruefe.Ungefaehr(2, buch.KrankheitFaktor(o), 1e-12);
        }),

        ("Charakterzüge: Ein Kind hat bis zur Volljährigkeit nur angeborene Züge, dann drei aus Erziehung und Eltern; Universität macht gelehrt", () =>
        {
            var sim = NeueSimulation();
            var w = sim.Wirtschaft!;
            var buch = w.Familien;
            var (k, f) = Haus(w, "Haus Probe");
            var volljaehrig = buch.VolkDaten(Volk.Mensch).Volljaehrig;
            var klein = buch.KindAufnehmen(k, 3);
            Pruefe.Gleich(0, Persoenlich(klein));
            var student = buch.KindAufnehmen(k, volljaehrig - 1, Geschlecht.Mann);
            student.Ausbildung = "Universität";
            Pruefe.Gleich(0, Persoenlich(student));
            var erhalten = new List<(Person, Charakterzug)>();
            buch.ZugErhalten += (p, z) => erhalten.Add((p, z));
            JahrBeenden(sim);
            Pruefe.Gleich(3, Persoenlich(student));
            KeineGegensaetze(student.Zuege, student.Name);
            Pruefe.Wahr(student.HatZug("Gelehrt"), "Universität → Gelehrt");
            Pruefe.Wahr(erhalten.Contains((student, buch.Zug("Gelehrt")!)), "Ereignis ZugErhalten");
            Pruefe.Wahr(buch.LetztesJahr.Any(e => e.Text.Contains($"{student.Vorname} {f.Nachname} ist erwachsen geworden:")), "Meldung zur Volljährigkeit");
            Pruefe.Wahr(buch.LetztesJahr.Any(e => e.Text.Contains("Neuer Charakterzug") && e.Text.Contains("Gelehrt")), "Meldung zum neuen Zug");
            Pruefe.Gleich(0, Persoenlich(klein));
            // Ein Jahr später bekommt der Student keine neuen Persönlichkeitszüge
            var vorher = string.Join(",", student.Zuege.Where(z => z.Art == Zugart.Persoenlichkeit));
            JahrBeenden(sim);
            Pruefe.Gleich(vorher, string.Join(",", student.Zuege.Where(z => z.Art == Zugart.Persoenlichkeit)));
        }),

        ("Charakterzüge: Erziehung und Eltern färben die Persönlichkeit (Klosterschule → Fromm, fromme Eltern → Fromm), Gegensätze nie zusammen", () =>
        {
            var d = Daten.Value.Charakterzuege!;
            var fromm = d.Zug("Fromm")!;
            double Anteil(string? erziehung, int frommeEltern)
            {
                var eltern = Enumerable.Repeat(fromm, frommeEltern).ToList();
                var treffer = 0;
                for (var i = 0; i < 4000; i++)
                {
                    var zuege = Zugwuerfel.Persoenlichkeit(d, new Zufallsquelle(i), Volk.Zwerg, erziehung, eltern, Array.Empty<Charakterzug>());
                    Pruefe.Gleich(3, zuege.Count);
                    KeineGegensaetze(zuege, $"Wurf {i}");
                    if (zuege.Contains(fromm)) treffer++;
                }
                return treffer / 4000.0;
            }
            var ohne = Anteil(null, 0);
            Pruefe.Wahr(ohne is > 0.12 and < 0.26, $"ohne Erziehung {ohne:P0} (etwa 3/16)");
            Pruefe.Wahr(Anteil("Klosterschule", 0) > ohne * 1.6, "Klosterschule");
            Pruefe.Wahr(Anteil(null, 2) > ohne * 1.6, "zwei fromme Eltern");
        }),

        ("Charakterzüge: angeborene sind selten und werden vererbt – zwei kluge Eltern: etwa 44 %, ohne: etwa 4 %", () =>
        {
            var d = Daten.Value.Charakterzuege!;
            var klug = d.Zug("Klug")!;
            double Anteil(int klugeEltern)
            {
                var eltern = Enumerable.Repeat(klug, klugeEltern).ToList();
                var treffer = 0;
                for (var i = 0; i < 5000; i++)
                {
                    var zuege = Zugwuerfel.Angeboren(d, new Zufallsquelle(i), eltern);
                    Pruefe.Wahr(!(zuege.Any(z => z.Kennung == "Stark") && zuege.Any(z => z.Kennung == "Kraenklich")), "Stark und Kränklich zugleich");
                    if (zuege.Contains(klug)) treffer++;
                }
                return treffer / 5000.0;
            }
            Pruefe.Ungefaehr(0.04, Anteil(0), 0.015);
            Pruefe.Ungefaehr(0.44, Anteil(2), 0.03);
        }),

        ("Charakterzüge: Anlässe – nach überstandener Vergiftung oft eine Narbe; ein Zyniker pilgert nie", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var buch = w.Familien;
            var narben = 0;
            for (var i = 0; i < 400; i++)
            {
                var (k, f) = Haus(w, $"Haus Probe {i}");
                var o = f.Oberhaupt;
                o.ZuegeLeeren();
                buch.ZugAnlass(k, o, "ueberstanden:Vergiftung");
                if (o.HatZug("Narbe")) narben++;
                o.ZuegeLeeren();
                o.ZugHinzufuegen(buch.Zug("Zynisch")!);
                for (var j = 0; j < 20; j++) buch.ZugAnlass(k, o, "trauer");
                Pruefe.Wahr(!o.HatZug("Pilger"), "Zyniker als Pilger");
            }
            Pruefe.Wahr(narben is > 160 and < 240, $"{narben} Narben von 400 (Chance 50 %)");
        }),

        ("Charakterzüge: Verwandtschaft fürs Charakterfenster – Eltern, Ehepartner (auch verwitwet), Kinder", () =>
        {
            var sim = NeueSimulation(5);
            var w = sim.Wirtschaft!;
            var buch = w.Familien;
            var (k, f) = Haus(w, "Haus Probe");
            var kandidat = buch.Kandidaten.First(c => buch.WerbenVerboten(k, c) is null);
            buch.ZuneigungSchenken(k, kandidat.Person.Nummer, 1000);
            Pruefe.Wahr(w.Ausfuehren(k, new Heiraten(kandidat.Person.Nummer)).Erfolg, "Heirat");
            var kind = buch.KindAufnehmen(k, 2, Geschlecht.Frau);
            var v = buch.Verwandte(f.Oberhaupt);
            Pruefe.Gleich(kandidat.Person, v.Ehepartner);
            Pruefe.Wahr(v.Kinder.Contains(kind), "Kind");
            Pruefe.Gleich<Person?>(null, v.Vater);
            var vk = buch.Verwandte(kind);
            Pruefe.Gleich(f.Oberhaupt, vk.Vater);
            Pruefe.Gleich(f.Oberhaupt, buch.Verwandte(kandidat.Person).Ehepartner);
            Pruefe.Gleich(kind, buch.PersonNummer(kind.Nummer));
        }),

        ("Charakterzüge: KI-Neigung – Faktoren multipliziert, Zuschläge addiert; ohne Familie neutral", () =>
        {
            var w = NeueSimulation().Wirtschaft!;
            var buch = w.Familien;
            var n = ZugNeigung.Aus(new[] { buch.Zug("Gierig")!, buch.Zug("Gesellig")!, buch.Zug("Verschlagen")! });
            Pruefe.Ungefaehr(0.7 * 1.2, n.Geschenke, 1e-12);
            Pruefe.Ungefaehr(0.04, n.Zins, 1e-12);
            Pruefe.Gleich(1, n.Gaunerstuecke);
            Pruefe.Gleich(1, n.Geselligkeit);
            Pruefe.Gleich(ZugNeigung.Neutral, buch.KiNeigung(w.KontoEroeffnen("Ohne Familie")));
            var (k, f) = Haus(w, "Haus Probe");
            f.Oberhaupt.ZuegeLeeren();
            f.Oberhaupt.ZugHinzufuegen(buch.Zug("Fromm")!);
            Pruefe.Wahr(buch.KiNeigung(k).GlaubensTreu, "Fromm bleibt beim Glauben");
            Pruefe.Ungefaehr(0.5, buch.KiNeigung(k).Ablass, 1e-12);
        }),

        ("Charakterzüge: KI – ein gieriges Haus verleiht teurer, ein großzügiges billiger", () =>
        {
            double Zins(string zug)
            {
                var sim = NeueSimulation();
                var w = sim.Wirtschaft!;
                var k = w.KontoEroeffnen("Familie Fugger", startvermoegen: 20_000);
                var b = w.BetriebEroeffnen($"{w.Daten.Art(Betriebsart.Wechselstube).Name} Fugger", Betriebsart.Wechselstube, k, startkarren: "");
                w.Einstellen(b, "Geselle", b.Plaetze);
                var o = w.Familien.Gruenden(k, Volk.Mensch, Geschlecht.Mann).Oberhaupt;
                o.ZuegeLeeren();
                o.ZugHinzufuegen(w.Familien.Zug(zug)!);
                w.KiAnmelden(k, KiProfil.Standard);
                for (var i = 0; i < 12 * 60 - Reichsgilde.Core.Zeit.Spielzeit.TagesbeginnMinute; i++) sim.Tick(); // über die Politikstunde
                return w.Bank.Zinssatz(k);
            }
            Pruefe.Ungefaehr(0.14 + 0.04, Zins("Gierig"), 1e-9);
            Pruefe.Ungefaehr(0.14 - 0.03, Zins("Grosszuegig"), 1e-9);
            Pruefe.Ungefaehr(0.14, Zins("Fleissig"), 1e-9);
        }),

        ("Charakterzüge: Speichern und Laden behält alle Züge und den Würfel der erworbenen", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 11 },
                Daten.Value, new NeuePartie());
            for (var i = 0; i < 3; i++) JahrBeenden(sim);
            var zeitpunkt = new DateTime(2026, 10, 28, 12, 0, 0);
            var json = Speicherung.Speichern(sim, spieler, zeitpunkt);
            Pruefe.Wahr(json.Contains("\"Zuege\": ["), "Züge im Spielstand");
            var (geladen, geladenerSpieler) = Speicherung.Laden(json);
            Pruefe.Gleich(json, Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt));
            for (var i = 0; i < 4; i++) { JahrBeenden(sim); JahrBeenden(geladen); }
            Pruefe.Gleich(Speicherung.Speichern(sim, spieler, zeitpunkt), Speicherung.Speichern(geladen, geladenerSpieler, zeitpunkt));
        }),

        ("Charakterzüge: Spielstand ohne Züge – beim Laden nachgewürfelt, genau wie in einer neuen Partie mit demselben Startwert", () =>
        {
            var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 12 },
                Daten.Value, new NeuePartie());
            var w = sim.Wirtschaft!;
            var original = w.Familien.Kandidaten.Select(k => k.Person).Concat(w.Konten.SelectMany(k => w.Familien.Von(k)!.Mitglieder))
                .ToDictionary(p => p.Nummer, p => string.Join(",", p.Zuege));
            // Ein volljähriges Kind mit Universität (seine Persönlichkeit hängt jetzt am Abschluss – sie wird darum neu gezogen)
            var kind = w.Familien.KindAufnehmen(spieler, 30, Geschlecht.Mann);
            kind.Abschluss = "Universität";
            var knoten = JsonNode.Parse(Speicherung.Speichern(sim, spieler))!;
            var personen = knoten["Wirtschaft"]!["Familien"]!["Personen"]!.AsArray();
            foreach (var p in personen) p!.AsObject().Remove("Zuege");
            knoten["Wirtschaft"]!["Familien"]!.AsObject().Remove("ZugWuerfe");
            var (geladen, _) = Speicherung.Laden(knoten.ToJsonString());
            var buch = geladen.Wirtschaft!.Familien;
            foreach (var (nummer, zuege) in original)
                Pruefe.Gleich(zuege, string.Join(",", buch.PersonNummer(nummer)!.Zuege), $"Person {nummer}");
            Pruefe.Wahr(buch.PersonNummer(kind.Nummer)!.HatZug("Gelehrt"), "Absolvent der Universität ist gelehrt");
            Pruefe.Gleich(3, Persoenlich(buch.PersonNummer(kind.Nummer)!));
        }),

        ("Charakterzüge: eigene Würfel – Namen, Talente und Aussehen der Personen sind dieselben wie ohne Züge", () =>
        {
            for (var s = 0; s < 5; s++)
            {
                string Bild(Wirtschaftsdaten daten)
                {
                    var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = s },
                        daten, new NeuePartie());
                    var buch = sim.Wirtschaft!.Familien;
                    return string.Join(";", sim.Wirtschaft.Konten.Select(k => buch.Von(k)!.Oberhaupt).Concat(buch.Kandidaten.Select(k => k.Person))
                        .Select(p => $"{p.Name}/{p.Alter(buch.Jahr)}/{string.Join(",", TalentTexte.Alle.Select(p.Anlage))}/{p.Aussehen}/{p.Glaube}"));
                }
                Pruefe.Gleich(Bild(OhneZuege.Value), Bild(Daten.Value), $"Startwert {s}");
            }
        }),
    };
}
