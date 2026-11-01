using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Partie;
using Reichsgilde.Core.Speichern;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Kartenbau.Staedte;

/// <summary>
/// Mehrere Marktplätze je Stadt (Entscheidung des Nutzers vom 31.10.2026, Köln: Heumarkt, Alter Markt, Neumarkt): EIN Stadtmarkt mit gemeinsamen
/// Preisen und gemeinsamem Bestand; ein Karren fährt zum nächsten Platz (kürzester Weg über die Karrenstraßen, bei Gleichstand der erste der
/// Karte); ein Marktplatz ist eine Kreuzung (Karrenstraßen, die seine Fläche berühren, sind über seine Mitte verbunden) – mit der Fahrzeit nach
/// Weglänge (Datenversion 65). Greybox, Probestadt und Messstadt rechnen wie bisher, ein alter Spielstand mit alten Daten auf seinem alten Netz,
/// und ein Spielstand merkt sich nichts Neues (der Platz folgt aus Karte und Platz des Betriebs).
/// </summary>
static class MarktplaetzeTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    /// <summary>
    /// Köln (seit 1.11.2026): Rheingasse, Malzbüchel und die Deutzer Straßen führen durch die Mitten der Plätze oder enden dort und brauchen keine
    /// Linie (bis dahin 2: Kunibertswall und Kartäuserwall berührten Alter Markt und Heumarkt an ihrem Ende an der Rheingasse).
    /// </summary>
    const int MarktlinienKoeln = 0;

    /// <summary>
    /// Kleine Karte: Weststraße endet am Westrand eines Marktplatzes (24 × 24 um 0 | 0), die Oststraße beginnt auf dem Platz (12 | 4) und führt
    /// nach Osten, eine Gasse (keine Karrenstraße) endet am Nordrand. Ohne Marktplatz wären die beiden Straßen nicht verbunden.
    /// </summary>
    static string KreuzungJson(bool mitMarkt = true) => $$"""
    { "format": 2, "kennung": "kreuzung", "stadt": "Kreuzung", "raster": 2.0,
      "wege": [
        { "name": "Weststraße", "klasse": "haupt", "linie": [[-100, 0], [-12, 0]] },
        { "name": "Oststraße", "klasse": "haupt", "linie": [[12, 4], [100, 4]] },
        { "name": "Nordgasse", "klasse": "gasse", "linie": [[0, -12], [0, -60]] }
      ],
      "gebaeude": [ {{(mitMarkt ? """{ "kennung": "Markt", "katalog": "markt", "name": "Markt", "punkt": [0, 0] }""" : "")}} ]
    }
    """;

    /// <summary>
    /// Die Probestadt (Markt im Ursprung auf der Hauptstraße, Werkstätten bei (−30 | −10), (32 | −10) und (32 | 10)) mit weiteren Marktplätzen
    /// auf der Hauptstraße: Partie mit beiden KI-Häusern.
    /// </summary>
    static (Simulation Sim, Konto Spieler, Stadtkarte Karte) Probestadt(params (string Name, double X)[] weitere)
    {
        var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
        foreach (var (name, x) in weitere)
        {
            var g = e.Setzen("markt", x, 0);
            e.Umbenennen(g.Kennung, name);
        }
        var karte = e.ZuStadtkarte();
        var (sim, spieler) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value,
            new NeuePartie(Rivalen: 2), karte: karte);
        return (sim, spieler, karte);
    }

    /// <summary>Die Probestadt ohne ihren Marktplatz (eine Karte ohne Markt: der Ursprung gilt).</summary>
    static Simulation ProbestadtOhneMarkt()
    {
        var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
        e.Loeschen(e.Gebaeude.Single(g => g.Katalog == "markt").Kennung);
        return Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value, new NeuePartie(Rivalen: 2),
            karte: e.ZuStadtkarte()).Simulation;
    }

    static Betrieb Werkstatt(Stadtwirtschaft w, string haus) => w.Betriebe.Single(b => b.Konto.Inhaber == haus && b.InDerStadt);

    /// <summary>
    /// Der Zubringer einer Werkstatt der Probestadt (Datenversion 65, <see cref="Karrenweg"/>): vom Halteplatz am Modell zum Hof und vom Hof gerade auf die
    /// Hauptstraße (sie liegt auf z = 0, die Höfe 3,5 m davor).
    /// </summary>
    static double Zubringer(Stadtwirtschaft w, Betrieb b)
    {
        var (anker, hof) = Karrenweg.Start(w.Bauen.PlatzVon(b.Name)!, b, w.Karte!.Katalog);
        return anker.Abstand(hof) + Math.Abs(hof.Z);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Marktplätze: Greybox, Probestadt und Messstadt haben genau einen, und keine Karrenstraße braucht eine Linie über ihn – die Wege bleiben wie bisher", () =>
        {
            foreach (var kennung in new[] { "greybox", "probestadt", "messstadt" })
            {
                var karte = Stadtkarte.AusDatei(Datenpfad.Karte(kennung));
                Pruefe.Gleich(1, karte.Marktplaetze.Count, kennung);
                Pruefe.Gleich(0, Karrennetz.Marktlinien(karte).Count, kennung);
            }
        }),

        ("Marktplatz als Kreuzung: Karrenstraßen, die am Platz enden, sind über seine Mitte verbunden (eine Gasse nicht); der Markt hängt in der Mitte", () =>
        {
            var karte = Stadtkarte.AusJson(KreuzungJson(), Katalog.Value);
            var linien = Karrennetz.Marktlinien(karte);
            Pruefe.Gleich(2, linien.Count, "Weststraße und Oststraße, nicht die Gasse");
            var netz = Karrennetz.Von(karte);
            var quer = netz.Fahrt(new Kartenort(-100, 0), new Kartenort(100, 4));
            Pruefe.Ungefaehr(88 + 12 + Math.Sqrt(12 * 12 + 4 * 4) + 88, quer.Sum(s => s.Laenge), 1e-9);
            Pruefe.Ungefaehr(38 + 12, netz.Fahrt(new Kartenort(-50, 0), new Kartenort(0, 0)).Sum(s => s.Laenge), 1e-9); // bis zur Mitte des Platzes
            Pruefe.Ungefaehr(Karrennetz.Kosten(quer), quer.Sum(s => s.Laenge), 1e-12); // ohne Wasser kostet eine Fahrt ihre Länge
            // ohne Marktplatz sind die beiden Straßen nicht verbunden
            var ohne = Karrennetz.Von(Stadtkarte.AusJson(KreuzungJson(mitMarkt: false), Katalog.Value));
            Pruefe.Gleich(0, ohne.Fahrt(new Kartenort(-100, 0), new Kartenort(100, 4)).Count);
        }),

        ("Mehrere Marktplätze: Jeder Betrieb fährt zum nächsten (kürzester Weg), bei Gleichstand zum ersten der Karte; alle handeln an einem Markt", () =>
        {
            var (sim, _, karte) = Probestadt(("Westmarkt", -60), ("Ostmarkt", 60));
            var w = sim.Wirtschaft!;
            Pruefe.Gleich("Marktplatz, Westmarkt, Ostmarkt", string.Join(", ", karte.Marktplaetze.Select(m => m.Name)));
            // Spieler (−30 | −10): vom Anschluss an der Hauptstraße (x = −30) 14,5 m bis zum Halt am Marktplatz (x = −15,5: 12 m Pflaster und 3,5 m davor)
            // und 14,5 m bis zum Halt am Westmarkt (x = −44,5) – Gleichstand, also der erste der Karte
            var spieler = w.Betriebe.Single(b => b.Konto == w.Konten[0]);
            Pruefe.Gleich("Marktplatz", w.MarktplatzVon(spieler)!.Name);
            Pruefe.Ungefaehr(Zubringer(w, spieler) + 14.5, w.Fahrten(spieler)!.Value.Hin.Sum(s => s.Laenge), 1e-6);
            // die Werkstätten bei x = 32: 12,5 m bis zum Halt am Ostmarkt (x = 44,5) statt 16,5 m bis zum Halt am Marktplatz – hin und zurück
            foreach (var haus in new[] { "Haus Ahlbeck", "Haus Brendel" })
            {
                var b = Werkstatt(w, haus);
                Pruefe.Gleich("Ostmarkt", w.MarktplatzVon(b)!.Name, haus);
                var (hin, zurueck) = w.Fahrten(b)!.Value;
                Pruefe.Ungefaehr(Zubringer(w, b) + 12.5, hin.Sum(s => s.Laenge), 1e-6);
                Pruefe.Ungefaehr(Zubringer(w, b) + 12.5, zurueck.Sum(s => s.Laenge), 1e-6);
                Pruefe.Gleich(FahrzeitWeglaengeTests.NachFormel(w, b, "Handkarren", true), w.Fahrzeit(b, "Handkarren", true), haus);
            }
            // Ein Stadtmarkt: Was die Karren am Ostmarkt verkaufen, steht im Angebot des einen Markts, an dem auch der Spieler (Marktplatz) handelt.
            var ahlbeck = Werkstatt(w, "Haus Ahlbeck");
            var vorher = w.Markt[ahlbeck.Produkt].SpielerAngebotImJahr;
            for (var i = 0; i < 2 * Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag; i++) sim.Tick();
            Pruefe.Wahr(ahlbeck.Karren.Any(k => k.FahrtenVorjahr > 0), "die Karren am Ostmarkt fahren");
            Pruefe.Wahr(w.Markt[ahlbeck.Produkt].SpielerAngebotImJahr > vorher, $"{ahlbeck.Produkt} vom Ostmarkt im Angebot des Stadtmarkts");
        }),

        ("Mehrere Marktplätze: nach Speichern und Laden dieselben Plätze, Wege und Fahrzeiten (der Spielstand braucht nichts Neues)", () =>
        {
            var (sim, spieler, _) = Probestadt(("Ostmarkt", 60));
            var w = sim.Wirtschaft!;
            var vorher = w.Betriebe.Where(b => w.Fahrten(b) is not null)
                .ToDictionary(b => b.Name, b => (w.MarktplatzVon(b)!.Name, w.Fahrzeit(b, "Handkarren", true), w.Fahrzeit(b, "Ochsenkarren", false)));
            Pruefe.Wahr(vorher.Values.Select(x => x.Item1).Distinct().Count() == 2, "beide Plätze werden angefahren");
            var (geladen, _) = Speicherung.Laden(Speicherung.Speichern(sim, spieler));
            var w2 = geladen.Wirtschaft!;
            Pruefe.Gleich(2, w2.Karte!.Marktplaetze.Count);
            foreach (var b in w2.Betriebe.Where(b => vorher.ContainsKey(b.Name)))
                Pruefe.Gleich(vorher[b.Name], (w2.MarktplatzVon(b)!.Name, w2.Fahrzeit(b, "Handkarren", true), w2.Fahrzeit(b, "Ochsenkarren", false)), b.Name);
        }),

        ("Köln: zwei Plätze an den Enden der Mauer (eine Hausreihe vor dem Tor), einer in Deutz; jede Startwerkstatt fährt zu ihrem Platz – Werkstattwege 20–24 m (Halteplatz bis Halt), für alle Häuser fast gleich", () =>
        {
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
            // Lage nach der Kennung (Entscheidung des Nutzers 31.10.2026): Süden hinter dem Bayentor, Norden hinter dem Kunibertstor (beide innen am
            // Rhein, an der Rheingasse), Deutz rechts des Rheins innerhalb der Deutzer Mauer. Der Platz des Spielers (Süden) steht zuerst.
            Pruefe.Gleich("MarktSued, MarktNord, MarktDeutz", string.Join(", ", karte.Marktplaetze.Select(m => m.Kennung)));
            // Die Namen stehen nur in dieser Zeile (und in Koeln.Maerkte): Tauscht der Nutzer sie, ändert sich hier nur die Zeile
            Pruefe.Gleich("Heumarkt, Alter Markt, Neumarkt", string.Join(", ", karte.Marktplaetze.Select(m => m.Name)));
            var (sued, nord, deutz) = (karte.Marktplaetze[0].Punkt, karte.Marktplaetze[1].Punkt, karte.Marktplaetze[2].Punkt);
            Pruefe.Wahr(sued[1] > 100 && nord[1] < -100 && Math.Abs(sued[0] - 37) < 1 && Math.Abs(nord[0] - 37) < 1, $"Süden {sued[0]} | {sued[1]}, Norden {nord[0]} | {nord[1]}");
            Pruefe.Wahr(deutz[0] > 130 && Math.Abs(deutz[1]) < 40, $"Deutz {deutz[0]} | {deutz[1]}");
            // Zwischen Mauer und Platz eine Hausreihe (Wunsch des Nutzers 1.11.2026; bis dahin stieß der Platz an Wallstraße und Tor): Der äußere
            // Rand des Platzes (24 × 24 m) liegt mindestens 25 m vor dem Tor (z = ±150), und zu beiden Seiten der Rheingasse steht dazwischen ein
            // Einwohnerplatz
            foreach (var (platz, richtung) in new[] { (sued, 1), (nord, -1) })
            {
                var rand = richtung * platz[1] + 12;
                Pruefe.Wahr(150 - rand >= 25, $"Platz bei z = {platz[1]}: {150 - rand:0.#} m vom Tor");
                foreach (var seite in new[] { -1, 1 })
                    Pruefe.Wahr(karte.GebaeudeDerArt(Bausteinart.Einwohnerplatz).Any(g => Math.Sign(g.Punkt[0] - platz[0]) == seite
                        && richtung * g.Punkt[1] is var t && t > rand && t < 150), $"Platz bei z = {platz[1]}: kein Haus zwischen Platz und Mauer auf der Seite {seite}");
            }
            Pruefe.Gleich(MarktlinienKoeln, Karrennetz.Marktlinien(karte).Count);
            var (sim, _) = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos, Schwierigkeit = Schwierigkeit.Schwer },
                Daten.Value, new NeuePartie(Rivalen: 7), karte: karte);
            var w = sim.Wirtschaft!;
            var werkstaetten = w.Betriebe.Where(b => b.InDerStadt && w.Fahrten(b) is { Hin.Count: > 0 }).ToList();
            Pruefe.Gleich(8, werkstaetten.Count);
            // vom Halteplatz am Modell (er liegt je Beruf links oder rechts vom Tor) über den Hof zur Ecke und bis zum Halt vor dem Pflaster
            var laengen = werkstaetten.Select(b => w.Fahrten(b)!.Value.Hin.Sum(s => s.Laenge)).ToList();
            for (var i = 0; i < werkstaetten.Count; i++)
                Pruefe.Wahr(laengen[i] is >= 19.5 and <= 24.5, $"{werkstaetten[i].Name}: {laengen[i]:0.0} m zum {w.MarktplatzVon(werkstaetten[i])!.Name}");
            Pruefe.Wahr(laengen.Max() - laengen.Min() <= 4.5, $"Werkstattwege {laengen.Min():0.0}–{laengen.Max():0.0} m");
            var jePlatz = werkstaetten.GroupBy(b => w.MarktplatzVon(b)!.Kennung).ToDictionary(g => g.Key, g => g.Count());
            Pruefe.Gleich((3, 2, 3), (jePlatz["MarktSued"], jePlatz["MarktNord"], jePlatz["MarktDeutz"]));
            Pruefe.Gleich("MarktSued", w.MarktplatzVon(w.Betriebe.Single(b => b.Konto == w.Konten[0]))!.Kennung); // der Spieler
            // Deutz: Werkstätten und Umlandbetriebe rechts des Rheins (x > 100) fahren zum Deutzer Platz, die links des Rheins zu einem Platz links –
            // keiner über den Rhein (Rheinbrücke oder Fähre): Jeder Punkt des Weges bleibt auf seiner Seite (das linke Ufer liegt bei x = 60, das
            // rechte bei x ≥ 120); über den Duffesbach darf ein Karren (Holzbrücke an der Bonner Straße)
            var mitWeg = w.Betriebe.Where(b => w.Fahrten(b) is { Hin.Count: > 0 }).ToList();
            foreach (var b in mitWeg)
            {
                var verlauf = w.Karrenverlauf(b)!;
                var rechts = verlauf[0].X > 100;
                Pruefe.Gleich(rechts, w.MarktplatzVon(b)!.Kennung == "MarktDeutz", $"{b.Name}: {w.MarktplatzVon(b)!.Name}");
                Pruefe.Wahr(verlauf.All(p => rechts ? p.X > 100 : p.X < 62) && w.Fahrten(b)!.Value.Hin.All(s => s.Art != Fahrbahn.Faehre), $"{b.Name}: über den Rhein");
            }
            Pruefe.Wahr(mitWeg.Count(b => w.MarktplatzVon(b)!.Kennung == "MarktDeutz") >= 3 + 5, "drei Werkstätten und mindestens fünf Umlandbetriebe in Deutz");
            // Umland: keine Fahrt an der Höchstdauer (Ochsenkarren; bis zum 31.10.2026 lagen 26 von 42 Fahrten dort)
            foreach (var b in mitWeg.Where(b => !b.InDerStadt))
                Pruefe.Wahr(w.Fahrzeit(b, "Ochsenkarren", true) < w.Daten.Logistik.Weglaenge!.HoechstMinuten, $"{b.Name}: {w.Fahrzeit(b, "Ochsenkarren", true)} Minuten");
        }),

        ("Mehrere Marktplätze: Ein alter Spielstand (Greybox, Herbst 1406) lädt mit seiner Karte – ein Marktplatz, jeder Betrieb mit Weg fährt dorthin", () =>
        {
            var datei = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Datenpfad.Wirtschaft())))!,
                "tests", "Reichsgilde.Core.Tests", "Spielstaende", "format2-greybox-herbst1406.json.gz");
            using var gz = new System.IO.Compression.GZipStream(File.OpenRead(datei), System.IO.Compression.CompressionMode.Decompress);
            var (sim, _) = Speicherung.Laden(new StreamReader(gz).ReadToEnd());
            var w = sim.Wirtschaft!;
            Pruefe.Gleich(1, w.Karte!.Marktplaetze.Count);
            Pruefe.Gleich(0, Karrennetz.Marktlinien(w.Karte).Count);
            var mitWeg = w.Betriebe.Where(b => w.Fahrten(b) is { Hin.Count: > 0 }).ToList();
            Pruefe.Wahr(mitWeg.Count >= 4, $"{mitWeg.Count} Betriebe mit Weg");
            Pruefe.Wahr(mitWeg.All(b => ReferenceEquals(w.MarktplatzVon(b), w.Karte.Marktplaetze[0])), "alle zum einen Marktplatz");
            for (var i = 0; i < 2 * Reichsgilde.Core.Zeit.Spielzeit.MinutenProTag; i++) sim.Tick(); // läuft weiter
        }),

        ("Mehrere Marktplätze: Wahl nach den Kosten der Wegwahl – Furt zählt zehnfach, Fähre 300 m dazu; ohne Marktplatz gilt der Ursprung", () =>
        {
            Pruefe.Ungefaehr(10 * 10 + (5 + 300) + 7, Karrennetz.Kosten(new[]
            {
                new Fahrtstueck(10, 0, Fahrbahn.Furt), new Fahrtstueck(5, 0, Fahrbahn.Faehre), new Fahrtstueck(7, 0), new Fahrtstueck(0, 0, Fahrbahn.Bruecke),
            }), 1e-12);
            // Gasse und Fußweg (Netz aller Wege, Datenversion 65): je Meter zwei- bzw. viermal so teuer, auch in einer Furt; die Fähre nicht
            Pruefe.Ungefaehr(2 * 6 + 4 * 3 + 10 * 10 * 2 + (5 + 300), Karrennetz.Kosten(new[]
            {
                new Fahrtstueck(6, 0) { Kostenfaktor = Karrennetz.Wegkosten(Wegklasse.Gasse) }, new Fahrtstueck(3, 0) { Kostenfaktor = Karrennetz.Wegkosten(Wegklasse.Fussweg) },
                new Fahrtstueck(10, 0, Fahrbahn.Furt) { Kostenfaktor = 2 }, new Fahrtstueck(5, 0, Fahrbahn.Faehre),
            }), 1e-12);
            Pruefe.Gleich((1.0, 1.0, 2.0, 4.0, 4.0), (Karrennetz.Wegkosten(Wegklasse.Haupt), Karrennetz.Wegkosten(Wegklasse.Feldweg), Karrennetz.Wegkosten(Wegklasse.Gasse),
                Karrennetz.Wegkosten(Wegklasse.Fussweg), Karrennetz.Wegkosten(Wegklasse.Leinpfad)));
            var w = ProbestadtOhneMarkt().Wirtschaft!;
            Pruefe.Gleich(0, w.Karte!.Marktplaetze.Count);
            var spieler = w.Betriebe.Single(b => b.Konto == w.Konten[0]);
            Pruefe.Gleich(null, w.MarktplatzVon(spieler));
            // zum Ursprung auf der Hauptstraße, wie bis Oktober 2026 – ohne Platz kein Halt davor (mit dem Zubringer vom Halteplatz)
            Pruefe.Ungefaehr(Zubringer(w, spieler) + 30, w.Fahrten(spieler)!.Value.Hin.Sum(s => s.Laenge), 1e-6);
        }),

        ("Marktplatz als Kreuzung nur mit der Fahrzeit nach Weglänge: Ein alter Kölner Stand (ein Markt, Daten vor Version 65) fährt auf seinem alten Netz", () =>
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Datenpfad.Karte("koeln")))!;
            var gebaeude = json["gebaeude"]!.AsArray();
            foreach (var g in gebaeude.Where(g => (string?)g!["kennung"] is "MarktNord" or "MarktDeutz").ToList()) gebaeude.Remove(g); // bleibt der Heumarkt
            // Eine Straße, die den Platz nur berührt: Der Malzbüchel endet hier am Westrand des Heumarkts statt in seiner Mitte (in der Karte führen
            // seit 1.11.2026 alle Straßen durch die Mitten der Plätze, dann braucht es keine Linie über das Pflaster)
            var malzbuechel = json["wege"]!.AsArray().Single(w => (string?)w!["name"] == "Malzbüchel")!["linie"]!.AsArray();
            var heumarkt = gebaeude.Single(g => (string?)g!["kennung"] == "MarktSued")!["punkt"]!.AsArray();
            malzbuechel[^1] = new System.Text.Json.Nodes.JsonArray((double)heumarkt[0]! - 12, (double)heumarkt[1]!);
            var karte = Stadtkarte.AusJson(json.ToJsonString(), Katalog.Value);
            Pruefe.Gleich(1, karte.Marktplaetze.Count);
            var ohneLinien = Karrennetz.Von(karte, marktlinien: false).Groesse;
            Pruefe.Wahr(Karrennetz.Von(karte).Groesse != ohneLinien, "am Heumarkt gibt es Linien über den Platz");
            var daten = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Datenpfad.Wirtschaft()))!;
            daten["logistik"]!.AsObject().Remove("weglaenge");
            var alt = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Wirtschaftsdaten.AusJson(daten.ToJsonString()),
                new NeuePartie(Rivalen: 7), karte: karte).Simulation.Wirtschaft!;
            Pruefe.Gleich(ohneLinien, alt.Karrennetz!.Groesse);
            var neu = Partieaufbau.Erstellen(Partieeinstellungen.Standard with { Siegbedingung = Siegbedingung.Endlos }, Daten.Value, new NeuePartie(Rivalen: 7),
                karte: Stadtkarte.AusJson(json.ToJsonString(), Katalog.Value)).Simulation.Wirtschaft!;
            Pruefe.Wahr(neu.Karrennetz!.Groesse != ohneLinien, "mit Datenversion 65 ist der Heumarkt eine Kreuzung");
        }),

        ("Kartenprüfung: ein Marktplatz, den keine Karrenstraße berührt, gibt einen Hinweis „markt“; die Plätze der Karten nicht", () =>
        {
            foreach (var kennung in new[] { "greybox", "probestadt", "messstadt", "koeln" })
                Pruefe.Wahr(!Kartenentwurf.AusJson(File.ReadAllText(Datenpfad.Karte(kennung)), Katalog.Value).Befunde.Any(b => b.Regel == "markt"), kennung);
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            var abseits = e.Setzen("markt", -130, -30); // im Feld zwischen den Feldwegen, kein Weg berührt ihn
            var befunde = e.Befunde.Where(b => b.Regel == "markt").ToList();
            Pruefe.Gleich(1, befunde.Count);
            Pruefe.Wahr(befunde[0].Schwere == Schwere.Hinweis && befunde[0].Gebaeude.Contains(abseits.Kennung), befunde[0].Text);
        }),

        ("Mehrere Marktplätze: Abstand zum nächsten (Standortwahl der KI) – ein Markt im Ursprung genau x² + z², ohne Markt der Ursprung", () =>
        {
            var greybox = Stadtkarte.AusDatei(Datenpfad.Karte("greybox"));
            foreach (var (x, z) in new[] { (3.0, 4.0), (-17.25, 31.5), (0.1, -0.3) })
                Pruefe.Gleich(x * x + z * z, greybox.MarktAbstandQuadrat(x, z), $"{x} | {z}");
            var (_, _, karte) = Probestadt(("Ostmarkt", 60));
            Pruefe.Ungefaehr(100, karte.MarktAbstandQuadrat(50, 0), 1e-12); // 10 m zum Ostmarkt
            Pruefe.Ungefaehr(400, karte.MarktAbstandQuadrat(-20, 0), 1e-12); // 20 m zur Mitte
            var ohne = Stadtkarte.AusJson(KreuzungJson(mitMarkt: false), Katalog.Value);
            Pruefe.Gleich(25.0, ohne.MarktAbstandQuadrat(3, 4));
            Pruefe.Gleich(0, ohne.Marktplaetze.Count);
        }),
    };
}
