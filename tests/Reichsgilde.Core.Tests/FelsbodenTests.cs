using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Felspinsel F6: Boden um den Fels (Fels unter dem Stück, Schuttkegel hangabwärts, Moossaum oben und im Norden) – mit den Stücken ein Befehl,
/// Rückgängig Byte für Byte, alte Karten unverändert; die Schuttsteine aus Lage und Form; das Suchraster ändert kein Ergebnis.
/// </summary>
static class FelsbodenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Westlich von x = 0 eben, von x = 0 bis 100 ein Hang mit 30 % nach Osten hinauf (wie FelsenTests).</summary>
    static Kartenentwurf HangMitEbene()
    {
        var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 1, Staerke: 30, Kante: 0), new[] { O(100, 0) });
        e.Rampe(O(0, 0), O(100, 0), breite: 400, kante: 0);
        return e;
    }

    static List<Kartenort> Linie(Kartenort von, Kartenort bis, int schritte) =>
        Enumerable.Range(0, schritte + 1).Select(i => O(von.X + (bis.X - von.X) * i / schritte, von.Z + (bis.Z - von.Z) * i / schritte)).ToList();

    static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    static string AlsText(this Bodenraster r) => System.Text.Json.JsonSerializer.Serialize(r);

    static int G(Kartenentwurf e, double x, double z, Bodentextur t) => e.Boden.Gewicht((int)Math.Round(x), (int)Math.Round(z), t);

    /// <summary>Die Prüfsumme des festen Ablaufs der FelsenTests ohne „Boden“, gemessen vor F6 (vor dem Suchraster): Es darf nichts ändern.</summary>
    const string AblaufVorF6 = "2d66973a6b83153867af14bb0bcd333ddd83e347ee5180d2eef2594d201b1384";

    /// <summary>
    /// Ein fester Ablauf mit Boden (Stempel am Hang und in der Ebene, Streuen, Radieren, Rückgängig/Wiederholen) – die Kartendatei. Für die Gleichheit
    /// zwischen Linux und Windows (GleichheitTests, Zeile „felsboden“).
    /// </summary>
    internal static string Ablauf()
    {
        var e = HangMitEbene();
        e.GrundgesteinSetzen("grauwacke");
        e.FelsStempeln(new Felspinsel(Gruppe: "wand"), O(50, -40));
        e.FelsStempeln(new Felspinsel(Gruppe: "kuppe", Groesse: 1.3), O(-50, 30));
        e.FelsStempeln(new Felspinsel(Formen: new[] { "rippe" }), O(70, 50), drehung: 12.5);
        e.FelsStreuen(new Felspinsel(Formen: new[] { "block", "platte", "felsnase", "schuttbrocken" }, Radius: 14, Dichte: 0.25), Linie(O(10, 80), O(90, 100), 20));
        e.FelsStreuen(Felspinsel.Radiergummi(5), Linie(O(30, 85), O(40, 90), 3));
        e.Rueckgaengig();
        e.Wiederholen();
        return e.ZuJson();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Felsboden: Stempel am Hang malt Fels unter der Wand, Kies hangabwärts, Moos am oberen Rand – ein Schritt, Rückgängig Byte für Byte", () =>
        {
            var e = HangMitEbene();
            var vorher = e.ZuJson();
            var neue = e.FelsStempeln(new Felspinsel(Formen: new[] { "wand" }), O(50, 0), drehung: 0);
            Pruefe.Gleich(1, neue.Count);
            Pruefe.Gleich("Felswand gestempelt", e.Geschichte.RueckgaengigText);
            // Mitte: Fels; der Hang steigt nach Osten (+x), hangabwärts ist −x
            Pruefe.Wahr(G(e, 50, 0, Bodentextur.Fels) >= 250, $"Fels unter der Mitte: {G(e, 50, 0, Bodentextur.Fels)}");
            var kies = Enumerable.Range(4, 6).Max(d => G(e, 50 - d, 0, Bodentextur.Kies));
            Pruefe.Wahr(kies >= 100, $"Kies hangabwärts: {kies}");
            Pruefe.Wahr(Enumerable.Range(3, 8).All(d => G(e, 50 + d, 0, Bodentextur.Kies) == 0), "kein Kies hangaufwärts");
            var tiefe = Felsformen.Finde("wand")!.Tiefe / 2; // Halbachse in x bei Drehung 0 ist die Breite – hier quer zum Hang
            var breite = Felsformen.Finde("wand")!.Breite / 2;
            var moosOben = Enumerable.Range(0, 3).Max(d => G(e, 50 + breite + d * 0.5, 0, Bodentextur.Moos));
            Pruefe.Wahr(moosOben >= 60, $"Moos am oberen Rand: {moosOben}");
            Pruefe.Wahr(G(e, 50 + breite + 6, 0, Bodentextur.Moos) == 0, "Moos nur als Saum");
            Pruefe.Wahr(tiefe > 0, "Maße");
            Pruefe.Gleich(0, (int)e.Boden.Dekodichte(50, 0), "Dekodichte bleibt 0");
            var nachher = e.ZuJson();
            e.Rueckgaengig();
            Pruefe.Gleich(Sha(vorher), Sha(e.ZuJson()), "Rückgängig nimmt Stück und Boden zusammen zurück");
            Pruefe.Gleich(0, e.Felsen.Count);
            Pruefe.Wahr(e.Boden.IstLeer, "der Boden ist wieder leer");
            e.Wiederholen();
            Pruefe.Gleich(Sha(nachher), Sha(e.ZuJson()), "Wiederholen");
        }),

        ("Felsboden: In der Ebene ein Kiesring statt des Kegels, Moos stärker im Norden (−z) als im Süden", () =>
        {
            var e = HangMitEbene();
            e.FelsStempeln(new Felspinsel(Formen: new[] { "block" }), O(-100, 0), drehung: 0);
            var form = Felsformen.Finde("block")!;
            var (rx, rz) = (form.Breite / 2, form.Tiefe / 2);
            Pruefe.Wahr(G(e, -100, 0, Bodentextur.Fels) >= 250, "Fels unter dem Block");
            int Max(Func<double, int> f) => Enumerable.Range(0, 4).Max(d => f(d * 0.5));
            var nord = Max(d => G(e, -100, -rz - d, Bodentextur.Moos));
            var sued = Max(d => G(e, -100, rz + d, Bodentextur.Moos));
            Pruefe.Wahr(nord > sued, $"Moos Nord {nord} > Süd {sued}");
            var ring = new[] { (rx + 0.8, 0.0), (-rx - 0.8, 0.0), (0.0, rz + 0.8), (0.0, -rz - 0.8) }.Select(p => G(e, -100 + p.Item1, p.Item2, Bodentextur.Kies)).ToList();
            Pruefe.Wahr(ring.All(k => k > 0), "Kies rund um den Fuß: " + string.Join(",", ring));
            Pruefe.Wahr(G(e, -100 + rx + 4, 0, Bodentextur.Kies) == 0, "nur ein schmaler Ring");
        }),

        ("Felsboden: Streuen malt den Boden beim Ziehen sofort mit, Beenden legt Stücke und Boden als einen Schritt; Radierer lässt den Boden", () =>
        {
            var e = HangMitEbene();
            var vorher = e.ZuJson();
            e.FelsStrichBeginnen(new Felspinsel(Formen: new[] { "block", "kuppe" }, Radius: 10, Dichte: 0.3), startwert: 5);
            e.FelsStrichWeiter(O(30, 0));
            Pruefe.Wahr(e.Felsen.Count > 0 && !e.Boden.IstLeer, "beim Ziehen schon Boden");
            e.FelsStrichWeiter(O(60, 10));
            Pruefe.Wahr(e.FelsStrichBeenden(), "Beenden");
            var nachher = e.ZuJson();
            Pruefe.Wahr(e.Geschichte.RueckgaengigText!.EndsWith("Felsstücke gestreut"), e.Geschichte.RueckgaengigText);
            e.Rueckgaengig();
            Pruefe.Gleich(Sha(vorher), Sha(e.ZuJson()), "ein Schritt zurück: Stücke und Boden");
            e.Wiederholen();
            Pruefe.Gleich(Sha(nachher), Sha(e.ZuJson()), "Wiederholen");

            var boden = e.ZuJson();
            Pruefe.Wahr(e.FelsStreuen(Felspinsel.Radiergummi(30), Linie(O(30, 0), O(60, 10), 4)), "radiert");
            var ohne = Kartenentwurf.AusJson(e.ZuJson(), Katalog.Value);
            var mit = Kartenentwurf.AusJson(boden, Katalog.Value);
            Pruefe.Wahr(ohne.Felsen.Count < mit.Felsen.Count, "Stücke weg");
            Pruefe.Gleich(Sha(mit.Boden.AlsText()), Sha(ohne.Boden.AlsText()), "der Boden bleibt");
        }),

        ("Felsboden: „Boden“ aus malt nichts; derselbe Strich ergibt mit Boden dieselben Stücke wie ohne", () =>
        {
            var (a, b) = (HangMitEbene(), HangMitEbene());
            var strich = Linie(O(-20, 0), O(70, 20), 25);
            a.FelsStreuen(new Felspinsel(Formen: new[] { "block", "wand" }, Radius: 12, Dichte: 0.3, Boden: false), strich, startwert: 9);
            b.FelsStreuen(new Felspinsel(Formen: new[] { "block", "wand" }, Radius: 12, Dichte: 0.3), strich, startwert: 9);
            Pruefe.Wahr(a.Boden.IstLeer, "ohne Boden bleibt das Raster leer");
            Pruefe.Wahr(!b.Boden.IstLeer, "mit Boden nicht");
            Pruefe.Gleich(a.Felsen.Count, b.Felsen.Count);
            Pruefe.Wahr(a.Felsen.Zip(b.Felsen).All(p => p.First.GleicheWerte(p.Second)), "gleiche Stücke");
            a.FelsStempeln(new Felspinsel(Gruppe: "wand", Boden: false), O(40, -60));
            Pruefe.Wahr(a.Boden.IstLeer, "Stempel ohne Boden");
        }),

        ("Felsboden: Das Suchraster ändert nichts – der feste Ablauf der FelsenTests ohne Boden gibt dieselbe Datei wie vor F6; mit Boden reihenfolgefest", () =>
        {
            Pruefe.Gleich(AblaufVorF6, Sha(FelsenTests.Ablauf(boden: false)), "Ablauf ohne Boden wie vor F6");
            // zwei Stempel in umgekehrter Reihenfolge: derselbe Boden (je Punkt das stärkste Gewicht, aus dem Stand vor dem Strich)
            var (a, b) = (HangMitEbene(), HangMitEbene());
            a.FelsStreuen(new Felspinsel(Formen: new[] { "kuppe" }, Radius: 6, Dichte: 0.6), new[] { O(40, 0) }, startwert: 3);
            var stuecke = a.Felsen.Select(f => (f.Form, f.X, f.Z, f.Drehung)).ToList();
            Pruefe.Wahr(stuecke.Count >= 2, $"mehrere Stücke: {stuecke.Count}");
            var bb = new Felsbodenstrich(b.Boden, b.Hoehen, b.Kartengrenzen.Aussen);
            foreach (var f in a.Felsen.Reverse()) bb.Malen(Felsfuss.Von(f));
            bb.Aenderung.Kacheln();
            Pruefe.Gleich(Sha(a.Boden.AlsText()), Sha(b.Boden.AlsText()), "Boden unabhängig von der Reihenfolge");
        }),

        ("Felsboden: Alte Karten – Stempel mit Boden und Rückgängig ergibt Byte für Byte die alte Datei", () =>
        {
            foreach (var datei in Directory.GetFiles(Path.Combine(Path.GetDirectoryName(Datenordner)!, "karten"), "*.json").OrderBy(d => d, StringComparer.Ordinal))
            {
                var text = File.ReadAllText(datei);
                var e = Kartenentwurf.AusJson(text, Katalog.Value);
                var alt = e.ZuJson();
                e.FelsStempeln(new Felspinsel(Gruppe: "wand"), O(10, 10));
                e.FelsStreuen(new Felspinsel(Formen: new[] { "block" }, Radius: 8, Dichte: 0.3), Linie(O(-20, -20), O(0, -10), 4), startwert: 1);
                e.Rueckgaengig();
                e.Rueckgaengig();
                Pruefe.Gleich(Sha(alt), Sha(e.ZuJson()), Path.GetFileName(datei));
            }
        }),

        ("Felsboden: Schuttsteine je Stück fest (gleiche Lage – gleiche Steine), Zahl nach Höhe, am Hang hangabwärts; Schuttbrocken haben keine", () =>
        {
            var e = HangMitEbene();
            var wand = new Felsfuss("wand", 50, 0, 0, 1);
            var a = Felsboden.Schuttsteine(wand, e.Hoehen);
            Pruefe.Gleich(a.Count, Felsboden.Schuttsteine(wand, e.Hoehen).Count);
            Pruefe.Wahr(a.SequenceEqual(Felsboden.Schuttsteine(wand, e.Hoehen)), "gleiche Steine");
            Pruefe.Gleich(Math.Min(Felsboden.SteineMax, (int)Math.Round(Felsboden.SteineJeHoehe * Felsformen.Finde("wand")!.Hoehe, MidpointRounding.AwayFromZero)), a.Count);
            Pruefe.Wahr(a.All(s => s.X < 50), "hangabwärts (−x): " + string.Join(";", a.Select(s => Zahlenformat.Knapp(s.X))));
            Pruefe.Wahr(a.All(s => s.Massstab >= Felsformen.MassstabMin && s.Massstab <= 2), "Maßstab in Grenzen");
            Pruefe.Wahr(!a.SequenceEqual(Felsboden.Schuttsteine(wand with { X = 51 }, e.Hoehen)), "andere Lage – andere Steine");
            Pruefe.Gleich(0, Felsboden.Schuttsteine(new Felsfuss("schuttbrocken", 50, 0, 0, 1), e.Hoehen).Count);
            Pruefe.Gleich(0, Felsboden.Schuttsteine(new Felsfuss("zinne", 50, 0, 0, 1), e.Hoehen).Count);
            var eben = Felsboden.Schuttsteine(new Felsfuss("kuppe", -100, 0, 0, 1), e.Hoehen);
            Pruefe.Wahr(eben.Any(s => s.X < -100) && eben.Any(s => s.X > -100), "in der Ebene rund um den Fuß");
        }),

        ("Felsboden: Der Ablauf mit Boden gibt zweimal und mit deutscher Ländereinstellung dieselben Bytes", () =>
        {
            var a = Sha(Ablauf());
            Pruefe.Gleich(a, Sha(Ablauf()), "zweiter Lauf");
            var (vorher, vorherUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
                Pruefe.Gleich(a, Sha(Ablauf()), "de-DE");
            }
            finally
            {
                (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (vorher, vorherUi);
            }
        }),
    };
}
