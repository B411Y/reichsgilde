using Reichsgilde;
using Reichsgilde.Core;
using Reichsgilde.Core.Glaube;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;
using M = Reichsgilde.MusikModell;

/// <summary>
/// Tests der Auswahlregeln der Musik aus Runde 2 (Nutzer, 11.11.2026): Völker nach dem Volk des Spielers (etwa jedes dritte Stück, fremde nie), Glaube
/// (Kirchenraum und Messe das Stück des Kirchenglaubens, im Tageswechsel selten; Ahtnisch stumm), Beruf (Handwerk, Dienstleistung; öfter in der eigenen
/// Werkstatt), die neuen Ereignisse (Einbruch, Geburt, Gefahr, Aufstieg) und das Menüstück. Jede Regel hat eine Gegenprobe (das andere Volk, der andere
/// Glaube, die andere Berufsgruppe spielt nie). Die Rechnung ist reine Funktion (<see cref="M.TagWaehlen"/>, <see cref="M.LageVon"/>) und läuft mit festem Zufall.
/// </summary>
static class MusikRegelnTests
{
    /// <summary>Würfelt <paramref name="n"/> Stücke des Tageswechsels nacheinander (mit dem Gedächtnis der letzten sechs wie die Steuerung).</summary>
    static List<M.Stueck> Wuerfeln(M.Eingabe e, int n = 6000, int saat = 1)
    {
        var (s, _) = MusikTests.Liste();
        var r = new Random(saat);
        var zuletzt = new List<string>();
        var aus = new List<M.Stueck>();
        for (var i = 0; i < n; i++)
        {
            var w = M.TagWaehlen(s, e, zuletzt, r.NextDouble)!;
            aus.Add(w);
            zuletzt.Add(w.Datei);
            if (zuletzt.Count > 6) zuletzt.RemoveAt(0);
        }
        return aus;
    }

    static M.Eingabe Tag(Volk? volk = Volk.Mensch, M.Berufsgruppe beruf = M.Berufsgruppe.Keine, string? glaube = null, M.Ort ort = M.Ort.Draussen, Jahreszeit jz = Jahreszeit.Sommer) =>
        new(12 * 60, jz, volk, M.Haeufigkeit.Immer, M.Anlass.Keiner, beruf, glaube, ort);

    static double Anteil(List<M.Stueck> l, Func<M.Stueck, bool> f) => l.Count(f) / (double)l.Count;

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Musik Völker: das Volk des Spielers spielt etwa jedes dritte Stück, die Stücke fremder Völker nie (je Volk)", () =>
        {
            var namen = new (Volk Volk, string Name)[] { (Volk.Mensch, "Menschen"), (Volk.Elb, "Elben"), (Volk.Zwerg, "Zwerge"), (Volk.Halbling, "Halblinge") };
            foreach (var (volk, name) in namen)
            {
                var g = Wuerfeln(Tag(volk));
                var anteil = Anteil(g, x => x.Volk == name);
                Pruefe.Wahr(anteil is > 0.28 and < 0.38, $"{name}: Anteil {anteil:0.000}");
                Pruefe.Wahr(g.All(x => x.Volk is null || x.Volk == name), $"{name}: fremdes Volk gespielt");
                foreach (var fremd in namen.Where(n => n.Name != name)) Pruefe.Gleich(0, g.Count(x => x.Volk == fremd.Name));
                Pruefe.Wahr(g.Where(x => x.Volk == name).Select(x => x.Datei).Distinct().Count() == 2, $"{name}: beide Stücke kommen vor");
            }
        }),

        ("Musik Völker (Gegenprobe): Orks, Oger und kein Volk bekommen gar kein Völkerstück", () =>
        {
            foreach (var volk in new Volk?[] { Volk.Ork, Volk.Oger, null })
                Pruefe.Gleich(0, Wuerfeln(Tag(volk), 3000).Count(x => x.Volk is not null));
        }),

        ("Musik Völker: fester Zufall – 0,0 trifft die Volksgruppe, 0,99 nicht (die Rechnung ist rein)", () =>
        {
            var (s, _) = MusikTests.Liste();
            var anfang = M.TagWaehlen(s, Tag(Volk.Zwerg), new List<string>(), () => 0.0)!;
            Pruefe.Gleich("Zwerge", anfang.Volk);
            var ende = M.TagWaehlen(s, Tag(Volk.Zwerg), new List<string>(), () => 0.99)!;
            Pruefe.Wahr(ende.Volk is null, "0,99 gehört den neutralen Stücken");
            // dieselben Eingaben, derselbe Zufall: dasselbe Stück
            Pruefe.Gleich(anfang.Datei, M.TagWaehlen(s, Tag(Volk.Zwerg), new List<string>(), () => 0.0)!.Datei);
        }),

        ("Musik Beruf: die Gruppe des Spielers (Handwerk oder Dienstleistung) spielt öfter, die andere nie; ohne Beruf keine", () =>
        {
            var hand = Wuerfeln(Tag(beruf: M.Berufsgruppe.Handwerk));
            var anteilHand = Anteil(hand, x => x.Beruf == "Handwerk");
            Pruefe.Wahr(anteilHand is > 0.10 and < 0.20, $"Handwerk: Anteil {anteilHand:0.000}");
            Pruefe.Gleich(0, hand.Count(x => x.Beruf == "Dienstleistung"));
            var dienst = Wuerfeln(Tag(beruf: M.Berufsgruppe.Dienstleistung));
            Pruefe.Wahr(Anteil(dienst, x => x.Beruf == "Dienstleistung") is > 0.10 and < 0.20, "Dienstleistung");
            Pruefe.Gleich(0, dienst.Count(x => x.Beruf == "Handwerk"));
            Pruefe.Gleich(0, Wuerfeln(Tag(beruf: M.Berufsgruppe.Keine), 3000).Count(x => x.Beruf is not null));
            var beide = Wuerfeln(Tag(beruf: M.Berufsgruppe.Handwerk | M.Berufsgruppe.Dienstleistung));
            Pruefe.Wahr(beide.Any(x => x.Beruf == "Handwerk") && beide.Any(x => x.Beruf == "Dienstleistung"), "beide Gruppen");
        }),

        ("Musik Beruf: in der eigenen Werkstatt spielt das Berufsstück deutlich öfter als im Tageswechsel draußen", () =>
        {
            var draussen = Anteil(Wuerfeln(Tag(beruf: M.Berufsgruppe.Handwerk)), x => x.Beruf == "Handwerk");
            var werkstatt = Anteil(Wuerfeln(Tag(beruf: M.Berufsgruppe.Handwerk, ort: M.Ort.EigeneWerkstatt)), x => x.Beruf == "Handwerk");
            Pruefe.Wahr(werkstatt > draussen * 1.7, $"Werkstatt {werkstatt:0.000} gegen draußen {draussen:0.000}");
            // das eine Berufsstück kann nicht zweimal hintereinander kommen: der Anteil bleibt unter dem Gewicht (0,45)
            Pruefe.Wahr(werkstatt is > 0.25 and < 0.45, $"Werkstatt {werkstatt:0.000}");
            // auch in der Werkstatt nie die fremde Gruppe
            Pruefe.Gleich(0, Wuerfeln(Tag(beruf: M.Berufsgruppe.Handwerk, ort: M.Ort.EigeneWerkstatt), 3000).Count(x => x.Beruf == "Dienstleistung"));
        }),

        ("Musik Beruf: die zehn Berufe gehören zu Handwerk oder Dienstleistung, wie vom Nutzer genannt", () =>
        {
            foreach (var b in new[] { "Schmied", "Tischler", "Steinmetz", "Alchimist", "Parfumeur" }) Pruefe.Gleich(M.Berufsgruppe.Handwerk, M.GruppeVon(b), b);
            foreach (var b in new[] { "Wirt", "Prediger", "Geldleiher", "Gardist", "Dieb" }) Pruefe.Gleich(M.Berufsgruppe.Dienstleistung, M.GruppeVon(b), b);
            Pruefe.Gleich(M.Berufsgruppe.Keine, M.GruppeVon("Bauer"));
            Pruefe.Gleich(M.Berufsgruppe.Keine, M.GruppeVon((string?)null));
            Pruefe.Gleich(M.Berufsgruppe.Handwerk | M.Berufsgruppe.Dienstleistung, M.GruppeVon(new[] { "Schmied", "Wirt" }));
            // jeder Beruf des Kerns ist zugeordnet (damit kein neuer Beruf still ohne Musik bleibt)
            var kern = Reichsgilde.Core.Wirtschaft.Wirtschaftsdaten.AusDatei(MusikTests.Spielordner("daten", "wirtschaft.json"));
            foreach (var name in kern.Berufe.Keys) Pruefe.Wahr(M.GruppeVon(name) != M.Berufsgruppe.Keine, "Beruf ohne Gruppe: " + name);
        }),

        ("Musik Glaube: im Tageswechsel selten das Stück des eigenen Glaubens, nie das des anderen; Ahtnisch und Naturglaube stumm", () =>
        {
            var kath = Wuerfeln(Tag(glaube: "Katholisch"));
            var anteil = Anteil(kath, x => x.Lage == "glaube");
            Pruefe.Wahr(anteil is > 0.02 and < 0.10, $"Katholisch: Anteil {anteil:0.000}");
            Pruefe.Wahr(kath.Where(x => x.Lage == "glaube").All(x => x.Glaube == "Katholisch"), "nur katholisch");
            var ev = Wuerfeln(Tag(glaube: "Evangelisch"));
            Pruefe.Wahr(ev.Any(x => x.Lage == "glaube") && ev.Where(x => x.Lage == "glaube").All(x => x.Glaube == "Evangelisch"), "nur evangelisch");
            // Gegenprobe: kein Glaube (Orks, Oger, Ahtnisch) – kein Glaubensstück
            Pruefe.Gleich(0, Wuerfeln(Tag(glaube: M.GlaubeName(Konfession.Naturglaube)), 3000).Count(x => x.Lage == "glaube"));
            Pruefe.Gleich(null, M.GlaubeName(Konfession.Naturglaube));
            Pruefe.Gleich(null, M.GlaubeName(null));
            Pruefe.Gleich("Katholisch", M.GlaubeName(Konfession.Katholisch));
            Pruefe.Gleich("Evangelisch", M.GlaubeName(Konfession.Evangelisch));
            // Ahtnisch ist da (beide Fassungen), spielt aber nicht, solange der Kern es nicht kennt
            var (s, _) = MusikTests.Liste();
            Pruefe.Gleich(2, M.GlaubensKandidaten(s, "Ahtnisch").Count);
            foreach (var k in Enum.GetValues<Konfession>()) Pruefe.Wahr(M.GlaubeName(k) != "Ahtnisch", "Ahtnisch darf erst mit dem Ahtnentum im Kern kommen");
        }),

        ("Musik Glaube: Kirchenraum und Messe – das Stück des Kirchenglaubens, nachts und tags; ohne Stück des Glaubens die normale Lage", () =>
        {
            var (s, _) = MusikTests.Liste();
            M.Eingabe E(double uhr, M.Ort ort = M.Ort.Draussen, string? glaube = null, string? kirche = null, bool messe = false) =>
                new(uhr, Jahreszeit.Herbst, Volk.Mensch, M.Haeufigkeit.Immer, M.Anlass.Keiner, M.Berufsgruppe.Keine, glaube, ort, kirche, messe);
            // Kirchenraum: Glaube der Kirche, nicht der des Spielers
            Pruefe.Gleich("kirche", M.LageVon(s, E(12 * 60, M.Ort.Kirche, "Katholisch", "Evangelisch")));
            var r = new Random(3);
            for (var i = 0; i < 100; i++)
            {
                var e = E(12 * 60, M.Ort.Kirche, "Katholisch", "Evangelisch");
                Pruefe.Gleich("Evangelisch", M.GrundWaehlen(s, "kirche", e, new List<string>(), r.NextDouble)!.Glaube);
                var k = E(12 * 60, M.Ort.Kirche, "Evangelisch", "Katholisch");
                Pruefe.Gleich("Katholisch", M.GrundWaehlen(s, "kirche", k, new List<string>(), r.NextDouble)!.Glaube);
            }
            // Kirche ohne eigenen Glauben (Dom ohne Stadtglaube): der Glaube des Spielers
            Pruefe.Gleich("Katholisch", M.GrundWaehlen(s, "kirche", E(12 * 60, M.Ort.Kirche, "Katholisch"), new List<string>(), () => 0.5)!.Glaube);
            // nachts im Kirchenraum ebenfalls die Kirche, nicht die Nacht
            Pruefe.Gleich("kirche", M.LageVon(s, E(22 * 60, M.Ort.Kirche, "Katholisch", "Katholisch")));
            // Messe draußen: Glaube des Spielers; ohne Glauben (Ork, Oger, Ahtnisch) bleibt es beim Tag
            Pruefe.Gleich("kirche", M.LageVon(s, E(10 * 60, glaube: "Evangelisch", messe: true)));
            Pruefe.Gleich("Evangelisch", M.GrundWaehlen(s, "kirche", E(10 * 60, glaube: "Evangelisch", messe: true), new List<string>(), () => 0.1)!.Glaube);
            Pruefe.Gleich("tag", M.LageVon(s, E(10 * 60, glaube: null, messe: true)));
            // Gegenproben: draußen ohne Messe Tag, Nacht Nacht, ohne Glauben im Kirchenraum Tag
            Pruefe.Gleich("tag", M.LageVon(s, E(14 * 60, glaube: "Katholisch")));
            Pruefe.Gleich("nacht", M.LageVon(s, E(22 * 60, glaube: "Katholisch")));
            Pruefe.Gleich("tag", M.LageVon(s, E(12 * 60, M.Ort.Kirche)));
            // Leere Liste: nie Kirche
            Pruefe.Gleich("tag", M.LageVon(new List<M.Stueck>(), E(12 * 60, M.Ort.Kirche, "Katholisch", "Katholisch")));
        }),

        ("Musik Messe: die Spanne ist die des Kerns (30 min vor bis 60 min nach der Messestunde), ohne Kirchen nie", () =>
        {
            Pruefe.Wahr(!M.IstMesse(9 * 60 + 29, 10), "9:29");
            Pruefe.Wahr(M.IstMesse(9 * 60 + 30, 10), "9:30");
            Pruefe.Wahr(M.IstMesse(10 * 60 + 59, 10), "10:59");
            Pruefe.Wahr(!M.IstMesse(11 * 60, 10), "11:00");
            Pruefe.Wahr(!M.IstMesse(10 * 60, -1), "ohne Kirchen");
        }),

        ("Musik Kirchenraum: der Wechsel in die Kirche blendet 4–8 s zum Stück des Glaubens über, zurück ebenso; in der Pause endet sie nach höchstens 6 s", () =>
        {
            var (s, _) = MusikTests.Liste();
            var l = new MusikTests.Lauf(s, 21) { Glaube = "Katholisch", Volk = Volk.Mensch };
            l.Schritte(30);
            Pruefe.Wahr(l.Steuerung.Laufend is { Lage: "tag" }, "Tag läuft");
            var n = l.Befehle.Count;
            l.Ort = M.Ort.Kirche; l.KirchenGlaube = "Evangelisch";
            l.Schritte(1);
            var b = l.Befehle.Skip(n).First().Befehl;
            Pruefe.Wahr(b.Art == M.BefehlArt.Starten && b.Wechsel && b.Stueck!.Glaube == "Evangelisch", "Stück der Kirche");
            Pruefe.Wahr(b.Einblende is >= 4 and <= 8 && b.Einblende == b.AltAus, $"Blende {b.Einblende:0.0}");
            Pruefe.Gleich("kirche", l.Steuerung.LaufendeLage);
            n = l.Befehle.Count;
            l.Ort = M.Ort.Draussen; l.KirchenGlaube = null;
            l.Schritte(1);
            var zurueck = l.Befehle.Skip(n).First().Befehl;
            Pruefe.Wahr(zurueck.Wechsel && zurueck.Stueck!.Lage == "tag", "zurück zum Tag");
            // Pause
            var p = new MusikTests.Lauf(s, 22) { Haeufigkeit = M.Haeufigkeit.Selten, Glaube = "Katholisch", Volk = Volk.Mensch };
            p.Schritte(10);
            while (p.Steuerung.Laufend is not null) p.Schritte(5);
            Pruefe.Wahr(p.Steuerung.PauseRest > 30, "lange Pause");
            p.Ort = M.Ort.Kirche; p.KirchenGlaube = "Katholisch";
            p.Schritte(M.Steuerung.KirchePause + 2);
            Pruefe.Wahr(p.Steuerung.Laufend is { Lage: "glaube" }, "in der Kirche beginnt das Stück nach höchstens 6 s");
        }),

        ("Musik Ahtnisch stumm: über sechs Stunden mit Naturglauben (kein Name) und in jeder Lage kein Glaubensstück; Gegenprobe Katholisch spielt eines", () =>
        {
            var (s, _) = MusikTests.Liste();
            var stumm = new MusikTests.Lauf(s, 23) { Glaube = M.GlaubeName(Konfession.Naturglaube), Volk = Volk.Ork };
            stumm.Schritte(3 * 3600);
            stumm.Ort = M.Ort.Kirche;
            stumm.Schritte(600);
            stumm.Ort = M.Ort.Draussen; stumm.Messe = true;
            stumm.Schritte(600);
            Pruefe.Wahr(stumm.Gespielt.Count > 20, "es spielt sonst Musik");
            Pruefe.Gleich(0, stumm.Gespielt.Count(x => x.Stueck.Lage == "glaube"));
            var kath = new MusikTests.Lauf(s, 23) { Glaube = "Katholisch", Volk = Volk.Mensch };
            kath.Schritte(6 * 3600);
            Pruefe.Wahr(kath.Gespielt.Any(x => x.Stueck.Lage == "glaube" && x.Stueck.Glaube == "Katholisch"), "Katholisch kommt vor");
            Pruefe.Gleich(0, kath.Gespielt.Count(x => x.Stueck.Lage == "glaube" && x.Stueck.Glaube != "Katholisch"));
            // Anteil über die Stunden bleibt klein
            Pruefe.Wahr(kath.Gespielt.Count(x => x.Stueck.Lage == "glaube") < kath.Gespielt.Count / 5, "selten");
        }),

        ("Musik Ereignisse: Einbruch, Geburt, Gefahr und Aufstieg haben je ihre Stücke (Aufstieg zwei Fassungen) und spielen mit ihrem Rang", () =>
        {
            var (s, _) = MusikTests.Liste();
            foreach (var (a, zusatz, anzahl) in new[] { (M.Anlass.Einbruch, "dieb", 1), (M.Anlass.Geburt, "geburt", 1), (M.Anlass.Gefahr, "gefahr", 1), (M.Anlass.Aufstieg, "levelup", 2), (M.Anlass.Kampf, "kampf", 1) })
            {
                Pruefe.Gleich(anzahl, M.AnlassKandidaten(s, a).Count, a.ToString());
                Pruefe.Gleich(zusatz, M.AnlassName(a));
                var l = new MusikTests.Lauf(s, 31);
                l.Schritte(20);
                l.Steuerung.Melden(a);
                l.Schritte(1);
                Pruefe.Gleich(a, l.Steuerung.LaufenderAnlass);
                Pruefe.Gleich(zusatz, l.Steuerung.Laufend!.Zusatz);
                Pruefe.Wahr(!M.IstZustand(a), "Augenblick: " + a);
            }
            var ordnung = new[] { M.Anlass.Tod, M.Anlass.Kampf, M.Anlass.Gefahr, M.Anlass.Einbruch, M.Anlass.Aufstand, M.Anlass.Hochzeit, M.Anlass.Geburt, M.Anlass.Aufstieg, M.Anlass.Sklaven };
            for (var i = 1; i < ordnung.Length; i++) Pruefe.Wahr(M.Rang(ordnung[i - 1]) < M.Rang(ordnung[i]), $"{ordnung[i - 1]} vor {ordnung[i]}");
            // Gefahr geht vor Geburt und unterbricht ein laufendes Geburtsstück nicht umgekehrt
            var g = new MusikTests.Lauf(s, 32);
            g.Schritte(20);
            g.Steuerung.Melden(M.Anlass.Geburt);
            g.Schritte(1);
            g.Steuerung.Melden(M.Anlass.Gefahr);
            g.Schritte(1);
            Pruefe.Gleich(M.Anlass.Gefahr, g.Steuerung.LaufenderAnlass);
            g.Steuerung.Melden(M.Anlass.Geburt);
            g.Schritte(5);
            Pruefe.Gleich(M.Anlass.Gefahr, g.Steuerung.LaufenderAnlass);
            // Aufstieg unterbricht das Stück der Sklaven (Zustand), beide Fassungen kommen vor
            var sk = new MusikTests.Lauf(s, 33);
            sk.Schritte(20);
            sk.Zustand = M.Anlass.Sklaven;
            sk.Schritte(25);
            Pruefe.Gleich(M.Anlass.Sklaven, sk.Steuerung.LaufenderAnlass);
            sk.Steuerung.Melden(M.Anlass.Aufstieg);
            sk.Schritte(1);
            Pruefe.Gleich(M.Anlass.Aufstieg, sk.Steuerung.LaufenderAnlass);
            var fassungen = new HashSet<string>();
            for (var i = 0; i < 40; i++)
            {
                var r = new Random(i);
                fassungen.Add(M.Waehlen(M.AnlassKandidaten(s, M.Anlass.Aufstieg), new List<string>(), r.NextDouble)!.Datei);
            }
            Pruefe.Gleich(2, fassungen.Count);
        }),

        ("Musik Ereignisse: Meisterbrief, Aufstieg im Stand, gewonnene Wahl = Aufstieg; Heirat, Geburt, Einbruch, Brand, Überfall; verlorene Wahl und Krankheit nichts", () =>
        {
            Pruefe.Gleich(M.Anlass.Aufstieg, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Meisterpruefung));
            Pruefe.Gleich(M.Anlass.Aufstieg, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.StandAufstieg));
            Pruefe.Gleich(M.Anlass.Aufstieg, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.WahlGewonnen));
            Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.WahlVerloren));
            Pruefe.Gleich(M.Anlass.Keiner, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Ratssitzung));
            Pruefe.Gleich(M.Anlass.Hochzeit, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Heirat));
            Pruefe.Gleich(M.Anlass.Geburt, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Geburt));
            Pruefe.Gleich(M.Anlass.Einbruch, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Diebstahl));
            Pruefe.Gleich(M.Anlass.Gefahr, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Feuer));
            Pruefe.Gleich(M.Anlass.Gefahr, M.AnlassAusAnsage(KlangAnsagenErkennung.Id.Ueberfall));
        }),

        ("Musik Menü und Daten: Reichsgilde ist das Menüstück (nicht im Tageswechsel), Adels Schmeich und Arbeitruf spielen im Tag; Dateinamen ASCII; kein Dämon", () =>
        {
            var (s, menue) = MusikTests.Liste();
            Pruefe.Gleich("menue/menue_reichsgilde.ogg", menue);
            var m = s.First(x => x.Datei == menue);
            Pruefe.Gleich("menue", m.Lage);
            Pruefe.Wahr(!M.TagKandidaten(s, Jahreszeit.Sommer, Volk.Mensch, M.Berufsgruppe.Handwerk | M.Berufsgruppe.Dienstleistung, "Katholisch").Contains(m), "Menüstück im Tageswechsel");
            var tag = M.TagKandidaten(s, Jahreszeit.Sommer, null);
            Pruefe.Wahr(tag.Any(x => x.Datei == "tag/tag_adels_schmeich.ogg") && tag.Any(x => x.Datei == "tag/tag_arbeitruf.ogg"), "Adels Schmeich, Arbeitruf");
            foreach (var x in s)
            {
                Pruefe.Wahr(x.Datei.All(c => c < 128 && c != ' ' && !char.IsUpper(c)), "Dateiname: " + x.Datei);
                Pruefe.Wahr(!x.Datei.Contains("daemon") && !x.Datei.Contains("gkueck"), "Datei: " + x.Datei);
            }
            Pruefe.Gleich(2, s.Count(x => x.Lage == "event" && x.Zusatz == "levelup"));
            Pruefe.Gleich(2, s.Count(x => x.Beruf is not null)); // Handwerk und Dienstleistung
            Pruefe.Gleich(4, s.Count(x => x.Lage == "glaube")); // Katholisch, Evangelisch, Ahtnisch in zwei Fassungen
        }),

        ("Musik Völker: alle Volksstücke haben den Namen tag_<volk>_<titel>, die vier ersten sind umbenannt und nur noch mit den neuen Namen in der Liste", () =>
        {
            var (s, _) = MusikTests.Liste();
            foreach (var (stamm, volk) in new[] { ("tag/tag_mensch_", "Menschen"), ("tag/tag_elb_", "Elben"), ("tag/tag_zwerg_", "Zwerge"), ("tag/tag_halbling_", "Halblinge") })
            {
                var v = s.Where(x => x.Volk == volk).ToList();
                Pruefe.Gleich(2, v.Count, volk);
                Pruefe.Wahr(v.All(x => x.Datei.StartsWith(stamm)), volk + ": Name");
            }
            foreach (var alt in new[] { "tag_menschen.ogg", "tag_elben.ogg", "tag_zwerge.ogg", "tag_halblinge.ogg" })
                Pruefe.Wahr(s.All(x => !x.Datei.EndsWith("/" + alt)), "alter Name " + alt);
        }),
    };
}
