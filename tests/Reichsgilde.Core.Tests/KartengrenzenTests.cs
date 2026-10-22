using System.Text.Json.Nodes;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Wirtschaft;
using Reichsgilde.Kartenbau.Staedte;

/// <summary>
/// Kartenwerkstatt, Schritt 5a (PR #178): Größe und Mitte des Spielfelds, Randstreifen, Kamera-Start und Kamera-Grenzen, Felder und der
/// Rand-Vorschlag – Format, Befehle mit Rückgängig, Prüfung.
/// </summary>
static class KartengrenzenTests
{
    static string Datenordner => Path.GetDirectoryName(Datenpfad.Wirtschaft())!;
    static readonly Lazy<Gebaeudekatalog> Katalog = new(() => Gebaeudekatalog.AusDatei(Path.Combine(Datenordner, "katalog.json")));
    static readonly Lazy<Wirtschaftsdaten> Daten = new(() => Wirtschaftsdaten.AusDatei(Datenpfad.Wirtschaft()));

    static string Text(string kennung) => File.ReadAllText(Datenpfad.Karte(kennung));
    static Kartenentwurf Oeffnen(string kennung) => Kartenentwurf.AusJson(Text(kennung), Katalog.Value);
    static string Liste(IEnumerable<Befund> befunde) => string.Join("\n  ", befunde.Select(b => b.ToString()));
    static List<Befund> Fehler(Kartenentwurf e) => e.Befunde.Where(b => b.Schwere == Schwere.Fehler).ToList();
    static Kartenort O(double x, double z) => new(x, z);

    /// <summary>Der Text einer Karte ohne die Felder des Kopfes (wie vor Schritt 5a).</summary>
    static string OhneGroesse(string kennung)
    {
        var json = JsonNode.Parse(Text(kennung))!.AsObject();
        foreach (var k in new[] { "groesse", "mitte", "rand" }) json.Remove(k);
        return Kartenschreiber.Kompakt(json);
    }

    /// <summary>Ein Rechteck als Ecken.</summary>
    static Kartenort[] Rechteck(double x0, double z0, double x1, double z1) => new[] { O(x0, z0), O(x1, z0), O(x1, z1), O(x0, z1) };

    public static IEnumerable<(string, Action)> Alle() => new (string, Action)[]
    {
        ("Kartengrenzen: Spielfeld um die Mitte, Rand drumherum, Blickpunkt ins Spielfeld geklemmt, Größen auf 50 m", () =>
        {
            var g = new Kartengrenzen(600, 450, O(-50, 25), 300);
            Pruefe.Gleich(new Kartenrechteck(-350, -200, 250, 250), g.Spielfeld);
            Pruefe.Gleich(new Kartenrechteck(-650, -500, 550, 550), g.Aussen);
            Pruefe.Wahr(g.ImSpielfeld(-350, 250) && !g.ImRand(-350, 250), "die Kante gehört zum Spielfeld");
            Pruefe.Wahr(g.ImRand(-400, 0) && g.InDerKarte(-650, -500) && !g.InDerKarte(-651, 0), "Rand und Außenkante");
            Pruefe.Gleich(O(250, -200), g.BlickpunktKlemmen(900, -900));
            Pruefe.Gleich(O(10, 20), g.BlickpunktKlemmen(10, 20), "drin bleibt er, wo er ist");
            Pruefe.Gleich(450.0, Kartengrenzen.SeiteRunden(437, Kartengrenzen.MinBreite));
            Pruefe.Gleich(300.0, Kartengrenzen.SeiteRunden(120, Kartengrenzen.MinTiefe));
            Pruefe.Gleich(2000.0, Kartengrenzen.SeiteRunden(5000, Kartengrenzen.MinBreite));
            Pruefe.Gleich(100.0, Kartengrenzen.RandRunden(10));
            Pruefe.Gleich(600.0, Kartengrenzen.RandRunden(900));
            Pruefe.Gleich(350.0, Kartengrenzen.RandRunden(340));
        }),

        ("Kartengrenzen: Beim größten Zoom bleibt das Bild am Rand des Spielfelds im Randstreifen – bei jeder Drehung", () =>
        {
            var g = new Kartengrenzen(400, 300, default, 300);
            const double neigung = 40, seiten = 16.0 / 9;
            var zoom = g.GroessterZoom(neigung, seiten);
            // halbe Diagonale des Bildes am Boden = Randbreite
            var (quer, laengs) = (zoom * seiten / 2, zoom / (2 * Math.Sin(neigung * Math.PI / 180)));
            Pruefe.Ungefaehr(300, Math.Sqrt(quer * quer + laengs * laengs), 1e-6);
            foreach (var ecke in new[] { O(-200, -150), O(200, -150), O(200, 150), O(-200, 150), O(0, 150) })
                for (var d = 0; d < 360; d += 5)
                    foreach (var (a, b) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    {
                        var p = Kartengeometrie.Dreh(a * quer, b * laengs, d);
                        Pruefe.Wahr(g.InDerKarte(ecke.X + p.X, ecke.Z + p.Z), $"Bildecke bei Drehung {d}° über die Karte hinaus");
                    }
            // für eine feste Blickrichtung darf es weiter sein, nie weniger; mit Reserve weniger
            Pruefe.Wahr(g.GroessterZoom(neigung, seiten, 0) > zoom && g.GroessterZoom(neigung, seiten, 45) >= zoom - 1e-9, "feste Drehung");
            Pruefe.Ungefaehr(300 / (seiten / 2), g.GroessterZoom(neigung, seiten, 0), 1e-6); // bei 0° bestimmt die Bildbreite (16:9 breiter als 1/sin 40°)
            Pruefe.Ungefaehr(zoom * 250 / 300, g.GroessterZoom(neigung, seiten, reserve: 50), 1e-6);
            Pruefe.Wahr(zoom > 140, $"Bei 300 m Rand reicht der Zoom des Spiels (140), hier {zoom:0}");
        }),

        ("Kartengrenzen: Größe alter Karten aus dem Inhalt – auf 50 m, mindestens 400 × 300, Wege und Natur passen in den Rand", () =>
        {
            var leer = Kartengrenzen.Ableiten(Array.Empty<Kartenort>(), Array.Empty<Kartenort>());
            Pruefe.Gleich((400.0, 300.0, O(0, 0), 300.0), (leer.Breite, leer.Tiefe, leer.Mitte, leer.Randbreite));
            var g = Kartengrenzen.Ableiten(new[] { O(-300, -10), O(320, 90) }, Array.Empty<Kartenort>());
            Pruefe.Gleich(new Kartenrechteck(-350, -100, 350, 200), g.Spielfeld); // −320 → −350, 340 → 350; Tiefe 130 → 300
            var weit = Kartengrenzen.Ableiten(new[] { O(0, 0) }, new[] { O(-700, 0) });
            Pruefe.Wahr(weit.InDerKarte(-700, 0) && weit.Spielfeld.X0 <= -400, $"ein weiter Weg passt in den Rand: {weit.Spielfeld}");
            var riesig = Kartengrenzen.Ableiten(new[] { O(-3000, 0), O(3000, 0) }, Array.Empty<Kartenort>());
            Pruefe.Gleich(2000.0, riesig.Breite, "höchstens 2 km");
        }),

        ("Kartenformat: Greybox und Köln haben eine feste Größe, abgeleitet wie für ältere Spielstände; der übrige Inhalt ist unverändert", () =>
        {
            var greybox = Stadtkarte.AusDatei(Datenpfad.Karte("greybox"));
            Pruefe.Gleich(new Kartenrechteck(-200, -150, 200, 150), greybox.Kartengrenzen.Spielfeld);
            Pruefe.Gleich(300.0, greybox.Kartengrenzen.Randbreite);
            var koeln = Stadtkarte.AusDatei(Datenpfad.Karte("koeln"));
            Pruefe.Gleich(new Kartengrenzen(700, 450, O(-50, 25), 300), koeln.Kartengrenzen);
            foreach (var kennung in new[] { "greybox", "koeln" })
            {
                // ein alter Spielstand hat die Karte ohne Größe: Die Stadtkarte leitet dieselbe ab
                var alt = Stadtkarte.AusJson(OhneGroesse(kennung), Katalog.Value);
                Pruefe.Gleich(Stadtkarte.AusDatei(Datenpfad.Karte(kennung)).Kartengrenzen, alt.Kartengrenzen, kennung);
                // der Entwurf ohne Größe leitet sie ebenso ab, schreibt sie aber nicht (Datei unverändert) – erst gesetzt (wie „groesse“ des Kartenskripts)
                var e = Kartenentwurf.AusJson(OhneGroesse(kennung), Katalog.Value);
                Pruefe.Wahr(!e.Kopf.GroesseInDatei, "abgeleitet");
                Pruefe.Gleich(OhneGroesse(kennung), e.ZuJson(), $"{kennung}: ohne Größe gespeichert = dieselbe Datei");
                e.GroesseSetzen(e.Kopf.Breite, e.Kopf.Tiefe, e.Kopf.Mitte);
                e.RandbreiteSetzen(e.Kopf.Randbreite);
                Pruefe.Gleich(Text(kennung), e.ZuJson(), $"{kennung}: mit Größe = die Datei im Spiel");
                Pruefe.Gleich("", Liste(Fehler(e)), kennung);
            }
        }),

        ("Kartenformat: Eine neue Karte hat Größe, keinen Rand-Eintrag (Vorgabe 300 m); unbekannte Felder der Kamera bleiben", () =>
        {
            var e = Kartenentwurf.Neu("neu", "Neustadt", Kartengrenzen.NeueBreite, Kartengrenzen.NeueTiefe, Katalog.Value);
            Pruefe.Gleich(new Kartengrenzen(600, 450, default, 300), e.Kartengrenzen);
            var json = e.ZuJson();
            Pruefe.Wahr(json.Contains("\"groesse\": [600, 450]") && !json.Contains("\"rand\"") && !json.Contains("\"mitte\"") && !json.Contains("\"kamera\""), json);
            var mitNotiz = JsonNode.Parse(Text("probestadt"))!.AsObject();
            mitNotiz["kamera"]!["notiz"] = "für den Klicktest";
            var text = Kartenschreiber.Kompakt(mitNotiz);
            var p = Kartenentwurf.AusJson(text, Katalog.Value);
            Pruefe.Gleich(text, p.ZuJson(), "unverändert");
            p.KameraStartSetzen(O(10, 5), 90, 30);
            Pruefe.Wahr(p.ZuJson().Contains("\"kamera\": {\"punkt\": [10, 5], \"zoom\": 90, \"drehung\": 30, \"notiz\": \"für den Klicktest\"}"), p.ZuJson());
        }),

        ("Kartenwerkstatt: Größe, Rand und Kamera-Start sind Befehle mit Rückgängig und melden den Kopf", () =>
        {
            var e = Oeffnen("probestadt");
            var vorher = e.ZuJson();
            var meldungen = new List<Kartenaenderung>();
            e.Geaendert += meldungen.Add;
            e.GroesseSetzen(437, 120, O(10.4, -3.6));
            Pruefe.Gleich(new Kartengrenzen(450, 300, O(10, -4), 300), e.Kartengrenzen);
            Pruefe.Wahr(meldungen is [{ Teil: Kartenteil.Kopf, Art: Aenderungsart.Geaendert, Kopf: not null }], "eine Meldung für den Kopf");
            Pruefe.Gleich("Kartengröße 450 × 300 m", e.Geschichte.RueckgaengigText);
            e.RandbreiteSetzen(420);
            Pruefe.Gleich(400.0, e.Kartengrenzen.Randbreite);
            e.KameraStartSetzen(O(12.34, -7.89), 88.88, -90);
            Pruefe.Gleich("12.35 -7.9", string.Join(" ", e.Kamera!.Punkt.Select(Kartenschreiber.Zahl)));
            Pruefe.Gleich((88.9, 270.0), (e.Kamera.Zoom, e.Kamera.Drehung));
            var json = e.ZuJson();
            Pruefe.Wahr(json.Contains("\"groesse\": [450, 300],\n \"mitte\": [10, -4],\n \"rand\": 400,\n \"gebaeude\""), json[..600]);
            var schritte = e.Geschichte.Schritte;
            e.GroesseSetzen(450, 300);  // nichts geändert: kein Schritt
            e.RandbreiteSetzen(400);
            Pruefe.Gleich(schritte, e.Geschichte.Schritte);
            while (e.Rueckgaengig()) { }
            Pruefe.Gleich(vorher, e.ZuJson(), "alles zurück");
            Pruefe.Gleich(new Kartengrenzen(400, 300, default, 300), e.Kartengrenzen);
            while (e.Wiederholen()) { }
            Pruefe.Gleich(json, e.ZuJson(), "alles wiederholt");
            Pruefe.Gleich(new Kartengrenzen(450, 300, O(10, -4), 400), Kartenentwurf.AusJson(json, Katalog.Value).Kartengrenzen, "wieder gelesen");
            Pruefe.Gleich(new Kartengrenzen(450, 300, O(10, -4), 400), Stadtkarte.AusJson(json, Katalog.Value).Kartengrenzen, "vom Kern gelesen");
        }),

        ("Prüfung: Gebäude, Bauzonen und Mauern nur im Spielfeld; Wege, Natur und Felder auch im Rand; nichts außerhalb; Kamera im Spielfeld", () =>
        {
            var e = Oeffnen("probestadt");   // Spielfeld ±200 × ±150, Rand 300
            Pruefe.Gleich("", Liste(e.Befunde));
            var huette = e.Setzen("kulisse_buergerhaus", 260, 0);
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "rand" && b.Gebaeude.Contains(huette.Kennung)), Liste(e.Befunde));
            Pruefe.Wahr(Kartenpruefung.PruefeStelle(e, "werkstattplatz", -230, 0, 0).Any(b => b.Regel == "rand"), "Umriss rot im Rand");
            Pruefe.Wahr(Kartenpruefung.PruefeStelle(e, "werkstattplatz", 199, 0, 0).Any(b => b.Regel == "rand"), "auch halb im Rand");
            Pruefe.Wahr(Kartenpruefung.PruefeStelle(e, "werkstattplatz", -520, 0, 0).Any(b => b.Regel == "ausserhalb"), "außerhalb der Karte");
            e.Loeschen(huette.Kennung);
            var zone = e.BauzoneAnlegen(Lage.Umland, Rechteck(220, -40, 260, 40));
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "rand" && b.Andere.Contains(zone)), "Bauzone im Rand");
            e.FlaecheLoeschen(zone);
            var mauer = e.MauerAnlegen(Rechteck(-300, -280, -250, -230), geschlossen: true);
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "rand" && b.Andere.Contains(mauer)), "Mauer im Rand");
            e.Rueckgaengig();
            // erlaubt im Rand: Weg nach draußen, Wald, Feld, Pflanze
            var weg = e.WegAnlegen(Wegklasse.Land, new[] { O(190, 0), O(480, 0) }, glatt: false, einrasten: false);
            e.NaturflaecheAnlegen("wald", Rechteck(250, 200, 400, 400));
            e.FeldAnlegen(Rechteck(-450, -400, -250, -200));
            e.PflanzeSetzen("eiche", -480, 420);
            Pruefe.Gleich("", Liste(Fehler(e)));
            // hinaus: Fehler
            e.StuetzpunktVerschieben(weg, 1, 520, 0, einrasten: false);
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "ausserhalb" && b.Andere.Contains(weg)), Liste(e.Befunde));
            e.Rueckgaengig();
            var fern = e.PflanzeSetzen("eiche", 0, 460);
            Pruefe.Wahr(e.Befunde.Any(b => b.Regel == "ausserhalb" && b.Andere.Contains(fern)), "Pflanze außerhalb");
            e.Rueckgaengig();
            e.KameraStartSetzen(O(250, 0), 100);
            Pruefe.Wahr(Fehler(e) is [{ Regel: "kamera" }], Liste(e.Befunde));
            // die Karte verkleinert: was danach außerhalb liegt, meldet die Prüfung (hier der Wald im Rand und der Kamera-Start)
            e.KameraStartSetzen(O(0, 0), 100);
            e.RandbreiteSetzen(100);
            Pruefe.Wahr(Fehler(e).Any(b => b.Regel == "ausserhalb") && Fehler(e).All(b => b.Regel == "ausserhalb"), Liste(e.Befunde));
            e.Rueckgaengig();
            Pruefe.Gleich("", Liste(Fehler(e)));
        }),

        ("Prüfung: Größe und Randbreite in ihren Grenzen (auch aus einer Datei)", () =>
        {
            var json = JsonNode.Parse(Text("probestadt"))!.AsObject();
            json["groesse"] = new JsonArray(300, 2500);
            json["rand"] = 50;
            var e = Kartenentwurf.AusJson(json.ToJsonString(), Katalog.Value);
            var groesse = e.Befunde.Where(b => b.Regel == "groesse").ToList();
            Pruefe.Gleich(2, groesse.Count, Liste(e.Befunde));
            Pruefe.Wahr(groesse[0].Text.Contains("300 × 2500"), groesse[0].Text);
        }),

        ("Felder: Ackerstreifen in Richtung und Breite, in der Fläche, frei auf Wegen, gleich aus gleichem Startwert", () =>
        {
            var e = Oeffnen("probestadt");
            var feld = e.FeldAnlegen(Rechteck(-480, -440, -260, -200), richtung: 90);
            Pruefe.Gleich((90.0, 8.0, 40.0), (feld.Richtung, feld.Streifen, feld.Laenge));
            Pruefe.Gleich("roggen 1, hafer 1, brache 0.5", string.Join(", ", feld.Fruechte.Select(x => $"{x.Key} {Kartenschreiber.Zahl(x.Value)}")));
            var frei = e.Freiraum();
            var aecker = e.FelderVon(feld, frei);
            Pruefe.Wahr(aecker.Count > 50, $"{aecker.Count} Äcker");
            foreach (var a in aecker)
            {
                Pruefe.Gleich(90.0, a.Drehung);
                Pruefe.Gleich(8.0, a.Breite);
                Pruefe.Wahr(a.Laenge >= FeldVerteilung.KuerzestesStueck && a.Laenge <= 40 * 1.4 + FeldVerteilung.Pruefschritt, $"Länge {a.Laenge}");
                Pruefe.Wahr(a.Ecken().All(p => p.X >= -480.3 && p.X <= -259.7 && p.Z >= -440.3 && p.Z <= -199.7), $"in der Fläche: {a}");
                Pruefe.Wahr(FeldVerteilung.Fruechte.ContainsKey(a.Frucht) && a.Variante is >= 1 and <= FeldVerteilung.Varianten, a.ToString());
            }
            // Richtung 90: die Stücke laufen Ost–West, die Streifen liegen übereinander (gleiche z-Mitte je Streifen)
            Pruefe.Wahr(aecker.Select(a => a.Z).Distinct().Count() < aecker.Count, "mehrere Stücke je Streifen");
            Pruefe.Wahr(aecker.Select(a => a.Frucht).Distinct().Count() == 3, "alle drei Früchte");
            Pruefe.Wahr(aecker.SequenceEqual(e.FelderVon(feld, e.Freiraum())), "gleich gerechnet = gleiche Äcker");

            // ein Weg quer hindurch: nur die Stücke am Weg ändern sich
            var weg = e.WegAnlegen(Wegklasse.Feldweg, new[] { O(-370, -460), O(-370, -180) }, glatt: false, einrasten: false);
            var mitWeg = e.FelderVon(feld, e.Freiraum());
            Pruefe.Wahr(mitWeg.All(a => a.Ecken().All(p => Math.Abs(p.X - (-370)) >= 1.5 + Naturfreiraum.Abstand - FeldVerteilung.Pruefschritt)), "frei am Weg");
            var fern = aecker.Where(a => Math.Abs(a.X - (-370)) > 80).ToList();
            Pruefe.Wahr(fern.Count > 10 && fern.All(mitWeg.Contains), "fern vom Weg bleibt jeder Acker");
            e.WegLoeschen(weg);

            e.FeldStartwertSetzen(feld, feld.Startwert + 1);
            Pruefe.Wahr(!e.FelderVon(feld, e.Freiraum()).SequenceEqual(aecker), "neu gewürfelt");
            e.Rueckgaengig();
            e.FeldFruechteSetzen(feld, new Dictionary<string, double> { ["flachs"] = 1, ["roggen"] = 0 });
            Pruefe.Wahr(e.FelderVon(feld, e.Freiraum()).All(a => a.Frucht == "flachs") && feld.Fruechte.Count == 1, "nur Flachs");
            Pruefe.WirftMitMeldung(() => e.FeldFruechteSetzen(feld, new Dictionary<string, double> { ["mais"] = 1 }), "mais");
            e.FeldRichtungSetzen(feld, 30);
            Pruefe.Wahr(e.FelderVon(feld, e.Freiraum()).All(a => a.Drehung == 30), "gedreht");
            e.FeldStreifenSetzen(feld, 1, 500);
            Pruefe.Gleich((FeldVerteilung.StreifenMin, FeldVerteilung.LaengeMax), (feld.Streifen, feld.Laenge));
            Pruefe.Gleich(0, e.AussparungAnlegen(feld, Rechteck(-400, -400, -350, -350)));
            Pruefe.Wahr(e.FelderVon(feld, e.Freiraum()).All(a => !(a.X > -398 && a.X < -352 && a.Z > -398 && a.Z < -352)), "in der Aussparung kein Acker");
            Pruefe.Gleich("", Liste(Fehler(e)));

            // Datei: natur.felder; der Kern liest dieselben Äcker
            var json = e.ZuJson();
            Pruefe.Wahr(json.Contains("\"felder\": [") && json.Contains("\"fruechte\": {\"flachs\": 1}"), json);
            var wieder = Kartenentwurf.AusJson(json, Katalog.Value);
            Pruefe.Gleich(json, wieder.ZuJson());
            var karte = Stadtkarte.AusJson(json, Katalog.Value);
            Pruefe.Wahr(e.FelderAlle().SequenceEqual(FeldVerteilung.Alle(karte)), "Kern und Werkstatt rechnen dieselben Äcker");
        }),

        ("Felder: Prüfung (Früchte, Streifen, Fläche) und Rückgängig beim Anlegen und Löschen", () =>
        {
            var e = Kartenentwurf.Neu("neu", "Neustadt", 600, 450, Katalog.Value);
            var vorher = e.ZuJson();
            var feld = e.FeldAnlegen(Rechteck(-450, -400, -300, -250), "Gewann am Galgenberg");
            e.FeldFruechteSetzen(feld, new Dictionary<string, double> { ["roggen"] = 0 });
            Pruefe.Wahr(Fehler(e).Count(b => b.Regel == "feld") == 1, Liste(e.Befunde));
            Pruefe.Gleich(0, e.FelderVon(feld, e.Freiraum()).Count);
            e.Rueckgaengig();
            e.FlaecheLoeschen(feld);
            Pruefe.Gleich(0, e.Felder.Count);
            e.Rueckgaengig();
            Pruefe.Gleich("Gewann am Galgenberg", e.Felder[0].Name);
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson(), "Datei wie vorher (ohne felder)");
            var zone = e.BauzoneAnlegen(Lage.Kern, Rechteck(0, 0, 10, 10));
            Pruefe.WirftMitMeldung(() => e.AussparungAnlegen(zone, Rechteck(2, 2, 4, 4)), "keine Aussparungen");
        }),

        ("Rand-Vorschlag: Felder und Waldstücke nur im Randstreifen, ohne neue Fehler, ein Schritt, gleich aus gleichem Startwert", () =>
        {
            var e = Kartenentwurf.Neu("neu", "Neustadt", 600, 450, Katalog.Value);
            var vorher = e.ZuJson();
            var neu = e.RandVorschlagen(7);
            var g = e.Kartengrenzen;
            Pruefe.Wahr(neu.OfType<EntwurfFeld>().Count() >= 8 && neu.OfType<EntwurfNaturflaeche>().Count(n => n.Art == "wald") >= 3,
                $"{neu.OfType<EntwurfFeld>().Count()} Felder, {neu.OfType<EntwurfNaturflaeche>().Count()} Wald/Streu");
            foreach (var f in neu)
            {
                Pruefe.Wahr(f.Ecken.All(p => g.InDerKarte(p.X, p.Z)), $"{f.Bezeichnung} in der Karte");
                Pruefe.Wahr(!Kartenrechteck.Um(f.Ecken)!.Value.Ueberdeckt(g.Spielfeld), $"{f.Bezeichnung} nicht im Spielfeld");
            }
            Pruefe.Wahr(Fehler(e) is [{ Regel: "start-fehlt" }], "nur der fehlende Start: " + Liste(Fehler(e)));
            Pruefe.Wahr(e.Befunde.All(b => b.Regel is not ("feld" or "natur" or "flaeche-kreuzt")), Liste(e.Befunde));
            Pruefe.Wahr(e.FelderAlle().Count > 500, "Äcker");
            Pruefe.Gleich("Randlandschaft vorgeschlagen", e.Geschichte.RueckgaengigText);
            var json = e.ZuJson();
            var zweite = Kartenentwurf.Neu("neu", "Neustadt", 600, 450, Katalog.Value);
            zweite.RandVorschlagen(7);
            Pruefe.Gleich(json, zweite.ZuJson(), "gleicher Startwert, gleicher Vorschlag");
            e.Rueckgaengig();
            Pruefe.Gleich(vorher, e.ZuJson(), "ein Rückgängig nimmt alles zurück");
            e.Wiederholen();
            Pruefe.Gleich(json, e.ZuJson());
        }),

        ("Rand-Vorschlag: ersetzt einen früheren, lässt Flächen im Spielfeld und bedeckte Stücke stehen", () =>
        {
            var e = Kartenentwurf.Neu("neu", "Neustadt", 600, 450, Katalog.Value);
            var stadtwald = e.NaturflaecheAnlegen("wald", Rechteck(100, 100, 200, 200), "Stadtwald");
            var randwald = e.NaturflaecheAnlegen("wald", Rechteck(-600, -500, -350, -300), "Mein Wald im Rand");
            var erster = e.RandVorschlagen(1, ersetzen: false);
            Pruefe.Wahr(!erster.Any(f => Kartengeometrie.ImVieleck(Kartengeometrie.Mitte(f.Ecken), randwald.Ecken)), "bedeckte Stücke bleiben aus");
            var zweiter = e.RandVorschlagen(2);
            Pruefe.Wahr(erster.All(f => !e.Enthaelt(f)), "der erste Vorschlag ist weg");
            Pruefe.Wahr(e.Enthaelt(stadtwald) && !e.Enthaelt(randwald), "der Stadtwald bleibt, der Wald ganz im Rand gehört zum Ersetzten");
            Pruefe.Gleich(1 + zweiter.OfType<EntwurfNaturflaeche>().Count(), e.Naturflaechen.Count);
            Pruefe.Gleich(zweiter.OfType<EntwurfFeld>().Count(), e.Felder.Count);
            e.Rueckgaengig();
            Pruefe.Wahr(erster.All(e.Enthaelt) && e.Enthaelt(randwald), "Rückgängig bringt den ersten zurück");
        }),

        ("Kartenskript Probestadt: Die Datei im Spiel ist die Ausgabe des Skripts (mit Rand-Vorschlag)", () =>
        {
            var e = new Probestadt().Bauen(Katalog.Value, Daten.Value);
            Pruefe.Gleich(Text("probestadt"), e.ZuJson());
            Pruefe.Wahr(e.Felder.Count > 0, "Felder im Rand");
            var karte = Stadtkarte.AusJson(e.ZuJson(), Katalog.Value);
            Pruefe.Gleich(e.Felder.Count, karte.Natur.Felder.Count);
            Pruefe.Wahr(FeldVerteilung.Alle(karte).Count > 0, "der Kern rechnet Äcker");
        }),
    };
}
