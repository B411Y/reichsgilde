using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Kartenbau.Umbauten;

// ═════════════════════════════════════════════════════════════════════════════
//  Greybox mit Fluss und Hafen (PR #199, Entscheidung des Nutzers 25.10.2026)
//
//  Zum Testen des Schiffshandels bekommt die Greybox am Südrand einen breiten, schiffbaren Fluss, ein Südtor und vor dem Tor einen Hafen.
//  Die Stadt, das Umland und die Startbelegung bleiben, wo sie sind; dafür wächst das Spielfeld nach Süden (Tiefe 300 → 400 m, Mitte 0 | 50):
//    • Fluss „Grenzfluss“: von West nach Ost (Fließrichtung Ost) über die ganze Karte, von Außenkante zu Außenkante (x = −500 … 500) – dort
//      beginnen die Schiffsrouten zu den fernen Städten. 36 m breit, Mitte bei z ≈ 165 (vor dem Hafen z = 166), 0,6 m/s.
//    • Ufer: das Gelände fällt beiderseits sanft zum Fluss ab, in der Flussmitte 1,5 m tiefer (Senken mit 70 m Radius, unter 3 % Gefälle);
//      die Stadt (z ≤ 112) bleibt flach. Sand am Nordufer, Schlamm am Südufer.
//    • Südtor in der Südmauer bei x = 38, in der Verlängerung der östlichen Ringgasse. Die Torburg (16 m) braucht dort den Platz der
//      Einwohnerplätze Ringplatz 19 und 20 (unbelegt); sie stehen jetzt als Vorstadtplätze an der Hafenstraße (gleich viele Einwohnerplätze).
//    • Südgasse (Hauptstraße, 4 m – K1 liegt nah am Ringeck) vom Ringeck durchs Tor, Hafenstraße (Landstraße, die Fahrstraße für Karren) gerade nach Süden zum Hafen.
//    • Hafen am Nordufer bei 38 | 140, Kai (Rückseite) nach Süden zum Wasser – das Modell bringt Kai, Tretradkran, Lagerschuppen und Kähne.
//      Östlich daneben ein Kiesplatz am Ufer für die Zollstelle (Platzhalter, bis es Modell und Katalog gibt).
//  Aufruf: dotnet run --project tools/Kartenbau -- umbauen greybox-hafen   (auf die Greybox vor PR #199)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class GreyboxHafen : Umbau
{
    public override string Kennung => "greybox-hafen";
    public override string Karte => "greybox";
    public override string Beschreibung => "Fluss am Südrand (36 m, nach Osten), Südtor, Hafenstraße, Hafen und Zollplatz; Spielfeld 400 × 400 m (PR #199)";

    /// <summary>Lage des Südtors (x) und des Hafens (Mitte, z).</summary>
    public const double TorX = 38, HafenZ = 140;

    /// <summary>Breite des Flusses (m) und Senkung des Geländes in der Flussmitte (m) mit ihrem Radius.</summary>
    public const double Breite = 36, Senkung = 1.5, SenkRadius = 70;

    /// <summary>Der Verlauf des Flusses in Fließrichtung (West → Ost), von Außenkante zu Außenkante.</summary>
    public static readonly Kartenort[] Verlauf =
    {
        new(-500, 172), new(-380, 166), new(-250, 160), new(-120, 163), new(0, 168), new(TorX, 166), new(110, 162), new(240, 168), new(370, 174), new(500, 170),
    };

    private static Kartenort P(double x, double z) => new(x, z);

    public override void Anwenden(Kartenentwurf e, TextWriter aus)
    {
        // ── Spielfeld nach Süden vergrößern (alles Vorhandene bleibt an seinem Ort) ──
        e.GroesseSetzen(400, 400, P(0, 50));
        var g = e.Kartengrenzen;
        aus.WriteLine($"   Spielfeld 400 × 400 m, z {g.Spielfeld.Z0} … {g.Spielfeld.Z1}; ganze Karte x {g.Aussen.X0} … {g.Aussen.X1}, z {g.Aussen.Z0} … {g.Aussen.Z1}");

        // ── Ufergelände: Tupfer im Abstand des Radius entlang des Flusses (die weichen Kanten ergänzen sich zu einer gleichmäßigen Rinne) ──
        foreach (var p in Abgetastet(Verlauf, SenkRadius))
            e.GelaendePinseln(new Pinsel(Pinselart.Senken, SenkRadius, Senkung, Kante: 1), new[] { p });

        // ── Der Fluss ──
        var fluss = e.FlussAnlegen(Verlauf, "fluss", Breite, "Grenzfluss");
        e.FlussStroemungSetzen(fluss, 0.6);
        aus.WriteLine($"   Grenzfluss: {Verlauf.Length} Punkte, {Breite} m breit, nach Osten, 0,6 m/s");

        // ── Südtor: Ringplatz 19 und 20 machen der Torburg Platz ──
        var mauer = e.Mauern.Single(m => m.Name == "Stadtmauer");
        foreach (var k in new[] { "Ring19", "Ring20" }) e.Loeschen(k);
        e.TorSetzen(mauer, TorX, 50, "Südtor");
        e.WegAnlegen(Wegklasse.Haupt, new[] { P(TorX, 38), P(TorX, 50) }, glatt: false, name: "Südgasse", breite: 4);
        var vorderkante = HafenZ - 7;
        e.WegAnlegen(Wegklasse.Land, new[] { P(TorX, 50), P(TorX, vorderkante) }, glatt: false, name: "Hafenstraße");
        aus.WriteLine($"   Südtor bei {TorX} | 50 (Ringplatz 19 und 20 entfernt), Südgasse vom Ringeck, Hafenstraße bis z = {vorderkante}");

        // ── Hafen am Nordufer, Kai nach Süden ──
        var hafen = e.Setzen(Kartenentwurf.HafenKatalog, TorX, HafenZ, 180);
        e.Umbenennen(hafen.Kennung, "Hafen am Grenzfluss");
        // Die beiden Einwohnerplätze vom Ring an der Hafenstraße (Ostseite, Vorderseite zur Straße)
        foreach (var (z, nr) in new[] { (116.0, 19), (124.0, 20) })
        {
            var platz = e.Setzen("einwohnerplatz_vorstadt", TorX + 2 + 0.5 + 3.1, z, 270);
            e.Umbenennen(platz.Kennung, $"Uferplatz {nr - 18}");
        }
        aus.WriteLine($"   Hafen am Grenzfluss bei {TorX} | {HafenZ}, Drehung 180; zwei Einwohnerplätze an der Hafenstraße");

        // ── Boden: Sand und Schlamm am Ufer, Kies auf dem Zollplatz ──
        var nordufer = Verlauf.Select(p => P(p.X, p.Z - Breite / 2 - 2)).ToList();
        var suedufer = Verlauf.Select(p => P(p.X, p.Z + Breite / 2 + 2)).ToList();
        e.BodenMalen(new Bodenpinsel(Bodentextur.Sand, 5, 0.8, 0.5), nordufer.Where(p => p.X is > -260 and < 260).ToList());
        e.BodenMalen(new Bodenpinsel(Bodentextur.Schlamm, 5, 0.7, 0.5), suedufer.Where(p => p.X is > -260 and < 260).ToList());
        e.BodenMalen(new Bodenpinsel(Bodentextur.Kies, 5, 1, 0.6, Deko: false), new[] { P(TorX + 20, HafenZ + 2) });
        aus.WriteLine($"   Zollplatz (Platzhalter, Kies) bei {TorX + 20} | {HafenZ + 2}");

        e.FiktionHinzufuegen("Der Grenzfluss (PR #199) läuft über die ganze Breite der Karte; an beiden Enden beginnen die Schiffsrouten zu den fernen Städten. "
                             + "Der Kiesplatz östlich des Hafens ist der Platz der Zollstelle.");

        // Höhen zur Kontrolle: Gelände an der Hafenstraße, am Kai und am Ufer, Spiegel vor dem Hafen
        var f = e.Gelaendeform;
        var spiegel = e.Wasserform.An(TorX, 166)?.Spiegel;
        aus.WriteLine($"   Höhen: Tor {Z(f.Hoehe(TorX, 52))}, Wald z=112 {Z(f.Hoehe(TorX, 112))}, Hafen vorn {Z(f.Hoehe(TorX, 133))}, hinten {Z(f.Hoehe(TorX, 145))}, "
                      + $"Ufer {Z(f.Hoehe(TorX - 20, 147))}, Spiegel {(spiegel is { } s ? Z(s) : "–")}");
    }

    private static string Z(double x) => Kartenschreiber.Zahl(Math.Round(x, 2));

    /// <summary>Punkte entlang des Linienzugs im Abstand <paramref name="schritt"/> (der erste und der letzte eingeschlossen).</summary>
    private static List<Kartenort> Abgetastet(IReadOnlyList<Kartenort> linie, double schritt)
    {
        var liste = new List<Kartenort> { linie[0] };
        var rest = schritt;
        for (var i = 1; i < linie.Count; i++)
        {
            var (a, b) = (linie[i - 1], linie[i]);
            var laenge = a.Abstand(b);
            var t = rest;
            for (; t <= laenge; t += schritt) liste.Add(P(a.X + (b.X - a.X) * t / laenge, a.Z + (b.Z - a.Z) * t / laenge));
            rest = t - laenge;
        }
        if (liste[^1].Abstand(linie[^1]) > 1) liste.Add(linie[^1]);
        return liste;
    }
}
