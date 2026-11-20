using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Reichsgilde.KartenbauMcp;

/// <summary>Die MCP-Werkzeuge: dünne Hülle um <see cref="Werkzeuge"/>. Fehler kommen als Text zurück, nicht als Absturz.</summary>
[McpServerToolType]
public static class Kartenwerkzeuge
{
    private static readonly Lazy<Werkzeuge?> Gemeinsam = new(Werkzeuge.Erzeugen);

    private static T Mit<T>(Func<Werkzeuge, T> f, Func<string, T> fehler)
    {
        try
        {
            var w = Gemeinsam.Value;
            return w is null ? fehler("Den Projektordner (mit game/daten/katalog.json) finde ich nicht – KARTENBAU_MCP_PROJEKT setzen.") : f(w);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or Reichsgilde.Core.Bauen.KartenFehler)
        {
            return fehler(ex is Reichsgilde.Core.Bauen.KartenFehler k ? "Die Karte ist nicht lesbar: " + string.Join("; ", k.Fehler) : ex.Message);
        }
    }

    [McpServerTool(Name = "karten_auflisten"), Description("Listet die Karten in game/karten, Entwürfe im Arbeitsordner und die Stadtskripte/Umbauten, die es gibt.")]
    public static string KartenAuflisten() => Mit(w => w.KartenAuflisten(), m => m);

    [McpServerTool(Name = "karte_lesen"), Description("Fasst eine Karte zusammen: Größe, Zonen, Wege, Gebäude und die Übersicht der Prüfung. Kennung wie greybox oder koeln.")]
    public static string KarteLesen([Description("Kennung der Karte (a-z, 0-9, _)")] string karte) => Mit(w => w.KarteLesen(karte), m => m);

    [McpServerTool(Name = "karte_bauen"), Description("Führt ein Stadtskript aus. Ohne schreiben entsteht nur ein Entwurf im Arbeitsordner (auch mit Fehlern); mit schreiben wird game/karten/<kennung>.json geschrieben, nur ohne Fehler und über eine vorhandene Karte nur mit ueberschreiben.")]
    public static string KarteBauen([Description("Kennung des Stadtskripts (siehe karten_auflisten)")] string skript,
        [Description("true: nach game/karten schreiben")] bool schreiben = false,
        [Description("true: eine vorhandene Karte ersetzen (nur mit schreiben)")] bool ueberschreiben = false) =>
        Mit(w => w.KarteBauen(skript, schreiben, ueberschreiben), m => m);

    [McpServerTool(Name = "karte_pruefen"), Description("Prüft eine Karte (Regeln, Startorte, Kapazität, Katalog gegen die Modelle) und gibt die Befunde als Liste zurück.")]
    public static string KartePruefen([Description("Kennung der Karte")] string karte) => Mit(w => w.KartePruefen(karte), m => m);

    [McpServerTool(Name = "karte_bild"), Description("Zeichnet das Vorschaubild einer Karte (Arbeitsordner, nicht docs/karten). Gibt ein PNG zurück (Svg.Skia); nur wenn das Zeichnen scheitert, das SVG als Text und den Dateipfad.")]
    public static IList<ContentBlock> KarteBild([Description("Kennung der Karte")] string karte,
        [Description("true: die Belegung (Probeplan der Prüfung) statt der Karte")] bool belegung = false) =>
        Mit<IList<ContentBlock>>(w =>
        {
            var (text, svg, png) = w.KarteBild(karte, belegung);
            var liste = new List<ContentBlock> { new TextContentBlock { Text = text } };
            if (png is not null) liste.Add(ImageContentBlock.FromBytes(png, "image/png"));
            else if (svg is not null && svg.Length <= 100_000) liste.Add(new TextContentBlock { Text = svg });
            return liste;
        }, m => new List<ContentBlock> { new TextContentBlock { Text = m } });
}
