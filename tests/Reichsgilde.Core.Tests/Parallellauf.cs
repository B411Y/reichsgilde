using System.Diagnostics;

// ─────────────────────────────────────────────────────────────────────────────
//  Paralleler Testlauf (Schalter  --parallel[=N], nur für lokale Läufe; die CI läuft seriell).
//
//  - Einheit ist die Testklasse: Klassen laufen gleichzeitig auf N Fäden, die Tests einer Klasse
//    nacheinander in ihrer Reihenfolge. Zähler und Lazy-Felder einer Klasse bleiben so in einem Faden.
//  - Die Ausgabe jeder Klasse (OK/FEHLER und was ihre Tests mit Console.WriteLine schreiben) wird
//    gepuffert und in fester Reihenfolge geschrieben: die Klassen in der gewohnten Reihenfolge, sobald
//    eine Klasse und alle vor ihr fertig sind.
//  - Die Klassen aus SeriellTests.cs (Zeitgrenzen) laufen danach allein, eine nach der anderen.
//  Der Kern hat keinen veränderlichen statischen Zustand und jede Simulation ihren eigenen Zufall –
//  deshalb rechnet jeder Test parallel dasselbe wie seriell.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Führt Testklassen auf mehreren Fäden aus.</summary>
static class Parallellauf
{
    /// <summary>Standard für <c>--parallel</c> ohne Zahl: Kerne − 2, höchstens 6 (die NAS teilen sich mehrere Mitglieder).</summary>
    public static int StandardFaeden => Math.Clamp(Environment.ProcessorCount - 2, 1, 6);

    /// <summary>Stapelgröße eines Testfadens: großzügig wie der Hauptfaden (Linux 8 MB), damit tiefe Rekursion nicht nur parallel scheitert.</summary>
    const int Stapel = 16 * 1024 * 1024;

    /// <summary>Eine Testklasse im Lauf: ihre Tests, ihr Puffer, ihre Ergebnisse.</summary>
    sealed class Klassenlauf(string klasse, List<Testfall> tests)
    {
        public string Klasse { get; } = klasse;
        public List<Testfall> Tests { get; } = tests;
        public StringWriter Text { get; } = new();
        public int Fehler { get; set; }
        public List<(Testfall Test, double Sekunden)> Dauer { get; } = new();
        public double Sekunden { get; set; }
        public ManualResetEventSlim Fertig { get; } = new(false);
    }

    /// <summary>Ergebnis: Fehlerzahl, Dauer je Test (in Ausgabereihenfolge), Dauer je Klasse.</summary>
    public sealed record Ergebnis(int Fehler, List<(Testfall Test, double Sekunden)> Dauer, List<(string Klasse, double Sekunden, bool Seriell)> Klassen);

    public static Ergebnis Ausfuehren(IReadOnlyList<Testfall> laufen, int faeden, TextWriter aus)
    {
        // Klassen in der Reihenfolge ihres ersten Tests (= Reihenfolge des seriellen Laufs).
        var klassen = new List<Klassenlauf>();
        var nachName = new Dictionary<string, Klassenlauf>(StringComparer.Ordinal); // nur Nachschlagen, Reihenfolge aus der Liste
        foreach (var test in laufen)
        {
            if (!nachName.TryGetValue(test.Klasse, out var k))
            {
                k = new Klassenlauf(test.Klasse, new List<Testfall>());
                nachName[test.Klasse] = k;
                klassen.Add(k);
            }
            k.Tests.Add(test);
        }
        var gleichzeitig = klassen.Where(k => !SeriellTests.IstSeriell(k.Klasse)).ToList();
        var allein = klassen.Where(k => SeriellTests.IstSeriell(k.Klasse)).ToList();
        // Klassen mit langsamen Tests zuerst anfangen, damit am Ende nicht eine lange Klasse allein läuft (OrderBy ist stabil).
        var arbeitsreihe = gleichzeitig.OrderByDescending(k => k.Tests.Count(LangsameTests.IstLangsam)).ToList();
        var anzahlFaeden = Math.Max(1, Math.Min(faeden, gleichzeitig.Count));

        aus.WriteLine($"  Parallel: {gleichzeitig.Count} Klassen auf {anzahlFaeden} Fäden"
                      + (allein.Count > 0 ? $", danach {allein.Count} allein (SeriellTests.cs)." : "."));

        using (Konsolenweiche.Einschalten())
        {
            var naechste = -1;
            var arbeiter = new List<Thread>();
            if (gleichzeitig.Count > 0)
                for (var i = 0; i < anzahlFaeden; i++)
                {
                    var faden = new Thread(() =>
                    {
                        while (true)
                        {
                            var nummer = Interlocked.Increment(ref naechste);
                            if (nummer >= arbeitsreihe.Count)
                                return;
                            KlasseAusfuehren(arbeitsreihe[nummer]);
                        }
                    }, Stapel) { IsBackground = true, Name = $"Testfaden {i + 1}" };
                    arbeiter.Add(faden);
                    faden.Start();
                }

            // In fester Reihenfolge ausgeben, sobald die nächste Klasse fertig ist.
            foreach (var k in gleichzeitig)
            {
                k.Fertig.Wait();
                aus.Write(k.Text.ToString());
            }
            foreach (var faden in arbeiter)
                faden.Join();

            // Die Klassen mit Zeitgrenzen allein, nacheinander.
            foreach (var k in allein)
            {
                KlasseAusfuehren(k);
                aus.Write(k.Text.ToString());
            }
        }

        var reihenfolge = gleichzeitig.Concat(allein).ToList();
        return new Ergebnis(
            reihenfolge.Sum(k => k.Fehler),
            reihenfolge.SelectMany(k => k.Dauer).ToList(),
            reihenfolge.Select(k => (k.Klasse, k.Sekunden, allein.Contains(k))).ToList());
    }

    static void KlasseAusfuehren(Klassenlauf k)
    {
        // Gleichzeitige Schreiber (ein Test, der selbst Parallel.For nutzt) sollen den Puffer nicht zerreißen.
        var puffer = TextWriter.Synchronized(k.Text);
        var vorher = Konsolenweiche.Ziel.Value;
        Konsolenweiche.Ziel.Value = puffer;
        var uhr = Stopwatch.StartNew();
        try
        {
            foreach (var test in k.Tests)
            {
                var start = uhr.Elapsed.TotalSeconds;
                if (!Testlauf.EinTest(test, puffer))
                    k.Fehler++;
                k.Dauer.Add((test, uhr.Elapsed.TotalSeconds - start));
            }
        }
        finally
        {
            Konsolenweiche.Ziel.Value = vorher;
            k.Sekunden = uhr.Elapsed.TotalSeconds;
            k.Fertig.Set();
        }
    }
}

/// <summary>
/// Leitet <c>Console.Out</c> in den Puffer der Klasse, die auf diesem Faden läuft (<see cref="Ziel"/>; fließt mit
/// in Fäden, die ein Test selbst startet). Ohne Ziel geht die Ausgabe wie gewohnt an die Konsole.
/// </summary>
static class Konsolenweiche
{
    public static readonly AsyncLocal<TextWriter?> Ziel = new();

    static readonly object Sperre = new();
    static int _eingeschaltet;
    static TextWriter? _konsole;

    /// <summary>Schaltet die Weiche ein (verschachtelt erlaubt); Dispose stellt die Konsole wieder her.</summary>
    public static IDisposable Einschalten()
    {
        lock (Sperre)
        {
            if (_eingeschaltet++ == 0)
            {
                _konsole = Console.Out;
                Console.SetOut(new Weiche(_konsole));
            }
        }
        return new Ausschalter();
    }

    sealed class Ausschalter : IDisposable
    {
        bool _erledigt;
        public void Dispose()
        {
            lock (Sperre)
            {
                if (_erledigt) return;
                _erledigt = true;
                if (--_eingeschaltet == 0)
                {
                    Console.Out.Flush();
                    Console.SetOut(_konsole!);
                    _konsole = null;
                }
            }
        }
    }

    sealed class Weiche(TextWriter konsole) : TextWriter
    {
        TextWriter Jetzt => Ziel.Value ?? konsole;
        public override System.Text.Encoding Encoding => konsole.Encoding;
        public override IFormatProvider FormatProvider => konsole.FormatProvider;
        public override void Write(char wert) => Jetzt.Write(wert);
        public override void Write(string? wert) => Jetzt.Write(wert);
        public override void Write(char[] puffer, int index, int anzahl) => Jetzt.Write(puffer, index, anzahl);
        public override void WriteLine() => Jetzt.WriteLine();
        public override void WriteLine(string? wert) => Jetzt.WriteLine(wert);
        public override void Flush() => Jetzt.Flush();
    }
}
