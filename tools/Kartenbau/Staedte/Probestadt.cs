using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Kartenbau.Staedte;

// ═════════════════════════════════════════════════════════════════════════════
//  Probestadt – eine kleine, vollständige Stadt als Muster für alle Stadtskripte (Köln und die weiteren Städte)
//
//  Aufruf: dotnet run --project tools/Kartenbau -- bauen probestadt   (schreibt game/karten/probestadt.json und docs/karten/probestadt*.svg)
//  Spielen: Godot mit -- --karte=probestadt
//
//  Aufbau (Meter, x = Osten, z = Süden, der Markt bei 0 | 0):
//    • Mauerring 140 × 110 m mit Westtor und Osttor, die Hauptstraße von Tor zu Tor über den Markt, eine Ringstraße 13 m innerhalb der Mauer.
//    • Am Markt Rathaus und Kirche, an der Hauptstraße vier Werkstattplätze, an der Ringstraße innen sechs Wohnhausplätze, außen (zur Mauer)
//      die Einwohnerplätze am Ring; zehn Einwohnerplätze der Vorstadt am Feldweg im Nordosten.
//    • Vor jedem Tor die Landstraße und beiderseits ein Feldweg; dazwischen vier Bauzonen der Vorstadt (Platz für eine Partie mit 8 Häusern,
//      siehe Kapazitätsprüfung).
//    • Wald im Nordwesten und Südosten, ein Waldweg als weiche Kurve; im Randstreifen (300 m) Felder und Waldstücke aus dem Rand-Vorschlag.
//    • Start: der Spieler und zwei KI-Häuser (Tischlerei, Gießerei).
//    • Gelände: die Stadt flach, nach Osten ein sanfter Hang hinauf (die östliche Vorstadt steigt bis 4 % an), im Randstreifen Hügel bis 25 m
//      und rundherum natürliche Unebenheit. Unter keinem Platz mehr als 8 % Gefälle (die Prüfung meldet nichts).
// ═════════════════════════════════════════════════════════════════════════════

public sealed class Probestadt : Stadtskript
{
    public override string Kennung => "probestadt";
    public override string Stadt => "Probestadt";
    public override string Beschreibung => "kleines Muster: Mauerring, zwei Tore, Markt, 4 Werkstatt- und 6 Wohnhausplätze, Vorstadt, Wald, 2 KI-Häuser";
    public override (double Breite, double Tiefe) Groesse => (400, 300);
    public override int? RandVorschlag => 1;

    private const double MauerX = 70, MauerZ = 55;   // Mauerring bei x = ±70, z = ±55
    private const double Ring = 13;                  // Ringstraße so weit innerhalb der Mauer
    private const double Ende = 190;                 // die Landstraßen enden bei x = ±190
    private const double FeldwegZ = 46;              // Feldwege hinter den Bauzonen der Vorstadt bei z = ±46

    protected override void Anlegen()
    {
        E.VorlageSetzen("Muster für Stadtskripte (tools/Kartenbau/Staedte/Probestadt.cs), kein historisches Vorbild");
        E.FiktionHinzufuegen("Ganz erfunden: eine kleine Stadt zum Ausprobieren von Kartenskript, Prüfung und Partie.");

        // ── Mauer und Tore ───────────────────────────────────────────────────
        var mauer = E.MauerAnlegen(new[] { P(-MauerX, -MauerZ), P(MauerX, -MauerZ), P(MauerX, MauerZ), P(-MauerX, MauerZ) }, geschlossen: true);
        E.TorSetzen(mauer, -MauerX, 0, "Westtor");
        E.TorSetzen(mauer, MauerX, 0, "Osttor");

        // ── Straßen: von Tor zu Tor, die Ringstraße, vor den Toren Landstraßen und Feldwege ──
        E.WegAnlegen(Wegklasse.Haupt, new[] { P(-MauerX, 0), P(MauerX, 0) }, glatt: false, name: "Hauptstraße");
        var ring = Ringgasse(mauer, Ring, name: "Ringstraße");
        E.WegAnlegen(Wegklasse.Land, new[] { P(MauerX, 0), P(Ende, 0) }, glatt: false, name: "Landstraße nach Osten");
        E.WegAnlegen(Wegklasse.Land, new[] { P(-MauerX, 0), P(-Ende, 0) }, glatt: false, name: "Landstraße nach Westen");
        foreach (var (seite, himmel) in new[] { (1, "Ost"), (-1, "West") })
            foreach (var (nord, name) in new[] { (-1, "Nord"), (1, "Süd") })
                E.WegAnlegen(Wegklasse.Feldweg, new[] { P(seite * Ende, 0), P(seite * Ende, nord * FeldwegZ), P(seite * (MauerX + 6), nord * FeldwegZ) },
                    glatt: false, name: $"Feldweg {himmel}-{name}");
        // Ein Waldweg als weiche Kurve vom nordwestlichen Feldweg in den Wald
        E.WegAnlegen(Wegklasse.Fussweg, new[] { P(-Ende, -FeldwegZ), P(-165, -78), P(-140, -100), P(-130, -132) }, glatt: true, name: "Waldweg");

        // ── Gebäude am Markt ─────────────────────────────────────────────────
        E.Setzen("markt", 0, 0);
        E.Setzen("rathaus", 0, -19);         // die Vorderseite zum Markt (Süden)
        E.Setzen("kirche", 0, 22, 180);      // die Vorderseite zum Markt (Norden)

        // ── Werkstattplätze an der Hauptstraße (16 × 14 m, der Hof 3,5 m neben der Straßenmitte) ──
        var werkstaetten = new[]
        {
            E.Setzen("werkstattplatz", -30, -10), E.Setzen("werkstattplatz", 32, -10),
            E.Setzen("werkstattplatz", -30, 10, 180), E.Setzen("werkstattplatz", 32, 10, 180),
        };

        // ── Wohnhausplätze an der Ringstraße (innen), Einwohnerplätze außen zur Mauer ──
        var rand = ring.BreiteWirksam / 2 + 0.5;  // die Vorderkante 0,5 m neben dem Straßenrand
        var (rx, rz) = (MauerX - Ring, MauerZ - Ring);
        var wohnhaeuser = Haeuserreihe("wohnhausplatz", P(-50, -rz), P(50, -rz), links: false, rand, luecke: 2, hoechstens: 3, name: "Wohnhaus Nord");
        wohnhaeuser.AddRange(Haeuserreihe("wohnhausplatz", P(50, rz), P(-50, rz), links: false, rand, luecke: 2, hoechstens: 3, name: "Wohnhaus Süd"));
        Haeuserreihe("einwohnerplatz_ring", P(-rx + 6, -rz), P(rx - 6, -rz), links: true, rand, luecke: 1);
        Haeuserreihe("einwohnerplatz_ring", P(rx - 6, rz), P(-rx + 6, rz), links: true, rand, luecke: 1);
        // Einwohnerplätze der Vorstadt (das Spiel stellt einen Teil der Einwohnerhäuser vor die Mauer): am Feldweg Ost-Nord, nach Norden
        Haeuserreihe("einwohnerplatz_vorstadt", P(Ende - 6, -FeldwegZ), P(MauerX + 6, -FeldwegZ), links: false, 2, luecke: 1.5, hoechstens: 10);

        // ── Bauzonen der Vorstadt zwischen Landstraße und Feldweg ────────────
        foreach (var seite in new[] { 1, -1 })
            foreach (var nord in new[] { -1, 1 })
            {
                var (x0, x1) = (seite * (MauerX + 6), seite * (Ende - 6));
                var (z0, z1) = (nord * 4.0, nord * (FeldwegZ - 4));
                E.BauzoneAnlegen(Lage.Vorstadt, new[] { P(x0, z0), P(x1, z0), P(x1, z1), P(x0, z1) },
                    $"V{(seite > 0 ? "O" : "W")}{(nord < 0 ? "N" : "S")}");
            }

        // ── Natur ────────────────────────────────────────────────────────────
        E.NaturflaecheAnlegen("wald", new[] { P(-195, -145), P(-60, -145), P(-60, -72), P(-120, -60), P(-195, -60) }, "Wald im Nordwesten");
        E.NaturflaecheAnlegen("wald", new[] { P(60, 70), P(195, 60), P(195, 145), P(40, 145) }, "Wald im Südosten");

        // ── Start: der Spieler und zwei KI-Häuser ────────────────────────────
        E.SpielerWohnhausSetzen(wohnhaeuser[1].Kennung);
        E.SpielerFarbeSetzen("#336bb8");
        E.SpielerStartplatzSetzen(werkstaetten[0].Kennung);
        var tischler = E.HausHinzufuegen("Haus Ahlbeck");
        E.BetriebHinzufuegen(tischler, werkstaetten[1].Kennung, Betriebsart.Tischlerei);
        var giesser = E.HausHinzufuegen("Haus Brendel");
        E.BetriebHinzufuegen(giesser, werkstaetten[3].Kennung, Betriebsart.Giesserei);

        // ── Gelände: sanfter Hang an der östlichen Vorstadt, Hügel im Randstreifen, Unebenheit im Rand ──
        Huegel(P(280, 0), 220, 6);          // höchstens 1,5 · 6 / 220 ≈ 4 % – die Mauer bei x = 70 liegt am Fuß
        Huegel(P(-260, -340), 160, 18);
        Huegel(P(180, -370), 170, 25);
        Huegel(P(430, 60), 150, 15);
        Huegel(P(-150, 360), 160, 20);
        Huegel(P(-430, -60), 140, 14);
        Huegel(P(380, 340), 150, 22);
        Huegel(P(-380, 300), 120, -6);      // eine Senke im Südwesten
        // Unebenheit: ein Strich rund um das Spielfeld, 170 m vor seiner Kante (der Pinsel endet genau an der Kante)
        Rauschen(new[] { P(-370, -320), P(370, -320), P(370, 320), P(-370, 320), P(-370, -320) }, radius: 170, staerke: 1.5, massstab: 40, startwert: 11);

        E.KameraStartSetzen(P(0, 0), 110);
    }
}
