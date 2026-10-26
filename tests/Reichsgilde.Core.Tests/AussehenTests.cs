using System.IO.Compression;
using System.Text.Json.Nodes;
using Reichsgilde.Core;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;

// ─────────────────────────────────────────────────────────────────────────────
//  Aussehen der Personen für die Porträts (PR #211): Paletten je Volk, Würfeln und Vererben,
//  Altersstufe, Standgruppe und Kleidung, Speichern und alte Spielstände.
// ─────────────────────────────────────────────────────────────────────────────

static class AussehenTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static readonly Volk[] AlleVoelker = Enum.GetValues<Volk>();

    static (Simulation Sim, Konto Spieler) Partie(int startwert, NeuePartie? neu = null) =>
        Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = startwert },
            Daten.Value, neu ?? new NeuePartie());

    static void JahrBeenden(Simulation sim)
    {
        var jahr = sim.Zeit.Jahr;
        while (sim.Zeit.Jahr == jahr) sim.Tick();
    }

    static IEnumerable<Person> AllePersonen(Stadtwirtschaft w) =>
        w.Konten.Select(w.Familien.Von).Where(f => f is not null).SelectMany(f => f!.Mitglieder)
            .Concat(w.Familien.Kandidaten.Select(k => k.Person));

    static Person Elternteil(int nummer, Volk volk, Geschlecht g, Aussehen a) =>
        new(nummer, "Test", "Muster", volk, g, 1380) { Aussehen = a };

    static string AlterStand()
    {
        var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
            "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
        using var gz = new GZipStream(File.OpenRead(datei), CompressionMode.Decompress);
        return new StreamReader(gz).ReadToEnd();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Aussehen: Paletten je Volk – Elben silberblond, hellblond oder schwarz, Orks schwarz oder kahl; gewürfelt wird nur aus der Palette", () =>
        {
            Pruefe.Gleich("Silberblond,Hellblond,Schwarz", string.Join(",", Aussehenspalette.Haarfarben(Volk.Elb)));
            Pruefe.Gleich("Schwarz,Kahl", string.Join(",", Aussehenspalette.Haarfarben(Volk.Ork)));
            Pruefe.Wahr(!Aussehenspalette.Haarfarben(Volk.Mensch).Contains(Haarfarbe.Silberblond), "Menschen ohne Silberblond");
            Pruefe.Wahr(Aussehenspalette.Augenfarben(Volk.Elb).Contains(Augenfarbe.Violett), "Elben mit violetten Augen");
            var z = new Zufallsquelle(5);
            foreach (var volk in AlleVoelker)
            {
                var haar = Aussehenspalette.Haarfarben(volk);
                var augen = Aussehenspalette.Augenfarben(volk);
                Pruefe.Wahr(haar.Count >= 2 && haar.Distinct().Count() == haar.Count, $"{volk}: Haarpalette");
                Pruefe.Wahr(augen.Count >= 3 && augen.Distinct().Count() == augen.Count, $"{volk}: Augenpalette");
                var gewuerfelt = Enumerable.Range(0, 3000).Select(_ => Aussehenspalette.Wuerfeln(z, volk)).ToList();
                Pruefe.Wahr(gewuerfelt.All(a => Aussehenspalette.Verboten(volk, a) is null), $"{volk}: gewürfelt außerhalb der Palette");
                Pruefe.Gleich(Aussehenspalette.Gesichter, gewuerfelt.Select(a => a.Gesicht).Distinct().Count(), $"{volk}: Gesichter");
                Pruefe.Gleich(haar.Count, gewuerfelt.Select(a => a.Haar).Distinct().Count(), $"{volk}: Haarfarben");
                Pruefe.Gleich(augen.Count, gewuerfelt.Select(a => a.Augen).Distinct().Count(), $"{volk}: Augenfarben");
                var abgeleitet = Aussehenspalette.Abgeleitet(17, volk);
                Pruefe.Wahr(Aussehenspalette.Verboten(volk, abgeleitet) is null, $"{volk}: abgeleitetes Aussehen außerhalb der Palette");
                Pruefe.Gleich(abgeleitet, Aussehenspalette.Abgeleitet(17, volk));
            }
            Pruefe.Wahr(Aussehenspalette.Verboten(Volk.Mensch, new Aussehen(5, Haarfarbe.Braun, Augenfarbe.Blau)) is not null, "Gesicht 5");
            Pruefe.Wahr(Aussehenspalette.Verboten(Volk.Mensch, new Aussehen(-1, Haarfarbe.Braun, Augenfarbe.Blau)) is not null, "Gesicht −1");
            Pruefe.Wahr(Aussehenspalette.Verboten(Volk.Mensch, new Aussehen(0, Haarfarbe.Silberblond, Augenfarbe.Blau)) is not null, "Silberblonder Mensch");
            Pruefe.Wahr(Aussehenspalette.Verboten(Volk.Zwerg, new Aussehen(0, Haarfarbe.Rot, Augenfarbe.Violett)) is not null, "Zwerg mit violetten Augen");
            Pruefe.Gleich(null, Aussehenspalette.Verboten(Volk.Elb, new Aussehen(4, Haarfarbe.Silberblond, Augenfarbe.Violett)));
        }),

        ("Aussehen: Kinder erben das Gesicht – vom Elternteil gleichen Geschlechts zu etwa 70 %, Haar und Augen zu etwa 90 % von einem Elternteil", () =>
        {
            var vater = Elternteil(1, Volk.Mensch, Geschlecht.Mann, new Aussehen(1, Haarfarbe.Schwarz, Augenfarbe.Braun));
            var mutter = Elternteil(2, Volk.Mensch, Geschlecht.Frau, new Aussehen(3, Haarfarbe.Blond, Augenfarbe.Blau));
            var z = new Zufallsquelle(11);
            const int n = 20_000;
            foreach (var g in new[] { Geschlecht.Mann, Geschlecht.Frau })
            {
                var kinder = Enumerable.Range(0, n).Select(_ => Aussehenspalette.Vererben(z, Volk.Mensch, g, vater, mutter)).ToList();
                Pruefe.Wahr(kinder.All(k => k.Gesicht is 1 or 3), "Gesicht nicht von den Eltern");
                var gleich = (g == Geschlecht.Mann ? vater : mutter).Aussehen.Gesicht;
                Pruefe.Ungefaehr(0.7, kinder.Count(k => k.Gesicht == gleich) / (double)n, 0.015);
                // Haar: je zur Hälfte von Vater oder Mutter, davon 10 % eine andere Farbe → 45 % schwarz, 45 % blond, 10 % andere
                // (die Abweichung vom anderen trifft diese Farbe zu 1/5 bei sechs Haarfarben, zu 1/4 bei fünf Augenfarben).
                Pruefe.Ungefaehr(0.45 + 0.05 / 5, kinder.Count(k => k.Haar == Haarfarbe.Schwarz) / (double)n, 0.012);
                Pruefe.Ungefaehr(0.45 + 0.05 / 5, kinder.Count(k => k.Haar == Haarfarbe.Blond) / (double)n, 0.012);
                Pruefe.Ungefaehr(0.45 + 0.05 / 4, kinder.Count(k => k.Augen == Augenfarbe.Braun) / (double)n, 0.012);
                Pruefe.Wahr(kinder.All(k => Aussehenspalette.Verboten(Volk.Mensch, k) is null), "Kind außerhalb der Palette");
            }

            // Beide Eltern gleich: 90 % genau diese Farbe, die übrigen 10 % verteilen sich auf die anderen Farben.
            var zwilling = Elternteil(3, Volk.Mensch, Geschlecht.Frau, vater.Aussehen with { Gesicht = 1 });
            var gleichfarbig = Enumerable.Range(0, n).Select(_ => Aussehenspalette.Vererben(z, Volk.Mensch, Geschlecht.Frau, vater, zwilling)).ToList();
            Pruefe.Ungefaehr(0.9, gleichfarbig.Count(k => k.Haar == Haarfarbe.Schwarz) / (double)n, 0.01);
            Pruefe.Gleich(Aussehenspalette.Haarfarben(Volk.Mensch).Count, gleichfarbig.Select(k => k.Haar).Distinct().Count());
            Pruefe.Wahr(gleichfarbig.All(k => k.Gesicht == 1), "Gesicht 1 bei beiden Eltern");

            // Nur ein Elternteil bekannt (Kind aus früherer Ehe): alles von ihm.
            var allein = Enumerable.Range(0, 2000).Select(_ => Aussehenspalette.Vererben(z, Volk.Mensch, Geschlecht.Frau, vater, null)).ToList();
            Pruefe.Wahr(allein.All(k => k.Gesicht == 1), "Gesicht vom einzigen Elternteil");
            Pruefe.Ungefaehr(0.9, allein.Count(k => k.Haar == Haarfarbe.Schwarz) / 2000.0, 0.03);
        }),

        ("Aussehen: Ehe zwischen Völkern – das Kind hat nur Farben seines Volkes, die Gesichtsnummer bleibt", () =>
        {
            var vater = Elternteil(1, Volk.Mensch, Geschlecht.Mann, new Aussehen(2, Haarfarbe.Schwarz, Augenfarbe.Braun));
            var mutter = Elternteil(2, Volk.Elb, Geschlecht.Frau, new Aussehen(4, Haarfarbe.Silberblond, Augenfarbe.Violett));
            var z = new Zufallsquelle(3);
            var kinder = Enumerable.Range(0, 5000).Select(_ => Aussehenspalette.Vererben(z, Volk.Mensch, Geschlecht.Mann, vater, mutter)).ToList();
            Pruefe.Wahr(kinder.All(k => Aussehenspalette.Verboten(Volk.Mensch, k) is null), "Menschenkind mit Elbenfarbe");
            Pruefe.Wahr(kinder.Any(k => k.Gesicht == 4) && kinder.Any(k => k.Gesicht == 2), "Gesicht von beiden Eltern möglich");
            Pruefe.Wahr(kinder.Count(k => k.Haar == Haarfarbe.Schwarz) > 2500, "Schwarz vom Vater überwiegt");
        }),

        ("Aussehen: Altersstufen in Menschenjahren – Grenzen 14, 30, 55; Elben und Zwerge altern langsamer", () =>
        {
            Pruefe.Gleich(Altersstufe.Kind, Aussehenspalette.AltersstufeAus(13.99));
            Pruefe.Gleich(Altersstufe.Jung, Aussehenspalette.AltersstufeAus(14));
            Pruefe.Gleich(Altersstufe.Jung, Aussehenspalette.AltersstufeAus(29.99));
            Pruefe.Gleich(Altersstufe.Mittel, Aussehenspalette.AltersstufeAus(30));
            Pruefe.Gleich(Altersstufe.Mittel, Aussehenspalette.AltersstufeAus(54.99));
            Pruefe.Gleich(Altersstufe.Alt, Aussehenspalette.AltersstufeAus(55));

            var (sim, spieler) = Partie(41);
            var w = sim.Wirtschaft!;
            var b = w.Familien;
            Person Mit(Volk volk, int alter) => new(90_000 + alter, "Test", "Muster", volk, Geschlecht.Mann, b.Jahr - alter);
            foreach (var (alter, stufe) in new[] { (13, Altersstufe.Kind), (14, Altersstufe.Jung), (29, Altersstufe.Jung), (30, Altersstufe.Mittel),
                         (54, Altersstufe.Mittel), (55, Altersstufe.Alt) })
                Pruefe.Gleich(stufe, b.AltersstufeVon(Mit(Volk.Mensch, alter)), $"Mensch mit {alter}");
            // Ein Elb beim Spielstart (55) ist jung, ein volljähriger Zwerg (30) auch; ein Elb wird erst mit über 140 alt.
            Pruefe.Gleich(Altersstufe.Jung, b.AltersstufeVon(Mit(Volk.Elb, 55)));
            Pruefe.Gleich(Altersstufe.Jung, b.AltersstufeVon(Mit(Volk.Zwerg, 30)));
            Pruefe.Gleich(Altersstufe.Kind, b.AltersstufeVon(Mit(Volk.Elb, 30)));
            Pruefe.Gleich(Altersstufe.Alt, b.AltersstufeVon(Mit(Volk.Elb, 150)));
            Pruefe.Gleich(Altersstufe.Jung, b.AltersstufeVon(b.Von(spieler)!.Oberhaupt)); // Mensch mit 24

            // Verstorbene: das Alter beim Tod.
            var tot = new Person(90_500, "Test", "Muster", Volk.Mensch, Geschlecht.Mann, b.Jahr - 80, todesjahr: b.Jahr - 40);
            Pruefe.Gleich(Altersstufe.Mittel, b.AltersstufeVon(tot));
        }),

        ("Aussehen: Standgruppe und Kleidung folgen dem Stand des Hauses; Kandidaten ihrem Herkunftsstand", () =>
        {
            var (sim, spieler) = Partie(42);
            var w = sim.Wirtschaft!;
            var b = w.Familien;
            var oberhaupt = b.Von(spieler)!.Oberhaupt;
            Pruefe.Gleich(b.Von(spieler), b.FamilieVon(oberhaupt));
            var erwartet = new[] { Standgruppe.Gemeiner, Standgruppe.Buerger, Standgruppe.Patrizier, Standgruppe.Adel, Standgruppe.Adel, Standgruppe.Adel };
            Pruefe.Gleich(erwartet.Length, w.Daten.Staende.Count);
            Pruefe.Gleich("Reichsgraf", w.Daten.Staende[^1].Name);
            for (var stufe = 0; stufe < erwartet.Length; stufe++)
            {
                spieler.Stand = stufe;
                Pruefe.Gleich(erwartet[stufe], b.StandgruppeVon(oberhaupt), w.Daten.Staende[stufe].Name);
                Pruefe.Gleich(stufe >= 2 ? Kleidung.Vornehm : Kleidung.Schlicht, b.KleidungVon(oberhaupt), w.Daten.Staende[stufe].Name);
            }
            foreach (var k in b.Kandidaten)
            {
                Pruefe.Gleich(null, b.FamilieVon(k.Person));
                Pruefe.Gleich(k.Stand, b.StandVon(k.Person));
            }
            // Eine Person, die nirgends dazugehört, gilt als Gemeiner.
            Pruefe.Gleich(Standgruppe.Gemeiner, b.StandgruppeVon(new Person(91_000, "Test", "Muster", Volk.Mensch, Geschlecht.Mann, 1380)));
        }),

        ("Aussehen: KI-Familien gewürfelt und reproduzierbar; das gewählte Aussehen des Spielers ändert sie nicht", () =>
        {
            string Bild(Stadtwirtschaft w) => string.Join(";", AllePersonen(w).OrderBy(p => p.Nummer).Select(p => $"{p.Nummer}:{p.Aussehen}"));
            var gewaehlt = new Aussehen(4, Haarfarbe.Rot, Augenfarbe.Gruen);
            var (a, spielerA) = Partie(77);
            var (b, _) = Partie(77);
            var (c, spielerC) = Partie(77, new NeuePartie(Aussehen: gewaehlt));
            Pruefe.Gleich(Bild(a.Wirtschaft!), Bild(b.Wirtschaft!));
            Pruefe.Gleich(gewaehlt, c.Wirtschaft!.Familien.Von(spielerC)!.Oberhaupt.Aussehen);
            var ohneSpieler = (Stadtwirtschaft w, Konto s) => string.Join(";", AllePersonen(w).Where(p => p != w.Familien.Von(s)!.Oberhaupt)
                .OrderBy(p => p.Nummer).Select(p => $"{p.Nummer}:{p.Aussehen}"));
            Pruefe.Gleich(ohneSpieler(a.Wirtschaft!, spielerA), ohneSpieler(c.Wirtschaft!, spielerC));
            Pruefe.Wahr(AllePersonen(a.Wirtschaft!).Select(p => p.Aussehen).Distinct().Count() > 3, "alle sehen gleich aus");
            Pruefe.Wahr(AllePersonen(a.Wirtschaft!).All(p => Aussehenspalette.Verboten(p.Volk, p.Aussehen) is null), "außerhalb der Palette");

            Pruefe.WirftMitMeldung(() => Partie(77, new NeuePartie(Aussehen: new Aussehen(0, Haarfarbe.Silberblond, Augenfarbe.Blau))), "Haarfarbe");
            Pruefe.WirftMitMeldung(() => Partie(77, new NeuePartie(Aussehen: new Aussehen(7, Haarfarbe.Braun, Augenfarbe.Blau))), "Gesicht 7");
        }),

        ("Aussehen: Kinder im Spiel tragen ein Gesicht ihrer Eltern; Speichern und Laden behält alles und würfelt danach gleich weiter", () =>
        {
            var (sim, spieler) = Partie(202);
            var w = sim.Wirtschaft!;
            for (var i = 0; i < 20; i++) JahrBeenden(sim);
            var personen = AllePersonen(w).ToDictionary(p => p.Nummer);
            var kinder = personen.Values.Where(p => p.Vater is { } v && p.Mutter is { } m && personen.ContainsKey(v) && personen.ContainsKey(m)).ToList();
            Pruefe.Wahr(kinder.Count >= 3, $"nur {kinder.Count} Kinder in 20 Jahren");
            foreach (var k in kinder)
                Pruefe.Wahr(k.Aussehen.Gesicht == personen[k.Vater!.Value].Aussehen.Gesicht || k.Aussehen.Gesicht == personen[k.Mutter!.Value].Aussehen.Gesicht,
                    $"{k.Name}: Gesicht nicht von den Eltern");

            var (kopie, kopieSpieler) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            string Bild(Stadtwirtschaft x) => string.Join(";", AllePersonen(x).OrderBy(p => p.Nummer).Select(p => $"{p.Nummer}:{p.Aussehen}"));
            Pruefe.Gleich(Bild(w), Bild(kopie.Wirtschaft!));
            for (var i = 0; i < 15; i++) { JahrBeenden(sim); JahrBeenden(kopie); }
            Pruefe.Wahr(AllePersonen(w).Max(p => p.Nummer) > personen.Keys.Max(), "keine neuen Personen nach dem Laden");
            Pruefe.Gleich(Bild(w), Bild(kopie.Wirtschaft!));
        }),

        ("Aussehen: Ein alter Spielstand ohne Aussehen lädt – festes Aussehen aus Nummer und Volk, die Würfe der Partie bleiben", () =>
        {
            var json = AlterStand();
            var alt = JsonNode.Parse(json)!.AsObject();
            var altFamilien = alt["Wirtschaft"]!["Familien"]!;
            Pruefe.Wahr(altFamilien["Personen"]!.AsArray().All(p => p!["Gesicht"] is null), "Der alte Spielstand hat schon Aussehen");
            var (sim, spieler) = Speicherung.Laden(json);
            var w = sim.Wirtschaft!;
            var personen = AllePersonen(w).ToList();
            Pruefe.Wahr(personen.Count >= 10, $"nur {personen.Count} Personen");
            foreach (var p in personen)
                Pruefe.Gleich(Aussehenspalette.Abgeleitet(p.Nummer, p.Volk), p.Aussehen, p.Name);
            Pruefe.Wahr(personen.Select(p => p.Aussehen).Distinct().Count() > 3, "alle sehen gleich aus");

            var neu = JsonNode.Parse(Speicherung.Speichern(sim, spieler))!["Wirtschaft"]!["Familien"]!;
            Pruefe.Gleich((long)altFamilien["ZufallWuerfe"]!, (long)neu["ZufallWuerfe"]!);
            Pruefe.Gleich((long)altFamilien["TalentWuerfe"]!, (long)neu["TalentWuerfe"]!);
            Pruefe.Gleich(0L, (long)neu["AussehenWuerfe"]!);
            var (wieder, _) = Speicherung.Laden(json);
            Pruefe.Gleich(string.Join(";", personen.Select(p => p.Aussehen)), string.Join(";", AllePersonen(wieder.Wirtschaft!).Select(p => p.Aussehen)));
        }),
    };
}
