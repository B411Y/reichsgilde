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
//       in den Achsen des Platzes (ein Quadrat mit der halben kürzeren Seite des Platzes plus 3,5 m). Steht der Karren am Halteplatz schon
//       darin (ein Betrieb am Platz), hält er sofort: ein Stück der Länge 0, die Mindestdauer.
//    Liegt der Hof näher am Pflaster eines Platzes als am nächsten Weg, führt der Zubringer wie im Bild gerade zur Mitte dieses Platzes
//    (Stadtwirtschaft.StreckeWieImBild; die Darstellung hängt einen solchen Hof über das Pflaster an, AnWegAnschliessen).
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

    /// <summary>
    /// Wie weit ein Punkt vom Pflaster eines Marktplatzes liegt (0 darauf; in den Achsen des Platzes, Quadrat mit der halben kürzeren Seite) –
    /// wie <c>MarktplatzAnsicht.Abstand</c> der Darstellung.
    /// </summary>
    public static double Pflasterabstand(Kartenort p, Kartengebaeude markt)
    {
        var halt = Halt(markt);
        var halb = halt.Halb - HaltVorPflaster;
        var l = Kartengeometrie.Dreh(p.X - halt.Mitte.X, p.Z - halt.Mitte.Z, -halt.DrehungGrad);
        var (dx, dz) = (Math.Max(0, Math.Abs(l.X) - halb), Math.Max(0, Math.Abs(l.Z) - halb));
        return Math.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Wie weit ein Punkt in den Achsen des Platzes von seiner Mitte liegt (der größere der beiden Abstände, Meter).</summary>
    public static double Randabstand(Kartenort p, (Kartenort Mitte, double DrehungGrad, double Halb) halt)
    {
        var l = Kartengeometrie.Dreh(p.X - halt.Mitte.X, p.Z - halt.Mitte.Z, -halt.DrehungGrad);
        return Math.Max(Math.Abs(l.X), Math.Abs(l.Z));
    }

    /// <summary>
    /// Die Fahrt bis zum Halt: abgeschnitten, wo sie zum ersten Mal das Quadrat des Halts betritt (das Stück dort anteilig, Länge und Anstieg).
    /// Beginnt sie schon darin (ein Betrieb am Platz), hält der Karren sofort: ein Stück der Länge 0 (die Mindestdauer) – sonst fuhre er von dort
    /// erst zum nächsten Weg und außen herum bis zur Platzmitte (Befund der Prüfung). Betritt sie es nie, bleibt sie, wie sie ist. Ohne Fahrt: leer.
    /// </summary>
    public static Netzfahrt BisZumHalt(Netzfahrt fahrt, (Kartenort Mitte, double DrehungGrad, double Halb) halt)
    {
        var p = fahrt.Punkte;
        if (p.Count < 2) return fahrt;
        if (Randabstand(p[0], halt) < halt.Halb) return new Netzfahrt(new[] { new Fahrtstueck(0, 0) }, new[] { p[0], p[0] });
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
    /// Wie im Bild (<c>AnWegAnschliessen</c>): Liegt der Hof näher am Pflaster eines Platzes als am nächsten Weg (<paramref name="abstandZumWeg"/>, null:
    /// kein Weg), fährt der Karren gerade dorthin – vom Halteplatz über den Hof zur Mitte des nächsten Platzes (bei gleichem Abstand der erste der
    /// Karte), bis zu dessen Halt. Null, wenn ein Weg näher (oder gleich nah) liegt oder kein Platz eine Größe hat.
    /// </summary>
    public static (Netzfahrt Fahrt, Kartengebaeude Markt)? UeberDasPflaster(Kartenort anker, Kartenort hof, IReadOnlyList<Kartengebaeude> maerkte,
        double? abstandZumWeg, Gelaendeform gelaende)
    {
        Kartengebaeude? nah = null;
        var abstand = double.MaxValue;
        foreach (var m in maerkte)
        {
            if (m.Baustein?.Groesse is not { Length: 2 }) continue;
            var a = Pflasterabstand(hof, m);
            if (a < abstand) (nah, abstand) = (m, a);
        }
        if (nah is null || abstand >= (abstandZumWeg ?? double.MaxValue)) return null;
        var fahrt = BisZumHalt(Gerade(new[] { anker, hof, new Kartenort(nah.Punkt[0], nah.Punkt[1]) }, gelaende), Halt(nah));
        return fahrt.Stuecke.Count > 0 ? (fahrt, nah) : null;
    }

    /// <summary>
    /// Die Fahrt über das Netz mit den Stücken davor: vom Anker zum Hof und vom Hof gerade zum Anschluss am Netz (dem ersten Punkt der Fahrt).
    /// Anstieg nach dem Boden (<see cref="Gelaendeform.Hoehe"/>); die Stücke gehören zu keinem Weg der Karte. Ohne Fahrt: leer.
    /// </summary>
    public static Netzfahrt MitZubringer(Netzfahrt fahrt, Kartenort anker, Kartenort hof, Gelaendeform gelaende)
    {
        if (fahrt.Stuecke.Count == 0) return Netzfahrt.Leer;
        var zubringer = Gerade(new[] { anker, hof, fahrt.Punkte[0] }, gelaende);
        return new Netzfahrt(zubringer.Stuecke.Concat(fahrt.Stuecke).ToList(), zubringer.Punkte.Concat(fahrt.Punkte.Skip(1)).ToList());
    }

    /// <summary>
    /// Gerade Stücke von Punkt zu Punkt, die zu keinem Weg der Karte gehören (Zubringer); Anstieg nach dem Boden. Punkte, die auf dem vorigen liegen,
    /// fallen weg; der erste bleibt immer (auch wenn kein Stück entsteht).
    /// </summary>
    public static Netzfahrt Gerade(IReadOnlyList<Kartenort> punkte, Gelaendeform gelaende)
    {
        var stuecke = new List<Fahrtstueck>();
        var liste = new List<Kartenort> { punkte[0] };
        foreach (var bis in punkte.Skip(1))
        {
            var von = liste[^1];
            var laenge = von.Abstand(bis);
            if (laenge <= 1e-9) continue;
            stuecke.Add(new Fahrtstueck(laenge, gelaende.Hoehe(bis.X, bis.Z) - gelaende.Hoehe(von.X, von.Z)));
            liste.Add(bis);
        }
        return new Netzfahrt(stuecke, liste);
    }

    /// <summary>Dieselbe Fahrt rückwärts: die Stücke in umgekehrter Folge, jeder Anstieg mit umgekehrtem Vorzeichen.</summary>
    public static IReadOnlyList<Fahrtstueck> Umgekehrt(IReadOnlyList<Fahrtstueck> fahrt) =>
        fahrt.Reverse().Select(s => s with { Anstieg = -s.Anstieg }).ToList();

    private static Kartenort Zwischen(Kartenort a, Kartenort b, double t) => new(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
}
