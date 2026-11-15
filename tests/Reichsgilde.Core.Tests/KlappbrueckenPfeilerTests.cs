using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Core.Zeit;

/// <summary>
/// Die Pfeiler der Klappbrücke (<see cref="Wasserplan.KlappDurchfahrt"/>): Die Steinbrücke mit Karren über das Gewässer aus
/// <c>logistik.klappbruecke.gewaesser</c> hat in der Flussmitte das Mittelteil von 21 m mit 13 m lichter Durchfahrt zwischen zwei
/// Torhauspfeilern; außen Bögen nach der Steinregel. Andere Brücken und Karten bleiben bitgleich.
/// </summary>
static class KlappbrueckenPfeilerTests
{
    static Stadtwirtschaft Mit(Wirtschaftsdaten daten, string karte)
    {
        var sim = new Simulation(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Zufallsstartwert = 1400 }, new Spielzeit(0), daten);
        sim.Wirtschaft!.KarteSetzen(Stadtkarte.AusDatei(Datenpfad.Karte(karte)));
        return sim.Wirtschaft;
    }

    /// <summary>Alles einer Querung als Text (Zahlen mit „R“): gleich heißt bitgleich.</summary>
    static string Text(Wasserquerung q) =>
        (q with { Pfeiler = Array.Empty<Brueckenpfeiler>() }).ToString() + "|" + string.Join(";", q.Pfeiler.Select(p => $"{p.Ort.X:R},{p.Ort.Z:R}@{p.Fuss:R}"))
        + $"|{q.Feldlaenge:R}|{q.Weite:R}|{q.Deckhoehe:R}|{q.Durchfahrt:R}";

    /// <summary>Lage eines Pfeilers als Meter entlang der Brücke von Ufer A (Abstand zum Anfang der Uferkante A auf der Geraden UferA–UferB).</summary>
    static double Stelle(Wasserquerung q, Brueckenpfeiler p)
    {
        var (dx, dz) = (q.UferB.X - q.UferA.X, q.UferB.Z - q.UferA.Z);
        var l = Math.Sqrt(dx * dx + dz * dz);
        return ((p.Ort.X - q.UferA.X) * dx + (p.Ort.Z - q.UferA.Z) * dz) / l;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Klappbrücke Köln: Torhauspfeiler mit 13 m Durchfahrt und 21 m Mittelteil in der Flussmitte, außen regelmäßige Bögen; " +
         "Gegenproben ohne Abschnitt (Steinregel) und alle anderen Querungen bitgleich", () =>
        {
            var mit = Mit(KlappbrueckeTests.DatenMit(_ => { }), "koeln").Karte!;
            var ohne = Mit(KlappbrueckeTests.OhneKlappbruecke(), "koeln").Karte!;
            Pruefe.Gleich("Rhein", mit.Klappbruecke);
            Pruefe.Gleich<string?>(null, ohne.Klappbruecke);
            var q = mit.Wasserplan.Bruecken.Single(b => b.Karren && b.GewaesserName == "Rhein");
            var alt = ohne.Wasserplan.Bruecken.Single(b => b.Karren && b.GewaesserName == "Rhein");
            Pruefe.Gleich(Uebergangsart.Steinbruecke, q.Art);

            // Mittelteil: die beiden Torhauspfeiler je 4 m, symmetrisch zur Flussmitte, 13 m Licht dazwischen
            var mitte = q.Weite / 2;
            var stellen = q.Pfeiler.Select(p => Stelle(q, p) - mitte).ToList();
            var torhaus = (Wasserplan.KlappMittelteil - Wasserplan.KlappDurchfahrt) / 2;
            Pruefe.Gleich(13.0, q.Durchfahrt);
            Pruefe.Gleich(4.0, torhaus);
            var innen = stellen.Where(s => Math.Abs(s) < Wasserplan.KlappMittelteil / 2).OrderBy(s => s).ToList();
            Pruefe.Gleich(2, innen.Count, "nur die Torhauspfeiler im Mittelteil");
            Pruefe.Ungefaehr(-8.5, innen[0], 1e-6);
            Pruefe.Ungefaehr(8.5, innen[1], 1e-6);
            Pruefe.Ungefaehr(13, innen[1] - innen[0] - torhaus, 1e-6);
            Pruefe.Ungefaehr(21, innen[1] - innen[0] + torhaus, 1e-6);

            // außen: je Seite gleich lange Bögen bis zur Uferkante, Pfeiler 2 m, spiegelbildlich
            var seite = q.Weite / 2 - 10.5;
            var n = q.Felder / 2;
            Pruefe.Gleich(Math.Max(1, (int)Math.Round((seite + 2) / 10, MidpointRounding.AwayFromZero)), n);
            Pruefe.Ungefaehr((seite - 2 * (n - 1)) / n, q.Feldlaenge, 1e-9);
            Pruefe.Gleich(2 + 2 * (n - 1), q.Pfeiler.Count);
            var geordnet = stellen.OrderBy(s => s).ToList();
            Pruefe.Wahr(stellen.SequenceEqual(geordnet), "Pfeiler von Ufer A nach B");
            for (var i = 0; i < geordnet.Count; i++) Pruefe.Ungefaehr(-geordnet[^(i + 1)], geordnet[i], 1e-6);
            var aussen = new List<double> { -q.Weite / 2 - 1 }; // gedachter Pfeiler an der Uferkante A (Mitte 1 m außerhalb)
            aussen.AddRange(geordnet.Take(n - 1));
            for (var i = 1; i < aussen.Count; i++) Pruefe.Ungefaehr(q.Feldlaenge + 2, aussen[i] - aussen[i - 1], 1e-6);
            Pruefe.Ungefaehr(q.Feldlaenge + 1 + torhaus / 2, geordnet[n - 1] - aussen[^1], 1e-6); // letzter Bogen bis an das Torhaus
            Pruefe.Wahr(q.Pfeiler.All(p => p.Fuss < q.Spiegel), "Füße im Flussbett");
            Console.WriteLine($"      Rheinbrücke: Weite {q.Weite:0.00} m, {n} Bögen je Seite zu {q.Feldlaenge:0.00} m, Pfeiler bei "
                + string.Join(" / ", stellen.Select(s => s.ToString("+0.0;-0.0", System.Globalization.CultureInfo.InvariantCulture))) + " m; ohne Abschnitt "
                + string.Join(" / ", alt.Pfeiler.Select(p => (Stelle(alt, p) - mitte).ToString("+0.0;-0.0", System.Globalization.CultureInfo.InvariantCulture))));

            // Gegenprobe ohne Abschnitt: die Steinregel (Bögen über die ganze Weite, keine Durchfahrt)
            Pruefe.Gleich(0.0, alt.Durchfahrt);
            var na = Math.Max(1, (int)Math.Round((alt.Weite + 2) / 10, MidpointRounding.AwayFromZero));
            Pruefe.Gleich(na, alt.Felder);
            Pruefe.Gleich(na - 1, alt.Pfeiler.Count);
            // nur die Pfeiler, Felder und Durchfahrt der Rheinbrücke ändern sich, alles andere bitgleich
            Pruefe.Gleich(Text(alt with { Pfeiler = q.Pfeiler, Felder = q.Felder, Feldlaenge = q.Feldlaenge, Durchfahrt = q.Durchfahrt }), Text(q));
            Pruefe.Gleich(ohne.Wasserplan.Querungen.Count, mit.Wasserplan.Querungen.Count);
            var andere = 0;
            for (var i = 0; i < mit.Wasserplan.Querungen.Count; i++)
            {
                var (a, b) = (ohne.Wasserplan.Querungen[i], mit.Wasserplan.Querungen[i]);
                if (ReferenceEquals(a, alt)) continue;
                Pruefe.Gleich(Text(a), Text(b), $"Querung {i} ({a.WegName})");
                andere++;
            }
            Pruefe.Wahr(andere > 0, "Köln hat weitere Querungen");
        }),

        ("Klappbrücke: andere Karten unverändert (Greybox und Köln-Karte ohne Wirtschaft bitgleich), anderes Gewässer ohne Mittelteil, Wasserplan zweimal gleich", () =>
        {
            foreach (var name in new[] { "greybox", "koeln" })
            {
                var mit = Mit(KlappbrueckeTests.DatenMit(_ => { }), name).Karte!;
                var roh = Stadtkarte.AusDatei(Datenpfad.Karte(name));
                var erwartet = name == "koeln" ? 1 : 0;
                Pruefe.Gleich(erwartet, mit.Wasserplan.Querungen.Count(q => q.Durchfahrt > 0), name);
                Pruefe.Wahr(roh.Wasserplan.Querungen.All(q => q.Durchfahrt == 0), "ohne Wirtschaft kein Mittelteil");
                if (name == "greybox")
                    Pruefe.Gleich(string.Join("\n", roh.Wasserplan.Querungen.Select(Text)), string.Join("\n", mit.Wasserplan.Querungen.Select(Text)), "Greybox bitgleich");
                // gleiches Gewässer zweimal gesetzt: kein Neurechnen; anderes Gewässer: Steinregel
                var plan = mit.Wasserplan;
                mit.KlappbrueckeSetzen(mit.Klappbruecke);
                Pruefe.Wahr(ReferenceEquals(plan, mit.Wasserplan), "unverändert");
                mit.KlappbrueckeSetzen("Duffesbach");
                Pruefe.Gleich(string.Join("\n", roh.Wasserplan.Querungen.Select(Text)), string.Join("\n", mit.Wasserplan.Querungen.Select(Text)), $"{name}: anderes Gewässer");
                var zweite = Stadtkarte.AusDatei(Datenpfad.Karte(name));
                zweite.KlappbrueckeSetzen("Rhein");
                var dritte = Stadtkarte.AusDatei(Datenpfad.Karte(name));
                dritte.KlappbrueckeSetzen("Rhein");
                Pruefe.Gleich(string.Join("\n", zweite.Wasserplan.Querungen.Select(Text)), string.Join("\n", dritte.Wasserplan.Querungen.Select(Text)), $"{name}: deterministisch");
            }
        }),

        ("Klappbrücke: nach Speichern und Laden dieselben Pfeiler (Spielstand ohne neues Feld, das Gewässer kommt aus den Daten)", () =>
        {
            var sim = KlappbrueckeTests.Partie(Stadtkarte.AusDatei(Datenpfad.Karte("koeln")), KlappbrueckeTests.DatenMit(_ => { }));
            var w = sim.Wirtschaft!;
            var text = Speicherung.Speichern(sim, w.Konten[0], new DateTime(2026, 1, 1));
            var (geladen, _) = Speicherung.Laden(text);
            var vorher = w.Karte!.Wasserplan.Querungen.Select(Text).ToList();
            var nachher = geladen.Wirtschaft!.Karte!.Wasserplan.Querungen.Select(Text).ToList();
            Pruefe.Gleich("Rhein", geladen.Wirtschaft.Karte.Klappbruecke);
            Pruefe.Gleich(string.Join("\n", vorher), string.Join("\n", nachher));
            Pruefe.Wahr(nachher.Count(t => t.Contains("Durchfahrt = 13")) == 1, "geladen: Mittelteil da");
        }),
    };
}
