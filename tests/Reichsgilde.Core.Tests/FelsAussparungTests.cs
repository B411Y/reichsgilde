using System.Security.Cryptography;
using System.Text;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Felspinsel P3b: Beim Streuen Gebäude (mit Vorplatz) und Wege aussparen – die reine Abfrage (Felsaussparung) und der Strich. Jede Probe hat eine
/// Gegenprobe (ohne Häkchen liegen Stücke im Haus bzw. auf dem Weg), Stempel und Radierer fragen nicht, alte Striche bleiben gleich.
/// </summary>
static class FelsAussparungTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Ein Haus (Werkstattplatz 16 × 14) bei (-30, 0) und ein Weg (Land) quer von (-80, 40) nach (80, 40), flaches Gelände.</summary>
    static Kartenentwurf Strasse(out EntwurfGebaeude haus, out EntwurfWeg weg)
    {
        var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);
        haus = e.Setzen("werkstattplatz", -30, 0);
        weg = e.WegAnlegen(Wegklasse.Land, new[] { O(-80, 40), O(80, 40) }, glatt: false, einrasten: false);
        return e;
    }

    /// <summary>Dicht gestreute Blöcke (ohne Boden) über Haus und Weg, in einem Strich.</summary>
    static Felspinsel Streu(bool aussparen) => new(Formen: new[] { "block", "platte" }, Radius: 14, Dichte: 0.6, Boden: false, Aussparen: aussparen);

    static readonly Kartenort[] Strich = Enumerable.Range(0, 11).Select(i => O(-70 + i * 14, 20)).ToArray();

    static bool Beruehrt(EntwurfFels f, EntwurfGebaeude haus, EntwurfWeg weg)
    {
        var p = O(f.X, f.Z);
        var ecken = haus.Ecken();
        if (Kartengeometrie.ImVieleck(p, ecken) || Kartengeometrie.Strecken(ecken).Any(s => Kartengeometrie.AbstandStrecke(p, s.A, s.B) <= f.Radius - 0.1)) return true;
        return Kartengeometrie.AbstandLinie(p, weg.Verlauf) <= f.Radius + weg.BreiteWirksam / 2 - 0.1;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fels P3b: Die Abfrage kennt Vielecke und Linien mit Breite; ein Kreis, der sie berührt, ist belegt, daneben frei", () =>
        {
            var quadrat = new Kartenort[] { O(0, 0), O(10, 0), O(10, 10), O(0, 10) };
            var linie = new Kartenort[] { O(20, 0), O(20, 30) };
            var a = new Felsaussparung(new[] { (IReadOnlyList<Kartenort>)quadrat }, new[] { ((IReadOnlyList<Kartenort>)linie, 4.0) });
            Pruefe.Gleich(2, a.Anzahl, "zwei Hindernisse");
            Pruefe.Wahr(a.Belegt(5, 5, 1), "mitten im Quadrat");
            Pruefe.Wahr(a.Belegt(11.5, 5, 2), "der Kreis reicht über die Kante hinein");
            Pruefe.Wahr(!a.Belegt(14, 5, 2), "daneben frei");
            Pruefe.Wahr(a.Belegt(22.5, 15, 1), "auf der Straße (Breite 4: ±2, dazu der Radius)");
            Pruefe.Wahr(!a.Belegt(26, 15, 1), "neben der Straße frei");
            Pruefe.Wahr(!a.Belegt(20, 40, 3), "hinter dem Ende der Straße frei");
            Pruefe.Wahr(!new Felsaussparung(new List<IReadOnlyList<Kartenort>>(), new List<(IReadOnlyList<Kartenort>, double)>()).Belegt(0, 0, 5), "ohne Hindernisse nie");
            // ein Vieleck über mehrere Zellen (Zelle 16) wird überall gefunden
            var gross = new Kartenort[] { O(-100, -100), O(100, -100), O(100, 100), O(-100, 100) };
            Pruefe.Wahr(new Felsaussparung(new[] { (IReadOnlyList<Kartenort>)gross }, Array.Empty<(IReadOnlyList<Kartenort>, double)>()).Belegt(77, -63, 0.5), "großes Vieleck, fern vom Rand");
        }),

        ("Fels P3b: Mit „Aussparen“ liegt kein gestreutes Stück in Haus oder Weg; ohne stehen welche darin (Gegenprobe)", () =>
        {
            var e = Strasse(out var haus, out var weg);
            e.FelsStreuen(Streu(true), Strich, startwert: 7);
            Pruefe.Wahr(e.Felsen.Count > 20, $"es wurde gestreut ({e.Felsen.Count})");
            Pruefe.Gleich(0, e.Felsen.Count(f => Beruehrt(f, haus, weg)), "kein Stück im Haus oder auf dem Weg");
            Pruefe.Wahr(e.Felsen.Any(f => Math.Abs(f.Z - 40) < 12), "…aber nah am Weg stehen welche");
            var e2 = Strasse(out var haus2, out var weg2);
            e2.FelsStreuen(Streu(false), Strich, startwert: 7);
            Pruefe.Wahr(e2.Felsen.Count(f => Beruehrt(f, haus2, weg2)) > 0, "Gegenprobe: ohne Aussparen stehen Stücke im Haus oder auf dem Weg");
            Pruefe.Wahr(e2.Felsen.Count > e.Felsen.Count, "ohne Aussparen mehr Stücke");
        }),

        ("Fels P3b: Das Streuen einer Gruppe spart ebenso aus; Stempel und Radierer fragen die Abfrage nicht", () =>
        {
            var e = Strasse(out var haus, out var weg);
            e.FelsStreuen(new Felspinsel(Gruppe: "kuppe", Radius: 14, Dichte: 0.5, Boden: false, Aussparen: true), Strich, startwert: 3);
            Pruefe.Wahr(e.Felsen.Count > 0, "Gruppen gestreut");
            Pruefe.Gleich(0, e.Felsen.Count(f => Beruehrt(f, haus, weg)), "keine Gruppe im Haus oder auf dem Weg");
            // Stempel mitten ins Haus: bleibt erlaubt (Bauen und Setzen auf Fels bleiben frei)
            var vorher = e.Felsen.Count;
            var gesetzt = e.FelsStempeln(new Felspinsel(Formen: new[] { "block" }, Boden: false, Aussparen: true), O(-30, 0), startwert: 1);
            Pruefe.Gleich(1, gesetzt.Count, "Stempel im Haus setzt");
            Pruefe.Gleich(vorher + 1, e.Felsen.Count, "ein Stück mehr");
            // Radierer im Haus nimmt weg
            e.FelsStreuen(new Felspinsel(Radius: 8, Radierer: true, Aussparen: true), new[] { O(-30, 0) });
            Pruefe.Gleich(vorher, e.Felsen.Count, "der Radierer nahm das Stück im Haus wieder weg");
        }),

        ("Fels P3b: Ein berührtes Stück verwirft die ganze Gruppe – es liegen nur vollständige Gruppen, keine berührt Haus oder Weg (mit Gegenprobe)", () =>
        {
            var e = Strasse(out var haus, out var weg);
            e.FelsStreuen(new Felspinsel(Gruppe: "kuppe", Radius: 14, Dichte: 0.5, Boden: false, Aussparen: true), Strich, startwert: 3);
            var gruppen = Gruppen(e.Felsen);
            Pruefe.Wahr(gruppen.Count > 0, $"Gruppen gestreut ({gruppen.Count})");
            Pruefe.Gleich(e.Felsen.Count, gruppen.Count * KuppenFormen.Length, "die Liste besteht nur aus vollständigen Gruppen");
            Pruefe.Gleich(0, gruppen.Count(g => g.Any(f => Beruehrt(f, haus, weg))), "keine Gruppe berührt Haus oder Weg, auch nicht mit einem Randstück");
            // Gegenprobe ohne Aussparen: dieselbe Lage erzeugt Gruppen, die nur mit einem Teil ihrer Stücke Haus oder Weg berühren – genau die
            // verwirft die Regel ganz; nähme sie nur die berührten Stücke weg, lägen oben unvollständige Gruppen
            var e2 = Strasse(out var haus2, out var weg2);
            e2.FelsStreuen(new Felspinsel(Gruppe: "kuppe", Radius: 14, Dichte: 0.5, Boden: false, Aussparen: false), Strich, startwert: 3);
            var gruppen2 = Gruppen(e2.Felsen);
            Pruefe.Gleich(e2.Felsen.Count, gruppen2.Count * KuppenFormen.Length, "Gegenprobe: auch ohne Aussparen ganze Gruppen");
            Pruefe.Wahr(gruppen2.Any(g => g.Any(f => Beruehrt(f, haus2, weg2)) && g.Any(f => !Beruehrt(f, haus2, weg2))),
                "Gegenprobe: ohne Aussparen gibt es Gruppen, die nur mit einem Teil ihrer Stücke berühren");
            Pruefe.Wahr(gruppen2.Count(g => g.Any(f => Beruehrt(f, haus2, weg2))) > 0, "Gegenprobe: ohne Aussparen berühren Gruppen Haus oder Weg");

            // Am Wegrand: die Mitten liegen 4,5 m neben dem Weg (die Kuppe selbst reicht keine 4 m weit), nur Randstücke können ihn berühren.
            // Prüfte die Regel nur die Mitte, lägen hier Gruppen mit einem Randstück auf dem Weg.
            List<List<EntwurfFels>> AmRand(bool aussparen, out EntwurfGebaeude h, out EntwurfWeg w)
            {
                var r = Strasse(out h, out w);
                var z = 40 - w.BreiteWirksam / 2 - 4.5;
                r.FelsStreuen(new Felspinsel(Gruppe: "kuppe", Radius: 1.5, Dichte: 0.9, Boden: false, Aussparen: aussparen),
                    Enumerable.Range(0, 16).Select(i => O(i * 4.0, z)).ToArray(), startwert: 11);
                var g = Gruppen(r.Felsen);
                Pruefe.Gleich(r.Felsen.Count, g.Count * KuppenFormen.Length, $"am Rand (Aussparen {aussparen}): nur vollständige Gruppen");
                return g;
            }
            var ohne = AmRand(false, out var hausO, out var wegO);
            Pruefe.Wahr(ohne.Any(g => !Beruehrt(g[0], hausO, wegO) && g.Skip(1).Any(f => Beruehrt(f, hausO, wegO))),
                "Gegenprobe am Rand: ohne Aussparen berühren Gruppen den Weg nur mit einem Randstück, die Mitte frei");
            var mit = AmRand(true, out var hausM, out var wegM);
            Pruefe.Wahr(mit.Count > 0, $"am Rand gestreut ({mit.Count} Gruppen)");
            Pruefe.Gleich(0, mit.Count(g => g.Any(f => Beruehrt(f, hausM, wegM))), "am Rand: keine Gruppe berührt den Weg, auch nicht mit einem Randstück");
        }),

        ("Fels P3b: Bauzonen nur mit AussparenBauzonen; ein Strich bleibt ein Schritt und Rückgängig gibt dieselbe Datei", () =>
        {
            var e = Strasse(out _, out _);
            e.BauzoneAnlegen(Lage.Kern, new[] { O(30, -30), O(70, -30), O(70, 20), O(30, 20) });
            var vorher = e.ZuJson();
            var strich = new[] { O(40, -10), O(60, 0), O(100, 5) }; // beginnt in der Bauzone und läuft aus ihr hinaus
            e.FelsStreuen(Streu(true), strich, startwert: 5);
            Pruefe.Wahr(e.Felsen.Any(f => Kartengeometrie.ImVieleck(O(f.X, f.Z), e.Bauzonen[0].Ecken)), "Bauzone gilt ohne das zweite Häkchen nicht als Hindernis");
            Pruefe.Wahr(e.Rueckgaengig(), "ein Schritt zurück");
            Pruefe.Gleich(vorher, e.ZuJson(), "Rückgängig gibt dieselbe Datei");
            e.FelsStreuen(Streu(true) with { AussparenBauzonen = true }, strich, startwert: 5);
            Pruefe.Wahr(e.Felsen.Count > 0, $"mit dem zweiten Häkchen wird außerhalb der Bauzone gestreut ({e.Felsen.Count})");
            Pruefe.Gleich(0, e.Felsen.Count(f => Kartengeometrie.ImVieleck(O(f.X, f.Z), e.Bauzonen[0].Ecken)), "mit dem zweiten Häkchen keine Mitte in der Bauzone");
            Pruefe.Wahr(!e.FelsStrichLaeuft, "kein Strich offen");
            // der feste Ablauf des Gleichheitstests: sein zweiter Strich (mit Bauzonen) setzt Stücke, keins mit der Mitte in der Bauzone
            var ablauf = AblaufEntwurf(out var ersterStrich);
            var zweiter = ablauf.Felsen.Skip(ersterStrich).Take(ablauf.Felsen.Count - ersterStrich - 1).ToList(); // ohne den Stempel am Ende
            Pruefe.Wahr(zweiter.Count > 0, $"Ablauf: der zweite Strich setzt Stücke ({zweiter.Count})");
            Pruefe.Gleich(0, zweiter.Count(f => Kartengeometrie.ImVieleck(O(f.X, f.Z), ablauf.Bauzonen[0].Ecken)), "Ablauf: keins mit der Mitte in der Bauzone");
        }),

        ("Fels P3b: Deterministisch und ohne Häkchen wie vorher – gleicher Strich gleiche Datei, Vorgabe ändert alte Striche nicht", () =>
        {
            string Lauf(bool an)
            {
                var e = Strasse(out _, out _);
                e.FelsStreuen(Streu(an), Strich, startwert: 7);
                return e.ZuJson();
            }
            Pruefe.Gleich(Lauf(true), Lauf(true), "zweimal dasselbe");
            Pruefe.Wahr(Lauf(true) != Lauf(false), "Gegenprobe: mit und ohne unterscheiden sich");
            // ohne Haus und Weg ändert das Häkchen nichts
            string Leer(bool an)
            {
                var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);
                e.FelsStreuen(Streu(an), Strich, startwert: 7);
                return e.ZuJson();
            }
            Pruefe.Gleich(Leer(true), Leer(false), "auf freiem Gelände dieselbe Datei");
        }),
    };

    /// <summary>Ein fester Ablauf für den Gleichheitstest (Zeile „felsaus …“): Haus, Weg und Bauzone, Streuen mit Aussparen (mit und ohne Bauzonen), Stempel im Haus.</summary>
    internal static string Ablauf() => AblaufEntwurf(out _).ZuJson();

    /// <summary>Der Entwurf zu <see cref="Ablauf"/>; <paramref name="ersterStrich"/>: so viele Stücke liegen nach dem ersten Strich.</summary>
    static Kartenentwurf AblaufEntwurf(out int ersterStrich)
    {
        var e = Strasse(out _, out _);
        e.BauzoneAnlegen(Lage.Kern, new[] { O(30, -30), O(70, -30), O(70, 20), O(30, 20) });
        e.FelsStreuen(Streu(true), Strich, startwert: 7);
        ersterStrich = e.Felsen.Count;
        // der zweite Strich beginnt in der Bauzone und läuft aus ihr hinaus (rechts von x = 70)
        e.FelsStreuen(Streu(true) with { AussparenBauzonen = true, Formen = new[] { "schuttbrocken" }, Abstand = 1 }, new[] { O(40, -10), O(100, 5) }, startwert: 9);
        e.FelsStempeln(new Felspinsel(Formen: new[] { "block" }, Boden: false, Aussparen: true), O(-30, 0), startwert: 1);
        return e;
    }

    /// <summary>Die Formen einer Felskuppe in der Reihenfolge, in der das Streuen ihre Stücke anhängt (Mitte zuerst).</summary>
    static readonly string[] KuppenFormen = { "kuppe", "block", "block", "platte", "schuttbrocken" };

    /// <summary>Teilt die Liste in Kuppen-Gruppen; eine Lücke oder eine fremde Form bricht ab (dann stimmt die Anzahl in der Probe nicht).</summary>
    static List<List<EntwurfFels>> Gruppen(IReadOnlyList<EntwurfFels> felsen)
    {
        var aus = new List<List<EntwurfFels>>();
        for (var i = 0; i + KuppenFormen.Length <= felsen.Count; i += KuppenFormen.Length)
        {
            var g = felsen.Skip(i).Take(KuppenFormen.Length).ToList();
            if (!g.Select(f => f.Form).SequenceEqual(KuppenFormen)) break;
            aus.Add(g);
        }
        return aus;
    }
}
