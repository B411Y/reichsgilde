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
//                                                   mit --entwurf=<datei> stattdessen nur diesen Entwurf schreiben (auch mit Fehlern, zum Ansehen)
//    dotnet run --project tools/Kartenbau -- liste                                die Stadtskripte
//    dotnet run --project tools/Kartenbau -- groesse <karte> [<breite> <tiefe> [<mitteX> <mitteZ>]] [--rand=<m>]
//                                                   Größe des Spielfelds (ohne Zahlen: aus dem Inhalt abgeleitet) und Randbreite in die Karte schreiben
//    dotnet run --project tools/Kartenbau -- randvorschlag <karte> [<startwert>]   Felder und Waldstücke im Randstreifen vorschlagen (ersetzt einen früheren)
//    dotnet run --project tools/Kartenbau -- gelaende <karte> <pinsel> <x,z;x,z…> [--radius= --staerke= --kante= --startwert= --massstab=]
//                                                   [--ziel= --stufe= --boeschung= --ausgleich --steigung=<ab>-<bis> --bodenfilter=<textur>[:<rand>]]
//                                                   ein Strich mit einem Geländepinsel (heben, senken, glaetten, ebnen, rauschen, terrassen)
//    dotnet run --project tools/Kartenbau -- textur <karte> <textur|radierer> <x,z;x,z…> [--radius= --staerke= --kante= --deko=an|aus --dichte=]
//                                                   [--steigung=<ab>-<bis> --bodenfilter=<textur>[:<rand>]]
//                                                   ein Strich mit dem Texturpinsel (wiese, erde, fels, kies, waldboden, sand, schlamm, moos, lehm, pflaster, mist)
//    dotnet run --project tools/Kartenbau -- rampe <karte> <x,z> <x,z> <breite> [--kante=]   eine Rampe mit gleichmäßigem Gefälle
//    dotnet run --project tools/Kartenbau -- hoehenkarte-einlesen <karte> <datei.r16> --pixel=<m> --ursprung=<x,z> --unten=<m> --oben=<m>
//                                                   --quelle=<…> --lizenz=<…> [--abruf=<datum>] [--breite=<px>] [--versatz=<m>] [--stufe=<cm>] [--bytefolge=be]
//                                                   eine Höhenkarte (r16/raw) ins Raster einlesen, Herkunft nach tools/Kartenbau/Staedte/HOEHENDATEN.md
//    dotnet run --project tools/Kartenbau -- hoehenkarte-ausgeben <karte> <datei.r16> [--unten=<m>] [--oben=<m>] [--bytefolge=be]
//                                                   das Raster der Karte als Höhenkarte (2 m je Pixel) ausgeben
//    dotnet run --project tools/Kartenbau -- fluss <karte> <x,z[,breite[,tiefe]];…> [--art=bach] [--name=] [--breite=] [--tiefe=] [--stroemung=]
//                                                   einen Fluss oder Bach in Fließrichtung anlegen
//    dotnet run --project tools/Kartenbau -- see <karte> <x,z;…> [--art=teich] [--name=] [--wasserstand=] [--tiefe=]   einen See oder Teich
//    dotnet run --project tools/Kartenbau -- faehre <karte> <x,z> <x,z> [--name=]   eine Fährstelle (zwei Anleger, rasten an Wegen ein)
//    dotnet run --project tools/Kartenbau -- hafen <karte> <x,z> <drehung> [--name=]   einen Hafenplatz (Rückseite zum Wasser)
//    dotnet run --project tools/Kartenbau -- belag <karte> <weg> <stufe> [<von> <bis>]   Startbelag eines Weges (ganz oder von–bis Meter)
//    dotnet run --project tools/Kartenbau -- umbauen <umbau>                     ein Umbauskript (Umbauten/) auf seine Karte anwenden
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
                "bauen" when ziel is not null => Befehle.Bauen(ordner, ziel, schalter.Contains("--ueberschreiben"), Console.Out,
                    args.FirstOrDefault(a => a.StartsWith("--entwurf="))?["--entwurf=".Length..]),
                "liste" => Befehle.Liste(Console.Out),
                "groesse" when ziel is not null && Zahlen(worte.Skip(2)) is { Count: 0 or 2 or 4 } z =>
                    Befehle.Groesse(ordner, ziel, z, Wert(args, "--rand"), Console.Out),
                "randvorschlag" when ziel is not null && Zahlen(worte.Skip(2)) is { Count: 0 or 1 } z => Befehle.Randvorschlag(ordner, ziel, z.Count == 1 ? (int)z[0] : null, Console.Out),
                "gelaende" when ziel is not null && worte.Count == 4 && PinselVon(worte[2], args) is { } pinsel && Orte(worte[3]) is { Count: > 0 } strich =>
                    Befehle.Gelaende(ordner, ziel, pinsel, strich, Console.Out),
                "textur" when ziel is not null && worte.Count == 4 && BodenpinselVon(worte[2], args) is { } bodenpinsel && Orte(worte[3]) is { Count: > 0 } bodenstrich =>
                    Befehle.Textur(ordner, ziel, bodenpinsel, bodenstrich, Console.Out),
                "hoehenkarte-einlesen" when ziel is not null && worte.Count == 3 =>
                    Befehle.HoehenkarteEinlesen(ordner, ziel, worte[2], Wert(args, "--pixel"), Orte(Text(args, "--ursprung") ?? "") is { Count: 1 } u ? u[0] : null,
                        Wert(args, "--unten"), Wert(args, "--oben"), (int?)Wert(args, "--breite") ?? 0, Wert(args, "--versatz") ?? 0, (int?)Wert(args, "--stufe") ?? 5,
                        Text(args, "--bytefolge") == "be", new Hoehendatenherkunft(Text(args, "--quelle"), Text(args, "--lizenz"), Text(args, "--abruf")), Console.Out),
                "hoehenkarte-ausgeben" when ziel is not null && worte.Count == 3 =>
                    Befehle.HoehenkarteAusgeben(ordner, ziel, worte[2], Wert(args, "--unten"), Wert(args, "--oben"), Text(args, "--bytefolge") == "be", Console.Out),
                "rampe" when ziel is not null && worte.Count == 5 && Orte(worte[2]) is { Count: 1 } von && Orte(worte[3]) is { Count: 1 } nach
                             && Zahlen(worte.Skip(4)) is { Count: 1 } breite =>
                    Befehle.Rampe(ordner, ziel, von[0], nach[0], breite[0], Wert(args, "--kante") ?? 4, Console.Out),
                "fluss" when ziel is not null && worte.Count == 3 && Stuetzpunkte(worte[2]) is { Count: >= 2 } punkte =>
                    Befehle.Fluss(ordner, ziel, punkte, Text(args, "--art") ?? "fluss", Text(args, "--name"), Wert(args, "--breite"), Wert(args, "--tiefe"),
                        Wert(args, "--stroemung"), Console.Out),
                "see" when ziel is not null && worte.Count == 3 && Orte(worte[2]) is { Count: >= 3 } ecken =>
                    Befehle.See(ordner, ziel, ecken, Text(args, "--art") ?? "see", Text(args, "--name"), Wert(args, "--wasserstand"), Wert(args, "--tiefe"), Console.Out),
                "faehre" when ziel is not null && worte.Count == 4 && Orte(worte[2]) is { Count: 1 } a && Orte(worte[3]) is { Count: 1 } b =>
                    Befehle.Faehre(ordner, ziel, a[0], b[0], Text(args, "--name"), Console.Out),
                "hafen" when ziel is not null && worte.Count == 4 && Orte(worte[2]) is { Count: 1 } ort && Zahlen(worte.Skip(3)) is { Count: 1 } drehung =>
                    Befehle.Hafen(ordner, ziel, ort[0], drehung[0], Text(args, "--name"), Console.Out),
                "belag" when ziel is not null && worte.Count is 4 or 6 && Zahlen(worte.Skip(4)) is { Count: 0 or 2 } bereich =>
                    Befehle.Belag(ordner, ziel, worte[2], worte[3], bereich.Count == 2 ? (bereich[0], bereich[1]) : null, Console.Out),
                "umbauen" when ziel is not null => Befehle.Umbauen(ordner, ziel, Console.Out),
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

    /// <summary>Die Zahlen unter den Worten (Punkt als Dezimaltrenner) – null, wenn ein Wort keine Zahl ist.</summary>
    private static List<double>? Zahlen(IEnumerable<string> worte)
    {
        var liste = new List<double>();
        foreach (var w in worte)
        {
            if (!double.TryParse(w, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) return null;
            liste.Add(x);
        }
        return liste;
    }

    /// <summary>Orte „x,z;x,z;…“ (Punkt als Dezimaltrenner) – null, wenn einer nicht lesbar ist.</summary>
    private static List<Kartenort>? Orte(string text)
    {
        var liste = new List<Kartenort>();
        foreach (var teil in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Zahlen(teil.Split(',')) is not { Count: 2 } z) return null;
            liste.Add(new Kartenort(z[0], z[1]));
        }
        return liste;
    }

    /// <summary>Stützpunkte eines Flusses „x,z[,breite[,tiefe]];…“ – null, wenn einer nicht lesbar ist.</summary>
    private static List<(Kartenort Ort, double? Breite, double? Tiefe)>? Stuetzpunkte(string text)
    {
        var liste = new List<(Kartenort, double?, double?)>();
        foreach (var teil in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Zahlen(teil.Split(',')) is not { Count: >= 2 and <= 4 } z) return null;
            liste.Add((new Kartenort(z[0], z[1]), z.Count >= 3 ? z[2] : null, z.Count >= 4 ? z[3] : null));
        }
        return liste;
    }

    /// <summary>Der Text eines Schalters „--name=text“ – oder null.</summary>
    private static string? Text(IEnumerable<string> args, string name) =>
        args.FirstOrDefault(a => a.StartsWith(name + "=", StringComparison.Ordinal)) is { } a ? a[(name.Length + 1)..] : null;

    /// <summary>
    /// Der Pinsel aus seinem Namen (heben, senken, glaetten, ebnen, rauschen, terrassen) und den Schaltern – null bei unbekanntem Namen oder
    /// unlesbarem Filter. Pinsel-Paket 1: --ziel (Ebnen: Zielhöhe; Terrassen: Bezugshöhe), --stufe, --boeschung, --ausgleich (nur Mulden / nur
    /// Spitzen) und der Filter (<see cref="FilterVon"/>).
    /// </summary>
    private static Pinsel? PinselVon(string name, IEnumerable<string> args)
    {
        if (!Enum.TryParse<Pinselart>(name, ignoreCase: true, out var art) || int.TryParse(name, out _)) return null;
        if (!FilterVon(args, out var filter)) return null;
        var p = new Pinsel(art);
        return p with
        {
            Radius = Wert(args, "--radius") ?? p.Radius,
            Staerke = Wert(args, "--staerke") ?? (p.StaerkeAlsAnteil ? 1 : p.Staerke),
            Kante = Wert(args, "--kante") ?? p.Kante,
            Startwert = (int)(Wert(args, "--startwert") ?? p.Startwert),
            Massstab = Wert(args, "--massstab") ?? p.Massstab,
            Zielhoehe = Wert(args, "--ziel"),
            Stufenhoehe = Wert(args, "--stufe") ?? p.Stufenhoehe,
            Boeschung = Wert(args, "--boeschung") ?? p.Boeschung,
            NurAusgleich = args.Contains("--ausgleich"),
            Filter = filter,
        };
    }

    /// <summary>
    /// Der Filter eines Pinsels aus --steigung=&lt;ab&gt;-&lt;bis&gt; (Prozent, eine Seite darf fehlen: „15-“, „-8“) und --bodenfilter=&lt;textur&gt;[:&lt;rand 0–1&gt;].
    /// False, wenn eine Angabe nicht lesbar ist; ohne Angaben ist der Filter null.
    /// </summary>
    private static bool FilterVon(IEnumerable<string> args, out Pinselfilter? filter)
    {
        filter = null;
        var f = Pinselfilter.Keiner;
        if (Text(args, "--steigung") is { } steigung)
        {
            var g = steigung.Split('-', 2);
            if (g.Length != 2 || Zahlen(g.Where(t => t.Length > 0)) is not { } z || z.Count == 0) return false;
            f = f with
            {
                SteigungAb = g[0].Length > 0 ? z[0] : 0,
                SteigungBis = g[1].Length > 0 ? z[^1] : null,
            };
        }
        if (Text(args, "--bodenfilter") is { } boden)
        {
            var t = boden.Split(':');
            if (Bodenraster.TexturVon(t[0]) is not { } textur) return false;
            double? rand = t.Length > 1 ? Zahlen(t.Skip(1)) is { Count: 1 } r ? r[0] : null : 0;
            if (rand is null) return false;
            f = f with { Boden = textur, BodenRand = rand.Value };
        }
        filter = f.IstLeer ? null : f;
        return true;
    }

    /// <summary>Der Texturpinsel aus dem Namen der Textur (oder „radierer“) und den Schaltern – null bei unbekanntem Namen oder --deko.</summary>
    private static Bodenpinsel? BodenpinselVon(string name, IEnumerable<string> args)
    {
        var radierer = name.Equals("radierer", StringComparison.OrdinalIgnoreCase);
        if (!radierer && Bodenraster.TexturVon(name) is null) return null;
        var p = radierer ? Bodenpinsel.Radierer() : new Bodenpinsel(Bodenraster.TexturVon(name)!.Value);
        var deko = Text(args, "--deko");
        if (deko is not (null or "an" or "aus")) return null;
        if (!FilterVon(args, out var filter)) return null;
        return p with
        {
            Radius = Wert(args, "--radius") ?? p.Radius,
            Staerke = Wert(args, "--staerke") ?? p.Staerke,
            Kante = Wert(args, "--kante") ?? p.Kante,
            Deko = !radierer && deko != "aus",
            Dichte = Wert(args, "--dichte") ?? p.Dichte,
            Filter = filter,
        };
    }

    /// <summary>Der Wert eines Schalters „--name=zahl“ – oder null.</summary>
    private static double? Wert(IEnumerable<string> args, string name) =>
        args.FirstOrDefault(a => a.StartsWith(name + "=", StringComparison.Ordinal)) is { } a
        && double.TryParse(a[(name.Length + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : null;

    private static int Hilfe()
    {
        Console.WriteLine("""
            Kartenskript – Stadtkarten anlegen, prüfen und zeichnen (siehe docs/LEVELDESIGN.md, „Kartenskript“)

              pruefen <karte|datei>               alle Prüfungen: Regeln, Kapazität, Katalog gegen die Modelle
              vorschau <karte|datei> [--belegung]  Vorschaubild docs/karten/<kennung>.svg (mit --belegung auch -belegung.svg)
              bauen <skript> [--ueberschreiben]    Stadtskript ausführen und game/karten/<kennung>.json schreiben
              liste                               die Stadtskripte
              groesse <karte> [<breite> <tiefe> [<mitteX> <mitteZ>]] [--rand=<m>]
                                                  Größe des Spielfelds (ohne Zahlen: aus dem Inhalt abgeleitet) und Randbreite schreiben
              randvorschlag <karte> [<startwert>]  Felder und Waldstücke im Randstreifen vorschlagen (ersetzt einen früheren Vorschlag)
              gelaende <karte> <pinsel> <x,z;x,z…> [--radius=10] [--staerke=…] [--kante=0.5] [--startwert=1] [--massstab=20]
                       [--ziel=<m>] [--stufe=2] [--boeschung=0.3] [--ausgleich] [--steigung=<ab>-<bis>] [--bodenfilter=<textur>[:<rand>]]
                                                  ein Strich mit einem Geländepinsel: heben, senken (Stärke in m je Tupfer, Vorgabe 0.25;
                                                  --ausgleich: nur Mulden heben bzw. nur Spitzen kappen), glaetten, ebnen (Stärke 0–1, Vorgabe 1;
                                                  --ziel: auf diese Höhe statt der unter dem ersten Punkt), rauschen (Stärke in m, Maßstab =
                                                  Buckelgröße in m), terrassen (Stufen von --stufe m bei --ziel + Vielfachen, --boeschung 0–1);
                                                  Filter: nur bei --steigung (Prozent, z. B. 15- oder -8) bzw. nur auf --bodenfilter (Rand 0–1)
              textur <karte> <textur|radierer> <x,z;x,z…> [--radius=5] [--staerke=1] [--kante=0.5] [--deko=an|aus] [--dichte=1]
                       [--steigung=<ab>-<bis>] [--bodenfilter=<textur>[:<rand>]]
                                                  ein Strich mit dem Texturpinsel: wiese, erde, fels, kies, waldboden, sand, schlamm, moos,
                                                  lehm, pflaster, mist (Stärke 0–1; Deko streuen mit Dichte 0–2) oder radierer (zurück zur Wiese)
              rampe <karte> <x,z> <x,z> <breite> [--kante=4]
                                                  ein Band mit gleichmäßigem Gefälle von der Höhe am ersten zur Höhe am zweiten Punkt
              hoehenkarte-einlesen <karte> <datei.r16> --pixel=<m> --ursprung=<x,z> --unten=<m> --oben=<m> --quelle=<…> --lizenz=<…>
                       [--abruf=<datum>] [--breite=<px>] [--versatz=<m>] [--stufe=5] [--bytefolge=be]
                                                  eine Höhenkarte (r16/raw, 16 Bit je Pixel, ohne Breite quadratisch) einlesen: das erste Pixel
                                                  liegt am Ursprung, Wert 0 hat die Höhe „unten“, 65535 „oben“, dazu der Versatz; neu auf 2 m
                                                  abgetastet, auf die Stufe (cm) gerundet, auf −20 … +60 m begrenzt (mit Warnung); Quelle und
                                                  Lizenz sind Pflicht und kommen nach tools/Kartenbau/Staedte/HOEHENDATEN.md
              hoehenkarte-ausgeben <karte> <datei.r16> [--unten=<m>] [--oben=<m>] [--bytefolge=be]
                                                  das Raster der ganzen Karte als Höhenkarte (2 m je Pixel; ohne unten/oben die tiefste und
                                                  höchste Höhe) – nennt die Angaben, mit denen sie wieder eingelesen wird
              fluss <karte> <x,z[,breite[,tiefe]];…> [--art=bach] [--name=…] [--breite=…] [--tiefe=…] [--stroemung=…]
                                                  einen Fluss (oder Bach) in Fließrichtung anlegen; Breite und Tiefe je Punkt oder für alle
              see <karte> <x,z;…> [--art=teich] [--name=…] [--wasserstand=…] [--tiefe=…]
                                                  einen See (oder Teich); ohne Wasserstand 0,4 m unter dem tiefsten Ufer
              faehre <karte> <x,z> <x,z> [--name=…]   eine Fährstelle: zwei Anleger an beiden Ufern (rasten an Wegen ein)
              hafen <karte> <x,z> <drehung> [--name=…]
                                                  einen Hafenplatz setzen – die Rückseite (Kai) zum Wasser, Drehung wie im Spiel
              belag <karte> <weg> <stufe> [<von> <bis>]
                                                  Startbelag eines Weges: trampelpfad, fahrweg, kies, pflaster oder vorgabe (nach der Klasse);
                                                  <weg> ist der Name (Leerzeichen als _) oder die Nummer, von–bis in Metern entlang des Weges
              umbauen <umbau>                     ein Umbauskript (tools/Kartenbau/Umbauten) auf seine Karte anwenden

            <karte> ist eine Kennung aus game/karten/ (z. B. greybox, koeln), <datei> ein Pfad zu einer .json-Datei.
            """);
        return 2;
    }
}

/// <summary>Die Befehle des Kartenskripts (Ausgabe in einen <see cref="TextWriter"/>, damit die Tests sie aufrufen können).</summary>
public static class Befehle
{
    /// <summary>Ganze Zahl mit Tausenderpunkt („1.072“) – ohne Kulturdaten, die nicht überall installiert sind.</summary>
    private static string Tausend(double x) => x.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');

    /// <summary>Der Gebäudekatalog und die Wirtschaftsdaten des Projekts (einmal je Lauf).</summary>
    private static (Gebaeudekatalog Katalog, Wirtschaftsdaten Daten) Laden(Projektordner o) =>
        (Gebaeudekatalog.AusDatei(o.Katalog), Wirtschaftsdaten.AusDatei(o.Wirtschaft));

    /// <summary>Öffnet eine Karte: Kennung (game/karten/&lt;kennung&gt;.json) oder Pfad.</summary>
    public static Kartenentwurf Oeffnen(Projektordner o, string karte, Gebaeudekatalog katalog, Wirtschaftsdaten daten)
    {
        var pfad = PfadVon(o, karte);
        if (!File.Exists(pfad)) throw new FileNotFoundException($"Die Karte {pfad} gibt es nicht.", pfad);
        var e = Kartenentwurf.AusJson(File.ReadAllText(pfad), katalog);
        e.Daten = daten;
        return e;
    }

    /// <summary>Der Pfad zu einer Karte: Kennung (game/karten/&lt;kennung&gt;.json) oder Pfad zu einer .json-Datei.</summary>
    private static string PfadVon(Projektordner o, string karte) =>
        karte.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || karte.Contains('/') || karte.Contains('\\') ? Path.GetFullPath(karte) : o.Karte(karte);

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
    public static int Bauen(Projektordner o, string skriptname, bool ueberschreiben, TextWriter aus, string? entwurfPfad = null)
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
        if (entwurfPfad is not null) // Entwurf zum Ansehen, auch mit Fehlern: nur diese Datei, nie die Karte
        {
            File.WriteAllText(entwurfPfad, e.ZuJson());
            aus.WriteLine($"   Entwurf geschrieben: {entwurfPfad}");
            return Bericht(e, aus) > 0 ? 1 : 0;
        }
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

    /// <summary>
    /// <c>groesse</c>: schreibt Größe und Mitte des Spielfelds (<paramref name="zahlen"/>: keine = so, wie die Karte sie hat – bei einer Karte ohne
    /// Größe aus dem Inhalt abgeleitet; Breite und Tiefe; dazu die Mitte) und die Randbreite (ohne Angabe die der Karte) in die Kartendatei,
    /// mit Vorschaubildern – nur ohne Fehler. Alles Übrige der Datei bleibt, wie es ist.
    /// </summary>
    public static int Groesse(Projektordner o, string karte, IReadOnlyList<double> zahlen, double? rand, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        var k = e.Kopf;
        var (b, t) = zahlen.Count >= 2 ? (zahlen[0], zahlen[1]) : (k.Breite, k.Tiefe);
        e.GroesseSetzen(b, t, zahlen.Count == 4 ? new Kartenort(zahlen[2], zahlen[3]) : k.Mitte);
        e.RandbreiteSetzen(rand ?? k.Randbreite);
        var g = e.Kartengrenzen;
        aus.WriteLine($"{e.Kennung}: Spielfeld {Kartenschreiber.Zahl(g.Breite)} × {Kartenschreiber.Zahl(g.Tiefe)} m um {g.Mitte}, Rand {Kartenschreiber.Zahl(g.Randbreite)} m");
        return Schreiben(e, PfadVon(o, karte), o.Vorschauen, true, aus, o);
    }

    /// <summary><c>randvorschlag</c>: schlägt Felder und Waldstücke im Randstreifen vor (<see cref="Kartenentwurf.RandVorschlagen"/>) und schreibt die Karte – nur ohne Fehler.</summary>
    public static int Randvorschlag(Projektordner o, string karte, int? startwert, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        var neu = e.RandVorschlagen(startwert);
        aus.WriteLine($"{e.Kennung}: {neu.Count(f => f is EntwurfFeld)} Felder, {neu.Count(f => f is EntwurfNaturflaeche)} Wald- und Streuflächen im Rand vorgeschlagen");
        return Schreiben(e, PfadVon(o, karte), o.Vorschauen, true, aus, o);
    }

    /// <summary><c>gelaende</c>: ein Strich mit einem Geländepinsel (<see cref="Kartenentwurf.GelaendePinseln"/>), dann schreiben – nur ohne Fehler.</summary>
    public static int Gelaende(Projektordner o, string karte, Pinsel pinsel, IReadOnlyList<Kartenort> strich, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        if (!e.GelaendePinseln(pinsel, strich))
        {
            aus.WriteLine($"{e.Kennung}: Der Strich ändert nichts (außerhalb der Karte?) – nichts geschrieben.");
            return 0;
        }
        aus.WriteLine($"{e.Kennung}: {e.Geschichte.RueckgaengigText} ({strich.Count} Punkte, Radius {Kartenschreiber.Zahl(pinsel.Radius)} m)");
        return Schreiben(e, PfadVon(o, karte), o.Vorschauen, true, aus, o);
    }

    /// <summary><c>textur</c>: ein Strich mit dem Texturpinsel (<see cref="Kartenentwurf.BodenMalen"/>), dann schreiben – nur ohne Fehler.</summary>
    public static int Textur(Projektordner o, string karte, Bodenpinsel pinsel, IReadOnlyList<Kartenort> strich, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        if (!e.BodenMalen(pinsel, strich))
        {
            aus.WriteLine($"{e.Kennung}: Der Strich ändert nichts (außerhalb der Karte?) – nichts geschrieben.");
            return 0;
        }
        aus.WriteLine($"{e.Kennung}: {e.Geschichte.RueckgaengigText} ({strich.Count} Punkte, Radius {Kartenschreiber.Zahl(pinsel.Radius)} m)");
        return Schreiben(e, PfadVon(o, karte), o.Vorschauen, true, aus, o);
    }

    /// <summary><c>rampe</c>: eine Rampe (<see cref="Kartenentwurf.Rampe"/>), dann schreiben – nur ohne Fehler.</summary>
    public static int Rampe(Projektordner o, string karte, Kartenort von, Kartenort nach, double breite, double kante, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        if (!e.Rampe(von, nach, breite, kante))
        {
            aus.WriteLine($"{e.Kennung}: Die Rampe ändert nichts – nichts geschrieben.");
            return 0;
        }
        aus.WriteLine($"{e.Kennung}: Rampe von {von} nach {nach}, {Kartenschreiber.Zahl(breite)} m breit");
        return Schreiben(e, PfadVon(o, karte), o.Vorschauen, true, aus, o);
    }

    /// <summary>Zentimeter aus Metern (gerundet halb weg von 0).</summary>
    private static int Cm(double meter) => (int)Math.Round(meter * 100, MidpointRounding.AwayFromZero);

    /// <summary>Wo das Kartenskript die Herkunft eingelesener Höhenkarten festhält.</summary>
    public static string HoehendatenPfad(Projektordner o) => Path.Combine(o.Wurzel, "tools", "Kartenbau", "Staedte", "HOEHENDATEN.md");

    /// <summary>
    /// <c>hoehenkarte-einlesen</c>: liest eine r16/raw-Datei (<see cref="Hoehenkarte"/>) in die Karte ein, meldet Abdeckung, Abschneiden und die
    /// Größe des Feldes <c>hoehen</c> vorher und nachher, schreibt die Karte – nur ohne Fehler – und hält die Herkunft fest. 2: falscher Aufruf
    /// (fehlende Angabe, Herkunft oder unlesbare Datei).
    /// </summary>
    public static int HoehenkarteEinlesen(Projektordner o, string karte, string datei, double? pixel, Kartenort? ursprung, double? unten, double? oben,
        int breite, double versatz, int stufe, bool hohesZuerst, Hoehendatenherkunft herkunft, TextWriter aus)
    {
        if (pixel is null || ursprung is null || unten is null || oben is null)
        {
            aus.WriteLine("hoehenkarte-einlesen braucht --pixel=<m>, --ursprung=<x,z>, --unten=<m> und --oben=<m> (Höhe der Werte 0 und 65535).");
            return 2;
        }
        if (herkunft.Fehlt is { } fehlt)
        {
            aus.WriteLine($"Ohne Herkunft liest das Skript keine Höhenkarte ein: {fehlt} fehlt (z. B. --quelle=\"DGM1 NRW, Kachel …\" --lizenz=\"dl-de/zero-2.0\").");
            return 2;
        }
        var pfad = Path.GetFullPath(datei);
        if (!File.Exists(pfad)) throw new FileNotFoundException($"Die Höhenkarte {pfad} gibt es nicht.", pfad);
        var bytes = File.ReadAllBytes(pfad);
        ushort[] werte;
        Hoehenkartenlage lage;
        try
        {
            (werte, var b, var t) = Hoehenkarte.Lesen(bytes, breite, hohesZuerst);
            lage = new Hoehenkartenlage(b, t, Cm(pixel.Value), Cm(ursprung.Value.X), Cm(ursprung.Value.Z), Cm(unten.Value), Cm(oben.Value), Cm(versatz), stufe);
            if (lage.Fehler() is { Count: > 0 } fehler) throw new ArgumentException(string.Join(" ", fehler));
        }
        catch (ArgumentException ex)
        {
            aus.WriteLine($"{Path.GetFileName(pfad)}: {ex.Message}");
            return 2;
        }
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        var kartenpfad = PfadVon(o, karte);
        var (dateiVorher, hoehenVorher) = (new FileInfo(kartenpfad).Length, e.Hoehen.AlsJson(e.Kartengrenzen.Aussen)?.ToJsonString().Length ?? 0);
        var bericht = e.HoehenkarteEinlesen(werte, lage);
        aus.WriteLine($"{e.Kennung}: Höhenkarte {Path.GetFileName(pfad)} ({lage.Breite} × {lage.Tiefe} Pixel zu {Kartenschreiber.Zahl(lage.PixelCm / 100.0)} m, " +
                      $"Ursprung {ursprung.Value}): {Tausend(bericht.Abgedeckt)} Rasterpunkte abgedeckt, {Tausend(bericht.Geaendert)} geändert");
        if (bericht.Warnung is { } warnung) aus.WriteLine("   " + warnung);
        if (bericht.Geaendert == 0)
        {
            aus.WriteLine($"{e.Kennung}: Die Höhenkarte ändert nichts (liegt sie außerhalb der Karte?) – nichts geschrieben.");
            return 0;
        }
        var hoehen = e.Hoehen.AlsJson(e.Kartengrenzen.Aussen);
        aus.WriteLine($"   hoehen: {hoehen?["kacheln"]?.AsArray().Count ?? 0} Kacheln, {Tausend(hoehen?.ToJsonString().Length ?? 0)} Zeichen (vorher {Tausend(hoehenVorher)})");
        var ergebnis = Schreiben(e, kartenpfad, o.Vorschauen, true, aus, o);
        if (ergebnis != 0) return ergebnis;
        aus.WriteLine($"   Kartendatei: {Tausend(new FileInfo(kartenpfad).Length)} Bytes (vorher {Tausend(dateiVorher)})");
        HerkunftEintragen(o, e.Kennung, pfad, bytes, lage, herkunft);
        aus.WriteLine($"   Herkunft eingetragen: {Relativ(o, HoehendatenPfad(o))}");
        return 0;
    }

    /// <summary>Hängt eine Zeile an tools/Kartenbau/Staedte/HOEHENDATEN.md (legt die Datei mit Kopf an, wenn es sie nicht gibt).</summary>
    private static void HerkunftEintragen(Projektordner o, string kennung, string pfad, byte[] bytes, Hoehenkartenlage lage, Hoehendatenherkunft h)
    {
        var ziel = HoehendatenPfad(o);
        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
        if (!File.Exists(ziel)) File.WriteAllText(ziel, Hoehendatenherkunft.Kopf);
        static string Zelle(string? text) => (text ?? "unbekannt").Replace("|", "\\|").Replace("\n", " ");
        static string M(int cm) => Kartenschreiber.Zahl(cm / 100.0);
        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var angaben = $"{lage.Breite} × {lage.Tiefe} px zu {M(lage.PixelCm)} m, Ursprung ({M(lage.UrsprungXCm)}, {M(lage.UrsprungZCm)}), " +
                      $"0 → {M(lage.UntenCm)} m, 65535 → {M(lage.ObenCm)} m, Versatz {M(lage.VersatzCm)} m, Stufe {lage.StufeCm} cm";
        File.AppendAllText(ziel, $"| {DateTime.Today:yyyy-MM-dd} | {kennung} | {Zelle(Path.GetFileName(pfad))} | `{sha}` | {angaben} | {Zelle(h.Quelle)} | {Zelle(h.Lizenz)} | {Zelle(h.Abruf)} |\n");
    }

    /// <summary>
    /// <c>hoehenkarte-ausgeben</c>: schreibt das Raster der ganzen Karte als r16 (2 m je Pixel) und nennt die Angaben zum Wiedereinlesen.
    /// </summary>
    public static int HoehenkarteAusgeben(Projektordner o, string karte, string datei, double? unten, double? oben, bool hohesZuerst, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        ushort[] werte;
        Hoehenkartenlage lage;
        int begrenzt;
        try
        {
            (werte, lage, begrenzt) = e.HoehenkarteAusgeben(unten is { } u ? Cm(u) : null, oben is { } h ? Cm(h) : null);
        }
        catch (ArgumentException ex)
        {
            aus.WriteLine(ex.Message);
            return 2;
        }
        var pfad = Path.GetFullPath(datei);
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        File.WriteAllBytes(pfad, Hoehenkarte.Schreiben(werte, hohesZuerst));
        static string M(int cm) => Kartenschreiber.Zahl(cm / 100.0);
        aus.WriteLine($"{e.Kennung}: {pfad} geschrieben – {lage.Breite} × {lage.Tiefe} Pixel zu 2 m (r16, {(hohesZuerst ? "hohes" : "niedriges")} Byte zuerst), " +
                      $"erstes Pixel bei ({M(lage.UrsprungXCm)}, {M(lage.UrsprungZCm)}), Wert 0 = {M(lage.UntenCm)} m, 65535 = {M(lage.ObenCm)} m");
        if (begrenzt > 0) aus.WriteLine($"   Warnung: {Tausend(begrenzt)} Punkte liegen außerhalb von unten … oben und wurden begrenzt.");
        aus.WriteLine($"   Wieder einlesen: hoehenkarte-einlesen {e.Kennung} {datei} --breite={lage.Breite} --pixel=2 --ursprung={M(lage.UrsprungXCm)},{M(lage.UrsprungZCm)} " +
                      $"--unten={M(lage.UntenCm)} --oben={M(lage.ObenCm)} --stufe=1{(hohesZuerst ? " --bytefolge=be" : "")} --quelle=… --lizenz=…");
        return 0;
    }

    /// <summary><c>liste</c>: die Stadtskripte und Umbauten mit Kennung und Beschreibung.</summary>
    public static int Liste(TextWriter aus)
    {
        aus.WriteLine("Stadtskripte (tools/Kartenbau/Staedte):");
        foreach (var s in Stadtskript.Alle()) aus.WriteLine($"   {s.Kennung,-14} {s.Stadt} – {s.Beschreibung}");
        aus.WriteLine("Umbauten (tools/Kartenbau/Umbauten):");
        foreach (var u in Umbau.Alle()) aus.WriteLine($"   {u.Kennung,-14} {u.Karte} – {u.Beschreibung}");
        return 0;
    }

    /// <summary>
    /// <c>fluss</c>: legt einen Fluss oder Bach an (<see cref="Kartenentwurf.FlussAnlegen"/>), mit Breite und Tiefe je Punkt bzw. für alle,
    /// dann schreiben – nur ohne Fehler.
    /// </summary>
    public static int Fluss(Projektordner o, string karte, IReadOnlyList<(Kartenort Ort, double? Breite, double? Tiefe)> punkte, string art, string? name,
        double? breite, double? tiefe, double? stroemung, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        var f = e.FlussAnlegen(punkte.Select(p => p.Ort).ToList(), art, breite ?? 0, name);
        if (f.Punkte.Count == punkte.Count)
            for (var i = 0; i < punkte.Count; i++)
            {
                if (punkte[i].Breite is { } b) e.FlussBreiteSetzen(f, i, b);
                if (punkte[i].Tiefe is { } t) e.FlussTiefeSetzen(f, i, t);
            }
        if (tiefe is { } ft) e.FlussTiefeSetzen(f, -1, ft);
        if (stroemung is { } s) e.FlussStroemungSetzen(f, s);
        var lauf = e.Wasserform.Laeufe.FirstOrDefault(l => l.Name == f.Name);
        aus.WriteLine($"{e.Kennung}: {f.Bezeichnung} ({Kartenentwurf.GewaesserName(f.Art)}, {f.Punkte.Count} Punkte, {Kartenschreiber.Zahl(Math.Round(lauf?.Laenge ?? 0))} m lang)");
        return Schreiben(e, PfadVon(o, karte), o.Vorschauen, true, aus, o);
    }

    /// <summary><c>see</c>: legt einen See oder Teich an (<see cref="Kartenentwurf.SeeAnlegen"/>), dann schreiben – nur ohne Fehler.</summary>
    public static int See(Projektordner o, string karte, IReadOnlyList<Kartenort> ecken, string art, string? name, double? wasserstand, double? tiefe, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        var s = e.SeeAnlegen(ecken, art, name, wasserstand, tiefe);
        var see = e.Wasserform.Seen.FirstOrDefault(x => x.Name == s.Name);
        aus.WriteLine($"{e.Kennung}: {s.Bezeichnung} ({Kartenentwurf.GewaesserName(s.Art)}, Wasserstand {Kartenschreiber.Zahl(Math.Round(see?.Wasserstand ?? 0, 2))} m" +
                      $"{(s.Wasserstand is null ? " (vom Ufer)" : "")})");
        return Schreiben(e, PfadVon(o, karte), o.Vorschauen, true, aus, o);
    }

    /// <summary><c>faehre</c>: setzt eine Fährstelle (<see cref="Kartenentwurf.FaehreSetzen"/>), dann schreiben – nur ohne Fehler.</summary>
    public static int Faehre(Projektordner o, string karte, Kartenort von, Kartenort bis, string? name, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        var f = e.FaehreSetzen(von, bis, name);
        aus.WriteLine($"{e.Kennung}: {f.Bezeichnung} von {f.Von} nach {f.Bis}");
        return Schreiben(e, PfadVon(o, karte), o.Vorschauen, true, aus, o);
    }

    /// <summary><c>hafen</c>: setzt einen Hafenplatz (Katalog „hafenplatz“), dann schreiben – nur ohne Fehler.</summary>
    public static int Hafen(Projektordner o, string karte, Kartenort ort, double drehung, string? name, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        var g = e.Setzen(Kartenentwurf.HafenKatalog, ort.X, ort.Z, drehung);
        if (!string.IsNullOrWhiteSpace(name)) e.Umbenennen(g.Kennung, name);
        aus.WriteLine($"{e.Kennung}: {g.Kennung} ({g.Name}) bei {g.Punkt}, Drehung {Kartenschreiber.Zahl(g.Drehung)}");
        return Schreiben(e, PfadVon(o, karte), o.Vorschauen, true, aus, o);
    }

    /// <summary>
    /// <c>belag</c>: Startbelag eines Weges (Name mit _ für Leerzeichen, sonst Nummer in der Karte) – ganz oder von–bis Meter; „vorgabe“ setzt den
    /// ganzen Weg bzw. den Abschnitt auf die Vorgabe der Klasse zurück. Dann schreiben – nur ohne Fehler.
    /// </summary>
    public static int Belag(Projektordner o, string karte, string wegname, string stufe, (double Von, double Bis)? bereich, TextWriter aus)
    {
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, karte, katalog, daten);
        var name = wegname.Replace('_', ' ');
        var weg = e.Wege.FirstOrDefault(w => w.Name == name)
                  ?? (int.TryParse(wegname, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nr) && nr >= 0 && nr < e.Wege.Count ? e.Wege[nr] : null);
        if (weg is null)
        {
            aus.WriteLine($"Einen Weg „{name}“ gibt es in {e.Kennung} nicht.");
            return 2;
        }
        Belagstufe? neu = null;
        if (!stufe.Equals("vorgabe", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<Belagstufe>(stufe, ignoreCase: true, out var s) || int.TryParse(stufe, out _))
            {
                aus.WriteLine($"Eine Stufe „{stufe}“ gibt es nicht (trampelpfad, fahrweg, kies, pflaster, vorgabe).");
                return 2;
            }
            neu = s;
        }
        if (bereich is { } b) e.BelagSetzen(weg, b.Von, b.Bis, neu ?? Wegbelag.Vorgabe(weg.Klasse));
        else e.BelagSetzen(weg, neu);
        aus.WriteLine($"{e.Kennung}: {weg.Bezeichnung} – " + string.Join(", ", weg.BelagAbschnitte.Select(a =>
            $"{Kartenschreiber.Zahl(Math.Round(a.Von, 1))}–{Kartenschreiber.Zahl(Math.Round(a.Bis, 1))} m {Wegbelag.Anzeigename(a.Stufe)}")));
        return Schreiben(e, PfadVon(o, karte), o.Vorschauen, true, aus, o);
    }

    /// <summary><c>umbauen</c>: wendet ein Umbauskript auf seine Karte an und schreibt sie – nur ohne Fehler.</summary>
    public static int Umbauen(Projektordner o, string name, TextWriter aus)
    {
        if (Umbau.Finde(name) is not { } umbau)
        {
            aus.WriteLine($"Einen Umbau „{name}“ gibt es nicht.");
            Liste(aus);
            return 2;
        }
        var (katalog, daten) = Laden(o);
        var e = Oeffnen(o, umbau.Karte, katalog, daten);
        umbau.Anwenden(e, aus);
        return Schreiben(e, PfadVon(o, umbau.Karte), o.Vorschauen, true, aus, o);
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
            aus.WriteLine($"   Bauzonen {g.Key.ToString().ToLowerInvariant()}: {g.Count()}, {Tausend(flaeche)} m², " +
                          $"{Tausend((int)(flaeche / (e.Raster * e.Raster)))} Rasterfelder");
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

/// <summary>Woher eine eingelesene Höhenkarte stammt: Quelle (Anbieter, Kachel, Adresse), Lizenz und Abrufdatum – Quelle und Lizenz sind Pflicht.</summary>
public sealed record Hoehendatenherkunft(string? Quelle, string? Lizenz, string? Abruf = null)
{
    /// <summary>Was fehlt („--quelle“, „--lizenz“) – null: vollständig.</summary>
    public string? Fehlt => string.IsNullOrWhiteSpace(Quelle) ? "--quelle" : string.IsNullOrWhiteSpace(Lizenz) ? "--lizenz" : null;

    /// <summary>Der Kopf der Datei tools/Kartenbau/Staedte/HOEHENDATEN.md.</summary>
    public const string Kopf = """
        # Höhendaten der Stadtskripte

        Herkunft jeder Höhenkarte, die das Kartenskript eingelesen hat (`hoehenkarte-einlesen`, docs/LEVELDESIGN.md „Kartenskript“). Das Skript
        hängt die Zeile selbst an, wenn es die Karte geschrieben hat; die Höhenkarte selbst liegt nicht im Repository (die SHA-256 sagt, welche
        Datei es war). Freie Quellen: DGM NRW (dl-de/zero-2.0, ohne Pflichtvermerk); Copernicus DEM und SRTM mit Quellenvermerk; Gaea oder
        World Machine nur mit geprüfter kommerzieller Lizenzstufe.

        | Eingelesen | Karte | Datei | SHA-256 | Lage und Maßstab | Quelle | Lizenz | Abruf |
        |---|---|---|---|---|---|---|---|

        """;
}
