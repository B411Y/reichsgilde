// Tests für den Schalter --parallel (Parallellauf.cs, SeriellTests.cs).

static class ParallellaufTests
{
    /// <summary>Nur die Zeilen der Tests (OK, FEHLER, Meldung) – ohne Laufzeit und ohne die Zeile „Parallel: …“.</summary>
    static List<string> Testzeilen(string text) =>
        text.Split('\n').Select(z => z.TrimEnd('\r'))
            .Where(z => z.StartsWith("  OK", StringComparison.Ordinal) || z.StartsWith("  FEHLER", StringComparison.Ordinal) || z.Contains('→'))
            .ToList();

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Testauswahl: --parallel nimmt den Standard (1–6 Fäden), --parallel=3 drei; ohne Schalter seriell, Unsinn fällt auf", () =>
        {
            Pruefe.Gleich(0, Testauswahl.Lesen(Array.Empty<string>()).Parallel);
            var standard = Testauswahl.Lesen(new[] { "--parallel" });
            Pruefe.Gleich(null, standard.Fehler);
            Pruefe.Gleich(Parallellauf.StandardFaeden, standard.Parallel);
            Pruefe.Wahr(standard.Parallel is >= 1 and <= 6, $"Standard {standard.Parallel} Fäden");
            Pruefe.Gleich(Math.Clamp(Environment.ProcessorCount - 2, 1, 6), Parallellauf.StandardFaeden);
            Pruefe.Gleich(3, Testauswahl.Lesen(new[] { "--parallel=3", "--ohne-langsam", "--klasse=DiebTests" }).Parallel);
            Pruefe.Wahr(Testauswahl.Lesen(new[] { "--parallel=0" }).Fehler is not null, "--parallel=0 sind keine Fäden");
            Pruefe.Wahr(Testauswahl.Lesen(new[] { "--parallel=viele" }).Fehler is not null, "--parallel=viele ist keine Zahl");
            Pruefe.Wahr(Testauswahl.Lesen(new[] { "--parallel=" }).Fehler is not null, "--parallel= ohne Zahl");
            Pruefe.Wahr(Testauswahl.Hilfetext.Contains("--parallel"), "Hilfe nennt --parallel");
        }),

        ("Parallel: dieselben Zeilen in derselben Reihenfolge wie seriell, gleicher Exit-Code, Konsolenausgabe im Block ihrer Klasse", () =>
        {
            // Klasse A wartet, bis B angefangen hat – so laufen die beiden sicher gleichzeitig (mit 2 Fäden).
            using var bGestartet = new ManualResetEventSlim(false);
            var gleichzeitig = false;
            var tests = new List<Testfall>
            {
                new("ATests", "a1", () =>
                {
                    Console.WriteLine("    Ausgabe von a1");
                    gleichzeitig = bGestartet.Wait(TimeSpan.FromSeconds(20));
                }),
                new("ATests", "a2 schlägt fehl", () => throw new Exception("absichtlich")),
                new("BTests", "b1", () => { bGestartet.Set(); Console.WriteLine("    Ausgabe von b1"); }),
                new("BTests", "b2", () => { }),
                new("CTests", "c1", () => Console.WriteLine("    Ausgabe von c1")),
            };

            var seriell = new StringWriter();
            bGestartet.Set(); // seriell läuft B erst nach A
            using (Konsolenweiche.Einschalten()) // die Konsolenausgabe des seriellen Laufs nicht in den echten Testlauf schreiben
            {
                var vorher = Konsolenweiche.Ziel.Value;
                Konsolenweiche.Ziel.Value = new StringWriter();
                try { Pruefe.Gleich(1, Testlauf.Ausfuehren(tests, Testauswahl.Lesen(Array.Empty<string>()), seriell)); }
                finally { Konsolenweiche.Ziel.Value = vorher; }
            }
            bGestartet.Reset();

            var parallel = new StringWriter();
            Pruefe.Gleich(1, Testlauf.Ausfuehren(tests, Testauswahl.Lesen(new[] { "--parallel=2" }), parallel));
            Pruefe.Wahr(gleichzeitig, "A und B liefen nicht gleichzeitig");

            Pruefe.Gleich(string.Join("|", Testzeilen(seriell.ToString())), string.Join("|", Testzeilen(parallel.ToString())));
            var text = parallel.ToString();
            Pruefe.Wahr(text.Contains("1 von 5 Tests fehlgeschlagen."), text);
            Pruefe.Wahr(text.Contains("Parallel: 3 Klassen auf 2 Fäden."), text);
            // Die Konsolenausgabe eines Tests steht vor seiner OK-Zeile, im Block seiner Klasse.
            var stellen = new[] { "Ausgabe von a1", "OK      a1", "FEHLER  a2", "Ausgabe von b1", "OK      b1", "OK      b2", "Ausgabe von c1", "OK      c1" }
                .Select(s => text.IndexOf(s, StringComparison.Ordinal)).ToList();
            Pruefe.Wahr(stellen.All(i => i >= 0) && stellen.Zip(stellen.Skip(1)).All(p => p.First < p.Second), text);
        }),

        ("Parallel: Klassen aus SeriellTests.cs laufen am Ende allein, die Ausgabe steht danach", () =>
        {
            var serielleKlasse = SeriellTests.Liste[0].Klasse;
            var laufend = 0;
            var alleinBeiSeriell = true;
            var reihenfolge = new List<string>();
            var sperre = new object();
            Action Test(string klasse) => () =>
            {
                lock (sperre) { laufend++; reihenfolge.Add(klasse); }
                Thread.Sleep(30);
                lock (sperre)
                {
                    if (klasse == serielleKlasse && laufend != 1) alleinBeiSeriell = false;
                    laufend--;
                }
            };
            var tests = new List<Testfall>
            {
                new("ATests", "a", Test("ATests")),
                new(serielleKlasse, "s1", Test(serielleKlasse)),
                new(serielleKlasse, "s2", Test(serielleKlasse)),
                new("BTests", "b", Test("BTests")),
                new("CTests", "c", Test("CTests")),
            };
            var text = new StringWriter();
            Pruefe.Gleich(0, Testlauf.Ausfuehren(tests, Testauswahl.Lesen(new[] { "--parallel=3", "--zeiten=3" }), text));
            Pruefe.Wahr(alleinBeiSeriell, "Die serielle Klasse lief neben einer anderen");
            Pruefe.Gleich(serielleKlasse, reihenfolge[^1]);
            Pruefe.Gleich(serielleKlasse, reihenfolge[^2]);
            var t = text.ToString();
            Pruefe.Wahr(t.IndexOf("OK      c", StringComparison.Ordinal) < t.IndexOf("OK      s1", StringComparison.Ordinal), t);
            Pruefe.Wahr(t.Contains("danach 1 allein (SeriellTests.cs)"), t);
            Pruefe.Wahr(t.Contains("langsamsten Klassen"), t);
        }),

        ("SeriellTests.cs: jede Klasse der Liste gibt es und sie hat Tests (Name ganz, einmal eingetragen)", () =>
        {
            var klassen = Testlauf.KlassenTests(typeof(Pruefe).Assembly).Select(t => t.Klasse).ToHashSet(StringComparer.Ordinal);
            foreach (var (klasse, grund) in SeriellTests.Liste)
            {
                Pruefe.Wahr(klassen.Contains(klasse), $"SeriellTests: Klasse {klasse} gibt es nicht (mehr) oder sie hat keine Tests");
                Pruefe.Wahr(grund.Length > 0, $"SeriellTests: {klasse} ohne Grund");
            }
            Pruefe.Gleich(SeriellTests.Liste.Length, SeriellTests.Liste.Select(e => e.Klasse).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }),
    };
}
