using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Wo die Äcker liegen: Feldflächen der Karte in Ackerstreifen geteilt (Kartenwerkstatt, Schritt 5a)
//
//  Wie NaturVerteilung rechnet der Kern die Lagen, damit Kartenwerkstatt und Spiel dieselben Äcker zeigen:
//    • Die Fläche wird in parallele Streifen geteilt (Breite „streifen“, Richtung „richtung“). Die Streifen liegen auf einem festen Raster um
//      den Ursprung – eine verschobene Ecke verschiebt keine Streifen.
//    • Jeder Streifen wird über seine ganze Länge in Stücke geschnitten (je 60–140 % von „laenge“), jedes Stück bekommt eine Frucht nach
//      Gewicht und eine Variante (1–4, für leichte Unterschiede im Bild) – aus dem Startwert und der Nummer des Streifens.
//    • Erst danach bleibt frei, was nicht zum Acker gehört: außerhalb der Fläche, in einer Aussparung, die Stadt, Wege, Gebäude und Bauzonen
//      mit Rand (Naturfreiraum). Ein Stück wird dort abgeschnitten oder geteilt; was kürzer als 4 m bleibt, fällt weg. So verschiebt ein neuer
//      Weg keine anderen Äcker.
//  Geprüft wird in Schritten von 2 m längs des Streifens, an drei Stellen quer (beide Ränder, Mitte).
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Ackerstück: Mitte, Drehung (Grad wie <c>Rotation.Y</c>; die Länge läuft entlang der lokalen z-Achse, die Breite entlang x), Länge und Breite
/// in Metern, Frucht (<see cref="FeldVerteilung.Fruechte"/>) und Variante (1–4).
/// </summary>
public readonly record struct Feldparzelle(double X, double Z, double Drehung, double Laenge, double Breite, string Frucht, int Variante)
{
    /// <summary>Die vier Ecken (hinten links, hinten rechts, vorn rechts, vorn links – wie bei Gebäuden).</summary>
    public IReadOnlyList<Kartenort> Ecken()
    {
        var (b, l) = (Breite / 2, Laenge / 2);
        var (x, z, d) = (X, Z, Drehung);
        return new[] { (-b, -l), (b, -l), (b, l), (-b, l) }.Select(e =>
        {
            var p = Kartengeometrie.Dreh(e.Item1, e.Item2, d);
            return new Kartenort(x + p.X, z + p.Z);
        }).ToArray();
    }
}

/// <summary>Teilt Feldflächen deterministisch in Ackerstücke (siehe Kopf der Datei).</summary>
public static class FeldVerteilung
{
    /// <summary>Die Früchte, die eine Feldfläche tragen kann (Kennung in der Datei → Anzeigename).</summary>
    public static readonly IReadOnlyDictionary<string, string> Fruechte = new Dictionary<string, string>
    {
        ["weizen"] = "Weizen", ["roggen"] = "Roggen", ["gerste"] = "Gerste", ["hafer"] = "Hafer", ["flachs"] = "Flachs", ["brache"] = "Brache",
    };

    /// <summary>Breite eines Ackerstreifens ohne Angabe (Meter) und die Grenzen für die Einstellung.</summary>
    public const double StreifenVorgabe = 8, StreifenMin = 3, StreifenMax = 30;

    /// <summary>Mittlere Länge eines Ackerstücks ohne Angabe (Meter) und die Grenzen für die Einstellung.</summary>
    public const double LaengeVorgabe = 40, LaengeMin = 10, LaengeMax = 200;

    /// <summary>Kürzer bleibt kein Stück stehen (Meter).</summary>
    public const double KuerzestesStueck = 4;

    /// <summary>In diesen Schritten (Meter) prüft die Verteilung längs eines Streifens, ob der Boden frei ist.</summary>
    public const double Pruefschritt = 2;

    /// <summary>So viele Varianten je Frucht (für leichte Unterschiede in Farbe und Wuchs).</summary>
    public const int Varianten = 4;

    /// <summary>Alle Äcker einer Stadtkarte, Fläche für Fläche in der Reihenfolge der Datei; <paramref name="modellmass"/> wie bei <see cref="Naturhindernis.Von"/>.</summary>
    public static List<Feldparzelle> Alle(Stadtkarte karte, Func<Baustein, (double X, double Z)?>? modellmass = null)
    {
        var frei = Naturfreiraum.Aus(karte, modellmass);
        return karte.Natur.Felder.SelectMany(f => Flaeche(f, frei)).ToList();
    }

    /// <summary>Die Äcker einer Feldfläche (siehe Kopf der Datei): gleiche Fläche, gleicher Startwert, gleiche Hindernisse – gleiche Äcker.</summary>
    public static List<Feldparzelle> Flaeche(Feldflaeche feld, Naturfreiraum frei)
    {
        var liste = new List<Feldparzelle>();
        var aussen = feld.Flaeche.Where(p => p is { Length: 2 }).ToList();
        var fruechte = feld.Fruechte.Where(f => f.Value > 0 && Fruechte.ContainsKey(f.Key)).ToList();
        if (aussen.Count < 3 || fruechte.Count == 0 || feld.Streifen <= 0 || feld.Laenge <= 0) return liste;
        var loecher = feld.Aussparungen.Select(l => l.Where(p => p is { Length: 2 }).ToList()).Where(l => l.Count >= 3).ToList();
        bool Acker(double x, double z) =>
            Geometrie.ImVieleck(x, z, aussen) && !loecher.Any(l => Geometrie.ImVieleck(x, z, l)) && frei.Frei(x, z);

        // Lokal: u quer zu den Streifen (lokal x), v entlang (lokal z). Welt = Dreh(u, v, richtung), zurück mit −richtung.
        var richtung = feld.Richtung;
        var lokal = aussen.Select(p => Kartengeometrie.Dreh(p[0], p[1], -richtung)).ToList();
        var (u0, u1, v0, v1) = (lokal.Min(p => p.X), lokal.Max(p => p.X), lokal.Min(p => p.Z), lokal.Max(p => p.Z));
        var w = feld.Streifen;
        var schritt = Pruefschritt;
        // Die Stücke beginnen an einer festen Linie (v auf das Prüfraster nach unten), damit kleine Änderungen sie nicht verschieben.
        var anfang = Math.Floor(v0 / schritt) * schritt;
        var zellen = (int)Math.Ceiling((v1 - anfang) / schritt);
        for (var k = (int)Math.Floor(u0 / w); k * w < u1; k++)
        {
            var (links, rechts) = (k * w, (k + 1) * w);
            var mitteU = (links + rechts) / 2;
            var quer = new[] { links + Math.Min(0.25, w / 4), mitteU, rechts - Math.Min(0.25, w / 4) };
            var zufall = new Random(unchecked(feld.Startwert * 7919 + k * 104729));

            // 1. Stücke über die ganze Länge (in Prüfschritten): [von, bis) in Zellen, mit Frucht und Variante
            var stuecke = new List<(int Von, int Bis, string Frucht, int Variante)>();
            for (var z = 0; z < zellen;)
            {
                var lang = Math.Max(1, (int)Math.Round(feld.Laenge * (0.6 + 0.8 * zufall.NextDouble()) / schritt));
                var frucht = NachGewicht(fruechte, zufall);
                var variante = zufall.Next(Varianten) + 1;
                stuecke.Add((z, Math.Min(zellen, z + lang), frucht, variante));
                z += lang;
            }

            // 2. Welche Zellen sind Acker?
            var acker = new bool[zellen];
            for (var z = 0; z < zellen; z++)
            {
                var v = anfang + (z + 0.5) * schritt;
                acker[z] = quer.All(u =>
                {
                    var p = Kartengeometrie.Dreh(u, v, richtung);
                    return Acker(p.X, p.Z);
                });
            }

            // 3. Jedes Stück auf die Acker-Zellen zuschneiden (ein Hindernis teilt es)
            foreach (var (von, bis, frucht, variante) in stuecke)
                for (var z = von; z < bis;)
                {
                    if (!acker[z]) { z++; continue; }
                    var ende = z;
                    while (ende < bis && acker[ende]) ende++;
                    var laenge = (ende - z) * schritt;
                    if (laenge >= KuerzestesStueck - 1e-9)
                    {
                        var mitteV = anfang + (z + ende) * schritt / 2;
                        var m = Kartengeometrie.Dreh(mitteU, mitteV, richtung);
                        liste.Add(new Feldparzelle(m.X, m.Z, Kartengeometrie.Mod(richtung, 360), laenge, w, frucht, variante));
                    }
                    z = ende;
                }
        }
        return liste;
    }

    private static string NachGewicht(List<KeyValuePair<string, double>> fruechte, Random zufall)
    {
        var summe = fruechte.Sum(f => f.Value);
        var wurf = zufall.NextDouble() * summe;
        foreach (var (frucht, gewicht) in fruechte)
        {
            wurf -= gewicht;
            if (wurf < 0) return frucht;
        }
        return fruechte[^1].Key;
    }
}
