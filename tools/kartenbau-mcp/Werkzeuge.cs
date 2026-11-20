using System.Text;
using System.Text.RegularExpressions;
using Reichsgilde.Core.Kartenwerkstatt;
using SkiaSharp;
using Svg.Skia;
using Reichsgilde.Kartenbau;

namespace Reichsgilde.KartenbauMcp;

/// <summary>
/// Die Arbeit hinter den Werkzeugen (ohne MCP, damit sie sich testen lässt). Sie ruft nur die Befehle von tools/Kartenbau auf.
/// Geschrieben wird nie außerhalb von game/karten/ (nur beim ausdrücklichen Bauen mit schreiben) und dem Arbeitsordner (Entwürfe, Bilder).
/// Kennungen sind nur kleine Buchstaben, Ziffern und Unterstrich – Pfade lässt der Server nie zu.
/// </summary>
public sealed class Werkzeuge
{
    private static readonly Regex Kennung = new("^[a-z0-9_]{1,40}$", RegexOptions.Compiled);

    public Projektordner Ordner { get; }

    /// <summary>Hier landen Entwürfe und Bilder (Vorgabe: Systemordner für Temporäres, einstellbar mit KARTENBAU_MCP_ARBEIT).</summary>
    public string Arbeit { get; }

    public Werkzeuge(Projektordner ordner, string? arbeit = null)
    {
        Ordner = ordner;
        Arbeit = Path.GetFullPath(arbeit ?? Environment.GetEnvironmentVariable("KARTENBAU_MCP_ARBEIT") ?? Path.Combine(Path.GetTempPath(), "kartenbau-mcp"));
        Directory.CreateDirectory(Arbeit);
    }

    /// <summary>Findet den Projektordner: KARTENBAU_MCP_PROJEKT, sonst vom Arbeitsordner und vom Programm aus nach oben.</summary>
    public static Werkzeuge? Erzeugen()
    {
        var wurzel = Environment.GetEnvironmentVariable("KARTENBAU_MCP_PROJEKT");
        if (wurzel is not null) Directory.SetCurrentDirectory(wurzel);
        var o = Projektordner.Finden();
        return o is null ? null : new Werkzeuge(o);
    }

    private static string PruefeKennung(string wert, string was)
    {
        if (!Kennung.IsMatch(wert ?? "")) throw new ArgumentException($"{was} „{wert}“ ist keine gültige Kennung (nur a-z, 0-9 und _, höchstens 40 Zeichen; keine Pfade).");
        return wert!;
    }

    /// <summary>Der Pfad einer Karte: game/karten/&lt;kennung&gt;.json, sonst ein Entwurf im Arbeitsordner.</summary>
    private string KartenPfad(string karte)
    {
        PruefeKennung(karte, "Die Karte");
        var pfad = Ordner.Karte(karte);
        if (File.Exists(pfad)) return pfad;
        var entwurf = Path.Combine(Arbeit, karte + ".json");
        if (File.Exists(entwurf)) return entwurf;
        throw new ArgumentException($"Die Karte „{karte}“ gibt es weder in game/karten noch als Entwurf im Arbeitsordner. Mit karten_auflisten ansehen.");
    }

    private Kartenentwurf Oeffnen(string karte)
    {
        var (katalog, daten) = Befehle.Laden(Ordner);
        return Befehle.Oeffnen(Ordner, KartenPfad(karte), katalog, daten);
    }

    public string KartenAuflisten()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Karten (game/karten):");
        foreach (var f in Directory.GetFiles(Ordner.Karten, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            sb.AppendLine("   " + Path.GetFileNameWithoutExtension(f));
        var entwuerfe = Directory.GetFiles(Arbeit, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToList();
        if (entwuerfe.Count > 0)
        {
            sb.AppendLine("Entwürfe im Arbeitsordner:");
            foreach (var f in entwuerfe) sb.AppendLine("   " + Path.GetFileNameWithoutExtension(f));
        }
        var aus = new StringWriter();
        Befehle.Liste(aus);
        sb.Append(aus);
        return sb.ToString();
    }

    public string KarteLesen(string karte)
    {
        var e = Oeffnen(karte);
        var g = e.Kartengrenzen;
        var sb = new StringBuilder();
        sb.AppendLine($"{e.Stadt} ({e.Kennung}): {g.Breite:0} × {g.Tiefe:0} m, Mitte ({g.Mitte.X:0}, {g.Mitte.Z:0}), Rand {g.Randbreite:0} m, Raster {e.Raster:0.##} m");
        sb.AppendLine($"Zonen: {e.Bauzonen.Count} Bauzonen ({string.Join(", ", e.Bauzonen.GroupBy(z => z.Art.ToString().ToLowerInvariant()).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Count()} {x.Key}"))})");
        sb.AppendLine($"Wege: {e.Wege.Count}; Naturflächen: {e.Naturflaechen.Count}; Pflanzen: {e.Pflanzen.Count}; Felder: {e.Felder.Count}");
        sb.AppendLine($"Gebäude: {e.Gebaeude.Count} ({string.Join(", ", e.Gebaeude.GroupBy(x => x.Art == "" ? "?" : x.Art).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Count()} {x.Key}"))})");
        sb.AppendLine();
        sb.AppendLine("Übersicht der Prüfung:");
        Befehle.Bericht(e, new StringWriterEin(sb));
        return sb.ToString();
    }

    public string KartePruefen(string karte)
    {
        var e = Oeffnen(karte);
        var befunde = Kartenpruefung.Pruefen(e).ToList();
        var (katalog, _) = Befehle.Laden(Ordner);
        var kat = Katalogpruefung.Pruefen(katalog, Ordner.Modelle).ToList();
        var sb = new StringBuilder();
        var fehler = befunde.Count(b => b.Schwere == Schwere.Fehler) + kat.Count(b => b.Schwere == Schwere.Fehler);
        sb.AppendLine($"{e.Kennung}: {fehler} Fehler, {befunde.Count + kat.Count - fehler} Hinweise");
        foreach (var b in befunde) sb.AppendLine($"- [{(b.Schwere == Schwere.Fehler ? "FEHLER" : "Hinweis")}] ({b.Regel}) {b.Text}");
        foreach (var b in kat) sb.AppendLine($"- [{(b.Schwere == Schwere.Fehler ? "FEHLER" : "Hinweis")}] (katalog) {b.Text}");
        return sb.ToString();
    }

    /// <summary>
    /// Führt ein Stadtskript aus. Ohne <paramref name="schreiben"/> entsteht nur ein Entwurf im Arbeitsordner (auch mit Fehlern, zum Ansehen);
    /// mit <paramref name="schreiben"/> wird game/karten/&lt;kennung&gt;.json geschrieben (nur ohne Fehler, über eine vorhandene Karte nur mit <paramref name="ueberschreiben"/>).
    /// Die Vorschaubilder gehen immer in den Arbeitsordner, nie nach docs/karten.
    /// </summary>
    public string KarteBauen(string skript, bool schreiben = false, bool ueberschreiben = false)
    {
        PruefeKennung(skript, "Das Stadtskript");
        var s = Stadtskript.Finde(skript);
        if (s is null) return $"Ein Stadtskript „{skript}“ gibt es nicht.\n" + KartenAuflisten();
        var (katalog, daten) = Befehle.Laden(Ordner);
        var e = s.Bauen(katalog, daten);
        PruefeKennung(e.Kennung, "Die Kennung des Skripts");
        var sb = new StringBuilder();
        var aus = new StringWriterEin(sb);
        if (!schreiben)
        {
            var pfad = Path.Combine(Arbeit, e.Kennung + ".json");
            File.WriteAllText(pfad, e.ZuJson());
            sb.AppendLine($"Entwurf geschrieben (Arbeitsordner, nicht game/karten): {e.Kennung}");
            Befehle.Bericht(e, aus);
            return sb.ToString();
        }
        var rueckgabe = Befehle.Schreiben(e, Ordner.Karte(e.Kennung), Arbeit, ueberschreiben, aus, Ordner);
        sb.AppendLine($"Rückgabe {rueckgabe} (0 = geschrieben, 1 = Fehler, 2 = Datei gibt es schon)");
        return sb.ToString();
    }

    /// <summary>Das SVG der Karte (im Arbeitsordner gezeichnet) und das PNG dazu.</summary>
    public (string Text, string? Svg, byte[]? Png) KarteBild(string karte, bool belegung = false)
    {
        var e = Oeffnen(karte);
        var pfade = Befehle.VorschauSchreiben(e, Arbeit, belegung);
        var svgPfad = pfade[belegung ? 1 : 0];
        var svg = File.ReadAllText(svgPfad);
        var png = Umwandeln(svgPfad);
        var text = $"Bild von {e.Kennung}{(belegung ? " (Belegung)" : "")}: {svgPfad}, {svg.Length / 1024} KB"
                   + (png is null ? "; PNG ließ sich nicht zeichnen – nur das SVG" : "; PNG anbei");
        return (text, svg, png);
    }

    /// <summary>SVG zu PNG mit Svg.Skia (Breite höchstens 1600 Pixel); null, wenn das Zeichnen scheitert.</summary>
    private static byte[]? Umwandeln(string svgPfad)
    {
        try
        {
            using var svg = new SKSvg();
            if (svg.Load(svgPfad) is null || svg.Picture is null) return null;
            var r = svg.Picture.CullRect;
            var f = Math.Min(1f, 1600f / Math.Max(1f, r.Width));
            using var bild = new SKBitmap((int)Math.Ceiling(r.Width * f), (int)Math.Ceiling(r.Height * f));
            using var leinwand = new SKCanvas(bild);
            leinwand.Clear(SKColors.White);
            leinwand.Scale(f);
            leinwand.DrawPicture(svg.Picture);
            using var daten = SKImage.FromBitmap(bild).Encode(SKEncodedImageFormat.Png, 90);
            var png = daten.ToArray();
            File.WriteAllBytes(Path.ChangeExtension(svgPfad, ".png"), png);
            return png;
        }
        catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException or IOException) { return null; }
    }

    /// <summary>Ein TextWriter, der in einen vorhandenen StringBuilder schreibt.</summary>
    private sealed class StringWriterEin : StringWriter
    {
        public StringWriterEin(StringBuilder sb) : base(sb) { }
    }
}
