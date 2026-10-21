using System.Diagnostics;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Kartenwerkstatt (Level-Editor), Schritt 1 (PR #168): der bearbeitbare Kartenentwurf, Befehle mit Rückgängig/Wiederholen,
/// die Kartenregeln in C# (dieselben Befunde wie das Python-Werkzeug) und der Schreiber (Byte für Byte wie das Werkzeug).
/// </summary>
static class KartenwerkstattTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static string Text(string kennung) => File.ReadAllText(Datenpfad.Karte(kennung));
    static Kartenentwurf Oeffnen(string kennung) => Kartenentwurf.AusJson(Text(kennung), Katalog.Value);

    /// <summary>Die Greybox, abgewandelt (für Fehlerfälle).</summary>
    static Kartenentwurf GreyboxMit(Action<JsonObject> aenderung)
    {
        var json = JsonNode.Parse(Text("greybox"))!.AsObject();
        aenderung(json);
        return Kartenentwurf.AusJson(json.ToJsonString(), Katalog.Value);
    }

    static string Liste(IEnumerable<Befund> befunde) => string.Join("\n  ", befunde.Select(b => b.ToString()));

    static List<Befund> Fehler(Kartenentwurf e) => e.Befunde.Where(b => b.Schwere == Schwere.Fehler).ToList();

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Kartenwerkstatt: Greybox und Köln öffnen und wieder schreiben ergibt Byte für Byte dieselbe Datei wie das Werkzeug", () =>
        {
            foreach (var kennung in new[] { "greybox", "koeln" })
            {
                var original = Text(kennung);
                var geschrieben = Oeffnen(kennung).ZuJson();
                if (geschrieben != original)
                {
                    var (a, b) = (original.Split('\n'), geschrieben.Split('\n'));
                    var i = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(n => a[n] != b[n], Math.Min(a.Length, b.Length));
                    throw new Exception($"{kennung}: Zeile {i + 1} weicht ab:\n  Datei:    {(i < a.Length ? a[i] : "(Ende)")}\n  Werkstatt: {(i < b.Length ? b[i] : "(Ende)")}");
                }
            }
        }),

        ("Kartenwerkstatt: Greybox ohne Fehler, Hinweise wie greybox.py (3 Felsberg-Ränder, 4 Wege über Umlandbetrieben)", () =>
        {
            var e = Oeffnen("greybox");
            Pruefe.Wahr(!e.HatFehler, "Fehler in der Greybox:\n  " + Liste(Fehler(e)));
            var hinweise = e.Befunde.Where(b => b.Schwere == Schwere.Hinweis).ToList();
            var erwartet = new[]
            {
                ("ueberlappung", "Bauernhof_an_der_Muehle", "Berg_Grube_Eisenbart"),
                ("ueberlappung", "Duftgarten_am_Lavendelhang", "Berg_Grube_Eisenbart"),
                ("ueberlappung", "Duftgarten_am_Lavendelhang", "Berg_Grube_am_Silberberg"),
                ("weg-ueber-umland", "Kraeutergarten_am_Bach", "Weg zu Klosterhof am Osthang"),
                ("weg-ueber-umland", "Wachturm_an_der_Landstrasse", "Weg zum Abzweig Bauernhof an der Mühle"),
                ("weg-ueber-umland", "Grube_am_Silberberg", "Weg zu Kräutergarten am Bach"),
                ("weg-ueber-umland", "Grube_am_Silberberg", "Weg zum Abzweig Klosterhof am Osthang"),
            };
            Pruefe.Gleich(erwartet.Length, hinweise.Count, "Hinweise:\n  " + Liste(hinweise));
            foreach (var (regel, gebaeude, weiteres) in erwartet)
                Pruefe.Wahr(hinweise.Any(h => h.Regel == regel && h.Gebaeude.Contains(gebaeude) && (h.Gebaeude.Contains(weiteres) || h.Text.Contains(weiteres))),
                    $"Hinweis fehlt: {regel} {gebaeude} / {weiteres}");
        }),

        ("Kartenwerkstatt: Köln ohne Fehler (die Kapazität prüft weiter das Werkzeug)", () =>
        {
            var e = Oeffnen("koeln");
            Pruefe.Wahr(!e.HatFehler, "Fehler in Köln:\n  " + Liste(Fehler(e)));
            Pruefe.Gleich(Pruefregeln.Koeln, e.Regeln);
        }),
        ("Kartenwerkstatt: Setzen, Verschieben, Drehen, Umbenennen, Löschen – Rückgängig und Wiederholen führen genau zurück", () =>
        {
            var e = Oeffnen("greybox");
            var vorher = e.ZuJson();
            var meldungen = new List<Kartenaenderung>();
            e.Geaendert += meldungen.Add;

            var g = e.Setzen("werkstattplatz", 25.4, 29.04, 0);
            Pruefe.Gleich("Werkstattplatz1", g.Kennung);
            Pruefe.Gleich("Werkstattplatz", g.Name);
            Pruefe.Gleich((25.4, 29.05), (g.X, g.Z), "Punkt auf 5 cm wie in der Datei");
            Pruefe.Gleich(Aenderungsart.Gesetzt, meldungen[^1].Art);
            Pruefe.Wahr(e.Geschichte.IstGeaendert, "nach dem Setzen geändert");
            var zweiter = e.Setzen("werkstattplatz", 60, 60);
            Pruefe.Gleich("Werkstattplatz2", zweiter.Kennung);
            Pruefe.Gleich("Werkstattplatz 2", zweiter.Name);

            e.Verschieben(g.Kennung, 20, 30);
            e.Drehen(g.Kennung, -90);
            Pruefe.Gleich(270.0, g.Drehung, "Drehung zwischen 0 und 360");
            e.Umbenennen(g.Kennung, "Schmiede am Südring");
            e.BetriebSetzen(g.Kennung, "Schmiede am Südring");
            e.Loeschen(zweiter.Kennung);
            Pruefe.Gleich(Aenderungsart.Geloescht, meldungen[^1].Art);
            Pruefe.Gleich(7, e.Geschichte.Schritte);
            Pruefe.Gleich("Werkstattplatz 2 gelöscht", e.Geschichte.RueckgaengigText);

            // Ein Befehl ohne Wirkung ist kein Schritt.
            e.Verschieben(g.Kennung, 20, 30);
            Pruefe.Gleich(7, e.Geschichte.Schritte);

            var mitte = e.ZuJson();
            while (e.Rueckgaengig()) { }
            Pruefe.Gleich(vorher, e.ZuJson(), "alles zurückgenommen = Datei wie vorher");
            Pruefe.Wahr(!e.Geschichte.IstGeaendert, "zurück am geöffneten Stand");
            Pruefe.Gleich(7, Enumerable.Range(0, 7).Count(_ => e.Wiederholen()));
            Pruefe.Gleich(mitte, e.ZuJson(), "alles wiederholt = derselbe Stand");
            Pruefe.Gleich("Schmiede am Südring", e.GebaeudeMit("Werkstattplatz1")!.Name);
            Pruefe.Wahr(ReferenceEquals(g, e.GebaeudeMit("Werkstattplatz1")), "das Gebäude bleibt dasselbe Objekt");

            // Gespeichert merken; ein neuer Befehl nach Rückgängig verwirft das Wiederholbare.
            e.Geschichte.Gespeichert();
            Pruefe.Wahr(!e.Geschichte.IstGeaendert, "gespeichert");
            e.Rueckgaengig();
            Pruefe.Wahr(e.Geschichte.IstGeaendert, "nach Rückgängig geändert");
            e.Drehen(g.Kennung, 180);
            Pruefe.Wahr(!e.Geschichte.KannWiederholen, "nach neuem Befehl nichts zu wiederholen");
            Pruefe.Wahr(e.Geschichte.IstGeaendert, "der gespeicherte Stand ist nicht mehr erreichbar");
        }),

        ("Kartenwerkstatt: Ein gelöschtes Gebäude kommt mit Rückgängig an seine Stelle der Reihenfolge zurück (Einwohnerplätze)", () =>
        {
            var e = Oeffnen("greybox");
            var kennungen = e.Gebaeude.Select(g => g.Kennung).ToList();
            e.Loeschen("Ring17");
            e.Loeschen("Markt");
            e.Rueckgaengig();
            e.Rueckgaengig();
            Pruefe.Gleich(string.Join(",", kennungen), string.Join(",", e.Gebaeude.Select(g => g.Kennung)));
        }),

        ("Kartenwerkstatt: Die Geschichte hält höchstens 200 Schritte, ältere fallen weg", () =>
        {
            var e = Oeffnen("greybox");
            for (var i = 1; i <= 250; i++) e.Drehen("Markt", i);
            Pruefe.Gleich(Kartengeschichte.Grenze, e.Geschichte.Schritte);
            while (e.Rueckgaengig()) { }
            Pruefe.Gleich(50.0, e.GebaeudeMit("Markt")!.Drehung, "der Stand nach dem 50. Schritt");
            Pruefe.Wahr(e.Geschichte.IstGeaendert, "der Stand beim Öffnen ist nicht mehr erreichbar");
        }),

        ("Kartenwerkstatt: Raster-Hilfe – 1 m, Drehung in 15°-Schritten zwischen 0 und 360, Punkte in der Datei auf 5 cm", () =>
        {
            Pruefe.Gleich(13.0, Kartenraster.Runden(12.6));
            Pruefe.Gleich(-4.0, Kartenraster.Runden(-4.4));
            Pruefe.Gleich(30.0, Kartenraster.DrehungRunden(37));
            Pruefe.Gleich(345.0, Kartenraster.DrehungRunden(-14));
            Pruefe.Gleich(0.0, Kartenraster.DrehungRunden(359));
            Pruefe.Gleich(12.35, Kartenraster.Punkt(12.3456));
            Pruefe.Gleich(0.0, Kartenraster.Punkt(-0.01));
            Pruefe.Gleich("0", Kartenschreiber.Zahl(Kartenraster.Punkt(-0.01)), "keine „-0“ in der Datei");
            Pruefe.Gleich(171.5, Kartenraster.Drehung(171.54));
        }),

        ("Kartenwerkstatt: Prüfung – gedrehte Grundflächen überlappen, Felsberg nur Hinweis, Kante an Kante erlaubt", () =>
        {
            var e = Oeffnen("greybox");
            // Ein Kulissenhaus schräg in die Ecke einer Werkstatt: Die umschließenden Rechtecke würden schon weiter weg überlappen,
            // die gedrehten Flächen erst hier.
            var giesserei = e.GebaeudeMit("Giesserei_Eisenbart")!; // 16 × 14 um (22 | 13): x 14 … 30, z 6 … 20
            var k = e.Katalog.Finde("kulisse_buergerhaus")!.Groesse!;
            var halbeDiagonale = Math.Sqrt(k[0] * k[0] + k[1] * k[1]) / 2;
            var frei = Kartenpruefung.PruefeStelle(e, "kulisse_buergerhaus", 30 + halbeDiagonale * 0.75, 20 + halbeDiagonale * 0.75, 45);
            Pruefe.Wahr(!frei.Any(b => b.Regel == "ueberlappung"), "schräg vor der Ecke: frei\n  " + Liste(frei));
            var drin = Kartenpruefung.PruefeStelle(e, "kulisse_buergerhaus", 30 + halbeDiagonale * 0.5, 20 + halbeDiagonale * 0.5, 45);
            Pruefe.Wahr(drin.Any(b => b.Regel == "ueberlappung" && b.Schwere == Schwere.Fehler && b.Gebaeude.Contains(giesserei.Kennung)), "in die Ecke: überlappt");

            // Kante an Kante (16 m nebeneinander) überlappt nicht.
            var kante = Kartenpruefung.PruefeStelle(e, "werkstattplatz", giesserei.X + 16, giesserei.Z, 0);
            Pruefe.Wahr(!kante.Any(b => b.Regel == "ueberlappung" && b.Gebaeude.Contains(giesserei.Kennung)), "Kante an Kante ist erlaubt");
            Pruefe.Wahr(kante.Any(b => b.Regel == "werkstattabstand"), "aber zwei Werkstätten brauchen 2 m Abstand");

            // Die volle Prüfung nach dem Verschieben findet dasselbe.
            var r = e.Setzen("kulisse_buergerhaus", 30 + halbeDiagonale * 0.5, 20 + halbeDiagonale * 0.5, 45);
            Pruefe.Wahr(e.FehlerhafteGebaeude.Contains(r.Kennung) && e.FehlerhafteGebaeude.Contains(giesserei.Kennung), "beide rot");
            e.Rueckgaengig();
            Pruefe.Wahr(!e.HatFehler, "nach Rückgängig wieder ohne Fehler");
        }),

        ("Kartenwerkstatt: Prüfung – unbekannter Baustein, Wohnhausplatz schräg, Umlandbetrieb ohne Betrieb, Kennung und Betrieb doppelt", () =>
        {
            var e = GreyboxMit(k =>
            {
                var g = k["gebaeude"]!.AsArray();
                g.Add(JsonNode.Parse("""{"kennung": "Schloss1", "katalog": "schloss", "name": "Schloss", "punkt": [300, 300], "drehung": 0}"""));
                g.Add(JsonNode.Parse("""{"kennung": "Ring1", "katalog": "einwohnerplatz_ring", "name": "Doppelt", "punkt": [400, 300], "drehung": 0}"""));
                g.Add(JsonNode.Parse("""{"kennung": "GrubeX", "katalog": "grube", "name": "Grube X", "punkt": [300, 400], "drehung": 0, "betrieb": "Gießerei Eisenbart"}"""));
                g.Add(JsonNode.Parse("""{"kennung": "GrubeY", "katalog": "grube", "name": "Grube Y", "punkt": [400, 400], "drehung": 0}"""));
            });
            var f = Fehler(e);
            Pruefe.Wahr(f.Any(b => b.Regel == "katalog" && b.Text.Contains("„schloss“") && b.Gebaeude.Contains("Schloss1")), "unbekannter Baustein\n  " + Liste(f));
            Pruefe.Wahr(f.Any(b => b.Regel == "kennung" && b.Text.Contains("„Ring1“")), "Kennung doppelt");
            Pruefe.Wahr(f.Any(b => b.Regel == "betrieb" && b.Gebaeude.Contains("GrubeX") && b.Gebaeude.Contains("Giesserei_Eisenbart")), "Betrieb doppelt");
            Pruefe.Wahr(f.Any(b => b.Regel == "betrieb" && b.Gebaeude.SequenceEqual(new[] { "GrubeY" })), "Umlandbetrieb ohne Betrieb");

            var schraeg = Kartenpruefung.PruefeStelle(e, "wohnhausplatz_ring", 300, 0, 30);
            Pruefe.Wahr(schraeg.Any(b => b.Regel == "rechtwinklig"), "Wohnhausplatz schräg");
            Pruefe.Wahr(!Kartenpruefung.PruefeStelle(e, "wohnhausplatz_ring", 300, 0, 270).Any(b => b.Regel == "rechtwinklig"), "270° ist rechtwinklig");
        }),

        ("Kartenwerkstatt: Prüfung – Werkstatthof ohne Weg, Eingang eines Wohnhausplatzes ohne Weg, Werkstatt auf der Mauer", () =>
        {
            var e = Oeffnen("greybox");
            var weit = Kartenpruefung.PruefeStelle(e, "werkstattplatz", -150, 120, 0);
            Pruefe.Wahr(weit.Any(b => b.Regel == "hof" && b.Ort is { } o && Math.Abs(o.Z - (120 + 6.5)) < 1e-9), "Hof weit weg vom Weg, Ort = Hof\n  " + Liste(weit));
            // Nördlich an der Weststraße (z = 6): Der Hof liegt 6,5 m vor der Mitte, also bei z = 4, 2 m neben der Straßenmitte.
            var nah = Kartenpruefung.PruefeStelle(e, "werkstattplatz", -75, -2.5, 0);
            Pruefe.Wahr(!nah.Any(b => b.Regel == "hof"), "Hof an der Weststraße\n  " + Liste(nah));
            Pruefe.Wahr(Kartenpruefung.PruefeStelle(e, "wohnhausplatz_ring", -150, 120, 0).Any(b => b.Regel == "eingang"), "Eingang ohne Weg");
            Pruefe.Wahr(Kartenpruefung.PruefeStelle(e, "werkstattplatz", 50, 20, 0).Any(b => b.Regel == "mauer"), "Werkstatt auf der Mauer");
            // Greybox: jede Werkstatt hat ihre Gasse am Hof und zeigt nach Süden
            Pruefe.Wahr(nah.Any(b => b.Regel == "gasse"), "Greybox: Gasse am Hof fehlt");
            var gedreht = Kartenpruefung.PruefeStelle(e, "werkstattplatz", 22, 13, 90, kennung: "Giesserei_Eisenbart");
            Pruefe.Wahr(gedreht.Any(b => b.Regel == "werkstattnorm"), "Greybox: Werkstatt gedreht");
            Pruefe.Wahr(!Kartenpruefung.PruefeStelle(e, "werkstattplatz", 22, 13, 0, kennung: "Giesserei_Eisenbart").Any(b => b.Schwere == Schwere.Fehler),
                "an der eigenen Stelle ohne Fehler (zählt nicht als eigener Nachbar)");
        }),

        ("Kartenwerkstatt: Prüfung – Mauer, Tor, Weg ohne Tor, Bauzone fern der Straße, Wegenetz zerfällt, Startbelegung", () =>
        {
            var e = GreyboxMit(k =>
            {
                k["mauern"]![0]!["geschlossen"] = false;
                k["mauern"]![0]!["tore"]![0]!["punkt"] = new JsonArray(55, 0);
                k["wege"]!.AsArray().Add(JsonNode.Parse("""{"name": "Schleichweg", "klasse": "gasse", "linie": [[45, 30], [60, 30]], "gefahr": 1, "breite": 2}"""));
                k["wege"]!.AsArray().Add(JsonNode.Parse("""{"name": "Inselweg", "klasse": "land", "linie": [[300, 300], [320, 300]], "gefahr": 1, "breite": 4}"""));
                k["bauzonen"]!.AsArray().Add(JsonNode.Parse("""{"kennung": "X1", "art": "vorstadt", "flaeche": [[200, 200], [210, 200], [210, 210], [200, 210]]}"""));
                k["start"]!["haeuser"]![0]!["wohnhaus"] = "Ring1";
                k["start"]!["haeuser"]![1]!["betriebe"]![0]!["platz"] = "Giesserei_Eisenbart";
            });
            var f = Fehler(e);
            var text = Liste(f);
            Pruefe.Wahr(f.Any(b => b.Regel == "mauer" && b.Text.Contains("geschlossen")), "Greybox-Mauer muss geschlossen sein\n  " + text);
            Pruefe.Wahr(f.Any(b => b.Regel == "tor" && b.Text.Contains("Osttor")), "Tor neben der Mauer");
            Pruefe.Wahr(f.Any(b => b.Regel == "tor" && b.Text.Contains("Schleichweg") && b.Ort is { } o && Math.Abs(o.X - 50) < 1e-9), "Weg kreuzt ohne Tor");
            Pruefe.Wahr(f.Any(b => b.Regel == "zone-anschluss" && b.Text.Contains("X1")), "Bauzone fern der Karrenstraße");
            Pruefe.Wahr(f.Any(b => b.Regel == "wegenetz" && b.Text.Contains("Inselweg")), "Wegenetz zerfällt");
            Pruefe.Wahr(f.Any(b => b.Regel == "start" && b.Text.Contains("„Ring1“ ist kein Wohnhausplatz") && b.Gebaeude.Contains("Ring1")), "Wohnhaus ist ein Einwohnerplatz");
            Pruefe.Wahr(f.Any(b => b.Regel == "start" && b.Text.Contains("„Giesserei_Eisenbart“ ist mehrfach vergeben")), "Platz doppelt");
        }),

        ("Kartenwerkstatt: Eine Karte ohne Fehler liest auch der Kern (Stadtkarte) – mit einem neuen Gebäude", () =>
        {
            var e = Oeffnen("greybox");
            var g = e.Setzen("kulisse_buergerhaus", -150, -40, 0);
            Pruefe.Wahr(!e.FehlerhafteGebaeude.Contains(g.Kennung), "das neue Haus ist frei\n  " + Liste(Fehler(e)));
            var karte = e.ZuStadtkarte();
            Pruefe.Gleich(e.Gebaeude.Count, karte.Gebaeude.Count);
            var neu = karte.GebaeudeMit(g.Kennung)!;
            Pruefe.Gleich(-150.0, neu.Punkt[0]);
            Pruefe.Gleich(Bausteinart.Kulisse, neu.Art);
        }),

        ("Kartenwerkstatt: Neue leere Karte – Kennung, Name, Größe; Gebäude setzen, schreiben, wieder öffnen", () =>
        {
            var e = Kartenentwurf.Neu("probestadt", "Probestadt", 400, 300, Katalog.Value);
            Pruefe.Gleich(Pruefregeln.Standard, e.Regeln);
            Pruefe.Gleich((400.0, 300.0), e.Groesse);
            Pruefe.Gleich((-200.0, -150.0, 200.0, 150.0), e.Grenzen());
            // Einziger Fehler einer leeren Karte: Ihr fehlt die Startbelegung (seit Schritt 3 – ohne sie hätte der Spieler keinen Startplatz).
            Pruefe.Wahr(Fehler(e) is [{ Regel: "start-fehlt" }], "leere Karte nur ohne Startbelegung:\n  " + Liste(e.Befunde));
            e.Setzen("markt", 0, 0);
            var w = e.Setzen("werkstattplatz", 20, 0, 90);
            Pruefe.Wahr(e.FehlerhafteGebaeude.Contains(w.Kennung), "Werkstatt ohne Weg ist ein Fehler");
            var json = e.ZuJson();
            Pruefe.Wahr(json.StartsWith("{\n \"format\": 2,\n \"kennung\": \"probestadt\",\n \"stadt\": \"Probestadt\","), json[..120]);
            Pruefe.Wahr(json.Contains("\"groesse\": [400, 300]") && !json.Contains("\"natur\"") && !json.Contains("\"start\""), json);
            var wieder = Kartenentwurf.AusJson(json, Katalog.Value);
            Pruefe.Gleich(json, wieder.ZuJson());
            Pruefe.Gleich(90.0, wieder.GebaeudeMit(w.Kennung)!.Drehung);
            // Der Kern liest sie (ohne Start: die Ersatz-Belegung).
            Pruefe.Gleich(2, Stadtkarte.AusJson(json, Katalog.Value).Gebaeude.Count);
        }),

        ("Kartenwerkstatt: Unbekannte Felder eines Gebäudes bleiben beim Schreiben erhalten; kaputtes JSON meldet sich verständlich", () =>
        {
            var e = GreyboxMit(k => k["gebaeude"]![0]!["notiz"] = "später verschieben");
            e.Verschieben("Markt", 1, 0);
            Pruefe.Wahr(e.ZuJson().Contains("\"kennung\": \"Markt\", \"katalog\": \"markt\", \"name\": \"Marktplatz\", \"punkt\": [1, 0], \"drehung\": 0, \"notiz\": \"später verschieben\""),
                "notiz bleibt hinten am Gebäude");
            Pruefe.WirftMitMeldung(() => Kartenentwurf.AusJson("{ \"gebaeude\": [ ", Katalog.Value), "Kein gültiges JSON");
            Pruefe.WirftMitMeldung(() => Kartenentwurf.AusJson("""{ "gebaeude": [ { "kennung": "A", "punkt": "hier" } ] }""", Katalog.Value), "„punkt“");
        }),

        ("Kartenwerkstatt: Die volle Prüfung der Greybox braucht unter 20 ms, die Prüfung einer Stelle unter 2 ms (Release, Median nach dem Aufwärmen)", () =>
        {
            // Gemessen wird der Median nach dem Aufwärmen: Die ersten Läufe übersetzt der JIT noch (im Spiel einmal beim Öffnen),
            // und andere Prozesse auf dem Rechner sollen einzelne Läufe nicht zum Ausreißer machen.
            var e = Oeffnen("greybox");
            static double Median(Action a)
            {
                for (var i = 0; i < 50; i++) a();
                var zeiten = new List<double>();
                for (var i = 0; i < 21; i++)
                {
                    var uhr = Stopwatch.StartNew();
                    a();
                    zeiten.Add(uhr.Elapsed.TotalMilliseconds);
                }
                return zeiten.Order().ElementAt(10);
            }
            var voll = Median(() => Kartenpruefung.Pruefen(e));
            var stelle = Median(() => Kartenpruefung.PruefeStelle(e, "werkstattplatz", 22.5, 13, 0, kennung: "Giesserei_Eisenbart"));
            Console.WriteLine($"          (volle Prüfung {voll:0.0} ms, eine Stelle {stelle:0.00} ms)");
            Pruefe.Wahr(voll < 20, $"volle Prüfung {voll:0.0} ms");
            Pruefe.Wahr(stelle < 2, $"eine Stelle {stelle:0.00} ms");
        }),
    };
}
