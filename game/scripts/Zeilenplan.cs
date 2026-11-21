using System;
using System.Collections.Generic;

namespace Reichsgilde;

/// <summary>
/// Modulares Wohnhaussystem (Variante A, Nutzer 20.11.2026): Planung einer Häuserzeile ohne Godot – welche Module wo stehen.
/// Eine Zeile hat eine Vorderlinie (Flucht, Knickpunkte), eine Seite, auf der die Häuser stehen, und eine Folge von Parzellen
/// (Satz = Volk und Stufe, Modul = Körper). Die Nachbarregel (Konzept 4.3) wählt die Seitenstücke: Giebelhäuser bekommen ihre
/// Traufseiten (blind am Nachbarn, mit Fenstern am freien Ende) und die halbe Traufgasse, Traufhäuser eine Brandwand bzw. eine
/// offene Giebelwand, wo ihr Giebel über den Nachbarn ragt. An einem rechten Knick steht das Eckhaus, an schrägen Knicks ein
/// Keilstück (Hofmauer mit Tor), unter 60° bleibt der Keil leer. Die Rechnung ist rein und deterministisch (Tests: ZeilenplanTests).
/// Die Module baut <c>tools/modelle/neubau/zeile_module.py</c>; die Maße hier müssen zu dessen SAETZE passen.
/// </summary>
public static class Zeilenplan
{
    /// <summary>Haustiefe aller Körper (m).</summary>
    public const float Tiefe = 8f;
    /// <summary>Einrückung der Giebelhaus-Wände (halbe Traufgasse, m).</summary>
    public const float Ein = 0.25f;
    /// <summary>Breite eines Mauerstücks im Keil (m); Tor 3 m.</summary>
    public const float MauerStueck = 1f, TorBreite = 3f;

    /// <summary>Ein Satz Module (Volk × Stufe): Traufhöhe (Summe der Geschosse), Dachneigungen (Tangens; Giebel- und Eckhaus bzw.
    /// Traufhaus), Auskragung (0,4 m nur bei Fachwerk im Obergeschoss), Steinbau (Obergeschoss gemauert: am freien Ende die Brandwand).</summary>
    public sealed record Satz(string Name, string Volk, int Stufe, float Traufhoehe, float NeigungGiebel, float NeigungTrauf, float Krag, bool Stein);

    /// <summary>Die gebauten Sätze (Reihenfolge = Ebene im Array der Zeilen-AO-Atlanten <c>zeilen_ao_ebenen.png</c>).</summary>
    public static readonly Satz[] Saetze =
    {
        new("mensch_s1", "mensch", 1, 5.6f, Tan(50), Tan(45), 0.4f, false),
        new("mensch_s2", "mensch", 2, 6.4f, Tan(52), Tan(45), 0.4f, false),
        new("mensch_s3", "mensch", 3, 8.4f, Tan(52), Tan(45), 0f, true),
        new("zwerg_s1", "zwerg", 1, 5.2f, Tan(45), Tan(40), 0f, true),
        new("zwerg_s2", "zwerg", 2, 5.6f, Tan(48), Tan(42), 0.4f, false),
        new("zwerg_s3", "zwerg", 3, 8f, Tan(48), Tan(42), 0f, true),
        new("elb_s1", "elb", 1, 6f, Tan(58), Tan(52), 0f, false),
        new("elb_s2", "elb", 2, 6.4f, Tan(58), Tan(52), 0.4f, false),
        new("elb_s3", "elb", 3, 8.4f, Tan(58), Tan(52), 0f, true),
        new("halbling_s1", "halbling", 1, 4f, Tan(48), Tan(45), 0.4f, false),
        new("halbling_s2", "halbling", 2, 5.6f, Tan(50), Tan(45), 0.4f, false),
        new("halbling_s3", "halbling", 3, 6f, Tan(50), Tan(45), 0f, true),
    };

    static float Tan(float grad) => MathF.Tan(grad * MathF.PI / 180f);

    public static Satz? SatzVon(string name) => Array.Find(Saetze, s => s.Name == name);
    public static int AoEbene(string satz) => Math.Max(0, Array.FindIndex(Saetze, s => s.Name == satz));

    /// <summary>Breite eines Körpermoduls (m); 0 für Seitenstücke.</summary>
    public static float Breite(string modul) => modul switch
    {
        "k_g4" => 4, "k_g6" => 6, "k_g8" => 8, "k_t6" => 6, "k_t8" => 8, "eck" => Tiefe, _ => 0,
    };

    public enum Stellung { Giebel, Trauf, Eck }

    public static Stellung StellungVon(string modul) => modul == "eck" ? Stellung.Eck : modul.StartsWith("k_g") ? Stellung.Giebel : Stellung.Trauf;

    /// <summary>Art einer Zeile. Seit dem 21.11.2026 stehen alle Häuser mit dem Giebel zur Gasse (Nutzer; vorher: Zeilen der
    /// Passanten mit der Traufe); die Art unterscheidet nur noch, wem die Häuser gehören.</summary>
    public enum Zeilenart { Buerger, Kulisse }

    /// <summary>Das Körpermodul für eine Parzelle der Breite <paramref name="breite"/> (m): für jede Zeilenart ein Giebelhaus
    /// (4, 6 oder 8 m), sonst leer. Die Traufhäuser (<c>k_t6</c>, <c>k_t8</c>) und ihre Kappen liegen weiter in den GLB und
    /// lassen sich ausdrücklich nennen, werden aber von keiner Zeilenart mehr gewählt.</summary>
    public static string Koerper(Zeilenart art, int breite) => breite is 4 or 6 or 8 ? "k_g" + breite : "";

    /// <summary>Fester Streuwert einer Zeichenkette (FNV-1a, 32 Bit): gleich auf jedem Rechner, ohne Zufallsquelle.</summary>
    public static uint Streuwert(string s) { uint h = 2166136261; foreach (var c in s) h = (h ^ c) * 16777619; return h; }

    /// <summary>Name der Variante <paramref name="index"/> eines Moduls: <c>k_g6</c> → <c>k_g6_a</c>, <c>k_g6_b</c> … (Lochbilder der
    /// Fassade, vom Bauskript geliefert). So heißen die Objekte in der GLB.</summary>
    public static string VariantenName(string modul, int index) => modul + "_" + (char)('a' + index);

    /// <summary>
    /// Wählt je Haus die Variante seines Körpermoduls (Fenster-Lochbild): fest aus Zeilenkennung, Parzelle und Modul, ohne
    /// Zufallsquelle – dieselbe Zeile sieht auf jedem Rechner und in jeder Partie gleich aus. Ein Haus bekommt nie die Variante
    /// des vorigen Hauses mit demselben Satz und Modul in der Liste, auch wenn andere Häuser dazwischen stehen (ab zwei
    /// Varianten) – so stehen gleiche Häuser nie mit demselben Lochbild nebeneinander, und drei gleiche in einer Zeile sehen
    /// nicht alle gleich aus.
    /// <paramref name="anzahl"/> nennt, wie viele Varianten es für (Satz, Modul) gibt; 0 oder 1: Index 0 (das Modul ohne Zusatz).
    /// Rückgabe: Index je Eintrag der Liste.
    /// </summary>
    public static int[] VariantenWahl(string kennung, IReadOnlyList<(string Satz, string Modul, int Parzelle)> haeuser, Func<string, string, int> anzahl)
    {
        var aus = new int[haeuser.Count];
        var zuletzt = new Dictionary<(string, string), int>();   // letzte Variante je (Satz, Modul); nur nachgeschlagen, nie durchlaufen
        for (var i = 0; i < haeuser.Count; i++)
        {
            var (satz, modul, parzelle) = haeuser[i];
            var n = anzahl(satz, modul);
            if (n <= 1) continue;
            var h = Streuwert($"{kennung}|{parzelle}|{modul}");
            var index = (int)(h % (uint)n);
            if (zuletzt.TryGetValue((satz, modul), out var vorige) && vorige == index)
                index = (index + 1 + (int)((h / 7919u) % (uint)(n - 1))) % n;   // um 1 bis n − 1 weiter: sicher eine andere
            aus[i] = index;
            zuletzt[(satz, modul)] = index;
        }
        return aus;
    }

    /// <summary>Variante eines Seitenstücks (offene Seite, Eckhaus …) für eine Parzelle: fest wie <see cref="VariantenWahl"/>, ohne Nachbarregel.</summary>
    public static int Variante(string kennung, int parzelle, string modul, int anzahl) =>
        anzahl <= 1 ? 0 : (int)(Streuwert($"{kennung}|{parzelle}|{modul}") % (uint)anzahl);

    /// <summary>Ein Haus einer Zeile: Satz und Körpermodul. <c>Modul</c> leer = freie Parzelle (Lücke) der Breite <c>Frei</c>.</summary>
    public sealed record Haus(string Satz, string Modul, float Frei = 0f)
    {
        public float Breite => Modul.Length == 0 ? Frei : Zeilenplan.Breite(Modul);
        public bool Leer => Modul.Length == 0;
    }

    /// <summary>Ein gestelltes Modul im Rahmen einer geraden Strecke: X0 = linke Kante der Parzelle (von der Gasse aus), Gespiegelt =
    /// rechte Seite (das Spiel spiegelt x → Breite − x), Parzelle = Index des Hauses in der Zeile (−1: Keilstück).</summary>
    public readonly record struct Stueck(string Satz, string Modul, float X0, float Breite, bool Gespiegelt, int Parzelle);

    /// <summary>Höhe des Giebels eines Traufhauses über dem Boden (Unterseite der Deckung am First).</summary>
    public static float Firsthoehe(Satz s) => s.Traufhoehe + (Tiefe + s.Krag) / 2f * s.NeigungTrauf;

    /// <summary>
    /// Ragt der Seitenumriss des Hauses <paramref name="h"/> über den des Nachbarn <paramref name="n"/> (null: kein Nachbar)?
    /// Giebel- und Eckhäuser zeigen zur Seite nur die Traufe (flach in Traufhöhe), Traufhäuser ihr Giebeldreieck.
    /// </summary>
    public static bool Ragt(Haus h, Haus? n)
    {
        if (n is null || n.Leer) return true;
        var (sh, sn) = (SatzVon(h.Satz)!, SatzVon(n.Satz)!);
        if (StellungVon(h.Modul) != Stellung.Trauf) return sh.Traufhoehe > sn.Traufhoehe + 0.05f;
        if (StellungVon(n.Modul) != Stellung.Trauf) return true;
        return Firsthoehe(sh) > Firsthoehe(sn) + 0.05f || sh.Traufhoehe > sn.Traufhoehe + 0.05f;
    }

    /// <summary>Ein Haus an seinem Platz auf einer geraden Strecke: X = linke Kante (von der Gasse aus), Index = Parzelle in der Zeile.</summary>
    public readonly record struct Platz(Haus Haus, float X, int Index);

    /// <summary>
    /// Die Module einer geraden Strecke: je Haus der Körper und nach der Nachbarregel die Seitenstücke. Die Plätze sind von der Gasse
    /// aus gesehen von links nach rechts geordnet. Nachbar ist ein Haus, das Kante an Kante steht (freie Parzellen und Lücken: kein
    /// Nachbar). <paramref name="links"/> bzw. <paramref name="rechts"/>: Nachbar jenseits des Anfangs bzw. Endes (das Eckhaus am
    /// Knick davor), sonst null.
    /// </summary>
    public static List<Stueck> Stuecke(IReadOnlyList<Platz> plaetze, Haus? links = null, Haus? rechts = null)
    {
        var aus = new List<Stueck>();
        for (var i = 0; i < plaetze.Count; i++)
        {
            var (h, x, index) = plaetze[i];
            var w = h.Breite;
            Haus? vor = i > 0 ? (MathF.Abs(plaetze[i - 1].X + plaetze[i - 1].Haus.Breite - x) < 0.05f ? plaetze[i - 1].Haus : null) : links;
            Haus? nach = i + 1 < plaetze.Count ? (MathF.Abs(x + w - plaetze[i + 1].X) < 0.05f ? plaetze[i + 1].Haus : null) : rechts;
            aus.Add(new Stueck(h.Satz, h.Modul, x, w, false, index));
            var satz = SatzVon(h.Satz)!;
            foreach (var (spiegel, n) in new[] { (false, vor), (true, nach) })
            {
                switch (StellungVon(h.Modul))
                {
                    case Stellung.Giebel:
                        aus.Add(new Stueck(h.Satz, n is null ? "seite_offen" : "seite_blind", x, w, spiegel, index));
                        if (n is not null) aus.Add(new Stueck(h.Satz, "gasse", x, w, spiegel, index));
                        break;
                    case Stellung.Trauf when Ragt(h, n):
                        aus.Add(new Stueck(h.Satz, n is not null || satz.Stein ? "kappe_brand" : "kappe_offen", x, w, spiegel, index));
                        break;
                }
            }
        }
        return aus;
    }

    /// <summary>Ein Modul in der Ebene: Ursprung (vordere linke Ecke des Modulrahmens, x/z), Richtung der Modul-x-Achse (entlang der
    /// Gasse, von der Gasse aus nach rechts) und der Tiefe (in die Häuser hinein). Gespiegelt wie <see cref="Stueck"/>.</summary>
    public readonly record struct Lage(string Satz, string Modul, float Ox, float Oz, float Ex, float Ez, float Tx, float Tz, float Breite, bool Gespiegelt, int Parzelle);

    /// <summary>Wie <see cref="Planen(IReadOnlyList{ValueTuple{float, float}}, bool, IReadOnlyList{Haus}, out int)"/>, ohne die Zahl der gestellten Häuser.</summary>
    public static List<Lage> Planen(IReadOnlyList<(float X, float Z)> linie, bool links, IReadOnlyList<Haus> haeuser) => Planen(linie, links, haeuser, out _);

    /// <summary>
    /// Plant eine ganze Zeile entlang der Vorderlinie <paramref name="linie"/> (Punkte x/z). <paramref name="links"/>: die Häuser stehen
    /// links der Laufrichtung. Die Häuser werden der Reihe nach auf die Strecken verteilt; ein Eckhaus steht am Ende seiner Strecke
    /// (rechter Knick zur Häuserseite hin), die nächste Strecke beginnt dann um die Haustiefe versetzt. An schrägen Knicks zur
    /// Häuserseite bleibt je Strecke der Keil frei (Tiefe / tan(Winkel / 2)), dort steht die Hofmauer mit Tor; unter 60° nichts.
    /// Passt ein Haus nicht mehr auf seine Strecke, beginnt es auf der nächsten. <paramref name="gestellt"/>: so viele Einträge der
    /// Liste haben Platz gefunden (kleiner als die Liste: die Linie ist zu kurz, die Kartenprüfung soll das melden).
    /// </summary>
    public static List<Lage> Planen(IReadOnlyList<(float X, float Z)> linie, bool links, IReadOnlyList<Haus> haeuser, out int gestellt)
    {
        var aus = new List<Lage>();
        gestellt = 0;
        if (linie.Count < 2) return aus;
        var n = linie.Count - 1;
        // Freihalten je Streckenende/-anfang am Knick zur Häuserseite: (Ende von Strecke k, Anfang von Strecke k+1)
        var vorEnde = new float[n]; var nachAnfang = new float[n]; var recht = new bool[n]; var winkel = new float[n];
        for (var k = 0; k + 1 < n; k++)
        {
            var (ax, az) = Richtung(linie[k], linie[k + 1]);
            var (bx, bz) = Richtung(linie[k + 1], linie[k + 2]);
            var kreuz = ax * bz - az * bx;          // < 0: Linksdrehung (Godot von oben: x nach Osten, z nach Süden)
            var zurHausseite = links ? kreuz < -1e-4f : kreuz > 1e-4f;
            if (!zurHausseite) continue;
            var drehung = MathF.Acos(Math.Clamp(ax * bx + az * bz, -1f, 1f));   // Abweichung von gerade (0 = gerade)
            var innen = MathF.PI - drehung;                                        // Innenwinkel auf der Häuserseite
            winkel[k] = innen * 180f / MathF.PI;
            recht[k] = MathF.Abs(winkel[k] - 90f) <= 0.6f;
            var frei = recht[k] ? Tiefe : Tiefe / MathF.Tan(innen / 2f);
            vorEnde[k] = frei; nachAnfang[k + 1] = frei;
        }
        var hausIndex = 0;
        Haus? eckVorher = null;
        var letzterSatz = haeuser.Count > 0 ? haeuser[0].Satz : Saetze[0].Name;
        var mitEckAn = new bool[n];
        for (var k = 0; k < n; k++)
        {
            (float X, float Z) d = Richtung(linie[k], linie[k + 1]);
            var laenge = Abstand(linie[k], linie[k + 1]);
            var ende = laenge - vorEnde[k];
            var plaetze = new List<Platz>();
            var x = nachAnfang[k];
            while (hausIndex < haeuser.Count)
            {
                var h = haeuser[hausIndex];
                if (h.Modul == "eck")
                {
                    if (!recht[k] || x + h.Breite > laenge + 0.05f) break;
                    plaetze.Add(new Platz(h, laenge - h.Breite, hausIndex)); hausIndex++; mitEckAn[k] = true; break;
                }
                if (x + h.Breite > ende + 0.05f) break;
                if (!h.Leer) plaetze.Add(new Platz(h, x, hausIndex));
                x += h.Breite; hausIndex++;
            }
            if (plaetze.Count > 0) letzterSatz = plaetze[^1].Haus.Satz;
            // Nachbarregel in der Sicht von der Gasse: links der Laufrichtung läuft sie mit der Strecke, rechts gegen sie
            List<Stueck> stuecke;
            if (links) stuecke = Stuecke(plaetze, links: eckVorher);
            else
            {
                var sicht = new List<Platz>();
                for (var i = plaetze.Count - 1; i >= 0; i--) sicht.Add(plaetze[i] with { X = laenge - plaetze[i].X - plaetze[i].Haus.Breite });
                stuecke = Stuecke(sicht, rechts: eckVorher);
            }
            foreach (var s in stuecke)
            {
                // Das Eckhaus zeigt seine zweite Schauseite zum Knick: rechts der Laufrichtung liegt er von der Gasse aus links, also gespiegelt
                var st = !links && s.Modul == "eck" ? s with { Gespiegelt = true } : s;
                aus.Add(links
                    ? new Lage(st.Satz, st.Modul, linie[k].X + d.X * st.X0, linie[k].Z + d.Z * st.X0, d.X, d.Z, d.Z, -d.X, st.Breite, st.Gespiegelt, st.Parzelle)
                    : new Lage(st.Satz, st.Modul, linie[k].X + d.X * (laenge - st.X0), linie[k].Z + d.Z * (laenge - st.X0), -d.X, -d.Z, -d.Z, d.X, st.Breite, st.Gespiegelt, st.Parzelle));
            }
            eckVorher = mitEckAn[k] ? plaetze[^1].Haus : null;
            // Knick zur Häuserseite ohne Eckhaus: Keilstück (Hofmauer mit Tor), unter 60° bleibt der Keil leer
            if (k + 1 < n && vorEnde[k] > 0 && !mitEckAn[k] && winkel[k] >= 60f - 0.01f)
                aus.AddRange(Keil(letzterSatz, linie[k], linie[k + 1], linie[k + 2], vorEnde[k], links));
        }
        gestellt = hausIndex;
        return aus;
    }

    /// <summary>Keilstück: Pfeiler an beiden Enden und am Knick, dazwischen Mauerstücke, auf der längeren Seite ein Tor (falls sie reicht).</summary>
    static IEnumerable<Lage> Keil(string satz, (float X, float Z) a, (float X, float Z) knick, (float X, float Z) b, float frei, bool links)
    {
        var (ax, az) = Richtung(a, knick); var (bx, bz) = Richtung(knick, b);
        var anfang = (X: knick.X - ax * frei, Z: knick.Z - az * frei);
        var ende = (X: knick.X + bx * frei, Z: knick.Z + bz * frei);
        foreach (var p in new[] { anfang, knick, ende }) yield return new Lage(satz, "pfeiler", p.X, p.Z, 1, 0, 0, 1, 0, false, -1);
        var torGesetzt = false;
        foreach (var (p, q) in new[] { (anfang, knick), (knick, ende) })
        {
            var (dx, dz) = Richtung(p, q);
            var (tx, tz) = links ? (dz, -dx) : (-dz, dx);
            var laenge = Abstand(p, q);
            var s = 0.3f;   // halber Pfeiler
            if (!torGesetzt && laenge >= TorBreite + 2 * 0.3f + 0.1f)
            {
                var t0 = (laenge - TorBreite) / 2f;
                for (var x = s; x < t0 - 0.01f; x += MauerStueck) yield return Mauer(satz, p, dx, dz, tx, tz, MathF.Min(x, t0 - MauerStueck));
                yield return new Lage(satz, "tor", p.X + dx * t0, p.Z + dz * t0, dx, dz, tx, tz, TorBreite, false, -1);
                for (var x = t0 + TorBreite; x < laenge - s - 0.01f; x += MauerStueck) yield return Mauer(satz, p, dx, dz, tx, tz, MathF.Min(x, laenge - s - MauerStueck));
                torGesetzt = true;
            }
            else
                for (var x = s; x < laenge - s - 0.01f; x += MauerStueck) yield return Mauer(satz, p, dx, dz, tx, tz, MathF.Min(x, laenge - s - MauerStueck));
        }
    }

    static Lage Mauer(string satz, (float X, float Z) p, float dx, float dz, float tx, float tz, float x) =>
        new(satz, "mauer", p.X + dx * x, p.Z + dz * x, dx, dz, tx, tz, MauerStueck, false, -1);

    static (float, float) Richtung((float X, float Z) a, (float X, float Z) b)
    {
        var (dx, dz) = (b.X - a.X, b.Z - a.Z);
        var l = MathF.Sqrt(dx * dx + dz * dz);
        return l < 1e-6f ? (1f, 0f) : (dx / l, dz / l);
    }

    static float Abstand((float X, float Z) a, (float X, float Z) b) => MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
}
