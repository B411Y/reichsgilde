using System.Text.Json;
using Reichsgilde;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Wirtschaft;

/// <summary>
/// Eine Strecke für Kern und Bild (31.10.2026, Datenversion 65; <see cref="Karrenweg"/>): Der Weg der Karren beginnt am Halteplatz des Modells
/// („Anker_Karren“, im Katalog als karrenAnker), führt über den Hof gerade an den nächsten Weg der Karte (jeder Klasse), über das Netz aller Wege
/// (Gassen ×2, Fußwege ×4 für die Wahl des Weges) und endet am Halt 3,5 m vor dem Pflaster; zurück derselbe Weg. Die Darstellung fährt genau diesen
/// Verlauf (<see cref="Stadtwirtschaft.Karrenverlauf"/>) – so ist der Karren im festen Tempo zur Kernzeit am Halt. Alte Daten rechnen wie vorher.
/// </summary>
static class KarrenwegTests
{
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));
    static string Spielordner => Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft()))!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Spielordner, "daten", "katalog.json")));
    static readonly string[] Karten = { "greybox", "koeln", "probestadt" };
    static readonly string[] Arten = { "Handkarren", "Ochsenkarren", "Kriegskarren" };

    /// <summary>Die Startpartie einer Karte mit allen KI-Häusern („Schwer“: mit Werkstatt und Umlandbetrieb, so sind auch die Umlandwege dabei).</summary>
    static Stadtwirtschaft Partie(string kennung, Wirtschaftsdaten? daten = null)
    {
        var haeuser = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Datenpfad.Karte(kennung)))!["start"]?["haeuser"]?.AsArray().Count ?? 1;
        var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
            daten ?? Daten.Value, new NeuePartie(Rivalen: haeuser), karte: Stadtkarte.AusDatei(Datenpfad.Karte(kennung)));
        return sim.Wirtschaft!;
    }

    /// <summary>Wo „Anker_Karren“ in einem Modell liegt ([x, z] in Metern; die Verschiebungen der Elternknoten eingerechnet) – null ohne den Knoten.</summary>
    static (double X, double Z)? AnkerImModell(string modell)
    {
        var daten = File.ReadAllBytes(Path.Combine(Spielordner, "assets", "modelle", modell + ".glb"));
        var laenge = BitConverter.ToInt32(daten, 12); // glb: Kopf (12 Bytes), dann der JSON-Teil mit Länge und Art
        using var json = JsonDocument.Parse(daten.AsMemory(20, laenge));
        var knoten = json.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        var eltern = new Dictionary<int, int>();
        for (var i = 0; i < knoten.Count; i++)
            if (knoten[i].TryGetProperty("children", out var kinder))
                foreach (var k in kinder.EnumerateArray()) eltern[k.GetInt32()] = i;
        var nummer = knoten.FindIndex(k => k.TryGetProperty("name", out var n) && n.GetString() == "Anker_Karren");
        if (nummer < 0) return null;
        double x = 0, z = 0;
        for (int? k = nummer; k is { } i; k = eltern.TryGetValue(i, out var e) ? e : null)
        {
            Pruefe.Wahr(!knoten[i].TryGetProperty("rotation", out _) && !knoten[i].TryGetProperty("scale", out _), $"{modell}: Anker ohne Drehung und Maßstab");
            if (knoten[i].TryGetProperty("translation", out var t)) (x, z) = (x + t[0].GetDouble(), z + t[2].GetDouble());
        }
        return (x, z);
    }

    /// <summary>Der nächste Punkt auf einem Weg der Karte (jeder Klasse) und sein Abstand.</summary>
    static (Kartenort Punkt, double Abstand) NaechsterWegpunkt(Stadtkarte karte, Kartenort p)
    {
        var best = (Punkt: p, Abstand: double.MaxValue);
        foreach (var w in karte.Wege)
        {
            var linie = w.Verlauf.Select(Kartenort.Aus).ToList();
            for (var i = 0; i + 1 < linie.Count; i++)
            {
                var (a, b) = (linie[i], linie[i + 1]);
                var (dx, dz) = (b.X - a.X, b.Z - a.Z);
                var l2 = dx * dx + dz * dz;
                var t = l2 <= 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / l2, 0, 1);
                var q = new Kartenort(a.X + dx * t, a.Z + dz * t);
                if (q.Abstand(p) < best.Abstand) best = (q, q.Abstand(p));
            }
        }
        return best;
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Karrenweg: karrenAnker jedes Werkstatt- und Umlandmodells im Katalog wie „Anker_Karren“ im Modell (glb), auf 1 cm", () =>
        {
            var geprueft = 0;
            foreach (var b in Katalog.Value.Bausteine.Where(b => b.Art is Bausteinart.Werkstatt or Bausteinart.Umlandbetrieb))
            {
                var anker = AnkerImModell(b.Modell!);
                Pruefe.Wahr(anker is not null, $"{b.Kennung}: Das Modell {b.Modell} hat keinen Anker_Karren");
                Pruefe.Wahr(b.KarrenAnker is { Length: 2 }, $"{b.Kennung}: karrenAnker fehlt im Katalog (Modell: {anker})");
                Pruefe.Ungefaehr(anker!.Value.X, b.KarrenAnker![0], 0.01);
                Pruefe.Ungefaehr(anker.Value.Z, b.KarrenAnker[1], 0.01);
                geprueft++;
            }
            Pruefe.Gleich(18, geprueft, "10 Werkstätten und 8 Umlandbetriebe");
            Pruefe.WirftMitMeldung(() => Gebaeudekatalog.AusJson("""{ "bausteine": [ { "kennung": "x", "art": "werkstatt", "modell": "m", "beruf": "Schmied", "groesse": [16, 14], "karrenAnker": [1] } ] }"""),
                "karrenAnker hat genau zwei Zahlen");
        }),

        ("Karrenweg: Greybox, Köln, Probestadt – Start am Halteplatz, über den Hof an den nächsten Weg, Ende am Halt 3,5 m vor dem Pflaster; Verlauf und Stücke passen zusammen, zurück derselbe Weg", () =>
        {
            foreach (var kennung in Karten)
            {
                var w = Partie(kennung);
                var karte = w.Karte!;
                var geprueft = 0;
                foreach (var b in w.Betriebe.Where(b => w.Fahrten(b) is { Hin.Count: > 0 }))
                {
                    var wer = $"{kennung}/{b.Name}";
                    var (hin, zurueck) = w.Fahrten(b)!.Value;
                    var punkte = w.Karrenverlauf(b);
                    Pruefe.Wahr(punkte is not null, $"{wer}: kein Verlauf");
                    Pruefe.Gleich(hin.Count + 1, punkte!.Count, wer);
                    for (var i = 0; i < hin.Count; i++)
                        Pruefe.Ungefaehr(hin[i].Laenge, punkte[i].Abstand(punkte[i + 1]), 1e-6);
                    // Start am Halteplatz, bei einer Werkstatt dann der Hof; von dort gerade an den nächsten Weg der Karte (jeder Klasse)
                    var (anker, hof) = Karrenweg.Start(w.Bauen.PlatzVon(b.Name)!, b, karte.Katalog);
                    Pruefe.Ungefaehr(0, punkte[0].Abstand(anker), 1e-9);
                    Pruefe.Wahr(b.InDerStadt == (anker.Abstand(hof) > 1), $"{wer}: Halteplatz einer Werkstatt seitlich am Tor, im Umland der Hof selbst");
                    var k = anker.Abstand(hof) > 1e-9 ? 1 : 0;
                    Pruefe.Ungefaehr(0, punkte[k].Abstand(hof), 1e-9);
                    var (naechster, abstand) = NaechsterWegpunkt(karte, hof);
                    if (abstand > 1e-9) k++;
                    Pruefe.Wahr(punkte[k].Abstand(naechster) < 0.01, $"{wer}: Anschluss {punkte[k]} statt {naechster} ({abstand:0.0} m vom Hof)");
                    Pruefe.Wahr(hin.Take(k).All(s => s.Weg < 0 && s.Art == Fahrbahn.Strasse), $"{wer}: der Zubringer gehört zu keinem Weg");
                    Pruefe.Wahr(abstand <= 1e-9 || Math.Abs(hin[k - 1].Laenge - abstand) < 0.01, $"{wer}: Zubringer {abstand:0.00} m"); // liegt der Hof auf dem Weg, gibt es keinen
                    // Ende am Halt: in den Achsen des Platzes 3,5 m vor dem Pflaster, vorher nie darin
                    var halt = Karrenweg.Halt(w.MarktplatzVon(b)!);
                    Pruefe.Ungefaehr(Math.Min(w.MarktplatzVon(b)!.Baustein!.Groesse![0], w.MarktplatzVon(b)!.Baustein!.Groesse![1]) / 2 + 3.5, halt.Halb, 1e-12);
                    Pruefe.Ungefaehr(halt.Halb, Karrenweg.Randabstand(punkte[^1], halt), 1e-6);
                    Pruefe.Wahr(punkte.Take(punkte.Count - 1).All(p => Karrenweg.Randabstand(p, halt) >= halt.Halb - 1e-9), $"{wer}: vor dem Halt schon am Platz");
                    // zurück derselbe Weg rückwärts
                    Pruefe.Gleich(hin.Count, zurueck.Count, wer);
                    for (var i = 0; i < hin.Count; i++)
                    {
                        Pruefe.Gleich(hin[i].Laenge, zurueck[hin.Count - 1 - i].Laenge);
                        Pruefe.Gleich(-hin[i].Anstieg, zurueck[hin.Count - 1 - i].Anstieg);
                    }
                    geprueft++;
                }
                Pruefe.Wahr(geprueft >= 3, $"{kennung}: {geprueft} Betriebe mit Weg");
            }
        }),

        ("Karrenweg: Im Bild fährt der Karren den Verlauf des Kerns im festen Tempo und ist zur Kernzeit am Halt (bis auf die Rundung auf ganze Minuten)", () =>
        {
            foreach (var kennung in Karten)
            {
                var w = Partie(kennung);
                var grenzen = w.Daten.Logistik.Weglaenge!;
                var wasser = w.Daten.Logistik.Wasser!;
                foreach (var b in w.Betriebe.Where(b => w.Karrenverlauf(b) is not null))
                {
                    var punkte = w.Karrenverlauf(b)!;
                    var laenge = 0.0; // so lang ist der Weg im Bild
                    for (var i = 0; i + 1 < punkte.Count; i++) laenge += punkte[i].Abstand(punkte[i + 1]);
                    foreach (var typ in Arten)
                        foreach (var zumMarkt in new[] { true, false })
                        {
                            var fahrt = zumMarkt ? w.Fahrten(b)!.Value.Hin : w.Fahrten(b)!.Value.Zurueck;
                            var bild = new Fahrprofil(Karrentempo.Stuecke(fahrt, w.Zeitfaktor, wasser.FurtMinuten, wasser.FaehreMinuten), laenge, w.Daten.Logistik.Karren[typ].MeterJeMinute);
                            if (bild.Dauer < grenzen.MindestMinuten || bild.Dauer > grenzen.HoechstMinuten) continue; // an den Grenzen fährt das Bild schneller bzw. wartet
                            var kern = w.Fahrzeit(b, typ, zumMarkt);
                            Pruefe.Wahr(Math.Abs(bild.Dauer - kern) <= 0.5 + 1e-9, $"{kennung}/{b.Name}/{typ}: im Bild {bild.Dauer:0.00} min, Kern {kern} min");
                        }
                }
            }
        }),

        ("Karrenweg: Gassen und Fußwege liegen im Netz, kosten aber mehr – eine Gasse nur, wenn die Karrenstraße mehr als doppelt so weit ist", () =>
        {
            // von (0 | 0) nach (100 | 0): direkt eine Gasse (100 m, kostet 200) oder ein Umweg über die Hauptstraße nach Norden
            Stadtkarte Karte(double umweg, string klasse = "gasse") => Stadtkarte.AusJson($$"""
                { "format": 2, "kennung": "gassen", "stadt": "Gassen", "raster": 2.0,
                  "wege": [
                    { "name": "Umweg", "klasse": "haupt", "linie": [[0, 0], [0, {{umweg}}], [100, {{umweg}}], [100, 0]] },
                    { "name": "Abkürzung", "klasse": "{{klasse}}", "linie": [[0, 0], [100, 0]] }
                  ], "gebaeude": [] }
                """, Katalog.Value);
            var (a, z) = (new Kartenort(0, 0), new Kartenort(100, 0));
            // Umweg 40 + 100 + 40 = 180 m < 200: die Hauptstraße, obwohl länger
            var fahrt = Karrennetz.Von(Karte(40), alleWege: true).Fahrt(a, z);
            Pruefe.Ungefaehr(180, fahrt.Sum(s => s.Laenge), 1e-6);
            Pruefe.Ungefaehr(180, Karrennetz.Kosten(fahrt), 1e-6);
            // Umweg 60 + 100 + 60 = 220 m > 200: die Gasse
            fahrt = Karrennetz.Von(Karte(60), alleWege: true).Fahrt(a, z);
            Pruefe.Ungefaehr(100, fahrt.Sum(s => s.Laenge), 1e-6);
            Pruefe.Ungefaehr(200, Karrennetz.Kosten(fahrt), 1e-6);
            Pruefe.Wahr(fahrt.All(s => s.Kostenfaktor == 2), "jedes Stück kennt seinen Kostenfaktor");
            // ein Fußweg kostet das Vierfache: 220 m Umweg sind billiger als 400
            Pruefe.Ungefaehr(220, Karrennetz.Von(Karte(60, "fussweg"), alleWege: true).Fahrt(a, z).Sum(s => s.Laenge), 1e-6);
            // ohne „alle Wege“ (alte Daten) gibt es die Gasse nicht
            Pruefe.Ungefaehr(220, Karrennetz.Von(Karte(60)).Fahrt(a, z).Sum(s => s.Laenge), 1e-6);
            // ein Hof hängt nie an einer Linie über einen Marktplatz (sie ist kein Weg der Karte), der Markt selbst schon
            var markt = Stadtkarte.AusJson("""
                { "format": 2, "kennung": "platz", "stadt": "Platz", "raster": 2.0,
                  "wege": [ { "name": "Weststraße", "klasse": "haupt", "linie": [[-100, 0], [-12, 0]] } ],
                  "gebaeude": [ { "kennung": "Markt", "katalog": "markt", "name": "Markt", "punkt": [0, 0] } ] }
                """, Katalog.Value);
            Pruefe.Gleich(1, Karrennetz.Marktlinien(markt).Count, "die Weststraße endet am Platz");
            var netz = Karrennetz.Von(markt, alleWege: true);
            var hof = new Kartenort(-6, 5); // 5 m neben der Linie über den Platz, 7,8 m vom Ende der Weststraße
            var vonHof = netz.FahrtMitVerlauf(hof, new Kartenort(0, 0));
            Pruefe.Ungefaehr(0, vonHof.Punkte[0].Abstand(new Kartenort(-12, 0)), 1e-9);
            Pruefe.Ungefaehr(12, vonHof.Stuecke.Sum(s => s.Laenge), 1e-9);
            Pruefe.Ungefaehr(6, netz.Fahrt(hof, new Kartenort(0, 0)).Sum(s => s.Laenge), 1e-9); // ohne die Regel hinge er an der Linie über den Platz
        }),

        ("Karrenweg: Alte Daten (vor Datenversion 65) – die Strecke wie bisher (vom Platz des Betriebs über Karrenstraßen zur Mitte des Marktplatzes), ohne Verlauf", () =>
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()))!;
            json["logistik"]!.AsObject().Remove("weglaenge");
            var alt = Wirtschaftsdaten.AusJson(json.ToJsonString());
            foreach (var kennung in Karten)
            {
                var w = Partie(kennung, alt);
                var karte = w.Karte!;
                var netz = Karrennetz.Von(karte, marktlinien: false);
                Pruefe.Gleich(netz.Groesse, w.Karrennetz!.Groesse, $"{kennung}: das alte Netz (nur Karrenstraßen, kein Platz als Kreuzung)");
                foreach (var b in w.Betriebe.Where(b => w.Bauen.PlatzVon(b.Name) is not null))
                {
                    var (x, z) = w.Bauen.PlatzVon(b.Name)!.Punkt;
                    var m = w.MarktplatzVon(b)!;
                    var (hin, zurueck) = w.Fahrten(b)!.Value;
                    var vorher = netz.Fahrt(new Kartenort(x, z), new Kartenort(m.Punkt[0], m.Punkt[1]));
                    Pruefe.Gleich(vorher.Count, hin.Count, $"{kennung}/{b.Name}");
                    for (var i = 0; i < hin.Count; i++) Pruefe.Gleich(vorher[i], hin[i]);
                    Pruefe.Gleich(netz.Fahrt(new Kartenort(m.Punkt[0], m.Punkt[1]), new Kartenort(x, z)).Sum(s => s.Laenge), zurueck.Sum(s => s.Laenge));
                    Pruefe.Gleich(null, w.Karrenverlauf(b), $"{kennung}/{b.Name}: mit alten Daten sucht die Darstellung den Weg selbst");
                }
            }
        }),
    };
}
