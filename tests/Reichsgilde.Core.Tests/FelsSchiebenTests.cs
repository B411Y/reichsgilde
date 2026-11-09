using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Fels P2: Schieben mit den Pfeiltasten (<see cref="Kartenentwurf.FelsSchieben"/>). Eine Folge von Schüben derselben Stücke ist ein Schritt
/// (zusammengefasst): ein Rückgängig stellt die Datei vor der Folge Byte für Byte her. Ohne Zusammenfassen, nach einem anderen Befehl, nach
/// Rückgängig oder mit anderen Stücken beginnt ein neuer Schritt.
/// </summary>
static class FelsSchiebenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Ein Hügel mit drei Blöcken und einer Kuppe (Gruppe).</summary>
    static Kartenentwurf Probe()
    {
        var e = Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 40, Staerke: 8, Kante: 0.5), new[] { O(0, 0) });
        var block = new Felspinsel(Formen: new[] { "block" }, Boden: false);
        for (var i = 0; i < 3; i++) e.FelsStempeln(block, O(-20 + i * 10, 5), drehung: 30 * i, startwert: 4 + i);
        e.FelsStempeln(new Felspinsel(Gruppe: "kuppe", Boden: false), O(30, -30), startwert: 2);
        return e;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fels P2: Zehn Schübe hintereinander (zusammengefasst) sind ein Schritt; ein Rückgängig stellt die Datei vor der Folge her, Wiederholen die nach ihr", () =>
        {
            var e = Probe();
            var auswahl = e.Felsen.Take(3).ToList();
            var (x0, z0) = (auswahl[0].X, auswahl[0].Z);
            var vorher = e.ZuJson();
            var schritte = e.Geschichte.Schritte;
            for (var i = 0; i < 10; i++) Pruefe.Wahr(e.FelsSchieben(auswahl, 0.25, 0, 0, zusammenfassen: i > 0), $"Schub {i}");
            Pruefe.Gleich(schritte + 1, e.Geschichte.Schritte, "ein Schritt für die ganze Folge");
            Pruefe.Gleich(Kartenraster.Punkt(x0 + 2.5), auswahl[0].X, "2,5 m nach Osten");
            Pruefe.Gleich(z0, auswahl[0].Z);
            Pruefe.Gleich("3 Felsstücke geschoben", e.Geschichte.RueckgaengigText);
            var nachher = e.ZuJson();
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson(), "Rückgängig: vor der Folge");
            e.Wiederholen();
            Pruefe.Gleich(nachher, e.ZuJson(), "Wiederholen: nach der Folge");
        }),
        ("Fels P2: Die zusammengefasste Folge gibt dieselbe Datei wie ein einziger Schub um die Summe (auch mit Höhe und schräg)", () =>
        {
            var a = Probe();
            var b = Probe();
            var schuebe = new[] { (0.177, 0.0, -0.177), (0.177, 0.0, -0.177), (0.0, 0.5, 0.0), (-0.25, 0.0, 0.0), (0.177, -0.25, -0.177) };
            foreach (var (dx, dy, dz) in schuebe) a.FelsSchieben(a.Felsen.ToList(), dx, dy, dz, zusammenfassen: true);
            b.FelsSchieben(b.Felsen.ToList(), schuebe.Sum(s => s.Item1), schuebe.Sum(s => s.Item2), schuebe.Sum(s => s.Item3), zusammenfassen: false);
            Pruefe.Gleich(b.ZuJson(), a.ZuJson());
            Pruefe.Gleich(b.Geschichte.Schritte, a.Geschichte.Schritte, "gleich viele Schritte");
        }),
        ("Fels P2: Ohne Zusammenfassen, nach einem anderen Befehl, nach Rückgängig oder mit anderen Stücken beginnt ein neuer Schritt", () =>
        {
            var e = Probe();
            var auswahl = e.Felsen.Take(2).ToList();
            var s = e.Geschichte.Schritte;
            e.FelsSchieben(auswahl, 1, 0, 0, zusammenfassen: true);
            e.FelsSchieben(auswahl, 1, 0, 0, zusammenfassen: false);
            Pruefe.Gleich(s + 2, e.Geschichte.Schritte, "ohne Zusammenfassen");
            e.FelsEinrasten(auswahl);
            e.FelsSchieben(auswahl, 0, 0, 1, zusammenfassen: true);
            Pruefe.Gleich(s + 4, e.Geschichte.Schritte, "nach einem anderen Befehl");
            e.FelsSchieben(e.Felsen.Take(3).ToList(), 0, 0, 1, zusammenfassen: true);
            Pruefe.Gleich(s + 5, e.Geschichte.Schritte, "andere Stücke");
            e.Rueckgaengig();
            e.FelsSchieben(e.Felsen.Take(3).ToList(), 0, 0, 1, zusammenfassen: true);
            Pruefe.Gleich(s + 5, e.Geschichte.Schritte, "nach Rückgängig: neu, nicht mit dem zurückgenommenen zusammen");
            Pruefe.Wahr(!e.Geschichte.KannWiederholen, "der neue Schritt löscht das Wiederholen");
        }),
        ("Fels P2: Hebt eine Folge sich auf (hin und zurück), bleibt kein Schritt und die Datei wie vorher; ohne Stücke geschieht nichts", () =>
        {
            var e = Probe();
            var auswahl = e.Felsen.Take(3).ToList();
            var vorher = e.ZuJson();
            var s = e.Geschichte.Schritte;
            e.FelsSchieben(auswahl, 0.25, 0, 0, zusammenfassen: true);
            Pruefe.Wahr(!e.FelsSchieben(auswahl, -0.25, 0, 0, zusammenfassen: true), "der Rückschub ändert nichts gegenüber vorher");
            Pruefe.Gleich(s, e.Geschichte.Schritte);
            Pruefe.Gleich(vorher, e.ZuJson());
            Pruefe.Wahr(!e.FelsSchieben(Array.Empty<EntwurfFels>(), 1, 0, 0, zusammenfassen: true), "ohne Stücke");
            Pruefe.Wahr(!e.FelsSchieben(auswahl, 0, 0, 0, zusammenfassen: true), "ohne Versatz");
            Pruefe.Gleich(s, e.Geschichte.Schritte);
        }),
        ("Fels P2: An der Höhengrenze (±20 m) merkt sich die Folge nur, was ankam – der erste Druck zurück bewegt das Stück wieder", () =>
        {
            var e = Probe();
            var (a, b, c) = (e.Felsen[0], e.Felsen[1], e.Felsen[2]);
            e.FelsTransformieren(new[] { a }, new Felswerte(Versatz: 19));
            e.FelsTransformieren(new[] { b }, new Felswerte(Versatz: 0));
            e.FelsTransformieren(new[] { c }, new Felswerte(Versatz: -19.5));
            var vorher = e.ZuJson();
            var s = e.Geschichte.Schritte;
            var auswahl = new List<EntwurfFels> { a, b };
            for (var i = 0; i < 3; i++) e.FelsSchieben(auswahl, 0, 1, 0, zusammenfassen: true);
            Pruefe.Gleich(Felsformen.VersatzMax, a.Versatz, "oben gekappt");
            Pruefe.Gleich(3.0, b.Versatz);
            Pruefe.Wahr(e.FelsSchieben(auswahl, 0, -1, 0, zusammenfassen: true), "der erste Druck zurück ändert etwas");
            Pruefe.Gleich(19.0, a.Versatz, "ein Druck zurück: 1 m unter der Grenze");
            Pruefe.Gleich(2.0, b.Versatz, "das andere Stück läuft weiter mit");
            Pruefe.Wahr(e.FelsSchieben(auswahl, 0, -1, 0, zusammenfassen: true), "zweiter Druck");
            Pruefe.Gleich(18.0, a.Versatz);
            Pruefe.Gleich(1.0, b.Versatz);
            Pruefe.Gleich(s + 1, e.Geschichte.Schritte, "ein Schritt");
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson(), "Rückgängig: vor der Folge");
            // unten genauso; hin und zurück hebt sich auf
            var unten = new List<EntwurfFels> { c };
            e.FelsSchieben(unten, 0, -0.5, 0, zusammenfassen: true);
            e.FelsSchieben(unten, 0, -0.5, 0, zusammenfassen: true);
            Pruefe.Gleich(-Felsformen.VersatzMax, c.Versatz, "unten gekappt");
            Pruefe.Wahr(!e.FelsSchieben(unten, 0, 0.5, 0, zusammenfassen: true), "zurück auf den Anfang: kein Schritt");
            Pruefe.Gleich(-19.5, c.Versatz);
            Pruefe.Gleich(vorher, e.ZuJson());
            Pruefe.Gleich(s, e.Geschichte.Schritte);
        }),
        ("Fels P2: Ein gespeicherter Stand mitten in einer Folge gilt danach als geändert", () =>
        {
            var e = Probe();
            var auswahl = e.Felsen.Take(1).ToList();
            e.FelsSchieben(auswahl, 0.25, 0, 0, zusammenfassen: true);
            e.Geschichte.Gespeichert();
            Pruefe.Wahr(!e.Geschichte.IstGeaendert, "gespeichert");
            e.FelsSchieben(auswahl, 0.25, 0, 0, zusammenfassen: true);
            Pruefe.Wahr(e.Geschichte.IstGeaendert, "nach dem nächsten Schub geändert");
        }),
    };
}
