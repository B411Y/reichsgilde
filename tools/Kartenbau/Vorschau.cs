using System.Globalization;
using System.Text;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau;

// ═════════════════════════════════════════════════════════════════════════════
//  Vorschaubild einer Karte als SVG (docs/karten/<kennung>.svg) – nur aus der Kartendatei und dem Katalog gezeichnet
//
//  Von oben, Norden oben, 1 Einheit = 1 Meter. Gebäude als ihre Grundfläche aus dem Katalog (gedreht, mal Maßstab) mit einem Punkt an der
//  Mitte der Vorderseite; Wege mit ihrem Verlauf (glatte Wege als Kurve, wie im Spiel); Mauern aus dem Mauerplan des Kerns (Mauerstücke,
//  Türme, Ecktürme, Torburgen, wie im Spiel); Bauzonen nach Lage, Naturflächen mit Aussparungen, Wasser, Gelände, Häfen. Die Startbelegung
//  ist farbig: Jeder Platz eines Hauses hat dessen Farbe (Legende oben links).
//  Die Belegung (<kennung>-belegung.svg) zeigt darüber, wie die Prüfung die Stadt füllt: die Kapazitätsprüfung (Köln, neue Karten) bzw. den
//  Probeplan (Greybox).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Zeichnet das Vorschaubild einer Karte (siehe Kopf der Datei).</summary>
public static class Vorschau
{
    private static readonly Dictionary<string, string> Farben = new()
    {
        ["boden"] = "#E6E8E1", ["feld"] = "#DCD5A6", ["wald"] = "#9DB287", ["huegel"] = "#C9BFA6", ["wildnis"] = "#D9B79A",
        ["wasser"] = "#AFC7D5", ["wasserlinie"] = "#4E7A96", ["mauer"] = "#1F2326", ["zonenrand"] = "#6B6E66", ["start"] = "#A3262A",
        ["fest"] = "#1F2326", ["vorkommen"] = "#FFFFFF", ["text"] = "#1F2326",
    };

    private static readonly Dictionary<Lage, string> Zonenfarben = new()
    {
        [Lage.Kern] = "#EBD3CB", [Lage.Mauerring] = "#E7E0CC", [Lage.Vorstadt] = "#E2DCC6", [Lage.Umland] = "#E1E5C0",
    };

    /// <summary>Füllfarbe je Art des Bausteins.</summary>
    private static readonly Dictionary<string, string> Gebaeudefarben = new()
    {
        ["sonderbau"] = "#8E8F88", ["werkstattplatz"] = "#B58A5A", ["umlandbetrieb"] = "#C9B88A", ["umlandstelle"] = "#B7C79A",
        ["kulisse"] = "#9DB287", ["einwohnerplatz"] = "#CFE0C0", ["wohnhausplatz"] = "#8E7470", ["berg"] = "#BDB3A0",
    };

    private static readonly Dictionary<string, string> Naturfarben = new() { ["wald"] = "#7F9B6A", ["streu"] = "#C4CFA8" };

    /// <summary>Strich je Wegklasse: Breite, Strichelung, Farbe.</summary>
    private static (double Breite, string? Strich, string Farbe) Strich(Wegklasse k) => k switch
    {
        Wegklasse.Land => (2.0, "6 3", "#4B5258"),
        Wegklasse.Haupt => (2.6, null, "#1F2326"),
        Wegklasse.Ring => (2.2, null, "#1F2326"),
        Wegklasse.Wall => (1.6, null, "#4B5258"),
        Wegklasse.Gasse => (1.0, "3 2", "#4B5258"),
        Wegklasse.Leinpfad => (0.8, "1 2", "#4E7A96"),
        Wegklasse.Feldweg => (1.4, "4 2", "#7A6A4F"),
        _ => (0.8, "2 2", "#7A6A4F"),
    };

    /// <summary>Farben der Kapazitätsprüfung je Gebäudeart.</summary>
    private static readonly Dictionary<string, string> Kapazitaetsfarben = new()
    {
        ["Werkstatt"] = "#8C5A2B", ["Wohnhaus"] = "#A3262A", ["Stadtpalais"] = "#6B2A7A", ["Stadthof"] = "#2F4F7A",
        ["Einwohner-Wohnhaus"] = "#7A8A6A", ["Einwohner-Bürgerhaus"] = "#5E7A4A", ["Ratsherrenhaus"] = "#3E5A34", ["fest"] = "#1F2326",
    };

    /// <summary>Farbe des Spielers, wenn die Karte keine nennt (wie in der Greybox).</summary>
    public const string Spielerfarbe = "#336bb8";

    /// <summary>Das Vorschaubild der Karte. <paramref name="belegung"/>: dazu die Kapazität bzw. den Probeplan zeichnen.</summary>
    public static string Zeichnen(Kartenentwurf e, bool belegung = false)
    {
        var (x0, z0, x1, z1) = Grenzen(e);
        var o = new StringBuilder();
        void Zeile(string s) => o.Append(s).Append('\n');
        Zeile($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{F(x0)} {F(z0)} {F(x1 - x0)} {F(z1 - z0)}\" font-family=\"sans-serif\">");
        Zeile($"<title>{X(e.Stadt)} – Kartendatei (Vorschau{(belegung ? ", Belegung" : "")})</title>");
        Zeile($"<rect x=\"{F(x0)}\" y=\"{F(z0)}\" width=\"{F(x1 - x0)}\" height=\"{F(z1 - z0)}\" fill=\"{Farben["boden"]}\"/>");

        foreach (var g in e.Gelaende)
            Zeile($"<polygon points=\"{Punkte(g.Flaeche.Select(Kartenort.Aus))}\" fill=\"{Farben.GetValueOrDefault(g.Art, "#ccc")}\" opacity=\"0.8\"/>");
        // Natur: jede Fläche mit ihren Aussparungen als Maske, Wald kräftiger als Streu
        for (var i = 0; i < e.Naturflaechen.Count; i++)
        {
            var f = e.Naturflaechen[i];
            if (f.Ecken.Count < 3) continue;
            Zeile($"<mask id=\"natur{i}\"><rect x=\"{F(x0)}\" y=\"{F(z0)}\" width=\"{F(x1 - x0)}\" height=\"{F(z1 - z0)}\" fill=\"#fff\"/>"
                  + string.Concat(f.Aussparungen.Select(a => $"<polygon points=\"{Punkte(a)}\" fill=\"#000\"/>")) + "</mask>");
            Zeile($"<polygon points=\"{Punkte(f.Ecken)}\" fill=\"{Naturfarben.GetValueOrDefault(f.Art, "#9DB287")}\" opacity=\"{(f.Art == "wald" ? "0.55" : "0.35")}\" mask=\"url(#natur{i})\"/>");
        }
        foreach (var z in e.Bauzonen.Where(z => z.Ecken.Count >= 3))
            Zeile($"<polygon points=\"{Punkte(z.Ecken)}\" fill=\"{Zonenfarben[z.Art]}\" stroke=\"{Farben["zonenrand"]}\" stroke-width=\"0.4\"/>");
        foreach (var w in e.Wasser)
        {
            if (w.Flaeche is { Count: > 0 } fl)
                Zeile($"<polygon points=\"{Punkte(fl.Select(Kartenort.Aus))}\" fill=\"{Farben["wasser"]}\" stroke=\"{Farben["wasserlinie"]}\" stroke-width=\"0.6\"/>");
            else if (w.Linie is { Count: > 0 } li)
                Zeile($"<polyline points=\"{Punkte(li.Select(Kartenort.Aus))}\" fill=\"none\" stroke=\"{(w.Breite > 4 ? Farben["wasser"] : Farben["wasserlinie"])}\" stroke-width=\"{F(w.Breite)}\" stroke-linecap=\"round\"/>");
        }
        foreach (var w in e.Wege.Where(w => w.Punkte.Count >= 2))
        {
            var (b, strich, farbe) = Strich(w.Klasse);
            if (w.Breite > 0) (b, strich) = (w.Breite, null); // gezeichnete Breite des Weges
            var linie = Punkte(w.Verlauf);
            if (w.Gefahr > 1) Zeile($"<polyline points=\"{linie}\" fill=\"none\" stroke=\"#B5651D\" stroke-width=\"6\" opacity=\"0.35\"/>");
            Zeile($"<polyline points=\"{linie}\" fill=\"none\" stroke=\"{farbe}\" stroke-width=\"{F(b)}\"{(strich is null ? "" : $" stroke-dasharray=\"{strich}\"")} stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
        }
        Mauern(e, Zeile);
        foreach (var h in e.Haefen)
        {
            Zeile($"<polyline points=\"{Punkte(h.Kante.Select(Kartenort.Aus))}\" fill=\"none\" stroke=\"#5A5148\" stroke-width=\"2\"/>");
            foreach (var a in h.Anleger) Zeile($"<rect x=\"{F(a[0])}\" y=\"{F(a[1] - 1)}\" width=\"7\" height=\"2\" fill=\"#6B4E31\"/>");
            if (h.Kran is { Length: 2 } k) Zeile($"<circle cx=\"{F(k[0])}\" cy=\"{F(k[1])}\" r=\"2.5\" fill=\"none\" stroke=\"#1F2326\" stroke-width=\"1\"/>");
        }
        foreach (var u in e.Uebergaenge)
            Zeile($"<polyline points=\"{Punkte(new[] { Kartenort.Aus(u.Von), Kartenort.Aus(u.Bis) })}\" fill=\"none\" stroke=\"#1F2326\" " +
                  $"stroke-width=\"{(u.Art == "bruecke" ? "4" : "1.2")}\"{(u.Art == "faehre" ? " stroke-dasharray=\"4 3\"" : "")}/>");
        Gebaeude(e, Zeile);
        foreach (var p in e.Pflanzen)
            Zeile($"<circle cx=\"{F(p.X)}\" cy=\"{F(p.Z)}\" r=\"1\" fill=\"#5F7F4F\"/>");
        foreach (var v in e.Vorkommen)
        {
            var (x, z) = (v.Punkt.X, v.Punkt.Z);
            Zeile($"<polygon points=\"{F(x)},{F(z - 5)} {F(x + 5)},{F(z)} {F(x)},{F(z + 5)} {F(x - 5)},{F(z)}\" fill=\"{Farben["vorkommen"]}\" stroke=\"#1F2326\" opacity=\"0.8\"/>"
                  + $"<text x=\"{F(x + 7)}\" y=\"{F(z + 2)}\" font-size=\"6\" fill=\"{Farben["text"]}\">{X(v.Betrieb)}</text>");
        }
        foreach (var z in e.Bauzonen.Where(z => z.Ecken.Count >= 3))
        {
            var m = Kartengeometrie.Mitte(z.Ecken);
            Zeile($"<text x=\"{F(Math.Round(m.X))}\" y=\"{F(Math.Round(m.Z))}\" font-size=\"6\" text-anchor=\"middle\" fill=\"{Farben["zonenrand"]}\">{X(z.Kennung)}</text>");
        }
        Legende(e, x0, z0, x1 - x0, Zeile);
        if (belegung) Belegung(e, Zeile);
        Zeile("</svg>");
        return o.ToString();
    }

    /// <summary>Wie weit das Bild reicht: alle Gebäude, Wege, Mauern, Wasser und Gelände mit 10 m Rand (bei einer Karte mit Größe deren Rahmen).</summary>
    private static (double X0, double Z0, double X1, double Z1) Grenzen(Kartenentwurf e)
    {
        if (e.Groesse is not null) return e.Grenzen();
        var punkte = e.Gebaeude.SelectMany(g => g.Ecken() is { Count: > 0 } ecken ? ecken : new[] { g.Punkt })
            .Concat(e.Wege.SelectMany(w => w.Verlauf)).Concat(e.Mauern.SelectMany(m => m.Ecken))
            .Concat(e.Gelaende.SelectMany(g => g.Flaeche).Concat(e.Wasser.SelectMany(w => (w.Linie ?? new()).Concat(w.Flaeche ?? new()))).Select(Kartenort.Aus))
            .ToList();
        if (punkte.Count == 0) return (-50, -50, 50, 50);
        return (punkte.Min(p => p.X) - 10, punkte.Min(p => p.Z) - 10, punkte.Max(p => p.X) + 10, punkte.Max(p => p.Z) + 10);
    }

    /// <summary>Mauern aus dem Mauerplan: Mauerstücke (dick), Ecktürme und Mauertürme (Kreise), Torburgen (Rechtecke quer zur Mauer) mit Namen.</summary>
    private static void Mauern(Kartenentwurf e, Action<string> zeile)
    {
        foreach (var m in e.Mauern.Where(m => m.Ecken.Count >= 2))
        {
            var plan = m.Plan;
            zeile($"<{(m.Geschlossen ? "polygon" : "polyline")} points=\"{Punkte(m.Ecken)}\" fill=\"none\" stroke=\"{Farben["mauer"]}\" stroke-width=\"0.5\" stroke-dasharray=\"1 1\"/>");
            foreach (var s in plan.Stuecke)
                zeile($"<line x1=\"{F(s.X - s.EntlangX * s.Laenge / 2)}\" y1=\"{F(s.Z - s.EntlangZ * s.Laenge / 2)}\" x2=\"{F(s.X + s.EntlangX * s.Laenge / 2)}\" " +
                      $"y2=\"{F(s.Z + s.EntlangZ * s.Laenge / 2)}\" stroke=\"{Farben["mauer"]}\" stroke-width=\"2.4\"/>");
            foreach (var t in plan.Ecktuerme) zeile($"<circle cx=\"{F(t.X)}\" cy=\"{F(t.Z)}\" r=\"3.2\" fill=\"{Farben["mauer"]}\"/>");
            foreach (var t in plan.Tuerme) zeile($"<circle cx=\"{F(t.X)}\" cy=\"{F(t.Z)}\" r=\"2.4\" fill=\"{Farben["mauer"]}\"/>");
            foreach (var tor in plan.Tore)
            {
                var l = tor.Lage;
                var (ex, ez, fx, fz) = (l.EntlangX * 4, l.EntlangZ * 4, l.FeldX * 3.5, l.FeldZ * 3.5);
                var ecken = new[] { new Kartenort(l.X - ex - fx, l.Z - ez - fz), new Kartenort(l.X + ex - fx, l.Z + ez - fz),
                                    new Kartenort(l.X + ex + fx, l.Z + ez + fz), new Kartenort(l.X - ex + fx, l.Z - ez + fz) };
                zeile($"<polygon points=\"{Punkte(ecken)}\" fill=\"#6B6E66\" stroke=\"{Farben["mauer"]}\" stroke-width=\"0.8\"/>");
                zeile($"<text x=\"{F(l.X + l.FeldX * 8)}\" y=\"{F(l.Z + l.FeldZ * 8 + 2)}\" font-size=\"6\" text-anchor=\"middle\" fill=\"{Farben["text"]}\">{X(tor.Name)}</text>");
            }
        }
    }

    /// <summary>
    /// Gebäude: Grundfläche aus dem Katalog (gedreht), ein Punkt an der Mitte der Vorderseite; Felsberge zuerst (sie liegen hinter den Gruben).
    /// Plätze der Startbelegung in der Farbe ihres Hauses.
    /// </summary>
    private static void Gebaeude(Kartenentwurf e, Action<string> zeile)
    {
        var farbeVon = new Dictionary<string, string>();
        foreach (var b in e.Belegung())
            farbeVon.TryAdd(b.Kennung, b.IstSpieler ? e.Start.SpielerFarbe ?? Spielerfarbe : Hausfarbe(e, b.Haus));
        foreach (var g in e.Gebaeude.OrderBy(g => g.Art != Bausteinart.Berg))
        {
            var ecken = g.Ecken();
            if (ecken.Count == 0)
            {
                zeile($"<circle cx=\"{F(g.X)}\" cy=\"{F(g.Z)}\" r=\"3.5\" fill=\"{Farben["fest"]}\"/>");
                continue;
            }
            var belegt = farbeVon.TryGetValue(g.Kennung, out var hausfarbe);
            var fuellung = belegt ? hausfarbe! : Gebaeudefarben.GetValueOrDefault(g.Art, "#999");
            zeile($"<polygon points=\"{Punkte(ecken)}\" fill=\"{fuellung}\" stroke=\"{Farben["fest"]}\" stroke-width=\"{(belegt ? "0.8" : "0.3")}\"{(g.Art == Bausteinart.Berg ? " opacity=\"0.6\"" : "")}/>");
            if (g.Art != Bausteinart.Berg && g.Eingang is { } vorn)
                zeile($"<circle cx=\"{F(vorn.X)}\" cy=\"{F(vorn.Z)}\" r=\"0.9\" fill=\"{(g.Art == Bausteinart.Wohnhausplatz || belegt ? "#fff" : Farben["start"])}\"/>");
            if (g.Art is Bausteinart.Sonderbau or Bausteinart.Werkstattplatz or Bausteinart.Umlandbetrieb or Bausteinart.Umlandstelle)
                zeile($"<text x=\"{F(g.X)}\" y=\"{F(g.Z + 1)}\" font-size=\"3\" text-anchor=\"middle\" fill=\"{(belegt ? "#fff" : Farben["text"])}\">{X(g.Name)}</text>");
            else if (g.Art == Bausteinart.Wohnhausplatz)
                zeile($"<text x=\"{F(g.X)}\" y=\"{F(g.Z + 1.5)}\" font-size=\"4\" text-anchor=\"middle\" fill=\"#fff\" font-weight=\"bold\">{X(g.Kennung)}</text>");
        }
    }

    /// <summary>Die Farbe eines KI-Hauses: aus der Karte, sonst die Hausfarbe an seiner Stelle.</summary>
    private static string Hausfarbe(Kartenentwurf e, int haus) =>
        e.Start.Haeuser[haus].Farbe ?? Kartenentwurf.Hausfarben[haus % Kartenentwurf.Hausfarben.Count];

    /// <summary>Oben links: wer welche Farbe hat (nur mit Startbelegung).</summary>
    private static void Legende(Kartenentwurf e, double x0, double z0, double breite, Action<string> zeile)
    {
        if (e.Start.IstLeer) return;
        var eintraege = new List<(string Name, string Farbe)> { ("Spieler", e.Start.SpielerFarbe ?? Spielerfarbe) };
        eintraege.AddRange(e.Start.Haeuser.Select((h, i) => (h.Name, Hausfarbe(e, i))));
        var gr = Math.Max(5, Math.Round(breite / 70));
        var (x, z) = (x0 + gr, z0 + gr);
        zeile($"<rect x=\"{F(x - gr / 2)}\" y=\"{F(z - gr / 2)}\" width=\"{F(gr * 12)}\" height=\"{F(gr * (eintraege.Count + 0.6))}\" fill=\"#fff\" opacity=\"0.8\"/>");
        for (var i = 0; i < eintraege.Count; i++)
        {
            var y = z + i * gr;
            zeile($"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(gr * 0.8)}\" height=\"{F(gr * 0.8)}\" fill=\"{eintraege[i].Farbe}\" stroke=\"{Farben["fest"]}\" stroke-width=\"0.3\"/>");
            zeile($"<text x=\"{F(x + gr * 1.2)}\" y=\"{F(y + gr * 0.7)}\" font-size=\"{F(gr * 0.8)}\" fill=\"{Farben["text"]}\">{X(eintraege[i].Name)}</text>");
        }
    }

    /// <summary>Die Belegung: was die Kapazitätsprüfung in die Bauzonen setzt (je Rasterfeld), sonst der Probeplan (Rechtecke mit Eingang).</summary>
    private static void Belegung(Kartenentwurf e, Action<string> zeile)
    {
        if (e.Regeln.Kapazitaet is not null)
        {
            var k = e.Kapazitaetsrechnung();
            foreach (var platz in k.Plaetze)
                foreach (var (i, j) in platz.Felder)
                    zeile($"<rect x=\"{F(i * k.Raster)}\" y=\"{F(j * k.Raster)}\" width=\"{F(k.Raster)}\" height=\"{F(k.Raster)}\" fill=\"{Kapazitaetsfarben[platz.Art]}\" " +
                          $"opacity=\"{(platz.Art == "fest" ? "0.35" : "0.75")}\"/>");
            return;
        }
        foreach (var p in e.Probeplan)
        {
            var (wx, wz) = Mod(p.Drehung, 180) == 0 ? (p.Breite, p.Tiefe) : (p.Tiefe, p.Breite);
            var (x0, z0) = (p.Punkt.X - wx / 2, p.Punkt.Z - wz / 2);
            zeile($"<rect x=\"{F(x0)}\" y=\"{F(z0)}\" width=\"{F(wx)}\" height=\"{F(wz)}\" fill=\"{Kapazitaetsfarben.GetValueOrDefault(p.Art, "#999")}\" opacity=\"0.75\" stroke=\"#1F2326\" stroke-width=\"0.3\"/>");
            var (ex, ez) = Mod(p.Drehung, 360) switch
            {
                0 => (p.Punkt.X, z0 + wz),
                90 => (x0 + wx, p.Punkt.Z),
                180 => (p.Punkt.X, z0),
                _ => (x0, p.Punkt.Z),
            };
            zeile($"<circle cx=\"{F(ex)}\" cy=\"{F(ez)}\" r=\"0.9\" fill=\"#fff\"/>");
        }
    }

    /// <summary>Modulo mit dem Vorzeichen des Teilers (−90 mod 360 = 270).</summary>
    private static double Mod(double a, double b) => a - b * Math.Floor(a / b);

    private static string Punkte(IEnumerable<Kartenort> p) => string.Join(" ", p.Select(q => $"{F(q.X)},{F(q.Z)}"));

    /// <summary>Eine Zahl auf 2 Nachkommastellen, ohne überflüssige Nullen und ohne „-0“.</summary>
    private static string F(double x) => (Math.Round(x, 2) + 0.0).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Text für XML (&amp;, &lt;, &gt;, Anführungszeichen).</summary>
    private static string X(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
