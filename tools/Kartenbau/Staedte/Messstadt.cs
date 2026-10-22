using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Kartenbau.Staedte;

// ═════════════════════════════════════════════════════════════════════════════
//  Messstadt – eine Stadt für die Balancing-Messung mit Platz für alle acht KI-Häuser (und den Spieler)
//
//  Aufruf: dotnet run --project tools/Kartenbau -- bauen messstadt   (schreibt game/karten/messstadt.json und docs/karten/messstadt*.svg)
//  Messen: dotnet run -c Release --project tests/Reichsgilde.Core.Tests -- messen bericht   (Abschnitt „1/3/5/8 Häuser“)
//  Spielen: Godot mit -- --karte=messstadt
//
//  Aufbau wie die Probestadt, nur größer (Meter, x = Osten, z = Süden, der Markt bei 0 | 0):
//    • Mauerring 180 × 120 m mit Westtor und Osttor, die Hauptstraße von Tor zu Tor über den Markt, eine Ringstraße 13 m innerhalb der Mauer.
//    • Am Markt Rathaus und Kirche, an der Hauptstraße neun Werkstattplätze (der Spieler und acht KI-Häuser), an der Ringstraße innen
//      neun Wohnhausplätze, außen die Einwohnerplätze am Ring; Einwohnerplätze der Vorstadt am Feldweg im Nordosten.
//    • Vor jedem Tor die Landstraße und beiderseits ein Feldweg, dazwischen vier große Bauzonen der Vorstadt (die KI-Häuser bauen dort
//      neue Werkstätten und Wohnhäuser).
//    • Im Umland zwei Umlandwege mit 17 Umlandbetrieben: die acht der KI-Häuser und die neun Liegenschaften zur Pacht aus wirtschaft.json.
//    • Start: der Spieler und acht KI-Häuser. Die ersten drei gleichen den Häusern der Greybox (Schmied/Zwerg, Tischler/Elb, Steinmetz/Halbling);
//      eine Messung mit n Häusern nimmt die ersten n.
// ═════════════════════════════════════════════════════════════════════════════

public sealed class Messstadt : Stadtskript
{
    public override string Kennung => "messstadt";
    public override string Stadt => "Messstadt";
    public override string Beschreibung => "Messung: Mauerring, 9 Werkstatt- und 9 Wohnhausplätze, große Vorstadt, 17 Umlandbetriebe, 8 KI-Häuser";
    public override (double Breite, double Tiefe) Groesse => (480, 340);

    private const double MauerX = 90, MauerZ = 60;   // Mauerring bei x = ±90, z = ±60
    private const double Ring = 13;                  // Ringstraße so weit innerhalb der Mauer
    private const double Ende = 220;                 // die Landstraßen enden bei x = ±220
    private const double FeldwegZ = 56;              // Feldwege hinter den Bauzonen der Vorstadt bei z = ±56
    private const double UmlandZ = 100;              // Umlandwege bei z = ±100

    /// <summary>Die acht KI-Häuser in ihrer Reihenfolge: Volk, Werkstatt und Umlandbetrieb (Baustein im Katalog).</summary>
    private static readonly (Volk Volk, Geschlecht Geschlecht, Betriebsart Werkstatt, Betriebsart Umland, string Baustein)[] Haeuser =
    {
        (Volk.Zwerg, Geschlecht.Mann, Betriebsart.Giesserei, Betriebsart.Mine, "grube"),
        (Volk.Elb, Geschlecht.Frau, Betriebsart.Tischlerei, Betriebsart.Holzfaellerei, "holzschlag"),
        (Volk.Halbling, Geschlecht.Mann, Betriebsart.Maurerei, Betriebsart.Steinbruch, "steinbruch"),
        (Volk.Mensch, Geschlecht.Frau, Betriebsart.Tinkturei, Betriebsart.Kraeutergarten, "kraeutergarten"),
        (Volk.Mensch, Geschlecht.Mann, Betriebsart.Giesserei, Betriebsart.Mine, "grube"),
        (Volk.Halbling, Geschlecht.Frau, Betriebsart.Schaenke, Betriebsart.Bauernhof, "bauernhof"),
        (Volk.Mensch, Geschlecht.Mann, Betriebsart.Tischlerei, Betriebsart.Holzfaellerei, "holzschlag"),
        (Volk.Zwerg, Geschlecht.Frau, Betriebsart.Maurerei, Betriebsart.Steinbruch, "steinbruch"),
    };

    /// <summary>Die neun Liegenschaften zur Pacht (Namen wie in wirtschaft.json, „liegenschaften“) mit ihrem Baustein.</summary>
    private static readonly (string Betrieb, string Baustein)[] Liegenschaften =
    {
        ("Grube am Silberberg", "grube"), ("Grube im Tannenwald", "grube"), ("Holzschlag im Eichenforst", "holzschlag"),
        ("Steinbruch am Felsenhang", "steinbruch"), ("Kräutergarten am Bach", "kraeutergarten"), ("Duftgarten am Lavendelhang", "duftgarten"),
        ("Bauernhof an der Mühle", "bauernhof"), ("Klosterhof am Osthang", "klosterhof"), ("Wachturm an der Landstraße", "wachturm"),
    };

    protected override void Anlegen()
    {
        E.VorlageSetzen("Messstadt für das Balancing (tools/Kartenbau/Staedte/Messstadt.cs), kein historisches Vorbild");
        E.FiktionHinzufuegen("Ganz erfunden: eine Stadt mit Platz für acht KI-Häuser, damit die Messung 1, 3, 5 und 8 Häuser vergleichen kann.");

        // ── Mauer und Tore ───────────────────────────────────────────────────
        var mauer = E.MauerAnlegen(new[] { P(-MauerX, -MauerZ), P(MauerX, -MauerZ), P(MauerX, MauerZ), P(-MauerX, MauerZ) }, geschlossen: true);
        E.TorSetzen(mauer, -MauerX, 0, "Westtor");
        E.TorSetzen(mauer, MauerX, 0, "Osttor");

        // ── Straßen: von Tor zu Tor, die Ringstraße, vor den Toren Landstraßen und Feldwege, im Umland die Umlandwege ──
        E.WegAnlegen(Wegklasse.Haupt, new[] { P(-MauerX, 0), P(MauerX, 0) }, glatt: false, name: "Hauptstraße");
        var ring = Ringgasse(mauer, Ring, name: "Ringstraße");
        E.WegAnlegen(Wegklasse.Land, new[] { P(MauerX, 0), P(Ende, 0) }, glatt: false, name: "Landstraße nach Osten");
        E.WegAnlegen(Wegklasse.Land, new[] { P(-MauerX, 0), P(-Ende, 0) }, glatt: false, name: "Landstraße nach Westen");
        foreach (var (seite, himmel) in new[] { (1, "Ost"), (-1, "West") })
            foreach (var (nord, name) in new[] { (-1, "Nord"), (1, "Süd") })
                E.WegAnlegen(Wegklasse.Feldweg, new[] { P(seite * Ende, 0), P(seite * Ende, nord * FeldwegZ), P(seite * (MauerX + 6), nord * FeldwegZ) },
                    glatt: false, name: $"Feldweg {himmel}-{name}");
        foreach (var (nord, name) in new[] { (-1, "Nord"), (1, "Süd") })
            E.WegAnlegen(Wegklasse.Land, new[] { P(-Ende, nord * FeldwegZ), P(-Ende, nord * UmlandZ), P(Ende, nord * UmlandZ), P(Ende, nord * FeldwegZ) },
                glatt: false, name: $"Umlandweg {name}");

        // ── Gebäude am Markt ─────────────────────────────────────────────────
        E.Setzen("markt", 0, 0);
        E.Setzen("rathaus", 0, -19);         // die Vorderseite zum Markt (Süden)
        E.Setzen("kirche", 0, 22, 180);      // die Vorderseite zum Markt (Norden)

        // ── Werkstattplätze an der Hauptstraße (16 × 14 m, der Hof 3,5 m neben der Straßenmitte), 2 m Abstand ──
        var werkstaetten = new List<EntwurfGebaeude>();
        foreach (var x in new[] { -24.0, 24, -42, 42, -60, 60 })
            werkstaetten.Add(E.Setzen("werkstattplatz", x, -10));
        foreach (var x in new[] { -24.0, 24, -42 })
            werkstaetten.Add(E.Setzen("werkstattplatz", x, 10, 180));

        // ── Wohnhausplätze an der Ringstraße (innen), Einwohnerplätze außen zur Mauer ──
        var rand = ring.BreiteWirksam / 2 + 0.5;  // die Vorderkante 0,5 m neben dem Straßenrand
        var (rx, rz) = (MauerX - Ring, MauerZ - Ring);
        var wohnhaeuser = Haeuserreihe("wohnhausplatz", P(-50, -rz), P(50, -rz), links: false, rand, luecke: 2, hoechstens: 5, name: "Wohnhaus Nord");
        wohnhaeuser.AddRange(Haeuserreihe("wohnhausplatz", P(50, rz), P(-50, rz), links: false, rand, luecke: 2, hoechstens: 4, name: "Wohnhaus Süd"));
        Haeuserreihe("einwohnerplatz_ring", P(-rx + 6, -rz), P(rx - 6, -rz), links: true, rand, luecke: 1);
        Haeuserreihe("einwohnerplatz_ring", P(rx - 6, rz), P(-rx + 6, rz), links: true, rand, luecke: 1);
        Haeuserreihe("einwohnerplatz_vorstadt", P(Ende - 6, -FeldwegZ), P(MauerX + 6, -FeldwegZ), links: false, 2, luecke: 1.5, hoechstens: 12);

        // ── Bauzonen der Vorstadt zwischen Landstraße und Feldweg ────────────
        foreach (var seite in new[] { 1, -1 })
            foreach (var nord in new[] { -1, 1 })
            {
                var (x0, x1) = (seite * (MauerX + 6), seite * (Ende - 6));
                var (z0, z1) = (nord * 4.0, nord * (FeldwegZ - 4));
                E.BauzoneAnlegen(Lage.Vorstadt, new[] { P(x0, z0), P(x1, z0), P(x1, z1), P(x0, z1) },
                    $"V{(seite > 0 ? "O" : "W")}{(nord < 0 ? "N" : "S")}");
            }

        // ── Umland: die Betriebe an den Umlandwegen, die Vorderseite (Halteplatz) zum Weg ──
        // Nördlich des nördlichen Umlandwegs die acht Betriebe der KI-Häuser, südlich des südlichen die neun Liegenschaften.
        var umlandKi = new List<EntwurfGebaeude>();
        for (var i = 0; i < Haeuser.Length; i++)
            umlandKi.Add(UmlandSetzen(Haeuser[i].Baustein, -180 + i * 50, -1));
        for (var i = 0; i < Liegenschaften.Length; i++)
        {
            var g = UmlandSetzen(Liegenschaften[i].Baustein, -200 + i * 50, 1);
            E.BetriebSetzen(g.Kennung, Liegenschaften[i].Betrieb);
        }

        // ── Natur ────────────────────────────────────────────────────────────
        E.NaturflaecheAnlegen("wald", new[] { P(-235, -165), P(-60, -165), P(-60, -125), P(-235, -125) }, "Wald im Norden");
        E.NaturflaecheAnlegen("wald", new[] { P(60, 125), P(235, 125), P(235, 165), P(60, 165) }, "Wald im Süden");

        // ── Start: der Spieler und acht KI-Häuser ────────────────────────────
        E.SpielerWohnhausSetzen(wohnhaeuser[0].Kennung);
        E.SpielerFarbeSetzen("#336bb8");
        E.SpielerStartplatzSetzen(werkstaetten[0].Kennung);
        for (var i = 0; i < Haeuser.Length; i++)
        {
            var h = Haeuser[i];
            var haus = E.HausHinzufuegen();
            E.HausVolkSetzen(haus, h.Volk);
            E.HausGeschlechtSetzen(haus, h.Geschlecht);
            E.BetriebHinzufuegen(haus, werkstaetten[i + 1].Kennung, h.Werkstatt);
            E.BetriebHinzufuegen(haus, umlandKi[i].Kennung, h.Umland);
        }

        E.KameraSetzen(P(0, 0), 140);
    }

    /// <summary>Ein Umlandbetrieb mit der Vorderseite zum Umlandweg: <paramref name="nord"/> −1 nördlich des Nordwegs, +1 südlich des Südwegs.</summary>
    private EntwurfGebaeude UmlandSetzen(string baustein, double x, int nord)
    {
        var tiefe = E.Katalog.Finde(baustein)?.Groesse?[1] ?? 14;
        var z = nord * (UmlandZ + 4 + tiefe / 2);
        return E.Setzen(baustein, x, z, nord < 0 ? 0 : 180);
    }
}
