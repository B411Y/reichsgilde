using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Reichsgilde.Core;

// ─────────────────────────────────────────────────────────────────────────────
//  Testauswahl: gezielt testen statt jedes Mal alle Tests laufen zu lassen.
//
//    dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- --filter=Taschendieb,Nachtgang
//    dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- --klasse=DiebTests
//    dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- --ohne-langsam
//    dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- --liste --filter=Hafen
//    dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- --zeiten
//    dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- --parallel
//
//  Ohne Schalter laufen wie bisher alle Tests nacheinander (so prüft auch die CI).
//  Welche Tests als langsam gelten, steht in LangsameTests.cs; welche Klassen mit --parallel
//  am Ende allein laufen, in SeriellTests.cs (der parallele Lauf selbst: Parallellauf.cs).
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Ein Test mit seiner Klasse (Tests direkt in Program.cs haben die Klasse „Program“).</summary>
sealed record Testfall(string Klasse, string Name, Action Pruefung);

/// <summary>Die Schalter eines Testlaufs.</summary>
sealed class Testauswahl
{
    /// <summary>Muster für Klassen- oder Testname (eines muss passen); leer = alle.</summary>
    public List<string> Filter { get; } = new();
    /// <summary>Muster für den Klassennamen (ganzer Name muss passen); leer = alle.</summary>
    public List<string> Klassen { get; } = new();
    public bool OhneLangsam { get; set; }
    public bool NurListe { get; set; }
    /// <summary>Wie viele der langsamsten Tests am Ende genannt werden (0 = keine Zeitliste).</summary>
    public int Zeiten { get; set; }
    /// <summary>Wie viele Fäden mit --parallel (0 = seriell wie bisher).</summary>
    public int Parallel { get; set; }
    public bool Hilfe { get; set; }
    /// <summary>Gesetzt, wenn ein Schalter unbekannt oder falsch ist.</summary>
    public string? Fehler { get; private set; }

    public const string Hilfetext =
        """
        Schalter der Kern-Tests (ohne Schalter laufen alle Tests):
          --filter=<Muster>[,<Muster>…]  nur Tests, deren Klassen- oder Testname das Muster enthält
                                         (ohne Groß- und Kleinschreibung, * steht für beliebigen Text)
          --klasse=<Name>[,<Name>…]      nur diese Testklassen (ganzer Name, * erlaubt), z. B. DiebTests
          --ohne-langsam                 langsame Tests überspringen (Liste in LangsameTests.cs)
          --liste                        die ausgewählten Tests nur auflisten, nicht ausführen
          --zeiten[=<Anzahl>]            am Ende die langsamsten Tests mit Sekunden (Standard 20)
          --parallel[=<Fäden>]           Testklassen gleichzeitig (Standard: Kerne − 2, höchstens 6); Ausgabe
                                         je Klasse in fester Reihenfolge, Klassen aus SeriellTests.cs am Ende allein
          --hilfe                        diesen Text zeigen
        Mehrere Schalter wirken zusammen: --klasse=Buerger* --filter=Hafen
        """;

    public static Testauswahl Lesen(IEnumerable<string> argumente)
    {
        var auswahl = new Testauswahl();
        foreach (var argument in argumente)
        {
            var (schalter, wert) = Teilen(argument);
            switch (schalter)
            {
                case "--filter" when Muster(wert).Any():
                    auswahl.Filter.AddRange(Muster(wert));
                    break;
                case "--klasse" when Muster(wert).Any():
                    auswahl.Klassen.AddRange(Muster(wert));
                    break;
                case "--ohne-langsam" when wert is null:
                    auswahl.OhneLangsam = true;
                    break;
                case "--liste" when wert is null:
                    auswahl.NurListe = true;
                    break;
                case "--zeiten" when wert is null:
                    auswahl.Zeiten = 20;
                    break;
                case "--zeiten" when int.TryParse(wert, out var anzahl) && anzahl > 0:
                    auswahl.Zeiten = anzahl;
                    break;
                case "--parallel" when wert is null:
                    auswahl.Parallel = Parallellauf.StandardFaeden;
                    break;
                case "--parallel" when int.TryParse(wert, out var faeden) && faeden is > 0 and <= 64:
                    auswahl.Parallel = faeden;
                    break;
                case "--hilfe" or "-h" or "--help" when wert is null:
                    auswahl.Hilfe = true;
                    break;
                default:
                    auswahl.Fehler ??= $"Unbekannter oder unvollständiger Schalter: {argument}";
                    break;
            }
        }
        return auswahl;
    }

    static (string Schalter, string? Wert) Teilen(string argument)
    {
        var gleich = argument.IndexOf('=');
        return gleich < 0 ? (argument, null) : (argument[..gleich], argument[(gleich + 1)..]);
    }

    static string[] Muster(string? wert) =>
        wert?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();

    /// <summary>Gehört der Test zur Auswahl (Filter und Klasse; „langsam“ prüft <see cref="IstLangsam"/>)?</summary>
    public bool Passt(Testfall test) =>
        (Klassen.Count == 0 || Klassen.Any(m => Mustervergleich.Ganz(m, test.Klasse)))
        && (Filter.Count == 0 || Filter.Any(m => Mustervergleich.Enthaelt(m, test.Klasse) || Mustervergleich.Enthaelt(m, test.Name)));
}

/// <summary>Vergleich ohne Groß- und Kleinschreibung, <c>*</c> steht für beliebigen Text.</summary>
static class Mustervergleich
{
    /// <summary>Kommt das Muster irgendwo im Text vor? („dieb“ passt auf „Taschendiebe“)</summary>
    public static bool Enthaelt(string muster, string text) => Ganz("*" + muster + "*", text);

    /// <summary>Passt das Muster auf den ganzen Text? („Dieb*“ passt auf „DiebTests“, „Dieb“ nicht)</summary>
    public static bool Ganz(string muster, string text)
    {
        // Jedes Stück zwischen den Sternen wörtlich nehmen, die Sterne werden zu „beliebig viel“.
        var ausdruck = "^" + string.Join(".*", muster.Split('*').Select(Regex.Escape)) + "$";
        return Regex.IsMatch(text, ausdruck, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }
}

/// <summary>Findet die Testklassen und führt eine Auswahl der Tests aus.</summary>
static class Testlauf
{
    /// <summary>Alle Testklassen: jede statische Klasse mit <c>public static IEnumerable&lt;(string, Action)&gt; Alle()</c>,
    /// nach Klassenname geordnet (fest, damit jeder Lauf gleich abläuft).</summary>
    public static List<Testfall> KlassenTests(Assembly baugruppe)
    {
        var tests = new List<Testfall>();
        foreach (var klasse in baugruppe.GetTypes()
                     .Where(t => t is { IsClass: true, IsAbstract: true, IsSealed: true }) // = statische Klasse
                     .OrderBy(t => t.Name, StringComparer.Ordinal))
            if (klasse.GetMethod("Alle", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)?.Invoke(null, null)
                is IEnumerable<(string, Action)> liste)
                tests.AddRange(liste.Select(t => new Testfall(klasse.Name, t.Item1, t.Item2)));
        return tests;
    }

    /// <summary>Führt die ausgewählten Tests aus und schreibt die Zusammenfassung. Rückgabe: Exit-Code.</summary>
    public static int Ausfuehren(IReadOnlyList<Testfall> alle, Testauswahl auswahl, TextWriter aus)
    {
        if (auswahl.Fehler is not null)
        {
            aus.WriteLine(auswahl.Fehler);
            aus.WriteLine();
            aus.WriteLine(Testauswahl.Hilfetext);
            return 2;
        }
        if (auswahl.Hilfe)
        {
            aus.WriteLine(Testauswahl.Hilfetext);
            return 0;
        }

        var passend = alle.Where(auswahl.Passt).ToList();
        // Nicht mit Except: das arbeitet wie eine Menge und würde zwei gleiche Testfälle zu einem machen.
        var laufen = passend.Where(t => !(auswahl.OhneLangsam && LangsameTests.IstLangsam(t))).ToList();
        var langsam = passend.Count - laufen.Count;
        var uebersprungen = alle.Count - laufen.Count;
        var gefiltert = auswahl.Filter.Count > 0 || auswahl.Klassen.Count > 0 || auswahl.OhneLangsam;

        if (auswahl.NurListe)
        {
            foreach (var test in auswahl.OhneLangsam ? laufen : passend)
                aus.WriteLine($"  {(LangsameTests.IstLangsam(test) ? "langsam" : "       ")} {test.Klasse}: {test.Name}");
            aus.WriteLine();
            aus.WriteLine($"{laufen.Count} von {alle.Count} Tests ausgewählt"
                          + (langsam > 0 ? $", {langsam} langsame übersprungen." : "."));
            return laufen.Count > 0 ? 0 : 1;
        }
        if (laufen.Count == 0)
        {
            aus.WriteLine($"Kein Test passt zur Auswahl (von {alle.Count} Tests). Schalter prüfen: --liste zeigt die Auswahl.");
            return 1;
        }

        var fehler = 0;
        var dauer = new List<(Testfall Test, double Sekunden)>();
        Parallellauf.Ergebnis? parallel = null;
        var uhr = Stopwatch.StartNew();
        if (auswahl.Parallel > 0)
        {
            parallel = Parallellauf.Ausfuehren(laufen, auswahl.Parallel, aus);
            fehler = parallel.Fehler;
            dauer = parallel.Dauer;
        }
        else
            foreach (var test in laufen)
            {
                var start = uhr.Elapsed.TotalSeconds;
                if (!EinTest(test, aus))
                    fehler++;
                dauer.Add((test, uhr.Elapsed.TotalSeconds - start));
            }
        uhr.Stop();

        aus.WriteLine();
        aus.WriteLine(fehler == 0
            ? $"Alle {laufen.Count} Tests bestanden."
            : $"{fehler} von {laufen.Count} Tests fehlgeschlagen.");
        aus.WriteLine($"Gelaufen: {laufen.Count}, übersprungen: {uebersprungen}"
                      + (langsam > 0 ? $" (davon {langsam} langsam)" : "")
                      + $", Laufzeit: {Zeit(uhr.Elapsed.TotalSeconds)}"
                      + (gefiltert ? " – nur eine Auswahl, vor dem PR einmal alle Tests laufen lassen." : ".")
                      + (parallel is null ? "" : $" Parallel mit {auswahl.Parallel} Fäden, Rechenzeit zusammen {Zeit(dauer.Sum(d => d.Sekunden))}."));

        if (auswahl.Zeiten > 0)
        {
            aus.WriteLine();
            aus.WriteLine($"Die {Math.Min(auswahl.Zeiten, dauer.Count)} langsamsten Tests:");
            foreach (var (test, sekunden) in dauer.OrderByDescending(d => d.Sekunden).Take(auswahl.Zeiten))
                aus.WriteLine($"  {Zahlenformat.Kommazahl(sekunden),7} s  {(LangsameTests.IstLangsam(test) ? "langsam" : "       ")}  {test.Klasse}: {test.Name}");
            if (parallel is not null)
            {
                // Parallel zählt die Klasse als Ganzes: die längste bestimmt, wie früh der Lauf fertig sein kann.
                aus.WriteLine();
                aus.WriteLine($"Die {Math.Min(auswahl.Zeiten, parallel.Klassen.Count)} langsamsten Klassen (unter Last gemessen):");
                foreach (var (klasse, sekunden, seriell) in parallel.Klassen.OrderByDescending(k => k.Sekunden).Take(auswahl.Zeiten))
                    aus.WriteLine($"  {Zahlenformat.Kommazahl(sekunden),7} s  {(seriell ? "allein " : "       ")}  {klasse}");
            }
        }
        return fehler == 0 ? 0 : 1;
    }

    /// <summary>Führt einen Test aus und schreibt OK oder FEHLER mit Meldung. Rückgabe: bestanden?</summary>
    public static bool EinTest(Testfall test, TextWriter aus)
    {
        try
        {
            test.Pruefung();
            aus.WriteLine($"  OK      {test.Name}");
            return true;
        }
        catch (Exception ex)
        {
            aus.WriteLine($"  FEHLER  {test.Name}");
            aus.WriteLine($"          → {ex.Message}");
            return false;
        }
    }

    static string Zeit(double sekunden) =>
        sekunden < 60
            ? $"{Zahlenformat.Kommazahl(sekunden)} s"
            : $"{(int)(sekunden / 60)} min {(int)(sekunden % 60)} s";
}
