using System.Text;

namespace Reichsgilde.Core.Bauen;

/// <summary>
/// Ein Kasten in einer Restfläche (<see cref="Restflaechenfuellung.Zerlegen"/>): ein Rechteck aus Rasterfeldern, das die Darstellung mit einem Prop-Satz füllt.
/// <see cref="I"/> und <see cref="J"/> sind das Feld mit den kleinsten Nummern (Ecke in −x/−z), <see cref="Breite"/> die Felder in x-, <see cref="Tiefe"/> die in z-Richtung.
/// </summary>
public readonly record struct Fuellkasten(int I, int J, int Breite, int Tiefe)
{
    /// <summary>Mitte des Kastens in Metern (Raster 2 m).</summary>
    public (double X, double Z) Mitte => ((I + Breite / 2.0) * Restflaechenfuellung.Raster, (J + Tiefe / 2.0) * Restflaechenfuellung.Raster);

    /// <summary>Die kurze und die lange Seite in Feldern.</summary>
    public int Kurz => Math.Min(Breite, Tiefe);
    public int Lang => Math.Max(Breite, Tiefe);

    /// <summary>Name des Formats ohne Drehung: „1x3“, „2x2“ (kurze Seite zuerst).</summary>
    public string Format => $"{Kurz}x{Lang}";

    /// <summary>Liegt die lange Seite in z-Richtung (der Satz wird dann um 90° gedreht)?</summary>
    public bool Hochkant => Tiefe > Breite;
}

/// <summary>
/// Die Füllung der Restflächen (<see cref="Restflaeche"/>), soweit sie rechnet und nicht zeichnet: Felder der Fläche, Zerlegung in Formate,
/// Nachbarart und ein fester Würfel. Reine Darstellung, ohne Wirkung aufs Spiel; was dort steht, entscheidet die Stadtansicht (<c>game/daten/restflaechen.json</c>).
/// Alles hängt nur von der Karte ab (Umriss, Gebäude, Kennungen) und ist darum bei jedem Laden gleich – keine Uhr, kein <c>Random</c>, keine Hash-Reihenfolge.
/// </summary>
public static class Restflaechenfuellung
{
    /// <summary>Rastergröße in Metern (wie die Bauzonen, <c>Rasterbauzonen.Raster</c>).</summary>
    public const double Raster = 2;

    /// <summary>
    /// Die Formate in der Reihenfolge, in der die Zerlegung sie versucht (Breite in x × Tiefe in z): groß vor klein, bei gleicher Größe quer vor hochkant.
    /// 3×3 und 2×3 zuerst, damit große Flächen in wenige zusammenhängende Sätze zerfallen; 1×1 ist der Rest und passt immer.
    /// </summary>
    public static readonly IReadOnlyList<(int Breite, int Tiefe)> Formate = new (int, int)[]
    {
        (3, 3), (3, 2), (2, 3), (5, 1), (1, 5), (2, 2), (4, 1), (1, 4), (3, 1), (1, 3), (2, 1), (1, 2), (1, 1),
    };

    /// <summary>
    /// Die Felder der Restfläche (Nummern <c>i</c> = x/2, <c>j</c> = z/2), deren Mitte im Umriss liegt, zeilenweise sortiert (erst j, dann i).
    /// </summary>
    public static IReadOnlyList<(int I, int J)> Felder(Restflaeche r)
    {
        var felder = new List<(int I, int J)>();
        if (r.Flaeche.Count < 3) return felder;
        var (i0, i1) = (int.MaxValue, int.MinValue);
        var (j0, j1) = (int.MaxValue, int.MinValue);
        foreach (var p in r.Flaeche)
        {
            i0 = Math.Min(i0, (int)Math.Floor(p[0] / Raster)); i1 = Math.Max(i1, (int)Math.Ceiling(p[0] / Raster));
            j0 = Math.Min(j0, (int)Math.Floor(p[1] / Raster)); j1 = Math.Max(j1, (int)Math.Ceiling(p[1] / Raster));
        }
        for (var j = j0; j < j1; j++)
            for (var i = i0; i < i1; i++)
                if (Geometrie.ImVieleck((i + 0.5) * Raster, (j + 0.5) * Raster, r.Flaeche)) felder.Add((i, j));
        return felder;
    }

    /// <summary>
    /// Zerlegt eine Feldmenge lückenlos in Kästen (Formate 3×3 … 1×1). Greedy und deterministisch: Das erste freie Feld (zeilenweise) ist die
    /// Ecke; dort wird das größte Format der Liste <see cref="Formate"/> gelegt, das ganz aus freien Feldern besteht. Dasselbe Ergebnis für dieselbe Menge,
    /// gleich in welcher Reihenfolge sie kommt.
    /// </summary>
    public static IReadOnlyList<Fuellkasten> Zerlegen(IEnumerable<(int I, int J)> felder)
    {
        var liste = felder.Distinct().ToList();
        var kaesten = new List<Fuellkasten>();
        if (liste.Count == 0) return kaesten;
        var (i0, j0) = (liste.Min(f => f.I), liste.Min(f => f.J));
        var (breite, tiefe) = (liste.Max(f => f.I) - i0 + 1, liste.Max(f => f.J) - j0 + 1);
        var frei = new bool[breite, tiefe];
        foreach (var (i, j) in liste) frei[i - i0, j - j0] = true;
        for (var j = 0; j < tiefe; j++)
            for (var i = 0; i < breite; i++)
            {
                if (!frei[i, j]) continue;
                foreach (var (b, t) in Formate)
                {
                    if (!Passt(frei, breite, tiefe, i, j, b, t)) continue;
                    for (var dj = 0; dj < t; dj++)
                        for (var di = 0; di < b; di++) frei[i + di, j + dj] = false;
                    kaesten.Add(new Fuellkasten(i + i0, j + j0, b, t));
                    break;
                }
            }
        return kaesten;
    }

    private static bool Passt(bool[,] frei, int breite, int tiefe, int i, int j, int b, int t)
    {
        if (i + b > breite || j + t > tiefe) return false;
        for (var dj = 0; dj < t; dj++)
            for (var di = 0; di < b; di++)
                if (!frei[i + di, j + dj]) return false;
        return true;
    }

    /// <summary>
    /// Steht auf dem Kasten inzwischen ein Gebäude (Mitte oder eine der vier Ecken, 0,4 m nach innen versetzt, liegt auf dem Grundriss samt 0,3 m Rand)?
    /// Dann fällt die Füllung dort weg. Eine Restfläche hält schon beim Rechnen Abstand zu Gebäuden – nur Änderungen nach der Rechnung (Kartenwerkstatt) können das auslösen.
    /// </summary>
    public static bool Bebaut(Fuellkasten k, Naturfreiraum frei)
    {
        var (mx, mz) = k.Mitte;
        var (hx, hz) = (k.Breite * Raster / 2 - 0.4, k.Tiefe * Raster / 2 - 0.4);
        return frei.AufGebaeude(mx, mz, 0.3) || frei.AufGebaeude(mx - hx, mz - hz, 0.3) || frei.AufGebaeude(mx + hx, mz - hz, 0.3)
            || frei.AufGebaeude(mx - hx, mz + hz, 0.3) || frei.AufGebaeude(mx + hx, mz + hz, 0.3);
    }

    /// <summary>Die Nachbarschaften, nach denen die Darstellung das Thema wählt.</summary>
    public const string Wohnen = "wohnen", Werkstatt = "werkstatt", Kirche = "kirche", Rand = "rand", Sonst = "sonst";

    /// <summary>So nah (Meter, vom Rand des Gebäudes) zählt ein Gebäude als Nachbar.</summary>
    public const double Nachbarabstand = 10;

    /// <summary>
    /// Die Nachbarart einer Restfläche: Welche Art Gebäude steht im Umkreis von <see cref="Nachbarabstand"/> Metern am häufigsten? Gleichstand entscheidet die Reihenfolge
    /// Wohnen, Werkstatt, Kirche, Sonst. Ohne Gebäude in der Nähe, am Mauerring (nahe einer Mauer) und in der Vorstadt zählt außerdem „Rand“ (bei Gleichstand nach den Gebäuden).
    /// </summary>
    public static string Nachbarart(Restflaeche r, Stadtkarte karte)
    {
        var felder = Felder(r);
        if (felder.Count == 0) return Rand;
        // höchstens 12 gleichmäßig gewählte Felder genügen für „wie nah ist ein Nachbar“ (große Flächen würden sonst jede Mauerstrecke hundertfach messen)
        var schritt = Math.Max(1, felder.Count / 12);
        var mitten = Enumerable.Range(0, felder.Count).Where(n => n % schritt == 0 || n == felder.Count - 1).Select(n => ((felder[n].I + 0.5) * Raster, (felder[n].J + 0.5) * Raster)).ToList();
        var zaehler = new Dictionary<string, int> { [Wohnen] = 0, [Werkstatt] = 0, [Kirche] = 0, [Sonst] = 0 };
        foreach (var g in karte.Gebaeude)
        {
            var radius = g.Grundriss is { } gr ? Math.Max(gr.Breite, gr.Tiefe) / 2 : 3;
            var abstand = mitten.Min(m => Math.Sqrt((m.Item1 - g.Punkt[0]) * (m.Item1 - g.Punkt[0]) + (m.Item2 - g.Punkt[1]) * (m.Item2 - g.Punkt[1]))) - radius;
            if (abstand <= Nachbarabstand) zaehler[Art(g)]++;
        }
        var randGewicht = 0;
        if (r.Art == Lage.Vorstadt) randGewicht += 2;
        foreach (var m in karte.Mauern)
        {
            var linie = m.Geschlossen && m.Linie.Count > 0 ? m.Linie.Append(m.Linie[0]).ToList() : m.Linie;
            for (var i = 0; i + 1 < linie.Count; i++)
                if (mitten.Any(p => Geometrie.AbstandZurStrecke(p.Item1, p.Item2, linie[i][0], linie[i][1], linie[i + 1][0], linie[i + 1][1]) <= Nachbarabstand)) { randGewicht += 2; goto mauerGesehen; }
        }
        mauerGesehen:
        if (zaehler.Values.Sum() == 0) return Rand;
        var beste = Wohnen;
        foreach (var art in new[] { Wohnen, Werkstatt, Kirche, Sonst })
            if (zaehler[art] > zaehler[beste]) beste = art;
        return randGewicht > zaehler[beste] ? Rand : beste;
    }

    private static string Art(Kartengebaeude g)
    {
        if (g.Art is Bausteinart.Wohnhausplatz or Bausteinart.Einwohnerplatz or Bausteinart.Kulisse || g.Katalog.StartsWith("kulisse_")) return Wohnen;
        if (g.Art is Bausteinart.Werkstattplatz or Bausteinart.Umlandbetrieb) return Werkstatt;
        var k = g.Katalog.ToLowerInvariant();
        if (k.Contains("kirche") || k.Contains("dom") || k.Contains("kloster")) return Kirche;
        return Sonst;
    }

    /// <summary>
    /// Ein fester Zahlenwert aus Text und Zahlen (FNV-1a über 32 Bit) – gleich auf jedem Rechner und bei jedem Start. Daraus wählt die Darstellung Thema, Drehung und Streuung.
    /// </summary>
    public static uint Wuerfel(string text, params int[] zahlen)
    {
        var h = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(text)) h = unchecked((h ^ b) * 16777619u);
        foreach (var z in zahlen)
            for (var s = 0; s < 32; s += 8) h = unchecked((h ^ (uint)((z >> s) & 0xFF)) * 16777619u);
        h ^= h >> 15; h = unchecked(h * 2246822519u); h ^= h >> 13;
        return h;
    }

    /// <summary>Wählt nach Gewicht: Der Wurf <paramref name="wuerfel"/> fällt in einen der Abschnitte (Gewichte ≤ 0 zählen nicht). −1, wenn kein Gewicht übrig ist.</summary>
    public static int Waehlen(IReadOnlyList<int> gewichte, uint wuerfel)
    {
        var summe = gewichte.Where(g => g > 0).Sum();
        if (summe <= 0) return -1;
        var wurf = (int)(wuerfel % (uint)summe);
        for (var i = 0; i < gewichte.Count; i++)
        {
            if (gewichte[i] <= 0) continue;
            if (wurf < gewichte[i]) return i;
            wurf -= gewichte[i];
        }
        return -1;
    }

    /// <summary>Eine Zahl in [0, 1) aus einem Würfel und einer laufenden Nummer (für die Streuung der Teile).</summary>
    public static double Zufall(uint wuerfel, int nummer)
    {
        var h = unchecked((wuerfel + (uint)nummer * 2654435761u) * 2246822519u);
        h ^= h >> 15; h = unchecked(h * 3266489917u); h ^= h >> 13;
        return (h & 0xFFFFFF) / (double)0x1000000;
    }
}
