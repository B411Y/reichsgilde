using System.Text.RegularExpressions;
using Reichsgilde;

/// <summary>Modulares Wohnhaussystem: Planung der Häuserzeilen – Nachbarregel, Eckhaus, Keilstück, beide Straßenseiten, Dateien der Sätze.</summary>
static class ZeilenplanTests
{
    const string S = "mensch_s1";

    static string Spielordner() => Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft()))!;

    static Zeilenplan.Haus H(string modul) => new(S, modul);

    static List<Zeilenplan.Platz> Reihe(params string[] module)
    {
        var aus = new List<Zeilenplan.Platz>();
        var x = 0f;
        foreach (var m in module) { aus.Add(new Zeilenplan.Platz(H(m), x, aus.Count)); x += Zeilenplan.Breite(m); }
        return aus;
    }

    /// <summary>Die Seitenstücke einer Parzelle als Text „links|rechts“ (mehrere mit +).</summary>
    static string Seiten(List<Zeilenplan.Stueck> stuecke, int parzelle)
    {
        string Seite(bool spiegel) => string.Join("+", stuecke.Where(s => s.Parzelle == parzelle && Zeilenplan.Breite(s.Modul) == 0 && s.Gespiegelt == spiegel).Select(s => s.Modul));
        return Seite(false) + "|" + Seite(true);
    }

    /// <summary>Grundriss eines Körpers in der Ebene: die vier Ecken (vorn links, vorn rechts, hinten rechts, hinten links von der Gasse aus).</summary>
    static (float X, float Z)[] Ecken(Zeilenplan.Lage l) => new[]
    {
        (l.Ox, l.Oz), (l.Ox + l.Ex * l.Breite, l.Oz + l.Ez * l.Breite),
        (l.Ox + l.Ex * l.Breite + l.Tx * Zeilenplan.Tiefe, l.Oz + l.Ez * l.Breite + l.Tz * Zeilenplan.Tiefe), (l.Ox + l.Tx * Zeilenplan.Tiefe, l.Oz + l.Tz * Zeilenplan.Tiefe),
    };

    static (float X0, float X1, float Z0, float Z1) Rahmen(Zeilenplan.Lage l)
    {
        var e = Ecken(l);
        return (e.Min(p => p.X), e.Max(p => p.X), e.Min(p => p.Z), e.Max(p => p.Z));
    }

    static List<Zeilenplan.Lage> Koerper(List<Zeilenplan.Lage> lagen) => lagen.Where(l => Zeilenplan.Breite(l.Modul) > 0 && l.Parzelle >= 0).ToList();

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Zeilenplan: Giebelhäuser bekommen am freien Ende die offene Seite, am Nachbarn die blinde Seite und die halbe Traufgasse", () =>
        {
            var st = Zeilenplan.Stuecke(Reihe("k_g6", "k_g4", "k_g8"));
            Pruefe.Gleich("seite_offen|seite_blind+gasse", Seiten(st, 0));
            Pruefe.Gleich("seite_blind+gasse|seite_blind+gasse", Seiten(st, 1));
            Pruefe.Gleich("seite_blind+gasse|seite_offen", Seiten(st, 2));
            Pruefe.Gleich(3, st.Count(s => Zeilenplan.Breite(s.Modul) > 0));
        }),
        ("Zeilenplan: gleiche Traufhäuser stoßen ohne Kappe aneinander, das freie Ende im Fachwerk bekommt den offenen Giebel", () =>
        {
            var st = Zeilenplan.Stuecke(Reihe("k_t6", "k_t8", "k_t6"));
            Pruefe.Gleich("kappe_offen|", Seiten(st, 0));
            Pruefe.Gleich("|", Seiten(st, 1));
            Pruefe.Gleich("|kappe_offen", Seiten(st, 2));
        }),
        ("Zeilenplan: ein Traufhaus neben einem Giebelhaus oder Eckhaus bekommt die Brandwand, das Giebelhaus die blinde Seite", () =>
        {
            var st = Zeilenplan.Stuecke(Reihe("k_g6", "k_t6", "eck"));
            Pruefe.Gleich("seite_offen|seite_blind+gasse", Seiten(st, 0));
            Pruefe.Gleich("kappe_brand|kappe_brand", Seiten(st, 1));
            Pruefe.Gleich("|", Seiten(st, 2));   // das Eckhaus hat seine Wände selbst
        }),
        ("Zeilenplan: eine Lücke trennt Nachbarn (beide Seiten offen), ein Nachbar jenseits des Anfangs oder Endes zählt", () =>
        {
            var plaetze = new List<Zeilenplan.Platz> { new(H("k_g6"), 0, 0), new(H("k_g6"), 10, 1) };
            var st = Zeilenplan.Stuecke(plaetze);
            Pruefe.Gleich("seite_offen|seite_offen", Seiten(st, 0));
            Pruefe.Gleich("seite_offen|seite_offen", Seiten(st, 1));
            st = Zeilenplan.Stuecke(Reihe("k_g6"), links: H("eck"), rechts: H("eck"));
            Pruefe.Gleich("seite_blind+gasse|seite_blind+gasse", Seiten(st, 0));
        }),
        ("Zeilenplan: Giebel überall – jede Zeilenart bekommt Giebelhäuser (4, 6, 8 m), Traufhäuser wählt keine Art mehr", () =>
        {
            foreach (var art in new[] { Zeilenplan.Zeilenart.Buerger, Zeilenplan.Zeilenart.Kulisse })
            {
                foreach (var b in new[] { 4, 6, 8 })
                {
                    var m = Zeilenplan.Koerper(art, b);
                    Pruefe.Gleich("k_g" + b, m);
                    Pruefe.Gleich((float)b, Zeilenplan.Breite(m));
                    Pruefe.Gleich(Zeilenplan.Stellung.Giebel, Zeilenplan.StellungVon(m));
                }
                Pruefe.Gleich("", Zeilenplan.Koerper(art, 3));   // Schmalhaus mindestens 4 m, ein 3-m-Modul gibt es nicht
                Pruefe.Gleich("", Zeilenplan.Koerper(art, 5));
                Pruefe.Gleich("", Zeilenplan.Koerper(art, 10));
            }
            // eine Kulissenzeile aus den gewählten Modulen hat nur Giebelhäuser, keine Kappen
            var haeuser = new[] { 6, 8, 6 }.Select(b => H(Zeilenplan.Koerper(Zeilenplan.Zeilenart.Kulisse, b))).ToArray();
            var lagen = Zeilenplan.Planen(new[] { (0f, 0f), (20f, 0f) }, true, haeuser, out var gestellt);
            Pruefe.Gleich(3, gestellt);
            Pruefe.Gleich(0, lagen.Count(l => l.Modul.StartsWith("kappe_") || l.Modul.StartsWith("k_t")));
            Pruefe.Gleich(2, lagen.Count(l => l.Modul == "seite_offen"));
            Pruefe.Gleich(4, lagen.Count(l => l.Modul == "gasse"));
            // ausdrücklich genannte Traufhäuser gehen weiter (die Module liegen in den GLB)
            Pruefe.Gleich(Zeilenplan.Stellung.Trauf, Zeilenplan.StellungVon("k_t6"));
            Pruefe.Gleich(6f, Zeilenplan.Breite("k_t6"));
        }),
        ("Zeilenplan: Fenster-Varianten – feste Wahl aus Kennung und Parzelle, nie zweimal dieselbe nebeneinander, ohne Varianten das Modul ohne Zusatz", () =>
        {
            Pruefe.Gleich("k_g6_a", Zeilenplan.VariantenName("k_g6", 0));
            Pruefe.Gleich("k_g6_c", Zeilenplan.VariantenName("k_g6", 2));
            Pruefe.Gleich(2166136261u, Zeilenplan.Streuwert(""));            // FNV-1a: Startwert
            Pruefe.Gleich(0xE40C292Cu, Zeilenplan.Streuwert("a"));           // bekannter Prüfwert
            foreach (var n in new[] { 2, 3, 5 })
                foreach (var kennung in new[] { "Probe", "koeln_severinstrasse_west", "z17", "" })
                {
                    var haeuser = Enumerable.Range(0, 60).Select(i => (S, "k_g6", i)).ToList();
                    var wahl = Zeilenplan.VariantenWahl(kennung, haeuser, (_, _) => n);
                    Pruefe.Gleich(60, wahl.Length);
                    for (var i = 0; i < wahl.Length; i++)
                    {
                        Pruefe.Wahr(wahl[i] >= 0 && wahl[i] < n, $"Index {wahl[i]} außerhalb 0..{n - 1}");
                        if (i > 0) Pruefe.Wahr(wahl[i] != wahl[i - 1], $"zweimal Variante {wahl[i]} nebeneinander (n={n}, {kennung}, Haus {i})");
                    }
                    Pruefe.Gleich(n, wahl.Distinct().Count(), $"alle {n} Varianten kommen vor ({kennung})");
                    // fest: zweiter Aufruf gleich
                    Pruefe.Gleich(string.Join(",", wahl), string.Join(",", Zeilenplan.VariantenWahl(kennung, haeuser, (_, _) => n)));
                }
            // verschiedene Kennungen geben verschiedene Folgen
            var folge = (string k) => string.Join(",", Zeilenplan.VariantenWahl(k, Enumerable.Range(0, 20).Select(i => (S, "k_g6", i)).ToList(), (_, _) => 3));
            Pruefe.Wahr(folge("Zeile A") != folge("Zeile B"), "Zeilen unterscheiden sich");
            // keine Varianten (0) oder nur eine: immer Index 0
            Pruefe.Wahr(Zeilenplan.VariantenWahl("x", Enumerable.Range(0, 10).Select(i => (S, "k_g6", i)).ToList(), (_, _) => 0).All(v => v == 0), "ohne Varianten Index 0");
            Pruefe.Wahr(Zeilenplan.VariantenWahl("x", Enumerable.Range(0, 10).Select(i => (S, "k_g6", i)).ToList(), (_, _) => 1).All(v => v == 0), "eine Variante: Index 0");
            // verschiedene Module oder Sätze nebeneinander dürfen denselben Index haben; gleiche nie – auch gemischt
            var gemischt = new List<(string, string, int)>();
            for (var i = 0; i < 80; i++) gemischt.Add((i % 7 == 3 ? "zwerg_s1" : S, i % 5 < 3 ? "k_g6" : "k_g4", i));
            var w = Zeilenplan.VariantenWahl("gemischt", gemischt, (_, m) => m == "k_g6" ? 3 : 2);
            for (var i = 1; i < gemischt.Count; i++)
                if (gemischt[i].Item1 == gemischt[i - 1].Item1 && gemischt[i].Item2 == gemischt[i - 1].Item2)
                    Pruefe.Wahr(w[i] != w[i - 1], $"gleiche Nachbarn mit derselben Variante bei {i}");
            // Seitenstücke: fest und im Bereich
            for (var p = 0; p < 30; p++)
            {
                var v = Zeilenplan.Variante("Probe", p, "seite_offen|l", 3);
                Pruefe.Wahr(v >= 0 && v < 3, "Variante im Bereich");
                Pruefe.Gleich(v, Zeilenplan.Variante("Probe", p, "seite_offen|l", 3));
                Pruefe.Gleich(0, Zeilenplan.Variante("Probe", p, "seite_offen|l", 0));
            }
        }),
        ("Zeilenplan: gerade Zeile links der Laufrichtung – Häuser lückenlos im 2-m-Raster, Tiefe nach Norden", () =>
        {
            var lagen = Zeilenplan.Planen(new[] { (0f, 0f), (30f, 0f) }, true, new[] { H("k_g6"), H("k_t8"), H("k_g4") }, out var gestellt);
            Pruefe.Gleich(3, gestellt);
            var k = Koerper(lagen);
            Pruefe.Gleich(3, k.Count);
            Pruefe.Ungefaehr(0, k[0].Ox, 1e-4); Pruefe.Ungefaehr(6, k[1].Ox, 1e-4); Pruefe.Ungefaehr(14, k[2].Ox, 1e-4);
            foreach (var l in k)
            {
                Pruefe.Ungefaehr(1, l.Ex, 1e-5); Pruefe.Ungefaehr(-1, l.Tz, 1e-5);   // Laufrichtung Osten, Häuser im Norden (z kleiner)
                Pruefe.Ungefaehr(0, l.Ox % 2, 1e-4);
            }
        }),
        ("Zeilenplan: rechts der Laufrichtung steht dieselbe Zeile gespiegelt – Reihenfolge entlang der Strecke bleibt, die Seitenstücke zeigen nach außen", () =>
        {
            var haeuser = new[] { H("k_g6"), H("k_g4") };
            var lagen = Zeilenplan.Planen(new[] { (0f, 0f), (20f, 0f) }, false, haeuser);
            var k = Koerper(lagen);
            // Parzelle 0 liegt von x 0 bis 6, Parzelle 1 von 6 bis 10; die Häuser stehen im Süden (z größer)
            var r0 = Rahmen(k.Single(l => l.Parzelle == 0)); var r1 = Rahmen(k.Single(l => l.Parzelle == 1));
            Pruefe.Ungefaehr(0, r0.X0, 1e-4); Pruefe.Ungefaehr(6, r0.X1, 1e-4); Pruefe.Ungefaehr(8, r0.Z1, 1e-4); Pruefe.Ungefaehr(0, r0.Z0, 1e-4);
            Pruefe.Ungefaehr(6, r1.X0, 1e-4); Pruefe.Ungefaehr(10, r1.X1, 1e-4);
            // Von der Gasse aus (Blick nach Süden) ist Westen rechts: die offene Seite der Parzelle 0 (freies Ende bei x = 0) ist ihre rechte, also gespiegelt
            var offen0 = lagen.Single(l => l.Parzelle == 0 && l.Modul == "seite_offen");
            Pruefe.Wahr(offen0.Gespiegelt, "offene Seite der Parzelle 0 gespiegelt");
            Pruefe.Wahr(!lagen.Single(l => l.Parzelle == 0 && l.Modul == "seite_blind").Gespiegelt, "blinde Seite der Parzelle 0 zum Nachbarn");
            Pruefe.Wahr(!lagen.Single(l => l.Parzelle == 1 && l.Modul == "seite_offen").Gespiegelt, "offene Seite der Parzelle 1 am Ende bei x = 10");
            // Rahmen rechtshändig wie links: Modul-x mal Tiefe zeigt nach oben (kein Spiegeln durch die Seite)
            foreach (var l in k) Pruefe.Ungefaehr(1, -(l.Ex * l.Tz - l.Ez * l.Tx), 1e-5);
            var linksLage = Koerper(Zeilenplan.Planen(new[] { (0f, 0f), (20f, 0f) }, true, haeuser))[0];
            Pruefe.Ungefaehr(1, -(linksLage.Ex * linksLage.Tz - linksLage.Ez * linksLage.Tx), 1e-5);
        }),
        ("Zeilenplan: am rechten Knick steht das Eckhaus im Winkel, die nächste Strecke beginnt um die Haustiefe versetzt, nichts überlappt", () =>
        {
            foreach (var links in new[] { true, false })
            {
                // links: nach Osten, dann nach Norden; rechts: nach Osten, dann nach Süden
                var linie = new[] { (0f, 0f), (20f, 0f), (20f, links ? -20f : 20f) };
                var lagen = Zeilenplan.Planen(linie, links, new[] { H("k_g6"), H("k_t6"), H("eck"), H("k_g6"), H("k_t6") }, out var gestellt);
                Pruefe.Gleich(5, gestellt);
                var k = Koerper(lagen);
                Pruefe.Gleich(5, k.Count);
                var eck = Rahmen(k.Single(l => l.Modul == "eck"));
                Pruefe.Ungefaehr(12, eck.X0, 1e-3); Pruefe.Ungefaehr(20, eck.X1, 1e-3);
                Pruefe.Ungefaehr(links ? -8 : 0, eck.Z0, 1e-3); Pruefe.Ungefaehr(links ? 0 : 8, eck.Z1, 1e-3);
                Pruefe.Gleich(!links, k.Single(l => l.Modul == "eck").Gespiegelt, "Eckhaus rechts der Laufrichtung gespiegelt (zweite Schauseite am Knick)");
                // erstes Haus der zweiten Strecke: 8 m hinter dem Knick, stößt an das Eckhaus (blinde Seite)
                var drei = Rahmen(k.Single(l => l.Parzelle == 3));
                Pruefe.Ungefaehr(8, Math.Min(Math.Abs(drei.Z0), Math.Abs(drei.Z1)), 1e-3);
                Pruefe.Gleich(2, lagen.Count(l => l.Parzelle == 3 && l.Modul == "seite_blind"));   // zum Eckhaus und zum Traufhaus
                Pruefe.Gleich(0, lagen.Count(l => l.Parzelle == 3 && l.Modul == "seite_offen"));
                Pruefe.Gleich(2, lagen.Count(l => l.Parzelle == 3 && l.Modul == "gasse"));
                // kein Grundriss überlappt einen anderen (Berühren erlaubt)
                var r = k.Select(Rahmen).ToList();
                for (var i = 0; i < r.Count; i++)
                    for (var j = i + 1; j < r.Count; j++)
                        Pruefe.Wahr(r[i].X1 <= r[j].X0 + 1e-3 || r[j].X1 <= r[i].X0 + 1e-3 || r[i].Z1 <= r[j].Z0 + 1e-3 || r[j].Z1 <= r[i].Z0 + 1e-3, $"Häuser {i} und {j} überlappen (links={links})");
                Pruefe.Gleich(0, lagen.Count(l => l.Modul is "mauer" or "tor" or "pfeiler"));
            }
        }),
        ("Zeilenplan: rechter Knick ohne Eckhaus und schräger Knick bekommen das Keilstück mit Tor, unter 60° bleibt der Keil leer", () =>
        {
            var haeuser = new[] { H("k_g6"), H("k_g6") };
            // rechter Knick ohne Eckhaus: 8 m bleiben je Schenkel frei
            var recht = Zeilenplan.Planen(new[] { (0f, 0f), (20f, 0f), (20f, -20f) }, true, haeuser);
            Pruefe.Gleich(3, recht.Count(l => l.Modul == "pfeiler"));
            Pruefe.Gleich(1, recht.Count(l => l.Modul == "tor"));
            Pruefe.Wahr(recht.Count(l => l.Modul == "mauer") >= 10, "Mauerstücke um die freie Ecke");
            // 45° Knick (Innenwinkel 135°): frei = 8 / tan(67,5°) = 3,31 m je Schenkel
            var d = 20f / MathF.Sqrt(2f);
            var schraeg = Zeilenplan.Planen(new[] { (0f, 0f), (20f, 0f), (20f + d, -d) }, true, new[] { H("k_g8"), H("k_g8"), H("k_g8"), H("k_g8") }, out var gestellt);
            Pruefe.Gleich(4, gestellt);   // je Schenkel 20 − 3,31 = 16,7 m: zwei Häuser zu 8 m
            var zweites = Rahmen(Koerper(schraeg).Single(l => l.Parzelle == 1));
            Pruefe.Ungefaehr(16, zweites.X1, 1e-3);
            var drittes = Koerper(schraeg).Single(l => l.Parzelle == 2);
            Pruefe.Ungefaehr(8 / Math.Tan(67.5 * Math.PI / 180), Math.Sqrt((drittes.Ox - 20) * (drittes.Ox - 20) + drittes.Oz * drittes.Oz), 1e-3);
            Pruefe.Gleich(3, schraeg.Count(l => l.Modul == "pfeiler"));
            Pruefe.Gleich(0, schraeg.Count(l => l.Modul == "tor"));   // 3,3 m je Schenkel: zu kurz für das Tor (3 m und zwei halbe Pfeiler), nur Mauer
            Pruefe.Wahr(schraeg.Count(l => l.Modul == "mauer") >= 6, "Mauerstücke im Keil");
            // 60° Knick (Innenwinkel 120°): 4,6 m je Schenkel, das Tor passt
            var flach = Zeilenplan.Planen(new[] { (0f, 0f), (20f, 0f), (30f, -17.3205f) }, true, haeuser);
            Pruefe.Gleich(1, flach.Count(l => l.Modul == "tor"));
            Pruefe.Gleich(3, flach.Count(l => l.Modul == "pfeiler"));
            // Mauerstücke liegen auf der Vorderlinie zwischen den Pfeilern, keins ragt über den Knick oder die Keilenden hinaus
            foreach (var m in schraeg.Where(l => l.Modul == "mauer"))
                Pruefe.Wahr(m.Ox >= 16.6f && m.Ox <= 20f + 3.4f, $"Mauerstück bei x = {m.Ox} außerhalb des Keils");
            // spitzer Knick (Innenwinkel 45°): kein Keilstück, die Häuser halten Abstand
            var spitz = Zeilenplan.Planen(new[] { (0f, 0f), (40f, 0f), (40f - 28.28f, -28.28f) }, true, haeuser);
            Pruefe.Gleich(0, spitz.Count(l => l.Modul is "mauer" or "tor" or "pfeiler"));
            // Knick von den Häusern weg: nichts wird frei gehalten, kein Keil
            var aussen = Zeilenplan.Planen(new[] { (0f, 0f), (12f, 0f), (12f, 12f) }, true, haeuser, out gestellt);
            Pruefe.Gleich(2, gestellt);
            Pruefe.Gleich(0, aussen.Count(l => l.Modul is "mauer" or "tor" or "pfeiler"));
        }),
        ("Zeilenplan: was nicht auf die Vorderlinie passt, wird nicht gestellt und gezählt; freie Parzellen lassen eine Lücke; leere Eingaben sind harmlos", () =>
        {
            var lagen = Zeilenplan.Planen(new[] { (0f, 0f), (13f, 0f) }, true, new[] { H("k_g6"), H("k_g6"), H("k_g6") }, out var gestellt);
            Pruefe.Gleich(2, gestellt);
            Pruefe.Gleich(2, Koerper(lagen).Count);
            lagen = Zeilenplan.Planen(new[] { (0f, 0f), (20f, 0f) }, true, new[] { H("k_g6"), new Zeilenplan.Haus(S, "", 4f), H("k_g6") }, out gestellt);
            Pruefe.Gleich(3, gestellt);
            Pruefe.Ungefaehr(10, Koerper(lagen)[1].Ox, 1e-4);
            Pruefe.Gleich(4, lagen.Count(l => l.Modul == "seite_offen"));   // die Lücke macht beide Nachbarseiten frei
            Pruefe.Gleich(0, Zeilenplan.Planen(new[] { (0f, 0f) }, true, new[] { H("k_g6") }, out gestellt).Count);
            Pruefe.Gleich(0, Zeilenplan.Planen(new[] { (0f, 0f), (20f, 0f), (20f, -20f) }, true, Array.Empty<Zeilenplan.Haus>(), out gestellt).Count(l => l.Parzelle >= 0));
            // ein Eckhaus ohne rechten Knick findet keinen Platz
            Zeilenplan.Planen(new[] { (0f, 0f), (20f, 0f) }, true, new[] { H("k_g6"), H("eck") }, out gestellt);
            Pruefe.Gleich(1, gestellt);
        }),
        ("Zeilenplan: höhere Sätze ragen über niedrigere – nur der höhere Nachbar bekommt die Kappe", () =>
        {
            var niedrig = new Zeilenplan.Haus(S, "k_t6");
            Pruefe.Wahr(!Zeilenplan.Ragt(niedrig, niedrig), "gleiche Traufhäuser ragen nicht");
            Pruefe.Wahr(Zeilenplan.Ragt(niedrig, null), "freies Ende ragt");
            Pruefe.Wahr(Zeilenplan.Ragt(niedrig, new Zeilenplan.Haus(S, "", 4f)), "freie Parzelle wie freies Ende");
            Pruefe.Wahr(Zeilenplan.Ragt(niedrig, H("k_g6")), "Giebeldreieck über der Traufe des Giebelhauses");
            foreach (var a in Zeilenplan.Saetze)
                foreach (var b in Zeilenplan.Saetze)
                {
                    if (a.Traufhoehe <= b.Traufhoehe + 0.05f && Zeilenplan.Firsthoehe(a) <= Zeilenplan.Firsthoehe(b) + 0.05f)
                        Pruefe.Wahr(!Zeilenplan.Ragt(new(a.Name, "k_t6"), new(b.Name, "k_t6")), $"{a.Name} ragt nicht über {b.Name}");
                    else
                        Pruefe.Wahr(Zeilenplan.Ragt(new(a.Name, "k_t6"), new(b.Name, "k_t6")), $"{a.Name} ragt über {b.Name}");
                }
        }),
        ("Zeilenplan: Sätze passen zu Bauskript und Dateien – Reihenfolge wie SAETZE in zeile_module.py, je Satz GLB und AO-Atlas, AO-Array mit einer Ebene je Satz", () =>
        {
            var spiel = Spielordner();
            var skript = File.ReadAllText(Path.Combine(Path.GetDirectoryName(spiel)!, "tools", "modelle", "neubau", "zeile_module.py"));
            var block = Regex.Match(skript, @"^SAETZE = \{(.*?)^\}", RegexOptions.Singleline | RegexOptions.Multiline);
            Pruefe.Wahr(block.Success, "SAETZE im Bauskript");
            var namen = Regex.Matches(block.Groups[1].Value, @"^\s*""([a-z0-9_]+)"":\s*dict\((.*?)\),\s*$", RegexOptions.Singleline | RegexOptions.Multiline);
            Pruefe.Gleich(string.Join(",", Zeilenplan.Saetze.Select(s => s.Name)), string.Join(",", namen.Select(m => m.Groups[1].Value)));
            for (var i = 0; i < Zeilenplan.Saetze.Length; i++)
            {
                var s = Zeilenplan.Saetze[i];
                Pruefe.Gleich(i, Zeilenplan.AoEbene(s.Name));
                Pruefe.Gleich($"{s.Volk}_s{s.Stufe}", s.Name);
                // Traufhöhe und Dachneigungen wie im Bauskript (die Nachbarregel rechnet damit)
                var werte = namen[i].Groups[2].Value;
                float Wert(string name) => float.Parse(Regex.Match(werte, name + @"=([0-9.]+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                var geschosse = Regex.Match(werte, @"geschosse=\(([0-9., ]+)\)").Groups[1].Value.Split(',').Sum(g => float.Parse(g.Trim(), System.Globalization.CultureInfo.InvariantCulture));
                Pruefe.Ungefaehr(geschosse, s.Traufhoehe, 1e-4);   // Traufhöhe = Summe der Geschosse
                Pruefe.Gleich(Regex.IsMatch(werte, @"og=""mauer"""), s.Stein, $"Steinbau = gemauertes Obergeschoss ({s.Name})");
                Pruefe.Ungefaehr(Math.Tan(Wert("neig_g") * Math.PI / 180), s.NeigungGiebel, 1e-4);
                Pruefe.Ungefaehr(Math.Tan(Wert("neig_t") * Math.PI / 180), s.NeigungTrauf, 1e-4);
                Pruefe.Gleich(Regex.IsMatch(werte, @"og=""fachwerk""") ? 0.4f : 0f, s.Krag, $"Auskragung nur bei Fachwerk ({s.Name})");
                Pruefe.Wahr(File.Exists(Path.Combine(spiel, "assets", "modelle", "gebaeude", "zeilen", $"zeile_{s.Name}.glb")), $"GLB des Satzes {s.Name}");
                Pruefe.Wahr(File.Exists(Path.Combine(spiel, "assets", "texturen", "neubau", $"zeile_{s.Name}_ao_atlas.png")), $"AO-Atlas des Satzes {s.Name}");
            }
            // AO-Array: 2048 breit, eine Ebene von 2048 je Satz, Import mit derselben Ebenenzahl
            var pfad = Path.Combine(spiel, "assets", "texturen", "neubau", "zeilen_ao_ebenen.png");
            var b = new byte[24];
            using (var f = File.OpenRead(pfad)) f.ReadExactly(b);
            int Zahl(int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
            Pruefe.Gleich(2048, Zahl(16));
            Pruefe.Gleich(2048 * Zeilenplan.Saetze.Length, Zahl(20));
            var import = File.ReadAllText(pfad + ".import");
            Pruefe.Wahr(import.Contains($"slices/vertical={Zeilenplan.Saetze.Length}\n") || import.Contains($"slices/vertical={Zeilenplan.Saetze.Length}\r"), "Ebenenzahl im Import des AO-Arrays");
            Pruefe.Wahr(import.Contains("slices/horizontal=1"), "Ebenen senkrecht");
            // Körperbreiten wie im Bauskript (BREITE)
            foreach (Match m in Regex.Matches(Regex.Match(skript, @"^BREITE = \{(.*?)\}", RegexOptions.Multiline).Groups[1].Value, @"""(\w+)"":\s*(\d+)"))
                Pruefe.Gleich(float.Parse(m.Groups[2].Value), Zeilenplan.Breite(m.Groups[1].Value), m.Groups[1].Value);
            Pruefe.Ungefaehr(Zeilenplan.Tiefe, float.Parse(Regex.Match(skript, @"^TIEFE = ([0-9.]+)", RegexOptions.Multiline).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-5);
            Pruefe.Ungefaehr(Zeilenplan.Ein, float.Parse(Regex.Match(skript, @"^EIN = ([0-9.]+)", RegexOptions.Multiline).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 1e-5);
        }),
    };
}
