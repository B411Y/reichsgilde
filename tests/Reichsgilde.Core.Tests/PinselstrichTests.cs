using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Werkstatt-Pinsel ohne Ruckeln: Die Darstellung schiebt während eines Strichs auf, was erst danach gebraucht wird (die volle Prüfung
/// <see cref="Kartenentwurf.Befunde"/>, Fehlerliste, Gras, Natur), und fragt dafür <see cref="Kartenentwurf.PinselstrichLaeuft"/>. Hier: dass die Abfrage
/// stimmt und dass der neue Weg (keine Prüfung zwischen den Tupfern) dieselbe Karte ergibt wie der alte (Prüfung nach jedem Tupfer) und wie der Strich in
/// einem Aufruf – Byte für Byte –, dass die Befunde danach dieselben sind und Rückgängig den ganzen Strich in einem Schritt zurücknimmt.
/// </summary>
static class PinselstrichTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Eine kleine Karte mit einem Weg (Bett im Gelände) und einer Bauzone, damit die Prüfung etwas zu tun hat.</summary>
    static Kartenentwurf Karte()
    {
        var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);
        e.WegAnlegen(Wegklasse.Gasse, new List<Kartenort> { O(-150, 20), O(0, 30), O(150, 20) });
        e.BauzoneAnlegen(Lage.Vorstadt, new List<Kartenort> { O(-40, -80), O(40, -80), O(40, -30), O(-40, -30) });
        e.Geschichte.Gespeichert();
        return e;
    }

    /// <summary>Ein Strich aus 30 Tupfern quer über die Karte (wie die Pinselmessung).</summary>
    static readonly IReadOnlyList<Kartenort> Strich = Enumerable.Range(0, 30).Select(i => O(-60 + 120.0 * i / 29, 10 + 8 * Math.Sin(i * 0.4))).ToList();

    static readonly Pinsel Heben = new(Pinselart.Heben, Radius: 12, Staerke: 0.4, Kante: 0.5);
    static readonly Bodenpinsel Erde = new(Bodentextur.Erde, 8, Staerke: 1, Kante: 0.5, Deko: true, Dichte: 1);
    static readonly Felspinsel Bloecke = new(Formen: new[] { "block" }, Radius: 10, Dichte: 0.3);

    /// <summary>Malt mit allen drei Pinseln Tupfer für Tupfer; mit <paramref name="pruefenJeTupfer"/> liest es nach jedem Tupfer die Befunde (der alte Weg).</summary>
    static Kartenentwurf TupferFuerTupfer(bool pruefenJeTupfer)
    {
        var e = Karte();
        void Lesen()
        {
            if (pruefenJeTupfer) _ = e.Befunde.Count;
        }
        e.GelaendeStrichBeginnen(Heben);
        foreach (var p in Strich) { e.GelaendeStrichWeiter(p); Lesen(); }
        Pruefe.Wahr(e.GelaendeStrichBeenden(), "Geländestrich ist ein Schritt");
        e.BodenStrichBeginnen(Erde);
        foreach (var p in Strich) { e.BodenStrichWeiter(p); Lesen(); }
        Pruefe.Wahr(e.BodenStrichBeenden(), "Bodenstrich ist ein Schritt");
        e.FelsStrichBeginnen(Bloecke);
        foreach (var p in Strich) { e.FelsStrichWeiter(p); Lesen(); }
        Pruefe.Wahr(e.FelsStrichBeenden(), "Felsstrich ist ein Schritt");
        return e;
    }

    static string Befundtext(Kartenentwurf e) => string.Join("\n", e.Befunde.Select(b => $"{b.Schwere} {b.Regel}: {b.Text}"));

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Pinselstrich läuft: Gelände, Boden und Fels zwischen Beginnen und Beenden, sonst nicht", () =>
        {
            var e = Karte();
            Pruefe.Wahr(!e.PinselstrichLaeuft, "vorher");
            e.GelaendeStrichBeginnen(Heben);
            Pruefe.Wahr(e.PinselstrichLaeuft, "Gelände");
            e.GelaendeStrichBeenden();
            Pruefe.Wahr(!e.PinselstrichLaeuft, "nach dem Gelände");
            e.BodenStrichBeginnen(Erde);
            Pruefe.Wahr(e.PinselstrichLaeuft, "Boden");
            e.BodenStrichBeenden();
            e.FelsStrichBeginnen(Bloecke);
            Pruefe.Wahr(e.PinselstrichLaeuft, "Fels");
            e.FelsStrichBeenden();
            Pruefe.Wahr(!e.PinselstrichLaeuft, "nachher");
        }),
        ("Pinselstrich: ohne Prüfung zwischen den Tupfern dieselbe Karte (Bytes) und dieselben Befunde wie mit", () =>
        {
            var alt = TupferFuerTupfer(pruefenJeTupfer: true);
            var neu = TupferFuerTupfer(pruefenJeTupfer: false);
            Pruefe.Gleich(alt.ZuJson(), neu.ZuJson(), "Kartendatei");
            Pruefe.Gleich(Befundtext(alt), Befundtext(neu), "Befunde");
        }),
        ("Pinselstrich: Tupfer für Tupfer gleich dem Strich in einem Aufruf, Rückgängig nimmt je Strich einen Schritt zurück", () =>
        {
            var tupfer = TupferFuerTupfer(pruefenJeTupfer: false);
            var ganz = Karte();
            var anfang = ganz.ZuJson();
            Pruefe.Wahr(ganz.GelaendePinseln(Heben, Strich), "Gelände");
            Pruefe.Wahr(ganz.BodenMalen(Erde, Strich), "Boden");
            Pruefe.Wahr(ganz.FelsStreuen(Bloecke, Strich), "Fels");
            Pruefe.Gleich(ganz.ZuJson(), tupfer.ZuJson(), "Kartendatei");
            for (var i = 0; i < 3; i++) tupfer.Geschichte.Rueckgaengig();
            Pruefe.Gleich(anfang, tupfer.ZuJson(), "drei Schritte zurück: wie vorher");
            Pruefe.Wahr(!tupfer.Geschichte.IstGeaendert, "nichts ungespeichert");
        }),
    };
}
