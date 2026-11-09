using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Die Kartenprüfung in Häppchen (<see cref="Kartenentwurf.BefundeSchritt"/>): Die Werkstatt verteilt sie nach einem Pinselstrich auf mehrere Bilder,
/// damit keins über 33 ms kommt. Sie muss genau dieselben Befunde in derselben Reihenfolge liefern wie die Prüfung in einem Zug
/// (<see cref="Kartenpruefung.Pruefen"/>), auch auf den echten Karten, und von vorn anfangen, wenn sich der Entwurf zwischen zwei Häppchen ändert.
/// </summary>
static class BefundeSchrittweiseTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenentwurf Lade(string kennung) => Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value);

    static string Text(IEnumerable<Befund> befunde) =>
        string.Join("\n", befunde.Select(b => $"{b.Schwere} {b.Regel}: {b.Text} @ {b.Ort} [{string.Join(",", b.Gebaeude)}] ({b.Andere.Count})"));

    /// <summary>Rechnet in Häppchen bis zum Ende und sagt, wie viele es waren.</summary>
    static int Haeppchen(Kartenentwurf e)
    {
        var n = 0;
        while (!e.BefundeSchritt())
            if (++n > 100_000) throw new Exception("Die Prüfung endet nicht.");
        return n + 1;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Prüfung in Häppchen: auf allen Karten dieselben Befunde in derselben Reihenfolge wie in einem Zug", () =>
        {
            foreach (var kennung in new[] { "greybox", "probestadt", "messstadt", "koeln" })
            {
                var e = Lade(kennung);
                var ganz = Text(Kartenpruefung.Pruefen(e));
                Pruefe.Wahr(!e.BefundeBereit, $"{kennung}: vorher nichts gerechnet");
                var n = Haeppchen(e);
                Pruefe.Wahr(e.BefundeBereit, $"{kennung}: bereit");
                Pruefe.Wahr(n > 5, $"{kennung}: mehrere Häppchen ({n})");
                Pruefe.Gleich(ganz, Text(e.Befunde), $"{kennung}: Befunde");
            }
        }),

        ("Prüfung in Häppchen: nach einem Pinselstrich und mitten im Lauf geändert gilt der Stand von danach", () =>
        {
            var e = Lade("koeln");
            for (var i = 0; i < 4; i++) e.BefundeSchritt(); // angefangen
            var strich = new Pinsel(Pinselart.Heben, Radius: 12, Staerke: 0.4, Kante: 0.5);
            e.GelaendeStrichBeginnen(strich);
            foreach (var i in Enumerable.Range(0, 10)) e.GelaendeStrichWeiter(new Kartenort(-30 + 6 * i, 12));
            e.GelaendeStrichBeenden();
            Pruefe.Wahr(!e.BefundeBereit, "der halbe Lauf ist verworfen");
            for (var i = 0; i < 3; i++) e.BefundeSchritt();
            e.WegAnlegen(Wegklasse.Gasse, new List<Kartenort> { new(-20, 40), new(0, 44), new(20, 40) }); // noch einmal mitten im Lauf
            Haeppchen(e);
            Pruefe.Gleich(Text(Kartenpruefung.Pruefen(e)), Text(e.Befunde), "Befunde nach den Änderungen");
        }),

        ("Prüfung in Häppchen: wer die Befunde in einem Zug liest, bekommt dieselben und danach ist sie bereit", () =>
        {
            var e = Lade("greybox");
            for (var i = 0; i < 3; i++) e.BefundeSchritt();
            var ganz = Text(e.Befunde);
            Pruefe.Wahr(e.BefundeBereit && e.BefundeSchritt(), "bereit");
            Pruefe.Gleich(Text(Kartenpruefung.Pruefen(e)), ganz, "gleich");
        }),
    };
}
