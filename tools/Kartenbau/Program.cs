using System.Globalization;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Kartenbau;

// ═════════════════════════════════════════════════════════════════════════════
//  Kartenskript von Reichsgilde (Bereich Leveldesign) – Stadtkarten per Code anlegen, prüfen und als Vorschaubild zeichnen
//
//  Aufruf (Ordner reichsgilde):
//    dotnet run --project tools/Kartenbau -- pruefen <karte|datei>               alle Prüfungen (Regeln, Kapazität, Katalog gegen die Modelle)
//    dotnet run --project tools/Kartenbau -- vorschau <karte|datei> [--belegung]  docs/karten/<kennung>.svg (und -belegung.svg) zeichnen
//    dotnet run --project tools/Kartenbau -- bauen <skript> [--ueberschreiben]    ein Stadtskript ausführen → game/karten/<kennung>.json
//    dotnet run --project tools/Kartenbau -- liste                                die Stadtskripte
//  <karte> ist die Kennung einer Karte in game/karten/ (greybox, koeln …), <datei> ein Pfad zu einer .json-Datei.
//  Rückgabe: 0 = in Ordnung, 1 = die Karte hat Fehler, 2 = falscher Aufruf (oder die Datei gibt es schon).
//
//  Die Prüfungen sind dieselben wie in der Kartenwerkstatt (Kartenpruefung des Kerns), dazu die Katalogprüfung. Nur Kartenwerkstatt und
//  Kartenskript schreiben Kartendateien; das Skript schreibt nie über eine vorhandene Datei, außer mit --ueberschreiben.
// ═════════════════════════════════════════════════════════════════════════════

public static class Program
{
    public static int Main(string[] args)
    {
        var ordner = Projektordner.Finden();
        if (ordner is null)
        {
            Console.Error.WriteLine("Den Projektordner (mit game/daten/katalog.json) finde ich nicht – bitte im Ordner reichsgilde aufrufen.");
            return 2;
        }
        var schalter = args.Where(a => a.StartsWith("--")).ToHashSet();
        var worte = args.Where(a => !a.StartsWith("--")).ToList();
        var befehl = worte.FirstOrDefault() ?? "hilfe";
        var ziel = worte.Skip(1).FirstOrDefault();
        try
        {
            return befehl switch
            {
                "pruefen" when ziel is not null => Befehle.Pruefen(ordner, ziel, Console.Out),
                "vorschau" when ziel is not null => Befehle.Vorschau(ordner, ziel, schalter.Contains("--belegung"), Console.Out),
                "bauen" when ziel is not null => Befehle.Bauen(ordner, ziel, schalter.Contains("--ueberschreiben"), Console.Out),
                "liste" => Befehle.Liste(Console.Out),
                _ => Hilfe(),
            };
        }
        catch (KartenFehler ex)
        {
            Console.Error.WriteLine("Die Karte ist nicht lesbar:\n  " + string.Join("\n  ", ex.Fehler));
            return 1;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static int Hilfe()
    {
        Console.WriteLine("""
            Kartenskript – Stadtkarten anlegen, prüfen und zeichnen (siehe docs/LEVELDESIGN.md, „Kartenskript“)

              pruefen <karte|datei>               alle Prüfungen: Regeln, Kapazität, Katalog gegen die Modelle
              vorschau <karte|datei> [--belegung]  Vorschaubild docs/karten/<kennung>.svg (mit --belegung auch -belegung.svg)
              bauen <skript> [--ueberschreiben]    Stadtskript ausführen und game/karten/<kennung>.json schreiben
              liste                               die Stadtskripte

            <karte> ist eine Kennung aus game/karten/ (z. B. greybox, koeln), <datei> ein Pfad zu einer .json-Datei.
            """);
        return 2;
    }
}

/// <summary>Die Befehle des Kartenskripts (Ausgabe in einen <see cref="TextWriter"/>, damit die Tests sie aufrufen können).</summary>
public static class Befehle
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Der Gebäudekatalog und die Wirtschaftsdaten des Projekts (einmal je Lauf).</summary>
    private static (Gebaeudekatalog Katalog, Wirtschaftsdaten Daten) Laden(Projektordner o) =>
        (Gebaeudekatalog.AusDatei(o.Katalog), Wirtschaftsdaten.AusDatei(o.Wirtschaft));

    /// <summary>Öffnet eine Karte: Kennung (game/karten/&lt;kennung&gt;.json) oder Pfad.</summary>
    public static Kartenentwurf Oeffnen(Projektordner o, string karte, Gebaeudekatalog katalog, Wirtschaftsdaten daten)
    {
        var pfad = karte.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || karte.Contains('/') || karte.Contains('\\') ? Path.GetFullPath(karte) : o.Karte(karte);
        if (!File.Exists(pfad)) throw new FileNotFoundException($"Die Karte {pfad} gibt es nicht.", pfad);
        var e = Kartenentwurf.AusJson(File.ReadAllText(pfad), katalog);
        e.Daten = daten;
        return e;
    }

    /// <summary><c>pruefen</c>: Regeln und Kapazität der Karte, dazu der Katalog gegen die Modelle. 1, wenn es einen Fehler gibt.</summary>
    public static int Pruefen(Projektordner o, string karte, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        var fehler = Bericht(e, aus);
        var kat = Katalogpruefung.Pruefen(katalog, o.Modelle);
        aus.WriteLine("Katalog gegen die Modelle (game/assets/modelle):");
        foreach (var b in kat.Where(b => b.Schwere == Schwere.Hinweis)) aus.WriteLine($"   Hinweis: {b.Text}");
        foreach (var b in kat.Where(b => b.Schwere == Schwere.Fehler)) aus.WriteLine($"FEHLER: {b.Text}");
        var katFehler = kat.Count(b => b.Schwere == Schwere.Fehler);
        aus.WriteLine($"Katalog: {katFehler} Fehler, {kat.Count - katFehler} Hinweise");
        return fehler + katFehler > 0 ? 1 : 0;
    }

    /// <summary><c>vorschau</c>: zeichnet docs/karten/&lt;kennung&gt;.svg, mit <paramref name="belegung"/> auch &lt;kennung&gt;-belegung.svg.</summary>
    public static int Vorschau(Projektordner o, string karte, bool belegung, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        foreach (var pfad in VorschauSchreiben(e, o.Vorschauen, belegung)) aus.WriteLine($"   geschrieben: {Relativ(o, pfad)}");
        return 0;
    }

    /// <summary>
    /// <c>bauen</c>: führt ein Stadtskript aus, prüft die Karte und schreibt sie nach game/karten/&lt;kennung&gt;.json mit den Vorschaubildern –
    /// nur ohne Fehler und nicht über eine vorhandene Datei (außer mit <paramref name="ueberschreiben"/>).
    /// </summary>
    public static int Bauen(Projektordner o, string skriptname, bool ueberschreiben, TextWriter aus)
    {
        var skript = Stadtskript.Finde(skriptname);
        if (skript is null)
        {
            aus.WriteLine($"Ein Stadtskript „{skriptname}“ gibt es nicht.");
            Liste(aus);
            return 2;
        }
        var (katalog, daten) = Laden(o);
        var e = skript.Bauen(katalog, daten);
        return Schreiben(e, o.Karte(e.Kennung), o.Vorschauen, ueberschreiben, aus, o);
    }

    /// <summary>
    /// Prüft den Entwurf und schreibt ihn nach <paramref name="kartenpfad"/>, dazu die Vorschaubilder nach <paramref name="vorschauordner"/>:
    /// 1 (nichts geschrieben) bei einem Fehler, 2 (nichts geschrieben), wenn es die Datei schon gibt und <paramref name="ueberschreiben"/> fehlt.
    /// </summary>
    public static int Schreiben(Kartenentwurf e, string kartenpfad, string vorschauordner, bool ueberschreiben, TextWriter aus, Projektordner? o = null)
    {
        if (Bericht(e, aus) > 0)
        {
            aus.WriteLine($"{e.Kennung}: Die Karte hat Fehler – nichts geschrieben.");
            return 1;
        }
        if (File.Exists(kartenpfad) && !ueberschreiben)
        {
            aus.WriteLine($"{e.Kennung}: {kartenpfad} gibt es schon – nichts geschrieben. Mit --ueberschreiben ersetzt das Skript sie " +
                          "(Änderungen aus der Kartenwerkstatt gehen dann verloren).");
            return 2;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(kartenpfad)!);
        File.WriteAllText(kartenpfad, e.ZuJson());
        var geschrieben = new List<string> { kartenpfad };
        geschrieben.AddRange(VorschauSchreiben(e, vorschauordner, belegung: true));
        aus.WriteLine("   geschrieben: " + string.Join(", ", geschrieben.Select(p => o is null ? p : Relativ(o, p))));
        return 0;
    }

    /// <summary><c>liste</c>: die Stadtskripte mit Kennung und Beschreibung.</summary>
    public static int Liste(TextWriter aus)
    {
        aus.WriteLine("Stadtskripte (tools/Kartenbau/Staedte):");
        foreach (var s in Stadtskript.Alle()) aus.WriteLine($"   {s.Kennung,-14} {s.Stadt} – {s.Beschreibung}");
        return 0;
    }

    /// <summary>Schreibt die Vorschaubilder und gibt ihre Pfade zurück.</summary>
    public static List<string> VorschauSchreiben(Kartenentwurf e, string ordner, bool belegung)
    {
        Directory.CreateDirectory(ordner);
        var liste = new List<string> { Path.Combine(ordner, e.Kennung + ".svg") };
        File.WriteAllText(liste[0], Kartenbau.Vorschau.Zeichnen(e));
        if (belegung)
        {
            liste.Add(Path.Combine(ordner, e.Kennung + "-belegung.svg"));
            File.WriteAllText(liste[1], Kartenbau.Vorschau.Zeichnen(e, belegung: true));
        }
        return liste;
    }

    /// <summary>
    /// Schreibt Befunde und Übersicht der Karte wie das alte Werkzeug: Hinweise, Wegenetz, Gebäude, Kapazität bzw. Probeplan, Bauzonen, zuletzt
    /// die Fehler. Gibt die Zahl der Fehler zurück.
    /// </summary>
    public static int Bericht(Kartenentwurf e, TextWriter aus)
    {
        var befunde = Kartenpruefung.Pruefen(e);
        aus.WriteLine($"{e.Stadt} ({e.Kennung}):");
        foreach (var b in befunde.Where(b => b.Schwere == Schwere.Hinweis)) aus.WriteLine($"   Hinweis: {b.Text}");
        aus.WriteLine($"   Wegenetz: {e.Wege.Count} Wege" + (befunde.Any(b => b.Regel == "wegenetz") ? "" : ", 1 zusammenhängender Teil"));
        aus.WriteLine("   Gebäude: " + string.Join(", ", e.Gebaeude.GroupBy(g => g.Art == "" ? "?" : g.Art).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Count()} {g.Key}")));
        if (e.Regeln.Kapazitaet is not null)
            foreach (var zeile in e.Kapazitaetsrechnung().Bericht) aus.WriteLine($"   {zeile}");
        if (e.Probeplan.Count > 0)
        {
            foreach (var z in e.Bauzonen)
            {
                var plan = string.Join(", ", e.Probeplan.Where(p => p.Zone == z.Kennung).GroupBy(p => p.Art).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Count()} {g.Key}"));
                aus.WriteLine($"   Zone {z.Kennung} ({z.Art.ToString().ToLowerInvariant()}, {Felder(z, e.Raster) * e.Raster * e.Raster:0} m²): Probeplan {(plan == "" ? "–" : plan)}");
            }
            int Zahl(string art) => e.Probeplan.Count(p => p.Art == art);
            aus.WriteLine($"   Probeplan zusammen: {Zahl("Werkstatt") + Zahl("Stadthof")} Plätze zu 16 × 14 m (davon {Zahl("Stadthof")} Stadthof in Kern oder Mauerring) " +
                          $"und {Zahl("Wohnhaus")} Wohnhäuser zu 8 × 10 m");
        }
        foreach (var g in e.Bauzonen.GroupBy(z => z.Art).OrderBy(g => g.Key.ToString().ToLowerInvariant(), StringComparer.Ordinal))
        {
            var flaeche = g.Sum(z => Flaeche(z.Ecken));
            aus.WriteLine($"   Bauzonen {g.Key.ToString().ToLowerInvariant()}: {g.Count()}, {flaeche.ToString("N0", Deutsch)} m², " +
                          $"{((int)(flaeche / (e.Raster * e.Raster))).ToString("N0", Deutsch)} Rasterfelder");
        }
        var fehler = befunde.Where(b => b.Schwere == Schwere.Fehler).ToList();
        foreach (var b in fehler) aus.WriteLine($"FEHLER: {b.Text}");
        aus.WriteLine($"{e.Kennung}: {fehler.Count} Fehler, {befunde.Count - fehler.Count} Hinweise");
        return fehler.Count;
    }

    /// <summary>Fläche eines Vielecks in m² (Gaußsche Trapezformel).</summary>
    private static double Flaeche(IReadOnlyList<Kartenort> p) =>
        Math.Abs(Enumerable.Range(0, p.Count).Sum(i => p[i].X * p[(i + 1) % p.Count].Z - p[(i + 1) % p.Count].X * p[i].Z)) / 2;

    /// <summary>Wie viele Rasterfelder ihre Mitte in der Zone haben.</summary>
    private static int Felder(EntwurfBauzone z, double r)
    {
        var p = z.Ecken;
        if (p.Count < 3) return 0;
        var n = 0;
        for (var i = (int)Math.Floor(p.Min(q => q.X) / r); i <= (int)Math.Floor(p.Max(q => q.X) / r); i++)
            for (var j = (int)Math.Floor(p.Min(q => q.Z) / r); j <= (int)Math.Floor(p.Max(q => q.Z) / r); j++)
                if (Kartengeometrie.ImVieleck(new Kartenort((i + 0.5) * r, (j + 0.5) * r), p)) n++;
        return n;
    }

    private static string Relativ(Projektordner o, string pfad) => Path.GetRelativePath(o.Wurzel, pfad).Replace('\\', '/');
}
