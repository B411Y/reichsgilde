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
            json.AsObject().Remove("jahreszeiten"); // alte Daten kannten keine Jahreszeiten (Datenversion 67)
            var alt = Wirtschaftsdaten.AusJson(json.ToJsonString());
            // Werte des Codes vor der gemeinsamen Strecke (Stand 1f0e80ad, dieselben Daten ohne weglaenge; an den Karrenstraßen beider Karten hat sich
            // nichts geändert): Länge hin und zurück bitgenau, Fahrzeit Handkarren hin und Ochsenkarren zurück
            var alterCode = new (string Karte, string Betrieb, double Hin, double Zurueck, int Hand, int Ochse)[]
            {
                ("greybox", "Gießerei Eisenbart", 22, 22, 30, 30), ("greybox", "Gießerei am Weinmarkt", 24.416407864998735, 24.41640786499874, 30, 30),
                ("greybox", "Grube Eisenbart", 98.08644811766285, 98.08644811766275, 120, 120), ("greybox", "Holzschlag Eichwald", 114.74983589514886, 114.74983589514898, 120, 120),
                ("greybox", "Maurerei Kieselbach", 89, 89, 30, 30), ("greybox", "Steinbruch Kieselbach", 136.4186281064889, 136.4186281064889, 120, 120),
                ("greybox", "Tischlerei Eichwald", 22, 22, 30, 30), ("probestadt", "Gießerei Brendel", 32, 32, 30, 30),
                ("probestadt", "Gießerei am Werkstattplatz", 30, 30, 30, 30), ("probestadt", "Tischlerei Ahlbeck", 32, 32, 30, 30),
            };
            foreach (var kennung in new[] { "greybox", "probestadt" })
            {
                var w = Partie(kennung, alt);
                var mitWeg = w.Betriebe.Where(b => w.Fahrten(b) is { Hin.Count: > 0 }).OrderBy(b => b.Name, StringComparer.Ordinal).ToList();
                var erwartet = alterCode.Where(x => x.Karte == kennung).ToList();
                Pruefe.Gleich(string.Join(", ", erwartet.Select(x => x.Betrieb)), string.Join(", ", mitWeg.Select(b => b.Name)), kennung);
                foreach (var (b, x) in mitWeg.Zip(erwartet))
                {
                    var (hin, zurueck) = w.Fahrten(b)!.Value;
                    Pruefe.Gleich((x.Hin, x.Zurueck, x.Hand, x.Ochse), (hin.Sum(s => s.Laenge), zurueck.Sum(s => s.Laenge), w.Fahrzeit(b, "Handkarren", true), w.Fahrzeit(b, "Ochsenkarren", false)), b.Name);
                }
            }
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

        ("Karrenweg: Randfälle – Start im Haltquadrat hält sofort (Länge 0), Quadrat nie betreten bleibt, Rand genau auf einem Punkt, Zubringer mit Anker am Hof", () =>
        {
            var halt = (Mitte: new Kartenort(0, 0), DrehungGrad: 0.0, Halb: 15.5);
            Netzfahrt Fahrt(params (double X, double Z)[] p) => new(
                p.Zip(p.Skip(1)).Select(x => new Fahrtstueck(new Kartenort(x.First.X, x.First.Z).Abstand(new Kartenort(x.Second.X, x.Second.Z)), 1)).ToList(),
                p.Select(x => new Kartenort(x.X, x.Z)).ToList());
            // Start im Quadrat (ein Betrieb am Platz): sofort am Halt – ein Stück der Länge 0, kein Umweg zur Mitte
            var drin = Karrenweg.BisZumHalt(Fahrt((14, 0), (14, 30), (0, 30), (0, 0)), halt);
            Pruefe.Gleich(1, drin.Stuecke.Count);
            Pruefe.Gleich((0.0, 0.0), (drin.Stuecke[0].Laenge, drin.Stuecke[0].Anstieg));
            Pruefe.Gleich(2, drin.Punkte.Count);
            Pruefe.Wahr(drin.Punkte.All(p => p == new Kartenort(14, 0)), "am Start");
            // nie im Quadrat: bleibt
            var aussen = Fahrt((-40, 20), (40, 20));
            Pruefe.Wahr(ReferenceEquals(aussen, Karrenweg.BisZumHalt(aussen, halt)), "unverändert");
            // der Rand genau auf einem Punkt: das Stück dahinter fällt weg, die Fahrt endet dort
            var rand = Karrenweg.BisZumHalt(Fahrt((0, 40), (0, 15.5), (0, 0)), halt);
            Pruefe.Gleich(1, rand.Stuecke.Count);
            Pruefe.Gleich(24.5, rand.Stuecke[0].Laenge);
            Pruefe.Gleich(new Kartenort(0, 15.5), rand.Punkte[^1]);
            // anteilig: Länge und Anstieg im selben Verhältnis
            var teil = Karrenweg.BisZumHalt(Fahrt((0, 40), (0, 0)), halt);
            Pruefe.Ungefaehr(24.5, teil.Stuecke.Single().Laenge, 1e-9);
            Pruefe.Ungefaehr(24.5 / 40, teil.Stuecke.Single().Anstieg, 1e-9);
            Pruefe.Ungefaehr(15.5, Karrenweg.Randabstand(teil.Punkte[^1], halt), 1e-9);
            // ein gedrehter Platz: das Quadrat dreht mit
            var gedreht = (Mitte: new Kartenort(0, 0), DrehungGrad: 45.0, Halb: 15.5);
            Pruefe.Ungefaehr(15.5 * Math.Sqrt(2), Karrenweg.BisZumHalt(Fahrt((40, 0), (0, 0)), gedreht).Punkte[^1].X, 1e-6);
            // Zubringer: Anker am Hof – nur das Stück vom Hof zum Weg; Hof auf dem Weg – nur Anker → Hof; ohne Fahrt leer
            var flach = Stadtkarte.AusDatei(Datenpfad.Karte("probestadt")).Gelaendeform;
            var netz = Fahrt((0, 0), (10, 0));
            var z1 = Karrenweg.MitZubringer(netz, new Kartenort(0, 5), new Kartenort(0, 5), flach);
            Pruefe.Gleich((2, 3), (z1.Stuecke.Count, z1.Punkte.Count));
            Pruefe.Gleich(5.0, z1.Stuecke[0].Laenge);
            Pruefe.Gleich(-1, z1.Stuecke[0].Weg);
            var z2 = Karrenweg.MitZubringer(netz, new Kartenort(-4, 0), new Kartenort(0, 0), flach);
            Pruefe.Gleich((2, 3), (z2.Stuecke.Count, z2.Punkte.Count));
            Pruefe.Gleich(0, Karrenweg.MitZubringer(Netzfahrt.Leer, new Kartenort(0, 5), new Kartenort(0, 5), flach).Stuecke.Count);
            // dieselbe Fahrt rückwärts
            var rueck = Karrenweg.Umgekehrt(new[] { new Fahrtstueck(3, 1), new Fahrtstueck(4, -2, Fahrbahn.Furt) });
            Pruefe.Gleich((4.0, 2.0, Fahrbahn.Furt, 3.0, -1.0), (rueck[0].Laenge, rueck[0].Anstieg, rueck[0].Art, rueck[1].Laenge, rueck[1].Anstieg));
        }),

        ("Karrenweg: Halteplatz und Hof wie im Bild – Drehung, Maßstab und Versatz (Umland: Modell mit Versatz und Maßstab; Werkstatt: Modell ohne Maßstab, Hof mit)", () =>
        {
            // ein Umlandbaustein mit Versatz und eigenem Anker, vor die Grube in den Katalog gesetzt
            const string grube = "{\"kennung\": \"grube\", \"art\": \"umlandbetrieb\",";
            const string test = "{\"kennung\": \"testgrube\", \"art\": \"umlandbetrieb\", \"name\": \"Testgrube\", \"modell\": \"umland/grube\", " +
                                "\"betriebsart\": \"Mine\", \"groesse\": [16, 14], \"versatz\": [1, 2], \"karrenAnker\": [3, 4]},\n    ";
            var katalog = Gebaeudekatalog.AusJson(File.ReadAllText(Path.Combine(Spielordner, "daten", "katalog.json")).Replace(grube, test + grube));
            Pruefe.Wahr(katalog.Finde("testgrube") is not null, "Testbaustein im Katalog");
            var karte = Stadtkarte.AusJson("""
                { "format": 2, "kennung": "lage", "stadt": "Lage", "raster": 2.0, "wege": [ { "name": "Weg", "klasse": "land", "linie": [[-100, 60], [100, 60]] } ],
                  "gebaeude": [ { "kennung": "U", "katalog": "testgrube", "name": "U", "betrieb": "Testgrube U", "punkt": [10, 20], "drehung": 30, "massstab": 1.5 },
                                { "kennung": "W", "katalog": "werkstattplatz", "name": "W", "punkt": [-20, 5], "drehung": 250, "massstab": 1.5 } ] }
                """, katalog);
            // unabhängig wie Godot gerechnet: Basis(Vector3.Up, θ) * (x, 0, z) = (x cos θ + z sin θ, −x sin θ + z cos θ)
            static (double X, double Z) Dreh(double x, double z, double grad)
            {
                var t = grad * Math.PI / 180;
                return (x * Math.Cos(t) + z * Math.Sin(t), -x * Math.Sin(t) + z * Math.Cos(t));
            }
            var w = Partie("greybox");
            var umland = w.Betriebe.First(b => !b.InDerStadt && b.Art == Betriebsart.Mine);
            var werkstatt = w.Betriebe.First(b => b.InDerStadt && b.Artdaten.Beruf == "Schmied");
            // Umland: Ursprung = Punkt + Dreh(Versatz · m), Anker = Ursprung + Dreh(Anker · m) (ModellLage · Anker_Karren); Hof = Anker
            var (anker, hof) = Karrenweg.Start(new Betriebsplatz(umland.Name, karte.GebaeudeMit("U")!, null), umland, karte.Katalog);
            var (vx, vz) = Dreh(1 * 1.5, 2 * 1.5, 30);
            var (ax, az) = Dreh(3 * 1.5, 4 * 1.5, 30);
            Pruefe.Ungefaehr(10 + vx + ax, anker.X, 1e-9);
            Pruefe.Ungefaehr(20 + vz + az, anker.Z, 1e-9);
            Pruefe.Gleich(anker, hof);
            // Werkstatt: das Modell des Berufs ungedreht in der Mitte, ohne Maßstab (WerkstattBauen); der Hof 0,5 m vor der Vorderkante der Fläche mit Maßstab (Platz.Hof)
            (anker, hof) = Karrenweg.Start(new Betriebsplatz(werkstatt.Name, karte.GebaeudeMit("W")!, null), werkstatt, karte.Katalog);
            var schmied = katalog.Werkstatt("Schmied")!.KarrenAnker!;
            (ax, az) = Dreh(schmied[0], schmied[1], 250);
            var (hx, hz) = Dreh(0, 14 * 1.5 / 2 - 0.5, 250);
            Pruefe.Ungefaehr(-20 + ax, anker.X, 1e-9);
            Pruefe.Ungefaehr(5 + az, anker.Z, 1e-9);
            Pruefe.Ungefaehr(-20 + hx, hof.X, 1e-9);
            Pruefe.Ungefaehr(5 + hz, hof.Z, 1e-9);
        }),

        ("Karrenweg: Liegt der Hof näher am Pflaster als am nächsten Weg, fährt der Karren wie im Bild gerade zum Platz und hält davor", () =>
        {
            var karte = Stadtkarte.AusJson("""
                { "format": 2, "kennung": "platz", "stadt": "Platz", "raster": 2.0,
                  "wege": [ { "name": "Nordstraße", "klasse": "haupt", "linie": [[-100, 40], [100, 40]] } ],
                  "gebaeude": [ { "kennung": "Markt", "katalog": "markt", "name": "Markt", "punkt": [0, 0] } ] }
                """, Katalog.Value);
            var netz = Karrennetz.Von(karte, alleWege: true);
            Pruefe.Ungefaehr(20, netz.AbstandZumWeg(new Kartenort(0, 20))!.Value, 1e-9);
            Pruefe.Ungefaehr(8, Karrenweg.Pflasterabstand(new Kartenort(0, 20), karte.Marktplaetze[0]), 1e-9);
            Pruefe.Ungefaehr(Math.Sqrt(3 * 3 + 4 * 4), Karrenweg.Pflasterabstand(new Kartenort(15, 16), karte.Marktplaetze[0]), 1e-9); // an der Ecke
            // Hof (0 | 20): 8 m zum Pflaster, 20 m zur Straße – über das Pflaster, vom Anker (5 | 20) über den Hof bis zum Halt bei z = 15,5
            var nah = Karrenweg.UeberDasPflaster(new Kartenort(5, 20), new Kartenort(0, 20), karte.Marktplaetze, netz.AbstandZumWeg(new Kartenort(0, 20)), karte.Gelaendeform);
            Pruefe.Wahr(nah is not null, "über das Pflaster");
            Pruefe.Wahr(ReferenceEquals(nah!.Value.Markt, karte.Marktplaetze[0]), "zum Markt");
            Pruefe.Ungefaehr(5 + 4.5, nah.Value.Fahrt.Stuecke.Sum(x => x.Laenge), 1e-6);
            Pruefe.Ungefaehr(15.5, nah.Value.Fahrt.Punkte[^1].Z, 1e-6);
            // Hof (0 | 30): 18 m zum Pflaster, 10 m zur Straße – über die Straße wie sonst
            Pruefe.Gleich(null, Karrenweg.UeberDasPflaster(new Kartenort(5, 30), new Kartenort(0, 30), karte.Marktplaetze, netz.AbstandZumWeg(new Kartenort(0, 30)), karte.Gelaendeform));
            // der Halteplatz schon vor dem Pflaster: sofort am Halt (Mindestdauer)
            var amPlatz = Karrenweg.UeberDasPflaster(new Kartenort(0, 14), new Kartenort(0, 18), karte.Marktplaetze, 22, karte.Gelaendeform);
            Pruefe.Gleich(0.0, amPlatz!.Value.Fahrt.Stuecke.Sum(x => x.Laenge));
        }),
    };
}
