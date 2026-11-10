using System.Diagnostics;
using System.Globalization;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Rechenzeit der Kartenprüfung in Häppchen (<see cref="Kartenentwurf.BefundeSchritt"/>) nach typischen Änderungen in der Kartenwerkstatt:
/// <c>… -- messen pruefung [Karte …] [--wiederholen=N] [--teile]</c> (Vorgabe: koeln greybox, 3 Wiederholungen). Je Karte und Änderung: wie viele
/// Häppchen, zusammen und das längste (Median über die Wiederholungen), dazu die drei längsten Häppchen mit ihrem Teil der Prüfung
/// (<see cref="Kartenentwurf.BefundeTeil"/>); mit <c>--teile</c> darunter je Teil Summe und längstes Häppchen. Gemessen wird nur die
/// Rechenzeit des Kerns (Stopwatch, ein Faden) – die Darstellung misst ihr Bild mit <c>--pinselmessung</c>.
/// </summary>
static class Pruefschrittmessung
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;

    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Die Änderungen: ein Name und was sie am Entwurf tun (Mitte des Spielfelds wie die Pinselmessung).</summary>
    static IEnumerable<(string Name, Action<Kartenentwurf, double, double> Tun)> Aenderungen() => new (string, Action<Kartenentwurf, double, double>)[]
    {
        ("nichts (Prüfung frisch geöffnet)", (_, _, _) => { }),
        ("Gelände: Heben (r 12, 30 Tupfer)", (e, cx, cz) =>
        {
            e.GelaendeStrichBeginnen(new Pinsel(Pinselart.Heben, Radius: 12, Staerke: 0.4, Kante: 0.5));
            foreach (var p in Strich(cx, cz)) e.GelaendeStrichWeiter(p);
            e.GelaendeStrichBeenden();
        }),
        ("Boden: Erde (r 8, 30 Tupfer)", (e, cx, cz) =>
        {
            e.BodenStrichBeginnen(new Bodenpinsel(Bodentextur.Erde, 8, Staerke: 1, Kante: 0.5, Deko: true, Dichte: 1));
            foreach (var p in Strich(cx, cz)) e.BodenStrichWeiter(p);
            e.BodenStrichBeenden();
        }),
        ("Weg anlegen (Gasse, 3 Punkte)", (e, cx, cz) => e.WegAnlegen(Wegklasse.Gasse, new List<Kartenort> { O(cx - 30, cz + 24), O(cx, cz + 30), O(cx + 30, cz + 24) })),
        ("Bauzone anlegen (30 × 30 m)", (e, cx, cz) =>
            e.BauzoneAnlegen(Lage.Vorstadt, new List<Kartenort> { O(cx - 15, cz - 60), O(cx + 15, cz - 60), O(cx + 15, cz - 30), O(cx - 15, cz - 30) })),
        ("Gebäude verschieben (erste Werkstatt 2 m)", (e, _, _) =>
        {
            if (e.Gebaeude.FirstOrDefault(g => g.Art == Bausteinart.Werkstattplatz) is { } g) e.Verschieben(g.Kennung, g.X + 2, g.Z);
        }),
    };

    /// <summary>Ein Strich aus 30 Tupfern über 80 m durch die Mitte (wie die Pinselmessung).</summary>
    static IEnumerable<Kartenort> Strich(double cx, double cz) => Enumerable.Range(0, 30).Select(i => O(cx - 40 + 80.0 * i / 29, cz + 6 * Math.Sin(i * 0.4)));

    static string Ms(double ms) => ms.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"));

    static double Median(IEnumerable<double> werte)
    {
        var l = werte.OrderBy(x => x).ToList();
        return l.Count == 0 ? 0 : l.Count % 2 == 1 ? l[l.Count / 2] : (l[l.Count / 2 - 1] + l[l.Count / 2]) / 2;
    }

    /// <summary>Der Teil der Prüfung, an dem das letzte Häppchen stand.</summary>
    static string Teil(Kartenentwurf e) => e.BefundeTeil ?? "";

    public static int Erstellen(string[] args)
    {
        var wiederholen = args.FirstOrDefault(a => a.StartsWith("--wiederholen=")) is { } w ? int.Parse(w["--wiederholen=".Length..]) : 3;
        var teile = args.Contains("--teile");
        var teileText = new List<(string Karte, string Aenderung, List<(double Ms, string Teil)> Haeppchen)>();
        var karten = args.Where(a => !a.StartsWith("--")).ToList();
        if (karten.Count == 0) karten = new List<string> { "koeln", "greybox" };
        var katalog = Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json"));
        Kartenentwurf Lade(string kennung) => Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), katalog);

        Console.WriteLine("| Karte | Änderung | Häppchen | zusammen (ms) | längstes (ms) | die drei längsten (ms, Teil) |");
        Console.WriteLine("|---|---|---:|---:|---:|---|");
        foreach (var kennung in karten)
        {
            // Aufwärmen: einmal alles in einem Zug und in Häppchen (der JIT übersetzt sonst im ersten Lauf)
            _ = Kartenpruefung.Pruefen(Lade(kennung));
            foreach (var (name, tun) in Aenderungen())
            {
                var laeufe = new List<(int Anzahl, double Zusammen, List<(double Ms, string Teil)> Haeppchen)>();
                for (var lauf = 0; lauf < wiederholen + 1; lauf++)
                {
                    var e = Lade(kennung);
                    if (name.StartsWith("nichts"))
                    {
                        // frisch geöffnet: nichts im Zwischenspeicher
                    }
                    else
                    {
                        _ = e.Befunde.Count; // wie in der Werkstatt: die Prüfung vor der Änderung ist gerechnet (Zwischenspeicher voll)
                        var feld = e.Kartengrenzen.Spielfeld;
                        tun(e, feld.Mitte.X, feld.Mitte.Z);
                    }
                    var haeppchen = new List<(double, string)>();
                    var uhr = new Stopwatch();
                    while (true)
                    {
                        uhr.Restart();
                        var fertig = e.BefundeSchritt();
                        uhr.Stop();
                        haeppchen.Add((uhr.Elapsed.TotalMilliseconds, Teil(e)));
                        if (fertig) break;
                    }
                    if (lauf > 0) laeufe.Add((haeppchen.Count, haeppchen.Sum(h => h.Item1), haeppchen)); // der erste Lauf wärmt nur auf
                }
                var spitze = laeufe.OrderBy(l => l.Haeppchen.Max(h => h.Ms)).ElementAt(laeufe.Count / 2); // der Lauf mit dem mittleren längsten Häppchen
                var drei = string.Join("; ", spitze.Haeppchen.Select((h, i) => (h.Ms, h.Teil, i)).OrderByDescending(h => h.Ms).Take(3)
                    .Select(h => $"{Ms(h.Ms)} (Nr. {h.i + 1}{(h.Teil == "" ? "" : ", " + h.Teil)})"));
                Console.WriteLine($"| {kennung} | {name} | {Median(laeufe.Select(l => (double)l.Anzahl))} | {Ms(Median(laeufe.Select(l => l.Zusammen)))} | "
                                  + $"{Ms(Median(laeufe.Select(l => l.Haeppchen.Max(h => h.Ms))))} | {drei} |");
                if (teile) teileText.Add((kennung, name, spitze.Haeppchen));
            }
        }
        // mit --teile: je Karte und Änderung jeder Teil der Prüfung mit Summe und längstem Häppchen (der Lauf mit dem mittleren längsten Häppchen)
        foreach (var (kennung, name, haeppchen) in teileText)
        {
            Console.WriteLine();
            Console.WriteLine($"{kennung}, {name}:");
            foreach (var g in haeppchen.GroupBy(h => h.Teil).OrderByDescending(g => g.Sum(h => h.Ms)))
                Console.WriteLine($"  {g.Key}: {g.Count()} Häppchen, zusammen {Ms(g.Sum(h => h.Ms))} ms, längstes {Ms(g.Max(h => h.Ms))} ms");
        }
        return 0;
    }
}
