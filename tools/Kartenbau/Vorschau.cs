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
//  Türme, Ecktürme, Torburgen, wie im Spiel); Bauzonen nach Lage, Naturflächen mit Aussparungen, Felder mit ihren Streifen, Wasser, Gelände,
//  Häfen. Das Bild zeigt die ganze Karte: den Randstreifen etwas dunkler, die Kante des Spielfelds gestrichelt (seit Schritt 5a). Die
//  Startbelegung ist farbig: Jeder Platz eines Hauses hat dessen Farbe (Legende oben links). Gelände (seit Schritt 5b) als braune Höhenlinien
//  des Bodens: alle 2 m (bei viel Höhenunterschied alle 5 m) eine dünne, alle 10 m eine kräftigere. Wasser (seit Schritt 5c) aus der
//  Wasserform des Kerns: Flüsse mit ihrer Breite je Punkt und einem Pfeil in Fließrichtung, Seen; Brücken (Holz braun, Stein grau, mit
//  Pfeilern), Furten (Trittsteine) und Fähren (gestrichelt, Anleger als Stege) aus dem Wasserplan.
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
        ["fest"] = "#1F2326", ["vorkommen"] = "#FFFFFF", ["text"] = "#1F2326", ["rand"] = "#D6DACF",
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

    /// <summary>Farbe eines Feldes nach seiner Hauptfrucht (die mit dem größten Gewicht).</summary>
    private static readonly Dictionary<string, string> Fruchtfarben = new()
    {
        ["weizen"] = "#E3CB7A", ["roggen"] = "#D6BE7E", ["gerste"] = "#E0D39A", ["hafer"] = "#CFCB92", ["flachs"] = "#A9B8CF", ["brache"] = "#B9C99A",
    };

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
        Zeile($"<rect x=\"{F(x0)}\" y=\"{F(z0)}\" width=\"{F(x1 - x0)}\" height=\"{F(z1 - z0)}\" fill=\"{Farben["rand"]}\"/>");
        var spielfeld = e.Kartengrenzen.Spielfeld;
        Zeile($"<rect x=\"{F(spielfeld.X0)}\" y=\"{F(spielfeld.Z0)}\" width=\"{F(spielfeld.Breite)}\" height=\"{F(spielfeld.Tiefe)}\" fill=\"{Farben["boden"]}\"/>");

        Bodentexturen(e, Zeile);
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
        // Felder: Fläche in der Farbe der Hauptfrucht, darüber die Streifen in ihrer Richtung (ein Muster je Feld), Aussparungen als Maske
        for (var i = 0; i < e.Felder.Count; i++)
        {
            var f = e.Felder[i];
            if (f.Ecken.Count < 3) continue;
            var frucht = f.Fruechte.OrderByDescending(x => x.Value).Select(x => x.Key).FirstOrDefault() ?? "";
            var w = Math.Max(1, f.Streifen);
            // Muster aus senkrechten Linien (entlang y = z); rotate(−richtung) dreht sie in die Richtung der Streifen (sin, cos)
            Zeile($"<defs><pattern id=\"feld{i}\" patternUnits=\"userSpaceOnUse\" width=\"{F(w)}\" height=\"{F(w)}\" patternTransform=\"rotate({F(-f.Richtung)})\">"
                  + $"<line x1=\"0\" y1=\"0\" x2=\"0\" y2=\"{F(w)}\" stroke=\"#8C7A4F\" stroke-width=\"0.4\"/></pattern></defs>");
            var maske = "";
            if (f.Aussparungen.Count > 0)
            {
                Zeile($"<mask id=\"feldmaske{i}\"><rect x=\"{F(x0)}\" y=\"{F(z0)}\" width=\"{F(x1 - x0)}\" height=\"{F(z1 - z0)}\" fill=\"#fff\"/>"
                      + string.Concat(f.Aussparungen.Select(a => $"<polygon points=\"{Punkte(a)}\" fill=\"#000\"/>")) + "</mask>");
                maske = $" mask=\"url(#feldmaske{i})\"";
            }
            Zeile($"<polygon points=\"{Punkte(f.Ecken)}\" fill=\"{Fruchtfarben.GetValueOrDefault(frucht, Farben["feld"])}\" opacity=\"0.8\"{maske}/>");
            Zeile($"<polygon points=\"{Punkte(f.Ecken)}\" fill=\"url(#feld{i})\"{maske}/>");
        }
        Hoehenlinien(e, Zeile);
        foreach (var z in e.Bauzonen.Where(z => z.Ecken.Count >= 3))
            Zeile($"<polygon points=\"{Punkte(z.Ecken)}\" fill=\"{Zonenfarben[z.Art]}\" stroke=\"{Farben["zonenrand"]}\" stroke-width=\"0.4\"/>");
        Wasser(e, Zeile);
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
        Uebergaenge(e, Zeile);
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
        // die Kante des Spielfelds: dahinter der Randstreifen (nicht bespielbar)
        Zeile($"<rect x=\"{F(spielfeld.X0)}\" y=\"{F(spielfeld.Z0)}\" width=\"{F(spielfeld.Breite)}\" height=\"{F(spielfeld.Tiefe)}\" fill=\"none\" " +
              $"stroke=\"{Farben["zonenrand"]}\" stroke-width=\"1.2\" stroke-dasharray=\"8 4\"/>");
        Legende(e, x0, z0, x1 - x0, Zeile);
        if (belegung) Belegung(e, Zeile);
        Zeile("</svg>");
        return o.ToString();
    }

    /// <summary>
    /// Das Wasser aus der Wasserform: je Fluss ein Band mit der Breite je Punkt (linkes Ufer hin, rechtes zurück) und ein Pfeil in der Mitte
    /// in Fließrichtung, je See die Fläche.
    /// </summary>
    private static void Wasser(Kartenentwurf e, Action<string> zeile)
    {
        var form = e.Wasserform;
        foreach (var s in form.Seen)
            zeile($"<polygon points=\"{Punkte(s.Umriss)}\" fill=\"{Farben["wasser"]}\" stroke=\"{Farben["wasserlinie"]}\" stroke-width=\"0.6\"/>");
        foreach (var l in form.Laeufe)
        {
            // jeden zweiten Meter ein Punkt genügt fürs Bild
            var punkte = l.Punkte.Where((_, i) => i % 2 == 0 || i == l.Punkte.Count - 1).ToList();
            Kartenort Ufer(Wasserpunkt w, int seite) => new(w.Ort.X - w.RichtungZ * w.Breite / 2 * seite, w.Ort.Z + w.RichtungX * w.Breite / 2 * seite);
            var band = punkte.Select(w => Ufer(w, 1)).Concat(punkte.AsEnumerable().Reverse().Select(w => Ufer(w, -1)));
            zeile($"<polygon points=\"{Punkte(band)}\" fill=\"{Farben["wasser"]}\" stroke=\"{Farben["wasserlinie"]}\" stroke-width=\"0.4\" stroke-linejoin=\"round\"/>");
            // Pfeil in Fließrichtung in der Mitte des Laufs
            var m = l.AnStelle(l.Laenge / 2);
            var g = Math.Clamp(m.Breite, 3, 12);
            var (vx, vz, qx, qz) = (m.RichtungX * g, m.RichtungZ * g, -m.RichtungZ * g * 0.5, m.RichtungX * g * 0.5);
            zeile($"<polygon points=\"{F(m.Ort.X + vx)},{F(m.Ort.Z + vz)} {F(m.Ort.X - vx + qx)},{F(m.Ort.Z - vz + qz)} {F(m.Ort.X - vx - qx)},{F(m.Ort.Z - vz - qz)}\" " +
                  $"fill=\"{Farben["wasserlinie"]}\" opacity=\"0.7\"/>");
        }
    }

    /// <summary>Brücken (Holz braun, Stein grau, Pfeiler als Punkte), Furten (Trittsteine), Fähren (gestrichelt mit Stegen), alte Brückeneinträge (dick).</summary>
    private static void Uebergaenge(Kartenentwurf e, Action<string> zeile)
    {
        var plan = e.Wasserplan;
        foreach (var q in plan.Querungen)
        {
            if (q.Art == Uebergangsart.Furt)
            {
                var n = Math.Max(2, (int)Math.Round(q.Spannweite / 1.2));
                for (var i = 0; i <= n; i++)
                {
                    var p = new Kartenort(q.Anfang.X + (q.Ende.X - q.Anfang.X) * i / n, q.Anfang.Z + (q.Ende.Z - q.Anfang.Z) * i / n);
                    zeile($"<circle cx=\"{F(p.X)}\" cy=\"{F(p.Z)}\" r=\"0.45\" fill=\"#8E8F88\" stroke=\"#1F2326\" stroke-width=\"0.15\"/>");
                }
                continue;
            }
            var farbe = q.Art == Uebergangsart.Holzbruecke ? "#8A5A2B" : "#7D7F78";
            zeile($"<polyline points=\"{Punkte(new[] { q.Anfang, q.Ende })}\" fill=\"none\" stroke=\"#1F2326\" stroke-width=\"{F(q.Breite + 1.2)}\"/>" +
                  $"<polyline points=\"{Punkte(new[] { q.Anfang, q.Ende })}\" fill=\"none\" stroke=\"{farbe}\" stroke-width=\"{F(q.Breite + 0.4)}\"/>");
            foreach (var p in q.Pfeiler) zeile($"<circle cx=\"{F(p.Ort.X)}\" cy=\"{F(p.Ort.Z)}\" r=\"0.6\" fill=\"#1F2326\"/>");
        }
        foreach (var f in plan.Faehren)
        {
            zeile($"<polyline points=\"{Punkte(new[] { f.Von, f.Bis })}\" fill=\"none\" stroke=\"#1F2326\" stroke-width=\"1.2\" stroke-dasharray=\"4 3\"/>");
            foreach (var a in new[] { f.Von, f.Bis })
                zeile($"<circle cx=\"{F(a.X)}\" cy=\"{F(a.Z)}\" r=\"1.6\" fill=\"#6B4E31\" stroke=\"#1F2326\" stroke-width=\"0.3\"/>");
        }
        foreach (var u in e.Faehren.Where(u => !u.IstFaehre))
            zeile($"<polyline points=\"{Punkte(new[] { u.Von, u.Bis })}\" fill=\"none\" stroke=\"#1F2326\" stroke-width=\"4\"/>");
    }

    /// <summary>Kantenlänge der Felder, in denen die Vorschau die Bodentexturen zeigt (Meter).</summary>
    public const double Bodenfeld = 4;

    /// <summary>
    /// Die gemalten Bodentexturen: je 4-m-Feld die stärkste Textur (in der Mitte des Feldes) in ihrer Farbe aus dem Katalog, gleiche Felder
    /// einer Zeile zu einem Streifen zusammengefasst. Die Wiese bleibt ungezeichnet (der Grund).
    /// </summary>
    private static void Bodentexturen(Kartenentwurf e, Action<string> zeile)
    {
        var boden = e.Boden;
        var aussen = e.Kartengrenzen.Aussen;
        foreach (var k in boden.Kacheln.OrderBy(k => k.J).ThenBy(k => k.I))
        {
            var r = Bodenraster.KachelBereich(k);
            var ende = r.X0 + Bodenraster.Kachel * Bodenraster.Abstand;
            for (var z = r.Z0; z < r.Z0 + Bodenraster.Kachel; z += Bodenfeld)
            {
                (Bodentextur T, double X)? lauf = null;
                void Schliessen(double bis)
                {
                    if (lauf is not { } l || l.T == Bodentextur.Wiese) return;
                    var (a, b) = (Math.Max(l.X, aussen.X0), Math.Min(bis, aussen.X1));
                    var (c, d) = (Math.Max(z, aussen.Z0), Math.Min(z + Bodenfeld, aussen.Z1));
                    if (b <= a || d <= c) return;
                    zeile($"<rect x=\"{F(a)}\" y=\"{F(c)}\" width=\"{F(b - a)}\" height=\"{F(d - c)}\" fill=\"{e.Katalog.TexturDaten(l.T)?.Farbe ?? "#888"}\" opacity=\"0.7\"/>");
                }
                for (var x = r.X0; x < ende; x += Bodenfeld)
                {
                    var t = boden.Textur(x + Bodenfeld / 2, z + Bodenfeld / 2);
                    if (lauf?.T == t) continue;
                    Schliessen(x);
                    lauf = (t, x);
                }
                Schliessen(ende);
            }
        }
    }

    /// <summary>Abstand der Punkte, an denen die Höhenlinien gerechnet werden (Meter).</summary>
    public const double Linienraster = 8;

    /// <summary>
    /// Höhenlinien des Bodens (Gelände mit dem Bett der Wege): auf einem Raster von 8 m die Höhen lesen, je Zelle die Linien jeder Höhenstufe
    /// durch die Zelle legen (Marching Squares; ein Sattel entscheidet sich nach dem Mittel der vier Ecken). Je Stufe ein Pfad. Flach: nichts.
    /// </summary>
    private static void Hoehenlinien(Kartenentwurf e, Action<string> zeile)
    {
        var g = e.Gelaendeform;
        if (!g.HatHoehen) return; // ohne Höhen nur das Bett der Gewässer – das zeigt das Wasser selbst
        var a = e.Kartengrenzen.Aussen;
        var (nx, nz) = ((int)Math.Ceiling(a.Breite / Linienraster - 1e-9) + 1, (int)Math.Ceiling(a.Tiefe / Linienraster - 1e-9) + 1);
        double X(int i) => Math.Min(a.X0 + i * Linienraster, a.X1);
        double Z(int j) => Math.Min(a.Z0 + j * Linienraster, a.Z1);
        var h = new double[nx, nz];
        double min = double.MaxValue, max = double.MinValue;
        for (var i = 0; i < nx; i++)
            for (var j = 0; j < nz; j++)
            {
                h[i, j] = g.Hoehe(X(i), Z(j));
                (min, max) = (Math.Min(min, h[i, j]), Math.Max(max, h[i, j]));
            }
        if (max - min < 0.5) return;
        var abstand = max - min > 80 ? 5.0 : 2.0;
        var stuecke = new SortedDictionary<int, List<(string, string)>>();
        for (var i = 0; i + 1 < nx; i++)
            for (var j = 0; j + 1 < nz; j++)
            {
                // Ecken im Uhrzeigersinn ab oben links, Kanten 0: oben, 1: rechts, 2: unten, 3: links
                var ecken = new[] { (X(i), Z(j), h[i, j]), (X(i + 1), Z(j), h[i + 1, j]), (X(i + 1), Z(j + 1), h[i + 1, j + 1]), (X(i), Z(j + 1), h[i, j + 1]) };
                var (lo, hi) = (ecken.Min(c => c.Item3), ecken.Max(c => c.Item3));
                for (var stufe = (int)Math.Ceiling(lo / abstand); stufe * abstand <= hi; stufe++)
                {
                    var l = stufe * abstand;
                    var schnitt = new (double X, double Z)?[4];
                    for (var k = 0; k < 4; k++)
                    {
                        var (p, q) = (ecken[k], ecken[(k + 1) % 4]);
                        if ((p.Item3 >= l) == (q.Item3 >= l)) continue;
                        var t = (l - p.Item3) / (q.Item3 - p.Item3);
                        schnitt[k] = (p.Item1 + (q.Item1 - p.Item1) * t, p.Item2 + (q.Item2 - p.Item2) * t);
                    }
                    var paare = new List<(int, int)>();
                    var da = Enumerable.Range(0, 4).Where(k => schnitt[k] is not null).ToList();
                    if (da.Count == 2) paare.Add((da[0], da[1]));
                    else if (da.Count == 4)
                    {
                        // Sattel: Liegt die Mitte auf der Seite der Ecke oben links, hängen sie durch die Mitte zusammen
                        var mitte = ecken.Average(c => c.Item3) >= l;
                        if (mitte == ecken[0].Item3 >= l) paare.AddRange(new[] { (0, 1), (2, 3) });
                        else paare.AddRange(new[] { (3, 0), (1, 2) });
                    }
                    if (paare.Count == 0) continue;
                    if (!stuecke.TryGetValue(stufe, out var liste)) stuecke[stufe] = liste = new List<(string, string)>();
                    foreach (var (k1, k2) in paare)
                        liste.Add(($"{G(schnitt[k1]!.Value.X)} {G(schnitt[k1]!.Value.Z)}", $"{G(schnitt[k2]!.Value.X)} {G(schnitt[k2]!.Value.Z)}"));
                }
            }
        foreach (var (stufe, liste) in stuecke)
        {
            var kraeftig = Math.Abs(stufe * abstand % 10) < 1e-9;
            zeile($"<path d=\"{Verketten(liste)}\" fill=\"none\" stroke=\"#8C6A43\" stroke-width=\"{(kraeftig ? "0.9" : "0.4")}\" opacity=\"{(kraeftig ? "0.7" : "0.5")}\"/>");
        }

        static string G(double x) => (Math.Round(x, 1) + 0.0).ToString("0.#", CultureInfo.InvariantCulture);
    }

    /// <summary>Hängt Linienstücke mit gemeinsamen Enden zu Linienzügen zusammen (kürzerer Pfad): „M x z L x z x z …“ je Zug.</summary>
    private static string Verketten(List<(string A, string B)> stuecke)
    {
        var an = new Dictionary<string, List<int>>();
        for (var k = 0; k < stuecke.Count; k++)
            foreach (var p in new[] { stuecke[k].A, stuecke[k].B })
            {
                if (!an.TryGetValue(p, out var l)) an[p] = l = new List<int>();
                l.Add(k);
            }
        var benutzt = new bool[stuecke.Count];
        string? Weiter(string p)
        {
            foreach (var k in an[p])
                if (!benutzt[k])
                {
                    benutzt[k] = true;
                    return stuecke[k].A == p ? stuecke[k].B : stuecke[k].A;
                }
            return null;
        }
        var d = new StringBuilder();
        for (var k = 0; k < stuecke.Count; k++)
        {
            if (benutzt[k]) continue;
            benutzt[k] = true;
            var zug = new LinkedList<string>(new[] { stuecke[k].A, stuecke[k].B });
            for (var p = Weiter(zug.Last!.Value); p is not null; p = Weiter(p)) zug.AddLast(p);
            for (var p = Weiter(zug.First!.Value); p is not null; p = Weiter(p)) zug.AddFirst(p);
            d.Append('M').Append(zug.First!.Value).Append('L').Append(string.Join(" ", zug.Skip(1)));
        }
        return d.ToString();
    }

    /// <summary>Wie weit das Bild reicht: die ganze Karte, Spielfeld und Randstreifen (seit Schritt 5a hat jede Karte eine Größe).</summary>
    private static (double X0, double Z0, double X1, double Z1) Grenzen(Kartenentwurf e)
    {
        var a = e.Kartengrenzen.Aussen;
        return (a.X0, a.Z0, a.X1, a.Z1);
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
