using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Fels P2: Kopieren von Felsstücken (<see cref="Kartenentwurf.FelsKopieren"/>, Strg+D in der Werkstatt). Die Kopien tragen alle Werte ihres
/// Vorbilds, stehen hinten in der Liste (in der Reihenfolge des Entwurfs, egal wie die Auswahl sortiert ist) und sind zusammen ein Schritt:
/// Rückgängig stellt die Datei Byte für Byte wieder her.
/// </summary>
static class FelsKopierenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static Kartenort O(double x, double z) => new(x, z);

    static Kartenentwurf Leer() => Kartenentwurf.Neu("probe", "Probe", 400, 300, Katalog.Value);

    /// <summary>Ein Hügel, damit die Stücke auf verschiedener Höhe stehen.</summary>
    static Kartenentwurf Huegel()
    {
        var e = Leer();
        e.GelaendePinseln(new Pinsel(Pinselart.Heben, Radius: 40, Staerke: 10, Kante: 0.5), new[] { O(0, 0) });
        return e;
    }

    /// <summary>Zehn Stücke: eine Wand-Gruppe, eine Kuppe und gestreute Einzelstücke (mit Achsen, Kippen und Versatz aus den Bereichen).</summary>
    static Kartenentwurf ZehnStuecke()
    {
        var e = Huegel();
        e.FelsStempeln(new Felspinsel(Gruppe: "wand", Boden: true), O(-40, -30), drehung: 20, startwert: 5);
        e.FelsStempeln(new Felspinsel(Gruppe: "kuppe", Gestein: "buntsandstein", Boden: false), O(40, 30), startwert: 9);
        var streu = new Felspinsel(Formen: new[] { "block", "platte" }, Radius: 20, Dichte: 0.4, Boden: false, AchseX: new Felsbereich(0.8, 1.3),
            Kippbereich: new Felsbereich(-10, 10), Versatzbereich: new Felsbereich(0, 0.4));
        var punkte = Enumerable.Range(0, 8).Select(i => O(-60 + i * 12, 60)).ToList();
        e.FelsStreuen(streu, punkte, startwert: 3);
        e.FelsenLoeschen(e.Felsen.Skip(10).ToList());
        return e;
    }

    /// <summary>
    /// Ein fester Ablauf für den Gleichheitstest: zehn Stücke, die Hälfte versetzt kopiert, die Kopien gedreht und aufgesetzt, alles kopiert,
    /// Rückgängig und Wiederholen – die Kartendatei am Ende.
    /// </summary>
    internal static string Ablauf()
    {
        var e = ZehnStuecke();
        var erste = e.Felsen.Where((_, i) => i % 2 == 0).Reverse().ToList();
        var kopien = e.FelsKopieren(erste, dx: 7.25, dz: -3.333);
        e.FelsTransformieren(kopien, new Felsaenderung(Drehachse: Felsachse.Y, Winkel: 33.3));
        // Pfeiltasten: drei Schübe schräg (Kamera über Eck) und einer in die Höhe, zusammengefasst zu einem Schritt
        for (var i = 0; i < 3; i++) e.FelsSchieben(kopien, 0.1768, 0, -0.1768, zusammenfassen: true);
        e.FelsSchieben(kopien, 0, 0.25, 0, zusammenfassen: true);
        e.FelsEinrasten(kopien, ausrichten: true);
        e.FelsKopieren(e.Felsen.ToList());
        e.Rueckgaengig();
        e.Wiederholen();
        e.FelsKopieren(new[] { e.Felsen[3] }, dz: 12.5);
        return e.ZuJson();
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Fels P2: Zehn Stücke kopieren ergibt zwanzig, ein Rückgängig wieder zehn – dieselbe Datei; Wiederholen bringt die Kopien zurück", () =>
        {
            var e = ZehnStuecke();
            Pruefe.Gleich(10, e.Felsen.Count, "zehn Stücke zum Kopieren");
            var vorher = e.ZuJson();
            var schritte = e.Geschichte.Schritte;
            var kopien = e.FelsKopieren(e.Felsen.ToList());
            Pruefe.Gleich(10, kopien.Count);
            Pruefe.Gleich(20, e.Felsen.Count);
            Pruefe.Gleich(schritte + 1, e.Geschichte.Schritte, "ein Schritt");
            Pruefe.Gleich("10 Felsstücke kopiert", e.Geschichte.RueckgaengigText);
            var nachher = e.ZuJson();
            e.Rueckgaengig();
            Pruefe.Gleich(10, e.Felsen.Count);
            Pruefe.Gleich(vorher, e.ZuJson(), "Rückgängig: dieselbe Datei");
            e.Wiederholen();
            Pruefe.Gleich(20, e.Felsen.Count);
            Pruefe.Gleich(nachher, e.ZuJson(), "Wiederholen: dieselbe Datei");
        }),
        ("Fels P2: Eine Kopie trägt alle Werte ihres Vorbilds (Form, Gestein, Lage, Winkel, Achsen, Einsinken, Versatz) und steht hinten in der Liste", () =>
        {
            var e = ZehnStuecke();
            var liste = e.Felsen.ToList();
            var kopien = e.FelsKopieren(liste);
            for (var i = 0; i < liste.Count; i++)
            {
                var (a, b) = (liste[i], kopien[i]);
                Pruefe.Wahr(!ReferenceEquals(a, b), "ein neues Stück");
                Pruefe.Wahr(ReferenceEquals(e.Felsen[10 + i], b), $"Kopie {i} steht an Stelle {10 + i}");
                Pruefe.Gleich(a.AlsLage(), b.AlsLage(), $"Kopie {i}: dieselbe Lage");
                Pruefe.Gleich(e.GesteinVon(a), e.GesteinVon(b), $"Kopie {i}: dasselbe Gestein");
            }
            Pruefe.Wahr(liste.Any(f => f.Gestein is not null), "eine Kopie mit eigenem Gestein ist dabei");
            Pruefe.Wahr(liste.Any(f => f.AchseX != 1 || f.KippenX != 0 || f.Versatz != 0), "eine Kopie mit Achsen, Kippen oder Versatz ist dabei");
        }),
        ("Fels P2: Kopieren mit Versatz rückt die Kopien gerundet wie in der Datei, das Vorbild bleibt; die Reihenfolge der Auswahl ist egal", () =>
        {
            var e = ZehnStuecke();
            var vorbild = e.Felsen[2];
            var (x, z) = (vorbild.X, vorbild.Z);
            var kopie = e.FelsKopieren(new[] { vorbild }, dx: 5.123456, dz: -3)[0];
            Pruefe.Gleich(Kartenraster.Punkt(x + 5.123456), kopie.X);
            Pruefe.Gleich(Kartenraster.Punkt(z - 3), kopie.Z);
            Pruefe.Gleich((x, z), (vorbild.X, vorbild.Z), "das Vorbild bleibt stehen");
            Pruefe.Gleich($"{vorbild.Bezeichnung} gesetzt", e.Geschichte.RueckgaengigText, "ein Stück: ein einfacher Schritt");
            // vorwärts und rückwärts gewählt: dieselben Bytes
            var a = ZehnStuecke();
            a.FelsKopieren(a.Felsen.Take(6).ToList(), dx: 1);
            var b = ZehnStuecke();
            b.FelsKopieren(b.Felsen.Take(6).Reverse().ToList(), dx: 1);
            Pruefe.Gleich(a.ZuJson(), b.ZuJson(), "Reihenfolge der Auswahl");
        }),
        ("Fels P2: Kopieren ohne Stücke im Entwurf tut nichts (kein Schritt); doppelt gewählte Stücke werden einmal kopiert", () =>
        {
            var e = ZehnStuecke();
            var schritte = e.Geschichte.Schritte;
            Pruefe.Gleich(0, e.FelsKopieren(Array.Empty<EntwurfFels>()).Count);
            var fremd = ZehnStuecke().Felsen[0];
            Pruefe.Gleich(0, e.FelsKopieren(new[] { fremd }).Count, "ein Stück eines anderen Entwurfs");
            Pruefe.Gleich(schritte, e.Geschichte.Schritte, "kein Schritt");
            Pruefe.Gleich(2, e.FelsKopieren(new[] { e.Felsen[0], e.Felsen[1], e.Felsen[0] }).Count, "doppelt gewählt");
            Pruefe.Gleich(12, e.Felsen.Count);
        }),
        ("Fels P2: Unbekannte Felder eines Stücks aus der Datei gehen auf die Kopie über; Lesen und Schreiben ergibt dieselbe Datei", () =>
        {
            var json = JsonNode.Parse(ZehnStuecke().ZuJson())!.AsObject();
            json["felsen"]!["stuecke"]![0]!.AsObject()["notiz"] = "Probe";
            var e = Kartenentwurf.AusJson(json.ToJsonString(), Katalog.Value);
            e.FelsKopieren(new[] { e.Felsen[0] });
            var text = e.ZuJson();
            Pruefe.Gleich(2, text.Split("\"notiz\"").Length - 1, "die Notiz steht zweimal in der Datei:\n" + text);
            Pruefe.Gleich(text, Kartenentwurf.AusJson(text, Katalog.Value).ZuJson(), "wieder geschrieben");
        }),
        ("Fels P2: Der Ablauf des Gleichheitstests ergibt zweimal dieselbe Datei, die Kopien stehen darin", () =>
        {
            var a = Ablauf();
            Pruefe.Gleich(a, Ablauf());
            var e = Kartenentwurf.AusJson(a, Katalog.Value);
            Pruefe.Wahr(e.Felsen.Count > 20, $"mehr als 20 Stücke, ist {e.Felsen.Count}");
        }),
    };
}
