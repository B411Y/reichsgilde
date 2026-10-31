using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Kartenbau.Staedte;

// ═════════════════════════════════════════════════════════════════════════════
//  Köln – die erste Startstadt (PR #213, „Köln richtig bauen“)
//
//  Aufruf: dotnet run --project tools/Kartenbau -- bauen koeln --ueberschreiben   (schreibt game/karten/koeln.json und docs/karten/koeln*.svg)
//  Spielen: Godot mit -- --karte=koeln
//
//  Erkennbar und spielbar vereinfacht (Entscheidung des Nutzers, 26.10.2026): Die Wahrzeichen stehen an ungefähr richtiger Lage, die
//  Stadt ist auf etwa ein Zehntel verkleinert. Meter auf dem Boden, x = Osten, z = Süden, der Ursprung in der Altstadt (bis 31.10.2026 lag dort
//  der Heumarkt; heute kreuzen sich dort Severinstraße, Schildergasse und Brückenstraße).
//
//    • Rhein: fließt nach Norden, 60–100 m breit, läuft von Außenkante zu Außenkante; sein linkes Ufer liegt bei x = 60. Am linken Ufer
//      Rheingasse, Hafen mit Kran, Zollstelle und Stapelhaus, davor Leinpfade; Rheinbrücke (Fiktion) und Fähre Mülheim.
//    • Deutz am rechten Ufer mit kleiner Mauer (Nord-, Ost- und Südtor), Deutzer Freiheit, Vorstadt.
//    • Stadtmauer: Halbkreis (Mitte 24 | 0, Radius 150 m) zum Rhein hin offen, Tore Eigelstein, Ehren, Hahnen, Weyer, Severin und die
//      Ufertore Kunibert und Bayen; innen die Wallstraßen.
//    • Märkte (Entscheidung des Nutzers 31.10.2026, ein Stadtmarkt mit drei Plätzen): zwei innerhalb der Mauer an ihren Enden am Rhein – im
//      Norden hinter dem Kunibertstor, im Süden hinter dem Bayentor –, der dritte in Deutz an der Kreuzung von Deutzer Freiheit und Kirchstraße.
//      Namen wie in Köln, nur in `Maerkte` gepflegt (Kommentare hier nennen die Plätze nach der Lage). Rathaus, Groß St. Martin, St. Gereon, St. Severin,
//      St. Aposteln, St. Kunibert, Dom-Baustelle im Norden. Hauptstraßen mit echten Namen, Nebengassen mit erfundenen kölschen.
//    • Spiel: acht Startgrundstücke (Spieler und sieben KI-Häuser) an den Straßen, die auf die Marktplätze führen, je 36 m vom nächsten (der
//      Werkstattweg vom Halteplatz bis zum Halt am Pflaster 20–24 m, der Spieler 30 Minuten wie die alte feste Fahrzeit): der Spieler und zwei
//      KI-Häuser am Südplatz, zwei am Nordplatz, drei in Deutz. Einwohnerplätze, Bauzonen aus dem Raster (alles, was frei ist, siehe
//      Koeln.Zonen.cs), Umlandbetriebe an den Landstraßen (die der KI-Häuser an den Straßen nahe den Märkten), Vorkommen.
//    • Gelände: Terrasse der Altstadt mit Hochufer zum Rhein, sanft ansteigend nach Westen (Ville), Bergisches Land im Osten.
//    • Rand (300 m): Kölner Bucht (Felder), Ville und Königsforst (Wald), im Dunst.
// ═════════════════════════════════════════════════════════════════════════════

public sealed partial class Koeln : Stadtskript
{
    public override string Kennung => "koeln";
    public override string Stadt => "Köln";
    public override string Beschreibung => "Köln: Rhein, Mauerhalbkreis mit Toren, drei Marktplätze, Dom-Baustelle, Hafen, Deutz, Gelände, 8 Startgrundstücke, 8 Häuser";
    public override (double Breite, double Tiefe) Groesse => (700, 450);

    // ── Maße ─────────────────────────────────────────────────────────────────
    private const double Cx = 24;       // Mitte des Mauerhalbkreises (x); z = 0
    private const double R = 150;       // Radius der Stadtmauer
    private const double WallR = 138;   // Radius der Wallstraße innerhalb der Mauer
    private const double Ufer = 60;     // das linke Rheinufer (Wasserlinie) bei x = 60
    private const double RheinX = 37;   // Rheingasse
    private const double HoheX = -34;   // Hohe Straße
    private const double ZielAbstand = 36;   // so weit vom nächsten Marktplatz liegen die Startgrundstücke (±10 %; bis 31.10.2026 96 m vom Heumarkt, dann 28 – seit Kern und Bild dieselbe Strecke rechnen, 36)

    /// <summary>Ein Marktplatz: Name (wie in Köln), wie man „an ihm“ sagt (für den Namen der Werkstatt), Kennung nach der Lage, Mitte.</summary>
    private sealed record Marktort(string Name, string Dativ, string Kennung, Kartenort Ort);

    // Die drei Marktplätze (Entscheidung des Nutzers 31.10.2026): zwei an den Enden des Mauerhalbkreises, wo er auf den Rhein trifft (innen, die
    // Rheingasse läuft über den Platz, die Wallstraße berührt ihn), der dritte in Deutz an der Kreuzung von Deutzer Freiheit und Kirchstraße.
    // Die Namen stehen nur hier – sollen sie die Plätze tauschen, genügt es im Skript, Name und Dativ zu tauschen (die Kennung bleibt bei der
    // Lage; dazu die eine Zeile mit den Namen in MarktplaetzeTests und die Doku, die die Plätze beschreibt).
    // Reihenfolge = Reihenfolge in der Karte: Der erste ist der Platz des Spielers (im Bild „der Markt“, bei Gleichstand der Wege der erste).
    private static readonly Marktort MarktSued = new("Heumarkt", "am Heumarkt", "MarktSued", P(RheinX, R - 25));
    private static readonly Marktort MarktNord = new("Alter Markt", "am Alten Markt", "MarktNord", P(RheinX, -(R - 25)));
    private static readonly Marktort MarktDeutz = new("Neumarkt", "am Neumarkt", "MarktDeutz", P(170, 6));
    private static readonly Marktort[] Maerkte = { MarktSued, MarktNord, MarktDeutz };

    private readonly Dictionary<string, EntwurfWeg> _strassen = new();
    private EntwurfMauer _stadtmauer = null!, _deutzer = null!;

    private static Kartenort Bogen(double grad, double r)
    {
        var a = grad * Math.PI / 180;
        return P(Cx - r * Math.Sin(a), -r * Math.Cos(a));
    }

    protected override void Anlegen()
    {
        E.GroesseSetzen(700, 450, P(-50, 25));
        E.VorlageSetzen("Köln um 1400 (Dom als Baustelle), auf etwa ein Zehntel verkleinert; Lage der Wahrzeichen und Straßennamen nach der Stadt");
        E.FiktionHinzufuegen("Rheinbrücke nach Deutz: Die erste feste Brücke gab es erst 1859; hier gehört sie zum Spielfeld (Fiktion).");
        E.FiktionHinzufuegen("Der Maßstab ist etwa 1 : 10; Märkte, Kirchen und Gassen sind so gerückt, dass sie ins Spiel passen. Nebengassen und Hausnamen sind erfunden.");
        E.FiktionHinzufuegen("Die drei Märkte liegen nicht an ihrer echten Stelle (Heumarkt und Alter Markt nebeneinander am Rathaus, der Neumarkt am Westende der Schildergasse; Deutz hatte keinen): Zwei stehen an den Enden der Mauer am Rhein, einer in Deutz.");
        E.FiktionHinzufuegen("Der Rhein ist 60–100 m breit (in Wirklichkeit 350–400 m); der Duffesbach läuft hier als Graben vor der Südmauer und mündet hinter dem Bayentor.");
        E.FiktionHinzufuegen("Groß St. Martin, St. Gereon, St. Severin, St. Aposteln und St. Kunibert stehen vorerst als Block des allgemeinen Bausteins „Kirche“ (16 × 14 m), bis es romanische Kirchenmodelle gibt.");

        Wasser();
        Gelaende();
        Mauern();
        Strassen();
        Wahrzeichen();
        Startgrundstuecke();
        Einwohnerplaetze();
        Umland();
        Arbeitsstellen();
        KreuzungenVerbinden();
        Bauzonen();
        Natur();
        Boden();
        Start();
        // Der Blick beim Start: zwischen dem Grundstück des Spielers und seinem Platz (bis 31.10.2026 auf die Altstadtmitte bei −6 | −22)
        var spielerPlatz = Maerkte.MinBy(m => m.Ort.Abstand(_startMitten[0]))!.Ort;
        E.KameraStartSetzen(P(Math.Round((_startMitten[0].X + spielerPlatz.X) / 2), Math.Round((_startMitten[0].Z + spielerPlatz.Z) / 2)), 130);
        E.RandVorschlagen(1, ersetzen: false);
    }

    // ── Hilfen ───────────────────────────────────────────────────────────────

    private EntwurfWeg Weg(Wegklasse klasse, string name, params Kartenort[] punkte)
    {
        var w = E.WegAnlegen(klasse, punkte, glatt: false, name: name, einrasten: false);
        _strassen[name] = w;
        return w;
    }

    private EntwurfWeg WegGlatt(Wegklasse klasse, string name, params Kartenort[] punkte)
    {
        var w = E.WegAnlegen(klasse, punkte, glatt: true, name: name, einrasten: false);
        _strassen[name] = w;
        return w;
    }

    /// <summary>Ein Gebäude mit Namen und – wenn es ein Wahrzeichen ist – einer sprechenden Kennung (sonst die laufende des Entwurfs).</summary>
    private EntwurfGebaeude Haus(string katalog, string name, double x, double z, double drehung = 0, string? kennung = null)
    {
        var g = E.Setzen(katalog, x, z, drehung);
        E.Umbenennen(g.Kennung, name);
        if (kennung is not null) E.KennungSetzen(g.Kennung, kennung);
        return g;
    }

    // ── Wasser ───────────────────────────────────────────────────────────────

    private void Wasser()
    {
        // Rhein: fließt nach Norden (die Linie beginnt im Süden), das linke Ufer bleibt bei x = 60, das rechte wandert mit der Breite
        var rhein = new (double Z, double Breite)[]
        {
            (550, 100), (430, 98), (310, 92), (210, 84), (125, 76), (62, 68), (8, 60), (-62, 64), (-140, 72), (-230, 80), (-340, 86), (-500, 90),
        };
        var fluss = E.FlussAnlegen(rhein.Select(r => P(Ufer + r.Breite / 2, r.Z)).ToList(), "fluss", 0, "Rhein");
        for (var i = 0; i < rhein.Length; i++) E.FlussBreiteSetzen(fluss, i, rhein[i].Breite);
        E.FlussStroemungSetzen(fluss, 1.2);

        // Duffesbach: kommt von Westen und läuft als Graben 15 m vor der Südmauer (Radius 165 um die Mitte des Halbkreises) zum Rhein, hinter
        // dem Bayentor in den Strom (bis 31.10.2026 quer durch die Südstadt; dort liegt jetzt der Südplatz). An der Bonner Straße vor dem
        // Severinstor eine Holzbrücke, am Leinpfad eine Furt.
        var bach = E.FlussAnlegen(new[]
        {
            P(-215, 150), P(-150, 130), P(-100, 116), P(-82, 127), P(-60, 142), P(-36, 154), P(-20, 159), P(-4, 163), P(12, 165), P(37, 167),
            P(62, 168), P(90, 169),
        }, "bach", 3, "Duffesbach");
        E.FlussStroemungSetzen(bach, 0.6);
        E.FlussTiefeSetzen(bach, 6, 0.9);   // tief genug für eine Holzbrücke an der Bonner Straße, sonst Furten
    }

    // ── Gelände ──────────────────────────────────────────────────────────────

    private void Gelaende()
    {
        // Terrasse der Altstadt: ein Plateau 4 m über dem Rhein, von dort ein gleichmäßiges Gefälle (4,4 %) bis zum Rhein – das Hochufer.
        // Das Plateau ist ein großer Tupfer (Radius 200 m um −170 | 0), die Rampe legt von x = −60 bis x = 30 die schiefe Ebene darüber,
        // für die ganze Stadt von Norden bis Süden; ab x = 30 liegt alles auf Höhe des Flusses.
        E.GelaendePinseln(new Pinsel(Pinselart.Heben, 200, 4, Kante: 0.12), new[] { P(-170, 0) });
        E.Rampe(P(-60, 0), P(30, 0), 360, 40);
        // nach Westen sanft ansteigend: Kölner Bucht und Ville
        Huegel(P(-430, 90), 230, 7);
        // im Randstreifen: Ville im Westen, Bergisches Land im Osten, flache Rheinebene dazwischen
        Huegel(P(-640, -90), 180, 30);
        Huegel(P(-640, 210), 170, 26);
        Huegel(P(-560, 390), 150, 18);
        Huegel(P(-600, -330), 160, 16);
        Huegel(P(430, -330), 200, 42);
        Huegel(P(520, -110), 190, 38);
        Huegel(P(390, -430), 150, 30);
        Huegel(P(520, 160), 190, 30);
        Huegel(P(450, 360), 170, 26);
        Huegel(P(340, 470), 140, 20);
        Huegel(P(-330, 480), 140, 10);
    }

    // ── Mauern ───────────────────────────────────────────────────────────────

    private void Mauern()
    {
        var linie = new List<Kartenort> { P(50, -R) };
        for (var a = 0; a <= 180; a += 6) linie.Add(Bogen(a, R));
        linie.Add(P(50, R));
        _stadtmauer = E.MauerAnlegen(linie, geschlossen: false, name: "Stadtmauer");
        foreach (var (name, ort) in new[]
        {
            ("Kunibertstor", P(RheinX, -R)), ("Eigelsteintor", Bogen(18, R)), ("Ehrentor", Bogen(60, R)), ("Hahnentor", Bogen(90, R)),
            ("Weyertor", Bogen(120, R)), ("Severinstor", Bogen(162, R)), ("Bayentor", P(RheinX, R)),
        })
            E.TorSetzen(_stadtmauer, ort.X, ort.Z, name);

        // Deutz: kleine Mauer um den Brückenkopf, zum Rhein hin offen
        _deutzer = E.MauerAnlegen(new[] { P(130, -48), P(212, -46), P(214, 40), P(130, 42) }, geschlossen: false, name: "Deutzer Mauer");
        E.TorSetzen(_deutzer, 172, -47, "Deutzer Nordtor");
        E.TorSetzen(_deutzer, 213, -3, "Deutzer Osttor");
        E.TorSetzen(_deutzer, 172, 41, "Deutzer Südtor");
    }

    // ── Straßen ──────────────────────────────────────────────────────────────

    private void Strassen()
    {
        double ZWall(double x, bool nord) => (nord ? -1 : 1) * Math.Sqrt(WallR * WallR - (x - Cx) * (x - Cx));

        // Hauptstraßen in der Altstadt
        Weg(Wegklasse.Haupt, "Rheingasse", P(RheinX, -R), P(RheinX, R));
        Weg(Wegklasse.Haupt, "Hohe Straße", P(HoheX, ZWall(HoheX, true)), P(HoheX, ZWall(HoheX, false)));
        Weg(Wegklasse.Haupt, "Eigelstein", Bogen(18, R), Bogen(18, WallR), P(-27, -118), P(HoheX, -110));
        Weg(Wegklasse.Haupt, "Ehrenstraße", Bogen(60, R), Bogen(60, WallR), P(-80, -45), P(-72, -20));
        Weg(Wegklasse.Haupt, "Hahnenstraße", Bogen(90, R), Bogen(90, WallR), P(-84, -8));
        Weg(Wegklasse.Haupt, "Schildergasse", P(-60, -8), P(HoheX, -8), P(0, -8));
        Weg(Wegklasse.Haupt, "Breite Straße", P(HoheX, -22), P(RheinX, -22));
        Weg(Wegklasse.Haupt, "Weyerstraße", Bogen(120, R), Bogen(120, WallR), P(-70, 64), P(HoheX, 57));
        Weg(Wegklasse.Haupt, "Bayenstraße", P(HoheX, 57), P(RheinX, 57));
        // Severinstraße bis zur Breiten Straße; Schildergasse und Brückenstraße münden in sie (bis 31.10.2026 endeten alle drei am Heumarkt im Ursprung)
        Weg(Wegklasse.Haupt, "Severinstraße", Bogen(162, R), Bogen(162, WallR), P(-14, 100), P(-7.7, 57), P(-2, 30), P(0, 12), P(0, -22));
        Weg(Wegklasse.Haupt, "Brückenstraße", P(0, 8), P(RheinX, 8));
        Weg(Wegklasse.Haupt, "Rheinbrücke", P(RheinX, 8), P(126, 8));
        Weg(Wegklasse.Gasse, "Trankgasse", P(HoheX, -70), P(RheinX, -70));
        Weg(Wegklasse.Gasse, "Hammergasse", P(-80, -45), P(HoheX, -45));

        // Nebengassen mit erfundenen Namen (nur Fußgänger)
        Weg(Wegklasse.Gasse, "Engelspforte", P(HoheX, -112), P(-11, -112));
        Weg(Wegklasse.Gasse, "Krähenpforte", P(0, -70), P(0, -56));
        Weg(Wegklasse.Gasse, "Wollgasse", P(-62, 4), P(-56, 40), P(-50, 60.1));

        // Wallstraßen innerhalb der Mauer
        Weg(Wegklasse.Wall, "Kunibertswall", P(RheinX, -WallR), P(Cx, -WallR), Bogen(6, WallR), Bogen(12, WallR), Bogen(18, WallR), Bogen(24, WallR), Bogen(30, WallR),
            Bogen(36, WallR), Bogen(42, WallR), Bogen(48, WallR), Bogen(54, WallR), Bogen(60, WallR));
        Weg(Wegklasse.Wall, "Mauritiuswall", Bogen(60, WallR), Bogen(66, WallR), Bogen(72, WallR), Bogen(78, WallR), Bogen(84, WallR), Bogen(90, WallR),
            Bogen(96, WallR), Bogen(102, WallR), Bogen(108, WallR), Bogen(114, WallR), Bogen(120, WallR));
        Weg(Wegklasse.Wall, "Kartäuserwall", Bogen(120, WallR), Bogen(126, WallR), Bogen(132, WallR), Bogen(138, WallR), Bogen(144, WallR), Bogen(150, WallR),
            Bogen(156, WallR), Bogen(162, WallR), Bogen(168, WallR), Bogen(174, WallR), Bogen(180, WallR), P(RheinX, WallR));

        // Rheinufer: Leinpfade vor den Ufertoren
        WegGlatt(Wegklasse.Leinpfad, "Leinpfad Nord", P(RheinX, -R), P(45, -175), P(51, -200), P(52, -300), P(51, -499));
        WegGlatt(Wegklasse.Leinpfad, "Leinpfad Süd", P(RheinX, R), P(45, 175), P(50, 200), P(51, 300), P(51, 549));

        // Landstraßen
        WegGlatt(Wegklasse.Land, "Aachener Straße", Bogen(90, R), P(-190, 6), P(-260, 14), P(-340, 24), P(-430, 34), P(-520, 48), P(-699, 70));
        WegGlatt(Wegklasse.Land, "Venloer Straße", Bogen(60, R), P(-170, -85), P(-250, -100), P(-340, -118), P(-440, -140), P(-560, -165), P(-699, -195));
        WegGlatt(Wegklasse.Land, "Neusser Straße", Bogen(18, R), P(-28, -190), P(-40, -250), P(-58, -340), P(-75, -420), P(-90, -499));
        WegGlatt(Wegklasse.Land, "Luxemburger Straße", Bogen(120, R), P(-165, 115), P(-225, 165), P(-285, 230), P(-350, 310), P(-430, 420), P(-520, 549));
        WegGlatt(Wegklasse.Land, "Bonner Straße", Bogen(162, R), P(-14, 190), P(-4, 250), P(6, 330), P(14, 440), P(20, 549));

        // Deutz
        Weg(Wegklasse.Haupt, "Deutzer Freiheit", P(126, 8), P(170, 6), P(213, -3));
        Weg(Wegklasse.Haupt, "Deutzer Kirchstraße", P(172, -47), P(170, 6), P(172, 41));
        WegGlatt(Wegklasse.Land, "Bensberger Straße", P(213, -3), P(270, -10), P(350, -25), P(450, -45), P(550, -70), P(599, -85));
        WegGlatt(Wegklasse.Land, "Mülheimer Weg", P(172, -47), P(160, -110), P(150, -160), P(146, -200), P(160, -300), P(175, -400), P(190, -499));
        WegGlatt(Wegklasse.Land, "Siegburger Straße", P(172, 41), P(180, 120), P(200, 200), P(260, 300), P(380, 420), P(500, 540));

        E.FaehreSetzen(P(51, -200), P(146, -200), "Fähre Mülheim");
    }

    // ── Wahrzeichen ──────────────────────────────────────────────────────────

    private void Wahrzeichen()
    {
        // Drei Marktplätze, ein Stadtmarkt (Entscheidung des Nutzers vom 31.10.2026): an den Enden der Mauer am Rhein und in Deutz
        foreach (var m in Maerkte) Haus("markt", m.Name, m.Ort.X, m.Ort.Z, kennung: m.Kennung);
        MarktPflastern();

        Haus("rathaus", "Rathaus", -20, -44, 90, "Rathaus");
        Haus("kirche", "Groß St. Martin", 26, -44, 90, "Martin");
        Haus("kirche", "St. Gereon", -52, -86, 90, "Gereon");
        Haus("kirche", "St. Severin", -2, 96, 270, "Severin");
        Haus("kirche", "St. Aposteln", -72, -34, 180, "Aposteln");
        Haus("kirche", "St. Kunibert", 14, -128, 0, "Kunibert");
        Haus("koelner_dom", "Dom", 6, -112, 270, "Dom");

        // Rheinufer: Hafen, Kran und Zollstelle mit dem Rücken zum Wasser (Kran und Zollstelle südlich des Hafens, bis 31.10.2026 nördlich –
        // dort liegt jetzt ein Startgrundstück am Nordplatz), das Stapelhaus am Ufer
        Haus("hafenplatz", "Rheinhafen", 51, -60, 270, "Rheinhafen");
        Haus("hafenkran", "Rheinkran", 54, -44, 270, "Rheinkran");
        Haus("zollstelle", "Zollstelle", 54, -30, 270, "Zollstelle");
        Haus("lagerschuppen", "Stapelhaus", 50, 28, 270, "Stapelhaus");

        // Leerstände und Ruinen: Hausnamen aus der Stadt (die Ruine in Deutz seit dem Deutzer Platz im Südosten statt an seiner Stelle)
        Haus("leerstand", "Haus zum güldenen Hammer (leer)", -64, 94, 90, "L1");
        Haus("leerstand", "Haus zum weißen Schwan (leer)", -100, -40, 90, "L2");
        Haus("leerstand", "Haus zum schwarzen Raben (Ruine)", 196, 27, 180, "L3");
    }
}
