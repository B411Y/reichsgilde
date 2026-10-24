using System.Text.Json.Serialization;

namespace Reichsgilde.Core.Bauen;

// ═════════════════════════════════════════════════════════════════════════════
//  Die Deko der Bodentexturen (Kartenwerkstatt, Schritt 5e): kleine Teile, die eine gemalte Textur von selbst streut
//
//  Nicht gespeichert, sondern aus dem Bodenraster gerechnet – gleich auf jedem Rechner, in der Kartenwerkstatt wie im Spiel:
//    • Je Textur steht im Katalog (game/daten/katalog.json, „bodentexturen“), was sie streut (Natur-Bausteine nach Gewicht) und wie dicht
//      (Stück je 100 m² bei vollem Anteil und normaler Dekodichte).
//    • Je Textur liegt ein festes, am Boden verankertes Gitter; in jeder Zelle ein Kandidat an einem festen Ort (aus Startwert, Textur und
//      Zelle). Er wird gesetzt mit der Wahrscheinlichkeit Anteil der Textur · Dekodichte (0 … 2) / 2. So ändert ein Pinselstrich nur die
//      Deko unter ihm – nichts daneben verrutscht.
//    • Wo die Dekodichte 0 ist (überall, wo nie mit „Deko streuen“ gemalt wurde), streut nichts: alte Karten bleiben, wie sie waren.
//    • Frei bleiben Wege, Gebäude, Bauzonen und Wasser mit 1 m Rand und die Mauer mit 3 m (Naturfreiraum.FreiFuerBoden); anders als Bäume
//      darf Deko auch in der Stadt liegen.
//  Gerechnet wird je Kachel des Bodenrasters (64 × 64 m); ein Kandidat gehört zu der Kachel, in der sein Ort liegt.
// ═════════════════════════════════════════════════════════════════════════════

/// <summary>Eine Bodentextur im Katalog: Name, Farbe (Vorschau, Karten), Dichte der Deko (Stück je 100 m²) und ihre Teile (Natur-Kennung → Gewicht).</summary>
public sealed class Bodentexturdaten
{
    public string Kennung { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Farbe als „#rrggbb“ – für die Vorschau des Kartenskripts und Übersichten, nicht für die 3D-Welt.</summary>
    public string Farbe { get; init; } = "#808080";

    /// <summary>Deko-Teile je 100 m² bei vollem Anteil und normaler Dekodichte (0: streut nichts).</summary>
    public double Dichte { get; init; }

    /// <summary>Was die Textur streut: Kennung eines Natur-Bausteins → Gewicht.</summary>
    public Dictionary<string, double> Deko { get; init; } = new();

    /// <summary>Die Textur dieses Eintrags (null: Kennung unbekannt).</summary>
    [JsonIgnore]
    public Bodentextur? Textur => Bodenraster.TexturVon(Kennung);
}

/// <summary>Rechnet die Deko eines Bodenrasters (siehe Kopf der Datei). Einmal je Änderung bauen, dann kachelweise abfragen.</summary>
public sealed class Bodendeko
{
    /// <summary>Abstand der Deko zu Wegen, Gebäuden, Bauzonen und Wasser (Meter).</summary>
    public const double Rand = 1;

    /// <summary>Abstand der Deko zur Mauerlinie (Meter).</summary>
    public const double RandMauer = 3;

    private readonly Bodenraster _raster;
    private readonly Naturfreiraum? _frei;

    /// <summary>Je Textur: Zellgröße des Gitters und die Teile (Baustein, Gewicht); null: streut nichts.</summary>
    private readonly (double Zelle, List<(Baustein Art, double Gewicht)> Teile)?[] _texturen = new (double, List<(Baustein, double)>)?[Bodenraster.Texturen];

    /// <param name="raster">Das Bodenraster.</param>
    /// <param name="katalog">Der Katalog mit den Bodentexturen und den Natur-Bausteinen.</param>
    /// <param name="frei">Wo nichts liegen darf (null: überall erlaubt).</param>
    public Bodendeko(Bodenraster raster, Gebaeudekatalog katalog, Naturfreiraum? frei)
    {
        (_raster, _frei) = (raster, frei);
        foreach (var d in katalog.Bodentexturen)
        {
            if (d.Textur is not { } t || d.Dichte <= 0 || _texturen[(int)t] is not null) continue;
            var teile = d.Deko.Where(e => e.Value > 0).Select(e => (Art: katalog.Finde(e.Key), Gewicht: e.Value))
                .Where(e => e.Art is { Art: Bausteinart.Natur }).Select(e => (e.Art!, e.Gewicht)).ToList();
            if (teile.Count == 0) continue;
            // bei doppelter Dichte (Dekodichte 255) ein Kandidat je Zelle
            _texturen[(int)t] = (Math.Max(0.5, Math.Sqrt(100 / (2 * d.Dichte))), teile);
        }
    }

    /// <summary>Die Deko einer Stadtkarte (für das Spiel); <paramref name="modellmass"/> wie bei <see cref="Naturhindernis.Von"/>.</summary>
    public static Bodendeko Von(Stadtkarte karte, Func<Baustein, (double X, double Z)?>? modellmass = null) =>
        new(karte.Boden ?? new Bodenraster(), karte.Katalog ?? new Gebaeudekatalog(), Naturfreiraum.Aus(karte, modellmass));

    /// <summary>Die Kacheln, in denen Deko liegen kann (Dekodichte irgendwo nicht 0).</summary>
    public IEnumerable<(int I, int J)> Kacheln => _raster.Kacheln.Where(_raster.HatDeko).OrderBy(k => k.J).ThenBy(k => k.I);

    /// <summary>Alle Deko-Teile (Kachel für Kachel).</summary>
    public List<Pflanzenlage> Alle() => Kacheln.SelectMany(Kachel).ToList();

    /// <summary>Die Deko-Teile einer Kachel des Bodenrasters (64 × 64 m): Art, Ort, Drehung, Größe, Variante. Gleicher Boden, gleiche Teile.</summary>
    public List<Pflanzenlage> Kachel((int I, int J) kachel)
    {
        var liste = new List<Pflanzenlage>();
        if (!_raster.HatDeko(kachel)) return liste;
        var (x0, z0) = (kachel.I * Bodenraster.Kachel * Bodenraster.Abstand, kachel.J * Bodenraster.Kachel * Bodenraster.Abstand);
        var (x1, z1) = (x0 + Bodenraster.Kachel * Bodenraster.Abstand, z0 + Bodenraster.Kachel * Bodenraster.Abstand);
        for (var t = 0; t < Bodenraster.Texturen; t++)
        {
            if (_texturen[t] is not var (zelle, teile)) continue;
            var textur = (Bodentextur)t;
            if (textur != Bodentextur.Wiese && !_raster.HatKanal(kachel, t - 1)) continue;
            var summe = teile.Sum(e => e.Gewicht);
            for (var cj = (int)Math.Floor(z0 / zelle); cj <= (int)Math.Floor(z1 / zelle); cj++)
                for (var ci = (int)Math.Floor(x0 / zelle); ci <= (int)Math.Floor(x1 / zelle); ci++)
                {
                    var h = Zelle(_raster.Startwert, t, ci, cj);
                    var (x, z) = ((ci + Wurf(h, 0)) * zelle, (cj + Wurf(h, 1)) * zelle);
                    if (x < x0 || x >= x1 || z < z0 || z >= z1) continue; // gehört zur Nachbarkachel
                    var chance = _raster.Anteil(x, z, textur) * Bodenraster.Dekofaktor(Dekodichte(x, z)) / 2;
                    if (Wurf(h, 2) >= chance) continue;
                    if (_frei is not null && !_frei.FreiFuerBoden(x, z, Rand, RandMauer)) continue;
                    var wahl = Wurf(h, 3) * summe;
                    var art = teile[^1].Art;
                    foreach (var (a, g) in teile)
                    {
                        wahl -= g;
                        if (wahl < 0)
                        {
                            art = a;
                            break;
                        }
                    }
                    var variante = (int)(Wurf(h, 4) * Math.Max(1, art.Varianten)) + 1;
                    liste.Add(new Pflanzenlage(art, x, z, Wurf(h, 5) * 360, 0.8 + Wurf(h, 6) * 0.4, Math.Min(variante, Math.Max(1, art.Varianten))));
                }
        }
        return liste;
    }

    /// <summary>Die Dekodichte an einem Ort (bilinear, gerundet).</summary>
    private byte Dekodichte(double x, double z)
    {
        var (i, j) = ((int)Math.Floor(x / Bodenraster.Abstand), (int)Math.Floor(z / Bodenraster.Abstand));
        var (tx, tz) = (x / Bodenraster.Abstand - i, z / Bodenraster.Abstand - j);
        double a = _raster.Dekodichte(i, j), b = _raster.Dekodichte(i + 1, j), c = _raster.Dekodichte(i, j + 1), d = _raster.Dekodichte(i + 1, j + 1);
        return (byte)Math.Round((a + (b - a) * tx) * (1 - tz) + (c + (d - c) * tx) * tz);
    }

    /// <summary>Eine feste Zahl je Startwert, Textur und Zelle (Hash-Mischung, kein Zufallsgenerator: auf jedem Rechner gleich).</summary>
    private static ulong Zelle(int startwert, int textur, int ci, int cj) =>
        Mischen(Mischen(((ulong)(uint)startwert << 32) | (uint)textur) ^ (((ulong)(uint)ci << 32) | (uint)cj));

    /// <summary>Der n-te Wurf 0 … &lt;1 aus der Zahl einer Zelle.</summary>
    private static double Wurf(ulong h, int n) => (Mischen(h + (ulong)n) >> 11) * (1.0 / (1UL << 53));

    private static ulong Mischen(ulong x)
    {
        unchecked
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }
}
