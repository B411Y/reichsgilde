using Reichsgilde;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Die Marktrunde der Streife (<see cref="Marktrunde"/>, Spielordner, reine Rechnung): In Köln war sie vom Wachhaus am Heumarkt nur 5 m lang
/// (Befund 08.11.2026). Geprüft auf der echten Karte; als Wegenetz dient das Netz aller Wege des Kerns mit den Linien über die Marktplätze
/// (<see cref="Karrennetz.Von"/>), als Wegpunkte die Punkte der Wege – wie in der Darstellung die Knoten des Gassennetzes auf den Wegen.
/// </summary>
static class StreifeMarktrundeTests
{
    /// <summary>Sucht eine Datei des Spiels (game/…), egal von wo aus die Tests gestartet werden.</summary>
    static string Spielordner(params string[] teile)
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null)
        {
            var pfad = Path.Combine(new[] { ordner.FullName, "game" }.Concat(teile).ToArray());
            if (File.Exists(pfad)) return pfad;
            ordner = ordner.Parent;
        }
        throw new FileNotFoundException("game/" + string.Join("/", teile) + " wurde nicht gefunden.");
    }

    static readonly Lazy<Stadtkarte> Koeln = new(() => Stadtkarte.AusDatei(Spielordner("karten", "koeln.json")));
    static readonly Lazy<Karrennetz> KoelnNetz = new(() => Karrennetz.Von(Koeln.Value, marktlinien: true, alleWege: true));

    /// <summary>Die Marktplätze der Karte: Mitte und halbe Kantenlänge des Pflasters.</summary>
    static List<((float X, float Z) Mitte, float Halb)> Maerkte(Stadtkarte karte) =>
        karte.Marktplaetze.Select(m =>
        {
            var ecken = Kartengrenzen.GebaeudeEcken(m.Baustein, m.Punkt[0], m.Punkt[1], m.Drehung, m.Massstab);
            var halb = ecken.Max(e => Math.Max(Math.Abs(e.X - m.Punkt[0]), Math.Abs(e.Z - m.Punkt[1])));
            return (((float)m.Punkt[0], (float)m.Punkt[1]), (float)halb);
        }).ToList();

    /// <summary>Liegt der Punkt innerhalb einer Mauer (die Linie als Vieleck, wie <c>InDerStadt</c> der Darstellung)?</summary>
    static bool InDerStadt(Stadtkarte karte, (float X, float Z) p) =>
        karte.Mauern.Where(m => m.Linie.Count >= 3).Any(m => Kartengeometrie.ImVieleck(new Kartenort(p.X, p.Z), m.Linie.Select(Kartenort.Aus).ToList()));

    /// <summary>Die Punkte der Wege auf derselben Seite der Mauer wie der Markt (wie in der Darstellung die Knoten des Gassennetzes auf den Wegen).</summary>
    static List<(float X, float Z)> Wegpunkte(Stadtkarte karte, (float X, float Z) mitte) =>
        karte.Wege.SelectMany(w => w.Verlauf).Where(p => p is { Length: 2 }).Select(p => ((float)p[0], (float)p[1])).Distinct()
            .Where(p => InDerStadt(karte, p) == InDerStadt(karte, mitte)).ToList();

    static IReadOnlyList<(float X, float Z)> Weg(Karrennetz netz, (float X, float Z) a, (float X, float Z) b) =>
        netz.FahrtMitVerlauf(new Kartenort(a.X, a.Z), new Kartenort(b.X, b.Z)).Punkte.Select(p => ((float)p.X, (float)p.Z)).ToList();

    /// <summary>Der Markt, dessen Pflaster dem Punkt am nächsten liegt (wie <c>NaechsterMarkt</c> der Darstellung).</summary>
    static ((float X, float Z) Mitte, float Halb) NaechsterMarkt(List<((float X, float Z) Mitte, float Halb)> maerkte, (float X, float Z) p) =>
        maerkte.OrderBy(m => Math.Max(0, Math.Max(Math.Abs(p.X - m.Mitte.X), Math.Abs(p.Z - m.Mitte.Z)) - m.Halb)).First();

    static (List<(float X, float Z)> Runde, float Laenge) RundeVon((float X, float Z) tuer)
    {
        var markt = NaechsterMarkt(Maerkte(Koeln.Value), tuer);
        var runde = Marktrunde.Planen(tuer, markt.Mitte, markt.Halb, Wegpunkte(Koeln.Value, markt.Mitte), (a, b) => Weg(KoelnNetz.Value, a, b));
        Pruefe.Wahr(runde is not null, $"Marktrunde ab {tuer} geplant");
        return runde!.Value;
    }

    /// <summary>Jeder Punkt der Runde (außer der Tür an Anfang und Ende) liegt auf einem Weg der Karte oder auf dem Pflaster eines Marktes.</summary>
    static void AufWegen(List<(float X, float Z)> runde)
    {
        var maerkte = Maerkte(Koeln.Value);
        for (var i = 1; i < runde.Count - 1; i++)
        {
            var p = runde[i];
            var aufPflaster = maerkte.Any(m => Math.Abs(p.X - m.Mitte.X) <= m.Halb + 1 && Math.Abs(p.Z - m.Mitte.Z) <= m.Halb + 1);
            var abstand = KoelnNetz.Value.AbstandZumWeg(new Kartenort(p.X, p.Z)) ?? double.MaxValue;
            Pruefe.Wahr(aufPflaster || abstand < 0.5, $"Punkt {p.X:0.0}|{p.Z:0.0} der Runde liegt {abstand:0.0} m neben dem Weg");
        }
        // die Stücke dazwischen: alle 2 m auf einem Weg oder dem Pflaster (keine Abkürzung quer durch Häuser)
        for (var i = 2; i < runde.Count - 1; i++)
        {
            var (a, b) = (runde[i - 1], runde[i]);
            var schritte = (int)(Marktrunde.Abstand(a, b) / 2);
            for (var s = 1; s < schritte; s++)
            {
                var t = (float)s / schritte;
                var q = (a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);
                var aufPflaster = maerkte.Any(m => Math.Abs(q.Item1 - m.Mitte.X) <= m.Halb + 1 && Math.Abs(q.Item2 - m.Mitte.Z) <= m.Halb + 1);
                var abstand = KoelnNetz.Value.AbstandZumWeg(new Kartenort(q.Item1, q.Item2)) ?? double.MaxValue;
                Pruefe.Wahr(aufPflaster || abstand < 0.5, $"Stück bei {q.Item1:0.0}|{q.Item2:0.0} liegt {abstand:0.0} m neben dem Weg");
            }
        }
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Streife: Köln, Marktrunde ab dem Wachhaus am Heumarkt ist 150–400 m lang, auf Wegen, umläuft den Platz und endet an der Tür", () =>
        {
            // das Wachhaus der Gardisten-Partie steht auf dem Werkstattplatz an der Rheingasse vor dem Heumarkt (Tür zur Rheingasse)
            var platz = Koeln.Value.Gebaeude.Single(g => g.Kennung == "S1_Werkstatt");
            var tuer = ((float)platz.Punkt[0] + 6f, (float)platz.Punkt[1]);
            var (runde, laenge) = RundeVon(tuer);
            Pruefe.Wahr(laenge >= Marktrunde.MindestLaenge && laenge <= Marktrunde.HoechstLaenge, $"Länge {laenge:0} m");
            Pruefe.Gleich(tuer, runde[0]);
            Pruefe.Gleich(tuer, runde[^1]);
            AufWegen(runde);
            // umläuft den Heumarkt: Punkte auf mindestens drei Seiten (im Osten fließt der Rhein)
            var heumarkt = Maerkte(Koeln.Value).Single(m => m.Mitte == (37f, 110f));
            var seiten = runde.Where(p => Marktrunde.Abstand(p, heumarkt.Mitte) > heumarkt.Halb + 10)
                .Select(p => (p.X - 37f, p.Z - 110f)).Select(d => Math.Abs(d.Item2) >= Math.Abs(d.Item1) ? (d.Item2 < 0 ? 0 : 2) : (d.Item1 > 0 ? 1 : 3)).Distinct().Count();
            Pruefe.Wahr(seiten >= 3, $"Runde auf {seiten} Seiten des Heumarkts");
            // bleibt in der Stadt (die Stadtmauer liegt 40 m südlich des Heumarkts)
            Pruefe.Wahr(runde.All(p => InDerStadt(Koeln.Value, p)), "Runde innerhalb der Mauer");
        }),

        ("Streife: Köln, von jedem Werkstattplatz der Stadt ist die Marktrunde mindestens 150 m lang und liegt auf Wegen", () =>
        {
            var maerkte = Maerkte(Koeln.Value);
            var plaetze = Koeln.Value.GebaeudeDerArt(Bausteinart.Werkstattplatz).Where(g => g.Lage != Lage.Umland).ToList();
            Pruefe.Wahr(plaetze.Count >= 5, $"{plaetze.Count} Werkstattplätze");
            foreach (var platz in plaetze)
            {
                var tuer = ((float)platz.Punkt[0], (float)platz.Punkt[1]);
                var (runde, laenge) = RundeVon(tuer);
                var markt = NaechsterMarkt(maerkte, tuer);
                // ein Wachhaus weit vom Markt: hin und zurück ist schon länger als die Höchstlänge – dann mindestens das
                var hinUndZurueck = 2 * Marktrunde.Abstand(tuer, markt.Mitte);
                Pruefe.Wahr(laenge >= Marktrunde.MindestLaenge, $"{platz.Kennung}: {laenge:0} m");
                Pruefe.Wahr(laenge <= Math.Max(Marktrunde.HoechstLaenge, 2.5f * hinUndZurueck), $"{platz.Kennung}: {laenge:0} m, Luftlinie hin und zurück {hinUndZurueck:0} m");
                AufWegen(runde);
            }
        }),

        ("Streife: Marktrunde ist fest (dieselbe Karte, dieselbe Runde) und nimmt je Seite des Platzes den äußersten Punkt im Ring", () =>
        {
            var punkte = new List<(float X, float Z)> { (0, -30), (0, -20), (25, 0), (0, 40), (-20, 0), (-20, 5), (100, 100) };
            var halte = Marktrunde.Haltepunkte((30, 0), (0, 0), 10, 45, punkte);
            Pruefe.Gleich(4, halte.Count);
            Pruefe.Gleich((25f, 0f), halte[0]); // die Seite nahe der Tür zuerst
            Pruefe.Gleich((0f, 40f), halte[1]);
            Pruefe.Gleich((-20f, 5f), halte[2]); // (-20, 5) liegt weiter außen als (-20, 0)
            Pruefe.Gleich((0f, -30f), halte[3]);
            var gedreht = Enumerable.Reverse(punkte).ToList();
            Pruefe.Wahr(halte.SequenceEqual(Marktrunde.Haltepunkte((30, 0), (0, 0), 10, 45, gedreht)), "Reihenfolge der Punkte ändert nichts");
            // gerade Wege (Luftlinie) als Netz: die Runde beginnt und endet an der Tür
            var r = Marktrunde.Planen((30, 0), (0, 0), 10, punkte, (a, b) => new[] { a, b })!.Value;
            Pruefe.Gleich((30f, 0f), r.Runde[0]);
            Pruefe.Gleich((30f, 0f), r.Runde[^1]);
        }),
    };
}
