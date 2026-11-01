using System.Text.Json;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Felspinsel F5: einzelne Felsstücke in der Werkstatt wählen, ziehen und drehen (<see cref="Kartenentwurf.FelsVerschieben"/>,
/// <see cref="Kartenentwurf.FelsDrehen"/>) – je ein Schritt mit Rückgängig, gerundet wie in der Datei, ein laufender Strich endet vorher.
/// Dazu: Die Formen der Gesteinsvorlagen in <c>game/daten/gestein.json</c> sind die Kennungen der Karte.
/// </summary>
static class FelsenWaehlenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenentwurf MitBlock(out EntwurfFels fels)
    {
        var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);
        e.FelsStempeln(new Felspinsel(Formen: new[] { "block" }), new Kartenort(10, 20), drehung: 30, startwert: 5);
        fels = e.Felsen.Single();
        return e;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Felsen F5: Verschieben ist ein Schritt, gerundet wie in der Datei, Rückgängig Byte für Byte zurück", () =>
        {
            var e = MitBlock(out var f);
            var vorher = e.ZuJson();
            var (drehung, massstab, einsinken) = (f.Drehung, f.Massstab, f.Einsinken);
            e.FelsVerschieben(f, 33.333333, -12.04);
            Pruefe.Gleich(Kartenraster.Punkt(33.333333), f.X);
            Pruefe.Gleich(Kartenraster.Punkt(-12.04), f.Z);
            Pruefe.Gleich(drehung, f.Drehung);
            Pruefe.Gleich(massstab, f.Massstab);
            Pruefe.Gleich(einsinken, f.Einsinken);
            Pruefe.Wahr(e.ZuJson() != vorher, "die Datei ändert sich");
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson());
            e.Wiederholen();
            Pruefe.Gleich(Kartenraster.Punkt(33.333333), e.Felsen.Single().X);
        }),
        ("Felsen F5: Drehen ist ein Schritt (0 … 360°), gleiche Lage ändert nichts", () =>
        {
            var e = MitBlock(out var f);
            var vorher = e.ZuJson();
            e.FelsDrehen(f, f.Drehung);
            Pruefe.Gleich(vorher, e.ZuJson());
            e.Rueckgaengig(); // kein Schritt fürs Drehen auf dieselbe Lage: Rückgängig nimmt den Stempel zurück
            Pruefe.Gleich(0, e.Felsen.Count);
            e.Wiederholen();
            f = e.Felsen.Single();
            e.FelsDrehen(f, -15);
            Pruefe.Gleich(Kartenraster.Drehung(-15), f.Drehung);
            Pruefe.Wahr(f.Drehung is >= 0 and < 360, $"Drehung {f.Drehung} liegt in 0 … 360");
            e.FelsDrehen(f, 375.04);
            Pruefe.Gleich(Kartenraster.Drehung(375.04), f.Drehung);
            e.Rueckgaengig();
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson());
        }),
        ("Felsen F5: Verschieben und Drehen beenden einen laufenden Felsstrich zuerst (er bleibt ein eigener Schritt)", () =>
        {
            var e = MitBlock(out var f);
            e.FelsStrichBeginnen(new Felspinsel(Formen: new[] { "schuttbrocken" }, Radius: 6, Dichte: 0.3), startwert: 3);
            e.FelsStrichWeiter(new Kartenort(-60, -40));
            Pruefe.Wahr(e.Felsen.Count > 1, "der Strich hat Stücke gesetzt");
            var mitStrich = e.Felsen.Count;
            e.FelsVerschieben(f, 12, 22);
            Pruefe.Wahr(!e.FelsStrichLaeuft, "der Strich ist beendet");
            Pruefe.Gleich(mitStrich, e.Felsen.Count);
            e.Rueckgaengig(); // das Verschieben
            Pruefe.Gleich(Kartenraster.Punkt(10), e.Felsen[0].X);
            Pruefe.Gleich(mitStrich, e.Felsen.Count);
            e.Rueckgaengig(); // der Strich
            Pruefe.Gleich(1, e.Felsen.Count);
            e.FelsStrichBeginnen(new Felspinsel(Formen: new[] { "schuttbrocken" }, Radius: 6, Dichte: 0.3), startwert: 4);
            e.FelsStrichWeiter(new Kartenort(60, 40));
            e.FelsDrehen(e.Felsen[0], 90);
            Pruefe.Wahr(!e.FelsStrichLaeuft, "Drehen beendet den Strich");
        }),
        ("Felsen F5: ein Stück, das nicht (mehr) zum Entwurf gehört, lässt sich nicht verschieben", () =>
        {
            var e = MitBlock(out var f);
            e.FelsenLoeschen(new[] { f });
            Pruefe.Wirft(() => e.FelsVerschieben(f, 1, 1));
            Pruefe.Wirft(() => e.FelsDrehen(f, 1));
        }),
        ("Felsen F5: die Formen der Gesteinsvorlagen (gestein.json) sind Kennungen der Karte", () =>
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Datenordner, "gestein.json")));
            var gesteine = doc.RootElement.GetProperty("gesteine").EnumerateArray().ToList();
            Pruefe.Gleich(Felsformen.Gesteine.Count, gesteine.Count);
            foreach (var g in gesteine)
            {
                var formen = g.GetProperty("formen").EnumerateArray().Select(x => x.GetString()).ToList();
                Pruefe.Wahr(formen.Count > 0, $"{g.GetProperty("kennung").GetString()}: keine Formen");
                foreach (var form in formen)
                    Pruefe.Wahr(Felsformen.Finde(form) is not null, $"{g.GetProperty("kennung").GetString()}: Form „{form}“ gibt es in der Karte nicht");
            }
        }),
    };
}
