using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Der Weg der Karren eines Betriebs – eine Strecke für Kern und Bild (31.10.2026, Datenversion 65)
//
//  Bis dahin rechnete der Kern die Fahrzeit vom nächsten Punkt einer Karrenstraße bis zur Mitte des Marktplatzes, das Bild fuhr den Karren
//  vom Hof über alle Gassen bis zum Halt am Rand des Pflasters – in Köln war der Karren im Bild ein Drittel früher da. Jetzt rechnet der Kern
//  die ganze Strecke, und die Darstellung fährt genau sie (Stadtwirtschaft.Karrenverlauf):
//
//    1. Start am Halteplatz des Modells („Anker_Karren“, im Katalog als karrenAnker): bei einer Werkstatt seitlich am Tor, im Umland vor dem Hof.
//    2. Bei einer Werkstatt zum Hof (Mitte des Vorplatzes, 0,5 m vor der Vorderkante), dort beginnt ihr Hofweg.
//    3. Gerade zum nächsten Weg der Karte (der Zubringer, wie ihn die Darstellung als Hofweg zeichnet) – jeder Klasse, auch Gasse und Fußweg.
//    4. Über das Netz aller Wege (Karrennetz, Gassen und Fußwege teurer) zur Mitte des Marktplatzes …
//    5. … und dort abgeschnitten, wo der Karren den Halt erreicht: 3,5 m vor dem Pflaster (die Zugtiere stehen am Rand), gemessen wie im Bild
//       in den Achsen des Platzes (ein Quadrat mit der halben kürzeren Seite des Platzes plus 3,5 m).
//
//  Zurück fährt der Karren denselben Weg rückwärts. Die Stücke 1 bis 3 gehören zu keinem Weg der Karte (Belag wie der Bezug, kein Verkehr
//  für den Wegebau, kein Ausbau).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Der Weg der Karren eines Betriebs (siehe Kopf der Datei): Stücke hin und zurück, der Verlauf vom Start bis zum Halt und der Marktplatz.</summary>
/// <param name="Hin">Die Stücke vom Start zum Halt am Markt.</param>
/// <param name="Zurueck">Dieselben rückwärts.</param>
/// <param name="Punkte">Der Verlauf: Stück i führt von Punkte[i] nach Punkte[i + 1] (leer ohne Weg).</param>
/// <param name="Markt">Der Marktplatz (null, wenn die Karte keinen hat: dann der Ursprung).</param>
public sealed record Karrenweg(IReadOnlyList<Fahrtstueck> Hin, IReadOnlyList<Fahrtstueck> Zurueck, IReadOnlyList<Kartenort> Punkte, Kartengebaeude? Markt)
{
    /// <summary>So weit vor dem Pflaster halten die Karren (Meter): Die Zugtiere stehen am Rand, der Karren dahinter.</summary>
    public const double HaltVorPflaster = 3.5;

    /// <summary>So weit innen im Vorplatz (vor der Vorderkante der Fläche) liegt der Hof einer Werkstatt (Meter) – hier beginnt ihr Hofweg.</summary>
    public const double HofVorKante = 0.5;

    /// <summary>
    /// Wo die Karren eines Betriebs halten und losfahren (<c>Anker_Karren</c> des Modells, <see cref="Baustein.KarrenAnker"/>) und wo sein Hof liegt
    /// (dort beginnt der Zubringer zum nächsten Weg). Werkstatt: Modell des Berufs in der Mitte der Fläche, gedreht wie sie, ohne Maßstab; der Hof in
    /// der Mitte des Vorplatzes. Umland: das Modell des Platzes (Ursprung mit Versatz, Drehung und Maßstab wie in der Karte); Hof und Anker sind
    /// dasselbe. Ohne Anker im Katalog beginnt der Weg am Hof bzw. am Ursprung des Modells.
    /// </summary>
    public static (Kartenort Anker, Kartenort Hof) Start(Betriebsplatz platz, Betrieb betrieb, Gebaeudekatalog? katalog)
    {
        var (x, z) = platz.Punkt;
        var drehung = platz.DrehungGrad;
        Kartenort Bei(Kartenort ursprung, double lx, double lz)
        {
            var d = Kartengeometrie.Dreh(lx, lz, drehung);
            return new Kartenort(ursprung.X + d.X, ursprung.Z + d.Z);
        }
        var mitte = new Kartenort(x, z);
        if (betrieb.InDerStadt)
        {
            var tiefe = platz.Gebaeude is { } g ? (g.Baustein?.Groesse is { Length: 2 } gr ? gr[1] * g.Massstab : 0) : platz.Grundstueck!.Grundriss.Tiefe;
            var hof = Bei(mitte, 0, tiefe / 2 - HofVorKante);
            return (katalog?.Werkstatt(betrieb.Artdaten.Beruf)?.KarrenAnker is { Length: 2 } a ? Bei(mitte, a[0], a[1]) : hof, hof);
        }
        var (baustein, massstab) = platz.Gebaeude is { Baustein: { } b } geb ? (b, geb.Massstab) : (katalog?.Umland(betrieb.Art), 1.0);
        var ursprung = platz.Gebaeude is { Baustein: { Versatz: { Length: 2 } v } } ? Bei(mitte, v[0] * massstab, v[1] * massstab) : mitte;
        var anker = baustein?.KarrenAnker is { Length: 2 } k ? Bei(ursprung, k[0] * massstab, k[1] * massstab) : ursprung;
        return (anker, anker);
    }

    /// <summary>
    /// Der Halt am Marktplatz: Ursprung des Platzes (mit Versatz), seine Drehung und die halbe Seite des Quadrats, an dessen Rand die Karren halten
    /// (halbe kürzere Seite des Platzes plus <see cref="HaltVorPflaster"/>).
    /// </summary>
    public static (Kartenort Mitte, double DrehungGrad, double Halb) Halt(Kartengebaeude markt)
    {
        var m = markt.Massstab;
        var mitte = new Kartenort(markt.Punkt[0], markt.Punkt[1]);
        if (markt.Baustein?.Versatz is { Length: 2 } v)
        {
            var d = Kartengeometrie.Dreh(v[0] * m, v[1] * m, markt.Drehung);
            mitte = new Kartenort(mitte.X + d.X, mitte.Z + d.Z);
        }
        var halb = markt.Baustein?.Groesse is { Length: 2 } g ? Math.Min(g[0], g[1]) * m / 2 : 0;
        return (mitte, markt.Drehung, halb + HaltVorPflaster);
    }

    /// <summary>Wie weit ein Punkt in den Achsen des Platzes von seiner Mitte liegt (der größere der beiden Abstände, Meter).</summary>
    public static double Randabstand(Kartenort p, (Kartenort Mitte, double DrehungGrad, double Halb) halt)
    {
        var l = Kartengeometrie.Dreh(p.X - halt.Mitte.X, p.Z - halt.Mitte.Z, -halt.DrehungGrad);
        return Math.Max(Math.Abs(l.X), Math.Abs(l.Z));
    }

    /// <summary>
    /// Die Fahrt bis zum Halt: abgeschnitten, wo sie zum ersten Mal das Quadrat des Halts betritt (das Stück dort anteilig, Länge und Anstieg).
    /// Beginnt sie schon darin (ein Betrieb am Platz) oder betritt sie es nie, bleibt sie, wie sie ist.
    /// </summary>
    public static Netzfahrt BisZumHalt(Netzfahrt fahrt, (Kartenort Mitte, double DrehungGrad, double Halb) halt)
    {
        var p = fahrt.Punkte;
        if (p.Count < 2 || Randabstand(p[0], halt) < halt.Halb) return fahrt;
        for (var i = 0; i + 1 < p.Count; i++)
        {
            if (Randabstand(p[i + 1], halt) >= halt.Halb) continue;
            // zwischen p[i] (draußen) und p[i + 1] (drinnen): wo wird der Rand erreicht? (halbiert, nur Grundrechenarten – auf jedem Rechner gleich)
            double klein = 0, gross = 1;
            for (var n = 0; n < 50; n++)
            {
                var t = (klein + gross) / 2;
                if (Randabstand(Zwischen(p[i], p[i + 1], t), halt) >= halt.Halb) klein = t; else gross = t;
            }
            var stuecke = fahrt.Stuecke.Take(i).ToList();
            var punkte = p.Take(i + 1).ToList();
            var s = fahrt.Stuecke[i];
            // (liegt der Rand genau auf p[i], fällt das Stück weg – außer es wäre das einzige: dann ein Stück der Länge 0, die Mindestdauer)
            if (s.Laenge * klein > 1e-9 || stuecke.Count == 0)
            {
                stuecke.Add(s with { Laenge = s.Laenge * klein, Anstieg = s.Anstieg * klein });
                punkte.Add(Zwischen(p[i], p[i + 1], klein));
            }
            return new Netzfahrt(stuecke, punkte);
        }
        return fahrt;
    }

    /// <summary>
    /// Die Fahrt über das Netz mit den Stücken davor: vom Anker zum Hof und vom Hof gerade zum Anschluss am Netz (dem ersten Punkt der Fahrt).
    /// Anstieg nach dem Boden (<see cref="Gelaendeform.Hoehe"/>); die Stücke gehören zu keinem Weg der Karte. Ohne Fahrt: leer.
    /// </summary>
    public static Netzfahrt MitZubringer(Netzfahrt fahrt, Kartenort anker, Kartenort hof, Gelaendeform gelaende)
    {
        if (fahrt.Stuecke.Count == 0) return Netzfahrt.Leer;
        var stuecke = new List<Fahrtstueck>();
        var punkte = new List<Kartenort> { anker };
        foreach (var bis in new[] { hof, fahrt.Punkte[0] })
        {
            var von = punkte[^1];
            var laenge = von.Abstand(bis);
            if (laenge <= 1e-9) continue;
            stuecke.Add(new Fahrtstueck(laenge, gelaende.Hoehe(bis.X, bis.Z) - gelaende.Hoehe(von.X, von.Z)));
            punkte.Add(bis);
        }
        stuecke.AddRange(fahrt.Stuecke);
        punkte.AddRange(fahrt.Punkte.Skip(1));
        return new Netzfahrt(stuecke, punkte);
    }

    /// <summary>Dieselbe Fahrt rückwärts: die Stücke in umgekehrter Folge, jeder Anstieg mit umgekehrtem Vorzeichen.</summary>
    public static IReadOnlyList<Fahrtstueck> Umgekehrt(IReadOnlyList<Fahrtstueck> fahrt) =>
        fahrt.Reverse().Select(s => s with { Anstieg = -s.Anstieg }).ToList();

    private static Kartenort Zwischen(Kartenort a, Kartenort b, double t) => new(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
}
