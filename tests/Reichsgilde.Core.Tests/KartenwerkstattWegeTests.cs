using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

/// <summary>
/// Kartenwerkstatt, Schritt 2 (PR #170): Wege mit Kurven und Kreuzungen, Bauzonen und Naturflächen als Vielecke, einzelne Pflanzen und
/// der Pinsel – alles mit Rückgängig; die Natur-Verteilung im Kern; die Prüfung der neuen Teile.
/// </summary>
static class KartenwerkstattWegeTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));

    static string Text(string kennung) => File.ReadAllText(Datenpfad.Karte(kennung));
    static Kartenentwurf Oeffnen(string kennung) => Kartenentwurf.AusJson(Text(kennung), Katalog.Value);

    static Kartenentwurf GreyboxMit(Action<JsonObject> aenderung)
    {
        var json = JsonNode.Parse(Text("greybox"))!.AsObject();
        aenderung(json);
        return Kartenentwurf.AusJson(json.ToJsonString(), Katalog.Value);
    }

    static string Liste(IEnumerable<Befund> befunde) => string.Join("\n  ", befunde.Select(b => b.ToString()));
    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Eine Pflanze ohne den Baustein (der hängt am Katalog, mit dem die Karte gelesen wurde).</summary>
    static (string, double, double, double, double, int) Schluessel(Pflanzenlage p) => (p.Art.Kennung, p.X, p.Z, p.Drehung, p.Massstab, p.Variante);

    /// <summary>Median der Laufzeit nach dem Aufwärmen (ms).</summary>
    static double Median(Action a)
    {
        for (var i = 0; i < 20; i++) a();
        var zeiten = new List<double>();
        for (var i = 0; i < 21; i++)
        {
            var uhr = Stopwatch.StartNew();
            a();
            zeiten.Add(uhr.Elapsed.TotalMilliseconds);
        }
        return zeiten.Order().ElementAt(10);
    }

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        // ── Kurve ────────────────────────────────────────────────────────────

        ("Wegkurve: Eine glatte Kurve geht durch alle Stützpunkte, Punkte höchstens 1 m auseinander, Länge plausibel", () =>
        {
            var stuetz = new List<double[]> { new double[] { 0, 0 }, new double[] { 20, 10 }, new double[] { 40, 0 }, new double[] { 60, 15 } };
            var v = Wegkurve.Verlauf(stuetz, glatt: true);
            foreach (var p in stuetz)
                Pruefe.Wahr(v.Punkte.Any(q => q[0] == p[0] && q[1] == p[1]), $"Stützpunkt ({p[0]} | {p[1]}) fehlt im Verlauf");
            for (var i = 0; i + 1 < v.Punkte.Count; i++)
            {
                var d = Math.Sqrt(Math.Pow(v.Punkte[i + 1][0] - v.Punkte[i][0], 2) + Math.Pow(v.Punkte[i + 1][1] - v.Punkte[i][1], 2));
                Pruefe.Wahr(d <= 1.0 + 1e-6 && d > 0.3, $"Abstand {d:0.000} m zwischen Punkt {i} und {i + 1}");
            }
            var gerade = Wegkurve.Laenge(stuetz);
            Pruefe.Wahr(v.Laenge >= gerade - 1e-6 && v.Laenge < gerade * 1.1, $"Länge {v.Laenge:0.0} m, gerade {gerade:0.0} m");
            Pruefe.Gleich(v.Punkte.Count, v.Abschnitte.Count);
            Pruefe.Gleich(0, v.Abschnitte[0]);
            Pruefe.Gleich(2, v.Abschnitte[^1]);
            // Gleiche Stützpunkte, gleiche Kurve
            var nochmal = Wegkurve.Verlauf(stuetz, glatt: true);
            Pruefe.Wahr(v.Punkte.Zip(nochmal.Punkte).All(x => x.First[0] == x.Second[0] && x.First[1] == x.Second[1]), "deterministisch");
        }),

        ("Wegkurve: Ohne „kurve“ bleibt jeder Weg der Greybox gerade (Verlauf = Stützpunkte); in Köln nur die Landstraßen und Leinpfade, die „glatt“ sind; ein glatter Ring schließt sich", () =>
        {
            foreach (var w in Stadtkarte.AusDatei(Datenpfad.Karte("greybox")).Wege)
            {
                Pruefe.Wahr(!w.IstGlatt && w.Kurve is null, $"{w.Name} ist gerade");
                Pruefe.Wahr(w.Verlauf.Count == w.Linie.Count && w.Verlauf.Zip(w.Linie).All(x => x.First[0] == x.Second[0] && x.First[1] == x.Second[1]), $"{w.Name}: Verlauf = Linie");
            }
            foreach (var w in Stadtkarte.AusDatei(Datenpfad.Karte("koeln")).Wege.Where(w => w.Klasse is Wegklasse.Haupt or Wegklasse.Wall or Wegklasse.Gasse or Wegklasse.Ring))
                Pruefe.Wahr(!w.IstGlatt && w.Verlauf.Count == w.Linie.Count, $"Köln, Stadt: {w.Name} ist gerade");
            Pruefe.Wahr(Stadtkarte.AusDatei(Datenpfad.Karte("koeln")).Wege.Where(w => w.Klasse is Wegklasse.Land or Wegklasse.Leinpfad).All(w => w.IstGlatt), "Köln: Landstraßen und Leinpfade sind glatt");
            var ring = new Kartenweg { Linie = new() { new double[] { -10, -10 }, new double[] { 10, -10 }, new double[] { 10, 10 }, new double[] { -10, 10 }, new double[] { -10, -10 } }, Kurve = "glatt" };
            var v = ring.Verlauf;
            Pruefe.Wahr(v[0][0] == v[^1][0] && v[0][1] == v[^1][1], "Ring geschlossen");
            Pruefe.Wahr(ring.Laenge > 80 && ring.Laenge < 88.9, $"Ringlänge {ring.Laenge:0.0} (zwischen Quadrat 80 und Kreis durch die Ecken 88,9)");
            // Bauwesen rechnet mit der Kurve: Die Mitte der Biegung liegt nah an der Kurve, aber weit von der Sehne
            var bogen = new Kartenweg { Linie = new() { new double[] { 0, 0 }, new double[] { 10, 10 }, new double[] { 20, 0 } }, Kurve = "glatt" };
            var gerade = new Kartenweg { Linie = bogen.Linie };
            Pruefe.Wahr(bogen.Abstand(10, 10) < 1e-9 && bogen.Abstand(5, 6) < gerade.Abstand(5, 6) + 1, "Abstand auf dem Verlauf");
            Pruefe.Wahr(Math.Abs(bogen.Abstand(5, 5) - gerade.Abstand(5, 5)) > 0.1, "die Kurve weicht von der Sehne ab");
        }),

        ("Wege: Mindestbreite je Klasse, Standardbreite, Karren auf Feldweg, nicht auf Fußweg; „kurve“ und neue Klassen liest der Kern", () =>
        {
            Pruefe.Gleich(4.0, Kartenweg.Mindestbreite(Wegklasse.Land));
            Pruefe.Gleich(2.0, Kartenweg.Mindestbreite(Wegklasse.Gasse));
            Pruefe.Gleich(3.0, Kartenweg.Mindestbreite(Wegklasse.Feldweg));
            Pruefe.Gleich(1.5, Kartenweg.Mindestbreite(Wegklasse.Fussweg));
            foreach (var k in Enum.GetValues<Wegklasse>())
                Pruefe.Wahr(Kartenweg.Standardbreite(k) >= Kartenweg.Mindestbreite(k), $"{k}: Standard nicht unter Mindestbreite");
            foreach (var w in Stadtkarte.AusDatei(Datenpfad.Karte("greybox")).Wege)
                Pruefe.Wahr(w.BreiteWirksam >= Kartenweg.Mindestbreite(w.Klasse), $"Greybox: {w.Name} hält die Mindestbreite");
            Pruefe.Wahr(Kartenweg.TraegtKarren(Wegklasse.Feldweg) && !Kartenweg.TraegtKarren(Wegklasse.Fussweg), "Karren");

            var e = Oeffnen("greybox");
            var feld = e.WegAnlegen(Wegklasse.Feldweg, new[] { O(-150, 6), O(-170, 20), O(-190, 6) }, glatt: true, einrasten: false);
            var fuss = e.WegAnlegen(Wegklasse.Fussweg, new[] { O(-190, 6), O(-200, 30) }, glatt: false, einrasten: false);
            var json = e.ZuJson();
            Pruefe.Wahr(json.Contains("\"klasse\": \"feldweg\"") && json.Contains("\"klasse\": \"fussweg\"") && json.Contains("\"kurve\": \"glatt\""), "Schreibweise in der Datei");
            var karte = Stadtkarte.AusJson(json, Katalog.Value);
            var k1 = karte.Wege.Single(w => w.Name == feld.Name);
            Pruefe.Gleich(Wegklasse.Feldweg, k1.Klasse);
            Pruefe.Wahr(k1.IstGlatt && k1.Verlauf.Count > 3, "der Kern liest die Kurve");
            Pruefe.Wahr(!karte.Wege.Single(w => w.Name == fuss.Name).IstGlatt, "ohne kurve gerade");
            Pruefe.WirftMitMeldung(() => Stadtkarte.AusJson(json.Replace("\"kurve\": \"glatt\"", "\"kurve\": \"wellig\""), Katalog.Value), "kurve");
            // Im Spielstand steht ein gerader Weg ohne „Kurve“
            Pruefe.Wahr(!JsonSerializer.Serialize(new Kartenweg { Name = "x" }).Contains("Kurve"), "kein Kurve-Feld im Spielstand");
        }),

        // ── Befehle für Wege ────────────────────────────────────────────────

        ("Wege: Anlegen, Stützpunkte verschieben/einfügen/löschen, Name, Art, Breite, Kurve, Verschieben, Löschen – alles mit Rückgängig", () =>
        {
            var e = Oeffnen("greybox");
            var vorher = e.ZuJson();
            var meldungen = new List<Kartenaenderung>();
            e.Geaendert += meldungen.Add;

            var w = e.WegAnlegen(Wegklasse.Gasse, new[] { O(-150.02, 40), O(-160, 50), O(-160, 50), O(-175, 52) }, glatt: true, einrasten: false);
            Pruefe.Gleich("Gasse", w.Name);
            Pruefe.Gleich(3, w.Punkte.Count, "doppelter Klick fällt weg");
            Pruefe.Gleich(O(-150, 40), w.Punkte[0], "Punkt auf 5 cm");
            Pruefe.Gleich(3.0, w.Breite, "Standardbreite der Gasse");
            Pruefe.Wahr(meldungen[^1] is { Art: Aenderungsart.Gesetzt, Teil: Kartenteil.Weg } m && ReferenceEquals(m.Weg, w), "Ereignis für den Weg");
            var zweite = e.WegAnlegen(Wegklasse.Gasse, new[] { O(-150, 60), O(-160, 70) }, einrasten: false);
            Pruefe.Gleich("Gasse 2", zweite.Name);

            e.StuetzpunktVerschieben(w, 1, -161, 49);
            e.StuetzpunktEinfuegen(w, 3, -180, 60);
            Pruefe.Gleich(4, w.Punkte.Count);
            var neu = e.StuetzpunktEinfuegenBei(w, -155, 44.5);
            Pruefe.Gleich(1, neu, "Klick auf die Linie zwischen Stützpunkt 0 und 1");
            Pruefe.Wahr(e.StuetzpunktLoeschen(w, 1), "löschen");
            e.WegUmbenennen(w, "Krumme Gasse");
            e.WegKlasseSetzen(w, Wegklasse.Fussweg);
            e.WegBreiteSetzen(w, 0.5);
            Pruefe.Gleich(1.5, w.Breite, "nicht unter der Mindestbreite des Fußwegs");
            e.WegKlasseSetzen(w, Wegklasse.Land);
            Pruefe.Gleich(4.0, w.Breite, "die Art hebt die Breite auf die Mindestbreite");
            e.WegKurveSetzen(w, false);
            Pruefe.Wahr(!w.IstGlatt && w.Verlauf.Count == w.Punkte.Count, "begradigt");
            e.WegVerschieben(w, 1, -2);
            Pruefe.Gleich(O(-149, 38), w.Punkte[0]);
            e.WegLoeschen(zweite);
            Pruefe.Wahr(!e.Wege.Contains(zweite), "gelöscht");
            Pruefe.Gleich("Gasse 2 gelöscht", e.Geschichte.RueckgaengigText);
            Pruefe.Gleich(13, e.Geschichte.Schritte);

            // Ohne Wirkung kein Schritt; ein Weg hat mindestens zwei Punkte
            e.WegUmbenennen(w, "Krumme Gasse");
            Pruefe.Gleich(13, e.Geschichte.Schritte);
            var zwei = e.WegAnlegen(Wegklasse.Gasse, new[] { O(-150, 80), O(-160, 80) }, einrasten: false);
            Pruefe.Wahr(!e.StuetzpunktLoeschen(zwei, 0), "zwei Punkte bleiben");
            Pruefe.WirftMitMeldung(() => e.WegAnlegen(Wegklasse.Gasse, new[] { O(1, 1), O(1.01, 1) }), "zwei verschiedene Punkte");
            e.Rueckgaengig();

            var mitte = e.ZuJson();
            while (e.Rueckgaengig()) { }
            Pruefe.Gleich(vorher, e.ZuJson(), "alles zurückgenommen = Datei wie vorher");
            for (var i = 0; i < 13; i++) e.Wiederholen(); // die 13 Schritte, nicht den zurückgenommenen 14.
            Pruefe.Gleich(mitte, e.ZuJson(), "alles wiederholt");
            Pruefe.Wahr(ReferenceEquals(w, e.Wege.Single(x => x.Name == "Krumme Gasse")), "der Weg bleibt dasselbe Objekt");
        }),

        ("Wege: Ein Wegende rastet an der Linie eines anderen ein – dort bekommt er einen Stützpunkt, ein Rückgängig nimmt beides zurück", () =>
        {
            var e = Oeffnen("greybox");
            var ost = e.Wege.Single(w => w.Name == "Ostgasse"); // [0, 0] – [50, 0]
            var vorher = e.ZuJson();

            var fang = e.Einrasten(20.3, 1.5);
            Pruefe.Wahr(fang is { Stuetzpunkt: null } f && ReferenceEquals(f.Weg, ost) && f.Ort == O(20.3, 0), $"Vorschau: auf der Linie ({fang})");
            Pruefe.Wahr(e.Einrasten(20, 2.5) is null, "außerhalb des Fangradius");
            Pruefe.Wahr(e.Einrasten(49.2, 1) is { Stuetzpunkt: 1, Ort: var s } && s == O(50, 0), "ein Stützpunkt geht vor");

            var schritte = e.Geschichte.Schritte;
            var neu = e.WegAnlegen(Wegklasse.Gasse, new[] { O(20.3, 1.5), O(20.3, 12) }, glatt: false);
            Pruefe.Gleich(O(20.3, 0), neu.Punkte[0], "Anfang eingerastet");
            Pruefe.Gleich(3, ost.Punkte.Count, "die Ostgasse hat dort einen Stützpunkt");
            Pruefe.Gleich(O(20.3, 0), ost.Punkte[1]);
            Pruefe.Gleich(schritte + 1, e.Geschichte.Schritte, "ein Schritt");
            Pruefe.Wahr(!e.Befunde.Any(b => b.Regel == "wegenetz"), "das Netz hängt zusammen");

            // Ende an einen Stützpunkt ziehen: kein neuer Stützpunkt beim Nachbarn
            e.StuetzpunktVerschieben(neu, 1, 50.8, 0.6);
            Pruefe.Gleich(O(50, 0), neu.Punkte[1]);
            Pruefe.Gleich(3, ost.Punkte.Count);

            e.Rueckgaengig();
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson(), "Weg und Stützpunkt zurückgenommen");
            Pruefe.Gleich(2, ost.Punkte.Count);

            // Einrasten an einer glatten Kurve: der neue Stützpunkt liegt auf ihr, beide Wege treffen sich genau
            var bogen = e.WegAnlegen(Wegklasse.Feldweg, new[] { O(-150, 40), O(-170, 60), O(-190, 40) }, glatt: true, einrasten: false);
            var p = bogen.Verlauf[bogen.Verlauf.Count / 3];
            var quer = e.WegAnlegen(Wegklasse.Fussweg, new[] { O(p.X + 0.8, p.Z + 0.8), O(p.X + 10, p.Z + 10) }, glatt: false);
            Pruefe.Wahr(bogen.Punkte.Contains(quer.Punkte[0]), "gemeinsamer Stützpunkt");
            Pruefe.Wahr(bogen.Verlauf.Any(q => q.Abstand(quer.Punkte[0]) < 1e-9), "die Kurve geht durch den Kreuzungspunkt");
        }),

        // ── Flächen ─────────────────────────────────────────────────────────

        ("Flächen: Bauzone und Naturfläche anlegen, Ecken und Aussparungen bearbeiten, Eigenschaften, Löschen – alles mit Rückgängig", () =>
        {
            var e = Oeffnen("greybox");
            var vorher = e.ZuJson();
            var z = e.BauzoneAnlegen(Lage.Vorstadt, new[] { O(-150.7, 41.2), O(-139.2, 41.2), O(-139.2, 52.9), O(-150.7, 52.9), O(-150.7, 41.2) });
            Pruefe.Gleich("V4", z.Kennung, "kleinste freie Nummer der Vorstadt (in der Greybox fehlt V4)");
            Pruefe.Wahr(z.Ecken.SequenceEqual(new[] { O(-150, 42), O(-140, 42), O(-140, 52), O(-150, 52) }), "Ecken auf dem 2-m-Raster, die letzte nicht doppelt: " + string.Join(" ", z.Ecken));
            e.EckeVerschieben(z, 2, -137.1, 55);
            Pruefe.Gleich(O(-138, 56), z.Ecken[2]);
            Pruefe.Gleich(2, e.EckeEinfuegenBei(z, -139, 47));
            Pruefe.Wahr(e.EckeLoeschen(z, 2), "Ecke gelöscht");
            e.BauzoneArtSetzen(z, Lage.Umland);
            e.FlaecheVerschieben(z, 3, 0);
            Pruefe.Gleich(O(-148, 42), z.Ecken[0], "in Rasterschritten");

            var n = e.NaturflaecheAnlegen("wald", new[] { O(-300, -100), O(-200, -100), O(-200, 0), O(-300, 0) });
            Pruefe.Gleich("Wald", n.Name);
            Pruefe.Gleich(Kartenentwurf.WaldDichte, n.Dichte);
            Pruefe.Wahr(n.Startwert > 3101, "neuer Startwert");
            var loch = e.AussparungAnlegen(n, new[] { O(-260, -60), O(-240, -60), O(-240, -40), O(-260, -40) });
            e.EckeVerschieben(n, 0, -262, -62, loch);
            Pruefe.Gleich(O(-262, -62), n.Aussparungen[loch][0]);
            e.DichteSetzen(n, 5);
            e.StartwertSetzen(n, 77);
            e.MischungSetzen(n, new Dictionary<string, double> { ["eiche"] = 1, ["buche"] = 1 });
            Pruefe.Wahr(n.Mischung.Count == 2 && Math.Abs(n.Mischung["eiche"] - 0.5) < 1e-9, "Mischung halb und halb");
            e.NaturArtSetzen(n, "streu");
            e.NaturflaecheUmbenennen(n, "Eichenhain");
            Pruefe.Wahr(e.EckeLoeschen(n, 3, loch) && !e.EckeLoeschen(n, 0, loch), "eine Aussparung behält mindestens drei Ecken");
            e.AussparungLoeschen(n, loch);
            Pruefe.Gleich(0, n.Aussparungen.Count);

            var karte = Stadtkarte.AusJson(e.ZuJson(), Katalog.Value);
            var eichenhain = karte.Natur.Flaechen.Single(f => f.Name == "Eichenhain");
            Pruefe.Gleich((5.0, 77, "streu"), (eichenhain.Dichte, eichenhain.Startwert, eichenhain.Art));
            Pruefe.Gleich(Lage.Umland, karte.Bauzonen.Single(b => b.Kennung == "V4").Art);

            e.FlaecheLoeschen(z);
            e.FlaecheLoeschen(n);
            while (e.Rueckgaengig()) { }
            Pruefe.Gleich(vorher, e.ZuJson(), "alles zurückgenommen = Datei wie vorher");
        }),

        ("Flächen: Köln ohne Natur – eine erste Pflanze schreibt „natur“ an seine Stelle hinter die Bauzonen; zurückgenommen ist die Datei wie vorher", () =>
        {
            var e = Oeffnen("koeln");
            var vorher = e.ZuJson();
            var p = e.PflanzeSetzen("eiche", -300, -300, 30, 1.1, 2);
            var json = e.ZuJson();
            var reihenfolge = JsonNode.Parse(json)!.AsObject().Select(x => x.Key).ToList();
            Pruefe.Gleich(reihenfolge.IndexOf("bauzonen") + 1, reihenfolge.IndexOf("natur"), "natur hinter bauzonen: " + string.Join(",", reihenfolge));
            Pruefe.Wahr(json.Contains("\"pflanzen\": [{\"katalog\": \"eiche\", \"punkt\": [-300, -300], \"drehung\": 30, \"massstab\": 1.1, \"variante\": 2}]"), json[json.IndexOf("\"natur\"")..][..200]);
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson());
            Pruefe.WirftMitMeldung(() => e.PflanzeSetzen("werkstattplatz", 0, 0), "kein Natur-Baustein");
        }),

        // ── Pflanzen und Pinsel ─────────────────────────────────────────────

        ("Pflanzen: Setzen, Verschieben, Drehen, Maßstab, Löschen mit Rückgängig; jede Natur-Art des Katalogs ist setzbar", () =>
        {
            var e = Oeffnen("greybox");
            var vorher = e.ZuJson();
            foreach (var b in e.Katalog.Bausteine.Where(b => b.Art == Bausteinart.Natur))
            {
                var p = e.PflanzeSetzen(b.Kennung, -200, -200);
                Pruefe.Wahr(e.LageVon(p) is { } l && l.Variante >= 1 && l.Variante <= Math.Max(1, b.Varianten), $"{b.Kennung}: Variante aus dem Ort");
            }
            var t = e.PflanzeSetzen("tanne", -210, -200, 0, 1, 3);
            e.PflanzeVerschieben(t, -211.02, -201);
            e.PflanzeDrehen(t, -45);
            e.PflanzeMassstabSetzen(t, 1.234);
            Pruefe.Gleich((-211.0, -201.0, 315.0, 1.23), (t.X, t.Z, t.Drehung, t.Massstab));
            e.PflanzenLoeschen(e.Pflanzen.Take(3).ToList());
            Pruefe.Gleich("3 Pflanzen gelöscht", e.Geschichte.RueckgaengigText);
            var mitte = e.ZuJson();
            while (e.Rueckgaengig()) { }
            Pruefe.Gleich(vorher, e.ZuJson());
            while (e.Wiederholen()) { }
            Pruefe.Gleich(mitte, e.ZuJson());
            Pruefe.Gleich(Katalog.Value.Bausteine.Count(b => b.Art == Bausteinart.Natur) - 2, e.Pflanzen.Count, "nach dem Löschen von dreien");
        }),

        ("Pinsel: Ein Strich streut deterministisch (gleicher Strich, gleicher Startwert), ein Rückgängig je Strich, frei auf Wegen und Gebäuden", () =>
        {
            var strich = new[] { O(-90, 30), O(-70, 34), O(-55, 40), O(-40, 70) };
            var arten = new Dictionary<string, double> { ["eiche"] = 2, ["hasel"] = 1 };
            List<(string, double, double, double, double, int)> Streuen(out Kartenentwurf e)
            {
                e = Oeffnen("greybox");
                return e.PflanzenStreuen(strich, 8, 40, arten, startwert: 5).Select(p => (p.Katalog, p.X, p.Z, p.Drehung, p.Massstab, p.Variante)).ToList();
            }
            var a = Streuen(out var e1);
            var b = Streuen(out _);
            Pruefe.Wahr(a.Count > 5, $"{a.Count} Pflanzen");
            Pruefe.Wahr(a.SequenceEqual(b), "deterministisch");
            Pruefe.Wahr(a.All(p => p.Item1 is "eiche" or "hasel"), "nur die gewählten Arten");
            var frei = e1.Freiraum();
            Pruefe.Wahr(e1.Pflanzen.All(p => frei.Frei(p.X, p.Z)), "frei auf Wegen, Gebäuden und Bauzonen");
            Pruefe.Wahr(!e1.Befunde.Any(x => x.Regel == "pflanze-frei"), "kein Hinweis „Pflanze auf …“");
            Pruefe.Gleich($"{a.Count} Pflanzen gestreut", e1.Geschichte.RueckgaengigText);
            e1.Rueckgaengig();
            Pruefe.Gleich(0, e1.Pflanzen.Count, "ein Rückgängig nimmt den ganzen Strich");
            // Ein zweiter Strich über denselben Ort hält Abstand zu den schon gesetzten
            e1.Wiederholen();
            var zweiter = e1.PflanzenStreuen(strich, 8, 40, arten, startwert: 6);
            Pruefe.Wahr(zweiter.All(p => a.All(q => Math.Sqrt((p.X - q.Item2) * (p.X - q.Item2) + (p.Z - q.Item3) * (p.Z - q.Item3)) >= 0.6 * Math.Sqrt(1000 / 40.0) - 0.1)),
                "Abstand zu den schon gesetzten");
        }),

        // ── Natur-Verteilung ────────────────────────────────────────────────

        ("Natur-Verteilung: Greybox deterministisch, frei unter Gebäuden, Wegen, Bauzonen und in der Stadt; Arten und Dichte wie die Darstellung", () =>
        {
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("greybox"));
            var a = NaturVerteilung.Alle(karte);
            var b = NaturVerteilung.Alle(karte);
            Pruefe.Wahr(a.SequenceEqual(b), "deterministisch");
            var frei = Naturfreiraum.Aus(karte);
            foreach (var p in a)
            {
                Pruefe.Wahr(!frei.InDerStadt(p.X, p.Z), $"{p.Art.Kennung} bei ({p.X:0.0} | {p.Z:0.0}) in der Stadt");
                foreach (var w in karte.Wege)
                    Pruefe.Wahr(w.Abstand(p.X, p.Z) >= w.BreiteWirksam / 2 + Naturfreiraum.Abstand - 1e-9, $"{p.Art.Kennung} bei ({p.X:0.0} | {p.Z:0.0}) auf {w.Name}");
                foreach (var g in karte.Gebaeude)
                    Pruefe.Wahr(Naturhindernis.Von(g.Baustein, g.Punkt[0], g.Punkt[1], g.Drehung, g.Massstab) is not { } h || !h.Enthaelt(p.X, p.Z, Naturfreiraum.Abstand),
                        $"{p.Art.Kennung} bei ({p.X:0.0} | {p.Z:0.0}) auf {g.Kennung}");
                Pruefe.Wahr(karte.Bauzonen.All(z => z.Abstand(p.X, p.Z) >= Naturfreiraum.Abstand), $"{p.Art.Kennung} auf einer Bauzone");
                Pruefe.Wahr(p.Massstab >= 0.85 && p.Massstab <= 1.15 && p.Drehung >= 0 && p.Drehung < 360, "Größe und Drehung");
                Pruefe.Wahr(p.Variante >= 1 && p.Variante <= Math.Max(1, p.Art.Varianten), "Variante");
            }
            // Je Fläche: nur ihre Arten; der Tannenwald nur Tannen, der Mischwald mit Eichen und Buchen
            var frei2 = Naturfreiraum.Aus(karte);
            var tannen = NaturVerteilung.Flaeche(karte.Natur.Flaechen[0], karte.Katalog!, frei2);
            var misch = NaturVerteilung.Flaeche(karte.Natur.Flaechen[1], karte.Katalog!, frei2);
            Pruefe.Wahr(tannen.Count > 20 && tannen.All(p => p.Art.Kennung == "tanne"), $"Tannenwald: {tannen.Count} Tannen");
            Pruefe.Wahr(misch.Select(p => p.Art.Kennung).Distinct().Count() == 3, "Mischwald mit drei Arten");
            Console.WriteLine($"          (Greybox: {a.Count} Pflanzen, Tannenwald {tannen.Count}, Mischwald {misch.Count})");
            // Eine neue Straße durch den Wald nimmt nur die Bäume auf ihr weg, die übrigen bleiben, wo sie waren
            var mitStrasse = Stadtkarte.AusJson(Text("greybox").Replace("\"wege\": [", "\"wege\": [\n  {\"name\": \"Schneise\", \"klasse\": \"land\", \"linie\": [[100, -120], [100, 120]], \"gefahr\": 1, \"breite\": 4},"), Katalog.Value);
            var nachher = NaturVerteilung.Flaeche(mitStrasse.Natur.Flaechen[0], Katalog.Value, Naturfreiraum.Aus(mitStrasse));
            var alt = tannen.Select(Schluessel).ToHashSet();
            Pruefe.Wahr(nachher.Count < tannen.Count && nachher.Select(Schluessel).All(alt.Contains), "nur Bäume auf der Schneise fallen weg");
        }),

        ("Natur-Verteilung: Neu-Streuen des Greybox-Walds unter 10 ms (Release, Median nach dem Aufwärmen); Entwurf und Stadtkarte zeigen dasselbe", () =>
        {
            var e = Oeffnen("greybox");
            var karte = Stadtkarte.AusDatei(Datenpfad.Karte("greybox"));
            var wald = e.Naturflaechen[0];
            var zeit = Median(() => e.NaturVon(wald, e.Freiraum()));
            var alle = Median(() => e.NaturAlle());
            Console.WriteLine($"          (Tannenwald mit Freiraum {zeit:0.0} ms, ganze Natur {alle:0.0} ms)");
            Pruefe.Wahr(zeit < 10, $"Tannenwald {zeit:0.0} ms");
            Pruefe.Wahr(e.NaturAlle().Select(Schluessel).SequenceEqual(NaturVerteilung.Alle(karte).Select(Schluessel)), "Entwurf = Stadtkarte");
            // Ein Weg im Entwurf räumt seinen Streifen
            var baeume = e.NaturVon(wald, e.Freiraum());
            var t = baeume[0];
            e.WegAnlegen(Wegklasse.Land, new[] { O(t.X - 20, t.Z - 3), O(t.X, t.Z), O(t.X + 20, t.Z - 3) }, glatt: true, einrasten: false);
            var nachher = e.NaturVon(wald, e.Freiraum());
            Pruefe.Wahr(nachher.Count < baeume.Count && !nachher.Any(b => b.X == t.X && b.Z == t.Z), "der neue Weg räumt den Baum auf seiner Kurve");
        }),

        // ── Prüfung ─────────────────────────────────────────────────────────

        ("Prüfung: Weg unter Mindestbreite, Weg mit einem Punkt, Kurve über ein Haus, Fläche kreuzt sich, Bauzone doppelt, Pflanze auf Weg/Gebäude", () =>
        {
            var e = GreyboxMit(k =>
            {
                var wege = k["wege"]!.AsArray();
                wege.Add(JsonNode.Parse("""{"name": "Zu schmal", "klasse": "land", "linie": [[-150, 6], [-200, 6]], "gefahr": 1, "breite": 3}"""));
                wege.Add(JsonNode.Parse("""{"name": "Stummel", "klasse": "gasse", "linie": [[-200, 6]], "gefahr": 1}"""));
                k["bauzonen"]!.AsArray().Add(JsonNode.Parse("""{"kennung": "V1", "art": "vorstadt", "flaeche": [[-150, 40], [-140, 50], [-140, 40], [-150, 50]]}"""));
                k["natur"]!["pflanzen"] = JsonNode.Parse("""[{"katalog": "eiche", "punkt": [-175, 6], "drehung": 0, "massstab": 1, "variante": 0}, {"katalog": "eiche", "punkt": [22, 10], "drehung": 0, "massstab": 1, "variante": 0}]""");
            });
            var b = e.Befunde;
            var text = Liste(b);
            Pruefe.Wahr(b.Any(x => x is { Regel: "weg-breite", Schwere: Schwere.Fehler } && x.Andere.Any(t => t is EntwurfWeg { Name: "Zu schmal" })), "Mindestbreite\n  " + text);
            Pruefe.Wahr(b.Any(x => x is { Regel: "weg-punkte", Schwere: Schwere.Fehler } && x.Text.Contains("Stummel")), "ein Punkt");
            Pruefe.Wahr(b.Any(x => x is { Regel: "flaeche-kreuzt", Schwere: Schwere.Hinweis } && x.Text.Contains("V1")), "Fläche kreuzt sich");
            Pruefe.Wahr(b.Any(x => x is { Regel: "zone-kennung" } && x.Text.Contains("„V1“")), "Bauzone doppelt");
            Pruefe.Wahr(b.Count(x => x is { Regel: "pflanze-frei", Schwere: Schwere.Hinweis }) == 2, "Pflanze auf Weg und auf Gebäude");
            Pruefe.Wahr(b.Any(x => x.Regel == "pflanze-frei" && x.Gebaeude.Contains("Giesserei_Eisenbart")), "auf der Gießerei");

            // Eine glatte Kurve, deren Bogen über ein Haus schwingt (die Stützpunkte liegen daneben)
            var f = Oeffnen("greybox");
            var giesserei = f.GebaeudeMit("Giesserei_Eisenbart")!; // x 14 … 30, z 6 … 20
            f.WegAnlegen(Wegklasse.Gasse, new[] { O(-150, -150), O(-150, -151), O(-151, -151) }, einrasten: false); // weit weg, ohne Befund
            var vorher = f.Befunde.Count(x => x.Regel == "kurve-ueber-haus");
            var w = f.WegAnlegen(Wegklasse.Fussweg, new[] { O(10, 25), O(22, 12), O(34, 25) }, glatt: true, einrasten: false);
            Pruefe.Wahr(f.Befunde.Any(x => x.Regel == "kurve-ueber-haus" && x.Gebaeude.Contains(giesserei.Kennung) && x.Andere.Contains(w)), "Kurve über der Gießerei\n  " + Liste(f.Befunde));
            Pruefe.Gleich(0, vorher);
        }),

        ("Prüfung: Ein Weg aus der Werkstatt ändert die Befunde der Greybox nicht; die volle Prüfung mit glatten Wegen bleibt unter 20 ms", () =>
        {
            var e = Oeffnen("greybox");
            var ohne = e.Befunde.Select(b => b.ToString()).ToList();
            // ein glatter Feldweg weit draußen, am Ende der Weststraße eingerastet
            var feld = e.WegAnlegen(Wegklasse.Feldweg, new[] { O(-160, -60), O(-170, -30), O(-150, -10), O(-119, 7) }, glatt: true);
            Pruefe.Gleich(O(-118, 6), feld.Punkte[^1], "am Ende der Weststraße eingerastet");
            var mit = e.Befunde.Select(b => b.ToString()).ToList();
            Pruefe.Wahr(ohne.SequenceEqual(mit), "dieselben Befunde:\n  " + string.Join("\n  ", mit.Except(ohne)));
            var voll = Median(() => Kartenpruefung.Pruefen(e));
            Console.WriteLine($"          (volle Prüfung mit glattem Weg {voll:0.0} ms)");
            Pruefe.Wahr(voll < 20, $"volle Prüfung {voll:0.0} ms");
        }),

        // ── Rückmeldungen aus Schritt 1 ─────────────────────────────────────

        ("Ereignis: Geaendert kommt erst, wenn die Geschichte den Schritt kennt (Rückgängig, Wiederholen, geändert)", () =>
        {
            var e = Oeffnen("greybox");
            var gesehen = new List<(bool KannZurueck, bool KannVor, bool Geaendert, string? Text)>();
            e.Geaendert += _ => gesehen.Add((e.Geschichte.KannRueckgaengig, e.Geschichte.KannWiederholen, e.Geschichte.IstGeaendert, e.Geschichte.RueckgaengigText));
            e.Drehen("Markt", 90);
            Pruefe.Gleich((true, false, true, (string?)"Marktplatz gedreht"), gesehen[^1], "nach dem Befehl");
            e.Rueckgaengig();
            Pruefe.Gleich((false, true, false, (string?)null), gesehen[^1], "nach Rückgängig");
            e.Wiederholen();
            Pruefe.Gleich((true, false, true, (string?)"Marktplatz gedreht"), gesehen[^1], "nach Wiederholen");
            // Ein Befehl, der zwei Teile ändert (Einrasten), meldet beide – beide schon mit dem neuen Stand
            gesehen.Clear();
            e.WegAnlegen(Wegklasse.Gasse, new[] { O(20, 1), O(20, 10) });
            Pruefe.Gleich(2, gesehen.Count);
            Pruefe.Wahr(gesehen.All(g => g.KannZurueck && g.Text == "Gasse gesetzt"), "beide nach dem Eintrag");
        }),

        ("Kennungen: aus „Bürgerhaus (Kulisse)“ wird „Buergerhaus_Kulisse1“; die Kennungen von Greybox und Köln bleiben", () =>
        {
            Pruefe.Gleich("Buergerhaus_Kulisse", Kartenentwurf.KennungVon("Bürgerhaus (Kulisse)"));
            Pruefe.Gleich("Grube_am_Silberberg", Kartenentwurf.KennungVon("Grube am Silberberg"));
            Pruefe.Gleich("Weg_zu_Hof", Kartenentwurf.KennungVon("  Weg -- zu / Hof!"));
            var e = Oeffnen("greybox");
            Pruefe.Gleich("Buergerhaus_Kulisse1", e.Setzen("kulisse_buergerhaus", -150, -40).Kennung);
            // Rundreise bleibt (die Kennungen der Dateien schreibt die Werkstatt nie neu)
            Pruefe.Gleich(Text("koeln"), Oeffnen("koeln").ZuJson());
        }),
    };
}
